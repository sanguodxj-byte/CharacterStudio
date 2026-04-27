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
    public static partial class ModExportXmlWriter
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
            if (layer.useWestOffset)
                yield return new XElement("useWestOffset", "true");
            if (layer.offsetWest != Vector3.zero)
                yield return new XElement("offsetWest", FormatVector3(layer.offsetWest));

            yield return new XElement("drawOrder", layer.drawOrder);
            yield return new XElement("scale", FormatVector2(layer.scale));

            if (layer.scaleEastMultiplier != Vector2.one)
                yield return new XElement("scaleEastMultiplier", FormatVector2(layer.scaleEastMultiplier));
            if (layer.scaleNorthMultiplier != Vector2.one)
                yield return new XElement("scaleNorthMultiplier", FormatVector2(layer.scaleNorthMultiplier));
            if (layer.scaleWestMultiplier != Vector2.one)
                yield return new XElement("scaleWestMultiplier", FormatVector2(layer.scaleWestMultiplier));

            yield return new XElement("rotation", layer.rotation);

            if (layer.rotationEastOffset != 0f)
                yield return new XElement("rotationEastOffset", layer.rotationEastOffset);
            if (layer.rotationNorthOffset != 0f)
                yield return new XElement("rotationNorthOffset", layer.rotationNorthOffset);
            if (layer.rotationWestOffset != 0f)
                yield return new XElement("rotationWestOffset", layer.rotationWestOffset);

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
                        skin.animationConfig != null && skin.animationConfig.enabled ? GenerateAnimationConfigXml(skin.animationConfig) : null
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
    }
}
