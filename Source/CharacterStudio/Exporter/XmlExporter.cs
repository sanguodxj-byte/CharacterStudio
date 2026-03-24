using System;
using System.Collections.Generic;
using System.Xml.Linq;
using CharacterStudio.Core;
using UnityEngine;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 将 PawnLayerConfig 导出为标准 Def XML（PawnRenderNodeDef）。
    /// </summary>
    public static class XmlExporter
    {
        public static XDocument ExportPawnRenderNodeDefs(PawnSkinDef skinDef)
        {
            if (skinDef == null)
            {
                throw new ArgumentNullException(nameof(skinDef));
            }

            var defs = new XElement("Defs");
            var layers = new List<PawnLayerConfig>();
            if (skinDef.layers != null)
            {
                layers.AddRange(skinDef.layers);
            }
            layers.AddRange(BaseAppearanceUtility.BuildSyntheticLayers(skinDef));

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                defs.Add(CreatePawnRenderNodeDefElement(skinDef, layer, i));
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), defs);
        }

        public static void ExportPawnRenderNodeDefs(PawnSkinDef skinDef, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath is empty", nameof(filePath));
            }

            var doc = ExportPawnRenderNodeDefs(skinDef);
            doc.Save(filePath);
        }

        private static XElement CreatePawnRenderNodeDefElement(PawnSkinDef skinDef, PawnLayerConfig layer, int index)
        {
            string baseName = string.IsNullOrWhiteSpace(layer.layerName)
                ? $"Layer_{index}"
                : layer.layerName;

            string safeName = SanitizeDefName(baseName);
            string defName = SanitizeDefName($"{skinDef.defName}_Node_{index}_{safeName}");

            return new XElement("PawnRenderNodeDef",
                new XElement("defName", defName),
                new XElement("label", layer.layerName ?? baseName),
                new XElement("texPath", layer.texPath ?? string.Empty),
                new XElement("anchorTag", layer.anchorTag ?? "Head"),
                string.IsNullOrEmpty(layer.anchorPath) ? null : new XElement("anchorPath", layer.anchorPath),
                new XElement("offset", ToVec3(layer.offset)),
                layer.offsetEast != Vector3.zero ? new XElement("offsetEast", ToVec3(layer.offsetEast)) : null,
                layer.offsetNorth != Vector3.zero ? new XElement("offsetNorth", ToVec3(layer.offsetNorth)) : null,
                new XElement("drawOrder", layer.drawOrder),
                new XElement("scale", ToVec2(layer.scale)),
                new XElement("rotation", layer.rotation),
                new XElement("flipHorizontal", layer.flipHorizontal.ToString().ToLowerInvariant()),
                new XElement("visible", layer.visible.ToString().ToLowerInvariant()),
                new XElement("colorSource", layer.colorSource.ToString()),
                layer.colorSource == LayerColorSource.Fixed ? new XElement("customColor", ToColor(layer.customColor)) : null,
                new XElement("colorTwoSource", layer.colorTwoSource.ToString()),
                layer.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", ToColor(layer.customColorTwo)) : null,
                !string.IsNullOrEmpty(layer.maskTexPath) ? new XElement("maskTexPath", layer.maskTexPath) : null,
                !string.IsNullOrEmpty(layer.shaderDefName) ? new XElement("shaderDefName", layer.shaderDefName) : null,
                layer.workerClass != null ? new XElement("workerClass", layer.workerClass.FullName) : null,
                layer.graphicClass != null ? new XElement("graphicClass", layer.graphicClass.FullName) : null,
                layer.animationType != LayerAnimationType.None ? new XElement("animationType", layer.animationType.ToString()) : null,
                layer.animationType != LayerAnimationType.None ? new XElement("animFrequency", layer.animFrequency) : null,
                layer.animationType != LayerAnimationType.None ? new XElement("animAmplitude", layer.animAmplitude) : null,
                layer.animationType != LayerAnimationType.None ? new XElement("animSpeed", layer.animSpeed) : null,
                layer.animationType != LayerAnimationType.None ? new XElement("animPhaseOffset", layer.animPhaseOffset) : null,
                layer.animationType != LayerAnimationType.None ? new XElement("animAffectsOffset", layer.animAffectsOffset.ToString().ToLowerInvariant()) : null,
                layer.animationType != LayerAnimationType.None ? new XElement("animOffsetAmplitude", layer.animOffsetAmplitude) : null,
                layer.animationType == LayerAnimationType.Spin && layer.animPivotOffset != Vector2.zero ? new XElement("animPivotOffset", ToVec2(layer.animPivotOffset)) : null
            );
        }

        private static string ToVec3(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
        private static string ToVec2(Vector2 v) => $"({v.x:F3}, {v.y:F3})";
        private static string ToColor(Color c) => $"({c.r:F3}, {c.g:F3}, {c.b:F3}, {c.a:F3})";

        private static string SanitizeDefName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "CS_Node";
            }

            var chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                bool ok = (ch >= 'a' && ch <= 'z') ||
                          (ch >= 'A' && ch <= 'Z') ||
                          (ch >= '0' && ch <= '9') ||
                          ch == '_';
                if (!ok)
                {
                    chars[i] = '_';
                }
            }

            string sanitized = new string(chars).Trim('_');
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "CS_Node";
            }

            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "N_" + sanitized;
            }

            return sanitized;
        }
    }
}
