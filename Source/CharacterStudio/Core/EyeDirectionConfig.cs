using System.Collections.Generic;
using CharacterStudio.Exporter;
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
            [XmlExportField] public float sideBiasX = 0.0016f;
            [XmlExportField] public float primaryWaveOffsetZ = 0.0032f;
            [XmlExportField] public float dirLeftOffsetX = -0.0096f;
            [XmlExportField] public float dirRightOffsetX = 0.0096f;
            [XmlExportField] public float dirUpOffsetZ = -0.008f;
            [XmlExportField] public float dirDownOffsetZ = 0.0096f;
            [XmlExportField] public float neutralSoftOffsetZ = 0.004f;
            [XmlExportField] public float neutralLookDownOffsetZ = 0.008f;
            [XmlExportField] public float neutralGlanceWaveOffsetX = 0.0064f;
            [XmlExportField] public float neutralGlanceSideOffsetX = 0.0028f;
            [XmlExportField] public float workFocusDownOffsetZ = 0.0128f;
            [XmlExportField] public float workFocusUpOffsetZ = -0.0096f;
            [XmlExportField] public float happySoftOffsetZ = -0.0048f;
            [XmlExportField] public float shockWideOffsetZ = -0.0144f;
            [XmlExportField] public float scaredWideOffsetZ = -0.0096f;
            [XmlExportField] public float scaredWideWaveOffsetX = 0.0048f;
            [XmlExportField] public float scaredWideSideOffsetX = 0.0024f;
            [XmlExportField] public float scaredFlinchOffsetZ = 0.0064f;
            [XmlExportField] public float scaredFlinchWaveOffsetX = 0.0056f;
            [XmlExportField] public float scaredFlinchSideOffsetX = 0.0036f;
            [XmlExportField] public float baseAngleWave = 0.15f;
            [XmlExportField] public float slowWaveOffsetZ = 0.0032f;
            [XmlExportField] public float scaleXBase = 1.01f;
            [XmlExportField] public float scaleXWaveAmplitude = 0.01f;

            // ── Eye white scaleZ per variant (previously hardcoded in FaceProfileBuilder) ──
            [XmlExportField] public float neutralSoftScaleZ = 0.95f;
            [XmlExportField] public float neutralLookDownScaleZ = 0.93f;
            [XmlExportField] public float happySoftScaleZ = 0.90f;
            [XmlExportField] public float shockWideScaleZ = 1.12f;
            [XmlExportField] public float shockWideScaleZWave = 0.03f;
            [XmlExportField] public float scaredWideScaleZ = 1.08f;
            [XmlExportField] public float scaredWideScaleZWave = 0.02f;
            [XmlExportField] public float scaredFlinchScaleZ = 0.94f;

            public EyeMotionConfig Clone() => (EyeMotionConfig)MemberwiseClone();
        }

        public class PupilMotionConfig
        {
            // ── 正面朝向瞳孔偏移（统一，不分左右） ──
            // frontBaseX 为幅度，运行时根据左右瞳孔自动取反。
            // 方向偏移为带符号值，左右瞳孔共用，直接应用。
            [XmlExportField] public float frontBaseX = 0.00144f;
            [XmlExportField] public float dirLeftX = -0.00144f;
            [XmlExportField] public float dirRightX = 0.00144f;
            [XmlExportField] public float dirUpZ = -0.00112f;
            [XmlExportField] public float dirDownZ = 0.00128f;

            // ── 侧面朝向偏移（带符号，直接应用） ──
            [XmlExportField] public float side_baseX = 0.00144f;
            [XmlExportField] public float side_dirLeftX = -0.00096f;
            [XmlExportField] public float side_dirRightX = 0.00096f;
            [XmlExportField] public float side_dirUpZ = -0.00080f;
            [XmlExportField] public float side_dirDownZ = 0.00096f;

            // ── 通用参数 (×8) ──
            [XmlExportField] public float sideBiasX = 0.000224f;
            [XmlExportField] public float slowWaveOffsetZ = 0.0004f;
            [XmlExportField] public float neutralSoftOffsetZ = 0.00032f;
            [XmlExportField] public float neutralLookDownOffsetZ = 0.00096f;
            [XmlExportField] public float neutralGlanceWaveOffsetX = 0.00080f;
            [XmlExportField] public float neutralGlanceSideOffsetX = 0.00036f;
            [XmlExportField] public float workFocusDownOffsetZ = 0.0016f;
            [XmlExportField] public float workFocusUpOffsetZ = 0.0012f;
            [XmlExportField] public float happyOpenOffsetZ = 0.00024f;
            [XmlExportField] public float shockWideOffsetZ = 0.00096f;
            [XmlExportField] public float scaredWideOffsetZ = 0.00064f;
            [XmlExportField] public float scaredWideWaveOffsetX = 0.00064f;
            [XmlExportField] public float scaredWideSideOffsetX = 0.00032f;
            [XmlExportField] public float scaredFlinchOffsetZ = 0.00064f;
            [XmlExportField] public float scaredFlinchWaveOffsetX = 0.00072f;
            [XmlExportField] public float scaredFlinchSideOffsetX = 0.00044f;
            [XmlExportField] public float transformAngleWave = 0.35f;
            [XmlExportField] public float finalWaveOffsetX = 0.00032f;
            [XmlExportField] public float focusScaleBase = 0.94f;
            [XmlExportField] public float focusScaleWave = 0.01f;
            [XmlExportField] public float slightlyContractedScaleBase = 0.88f;
            [XmlExportField] public float slightlyContractedScaleWave = 0.01f;
            [XmlExportField] public float contractedScaleBase = 0.78f;
            [XmlExportField] public float contractedScaleWave = 0.015f;
            [XmlExportField] public float dilatedScaleBase = 1.12f;
            [XmlExportField] public float dilatedScaleWave = 0.02f;
            [XmlExportField] public float dilatedMaxScaleBase = 1.22f;
            [XmlExportField] public float dilatedMaxScaleWave = 0.03f;
            [XmlExportField] public float scaredPulseScaleBase = 1.16f;
            [XmlExportField] public float scaredPulseScaleWave = 0.05f;
            [XmlExportField] public float shockScaredMinScaleBase = 1.08f;
            [XmlExportField] public float shockScaredMinScaleWave = 0.03f;
            [XmlExportField] public float happyMaxScaleBase = 0.96f;
            [XmlExportField] public float happyMaxScaleWave = 0.01f;
            [XmlExportField] public float sleepingScale = 0.9f;
            [XmlExportField] public float workFocusMaxScale = 0.98f;
            [XmlExportField] public float neutralSoftMaxScale = 0.96f;
            [XmlExportField] public float neutralLookDownMaxScale = 0.94f;
            [XmlExportField] public float shockWideMinScaleBase = 1.18f;
            [XmlExportField] public float shockWideMinScaleWave = 0.02f;
            [XmlExportField] public float scaredWideMinScaleBase = 1.14f;
            [XmlExportField] public float scaredWideMinScaleWave = 0.03f;
            [XmlExportField] public float scaredFlinchMinScaleBase = 1.04f;
            [XmlExportField] public float scaredFlinchMinScaleWave = 0.01f;

            public PupilMotionConfig Clone() => (PupilMotionConfig)MemberwiseClone();

            // ── 向后兼容：旧版 XML 字段映射到新版统一字段 ──
            // frontBaseX 取绝对值（幅度），方向偏移直接赋值（带符号）
            // getter 返回 0，仅用于满足 RimWorld XML 反序列化器对 { get; set; } 属性的要求
            public float leftPupil_frontBaseX { get { return 0f; } set { frontBaseX = Mathf.Abs(value); } }
            public float leftPupil_dirLeftX { get { return 0f; } set { dirLeftX = value; } }
            public float leftPupil_dirRightX { get { return 0f; } set { dirRightX = value; } }
            public float leftPupil_dirUpZ { get { return 0f; } set { dirUpZ = value; } }
            public float leftPupil_dirDownZ { get { return 0f; } set { dirDownZ = value; } }
            public float rightPupil_frontBaseX { get { return 0f; } set { frontBaseX = Mathf.Abs(value); } }
            public float rightPupil_dirLeftX { get { return 0f; } set { dirLeftX = value; } }
            public float rightPupil_dirRightX { get { return 0f; } set { dirRightX = value; } }
            public float rightPupil_dirUpZ { get { return 0f; } set { dirUpZ = value; } }
            public float rightPupil_dirDownZ { get { return 0f; } set { dirDownZ = value; } }
            public float sideFacingOffsetX { get { return 0f; } set { side_baseX = value; } }
        }

        public class LidMotionConfig
        {
            // ── Upper lid offsets (×8) ──
            [XmlExportField] public float upperSideBiasX = 0.0028f;
            [XmlExportField] public float upperBlinkScaleX = 1.01f;
            [XmlExportField] public float upperBlinkScaleZ = 0.88f;
            [XmlExportField] public float upperCloseScaleX = 1.01f;
            [XmlExportField] public float upperCloseScaleZ = 0.90f;
            [XmlExportField] public float upperHalfBaseOffsetSubtract = 0.0128f;
            [XmlExportField] public float upperHalfNeutralSoftExtraOffset = 0.0024f;
            [XmlExportField] public float upperHalfLookDownExtraOffset = 0.0064f;
            [XmlExportField] public float upperHalfScaredExtraOffset = 0.008f;
            [XmlExportField] public float upperHalfSlowWaveOffset = 0.0032f;
            [XmlExportField] public float upperHalfScaleDefault = 0.95f;
            [XmlExportField] public float upperHalfScaleNeutralSoft = 0.93f;
            [XmlExportField] public float upperHalfScaleLookDown = 0.91f;
            [XmlExportField] public float upperHalfScaleScared = 0.89f;
            [XmlExportField] public float upperHappySoftOffset = -0.0144f;
            [XmlExportField] public float upperHappyOpenOffset = -0.0048f;
            [XmlExportField] public float upperHappySoftScale = 0.90f;
            [XmlExportField] public float upperHappyOpenScale = 0.95f;
            [XmlExportField] public float upperHappyScaleX = 1.02f;
            [XmlExportField] public float upperHappyAngleBase = -1.2f;
            [XmlExportField] public float upperHappyAngleWave = 0.2f;
            [XmlExportField] public float upperHappySlowWaveOffset = 0.0032f;
            [XmlExportField] public float upperDefaultSlowWaveOffset = 0.0024f;
            [XmlExportField] public float upperBlinkClosingPhaseDuration = 0.5f;
            [XmlExportField] public float upperBlinkOpeningStart = 0.6f;
            [XmlExportField] public float upperBlinkOpeningDuration = 0.4f;

            // ── Lower lid offsets (×8) ──
            [XmlExportField] public float lowerSideBiasX = 0.0016f;
            [XmlExportField] public float lowerBlinkOffset = -0.0192f;
            [XmlExportField] public float lowerBlinkScaleX = 1.00f;
            [XmlExportField] public float lowerBlinkScaleZ = 0.96f;
            [XmlExportField] public float lowerCloseOffset = -0.0144f;
            [XmlExportField] public float lowerCloseScaleX = 1.00f;
            [XmlExportField] public float lowerCloseScaleZ = 0.97f;
            [XmlExportField] public float lowerHalfOffset = -0.0096f;
            [XmlExportField] public float lowerHalfSlowWaveOffset = 0.0024f;
            [XmlExportField] public float lowerHalfScaleX = 1.00f;
            [XmlExportField] public float lowerHalfScaleZ = 0.985f;
            [XmlExportField] public float lowerHappyAngleBase = 0.85f;
            [XmlExportField] public float lowerHappyAngleWave = 0.15f;
            [XmlExportField] public float lowerHappyOffset = -0.0064f;
            [XmlExportField] public float lowerHappySlowWaveOffset = 0.0024f;
            [XmlExportField] public float lowerHappyScaleX = 1.01f;
            [XmlExportField] public float lowerHappyScaleZ = 0.98f;
            [XmlExportField] public float lowerDefaultSlowWaveOffset = 0.0016f;

            // ── Generic lid offsets (×8) ──
            [XmlExportField] public float genericBlinkOffset = 0.036f;
            [XmlExportField] public float genericBlinkScaleX = 1.02f;
            [XmlExportField] public float genericBlinkScaleZ = 0.72f;
            [XmlExportField] public float genericCloseOffset = 0.028f;
            [XmlExportField] public float genericCloseScaleX = 1.01f;
            [XmlExportField] public float genericCloseScaleZ = 0.78f;
            [XmlExportField] public float genericHalfOffset = 0.0176f;
            [XmlExportField] public float genericHalfSlowWaveOffset = 0.004f;
            [XmlExportField] public float genericHalfScaleX = 1.01f;
            [XmlExportField] public float genericHalfScaleZ = 0.89f;
            [XmlExportField] public float genericHappyAngleBase = -1.1f;
            [XmlExportField] public float genericHappyAngleWave = 0.25f;
            [XmlExportField] public float genericHappyOffset = -0.008f;
            [XmlExportField] public float genericHappySlowWaveOffset = 0.004f;
            [XmlExportField] public float genericHappyScaleX = 1.03f;
            [XmlExportField] public float genericHappyScaleZ = 0.91f;
            [XmlExportField] public float genericDefaultSlowWaveOffset = 0.0032f;
            [XmlExportField] public float genericDefaultScaleZBase = 0.99f;
            [XmlExportField] public float genericDefaultScaleZWaveAmplitude = 0.01f;

            public LidMotionConfig Clone()
            {
                return (LidMotionConfig)MemberwiseClone();
            }
        }

        /// <summary>
        /// 获取一个共享的默认配置实例。
        /// 用于面部配置存在但未显式指定眼睛方向配置时的回退。
        /// </summary>
        public static PawnEyeDirectionConfig Default { get; } = new PawnEyeDirectionConfig();

        /// <summary>是否启用眼睛方向功能</summary>
        [XmlExportField(BoolToLower = true)] public bool enabled = false;

        /// <summary>正视前方贴图路径（Center，默认回退）</summary>
        [XmlExportField(SkipEmptyString = true)] public string texCenter = "";

        /// <summary>向左注视贴图路径</summary>
        [XmlExportField(SkipEmptyString = true)] public string texLeft = "";

        /// <summary>向右注视贴图路径</summary>
        [XmlExportField(SkipEmptyString = true)] public string texRight = "";

        /// <summary>向上注视贴图路径（Pawn 面朝北时）</summary>
        [XmlExportField(SkipEmptyString = true)] public string texUp = "";

        /// <summary>向下注视贴图路径</summary>
        [XmlExportField(SkipEmptyString = true)] public string texDown = "";

        /// <summary>
         /// 分层模式下，上眼睑在触发替换式眼部表情时的下移量。
        /// 0 = 不额外移动；建议值 0.002 ~ 0.008。
        /// 仅影响 LayeredDynamic 中的 UpperLid 程序位移。
        /// </summary>
        [XmlExportField(SkipDefault = 0.0352f, SkipDefaultFloat = true)] public float upperLidMoveDown = 0.0352f;

        /// <summary>程序化眼睑/眨眼运动参数。</summary>
        [XmlExportField] public LidMotionConfig lidMotion = new LidMotionConfig();

        /// <summary>程序化眼球运动参数。</summary>
        [XmlExportField] public EyeMotionConfig eyeMotion = new EyeMotionConfig();

        /// <summary>程序化瞳孔运动参数。</summary>
        [XmlExportField] public PupilMotionConfig pupilMotion = new PupilMotionConfig();

        // ─────────────────────────────────────────────
        // Profile 驱动的动画参数（JSON 可编辑）
        // 每个 FaceChannelProfileSet 包含一个通道所有状态的参数。
        // null 表示从旧 MotionConfig 字段实时构建（无缓存，保证编辑器修改立即生效）。
        // ─────────────────────────────────────────────

        /// <summary>眼球（Eye 白）通道动画参数。null 时从 eyeMotion 实时构建。</summary>
        [XmlExportField(Ignore = true)] public FaceChannelProfileSet? eyeProfiles;

        /// <summary>瞳孔（Pupil）通道动画参数。null 时从 pupilMotion 实时构建。</summary>
        [XmlExportField(Ignore = true)] public FaceChannelProfileSet? pupilProfiles;

        /// <summary>上眼睑（UpperLid）通道动画参数。null 时从 lidMotion 实时构建。</summary>
        [XmlExportField(Ignore = true)] public FaceChannelProfileSet? upperLidProfiles;

        /// <summary>下眼睑（LowerLid）通道动画参数。null 时从 lidMotion 实时构建。</summary>
        [XmlExportField(Ignore = true)] public FaceChannelProfileSet? lowerLidProfiles;

        /// <summary>
        /// 获取 Eye 通道的 profile。如果 eyeProfiles 已设置则使用它，
        /// 否则从 eyeMotion 字段实时构建（无缓存，编辑器修改立即反映）。
        /// </summary>
        public FaceChannelProfileSet GetOrBuildEyeProfiles()
        {
            return eyeProfiles ?? FaceProfileBuilder.BuildEyeDefaults(eyeMotion ?? new EyeMotionConfig());
        }

        public FaceChannelProfileSet GetOrBuildPupilProfiles()
        {
            return pupilProfiles ?? FaceProfileBuilder.BuildPupilDefaults(pupilMotion ?? new PupilMotionConfig());
        }

        public FaceChannelProfileSet GetOrBuildUpperLidProfiles()
        {
            return upperLidProfiles ?? FaceProfileBuilder.BuildUpperLidDefaults(lidMotion ?? new LidMotionConfig(), upperLidMoveDown);
        }

        public FaceChannelProfileSet GetOrBuildLowerLidProfiles()
        {
            return lowerLidProfiles ?? FaceProfileBuilder.BuildLowerLidDefaults(lidMotion ?? new LidMotionConfig());
        }

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
            eyeProfiles      = this.eyeProfiles,
            pupilProfiles    = this.pupilProfiles,
            upperLidProfiles = this.upperLidProfiles,
            lowerLidProfiles = this.lowerLidProfiles,
        };
    }
}
