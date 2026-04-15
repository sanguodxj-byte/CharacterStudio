using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;
using HarmonyLib;

namespace CharacterStudio.Introspection
{
    /// <summary>
    /// RenderTreeParser - 解析 Pawn 的渲染树结构
    /// 支持原版渲染树及第三方扩展（如 HAR）
    ///
    /// 子文件职责：
    ///   RenderTreeParser.TexturePath.cs  — 纹理路径多级回退解析
    ///   RenderTreeParser.RuntimeData.cs  — 运行时变换及颜色通道捕获
    ///   RenderTreeParser.Debug.cs        — 渲染树内省调试日志
    /// </summary>
    public static partial class RenderTreeParser
    {
        private static readonly HashSet<string> captureFailureWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ─────────────────────────────────────────────
        // 线程检测
        // ─────────────────────────────────────────────

        /// <summary>
        /// 是否在主线程。使用反射以兼容不同 RimWorld/Verse 版本。
        /// </summary>
        internal static bool IsMainThread()
        {
            try
            {
                var unityDataType = AccessTools.TypeByName("Verse.UnityData");
                if (unityDataType != null)
                {
                    var prop = AccessTools.Property(unityDataType, "IsInMainThread");
                    if (prop != null)
                    {
                        object value = prop.GetValue(null, null);
                        if (value is bool b) return b;
                    }

                    var field = AccessTools.Field(unityDataType, "IsInMainThread");
                    if (field != null)
                    {
                        object value = field.GetValue(null);
                        if (value is bool b) return b;
                    }
                }
            }
            catch
            {
                // 忽略并走保守默认值
            }

            // 无法判断时默认 true，避免影响正常主线程逻辑
            return true;
        }

        // 调试开关：当开启原版开发者模式且显式开启时激活
        private static bool IsVerboseDebugEnabled => Prefs.DevMode;

        // ─────────────────────────────────────────────
        // 公共入口
        // ─────────────────────────────────────────────

        /// <summary>
        /// 捕捉给定 Pawn 的完整渲染树快照。
        /// 包含强制树构建逻辑，确保渲染树已初始化
        /// </summary>
        public static RenderNodeSnapshot? Capture(Pawn pawn)
        {
            if (pawn?.Drawer?.renderer == null)
                return null;

            var renderTree = pawn.Drawer.renderer.renderTree;
            if (renderTree == null) return null;

            try
            {
                if (!renderTree.Resolved)
                {
                    renderTree.SetDirty();
                    renderTree.EnsureInitialized(PawnRenderFlags.None);
                }
                else
                {
                    renderTree.EnsureInitialized(PawnRenderFlags.None);
                }

                var rootNode = renderTree.rootNode;
                if (rootNode == null)
                {
                    Log.Warning($"[CharacterStudio] RenderTreeParser: rootNode is null for pawn {pawn.LabelShort}");
                    return null;
                }

                return RecursiveParse(rootNode, 0, pawn, "Root", 0);
            }
            catch (Exception ex)
            {
                string pawnKey = pawn?.ThingID ?? pawn?.LabelShort ?? "<null-pawn>";
                lock (captureFailureWarnings)
                {
                    if (captureFailureWarnings.Add(pawnKey))
                    {
                        Log.Warning($"[CharacterStudio] RenderTreeParser.Capture failed, 已回退为空结果: {pawn?.LabelShort ?? "<null>"}, {ex.Message}");
                    }
                }
                return null;
            }
        }

        // ─────────────────────────────────────────────
        // 递归解析
        // ─────────────────────────────────────────────

        /// <summary>
        /// 递归解析渲染节点，生成 RenderNodeSnapshot 树
        /// </summary>
        private static RenderNodeSnapshot? RecursiveParse(
            PawnRenderNode? gameNode,
            int depth,
            Pawn pawn,
            string currentPath,
            int childIndex)
        {
            if (gameNode == null) return null;

            try
            {
                string label   = gameNode.ToString() ?? "Unnamed Node";
                var    props   = gameNode.Props;
                string tagDef  = props?.tagDef?.defName ?? "Untagged";
                bool   visible = gameNode.DebugEnabled;
                string workerName = props?.workerClass?.Name ?? "Default";

                string resolvedTexPath = GetNodeTexturePath(gameNode, pawn);

                // 捕获 Graphic 基础信息
                Type?   graphicClass    = null;
                Vector2 graphicDrawSize = Vector2.one;
                Color   graphicColor    = Color.white;
                Color   graphicColorTwo = Color.white;
                string  shaderName      = "";
                string  maskPath        = "";
                string  colorChannel    = "";

                CaptureGraphicInfo(gameNode, pawn, label, tagDef, resolvedTexPath,
                    out graphicClass, out graphicDrawSize,
                    out graphicColor, out graphicColorTwo,
                    out shaderName, out maskPath);

                colorChannel = CaptureExternalColorChannel(gameNode, props);

                // 运行时颜色（主线程优先）
                Color nodeColor = props?.color ?? Color.white;
                if (IsMainThread())
                {
                    try { nodeColor = gameNode.ColorFor(pawn); } catch { }
                }

                // 内部调试输出（仅在显式启用时触发）
                bool shouldDebug = IsVerboseDebugEnabled && ShouldInspectNodeDebug(label, tagDef);
                if (shouldDebug)
                    EmitNodeDebugInfo(gameNode, label, tagDef, props, nodeColor);

                // 捕获运行时变换数据
                CaptureRuntimeTransform(
                    gameNode, pawn, props, label, tagDef, shouldDebug,
                    out Vector3 nodeOffset,
                    out float   nodeScale,
                    out float   nodeDrawOrder,
                    out Vector3 runtimeOffset,
                    out Vector3 runtimeOffsetEast,
                    out Vector3 runtimeOffsetNorth,
                    out Vector3 runtimeScale,
                    out float   runtimeRotation,
                    out bool    runtimeDataValid);

                Color runtimeColor = nodeColor;

                if (shouldDebug)
                    EmitFinalDebugInfo(runtimeOffset, runtimeScale, runtimeColor);

                var snapshot = new RenderNodeSnapshot
                {
                    debugLabel        = label,
                    tagDefName        = tagDef,
                    meshDefName       = "N/A",
                    isVisible         = visible,
                    treeLayer         = depth,
                    workerClass       = workerName,
                    texPath           = resolvedTexPath,
                    color             = nodeColor,
                    offset            = nodeOffset,
                    scale             = nodeScale,
                    drawOrder         = nodeDrawOrder,
                    uniqueNodePath    = currentPath,
                    childIndex        = childIndex,
                    runtimeOffset     = runtimeOffset,
                    runtimeOffsetEast = runtimeOffsetEast,
                    runtimeOffsetNorth= runtimeOffsetNorth,
                    runtimeScale      = runtimeScale,
                    runtimeColor      = runtimeColor,
                    runtimeRotation   = runtimeRotation,
                    runtimeDataValid  = runtimeDataValid,
                    graphicClass      = graphicClass,
                    graphicDrawSize   = graphicDrawSize,
                    graphicColor      = graphicColor,
                    graphicColorTwo   = graphicColorTwo,
                    shaderName        = shaderName,
                    maskPath          = maskPath,
                    colorChannel      = colorChannel
                };

                RecursiveParseChildren(gameNode, depth, pawn, currentPath, snapshot);
                return snapshot;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] RenderTreeParser.RecursiveParse failed at depth {depth}: {ex.Message}");
                return null;
            }
        }

        private static void RecursiveParseChildren(
            PawnRenderNode gameNode,
            int depth,
            Pawn pawn,
            string currentPath,
            RenderNodeSnapshot snapshot)
        {
            if (gameNode.children == null) return;

            var tagIndexCounter = new Dictionary<string, int>();
            foreach (var child in gameNode.children)
            {
                if (child == null) continue;

                string childTag = child.Props?.tagDef?.defName ?? "Untagged";
                if (!tagIndexCounter.ContainsKey(childTag))
                    tagIndexCounter[childTag] = 0;
                int thisChildIndex = tagIndexCounter[childTag]++;

                string childPath = $"{currentPath}/{childTag}:{thisChildIndex}";
                var childSnap = RecursiveParse(child, depth + 1, pawn, childPath, thisChildIndex);
                if (childSnap != null)
                    snapshot.children.Add(childSnap);
            }
        }
    }
}
