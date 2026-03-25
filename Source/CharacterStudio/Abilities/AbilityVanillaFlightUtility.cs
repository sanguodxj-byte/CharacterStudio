using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CharacterStudio.Abilities
{
    public static class AbilityVanillaFlightUtility
    {
        public static bool TryLaunchPawnFlyer(
            Pawn caster,
            LocalTargetInfo target,
            AbilityRuntimeComponentConfig component,
            string sourceAbilityDefName,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (caster == null)
            {
                failureReason = "caster is null";
                return false;
            }

            if (caster.Map == null)
            {
                failureReason = "caster map is null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(component.flyerThingDefName))
            {
                failureReason = "flyerThingDefName is empty";
                return false;
            }

            IntVec3 destination = target.IsValid ? target.Cell : caster.Position;
            if (component.requireValidTargetCell && !target.IsValid)
            {
                failureReason = "target cell invalid";
                return false;
            }

            ThingDef? flyerDef = DefDatabase<ThingDef>.GetNamedSilentFail(component.flyerThingDefName.Trim());
            if (flyerDef == null)
            {
                failureReason = $"flyer def not found: {component.flyerThingDefName}";
                return false;
            }

            if (!typeof(PawnFlyer).IsAssignableFrom(flyerDef.thingClass))
            {
                failureReason = $"flyer def thingClass is not PawnFlyer: {flyerDef.thingClass}";
                return false;
            }

            if (flyerDef.pawnFlyer == null)
            {
                failureReason = $"flyer def missing pawnFlyer properties: {flyerDef.defName}";
                return false;
            }

            try
            {
                PawnFlyer? flyer = PawnFlyer.MakeFlyer(
                    flyerDef,
                    caster,
                    destination,
                    flightEffecterDef: null,
                    landingSound: null,
                    flyWithCarriedThing: false,
                    overrideStartVec: component.launchFromCasterPosition ? caster.TrueCenter() : (Vector3?)null,
                    triggeringAbility: null,
                    target: target) as PawnFlyer;

                if (flyer is not CharacterStudioPawnFlyer_Base csFlyer)
                {
                    failureReason = $"flyer instance is not CharacterStudioPawnFlyer_Base: {flyerDef.thingClass}";
                    return false;
                }

                CompPawnSkin? skinComp = caster.GetComp<CompPawnSkin>();
                if (skinComp?.isInVanillaFlight == true)
                {
                    CSLogger.Warn($"检测到 {caster.LabelShortCap} 在旧飞行状态未清理时再次启动真实飞行，已强制重置状态。", "VanillaFlight");
                    ClearVanillaFlightState(caster);
                }

                csFlyer.InitializeFlight(caster, destination, sourceAbilityDefName, component);
                Map launchMap = caster.MapHeld;
                GenSpawn.Spawn(csFlyer, caster.PositionHeld, launchMap, WipeMode.Vanish);

                if (skinComp != null)
                {
                    int nowTick = Find.TickManager?.TicksGame ?? 0;
                    string followupAbilityDefName = component.flightOnlyAbilityDefName;
                    if (string.IsNullOrWhiteSpace(followupAbilityDefName))
                    {
                        CharacterAbilityLoadout? loadout = AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(caster);
                        SkinAbilityHotkeyConfig? hotkeys = loadout?.hotkeys;
                        if (hotkeys != null && !string.IsNullOrWhiteSpace(hotkeys.eAbilityDefName)
                            && !string.Equals(hotkeys.eAbilityDefName, sourceAbilityDefName, StringComparison.OrdinalIgnoreCase))
                        {
                            followupAbilityDefName = hotkeys.eAbilityDefName;
                        }
                    }

                    int resolvedFlightDurationTicks = Math.Max(1, component.flightDurationTicks);
                    skinComp.isInVanillaFlight = true;
                    skinComp.vanillaFlightStartTick = nowTick;
                    skinComp.vanillaFlightExpireTick = nowTick + resolvedFlightDurationTicks;
                    skinComp.vanillaFlightSourceAbilityDefName = sourceAbilityDefName ?? string.Empty;
                    skinComp.vanillaFlightFollowupAbilityDefName = followupAbilityDefName ?? string.Empty;
                    skinComp.vanillaFlightReservedTargetCell = destination;
                    skinComp.vanillaFlightHasReservedTargetCell = target.IsValid && component.storeTargetForFollowup;
                    skinComp.vanillaFlightFollowupWindowEndTick = component.enableFlightOnlyWindow
                        ? nowTick + Math.Max(1, component.flightOnlyWindowTicks)
                        : -1;
                    skinComp.vanillaFlightPendingLandingBurst = false;
                    skinComp.RequestRenderRefresh();
                    CSLogger.Debug($"启动真实飞行: pawn={caster.LabelShortCap}, flyer={flyerDef.defName}, from={caster.Position}, to={destination}, flightExpireTick={skinComp.vanillaFlightExpireTick}, followupWindowEndTick={skinComp.vanillaFlightFollowupWindowEndTick}", "VanillaFlight");
                }

                return true;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                Log.Error($"[CharacterStudio] TryLaunchPawnFlyer failed: {ex}");
                return false;
            }
        }

        public static bool CanUseFlightFollowup(
            Pawn? pawn,
            ModularAbilityDef? ability,
            out string failureReason,
            out AbilityRuntimeComponentConfig? followupComponent)
        {
            failureReason = string.Empty;
            followupComponent = ability?.runtimeComponents?.FirstOrDefault(component => component != null
                && component.enabled
                && component.type == AbilityRuntimeComponentType.FlightOnlyFollowup);
            if (pawn == null || followupComponent == null)
            {
                return true;
            }

            CompPawnSkin? skinComp = pawn.GetComp<CompPawnSkin>();
            if (skinComp == null)
            {
                failureReason = "missing CompPawnSkin";
                return false;
            }

            if (!skinComp.isInVanillaFlight)
            {
                failureReason = "pawn is not in vanilla flight";
                return false;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (skinComp.vanillaFlightExpireTick >= 0 && nowTick > skinComp.vanillaFlightExpireTick)
            {
                failureReason = "vanilla flight state expired";
                ClearVanillaFlightState(pawn);
                return false;
            }

            if (followupComponent.onlyUseDuringFlightWindow
                && skinComp.vanillaFlightFollowupWindowEndTick >= 0
                && nowTick > skinComp.vanillaFlightFollowupWindowEndTick)
            {
                failureReason = "flight followup window expired";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(skinComp.vanillaFlightFollowupAbilityDefName)
                && !string.Equals(
                    ability?.defName,
                    skinComp.vanillaFlightFollowupAbilityDefName,
                    StringComparison.OrdinalIgnoreCase))
            {
                failureReason = $"followup ability mismatch: expected {skinComp.vanillaFlightFollowupAbilityDefName}";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(followupComponent.requiredFlightSourceAbilityDefName)
                && !string.Equals(
                    skinComp.vanillaFlightSourceAbilityDefName,
                    followupComponent.requiredFlightSourceAbilityDefName,
                    StringComparison.OrdinalIgnoreCase))
            {
                failureReason = $"flight source mismatch: {skinComp.vanillaFlightSourceAbilityDefName}";
                return false;
            }

            if (followupComponent.requireReservedTargetCell)
            {
                if (!skinComp.vanillaFlightHasReservedTargetCell)
                {
                    failureReason = "reserved target cell missing";
                    return false;
                }

                if (pawn.Map == null || !skinComp.vanillaFlightReservedTargetCell.InBounds(pawn.Map))
                {
                    failureReason = "reserved target cell invalid";
                    return false;
                }
            }

            return true;
        }

        public static LocalTargetInfo ResolveFollowupTarget(Pawn? pawn, ModularAbilityDef? ability, LocalTargetInfo fallbackTarget)
        {
            if (!CanUseFlightFollowup(pawn, ability, out _, out AbilityRuntimeComponentConfig? followupComponent)
                || followupComponent == null
                || !followupComponent.requireReservedTargetCell)
            {
                return fallbackTarget;
            }

            CompPawnSkin? skinComp = pawn?.GetComp<CompPawnSkin>();
            if (skinComp == null || !skinComp.vanillaFlightHasReservedTargetCell)
            {
                return fallbackTarget;
            }

            return new LocalTargetInfo(skinComp.vanillaFlightReservedTargetCell);
        }

        public static bool TryNotifyFlightFollowupFailure(Pawn? pawn, ModularAbilityDef? ability)
        {
            if (CanUseFlightFollowup(pawn, ability, out string failureReason, out _))
            {
                return false;
            }

            string abilityLabel = ability?.label ?? ability?.defName ?? "Ability";
            Messages.Message(
                "[CharacterStudio] " + abilityLabel + " unavailable: " + failureReason,
                MessageTypeDefOf.RejectInput,
                false);
            return true;
        }

        public static AbilityRuntimeComponentConfig? ResolveLandingBurstComponent(Pawn? pawn, string? sourceAbilityDefName)
        {
            if (pawn == null)
            {
                return null;
            }

            string resolvedAbilityDefName = sourceAbilityDefName?.Trim() ?? string.Empty;
            if (resolvedAbilityDefName.Length == 0)
            {
                return null;
            }
            ModularAbilityDef? ability = AbilityLoadoutRuntimeUtility.ResolveAbilityByDefName(pawn, resolvedAbilityDefName);
            return ability?.runtimeComponents?.FirstOrDefault(component => component != null
                && component.enabled
                && component.type == AbilityRuntimeComponentType.FlightLandingBurst);
        }

        public static void TryApplyLandingBurst(Pawn? pawn, IntVec3 landingCell, string? sourceAbilityDefName)
        {
            if (pawn?.Map == null)
            {
                return;
            }

            CompPawnSkin? skinComp = pawn.GetComp<CompPawnSkin>();
            if (skinComp == null || !skinComp.vanillaFlightPendingLandingBurst)
            {
                return;
            }

            IntVec3 resolvedLandingCell = landingCell;
            if (!resolvedLandingCell.IsValid || !resolvedLandingCell.InBounds(pawn.Map))
            {
                resolvedLandingCell = pawn.Position;
                CSLogger.Warn($"真实飞行落地格无效，已回退到 pawn 当前位置: {resolvedLandingCell}。", "VanillaFlight");
            }

            AbilityRuntimeComponentConfig? component = ResolveLandingBurstComponent(pawn, sourceAbilityDefName);
            if (component == null)
            {
                skinComp.vanillaFlightPendingLandingBurst = false;
                return;
            }

            try
            {
                ApplyLandingBurst(pawn, resolvedLandingCell, component);
            }
            catch (Exception ex)
            {
                CSLogger.Error("真实飞行落地爆发执行失败。", ex, "VanillaFlight");
            }
            finally
            {
                skinComp.vanillaFlightPendingLandingBurst = false;
            }
        }

        private static void ApplyLandingBurst(Pawn pawn, IntVec3 landingCell, AbilityRuntimeComponentConfig component)
        {
            Map map = pawn.Map!;
            DamageDef damageDef = component.landingBurstDamageDef ?? DamageDefOf.Bomb;
            float radius = Mathf.Max(0.1f, component.landingBurstRadius);
            float damageAmount = Mathf.Max(0.01f, component.landingBurstDamage);

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(landingCell, radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                if (component.affectCells)
                {
                    FleckMaker.ThrowDustPuff(cell, map, Mathf.Clamp(radius * 0.3f, 0.6f, 2.5f));
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (!CanAffectLandingBurstThing(pawn, thing, component))
                    {
                        continue;
                    }

                    thing.TakeDamage(new DamageInfo(damageDef, damageAmount, 0f, -1f, pawn));
                    if (component.knockbackTargets && thing is Pawn targetPawn && !targetPawn.Dead)
                    {
                        TryApplyLandingKnockback(targetPawn, pawn, component.knockbackDistance);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(component.landingEffecterDefName))
            {
                EffecterDef? effecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail(component.landingEffecterDefName);
                Effecter? effecter = effecterDef?.Spawn();
                if (effecter != null)
                {
                    TargetInfo targetInfo = new TargetInfo(landingCell, map);
                    effecter.Trigger(targetInfo, targetInfo);
                    effecter.Cleanup();
                }
            }

            if (!string.IsNullOrWhiteSpace(component.landingSoundDefName))
            {
                SoundDef? soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(component.landingSoundDefName);
                soundDef?.PlayOneShot(new TargetInfo(landingCell, map));
            }
        }

        private static bool CanAffectLandingBurstThing(Pawn pawn, Thing thing, AbilityRuntimeComponentConfig component)
        {
            if (thing == pawn || thing.Destroyed)
            {
                return false;
            }

            if (thing is Pawn targetPawn)
            {
                if (targetPawn.Dead || targetPawn.Downed || targetPawn == pawn)
                {
                    return false;
                }

                Faction? sourceFaction = pawn.Faction;
                Faction? targetFaction = targetPawn.Faction;
                if (sourceFaction != null && targetFaction != null && !sourceFaction.HostileTo(targetFaction))
                {
                    return false;
                }

                if (sourceFaction == null && targetFaction == null)
                {
                    return targetPawn.HostileTo(pawn);
                }

                return true;
            }

            if (!component.affectBuildings || thing is not Building building)
            {
                return false;
            }

            Faction? pawnFaction = pawn.Faction;
            Faction? buildingFaction = building.Faction;
            if (pawnFaction != null && buildingFaction != null)
            {
                return pawnFaction.HostileTo(buildingFaction);
            }

            return building.HostileTo(pawn);
        }

        private static void TryApplyLandingKnockback(Pawn targetPawn, Pawn sourcePawn, float knockbackDistance)
        {
            Map? map = sourcePawn.Map;
            if (map == null)
            {
                return;
            }

            int steps = Mathf.Max(1, Mathf.RoundToInt(knockbackDistance));
            IntVec3 direction = targetPawn.Position - sourcePawn.Position;
            if (direction == IntVec3.Zero)
            {
                direction = targetPawn.Position - sourcePawn.Position;
            }

            direction = new IntVec3(Math.Sign(direction.x), 0, Math.Sign(direction.z));
            if (direction == IntVec3.Zero)
            {
                direction = sourcePawn.Rotation.FacingCell;
            }

            IntVec3 bestCell = targetPawn.Position;
            for (int i = 0; i < steps; i++)
            {
                IntVec3 next = bestCell + direction;
                if (!next.InBounds(map) || !next.Standable(map))
                {
                    break;
                }

                bestCell = next;
            }

            if (bestCell != targetPawn.Position)
            {
                targetPawn.Position = bestCell;
            }
        }

        public static void ClearVanillaFlightState(Pawn? pawn)
        {
            CompPawnSkin? skinComp = pawn?.GetComp<CompPawnSkin>();
            if (skinComp == null)
            {
                return;
            }

            skinComp.isInVanillaFlight = false;
            skinComp.vanillaFlightStartTick = -1;
            skinComp.vanillaFlightExpireTick = -1;
            skinComp.vanillaFlightSourceAbilityDefName = string.Empty;
            skinComp.vanillaFlightFollowupAbilityDefName = string.Empty;
            skinComp.vanillaFlightReservedTargetCell = IntVec3.Invalid;
            skinComp.vanillaFlightHasReservedTargetCell = false;
            skinComp.vanillaFlightFollowupWindowEndTick = -1;
            skinComp.vanillaFlightPendingLandingBurst = false;
            skinComp.RequestRenderRefresh();
        }
    }

    public class CharacterStudioPawnFlyer_Base : PawnFlyer
    {
        protected string sourceAbilityDefName = string.Empty;
        protected AbilityRuntimeComponentConfig? sourceComponent;
        protected IntVec3 destinationCell = IntVec3.Invalid;
        protected IntVec3 launchCell = IntVec3.Invalid;
        protected int launchTick = -1;
        protected int cachedFlightDurationTicks = 1;

        public virtual void InitializeFlight(Pawn pawn, IntVec3 destination, string abilityDefName, AbilityRuntimeComponentConfig component)
        {
            this.launchCell = pawn.Position;
            this.destinationCell = destination;
            this.sourceAbilityDefName = abilityDefName ?? string.Empty;
            this.sourceComponent = component;
            this.launchTick = Find.TickManager?.TicksGame ?? 0;
            this.cachedFlightDurationTicks = ResolveFlightDurationTicks();

            if (component.hideCasterDuringTakeoff)
            {
                pawn.GetComp<CompPawnSkin>()?.RequestRenderRefresh();
            }

            if (component.flyerWarmupTicks > 0)
            {
                this.launchTick += component.flyerWarmupTicks;
            }
        }

        protected float GetFlightProgress01()
        {
            int nowTick = Find.TickManager?.TicksGame ?? this.launchTick;
            int elapsedTicks = Math.Max(0, nowTick - this.launchTick);
            return Mathf.Clamp01(elapsedTicks / (float)Math.Max(1, this.cachedFlightDurationTicks));
        }

        protected int ResolveFlightDurationTicks()
        {
            if (this.sourceComponent != null && this.sourceComponent.flightDurationTicks > 0)
            {
                return Math.Max(1, this.sourceComponent.flightDurationTicks);
            }

            float durationSeconds = def?.pawnFlyer?.flightDurationMin ?? 0.35f;
            return Math.Max(1, Mathf.RoundToInt(durationSeconds * 60f));
        }

        protected override void RespawnPawn()
        {
            Pawn? pawn = this.FlyingPawn;
            string abilityDefName = this.sourceAbilityDefName;
            IntVec3 landingCell = this.destinationCell.IsValid ? this.destinationCell : this.Position;

            try
            {
                base.RespawnPawn();
                AbilityVanillaFlightUtility.TryApplyLandingBurst(pawn, landingCell, abilityDefName);
            }
            finally
            {
                AbilityVanillaFlightUtility.ClearVanillaFlightState(pawn);
            }
        }
    }

    public class CharacterStudioPawnFlyer_Default : CharacterStudioPawnFlyer_Base
    {
    }

    public class CharacterStudioPawnFlyer_OffscreenDive : CharacterStudioPawnFlyer_Base
    {
        private const float OffscreenOvershootCells = 8f;
        private const float DiveHeightCells = 6f;

        public override Vector3 DrawPos
        {
            get
            {
                Vector3 basePos = base.DrawPos;
                Map? map = this.Map;
                if (map == null)
                {
                    return basePos;
                }

                float progress = GetFlightProgress01();
                Vector3 start = ResolveOffscreenStartPos(map);
                Vector3 end = this.destinationCell.IsValid ? this.destinationCell.ToVector3Shifted() : basePos;
                Vector3 pos = Vector3.Lerp(start, end, progress);
                pos.y = Altitudes.AltitudeFor(AltitudeLayer.Pawn);
                pos.z += Mathf.Sin(progress * Mathf.PI) * DiveHeightCells;
                return pos;
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (this.Map == null)
            {
                return;
            }

            if ((Find.TickManager?.TicksGame ?? 0) - this.launchTick <= 1)
            {
                FleckMaker.ThrowDustPuff(this.launchCell.IsValid ? this.launchCell : this.Position, this.Map, 1.6f);
            }
        }

        private Vector3 ResolveOffscreenStartPos(Map map)
        {
            Vector3 destination = this.destinationCell.IsValid ? this.destinationCell.ToVector3Shifted() : base.DrawPos;
            Vector3 origin = this.launchCell.IsValid ? this.launchCell.ToVector3Shifted() : destination;
            Vector3 outward = destination - origin;
            if (outward.sqrMagnitude < 0.001f)
            {
                IntVec3 centerCell = map.Center;
                outward = destination - centerCell.ToVector3Shifted();
            }

            outward.y = 0f;
            if (outward.sqrMagnitude < 0.001f)
            {
                outward = new Vector3(0f, 0f, 1f);
            }

            outward.Normalize();
            float horizontalExtent = Mathf.Max(map.Size.x, map.Size.z) * 0.5f + OffscreenOvershootCells;
            return destination + outward * horizontalExtent;
        }
    }
}
