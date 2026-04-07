using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Items;
using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    public sealed class CharacterSpawnSettings
    {
        public Map? sourceMapForConditionCheck;
        public string arrivalDefName = string.Empty;
        public SummonArrivalMode arrivalMode = SummonArrivalMode.Standing;
        public string spawnEventDefName = string.Empty;
        public SummonSpawnEventMode spawnEvent = SummonSpawnEventMode.Message;
        public string eventMessageText = string.Empty;
        public string eventLetterTitle = string.Empty;
        public string spawnAnimationDefName = string.Empty;
        public SummonSpawnAnimationMode spawnAnimation = SummonSpawnAnimationMode.None;
        public float spawnAnimationScale = 1f;

        public CharacterSpawnSettings Clone()
        {
            return new CharacterSpawnSettings
            {
                sourceMapForConditionCheck = sourceMapForConditionCheck,
                arrivalDefName = arrivalDefName,
                arrivalMode = arrivalMode,
                spawnEventDefName = spawnEventDefName,
                spawnEvent = spawnEvent,
                eventMessageText = eventMessageText,
                eventLetterTitle = eventLetterTitle,
                spawnAnimationDefName = spawnAnimationDefName,
                spawnAnimation = spawnAnimation,
                spawnAnimationScale = spawnAnimationScale
            };
        }
    }

    public static class CharacterSpawnUtility
    {
        public static bool AreConditionsSatisfied(Map? map, IEnumerable<CharacterSpawnConditionDef>? conditions)
        {
            if (conditions == null)
                return true;

            foreach (CharacterSpawnConditionDef condition in conditions)
            {
                if (condition == null)
                    continue;

                if (!IsConditionSatisfied(map, condition))
                    return false;
            }

            return true;
        }

        public static bool AreConditionsSatisfied(Map? map, CharacterSpawnOptionDef? optionDef)
        {
            if (optionDef == null)
                return false;

            List<CharacterSpawnConditionDef> conditions = optionDef.requiredConditions ?? new List<CharacterSpawnConditionDef>();
            if (conditions.Count == 0)
                return true;

            switch (optionDef.conditionLogic)
            {
                case CharacterSpawnConditionLogic.Any:
                    return conditions.Any(condition => IsConditionSatisfied(map, condition));
                case CharacterSpawnConditionLogic.Not:
                    return conditions.All(condition => !IsConditionSatisfied(map, condition));
                default:
                    return conditions.All(condition => IsConditionSatisfied(map, condition));
            }
        }

        public static bool IsConditionSatisfied(Map? map, CharacterSpawnConditionDef? condition)
        {
            if (condition == null)
                return true;
            if (map == null)
                return false;

            int count = CountMatchingThings(map, condition);
            return count >= Math.Max(1, condition.minCount);
        }

        public static int CountMatchingThings(Map? map, CharacterSpawnConditionDef? condition)
        {
            if (map == null || condition == null)
                return 0;

            IEnumerable<Thing> allThings = map.listerThings?.AllThings ?? Enumerable.Empty<Thing>();
            int total = 0;

            foreach (Thing thing in allThings)
            {
                if (thing == null || thing.Destroyed)
                    continue;
                if (!IsColonyOwnedInventoryThing(thing, map))
                    continue;

                if (!MatchesConditionThing(thing, condition))
                    continue;

                total += condition.countStackCount ? Math.Max(1, thing.stackCount) : 1;
            }

            return total;
        }

        private static bool IsColonyOwnedInventoryThing(Thing thing, Map map)
        {
            if (!thing.Spawned || thing.Map != map)
                return false;
            if (thing.IsForbidden(Faction.OfPlayer))
                return false;

            Faction? faction = thing.Faction;
            if (faction != null && faction != Faction.OfPlayer)
                return false;

            if (thing.ParentHolder is Pawn_InventoryTracker inventoryTracker)
            {
                return inventoryTracker.pawn != null
                    && inventoryTracker.pawn.Faction == Faction.OfPlayer;
            }

            if (thing.ParentHolder is Pawn_ApparelTracker apparelTracker)
            {
                return apparelTracker.pawn != null
                    && apparelTracker.pawn.Faction == Faction.OfPlayer;
            }

            if (thing.ParentHolder is Pawn_EquipmentTracker equipmentTracker)
            {
                return equipmentTracker.pawn != null
                    && equipmentTracker.pawn.Faction == Faction.OfPlayer;
            }

            if (thing.ParentHolder != null && !(thing.ParentHolder is Map))
            {
                Thing? ownerThing = thing.ParentHolder as Thing;
                if (ownerThing is Pawn holderPawn)
                    return holderPawn.Faction == Faction.OfPlayer;
            }

            if (thing.Position.IsValid && thing.Position.InBounds(map))
            {
                Room? room = thing.GetRoom();
                if (room != null && room.IsPrisonCell)
                    return false;
            }

            return true;
        }

        public static string DescribeConditions(CharacterSpawnOptionDef? optionDef)
        {
            if (optionDef == null || optionDef.requiredConditions == null || optionDef.requiredConditions.Count == 0)
                return "";

            List<string> parts = optionDef.requiredConditions
                .Where(condition => condition != null)
                .Select(DescribeCondition)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            if (parts.Count == 0)
                return "";

            string joiner = optionDef.conditionLogic switch
            {
                CharacterSpawnConditionLogic.Any => " / ",
                CharacterSpawnConditionLogic.Not => " / ",
                _ => " + "
            };

            string prefix = optionDef.conditionLogic switch
            {
                CharacterSpawnConditionLogic.Any => "满足其一：",
                CharacterSpawnConditionLogic.Not => "必须不满足：",
                _ => "需要："
            };

            return prefix + string.Join(joiner, parts);
        }

        public static string DescribeCondition(CharacterSpawnConditionDef? condition)
        {
            if (condition == null)
                return string.Empty;

            int minCount = Math.Max(1, condition.minCount);
            return condition.matchMode switch
            {
                CharacterSpawnConditionMatchMode.SpecificThingDef => $"{minCount}× {condition.thingDefName}",
                CharacterSpawnConditionMatchMode.ThingCategory => $"{minCount}× 分类 {condition.categoryDefName}",
                CharacterSpawnConditionMatchMode.TradeTag => $"{minCount}× 标签 {condition.tradeTag}",
                CharacterSpawnConditionMatchMode.FoodType => $"{minCount}× 食物类型 {condition.foodType}",
                CharacterSpawnConditionMatchMode.FoodPreferability => $"{minCount}× 食物等级 {condition.foodPreferability}",
                _ => condition.GetDisplayLabel()
            };
        }

        private static bool MatchesConditionThing(Thing thing, CharacterSpawnConditionDef condition)
        {
            ThingDef def = thing.def;
            switch (condition.matchMode)
            {
                case CharacterSpawnConditionMatchMode.SpecificThingDef:
                    return !string.IsNullOrWhiteSpace(condition.thingDefName)
                        && string.Equals(def.defName, condition.thingDefName, StringComparison.OrdinalIgnoreCase);

                case CharacterSpawnConditionMatchMode.ThingCategory:
                    if (string.IsNullOrWhiteSpace(condition.categoryDefName) || def.thingCategories == null)
                        return false;
                    return def.thingCategories.Any(cat => cat != null && string.Equals(cat.defName, condition.categoryDefName, StringComparison.OrdinalIgnoreCase));

                case CharacterSpawnConditionMatchMode.TradeTag:
                    if (string.IsNullOrWhiteSpace(condition.tradeTag) || def.tradeTags == null)
                        return false;
                    return def.tradeTags.Any(tag => string.Equals(tag, condition.tradeTag, StringComparison.OrdinalIgnoreCase));

                case CharacterSpawnConditionMatchMode.FoodType:
                    return def.IsIngestible && def.ingestible != null && (def.ingestible.foodType & condition.foodType) != 0;

                case CharacterSpawnConditionMatchMode.FoodPreferability:
                    return def.IsIngestible
                        && def.ingestible != null
                        && condition.foodPreferability != FoodPreferability.Undefined
                        && def.ingestible.preferability == condition.foodPreferability;

                default:
                    return false;
            }
        }

        public static CharacterSpawnArrivalDef? ResolveArrivalDef(CharacterSpawnSettings? settings)
        {
            string arrivalDefName = settings?.arrivalDefName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(arrivalDefName))
            {
                CharacterSpawnArrivalDef? named = DefDatabase<CharacterSpawnArrivalDef>.GetNamedSilentFail(arrivalDefName);
                if (named != null && named.enabled && AreConditionsSatisfied(settings?.sourceMapForConditionCheck, named.requiredConditions))
                    return named;
            }

            return DefDatabase<CharacterSpawnArrivalDef>.AllDefsListForReading
                .Where(def => def != null && def.enabled)
                .Where(def => AreConditionsSatisfied(settings?.sourceMapForConditionCheck, def))
                .OrderBy(def => def.sortOrder)
                .ThenBy(def => def.label ?? def.defName)
                .FirstOrDefault(def => def.mode == (settings?.arrivalMode ?? SummonArrivalMode.Standing));
        }

        public static CharacterSpawnEventDef? ResolveSpawnEventDef(CharacterSpawnSettings? settings)
        {
            string spawnEventDefName = settings?.spawnEventDefName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(spawnEventDefName))
            {
                CharacterSpawnEventDef? named = DefDatabase<CharacterSpawnEventDef>.GetNamedSilentFail(spawnEventDefName);
                if (named != null && named.enabled && AreConditionsSatisfied(settings?.sourceMapForConditionCheck, named.requiredConditions))
                    return named;
            }

            return DefDatabase<CharacterSpawnEventDef>.AllDefsListForReading
                .Where(def => def != null && def.enabled)
                .Where(def => AreConditionsSatisfied(settings?.sourceMapForConditionCheck, def))
                .OrderBy(def => def.sortOrder)
                .ThenBy(def => def.label ?? def.defName)
                .FirstOrDefault(def => def.mode == (settings?.spawnEvent ?? SummonSpawnEventMode.Message));
        }

        public static CharacterSpawnAnimationDef? ResolveSpawnAnimationDef(CharacterSpawnSettings? settings)
        {
            string spawnAnimationDefName = settings?.spawnAnimationDefName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(spawnAnimationDefName))
            {
                CharacterSpawnAnimationDef? named = DefDatabase<CharacterSpawnAnimationDef>.GetNamedSilentFail(spawnAnimationDefName);
                if (named != null && named.enabled && AreConditionsSatisfied(settings?.sourceMapForConditionCheck, named.requiredConditions))
                    return named;
            }

            return DefDatabase<CharacterSpawnAnimationDef>.AllDefsListForReading
                .Where(def => def != null && def.enabled)
                .Where(def => AreConditionsSatisfied(settings?.sourceMapForConditionCheck, def))
                .OrderBy(def => def.sortOrder)
                .ThenBy(def => def.label ?? def.defName)
                .FirstOrDefault(def => def.mode == (settings?.spawnAnimation ?? SummonSpawnAnimationMode.None));
        }

        public static CharacterSpawnSettings NormalizeSpawnSettings(CharacterSpawnSettings? settings)
        {
            CharacterSpawnSettings normalized = settings?.Clone() ?? new CharacterSpawnSettings();

            CharacterSpawnArrivalDef? arrivalDef = ResolveArrivalDef(normalized);
            if (arrivalDef != null)
            {
                normalized.arrivalDefName = arrivalDef.defName;
                normalized.arrivalMode = arrivalDef.mode;
            }

            CharacterSpawnEventDef? eventDef = ResolveSpawnEventDef(normalized);
            if (eventDef != null)
            {
                normalized.spawnEventDefName = eventDef.defName;
                normalized.spawnEvent = eventDef.mode;
            }

            CharacterSpawnAnimationDef? animationDef = ResolveSpawnAnimationDef(normalized);
            if (animationDef != null)
            {
                normalized.spawnAnimationDefName = animationDef.defName;
                normalized.spawnAnimation = animationDef.mode;
                if (normalized.spawnAnimationScale <= 0f)
                    normalized.spawnAnimationScale = animationDef.defaultScale;
            }
            else
            {
                normalized.spawnAnimationDefName = string.Empty;
                normalized.spawnAnimation = SummonSpawnAnimationMode.None;
            }

            if (arrivalDef == null)
            {
                normalized.arrivalDefName = string.Empty;
                normalized.arrivalMode = SummonArrivalMode.Standing;
            }

            if (eventDef == null)
            {
                normalized.spawnEventDefName = string.Empty;
                normalized.spawnEvent = SummonSpawnEventMode.None;
            }

            normalized.spawnAnimationScale = Math.Max(0.1f, normalized.spawnAnimationScale);
            return normalized;
        }

        public static bool TryFindSpawnCell(Map? map, IntVec3 desiredCell, int searchRadius, out IntVec3 resolvedCell)
        {
            resolvedCell = desiredCell;
            if (map == null)
            {
                return false;
            }

            if (resolvedCell.IsValid && resolvedCell.InBounds(map) && resolvedCell.Standable(map) && !resolvedCell.Fogged(map))
            {
                return true;
            }

            if (CellFinder.TryFindRandomCellNear(
                desiredCell.InBounds(map) ? desiredCell : map.Center,
                map,
                Math.Max(1, searchRadius),
                c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map),
                out resolvedCell))
            {
                return true;
            }

            resolvedCell = map.Center;
            return resolvedCell.InBounds(map) && resolvedCell.Standable(map);
        }

        public static PawnKindDef ResolvePawnKindForRace(ThingDef? raceDef)
        {
            if (raceDef == null || raceDef == ThingDefOf.Human)
            {
                return PawnKindDefOf.Colonist;
            }

            PawnKindDef? matchingKind = DefDatabase<PawnKindDef>.AllDefs
                .FirstOrDefault(kind => kind.race == raceDef && kind.defaultFactionDef == null);

            if (matchingKind != null)
            {
                return matchingKind;
            }

            matchingKind = DefDatabase<PawnKindDef>.AllDefs
                .FirstOrDefault(kind => kind.race == raceDef);

            if (matchingKind != null)
            {
                return matchingKind;
            }

            Log.Warning($"[CharacterStudio] 未找到种族 {raceDef.defName} 对应的 PawnKindDef，回退到 Colonist");
            return PawnKindDefOf.Colonist;
        }

        public static IntVec3 ResolveSpawnOrigin(Map map, Pawn? preferredPawn)
        {
            if (preferredPawn != null && preferredPawn.Spawned && preferredPawn.Map == map)
            {
                return preferredPawn.Position;
            }

            Pawn? colonist = map.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
            if (colonist != null)
            {
                return colonist.Position;
            }

            return map.Center;
        }

        public static Pawn? GeneratePawn(PawnKindDef? pawnKind, Faction? faction)
        {
            if (pawnKind == null)
            {
                return null;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(
                pawnKind,
                faction,
                PawnGenerationContext.NonPlayer,
                tile: -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: false,
                colonistRelationChanceFactor: 0f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowPregnant: false,
                allowFood: false,
                allowAddictions: false
            );

            return PawnGenerator.GeneratePawn(request);
        }

        public static void SpawnPawnWithSettings(Pawn pawn, Map map, IntVec3 cell, CharacterSpawnSettings settings)
        {
            if (pawn == null || map == null)
            {
                throw new ArgumentNullException(map == null ? nameof(map) : nameof(pawn));
            }

            settings = NormalizeSpawnSettings(settings);

            PlaySpawnAnimation(map, cell, settings);

            if (settings.arrivalMode == SummonArrivalMode.DropPod)
            {
                var dropPodInfo = new ActiveTransporterInfo();
                dropPodInfo.innerContainer.TryAdd(pawn);
                dropPodInfo.openDelay = 60;
                dropPodInfo.leaveSlag = false;
                DropPodUtility.MakeDropPodAt(cell, map, dropPodInfo);
                return;
            }

            GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
        }

        public static void SendSpawnEvent(CharacterSpawnSettings settings, Pawn pawn, Map map, IntVec3 cell, string successText, string letterLabel)
        {
            settings = NormalizeSpawnSettings(settings);

            string resolvedMessageText = string.IsNullOrWhiteSpace(settings.eventMessageText)
                ? successText
                : settings.eventMessageText;
            string resolvedLetterTitle = string.IsNullOrWhiteSpace(settings.eventLetterTitle)
                ? letterLabel
                : settings.eventLetterTitle;

            switch (settings.spawnEvent)
            {
                case SummonSpawnEventMode.None:
                    return;
                case SummonSpawnEventMode.PositiveLetter:
                    Find.LetterStack?.ReceiveLetter(resolvedLetterTitle, resolvedMessageText, LetterDefOf.PositiveEvent, new TargetInfo(cell, map));
                    return;
                default:
                    Messages.Message(resolvedMessageText, pawn, MessageTypeDefOf.PositiveEvent, true);
                    return;
            }
        }

        private static void PlaySpawnAnimation(Map map, IntVec3 cell, CharacterSpawnSettings settings)
        {
            float scale = Math.Max(0.1f, settings.spawnAnimationScale);

            switch (settings.spawnAnimation)
            {
                case SummonSpawnAnimationMode.DustPuff:
                    FleckMaker.ThrowDustPuff(cell, map, scale);
                    break;
                case SummonSpawnAnimationMode.MicroSparks:
                    FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), map);
                    break;
                case SummonSpawnAnimationMode.LightningGlow:
                    FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, scale);
                    break;
                case SummonSpawnAnimationMode.FireGlow:
                    FleckMaker.ThrowFireGlow(cell.ToVector3Shifted(), map, scale);
                    break;
                case SummonSpawnAnimationMode.Smoke:
                    FleckMaker.ThrowSmoke(cell.ToVector3Shifted(), map, scale);
                    break;
                case SummonSpawnAnimationMode.ExplosionEffect:
                    FleckMaker.ThrowDustPuff(cell, map, scale * 1.5f);
                    FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, scale * 2f);
                    FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), map);
                    break;
            }
        }
    }
}
