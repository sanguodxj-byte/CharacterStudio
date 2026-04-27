using CharacterStudio.Abilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents
{
    /// <summary>
    /// 贝塞尔曲线拦截墙处理器。
    /// OnApply：根据两个目标点 + 配置计算贝塞尔曲线参数，注册到全局 Manager。
    /// OnTick：检查墙壁是否过期或吸收量耗尽，过期则注销。
    /// </summary>
    public class BezierCurveWallHandler : IOnApplyHandler, ITickHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.BezierCurveWall;

        public void OnApply(
            CompAbilityEffect_Modular source,
            AbilityRuntimeComponentConfig config,
            Pawn caster,
            CompCharacterAbilityRuntime abilityComp,
            LocalTargetInfo target,
            LocalTargetInfo dest,
            int nowTick)
        {
            if (caster.Map == null) return;

            // 自定义 Gizmo 只提供单一 target，dest 通常无效。
            // 策略：以 caster 为起点、target 为终点生成墙体；
            //        如果 dest 有效则使用 target→dest。
            IntVec3 startCell;
            IntVec3 endCell;
            if (dest.IsValid && dest.Cell != target.Cell)
            {
                startCell = target.IsValid ? target.Cell : caster.Position;
                endCell = dest.Cell;
            }
            else
            {
                startCell = caster.Position;
                endCell = target.IsValid ? target.Cell : (caster.Position + caster.Rotation.FacingCell * 5);
            }

            // 退化保护：如果起点终点重合，沿朝向方向偏移 5 格
            if (startCell == endCell)
            {
                endCell = startCell + caster.Rotation.FacingCell * 5;
            }

            // 转换为世界坐标（格子中心）
            Vector2 startPos = new Vector2(startCell.x + 0.5f, startCell.z + 0.5f);
            Vector2 endPos = new Vector2(endCell.x + 0.5f, endCell.z + 0.5f);

            // 计算控制点：两端中点 + 垂直法线方向偏移
            Vector2 midPoint = (startPos + endPos) * 0.5f;
            Vector2 direction = (endPos - startPos).normalized;
            // 法线方向（垂直于连线）
            Vector2 normal = new Vector2(-direction.y, direction.x);
            // 应用弯曲方向配置
            float curveDir = config.bezierWallCurveDirection >= 0 ? 1f : -1f;
            Vector2 controlPoint = midPoint + normal * config.bezierWallControlPointHeight * curveDir;

            // 设置状态
            abilityComp.BezierWallStartX = startPos.x;
            abilityComp.BezierWallStartZ = startPos.y;
            abilityComp.BezierWallEndX = endPos.x;
            abilityComp.BezierWallEndZ = endPos.y;
            abilityComp.BezierWallControlX = controlPoint.x;
            abilityComp.BezierWallControlZ = controlPoint.y;
            abilityComp.BezierWallThickness = config.bezierWallThickness;
            abilityComp.BezierWallSegmentCount = config.bezierWallSegmentCount;
            abilityComp.BezierWallBlockFriendly = config.bezierWallBlockFriendly;
            abilityComp.BezierWallAbsorbRemaining = config.bezierWallAbsorbMax;
            abilityComp.BezierWallExpireTick = nowTick + Mathf.Max(1, config.bezierWallDurationTicks);

            // 创建墙壁实例并注册
            var wallInstance = new BezierWallInstance
            {
                Start = startPos,
                End = endPos,
                ControlPoint = controlPoint,
                Thickness = config.bezierWallThickness,
                SegmentCount = config.bezierWallSegmentCount,
                ExpireTick = abilityComp.BezierWallExpireTick,
                BlockFriendly = config.bezierWallBlockFriendly,
                OwnerFaction = caster.Faction,
                Map = caster.Map,
                OwnerComp = abilityComp,
                CustomTexturePath = config.bezierWallCustomTexture,
                ReflectsProjectiles = config.bezierWallReflectsProjectiles
            };
            wallInstance.BuildSegmentPoints();

            BezierCurveWallManager.Register(wallInstance);
        }

        public void OnTick(
            CompAbilityEffect_Modular source,
            AbilityRuntimeComponentConfig config,
            Pawn caster,
            CompCharacterAbilityRuntime abilityComp,
            int nowTick)
        {
            if (abilityComp.BezierWallExpireTick < 0) return;

            // 检查是否过期或吸收量耗尽
            if (nowTick > abilityComp.BezierWallExpireTick || abilityComp.BezierWallAbsorbRemaining <= 0f)
            {
                abilityComp.BezierWallExpireTick = -1;
                abilityComp.BezierWallAbsorbRemaining = 0f;
                // Manager 会在 CleanupExpired 中自动移除
            }
        }
    }
}
