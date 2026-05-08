using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    public static class Patch_AnimationRender
    {
        // P-PERF: 单一 ConditionalWeakTable 替代两组 WeakReference，多 Pawn 零 miss
        private static readonly ConditionalWeakTable<Pawn, CompPawnSkin> _skinCompCache
            = new ConditionalWeakTable<Pawn, CompPawnSkin>();

        /// <summary>P-PERF: ConditionalWeakTable O(1) 查找，自动跟随 GC</summary>
        private static CompPawnSkin? FastGetSkinComp(Pawn pawn)
        {
            if (_skinCompCache.TryGetValue(pawn, out CompPawnSkin? cached))
                return cached;
            cached = pawn.GetComp<CompPawnSkin>();
            if (cached != null)
                _skinCompCache.Add(pawn, cached);
            return cached;
        }

        private static bool IsWeaponOffHand(PawnRenderNode node)
        {
            string tagName = node.Props.tagDef.defName;
            return tagName.IndexOf("Off", StringComparison.OrdinalIgnoreCase) >= 0
                || tagName.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void OffsetFor_Postfix(PawnRenderNode __0, PawnDrawParms __1, ref Vector3 __result)
        {
            try
            {
                // P-PERF: 先检查标签再获取 Comp，避免对大部分无关节点调用 GetComp
                string tag = __0.Props?.tagDef?.defName ?? "";
                bool isBody = tag == "Body";
                bool isHead = tag == "Head";
                bool isWeapon = !isBody && !isHead && tag.Length > 0 && tag.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isBody && !isHead && !isWeapon) return;

                Pawn? pawn = __1.pawn;
                if (pawn == null) return;

                CompPawnSkin? skinComp = FastGetSkinComp(pawn);
                if (skinComp == null) return;

                if (skinComp.ActiveSkin?.animationConfig == null) return;
                var animCfg = skinComp.ActiveSkin.animationConfig;
                if (!animCfg.enabled) return;

                if (isBody)
                {
                    __result += skinComp.GetAnimationDelta(AnimBone.Body);
                }
                else if (isHead)
                {
                    __result += skinComp.GetAnimationDelta(AnimBone.Head);
                }
                else if (isWeapon)
                {
                    if (animCfg.weaponOverrideEnabled)
                    {
                        bool isOffHand = IsWeaponOffHand(__0);
                        if (!isOffHand || animCfg.applyToOffHand)
                        {
                            Vector3 wOffset = animCfg.GetWeaponOffsetForRotation(__1.facing);
                            if (__1.facing == Rot4.West) wOffset.x = -wOffset.x;
                            __result += wOffset;
                        }
                    }
                }
            }
            catch (Exception ex) { CSLogger.Warn($"Animation.OffsetFor error: {ex.Message}", "AnimationPatch"); }
        }

        public static void ScaleFor_Postfix(PawnRenderNode __0, PawnDrawParms __1, ref Vector3 __result)
        {
            try
            {
                // P-PERF: 先检查标签再获取 Comp
                string tag = __0.Props?.tagDef?.defName ?? "";
                bool isBody = tag == "Body";
                bool isWeapon = !isBody && tag.Length > 0 && tag.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isBody && !isWeapon) return;

                Pawn? pawn = __1.pawn;
                if (pawn == null) return;

                CompPawnSkin? skinComp = FastGetSkinComp(pawn);
                if (skinComp == null) return;

                if (skinComp.ActiveSkin?.animationConfig == null) return;
                var animCfg = skinComp.ActiveSkin.animationConfig;
                if (!animCfg.enabled) return;

                if (isBody)
                {
                    float bScale = skinComp.GetBreathingScale(AnimBone.Body);
                    if (bScale != 1.0f)
                    {
                        __result.x *= bScale;
                        __result.z *= bScale;
                    }
                }
                else if (isWeapon)
                {
                    if (animCfg.weaponOverrideEnabled)
                    {
                        bool isOffHand = IsWeaponOffHand(__0);
                        if (!isOffHand || animCfg.applyToOffHand)
                        {
                            __result.x *= animCfg.scale.x > 0 ? animCfg.scale.x : 1f;
                            __result.z *= animCfg.scale.y > 0 ? animCfg.scale.y : 1f;
                        }
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
