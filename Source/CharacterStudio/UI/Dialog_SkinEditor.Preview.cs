using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private enum PreviewFacePreset
        {
            Auto,
            Combat,
            Panic,
            Tired,
            Depressed,
            Romance,
            Downed,
            Dead
        }

        private void ApplyPreviewExpressionOverride(bool enabled, ExpressionType expression)
        {
            previewExpressionOverrideEnabled = enabled;
            previewExpression = expression;

            if (enabled)
            {
                previewRuntimeExpressionOverrideEnabled = previewAutoPlayEnabled;
                if (previewAutoPlayEnabled)
                {
                    previewRuntimeExpression = expression;
                }

                previewMouthStateOverrideEnabled = false;
                previewLidStateOverrideEnabled = false;
                previewBrowStateOverrideEnabled = false;
                previewEmotionStateOverrideEnabled = false;
            }
            else if (!previewAutoPlayEnabled)
            {
                previewRuntimeExpressionOverrideEnabled = false;
            }

            SyncPreviewOverridesToSkinComp();
            RefreshPreview();
        }

        private void ApplyPreviewRuntimeExpressionOverride(bool enabled, ExpressionType expression)
        {
            previewRuntimeExpressionOverrideEnabled = enabled;
            previewRuntimeExpression = expression;

            if (enabled)
            {
                previewAutoPlayEnabled = false;
                previewExpressionOverrideEnabled = false;
                previewMouthStateOverrideEnabled = false;
                previewLidStateOverrideEnabled = false;
                previewBrowStateOverrideEnabled = false;
                previewEmotionStateOverrideEnabled = false;
            }

            SyncPreviewOverridesToSkinComp();
            RefreshPreview();
        }

        private void ApplyPreviewEyeDirectionOverride(bool enabled, EyeDirection direction)
        {
            previewEyeDirectionOverrideEnabled = enabled;
            previewEyeDirection = direction;

            if (enabled)
            {
                previewAutoPlayEnabled = false;
            }

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewEyeDirection(enabled ? direction : null);
            RefreshPreview();
        }

        private void OpenPreviewEyeDirectionMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewEyeDirectionOverride(false, previewEyeDirection))
            };

            foreach (EyeDirection direction in Enum.GetValues(typeof(EyeDirection)))
            {
                EyeDirection localDirection = direction;
                options.Add(new FloatMenuOption(GetEyeDirectionLabel(localDirection), () => ApplyPreviewEyeDirectionOverride(true, localDirection)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string GetEyeDirectionLabel(EyeDirection direction)
        {
            return ($"CS_Studio_Face_EyeDir_{direction}").Translate();
        }

        private static string GetMouthStateLabel(MouthState state)
        {
            return ($"CS_Studio_Preview_MouthState_{state}").Translate();
        }

        private static string GetLidStateLabel(LidState state)
        {
            return ($"CS_Studio_Preview_LidState_{state}").Translate();
        }

        private static string GetBrowStateLabel(BrowState state)
        {
            return ($"CS_Studio_Preview_BrowState_{state}").Translate();
        }

        private static string GetEmotionStateLabel(EmotionOverlayState state)
        {
            return ($"CS_Studio_Preview_EmotionState_{state}").Translate();
        }

        private static string GetPreviewPresetLabel(PreviewFacePreset preset)
        {
            return ($"CS_Studio_Preview_Preset_{preset}").Translate();
        }

        private static string GetPreviewFlowStepLabel(string label)
        {
            return ($"CS_Studio_Preview_FlowStep_{label}").Translate();
        }

        private static string GetExpressionRuntimeHint(ExpressionType expression)
        {
            return ($"CS_Studio_Preview_ExpressionHint_{expression}").Translate();
        }

        private static void DrawPreviewExpressionMenuTooltip(Rect rect, string tooltip)
        {
            if (string.IsNullOrWhiteSpace(tooltip))
                return;

            TooltipHandler.TipRegion(rect, tooltip);
        }

        private static string GetPreviewHintLabel(string hintKey)
        {
            return ($"CS_Studio_Preview_Hint_{hintKey}").Translate();
        }

        private PreviewFlowStep GetCurrentPreviewFlowStep()
        {
            if (PreviewAutoPlayFlowSteps.Length == 0)
            {
                return new PreviewFlowStep("Empty", ExpressionType.Neutral, previewAutoPlayIntervalSeconds);
            }

            int stepIndex = Mathf.Abs(previewAutoPlayStepIndex) % PreviewAutoPlayFlowSteps.Length;
            return PreviewAutoPlayFlowSteps[stepIndex];
        }

        private void ResetPreviewAutoPlayState(bool keepEnabled = false)
        {
            previewAutoPlayEnabled = keepEnabled;
            previewAutoPlayStepIndex = 0;
            previewAutoPlayNextStepTime = Time.realtimeSinceStartup;
        }

        private void ApplyPreviewAutoPlayStep()
        {
            if (!previewAutoPlayEnabled)
            {
                return;
            }

            PreviewFlowStep step = GetCurrentPreviewFlowStep();
            float durationSeconds = step.DurationSeconds > 0f
                ? step.DurationSeconds
                : previewAutoPlayIntervalSeconds;

            previewRuntimeExpressionOverrideEnabled = true;
            previewRuntimeExpression = step.RuntimeExpression;

            previewEyeDirectionOverrideEnabled = step.OverrideEyeDirection;
            if (step.OverrideEyeDirection)
            {
                previewEyeDirection = step.EyeDirection;
            }

            previewMouthStateOverrideEnabled = step.MouthState.HasValue;
            if (step.MouthState.HasValue)
            {
                previewMouthState = step.MouthState.Value;
            }

            previewLidStateOverrideEnabled = step.LidState.HasValue;
            if (step.LidState.HasValue)
            {
                previewLidState = step.LidState.Value;
            }

            previewBrowStateOverrideEnabled = step.BrowState.HasValue;
            if (step.BrowState.HasValue)
            {
                previewBrowState = step.BrowState.Value;
            }

            previewEmotionStateOverrideEnabled = step.EmotionState.HasValue;
            if (step.EmotionState.HasValue)
            {
                previewEmotionState = step.EmotionState.Value;
            }

            previewAutoPlayNextStepTime = Time.realtimeSinceStartup + durationSeconds;

            SyncPreviewOverridesToSkinComp();
            RefreshPreview();
        }

        private void UpdatePreviewAutoPlay()
        {
            if (!previewAutoPlayEnabled)
            {
                return;
            }

            if (!EnsureMannequinReady())
            {
                return;
            }

            PreviewFlowStep currentStep = GetCurrentPreviewFlowStep();
            bool needsApplyStep =
                !previewRuntimeExpressionOverrideEnabled
                || previewRuntimeExpression != currentStep.RuntimeExpression;

            if (!needsApplyStep && previewAutoPlayNextStepTime > 0f && Time.realtimeSinceStartup < previewAutoPlayNextStepTime)
            {
                return;
            }

            if (!needsApplyStep && PreviewAutoPlayFlowSteps.Length > 0)
            {
                previewAutoPlayStepIndex = (previewAutoPlayStepIndex + 1) % PreviewAutoPlayFlowSteps.Length;
            }

            ApplyPreviewAutoPlayStep();
        }
        

        private void OpenPreviewExpressionMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewExpressionOverride(false, previewExpression))
            };

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                ExpressionType localExpression = expression;
                string tooltip = GetExpressionRuntimeHint(localExpression);
                options.Add(new FloatMenuOption(
                    GetExpressionTypeLabel(localExpression),
                    () => ApplyPreviewExpressionOverride(true, localExpression),
                    MenuOptionPriority.Default,
                    rect => DrawPreviewExpressionMenuTooltip(rect, tooltip)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyPreviewMouthStateOverride(bool enabled, MouthState state)
        {
            previewMouthStateOverrideEnabled = enabled;
            previewMouthState = state;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewMouthState(enabled ? state : null);
            RefreshPreview();
        }

        private void OpenPreviewMouthStateMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewMouthStateOverride(false, previewMouthState))
            };

            foreach (MouthState state in Enum.GetValues(typeof(MouthState)))
            {
                MouthState localState = state;
                options.Add(new FloatMenuOption(GetMouthStateLabel(localState), () => ApplyPreviewMouthStateOverride(true, localState)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyPreviewLidStateOverride(bool enabled, LidState state)
        {
            previewLidStateOverrideEnabled = enabled;
            previewLidState = state;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewLidState(enabled ? state : null);
            RefreshPreview();
        }

        private void OpenPreviewLidStateMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewLidStateOverride(false, previewLidState))
            };

            foreach (LidState state in Enum.GetValues(typeof(LidState)))
            {
                LidState localState = state;
                options.Add(new FloatMenuOption(GetLidStateLabel(localState), () => ApplyPreviewLidStateOverride(true, localState)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyPreviewBrowStateOverride(bool enabled, BrowState state)
        {
            previewBrowStateOverrideEnabled = enabled;
            previewBrowState = state;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewBrowState(enabled ? state : null);
            RefreshPreview();
        }

        private void OpenPreviewBrowStateMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewBrowStateOverride(false, previewBrowState))
            };

            foreach (BrowState state in Enum.GetValues(typeof(BrowState)))
            {
                BrowState localState = state;
                options.Add(new FloatMenuOption(GetBrowStateLabel(localState), () => ApplyPreviewBrowStateOverride(true, localState)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyPreviewEmotionStateOverride(bool enabled, EmotionOverlayState state)
        {
            previewEmotionStateOverrideEnabled = enabled;
            previewEmotionState = state;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewEmotionOverlayState(enabled ? state : null);
            RefreshPreview();
        }

        private void OpenPreviewEmotionStateMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewEmotionStateOverride(false, previewEmotionState))
            };

            foreach (EmotionOverlayState state in Enum.GetValues(typeof(EmotionOverlayState)))
            {
                EmotionOverlayState localState = state;
                options.Add(new FloatMenuOption(GetEmotionStateLabel(localState), () => ApplyPreviewEmotionStateOverride(true, localState)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyPreviewPreset(PreviewFacePreset preset)
        {
            previewAutoPlayEnabled = false;
            previewAutoPlayStepIndex = 0;
            previewAutoPlayNextStepTime = 0f;

            switch (preset)
            {
                case PreviewFacePreset.Auto:
                    previewExpressionOverrideEnabled = false;
                    previewEyeDirectionOverrideEnabled = false;
                    previewMouthStateOverrideEnabled = false;
                    previewLidStateOverrideEnabled = false;
                    previewBrowStateOverrideEnabled = false;
                    previewEmotionStateOverrideEnabled = false;
                    break;

                case PreviewFacePreset.Combat:
                    SetPreviewPresetState(ExpressionType.WaitCombat, EyeDirection.Right);
                    break;

                case PreviewFacePreset.Panic:
                    SetPreviewPresetState(ExpressionType.Scared, EyeDirection.Left);
                    break;

                case PreviewFacePreset.Tired:
                    SetPreviewPresetState(ExpressionType.Tired, EyeDirection.Down);
                    break;

                case PreviewFacePreset.Depressed:
                    SetPreviewPresetState(ExpressionType.Hopeless, EyeDirection.Down);
                    break;

                case PreviewFacePreset.Romance:
                    SetPreviewPresetState(ExpressionType.Lovin, EyeDirection.Center);
                    break;

                case PreviewFacePreset.Downed:
                    SetPreviewPresetState(ExpressionType.Pain, EyeDirection.Center);
                    break;

                case PreviewFacePreset.Dead:
                    SetPreviewPresetState(ExpressionType.Dead, EyeDirection.Center);
                    break;
            }

            SyncPreviewOverridesToSkinComp();
            RefreshPreview();
        }

        private void SetPreviewPresetState(
            ExpressionType expression,
            EyeDirection eyeDirection,
            MouthState? mouthState = null,
            LidState? lidState = null,
            BrowState? browState = null,
            EmotionOverlayState? emotionState = null)
        {
            previewExpressionOverrideEnabled = true;
            previewExpression = expression;

            previewEyeDirectionOverrideEnabled = true;
            previewEyeDirection = eyeDirection;

            previewMouthStateOverrideEnabled = mouthState.HasValue;
            if (mouthState.HasValue)
            {
                previewMouthState = mouthState.Value;
            }

            previewLidStateOverrideEnabled = lidState.HasValue;
            if (lidState.HasValue)
            {
                previewLidState = lidState.Value;
            }

            previewBrowStateOverrideEnabled = browState.HasValue;
            if (browState.HasValue)
            {
                previewBrowState = browState.Value;
            }

            previewEmotionStateOverrideEnabled = emotionState.HasValue;
            if (emotionState.HasValue)
            {
                previewEmotionState = emotionState.Value;
            }
        }

        private string GetSelectedEquipmentAnimationTriggerKey()
        {
            if (currentTab != EditorTab.Items)
            {
                return string.Empty;
            }

            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                return string.Empty;
            }

            CharacterEquipmentDef? equipment = workingSkin.equipments[selectedEquipmentIndex];
            CharacterEquipmentRenderData? renderData = equipment?.renderData;
            if (renderData == null)
            {
                return string.Empty;
            }

            EquipmentTriggeredAnimationOverride animationState = ResolveSelectedEquipmentAnimationState(renderData);
            if (!animationState.useTriggeredLocalAnimation)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(animationState.triggerAbilityDefName))
            {
                return animationState.triggerAbilityDefName;
            }

            if (!string.IsNullOrWhiteSpace(animationState.animationGroupKey))
            {
                return animationState.animationGroupKey;
            }

            string equipmentKey = !string.IsNullOrWhiteSpace(equipment?.defName)
                ? equipment!.defName!
                : $"Equipment_{selectedEquipmentIndex}";
            return $"Preview_{equipmentKey}";
        }

        private int GetSelectedEquipmentAnimationDurationTicks()
        {
            if (currentTab != EditorTab.Items)
            {
                return 0;
            }

            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                return 0;
            }

            CharacterEquipmentDef? equipment = workingSkin.equipments[selectedEquipmentIndex];
            CharacterEquipmentRenderData? renderData = equipment?.renderData;
            if (renderData == null)
            {
                return 0;
            }

            EquipmentTriggeredAnimationOverride animationState = ResolveSelectedEquipmentAnimationState(renderData);
            if (!animationState.useTriggeredLocalAnimation)
            {
                return 0;
            }

            return Mathf.Max(1, animationState.triggeredDeployTicks)
                + Mathf.Max(0, animationState.triggeredHoldTicks)
                + Mathf.Max(1, animationState.triggeredReturnTicks);
        }

        private EquipmentTriggeredAnimationOverride ResolveSelectedEquipmentAnimationState(CharacterEquipmentRenderData renderData)
        {
            if (previewRotation == Rot4.North && renderData.triggeredAnimationNorth != null)
            {
                return renderData.triggeredAnimationNorth;
            }

            if ((previewRotation == Rot4.East || previewRotation == Rot4.West) && renderData.triggeredAnimationEastWest != null)
            {
                return renderData.triggeredAnimationEastWest;
            }

            if (renderData.triggeredAnimationSouth != null)
            {
                return renderData.triggeredAnimationSouth;
            }

            return new EquipmentTriggeredAnimationOverride
            {
                useTriggeredLocalAnimation = renderData.useTriggeredLocalAnimation,
                triggerAbilityDefName = renderData.triggerAbilityDefName ?? string.Empty,
                animationGroupKey = renderData.animationGroupKey ?? string.Empty,
                triggeredAnimationRole = renderData.triggeredAnimationRole,
                triggeredDeployAngle = renderData.triggeredDeployAngle,
                triggeredReturnAngle = renderData.triggeredReturnAngle,
                triggeredDeployTicks = renderData.triggeredDeployTicks,
                triggeredHoldTicks = renderData.triggeredHoldTicks,
                triggeredReturnTicks = renderData.triggeredReturnTicks,
                triggeredPivotOffset = renderData.triggeredPivotOffset,
                triggeredUseVfxVisibility = renderData.triggeredUseVfxVisibility,
                triggeredVisibleDuringDeploy = renderData.triggeredVisibleDuringDeploy,
                triggeredVisibleDuringHold = renderData.triggeredVisibleDuringHold,
                triggeredVisibleDuringReturn = renderData.triggeredVisibleDuringReturn,
                triggeredVisibleOutsideCycle = renderData.triggeredVisibleOutsideCycle
            };
        }

        private void StopPreviewEquipmentAnimation(bool refreshPreview = true)
        {
            previewEquipmentAnimationPlaying = false;
            previewEquipmentAnimationLastRealtime = -1f;
            previewEquipmentAnimationElapsedTicks = 0f;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            if (skinComp != null)
            {
                skinComp.ClearEquipmentAnimationState();
            }

            previewEquipmentAnimationTriggerKey = string.Empty;
            if (refreshPreview)
            {
                RefreshPreview();
            }
        }

        private void StartPreviewEquipmentAnimation(bool loop)
        {
            string triggerKey = GetSelectedEquipmentAnimationTriggerKey();
            int durationTicks = GetSelectedEquipmentAnimationDurationTicks();
            if (string.IsNullOrWhiteSpace(triggerKey) || durationTicks <= 0)
            {
                StopPreviewEquipmentAnimation(refreshPreview: false);
                return;
            }

            previewEquipmentAnimationLoop = loop;
            previewEquipmentAnimationPlaying = true;
            previewEquipmentAnimationLastRealtime = Time.realtimeSinceStartup;
            previewEquipmentAnimationElapsedTicks = 0f;
            previewEquipmentAnimationTriggerKey = triggerKey;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            if (skinComp != null)
            {
                ApplyPreviewEquipmentAnimationState(skinComp, triggerKey, durationTicks);
            }
        }

        private void TogglePreviewEquipmentAnimation(bool loop)
        {
            string triggerKey = GetSelectedEquipmentAnimationTriggerKey();
            if (string.IsNullOrWhiteSpace(triggerKey))
            {
                StopPreviewEquipmentAnimation();
                return;
            }

            if (previewEquipmentAnimationPlaying
                && string.Equals(previewEquipmentAnimationTriggerKey, triggerKey, StringComparison.OrdinalIgnoreCase)
                && previewEquipmentAnimationLoop == loop)
            {
                StopPreviewEquipmentAnimation();
                return;
            }

            StartPreviewEquipmentAnimation(loop);
            RefreshPreview();
        }

        private void TogglePreviewFaceAnimation(bool loop)
        {
            if (previewFaceAnimationPlaying && previewFaceAnimationLoop == loop)
            {
                previewFaceAnimationPlaying = false;
                previewFaceAnimationLoop = false;
                previewFaceAnimationLastRealtime = -1f;
                previewFaceAnimationElapsedTicks = 0f;
                return;
            }

            previewFaceAnimationPlaying = true;
            previewFaceAnimationLoop = loop;
            previewFaceAnimationLastRealtime = -1f;
            previewFaceAnimationElapsedTicks = 0f;
            
            var skinComp = mannequin?.CurrentPawn?.GetComp<CompPawnSkin>();
            skinComp?.TriggerBlink();
            RefreshPreview();
        }

        private void UpdatePreviewFaceAnimation()
        {
            var skinComp = mannequin?.CurrentPawn?.GetComp<CompPawnSkin>();
            if (skinComp == null)
            {
                previewFaceAnimationLastRealtime = -1f;
                previewFaceAnimationElapsedTicks = 0f;
                return;
            }

            bool blinkActive = skinComp.IsBlinkActive();
            if (!previewFaceAnimationPlaying && !blinkActive)
            {
                previewFaceAnimationLastRealtime = -1f;
                previewFaceAnimationElapsedTicks = 0f;
                return;
            }

            float nowRealtime = Time.realtimeSinceStartup;
            if (previewFaceAnimationLastRealtime < 0f)
            {
                previewFaceAnimationLastRealtime = nowRealtime;
            }
            else
            {
                float deltaRealtime = nowRealtime - previewFaceAnimationLastRealtime;
                if (deltaRealtime > 0f)
                {
                    previewFaceAnimationLastRealtime = nowRealtime;
                    previewFaceAnimationElapsedTicks += deltaRealtime * 60f;

                    int ticksToAdvance = Mathf.FloorToInt(previewFaceAnimationElapsedTicks);
                    if (ticksToAdvance > 0)
                    {
                        previewFaceAnimationElapsedTicks -= ticksToAdvance;
                        skinComp.AdvancePreviewFaceAnimationTicks(ticksToAdvance);
                        RefreshPreview();
                    }
                }
            }

            if (skinComp.IsBlinkActive())
            {
                return;
            }

            previewFaceAnimationElapsedTicks = 0f;
            if (previewFaceAnimationPlaying && previewFaceAnimationLoop)
            {
                skinComp.TriggerBlink();
                previewFaceAnimationLastRealtime = Time.realtimeSinceStartup;
                RefreshPreview();
                return;
            }

            if (previewFaceAnimationPlaying)
            {
                previewFaceAnimationPlaying = false;
                previewFaceAnimationLoop = false;
                previewFaceAnimationLastRealtime = -1f;
                RefreshPreview();
            }
        }

        private void UpdatePreviewEquipmentAnimation()
        {
            if (!previewEquipmentAnimationPlaying)
            {
                return;
            }

            string triggerKey = GetSelectedEquipmentAnimationTriggerKey();
            int durationTicks = GetSelectedEquipmentAnimationDurationTicks();
            if (string.IsNullOrWhiteSpace(triggerKey) || durationTicks <= 0)
            {
                StopPreviewEquipmentAnimation();
                return;
            }

            if (!string.Equals(previewEquipmentAnimationTriggerKey, triggerKey, StringComparison.OrdinalIgnoreCase))
            {
                StartPreviewEquipmentAnimation(previewEquipmentAnimationLoop);
                return;
            }

            float nowRealtime = Time.realtimeSinceStartup;
            if (previewEquipmentAnimationLastRealtime < 0f)
            {
                previewEquipmentAnimationLastRealtime = nowRealtime;
                return;
            }

            float deltaRealtime = nowRealtime - previewEquipmentAnimationLastRealtime;
            if (deltaRealtime <= 0f)
            {
                return;
            }

            previewEquipmentAnimationLastRealtime = nowRealtime;
            previewEquipmentAnimationElapsedTicks += deltaRealtime * 60f;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            if (skinComp == null)
            {
                return;
            }

            if (previewEquipmentAnimationLoop)
            {
                if (durationTicks > 0)
                {
                    previewEquipmentAnimationElapsedTicks %= durationTicks;
                }
            }
            else if (previewEquipmentAnimationElapsedTicks >= durationTicks)
            {
                StopPreviewEquipmentAnimation(refreshPreview: false);
                skinComp.RequestRenderRefresh();
                return;
            }

            ApplyPreviewEquipmentAnimationState(skinComp, triggerKey, durationTicks);
        }

        private void ApplyPreviewEquipmentAnimationState(CompPawnSkin skinComp, string triggerKey, int durationTicks)
        {
            if (skinComp == null || string.IsNullOrWhiteSpace(triggerKey) || durationTicks <= 0)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int elapsedTicks = Mathf.Clamp(Mathf.RoundToInt(previewEquipmentAnimationElapsedTicks), 0, Math.Max(0, durationTicks - 1));
            int startTick = nowTick - elapsedTicks;
            skinComp.TriggerEquipmentAnimationState(triggerKey, startTick, durationTicks);
        }

        private void SyncPreviewOverridesToSkinComp()
        {
            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            if (skinComp == null)
            {
                return;
            }

            skinComp.SetPreviewExpressionOverride(
                previewAutoPlayEnabled
                    ? (ExpressionType?)null
                    : (previewExpressionOverrideEnabled ? previewExpression : (ExpressionType?)null));
            skinComp.SetPreviewRuntimeExpression(previewRuntimeExpressionOverrideEnabled ? previewRuntimeExpression : null);
            skinComp.SetPreviewEyeDirection(previewEyeDirectionOverrideEnabled ? previewEyeDirection : null);
            skinComp.SetPreviewGazeOffset(previewGazeCursorEnabled ? previewGazeCursorOffset : (Vector2?)null);
            skinComp.SetPreviewMouthState(previewMouthStateOverrideEnabled ? previewMouthState : null);
            skinComp.SetPreviewLidState(previewLidStateOverrideEnabled ? previewLidState : null);
            skinComp.SetPreviewBrowState(previewBrowStateOverrideEnabled ? previewBrowState : null);
            skinComp.SetPreviewEmotionOverlayState(previewEmotionStateOverrideEnabled ? previewEmotionState : null);

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
                    ApplyPreviewEquipmentAnimationState(skinComp, currentTriggerKey, durationTicks);
                }
            }

            skinComp.EnsureFaceRuntimeStateReadyForPreview();
            skinComp.RequestRenderRefresh();
        }

        private string GetPreviewOverrideLabel(bool enabled, string value)
        {
            return enabled ? value : "CS_Studio_Face_PreviewAuto".Translate();
        }

        private void DrawPreviewOverrideButton(ref float x, float y, string label, string valueLabel, float labelWidth, float buttonWidth, Action onClick)
        {
            Widgets.Label(new Rect(x, y + 4f, labelWidth, 24f), label);

            Rect buttonRect = new Rect(x + labelWidth, y, buttonWidth, 24f);
            if (UIHelper.DrawToolbarButton(buttonRect, valueLabel))
            {
                onClick();
            }

            x += labelWidth + buttonWidth + Margin;
        }

        // ─────────────────────────────────────────────
        // 预览面板
        // ─────────────────────────────────────────────

        private void OpenPreviewApparelMenu()
        {
            Find.WindowStack.Add(new Dialog_ApparelBrowser(apparel =>
            {
                if (apparel == null)
                {
                    mannequin?.ClearApparel();
                }
                else
                {
                    mannequin?.EquipApparel(apparel);
                }
                RefreshPreview();
            }));
        }

        private void DrawPreviewPanel(Rect rect)
        {
            Rect titleRect = UIHelper.DrawPanelShell(rect, "CS_Studio_Panel_Preview".Translate(), Margin);

            float btnY = titleRect.yMax + 4f;
            float btnWidth = 40f;
            float btnHeight = Mathf.Max(ButtonHeight - 2f, 22f);
            float btnX = rect.x + Margin;
            float maxX = rect.xMax - Margin;

            // ── 旋转 / 缩放 ──
            if (btnX + btnWidth <= maxX && UIHelper.DrawToolbarButton(new Rect(btnX, btnY, btnWidth, btnHeight), "◀", tooltip: "CS_Studio_Preview_RotateLeft".Translate()))
            {
                previewRotation.Rotate(RotationDirection.Counterclockwise);
                RefreshPreview();
            }
            btnX += btnWidth + Margin;

            if (btnX + btnWidth <= maxX && UIHelper.DrawToolbarButton(new Rect(btnX, btnY, btnWidth, btnHeight), "▶", tooltip: "CS_Studio_Preview_RotateRight".Translate()))
            {
                previewRotation.Rotate(RotationDirection.Clockwise);
                RefreshPreview();
            }
            btnX += btnWidth + Margin;

            if (btnX + btnWidth <= maxX && UIHelper.DrawToolbarButton(new Rect(btnX, btnY, btnWidth, btnHeight), "-", tooltip: "CS_Studio_Preview_ZoomOut".Translate()))
            {
                previewZoom = Mathf.Max(0.5f, previewZoom - 0.1f);
            }
            btnX += btnWidth + Margin;

            if (btnX + btnWidth <= maxX && UIHelper.DrawToolbarButton(new Rect(btnX, btnY, btnWidth, btnHeight), "+", tooltip: "CS_Studio_Preview_ZoomIn".Translate()))
            {
                previewZoom = Mathf.Min(2f, previewZoom + 0.1f);
            }
            btnX += btnWidth + Margin;

            float resetBtnWidth = btnWidth + 20f;
            if (btnX + resetBtnWidth <= maxX && UIHelper.DrawToolbarButton(new Rect(btnX, btnY, resetBtnWidth, btnHeight), "↺", tooltip: "CS_Studio_Preview_ResetView".Translate()))
            {
                previewRotation = Rot4.South;
                previewZoom = 1f;
                RefreshPreview();
            }
            btnX += resetBtnWidth + Margin;

            // ── 装扮选择 ──
            if (mannequin != null)
            {
                float apparelBtnWidth = 64f;
                if (btnX + apparelBtnWidth <= maxX && UIHelper.DrawToolbarButton(new Rect(btnX, btnY, apparelBtnWidth, btnHeight), "CS_Studio_Preview_SelectApparel".Translate()))
                {
                    OpenPreviewApparelMenu();
                }
                btnX += apparelBtnWidth + Margin;
            }

            // ── 装备动画控制（仅 Items 选项卡） ──
            if (currentTab == EditorTab.Items)
            {
                float equipmentPreviewBtnWidth = 76f;
                bool canPreviewEquipmentAnimation = !string.IsNullOrWhiteSpace(GetSelectedEquipmentAnimationTriggerKey());

                if (btnX + equipmentPreviewBtnWidth <= maxX)
                {
                    if (UIHelper.DrawToolbarButton(new Rect(btnX, btnY, equipmentPreviewBtnWidth, btnHeight),
                        (previewEquipmentAnimationPlaying && !previewEquipmentAnimationLoop ? "▶ " : string.Empty) + "CS_Studio_Equip_PreviewPlay".Translate(),
                        previewEquipmentAnimationPlaying && !previewEquipmentAnimationLoop))
                    {
                        TogglePreviewEquipmentAnimation(loop: false);
                    }
                    TooltipHandler.TipRegion(new Rect(btnX, btnY, equipmentPreviewBtnWidth, btnHeight), canPreviewEquipmentAnimation
                        ? "CS_Studio_Equip_PreviewPlay_Hint".Translate()
                        : "CS_Studio_Equip_PreviewUnavailable_Hint".Translate());
                }
                btnX += equipmentPreviewBtnWidth + Margin;

                if (btnX + equipmentPreviewBtnWidth <= maxX)
                {
                    if (UIHelper.DrawToolbarButton(new Rect(btnX, btnY, equipmentPreviewBtnWidth, btnHeight),
                        (previewEquipmentAnimationPlaying && previewEquipmentAnimationLoop ? "▶ " : string.Empty) + "CS_Studio_Equip_PreviewLoop".Translate(),
                        previewEquipmentAnimationPlaying && previewEquipmentAnimationLoop))
                    {
                        TogglePreviewEquipmentAnimation(loop: true);
                    }
                    TooltipHandler.TipRegion(new Rect(btnX, btnY, equipmentPreviewBtnWidth, btnHeight), canPreviewEquipmentAnimation
                        ? "CS_Studio_Equip_PreviewLoop_Hint".Translate()
                        : "CS_Studio_Equip_PreviewUnavailable_Hint".Translate());
                }
                btnX += equipmentPreviewBtnWidth + Margin;
            }

            // ── 第二行：表情控制 + Flow + 朝向复选框 + Face 扩展条 ──
            float extY = btnY + btnHeight + Margin;

            if (mannequin != null)
            {
                float controlX = rect.x + Margin;

                // 表情选择按钮
                DrawPreviewOverrideButton(
                    ref controlX,
                    extY,
                    "CS_Studio_Preview_Exp".Translate(),
                    GetPreviewOverrideLabel(previewExpressionOverrideEnabled, GetExpressionTypeLabel(previewExpression)),
                    36f,
                    150f,
                    OpenPreviewExpressionMenu);
                TooltipHandler.TipRegion(
                    new Rect(rect.x + Margin, extY, 190f, ButtonHeight),
                    "CS_Studio_Preview_ExpressionContextHint".Translate() + "\n\n" + GetExpressionRuntimeHint(previewExpression));

                // Flow 按钮
                float autoPlayWidth = 72f;
                if (controlX + autoPlayWidth <= maxX)
                {
                    Rect autoPlayRect = new Rect(controlX, extY, autoPlayWidth, btnHeight);
                    if (UIHelper.DrawToolbarButton(autoPlayRect, (previewAutoPlayEnabled ? "▶ " : string.Empty) + "CS_Studio_Preview_Flow".Translate(), previewAutoPlayEnabled))
                    {
                        previewAutoPlayEnabled = !previewAutoPlayEnabled;
                        if (previewAutoPlayEnabled)
                        {
                            ResetPreviewAutoPlayState(keepEnabled: true);
                            ApplyPreviewAutoPlayStep();
                        }
                        else
                        {
                            previewRuntimeExpressionOverrideEnabled = false;
                            previewAutoPlayStepIndex = 0;
                            previewAutoPlayNextStepTime = 0f;
                            SyncPreviewOverridesToSkinComp();
                            RefreshPreview();
                        }
                    }
                    TooltipHandler.TipRegion(autoPlayRect, "CS_Studio_Preview_FlowTooltip".Translate(GetExpressionTypeLabel(previewExpression)));
                    controlX += autoPlayWidth + Margin;
                }

                // 朝向复选框 + 标签
                float facingBoxSize = 16f;
                float facingMargin = 4f;
                string facingText = "CS_Studio_Preview_EditPerFacing".Translate();
                Text.Font = GameFont.Tiny;
                float facingTextWidth = Text.CalcSize(facingText).x;
                Text.Font = GameFont.Small;
                float facingCtrlTotalWidth = facingBoxSize + facingMargin + facingTextWidth;
                if (controlX + facingCtrlTotalWidth <= maxX)
                {
                    bool prevMode = editLayerOffsetPerFacing;
                    Rect checkRect = new Rect(controlX, extY + (btnHeight - facingBoxSize) / 2f, facingBoxSize, facingBoxSize);
                    Widgets.Checkbox(checkRect.position, ref prevMode, facingBoxSize);
                    if (prevMode != editLayerOffsetPerFacing)
                    {
                        editLayerOffsetPerFacing = prevMode;
                        if (workingSkin != null)
                            workingSkin.editLayerOffsetPerFacing = prevMode;
                    }

                    Rect labelRect = new Rect(checkRect.xMax + facingMargin, extY, facingTextWidth, btnHeight);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = editLayerOffsetPerFacing ? UIHelper.AccentColor : UIHelper.SubtleColor;
                    Widgets.Label(labelRect, facingText);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;

                    TooltipHandler.TipRegion(new Rect(controlX, extY, facingCtrlTotalWidth, btnHeight),
                        "CS_Studio_Preview_EditPerFacingTip".Translate());
                    controlX += facingCtrlTotalWidth + Margin;
                }

                // Face 选项卡扩展条：在控件右侧绘制
                if (currentTab == EditorTab.Face)
                {
                    float faceExtX = controlX + Margin;
                    float faceExtWidth = maxX - Margin - faceExtX;
                    if (faceExtWidth > 40f)
                    {
                        DrawFacePreviewExtensionStrip(faceExtX, extY, faceExtWidth, btnHeight);
                    }
                }

                // 控件行已占用一行，扩展条从下一行开始
                extY += btnHeight + Margin;
            }

            // ── 通用扩展工具条区域（各选项卡可复用） ──
            float extUsedHeight = DrawTabPreviewExtensionStrip(rect.x + Margin, extY, rect.width - Margin * 2, btnHeight);
            extY += extUsedHeight;

            // ── 预览渲染区域 ──
            float previewY = extY;
            float previewHeight = rect.height - previewY + rect.y - Margin;
            Rect previewRect = new Rect(rect.x + Margin, previewY, rect.width - Margin * 2, previewHeight);

            UIHelper.DrawContentCard(previewRect);
            Rect previewInnerRect = previewRect.ContractedBy(6f);
            Widgets.DrawBoxSolid(previewInnerRect, new Color(0.12f, 0.12f, 0.12f));
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(previewInnerRect, 1);
            GUI.color = Color.white;

            if (mannequin != null)
            {
                mannequin.DrawPreview(previewInnerRect, previewRotation, previewZoom);
                DrawReferenceGhostOverlay(previewInnerRect);
                DrawMapTileReferenceGrid(previewInnerRect);
                DrawFaceGazeCursorOverlay(previewInnerRect);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(previewInnerRect, "CS_Studio_Status_Loading".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            DrawPreviewHintsOverlay(previewInnerRect);
            DrawSelectedEquipmentPivotOverlay(previewInnerRect);

            HandlePreviewInput(previewInnerRect);
            HandleFaceGazeCursorInput(previewInnerRect);

            DrawSelectedLayerHighlight(previewInnerRect);
        }

        /// <summary>
        /// 通用扩展工具条：根据当前选项卡绘制对应的扩展控件。
        /// 返回实际占用的高度（0 表示不绘制任何内容）。
        /// 子类或分部文件可重写以追加选项卡专属控件。
        /// </summary>
        protected virtual float DrawTabPreviewExtensionStrip(float x, float y, float width, float rowHeight)
        {
            // 默认不绘制任何扩展内容；各选项卡分部文件可 override 或扩展
            return 0f;
        }

        /// <summary>
        /// Face 选项卡专用扩展条：在表情控件右侧绘制辅助控件。
        /// </summary>
        protected virtual void DrawFacePreviewExtensionStrip(float x, float y, float width, float rowHeight)
        {
            // 默认不绘制；Face 选项卡分部文件可扩展
        }

        private void DrawFaceGazeCursorOverlay(Rect previewRect)
        {
            if (currentTab != EditorTab.Face)
            {
                return;
            }

            Rect hintRect = new Rect(previewRect.x + 10f, previewRect.y + 10f, Mathf.Min(360f, previewRect.width - 20f), 30f);
            Widgets.DrawBoxSolid(hintRect, new Color(0f, 0f, 0f, 0.35f));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(hintRect.ContractedBy(6f), previewGazeCursorEnabled
                ? "CS_Studio_Face_GazeCursor_Hint_Enabled".Translate()
                : "CS_Studio_Face_GazeCursor_Hint_Disabled".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Vector2 center = previewRect.center;
            Vector2 cursorPos = center + new Vector2(previewGazeCursorOffset.x * previewRect.width * 0.22f, -previewGazeCursorOffset.y * previewRect.height * 0.22f);
            Color crossColor = new Color(0.25f, 0.75f, 1f, previewGazeCursorEnabled ? 0.95f : 0.75f);
            Widgets.DrawLine(new Vector2(cursorPos.x - 10f, cursorPos.y), new Vector2(cursorPos.x + 10f, cursorPos.y), crossColor, 2f);
            Widgets.DrawLine(new Vector2(cursorPos.x, cursorPos.y - 10f), new Vector2(cursorPos.x, cursorPos.y + 10f), crossColor, 2f);
            Widgets.DrawBoxSolid(new Rect(cursorPos.x - 2f, cursorPos.y - 2f, 4f, 4f), crossColor);
            GUI.color = Color.white;
        }

        private void HandleFaceGazeCursorInput(Rect previewRect)
        {
            if (currentTab != EditorTab.Face)
            {
                if (previewGazeCursorEnabled || previewGazeCursorOffset != Vector2.zero)
                {
                    previewGazeCursorEnabled = false;
                    SyncPreviewOverridesToSkinComp();
                }
                return;
            }

            if (!previewGazeCursorEnabled)
            {
                return;
            }

            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            bool inside = previewRect.Contains(current.mousePosition);
            bool dragging = current.type == EventType.MouseDrag && current.button == 0;
            bool clicking = current.type == EventType.MouseDown && current.button == 0;

            if (!inside && !dragging && !clicking)
            {
                return;
            }

            previewEyeDirectionOverrideEnabled = false;
            previewAutoPlayEnabled = false;

            float normalizedX = Mathf.Clamp((current.mousePosition.x - previewRect.center.x) / (previewRect.width * 0.22f), -1f, 1f);
            float normalizedY = Mathf.Clamp((previewRect.center.y - current.mousePosition.y) / (previewRect.height * 0.22f), -1f, 1f);
            previewGazeCursorOffset = new Vector2(normalizedX, normalizedY);
            SyncPreviewOverridesToSkinComp();
            if (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
            {
                current.Use();
            }
        }

        private void DrawReferenceGhostOverlay(Rect previewRect)
        {
            // 使用帧开始时预读的状态，避免 IMGUI TextField 拦截 Input.GetKey
            if (targetPawn == null || !isHoldingReferenceGhost)
            {
                return;
            }

            try
            {
                Vector2 portraitSize = new Vector2(256f, 256f);
                var portrait = PortraitsCache.Get(
                    targetPawn,
                    portraitSize,
                    previewRotation,
                    MannequinManager.PreviewCameraOffset,
                    previewZoom
                );

                if (portrait == null)
                {
                    return;
                }

                float drawSize = Mathf.Min(previewRect.width, previewRect.height);

                // 按住 F 时：默认居中；若鼠标位于预览区域内则让参考虚影跟随鼠标
                float x = previewRect.x + (previewRect.width - drawSize) / 2f;
                float y = previewRect.y + (previewRect.height - drawSize) / 2f;
                if (Mouse.IsOver(previewRect))
                {
                    Vector2 mousePos = Event.current.mousePosition;
                    x = mousePos.x - drawSize * 0.5f;
                    y = mousePos.y - drawSize * 0.5f;

                    // 限制在预览框内，避免完全拖出可视区域
                    x = Mathf.Clamp(x, previewRect.x, previewRect.xMax - drawSize);
                    y = Mathf.Clamp(y, previewRect.y, previewRect.yMax - drawSize);
                }

                Rect drawRect = new Rect(x, y, drawSize, drawSize);

                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, ReferenceGhostAlpha);
                GUI.DrawTexture(drawRect, portrait, ScaleMode.ScaleToFit, true);
                GUI.color = prevColor;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 绘制参考虚影失败: {ex.Message}");
            }
        }

        private void DrawMapTileReferenceGrid(Rect previewRect)
        {
            float pixelsPerUnit = GetPreviewPixelsPerUnit(previewRect);
            if (pixelsPerUnit <= 1f)
            {
                return;
            }

            Vector2 center = previewRect.center;
            float halfTile = pixelsPerUnit * 0.5f;

            Vector2 topLeft = new Vector2(center.x - halfTile, center.y - halfTile);
            Vector2 topRight = new Vector2(center.x + halfTile, center.y - halfTile);
            Vector2 bottomRight = new Vector2(center.x + halfTile, center.y + halfTile);
            Vector2 bottomLeft = new Vector2(center.x - halfTile, center.y + halfTile);

            Color lineColor = new Color(0.78f, 0.86f, 1f, 0.32f);
            Color pointColor = new Color(0.92f, 0.96f, 1f, 0.92f);

            DrawDashedLine(topLeft, topRight, lineColor, 2f, 8f, 5f);
            DrawDashedLine(topRight, bottomRight, lineColor, 2f, 8f, 5f);
            DrawDashedLine(bottomRight, bottomLeft, lineColor, 2f, 8f, 5f);
            DrawDashedLine(bottomLeft, topLeft, lineColor, 2f, 8f, 5f);

            DrawReferencePoint(topLeft, pointColor, 5f);
            DrawReferencePoint(topRight, pointColor, 5f);
            DrawReferencePoint(bottomRight, pointColor, 5f);
            DrawReferencePoint(bottomLeft, pointColor, 5f);
        }

        private static void DrawDashedLine(Vector2 start, Vector2 end, Color color, float thickness, float dashLength, float gapLength)
        {
            float totalLength = Vector2.Distance(start, end);
            if (totalLength <= 0.001f)
            {
                return;
            }

            Vector2 direction = (end - start) / totalLength;
            float traveled = 0f;
            while (traveled < totalLength)
            {
                float currentDashLength = Mathf.Min(dashLength, totalLength - traveled);
                Vector2 dashStart = start + direction * traveled;
                Vector2 dashEnd = dashStart + direction * currentDashLength;
                Widgets.DrawLine(dashStart, dashEnd, color, thickness);
                traveled += dashLength + gapLength;
            }
        }

        private static void DrawReferencePoint(Vector2 position, Color color, float size)
        {
            float halfSize = size * 0.5f;
            Widgets.DrawBoxSolid(new Rect(position.x - halfSize, position.y - halfSize, size, size), color);
        }

        private float GetPreviewPixelsPerUnit(Rect previewRect)
        {
            return (Mathf.Min(previewRect.width, previewRect.height) / 1.5f) * previewZoom;
        }


        private void DrawSelectedLayerHighlight(Rect previewRect)
        {
            if (selectedLayerIndex < 0 || selectedLayerIndex >= workingSkin.layers.Count) return;
            var layer = workingSkin.layers[selectedLayerIndex];
            if (!layer.visible) return;

            Vector2 center = previewRect.center;
            // 估算比例 (需与 TrySelectLayerAt 保持一致)
            float pixelsPerUnit = GetPreviewPixelsPerUnit(previewRect);
            Vector3 displayOffset = GetDisplayedLayerOffsetForPreview(layer);
            Vector2 layerScreenPos = center + new Vector2(displayOffset.x, -displayOffset.z) * pixelsPerUnit;

            // 绘制黄色十字准星
            float size = 10f;
            Color highlightColor = new Color(1f, 0.8f, 0f, 0.6f);

            // 绘制
            GUI.color = highlightColor;
            // 横线
            Widgets.DrawLine(new Vector2(layerScreenPos.x - size, layerScreenPos.y), new Vector2(layerScreenPos.x + size, layerScreenPos.y), highlightColor, 2f);
            // 竖线
            Widgets.DrawLine(new Vector2(layerScreenPos.x, layerScreenPos.y - size), new Vector2(layerScreenPos.x, layerScreenPos.y + size), highlightColor, 2f);
            // 中心点
            Widgets.DrawBoxSolid(new Rect(layerScreenPos.x - 2, layerScreenPos.y - 2, 4, 4), highlightColor);

            GUI.color = Color.white;
        }

        private void DrawSelectedEquipmentPivotOverlay(Rect previewRect)
        {
            if (currentTab != EditorTab.Items)
            {
                return;
            }

            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                return;
            }

            CharacterEquipmentDef? equipment = workingSkin.equipments[selectedEquipmentIndex];
            CharacterEquipmentRenderData? renderData = equipment?.renderData;
            if (renderData == null)
            {
                return;
            }

            EquipmentTriggeredAnimationOverride animationState = ResolveSelectedEquipmentAnimationState(renderData);
            if (!animationState.useTriggeredLocalAnimation)
            {
                return;
            }

            float pixelsPerUnit = GetPreviewPixelsPerUnit(previewRect);
            Vector2 pivotScreenPos = GetSelectedEquipmentPivotScreenPosition(previewRect, renderData, animationState, pixelsPerUnit);
            float ringRadius = equipmentPivotEditMode ? 11f : 9f;
            Color pivotColor = equipmentPivotEditMode
                ? new Color(0.3f, 1f, 0.92f, 0.95f)
                : new Color(1f, 0.84f, 0.22f, 0.9f);

            Widgets.DrawLine(new Vector2(pivotScreenPos.x - ringRadius, pivotScreenPos.y), new Vector2(pivotScreenPos.x + ringRadius, pivotScreenPos.y), pivotColor, 2f);
            Widgets.DrawLine(new Vector2(pivotScreenPos.x, pivotScreenPos.y - ringRadius), new Vector2(pivotScreenPos.x, pivotScreenPos.y + ringRadius), pivotColor, 2f);
            Widgets.DrawBoxSolid(new Rect(pivotScreenPos.x - 3f, pivotScreenPos.y - 3f, 6f, 6f), pivotColor);

            Rect hintRect = new Rect(pivotScreenPos.x + 10f, pivotScreenPos.y - 10f, 180f, 20f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Tiny;
            GUI.color = pivotColor;
            Widgets.Label(hintRect, equipmentPivotEditMode ? "枢轴编辑中：拖拽十字光标" : "触发动画枢轴预览");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private Vector2 GetSelectedEquipmentPivotScreenPosition(Rect previewRect, CharacterEquipmentRenderData renderData, float pixelsPerUnit)
        {
            return GetSelectedEquipmentPivotScreenPosition(previewRect, renderData, ResolveSelectedEquipmentAnimationState(renderData), pixelsPerUnit);
        }

        private Vector2 GetSelectedEquipmentPivotScreenPosition(Rect previewRect, CharacterEquipmentRenderData renderData, EquipmentTriggeredAnimationOverride animationState, float pixelsPerUnit)
        {
            Vector3 layerOffset = GetDisplayedLayerOffsetForPreview(renderData);
            Vector2 pivot = animationState.triggeredPivotOffset;
            Vector3 worldPivotOffset = new Vector3(layerOffset.x + pivot.x, layerOffset.y, layerOffset.z + pivot.y);
            return previewRect.center + new Vector2(worldPivotOffset.x, -worldPivotOffset.z) * pixelsPerUnit;
        }

        private bool TryGetSelectedEquipmentPivotData(Rect previewRect, out CharacterEquipmentRenderData? renderData, out Vector2 pivotScreenPos)
        {
            renderData = null;
            pivotScreenPos = Vector2.zero;

            if (currentTab != EditorTab.Items)
            {
                return false;
            }

            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                return false;
            }

            CharacterEquipmentDef? equipment = workingSkin.equipments[selectedEquipmentIndex];
            renderData = equipment?.renderData;
            if (renderData == null)
            {
                renderData = null;
                return false;
            }

            EquipmentTriggeredAnimationOverride animationState = ResolveSelectedEquipmentAnimationState(renderData);
            if (!animationState.useTriggeredLocalAnimation)
            {
                renderData = null;
                return false;
            }

            float pixelsPerUnit = GetPreviewPixelsPerUnit(previewRect);
            pivotScreenPos = GetSelectedEquipmentPivotScreenPosition(previewRect, renderData, animationState, pixelsPerUnit);
            return true;
        }

        private bool IsMouseOverSelectedEquipmentPivotHandle(Rect previewRect, Vector2 mousePos)
        {
            if (!TryGetSelectedEquipmentPivotData(previewRect, out _, out Vector2 pivotScreenPos))
            {
                return false;
            }

            float hitRadius = equipmentPivotEditMode ? 16f : 12f;
            return Vector2.Distance(mousePos, pivotScreenPos) <= hitRadius;
        }

        private void UpdateEquipmentPivotDragState(Rect previewRect)
        {
            Event evt = Event.current;
            if (!equipmentPivotEditMode || currentTab != EditorTab.Items)
            {
                isDraggingEquipmentPivot = false;
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && Mouse.IsOver(previewRect))
            {
                isDraggingEquipmentPivot = IsMouseOverSelectedEquipmentPivotHandle(previewRect, evt.mousePosition);
                if (isDraggingEquipmentPivot)
                {
                    evt.Use();
                }
            }
            else if ((evt.type == EventType.MouseUp && evt.button == 0) || evt.rawType == EventType.MouseUp)
            {
                isDraggingEquipmentPivot = false;
            }
        }

        private bool TryApplyDragToSelectedEquipmentPivot(Rect previewRect)
        {
            if (!equipmentPivotEditMode || currentTab != EditorTab.Items || !isDraggingEquipmentPivot)
            {
                return false;
            }

            if (!TryGetSelectedEquipmentPivotData(previewRect, out CharacterEquipmentRenderData? renderData, out _)
                || renderData == null)
            {
                isDraggingEquipmentPivot = false;
                return false;
            }

            Event evt = Event.current;
            if (evt.type != EventType.MouseDrag || evt.button != 0 || !Mouse.IsOver(previewRect))
            {
                return false;
            }

            float sensitivity = 0.0025f / Mathf.Max(0.25f, previewZoom);
            if (evt.shift)
            {
                sensitivity *= 4f;
            }
            if (evt.control)
            {
                sensitivity *= 0.35f;
            }

            Vector2 delta = evt.delta;
            EquipmentTriggeredAnimationOverride animationState = ResolveSelectedEquipmentAnimationState(renderData);
            Vector2 pivot = animationState.triggeredPivotOffset;
            pivot.x += delta.x * sensitivity;
            pivot.y += -delta.y * sensitivity;
            animationState.triggeredPivotOffset = pivot;
            isDirty = true;
            RefreshPreview();
            evt.Use();
            return true;
        }

        private void HandlePreviewInput(Rect rect)
        {
            UpdatePreviewEquipmentAnimation();
            UpdateEquipmentPivotDragState(rect);

            if (Mouse.IsOver(rect))
            {
                // 滚轮缩放
                if (Event.current.type == EventType.ScrollWheel)
                {
                    float delta = Event.current.delta.y;
                    if (delta > 0) previewZoom /= 1.1f;
                    else previewZoom *= 1.1f;
                    previewZoom = Mathf.Clamp(previewZoom, 0.1f, 5f);
                    Event.current.Use();
                }

                // Shift + 左键点击选中 (已根据反馈移除，但为了代码完整性保留方法定义，此处注释掉调用)
                /*
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.shift)
                {
                    TrySelectLayerAt(Event.current.mousePosition, rect);
                    Event.current.Use();
                }
                */

                // 鼠标拖拽调整偏移（支持多选同步；基础槽位与图层保持一致；Shift 加速，Ctrl 精调）
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
                {
                    if (TryApplyDragToSelectedEquipmentPivot(rect))
                    {
                        return;
                    }

                    Vector2 delta = Event.current.delta;

                    float sensitivity = 0.0025f / Mathf.Max(0.25f, previewZoom);
                    if (Event.current.shift)
                    {
                        sensitivity *= 4f;
                    }
                    if (Event.current.control)
                    {
                        sensitivity *= 0.35f;
                    }

                    float dx = delta.x * sensitivity;
                    float dz = -delta.y * sensitivity; // 屏幕Y向下，世界Z向上

                    if (TryApplyDragToWeaponPreview(dx, dz))
                    {
                        Event.current.Use();
                    }
                    else if (TryApplyDragToSelectedEquipmentPreview(dx, dz))
                    {
                        Event.current.Use();
                    }
                    else
                    {
                        var targets = GetSelectedLayerTargets();
                        if (targets.Count > 0)
                        {
                            foreach (int idx in targets)
                            {
                                ApplyPreviewDeltaToLayer(workingSkin.layers[idx], dx, dz);
                            }

                            isDirty = true;
                            RefreshPreview();
                            Event.current.Use();
                        }
                        else if (TryApplyDragToSelectedBaseSlot(dx, dz))
                        {
                            Event.current.Use();
                        }
                    }
                }
            }

            // 方向键微调（图层与基础槽位都可用；Shift 加速，Ctrl 精调）
            if (Event.current.type == EventType.KeyDown)
            {
                if (GUIUtility.keyboardControl != 0 && !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
                {
                    return;
                }

                float step = 0.001f;
                if (Event.current.shift)
                {
                    step *= 5f;
                }
                if (Event.current.control)
                {
                    step *= 0.25f;
                }

                float dx = 0f;
                float dz = 0f;
                bool handled = true;
                switch (Event.current.keyCode)
                {
                    case KeyCode.LeftArrow: dx = -step; break;
                    case KeyCode.RightArrow: dx = step; break;
                    case KeyCode.UpArrow: dz = step; break;
                    case KeyCode.DownArrow: dz = -step; break;
                    default: handled = false; break;
                }

                if (handled)
                {
                    if (TryApplyNudgeToWeaponPreview(dx, dz))
                    {
                        Event.current.Use();
                    }
                    else if (TryApplyNudgeToSelectedEquipmentPreview(dx, dz))
                    {
                        Event.current.Use();
                    }
                    else
                    {
                        var targets = GetSelectedLayerTargets();
                        if (targets.Count > 0)
                        {
                            foreach (int idx in targets)
                            {
                                ApplyPreviewDeltaToLayer(workingSkin.layers[idx], dx, dz);
                            }

                            isDirty = true;
                            RefreshPreview();
                            Event.current.Use();
                        }
                        else if (TryApplyNudgeToSelectedBaseSlot(dx, dz))
                        {
                            Event.current.Use();
                        }
                    }
                }
            }
        }

        private void ApplyPreviewDeltaToLayer(PawnLayerConfig layer, float dx, float dz)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.offset.x += dx;
                layer.offset.z += dz;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.offsetNorth.x += dx;
                layer.offsetNorth.z += dz;
                return;
            }

            if (previewRotation == Rot4.West)
            {
                if (layer.useWestOffset)
                {
                    layer.offsetWest.x += dx;
                    layer.offsetWest.z += dz;
                }
                else
                {
                    layer.offsetEast.x -= dx;
                    layer.offsetEast.z += dz;
                }
                return;
            }

            layer.offsetEast.x += dx;
            layer.offsetEast.z += dz;
        }

        private void ApplyPreviewDeltaToLayer(CharacterEquipmentRenderData layer, float dx, float dz)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.offset.x += dx;
                layer.offset.z += dz;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.offsetNorth.x += dx;
                layer.offsetNorth.z += dz;
                return;
            }

            if (previewRotation == Rot4.West)
            {
                layer.offsetEast.x -= dx;
                layer.offsetEast.z += dz;
                return;
            }

            layer.offsetEast.x += dx;
            layer.offsetEast.z += dz;
        }

        private bool TryApplyDragToWeaponPreview(float dx, float dz)
        {
            if (currentTab != EditorTab.Animation)
            {
                return false;
            }

            workingSkin.animationConfig ??= new PawnAnimationConfig();
            workingSkin.animationConfig.carryVisual ??= new WeaponCarryVisualConfig();
            ApplyPreviewDeltaToWeaponConfig(workingSkin.animationConfig.carryVisual, dx, dz);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool TryApplyNudgeToWeaponPreview(float dx, float dz)
        {
            if (currentTab != EditorTab.Animation)
            {
                return false;
            }

            workingSkin.animationConfig ??= new PawnAnimationConfig();
            workingSkin.animationConfig.carryVisual ??= new WeaponCarryVisualConfig();
            ApplyPreviewDeltaToWeaponConfig(workingSkin.animationConfig.carryVisual, dx, dz);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool TryApplyDragToSelectedEquipmentPreview(float dx, float dz)
        {
            if (currentTab != EditorTab.Items)
            {
                return false;
            }

            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                return false;
            }

            var equipment = workingSkin.equipments[selectedEquipmentIndex] ?? new CharacterEquipmentDef();
            equipment.EnsureDefaults();
            workingSkin.equipments[selectedEquipmentIndex] = equipment;

            ApplyPreviewDeltaToLayer(equipment.renderData, dx, dz);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool TryApplyNudgeToSelectedEquipmentPreview(float dx, float dz)
        {
            if (currentTab != EditorTab.Items)
            {
                return false;
            }

            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                return false;
            }

            var equipment = workingSkin.equipments[selectedEquipmentIndex] ?? new CharacterEquipmentDef();
            equipment.EnsureDefaults();
            workingSkin.equipments[selectedEquipmentIndex] = equipment;

            ApplyPreviewDeltaToLayer(equipment.renderData, dx, dz);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private void ApplyPreviewDeltaToWeaponConfig(WeaponCarryVisualConfig config, float dx, float dz)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                config.offset.x += dx;
                config.offset.z += dz;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                config.offsetNorth.x += dx;
                config.offsetNorth.z += dz;
                return;
            }

            config.offsetEast.x += dx;
            config.offsetEast.z += dz;
        }

        private Vector3 GetLayerOffsetForRotation(PawnLayerConfig layer, Rot4 rotation)
        {
            if (rotation == Rot4.South) return layer.offset;
            if (rotation == Rot4.North) return layer.offsetNorth;
            if (rotation == Rot4.West)
            {
                if (layer.useWestOffset || editLayerOffsetPerFacing)
                {
                    if (layer.useWestOffset) return layer.offsetWest;
                    Vector3 mirror = layer.offsetEast;
                    mirror.x = -mirror.x;
                    return mirror;
                }
                Vector3 m = layer.offsetEast;
                m.x = -m.x;
                return m;
            }
            return layer.offsetEast;
        }

        private void SetLayerOffsetForRotation(PawnLayerConfig layer, Rot4 rotation, float? newOffsetX = null, float? newOffsetY = null, float? newOffsetZ = null)
        {
            if (rotation == Rot4.South)
            {
                if (newOffsetX.HasValue) layer.offset.x = newOffsetX.Value;
                if (newOffsetY.HasValue) layer.offset.y = newOffsetY.Value;
                if (newOffsetZ.HasValue) layer.offset.z = newOffsetZ.Value;
            }
            else if (rotation == Rot4.North)
            {
                if (newOffsetX.HasValue) layer.offsetNorth.x = newOffsetX.Value;
                if (newOffsetY.HasValue) layer.offsetNorth.y = newOffsetY.Value;
                if (newOffsetZ.HasValue) layer.offsetNorth.z = newOffsetZ.Value;
            }
            else if (rotation == Rot4.West)
            {
                if (layer.useWestOffset || editLayerOffsetPerFacing)
                {
                    if (!layer.useWestOffset)
                    {
                        layer.useWestOffset = true;
                        layer.offsetWest = new Vector3(-layer.offsetEast.x, layer.offsetEast.y, layer.offsetEast.z);
                    }
                    if (newOffsetX.HasValue) layer.offsetWest.x = newOffsetX.Value;
                    if (newOffsetY.HasValue) layer.offsetWest.y = newOffsetY.Value;
                    if (newOffsetZ.HasValue) layer.offsetWest.z = newOffsetZ.Value;
                }
                else
                {
                    if (newOffsetX.HasValue) layer.offsetEast.x = -newOffsetX.Value;
                    if (newOffsetY.HasValue) layer.offsetEast.y = newOffsetY.Value;
                    if (newOffsetZ.HasValue) layer.offsetEast.z = newOffsetZ.Value;
                }
            }
            else
            {
                if (newOffsetX.HasValue) layer.offsetEast.x = newOffsetX.Value;
                if (newOffsetY.HasValue) layer.offsetEast.y = newOffsetY.Value;
                if (newOffsetZ.HasValue) layer.offsetEast.z = newOffsetZ.Value;
            }
        }

        private Vector3 GetEditableLayerOffsetForPreview(PawnLayerConfig layer)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return layer.offset;

            if (previewRotation == Rot4.North)
                return layer.offsetNorth;

            if (previewRotation == Rot4.West)
            {
                if (layer.useWestOffset)
                    return layer.offsetWest;
                
                Vector3 eastMirror = layer.offsetEast;
                eastMirror.x = -eastMirror.x;
                return eastMirror;
            }

            return layer.offsetEast;
        }

        private Vector3 GetEditableLayerOffsetForPreview(CharacterEquipmentRenderData layer)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return layer.offset;

            if (previewRotation == Rot4.North)
                return layer.offsetNorth;

            Vector3 offset = layer.offsetEast;
            if (previewRotation == Rot4.West)
                offset.x = -offset.x;

            return offset;
        }

        private void SetEditableLayerOffsetForPreview(PawnLayerConfig layer, Vector3 value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.offset = value;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.offsetNorth = value;
                return;
            }

            if (previewRotation == Rot4.West)
            {
                if (layer.useWestOffset)
                {
                    layer.offsetWest = value;
                }
                else
                {
                    value.x = -value.x;
                    layer.offsetEast = value;
                }
                return;
            }

            layer.offsetEast = value;
        }

        private void SetEditableLayerOffsetForPreview(CharacterEquipmentRenderData layer, Vector3 value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.offset = value;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.offsetNorth = value;
                return;
            }

            if (previewRotation == Rot4.West)
                value.x = -value.x;

            layer.offsetEast = value;
        }

        private Vector2 GetEditableLayerScaleForPreview(PawnLayerConfig layer)
        {
            if (editLayerOffsetPerFacing)
            {
                if (previewRotation == Rot4.East)
                    return new Vector2(layer.scale.x * layer.scaleEastMultiplier.x, layer.scale.y * layer.scaleEastMultiplier.y);
                
                if (previewRotation == Rot4.West)
                {
                    var mult = layer.useWestOffset ? layer.scaleWestMultiplier : layer.scaleEastMultiplier;
                    return new Vector2(layer.scale.x * mult.x, layer.scale.y * mult.y);
                }
            }

            return layer.scale;
        }

        private Vector2 GetEditableLayerScaleForPreview(CharacterEquipmentRenderData layer)
        {
            if (editLayerOffsetPerFacing && (previewRotation == Rot4.East || previewRotation == Rot4.West))
                return new Vector2(layer.scale.x * layer.scaleEastMultiplier.x, layer.scale.y * layer.scaleEastMultiplier.y);

            return layer.scale;
        }

        private void SetEditableLayerScaleForPreview(PawnLayerConfig layer, Vector2 value)
        {
            if (editLayerOffsetPerFacing)
            {
                if (previewRotation == Rot4.East)
                {
                    layer.scaleEastMultiplier = new Vector2(layer.scale.x != 0f ? value.x / layer.scale.x : 1f, layer.scale.y != 0f ? value.y / layer.scale.y : 1f);
                    return;
                }

                if (previewRotation == Rot4.West)
                {
                    Vector2 mult = new Vector2(layer.scale.x != 0f ? value.x / layer.scale.x : 1f, layer.scale.y != 0f ? value.y / layer.scale.y : 1f);
                    if (layer.useWestOffset)
                        layer.scaleWestMultiplier = mult;
                    else
                        layer.scaleEastMultiplier = mult;
                    return;
                }
            }

            layer.scale = value;
            layer.scaleNorthMultiplier = Vector2.one;
        }

        private void SetEditableLayerScaleForPreview(CharacterEquipmentRenderData layer, Vector2 value)
        {
            if (editLayerOffsetPerFacing && (previewRotation == Rot4.East || previewRotation == Rot4.West))
            {
                layer.scaleEastMultiplier = new Vector2(layer.scale.x != 0f ? value.x / layer.scale.x : 1f, layer.scale.y != 0f ? value.y / layer.scale.y : 1f);
                return;
            }

            layer.scale = value;
            layer.scaleNorthMultiplier = Vector2.one;
        }

        private float GetEditableLayerRotationForPreview(PawnLayerConfig layer)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return layer.rotation;

            if (previewRotation == Rot4.North)
                return layer.rotation + layer.rotationNorthOffset;

            if (previewRotation == Rot4.West)
            {
                if (layer.useWestOffset)
                    return layer.rotation + layer.rotationWestOffset;
                return layer.rotation - layer.rotationEastOffset;
            }

            return layer.rotation + layer.rotationEastOffset;
        }

        private float GetEditableLayerRotationForPreview(CharacterEquipmentRenderData layer)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return layer.rotation;

            if (previewRotation == Rot4.North)
                return layer.rotation + layer.rotationNorthOffset;

            return previewRotation == Rot4.West ? layer.rotation - layer.rotationEastOffset : layer.rotation + layer.rotationEastOffset;
        }

        private void SetEditableLayerRotationForPreview(PawnLayerConfig layer, float value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.rotation = value;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.rotationNorthOffset = value - layer.rotation;
                return;
            }

            if (previewRotation == Rot4.West)
            {
                if (layer.useWestOffset)
                    layer.rotationWestOffset = value - layer.rotation;
                else
                    layer.rotationEastOffset = layer.rotation - value;
                return;
            }

            layer.rotationEastOffset = value - layer.rotation;
        }

        private void SetEditableLayerRotationForPreview(CharacterEquipmentRenderData layer, float value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
            {
                layer.rotation = value;
                return;
            }

            if (previewRotation == Rot4.North)
            {
                layer.rotationNorthOffset = value - layer.rotation;
                return;
            }

            layer.rotationEastOffset = previewRotation == Rot4.West ? layer.rotation - value : value - layer.rotation;
        }

        private Vector3 GetDisplayedLayerOffsetForPreview(PawnLayerConfig layer)
        {
            Vector3 offset = layer.offset;

            if (previewRotation == Rot4.North)
            {
                offset += layer.offsetNorth;
            }
            else if (previewRotation == Rot4.East)
            {
                offset += layer.offsetEast;
            }
            else if (previewRotation == Rot4.West)
            {
                if (layer.useWestOffset)
                {
                    offset += layer.offsetWest;
                }
                else
                {
                    Vector3 eastMirror = layer.offsetEast;
                    eastMirror.x = -eastMirror.x;
                    offset += eastMirror;
                }
            }

            return offset;
        }

        private Vector3 GetDisplayedLayerOffsetForPreview(CharacterEquipmentRenderData layer)
        {
            Vector3 offset = layer.offset;

            if (previewRotation == Rot4.North)
            {
                offset += layer.offsetNorth;
            }
            else if (previewRotation == Rot4.East || previewRotation == Rot4.West)
            {
                Vector3 eastOffset = layer.offsetEast;
                if (previewRotation == Rot4.West)
                {
                    eastOffset.x = -eastOffset.x;
                }

                offset += eastOffset;
            }

            return offset;
        }

        private void DrawPreviewHintsOverlay(Rect previewRect)
        {
            string[] hints =
            {
                GetPreviewHintLabel("Zoom"),
                GetPreviewHintLabel("Drag"),
                GetPreviewHintLabel("Nudge"),
                GetPreviewHintLabel("Boost"),
                GetPreviewHintLabel("ReferenceGhost")
            };

            float lineHeight = 18f;
            float padding = 8f;
            float width = 124f;
            float height = padding * 2f + hints.Length * lineHeight;

            Rect hintRect = new Rect(
                previewRect.x + 10f,
                previewRect.yMax - height - 10f,
                width,
                height);

            Widgets.DrawBoxSolid(hintRect, new Color(0f, 0f, 0f, 0.18f));

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            for (int i = 0; i < hints.Length; i++)
            {
                Rect lineRect = new Rect(
                    hintRect.x + padding,
                    hintRect.y + padding + i * lineHeight,
                    hintRect.width - padding * 2f,
                    lineHeight);

                GUI.color = new Color(1f, 1f, 1f, 0.58f);
                Widgets.Label(lineRect, hints[i]);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;
        }

        private bool TryApplyDragToSelectedBaseSlot(float dx, float dz)
        {
            if (selectedBaseSlotType == null)
            {
                return false;
            }

            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            var slot = workingSkin.baseAppearance.GetSlot(selectedBaseSlotType.Value);
            Vector3 offset = GetEditableOffsetForPreview(slot);
            offset.x += dx;
            offset.z += dz;
            SetEditableOffsetForPreview(slot, offset);

            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool TryApplyNudgeToSelectedBaseSlot(float dx, float dz)
        {
            if (selectedBaseSlotType == null)
            {
                return false;
            }

            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            var slot = workingSkin.baseAppearance.GetSlot(selectedBaseSlotType.Value);
            Vector3 offset = GetEditableOffsetForPreview(slot);
            offset.x += dx;
            offset.z += dz;
            SetEditableOffsetForPreview(slot, offset);

            isDirty = true;
            RefreshPreview();
            return true;
        }

        private Vector3 GetEditableOffsetForPreview(BaseAppearanceSlotConfig slot)
        {
            if (previewRotation == Rot4.North)
            {
                return slot.offsetNorth;
            }

            if (previewRotation == Rot4.East || previewRotation == Rot4.West)
            {
                return slot.offsetEast;
            }

            return slot.offset;
        }

        private void SetEditableOffsetForPreview(BaseAppearanceSlotConfig slot, Vector3 value)
        {
            if (previewRotation == Rot4.North)
            {
                slot.offsetNorth = value;
                return;
            }

            if (previewRotation == Rot4.East || previewRotation == Rot4.West)
            {
                slot.offsetEast = value;
                return;
            }

            slot.offset = value;
        }

        private Vector2 GetEditableSlotScaleForPreview(BaseAppearanceSlotConfig slot)
        {
            if (editLayerOffsetPerFacing && (previewRotation == Rot4.East || previewRotation == Rot4.West))
                return new Vector2(slot.scale.x * slot.scaleEastMultiplier.x, slot.scale.y * slot.scaleEastMultiplier.y);

            return slot.scale;
        }

        private void SetEditableSlotScaleForPreview(BaseAppearanceSlotConfig slot, Vector2 value)
        {
            if (editLayerOffsetPerFacing && (previewRotation == Rot4.East || previewRotation == Rot4.West))
            {
                slot.scaleEastMultiplier = new Vector2(slot.scale.x != 0f ? value.x / slot.scale.x : 1f, slot.scale.y != 0f ? value.y / slot.scale.y : 1f);
                return;
            }

            slot.scale = value;
            slot.scaleNorthMultiplier = Vector2.one;
        }

        private float GetEditableSlotRotationForPreview(BaseAppearanceSlotConfig slot)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South)
                return slot.rotation;

            if (previewRotation == Rot4.North)
                return slot.rotation + slot.rotationNorthOffset;

            return previewRotation == Rot4.West ? slot.rotation - slot.rotationEastOffset : slot.rotation + slot.rotationEastOffset;
        }

        private void SetEditableSlotRotationForPreview(BaseAppearanceSlotConfig slot, float value)
        {
            if (!editLayerOffsetPerFacing || previewRotation == Rot4.South) { slot.rotation = value; return; }
            if (previewRotation == Rot4.North) { slot.rotationNorthOffset = value - slot.rotation; return; }
            slot.rotationEastOffset = previewRotation == Rot4.West ? slot.rotation - value : value - slot.rotation;
        }

        private void TrySelectLayerAt(Vector2 mousePos, Rect previewRect)
        {
            // 简单的距离检测选中算法
            // 假设 Pawn 在预览框中心
            Vector2 center = previewRect.center;

            // 估算世界单位到屏幕像素的转换比例
            // 假设标准 Pawn 高度约 1.8 单位，占满预览框时 height 对应 1.8
            // 实际上还要考虑 previewZoom
            float pixelsPerUnit = GetPreviewPixelsPerUnit(previewRect);
            float minDistance = 30f; // 选中阈值 (像素)
            int bestIndex = -1;

            for (int i = 0; i < workingSkin.layers.Count; i++)
            {
                var layer = workingSkin.layers[i];
                if (!layer.visible) continue;

                // 计算图层的大致屏幕位置
                // 注意：这里只考虑了 Offset，没有考虑 Body/Head 等锚点的基础位置
                // 这是一个简化的估算，假设用户主要通过 Offset 调整位置
                Vector3 displayOffset = GetDisplayedLayerOffsetForPreview(layer);
                Vector2 layerScreenPos = center + new Vector2(displayOffset.x, -displayOffset.z) * pixelsPerUnit;

                float dist = Vector2.Distance(mousePos, layerScreenPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestIndex = i;
                }
            }

            if (bestIndex != -1)
            {
                selectedLayerIndex = bestIndex;
                selectedNodePath = ""; // 清除节点选择
                // 自动滚动到列表位置
                // layerScrollPos.y = bestIndex * TreeNodeHeight; // 简单估算
                ShowStatus("CS_Studio_Msg_SelectedLayer".Translate(workingSkin.layers[bestIndex].layerName));
            }
        }
    }
}
