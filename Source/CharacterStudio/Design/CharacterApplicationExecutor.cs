using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Design
{
    /// <summary>
    /// 第二阶段最小计划执行器。
    /// 暂时仍复用 PawnSkinRuntimeUtility 作为底层运行时入口，
    /// 但由应用计划统一调度，避免预览与正式应用直接散落调用。
    /// </summary>
    public static class CharacterApplicationExecutor
    {
        public static bool Execute(CharacterApplicationPlan? plan)
        {
            if (plan == null)
            {
                return false;
            }

            if (!plan.spawnAsNewPawn && plan.targetPawn == null)
            {
                plan.isValid = false;
                plan.statusMessage = "CS_Studio_Msg_TargetPawnRequired";
                return false;
            }

            plan.runtimeSkin = PawnSkinRuntimeValidator.PrepareForRuntime(plan.runtimeSkin);
            plan.runtimeSkin.RemoveApparelHidingData();

            if (string.IsNullOrWhiteSpace(plan.runtimeSkin.defName))
            {
                plan.warnings ??= new System.Collections.Generic.List<string>();
                plan.warnings.Add("CS_Studio_Warn_RuntimeSkinDefNameMissing");
            }

            plan.isValid = true;
            plan.statusMessage = plan.isPreview
                ? "CS_Studio_Msg_PreviewApplied"
                : "CS_Studio_Msg_AppliedToPawn";

            if (plan.spawnAsNewPawn)
            {
                return TrySpawnAsNewPawn(plan);
            }

            if (!PawnSkinRuntimeUtility.ApplySkinToPawn(
                plan.targetPawn,
                plan.runtimeSkin,
                fromDefaultRaceBinding: false,
                previewMode: plan.isPreview,
                applicationSource: plan.source ?? string.Empty))
            {
                plan.isValid = false;
                plan.statusMessage = "CS_Studio_Err_ApplyNoSkinComp";
                return false;
            }

            return true;
        }

        private static bool TrySpawnAsNewPawn(CharacterApplicationPlan plan)
        {
            Map? map = plan.spawnMap;
            if (map == null)
            {
                plan.isValid = false;
                plan.statusMessage = "CS_Studio_Err_NoMap";
                return false;
            }

            PawnKindDef pawnKind = plan.spawnPawnKind ?? CharacterSpawnUtility.ResolvePawnKindForRace(plan.spawnRaceDef ?? ThingDefOf.Human);
            Faction faction = plan.spawnFaction ?? Faction.OfPlayer;
            Pawn? pawn = CharacterSpawnUtility.GeneratePawn(pawnKind, faction);
            if (pawn == null)
            {
                plan.isValid = false;
                plan.statusMessage = "CS_Studio_Err_ApplyFailedCheckLog";
                return false;
            }

            if (pawn.Faction != faction)
            {
                pawn.SetFaction(faction);
            }

            IntVec3 origin = plan.desiredSpawnCell.IsValid
                ? plan.desiredSpawnCell
                : CharacterSpawnUtility.ResolveSpawnOrigin(map, plan.targetPawn);
            if (!CharacterSpawnUtility.TryFindSpawnCell(map, origin, 6, out IntVec3 spawnCell))
            {
                plan.isValid = false;
                plan.statusMessage = "CS_Studio_Err_ApplyFailedCheckLog";
                return false;
            }

            CharacterSpawnUtility.SpawnPawnWithSettings(pawn, map, spawnCell, plan.spawnSettings ?? new CharacterSpawnSettings());

            if (!PawnSkinRuntimeUtility.ApplySkinToPawn(
                pawn,
                plan.runtimeSkin,
                fromDefaultRaceBinding: false,
                previewMode: false,
                applicationSource: plan.source ?? string.Empty))
            {
                pawn.Destroy(DestroyMode.Vanish);
                plan.isValid = false;
                plan.statusMessage = "CS_Studio_Err_ApplyNoSkinComp";
                return false;
            }

            plan.targetPawn = pawn;
            plan.statusMessage = "CS_Studio_SpawnedNewPawn";
            CharacterSpawnUtility.SendSpawnEvent(
                plan.spawnSettings ?? new CharacterSpawnSettings(),
                pawn,
                map,
                spawnCell,
                "CS_Studio_SpawnedNewPawnMessage".Translate(pawn.LabelShort),
                "CS_Studio_SpawnedNewPawnLetter".Translate(pawn.LabelShort));
            return true;
        }
    }
}
