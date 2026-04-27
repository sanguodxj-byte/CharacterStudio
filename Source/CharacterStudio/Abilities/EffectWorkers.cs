using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 效果执行器基类
    /// </summary>
    public abstract class EffectWorker
    {
        public abstract void Apply(AbilityEffectConfig config, LocalTargetInfo target, Pawn caster);
    }

    /// <summary>
    /// 伤害效果
    /// </summary>
    public class EffectWorker_Damage : EffectWorker
    {
        public override void Apply(AbilityEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            if (!target.HasThing) return;
            if (target.Thing == caster && !config.canHurtSelf) return;

            float amount = config.amount;
            DamageDef def = config.damageDef ?? DamageDefOf.Blunt;
            
            DamageInfo dinfo = new DamageInfo(def, amount, 0f, -1f, caster);
            target.Thing.TakeDamage(dinfo);
        }
    }

    /// <summary>
    /// 治疗效果
    /// </summary>
    public class EffectWorker_Heal : EffectWorker
    {
        public override void Apply(AbilityEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            if (target.Thing is Pawn pawn)
            {
                // 简单的全身治疗逻辑，实际可扩展为治疗特定部位
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                for (int i = hediffs.Count - 1; i >= 0; i--)
                {
                    if (hediffs[i] is Hediff_Injury injury)
                    {
                        float healAmount = config.amount;
                        injury.Heal(healAmount);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 状态效果 (Buff/Debuff)
    /// </summary>
    public class EffectWorker_Status : EffectWorker
    {
        public override void Apply(AbilityEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            if (target.Thing is not Pawn pawn || config.hediffDef == null) return;

            var hediff = pawn.health.AddHediff(config.hediffDef);

            // 将编辑器中设置的 duration（秒）应用到 HediffComp_Disappears
            // config.duration <= 0 表示永久效果，由 HediffDef 自身控制
            if (config.duration > 0f && hediff != null)
            {
                var comp = hediff.TryGetComp<HediffComp_Disappears>();
                if (comp != null)
                {
                    int durationTicks = (int)(config.duration * 60f);
                    comp.ticksToDisappear = durationTicks;
                }
            }
        }
    }

    /// <summary>
    /// 召唤效果
    /// </summary>
    public class EffectWorker_Summon : EffectWorker
    {
        public override void Apply(AbilityEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            if (config.summonKind == null) return;

            IntVec3 loc = target.Cell;
            Map map = caster.Map;

            for (int i = 0; i < config.summonCount; i++)
            {
                Faction? faction = ResolveSummonFaction(config, caster);
                faction ??= Faction.OfPlayer;
                Pawn pawn = PawnGenerator.GeneratePawn(config.summonKind, faction);
                GenSpawn.Spawn(pawn, loc, map, WipeMode.Vanish);
            }
        }

        private Faction? ResolveSummonFaction(AbilityEffectConfig config, Pawn caster)
        {
            switch (config.summonFactionType)
            {
                case SummonFactionType.Caster:
                    return caster.Faction;
                case SummonFactionType.Hostile:
                    return Faction.OfAncientsHostile ?? Find.FactionManager?.RandomEnemyFaction();
                case SummonFactionType.Neutral:
                    return Find.FactionManager?.RandomNonHostileFaction(false, false, true);
                case SummonFactionType.FixedDef:
                    // 优先从 summonFactionDefName 查找（持久化更可靠）
                    if (!string.IsNullOrEmpty(config.summonFactionDefName))
                    {
                        FactionDef? def = DefDatabase<FactionDef>.GetNamedSilentFail(config.summonFactionDefName);
                        if (def != null) return Find.FactionManager?.FirstFactionOfDef(def);
                    }
                    // 回退到 Def 对象
                    if (config.summonFactionDef != null)
                        return Find.FactionManager?.FirstFactionOfDef(config.summonFactionDef);
                    break;
                default:
                    return Faction.OfPlayer;
            }
            return Faction.OfPlayer;
        }
    }

    /// <summary>
    /// 传送效果
    /// </summary>
    public class EffectWorker_Teleport : EffectWorker
    {
        public override void Apply(AbilityEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            if (!target.IsValid || caster?.Map == null) return;

            IntVec3 destCell = target.Cell;
            
            // 确保目标位置可以站立
            if (!destCell.Standable(caster.Map))
            {
                // 尝试在附近找到可站立的位置
                if (!CellFinder.TryFindRandomCellNear(destCell, caster.Map, 3,
                    (IntVec3 c) => c.Standable(caster.Map), out destCell))
                {
                    return; // 找不到有效位置
                }
            }

            // 执行传送
            caster.Position = destCell;
            caster.Notify_Teleported(true, true);
            
            // 可选：添加传送特效
            FleckMaker.ThrowDustPuff(destCell, caster.Map, 1f);
        }
    }

    /// <summary>
    /// 控制效果（击晕/强制移动）
    /// </summary>
    public class EffectWorker_Control : EffectWorker
    {
        public override void Apply(AbilityEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            if (target.Thing is not Pawn targetPawn) return;
            if (targetPawn == caster) return;

            switch (config.controlMode)
            {
                case ControlEffectMode.Knockback:
                    ApplyForcedMove(targetPawn, caster, config.controlMoveDistance, pushAway: true);
                    break;
                case ControlEffectMode.Pull:
                    ApplyForcedMove(targetPawn, caster, config.controlMoveDistance, pushAway: false);
                    break;
                default:
                    ApplyStun(targetPawn, caster, config.duration);
                    break;
            }
        }

        private void ApplyStun(Pawn targetPawn, Pawn caster, float durationSeconds)
        {
            if (durationSeconds <= 0f) return;

            int durationTicks = (int)(durationSeconds * 60f);
            if (targetPawn.stances != null && targetPawn.stances.stunner != null)
            {
                targetPawn.stances.stunner.StunFor(durationTicks, caster, false);
            }
        }

        private void ApplyForcedMove(Pawn targetPawn, Pawn caster, int distance, bool pushAway)
        {
            if (caster.Map == null || distance <= 0) return;

            IntVec3 direction = ResolveForcedMoveDirection(targetPawn.Position, caster.Position, caster.Rotation.FacingCell);

            if (!pushAway)
                direction = new IntVec3(-direction.x, 0, -direction.z);

            if (direction == IntVec3.Zero)
                return;

            CompCharacterAbilityRuntime? abilityComp = targetPawn.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp != null)
            {
                Vector2 dir = new Vector2(direction.x, direction.z).normalized;
                abilityComp.BeginForcedMove(dir, distance, distance * 4);
                return;
            }

            IntVec3 bestCell = targetPawn.Position;
            for (int i = 0; i < distance; i++)
            {
                IntVec3 next = bestCell + direction;
                if (!next.InBounds(caster.Map) || !next.Standable(caster.Map))
                    break;
                bestCell = next;
            }

            if (bestCell != targetPawn.Position)
            {
                targetPawn.Position = bestCell;
                targetPawn.Notify_Teleported(true, false);
            }
        }

        internal static IntVec3 ResolveForcedMoveDirection(IntVec3 targetCell, IntVec3 sourceCell, IntVec3 fallbackFacing)
        {
            IntVec3 delta = targetCell - sourceCell;
            if (delta == IntVec3.Zero)
            {
                delta = fallbackFacing;
            }

            int absX = Math.Abs(delta.x);
            int absZ = Math.Abs(delta.z);
            if (absX == 0 && absZ == 0) return IntVec3.Zero;

            if (absX > absZ) return new IntVec3(Math.Sign(delta.x), 0, 0);
            if (absZ > absX) return new IntVec3(0, 0, Math.Sign(delta.z));
            return new IntVec3(Math.Sign(delta.x), 0, Math.Sign(delta.z));
        }
    }

    /// <summary>
    /// 地形改变效果
    /// </summary>
    public class EffectWorker_Terraform : EffectWorker
    {
        public override void Apply(AbilityEffectConfig config, LocalTargetInfo target, Pawn caster)
        {
            if (!target.IsValid || caster?.Map == null) return;

            IntVec3 cell = target.Cell;
            Map map = caster.Map;
            if (!cell.InBounds(map)) return;

            switch (config.terraformMode)
            {
                case TerraformEffectMode.SpawnThing:
                    SpawnThing(config, cell, map);
                    break;
                case TerraformEffectMode.ReplaceTerrain:
                    ReplaceTerrain(config, cell, map);
                    break;
                default:
                    CleanFilth(cell, map);
                    break;
            }
        }

        private static void CleanFilth(IntVec3 cell, Map map)
        {
            var filthList = cell.GetThingList(map).FindAll(t => t is Filth);
            foreach (var filth in filthList)
            {
                filth.Destroy();
            }
        }

        private static void SpawnThing(AbilityEffectConfig config, IntVec3 cell, Map map)
        {
            ThingDef? thingDef = config.terraformThingDef;
            if (thingDef == null) return;

            int count = Math.Max(1, config.terraformSpawnCount);
            for (int i = 0; i < count; i++)
            {
                Thing thing = ThingMaker.MakeThing(thingDef);
                if (thing.stackCount > 1) thing.stackCount = 1;
                GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
            }
        }

        private static void ReplaceTerrain(AbilityEffectConfig config, IntVec3 cell, Map map)
        {
            TerrainDef? terrainDef = config.terraformTerrainDef;
            if (terrainDef == null) return;
            map.terrainGrid.SetTerrain(cell, terrainDef);
        }
    }

    public class EffectWorker_WeatherChange : EffectWorker
    {
        public override void Apply(AbilityEffectConfig effectConfig, LocalTargetInfo target, Pawn caster)
        {
            if (caster.Map == null || effectConfig == null) return;

            if (string.IsNullOrWhiteSpace(effectConfig.weatherDefName))
            {
                Log.Warning("[CharacterStudio] Weather change failed: weatherDefName is empty.");
                return;
            }

            WeatherDef? weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(effectConfig.weatherDefName.Trim());
            if (weatherDef == null)
            {
                Log.Warning($"[CharacterStudio] Ability weather change failed: WeatherDef '{effectConfig.weatherDefName}' not found.");
                return;
            }

            Map map = caster.Map;
            if (map.weatherManager == null) return;

            Log.Message($"[CharacterStudio] Force changing weather to '{weatherDef.defName}' on map '{map.uniqueID}'");

            var traverse = HarmonyLib.Traverse.Create(map.weatherManager);
            traverse.Field("lastWeather").SetValue(weatherDef);
            traverse.Field("curWeather").SetValue(weatherDef);
            traverse.Field("curWeatherDuration").SetValue(effectConfig.weatherDurationTicks);
            traverse.Field("transitionEndTick").SetValue(Find.TickManager.TicksGame);
            traverse.Method("InternalTick").GetValue();

            Messages.Message("CS_Ability_WeatherChanged".Translate(weatherDef.LabelCap), MessageTypeDefOf.PositiveEvent, false);
        }
    }

    /// <summary>
    /// 工厂类 - 使用单例缓存
    /// </summary>
    public static class EffectWorkerFactory
    {
        private static readonly Dictionary<AbilityEffectType, EffectWorker> workerInstances = new Dictionary<AbilityEffectType, EffectWorker>
        {
            { AbilityEffectType.Damage, new EffectWorker_Damage() },
            { AbilityEffectType.Heal, new EffectWorker_Heal() },
            { AbilityEffectType.Buff, new EffectWorker_Status() },
            { AbilityEffectType.Debuff, new EffectWorker_Status() },
            { AbilityEffectType.Summon, new EffectWorker_Summon() },
            { AbilityEffectType.Teleport, new EffectWorker_Teleport() },
            { AbilityEffectType.Control, new EffectWorker_Control() },
            { AbilityEffectType.Terraform, new EffectWorker_Terraform() },
            { AbilityEffectType.WeatherChange, new EffectWorker_WeatherChange() }
        };

        public static EffectWorker GetWorker(AbilityEffectType type)
        {
            if (workerInstances.TryGetValue(type, out var worker)) return worker;
            Log.Warning($"[CharacterStudio] [EffectWorkerFactory] 未知的效果类型: {type}, 使用默认伤害效果");
            return workerInstances[AbilityEffectType.Damage];
        }

        public static void RegisterWorkerType(AbilityEffectType type, Type workerType)
        {
            if (!typeof(EffectWorker).IsAssignableFrom(workerType))
            {
                Log.Error($"[CharacterStudio] [EffectWorkerFactory] 类型 {workerType.Name} 不是 EffectWorker 的子类");
                return;
            }
            workerInstances[type] = (EffectWorker)Activator.CreateInstance(workerType);
        }
    }
}
