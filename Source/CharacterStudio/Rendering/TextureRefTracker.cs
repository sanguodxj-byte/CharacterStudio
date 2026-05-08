using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 纹理引用来源标识
    /// </summary>
    public enum RefSource
    {
        CustomLayer,
        FaceComponent,
        EyeDirection,
        RenderNode,
        Prewarm
    }

    /// <summary>
    /// 统一纹理引用计数跟踪器
    /// </summary>
    public static class TextureRefTracker
    {
        private static readonly Dictionary<int, Dictionary<RefSource, int>> _refs
            = new Dictionary<int, Dictionary<RefSource, int>>();
        private static readonly object _lock = new object();

        public static void AddRef(Texture2D tex, RefSource source)
        {
            if (tex == null) return;
            int id = tex.GetInstanceID();
            lock (_lock)
            {
                if (!_refs.TryGetValue(id, out var sourceCounts))
                {
                    sourceCounts = new Dictionary<RefSource, int>();
                    _refs[id] = sourceCounts;
                }
                if (sourceCounts.ContainsKey(source))
                    sourceCounts[source]++;
                else
                    sourceCounts[source] = 1;
            }
        }

        public static void ReleaseRef(Texture2D tex, RefSource source)
        {
            if (tex == null) return;
            ReleaseRefById(tex.GetInstanceID(), source);
        }

        public static void ReleaseRefById(int textureInstanceId, RefSource source)
        {
            if (textureInstanceId == 0) return;
            lock (_lock)
            {
                if (!_refs.TryGetValue(textureInstanceId, out var sourceCounts))
                    return;
                if (!sourceCounts.TryGetValue(source, out int count))
                    return;
                if (count <= 1)
                    sourceCounts.Remove(source);
                else
                    sourceCounts[source] = count - 1;
                if (sourceCounts.Count == 0)
                    _refs.Remove(textureInstanceId);
            }
        }

        public static void ReleaseAllFromSource(RefSource source)
        {
            lock (_lock)
            {
                List<int>? emptyIds = null;
                foreach (var kv in _refs)
                {
                    if (kv.Value.Remove(source) && kv.Value.Count == 0)
                    {
                        if (emptyIds == null) emptyIds = new List<int>();
                        emptyIds.Add(kv.Key);
                    }
                }
                if (emptyIds != null)
                {
                    for (int i = 0; i < emptyIds.Count; i++)
                        _refs.Remove(emptyIds[i]);
                }
            }
        }

        public static bool IsReferenced(Texture2D tex)
        {
            if (tex == null) return false;
            int id = tex.GetInstanceID();
            lock (_lock) { return _refs.ContainsKey(id); }
        }

        public static bool SafeDestroy(Texture2D tex)
        {
            if (tex == null) return false;
            int id = tex.GetInstanceID();
            lock (_lock) { if (_refs.ContainsKey(id)) return false; }
            Object.Destroy(tex);
            return true;
        }

        public static int GetRefCount(Texture2D tex)
        {
            if (tex == null) return 0;
            int id = tex.GetInstanceID();
            lock (_lock)
            {
                if (!_refs.TryGetValue(id, out var sourceCounts)) return 0;
                int total = 0;
                foreach (var kv in sourceCounts) total += kv.Value;
                return total;
            }
        }

        public static void ClearAll() { lock (_lock) { _refs.Clear(); } }
        public static int TotalTrackedCount { get { lock (_lock) { return _refs.Count; } } }
    }
}
