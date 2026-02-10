using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Introspection;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 导入结果结构，包含图层列表和对应的原生节点路径/标签
    /// </summary>
    public struct ImportResult
    {
        public List<PawnLayerConfig> layers;
        public List<string> sourcePaths;
        public List<string> sourceTags;
    }

    /// <summary>
    /// 原版外观导入工具
    /// </summary>
    public static class VanillaImportUtility
    {
        private static readonly HashSet<string> ExcludedLabels = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "blank", "babyswaddled", "婴儿襁褓", "swaddled"
        };

        private static readonly HashSet<string> ExcludedTags = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Label"
        };

        public static List<PawnLayerConfig> ImportFromPawn(Pawn pawn)
        {
            var result = ImportFromPawnWithPaths(pawn);
            return result.layers;
        }

        public static ImportResult ImportFromPawnWithPaths(Pawn pawn)
        {
            var result = new ImportResult
            {
                layers = new List<PawnLayerConfig>(),
                sourcePaths = new List<string>(),
                sourceTags = new List<string>()
            };
            
            if (pawn == null) return result;

            try
            {
                var compSkin = pawn.GetComp<CompPawnSkin>();
                if (compSkin?.ActiveSkin != null && compSkin.ActiveSkin.layers.Count > 0)
                {
                    Log.Message($"[CharacterStudio] 检测到 {pawn.LabelShort} 已有 CS 皮肤配置，直接导入现有配置");
                    return ImportFromExistingSkin(compSkin.ActiveSkin);
                }
                
                var snapshot = RenderTreeParser.Capture(pawn);
                if (snapshot == null)
                {
                    Log.Warning($"[CharacterStudio] 无法捕获 Pawn {pawn.LabelShort} 的渲染树快照");
                    return result;
                }

                int index = 0;
                var allPaths = new List<string>();
                var allTags = new List<string>();
                var rawLayers = new List<PawnLayerConfig>();
                ProcessSnapshotNode(snapshot, "Root", "", rawLayers, allPaths, allTags, ref index, pawn);
                
                if (rawLayers.Count == 0)
                {
                    Log.Warning($"[CharacterStudio] 从 {pawn.LabelShort} 未能扫描到任何有效图层。");
                    return result;
                }

                var seenTexPaths = new HashSet<string>();
                foreach (var layer in rawLayers)
                {
                    string uniqueKey = $"{layer.texPath}|{layer.anchorPath}";
                    if (!seenTexPaths.Contains(uniqueKey))
                    {
                        seenTexPaths.Add(uniqueKey);
                        result.layers.Add(layer);
                    }
                }
                
                result.sourcePaths = allPaths;
                result.sourceTags = allTags;

                Log.Message($"[CharacterStudio] 从 {pawn.LabelShort} 成功扫描 {rawLayers.Count} 个图层，保留 {result.layers.Count} 个");

                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 导入图层时发生错误: {ex}");
                return result;
            }
        }

        private static void ProcessSnapshotNode(RenderNodeSnapshot node, string parentAnchorTag, string parentAnchorPath, List<PawnLayerConfig> layers, List<string> sourcePaths, List<string> sourceTags, ref int index, Pawn pawn)
        {
            if (node == null) return;

            string currentAnchor = parentAnchorTag;

            if (IsValidLayer(node))
            {
                Vector3 effectiveOffset;
                Vector3 effectiveOffsetEast;
                Vector3 effectiveOffsetNorth;
                Vector2 effectiveScale;
                Color effectiveColor;
                Color effectiveColorTwo = Color.white;
                
                if (node.runtimeDataValid)
                {
                    effectiveOffset = node.runtimeOffset;
                    effectiveOffsetEast = node.runtimeOffsetEast;
                    effectiveOffsetNorth = node.runtimeOffsetNorth;
                    effectiveScale = new Vector2(node.runtimeScale.x, node.runtimeScale.z);

                    // 智能缩放检测：
                    // 如果 runtimeScale (来自 ScaleFor) 接近 1.0，但 graphicDrawSize (来自 Graphic) 有显著缩放，
                    // 则优先使用 graphicDrawSize。这适用于那些通过 drawSize 而非 Matrix 缩放的模组。
                    // 我们在 PawnRenderNodeWorker_CustomLayer 中已经修复了双重缩放问题，所以现在可以安全地信任捕获到的值。
                    if (Mathf.Abs(effectiveScale.x - 1f) < 0.05f && Mathf.Abs(effectiveScale.y - 1f) < 0.05f)
                    {
                        if (node.graphicDrawSize != Vector2.zero &&
                            node.graphicDrawSize != Vector2.one &&
                            Mathf.Abs(node.graphicDrawSize.x - 1f) > 0.05f &&
                            node.graphicDrawSize.x < 5f) // 简单的安全性检查
                        {
                            effectiveScale = node.graphicDrawSize;
                        }
                    }

                    effectiveColor = node.runtimeColor;
                    
                    effectiveColorTwo = node.graphicColorTwo;
                }
                else
                {
                    effectiveOffset = node.offset;
                    effectiveOffsetEast = Vector3.zero;
                    effectiveOffsetNorth = Vector3.zero;
                    effectiveScale = node.scale > 0f ? new Vector2(node.scale, node.scale) : Vector2.one;
                    effectiveColor = node.color;
                }
                
                LayerColorSource detectedSource = DetectColorSource(node, pawn);
                LayerColorSource detectedSourceTwo = DetectSourceByColor(effectiveColorTwo, pawn);

                // 针对 HAR BodyAddon (Ear/Tail) 的特殊修正
                if (!string.IsNullOrEmpty(node.debugLabel))
                {
                    string labelLower = node.debugLabel.ToLower();
                    if (labelLower.Contains("ear") || labelLower.Contains("tail"))
                    {
                        // 修正 Color One: 如果当前判定为 Fixed，强制尝试 Hair
                        if (detectedSource == LayerColorSource.Fixed && pawn?.story != null)
                        {
                            // 只要不是非常像皮肤，就认为是头发
                            if (!IsColorSimilar(effectiveColor, pawn.story.SkinColor, 0.1f))
                            {
                                detectedSource = LayerColorSource.PawnHair;
                            }
                        }

                        // 修正 Color Two: 如果第一颜色是 Hair，且第二颜色是 Fixed，尝试绑定到 Hair
                        if (detectedSource == LayerColorSource.PawnHair && detectedSourceTwo == LayerColorSource.Fixed && pawn?.story != null)
                        {
                            // 如果第二颜色和头发颜色差异不是极大 (容差 0.3)，则认为是头发
                            // 这解决了 Miho 尾巴末端颜色略有不同的问题
                            if (IsColorSimilar(effectiveColorTwo, pawn.story.HairColor, 0.3f))
                            {
                                detectedSourceTwo = LayerColorSource.PawnHair;
                            }
                        }
                    }
                }
                
                // 简化 Shader 名称
                string simpleShaderName = "Cutout";
                if (!string.IsNullOrEmpty(node.shaderName))
                {
                    if (node.shaderName.Contains("CutoutComplex")) simpleShaderName = "CutoutComplex";
                    else if (node.shaderName.Contains("TransparentPostLight")) simpleShaderName = "TransparentPostLight";
                    else if (node.shaderName.Contains("Transparent")) simpleShaderName = "Transparent";
                    else if (node.shaderName.Contains("MetaOverlay")) simpleShaderName = "MetaOverlay";
                    else if (node.shaderName.Contains("Cutout")) simpleShaderName = "Cutout";
                }

                var layer = new PawnLayerConfig
                {
                    layerName = GenerateLayerName(node, index),
                    texPath = node.texPath,
                    anchorTag = parentAnchorTag,
                    anchorPath = string.IsNullOrEmpty(parentAnchorPath) ? "Root" : parentAnchorPath,
                    colorSource = detectedSource,
                    customColor = effectiveColor,
                    colorTwoSource = detectedSourceTwo,
                    customColorTwo = effectiveColorTwo,
                    shaderDefName = simpleShaderName,
                    maskTexPath = node.maskPath,
                    drawOrder = node.drawOrder != 0f ? node.drawOrder : node.treeLayer * 0.1f,
                    offset = effectiveOffset,
                    offsetEast = effectiveOffsetEast,
                    offsetNorth = effectiveOffsetNorth,
                    scale = effectiveScale,
                    visible = true,
                    graphicClass = node.graphicClass
                };
                layers.Add(layer);
                
                bool isDynamicOrUnknown = node.texPath == "Dynamic/Unknown"
                    || node.texPath.StartsWith("Dynamic/")
                    || node.texPath == "Unknown";
                
                if (!isDynamicOrUnknown)
                {
                    sourcePaths.Add(node.uniqueNodePath ?? "");
                    if (!string.IsNullOrEmpty(node.tagDefName) && node.tagDefName != "Untagged" && !sourceTags.Contains(node.tagDefName))
                    {
                        sourceTags.Add(node.tagDefName);
                    }
                }
                index++;
            }

            if (!string.IsNullOrEmpty(node.tagDefName) && node.tagDefName != "Untagged" && !ExcludedTags.Contains(node.tagDefName))
            {
                currentAnchor = node.tagDefName;
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    ProcessSnapshotNode(child, currentAnchor, node.uniqueNodePath, layers, sourcePaths, sourceTags, ref index, pawn);
                }
            }
        }

        private static bool IsValidLayer(RenderNodeSnapshot node)
        {
            if (string.IsNullOrEmpty(node.texPath)) return false;
            if (node.texPath == "No Graphic (Logic Only)") return false;
            if (node.texPath == "Error") return false;
            
            if (node.tagDefName != null && ExcludedTags.Contains(node.tagDefName)) return false;
            if (node.tagDefName != null && (node.tagDefName.Contains("Apparel") || node.tagDefName.Contains("Headgear"))) return false;
            if (node.workerClass != null && (node.workerClass.Contains("Apparel") || node.workerClass.Contains("Headgear"))) return false;

            string label = node.debugLabel ?? "";
            if (ExcludedLabels.Contains(label)) return false;
            
            string labelLower = label.ToLower();
            if (labelLower.Contains("swaddl") || labelLower.Contains("襁褓")) return false;

            return true;
        }

        private static ImportResult ImportFromExistingSkin(PawnSkinDef skinDef)
        {
            var result = new ImportResult
            {
                layers = new List<PawnLayerConfig>(),
                sourcePaths = new List<string>()
            };
            
            foreach (var layer in skinDef.layers)
            {
                var clonedLayer = layer.Clone();
                result.layers.Add(clonedLayer);
            }
            
            if (skinDef.hiddenPaths != null)
            {
                result.sourcePaths = new List<string>(skinDef.hiddenPaths);
            }
            
            Log.Message($"[CharacterStudio] 从现有皮肤导入 {result.layers.Count} 个图层，{result.sourcePaths.Count} 个隐藏路径");
            return result;
        }
        
        private static LayerColorSource DetectSourceByColor(Color color, Pawn pawn)
        {
            if (pawn?.story == null) return LayerColorSource.Fixed;
            
            if (IsColorSimilar(color, pawn.story.HairColor)) return LayerColorSource.PawnHair;
            if (IsColorSimilar(color, pawn.story.SkinColor)) return LayerColorSource.PawnSkin;
            
            return LayerColorSource.Fixed;
        }

        private static LayerColorSource DetectColorSource(RenderNodeSnapshot node, Pawn pawn)
        {
            // 0. 基于 HAR colorChannel 的绝对匹配 (最高优先级)
            if (!string.IsNullOrEmpty(node.colorChannel))
            {
                if (node.colorChannel == "hair") return LayerColorSource.PawnHair;
                if (node.colorChannel == "skin") return LayerColorSource.PawnSkin;
                // "mask" 通常意味着颜色来自 mask，但在 CS 中我们通常将其视为 Fixed 或 Custom，
                // 除非我们想让 mask 颜色跟随某些属性。目前保持默认。
            }

            // 1. 基于标签的强匹配
            if (!string.IsNullOrEmpty(node.tagDefName))
            {
                if (node.tagDefName.Contains("Hair") || node.tagDefName.Contains("Beard"))
                {
                    return LayerColorSource.PawnHair;
                }
                if (node.tagDefName.Contains("Head") || node.tagDefName.Contains("Body"))
                {
                    if (!node.workerClass.Contains("Apparel"))
                    {
                        return LayerColorSource.PawnSkin;
                    }
                }
            }
            
            // 1.1 基于 debugLabel 的补充匹配 (针对 HAR BodyAddon)
            if (!string.IsNullOrEmpty(node.debugLabel))
            {
                string labelLower = node.debugLabel.ToLower();
                if (labelLower.Contains("hair") || labelLower.Contains("beard")) return LayerColorSource.PawnHair;
                
                if (labelLower.Contains("ear") || labelLower.Contains("tail"))
                {
                    // 如果有 Pawn 数据，尝试验证颜色相似度，但不要过于严格
                    if (pawn?.story != null)
                    {
                        // 即使相似度验证失败，对于 tail/ear 我们也倾向于 hair，除非它像皮肤
                        // 降低皮肤匹配容差，避免误判
                        if (IsColorSimilar(node.runtimeColor, pawn.story.SkinColor, 0.05f)) return LayerColorSource.PawnSkin;
                        return LayerColorSource.PawnHair;
                    }
                    return LayerColorSource.PawnHair;
                }
            }

            // 2. 基于运行时颜色的匹配
            if (node.runtimeDataValid && pawn?.story != null)
            {
                if (IsColorSimilar(node.runtimeColor, pawn.story.HairColor))
                {
                    return LayerColorSource.PawnHair;
                }
                
                if (IsColorSimilar(node.runtimeColor, pawn.story.SkinColor))
                {
                    return LayerColorSource.PawnSkin;
                }
            }
            
            return LayerColorSource.Fixed;
        }

        private static bool IsColorSimilar(Color a, Color b, float tolerance = 0.01f)
        {
            return Mathf.Abs(a.r - b.r) < tolerance &&
                   Mathf.Abs(a.g - b.g) < tolerance &&
                   Mathf.Abs(a.b - b.b) < tolerance &&
                   Mathf.Abs(a.a - b.a) < tolerance;
        }

        private static string GenerateLayerName(RenderNodeSnapshot node, int index)
        {
            if (!string.IsNullOrEmpty(node.texPath) && node.texPath != "Dynamic/Unknown")
            {
                try
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(node.texPath);
                    if (!string.IsNullOrEmpty(fileName)) return fileName;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(node.debugLabel)) return node.debugLabel;

            return $"Layer_{index}";
        }
    }
}
