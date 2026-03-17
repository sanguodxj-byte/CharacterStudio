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
                sb.AppendLine("CS_Ability_Validate_Failed".Translate());
                foreach (var error in Errors)
                {
                    sb.AppendLine($"  ✗ {error}");
                }
            }
            if (Warnings.Count > 0)
            {
                sb.AppendLine("CS_Ability_Validate_Warnings".Translate());
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
        // 视觉特效 (Visual Effects)
        // 技能触发时的外观粒子/闪光效果
        // ─────────────────────────────────────────────
        public List<AbilityVisualEffectConfig> visualEffects = new List<AbilityVisualEffectConfig>();

        // ─────────────────────────────────────────────
        // 运行时组件 (Runtime Components)
        // 用于组合复杂技能行为，让玩家通过编辑器配置
        // ─────────────────────────────────────────────
        public List<AbilityRuntimeComponentConfig> runtimeComponents = new List<AbilityRuntimeComponentConfig>();

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

            foreach (var component in this.runtimeComponents)
            {
                clone.runtimeComponents.Add(component.Clone());
            }

            foreach (var vfx in this.visualEffects)
            {
                clone.visualEffects.Add(vfx.Clone());
            }

            return clone;
        }
    }

    public enum AbilityRuntimeComponentType
    {
        QComboWindow,      // Q 施放后开启连段窗口（如 W 连段）
        EShortJump,        // E 热键短位移
        RStackDetonation   // R 两段：叠层-选点-延时爆发
    }

    public class AbilityRuntimeComponentConfig
    {
        public AbilityRuntimeComponentType type;
        public bool enabled = true;

        // Q 连段组件
        public int comboWindowTicks = 12;

        // E 短跳组件
        public int cooldownTicks = 120;
        public int jumpDistance = 6;
        public int findCellRadius = 3;
        public bool triggerAbilityEffectsAfterJump = true;

        // R 两段组件
        public int requiredStacks = 7;
        public int delayTicks = 180;
        public float wave1Radius = 3f;
        public float wave1Damage = 80f;
        public float wave2Radius = 6f;
        public float wave2Damage = 140f;
        public float wave3Radius = 9f;
        public float wave3Damage = 220f;
        public DamageDef? waveDamageDef;

        public AbilityRuntimeComponentConfig Clone()
        {
            return (AbilityRuntimeComponentConfig)MemberwiseClone();
        }

        public AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled)
            {
                return result;
            }

            switch (type)
            {
                case AbilityRuntimeComponentType.QComboWindow:
                    if (comboWindowTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_QComboWindowTicks".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.EShortJump:
                    if (cooldownTicks < 0)
                    {
                        result.AddError("CS_Ability_Validate_EShortJumpCooldown".Translate());
                    }
                    if (jumpDistance <= 0)
                    {
                        result.AddError("CS_Ability_Validate_EShortJumpDistance".Translate());
                    }
                    if (findCellRadius < 0)
                    {
                        result.AddError("CS_Ability_Validate_EShortJumpFindCellRadius".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.RStackDetonation:
                    if (requiredStacks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_RRequiredStacks".Translate());
                    }
                    if (delayTicks < 0)
                    {
                        result.AddError("CS_Ability_Validate_RDelayTicks".Translate());
                    }
                    if (wave1Radius <= 0 || wave2Radius <= 0 || wave3Radius <= 0)
                    {
                        result.AddError("CS_Ability_Validate_RWaveRadius".Translate());
                    }
                    if (wave1Damage <= 0 || wave2Damage <= 0 || wave3Damage <= 0)
                    {
                        result.AddError("CS_Ability_Validate_RWaveDamage".Translate());
                    }
                    break;
            }

            return result;
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
    /// 视觉特效类型（外观粒子/闪光）
    /// </summary>
    public enum AbilityVisualEffectType
    {
        DustPuff,       // 尘埃喷溅
        MicroSparks,    // 微型火花
        LightningGlow,  // 闪电光晕
        FireGlow,       // 火焰光晕
        Smoke,          // 烟雾
        ExplosionEffect // 爆炸闪光
    }

    /// <summary>
    /// 视觉特效作用目标
    /// </summary>
    public enum VisualEffectTarget
    {
        Caster,     // 施法者
        Target,     // 命中目标
        Both        // 双方
    }

    /// <summary>
    /// 单个视觉特效配置
    /// </summary>
    public class AbilityVisualEffectConfig
    {
        public AbilityVisualEffectType type = AbilityVisualEffectType.DustPuff;
        public VisualEffectTarget target = VisualEffectTarget.Target;
        /// <summary>在效果执行后延迟多少 ticks 再播放特效（0 = 立即）</summary>
        public int delayTicks = 0;
        /// <summary>特效规模缩放（1.0 = 正常）</summary>
        public float scale = 1.0f;

        public AbilityVisualEffectConfig Clone()
        {
            return (AbilityVisualEffectConfig)MemberwiseClone();
        }
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
                result.AddWarning("CS_Ability_Validate_EffectNegativeAmount".Translate());
            }

            if (chance <= 0 || chance > 1)
            {
                result.AddError("CS_Ability_Validate_EffectChanceRange".Translate(chance));
            }

            // 检查效果类型特定要求
            switch (type)
            {
                case AbilityEffectType.Damage:
                    if (amount <= 0)
                    {
                        result.AddError("CS_Ability_Validate_DamageAmount".Translate());
                    }
                    break;

                case AbilityEffectType.Buff:
                case AbilityEffectType.Debuff:
                    if (hediffDef == null)
                    {
                        result.AddError("CS_Ability_Validate_HediffRequired".Translate(($"CS_Ability_EffectType_{type}").Translate()));
                    }
                    break;

                case AbilityEffectType.Summon:
                    if (summonKind == null)
                    {
                        result.AddError("CS_Ability_Validate_SummonKindRequired".Translate());
                    }
                    if (summonCount <= 0)
                    {
                        result.AddError("CS_Ability_Validate_SummonCount".Translate());
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
                result.AddError("CS_Ability_Validate_DefNameEmpty".Translate());
            }
            else if (!IsValidDefName(ability.defName))
            {
                result.AddError("CS_Ability_Validate_DefNameInvalid".Translate());
            }

            if (string.IsNullOrEmpty(ability.label))
            {
                result.AddWarning("CS_Ability_Validate_LabelEmpty".Translate());
            }

            // 数值验证
            if (ability.cooldownTicks < 0)
            {
                result.AddError("CS_Ability_Validate_CooldownNegative".Translate());
            }

            if (ability.warmupTicks < 0)
            {
                result.AddError("CS_Ability_Validate_WarmupNegative".Translate());
            }

            if (ability.charges <= 0)
            {
                result.AddWarning("CS_Ability_Validate_ChargesNonPositive".Translate());
            }

            if (ability.range < 0)
            {
                result.AddError("CS_Ability_Validate_RangeNegative".Translate());
            }

            if (ability.radius < 0)
            {
                result.AddError("CS_Ability_Validate_RadiusNegative".Translate());
            }

            // 载体类型验证
            switch (ability.carrierType)
            {
                case AbilityCarrierType.Target:
                case AbilityCarrierType.Projectile:
                case AbilityCarrierType.Area:
                    if (ability.range <= 0)
                    {
                        result.AddWarning("CS_Ability_Validate_CarrierNeedsRange".Translate(GetCarrierTypeLabel(ability.carrierType)));
                    }
                    break;
            }

            if (ability.carrierType == AbilityCarrierType.Projectile && ability.projectileDef == null)
            {
                result.AddWarning("CS_Ability_Validate_ProjectileDefMissing".Translate());
            }

            // 效果验证
            if (ability.effects == null || ability.effects.Count == 0)
            {
                result.AddWarning("CS_Ability_Validate_NoEffects".Translate());
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
                            result.AddError("CS_Ability_Validate_EffectIndexError".Translate(i + 1, error));
                        }
                    }
                    foreach (var warning in effectResult.Warnings)
                    {
                        result.AddWarning("CS_Ability_Validate_EffectIndexWarning".Translate(i + 1, warning));
                    }
                }
            }

            if (ability.runtimeComponents != null)
            {
                for (int i = 0; i < ability.runtimeComponents.Count; i++)
                {
                    var component = ability.runtimeComponents[i];
                    if (component == null)
                    {
                        result.AddWarning("CS_Ability_Validate_RuntimeComponentNull".Translate(i + 1));
                        continue;
                    }

                    var compResult = component.Validate();
                    if (!compResult.IsValid)
                    {
                        foreach (var error in compResult.Errors)
                        {
                            result.AddError("CS_Ability_Validate_RuntimeComponentError".Translate(i + 1, error));
                        }
                    }

                    foreach (var warning in compResult.Warnings)
                    {
                        result.AddWarning("CS_Ability_Validate_RuntimeComponentWarning".Translate(i + 1, warning));
                    }
                }
            }

            return result;
        }

        private static string GetCarrierTypeLabel(AbilityCarrierType type)
        {
            return ($"CS_Ability_CarrierType_{type}").Translate();
        }

        private static string GetEffectTypeLabel(AbilityEffectType type)
        {
            return ($"CS_Ability_EffectType_{type}").Translate();
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