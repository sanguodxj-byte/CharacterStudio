using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Rendering;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 模块化能力组件
    /// 负责执行 ModularAbilityDef 中定义的原子效果及视觉特效
    /// </summary>
    public class CompAbilityEffect_Modular : CompAbilityEffect, ICompAbilityEffectOnJumpCompleted
    {
        public new CompProperties_AbilityModular Props => (CompProperties_AbilityModular)props;

        private static readonly Dictionary<string, ThingDef> customTextureMoteDefCache = new Dictionary<string, ThingDef>();
        private static readonly HashSet<string> runtimeVfxWarnings = new HashSet<string>();

        private static bool CanApplyDamageToTarget(Pawn? caster, LocalTargetInfo target, AbilityEffectConfig? damageEffect = null)
        {
            if (caster == null || !target.HasThing || target.Thing != caster)
            {
                return true;
            }

            return damageEffect?.canHurtSelf == true;
        }

        private bool HasAnySelfDamageEnabledEffect()
        {
            return Props.effects != null && Props.effects.Any(effect => effect != null
                && effect.type == AbilityEffectType.Damage
                && effect.canHurtSelf);
        }

        private static bool IsPulseSelfEffect(Pawn caster, AbilityEffectConfig? effect)
        {
            if (effect == null)
            {
                return false;
            }

            return effect.type == AbilityEffectType.Heal
                || effect.type == AbilityEffectType.Buff
                || effect.type == AbilityEffectType.Debuff
                || (effect.type == AbilityEffectType.Damage && CanApplyDamageToTarget(caster, new LocalTargetInfo(caster), effect));
        }

        private static bool ShouldApplyPulseEffectToPrimaryTarget(AbilityEffectConfig? effect)
        {
            if (effect == null)
            {
                return false;
            }

            return effect.type != AbilityEffectType.Heal
                && effect.type != AbilityEffectType.Buff
                && effect.type != AbilityEffectType.Debuff;
        }

        private bool CanApplyRuntimeBonusDamageToTarget(Pawn? caster, LocalTargetInfo target)
        {
            return CanApplyDamageToTarget(caster, target) || HasAnySelfDamageEnabledEffect();
        }

        private static void ApplyDirectDamageToPawn(Pawn? caster, Pawn? targetPawn, DamageDef? damageDef, float amount, bool allowSelfDamage = false)
        {
            if (caster == null || targetPawn == null || targetPawn.Dead || amount <= 0f)
            {
                return;
            }

            if (targetPawn == caster && !allowSelfDamage)
            {
                return;
            }

            targetPawn.TakeDamage(new DamageInfo(damageDef ?? DamageDefOf.Bomb, amount, 0f, -1f, caster));
        }

        // 延迟视觉特效队列：(触发时间tick, 特效配置, 目标, 视觉源点覆盖)
        private readonly List<(int triggerTick, AbilityVisualEffectConfig vfxConfig, LocalTargetInfo target, Vector3? sourceOverride)> pendingVfx
            = new List<(int, AbilityVisualEffectConfig, LocalTargetInfo, Vector3?)>();

        // 延迟声音队列：(触发时间tick, 特效配置, 目标, 视觉源点覆盖)
        private readonly List<(int triggerTick, AbilityVisualEffectConfig vfxConfig, LocalTargetInfo target, Vector3? sourceOverride)> pendingVfxSounds
            = new List<(int, AbilityVisualEffectConfig, LocalTargetInfo, Vector3?)>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            TriggerVisualEffects(AbilityVisualEffectTrigger.OnCastStart, target);
            if (parent?.def?.verbProperties?.warmupTime > 0f)
            {
                TriggerVisualEffects(AbilityVisualEffectTrigger.OnWarmup, target);
            }

            AbilityRuntimeComponentConfig? jumpComponent = GetDeferredJumpComponent();
            if (jumpComponent != null)
            {
                return;
            }

            ApplyResolvedEffects(target);
        }

        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            Pawn? caster = parent?.pawn;
            LocalTargetInfo resolvedTarget = target;

            if (!resolvedTarget.IsValid || resolvedTarget.Thing == caster)
            {
                IntVec3 landingCell = caster?.Position ?? origin;
                resolvedTarget = new LocalTargetInfo(landingCell);
            }

            ApplyResolvedEffects(resolvedTarget);
        }

        private void ApplyResolvedEffects(LocalTargetInfo target)
        {
            Pawn? caster = parent?.pawn;
            if (caster == null)
            {
                return;
            }

            ModularAbilityDef runtimeAbility = BuildRuntimeAbilitySnapshot();
            List<IntVec3> affectedCells = AbilityAreaUtility.BuildResolvedTargetCells(caster, runtimeAbility, target)
                .Distinct()
                .ToList();
            if (affectedCells.Count == 0 && target.IsValid)
            {
                affectedCells.Add(target.Cell);
            }

            List<LocalTargetInfo> thingTargets = ResolveThingTargets(caster, runtimeAbility, target, affectedCells);
            List<LocalTargetInfo> areaCellTargets = ResolveAreaCellTargets(target, affectedCells);
            LocalTargetInfo primaryCellTarget = ResolvePrimaryCellTarget(caster, target);

            float pendingBonusDamage = ResolveAndConsumeShieldBonusDamage(caster);

            TriggerVisualEffects(AbilityVisualEffectTrigger.OnCastFinish, target);

            for (int i = 0; i < thingTargets.Count; i++)
            {
                LocalTargetInfo resolvedTarget = thingTargets[i];
                bool targetWasAlive = resolvedTarget.Thing is Pawn targetPawnBefore && !targetPawnBefore.Dead;
                ApplyConfiguredEffectsToTarget(
                    resolvedTarget,
                    caster,
                    1f,
                    i == 0 ? pendingBonusDamage : 0f,
                    consumeDashEmpower: i == 0,
                    includeEntityEffects: true,
                    includePrimaryCellEffects: false,
                    includeAreaCellEffects: false);

                HandleKillRefreshAtApply(caster, resolvedTarget, targetWasAlive);
            }

            ApplyConfiguredEffectsToTarget(
                primaryCellTarget,
                caster,
                1f,
                0f,
                false,
                includeEntityEffects: false,
                includePrimaryCellEffects: true,
                includeAreaCellEffects: false);

            foreach (LocalTargetInfo cellTarget in areaCellTargets)
            {
                ApplyConfiguredEffectsToTarget(
                    cellTarget,
                    caster,
                    1f,
                    0f,
                    false,
                    includeEntityEffects: false,
                    includePrimaryCellEffects: false,
                    includeAreaCellEffects: true);
            }

            if (thingTargets.Count == 0)
            {
                CompPawnSkin? skinComp = caster.GetComp<CompPawnSkin>();
                if (skinComp != null)
                {
                    skinComp.dashEmpowerExpireTick = -1;
                }
            }

            HandleRuntimeComponentsAtApply(target);
            TriggerVisualEffects(AbilityVisualEffectTrigger.OnTargetApply, target);
        }

        private ModularAbilityDef BuildRuntimeAbilitySnapshot()
        {
            return new ModularAbilityDef
            {
                carrierType = Props.carrierType,
                targetType = Props.targetType,
                useRadius = Props.useRadius,
                areaCenter = Props.areaCenter,
                areaShape = Props.areaShape,
                irregularAreaPattern = Props.irregularAreaPattern ?? string.Empty,
                range = Props.range,
                radius = Props.radius
            };
        }

        private static List<LocalTargetInfo> ResolveThingTargets(Pawn caster, ModularAbilityDef ability, LocalTargetInfo originalTarget, List<IntVec3> affectedCells)
        {
            List<LocalTargetInfo> result = new List<LocalTargetInfo>();

            if (!ability.useRadius && originalTarget.HasThing)
            {
                result.Add(originalTarget);
                return result;
            }

            Map? map = caster.Map;
            if (map != null)
            {
                foreach (Thing thing in AbilityAreaUtility.EnumerateDistinctThingsInCells(map, affectedCells))
                {
                    result.Add(new LocalTargetInfo(thing));
                }
            }

            if (result.Count == 0 && ModularAbilityDefExtensions.NormalizeTargetType(ability) == AbilityTargetType.Self)
            {
                result.Add(new LocalTargetInfo(caster));
            }

            return result;
        }

        private static LocalTargetInfo ResolvePrimaryCellTarget(Pawn caster, LocalTargetInfo originalTarget)
        {
            if (originalTarget.IsValid)
            {
                return new LocalTargetInfo(originalTarget.Cell);
            }

            return new LocalTargetInfo(caster.Position);
        }

        private static List<LocalTargetInfo> ResolveAreaCellTargets(LocalTargetInfo originalTarget, List<IntVec3> affectedCells)
        {
            if (affectedCells.Count > 0)
            {
                return affectedCells.Select(cell => new LocalTargetInfo(cell)).ToList();
            }

            if (originalTarget.IsValid)
            {
                return new List<LocalTargetInfo> { new LocalTargetInfo(originalTarget.Cell) };
            }

            return new List<LocalTargetInfo>();
        }

        private AbilityRuntimeComponentConfig? GetDeferredJumpComponent()
        {
            if (Props.runtimeComponents == null || Props.runtimeComponents.Count == 0)
            {
                return null;
            }

            return Props.runtimeComponents.FirstOrDefault(component => component != null
                && component.enabled
                && component.triggerAbilityEffectsAfterJump
                && (component.type == AbilityRuntimeComponentType.SmartJump || component.type == AbilityRuntimeComponentType.EShortJump));
        }

        private void HandleRuntimeComponentsAtApply(LocalTargetInfo target)
        {
            if (Props.runtimeComponents == null || Props.runtimeComponents.Count == 0)
            {
                return;
            }

            Pawn? caster = parent?.pawn;
            CompPawnSkin? skinComp = caster?.GetComp<CompPawnSkin>();
            if (caster == null || skinComp == null)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            foreach (var component in Props.runtimeComponents)
            {
                if (component == null || !component.enabled)
                {
                    continue;
                }

                switch (component.type)
                {
                    case AbilityRuntimeComponentType.SlotOverrideWindow:
                        int comboWindow = component.comboWindowTicks > 0 ? component.comboWindowTicks : 12;
                        skinComp.slotOverrideWindowEndTick = nowTick + comboWindow;
                        skinComp.slotOverrideWindowSlotId = component.comboTargetHotkeySlot.ToString();
                        skinComp.slotOverrideWindowAbilityDefName = component.comboTargetAbilityDefName ?? string.Empty;
                        break;
                    case AbilityRuntimeComponentType.HotkeyOverride:
                        ApplyHotkeyOverride(component, skinComp, nowTick);
                        break;
                    case AbilityRuntimeComponentType.FollowupCooldownGate:
                        ApplyFollowupCooldownGate(component, skinComp, nowTick);
                        break;
                    case AbilityRuntimeComponentType.RStackDetonation:
                        if (skinComp.rStackingEnabled && !skinComp.rSecondStageReady)
                        {
                            int requiredStacks = component.requiredStacks > 0 ? component.requiredStacks : 7;
                            skinComp.rStackCount = System.Math.Min(requiredStacks, skinComp.rStackCount + 1);
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
                        break;
                    case AbilityRuntimeComponentType.PeriodicPulse:
                        ArmPeriodicPulse(component, skinComp, nowTick);
                        break;
                    case AbilityRuntimeComponentType.ShieldAbsorb:
                        ArmShieldAbsorb(component, skinComp, nowTick);
                        break;
                    case AbilityRuntimeComponentType.AttachedShieldVisual:
                        ArmAttachedShieldVisual(component, skinComp, nowTick);
                        break;
                    case AbilityRuntimeComponentType.ProjectileInterceptorShield:
                        ArmProjectileInterceptorShield(component, caster, skinComp, nowTick);
                        break;
                    case AbilityRuntimeComponentType.ChainBounce:
                        TriggerChainBounce(component, caster, target);
                        break;
                    case AbilityRuntimeComponentType.DashEmpoweredStrike:
                        if (HasMovementRuntimeComponent())
                        {
                            skinComp.dashEmpowerExpireTick = nowTick + Mathf.Max(1, component.dashEmpowerDurationTicks);
                        }
                        break;
                    case AbilityRuntimeComponentType.FlightState:
                        skinComp.flightStateStartTick = nowTick;
                        skinComp.flightStateExpireTick = nowTick + Mathf.Max(1, component.flightDurationTicks);
                        skinComp.flightStateHeightFactor = Mathf.Max(0f, component.flightHeightFactor);
                        skinComp.suppressCombatActionsDuringFlightState = component.suppressCombatActionsDuringFlightState;
                        skinComp.isInVanillaFlight = true;
                        skinComp.vanillaFlightStartTick = nowTick;
                        skinComp.vanillaFlightExpireTick = skinComp.flightStateExpireTick;
                        skinComp.vanillaFlightSourceAbilityDefName = parent?.def?.defName ?? string.Empty;
                        skinComp.vanillaFlightFollowupAbilityDefName = component.flightOnlyAbilityDefName?.Trim() ?? string.Empty;
                        skinComp.vanillaFlightReservedTargetCell = target.IsValid ? target.Cell : caster.Position;
                        skinComp.vanillaFlightHasReservedTargetCell = target.IsValid;
                        skinComp.vanillaFlightFollowupWindowEndTick = skinComp.flightStateExpireTick;
                        caster.flight?.StartFlying();
                        skinComp.TriggerEquipmentAnimationState("FlightState", nowTick, component.flightDurationTicks);
                        break;
                    case AbilityRuntimeComponentType.VanillaPawnFlyer:
                        skinComp.flightStateStartTick = nowTick;
                        skinComp.flightStateExpireTick = nowTick + Mathf.Max(1, component.flightDurationTicks);
                        skinComp.flightStateHeightFactor = Mathf.Max(0f, component.flightHeightFactor);
                        skinComp.suppressCombatActionsDuringFlightState = component.suppressCombatActionsDuringFlightState;
                        skinComp.isInVanillaFlight = true;
                        skinComp.vanillaFlightStartTick = nowTick;
                        skinComp.vanillaFlightExpireTick = skinComp.flightStateExpireTick;
                        skinComp.vanillaFlightSourceAbilityDefName = parent?.def?.defName ?? string.Empty;
                        skinComp.vanillaFlightFollowupAbilityDefName = component.flightOnlyAbilityDefName?.Trim() ?? string.Empty;
                        skinComp.vanillaFlightReservedTargetCell = target.IsValid ? target.Cell : caster.Position;
                        skinComp.vanillaFlightHasReservedTargetCell = target.IsValid;
                        skinComp.vanillaFlightFollowupWindowEndTick = skinComp.flightStateExpireTick;
                        caster.flight?.StartFlying();
                        skinComp.TriggerEquipmentAnimationState("FlightState", nowTick, component.flightDurationTicks);
                        break;
                    case AbilityRuntimeComponentType.FlightOnlyFollowup:
                        if (caster.Flying && component.consumeFlightStateOnCast)
                        {
                            skinComp.flightStateExpireTick = nowTick;
                            caster.flight?.ForceLand();
                        }
                        break;
                    case AbilityRuntimeComponentType.FlightLandingBurst:
                        skinComp.vanillaFlightPendingLandingBurst = true;
                        break;
                    case AbilityRuntimeComponentType.TimeStop:
                        AbilityTimeStopRuntimeController.ActivateForCaster(caster, component, nowTick);
                        break;
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            Pawn? caster = parent?.pawn;
            CompPawnSkin? skinComp = caster?.GetComp<CompPawnSkin>();

            if (caster != null && skinComp != null)
            {
                TickFlightState(caster, skinComp, nowTick);
                TickPeriodicPulse(caster, skinComp, nowTick);
                TickShieldAbsorb(caster, skinComp, nowTick);
                TickProjectileInterceptorShield(caster, skinComp, nowTick);
            }

            for (int i = pendingVfx.Count - 1; i >= 0; i--)
            {
                var (triggerTick, vfxConfig, target, sourceOverride) = pendingVfx[i];
                if (nowTick >= triggerTick)
                {
                    PlayVfx(vfxConfig, target, sourceOverride);
                    pendingVfx.RemoveAt(i);
                }
            }

            for (int i = pendingVfxSounds.Count - 1; i >= 0; i--)
            {
                var (triggerTick, vfxConfig, target, sourceOverride) = pendingVfxSounds[i];
                if (nowTick >= triggerTick)
                {
                    PlayVfxSound(vfxConfig, target, sourceOverride);
                    pendingVfxSounds.RemoveAt(i);
                }
            }
        }

        private void HandleKillRefreshAtApply(Pawn? caster, LocalTargetInfo target, bool targetWasAlive)
        {
            if (caster == null || !targetWasAlive || target.Thing is not Pawn targetPawn || !targetPawn.Dead)
            {
                return;
            }

            CompPawnSkin? skinComp = caster.GetComp<CompPawnSkin>();
            AbilityRuntimeComponentConfig? component = Props.runtimeComponents?.FirstOrDefault(c => c != null && c.enabled && c.type == AbilityRuntimeComponentType.KillRefresh);
            if (skinComp == null || component == null)
            {
                return;
            }

            float ratio = Mathf.Clamp01(component.killRefreshCooldownPercent <= 0f ? 1f : component.killRefreshCooldownPercent);
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int currentCooldown = GetSlotCooldownUntilTick(skinComp, component.killRefreshHotkeySlot) - nowTick;
            currentCooldown = Mathf.Max(0, currentCooldown);
            int reducedCooldown = Mathf.RoundToInt(currentCooldown * Mathf.Max(0f, 1f - ratio));
            SetSlotCooldownUntilTick(skinComp, component.killRefreshHotkeySlot, nowTick + reducedCooldown);
        }

        private void ApplyHotkeyOverride(AbilityRuntimeComponentConfig component, CompPawnSkin skinComp, int nowTick)
        {
            if (string.IsNullOrWhiteSpace(component.overrideAbilityDefName))
            {
                return;
            }

            int expireTick = nowTick + Mathf.Max(1, component.overrideDurationTicks);
            string overrideDefName = component.overrideAbilityDefName.Trim();
            switch (component.overrideHotkeySlot)
            {
                case AbilityRuntimeHotkeySlot.Q:
                    skinComp.qOverrideAbilityDefName = overrideDefName;
                    skinComp.qOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.W:
                    skinComp.wOverrideAbilityDefName = overrideDefName;
                    skinComp.wOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.E:
                    skinComp.eOverrideAbilityDefName = overrideDefName;
                    skinComp.eOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.T:
                    skinComp.tOverrideAbilityDefName = overrideDefName;
                    skinComp.tOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.A:
                    skinComp.aOverrideAbilityDefName = overrideDefName;
                    skinComp.aOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.S:
                    skinComp.sOverrideAbilityDefName = overrideDefName;
                    skinComp.sOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.D:
                    skinComp.dOverrideAbilityDefName = overrideDefName;
                    skinComp.dOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.F:
                    skinComp.fOverrideAbilityDefName = overrideDefName;
                    skinComp.fOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.Z:
                    skinComp.zOverrideAbilityDefName = overrideDefName;
                    skinComp.zOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.X:
                    skinComp.xOverrideAbilityDefName = overrideDefName;
                    skinComp.xOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.C:
                    skinComp.cOverrideAbilityDefName = overrideDefName;
                    skinComp.cOverrideExpireTick = expireTick;
                    break;
                case AbilityRuntimeHotkeySlot.V:
                    skinComp.vOverrideAbilityDefName = overrideDefName;
                    skinComp.vOverrideExpireTick = expireTick;
                    break;
                default:
                    skinComp.rOverrideAbilityDefName = overrideDefName;
                    skinComp.rOverrideExpireTick = expireTick;
                    break;
            }
        }

        private static void ApplyFollowupCooldownGate(AbilityRuntimeComponentConfig component, CompPawnSkin skinComp, int nowTick)
        {
            int cooldownUntil = nowTick + Mathf.Max(1, component.followupCooldownTicks);
            switch (component.followupCooldownHotkeySlot)
            {
                case AbilityRuntimeHotkeySlot.Q:
                    skinComp.qCooldownUntilTick = Mathf.Max(skinComp.qCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.W:
                    skinComp.wCooldownUntilTick = Mathf.Max(skinComp.wCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.E:
                    skinComp.eCooldownUntilTick = Mathf.Max(skinComp.eCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.T:
                    skinComp.tCooldownUntilTick = Mathf.Max(skinComp.tCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.A:
                    skinComp.aCooldownUntilTick = Mathf.Max(skinComp.aCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.S:
                    skinComp.sCooldownUntilTick = Mathf.Max(skinComp.sCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.D:
                    skinComp.dCooldownUntilTick = Mathf.Max(skinComp.dCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.F:
                    skinComp.fCooldownUntilTick = Mathf.Max(skinComp.fCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.Z:
                    skinComp.zCooldownUntilTick = Mathf.Max(skinComp.zCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.X:
                    skinComp.xCooldownUntilTick = Mathf.Max(skinComp.xCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.C:
                    skinComp.cCooldownUntilTick = Mathf.Max(skinComp.cCooldownUntilTick, cooldownUntil);
                    break;
                case AbilityRuntimeHotkeySlot.V:
                    skinComp.vCooldownUntilTick = Mathf.Max(skinComp.vCooldownUntilTick, cooldownUntil);
                    break;
                default:
                    skinComp.rCooldownUntilTick = Mathf.Max(skinComp.rCooldownUntilTick, cooldownUntil);
                    break;
            }
        }

        private void ArmPeriodicPulse(AbilityRuntimeComponentConfig component, CompPawnSkin skinComp, int nowTick)
        {
            int interval = Mathf.Max(1, component.pulseIntervalTicks);
            int duration = Mathf.Max(interval, component.pulseTotalTicks);
            skinComp.periodicPulseEndTick = nowTick + duration;
            skinComp.periodicPulseNextTick = component.pulseStartsImmediately ? nowTick : nowTick + interval;
        }

        private void TickPeriodicPulse(Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            if (skinComp.periodicPulseNextTick < 0 || skinComp.periodicPulseEndTick < nowTick)
            {
                return;
            }

            AbilityRuntimeComponentConfig? component = Props.runtimeComponents?.FirstOrDefault(c => c != null && c.enabled && c.type == AbilityRuntimeComponentType.PeriodicPulse);
            if (component == null || caster.Map == null)
            {
                return;
            }

            int interval = Mathf.Max(1, component.pulseIntervalTicks);
            while (skinComp.periodicPulseNextTick >= 0 && nowTick >= skinComp.periodicPulseNextTick && skinComp.periodicPulseNextTick <= skinComp.periodicPulseEndTick)
            {
                ExecutePulse(caster);
                TriggerVisualEffects(AbilityVisualEffectTrigger.OnDurationTick, new LocalTargetInfo(caster.Position));
                skinComp.periodicPulseNextTick += interval;
            }

            if (skinComp.periodicPulseNextTick > skinComp.periodicPulseEndTick)
            {
                TriggerVisualEffects(AbilityVisualEffectTrigger.OnExpire, new LocalTargetInfo(caster.Position));
                skinComp.periodicPulseNextTick = -1;
                skinComp.periodicPulseEndTick = -1;
            }
        }

        private void ExecutePulse(Pawn caster)
        {
            if (caster.Map == null || Props.effects == null)
            {
                return;
            }

            bool hasSelfAffectingPulseEffect = Props.effects.Any(effect => IsPulseSelfEffect(caster, effect));
            if (hasSelfAffectingPulseEffect)
            {
                ApplyConfiguredEffectsToTarget(
                    new LocalTargetInfo(caster),
                    caster,
                    effectFilter: effect => IsPulseSelfEffect(caster, effect));
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

            ApplyConfiguredEffectsToTarget(
                pulseTarget,
                caster,
                effectFilter: effect => ShouldApplyPulseEffectToPrimaryTarget(effect));
        }

        private void ArmShieldAbsorb(AbilityRuntimeComponentConfig component, CompPawnSkin skinComp, int nowTick)
        {
            skinComp.shieldRemainingDamage = Mathf.Max(0f, component.shieldMaxDamage);
            skinComp.shieldExpireTick = nowTick + Mathf.Max(1, Mathf.RoundToInt(component.shieldDurationTicks));
            skinComp.shieldStoredHeal = 0f;
            skinComp.shieldStoredBonusDamage = 0f;
        }

        private static void ArmAttachedShieldVisual(AbilityRuntimeComponentConfig component, CompPawnSkin skinComp, int nowTick)
        {
            Pawn? caster = skinComp.Pawn;
            if (caster?.Map == null)
            {
                return;
            }

            ThingDef? visualDef = DefDatabase<ThingDef>.GetNamedSilentFail("CS_AttachedShieldVisual");
            if (visualDef == null)
            {
                Log.Warning("[CharacterStudio] AttachedShieldVisual missing thingDef: CS_AttachedShieldVisual");
                return;
            }

            Thing visualThing = ThingMaker.MakeThing(visualDef);
            GenSpawn.Spawn(visualThing, caster.Position, caster.Map, WipeMode.Vanish);

            int duration = Mathf.Max(1, Mathf.RoundToInt(component.shieldDurationTicks));
            skinComp.attachedShieldVisualExpireTick = nowTick + duration;
            skinComp.attachedShieldVisualScale = Mathf.Max(0.1f, component.shieldVisualScale);
            skinComp.attachedShieldVisualHeightOffset = component.shieldVisualHeightOffset;
            skinComp.attachedShieldVisualThingId = visualThing.ThingID;
            skinComp.RequestRenderRefresh();
        }

        private static void ArmProjectileInterceptorShield(AbilityRuntimeComponentConfig component, Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            if (caster.Map == null || string.IsNullOrWhiteSpace(component.shieldInterceptorThingDefName))
            {
                return;
            }

            ThingDef? shieldDef = DefDatabase<ThingDef>.GetNamedSilentFail(component.shieldInterceptorThingDefName);
            if (shieldDef == null)
            {
                Log.Warning($"[CharacterStudio] ProjectileInterceptorShield missing thingDef: {component.shieldInterceptorThingDefName}");
                return;
            }

            Thing thing = ThingMaker.MakeThing(shieldDef);
            if (thing == null)
            {
                return;
            }

            GenSpawn.Spawn(thing, caster.Position, caster.Map, WipeMode.Vanish);
            if (thing.TryGetComp<CompProjectileInterceptor>() is CompProjectileInterceptor interceptor)
            {
                interceptor.Activate();
            }

            skinComp.projectileInterceptorShieldThingId = thing.ThingID;
            skinComp.projectileInterceptorShieldExpireTick = nowTick + Mathf.Max(1, component.shieldInterceptorDurationTicks);
        }

        private void TickShieldAbsorb(Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            if (skinComp.shieldExpireTick < 0 || nowTick <= skinComp.shieldExpireTick)
            {
                return;
            }

            AbilityRuntimeComponentConfig? shieldComponent = Props.runtimeComponents?.FirstOrDefault(c => c != null && c.enabled && c.type == AbilityRuntimeComponentType.ShieldAbsorb);

            if (skinComp.shieldStoredHeal > 0f)
            {
                ApplyShieldHeal(caster, skinComp.shieldStoredHeal);
            }

            if (shieldComponent != null)
            {
                TriggerShieldExpiryBurst(caster, skinComp, shieldComponent);
            }

            skinComp.shieldRemainingDamage = 0f;
            skinComp.shieldExpireTick = -1;
            skinComp.shieldStoredHeal = 0f;
            skinComp.shieldStoredBonusDamage = 0f;
            TriggerVisualEffects(AbilityVisualEffectTrigger.OnExpire, new LocalTargetInfo(caster.Position));
        }

        private static void TickProjectileInterceptorShield(Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            TickAttachedShieldVisual(caster, skinComp, nowTick);

            if (skinComp.projectileInterceptorShieldExpireTick < 0)
            {
                return;
            }

            Thing? activeThing = FindThingById(caster.MapHeld, skinComp.projectileInterceptorShieldThingId);
            if (activeThing != null && activeThing.Position != caster.Position)
            {
                activeThing.Position = caster.Position;
            }

            if (nowTick <= skinComp.projectileInterceptorShieldExpireTick)
            {
                return;
            }

            Thing? shieldThing = FindThingById(caster.MapHeld, skinComp.projectileInterceptorShieldThingId);
            if (shieldThing != null && !shieldThing.Destroyed)
            {
                shieldThing.Destroy(DestroyMode.Vanish);
            }

            skinComp.projectileInterceptorShieldExpireTick = -1;
            skinComp.projectileInterceptorShieldThingId = string.Empty;
        }

        private static void TickFlightState(Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            if (skinComp.flightStateExpireTick < 0 && !skinComp.isInVanillaFlight)
            {
                return;
            }

            if (caster.Flying)
            {
                if (skinComp.flightStateExpireTick >= 0 && nowTick > skinComp.flightStateExpireTick)
                {
                    caster.flight?.ForceLand();
                }
                return;
            }

            if (skinComp.vanillaFlightPendingLandingBurst)
            {
                AbilityVanillaFlightUtility.TryApplyLandingBurst(caster, caster.Position, skinComp.vanillaFlightSourceAbilityDefName);
            }

            skinComp.flightStateExpireTick = -1;
            skinComp.flightStateHeightFactor = 0f;
            skinComp.suppressCombatActionsDuringFlightState = false;
            skinComp.ClearEquipmentAnimationState("FlightState");
            AbilityVanillaFlightUtility.ClearVanillaFlightState(caster);
            skinComp.RequestRenderRefresh();
        }

        private static void TickAttachedShieldVisual(Pawn caster, CompPawnSkin skinComp, int nowTick)
        {
            if (skinComp.attachedShieldVisualExpireTick < 0)
            {
                return;
            }

            Thing? visualThing = FindThingById(caster.MapHeld, skinComp.attachedShieldVisualThingId);
            if (visualThing != null && visualThing.Position != caster.Position)
            {
                visualThing.Position = caster.Position;
            }

            if (nowTick <= skinComp.attachedShieldVisualExpireTick)
            {
                return;
            }

            if (visualThing != null && !visualThing.Destroyed)
            {
                visualThing.Destroy(DestroyMode.Vanish);
            }

            skinComp.attachedShieldVisualExpireTick = -1;
            skinComp.attachedShieldVisualThingId = string.Empty;
        }

        private static Thing? FindThingById(Map? map, string thingId)
        {
            if (map == null || string.IsNullOrWhiteSpace(thingId))
            {
                return null;
            }

            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (string.Equals(thing.ThingID, thingId, StringComparison.OrdinalIgnoreCase))
                {
                    return thing;
                }
            }

            return null;
        }

        private void TriggerVisualEffects(AbilityVisualEffectTrigger trigger, LocalTargetInfo target, Vector3? sourceOverride = null)
        {
            QueueVisualEffectsForTrigger(trigger, target, sourceOverride);
        }

        private void QueueVisualEffectsForTrigger(AbilityVisualEffectTrigger trigger, LocalTargetInfo target, Vector3? sourceOverride = null)
        {
            if (Props.visualEffects == null || Props.visualEffects.Count == 0)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            foreach (var vfx in Props.visualEffects)
            {
                if (vfx == null || !vfx.enabled)
                {
                    continue;
                }

                AbilityVisualEffectTrigger runtimeTrigger = NormalizeRuntimeTrigger(vfx.trigger);
                if (runtimeTrigger != trigger)
                {
                    continue;
                }

                int repeatCount = vfx.repeatCount <= 0 ? 1 : vfx.repeatCount;
                int repeatIntervalTicks = vfx.repeatIntervalTicks < 0 ? 0 : vfx.repeatIntervalTicks;

                for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                {
                    int totalDelay = vfx.delayTicks + (repeatIndex * repeatIntervalTicks);
                    if (totalDelay <= 0)
                    {
                        PlayVfx(vfx, target, sourceOverride);
                    }
                    else
                    {
                        pendingVfx.Add((nowTick + totalDelay, vfx, target, sourceOverride));
                    }

                    if (vfx.playSound && !string.IsNullOrWhiteSpace(vfx.soundDefName))
                    {
                        int soundDelay = totalDelay + Mathf.Max(0, vfx.soundDelayTicks);
                        if (soundDelay <= 0)
                        {
                            PlayVfxSound(vfx, target, sourceOverride);
                        }
                        else
                        {
                            pendingVfxSounds.Add((nowTick + soundDelay, vfx, target, sourceOverride));
                        }
                    }
                }
            }
        }

        private static void ApplyShieldHeal(Pawn caster, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            List<Hediff>? hediffs = caster.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return;
            }

            float remainingHeal = amount;
            for (int i = hediffs.Count - 1; i >= 0 && remainingHeal > 0.001f; i--)
            {
                if (hediffs[i] is Hediff_Injury injury)
                {
                    float healAmount = Mathf.Min(remainingHeal, injury.Severity);
                    if (healAmount <= 0f)
                    {
                        continue;
                    }

                    injury.Heal(healAmount);
                    remainingHeal -= healAmount;
                }
            }
        }

        private float ResolveAndConsumeShieldBonusDamage(Pawn? caster)
        {
            CompPawnSkin? skinComp = caster?.GetComp<CompPawnSkin>();
            if (skinComp == null)
            {
                return 0f;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (skinComp.shieldExpireTick < nowTick || skinComp.shieldStoredBonusDamage <= 0f)
            {
                return 0f;
            }

            float bonus = skinComp.shieldStoredBonusDamage;
            skinComp.shieldStoredBonusDamage = 0f;
            return bonus;
        }

        private void TriggerShieldExpiryBurst(Pawn caster, CompPawnSkin skinComp, AbilityRuntimeComponentConfig component)
        {
            if (caster.Map == null)
            {
                return;
            }

            float storedDamage = skinComp.shieldStoredBonusDamage;
            float burstDamage = storedDamage * Mathf.Max(0f, component.shieldBonusDamageRatio);
            if (burstDamage <= 0.001f)
            {
                return;
            }

            const float burstRadius = 2.4f;

            IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(caster.Position, burstRadius, true);
            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(caster.Map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(caster.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn pawn && pawn != caster && pawn.Faction != caster.Faction)
                    {
                        ApplyDirectDamageToPawn(caster, pawn, DamageDefOf.Bomb, burstDamage, allowSelfDamage: false);
                    }
                }
            }
        }

        private void TriggerChainBounce(AbilityRuntimeComponentConfig component, Pawn caster, LocalTargetInfo target)
        {
            if (caster.Map == null || Props.effects == null || !target.HasThing)
            {
                return;
            }

            Thing firstThing = target.Thing;
            if (firstThing is not Pawn firstPawn)
            {
                return;
            }

            HashSet<Thing> hitThings = new HashSet<Thing> { firstThing };
            Thing currentThing = firstThing;
            float damageScale = 1f;
            int maxBounceCount = Mathf.Max(0, component.maxBounceCount);
            float range = Mathf.Max(0.1f, component.bounceRange);
            float falloff = Mathf.Clamp(component.bounceDamageFalloff, -0.95f, 0.95f);

            for (int bounceIndex = 0; bounceIndex < maxBounceCount; bounceIndex++)
            {
                Pawn? nextPawn = FindNearestBounceTarget(caster, currentThing.Position, range, hitThings);
                if (nextPawn == null)
                {
                    break;
                }

                damageScale *= Mathf.Max(0.01f, 1f - falloff);
                Vector3 bounceSource = currentThing.DrawPos;
                ApplyConfiguredEffectsToTarget(
                    new LocalTargetInfo(nextPawn),
                    caster,
                    damageScale,
                    0f,
                    false,
                    includeEntityEffects: true,
                    includePrimaryCellEffects: false,
                    includeAreaCellEffects: false);
                TriggerVisualEffects(AbilityVisualEffectTrigger.OnTargetApply, new LocalTargetInfo(nextPawn), bounceSource);
                hitThings.Add(nextPawn);
                currentThing = nextPawn;
            }
        }

        private bool HasMovementRuntimeComponent()
        {
            return Props.runtimeComponents != null
                && Props.runtimeComponents.Any(component => component != null
                    && component.enabled
                    && (component.type == AbilityRuntimeComponentType.SmartJump || component.type == AbilityRuntimeComponentType.EShortJump));
        }

        private float GetRuntimeDamageScale(LocalTargetInfo target, Pawn caster, bool allowDashConsume)
        {
            float scale = 1f;
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            CompPawnSkin? casterSkin = caster.GetComp<CompPawnSkin>();
            Pawn? targetPawn = target.Thing as Pawn;
            CompPawnSkin? targetSkin = targetPawn?.GetComp<CompPawnSkin>();

            if (Props.runtimeComponents == null)
            {
                return scale;
            }

            foreach (var component in Props.runtimeComponents)
            {
                if (component == null || !component.enabled)
                {
                    continue;
                }

                switch (component.type)
                {
                    case AbilityRuntimeComponentType.ExecuteBonusDamage:
                        if (targetPawn != null && targetPawn.health != null)
                        {
                            float summary = targetPawn.health.summaryHealth.SummaryHealthPercent;
                            if (summary <= component.executeThresholdPercent)
                            {
                                scale += Mathf.Max(0f, component.executeBonusDamageScale);
                            }
                        }
                        break;
                    case AbilityRuntimeComponentType.MissingHealthBonusDamage:
                        if (targetPawn != null && targetPawn.health != null)
                        {
                            float missing = 1f - targetPawn.health.summaryHealth.SummaryHealthPercent;
                            float steps = Mathf.Max(0f, missing / 0.1f);
                            float bonus = steps * Mathf.Max(0f, component.missingHealthBonusPerTenPercent);
                            scale += Mathf.Min(Mathf.Max(0f, component.missingHealthBonusMaxScale), bonus);
                        }
                        break;
                    case AbilityRuntimeComponentType.FullHealthBonusDamage:
                        if (targetPawn != null && targetPawn.health != null)
                        {
                            float summary = targetPawn.health.summaryHealth.SummaryHealthPercent;
                            if (summary >= Mathf.Clamp01(component.fullHealthThresholdPercent))
                            {
                                scale += Mathf.Max(0f, component.fullHealthBonusDamageScale);
                            }
                        }
                        break;
                    case AbilityRuntimeComponentType.NearbyEnemyBonusDamage:
                        if (targetPawn != null && caster.Map != null)
                        {
                            int nearbyEnemyCount = CountNearbyEnemyPawns(
                                caster,
                                target.Cell,
                                Mathf.Max(0.1f, component.nearbyEnemyBonusRadius),
                                Mathf.Max(1, component.nearbyEnemyBonusMaxTargets),
                                targetPawn);
                            scale += nearbyEnemyCount * Mathf.Max(0f, component.nearbyEnemyBonusPerTarget);
                        }
                        break;
                    case AbilityRuntimeComponentType.IsolatedTargetBonusDamage:
                        if (targetPawn != null && caster.Map != null)
                        {
                            int nearbyEnemyCount = CountNearbyEnemyPawns(
                                caster,
                                target.Cell,
                                Mathf.Max(0.1f, component.isolatedTargetRadius),
                                1,
                                targetPawn);
                            if (nearbyEnemyCount == 0)
                            {
                                scale += Mathf.Max(0f, component.isolatedTargetBonusDamageScale);
                            }
                        }
                        break;
                    case AbilityRuntimeComponentType.ComboStacks:
                        if (casterSkin != null && casterSkin.offensiveComboExpireTick >= nowTick)
                        {
                            scale += casterSkin.offensiveComboStacks * Mathf.Max(0f, component.comboStackBonusDamagePerStack);
                        }
                        break;
                    case AbilityRuntimeComponentType.DashEmpoweredStrike:
                        if (allowDashConsume && casterSkin != null && casterSkin.dashEmpowerExpireTick >= nowTick)
                        {
                            scale += Mathf.Max(0f, component.dashEmpowerBonusDamageScale);
                        }
                        break;
                    case AbilityRuntimeComponentType.PierceBonusDamage:
                        if (caster.Map != null)
                        {
                            int hitCount = CountNearbyEnemyPawns(caster, target.Cell, Mathf.Max(0.1f, component.pierceSearchRange), Mathf.Max(1, component.pierceMaxTargets));
                            scale += hitCount * Mathf.Max(0f, component.pierceBonusDamagePerTarget);
                        }
                        break;
                    case AbilityRuntimeComponentType.MarkDetonation:
                        if (targetSkin != null && targetSkin.offensiveMarkExpireTick >= nowTick && targetSkin.offensiveMarkStacks > 0)
                        {
                            scale += Mathf.Min(0.5f, targetSkin.offensiveMarkStacks * 0.05f);
                        }
                        break;
                }
            }

            return Mathf.Max(0f, scale);
        }

        private void HandlePostHitRuntimeComponents(LocalTargetInfo target, Pawn caster, float appliedDamage, bool consumeDashEmpower)
        {
            if (Props.runtimeComponents == null)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            CompPawnSkin? casterSkin = caster.GetComp<CompPawnSkin>();
            Pawn? targetPawn = target.Thing as Pawn;
            CompPawnSkin? targetSkin = targetPawn?.GetComp<CompPawnSkin>();

            foreach (var component in Props.runtimeComponents)
            {
                if (component == null || !component.enabled)
                {
                    continue;
                }

                switch (component.type)
                {
                    case AbilityRuntimeComponentType.MarkDetonation:
                        if (targetSkin != null)
                        {
                            if (targetSkin.offensiveMarkExpireTick < nowTick)
                            {
                                targetSkin.offensiveMarkStacks = 0;
                            }
                            targetSkin.offensiveMarkExpireTick = nowTick + Mathf.Max(1, component.markDurationTicks);
                            targetSkin.offensiveMarkStacks = Mathf.Min(Mathf.Max(1, component.markMaxStacks), targetSkin.offensiveMarkStacks + 1);
                            if (targetSkin.offensiveMarkStacks >= Mathf.Max(1, component.markMaxStacks) && targetPawn != null && !targetPawn.Dead)
                            {
                                ApplyDirectDamageToPawn(caster, targetPawn, component.markDamageDef, component.markDetonationDamage);
                                targetSkin.offensiveMarkStacks = 0;
                                targetSkin.offensiveMarkExpireTick = -1;
                            }
                        }
                        break;
                    case AbilityRuntimeComponentType.ComboStacks:
                        if (casterSkin != null)
                        {
                            if (casterSkin.offensiveComboExpireTick < nowTick)
                            {
                                casterSkin.offensiveComboStacks = 0;
                            }
                            casterSkin.offensiveComboExpireTick = nowTick + Mathf.Max(1, component.comboStackWindowTicks);
                            casterSkin.offensiveComboStacks = Mathf.Min(Mathf.Max(1, component.comboStackMax), casterSkin.offensiveComboStacks + 1);
                        }
                        break;
                    case AbilityRuntimeComponentType.HitSlowField:
                        ApplySlowField(component, target.Cell, caster);
                        break;
                    case AbilityRuntimeComponentType.HitHeal:
                        ApplyShieldHeal(caster, Mathf.Max(0f, component.hitHealAmount) + (Mathf.Max(0f, component.hitHealRatio) * Mathf.Max(0f, appliedDamage)));
                        break;
                    case AbilityRuntimeComponentType.HitCooldownRefund:
                        if (casterSkin != null)
                        {
                            ApplyCooldownRefund(component, casterSkin, nowTick);
                        }
                        break;
                    case AbilityRuntimeComponentType.DashEmpoweredStrike:
                        if (consumeDashEmpower && casterSkin != null && casterSkin.dashEmpowerExpireTick >= nowTick)
                        {
                            casterSkin.dashEmpowerExpireTick = -1;
                        }
                        break;
                    case AbilityRuntimeComponentType.ProjectileSplit:
                        TriggerProjectileSplit(component, caster, target);
                        break;
                }
            }
        }

        private void ApplySlowField(AbilityRuntimeComponentConfig component, IntVec3 center, Pawn caster)
        {
            if (caster.Map == null || string.IsNullOrWhiteSpace(component.slowFieldHediffDefName))
            {
                return;
            }

            HediffDef? hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(component.slowFieldHediffDefName.Trim());
            if (hediffDef == null)
            {
                return;
            }

            int durationTicks = Mathf.Max(1, component.slowFieldDurationTicks);
            float radiusSquared = component.slowFieldRadius * component.slowFieldRadius;
            foreach (Pawn pawn in caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn.Dead || pawn.Faction == caster.Faction)
                {
                    continue;
                }

                if ((pawn.Position - center).LengthHorizontalSquared > radiusSquared)
                {
                    continue;
                }

                Hediff hediff = pawn.health.AddHediff(hediffDef);
                HediffComp_Disappears? disappears = hediff.TryGetComp<HediffComp_Disappears>();
                if (disappears != null)
                {
                    disappears.ticksToDisappear = durationTicks;
                }
            }
        }

        private void ApplyCooldownRefund(AbilityRuntimeComponentConfig component, CompPawnSkin skinComp, int nowTick)
        {
            float percent = Mathf.Clamp01(component.hitCooldownRefundPercent);
            int currentCooldown = Mathf.Max(0, GetSlotCooldownUntilTick(skinComp, component.refundHotkeySlot) - nowTick);
            SetSlotCooldownUntilTick(skinComp, component.refundHotkeySlot, nowTick + Mathf.RoundToInt(currentCooldown * (1f - percent)));
        }

        private static int GetSlotCooldownUntilTick(CompPawnSkin skinComp, AbilityRuntimeHotkeySlot slot)
        {
            return slot switch
            {
                AbilityRuntimeHotkeySlot.Q => skinComp.qCooldownUntilTick,
                AbilityRuntimeHotkeySlot.W => skinComp.wCooldownUntilTick,
                AbilityRuntimeHotkeySlot.E => skinComp.eCooldownUntilTick,
                AbilityRuntimeHotkeySlot.T => skinComp.tCooldownUntilTick,
                AbilityRuntimeHotkeySlot.A => skinComp.aCooldownUntilTick,
                AbilityRuntimeHotkeySlot.S => skinComp.sCooldownUntilTick,
                AbilityRuntimeHotkeySlot.D => skinComp.dCooldownUntilTick,
                AbilityRuntimeHotkeySlot.F => skinComp.fCooldownUntilTick,
                AbilityRuntimeHotkeySlot.Z => skinComp.zCooldownUntilTick,
                AbilityRuntimeHotkeySlot.X => skinComp.xCooldownUntilTick,
                AbilityRuntimeHotkeySlot.C => skinComp.cCooldownUntilTick,
                AbilityRuntimeHotkeySlot.V => skinComp.vCooldownUntilTick,
                _ => skinComp.rCooldownUntilTick
            };
        }

        private static void SetSlotCooldownUntilTick(CompPawnSkin skinComp, AbilityRuntimeHotkeySlot slot, int value)
        {
            switch (slot)
            {
                case AbilityRuntimeHotkeySlot.Q:
                    skinComp.qCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.W:
                    skinComp.wCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.E:
                    skinComp.eCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.T:
                    skinComp.tCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.A:
                    skinComp.aCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.S:
                    skinComp.sCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.D:
                    skinComp.dCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.F:
                    skinComp.fCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.Z:
                    skinComp.zCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.X:
                    skinComp.xCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.C:
                    skinComp.cCooldownUntilTick = value;
                    break;
                case AbilityRuntimeHotkeySlot.V:
                    skinComp.vCooldownUntilTick = value;
                    break;
                default:
                    skinComp.rCooldownUntilTick = value;
                    break;
            }
        }

        private void TriggerProjectileSplit(AbilityRuntimeComponentConfig component, Pawn caster, LocalTargetInfo target)
        {
            if (caster.Map == null || !target.HasThing)
            {
                return;
            }

            HashSet<Thing> excluded = new HashSet<Thing> { target.Thing };
            int splitCount = Mathf.Max(0, component.splitProjectileCount);
            for (int i = 0; i < splitCount; i++)
            {
                Pawn? nextPawn = FindNearestBounceTarget(caster, target.Cell, Mathf.Max(0.1f, component.splitSearchRange), excluded);
                if (nextPawn == null)
                {
                    break;
                }

                excluded.Add(nextPawn);
                ApplyConfiguredEffectsToTarget(
                    new LocalTargetInfo(nextPawn),
                    caster,
                    Mathf.Max(0f, component.splitDamageScale),
                    0f,
                    false,
                    includeEntityEffects: true,
                    includePrimaryCellEffects: false,
                    includeAreaCellEffects: false);
            }
        }

        private int CountNearbyEnemyPawns(Pawn caster, IntVec3 center, float range, int maxCount, Thing? excludedThing = null)
        {
            if (caster.Map == null)
            {
                return 0;
            }

            int count = 0;
            float rangeSquared = range * range;
            foreach (Pawn pawn in caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn == caster || pawn == excludedThing || pawn.Dead || pawn.Faction == caster.Faction)
                {
                    continue;
                }

                if ((pawn.Position - center).LengthHorizontalSquared > rangeSquared)
                {
                    continue;
                }

                count++;
                if (count >= maxCount)
                {
                    break;
                }
            }

            return count;
        }

        private Pawn? FindNearestBounceTarget(Pawn caster, IntVec3 center, float range, HashSet<Thing> hitThings)
        {
            if (caster.Map == null)
            {
                return null;
            }

            Pawn? bestPawn = null;
            float bestDistance = float.MaxValue;
            float rangeSquared = range * range;
            foreach (Pawn pawn in caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn == caster || pawn.Dead || hitThings.Contains(pawn))
                {
                    continue;
                }
                if (pawn.Faction == caster.Faction)
                {
                    continue;
                }

                float distSquared = (pawn.Position - center).LengthHorizontalSquared;
                if (distSquared > rangeSquared || distSquared >= bestDistance)
                {
                    continue;
                }

                bestDistance = distSquared;
                bestPawn = pawn;
            }

            return bestPawn;
        }

        private void ApplyConfiguredEffectsToTarget(LocalTargetInfo target, Pawn caster, float damageScale = 1f, float flatBonusDamage = 0f, bool consumeDashEmpower = false, bool includeEntityEffects = true, bool includePrimaryCellEffects = true, bool includeAreaCellEffects = true, Func<AbilityEffectConfig, bool>? effectFilter = null)
        {
            if (Props.effects == null)
            {
                return;
            }

            bool allowRuntimeBonusDamage = CanApplyRuntimeBonusDamageToTarget(caster, target);

            float runtimeScale = includeEntityEffects ? GetRuntimeDamageScale(target, caster, consumeDashEmpower) : 1f;
            float finalDamageScale = Mathf.Max(0f, damageScale * runtimeScale);
            float appliedDamage = 0f;
            bool damageApplied = false;

            foreach (var effectConfig in Props.effects)
            {
                if (effectConfig == null)
                {
                    continue;
                }

                if (effectFilter != null && !effectFilter(effectConfig))
                {
                    continue;
                }

                if (Rand.Value > effectConfig.chance)
                {
                    continue;
                }

                if (IsEntityDirectedEffect(effectConfig.type))
                {
                    if (!includeEntityEffects)
                    {
                        continue;
                    }
                }
                else if (IsPrimaryCellEffect(effectConfig.type))
                {
                    if (!includePrimaryCellEffects)
                    {
                        continue;
                    }
                }
                else if (IsAreaCellEffect(effectConfig.type))
                {
                    if (!includeAreaCellEffects)
                    {
                        continue;
                    }
                }

                var worker = EffectWorkerFactory.GetWorker(effectConfig.type);
                if (effectConfig.type == AbilityEffectType.Damage)
                {
                    AbilityEffectConfig scaledConfig = effectConfig.Clone();
                    float extraDamage = allowRuntimeBonusDamage ? flatBonusDamage : 0f;
                    scaledConfig.amount = (scaledConfig.amount * finalDamageScale) + extraDamage;

                    appliedDamage += Mathf.Max(0f, scaledConfig.amount);
                    damageApplied = true;
                    worker.Apply(scaledConfig, target, caster);
                }
                else
                {
                    worker.Apply(effectConfig, target, caster);
                }
            }

            if (damageApplied)
            {
                HandlePostHitRuntimeComponents(target, caster, appliedDamage, consumeDashEmpower);
            }
            else if (consumeDashEmpower && includeEntityEffects)
            {
                CompPawnSkin? skinComp = caster.GetComp<CompPawnSkin>();
                if (skinComp != null)
                {
                    skinComp.dashEmpowerExpireTick = -1;
                }
            }
        }

        private static bool IsEntityDirectedEffect(AbilityEffectType effectType)
        {
            return effectType == AbilityEffectType.Damage
                || effectType == AbilityEffectType.Heal
                || effectType == AbilityEffectType.Buff
                || effectType == AbilityEffectType.Debuff
                || effectType == AbilityEffectType.Control;
        }

        private static bool IsPrimaryCellEffect(AbilityEffectType effectType)
        {
            return effectType == AbilityEffectType.Teleport;
        }

        private static bool IsAreaCellEffect(AbilityEffectType effectType)
        {
            return effectType == AbilityEffectType.Summon
                || effectType == AbilityEffectType.Terraform;
        }

        private static AbilityVisualEffectTrigger NormalizeRuntimeTrigger(AbilityVisualEffectTrigger trigger)
        {
            return trigger;
        }

        private static void LogRuntimeVfxWarningOnce(string key, string message)
        {
            if (runtimeVfxWarnings.Add(key))
            {
                Log.Warning(message);
            }
        }

        private static bool TryResolveRuntimeVfxType(AbilityVisualEffectConfig vfx, out AbilityVisualEffectType resolvedType)
        {
            resolvedType = vfx.type;
            if (!vfx.UsesPresetType)
            {
                return true;
            }

            if (VisualEffectWorkerFactory.TryResolvePresetType(vfx.presetDefName, out resolvedType))
            {
                return true;
            }

            string presetName = string.IsNullOrWhiteSpace(vfx.presetDefName)
                ? "<empty>"
                : vfx.presetDefName.Trim();
            LogRuntimeVfxWarningOnce(
                $"PresetMissing:{presetName}",
                $"[CharacterStudio] 视觉特效预设 '{presetName}' 未注册到运行时 Worker，已跳过播放。");
            return false;
        }

        private void PlayVfx(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Vector3? sourceOverride = null)
        {
            var caster = parent?.pawn;
            if (caster == null || caster.Map == null) return;

            try
            {
                vfx.NormalizeLegacyData();
                vfx.SyncLegacyFields();

                CompPawnSkin? skinComp = caster.GetComp<CompPawnSkin>();
                if (skinComp != null
                    && (vfx.linkedExpression.HasValue
                        || Math.Abs(vfx.linkedPupilBrightnessOffset) > 0.001f
                        || Math.Abs(vfx.linkedPupilContrastOffset) > 0.001f))
                {
                    skinComp.ApplyAbilityFaceOverride(
                        vfx.linkedExpression,
                        vfx.linkedExpressionDurationTicks,
                        vfx.linkedPupilBrightnessOffset,
                        vfx.linkedPupilContrastOffset);
                }

                if (vfx.UsesCustomTextureType)
                {
                    if (TryPlayCustomTextureVfx(vfx, target, caster, sourceOverride))
                    {
                        return;
                    }

                    Log.Warning($"[CharacterStudio] 自定义贴图特效缺少有效贴图路径，已跳过播放。");
                    return;
                }

                if ((vfx.type == AbilityVisualEffectType.LineTexture || vfx.type == AbilityVisualEffectType.WallTexture)
                    && TryPlaySpatialTextureVfx(vfx, target, caster, sourceOverride))
                {
                    return;
                }

                if (!TryResolveRuntimeVfxType(vfx, out AbilityVisualEffectType runtimeVfxType))
                {
                    return;
                }

                VisualEffectWorker worker = VisualEffectWorkerFactory.GetWorker(runtimeVfxType);
                worker.Play(vfx, CreateRuntimeVfxTarget(vfx, target, caster, sourceOverride), caster);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] VFX 播放异常: {ex.Message}");
            }
        }

        private void PlayVfxSound(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Vector3? sourceOverride = null)
        {
            Pawn? caster = parent?.pawn;
            if (caster?.Map == null || string.IsNullOrWhiteSpace(vfx.soundDefName))
            {
                return;
            }

            try
            {
                SoundDef? soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(vfx.soundDefName);
                if (soundDef == null)
                {
                    Log.Warning($"[CharacterStudio] 未找到 VFX 声音 Def: {vfx.soundDefName}");
                    return;
                }

                foreach (Vector3 soundPos in ResolveVfxPositions(vfx, target, caster, sourceOverride))
                {
                    SoundInfo soundInfo = SoundInfo.InMap(new TargetInfo(soundPos.ToIntVec3(), caster.Map, false), MaintenanceType.None);
                    soundInfo.volumeFactor = Mathf.Max(0f, vfx.soundVolume);
                    soundInfo.pitchFactor = Mathf.Max(0.01f, vfx.soundPitch);
                    soundDef.PlayOneShot(soundInfo);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] VFX 声音播放异常: {ex.Message}");
            }
        }

        private static bool TryPlayCustomTextureVfx(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (string.IsNullOrWhiteSpace(vfx.customTexturePath) || caster.Map == null)
            {
                return false;
            }

            ThingDef moteDef = GetOrCreateCustomTextureMoteDef(
                vfx.customTexturePath,
                Mathf.Max(0.1f, vfx.drawSize),
                Mathf.Max(1, vfx.displayDurationTicks));

            float uniformScale = Mathf.Max(0.1f, vfx.scale);
            float scaleX = Mathf.Max(0.1f, vfx.textureScale.x) * uniformScale;
            float scaleZ = Mathf.Max(0.1f, vfx.textureScale.y) * uniformScale;
            bool playedAny = false;

            foreach (Vector3 spawnPos in ResolveVfxPositions(vfx, target, caster, sourceOverride))
            {
                MoteThrown? mote = ThingMaker.MakeThing(moteDef) as MoteThrown;
                if (mote == null)
                {
                    continue;
                }

                mote.exactPosition = spawnPos;
                mote.exactRotation = ResolveVfxRotation(vfx, target, caster, sourceOverride);
                mote.rotationRate = 0f;
                mote.instanceColor = Color.white;
                mote.linearScale = new Vector3(scaleX, 1f, scaleZ);
                mote.SetVelocity(0f, 0f);
                GenSpawn.Spawn(mote, spawnPos.ToIntVec3(), caster.Map, WipeMode.Vanish);
                playedAny = true;
            }

            return playedAny;
        }

        private static bool TryPlaySpatialTextureVfx(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (string.IsNullOrWhiteSpace(vfx.customTexturePath) || caster.Map == null)
            {
                return false;
            }

            if (!TryResolveSpatialAnchors(vfx, target, caster, sourceOverride, out Vector3 start, out Vector3 end))
            {
                return false;
            }

            Vector3 delta = end - start;
            delta.y = 0f;
            float length = delta.magnitude;
            if (length < 0.05f)
            {
                end = start + caster.Rotation.FacingCell.ToVector3() * 0.2f;
                delta = end - start;
                delta.y = 0f;
                length = Mathf.Max(0.2f, delta.magnitude);
            }

            ThingDef moteDef = GetOrCreateCustomTextureMoteDef(
                vfx.customTexturePath,
                Mathf.Max(0.1f, vfx.drawSize),
                Mathf.Max(1, vfx.displayDurationTicks));

            int requestedSegments = Mathf.Max(1, vfx.segmentCount);
            int segmentCount = vfx.tileByLength
                ? Mathf.Max(requestedSegments, Mathf.CeilToInt(length / Mathf.Max(0.2f, vfx.drawSize)))
                : requestedSegments;
            float rotation = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg + vfx.rotation;
            Vector3 forward = delta.normalized;
            if (forward == Vector3.zero)
            {
                forward = caster.Rotation.FacingCell.ToVector3();
            }

            bool playedAny = false;
            for (int i = 0; i < segmentCount; i++)
            {
                if (vfx.revealBySegments && i > 0)
                {
                    // v1 runtime keeps immediate full spawn; editor/preview expose the field already.
                }

                float t = segmentCount == 1 ? 0.5f : (i + 0.5f) / segmentCount;
                Vector3 spawnPos = Vector3.Lerp(start, end, t);
                if (vfx.followGround)
                {
                    spawnPos = spawnPos.ToIntVec3().ToVector3Shifted();
                }

                MoteThrown? mote = ThingMaker.MakeThing(moteDef) as MoteThrown;
                if (mote == null)
                {
                    continue;
                }

                float uniformScale = Mathf.Max(0.1f, vfx.scale);
                float segmentLength = length / segmentCount;
                float scaleX;
                float scaleZ;
                if (vfx.type == AbilityVisualEffectType.WallTexture)
                {
                    scaleX = Mathf.Max(0.1f, vfx.wallThickness) * uniformScale;
                    scaleZ = Mathf.Max(0.1f, vfx.wallHeight) * uniformScale;
                    spawnPos.y += Mathf.Max(0f, vfx.wallHeight) * 0.5f;
                }
                else
                {
                    scaleX = Mathf.Max(0.1f, vfx.lineWidth) * uniformScale;
                    scaleZ = Mathf.Max(0.1f, segmentLength) * Mathf.Max(0.1f, vfx.textureScale.y) * uniformScale;
                }

                scaleX *= Mathf.Max(0.1f, vfx.textureScale.x);

                mote.exactPosition = spawnPos;
                mote.exactRotation = rotation;
                mote.rotationRate = 0f;
                mote.instanceColor = Color.white;
                mote.linearScale = new Vector3(scaleX, 1f, scaleZ);
                mote.SetVelocity(0f, 0f);
                GenSpawn.Spawn(mote, spawnPos.ToIntVec3(), caster.Map, WipeMode.Vanish);
                playedAny = true;
            }

            return playedAny;
        }

        private static bool TryResolveSpatialAnchors(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride, out Vector3 start, out Vector3 end)
        {
            start = ResolveSpatialAnchor(vfx.anchorMode, target, caster, sourceOverride);
            end = ResolveSpatialAnchor(vfx.secondaryAnchorMode, target, caster, sourceOverride);

            if (vfx.pathMode == AbilityVisualPathMode.DirectLineCasterToTarget)
            {
                start = ResolveSpatialAnchor(AbilityVisualAnchorMode.Caster, target, caster, sourceOverride);
                end = ResolveSpatialAnchor(AbilityVisualAnchorMode.Target, target, caster, sourceOverride);
            }

            if ((end - start).sqrMagnitude < 0.0001f)
            {
                end = ResolveSpatialAnchor(AbilityVisualAnchorMode.Target, target, caster, sourceOverride);
            }

            return true;
        }

        private static Vector3 ResolveSpatialAnchor(AbilityVisualAnchorMode anchorMode, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            switch (anchorMode)
            {
                case AbilityVisualAnchorMode.Caster:
                    return sourceOverride ?? caster.DrawPos;
                case AbilityVisualAnchorMode.TargetCell:
                    return target.IsValid ? target.Cell.ToVector3Shifted() : caster.Position.ToVector3Shifted();
                case AbilityVisualAnchorMode.AreaCenter:
                    return target.IsValid ? target.Cell.ToVector3Shifted() : caster.DrawPos;
                case AbilityVisualAnchorMode.Target:
                default:
                    if (target.HasThing)
                    {
                        return target.Thing.DrawPos;
                    }

                    return target.IsValid ? target.Cell.ToVector3Shifted() : caster.DrawPos;
            }
        }

        private static IEnumerable<Vector3> ResolveVfxPositions(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (vfx.target == VisualEffectTarget.Both)
            {
                Vector3 casterPos = ResolveVfxPosition(vfx, target, caster, VisualEffectTarget.Caster, sourceOverride);
                yield return casterPos;

                Vector3 targetPos = ResolveVfxPosition(vfx, target, caster, VisualEffectTarget.Target, sourceOverride);
                if ((targetPos - casterPos).sqrMagnitude > 0.0001f)
                {
                    yield return targetPos;
                }

                yield break;
            }

            yield return ResolveVfxPosition(vfx, target, caster, vfx.target, sourceOverride);
        }

        private static Vector3 ResolveVfxPosition(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            VisualEffectTarget resolvedTarget = vfx.target == VisualEffectTarget.Both
                ? VisualEffectTarget.Caster
                : vfx.target;
            return ResolveVfxPosition(vfx, target, caster, resolvedTarget, sourceOverride);
        }

        private static Vector3 ResolveVfxPosition(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, VisualEffectTarget targetMode, Vector3? sourceOverride = null)
        {
            Vector3 pos;
            switch (targetMode)
            {
                case VisualEffectTarget.Caster:
                    pos = sourceOverride ?? caster.DrawPos;
                    break;
                case VisualEffectTarget.Target:
                    if (target.HasThing)
                    {
                        pos = target.Thing.DrawPos;
                    }
                    else if (target.IsValid)
                    {
                        pos = target.Cell.ToVector3Shifted();
                    }
                    else
                    {
                        pos = caster.DrawPos;
                    }
                    break;
                case VisualEffectTarget.Both:
                default:
                    pos = caster.DrawPos;
                    break;
            }

            pos += vfx.offset;
            pos.y += vfx.heightOffset;

            if (!TryResolveFacingBasis(vfx, target, caster, sourceOverride, out Vector3 forward, out Vector3 right))
            {
                return pos;
            }

            return pos + forward * vfx.forwardOffset + right * vfx.sideOffset;
        }

        private static float ResolveVfxRotation(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            return vfx.rotation + ResolveAutoFacingAngle(vfx, target, caster, sourceOverride);
        }

        private static float ResolveAutoFacingAngle(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (!TryResolveFacingBasis(vfx, target, caster, sourceOverride, out Vector3 forward, out _))
            {
                return 0f;
            }

            if (Mathf.Abs(forward.x) >= Mathf.Abs(forward.z))
            {
                return forward.x >= 0f ? 90f : 270f;
            }

            return forward.z >= 0f ? 0f : 180f;
        }

        private static bool TryResolveFacingBasis(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride, out Vector3 forward, out Vector3 right)
        {
            AbilityVisualFacingMode facingMode = ResolveFacingMode(vfx);
            if (facingMode == AbilityVisualFacingMode.None)
            {
                forward = Vector3.zero;
                right = Vector3.zero;
                return false;
            }

            IntVec3 forwardCell = facingMode == AbilityVisualFacingMode.CastDirection
                ? ResolveCastDirectionCell(target, caster, sourceOverride)
                : caster.Rotation.FacingCell;

            if (forwardCell == IntVec3.Zero)
            {
                forwardCell = caster.Rotation.FacingCell;
            }

            forward = forwardCell.ToVector3();
            if (forward == Vector3.zero)
            {
                forward = new Vector3(0f, 0f, 1f);
            }

            right = new Vector3(forward.z, 0f, -forward.x);
            return true;
        }

        private static AbilityVisualFacingMode ResolveFacingMode(AbilityVisualEffectConfig vfx)
        {
            AbilityVisualFacingMode facingMode = vfx.facingMode;
            if (!Enum.IsDefined(typeof(AbilityVisualFacingMode), facingMode))
            {
                return vfx.useCasterFacing ? AbilityVisualFacingMode.CasterFacing : AbilityVisualFacingMode.None;
            }

            if (facingMode == AbilityVisualFacingMode.None && vfx.useCasterFacing)
            {
                return AbilityVisualFacingMode.CasterFacing;
            }

            return facingMode;
        }

        private static IntVec3 ResolveCastDirectionCell(LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            IntVec3 origin = sourceOverride?.ToIntVec3() ?? caster.Position;
            IntVec3 destination = target.IsValid ? target.Cell : origin + caster.Rotation.FacingCell;
            IntVec3 delta = destination - origin;
            if (delta == IntVec3.Zero)
            {
                return caster.Rotation.FacingCell;
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
            {
                return delta.x >= 0 ? IntVec3.East : IntVec3.West;
            }

            return delta.z >= 0 ? IntVec3.North : IntVec3.South;
        }

        private static LocalTargetInfo CreateRuntimeVfxTarget(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride)
        {
            if (!sourceOverride.HasValue || vfx.target != VisualEffectTarget.Caster)
            {
                return target;
            }

            return new LocalTargetInfo(sourceOverride.Value.ToIntVec3());
        }

        private static ThingDef GetOrCreateCustomTextureMoteDef(string texturePath, float drawSize, int displayDurationTicks)
        {
            string key = $"{texturePath}|{drawSize:F3}|{displayDurationTicks}";
            if (customTextureMoteDefCache.TryGetValue(key, out ThingDef cachedDef))
            {
                return cachedDef;
            }

            var def = new ThingDef
            {
                defName = $"CS_RuntimeCustomVfx_{customTextureMoteDefCache.Count}",
                label = "runtime custom vfx mote",
                thingClass = typeof(MoteThrown),
                category = ThingCategory.Mote,
                altitudeLayer = AltitudeLayer.MoteOverhead,
                drawerType = DrawerType.RealtimeOnly,
                useHitPoints = false,
                drawGUIOverlay = false,
                tickerType = TickerType.Normal,
                mote = new MoteProperties
                {
                    realTime = true,
                    fadeInTime = 0f,
                    solidTime = Mathf.Max(1, displayDurationTicks) / 60f,
                    fadeOutTime = 0.2f,
                    needsMaintenance = false,
                    collide = false,
                    speedPerTime = 0f,
                    growthRate = 0f
                },
                graphicData = new GraphicData
                {
                    texPath = texturePath,
                    graphicClass = System.IO.Path.IsPathRooted(texturePath) || texturePath.StartsWith("/")
                        ? typeof(Graphic_Runtime)
                        : typeof(Graphic_Single),
                    shaderType = ShaderTypeDefOf.Transparent,
                    drawSize = new Vector2(drawSize, drawSize),
                    color = Color.white,
                    colorTwo = Color.white
                }
            };

            customTextureMoteDefCache[key] = def;
            return def;
        }
    }

    public class CompProperties_AbilityModular : CompProperties_AbilityEffect
    {
        // 运行时几何/目标元数据
        public AbilityCarrierType carrierType = AbilityCarrierType.Self;
        public AbilityTargetType targetType = AbilityTargetType.Self;
        public bool useRadius = false;
        public AbilityAreaCenter areaCenter = AbilityAreaCenter.Target;
        public AbilityAreaShape areaShape = AbilityAreaShape.Circle;
        public string irregularAreaPattern = string.Empty;
        public float range = 0f;
        public float radius = 0f;

        // 游戏逻辑效果列表
        public List<AbilityEffectConfig> effects = new List<AbilityEffectConfig>();

        // 视觉特效列表
        public List<AbilityVisualEffectConfig> visualEffects = new List<AbilityVisualEffectConfig>();

        // 运行时组件列表（用于 Q 连段 / E 短跳 / R 叠层引爆 等扩展行为）
        public List<AbilityRuntimeComponentConfig> runtimeComponents = new List<AbilityRuntimeComponentConfig>();

        public CompProperties_AbilityModular()
        {
            this.compClass = typeof(CompAbilityEffect_Modular);
        }
    }
}
