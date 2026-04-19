using System;
using System.Collections.Generic;
using System.IO;
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
        public static event Action<Pawn, IReadOnlyCollection<string>>? AbilitiesGrantedGlobal;
        public static event Action<Pawn, IReadOnlyCollection<string>>? AbilitiesRevokedGlobal;

        // 追踪已授予给每个 Pawn 的 CS 技能 defName，用于撤销
        private static readonly Dictionary<int, HashSet<string>> grantedAbilityNames =
            new Dictionary<int, HashSet<string>>();

        // 动态生成的运行时 AbilityDef 缓存（按 ModularAbilityDef.defName）
        private static readonly Dictionary<string, AbilityDef> runtimeAbilityDefs =
            new Dictionary<string, AbilityDef>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> runtimeAbilityFingerprints =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ModularAbilityDef> runtimeAbilitySourceDefs =
            new Dictionary<string, ModularAbilityDef>(StringComparer.OrdinalIgnoreCase);

        /// <summary>所有 CS 技能的最小冷却时间（0.5s = 30 ticks），防止技能被连点刷屏</summary>
        private const int MinAbilityCooldownTicks = 30;

        // ─────────────────────────────────────────────
        // 公共入口
        // ─────────────────────────────────────────────

        /// <summary>
        /// 将技能列表授予 Pawn。
        /// </summary>
        public static void GrantAbilitiesToPawn(Pawn pawn, IEnumerable<ModularAbilityDef> abilities)
        {
            if (pawn == null || abilities == null)
                return;

            RevokeAllCSAbilitiesFromPawn(pawn);

            // 仅授予技能不应触发任何渲染树重建或全图图形刷新。
            // 这里显式避免通过授予链路波及皮肤/服装渲染状态；
            // 技能栏与运行时 Ability 会由原版 AbilityTracker / Gizmo 链路自行更新。
            if (pawn.abilities == null)
                return;

            var grantedSet = GetOrCreateGrantedSet(pawn);
            List<string> newlyGrantedAbilityNames = new List<string>();

            foreach (var modAbility in abilities)
            {
                if (modAbility == null || string.IsNullOrEmpty(modAbility.defName))
                    continue;

                try
                {
                    var abilityDef = GetOrBuildRuntimeAbilityDef(modAbility);
                    if (abilityDef == null) continue;

                    // 防止重复授予
                    if (pawn.abilities.GetAbility(abilityDef) != null) continue;

                    pawn.abilities.GainAbility(abilityDef);
                    grantedSet.Add(modAbility.defName);
                    newlyGrantedAbilityNames.Add(modAbility.defName);

                    if (Prefs.DevMode)
                        Log.Message($"[CharacterStudio] 已授予技能 {modAbility.label ?? modAbility.defName} 给 {pawn.LabelShort}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 授予技能 {modAbility.defName} 时出错: {ex.Message}");
                }
            }

            if (newlyGrantedAbilityNames.Count > 0)
            {
                AbilitiesGrantedGlobal?.Invoke(pawn, newlyGrantedAbilityNames);
            }
        }

        /// <summary>
        /// 将皮肤中的所有技能授予 Pawn
        /// 已有同名技能时跳过，不重复授予
        /// </summary>
        public static void GrantSkinAbilitiesToPawn(Pawn pawn, PawnSkinDef skin)
        {
            if (pawn == null || skin == null || skin.abilities == null || skin.abilities.Count == 0)
                return;

            GrantAbilitiesToPawn(pawn, skin.abilities);
        }

        /// <summary>
        /// 撤销所有由 CS 授予给 Pawn 的技能
        /// </summary>
        public static void RevokeAllCSAbilitiesFromPawn(Pawn pawn)
        {
            if (pawn == null) return;

            int pawnId = pawn.thingIDNumber;
            if (!grantedAbilityNames.TryGetValue(pawnId, out var grantedSet)) return;

            List<string> revokedAbilityNames = grantedSet.ToList();

            foreach (var defName in grantedSet.ToList())
            {
                try
                {
                    if (runtimeAbilityDefs.TryGetValue(defName, out var abilityDef))
                    {
                        var ability = pawn.abilities.GetAbility(abilityDef);
                        if (ability != null)
                        {
                            pawn.abilities.RemoveAbility(abilityDef);
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

            if (revokedAbilityNames.Count > 0)
            {
                AbilitiesRevokedGlobal?.Invoke(pawn, revokedAbilityNames);
            }
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

        /// <summary>
        /// 获取某运行时 AbilityDef 对应的原始模块化能力快照
        /// 用于 Gizmo/热键等运行时链路在缺少显式 loadout 时仍能恢复类型信息
        /// </summary>
        public static ModularAbilityDef? GetRuntimeAbilitySourceDef(string modAbilityDefName)
        {
            if (!runtimeAbilitySourceDefs.TryGetValue(modAbilityDefName, out var def))
            {
                return null;
            }

            return CopyAbilityDefForRuntimeCache(def);
        }

        public static void WarmupAllRuntimeAbilityDefs()
        {
            // 先从 Config 目录加载已保存的技能文件并注册到 DefDatabase，
            // 确保后续 RehydrateAbilities 能通过 DefDatabase 查找到自定义技能。
            LoadAndRegisterSavedAbilities();

            foreach (ModularAbilityDef modAbility in DefDatabase<ModularAbilityDef>.AllDefsListForReading)
            {
                if (modAbility == null || string.IsNullOrWhiteSpace(modAbility.defName))
                {
                    continue;
                }

                GetOrBuildRuntimeAbilityDef(modAbility);
            }
        }

        /// <summary>
        /// 从 Config/CharacterStudio/Abilities/ 目录加载已保存的技能 XML 文件，
        /// 将其中的 ModularAbilityDef 注册到 DefDatabase。
        /// 这确保重启游戏后读档时，RehydrateAbilities 能通过 DefDatabase 查找到自定义技能。
        /// 
        /// 编辑器会话 XML 结构：
        /// &lt;Defs&gt;
        ///   &lt;CharacterStudio.Core.PawnSkinDef&gt;
        ///     &lt;abilities&gt;
        ///       &lt;li&gt;
        ///         &lt;defName&gt;CS_PureCode_StormBurst&lt;/defName&gt;
        ///         &lt;label&gt;...&lt;/label&gt;
        ///         &lt;cooldownTicks&gt;...&lt;/cooldownTicks&gt;
        ///         ...
        ///       &lt;/li&gt;
        ///     &lt;/abilities&gt;
        ///   &lt;/CharacterStudio.Core.PawnSkinDef&gt;
        /// &lt;/Defs&gt;
        /// </summary>
        private static void LoadAndRegisterSavedAbilities()
        {
            string abilitiesDir = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Abilities");
            if (!Directory.Exists(abilitiesDir))
                return;

            int registered = 0;

            foreach (string filePath in Directory.GetFiles(abilitiesDir, "*.xml"))
            {
                try
                {
                    var xmlDoc = new System.Xml.XmlDocument();
                    xmlDoc.Load(filePath);
                    System.Xml.XmlNode? root = xmlDoc.DocumentElement;
                    if (root == null)
                        continue;

                    // 查找 <abilities> 下的所有 <li> 节点（编辑器会话格式）
                    var abilityLiNodes = root.SelectNodes("//abilities/li");
                    if (abilityLiNodes != null)
                    {
                        foreach (System.Xml.XmlNode liNode in abilityLiNodes)
                        {
                            if (liNode == null) continue;
                            try
                            {
                                var ability = DirectXmlToObject.ObjectFromXml<ModularAbilityDef>(liNode, false);
                                if (ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                                {
                                    RegisterModularAbilityDefIfMissing(ability);
                                    registered++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"[CharacterStudio] 加载技能(li)从 {Path.GetFileName(filePath)} 失败: {ex.Message}");
                            }
                        }
                    }

                    // 也查找直接用类全名包装的节点（导出格式）
                    string modAbilityNodeName = typeof(ModularAbilityDef).FullName!;
                    var typedNodes = root.SelectNodes($"//{modAbilityNodeName}");
                    if (typedNodes != null)
                    {
                        foreach (System.Xml.XmlNode typedNode in typedNodes)
                        {
                            if (typedNode == null) continue;
                            try
                            {
                                var ability = DirectXmlToObject.ObjectFromXml<ModularAbilityDef>(typedNode, true);
                                if (ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                                {
                                    RegisterModularAbilityDefIfMissing(ability);
                                    registered++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"[CharacterStudio] 加载技能(typed)从 {Path.GetFileName(filePath)} 失败: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 读取技能文件 {Path.GetFileName(filePath)} 失败: {ex.Message}");
                }
            }

            if (registered > 0 && Prefs.DevMode)
                Log.Message($"[CharacterStudio] 从 Config 目录预加载了 {registered} 个 ModularAbilityDef");
        }

        // ─────────────────────────────────────────────
        // 运行时 AbilityDef 构建
        // ─────────────────────────────────────────────

        private static AbilityDef? GetOrBuildRuntimeAbilityDef(ModularAbilityDef modAbility)
        {
            string key = modAbility.defName;
            string fingerprint = BuildAbilityFingerprint(modAbility);

            // 确保 ModularAbilityDef 在 DefDatabase 中注册（所有路径都需要，不仅仅是新建），
            // 以便存档加载时 RehydrateAbilities 能通过 DefDatabase 查找恢复。
            RegisterModularAbilityDefIfMissing(modAbility);

            if (runtimeAbilityDefs.TryGetValue(key, out var cached))
            {
                if (runtimeAbilityFingerprints.TryGetValue(key, out var oldFingerprint) && oldFingerprint == fingerprint)
                {
                    CacheRuntimeAbilitySource(key, modAbility);
                    return cached;
                }

                try
                {
                    ConfigureRuntimeAbilityDef(cached, modAbility, key);
                    CacheRuntimeAbilitySource(key, modAbility);
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

                // 确保 ModularAbilityDef 在 DefDatabase 中注册，
                // 以便存档加载时 RehydrateAbilities 能通过 DefDatabase 查找恢复。
                RegisterModularAbilityDefIfMissing(modAbility);

                var abilityDef = new AbilityDef();
                ConfigureRuntimeAbilityDef(abilityDef, modAbility, key, resolvedCd);
                DefDatabase<AbilityDef>.Add(abilityDef);

                runtimeAbilityDefs[key] = abilityDef;
                runtimeAbilityFingerprints[key] = fingerprint;
                CacheRuntimeAbilitySource(key, modAbility);
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

            bool usesJumpVerb = modAbility.runtimeComponents != null
                && modAbility.runtimeComponents.Any(c => c != null && c.enabled
                    && (c.type == AbilityRuntimeComponentType.SmartJump
                        || c.type == AbilityRuntimeComponentType.EShortJump));

            VerbProperties verbProps = normalizedCarrier switch
            {
                AbilityCarrierType.Self => BuildVerbProps_Self(),
                AbilityCarrierType.Target => BuildVerbProps_Target(modAbility.range, normalizedTarget, usesJumpVerb),
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
                var compProps = new CompProperties_AbilityModular
                {
                    carrierType = normalizedCarrier,
                    targetType = normalizedTarget,
                    useRadius = modAbility.useRadius,
                    areaCenter = ModularAbilityDefExtensions.NormalizeAreaCenter(modAbility),
                    areaShape = ModularAbilityDefExtensions.NormalizeAreaShape(modAbility),
                    irregularAreaPattern = modAbility.irregularAreaPattern ?? string.Empty,
                    range = modAbility.range,
                    radius = modAbility.radius,
                    projectileDef = modAbility.projectileDef
                };

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

        private static void CacheRuntimeAbilitySource(string key, ModularAbilityDef modAbility)
        {
            runtimeAbilitySourceDefs[key] = CopyAbilityDefForRuntimeCache(modAbility);
        }

        /// <summary>
        /// 将 ModularAbilityDef 注册到 DefDatabase（如果尚未注册）。
        /// 这确保存档加载时 RehydrateAbilities 能通过 DefDatabase 查找到对应的 def。
        /// </summary>
        private static void RegisterModularAbilityDefIfMissing(ModularAbilityDef modAbility)
        {
            if (modAbility == null || string.IsNullOrWhiteSpace(modAbility.defName))
                return;

            if (DefDatabase<ModularAbilityDef>.GetNamedSilentFail(modAbility.defName) != null)
                return;

            try
            {
                DefDatabase<ModularAbilityDef>.Add(modAbility);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 注册 ModularAbilityDef [{modAbility.defName}] 到 DefDatabase 失败: {ex.Message}");
            }
        }

        private static ModularAbilityDef CopyAbilityDefForRuntimeCache(ModularAbilityDef source)
        {
            var copy = new ModularAbilityDef
            {
                defName = source.defName,
                label = source.label,
                description = source.description,
                iconPath = source.iconPath,
                cooldownTicks = source.cooldownTicks,
                warmupTicks = source.warmupTicks,
                charges = source.charges,
                aiCanUse = source.aiCanUse,
                carrierType = source.carrierType,
                targetType = source.targetType,
                useRadius = source.useRadius,
                areaCenter = source.areaCenter,
                areaShape = source.areaShape,
                irregularAreaPattern = source.irregularAreaPattern,
                range = source.range,
                radius = source.radius,
                projectileDef = source.projectileDef
            };

            if (source.effects != null)
            {
                copy.effects.AddRange(source.effects.Where(e => e != null).Select(e => e.Clone()));
            }

            if (source.visualEffects != null)
            {
                copy.visualEffects.AddRange(source.visualEffects.Where(v => v != null).Select(v => v.Clone()));
            }

            if (source.runtimeComponents != null)
            {
                copy.runtimeComponents.AddRange(source.runtimeComponents.Where(c => c != null).Select(c => c.Clone()));
            }

            return copy;
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
                modAbility.areaShape.ToString(),
                modAbility.irregularAreaPattern ?? string.Empty,
                modAbility.range.ToString("F3"),
                modAbility.radius.ToString("F3"),
                modAbility.projectileDef?.defName ?? string.Empty
            };

            if (modAbility.effects != null)
            {
                foreach (var effect in modAbility.effects)
                {
                    if (effect == null) continue;
                    parts.Add($"E:{effect.type}|{effect.amount:F3}|{effect.duration:F3}|{effect.chance:F3}|{effect.damageDef?.defName}|{effect.hediffDef?.defName}|{effect.summonKind?.defName}|{effect.summonCount}|{effect.summonFactionType}|{effect.summonFactionDefName}|{effect.summonFactionDef?.defName}|{effect.controlMode}|{effect.controlMoveDistance}|{effect.terraformMode}|{effect.terraformThingDef?.defName}|{effect.terraformTerrainDef?.defName}|{effect.terraformSpawnCount}|{effect.canHurtSelf}");
                }
            }

            if (modAbility.visualEffects != null)
            {
                foreach (var vfx in modAbility.visualEffects)
                {
                    if (vfx == null) continue;
                    vfx.NormalizeLegacyData();
                    vfx.SyncLegacyFields();
                    parts.Add($"V:{vfx.type}|{vfx.sourceMode}|{vfx.textureSource}|{vfx.presetDefName}|{vfx.customTexturePath}|{vfx.target}|{vfx.trigger}|{vfx.delayTicks}|{vfx.displayDurationTicks}|{vfx.linkedExpression}|{vfx.linkedExpressionDurationTicks}|{vfx.linkedPupilBrightnessOffset:F3}|{vfx.linkedPupilContrastOffset:F3}|{vfx.scale:F3}|{vfx.drawSize:F3}|{vfx.useCasterFacing}|{vfx.forwardOffset:F3}|{vfx.sideOffset:F3}|{vfx.heightOffset:F3}|{vfx.rotation:F3}|{vfx.textureScale.x:F3},{vfx.textureScale.y:F3}|{vfx.repeatCount}|{vfx.repeatIntervalTicks}|{vfx.offset.x:F3},{vfx.offset.y:F3},{vfx.offset.z:F3}|{vfx.playSound}|{vfx.soundDefName}|{vfx.soundDelayTicks}|{vfx.soundVolume:F3}|{vfx.soundPitch:F3}|{vfx.attachToPawn}|{vfx.attachToTargetCell}|{vfx.enabled}");
                }
            }

            if (modAbility.runtimeComponents != null)
            {
                foreach (var component in modAbility.runtimeComponents)
                {
                    if (component == null) continue;
                    parts.Add($"R:{component.type}|{component.enabled}|{component.comboWindowTicks}|{component.cooldownTicks}|{component.jumpDistance}|{component.findCellRadius}|{component.triggerAbilityEffectsAfterJump}|{component.useMouseTargetCell}|{component.smartCastOffsetCells}|{component.smartCastClampToMaxDistance}|{component.smartCastAllowFallbackForward}|{component.overrideHotkeySlot}|{component.overrideAbilityDefName}|{component.overrideDurationTicks}|{component.followupCooldownHotkeySlot}|{component.followupCooldownTicks}|{component.requiredStacks}|{component.delayTicks}|{component.wave1Radius:F3}|{component.wave1Damage:F3}|{component.wave2Radius:F3}|{component.wave2Damage:F3}|{component.wave3Radius:F3}|{component.wave3Damage:F3}|{component.waveDamageDef?.defName}|{component.pulseIntervalTicks}|{component.pulseTotalTicks}|{component.pulseStartsImmediately}|{component.killRefreshHotkeySlot}|{component.killRefreshCooldownPercent:F3}|{component.shieldMaxDamage:F3}|{component.shieldDurationTicks:F3}|{component.shieldHealRatio:F3}|{component.shieldBonusDamageRatio:F3}|{component.maxBounceCount}|{component.bounceRange:F3}|{component.bounceDamageFalloff:F3}|{component.executeThresholdPercent:F3}|{component.executeBonusDamageScale:F3}|{component.missingHealthBonusPerTenPercent:F3}|{component.missingHealthBonusMaxScale:F3}|{component.fullHealthThresholdPercent:F3}|{component.fullHealthBonusDamageScale:F3}|{component.nearbyEnemyBonusMaxTargets}|{component.nearbyEnemyBonusPerTarget:F3}|{component.nearbyEnemyBonusRadius:F3}|{component.isolatedTargetRadius:F3}|{component.isolatedTargetBonusDamageScale:F3}|{component.markDurationTicks}|{component.markMaxStacks}|{component.markDetonationDamage:F3}|{component.markDamageDef?.defName}|{component.comboStackWindowTicks}|{component.comboStackMax}|{component.comboStackBonusDamagePerStack:F3}|{component.slowFieldDurationTicks}|{component.slowFieldRadius:F3}|{component.slowFieldHediffDefName}|{component.pierceMaxTargets}|{component.pierceBonusDamagePerTarget:F3}|{component.pierceSearchRange:F3}|{component.dashEmpowerDurationTicks}|{component.dashEmpowerBonusDamageScale:F3}|{component.hitHealAmount:F3}|{component.hitHealRatio:F3}|{component.refundHotkeySlot}|{component.hitCooldownRefundPercent:F3}|{component.splitProjectileCount}|{component.splitDamageScale:F3}|{component.splitSearchRange:F3}|{component.flightDurationTicks}|{component.flightHeightFactor:F3}");
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

        private static VerbProperties BuildVerbProps_Target(float range, AbilityTargetType targetType, bool useJumpVerb = false)
            => new VerbProperties
            {
                verbClass    = useJumpVerb ? typeof(Verb_CastAbilityStraightJump) : typeof(Verb_CastAbility),
                range        = Mathf.Max(range, 1f),
                targetParams = BuildTargetingParameters(targetType)
            };

        /// <summary>
        /// 构建投射物载体的 VerbProperties
        /// 使用 Verb_CastAbility 以支持完整的技能效果流水线。
        /// 实际的投射物发射逻辑由 CompAbilityEffect_Modular 接管。
        /// </summary>
        private static VerbProperties BuildVerbProps_Projectile(float range, AbilityTargetType targetType, ThingDef? projectileDef)
        {
            return new VerbProperties
            {
                verbClass         = typeof(Verb_CastAbility),
                range             = Mathf.Max(range, 1f),
                targetParams      = BuildTargetingParameters(targetType)
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
