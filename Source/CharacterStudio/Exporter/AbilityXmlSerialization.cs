using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml;
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

                effectsElement.Add(new XElement("li", SerializePublicFields(effect)));
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

                root.Add(new XElement("li", SerializePublicFields(visualEffect)));
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

                // type 是 virtual property 而非 public field，SerializePublicFields 不会序列化它。
                // 必须显式写出 <type> 元素，否则 LoadDataFromXmlCustom 反序列化时会丢失类型，
                // 导致所有运行时组件回退为 SlotOverrideWindow（默认值），造成功能失效。
                var elements = new List<object?>
                {
                    new XElement("type", component.type.ToString())
                };
                elements.AddRange(SerializePublicFields(component));
                root.Add(new XElement("li", elements.ToArray()));
            }

            return root;
        }

        /// <summary>
        /// 通过反射将对象的全部 public 实例字段序列化为 XElement 数组。
        /// 自动处理 int/float/bool/string/enum/Def? 以及 Unity 基础类型。
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

                if (value == null) continue;

                // DamageDef? 等 Def 引用 → 写 defName
                if (typeof(Def).IsAssignableFrom(fieldType))
                {
                    string? defName = (value as Def)?.defName;
                    if (!string.IsNullOrWhiteSpace(defName))
                    {
                        elements.Add(new XElement(field.Name, defName));
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
                    elements.Add(new XElement(field.Name, value.ToString()));
                    continue;
                }

                // string → 直接写
                if (fieldType == typeof(string))
                {
                    elements.Add(new XElement(field.Name, (string)value));
                    continue;
                }

                // Vector2
                if (fieldType == typeof(Vector2))
                {
                    elements.Add(new XElement(field.Name, XmlExportHelper.FormatVector2((Vector2)value)));
                    continue;
                }

                // Vector3
                if (fieldType == typeof(Vector3))
                {
                    elements.Add(new XElement(field.Name, XmlExportHelper.FormatVector3((Vector3)value)));
                    continue;
                }

                // Color
                if (fieldType == typeof(Color))
                {
                    elements.Add(new XElement(field.Name, XmlExportHelper.FormatColor((Color)value)));
                    continue;
                }

                // int, double 等 值类型 → 直接 ToString()
                if (fieldType.IsValueType)
                {
                    elements.Add(new XElement(field.Name, value.ToString()));
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
                if (AbilityHotkeySlotUtility.IsSupportedSlotKey(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                    elements.Add(new XElement(kvp.Key.ToLowerInvariant() + "AbilityDefName", kvp.Value));
            }

            return new XElement("abilityHotkeys", elements.ToArray());
        }

        internal static List<ModularAbilityDef> ParseAbilities(XElement? abilitiesElement)
        {
            List<ModularAbilityDef> result = new List<ModularAbilityDef>();
            if (abilitiesElement == null)
            {
                return result;
            }

            foreach (XElement element in abilitiesElement.Elements("li"))
            {
                try
                {
                    string xml = element.ToString(SaveOptions.DisableFormatting);
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(xml);
                    XmlElement? root = doc.DocumentElement;
                    if (root == null)
                    {
                        continue;
                    }

                    ModularAbilityDef? ability = DirectXmlToObject.ObjectFromXml<ModularAbilityDef>(root, true);
                    if (ability != null)
                    {
                        result.Add(ability);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 解析技能能力节点失败: {ex.Message}");
                }
            }

            return result;
        }

        internal static SkinAbilityHotkeyConfig ParseHotkeys(XElement? hotkeysElement)
        {
            SkinAbilityHotkeyConfig config = new SkinAbilityHotkeyConfig();
            if (hotkeysElement == null)
            {
                return config;
            }

            config.enabled = bool.TryParse(hotkeysElement.Element("enabled")?.Value ?? "false", out bool enabled) && enabled;
            foreach (string slotKey in AbilityHotkeySlotUtility.SupportedSlotKeys)
            {
                string elementName = slotKey.ToLowerInvariant() + "AbilityDefName";
                string? value = hotkeysElement.Element(elementName)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    config[slotKey] = value ?? string.Empty;
                }
            }

            config.NormalizeToSupportedSlots();
            return config;
        }

        private static XElement? GenerateAbilityElement(ModularAbilityDef? ability)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.defName))
            {
                return null;
            }

            var element = new XElement("li");
            element.Add(SerializePublicFields(ability));

            // 列表和特殊嵌套字段需要手动处理，因为 SerializePublicFields 只处理简单字段
            var effectsElem = GenerateAbilityEffectsElement(ability.effects);
            if (effectsElem != null) element.Add(effectsElem);

            var vfxElem = GenerateAbilityVisualEffectsElement(ability.visualEffects);
            if (vfxElem != null) element.Add(vfxElem);

            var rcElem = GenerateRuntimeComponentsElement(ability.runtimeComponents);
            if (rcElem != null) element.Add(rcElem);

            return element;
        }

        private static string SerializeBool(bool value)
        {
            return value.ToString().ToLowerInvariant();
        }
    }
}