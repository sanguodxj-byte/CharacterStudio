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
                Vector2 cfg = GetConfiguredScale(customNode.config, parms.facing);
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
                float rot = GetConfiguredRotation(customNode.config, parms.facing);
                if (Mathf.Abs(rot) > 0.01f)
                    baseRot *= Quaternion.Euler(0f, rot, 0f);
            }

            return baseRot;
        }

        private static Vector2 GetConfiguredScale(PawnLayerConfig config, Rot4 facing)
        {
            Vector2 scale = config.scale;
            if (facing == Rot4.East || facing == Rot4.West)
                return new Vector2(scale.x * config.scaleEastMultiplier.x, scale.y * config.scaleEastMultiplier.y);

            return scale;
        }

        private static float GetConfiguredRotation(PawnLayerConfig config, Rot4 facing)
        {
            float rotation = config.rotation;
            if (facing == Rot4.North)
                return rotation + config.rotationNorthOffset;

            if (facing == Rot4.East)
                return rotation + config.rotationEastOffset;

            if (facing == Rot4.West)
                return rotation - config.rotationEastOffset;

            return rotation;
        }

        /// <summary>
        /// 根据当前表情获取头部贴图。
        /// 若 faceConfig 未启用或无对应表情，回退到原版渲染。
        /// </summary>
        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            var comp = parms.pawn.GetComp<CompPawnSkin>();
            var skin = comp?.ActiveSkin;
            var runtimeState = comp?.CurrentFaceRuntimeState;
            var compiledData = comp?.CurrentFaceRuntimeCompiledData;

            if (skin?.faceConfig?.enabled == true)
            {
                ExpressionType exp = comp?.GetEffectiveExpression() ?? ExpressionType.Neutral;
                int animTick = comp?.GetExpressionAnimTick() ?? 0;
                FaceRenderTrack currentTrack = runtimeState?.currentTrack ?? FaceRenderTrack.World;

                string path = string.Empty;
                int cacheTickKey = 0;

                if (currentTrack == FaceRenderTrack.World)
                {
                    path = ResolveWorldTrackPath(compiledData, exp);
                }
                else if (skin.faceConfig.workflowMode == FaceWorkflowMode.LayeredDynamic)
                {
                    // LayeredDynamic 的 Base 现在由自定义图层节点统一承载，
                    // FaceComponent 在肖像轨不再重复接管头部底图，避免与可编辑 [Face] Base 双绘。
                    path = string.Empty;
                }
                else
                {
                    path = skin.faceConfig.GetTexPath(exp, animTick);
                    var expEntry = skin.faceConfig.GetExpression(exp);
                    cacheTickKey = (expEntry?.IsAnimated == true) ? animTick : 0;

                    if (string.IsNullOrWhiteSpace(path))
                        path = ResolveWorldTrackPath(compiledData, exp);
                }

                if (!string.IsNullOrEmpty(path))
                {
                    string directionalPath = ResolveDirectionalVariant(path, parms.facing);
                    Shader shader = node.ShaderFor(parms.pawn) ?? ShaderDatabase.Cutout;
                    Color nodeColor = node.Props?.color ?? Color.white;
                    return GetOrBuildGraphic(directionalPath, shader, nodeColor, cacheTickKey);
                }
            }

            return base.GetGraphic(node, parms);
        }

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
            => base.CanDrawNow(node, parms);

        private static string ResolveWorldTrackPath(FaceRuntimeCompiledData? compiledData, ExpressionType expression)
        {
            if (compiledData?.worldTrack?.expressionCaches != null
                && compiledData.worldTrack.expressionCaches.TryGetValue(expression, out FaceExpressionRuntimeCache? cache)
                && cache != null
                && !string.IsNullOrWhiteSpace(cache.worldPath))
            {
                return cache.worldPath;
            }

            return compiledData?.worldTrack?.defaultPath ?? string.Empty;
        }

        private static string ResolvePortraitBasePath(FaceRuntimeCompiledData? compiledData, ExpressionType expression)
        {
            if (compiledData?.portraitTrack?.expressionCaches != null
                && compiledData.portraitTrack.expressionCaches.TryGetValue(expression, out FaceExpressionRuntimeCache? cache)
                && cache != null
                && cache.TryGetPortraitPartPath(LayeredFacePartType.Base, LayeredFacePartSide.None, out string path)
                && !string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return compiledData?.portraitTrack?.basePath ?? string.Empty;
        }

        // ─────────────────────────────────────────────
        // 缓存辅助
        // ─────────────────────────────────────────────

        private static string ResolveDirectionalVariant(string basePath, Rot4 facing)
        {
            if (string.IsNullOrEmpty(basePath))
                return basePath;

            string? directionalSuffix = null;
            switch (facing.AsInt)
            {
                case 0: // North
                    directionalSuffix = "_north";
                    break;
                case 1: // East
                case 3: // West
                    directionalSuffix = "_east";
                    break;
                default: // South 使用基准图
                    return basePath;
            }

            if (string.IsNullOrEmpty(directionalSuffix))
                return basePath;

            string directionalPath = AppendDirectionalSuffix(basePath, directionalSuffix);
            return TextureExists(directionalPath) ? directionalPath : basePath;
        }

        private static string AppendDirectionalSuffix(string basePath, string suffix)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(suffix))
                return basePath;

            string extension = System.IO.Path.GetExtension(basePath);
            if (!string.IsNullOrEmpty(extension))
            {
                string withoutExtension = basePath.Substring(0, basePath.Length - extension.Length);
                return withoutExtension + suffix + extension;
            }

            return basePath + suffix;
        }

        private static bool TextureExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            if (System.IO.Path.IsPathRooted(path) || path.StartsWith("/"))
                return System.IO.File.Exists(path);

            if (!CharacterStudio.Rendering.RuntimeAssetLoader.IsMainThread())
                return true;

            if (ContentFinder<Texture2D>.Get(path, false) != null)
                return true;

            return ContentFinder<Texture2D>.Get(path + "_north", false) != null;
        }

        private static Graphic GetOrBuildGraphic(string path, Shader shader, Color color, int tickKey = 0)
        {
            // 缓存键：路径 + shader 名称 + 颜色 RGBA + tickKey（帧动画每帧独立缓存）
            string key = BuildCacheKey(path, shader, color, tickKey);
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

            if (!CharacterStudio.Rendering.RuntimeAssetLoader.IsMainThread())
                return true;

            bool isMulti = ContentFinder<Texture2D>.Get(path + "_north", false) != null;
            isMultiCache[path] = isMulti;
            return isMulti;
        }

        private static string BuildCacheKey(string path, Shader shader, Color color, int tickKey = 0)
        {
            // tickKey 非零时（帧动画）加入 tick 区分不同帧的缓存
            if (tickKey != 0)
                return $"{path}|{shader?.name ?? ""}|{color.r:F3},{color.g:F3},{color.b:F3},{color.a:F3}|t{tickKey}";
            return $"{path}|{shader?.name ?? ""}|{color.r:F3},{color.g:F3},{color.b:F3},{color.a:F3}";
        }

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