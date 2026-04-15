using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 从旧 MotionConfig 字段构建 FaceChannelProfileSet 的工具类。
    /// 
    /// 用途：
    ///   1. 旧皮肤没有 eyeProfiles 等字段时，从已有的 eyeMotion/lidMotion/pupilMotion 自动构建
    ///   2. 编辑器中用户未切换到 profile 编辑模式时，保持从旧字段读取
    /// 
    /// 不持有任何全局状态。每次调用都是纯函数。
    /// </summary>
    public static class FaceProfileBuilder
    {
        // ── Eye ──
        // 来源: FaceTransformEvaluator.EvaluateEye + PawnEyeDirectionConfig.EyeMotionConfig
        // Eye 通道按 EyeAnimationVariant 索引
        public static FaceChannelProfileSet BuildEyeDefaults(PawnEyeDirectionConfig.EyeMotionConfig m)
        {
            var set = new FaceChannelProfileSet { channel = "eye" };
            set.profiles = new[]
            {
                Profile("NeutralOpen", 4,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZWave: m.primaryWaveOffsetZ,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude),
                Profile("NeutralSoft", 6,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZBase: m.neutralSoftOffsetZ,
                    offsetZWave: m.primaryWaveOffsetZ,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude,
                    scaleZBase: 0.95f),
                Profile("NeutralLookDown", 6,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZBase: m.neutralLookDownOffsetZ,
                    offsetZWave: m.primaryWaveOffsetZ,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude,
                    scaleZBase: 0.93f),
                Profile("NeutralGlance", 4,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX + m.neutralGlanceSideOffsetX,
                    offsetXWave: m.neutralGlanceWaveOffsetX,
                    offsetZWave: m.primaryWaveOffsetZ,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude),
                Profile("WorkFocusDown", 6,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZBase: m.workFocusDownOffsetZ,
                    offsetZWave: m.primaryWaveOffsetZ,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude),
                Profile("WorkFocusUp", 6,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZBase: m.workFocusUpOffsetZ,
                    offsetZWave: m.primaryWaveOffsetZ,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude),
                Profile("HappySoft", 6,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZBase: m.happySoftOffsetZ,
                    offsetZWave: m.primaryWaveOffsetZ,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude,
                    scaleZBase: 0.90f),
                Profile("ShockWide", 3,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZBase: m.shockWideOffsetZ,
                    offsetZWave: m.primaryWaveOffsetZ,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude,
                    scaleZBase: 1.12f,
                    scaleZWave: 0.03f),
                Profile("ScaredWide", 3,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX + m.scaredWideSideOffsetX,
                    offsetZBase: m.scaredWideOffsetZ,
                    offsetXWave: m.scaredWideWaveOffsetX,
                    offsetZWave: m.primaryWaveOffsetZ,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude,
                    scaleZBase: 1.08f,
                    scaleZWave: 0.02f),
                Profile("ScaredFlinch", 3,
                    angleWave: m.baseAngleWave,
                    sideBiasX: m.sideBiasX + m.scaredFlinchSideOffsetX,
                    offsetZBase: m.scaredFlinchOffsetZ,
                    offsetXWave: m.scaredFlinchWaveOffsetX,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    scaleXBase: m.scaleXBase,
                    scaleXWave: m.scaleXWaveAmplitude,
                    scaleZBase: 0.94f),
                Profile("Hidden", 0),
            };
            return set;
        }

        // ── Pupil ──
        // 来源: FaceTransformEvaluator.EvaluatePupil + PawnEyeDirectionConfig.PupilMotionConfig
        // Pupil 通道按 PupilScaleVariant 索引
        public static FaceChannelProfileSet BuildPupilDefaults(PawnEyeDirectionConfig.PupilMotionConfig m)
        {
            var set = new FaceChannelProfileSet { channel = "pupil" };
            set.profiles = new[]
            {
                Profile("Neutral", 4,
                    angleWave: m.transformAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    offsetXWave: m.finalWaveOffsetX),
                Profile("Focus", 6,
                    angleWave: m.transformAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    offsetXWave: m.finalWaveOffsetX,
                    scaleXBase: m.focusScaleBase,
                    scaleXWave: m.focusScaleWave,
                    scaleZBase: m.focusScaleBase,
                    scaleZWave: m.focusScaleWave),
                Profile("SlightlyContracted", 6,
                    angleWave: m.transformAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    offsetXWave: m.finalWaveOffsetX,
                    scaleXBase: m.slightlyContractedScaleBase,
                    scaleXWave: m.slightlyContractedScaleWave,
                    scaleZBase: m.slightlyContractedScaleBase,
                    scaleZWave: m.slightlyContractedScaleWave),
                Profile("Contracted", 6,
                    angleWave: m.transformAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    offsetXWave: m.finalWaveOffsetX,
                    scaleXBase: m.contractedScaleBase,
                    scaleXWave: m.contractedScaleWave,
                    scaleZBase: m.contractedScaleBase,
                    scaleZWave: m.contractedScaleWave),
                Profile("Dilated", 6,
                    angleWave: m.transformAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    offsetXWave: m.finalWaveOffsetX,
                    scaleXBase: m.dilatedScaleBase,
                    scaleXWave: m.dilatedScaleWave,
                    scaleZBase: m.dilatedScaleBase,
                    scaleZWave: m.dilatedScaleWave),
                Profile("DilatedMax", 6,
                    angleWave: m.transformAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    offsetXWave: m.finalWaveOffsetX,
                    scaleXBase: m.dilatedMaxScaleBase,
                    scaleXWave: m.dilatedMaxScaleWave,
                    scaleZBase: m.dilatedMaxScaleBase,
                    scaleZWave: m.dilatedMaxScaleWave),
                Profile("ScaredPulse", 4,
                    angleWave: m.transformAngleWave,
                    sideBiasX: m.sideBiasX,
                    offsetZSlowWave: m.slowWaveOffsetZ,
                    offsetXWave: m.finalWaveOffsetX,
                    scaleXBase: m.scaredPulseScaleBase,
                    scaleXWave: m.scaredPulseScaleWave,
                    scaleZBase: m.scaredPulseScaleBase,
                    scaleZWave: m.scaredPulseScaleWave),
                Profile("BlinkHidden", 0, scaleXBase: 0f, scaleZBase: 0f),
            };
            return set;
        }

        // ── Upper Lid ──
        // 来源: FaceTransformEvaluator.EvaluateUpperLid + PawnEyeDirectionConfig.LidMotionConfig
        // 状态键使用复合格式: "LidState" 或 "LidState_Variant"
        public static FaceChannelProfileSet BuildUpperLidDefaults(PawnEyeDirectionConfig.LidMotionConfig m, float moveDown)
        {
            var set = new FaceChannelProfileSet { channel = "upperLid" };
            float halfBase = Mathf.Max(0f, moveDown - m.upperHalfBaseOffsetSubtract);
            set.profiles = new[]
            {
                Profile("Blink", 0,
                    sideBiasX: m.upperSideBiasX,
                    moveDown: moveDown,
                    scaleXBase: m.upperBlinkScaleX,
                    scaleZBase: m.upperBlinkScaleZ),
                Profile("Close", 0,
                    sideBiasX: m.upperSideBiasX,
                    moveDown: moveDown,
                    scaleXBase: m.upperCloseScaleX,
                    scaleZBase: m.upperCloseScaleZ),
                Profile("Half", 8,
                    sideBiasX: m.upperSideBiasX,
                    moveDown: halfBase,
                    offsetZSlowWave: m.upperHalfSlowWaveOffset,
                    scaleXBase: m.upperCloseScaleX,
                    scaleZBase: m.upperHalfScaleDefault),
                Profile("Half_NeutralSoft", 8,
                    sideBiasX: m.upperSideBiasX,
                    moveDown: halfBase + m.upperHalfNeutralSoftExtraOffset,
                    offsetZSlowWave: m.upperHalfSlowWaveOffset,
                    scaleXBase: m.upperCloseScaleX,
                    scaleZBase: m.upperHalfScaleNeutralSoft),
                Profile("Half_NeutralLookDown", 8,
                    sideBiasX: m.upperSideBiasX,
                    moveDown: halfBase + m.upperHalfLookDownExtraOffset,
                    offsetZSlowWave: m.upperHalfSlowWaveOffset,
                    scaleXBase: m.upperCloseScaleX,
                    scaleZBase: m.upperHalfScaleLookDown),
                Profile("Half_ScaredFlinch", 8,
                    sideBiasX: m.upperSideBiasX,
                    moveDown: halfBase + m.upperHalfScaredExtraOffset,
                    offsetZSlowWave: m.upperHalfSlowWaveOffset,
                    scaleXBase: m.upperCloseScaleX,
                    scaleZBase: m.upperHalfScaleScared),
                Profile("Happy_Open", 8,
                    sideBiasX: m.upperSideBiasX,
                    angleBase: m.upperHappyAngleBase,
                    angleWave: m.upperHappyAngleWave,
                    moveDown: m.upperHappyOpenOffset,
                    offsetZSlowWave: m.upperHappySlowWaveOffset,
                    scaleXBase: m.upperHappyScaleX,
                    scaleZBase: m.upperHappyOpenScale),
                Profile("Happy_Soft", 8,
                    sideBiasX: m.upperSideBiasX,
                    angleBase: m.upperHappyAngleBase,
                    angleWave: m.upperHappyAngleWave,
                    moveDown: m.upperHappySoftOffset,
                    offsetZSlowWave: m.upperHappySlowWaveOffset,
                    scaleXBase: m.upperHappyScaleX,
                    scaleZBase: m.upperHappySoftScale),
                Profile("Hidden", 0),
                Profile("Default", 6,
                    sideBiasX: m.upperSideBiasX,
                    offsetZSlowWave: m.upperDefaultSlowWaveOffset),
            };
            return set;
        }

        // ── Lower Lid ──
        public static FaceChannelProfileSet BuildLowerLidDefaults(PawnEyeDirectionConfig.LidMotionConfig m)
        {
            var set = new FaceChannelProfileSet { channel = "lowerLid" };
            set.profiles = new[]
            {
                Profile("Blink", 0,
                    sideBiasX: m.lowerSideBiasX,
                    moveDown: m.lowerBlinkOffset,
                    scaleXBase: m.lowerBlinkScaleX,
                    scaleZBase: m.lowerBlinkScaleZ),
                Profile("Close", 0,
                    sideBiasX: m.lowerSideBiasX,
                    moveDown: m.lowerCloseOffset,
                    scaleXBase: m.lowerCloseScaleX,
                    scaleZBase: m.lowerCloseScaleZ),
                Profile("Half", 8,
                    sideBiasX: m.lowerSideBiasX,
                    moveDown: m.lowerHalfOffset,
                    offsetZSlowWave: m.lowerHalfSlowWaveOffset,
                    scaleXBase: m.lowerHalfScaleX,
                    scaleZBase: m.lowerHalfScaleZ),
                Profile("Happy", 8,
                    sideBiasX: m.lowerSideBiasX,
                    angleBase: m.lowerHappyAngleBase,
                    angleWave: m.lowerHappyAngleWave,
                    moveDown: m.lowerHappyOffset,
                    offsetZSlowWave: m.lowerHappySlowWaveOffset,
                    scaleXBase: m.lowerHappyScaleX,
                    scaleZBase: m.lowerHappyScaleZ),
                Profile("Default", 6,
                    sideBiasX: m.lowerSideBiasX,
                    offsetZSlowWave: m.lowerDefaultSlowWaveOffset),
            };
            return set;
        }

        // ── Brow ──
        // 来源: FaceTransformEvaluator.EvaluateBrow + PawnFaceConfig.BrowMotionConfig
        public static FaceChannelProfileSet BuildBrowDefaults(PawnFaceConfig.BrowMotionConfig m)
        {
            var set = new FaceChannelProfileSet { channel = "brow" };
            set.profiles = new[]
            {
                Profile("Angry", 8,
                    angleBase: m.angryAngleBase,
                    angleWave: m.angryAngleWave,
                    offsetZBase: m.angryOffsetZBase,
                    offsetZSlowWave: m.angrySlowWaveOffsetZ,
                    scaleXBase: m.angryScaleX,
                    scaleZBase: m.angryScaleZ),
                Profile("Sad", 8,
                    angleBase: m.sadAngleBase,
                    angleWave: m.sadAngleWave,
                    offsetZBase: m.sadOffsetZBase,
                    offsetZSlowWave: m.sadSlowWaveOffsetZ,
                    scaleXBase: m.sadScaleX,
                    scaleZBase: m.sadScaleZ),
                Profile("Happy", 8,
                    angleBase: m.happyAngleBase,
                    angleWave: m.happyAngleWave,
                    offsetZBase: m.happyOffsetZBase,
                    offsetZSlowWave: m.happySlowWaveOffsetZ,
                    scaleXBase: m.happyScaleX,
                    scaleZBase: m.happyScaleZ),
                Profile("Default", 6,
                    offsetZSlowWave: m.defaultSlowWaveOffsetZ),
            };
            return set;
        }

        // ── Mouth ──
        // 来源: FaceTransformEvaluator.EvaluateMouth + PawnFaceConfig.MouthMotionConfig
        public static FaceChannelProfileSet BuildMouthDefaults(PawnFaceConfig.MouthMotionConfig m)
        {
            var set = new FaceChannelProfileSet { channel = "mouth" };
            set.profiles = new[]
            {
                Profile("Smile", 8,
                    angleWave: m.smileAngleWave,
                    offsetZBase: m.smileOffsetZBase,
                    offsetZWave: m.smilePrimaryWaveOffsetZ,
                    scaleXBase: m.smileScaleXBase,
                    scaleXWave: m.smileScaleXWave,
                    scaleZBase: m.smileScaleZ),
                Profile("Open", 6,
                    angleWave: m.openAngleWave,
                    offsetZBase: m.openOffsetZBase,
                    offsetZWave: m.openPrimaryWaveOffsetZ,
                    scaleXBase: m.openScaleX,
                    scaleZBase: m.openScaleZBase,
                    scaleZWave: m.openScaleZWave),
                Profile("Down", 8,
                    angleBase: m.downAngleBase,
                    angleWave: m.downAngleWave,
                    offsetZBase: m.downOffsetZBase,
                    offsetZSlowWave: m.downSlowWaveOffsetZ,
                    scaleXBase: m.downScaleX,
                    scaleZBase: m.downScaleZ),
                Profile("Sleep", 10,
                    offsetZBase: m.sleepOffsetZ,
                    scaleXBase: m.sleepScaleX,
                    scaleZBase: m.sleepScaleZ),
                Profile("Eating", 4,
                    angleWave: m.eatingAngleWave,
                    offsetZBase: m.eatingOffsetZBase,
                    offsetZWave: m.eatingPrimaryWaveOffsetZ,
                    scaleXBase: m.eatingScaleX,
                    scaleZBase: m.eatingScaleZBase,
                    scaleZWave: m.eatingScaleZWave),
                Profile("ShockScared", 4,
                    angleWave: m.shockScaredAngleWave,
                    offsetZBase: m.shockScaredOffsetZBase,
                    offsetZWave: m.shockScaredPrimaryWaveOffsetZ,
                    scaleXBase: m.shockScaredScaleX,
                    scaleZBase: m.shockScaredScaleZBase,
                    scaleZWave: m.shockScaredScaleZWave),
                Profile("Default", 6,
                    offsetZSlowWave: m.defaultSlowWaveOffsetZ),
            };
            return set;
        }

        // ── 构建辅助 ──

        private static FaceStateProfile Profile(
            string stateName,
            int blendIn,
            float angleBase = 0f,
            float angleWave = 0f,
            float offsetZBase = 0f,
            float offsetZWave = 0f,
            float offsetZSlowWave = 0f,
            float offsetXWave = 0f,
            float scaleXBase = 1f,
            float scaleXWave = 0f,
            float scaleZBase = 1f,
            float scaleZWave = 0f,
            float sideBiasX = 0f,
            float moveDown = 0f)
        {
            return new FaceStateProfile
            {
                stateName = stateName,
                blendIn = blendIn,
                angleBase = angleBase,
                angleWave = angleWave,
                offsetZBase = offsetZBase,
                offsetZWave = offsetZWave,
                offsetZSlowWave = offsetZSlowWave,
                offsetXWave = offsetXWave,
                scaleXBase = scaleXBase,
                scaleXWave = scaleXWave,
                scaleZBase = scaleZBase,
                scaleZWave = scaleZWave,
                sideBiasX = sideBiasX,
                moveDown = moveDown,
            };
        }
    }
}
