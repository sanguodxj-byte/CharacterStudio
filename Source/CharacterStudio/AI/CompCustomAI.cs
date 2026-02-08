using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace CharacterStudio.AI
{
    /// <summary>
    /// AI行为调整用Hediff定义名称
    /// </summary>
    public static class CS_HediffDefOf
    {
        // 这些Hediff需要在Defs中定义
        public static HediffDef? CS_BossImmunity;
        public static HediffDef? CS_SpeedModifier;
        public static HediffDef? CS_PainImmunity;
    }

    /// <summary>
    /// 自定义 AI 组件
    /// 用于应用 AI 行为定义
    /// </summary>
    public class CompCustomAI : ThingComp
    {
        // 缓存的行为状态
        private bool behaviorApplied = false;
        private float cachedMoveSpeedMultiplier = 1f;

        public CompProperties_CustomAI Props => (CompProperties_CustomAI)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            var pawn = parent as Pawn;
            if (pawn == null) return;

            // 无论是否重载都应用行为（确保行为一致性）
            if (!behaviorApplied)
            {
                ApplyBehavior(pawn);
                behaviorApplied = true;
            }
        }

        private void ApplyBehavior(Pawn pawn)
        {
            var behavior = Props.behavior;
            if (behavior == null) return;

            try
            {
                // 应用疼痛免疫
                if (behavior.painImmune)
                {
                    ApplyPainImmunity(pawn);
                }

                // 应用移动速度调整
                if (behavior.moveSpeedMultiplier != 1f)
                {
                    cachedMoveSpeedMultiplier = behavior.moveSpeedMultiplier;
                    ApplyMoveSpeedModifier(pawn, behavior.moveSpeedMultiplier);
                }

                // 应用敌对状态
                if (behavior.alwaysHostile && pawn.Faction == Faction.OfPlayer)
                {
                    pawn.SetFaction(Faction.OfAncientsHostile);
                }

                // 应用 Boss 逻辑
                if (behavior.behaviorType == AIBehaviorType.Boss)
                {
                    ApplyBossImmunities(pawn);
                }

                Log.Message($"[CharacterStudio] 已应用AI行为 {behavior.behaviorType} 到 {pawn.LabelShort}");
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用AI行为时出错: {ex}");
            }
        }

        /// <summary>
        /// 应用疼痛免疫
        /// </summary>
        private void ApplyPainImmunity(Pawn pawn)
        {
            // 方案1: 使用内置的 Hediff
            // 方案2: 添加自定义 Hediff (需要在Defs中定义)
            
            // 检查是否已有疼痛免疫
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicShock))
            {
                return; // 已有类似效果
            }

            // 注意: 完整实现需要创建自定义HediffDef
            // 这里使用简化方案：通过能力或特质实现
        }

        /// <summary>
        /// 应用移动速度调整
        /// </summary>
        private void ApplyMoveSpeedModifier(Pawn pawn, float multiplier)
        {
            // 方案: 添加带有移动速度偏移的Hediff
            // 完整实现需要自定义HediffDef with StatModifiers
            
            // 简化方案：使用现有的Go-juice效果作为速度提升参考
            if (multiplier > 1f)
            {
                // 可以添加类似Go-juice的效果
            }
        }

        /// <summary>
        /// 应用Boss免疫效果
        /// </summary>
        private void ApplyBossImmunities(Pawn pawn)
        {
            // Boss单位应该：
            // 1. 免疫精神崩溃
            // 2. 免疫某些负面状态
            // 3. 可能有额外的护甲/生命值
            
            // 注意: 完整实现需要Harmony补丁拦截MentalBreaker
        }

        public override void CompTick()
        {
            base.CompTick();

            var pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead) return;

            var behavior = Props.behavior;
            if (behavior == null) return;

            // 每 60 帧检查一次行为状态
            if (pawn.IsHashIntervalTick(60))
            {
                CheckBehaviorState(pawn, behavior);
            }
        }

        private void CheckBehaviorState(Pawn pawn, AIBehaviorDef behavior)
        {
            switch (behavior.behaviorType)
            {
                case AIBehaviorType.Boss:
                    HandleBossBehavior(pawn, behavior);
                    break;
                case AIBehaviorType.Rusher:
                    HandleRusherBehavior(pawn, behavior);
                    break;
                case AIBehaviorType.Sniper:
                    HandleSniperBehavior(pawn, behavior);
                    break;
                case AIBehaviorType.Tank:
                    HandleTankBehavior(pawn, behavior);
                    break;
                default:
                    // Normal behavior - no special handling
                    break;
            }
        }

        /// <summary>
        /// Boss行为：免疫精神崩溃，持续战斗
        /// </summary>
        private void HandleBossBehavior(Pawn pawn, AIBehaviorDef behavior)
        {
            // 如果进入了非战斗精神状态，强制结束
            var mentalState = pawn.mindState?.mentalStateHandler?.CurStateDef;
            if (mentalState != null &&
                mentalState != MentalStateDefOf.ManhunterPermanent &&
                mentalState != MentalStateDefOf.Manhunter)
            {
                // 尝试结束非战斗精神状态
                pawn.mindState?.mentalStateHandler?.Reset();
            }
        }

        /// <summary>
        /// 突击者行为：无视掩体，直接冲锋
        /// </summary>
        private void HandleRusherBehavior(Pawn pawn, AIBehaviorDef behavior)
        {
            // 如果当前在掩体后面，强制移动
            // 这需要更复杂的Job系统集成
        }

        /// <summary>
        /// 狙击手行为：保持距离，风筝战术
        /// </summary>
        private void HandleSniperBehavior(Pawn pawn, AIBehaviorDef behavior)
        {
            // 检测附近敌人，如果太近则后撤
            if (behavior.preferredRange > 0)
            {
                // 实现风筝逻辑
            }
        }

        /// <summary>
        /// 坦克行为：吸引仇恨，保护队友
        /// </summary>
        private void HandleTankBehavior(Pawn pawn, AIBehaviorDef behavior)
        {
            // 检测附近受伤的友军，尝试挡在前面
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            
            Scribe_Values.Look(ref behaviorApplied, "behaviorApplied", false);
            Scribe_Values.Look(ref cachedMoveSpeedMultiplier, "cachedMoveSpeedMultiplier", 1f);
        }
    }

    public class CompProperties_CustomAI : CompProperties
    {
        public AIBehaviorDef? behavior;

        public CompProperties_CustomAI()
        {
            this.compClass = typeof(CompCustomAI);
        }
    }
}