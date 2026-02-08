using System;
using System.Collections.Generic;
using RimWorld;
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
            if (target.Thing is Pawn pawn && config.hediffDef != null)
            {
                pawn.health.AddHediff(config.hediffDef);
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
                Pawn pawn = PawnGenerator.GeneratePawn(config.summonKind, Faction.OfPlayer);
                GenSpawn.Spawn(pawn, loc, map, WipeMode.Vanish);
            }
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

            // 应用眩晕效果
            if (config.duration > 0)
            {
                var stunHediff = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, targetPawn);
                int durationTicks = (int)(config.duration * 60f); // 转换为tick
                stunHediff.TryGetComp<HediffComp_Disappears>()?.SetDuration(durationTicks);
                targetPawn.health.AddHediff(stunHediff);
            }
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

            // 清除污秽物
            var filthList = cell.GetThingList(map).FindAll(t => t is Filth);
            foreach (var filth in filthList)
            {
                filth.Destroy();
            }

            // 可选：生成掩体（如果配置中指定）
            // 这里仅作为框架，实际实现需要更多配置参数
        }
    }

    /// <summary>
    /// 工厂类 - 使用类型映射而非单例实例
    /// </summary>
    public static class EffectWorkerFactory
    {
        // 类型映射，每次调用时创建新实例，避免状态共享问题
        private static readonly Dictionary<AbilityEffectType, Type> workerTypes = new Dictionary<AbilityEffectType, Type>
        {
            { AbilityEffectType.Damage, typeof(EffectWorker_Damage) },
            { AbilityEffectType.Heal, typeof(EffectWorker_Heal) },
            { AbilityEffectType.Buff, typeof(EffectWorker_Status) },
            { AbilityEffectType.Debuff, typeof(EffectWorker_Status) },
            { AbilityEffectType.Summon, typeof(EffectWorker_Summon) },
            { AbilityEffectType.Teleport, typeof(EffectWorker_Teleport) },
            { AbilityEffectType.Control, typeof(EffectWorker_Control) },
            { AbilityEffectType.Terraform, typeof(EffectWorker_Terraform) }
        };

        /// <summary>
        /// 获取效果工作器（每次调用创建新实例）
        /// </summary>
        public static EffectWorker GetWorker(AbilityEffectType type)
        {
            if (workerTypes.TryGetValue(type, out var workerType))
            {
                return (EffectWorker)Activator.CreateInstance(workerType);
            }
            
            Log.Warning($"[CharacterStudio] [EffectWorkerFactory] 未知的效果类型: {type}, 使用默认伤害效果");
            return new EffectWorker_Damage();
        }

        /// <summary>
        /// 注册自定义效果工作器类型
        /// </summary>
        public static void RegisterWorkerType(AbilityEffectType type, Type workerType)
        {
            if (!typeof(EffectWorker).IsAssignableFrom(workerType))
            {
                Log.Error($"[CharacterStudio] [EffectWorkerFactory] 类型 {workerType.Name} 不是 EffectWorker 的子类");
                return;
            }
            
            workerTypes[type] = workerType;
        }
    }
}