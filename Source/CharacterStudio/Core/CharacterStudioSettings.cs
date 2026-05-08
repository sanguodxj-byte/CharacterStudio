using Verse;

namespace CharacterStudio.Core
{
    public sealed class CharacterStudioSettings : ModSettings
    {
        // ── 更新频率配置（tick） ──

        /// <summary>状态评估间隔：多久重新解析表情/LOD。默认 3000 tick ≈ 50 秒。</summary>
        public int stateEvaluationInterval = 3000;

        /// <summary>HighFocus 动画推进间隔：眨眼/帧动画/程序动画。默认 5 tick。</summary>
        public int highFocusAnimationInterval = 5;

        // ── 视距阈值（格） ──

        /// <summary>视口中心距离在此范围内的角色获得 Standard LOD（有状态评估，无动画）。默认 15 格。</summary>
        public float visibleRangeStandardLod = 15f;

        // ── 调试工具 ──

        /// <summary>是否显示性能统计悬浮窗。</summary>
        public bool showPerformanceOverlay;

        public void ApplyBalancedPreset()
        {
            stateEvaluationInterval = 3000;
            highFocusAnimationInterval = 5;
            visibleRangeStandardLod = 15f;
        }

        public void ApplyPerformancePreset()
        {
            stateEvaluationInterval = 3000;
            highFocusAnimationInterval = 10;
            visibleRangeStandardLod = 10f;
        }

        public void ApplyUltraPerformancePreset()
        {
            stateEvaluationInterval = 6000;
            highFocusAnimationInterval = 15;
            visibleRangeStandardLod = 5f;
        }

        public void ResetToDefaults()
            => ApplyBalancedPreset();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref stateEvaluationInterval, nameof(stateEvaluationInterval), 3000);
            Scribe_Values.Look(ref highFocusAnimationInterval, nameof(highFocusAnimationInterval), 5);
            Scribe_Values.Look(ref visibleRangeStandardLod, nameof(visibleRangeStandardLod), 15f);
            Scribe_Values.Look(ref showPerformanceOverlay, nameof(showPerformanceOverlay), false);
        }
    }
}
