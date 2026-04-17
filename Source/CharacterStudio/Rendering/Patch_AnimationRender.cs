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
            
            // P-PERF: 使用 IndexOf 而非正则，性能更优
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
                if (skinComp == null) return; // 快速跳过：原版角色无组件

                if (skinComp.ActiveSkin?.animationConfig == null) return;
                var animCfg = skinComp.ActiveSkin.animationConfig;
                if (!animCfg.enabled) return;

                // --- 1. 防止层级叠加 ---
                // 仅在“骨架根节点”上应用动画，子节点（衣服、面部、发型）会自然继承父节点的位移。
                string tag = __0.Props?.tagDef?.defName ?? "";
                
                if (tag == "Body")
                {
                    __result += skinComp.GetAnimationDelta(AnimBone.Body);
                }
                else if (tag == "Head")
                {
                    // Head 在渲染树里是 Body 的子节点，这里只叠加 Head 相对于 Body 的增量
                    __result += skinComp.GetAnimationDelta(AnimBone.Head);
                }

                // --- 2. 处理武器专项偏移 ---
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
            catch (Exception ex) { CSLogger.Warn($"Animation.OffsetFor error: {ex.Message}", "AnimationPatch"); }
        }

        public static void ScaleFor_Postfix(PawnRenderNode __0, PawnDrawParms __1, ref Vector3 __result)
        {
            try
            {
                Pawn? pawn = __1.pawn;
                if (pawn == null) return;
                
                CompPawnSkin? skinComp = FastGetSkinComp_Pawn(pawn, _lastScalePawnRef, ref _lastScaleSkinComp);
                if (skinComp == null) return; // 快速跳过：原版角色无组件

                if (skinComp.ActiveSkin?.animationConfig == null) return;
                var animCfg = skinComp.ActiveSkin.animationConfig;
                if (!animCfg.enabled) return;

                // 缩放同样只应用给 Body 根节点，避免子节点（如面部）跟着二次缩放导致形变
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
