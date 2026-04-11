import re

with open('Source/CharacterStudio/Rendering/RuntimeAssetLoader.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Update cache sizes and add collections
content = content.replace(
    'private const int MaxTextureCacheSize = 100;\n        private const int MaxMaterialCacheSize = 200;',
    '''private const int MaxTextureCacheSize = 512;
        private const int MaxMaterialCacheSize = 1024;

        // P2: 纹理 InstanceID → 缓存路径 反向索引（O(1) 查找替代 O(N) 遍历）
        private static readonly Dictionary<int, string> textureInstanceIdToPath = new Dictionary<int, string>();

        // P3: 已执行过透明边缘修正的纹理路径集合
        private static readonly HashSet<string> edgeBleedingProcessedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // P7: 路径解析结果缓存（避免重复目录扫描）
        private static readonly Dictionary<string, string> resolvedPathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // P1: 文件修改检查节流（避免每帧文件系统 I/O）
        private static DateTime lastFileModCheckTime = DateTime.MinValue;'''
)

# 2. Add ShouldCheckFileModificationAggressively
load_texture_raw = '''
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

        public static Texture2D? LoadTextureRaw(string fullPath, bool useCache = true)'''

content = content.replace('public static Texture2D? LoadTextureRaw(string fullPath, bool useCache = true)', load_texture_raw)

# 3. Update LoadTextureRaw cache check
old_cache_check = '''            // 检查缓存（线程安全）
            if (useCache)
            {
                lock (textureCacheLock)
                {
                    if (textureCache.TryGetValue(resolvedPath, out var cachedTex))
                    {
                        // 检查文件是否被修改（仅在编辑器模式下）
                        if (IsFileModified(resolvedPath))
                        {
                            // 文件已修改，移除缓存并重新加载
                            RemoveFromCacheInternal(resolvedPath);
                        }
                        else if (cachedTex != null)
                        {
                            // 更新访问时间
                            cacheAccessTimes[resolvedPath] = DateTime.Now;
                            return cachedTex;
                        }
                        else
                        {
                            textureCache.Remove(resolvedPath);
                            cacheAccessTimes.Remove(resolvedPath);
                        }
                    }
                }
            }'''

new_cache_check = '''            bool aggressive = ShouldCheckFileModificationAggressively();
            bool checkMod = aggressive;
            if (!checkMod)
            {
                DateTime now = DateTime.Now;
                if ((now - lastFileModCheckTime).TotalSeconds >= 2.0)
                {
                    lastFileModCheckTime = now;
                    checkMod = true;
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
                                cacheAccessTimes[resolvedPath] = DateTime.Now;
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
            }'''

content = content.replace(old_cache_check, new_cache_check)

# 4. Update IsFileModified
old_is_file_modified = '''                if (fileLastWriteTimes.TryGetValue(fullPath, out var lastWriteTime))
                {
                    return currentWriteTime > lastWriteTime;
                }
                return true; // 如果没有记录，视为已修改'''

new_is_file_modified = '''                if (fileLastWriteTimes.TryGetValue(fullPath, out var lastWriteTime))
                {
                    bool modified = currentWriteTime > lastWriteTime;
                    if (modified) fileLastWriteTimes[fullPath] = currentWriteTime;
                    return modified;
                }
                fileLastWriteTimes[fullPath] = currentWriteTime;
                return false;'''

content = content.replace(old_is_file_modified, new_is_file_modified)

# 5. Update ResolveExistingTexturePath
old_resolve = '''        private static string ResolveExistingTexturePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return fullPath;

            if (File.Exists(fullPath))
                return fullPath;'''

new_resolve = '''        private static string ResolveExistingTexturePath(string fullPath)
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
                return fullPath;'''

content = content.replace(old_resolve, new_resolve)

# 6. Update FixTransparentEdgeBleeding in CreateTextureFromBytes
old_create = '''            FixTransparentEdgeBleeding(tex);
            tex.Apply(false, false);

            if (useCache)
            {
                lock (textureCacheLock)
                {
                    fileLastWriteTimes[resolvedPath] = File.GetLastWriteTime(resolvedPath);
                    EnsureCacheCapacity(textureCache, cacheAccessTimes, MaxTextureCacheSize);
                    textureCache[resolvedPath] = tex;
                    cacheAccessTimes[resolvedPath] = DateTime.Now;
                }
            }'''

new_create = '''            if (edgeBleedingProcessedPaths.Add(resolvedPath))
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
                    cacheAccessTimes[resolvedPath] = DateTime.Now;
                    textureInstanceIdToPath[tex.GetInstanceID()] = resolvedPath;
                }
            }'''

content = content.replace(old_create, new_create)

# 7. Update ClearAllCaches
old_clear = '''                textureLoadFailureErrors.Clear();
                textureSemiTransparencyCache.Clear();
            }

            lock (materialCacheLock)'''

new_clear = '''                textureLoadFailureErrors.Clear();
                textureSemiTransparencyCache.Clear();
                textureInstanceIdToPath.Clear();
                edgeBleedingProcessedPaths.Clear();
                resolvedPathCache.Clear();
            }

            lock (materialCacheLock)'''

content = content.replace(old_clear, new_clear)

# 8. Update RemoveFromCacheInternal
old_remove = '''                    textureSemiTransparencyCache.Remove(textureInstanceId);
                    UnityEngine.Object.Destroy(tex);'''

new_remove = '''                    textureSemiTransparencyCache.Remove(textureInstanceId);
                    textureInstanceIdToPath.Remove(textureInstanceId);
                    UnityEngine.Object.Destroy(tex);'''

content = content.replace(old_remove, new_remove)

# 9. Update GetMaterialCacheKey
old_mat_key = '''        private static string GetMaterialCacheKey(Texture2D texture, Shader shader)
        {
            string textureIdentity = texture.name;

            lock (textureCacheLock)
            {
                foreach (var entry in textureCache)
                {
                    if (ReferenceEquals(entry.Value, texture))
                    {
                        textureIdentity = entry.Key;
                        break;
                    }
                }
            }'''

new_mat_key = '''        private static string GetMaterialCacheKey(Texture2D texture, Shader shader)
        {
            int instanceId = texture.GetInstanceID();
            string textureIdentity;

            lock (textureCacheLock)
            {
                if (!textureInstanceIdToPath.TryGetValue(instanceId, out textureIdentity!))
                {
                    textureIdentity = texture.name;
                }
            }'''

content = content.replace(old_mat_key, new_mat_key)

# 10. Update EnsureCacheCapacity (LRU without LINQ)
old_lru = '''                // 使用LRU策略：找到最久未访问的项
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
                            int textureInstanceId = tex.GetInstanceID();
                            RemoveMaterialCacheEntriesForTextureInternal(textureInstanceId);
                            textureSemiTransparencyCache.Remove(textureInstanceId);
                            UnityEngine.Object.Destroy(tex);
                        }

                        cache.Remove(key);
                    }
                    accessTimes.Remove(key);
                    fileLastWriteTimes.Remove(key);
                }'''

new_lru = '''                var keysToEvict = new List<string>(toRemove);
                for (int i = 0; i < toRemove; i++)
                {
                    string? oldestKey = null;
                    DateTime oldestTime = DateTime.MaxValue;
                    foreach (var kv in accessTimes)
                    {
                        if (kv.Value < oldestTime && cache.ContainsKey(kv.Key) && !keysToEvict.Contains(kv.Key))
                        {
                            oldestTime = kv.Value;
                            oldestKey = kv.Key;
                        }
                    }
                    if (oldestKey != null)
                    {
                        keysToEvict.Add(oldestKey);
                    }
                }

                foreach (var key in keysToEvict)
                {
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
                }'''

content = content.replace(old_lru, new_lru)

with open('Source/CharacterStudio/Rendering/RuntimeAssetLoader.cs', 'w', encoding='utf-8') as f:
    f.write(content)
