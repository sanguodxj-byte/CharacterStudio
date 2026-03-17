using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 武器渲染覆盖配置
    /// 允许皮肤定义对武器节点的偏移量和缩放进行自定义覆写。
    ///
    /// 渲染原理：
    ///   以 Harmony Postfix 拦截 PawnRenderNodeWorker.OffsetFor / ScaleFor，
    ///   当节点 tagDef 包含 "Weapon" 时叠加本配置中的偏移和缩放。
    ///   不影响其他节点，不修改武器 Def 数据。
    /// </summary>
    public class WeaponRenderConfig
    {
        /// <summary>是否启用武器渲染覆写</summary>
        public bool enabled = false;

        /// <summary>所有朝向通用额外偏移（世界坐标增量）</summary>
        public Vector3 offset = Vector3.zero;

        /// <summary>面朝南（正面）时的额外偏移</summary>
        public Vector3 offsetSouth = Vector3.zero;

        /// <summary>面朝北（背面）时的额外偏移</summary>
        public Vector3 offsetNorth = Vector3.zero;

        /// <summary>面朝东/西（侧面）时的额外偏移</summary>
        public Vector3 offsetEast = Vector3.zero;

        /// <summary>缩放乘数（1.0 = 不改变大小）</summary>
        public Vector2 scale = Vector2.one;

        /// <summary>是否对副手武器（OffHand/LeftHand）应用同样的配置</summary>
        public bool applyToOffHand = true;

        // ─────────────────────────────────────────────
        // 查询 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 根据 Pawn 朝向返回应叠加的偏移量。
        /// 通用 offset 始终叠加；特定朝向偏移在对应朝向额外叠加。
        /// </summary>
        public Vector3 GetOffsetForRotation(Verse.Rot4 rot)
        {
            Vector3 dir = rot.AsInt switch
            {
                0 => offsetNorth,   // North
                1 => offsetEast,    // East
                2 => offsetSouth,   // South
                3 => offsetEast,    // West（复用 East 的偏移，x 轴会被 flipHorizontal 处理）
                _ => Vector3.zero
            };
            return offset + dir;
        }

        // ─────────────────────────────────────────────
        // 克隆
        // ─────────────────────────────────────────────

        public WeaponRenderConfig Clone() => new WeaponRenderConfig
        {
            enabled      = this.enabled,
            offset       = this.offset,
            offsetSouth  = this.offsetSouth,
            offsetNorth  = this.offsetNorth,
            offsetEast   = this.offsetEast,
            scale        = this.scale,
            applyToOffHand = this.applyToOffHand,
        };
    }
}
