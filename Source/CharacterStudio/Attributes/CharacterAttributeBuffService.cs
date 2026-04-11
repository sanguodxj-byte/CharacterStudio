using System;
using System.Collections.Generic;
using System.Globalization;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Attributes
{
    public static class CharacterAttributeBuffService
    {
        // P0: 追踪有活跃属性 Buff 的 Pawn ID 集合，用于 StatWorker Postfix 快速跳过
        private static readonly HashSet<int> pawnIdsWithActiveBuffs = new HashSet<int>();

        public static CharacterStatModifierProfile GetOrCreateProfile(PawnSkinDef skin)
        {
            if (skin == null)
            {
                throw new ArgumentNullException(nameof(skin));
            }

            skin.statModifiers ??= new CharacterStatModifierProfile();
            skin.statModifiers.entries ??= new List<CharacterStatModifierEntry>();
            return skin.statModifiers;
        }

        public static IEnumerable<CharacterStatModifierEntry> GetActiveEntries(Pawn pawn)
        {
            var comp = pawn?.GetComp<CompPawnSkin>();
            var profile = comp?.ActiveSkin?.statModifiers;
            if (profile == null)
            {
                yield break;
            }

            foreach (CharacterStatModifierEntry entry in profile.ActiveEntries)
            {
                yield return entry;
            }
        }

        /// <summary>
        /// P0: 快速检查 Pawn 是否有活跃的属性 Buff（O(1) HashSet 查找）
        /// 用于 StatWorker Postfix 早期退出，避免对无 buff Pawn 执行反射和迭代
        /// </summary>
        public static bool HasActiveBuffFast(int pawnId)
        {
            return pawnIdsWithActiveBuffs.Contains(pawnId);
        }

        /// <summary>清除 buff 追踪状态（游戏结束时调用）</summary>
        public static void ClearBuffTracker()
        {
            pawnIdsWithActiveBuffs.Clear();
        }

        public static void SyncAttributeBuff(Pawn? pawn)
        {
            if (pawn == null || pawn.health?.hediffSet == null)
            {
                return;
            }

            HediffDef buffDef = DefDatabase<HediffDef>.GetNamedSilentFail(CharacterStatModifierCatalog.AttributeBuffHediffDefName);
            if (buffDef == null)
            {
                return;
            }

            bool shouldHaveBuff = HasAnyActiveEntry(pawn);

            // P0: 同步追踪集合
            if (shouldHaveBuff)
                pawnIdsWithActiveBuffs.Add(pawn.thingIDNumber);
            else
                pawnIdsWithActiveBuffs.Remove(pawn.thingIDNumber);

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(buffDef);

            if (shouldHaveBuff)
            {
                if (existing == null)
                {
                    Hediff created = HediffMaker.MakeHediff(buffDef, pawn);
                    created.Severity = 1f;
                    pawn.health.AddHediff(created);
                }
                else
                {
                    existing.Severity = 1f;
                }
            }
            else if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
        }

        public static float ApplyModifiers(Pawn pawn, StatDef stat, float value)
        {
            if (pawn == null || stat == null)
            {
                return value;
            }

            // P0: 快速路径 — 大多数 Pawn 没有活跃 buff，直接返回
            if (!pawnIdsWithActiveBuffs.Contains(pawn.thingIDNumber))
            {
                return value;
            }

            // 直接访问 profile.entries 避免 yield return 迭代器分配
            var comp = pawn.GetComp<CompPawnSkin>();
            var profile = comp?.ActiveSkin?.statModifiers;
            if (profile?.entries == null || profile.entries.Count == 0)
            {
                return value;
            }

            float result = value;
            string statDefName = stat.defName;
            for (int i = 0; i < profile.entries.Count; i++)
            {
                CharacterStatModifierEntry? entry = profile.entries[i];
                if (entry == null || !entry.enabled || Math.Abs(entry.value) < 0.0001f)
                    continue;

                if (!string.Equals(entry.statDefName, statDefName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.mode == CharacterStatModifierMode.Offset)
                {
                    result += entry.value;
                }
                else
                {
                    result *= Math.Max(0f, 1f + entry.value);
                }
            }

            return result;
        }

        public static string BuildExplanation(Pawn pawn, StatDef stat)
        {
            if (pawn == null || stat == null)
            {
                return string.Empty;
            }

            List<string>? lines = null;
            foreach (CharacterStatModifierEntry entry in GetActiveEntries(pawn))
            {
                if (!string.Equals(entry.statDefName, stat.defName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lines ??= new List<string>();
                string valueText;
                if (entry.mode == CharacterStatModifierMode.Offset)
                {
                    valueText = entry.value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);
                }
                else
                {
                    valueText = (entry.value * 100f).ToString("+0.##%;-0.##%;0%", CultureInfo.InvariantCulture);
                }

                lines.Add($"  - {CharacterStatModifierCatalog.GetModeLabel(entry.mode)}: {valueText}");
            }

            if (lines == null || lines.Count == 0)
            {
                return string.Empty;
            }

            return "CS_AttrBuff_ExplanationHeader".Translate().ToString() + "\n" + string.Join("\n", lines);
        }

        private static bool HasAnyActiveEntry(Pawn pawn)
        {
            var comp = pawn?.GetComp<CompPawnSkin>();
            var profile = comp?.ActiveSkin?.statModifiers;
            if (profile?.entries == null)
            {
                return false;
            }

            // P0: 替换 LINQ .Any() 为手动遍历，避免闭包分配
            for (int i = 0; i < profile.entries.Count; i++)
            {
                var e = profile.entries[i];
                if (e != null && e.enabled && !string.IsNullOrWhiteSpace(e.statDefName) && Math.Abs(e.value) >= 0.0001f)
                    return true;
            }
            return false;
        }
    }
}
