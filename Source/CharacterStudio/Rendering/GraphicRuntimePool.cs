using System.Collections.Concurrent;
using System.Collections.Generic;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// Graphic_Runtime 对象池
    /// P-PERF: 通过复用实例减少 Activator.CreateInstance 和 GC 压力
    /// 线程安全：使用 lock 保护 Stack 操作，防止渲染多线程并发访问导致集合损坏
    /// </summary>
    public static class GraphicRuntimePool
    {
        private static readonly Stack<Graphic_Runtime> pool = new Stack<Graphic_Runtime>();
        private static readonly object poolLock = new object();
        private const int MaxPoolSize = 256;

        public static Graphic_Runtime Get()
        {
            lock (poolLock)
            {
                if (pool.Count > 0)
                {
                    return pool.Pop();
                }
            }
            return new Graphic_Runtime();
        }

        public static void Return(Graphic_Runtime? graphic)
        {
            if (graphic == null) return;
            
            graphic.Reset();
            lock (poolLock)
            {
                if (pool.Count < MaxPoolSize)
                {
                    pool.Push(graphic);
                }
            }
        }

        public static void Clear()
        {
            lock (poolLock)
            {
                pool.Clear();
            }
        }

        /// <summary>
        /// 当前池中可用实例数。
        /// </summary>
        public static int AvailableCount
        {
            get
            {
                lock (poolLock)
                {
                    return pool.Count;
                }
            }
        }
    }
}
