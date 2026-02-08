using System;
using Verse;

namespace CharacterStudio.AI
{
    /// <summary>
    /// AI 行为模式
    /// </summary>
    public enum AIBehaviorType
    {
        /// <summary>正常行为</summary>
        Normal,
        /// <summary>突击者（无视掩体，冲锋）</summary>
        Rusher,
        /// <summary>狙击手（风筝战术）</summary>
        Sniper,
        /// <summary>坦克（嘲讽/守护）</summary>
        Tank,
        /// <summary>Boss（无所畏惧，免疫精神崩溃）</summary>
        Boss
    }

    /// <summary>
    /// AI 行为定义
    /// 定义角色的战斗行为和特殊逻辑
    /// </summary>
    public class AIBehaviorDef : Def
    {
        /// <summary>行为类型</summary>
        public AIBehaviorType behaviorType = AIBehaviorType.Normal;

        /// <summary>是否免疫疼痛</summary>
        public bool painImmune = false;

        /// <summary>移动速度倍率</summary>
        public float moveSpeedMultiplier = 1f;

        /// <summary>攻击距离偏好（0 = 自动）</summary>
        public float preferredRange = 0f;

        /// <summary>是否始终敌对</summary>
        public bool alwaysHostile = false;
    }
}