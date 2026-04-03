using System;
using System.Collections.Generic;
using System.IO;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private static string lastImportedAbilityXmlPath = string.Empty;
        private static string lastExportedAbilityXmlPath = string.Empty;

        private static string GetAbilityExportDir()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Abilities");
        }

        private static string GetDefaultAbilityExportFilePath()
        {
            return Path.Combine(GetAbilityExportDir(), "AbilityEditor_Export.xml");
        }

        private void OpenImportXmlDialog()
        {
            string initialPath = !string.IsNullOrWhiteSpace(lastImportedAbilityXmlPath)
                ? lastImportedAbilityXmlPath
                : (!string.IsNullOrWhiteSpace(lastExportedAbilityXmlPath) ? lastExportedAbilityXmlPath : GetDefaultAbilityExportFilePath());

            Find.WindowStack.Add(new Dialog_FileBrowser(GetAbilityImportBrowseStartPath(initialPath), selectedPath =>
            {
                string normalizedPath = selectedPath?.Trim().Trim('"') ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    return;
                }

                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("CS_Studio_Ability_ImportReplace".Translate(), () => ImportAbilitiesFromXmlPath(normalizedPath, true)),
                    new FloatMenuOption("CS_Studio_Ability_ImportAppend".Translate(), () => ImportAbilitiesFromXmlPath(normalizedPath, false)),
                    new FloatMenuOption("CS_Studio_Btn_Cancel".Translate(), () => { })
                }));
            }, "*.xml"));
        }

        private static string GetAbilityImportBrowseStartPath(string initialPath)
        {
            if (string.IsNullOrWhiteSpace(initialPath))
            {
                return GetAbilityExportDir();
            }

            string normalizedPath = initialPath.Trim().Trim('"');
            if (Directory.Exists(normalizedPath))
            {
                return normalizedPath;
            }

            if (File.Exists(normalizedPath))
            {
                string? directory = Path.GetDirectoryName(normalizedPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
            }

            return GetAbilityExportDir();
        }

        private void ExportAbilitiesToDefaultPath()
        {
            try
            {
                string exportDir = GetAbilityExportDir();
                Directory.CreateDirectory(exportDir);
                string exportPath = GetDefaultAbilityExportFilePath();
                var doc = CreateAbilitiesDocument(abilities, GetCurrentHotkeyConfig());
                doc.Save(exportPath);
                lastExportedAbilityXmlPath = exportPath;
                validationSummary = "CS_Studio_Ability_ExportSuccess".Translate(exportPath);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 技能 XML 导出失败: {ex}");
                validationSummary = "CS_Studio_Ability_ExportFailed".Translate(ex.Message);
            }
        }

        private void SaveAbilityEditorSessionToDisk()
        {
            try
            {
                string exportDir = GetAbilityExportDir();
                Directory.CreateDirectory(exportDir);
                string sessionPath = Path.Combine(exportDir, "AbilityEditor_LastSession.xml");
                CreateAbilitiesDocument(abilities, GetCurrentHotkeyConfig()).Save(sessionPath);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 持久化技能编辑器列表失败: {ex.Message}");
            }
        }

        private static List<ModularAbilityDef> TryLoadAbilityEditorSessionFromDisk()
        {
            return TryLoadAbilityEditorSessionFromDisk(out _);
        }

        private static List<ModularAbilityDef> TryLoadAbilityEditorSessionFromDisk(out SkinAbilityHotkeyConfig? hotkeys)
        {
            try
            {
                string sessionPath = Path.Combine(GetAbilityExportDir(), "AbilityEditor_LastSession.xml");
                if (!File.Exists(sessionPath))
                {
                    hotkeys = null;
                    return new List<ModularAbilityDef>();
                }

                return LoadAbilitiesFromXmlFile(sessionPath, out hotkeys);
            }
            catch
            {
                hotkeys = null;
                return new List<ModularAbilityDef>();
            }
        }

        private static System.Xml.Linq.XDocument CreateAbilitiesDocument(List<ModularAbilityDef> abilityList, SkinAbilityHotkeyConfig? hotkeys = null)
        {
            var defs = new System.Xml.Linq.XElement("Defs");
            var skinRoot = new System.Xml.Linq.XElement(nameof(PawnSkinDef));
            var abilitiesEl = new System.Xml.Linq.XElement("abilities");

            if (abilityList != null)
            {
                foreach (var ability in abilityList)
                {
                    if (ability == null || string.IsNullOrWhiteSpace(ability.defName))
                        continue;

                    var abilityEl = new System.Xml.Linq.XElement("li",
                        new System.Xml.Linq.XElement("defName", ability.defName),
                        !string.IsNullOrWhiteSpace(ability.label) ? new System.Xml.Linq.XElement("label", ability.label) : null,
                        !string.IsNullOrWhiteSpace(ability.description) ? new System.Xml.Linq.XElement("description", ability.description) : null,
                        !string.IsNullOrWhiteSpace(ability.iconPath) ? new System.Xml.Linq.XElement("iconPath", ability.iconPath) : null,
                        new System.Xml.Linq.XElement("cooldownTicks", ability.cooldownTicks),
                        new System.Xml.Linq.XElement("warmupTicks", ability.warmupTicks),
                        new System.Xml.Linq.XElement("charges", ability.charges),
                        new System.Xml.Linq.XElement("aiCanUse", ability.aiCanUse),
                        new System.Xml.Linq.XElement("carrierType", ability.carrierType),
                        new System.Xml.Linq.XElement("targetType", ability.targetType),
                        new System.Xml.Linq.XElement("useRadius", ability.useRadius.ToString().ToLowerInvariant()),
                        new System.Xml.Linq.XElement("areaCenter", ability.areaCenter),
                        new System.Xml.Linq.XElement("areaShape", ability.areaShape),
                        !string.IsNullOrWhiteSpace(ability.irregularAreaPattern) ? new System.Xml.Linq.XElement("irregularAreaPattern", ability.irregularAreaPattern) : null,
                        new System.Xml.Linq.XElement("range", ability.range),
                        new System.Xml.Linq.XElement("radius", ability.radius),
                        ability.projectileDef != null ? new System.Xml.Linq.XElement("projectileDef", ability.projectileDef.defName) : null,
                        GenerateEffectsXml(ability.effects),
                        GenerateVisualEffectsXml(ability.visualEffects),
                        GenerateRuntimeComponentsXml(ability.runtimeComponents)
                    );

                    abilitiesEl.Add(abilityEl);
                }
            }

            skinRoot.Add(abilitiesEl);
            skinRoot.Add(GenerateAbilityHotkeysXml(hotkeys));
            defs.Add(skinRoot);
            return new System.Xml.Linq.XDocument(defs);
        }

        private static System.Xml.Linq.XElement? GenerateEffectsXml(List<AbilityEffectConfig>? effects)
        {
            if (effects == null || effects.Count == 0) return null;

            var effectsEl = new System.Xml.Linq.XElement("effects");
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                effectsEl.Add(new System.Xml.Linq.XElement("li",
                    new System.Xml.Linq.XElement("type", effect.type.ToString()),
                    new System.Xml.Linq.XElement("amount", effect.amount),
                    new System.Xml.Linq.XElement("duration", effect.duration),
                    new System.Xml.Linq.XElement("chance", effect.chance),
                    effect.damageDef != null ? new System.Xml.Linq.XElement("damageDef", effect.damageDef.defName) : null,
                    effect.hediffDef != null ? new System.Xml.Linq.XElement("hediffDef", effect.hediffDef.defName) : null,
                    effect.summonKind != null ? new System.Xml.Linq.XElement("summonKind", effect.summonKind.defName) : null,
                    effect.summonFactionDef != null ? new System.Xml.Linq.XElement("summonFactionDef", effect.summonFactionDef.defName) : null,
                    new System.Xml.Linq.XElement("summonCount", effect.summonCount),
                    new System.Xml.Linq.XElement("controlMode", effect.controlMode.ToString()),
                    new System.Xml.Linq.XElement("controlMoveDistance", effect.controlMoveDistance),
                    new System.Xml.Linq.XElement("terraformMode", effect.terraformMode.ToString()),
                    effect.terraformThingDef != null ? new System.Xml.Linq.XElement("terraformThingDef", effect.terraformThingDef.defName) : null,
                    effect.terraformTerrainDef != null ? new System.Xml.Linq.XElement("terraformTerrainDef", effect.terraformTerrainDef.defName) : null,
                    new System.Xml.Linq.XElement("terraformSpawnCount", effect.terraformSpawnCount),
                    new System.Xml.Linq.XElement("canHurtSelf", effect.canHurtSelf.ToString().ToLowerInvariant())
                ));
            }
            return effectsEl;
        }

        private static System.Xml.Linq.XElement? GenerateVisualEffectsXml(List<AbilityVisualEffectConfig>? visualEffects)
        {
            if (visualEffects == null || visualEffects.Count == 0) return null;

            var root = new System.Xml.Linq.XElement("visualEffects");
            foreach (var vfx in visualEffects)
            {
                if (vfx == null) continue;
                vfx.NormalizeLegacyData();
                vfx.SyncLegacyFields();
                root.Add(new System.Xml.Linq.XElement("li",
                    new System.Xml.Linq.XElement("type", vfx.type.ToString()),
                    new System.Xml.Linq.XElement("sourceMode", vfx.sourceMode.ToString()),
                    vfx.UsesCustomTextureType ? new System.Xml.Linq.XElement("textureSource", vfx.textureSource.ToString()) : null,
                    !string.IsNullOrEmpty(vfx.presetDefName) ? new System.Xml.Linq.XElement("presetDefName", vfx.presetDefName) : null,
                    !string.IsNullOrEmpty(vfx.customTexturePath) ? new System.Xml.Linq.XElement("customTexturePath", vfx.customTexturePath) : null,
                    new System.Xml.Linq.XElement("target", vfx.target.ToString()),
                    new System.Xml.Linq.XElement("trigger", vfx.trigger.ToString()),
                    new System.Xml.Linq.XElement("delayTicks", vfx.delayTicks),
                    new System.Xml.Linq.XElement("displayDurationTicks", vfx.displayDurationTicks),
                    vfx.linkedExpression.HasValue ? new System.Xml.Linq.XElement("linkedExpression", vfx.linkedExpression.Value.ToString()) : null,
                    new System.Xml.Linq.XElement("linkedExpressionDurationTicks", vfx.linkedExpressionDurationTicks),
                    new System.Xml.Linq.XElement("linkedPupilBrightnessOffset", vfx.linkedPupilBrightnessOffset),
                    new System.Xml.Linq.XElement("linkedPupilContrastOffset", vfx.linkedPupilContrastOffset),
                    new System.Xml.Linq.XElement("scale", vfx.scale),
                    new System.Xml.Linq.XElement("drawSize", vfx.drawSize),
                    new System.Xml.Linq.XElement("useCasterFacing", vfx.useCasterFacing.ToString().ToLower()),
                    new System.Xml.Linq.XElement("forwardOffset", vfx.forwardOffset),
                    new System.Xml.Linq.XElement("sideOffset", vfx.sideOffset),
                    new System.Xml.Linq.XElement("heightOffset", vfx.heightOffset),
                    new System.Xml.Linq.XElement("rotation", vfx.rotation),
                    vfx.textureScale != UnityEngine.Vector2.one ? new System.Xml.Linq.XElement("textureScale", $"({vfx.textureScale.x:F3}, {vfx.textureScale.y:F3})") : null,
                    new System.Xml.Linq.XElement("repeatCount", vfx.repeatCount),
                    new System.Xml.Linq.XElement("repeatIntervalTicks", vfx.repeatIntervalTicks),
                    new System.Xml.Linq.XElement("playSound", vfx.playSound.ToString().ToLowerInvariant()),
                    !string.IsNullOrWhiteSpace(vfx.soundDefName) ? new System.Xml.Linq.XElement("soundDefName", vfx.soundDefName) : null,
                    new System.Xml.Linq.XElement("soundDelayTicks", vfx.soundDelayTicks),
                    new System.Xml.Linq.XElement("soundVolume", vfx.soundVolume),
                    new System.Xml.Linq.XElement("soundPitch", vfx.soundPitch),
                    new System.Xml.Linq.XElement("enabled", vfx.enabled.ToString().ToLower())
                ));
            }
            return root;
        }

        private static System.Xml.Linq.XElement? GenerateRuntimeComponentsXml(List<AbilityRuntimeComponentConfig>? components)
        {
            if (components == null || components.Count == 0) return null;

            var root = new System.Xml.Linq.XElement("runtimeComponents");
            foreach (var component in components)
            {
                if (component == null) continue;
                root.Add(new System.Xml.Linq.XElement("li",
                    new System.Xml.Linq.XElement("type", component.type.ToString()),
                    new System.Xml.Linq.XElement("enabled", component.enabled.ToString().ToLower()),
                    new System.Xml.Linq.XElement("comboWindowTicks", component.comboWindowTicks),
                    new System.Xml.Linq.XElement("cooldownTicks", component.cooldownTicks),
                    new System.Xml.Linq.XElement("jumpDistance", component.jumpDistance),
                    new System.Xml.Linq.XElement("findCellRadius", component.findCellRadius),
                    new System.Xml.Linq.XElement("triggerAbilityEffectsAfterJump", component.triggerAbilityEffectsAfterJump.ToString().ToLower()),
                    new System.Xml.Linq.XElement("useMouseTargetCell", component.useMouseTargetCell.ToString().ToLower()),
                    new System.Xml.Linq.XElement("smartCastOffsetCells", component.smartCastOffsetCells),
                    new System.Xml.Linq.XElement("smartCastClampToMaxDistance", component.smartCastClampToMaxDistance.ToString().ToLower()),
                    new System.Xml.Linq.XElement("smartCastAllowFallbackForward", component.smartCastAllowFallbackForward.ToString().ToLower()),
                    new System.Xml.Linq.XElement("overrideHotkeySlot", component.overrideHotkeySlot.ToString()),
                    new System.Xml.Linq.XElement("overrideAbilityDefName", component.overrideAbilityDefName ?? string.Empty),
                    new System.Xml.Linq.XElement("overrideDurationTicks", component.overrideDurationTicks),
                    new System.Xml.Linq.XElement("followupCooldownHotkeySlot", component.followupCooldownHotkeySlot.ToString()),
                    new System.Xml.Linq.XElement("followupCooldownTicks", component.followupCooldownTicks),
                    new System.Xml.Linq.XElement("requiredStacks", component.requiredStacks),
                    new System.Xml.Linq.XElement("delayTicks", component.delayTicks),
                    new System.Xml.Linq.XElement("wave1Radius", component.wave1Radius),
                    new System.Xml.Linq.XElement("wave1Damage", component.wave1Damage),
                    new System.Xml.Linq.XElement("wave2Radius", component.wave2Radius),
                    new System.Xml.Linq.XElement("wave2Damage", component.wave2Damage),
                    new System.Xml.Linq.XElement("wave3Radius", component.wave3Radius),
                    new System.Xml.Linq.XElement("wave3Damage", component.wave3Damage),
                    component.waveDamageDef != null ? new System.Xml.Linq.XElement("waveDamageDef", component.waveDamageDef.defName) : null,
                    new System.Xml.Linq.XElement("pulseIntervalTicks", component.pulseIntervalTicks),
                    new System.Xml.Linq.XElement("pulseTotalTicks", component.pulseTotalTicks),
                    new System.Xml.Linq.XElement("pulseStartsImmediately", component.pulseStartsImmediately.ToString().ToLower()),
                    new System.Xml.Linq.XElement("killRefreshHotkeySlot", component.killRefreshHotkeySlot.ToString()),
                    new System.Xml.Linq.XElement("killRefreshCooldownPercent", component.killRefreshCooldownPercent),
                    new System.Xml.Linq.XElement("shieldMaxDamage", component.shieldMaxDamage),
                    new System.Xml.Linq.XElement("shieldDurationTicks", component.shieldDurationTicks),
                    new System.Xml.Linq.XElement("shieldHealRatio", component.shieldHealRatio),
                    new System.Xml.Linq.XElement("shieldBonusDamageRatio", component.shieldBonusDamageRatio),
                    new System.Xml.Linq.XElement("maxBounceCount", component.maxBounceCount),
                    new System.Xml.Linq.XElement("bounceRange", component.bounceRange),
                    new System.Xml.Linq.XElement("bounceDamageFalloff", component.bounceDamageFalloff),
                    new System.Xml.Linq.XElement("executeThresholdPercent", component.executeThresholdPercent),
                    new System.Xml.Linq.XElement("executeBonusDamageScale", component.executeBonusDamageScale),
                    new System.Xml.Linq.XElement("missingHealthBonusPerTenPercent", component.missingHealthBonusPerTenPercent),
                    new System.Xml.Linq.XElement("missingHealthBonusMaxScale", component.missingHealthBonusMaxScale),
                    new System.Xml.Linq.XElement("fullHealthThresholdPercent", component.fullHealthThresholdPercent),
                    new System.Xml.Linq.XElement("fullHealthBonusDamageScale", component.fullHealthBonusDamageScale),
                    new System.Xml.Linq.XElement("nearbyEnemyBonusMaxTargets", component.nearbyEnemyBonusMaxTargets),
                    new System.Xml.Linq.XElement("nearbyEnemyBonusPerTarget", component.nearbyEnemyBonusPerTarget),
                    new System.Xml.Linq.XElement("nearbyEnemyBonusRadius", component.nearbyEnemyBonusRadius),
                    new System.Xml.Linq.XElement("isolatedTargetRadius", component.isolatedTargetRadius),
                    new System.Xml.Linq.XElement("isolatedTargetBonusDamageScale", component.isolatedTargetBonusDamageScale),
                    new System.Xml.Linq.XElement("markDurationTicks", component.markDurationTicks),
                    new System.Xml.Linq.XElement("markMaxStacks", component.markMaxStacks),
                    new System.Xml.Linq.XElement("markDetonationDamage", component.markDetonationDamage),
                    component.markDamageDef != null ? new System.Xml.Linq.XElement("markDamageDef", component.markDamageDef.defName) : null,
                    new System.Xml.Linq.XElement("comboStackWindowTicks", component.comboStackWindowTicks),
                    new System.Xml.Linq.XElement("comboStackMax", component.comboStackMax),
                    new System.Xml.Linq.XElement("comboStackBonusDamagePerStack", component.comboStackBonusDamagePerStack),
                    new System.Xml.Linq.XElement("slowFieldDurationTicks", component.slowFieldDurationTicks),
                    new System.Xml.Linq.XElement("slowFieldRadius", component.slowFieldRadius),
                    new System.Xml.Linq.XElement("slowFieldHediffDefName", component.slowFieldHediffDefName ?? string.Empty),
                    new System.Xml.Linq.XElement("pierceMaxTargets", component.pierceMaxTargets),
                    new System.Xml.Linq.XElement("pierceBonusDamagePerTarget", component.pierceBonusDamagePerTarget),
                    new System.Xml.Linq.XElement("pierceSearchRange", component.pierceSearchRange),
                    new System.Xml.Linq.XElement("dashEmpowerDurationTicks", component.dashEmpowerDurationTicks),
                    new System.Xml.Linq.XElement("dashEmpowerBonusDamageScale", component.dashEmpowerBonusDamageScale),
                    new System.Xml.Linq.XElement("hitHealAmount", component.hitHealAmount),
                    new System.Xml.Linq.XElement("hitHealRatio", component.hitHealRatio),
                    new System.Xml.Linq.XElement("refundHotkeySlot", component.refundHotkeySlot.ToString()),
                    new System.Xml.Linq.XElement("hitCooldownRefundPercent", component.hitCooldownRefundPercent),
                    new System.Xml.Linq.XElement("splitProjectileCount", component.splitProjectileCount),
                    new System.Xml.Linq.XElement("splitDamageScale", component.splitDamageScale),
                    new System.Xml.Linq.XElement("splitSearchRange", component.splitSearchRange),
                    new System.Xml.Linq.XElement("flightDurationTicks", component.flightDurationTicks),
                    new System.Xml.Linq.XElement("flightHeightFactor", component.flightHeightFactor),
                    new System.Xml.Linq.XElement("suppressCombatActionsDuringFlightState", component.suppressCombatActionsDuringFlightState.ToString().ToLower()),
                    new System.Xml.Linq.XElement("flyerThingDefName", component.flyerThingDefName ?? string.Empty),
                    new System.Xml.Linq.XElement("flyerWarmupTicks", component.flyerWarmupTicks),
                    new System.Xml.Linq.XElement("launchFromCasterPosition", component.launchFromCasterPosition.ToString().ToLower()),
                    new System.Xml.Linq.XElement("requireValidTargetCell", component.requireValidTargetCell.ToString().ToLower()),
                    new System.Xml.Linq.XElement("storeTargetForFollowup", component.storeTargetForFollowup.ToString().ToLower()),
                    new System.Xml.Linq.XElement("enableFlightOnlyWindow", component.enableFlightOnlyWindow.ToString().ToLower()),
                    new System.Xml.Linq.XElement("flightOnlyWindowTicks", component.flightOnlyWindowTicks),
                    new System.Xml.Linq.XElement("flightOnlyAbilityDefName", component.flightOnlyAbilityDefName ?? string.Empty),
                    new System.Xml.Linq.XElement("hideCasterDuringTakeoff", component.hideCasterDuringTakeoff.ToString().ToLower()),
                    new System.Xml.Linq.XElement("autoExpireFlightMarkerOnLanding", component.autoExpireFlightMarkerOnLanding.ToString().ToLower()),
                    new System.Xml.Linq.XElement("requiredFlightSourceAbilityDefName", component.requiredFlightSourceAbilityDefName ?? string.Empty),
                    new System.Xml.Linq.XElement("requireReservedTargetCell", component.requireReservedTargetCell.ToString().ToLower()),
                    new System.Xml.Linq.XElement("consumeFlightStateOnCast", component.consumeFlightStateOnCast.ToString().ToLower()),
                    new System.Xml.Linq.XElement("onlyUseDuringFlightWindow", component.onlyUseDuringFlightWindow.ToString().ToLower()),
                    new System.Xml.Linq.XElement("landingBurstRadius", component.landingBurstRadius),
                    new System.Xml.Linq.XElement("landingBurstDamage", component.landingBurstDamage),
                    component.landingBurstDamageDef != null ? new System.Xml.Linq.XElement("landingBurstDamageDef", component.landingBurstDamageDef.defName) : null,
                    new System.Xml.Linq.XElement("landingEffecterDefName", component.landingEffecterDefName ?? string.Empty),
                    new System.Xml.Linq.XElement("landingSoundDefName", component.landingSoundDefName ?? string.Empty),
                    new System.Xml.Linq.XElement("affectBuildings", component.affectBuildings.ToString().ToLower()),
                    new System.Xml.Linq.XElement("affectCells", component.affectCells.ToString().ToLower()),
                    new System.Xml.Linq.XElement("knockbackTargets", component.knockbackTargets.ToString().ToLower()),
                    new System.Xml.Linq.XElement("knockbackDistance", component.knockbackDistance)
                ));
            }
            return root;
        }

        private static System.Xml.Linq.XElement? GenerateAbilityHotkeysXml(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null) return null;

            return new System.Xml.Linq.XElement("abilityHotkeys",
                new System.Xml.Linq.XElement("enabled", hotkeys.enabled.ToString().ToLower()),
                !string.IsNullOrEmpty(hotkeys.qAbilityDefName) ? new System.Xml.Linq.XElement("qAbilityDefName", hotkeys.qAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.wAbilityDefName) ? new System.Xml.Linq.XElement("wAbilityDefName", hotkeys.wAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.eAbilityDefName) ? new System.Xml.Linq.XElement("eAbilityDefName", hotkeys.eAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.rAbilityDefName) ? new System.Xml.Linq.XElement("rAbilityDefName", hotkeys.rAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.wComboAbilityDefName) ? new System.Xml.Linq.XElement("wComboAbilityDefName", hotkeys.wComboAbilityDefName) : null
            );
        }
    }
}
