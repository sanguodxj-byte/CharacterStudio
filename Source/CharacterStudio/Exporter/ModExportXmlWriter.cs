using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Items;
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
                    !string.IsNullOrWhiteSpace(equipment.exportGroupKey) ? new XElement("exportGroupKey", equipment.exportGroupKey) : null,
                    !string.IsNullOrWhiteSpace(equipment.thingDefName) ? new XElement("thingDefName", equipment.thingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.parentThingDefName) ? new XElement("parentThingDefName", equipment.parentThingDefName) : null,
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
                    !string.IsNullOrWhiteSpace(equipment.previewTexPath) ? new XElement("previewTexPath", equipment.previewTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.sourceNote) ? new XElement("sourceNote", equipment.sourceNote) : null,
                    !string.IsNullOrWhiteSpace(equipment.flyerThingDefName) ? new XElement("flyerThingDefName", equipment.flyerThingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.flyerClassName) ? new XElement("flyerClassName", equipment.flyerClassName) : null,
                    Math.Abs(equipment.flyerFlightSpeed - 22f) > 0.0001f ? new XElement("flyerFlightSpeed", equipment.flyerFlightSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    GenerateStringListXml("tags", equipment.tags),
                    GenerateStringListXml("tradeTags", equipment.tradeTags),
                    GenerateStringListXml("abilityDefNames", equipment.abilityDefNames),
                    GenerateStringListXml("thingCategories", equipment.thingCategories),
                    GenerateStringListXml("bodyPartGroups", equipment.bodyPartGroups),
                    GenerateStringListXml("apparelLayers", equipment.apparelLayers),
                    GenerateStringListXml("apparelTags", equipment.apparelTags),
                    GenerateEquipmentCostEntriesXml("recipeIngredients", equipment.recipeIngredients),
                    GenerateEquipmentStatEntriesXml("statBases", equipment.statBases),
                    GenerateEquipmentStatEntriesXml("equippedStatOffsets", equipment.equippedStatOffsets),
                    GenerateEquipmentRenderDataXml(equipment.renderData)
                );

                element.Add(equipmentEl);
            }

            return element.HasElements ? element : null;
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
            if (renderData == null)
            {
                return null;
            }

            return new XElement("renderData",
                !string.IsNullOrWhiteSpace(renderData.layerName) ? new XElement("layerName", renderData.layerName) : null,
                !string.IsNullOrWhiteSpace(renderData.texPath) ? new XElement("texPath", renderData.texPath) : null,
                !string.IsNullOrWhiteSpace(renderData.anchorTag) ? new XElement("anchorTag", renderData.anchorTag) : null,
                !string.IsNullOrWhiteSpace(renderData.anchorPath) ? new XElement("anchorPath", renderData.anchorPath) : null,
                !string.IsNullOrWhiteSpace(renderData.maskTexPath) ? new XElement("maskTexPath", renderData.maskTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.shaderDefName) ? new XElement("shaderDefName", renderData.shaderDefName) : null,
                !string.IsNullOrWhiteSpace(renderData.directionalFacing) ? new XElement("directionalFacing", renderData.directionalFacing) : null,
                new XElement("offset", FormatVector3(renderData.offset)),
                renderData.offsetEast != Vector3.zero ? new XElement("offsetEast", FormatVector3(renderData.offsetEast)) : null,
                renderData.offsetNorth != Vector3.zero ? new XElement("offsetNorth", FormatVector3(renderData.offsetNorth)) : null,
                new XElement("scale", FormatVector2(renderData.scale)),
                renderData.scaleEastMultiplier != Vector2.one ? new XElement("scaleEastMultiplier", FormatVector2(renderData.scaleEastMultiplier)) : null,
                renderData.scaleNorthMultiplier != Vector2.one ? new XElement("scaleNorthMultiplier", FormatVector2(renderData.scaleNorthMultiplier)) : null,
                new XElement("rotation", renderData.rotation),
                renderData.rotationEastOffset != 0f ? new XElement("rotationEastOffset", renderData.rotationEastOffset) : null,
                renderData.rotationNorthOffset != 0f ? new XElement("rotationNorthOffset", renderData.rotationNorthOffset) : null,
                new XElement("drawOrder", renderData.drawOrder),
                new XElement("flipHorizontal", renderData.flipHorizontal.ToString().ToLower()),
                new XElement("visible", renderData.visible.ToString().ToLower()),
                new XElement("colorSource", renderData.colorSource.ToString()),
                renderData.colorSource == LayerColorSource.Fixed ? new XElement("customColor", FormatColor(renderData.customColor)) : null,
                new XElement("colorTwoSource", renderData.colorTwoSource.ToString()),
                renderData.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", FormatColor(renderData.customColorTwo)) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("useTriggeredLocalAnimation", renderData.useTriggeredLocalAnimation.ToString().ToLower()) : null,
                !string.IsNullOrWhiteSpace(renderData.triggerAbilityDefName) ? new XElement("triggerAbilityDefName", renderData.triggerAbilityDefName) : null,
                !string.IsNullOrWhiteSpace(renderData.animationGroupKey) ? new XElement("animationGroupKey", renderData.animationGroupKey) : null,
                renderData.triggeredAnimationRole != EquipmentTriggeredAnimationRole.MovablePart ? new XElement("triggeredAnimationRole", renderData.triggeredAnimationRole.ToString()) : null,
                renderData.triggeredDeployAngle != 45f ? new XElement("triggeredDeployAngle", renderData.triggeredDeployAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                renderData.triggeredReturnAngle != 0f ? new XElement("triggeredReturnAngle", renderData.triggeredReturnAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                renderData.triggeredDeployTicks != 12 ? new XElement("triggeredDeployTicks", renderData.triggeredDeployTicks) : null,
                renderData.triggeredHoldTicks != 24 ? new XElement("triggeredHoldTicks", renderData.triggeredHoldTicks) : null,
                renderData.triggeredReturnTicks != 12 ? new XElement("triggeredReturnTicks", renderData.triggeredReturnTicks) : null,
                renderData.triggeredPivotOffset != Vector2.zero ? new XElement("triggeredPivotOffset", FormatVector2(renderData.triggeredPivotOffset)) : null,
                renderData.triggeredUseVfxVisibility ? new XElement("triggeredUseVfxVisibility", renderData.triggeredUseVfxVisibility.ToString().ToLower()) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredIdleTexPath) ? new XElement("triggeredIdleTexPath", renderData.triggeredIdleTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredDeployTexPath) ? new XElement("triggeredDeployTexPath", renderData.triggeredDeployTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredHoldTexPath) ? new XElement("triggeredHoldTexPath", renderData.triggeredHoldTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredReturnTexPath) ? new XElement("triggeredReturnTexPath", renderData.triggeredReturnTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredIdleMaskTexPath) ? new XElement("triggeredIdleMaskTexPath", renderData.triggeredIdleMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredDeployMaskTexPath) ? new XElement("triggeredDeployMaskTexPath", renderData.triggeredDeployMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredHoldMaskTexPath) ? new XElement("triggeredHoldMaskTexPath", renderData.triggeredHoldMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredReturnMaskTexPath) ? new XElement("triggeredReturnMaskTexPath", renderData.triggeredReturnMaskTexPath) : null,
                !renderData.triggeredVisibleDuringDeploy ? new XElement("triggeredVisibleDuringDeploy", renderData.triggeredVisibleDuringDeploy.ToString().ToLower()) : null,
                !renderData.triggeredVisibleDuringHold ? new XElement("triggeredVisibleDuringHold", renderData.triggeredVisibleDuringHold.ToString().ToLower()) : null,
                !renderData.triggeredVisibleDuringReturn ? new XElement("triggeredVisibleDuringReturn", renderData.triggeredVisibleDuringReturn.ToString().ToLower()) : null,
                !renderData.triggeredVisibleOutsideCycle ? new XElement("triggeredVisibleOutsideCycle", renderData.triggeredVisibleOutsideCycle.ToString().ToLower()) : null,
                GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationSouth", renderData.triggeredAnimationSouth),
                GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationEastWest", renderData.triggeredAnimationEastWest),
                GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationNorth", renderData.triggeredAnimationNorth)
            );
        }

        private static XElement? GenerateEquipmentTriggeredAnimationOverrideXml(string tagName, EquipmentTriggeredAnimationOverride? animation)
        {
            if (animation == null)
            {
                return null;
            }

            animation.EnsureDefaults(string.Empty, string.Empty, string.Empty);

            return new XElement(tagName,
                new XElement("useTriggeredLocalAnimation", animation.useTriggeredLocalAnimation.ToString().ToLower()),
                !string.IsNullOrWhiteSpace(animation.triggerAbilityDefName) ? new XElement("triggerAbilityDefName", animation.triggerAbilityDefName) : null,
                !string.IsNullOrWhiteSpace(animation.animationGroupKey) ? new XElement("animationGroupKey", animation.animationGroupKey) : null,
                new XElement("triggeredAnimationRole", animation.triggeredAnimationRole.ToString()),
                new XElement("triggeredDeployAngle", animation.triggeredDeployAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                animation.triggeredReturnAngle != 0f ? new XElement("triggeredReturnAngle", animation.triggeredReturnAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                new XElement("triggeredDeployTicks", animation.triggeredDeployTicks),
                new XElement("triggeredHoldTicks", animation.triggeredHoldTicks),
                new XElement("triggeredReturnTicks", animation.triggeredReturnTicks),
                animation.triggeredPivotOffset != Vector2.zero ? new XElement("triggeredPivotOffset", FormatVector2(animation.triggeredPivotOffset)) : null,
                new XElement("triggeredUseVfxVisibility", animation.triggeredUseVfxVisibility.ToString().ToLower()),
                !string.IsNullOrWhiteSpace(animation.triggeredIdleTexPath) ? new XElement("triggeredIdleTexPath", animation.triggeredIdleTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredDeployTexPath) ? new XElement("triggeredDeployTexPath", animation.triggeredDeployTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredHoldTexPath) ? new XElement("triggeredHoldTexPath", animation.triggeredHoldTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredReturnTexPath) ? new XElement("triggeredReturnTexPath", animation.triggeredReturnTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredIdleMaskTexPath) ? new XElement("triggeredIdleMaskTexPath", animation.triggeredIdleMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredDeployMaskTexPath) ? new XElement("triggeredDeployMaskTexPath", animation.triggeredDeployMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredHoldMaskTexPath) ? new XElement("triggeredHoldMaskTexPath", animation.triggeredHoldMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredReturnMaskTexPath) ? new XElement("triggeredReturnMaskTexPath", animation.triggeredReturnMaskTexPath) : null,
                new XElement("triggeredVisibleDuringDeploy", animation.triggeredVisibleDuringDeploy.ToString().ToLower()),
                new XElement("triggeredVisibleDuringHold", animation.triggeredVisibleDuringHold.ToString().ToLower()),
                new XElement("triggeredVisibleDuringReturn", animation.triggeredVisibleDuringReturn.ToString().ToLower()),
                new XElement("triggeredVisibleOutsideCycle", animation.triggeredVisibleOutsideCycle.ToString().ToLower())
            );
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
            if (!string.IsNullOrWhiteSpace(layer.directionalFacing))
                yield return new XElement("directionalFacing", layer.directionalFacing);
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
                if (layer.animationType == LayerAnimationType.Spin && layer.animPivotOffset != Vector2.zero)
                    yield return new XElement("animPivotOffset", $"({layer.animPivotOffset.x:F3}, {layer.animPivotOffset.y:F3})");
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
                    if (part == null) continue;

                    part.SyncDirectionalTexPathsFromLegacy();
                    part.SyncLegacyMotionAmplitude();
                    if (!part.HasAnyTexture()) continue;

                    layeredPartsEl.Add(new XElement("li",
                        new XElement("partType", part.partType.ToString()),
                        new XElement("expression", part.expression.ToString()),
                        new XElement("texPath", part.texPath),
                        !string.IsNullOrWhiteSpace(part.texPathSouth) ? new XElement("texPathSouth", part.texPathSouth) : null,
                        !string.IsNullOrWhiteSpace(part.texPathEast) ? new XElement("texPathEast", part.texPathEast) : null,
                        !string.IsNullOrWhiteSpace(part.texPathNorth) ? new XElement("texPathNorth", part.texPathNorth) : null,
                        new XElement("enabled", part.enabled.ToString().ToLower()),
                        PawnFaceConfig.NormalizePartSide(part.partType, part.side) != LayeredFacePartSide.None
                            ? new XElement("side", PawnFaceConfig.NormalizePartSide(part.partType, part.side).ToString())
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

            // 序列化眼睛方向配置（嵌套在 faceConfig 内）
            if (config.eyeDirectionConfig != null
                && (config.eyeDirectionConfig.HasAnyTex()
                    || config.eyeDirectionConfig.upperLidMoveDown != 0.0044f
                    || config.eyeDirectionConfig.enabled))
            {
                var eyeEl = GenerateEyeDirectionConfigXml(config.eyeDirectionConfig);
                if (eyeEl != null) element.Add(eyeEl);
            }

            if (config.browMotion != null)
                element.Add(new XElement("browMotion", DirectXmlToInnerElements(config.browMotion)));
            if (config.mouthMotion != null)
                element.Add(new XElement("mouthMotion", DirectXmlToInnerElements(config.mouthMotion)));
            if (config.emotionOverlayMotion != null)
                element.Add(new XElement("emotionOverlayMotion", DirectXmlToInnerElements(config.emotionOverlayMotion)));
            if (config.expressionOverlayRules != null && config.expressionOverlayRules.Count > 0)
                element.Add(new XElement("expressionOverlayRules", config.expressionOverlayRules.Select(rule => new XElement("li",
                    new XElement("expression", rule.expression.ToString()),
                    new XElement("emotionState", rule.emotionState ?? string.Empty)
                ))));
            if (config.emotionOverlayRules != null && config.emotionOverlayRules.Count > 0)
                element.Add(new XElement("emotionOverlayRules", config.emotionOverlayRules.Select(rule => new XElement("li",
                    new XElement("emotionState", rule.emotionState ?? string.Empty),
                    new XElement("overlayId", rule.overlayId ?? string.Empty)
                ))));

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

        private static IEnumerable<object> DirectXmlToInnerElements(object value)
        {
            string xml = Verse.DirectXmlSaver.XElementFromObject(value, value.GetType()).ToString();
            XElement element = XElement.Parse(xml);
            return element.Elements().Cast<object>().ToList();
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
                    new XElement("areaShape", ability.areaShape.ToString()),
                    !string.IsNullOrWhiteSpace(ability.irregularAreaPattern) ? new XElement("irregularAreaPattern", ability.irregularAreaPattern) : null,
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
                    component.flyerWarmupTicks != 0 ? new XElement("flyerWarmupTicks", component.flyerWarmupTicks) : null,
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
                !string.IsNullOrEmpty(hotkeys.tAbilityDefName) ? new XElement("tAbilityDefName", hotkeys.tAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.aAbilityDefName) ? new XElement("aAbilityDefName", hotkeys.aAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.sAbilityDefName) ? new XElement("sAbilityDefName", hotkeys.sAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.dAbilityDefName) ? new XElement("dAbilityDefName", hotkeys.dAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.fAbilityDefName) ? new XElement("fAbilityDefName", hotkeys.fAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.zAbilityDefName) ? new XElement("zAbilityDefName", hotkeys.zAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.xAbilityDefName) ? new XElement("xAbilityDefName", hotkeys.xAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.cAbilityDefName) ? new XElement("cAbilityDefName", hotkeys.cAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.vAbilityDefName) ? new XElement("vAbilityDefName", hotkeys.vAbilityDefName) : null
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
                if (effect.summonFactionDef != null)
                    effectEl.Add(new XElement("summonFactionDef", effect.summonFactionDef.defName));
                if (effect.terraformThingDef != null)
                    effectEl.Add(new XElement("terraformThingDef", effect.terraformThingDef.defName));
                if (effect.terraformTerrainDef != null)
                    effectEl.Add(new XElement("terraformTerrainDef", effect.terraformTerrainDef.defName));

                effectEl.Add(new XElement("summonCount", effect.summonCount));
                effectEl.Add(new XElement("controlMode", effect.controlMode.ToString()));
                effectEl.Add(new XElement("controlMoveDistance", effect.controlMoveDistance));
                effectEl.Add(new XElement("terraformMode", effect.terraformMode.ToString()));
                effectEl.Add(new XElement("terraformSpawnCount", effect.terraformSpawnCount));
                effectEl.Add(new XElement("canHurtSelf", effect.canHurtSelf.ToString().ToLower()));
                if (!string.IsNullOrWhiteSpace(effect.weatherDefName))
                    effectEl.Add(new XElement("weatherDefName", effect.weatherDefName));
                effectEl.Add(new XElement("weatherDurationTicks", effect.weatherDurationTicks));
                effectEl.Add(new XElement("weatherTransitionTicks", effect.weatherTransitionTicks));
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

                visualEffect.NormalizeLegacyData();
                visualEffect.SyncLegacyFields();
                element.Add(new XElement("li",
                    new XElement("enabled", visualEffect.enabled.ToString().ToLower()),
                    new XElement("type", visualEffect.type.ToString()),
                    new XElement("sourceMode", visualEffect.sourceMode.ToString()),
                    new XElement("spatialMode", visualEffect.spatialMode.ToString()),
                    new XElement("anchorMode", visualEffect.anchorMode.ToString()),
                    new XElement("secondaryAnchorMode", visualEffect.secondaryAnchorMode.ToString()),
                    new XElement("pathMode", visualEffect.pathMode.ToString()),
                    new XElement("facingMode", visualEffect.facingMode.ToString()),
                    visualEffect.UsesCustomTextureType ? new XElement("textureSource", visualEffect.textureSource.ToString()) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.presetDefName) ? new XElement("presetDefName", visualEffect.presetDefName) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.customTexturePath) ? new XElement("customTexturePath", visualEffect.customTexturePath) : null,
                    new XElement("target", visualEffect.target.ToString()),
                    new XElement("trigger", visualEffect.trigger.ToString()),
                    new XElement("delayTicks", visualEffect.delayTicks),
                    new XElement("displayDurationTicks", visualEffect.displayDurationTicks),
                    visualEffect.linkedExpression.HasValue ? new XElement("linkedExpression", visualEffect.linkedExpression.Value.ToString()) : null,
                    new XElement("linkedExpressionDurationTicks", visualEffect.linkedExpressionDurationTicks),
                    new XElement("linkedPupilBrightnessOffset", visualEffect.linkedPupilBrightnessOffset),
                    new XElement("linkedPupilContrastOffset", visualEffect.linkedPupilContrastOffset),
                    new XElement("scale", visualEffect.scale),
                    new XElement("drawSize", visualEffect.drawSize),
                    new XElement("useCasterFacing", visualEffect.useCasterFacing.ToString().ToLower()),
                    new XElement("forwardOffset", visualEffect.forwardOffset),
                    new XElement("sideOffset", visualEffect.sideOffset),
                    new XElement("heightOffset", visualEffect.heightOffset),
                    new XElement("rotation", visualEffect.rotation),
                    visualEffect.textureScale != Vector2.one ? new XElement("textureScale", $"({visualEffect.textureScale.x:F3}, {visualEffect.textureScale.y:F3})") : null,
                    new XElement("lineWidth", visualEffect.lineWidth),
                    new XElement("wallHeight", visualEffect.wallHeight),
                    new XElement("wallThickness", visualEffect.wallThickness),
                    new XElement("tileByLength", visualEffect.tileByLength.ToString().ToLower()),
                    new XElement("followGround", visualEffect.followGround.ToString().ToLower()),
                    new XElement("segmentCount", visualEffect.segmentCount),
                    new XElement("revealBySegments", visualEffect.revealBySegments.ToString().ToLower()),
                    new XElement("segmentRevealIntervalTicks", visualEffect.segmentRevealIntervalTicks),
                    visualEffect.offset != Vector3.zero ? new XElement("offset", FormatVector3(visualEffect.offset)) : null,
                    new XElement("repeatCount", visualEffect.repeatCount),
                    new XElement("repeatIntervalTicks", visualEffect.repeatIntervalTicks),
                    new XElement("attachToPawn", visualEffect.attachToPawn.ToString().ToLower()),
                    new XElement("attachToTargetCell", visualEffect.attachToTargetCell.ToString().ToLower()),
                    new XElement("playSound", visualEffect.playSound.ToString().ToLowerInvariant()),
                    !string.IsNullOrWhiteSpace(visualEffect.soundDefName) ? new XElement("soundDefName", visualEffect.soundDefName) : null,
                    new XElement("soundDelayTicks", visualEffect.soundDelayTicks),
                    new XElement("soundVolume", visualEffect.soundVolume),
                    new XElement("soundPitch", visualEffect.soundPitch)
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
                        new XElement("globalTextureScale", skin.globalTextureScale.ToString(System.Globalization.CultureInfo.InvariantCulture)),
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
                        skin.faceConfig != null && (skin.faceConfig.enabled || skin.faceConfig.HasAnyExpression() || skin.faceConfig.HasAnyLayeredPart() || skin.faceConfig.eyeDirectionConfig?.HasAnyTex() == true || skin.faceConfig.eyeDirectionConfig?.upperLidMoveDown != 0.0044f || skin.faceConfig.eyeDirectionConfig?.enabled == true) ? GenerateFaceConfigXml(skin.faceConfig) : null,
                        skin.animationConfig != null && skin.animationConfig.enabled ? GenerateAnimationConfigXml(skin.animationConfig) : null,
                        GenerateSkinAbilitiesXml(skin.abilities),
                        GenerateAbilityHotkeysXml(skin.abilityHotkeys)
                    )
                )
            );
        }

        public static XDocument CreateEquipmentThingDefsDocument(List<CharacterEquipmentDef>? equipments)
        {
            var defsRoot = new XElement("Defs");

            if (equipments != null)
            {
                foreach (var equipment in equipments)
                {
                    var equipmentEl = GenerateEquipmentThingDefXml(equipment);
                    if (equipmentEl != null)
                    {
                        defsRoot.Add(equipmentEl);
                    }

                    XElement? flyerDef = GenerateEquipmentFlyerThingDefXml(equipment);
                    if (flyerDef != null)
                    {
                        defsRoot.Add(flyerDef);
                    }
                }
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                defsRoot
            );
        }

        public static XElement? GenerateEquipmentThingDefXml(CharacterEquipmentDef? equipment)
        {
            if (equipment == null)
            {
                return null;
            }

            equipment.EnsureDefaults();

            string resolvedThingDefName = equipment.GetResolvedThingDefName();
            if (string.IsNullOrWhiteSpace(resolvedThingDefName))
            {
                return null;
            }

            var thingDef = new XElement("ThingDef",
                new XAttribute("ParentName", string.IsNullOrWhiteSpace(equipment.parentThingDefName) ? "ApparelMakeableBase" : equipment.parentThingDefName),
                new XElement("defName", resolvedThingDefName),
                new XElement("label", equipment.GetDisplayLabel()),
                !string.IsNullOrWhiteSpace(equipment.description) ? new XElement("description", equipment.description) : null,
                new XElement("tradeability", equipment.allowTrading ? "All" : "None"),
                GenerateEquipmentGraphicDataXml(equipment),
                GenerateEquipmentThingCategoriesXml(equipment.thingCategories),
                GenerateEquipmentTradeTagsXml(equipment.tradeTags),
                equipment.itemType == EquipmentType.WeaponMelee || equipment.itemType == EquipmentType.WeaponRanged ? GenerateWeaponTagsXml(equipment.weaponTags) : null,
                equipment.itemType == EquipmentType.WeaponMelee || equipment.itemType == EquipmentType.WeaponRanged ? GenerateWeaponClassesXml(equipment.weaponClasses) : null,
                GenerateEquipmentStatEntryContainerXml("statBases", equipment.statBases),
                GenerateEquipmentStatEntryContainerXml("equippedStatOffsets", equipment.equippedStatOffsets),
                equipment.itemType == EquipmentType.Apparel ? GenerateEquipmentApparelXml(equipment) : null,
                GenerateEquipmentModExtensionsXml(equipment)
            );

            XElement statBasesEl = thingDef.Element("statBases") ?? new XElement("statBases");
            if (thingDef.Element("statBases") == null)
                thingDef.Add(statBasesEl);
            if (statBasesEl.Element("MarketValue") == null)
            {
                statBasesEl.Add(new XElement("MarketValue", equipment.marketValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            return thingDef;
        }

        public static XElement? GenerateEquipmentRecipeDefXml(CharacterEquipmentDef? equipment)
        {
            if (equipment == null)
                return null;

            equipment.EnsureDefaults();
            if (!equipment.allowCrafting)
                return null;

            string resolvedThingDefName = equipment.GetResolvedThingDefName();
            string recipeDefName = string.IsNullOrWhiteSpace(equipment.recipeDefName)
                ? $"Recipe_{resolvedThingDefName}"
                : equipment.recipeDefName;

            var recipeDef = new XElement("RecipeDef",
                new XAttribute("ParentName", "MakeRecipeBase"),
                new XElement("defName", recipeDefName),
                new XElement("label", $"Make {equipment.GetDisplayLabel()}"),
                new XElement("jobString", $"Making {equipment.GetDisplayLabel()}"),
                new XElement("workAmount", equipment.recipeWorkAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("products",
                    new XElement("li",
                        new XElement("thingDef", resolvedThingDefName),
                        new XElement("count", equipment.recipeProductCount)
                    )),
                new XElement("recipeUsers",
                    new XElement("li", string.IsNullOrWhiteSpace(equipment.recipeWorkbenchDefName) ? "TableMachining" : equipment.recipeWorkbenchDefName))
            );

            XElement? ingredients = GenerateEquipmentRecipeIngredientsXml(equipment.recipeIngredients);
            if (ingredients != null)
                recipeDef.Add(ingredients);

            return recipeDef;
        }

        public static XDocument CreateEquipmentRecipeDefsDocument(List<CharacterEquipmentDef>? equipments)
        {
            var defsRoot = new XElement("Defs");

            if (equipments != null)
            {
                foreach (var equipment in equipments)
                {
                    XElement? recipeDef = GenerateEquipmentRecipeDefXml(equipment);
                    if (recipeDef != null)
                    {
                        defsRoot.Add(recipeDef);
                    }
                }
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                defsRoot
            );
        }

        public static XDocument CreateEquipmentBundleManifestDocument(List<CharacterEquipmentDef>? equipments)
        {
            var defsRoot = new XElement("Defs");
            if (equipments == null)
                return new XDocument(new XDeclaration("1.0", "utf-8", null), defsRoot);

            foreach (var group in equipments
                .Where(equipment => equipment != null && equipment.enabled && !string.IsNullOrWhiteSpace(equipment.exportGroupKey))
                .GroupBy(equipment => equipment.exportGroupKey, StringComparer.OrdinalIgnoreCase))
            {
                var manifest = new XElement("CharacterStudioEquipmentBundleDef",
                    new XElement("defName", $"CS_EquipBundle_{group.Key}"),
                    new XElement("label", group.Key),
                    new XElement("equipmentThingDefs",
                        group.Select(item => new XElement("li", item.GetResolvedThingDefName()))),
                    new XElement("startingApparelDefNames",
                        group.Select(item => new XElement("li", item.GetResolvedThingDefName()))));

                defsRoot.Add(manifest);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), defsRoot);
        }

        public static XElement? GenerateEquipmentFlyerThingDefXml(CharacterEquipmentDef? equipment)
        {
            if (equipment == null)
            {
                return null;
            }

            equipment.EnsureDefaults();
            if (string.IsNullOrWhiteSpace(equipment.flyerThingDefName))
            {
                return null;
            }

            string flyerClassName = string.IsNullOrWhiteSpace(equipment.flyerClassName)
                ? "CharacterStudio.Abilities.CharacterStudioPawnFlyer_Default"
                : equipment.flyerClassName;

            return new XElement("ThingDef",
                new XAttribute("ParentName", "PawnFlyerBase"),
                new XElement("defName", equipment.flyerThingDefName),
                new XElement("label", $"{equipment.GetDisplayLabel()} flyer"),
                new XElement("thingClass", flyerClassName),
                new XElement("drawOffscreen", "true"),
                new XElement("drawerType", "RealtimeOnly"),
                new XElement("altitudeLayer", "Pawn"),
                new XElement("pawnFlyer",
                    new XElement("flightDurationMin", "0.35"),
                    new XElement("flightSpeed", equipment.flyerFlightSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement("workerClass", "PawnFlyerWorker"),
                    new XElement("heightFactor", "0")
                )
            );
        }

        private static XElement? GenerateEquipmentGraphicDataXml(CharacterEquipmentDef equipment)
        {
            string texPath = string.IsNullOrWhiteSpace(equipment.worldTexPath)
                ? equipment.renderData?.GetResolvedTexPath() ?? string.Empty
                : equipment.worldTexPath;

            if (string.IsNullOrWhiteSpace(texPath))
            {
                return null;
            }

            return new XElement("graphicData",
                new XElement("texPath", texPath),
                !string.IsNullOrWhiteSpace(equipment.shaderDefName) ? new XElement("shaderType", equipment.shaderDefName) : null,
                new XElement("graphicClass", "Graphic_Single")
            );
        }

        private static XElement? GenerateEquipmentThingCategoriesXml(List<string>? categories)
        {
            return GenerateStringListXml("thingCategories", categories);
        }

        private static XElement? GenerateEquipmentTradeTagsXml(List<string>? tradeTags)
        {
            return GenerateStringListXml("tradeTags", tradeTags);
        }

        private static XElement? GenerateWeaponTagsXml(List<string>? weaponTags)
        {
            return GenerateStringListXml("weaponTags", weaponTags);
        }

        private static XElement? GenerateWeaponClassesXml(List<string>? weaponClasses)
        {
            return GenerateStringListXml("weaponClasses", weaponClasses);
        }

        private static XElement? GenerateEquipmentRecipeIngredientsXml(List<CharacterEquipmentCostEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
                return null;

            var ingredients = new XElement("ingredients");
            foreach (CharacterEquipmentCostEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.thingDefName) || entry.count <= 0)
                    continue;

                ingredients.Add(new XElement("li",
                    new XElement("filter",
                        new XElement("thingDefs",
                            new XElement("li", entry.thingDefName))),
                    new XElement("count", entry.count)));
            }

            return ingredients.HasElements ? ingredients : null;
        }

        private static XElement GenerateEquipmentApparelXml(CharacterEquipmentDef equipment)
        {
            return new XElement("apparel",
                !string.IsNullOrWhiteSpace(equipment.wornTexPath) ? new XElement("wornGraphicPath", equipment.wornTexPath) : null,
                equipment.useWornGraphicMask ? new XElement("useWornGraphicMask", equipment.useWornGraphicMask.ToString().ToLower()) : null,
                GenerateStringListXml("bodyPartGroups", equipment.bodyPartGroups),
                GenerateStringListXml("layers", equipment.apparelLayers),
                GenerateStringListXml("tags", equipment.apparelTags)
            );
        }

        private static XElement? GenerateEquipmentStatEntryContainerXml(string tagName, List<CharacterEquipmentStatEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var element = new XElement(tagName);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.statDefName))
                {
                    continue;
                }

                element.Add(new XElement(entry.statDefName, entry.value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            return element.HasElements ? element : null;
        }

        private static XElement GenerateEquipmentModExtensionsXml(CharacterEquipmentDef equipment)
        {
            var renderExtension = DefModExtension_EquipmentRender.FromEquipment(equipment);
            renderExtension.EnsureDefaults();

            return new XElement("modExtensions",
                new XElement("li",
                    new XAttribute("Class", "CharacterStudio.Core.DefModExtension_EquipmentRender"),
                    new XElement("equipmentDefName", renderExtension.equipmentDefName),
                    !string.IsNullOrWhiteSpace(renderExtension.label) ? new XElement("label", renderExtension.label) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.slotTag) ? new XElement("slotTag", renderExtension.slotTag) : null,
                    new XElement("enabled", renderExtension.enabled.ToString().ToLower()),
                    new XElement("texPath", renderExtension.texPath ?? string.Empty),
                    !string.IsNullOrWhiteSpace(renderExtension.maskTexPath) ? new XElement("maskTexPath", renderExtension.maskTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.directionalFacing) ? new XElement("directionalFacing", renderExtension.directionalFacing) : null,
                    new XElement("anchorTag", string.IsNullOrWhiteSpace(renderExtension.anchorTag) ? "Apparel" : renderExtension.anchorTag),
                    !string.IsNullOrWhiteSpace(renderExtension.anchorPath) ? new XElement("anchorPath", renderExtension.anchorPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.shaderDefName) ? new XElement("shaderDefName", renderExtension.shaderDefName) : null,
                    new XElement("offset", FormatVector3(renderExtension.offset)),
                    renderExtension.offsetEast != Vector3.zero ? new XElement("offsetEast", FormatVector3(renderExtension.offsetEast)) : null,
                    renderExtension.offsetNorth != Vector3.zero ? new XElement("offsetNorth", FormatVector3(renderExtension.offsetNorth)) : null,
                    new XElement("scale", FormatVector2(renderExtension.scale)),
                    renderExtension.scaleEastMultiplier != Vector2.one ? new XElement("scaleEastMultiplier", FormatVector2(renderExtension.scaleEastMultiplier)) : null,
                    renderExtension.scaleNorthMultiplier != Vector2.one ? new XElement("scaleNorthMultiplier", FormatVector2(renderExtension.scaleNorthMultiplier)) : null,
                    new XElement("rotation", renderExtension.rotation),
                    renderExtension.rotationEastOffset != 0f ? new XElement("rotationEastOffset", renderExtension.rotationEastOffset) : null,
                    renderExtension.rotationNorthOffset != 0f ? new XElement("rotationNorthOffset", renderExtension.rotationNorthOffset) : null,
                    new XElement("drawOrder", renderExtension.drawOrder),
                    new XElement("flipHorizontal", renderExtension.flipHorizontal.ToString().ToLower()),
                    new XElement("visible", renderExtension.visible.ToString().ToLower()),
                    new XElement("colorSource", renderExtension.colorSource.ToString()),
                    renderExtension.colorSource == LayerColorSource.Fixed ? new XElement("customColor", FormatColor(renderExtension.customColor)) : null,
                    new XElement("colorTwoSource", renderExtension.colorTwoSource.ToString()),
                    renderExtension.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", FormatColor(renderExtension.customColorTwo)) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.flyerThingDefName) ? new XElement("flyerThingDefName", renderExtension.flyerThingDefName) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.flyerClassName) ? new XElement("flyerClassName", renderExtension.flyerClassName) : null,
                    Math.Abs(renderExtension.flyerFlightSpeed - 1f) > 0.0001f ? new XElement("flyerFlightSpeed", renderExtension.flyerFlightSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    renderExtension.useTriggeredLocalAnimation ? new XElement("useTriggeredLocalAnimation", renderExtension.useTriggeredLocalAnimation.ToString().ToLower()) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggerAbilityDefName) ? new XElement("triggerAbilityDefName", renderExtension.triggerAbilityDefName) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.animationGroupKey) ? new XElement("animationGroupKey", renderExtension.animationGroupKey) : null,
                    renderExtension.triggeredAnimationRole != EquipmentTriggeredAnimationRole.MovablePart ? new XElement("triggeredAnimationRole", renderExtension.triggeredAnimationRole.ToString()) : null,
                    renderExtension.triggeredDeployAngle != 45f ? new XElement("triggeredDeployAngle", renderExtension.triggeredDeployAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    renderExtension.triggeredReturnAngle != 0f ? new XElement("triggeredReturnAngle", renderExtension.triggeredReturnAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    renderExtension.triggeredDeployTicks != 12 ? new XElement("triggeredDeployTicks", renderExtension.triggeredDeployTicks) : null,
                    renderExtension.triggeredHoldTicks != 24 ? new XElement("triggeredHoldTicks", renderExtension.triggeredHoldTicks) : null,
                    renderExtension.triggeredReturnTicks != 12 ? new XElement("triggeredReturnTicks", renderExtension.triggeredReturnTicks) : null,
                    renderExtension.triggeredPivotOffset != Vector2.zero ? new XElement("triggeredPivotOffset", FormatVector2(renderExtension.triggeredPivotOffset)) : null,
                    renderExtension.triggeredUseVfxVisibility ? new XElement("triggeredUseVfxVisibility", renderExtension.triggeredUseVfxVisibility.ToString().ToLower()) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredIdleTexPath) ? new XElement("triggeredIdleTexPath", renderExtension.triggeredIdleTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredDeployTexPath) ? new XElement("triggeredDeployTexPath", renderExtension.triggeredDeployTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredHoldTexPath) ? new XElement("triggeredHoldTexPath", renderExtension.triggeredHoldTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredReturnTexPath) ? new XElement("triggeredReturnTexPath", renderExtension.triggeredReturnTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredIdleMaskTexPath) ? new XElement("triggeredIdleMaskTexPath", renderExtension.triggeredIdleMaskTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredDeployMaskTexPath) ? new XElement("triggeredDeployMaskTexPath", renderExtension.triggeredDeployMaskTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredHoldMaskTexPath) ? new XElement("triggeredHoldMaskTexPath", renderExtension.triggeredHoldMaskTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredReturnMaskTexPath) ? new XElement("triggeredReturnMaskTexPath", renderExtension.triggeredReturnMaskTexPath) : null,
                    !renderExtension.triggeredVisibleDuringDeploy ? new XElement("triggeredVisibleDuringDeploy", renderExtension.triggeredVisibleDuringDeploy.ToString().ToLower()) : null,
                    !renderExtension.triggeredVisibleDuringHold ? new XElement("triggeredVisibleDuringHold", renderExtension.triggeredVisibleDuringHold.ToString().ToLower()) : null,
                    !renderExtension.triggeredVisibleDuringReturn ? new XElement("triggeredVisibleDuringReturn", renderExtension.triggeredVisibleDuringReturn.ToString().ToLower()) : null,
                    !renderExtension.triggeredVisibleOutsideCycle ? new XElement("triggeredVisibleOutsideCycle", renderExtension.triggeredVisibleOutsideCycle.ToString().ToLower()) : null,
                    GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationSouth", renderExtension.triggeredAnimationSouth),
                    GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationEastWest", renderExtension.triggeredAnimationEastWest),
                    GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationNorth", renderExtension.triggeredAnimationNorth),
                    GenerateStringListXml("abilityDefNames", renderExtension.abilityDefNames)
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
            string? raceDefName = config.CharacterDefinition?.raceDefName;
            if (string.IsNullOrWhiteSpace(raceDefName) && config.SkinDef?.targetRaces != null && config.SkinDef.targetRaces.Count > 0)
            {
                raceDefName = config.SkinDef.targetRaces[0];
            }
            raceDefName = string.IsNullOrWhiteSpace(raceDefName) ? "Human" : raceDefName;

            var pawnKindDef = new XElement("PawnKindDef",
                new XElement("defName", pawnKindName),
                new XElement("label", config.ModName),
                new XElement("race", raceDefName),
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
                        new XElement("label", $"Character: {config.ModName}"),
                        new XElement("description", $"Use this item to generate {config.ModName} on the map."),
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
                                new XElement("useLabel", "Generate Character")
                            ),
                            new XElement("li", new XAttribute("Class", "CharacterStudio.Items.CompProperties_SummonCharacter"),
                                new XElement("pawnKind", pawnKindName),
                                new XElement("skinDefName", skinDefName),
                                new XElement("characterDefFileName", $"{safeName}_Character.xml"),
                                new XElement("arrivalMode", config.RoleCardArrivalMode.ToString()),
                                new XElement("spawnEvent", config.RoleCardSpawnEvent.ToString()),
                                new XElement("spawnAnimation", config.RoleCardSpawnAnimation.ToString()),
                                new XElement("spawnAnimationScale", config.RoleCardSpawnAnimationScale.ToString(System.Globalization.CultureInfo.InvariantCulture))
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
                AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
                AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);

                var targetParams = normalizedTarget switch
                {
                    AbilityTargetType.Cell => new XElement("targetParams",
                        new XElement("canTargetPawns", "false"),
                        new XElement("canTargetBuildings", "false"),
                        new XElement("canTargetLocations", "true"),
                        new XElement("canTargetSelf", "false")
                    ),
                    AbilityTargetType.Entity => new XElement("targetParams",
                        new XElement("canTargetPawns", "true"),
                        new XElement("canTargetBuildings", "true"),
                        new XElement("canTargetLocations", "false"),
                        new XElement("canTargetSelf", "false")
                    ),
                    _ => new XElement("targetParams",
                        new XElement("canTargetPawns", "false"),
                        new XElement("canTargetBuildings", "false"),
                        new XElement("canTargetLocations", "false"),
                        new XElement("canTargetSelf", "true")
                    )
                };

                float exportedRange = normalizedCarrier == AbilityCarrierType.Self
                    ? 0f
                    : Mathf.Max(ability.range, 1f);

                var verbProps = new XElement("verbProperties",
                    new XElement("warmupTime", ability.warmupTicks / 60f),
                    new XElement("range", exportedRange),
                    targetParams
                );

                switch (normalizedCarrier)
                {
                    case AbilityCarrierType.Self:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        verbProps.Add(new XElement("targetable", "false"));
                        break;
                    case AbilityCarrierType.Projectile:
                        verbProps.Add(new XElement("verbClass", "Verb_LaunchProjectile"));
                        if (ability.projectileDef != null)
                        {
                            verbProps.Add(new XElement("defaultProjectile", ability.projectileDef.defName));
                        }
                        break;
                    default:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        break;
                }

                if (ability.useRadius && ability.radius > 0f)
                {
                    verbProps.Add(new XElement("radius", ability.radius));
                }

                var abilityDef = new XElement("AbilityDef",
                    new XElement("defName", ability.defName),
                    new XElement("label", ability.label),
                    new XElement("description", ability.description),
                    new XElement("iconPath", ability.iconPath),
                    new XElement("cooldownTicksRange", ability.cooldownTicks),
                    verbProps,
                    new XElement("comps",
                        new XElement("li", new XAttribute("Class", "CharacterStudio.Abilities.CompProperties_AbilityModular"),
                            GenerateAbilityEffectsXml(ability.effects),
                            GenerateAbilityVisualEffectsXml(ability.visualEffects),
                            GenerateRuntimeComponentsXml(ability.runtimeComponents)
                        )
                    )
                );

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
            if (!Mathf.Approximately(eyeCfg.upperLidMoveDown, 0.0044f))
                el.Add(new XElement("upperLidMoveDown", eyeCfg.upperLidMoveDown.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)));
            el.Add(new XElement("lidMotion",
                new XElement("upperSideBiasX", eyeCfg.lidMotion.upperSideBiasX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperBlinkScaleX", eyeCfg.lidMotion.upperBlinkScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperBlinkScaleZ", eyeCfg.lidMotion.upperBlinkScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperCloseScaleX", eyeCfg.lidMotion.upperCloseScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperCloseScaleZ", eyeCfg.lidMotion.upperCloseScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHalfBaseOffsetSubtract", eyeCfg.lidMotion.upperHalfBaseOffsetSubtract.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHalfNeutralSoftExtraOffset", eyeCfg.lidMotion.upperHalfNeutralSoftExtraOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHalfLookDownExtraOffset", eyeCfg.lidMotion.upperHalfLookDownExtraOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHalfScaredExtraOffset", eyeCfg.lidMotion.upperHalfScaredExtraOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHalfSlowWaveOffset", eyeCfg.lidMotion.upperHalfSlowWaveOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHalfScaleDefault", eyeCfg.lidMotion.upperHalfScaleDefault.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHalfScaleNeutralSoft", eyeCfg.lidMotion.upperHalfScaleNeutralSoft.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHalfScaleLookDown", eyeCfg.lidMotion.upperHalfScaleLookDown.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHalfScaleScared", eyeCfg.lidMotion.upperHalfScaleScared.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHappySoftOffset", eyeCfg.lidMotion.upperHappySoftOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHappyOpenOffset", eyeCfg.lidMotion.upperHappyOpenOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHappySoftScale", eyeCfg.lidMotion.upperHappySoftScale.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHappyOpenScale", eyeCfg.lidMotion.upperHappyOpenScale.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHappyScaleX", eyeCfg.lidMotion.upperHappyScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHappyAngleBase", eyeCfg.lidMotion.upperHappyAngleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHappyAngleWave", eyeCfg.lidMotion.upperHappyAngleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperHappySlowWaveOffset", eyeCfg.lidMotion.upperHappySlowWaveOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("upperDefaultSlowWaveOffset", eyeCfg.lidMotion.upperDefaultSlowWaveOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerSideBiasX", eyeCfg.lidMotion.lowerSideBiasX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerBlinkOffset", eyeCfg.lidMotion.lowerBlinkOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerBlinkScaleX", eyeCfg.lidMotion.lowerBlinkScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerBlinkScaleZ", eyeCfg.lidMotion.lowerBlinkScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerCloseOffset", eyeCfg.lidMotion.lowerCloseOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerCloseScaleX", eyeCfg.lidMotion.lowerCloseScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerCloseScaleZ", eyeCfg.lidMotion.lowerCloseScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHalfOffset", eyeCfg.lidMotion.lowerHalfOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHalfSlowWaveOffset", eyeCfg.lidMotion.lowerHalfSlowWaveOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHalfScaleX", eyeCfg.lidMotion.lowerHalfScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHalfScaleZ", eyeCfg.lidMotion.lowerHalfScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHappyAngleBase", eyeCfg.lidMotion.lowerHappyAngleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHappyAngleWave", eyeCfg.lidMotion.lowerHappyAngleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHappyOffset", eyeCfg.lidMotion.lowerHappyOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHappySlowWaveOffset", eyeCfg.lidMotion.lowerHappySlowWaveOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHappyScaleX", eyeCfg.lidMotion.lowerHappyScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerHappyScaleZ", eyeCfg.lidMotion.lowerHappyScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("lowerDefaultSlowWaveOffset", eyeCfg.lidMotion.lowerDefaultSlowWaveOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericBlinkOffset", eyeCfg.lidMotion.genericBlinkOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericBlinkScaleX", eyeCfg.lidMotion.genericBlinkScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericBlinkScaleZ", eyeCfg.lidMotion.genericBlinkScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericCloseOffset", eyeCfg.lidMotion.genericCloseOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericCloseScaleX", eyeCfg.lidMotion.genericCloseScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericCloseScaleZ", eyeCfg.lidMotion.genericCloseScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHalfOffset", eyeCfg.lidMotion.genericHalfOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHalfSlowWaveOffset", eyeCfg.lidMotion.genericHalfSlowWaveOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHalfScaleX", eyeCfg.lidMotion.genericHalfScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHalfScaleZ", eyeCfg.lidMotion.genericHalfScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHappyAngleBase", eyeCfg.lidMotion.genericHappyAngleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHappyAngleWave", eyeCfg.lidMotion.genericHappyAngleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHappyOffset", eyeCfg.lidMotion.genericHappyOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHappySlowWaveOffset", eyeCfg.lidMotion.genericHappySlowWaveOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHappyScaleX", eyeCfg.lidMotion.genericHappyScaleX.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericHappyScaleZ", eyeCfg.lidMotion.genericHappyScaleZ.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericDefaultSlowWaveOffset", eyeCfg.lidMotion.genericDefaultSlowWaveOffset.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericDefaultScaleZBase", eyeCfg.lidMotion.genericDefaultScaleZBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("genericDefaultScaleZWaveAmplitude", eyeCfg.lidMotion.genericDefaultScaleZWaveAmplitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))
            ));
            el.Add(new XElement("eyeMotion",
                new XElement("sideBiasX", eyeCfg.eyeMotion.sideBiasX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("primaryWaveOffsetZ", eyeCfg.eyeMotion.primaryWaveOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("dirLeftOffsetX", eyeCfg.eyeMotion.dirLeftOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("dirRightOffsetX", eyeCfg.eyeMotion.dirRightOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("dirUpOffsetZ", eyeCfg.eyeMotion.dirUpOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("dirDownOffsetZ", eyeCfg.eyeMotion.dirDownOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralSoftOffsetZ", eyeCfg.eyeMotion.neutralSoftOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralLookDownOffsetZ", eyeCfg.eyeMotion.neutralLookDownOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralGlanceWaveOffsetX", eyeCfg.eyeMotion.neutralGlanceWaveOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralGlanceSideOffsetX", eyeCfg.eyeMotion.neutralGlanceSideOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("workFocusDownOffsetZ", eyeCfg.eyeMotion.workFocusDownOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("workFocusUpOffsetZ", eyeCfg.eyeMotion.workFocusUpOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("happySoftOffsetZ", eyeCfg.eyeMotion.happySoftOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("shockWideOffsetZ", eyeCfg.eyeMotion.shockWideOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredWideOffsetZ", eyeCfg.eyeMotion.scaredWideOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredWideWaveOffsetX", eyeCfg.eyeMotion.scaredWideWaveOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredWideSideOffsetX", eyeCfg.eyeMotion.scaredWideSideOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredFlinchOffsetZ", eyeCfg.eyeMotion.scaredFlinchOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredFlinchWaveOffsetX", eyeCfg.eyeMotion.scaredFlinchWaveOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredFlinchSideOffsetX", eyeCfg.eyeMotion.scaredFlinchSideOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("baseAngleWave", eyeCfg.eyeMotion.baseAngleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("slowWaveOffsetZ", eyeCfg.eyeMotion.slowWaveOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaleXBase", eyeCfg.eyeMotion.scaleXBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaleXWaveAmplitude", eyeCfg.eyeMotion.scaleXWaveAmplitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))
            ));
            el.Add(new XElement("pupilMotion",
                new XElement("sideBiasX", eyeCfg.pupilMotion.sideBiasX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("slowWaveOffsetZ", eyeCfg.pupilMotion.slowWaveOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("leftPupil_frontBaseX", eyeCfg.pupilMotion.leftPupil_frontBaseX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("leftPupil_dirLeftX", eyeCfg.pupilMotion.leftPupil_dirLeftX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("leftPupil_dirRightX", eyeCfg.pupilMotion.leftPupil_dirRightX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("leftPupil_dirUpZ", eyeCfg.pupilMotion.leftPupil_dirUpZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("leftPupil_dirDownZ", eyeCfg.pupilMotion.leftPupil_dirDownZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("rightPupil_frontBaseX", eyeCfg.pupilMotion.rightPupil_frontBaseX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("rightPupil_dirLeftX", eyeCfg.pupilMotion.rightPupil_dirLeftX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("rightPupil_dirRightX", eyeCfg.pupilMotion.rightPupil_dirRightX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("rightPupil_dirUpZ", eyeCfg.pupilMotion.rightPupil_dirUpZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("rightPupil_dirDownZ", eyeCfg.pupilMotion.rightPupil_dirDownZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("side_baseX", eyeCfg.pupilMotion.side_baseX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("side_dirLeftX", eyeCfg.pupilMotion.side_dirLeftX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("side_dirRightX", eyeCfg.pupilMotion.side_dirRightX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("side_dirUpZ", eyeCfg.pupilMotion.side_dirUpZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("side_dirDownZ", eyeCfg.pupilMotion.side_dirDownZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralSoftOffsetZ", eyeCfg.pupilMotion.neutralSoftOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralLookDownOffsetZ", eyeCfg.pupilMotion.neutralLookDownOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralGlanceWaveOffsetX", eyeCfg.pupilMotion.neutralGlanceWaveOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralGlanceSideOffsetX", eyeCfg.pupilMotion.neutralGlanceSideOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("workFocusDownOffsetZ", eyeCfg.pupilMotion.workFocusDownOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("workFocusUpOffsetZ", eyeCfg.pupilMotion.workFocusUpOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("happyOpenOffsetZ", eyeCfg.pupilMotion.happyOpenOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("shockWideOffsetZ", eyeCfg.pupilMotion.shockWideOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredWideOffsetZ", eyeCfg.pupilMotion.scaredWideOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredWideWaveOffsetX", eyeCfg.pupilMotion.scaredWideWaveOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredWideSideOffsetX", eyeCfg.pupilMotion.scaredWideSideOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredFlinchOffsetZ", eyeCfg.pupilMotion.scaredFlinchOffsetZ.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredFlinchWaveOffsetX", eyeCfg.pupilMotion.scaredFlinchWaveOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredFlinchSideOffsetX", eyeCfg.pupilMotion.scaredFlinchSideOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("transformAngleWave", eyeCfg.pupilMotion.transformAngleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("finalWaveOffsetX", eyeCfg.pupilMotion.finalWaveOffsetX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("focusScaleBase", eyeCfg.pupilMotion.focusScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("focusScaleWave", eyeCfg.pupilMotion.focusScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("slightlyContractedScaleBase", eyeCfg.pupilMotion.slightlyContractedScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("slightlyContractedScaleWave", eyeCfg.pupilMotion.slightlyContractedScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("contractedScaleBase", eyeCfg.pupilMotion.contractedScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("contractedScaleWave", eyeCfg.pupilMotion.contractedScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("dilatedScaleBase", eyeCfg.pupilMotion.dilatedScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("dilatedScaleWave", eyeCfg.pupilMotion.dilatedScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("dilatedMaxScaleBase", eyeCfg.pupilMotion.dilatedMaxScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("dilatedMaxScaleWave", eyeCfg.pupilMotion.dilatedMaxScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredPulseScaleBase", eyeCfg.pupilMotion.scaredPulseScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredPulseScaleWave", eyeCfg.pupilMotion.scaredPulseScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("shockScaredMinScaleBase", eyeCfg.pupilMotion.shockScaredMinScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("shockScaredMinScaleWave", eyeCfg.pupilMotion.shockScaredMinScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("happyMaxScaleBase", eyeCfg.pupilMotion.happyMaxScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("happyMaxScaleWave", eyeCfg.pupilMotion.happyMaxScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("sleepingScale", eyeCfg.pupilMotion.sleepingScale.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("workFocusMaxScale", eyeCfg.pupilMotion.workFocusMaxScale.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralSoftMaxScale", eyeCfg.pupilMotion.neutralSoftMaxScale.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("neutralLookDownMaxScale", eyeCfg.pupilMotion.neutralLookDownMaxScale.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("shockWideMinScaleBase", eyeCfg.pupilMotion.shockWideMinScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("shockWideMinScaleWave", eyeCfg.pupilMotion.shockWideMinScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredWideMinScaleBase", eyeCfg.pupilMotion.scaredWideMinScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredWideMinScaleWave", eyeCfg.pupilMotion.scaredWideMinScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredFlinchMinScaleBase", eyeCfg.pupilMotion.scaredFlinchMinScaleBase.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("scaredFlinchMinScaleWave", eyeCfg.pupilMotion.scaredFlinchMinScaleWave.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))
            ));

            return el;
        }

        public static XElement GenerateAnimationConfigXml(PawnAnimationConfig cfg)
        {
            var el = new XElement("animationConfig",
                new XElement("enabled",               cfg.enabled.ToString().ToLower()),
                new XElement("weaponOverrideEnabled",  cfg.weaponOverrideEnabled.ToString().ToLower()),
                new XElement("applyToOffHand",        cfg.applyToOffHand.ToString().ToLower()),
                new XElement("scale",                 FormatVector2(cfg.scale))
            );

            if (cfg.procedural != null)
            {
                el.Add(new XElement("procedural",
                    new XElement("breathingEnabled",   cfg.procedural.breathingEnabled.ToString().ToLower()),
                    new XElement("breathingSpeed",     cfg.procedural.breathingSpeed),
                    new XElement("breathingAmplitude", cfg.procedural.breathingAmplitude),
                    new XElement("hoveringEnabled",    cfg.procedural.hoveringEnabled.ToString().ToLower()),
                    new XElement("hoveringSpeed",      cfg.procedural.hoveringSpeed),
                    new XElement("hoveringAmplitude",  cfg.procedural.hoveringAmplitude)
                ));
            }

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
