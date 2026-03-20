using UnityEngine;

namespace CharacterStudio.Core
{
    public enum WeaponCarryVisualState
    {
        Undrafted,
        Drafted,
        Casting
    }

    /// <summary>
    /// 武器收纳/拔刀视觉配置。
    /// 用于通用角色编辑器，不硬编码任何具体武器类型，
    /// 仅按 Pawn 状态在“非征召 / 征召 / 施法中”三种贴图之间切换。
    /// </summary>
    public class WeaponCarryVisualConfig
    {
        /// <summary>是否启用状态武器视觉</summary>
        public bool enabled = false;

        /// <summary>挂载锚点（通常为 Body，可通过偏移放到背上/腰上）</summary>
        public string anchorTag = "Body";

        /// <summary>非征召状态贴图</summary>
        public string texUndrafted = string.Empty;

        /// <summary>征召状态贴图</summary>
        public string texDrafted = string.Empty;

        /// <summary>施法中状态贴图</summary>
        public string texCasting = string.Empty;

        /// <summary>通用偏移</summary>
        public Vector3 offset = Vector3.zero;

        /// <summary>北向偏移</summary>
        public Vector3 offsetNorth = Vector3.zero;

        /// <summary>东/西向偏移</summary>
        public Vector3 offsetEast = Vector3.zero;

        /// <summary>缩放</summary>
        public Vector2 scale = Vector2.one;
        public Vector2 scaleNorthMultiplier = Vector2.one;
        public Vector2 scaleEastMultiplier = Vector2.one;

        /// <summary>旋转</summary>
        public float rotation = 0f;
        public float rotationNorthOffset = 0f;
        public float rotationEastOffset = 0f;

        /// <summary>绘制层级</summary>
        public float drawOrder = 80f;

        public string GetTexPath(WeaponCarryVisualState state)
        {
            return state switch
            {
                WeaponCarryVisualState.Casting when !string.IsNullOrWhiteSpace(texCasting) => texCasting,
                WeaponCarryVisualState.Drafted when !string.IsNullOrWhiteSpace(texDrafted) => texDrafted,
                _ => texUndrafted
            };
        }

        public Vector3 GetOffsetForRotation(Verse.Rot4 rot)
        {
            Vector3 directional = rot.AsInt switch
            {
                0 => offsetNorth,
                1 => offsetEast,
                3 => offsetEast,
                _ => Vector3.zero
            };

            return offset + directional;
        }

        public string GetAnyTexPath()
        {
            if (!string.IsNullOrWhiteSpace(texUndrafted)) return texUndrafted;
            if (!string.IsNullOrWhiteSpace(texDrafted)) return texDrafted;
            return texCasting;
        }

        public WeaponCarryVisualConfig Clone() => new WeaponCarryVisualConfig
        {
            enabled = this.enabled,
            anchorTag = this.anchorTag,
            texUndrafted = this.texUndrafted,
            texDrafted = this.texDrafted,
            texCasting = this.texCasting,
            offset = this.offset,
            offsetNorth = this.offsetNorth,
            offsetEast = this.offsetEast,
            scale = this.scale,
            scaleNorthMultiplier = this.scaleNorthMultiplier,
            scaleEastMultiplier = this.scaleEastMultiplier,
            rotation = this.rotation,
            rotationNorthOffset = this.rotationNorthOffset,
            rotationEastOffset = this.rotationEastOffset,
            drawOrder = this.drawOrder,
        };
    }

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

        /// <summary>通用状态武器视觉（非征召 / 征召 / 施法中）</summary>
        public WeaponCarryVisualConfig carryVisual = new WeaponCarryVisualConfig();

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
            carryVisual   = this.carryVisual?.Clone() ?? new WeaponCarryVisualConfig(),
        };
    }
}
