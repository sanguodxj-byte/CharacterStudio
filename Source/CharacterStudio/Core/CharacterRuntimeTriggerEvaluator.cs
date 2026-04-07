using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    public static class CharacterRuntimeTriggerEvaluator
    {
        public static bool AreConditionsSatisfied(Map? map, CharacterRuntimeTriggerDef? trigger, int currentTick)
        {
            if (trigger == null)
            {
                return false;
            }

            List<CharacterRuntimeTriggerCondition> conditions = trigger.requiredConditions ?? new List<CharacterRuntimeTriggerCondition>();
            if (conditions.Count == 0)
            {
                return true;
            }

            return trigger.conditionLogic switch
            {
                CharacterSpawnConditionLogic.Any => conditions.Any(condition => IsConditionSatisfied(map, condition, currentTick)),
                CharacterSpawnConditionLogic.Not => conditions.All(condition => !IsConditionSatisfied(map, condition, currentTick)),
                _ => conditions.All(condition => IsConditionSatisfied(map, condition, currentTick))
            };
        }

        public static bool IsConditionSatisfied(Map? map, CharacterRuntimeTriggerCondition? condition, int currentTick)
        {
            if (condition == null)
            {
                return true;
            }

            switch (condition.conditionType)
            {
                case CharacterRuntimeTriggerConditionType.Always:
                    return true;

                case CharacterRuntimeTriggerConditionType.ColonistCountAtLeast:
                    return (map?.mapPawns?.FreeColonistsSpawned?.Count ?? 0) >= Math.Max(1, condition.minColonistCount);

                case CharacterRuntimeTriggerConditionType.DaysPassedAtLeast:
                    return currentTick >= Math.Max(0f, condition.minDaysPassed) * GenDate.TicksPerDay;

                default:
                    CharacterSpawnConditionDef? spawnCondition = condition.ToSpawnCondition();
                    if (spawnCondition == null)
                    {
                        return false;
                    }

                    int count = CharacterSpawnUtility.CountMatchingThings(map, spawnCondition);
                    return count >= Math.Max(1, spawnCondition.minCount);
            }
        }
    }
}
