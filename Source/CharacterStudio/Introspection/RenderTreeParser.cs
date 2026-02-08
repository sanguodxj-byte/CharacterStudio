using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;
using HarmonyLib;

namespace CharacterStudio.Introspection
{
    /// <summary>
    /// RenderTreeParser - 解析 Pawn 的渲染树
    /// 支持原版 RimWorld 和 HAR (Humanoid Alien Races) 模组
    /// </summary>
    public static class RenderTreeParser
    {
        /// <summary>
        /// Entry point: Captures the entire tree of a Pawn.
        /// 包含强制树构建逻辑，确保渲染树已初始化
        /// </summary>
        public static RenderNodeSnapshot? Capture(Pawn pawn)
        {
            // 检查点2: 空引用防御
            if (pawn?.Drawer?.renderer == null)
                return null;

            var renderTree = pawn.Drawer.renderer.renderTree;
            if (renderTree == null) return null;

            try
            {
                // 检查点3: 强制树构建
                // 当树处于 Dirty 状态或尚未构建时，强制刷新
                if (!renderTree.Resolved)
                {
                    renderTree.SetDirty();
                    // EnsureInitialized 会触发 TrySetupGraphIfNeeded
                    renderTree.EnsureInitialized(PawnRenderFlags.None);
                }
                else
                {
                    // 即使已解析，也确保节点已初始化
                    renderTree.EnsureInitialized(PawnRenderFlags.None);
                }

                var rootNode = renderTree.rootNode;
                if (rootNode == null)
                {
                    Log.Warning($"[CharacterStudio] RenderTreeParser: rootNode is null for pawn {pawn.LabelShort}");
                    return null;
                }

                // 阶段2: 从 "Root" 开始生成路径
                return RecursiveParse(rootNode, 0, pawn, "Root", 0);
            }
            catch (Exception ex)
            {
                Log.Error($"[TSS.Studio] RenderTreeParser.Capture failed for {pawn.LabelShort}: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 递归解析渲染节点
        /// 阶段2升级: 增加 parentPath 和 childIndex 参数，生成唯一 NodePath
        /// </summary>
        /// <param name="gameNode">当前游戏渲染节点</param>
        /// <param name="depth">树深度</param>
        /// <param name="pawn">所属 Pawn</param>
        /// <param name="currentPath">当前节点的完整路径</param>
        /// <param name="childIndex">在父节点中同名子节点的索引</param>
        private static RenderNodeSnapshot? RecursiveParse(PawnRenderNode? gameNode, int depth, Pawn pawn, string currentPath, int childIndex)
        {
            // 检查点2: 空引用防御
            if (gameNode == null) return null;

            try
            {
                // Extract basic properties safely
                // PawnRenderNode.ToString() returns the debug label from props
                string label = gameNode.ToString() ?? "Unnamed Node";
                
                // Access Props safely - 检查点2
                var props = gameNode.Props;
                
                // 检查点2: tagDef 可能为 null
                string tagDef = props?.tagDef?.defName ?? "Untagged";
                string meshDef = "N/A";
                
                bool visible = gameNode.DebugEnabled;
                
                string workerName = props?.workerClass?.Name ?? "Default";

                // 检查点1: HAR 兼容性 - 解析纹理路径
                string resolvedTexPath = GetNodeTexturePath(gameNode, pawn);

                // 捕获 Graphic 类类型，确保我们可以正确重建多方向图形
                Type graphicClass = null;
                try
                {
                    // 1. 优先尝试使用公共 API 获取当前 Graphic
                    var graphic = gameNode.GraphicFor(pawn);
                    
                    // 2. 如果公共 API 返回空，尝试反射访问内部属性
                    if (graphic == null)
                    {
                        graphic = AccessTools.Property(typeof(PawnRenderNode), "Graphic")?.GetValue(gameNode, null) as Graphic;
                    }

                    if (graphic != null)
                    {
                        graphicClass = graphic.GetType();
                    }
                }
                catch { }

                // ===== 运行时属性捕获 =====
                // 创建 PawnDrawParms 用于调用 Worker 方法
                var drawParms = new PawnDrawParms()
                {
                    pawn = pawn,
                    facing = pawn.Rotation
                };

                // 获取运行时颜色 - 使用 node.ColorFor() 而非静态 props.color
                Color nodeColor = Color.white;
                try
                {
                    nodeColor = gameNode.ColorFor(pawn);
                }
                catch
                {
                    // 回退到 props.color
                    if (props?.color != null)
                    {
                        nodeColor = props.color.Value;
                    }
                }

                // 获取运行时偏移和缩放 - 使用 node.GetTransform()
                Vector3 nodeOffset = Vector3.zero;
                float nodeScale = 1f;
                float nodeDrawOrder = 0f;
                
                // ===== 运行时数据探针 =====
                Vector3 runtimeOffset = Vector3.zero;
                Vector3 runtimeOffsetEast = Vector3.zero;  // East 朝向偏移
                Vector3 runtimeOffsetNorth = Vector3.zero; // North 朝向偏移
                Vector3 runtimeScale = Vector3.one;
                Color runtimeColor = Color.white;
                float runtimeRotation = 0f;
                bool runtimeDataValid = false;
                
                try
                {
                    // ===== 核心修正: 使用固定朝向获取基准数据 =====
                    // CS 的 offset 是基于 South 朝向的基准值
                    // offsetEast 是 East 朝向相对于 South 的额外偏移
                    var worker = gameNode.Worker;
                    
                    if (worker != null)
                    {
                        // 创建 South 朝向的 drawParms 用于获取基准偏移
                        var southParms = new PawnDrawParms()
                        {
                            pawn = pawn,
                            facing = Rot4.South
                        };
                        
                        // 创建 East 朝向的 drawParms 用于获取侧面偏移
                        var eastParms = new PawnDrawParms()
                        {
                            pawn = pawn,
                            facing = Rot4.East
                        };
                        
                        // 创建 North 朝向的 drawParms 用于获取北向偏移
                        var northParms = new PawnDrawParms()
                        {
                            pawn = pawn,
                            facing = Rot4.North
                        };
                        
                        // 使用 South 朝向获取基准偏移
                        Vector3 southOffset = worker.OffsetFor(gameNode, southParms, out Vector3 pivotSouth);
                        if (southOffset != Vector3.zero)
                        {
                            nodeOffset = southOffset;
                            runtimeOffset = southOffset;
                            Log.Message($"[CS.Debug] 节点 '{label}' South偏移: {southOffset}");
                        }
                        
                        // 使用 East 朝向获取侧面偏移
                        Vector3 eastOffset = worker.OffsetFor(gameNode, eastParms, out Vector3 pivotEast);
                        // 计算 East 相对于 South 的差值作为 offsetEast
                        Vector3 eastDelta = eastOffset - southOffset;
                        if (eastDelta != Vector3.zero)
                        {
                            runtimeOffsetEast = eastDelta;
                            Log.Message($"[CS.Debug] 节点 '{label}' East偏移差值: {eastDelta}");
                        }
                        
                        // 使用 North 朝向获取北向偏移
                        Vector3 northOffset = worker.OffsetFor(gameNode, northParms, out Vector3 pivotNorth);
                        // 计算 North 相对于 South 的差值作为 offsetNorth
                        Vector3 northDelta = northOffset - southOffset;
                        if (northDelta != Vector3.zero)
                        {
                            runtimeOffsetNorth = northDelta;
                            Log.Message($"[CS.Debug] 节点 '{label}' North偏移差值: {northDelta}");
                        }
                        
                        // 使用 Worker.ScaleFor() 获取缩放（使用 South 朝向）
                        Vector3 workerScale = worker.ScaleFor(gameNode, southParms);
                        float avgScale = (workerScale.x + workerScale.z) / 2f;
                        if (avgScale != 1f)
                        {
                            nodeScale = avgScale;
                            Log.Message($"[CS.Debug] 节点 '{label}' 缩放: {workerScale} -> avgScale={avgScale}");
                        }
                        
                        // 使用 Worker.RotationFor() 获取旋转（使用 South 朝向）
                        Quaternion workerRotation = worker.RotationFor(gameNode, southParms);
                        runtimeRotation = workerRotation.eulerAngles.y;
                        
                        // 获取绘制顺序
                        nodeDrawOrder = worker.LayerFor(gameNode, southParms);
                        
                        runtimeDataValid = true;
                    }
                    else
                    {
                        // Worker 为空时，回退到 GetTransform（使用 South 朝向）
                        var southParms = new PawnDrawParms()
                        {
                            pawn = pawn,
                            facing = Rot4.South
                        };
                        
                        gameNode.GetTransform(southParms, out Vector3 transformOffset, out _, out Quaternion rotation, out Vector3 transformScale);
                        
                        if (transformOffset != Vector3.zero)
                        {
                            nodeOffset = transformOffset;
                            runtimeOffset = transformOffset;
                        }
                        
                        float avgScale = (transformScale.x + transformScale.z) / 2f;
                        if (avgScale != 1f)
                        {
                            nodeScale = avgScale;
                        }
                        
                        runtimeRotation = rotation.eulerAngles.y;
                        nodeDrawOrder = props?.baseLayer ?? 0f;
                        runtimeDataValid = true;
                    }
                    
                    // 更新运行时数据
                    runtimeScale = new Vector3(nodeScale, 1f, nodeScale);
                    runtimeColor = nodeColor;
                    
                    // 汇总调试日志
                    if (nodeOffset != Vector3.zero || nodeScale != 1f || runtimeOffsetEast != Vector3.zero || runtimeOffsetNorth != Vector3.zero)
                    {
                        Log.Message($"[CS.Debug] 节点 '{label}' 最终值: offset={nodeOffset}, offsetEast={runtimeOffsetEast}, offsetNorth={runtimeOffsetNorth}, scale={nodeScale}, color={nodeColor}");
                    }
                }
                catch (Exception ex)
                {
                    // 回退到 debug 值
                    nodeOffset = gameNode.debugOffset;
                    nodeScale = gameNode.debugScale;
                    if (props != null)
                    {
                        nodeDrawOrder = props.baseLayer;
                    }
                    runtimeDataValid = false;
                    Log.Warning($"[CS.Debug] 节点 '{label}' 数据捕获失败: {ex.Message}，使用 debug 值: offset={nodeOffset}, scale={nodeScale}");
                }

                var snapshot = new RenderNodeSnapshot
                {
                    debugLabel = label,
                    tagDefName = tagDef,
                    meshDefName = meshDef,
                    isVisible = visible,
                    treeLayer = depth,
                    workerClass = workerName,
                    texPath = resolvedTexPath,
                    color = nodeColor,
                    // 偏移和缩放信息
                    offset = nodeOffset,
                    scale = nodeScale,
                    drawOrder = nodeDrawOrder,
                    // 阶段2: 设置唯一路径和索引
                    uniqueNodePath = currentPath,
                    childIndex = childIndex,
                    // 运行时数据探针
                    runtimeOffset = runtimeOffset,
                    runtimeOffsetEast = runtimeOffsetEast,
                    runtimeOffsetNorth = runtimeOffsetNorth,
                    runtimeScale = runtimeScale,
                    runtimeColor = runtimeColor,
                    runtimeRotation = runtimeRotation,
                    runtimeDataValid = runtimeDataValid,
                    graphicClass = graphicClass
                };

                // Recursively capture children - 检查点2: 空检查
                // 阶段2: 追踪同名子节点的索引
                if (gameNode.children != null)
                {
                    // 用于追踪每个 tag 出现的次数
                    var tagIndexCounter = new Dictionary<string, int>();
                    
                    foreach (var child in gameNode.children)
                    {
                        if (child == null) continue;
                        
                        // 获取子节点的 tag
                        string childTag = child.Props?.tagDef?.defName ?? "Untagged";
                        
                        // 计算此 tag 的索引
                        if (!tagIndexCounter.ContainsKey(childTag))
                        {
                            tagIndexCounter[childTag] = 0;
                        }
                        int thisChildIndex = tagIndexCounter[childTag];
                        tagIndexCounter[childTag]++;
                        
                        // 生成子节点路径: {currentPath}/{childTag}:{index}
                        string childPath = $"{currentPath}/{childTag}:{thisChildIndex}";
                        
                        var childSnap = RecursiveParse(child, depth + 1, pawn, childPath, thisChildIndex);
                        if (childSnap != null)
                        {
                            snapshot.children.Add(childSnap);
                        }
                    }
                }

                return snapshot;
            }
            catch (Exception ex)
            {
                Log.Warning($"[TSS.Studio] RenderTreeParser.RecursiveParse failed at depth {depth}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查点1: HAR 兼容性 - 获取节点的纹理路径
        /// 实现多级回退策略：texPath -> Graphic.path (不访问材质以避免渲染上下文错误)
        /// </summary>
        private static string GetNodeTexturePath(PawnRenderNode node, Pawn pawn)
        {
            try
            {
                var props = node.Props;

                // 1. 优先尝试读取标准的 texPath (原版逻辑)
                if (props != null && !string.IsNullOrEmpty(props.texPath))
                {
                    return props.texPath;
                }

                // 2. 尝试从 PrimaryGraphic 获取路径
                try
                {
                    var primaryGraphic = node.PrimaryGraphic;
                    if (primaryGraphic != null && !string.IsNullOrEmpty(primaryGraphic.path))
                    {
                        return primaryGraphic.path;
                    }
                }
                catch
                {
                    // 某些节点访问 PrimaryGraphic 可能失败
                }

                // 3. [关键] HAR 兼容性回退：尝试从 Graphics 列表获取
                try
                {
                    var graphics = node.Graphics;
                    if (graphics != null && graphics.Count > 0)
                    {
                        foreach (var graphic in graphics)
                        {
                            if (graphic != null && !string.IsNullOrEmpty(graphic.path))
                            {
                                return graphic.path;
                            }
                            // 注意：不再尝试访问 MatSingle，因为它可能导致渲染上下文错误
                        }
                    }
                }
                catch
                {
                    // 某些节点访问 Graphics 可能失败
                }

                // 4. [增强] 尝试从 GraphicData 获取路径 (适用于某些动态生成的 Graphic)
                try
                {
                    var primaryGraphic = node.PrimaryGraphic;
                    if (primaryGraphic?.data != null && !string.IsNullOrEmpty(primaryGraphic.data.texPath))
                    {
                        return primaryGraphic.data.texPath;
                    }
                }
                catch { }

                // 5. 检查节点是否只是逻辑节点（无图形）
                if (props?.useGraphic == false)
                {
                    return "No Graphic (Logic Only)";
                }

                return "Dynamic/Unknown";
            }
            catch (Exception ex)
            {
                Log.Warning($"[TSS.Studio] GetNodeTexturePath failed: {ex.Message}");
                return "Error";
            }
        }
    }
}
