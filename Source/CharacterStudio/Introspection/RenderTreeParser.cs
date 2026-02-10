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
                Vector2 graphicDrawSize = Vector2.one;
                Color graphicColor = Color.white;
                Color graphicColorTwo = Color.white;
                
                // 捕获 Shader 和 Mask
                string shaderName = "";
                string maskPath = "";
                
                // HAR 兼容性：颜色通道
                string colorChannel = "";

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
                        graphicDrawSize = graphic.drawSize;
                        graphicColor = graphic.color;
                        graphicColorTwo = graphic.colorTwo;
                        
                        if (graphic.Shader != null)
                        {
                            shaderName = graphic.Shader.name;
                        }
                        
                        // 探测 Mask
                        if (!string.IsNullOrEmpty(graphic.path))
                        {
                            string potentialMask = graphic.path + "_m";
                            if (ContentFinder<Texture2D>.Get(potentialMask, false) != null)
                            {
                                maskPath = potentialMask;
                            }
                        }
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
                
                // ===== HAR 兼容性: 提取 colorChannel =====
                // 尝试从节点自身或其属性中提取 HAR 特有的 colorChannel 信息
                try
                {
                    // 1. 尝试从 BodyAddon 获取 (HAR 标准方式)
                    // HAR 的 PawnRenderNode_BodyAddon 通常有一个 'addon' 字段指向 BodyAddon 定义
                    var addonField = AccessTools.Field(gameNode.GetType(), "addon");
                    if (addonField != null)
                    {
                        object addon = addonField.GetValue(gameNode);
                        if (addon != null)
                        {
                            var channelField = AccessTools.Field(addon.GetType(), "colorChannel");
                            if (channelField != null)
                            {
                                object channelVal = channelField.GetValue(addon);
                                if (channelVal != null)
                                {
                                    colorChannel = channelVal.ToString();
                                    // Log.Message($"[CS.HAR] Found colorChannel '{colorChannel}' for node {label}");
                                }
                            }
                        }
                    }
                    
                    // 2. 如果没找到，尝试直接从 Props 获取 (某些变体)
                    if (string.IsNullOrEmpty(colorChannel) && props != null)
                    {
                        var propsChannelField = AccessTools.Field(props.GetType(), "colorChannel");
                        if (propsChannelField != null)
                        {
                            object channelVal = propsChannelField.GetValue(props);
                            if (channelVal != null)
                            {
                                colorChannel = channelVal.ToString();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 仅在调试模式下记录，避免刷屏
                    // Log.Warning($"[CS.HAR] Failed to extract colorChannel for {label}: {ex.Message}");
                }

                // ===== HAR 调试: 详细记录节点信息 =====
                bool isDebugNode = tagDef.ToLower().Contains("head") ||
                                   tagDef.ToLower().Contains("ear") ||
                                   tagDef.ToLower().Contains("tail") ||
                                   label.ToLower().Contains("head") ||
                                   label.ToLower().Contains("ear") ||
                                   label.ToLower().Contains("tail");
                string nodeTypeName = gameNode.GetType().Name;

                // ===== HAR 调试: 探索节点自身的所有字段 =====
                if (isDebugNode)
                {
                    Log.Message($"[CS.HAR.Debug] ====== 节点 '{label}' ({nodeTypeName}) ======");
                    Log.Message($"[CS.HAR.Debug] 节点类型完整名: {gameNode.GetType().FullName}");
                    
                    // 遍历节点自身的所有字段（不是 props）
                    var nodeType = gameNode.GetType();
                    var allNodeFields = nodeType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Log.Message($"[CS.HAR.Debug] 节点字段数量: {allNodeFields.Length}");
                    
                    foreach (var field in allNodeFields)
                    {
                        try
                        {
                            var value = field.GetValue(gameNode);
                            string fieldName = field.Name.ToLower();
                            
                            // 扩展搜索：包含 size, draw, mesh, parent 相关字段
                            if (fieldName.Contains("offset") ||
                                fieldName.Contains("color") ||
                                fieldName.Contains("scale") ||
                                fieldName.Contains("position") ||
                                fieldName.Contains("loc") ||
                                fieldName.Contains("pos") ||
                                fieldName.Contains("size") ||
                                fieldName.Contains("draw") ||
                                fieldName.Contains("mesh") ||
                                fieldName.Contains("parent") ||
                                fieldName.Contains("anchor"))
                            {
                                Log.Message($"[CS.HAR.Debug]   节点.{field.Name} ({field.FieldType.Name}): {value}");
                            }
                        }
                        catch { }
                    }
                    
                    // 检查父节点信息
                    try
                    {
                        var parentField = AccessTools.Field(typeof(PawnRenderNode), "parent");
                        if (parentField != null)
                        {
                            var parentNode = parentField.GetValue(gameNode) as PawnRenderNode;
                            if (parentNode != null)
                            {
                                Log.Message($"[CS.HAR.Debug]   父节点类型: {parentNode.GetType().Name}");
                                Log.Message($"[CS.HAR.Debug]   父节点label: {parentNode}");
                                Log.Message($"[CS.HAR.Debug]   父节点debugScale: {parentNode.debugScale}");
                                Log.Message($"[CS.HAR.Debug]   父节点debugOffset: {parentNode.debugOffset}");
                            }
                        }
                    }
                    catch { }
                    
                    // 也检查属性
                    var allNodeProps = nodeType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    foreach (var prop in allNodeProps)
                    {
                        try
                        {
                            if (!prop.CanRead) continue;
                            var value = prop.GetValue(gameNode);
                            string propName = prop.Name.ToLower();
                            
                            if (propName.Contains("offset") ||
                                propName.Contains("color") ||
                                propName.Contains("scale") ||
                                propName.Contains("position") ||
                                propName.Contains("loc") ||
                                propName.Contains("pos") ||
                                propName.Contains("size") ||
                                propName.Contains("draw"))
                            {
                                Log.Message($"[CS.HAR.Debug]   节点属性.{prop.Name} ({prop.PropertyType.Name}): {value}");
                            }
                        }
                        catch { }
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
                
                // ===== 关键修复: 先读取 props 中的静态偏移作为基础值 =====
                // 原版 RimWorld 的节点偏移通常存储在 props.offset 中
                // Worker.OffsetFor() 返回的是 Worker 额外计算的偏移，很多原版节点返回零
                Vector3 propsOffset = Vector3.zero;
                Vector3 propsOffsetEast = Vector3.zero;
                Vector3 propsOffsetNorth = Vector3.zero;
                
                // 使用反射读取 props 中的偏移属性
                // 不同版本的 RimWorld 和模组可能有不同的属性结构
                if (props != null)
                {
                    try
                    {
                        // 尝试读取 offset 字段/属性
                        var offsetField = AccessTools.Field(props.GetType(), "offset");
                        if (offsetField != null)
                        {
                            propsOffset = (Vector3)offsetField.GetValue(props);
                        }
                        else
                        {
                            var offsetProp = AccessTools.Property(props.GetType(), "offset");
                            if (offsetProp != null)
                            {
                                propsOffset = (Vector3)offsetProp.GetValue(props);
                            }
                        }
                        
                        // 尝试读取 offsetEast 字段/属性
                        var offsetEastField = AccessTools.Field(props.GetType(), "offsetEast");
                        if (offsetEastField != null)
                        {
                            propsOffsetEast = (Vector3)offsetEastField.GetValue(props);
                        }
                        
                        // 尝试读取 offsetNorth 字段/属性
                        var offsetNorthField = AccessTools.Field(props.GetType(), "offsetNorth");
                        if (offsetNorthField != null)
                        {
                            propsOffsetNorth = (Vector3)offsetNorthField.GetValue(props);
                        }
                        
                        // ===== HAR 调试: 打印 props 相关信息 =====
                        if (isDebugNode)
                        {
                            Log.Message($"[CS.HAR.Debug] 节点 '{label}' ({nodeTypeName}) Tag={tagDef}");
                            Log.Message($"[CS.HAR.Debug]   - props类型: {props.GetType().FullName}");
                            Log.Message($"[CS.HAR.Debug]   - props.offset: {propsOffset}");
                            Log.Message($"[CS.HAR.Debug]   - props.offsetEast: {propsOffsetEast}");
                            Log.Message($"[CS.HAR.Debug]   - props.offsetNorth: {propsOffsetNorth}");
                            Log.Message($"[CS.HAR.Debug]   - nodeColor: {nodeColor}");
                            
                            // 列出 props 的所有字段
                            var propsType = props.GetType();
                            var allFields = propsType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            foreach (var field in allFields)
                            {
                                try
                                {
                                    var value = field.GetValue(props);
                                    if (value != null && (field.Name.ToLower().Contains("offset") || field.Name.ToLower().Contains("color") || field.Name.ToLower().Contains("scale")))
                                    {
                                        // 扩展搜索 drawSize
                                        Log.Message($"[CS.HAR.Debug]   - props.{field.Name}: {value}");
                                    }
                                    // 增加 drawSize 和 size 的搜索
                                    if (field.Name.ToLower().Contains("size") || field.Name.ToLower().Contains("draw"))
                                    {
                                        Log.Message($"[CS.HAR.Debug]   - props.{field.Name}: {value}");
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
                
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
                        
                        // ===== HAR 调试: 打印 Worker 返回的偏移 =====
                        if (isDebugNode)
                        {
                            Log.Message($"[CS.HAR.Debug]   - Worker类型: {worker.GetType().FullName}");
                            Log.Message($"[CS.HAR.Debug]   - Worker.OffsetFor(South): {southOffset}");
                        }
                        
                        // ===== 关键修复: 如果 Worker 返回零，使用 props.offset 作为回退 =====
                        if (southOffset == Vector3.zero && propsOffset != Vector3.zero)
                        {
                            southOffset = propsOffset;
                            if (isDebugNode)
                            {
                                Log.Message($"[CS.HAR.Debug]   - 使用 props.offset 回退: {propsOffset}");
                            }
                        }
                        
                        if (southOffset != Vector3.zero)
                        {
                            nodeOffset = southOffset;
                            runtimeOffset = southOffset;
                        }
                        
                        // 使用 East 朝向获取侧面偏移
                        Vector3 eastOffset = worker.OffsetFor(gameNode, eastParms, out Vector3 pivotEast);
                        // 计算 East 相对于 South 的差值作为 offsetEast
                        Vector3 eastDelta = eastOffset - southOffset;
                        
                        // ===== 关键修复: 如果 Worker 差值为零，使用 props.offsetEast 作为回退 =====
                        if (eastDelta == Vector3.zero && propsOffsetEast != Vector3.zero)
                        {
                            eastDelta = propsOffsetEast;
                        }
                        
                        if (eastDelta != Vector3.zero)
                        {
                            runtimeOffsetEast = eastDelta;
                        }
                        
                        // 使用 North 朝向获取北向偏移
                        Vector3 northOffset = worker.OffsetFor(gameNode, northParms, out Vector3 pivotNorth);
                        // 计算 North 相对于 South 的差值作为 offsetNorth
                        Vector3 northDelta = northOffset - southOffset;
                        
                        // ===== 关键修复: 如果 Worker 差值为零，使用 props.offsetNorth 作为回退 =====
                        if (northDelta == Vector3.zero && propsOffsetNorth != Vector3.zero)
                        {
                            northDelta = propsOffsetNorth;
                        }
                        
                        if (northDelta != Vector3.zero)
                        {
                            runtimeOffsetNorth = northDelta;
                        }
                        
                        // 使用 Worker.ScaleFor() 获取缩放（使用 South 朝向）
                        Vector3 workerScale = worker.ScaleFor(gameNode, southParms);
                        float avgScale = (workerScale.x + workerScale.z) / 2f;
                        if (avgScale != 1f)
                        {
                            nodeScale = avgScale;
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
                        
                        // ===== 关键修复: 如果 GetTransform 返回零，使用 props.offset 作为回退 =====
                        if (transformOffset == Vector3.zero && propsOffset != Vector3.zero)
                        {
                            transformOffset = propsOffset;
                        }
                        
                        if (transformOffset != Vector3.zero)
                        {
                            nodeOffset = transformOffset;
                            runtimeOffset = transformOffset;
                        }
                        
                        // 对于 East/North 偏移，直接使用 props 中的值
                        if (propsOffsetEast != Vector3.zero)
                        {
                            runtimeOffsetEast = propsOffsetEast;
                        }
                        if (propsOffsetNorth != Vector3.zero)
                        {
                            runtimeOffsetNorth = propsOffsetNorth;
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
                    
                    // ===== HAR 调试: 最终捕获的数据 =====
                    if (isDebugNode)
                    {
                        Log.Message($"[CS.HAR.Debug]   - 最终 runtimeOffset: {runtimeOffset}");
                        Log.Message($"[CS.HAR.Debug]   - 最终 runtimeScale: {runtimeScale}");
                        Log.Message($"[CS.HAR.Debug]   - 最终 runtimeColor: {runtimeColor}");
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
                    graphicClass = graphicClass,
                    graphicDrawSize = graphicDrawSize,
                    graphicColor = graphicColor,
                    graphicColorTwo = graphicColorTwo,
                    shaderName = shaderName,
                    maskPath = maskPath
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
        /// 实现多级回退策略：texPath -> GraphicFor() -> PrimaryGraphic -> Graphics 列表 -> 反射
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

                // 2. [关键增强] 使用 GraphicFor(pawn) 获取动态生成的图形
                // 这对于 HAR 外星人种族非常重要，因为它们的纹理路径是动态计算的
                try
                {
                    var graphicForPawn = node.GraphicFor(pawn);
                    if (graphicForPawn != null && !string.IsNullOrEmpty(graphicForPawn.path))
                    {
                        Log.Message($"[CS.Debug] 节点 '{node}' 从 GraphicFor() 获取路径: {graphicForPawn.path}");
                        return graphicForPawn.path;
                    }
                }
                catch (Exception ex)
                {
                    // GraphicFor 可能在某些情况下失败
                    Log.Message($"[CS.Debug] 节点 '{node}' GraphicFor() 失败: {ex.Message}");
                }

                // 3. 尝试从 PrimaryGraphic 获取路径
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

                // 4. [关键] HAR 兼容性回退：尝试从 Graphics 列表获取
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
                        }
                    }
                }
                catch
                {
                    // 某些节点访问 Graphics 可能失败
                }

                // 5. [增强] 尝试从 GraphicData 获取路径 (适用于某些动态生成的 Graphic)
                try
                {
                    var primaryGraphic = node.PrimaryGraphic;
                    if (primaryGraphic?.data != null && !string.IsNullOrEmpty(primaryGraphic.data.texPath))
                    {
                        return primaryGraphic.data.texPath;
                    }
                }
                catch { }

                // 6. [HAR 增强] 尝试反射访问 HAR 可能使用的私有/内部字段
                try
                {
                    // 尝试访问 cachedGraphic 或类似的私有字段
                    var cachedGraphicField = AccessTools.Field(node.GetType(), "cachedGraphic");
                    if (cachedGraphicField != null)
                    {
                        var cachedGraphic = cachedGraphicField.GetValue(node) as Graphic;
                        if (cachedGraphic != null && !string.IsNullOrEmpty(cachedGraphic.path))
                        {
                            Log.Message($"[CS.Debug] 节点 '{node}' 从 cachedGraphic 字段获取路径: {cachedGraphic.path}");
                            return cachedGraphic.path;
                        }
                    }
                    
                    // 尝试访问 graphic 字段
                    var graphicField = AccessTools.Field(node.GetType(), "graphic");
                    if (graphicField != null)
                    {
                        var graphic = graphicField.GetValue(node) as Graphic;
                        if (graphic != null && !string.IsNullOrEmpty(graphic.path))
                        {
                            Log.Message($"[CS.Debug] 节点 '{node}' 从 graphic 字段获取路径: {graphic.path}");
                            return graphic.path;
                        }
                    }
                    
                    // 尝试访问 texPath 字段（某些自定义节点可能直接存储）
                    var texPathField = AccessTools.Field(node.GetType(), "texPath");
                    if (texPathField != null)
                    {
                        var texPath = texPathField.GetValue(node) as string;
                        if (!string.IsNullOrEmpty(texPath))
                        {
                            Log.Message($"[CS.Debug] 节点 '{node}' 从 texPath 字段获取路径: {texPath}");
                            return texPath;
                        }
                    }
                }
                catch { }

                // 7. 检查节点是否只是逻辑节点（无图形）
                if (props?.useGraphic == false)
                {
                    return "No Graphic (Logic Only)";
                }

                // 8. 如果所有方法都失败，记录节点类型以便调试
                string nodeType = node.GetType().FullName;
                Log.Message($"[CS.Debug] 节点 '{node}' ({nodeType}) 无法获取纹理路径，标记为 Dynamic/Unknown");

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
