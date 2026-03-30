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
        public static FaceTransformResult Evaluate(FaceTransformContext context, PawnEyeDirectionConfig.LidMotionConfig lidMotion, float upperLidMoveDown)
        {
            return context.partType switch
            {
                LayeredFacePartType.Brow => EvaluateBrow(context),
                LayeredFacePartType.Eye => EvaluateEye(context),
                LayeredFacePartType.Pupil => EvaluatePupil(context),
                LayeredFacePartType.UpperLid => EvaluateUpperLid(context, lidMotion, upperLidMoveDown),
                LayeredFacePartType.LowerLid => EvaluateLowerLid(context, lidMotion),
                LayeredFacePartType.Mouth => EvaluateMouth(context),
                LayeredFacePartType.Hair => EvaluateHair(),
                LayeredFacePartType.Blush => EvaluateBlush(context),
                LayeredFacePartType.Tear => EvaluateTear(context),
                LayeredFacePartType.Sweat => EvaluateSweat(context),
                LayeredFacePartType.Overlay => EvaluateOverlay(context),
                _ => FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one)
            };
        }

        private static FaceTransformResult EvaluateBrow(FaceTransformContext context)
        {
            return context.browState switch
            {
                BrowState.Angry => FaceTransformResult.Visible(
                    -4.5f + context.primaryWave * 0.6f,
                    new Vector3(0f, 0f, -0.004f + context.slowWave * 0.0008f),
                    new Vector3(1.04f, 1f, 0.97f)),
                BrowState.Sad => FaceTransformResult.Visible(
                    3.25f + context.primaryWave * 0.45f,
                    new Vector3(0f, 0f, 0.0045f + context.slowWave * 0.0008f),
                    new Vector3(1.02f, 1f, 0.98f)),
                BrowState.Happy => FaceTransformResult.Visible(
                    -1.5f + context.primaryWave * 0.25f,
                    new Vector3(0f, 0f, -0.0015f + context.slowWave * 0.0004f),
                    new Vector3(1.03f, 1f, 0.97f)),
                _ => FaceTransformResult.Visible(0f, new Vector3(0f, 0f, context.slowWave * 0.0006f), Vector3.one)
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

        private static FaceTransformResult EvaluateEye(FaceTransformContext context)
        {
            if (context.lidState == LidState.Blink || context.lidState == LidState.Close)
                return FaceTransformResult.Hidden();

            float sideSign = SideSign(context.side);
            float offsetX = SideBias(context.side, 0.00002f);
            float offsetZ = 0.00004f * context.primaryWave;

            switch (context.eyeDirection)
            {
                case EyeDirection.Left: offsetX = -0.00012f; break;
                case EyeDirection.Right: offsetX = 0.00012f; break;
                case EyeDirection.Up: offsetZ -= 0.00010f; break;
                case EyeDirection.Down: offsetZ += 0.000012f; break;
            }

            switch (context.eyeVariant)
            {
                case EyeAnimationVariant.NeutralSoft:
                    offsetZ += 0.00005f;
                    break;
                case EyeAnimationVariant.NeutralLookDown:
                    offsetZ += 0.000010f;
                    break;
                case EyeAnimationVariant.NeutralGlance:
                    offsetX += (context.primaryWave > 0f ? 0.00008f : -0.00008f) + sideSign * 0.000035f;
                    break;
                case EyeAnimationVariant.WorkFocusDown:
                    offsetZ += 0.000016f;
                    break;
                case EyeAnimationVariant.WorkFocusUp:
                    offsetZ -= 0.00012f;
                    break;
                case EyeAnimationVariant.HappySoft:
                    offsetZ -= 0.00006f;
                    break;
                case EyeAnimationVariant.ShockWide:
                    offsetZ -= 0.00018f;
                    break;
                case EyeAnimationVariant.ScaredWide:
                    offsetZ -= 0.00012f;
                    offsetX += context.primaryWave * 0.00006f + sideSign * 0.00003f;
                    break;
                case EyeAnimationVariant.ScaredFlinch:
                    offsetZ += 0.00008f;
                    offsetX += context.slowWave * 0.00007f + sideSign * 0.000045f;
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
                context.primaryWave * 0.15f,
                new Vector3(offsetX, 0f, offsetZ + context.slowWave * 0.00004f),
                new Vector3(1.01f + Mathf.Abs(context.slowWave) * 0.01f, 1f, scaleZ));
        }

        private static FaceTransformResult EvaluatePupil(FaceTransformContext context)
        {
            if (context.lidState == LidState.Blink || context.lidState == LidState.Close)
                return FaceTransformResult.Hidden();

            float sideSign = SideSign(context.side);
            float offsetX = SideBias(context.side, 0.000028f);
            float offsetZ = context.slowWave * 0.00005f;

            switch (context.eyeDirection)
            {
                case EyeDirection.Left: offsetX = -0.00018f; break;
                case EyeDirection.Right: offsetX = 0.00018f; break;
                case EyeDirection.Up: offsetZ -= 0.00014f; break;
                case EyeDirection.Down: offsetZ += 0.000016f; break;
            }

            switch (context.eyeVariant)
            {
                case EyeAnimationVariant.NeutralSoft:
                    offsetZ += 0.00004f;
                    break;
                case EyeAnimationVariant.NeutralLookDown:
                    offsetZ += 0.000012f;
                    break;
                case EyeAnimationVariant.NeutralGlance:
                    offsetX += (context.primaryWave > 0f ? 0.00010f : -0.00010f) + sideSign * 0.000045f;
                    break;
                case EyeAnimationVariant.WorkFocusDown:
                    offsetZ += 0.000020f;
                    break;
                case EyeAnimationVariant.WorkFocusUp:
                    offsetZ -= 0.00015f;
                    break;
                case EyeAnimationVariant.HappyOpen:
                    offsetZ -= 0.00003f;
                    break;
                case EyeAnimationVariant.ShockWide:
                    offsetZ -= 0.00012f;
                    break;
                case EyeAnimationVariant.ScaredWide:
                    offsetZ -= 0.00008f;
                    offsetX += context.primaryWave * 0.00008f + sideSign * 0.00004f;
                    break;
                case EyeAnimationVariant.ScaredFlinch:
                    offsetZ += 0.00008f;
                    offsetX += context.slowWave * 0.00009f + sideSign * 0.000055f;
                    break;
                case EyeAnimationVariant.HappyClosedPeak:
                case EyeAnimationVariant.BlinkClosed:
                    return FaceTransformResult.Hidden();
            }

            float scale = context.pupilVariant switch
            {
                PupilScaleVariant.Focus => 0.94f + Mathf.Abs(context.slowWave) * 0.01f,
                PupilScaleVariant.SlightlyContracted => 0.88f + Mathf.Abs(context.slowWave) * 0.01f,
                PupilScaleVariant.Contracted => 0.78f + Mathf.Abs(context.slowWave) * 0.015f,
                PupilScaleVariant.Dilated => 1.12f + Mathf.Abs(context.primaryWave) * 0.02f,
                PupilScaleVariant.DilatedMax => 1.22f + Mathf.Abs(context.primaryWave) * 0.03f,
                PupilScaleVariant.ScaredPulse => 1.16f + Mathf.Abs(context.primaryWave) * 0.05f,
                PupilScaleVariant.BlinkHidden => 0f,
                _ => 1f,
            };

            if (context.expression == ExpressionType.Shock || context.expression == ExpressionType.Scared)
                scale = Mathf.Max(scale, 1.08f + Mathf.Abs(context.primaryWave) * 0.03f);
            else if (context.expression == ExpressionType.Happy || context.expression == ExpressionType.Cheerful)
                scale = Mathf.Min(scale, 0.96f + Mathf.Abs(context.slowWave) * 0.01f);
            else if (context.expression == ExpressionType.Sleeping)
                scale = 0.9f;
            else if (context.eyeVariant == EyeAnimationVariant.WorkFocusDown)
                scale = Mathf.Min(scale, 0.98f);

            if (context.eyeVariant == EyeAnimationVariant.NeutralSoft)
                scale = Mathf.Min(scale, 0.96f);
            else if (context.eyeVariant == EyeAnimationVariant.NeutralLookDown)
                scale = Mathf.Min(scale, 0.94f);
            else if (context.eyeVariant == EyeAnimationVariant.ShockWide)
                scale = Mathf.Max(scale, 1.18f + Mathf.Abs(context.primaryWave) * 0.02f);
            else if (context.eyeVariant == EyeAnimationVariant.ScaredWide)
                scale = Mathf.Max(scale, 1.14f + Mathf.Abs(context.slowWave) * 0.03f);
            else if (context.eyeVariant == EyeAnimationVariant.ScaredFlinch)
                scale = Mathf.Max(scale, 1.04f + Mathf.Abs(context.primaryWave) * 0.01f);

            if (context.pupilVariant == PupilScaleVariant.BlinkHidden)
                return FaceTransformResult.Hidden();

            if (context.eyeRenderMode == EyeRenderMode.UvOffset)
                return FaceTransformResult.Visible(0f, Vector3.zero, new Vector3(scale, 1f, scale));

            return FaceTransformResult.Visible(
                context.primaryWave * 0.35f,
                new Vector3(offsetX + context.primaryWave * 0.00004f, 0f, offsetZ),
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

        private static FaceTransformResult EvaluateMouth(FaceTransformContext context)
        {
            return context.mouthState switch
            {
                MouthState.Smile => FaceTransformResult.Visible(
                    context.slowWave * 0.6f,
                    new Vector3(0f, 0f, -0.001f + context.primaryWave * 0.0006f),
                    new Vector3(1.06f + Mathf.Abs(context.primaryWave) * 0.02f, 1f, 0.94f)),
                MouthState.Open => FaceTransformResult.Visible(
                    context.primaryWave * 0.8f,
                    new Vector3(0f, 0f, 0.004f + Mathf.Abs(context.primaryWave) * 0.0015f),
                    new Vector3(1.03f, 1f, 1.14f + Mathf.Abs(context.slowWave) * 0.04f)),
                MouthState.Down => FaceTransformResult.Visible(
                    -0.75f + context.primaryWave * 0.3f,
                    new Vector3(0f, 0f, 0.0025f + context.slowWave * 0.0006f),
                    new Vector3(0.99f, 1f, 0.90f)),
                MouthState.Sleep => FaceTransformResult.Visible(
                    0f,
                    new Vector3(0f, 0f, 0.002f),
                    new Vector3(0.97f, 1f, 0.84f)),
                _ => context.expression switch
                {
                    ExpressionType.Eating => FaceTransformResult.Visible(
                        context.primaryWave * 1.25f,
                        new Vector3(0f, 0f, 0.002f + Mathf.Abs(context.primaryWave) * 0.001f),
                        new Vector3(1.01f, 1f, 1.05f + Mathf.Abs(context.primaryWave) * 0.04f)),
                    ExpressionType.Shock or ExpressionType.Scared => FaceTransformResult.Visible(
                        context.primaryWave * 0.75f,
                        new Vector3(0f, 0f, 0.0032f + Mathf.Abs(context.primaryWave) * 0.001f),
                        new Vector3(1.02f, 1f, 1.10f + Mathf.Abs(context.slowWave) * 0.03f)),
                    _ => FaceTransformResult.Visible(0f, new Vector3(0f, 0f, context.slowWave * 0.0005f), Vector3.one)
                }
            };
        }

        private static FaceTransformResult EvaluateHair()
            => FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one);

        private static FaceTransformResult EvaluateBlush(FaceTransformContext context)
        {
            bool active = context.emotionState == EmotionOverlayState.Lovin || context.emotionState == EmotionOverlayState.Blush;
            if (!active)
                return FaceTransformResult.Hidden();

            float pulse = 1.04f + Mathf.Abs(context.primaryWave) * 0.05f;
            return FaceTransformResult.Visible(
                0f,
                new Vector3(0f, 0f, -0.001f + context.slowWave * 0.001f),
                new Vector3(pulse, 1f, 1.02f + Mathf.Abs(context.slowWave) * 0.02f));
        }

        private static FaceTransformResult EvaluateTear(FaceTransformContext context)
        {
            bool active = context.emotionState == EmotionOverlayState.Tear || context.emotionState == EmotionOverlayState.Gloomy;
            if (!active)
                return FaceTransformResult.Hidden();

            float pulse = 1.01f + Mathf.Abs(context.slowWave) * 0.02f;
            return FaceTransformResult.Visible(
                context.primaryWave * 0.5f,
                new Vector3(0f, 0f, 0.002f + Mathf.Abs(context.primaryWave) * 0.0015f),
                new Vector3(pulse, 1f, pulse));
        }

        private static FaceTransformResult EvaluateSweat(FaceTransformContext context)
        {
            if (context.emotionState != EmotionOverlayState.Sweat)
                return FaceTransformResult.Hidden();

            float pulse = 1f + Mathf.Abs(context.primaryWave) * 0.03f;
            return FaceTransformResult.Visible(
                context.primaryWave * 2.5f,
                new Vector3(context.primaryWave * 0.0025f, 0f, 0.0015f + Mathf.Abs(context.slowWave) * 0.001f),
                new Vector3(pulse, 1f, pulse));
        }

        private static FaceTransformResult EvaluateOverlay(FaceTransformContext context)
        {
            if (PawnFaceConfig.GetOverlayDisplayPartType(context.overlayId) == LayeredFacePartType.Eye)
            {
                if (!context.hasReplacementEyeOverlay)
                    return FaceTransformResult.Hidden();

                if (context.isBlinkActive && context.blinkPhase != BlinkPhase.ShowReplacementEye)
                    return FaceTransformResult.Hidden();

                if (!context.isBlinkActive && context.expression == ExpressionType.Neutral)
                    return FaceTransformResult.Hidden();
            }

            return FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one);
        }
    }
}
