using HarmonyLib;
using CharacterStudio.Attributes;
using RimWorld;
using Verse;

namespace CharacterStudio.Patches
{
    /// <summary>
    /// 在原版 Stat 计算完成后，对挂载了 Character Studio 属性增强 Buff 的 Pawn 追加修正。
    /// 这样不会改宿主种族基础值，只是在最终值上叠加增强/减益。
    /// </summary>
    public static class Patch_CharacterAttributeBuffStat
    {
        private static readonly System.Reflection.FieldInfo? StatWorkerStatField = AccessTools.Field(typeof(StatWorker), "stat");

        public static void Apply(Harmony harmony)
        {
            if (StatWorkerStatField == null)
            {
                Log.Warning("[CharacterStudio] Patch_CharacterAttributeBuffStat: Could not find StatWorker.stat field via reflection. Attribute buffs will be silently inactive. This may indicate a RimWorld version mismatch.");
            }

            var target = AccessTools.Method(typeof(StatWorker), nameof(StatWorker.GetValue), new[] { typeof(Thing), typeof(bool), typeof(int) });
            var postfix = AccessTools.Method(typeof(Patch_CharacterAttributeBuffStat), nameof(Postfix));
            var explanationPostfix = AccessTools.Method(typeof(Patch_CharacterAttributeBuffStat), nameof(ExplanationPostfix));
            if (target != null && postfix != null)
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }

            var explanationTarget = AccessTools.Method(typeof(StatWorker), nameof(StatWorker.GetExplanationUnfinalized), new[] { typeof(StatRequest), typeof(ToStringNumberSense) });
            if (explanationTarget != null && explanationPostfix != null)
            {
                harmony.Patch(explanationTarget, postfix: new HarmonyMethod(explanationPostfix));
            }
        }

        private static void Postfix(StatWorker __instance, Thing thing, ref float __result)
        {
            if (thing is not Pawn pawn || pawn.DestroyedOrNull())
            {
                return;
            }

            // P0: 快速路径 — O(1) HashSet 检查，无 buff 的 Pawn 立即跳过，避免后续反射和迭代
            if (!CharacterAttributeBuffService.HasActiveBuffFast(pawn.thingIDNumber))
            {
                return;
            }

            StatDef? stat = StatWorkerStatField?.GetValue(__instance) as StatDef;
            if (stat == null)
            {
                return;
            }

            __result = CharacterAttributeBuffService.ApplyModifiers(pawn, stat, __result);
        }

        private static void ExplanationPostfix(StatWorker __instance, StatRequest req, ref string __result)
        {
            Pawn pawn = req.Pawn;
            if (pawn == null)
            {
                return;
            }

            StatDef? stat = StatWorkerStatField?.GetValue(__instance) as StatDef;
            if (stat == null)
            {
                return;
            }

            string extra = CharacterAttributeBuffService.BuildExplanation(pawn, stat);
            if (!string.IsNullOrWhiteSpace(extra))
            {
                __result = string.IsNullOrWhiteSpace(__result) ? extra : (__result + "\n" + extra);
            }
        }
    }
}
