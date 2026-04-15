using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 单个通道的过渡状态。
    /// 跟踪从前一 profile 参数到当前 profile 参数的插值进度。
    /// 
    /// 设计为 struct 以避免 GC 分配。嵌入在 FaceRuntimeState 等持久对象中。
    /// 
    /// 求值流程:
    ///   1. 通道状态变化时调用 BeginTransition(targetProfile, nowTick)
    ///   2. 每帧求值时调用 Evaluate(primaryWave, slowWave) 获取当前帧的最终 transform
    ///   3. 过渡结束后（blendElapsed >= blendDuration），Evaluate 直接使用 target 参数
    /// </summary>
    public struct FaceBlendState
    {
        // ── 前一状态参数快照（用于插值起点）──
        private float prevAngleBase;
        private float prevOffsetXBase;
        private float prevOffsetZBase;
        private float prevScaleXBase;
        private float prevScaleZBase;

        // ── 当前目标参数引用 ──
        private FaceStateProfile? targetProfile; // class 引用，null 表示无 profile

        // ── 过渡时序 ──
        private int blendStartTick;
        private int blendDuration;

        // ── 状态标记 ──
        private bool hasActiveProfile;

        /// <summary>当前是否处于过渡中</summary>
        public bool IsBlending => blendDuration > 0 && targetProfile != null && targetProfile.blendIn > 0 && hasActiveProfile;

        /// <summary>是否已有有效 profile</summary>
        public bool HasProfile => hasActiveProfile;

        /// <summary>
        /// 开始一个新的状态过渡。
        /// 将当前参数快照为起点，设置目标 profile 和过渡时长。
        /// </summary>
        public void BeginTransition(FaceStateProfile? newProfile, int nowTick)
        {
            // 快照当前视觉输出参数作为插值起点
            if (hasActiveProfile && targetProfile != null)
            {
                prevAngleBase = targetProfile.angleBase;
                prevOffsetXBase = targetProfile.offsetXBase;
                prevOffsetZBase = targetProfile.offsetZBase;
                prevScaleXBase = targetProfile.scaleXBase;
                prevScaleZBase = targetProfile.scaleZBase;
            }
            else
            {
                prevAngleBase = 0f;
                prevOffsetXBase = 0f;
                prevOffsetZBase = 0f;
                prevScaleXBase = 1f;
                prevScaleZBase = 1f;
            }

            targetProfile = newProfile;
            hasActiveProfile = newProfile != null;
            blendDuration = newProfile?.blendIn ?? 0;
            blendStartTick = nowTick;
        }

        /// <summary>
        /// 强制设置当前 profile 而不过渡（用于初始化）。
        /// </summary>
        public void ForceSet(FaceStateProfile? profile)
        {
            targetProfile = profile;
            hasActiveProfile = profile != null;
            blendDuration = 0;
            blendStartTick = 0;

            if (profile != null)
            {
                prevAngleBase = profile.angleBase;
                prevOffsetXBase = profile.offsetXBase;
                prevOffsetZBase = profile.offsetZBase;
                prevScaleXBase = profile.scaleXBase;
                prevScaleZBase = profile.scaleZBase;
            }
            else
            {
                prevAngleBase = 0f;
                prevOffsetXBase = 0f;
                prevOffsetZBase = 0f;
                prevScaleXBase = 1f;
                prevScaleZBase = 1f;
            }
        }

        /// <summary>
        /// 计算当前帧的 angle 值（含过渡插值 + 波叠加）。
        /// </summary>
        public float EvaluateAngle(int currentTick, float primaryWave)
        {
            if (!hasActiveProfile || targetProfile == null)
                return 0f;

            float baseVal = BlendValue(prevAngleBase, targetProfile.angleBase, currentTick);
            return baseVal + primaryWave * targetProfile.angleWave;
        }

        /// <summary>
        /// 计算当前帧的 offset 向量（含过渡插值 + 波叠加）。
        /// </summary>
        public Vector3 EvaluateOffset(int currentTick, float primaryWave, float slowWave)
        {
            if (!hasActiveProfile || targetProfile == null)
                return Vector3.zero;

            float x = BlendValue(prevOffsetXBase, targetProfile.offsetXBase, currentTick) + primaryWave * targetProfile.offsetXWave;
            float z = BlendValue(prevOffsetZBase, targetProfile.offsetZBase, currentTick)
                + primaryWave * targetProfile.offsetZWave
                + slowWave * targetProfile.offsetZSlowWave;
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// 计算当前帧的 scale 向量（含过渡插值 + 波叠加）。
        /// </summary>
        public Vector3 EvaluateScale(int currentTick, float primaryWave, float slowWave)
        {
            if (!hasActiveProfile || targetProfile == null)
                return Vector3.one;

            float x = BlendValue(prevScaleXBase, targetProfile.scaleXBase, currentTick) + Mathf.Abs(primaryWave) * targetProfile.scaleXWave;
            float z = BlendValue(prevScaleZBase, targetProfile.scaleZBase, currentTick) + Mathf.Abs(slowWave) * targetProfile.scaleZWave;
            return new Vector3(x, 1f, z);
        }

        /// <summary>
        /// 获取当前 target profile 的 moveDown 值（用于眼睑闭合位移）。
        /// </summary>
        public float GetMoveDown()
        {
            if (!hasActiveProfile || targetProfile == null) return 0f;
            return targetProfile.moveDown;
        }

        /// <summary>
        /// 获取当前 target profile 的 sideBiasX 值。
        /// </summary>
        public float GetSideBiasX()
        {
            if (!hasActiveProfile || targetProfile == null) return 0f;
            return targetProfile.sideBiasX;
        }

        /// <summary>
        /// 获取当前 target profile 的引用（可能为 null）。
        /// </summary>
        public FaceStateProfile? GetCurrentProfile()
        {
            return hasActiveProfile ? targetProfile : null;
        }

        /// <summary>
        /// 获取变体增量参数。
        /// </summary>
        public FaceStateVariant? GetVariant(string variantName)
        {
            if (!hasActiveProfile || targetProfile == null) return null;
            return targetProfile.GetVariant(variantName);
        }

        // ── 内部工具 ──

        private float BlendValue(float from, float to, int currentTick)
        {
            if (blendDuration <= 0)
                return to;

            int elapsed = currentTick - blendStartTick;
            if (elapsed >= blendDuration)
                return to;

            if (elapsed <= 0)
                return from;

            float t = (float)elapsed / blendDuration;
            // smoothstep 缓动
            t = t * t * (3f - 2f * t);
            return Mathf.LerpUnclamped(from, to, t);
        }
    }
}
