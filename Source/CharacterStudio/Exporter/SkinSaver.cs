using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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

                doc.Save(filePath);
                Log.Message($"[CharacterStudio] 皮肤已保存至: {filePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 保存皮肤失败: {ex}");
                throw;
            }
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
                !string.IsNullOrEmpty(skin.author) ? new XElement("author", skin.author) : null,
                !string.IsNullOrEmpty(skin.version) ? new XElement("version", skin.version) : null,
                !string.IsNullOrEmpty(skin.previewTexPath) ? new XElement("previewTexPath", skin.previewTexPath) : null,
                
                // 隐藏路径
                GenerateListElement("hiddenPaths", skin.hiddenPaths),
                // 隐藏标签 (保留兼容性)
                #pragma warning disable CS0618
                GenerateListElement("hiddenTags", skin.hiddenTags),
                #pragma warning restore CS0618

                // 图层
                GenerateLayersXml(skin.layers),
                
                // 目标种族
                GenerateListElement("targetRaces", skin.targetRaces),

                // 面部配置
                skin.faceConfig != null && (skin.faceConfig.enabled || skin.faceConfig.components.Any()) ? GenerateFaceConfigXml(skin.faceConfig) : null
            );
        }

        private static XElement GenerateListElement(string tagName, List<string> items)
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

        private static XElement GenerateLayersXml(List<PawnLayerConfig> layers)
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
                    
                    // 变换
                    new XElement("offset", $"({layer.offset.x:F3}, {layer.offset.y:F3}, {layer.offset.z:F3})"),
                    layer.offsetEast != Vector3.zero ? new XElement("offsetEast", $"({layer.offsetEast.x:F3}, {layer.offsetEast.y:F3}, {layer.offsetEast.z:F3})") : null,
                    layer.offsetNorth != Vector3.zero ? new XElement("offsetNorth", $"({layer.offsetNorth.x:F3}, {layer.offsetNorth.y:F3}, {layer.offsetNorth.z:F3})") : null,
                    
                    new XElement("drawOrder", layer.drawOrder),
                    new XElement("scale", $"({layer.scale.x:F2}, {layer.scale.y:F2})"),
                    new XElement("flipHorizontal", layer.flipHorizontal.ToString().ToLower()),
                    
                    // 颜色
                    new XElement("colorSource", layer.colorSource.ToString()),
                    layer.colorSource == LayerColorSource.Fixed ? new XElement("customColor", $"({layer.customColor.r:F3}, {layer.customColor.g:F3}, {layer.customColor.b:F3}, {layer.customColor.a:F3})") : null,
                    layer.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", $"({layer.customColorTwo.r:F3}, {layer.customColorTwo.g:F3}, {layer.customColorTwo.b:F3}, {layer.customColorTwo.a:F3})") : null,
                    
                    new XElement("visible", layer.visible.ToString().ToLower()),
                    
                    // Worker
                    layer.workerClass != null ? new XElement("workerClass", layer.workerClass.FullName) : null,
                    // 仅当 Worker 是 FaceComponent 时才保存 faceComponent
                    (layer.workerClass != null && layer.workerClass.Name.Contains("FaceComponent")) ? new XElement("faceComponent", layer.faceComponent.ToString()) : null,

                    // 动画
                    layer.animationType != LayerAnimationType.None ? new XElement("animationType", layer.animationType.ToString()) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animFrequency", layer.animFrequency) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animAmplitude", layer.animAmplitude) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animSpeed", layer.animSpeed) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animPhaseOffset", layer.animPhaseOffset) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animAffectsOffset", layer.animAffectsOffset.ToString().ToLower()) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animOffsetAmplitude", layer.animOffsetAmplitude) : null
                );
                element.Add(layerEl);
            }
            return element;
        }

        private static XElement GenerateFaceConfigXml(PawnFaceConfig config)
        {
            var element = new XElement("faceConfig",
                new XElement("enabled", config.enabled.ToString().ToLower())
            );

            if (config.components != null && config.components.Count > 0)
            {
                var componentsEl = new XElement("components");
                foreach (var comp in config.components)
                {
                    var compEl = new XElement("li",
                        new XElement("type", comp.type.ToString())
                    );

                    if (comp.expressions != null && comp.expressions.Count > 0)
                    {
                        var exprsEl = new XElement("expressions");
                        foreach (var expr in comp.expressions)
                        {
                            if (!string.IsNullOrEmpty(expr.texPath))
                            {
                                exprsEl.Add(new XElement("li",
                                    new XElement("expression", expr.expression.ToString()),
                                    new XElement("texPath", expr.texPath)
                                ));
                            }
                        }
                        compEl.Add(exprsEl);
                    }
                    componentsEl.Add(compEl);
                }
                element.Add(componentsEl);
            }

            return element;
        }
    }
}