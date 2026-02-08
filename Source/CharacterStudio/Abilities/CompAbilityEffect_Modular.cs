using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 模块化能力组件
    /// 负责执行 ModularAbilityDef 中定义的原子效果
    /// </summary>
    public class CompAbilityEffect_Modular : CompAbilityEffect
    {
        public new CompProperties_AbilityModular Props => (CompProperties_AbilityModular)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 获取模块化定义
            // 注意：这里假设 AbilityDef 实际上是 ModularAbilityDef 或者我们在 Props 中存储了引用
            // 在实际导出时，我们会生成一个包含此组件的标准 AbilityDef
            // 因此我们需要在 Props 中存储效果列表
            
            if (Props.effects == null) return;

            foreach (var effectConfig in Props.effects)
            {
                // 检查触发几率
                if (Rand.Value > effectConfig.chance) continue;

                // 执行效果
                var worker = EffectWorkerFactory.GetWorker(effectConfig.type);
                worker.Apply(effectConfig, target, parent.pawn);
            }
        }
    }

    public class CompProperties_AbilityModular : CompProperties_AbilityEffect
    {
        // 存储效果列表
        public List<AbilityEffectConfig> effects = new List<AbilityEffectConfig>();

        public CompProperties_AbilityModular()
        {
            this.compClass = typeof(CompAbilityEffect_Modular);
        }
    }
}