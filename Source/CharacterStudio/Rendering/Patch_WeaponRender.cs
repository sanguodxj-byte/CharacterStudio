using System;
using HarmonyLib;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 武器渲染补丁
    /// 拦截 PawnRenderNodeWorker 的 OffsetFor / ScaleFor，
    /// 在武器节点上叠加 PawnSkinDef.weaponRenderConfig 配置的偏移和缩放。
    ///
    /// 武器节点识别：通过 node.Props.tagDef.defName 包含 "Weapon" 来判断。
    /// </summary>
    public static class Patch_WeaponRender
    {
        // ─────────────────────────────────────────────
        // 武器节点识别
        // ─────────────────────────────────────────────

        /// <summary>
        /// 判断给定渲染节点是否为武器节点。
        /// 通过 Props.tagDef.defName 包含 "Weapon" 来识别，同时区分主手/副手。
        /// </summary>
        private static bool IsWeaponNode(PawnRenderNode node, out bool isOffHand)
        {
            isOffHand = false;
            string tagName = node?.Props?.tagDef?.defName ?? "";
            if (tagName.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            // 副手：包含 Off / Left
            if (tagName.IndexOf("Off",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                tagName.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
                isOffHand = true;

            return true;
        }

        /// <summary>
        /// 根据 Pawn 获取其激活皮肤的武器渲染配置，若未启用则返回 null。
        /// </summary>
        private static WeaponRenderConfig? GetConfig(Pawn? pawn)
        {
            if (pawn == null) return null;
            var comp = pawn.GetComp<CompPawnSkin>();
            var cfg = comp?.ActiveSkin?.weaponRenderConfig;
            return (cfg != null && cfg.enabled) ? cfg : null;
        }

        /// <summary>
        /// 将 FlightState 的抬升偏移统一接入主渲染树。
        /// 自定义图层节点会在 PawnRenderNodeWorker_CustomLayer 中单独处理，这里跳过以避免重复叠加。
        /// </summary>
        private static void ApplyFlightStateOffset(PawnRenderNode node, PawnDrawParms parms, ref Vector3 result)
        {
            if (node is PawnRenderNode_Custom)
            {
                return;
            }

            Pawn? pawn = parms.pawn;
            CompPawnSkin? skinComp = pawn?.GetComp<CompPawnSkin>();
            if (skinComp == null || !skinComp.IsFlightStateActive())
            {
                return;
            }

            float liftFactor = skinComp.GetFlightLiftFactor01();
            float flightBaseHeight = skinComp.flightStateHeightFactor * liftFactor;
            result.y += flightBaseHeight + skinComp.GetFlightHoverOffset();
        }

        // ─────────────────────────────────────────────
        // Postfix: OffsetFor
        // ─────────────────────────────────────────────

        public static void OffsetFor_Postfix(
            PawnRenderNode __0,
            PawnDrawParms __1,
            ref Vector3 pivot,
            ref Vector3 __result)
        {
            try
            {
                ApplyFlightStateOffset(__0, __1, ref __result);

                if (!IsWeaponNode(__0, out bool isOffHand)) return;
                var cfg = GetConfig(__1.pawn);
                if (cfg == null) return;
                if (isOffHand && !cfg.applyToOffHand) return;

                Rot4 facing = __1.facing;
                Vector3 extraOffset = cfg.GetOffsetForRotation(facing);
                if (facing == Rot4.West)
                    extraOffset.x = -extraOffset.x;

                __result += extraOffset;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] Patch_WeaponRender.OffsetFor_Postfix 出错: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // Postfix: ScaleFor
        // ─────────────────────────────────────────────

        public static void ScaleFor_Postfix(
            PawnRenderNode __0,
            PawnDrawParms __1,
            ref Vector3 __result)
        {
            try
            {
                if (!IsWeaponNode(__0, out bool isOffHand)) return;
                var cfg = GetConfig(__1.pawn);
                if (cfg == null) return;
                if (isOffHand && !cfg.applyToOffHand) return;

                // scale.x → X 轴，scale.y → Z 轴（RimWorld 中 Z 对应视觉高度）
                __result.x *= cfg.scale.x > 0f ? cfg.scale.x : 1f;
                __result.z *= cfg.scale.y > 0f ? cfg.scale.y : 1f;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] Patch_WeaponRender.ScaleFor_Postfix 出错: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // 注册 / 注销
        // ─────────────────────────────────────────────

        public static void Apply(Harmony harmony)
        {
            try
            {
                var offsetMethod = AccessTools.Method(typeof(PawnRenderNodeWorker), "OffsetFor");
                var scaleMethod  = AccessTools.Method(typeof(PawnRenderNodeWorker), "ScaleFor");

                var offsetPostfix = new HarmonyMethod(
                    AccessTools.Method(typeof(Patch_WeaponRender), nameof(OffsetFor_Postfix)));
                var scalePostfix = new HarmonyMethod(
                    AccessTools.Method(typeof(Patch_WeaponRender), nameof(ScaleFor_Postfix)));

                if (offsetMethod != null)
                    harmony.Patch(offsetMethod, postfix: offsetPostfix);
                if (scaleMethod != null)
                    harmony.Patch(scaleMethod, postfix: scalePostfix);

                Log.Message("[CharacterStudio] Patch_WeaponRender 武器渲染补丁已应用");
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用 Patch_WeaponRender 时出错: {ex}");
            }
        }

        public static void Unpatch(Harmony harmony)
        {
            try
            {
                var offsetMethod = AccessTools.Method(typeof(PawnRenderNodeWorker), "OffsetFor");
                var scaleMethod  = AccessTools.Method(typeof(PawnRenderNodeWorker), "ScaleFor");
                if (offsetMethod != null)
                    harmony.Unpatch(offsetMethod, HarmonyPatchType.Postfix, harmony.Id);
                if (scaleMethod != null)
                    harmony.Unpatch(scaleMethod, HarmonyPatchType.Postfix, harmony.Id);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 移除 Patch_WeaponRender 时出错: {ex.Message}");
            }
        }
    }
}