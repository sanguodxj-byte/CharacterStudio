using System;
using System.Collections.Generic;
using System.Xml.Linq;
using CharacterStudio.Core;
using UnityEngine;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// ModExportXmlWriter 皮肤定义域（SkinDef / BaseAppearance / Layers / FaceConfig / Animation）。
    /// </summary>
    public static partial class ModExportXmlWriter
    {
        // ─── FormatVector3 / FormatVector2 / FormatColor 通过 XmlExportHelper 提供外部复用 ───
        // 内部保留 private 方法以保持兼容

        internal static string FormatVector3(Vector3 value)
        {
            return XmlExportHelper.FormatVector3(value);
        }

        internal static string FormatVector2(Vector2 value)
        {
            return XmlExportHelper.FormatVector2(value);
        }

        internal static string FormatColor(Color value)
        {
            return XmlExportHelper.FormatColor(value);
        }
    }
}
