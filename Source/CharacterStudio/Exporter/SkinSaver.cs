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
                skin.useCanDrawNowHiding ? new XElement("useCanDrawNowHiding", "true") : null,
                !string.IsNullOrEmpty(skin.author) ? new XElement("author", skin.author) : null,
                !string.IsNullOrEmpty(skin.version) ? new XElement("version", skin.version) : null,
                !string.IsNullOrEmpty(skin.previewTexPath) ? new XElement("previewTexPath", skin.previewTexPath) : null,
                !string.IsNullOrEmpty(skin.xenotypeDefName) ? new XElement("xenotypeDefName", skin.xenotypeDefName) : null,
                !string.IsNullOrEmpty(skin.raceDisplayName) ? new XElement("raceDisplayName", skin.raceDisplayName) : null,
                Math.Abs(skin.previewHeadOffsetZ) > 0.0001f ? new XElement("previewHeadOffsetZ", skin.previewHeadOffsetZ.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                skin.editLayerOffsetPerFacing ? new XElement("editLayerOffsetPerFacing", "true") : null,
                GenerateAttributesXml(skin.attributes),
                
                // 隐藏路径
                GenerateListElement("hiddenPaths", skin.hiddenPaths),
                // 隐藏标签（[Obsolete] 兼容导出）
                GenerateListElement("hiddenTags", skin.hiddenTags),

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

                GenerateStatModifiersXml(skin.statModifiers),
                null
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

                var equipmentEl = new XElement("li", AbilityXmlSerialization.SerializePublicFields(equipment));
                root.Add(equipmentEl);
            }

            return root.HasElements ? root : null;
        }

        private static XElement GenerateTriggeredAnimationOverrideXml(string tagName, CharacterStudio.Core.EquipmentTriggeredAnimationOverride cfg)
        {
            return new XElement(tagName, AbilityXmlSerialization.SerializePublicFields(cfg));
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
                    ability.useTwoPointTargeting ? new XElement("useTwoPointTargeting", "true") : null,
                    new XElement("useRadius", ability.useRadius.ToString().ToLower()),
                    new XElement("areaCenter", ability.areaCenter.ToString()),
                    new XElement("areaShape", ability.areaShape.ToString()),
                    !string.IsNullOrWhiteSpace(ability.irregularAreaPattern) ? new XElement("irregularAreaPattern", ability.irregularAreaPattern) : null,
                    new XElement("range", ability.range),
                    new XElement("radius", ability.radius),
                    ability.projectileDef != null ? new XElement("projectileDef", ability.projectileDef.defName) : null,
                    GenerateEffectsXml(ability.effects),
                    GenerateVisualEffectsXml(ability.visualEffects),
                    AbilityXmlSerialization.GenerateRuntimeComponentsElement(ability.runtimeComponents)
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
                    effect.delayTicks > 0 ? new XElement("delayTicks", effect.delayTicks) : null,
                    !string.IsNullOrWhiteSpace(effect.weatherDefName) ? new XElement("weatherDefName", effect.weatherDefName) : null,
                    new XElement("weatherDurationTicks", effect.weatherDurationTicks),
                    new XElement("weatherTransitionTicks", effect.weatherTransitionTicks)
                );

                effectsEl.Add(effectEl);
            }

            return effectsEl;
        }

        private static XElement? GenerateAbilityHotkeysXml(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null) return null;

            var elements = new List<object> { new XElement("enabled", hotkeys.enabled.ToString().ToLower()) };
            foreach (var kvp in hotkeys.slotBindings)
            {
                if (AbilityHotkeySlotUtility.IsSupportedSlotKey(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
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
            root.Add(new XElement("globalScale", baseAppearance.globalScale.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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
                    slot.scaleWestMultiplier != Vector2.one ? new XElement("scaleWestMultiplier", $"({slot.scaleWestMultiplier.x:F3}, {slot.scaleWestMultiplier.y:F3})") : null,
                    new XElement("offset", $"({slot.offset.x:F3}, {slot.offset.y:F3}, {slot.offset.z:F3})"),
                    slot.offsetEast != Vector3.zero ? new XElement("offsetEast", $"({slot.offsetEast.x:F3}, {slot.offsetEast.y:F3}, {slot.offsetEast.z:F3})") : null,
                    slot.offsetNorth != Vector3.zero ? new XElement("offsetNorth", $"({slot.offsetNorth.x:F3}, {slot.offsetNorth.y:F3}, {slot.offsetNorth.z:F3})") : null,
                    slot.useWestOffset ? new XElement("useWestOffset", "true") : null,
                    slot.offsetWest != Vector3.zero ? new XElement("offsetWest", $"({slot.offsetWest.x:F3}, {slot.offsetWest.y:F3}, {slot.offsetWest.z:F3})") : null,
                    new XElement("rotation", slot.rotation),
                    slot.rotationEastOffset != 0f ? new XElement("rotationEastOffset", slot.rotationEastOffset) : null,
                    slot.rotationNorthOffset != 0f ? new XElement("rotationNorthOffset", slot.rotationNorthOffset) : null,
                    slot.rotationWestOffset != 0f ? new XElement("rotationWestOffset", slot.rotationWestOffset) : null,
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
                    // 始终保存useWestOffset，确保导入后状态正确
                    new XElement("useWestOffset", layer.useWestOffset.ToString().ToLower()),
                    layer.offsetWest != Vector3.zero ? new XElement("offsetWest", $"({layer.offsetWest.x:F3}, {layer.offsetWest.y:F3}, {layer.offsetWest.z:F3})") : null,

                    new XElement("drawOrder", layer.drawOrder),
                    new XElement("scale", $"({layer.scale.x:F2}, {layer.scale.y:F2})"),
                    layer.scaleEastMultiplier != Vector2.one ? new XElement("scaleEastMultiplier", $"({layer.scaleEastMultiplier.x:F3}, {layer.scaleEastMultiplier.y:F3})") : null,
                    layer.scaleNorthMultiplier != Vector2.one ? new XElement("scaleNorthMultiplier", $"({layer.scaleNorthMultiplier.x:F3}, {layer.scaleNorthMultiplier.y:F3})") : null,
                    layer.scaleWestMultiplier != Vector2.one ? new XElement("scaleWestMultiplier", $"({layer.scaleWestMultiplier.x:F3}, {layer.scaleWestMultiplier.y:F3})") : null,
                    new XElement("rotation", layer.rotation),
                    layer.rotationEastOffset != 0f ? new XElement("rotationEastOffset", layer.rotationEastOffset) : null,
                    layer.rotationNorthOffset != 0f ? new XElement("rotationNorthOffset", layer.rotationNorthOffset) : null,
                    layer.rotationWestOffset != 0f ? new XElement("rotationWestOffset", layer.rotationWestOffset) : null,
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

                    part.EnsureDirectionalTexPathsConsistent();
                    part.EnsureMotionAmplitudeInitialized();
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
                vfx.NormalizeFieldConsistency();
                vfx.SyncDerivedFields();
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
            return new XElement("eyeDirectionConfig", AbilityXmlSerialization.SerializePublicFields(eyeCfg));
        }

        private static XElement GenerateBrowMotionConfigXml(PawnFaceConfig.BrowMotionConfig cfg)
        {
            return new XElement("browMotion", AbilityXmlSerialization.SerializePublicFields(cfg));
        }

        private static XElement GenerateMouthMotionConfigXml(PawnFaceConfig.MouthMotionConfig cfg)
        {
            return new XElement("mouthMotion", AbilityXmlSerialization.SerializePublicFields(cfg));
        }

        private static XElement GenerateEmotionOverlayMotionConfigXml(PawnFaceConfig.EmotionOverlayMotionConfig cfg)
        {
            return new XElement("emotionOverlayMotion", AbilityXmlSerialization.SerializePublicFields(cfg));
        }

        private static XElement GenerateLidMotionConfigXml(PawnEyeDirectionConfig.LidMotionConfig cfg)
        {
            return new XElement("lidMotion", AbilityXmlSerialization.SerializePublicFields(cfg));
        }

        private static XElement GenerateEyeMotionConfigXml(PawnEyeDirectionConfig.EyeMotionConfig cfg)
        {
            return new XElement("eyeMotion", AbilityXmlSerialization.SerializePublicFields(cfg));
        }

        private static XElement GeneratePupilMotionConfigXml(PawnEyeDirectionConfig.PupilMotionConfig cfg)
        {
            return new XElement("pupilMotion", AbilityXmlSerialization.SerializePublicFields(cfg));
        }

        private static XElement GenerateAnimationConfigXml(CharacterStudio.Core.PawnAnimationConfig cfg)
        {
            var el = new XElement("animationConfig", AbilityXmlSerialization.SerializePublicFields(cfg));

            // carryVisual 仅在 enabled 时输出（SerializePublicFields 无法表达此条件）
            if (cfg.carryVisual != null && cfg.carryVisual.enabled)
                el.Add(GenerateWeaponCarryVisualXml(cfg.carryVisual));

            return el;
        }

        private static XElement GenerateWeaponCarryVisualXml(CharacterStudio.Core.WeaponCarryVisualConfig cfg)
        {
            return new XElement("carryVisual", AbilityXmlSerialization.SerializePublicFields(cfg));
        }
    }
}
