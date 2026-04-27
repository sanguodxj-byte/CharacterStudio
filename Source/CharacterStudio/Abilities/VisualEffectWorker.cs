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
            return AbilityVfxPlayer.ResolveVfxPosition(config, target, caster);
        }

        protected static IEnumerable<Vector3> ResolvePositions(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            return AbilityVfxPlayer.ResolveVfxPositions(config, target, caster);
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

    public class VisualEffectWorker_SteamBurst : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            Map map = caster.Map;
            if (map == null) return;
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
            {
                FleckMaker.ThrowSmoke(pos, map, config.scale * 1.25f);
                FleckMaker.ThrowDustPuff(pos, map, config.scale * 0.7f);
            }
        }
    }

    public class VisualEffectWorker_EmberBurst : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            Map map = caster.Map;
            if (map == null) return;
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
            {
                FleckMaker.ThrowFireGlow(pos, map, config.scale * 0.85f);
                FleckMaker.ThrowMicroSparks(pos, map);
                FleckMaker.ThrowDustPuff(pos, map, config.scale * 0.45f);
            }
        }
    }

    public class VisualEffectWorker_ShockBurst : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            Map map = caster.Map;
            if (map == null) return;
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
            {
                FleckMaker.ThrowLightningGlow(pos, map, config.scale * 1.6f);
                FleckMaker.ThrowMicroSparks(pos, map);
                FleckMaker.ThrowSmoke(pos, map, config.scale * 0.35f);
            }
        }
    }

    public class VisualEffectWorker_DustRing : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            Map map = caster.Map;
            if (map == null) return;
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
            {
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * 90f * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.45f;
                    FleckMaker.ThrowDustPuff(pos + offset, map, config.scale * 0.7f);
                }
            }
        }
    }

    public class VisualEffectWorker_ArcSparkBurst : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            Map map = caster.Map;
            if (map == null) return;
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
            {
                FleckMaker.ThrowLightningGlow(pos, map, config.scale);
                FleckMaker.ThrowMicroSparks(pos + new Vector3(0.2f, 0f, 0f), map);
                FleckMaker.ThrowMicroSparks(pos + new Vector3(-0.2f, 0f, 0f), map);
                FleckMaker.ThrowMicroSparks(pos + new Vector3(0f, 0f, 0.2f), map);
            }
        }
    }

    public class VisualEffectWorker_FlameSurge : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            Map map = caster.Map;
            if (map == null) return;
            foreach (Vector3 pos in ResolvePositions(config, target, caster))
            {
                FleckMaker.ThrowFireGlow(pos, map, config.scale * 1.25f);
                FleckMaker.ThrowSmoke(pos, map, config.scale * 0.55f);
                FleckMaker.ThrowMicroSparks(pos, map);
            }
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
    /// 通用 Fleck 特效：用户选择任意 FleckDef，在指定位置生成。
    /// 支持 scale / rotation / 颜色 / 速度等参数。
    /// </summary>
    public class VisualEffectWorker_Fleck : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            Map map = caster.Map;
            if (map == null) return;

            FleckDef? fleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(config.fleckDefName);
            if (fleckDef == null)
            {
                Log.Warning($"[CharacterStudio] FleckDef '{config.fleckDefName}' 未找到，跳止播放。");
                return;
            }

            foreach (Vector3 pos in ResolvePositions(config, target, caster))
            {
                FleckCreationData data = FleckMaker.GetDataStatic(pos, map, fleckDef, config.scale);
                data.rotation = config.rotation;
                if (config.textureScale != Vector2.one)
                {
                    data.exactScale = new Vector3(config.textureScale.x, 1f, config.textureScale.y);
                }
                map.flecks.CreateFleck(data);
            }
        }
    }

    /// <summary>
    /// Fleck 连接线特效：在施法者与目标之间画一条 Fleck 连接线。
    /// 复用原版 FleckMaker.ConnectingLine。
    /// </summary>
    public class VisualEffectWorker_FleckConnectingLine : VisualEffectWorker
    {
        public override void Play(AbilityVisualEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            Map map = caster.Map;
            if (map == null) return;

            FleckDef? fleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(config.fleckDefName);
            if (fleckDef == null)
            {
                Log.Warning($"[CharacterStudio] FleckDef '{config.fleckDefName}' 未找到，跳止播放。");
                return;
            }

            Vector3 start = caster.DrawPos;
            Vector3 end = target.CenterVector3;

            float width = Mathf.Max(0.05f, config.lineWidth) * Mathf.Max(0.1f, config.scale);
            FleckMaker.ConnectingLine(start, end, fleckDef, map, width);
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
            { AbilityVisualEffectType.SteamBurst,      typeof(VisualEffectWorker_SteamBurst) },
            { AbilityVisualEffectType.EmberBurst,      typeof(VisualEffectWorker_EmberBurst) },
            { AbilityVisualEffectType.ShockBurst,      typeof(VisualEffectWorker_ShockBurst) },
            { AbilityVisualEffectType.DustRing,        typeof(VisualEffectWorker_DustRing) },
            { AbilityVisualEffectType.ArcSparkBurst,   typeof(VisualEffectWorker_ArcSparkBurst) },
            { AbilityVisualEffectType.FlameSurge,      typeof(VisualEffectWorker_FlameSurge) },
            { (AbilityVisualEffectType)1001, typeof(VisualEffectWorker_FlightJetBlue) },
            { (AbilityVisualEffectType)1002, typeof(VisualEffectWorker_FlightJetTrail) },
            { AbilityVisualEffectType.CustomMesh, typeof(VisualEffectWorker_CustomMesh) },
            { AbilityVisualEffectType.Fleck, typeof(VisualEffectWorker_Fleck) },
            { AbilityVisualEffectType.FleckConnectingLine, typeof(VisualEffectWorker_FleckConnectingLine) },
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
            { "SteamBurst", AbilityVisualEffectType.SteamBurst },
            { "EmberBurst", AbilityVisualEffectType.EmberBurst },
            { "ShockBurst", AbilityVisualEffectType.ShockBurst },
            { "DustRing", AbilityVisualEffectType.DustRing },
            { "ArcSparkBurst", AbilityVisualEffectType.ArcSparkBurst },
            { "FlameSurge", AbilityVisualEffectType.FlameSurge },
            { "FlightJetBlue", (AbilityVisualEffectType)1001 },
            { "FlightJetTrail", (AbilityVisualEffectType)1002 },
            { "CustomMesh", AbilityVisualEffectType.CustomMesh },
            { "LightningBolt", AbilityVisualEffectType.CustomMesh },
            { "Ring", AbilityVisualEffectType.CustomMesh },
            { "Spiral", AbilityVisualEffectType.CustomMesh },
            { "Beam", AbilityVisualEffectType.CustomMesh },
            { "Fleck", AbilityVisualEffectType.Fleck },
            { "FleckConnectingLine", AbilityVisualEffectType.FleckConnectingLine },
            { "ConnectingLine", AbilityVisualEffectType.FleckConnectingLine },
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
