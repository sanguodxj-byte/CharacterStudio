using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 眼睛注视方向枚举
    /// 对应 RimWorld 等距视角下可感知到的 5 个方向
    /// </summary>
    public enum EyeDirection
    {
        Center, // 正视前方（默认回退）
        Left,   // 向左注视
        Right,  // 向右注视
        Up,     // 向上注视（背向摄像机时）
        Down,   // 向下注视
    }

    /// <summary>
    /// 眼睛方向贴图配置
    /// 为每个方向指定一张眼睛覆盖贴图，随 Pawn 朝向自动切换。
    ///
    /// 渲染方式（方案 A）：
    ///   以额外覆盖层叠加在头部节点上，贴图替换实现方向感。
    ///   依赖现有 ExpressionType 驱动框架，不引入程序偏移。
    /// </summary>
    public class PawnEyeDirectionConfig
    {
        public class EyeMotionConfig
        {
            public float sideBiasX = 0.0002f;
            public float primaryWaveOffsetZ = 0.0004f;
            public float dirLeftOffsetX = -0.0012f;
            public float dirRightOffsetX = 0.0012f;
            public float dirUpOffsetZ = -0.0010f;
            public float dirDownOffsetZ = 0.0012f;
            public float neutralSoftOffsetZ = 0.0005f;
            public float neutralLookDownOffsetZ = 0.0010f;
            public float neutralGlanceWaveOffsetX = 0.0008f;
            public float neutralGlanceSideOffsetX = 0.00035f;
            public float workFocusDownOffsetZ = 0.0016f;
            public float workFocusUpOffsetZ = -0.0012f;
            public float happySoftOffsetZ = -0.0006f;
            public float shockWideOffsetZ = -0.0018f;
            public float scaredWideOffsetZ = -0.0012f;
            public float scaredWideWaveOffsetX = 0.0006f;
            public float scaredWideSideOffsetX = 0.0003f;
            public float scaredFlinchOffsetZ = 0.0008f;
            public float scaredFlinchWaveOffsetX = 0.0007f;
            public float scaredFlinchSideOffsetX = 0.00045f;
            public float baseAngleWave = 0.15f;
            public float slowWaveOffsetZ = 0.0004f;
            public float scaleXBase = 1.01f;
            public float scaleXWaveAmplitude = 0.01f;

            public EyeMotionConfig Clone() => (EyeMotionConfig)MemberwiseClone();
        }

        public class PupilMotionConfig
        {
            public float frontLeftEyeOffsetX = -0.00018f;
            public float frontRightEyeOffsetX = 0.00018f;
            public float sideFacingOffsetX = 0.00018f;
            public float sideBiasX = 0.000028f;
            public float slowWaveOffsetZ = 0.00005f;
            public float dirLeftOffsetX = -0.00018f;
            public float dirRightOffsetX = 0.00018f;
            public float dirUpOffsetZ = -0.00014f;
            public float dirDownOffsetZ = 0.00016f;
            public float neutralSoftOffsetZ = 0.00004f;
            public float neutralLookDownOffsetZ = 0.00012f;
            public float neutralGlanceWaveOffsetX = 0.00010f;
            public float neutralGlanceSideOffsetX = 0.000045f;
            public float workFocusDownOffsetZ = 0.00020f;
            public float workFocusUpOffsetZ = -0.00015f;
            public float happyOpenOffsetZ = -0.00003f;
            public float shockWideOffsetZ = -0.00012f;
            public float scaredWideOffsetZ = -0.00008f;
            public float scaredWideWaveOffsetX = 0.00008f;
            public float scaredWideSideOffsetX = 0.00004f;
            public float scaredFlinchOffsetZ = 0.00008f;
            public float scaredFlinchWaveOffsetX = 0.00009f;
            public float scaredFlinchSideOffsetX = 0.000055f;
            public float transformAngleWave = 0.35f;
            public float finalWaveOffsetX = 0.00004f;
            public float focusScaleBase = 0.94f;
            public float focusScaleWave = 0.01f;
            public float slightlyContractedScaleBase = 0.88f;
            public float slightlyContractedScaleWave = 0.01f;
            public float contractedScaleBase = 0.78f;
            public float contractedScaleWave = 0.015f;
            public float dilatedScaleBase = 1.12f;
            public float dilatedScaleWave = 0.02f;
            public float dilatedMaxScaleBase = 1.22f;
            public float dilatedMaxScaleWave = 0.03f;
            public float scaredPulseScaleBase = 1.16f;
            public float scaredPulseScaleWave = 0.05f;
            public float shockScaredMinScaleBase = 1.08f;
            public float shockScaredMinScaleWave = 0.03f;
            public float happyMaxScaleBase = 0.96f;
            public float happyMaxScaleWave = 0.01f;
            public float sleepingScale = 0.9f;
            public float workFocusMaxScale = 0.98f;
            public float neutralSoftMaxScale = 0.96f;
            public float neutralLookDownMaxScale = 0.94f;
            public float shockWideMinScaleBase = 1.18f;
            public float shockWideMinScaleWave = 0.02f;
            public float scaredWideMinScaleBase = 1.14f;
            public float scaredWideMinScaleWave = 0.03f;
            public float scaredFlinchMinScaleBase = 1.04f;
            public float scaredFlinchMinScaleWave = 0.01f;

            public PupilMotionConfig Clone() => (PupilMotionConfig)MemberwiseClone();

            public void EnsureDirectionalDefaults()
            {
                if (Mathf.Approximately(frontLeftEyeOffsetX, 0f) && Mathf.Approximately(frontRightEyeOffsetX, 0f))
                {
                    frontLeftEyeOffsetX = dirLeftOffsetX;
                    frontRightEyeOffsetX = dirRightOffsetX;
                }

                if (Mathf.Approximately(sideFacingOffsetX, 0f))
                    sideFacingOffsetX = Mathf.Max(Mathf.Abs(dirLeftOffsetX), Mathf.Abs(dirRightOffsetX));
            }
        }

        public class LidMotionConfig
        {
            public float upperSideBiasX = 0.00035f;
            public float upperBlinkScaleX = 1.01f;
            public float upperBlinkScaleZ = 0.88f;
            public float upperCloseScaleX = 1.01f;
            public float upperCloseScaleZ = 0.90f;
            public float upperHalfBaseOffsetSubtract = 0.0016f;
            public float upperHalfNeutralSoftExtraOffset = 0.0003f;
            public float upperHalfLookDownExtraOffset = 0.0008f;
            public float upperHalfScaredExtraOffset = 0.0010f;
            public float upperHalfSlowWaveOffset = 0.0004f;
            public float upperHalfScaleDefault = 0.95f;
            public float upperHalfScaleNeutralSoft = 0.93f;
            public float upperHalfScaleLookDown = 0.91f;
            public float upperHalfScaleScared = 0.89f;
            public float upperHappySoftOffset = -0.0014f;
            public float upperHappyOpenOffset = -0.0008f;
            public float upperHappySoftScale = 0.90f;
            public float upperHappyOpenScale = 0.95f;
            public float upperHappyScaleX = 1.02f;
            public float upperHappyAngleBase = -1.2f;
            public float upperHappyAngleWave = 0.2f;
            public float upperHappySlowWaveOffset = 0.0004f;
            public float upperDefaultSlowWaveOffset = 0.0003f;
            public float upperBlinkClosingPhaseDuration = 0.5f;
            public float upperBlinkOpeningStart = 0.6f;
            public float upperBlinkOpeningDuration = 0.4f;

            public float lowerSideBiasX = 0.0002f;
            public float lowerBlinkOffset = -0.0024f;
            public float lowerBlinkScaleX = 1.00f;
            public float lowerBlinkScaleZ = 0.96f;
            public float lowerCloseOffset = -0.0018f;
            public float lowerCloseScaleX = 1.00f;
            public float lowerCloseScaleZ = 0.97f;
            public float lowerHalfOffset = -0.0012f;
            public float lowerHalfSlowWaveOffset = 0.0003f;
            public float lowerHalfScaleX = 1.00f;
            public float lowerHalfScaleZ = 0.985f;
            public float lowerHappyAngleBase = 0.85f;
            public float lowerHappyAngleWave = 0.15f;
            public float lowerHappyOffset = -0.0008f;
            public float lowerHappySlowWaveOffset = 0.0003f;
            public float lowerHappyScaleX = 1.01f;
            public float lowerHappyScaleZ = 0.98f;
            public float lowerDefaultSlowWaveOffset = 0.0002f;

            public float genericBlinkOffset = 0.0045f;
            public float genericBlinkScaleX = 1.02f;
            public float genericBlinkScaleZ = 0.72f;
            public float genericCloseOffset = 0.0035f;
            public float genericCloseScaleX = 1.01f;
            public float genericCloseScaleZ = 0.78f;
            public float genericHalfOffset = 0.0022f;
            public float genericHalfSlowWaveOffset = 0.0005f;
            public float genericHalfScaleX = 1.01f;
            public float genericHalfScaleZ = 0.89f;
            public float genericHappyAngleBase = -1.1f;
            public float genericHappyAngleWave = 0.25f;
            public float genericHappyOffset = -0.001f;
            public float genericHappySlowWaveOffset = 0.0005f;
            public float genericHappyScaleX = 1.03f;
            public float genericHappyScaleZ = 0.91f;
            public float genericDefaultSlowWaveOffset = 0.0004f;
            public float genericDefaultScaleZBase = 0.99f;
            public float genericDefaultScaleZWaveAmplitude = 0.01f;

            public LidMotionConfig Clone()
            {
                return (LidMotionConfig)MemberwiseClone();
            }
        }

        /// <summary>是否启用眼睛方向功能</summary>
        public bool enabled = false;

        /// <summary>正视前方贴图路径（Center，默认回退）</summary>
        public string texCenter = "";

        /// <summary>向左注视贴图路径</summary>
        public string texLeft = "";

        /// <summary>向右注视贴图路径</summary>
        public string texRight = "";

        /// <summary>向上注视贴图路径（Pawn 面朝北时）</summary>
        public string texUp = "";

        /// <summary>向下注视贴图路径</summary>
        public string texDown = "";

        /// <summary>
         /// 分层模式下，上眼睑在触发替换式眼部表情时的下移量。
        /// 0 = 不额外移动；建议值 0.002 ~ 0.008。
        /// 仅影响 LayeredDynamic 中的 UpperLid 程序位移。
        /// </summary>
        public float upperLidMoveDown = 0.0044f;

        /// <summary>程序化眼睑/眨眼运动参数。</summary>
        public LidMotionConfig lidMotion = new LidMotionConfig();

        /// <summary>程序化眼球运动参数。</summary>
        public EyeMotionConfig eyeMotion = new EyeMotionConfig();

        /// <summary>程序化瞳孔运动参数。</summary>
        public PupilMotionConfig pupilMotion = new PupilMotionConfig();

        // ─────────────────────────────────────────────
        // 查询 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 获取指定方向对应的贴图路径。
        /// 若目标方向未配置贴图，自动回退到 Center；
        /// Center 也未配置则返回空字符串。
        /// </summary>
        public string GetTexPath(EyeDirection direction)
        {
            string? path = direction switch
            {
                EyeDirection.Left  => texLeft,
                EyeDirection.Right => texRight,
                EyeDirection.Up    => texUp,
                EyeDirection.Down  => texDown,
                _                  => texCenter
            };

            // 未配置时回退到 Center
            if (string.IsNullOrEmpty(path) && direction != EyeDirection.Center)
                path = texCenter;

            return path ?? string.Empty;
        }

        /// <summary>是否配置了任意一张眼睛贴图</summary>
        public bool HasAnyTex()
            => !string.IsNullOrEmpty(texCenter)
            || !string.IsNullOrEmpty(texLeft)
            || !string.IsNullOrEmpty(texRight)
            || !string.IsNullOrEmpty(texUp)
            || !string.IsNullOrEmpty(texDown);

        // ─────────────────────────────────────────────
        // 克隆
        // ─────────────────────────────────────────────

        public PawnEyeDirectionConfig Clone() => new PawnEyeDirectionConfig
        {
            enabled          = this.enabled,
            texCenter        = this.texCenter,
            texLeft          = this.texLeft,
            texRight         = this.texRight,
            texUp            = this.texUp,
            texDown          = this.texDown,
            upperLidMoveDown = this.upperLidMoveDown,
            lidMotion        = this.lidMotion?.Clone() ?? new LidMotionConfig(),
            eyeMotion        = this.eyeMotion?.Clone() ?? new EyeMotionConfig(),
            pupilMotion      = this.pupilMotion?.Clone() ?? new PupilMotionConfig(),
        };
    }
}
