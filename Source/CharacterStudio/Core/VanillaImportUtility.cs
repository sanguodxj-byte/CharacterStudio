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
        public BaseAppearanceConfig baseAppearance;
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
                sourceTags = new List<string>(),
                baseAppearance = new BaseAppearanceConfig()
            };
            
            if (pawn == null) return result;

            try
            {
                var compSkin = pawn.GetComp<CompPawnSkin>();
                if (compSkin?.ActiveSkin != null)
                {
                    bool hasExistingLayers = compSkin.ActiveSkin.layers.Count > 0;
                    bool hasExistingBaseAppearance = compSkin.ActiveSkin.baseAppearance != null
                        && compSkin.ActiveSkin.baseAppearance.EnabledSlots().Any();
                    if (hasExistingLayers || hasExistingBaseAppearance)
                    {
                        Log.Message($"[CharacterStudio] 检测到 {pawn.LabelShort} 已有 CS 皮肤配置，直接导入现有配置");
                        return ImportFromExistingSkin(compSkin.ActiveSkin);
                    }
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
                ProcessSnapshotNode(snapshot, "Root", "", rawLayers, allPaths, allTags, result.baseAppearance, ref index, pawn);
                
                bool hasImportedBaseAppearance = result.baseAppearance.EnabledSlots().Any();
                if (rawLayers.Count == 0 && !hasImportedBaseAppearance)
                {
                    Log.Warning($"[CharacterStudio] 从 {pawn.LabelShort} 未能扫描到任何有效图层或基础槽位。");
                    return result;
                }

                // 去重策略：按“渲染语义”去重，而不是仅按 texPath。
                // 这样可保留同贴图但锚点、颜色、遮罩、排序或缩放不同的合法图层。
                var seenLayerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var layer in rawLayers)
                {
                    if (string.IsNullOrEmpty(layer.texPath))
                        continue;

                    string dedupeKey = string.Join("|",
                        layer.texPath ?? string.Empty,
                        layer.maskTexPath ?? string.Empty,
                        layer.anchorPath ?? string.Empty,
                        layer.anchorTag ?? string.Empty,
                        layer.shaderDefName ?? string.Empty,
                        layer.graphicClass?.FullName ?? string.Empty,
                        layer.colorSource.ToString(),
                        layer.colorTwoSource.ToString(),
                        layer.customColor.r.ToString("F4"),
                        layer.customColor.g.ToString("F4"),
                        layer.customColor.b.ToString("F4"),
                        layer.customColor.a.ToString("F4"),
                        layer.customColorTwo.r.ToString("F4"),
                        layer.customColorTwo.g.ToString("F4"),
                        layer.customColorTwo.b.ToString("F4"),
                        layer.customColorTwo.a.ToString("F4"),
                        layer.drawOrder.ToString("F4"),
                        layer.offset.x.ToString("F4"),
                        layer.offset.y.ToString("F4"),
                        layer.offset.z.ToString("F4"),
                        layer.offsetEast.x.ToString("F4"),
                        layer.offsetEast.y.ToString("F4"),
                        layer.offsetEast.z.ToString("F4"),
                        layer.offsetNorth.x.ToString("F4"),
                        layer.offsetNorth.y.ToString("F4"),
                        layer.offsetNorth.z.ToString("F4"),
                        layer.scale.x.ToString("F4"),
                        layer.scale.y.ToString("F4"),
                        layer.visible ? "1" : "0",
                        layer.flipHorizontal ? "1" : "0"
                    );

                    if (seenLayerKeys.Add(dedupeKey))
                    {
                        result.layers.Add(layer);
                    }
                }
                
                result.sourcePaths = allPaths;
                result.sourceTags = allTags;

                Log.Message($"[CharacterStudio] 从 {pawn.LabelShort} 成功扫描 {rawLayers.Count} 个图层，保留 {result.layers.Count} 个，基础槽位 {result.baseAppearance.EnabledSlots().Count()} 个");

                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 导入图层时发生错误: {ex}");
                return result;
            }
        }

        private static void ProcessSnapshotNode(RenderNodeSnapshot node, string parentAnchorTag, string? parentAnchorPath, List<PawnLayerConfig> layers, List<string> sourcePaths, List<string> sourceTags, BaseAppearanceConfig baseAppearance, ref int index, Pawn? pawn)
        {
            if (node == null) return;

            string currentAnchor = parentAnchorTag;

            // ── 结构性父节点过滤 ──
            // Head/Body 节点自身携带皮肤纹理，但这些纹理在游戏中由原版渲染树管理。
            // 将它们导入为自定义图层会导致纹理重复。
            // 仅跳过导入，不跳过递归子节点处理和 anchor 更新。
            bool isStructuralParent = IsStructuralParentNode(node);

            if (isStructuralParent)
            {
                TryImportBaseSlot(node, baseAppearance, pawn);
            }

            if (IsValidLayer(node) && !isStructuralParent)
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

                // 针对 HAR 的 Mask 颜色 (colorTwo) 进行额外推断
                // 如果 Shader 是 CutoutComplex 且 colorChannel 是 hair/skin，那么 colorTwo 可能是对应的第二颜色
                // 但 HAR 通常在 skin 通道使用 skinColorSecond，在 hair 通道使用 hairColorSecond (如果有的话)
                // 目前 RimWorld 原版 hair 没有第二颜色，但 HAR 可能有。
                // 简单的逻辑：如果 colorTwoSource 仍然是 Fixed，且 colorSource 是动态的，尝试看看 colorTwo 是否也接近该动态源
                if (detectedSourceTwo == LayerColorSource.Fixed && detectedSource != LayerColorSource.Fixed && pawn?.story != null)
                {
                    // 如果主颜色是 Hair，副颜色也接近 Hair，则绑定到 Hair
                    if (detectedSource == LayerColorSource.PawnHair && IsColorSimilar(effectiveColorTwo, pawn.story.HairColor, 0.2f))
                    {
                        detectedSourceTwo = LayerColorSource.PawnHair;
                    }
                    // 如果主颜色是 Skin，副颜色也接近 Skin，则绑定到 Skin
                    else if (detectedSource == LayerColorSource.PawnSkin && IsColorSimilar(effectiveColorTwo, pawn.story.SkinColor, 0.2f))
                    {
                        detectedSourceTwo = LayerColorSource.PawnSkin;
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
                    anchorPath = string.IsNullOrEmpty(parentAnchorPath) ? "Root" : (parentAnchorPath ?? "Root"),
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
                    // 结构性父节点的路径不加入 hiddenPaths，因为原版节点保持正常渲染
                    // （此分支已被 isStructuralParent 过滤，不会到达，但保留防御性检查）
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

            // HAR/异种兼容：某些身体节点可能没有标准 Body tag，补充 Body 回退隐藏标记
            if (!string.IsNullOrEmpty(node.debugLabel) &&
                node.debugLabel.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (node.workerClass == null || !node.workerClass.Contains("Apparel")) &&
                !sourceTags.Contains("Body"))
            {
                sourceTags.Add("Body");
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    ProcessSnapshotNode(child, currentAnchor, node.uniqueNodePath, layers, sourcePaths, sourceTags, baseAppearance, ref index, pawn);
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
                sourcePaths = new List<string>(),
                sourceTags = new List<string>(),
                baseAppearance = skinDef.baseAppearance?.Clone() ?? new BaseAppearanceConfig()
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

#pragma warning disable CS0618 // hiddenTags 仅用于旧数据兼容
            if (skinDef.hiddenTags != null)
            {
                result.sourceTags = new List<string>(skinDef.hiddenTags);
            }
#pragma warning restore CS0618
            
            Log.Message($"[CharacterStudio] 从现有皮肤导入 {result.layers.Count} 个图层，{result.sourcePaths.Count} 个隐藏路径，{result.sourceTags.Count} 个隐藏标签");
            return result;
        }
        
        private static LayerColorSource DetectSourceByColor(Color color, Pawn? pawn)
        {
            if (pawn?.story == null) return LayerColorSource.Fixed;
            
            if (IsColorSimilar(color, pawn.story.HairColor)) return LayerColorSource.PawnHair;
            if (IsColorSimilar(color, pawn.story.SkinColor)) return LayerColorSource.PawnSkin;
            
            return LayerColorSource.Fixed;
        }

        private static LayerColorSource DetectColorSource(RenderNodeSnapshot node, Pawn? pawn)
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

        private static void TryImportBaseSlot(RenderNodeSnapshot node, BaseAppearanceConfig baseAppearance, Pawn? pawn)
        {
            BaseAppearanceSlotType? slotType = TryResolveBaseSlotType(node);
            if (slotType == null)
            {
                return;
            }

            var slot = baseAppearance.GetSlot(slotType.Value);
            Vector3 effectiveOffset = node.runtimeDataValid ? node.runtimeOffset : node.offset;
            Vector3 effectiveOffsetEast = node.runtimeDataValid ? node.runtimeOffsetEast : Vector3.zero;
            Vector3 effectiveOffsetNorth = node.runtimeDataValid ? node.runtimeOffsetNorth : Vector3.zero;
            Vector2 effectiveScale = node.runtimeDataValid
                ? new Vector2(node.runtimeScale.x, node.runtimeScale.z)
                : (node.scale > 0f ? new Vector2(node.scale, node.scale) : Vector2.one);
            Color effectiveColor = node.runtimeDataValid ? node.runtimeColor : node.color;
            Color effectiveColorTwo = node.runtimeDataValid ? node.graphicColorTwo : Color.white;

            string simpleShaderName = "Cutout";
            if (!string.IsNullOrEmpty(node.shaderName))
            {
                if (node.shaderName.Contains("CutoutComplex")) simpleShaderName = "CutoutComplex";
                else if (node.shaderName.Contains("TransparentPostLight")) simpleShaderName = "TransparentPostLight";
                else if (node.shaderName.Contains("Transparent")) simpleShaderName = "Transparent";
                else if (node.shaderName.Contains("MetaOverlay")) simpleShaderName = "MetaOverlay";
                else if (node.shaderName.Contains("Cutout")) simpleShaderName = "Cutout";
            }

            slot.enabled = !string.IsNullOrWhiteSpace(node.texPath);
            slot.slotType = slotType.Value;
            slot.texPath = node.texPath ?? string.Empty;
            slot.maskTexPath = node.maskPath ?? string.Empty;
            slot.shaderDefName = simpleShaderName;
            slot.colorSource = DetectColorSource(node, pawn);
            slot.customColor = effectiveColor;
            slot.colorTwoSource = DetectSourceByColor(effectiveColorTwo, pawn);
            slot.customColorTwo = effectiveColorTwo;
            slot.scale = effectiveScale;
            slot.offset = effectiveOffset;
            slot.offsetEast = effectiveOffsetEast;
            slot.offsetNorth = effectiveOffsetNorth;
            slot.rotation = node.runtimeDataValid ? node.runtimeRotation : 0f;
            slot.drawOrderOffset = 0f;
            slot.graphicClass = node.graphicClass;
        }

        private static BaseAppearanceSlotType? TryResolveBaseSlotType(RenderNodeSnapshot node)
        {
            string tag = node.tagDefName ?? string.Empty;
            string label = node.debugLabel ?? string.Empty;
            string texPath = node.texPath ?? string.Empty;

            if (ContainsAny(tag, label, texPath, "body")) return BaseAppearanceSlotType.Body;
            if (ContainsAny(tag, label, texPath, "head")) return BaseAppearanceSlotType.Head;
            if (ContainsAny(tag, label, texPath, "hair")) return BaseAppearanceSlotType.Hair;
            if (ContainsAny(tag, label, texPath, "beard")) return BaseAppearanceSlotType.Beard;
            if (ContainsAny(tag, label, texPath, "eye", "eyes")) return BaseAppearanceSlotType.Eyes;
            if (ContainsAny(tag, label, texPath, "brow", "eyebrow")) return BaseAppearanceSlotType.Brow;
            if (ContainsAny(tag, label, texPath, "mouth", "lip")) return BaseAppearanceSlotType.Mouth;
            if (ContainsAny(tag, label, texPath, "nose")) return BaseAppearanceSlotType.Nose;
            if (ContainsAny(tag, label, texPath, "ear")) return BaseAppearanceSlotType.Ear;

            return null;
        }

        private static bool ContainsAny(string a, string b, string c, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if ((!string.IsNullOrEmpty(a) && a.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(b) && b.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c) && c.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断节点是否为结构性父节点（Head/Body 容器节点）。
        /// 这些节点有自己的纹理（皮肤贴图），但它们的纹理应由原版机制管理，
        /// 不应导入为自定义图层，否则会导致重复渲染。
        /// </summary>
        private static bool IsStructuralParentNode(RenderNodeSnapshot node)
        {
            if (node == null) return false;
            if (node.children == null || node.children.Count == 0) return false;

            string tag = node.tagDefName ?? "";
            if (tag == "Head" || tag == "Body")
            {
                // 排除服装类节点（它们可能也有 Body 相关 tag）
                if (node.workerClass != null &&
                    (node.workerClass.Contains("Apparel") || node.workerClass.Contains("Headgear")))
                    return false;
                return true;
            }
            return false;
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
