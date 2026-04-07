using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CharacterStudio.Core
{
    public readonly struct CharacterRuntimeTriggerSnapshot
    {
        public string TriggerDefName { get; }
        public int MapId { get; }
        public bool ShouldEvaluate { get; }
        public bool ConditionsSatisfied { get; }
        public bool CanFire { get; }
        public int LastTriggeredTick { get; }
        public int RemainingCooldownTicks { get; }

        public CharacterRuntimeTriggerSnapshot(string triggerDefName, int mapId, bool shouldEvaluate, bool conditionsSatisfied, bool canFire, int lastTriggeredTick, int remainingCooldownTicks)
        {
            TriggerDefName = triggerDefName;
            MapId = mapId;
            ShouldEvaluate = shouldEvaluate;
            ConditionsSatisfied = conditionsSatisfied;
            CanFire = canFire;
            LastTriggeredTick = lastTriggeredTick;
            RemainingCooldownTicks = remainingCooldownTicks;
        }

        public static CharacterRuntimeTriggerSnapshot Empty(string triggerDefName, int mapId)
        {
            return new CharacterRuntimeTriggerSnapshot(triggerDefName, mapId, false, false, false, -1, 0);
        }
    }

    public sealed class CharacterRuntimeTriggerComponent : GameComponent
    {
        private Dictionary<string, int> lastTriggeredTicks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> lastTriggeredTicksByMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> lastEvaluatedTicksByMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> firedOnceGame = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> firedOnceMap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private List<string>? firedOnceGameScribe;
        private List<string>? firedOnceMapScribe;

        public CharacterRuntimeTriggerComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick <= 0 || currentTick % 60 != 0)
            {
                return;
            }

            EvaluateAllTriggers(currentTick);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref lastTriggeredTicks, "lastTriggeredTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastTriggeredTicksByMap, "lastTriggeredTicksByMap", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastEvaluatedTicksByMap, "lastEvaluatedTicksByMap", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                firedOnceGameScribe = firedOnceGame.ToList();
                firedOnceMapScribe = firedOnceMap.ToList();
            }

            Scribe_Collections.Look(ref firedOnceGameScribe, "firedOnceGame", LookMode.Value);
            Scribe_Collections.Look(ref firedOnceMapScribe, "firedOnceMap", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                firedOnceGame = new HashSet<string>(firedOnceGameScribe ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                firedOnceMap = new HashSet<string>(firedOnceMapScribe ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            }
        }

        public CharacterRuntimeTriggerSnapshot GetSnapshot(CharacterRuntimeTriggerDef trigger, Map map, int currentTick)
        {
            bool shouldEvaluate = ShouldEvaluate(trigger, map, currentTick, ignoreIntervalGate: true);
            bool conditionsSatisfied = CharacterRuntimeTriggerEvaluator.AreConditionsSatisfied(map, trigger, currentTick);
            bool canFire = CanFire(trigger, map, currentTick);
            string mapKey = BuildMapScopedKey(trigger.defName, map.uniqueID);
            int lastTriggeredTick = lastTriggeredTicksByMap.TryGetValue(mapKey, out int lastMapTriggered)
                ? lastMapTriggered
                : (lastTriggeredTicks.TryGetValue(trigger.defName, out int lastGlobalTriggered) ? lastGlobalTriggered : -1);
            int remainingCooldownTicks = 0;
            int cooldownTicks = trigger.GetSafeCooldownTicks();
            if (cooldownTicks > 0 && lastTriggeredTick >= 0)
            {
                remainingCooldownTicks = Math.Max(0, cooldownTicks - (currentTick - lastTriggeredTick));
            }

            return new CharacterRuntimeTriggerSnapshot(trigger.defName, map.uniqueID, shouldEvaluate, conditionsSatisfied, canFire, lastTriggeredTick, remainingCooldownTicks);
        }

        private void EvaluateAllTriggers(int currentTick)
        {
            Game? game = Current.Game;
            if (game == null || game.Maps == null || game.Maps.Count == 0)
            {
                return;
            }

            IEnumerable<CharacterRuntimeTriggerDef> triggers = CharacterRuntimeTriggerRegistry.AllTriggers
                .Where(static trigger => trigger != null && trigger.enabled)
                .OrderBy(static trigger => trigger.sortOrder)
                .ThenBy(static trigger => trigger.defName);

            foreach (CharacterRuntimeTriggerDef trigger in triggers)
            {
                foreach (Map map in game.Maps)
                {
                    if (!ShouldEvaluate(trigger, map, currentTick, ignoreIntervalGate: false))
                    {
                        continue;
                    }

                    MarkEvaluated(trigger, map, currentTick);
                    if (!CharacterRuntimeTriggerEvaluator.AreConditionsSatisfied(map, trigger, currentTick))
                    {
                        continue;
                    }

                    if (!CanFire(trigger, map, currentTick))
                    {
                        continue;
                    }

                    if (CharacterRuntimeTriggerExecutor.TryExecute(trigger, map))
                    {
                        MarkTriggered(trigger, map, currentTick);
                    }
                }
            }
        }

        private bool ShouldEvaluate(CharacterRuntimeTriggerDef trigger, Map map, int currentTick, bool ignoreIntervalGate)
        {
            if (trigger.requirePlayerHomeMap && !map.IsPlayerHome)
            {
                return false;
            }

            string evaluationKey = BuildMapScopedKey(trigger.defName, map.uniqueID);
            if (!ignoreIntervalGate
                && lastEvaluatedTicksByMap.TryGetValue(evaluationKey, out int lastEvaluatedTick)
                && currentTick - lastEvaluatedTick < trigger.GetSafeEvaluationIntervalTicks())
            {
                return false;
            }

            return true;
        }

        private bool CanFire(CharacterRuntimeTriggerDef trigger, Map map, int currentTick)
        {
            if (trigger.fireOncePerGame && firedOnceGame.Contains(trigger.defName))
            {
                return false;
            }

            string mapKey = BuildMapScopedKey(trigger.defName, map.uniqueID);
            if (trigger.fireOncePerMap && firedOnceMap.Contains(mapKey))
            {
                return false;
            }

            int cooldownTicks = trigger.GetSafeCooldownTicks();
            if (cooldownTicks <= 0)
            {
                return true;
            }

            if (lastTriggeredTicks.TryGetValue(trigger.defName, out int lastGlobalTriggered)
                && currentTick - lastGlobalTriggered < cooldownTicks)
            {
                return false;
            }

            if (lastTriggeredTicksByMap.TryGetValue(mapKey, out int lastMapTriggered)
                && currentTick - lastMapTriggered < cooldownTicks)
            {
                return false;
            }

            return true;
        }

        private void MarkEvaluated(CharacterRuntimeTriggerDef trigger, Map map, int currentTick)
        {
            lastEvaluatedTicksByMap[BuildMapScopedKey(trigger.defName, map.uniqueID)] = currentTick;
        }

        private void MarkTriggered(CharacterRuntimeTriggerDef trigger, Map map, int currentTick)
        {
            string mapKey = BuildMapScopedKey(trigger.defName, map.uniqueID);
            lastTriggeredTicks[trigger.defName] = currentTick;
            lastTriggeredTicksByMap[mapKey] = currentTick;

            if (trigger.fireOncePerGame)
            {
                firedOnceGame.Add(trigger.defName);
            }

            if (trigger.fireOncePerMap)
            {
                firedOnceMap.Add(mapKey);
            }
        }

        private static string BuildMapScopedKey(string triggerDefName, int mapId)
        {
            return $"{triggerDefName}|{mapId}";
        }
    }
}
