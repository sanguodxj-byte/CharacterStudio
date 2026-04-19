using Verse;
using Verse.AI;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 自定义飞行寻路网格 — 覆盖代价计算，使所有地形对飞行角色均可通行且代价均匀。
    /// 替代方案：RimWorld 原版 Flying PathGrid 仍对缺少 forcePassableByFlyingPawns 的
    /// 不可通行地形返回 10000，导致 A* 视为不可通行。
    /// </summary>
    public class CS_FlightPathGrid : PathGrid
    {
        public const int UniformFlightCost = 5;

        public CS_FlightPathGrid(Map map, PathGridDef def)
            : base(map, def)
        {
        }

        /// <summary>
        /// 飞行角色忽略所有地面障碍（墙壁、水深、建筑），返回固定低代价。
        /// </summary>
        public override int CalculatedCostAt(
            IntVec3 c,
            bool perceivedStatic,
            IntVec3 prevCell,
            int? baseCostOverride = null)
        {
            if (!c.InBounds(this.map))
                return 10000;

            return UniformFlightCost;
        }
    }
}
