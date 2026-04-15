using System;
using Verse;
using HarmonyLib;
using UnityEngine;

namespace CharacterStudio.Introspection
{
    public static partial class RenderTreeParser
    {
        // ─────────────────────────────────────────────
        // 纹理路径解析
        // 多级回退策略：
        //   1. props.texPath（原版标准）
        //   2. 第三方/内置 GraphicFor 逻辑
        //   3. PrimaryGraphic
        //   4. Graphics 列表
        //   5. GraphicData.texPath
        //   6. 反射私有字段（cachedGraphic / graphic / texPath）
        //   7. Logic Only 标记
        //   8. Dynamic/Unknown
        // ─────────────────────────────────────────────

        private static string GetNodeTexturePath(PawnRenderNode node, Pawn pawn)
        {
            try
            {
                var props = node.Props;

                // 1. 原版标准路径
                if (props != null && !string.IsNullOrEmpty(props.texPath))
                    return props.texPath;

                // 非主线程：禁止触发资源加载
                if (!IsMainThread())
                {
                    if (props?.useGraphic == false)
                        return "No Graphic (Logic Only)";
                    return "Dynamic/Unknown";
                }

                // 2. 第三方/动态路径解析
                try
                {
                    var graphicForPawn = node.GraphicFor(pawn);
                    if (graphicForPawn != null && !string.IsNullOrEmpty(graphicForPawn.path))
                    {
                        DebugLog($"[CS.Studio.Debug] 节点 '{node}' 从内置方法获取路径: {graphicForPawn.path}");
                        return graphicForPawn.path;
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"[CS.Studio.Debug] 节点 '{node}' 内置解析失败: {ex.Message}");
                }

                // 3. PrimaryGraphic
                try
                {
                    var primaryGraphic = node.PrimaryGraphic;
                    if (primaryGraphic != null && !string.IsNullOrEmpty(primaryGraphic.path))
                        return primaryGraphic.path;
                }
                catch { }

                // 4. Graphics 列表
                try
                {
                    var graphics = node.Graphics;
                    if (graphics != null && graphics.Count > 0)
                    {
                        foreach (var graphic in graphics)
                        {
                            if (graphic != null && !string.IsNullOrEmpty(graphic.path))
                                return graphic.path;
                        }
                    }
                }
                catch { }

                // 5. GraphicData.texPath
                try
                {
                    var primary = node.PrimaryGraphic;
                    if (primary?.data != null && !string.IsNullOrEmpty(primary.data.texPath))
                        return primary.data.texPath;
                }
                catch { }

                // 6. 反射私有字段
                try
                {
                    foreach (var fieldName in new[] { "cachedGraphic", "graphic" })
                    {
                        var f = AccessTools.Field(node.GetType(), fieldName);
                        if (f != null)
                        {
                            var g = f.GetValue(node) as Graphic;
                            if (g != null && !string.IsNullOrEmpty(g.path))
                            {
                                DebugLog($"[CS.Studio.Debug] 节点 '{node}' 从 {fieldName} 字段获取路径: {g.path}");
                                return g.path;
                            }
                        }
                    }

                    var texPathField = AccessTools.Field(node.GetType(), "texPath");
                    if (texPathField != null)
                    {
                        var texPath = texPathField.GetValue(node) as string;
                        if (!string.IsNullOrEmpty(texPath))
                        {
                            DebugLog($"[CS.Studio.Debug] 节点 '{node}' 从 texPath 字段获取路径: {texPath}");
                            return texPath!;
                        }
                    }
                }
                catch { }

                // 7. Logic Only
                if (props?.useGraphic == false)
                    return "No Graphic (Logic Only)";

                // 8. 兜底策略
                DebugLog($"[CS.Studio.Debug] 节点 '{node}' ({node.GetType().FullName}) 无法获取纹理路径，标记为 Dynamic/Unknown");
                return "Dynamic/Unknown";
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] GetNodeTexturePath failed: {ex.Message}");
                return "Error";
            }
        }

        /// <summary>
        /// 捕获节点的 Graphic 基础信息（类型、drawSize、颜色、Shader、Mask 路径）
        /// </summary>
        private static void CaptureGraphicInfo(
            PawnRenderNode gameNode,
            Pawn pawn,
            string label,
            string tagDef,
            string resolvedTexPath,
            out Type?   graphicClass,
            out Vector2 graphicDrawSize,
            out Color   graphicColor,
            out Color   graphicColorTwo,
            out string  shaderName,
            out string  maskPath)
        {
            graphicClass    = null;
            graphicDrawSize = Vector2.one;
            graphicColor    = Color.white;
            graphicColorTwo = Color.white;
            shaderName      = "";
            maskPath        = "";

            if (!IsMainThread()) return;

            try
            {
                if (!string.IsNullOrEmpty(resolvedTexPath) &&
                    (System.IO.Path.IsPathRooted(resolvedTexPath) || resolvedTexPath.StartsWith("/")))
                {
                    graphicClass    = typeof(CharacterStudio.Rendering.Graphic_Runtime);
                    graphicDrawSize = Vector2.one;
                    return;
                }

                var graphic = gameNode.GraphicFor(pawn);
                if (graphic == null)
                    graphic = AccessTools.Property(typeof(PawnRenderNode), "Graphic")?.GetValue(gameNode, null) as Graphic;

                if (graphic != null)
                {
                    graphicClass    = graphic.GetType();
                    graphicDrawSize = graphic.drawSize;
                    graphicColor    = graphic.color;
                    graphicColorTwo = graphic.colorTwo;

                    if (graphic.Shader != null)
                        shaderName = graphic.Shader.name;

                    if (!string.IsNullOrEmpty(graphic.path))
                    {
                        string potentialMask = graphic.path + "_m";
                        if (ContentFinder<Texture2D>.Get(potentialMask, false) != null)
                            maskPath = potentialMask;
                    }
                }
            }
            catch { }
        }
    }
}
