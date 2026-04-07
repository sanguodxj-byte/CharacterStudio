using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Items;
using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    public enum CharacterRuntimeTriggerConditionType
    {
        Always,
        ColonyThingDefCount,
        ColonyThingCategoryCount,
        ColonyTradeTagCount,
        ColonyFoodTypeCount,
        ColonyFoodPreferabilityCount,
        ColonistCountAtLeast,
        DaysPassedAtLeast
    }

    public sealed class CharacterRuntimeTriggerCondition
    {
        public CharacterRuntimeTriggerConditionType conditionType = CharacterRuntimeTriggerConditionType.Always;
        public string thingDefName = string.Empty;
        public string categoryDefName = string.Empty;
        public string tradeTag = string.Empty;
        public FoodTypeFlags foodType = FoodTypeFlags.None;
        public FoodPreferability foodPreferability = FoodPreferability.Undefined;
        public int minCount = 1;
        public bool countStackCount = true;
        public int minColonistCount = 1;
        public float minDaysPassed = 0f;

        public CharacterRuntimeTriggerCondition Clone()
        {
            return new CharacterRuntimeTriggerCondition
            {
                conditionType = conditionType,
                thingDefName = thingDefName ?? string.Empty,
                categoryDefName = categoryDefName ?? string.Empty,
                tradeTag = tradeTag ?? string.Empty,
                foodType = foodType,
                foodPreferability = foodPreferability,
                minCount = minCount,
                countStackCount = countStackCount,
                minColonistCount = minColonistCount,
                minDaysPassed = minDaysPassed
            };
        }

        public CharacterSpawnConditionDef? ToSpawnCondition()
        {
            CharacterSpawnConditionMatchMode? matchMode = conditionType switch
            {
                CharacterRuntimeTriggerConditionType.ColonyThingDefCount => CharacterSpawnConditionMatchMode.SpecificThingDef,
                CharacterRuntimeTriggerConditionType.ColonyThingCategoryCount => CharacterSpawnConditionMatchMode.ThingCategory,
                CharacterRuntimeTriggerConditionType.ColonyTradeTagCount => CharacterSpawnConditionMatchMode.TradeTag,
                CharacterRuntimeTriggerConditionType.ColonyFoodTypeCount => CharacterSpawnConditionMatchMode.FoodType,
                CharacterRuntimeTriggerConditionType.ColonyFoodPreferabilityCount => CharacterSpawnConditionMatchMode.FoodPreferability,
                _ => null
            };

            if (matchMode == null)
            {
                return null;
            }

            return new CharacterSpawnConditionDef
            {
                matchMode = matchMode.Value,
                thingDefName = thingDefName ?? string.Empty,
                categoryDefName = categoryDefName ?? string.Empty,
                tradeTag = tradeTag ?? string.Empty,
                foodType = foodType,
                foodPreferability = foodPreferability,
                minCount = Math.Max(1, minCount),
                countStackCount = countStackCount
            };
        }

        public string GetSummary()
        {
            return conditionType switch
            {
                CharacterRuntimeTriggerConditionType.Always => "Always",
                CharacterRuntimeTriggerConditionType.ColonyThingDefCount => $"ThingDef {thingDefName} x{Math.Max(1, minCount)}",
                CharacterRuntimeTriggerConditionType.ColonyThingCategoryCount => $"Category {categoryDefName} x{Math.Max(1, minCount)}",
                CharacterRuntimeTriggerConditionType.ColonyTradeTagCount => $"TradeTag {tradeTag} x{Math.Max(1, minCount)}",
                CharacterRuntimeTriggerConditionType.ColonyFoodTypeCount => $"FoodType {foodType} x{Math.Max(1, minCount)}",
                CharacterRuntimeTriggerConditionType.ColonyFoodPreferabilityCount => $"FoodPreferability {foodPreferability} x{Math.Max(1, minCount)}",
                CharacterRuntimeTriggerConditionType.ColonistCountAtLeast => $"Colonists >= {Math.Max(1, minColonistCount)}",
                CharacterRuntimeTriggerConditionType.DaysPassedAtLeast => $"Days >= {Math.Max(0f, minDaysPassed):0.##}",
                _ => conditionType.ToString()
            };
        }
    }

    public class CharacterSpawnProfileDef : Def
    {
        public string ownerCharacterDefName = string.Empty;
        public string skinDefName = string.Empty;
        public CharacterDefinition characterDefinition = new CharacterDefinition();
        public string raceDefName = string.Empty;
        public string pawnKindDefName = string.Empty;
        public string factionDefName = string.Empty;
        public bool forcePlayerFaction = true;

        public CharacterSpawnProfileDef Clone()
        {
            return new CharacterSpawnProfileDef
            {
                defName = defName,
                label = label,
                description = description,
                ownerCharacterDefName = ownerCharacterDefName ?? string.Empty,
                skinDefName = skinDefName ?? string.Empty,
                characterDefinition = characterDefinition?.Clone() ?? new CharacterDefinition(),
                raceDefName = raceDefName ?? string.Empty,
                pawnKindDefName = pawnKindDefName ?? string.Empty,
                factionDefName = factionDefName ?? string.Empty,
                forcePlayerFaction = forcePlayerFaction
            };
        }

        public string GetDisplayLabel()
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                return LabelCap.ToString();
            }

            string displayName = characterDefinition?.displayName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return defName ?? "CharacterSpawnProfile";
        }

        public PawnSkinDef? ResolveSkin()
        {
            if (string.IsNullOrWhiteSpace(skinDefName))
            {
                return null;
            }

            return DefDatabase<PawnSkinDef>.GetNamedSilentFail(skinDefName)
                ?? PawnSkinDefRegistry.TryGet(skinDefName);
        }

        public ThingDef ResolveRaceDef()
        {
            if (!string.IsNullOrWhiteSpace(raceDefName))
            {
                ThingDef? race = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
                if (race != null)
                {
                    return race;
                }
            }

            string characterRaceDefName = characterDefinition?.raceDefName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(characterRaceDefName))
            {
                ThingDef? race = DefDatabase<ThingDef>.GetNamedSilentFail(characterRaceDefName);
                if (race != null)
                {
                    return race;
                }
            }

            PawnSkinDef? skin = ResolveSkin();
            if (skin?.targetRaces != null)
            {
                foreach (string raceDefNameCandidate in skin.targetRaces)
                {
                    ThingDef? race = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefNameCandidate);
                    if (race != null)
                    {
                        return race;
                    }
                }
            }

            return ThingDefOf.Human;
        }

        public PawnKindDef ResolvePawnKindDef()
        {
            if (!string.IsNullOrWhiteSpace(pawnKindDefName))
            {
                PawnKindDef? pawnKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawnKindDefName);
                if (pawnKind != null)
                {
                    return pawnKind;
                }
            }

            return CharacterSpawnUtility.ResolvePawnKindForRace(ResolveRaceDef());
        }

        public Faction ResolveFaction(Map? map)
        {
            if (forcePlayerFaction)
            {
                return Faction.OfPlayer;
            }

            if (!string.IsNullOrWhiteSpace(factionDefName))
            {
                FactionDef? factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
                if (factionDef != null)
                {
                    Faction? faction = Find.FactionManager?.FirstFactionOfDef(factionDef);
                    if (faction != null)
                    {
                        return faction;
                    }
                }
            }

            return Faction.OfPlayer;
        }
    }

    public class CharacterRuntimeTriggerDef : Def
    {
        public bool enabled = true;
        public int sortOrder = 0;
        public string ownerCharacterDefName = string.Empty;
        public string spawnProfileDefName = string.Empty;
        public bool spawnNearColonist = true;
        public bool requirePlayerHomeMap = true;
        public int evaluationIntervalTicks = 250;
        public int cooldownTicks = 60000;
        public bool fireOncePerGame = false;
        public bool fireOncePerMap = false;
        public List<CharacterRuntimeTriggerCondition> requiredConditions = new List<CharacterRuntimeTriggerCondition>();
        public CharacterSpawnConditionLogic conditionLogic = CharacterSpawnConditionLogic.All;
        public CharacterSpawnSettings spawnSettings = new CharacterSpawnSettings();

        public CharacterRuntimeTriggerDef Clone()
        {
            return new CharacterRuntimeTriggerDef
            {
                defName = defName,
                label = label,
                description = description,
                enabled = enabled,
                sortOrder = sortOrder,
                ownerCharacterDefName = ownerCharacterDefName ?? string.Empty,
                spawnProfileDefName = spawnProfileDefName ?? string.Empty,
                spawnNearColonist = spawnNearColonist,
                requirePlayerHomeMap = requirePlayerHomeMap,
                evaluationIntervalTicks = evaluationIntervalTicks,
                cooldownTicks = cooldownTicks,
                fireOncePerGame = fireOncePerGame,
                fireOncePerMap = fireOncePerMap,
                requiredConditions = (requiredConditions ?? new List<CharacterRuntimeTriggerCondition>())
                    .Where(static condition => condition != null)
                    .Select(static condition => condition.Clone())
                    .ToList(),
                conditionLogic = conditionLogic,
                spawnSettings = spawnSettings?.Clone() ?? new CharacterSpawnSettings()
            };
        }

        public string GetDisplayLabel()
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                return LabelCap.ToString();
            }

            return defName ?? "RuntimeTrigger";
        }

        public string GetResolvedSignalId()
        {
            return defName ?? "CharacterStudio.RuntimeTrigger";
        }

        public int GetSafeEvaluationIntervalTicks()
        {
            return Math.Max(60, evaluationIntervalTicks);
        }

        public int GetSafeCooldownTicks()
        {
            return Math.Max(0, cooldownTicks);
        }

        public string DescribeConditions()
        {
            if (requiredConditions == null || requiredConditions.Count == 0)
            {
                return "Always";
            }

            List<string> parts = requiredConditions
                .Where(static condition => condition != null)
                .Select(static condition => condition.GetSummary())
                .Where(static summary => !string.IsNullOrWhiteSpace(summary))
                .ToList();

            if (parts.Count == 0)
            {
                return "Always";
            }

            string joiner = conditionLogic switch
            {
                CharacterSpawnConditionLogic.Any => " OR ",
                CharacterSpawnConditionLogic.Not => " OR ",
                _ => " AND "
            };

            string prefix = conditionLogic switch
            {
                CharacterSpawnConditionLogic.Any => "Any: ",
                CharacterSpawnConditionLogic.Not => "Not: ",
                _ => string.Empty
            };

            return prefix + string.Join(joiner, parts);
        }
    }
}
