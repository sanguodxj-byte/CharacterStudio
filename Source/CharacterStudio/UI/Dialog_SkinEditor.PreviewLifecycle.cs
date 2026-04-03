using System;
using CharacterStudio.Core;
using CharacterStudio.Design;
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

        internal MannequinManager? EditorMannequin => mannequin;

        internal bool IsPreviewEquipmentAnimationPlaying => previewEquipmentAnimationPlaying;

        internal bool EnsureMannequinReadyForRefresh()
        {
            return EnsureMannequinReady();
        }

        internal CharacterApplicationPlan BuildPreviewApplicationPlan()
        {
            return BuildApplicationPlan(null, true, "EditorPreview");
        }

        internal void ShowPreviewStatus(string message)
        {
            ShowStatus(message);
        }

        internal string GetPreviewEquipmentAnimationTriggerKey()
        {
            return GetSelectedEquipmentAnimationTriggerKey();
        }

        internal int GetPreviewEquipmentAnimationDurationTicks()
        {
            return GetSelectedEquipmentAnimationDurationTicks();
        }

        internal void SetPreviewEquipmentAnimationTriggerKey(string value)
        {
            previewEquipmentAnimationTriggerKey = value;
        }

        internal void ApplyPreviewOverridesToSkinComp(CompPawnSkin skinComp)
        {
            skinComp.SetPreviewExpressionOverride(previewExpressionOverrideEnabled ? previewExpression : null);
            skinComp.SetPreviewMouthState(previewMouthStateOverrideEnabled ? previewMouthState : null);
            skinComp.SetPreviewLidState(previewLidStateOverrideEnabled ? previewLidState : null);
            skinComp.SetPreviewBrowState(previewBrowStateOverrideEnabled ? previewBrowState : null);
            skinComp.SetPreviewEmotionOverlayState(previewEmotionStateOverrideEnabled ? previewEmotionState : null);
            skinComp.SetPreviewEyeDirection(previewEyeDirectionOverrideEnabled ? previewEyeDirection : null);
            skinComp.EnsureFaceRuntimeStateReadyForPreview();
        }

        internal void ApplyPreviewEquipmentAnimationToSkinComp(CompPawnSkin skinComp, string triggerKey, int durationTicks)
        {
            ApplyPreviewEquipmentAnimationState(skinComp, triggerKey, durationTicks);
        }

        private void RefreshPreview()
        {
            SkinEditorPreviewRefresher.Refresh(this);
        }

        private void ShowStatus(string message)
        {
            statusMessage = message;
            statusMessageTime = 3f;
        }
    }
}
