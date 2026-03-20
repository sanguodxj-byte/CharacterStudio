using System.Collections.Generic;
using CharacterStudio.Core;
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

            HandleRuntimeComponentsAtApply();
            QueueVisualEffects(target);
        }

        private void HandleRuntimeComponentsAtApply()
        {
            if (Props.runtimeComponents == null || Props.runtimeComponents.Count == 0)
            {
                return;
            }

            Pawn? caster = parent?.pawn;
            CompPawnSkin? skinComp = caster?.GetComp<CompPawnSkin>();
            if (caster == null || skinComp == null)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            foreach (var component in Props.runtimeComponents)
            {
                if (component == null || !component.enabled)
                {
                    continue;
                }

                switch (component.type)
                {
                    case AbilityRuntimeComponentType.QComboWindow:
                        int comboWindow = component.comboWindowTicks > 0 ? component.comboWindowTicks : 12;
                        skinComp.qComboWindowEndTick = nowTick + comboWindow;
                        break;
                    case AbilityRuntimeComponentType.RStackDetonation:
                        if (skinComp.rStackingEnabled && !skinComp.rSecondStageReady)
                        {
                            int requiredStacks = component.requiredStacks > 0 ? component.requiredStacks : 7;
                            skinComp.rStackCount = System.Math.Min(requiredStacks, skinComp.rStackCount + 1);
                            if (skinComp.rStackCount >= requiredStacks)
                            {
                                skinComp.rStackingEnabled = false;
                                skinComp.rSecondStageReady = true;
                                Messages.Message("CS_Ability_R_Ready".Translate(), MessageTypeDefOf.PositiveEvent, false);
                            }
                            else
                            {
                                Messages.Message("CS_Ability_R_StackGain".Translate(skinComp.rStackCount, requiredStacks), MessageTypeDefOf.NeutralEvent, false);
                            }
                        }
                        break;
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

        private void QueueVisualEffects(LocalTargetInfo target)
        {
            if (Props.visualEffects == null || Props.visualEffects.Count == 0)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            foreach (var vfx in Props.visualEffects)
            {
                if (vfx == null || !vfx.enabled)
                {
                    continue;
                }

                if (!ShouldHandleTriggerAtApply(vfx.trigger))
                {
                    continue;
                }

                int repeatCount = vfx.repeatCount <= 0 ? 1 : vfx.repeatCount;
                int repeatIntervalTicks = vfx.repeatIntervalTicks < 0 ? 0 : vfx.repeatIntervalTicks;

                for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                {
                    int totalDelay = vfx.delayTicks + (repeatIndex * repeatIntervalTicks);
                    if (totalDelay <= 0)
                    {
                        PlayVfx(vfx, target);
                    }
                    else
                    {
                        pendingVfx.Add((nowTick + totalDelay, vfx, target));
                    }
                }
            }
        }

        private static bool ShouldHandleTriggerAtApply(AbilityVisualEffectTrigger trigger)
        {
            switch (trigger)
            {
                case AbilityVisualEffectTrigger.OnTargetApply:
                case AbilityVisualEffectTrigger.OnCastFinish:
                    return true;
                case AbilityVisualEffectTrigger.OnCastStart:
                case AbilityVisualEffectTrigger.OnWarmup:
                case AbilityVisualEffectTrigger.OnDurationTick:
                case AbilityVisualEffectTrigger.OnExpire:
                default:
                    return true;
            }
        }

        private void PlayVfx(AbilityVisualEffectConfig vfx, LocalTargetInfo target)
        {
            var caster = parent?.pawn;
            if (caster == null || caster.Map == null) return;

            try
            {
                if (vfx.sourceMode == AbilityVisualEffectSourceMode.Preset && !string.IsNullOrWhiteSpace(vfx.presetDefName))
                {
                    Log.Warning($"[CharacterStudio] 视觉特效预设 '{vfx.presetDefName}' 当前阶段尚未接入运行时，已回退到内建特效 {vfx.type}。");
                }

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

        // 运行时组件列表（用于 Q 连段 / E 短跳 / R 叠层引爆 等扩展行为）
        public List<AbilityRuntimeComponentConfig> runtimeComponents = new List<AbilityRuntimeComponentConfig>();

        public CompProperties_AbilityModular()
        {
            this.compClass = typeof(CompAbilityEffect_Modular);
        }
    }
}