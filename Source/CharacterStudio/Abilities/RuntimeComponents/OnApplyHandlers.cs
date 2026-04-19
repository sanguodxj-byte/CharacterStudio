using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents
{
    public class SlotOverrideWindowHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.SlotOverrideWindow;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            int comboWindow = config.comboWindowTicks > 0 ? config.comboWindowTicks : 12;
            abilityComp.SlotOverrideWindowEndTick = nowTick + comboWindow;
            abilityComp.SlotOverrideWindowSlotId = config.comboTargetHotkeySlot.ToString();
            abilityComp.SlotOverrideWindowAbilityDefName = config.comboTargetAbilityDefName ?? string.Empty;
        }
    }

    public class HotkeyOverrideHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.HotkeyOverride;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            if (string.IsNullOrWhiteSpace(config.overrideAbilityDefName)) return;
            int expireTick = nowTick + Mathf.Max(1, config.overrideDurationTicks);
            abilityComp.SetOverrideDefName(config.overrideHotkeySlot, config.overrideAbilityDefName.Trim());
            abilityComp.SetOverrideExpireTick(config.overrideHotkeySlot, expireTick);
        }
    }

    public class FollowupCooldownGateHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.FollowupCooldownGate;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            int cooldownUntil = nowTick + Mathf.Max(1, config.followupCooldownTicks);
            int current = abilityComp.GetCooldownUntilTick(config.followupCooldownHotkeySlot);
            abilityComp.SetCooldownUntilTick(config.followupCooldownHotkeySlot, Mathf.Max(current, cooldownUntil));
        }
    }

    public class RStackDetonationHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.RStackDetonation;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            if (!abilityComp.RStackingEnabled || abilityComp.RSecondStageReady) return;
            int requiredStacks = config.requiredStacks > 0 ? config.requiredStacks : 7;
            abilityComp.RStackCount = Math.Min(requiredStacks, abilityComp.RStackCount + 1);
            if (abilityComp.RStackCount >= requiredStacks)
            {
                abilityComp.RStackingEnabled = false;
                abilityComp.RSecondStageReady = true;
                Messages.Message("CS_Ability_R_Ready".Translate(), MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message("CS_Ability_R_StackGain".Translate(abilityComp.RStackCount, requiredStacks), MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }

    public class PeriodicPulseHandler : IOnApplyHandler, ITickHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.PeriodicPulse;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            int interval = Mathf.Max(1, config.pulseIntervalTicks);
            int duration = Mathf.Max(interval, config.pulseTotalTicks);
            abilityComp.PeriodicPulseEndTick = nowTick + duration;
            abilityComp.PeriodicPulseNextTick = config.pulseStartsImmediately ? nowTick : nowTick + interval;
        }

        public void OnTick(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, int nowTick)
        {
            if (abilityComp.PeriodicPulseNextTick < 0 || abilityComp.PeriodicPulseEndTick < nowTick) return;
            if (caster.Map == null) return;

            int interval = Mathf.Max(1, config.pulseIntervalTicks);
            while (abilityComp.PeriodicPulseNextTick >= 0 && nowTick >= abilityComp.PeriodicPulseNextTick && abilityComp.PeriodicPulseNextTick <= abilityComp.PeriodicPulseEndTick)
            {
                ExecutePulse(source, caster);
                source.TriggerVisualEffects(AbilityVisualEffectTrigger.OnDurationTick, new LocalTargetInfo(caster.Position));
                abilityComp.PeriodicPulseNextTick += interval;
            }

            if (abilityComp.PeriodicPulseNextTick > abilityComp.PeriodicPulseEndTick)
            {
                source.TriggerVisualEffects(AbilityVisualEffectTrigger.OnExpire, new LocalTargetInfo(caster.Position));
                abilityComp.PeriodicPulseNextTick = -1;
                abilityComp.PeriodicPulseEndTick = -1;
            }
        }

        private void ExecutePulse(CompAbilityEffect_Modular source, Pawn caster)
        {
            if (caster.Map == null || source.Props.effects == null) return;

            bool hasSelfAffectingPulseEffect = source.Props.effects.Any(effect => CompAbilityEffect_Modular.IsPulseSelfEffect(caster, effect));
            if (hasSelfAffectingPulseEffect)
            {
                source.ApplyConfiguredEffectsToTarget(
                    new LocalTargetInfo(caster), caster,
                    effectFilter: effect => CompAbilityEffect_Modular.IsPulseSelfEffect(caster, effect));
            }

            LocalTargetInfo pulseTarget = new LocalTargetInfo(caster.Position);
            foreach (Thing thing in caster.Position.GetThingList(caster.Map))
            {
                if (thing is Pawn pawn && pawn != caster && pawn.Faction != caster.Faction)
                {
                    pulseTarget = new LocalTargetInfo(pawn);
                    break;
                }
            }

            source.ApplyConfiguredEffectsToTarget(pulseTarget, caster,
                effectFilter: effect => CompAbilityEffect_Modular.ShouldApplyPulseEffectToPrimaryTarget(effect));
        }
    }

    public class ShieldAbsorbHandler : IOnApplyHandler, ITickHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.ShieldAbsorb;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            abilityComp.ShieldRemainingDamage = Mathf.Max(0f, config.shieldMaxDamage);
            abilityComp.ShieldExpireTick = nowTick + Mathf.Max(1, Mathf.RoundToInt(config.shieldDurationTicks));
            abilityComp.ShieldStoredHeal = 0f;
            abilityComp.ShieldStoredBonusDamage = 0f;
        }

        public void OnTick(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, int nowTick)
        {
            if (abilityComp.ShieldExpireTick < 0 || nowTick <= abilityComp.ShieldExpireTick) return;

            if (abilityComp.ShieldStoredHeal > 0f)
                CompAbilityEffect_Modular.ApplyShieldHeal(caster, abilityComp.ShieldStoredHeal);

            source.TriggerShieldExpiryBurst(caster, abilityComp, config);

            abilityComp.ShieldRemainingDamage = 0f;
            abilityComp.ShieldExpireTick = -1;
            abilityComp.ShieldStoredHeal = 0f;
            abilityComp.ShieldStoredBonusDamage = 0f;
            source.TriggerVisualEffects(AbilityVisualEffectTrigger.OnExpire, new LocalTargetInfo(caster.Position));
        }
    }

    public class AttachedShieldVisualHandler : IOnApplyHandler, ITickHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.AttachedShieldVisual;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            Pawn? pawn = abilityComp.Pawn;
            if (pawn?.Map == null) return;

            ThingDef? visualDef = DefDatabase<ThingDef>.GetNamedSilentFail("CS_AttachedShieldVisual");
            if (visualDef == null)
            {
                Log.Warning("[CharacterStudio] AttachedShieldVisual missing thingDef: CS_AttachedShieldVisual");
                return;
            }

            Thing visualThing = ThingMaker.MakeThing(visualDef);
            int duration = Mathf.Max(1, Mathf.RoundToInt(config.shieldDurationTicks));
            abilityComp.AttachedShieldVisualExpireTick = nowTick + duration;
            abilityComp.AttachedShieldVisualScale = Mathf.Max(0.1f, config.shieldVisualScale);
            abilityComp.AttachedShieldVisualHeightOffset = config.shieldVisualHeightOffset;
            abilityComp.AttachedShieldVisualThingId = visualThing.ThingID;
            abilityComp.attachedShieldVisualCached = visualThing;

            // Notify skin to refresh rendering
            CompPawnSkin? skinComp = pawn.GetComp<CompPawnSkin>();
            if (skinComp != null)
                skinComp.RequestRenderRefresh();
        }

        public void OnTick(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, int nowTick)
        {
            if (abilityComp.AttachedShieldVisualExpireTick < 0) return;

            Thing? visualThing = abilityComp.attachedShieldVisualCached;
            if (visualThing == null && !string.IsNullOrWhiteSpace(abilityComp.AttachedShieldVisualThingId))
            {
                ThingDef? visualDef = DefDatabase<ThingDef>.GetNamedSilentFail("CS_AttachedShieldVisual");
                if (visualDef != null)
                {
                    visualThing = ThingMaker.MakeThing(visualDef);
                    abilityComp.attachedShieldVisualCached = visualThing;
                }
            }

            if (visualThing != null && visualThing.Position != caster.Position)
                visualThing.Position = caster.Position;

            if (nowTick <= abilityComp.AttachedShieldVisualExpireTick) return;

            abilityComp.AttachedShieldVisualExpireTick = -1;
            abilityComp.AttachedShieldVisualThingId = string.Empty;
            abilityComp.attachedShieldVisualCached = null;
        }
    }

    public class ProjectileInterceptorShieldHandler : IOnApplyHandler, ITickHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.ProjectileInterceptorShield;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            if (caster.Map == null || string.IsNullOrWhiteSpace(config.shieldInterceptorThingDefName)) return;

            ThingDef? shieldDef = DefDatabase<ThingDef>.GetNamedSilentFail(config.shieldInterceptorThingDefName);
            if (shieldDef == null)
            {
                Log.Warning($"[CharacterStudio] ProjectileInterceptorShield missing thingDef: {config.shieldInterceptorThingDefName}");
                return;
            }

            Thing thing = ThingMaker.MakeThing(shieldDef);
            if (thing == null) return;

            if (thing is ProjectileInterceptorThing interceptorThing)
                interceptorThing.trackedPawn = caster;

            GenSpawn.Spawn(thing, caster.Position, caster.Map, WipeMode.Vanish);
            if (thing.TryGetComp<CompProjectileInterceptor>() is CompProjectileInterceptor interceptor)
                interceptor.Activate();

            abilityComp.ProjectileInterceptorShieldThingId = thing.ThingID;
            abilityComp.ProjectileInterceptorShieldExpireTick = nowTick + Mathf.Max(1, config.shieldInterceptorDurationTicks);
            abilityComp.projectileInterceptorShieldCached = thing;
        }

        public void OnTick(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, int nowTick)
        {
            if (abilityComp.ProjectileInterceptorShieldExpireTick < 0) return;

            Thing? activeThing = CompAbilityEffect_Modular.FindThingByIdCached(caster.MapHeld, abilityComp.ProjectileInterceptorShieldThingId, ref abilityComp.projectileInterceptorShieldCached);

            if (activeThing != null && activeThing.Position != caster.Position)
                activeThing.Position = caster.Position;

            if (nowTick <= abilityComp.ProjectileInterceptorShieldExpireTick) return;

            if (activeThing != null && !activeThing.Destroyed)
                activeThing.Destroy(DestroyMode.Vanish);

            abilityComp.ProjectileInterceptorShieldExpireTick = -1;
            abilityComp.ProjectileInterceptorShieldThingId = string.Empty;
            abilityComp.projectileInterceptorShieldCached = null;
        }
    }

    public class ChainBounceHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.ChainBounce;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
            => source.TriggerChainBounce(config, caster, target);
    }

    public class DashEmpoweredStrikeHandler : IOnApplyHandler, IPostHitHandler, IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.DashEmpoweredStrike;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            if (source.HasMovementRuntimeComponent())
                abilityComp.DashEmpowerExpireTick = nowTick + Mathf.Max(1, config.dashEmpowerDurationTicks);
        }

        public void OnPostHit(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, float appliedDamage, int nowTick)
        {
            if (casterAbility != null && casterAbility.DashEmpowerExpireTick >= nowTick)
            {
                casterAbility.DashEmpowerExpireTick = -1;
            }
        }

        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, bool allowDashConsume, int nowTick)
        {
            if (allowDashConsume && casterAbility != null && casterAbility.DashEmpowerExpireTick >= nowTick)
                return Mathf.Max(0f, config.dashEmpowerBonusDamageScale);
            return 0f;
        }
    }

    public class FlightStateHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.FlightState;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            CompAbilityEffect_Modular.ApplyFlightState(config, source.parent?.def?.defName ?? string.Empty, caster, abilityComp, target, nowTick);
        }
    }

    public class VanillaPawnFlyerHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.VanillaPawnFlyer;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            if (!AbilityVanillaFlightUtility.TryLaunchPawnFlyer(caster, target, config, source.parent?.def?.defName ?? string.Empty, out string failureReason))
            {
                CSLogger.Warn($"Failed to launch vanilla pawn flyer: {failureReason}", "VanillaFlight");
            }
        }
    }

    public class FlightOnlyFollowupHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.FlightOnlyFollowup;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            if (caster.Flying && config.consumeFlightStateOnCast)
            {
                abilityComp.FlightStateExpireTick = nowTick;
                caster.flight?.ForceLand();
            }
        }
    }

    public class FlightLandingBurstHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.FlightLandingBurst;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
            => abilityComp.VanillaFlightPendingLandingBurst = true;
    }

    public class DashHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.Dash;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime abilityComp, LocalTargetInfo target, int nowTick)
        {
            int distance = config.dashDistance > 0 ? config.dashDistance : 6;
            int stepDuration = config.dashStepDurationTicks > 0 ? config.dashStepDurationTicks : 3;
            int totalDurationTicks = distance * stepDuration;

            // Resolve dash direction: precise angle toward target
            IntVec3 origin = caster.Position;
            IntVec3 targetCell = target.IsValid ? target.Cell : (origin + caster.Rotation.FacingCell);
            IntVec3 delta = targetCell - origin;
            Vector2 dir;
            if (delta.x == 0 && delta.z == 0)
            {
                IntVec3 facing = caster.Rotation.FacingCell;
                dir = new Vector2(facing.x, facing.z).normalized;
            }
            else
            {
                dir = new Vector2(delta.x, delta.z).normalized;
            }

            // Begin forced move
            CompCharacterAbilityRuntime? abilityCompDirect = caster.GetComp<CompCharacterAbilityRuntime>();
            if (abilityCompDirect != null)
                abilityCompDirect.BeginForcedMove(dir, distance, totalDurationTicks);

            // Tag-based Equipment Animation linkage
            if (config.triggerEquipmentAnimationOnApply && !string.IsNullOrWhiteSpace(config.equipmentAnimationTriggerKey))
            {
                int animDuration = config.equipmentAnimationDurationTicks > 0
                    ? config.equipmentAnimationDurationTicks
                    : distance * stepDuration;
                abilityComp.TriggerEquipmentAnimationState(config.equipmentAnimationTriggerKey, nowTick, animDuration);
            }
        }
    }
}
