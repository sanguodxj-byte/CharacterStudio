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
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            int comboWindow = config.comboWindowTicks > 0 ? config.comboWindowTicks : 12;
            skinComp.slotOverrideWindowEndTick = nowTick + comboWindow;
            skinComp.slotOverrideWindowSlotId = config.comboTargetHotkeySlot.ToString();
            skinComp.slotOverrideWindowAbilityDefName = config.comboTargetAbilityDefName ?? string.Empty;
        }
    }

    public class HotkeyOverrideHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.HotkeyOverride;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            if (string.IsNullOrWhiteSpace(config.overrideAbilityDefName)) return;
            int expireTick = nowTick + Mathf.Max(1, config.overrideDurationTicks);
            skinComp.abilityRuntimeState.SetOverrideDefName(config.overrideHotkeySlot, config.overrideAbilityDefName.Trim());
            skinComp.abilityRuntimeState.SetOverrideExpireTick(config.overrideHotkeySlot, expireTick);
        }
    }

    public class FollowupCooldownGateHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.FollowupCooldownGate;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            int cooldownUntil = nowTick + Mathf.Max(1, config.followupCooldownTicks);
            int current = skinComp.abilityRuntimeState.GetCooldownUntilTick(config.followupCooldownHotkeySlot);
            skinComp.abilityRuntimeState.SetCooldownUntilTick(config.followupCooldownHotkeySlot, Mathf.Max(current, cooldownUntil));
        }
    }

    public class RStackDetonationHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.RStackDetonation;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            if (!skinComp.rStackingEnabled || skinComp.rSecondStageReady) return;
            int requiredStacks = config.requiredStacks > 0 ? config.requiredStacks : 7;
            skinComp.rStackCount = Math.Min(requiredStacks, skinComp.rStackCount + 1);
            if (skinComp.rStackCount >= requiredStacks)
            {
                skinComp.rStackingEnabled = false;
                skinComp.rSecondStageReady = true;
                Messages.Message("CS_Ability_R_Ready".Translate(), MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message("CS_Ability_R_StackGain".Translate(skinComp.rStackCount, requiredStacks), MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }

    public class PeriodicPulseHandler : IOnApplyHandler, ITickHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.PeriodicPulse;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            int interval = Mathf.Max(1, config.pulseIntervalTicks);
            int duration = Mathf.Max(interval, config.pulseTotalTicks);
            skinComp.periodicPulseEndTick = nowTick + duration;
            skinComp.periodicPulseNextTick = config.pulseStartsImmediately ? nowTick : nowTick + interval;
        }

        public void OnTick(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            if (skinComp.periodicPulseNextTick < 0 || skinComp.periodicPulseEndTick < nowTick) return;
            if (caster.Map == null) return;

            int interval = Mathf.Max(1, config.pulseIntervalTicks);
            while (skinComp.periodicPulseNextTick >= 0 && nowTick >= skinComp.periodicPulseNextTick && skinComp.periodicPulseNextTick <= skinComp.periodicPulseEndTick)
            {
                ExecutePulse(source, caster);
                source.TriggerVisualEffects(AbilityVisualEffectTrigger.OnDurationTick, new LocalTargetInfo(caster.Position));
                skinComp.periodicPulseNextTick += interval;
            }

            if (skinComp.periodicPulseNextTick > skinComp.periodicPulseEndTick)
            {
                source.TriggerVisualEffects(AbilityVisualEffectTrigger.OnExpire, new LocalTargetInfo(caster.Position));
                skinComp.periodicPulseNextTick = -1;
                skinComp.periodicPulseEndTick = -1;
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

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            skinComp.shieldRemainingDamage = Mathf.Max(0f, config.shieldMaxDamage);
            skinComp.shieldExpireTick = nowTick + Mathf.Max(1, Mathf.RoundToInt(config.shieldDurationTicks));
            skinComp.shieldStoredHeal = 0f;
            skinComp.shieldStoredBonusDamage = 0f;
        }

        public void OnTick(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            if (skinComp.shieldExpireTick < 0 || nowTick <= skinComp.shieldExpireTick) return;

            if (skinComp.shieldStoredHeal > 0f)
                CompAbilityEffect_Modular.ApplyShieldHeal(caster, skinComp.shieldStoredHeal);

            source.TriggerShieldExpiryBurst(caster, skinComp, config);

            skinComp.shieldRemainingDamage = 0f;
            skinComp.shieldExpireTick = -1;
            skinComp.shieldStoredHeal = 0f;
            skinComp.shieldStoredBonusDamage = 0f;
            source.TriggerVisualEffects(AbilityVisualEffectTrigger.OnExpire, new LocalTargetInfo(caster.Position));
        }
    }

    public class AttachedShieldVisualHandler : IOnApplyHandler, ITickHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.AttachedShieldVisual;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            Pawn? pawn = skinComp.Pawn;
            if (pawn?.Map == null) return;

            ThingDef? visualDef = DefDatabase<ThingDef>.GetNamedSilentFail("CS_AttachedShieldVisual");
            if (visualDef == null)
            {
                Log.Warning("[CharacterStudio] AttachedShieldVisual missing thingDef: CS_AttachedShieldVisual");
                return;
            }

            Thing visualThing = ThingMaker.MakeThing(visualDef);
            int duration = Mathf.Max(1, Mathf.RoundToInt(config.shieldDurationTicks));
            skinComp.attachedShieldVisualExpireTick = nowTick + duration;
            skinComp.attachedShieldVisualScale = Mathf.Max(0.1f, config.shieldVisualScale);
            skinComp.attachedShieldVisualHeightOffset = config.shieldVisualHeightOffset;
            skinComp.attachedShieldVisualThingId = visualThing.ThingID;
            skinComp.attachedShieldVisualCached = visualThing;
            skinComp.RequestRenderRefresh();
        }

        public void OnTick(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            if (skinComp.attachedShieldVisualExpireTick < 0) return;

            Thing? visualThing = skinComp.attachedShieldVisualCached;
            if (visualThing == null && !string.IsNullOrWhiteSpace(skinComp.attachedShieldVisualThingId))
            {
                ThingDef? visualDef = DefDatabase<ThingDef>.GetNamedSilentFail("CS_AttachedShieldVisual");
                if (visualDef != null)
                {
                    visualThing = ThingMaker.MakeThing(visualDef);
                    skinComp.attachedShieldVisualCached = visualThing;
                }
            }

            if (visualThing != null && visualThing.Position != caster.Position)
                visualThing.Position = caster.Position;

            if (nowTick <= skinComp.attachedShieldVisualExpireTick) return;

            skinComp.attachedShieldVisualExpireTick = -1;
            skinComp.attachedShieldVisualThingId = string.Empty;
            skinComp.attachedShieldVisualCached = null;
        }
    }

    public class ProjectileInterceptorShieldHandler : IOnApplyHandler, ITickHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.ProjectileInterceptorShield;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
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

            skinComp.projectileInterceptorShieldThingId = thing.ThingID;
            skinComp.projectileInterceptorShieldExpireTick = nowTick + Mathf.Max(1, config.shieldInterceptorDurationTicks);
            skinComp.projectileInterceptorShieldCached = thing;
        }

        public void OnTick(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            if (skinComp.projectileInterceptorShieldExpireTick < 0) return;

            Thing? activeThing = CompAbilityEffect_Modular.FindThingByIdCached(caster.MapHeld, skinComp.projectileInterceptorShieldThingId, ref skinComp.projectileInterceptorShieldCached);

            if (activeThing != null && activeThing.Position != caster.Position)
                activeThing.Position = caster.Position;

            if (nowTick <= skinComp.projectileInterceptorShieldExpireTick) return;

            if (activeThing != null && !activeThing.Destroyed)
                activeThing.Destroy(DestroyMode.Vanish);

            skinComp.projectileInterceptorShieldExpireTick = -1;
            skinComp.projectileInterceptorShieldThingId = string.Empty;
            skinComp.projectileInterceptorShieldCached = null;
        }
    }

    public class ChainBounceHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.ChainBounce;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
            => source.TriggerChainBounce(config, caster, target);
    }

    public class DashEmpoweredStrikeHandler : IOnApplyHandler, IPostHitHandler, IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.DashEmpoweredStrike;

        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            if (source.HasMovementRuntimeComponent())
                skinComp.dashEmpowerExpireTick = nowTick + Mathf.Max(1, config.dashEmpowerDurationTicks);
        }

        public void OnPostHit(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin casterSkin, LocalTargetInfo target, Pawn targetPawn, CompPawnSkin targetSkin, float appliedDamage, int nowTick)
        {
            // (}-ˆ²::¶Å–!î}-ö dashEmpower ÍŽÀ;¶	
            if (casterSkin != null && casterSkin.dashEmpowerExpireTick >= nowTick)
            {
                casterSkin.dashEmpowerExpireTick = -1;
            }
        }

        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin casterSkin, LocalTargetInfo target, Pawn targetPawn, CompPawnSkin targetSkin, bool allowDashConsume, int nowTick)
        {
            if (allowDashConsume && casterSkin != null && casterSkin.dashEmpowerExpireTick >= nowTick)
                return Mathf.Max(0f, config.dashEmpowerBonusDamageScale);
            return 0f;
        }
    }

    public class FlightStateHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.FlightState;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            CompAbilityEffect_Modular.ApplyFlightState(config, source.parent?.def?.defName ?? string.Empty, caster, skinComp, target, nowTick);
        }
    }

    public class VanillaPawnFlyerHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.VanillaPawnFlyer;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            CompAbilityEffect_Modular.ApplyFlightState(config, source.parent?.def?.defName ?? string.Empty, caster, skinComp, target, nowTick);
        }
    }

    public class FlightOnlyFollowupHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.FlightOnlyFollowup;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            if (caster.Flying && config.consumeFlightStateOnCast)
            {
                skinComp.flightStateExpireTick = nowTick;
                caster.flight?.ForceLand();
            }
        }
    }

    public class FlightLandingBurstHandler : IOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.FlightLandingBurst;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
            => skinComp.vanillaFlightPendingLandingBurst = true;
    }
}