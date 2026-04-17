using System.Collections.Generic;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// Graphic_Runtime 对象池
    /// P-PERF: 通过复用实例减少 Activator.CreateInstance 和 GC 压力
    /// </summary>
    public static class GraphicRuntimePool
    {
        private static readonly Stack<Graphic_Runtime> pool = new Stack<Graphic_Runtime>();
        private const int MaxPoolSize = 256;

        public static Graphic_Runtime Get()
        {
            if (pool.Count > 0)
            {
                var graphic = pool.Pop();
                return graphic;
            }
            return new Graphic_Runtime();
        }

        public static void Return(Graphic_Runtime? graphic)
        {
            if (graphic == null) return;
            
            // 只有成功初始化的才回收，或者未初始化的回收也行，但需 Reset
            if (pool.Count < MaxPoolSize)
            {
                graphic.Reset();
                pool.Push(graphic);
            }
        }

        public static void Clear()
        {
            pool.Clear();
        }
    }
}
