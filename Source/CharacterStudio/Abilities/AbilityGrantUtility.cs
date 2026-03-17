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
            if (runtimeAbilityDefs.TryGetValue(key, out var cached))
                return cached;

            try
            {
                // 强制最小 CD：无论 ModularAbilityDef.cooldownTicks 如何配置，
                // 原版技能栏中的技能也必须至少有 MinAbilityCooldownTicks（0.5s）的冷却，
                // 防止玩家连点刷屏产生重复效果。
                int resolvedCd = Mathf.Max((int)modAbility.cooldownTicks, MinAbilityCooldownTicks);
                var abilityDef = new AbilityDef
                {
                    defName            = "CS_RT_" + key,
                    label              = modAbility.label ?? key,
                    description        = modAbility.description ?? string.Empty,
                    iconPath           = string.IsNullOrEmpty(modAbility.iconPath)
                                         ? "UI/Abilities/Shoot"   // 占位图标
                                         : modAbility.iconPath,
                    cooldownTicksRange = new IntRange(resolvedCd, resolvedCd),
                    charges            = modAbility.charges,
                    aiCanUse           = modAbility.aiCanUse > 0.5f
                };

                // 设置载体类型（含 warmupTime）
                VerbProperties verbProps;
                switch (modAbility.carrierType)
                {
                    case AbilityCarrierType.Self:
                        verbProps = BuildVerbProps_Self();
                        break;
                    case AbilityCarrierType.Touch:
                    case AbilityCarrierType.Target:
                        verbProps = BuildVerbProps_Target(modAbility.range);
                        break;
                    case AbilityCarrierType.Area:
                        verbProps = BuildVerbProps_Area(modAbility.range);
                        break;
                    case AbilityCarrierType.Projectile:
                        verbProps = BuildVerbProps_Target(modAbility.range);
                        break;
                    default:
                        verbProps = BuildVerbProps_Self();
                        break;
                }
                // 修复：将编辑器中的 warmupTicks 设置到 VerbProperties.warmupTime
                if (modAbility.warmupTicks > 0f)
                    verbProps.warmupTime = modAbility.warmupTicks / 60f; // 转换为秒
                abilityDef.verbProperties = verbProps;

                // 注入效果组件
                if (modAbility.effects != null && modAbility.effects.Count > 0)
                {
                    var compProps = new CompProperties_AbilityModular();
                    compProps.effects.AddRange(modAbility.effects.Select(e => e.Clone()));
                    abilityDef.comps = new List<AbilityCompProperties> { compProps };
                }

                // 注册到 DefDatabase（运行时注入）
                abilityDef.ResolveReferences();
                abilityDef.PostLoad();
                DefDatabase<AbilityDef>.Add(abilityDef);

                runtimeAbilityDefs[key] = abilityDef;
                return abilityDef;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 构建运行时 AbilityDef [{key}] 失败: {ex.Message}");
                return null;
            }
        }

        private static VerbProperties BuildVerbProps_Self()
            => new VerbProperties
            {
                verbClass    = typeof(Verb_CastAbility),
                range        = 0f,
                targetParams = new TargetingParameters { canTargetSelf = true, canTargetPawns = false, canTargetLocations = false }
            };

        private static VerbProperties BuildVerbProps_Target(float range)
            => new VerbProperties
            {
                verbClass    = typeof(Verb_CastAbility),
                range        = Mathf.Max(range, 1f),
                targetParams = new TargetingParameters { canTargetPawns = true, canTargetLocations = false }
            };

        private static VerbProperties BuildVerbProps_Area(float range)
            => new VerbProperties
            {
                verbClass    = typeof(Verb_CastAbility),
                range        = Mathf.Max(range, 1f),
                targetParams = new TargetingParameters { canTargetPawns = true, canTargetLocations = true }
            };

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


