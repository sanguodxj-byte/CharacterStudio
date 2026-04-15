using UnityEngine;
using CharacterStudio.Rendering;
using Verse;

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
        public readonly float blinkPhaseProgress;
        public readonly bool hasReplacementEyeOverlay;
        public readonly EyeAnimationVariant eyeVariant;
        public readonly PupilScaleVariant pupilVariant;
        public readonly LayeredFacePartSide winkSide;
        public readonly ExpressionType expression;
        public readonly Rot4 facing;
        public readonly Vector2 gazeOffset;
        public readonly float primaryWave;
        public readonly float slowWave;

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
            float blinkPhaseProgress,
            bool hasReplacementEyeOverlay,
            EyeAnimationVariant eyeVariant,
            PupilScaleVariant pupilVariant,
            LayeredFacePartSide winkSide,
            ExpressionType expression,
            Rot4 facing,
            Vector2 gazeOffset,
            float primaryWave,
            float slowWave)
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
            this.blinkPhaseProgress = blinkPhaseProgress;
            this.hasReplacementEyeOverlay = hasReplacementEyeOverlay;
            this.eyeVariant = eyeVariant;
            this.pupilVariant = pupilVariant;
            this.winkSide = winkSide;
            this.expression = expression;
            this.facing = facing;
            this.gazeOffset = gazeOffset;
            this.primaryWave = primaryWave;
            this.slowWave = slowWave;
        }
    }

    internal static class FaceTransformEvaluator
    {
        // ─────────────────────────────────────────────────────────────
        // 旧接口：保持向后兼容，供未迁移的调用方使用
        // ─────────────────────────────────────────────────────────────

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
                LayeredFacePartType.Hair => EvaluateHair(context),
                LayeredFacePartType.Blush => EvaluateBlush(context, emotionOverlayMotion),
                LayeredFacePartType.Tear => EvaluateTear(context, emotionOverlayMotion),
                LayeredFacePartType.Sweat => EvaluateSweat(context, emotionOverlayMotion),
                LayeredFacePartType.Overlay => EvaluateOverlay(context),
                _ => FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one)
            };
        }

        // ─────────────────────────────────────────────────────────────
        // 新接口：基于 FaceBlendState 的 profile 驱动求值
        //
        // 调用方需要为每个通道维护一个 FaceBlendState，
        // 在通道状态变化时调用 BeginTransition，每帧调用此方法求值。
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 基于 FaceBlendState 求值 Eye 通道。
        /// Eye 通道的核心公式：
        ///   angle = base + primaryWave * wave
        ///   offset.x = base + sideBias * sideSign + variantAdjustments + primaryWave * wave
        ///   offset.z = base + variantAdjustments + primaryWave * wave + slowWave * slowWave
        ///   scale = (scaleXBase + |slowWave| * scaleXWave, 1, expressionScaleZ)
        /// </summary>
        public static FaceTransformResult EvaluateEyeFromProfile(
            FaceBlendState blendState,
            FaceTransformContext context,
            int currentTick)
        {
            float sideSign = SideSign(context.side);

            // 从 blendState 获取基础参数（含过渡插值）
            float baseAngle = blendState.EvaluateAngle(currentTick, context.primaryWave);
            Vector3 baseOffset = blendState.EvaluateOffset(currentTick, context.primaryWave, context.slowWave);
            Vector3 baseScale = blendState.EvaluateScale(currentTick, context.primaryWave, context.slowWave);

            // 叠加侧偏（从 profile 的 sideBiasX 字段）
            float sideBias = blendState.GetSideBiasX();
            baseOffset.x += sideSign * sideBias;

            return FaceTransformResult.Visible(baseAngle, baseOffset, baseScale);
        }

        /// <summary>
        /// 基于 FaceBlendState 求值 Pupil 通道。
        /// Pupil 的方向偏移和 gazeOffset 由代码逻辑叠加，profile 只管基础参数。
        /// </summary>
        public static FaceTransformResult EvaluatePupilFromProfile(
            FaceBlendState blendState,
            FaceTransformContext context,
            int currentTick)
        {
            float sideSign = SideSign(context.side);

            // 从 profile 获取基础参数
            float angle = blendState.EvaluateAngle(currentTick, context.primaryWave);
            Vector3 offset = blendState.EvaluateOffset(currentTick, context.primaryWave, context.slowWave);
            Vector3 scale = blendState.EvaluateScale(currentTick, context.primaryWave, context.slowWave);

            // 叠加侧偏
            float sideBias = blendState.GetSideBiasX();
            offset.x += sideSign * sideBias;

            return FaceTransformResult.Visible(angle, offset, scale);
        }

        /// <summary>
        /// 基于 FaceBlendState 求值 UpperLid 通道。
        /// upperLidMoveDown 由外部传入（来自 EyeDirectionConfig）。
        /// </summary>
        public static FaceTransformResult EvaluateUpperLidFromProfile(
            FaceBlendState blendState,
            FaceTransformContext context,
            int currentTick,
            float upperLidMoveDown)
        {
            float sideSign = SideSign(context.side);

            float angle = blendState.EvaluateAngle(currentTick, context.primaryWave);
            Vector3 offset = blendState.EvaluateOffset(currentTick, context.primaryWave, context.slowWave);
            Vector3 scale = blendState.EvaluateScale(currentTick, context.primaryWave, context.slowWave);

            // 叠加侧偏
            float sideBias = blendState.GetSideBiasX();
            offset.x += sideSign * sideBias;

            // 叠加 moveDown（用于 Blink/Close 闭合位移）
            float moveDown = blendState.GetMoveDown();
            offset.z += moveDown;

            return FaceTransformResult.Visible(angle, offset, scale);
        }

        /// <summary>
        /// 基于 FaceBlendState 求值 LowerLid 通道。
        /// </summary>
        public static FaceTransformResult EvaluateLowerLidFromProfile(
            FaceBlendState blendState,
            FaceTransformContext context,
            int currentTick)
        {
            float sideSign = SideSign(context.side);

            float angle = blendState.EvaluateAngle(currentTick, context.primaryWave);
            Vector3 offset = blendState.EvaluateOffset(currentTick, context.primaryWave, context.slowWave);
            Vector3 scale = blendState.EvaluateScale(currentTick, context.primaryWave, context.slowWave);

            // 叠加侧偏
            float sideBias = blendState.GetSideBiasX();
            offset.x += sideSign * sideBias;

            // 叠加 moveDown（LowerLid 在 Blink/Close 时上移）
            float moveDown = blendState.GetMoveDown();
            offset.z += moveDown;

            return FaceTransformResult.Visible(angle, offset, scale);
        }

        /// <summary>
        /// 基于 FaceBlendState 求值 Brow 通道。
        /// Brow 是最简单的通道：无侧偏、无 moveDown，只有 angle/offsetZ/scale。
        /// </summary>
        public static FaceTransformResult EvaluateBrowFromProfile(
            FaceBlendState blendState,
            FaceTransformContext context,
            int currentTick)
        {
            float angle = blendState.EvaluateAngle(currentTick, context.primaryWave);
            Vector3 offset = blendState.EvaluateOffset(currentTick, context.primaryWave, context.slowWave);
            Vector3 scale = blendState.EvaluateScale(currentTick, context.primaryWave, context.slowWave);
            return FaceTransformResult.Visible(angle, offset, scale);
        }

        /// <summary>
        /// 基于 FaceBlendState 求值 Mouth 通道。
        /// Mouth 与 Brow 类似，无侧偏。
        /// </summary>
        public static FaceTransformResult EvaluateMouthFromProfile(
            FaceBlendState blendState,
            FaceTransformContext context,
            int currentTick)
        {
            float angle = blendState.EvaluateAngle(currentTick, context.primaryWave);
            Vector3 offset = blendState.EvaluateOffset(currentTick, context.primaryWave, context.slowWave);
            Vector3 scale = blendState.EvaluateScale(currentTick, context.primaryWave, context.slowWave);
            return FaceTransformResult.Visible(angle, offset, scale);
        }

        // ─────────────────────────────────────────────────────────────
        // 状态键解析：将运行时枚举映射到 JSON profile 的 stateName
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Eye 通道：按 EyeAnimationVariant + LidState 映射。
        /// Close 隐藏，HappyClosedPeak 隐藏。
        /// </summary>
        public static string ResolveEyeStateKey(EyeAnimationVariant variant, LidState lidState, ExpressionType expression)
        {
            if (lidState == LidState.Close) return "Hidden";
            if (variant == EyeAnimationVariant.HappyClosedPeak) return "Hidden";
            return variant.ToString();
        }

        /// <summary>
        /// Pupil 通道：按 PupilScaleVariant 映射，叠加 expression 约束。
        /// </summary>
        public static string ResolvePupilStateKey(PupilScaleVariant pupilVariant, ExpressionType expression, EyeAnimationVariant eyeVariant)
        {
            return pupilVariant.ToString();
        }

        /// <summary>
        /// UpperLid 通道：按 LidState 映射，Half/Happy 状态还需考虑 EyeAnimationVariant。
        /// 返回格式: "LidState" 或 "LidState_Variant"
        /// </summary>
        public static string ResolveUpperLidStateKey(LidState lidState, EyeAnimationVariant eyeVariant)
        {
            switch (lidState)
            {
                case LidState.Half:
                {
                    // Half 状态根据 variant 选择不同 profile
                    if (eyeVariant == EyeAnimationVariant.NeutralSoft) return "Half_NeutralSoft";
                    if (eyeVariant == EyeAnimationVariant.NeutralLookDown) return "Half_NeutralLookDown";
                    if (eyeVariant == EyeAnimationVariant.ScaredFlinch) return "Half_ScaredFlinch";
                    return "Half";
                }
                case LidState.Happy:
                {
                    if (eyeVariant == EyeAnimationVariant.HappySoft) return "Happy_Soft";
                    if (eyeVariant == EyeAnimationVariant.HappyClosedPeak) return "Hidden";
                    return "Happy_Open";
                }
                default:
                    return lidState.ToString();
            }
        }

        /// <summary>
        /// LowerLid 通道：按 LidState 映射。
        /// </summary>
        public static string ResolveLowerLidStateKey(LidState lidState)
        {
            return lidState.ToString();
        }

        /// <summary>
        /// Brow 通道：按 BrowState 映射。
        /// </summary>
        public static string ResolveBrowStateKey(BrowState browState)
        {
            return browState.ToString();
        }

        /// <summary>
        /// Mouth 通道：按 MouthState + ExpressionType 映射。
        /// Eating 和 Shock/Scared 由 expression 而非 mouthState 驱动。
        /// </summary>
        public static string ResolveMouthStateKey(MouthState mouthState, ExpressionType expression)
        {
            if (mouthState == MouthState.Normal)
            {
                if (expression == ExpressionType.Eating) return "Eating";
                if (expression == ExpressionType.Shock || expression == ExpressionType.Scared) return "ShockScared";
            }
            return mouthState.ToString();
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
            if (context.lidState == LidState.Close)
                return FaceTransformResult.Hidden();

            float sideSign = SideSign(context.side);
            float offsetX = SideBias(context.side, motion.sideBiasX);
            float offsetZ = motion.primaryWaveOffsetZ * context.primaryWave;

            // Eye white (sclera) does NOT shift with gaze direction.
            // Only the Pupil layer tracks eye direction; the sclera stays stationary.

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
            if (context.lidState == LidState.Close)
                return FaceTransformResult.Hidden();

            float sideSign = SideSign(context.side);
            float offsetX = SideBias(context.side, motion.sideBiasX);
            float offsetZ = context.slowWave * motion.slowWaveOffsetZ;
            Vector2 clampedGazeOffset = Vector2.ClampMagnitude(context.gazeOffset, 1f);

            bool isSideFacing = context.facing == Rot4.East || context.facing == Rot4.West;
            bool isLeftEye = context.side == LayeredFacePartSide.Left;

            // 根据朝向和瞳孔侧选择对应的方向偏移
            float dirLeftX, dirRightX, dirUpZ, dirDownZ;
            if (isSideFacing)
            {
                // 侧面朝向：使用侧面偏移配置
                offsetX += SideBias(context.side, motion.side_baseX);
                dirLeftX = motion.side_dirLeftX;
                dirRightX = motion.side_dirRightX;
                dirUpZ = motion.side_dirUpZ;
                dirDownZ = motion.side_dirDownZ;
            }
            else
            {
                // 正面朝向：使用左/右瞳孔独立配置
                if (isLeftEye)
                {
                    offsetX += motion.leftPupil_frontBaseX;
                    dirLeftX = motion.leftPupil_dirLeftX;
                    dirRightX = motion.leftPupil_dirRightX;
                    dirUpZ = motion.leftPupil_dirUpZ;
                    dirDownZ = motion.leftPupil_dirDownZ;
                }
                else
                {
                    offsetX += motion.rightPupil_frontBaseX;
                    dirLeftX = motion.rightPupil_dirLeftX;
                    dirRightX = motion.rightPupil_dirRightX;
                    dirUpZ = motion.rightPupil_dirUpZ;
                    dirDownZ = motion.rightPupil_dirDownZ;
                }
            }

            offsetX += clampedGazeOffset.x * Mathf.Max(Mathf.Abs(dirLeftX), Mathf.Abs(dirRightX));
            offsetZ += clampedGazeOffset.y * Mathf.Max(Mathf.Abs(dirUpZ), Mathf.Abs(dirDownZ));

            switch (context.eyeDirection)
            {
                case EyeDirection.Left: offsetX += dirLeftX; break;
                case EyeDirection.Right: offsetX += dirRightX; break;
                case EyeDirection.Up: offsetZ += dirUpZ; break;
                case EyeDirection.Down: offsetZ += dirDownZ; break;
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
                    float upperBlinkProgress = context.blinkPhase switch
                    {
                        BlinkPhase.ClosingLid => context.blinkPhaseProgress,
                        BlinkPhase.HideBaseEyeParts => 1f,
                        BlinkPhase.ShowReplacementEye => 1f,
                        BlinkPhase.RestoreBaseEyeParts => 1f,
                        BlinkPhase.OpeningLid => 1f - context.blinkPhaseProgress,
                        _ => context.isBlinkActive ? 1f : 0f,
                    };
                    return FaceTransformResult.Visible(
                        0f,
                        new Vector3(sideBiasX, 0f, replacementMoveDown * upperBlinkProgress),
                        new Vector3(
                            Mathf.Lerp(1f, lidMotion.upperBlinkScaleX, upperBlinkProgress),
                            1f,
                            Mathf.Lerp(1f, lidMotion.upperBlinkScaleZ, upperBlinkProgress)));
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
                    float lowerBlinkProgress = context.blinkPhase switch
                    {
                        BlinkPhase.ClosingLid => context.blinkPhaseProgress,
                        BlinkPhase.HideBaseEyeParts => 1f,
                        BlinkPhase.ShowReplacementEye => 1f,
                        BlinkPhase.RestoreBaseEyeParts => 1f,
                        BlinkPhase.OpeningLid => 1f - context.blinkPhaseProgress,
                        _ => context.isBlinkActive ? 1f : 0f,
                    };
                    return FaceTransformResult.Visible(
                        0f,
                        new Vector3(sideBiasX, 0f, lidMotion.lowerBlinkOffset * lowerBlinkProgress),
                        new Vector3(
                            Mathf.Lerp(1f, lidMotion.lowerBlinkScaleX, lowerBlinkProgress),
                            1f,
                            Mathf.Lerp(1f, lidMotion.lowerBlinkScaleZ, lowerBlinkProgress)));
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

        private static FaceTransformResult EvaluateHair(FaceTransformContext context)
        {
            string overlayId = PawnFaceConfig.NormalizeOverlayId(context.overlayId);

            // 后发节点会挂到 Body 父节点，而其它分层面部/头发节点普遍跟随 Head 体系做程序化起伏。
            // 若后发完全不参与这类纵向位移，就会在上下动画时与前发/面部层产生明显撕裂。
            // 这里仅为 back 组补一个轻微、稳定的慢波位移，避免扩大到其它 Hair 组的表现风险。
            if (overlayId.Equals("back", System.StringComparison.OrdinalIgnoreCase))
            {
                return FaceTransformResult.Visible(
                    0f,
                    new Vector3(0f, 0f, context.slowWave * 0.0006f),
                    Vector3.one);
            }

            return FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one);
        }

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

            if (context.expression == ExpressionType.Wink
                && context.side != LayeredFacePartSide.None)
            {
                bool shouldShowThisSide = context.side == context.winkSide;

                if (!shouldShowThisSide)
                    return FaceTransformResult.Hidden();
            }

            if (context.isBlinkActive)
            {
                // Show replacement eye during HideBaseEyeParts, ShowReplacementEye, and
                // RestoreBaseEyeParts to eliminate empty frames where neither base nor
                // replacement eye is visible.
                bool showDuringBlink = context.blinkPhase == BlinkPhase.ShowReplacementEye
                    || context.blinkPhase == BlinkPhase.HideBaseEyeParts
                    || context.blinkPhase == BlinkPhase.RestoreBaseEyeParts;
                return showDuringBlink
                    ? FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one)
                    : FaceTransformResult.Hidden();
            }

            return FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one);
        }
    }
}
