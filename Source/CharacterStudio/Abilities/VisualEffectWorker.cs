using System;
using System.Collections.Generic;
using System.Linq;
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
            return ResolvePosition(config, target, caster).ToIntVec3();
        }

        protected static Vector3 ResolvePosition(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            Vector3 pos;
            switch (config.target)
            {
                case VisualEffectTarget.Caster:
                    pos = caster.DrawPos;
                    break;
                case VisualEffectTarget.Target:
                    if (target.HasThing)
                    {
                        pos = target.Thing.DrawPos;
                    }
                    else if (target.IsValid)
                    {
                        pos = target.Cell.ToVector3Shifted();
                    }
                    else
                    {
                        pos = caster.DrawPos;
                    }
                    break;
                default: // Both — 默认在目标位置（调用者可循环调用两次）
                    pos = target.IsValid ? target.Cell.ToVector3Shifted() : caster.DrawPos;
                    break;
            }

            pos += config.offset;
            pos.y += config.heightOffset;

            if (!TryResolveFacingBasis(config, target, caster, out Vector3 forward, out Vector3 right))
            {
                return pos;
            }

            return pos + forward * config.forwardOffset + right * config.sideOffset;
        }

        protected static IEnumerable<Vector3> ResolvePositions(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            if (config.target == VisualEffectTarget.Both)
            {
                Vector3 casterPos = ResolvePositionForMode(config, target, caster, VisualEffectTarget.Caster);
                yield return casterPos;

                Vector3 targetPos = ResolvePositionForMode(config, target, caster, VisualEffectTarget.Target);
                if ((targetPos - casterPos).sqrMagnitude > 0.0001f)
                {
                    yield return targetPos;
                }

                yield break;
            }

            yield return ResolvePosition(config, target, caster);
        }

        private static Vector3 ResolvePositionForMode(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster, VisualEffectTarget mode)
        {
            AbilityVisualEffectConfig resolvedConfig = config.Clone();
            resolvedConfig.target = mode;
            return ResolvePosition(resolvedConfig, target, caster);
        }

        private static bool TryResolveFacingBasis(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster, out Vector3 forward, out Vector3 right)
        {
            AbilityVisualFacingMode facingMode = ResolveFacingMode(config);
            if (facingMode == AbilityVisualFacingMode.None)
            {
                forward = Vector3.zero;
                right = Vector3.zero;
                return false;
            }

            IntVec3 forwardCell = facingMode == AbilityVisualFacingMode.CastDirection
                ? ResolveCastDirectionCell(target, caster)
                : caster.Rotation.FacingCell;
            if (forwardCell == IntVec3.Zero)
            {
                forwardCell = caster.Rotation.FacingCell;
            }

            forward = forwardCell.ToVector3();
            if (forward == Vector3.zero)
            {
                forward = new Vector3(0f, 0f, 1f);
            }

            right = new Vector3(forward.z, 0f, -forward.x);
            return true;
        }

        private static AbilityVisualFacingMode ResolveFacingMode(AbilityVisualEffectConfig config)
        {
            if (!Enum.IsDefined(typeof(AbilityVisualFacingMode), config.facingMode))
            {
                return config.useCasterFacing ? AbilityVisualFacingMode.CasterFacing : AbilityVisualFacingMode.None;
            }

            if (config.facingMode == AbilityVisualFacingMode.None && config.useCasterFacing)
            {
                return AbilityVisualFacingMode.CasterFacing;
            }

            return config.facingMode;
        }

        private static IntVec3 ResolveCastDirectionCell(LocalTargetInfo target, Pawn caster)
        {
            IntVec3 origin = caster.Position;
            IntVec3 destination = target.IsValid ? target.Cell : origin + caster.Rotation.FacingCell;
            IntVec3 delta = destination - origin;
            if (delta == IntVec3.Zero)
            {
                return caster.Rotation.FacingCell;
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
            {
                return delta.x >= 0 ? IntVec3.East : IntVec3.West;
            }

            return delta.z >= 0 ? IntVec3.North : IntVec3.South;
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
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
                FleckMaker.ThrowDustPuff(pos, map, config.scale);
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
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
                FleckMaker.ThrowMicroSparks(pos, map);
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
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
                FleckMaker.ThrowLightningGlow(pos, map, config.scale);
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
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
                FleckMaker.ThrowFireGlow(pos, map, config.scale);
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
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
                FleckMaker.ThrowSmoke(pos, map, config.scale);
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
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
                PlayExplosionAt(pos, map, config.scale);
        }

        private static void PlayExplosionAt(Vector3 pos, Map map, float scale)
        {
            FleckMaker.ThrowDustPuff(pos, map, scale * 1.5f);
            FleckMaker.ThrowLightningGlow(pos, map, scale * 2f);
            FleckMaker.ThrowMicroSparks(pos, map);
        }
    }

    /// <summary>
    /// 飞行喷射蓝光特效（机械推进尾焰）
    /// </summary>
    public class VisualEffectWorker_FlightJetBlue : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            var map = caster.Map;
            if (map == null) return;

            Vector3 basePos = caster.DrawPos;
            Vector3 backward = -caster.Rotation.FacingCell.ToVector3();
            if (backward == Vector3.zero)
            {
                backward = new Vector3(0f, 0f, -1f);
            }

            float jetScale = Mathf.Max(0.8f, config.scale);
            Vector3 leftPos = basePos + backward * 0.28f + new Vector3(-0.18f, 0f, 0f);
            Vector3 rightPos = basePos + backward * 0.28f + new Vector3(0.18f, 0f, 0f);

            FleckMaker.ThrowLightningGlow(leftPos, map, jetScale * 0.75f);
            FleckMaker.ThrowLightningGlow(rightPos, map, jetScale * 0.75f);
            FleckMaker.ThrowMicroSparks(leftPos + backward * 0.12f, map);
            FleckMaker.ThrowMicroSparks(rightPos + backward * 0.12f, map);
            FleckMaker.ThrowDustPuff(leftPos + backward * 0.18f, map, jetScale * 0.45f);
            FleckMaker.ThrowDustPuff(rightPos + backward * 0.18f, map, jetScale * 0.45f);
        }
    }

    /// <summary>
    /// 飞行喷射尾流特效（轻烟+火花）
    /// </summary>
    public class VisualEffectWorker_FlightJetTrail : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            var map = caster.Map;
            if (map == null) return;

            Vector3 basePos = caster.DrawPos;
            Vector3 backward = -caster.Rotation.FacingCell.ToVector3();
            if (backward == Vector3.zero)
            {
                backward = new Vector3(0f, 0f, -1f);
            }

            float trailScale = Mathf.Max(0.6f, config.scale);
            for (int i = 0; i < 3; i++)
            {
                float factor = 0.18f + 0.14f * i;
                Vector3 center = basePos + backward * factor;
                FleckMaker.ThrowSmoke(center, map, trailScale * (0.55f + 0.1f * i));
            }
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
            { (AbilityVisualEffectType)1001, typeof(VisualEffectWorker_FlightJetBlue) },
            { (AbilityVisualEffectType)1002, typeof(VisualEffectWorker_FlightJetTrail) },
        };

        private static readonly Dictionary<string, AbilityVisualEffectType> presetTypes
            = new Dictionary<string, AbilityVisualEffectType>(StringComparer.OrdinalIgnoreCase)
        {
            { "DustPuff", AbilityVisualEffectType.DustPuff },
            { "MicroSparks", AbilityVisualEffectType.MicroSparks },
            { "LightningGlow", AbilityVisualEffectType.LightningGlow },
            { "FireGlow", AbilityVisualEffectType.FireGlow },
            { "Smoke", AbilityVisualEffectType.Smoke },
            { "Explosion", AbilityVisualEffectType.ExplosionEffect },
            { "ExplosionEffect", AbilityVisualEffectType.ExplosionEffect },
            { "FlightJetBlue", (AbilityVisualEffectType)1001 },
            { "FlightJetTrail", (AbilityVisualEffectType)1002 },
        };

        public static IReadOnlyList<string> GetRegisteredPresetNames()
        {
            return presetTypes.Keys
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool TryResolvePresetType(string? presetDefName, out AbilityVisualEffectType resolvedType)
        {
            resolvedType = AbilityVisualEffectType.DustPuff;
            if (presetDefName is not string presetName || string.IsNullOrWhiteSpace(presetName))
            {
                return false;
            }

            string trimmedPresetName = presetName.Trim();
            if (presetTypes.TryGetValue(trimmedPresetName, out resolvedType))
            {
                return true;
            }

            if (Enum.TryParse(trimmedPresetName, true, out AbilityVisualEffectType parsedType)
                && workerTypes.ContainsKey(parsedType))
            {
                resolvedType = parsedType;
                return true;
            }

            return false;
        }

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
