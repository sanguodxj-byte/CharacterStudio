using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 运行时资源加载器
    /// 用于从文件系统动态加载纹理
    /// 缓存容器访问已加锁；Unity 对象相关操作仍应在主线程执行
    /// </summary>
    public static class RuntimeAssetLoader
    {
        // 纹理缓存
        private static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

        // 材质缓存
        private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<int, HashSet<string>> textureMaterialCacheKeys = new Dictionary<int, HashSet<string>>();
        private static readonly Dictionary<int, bool> textureSemiTransparencyCache = new Dictionary<int, bool>();

        // 文件修改时间缓存（用于热加载检测）
        private static readonly Dictionary<string, DateTime> fileLastWriteTimes = new Dictionary<string, DateTime>();

        // P-PERF: 缓存访问时间记录（用于LRU淘汰策略），使用 Environment.TickCount 替代 DateTime.Now 降低系统调用开销
        private static readonly Dictionary<string, int> cacheAccessTimes = new Dictionary<string, int>();

        // 最大缓存数量
        private const int MaxTextureCacheSize = 512;
        private const int MaxMaterialCacheSize = 1024;

        // P2: 纹理 InstanceID → 缓存路径 反向索引（O(1) 查找替代 O(N) 遍历）
        private static readonly Dictionary<int, string> textureInstanceIdToPath = new Dictionary<int, string>();

        // P3: 已执行过透明边缘修正的纹理路径集合
        private static readonly HashSet<string> edgeBleedingProcessedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // P7: 路径解析结果缓存（避免重复目录扫描）
        private static readonly Dictionary<string, string> resolvedPathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // P1: 文件修改检查节流（避免每帧文件系统 I/O）
        private static DateTime lastFileModCheckTime = DateTime.MinValue;
        // P-PERF: 编辑器模式下也使用节流（0.5秒），避免每帧 File.GetLastWriteTime
        private static DateTime lastEditorFileModCheckTime = DateTime.MinValue;
        private static readonly TimeSpan editorFileModCheckInterval = TimeSpan.FromSeconds(0.5);

        // 线程同步锁
        private static readonly object textureCacheLock = new object();
        private static readonly object materialCacheLock = new object();
        private static readonly HashSet<string> nonMainThreadLoadWarnings = new HashSet<string>();
        private static readonly HashSet<string> missingFileWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> textureLoadFailureErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> materialCreationWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, byte[]> pendingTextureBytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> pendingTextureReadRequests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> pendingTextureReadFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ─────────────────────────────────────────────
        // 节点数据注册表 (Patch层 → Worker层 数据传递)
        // ─────────────────────────────────────────────

        // 节点东/西方向偏移量注册表
        private static readonly Dictionary<int, Vector3> nodeOffsetEastRegistry = new Dictionary<int, Vector3>();
        // 节点北方向偏移量注册表
        private static readonly Dictionary<int, Vector3> nodeOffsetNorthRegistry = new Dictionary<int, Vector3>();
        private static readonly object nodeRegistryLock = new object();

        // ─────────────────────────────────────────────
        // 纹理加载
        // ─────────────────────────────────────────────

        /// <summary>
        /// 从文件路径加载纹理
        /// </summary>
        /// <param name="fullPath">完整文件路径</param>
        /// <param name="useCache">是否使用缓存</param>
        /// <returns>加载的纹理，失败返回 null</returns>
        
        private static bool ShouldCheckFileModificationAggressively()
        {
            if (!IsMainThread()) return false;
            try 
            {
                if (Current.ProgramState != ProgramState.Playing) return true;
                if (Find.WindowStack != null && Find.WindowStack.IsOpen(typeof(CharacterStudio.UI.Dialog_SkinEditor))) return true;
            }
            catch {}
            return false;
        }

        public static Texture2D? LoadTextureRaw(string fullPath, bool useCache = true)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return null;
            }

            string requestedPath = fullPath;
            string resolvedPath = ResolveExistingTexturePath(fullPath);

            // P-PERF: 仅编辑器模式检查文件修改（热重载），游戏内跳过磁盘 I/O
            bool aggressive = ShouldCheckFileModificationAggressively();
            bool checkMod = false;
            if (aggressive)
            {
                DateTime now = DateTime.Now;
                TimeSpan elapsed = now - lastEditorFileModCheckTime;
                if (elapsed.TotalSeconds >= editorFileModCheckInterval.TotalSeconds)
                {
                    checkMod = true;
                    lastEditorFileModCheckTime = now;
                }
            }

            // 检查缓存（线程安全）
            if (useCache)
            {
                lock (textureCacheLock)
                {
                    if (textureCache.TryGetValue(resolvedPath, out var cachedTex))
                    {
                        if (cachedTex != null)
                        {
                            if (checkMod && IsFileModified(resolvedPath))
                            {
                                RemoveFromCacheInternal(resolvedPath);
                            }
                            else
                            {
                                cacheAccessTimes[resolvedPath] = Environment.TickCount;
                                return cachedTex;
                            }
                        }
                        else
                        {
                            textureCache.Remove(resolvedPath);
                            cacheAccessTimes.Remove(resolvedPath);
                        }
                    }
                }
            }

            try
            {
                if (!File.Exists(resolvedPath))
                {
                    LogWarningOnce(
                        missingFileWarnings,
                        requestedPath.Trim(),
                        $"[CharacterStudio] 外部纹理文件不存在，已跳过加载: {requestedPath}");
                    return null;
                }

                if (!IsMainThread())
                {
                    QueueBackgroundTextureRead(resolvedPath);
                    lock (textureCacheLock)
                    {
                        if (nonMainThreadLoadWarnings.Add(resolvedPath))
                        {
                            Log.Warning($"[CharacterStudio] 已将外部纹理读取排队到后台线程，等待主线程完成纹理创建: {resolvedPath}");
                        }
                    }
                    return null;
                }

                if (TryFinalizeQueuedTexture(resolvedPath, useCache, out Texture2D? queuedTexture))
                {
                    return queuedTexture;
                }

                if (!string.Equals(requestedPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Message($"[CharacterStudio] 外部纹理路径已回退解析: {requestedPath} -> {resolvedPath}");
                }

                // Log.Message($"[CharacterStudio] 正在加载外部纹理: {resolvedPath}"); // 调试日志

                byte[] bytes = File.ReadAllBytes(resolvedPath);
                return CreateTextureFromBytes(resolvedPath, bytes, useCache);
            }
            catch (Exception ex)
            {
                LogErrorOnce(
                    textureLoadFailureErrors,
                    resolvedPath,
                    $"[CharacterStudio] 加载纹理时出错: {resolvedPath}\n{ex}");
                return null;
            }
        }

        /// <summary>
        /// 检查文件是否被修改
        /// </summary>
        private static bool IsFileModified(string fullPath)
        {
            try
            {
                if (!File.Exists(fullPath)) return false;

                DateTime currentWriteTime = File.GetLastWriteTime(fullPath);
                if (fileLastWriteTimes.TryGetValue(fullPath, out var lastWriteTime))
                {
                    bool modified = currentWriteTime > lastWriteTime;
                    if (modified) fileLastWriteTimes[fullPath] = currentWriteTime;
                    return modified;
                }
                fileLastWriteTimes[fullPath] = currentWriteTime;
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static string ResolveTexturePathForLoad(string fullPath)
        {
            return ResolveExistingTexturePath(fullPath);
        }

        public static bool LooksLikeExternalTexturePath(string? texturePath)
        {
            if (string.IsNullOrWhiteSpace(texturePath))
            {
                return false;
            }

            return texturePath != null
                && (texturePath.Contains(":")
                    || texturePath.StartsWith("/")
                    || Path.IsPathRooted(texturePath));
        }

        public static bool ExternalTextureExists(string fullPath, out string resolvedPath)
        {
            resolvedPath = ResolveExistingTexturePath(fullPath);
            return !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath);
        }

        private static void LogWarningOnce(HashSet<string> warningSet, string key, string message)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (textureCacheLock)
            {
                if (!warningSet.Add(key))
                {
                    return;
                }
            }

            Log.Warning(message);
        }

        private static void LogErrorOnce(HashSet<string> errorSet, string key, string message)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (textureCacheLock)
            {
                if (!errorSet.Add(key))
                {
                    return;
                }
            }

            Log.Error(message);
        }

        private static string ResolveExistingTexturePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return fullPath;

            lock (textureCacheLock)
            {
                if (resolvedPathCache.TryGetValue(fullPath, out string? cached))
                    return cached;
            }

            string result = ResolveExistingTexturePathCore(fullPath);

            lock (textureCacheLock)
            {
                resolvedPathCache[fullPath] = result;
            }

            return result;
        }

        private static string ResolveExistingTexturePathCore(string fullPath)
        {
            if (File.Exists(fullPath))
                return fullPath;

            try
            {
                string? directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                    return fullPath;

                string requestedFileName = Path.GetFileName(fullPath);
                if (string.IsNullOrWhiteSpace(requestedFileName))
                    return fullPath;

                string[] siblingFiles = Directory.GetFiles(directory)
                    .Where(IsSupportedImageFormat)
                    .ToArray();
                if (siblingFiles.Length == 0)
                    return fullPath;

                string? exactNameMatch = siblingFiles.FirstOrDefault(candidate =>
                    string.Equals(Path.GetFileName(candidate), requestedFileName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exactNameMatch))
                    return exactNameMatch;

                string requestedStem = Path.GetFileNameWithoutExtension(fullPath) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(requestedStem))
                    return fullPath;

                string? exactStemMatch = siblingFiles.FirstOrDefault(candidate =>
                    string.Equals(Path.GetFileNameWithoutExtension(candidate), requestedStem, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exactStemMatch))
                    return exactStemMatch;

                string prefix = requestedStem + "_";
                string? variantStemMatch = siblingFiles
                    .Where(candidate =>
                    {
                        string candidateStem = Path.GetFileNameWithoutExtension(candidate) ?? string.Empty;
                        return candidateStem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(candidate => Path.GetFileName(candidate), StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(variantStemMatch))
                    return variantStemMatch;
            }
            catch
            {
            }

            return fullPath;
        }

        private static void FixTransparentEdgeBleeding(Texture2D texture)
        {
            try
            {
                int width = texture.width;
                int height = texture.height;
                if (width <= 1 || height <= 1)
                {
                    return;
                }

                Color32[] source = texture.GetPixels32();
                Color32[] result = new Color32[source.Length];
                Array.Copy(source, result, source.Length);

                bool changed = false;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        Color32 current = source[index];
                        if (current.a > 8)
                        {
                            continue;
                        }

                        int totalR = 0;
                        int totalG = 0;
                        int totalB = 0;
                        int totalWeight = 0;

                        for (int ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
                        {
                            for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                            {
                                if (nx == x && ny == y)
                                {
                                    continue;
                                }

                                Color32 neighbor = source[ny * width + nx];
                                if (neighbor.a <= 8)
                                {
                                    continue;
                                }

                                int weight = Math.Max(1, (int)neighbor.a);
                                totalR += neighbor.r * weight;
                                totalG += neighbor.g * weight;
                                totalB += neighbor.b * weight;
                                totalWeight += weight;
                            }
                        }

                        if (totalWeight <= 0)
                        {
                            continue;
                        }

                        result[index] = new Color32(
                            (byte)(totalR / totalWeight),
                            (byte)(totalG / totalWeight),
                            (byte)(totalB / totalWeight),
                            current.a);
                        changed = true;
                    }
                }

                if (changed)
                {
                    texture.SetPixels32(result);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 修正透明边缘采样时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 从模组路径加载纹理
        /// </summary>
        /// <param name="modContentPack">模组内容包</param>
        /// <param name="relativePath">相对于 Textures 文件夹的路径（不含扩展名）</param>
        public static Texture2D? LoadTextureFromMod(ModContentPack modContentPack, string relativePath)
        {
            if (modContentPack == null || string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            // 构建完整路径
            string basePath = Path.Combine(modContentPack.RootDir, "Textures", relativePath);

            // 尝试不同的扩展名
            string[] extensions = { ".png", ".PNG", ".jpg", ".JPG", ".jpeg", ".JPEG" };

            foreach (var ext in extensions)
            {
                string fullPath = basePath + ext;
                if (File.Exists(fullPath))
                {
                    return LoadTextureRaw(fullPath);
                }
            }

            Log.Warning($"[CharacterStudio] 无法在模组中找到纹理: {relativePath}");
            return null;
        }

        // ─────────────────────────────────────────────
        // 材质创建
        // ─────────────────────────────────────────────

        /// <summary>
        /// 为纹理创建材质
        /// </summary>
        /// <param name="texture">源纹理</param>
        /// <param name="shader">着色器（默认使用 Cutout）</param>
        public static Material? GetMaterialForTexture(Texture2D texture, Shader? shader = null)
        {
            if (texture == null)
            {
                return null;
            }

            if (!IsMainThread())
            {
                return null;
            }

            try
            {
                shader ??= ShaderDatabase.Cutout;
                shader = ResolveRecommendedShaderForTexture(texture, shader);
                string cacheKey = GetMaterialCacheKey(texture, shader);

                lock (materialCacheLock)
                {
                    if (materialCache.TryGetValue(cacheKey, out var cachedMat))
                    {
                        if (cachedMat != null)
                        {
                            return cachedMat;
                        }

                        materialCache.Remove(cacheKey);
                    }

                    EnsureCacheCapacity(materialCache, null, MaxMaterialCacheSize);

                    var mat = MaterialPool.MatFrom(
                        new MaterialRequest(texture, shader)
                    );

                    materialCache[cacheKey] = mat;
                    RegisterMaterialCacheKeyForTexture(texture.GetInstanceID(), cacheKey);
                    return mat;
                }
            }
            catch (Exception ex)
            {
                string textureName = texture?.name ?? "<null>";
                LogWarningOnce(
                    materialCreationWarnings,
                    textureName,
                    $"[CharacterStudio] 创建材质失败，已回退为空材质: {textureName}, {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 直接从文件创建材质
        /// </summary>
        public static Material? GetMaterialFromFile(string fullPath, Shader? shader = null)
        {
            var tex = LoadTextureRaw(fullPath);
            if (tex == null)
            {
                return null;
            }

            return GetMaterialForTexture(tex, shader);
        }

        private static Shader ResolveRecommendedShaderForTexture(Texture2D texture, Shader? requestedShader)
        {
            Shader resolvedShader = requestedShader ?? ShaderDatabase.Cutout;
            if (!IsCutoutLikeShader(resolvedShader))
            {
                return resolvedShader;
            }

            // 真实原因修复：
            // 切勿因为贴图包含半透明像素就强制将 Shader 回退到 Transparent 或 TransparentPostLight！
            // RimWorld 的角色渲染必须使用 Cutout 系列材质（ZWrite On）来写入深度缓冲区。
            // 一旦回退到 Transparent（ZWrite Off），角色将无法阻挡更低层级的阴影（如自带脚底阴影 Graphic_Shadow，以及投射在草丛上的树影）。
            // 这会导致角色的影子画在角色身上，或者透过半透明像素看到底部草丛上的阴影形状。
            return resolvedShader;
        }

        private static bool IsCutoutLikeShader(Shader shader)
        {
            string shaderName = shader.name ?? string.Empty;
            return shaderName.IndexOf("Cutout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCutoutComplexShader(Shader shader)
        {
            string shaderName = shader.name ?? string.Empty;
            return shaderName.IndexOf("CutoutComplex", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool TextureHasSemiTransparentPixels(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }

            if (!IsMainThread())
            {
                return false;
            }

            int textureInstanceId = texture.GetInstanceID();
            lock (textureCacheLock)
            {
                if (textureSemiTransparencyCache.TryGetValue(textureInstanceId, out bool cachedValue))
                {
                    return cachedValue;
                }
            }

            bool hasSemiTransparentPixels = false;
            try
            {
                Color32[] pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte alpha = pixels[i].a;
                    if (alpha > 0 && alpha < 255)
                    {
                        hasSemiTransparentPixels = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 检测纹理半透明像素时出错: {texture.name} - {ex.Message}");
            }

            lock (textureCacheLock)
            {
                textureSemiTransparencyCache[textureInstanceId] = hasSemiTransparentPixels;
            }

            return hasSemiTransparentPixels;
        }

        // ─────────────────────────────────────────────
        // 缓存管理
        // ─────────────────────────────────────────────

        /// <summary>
        /// 清除所有缓存（线程安全）
        /// </summary>
        public static void ClearAllCaches()
        {
            lock (textureCacheLock)
            {
                // 销毁所有缓存的纹理
                foreach (var tex in textureCache.Values)
                {
                    if (tex != null)
                    {
                        UnityEngine.Object.Destroy(tex);
                    }
                }
                textureCache.Clear();
                fileLastWriteTimes.Clear();
                cacheAccessTimes.Clear();
                nonMainThreadLoadWarnings.Clear();
                missingFileWarnings.Clear();
                textureLoadFailureErrors.Clear();
                textureSemiTransparencyCache.Clear();
                textureInstanceIdToPath.Clear();
                edgeBleedingProcessedPaths.Clear();
                resolvedPathCache.Clear();
            }

            lock (materialCacheLock)
            {
                // 材质由 MaterialPool 管理，不需要手动销毁
                materialCache.Clear();
                textureMaterialCacheKeys.Clear();
            }

            Log.Message("[CharacterStudio] 缓存已清除");
        }

        /// <summary>
        /// 从缓存中移除特定纹理（线程安全）
        /// </summary>
        public static void RemoveFromCache(string fullPath)
        {
            lock (textureCacheLock)
            {
                RemoveFromCacheInternal(fullPath);
            }
        }

        /// <summary>
        /// 内部移除缓存方法（需在锁内调用）
        /// </summary>
        private static void RemoveFromCacheInternal(string fullPath)
        {
            if (textureCache.TryGetValue(fullPath, out var tex))
            {
                if (tex != null)
                {
                    int textureInstanceId = tex.GetInstanceID();
                    RemoveMaterialCacheEntriesForTextureInternal(textureInstanceId);
                    textureSemiTransparencyCache.Remove(textureInstanceId);
                    textureInstanceIdToPath.Remove(textureInstanceId);
                    UnityEngine.Object.Destroy(tex);
                }

                textureCache.Remove(fullPath);
            }

            fileLastWriteTimes.Remove(fullPath);
            cacheAccessTimes.Remove(fullPath);
            nonMainThreadLoadWarnings.Remove(fullPath);
            pendingTextureBytes.Remove(fullPath);
            pendingTextureReadRequests.Remove(fullPath);
            pendingTextureReadFailures.Remove(fullPath);
        }

        private static void RegisterMaterialCacheKeyForTexture(int textureInstanceId, string cacheKey)
        {
            if (!textureMaterialCacheKeys.TryGetValue(textureInstanceId, out var cacheKeys))
            {
                cacheKeys = new HashSet<string>();
                textureMaterialCacheKeys[textureInstanceId] = cacheKeys;
            }

            cacheKeys.Add(cacheKey);
        }

        private static void RemoveMaterialCacheEntriesForTextureInternal(int textureInstanceId)
        {
            lock (materialCacheLock)
            {
                if (!textureMaterialCacheKeys.TryGetValue(textureInstanceId, out var cacheKeys))
                {
                    return;
                }

                foreach (var cacheKey in cacheKeys)
                {
                    materialCache.Remove(cacheKey);
                }

                textureMaterialCacheKeys.Remove(textureInstanceId);
            }
        }

        private static void RemoveMaterialCacheKeyInternal(int textureInstanceId, string cacheKey)
        {
            if (!textureMaterialCacheKeys.TryGetValue(textureInstanceId, out var cacheKeys))
            {
                return;
            }

            cacheKeys.Remove(cacheKey);
            if (cacheKeys.Count == 0)
            {
                textureMaterialCacheKeys.Remove(textureInstanceId);
            }
        }

        /// <summary>
        /// 获取缓存统计信息（线程安全）
        /// </summary>
        public static string GetCacheStats()
        {
            int texCount, matCount;
            lock (textureCacheLock)
            {
                texCount = textureCache.Count;
            }
            lock (materialCacheLock)
            {
                matCount = materialCache.Count;
            }
            return $"纹理缓存: {texCount}/{MaxTextureCacheSize} 项, 材质缓存: {matCount}/{MaxMaterialCacheSize} 项";
        }

        // P-PERF: 复用排序缓冲区，避免 EnsureCacheCapacity 每次淘汰 new List + Sort 分配
        private static List<KeyValuePair<string, int>>? _evictionSortBuffer;

        /// <summary>
        /// 确保缓存不超过最大容量（LRU淘汰策略）
        /// </summary>
        private static void EnsureCacheCapacity<T>(Dictionary<string, T> cache, Dictionary<string, int>? accessTimes, int maxSize)
        {
            if (cache.Count < maxSize) return;

            // 需要淘汰的数量（淘汰10%）
            int toRemove = Math.Max(1, maxSize / 10);

            if (accessTimes != null && accessTimes.Count > 0)
            {
                // P-PERF: 复用排序缓冲区
                var sorted = _evictionSortBuffer ?? new List<KeyValuePair<string, int>>(accessTimes.Count);
                sorted.Clear();
                _evictionSortBuffer = null; // 防止重入

                foreach (var kv in accessTimes)
                {
                    if (cache.ContainsKey(kv.Key))
                        sorted.Add(kv);
                }
                sorted.Sort((a, b) => a.Value.CompareTo(b.Value));

                int evicted = 0;
                for (int i = 0; i < sorted.Count && evicted < toRemove; i++)
                {
                    string key = sorted[i].Key;
                    if (cache.TryGetValue(key, out var item))
                    {
                        if (item is Texture2D tex)
                        {
                            int textureInstanceId = tex.GetInstanceID();
                            RemoveMaterialCacheEntriesForTextureInternal(textureInstanceId);
                            textureSemiTransparencyCache.Remove(textureInstanceId);
                            textureInstanceIdToPath.Remove(textureInstanceId);
                            UnityEngine.Object.Destroy(tex);
                        }
                        cache.Remove(key);
                    }
                    accessTimes.Remove(key);
                    fileLastWriteTimes.Remove(key);
                    evicted++;
                }

                sorted.Clear();
                _evictionSortBuffer = sorted;
            }
            else
            {
                // P-PERF: 用枚举器遍历代替 .Take().ToList() 分配
                int removed = 0;
                using (var enumerator = cache.GetEnumerator())
                {
                    while (removed < toRemove && enumerator.MoveNext())
                    {
                        string key = enumerator.Current.Key;
                        if (cache.TryGetValue(key, out var item) && item is Material material && material.mainTexture is Texture2D texture)
                        {
                            RemoveMaterialCacheKeyInternal(texture.GetInstanceID(), key);
                        }
                        // 延迟收集要移除的 key（不能在枚举中直接 Remove）
                        // 使用文件级复用列表
                        if (_evictionKeysBuffer == null)
                            _evictionKeysBuffer = new List<string>(toRemove);
                        _evictionKeysBuffer.Add(key);
                        removed++;
                    }
                }
                if (_evictionKeysBuffer != null)
                {
                    for (int i = 0; i < _evictionKeysBuffer.Count; i++)
                        cache.Remove(_evictionKeysBuffer[i]);
                    _evictionKeysBuffer.Clear();
                }
            }
        }

        // P-PERF: 复用 key 淘汰缓冲区
        private static List<string>? _evictionKeysBuffer;

        /// <summary>
        /// 更新缓存访问时间（需在锁内调用）
        /// </summary>
        private static void UpdateAccessTime(string key)
        {
            cacheAccessTimes[key] = Environment.TickCount;
        }

        /// <summary>
        /// 检查是否在主线程上
        /// Unity纹理操作必须在主线程执行
        /// </summary>
        public static bool IsMainThread()
        {
            // 在Unity中，可以通过检查当前线程是否是主线程来判断
            // 这里使用简单的线程ID比较（需要在初始化时记录主线程ID）
            return System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId;
        }

        private static readonly int mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

        private static void QueueBackgroundTextureRead(string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return;
            }

            lock (textureCacheLock)
            {
                if (pendingTextureBytes.ContainsKey(resolvedPath) || pendingTextureReadRequests.Contains(resolvedPath))
                {
                    return;
                }

                pendingTextureReadRequests.Add(resolvedPath);
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(resolvedPath);
                    lock (textureCacheLock)
                    {
                        pendingTextureReadRequests.Remove(resolvedPath);
                        pendingTextureReadFailures.Remove(resolvedPath);
                        pendingTextureBytes[resolvedPath] = bytes;
                    }
                }
                catch (Exception ex)
                {
                    bool shouldLog;
                    lock (textureCacheLock)
                    {
                        pendingTextureReadRequests.Remove(resolvedPath);
                        pendingTextureBytes.Remove(resolvedPath);
                        shouldLog = pendingTextureReadFailures.Add(resolvedPath);
                    }

                    if (shouldLog)
                    {
                        Log.Warning($"[CharacterStudio] 后台读取外部纹理失败: {resolvedPath} - {ex.Message}");
                    }
                }
            });
        }

        private static bool TryFinalizeQueuedTexture(string resolvedPath, bool useCache, out Texture2D? texture)
        {
            texture = null;

            byte[]? queuedBytes = null;
            lock (textureCacheLock)
            {
                if (!pendingTextureBytes.TryGetValue(resolvedPath, out queuedBytes) || queuedBytes == null)
                {
                    return false;
                }

                pendingTextureBytes.Remove(resolvedPath);
            }

            texture = CreateTextureFromBytes(resolvedPath, queuedBytes, useCache);
            if (texture == null)
            {
                QueueBackgroundTextureRead(resolvedPath);
            }
            return texture != null;
        }

        private static Texture2D? CreateTextureFromBytes(string resolvedPath, byte[] bytes, bool useCache)
        {
            if (!IsMainThread())
            {
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = Path.GetFileNameWithoutExtension(resolvedPath);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 0;

            if (!ImageConversion.LoadImage(tex, bytes))
            {
                LogErrorOnce(
                    textureLoadFailureErrors,
                    resolvedPath,
                    $"[CharacterStudio] 无法解析图像: {resolvedPath}");
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            // P-PERF: 仅对使用 Transparent 类 Shader 的纹理执行边缘颜色修正。
            // Cutout 纹理通过 alpha test 硬裁剪，不存在半透明边缘混合问题，
            // 跳过可避免 ~2.3M 次像素遍历（512×512 纹理）的 CPU 开销。
            bool needsEdgeFix = !resolvedPath.EndsWith("_north", StringComparison.OrdinalIgnoreCase)
                && !resolvedPath.EndsWith("_east", StringComparison.OrdinalIgnoreCase)
                && !resolvedPath.EndsWith("_south", StringComparison.OrdinalIgnoreCase)
                && !resolvedPath.EndsWith("_west", StringComparison.OrdinalIgnoreCase)
                && resolvedPath.IndexOf("Cutout", StringComparison.OrdinalIgnoreCase) < 0;

            if (needsEdgeFix && edgeBleedingProcessedPaths.Add(resolvedPath))
            {
                FixTransparentEdgeBleeding(tex);
            }
            tex.Apply(false, false);

            if (useCache)
            {
                lock (textureCacheLock)
                {
                    try { fileLastWriteTimes[resolvedPath] = File.GetLastWriteTime(resolvedPath); } catch {}
                    EnsureCacheCapacity(textureCache, cacheAccessTimes, MaxTextureCacheSize);
                    textureCache[resolvedPath] = tex;
                    cacheAccessTimes[resolvedPath] = Environment.TickCount;
                    textureInstanceIdToPath[tex.GetInstanceID()] = resolvedPath;
                }
            }

            return tex;
        }

        // ─────────────────────────────────────────────
        // 节点数据注册表方法
        // ─────────────────────────────────────────────

        /// <summary>
        /// 注册节点的东/西方向偏移量
        /// </summary>
        /// <param name="nodeId">节点ID (通常使用 node.GetHashCode())</param>
        /// <param name="offset">偏移量向量</param>
        public static void RegisterNodeOffsetEast(int nodeId, Vector3 offset)
        {
            lock (nodeRegistryLock)
            {
                nodeOffsetEastRegistry[nodeId] = offset;
            }
        }

        /// <summary>
        /// 尝试获取节点的东/西方向偏移量
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <param name="offset">输出偏移量</param>
        /// <returns>是否找到注册的偏移量</returns>
        public static bool TryGetOffsetEast(int nodeId, out Vector3 offset)
        {
            lock (nodeRegistryLock)
            {
                return nodeOffsetEastRegistry.TryGetValue(nodeId, out offset);
            }
        }

        /// <summary>
        /// 注册节点的北方向偏移量
        /// </summary>
        /// <param name="nodeId">节点ID (通常使用 node.GetHashCode())</param>
        /// <param name="offset">偏移量向量</param>
        public static void RegisterNodeOffsetNorth(int nodeId, Vector3 offset)
        {
            lock (nodeRegistryLock)
            {
                nodeOffsetNorthRegistry[nodeId] = offset;
            }
        }

        /// <summary>
        /// 尝试获取节点的北方向偏移量
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <param name="offset">输出偏移量</param>
        /// <returns>是否找到注册的偏移量</returns>
        public static bool TryGetOffsetNorth(int nodeId, out Vector3 offset)
        {
            lock (nodeRegistryLock)
            {
                return nodeOffsetNorthRegistry.TryGetValue(nodeId, out offset);
            }
        }

        /// <summary>
        /// 注销单个节点的偏移量注册
        /// </summary>
        public static void UnregisterNodeOffsets(int nodeId)
        {
            lock (nodeRegistryLock)
            {
                nodeOffsetEastRegistry.Remove(nodeId);
                nodeOffsetNorthRegistry.Remove(nodeId);
            }
        }

        /// <summary>
        /// 清除所有节点注册表数据
        /// </summary>
        public static void ClearNodeRegistry()
        {
            lock (nodeRegistryLock)
            {
                nodeOffsetEastRegistry.Clear();
                nodeOffsetNorthRegistry.Clear();
            }
        }

        /// <summary>
        /// 获取材质的变体版本（用于表情状态：Blink、Sleep等）
        /// </summary>
        /// <param name="baseMat">基础材质</param>
        /// <param name="suffix">后缀名（如 "_Blink", "_Sleep"）</param>
        /// <returns>变体材质，如果不存在则返回null</returns>
        public static Material? GetVariant(Material baseMat, string suffix)
        {
            if (baseMat == null || baseMat.mainTexture == null || string.IsNullOrEmpty(suffix))
            {
                return null;
            }

            if (!IsMainThread())
            {
                return null;
            }

            string baseName = baseMat.mainTexture.name;
            string variantName = baseName + suffix;

            // 检查材质缓存中是否已有变体
            lock (materialCacheLock)
            {
                string cacheKey = $"{variantName}_{baseMat.shader?.name ?? "Cutout"}";
                if (materialCache.TryGetValue(cacheKey, out var cachedMat) && cachedMat != null)
                {
                    return cachedMat;
                }
            }

            // 尝试从内容数据库加载变体纹理
            var variantTex = ContentFinder<Texture2D>.Get(variantName, false);
            if (variantTex == null)
            {
                return null; // 变体不存在
            }

            // 创建变体材质
            var variantMat = GetMaterialForTexture(variantTex, baseMat.shader);
            return variantMat;
        }

        // ─────────────────────────────────────────────
        // 实用方法
        // ─────────────────────────────────────────────

        /// <summary>
        /// 检查文件是否为支持的图像格式
        /// </summary>
        public static bool IsSupportedImageFormat(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        /// <summary>
        /// 获取图像尺寸（不加载整个图像）
        /// </summary>
        public static Vector2Int GetImageDimensions(string fullPath)
        {
            try
            {
                // 简单方法：加载图像获取尺寸
                var tex = LoadTextureRaw(fullPath, false); // 不使用缓存加载，避免污染
                if (tex != null)
                {
                    var size = new Vector2Int(tex.width, tex.height);
                    UnityEngine.Object.Destroy(tex);
                    return size;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 获取图像尺寸失败: {ex.Message}");
            }

            return Vector2Int.zero;
        }

        private static string GetMaterialCacheKey(Texture2D texture, Shader shader)
        {
            int instanceId = texture.GetInstanceID();
            string textureIdentity;

            lock (textureCacheLock)
            {
                if (!textureInstanceIdToPath.TryGetValue(instanceId, out textureIdentity!))
                {
                    textureIdentity = texture.name;
                }
            }

            return $"{textureIdentity}_{shader.name}";
        }

        /// <summary>
        /// 主线程 tick 回调：消费后台读取的纹理字节数据，创建 Texture2D 对象。
        /// 每次 tick 最多处理 4 个，避免单帧卡顿。
        /// </summary>
        public static void TickProcessPendingTextures()
        {
            if (!IsMainThread()) return;

            int processed = 0;
            while (processed < 4)
            {
                string? path = null;
                byte[]? bytes = null;

                lock (textureCacheLock)
                {
                    foreach (var kvp in pendingTextureBytes)
                    {
                        path = kvp.Key;
                        bytes = kvp.Value;
                        break;
                    }
                }

                if (path == null || bytes == null)
                    break;

                lock (textureCacheLock)
                {
                    pendingTextureBytes.Remove(path);
                }

                CreateTextureFromBytes(path, bytes, true);
                processed++;
            }
        }
    }
}