using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Attributes;
using CharacterStudio.Core;
using Verse;
using UnityEngine;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 皮肤保存器
    /// 用于将 PawnSkinDef 保存为 XML 文件
    /// </summary>
    public static class SkinSaver
    {
        /// <summary>
        /// 保存皮肤定义到指定路径
        /// </summary>
        /// <param name="skinDef">要保存的皮肤定义</param>
        /// <param name="filePath">保存路径（完整路径）</param>
        public static void SaveSkinDef(PawnSkinDef skinDef, string filePath)
        {
            if (skinDef == null)
            {
                Log.Error("[CharacterStudio] 尝试保存空的皮肤定义");
                return;
            }

            try
            {
                // 确保目录存在
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("Defs",
                        GenerateSkinDefElement(skinDef)
                    )
                );

                string tempFilePath = filePath + ".tmp";
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }

                doc.Save(tempFilePath);

                if (File.Exists(filePath))
                {
                    File.Copy(tempFilePath, filePath, true);
                    File.Delete(tempFilePath);
                }
                else
                {
                    File.Move(tempFilePath, filePath);
                }

                Log.Message($"[CharacterStudio] 皮肤已保存至: {filePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 保存皮肤失败: path={filePath}, kind={ClassifySaveFailure(ex)}, {ex}");
                throw;
            }
        }

        public static string GetSaveFailureMessageKey(Exception ex)
        {
            if (IsDiskFullException(ex))
            {
                return "CS_Studio_Err_SaveFailedDiskFull";
            }

            return "CS_Studio_Err_SaveFailed";
        }

        private static string ClassifySaveFailure(Exception ex)
        {
            if (IsDiskFullException(ex))
            {
                return "DiskFull";
            }

            if (ex is UnauthorizedAccessException)
            {
                return "UnauthorizedAccess";
            }

            if (ex is PathTooLongException)
            {
                return "PathTooLong";
            }

            if (ex is DirectoryNotFoundException)
            {
                return "DirectoryNotFound";
            }

            if (ex is IOException)
            {
                return "IOException";
            }

            return ex.GetType().Name;
        }

        private static bool IsDiskFullException(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is IOException ioEx)
                {
                    int win32Code = ioEx.HResult & 0xFFFF;
                    if (win32Code == 112)
                    {
                        return true;
                    }
                }

                if (current.Message.IndexOf("Win32 IO returned 112", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static XElement GenerateSkinDefElement(PawnSkinDef skin)
        {
            return new XElement("CharacterStudio.Core.PawnSkinDef",
                new XElement("defName", skin.defName),
                new XElement("label", skin.label ?? skin.defName),
                !string.IsNullOrEmpty(skin.description) ? new XElement("description", skin.description) : null,
                new XElement("hideVanillaHead", skin.hideVanillaHead.ToString().ToLower()),
                new XElement("hideVanillaHair", skin.hideVanillaHair.ToString().ToLower()),
                new XElement("hideVanillaBody", skin.hideVanillaBody.ToString().ToLower()),
                new XElement("hideVanillaApparel", skin.hideVanillaApparel.ToString().ToLower()),
                new XElement("humanlikeOnly", skin.humanlikeOnly.ToString().ToLower()),
                !string.IsNullOrEmpty(skin.author) ? new XElement("author", skin.author) : null,
                !string.IsNullOrEmpty(skin.version) ? new XElement("version", skin.version) : null,
                !string.IsNullOrEmpty(skin.previewTexPath) ? new XElement("previewTexPath", skin.previewTexPath) : null,
                GenerateAttributesXml(skin.attributes),
                
                // 隐藏路径
                GenerateListElement("hiddenPaths", skin.hiddenPaths),
                // 隐藏标签 (保留兼容性)
                #pragma warning disable CS0618
                GenerateListElement("hiddenTags", skin.hiddenTags),
                #pragma warning restore CS0618

                // 基础槽位
                GenerateBaseAppearanceXml(skin.baseAppearance),

                // 图层
                GenerateLayersXml(skin.layers),
                
                // 目标种族
                GenerateListElement("targetRaces", skin.targetRaces),
                skin.applyAsDefaultForTargetRaces ? new XElement("applyAsDefaultForTargetRaces", "true") : null,
                skin.defaultRacePriority != 0 ? new XElement("defaultRacePriority", skin.defaultRacePriority) : null,

                // 面部配置（eyeDirectionConfig 已嵌套在 faceConfig 内由 GenerateFaceConfigXml 序列化，无需单独输出）
                skin.faceConfig != null
                    && (skin.faceConfig.enabled
                        || skin.faceConfig.HasAnyExpression()
                        || skin.faceConfig.HasAnyLayeredPart()
                        || skin.faceConfig.eyeDirectionConfig?.HasAnyTex() == true
                        || skin.faceConfig.eyeDirectionConfig?.pupilMoveRange != 0f
                        || skin.faceConfig.eyeDirectionConfig?.enabled == true)
                    ? GenerateFaceConfigXml(skin.faceConfig)
                    : null,

                // 武器渲染配置
                skin.weaponRenderConfig != null && skin.weaponRenderConfig.enabled ? GenerateWeaponRenderConfigXml(skin.weaponRenderConfig) : null,

                // 装备配置
                GenerateEquipmentsXml(skin.equipments),

                // 技能与热键
                GenerateAbilitiesXml(skin.abilities),
                GenerateStatModifiersXml(skin.statModifiers),
                GenerateAbilityHotkeysXml(skin.abilityHotkeys)
            );
        }

        private static XElement? GenerateEquipmentsXml(List<CharacterEquipmentDef>? equipments)
        {
            if (equipments == null || equipments.Count == 0) return null;

            var root = new XElement("equipments");
            foreach (var equipment in equipments)
            {
                if (equipment == null) continue;

                equipment.EnsureDefaults();

                var equipmentEl = new XElement("li",
                    !string.IsNullOrWhiteSpace(equipment.defName) ? new XElement("defName", equipment.defName) : null,
                    !string.IsNullOrWhiteSpace(equipment.label) ? new XElement("label", equipment.label) : null,
                    !string.IsNullOrWhiteSpace(equipment.description) ? new XElement("description", equipment.description) : null,
                    new XElement("enabled", equipment.enabled.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(equipment.slotTag) ? new XElement("slotTag", equipment.slotTag) : null,
                    !string.IsNullOrWhiteSpace(equipment.thingDefName) ? new XElement("thingDefName", equipment.thingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.parentThingDefName) ? new XElement("parentThingDefName", equipment.parentThingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.previewTexPath) ? new XElement("previewTexPath", equipment.previewTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.sourceNote) ? new XElement("sourceNote", equipment.sourceNote) : null,
                    !string.IsNullOrWhiteSpace(equipment.worldTexPath) ? new XElement("worldTexPath", equipment.worldTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.wornTexPath) ? new XElement("wornTexPath", equipment.wornTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.maskTexPath) ? new XElement("maskTexPath", equipment.maskTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.shaderDefName) ? new XElement("shaderDefName", equipment.shaderDefName) : null,
                    equipment.useWornGraphicMask ? new XElement("useWornGraphicMask", equipment.useWornGraphicMask.ToString().ToLower()) : null,
                    GenerateListElement("tags", equipment.tags),
                    GenerateListElement("abilityDefNames", equipment.abilityDefNames),
                    GenerateListElement("thingCategories", equipment.thingCategories),
                    GenerateListElement("bodyPartGroups", equipment.bodyPartGroups),
                    GenerateListElement("apparelLayers", equipment.apparelLayers),
                    GenerateListElement("apparelTags", equipment.apparelTags),
                    GenerateEquipmentStatEntriesXml("statBases", equipment.statBases),
                    GenerateEquipmentStatEntriesXml("equippedStatOffsets", equipment.equippedStatOffsets),
                    GenerateEquipmentRenderDataXml(equipment.renderData)
                );

                root.Add(equipmentEl);
            }

            return root.HasElements ? root : null;
        }

        private static XElement? GenerateEquipmentStatEntriesXml(string tagName, List<CharacterEquipmentStatEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var root = new XElement(tagName);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.statDefName))
                {
                    continue;
                }

                root.Add(new XElement("li",
                    new XElement("statDefName", entry.statDefName),
                    new XElement("value", entry.value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                ));
            }

            return root.HasElements ? root : null;
        }

        private static XElement? GenerateEquipmentRenderDataXml(CharacterEquipmentRenderData? renderData)
        {
            if (renderData == null) return null;

            return new XElement("renderData",
                !string.IsNullOrEmpty(renderData.layerName) ? new XElement("layerName", renderData.layerName) : null,
                !string.IsNullOrEmpty(renderData.texPath) ? new XElement("texPath", renderData.texPath) : null,
                !string.IsNullOrEmpty(renderData.anchorTag) ? new XElement("anchorTag", renderData.anchorTag) : null,
                !string.IsNullOrEmpty(renderData.anchorPath) ? new XElement("anchorPath", renderData.anchorPath) : null,
                !string.IsNullOrEmpty(renderData.maskTexPath) ? new XElement("maskTexPath", renderData.maskTexPath) : null,
                !string.IsNullOrEmpty(renderData.shaderDefName) ? new XElement("shaderDefName", renderData.shaderDefName) : null,
                new XElement("offset", $"({renderData.offset.x:F3}, {renderData.offset.y:F3}, {renderData.offset.z:F3})"),
                renderData.offsetEast != Vector3.zero ? new XElement("offsetEast", $"({renderData.offsetEast.x:F3}, {renderData.offsetEast.y:F3}, {renderData.offsetEast.z:F3})") : null,
                renderData.offsetNorth != Vector3.zero ? new XElement("offsetNorth", $"({renderData.offsetNorth.x:F3}, {renderData.offsetNorth.y:F3}, {renderData.offsetNorth.z:F3})") : null,
                new XElement("drawOrder", renderData.drawOrder),
                new XElement("scale", $"({renderData.scale.x:F2}, {renderData.scale.y:F2})"),
                renderData.scaleEastMultiplier != Vector2.one ? new XElement("scaleEastMultiplier", $"({renderData.scaleEastMultiplier.x:F3}, {renderData.scaleEastMultiplier.y:F3})") : null,
                renderData.scaleNorthMultiplier != Vector2.one ? new XElement("scaleNorthMultiplier", $"({renderData.scaleNorthMultiplier.x:F3}, {renderData.scaleNorthMultiplier.y:F3})") : null,
                new XElement("rotation", renderData.rotation),
                renderData.rotationEastOffset != 0f ? new XElement("rotationEastOffset", renderData.rotationEastOffset) : null,
                renderData.rotationNorthOffset != 0f ? new XElement("rotationNorthOffset", renderData.rotationNorthOffset) : null,
                new XElement("flipHorizontal", renderData.flipHorizontal.ToString().ToLower()),
                new XElement("colorSource", renderData.colorSource.ToString()),
                renderData.colorSource == LayerColorSource.Fixed ? new XElement("customColor", $"({renderData.customColor.r:F3}, {renderData.customColor.g:F3}, {renderData.customColor.b:F3}, {renderData.customColor.a:F3})") : null,
                new XElement("colorTwoSource", renderData.colorTwoSource.ToString()),
                renderData.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", $"({renderData.customColorTwo.r:F3}, {renderData.customColorTwo.g:F3}, {renderData.customColorTwo.b:F3}, {renderData.customColorTwo.a:F3})") : null,
                new XElement("visible", renderData.visible.ToString().ToLower())
            );
        }

        private static XElement? GenerateAbilitiesXml(List<ModularAbilityDef>? abilities)
        {
            if (abilities == null || abilities.Count == 0) return null;

            var element = new XElement("abilities");
            foreach (var ability in abilities)
            {
                if (ability == null || string.IsNullOrEmpty(ability.defName)) continue;

                var abilityEl = new XElement("li",
                    new XElement("defName", ability.defName),
                    !string.IsNullOrEmpty(ability.label) ? new XElement("label", ability.label) : null,
                    !string.IsNullOrEmpty(ability.description) ? new XElement("description", ability.description) : null,
                    !string.IsNullOrEmpty(ability.iconPath) ? new XElement("iconPath", ability.iconPath) : null,
                    new XElement("cooldownTicks", ability.cooldownTicks),
                    new XElement("warmupTicks", ability.warmupTicks),
                    new XElement("charges", ability.charges),
                    new XElement("aiCanUse", ability.aiCanUse),
                    new XElement("carrierType", ability.carrierType.ToString()),
                    new XElement("targetType", ability.targetType.ToString()),
                    new XElement("useRadius", ability.useRadius.ToString().ToLower()),
                    new XElement("areaCenter", ability.areaCenter.ToString()),
                    new XElement("range", ability.range),
                    new XElement("radius", ability.radius),
                    ability.projectileDef != null ? new XElement("projectileDef", ability.projectileDef.defName) : null,
                    GenerateEffectsXml(ability.effects),
                    GenerateVisualEffectsXml(ability.visualEffects),
                    GenerateRuntimeComponentsXml(ability.runtimeComponents)
                );

                element.Add(abilityEl);
            }

            return element;
        }

        private static XElement? GenerateEffectsXml(List<AbilityEffectConfig>? effects)
        {
            if (effects == null || effects.Count == 0) return null;

            var effectsEl = new XElement("effects");
            foreach (var effect in effects)
            {
                if (effect == null) continue;

                var effectEl = new XElement("li",
                    new XElement("type", effect.type.ToString()),
                    new XElement("amount", effect.amount),
                    new XElement("duration", effect.duration),
                    new XElement("chance", effect.chance),
                    effect.damageDef != null ? new XElement("damageDef", effect.damageDef.defName) : null,
                    effect.hediffDef != null ? new XElement("hediffDef", effect.hediffDef.defName) : null,
                    effect.summonKind != null ? new XElement("summonKind", effect.summonKind.defName) : null,
                    new XElement("summonCount", effect.summonCount),
                    new XElement("controlMode", effect.controlMode.ToString()),
                    new XElement("controlMoveDistance", effect.controlMoveDistance),
                    new XElement("terraformMode", effect.terraformMode.ToString()),
                    effect.terraformThingDef != null ? new XElement("terraformThingDef", effect.terraformThingDef.defName) : null,
                    effect.terraformTerrainDef != null ? new XElement("terraformTerrainDef", effect.terraformTerrainDef.defName) : null,
                    new XElement("terraformSpawnCount", effect.terraformSpawnCount)
                );

                effectsEl.Add(effectEl);
            }

            return effectsEl;
        }

        private static XElement? GenerateRuntimeComponentsXml(List<AbilityRuntimeComponentConfig>? components)
        {
            if (components == null || components.Count == 0) return null;

            var root = new XElement("runtimeComponents");
            foreach (var component in components)
            {
                if (component == null) continue;

                var compEl = new XElement("li",
                    new XElement("type", component.type.ToString()),
                    new XElement("enabled", component.enabled.ToString().ToLower()),
                    new XElement("comboWindowTicks", component.comboWindowTicks),
                    new XElement("cooldownTicks", component.cooldownTicks),
                    new XElement("jumpDistance", component.jumpDistance),
                    new XElement("findCellRadius", component.findCellRadius),
                    new XElement("triggerAbilityEffectsAfterJump", component.triggerAbilityEffectsAfterJump.ToString().ToLower()),
                    new XElement("useMouseTargetCell", component.useMouseTargetCell.ToString().ToLower()),
                    new XElement("smartCastOffsetCells", component.smartCastOffsetCells),
                    new XElement("smartCastClampToMaxDistance", component.smartCastClampToMaxDistance.ToString().ToLower()),
                    new XElement("smartCastAllowFallbackForward", component.smartCastAllowFallbackForward.ToString().ToLower()),
                    new XElement("overrideHotkeySlot", component.overrideHotkeySlot.ToString()),
                    new XElement("overrideAbilityDefName", component.overrideAbilityDefName ?? string.Empty),
                    new XElement("overrideDurationTicks", component.overrideDurationTicks),
                    new XElement("followupCooldownHotkeySlot", component.followupCooldownHotkeySlot.ToString()),
                    new XElement("followupCooldownTicks", component.followupCooldownTicks),
                    new XElement("requiredStacks", component.requiredStacks),
                    new XElement("delayTicks", component.delayTicks),
                    new XElement("wave1Radius", component.wave1Radius),
                    new XElement("wave1Damage", component.wave1Damage),
                    new XElement("wave2Radius", component.wave2Radius),
                    new XElement("wave2Damage", component.wave2Damage),
                    new XElement("wave3Radius", component.wave3Radius),
                    new XElement("wave3Damage", component.wave3Damage),
                    component.waveDamageDef != null ? new XElement("waveDamageDef", component.waveDamageDef.defName) : null,
                    new XElement("pulseIntervalTicks", component.pulseIntervalTicks),
                    new XElement("pulseTotalTicks", component.pulseTotalTicks),
                    new XElement("pulseStartsImmediately", component.pulseStartsImmediately.ToString().ToLower()),
                    new XElement("killRefreshHotkeySlot", component.killRefreshHotkeySlot.ToString()),
                    new XElement("killRefreshCooldownPercent", component.killRefreshCooldownPercent),
                    new XElement("shieldMaxDamage", component.shieldMaxDamage),
                    new XElement("shieldDurationTicks", component.shieldDurationTicks),
                    new XElement("shieldHealRatio", component.shieldHealRatio),
                    new XElement("shieldBonusDamageRatio", component.shieldBonusDamageRatio),
                    new XElement("maxBounceCount", component.maxBounceCount),
                    new XElement("bounceRange", component.bounceRange),
                    new XElement("bounceDamageFalloff", component.bounceDamageFalloff),
                    new XElement("executeThresholdPercent", component.executeThresholdPercent),
                    new XElement("executeBonusDamageScale", component.executeBonusDamageScale),
                    new XElement("missingHealthBonusPerTenPercent", component.missingHealthBonusPerTenPercent),
                    new XElement("missingHealthBonusMaxScale", component.missingHealthBonusMaxScale),
                    new XElement("fullHealthThresholdPercent", component.fullHealthThresholdPercent),
                    new XElement("fullHealthBonusDamageScale", component.fullHealthBonusDamageScale),
                    new XElement("nearbyEnemyBonusMaxTargets", component.nearbyEnemyBonusMaxTargets),
                    new XElement("nearbyEnemyBonusPerTarget", component.nearbyEnemyBonusPerTarget),
                    new XElement("nearbyEnemyBonusRadius", component.nearbyEnemyBonusRadius),
                    new XElement("isolatedTargetRadius", component.isolatedTargetRadius),
                    new XElement("isolatedTargetBonusDamageScale", component.isolatedTargetBonusDamageScale),
                    new XElement("markDurationTicks", component.markDurationTicks),
                    new XElement("markMaxStacks", component.markMaxStacks),
                    new XElement("markDetonationDamage", component.markDetonationDamage),
                    component.markDamageDef != null ? new XElement("markDamageDef", component.markDamageDef.defName) : null,
                    new XElement("comboStackWindowTicks", component.comboStackWindowTicks),
                    new XElement("comboStackMax", component.comboStackMax),
                    new XElement("comboStackBonusDamagePerStack", component.comboStackBonusDamagePerStack),
                    new XElement("slowFieldDurationTicks", component.slowFieldDurationTicks),
                    new XElement("slowFieldRadius", component.slowFieldRadius),
                    new XElement("slowFieldHediffDefName", component.slowFieldHediffDefName ?? string.Empty),
                    new XElement("pierceMaxTargets", component.pierceMaxTargets),
                    new XElement("pierceBonusDamagePerTarget", component.pierceBonusDamagePerTarget),
                    new XElement("pierceSearchRange", component.pierceSearchRange),
                    new XElement("dashEmpowerDurationTicks", component.dashEmpowerDurationTicks),
                    new XElement("dashEmpowerBonusDamageScale", component.dashEmpowerBonusDamageScale),
                    new XElement("hitHealAmount", component.hitHealAmount),
                    new XElement("hitHealRatio", component.hitHealRatio),
                    new XElement("refundHotkeySlot", component.refundHotkeySlot.ToString()),
                    new XElement("hitCooldownRefundPercent", component.hitCooldownRefundPercent),
                    new XElement("splitProjectileCount", component.splitProjectileCount),
                    new XElement("splitDamageScale", component.splitDamageScale),
                    new XElement("splitSearchRange", component.splitSearchRange),
                    new XElement("flightDurationTicks", component.flightDurationTicks),
                    new XElement("flightHeightFactor", component.flightHeightFactor),
                    new XElement("suppressCombatActionsDuringFlightState", component.suppressCombatActionsDuringFlightState.ToString().ToLower())
                );

                root.Add(compEl);
            }

            return root;
        }

        private static XElement? GenerateAbilityHotkeysXml(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null) return null;

            return new XElement("abilityHotkeys",
                new XElement("enabled", hotkeys.enabled.ToString().ToLower()),
                !string.IsNullOrEmpty(hotkeys.qAbilityDefName) ? new XElement("qAbilityDefName", hotkeys.qAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.wAbilityDefName) ? new XElement("wAbilityDefName", hotkeys.wAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.eAbilityDefName) ? new XElement("eAbilityDefName", hotkeys.eAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.rAbilityDefName) ? new XElement("rAbilityDefName", hotkeys.rAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.wComboAbilityDefName) ? new XElement("wComboAbilityDefName", hotkeys.wComboAbilityDefName) : null
            );
        }

        private static XElement? GenerateListElement(string tagName, List<string>? items)
        {
            if (items == null || items.Count == 0) return null;

            var element = new XElement(tagName);
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item))
                {
                    element.Add(new XElement("li", item));
                }
            }
            return element;
        }

        private static XElement? GenerateStringArrayElement(string tagName, string[]? items)
        {
            if (items == null || items.Length == 0) return null;

            var element = new XElement(tagName);
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    element.Add(new XElement("li", item));
                }
            }

            return element.HasElements ? element : null;
        }

        private static XElement? GenerateAttributesXml(CharacterStudio.AI.CharacterAttributeProfile? attributes)
        {
            if (attributes == null)
            {
                return null;
            }

            return new XElement("attributes",
                !string.IsNullOrEmpty(attributes.title) ? new XElement("title", attributes.title) : null,
                !string.IsNullOrEmpty(attributes.factionRole) ? new XElement("factionRole", attributes.factionRole) : null,
                !string.IsNullOrEmpty(attributes.combatRole) ? new XElement("combatRole", attributes.combatRole) : null,
                !string.IsNullOrEmpty(attributes.backstorySummary) ? new XElement("backstorySummary", attributes.backstorySummary) : null,
                !string.IsNullOrEmpty(attributes.personality) ? new XElement("personality", attributes.personality) : null,
                !string.IsNullOrEmpty(attributes.bodyTypeDefName) ? new XElement("bodyTypeDefName", attributes.bodyTypeDefName) : null,
                !string.IsNullOrEmpty(attributes.headTypeDefName) ? new XElement("headTypeDefName", attributes.headTypeDefName) : null,
                !string.IsNullOrEmpty(attributes.hairDefName) ? new XElement("hairDefName", attributes.hairDefName) : null,
                !string.IsNullOrEmpty(attributes.favoriteColorHex) ? new XElement("favoriteColorHex", attributes.favoriteColorHex) : null,
                new XElement("biologicalAge", attributes.biologicalAge),
                new XElement("chronologicalAge", attributes.chronologicalAge),
                new XElement("moveSpeedMultiplier", attributes.moveSpeedMultiplier),
                new XElement("meleePower", attributes.meleePower),
                new XElement("shootingAccuracy", attributes.shootingAccuracy),
                new XElement("armorRating", attributes.armorRating),
                new XElement("psychicSensitivity", attributes.psychicSensitivity),
                new XElement("marketValue", attributes.marketValue),
                GenerateListElement("tags", attributes.tags),
                GenerateListElement("keyTraits", attributes.keyTraits),
                GenerateListElement("startingApparelDefs", attributes.startingApparelDefs)
            );
        }

        private static XElement? GenerateStatModifiersXml(CharacterStatModifierProfile? profile)
        {
            if (profile == null || profile.entries == null || profile.entries.Count == 0)
            {
                return null;
            }

            var root = new XElement("statModifiers");
            var entriesElement = new XElement("entries");
            foreach (CharacterStatModifierEntry entry in profile.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.statDefName))
                {
                    continue;
                }

                entriesElement.Add(new XElement("li",
                    new XElement("statDefName", entry.statDefName),
                    new XElement("mode", entry.mode.ToString()),
                    new XElement("value", entry.value),
                    new XElement("enabled", entry.enabled.ToString().ToLowerInvariant())
                ));
            }

            if (entriesElement.HasElements)
                root.Add(entriesElement);

            return root.HasElements ? root : null;
        }

        private static XElement? GenerateBaseAppearanceXml(BaseAppearanceConfig? baseAppearance)
        {
            if (baseAppearance == null || baseAppearance.slots == null || baseAppearance.slots.Count == 0) return null;

            var root = new XElement("baseAppearance");
            var slotsEl = new XElement("slots");

            foreach (var slot in baseAppearance.slots)
            {
                if (slot == null) continue;

                slotsEl.Add(new XElement("li",
                    new XElement("slotType", slot.slotType.ToString()),
                    new XElement("enabled", slot.enabled.ToString().ToLower()),
                    !string.IsNullOrEmpty(slot.texPath) ? new XElement("texPath", slot.texPath) : null,
                    !string.IsNullOrEmpty(slot.maskTexPath) ? new XElement("maskTexPath", slot.maskTexPath) : null,
                    !string.IsNullOrEmpty(slot.shaderDefName) ? new XElement("shaderDefName", slot.shaderDefName) : null,
                    new XElement("colorSource", slot.colorSource.ToString()),
                    slot.colorSource == LayerColorSource.Fixed ? new XElement("customColor", $"({slot.customColor.r:F3}, {slot.customColor.g:F3}, {slot.customColor.b:F3}, {slot.customColor.a:F3})") : null,
                    new XElement("colorTwoSource", slot.colorTwoSource.ToString()),
                    slot.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", $"({slot.customColorTwo.r:F3}, {slot.customColorTwo.g:F3}, {slot.customColorTwo.b:F3}, {slot.customColorTwo.a:F3})") : null,
                    new XElement("scale", $"({slot.scale.x:F2}, {slot.scale.y:F2})"),
                    slot.scaleEastMultiplier != Vector2.one ? new XElement("scaleEastMultiplier", $"({slot.scaleEastMultiplier.x:F3}, {slot.scaleEastMultiplier.y:F3})") : null,
                    slot.scaleNorthMultiplier != Vector2.one ? new XElement("scaleNorthMultiplier", $"({slot.scaleNorthMultiplier.x:F3}, {slot.scaleNorthMultiplier.y:F3})") : null,
                    new XElement("offset", $"({slot.offset.x:F3}, {slot.offset.y:F3}, {slot.offset.z:F3})"),
                    slot.offsetEast != Vector3.zero ? new XElement("offsetEast", $"({slot.offsetEast.x:F3}, {slot.offsetEast.y:F3}, {slot.offsetEast.z:F3})") : null,
                    slot.offsetNorth != Vector3.zero ? new XElement("offsetNorth", $"({slot.offsetNorth.x:F3}, {slot.offsetNorth.y:F3}, {slot.offsetNorth.z:F3})") : null,
                    new XElement("rotation", slot.rotation),
                    slot.rotationEastOffset != 0f ? new XElement("rotationEastOffset", slot.rotationEastOffset) : null,
                    slot.rotationNorthOffset != 0f ? new XElement("rotationNorthOffset", slot.rotationNorthOffset) : null,
                    new XElement("flipHorizontal", slot.flipHorizontal.ToString().ToLower()),
                    new XElement("drawOrderOffset", slot.drawOrderOffset),
                    slot.graphicClass != null ? new XElement("graphicClass", slot.graphicClass.FullName) : null
                ));
            }

            root.Add(slotsEl);
            return root;
        }

        private static XElement? GenerateLayersXml(List<PawnLayerConfig>? layers)
        {
            if (layers == null || layers.Count == 0) return null;

            var element = new XElement("layers");
            foreach (var layer in layers)
            {
                var layerEl = new XElement("li",
                    new XElement("layerName", layer.layerName ?? ""),
                    new XElement("texPath", layer.texPath ?? ""),
                    new XElement("anchorTag", layer.anchorTag ?? "Head"),
                    !string.IsNullOrEmpty(layer.anchorPath) ? new XElement("anchorPath", layer.anchorPath) : null,
                    !string.IsNullOrEmpty(layer.maskTexPath) ? new XElement("maskTexPath", layer.maskTexPath) : null,
                    !string.IsNullOrEmpty(layer.shaderDefName) ? new XElement("shaderDefName", layer.shaderDefName) : null,

                    // 变换
                    new XElement("offset", $"({layer.offset.x:F3}, {layer.offset.y:F3}, {layer.offset.z:F3})"),
                    layer.offsetEast != Vector3.zero ? new XElement("offsetEast", $"({layer.offsetEast.x:F3}, {layer.offsetEast.y:F3}, {layer.offsetEast.z:F3})") : null,
                    layer.offsetNorth != Vector3.zero ? new XElement("offsetNorth", $"({layer.offsetNorth.x:F3}, {layer.offsetNorth.y:F3}, {layer.offsetNorth.z:F3})") : null,

                    new XElement("drawOrder", layer.drawOrder),
                    new XElement("scale", $"({layer.scale.x:F2}, {layer.scale.y:F2})"),
                    layer.scaleEastMultiplier != Vector2.one ? new XElement("scaleEastMultiplier", $"({layer.scaleEastMultiplier.x:F3}, {layer.scaleEastMultiplier.y:F3})") : null,
                    layer.scaleNorthMultiplier != Vector2.one ? new XElement("scaleNorthMultiplier", $"({layer.scaleNorthMultiplier.x:F3}, {layer.scaleNorthMultiplier.y:F3})") : null,
                    new XElement("rotation", layer.rotation),
                    layer.rotationEastOffset != 0f ? new XElement("rotationEastOffset", layer.rotationEastOffset) : null,
                    layer.rotationNorthOffset != 0f ? new XElement("rotationNorthOffset", layer.rotationNorthOffset) : null,
                    new XElement("flipHorizontal", layer.flipHorizontal.ToString().ToLower()),

                    // 颜色
                    new XElement("colorSource", layer.colorSource.ToString()),
                    layer.colorSource == LayerColorSource.Fixed ? new XElement("customColor", $"({layer.customColor.r:F3}, {layer.customColor.g:F3}, {layer.customColor.b:F3}, {layer.customColor.a:F3})") : null,
                    new XElement("colorTwoSource", layer.colorTwoSource.ToString()),
                    layer.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", $"({layer.customColorTwo.r:F3}, {layer.customColorTwo.g:F3}, {layer.customColorTwo.b:F3}, {layer.customColorTwo.a:F3})") : null,

                    new XElement("visible", layer.visible.ToString().ToLower()),
                    new XElement("role", layer.role.ToString()),
                    new XElement("variantLogic", layer.variantLogic.ToString()),
                    !string.IsNullOrEmpty(layer.variantBaseName) ? new XElement("variantBaseName", layer.variantBaseName) : null,
                    !layer.useDirectionalSuffix ? new XElement("useDirectionalSuffix", layer.useDirectionalSuffix.ToString().ToLower()) : null,
                    layer.useExpressionSuffix ? new XElement("useExpressionSuffix", layer.useExpressionSuffix.ToString().ToLower()) : null,
                    layer.useEyeDirectionSuffix ? new XElement("useEyeDirectionSuffix", layer.useEyeDirectionSuffix.ToString().ToLower()) : null,
                    layer.useBlinkSuffix ? new XElement("useBlinkSuffix", layer.useBlinkSuffix.ToString().ToLower()) : null,
                    layer.useFrameSequence ? new XElement("useFrameSequence", layer.useFrameSequence.ToString().ToLower()) : null,
                    layer.hideWhenMissingVariant ? new XElement("hideWhenMissingVariant", layer.hideWhenMissingVariant.ToString().ToLower()) : null,
                    layer.eyeRenderMode != EyeRenderMode.TextureSwap ? new XElement("eyeRenderMode", layer.eyeRenderMode.ToString()) : null,
                    layer.eyeUvMoveRange != 0f ? new XElement("eyeUvMoveRange", layer.eyeUvMoveRange.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)) : null,
                    GenerateStringArrayElement("visibleExpressions", layer.visibleExpressions),
                    GenerateStringArrayElement("hiddenExpressions", layer.hiddenExpressions),

                    // Worker / Graphic
                    layer.workerClass != null ? new XElement("workerClass", layer.workerClass.FullName) : null,
                    layer.graphicClass != null ? new XElement("graphicClass", layer.graphicClass.FullName) : null,

                    // 动画
                    layer.animationType != LayerAnimationType.None ? new XElement("animationType", layer.animationType.ToString()) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animFrequency", layer.animFrequency) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animAmplitude", layer.animAmplitude) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animSpeed", layer.animSpeed) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animPhaseOffset", layer.animPhaseOffset) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animAffectsOffset", layer.animAffectsOffset.ToString().ToLower()) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animOffsetAmplitude", layer.animOffsetAmplitude) : null,
                    layer.animationType == LayerAnimationType.Spin && layer.animPivotOffset != Vector2.zero ? new XElement("animPivotOffset", $"({layer.animPivotOffset.x:F3}, {layer.animPivotOffset.y:F3})") : null
                );
                element.Add(layerEl);
            }
            return element;
        }

        private static XElement GenerateFaceConfigXml(PawnFaceConfig config)
        {
            var element = new XElement("faceConfig",
                new XElement("enabled", config.enabled.ToString().ToLower()),
                new XElement("workflowMode", config.workflowMode.ToString())
            );

            if (!string.IsNullOrWhiteSpace(config.layeredSourceRoot))
            {
                element.Add(new XElement("layeredSourceRoot", config.layeredSourceRoot));
            }

            if (config.expressions != null && config.expressions.Count > 0)
            {
                var exprsEl = new XElement("expressions");
                foreach (var expr in config.expressions)
                {
                    if (expr == null) continue;

                    bool hasStatic = !string.IsNullOrEmpty(expr.texPath);
                    List<ExpressionFrame>? frames = expr.frames;
                    bool hasFrames = frames != null && frames.Count > 0;
                    if (!hasStatic && !hasFrames) continue;

                    var exprEl = new XElement("li",
                        new XElement("expression", expr.expression.ToString()),
                        hasStatic ? new XElement("texPath", expr.texPath) : null
                    );

                    if (hasFrames)
                    {
                        List<ExpressionFrame> nonNullFrames = frames!;
                        var framesEl = new XElement("frames");
                        foreach (var frame in nonNullFrames)
                        {
                            if (frame == null || string.IsNullOrWhiteSpace(frame.texPath)) continue;

                            framesEl.Add(new XElement("li",
                                new XElement("texPath", frame.texPath),
                                new XElement("durationTicks", frame.durationTicks)
                            ));
                        }

                        if (framesEl.HasElements)
                        {
                            exprEl.Add(framesEl);
                        }
                    }

                    exprsEl.Add(exprEl);
                }

                if (exprsEl.HasElements) element.Add(exprsEl);
            }

            if (config.layeredParts != null && config.layeredParts.Count > 0)
            {
                var layeredPartsEl = new XElement("layeredParts");
                foreach (var part in config.layeredParts)
                {
                    if (part == null) continue;

                    part.SyncDirectionalTexPathsFromLegacy();
                    part.SyncLegacyMotionAmplitude();
                    if (!part.HasAnyTexture()) continue;

                    LayeredFacePartSide normalizedSide = PawnFaceConfig.NormalizePartSide(part.partType, part.side);

                    layeredPartsEl.Add(new XElement("li",
                        new XElement("partType", part.partType.ToString()),
                        new XElement("expression", part.expression.ToString()),
                        new XElement("texPath", part.texPath),
                        !string.IsNullOrWhiteSpace(part.texPathSouth) ? new XElement("texPathSouth", part.texPathSouth) : null,
                        !string.IsNullOrWhiteSpace(part.texPathEast) ? new XElement("texPathEast", part.texPathEast) : null,
                        !string.IsNullOrWhiteSpace(part.texPathNorth) ? new XElement("texPathNorth", part.texPathNorth) : null,
                        new XElement("enabled", part.enabled.ToString().ToLower()),
                        normalizedSide != LayeredFacePartSide.None
                            ? new XElement("side", normalizedSide.ToString())
                            : null,
                        PawnFaceConfig.IsOverlayPart(part.partType) && !string.IsNullOrWhiteSpace(part.overlayId)
                            ? new XElement("overlayId", part.overlayId)
                            : null,
                        PawnFaceConfig.IsOverlayPart(part.partType)
                            ? new XElement("overlayOrder", part.overlayOrder)
                            : null,
                        part.motionAmplitude > 0f
                            ? new XElement("motionAmplitude", part.motionAmplitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))
                            : null
                    ));
                }

                if (layeredPartsEl.HasElements) element.Add(layeredPartsEl);
            }

            // 序列化眼睛方向配置（支持 UV-only pupilMoveRange，无需依赖方向贴图）
            if (config.eyeDirectionConfig != null
                && (config.eyeDirectionConfig.HasAnyTex()
                    || config.eyeDirectionConfig.pupilMoveRange != 0f
                    || config.eyeDirectionConfig.upperLidMoveDown != 0.0044f
                    || config.eyeDirectionConfig.enabled))
            {
                var eyeEl = GenerateEyeDirectionConfigXml(config.eyeDirectionConfig);
                if (eyeEl != null) element.Add(eyeEl);
            }

            if (config.browMotion != null)
                element.Add(new XElement("browMotion", DirectXmlSaver.XElementFromObject(config.browMotion, typeof(PawnFaceConfig.BrowMotionConfig)).Elements()));
            if (config.mouthMotion != null)
                element.Add(new XElement("mouthMotion", DirectXmlSaver.XElementFromObject(config.mouthMotion, typeof(PawnFaceConfig.MouthMotionConfig)).Elements()));
            if (config.emotionOverlayMotion != null)
                element.Add(new XElement("emotionOverlayMotion", DirectXmlSaver.XElementFromObject(config.emotionOverlayMotion, typeof(PawnFaceConfig.EmotionOverlayMotionConfig)).Elements()));

            return element;
        }

        private static XElement? GenerateVisualEffectsXml(List<CharacterStudio.Abilities.AbilityVisualEffectConfig>? vfxList)
        {
            if (vfxList == null || vfxList.Count == 0) return null;

            var root = new XElement("visualEffects");
            foreach (var vfx in vfxList)
            {
                if (vfx == null) continue;
                vfx.NormalizeLegacyData();
                vfx.SyncLegacyFields();
                root.Add(new XElement("li",
                    new XElement("enabled", vfx.enabled.ToString().ToLower()),
                    new XElement("type", vfx.type.ToString()),
                    new XElement("sourceMode", vfx.sourceMode.ToString()),
                    vfx.UsesCustomTextureType ? new XElement("textureSource", vfx.textureSource.ToString()) : null,
                    !string.IsNullOrWhiteSpace(vfx.presetDefName) ? new XElement("presetDefName", vfx.presetDefName) : null,
                    !string.IsNullOrWhiteSpace(vfx.customTexturePath) ? new XElement("customTexturePath", vfx.customTexturePath) : null,
                    new XElement("target", vfx.target.ToString()),
                    new XElement("trigger", vfx.trigger.ToString()),
                    new XElement("delayTicks", vfx.delayTicks),
                    new XElement("displayDurationTicks", vfx.displayDurationTicks),
                    vfx.linkedExpression.HasValue ? new XElement("linkedExpression", vfx.linkedExpression.Value.ToString()) : null,
                    new XElement("linkedExpressionDurationTicks", vfx.linkedExpressionDurationTicks),
                    new XElement("linkedPupilBrightnessOffset", vfx.linkedPupilBrightnessOffset),
                    new XElement("linkedPupilContrastOffset", vfx.linkedPupilContrastOffset),
                    new XElement("scale", vfx.scale),
                    new XElement("drawSize", vfx.drawSize),
                    new XElement("useCasterFacing", vfx.useCasterFacing.ToString().ToLowerInvariant()),
                    new XElement("forwardOffset", vfx.forwardOffset),
                    new XElement("sideOffset", vfx.sideOffset),
                    new XElement("heightOffset", vfx.heightOffset),
                    new XElement("rotation", vfx.rotation),
                    vfx.textureScale != UnityEngine.Vector2.one ? new XElement("textureScale", $"({vfx.textureScale.x:F3}, {vfx.textureScale.y:F3})") : null,
                    new XElement("repeatCount", vfx.repeatCount),
                    new XElement("repeatIntervalTicks", vfx.repeatIntervalTicks),
                    vfx.offset != UnityEngine.Vector3.zero ? new XElement("offset", $"{vfx.offset.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{vfx.offset.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{vfx.offset.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}") : null,
                    new XElement("playSound", vfx.playSound.ToString().ToLowerInvariant()),
                    !string.IsNullOrWhiteSpace(vfx.soundDefName) ? new XElement("soundDefName", vfx.soundDefName) : null,
                    new XElement("soundDelayTicks", vfx.soundDelayTicks),
                    new XElement("soundVolume", vfx.soundVolume),
                    new XElement("soundPitch", vfx.soundPitch),
                    new XElement("attachToPawn", vfx.attachToPawn.ToString().ToLower()),
                    new XElement("attachToTargetCell", vfx.attachToTargetCell.ToString().ToLower())
                ));
            }
            return root;
        }

        private static XElement? GenerateEyeDirectionConfigXml(CharacterStudio.Core.PawnEyeDirectionConfig eyeCfg)
        {
            if (eyeCfg == null) return null;

            var el = new XElement("eyeDirectionConfig",
                new XElement("enabled", eyeCfg.enabled.ToString().ToLower())
            );

            if (!string.IsNullOrEmpty(eyeCfg.texCenter)) el.Add(new XElement("texCenter", eyeCfg.texCenter));
            if (!string.IsNullOrEmpty(eyeCfg.texLeft))   el.Add(new XElement("texLeft",   eyeCfg.texLeft));
            if (!string.IsNullOrEmpty(eyeCfg.texRight))  el.Add(new XElement("texRight",  eyeCfg.texRight));
            if (!string.IsNullOrEmpty(eyeCfg.texUp))     el.Add(new XElement("texUp",     eyeCfg.texUp));
            if (!string.IsNullOrEmpty(eyeCfg.texDown))   el.Add(new XElement("texDown",   eyeCfg.texDown));
            if (eyeCfg.pupilMoveRange != 0f)             el.Add(new XElement("pupilMoveRange", eyeCfg.pupilMoveRange.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)));
            if (!Mathf.Approximately(eyeCfg.upperLidMoveDown, 0.0044f))
                el.Add(new XElement("upperLidMoveDown", eyeCfg.upperLidMoveDown.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)));
            el.Add(new XElement("lidMotion", DirectXmlToXElement(eyeCfg.lidMotion)));
            el.Add(new XElement("eyeMotion", DirectXmlToXElement(eyeCfg.eyeMotion)));
            el.Add(new XElement("pupilMotion", DirectXmlToXElement(eyeCfg.pupilMotion)));

            return el;
        }

        private static XElement DirectXmlToXElement(object value)
        {
            string xml = DirectXmlSaver.XElementFromObject(value, value.GetType()).ToString();
            return XElement.Parse(xml);
        }

        private static XElement GenerateWeaponRenderConfigXml(CharacterStudio.Core.WeaponRenderConfig cfg)
        {
            var el = new XElement("weaponRenderConfig",
                new XElement("enabled", cfg.enabled.ToString().ToLower()),
                new XElement("applyToOffHand", cfg.applyToOffHand.ToString().ToLower()),
                new XElement("scale", $"({cfg.scale.x:F3}, {cfg.scale.y:F3})")
            );

            if (cfg.carryVisual != null && cfg.carryVisual.enabled) el.Add(GenerateWeaponCarryVisualXml(cfg.carryVisual));

            if (cfg.offset != UnityEngine.Vector3.zero)
                el.Add(new XElement("offset", $"({cfg.offset.x:F3}, {cfg.offset.y:F3}, {cfg.offset.z:F3})"));
            if (cfg.offsetSouth != UnityEngine.Vector3.zero)
                el.Add(new XElement("offsetSouth", $"({cfg.offsetSouth.x:F3}, {cfg.offsetSouth.y:F3}, {cfg.offsetSouth.z:F3})"));
            if (cfg.offsetNorth != UnityEngine.Vector3.zero)
                el.Add(new XElement("offsetNorth", $"({cfg.offsetNorth.x:F3}, {cfg.offsetNorth.y:F3}, {cfg.offsetNorth.z:F3})"));
            if (cfg.offsetEast != UnityEngine.Vector3.zero)
                el.Add(new XElement("offsetEast", $"({cfg.offsetEast.x:F3}, {cfg.offsetEast.y:F3}, {cfg.offsetEast.z:F3})"));

            return el;
        }

        private static XElement GenerateWeaponCarryVisualXml(CharacterStudio.Core.WeaponCarryVisualConfig cfg)
        {
            var el = new XElement("carryVisual",
                new XElement("enabled", cfg.enabled.ToString().ToLower()),
                new XElement("anchorTag", cfg.anchorTag ?? "Body"),
                new XElement("scale", $"({cfg.scale.x:F3}, {cfg.scale.y:F3})"),
                new XElement("drawOrder", cfg.drawOrder.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))
            );

            if (!string.IsNullOrEmpty(cfg.texUndrafted)) el.Add(new XElement("texUndrafted", cfg.texUndrafted));
            if (!string.IsNullOrEmpty(cfg.texDrafted))   el.Add(new XElement("texDrafted", cfg.texDrafted));
            if (!string.IsNullOrEmpty(cfg.texCasting))   el.Add(new XElement("texCasting", cfg.texCasting));
            if (cfg.offset != UnityEngine.Vector3.zero)      el.Add(new XElement("offset", $"({cfg.offset.x:F3}, {cfg.offset.y:F3}, {cfg.offset.z:F3})"));
            if (cfg.offsetNorth != UnityEngine.Vector3.zero) el.Add(new XElement("offsetNorth", $"({cfg.offsetNorth.x:F3}, {cfg.offsetNorth.y:F3}, {cfg.offsetNorth.z:F3})"));
            if (cfg.offsetEast != UnityEngine.Vector3.zero)  el.Add(new XElement("offsetEast", $"({cfg.offsetEast.x:F3}, {cfg.offsetEast.y:F3}, {cfg.offsetEast.z:F3})"));

            return el;
        }
    }
}
