using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 视觉特效执行器基类
    /// </summary>
    public abstract class VisualEffectWorker
    {
        public abstract void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster);

        /// <summary>
        /// 根据目标设置解析作用格子
        /// </summary>
        protected static IntVec3 ResolveCell(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            switch (config.target)
            {
                case VisualEffectTarget.Caster:
                    return caster.Position;
                case VisualEffectTarget.Target:
                    return target.IsValid ? target.Cell : caster.Position;
                default: // Both — 默认在目标位置（调用者可循环调用两次）
                    return target.IsValid ? target.Cell : caster.Position;
            }
        }
    }

    /// <summary>
    /// 尘埃喷溅特效
    /// </summary>
    public class VisualEffectWorker_DustPuff : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            var map = caster.Map;
            if (map == null) return;
            IntVec3 cell = ResolveCell(config, target, caster);
            FleckMaker.ThrowDustPuff(cell.ToVector3Shifted(), map, config.scale);
            if (config.target == VisualEffectTarget.Both && target.IsValid && target.Cell != caster.Position)
                FleckMaker.ThrowDustPuff(caster.Position.ToVector3Shifted(), map, config.scale);
        }
    }

    /// <summary>
    /// 微型火花特效
    /// </summary>
    public class VisualEffectWorker_MicroSparks : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            var map = caster.Map;
            if (map == null) return;
            IntVec3 cell = ResolveCell(config, target, caster);
            FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), map);
            if (config.target == VisualEffectTarget.Both && target.IsValid && target.Cell != caster.Position)
                FleckMaker.ThrowMicroSparks(caster.Position.ToVector3Shifted(), map);
        }
    }

    /// <summary>
    /// 闪电光晕特效
    /// </summary>
    public class VisualEffectWorker_LightningGlow : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            var map = caster.Map;
            if (map == null) return;
            IntVec3 cell = ResolveCell(config, target, caster);
            FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, config.scale);
            if (config.target == VisualEffectTarget.Both && target.IsValid && target.Cell != caster.Position)
                FleckMaker.ThrowLightningGlow(caster.Position.ToVector3Shifted(), map, config.scale);
        }
    }

    /// <summary>
    /// 火焰光晕特效
    /// </summary>
    public class VisualEffectWorker_FireGlow : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            var map = caster.Map;
            if (map == null) return;
            IntVec3 cell = ResolveCell(config, target, caster);
            FleckMaker.ThrowFireGlow(cell.ToVector3Shifted(), map, config.scale);
            if (config.target == VisualEffectTarget.Both && target.IsValid && target.Cell != caster.Position)
                FleckMaker.ThrowFireGlow(caster.Position.ToVector3Shifted(), map, config.scale);
        }
    }

    /// <summary>
    /// 烟雾特效
    /// </summary>
    public class VisualEffectWorker_Smoke : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            var map = caster.Map;
            if (map == null) return;
            IntVec3 cell = ResolveCell(config, target, caster);
            FleckMaker.ThrowSmoke(cell.ToVector3Shifted(), map, config.scale);
            if (config.target == VisualEffectTarget.Both && target.IsValid && target.Cell != caster.Position)
                FleckMaker.ThrowSmoke(caster.Position.ToVector3Shifted(), map, config.scale);
        }
    }

    /// <summary>
    /// 爆炸闪光特效（DustPuff + LightningGlow + MicroSparks 组合）
    /// </summary>
    public class VisualEffectWorker_ExplosionEffect : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            var map = caster.Map;
            if (map == null) return;
            IntVec3 cell = ResolveCell(config, target, caster);
            PlayExplosionAt(cell.ToVector3Shifted(), map, config.scale);
            if (config.target == VisualEffectTarget.Both && target.IsValid && target.Cell != caster.Position)
                PlayExplosionAt(caster.Position.ToVector3Shifted(), map, config.scale);
        }

        private static void PlayExplosionAt(Vector3 pos, Map map, float scale)
        {
            FleckMaker.ThrowDustPuff(pos, map, scale * 1.5f);
            FleckMaker.ThrowLightningGlow(pos, map, scale * 2f);
            FleckMaker.ThrowMicroSparks(pos, map);
        }
    }

    /// <summary>
    /// 工厂：根据特效类型创建对应 Worker
    /// </summary>
    public static class VisualEffectWorkerFactory
    {
        private static readonly Dictionary<AbilityVisualEffectType, Type> workerTypes
            = new Dictionary<AbilityVisualEffectType, Type>
        {
            { AbilityVisualEffectType.DustPuff,        typeof(VisualEffectWorker_DustPuff) },
            { AbilityVisualEffectType.MicroSparks,     typeof(VisualEffectWorker_MicroSparks) },
            { AbilityVisualEffectType.LightningGlow,   typeof(VisualEffectWorker_LightningGlow) },
            { AbilityVisualEffectType.FireGlow,        typeof(VisualEffectWorker_FireGlow) },
            { AbilityVisualEffectType.Smoke,           typeof(VisualEffectWorker_Smoke) },
            { AbilityVisualEffectType.ExplosionEffect, typeof(VisualEffectWorker_ExplosionEffect) },
        };

        public static VisualEffectWorker GetWorker(AbilityVisualEffectType type)
        {
            if (workerTypes.TryGetValue(type, out var workerType))
                return (VisualEffectWorker)Activator.CreateInstance(workerType);

            Log.Warning($"[CharacterStudio] [VisualEffectWorkerFactory] 未知特效类型: {type}, 回退到 DustPuff");
            return new VisualEffectWorker_DustPuff();
        }

        public static void RegisterWorkerType(AbilityVisualEffectType type, Type workerType)
        {
            if (!typeof(VisualEffectWorker).IsAssignableFrom(workerType))
            {
                Log.Error($"[CharacterStudio] [VisualEffectWorkerFactory] {workerType.Name} 不是 VisualEffectWorker 子类");
                return;
            }
            workerTypes[type] = workerType;
        }
    }
}
