using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// XML 导出共享工具方法。
    /// 提供向量/颜色格式化、字符串列表序列化等通用 XML 构建辅助。
    /// </summary>
    public static class XmlExportHelper
    {
        public static string FormatVector3(Vector3 value)
        {
            return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
        }

        public static string FormatVector2(Vector2 value)
        {
            return $"({value.x:F3}, {value.y:F3})";
        }

        public static string FormatColor(Color value)
        {
            return $"({value.r:F3}, {value.g:F3}, {value.b:F3}, {value.a:F3})";
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
                if (value != null)
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
                if (value != null)
                {
                    element.Add(new XElement("li", value));
                }
            }

            return element.HasElements ? element : null;
        }

        // ─── 解析辅助方法 ───

        public static bool ParseBool(string? value, bool fallback)
            => bool.TryParse(value, out bool parsed) ? parsed : fallback;

        public static int ParseInt(string? value, int fallback)
            => int.TryParse(value, out int parsed) ? parsed : fallback;

        public static float ParseFloat(string? value, float fallback)
            => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;

        public static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
            => Enum.TryParse(value ?? string.Empty, true, out TEnum parsed) ? parsed : fallback;

        public static Vector2 ParseVector2(string? value, Vector2 fallback)
        {
            if (value == null) return fallback;
            return ParseVector2Core(value, fallback);
        }

        private static Vector2 ParseVector2Core(string value, Vector2 fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string trimmed = value.Trim('(', ')', ' ');
            var parts = trimmed.Split(',');
            if (parts.Length < 2) return fallback;
            float x = ParseFloat(parts[0].Trim(), fallback.x);
            float y = ParseFloat(parts[1].Trim(), fallback.y);
            return new Vector2(x, y);
        }

        public static Vector3 ParseVector3(string? value, Vector3 fallback)
        {
            if (value == null) return fallback;
            return ParseVector3Core(value, fallback);
        }

        private static Vector3 ParseVector3Core(string value, Vector3 fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string trimmed = value.Trim('(', ')', ' ');
            var parts = trimmed.Split(',');
            if (parts.Length < 3) return fallback;
            float x = ParseFloat(parts[0].Trim(), fallback.x);
            float y = ParseFloat(parts[1].Trim(), fallback.y);
            float z = ParseFloat(parts[2].Trim(), fallback.z);
            return new Vector3(x, y, z);
        }

        public static Color ParseColor(string? value, Color fallback)
        {
            if (value == null) return fallback;
            return ParseColorCore(value, fallback);
        }

        private static Color ParseColorCore(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string trimmed = value.Trim('(', ')', ' ');
            var parts = trimmed.Split(',');
            if (parts.Length < 3) return fallback;
            float r = ParseFloat(parts[0].Trim(), fallback.r);
            float g = ParseFloat(parts[1].Trim(), fallback.g);
            float b = ParseFloat(parts[2].Trim(), fallback.b);
            float a = parts.Length > 3 ? ParseFloat(parts[3].Trim(), fallback.a) : 1f;
            return new Color(r, g, b, a);
        }

        public static List<string> ParseStringList(XElement? element)
        {
            var result = new List<string>();
            if (element == null) return result;
            foreach (var li in element.Elements("li"))
            {
                if (!string.IsNullOrWhiteSpace(li.Value))
                    result.Add(li.Value);
            }
            return result;
        }

        /// <summary>
        /// 通过反射将 XElement 子元素反序列化到对象的 public 实例字段。
        /// 自动处理 int/float/bool/string/enum/Vector2/Vector3/Color/List&lt;string&gt;/复杂子对象。
        /// 新增字段时无需更新此方法。
        /// </summary>
        public static void DeserializePublicFields(object target, XElement element)
        {
            if (target == null || element == null) return;

            var fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<XmlExportFieldAttribute>();
                if (attr != null && attr.Ignore) continue;

                string elemName = attr?.ElementName ?? field.Name;
                var childEl = element.Element(elemName);
                if (childEl == null) continue;

                var fieldType = field.FieldType;

                try
                {
                    if (fieldType == typeof(bool))
                        field.SetValue(target, ParseBool(childEl.Value, (bool)field.GetValue(target)!));
                    else if (fieldType == typeof(float))
                        field.SetValue(target, ParseFloat(childEl.Value, (float)field.GetValue(target)!));
                    else if (fieldType == typeof(int))
                        field.SetValue(target, ParseInt(childEl.Value, (int)field.GetValue(target)!));
                    else if (fieldType == typeof(string))
                        field.SetValue(target, childEl.Value);
                    else if (fieldType.IsEnum)
                    {
                        try { field.SetValue(target, Enum.Parse(fieldType, childEl.Value, true)); }
                        catch { /* keep default */ }
                    }
                    else if (fieldType == typeof(Vector2))
                        field.SetValue(target, ParseVector2(childEl.Value, (Vector2)field.GetValue(target)!));
                    else if (fieldType == typeof(Vector3))
                        field.SetValue(target, ParseVector3(childEl.Value, (Vector3)field.GetValue(target)!));
                    else if (fieldType == typeof(Color))
                        field.SetValue(target, ParseColor(childEl.Value, (Color)field.GetValue(target)!));
                    else if (fieldType == typeof(List<string>))
                        field.SetValue(target, ParseStringList(childEl));
                    else if (fieldType.IsClass && !fieldType.IsAbstract && fieldType.GetConstructor(Type.EmptyTypes) != null)
                    {
                        // 复杂子对象 — 递归反序列化
                        var subObj = Activator.CreateInstance(fieldType);
                        DeserializePublicFields(subObj, childEl);
                        field.SetValue(target, subObj);
                    }
                }
                catch
                {
                    // 解析失败时保留默认值
                }
            }
        }

        /// <summary>
        /// 从 XElement 创建并填充一个新对象。要求类型有无参构造函数。
        /// </summary>
        public static T? CreateFromXml<T>(XElement? element) where T : class, new()
        {
            if (element == null) return null;
            var obj = new T();
            DeserializePublicFields(obj, element);
            return obj;
        }
    }
}
