using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 导出 XML 片段写入器。
    /// 将 ModBuilder 中的 Def/XML 片段序列化职责集中到此处，避免构建流程类承担过多 XML 拼装细节。
    /// </summary>
    public static class ModExportXmlWriter
    {
        public static XElement? GenerateBaseAppearanceXml(BaseAppearanceConfig? baseAppearance)
        {
            if (baseAppearance == null || baseAppearance.slots == null || baseAppearance.slots.Count == 0)
            {
                return null;
            }

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
                    slot.colorSource == LayerColorSource.Fixed ? new XElement("customColor", FormatColor(slot.customColor)) : null,
                    new XElement("colorTwoSource", slot.colorTwoSource.ToString()),
                    slot.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", FormatColor(slot.customColorTwo)) : null,
                    new XElement("scale", FormatVector2(slot.scale)),
                    slot.scaleEastMultiplier != Vector2.one ? new XElement("scaleEastMultiplier", FormatVector2(slot.scaleEastMultiplier)) : null,
                    slot.scaleNorthMultiplier != Vector2.one ? new XElement("scaleNorthMultiplier", FormatVector2(slot.scaleNorthMultiplier)) : null,
                    new XElement("offset", FormatVector3(slot.offset)),
                    slot.offsetEast != Vector3.zero ? new XElement("offsetEast", FormatVector3(slot.offsetEast)) : null,
                    slot.offsetNorth != Vector3.zero ? new XElement("offsetNorth", FormatVector3(slot.offsetNorth)) : null,
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

        public static XElement GenerateLayersXml(List<PawnLayerConfig>? layers)
        {
            var element = new XElement("layers");

            if (layers == null || layers.Count == 0)
            {
                return element;
            }

            foreach (var layer in layers)
            {
                element.Add(new XElement("li", GeneratePawnLayerXmlContent(layer)));
            }

            return element;
        }

        public static XElement? GenerateEquipmentsXml(List<CharacterEquipmentDef>? equipments)
        {
            if (equipments == null || equipments.Count == 0)
            {
                return null;
            }

            var element = new XElement("equipments");
            foreach (var equipment in equipments)
            {
                if (equipment == null)
                {
                    continue;
                }

                equipment.EnsureDefaults();

                var equipmentEl = new XElement("li",
                    !string.IsNullOrWhiteSpace(equipment.defName) ? new XElement("defName", equipment.defName) : null,
                    !string.IsNullOrWhiteSpace(equipment.label) ? new XElement("label", equipment.label) : null,
                    !string.IsNullOrWhiteSpace(equipment.description) ? new XElement("description", equipment.description) : null,
                    new XElement("enabled", equipment.enabled.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(equipment.slotTag) ? new XElement("slotTag", equipment.slotTag) : null,
                    !string.IsNullOrWhiteSpace(equipment.linkedThingDefName) ? new XElement("linkedThingDefName", equipment.linkedThingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.previewTexPath) ? new XElement("previewTexPath", equipment.previewTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.sourceNote) ? new XElement("sourceNote", equipment.sourceNote) : null,
                    GenerateStringListXml("tags", equipment.tags),
                    GenerateStringListXml("abilityDefNames", equipment.abilityDefNames),
                    GenerateEquipmentVisualXml(equipment.visual)
                );

                element.Add(equipmentEl);
            }

            return element.HasElements ? element : null;
        }

        private static XElement? GenerateEquipmentVisualXml(PawnLayerConfig? layer)
        {
            if (layer == null)
            {
                return null;
            }

            return new XElement("visual", GeneratePawnLayerXmlContent(layer));
        }

        private static IEnumerable<object?> GeneratePawnLayerXmlContent(PawnLayerConfig layer)
        {
            yield return new XElement("layerName", layer.layerName ?? "");
            yield return new XElement("texPath", layer.texPath ?? "");
            yield return new XElement("anchorTag", layer.anchorTag ?? "Head");

            if (!string.IsNullOrEmpty(layer.anchorPath))
                yield return new XElement("anchorPath", layer.anchorPath);
            if (!string.IsNullOrEmpty(layer.maskTexPath))
                yield return new XElement("maskTexPath", layer.maskTexPath);
            if (!string.IsNullOrEmpty(layer.shaderDefName))
                yield return new XElement("shaderDefName", layer.shaderDefName);

            yield return new XElement("offset", FormatVector3(layer.offset));

            if (layer.offsetEast != Vector3.zero)
                yield return new XElement("offsetEast", FormatVector3(layer.offsetEast));
            if (layer.offsetNorth != Vector3.zero)
                yield return new XElement("offsetNorth", FormatVector3(layer.offsetNorth));

            yield return new XElement("drawOrder", layer.drawOrder);
            yield return new XElement("scale", FormatVector2(layer.scale));

            if (layer.scaleEastMultiplier != Vector2.one)
                yield return new XElement("scaleEastMultiplier", FormatVector2(layer.scaleEastMultiplier));
            if (layer.scaleNorthMultiplier != Vector2.one)
                yield return new XElement("scaleNorthMultiplier", FormatVector2(layer.scaleNorthMultiplier));

            yield return new XElement("rotation", layer.rotation);

            if (layer.rotationEastOffset != 0f)
                yield return new XElement("rotationEastOffset", layer.rotationEastOffset);
            if (layer.rotationNorthOffset != 0f)
                yield return new XElement("rotationNorthOffset", layer.rotationNorthOffset);

            yield return new XElement("flipHorizontal", layer.flipHorizontal.ToString().ToLower());
            yield return new XElement("colorSource", layer.colorSource.ToString());

            if (layer.colorSource == LayerColorSource.Fixed)
                yield return new XElement("customColor", FormatColor(layer.customColor));

            yield return new XElement("colorTwoSource", layer.colorTwoSource.ToString());

            if (layer.colorTwoSource == LayerColorSource.Fixed)
                yield return new XElement("customColorTwo", FormatColor(layer.customColorTwo));

            yield return new XElement("visible", layer.visible.ToString().ToLower());
            yield return new XElement("role", layer.role.ToString());
            yield return new XElement("variantLogic", layer.variantLogic.ToString());

            if (!string.IsNullOrEmpty(layer.variantBaseName))
                yield return new XElement("variantBaseName", layer.variantBaseName);
            if (!layer.useDirectionalSuffix)
                yield return new XElement("useDirectionalSuffix", layer.useDirectionalSuffix.ToString().ToLower());
            if (layer.useExpressionSuffix)
                yield return new XElement("useExpressionSuffix", layer.useExpressionSuffix.ToString().ToLower());
            if (layer.useEyeDirectionSuffix)
                yield return new XElement("useEyeDirectionSuffix", layer.useEyeDirectionSuffix.ToString().ToLower());
            if (layer.useBlinkSuffix)
                yield return new XElement("useBlinkSuffix", layer.useBlinkSuffix.ToString().ToLower());
            if (layer.useFrameSequence)
                yield return new XElement("useFrameSequence", layer.useFrameSequence.ToString().ToLower());
            if (layer.hideWhenMissingVariant)
                yield return new XElement("hideWhenMissingVariant", layer.hideWhenMissingVariant.ToString().ToLower());
            if (layer.eyeRenderMode != EyeRenderMode.TextureSwap)
                yield return new XElement("eyeRenderMode", layer.eyeRenderMode.ToString());
            if (layer.eyeUvMoveRange != 0f)
                yield return new XElement("eyeUvMoveRange", layer.eyeUvMoveRange.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));

            var visibleExpressions = GenerateStringArrayXml("visibleExpressions", layer.visibleExpressions);
            if (visibleExpressions != null)
                yield return visibleExpressions;

            var hiddenExpressions = GenerateStringArrayXml("hiddenExpressions", layer.hiddenExpressions);
            if (hiddenExpressions != null)
                yield return hiddenExpressions;

            if (layer.workerClass != null)
                yield return new XElement("workerClass", layer.workerClass.FullName);
            if (layer.graphicClass != null)
                yield return new XElement("graphicClass", layer.graphicClass.FullName);

            if (layer.animationType != LayerAnimationType.None)
            {
                yield return new XElement("animationType", layer.animationType.ToString());
                yield return new XElement("animFrequency", layer.animFrequency);
                yield return new XElement("animAmplitude", layer.animAmplitude);
                yield return new XElement("animSpeed", layer.animSpeed);
                yield return new XElement("animPhaseOffset", layer.animPhaseOffset);
                yield return new XElement("animAffectsOffset", layer.animAffectsOffset.ToString().ToLower());
                yield return new XElement("animOffsetAmplitude", layer.animOffsetAmplitude);
            }
        }

        public static XElement? GenerateStringListXml(string tagName, List<string>? values)
        {
            if (values == null || values.Count == 0)
            {
                return null;
            }

            var element = new XElement(tagName);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    element.Add(new XElement("li", value));
                }
            }

            return element.HasElements ? element : null;
        }

        public static XElement? GenerateStringArrayXml(string tagName, string[]? values)
        {
            if (values == null || values.Length == 0)
            {
                return null;
            }

            var element = new XElement(tagName);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    element.Add(new XElement("li", value));
                }
            }

            return element.HasElements ? element : null;
        }

        public static XElement GenerateFaceConfigXml(PawnFaceConfig config)
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
                foreach (var expression in config.expressions)
                {
                    if (expression == null) continue;

                    bool hasStatic = !string.IsNullOrEmpty(expression.texPath);
                    List<ExpressionFrame>? frames = expression.frames;
                    bool hasFrames = frames != null && frames.Count > 0;
                    if (!hasStatic && !hasFrames) continue;

                    var exprEl = new XElement("li",
                        new XElement("expression", expression.expression.ToString()),
                        hasStatic ? new XElement("texPath", expression.texPath) : null
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
                    if (part == null || string.IsNullOrWhiteSpace(part.texPath)) continue;

                    layeredPartsEl.Add(new XElement("li",
                        new XElement("partType", part.partType.ToString()),
                        new XElement("expression", part.expression.ToString()),
                        new XElement("texPath", part.texPath),
                        new XElement("enabled", part.enabled.ToString().ToLower()),
                        part.partType == LayeredFacePartType.Overlay && !string.IsNullOrWhiteSpace(part.overlayId)
                            ? new XElement("overlayId", part.overlayId)
                            : null,
                        part.partType == LayeredFacePartType.Overlay
                            ? new XElement("overlayOrder", part.overlayOrder)
                            : null,
                        part.anchorCorrection != Vector2.zero
                            ? new XElement("anchorCorrection", FormatVector2(part.anchorCorrection))
                            : null
                    ));
                }

                if (layeredPartsEl.HasElements) element.Add(layeredPartsEl);
            }

            // 序列化眼睛方向配置（嵌套在 faceConfig 内）
            if (config.eyeDirectionConfig != null && config.eyeDirectionConfig.HasAnyTex())
            {
                var eyeEl = GenerateEyeDirectionConfigXml(config.eyeDirectionConfig);
                if (eyeEl != null) element.Add(eyeEl);
            }

            return element;
        }

        public static XElement GenerateTargetRacesXml(List<string>? races)
        {
            var element = new XElement("targetRaces");

            if (races == null || races.Count == 0)
            {
                return element;
            }

            foreach (var race in races)
            {
                element.Add(new XElement("li", race));
            }

            return element;
        }

        public static XElement? GenerateSkinAbilitiesXml(List<ModularAbilityDef>? abilities)
        {
            if (abilities == null || abilities.Count == 0)
            {
                return null;
            }

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
                    GenerateSkinAbilityEffectsXml(ability.effects),
                    GenerateAbilityVisualEffectsXml(ability.visualEffects),
                    GenerateRuntimeComponentsXml(ability.runtimeComponents)
                );

                element.Add(abilityEl);
            }

            return element;
        }

        public static XElement? GenerateSkinAbilityEffectsXml(List<AbilityEffectConfig>? effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return null;
            }

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

        public static XElement? GenerateRuntimeComponentsXml(List<AbilityRuntimeComponentConfig>? components)
        {
            if (components == null || components.Count == 0)
            {
                return null;
            }

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
                    new XElement("requiredStacks", component.requiredStacks),
                    new XElement("delayTicks", component.delayTicks),
                    new XElement("wave1Radius", component.wave1Radius),
                    new XElement("wave1Damage", component.wave1Damage),
                    new XElement("wave2Radius", component.wave2Radius),
                    new XElement("wave2Damage", component.wave2Damage),
                    new XElement("wave3Radius", component.wave3Radius),
                    new XElement("wave3Damage", component.wave3Damage),
                    component.waveDamageDef != null ? new XElement("waveDamageDef", component.waveDamageDef.defName) : null
                );

                root.Add(compEl);
            }

            return root;
        }

        public static XElement? GenerateAbilityHotkeysXml(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null)
            {
                return null;
            }

            return new XElement("abilityHotkeys",
                new XElement("enabled", hotkeys.enabled.ToString().ToLower()),
                !string.IsNullOrEmpty(hotkeys.qAbilityDefName) ? new XElement("qAbilityDefName", hotkeys.qAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.wAbilityDefName) ? new XElement("wAbilityDefName", hotkeys.wAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.eAbilityDefName) ? new XElement("eAbilityDefName", hotkeys.eAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.rAbilityDefName) ? new XElement("rAbilityDefName", hotkeys.rAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.wComboAbilityDefName) ? new XElement("wComboAbilityDefName", hotkeys.wComboAbilityDefName) : null
            );
        }

        public static XElement? GenerateAbilityRefsXml(List<ModularAbilityDef>? abilities)
        {
            if (abilities == null || abilities.Count == 0)
            {
                return null;
            }

            var element = new XElement("abilities");
            foreach (var ability in abilities)
            {
                element.Add(new XElement("li", ability.defName));
            }

            return element;
        }

        public static XElement GenerateAbilityEffectsXml(List<AbilityEffectConfig> effects)
        {
            var element = new XElement("effects");
            foreach (var effect in effects)
            {
                var effectEl = new XElement("li",
                    new XElement("type", effect.type.ToString()),
                    new XElement("amount", effect.amount),
                    new XElement("duration", effect.duration),
                    new XElement("chance", effect.chance)
                );

                if (effect.damageDef != null)
                    effectEl.Add(new XElement("damageDef", effect.damageDef.defName));
                if (effect.hediffDef != null)
                    effectEl.Add(new XElement("hediffDef", effect.hediffDef.defName));
                if (effect.summonKind != null)
                    effectEl.Add(new XElement("summonKind", effect.summonKind.defName));
                if (effect.terraformThingDef != null)
                    effectEl.Add(new XElement("terraformThingDef", effect.terraformThingDef.defName));
                if (effect.terraformTerrainDef != null)
                    effectEl.Add(new XElement("terraformTerrainDef", effect.terraformTerrainDef.defName));

                effectEl.Add(new XElement("summonCount", effect.summonCount));
                effectEl.Add(new XElement("controlMode", effect.controlMode.ToString()));
                effectEl.Add(new XElement("controlMoveDistance", effect.controlMoveDistance));
                effectEl.Add(new XElement("terraformMode", effect.terraformMode.ToString()));
                effectEl.Add(new XElement("terraformSpawnCount", effect.terraformSpawnCount));
                element.Add(effectEl);
            }

            return element;
        }

        public static XElement? GenerateAbilityVisualEffectsXml(List<AbilityVisualEffectConfig>? visualEffects)
        {
            if (visualEffects == null || visualEffects.Count == 0)
            {
                return null;
            }

            var element = new XElement("visualEffects");
            foreach (var visualEffect in visualEffects)
            {
                if (visualEffect == null) continue;

                element.Add(new XElement("li",
                    new XElement("enabled", visualEffect.enabled.ToString().ToLower()),
                    new XElement("type", visualEffect.type.ToString()),
                    new XElement("sourceMode", visualEffect.sourceMode.ToString()),
                    !string.IsNullOrWhiteSpace(visualEffect.presetDefName) ? new XElement("presetDefName", visualEffect.presetDefName) : null,
                    new XElement("target", visualEffect.target.ToString()),
                    new XElement("trigger", visualEffect.trigger.ToString()),
                    new XElement("delayTicks", visualEffect.delayTicks),
                    new XElement("scale", visualEffect.scale),
                    new XElement("repeatCount", visualEffect.repeatCount),
                    new XElement("repeatIntervalTicks", visualEffect.repeatIntervalTicks),
                    visualEffect.offset != Vector3.zero ? new XElement("offset", FormatVector3(visualEffect.offset)) : null,
                    new XElement("attachToPawn", visualEffect.attachToPawn.ToString().ToLower()),
                    new XElement("attachToTargetCell", visualEffect.attachToTargetCell.ToString().ToLower())
                ));
            }

            return element;
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
                GenerateStringListXml("tags", attributes.tags),
                GenerateStringListXml("keyTraits", attributes.keyTraits),
                GenerateStringListXml("startingApparelDefs", attributes.startingApparelDefs)
            );
        }

        public static XDocument CreateSkinDefDocument(PawnSkinDef skin, string modName, string description, string author, string version, Func<string, string> sanitizeFileName)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs",
                    new XElement("CharacterStudio.Core.PawnSkinDef",
                        new XElement("defName", skin.defName ?? $"Skin_{sanitizeFileName(modName)}"),
                        new XElement("label", skin.label ?? modName),
                        new XElement("description", skin.description ?? description),
                        new XElement("hideVanillaHead", skin.hideVanillaHead.ToString().ToLower()),
                        new XElement("hideVanillaHair", skin.hideVanillaHair.ToString().ToLower()),
                        new XElement("hideVanillaBody", skin.hideVanillaBody.ToString().ToLower()),
                        new XElement("hideVanillaApparel", skin.hideVanillaApparel.ToString().ToLower()),
                        new XElement("humanlikeOnly", skin.humanlikeOnly.ToString().ToLower()),
                        new XElement("author", author),
                        new XElement("version", version),
                        !string.IsNullOrEmpty(skin.previewTexPath) ? new XElement("previewTexPath", skin.previewTexPath) : null,
                        GenerateAttributesXml(skin.attributes),
                        GenerateStringListXml("hiddenPaths", skin.hiddenPaths),
#pragma warning disable CS0618
                        GenerateStringListXml("hiddenTags", skin.hiddenTags),
#pragma warning restore CS0618
                        GenerateBaseAppearanceXml(skin.baseAppearance),
                        GenerateLayersXml(skin.layers),
                        GenerateTargetRacesXml(skin.targetRaces),
                        skin.applyAsDefaultForTargetRaces ? new XElement("applyAsDefaultForTargetRaces", "true") : null,
                        skin.defaultRacePriority != 0 ? new XElement("defaultRacePriority", skin.defaultRacePriority) : null,
                        skin.faceConfig != null && (skin.faceConfig.enabled || skin.faceConfig.HasAnyExpression() || skin.faceConfig.HasAnyLayeredPart() || skin.faceConfig.eyeDirectionConfig?.HasAnyTex() == true) ? GenerateFaceConfigXml(skin.faceConfig) : null,
                        skin.weaponRenderConfig != null && skin.weaponRenderConfig.enabled ? GenerateWeaponRenderConfigXml(skin.weaponRenderConfig) : null,
                        GenerateEquipmentsXml(skin.equipments),
                        GenerateSkinAbilitiesXml(skin.abilities),
                        GenerateAbilityHotkeysXml(skin.abilityHotkeys)
                    )
                )
            );
        }

        public static XDocument CreateGeneDefDocument(ModExportConfig config, string safeName)
        {
            if (config.SkinDef == null)
            {
                throw new ArgumentNullException(nameof(config), "导出配置缺少皮肤定义");
            }

            string geneDefName = $"CS_Gene_Face_{safeName}";
            string skinDefName = config.SkinDef.defName ?? $"Skin_{safeName}";
            string iconPath = string.IsNullOrEmpty(config.GeneIconPath)
                ? $"CS/{safeName}/Icon"
                : config.GeneIconPath;

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs",
                    new XElement("GeneDef",
                        new XElement("defName", geneDefName),
                        new XElement("label", $"Face: {config.ModName}"),
                        new XElement("description",
                            $"Changes the carrier's appearance to resemble {config.ModName}.\n\n" +
                            (config.OverlayMode
                                ? "This is an overlay effect that adds to the existing appearance."
                                : "This replaces the carrier's head appearance.")),
                        new XElement("iconPath", iconPath),
                        new XElement("biostatCpx", 0),
                        new XElement("biostatMet", 0),
                        new XElement("displayCategory", config.GeneCategory),
                        new XElement("displayOrderInCategory", 100),
                        new XElement("selectionWeight", 0),
                        new XElement("modExtensions",
                            new XElement("li", new XAttribute("Class", "CharacterStudio.Core.DefModExtension_SkinLink"),
                                new XElement("skinDefName", skinDefName),
                                new XElement("priority", 100),
                                new XElement("overlayMode", config.OverlayMode.ToString().ToLower()),
                                new XElement("hideVanillaHead", (!config.OverlayMode).ToString().ToLower()),
                                new XElement("hideVanillaHair", (!config.OverlayMode && (config.SkinDef?.hideVanillaHair ?? true)).ToString().ToLower())
                            )
                        )
                    )
                )
            );
        }

        public static XElement CreatePawnKindDefElement(string pawnKindName, ModExportConfig config, string skinDefName)
        {
            var pawnKindDef = new XElement("PawnKindDef",
                new XElement("defName", pawnKindName),
                new XElement("label", config.ModName),
                new XElement("race", "Human"),
                new XElement("combatPower", 100),
                new XElement("defaultFactionType", "PlayerColony")
            );

            var abilitiesXml = GenerateAbilityRefsXml(config.Abilities);
            if (abilitiesXml != null)
            {
                pawnKindDef.Add(abilitiesXml);
            }

            pawnKindDef.Add(new XElement("modExtensions",
                new XElement("li", new XAttribute("Class", "CharacterStudio.Core.DefModExtension_SkinLink"),
                    new XElement("skinDefName", skinDefName),
                    new XElement("priority", 100),
                    new XElement("hideVanillaHead", "true"),
                    new XElement("hideVanillaHair", "true")
                ),
                new XElement("li", new XAttribute("Class", "CharacterStudio.AI.CompProperties_CustomAI"),
                    new XElement("behavior",
                        new XElement("behaviorType", "Normal")
                    )
                )
            ));

            return pawnKindDef;
        }

        public static XDocument CreateUnitDefDocument(ModExportConfig config, string safeName)
        {
            if (config.SkinDef == null)
            {
                throw new ArgumentNullException(nameof(config), "导出配置缺少皮肤定义");
            }

            string pawnKindName = $"CS_PawnKind_{safeName}";
            string thingDefName = $"CS_Item_Summon_{safeName}";
            string skinDefName = config.SkinDef.defName ?? $"Skin_{safeName}";

            var defsRoot = new XElement("Defs",
                CreatePawnKindDefElement(pawnKindName, config, skinDefName)
            );

            // ── XenotypeDef（若皮肤绑定了 xenotypeDefName 则生成）────────────
            if (!string.IsNullOrEmpty(config.SkinDef.xenotypeDefName))
            {
                var xenoDef = BuildXenotypeDef(
                    config.SkinDef.xenotypeDefName,
                    string.IsNullOrEmpty(config.SkinDef.raceDisplayName)
                        ? config.ModName
                        : config.SkinDef.raceDisplayName,
                    skinDefName);
                defsRoot.AddFirst(xenoDef); // XenotypeDef 放在 PawnKindDef 前面
            }

            if (config.IncludeSummonItem)
            {
                defsRoot.Add(
                    new XElement("ThingDef", new XAttribute("ParentName", "ResourceBase"),
                        new XElement("defName", thingDefName),
                        new XElement("label", $"Summon: {config.ModName}"),
                        new XElement("description", $"Use to summon {config.ModName}."),
                        new XElement("graphicData",
                            new XElement("texPath", "Things/Item/Resource/UnfinishedComponent"),
                            new XElement("graphicClass", "Graphic_Single")
                        ),
                        new XElement("statBases",
                            new XElement("MarketValue", 1000),
                            new XElement("Mass", 0.5)
                        ),
                        new XElement("thingCategories",
                            new XElement("li", "Items")
                        ),
                        new XElement("comps",
                            new XElement("li", new XAttribute("Class", "CompProperties_Usable"),
                                new XElement("useJob", "UseItem"),
                                new XElement("useLabel", "Summon")
                            ),
                            new XElement("li", new XAttribute("Class", "CharacterStudio.Items.CompProperties_SummonCharacter"),
                                new XElement("pawnKind", pawnKindName),
                                new XElement("arrivalMode", "DropPod")
                            )
                        )
                    )
                );
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                defsRoot
            );
        }

        public static XDocument CreateAbilityDefDocument(List<ModularAbilityDef> abilities)
        {
            var element = new XElement("Defs");

            foreach (var ability in abilities)
            {
                var abilityDef = new XElement("AbilityDef",
                    new XElement("defName", ability.defName),
                    new XElement("label", ability.label),
                    new XElement("description", ability.description),
                    new XElement("iconPath", ability.iconPath),
                    new XElement("cooldownTicksRange", ability.cooldownTicks),
                    new XElement("verbProperties",
                        new XElement("warmupTime", ability.warmupTicks / 60f),
                        new XElement("range", ability.range),
                        new XElement("targetParams",
                            new XElement("canTargetPawns", "true"),
                            new XElement("canTargetLocations", "true")
                        )
                    ),
                    new XElement("comps",
                        new XElement("li", new XAttribute("Class", "CharacterStudio.Abilities.CompProperties_AbilityModular"),
                            GenerateAbilityEffectsXml(ability.effects),
                            GenerateAbilityVisualEffectsXml(ability.visualEffects),
                            GenerateRuntimeComponentsXml(ability.runtimeComponents)
                        )
                    )
                );

                var verbProps = abilityDef.Element("verbProperties");
                switch (ability.carrierType)
                {
                    case AbilityCarrierType.Self:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        verbProps.Add(new XElement("targetable", "false"));
                        break;
                    case AbilityCarrierType.Touch:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbilityTouch"));
                        verbProps.Add(new XElement("range", "-1"));
                        break;
                    case AbilityCarrierType.Target:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        break;
                    case AbilityCarrierType.Projectile:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        break;
                    case AbilityCarrierType.Area:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        verbProps.Add(new XElement("radius", ability.radius));
                        break;
                }

                element.Add(abilityDef);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), element);
        }

        public static XDocument CreateManifestDocument(ModExportConfig config, string generatorVersion)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Manifest",
                    new XElement("GeneratorVersion", generatorVersion),
                    new XElement("GeneratedAt", DateTime.UtcNow.ToString("o")),
                    new XElement("ExportMode", config.Mode.ToString()),
                    new XElement("ModName", config.ModName),
                    new XElement("ModVersion", config.Version),
                    new XElement("Author", config.Author),
                    new XElement("ExportSettings",
                        new XElement("IncludeSkinDef", config.IncludeSkinDef.ToString().ToLower()),
                        new XElement("IncludeGeneDef", config.IncludeGeneDef.ToString().ToLower()),
                        new XElement("IncludePawnKind", config.IncludePawnKind.ToString().ToLower()),
                        new XElement("IncludeSummonItem", config.IncludeSummonItem.ToString().ToLower()),
                        new XElement("IncludeAbilities", config.IncludeAbilities.ToString().ToLower()),
                        new XElement("CopyTextures", config.CopyTextures.ToString().ToLower()),
                        new XElement("ExportAsGene", config.ExportAsGene.ToString().ToLower()),
                        new XElement("OverlayMode", config.OverlayMode.ToString().ToLower())
                    ),
                    new XElement("AssetSources",
                        from source in config.AssetSources
                        select new XElement("Asset",
                            new XAttribute("type", source.SourceType.ToString()),
                            new XElement("OriginalPath", source.OriginalPath),
                            new XElement("ResolvedPath", source.ResolvedPath),
                            source.SourceModPackageId != null ? new XElement("SourceMod", source.SourceModPackageId) : null
                        )
                    ),
                    new XElement("Dependencies",
                        from dep in config.DetectedDependencies
                        select new XElement("Dependency", dep)
                    )
                )
            );
        }

        public static XElement? GenerateEyeDirectionConfigXml(PawnEyeDirectionConfig eyeCfg)
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

            return el;
        }

        public static XElement GenerateWeaponRenderConfigXml(WeaponRenderConfig cfg)
        {
            var el = new XElement("weaponRenderConfig",
                new XElement("enabled",         cfg.enabled.ToString().ToLower()),
                new XElement("applyToOffHand",  cfg.applyToOffHand.ToString().ToLower()),
                new XElement("scale",           FormatVector2(cfg.scale))
            );

            if (cfg.carryVisual != null && cfg.carryVisual.enabled) el.Add(GenerateWeaponCarryVisualXml(cfg.carryVisual));

            if (cfg.offset      != Vector3.zero) el.Add(new XElement("offset",      FormatVector3(cfg.offset)));
            if (cfg.offsetSouth != Vector3.zero) el.Add(new XElement("offsetSouth", FormatVector3(cfg.offsetSouth)));
            if (cfg.offsetNorth != Vector3.zero) el.Add(new XElement("offsetNorth", FormatVector3(cfg.offsetNorth)));
            if (cfg.offsetEast  != Vector3.zero) el.Add(new XElement("offsetEast",  FormatVector3(cfg.offsetEast)));

            return el;
        }

        private static XElement GenerateWeaponCarryVisualXml(WeaponCarryVisualConfig cfg)
        {
            var el = new XElement("carryVisual",
                new XElement("enabled", cfg.enabled.ToString().ToLower()),
                new XElement("anchorTag", cfg.anchorTag ?? "Body"),
                new XElement("scale", FormatVector2(cfg.scale)),
                new XElement("drawOrder", cfg.drawOrder.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))
            );

            if (!string.IsNullOrEmpty(cfg.texUndrafted)) el.Add(new XElement("texUndrafted", cfg.texUndrafted));
            if (!string.IsNullOrEmpty(cfg.texDrafted))   el.Add(new XElement("texDrafted", cfg.texDrafted));
            if (!string.IsNullOrEmpty(cfg.texCasting))   el.Add(new XElement("texCasting", cfg.texCasting));
            if (cfg.offset      != Vector3.zero) el.Add(new XElement("offset",      FormatVector3(cfg.offset)));
            if (cfg.offsetNorth != Vector3.zero) el.Add(new XElement("offsetNorth", FormatVector3(cfg.offsetNorth)));
            if (cfg.offsetEast  != Vector3.zero) el.Add(new XElement("offsetEast",  FormatVector3(cfg.offsetEast)));

            return el;
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
        }

        private static string FormatVector2(Vector2 value)
        {
            return $"({value.x:F2}, {value.y:F2})";
        }

        private static string FormatColor(Color value)
        {
            return $"({value.r:F3}, {value.g:F3}, {value.b:F3}, {value.a:F3})";
        }

        // ─────────────────────────────────────────────
        // XenotypeDef 生成
        // ─────────────────────────────────────────────

        /// <summary>
        /// 生成一个最小可用的 XenotypeDef XML 元素。
        /// 包含 defName、label，以及通过 modExtensions 关联皮肤的占位结构。
        /// 实际外观由运行时 CompPawnSkin 承载，XenotypeDef 主要作为身份标识。
        /// </summary>
        /// <param name="xenoDefName">XenotypeDef 的 defName</param>
        /// <param name="xenoLabel">显示名称</param>
        /// <param name="skinDefName">关联的 PawnSkinDef defName（写入注释供人工参考）</param>
        public static XElement BuildXenotypeDef(string xenoDefName, string xenoLabel, string skinDefName)
        {
            return new XElement("XenotypeDef",
                new XComment($" Generated by CharacterStudio — linked skin: {skinDefName} "),
                new XElement("defName", xenoDefName),
                new XElement("label", xenoLabel),
                new XElement("description", $"A xenotype defined by the CharacterStudio skin '{skinDefName}'."),
                // iconDef 留空，使用默认图标；作者可手动指定
                // genes 留空列表，外观完全由 CompPawnSkin 图层驱动
                new XElement("genes")
            );
        }

        /// <summary>
        /// 生成独立的 XenotypeDef XDocument（供 ModBuilder 写入单独文件使用）。
        /// </summary>
        public static XDocument CreateXenotypeDefDocument(string xenoDefName, string xenoLabel, string skinDefName)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs",
                    BuildXenotypeDef(xenoDefName, xenoLabel, skinDefName)
                )
            );
        }
    }
}
