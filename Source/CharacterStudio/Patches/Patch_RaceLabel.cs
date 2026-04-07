using System;
using System.Reflection;
using HarmonyLib;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Patches
{
    /// <summary>
    /// RimWorld 1.6 中角色卡顶部标签已不再稳定地直接读取 DrawCharacterCard 内的 IL。
    /// 这里改为在 CharacterCardUtility.DoTopStack 的绘制上下文内，覆写 Pawn_GeneTracker.XenotypeLabelCap，
    /// 从而稳定地将角色卡顶部显示替换为皮肤自定义 raceDisplayName。
    /// </summary>
    [HarmonyPatch]
    public static class Patch_RaceLabel
    {
        [ThreadStatic]
        private static int characterCardContextDepth;

        private static readonly FieldInfo? PawnGeneTrackerPawnField =
            AccessTools.Field(typeof(Pawn_GeneTracker), "pawn");

        private static void DoTopStack_Prefix()
        {
            characterCardContextDepth++;
        }

        private static void DoTopStack_Postfix()
        {
            if (characterCardContextDepth > 0)
            {
                characterCardContextDepth--;
            }
        }

        private static void XenotypeLabelCap_Postfix(Pawn_GeneTracker __instance, ref string __result)
        {
            if (characterCardContextDepth <= 0)
            {
                return;
            }

            Pawn? pawn = PawnGeneTrackerPawnField?.GetValue(__instance) as Pawn;
            if (pawn == null)
            {
                return;
            }

            __result = GetRaceLabel(pawn).Resolve();
        }

        // ─────────────────────────────────────────────
        // 辅助：根据皮肤返回种族显示名
        // ─────────────────────────────────────────────

        /// <summary>
        /// 若 pawn 有激活皮肤且皮肤设置了 raceDisplayName，返回该名称；
        /// 否则返回原版 pawn.def.LabelCap。
        /// 注意：Transpiler 把 pawn（Thing）留在栈上，此方法接收 Pawn 类型。
        /// </summary>
        public static TaggedString GetRaceLabel(Pawn pawn)
        {
            if (pawn == null) return string.Empty;

            var comp = pawn.TryGetComp<CompPawnSkin>();
            if (comp?.ActiveSkin != null
                && !string.IsNullOrEmpty(comp.ActiveSkin.raceDisplayName))
            {
                return comp.ActiveSkin.raceDisplayName;
            }

            return pawn.def.LabelCap;
        }

        // ─────────────────────────────────────────────
        // 注册入口
        // ─────────────────────────────────────────────

        /// <summary>由 ModEntryPoint.ApplyPatches() 调用，手动注册补丁。</summary>
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            MethodInfo? doTopStack = AccessTools.Method(
                typeof(CharacterCardUtility),
                "DoTopStack",
                new[] { typeof(Pawn), typeof(Rect), typeof(bool), typeof(float) });
            MethodInfo? xenotypeLabelGetter = AccessTools.PropertyGetter(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.XenotypeLabelCap));

            if (doTopStack == null || xenotypeLabelGetter == null)
            {
                Log.Warning("[CharacterStudio] Patch_RaceLabel: 未找到 1.6 所需目标方法，跳过注册。");
                return;
            }

            var prefix = new HarmonyLib.HarmonyMethod(
                typeof(Patch_RaceLabel),
                nameof(DoTopStack_Prefix));
            var postfix = new HarmonyLib.HarmonyMethod(
                typeof(Patch_RaceLabel),
                nameof(DoTopStack_Postfix));
            var xenotypePostfix = new HarmonyLib.HarmonyMethod(
                typeof(Patch_RaceLabel),
                nameof(XenotypeLabelCap_Postfix));

            harmony.Patch(doTopStack, prefix: prefix, postfix: postfix);
            harmony.Patch(xenotypeLabelGetter, postfix: xenotypePostfix);
            Log.Message("[CharacterStudio] Patch_RaceLabel 已注册（DoTopStack + XenotypeLabelCap）。");
        }
    }
}
