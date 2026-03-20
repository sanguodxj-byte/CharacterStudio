using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 技能授予工具
    /// 将 ModularAbilityDef 列表即时授予 / 撤销 Pawn
    /// 使用 HediffComp_GiveAbility 思路：在 Pawn.abilities 列表中动态注入临时 AbilityDef
    /// </summary>
    public static class AbilityGrantUtility
    {
        // 追踪已授予给每个 Pawn 的 CS 技能 defName，用于撤销
        private static readonly Dictionary<int, HashSet<string>> grantedAbilityNames =
            new Dictionary<int, HashSet<string>>();

        // 动态生成的运行时 AbilityDef 缓存（按 ModularAbilityDef.defName）
        private static readonly Dictionary<string, AbilityDef> runtimeAbilityDefs =
            new Dictionary<string, AbilityDef>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> runtimeAbilityFingerprints =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>所有 CS 技能的最小冷却时间（0.5s = 30 ticks），防止技能被连点刷屏</summary>
        private const int MinAbilityCooldownTicks = 30;

        // ─────────────────────────────────────────────
        // 公共入口
        // ─────────────────────────────────────────────

        /// <summary>
        /// 将皮肤中的所有技能授予 Pawn
        /// 已有同名技能时跳过，不重复授予
        /// </summary>
        public static void GrantSkinAbilitiesToPawn(Pawn pawn, PawnSkinDef skin)
        {
            if (pawn == null || skin == null || skin.abilities == null || skin.abilities.Count == 0)
                return;

            RevokeAllCSAbilitiesFromPawn(pawn);

            var grantedSet = GetOrCreateGrantedSet(pawn);

            foreach (var modAbility in skin.abilities)
            {
                if (modAbility == null || string.IsNullOrEmpty(modAbility.defName))
                    continue;

                try
                {
                    var abilityDef = GetOrBuildRuntimeAbilityDef(modAbility);
                    if (abilityDef == null) continue;

                    // 防止重复授予
                    if (pawn.abilities?.GetAbility(abilityDef) != null) continue;

                    pawn.abilities?.GainAbility(abilityDef);
                    grantedSet.Add(modAbility.defName);

                    if (Prefs.DevMode)
                        Log.Message($"[CharacterStudio] 已授予技能 {modAbility.label ?? modAbility.defName} 给 {pawn.LabelShort}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 授予技能 {modAbility.defName} 时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 撤销所有由 CS 授予给 Pawn 的技能
        /// </summary>
        public static void RevokeAllCSAbilitiesFromPawn(Pawn pawn)
        {
            if (pawn == null) return;

            int pawnId = pawn.thingIDNumber;
            if (!grantedAbilityNames.TryGetValue(pawnId, out var grantedSet)) return;

            foreach (var defName in grantedSet.ToList())
            {
                try
                {
                    if (runtimeAbilityDefs.TryGetValue(defName, out var abilityDef))
                    {
                        var ability = pawn.abilities?.GetAbility(abilityDef);
                        if (ability != null)
                        {
                            pawn.abilities?.RemoveAbility(abilityDef);
                            if (Prefs.DevMode)
                                Log.Message($"[CharacterStudio] 已撤销技能 {defName} 从 {pawn.LabelShort}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 撤销技能 {defName} 时出错: {ex.Message}");
                }
            }

            grantedAbilityNames.Remove(pawnId);
        }

        /// <summary>
        /// 获取某 Pawn 当前被 CS 授予的技能 defName 列表
        /// </summary>
        public static IReadOnlyCollection<string> GetGrantedAbilityNames(Pawn pawn)
        {
            if (pawn == null) return Array.Empty<string>();
            if (grantedAbilityNames.TryGetValue(pawn.thingIDNumber, out var set))
                return set;
            return Array.Empty<string>();
        }

        /// <summary>
        /// 获取 Pawn 被 CS 授予的某个运行时 AbilityDef
        /// </summary>
        public static AbilityDef? GetRuntimeAbilityDef(string modAbilityDefName)
        {
            runtimeAbilityDefs.TryGetValue(modAbilityDefName, out var def);
            return def;
        }

        // ─────────────────────────────────────────────
        // 运行时 AbilityDef 构建
        // ─────────────────────────────────────────────

        private static AbilityDef? GetOrBuildRuntimeAbilityDef(ModularAbilityDef modAbility)
        {
            string key = modAbility.defName;
            string fingerprint = BuildAbilityFingerprint(modAbility);

            if (runtimeAbilityDefs.TryGetValue(key, out var cached))
            {
                if (runtimeAbilityFingerprints.TryGetValue(key, out var oldFingerprint) && oldFingerprint == fingerprint)
                    return cached;

                try
                {
                    ConfigureRuntimeAbilityDef(cached, modAbility, key);
                    runtimeAbilityFingerprints[key] = fingerprint;
                    return cached;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 刷新运行时 AbilityDef [{key}] 失败: {ex.Message}");
                }
            }

            try
            {
                // 强制最小 CD：无论 ModularAbilityDef.cooldownTicks 如何配置，
                // 原版技能栏中的技能也必须至少有 MinAbilityCooldownTicks（0.5s）的冷却，
                // 防止玩家连点刷屏产生重复效果。
                int resolvedCd = Mathf.Max((int)modAbility.cooldownTicks, MinAbilityCooldownTicks);
                var abilityDef = new AbilityDef();
                ConfigureRuntimeAbilityDef(abilityDef, modAbility, key, resolvedCd);
                DefDatabase<AbilityDef>.Add(abilityDef);

                runtimeAbilityDefs[key] = abilityDef;
                runtimeAbilityFingerprints[key] = fingerprint;
                return abilityDef;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 构建运行时 AbilityDef [{key}] 失败: {ex.Message}");
                return null;
            }
        }

        private static void ConfigureRuntimeAbilityDef(AbilityDef abilityDef, ModularAbilityDef modAbility, string key, int? preResolvedCooldown = null)
        {
            int resolvedCd = preResolvedCooldown ?? Mathf.Max((int)modAbility.cooldownTicks, MinAbilityCooldownTicks);

            abilityDef.defName = "CS_RT_" + key;
            abilityDef.label = modAbility.label ?? key;
            abilityDef.description = modAbility.description ?? string.Empty;
            abilityDef.iconPath = string.IsNullOrEmpty(modAbility.iconPath)
                ? "UI/Designators/Strip"
                : modAbility.iconPath;
            abilityDef.cooldownTicksRange = new IntRange(resolvedCd, resolvedCd);
            abilityDef.charges = modAbility.charges;
            abilityDef.aiCanUse = modAbility.aiCanUse > 0.5f;

            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(modAbility.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(modAbility);

            VerbProperties verbProps = normalizedCarrier switch
            {
                AbilityCarrierType.Self => BuildVerbProps_Self(),
                AbilityCarrierType.Target => BuildVerbProps_Target(modAbility.range, normalizedTarget),
                AbilityCarrierType.Projectile => BuildVerbProps_Projectile(modAbility.range, normalizedTarget, modAbility.projectileDef),
                _ => BuildVerbProps_Self()
            };

            if (modAbility.warmupTicks > 0f)
                verbProps.warmupTime = modAbility.warmupTicks / 60f;
            abilityDef.verbProperties = verbProps;

            bool hasEffects = modAbility.effects != null && modAbility.effects.Count > 0;
            bool hasVisualEffects = modAbility.visualEffects != null && modAbility.visualEffects.Count > 0;
            bool hasRuntimeComponents = modAbility.runtimeComponents != null && modAbility.runtimeComponents.Count > 0;

            if (hasEffects || hasVisualEffects || hasRuntimeComponents)
            {
                var compProps = new CompProperties_AbilityModular();
                if (hasEffects)
                    compProps.effects.AddRange(modAbility.effects.Where(e => e != null).Select(e => e.Clone()));
                if (hasVisualEffects)
                    compProps.visualEffects.AddRange(modAbility.visualEffects.Where(v => v != null).Select(v => v.Clone()));
                if (hasRuntimeComponents)
                    compProps.runtimeComponents.AddRange(modAbility.runtimeComponents.Where(c => c != null).Select(c => c.Clone()));
                abilityDef.comps = new List<AbilityCompProperties> { compProps };
            }
            else
            {
                abilityDef.comps = null;
            }

            abilityDef.ResolveReferences();
            abilityDef.PostLoad();
        }

        private static string BuildAbilityFingerprint(ModularAbilityDef modAbility)
        {
            var parts = new List<string>
            {
                modAbility.defName ?? string.Empty,
                modAbility.label ?? string.Empty,
                modAbility.description ?? string.Empty,
                modAbility.iconPath ?? string.Empty,
                modAbility.cooldownTicks.ToString("F3"),
                modAbility.warmupTicks.ToString("F3"),
                modAbility.charges.ToString(),
                modAbility.aiCanUse.ToString("F3"),
                modAbility.carrierType.ToString(),
                modAbility.targetType.ToString(),
                modAbility.useRadius.ToString(),
                modAbility.areaCenter.ToString(),
                modAbility.range.ToString("F3"),
                modAbility.radius.ToString("F3"),
                modAbility.projectileDef?.defName ?? string.Empty
            };

            if (modAbility.effects != null)
            {
                foreach (var effect in modAbility.effects)
                {
                    if (effect == null) continue;
                    parts.Add($"E:{effect.type}|{effect.amount:F3}|{effect.duration:F3}|{effect.chance:F3}|{effect.damageDef?.defName}|{effect.hediffDef?.defName}|{effect.summonKind?.defName}|{effect.summonCount}|{effect.canHurtSelf}");
                }
            }

            if (modAbility.visualEffects != null)
            {
                foreach (var vfx in modAbility.visualEffects)
                {
                    if (vfx == null) continue;
                    parts.Add($"V:{vfx.type}|{vfx.sourceMode}|{vfx.presetDefName}|{vfx.target}|{vfx.trigger}|{vfx.delayTicks}|{vfx.scale:F3}|{vfx.repeatCount}|{vfx.repeatIntervalTicks}|{vfx.offset.x:F3},{vfx.offset.y:F3},{vfx.offset.z:F3}|{vfx.attachToPawn}|{vfx.attachToTargetCell}|{vfx.enabled}");
                }
            }

            if (modAbility.runtimeComponents != null)
            {
                foreach (var component in modAbility.runtimeComponents)
                {
                    if (component == null) continue;
                    parts.Add($"R:{component.type}|{component.enabled}|{component.comboWindowTicks}|{component.cooldownTicks}|{component.jumpDistance}|{component.findCellRadius}|{component.triggerAbilityEffectsAfterJump}|{component.requiredStacks}|{component.delayTicks}|{component.wave1Radius:F3}|{component.wave1Damage:F3}|{component.wave2Radius:F3}|{component.wave2Damage:F3}|{component.wave3Radius:F3}|{component.wave3Damage:F3}|{component.waveDamageDef?.defName}");
                }
            }

            return string.Join("||", parts);
        }

        private static VerbProperties BuildVerbProps_Self()
            => new VerbProperties
            {
                verbClass    = typeof(Verb_CastAbility),
                range        = 0f,
                targetParams = new TargetingParameters { canTargetSelf = true, canTargetPawns = false, canTargetLocations = false }
            };

        private static VerbProperties BuildVerbProps_Target(float range, AbilityTargetType targetType)
            => new VerbProperties
            {
                verbClass    = typeof(Verb_CastAbility),
                range        = Mathf.Max(range, 1f),
                targetParams = BuildTargetingParameters(targetType)
            };

        /// <summary>
        /// 构建投射物载体的 VerbProperties
        /// 使用 Verb_LaunchProjectile 实现真正的投射物发射
        /// </summary>
        private static VerbProperties BuildVerbProps_Projectile(float range, AbilityTargetType targetType, ThingDef? projectileDef)
        {
            var projectile = projectileDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("Bullet_Basic");

            return new VerbProperties
            {
                verbClass         = typeof(Verb_LaunchProjectile),
                range             = Mathf.Max(range, 1f),
                targetParams      = BuildTargetingParameters(targetType),
                defaultProjectile = projectile
            };
        }

        private static TargetingParameters BuildTargetingParameters(AbilityTargetType targetType)
        {
            return targetType switch
            {
                AbilityTargetType.Cell => new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    canTargetSelf = false
                },
                AbilityTargetType.Entity => new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetLocations = false,
                    canTargetSelf = false
                },
                _ => new TargetingParameters
                {
                    canTargetSelf = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    canTargetLocations = false
                }
            };
        }

        private static HashSet<string> GetOrCreateGrantedSet(Pawn pawn)
        {
            int id = pawn.thingIDNumber;
            if (!grantedAbilityNames.TryGetValue(id, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                grantedAbilityNames[id] = set;
            }
            return set;
        }
    }
}