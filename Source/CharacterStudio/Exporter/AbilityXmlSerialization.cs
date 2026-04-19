using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 技能能力数据 XML 序列化的单一事实来源。
    ///
    /// 用于统一：
    /// - 技能编辑器会话持久化
    /// - 皮肤工程保存
    /// - Mod/AbilityDef 导出时的共享能力块写出
    /// </summary>
    internal static class AbilityXmlSerialization
    {
        internal static XDocument CreateEditorSessionDocument(List<ModularAbilityDef>? abilities, SkinAbilityHotkeyConfig? hotkeys = null, string? selectedAbilityDefName = null)
        {
            var defs = new XElement("Defs");
            var skinRoot = new XElement("CharacterStudio.Core.PawnSkinDef");

            skinRoot.Add(GenerateAbilitiesElement(abilities) ?? new XElement("abilities"));

            XElement? hotkeysElement = GenerateAbilityHotkeysElement(hotkeys);
            if (hotkeysElement != null)
            {
                skinRoot.Add(hotkeysElement);
            }

            if (!string.IsNullOrWhiteSpace(selectedAbilityDefName))
            {
                skinRoot.Add(new XElement("_editorSelectedAbilityDefName", selectedAbilityDefName));
            }

            defs.Add(skinRoot);
            return new XDocument(defs);
        }

        /// <summary>
        /// 从会话 XML 文档中提取编辑器选中技能的 defName。
        /// 返回 null 表示未存储或无法解析。
        /// </summary>
        internal static string? ExtractSelectedAbilityDefName(System.Xml.Linq.XDocument doc)
        {
            if (doc?.Root == null)
            {
                return null;
            }

            var element = doc.Root.Descendants("_editorSelectedAbilityDefName").FirstOrDefault();
            return element?.Value?.Trim();
        }

        internal static XElement? GenerateAbilitiesElement(List<ModularAbilityDef>? abilities)
        {
            if (abilities == null || abilities.Count == 0)
            {
                return null;
            }

            var element = new XElement("abilities");
            foreach (ModularAbilityDef? ability in abilities)
            {
                XElement? abilityElement = GenerateAbilityElement(ability);
                if (abilityElement != null)
                {
                    element.Add(abilityElement);
                }
            }

            return element;
        }

        internal static XElement? GenerateAbilityEffectsElement(List<AbilityEffectConfig>? effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return null;
            }

            var effectsElement = new XElement("effects");
            foreach (AbilityEffectConfig? effect in effects)
            {
                if (effect == null)
                {
                    continue;
                }

                effectsElement.Add(new XElement("li",
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
                    new XElement("canHurtSelf", SerializeBool(effect.canHurtSelf)),
                    !string.IsNullOrWhiteSpace(effect.weatherDefName) ? new XElement("weatherDefName", effect.weatherDefName) : null,
                    new XElement("weatherDurationTicks", effect.weatherDurationTicks),
                    new XElement("weatherTransitionTicks", effect.weatherTransitionTicks)
                ));
            }

            return effectsElement;
        }

        internal static XElement? GenerateAbilityVisualEffectsElement(List<AbilityVisualEffectConfig>? visualEffects)
        {
            if (visualEffects == null || visualEffects.Count == 0)
            {
                return null;
            }

            var root = new XElement("visualEffects");
            foreach (AbilityVisualEffectConfig? visualEffect in visualEffects)
            {
                if (visualEffect == null)
                {
                    continue;
                }

                visualEffect.NormalizeLegacyData();
                visualEffect.SyncLegacyFields();

                root.Add(new XElement("li",
                    new XElement("enabled", SerializeBool(visualEffect.enabled)),
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
                    !string.IsNullOrWhiteSpace(visualEffect.assetBundlePath) ? new XElement("assetBundlePath", visualEffect.assetBundlePath) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.assetBundleEffectName) ? new XElement("assetBundleEffectName", visualEffect.assetBundleEffectName) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.assetBundleTextureName) ? new XElement("assetBundleTextureName", visualEffect.assetBundleTextureName) : null,
                    new XElement("assetBundleEffectScale", visualEffect.assetBundleEffectScale),
                    new XElement("bundleRenderStrategy", visualEffect.bundleRenderStrategy.ToString()),
                    !string.IsNullOrWhiteSpace(visualEffect.shaderPath) ? new XElement("shaderPath", visualEffect.shaderPath) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.shaderAssetBundlePath) ? new XElement("shaderAssetBundlePath", visualEffect.shaderAssetBundlePath) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.shaderAssetBundleShaderName) ? new XElement("shaderAssetBundleShaderName", visualEffect.shaderAssetBundleShaderName) : null,
                    new XElement("shaderLoadFromAssetBundle", SerializeBool(visualEffect.shaderLoadFromAssetBundle)),
                    !string.IsNullOrWhiteSpace(visualEffect.shaderTexturePath) ? new XElement("shaderTexturePath", visualEffect.shaderTexturePath) : null,
                    new XElement("shaderTintColor", XmlExportHelper.FormatColor(visualEffect.shaderTintColor)),
                    new XElement("shaderIntensity", visualEffect.shaderIntensity),
                    new XElement("shaderSpeed", visualEffect.shaderSpeed),
                    new XElement("shaderParam1", visualEffect.shaderParam1),
                    new XElement("shaderParam2", visualEffect.shaderParam2),
                    new XElement("shaderParam3", visualEffect.shaderParam3),
                    new XElement("shaderParam4", visualEffect.shaderParam4),
                    new XElement("target", visualEffect.target.ToString()),
                    new XElement("trigger", visualEffect.trigger.ToString()),
                    new XElement("delayTicks", visualEffect.delayTicks),
                    new XElement("displayDurationTicks", visualEffect.displayDurationTicks),
                    new XElement("globalFilterMode", visualEffect.globalFilterMode.ToString()),
                    new XElement("globalFilterTransition", visualEffect.globalFilterTransition.ToString()),
                    new XElement("globalFilterTransitionTicks", visualEffect.globalFilterTransitionTicks),
                    visualEffect.linkedExpression.HasValue ? new XElement("linkedExpression", visualEffect.linkedExpression.Value.ToString()) : null,
                    new XElement("linkedExpressionDurationTicks", visualEffect.linkedExpressionDurationTicks),
                    new XElement("linkedPupilBrightnessOffset", visualEffect.linkedPupilBrightnessOffset),
                    new XElement("linkedPupilContrastOffset", visualEffect.linkedPupilContrastOffset),
                    new XElement("scale", visualEffect.scale),
                    new XElement("drawSize", visualEffect.drawSize),
                    new XElement("useCasterFacing", SerializeBool(visualEffect.useCasterFacing)),
                    new XElement("forwardOffset", visualEffect.forwardOffset),
                    new XElement("sideOffset", visualEffect.sideOffset),
                    new XElement("heightOffset", visualEffect.heightOffset),
                    new XElement("rotation", visualEffect.rotation),
                    visualEffect.textureScale != Vector2.one ? new XElement("textureScale", XmlExportHelper.FormatVector2(visualEffect.textureScale)) : null,
                    new XElement("lineWidth", visualEffect.lineWidth),
                    new XElement("wallHeight", visualEffect.wallHeight),
                    new XElement("wallThickness", visualEffect.wallThickness),
                    new XElement("tileByLength", SerializeBool(visualEffect.tileByLength)),
                    new XElement("followGround", SerializeBool(visualEffect.followGround)),
                    new XElement("segmentCount", visualEffect.segmentCount),
                    new XElement("revealBySegments", SerializeBool(visualEffect.revealBySegments)),
                    new XElement("segmentRevealIntervalTicks", visualEffect.segmentRevealIntervalTicks),
                    visualEffect.offset != Vector3.zero ? new XElement("offset", XmlExportHelper.FormatVector3(visualEffect.offset)) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.vfxSourceLayerName) ? new XElement("vfxSourceLayerName", visualEffect.vfxSourceLayerName) : null,
                    new XElement("repeatCount", visualEffect.repeatCount),
                    new XElement("repeatIntervalTicks", visualEffect.repeatIntervalTicks),
                    new XElement("attachToPawn", SerializeBool(visualEffect.attachToPawn)),
                    new XElement("attachToTargetCell", SerializeBool(visualEffect.attachToTargetCell)),
                    new XElement("playSound", SerializeBool(visualEffect.playSound)),
                    !string.IsNullOrWhiteSpace(visualEffect.soundDefName) ? new XElement("soundDefName", visualEffect.soundDefName) : null,
                    new XElement("soundDelayTicks", visualEffect.soundDelayTicks),
                    new XElement("soundVolume", visualEffect.soundVolume),
                    new XElement("soundPitch", visualEffect.soundPitch),
                    new XElement("enableFrameAnimation", SerializeBool(visualEffect.enableFrameAnimation)),
                    new XElement("frameCount", visualEffect.frameCount),
                    new XElement("frameIntervalTicks", visualEffect.frameIntervalTicks),
                    new XElement("frameLoop", SerializeBool(visualEffect.frameLoop))
                ));
            }

            return root;
        }

        internal static XElement? GenerateRuntimeComponentsElement(List<AbilityRuntimeComponentConfig>? components)
        {
            if (components == null || components.Count == 0)
            {
                return null;
            }

            var root = new XElement("runtimeComponents");
            foreach (AbilityRuntimeComponentConfig? component in components)
            {
                if (component == null)
                {
                    continue;
                }

                root.Add(new XElement("li", SerializePublicFields(component)));
            }

            return root;
        }

        /// <summary>
        /// 通过反射将对象的全部 public 实例字段序列化为 XElement 数组。
        /// 自动处理 int/float/bool/string/enum/Def? 类型。
        /// 新增字段时无需更新此方法。
        /// </summary>
        private static object?[] SerializePublicFields(object obj)
        {
            var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            var elements = new List<object?>();

            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                var fieldType = field.FieldType;

                // DamageDef? 等 Def 引用 → 写 defName，null 跳过
                if (typeof(Def).IsAssignableFrom(fieldType))
                {
                    if (value != null)
                    {
                        string? defName = (value as Def)?.defName;
                        if (!string.IsNullOrWhiteSpace(defName))
                        {
                            elements.Add(new XElement(field.Name, defName));
                        }
                    }
                    continue;
                }

                // bool → "true"/"false"
                if (fieldType == typeof(bool))
                {
                    elements.Add(new XElement(field.Name, SerializeBool((bool)value)));
                    continue;
                }

                // float → InvariantCulture 避免逗号
                if (fieldType == typeof(float))
                {
                    elements.Add(new XElement(field.Name, ((float)value).ToString(CultureInfo.InvariantCulture)));
                    continue;
                }

                // enum → ToString()
                if (fieldType.IsEnum)
                {
                    elements.Add(new XElement(field.Name, value!.ToString()));
                    continue;
                }

                // string → 直接写
                if (fieldType == typeof(string))
                {
                    elements.Add(new XElement(field.Name, (string?)value ?? string.Empty));
                    continue;
                }

                // int, double 等 值类型 → 直接 ToString()
                if (fieldType.IsValueType)
                {
                    elements.Add(new XElement(field.Name, value!.ToString()));
                    continue;
                }
            }

            return elements.ToArray();
        }

        internal static XElement? GenerateAbilityHotkeysElement(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null)
            {
                return null;
            }

            var elements = new List<object?>
            {
                new XElement("enabled", SerializeBool(hotkeys.enabled))
            };

            foreach (var kvp in hotkeys.slotBindings)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                    elements.Add(new XElement(kvp.Key.ToLowerInvariant() + "AbilityDefName", kvp.Value));
            }

            return new XElement("abilityHotkeys", elements.ToArray());
        }

        private static XElement? GenerateAbilityElement(ModularAbilityDef? ability)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.defName))
            {
                return null;
            }

            return new XElement("li",
                new XElement("defName", ability.defName),
                !string.IsNullOrWhiteSpace(ability.label) ? new XElement("label", ability.label) : null,
                !string.IsNullOrWhiteSpace(ability.description) ? new XElement("description", ability.description) : null,
                !string.IsNullOrWhiteSpace(ability.iconPath) ? new XElement("iconPath", ability.iconPath) : null,
                new XElement("cooldownTicks", ability.cooldownTicks),
                new XElement("warmupTicks", ability.warmupTicks),
                new XElement("charges", ability.charges),
                new XElement("aiCanUse", ability.aiCanUse),
                new XElement("carrierType", ability.carrierType.ToString()),
                new XElement("targetType", ability.targetType.ToString()),
                new XElement("useRadius", SerializeBool(ability.useRadius)),
                new XElement("areaCenter", ability.areaCenter.ToString()),
                new XElement("areaShape", ability.areaShape.ToString()),
                !string.IsNullOrWhiteSpace(ability.irregularAreaPattern) ? new XElement("irregularAreaPattern", ability.irregularAreaPattern) : null,
                new XElement("range", ability.range),
                new XElement("radius", ability.radius),
                ability.projectileDef != null ? new XElement("projectileDef", ability.projectileDef.defName) : null,
                GenerateAbilityEffectsElement(ability.effects),
                GenerateAbilityVisualEffectsElement(ability.visualEffects),
                GenerateRuntimeComponentsElement(ability.runtimeComponents)
            );
        }

        private static string SerializeBool(bool value)
        {
            return value.ToString().ToLowerInvariant();
        }
    }
}
