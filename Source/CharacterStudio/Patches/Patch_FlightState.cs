using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using CharacterStudio.Abilities;

namespace CharacterStudio.Patches
{
    [StaticConstructorOnStartup]
    public static class Patch_FlightState
    {
        public static void Apply(Harmony harmony)
        {
            harmony.PatchAll(typeof(Patch_FlightState).Assembly);
            
            // 拦截近战攻击：如果目标飞得太高，近战无法触及
            var meleeTarget = AccessTools.Method(typeof(Verb_MeleeAttack), "CanHitTargetFrom");
            var meleePrefix = AccessTools.Method(typeof(Patch_FlightState), nameof(MeleeCanHitTarget_Prefix));
            if (meleeTarget != null && meleePrefix != null)
            {
                harmony.Patch(meleeTarget, prefix: new HarmonyMethod(meleePrefix));
            }
        }

        public static bool MeleeCanHitTarget_Prefix(Verb __instance, LocalTargetInfo targ, ref bool __result)
        {
            if (targ.Thing is Pawn targetPawn && IsInFlightState(targetPawn))
            {
                var abilityComp = targetPawn.GetComp<CompCharacterAbilityRuntime>();
                // 高度超过 0.6 (大约略高于人头) 时，近战无法够到
                if (abilityComp != null && abilityComp.TotalFlightHeight > 0.6f)
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }

        private static bool IsInFlightState(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead) return false;
            var abilityComp = pawn.GetComp<CompCharacterAbilityRuntime>();
            return abilityComp != null && abilityComp.IsFlightStateActive();
        }

        // --- Pathfinding & Locomotion Patches ---

        [HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new Type[] { typeof(IntVec3) })]
        public static class CostToMoveIntoCell_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(ref float __result, Pawn ___pawn, IntVec3 c)
            {
                if (IsInFlightState(___pawn))
                {
                    // If flying, treat every cell as minimum cost (half cardinal ticks)
                    // unless it is an absolutely out-of-bounds or totally impassable wall edge case.
                    // But for true flight, we can just return a flat, low cost to pass right over walls.
                    __result = (float)___pawn.TicksPerMoveCardinal / 2f;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Pawn_PathFollower), "BuildingBlockingNextPathCell")]
        public static class BuildingBlockingNextPathCell_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref Building __result, Pawn ___pawn)
            {
                // Flights ignore buildings blocking
                if (IsInFlightState(___pawn))
                {
                    __result = null!;
                }
            }
        }

        [HarmonyPatch(typeof(Pawn_PathFollower), "NextCellDoorToWaitForOrManuallyOpen")]
        public static class NextCellDoorToWaitForOrManuallyOpen_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref Building_Door __result, Pawn ___pawn)
            {
                // Flights don't wait for doors
                if (IsInFlightState(___pawn))
                {
                    __result = null!;
                }
            }
        }

        [HarmonyPatch(typeof(GenGrid), nameof(GenGrid.StandableBy))]
        public static class StandableBy_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(IntVec3 c, Map map, Pawn pawn, ref bool __result)
            {
                if (IsInFlightState(pawn))
                {
                    __result = true; // Everywhere is standable while flying
                }
            }
        }

        [HarmonyPatch(typeof(Pawn_PathFollower), "PawnCanOccupy")]
        public static class PawnCanOccupy_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref bool __result, Pawn ___pawn)
            {
                if (IsInFlightState(___pawn))
                {
                    __result = true;
                }
            }
        }

        [HarmonyPatch(typeof(ReachabilityUtility), "CanReach", new Type[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(bool), typeof(bool), typeof(TraverseMode) })]
        public static class CanReach_PawnDest_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, LocalTargetInfo dest, ref bool __result)
            {
                if (IsInFlightState(pawn) && (!dest.HasThing || dest.Thing.Map == pawn.Map))
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ReachabilityUtility), "CanReach", new Type[] { typeof(Pawn), typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(bool), typeof(bool), typeof(TraverseMode) })]
        public static class CanReach_PawnStartDest_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, ref bool __result)
            {
                if (IsInFlightState(pawn))
                {
                    __result = true;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(JobGiver_MoveToStandable), "TryGiveJob")]
        public static class JobGiver_MoveToStandable_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, ref Job __result)
            {
                if (IsInFlightState(pawn))
                {
                    __result = null!;
                    return false;
                }
                return true;
            }
        }
    }
}
