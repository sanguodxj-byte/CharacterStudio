using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 运行时资源加载器
    /// 用于从文件系统动态加载纹理
    /// 线程安全实现
    /// </summary>
    public static class RuntimeAssetLoader
    {
        // 纹理缓存
        private static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        
        // 材质缓存
        private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

        // 文件修改时间缓存（用于热加载检测）
        private static readonly Dictionary<string, DateTime> fileLastWriteTimes = new Dictionary<string, DateTime>();

        // 缓存访问时间记录（用于LRU淘汰策略）
        private static readonly Dictionary<string, DateTime> cacheAccessTimes = new Dictionary<string, DateTime>();

        // 最大缓存数量
        private const int MaxTextureCacheSize = 100;
        private const int MaxMaterialCacheSize = 200;

        // 线程同步锁
        private static readonly object textureCacheLock = new object();
        private static readonly object materialCacheLock = new object();

        // ─────────────────────────────────────────────
        // 节点数据注册表 (Patch层 → Worker层 数据传递)
        // ─────────────────────────────────────────────

        // 节点东/西方向偏移量注册表
        private static readonly Dictionary<int, Vector3> nodeOffsetEastRegistry = new Dictionary<int, Vector3>();
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
        public static Texture2D? LoadTextureRaw(string fullPath, bool useCache = true)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                Log.Warning("[CharacterStudio] 纹理路径为空");
                return null;
            }

            // 检查缓存（线程安全）
            if (useCache)
            {
                lock (textureCacheLock)
                {
                    if (textureCache.TryGetValue(fullPath, out var cachedTex))
                    {
                        // 检查文件是否被修改（仅在编辑器模式下）
                        if (IsFileModified(fullPath))
                        {
                            // 文件已修改，移除缓存并重新加载
                            RemoveFromCacheInternal(fullPath);
                        }
                        else if (cachedTex != null)
                        {
                            // 更新访问时间
                            cacheAccessTimes[fullPath] = DateTime.Now;
                            return cachedTex;
                        }
                        else
                        {
                            textureCache.Remove(fullPath);
                            cacheAccessTimes.Remove(fullPath);
                        }
                    }
                }
            }

            try
            {
                if (!File.Exists(fullPath))
                {
                    Log.Warning($"[CharacterStudio] 文件不存在: {fullPath}");
                    return null;
                }

                // Log.Message($"[CharacterStudio] 正在加载外部纹理: {fullPath}"); // 调试日志

                // 读取文件字节（在锁外执行IO操作）
                byte[] bytes = File.ReadAllBytes(fullPath);
                
                // 创建纹理（需要在主线程执行）
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.name = Path.GetFileNameWithoutExtension(fullPath);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                // 加载图像数据
                if (!ImageConversion.LoadImage(tex, bytes))
                {
                    Log.Error($"[CharacterStudio] 无法解析图像: {fullPath}");
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }

                // 压缩纹理以节省内存 (暂时禁用压缩，以防兼容性问题导致显示空白)
                // tex.Compress(true);
                // 禁用 Mipmap 生成，因为我们在创建 Texture2D 时指定了 no mipmaps
                // 且 FilterMode 为 Point，不需要 Mipmap
                tex.Apply(false, false); // 保持可读以便调试，且暂不释放CPU内存

                // 添加到缓存（线程安全）
                if (useCache)
                {
                    lock (textureCacheLock)
                    {
                        // 更新文件修改时间
                        fileLastWriteTimes[fullPath] = File.GetLastWriteTime(fullPath);
                        
                        EnsureCacheCapacity(textureCache, cacheAccessTimes, MaxTextureCacheSize);
                        textureCache[fullPath] = tex;
                        cacheAccessTimes[fullPath] = DateTime.Now;
                    }
                }

                return tex;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 加载纹理时出错: {fullPath}\n{ex}");
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
                    return currentWriteTime > lastWriteTime;
                }
                return true; // 如果没有记录，视为已修改
            }
            catch
            {
                return false;
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

            string cacheKey = $"{texture.name}_{shader?.name ?? "Cutout"}";

            // 检查缓存
            if (materialCache.TryGetValue(cacheKey, out var cachedMat))
            {
                if (cachedMat != null)
                {
                    return cachedMat;
                }
                materialCache.Remove(cacheKey);
            }

            try
            {
                // 使用默认着色器
                if (shader == null)
                {
                    shader = ShaderDatabase.Cutout;
                }

                // 创建材质
                var mat = MaterialPool.MatFrom(
                    new MaterialRequest(texture, shader)
                );

                // 添加到缓存（带LRU管理）
                EnsureCacheCapacity(materialCache, null, MaxMaterialCacheSize);
                materialCache[cacheKey] = mat;

                return mat;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 创建材质时出错: {ex}");
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
            }

            lock (materialCacheLock)
            {
                // 材质由 MaterialPool 管理，不需要手动销毁
                materialCache.Clear();
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

            lock (materialCacheLock)
            {
                // 同时清除相关材质缓存
                var keysToRemove = new List<string>();
                foreach (var key in materialCache.Keys)
                {
                    if (key.StartsWith(Path.GetFileNameWithoutExtension(fullPath)))
                    {
                        keysToRemove.Add(key);
                    }
                }
                foreach (var key in keysToRemove)
                {
                    materialCache.Remove(key);
                }
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
                    UnityEngine.Object.Destroy(tex);
                }
                textureCache.Remove(fullPath);
            }
            
            fileLastWriteTimes.Remove(fullPath);
            cacheAccessTimes.Remove(fullPath);
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

        /// <summary>
        /// 确保缓存不超过最大容量（LRU淘汰策略）
        /// </summary>
        private static void EnsureCacheCapacity<T>(Dictionary<string, T> cache, Dictionary<string, DateTime>? accessTimes, int maxSize)
        {
            if (cache.Count < maxSize) return;

            // 需要淘汰的数量（淘汰10%）
            int toRemove = Math.Max(1, maxSize / 10);

            if (accessTimes != null && accessTimes.Count > 0)
            {
                // 使用LRU策略：找到最久未访问的项
                var oldestEntries = accessTimes
                    .Where(kv => cache.ContainsKey(kv.Key))
                    .OrderBy(kv => kv.Value)
                    .Take(toRemove)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in oldestEntries)
                {
                    if (cache.TryGetValue(key, out var item))
                    {
                        // 如果是纹理，需要销毁
                        if (item is Texture2D tex)
                        {
                            UnityEngine.Object.Destroy(tex);
                        }
                        cache.Remove(key);
                    }
                    accessTimes.Remove(key);
                    fileLastWriteTimes.Remove(key);
                }
            }
            else
            {
                // 无访问时间记录，简单移除最早添加的项
                var keysToRemove = cache.Keys.Take(toRemove).ToList();
                foreach (var key in keysToRemove)
                {
                    cache.Remove(key);
                }
            }
        }

        /// <summary>
        /// 更新缓存访问时间（需在锁内调用）
        /// </summary>
        private static void UpdateAccessTime(string key)
        {
            cacheAccessTimes[key] = DateTime.Now;
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
        /// 清除所有节点注册表数据
        /// </summary>
        public static void ClearNodeRegistry()
        {
            lock (nodeRegistryLock)
            {
                nodeOffsetEastRegistry.Clear();
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
    }
}
