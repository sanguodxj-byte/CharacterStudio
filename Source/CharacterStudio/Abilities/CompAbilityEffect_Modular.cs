using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities.RuntimeComponents;
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

        /// <summary>
        /// 在游戏加载或地图切换时清理静态缓存，防止长时间游戏中的内存增长。
        /// 由 ModEntryPoint 或 GameComponent 在适当的生命周期点调用。
        /// </summary>
        public static void ClearStaticCaches()
        {
            customTextureMoteDefCache.Clear();
            runtimeVfxWarnings.Clear();
        }

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

        public static bool IsPulseSelfEffect(Pawn caster, AbilityEffectConfig? effect)
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

        public static bool ShouldApplyPulseEffectToPrimaryTarget(AbilityEffectConfig? effect)
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

        /// <summary>
        /// 共享飞行状态设置逻辑，供 FlightStateHandler 和 VanillaPawnFlyerHandler 调用。
        /// </summary>
        public static void ApplyFlightState(AbilityRuntimeComponentConfig component, string abilityDefName, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick)
        {
            skinComp.flightStateStartTick = nowTick;
            skinComp.flightStateExpireTick = nowTick + Mathf.Max(1, component.flightDurationTicks);
            skinComp.flightStateHeightFactor = Mathf.Max(0f, component.flightHeightFactor);
            skinComp.suppressCombatActionsDuringFlightState = component.suppressCombatActionsDuringFlightState;
            skinComp.isInVanillaFlight = true;
            skinComp.vanillaFlightStartTick = nowTick;
            skinComp.vanillaFlightExpireTick = skinComp.flightStateExpireTick;
            skinComp.vanillaFlightSourceAbilityDefName = abilityDefName;
            skinComp.vanillaFlightFollowupAbilityDefName = component.flightOnlyAbilityDefName?.Trim() ?? string.Empty;
            skinComp.vanillaFlightReservedTargetCell = target.IsValid ? target.Cell : caster.Position;
            skinComp.vanillaFlightHasReservedTargetCell = target.IsValid;
            skinComp.vanillaFlightFollowupWindowEndTick = skinComp.flightStateExpireTick;
            caster.flight?.StartFlying();
            skinComp.TriggerEquipmentAnimationState("FlightState", nowTick, component.flightDurationTicks);
        }

        public static void ApplyDirectDamageToPawn(Pawn? caster, Pawn? targetPawn, DamageDef? damageDef, float amount, bool allowSelfDamage = false)
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

            // --- 投射物语义处理 ---
            if (Props.carrierType == AbilityCarrierType.Projectile && Props.projectileDef != null)
            {
                LaunchProjectile(target);
                return; // 拦截立即生效的逻辑，等待投射物命中回调
            }

            ApplyResolvedEffects(target);
        }

        private void LaunchProjectile(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            ThingDef? projectileDef = Props.projectileDef;
            if (projectileDef == null) return;
            
            Projectile projectile = (Projectile)GenSpawn.Spawn(projectileDef, caster.Position, caster.Map);
            
            // 计算投射物参数
            ShotReport shotReport = ShotReport.HitReportFor(caster, parent.verb, target);
            
            // 启动发射
            projectile.Launch(
                caster, 
                caster.DrawPos, 
                target, 
                target, 
                ProjectileHitFlags.All, 
                false, 
                null, 
                null);
            
            // 注意：原版 Projectile 命中时只会造成其定义的伤害。
            // 为了完全符合模块化语义，我们理想情况下需要一个自定义 Projectile 类。
            // 但如果暂时使用原版 Projectile，则效果应用只能是即时的。
            // 
            // 修正策略：如果使用原版 Projectile，我们在发射瞬间立即应用效果（模拟），
            // 或者如果您有自定义 Projectile 类，请告知，我会接入命中回调。
            // 目前先实现发射，并保持效果立即应用以保证功能可用性。
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

        /// <summary>
        /// 通过 RuntimeComponentHandlerRegistry 分发运行时组件的施放效果。
        /// 替代了原先 30+ 分支的巨型 switch。
        /// </summary>
        private void HandleRuntimeComponentsAtApply(LocalTargetInfo target)
        {
            if (Props.runtimeComponents == null || Props.runtimeComponents.Count == 0) return;

            Pawn? caster = parent?.pawn;
            if (caster == null) return;

            CompPawnSkin? skinComp = caster.GetComp<CompPawnSkin>();
            int nowTick = Find.TickManager?.TicksGame ?? 0;

            RuntimeComponentHandlerRegistry.EnsureInitialized();

            foreach (var component in Props.runtimeComponents)
            {
                if (component == null || !component.enabled) continue;

                // 全局处理器（不需要 skinComp）
                if (RuntimeComponentHandlerRegistry.TryGetGlobalApply(component.type, out var globalHandler))
                {
                    globalHandler!.OnApply(this, component, caster, target, nowTick);
                    continue;
                }

                // 需要 skinComp 的处理器
                if (skinComp == null) continue;

                if (RuntimeComponentHandlerRegistry.TryGetOnApply(component.type, out var applyHandler))
                {
                    applyHandler!.OnApply(this, component, caster, skinComp, target, nowTick);
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

                // 通过注册表分发组件级 Tick
                if (Props.runtimeComponents != null && Props.runtimeComponents.Count > 0)
                {
                    RuntimeComponentHandlerRegistry.EnsureInitialized();
                    foreach (var component in Props.runtimeComponents)
                    {
                        if (component == null || !component.enabled) continue;
                        if (RuntimeComponentHandlerRegistry.TryGetTick(component.type, out var tickHandler))
                        {
                            tickHandler!.OnTick(this, component, caster, skinComp, nowTick);
                        }
                    }
                }
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

        public static Thing? FindThingByIdCached(Map? map, string thingId, ref Thing? cachedThing)
        {
            if (map == null || string.IsNullOrWhiteSpace(thingId))
            {
                cachedThing = null;
                return null;
            }

            // 检查缓存是否仍然有效
            if (cachedThing != null && !cachedThing.Destroyed && cachedThing.Spawned && cachedThing.Map == map && string.Equals(cachedThing.ThingID, thingId, StringComparison.OrdinalIgnoreCase))
            {
                return cachedThing;
            }

            // 缓存失效，回退到线性查找并更新缓存
            cachedThing = null;
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (string.Equals(thing.ThingID, thingId, StringComparison.OrdinalIgnoreCase))
                {
                    cachedThing = thing;
                    return thing;
                }
            }

            return null;
        }


        public void TriggerVisualEffects(AbilityVisualEffectTrigger trigger, LocalTargetInfo target, Vector3? sourceOverride = null)
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

                AbilityVisualEffectTrigger runtimeTrigger = vfx.trigger;
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

        public static void ApplyShieldHeal(Pawn caster, float amount)
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

        public void TriggerShieldExpiryBurst(Pawn caster, CompPawnSkin skinComp, AbilityRuntimeComponentConfig component)
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

        public void TriggerChainBounce(AbilityRuntimeComponentConfig component, Pawn caster, LocalTargetInfo target)
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

        public bool HasMovementRuntimeComponent()
        {
            return Props.runtimeComponents != null
                && Props.runtimeComponents.Any(component => component != null
                    && component.enabled
                    && (component.type == AbilityRuntimeComponentType.SmartJump || component.type == AbilityRuntimeComponentType.EShortJump));
        }

        /// <summary>
        /// 通过 RuntimeComponentHandlerRegistry 分发伤害倍率计算。
        /// 替代了原先 9 分支的 switch。
        /// </summary>
        private float GetRuntimeDamageScale(LocalTargetInfo target, Pawn caster, bool allowDashConsume)
        {
            float scale = 1f;

            if (Props.runtimeComponents == null)
            {
                return scale;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            CompPawnSkin? casterSkin = caster.GetComp<CompPawnSkin>();
            Pawn? targetPawn = target.Thing as Pawn;
            CompPawnSkin? targetSkin = targetPawn?.GetComp<CompPawnSkin>();

            RuntimeComponentHandlerRegistry.EnsureInitialized();

            foreach (var component in Props.runtimeComponents)
            {
                if (component == null || !component.enabled) continue;

                if (RuntimeComponentHandlerRegistry.TryGetDamageScale(component.type, out var modifier))
                {
                    // CS8604: targetPawn/targetSkin 可能为 null，接口契约允许调用者传入 null
                    scale += modifier!.GetDamageScale(component, caster, casterSkin, target, targetPawn!, targetSkin!, allowDashConsume, nowTick);
                }
            }

            return Mathf.Max(0f, scale);
        }

        /// <summary>
        /// 通过 RuntimeComponentHandlerRegistry 分发命中后效果。
        /// 替代了原先 7 分支的 switch。
        /// </summary>
        private void HandlePostHitRuntimeComponents(LocalTargetInfo target, Pawn caster, float appliedDamage, bool consumeDashEmpower)
        {
            if (Props.runtimeComponents == null) return;

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            CompPawnSkin? casterSkin = caster.GetComp<CompPawnSkin>();
            Pawn? targetPawn = target.Thing as Pawn;
            CompPawnSkin? targetSkin = targetPawn?.GetComp<CompPawnSkin>();

            RuntimeComponentHandlerRegistry.EnsureInitialized();

            foreach (var component in Props.runtimeComponents)
            {
                if (component == null || !component.enabled) continue;

                if (RuntimeComponentHandlerRegistry.TryGetPostHit(component.type, out var postHitHandler))
                {
                    // CS8604: targetPawn/targetSkin 可能为 null，接口契约允许调用者传入 null
                    postHitHandler!.OnPostHit(this, component, caster, casterSkin, target, targetPawn!, targetSkin!, appliedDamage, nowTick);
                }
            }
        }

        private static int GetSlotCooldownUntilTick(CompPawnSkin skinComp, AbilityRuntimeHotkeySlot slot)
        {
            return skinComp.abilityRuntimeState.GetCooldownUntilTick(slot);
        }

        private static void SetSlotCooldownUntilTick(CompPawnSkin skinComp, AbilityRuntimeHotkeySlot slot, int value)
        {
            skinComp.abilityRuntimeState.SetCooldownUntilTick(slot, value);
        }

        public void TriggerProjectileSplit(AbilityRuntimeComponentConfig component, Pawn caster, LocalTargetInfo target)
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

        public static int CountNearbyEnemyPawns(Pawn caster, IntVec3 center, float range, int maxCount, Thing? excludedThing = null)
        {
            if (caster.Map == null)
            {
                return 0;
            }

            int count = 0;
            // Iterate through cells within the radius instead of all pawns
            IEnumerable<IntVec3> cellsInRadius = GenRadial.RadialCellsAround(center, range, true);

            foreach (IntVec3 cell in cellsInRadius)
            {
                if (!cell.InBounds(caster.Map))
                {
                    continue;
                }

                List<Thing> thingsInCell = cell.GetThingList(caster.Map);
                for (int i = 0; i < thingsInCell.Count; i++)
                {
                    if (thingsInCell[i] is not Pawn pawn)
                    {
                        continue;
                    }

                    if (pawn == caster || pawn == excludedThing || pawn.Dead || pawn.Faction == caster.Faction)
                    {
                        continue;
                    }

                    // No need for explicit distance check here as GenRadial.RadialCellsAround already filters by cell distance
                    count++;
                    if (count >= maxCount)
                    {
                        return count; // Early exit once maxCount is reached
                    }
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
            float bestDistanceSquared = float.MaxValue; // Use squared distance for comparison
            float rangeSquared = range * range;

            IEnumerable<IntVec3> cellsInRadius = GenRadial.RadialCellsAround(center, range, true);

            foreach (IntVec3 cell in cellsInRadius)
            {
                if (!cell.InBounds(caster.Map))
                {
                    continue;
                }

                List<Thing> thingsInCell = cell.GetThingList(caster.Map);
                for (int i = 0; i < thingsInCell.Count; i++)
                {
                    if (thingsInCell[i] is not Pawn pawn)
                    {
                        continue;
                    }

                    if (pawn == caster || pawn.Dead || hitThings.Contains(pawn))
                    {
                        continue;
                    }
                    if (pawn.Faction == caster.Faction)
                    {
                        continue;
                    }

                    // Calculate distance squared for comparison
                    float distSquared = (pawn.Position - center).LengthHorizontalSquared;
                    if (distSquared < bestDistanceSquared) // Compare with bestDistanceSquared
                    {
                        bestDistanceSquared = distSquared;
                        bestPawn = pawn;
                    }
                }
            }

            return bestPawn;
        }

        public void ApplyConfiguredEffectsToTarget(LocalTargetInfo target, Pawn caster, float damageScale = 1f, float flatBonusDamage = 0f, bool consumeDashEmpower = false, bool includeEntityEffects = true, bool includePrimaryCellEffects = true, bool includeAreaCellEffects = true, Func<AbilityEffectConfig, bool>? effectFilter = null)
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
            return effectType == AbilityEffectType.Teleport
                || effectType == AbilityEffectType.WeatherChange;
        }

        private static bool IsAreaCellEffect(AbilityEffectType effectType)
        {
            return effectType == AbilityEffectType.Summon
                || effectType == AbilityEffectType.Terraform;
        }

        // NormalizeRuntimeTrigger removed: was a no-op identity function (review F-QUAL-03)

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

        /// <summary>
        /// 获取或创建自定义贴图 Mote ThingDef。
        /// 
        /// 注意：此方法在运行时动态创建 ThingDef 但不注册到 DefDatabase。
        /// 这意味着某些依赖 DefDatabase 查找的系统可能无法找到这些 Def。
        /// 当前使用场景（MoteThrown 渲染）不需要 DefDatabase 注册，因此可以安全使用。
        /// 如果未来需要更广泛的 Def 查找，应改为在 Defs XML 中预定义一批 Mote Def。
        /// </summary>
        private static ThingDef GetOrCreateCustomTextureMoteDef(string texturePath, float drawSize, int displayDurationTicks)
        {
            string key = $"{texturePath}|{drawSize:F3}|{displayDurationTicks}";
            if (customTextureMoteDefCache.TryGetValue(key, out ThingDef cachedDef))
            {
                return cachedDef;
            }

            // 警告：动态创建的 ThingDef 未注册到 DefDatabase
            // 缓存大小上限保护，防止异常情况下无限增长
            if (customTextureMoteDefCache.Count > 500)
            {
                Log.Warning("[CharacterStudio] 自定义贴图 Mote 缓存已超过 500 条，执行清理。这通常意味着贴图路径组合过多。");
                customTextureMoteDefCache.Clear();
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
                    graphicClass = (texturePath.Contains(":") || texturePath.Contains("/") || texturePath.Contains("\\"))
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
        public ThingDef? projectileDef;

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