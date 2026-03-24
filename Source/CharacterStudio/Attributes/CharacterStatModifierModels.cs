using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;

namespace CharacterStudio.Attributes
{
    public enum CharacterStatModifierMode
    {
        Offset,
        Factor
    }

    public class CharacterStatModifierEntry : IExposable
    {
        public string statDefName = string.Empty;
        public CharacterStatModifierMode mode = CharacterStatModifierMode.Offset;
        public float value = 0f;
        public bool enabled = true;

        public CharacterStatModifierEntry Clone()
        {
            return new CharacterStatModifierEntry
            {
                statDefName = statDefName,
                mode = mode,
                value = value,
                enabled = enabled
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref statDefName, "statDefName", string.Empty);
            Scribe_Values.Look(ref mode, "mode", CharacterStatModifierMode.Offset);
            Scribe_Values.Look(ref value, "value", 0f);
            Scribe_Values.Look(ref enabled, "enabled", true);
        }
    }

    public class CharacterStatModifierProfile : IExposable
    {
        public List<CharacterStatModifierEntry> entries = new List<CharacterStatModifierEntry>();

        public CharacterStatModifierProfile Clone()
        {
            var clone = new CharacterStatModifierProfile();
            if (entries != null)
            {
                foreach (CharacterStatModifierEntry entry in entries)
                {
                    if (entry != null)
                    {
                        clone.entries.Add(entry.Clone());
                    }
                }
            }
            return clone;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            entries ??= new List<CharacterStatModifierEntry>();
        }

        public IEnumerable<CharacterStatModifierEntry> ActiveEntries
        {
            get
            {
                if (entries == null)
                {
                    yield break;
                }

                foreach (CharacterStatModifierEntry entry in entries)
                {
                    if (entry == null || !entry.enabled || string.IsNullOrWhiteSpace(entry.statDefName) || Math.Abs(entry.value) < 0.0001f)
                    {
                        continue;
                    }

                    yield return entry;
                }
            }
        }
    }

    public static class CharacterStatModifierCatalog
    {
        public const string AttributeBuffHediffDefName = "CS_CharacterAttributeBuff";

        public static readonly string[] CommonStatDefNames =
        {
            "MoveSpeed",
            "ShootingAccuracyPawn",
            "MeleeHitChance",
            "MeleeDodgeChance",
            "ArmorRating_Sharp",
            "ArmorRating_Blunt",
            "ArmorRating_Heat",
            "PsychicSensitivity",
            "PainShockThreshold",
            "IncomingDamageFactor",
            "WorkSpeedGlobal",
            "GlobalLearningFactor",
            "CarryingCapacity",
            "MedicalTendQuality",
            "MedicalTendSpeed",
            "ImmunityGainSpeed",
            "RestRateMultiplier",
            "RestFallRateFactor",
            "ResearchSpeed",
            "TradePriceImprovement",
            "NegotiationAbility",
            "AnimalGatherSpeed",
            "AnimalGatherYield",
            "TameAnimalChance",
            "TrainAnimalChance",
            "SocialImpact",
            "PawnBeauty",
            "DrugSellPriceImprovement",
            "AnimalProductsSellImprovement",
            "ConversionPower",
            "Fertility",
            "BedHungerRateFactor"
        };

        public static IEnumerable<StatDef> GetAvailableStatDefs()
        {
            var resolved = new HashSet<StatDef>();

            foreach (string defName in CommonStatDefNames)
            {
                StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(defName);
                if (stat != null)
                {
                    resolved.Add(stat);
                }
            }

            foreach (StatDef stat in DefDatabase<StatDef>.AllDefsListForReading)
            {
                if (stat == null || resolved.Contains(stat))
                {
                    continue;
                }

                if (IsUsefulPawnStat(stat))
                {
                    resolved.Add(stat);
                }
            }

            return resolved.OrderBy(GetDisplayLabel, StringComparer.CurrentCultureIgnoreCase);
        }

        public static bool IsUsefulPawnStat(StatDef stat)
        {
            if (stat == null)
            {
                return false;
            }

            string categoryName = stat.category?.defName ?? string.Empty;
            if (categoryName.StartsWith("Pawn", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (string defName in CommonStatDefNames)
            {
                if (string.Equals(defName, stat.defName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string GetDisplayLabel(StatDef stat)
        {
            if (stat == null)
            {
                return "CS_Studio_None".Translate();
            }

            if (!string.IsNullOrWhiteSpace(stat.label))
            {
                return stat.label.CapitalizeFirst();
            }

            return stat.defName ?? "CS_Studio_None".Translate();
        }

        public static string GetCategoryLabel(StatDef stat)
        {
            string categoryName = stat?.category?.defName ?? string.Empty;
            if (categoryName == "PawnCombat" || categoryName == "PawnResistances")
                return "CS_AttrBuff_Category_Combat".Translate().ToString();
            if (categoryName == "PawnHealth" || categoryName == "PawnFood")
                return "CS_AttrBuff_Category_Survival".Translate().ToString();
            if (categoryName == "PawnPsyfocus")
                return "CS_AttrBuff_Category_Psychic".Translate().ToString();
            if (categoryName == "PawnWork" || categoryName == "BasicsPawn")
                return "CS_AttrBuff_Category_Work".Translate().ToString();
            if (categoryName == "PawnSocial" || categoryName == "PawnMisc")
                return "CS_AttrBuff_Category_Social".Translate().ToString();
            if (categoryName == "Animals" || categoryName == "AnimalProductivity")
                return "CS_AttrBuff_Category_Animal".Translate().ToString();

            return "CS_AttrBuff_Category_Other".Translate().ToString();
        }

        public static string GetMenuLabel(StatDef stat)
        {
            return $"[{GetCategoryLabel(stat)}] {GetDisplayLabel(stat)}";
        }

        public static string FormatValuePreview(CharacterStatModifierMode mode, float value)
        {
            if (mode == CharacterStatModifierMode.Offset)
            {
                return value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);
            }

            return (value * 100f).ToString("+0.##%;-0.##%;0%", CultureInfo.InvariantCulture);
        }

        public static string GetModeLabel(CharacterStatModifierMode mode)
        {
            return mode == CharacterStatModifierMode.Offset
                ? "CS_AttrBuff_Mode_Offset".Translate()
                : "CS_AttrBuff_Mode_Factor".Translate();
        }
    }
}
