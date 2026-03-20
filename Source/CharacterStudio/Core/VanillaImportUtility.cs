﻿using System;
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
        /// <summary>
        /// true 表示数据来自角色已有的 CS 皮肤（直接克隆），
        /// 此时 sourceTags 存储的是旧皮肤的 hiddenTags 而非渲染树推断结果，
        /// 调用方不应再用它做 shouldHideVanilla* 启发式推断。
        /// </summary>
        public bool isFromExistingSkin;
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
                        var existingResult = ImportFromExistingSkin(compSkin.ActiveSkin);
                        existingResult.isFromExistingSkin = true;
                        return existingResult;
                    }
                }
                
                var snapshot = RenderTreeParser.Capture(pawn);
                if (snapshot == null)
                {
                    Log.Warning($"[CharacterStudio] 无法捕获 Pawn {pawn.LabelShort} 的渲染树快照");
                    return result;
                }

                var ctx = new ImportContext
                {
                    rawLayers      = new List<PawnLayerConfig>(),
                    allPaths       = new List<string>(),
                    allTags        = new List<string>(),
                    baseAppearance = result.baseAppearance,
                    index          = 0,
                    pawn           = pawn
                };
                ProcessSnapshotNode(snapshot, "Root", "", ctx);
                var rawLayers = ctx.rawLayers;
                var allPaths  = ctx.allPaths;
                var allTags   = ctx.allTags;
                
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

        // ─────────────────────────────────────────────
        // 导入上下文（替代9参数递归）
        // ─────────────────────────────────────────────

        private sealed class ImportContext
        {
            public List<PawnLayerConfig> rawLayers      = null!;
            public List<string>          allPaths       = null!;
            public List<string>          allTags        = null!;
            public BaseAppearanceConfig  baseAppearance = null!;
            public int                   index;
            public Pawn?                 pawn;
        }

        private static void ProcessSnapshotNode(
            RenderNodeSnapshot node,
            string parentAnchorTag,
            string? parentAnchorPath,
            ImportContext ctx)
        {
            // 将字段解包为局部变量，减少 ctx.xxx 的噪声
            var layers        = ctx.rawLayers;
            var sourcePaths   = ctx.allPaths;
            var sourceTags    = ctx.allTags;
            var baseAppearance= ctx.baseAppearance;
            var pawn          = ctx.pawn;
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
                            // 容差 0.15：比默认 0.01 宽松以兼容 HAR 尾巴末端颜色偏差，
                            // 但不超过 0.2 以避免将差异较大的固定色误判为发色。
                            if (IsColorSimilar(effectiveColorTwo, pawn.story.HairColor, 0.15f))
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
                    layerName = GenerateLayerName(node, ctx.index),
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
                ctx.index++;
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
                    ProcessSnapshotNode(child, currentAnchor, node.uniqueNodePath, ctx);
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

            // 结构性父节点（Body/Head/Hair/Beard）的 offset/scale/rotation 由原版 Worker 自动处理
            // （BaseHeadOffsetAt、bodyGraphicScale、ScaleFor 等），不应存入槽位配置。
            // 槽位存储的是用户的

            string simpleShaderName = "Cutout";
            if (!string.IsNullOrEmpty(node.shaderName))
            {
                if (node.shaderName.Contains("CutoutComplex")) simpleShaderName = "CutoutComplex";
                else if (node.shaderName.Contains("TransparentPostLight")) simpleShaderName = "TransparentPostLight";
                else if (node.shaderName.Contains("Transparent")) simpleShaderName = "Transparent";
                else if (node.shaderName.Contains("MetaOverlay")) simpleShaderName = "MetaOverlay";
                else if (node.shaderName.Contains("Cutout")) simpleShaderName = "Cutout";
            }

            // 即使 texPath 为空（动态路径尚未解析），也标记槽位为 enabled。
            // texPath 为空表示用户需要手动选择贴图；偏移/缩放/颜色等数据仍然正确捕获。
            // 颜色来源检测
            LayerColorSource colorSrc = DetectColorSource(node, pawn);
            Color customColor = (colorSrc == LayerColorSource.Fixed) ? node.color : Color.white;

            // 有 texPath 时才 enabled，让 UI 层决定是否展示
            slot.enabled = !string.IsNullOrWhiteSpace(node.texPath)
                || node.runtimeDataValid; // 有运行时数据也标记为可见
            slot.slotType = slotType.Value;
            slot.texPath = node.texPath ?? string.Empty;
            slot.maskTexPath = node.maskPath ?? string.Empty;
            slot.shaderDefName = simpleShaderName;
            slot.colorSource = colorSrc; // 修复：移除之前重复的 DetectColorSource 调用
            slot.customColor = customColor;
            slot.colorTwoSource = LayerColorSource.PawnSkin;
            slot.customColorTwo = Color.white;
            slot.scale = Vector2.one;
            slot.offset = Vector3.zero;
            slot.offsetEast = Vector3.zero;
            slot.offsetNorth = Vector3.zero;
            slot.rotation = 0f;
            slot.graphicClass = node.graphicClass;

            if (!string.IsNullOrEmpty(slot.texPath) &&
                (System.IO.Path.IsPathRooted(slot.texPath) || slot.texPath.StartsWith("/")))
                slot.graphicClass = typeof(CharacterStudio.Rendering.Graphic_Runtime);

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
        /// 设计原则：只识别原版顶层的 Head/Body 节点。
        /// 面部子节点（Eyes/Mouth/Brow/Nose/Ear）和 HAR BodyAddon 一律作为普通自定义图层导入，
        /// 由 ApplyBaseAppearanceOverrides 在渲染时通过递归搜索覆写。
        /// </summary>
        private static bool IsStructuralParentNode(RenderNodeSnapshot node)
        {
            if (node == null) return false;

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


