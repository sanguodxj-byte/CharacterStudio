using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 通用 AssetBundle 资源加载管理器。
    /// 提供 AssetBundle 加载、缓存、资源提取和生命周期管理的通用基础设施。
    /// 不包含任何业务逻辑（如 VFX 播放），仅负责 AssetBundle 的 I/O 层。
    /// </summary>
    public static class AssetBundleManager
    {
        // ─────────────────────────────────────────────
        // 缓存
        // ─────────────────────────────────────────────

        /// <summary>已加载的 AssetBundle 缓存（完整路径 → AssetBundle）</summary>
        private static readonly Dictionary<string, AssetBundle> loadedBundles
            = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

        /// <summary>加载失败记录（避免重复警告）</summary>
        private static readonly HashSet<string> loadFailureWarnings
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ─────────────────────────────────────────────
        // 属性
        // ─────────────────────────────────────────────

        /// <summary>当前已加载的 AssetBundle 数量</summary>
        public static int LoadedBundleCount => loadedBundles.Count;

        // ─────────────────────────────────────────────
        // 路径解析
        // ─────────────────────────────────────────────

        /// <summary>
        /// 将相对路径解析为 AssetBundle 文件的完整绝对路径。
        /// 搜索顺序：
        ///   1. 如果已经是绝对路径且文件存在，直接使用
        ///   2. 相对于 CharacterStudio Mod 根目录
        ///   3. 附加 .ab 扩展名后重试
        /// </summary>
        /// <param name="relativePath">相对路径或绝对路径</param>
        /// <returns>解析后的完整路径（可能不存在）</returns>
        public static string ResolveBundlePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            // 绝对路径且文件存在
            if (Path.IsPathRooted(relativePath) && File.Exists(relativePath))
                return relativePath;

            // 相对于 Mod 根目录解析
            string? modRoot = CharacterStudio.CharacterStudioMod.ModContent?.RootDir;
            if (!string.IsNullOrEmpty(modRoot))
            {
                // 直接路径
                string fullPath = Path.Combine(modRoot, relativePath);
                if (File.Exists(fullPath))
                    return fullPath;

                // 附加 .ab 扩展名
                if (!fullPath.EndsWith(".ab", StringComparison.OrdinalIgnoreCase))
                {
                    fullPath += ".ab";
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }

            return relativePath;
        }

        // ─────────────────────────────────────────────
        // Bundle 加载
        // ─────────────────────────────────────────────

        /// <summary>
        /// 加载 AssetBundle（带缓存）。
        /// 如果已经加载过相同路径，直接返回缓存实例。
        /// </summary>
        /// <param name="relativeOrAbsolutePath">相对路径或绝对路径</param>
        /// <returns>加载的 AssetBundle，失败返回 null</returns>
        public static AssetBundle? LoadBundle(string relativeOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
                return null;

            string fullPath = ResolveBundlePath(relativeOrAbsolutePath);

            // 检查缓存
            if (loadedBundles.TryGetValue(fullPath, out AssetBundle? cached))
            {
                if (cached != null)
                    return cached;
                loadedBundles.Remove(fullPath);
            }

            if (!File.Exists(fullPath))
            {
                LogWarningOnce($"BundleNotFound:{fullPath}",
                    $"[CharacterStudio] AssetBundle 文件不存在: {relativeOrAbsolutePath} (解析为: {fullPath})");
                return null;
            }

            try
            {
                AssetBundle bundle = AssetBundle.LoadFromFile(fullPath);
                if (bundle == null)
                {
                    LogWarningOnce($"BundleLoadFailed:{fullPath}",
                        $"[CharacterStudio] AssetBundle 加载失败: {fullPath}");
                    return null;
                }

                loadedBundles[fullPath] = bundle;
                return bundle;
            }
            catch (Exception ex)
            {
                LogWarningOnce($"BundleLoadError:{fullPath}",
                    $"[CharacterStudio] AssetBundle 加载异常: {fullPath} - {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────────
        // 资源加载
        // ─────────────────────────────────────────────

        /// <summary>
        /// 从 AssetBundle 中加载指定类型和名称的资源。
        /// 自动解析路径并使用候选名称匹配。
        /// </summary>
        /// <typeparam name="T">资源类型（如 Shader, Texture2D, GameObject）</typeparam>
        /// <param name="relativeOrAbsolutePath">AssetBundle 相对或绝对路径</param>
        /// <param name="assetName">包内资源名称</param>
        /// <returns>加载的资源，失败返回 null</returns>
        public static T? LoadAsset<T>(string relativeOrAbsolutePath, string assetName) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath) || string.IsNullOrWhiteSpace(assetName))
                return null;

            AssetBundle? bundle = LoadBundle(relativeOrAbsolutePath);
            if (bundle == null)
                return null;

            // 1. 精确匹配：按候选名称直接查找
            foreach (string candidate in BuildAssetLookupCandidates(assetName))
            {
                try
                {
                    T? asset = bundle.LoadAsset<T>(candidate);
                    if (asset != null)
                        return asset;
                }
                catch { }
            }

            // 2. 按路径匹配：用 AssetBundle 内的完整路径查找
            //    LoadAsset 支持按路径查找，如 "Assets/.../Hologram.shader"
            string[]? allAssetNames = null;
            try { allAssetNames = bundle.GetAllAssetNames(); } catch { }

            if (allAssetNames != null)
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(assetName);
                foreach (string path in allAssetNames)
                {
                    // 按文件名匹配（去扩展名）
                    string pathNoExt = Path.GetFileNameWithoutExtension(path);
                    if (string.Equals(pathNoExt, nameNoExt, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(pathNoExt, assetName, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            T? asset = bundle.LoadAsset<T>(path);
                            if (asset != null)
                                return asset;
                        }
                        catch { }
                    }
                }
            }

            // 3. 遍历所有该类型资源：按资源名模糊匹配
            //    对于 Shader，资源名是 Shader "XXX" 里声明的名字，可能和文件名不同
            try
            {
                T[] allAssets = bundle.LoadAllAssets<T>();
                foreach (T asset in allAssets)
                {
                    if (asset != null && asset.name != null
                        && (string.Equals(asset.name, assetName, StringComparison.OrdinalIgnoreCase)
                            || asset.name.IndexOf(assetName, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return asset;
                    }
                }
            }
            catch { }

            LogWarningOnce($"AssetMissing:{relativeOrAbsolutePath}|{assetName}|{typeof(T).Name}",
                $"[CharacterStudio] AssetBundle '{relativeOrAbsolutePath}' 中未找到资源 '{assetName}' ({typeof(T).Name})");
            return null;
        }

        /// <summary>
        /// 从 AssetBundle 中加载纹理资源。
        /// 自动尝试常见图片扩展名。
        /// </summary>
        /// <param name="relativeOrAbsolutePath">AssetBundle 相对或绝对路径</param>
        /// <param name="textureName">纹理资源名称</param>
        /// <returns>加载的纹理，失败返回 null</returns>
        public static Texture2D? LoadTexture(string relativeOrAbsolutePath, string textureName)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath) || string.IsNullOrWhiteSpace(textureName))
                return null;

            AssetBundle? bundle = LoadBundle(relativeOrAbsolutePath);
            if (bundle == null)
                return null;

            string[] candidates = BuildAssetLookupCandidates(textureName,
                ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff");

            foreach (string candidate in candidates)
            {
                try
                {
                    Texture2D? texture = bundle.LoadAsset<Texture2D>(candidate);
                    if (texture != null)
                        return texture;
                }
                catch { }
            }

            LogWarningOnce($"TextureMissing:{relativeOrAbsolutePath}|{textureName}",
                $"[CharacterStudio] AssetBundle '{relativeOrAbsolutePath}' 中未找到贴图资源: {textureName}");
            return null;
        }

        // ─────────────────────────────────────────────
        // 资源枚举
        // ─────────────────────────────────────────────

        /// <summary>
        /// 枚举 AssetBundle 中所有资源的完整路径名。
        /// </summary>
        /// <param name="relativeOrAbsolutePath">AssetBundle 相对或绝对路径</param>
        /// <returns>资源路径数组，失败返回 null</returns>
        public static string[]? EnumerateAssets(string relativeOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
                return null;

            string fullPath = ResolveBundlePath(relativeOrAbsolutePath);
            if (!File.Exists(fullPath))
            {
                LogWarningOnce($"BundleEnumerateMissing:{fullPath}",
                    $"[CharacterStudio] 枚举 AssetBundle 资源失败，文件不存在: {relativeOrAbsolutePath} (解析为: {fullPath})");
                return null;
            }

            AssetBundle? bundle = LoadBundle(fullPath);
            if (bundle == null)
                return null;

            try
            {
                return bundle.GetAllAssetNames();
            }
            catch (Exception ex)
            {
                LogWarningOnce($"BundleEnumerateError:{fullPath}",
                    $"[CharacterStudio] 枚举 AssetBundle 资源异常: {relativeOrAbsolutePath} - {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────────
        // 候选名称构建
        // ─────────────────────────────────────────────

        /// <summary>
        /// 为资源名称构建候选匹配列表。
        /// 包括原名、文件名、去扩展名、以及附加指定扩展名的变体。
        /// </summary>
        /// <param name="assetName">资源名称</param>
        /// <param name="extensions">要尝试的扩展名（可选）</param>
        /// <returns>去重后的候选名称数组</returns>
        public static string[] BuildAssetLookupCandidates(string assetName, params string[] extensions)
        {
            string trimmed = assetName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
                return Array.Empty<string>();

            var candidates = new List<string> { trimmed };

            string fileName = Path.GetFileName(trimmed);
            if (!string.Equals(fileName, trimmed, StringComparison.OrdinalIgnoreCase))
                candidates.Add(fileName);

            string withoutExtension = Path.GetFileNameWithoutExtension(trimmed);
            if (!string.IsNullOrWhiteSpace(withoutExtension)
                && !string.Equals(withoutExtension, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(withoutExtension);
            }

            foreach (string extension in extensions)
            {
                if (!trimmed.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(trimmed + extension);

                if (!string.IsNullOrWhiteSpace(withoutExtension))
                    candidates.Add(withoutExtension + extension);
            }

            return candidates
                .Where(static c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // ─────────────────────────────────────────────
        // 生命周期
        // ─────────────────────────────────────────────

        /// <summary>
        /// 卸载指定 AssetBundle。
        /// </summary>
        /// <param name="relativeOrAbsolutePath">AssetBundle 相对或绝对路径</param>
        public static void UnloadBundle(string relativeOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
                return;

            string fullPath = ResolveBundlePath(relativeOrAbsolutePath);
            if (loadedBundles.TryGetValue(fullPath, out AssetBundle? bundle))
            {
                loadedBundles.Remove(fullPath);
                try { bundle?.Unload(true); } catch { }
            }
        }

        /// <summary>
        /// 卸载所有已加载的 AssetBundle。
        /// </summary>
        public static void UnloadAll()
        {
            foreach (var kvp in loadedBundles)
            {
                try { kvp.Value?.Unload(true); } catch { }
            }
            loadedBundles.Clear();
            loadFailureWarnings.Clear();
        }

        // ─────────────────────────────────────────────
        // 日志
        // ─────────────────────────────────────────────

        private static void LogWarningOnce(string key, string message)
        {
            if (loadFailureWarnings.Add(key))
                Log.Warning(message);
        }
    }
}
