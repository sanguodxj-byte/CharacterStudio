using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    public partial class CompPawnSkin
    {
        private void UpdateExpressionState()
        {
            if (!FaceRuntimeActivationGuard.CanProcessFaceRuntime(this, Pawn)) return;

            var oldExp = curExpression;
            var pawn = Pawn!;

            curExpression = FaceExpressionStateResolver.ResolveExpression(pawn);

            if (oldExp != curExpression)
            {
                faceExpressionState.ResetAnimatedFrameTracking();
                MarkFaceGraphicDirty(true);
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
                MarkFaceGraphicDirty();
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

        public void TriggerBlink()
        {
            if (faceExpressionState != null && !faceExpressionState.IsBlinkActive)
            {
                faceExpressionState.StartBlink(BlinkDuration);
                MarkFaceGraphicDirty();
            }
        }

        public void AdvancePreviewFaceAnimationTicks(int ticks)
        {
            if (ticks <= 0)
            {
                return;
            }

            if (!FaceRuntimeActivationGuard.CanProcessFaceRuntime(this, Pawn))
            {
                return;
            }

            EnsureFaceRuntimeStateReadyForPreview();

            bool blinkStateChanged = false;
            for (int i = 0; i < ticks; i++)
            {
                faceExpressionState.AdvanceAnimTick();
                if (!faceExpressionState.IsBlinkActive)
                {
                    continue;
                }

                BlinkPhase phaseBeforeTick = faceExpressionState.blinkPhase;
                int phaseTickBeforeAdvance = faceExpressionState.blinkPhaseTick;
                bool finishedBlink = faceExpressionState.ConsumeBlinkTick();
                blinkStateChanged |= finishedBlink
                    || phaseBeforeTick != faceExpressionState.blinkPhase
                    || phaseTickBeforeAdvance != faceExpressionState.blinkPhaseTick;
            }

            UpdateAnimatedExpressionFrame();
            UpdateEyeAnimationVariant();
            UpdateGlobalFaceDriveState();

            if (blinkStateChanged)
            {
                MarkFaceGraphicDirty();
            }
        }

        private void UpdateBlinkLogic()
        {
            if (previewOverrides.PreviewExpression.HasValue) return;
            if (curExpression == ExpressionType.Sleeping || curExpression == ExpressionType.Dead) return;

            if (faceExpressionState.IsBlinkActive)
            {
                // 眨眼相位推进只影响 transform（alpha/offset），不需要重建 graphic
                faceExpressionState.ConsumeBlinkTick();
                RequestTransformRefresh();
            }
            else
            {
                // 真实人类眨眼约每分钟 15~20 次（每 180~240 tick）。
                // IsHashIntervalTick(30) × Rand < 0.12 ≈ 每 250 tick 一次。
                if (Pawn!.IsHashIntervalTick(30) && Rand.Value < 0.12f)
                {
                    faceExpressionState.StartBlink(BlinkDuration);
                    MarkFaceGraphicDirty();
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
                MarkFaceGraphicDirty(true);
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
                MarkFaceGraphicDirty(true);
            }
        }

        public void SetPreviewWinkSide(LayeredFacePartSide? side)
        {
            LayeredFacePartSide? normalized = side.HasValue && side.Value != LayeredFacePartSide.None
                ? side.Value
                : null;

            bool changed = previewOverrides.SetWinkSide(normalized);
            if (normalized.HasValue)
                faceExpressionState.SetWinkSide(normalized.Value);

            if (changed)
                RequestTransformRefresh();
        }

        public LayeredFacePartSide GetEffectiveWinkSide()
        {
            if (GetEffectiveExpression() != ExpressionType.Wink)
                return LayeredFacePartSide.None;

            if (previewOverrides.PreviewWinkSide.HasValue)
                return previewOverrides.PreviewWinkSide.Value;

            EyeDirection previewEyeDirection = previewOverrides.PreviewEyeDirection ?? curEyeDirection;
            if (previewEyeDirection == EyeDirection.Right)
                return LayeredFacePartSide.Right;

            if (previewEyeDirection == EyeDirection.Left)
                return LayeredFacePartSide.Left;

            if (faceExpressionState.winkSide != LayeredFacePartSide.None)
                return faceExpressionState.winkSide;

            return LayeredFacePartSide.Left;
        }

        /// <summary>
        /// P-PERF: 获取当前帧的缓存 ID（渲染帧用 frameCount，非渲染用 TicksGame）
        /// </summary>
        private int GetCurrentEffectiveStateFrameId()
        {
            // 在渲染期间使用 frameCount（每帧变化一次），
            // 非 Tick 驱动路径下用 TicksGame
            return Time.frameCount;
        }

        private void EnsureEffectiveStateCache()
        {
            int frameId = GetCurrentEffectiveStateFrameId();
            if (_effectiveStateCacheFrameId == frameId)
                return;

            _effectiveStateCacheFrameId = frameId;
            _cachedEffectiveExpression = EffectiveFaceStateEvaluator.ResolveExpression(this);
            _cachedEffectiveMouthState = EffectiveFaceStateEvaluator.ResolveMouthState(this);
            _cachedEffectiveLidState = EffectiveFaceStateEvaluator.ResolveLidState(this);
            _cachedEffectiveBrowState = EffectiveFaceStateEvaluator.ResolveBrowState(this);
            _cachedEffectiveEmotionOverlayState = EffectiveFaceStateEvaluator.ResolveEmotionOverlayState(this);
            _cachedEffectiveOverlaySemanticKey = EffectiveFaceStateEvaluator.ResolveOverlaySemanticKey(this);
            _cachedEffectiveEyeVariant = ResolveEyeAnimationVariant(_cachedEffectiveExpression);
            _cachedEffectivePupilVariant = ResolvePupilScaleVariant(_cachedEffectiveExpression);
            _cachedEffectiveEyeDirection = EffectiveFaceStateEvaluator.ResolveEyeDirection(this);
        }

        /// <summary>P-PERF: 令有效状态缓存失效（表情/眨眼等状态变更时调用）</summary>
        public void InvalidateEffectiveStateCache()
        {
            _effectiveStateCacheFrameId = -1;
        }

        public ExpressionType GetEffectiveExpression()
        {
            EnsureEffectiveStateCache();
            return _cachedEffectiveExpression;
        }

        public bool IsBlinkActive()
        {
            // 内部眨眼状态机（faceExpressionState.IsBlinkActive）驱动实际的相位推进，
            // 而 curExpression 从不会被设置为 ExpressionType.Blink（它只反映需求/工作等状态）。
            // 因此必须直接检查内部状态机，而非通过缓存的表达式判断。
            return faceExpressionState.IsBlinkActive;
        }

        public BlinkPhase GetBlinkPhase()
            => faceExpressionState.IsBlinkActive ? faceExpressionState.blinkPhase : BlinkPhase.None;

        public float GetBlinkPhaseProgress01()
            => faceExpressionState.IsBlinkActive ? faceExpressionState.GetBlinkPhaseProgress01() : 0f;

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
                RequestTransformRefresh();
        }

        public void SetPreviewLidState(LidState? state)
        {
            bool changed = previewOverrides.SetLidState(state);
            if (changed)
                RequestTransformRefresh();
        }

        public void SetPreviewBrowState(BrowState? state)
        {
            bool changed = previewOverrides.SetBrowState(state);
            if (changed)
                RequestTransformRefresh();
        }

        public void SetPreviewEmotionOverlayState(EmotionOverlayState? state)
        {
            bool changed = previewOverrides.SetEmotionOverlayState(state);
            if (changed)
                RequestTransformRefresh();
        }

        public void ClearPreviewChannelOverrides()
        {
            bool changed = previewOverrides.ClearChannelOverrides();

            if (changed)
                RequestTransformRefresh();
        }

        public MouthState GetEffectiveMouthState()
        {
            EnsureEffectiveStateCache();
            return _cachedEffectiveMouthState;
        }

        public LidState GetEffectiveLidState()
        {
            EnsureEffectiveStateCache();
            return _cachedEffectiveLidState;
        }

        public BrowState GetEffectiveBrowState()
        {
            EnsureEffectiveStateCache();
            return _cachedEffectiveBrowState;
        }

        public EmotionOverlayState GetEffectiveEmotionOverlayState()
        {
            EnsureEffectiveStateCache();
            return _cachedEffectiveEmotionOverlayState;
        }

        public string GetEffectiveOverlaySemanticKey()
        {
            EnsureEffectiveStateCache();
            return _cachedEffectiveOverlaySemanticKey;
        }

        public EyeAnimationVariant GetEffectiveEyeAnimationVariant()
        {
            EnsureEffectiveStateCache();
            return _cachedEffectiveEyeVariant;
        }

        public PupilScaleVariant GetEffectivePupilScaleVariant()
        {
            EnsureEffectiveStateCache();
            return _cachedEffectivePupilVariant;
        }

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
                    string semanticKey = GetEffectiveOverlaySemanticKey();
                    return string.IsNullOrWhiteSpace(semanticKey) ? null : semanticKey;

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

        private EyeAnimationVariant ResolveEyeAnimationVariant(ExpressionType expression)
        {
            if (previewOverrides.PreviewExpression.HasValue)
                return EyeAnimationVariant.NeutralOpen;

            // 显式配置覆盖
            var expConfig = activeSkin?.faceConfig?.GetExpression(expression);
            if (expConfig?.eyeVariantOverride.HasValue == true)
                return expConfig.eyeVariantOverride.Value;

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

            if (Pawn != null)
            {
                ExpressionType needsExpr = FaceExpressionStateResolver.ResolveNeedsExpression(Pawn);
                if (needsExpr != expression && needsExpr != ExpressionType.Neutral)
                {
                    if (needsExpr == ExpressionType.Happy || needsExpr == ExpressionType.Cheerful || needsExpr == ExpressionType.Lovin || needsExpr == ExpressionType.SocialRelax)
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
                }
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

            // 显式配置覆盖
            var expConfig = activeSkin?.faceConfig?.GetExpression(expression);
            if (expConfig?.pupilVariantOverride.HasValue == true)
                return expConfig.pupilVariantOverride.Value;

            if (faceExpressionState.IsBlinkActive)
                return PupilScaleVariant.BlinkHidden;

            int tick = Find.TickManager?.TicksGame ?? 0;
            int pulse = Mathf.Abs((Pawn?.thingIDNumber ?? 0) + tick / 24) % 4;

            PupilScaleVariant needsVariant = PupilScaleVariant.Neutral;
            if (Pawn != null && expression != ExpressionType.Neutral)
            {
                ExpressionType needsExpr = FaceExpressionStateResolver.ResolveNeedsExpression(Pawn);
                if (needsExpr == ExpressionType.Lovin)
                    needsVariant = PupilScaleVariant.SlightlyContracted;
            }

            return expression switch
            {
                ExpressionType.Shock => pulse < 3 ? PupilScaleVariant.DilatedMax : PupilScaleVariant.ScaredPulse,
                ExpressionType.Scared => pulse < 2 ? PupilScaleVariant.ScaredPulse : PupilScaleVariant.DilatedMax,
                ExpressionType.Happy => PupilScaleVariant.Neutral,
                ExpressionType.Cheerful => PupilScaleVariant.Neutral,
                ExpressionType.Lovin => PupilScaleVariant.SlightlyContracted,
                ExpressionType.Working => PupilScaleVariant.Focus,
                ExpressionType.Reading => PupilScaleVariant.Focus,
                ExpressionType.Angry => PupilScaleVariant.Focus,

                // ── 战斗状态：瞳孔聚焦目标方向 ──
                // AttackMelee / AttackRanged / WaitCombat 时瞳孔收缩为 Focus，
                // 配合 EyeDirectionStateResolver 从 CurJob.targetA 计算目标位置，
                // 使瞳孔朝向攻击目标（敌方 Pawn 或目标格子）。
                // 眼睛方向由 EyeDirectionStateResolver.ResolveDirection 驱动，
                // 瞳孔缩放由 Focus 变体提供轻微收缩效果，模拟战斗聚焦感。
                ExpressionType.AttackMelee => PupilScaleVariant.Focus,
                ExpressionType.AttackRanged => PupilScaleVariant.Focus,
                ExpressionType.WaitCombat => PupilScaleVariant.Focus,

                _ => needsVariant,
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
                MarkFaceGraphicDirty();
            }
        }

        public EyeDirection CurEyeDirection
        {
            get
            {
                EnsureEffectiveStateCache();
                return _cachedEffectiveEyeDirection;
            }
        }

        public void SetPreviewEyeDirection(EyeDirection? dir)
        {
            bool changed = previewOverrides.SetEyeDirection(dir);
            if (changed)
                RequestTransformRefresh();
        }

        public void SetPreviewGazeOffset(Vector2? gazeOffset)
        {
            if (!FaceRuntimeActivationGuard.CanProcessFaceRuntime(this, Pawn))
                return;

            FaceRuntimeState runtimeState = CurrentFaceRuntimeState;
            Vector2 target = gazeOffset.HasValue
                ? Vector2.ClampMagnitude(gazeOffset.Value, 1f)
                : Vector2.zero;

            if ((runtimeState.gazeOffset - target).sqrMagnitude > 0.000001f)
            {
                runtimeState.gazeOffset = target;
                RequestTransformRefresh();
            }
        }

        private void UpdateEyeDirectionState()
        {
            if (!FaceRuntimeActivationGuard.CanProcessEyeDirection(this, Pawn)) return;

            var pawn = Pawn!;
            EyeDirection resolvedDirection = EyeDirectionStateResolver.ResolveDirection(pawn);
            bool directionChanged = eyeDirectionState.SetDirection(resolvedDirection);

            // 攻击状态（AttackMelee / AttackRanged / WaitCombat）时，
            // 根据目标距离计算连续注视偏移：近处瞳孔偏移小，远处偏移大。
            // 非攻击状态保持 gazeOffset = zero，不影响现有行为。
            ExpressionType expr = curExpression;
            bool isAttackExpression = expr == ExpressionType.AttackMelee
                || expr == ExpressionType.AttackRanged
                || expr == ExpressionType.WaitCombat;

            Vector2 targetGazeOffset = isAttackExpression
                ? EyeDirectionStateResolver.ResolveGazeOffset(pawn)
                : Vector2.zero;

            FaceRuntimeState runtimeState = CurrentFaceRuntimeState;
            bool gazeChanged = (runtimeState.gazeOffset - targetGazeOffset).sqrMagnitude > 0.000001f;
            if (gazeChanged)
                runtimeState.gazeOffset = targetGazeOffset;

            if (directionChanged || gazeChanged)
                RequestTransformRefresh();
        }

        private void UpdateGlobalFaceDriveState()
        {
            if (Pawn == null || !FaceRuntimeActivationGuard.CanProcessFaceRuntime(this, Pawn))
                return;

            FaceRuntimeState runtimeState = CurrentFaceRuntimeState;
            int tick = Find.TickManager?.TicksGame ?? 0;
            int pawnSeed = Pawn.thingIDNumber;

            float rawBlinkProgress = GetBlinkProgress01();
            if (rawBlinkProgress <= 0f)
            {
                runtimeState.blinkEased = 0f;
            }
            else if (rawBlinkProgress >= 1f)
            {
                runtimeState.blinkEased = 1f;
            }
            else
            {
                runtimeState.blinkEased = 0.5f - 0.5f * Mathf.Cos(rawBlinkProgress * Mathf.PI);
            }

            EyeDirection curDir = CurEyeDirection;
            Vector2 targetGaze = Vector2.zero;
            switch (curDir)
            {
                case EyeDirection.Left: targetGaze = new Vector2(-1f, 0f); break;
                case EyeDirection.Right: targetGaze = new Vector2(1f, 0f); break;
                case EyeDirection.Up: targetGaze = new Vector2(0f, 1f); break;
                case EyeDirection.Down: targetGaze = new Vector2(0f, -1f); break;
            }

            float blinkDrivenLidClosure = runtimeState.blinkEased;
            runtimeState.gazeOffset = Vector2.Lerp(runtimeState.gazeOffset, targetGaze, 0.3f);
            runtimeState.gazeOffset = Vector2.ClampMagnitude(
                runtimeState.gazeOffset + new Vector2(0f, blinkDrivenLidClosure),
                1f);
            runtimeState.breathingPulse = Mathf.Sin((tick + pawnSeed) * 0.0418f);
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
    }
}