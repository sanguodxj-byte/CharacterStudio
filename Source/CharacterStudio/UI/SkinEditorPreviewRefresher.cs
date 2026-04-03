using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// Thin orchestration seam for editor preview refresh.
    ///
    /// This first extraction intentionally keeps behavior in the editor type via delegates,
    /// while moving the refresh sequence into a dedicated collaborator.
    /// Later slices can replace delegates with narrower data contracts.
    /// </summary>
    internal static class SkinEditorPreviewRefresher
    {
        internal static void Refresh(Dialog_SkinEditor editor)
        {
            if (!editor.EnsureMannequinReadyForRefresh())
            {
                return;
            }

            var previewPlan = editor.BuildPreviewApplicationPlan();
            editor.EditorMannequin!.ApplyPlan(previewPlan);
            if (!previewPlan.isValid)
            {
                string statusKey = string.IsNullOrWhiteSpace(previewPlan.statusMessage)
                    ? "CS_Studio_Err_ApplyFailedCheckLog"
                    : previewPlan.statusMessage;
                editor.ShowPreviewStatus(statusKey.Translate());
                Log.Warning($"[CharacterStudio] 预览计划无效: source={previewPlan.source}, status={previewPlan.statusMessage ?? "<empty>"}");
                return;
            }

            if (previewPlan.warnings != null && previewPlan.warnings.Count > 0)
            {
                editor.ShowPreviewStatus(previewPlan.warnings[0].Translate());
            }

            var previewPawn = editor.EditorMannequin.CurrentPawn;
            if (previewPawn == null)
            {
                Log.Warning("[CharacterStudio] 预览刷新后未取得人偶 Pawn，已跳过预览覆盖状态同步");
                return;
            }

            CharacterStudio.Rendering.Patch_PawnRenderTree.ForceRebuildRenderTree(previewPawn);

            var skinComp = previewPawn.GetComp<CompPawnSkin>();
            if (skinComp == null)
            {
                Log.Warning($"[CharacterStudio] 预览人偶缺少 CompPawnSkin: {previewPawn.LabelShort}");
                return;
            }

            editor.ApplyPreviewOverridesToSkinComp(skinComp);

            string currentTriggerKey = editor.GetPreviewEquipmentAnimationTriggerKey();
            if (!editor.IsPreviewEquipmentAnimationPlaying || string.IsNullOrWhiteSpace(currentTriggerKey))
            {
                skinComp.ClearEquipmentAnimationState();
                editor.SetPreviewEquipmentAnimationTriggerKey(string.Empty);
            }
            else
            {
                editor.SetPreviewEquipmentAnimationTriggerKey(currentTriggerKey);
                int durationTicks = editor.GetPreviewEquipmentAnimationDurationTicks();
                if (durationTicks > 0)
                {
                    editor.ApplyPreviewEquipmentAnimationToSkinComp(skinComp, currentTriggerKey, durationTicks);
                }
            }

            skinComp.RequestRenderRefresh();
        }
    }
}
