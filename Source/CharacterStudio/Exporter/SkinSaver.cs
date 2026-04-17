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
                    string backupPath = filePath + ".bak";
                    File.Replace(tempFilePath, filePath, backupPath);
                    try { File.Delete(backupPath); } catch { /* backup cleanup is best-effort */ }
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
                new XElement("globalTextureScale", skin.globalTextureScale.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                !string.IsNullOrEmpty(skin.author) ? new XElement("author", skin.author) : null,
                !string.IsNullOrEmpty(skin.version) ? new XElement("version", skin.version) : null,
                !string.IsNullOrEmpty(skin.previewTexPath) ? new XElement("previewTexPath", skin.previewTexPath) : null,
                !string.IsNullOrEmpty(skin.xenotypeDefName) ? new XElement("xenotypeDefName", skin.xenotypeDefName) : null,
                !string.IsNullOrEmpty(skin.raceDisplayName) ? new XElement("raceDisplayName", skin.raceDisplayName) : null,
                Math.Abs(skin.previewHeadOffsetZ) > 0.0001f ? new XElement("previewHeadOffsetZ", skin.previewHeadOffsetZ.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
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

                // 面部配置
                skin.faceConfig != null
                    && (skin.faceConfig.enabled
                        || skin.faceConfig.HasAnyExpression()
                        || skin.faceConfig.HasAnyLayeredPart()
                        || skin.faceConfig.eyeDirectionConfig?.HasAnyTex() == true
                        || skin.faceConfig.eyeDirectionConfig?.enabled == true)
                    ? GenerateFaceConfigXml(skin.faceConfig)
                    : null,

                // 动画配置
                skin.animationConfig != null && skin.animationConfig.enabled ? GenerateAnimationConfigXml(skin.animationConfig) : null,

                // 装备配置
                GenerateEquipmentsXml(skin.equipments),

                // 技能与热键
                AbilityXmlSerialization.GenerateAbilitiesElement(skin.abilities),
                GenerateStatModifiersXml(skin.statModifiers),
                AbilityXmlSerialization.GenerateAbilityHotkeysElement(skin.abilityHotkeys)
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
                    !string.IsNullOrWhiteSpace(equipment.exportGroupKey) ? new XElement("exportGroupKey", equipment.exportGroupKey) : null,
                    !string.IsNullOrWhiteSpace(equipment.thingDefName) ? new XElement("thingDefName", equipment.thingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.parentThingDefName) ? new XElement("parentThingDefName", equipment.parentThingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.previewTexPath) ? new XElement("previewTexPath", equipment.previewTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.sourceNote) ? new XElement("sourceNote", equipment.sourceNote) : null,
                    !string.IsNullOrWhiteSpace(equipment.flyerThingDefName) ? new XElement("flyerThingDefName", equipment.flyerThingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.flyerClassName) && equipment.flyerClassName != "CharacterStudio.Abilities.CharacterStudioPawnFlyer_Default" ? new XElement("flyerClassName", equipment.flyerClassName) : null,
                    Math.Abs(equipment.flyerFlightSpeed - 22f) > 0.0001f ? new XElement("flyerFlightSpeed", equipment.flyerFlightSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    !string.IsNullOrWhiteSpace(equipment.worldTexPath) ? new XElement("worldTexPath", equipment.worldTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.wornTexPath) ? new XElement("wornTexPath", equipment.wornTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.maskTexPath) ? new XElement("maskTexPath", equipment.maskTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.shaderDefName) ? new XElement("shaderDefName", equipment.shaderDefName) : null,
                    equipment.useWornGraphicMask ? new XElement("useWornGraphicMask", equipment.useWornGraphicMask.ToString().ToLower()) : null,
                    new XElement("allowCrafting", equipment.allowCrafting.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(equipment.recipeDefName) ? new XElement("recipeDefName", equipment.recipeDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.recipeWorkbenchDefName) ? new XElement("recipeWorkbenchDefName", equipment.recipeWorkbenchDefName) : null,
                    Math.Abs(equipment.recipeWorkAmount - 1200f) > 0.0001f ? new XElement("recipeWorkAmount", equipment.recipeWorkAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    equipment.recipeProductCount != 1 ? new XElement("recipeProductCount", equipment.recipeProductCount) : null,
                    new XElement("allowTrading", equipment.allowTrading.ToString().ToLower()),
                    Math.Abs(equipment.marketValue - 250f) > 0.0001f ? new XElement("marketValue", equipment.marketValue.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    GenerateListElement("tags", equipment.tags),
                    GenerateListElement("tradeTags", equipment.tradeTags),
                    GenerateListElement("weaponTags", equipment.weaponTags),
                    GenerateListElement("weaponClasses", equipment.weaponClasses),
                    GenerateListElement("abilityDefNames", equipment.abilityDefNames),
                    GenerateListElement("thingCategories", equipment.thingCategories),
                    GenerateListElement("bodyPartGroups", equipment.bodyPartGroups),
                    GenerateListElement("apparelLayers", equipment.apparelLayers),
                    GenerateListElement("apparelTags", equipment.apparelTags),
                    GenerateEquipmentCostEntriesXml("recipeIngredients", equipment.recipeIngredients),
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

        private static XElement? GenerateEquipmentCostEntriesXml(string tagName, List<CharacterEquipmentCostEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var root = new XElement(tagName);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.thingDefName) || entry.count <= 0)
                {
                    continue;
                }

                root.Add(new XElement("li",
                    new XElement("thingDefName", entry.thingDefName),
                    new XElement("count", entry.count)
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
                !string.IsNullOrWhiteSpace(renderData.directionalFacing) ? new XElement("directionalFacing", renderData.directionalFacing) : null,
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
                new XElement("visible", renderData.visible.ToString().ToLower()),

                // 技能触发动画 (装备侧)
                renderData.useTriggeredLocalAnimation ? new XElement("useTriggeredLocalAnimation", "true") : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.triggerAbilityDefName) ? new XElement("triggerAbilityDefName", renderData.triggerAbilityDefName) : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.animationGroupKey) ? new XElement("animationGroupKey", renderData.animationGroupKey) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredAnimationRole", renderData.triggeredAnimationRole.ToString()) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredDeployAngle", renderData.triggeredDeployAngle) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredReturnAngle", renderData.triggeredReturnAngle) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredDeployTicks", renderData.triggeredDeployTicks) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredHoldTicks", renderData.triggeredHoldTicks) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredReturnTicks", renderData.triggeredReturnTicks) : null,
                renderData.useTriggeredLocalAnimation && renderData.triggeredPivotOffset != Vector2.zero ? new XElement("triggeredPivotOffset", $"({renderData.triggeredPivotOffset.x:F3}, {renderData.triggeredPivotOffset.y:F3})") : null,
                renderData.useTriggeredLocalAnimation && renderData.triggeredDeployOffset != Vector3.zero ? new XElement("triggeredDeployOffset", $"({renderData.triggeredDeployOffset.x:F3}, {renderData.triggeredDeployOffset.y:F3}, {renderData.triggeredDeployOffset.z:F3})") : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredUseVfxVisibility", renderData.triggeredUseVfxVisibility.ToString().ToLower()) : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.triggeredIdleTexPath) ? new XElement("triggeredIdleTexPath", renderData.triggeredIdleTexPath) : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.triggeredDeployTexPath) ? new XElement("triggeredDeployTexPath", renderData.triggeredDeployTexPath) : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.triggeredHoldTexPath) ? new XElement("triggeredHoldTexPath", renderData.triggeredHoldTexPath) : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.triggeredReturnTexPath) ? new XElement("triggeredReturnTexPath", renderData.triggeredReturnTexPath) : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.triggeredIdleMaskTexPath) ? new XElement("triggeredIdleMaskTexPath", renderData.triggeredIdleMaskTexPath) : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.triggeredDeployMaskTexPath) ? new XElement("triggeredDeployMaskTexPath", renderData.triggeredDeployMaskTexPath) : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.triggeredHoldMaskTexPath) ? new XElement("triggeredHoldMaskTexPath", renderData.triggeredHoldMaskTexPath) : null,
                renderData.useTriggeredLocalAnimation && !string.IsNullOrEmpty(renderData.triggeredReturnMaskTexPath) ? new XElement("triggeredReturnMaskTexPath", renderData.triggeredReturnMaskTexPath) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredVisibleDuringDeploy", renderData.triggeredVisibleDuringDeploy.ToString().ToLower()) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredVisibleDuringHold", renderData.triggeredVisibleDuringHold.ToString().ToLower()) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredVisibleDuringReturn", renderData.triggeredVisibleDuringReturn.ToString().ToLower()) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("triggeredVisibleOutsideCycle", renderData.triggeredVisibleOutsideCycle.ToString().ToLower()) : null,
                renderData.useTriggeredLocalAnimation && renderData.triggeredAnimationSouth != null ? GenerateTriggeredAnimationOverrideXml("triggeredAnimationSouth", renderData.triggeredAnimationSouth) : null,
                renderData.useTriggeredLocalAnimation && renderData.triggeredAnimationEastWest != null ? GenerateTriggeredAnimationOverrideXml("triggeredAnimationEastWest", renderData.triggeredAnimationEastWest) : null,
                renderData.useTriggeredLocalAnimation && renderData.triggeredAnimationNorth != null ? GenerateTriggeredAnimationOverrideXml("triggeredAnimationNorth", renderData.triggeredAnimationNorth) : null
            );
        }

        private static XElement GenerateTriggeredAnimationOverrideXml(string tagName, CharacterStudio.Core.EquipmentTriggeredAnimationOverride cfg)
        {
            return new XElement(tagName,
                new XElement("useTriggeredLocalAnimation", cfg.useTriggeredLocalAnimation.ToString().ToLower()),
                !string.IsNullOrEmpty(cfg.triggerAbilityDefName) ? new XElement("triggerAbilityDefName", cfg.triggerAbilityDefName) : null,
                !string.IsNullOrEmpty(cfg.animationGroupKey) ? new XElement("animationGroupKey", cfg.animationGroupKey) : null,
                new XElement("triggeredAnimationRole", cfg.triggeredAnimationRole.ToString()),
                new XElement("triggeredDeployAngle", cfg.triggeredDeployAngle),
                new XElement("triggeredReturnAngle", cfg.triggeredReturnAngle),
                new XElement("triggeredDeployTicks", cfg.triggeredDeployTicks),
                new XElement("triggeredHoldTicks", cfg.triggeredHoldTicks),
                new XElement("triggeredReturnTicks", cfg.triggeredReturnTicks),
                cfg.triggeredPivotOffset != Vector2.zero ? new XElement("triggeredPivotOffset", $"({cfg.triggeredPivotOffset.x:F3}, {cfg.triggeredPivotOffset.y:F3})") : null,
                cfg.triggeredDeployOffset != Vector3.zero ? new XElement("triggeredDeployOffset", $"({cfg.triggeredDeployOffset.x:F3}, {cfg.triggeredDeployOffset.y:F3}, {cfg.triggeredDeployOffset.z:F3})") : null,
                new XElement("triggeredUseVfxVisibility", cfg.triggeredUseVfxVisibility.ToString().ToLower()),
                !string.IsNullOrEmpty(cfg.triggeredIdleTexPath) ? new XElement("triggeredIdleTexPath", cfg.triggeredIdleTexPath) : null,
                !string.IsNullOrEmpty(cfg.triggeredDeployTexPath) ? new XElement("triggeredDeployTexPath", cfg.triggeredDeployTexPath) : null,
                !string.IsNullOrEmpty(cfg.triggeredHoldTexPath) ? new XElement("triggeredHoldTexPath", cfg.triggeredHoldTexPath) : null,
                !string.IsNullOrEmpty(cfg.triggeredReturnTexPath) ? new XElement("triggeredReturnTexPath", cfg.triggeredReturnTexPath) : null,
                !string.IsNullOrEmpty(cfg.triggeredIdleMaskTexPath) ? new XElement("triggeredIdleMaskTexPath", cfg.triggeredIdleMaskTexPath) : null,
                !string.IsNullOrEmpty(cfg.triggeredDeployMaskTexPath) ? new XElement("triggeredDeployMaskTexPath", cfg.triggeredDeployMaskTexPath) : null,
                !string.IsNullOrEmpty(cfg.triggeredHoldMaskTexPath) ? new XElement("triggeredHoldMaskTexPath", cfg.triggeredHoldMaskTexPath) : null,
                !string.IsNullOrEmpty(cfg.triggeredReturnMaskTexPath) ? new XElement("triggeredReturnMaskTexPath", cfg.triggeredReturnMaskTexPath) : null,
                new XElement("triggeredVisibleDuringDeploy", cfg.triggeredVisibleDuringDeploy.ToString().ToLower()),
                new XElement("triggeredVisibleDuringHold", cfg.triggeredVisibleDuringHold.ToString().ToLower()),
                new XElement("triggeredVisibleDuringReturn", cfg.triggeredVisibleDuringReturn.ToString().ToLower()),
                new XElement("triggeredVisibleOutsideCycle", cfg.triggeredVisibleOutsideCycle.ToString().ToLower())
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
                    new XElement("areaShape", ability.areaShape.ToString()),
                    !string.IsNullOrWhiteSpace(ability.irregularAreaPattern) ? new XElement("irregularAreaPattern", ability.irregularAreaPattern) : null,
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
                    new XElement("summonFactionType", effect.summonFactionType.ToString()),
                    !string.IsNullOrEmpty(effect.summonFactionDefName) ? new XElement("summonFactionDefName", effect.summonFactionDefName) : null,
                    effect.summonFactionDef != null ? new XElement("summonFactionDef", effect.summonFactionDef.defName) : null,
                    new XElement("summonCount", effect.summonCount),
                    new XElement("controlMode", effect.controlMode.ToString()),
                    new XElement("controlMoveDistance", effect.controlMoveDistance),
                    new XElement("terraformMode", effect.terraformMode.ToString()),
                    effect.terraformThingDef != null ? new XElement("terraformThingDef", effect.terraformThingDef.defName) : null,
                    effect.terraformTerrainDef != null ? new XElement("terraformTerrainDef", effect.terraformTerrainDef.defName) : null,
                    new XElement("terraformSpawnCount", effect.terraformSpawnCount),
                    new XElement("canHurtSelf", effect.canHurtSelf.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(effect.weatherDefName) ? new XElement("weatherDefName", effect.weatherDefName) : null,
                    new XElement("weatherDurationTicks", effect.weatherDurationTicks),
                    new XElement("weatherTransitionTicks", effect.weatherTransitionTicks)
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
                    new XElement("comboTargetHotkeySlot", component.comboTargetHotkeySlot.ToString()),
                    !string.IsNullOrWhiteSpace(component.comboTargetAbilityDefName) ? new XElement("comboTargetAbilityDefName", component.comboTargetAbilityDefName) : null,
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
                    new XElement("shieldVisualScale", component.shieldVisualScale),
                    new XElement("shieldVisualHeightOffset", component.shieldVisualHeightOffset),
                    !string.IsNullOrWhiteSpace(component.shieldInterceptorThingDefName) ? new XElement("shieldInterceptorThingDefName", component.shieldInterceptorThingDefName) : null,
                    new XElement("shieldInterceptorDurationTicks", component.shieldInterceptorDurationTicks),
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
                    new XElement("suppressCombatActionsDuringFlightState", component.suppressCombatActionsDuringFlightState.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(component.flyerThingDefName) ? new XElement("flyerThingDefName", component.flyerThingDefName) : null,
                    new XElement("flyerWarmupTicks", component.flyerWarmupTicks),
                    new XElement("launchFromCasterPosition", component.launchFromCasterPosition.ToString().ToLower()),
                    new XElement("requireValidTargetCell", component.requireValidTargetCell.ToString().ToLower()),
                    new XElement("storeTargetForFollowup", component.storeTargetForFollowup.ToString().ToLower()),
                    new XElement("enableFlightOnlyWindow", component.enableFlightOnlyWindow.ToString().ToLower()),
                    new XElement("flightOnlyWindowTicks", component.flightOnlyWindowTicks),
                    !string.IsNullOrWhiteSpace(component.flightOnlyAbilityDefName) ? new XElement("flightOnlyAbilityDefName", component.flightOnlyAbilityDefName) : null,
                    new XElement("hideCasterDuringTakeoff", component.hideCasterDuringTakeoff.ToString().ToLower()),
                    new XElement("autoExpireFlightMarkerOnLanding", component.autoExpireFlightMarkerOnLanding.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(component.requiredFlightSourceAbilityDefName) ? new XElement("requiredFlightSourceAbilityDefName", component.requiredFlightSourceAbilityDefName) : null,
                    new XElement("requireReservedTargetCell", component.requireReservedTargetCell.ToString().ToLower()),
                    new XElement("consumeFlightStateOnCast", component.consumeFlightStateOnCast.ToString().ToLower()),
                    new XElement("onlyUseDuringFlightWindow", component.onlyUseDuringFlightWindow.ToString().ToLower()),
                    new XElement("landingBurstRadius", component.landingBurstRadius),
                    new XElement("landingBurstDamage", component.landingBurstDamage),
                    component.landingBurstDamageDef != null ? new XElement("landingBurstDamageDef", component.landingBurstDamageDef.defName) : null,
                    !string.IsNullOrWhiteSpace(component.landingEffecterDefName) ? new XElement("landingEffecterDefName", component.landingEffecterDefName) : null,
                    !string.IsNullOrWhiteSpace(component.landingSoundDefName) ? new XElement("landingSoundDefName", component.landingSoundDefName) : null,
                    new XElement("affectBuildings", component.affectBuildings.ToString().ToLower()),
                    new XElement("affectCells", component.affectCells.ToString().ToLower()),
                    new XElement("knockbackTargets", component.knockbackTargets.ToString().ToLower()),
                    new XElement("knockbackDistance", component.knockbackDistance),
                    new XElement("timeStopDurationTicks", component.timeStopDurationTicks),
                    new XElement("freezeVisualsDuringTimeStop", component.freezeVisualsDuringTimeStop.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(component.weatherDefName) ? new XElement("weatherDefName", component.weatherDefName) : null,
                    new XElement("weatherDurationTicks", component.weatherDurationTicks),
                    new XElement("weatherTransitionTicks", component.weatherTransitionTicks)
                );

                root.Add(compEl);
            }

            return root;
        }

        private static XElement? GenerateAbilityHotkeysXml(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null) return null;

            var elements = new List<object> { new XElement("enabled", hotkeys.enabled.ToString().ToLower()) };
            foreach (var kvp in hotkeys.slotBindings)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                    elements.Add(new XElement(kvp.Key.ToLowerInvariant() + "AbilityDefName", kvp.Value));
            }
            return new XElement("abilityHotkeys", elements.ToArray());
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
            root.Add(new XElement("globalScale", baseAppearance.drawSizeScale.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            root.Add(new XElement("drawSizeScale", baseAppearance.drawSizeScale.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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
                    !string.IsNullOrEmpty(layer.customShaderPath) ? new XElement("customShaderPath", layer.customShaderPath) : null,
                    Math.Abs(layer.alpha - 1f) > 0.001f ? new XElement("alpha", layer.alpha.ToString("F3")) : null,
                    layer.zWrite ? new XElement("zWrite", "true") : null,

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
                    !string.IsNullOrWhiteSpace(layer.directionalFacing) ? new XElement("directionalFacing", layer.directionalFacing) : null,
                    layer.rotDrawMode != (RotDrawMode.Fresh | RotDrawMode.Rotting) ? new XElement("rotDrawMode", layer.rotDrawMode.ToString()) : null,
                    !layer.useDirectionalSuffix ? new XElement("useDirectionalSuffix", layer.useDirectionalSuffix.ToString().ToLower()) : null,
                    layer.useExpressionSuffix ? new XElement("useExpressionSuffix", layer.useExpressionSuffix.ToString().ToLower()) : null,
                    layer.useEyeDirectionSuffix ? new XElement("useEyeDirectionSuffix", layer.useEyeDirectionSuffix.ToString().ToLower()) : null,
                    layer.useBlinkSuffix ? new XElement("useBlinkSuffix", layer.useBlinkSuffix.ToString().ToLower()) : null,
                    layer.useFrameSequence ? new XElement("useFrameSequence", layer.useFrameSequence.ToString().ToLower()) : null,
                    layer.hideWhenMissingVariant ? new XElement("hideWhenMissingVariant", layer.hideWhenMissingVariant.ToString().ToLower()) : null,
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
                    layer.animationType == LayerAnimationType.Spin && layer.animPivotOffset != Vector2.zero ? new XElement("animPivotOffset", $"({layer.animPivotOffset.x:F3}, {layer.animPivotOffset.y:F3})") : null,

                    // Brownian
                    layer.animationType == LayerAnimationType.Brownian ? new XElement("brownianRadius", layer.brownianRadius) : null,
                    layer.animationType == LayerAnimationType.Brownian ? new XElement("brownianJitter", layer.brownianJitter) : null,
                    layer.animationType == LayerAnimationType.Brownian ? new XElement("brownianDamping", layer.brownianDamping) : null,
                    layer.animationType == LayerAnimationType.Brownian ? new XElement("brownianCombatRadius", layer.brownianCombatRadius) : null,
                    layer.animationType == LayerAnimationType.Brownian ? new XElement("brownianRespectWalkability", layer.brownianRespectWalkability.ToString().ToLower()) : null,
                    layer.animationType == LayerAnimationType.Brownian ? new XElement("brownianStayInRoom", layer.brownianStayInRoom.ToString().ToLower()) : null,

                    // 技能触发动画 (图层侧)
                    layer.useTriggeredEquipmentAnimation ? new XElement("useTriggeredEquipmentAnimation", "true") : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggerAbilityDefName) ? new XElement("triggerAbilityDefName", layer.triggerAbilityDefName) : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggeredAnimationGroupKey) ? new XElement("triggeredAnimationGroupKey", layer.triggeredAnimationGroupKey) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredAnimationRole", layer.triggeredAnimationRole.ToString()) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredDeployAngle", layer.triggeredDeployAngle) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredReturnAngle", layer.triggeredReturnAngle) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredDeployTicks", layer.triggeredDeployTicks) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredHoldTicks", layer.triggeredHoldTicks) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredReturnTicks", layer.triggeredReturnTicks) : null,
                    layer.useTriggeredEquipmentAnimation && layer.triggeredPivotOffset != Vector2.zero ? new XElement("triggeredPivotOffset", $"({layer.triggeredPivotOffset.x:F3}, {layer.triggeredPivotOffset.y:F3})") : null,
                    layer.useTriggeredEquipmentAnimation && layer.triggeredDeployOffset != Vector3.zero ? new XElement("triggeredDeployOffset", $"({layer.triggeredDeployOffset.x:F3}, {layer.triggeredDeployOffset.y:F3}, {layer.triggeredDeployOffset.z:F3})") : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredUseVfxVisibility", layer.triggeredUseVfxVisibility.ToString().ToLower()) : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggeredIdleTexPath) ? new XElement("triggeredIdleTexPath", layer.triggeredIdleTexPath) : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggeredDeployTexPath) ? new XElement("triggeredDeployTexPath", layer.triggeredDeployTexPath) : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggeredHoldTexPath) ? new XElement("triggeredHoldTexPath", layer.triggeredHoldTexPath) : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggeredReturnTexPath) ? new XElement("triggeredReturnTexPath", layer.triggeredReturnTexPath) : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggeredIdleMaskTexPath) ? new XElement("triggeredIdleMaskTexPath", layer.triggeredIdleMaskTexPath) : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggeredDeployMaskTexPath) ? new XElement("triggeredDeployMaskTexPath", layer.triggeredDeployMaskTexPath) : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggeredHoldMaskTexPath) ? new XElement("triggeredHoldMaskTexPath", layer.triggeredHoldMaskTexPath) : null,
                    layer.useTriggeredEquipmentAnimation && !string.IsNullOrEmpty(layer.triggeredReturnMaskTexPath) ? new XElement("triggeredReturnMaskTexPath", layer.triggeredReturnMaskTexPath) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredVisibleDuringDeploy", layer.triggeredVisibleDuringDeploy.ToString().ToLower()) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredVisibleDuringHold", layer.triggeredVisibleDuringHold.ToString().ToLower()) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredVisibleDuringReturn", layer.triggeredVisibleDuringReturn.ToString().ToLower()) : null,
                    layer.useTriggeredEquipmentAnimation ? new XElement("triggeredVisibleOutsideCycle", layer.triggeredVisibleOutsideCycle.ToString().ToLower()) : null,
                    layer.useTriggeredEquipmentAnimation && layer.triggeredAnimationSouth != null ? GenerateTriggeredAnimationOverrideXml("triggeredAnimationSouth", layer.triggeredAnimationSouth) : null,
                    layer.useTriggeredEquipmentAnimation && layer.triggeredAnimationEastWest != null ? GenerateTriggeredAnimationOverrideXml("triggeredAnimationEastWest", layer.triggeredAnimationEastWest) : null,
                    layer.useTriggeredEquipmentAnimation && layer.triggeredAnimationNorth != null ? GenerateTriggeredAnimationOverrideXml("triggeredAnimationNorth", layer.triggeredAnimationNorth) : null
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

            // 新增：幅度乘数
            if (config.browAmplitude != 1f) element.Add(new XElement("browAmplitude", config.browAmplitude.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (config.mouthAmplitude != 1f) element.Add(new XElement("mouthAmplitude", config.mouthAmplitude.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (config.pupilScaleAmplitude != 1f) element.Add(new XElement("pupilScaleAmplitude", config.pupilScaleAmplitude.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            // 新增：运动配置
            if (config.browMotion != null) element.Add(GenerateBrowMotionConfigXml(config.browMotion));
            if (config.mouthMotion != null) element.Add(GenerateMouthMotionConfigXml(config.mouthMotion));
            if (config.emotionOverlayMotion != null) element.Add(GenerateEmotionOverlayMotionConfigXml(config.emotionOverlayMotion));

            // 新增：规则配置
            if (config.expressionOverlayRules != null && config.expressionOverlayRules.Count > 0)
            {
                var rulesEl = new XElement("expressionOverlayRules");
                foreach (var rule in config.expressionOverlayRules)
                {
                    rulesEl.Add(new XElement("li",
                        new XElement("expression", rule.expression.ToString()),
                        new XElement("semanticKey", rule.semanticKey),
                        new XElement("emotionState", rule.emotionState)
                    ));
                }
                element.Add(rulesEl);
            }

            if (config.emotionOverlayRules != null && config.emotionOverlayRules.Count > 0)
            {
                var rulesEl = new XElement("emotionOverlayRules");
                foreach (var rule in config.emotionOverlayRules)
                {
                    var li = new XElement("li",
                        new XElement("semanticKey", rule.semanticKey),
                        new XElement("emotionState", rule.emotionState),
                        new XElement("overlayId", rule.overlayId)
                    );
                    if (rule.overlayIds != null && rule.overlayIds.Count > 0)
                    {
                        var idsEl = new XElement("overlayIds");
                        foreach (var id in rule.overlayIds) idsEl.Add(new XElement("li", id));
                        li.Add(idsEl);
                    }
                    rulesEl.Add(li);
                }
                element.Add(rulesEl);
            }

            // 眼睛方向
            if (config.eyeDirectionConfig != null
                && (config.eyeDirectionConfig.HasAnyTex()
                    || config.eyeDirectionConfig.upperLidMoveDown != 0.0044f
                    || config.eyeDirectionConfig.enabled))
            {
                var eyeEl = GenerateEyeDirectionConfigXml(config.eyeDirectionConfig);
                if (eyeEl != null) element.Add(eyeEl);
            }

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
                    new XElement("spatialMode", vfx.spatialMode.ToString()),
                    new XElement("anchorMode", vfx.anchorMode.ToString()),
                    new XElement("secondaryAnchorMode", vfx.secondaryAnchorMode.ToString()),
                    new XElement("pathMode", vfx.pathMode.ToString()),
                    new XElement("facingMode", vfx.facingMode.ToString()),
                    vfx.UsesCustomTextureType ? new XElement("textureSource", vfx.textureSource.ToString()) : null,
                    !string.IsNullOrEmpty(vfx.presetDefName) ? new XElement("presetDefName", vfx.presetDefName) : null,
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
                    new XElement("useCasterFacing", vfx.useCasterFacing.ToString().ToLower()),
                    new XElement("forwardOffset", vfx.forwardOffset),
                    new XElement("sideOffset", vfx.sideOffset),
                    new XElement("heightOffset", vfx.heightOffset),
                    new XElement("rotation", vfx.rotation),
                    vfx.textureScale != UnityEngine.Vector2.one ? new XElement("textureScale", $"({vfx.textureScale.x:F3}, {vfx.textureScale.y:F3})") : null,
                    new XElement("lineWidth", vfx.lineWidth),
                    new XElement("wallHeight", vfx.wallHeight),
                    new XElement("wallThickness", vfx.wallThickness),
                    new XElement("tileByLength", vfx.tileByLength.ToString().ToLower()),
                    new XElement("followGround", vfx.followGround.ToString().ToLower()),
                    new XElement("segmentCount", vfx.segmentCount),
                    new XElement("revealBySegments", vfx.revealBySegments.ToString().ToLower()),
                    new XElement("segmentRevealIntervalTicks", vfx.segmentRevealIntervalTicks),
                    vfx.offset != UnityEngine.Vector3.zero ? new XElement("offset", $"({vfx.offset.x:F3}, {vfx.offset.y:F3}, {vfx.offset.z:F3})") : null,
                    new XElement("repeatCount", vfx.repeatCount),
                    new XElement("repeatIntervalTicks", vfx.repeatIntervalTicks),
                    new XElement("attachToPawn", vfx.attachToPawn.ToString().ToLower()),
                    new XElement("attachToTargetCell", vfx.attachToTargetCell.ToString().ToLower()),
                    new XElement("playSound", vfx.playSound.ToString().ToLowerInvariant()),
                    !string.IsNullOrWhiteSpace(vfx.soundDefName) ? new XElement("soundDefName", vfx.soundDefName) : null,
                    new XElement("soundDelayTicks", vfx.soundDelayTicks),
                    new XElement("soundVolume", vfx.soundVolume),
                    new XElement("soundPitch", vfx.soundPitch),
                    // AssetBundle / VFX fields
                    !string.IsNullOrWhiteSpace(vfx.assetBundlePath) ? new XElement("assetBundlePath", vfx.assetBundlePath) : null,
                    !string.IsNullOrWhiteSpace(vfx.assetBundleEffectName) ? new XElement("assetBundleEffectName", vfx.assetBundleEffectName) : null,
                    !string.IsNullOrWhiteSpace(vfx.assetBundleTextureName) ? new XElement("assetBundleTextureName", vfx.assetBundleTextureName) : null,
                    new XElement("assetBundleEffectScale", vfx.assetBundleEffectScale),
                    new XElement("bundleRenderStrategy", vfx.bundleRenderStrategy.ToString()),
                    // Shader fields
                    !string.IsNullOrWhiteSpace(vfx.shaderPath) ? new XElement("shaderPath", vfx.shaderPath) : null,
                    !string.IsNullOrWhiteSpace(vfx.shaderAssetBundlePath) ? new XElement("shaderAssetBundlePath", vfx.shaderAssetBundlePath) : null,
                    !string.IsNullOrWhiteSpace(vfx.shaderAssetBundleShaderName) ? new XElement("shaderAssetBundleShaderName", vfx.shaderAssetBundleShaderName) : null,
                    new XElement("shaderLoadFromAssetBundle", vfx.shaderLoadFromAssetBundle.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(vfx.shaderTexturePath) ? new XElement("shaderTexturePath", vfx.shaderTexturePath) : null,
                    new XElement("shaderTintColor", $"({vfx.shaderTintColor.r:F3}, {vfx.shaderTintColor.g:F3}, {vfx.shaderTintColor.b:F3}, {vfx.shaderTintColor.a:F3})"),
                    new XElement("shaderIntensity", vfx.shaderIntensity),
                    new XElement("shaderSpeed", vfx.shaderSpeed),
                    new XElement("shaderParam1", vfx.shaderParam1),
                    new XElement("shaderParam2", vfx.shaderParam2),
                    new XElement("shaderParam3", vfx.shaderParam3),
                    new XElement("shaderParam4", vfx.shaderParam4),
                    // Global filter
                    new XElement("globalFilterMode", vfx.globalFilterMode),
                    new XElement("globalFilterTransition", vfx.globalFilterTransition),
                    new XElement("globalFilterTransitionTicks", vfx.globalFilterTransitionTicks)
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
            
            if (eyeCfg.upperLidMoveDown != 0.0044f) el.Add(new XElement("upperLidMoveDown", eyeCfg.upperLidMoveDown.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            if (eyeCfg.lidMotion != null) el.Add(GenerateLidMotionConfigXml(eyeCfg.lidMotion));
            if (eyeCfg.eyeMotion != null) el.Add(GenerateEyeMotionConfigXml(eyeCfg.eyeMotion));
            if (eyeCfg.pupilMotion != null) el.Add(GeneratePupilMotionConfigXml(eyeCfg.pupilMotion));

            return el;
        }

        private static XElement GenerateBrowMotionConfigXml(PawnFaceConfig.BrowMotionConfig cfg)
        {
            return new XElement("browMotion",
                new XElement("angryAngleBase", cfg.angryAngleBase),
                new XElement("angryAngleWave", cfg.angryAngleWave),
                new XElement("angryOffsetZBase", cfg.angryOffsetZBase),
                new XElement("angrySlowWaveOffsetZ", cfg.angrySlowWaveOffsetZ),
                new XElement("angryScaleX", cfg.angryScaleX),
                new XElement("angryScaleZ", cfg.angryScaleZ),
                new XElement("sadAngleBase", cfg.sadAngleBase),
                new XElement("sadAngleWave", cfg.sadAngleWave),
                new XElement("sadOffsetZBase", cfg.sadOffsetZBase),
                new XElement("sadSlowWaveOffsetZ", cfg.sadSlowWaveOffsetZ),
                new XElement("sadScaleX", cfg.sadScaleX),
                new XElement("sadScaleZ", cfg.sadScaleZ),
                new XElement("happyAngleBase", cfg.happyAngleBase),
                new XElement("happyAngleWave", cfg.happyAngleWave),
                new XElement("happyOffsetZBase", cfg.happyOffsetZBase),
                new XElement("happySlowWaveOffsetZ", cfg.happySlowWaveOffsetZ),
                new XElement("happyScaleX", cfg.happyScaleX),
                new XElement("happyScaleZ", cfg.happyScaleZ),
                new XElement("defaultSlowWaveOffsetZ", cfg.defaultSlowWaveOffsetZ)
            );
        }

        private static XElement GenerateMouthMotionConfigXml(PawnFaceConfig.MouthMotionConfig cfg)
        {
            return new XElement("mouthMotion",
                new XElement("smileAngleWave", cfg.smileAngleWave),
                new XElement("smileOffsetZBase", cfg.smileOffsetZBase),
                new XElement("smilePrimaryWaveOffsetZ", cfg.smilePrimaryWaveOffsetZ),
                new XElement("smileScaleXBase", cfg.smileScaleXBase),
                new XElement("smileScaleXWave", cfg.smileScaleXWave),
                new XElement("smileScaleZ", cfg.smileScaleZ),
                new XElement("openAngleWave", cfg.openAngleWave),
                new XElement("openOffsetZBase", cfg.openOffsetZBase),
                new XElement("openPrimaryWaveOffsetZ", cfg.openPrimaryWaveOffsetZ),
                new XElement("openScaleX", cfg.openScaleX),
                new XElement("openScaleZBase", cfg.openScaleZBase),
                new XElement("openScaleZWave", cfg.openScaleZWave),
                new XElement("downAngleBase", cfg.downAngleBase),
                new XElement("downAngleWave", cfg.downAngleWave),
                new XElement("downOffsetZBase", cfg.downOffsetZBase),
                new XElement("downSlowWaveOffsetZ", cfg.downSlowWaveOffsetZ),
                new XElement("downScaleX", cfg.downScaleX),
                new XElement("downScaleZ", cfg.downScaleZ),
                new XElement("sleepOffsetZ", cfg.sleepOffsetZ),
                new XElement("sleepScaleX", cfg.sleepScaleX),
                new XElement("sleepScaleZ", cfg.sleepScaleZ),
                new XElement("eatingAngleWave", cfg.eatingAngleWave),
                new XElement("eatingOffsetZBase", cfg.eatingOffsetZBase),
                new XElement("eatingPrimaryWaveOffsetZ", cfg.eatingPrimaryWaveOffsetZ),
                new XElement("eatingScaleX", cfg.eatingScaleX),
                new XElement("eatingScaleZBase", cfg.eatingScaleZBase),
                new XElement("eatingScaleZWave", cfg.eatingScaleZWave),
                new XElement("shockScaredAngleWave", cfg.shockScaredAngleWave),
                new XElement("shockScaredOffsetZBase", cfg.shockScaredOffsetZBase),
                new XElement("shockScaredPrimaryWaveOffsetZ", cfg.shockScaredPrimaryWaveOffsetZ),
                new XElement("shockScaredScaleX", cfg.shockScaredScaleX),
                new XElement("shockScaredScaleZBase", cfg.shockScaredScaleZBase),
                new XElement("shockScaredScaleZWave", cfg.shockScaredScaleZWave),
                new XElement("defaultSlowWaveOffsetZ", cfg.defaultSlowWaveOffsetZ)
            );
        }

        private static XElement GenerateEmotionOverlayMotionConfigXml(PawnFaceConfig.EmotionOverlayMotionConfig cfg)
        {
            return new XElement("emotionOverlayMotion",
                new XElement("blushPulseBase", cfg.blushPulseBase),
                new XElement("blushPulseWave", cfg.blushPulseWave),
                new XElement("blushOffsetZBase", cfg.blushOffsetZBase),
                new XElement("blushSlowWaveOffsetZ", cfg.blushSlowWaveOffsetZ),
                new XElement("blushScaleZBase", cfg.blushScaleZBase),
                new XElement("blushScaleZWave", cfg.blushScaleZWave),
                new XElement("tearPulseBase", cfg.tearPulseBase),
                new XElement("tearPulseWave", cfg.tearPulseWave),
                new XElement("tearAngleWave", cfg.tearAngleWave),
                new XElement("tearOffsetZBase", cfg.tearOffsetZBase),
                new XElement("tearPrimaryWaveOffsetZ", cfg.tearPrimaryWaveOffsetZ),
                new XElement("sweatPulseBase", cfg.sweatPulseBase),
                new XElement("sweatPulseWave", cfg.sweatPulseWave),
                new XElement("sweatAngleWave", cfg.sweatAngleWave),
                new XElement("sweatOffsetXWave", cfg.sweatOffsetXWave),
                new XElement("sweatOffsetZBase", cfg.sweatOffsetZBase),
                new XElement("sweatSlowWaveOffsetZ", cfg.sweatSlowWaveOffsetZ)
            );
        }

        private static XElement GenerateLidMotionConfigXml(PawnEyeDirectionConfig.LidMotionConfig cfg)
        {
            return new XElement("lidMotion",
                new XElement("upperSideBiasX", cfg.upperSideBiasX),
                new XElement("upperBlinkScaleX", cfg.upperBlinkScaleX),
                new XElement("upperBlinkScaleZ", cfg.upperBlinkScaleZ),
                new XElement("upperCloseScaleX", cfg.upperCloseScaleX),
                new XElement("upperCloseScaleZ", cfg.upperCloseScaleZ),
                new XElement("upperHalfBaseOffsetSubtract", cfg.upperHalfBaseOffsetSubtract),
                new XElement("upperHalfNeutralSoftExtraOffset", cfg.upperHalfNeutralSoftExtraOffset),
                new XElement("upperHalfLookDownExtraOffset", cfg.upperHalfLookDownExtraOffset),
                new XElement("upperHalfScaredExtraOffset", cfg.upperHalfScaredExtraOffset),
                new XElement("upperHalfSlowWaveOffset", cfg.upperHalfSlowWaveOffset),
                new XElement("upperHalfScaleDefault", cfg.upperHalfScaleDefault),
                new XElement("upperHalfScaleNeutralSoft", cfg.upperHalfScaleNeutralSoft),
                new XElement("upperHalfScaleLookDown", cfg.upperHalfScaleLookDown),
                new XElement("upperHalfScaleScared", cfg.upperHalfScaleScared),
                new XElement("upperHappySoftOffset", cfg.upperHappySoftOffset),
                new XElement("upperHappyOpenOffset", cfg.upperHappyOpenOffset),
                new XElement("upperHappySoftScale", cfg.upperHappySoftScale),
                new XElement("upperHappyOpenScale", cfg.upperHappyOpenScale),
                new XElement("upperHappyScaleX", cfg.upperHappyScaleX),
                new XElement("upperHappyAngleBase", cfg.upperHappyAngleBase),
                new XElement("upperHappyAngleWave", cfg.upperHappyAngleWave),
                new XElement("upperHappySlowWaveOffset", cfg.upperHappySlowWaveOffset),
                new XElement("upperDefaultSlowWaveOffset", cfg.upperDefaultSlowWaveOffset),
                new XElement("upperBlinkClosingPhaseDuration", cfg.upperBlinkClosingPhaseDuration),
                new XElement("upperBlinkOpeningStart", cfg.upperBlinkOpeningStart),
                new XElement("upperBlinkOpeningDuration", cfg.upperBlinkOpeningDuration),
                new XElement("lowerSideBiasX", cfg.lowerSideBiasX),
                new XElement("lowerBlinkOffset", cfg.lowerBlinkOffset),
                new XElement("lowerBlinkScaleX", cfg.lowerBlinkScaleX),
                new XElement("lowerBlinkScaleZ", cfg.lowerBlinkScaleZ),
                new XElement("lowerCloseOffset", cfg.lowerCloseOffset),
                new XElement("lowerCloseScaleX", cfg.lowerCloseScaleX),
                new XElement("lowerCloseScaleZ", cfg.lowerCloseScaleZ),
                new XElement("lowerHalfOffset", cfg.lowerHalfOffset),
                new XElement("lowerHalfSlowWaveOffset", cfg.lowerHalfSlowWaveOffset),
                new XElement("lowerHalfScaleX", cfg.lowerHalfScaleX),
                new XElement("lowerHalfScaleZ", cfg.lowerHalfScaleZ),
                new XElement("lowerHappyAngleBase", cfg.lowerHappyAngleBase),
                new XElement("lowerHappyAngleWave", cfg.lowerHappyAngleWave),
                new XElement("lowerHappyOffset", cfg.lowerHappyOffset),
                new XElement("lowerHappySlowWaveOffset", cfg.lowerHappySlowWaveOffset),
                new XElement("lowerHappyScaleX", cfg.lowerHappyScaleX),
                new XElement("lowerHappyScaleZ", cfg.lowerHappyScaleZ),
                new XElement("lowerDefaultSlowWaveOffset", cfg.lowerDefaultSlowWaveOffset),
                new XElement("genericBlinkOffset", cfg.genericBlinkOffset),
                new XElement("genericBlinkScaleX", cfg.genericBlinkScaleX),
                new XElement("genericBlinkScaleZ", cfg.genericBlinkScaleZ),
                new XElement("genericCloseOffset", cfg.genericCloseOffset),
                new XElement("genericCloseScaleX", cfg.genericCloseScaleX),
                new XElement("genericCloseScaleZ", cfg.genericCloseScaleZ),
                new XElement("genericHalfOffset", cfg.genericHalfOffset),
                new XElement("genericHalfSlowWaveOffset", cfg.genericHalfSlowWaveOffset),
                new XElement("genericHalfScaleX", cfg.genericHalfScaleX),
                new XElement("genericHalfScaleZ", cfg.genericHalfScaleZ),
                new XElement("genericHappyAngleBase", cfg.genericHappyAngleBase),
                new XElement("genericHappyAngleWave", cfg.genericHappyAngleWave),
                new XElement("genericHappyOffset", cfg.genericHappyOffset),
                new XElement("genericHappySlowWaveOffset", cfg.genericHappySlowWaveOffset),
                new XElement("genericHappyScaleX", cfg.genericHappyScaleX),
                new XElement("genericHappyScaleZ", cfg.genericHappyScaleZ),
                new XElement("genericDefaultSlowWaveOffset", cfg.genericDefaultSlowWaveOffset),
                new XElement("genericDefaultScaleZBase", cfg.genericDefaultScaleZBase),
                new XElement("genericDefaultScaleZWaveAmplitude", cfg.genericDefaultScaleZWaveAmplitude)
            );
        }

        private static XElement GenerateEyeMotionConfigXml(PawnEyeDirectionConfig.EyeMotionConfig cfg)
        {
            return new XElement("eyeMotion",
                new XElement("sideBiasX", cfg.sideBiasX),
                new XElement("primaryWaveOffsetZ", cfg.primaryWaveOffsetZ),
                new XElement("dirLeftOffsetX", cfg.dirLeftOffsetX),
                new XElement("dirRightOffsetX", cfg.dirRightOffsetX),
                new XElement("dirUpOffsetZ", cfg.dirUpOffsetZ),
                new XElement("dirDownOffsetZ", cfg.dirDownOffsetZ),
                new XElement("neutralSoftOffsetZ", cfg.neutralSoftOffsetZ),
                new XElement("neutralLookDownOffsetZ", cfg.neutralLookDownOffsetZ),
                new XElement("neutralGlanceWaveOffsetX", cfg.neutralGlanceWaveOffsetX),
                new XElement("neutralGlanceSideOffsetX", cfg.neutralGlanceSideOffsetX),
                new XElement("workFocusDownOffsetZ", cfg.workFocusDownOffsetZ),
                new XElement("workFocusUpOffsetZ", cfg.workFocusUpOffsetZ),
                new XElement("happySoftOffsetZ", cfg.happySoftOffsetZ),
                new XElement("shockWideOffsetZ", cfg.shockWideOffsetZ),
                new XElement("scaredWideOffsetZ", cfg.scaredWideOffsetZ),
                new XElement("scaredWideWaveOffsetX", cfg.scaredWideWaveOffsetX),
                new XElement("scaredWideSideOffsetX", cfg.scaredWideSideOffsetX),
                new XElement("scaredFlinchOffsetZ", cfg.scaredFlinchOffsetZ),
                new XElement("scaredFlinchWaveOffsetX", cfg.scaredFlinchWaveOffsetX),
                new XElement("scaredFlinchSideOffsetX", cfg.scaredFlinchSideOffsetX),
                new XElement("baseAngleWave", cfg.baseAngleWave),
                new XElement("slowWaveOffsetZ", cfg.slowWaveOffsetZ),
                new XElement("scaleXBase", cfg.scaleXBase),
                new XElement("scaleXWaveAmplitude", cfg.scaleXWaveAmplitude)
            );
        }

        private static XElement GeneratePupilMotionConfigXml(PawnEyeDirectionConfig.PupilMotionConfig cfg)
        {
            return new XElement("pupilMotion",
                new XElement("leftPupil_frontBaseX", cfg.leftPupil_frontBaseX),
                new XElement("leftPupil_dirLeftX", cfg.leftPupil_dirLeftX),
                new XElement("leftPupil_dirRightX", cfg.leftPupil_dirRightX),
                new XElement("leftPupil_dirUpZ", cfg.leftPupil_dirUpZ),
                new XElement("leftPupil_dirDownZ", cfg.leftPupil_dirDownZ),
                new XElement("rightPupil_frontBaseX", cfg.rightPupil_frontBaseX),
                new XElement("rightPupil_dirLeftX", cfg.rightPupil_dirLeftX),
                new XElement("rightPupil_dirRightX", cfg.rightPupil_dirRightX),
                new XElement("rightPupil_dirUpZ", cfg.rightPupil_dirUpZ),
                new XElement("rightPupil_dirDownZ", cfg.rightPupil_dirDownZ),
                new XElement("side_baseX", cfg.side_baseX),
                new XElement("side_dirLeftX", cfg.side_dirLeftX),
                new XElement("side_dirRightX", cfg.side_dirRightX),
                new XElement("side_dirUpZ", cfg.side_dirUpZ),
                new XElement("side_dirDownZ", cfg.side_dirDownZ),
                new XElement("sideBiasX", cfg.sideBiasX),
                new XElement("slowWaveOffsetZ", cfg.slowWaveOffsetZ),
                new XElement("neutralSoftOffsetZ", cfg.neutralSoftOffsetZ),
                new XElement("neutralLookDownOffsetZ", cfg.neutralLookDownOffsetZ),
                new XElement("neutralGlanceWaveOffsetX", cfg.neutralGlanceWaveOffsetX),
                new XElement("neutralGlanceSideOffsetX", cfg.neutralGlanceSideOffsetX),
                new XElement("workFocusDownOffsetZ", cfg.workFocusDownOffsetZ),
                new XElement("workFocusUpOffsetZ", cfg.workFocusUpOffsetZ),
                new XElement("happyOpenOffsetZ", cfg.happyOpenOffsetZ),
                new XElement("shockWideOffsetZ", cfg.shockWideOffsetZ),
                new XElement("scaredWideOffsetZ", cfg.scaredWideOffsetZ),
                new XElement("scaredWideWaveOffsetX", cfg.scaredWideWaveOffsetX),
                new XElement("scaredWideSideOffsetX", cfg.scaredWideSideOffsetX),
                new XElement("scaredFlinchOffsetZ", cfg.scaredFlinchOffsetZ),
                new XElement("scaredFlinchWaveOffsetX", cfg.scaredFlinchWaveOffsetX),
                new XElement("scaredFlinchSideOffsetX", cfg.scaredFlinchSideOffsetX),
                new XElement("transformAngleWave", cfg.transformAngleWave),
                new XElement("finalWaveOffsetX", cfg.finalWaveOffsetX),
                new XElement("focusScaleBase", cfg.focusScaleBase),
                new XElement("focusScaleWave", cfg.focusScaleWave),
                new XElement("slightlyContractedScaleBase", cfg.slightlyContractedScaleBase),
                new XElement("slightlyContractedScaleWave", cfg.slightlyContractedScaleWave),
                new XElement("contractedScaleBase", cfg.contractedScaleBase),
                new XElement("contractedScaleWave", cfg.contractedScaleWave),
                new XElement("dilatedScaleBase", cfg.dilatedScaleBase),
                new XElement("dilatedScaleWave", cfg.dilatedScaleWave),
                new XElement("dilatedMaxScaleBase", cfg.dilatedMaxScaleBase),
                new XElement("dilatedMaxScaleWave", cfg.dilatedMaxScaleWave),
                new XElement("scaredPulseScaleBase", cfg.scaredPulseScaleBase),
                new XElement("scaredPulseScaleWave", cfg.scaredPulseScaleWave),
                new XElement("shockScaredMinScaleBase", cfg.shockScaredMinScaleBase),
                new XElement("shockScaredMinScaleWave", cfg.shockScaredMinScaleWave),
                new XElement("happyMaxScaleBase", cfg.happyMaxScaleBase),
                new XElement("happyMaxScaleWave", cfg.happyMaxScaleWave),
                new XElement("sleepingScale", cfg.sleepingScale),
                new XElement("workFocusMaxScale", cfg.workFocusMaxScale),
                new XElement("neutralSoftMaxScale", cfg.neutralSoftMaxScale),
                new XElement("neutralLookDownMaxScale", cfg.neutralLookDownMaxScale),
                new XElement("shockWideMinScaleBase", cfg.shockWideMinScaleBase),
                new XElement("shockWideMinScaleWave", cfg.shockWideMinScaleWave),
                new XElement("scaredWideMinScaleBase", cfg.scaredWideMinScaleBase),
                new XElement("scaredWideMinScaleWave", cfg.scaredWideMinScaleWave),
                new XElement("scaredFlinchMinScaleBase", cfg.scaredFlinchMinScaleBase),
                new XElement("scaredFlinchMinScaleWave", cfg.scaredFlinchMinScaleWave)
            );
        }

        private static XElement GenerateAnimationConfigXml(CharacterStudio.Core.PawnAnimationConfig cfg)
        {
            var el = new XElement("animationConfig",
                new XElement("enabled", cfg.enabled.ToString().ToLower()),
                new XElement("weaponOverrideEnabled", cfg.weaponOverrideEnabled.ToString().ToLower()),
                new XElement("applyToOffHand", cfg.applyToOffHand.ToString().ToLower()),
                new XElement("scale", $"({cfg.scale.x:F3}, {cfg.scale.y:F3})")
            );

            if (cfg.procedural != null)
            {
                el.Add(new XElement("procedural",
                    new XElement("breathingEnabled", cfg.procedural.breathingEnabled.ToString().ToLower()),
                    new XElement("breathingSpeed", cfg.procedural.breathingSpeed),
                    new XElement("breathingAmplitude", cfg.procedural.breathingAmplitude),
                    new XElement("hoveringEnabled", cfg.procedural.hoveringEnabled.ToString().ToLower()),
                    new XElement("hoveringSpeed", cfg.procedural.hoveringSpeed),
                    new XElement("hoveringAmplitude", cfg.procedural.hoveringAmplitude)
                ));
            }

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