using System;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 预览生命周期 / 状态消息
        // ─────────────────────────────────────────────

        private bool EnsureMannequinReady()
        {
            if (mannequin == null)
            {
                InitializeMannequin();
            }

            if (mannequin == null)
            {
                ShowStatus("CS_Studio_Err_MannequinFailed".Translate());
                return false;
            }

            return true;
        }

        private void SyncPreviewRace(ThingDef? previewRace, Pawn? sourcePawn)
        {
            if (previewRace == null || !EnsureMannequinReady())
            {
                return;
            }

            mannequin!.SetRace(previewRace);
            if (sourcePawn != null)
            {
                mannequin.CopyAppearanceFrom(sourcePawn);
                Log.Message($"[CharacterStudio] 已将人偶种族同步为 {previewRace.defName} 并复制外观");
            }
        }

        private ThingDef ResolvePreviewRaceForReset(ThingDef? preferredRace = null)
        {
            return preferredRace
                ?? targetPawn?.def
                ?? mannequin?.CurrentPawn?.def
                ?? DefDatabase<ThingDef>.GetNamed("Human");
        }

        private void ForceResetPreviewMannequin(ThingDef? preferredRace = null, Pawn? sourcePawn = null)
        {
            if (!EnsureMannequinReady())
            {
                return;
            }

            ThingDef previewRace = ResolvePreviewRaceForReset(preferredRace);
            mannequin!.ForceReset(previewRace);
            if (sourcePawn != null)
            {
                mannequin.CopyAppearanceFrom(sourcePawn);
                Log.Message($"[CharacterStudio] 已强制重置人偶并复制外观: {previewRace.defName}");
            }
        }

        private void InitializeMannequin()
        {
            try
            {
                mannequin = new MannequinManager();
                mannequin.Initialize();

                if (targetPawn != null)
                {
                    mannequin.SetRace(targetPawn.def);
                    mannequin.CopyAppearanceFrom(targetPawn);
                }

                RefreshPreview();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 初始化人偶失败: {ex}");
                mannequin = null;
                ShowStatus("CS_Studio_Err_MannequinFailed".Translate());
            }
        }

        private void CleanupMannequin()
        {
            mannequin?.Cleanup();
            mannequin = null;
        }

        private void RefreshPreview()
        {
            if (!EnsureMannequinReady())
            {
                return;
            }

            var previewPlan = BuildApplicationPlan(null, true, "EditorPreview");
            mannequin!.ApplyPlan(previewPlan);
            if (!previewPlan.isValid)
            {
                string statusKey = string.IsNullOrWhiteSpace(previewPlan.statusMessage)
                    ? "CS_Studio_Err_ApplyFailedCheckLog"
                    : previewPlan.statusMessage;
                ShowStatus(statusKey.Translate());
                Log.Warning($"[CharacterStudio] 预览计划无效: source={previewPlan.source}, status={previewPlan.statusMessage ?? "<empty>"}");
                return;
            }

            if (previewPlan.warnings != null && previewPlan.warnings.Count > 0)
            {
                ShowStatus(previewPlan.warnings[0].Translate());
            }

            var previewPawn = mannequin.CurrentPawn;
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

            skinComp.SetPreviewExpressionOverride(previewExpressionOverrideEnabled ? previewExpression : null);
            skinComp.SetPreviewMouthState(previewMouthStateOverrideEnabled ? previewMouthState : null);
            skinComp.SetPreviewLidState(previewLidStateOverrideEnabled ? previewLidState : null);
            skinComp.SetPreviewBrowState(previewBrowStateOverrideEnabled ? previewBrowState : null);
            skinComp.SetPreviewEmotionOverlayState(previewEmotionStateOverrideEnabled ? previewEmotionState : null);
            skinComp.SetPreviewEyeDirection(previewEyeDirectionOverrideEnabled ? previewEyeDirection : null);
            skinComp.EnsureFaceRuntimeStateReadyForPreview();

            string currentTriggerKey = GetSelectedEquipmentAnimationTriggerKey();
            if (!previewEquipmentAnimationPlaying || string.IsNullOrWhiteSpace(currentTriggerKey))
            {
                skinComp.ClearEquipmentAnimationState();
                previewEquipmentAnimationTriggerKey = string.Empty;
            }
            else
            {
                previewEquipmentAnimationTriggerKey = currentTriggerKey;
                int durationTicks = GetSelectedEquipmentAnimationDurationTicks();
                if (durationTicks > 0)
                {
                    int now = Find.TickManager?.TicksGame ?? 0;
                    int startTick = skinComp.IsTriggeredEquipmentAnimationActive(currentTriggerKey)
                        ? skinComp.triggeredEquipmentAnimationStartTick
                        : now;
                    if (startTick < 0)
                    {
                        startTick = now;
                    }

                    skinComp.TriggerEquipmentAnimationState(currentTriggerKey, startTick, durationTicks);
                }
            }

            skinComp.RequestRenderRefresh();
        }

        private void ShowStatus(string message)
        {
            statusMessage = message;
            statusMessageTime = 3f;
        }
    }
}
