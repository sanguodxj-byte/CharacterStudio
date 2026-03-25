using System;
using CharacterStudio.Abilities;
using CharacterStudio.Attributes;
using CharacterStudio.Performance;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 皮肤组件
    /// 附加到 Pawn 上，处理皮肤应用和动态表情逻辑
    /// </summary>
    public class CompPawnSkin : ThingComp
    {
        private PawnSkinDef? activeSkin;
        private string? activeSkinDefName;
        private bool needsRefresh = false;
        private bool activeSkinFromDefaultRaceBinding = false;
        private bool activeSkinPreviewMode = false;
        private string activeSkinApplicationSource = string.Empty;
        private CharacterAbilityLoadout? activeAbilityLoadout;

        // 双轨面部运行时状态（第一阶段：先接入状态层，不直接改动渲染 worker）
        private FaceRuntimeState? faceRuntimeState = null;
        private FaceRuntimeCompiledData? faceRuntimeCompiledData = null;

        // 表情状态
        private readonly FaceExpressionRuntimeState faceExpressionState = new FaceExpressionRuntimeState();
        private ExpressionType curExpression
        {
            get => faceExpressionState.currentExpression;
            set => faceExpressionState.currentExpression = value;
        }

        private readonly FacePreviewOverrideState previewOverrides = new FacePreviewOverrideState();
        private const int BlinkDuration = 10;

        // 眼睛注视方向
        private readonly EyeDirectionRuntimeState eyeDirectionState = new EyeDirectionRuntimeState();
        private EyeDirection curEyeDirection
        {
            get => eyeDirectionState.currentEyeDirection;
            set => eyeDirectionState.currentEyeDirection = value;
        }

        private readonly AbilityHotkeyRuntimeState abilityRuntimeState = new AbilityHotkeyRuntimeState();

        // Q 键四段轮换模式索引（0..3）
        public int qHotkeyModeIndex
        {
            get => abilityRuntimeState.qHotkeyModeIndex;
            set => abilityRuntimeState.qHotkeyModeIndex = value;
        }

        // Q->W 连段窗口（单位：Tick）
        public int qComboWindowEndTick
        {
            get => abilityRuntimeState.qComboWindowEndTick;
            set => abilityRuntimeState.qComboWindowEndTick = value;
        }

        public string qOverrideAbilityDefName
        {
            get => abilityRuntimeState.qOverrideAbilityDefName;
            set => abilityRuntimeState.qOverrideAbilityDefName = value ?? string.Empty;
        }

        public int qOverrideExpireTick
        {
            get => abilityRuntimeState.qOverrideExpireTick;
            set => abilityRuntimeState.qOverrideExpireTick = value;
        }

        public string wOverrideAbilityDefName
        {
            get => abilityRuntimeState.wOverrideAbilityDefName;
            set => abilityRuntimeState.wOverrideAbilityDefName = value ?? string.Empty;
        }

        public int wOverrideExpireTick
        {
            get => abilityRuntimeState.wOverrideExpireTick;
            set => abilityRuntimeState.wOverrideExpireTick = value;
        }

        public string eOverrideAbilityDefName
        {
            get => abilityRuntimeState.eOverrideAbilityDefName;
            set => abilityRuntimeState.eOverrideAbilityDefName = value ?? string.Empty;
        }

        public int eOverrideExpireTick
        {
            get => abilityRuntimeState.eOverrideExpireTick;
            set => abilityRuntimeState.eOverrideExpireTick = value;
        }

        public string rOverrideAbilityDefName
        {
            get => abilityRuntimeState.rOverrideAbilityDefName;
            set => abilityRuntimeState.rOverrideAbilityDefName = value ?? string.Empty;
        }

        public int rOverrideExpireTick
        {
            get => abilityRuntimeState.rOverrideExpireTick;
            set => abilityRuntimeState.rOverrideExpireTick = value;
        }

        // 各槽位技能 CD（单位：Tick）
        // E 槽由 AbilityHotkeyRuntimeComponent 写入，Q/W/R 由统一门控写入
        public int qCooldownUntilTick
        {
            get => abilityRuntimeState.qCooldownUntilTick;
            set => abilityRuntimeState.qCooldownUntilTick = value;
        }

        public int wCooldownUntilTick
        {
            get => abilityRuntimeState.wCooldownUntilTick;
            set => abilityRuntimeState.wCooldownUntilTick = value;
        }

        public int eCooldownUntilTick
        {
            get => abilityRuntimeState.eCooldownUntilTick;
            set => abilityRuntimeState.eCooldownUntilTick = value;
        }

        public int rCooldownUntilTick
        {
            get => abilityRuntimeState.rCooldownUntilTick;
            set => abilityRuntimeState.rCooldownUntilTick = value;
        }

        // R 两段机制状态
        public bool rStackingEnabled
        {
            get => abilityRuntimeState.rStackingEnabled;
            set => abilityRuntimeState.rStackingEnabled = value;
        }

        public int rStackCount
        {
            get => abilityRuntimeState.rStackCount;
            set => abilityRuntimeState.rStackCount = value;
        }

        public bool rSecondStageReady
        {
            get => abilityRuntimeState.rSecondStageReady;
            set => abilityRuntimeState.rSecondStageReady = value;
        }

        public int rSecondStageExecuteTick
        {
            get => abilityRuntimeState.rSecondStageExecuteTick;
            set => abilityRuntimeState.rSecondStageExecuteTick = value;
        }

        public bool rSecondStageHasTarget
        {
            get => abilityRuntimeState.rSecondStageHasTarget;
            set => abilityRuntimeState.rSecondStageHasTarget = value;
        }

        public IntVec3 rSecondStageTargetCell
        {
            get => abilityRuntimeState.rSecondStageTargetCell;
            set => abilityRuntimeState.rSecondStageTargetCell = value;
        }

        // 状态武器视觉：施法中窗口
        public int weaponCarryCastingUntilTick
        {
            get => abilityRuntimeState.weaponCarryCastingUntilTick;
            set => abilityRuntimeState.weaponCarryCastingUntilTick = value;
        }

        // 周期脉冲运行时状态
        public int periodicPulseNextTick
        {
            get => abilityRuntimeState.periodicPulseNextTick;
            set => abilityRuntimeState.periodicPulseNextTick = value;
        }

        public int periodicPulseEndTick
        {
            get => abilityRuntimeState.periodicPulseEndTick;
            set => abilityRuntimeState.periodicPulseEndTick = value;
        }

        // 护盾吸收运行时状态
        public float shieldRemainingDamage
        {
            get => abilityRuntimeState.shieldRemainingDamage;
            set => abilityRuntimeState.shieldRemainingDamage = value;
        }

        public int shieldExpireTick
        {
            get => abilityRuntimeState.shieldExpireTick;
            set => abilityRuntimeState.shieldExpireTick = value;
        }

        public float shieldStoredHeal
        {
            get => abilityRuntimeState.shieldStoredHeal;
            set => abilityRuntimeState.shieldStoredHeal = value;
        }

        public float shieldStoredBonusDamage
        {
            get => abilityRuntimeState.shieldStoredBonusDamage;
            set => abilityRuntimeState.shieldStoredBonusDamage = value;
        }

        public int offensiveMarkExpireTick
        {
            get => abilityRuntimeState.offensiveMarkExpireTick;
            set => abilityRuntimeState.offensiveMarkExpireTick = value;
        }

        public int offensiveMarkStacks
        {
            get => abilityRuntimeState.offensiveMarkStacks;
            set => abilityRuntimeState.offensiveMarkStacks = value;
        }

        public int offensiveComboExpireTick
        {
            get => abilityRuntimeState.offensiveComboExpireTick;
            set => abilityRuntimeState.offensiveComboExpireTick = value;
        }

        public int offensiveComboStacks
        {
            get => abilityRuntimeState.offensiveComboStacks;
            set => abilityRuntimeState.offensiveComboStacks = value;
        }

        public int dashEmpowerExpireTick
        {
            get => abilityRuntimeState.dashEmpowerExpireTick;
            set => abilityRuntimeState.dashEmpowerExpireTick = value;
        }

        public int flightStateStartTick
        {
            get => abilityRuntimeState.flightStateStartTick;
            set => abilityRuntimeState.flightStateStartTick = value;
        }

        public int flightStateExpireTick
        {
            get => abilityRuntimeState.flightStateExpireTick;
            set => abilityRuntimeState.flightStateExpireTick = value;
        }

        public float flightStateHeightFactor
        {
            get => abilityRuntimeState.flightStateHeightFactor;
            set => abilityRuntimeState.flightStateHeightFactor = value;
        }

        public bool IsFlightStateActive()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return flightStateExpireTick >= now;
        }

        public string triggeredEquipmentAnimationAbilityDefName
        {
            get => abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName;
            set => abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName = value ?? string.Empty;
        }

        public int triggeredEquipmentAnimationStartTick
        {
            get => abilityRuntimeState.triggeredEquipmentAnimationStartTick;
            set => abilityRuntimeState.triggeredEquipmentAnimationStartTick = value;
        }

        public float GetFlightLiftFactor01()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!IsFlightStateActive() || flightStateStartTick < 0 || flightStateExpireTick < flightStateStartTick)
                return 0f;

            const int easeTicks = 18;
            float fadeIn = Mathf.Clamp01((now - flightStateStartTick) / (float)easeTicks);
            float fadeOut = Mathf.Clamp01((flightStateExpireTick - now) / (float)easeTicks);
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
        }

        public float GetFlightHoverOffset()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!IsFlightStateActive()) return 0f;
            float amplitude = Mathf.Max(0.015f, flightStateHeightFactor * 0.18f) * GetFlightLiftFactor01();
            return Mathf.Sin((now + (Pawn?.thingIDNumber ?? 0)) * 0.14f) * amplitude;
        }

        public int triggeredEquipmentAnimationEndTick
        {
            get => abilityRuntimeState.triggeredEquipmentAnimationEndTick;
            set => abilityRuntimeState.triggeredEquipmentAnimationEndTick = value;
        }

        public bool suppressCombatActionsDuringFlightState
        {
            get => abilityRuntimeState.suppressCombatActionsDuringFlightState;
            set => abilityRuntimeState.suppressCombatActionsDuringFlightState = value;
        }

        public bool isInVanillaFlight
        {
            get => abilityRuntimeState.isInVanillaFlight;
            set => abilityRuntimeState.isInVanillaFlight = value;
        }

        public int vanillaFlightStartTick
        {
            get => abilityRuntimeState.vanillaFlightStartTick;
            set => abilityRuntimeState.vanillaFlightStartTick = value;
        }

        public int vanillaFlightExpireTick
        {
            get => abilityRuntimeState.vanillaFlightExpireTick;
            set => abilityRuntimeState.vanillaFlightExpireTick = value;
        }

        public string vanillaFlightSourceAbilityDefName
        {
            get => abilityRuntimeState.vanillaFlightSourceAbilityDefName;
            set => abilityRuntimeState.vanillaFlightSourceAbilityDefName = value ?? string.Empty;
        }

        public string vanillaFlightFollowupAbilityDefName
        {
            get => abilityRuntimeState.vanillaFlightFollowupAbilityDefName;
            set => abilityRuntimeState.vanillaFlightFollowupAbilityDefName = value ?? string.Empty;
        }

        public IntVec3 vanillaFlightReservedTargetCell
        {
            get => abilityRuntimeState.vanillaFlightReservedTargetCell;
            set => abilityRuntimeState.vanillaFlightReservedTargetCell = value;
        }

        public bool vanillaFlightHasReservedTargetCell
        {
            get => abilityRuntimeState.vanillaFlightHasReservedTargetCell;
            set => abilityRuntimeState.vanillaFlightHasReservedTargetCell = value;
        }

        public int vanillaFlightFollowupWindowEndTick
        {
            get => abilityRuntimeState.vanillaFlightFollowupWindowEndTick;
            set => abilityRuntimeState.vanillaFlightFollowupWindowEndTick = value;
        }

        public bool vanillaFlightPendingLandingBurst
        {
            get => abilityRuntimeState.vanillaFlightPendingLandingBurst;
            set => abilityRuntimeState.vanillaFlightPendingLandingBurst = value;
        }

        public bool IsTriggeredEquipmentAnimationActive(string? abilityDefName)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return triggeredEquipmentAnimationEndTick >= now
                && !string.IsNullOrWhiteSpace(triggeredEquipmentAnimationAbilityDefName)
                && (string.IsNullOrWhiteSpace(abilityDefName)
                    || string.Equals(triggeredEquipmentAnimationAbilityDefName, abilityDefName, StringComparison.OrdinalIgnoreCase));
        }

        public void TriggerEquipmentAnimationState(string triggerKey, int startTick, int durationTicks)
        {
            if (string.IsNullOrWhiteSpace(triggerKey))
            {
                return;
            }

            triggeredEquipmentAnimationAbilityDefName = triggerKey;
            triggeredEquipmentAnimationStartTick = startTick;
            triggeredEquipmentAnimationEndTick = startTick + Math.Max(1, durationTicks);
            RequestRenderRefresh();
        }

        public void ClearEquipmentAnimationState(string? triggerKey = null)
        {
            if (!string.IsNullOrWhiteSpace(triggerKey)
                && !string.Equals(triggeredEquipmentAnimationAbilityDefName, triggerKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            triggeredEquipmentAnimationAbilityDefName = string.Empty;
            triggeredEquipmentAnimationStartTick = -1;
            triggeredEquipmentAnimationEndTick = -1;
            RequestRenderRefresh();
        }

        public int abilityExpressionOverrideExpireTick
        {
            get => abilityRuntimeState.abilityExpressionOverrideExpireTick;
            set => abilityRuntimeState.abilityExpressionOverrideExpireTick = value;
        }

        public ExpressionType? abilityExpressionOverride
        {
            get => abilityRuntimeState.abilityExpressionOverride;
            set => abilityRuntimeState.abilityExpressionOverride = value;
        }

        public float abilityPupilBrightnessOffset
        {
            get => abilityRuntimeState.abilityPupilBrightnessOffset;
            set => abilityRuntimeState.abilityPupilBrightnessOffset = value;
        }

        public float abilityPupilContrastOffset
        {
            get => abilityRuntimeState.abilityPupilContrastOffset;
            set => abilityRuntimeState.abilityPupilContrastOffset = value;
        }

        private sealed class FaceExpressionRuntimeState
        {
            public ExpressionType currentExpression = ExpressionType.Neutral;
            public int blinkTimer = 0;
            public int expressionAnimTick = 0;
            public ExpressionType lastAnimatedExpression = ExpressionType.Neutral;
            public int lastAnimatedFrameIndex = -1;
            public BlinkPhase blinkPhase = BlinkPhase.None;
            public int blinkPhaseTick = 0;
            public int blinkClosingTicks = 2;
            public int blinkHideBaseTicks = 1;
            public int blinkReplacementTicks = 2;
            public int blinkRestoreBaseTicks = 1;
            public int blinkOpeningTicks = 2;

            public bool IsBlinkActive => blinkPhase != BlinkPhase.None;

            public void AdvanceAnimTick()
            {
                expressionAnimTick++;
            }

            public bool ConsumeBlinkTick()
            {
                if (!IsBlinkActive)
                    return false;

                if (blinkTimer > 0)
                    blinkTimer--;

                blinkPhaseTick++;
                if (blinkPhaseTick < GetPhaseDuration(blinkPhase))
                    return false;

                AdvanceBlinkPhase();
                return !IsBlinkActive;
            }

            public void StartBlink(int durationTicks)
            {
                blinkClosingTicks = Math.Max(1, durationTicks / 4);
                blinkHideBaseTicks = 1;
                blinkReplacementTicks = Math.Max(1, durationTicks / 5);
                blinkRestoreBaseTicks = 1;
                blinkOpeningTicks = Math.Max(1, durationTicks - blinkClosingTicks - blinkHideBaseTicks - blinkReplacementTicks - blinkRestoreBaseTicks);

                blinkTimer = blinkClosingTicks + blinkHideBaseTicks + blinkReplacementTicks + blinkRestoreBaseTicks + blinkOpeningTicks;
                blinkPhase = BlinkPhase.ClosingLid;
                blinkPhaseTick = 0;
            }

            public void ClearBlink()
            {
                blinkTimer = 0;
                blinkPhase = BlinkPhase.None;
                blinkPhaseTick = 0;
            }

            public float GetBlinkPhaseProgress01()
            {
                if (!IsBlinkActive)
                    return 0f;

                int duration = GetPhaseDuration(blinkPhase);
                if (duration <= 1)
                    return 1f;

                return Mathf.Clamp01(blinkPhaseTick / (float)(duration - 1));
            }

            private int GetPhaseDuration(BlinkPhase phase)
            {
                return phase switch
                {
                    BlinkPhase.ClosingLid => blinkClosingTicks,
                    BlinkPhase.HideBaseEyeParts => blinkHideBaseTicks,
                    BlinkPhase.ShowReplacementEye => blinkReplacementTicks,
                    BlinkPhase.RestoreBaseEyeParts => blinkRestoreBaseTicks,
                    BlinkPhase.OpeningLid => blinkOpeningTicks,
                    _ => 0,
                };
            }

            private void AdvanceBlinkPhase()
            {
                blinkPhaseTick = 0;
                blinkPhase = blinkPhase switch
                {
                    BlinkPhase.ClosingLid => BlinkPhase.HideBaseEyeParts,
                    BlinkPhase.HideBaseEyeParts => BlinkPhase.ShowReplacementEye,
                    BlinkPhase.ShowReplacementEye => BlinkPhase.RestoreBaseEyeParts,
                    BlinkPhase.RestoreBaseEyeParts => BlinkPhase.OpeningLid,
                    BlinkPhase.OpeningLid => BlinkPhase.None,
                    _ => BlinkPhase.None,
                };
            }

            public void ResetAnimatedFrameTracking()
            {
                lastAnimatedExpression = ExpressionType.Neutral;
                lastAnimatedFrameIndex = -1;
            }
        }

        private sealed class EyeDirectionRuntimeState
        {
            public EyeDirection currentEyeDirection = EyeDirection.Center;

            public bool SetDirection(EyeDirection direction)
            {
                if (currentEyeDirection == direction)
                    return false;

                currentEyeDirection = direction;
                return true;
            }
        }

        private sealed class EffectiveFaceStateSnapshot
        {
            public readonly ExpressionType expression;
            public readonly EyeDirection eyeDirection;
            public readonly MouthState mouthState;
            public readonly LidState lidState;
            public readonly BrowState browState;
            public readonly EmotionOverlayState emotionOverlayState;
            public readonly EyeAnimationVariant eyeVariant;
            public readonly PupilScaleVariant pupilVariant;

            public EffectiveFaceStateSnapshot(
                ExpressionType expression,
                EyeDirection eyeDirection,
                MouthState mouthState,
                LidState lidState,
                BrowState browState,
                EmotionOverlayState emotionOverlayState,
                EyeAnimationVariant eyeVariant,
                PupilScaleVariant pupilVariant)
            {
                this.expression = expression;
                this.eyeDirection = eyeDirection;
                this.mouthState = mouthState;
                this.lidState = lidState;
                this.browState = browState;
                this.emotionOverlayState = emotionOverlayState;
                this.eyeVariant = eyeVariant;
                this.pupilVariant = pupilVariant;
            }
        }

        private static class FaceRuntimeSyncCoordinator
        {
            public static bool UpdateTrackAndLodIfNeeded(
                Pawn pawn,
                CompPawnSkin owner,
                FaceRuntimeState runtimeState,
                FaceRuntimeCompiledData compiledData,
                int currentTick)
            {
                if (!ShouldUpdateTrackAndLod(runtimeState, currentTick))
                    return false;

                bool shouldRefresh = runtimeState.trackDirty || runtimeState.lodDirty;
                FaceRuntimePolicy.UpdateRuntimeState(pawn, owner, runtimeState, compiledData, currentTick);
                ResetDirtyFlags(runtimeState);
                return shouldRefresh;
            }

            public static void SyncEffectiveState(FaceRuntimeState runtimeState, EffectiveFaceStateSnapshot snapshot)
            {
                runtimeState.expressionDirty = HasEffectiveFaceStateChanged(runtimeState, snapshot);
                ApplyEffectiveFaceState(runtimeState, snapshot);
            }

            public static void PreparePreviewState(FaceRuntimeState runtimeState, EffectiveFaceStateSnapshot snapshot)
            {
                ApplyEffectiveFaceState(runtimeState, snapshot);
                runtimeState.expressionDirty = false;
            }

            public static void ResetDirtyFlags(FaceRuntimeState runtimeState)
            {
                runtimeState.trackDirty = false;
                runtimeState.lodDirty = false;
                runtimeState.compiledDataDirty = false;
            }

            private static bool ShouldUpdateTrackAndLod(FaceRuntimeState runtimeState, int currentTick)
            {
                bool shouldUpdateTrackAndLod = runtimeState.trackDirty
                    || runtimeState.lodDirty
                    || runtimeState.compiledDataDirty;

                if (runtimeState.currentTrack == FaceRenderTrack.Portrait)
                    shouldUpdateTrackAndLod |= currentTick >= runtimeState.nextPortraitUpdateTick;
                else
                    shouldUpdateTrackAndLod |= currentTick >= runtimeState.nextWorldUpdateTick;

                return shouldUpdateTrackAndLod;
            }

            private static bool HasEffectiveFaceStateChanged(FaceRuntimeState runtimeState, EffectiveFaceStateSnapshot snapshot)
            {
                return runtimeState.currentExpression != snapshot.expression
                    || runtimeState.currentEyeDirection != snapshot.eyeDirection
                    || runtimeState.currentMouthState != snapshot.mouthState
                    || runtimeState.currentLidState != snapshot.lidState
                    || runtimeState.currentBrowState != snapshot.browState
                    || runtimeState.currentEmotionOverlayState != snapshot.emotionOverlayState
                    || runtimeState.eyeDirectionRuntimeVariant != snapshot.eyeVariant
                    || runtimeState.pupilScaleRuntimeVariant != snapshot.pupilVariant;
            }

            private static void ApplyEffectiveFaceState(FaceRuntimeState runtimeState, EffectiveFaceStateSnapshot snapshot)
            {
                runtimeState.currentExpression = snapshot.expression;
                runtimeState.currentEyeDirection = snapshot.eyeDirection;
                runtimeState.currentMouthState = snapshot.mouthState;
                runtimeState.currentLidState = snapshot.lidState;
                runtimeState.currentBrowState = snapshot.browState;
                runtimeState.currentEmotionOverlayState = snapshot.emotionOverlayState;
                runtimeState.eyeDirectionRuntimeVariant = snapshot.eyeVariant;
                runtimeState.pupilScaleRuntimeVariant = snapshot.pupilVariant;
            }
        }

        private static class FaceChannelStateResolver
        {
            public static MouthState ResolveMouthState(ExpressionType expression)
            {
                switch (expression)
                {
                    case ExpressionType.Happy:
                    case ExpressionType.Cheerful:
                    case ExpressionType.Lovin:
                    case ExpressionType.SocialRelax:
                        return MouthState.Smile;

                    case ExpressionType.Eating:
                    case ExpressionType.AttackMelee:
                    case ExpressionType.AttackRanged:
                    case ExpressionType.Scared:
                        return MouthState.Open;

                    case ExpressionType.Gloomy:
                    case ExpressionType.Sad:
                    case ExpressionType.Hopeless:
                    case ExpressionType.Pain:
                    case ExpressionType.Tired:
                        return MouthState.Down;

                    case ExpressionType.Sleeping:
                    case ExpressionType.LayDown:
                    case ExpressionType.Dead:
                        return MouthState.Sleep;

                    default:
                        return MouthState.Normal;
                }
            }

            public static LidState ResolveLidState(ExpressionType expression)
            {
                switch (expression)
                {
                    case ExpressionType.Blink:
                        return LidState.Blink;

                    case ExpressionType.Sleeping:
                    case ExpressionType.Dead:
                        return LidState.Close;

                    case ExpressionType.Tired:
                    case ExpressionType.Gloomy:
                    case ExpressionType.Sad:
                    case ExpressionType.Hopeless:
                    case ExpressionType.Pain:
                    case ExpressionType.LayDown:
                        return LidState.Half;

                    case ExpressionType.Happy:
                    case ExpressionType.Cheerful:
                    case ExpressionType.Lovin:
                        return LidState.Happy;

                    default:
                        return LidState.Normal;
                }
            }

            public static BrowState ResolveBrowState(ExpressionType expression)
            {
                switch (expression)
                {
                    case ExpressionType.Angry:
                    case ExpressionType.WaitCombat:
                    case ExpressionType.AttackMelee:
                    case ExpressionType.AttackRanged:
                        return BrowState.Angry;

                    case ExpressionType.Gloomy:
                    case ExpressionType.Sad:
                    case ExpressionType.Hopeless:
                    case ExpressionType.Tired:
                    case ExpressionType.Pain:
                    case ExpressionType.Dead:
                        return BrowState.Sad;

                    case ExpressionType.Happy:
                    case ExpressionType.Cheerful:
                    case ExpressionType.Lovin:
                        return BrowState.Happy;

                    default:
                        return BrowState.Normal;
                }
            }

            public static EmotionOverlayState ResolveEmotionOverlayState(ExpressionType expression)
            {
                switch (expression)
                {
                    case ExpressionType.Lovin:
                        return EmotionOverlayState.Lovin;

                    case ExpressionType.Happy:
                    case ExpressionType.Cheerful:
                    case ExpressionType.SocialRelax:
                        return EmotionOverlayState.Blush;

                    case ExpressionType.Scared:
                    case ExpressionType.Shock:
                    case ExpressionType.WaitCombat:
                    case ExpressionType.AttackMelee:
                    case ExpressionType.AttackRanged:
                        return EmotionOverlayState.Sweat;

                    case ExpressionType.Pain:
                    case ExpressionType.Sad:
                    case ExpressionType.Hopeless:
                    case ExpressionType.Dead:
                        return EmotionOverlayState.Tear;

                    case ExpressionType.Gloomy:
                        return EmotionOverlayState.Gloomy;

                    default:
                        return EmotionOverlayState.None;
                }
            }
        }

        private static class EffectiveFaceStateEvaluator
        {
            public static EffectiveFaceStateSnapshot BuildSnapshot(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                return new EffectiveFaceStateSnapshot(
                    expression,
                    ResolveEyeDirection(owner),
                    ResolveMouthState(owner, expression),
                    ResolveLidState(owner, expression),
                    ResolveBrowState(owner, expression),
                    ResolveEmotionOverlayState(owner, expression),
                    ResolveEyeAnimationVariant(owner, expression),
                    ResolvePupilScaleVariant(owner, expression));
            }

            public static ExpressionType ResolveExpression(CompPawnSkin owner)
            {
                if (owner.previewOverrides.PreviewExpression.HasValue)
                    return owner.previewOverrides.PreviewExpression.Value;

                if (owner.previewOverrides.PreviewRuntimeExpression.HasValue)
                    return owner.previewOverrides.PreviewRuntimeExpression.Value;

                if (owner.IsAbilityExpressionOverrideActive())
                    return owner.abilityExpressionOverride ?? owner.curExpression;

                if (owner.faceExpressionState.IsBlinkActive)
                    return ExpressionType.Blink;

                return owner.curExpression;
            }

            public static EyeDirection ResolveEyeDirection(CompPawnSkin owner)
                => owner.previewOverrides.PreviewEyeDirection ?? owner.curEyeDirection;

            public static MouthState ResolveMouthState(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                return ResolveMouthState(owner, expression);
            }

            public static LidState ResolveLidState(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                return ResolveLidState(owner, expression);
            }

            public static BrowState ResolveBrowState(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                return ResolveBrowState(owner, expression);
            }

            public static EmotionOverlayState ResolveEmotionOverlayState(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                return ResolveEmotionOverlayState(owner, expression);
            }

            private static MouthState ResolveMouthState(CompPawnSkin owner, ExpressionType expression)
            {
                if (owner.previewOverrides.PreviewMouthState.HasValue)
                    return owner.previewOverrides.PreviewMouthState.Value;

                return CompPawnSkin.ResolveMouthState(expression);
            }

            private static LidState ResolveLidState(CompPawnSkin owner, ExpressionType expression)
            {
                if (owner.previewOverrides.PreviewLidState.HasValue)
                    return owner.previewOverrides.PreviewLidState.Value;

                return CompPawnSkin.ResolveLidState(expression);
            }

            private static BrowState ResolveBrowState(CompPawnSkin owner, ExpressionType expression)
            {
                if (owner.previewOverrides.PreviewBrowState.HasValue)
                    return owner.previewOverrides.PreviewBrowState.Value;

                return CompPawnSkin.ResolveBrowState(expression);
            }

            private static EmotionOverlayState ResolveEmotionOverlayState(CompPawnSkin owner, ExpressionType expression)
            {
                if (owner.previewOverrides.PreviewEmotionOverlayState.HasValue)
                    return owner.previewOverrides.PreviewEmotionOverlayState.Value;

                return CompPawnSkin.ResolveEmotionOverlayState(expression);
            }

            private static EyeAnimationVariant ResolveEyeAnimationVariant(CompPawnSkin owner, ExpressionType expression)
            {
                return owner.ResolveEyeAnimationVariant(expression);
            }

            private static PupilScaleVariant ResolvePupilScaleVariant(CompPawnSkin owner, ExpressionType expression)
            {
                return owner.ResolvePupilScaleVariant(expression);
            }
        }

        private static class FaceExpressionStateResolver
        {
            public static ExpressionType ResolveExpression(Pawn pawn)
            {
                if (pawn.Dead)
                    return ExpressionType.Dead;

                if (pawn.Downed)
                    return ExpressionType.Pain;

                if (RestUtility.InBed(pawn))
                    return ExpressionType.Sleeping;

                ExpressionType? jobExpression = ResolveJobExpression(pawn);
                if (jobExpression.HasValue)
                    return jobExpression.Value;

                ExpressionType? mentalStateExpression = ResolveMentalStateExpression(pawn);
                if (mentalStateExpression.HasValue)
                    return mentalStateExpression.Value;

                return ResolveNeedsExpression(pawn);
            }

            private static ExpressionType? ResolveJobExpression(Pawn pawn)
            {
                var job = pawn.CurJob?.def;
                if (job == null)
                    return null;

                if (job == JobDefOf.Ingest)
                    return ExpressionType.Eating;

                if (job == JobDefOf.Lovin)
                    return ExpressionType.Lovin;

                if (job.defName == "LayDown"
                    || job.defName.IndexOf("LayDown", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ExpressionType.LayDown;

                if (job == JobDefOf.Strip
                    || job.defName.IndexOf("Strip", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ExpressionType.Strip;

                if (job == JobDefOf.HaulToCell
                    || job == JobDefOf.HaulToContainer
                    || job.defName.IndexOf("Haul", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Carry", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ExpressionType.Hauling;

                if (job.defName.IndexOf("Read", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Study", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ExpressionType.Reading;

                if (job.defName.IndexOf("SocialRelax", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Chat", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ExpressionType.SocialRelax;

                if (job == JobDefOf.DoBill
                    || job.defName.IndexOf("DoBill", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Craft", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Cook", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Smelt", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Repair", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Build", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Sow", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Harvest", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Clean", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Train", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Tend", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ExpressionType.Working;

                if (job.defName.IndexOf("WaitCombat", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ExpressionType.WaitCombat;

                if (job.defName.IndexOf("AttackMelee", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ExpressionType.AttackMelee;

                if (job.defName.IndexOf("AttackStatic", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Shoot", StringComparison.OrdinalIgnoreCase) >= 0
                    || job.defName.IndexOf("Burst", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ExpressionType.AttackRanged;

                return null;
            }

            private static ExpressionType? ResolveMentalStateExpression(Pawn pawn)
            {
                if (!pawn.InMentalState)
                    return null;

                var stateDef = pawn.MentalStateDef;
                bool isPanic = stateDef != null
                    && (stateDef.defName.IndexOf("Flee", StringComparison.OrdinalIgnoreCase) >= 0
                        || stateDef.defName.IndexOf("Panic", StringComparison.OrdinalIgnoreCase) >= 0
                        || stateDef.defName.IndexOf("WildMan", StringComparison.OrdinalIgnoreCase) >= 0);

                return isPanic ? ExpressionType.Scared : ExpressionType.Angry;
            }

            private static ExpressionType ResolveNeedsExpression(Pawn pawn)
            {
                float rest = pawn.needs?.rest?.CurLevel ?? 1f;
                float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;

                if (rest < 0.15f)
                    return ExpressionType.Tired;

                if (mood < 0.1f)
                    return ExpressionType.Hopeless;

                if (mood < 0.2f)
                    return ExpressionType.Sad;

                if (mood < 0.4f)
                    return ExpressionType.Gloomy;

                if (mood > 0.9f)
                    return ExpressionType.Cheerful;

                if (mood > 0.8f)
                    return ExpressionType.Happy;

                return ExpressionType.Neutral;
            }
        }

        private static class EyeDirectionStateResolver
        {
            public static EyeDirection ResolveDirection(Pawn pawn)
            {
                if (pawn.Dead || pawn.Downed || RestUtility.InBed(pawn))
                    return EyeDirection.Center;

                IntVec3 targetCell = GetJobTargetCell(pawn);
                if (targetCell.IsValid && pawn.Position.IsValid)
                {
                    IntVec3 delta = targetCell - pawn.Position;
                    return MapDeltaToEyeDirection(delta, pawn.Rotation);
                }

                return MapRotationToEyeDirection(pawn.Rotation);
            }

            private static IntVec3 GetJobTargetCell(Pawn pawn)
            {
                try
                {
                    var job = pawn.CurJob;
                    if (job == null) return IntVec3.Invalid;

                    var targetA = job.targetA;
                    if (targetA.HasThing && targetA.Thing?.Position.IsValid == true)
                        return targetA.Thing.Position;
                    if (targetA.Cell.IsValid)
                        return targetA.Cell;

                    return IntVec3.Invalid;
                }
                catch
                {
                    return IntVec3.Invalid;
                }
            }

            private static EyeDirection MapDeltaToEyeDirection(IntVec3 delta, Rot4 rot)
            {
                if (delta == IntVec3.Zero) return EyeDirection.Center;

                int localX;
                int localZ;
                if (rot == Rot4.North) { localX = -delta.x; localZ = -delta.z; }
                else if (rot == Rot4.East) { localX = delta.z; localZ = -delta.x; }
                else if (rot == Rot4.West) { localX = -delta.z; localZ = delta.x; }
                else { localX = delta.x; localZ = delta.z; }

                int absX = Math.Abs(localX);
                int absZ = Math.Abs(localZ);

                if (absX <= 1 && absZ <= 1) return EyeDirection.Center;

                if (absX >= absZ)
                    return localX > 0 ? EyeDirection.Right : EyeDirection.Left;

                return localZ > 0 ? EyeDirection.Down : EyeDirection.Up;
            }

            private static EyeDirection MapRotationToEyeDirection(Rot4 rot)
            {
                if (rot == Rot4.East)
                    return EyeDirection.Right;

                if (rot == Rot4.West)
                    return EyeDirection.Left;

                if (rot == Rot4.North)
                    return EyeDirection.Up;

                return EyeDirection.Center;
            }
        }

        private static class FaceRuntimeActivationGuard
        {
            public static bool IsFaceRuntimeEnabled(CompPawnSkin owner)
                => owner.activeSkin?.faceConfig?.enabled == true;

            public static bool CanProcessFaceRuntime(CompPawnSkin owner, Pawn? pawn)
                => pawn != null && IsFaceRuntimeEnabled(owner);

            public static bool IsEyeDirectionEnabled(CompPawnSkin owner)
                => IsFaceRuntimeEnabled(owner)
                    && owner.activeSkin?.faceConfig?.eyeDirectionConfig?.enabled == true;

            public static bool CanProcessEyeDirection(CompPawnSkin owner, Pawn? pawn)
                => pawn != null && IsEyeDirectionEnabled(owner);
        }

        private static class FaceRuntimeRefreshCoordinator
        {
            public static void Apply(CompPawnSkin owner, bool immediateRefresh, bool deferredRefresh)
            {
                owner.MarkFaceRuntimeDirty();
                owner.needsRefresh = deferredRefresh;

                if (immediateRefresh)
                    owner.RequestRenderRefresh();
            }

            public static void FlushDeferredRefresh(CompPawnSkin owner)
            {
                if (!owner.needsRefresh)
                    return;

                owner.RequestRenderRefresh();
                owner.needsRefresh = false;
            }
        }

        private static class SkinLifecycleRecoveryCoordinator
        {
            public static void RestoreAfterSpawn(CompPawnSkin owner)
            {
                SkinRecoveryOutcome recoveryOutcome = TryEnsureSkinAvailableAfterSpawn(owner);
                ApplyRecoveryOutcome(owner, recoveryOutcome);
            }

            public static void RestoreAfterLoad(CompPawnSkin owner)
            {
                SkinRecoveryOutcome recoveryOutcome = TryRestoreSkinAfterLoad(owner);
                ApplyRecoveryOutcome(owner, recoveryOutcome);
            }

            private readonly struct SkinRecoveryPolicy
            {
                public readonly bool allowRegistryFallback;
                public readonly bool normalizeDefName;
                public readonly bool allowDefaultRaceFallback;
                public readonly bool treatExistingActiveSkinAsSuccess;
                public readonly SkinRecoveryRefreshDirective successRefreshDirective;

                public SkinRecoveryPolicy(
                    bool allowRegistryFallback,
                    bool normalizeDefName,
                    bool allowDefaultRaceFallback,
                    bool treatExistingActiveSkinAsSuccess,
                    SkinRecoveryRefreshDirective successRefreshDirective)
                {
                    this.allowRegistryFallback = allowRegistryFallback;
                    this.normalizeDefName = normalizeDefName;
                    this.allowDefaultRaceFallback = allowDefaultRaceFallback;
                    this.treatExistingActiveSkinAsSuccess = treatExistingActiveSkinAsSuccess;
                    this.successRefreshDirective = successRefreshDirective;
                }
            }

            private enum SkinRecoveryResult
            {
                NotRecovered,
                ExistingActiveSkin,
                RestoredSavedSkin,
                AppliedDefaultRaceSkin
            }

            private enum SkinRecoveryRefreshDirective
            {
                None,
                ImmediateRefresh
            }

            private readonly struct SkinRecoveryOutcome
            {
                public readonly SkinRecoveryResult result;
                public readonly SkinRecoveryRefreshDirective refreshDirective;

                public SkinRecoveryOutcome(
                    SkinRecoveryResult result,
                    SkinRecoveryRefreshDirective refreshDirective)
                {
                    this.result = result;
                    this.refreshDirective = refreshDirective;
                }
            }

            // 保持既有语义：读档恢复后不立即刷新。
            private static readonly SkinRecoveryPolicy LoadRecoveryPolicy = new SkinRecoveryPolicy(
                allowRegistryFallback: true,
                normalizeDefName: true,
                allowDefaultRaceFallback: false,
                treatExistingActiveSkinAsSuccess: false,
                successRefreshDirective: SkinRecoveryRefreshDirective.None);

            private static readonly SkinRecoveryPolicy SpawnRecoveryPolicy = new SkinRecoveryPolicy(
                allowRegistryFallback: false,
                normalizeDefName: false,
                allowDefaultRaceFallback: true,
                treatExistingActiveSkinAsSuccess: true,
                successRefreshDirective: SkinRecoveryRefreshDirective.ImmediateRefresh);

            private static SkinRecoveryOutcome TryRestoreSkinAfterLoad(CompPawnSkin owner)
            {
                return TryRecoverSkin(owner, LoadRecoveryPolicy);
            }

            private static SkinRecoveryOutcome TryEnsureSkinAvailableAfterSpawn(CompPawnSkin owner)
            {
                return TryRecoverSkin(owner, SpawnRecoveryPolicy);
            }

            private static void ApplyRecoveryOutcome(CompPawnSkin owner, SkinRecoveryOutcome recoveryOutcome)
            {
                if (recoveryOutcome.refreshDirective == SkinRecoveryRefreshDirective.ImmediateRefresh)
                    owner.RequestRenderRefresh();
            }

            private static SkinRecoveryOutcome CreateRecoveryOutcome(
                SkinRecoveryResult recoveryResult,
                SkinRecoveryRefreshDirective recoveredRefreshDirective)
            {
                switch (recoveryResult)
                {
                    case SkinRecoveryResult.ExistingActiveSkin:
                    case SkinRecoveryResult.RestoredSavedSkin:
                    case SkinRecoveryResult.AppliedDefaultRaceSkin:
                        return new SkinRecoveryOutcome(
                            recoveryResult,
                            recoveredRefreshDirective);

                    case SkinRecoveryResult.NotRecovered:
                    default:
                        return new SkinRecoveryOutcome(
                            recoveryResult,
                            SkinRecoveryRefreshDirective.None);
                }
            }

            private static SkinRecoveryOutcome TryRecoverSkin(
                CompPawnSkin owner,
                SkinRecoveryPolicy policy)
            {
                if (policy.treatExistingActiveSkinAsSuccess && owner.activeSkin != null)
                {
                    return CreateRecoveryOutcome(
                        SkinRecoveryResult.ExistingActiveSkin,
                        policy.successRefreshDirective);
                }

                bool hasRestoredSavedSkin = TryRestoreSavedSkin(
                    owner,
                    policy.allowRegistryFallback,
                    policy.normalizeDefName);
                if (hasRestoredSavedSkin)
                {
                    return CreateRecoveryOutcome(
                        SkinRecoveryResult.RestoredSavedSkin,
                        policy.successRefreshDirective);
                }

                if (policy.allowDefaultRaceFallback && TryApplyDefaultRaceSkin(owner))
                {
                    return CreateRecoveryOutcome(
                        SkinRecoveryResult.AppliedDefaultRaceSkin,
                        policy.successRefreshDirective);
                }

                return CreateRecoveryOutcome(
                    SkinRecoveryResult.NotRecovered,
                    policy.successRefreshDirective);
            }

            public static bool TryApplyDefaultRaceSkin(CompPawnSkin owner)
            {
                PawnSkinDef? defaultSkin = ResolveDefaultRaceSkinToApply(owner);
                if (defaultSkin == null)
                    return false;

                SkinApplicationCoordinator.ApplyDefaultBound(owner, defaultSkin);
                return true;
            }

            private static PawnSkinDef? ResolveDefaultRaceSkinToApply(CompPawnSkin owner)
            {
                Pawn? pawn = owner.Pawn;
                if (pawn == null || pawn.def == null)
                    return null;

                if (!pawn.RaceProps.Humanlike)
                    return null;

                if (owner.activeSkin != null)
                    return null;

                PawnSkinDef? defaultSkin = PawnSkinDefRegistry.GetDefaultSkinForRace(pawn.def);
                return defaultSkin?.Clone();
            }

            private static bool TryRestoreSavedSkin(
                CompPawnSkin owner,
                bool allowRegistryFallback,
                bool normalizeDefName)
            {
                PawnSkinDef? restoredSkin = ResolveSavedSkin(owner.activeSkinDefName, allowRegistryFallback);
                if (restoredSkin == null)
                    return false;

                SkinApplicationCoordinator.RestoreResolved(owner, restoredSkin, owner.activeSkinFromDefaultRaceBinding);

                if (normalizeDefName)
                    owner.activeSkinDefName = restoredSkin.defName;

                return true;
            }

            private static PawnSkinDef? ResolveSavedSkin(string? skinDefName, bool allowRegistryFallback)
            {
                if (string.IsNullOrEmpty(skinDefName))
                    return null;

                PawnSkinDef? restoredSkin = DefDatabase<PawnSkinDef>.GetNamedSilentFail(skinDefName);
                if (restoredSkin == null && allowRegistryFallback)
                {
                    restoredSkin = PawnSkinDefRegistry.TryGet(skinDefName);
                }

                return restoredSkin;
            }
        }

        internal readonly struct SkinApplicationWriteResult
        {
            public readonly bool skinChanged;
            public readonly bool sourceChanged;

            public bool HasChanged => skinChanged || sourceChanged;

            public SkinApplicationWriteResult(bool skinChanged, bool sourceChanged)
            {
                this.skinChanged = skinChanged;
                this.sourceChanged = sourceChanged;
            }
        }

        private static class SkinApplicationCoordinator
        {
            private enum SkinApplicationRefreshDirective
            {
                None,
                MarkDirtyOnly,
                DeferredRefresh,
                ImmediateRefresh,
                ImmediateAndDeferredRefresh
            }

            private readonly struct SkinApplicationDirective
            {
                public readonly bool fromDefaultRaceBinding;
                public readonly bool previewMode;
                public readonly string applicationSource;
                public readonly bool syncXenotype;
                public readonly SkinApplicationRefreshDirective refreshDirective;

                public SkinApplicationDirective(
                    bool fromDefaultRaceBinding,
                    bool previewMode,
                    string applicationSource,
                    bool syncXenotype,
                    SkinApplicationRefreshDirective refreshDirective)
                {
                    this.fromDefaultRaceBinding = fromDefaultRaceBinding;
                    this.previewMode = previewMode;
                    this.applicationSource = applicationSource ?? string.Empty;
                    this.syncXenotype = syncXenotype;
                    this.refreshDirective = refreshDirective;
                }
            }

            private static readonly SkinApplicationDirective ManualApplicationDirective = new SkinApplicationDirective(
                fromDefaultRaceBinding: false,
                previewMode: false,
                applicationSource: string.Empty,
                syncXenotype: true,
                refreshDirective: SkinApplicationRefreshDirective.ImmediateAndDeferredRefresh);

            private static readonly SkinApplicationDirective DefaultBoundApplicationDirective = new SkinApplicationDirective(
                fromDefaultRaceBinding: true,
                previewMode: false,
                applicationSource: string.Empty,
                syncXenotype: false,
                refreshDirective: SkinApplicationRefreshDirective.DeferredRefresh);

            private static readonly SkinApplicationDirective ClearApplicationDirective = new SkinApplicationDirective(
                fromDefaultRaceBinding: false,
                previewMode: false,
                applicationSource: string.Empty,
                syncXenotype: false,
                refreshDirective: SkinApplicationRefreshDirective.ImmediateRefresh);

            public static void ApplyManual(CompPawnSkin owner, PawnSkinDef? skin)
            {
                Apply(owner, skin, ManualApplicationDirective);
            }

            public static SkinApplicationWriteResult SetWithSourceSilently(
                CompPawnSkin owner,
                PawnSkinDef? skin,
                bool fromDefaultRaceBinding,
                bool previewMode,
                string applicationSource)
            {
                return Apply(owner, skin, CreateSilentApplicationDirective(fromDefaultRaceBinding, previewMode, applicationSource));
            }

            public static void RestoreResolved(CompPawnSkin owner, PawnSkinDef? skin, bool fromDefaultRaceBinding)
            {
                Apply(owner, skin, CreateRestoreApplicationDirective(fromDefaultRaceBinding));
            }

            public static void ApplyDefaultBound(CompPawnSkin owner, PawnSkinDef? skin)
            {
                if (skin == null)
                    return;

                Apply(owner, skin, DefaultBoundApplicationDirective);
            }

            public static SkinApplicationWriteResult Clear(CompPawnSkin owner)
            {
                return Apply(owner, null, ClearApplicationDirective);
            }

            private static SkinApplicationDirective CreateSilentApplicationDirective(
                bool fromDefaultRaceBinding,
                bool previewMode,
                string applicationSource)
            {
                return new SkinApplicationDirective(
                    fromDefaultRaceBinding: fromDefaultRaceBinding,
                    previewMode: previewMode,
                    applicationSource: applicationSource,
                    syncXenotype: false,
                    refreshDirective: SkinApplicationRefreshDirective.MarkDirtyOnly);
            }

            private static SkinApplicationDirective CreateRestoreApplicationDirective(bool fromDefaultRaceBinding)
            {
                return new SkinApplicationDirective(
                    fromDefaultRaceBinding: fromDefaultRaceBinding,
                    previewMode: false,
                    applicationSource: string.Empty,
                    syncXenotype: false,
                    refreshDirective: SkinApplicationRefreshDirective.None);
            }

            private static SkinApplicationWriteResult Apply(
                CompPawnSkin owner,
                PawnSkinDef? skin,
                SkinApplicationDirective directive)
            {
                SkinApplicationWriteResult writeResult = SetSkin(
                    owner,
                    skin,
                    directive.fromDefaultRaceBinding,
                    directive.previewMode,
                    directive.applicationSource);
                if (!writeResult.HasChanged)
                    return writeResult;

                CharacterAttributeBuffService.SyncAttributeBuff(owner.Pawn);

                if (directive.syncXenotype)
                    owner.SyncXenotype(skin);

                ApplyRefreshDirective(owner, directive.refreshDirective);
                return writeResult;
            }

            private static void ApplyRefreshDirective(
                CompPawnSkin owner,
                SkinApplicationRefreshDirective refreshDirective)
            {
                switch (refreshDirective)
                {
                    case SkinApplicationRefreshDirective.MarkDirtyOnly:
                        FaceRuntimeRefreshCoordinator.Apply(owner, immediateRefresh: false, deferredRefresh: false);
                        return;

                    case SkinApplicationRefreshDirective.DeferredRefresh:
                        FaceRuntimeRefreshCoordinator.Apply(owner, immediateRefresh: false, deferredRefresh: true);
                        return;

                    case SkinApplicationRefreshDirective.ImmediateRefresh:
                        FaceRuntimeRefreshCoordinator.Apply(owner, immediateRefresh: true, deferredRefresh: false);
                        return;

                    case SkinApplicationRefreshDirective.ImmediateAndDeferredRefresh:
                        FaceRuntimeRefreshCoordinator.Apply(owner, immediateRefresh: true, deferredRefresh: true);
                        return;

                    case SkinApplicationRefreshDirective.None:
                    default:
                        return;
                }
            }

            private static SkinApplicationWriteResult SetSkin(
                CompPawnSkin owner,
                PawnSkinDef? skin,
                bool fromDefaultRaceBinding,
                bool previewMode,
                string applicationSource)
            {
                string? nextSkinDefName = skin?.defName;
                string normalizedSource = applicationSource ?? string.Empty;
                bool skinChanged = owner.activeSkin != skin
                    || owner.activeSkinDefName != nextSkinDefName;
                bool sourceChanged = owner.activeSkinFromDefaultRaceBinding != fromDefaultRaceBinding
                    || owner.activeSkinPreviewMode != previewMode
                    || !string.Equals(owner.activeSkinApplicationSource ?? string.Empty, normalizedSource, StringComparison.Ordinal);

                if (!skinChanged && !sourceChanged)
                    return new SkinApplicationWriteResult(false, false);

                owner.activeSkin = skin;
                owner.activeSkinDefName = nextSkinDefName;
                owner.activeSkinFromDefaultRaceBinding = fromDefaultRaceBinding;
                owner.activeSkinPreviewMode = skin != null && previewMode;
                owner.activeSkinApplicationSource = skin != null ? normalizedSource : string.Empty;
                return new SkinApplicationWriteResult(skinChanged, sourceChanged);
            }
        }

        private static class FaceRuntimeTickCoordinator
        {
            public static void Tick(CompPawnSkin owner, Pawn pawn)
            {
                owner.EnsureFaceRuntimeStateUpdated();
                owner.faceExpressionState.AdvanceAnimTick();

                if (ShouldUpdateExpression(pawn))
                    owner.UpdateExpressionState();

                owner.UpdateAnimatedExpressionFrame();
                owner.UpdateBlinkLogic();
                owner.UpdateEyeAnimationVariant();

                if (ShouldUpdateEyeDirection(owner, pawn))
                    owner.UpdateEyeDirectionState();
            }

            private static bool ShouldUpdateExpression(Pawn pawn)
                => pawn.IsHashIntervalTick(30);

            private static bool ShouldUpdateEyeDirection(CompPawnSkin owner, Pawn pawn)
                => FaceRuntimeActivationGuard.IsEyeDirectionEnabled(owner)
                    && pawn.IsHashIntervalTick(15);
        }

        private sealed class FacePreviewOverrideState
        {
            public ExpressionType? PreviewExpression { get; private set; }
            public ExpressionType? PreviewRuntimeExpression { get; private set; }
            public EyeDirection? PreviewEyeDirection { get; private set; }
            public MouthState? PreviewMouthState { get; private set; }
            public LidState? PreviewLidState { get; private set; }
            public BrowState? PreviewBrowState { get; private set; }
            public EmotionOverlayState? PreviewEmotionOverlayState { get; private set; }

            public bool SetExpression(ExpressionType? expression)
            {
                if (PreviewExpression == expression)
                    return false;

                PreviewExpression = expression;
                return true;
            }

            public bool SetRuntimeExpression(ExpressionType? expression)
            {
                if (PreviewRuntimeExpression == expression)
                    return false;

                PreviewRuntimeExpression = expression;
                return true;
            }

            public bool SetEyeDirection(EyeDirection? direction)
            {
                if (PreviewEyeDirection == direction)
                    return false;

                PreviewEyeDirection = direction;
                return true;
            }

            public bool SetMouthState(MouthState? state)
            {
                if (PreviewMouthState == state)
                    return false;

                PreviewMouthState = state;
                return true;
            }

            public bool SetLidState(LidState? state)
            {
                if (PreviewLidState == state)
                    return false;

                PreviewLidState = state;
                return true;
            }

            public bool SetBrowState(BrowState? state)
            {
                if (PreviewBrowState == state)
                    return false;

                PreviewBrowState = state;
                return true;
            }

            public bool SetEmotionOverlayState(EmotionOverlayState? state)
            {
                if (PreviewEmotionOverlayState == state)
                    return false;

                PreviewEmotionOverlayState = state;
                return true;
            }

            public bool ClearChannelOverrides()
            {
                bool changed = PreviewMouthState.HasValue
                    || PreviewLidState.HasValue
                    || PreviewBrowState.HasValue
                    || PreviewEmotionOverlayState.HasValue;

                PreviewMouthState = null;
                PreviewLidState = null;
                PreviewBrowState = null;
                PreviewEmotionOverlayState = null;

                return changed;
            }
        }

        private sealed class AbilityHotkeyRuntimeState
        {
            public int qHotkeyModeIndex = 0;
            public int qComboWindowEndTick = 0;
            public string qOverrideAbilityDefName = string.Empty;
            public int qOverrideExpireTick = -1;
            public string wOverrideAbilityDefName = string.Empty;
            public int wOverrideExpireTick = -1;
            public string eOverrideAbilityDefName = string.Empty;
            public int eOverrideExpireTick = -1;
            public string rOverrideAbilityDefName = string.Empty;
            public int rOverrideExpireTick = -1;
            public int qCooldownUntilTick = 0;
            public int wCooldownUntilTick = 0;
            public int eCooldownUntilTick = 0;
            public int rCooldownUntilTick = 0;
            public bool rStackingEnabled = false;
            public int rStackCount = 0;
            public bool rSecondStageReady = false;
            public int rSecondStageExecuteTick = -1;
            public bool rSecondStageHasTarget = false;
            public IntVec3 rSecondStageTargetCell = IntVec3.Invalid;
            public int weaponCarryCastingUntilTick = -1;
            public int periodicPulseNextTick = -1;
            public int periodicPulseEndTick = -1;
            public float shieldRemainingDamage = 0f;
            public int shieldExpireTick = -1;
            public float shieldStoredHeal = 0f;
            public float shieldStoredBonusDamage = 0f;
            public int offensiveMarkExpireTick = -1;
            public int offensiveMarkStacks = 0;
            public int offensiveComboExpireTick = -1;
            public int offensiveComboStacks = 0;
            public int dashEmpowerExpireTick = -1;
            public int flightStateStartTick = -1;
            public int flightStateExpireTick = -1;
            public float flightStateHeightFactor = 0.35f;
            public bool suppressCombatActionsDuringFlightState = true;
            public bool isInVanillaFlight = false;
            public int vanillaFlightStartTick = -1;
            public int vanillaFlightExpireTick = -1;
            public string vanillaFlightSourceAbilityDefName = string.Empty;
            public string vanillaFlightFollowupAbilityDefName = string.Empty;
            public IntVec3 vanillaFlightReservedTargetCell = IntVec3.Invalid;
            public bool vanillaFlightHasReservedTargetCell = false;
            public int vanillaFlightFollowupWindowEndTick = -1;
            public bool vanillaFlightPendingLandingBurst = false;
            public string triggeredEquipmentAnimationAbilityDefName = string.Empty;
            public int triggeredEquipmentAnimationStartTick = -1;
            public int triggeredEquipmentAnimationEndTick = -1;
            public int abilityExpressionOverrideExpireTick = -1;
            public ExpressionType? abilityExpressionOverride = null;
            public float abilityPupilBrightnessOffset = 0f;
            public float abilityPupilContrastOffset = 0f;

            public void ExposeData()
            {
                Scribe_Values.Look(ref qHotkeyModeIndex, "qHotkeyModeIndex", 0);
                Scribe_Values.Look(ref qComboWindowEndTick, "qComboWindowEndTick", 0);
                Scribe_Values.Look(ref qOverrideAbilityDefName, "qOverrideAbilityDefName", string.Empty);
                Scribe_Values.Look(ref qOverrideExpireTick, "qOverrideExpireTick", -1);
                Scribe_Values.Look(ref wOverrideAbilityDefName, "wOverrideAbilityDefName", string.Empty);
                Scribe_Values.Look(ref wOverrideExpireTick, "wOverrideExpireTick", -1);
                Scribe_Values.Look(ref eOverrideAbilityDefName, "eOverrideAbilityDefName", string.Empty);
                Scribe_Values.Look(ref eOverrideExpireTick, "eOverrideExpireTick", -1);
                Scribe_Values.Look(ref rOverrideAbilityDefName, "rOverrideAbilityDefName", string.Empty);
                Scribe_Values.Look(ref rOverrideExpireTick, "rOverrideExpireTick", -1);
                Scribe_Values.Look(ref qCooldownUntilTick, "qCooldownUntilTick", 0);
                Scribe_Values.Look(ref wCooldownUntilTick, "wCooldownUntilTick", 0);
                Scribe_Values.Look(ref eCooldownUntilTick, "eCooldownUntilTick", 0);
                Scribe_Values.Look(ref rCooldownUntilTick, "rCooldownUntilTick", 0);
                Scribe_Values.Look(ref rStackingEnabled, "rStackingEnabled", false);
                Scribe_Values.Look(ref rStackCount, "rStackCount", 0);
                Scribe_Values.Look(ref rSecondStageReady, "rSecondStageReady", false);
                Scribe_Values.Look(ref rSecondStageExecuteTick, "rSecondStageExecuteTick", -1);
                Scribe_Values.Look(ref rSecondStageHasTarget, "rSecondStageHasTarget", false);
                Scribe_Values.Look(ref rSecondStageTargetCell, "rSecondStageTargetCell", IntVec3.Invalid);
                Scribe_Values.Look(ref weaponCarryCastingUntilTick, "weaponCarryCastingUntilTick", -1);
                Scribe_Values.Look(ref periodicPulseNextTick, "periodicPulseNextTick", -1);
                Scribe_Values.Look(ref periodicPulseEndTick, "periodicPulseEndTick", -1);
                Scribe_Values.Look(ref isInVanillaFlight, "isInVanillaFlight", false);
                Scribe_Values.Look(ref vanillaFlightStartTick, "vanillaFlightStartTick", -1);
                Scribe_Values.Look(ref vanillaFlightExpireTick, "vanillaFlightExpireTick", -1);
                Scribe_Values.Look(ref vanillaFlightSourceAbilityDefName, "vanillaFlightSourceAbilityDefName", string.Empty);
                Scribe_Values.Look(ref vanillaFlightFollowupAbilityDefName, "vanillaFlightFollowupAbilityDefName", string.Empty);
                Scribe_Values.Look(ref vanillaFlightReservedTargetCell, "vanillaFlightReservedTargetCell", IntVec3.Invalid);
                Scribe_Values.Look(ref vanillaFlightHasReservedTargetCell, "vanillaFlightHasReservedTargetCell", false);
                Scribe_Values.Look(ref vanillaFlightFollowupWindowEndTick, "vanillaFlightFollowupWindowEndTick", -1);
                Scribe_Values.Look(ref vanillaFlightPendingLandingBurst, "vanillaFlightPendingLandingBurst", false);

                Scribe_Values.Look(ref shieldRemainingDamage, "shieldRemainingDamage", 0f);
                Scribe_Values.Look(ref shieldExpireTick, "shieldExpireTick", -1);
                Scribe_Values.Look(ref shieldStoredHeal, "shieldStoredHeal", 0f);
                Scribe_Values.Look(ref shieldStoredBonusDamage, "shieldStoredBonusDamage", 0f);
                Scribe_Values.Look(ref offensiveMarkExpireTick, "offensiveMarkExpireTick", -1);
                Scribe_Values.Look(ref offensiveMarkStacks, "offensiveMarkStacks", 0);
                Scribe_Values.Look(ref offensiveComboExpireTick, "offensiveComboExpireTick", -1);
                Scribe_Values.Look(ref offensiveComboStacks, "offensiveComboStacks", 0);
                Scribe_Values.Look(ref dashEmpowerExpireTick, "dashEmpowerExpireTick", -1);
                Scribe_Values.Look(ref flightStateStartTick, "flightStateStartTick", -1);
                Scribe_Values.Look(ref flightStateExpireTick, "flightStateExpireTick", -1);
                Scribe_Values.Look(ref flightStateHeightFactor, "flightStateHeightFactor", 0.35f);
                Scribe_Values.Look(ref suppressCombatActionsDuringFlightState, "suppressCombatActionsDuringFlightState", true);
                Scribe_Values.Look(ref triggeredEquipmentAnimationAbilityDefName, "triggeredEquipmentAnimationAbilityDefName", string.Empty);
                Scribe_Values.Look(ref triggeredEquipmentAnimationStartTick, "triggeredEquipmentAnimationStartTick", -1);
                Scribe_Values.Look(ref triggeredEquipmentAnimationEndTick, "triggeredEquipmentAnimationEndTick", -1);
                Scribe_Values.Look(ref abilityExpressionOverrideExpireTick, "abilityExpressionOverrideExpireTick", -1);
                Scribe_Values.Look(ref abilityExpressionOverride, "abilityExpressionOverride");
                Scribe_Values.Look(ref abilityPupilBrightnessOffset, "abilityPupilBrightnessOffset", 0f);
                Scribe_Values.Look(ref abilityPupilContrastOffset, "abilityPupilContrastOffset", 0f);
            }

            public void Normalize()
            {
                qOverrideAbilityDefName ??= string.Empty;
                wOverrideAbilityDefName ??= string.Empty;
                eOverrideAbilityDefName ??= string.Empty;
                rOverrideAbilityDefName ??= string.Empty;

                if (qHotkeyModeIndex < 0 || qHotkeyModeIndex > 3)
                {
                    qHotkeyModeIndex = 0;
                }

                if (rStackCount < 0)
                {
                    rStackCount = 0;
                }
                if (rStackCount > 7)
                {
                    rStackCount = 7;
                }

                if (!rSecondStageHasTarget)
                {
                    rSecondStageTargetCell = IntVec3.Invalid;
                }

                if (shieldRemainingDamage < 0f)
                {
                    shieldRemainingDamage = 0f;
                }
                if (shieldStoredHeal < 0f)
                {
                    shieldStoredHeal = 0f;
                }
                if (shieldStoredBonusDamage < 0f)
                {
                    shieldStoredBonusDamage = 0f;
                }
                if (offensiveMarkStacks < 0)
                {
                    offensiveMarkStacks = 0;
                }
                if (offensiveComboStacks < 0)
                {
                    offensiveComboStacks = 0;
                }
                if (flightStateExpireTick < -1)
                {
                    flightStateExpireTick = -1;
                }
                if (flightStateStartTick < -1)
                {
                    flightStateStartTick = -1;
                }
                if (flightStateHeightFactor < 0f)
                {
                    flightStateHeightFactor = 0f;
                }

                if (abilityExpressionOverrideExpireTick < -1)
                {
                    abilityExpressionOverrideExpireTick = -1;
                }

                triggeredEquipmentAnimationAbilityDefName ??= string.Empty;
                if (triggeredEquipmentAnimationEndTick < triggeredEquipmentAnimationStartTick)
                {
                    triggeredEquipmentAnimationStartTick = -1;
                }
            }
        }

        public PawnSkinDef? ActiveSkin
        {
            get => activeSkin;
            set
            {
                if (activeSkin != value || activeSkinFromDefaultRaceBinding)
                {
                    SkinApplicationCoordinator.ApplyManual(this, value);
                }
            }
        }

        public CharacterAbilityLoadout? ActiveAbilityLoadout
        {
            get => activeAbilityLoadout;
            set => activeAbilityLoadout = value?.Clone();
        }

        public bool HasActiveSkin => activeSkin != null;
        public bool HasExplicitAbilityLoadout => activeAbilityLoadout != null;
        public bool ActiveSkinFromDefaultRaceBinding => activeSkinFromDefaultRaceBinding;
        public bool ActiveSkinPreviewMode => activeSkinPreviewMode;
        public string ActiveSkinApplicationSource => activeSkinApplicationSource ?? string.Empty;
        public bool ShouldInjectEquipmentRenderDataDirectly => activeSkinPreviewMode;

        internal SkinApplicationWriteResult SetActiveSkinWithSource(PawnSkinDef? skin, bool fromDefaultRaceBinding)
        {
            return SkinApplicationCoordinator.SetWithSourceSilently(this, skin, fromDefaultRaceBinding, false, string.Empty);
        }

        internal SkinApplicationWriteResult SetActiveSkinWithSource(
            PawnSkinDef? skin,
            bool fromDefaultRaceBinding,
            bool previewMode,
            string applicationSource)
        {
            return SkinApplicationCoordinator.SetWithSourceSilently(
                this,
                skin,
                fromDefaultRaceBinding,
                previewMode,
                applicationSource);
        }

        // ─────────────────────────────────────────────
        // Xenotype 同步
        // ─────────────────────────────────────────────

        /// <summary>
        /// 应用皮肤时，将 pawn 的 Xenotype 同步到皮肤绑定的 XenotypeDef。
        /// xenotypeDefName 为空时静默跳过，保持向后兼容。
        /// </summary>
        private void SyncXenotype(PawnSkinDef? skin)
        {
            if (skin == null || string.IsNullOrEmpty(skin.xenotypeDefName))
                return;

            var pawn = Pawn;
            if (pawn == null || pawn.genes == null)
                return;

            // 避免在 spawning 阶段之前执行（地图尚未初始化时 Spawned 为 false）
            if (!pawn.Spawned)
                return;

            var xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(skin.xenotypeDefName);
            if (xenotype == null)
            {
                Log.Warning($"[CharacterStudio] SyncXenotype: XenotypeDef '{skin.xenotypeDefName}' not found for skin '{skin.defName}'.");
                return;
            }

            pawn.genes.SetXenotype(xenotype);
        }

        public Pawn? Pawn => parent as Pawn;

        /// <summary>
        /// 当前 Pawn 的双轨运行时状态。
        /// 第一阶段先作为状态同步与后续渲染接入的统一入口。
        /// </summary>
        public FaceRuntimeState CurrentFaceRuntimeState => faceRuntimeState ??= new FaceRuntimeState();

        /// <summary>
        /// 当前 Pawn 的面部编译缓存。
        /// 由 Runtime Compiler 按皮肤内容签名构建并缓存。
        /// </summary>
        public FaceRuntimeCompiledData CurrentFaceRuntimeCompiledData
            => faceRuntimeCompiledData ??= FaceRuntimeCompiler.GetOrBuild(activeSkin);

        private void MarkFaceRuntimeDirty()
        {
            faceRuntimeCompiledData = null;

            if (faceRuntimeState == null)
                faceRuntimeState = new FaceRuntimeState();
            else
                faceRuntimeState.MarkAllDirty();
        }

        private EffectiveFaceStateSnapshot BuildEffectiveFaceStateSnapshot()
            => EffectiveFaceStateEvaluator.BuildSnapshot(this);

        private void EnsureFaceRuntimeStateUpdated()
        {
            if (!FaceRuntimeActivationGuard.CanProcessFaceRuntime(this, Pawn))
                return;

            var runtimeState = CurrentFaceRuntimeState;
            var compiledData = CurrentFaceRuntimeCompiledData;
            int currentTick = Find.TickManager?.TicksGame ?? 0;

            bool shouldRefresh = FaceRuntimeSyncCoordinator.UpdateTrackAndLodIfNeeded(
                Pawn!,
                this,
                runtimeState,
                compiledData,
                currentTick);

            EffectiveFaceStateSnapshot effectiveState = BuildEffectiveFaceStateSnapshot();
            FaceRuntimeSyncCoordinator.SyncEffectiveState(runtimeState, effectiveState);

            if (shouldRefresh)
                RequestRenderRefresh();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            SkinLifecycleRecoveryCoordinator.RestoreAfterSpawn(this);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn? pawn = Pawn;
            if (pawn == null || !pawn.Spawned) return;

            FaceRuntimeRefreshCoordinator.FlushDeferredRefresh(this);
            TickAbilityFaceOverride();

            if (FaceRuntimeActivationGuard.IsFaceRuntimeEnabled(this))
            {
                FaceRuntimeTickCoordinator.Tick(this, pawn);
            }
        }

        private void UpdateExpressionState()
        {
            if (!FaceRuntimeActivationGuard.CanProcessFaceRuntime(this, Pawn)) return;

            var oldExp = curExpression;
            var pawn = Pawn!;

            curExpression = FaceExpressionStateResolver.ResolveExpression(pawn);

            if (oldExp != curExpression)
            {
                faceExpressionState.ResetAnimatedFrameTracking();
                RequestRenderRefresh();
            }
        }

        /// <summary>
        /// 检测当前帧动画是否应触发渲染更新（贴图切换时刷新）
        /// </summary>
        private void UpdateAnimatedExpressionFrame()
        {
            if (previewOverrides.PreviewExpression.HasValue)
            {
                faceExpressionState.ResetAnimatedFrameTracking();
                return;
            }

            var faceConfig = activeSkin?.faceConfig;
            if (faceConfig == null)
            {
                faceExpressionState.ResetAnimatedFrameTracking();
                return;
            }

            var expEntry = faceConfig.GetExpression(curExpression);
            if (expEntry == null || !expEntry.IsAnimated)
            {
                faceExpressionState.ResetAnimatedFrameTracking();
                return;
            }

            int currentFrameIndex = GetAnimatedExpressionFrameIndex(expEntry, faceExpressionState.expressionAnimTick);
            if (currentFrameIndex < 0)
            {
                faceExpressionState.ResetAnimatedFrameTracking();
                return;
            }

            if (faceExpressionState.lastAnimatedFrameIndex < 0
                || faceExpressionState.lastAnimatedExpression != curExpression)
            {
                faceExpressionState.lastAnimatedExpression = curExpression;
                faceExpressionState.lastAnimatedFrameIndex = currentFrameIndex;
                return;
            }

            if (faceExpressionState.lastAnimatedFrameIndex != currentFrameIndex)
            {
                faceExpressionState.lastAnimatedFrameIndex = currentFrameIndex;
                RequestRenderRefresh();
            }
        }

        private static int GetAnimatedExpressionFrameIndex(ExpressionTexPath expEntry, int tick)
        {
            if (expEntry?.frames == null || expEntry.frames.Count == 0)
                return -1;

            if (expEntry.frames.Count == 1)
                return 0;

            int totalDuration = 0;
            foreach (var frame in expEntry.frames)
                totalDuration += frame.durationTicks > 0 ? frame.durationTicks : 1;

            if (totalDuration <= 0)
                return 0;

            int normalizedTick = tick % totalDuration;
            int accumulated = 0;
            for (int i = 0; i < expEntry.frames.Count; i++)
            {
                int duration = expEntry.frames[i].durationTicks > 0 ? expEntry.frames[i].durationTicks : 1;
                accumulated += duration;
                if (normalizedTick < accumulated)
                    return i;
            }

            return expEntry.frames.Count - 1;
        }

        private void UpdateBlinkLogic()
        {
            if (previewOverrides.PreviewExpression.HasValue) return;
            if (curExpression == ExpressionType.Sleeping || curExpression == ExpressionType.Dead) return;

            if (faceExpressionState.IsBlinkActive)
            {
                if (faceExpressionState.ConsumeBlinkTick()) RequestRenderRefresh();
            }
            else
            {
                // 优化：眨眼概率检测由每 Tick 降低为每 10 Tick（约 0.17s）一次，
                // 调整概率保持等效眨眼频率（0.008 * 60 ≈ 0.08/s → 0.08 * 10 = 约0.08s触发间隔不变）
                if (Pawn!.IsHashIntervalTick(10) && Rand.Value < 0.08f)
                {
                    faceExpressionState.StartBlink(BlinkDuration);
                    RequestRenderRefresh();
                }
            }
        }

        public void SetPreviewExpressionOverride(ExpressionType? expression)
        {
            bool changed = previewOverrides.SetExpression(expression);

            if (expression.HasValue)
            {
                previewOverrides.SetRuntimeExpression(null);
                faceExpressionState.ClearBlink();
            }

            if (changed)
            {
                faceExpressionState.ResetAnimatedFrameTracking();
                RequestRenderRefresh();
            }
        }

        public void SetPreviewRuntimeExpression(ExpressionType? expression)
        {
            bool changed = previewOverrides.SetRuntimeExpression(expression);

            if (expression.HasValue)
            {
                previewOverrides.SetExpression(null);
                faceExpressionState.ClearBlink();
            }

            if (changed)
            {
                faceExpressionState.ResetAnimatedFrameTracking();
                RequestRenderRefresh();
            }
        }

        public ExpressionType GetEffectiveExpression()
            => EffectiveFaceStateEvaluator.ResolveExpression(this);

        /// <summary>当前是否处于 Blink 有效态（包含预览覆盖）</summary>
        public bool IsBlinkActive() => GetEffectiveExpression() == ExpressionType.Blink;

        public BlinkPhase GetBlinkPhase()
            => faceExpressionState.IsBlinkActive ? faceExpressionState.blinkPhase : BlinkPhase.None;

        /// <summary>返回当前眨眼进度（0~1）。前半段为闭合，后半段为回弹。</summary>
        public float GetBlinkProgress01()
        {
            if (!faceExpressionState.IsBlinkActive || BlinkDuration <= 0)
                return 0f;

            return GetBlinkPhase() switch
            {
                BlinkPhase.ClosingLid => faceExpressionState.GetBlinkPhaseProgress01() * 0.5f,
                BlinkPhase.HideBaseEyeParts => 0.5f,
                BlinkPhase.ShowReplacementEye => 0.5f + faceExpressionState.GetBlinkPhaseProgress01() * 0.1f,
                BlinkPhase.RestoreBaseEyeParts => 0.6f,
                BlinkPhase.OpeningLid => 0.6f + faceExpressionState.GetBlinkPhaseProgress01() * 0.4f,
                _ => 0f,
            };
        }

        public void SetPreviewMouthState(MouthState? state)
        {
            bool changed = previewOverrides.SetMouthState(state);
            if (changed)
                RequestRenderRefresh();
        }

        public void SetPreviewLidState(LidState? state)
        {
            bool changed = previewOverrides.SetLidState(state);
            if (changed)
                RequestRenderRefresh();
        }

        public void SetPreviewBrowState(BrowState? state)
        {
            bool changed = previewOverrides.SetBrowState(state);
            if (changed)
                RequestRenderRefresh();
        }

        public void SetPreviewEmotionOverlayState(EmotionOverlayState? state)
        {
            bool changed = previewOverrides.SetEmotionOverlayState(state);
            if (changed)
                RequestRenderRefresh();
        }

        public void ClearPreviewChannelOverrides()
        {
            bool changed = previewOverrides.ClearChannelOverrides();

            if (changed)
                RequestRenderRefresh();
        }

        public MouthState GetEffectiveMouthState()
            => EffectiveFaceStateEvaluator.ResolveMouthState(this);

        public LidState GetEffectiveLidState()
            => EffectiveFaceStateEvaluator.ResolveLidState(this);

        public BrowState GetEffectiveBrowState()
            => EffectiveFaceStateEvaluator.ResolveBrowState(this);

        public EmotionOverlayState GetEffectiveEmotionOverlayState()
            => EffectiveFaceStateEvaluator.ResolveEmotionOverlayState(this);

        public EyeAnimationVariant GetEffectiveEyeAnimationVariant()
            => ResolveEyeAnimationVariant(GetEffectiveExpression());

        public PupilScaleVariant GetEffectivePupilScaleVariant()
            => ResolvePupilScaleVariant(GetEffectiveExpression());

        /// <summary>
        /// 统一图层系统按 LayerRole 获取当前推荐状态后缀。
        /// 返回 null 表示该角色当前没有可用状态后缀。
        /// </summary>
        public string? GetChannelStateSuffix(LayerRole role)
        {
            switch (role)
            {
                case LayerRole.Mouth:
                    return GetEffectiveMouthState().ToString();

                case LayerRole.Lid:
                    return GetEffectiveLidState().ToString();

                case LayerRole.Brow:
                    return GetEffectiveBrowState().ToString();

                case LayerRole.Emotion:
                    EmotionOverlayState emotionState = GetEffectiveEmotionOverlayState();
                    return emotionState == EmotionOverlayState.None ? null : emotionState.ToString();

                case LayerRole.Eye:
                    return CurEyeDirection.ToString();

                default:
                    return null;
            }
        }

        private static MouthState ResolveMouthState(ExpressionType expression)
            => FaceChannelStateResolver.ResolveMouthState(expression);

        private static LidState ResolveLidState(ExpressionType expression)
            => FaceChannelStateResolver.ResolveLidState(expression);

        private static BrowState ResolveBrowState(ExpressionType expression)
            => FaceChannelStateResolver.ResolveBrowState(expression);

        private static EmotionOverlayState ResolveEmotionOverlayState(ExpressionType expression)
            => FaceChannelStateResolver.ResolveEmotionOverlayState(expression);

        public void ApplyAbilityFaceOverride(ExpressionType? expression, int durationTicks, float pupilBrightnessOffset, float pupilContrastOffset)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int resolvedDuration = Math.Max(1, durationTicks);
            abilityExpressionOverride = expression;
            abilityExpressionOverrideExpireTick = now + resolvedDuration;
            abilityPupilBrightnessOffset = pupilBrightnessOffset;
            abilityPupilContrastOffset = pupilContrastOffset;

            if (expression.HasValue)
            {
                faceExpressionState.ClearBlink();
                faceExpressionState.ResetAnimatedFrameTracking();
            }

            RequestRenderRefresh();
        }

        public bool IsAbilityExpressionOverrideActive()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return abilityExpressionOverrideExpireTick >= now
                && (abilityExpressionOverride.HasValue
                    || Math.Abs(abilityPupilBrightnessOffset) > 0.001f
                    || Math.Abs(abilityPupilContrastOffset) > 0.001f);
        }

        public float GetAbilityPupilBrightnessOffset()
            => IsAbilityExpressionOverrideActive() ? abilityPupilBrightnessOffset : 0f;

        public float GetAbilityPupilContrastOffset()
            => IsAbilityExpressionOverrideActive() ? abilityPupilContrastOffset : 0f;

        private void TickAbilityFaceOverride()
        {
            if (abilityExpressionOverrideExpireTick < 0)
            {
                return;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            if (abilityExpressionOverrideExpireTick >= now)
            {
                return;
            }

            if (abilityExpressionOverride.HasValue
                || Math.Abs(abilityPupilBrightnessOffset) > 0.001f
                || Math.Abs(abilityPupilContrastOffset) > 0.001f)
            {
                abilityExpressionOverride = null;
                abilityExpressionOverrideExpireTick = -1;
                abilityPupilBrightnessOffset = 0f;
                abilityPupilContrastOffset = 0f;
                faceExpressionState.ResetAnimatedFrameTracking();
                RequestRenderRefresh();
            }
        }

        public void SetWeaponCarryCastingWindow(int durationTicks)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int resolvedDuration = Math.Max(1, durationTicks);
            abilityRuntimeState.weaponCarryCastingUntilTick = now + resolvedDuration;
            RequestRenderRefresh();
        }

        private EyeAnimationVariant ResolveEyeAnimationVariant(ExpressionType expression)
        {
            if (previewOverrides.PreviewExpression.HasValue)
                return EyeAnimationVariant.NeutralOpen;

            if (faceExpressionState.IsBlinkActive)
            {
                return faceExpressionState.blinkPhase == BlinkPhase.ShowReplacementEye
                    ? EyeAnimationVariant.HappyClosedPeak
                    : EyeAnimationVariant.BlinkClosed;
            }

            int tick = Find.TickManager?.TicksGame ?? 0;
            int pawnSeed = Pawn?.thingIDNumber ?? 0;
            bool happyFamily = expression == ExpressionType.Happy
                || expression == ExpressionType.Cheerful
                || expression == ExpressionType.Lovin
                || expression == ExpressionType.SocialRelax;

            if (happyFamily)
            {
                int cycle = Mathf.Abs(pawnSeed + tick / 45) % 6;
                return cycle switch
                {
                    0 => EyeAnimationVariant.HappyOpen,
                    1 => EyeAnimationVariant.HappySoft,
                    2 => EyeAnimationVariant.HappyClosedPeak,
                    3 => EyeAnimationVariant.HappyClosedPeak,
                    4 => EyeAnimationVariant.HappyOpen,
                    _ => EyeAnimationVariant.HappySoft,
                };
            }

            if (expression == ExpressionType.Working || expression == ExpressionType.Reading)
            {
                int cycle = Mathf.Abs(pawnSeed + tick / 30) % 4;
                return cycle switch
                {
                    0 => EyeAnimationVariant.WorkFocusCenter,
                    1 => EyeAnimationVariant.WorkFocusDown,
                    2 => EyeAnimationVariant.WorkFocusDown,
                    _ => EyeAnimationVariant.WorkFocusUp,
                };
            }

            if (expression == ExpressionType.Shock)
            {
                int cycle = Mathf.Abs(pawnSeed + tick / 12) % 3;
                return cycle switch
                {
                    0 => EyeAnimationVariant.ShockWide,
                    1 => EyeAnimationVariant.ScaredFlinch,
                    _ => EyeAnimationVariant.ShockWide,
                };
            }

            if (expression == ExpressionType.Scared)
            {
                int cycle = Mathf.Abs(pawnSeed + tick / 18) % 4;
                return cycle switch
                {
                    0 => EyeAnimationVariant.ScaredWide,
                    1 => EyeAnimationVariant.ScaredFlinch,
                    2 => EyeAnimationVariant.ScaredWide,
                    _ => EyeAnimationVariant.ShockWide,
                };
            }

            int neutralCycle = Mathf.Abs(pawnSeed + tick / 60) % 6;
            return neutralCycle switch
            {
                0 => EyeAnimationVariant.NeutralOpen,
                1 => EyeAnimationVariant.NeutralSoft,
                2 => EyeAnimationVariant.NeutralLookDown,
                3 => EyeAnimationVariant.NeutralOpen,
                4 => EyeAnimationVariant.NeutralGlance,
                _ => EyeAnimationVariant.NeutralSoft,
            };
        }

        private PupilScaleVariant ResolvePupilScaleVariant(ExpressionType expression)
        {
            if (previewOverrides.PreviewExpression.HasValue)
                return PupilScaleVariant.Neutral;

            if (faceExpressionState.IsBlinkActive)
                return PupilScaleVariant.BlinkHidden;

            int tick = Find.TickManager?.TicksGame ?? 0;
            int pulse = Mathf.Abs((Pawn?.thingIDNumber ?? 0) + tick / 24) % 4;

            return expression switch
            {
                ExpressionType.Shock => pulse < 3 ? PupilScaleVariant.DilatedMax : PupilScaleVariant.ScaredPulse,
                ExpressionType.Scared => pulse < 2 ? PupilScaleVariant.ScaredPulse : PupilScaleVariant.DilatedMax,
                ExpressionType.Happy => pulse == 0 ? PupilScaleVariant.Contracted : PupilScaleVariant.SlightlyContracted,
                ExpressionType.Cheerful => pulse <= 1 ? PupilScaleVariant.Contracted : PupilScaleVariant.SlightlyContracted,
                ExpressionType.Lovin => PupilScaleVariant.SlightlyContracted,
                ExpressionType.Working => PupilScaleVariant.Focus,
                ExpressionType.Reading => PupilScaleVariant.Focus,
                ExpressionType.Angry => PupilScaleVariant.Focus,
                _ => PupilScaleVariant.Neutral,
            };
        }

        private void UpdateEyeAnimationVariant()
        {
            if (Pawn == null || previewOverrides.PreviewExpression.HasValue)
                return;

            FaceRuntimeState runtimeState = CurrentFaceRuntimeState;
            EyeAnimationVariant previousEye = runtimeState.eyeDirectionRuntimeVariant;
            PupilScaleVariant previousPupil = runtimeState.pupilScaleRuntimeVariant;
            ExpressionType expression = GetEffectiveExpression();
            EyeAnimationVariant nextEye = ResolveEyeAnimationVariant(expression);
            PupilScaleVariant nextPupil = ResolvePupilScaleVariant(expression);
            if (previousEye != nextEye || previousPupil != nextPupil)
            {
                runtimeState.eyeDirectionRuntimeVariant = nextEye;
                runtimeState.pupilScaleRuntimeVariant = nextPupil;
                RequestRenderRefresh();
            }
        }

        public bool IsWeaponCarryCastingNow()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return abilityRuntimeState.weaponCarryCastingUntilTick >= now;
        }

        /// <summary>获取当前帧动画 Tick（供 FaceComponent 渲染时定位帧）</summary>
        public int GetExpressionAnimTick() => faceExpressionState.expressionAnimTick;

        // ─────────────────────────────────────────────
        // 眼睛方向 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 获取当前有效的眼睛方向。
        /// 若编辑器设置了预览覆盖，优先返回覆盖值；否则返回运行时推断值。
        /// </summary>
        public EyeDirection CurEyeDirection
        {
            get => EffectiveFaceStateEvaluator.ResolveEyeDirection(this);
        }

        /// <summary>设置编辑器预览方向覆盖（null = 取消覆盖，恢复自动）</summary>
        public void SetPreviewEyeDirection(EyeDirection? dir)
        {
            bool changed = previewOverrides.SetEyeDirection(dir);
            if (changed)
                RequestRenderRefresh();
        }

        /// <summary>
        /// 根据 Pawn 当前状态推断眼睛注视方向，并在发生变化时触发渲染刷新。
        /// 推断规则（优先级从高到低）：
        ///   1. 死亡/倒地/睡眠 → Center
        ///   2. 有 Job 目标单元 → 按目标相对方向映射 Left/Right/Up/Down
        ///   3. 当前行走朝向推断（Rotation） → 对应方向
        ///   4. 默认 Center
        /// </summary>
        private void UpdateEyeDirectionState()
        {
            if (!FaceRuntimeActivationGuard.CanProcessEyeDirection(this, Pawn)) return;

            var pawn = Pawn!;
            EyeDirection resolvedDirection = EyeDirectionStateResolver.ResolveDirection(pawn);

            if (eyeDirectionState.SetDirection(resolvedDirection))
                RequestRenderRefresh();
        }

        public void RequestRenderRefresh()
        {
            Pawn? pawn = Pawn;
            if (pawn?.Drawer?.renderer == null)
                return;

            int currentTick = Find.TickManager?.TicksGame ?? -1;
            if (!CharacterStudioPerformanceStats.TryBeginRenderRefresh(pawn, currentTick))
                return;

            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
            // RequestRenderRefresh 只做当前 pawn 的轻量图形 dirty；
            // 不在这里触发 RefreshHiddenNodes / ForceRebuildRenderTree，
            // 避免普通状态变化经由全局渲染树链路误伤其他 pawn 的服装显示。
        }

        public void EnsureFaceRuntimeStateReadyForPreview()
        {
            if (!FaceRuntimeActivationGuard.CanProcessFaceRuntime(this, Pawn))
                return;

            var runtimeState = CurrentFaceRuntimeState;
            var compiledData = CurrentFaceRuntimeCompiledData;
            int currentTick = Find.TickManager?.TicksGame ?? 0;

            FaceRuntimePolicy.UpdateRuntimeState(Pawn!, this, runtimeState, compiledData, currentTick);
            FaceRuntimeSyncCoordinator.ResetDirtyFlags(runtimeState);

            EffectiveFaceStateSnapshot effectiveState = BuildEffectiveFaceStateSnapshot();
            FaceRuntimeSyncCoordinator.PreparePreviewState(runtimeState, effectiveState);
        }

        internal SkinApplicationWriteResult ClearSkinWithResult()
        {
            return SkinApplicationCoordinator.Clear(this);
        }

        private void TryApplyDefaultRaceSkinIfNeeded()
        {
            SkinLifecycleRecoveryCoordinator.TryApplyDefaultRaceSkin(this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref activeSkinDefName, "activeSkinDefName");
            Scribe_Values.Look(ref activeSkinFromDefaultRaceBinding, "activeSkinFromDefaultRaceBinding", false);
            Scribe_Deep.Look(ref activeAbilityLoadout, "activeAbilityLoadout");
            abilityRuntimeState.ExposeData();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                activeAbilityLoadout ??= null;
                SkinLifecycleRecoveryCoordinator.RestoreAfterLoad(this);
            }

            abilityRuntimeState.Normalize();
        }
    }

    public class CompProperties_PawnSkin : CompProperties
    {
        public CompProperties_PawnSkin() => this.compClass = typeof(CompPawnSkin);
    }

    public enum MouthState
    {
        Normal,
        Smile,
        Open,
        Down,
        Sleep
    }

    public enum LidState
    {
        Normal,
        Blink,
        Half,
        Close,
        Happy
    }

    public enum BrowState
    {
        Normal,
        Angry,
        Sad,
        Happy
    }

    public enum EmotionOverlayState
    {
        None,
        Blush,
        Tear,
        Sweat,
        Gloomy,
        Lovin
    }

    public enum BlinkPhase
    {
        None,
        ClosingLid,
        HideBaseEyeParts,
        ShowReplacementEye,
        RestoreBaseEyeParts,
        OpeningLid
    }

    public enum EyeAnimationVariant
    {
        NeutralOpen,
        NeutralSoft,
        NeutralLookDown,
        NeutralGlance,
        WorkFocusCenter,
        WorkFocusDown,
        WorkFocusUp,
        HappyOpen,
        HappySoft,
        HappyClosedPeak,
        ShockWide,
        ScaredWide,
        ScaredFlinch,
        BlinkClosed
    }

    public enum PupilScaleVariant
    {
        Neutral,
        Focus,
        SlightlyContracted,
        Contracted,
        Dilated,
        DilatedMax,
        ScaredPulse,
        BlinkHidden
    }
}