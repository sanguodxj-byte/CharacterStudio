using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 通用武器携带视觉层：按 Pawn 状态在“非征召 / 征召 / 施法中”三种贴图之间切换。
    /// 不硬编码具体武器类型，主要用于技能导向角色的状态展示。
    /// </summary>
    public class PawnRenderNodeWorker_WeaponCarryVisual : PawnRenderNodeWorker
    {
        private static readonly Dictionary<string, Graphic> graphicCache = new Dictionary<string, Graphic>(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool> isMultiCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // P-PERF: WeakReference 缓存 CompCharacterAbilityRuntime，避免每帧 GetComp O(N) 遍历
        private static readonly System.WeakReference<Pawn> _cachedAbilityPawnRef = new System.WeakReference<Pawn>(null!);
        private static CompCharacterAbilityRuntime? _cachedAbilityComp;

        private static CompCharacterAbilityRuntime? FastGetAbilityComp(Pawn? pawn)
        {
            if (pawn == null) return null;
            if (_cachedAbilityPawnRef.TryGetTarget(out Pawn? cached) && cached == pawn)
                return _cachedAbilityComp;
            _cachedAbilityComp = pawn.GetComp<CompCharacterAbilityRuntime>();
            _cachedAbilityPawnRef.SetTarget(pawn);
            return _cachedAbilityComp;
        }

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
                return false;

            if (node is not PawnRenderNode_Custom customNode || customNode.config == null)
                return false;

            WeaponCarryVisualConfig? carryVisual = customNode.config.weaponCarryVisual;
            if (carryVisual == null || !carryVisual.enabled)
                return false;

            string path = carryVisual.GetTexPath(GetCurrentState(parms.pawn));
            return !string.IsNullOrWhiteSpace(path);
        }

        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is not PawnRenderNode_Custom customNode || customNode.config == null)
                return null;

            WeaponCarryVisualConfig? carryVisual = customNode.config.weaponCarryVisual;
            if (carryVisual == null || !carryVisual.enabled)
                return null;

            string path = carryVisual.GetTexPath(GetCurrentState(parms.pawn));
            if (string.IsNullOrWhiteSpace(path))
                return null;

            Shader shader = node.ShaderFor(parms.pawn) ?? ShaderDatabase.Cutout;
            Color color = node.Props?.color ?? Color.white;
            return GetOrBuildGraphic(path, shader, color);
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 baseOffset = base.OffsetFor(node, parms, out pivot);

            if (node is not PawnRenderNode_Custom customNode || customNode.config?.weaponCarryVisual == null)
                return baseOffset;

            Vector3 extra = customNode.config.weaponCarryVisual.GetOffsetForRotation(parms.facing);
            if (parms.facing == Rot4.West)
                extra.x = -extra.x;

            return baseOffset + extra;
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 baseScale = base.ScaleFor(node, parms);

            if (node is not PawnRenderNode_Custom customNode || customNode.config?.weaponCarryVisual == null)
                return baseScale;

            Vector2 cfg = customNode.config.weaponCarryVisual.scale;
            float sx = cfg.x <= 0f ? 1f : cfg.x;
            float sy = cfg.y <= 0f ? 1f : cfg.y;
            return new Vector3(baseScale.x * sx, baseScale.y, baseScale.z * sy);
        }

        private static WeaponCarryVisualState GetCurrentState(Pawn? pawn)
        {
            if (pawn == null)
                return WeaponCarryVisualState.Undrafted;

            var abilityComp = FastGetAbilityComp(pawn);
            if (abilityComp != null && abilityComp.IsWeaponCarryCastingNow())
                return WeaponCarryVisualState.Casting;

            if (IsCastingAbility(pawn))
                return WeaponCarryVisualState.Casting;

            return pawn.Drafted ? WeaponCarryVisualState.Drafted : WeaponCarryVisualState.Undrafted;
        }

        private static bool IsCastingAbility(Pawn pawn)
        {
            try
            {
                if (pawn.stances?.curStance is not Stance_Busy busy || busy.verb == null)
                    return false;

                string verbTypeName = busy.verb.GetType().Name;
                if (verbTypeName.IndexOf("CastAbility", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                string verbClassName = busy.verb.verbProps?.verbClass?.Name ?? string.Empty;
                return verbClassName.IndexOf("CastAbility", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static Graphic GetOrBuildGraphic(string path, Shader shader, Color color)
        {
            string key = $"{path}|{shader?.name ?? string.Empty}|{color.r:F3},{color.g:F3},{color.b:F3},{color.a:F3}";
            if (graphicCache.TryGetValue(key, out var cached))
            {
                if (CanCacheGraphic(cached))
                    return cached;

                graphicCache.Remove(key);
            }

            Graphic g = BuildGraphic(path, shader ?? ShaderDatabase.Cutout, color);
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

        private static Graphic BuildGraphic(string path, Shader shader, Color color)
        {
            if (System.IO.Path.IsPathRooted(path) || path.StartsWith("/"))
            {
                var req = new GraphicRequest(
                    typeof(Graphic_Runtime), path, shader,
                    Vector2.one, color, Color.white,
                    null, 0, null, null);
                var gr = new Graphic_Runtime();
                gr.Init(req);
                return gr;
            }

            bool isMulti = GetOrDetectIsMulti(path);
            return isMulti
                ? GraphicDatabase.Get<Graphic_Multi>(path, shader, Vector2.one, color)
                : GraphicDatabase.Get<Graphic_Single>(path, shader, Vector2.one, color);
        }

        private static bool GetOrDetectIsMulti(string path)
        {
            if (isMultiCache.TryGetValue(path, out bool cached))
                return cached;

            bool isMulti = ContentFinder<Texture2D>.Get(path + "_north", false) != null;
            isMultiCache[path] = isMulti;
            return isMulti;
        }
    }
}