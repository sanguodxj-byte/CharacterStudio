using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 眼睛方向覆盖层渲染工人（方案 A：贴图替换）
    ///
    /// 工作原理：
    ///   作为额外图层叠加在头部节点上，根据 CompPawnSkin.curEyeDirection
    ///   从 PawnEyeDirectionConfig 中取对应贴图进行覆写渲染。
    ///
    /// 空值防护：
    ///   任何一个链路节点（comp / skin / faceConfig / eyeDirectionConfig）为 null
    ///   或未启用时，均回退到不绘制（返回 null），不抛出异常。
    ///
    /// 性能优化：
    ///   贴图按 (path, shaderName, color) 三元组缓存为 Graphic，避免每帧 new。
    /// </summary>
    public class PawnRenderNodeWorker_EyeDirection : PawnRenderNodeWorker
    {
        // ─────────────────────────────────────────────
        // 静态缓存
        // ─────────────────────────────────────────────

        /// <summary>Graphic 缓存：key = "path|shaderName|colorHex"</summary>
        private static readonly Dictionary<string, Graphic> graphicCache
            = new Dictionary<string, Graphic>(StringComparer.Ordinal);

        /// <summary>_north 探测缓存：key = path, value = isMulti</summary>
        private static readonly Dictionary<string, bool> isMultiCache
            = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // P-PERF: WeakReference 缓存 CompPawnSkin，避免每帧 3x GetComp O(N) 遍历
        private static readonly System.WeakReference<Pawn> _cachedSkinPawnRef = new System.WeakReference<Pawn>(null!);
        private static CompPawnSkin? _cachedSkinComp;

        // ─────────────────────────────────────────────
        // Worker 覆写
        // ─────────────────────────────────────────────

        /// <summary>
        /// 决定是否绘制此节点。
        /// 仅当眼睛方向配置启用、当前不处于眨眼/睡眠/死亡状态、且对应方向有贴图时才绘制。
        /// 眨眼时隐藏眼方向覆盖层，避免叠在闭眼贴图上。
        /// </summary>
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms)) return false;

            var skinComp = FastGetSkinComp(parms.pawn);
            var eyeData = skinComp?.CurrentFaceRuntimeCompiledData?.portraitTrack?.eyeDirection;
            if (eyeData == null || !eyeData.enabled) return false;

            // 双轨运行时裁剪：
            // World Track 目标是保底单节点脸，因此关闭旧眼方向覆盖层。
            if (skinComp?.CurrentFaceRuntimeState.currentTrack == FaceRenderTrack.World)
                return false;

            // 眨眼 / 睡眠 / 死亡时隐藏眼方向层（闭眼状态不需要方向感）
            if (skinComp != null)
            {
                var expr = skinComp.GetEffectiveExpression();
                if (expr == ExpressionType.Blink ||
                    expr == ExpressionType.Sleeping ||
                    expr == ExpressionType.Dead)
                    return false;
            }

            var dir = GetCurrentDirection(parms.pawn);
            return !string.IsNullOrEmpty(eyeData.GetTexPath(dir));
        }

        /// <summary>
        /// 根据当前眼睛方向返回覆盖贴图 Graphic。
        /// </summary>
        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            var eyeData = GetRuntimeEyeDirectionData(parms.pawn);
            if (eyeData == null || !eyeData.enabled) return null;

            Shader shader = node.ShaderFor(parms.pawn) ?? ShaderDatabase.Cutout!;
            Color nodeColor = node.Props?.color ?? Color.white;

            var dir = GetCurrentDirection(parms.pawn);
            string path = eyeData.GetTexPath(dir);
            if (string.IsNullOrEmpty(path)) return null;

            return GetOrBuildGraphic(path, shader, nodeColor);
        }

        // ─────────────────────────────────────────────
        // 辅助：配置 / 方向获取
        // ─────────────────────────────────────────────

        // P-PERF: WeakReference 缓存，避免每帧多次 GetComp O(N) 遍历
        private static CompPawnSkin? FastGetSkinComp(Pawn? pawn)
        {
            if (pawn == null) return null;
            if (_cachedSkinPawnRef.TryGetTarget(out Pawn? cached) && cached == pawn)
                return _cachedSkinComp;
            _cachedSkinComp = pawn.GetComp<CompPawnSkin>();
            _cachedSkinPawnRef.SetTarget(pawn);
            return _cachedSkinComp;
        }

        private static FaceEyeDirectionRuntimeData? GetRuntimeEyeDirectionData(Pawn? pawn)
        {
            if (pawn == null) return null;
            var comp = FastGetSkinComp(pawn);
            return comp?.CurrentFaceRuntimeCompiledData?.portraitTrack?.eyeDirection;
        }

        private static EyeDirection GetCurrentDirection(Pawn? pawn)
        {
            if (pawn == null) return EyeDirection.Center;
            var comp = FastGetSkinComp(pawn);
            return comp?.CurEyeDirection ?? EyeDirection.Center;
        }

        // ─────────────────────────────────────────────
        // 缓存构建
        // ─────────────────────────────────────────────

        private static Graphic GetOrBuildGraphic(string path, Shader? shader, Color color)
        {
            string key = $"{path}|{shader?.name ?? ""}|{color.r:F3},{color.g:F3},{color.b:F3},{color.a:F3}";
            if (graphicCache.TryGetValue(key, out var cached))
            {
                if (CanCacheGraphic(cached))
                    return cached;

                graphicCache.Remove(key);
            }

            Graphic g = BuildGraphic(path, shader, color);
            if (CanCacheGraphic(g))
                graphicCache[key] = g;

            return g;
        }

        private static bool CanCacheGraphic(Graphic graphic)
        {
            if (graphic is Graphic_Runtime runtimeGraphic)
                return runtimeGraphic.IsInitializedSuccessfully;

            return true;
        }

        private static Graphic BuildGraphic(string path, Shader? shader, Color color)
        {
            Shader safeShader = shader ?? ShaderDatabase.Cutout;

            // 外部文件路径（绝对路径）— 与 FaceComponent Worker 保持一致，仅使用 IsPathRooted 判断
            if (System.IO.Path.IsPathRooted(path))
            {
                var req = new GraphicRequest
                {
                    graphicClass = typeof(Graphic_Single),
                    path         = path,
                    shader       = safeShader,
                    drawSize     = Vector2.one,
                    color        = color,
                    colorTwo     = Color.white,
                };
                var gr = new Graphic_Runtime();
                gr.Init(req);
                return gr;
            }

            // 游戏内路径：探测是否为 Multi（结果缓存）
            bool isMulti = GetOrDetectIsMulti(path);
            if (isMulti)
                return GraphicDatabase.Get<Graphic_Multi>(path, safeShader, Vector2.one, color);
            else
                return GraphicDatabase.Get<Graphic_Single>(path, safeShader, Vector2.one, color);
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

        /// <summary>
        /// 清除缓存（皮肤热重载时调用）
        /// </summary>
        public static void ClearCache()
        {
            graphicCache.Clear();
            isMultiCache.Clear();
        }
    }
}
