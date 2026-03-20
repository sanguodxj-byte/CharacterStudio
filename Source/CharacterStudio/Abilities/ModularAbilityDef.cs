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
        public AbilityTargetType targetType = AbilityTargetType.Self;
        public bool useRadius = false;
        public AbilityAreaCenter areaCenter = AbilityAreaCenter.Target;
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
                targetType = this.targetType,
                useRadius = this.useRadius,
                areaCenter = this.areaCenter,
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
        QComboWindow,      // 施放后开启连段窗口（如 W 连段）
        EShortJump,        // 热键触发位移到目标落点
        RStackDetonation   // 两段：叠层-选点-延时爆发
    }

    public class AbilityRuntimeComponentConfig
    {
        public AbilityRuntimeComponentType type;
        public bool enabled = true;

        // 连段窗口组件
        public int comboWindowTicks = 12;

        // 位移组件
        public int cooldownTicks = 120;
        public int jumpDistance = 6;
        public int findCellRadius = 3;
        public bool triggerAbilityEffectsAfterJump = true;

        // 叠层引爆组件
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
        Self,       // 自身施法
        Target,     // 直接作用到目标
        Projectile, // 投射物命中目标

        // 兼容旧存档/旧 XML 的遗留值
        Touch,
        Area
    }

    public enum AbilityTargetType
    {
        Self,
        Entity,
        Cell
    }

    public enum AbilityAreaCenter
    {
        Self,
        Target
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
    /// 视觉特效来源模式
    /// </summary>
    public enum AbilityVisualEffectSourceMode
    {
        BuiltIn,    // 当前内建 Fleck / 组合效果
        Preset      // CharacterStudio 原生特效预设（第一阶段先保留导出/配置骨架）
    }

    /// <summary>
    /// 视觉特效触发时机
    /// 第一阶段运行时先完整支持 OnTargetApply / OnCastFinish，并将其余时机安全回退为当前 Apply 时机。
    /// </summary>
    public enum AbilityVisualEffectTrigger
    {
        OnCastStart,
        OnWarmup,
        OnCastFinish,
        OnTargetApply,
        OnDurationTick,
        OnExpire
    }

    /// <summary>
    /// 单个视觉特效配置
    /// </summary>
    public class AbilityVisualEffectConfig
    {
        /// <summary>
        /// 旧内建特效类型；为兼容现有数据和运行时逻辑保留。
        /// </summary>
        public AbilityVisualEffectType type = AbilityVisualEffectType.DustPuff;

        /// <summary>
        /// 新来源模式。默认 BuiltIn 以兼容旧数据。
        /// </summary>
        public AbilityVisualEffectSourceMode sourceMode = AbilityVisualEffectSourceMode.BuiltIn;

        /// <summary>
        /// CharacterStudio 原生预设名。仅在 sourceMode=Preset 时使用。
        /// </summary>
        public string presetDefName = string.Empty;

        public VisualEffectTarget target = VisualEffectTarget.Target;

        /// <summary>
        /// 触发时机。
        /// </summary>
        public AbilityVisualEffectTrigger trigger = AbilityVisualEffectTrigger.OnTargetApply;

        /// <summary>在效果执行后延迟多少 ticks 再播放特效（0 = 立即）</summary>
        public int delayTicks = 0;

        /// <summary>特效规模缩放（1.0 = 正常）</summary>
        public float scale = 1.0f;

        /// <summary>重复次数（包含首次播放）；最小为 1。</summary>
        public int repeatCount = 1;

        /// <summary>重复播放间隔 tick。</summary>
        public int repeatIntervalTicks = 0;

        /// <summary>附加偏移，供后续预设/运行时扩展使用。</summary>
        public Vector3 offset = Vector3.zero;

        /// <summary>是否附着到 Pawn（供后续预设播放扩展使用）。</summary>
        public bool attachToPawn = false;

        /// <summary>是否附着到目标格（供后续预设播放扩展使用）。</summary>
        public bool attachToTargetCell = false;

        /// <summary>是否启用此特效条目。</summary>
        public bool enabled = true;

        public AbilityVisualEffectConfig Clone()
        {
            return (AbilityVisualEffectConfig)MemberwiseClone();
        }
    }

    /// <summary>
    /// 单个效果配置
    /// </summary>
    public enum ControlEffectMode
    {
        Stun,
        Knockback,
        Pull
    }

    public enum TerraformEffectMode
    {
        CleanFilth,
        SpawnThing,
        ReplaceTerrain
    }

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
        public FactionDef? summonFactionDef;

        public ControlEffectMode controlMode = ControlEffectMode.Stun;
        public int controlMoveDistance = 3;

        public TerraformEffectMode terraformMode = TerraformEffectMode.CleanFilth;
        public ThingDef? terraformThingDef;
        public TerrainDef? terraformTerrainDef;
        public int terraformSpawnCount = 1;

        /// <summary>是否可以伤害施法者自身（默认 false = 不伤害自己）</summary>
        public bool canHurtSelf = false;

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

                case AbilityEffectType.Control:
                    if (controlMode != ControlEffectMode.Stun && controlMoveDistance <= 0)
                    {
                        result.AddError("CS_Ability_Validate_ControlMoveDistance".Translate());
                    }
                    if (duration < 0f)
                    {
                        result.AddError("CS_Ability_Validate_ControlDuration".Translate());
                    }
                    break;

                case AbilityEffectType.Terraform:
                    switch (terraformMode)
                    {
                        case TerraformEffectMode.SpawnThing:
                            if (terraformThingDef == null)
                            {
                                result.AddError("CS_Ability_Validate_TerraformThingRequired".Translate());
                            }
                            if (terraformSpawnCount <= 0)
                            {
                                result.AddError("CS_Ability_Validate_TerraformSpawnCount".Translate());
                            }
                            break;
                        case TerraformEffectMode.ReplaceTerrain:
                            if (terraformTerrainDef == null)
                            {
                                result.AddError("CS_Ability_Validate_TerraformTerrainRequired".Translate());
                            }
                            break;
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

            AbilityCarrierType normalizedCarrier = NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = NormalizeTargetType(ability);

            if (CarrierNeedsRange(normalizedCarrier, normalizedTarget) && ability.range <= 0)
            {
                result.AddWarning("CS_Ability_Validate_CarrierNeedsRange".Translate(GetCarrierTypeLabel(normalizedCarrier)));
            }

            if (ability.useRadius && ability.radius <= 0)
            {
                result.AddError("CS_Ability_Validate_AreaRadiusPositive".Translate());
            }

            if (normalizedCarrier == AbilityCarrierType.Projectile && ability.projectileDef == null)
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

        public static AbilityCarrierType NormalizeCarrierType(AbilityCarrierType type)
        {
            return type switch
            {
                AbilityCarrierType.Touch => AbilityCarrierType.Target,
                AbilityCarrierType.Area => AbilityCarrierType.Target,
                _ => type
            };
        }

        public static AbilityTargetType NormalizeTargetType(ModularAbilityDef ability)
        {
            if (ability == null)
            {
                return AbilityTargetType.Self;
            }

            if (ability.targetType != AbilityTargetType.Self || ability.useRadius || ability.projectileDef != null)
            {
                return ability.targetType;
            }

            return ability.carrierType switch
            {
                AbilityCarrierType.Self => AbilityTargetType.Self,
                AbilityCarrierType.Area => AbilityTargetType.Cell,
                AbilityCarrierType.Projectile => AbilityTargetType.Entity,
                AbilityCarrierType.Touch => AbilityTargetType.Entity,
                AbilityCarrierType.Target => AbilityTargetType.Entity,
                _ => AbilityTargetType.Entity
            };
        }

        public static AbilityAreaCenter NormalizeAreaCenter(ModularAbilityDef ability)
        {
            if (ability == null)
            {
                return AbilityAreaCenter.Target;
            }

            if (ability.areaCenter != AbilityAreaCenter.Target || ability.targetType != AbilityTargetType.Self)
            {
                return ability.areaCenter;
            }

            return ability.carrierType == AbilityCarrierType.Area
                ? AbilityAreaCenter.Target
                : AbilityAreaCenter.Self;
        }

        public static bool CarrierNeedsRange(AbilityCarrierType carrierType, AbilityTargetType targetType)
        {
            carrierType = NormalizeCarrierType(carrierType);
            return carrierType == AbilityCarrierType.Projectile
                || carrierType == AbilityCarrierType.Target && targetType != AbilityTargetType.Self;
        }

        private static string GetCarrierTypeLabel(AbilityCarrierType type)
        {
            return ($"CS_Ability_CarrierType_{NormalizeCarrierType(type)}").Translate();
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