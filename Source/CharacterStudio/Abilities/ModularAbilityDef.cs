using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 能力验证结果
    /// </summary>
    public class AbilityValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (!IsValid)
            {
                sb.AppendLine("验证失败:");
                foreach (var error in Errors)
                {
                    sb.AppendLine($"  ✗ {error}");
                }
            }
            if (Warnings.Count > 0)
            {
                sb.AppendLine("警告:");
                foreach (var warning in Warnings)
                {
                    sb.AppendLine($"  ⚠ {warning}");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 模块化能力配置
    /// 用于编辑器内存存储，最终会导出为原版 AbilityDef
    /// </summary>
    public class ModularAbilityDef : Def
    {
        // ─────────────────────────────────────────────
        // 基础属性
        // ─────────────────────────────────────────────
        public string iconPath = "";
        public float cooldownTicks = 600f;
        public float warmupTicks = 60f;
        public int charges = 1;
        public float aiCanUse = 1f; // 0=false, 1=true

        // ─────────────────────────────────────────────
        // 载体 (Carrier) - X轴
        // 决定技能如何触达目标
        // ─────────────────────────────────────────────
        public AbilityCarrierType carrierType = AbilityCarrierType.Self;
        public float range = 20f;
        public float radius = 0f; // 范围半径（用于爆炸/光环）
        public ThingDef? projectileDef; // 投射物定义（如果是 Projectile）

        // ─────────────────────────────────────────────
        // 效果 (Effects) - Y轴
        // 技能产生的实际影响
        // ─────────────────────────────────────────────
        public List<AbilityEffectConfig> effects = new List<AbilityEffectConfig>();

        // ─────────────────────────────────────────────
        // 运行时方法
        // ─────────────────────────────────────────────
        public ModularAbilityDef Clone()
        {
            var clone = new ModularAbilityDef
            {
                defName = this.defName + "_Copy",
                label = this.label + " (Copy)",
                description = this.description,
                iconPath = this.iconPath,
                cooldownTicks = this.cooldownTicks,
                warmupTicks = this.warmupTicks,
                charges = this.charges,
                aiCanUse = this.aiCanUse,
                carrierType = this.carrierType,
                range = this.range,
                radius = this.radius,
                projectileDef = this.projectileDef
            };

            foreach (var effect in this.effects)
            {
                clone.effects.Add(effect.Clone());
            }

            return clone;
        }
    }

    /// <summary>
    /// 技能载体类型
    /// </summary>
    public enum AbilityCarrierType
    {
        Self,       // 自身施法（强化/治疗自身）
        Touch,      // 接触施法（近战/治疗他人）
        Target,     // 指定目标（瞬发/引导）
        Projectile, // 投射物（火球/箭矢）
        Area        // 范围作用（以自身为中心或目标点）
    }

    /// <summary>
    /// 技能效果类型
    /// </summary>
    public enum AbilityEffectType
    {
        Damage,     // 伤害
        Heal,       // 治疗
        Buff,       // 强化（Hediff）
        Debuff,     // 弱化（Hediff）
        Summon,     // 召唤
        Teleport,   // 位移
        Control,    // 控制（击晕/强制移动）
        Terraform   // 地形改变（生成掩体/清除污秽）
    }

    /// <summary>
    /// 单个效果配置
    /// </summary>
    public class AbilityEffectConfig
    {
        public AbilityEffectType type;
        
        // 数值参数
        public float amount = 0f; // 伤害量/治疗量
        public float duration = 0f; // 持续时间（秒）
        public float chance = 1f; // 触发几率

        // 引用参数
        public DamageDef? damageDef;
        public HediffDef? hediffDef;
        public PawnKindDef? summonKind;
        public int summonCount = 1;

        public AbilityEffectConfig Clone()
        {
            return (AbilityEffectConfig)MemberwiseClone();
        }

        /// <summary>
        /// 验证效果配置
        /// </summary>
        public AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();

            // 检查数值范围
            if (amount < 0)
            {
                result.AddWarning("效果数值为负数，可能导致意外行为");
            }

            if (chance <= 0 || chance > 1)
            {
                result.AddError($"触发几率必须在 0-1 之间，当前值: {chance}");
            }

            // 检查效果类型特定要求
            switch (type)
            {
                case AbilityEffectType.Damage:
                    if (amount <= 0)
                    {
                        result.AddError("伤害效果的数值必须大于 0");
                    }
                    break;

                case AbilityEffectType.Buff:
                case AbilityEffectType.Debuff:
                    if (hediffDef == null)
                    {
                        result.AddError($"{type} 效果必须指定 HediffDef");
                    }
                    break;

                case AbilityEffectType.Summon:
                    if (summonKind == null)
                    {
                        result.AddError("召唤效果必须指定 PawnKindDef");
                    }
                    if (summonCount <= 0)
                    {
                        result.AddError("召唤数量必须大于 0");
                    }
                    break;
            }

            return result;
        }
    }

    /// <summary>
    /// ModularAbilityDef 扩展方法
    /// </summary>
    public static class ModularAbilityDefExtensions
    {
        /// <summary>
        /// 验证能力定义
        /// </summary>
        public static AbilityValidationResult Validate(this ModularAbilityDef ability)
        {
            var result = new AbilityValidationResult();

            // 基础验证
            if (string.IsNullOrEmpty(ability.defName))
            {
                result.AddError("defName 不能为空");
            }
            else if (!IsValidDefName(ability.defName))
            {
                result.AddError("defName 只能包含字母、数字和下划线");
            }

            if (string.IsNullOrEmpty(ability.label))
            {
                result.AddWarning("label 为空，将使用 defName 作为显示名称");
            }

            // 数值验证
            if (ability.cooldownTicks < 0)
            {
                result.AddError("冷却时间不能为负数");
            }

            if (ability.warmupTicks < 0)
            {
                result.AddError("热身时间不能为负数");
            }

            if (ability.charges <= 0)
            {
                result.AddWarning("能力使用次数为 0 或负数");
            }

            if (ability.range < 0)
            {
                result.AddError("射程不能为负数");
            }

            if (ability.radius < 0)
            {
                result.AddError("作用半径不能为负数");
            }

            // 载体类型验证
            switch (ability.carrierType)
            {
                case AbilityCarrierType.Target:
                case AbilityCarrierType.Projectile:
                case AbilityCarrierType.Area:
                    if (ability.range <= 0)
                    {
                        result.AddWarning($"载体类型 {ability.carrierType} 通常需要设置射程");
                    }
                    break;
            }

            if (ability.carrierType == AbilityCarrierType.Projectile && ability.projectileDef == null)
            {
                result.AddWarning("投射物载体未指定 projectileDef，可能使用默认投射物");
            }

            // 效果验证
            if (ability.effects == null || ability.effects.Count == 0)
            {
                result.AddWarning("能力没有定义任何效果");
            }
            else
            {
                for (int i = 0; i < ability.effects.Count; i++)
                {
                    var effectResult = ability.effects[i].Validate();
                    if (!effectResult.IsValid)
                    {
                        foreach (var error in effectResult.Errors)
                        {
                            result.AddError($"效果 #{i + 1}: {error}");
                        }
                    }
                    foreach (var warning in effectResult.Warnings)
                    {
                        result.AddWarning($"效果 #{i + 1}: {warning}");
                    }
                }
            }

            return result;
        }

        private static bool IsValidDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            foreach (char c in defName)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    return false;
                }
            }
            return true;
        }
    }
}