using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 模块化能力组件
    /// 负责执行 ModularAbilityDef 中定义的原子效果及视觉特效
    /// </summary>
    public class CompAbilityEffect_Modular : CompAbilityEffect
    {
        public new CompProperties_AbilityModular Props => (CompProperties_AbilityModular)props;

        // 延迟视觉特效队列：(触发时间tick, 特效配置, 目标)
        private readonly List<(int triggerTick, AbilityVisualEffectConfig vfxConfig, LocalTargetInfo target)> pendingVfx
            = new List<(int, AbilityVisualEffectConfig, LocalTargetInfo)>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (Props.effects != null)
            {
                foreach (var effectConfig in Props.effects)
                {
                    // 检查触发几率
                    if (Rand.Value > effectConfig.chance) continue;

                    // 执行游戏逻辑效果
                    var worker = EffectWorkerFactory.GetWorker(effectConfig.type);
                    worker.Apply(effectConfig, target, parent.pawn);
                }
            }

            // 排队视觉特效（立即或延迟）
            if (Props.visualEffects != null)
            {
                int nowTick = Find.TickManager?.TicksGame ?? 0;
                foreach (var vfx in Props.visualEffects)
                {
                    if (vfx == null) continue;
                    if (vfx.delayTicks <= 0)
                    {
                        // 立即播放
                        PlayVfx(vfx, target);
                    }
                    else
                    {
                        // 入队延迟播放
                        pendingVfx.Add((nowTick + vfx.delayTicks, vfx, target));
                    }
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (pendingVfx.Count == 0) return;
            int nowTick = Find.TickManager?.TicksGame ?? 0;

            for (int i = pendingVfx.Count - 1; i >= 0; i--)
            {
                var (triggerTick, vfxConfig, target) = pendingVfx[i];
                if (nowTick >= triggerTick)
                {
                    PlayVfx(vfxConfig, target);
                    pendingVfx.RemoveAt(i);
                }
            }
        }

        private void PlayVfx(AbilityVisualEffectConfig vfx, LocalTargetInfo target)
        {
            var caster = parent?.pawn;
            if (caster == null || caster.Map == null) return;
            try
            {
                var worker = VisualEffectWorkerFactory.GetWorker(vfx.type);
                worker.Play(vfx, target, caster);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[CharacterStudio] VFX 播放异常: {ex.Message}");
            }
        }
    }

    public class CompProperties_AbilityModular : CompProperties_AbilityEffect
    {
        // 游戏逻辑效果列表
        public List<AbilityEffectConfig> effects = new List<AbilityEffectConfig>();

        // 视觉特效列表
        public List<AbilityVisualEffectConfig> visualEffects = new List<AbilityVisualEffectConfig>();

        public CompProperties_AbilityModular()
        {
            this.compClass = typeof(CompAbilityEffect_Modular);
        }
    }
}
