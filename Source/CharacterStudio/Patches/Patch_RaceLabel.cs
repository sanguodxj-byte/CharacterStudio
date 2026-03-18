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
    /// Harmony Transpiler：拦截 CharacterCardUtility.DrawCharacterCard 中对 pawn.def.LabelCap 的调用，
    /// 若当前 pawn 装备了含 raceDisplayName 的皮肤，则替换为皮肤自定义名称。
    /// 这样角色卡上的种族行会显示皮肤定义的标签，而不是原版「智人」。
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
            // 我们要替换的 IL 序列：
            //   ldarg / ldloc  (把 pawn 推上栈)
            //   ldfld / callvirt  ThingDef Pawn::def
            //   callvirt  string Def::get_LabelCap()
            // 替换成调用我们的静态辅助方法 GetRaceLabel(Pawn)

            var defField      = typeof(Thing).GetField("def");
            var labelCapGetter = typeof(Def).GetProperty("LabelCap")?.GetGetMethod();
            var helperMethod  = typeof(Patch_RaceLabel).GetMethod(
                nameof(GetRaceLabel), BindingFlags.Public | BindingFlags.Static);

            if (defField == null || labelCapGetter == null || helperMethod == null)
            {
                Log.Warning("[CharacterStudio] Patch_RaceLabel: 无法定位目标成员，Transpiler 跳过。");
                foreach (var inst in instructions) yield return inst;
                yield break;
            }

            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            for (int i = 0; i < codes.Count - 1; i++)
            {
                var cur  = codes[i];
                var next = codes[i + 1];

                // 识别模式：ldfld ThingDef Thing::def  +  callvirt get_LabelCap
                bool isDefLoad = cur.opcode == OpCodes.Ldfld && cur.operand is FieldInfo fi && fi == defField;
                bool isLabelCap = next.opcode == OpCodes.Callvirt && next.operand is MethodInfo mi && mi == labelCapGetter;

                if (isDefLoad && isLabelCap)
                {
                    // 此时栈顶是 pawn（Thing），cur 会把 def push 上去。
                    // 我们把这两条指令替换为：
                    //   call GetRaceLabel(Pawn)   ← 栈顶已有 pawn，直接消费
                    // 注意：cur (ldfld) 消耗 pawn 并 push def，我们要保留 pawn 在栈上
                    // 所以跳过 cur（ldfld），把 next（callvirt LabelCap）换成 call GetRaceLabel

                    // 保留 cur 的 label（可能有跳转目标），但改变它为 nop
                    cur.opcode  = OpCodes.Nop;
                    cur.operand = null;

                    next.opcode  = OpCodes.Call;
                    next.operand = helperMethod;

                    patched = true;
                    // 跳过 next，i 正常递增即可
                }

                yield return cur;
            }

            // 输出最后一条
            if (codes.Count > 0)
                yield return codes[codes.Count - 1];

            if (!patched)
                Log.Warning("[CharacterStudio] Patch_RaceLabel: 未能找到 ldfld::def + callvirt::LabelCap 指令对，种族名替换未生效。");
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
