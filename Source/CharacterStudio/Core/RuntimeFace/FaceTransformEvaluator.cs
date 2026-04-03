using UnityEngine;
using CharacterStudio.Rendering;

namespace CharacterStudio.Core
{
    internal readonly struct FaceTransformResult
    {
        public readonly bool hidden;
        public readonly float angle;
        public readonly Vector3 offset;
        public readonly Vector3 scale;

        public FaceTransformResult(bool hidden, float angle, Vector3 offset, Vector3 scale)
        {
            this.hidden = hidden;
            this.angle = angle;
            this.offset = offset;
            this.scale = scale;
        }

        public static FaceTransformResult Visible(float angle, Vector3 offset, Vector3 scale)
            => new FaceTransformResult(false, angle, offset, scale);

        public static FaceTransformResult Hidden()
            => new FaceTransformResult(true, 0f, Vector3.zero, Vector3.one);
    }

    internal readonly struct FaceTransformContext
    {
        public readonly LayeredFacePartType partType;
        public readonly LayeredFacePartSide side;
        public readonly string overlayId;
        public readonly EyeDirection eyeDirection;
        public readonly LidState lidState;
        public readonly BrowState browState;
        public readonly MouthState mouthState;
        public readonly EmotionOverlayState emotionState;
        public readonly bool isBlinkActive;
        public readonly BlinkPhase blinkPhase;
        public readonly bool hasReplacementEyeOverlay;
        public readonly EyeAnimationVariant eyeVariant;
        public readonly PupilScaleVariant pupilVariant;
        public readonly ExpressionType expression;
        public readonly float primaryWave;
        public readonly float slowWave;
        public readonly EyeRenderMode eyeRenderMode;

        public FaceTransformContext(
            LayeredFacePartType partType,
            LayeredFacePartSide side,
            string overlayId,
            EyeDirection eyeDirection,
            LidState lidState,
            BrowState browState,
            MouthState mouthState,
            EmotionOverlayState emotionState,
            bool isBlinkActive,
            BlinkPhase blinkPhase,
            bool hasReplacementEyeOverlay,
            EyeAnimationVariant eyeVariant,
            PupilScaleVariant pupilVariant,
            ExpressionType expression,
            float primaryWave,
            float slowWave,
            EyeRenderMode eyeRenderMode)
        {
            this.partType = partType;
            this.side = side;
            this.overlayId = overlayId ?? string.Empty;
            this.eyeDirection = eyeDirection;
            this.lidState = lidState;
            this.browState = browState;
            this.mouthState = mouthState;
            this.emotionState = emotionState;
            this.isBlinkActive = isBlinkActive;
            this.blinkPhase = blinkPhase;
            this.hasReplacementEyeOverlay = hasReplacementEyeOverlay;
            this.eyeVariant = eyeVariant;
            this.pupilVariant = pupilVariant;
            this.expression = expression;
            this.primaryWave = primaryWave;
            this.slowWave = slowWave;
            this.eyeRenderMode = eyeRenderMode;
        }
    }

    internal static class FaceTransformEvaluator
    {
        public static FaceTransformResult Evaluate(
            FaceTransformContext context,
            PawnFaceConfig.BrowMotionConfig browMotion,
            PawnFaceConfig.MouthMotionConfig mouthMotion,
            PawnFaceConfig.EmotionOverlayMotionConfig emotionOverlayMotion,
            PawnEyeDirectionConfig.LidMotionConfig lidMotion,
            PawnEyeDirectionConfig.EyeMotionConfig eyeMotion,
            PawnEyeDirectionConfig.PupilMotionConfig pupilMotion,
            float upperLidMoveDown)
        {
            return context.partType switch
            {
                LayeredFacePartType.Brow => EvaluateBrow(context, browMotion),
                LayeredFacePartType.Eye => EvaluateEye(context, eyeMotion),
                LayeredFacePartType.Pupil => EvaluatePupil(context, pupilMotion),
                LayeredFacePartType.UpperLid => EvaluateUpperLid(context, lidMotion, upperLidMoveDown),
                LayeredFacePartType.LowerLid => EvaluateLowerLid(context, lidMotion),
                LayeredFacePartType.ReplacementEye => EvaluateReplacementEye(context),
                LayeredFacePartType.Mouth => EvaluateMouth(context, mouthMotion),
                LayeredFacePartType.Hair => EvaluateHair(),
                LayeredFacePartType.Blush => EvaluateBlush(context, emotionOverlayMotion),
                LayeredFacePartType.Tear => EvaluateTear(context, emotionOverlayMotion),
                LayeredFacePartType.Sweat => EvaluateSweat(context, emotionOverlayMotion),
                LayeredFacePartType.Overlay => EvaluateOverlay(context),
                _ => FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one)
            };
        }

        private static FaceTransformResult EvaluateBrow(FaceTransformContext context, PawnFaceConfig.BrowMotionConfig motion)
        {
            return context.browState switch
            {
                BrowState.Angry => FaceTransformResult.Visible(
                    motion.angryAngleBase + context.primaryWave * motion.angryAngleWave,
                    new Vector3(0f, 0f, motion.angryOffsetZBase + context.slowWave * motion.angrySlowWaveOffsetZ),
                    new Vector3(motion.angryScaleX, 1f, motion.angryScaleZ)),
                BrowState.Sad => FaceTransformResult.Visible(
                    motion.sadAngleBase + context.primaryWave * motion.sadAngleWave,
                    new Vector3(0f, 0f, motion.sadOffsetZBase + context.slowWave * motion.sadSlowWaveOffsetZ),
                    new Vector3(motion.sadScaleX, 1f, motion.sadScaleZ)),
                BrowState.Happy => FaceTransformResult.Visible(
                    motion.happyAngleBase + context.primaryWave * motion.happyAngleWave,
                    new Vector3(0f, 0f, motion.happyOffsetZBase + context.slowWave * motion.happySlowWaveOffsetZ),
                    new Vector3(motion.happyScaleX, 1f, motion.happyScaleZ)),
                _ => FaceTransformResult.Visible(0f, new Vector3(0f, 0f, context.slowWave * motion.defaultSlowWaveOffsetZ), Vector3.one)
            };
        }

        private static float SideSign(LayeredFacePartSide side)
            => side switch
            {
                LayeredFacePartSide.Left => -1f,
                LayeredFacePartSide.Right => 1f,
                _ => 0f
            };

        private static float SideBias(LayeredFacePartSide side, float magnitude)
            => SideSign(side) * magnitude;

        private static FaceTransformResult EvaluateEye(FaceTransformContext context, PawnEyeDirectionConfig.EyeMotionConfig motion)
        {
            if (context.lidState == LidState.Blink || context.lidState == LidState.Close)
                return FaceTransformResult.Hidden();

            float sideSign = SideSign(context.side);
            float offsetX = SideBias(context.side, motion.sideBiasX);
            float offsetZ = motion.primaryWaveOffsetZ * context.primaryWave;

            switch (context.eyeDirection)
            {
                case EyeDirection.Left: offsetX = motion.dirLeftOffsetX; break;
                case EyeDirection.Right: offsetX = motion.dirRightOffsetX; break;
                case EyeDirection.Up: offsetZ += motion.dirUpOffsetZ; break;
                case EyeDirection.Down: offsetZ += motion.dirDownOffsetZ; break;
            }

            switch (context.eyeVariant)
            {
                case EyeAnimationVariant.NeutralSoft:
                    offsetZ += motion.neutralSoftOffsetZ;
                    break;
                case EyeAnimationVariant.NeutralLookDown:
                    offsetZ += motion.neutralLookDownOffsetZ;
                    break;
                case EyeAnimationVariant.NeutralGlance:
                    offsetX += (context.primaryWave > 0f ? motion.neutralGlanceWaveOffsetX : -motion.neutralGlanceWaveOffsetX) + sideSign * motion.neutralGlanceSideOffsetX;
                    break;
                case EyeAnimationVariant.WorkFocusDown:
                    offsetZ += motion.workFocusDownOffsetZ;
                    break;
                case EyeAnimationVariant.WorkFocusUp:
                    offsetZ += motion.workFocusUpOffsetZ;
                    break;
                case EyeAnimationVariant.HappySoft:
                    offsetZ += motion.happySoftOffsetZ;
                    break;
                case EyeAnimationVariant.ShockWide:
                    offsetZ += motion.shockWideOffsetZ;
                    break;
                case EyeAnimationVariant.ScaredWide:
                    offsetZ += motion.scaredWideOffsetZ;
                    offsetX += context.primaryWave * motion.scaredWideWaveOffsetX + sideSign * motion.scaredWideSideOffsetX;
                    break;
                case EyeAnimationVariant.ScaredFlinch:
                    offsetZ += motion.scaredFlinchOffsetZ;
                    offsetX += context.slowWave * motion.scaredFlinchWaveOffsetX + sideSign * motion.scaredFlinchSideOffsetX;
                    break;
                case EyeAnimationVariant.HappyClosedPeak:
                    return FaceTransformResult.Hidden();
            }

            float scaleZ = context.lidState == LidState.Half ? 0.92f : 1f;
            if (context.expression == ExpressionType.Shock || context.expression == ExpressionType.Scared)
                scaleZ = Mathf.Max(scaleZ, 1.06f + Mathf.Abs(context.primaryWave) * 0.02f);
            else if (context.expression == ExpressionType.Sleeping)
                scaleZ = Mathf.Min(scaleZ, 0.88f);
            else if (context.eyeVariant == EyeAnimationVariant.HappySoft)
                scaleZ = Mathf.Min(scaleZ, 0.90f);

            if (context.eyeVariant == EyeAnimationVariant.NeutralSoft)
                scaleZ = Mathf.Min(scaleZ, 0.95f);
            else if (context.eyeVariant == EyeAnimationVariant.NeutralLookDown)
                scaleZ = Mathf.Min(scaleZ, 0.93f);
            else if (context.eyeVariant == EyeAnimationVariant.ShockWide)
                scaleZ = Mathf.Max(scaleZ, 1.12f + Mathf.Abs(context.primaryWave) * 0.03f);
            else if (context.eyeVariant == EyeAnimationVariant.ScaredWide)
                scaleZ = Mathf.Max(scaleZ, 1.08f + Mathf.Abs(context.slowWave) * 0.02f);
            else if (context.eyeVariant == EyeAnimationVariant.ScaredFlinch)
                scaleZ = Mathf.Min(scaleZ, 0.94f);

            return FaceTransformResult.Visible(
                context.primaryWave * motion.baseAngleWave,
                new Vector3(offsetX, 0f, offsetZ + context.slowWave * motion.slowWaveOffsetZ),
                new Vector3(motion.scaleXBase + Mathf.Abs(context.slowWave) * motion.scaleXWaveAmplitude, 1f, scaleZ));
        }

        private static FaceTransformResult EvaluatePupil(FaceTransformContext context, PawnEyeDirectionConfig.PupilMotionConfig motion)
        {
            if (context.lidState == LidState.Blink || context.lidState == LidState.Close)
                return FaceTransformResult.Hidden();

            float sideSign = SideSign(context.side);
            float offsetX = SideBias(context.side, motion.sideBiasX);
            float offsetZ = context.slowWave * motion.slowWaveOffsetZ;

            switch (context.eyeDirection)
            {
                case EyeDirection.Left: offsetX = motion.dirLeftOffsetX; break;
                case EyeDirection.Right: offsetX = motion.dirRightOffsetX; break;
                case EyeDirection.Up: offsetZ += motion.dirUpOffsetZ; break;
                case EyeDirection.Down: offsetZ += motion.dirDownOffsetZ; break;
            }

            switch (context.eyeVariant)
            {
                case EyeAnimationVariant.NeutralSoft:
                    offsetZ += motion.neutralSoftOffsetZ;
                    break;
                case EyeAnimationVariant.NeutralLookDown:
                    offsetZ += motion.neutralLookDownOffsetZ;
                    break;
                case EyeAnimationVariant.NeutralGlance:
                    offsetX += (context.primaryWave > 0f ? motion.neutralGlanceWaveOffsetX : -motion.neutralGlanceWaveOffsetX) + sideSign * motion.neutralGlanceSideOffsetX;
                    break;
                case EyeAnimationVariant.WorkFocusDown:
                    offsetZ += motion.workFocusDownOffsetZ;
                    break;
                case EyeAnimationVariant.WorkFocusUp:
                    offsetZ += motion.workFocusUpOffsetZ;
                    break;
                case EyeAnimationVariant.HappyOpen:
                    offsetZ += motion.happyOpenOffsetZ;
                    break;
                case EyeAnimationVariant.ShockWide:
                    offsetZ += motion.shockWideOffsetZ;
                    break;
                case EyeAnimationVariant.ScaredWide:
                    offsetZ += motion.scaredWideOffsetZ;
                    offsetX += context.primaryWave * motion.scaredWideWaveOffsetX + sideSign * motion.scaredWideSideOffsetX;
                    break;
                case EyeAnimationVariant.ScaredFlinch:
                    offsetZ += motion.scaredFlinchOffsetZ;
                    offsetX += context.slowWave * motion.scaredFlinchWaveOffsetX + sideSign * motion.scaredFlinchSideOffsetX;
                    break;
                case EyeAnimationVariant.HappyClosedPeak:
                case EyeAnimationVariant.BlinkClosed:
                    return FaceTransformResult.Hidden();
            }

            float scale = context.pupilVariant switch
            {
                PupilScaleVariant.Focus => motion.focusScaleBase + Mathf.Abs(context.slowWave) * motion.focusScaleWave,
                PupilScaleVariant.SlightlyContracted => motion.slightlyContractedScaleBase + Mathf.Abs(context.slowWave) * motion.slightlyContractedScaleWave,
                PupilScaleVariant.Contracted => motion.contractedScaleBase + Mathf.Abs(context.slowWave) * motion.contractedScaleWave,
                PupilScaleVariant.Dilated => motion.dilatedScaleBase + Mathf.Abs(context.primaryWave) * motion.dilatedScaleWave,
                PupilScaleVariant.DilatedMax => motion.dilatedMaxScaleBase + Mathf.Abs(context.primaryWave) * motion.dilatedMaxScaleWave,
                PupilScaleVariant.ScaredPulse => motion.scaredPulseScaleBase + Mathf.Abs(context.primaryWave) * motion.scaredPulseScaleWave,
                PupilScaleVariant.BlinkHidden => 0f,
                _ => 1f,
            };

            if (context.expression == ExpressionType.Shock || context.expression == ExpressionType.Scared)
                scale = Mathf.Max(scale, motion.shockScaredMinScaleBase + Mathf.Abs(context.primaryWave) * motion.shockScaredMinScaleWave);
            else if (context.expression == ExpressionType.Happy || context.expression == ExpressionType.Cheerful)
                scale = Mathf.Min(scale, motion.happyMaxScaleBase + Mathf.Abs(context.slowWave) * motion.happyMaxScaleWave);
            else if (context.expression == ExpressionType.Sleeping)
                scale = motion.sleepingScale;
            else if (context.eyeVariant == EyeAnimationVariant.WorkFocusDown)
                scale = Mathf.Min(scale, motion.workFocusMaxScale);

            if (context.eyeVariant == EyeAnimationVariant.NeutralSoft)
                scale = Mathf.Min(scale, motion.neutralSoftMaxScale);
            else if (context.eyeVariant == EyeAnimationVariant.NeutralLookDown)
                scale = Mathf.Min(scale, motion.neutralLookDownMaxScale);
            else if (context.eyeVariant == EyeAnimationVariant.ShockWide)
                scale = Mathf.Max(scale, motion.shockWideMinScaleBase + Mathf.Abs(context.primaryWave) * motion.shockWideMinScaleWave);
            else if (context.eyeVariant == EyeAnimationVariant.ScaredWide)
                scale = Mathf.Max(scale, motion.scaredWideMinScaleBase + Mathf.Abs(context.slowWave) * motion.scaredWideMinScaleWave);
            else if (context.eyeVariant == EyeAnimationVariant.ScaredFlinch)
                scale = Mathf.Max(scale, motion.scaredFlinchMinScaleBase + Mathf.Abs(context.primaryWave) * motion.scaredFlinchMinScaleWave);

            if (context.pupilVariant == PupilScaleVariant.BlinkHidden)
                return FaceTransformResult.Hidden();

            if (context.eyeRenderMode == EyeRenderMode.UvOffset)
                return FaceTransformResult.Visible(0f, Vector3.zero, new Vector3(scale, 1f, scale));

            return FaceTransformResult.Visible(
                context.primaryWave * motion.transformAngleWave,
                new Vector3(offsetX + context.primaryWave * motion.finalWaveOffsetX, 0f, offsetZ),
                new Vector3(scale, 1f, scale));
        }

        private static FaceTransformResult EvaluateUpperLid(FaceTransformContext context, PawnEyeDirectionConfig.LidMotionConfig lidMotion, float upperLidMoveDown)
        {
            float replacementMoveDown = upperLidMoveDown;
            float sideBiasX = SideBias(context.side, lidMotion.upperSideBiasX);
            switch (context.lidState)
            {
                case LidState.Blink:
                    return FaceTransformResult.Visible(0f, new Vector3(sideBiasX, 0f, replacementMoveDown), new Vector3(lidMotion.upperBlinkScaleX, 1f, lidMotion.upperBlinkScaleZ));
                case LidState.Close:
                    return FaceTransformResult.Visible(0f, new Vector3(sideBiasX, 0f, replacementMoveDown), new Vector3(lidMotion.upperCloseScaleX, 1f, lidMotion.upperCloseScaleZ));
                case LidState.Half:
                {
                    float halfOffset = Mathf.Max(0f, replacementMoveDown - lidMotion.upperHalfBaseOffsetSubtract);
                    float halfScale = lidMotion.upperHalfScaleDefault;
                    if (context.eyeVariant == EyeAnimationVariant.NeutralSoft)
                    {
                        halfOffset += lidMotion.upperHalfNeutralSoftExtraOffset;
                        halfScale = lidMotion.upperHalfScaleNeutralSoft;
                    }
                    else if (context.eyeVariant == EyeAnimationVariant.NeutralLookDown)
                    {
                        halfOffset += lidMotion.upperHalfLookDownExtraOffset;
                        halfScale = lidMotion.upperHalfScaleLookDown;
                    }
                    else if (context.eyeVariant == EyeAnimationVariant.ScaredFlinch)
                    {
                        halfOffset += lidMotion.upperHalfScaredExtraOffset;
                        halfScale = lidMotion.upperHalfScaleScared;
                    }

                    return FaceTransformResult.Visible(0f, new Vector3(sideBiasX, 0f, halfOffset + context.slowWave * lidMotion.upperHalfSlowWaveOffset), new Vector3(lidMotion.upperCloseScaleX, 1f, halfScale));
                }
                case LidState.Happy:
                {
                    if (context.eyeVariant == EyeAnimationVariant.HappyClosedPeak)
                        return FaceTransformResult.Hidden();

                    float happyOffset = context.eyeVariant == EyeAnimationVariant.HappySoft ? lidMotion.upperHappySoftOffset : lidMotion.upperHappyOpenOffset;
                    float happyScale = context.eyeVariant == EyeAnimationVariant.HappySoft ? lidMotion.upperHappySoftScale : lidMotion.upperHappyOpenScale;
                    return FaceTransformResult.Visible(
                        lidMotion.upperHappyAngleBase + context.primaryWave * lidMotion.upperHappyAngleWave,
                        new Vector3(sideBiasX, 0f, happyOffset + context.slowWave * lidMotion.upperHappySlowWaveOffset),
                        new Vector3(lidMotion.upperHappyScaleX, 1f, happyScale));
                }
                default:
                    return FaceTransformResult.Visible(0f, new Vector3(sideBiasX, 0f, context.slowWave * lidMotion.upperDefaultSlowWaveOffset), Vector3.one);
            }
        }

        private static FaceTransformResult EvaluateLowerLid(FaceTransformContext context, PawnEyeDirectionConfig.LidMotionConfig lidMotion)
        {
            float sideBiasX = SideBias(context.side, lidMotion.lowerSideBiasX);
            switch (context.lidState)
            {
                case LidState.Blink:
                    return FaceTransformResult.Visible(0f, new Vector3(sideBiasX, 0f, lidMotion.lowerBlinkOffset), new Vector3(lidMotion.lowerBlinkScaleX, 1f, lidMotion.lowerBlinkScaleZ));
                case LidState.Close:
                    return FaceTransformResult.Visible(0f, new Vector3(sideBiasX, 0f, lidMotion.lowerCloseOffset), new Vector3(lidMotion.lowerCloseScaleX, 1f, lidMotion.lowerCloseScaleZ));
                case LidState.Half:
                    return FaceTransformResult.Visible(0f, new Vector3(sideBiasX, 0f, lidMotion.lowerHalfOffset + context.slowWave * lidMotion.lowerHalfSlowWaveOffset), new Vector3(lidMotion.lowerHalfScaleX, 1f, lidMotion.lowerHalfScaleZ));
                case LidState.Happy:
                    return FaceTransformResult.Visible(
                        lidMotion.lowerHappyAngleBase + context.primaryWave * lidMotion.lowerHappyAngleWave,
                        new Vector3(sideBiasX, 0f, lidMotion.lowerHappyOffset + context.slowWave * lidMotion.lowerHappySlowWaveOffset),
                        new Vector3(lidMotion.lowerHappyScaleX, 1f, lidMotion.lowerHappyScaleZ));
                default:
                    return FaceTransformResult.Visible(0f, new Vector3(sideBiasX, 0f, -context.slowWave * lidMotion.lowerDefaultSlowWaveOffset), Vector3.one);
            }
        }

        private static FaceTransformResult EvaluateMouth(FaceTransformContext context, PawnFaceConfig.MouthMotionConfig motion)
        {
            return context.mouthState switch
            {
                MouthState.Smile => FaceTransformResult.Visible(
                    context.slowWave * motion.smileAngleWave,
                    new Vector3(0f, 0f, motion.smileOffsetZBase + context.primaryWave * motion.smilePrimaryWaveOffsetZ),
                    new Vector3(motion.smileScaleXBase + Mathf.Abs(context.primaryWave) * motion.smileScaleXWave, 1f, motion.smileScaleZ)),
                MouthState.Open => FaceTransformResult.Visible(
                    context.primaryWave * motion.openAngleWave,
                    new Vector3(0f, 0f, motion.openOffsetZBase + Mathf.Abs(context.primaryWave) * motion.openPrimaryWaveOffsetZ),
                    new Vector3(motion.openScaleX, 1f, motion.openScaleZBase + Mathf.Abs(context.slowWave) * motion.openScaleZWave)),
                MouthState.Down => FaceTransformResult.Visible(
                    motion.downAngleBase + context.primaryWave * motion.downAngleWave,
                    new Vector3(0f, 0f, motion.downOffsetZBase + context.slowWave * motion.downSlowWaveOffsetZ),
                    new Vector3(motion.downScaleX, 1f, motion.downScaleZ)),
                MouthState.Sleep => FaceTransformResult.Visible(
                    0f,
                    new Vector3(0f, 0f, motion.sleepOffsetZ),
                    new Vector3(motion.sleepScaleX, 1f, motion.sleepScaleZ)),
                _ => context.expression switch
                {
                    ExpressionType.Eating => FaceTransformResult.Visible(
                        context.primaryWave * motion.eatingAngleWave,
                        new Vector3(0f, 0f, motion.eatingOffsetZBase + Mathf.Abs(context.primaryWave) * motion.eatingPrimaryWaveOffsetZ),
                        new Vector3(motion.eatingScaleX, 1f, motion.eatingScaleZBase + Mathf.Abs(context.primaryWave) * motion.eatingScaleZWave)),
                    ExpressionType.Shock or ExpressionType.Scared => FaceTransformResult.Visible(
                        context.primaryWave * motion.shockScaredAngleWave,
                        new Vector3(0f, 0f, motion.shockScaredOffsetZBase + Mathf.Abs(context.primaryWave) * motion.shockScaredPrimaryWaveOffsetZ),
                        new Vector3(motion.shockScaredScaleX, 1f, motion.shockScaredScaleZBase + Mathf.Abs(context.slowWave) * motion.shockScaredScaleZWave)),
                    _ => FaceTransformResult.Visible(0f, new Vector3(0f, 0f, context.slowWave * motion.defaultSlowWaveOffsetZ), Vector3.one)
                }
            };
        }

        private static FaceTransformResult EvaluateHair()
            => FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one);

        private static FaceTransformResult EvaluateBlush(FaceTransformContext context, PawnFaceConfig.EmotionOverlayMotionConfig motion)
        {
            bool active = context.emotionState == EmotionOverlayState.Lovin || context.emotionState == EmotionOverlayState.Blush;
            if (!active)
                return FaceTransformResult.Hidden();

            float pulse = motion.blushPulseBase + Mathf.Abs(context.primaryWave) * motion.blushPulseWave;
            return FaceTransformResult.Visible(
                0f,
                new Vector3(0f, 0f, motion.blushOffsetZBase + context.slowWave * motion.blushSlowWaveOffsetZ),
                new Vector3(pulse, 1f, motion.blushScaleZBase + Mathf.Abs(context.slowWave) * motion.blushScaleZWave));
        }

        private static FaceTransformResult EvaluateTear(FaceTransformContext context, PawnFaceConfig.EmotionOverlayMotionConfig motion)
        {
            bool active = context.emotionState == EmotionOverlayState.Tear || context.emotionState == EmotionOverlayState.Gloomy;
            if (!active)
                return FaceTransformResult.Hidden();

            float pulse = motion.tearPulseBase + Mathf.Abs(context.slowWave) * motion.tearPulseWave;
            return FaceTransformResult.Visible(
                context.primaryWave * motion.tearAngleWave,
                new Vector3(0f, 0f, motion.tearOffsetZBase + Mathf.Abs(context.primaryWave) * motion.tearPrimaryWaveOffsetZ),
                new Vector3(pulse, 1f, pulse));
        }

        private static FaceTransformResult EvaluateSweat(FaceTransformContext context, PawnFaceConfig.EmotionOverlayMotionConfig motion)
        {
            if (context.emotionState != EmotionOverlayState.Sweat)
                return FaceTransformResult.Hidden();

            float pulse = motion.sweatPulseBase + Mathf.Abs(context.primaryWave) * motion.sweatPulseWave;
            return FaceTransformResult.Visible(
                context.primaryWave * motion.sweatAngleWave,
                new Vector3(context.primaryWave * motion.sweatOffsetXWave, 0f, motion.sweatOffsetZBase + Mathf.Abs(context.slowWave) * motion.sweatSlowWaveOffsetZ),
                new Vector3(pulse, 1f, pulse));
        }

        private static FaceTransformResult EvaluateOverlay(FaceTransformContext context)
        {
            return FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one);
        }

        private static FaceTransformResult EvaluateReplacementEye(FaceTransformContext context)
        {
            if (!context.hasReplacementEyeOverlay)
                return FaceTransformResult.Hidden();

            if (context.isBlinkActive)
            {
                return context.blinkPhase == BlinkPhase.ShowReplacementEye
                    ? FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one)
                    : FaceTransformResult.Hidden();
            }

            return context.expression switch
            {
                ExpressionType.Dead => FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one),
                ExpressionType.Sleeping => FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one),
                ExpressionType.Happy or ExpressionType.Cheerful or ExpressionType.Lovin or ExpressionType.SocialRelax => FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one),
                _ => FaceTransformResult.Hidden()
            };
        }
    }
}
