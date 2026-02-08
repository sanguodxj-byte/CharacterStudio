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
        /// <summary>
        /// 源节点的 Tag 名称列表，用于在路径匹配失败时作为回退
        /// </summary>
        public List<string> sourceTags;
    }

    /// <summary>
    /// 原版外观导入工具
    /// </summary>
    public static class VanillaImportUtility
    {
        // 仅排除确定的逻辑容器或不含贴图的节点
        private static readonly HashSet<string> ExcludedLabels = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "blank", "tailmoving", "babyswaddled", "婴儿襁褓", "swaddled"
        };

        // 仅排除确定无意义的视觉标签
        private static readonly HashSet<string> ExcludedTags = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Label"
        };

        /// <summary>
        /// 从 Pawn 导入外观图层
        /// </summary>
        public static List<PawnLayerConfig> ImportFromPawn(Pawn pawn)
        {
            var result = ImportFromPawnWithPaths(pawn);
            return result.layers;
        }

        /// <summary>
        /// 从 Pawn 导入外观图层，同时返回需要隐藏的原生节点路径
        /// </summary>
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
                // 优先检查是否已有 CS 皮肤配置
                // 如果小人已经使用了 CS 自定义，直接导入其配置而非从渲染树解析
                var compSkin = pawn.GetComp<CompPawnSkin>();
                if (compSkin?.ActiveSkin != null && compSkin.ActiveSkin.layers.Count > 0)
                {
                    Log.Message($"[CharacterStudio] 检测到 {pawn.LabelShort} 已有 CS 皮肤配置，直接导入现有配置");
                    return ImportFromExistingSkin(compSkin.ActiveSkin);
                }
                
                // 使用 RenderTreeParser 捕获真实的渲染树
                var snapshot = RenderTreeParser.Capture(pawn);
                if (snapshot == null)
                {
                    Log.Warning($"[CharacterStudio] 无法捕获 Pawn {pawn.LabelShort} 的渲染树快照");
                    return result;
                }

                // 递归遍历快照并转换为图层配置
                int index = 0;
                var allPaths = new List<string>();
                var allTags = new List<string>();
                // 初始锚点应为 "Root"，因为 RenderTreeParser 从 Root 节点开始解析
                // 这样 Root 的子节点（如 Body）会挂在 Root 上，Offset 是相对于 Root 的
                // 之前设为 "Body" 导致 Body 节点挂在 Body 上，Offset 叠加，位置错误
                // parentAnchorPath 为空字符串表示根节点没有父路径
                ProcessSnapshotNode(snapshot, "Root", "", result.layers, allPaths, allTags, ref index);
                
                if (result.layers.Count == 0)
                {
                    Log.Warning($"[CharacterStudio] 从 {pawn.LabelShort} 未能扫描到任何有效图层。请确保该角色已在场景中渲染。");
                    return result;
                }

                // 不要基于 texPath 去重，因为不同的身体部位可能使用相同的纹理（如左右手、相同的假肢等）
                // RenderTreeParser 已经确保了节点路径的唯一性
                
                result.sourcePaths = allPaths;
                result.sourceTags = allTags;
                // result.layers 已经在 ProcessSnapshotNode 中填充

                Log.Message($"[CharacterStudio] 从 {pawn.LabelShort} 成功扫描并导入 {result.layers.Count} 个图层，{allTags.Count} 个标签用于回退隐藏");

                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 导入图层时发生错误: {ex}");
                return result;
            }
        }

        /// <summary>
        /// 递归处理渲染树节点，转换为图层配置
        /// </summary>
        /// <param name="node">当前节点快照</param>
        /// <param name="parentAnchorTag">父节点的锚点标签（用于 anchorTag 回退）</param>
        /// <param name="parentAnchorPath">父节点的完整路径（用于 anchorPath 精确定位）</param>
        /// <param name="layers">输出的图层列表</param>
        /// <param name="sourcePaths">输出的原节点路径列表（用于自动隐藏）</param>
        /// <param name="sourceTags">输出的原节点标签列表（用于路径匹配失败时的回退隐藏）</param>
        /// <param name="index">图层索引</param>
        private static void ProcessSnapshotNode(RenderNodeSnapshot node, string parentAnchorTag, string parentAnchorPath, List<PawnLayerConfig> layers, List<string> sourcePaths, List<string> sourceTags, ref int index)
        {
            if (node == null) return;

            // 确定当前节点的锚点
            string currentAnchor = parentAnchorTag;

            // 检查节点是否有有效的纹理
            if (IsValidLayer(node))
            {
                // 优先使用运行时数据（更准确地还原原版渲染结构）
                Vector3 effectiveOffset;
                Vector3 effectiveOffsetEast;
                Vector3 effectiveOffsetNorth;
                Vector2 effectiveScale;
                Color effectiveColor;
                
                if (node.runtimeDataValid)
                {
                    // 使用运行时捕获的真实数据
                    // runtimeOffset 是 South 朝向的基准偏移
                    effectiveOffset = node.runtimeOffset;
                    // runtimeOffsetEast 是 East 朝向相对于 South 的额外偏移
                    effectiveOffsetEast = node.runtimeOffsetEast;
                    // runtimeOffsetNorth 是 North 朝向相对于 South 的额外偏移
                    effectiveOffsetNorth = node.runtimeOffsetNorth;
                    effectiveScale = new Vector2(node.runtimeScale.x, node.runtimeScale.z); // X和Z分量作为2D缩放
                    effectiveColor = node.runtimeColor;
                    Log.Message($"[CharacterStudio] 导入图层 '{node.debugLabel}': 使用运行时数据 - Offset={effectiveOffset}, OffsetEast={effectiveOffsetEast}, OffsetNorth={effectiveOffsetNorth}, Scale={effectiveScale}, Color={effectiveColor}");
                }
                else
                {
                    // 回退到静态属性
                    effectiveOffset = node.offset;
                    effectiveOffsetEast = Vector3.zero;
                    effectiveOffsetNorth = Vector3.zero;
                    effectiveScale = node.scale > 0f ? new Vector2(node.scale, node.scale) : Vector2.one;
                    effectiveColor = node.color;
                    Log.Warning($"[CharacterStudio] 导入图层 '{node.debugLabel}': 运行时数据无效，回退到静态值 - Offset={effectiveOffset}, Scale={effectiveScale}, Color={effectiveColor}");
                }
                
                var layer = new PawnLayerConfig
                {
                    layerName = GenerateLayerName(node, index),
                    texPath = node.texPath,
                    anchorTag = parentAnchorTag,
                    // 关键修复：anchorPath 应该是父节点的路径，而非当前节点的路径
                    // 这样图层会作为父节点的子节点注入，与原节点平级
                    // 如果父路径为空（Root节点），则使用 "Root"
                    anchorPath = string.IsNullOrEmpty(parentAnchorPath) ? "Root" : parentAnchorPath,
                    colorType = LayerColorType.Custom,
                    customColor = effectiveColor,
                    // 使用节点的原始绘制顺序，如果没有则使用树层级
                    drawOrder = node.drawOrder != 0f ? node.drawOrder : node.treeLayer * 0.1f,
                    // 从节点读取偏移和缩放（优先使用运行时数据）
                    // offset 是 South 朝向的基准偏移
                    offset = effectiveOffset,
                    // offsetEast 是 East/West 朝向的额外偏移
                    offsetEast = effectiveOffsetEast,
                    // offsetNorth 是 North 朝向的额外偏移
                    offsetNorth = effectiveOffsetNorth,
                    scale = effectiveScale,
                    // 导入的图层默认可见，让用户手动决定是否隐藏
                    visible = true,
                    graphicClass = node.graphicClass
                };
                layers.Add(layer);
                
                // 判断是否应该隐藏原生节点
                // 对于 Dynamic/Unknown 类型的纹理路径，我们无法正确渲染替代图层，
                // 所以不应该隐藏原生节点（否则会导致空白）
                bool isDynamicOrUnknown = node.texPath == "Dynamic/Unknown"
                    || node.texPath.StartsWith("Dynamic/")
                    || node.texPath == "Unknown";
                
                if (!isDynamicOrUnknown)
                {
                    // 只有能正确渲染的图层才记录其原生路径用于隐藏
                    sourcePaths.Add(node.uniqueNodePath ?? "");
                    
                    // 同样，只有能正确渲染的图层才记录其标签用于回退隐藏
                    // 这样 Dynamic/Unknown 类型的节点（如 HAR 头部/身体）不会被标签隐藏
                    if (!string.IsNullOrEmpty(node.tagDefName) && node.tagDefName != "Untagged" && !sourceTags.Contains(node.tagDefName))
                    {
                        sourceTags.Add(node.tagDefName);
                    }
                }
                else
                {
                    Log.Message($"[CharacterStudio] 图层 '{node.debugLabel}' 使用动态纹理 ({node.texPath})，保留原生节点可见（不隐藏路径和标签）");
                }
                index++;
            }

            // 如果节点定义了新的 Tag，更新锚点上下文
            if (!string.IsNullOrEmpty(node.tagDefName) && node.tagDefName != "Untagged" && !ExcludedTags.Contains(node.tagDefName))
            {
                currentAnchor = node.tagDefName;
            }

            // 递归处理子节点，传递当前节点的路径作为子节点的父路径
            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    ProcessSnapshotNode(child, currentAnchor, node.uniqueNodePath, layers, sourcePaths, sourceTags, ref index);
                }
            }
        }

        private static bool IsValidLayer(RenderNodeSnapshot node)
        {
            // 过滤掉无纹理或无效纹理的节点
            if (string.IsNullOrEmpty(node.texPath)) return false;
            if (node.texPath == "No Graphic (Logic Only)") return false;
            if (node.texPath == "Error") return false;
            
            // 过滤掉特定排除标签
            if (node.tagDefName != null && ExcludedTags.Contains(node.tagDefName)) return false;

            // 过滤掉装备节点 ( Apparel / Headgear )
            // 这可以避免导入动态变化的装备层，减少预览时的锚点失效问题
            if (node.tagDefName != null && (node.tagDefName.Contains("Apparel") || node.tagDefName.Contains("Headgear"))) return false;
            
            // 检查 workerClass 是否指示为装备
            if (node.workerClass != null && (node.workerClass.Contains("Apparel") || node.workerClass.Contains("Headgear"))) return false;

            // 过滤掉确定的容器节点（如逻辑空节点）
            string label = node.debugLabel ?? "";
            if (ExcludedLabels.Contains(label)) return false;
            
            // 过滤名称中包含特定关键字的节点（如包裹状态）
            string labelLower = label.ToLower();
            if (labelLower.Contains("swaddl") || labelLower.Contains("襁褓")) return false;

            // 只要有贴图路径就认为是有效图层（移除斜杠检查以兼容模组和原版特殊路径）
            return true;
        }

        /// <summary>
        /// 从已有的 PawnSkinDef 导入配置
        /// </summary>
        private static ImportResult ImportFromExistingSkin(PawnSkinDef skinDef)
        {
            var result = new ImportResult
            {
                layers = new List<PawnLayerConfig>(),
                sourcePaths = new List<string>()
            };
            
            foreach (var layer in skinDef.layers)
            {
                // 深拷贝图层配置
                var clonedLayer = new PawnLayerConfig
                {
                    layerName = layer.layerName,
                    texPath = layer.texPath,
                    anchorTag = layer.anchorTag,
                    anchorPath = layer.anchorPath,
                    colorType = layer.colorType,
                    customColor = layer.customColor,
                    drawOrder = layer.drawOrder,
                    offset = layer.offset,
                    offsetEast = layer.offsetEast,
                    offsetNorth = layer.offsetNorth,
                    scale = layer.scale,
                    visible = layer.visible,
                    graphicClass = layer.graphicClass
                };
                result.layers.Add(clonedLayer);
            }
            
            // 如果皮肤有隐藏路径，也导入
            if (skinDef.hiddenPaths != null)
            {
                result.sourcePaths = new List<string>(skinDef.hiddenPaths);
            }
            
            Log.Message($"[CharacterStudio] 从现有皮肤导入 {result.layers.Count} 个图层，{result.sourcePaths.Count} 个隐藏路径");
            return result;
        }
        
        /// <summary>
        /// 根据节点信息生成有意义的图层名称
        /// </summary>
        private static string GenerateLayerName(RenderNodeSnapshot node, int index)
        {
            // 优先使用 texPath 的文件名
            if (!string.IsNullOrEmpty(node.texPath) && node.texPath != "Dynamic/Unknown")
            {
                try
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(node.texPath);
                    if (!string.IsNullOrEmpty(fileName)) return fileName;
                }
                catch { }
            }

            // 其次使用 debugLabel
            if (!string.IsNullOrEmpty(node.debugLabel)) return node.debugLabel;

            // 最后使用索引
            return $"Layer_{index}";
        }
    }
}
