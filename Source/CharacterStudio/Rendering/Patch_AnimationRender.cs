using System;
using HarmonyLib;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    public static class Patch_AnimationRender
    {
        // P-PERF: 缓存 CompPawnSkin 查找结果，避免每个渲染节点重复 GetComp
        private static readonly System.WeakReference<Pawn> _lastOffsetPawnRef = new System.WeakReference<Pawn>(null!);
        private static CompPawnSkin? _lastOffsetSkinComp;
        private static readonly System.WeakReference<Pawn> _lastScalePawnRef = new System.WeakReference<Pawn>(null!);
        private static CompPawnSkin? _lastScaleSkinComp;

        /// <summary>P-PERF: 快速获取 CompPawnSkin，利用 WeakReference 避免对同一 Pawn 重复 GetComp</summary>
        private static CompPawnSkin? FastGetSkinComp_Pawn(Pawn pawn, System.WeakReference<Pawn> cacheRef, ref CompPawnSkin? cached)
        {
            if (cacheRef.TryGetTarget(out Pawn? cachedPawn) && cachedPawn == pawn)
                return cached;
            cached = pawn.GetComp<CompPawnSkin>();
            cacheRef.SetTarget(pawn);
            return cached;
        }

        private static bool IsWeaponNode(PawnRenderNode node, out bool isOffHand)
        {
            isOffHand = false;
            if (node?.Props?.tagDef == null) return false;
            string tagName = node.Props.tagDef.defName;
            if (tagName.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (tagName.IndexOf("Off",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                tagName.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
                isOffHand = true;
            return true;
        }

        public static void OffsetFor_Postfix(PawnRenderNode __0, PawnDrawParms __1, ref Vector3 __result)
        {
            try
            {
                Pawn? pawn = __1.pawn;
                if (pawn == null) return;
                CompPawnSkin? skinComp = FastGetSkinComp_Pawn(pawn, _lastOffsetPawnRef, ref _lastOffsetSkinComp);
                if (skinComp == null || skinComp.ActiveSkin?.animationConfig == null) return;

                var animCfg = skinComp.ActiveSkin.animationConfig;
                if (!animCfg.enabled) return;

                // --- 1. 鏍稿績淇锛氶槻姝㈠眰绾у彔鍔?---
                // 鎴戜滑鍙湪鈥滈楠兼牴鑺傜偣鈥濅笂搴旂敤鍔ㄧ敾锛屽叾浣欏瓙鑺傜偣锛堣。鏈嶃€侀潰閮ㄣ€佸彂鍨嬶級浼氳嚜鐒剁户鎵跨埗鑺傜偣鐨勪綅绉汇€?
                
                string tag = __0.Props?.tagDef?.defName ?? "";
                
                // 濡傛灉鏄韩浣撴牴鑺傜偣
                if (tag == "Body")
                {
                    __result += skinComp.GetAnimationDelta(AnimBone.Body);
                }
                // 濡傛灉鏄ご閮ㄦ牴鑺傜偣
                else if (tag == "Head")
                {
                    // 娉ㄦ剰锛欻ead 鍦ㄦ覆鏌撴爲閲屾槸 Body 鐨勫瓙鑺傜偣锛屾墍浠ヨ繖閲屾垜浠彧鍙犲姞 Head 鐩稿浜?Body 鐨勨€滃樊鍊尖€?
                    __result += skinComp.GetAnimationDelta(AnimBone.Head);
                }

                // --- 2. 澶勭悊姝﹀櫒涓撻」鍋忕Щ (姝﹀櫒閫氬父鏄嫭绔嬭绠楁垨鎸傚湪 Root 涓嬶紝淇濈暀鍘熼€昏緫) ---
                if (IsWeaponNode(__0, out bool isOffHand))
                {
                    if (animCfg.weaponOverrideEnabled && (!isOffHand || animCfg.applyToOffHand))
                    {
                        Vector3 wOffset = animCfg.GetWeaponOffsetForRotation(__1.facing);
                        if (__1.facing == Rot4.West) wOffset.x = -wOffset.x;
                        __result += wOffset;
                    }
                }
            }
            catch (Exception ex) { Log.Warning($"[CharacterStudio] Animation.OffsetFor 杩愯鎶ラ敊: {ex.Message}"); }
        }

        public static void ScaleFor_Postfix(PawnRenderNode __0, PawnDrawParms __1, ref Vector3 __result)
        {
            try
            {
                Pawn? pawn = __1.pawn;
                if (pawn == null) return;
                CompPawnSkin? skinComp = FastGetSkinComp_Pawn(pawn, _lastScalePawnRef, ref _lastScaleSkinComp);
                if (skinComp == null || skinComp.ActiveSkin?.animationConfig == null) return;

                var animCfg = skinComp.ActiveSkin.animationConfig;
                if (!animCfg.enabled) return;

                // 缂╂斁鍚屾牱鍙簲鐢ㄧ粰 Body 鏍硅妭鐐癸紝閬垮厤瀛愯妭鐐癸紙濡傞潰閮級璺熺潃浜屾缂╂斁瀵艰嚧褰㈠彉
                string tag = __0.Props?.tagDef?.defName ?? "";
                if (tag == "Body")
                {
                    float bScale = skinComp.GetBreathingScale(AnimBone.Body);
                    if (Math.Abs(bScale - 1.0f) > 0.0001f)
                    {
                        __result.x *= bScale;
                        __result.z *= bScale;
                    }
                }

                if (IsWeaponNode(__0, out bool isOffHand))
                {
                    if (animCfg.weaponOverrideEnabled && (!isOffHand || animCfg.applyToOffHand))
                    {
                        __result.x *= animCfg.scale.x > 0 ? animCfg.scale.x : 1f;
                        __result.z *= animCfg.scale.y > 0 ? animCfg.scale.y : 1f;
                    }
                }
            }
            catch (Exception) {}
        }

        public static void Apply(Harmony harmony)
        {
            var type = typeof(PawnRenderNodeWorker);
            harmony.Patch(AccessTools.Method(type, "OffsetFor"), postfix: new HarmonyMethod(typeof(Patch_AnimationRender), nameof(OffsetFor_Postfix)));
            harmony.Patch(AccessTools.Method(type, "ScaleFor"), postfix: new HarmonyMethod(typeof(Patch_AnimationRender), nameof(ScaleFor_Postfix)));
        }
    }
}