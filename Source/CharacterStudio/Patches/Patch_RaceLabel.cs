using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Patches
{
    /// <summary>
    /// Harmony Transpiler：兼容不同版本的 CharacterCardUtility.DrawCharacterCard。
    /// 旧版本可能直接读取 pawn.def.LabelCap，1.6/Biotech 场景则通常显示 pawn.genes.XenotypeLabelCap。
    /// 若当前 pawn 装备了含 raceDisplayName 的皮肤，则统一替换为皮肤自定义名称。
    /// </summary>
    [HarmonyPatch]
    public static class Patch_RaceLabel
    {
        // ─────────────────────────────────────────────
        // 目标方法定位
        // ─────────────────────────────────────────────

        static MethodBase TargetMethod()
        {
            // CharacterCardUtility.DrawCharacterCard 存在多个重载，取参数最多的那个
            // RimWorld 1.6 签名：DrawCharacterCard(Rect, Pawn, Action, Rect, bool)
            var methods = typeof(CharacterCardUtility).GetMethods(
                BindingFlags.Public | BindingFlags.Static);

            MethodBase? best = null;
            int bestParamCount = -1;
            foreach (var m in methods)
            {
                if (m.Name != "DrawCharacterCard") continue;
                int cnt = m.GetParameters().Length;
                if (cnt > bestParamCount)
                {
                    bestParamCount = cnt;
                    best = m;
                }
            }
            return best!;
        }

        // ─────────────────────────────────────────────
        // Transpiler
        // ─────────────────────────────────────────────

        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            // 兼容两种 IL：
            // 1) pawn.def.LabelCap
            // 2) pawn.genes.XenotypeLabelCap（RimWorld 1.6 角色卡常见路径）
            // 两种情况都替换为调用我们的静态辅助方法 GetRaceLabel(Pawn)。

            var defField = typeof(Thing).GetField("def");
            var labelCapGetter = typeof(Def).GetProperty("LabelCap")?.GetGetMethod();
            var genesGetter = typeof(Pawn).GetProperty("genes")?.GetGetMethod();
            var xenotypeLabelGetter = typeof(Pawn_GeneTracker).GetProperty("XenotypeLabelCap")?.GetGetMethod();
            var helperMethod = typeof(Patch_RaceLabel).GetMethod(
                nameof(GetRaceLabel), BindingFlags.Public | BindingFlags.Static);

            if (helperMethod == null)
            {
                Log.Warning("[CharacterStudio] Patch_RaceLabel: 无法定位辅助方法，Transpiler 跳过。");
                foreach (var inst in instructions) yield return inst;
                yield break;
            }

            var codes = new List<CodeInstruction>(instructions);
            bool patchedLegacyDefLabel = false;
            bool patchedXenotypeLabel = false;

            for (int i = 0; i < codes.Count - 1; i++)
            {
                var cur = codes[i];
                var next = codes[i + 1];

                bool isDefLoad = defField != null
                    && cur.opcode == OpCodes.Ldfld
                    && cur.operand is FieldInfo defFieldInfo
                    && defFieldInfo == defField;
                bool isLabelCapCall = labelCapGetter != null
                    && (next.opcode == OpCodes.Call || next.opcode == OpCodes.Callvirt)
                    && next.operand is MethodInfo labelMethod
                    && labelMethod == labelCapGetter;

                bool isGenesGetterCall = genesGetter != null
                    && (cur.opcode == OpCodes.Call || cur.opcode == OpCodes.Callvirt)
                    && cur.operand is MethodInfo genesMethod
                    && genesMethod == genesGetter;
                bool isXenotypeLabelCall = xenotypeLabelGetter != null
                    && (next.opcode == OpCodes.Call || next.opcode == OpCodes.Callvirt)
                    && next.operand is MethodInfo xenotypeMethod
                    && xenotypeMethod == xenotypeLabelGetter;

                if (isDefLoad && isLabelCapCall)
                {
                    cur.opcode = OpCodes.Nop;
                    cur.operand = null;

                    next.opcode = OpCodes.Call;
                    next.operand = helperMethod;

                    patchedLegacyDefLabel = true;
                }
                else if (isGenesGetterCall && isXenotypeLabelCall)
                {
                    cur.opcode = OpCodes.Nop;
                    cur.operand = null;

                    next.opcode = OpCodes.Call;
                    next.operand = helperMethod;

                    patchedXenotypeLabel = true;
                }

                yield return cur;
            }

            if (codes.Count > 0)
                yield return codes[codes.Count - 1];

            if (!patchedLegacyDefLabel && !patchedXenotypeLabel)
            {
                Log.Warning("[CharacterStudio] Patch_RaceLabel: 未能找到 def.LabelCap 或 genes.XenotypeLabelCap 指令对，种族名替换未生效。");
            }
            else
            {
                Log.Message($"[CharacterStudio] Patch_RaceLabel: legacyDefLabel={patchedLegacyDefLabel}, xenotypeLabel={patchedXenotypeLabel}");
            }
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

        /// <summary>由 ModEntryPoint.ApplyPatches() 调用，手动注册此 Transpiler。</summary>
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            var target = TargetMethod();
            if (target == null)
            {
                Log.Warning("[CharacterStudio] Patch_RaceLabel: 未找到目标方法 DrawCharacterCard，跳过注册。");
                return;
            }

            var transpiler = new HarmonyLib.HarmonyMethod(
                typeof(Patch_RaceLabel),
                nameof(Transpiler));
            harmony.Patch(target, transpiler: transpiler);
            Log.Message("[CharacterStudio] Patch_RaceLabel 已注册（Transpiler）。");
        }
    }
}
