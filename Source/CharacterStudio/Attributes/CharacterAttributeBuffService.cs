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

            float result = value;
            foreach (CharacterStatModifierEntry entry in GetActiveEntries(pawn))
            {
                if (!string.Equals(entry.statDefName, stat.defName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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
            foreach (CharacterStatModifierEntry _ in GetActiveEntries(pawn))
            {
                return true;
            }

            return false;
        }
    }
}
