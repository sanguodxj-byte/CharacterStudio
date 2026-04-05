using System;
using System.Linq;
using CharacterStudio.Items;
using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    public sealed class CharacterSpawnSettings
    {
        public SummonArrivalMode arrivalMode = SummonArrivalMode.Standing;
        public SummonSpawnEventMode spawnEvent = SummonSpawnEventMode.Message;
        public SummonSpawnAnimationMode spawnAnimation = SummonSpawnAnimationMode.None;
        public float spawnAnimationScale = 1f;

        public CharacterSpawnSettings Clone()
        {
            return new CharacterSpawnSettings
            {
                arrivalMode = arrivalMode,
                spawnEvent = spawnEvent,
                spawnAnimation = spawnAnimation,
                spawnAnimationScale = spawnAnimationScale
            };
        }
    }

    public static class CharacterSpawnUtility
    {
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
            switch (settings.spawnEvent)
            {
                case SummonSpawnEventMode.None:
                    return;
                case SummonSpawnEventMode.PositiveLetter:
                    Find.LetterStack?.ReceiveLetter(letterLabel, successText, LetterDefOf.PositiveEvent, new TargetInfo(cell, map));
                    return;
                default:
                    Messages.Message(successText, pawn, MessageTypeDefOf.PositiveEvent, true);
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
