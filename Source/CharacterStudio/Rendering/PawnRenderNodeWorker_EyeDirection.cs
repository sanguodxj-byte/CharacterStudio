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

            var skinComp = parms.pawn?.TryGetComp<CompPawnSkin>();
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

            // UV 偏移模式：仅需中心贴图存在即可绘制（方向由 UV 控制）
            if (eyeData.useUvOffset)
                return !string.IsNullOrEmpty(eyeData.texCenter);

            var dir = GetCurrentDirection(parms.pawn);
            return !string.IsNullOrEmpty(eyeData.GetTexPath(dir));
        }

        /// <summary>
        /// 根据当前眼睛方向返回覆盖贴图 Graphic。
        /// UV 模式下始终使用 texCenter 贴图，方向感由 GetMaterialPropertyBlock 中的 UV 偏移实现。
        /// </summary>
        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            var eyeData = GetRuntimeEyeDirectionData(parms.pawn);
            if (eyeData == null || !eyeData.enabled) return null;

            Shader shader = node.ShaderFor(parms.pawn) ?? ShaderDatabase.Cutout!;
            Color nodeColor = node.Props?.color ?? Color.white;

            // UV 偏移模式：始终用 Center 贴图，偏移由 MaterialPropertyBlock 驱动
            if (eyeData.useUvOffset)
            {
                if (string.IsNullOrEmpty(eyeData.texCenter)) return null;
                return GetOrBuildGraphic(eyeData.texCenter, shader, nodeColor);
            }

            var dir = GetCurrentDirection(parms.pawn);
            string path = eyeData.GetTexPath(dir);
            if (string.IsNullOrEmpty(path)) return null;

            return GetOrBuildGraphic(path, shader, nodeColor);
        }

        /// <summary>
        /// UV 偏移模式下，通过 _MainTex_ST 的偏移分量（.zw）驱动瞳孔偏移。
        /// _MainTex_ST = (scaleX, scaleY, offsetX, offsetY)
        ///
        /// EyeDirection → UV offset 映射（Unity UV 坐标 x 向右、y 向上）：
        ///   Left  → offset.x = +range（贴图右移 → 采样到瞳孔左侧区域）
        ///   Right → offset.x = -range
        ///   Up    → offset.y = -range（贴图下移 → 采样到瞳孔上侧区域）
        ///   Down  → offset.y = +range
        ///
        /// 注意：仅对 pupilMoveRange > 0 时生效，否则走贴图替换路径（无需额外 MPB 操作）。
        /// </summary>
        public override MaterialPropertyBlock GetMaterialPropertyBlock(
            PawnRenderNode node, Material material, PawnDrawParms parms)
        {
            var mpb = base.GetMaterialPropertyBlock(node, material, parms);

            var eyeData = GetRuntimeEyeDirectionData(parms.pawn);
            CompPawnSkin? skinComp = parms.pawn?.GetComp<CompPawnSkin>();
            if (eyeData == null || !eyeData.enabled || !eyeData.useUvOffset || eyeData.uvMoveRange <= 0f)
                return mpb;

            float brightnessOffset = skinComp?.GetAbilityPupilBrightnessOffset() ?? 0f;
            float contrastOffset = skinComp?.GetAbilityPupilContrastOffset() ?? 0f;
            float r = eyeData.uvMoveRange;
            var dir = GetCurrentDirection(parms.pawn);

            float offsetX = 0f, offsetY = 0f;
            switch (dir)
            {
                case EyeDirection.Left:  offsetX = +r; break;
                case EyeDirection.Right: offsetX = -r; break;
                case EyeDirection.Up:    offsetY = -r; break;
                case EyeDirection.Down:  offsetY = +r; break;
                // Center: 不偏移
            }

            if (parms.facing == Rot4.West)
            {
                offsetX = -offsetX;
            }

            // _MainTex_ST.xy = scale（保持 (1,1)），_MainTex_ST.zw = offset
            int stID = Shader.PropertyToID("_MainTex_ST");
            mpb.SetVector(stID, new Vector4(1f, 1f, offsetX, offsetY));

            float tintScalar = Mathf.Clamp(1f + brightnessOffset + (contrastOffset * 0.5f), 0.2f, 3f);
            int colorID = Shader.PropertyToID("_Color");
            mpb.SetColor(colorID, new Color(tintScalar, tintScalar, tintScalar, 1f));
            mpb.SetFloat(Shader.PropertyToID("_CS_PupilBrightnessOffset"), brightnessOffset);
            mpb.SetFloat(Shader.PropertyToID("_CS_PupilContrastOffset"), contrastOffset);

            return mpb;
        }

        // ─────────────────────────────────────────────
        // 辅助：配置 / 方向获取
        // ─────────────────────────────────────────────

        private static FaceEyeDirectionRuntimeData? GetRuntimeEyeDirectionData(Pawn? pawn)
        {
            if (pawn == null) return null;
            var comp = pawn.GetComp<CompPawnSkin>();
            return comp?.CurrentFaceRuntimeCompiledData?.portraitTrack?.eyeDirection;
        }

        private static EyeDirection GetCurrentDirection(Pawn? pawn)
        {
            if (pawn == null) return EyeDirection.Center;
            var comp = pawn.GetComp<CompPawnSkin>();
            return comp?.CurEyeDirection ?? EyeDirection.Center;
        }

        // ─────────────────────────────────────────────
        // 缓存构建
        // ─────────────────────────────────────────────

        private static Graphic GetOrBuildGraphic(string path, Shader? shader, Color color)
        {
            string key = $"{path}|{shader?.name ?? ""}|{color.r:F3},{color.g:F3},{color.b:F3},{color.a:F3}";
            if (graphicCache.TryGetValue(key, out var cached))
                return cached;

            Graphic g = BuildGraphic(path, shader, color);
            graphicCache[key] = g;
            return g;
        }

        private static Graphic BuildGraphic(string path, Shader? shader, Color color)
        {
            Shader safeShader = shader ?? ShaderDatabase.Cutout;

            // 外部文件路径（绝对路径，含扩展名）
            if (System.IO.Path.IsPathRooted(path) || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
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