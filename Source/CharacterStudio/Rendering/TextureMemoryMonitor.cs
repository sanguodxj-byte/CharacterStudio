using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 纹理内存预算监控器
    /// </summary>
    public static class TextureMemoryMonitor
    {
        public static long MemoryBudgetBytes { get; set; } = 512L * 1024 * 1024;
        private static readonly Dictionary<string, long> _textureSizeEstimates
            = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static long _cachedTotalBytes;
        private static readonly object _lock = new object();

        public static void RecordTexture(string path, Texture2D tex)
        {
            if (string.IsNullOrEmpty(path) || tex == null) return;
            long size = (long)tex.width * tex.height * 4;
            lock (_lock) { _textureSizeEstimates[path] = size; _cachedTotalBytes += size; }
        }

        public static void RemoveTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            lock (_lock)
            {
                if (_textureSizeEstimates.TryGetValue(path, out long size))
                { _textureSizeEstimates.Remove(path); _cachedTotalBytes -= size; if (_cachedTotalBytes < 0) _cachedTotalBytes = 0; }
            }
        }

        public static long CurrentEstimatedUsage { get { lock (_lock) { return _cachedTotalBytes; } } }
        public static float MemoryPressure { get { long b = MemoryBudgetBytes; return b <= 0 ? 0f : (float)CurrentEstimatedUsage / b; } }
        public static string GetMemoryReport()
        {
            lock (_lock)
            { float mb = _cachedTotalBytes / (1024f * 1024f); float bMb = MemoryBudgetBytes / (1024f * 1024f); float pct = MemoryBudgetBytes > 0 ? (_cachedTotalBytes * 100f / MemoryBudgetBytes) : 0f; return $"[CharacterStudio] 纹理内存: {mb:F1} MB / {bMb:F0} MB ({pct:F1}%), 跟踪 {_textureSizeEstimates.Count} 张"; }
        }
        public static void ClearAll() { lock (_lock) { _textureSizeEstimates.Clear(); _cachedTotalBytes = 0; } }

        /// <summary>
        /// 返回内存统计字符串（供性能报告使用）。
        /// </summary>
        public static string GetStats()
        {
            return GetMemoryReport();
        }
    }
}
