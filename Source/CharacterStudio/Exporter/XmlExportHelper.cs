using System.Collections.Generic;
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
    }
}
