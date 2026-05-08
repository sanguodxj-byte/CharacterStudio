using System;
using CharacterStudio.Abilities;
using CharacterStudio.Attributes;
using CharacterStudio.Performance;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    public partial class CompPawnSkin
    {
        internal sealed class FaceExpressionRuntimeState
        {
            public ExpressionType currentExpression = ExpressionType.Neutral;
            public int shockExpireTick = -1;
            public LayeredFacePartSide winkSide = LayeredFacePartSide.None;
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

            // ── 待机微表情调度（零 GC，仅值类型字段） ──
            public int idleMicroExpNextTick = -1;                          // 下次微表情触发 tick，-1 = 未调度
            public ExpressionType idleMicroExpression = ExpressionType.Neutral;  // 当前激活的微表情
            public int idleMicroExpEndTick = -1;                           // 微表情结束 tick，-1 = 未激活

            // ── 待机张望调度 ──
            public int idleGazeNextTick = -1;                              // 下次张望切换 tick
            public EyeDirection idleGazeTarget = EyeDirection.Center;     // 当前张望目标方向

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

            public void TriggerShock(int nowTick, int durationTicks)
            {
                shockExpireTick = nowTick + Math.Max(1, durationTicks);
            }

            public bool IsShockActive(int nowTick)
                => shockExpireTick >= nowTick;

            public bool ClearExpiredShock(int nowTick)
            {
                if (shockExpireTick < 0 || shockExpireTick >= nowTick)
                    return false;

                shockExpireTick = -1;
                return true;
            }

            public bool SetWinkSide(LayeredFacePartSide side)
            {
                LayeredFacePartSide normalized = side == LayeredFacePartSide.Right
                    ? LayeredFacePartSide.Right
                    : side == LayeredFacePartSide.Left
                        ? LayeredFacePartSide.Left
                        : LayeredFacePartSide.None;

                if (winkSide == normalized)
                    return false;

                winkSide = normalized;
                return true;
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
            public readonly ExpressionType baseExpressionBeforeBlink;
            public readonly LayeredFacePartSide winkSide;
            public readonly EyeDirection eyeDirection;
            public readonly MouthState mouthState;
            public readonly LidState lidState;
            public readonly BrowState browState;
            public readonly EmotionOverlayState emotionOverlayState;
            public readonly string overlaySemanticKey;
            public readonly EyeAnimationVariant eyeVariant;
            public readonly PupilScaleVariant pupilVariant;

            public EffectiveFaceStateSnapshot(
                ExpressionType expression,
                ExpressionType baseExpressionBeforeBlink,
                LayeredFacePartSide winkSide,
                EyeDirection eyeDirection,
                MouthState mouthState,
                LidState lidState,
                BrowState browState,
                EmotionOverlayState emotionOverlayState,
                string overlaySemanticKey,
                EyeAnimationVariant eyeVariant,
                PupilScaleVariant pupilVariant)
            {
                this.expression = expression;
                this.baseExpressionBeforeBlink = baseExpressionBeforeBlink;
                this.winkSide = winkSide;
                this.eyeDirection = eyeDirection;
                this.mouthState = mouthState;
                this.lidState = lidState;
                this.browState = browState;
                this.emotionOverlayState = emotionOverlayState;
                this.overlaySemanticKey = overlaySemanticKey ?? string.Empty;
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
                if (runtimeState.trackDirty || runtimeState.lodDirty || runtimeState.compiledDataDirty)
                    return true;

                return currentTick >= runtimeState.nextWorldUpdateTick;
            }

            private static bool HasEffectiveFaceStateChanged(FaceRuntimeState runtimeState, EffectiveFaceStateSnapshot snapshot)
            {
                return runtimeState.currentExpression != snapshot.expression
                    || runtimeState.baseExpressionBeforeBlink != snapshot.baseExpressionBeforeBlink
                    || runtimeState.winkSide != snapshot.winkSide
                    || runtimeState.currentEyeDirection != snapshot.eyeDirection
                    || runtimeState.currentMouthState != snapshot.mouthState
                    || runtimeState.currentLidState != snapshot.lidState
                    || runtimeState.currentBrowState != snapshot.browState
                    || runtimeState.currentEmotionOverlayState != snapshot.emotionOverlayState
                    || !string.Equals(runtimeState.currentOverlaySemanticKey ?? string.Empty, snapshot.overlaySemanticKey ?? string.Empty, StringComparison.Ordinal)
                    || runtimeState.eyeDirectionRuntimeVariant != snapshot.eyeVariant
                    || runtimeState.pupilScaleRuntimeVariant != snapshot.pupilVariant;
            }

            private static void ApplyEffectiveFaceState(FaceRuntimeState runtimeState, EffectiveFaceStateSnapshot snapshot)
            {
                runtimeState.currentExpression = snapshot.expression;
                runtimeState.baseExpressionBeforeBlink = snapshot.baseExpressionBeforeBlink;
                runtimeState.winkSide = snapshot.winkSide;
                runtimeState.currentEyeDirection = snapshot.eyeDirection;
                runtimeState.currentMouthState = snapshot.mouthState;
                runtimeState.currentLidState = snapshot.lidState;
                runtimeState.currentBrowState = snapshot.browState;
                runtimeState.currentEmotionOverlayState = snapshot.emotionOverlayState;
                runtimeState.currentOverlaySemanticKey = snapshot.overlaySemanticKey ?? string.Empty;
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
                    case ExpressionType.Wink:
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

                    case ExpressionType.Wink:
                        return LidState.Happy;

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
                    case ExpressionType.Wink:
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

                    case ExpressionType.Sleeping:
                        return EmotionOverlayState.Sleep;

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

                // 预计算 needs 表情，供所有通道的隐式覆盖共用，
                // 避免每个通道独立重复调用 ResolveNeedsExpression
                ExpressionType? needsExprOverride = null;
                if (owner.Pawn != null && expression != ExpressionType.Neutral)
                {
                    ExpressionType needsExpr = FaceExpressionStateResolver.ResolveNeedsExpression(owner.Pawn);
                    if (needsExpr != expression && needsExpr != ExpressionType.Neutral)
                        needsExprOverride = needsExpr;
                }

                return new EffectiveFaceStateSnapshot(
                    expression,
                    ResolveBaseExpressionBeforeBlink(owner, expression),
                    ResolveWinkSide(owner, expression),
                    ResolveEyeDirection(owner),
                    ResolveMouthState(owner, expression, needsExprOverride),
                    ResolveLidState(owner, expression, needsExprOverride),
                    ResolveBrowState(owner, expression, needsExprOverride),
                    ResolveEmotionOverlayState(owner, expression, needsExprOverride),
                    ResolveOverlaySemanticKey(owner, expression),
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
                    return owner.AbilityExpressionOverride ?? owner.curExpression;

                Pawn? pawn = owner.Pawn;
                int now = Find.TickManager?.TicksGame ?? 0;
                if (pawn != null
                    && owner.faceExpressionState.IsShockActive(now)
                    && !pawn.Dead
                    && !pawn.Downed
                    && !RestUtility.InBed(pawn))
                {
                    return ExpressionType.Shock;
                }

                return owner.curExpression;
            }

            private static ExpressionType ResolveBaseExpressionBeforeBlink(CompPawnSkin owner, ExpressionType effectiveExpression)
            {
                if (effectiveExpression != ExpressionType.Blink)
                    return effectiveExpression;

                if (owner.previewOverrides.PreviewExpression.HasValue)
                    return owner.previewOverrides.PreviewExpression.Value;

                if (owner.previewOverrides.PreviewRuntimeExpression.HasValue)
                    return owner.previewOverrides.PreviewRuntimeExpression.Value;

                if (owner.IsAbilityExpressionOverrideActive())
                    return owner.AbilityExpressionOverride ?? owner.curExpression;

                Pawn? pawn = owner.Pawn;
                int now = Find.TickManager?.TicksGame ?? 0;
                if (pawn != null
                    && owner.faceExpressionState.IsShockActive(now)
                    && !pawn.Dead
                    && !pawn.Downed
                    && !RestUtility.InBed(pawn))
                {
                    return ExpressionType.Shock;
                }

                return owner.curExpression;
            }

            public static EyeDirection ResolveEyeDirection(CompPawnSkin owner)
                => owner.previewOverrides.PreviewEyeDirection ?? owner.curEyeDirection;

            public static LayeredFacePartSide ResolveWinkSide(CompPawnSkin owner, ExpressionType expression)
            {
                if (expression != ExpressionType.Wink)
                    return LayeredFacePartSide.None;

                if (owner.previewOverrides.PreviewWinkSide.HasValue)
                    return owner.previewOverrides.PreviewWinkSide.Value;

                if (owner.faceExpressionState.winkSide == LayeredFacePartSide.Left
                    || owner.faceExpressionState.winkSide == LayeredFacePartSide.Right)
                    return owner.faceExpressionState.winkSide;

                return LayeredFacePartSide.Left;
            }

            public static MouthState ResolveMouthState(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                ExpressionType? needsOverride = ResolveNeedsOverride(owner, expression);
                return ResolveMouthState(owner, expression, needsOverride);
            }

            public static LidState ResolveLidState(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                ExpressionType? needsOverride = ResolveNeedsOverride(owner, expression);
                return ResolveLidState(owner, expression, needsOverride);
            }

            public static BrowState ResolveBrowState(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                ExpressionType? needsOverride = ResolveNeedsOverride(owner, expression);
                return ResolveBrowState(owner, expression, needsOverride);
            }

            public static EmotionOverlayState ResolveEmotionOverlayState(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                ExpressionType? needsOverride = ResolveNeedsOverride(owner, expression);
                return ResolveEmotionOverlayState(owner, expression, needsOverride);
            }

            public static string ResolveOverlaySemanticKey(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                return ResolveOverlaySemanticKey(owner, expression);
            }

            /// <summary>
            /// 预计算 needs 表情覆盖。
            /// 当当前表情不反映心情状态且 needs 表情非 Neutral 时，返回 needs 表情供通道覆盖使用。
            /// </summary>
            private static ExpressionType? ResolveNeedsOverride(CompPawnSkin owner, ExpressionType expression)
            {
                if (owner.Pawn == null || expression == ExpressionType.Neutral)
                    return null;

                ExpressionType needsExpr = FaceExpressionStateResolver.ResolveNeedsExpression(owner.Pawn);
                if (needsExpr != expression && needsExpr != ExpressionType.Neutral)
                    return needsExpr;

                return null;
            }

            public static MouthState ResolveMouthState(CompPawnSkin owner, ExpressionType expression, ExpressionType? needsExprOverride)
            {
                if (owner.previewOverrides.PreviewMouthState.HasValue)
                    return owner.previewOverrides.PreviewMouthState.Value;

                // 显式配置覆盖
                var expConfig = owner.activeSkin?.faceConfig?.GetExpression(expression);
                if (expConfig?.mouthStateOverride.HasValue == true)
                    return expConfig.mouthStateOverride.Value;

                MouthState state = CompPawnSkin.ResolveMouthState(expression);
                if (state == MouthState.Normal && needsExprOverride.HasValue)
                {
                    MouthState needsState = CompPawnSkin.ResolveMouthState(needsExprOverride.Value);
                    if (needsState != MouthState.Normal)
                        return needsState;
                }

                return state;
            }

            public static LidState ResolveLidState(CompPawnSkin owner, ExpressionType expression, ExpressionType? needsExprOverride)
            {
                if (owner.previewOverrides.PreviewLidState.HasValue)
                    return owner.previewOverrides.PreviewLidState.Value;

                // 显式配置覆盖
                var expConfig = owner.activeSkin?.faceConfig?.GetExpression(expression);
                if (expConfig?.lidStateOverride.HasValue == true)
                    return expConfig.lidStateOverride.Value;

                LidState state = CompPawnSkin.ResolveLidState(expression);
                if (state == LidState.Normal && needsExprOverride.HasValue)
                {
                    LidState needsState = CompPawnSkin.ResolveLidState(needsExprOverride.Value);
                    if (needsState != LidState.Normal)
                        return needsState;
                }

                return state;
            }

            public static BrowState ResolveBrowState(CompPawnSkin owner, ExpressionType expression, ExpressionType? needsExprOverride)
            {
                if (owner.previewOverrides.PreviewBrowState.HasValue)
                    return owner.previewOverrides.PreviewBrowState.Value;

                // 显式配置覆盖
                var expConfig = owner.activeSkin?.faceConfig?.GetExpression(expression);
                if (expConfig?.browStateOverride.HasValue == true)
                    return expConfig.browStateOverride.Value;

                BrowState state = CompPawnSkin.ResolveBrowState(expression);
                if (state == BrowState.Normal && needsExprOverride.HasValue)
                {
                    BrowState needsState = CompPawnSkin.ResolveBrowState(needsExprOverride.Value);
                    if (needsState != BrowState.Normal)
                        return needsState;
                }

                return state;
            }

            public static EmotionOverlayState ResolveEmotionOverlayState(CompPawnSkin owner, ExpressionType expression, ExpressionType? needsExprOverride)
            {
                if (owner.previewOverrides.PreviewEmotionOverlayState.HasValue)
                    return owner.previewOverrides.PreviewEmotionOverlayState.Value;

                PawnFaceConfig? faceConfig = owner.ActiveSkin?.faceConfig;
                if (faceConfig != null)
                {
                    EmotionOverlayState cfgState = faceConfig.ResolveEmotionOverlayState(expression);
                    if (cfgState != EmotionOverlayState.None)
                        return cfgState;
                }

                EmotionOverlayState state = CompPawnSkin.ResolveEmotionOverlayState(expression);
                if (state == EmotionOverlayState.None && needsExprOverride.HasValue)
                {
                    EmotionOverlayState needsState = CompPawnSkin.ResolveEmotionOverlayState(needsExprOverride.Value);
                    if (needsState != EmotionOverlayState.None)
                        return needsState;
                }

                return state;
            }

            private static string ResolveOverlaySemanticKey(CompPawnSkin owner, ExpressionType expression)
            {
                PawnFaceConfig? faceConfig = owner.ActiveSkin?.faceConfig;
                if (faceConfig != null)
                    return faceConfig.ResolveOverlaySemanticKey(expression);

                EmotionOverlayState fallback = CompPawnSkin.ResolveEmotionOverlayState(expression);
                return PawnFaceConfig.MapLegacyEmotionStateToSemanticKey(fallback.ToString());
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

        internal static class FaceExpressionStateResolver
        {
            public static ExpressionType ResolveExpression(Pawn pawn, FaceExpressionRuntimeState state, PawnFaceConfig.IdleMicroExpressionConfig? config)
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

                ExpressionType baseExpression = ResolveNeedsExpression(pawn);
                return ResolveIdleMicroExpression(baseExpression, state, config, pawn.thingIDNumber, Find.TickManager?.TicksGame ?? 0);
            }

            /// <summary>
            /// 心情兼容的微表情调度。
            /// 根据当前基础表情和心情状态决定是否触发微表情，确保不与情感状态冲突。
            /// 
            /// 规则：
            /// - Hopeless / Sad：完全禁止微表情
            /// - Gloomy：仅允许 Wink
            /// - Neutral：Wink / SocialRelax / Reading / LayDown
            /// - Happy / Cheerful：仅允许 Wink（Wink 仅在开心时允许）
            /// </summary>
            private static ExpressionType ResolveIdleMicroExpression(
                ExpressionType baseExpression,
                FaceExpressionRuntimeState state,
                PawnFaceConfig.IdleMicroExpressionConfig? config,
                int pawnId,
                int now)
            {
                // 未配置或禁用时直接返回基础表情
                if (config == null || !config.enabled)
                    return baseExpression;

                // 心情兼容检查：确定允许的微表情候选
                ExpressionType[] candidates = GetMoodCompatibleCandidates(baseExpression);
                if (candidates == null || candidates.Length == 0)
                {
                    // 当前心情不允许微表情时，清理残留的活跃微表情状态，
                    // 避免心情恢复后短暂显示过期的旧微表情
                    if (state.idleMicroExpEndTick > 0)
                    {
                        state.idleMicroExpEndTick = -1;
                        state.idleMicroExpression = ExpressionType.Neutral;
                    }
                    return baseExpression;
                }

                // 正处于微表情中且未过期
                if (state.idleMicroExpEndTick > now)
                    return state.idleMicroExpression;

                // 微表情已过期，回到基础表情并调度下一次
                if (state.idleMicroExpEndTick > 0 && state.idleMicroExpEndTick <= now)
                {
                    state.idleMicroExpEndTick = -1;
                    state.idleMicroExpression = ExpressionType.Neutral;
                    // 立即调度下一次（不在此 tick 触发，避免连续两次微表情无间隔）
                    if (state.idleMicroExpNextTick <= now)
                    {
                        state.idleMicroExpNextTick = now + Rand.RangeSeeded(config.intervalMinTicks, config.intervalMaxTicks, (pawnId ^ 3571) ^ now);
                    }
                    return baseExpression;
                }

                // 检查是否到达触发时间
                if (state.idleMicroExpNextTick < 0)
                {
                    // 首次调度：随机延迟一个间隔，避免所有角色同时触发
                    state.idleMicroExpNextTick = now + Rand.RangeSeeded(config.intervalMinTicks, config.intervalMaxTicks, pawnId);
                    return baseExpression;
                }

                if (state.idleMicroExpNextTick > now)
                    return baseExpression;

                // 触发微表情
                ExpressionType chosen = candidates[Rand.RangeSeeded(0, candidates.Length, (pawnId ^ 4217) ^ now)];
                int duration = Rand.RangeSeeded(config.durationMinTicks, config.durationMaxTicks, (pawnId ^ 7919) ^ now);
                
                state.idleMicroExpression = chosen;
                state.idleMicroExpEndTick = now + duration;
                state.idleMicroExpNextTick = now + duration + Rand.RangeSeeded(config.intervalMinTicks, config.intervalMaxTicks, pawnId ^ (now ^ 1301));
                
                return chosen;
            }

            /// <summary>
            /// 根据基础表情返回心情兼容的微表情候选数组。
            /// 返回 null 或空数组表示该表情不允许微表情。
            /// </summary>
            private static ExpressionType[] GetMoodCompatibleCandidates(ExpressionType baseExpression)
            {
                switch (baseExpression)
                {
                    // 绝望/悲伤：完全禁止
                    case ExpressionType.Hopeless:
                    case ExpressionType.Sad:
                        return Array.Empty<ExpressionType>();

                    // 忧郁：不产生微表情，忧郁状态应保持
                    case ExpressionType.Gloomy:
                        return Array.Empty<ExpressionType>();

                    // 中立：SocialRelax / Reading / LayDown（Wink 仅 Happy 允许）
                    case ExpressionType.Neutral:
                        return new[] { ExpressionType.SocialRelax, ExpressionType.Reading, ExpressionType.LayDown };

                    // 开心/非常开心：Wink
                    case ExpressionType.Happy:
                    case ExpressionType.Cheerful:
                        return new[] { ExpressionType.Wink };

                    default:
                        return Array.Empty<ExpressionType>();
                }
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

            public static ExpressionType ResolveNeedsExpression(Pawn pawn)
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

        /// <summary>
        /// 眼睛方向状态解析器。
        /// 根据当前 Job 的目标位置（targetA）计算 Pawn 应注视的方向。
        ///
        /// 战斗状态下的瞳孔-方向联动机制：
        ///   - AttackMelee / AttackRanged / WaitCombat 时，CurJob.targetA 为攻击目标
        ///     （敌方 Pawn 或目标格子），GetJobTargetCell 提取其坐标，
        ///     通过 delta 计算将眼睛方向映射到目标方位。
        ///   - ResolvePupilScaleVariant 中对应使用 Focus 变体（瞳孔轻微收缩），
        ///     与此处的方向计算共同实现"战斗中瞳孔注视目标"的效果。
        ///   - 瞳孔的实际渲染偏移由 FaceTransformEvaluator 根据
        ///     EyeDirection + PupilScaleVariant 的组合参数驱动。
        /// </summary>
        private static class EyeDirectionStateResolver
        {
            public static EyeDirection ResolveDirection(Pawn pawn, FaceExpressionRuntimeState state, PawnFaceConfig.IdleMicroExpressionConfig? config)
            {
                if (pawn.Dead || pawn.Downed || RestUtility.InBed(pawn))
                    return EyeDirection.Center;

                // 从当前 Job 提取目标位置（攻击目标/工作目标/社交对象等），
                // 用于驱动眼睛注视方向。战斗 Job（AttackMelee/AttackRanged/WaitCombat）
                // 的 targetA 即为攻击对象，使角色在攻击时注视敌方目标。
                IntVec3 targetCell = GetJobTargetCell(pawn);
                if (targetCell.IsValid && pawn.Position.IsValid)
                {
                    IntVec3 delta = targetCell - pawn.Position;
                    if (delta.LengthHorizontalSquared > 1.1f)
                        return MapDeltaToEyeDirection(delta, pawn.Rotation);
                }

                // 待机张望：每人独立的随机停留调度
                int now = Find.TickManager?.TicksGame ?? 0;
                return ResolveIdleGaze(pawn, state, config, now);
            }

            /// <summary>
            /// 待机张望调度。每个角色独立维护下次切换时间和目标方向，
            /// 替代原有的机械 tick/160 % 12 周期。
            /// </summary>
            private static EyeDirection ResolveIdleGaze(
                Pawn pawn,
                FaceExpressionRuntimeState state,
                PawnFaceConfig.IdleMicroExpressionConfig? config,
                int now)
            {
                // 未到达切换时间，保持当前方向
                if (state.idleGazeNextTick > now)
                    return state.idleGazeTarget;

                int pawnId = pawn.thingIDNumber;
                int holdMin = config?.gazeHoldMinTicks ?? 240;
                int holdMax = config?.gazeHoldMaxTicks ?? 800;
                float shiftChance = config?.gazeShiftChance ?? 0.35f;

                // 决定新方向
                if (Rand.RangeSeeded(0f, 1f, (pawnId ^ 6131) ^ now) < shiftChance)
                {
                    // 随机偏移方向
                    int dir = Rand.RangeSeeded(0, 4, (pawnId ^ 31) ^ now);
                    state.idleGazeTarget = dir switch
                    {
                        0 => EyeDirection.Left,
                        1 => EyeDirection.Right,
                        2 => EyeDirection.Up,
                        _ => EyeDirection.Down
                    };
                }
                else
                {
                    // 回到面朝方向
                    state.idleGazeTarget = MapRotationToEyeDirection(pawn.Rotation);
                }

                state.idleGazeNextTick = now + Rand.RangeSeeded(holdMin, holdMax, (pawnId ^ 8461) ^ now);
                return state.idleGazeTarget;
            }

            /// <summary>
            /// 获取当前 Job 的目标格子坐标。
            /// 优先取 targetA.Thing.Position（如攻击目标 Pawn 的位置），
            /// 回退到 targetA.Cell（如 WaitCombat 的警戒点）。
            ///
            /// 战斗场景：AttackMelee 的 targetA 为近战目标 Pawn，
            /// AttackRanged/AttackStatic 的 targetA 为射击目标，
            /// WaitCombat 的 targetA 为警戒朝向的敌方。
            /// </summary>
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

            /// <summary>
            /// 计算基于目标距离的连续注视偏移矢量。
            /// 近处目标（2 格以内）偏移趋近 0，远处目标（20 格）偏移达到最大值 1.0。
            /// 中间距离线性插值。
            ///
            /// 渲染器 EvaluatePupil 将此偏移乘以瞳孔方向振幅，实现距离驱动的瞳孔位移梯度：
            ///   实际瞳孔偏移 = EyeDirection 固定偏移 + gazeOffset * max(dirAmplitude)
            /// </summary>
            public static Vector2 ResolveGazeOffset(Pawn pawn)
            {
                if (pawn.Dead || pawn.Downed || RestUtility.InBed(pawn))
                    return Vector2.zero;

                IntVec3 targetCell = GetJobTargetCell(pawn);
                if (!targetCell.IsValid || !pawn.Position.IsValid)
                    return Vector2.zero;

                IntVec3 delta = targetCell - pawn.Position;
                float horizontalDist = delta.LengthHorizontal;
                if (horizontalDist <= 1f)
                    return Vector2.zero;

                // 距离因子：2 格内为 0（近处瞳孔偏移小），20 格线性增长到 1.0（远处偏移大）
                float distanceFactor = Mathf.InverseLerp(2f, 20f, horizontalDist);

                // 归一化方向 * 距离因子
                // X 轴：RimWorld delta.x 正=东=视觉右，与渲染 offsetX 正方向一致，无需翻转
                // Z 轴：RimWorld delta.z 正=北=视觉上，但渲染 offsetZ 正值=视觉下（dirDownZ=正），
                //       需要取反：gazeOffset.y 为负时 → offsetZ 减小 → 视觉向上
                float dirX = (float)delta.x / horizontalDist;
                float dirZ = (float)delta.z / horizontalDist;

                return Vector2.ClampMagnitude(new Vector2(dirX * distanceFactor, -dirZ * distanceFactor), 1f);
            }

            private static EyeDirection MapDeltaToEyeDirection(IntVec3 delta, Rot4 rot)
            {
                if (delta == IntVec3.Zero) return EyeDirection.Center;

                int absX = Math.Abs(delta.x);
                int absZ = Math.Abs(delta.z);

                if (absX <= 1 && absZ <= 1) return EyeDirection.Center;

                if (absX >= absZ)
                    return delta.x > 0 ? EyeDirection.Right : EyeDirection.Left;

                return delta.z > 0 ? EyeDirection.Up : EyeDirection.Down;
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
                owner.RaiseSkinChangedGlobal();
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
                FaceRuntimeState? runtimeState = owner.faceRuntimeState;
                FaceRenderLod lod = runtimeState?.currentLod ?? FaceRenderLod.Dormant;
                int now = Find.TickManager?.TicksGame ?? 0;

                // ── 1. 状态评估（按间隔，或 dirty flag 触发） ──
                // Reduced/Dormant 也需要定期重新评估 LOD，否则选中/拉近视角后无法回到 HighFocus。
                if (FaceRuntimePolicy.ShouldEvaluateState(lod)
                    || runtimeState?.trackDirty == true
                    || runtimeState?.lodDirty == true
                    || runtimeState?.compiledDataDirty == true
                    || (runtimeState != null && now >= runtimeState.nextWorldUpdateTick))
                {
                    bool needsEvaluation = runtimeState == null
                        || runtimeState.trackDirty
                        || runtimeState.lodDirty
                        || runtimeState.compiledDataDirty
                        || now - runtimeState.lastStateEvaluationTick >= FaceRuntimePolicy.GetStateEvaluationInterval();

                    if (needsEvaluation)
                    {
                        owner.EnsureFaceRuntimeStateUpdated();
                        if (runtimeState != null)
                            runtimeState.lastStateEvaluationTick = now;
                        lod = runtimeState?.currentLod ?? FaceRenderLod.Dormant;
                    }
                }

                // ── 2. 非 HighFocus: 无动画，仅状态评估 ──
                if (lod != FaceRenderLod.HighFocus)
                    return;

                // ── 3. HighFocus 动画帧推进 ──
                int animInterval = FaceRuntimePolicy.GetHighFocusAnimationInterval();
                bool shouldAnimate = runtimeState == null
                    || now - runtimeState.lastAnimationTick >= animInterval;

                if (!shouldAnimate)
                    return;

                if (runtimeState != null)
                    runtimeState.lastAnimationTick = now;

                owner.faceExpressionState.AdvanceAnimTick();

                if (owner.faceExpressionState.ClearExpiredShock(now))
                    owner.MarkFaceGraphicDirty();

                owner.UpdateExpressionState();
                owner.UpdateAnimatedExpressionFrame();
                owner.UpdateBlinkLogic();
                owner.UpdateEyeAnimationVariant();

                if (FaceRuntimeActivationGuard.IsEyeDirectionEnabled(owner))
                    owner.UpdateEyeDirectionState();

                // 程序动画（眨眼缓动/注视平滑/呼吸脉动）
                owner.UpdateGlobalFaceDriveState();
            }
        }

        private sealed class FacePreviewOverrideState
        {
            public ExpressionType? PreviewExpression { get; private set; }
            public ExpressionType? PreviewRuntimeExpression { get; private set; }
            public LayeredFacePartSide? PreviewWinkSide { get; private set; }
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

            public bool SetWinkSide(LayeredFacePartSide? side)
            {
                if (PreviewWinkSide == side)
                    return false;

                PreviewWinkSide = side;
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
                    || PreviewEmotionOverlayState.HasValue
                    || PreviewWinkSide.HasValue;

                PreviewMouthState = null;
                PreviewLidState = null;
                PreviewBrowState = null;
                PreviewEmotionOverlayState = null;
                PreviewWinkSide = null;

                return changed;
            }
        }
    }
}
