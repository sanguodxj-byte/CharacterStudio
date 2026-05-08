using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents
{
    /// <summary>
    /// 跨地图飞行运行时组件处理器。
    ///
    /// 视觉：FlightState 浮空上升（现有飞行组件渲染）→ 到期 DeSpawn → 世界地图飞行 → 角色从天而降
    /// 逻辑：FlightState → handoff → TravellingTransporters → ArrivalAction
    /// </summary>
    public class WorldMapFlightHandler : IGlobalOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.WorldMapFlight;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, LocalTargetInfo target, LocalTargetInfo dest, int nowTick)
        {
            if (caster.Map == null)
                return;

            if (!AbilityWorldMapFlightUtility.CanLaunchWorldMapFlight(caster, out string failReason))
            {
                if (!string.IsNullOrWhiteSpace(failReason))
                    Messages.Message(failReason, MessageTypeDefOf.RejectInput, false);
                return;
            }

            StartWorldTargeter(source, config, caster, nowTick);
        }

        private void StartWorldTargeter(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, int nowTick)
        {
            AbilityRuntimeComponentConfig capturedConfig = config;
            Pawn capturedCaster = caster;
            string sourceAbilityDefName = source.parent?.def?.defName ?? string.Empty;
            PlanetTile originTile = caster.Map.Tile;
            int maxDistance = config.worldMapMaxLaunchDistance;

            Find.WorldSelector.ClearSelection();
            CameraJumper.TryJump(CameraJumper.GetWorldTarget(caster));

            Find.WorldTargeter.BeginTargeting(
                (GlobalTargetInfo target) => ValidateAndLaunch(capturedConfig, capturedCaster, target, originTile, sourceAbilityDefName),
                true,
                CompLaunchable.TargeterMouseAttachment,
                onUpdate: new Action(() =>
                {
                    if (maxDistance > 0)
                        GenDraw.DrawWorldRadiusRing(originTile, maxDistance);
                }),
                extraLabelGetter: (Func<GlobalTargetInfo, TaggedString>)(target =>
                {
                    if (!target.IsValid || maxDistance <= 0)
                        return (TaggedString)null;
                    int numTiles = Find.WorldGrid.TraversalDistanceBetween(originTile, target.Tile, true);
                    if (numTiles > maxDistance)
                        return (TaggedString)("TransportPodDestinationBeyondMaximumRange".Translate());
                    return (TaggedString)null;
                })
            );
        }

        private bool ValidateAndLaunch(AbilityRuntimeComponentConfig config, Pawn caster, GlobalTargetInfo target, PlanetTile originTile, string sourceAbilityDefName)
        {
            if (caster == null || caster.Map == null)
                return false;

            if (!target.IsValid)
            {
                Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            PlanetTile tile = target.Tile;

            if (tile == originTile)
            {
                Messages.Message("CS_Ability_Fail_SameTile".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (Find.World.Impassable(tile))
            {
                Messages.Message("CS_Ability_Fail_ImpassableDestination".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            int maxDistance = config.worldMapMaxLaunchDistance;
            if (maxDistance > 0)
            {
                int numTiles = Find.WorldGrid.TraversalDistanceBetween(originTile, tile, true);
                if (numTiles > maxDistance)
                {
                    Messages.Message("TransportPodDestinationBeyondMaximumRange".Translate(), MessageTypeDefOf.RejectInput, false);
                    return false;
                }
            }

            // arrivalAction 在 handoff 时重新 Resolve，此处传 null
            if (AbilityWorldMapFlightUtility.TryLaunchWorldMapFlight(caster, tile, null, config, sourceAbilityDefName, out string failureReason))
            {
                // 相机切回角色所在地图观看起飞动画
                CameraJumper.TryJump(caster);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(failureReason))
                Messages.Message(failureReason, MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }
}
