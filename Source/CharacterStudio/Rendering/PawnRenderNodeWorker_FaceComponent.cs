using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 面部渲染工人 - 驱动 Head 节点整张贴图的表情切换
    /// 当 PawnFaceConfig 启用时，根据当前表情状态切换头部贴图。
    ///
    /// 性能优化：
    ///   - 表情贴图按 (path, shaderName, color) 三元组缓存为 Graphic 对象，避免每帧 new。
    ///   - ContentFinder "_north" 探测结果按路径缓存，避免每帧文件系统查询。
    ///   - 缓存随游戏退出自动释放（静态字典，生命周期与进程绑定）。
    /// </summary>
    public class PawnRenderNodeWorker_FaceComponent : PawnRenderNodeWorker
    {
        // ─────────────────────────────────────────────
        // 静态缓存
        // ─────────────────────────────────────────────

        // 表情 Graphic 缓存：key = "path|shaderName|colorHex"
        private static readonly Dictionary<string, Graphic> graphicCache
            = new Dictionary<string, Graphic>(StringComparer.Ordinal);

        // _north 探测缓存：key = path, value = isMulti
        private static readonly Dictionary<string, bool> isMultiCache
            = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // ─────────────────────────────────────────────
        // Worker 覆写
        // ─────────────────────────────────────────────

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector2 originalDrawSize = node.Props.drawSize;
            node.Props.drawSize = Vector2.one;
            Vector3 baseScale;
            try
            {
                baseScale = base.ScaleFor(node, parms);
            }
            finally
            {
                node.Props.drawSize = originalDrawSize;
            }

            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                Vector2 cfg = customNode.config.scale;
                float sx = cfg.x <= 0f ? 1f : cfg.x;
                float sy = cfg.y <= 0f ? 1f : cfg.y;
                baseScale = new Vector3(sx * baseScale.x, 1f, sy * baseScale.z);
            }

            if (node.debugScale != 1f)
                baseScale *= node.debugScale;

            return baseScale;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion baseRot = base.RotationFor(node, parms);

            if (Mathf.Abs(node.debugAngleOffset) > 0.01f)
                baseRot *= Quaternion.Euler(0f, node.debugAngleOffset, 0f);

            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                float rot = customNode.config.rotation;
                if (Mathf.Abs(rot) > 0.01f)
                    baseRot *= Quaternion.Euler(0f, rot, 0f);
            }

            return baseRot;
        }

        /// <summary>
        /// 根据当前表情获取头部贴图。
        /// 若 faceConfig 未启用或无对应表情，回退到原版渲染。
        /// </summary>
        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            var comp = parms.pawn.GetComp<CompPawnSkin>();
            var skin = comp?.ActiveSkin;

            if (skin?.faceConfig?.enabled == true)
            {
                ExpressionType exp  = comp?.GetEffectiveExpression() ?? ExpressionType.Neutral;
                string         path = skin.faceConfig.GetTexPath(exp);

                if (!string.IsNullOrEmpty(path))
                {
                    Shader shader    = node.ShaderFor(parms.pawn) ?? ShaderDatabase.Cutout;
                    Color  nodeColor = node.Props?.color ?? Color.white;

                    return GetOrBuildGraphic(path, shader, nodeColor);
                }
            }

            return base.GetGraphic(node, parms);
        }

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
            => base.CanDrawNow(node, parms);

        // ─────────────────────────────────────────────
        // 缓存辅助
        // ─────────────────────────────────────────────

        private static Graphic GetOrBuildGraphic(string path, Shader shader, Color color)
        {
            // 缓存键：路径 + shader 名称 + 颜色 RGBA（精确到 F3 避免浮点抖动）
            string key = BuildCacheKey(path, shader, color);
            if (graphicCache.TryGetValue(key, out var cached))
                return cached;

            Graphic g = BuildGraphic(path, shader, color);
            graphicCache[key] = g;
            return g;
        }

        private static Graphic BuildGraphic(string path, Shader shader, Color color)
        {
            bool isAbsolutePath = System.IO.Path.IsPathRooted(path);

            if (isAbsolutePath)
            {
                // 外部文件：使用 Graphic_Runtime，绕过 ContentFinder
                var req = new GraphicRequest(
                    typeof(Graphic_Runtime), path, shader,
                    Vector2.one, color, Color.white,
                    null, 0, null, null);
                var gr = new Graphic_Runtime();
                gr.Init(req);
                return gr;
            }
            else
            {
                // 游戏内路径：探测是否为 Multi（结果缓存）
                bool isMulti = GetOrDetectIsMulti(path);
                if (isMulti)
                    return GraphicDatabase.Get<Graphic_Multi>(path, shader, Vector2.one, color);
                else
                    return GraphicDatabase.Get<Graphic_Single>(path, shader, Vector2.one, color);
            }
        }

        private static bool GetOrDetectIsMulti(string path)
        {
            if (isMultiCache.TryGetValue(path, out bool cached))
                return cached;

            bool isMulti = ContentFinder<Texture2D>.Get(path + "_north", false) != null;
            isMultiCache[path] = isMulti;
            return isMulti;
        }

        private static string BuildCacheKey(string path, Shader shader, Color color)
            => $"{path}|{shader?.name ?? ""}|{color.r:F3},{color.g:F3},{color.b:F3},{color.a:F3}";

        /// <summary>
        /// 清除表情图形缓存（皮肤热重载时调用）
        /// </summary>
        public static void ClearCache()
        {
            graphicCache.Clear();
            isMultiCache.Clear();
        }
    }
}
