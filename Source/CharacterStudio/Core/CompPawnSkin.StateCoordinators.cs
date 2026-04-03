using System;
using CharacterStudio.Attributes;
using CharacterStudio.Performance;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    public partial class CompPawnSkin
    {
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
            public readonly string overlaySemanticKey;
            public readonly EyeAnimationVariant eyeVariant;
            public readonly PupilScaleVariant pupilVariant;

            public EffectiveFaceStateSnapshot(
                ExpressionType expression,
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
                    || !string.Equals(runtimeState.currentOverlaySemanticKey ?? string.Empty, snapshot.overlaySemanticKey ?? string.Empty, StringComparison.Ordinal)
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

            public static string ResolveOverlaySemanticKey(CompPawnSkin owner)
            {
                ExpressionType expression = ResolveExpression(owner);
                return ResolveOverlaySemanticKey(owner, expression);
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

                PawnFaceConfig? faceConfig = owner.ActiveSkin?.faceConfig;
                if (faceConfig != null)
                    return faceConfig.ResolveEmotionOverlayState(expression);

                return CompPawnSkin.ResolveEmotionOverlayState(expression);
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
                    if (delta.LengthHorizontalSquared > 1.1f)
                        return MapDeltaToEyeDirection(delta, pawn.Rotation);
                }

                int tick = Find.TickManager?.TicksGame ?? 0;
                int pawnSeed = pawn.thingIDNumber;
                int cycle = Mathf.Abs(pawnSeed + tick / 160) % 12;

                return cycle switch
                {
                    1 => EyeDirection.Left,
                    3 => EyeDirection.Right,
                    5 => EyeDirection.Up,
                    7 => EyeDirection.Down,
                    _ => MapRotationToEyeDirection(pawn.Rotation)
                };
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
                owner.EnsureFaceRuntimeStateUpdated();
                owner.faceExpressionState.AdvanceAnimTick();

                if (ShouldUpdateExpression(pawn))
                    owner.UpdateExpressionState();

                owner.UpdateAnimatedExpressionFrame();
                owner.UpdateBlinkLogic();
                owner.UpdateEyeAnimationVariant();

                if (ShouldUpdateEyeDirection(owner, pawn))
                    owner.UpdateEyeDirectionState();

                owner.UpdateGlobalFaceDriveState();
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
    }
}
