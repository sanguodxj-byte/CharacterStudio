using System;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    public enum AnimBone { Body, Head }

    public partial class CompPawnSkin
    {
        /// <summary>
        /// 仅获取动态变化的动画增量（Sin波部分）。
        /// 这里的 Head 返回的是相对于 Body 的额外偏移量，用于解决层级叠加问题。
        /// </summary>
        public Vector3 GetAnimationDelta(AnimBone bone)
        {
            if (ActiveSkin?.animationConfig == null || !ActiveSkin.animationConfig.enabled)
                return Vector3.zero;

            Vector3 delta = Vector3.zero;
            var cfg = ActiveSkin.animationConfig.procedural;
            float time = (Find.TickManager.TicksGame + parent.thingIDNumber);

            if (cfg.breathingEnabled)
            {
                float breath = Mathf.Sin(time * 0.05f * cfg.breathingSpeed);
                float bodyLift = breath * cfg.breathingAmplitude;
                
                if (bone == AnimBone.Body)
                {
                    delta.z += bodyLift;
                }
                else if (bone == AnimBone.Head)
                {
                    // Head 逻辑上总跳动是 bodyLift * 1.5
                    // 但因为 Head 已经是 Body 的子节点，它已经继承了 1.0 倍的 bodyLift
                    // 所以我们在这里只需补上剩下的 0.5 倍差值，防止双重叠加。
                    delta.z += (bodyLift * 1.5f) - bodyLift;
                }
            }

            if (cfg.hoveringEnabled || IsFlightStateActive())
            {
                float hover = Mathf.Sin(time * 0.04f * cfg.hoveringSpeed);
                // 悬浮通常只应用在 Body 根节点，Head 会自动跟随，所以 Head 不需要差值
                if (bone == AnimBone.Body)
                {
                    delta.y += hover * cfg.hoveringAmplitude;
                }
            }

            return delta;
        }

        public float GetBreathingScale(AnimBone bone)
        {
            if (bone == AnimBone.Head) return 1.0f; // 头部不缩放，且不随身体缩放（防止面部形变）
            if (ActiveSkin?.animationConfig?.procedural == null || !ActiveSkin.animationConfig.procedural.breathingEnabled)
                return 1.0f;

            var cfg = ActiveSkin.animationConfig.procedural;
            float breath = Mathf.Sin((Find.TickManager.TicksGame + parent.thingIDNumber) * 0.05f * cfg.breathingSpeed);
            return 1.0f + breath * (cfg.breathingAmplitude * 0.5f);
        }
    }
}
