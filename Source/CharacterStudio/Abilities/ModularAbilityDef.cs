using System.Collections.Generic;
using System.Text;
using CharacterStudio.Core;
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
        public AbilityAreaShape areaShape = AbilityAreaShape.Circle;
        public string irregularAreaPattern = string.Empty;
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
                defName = this.defName,
                label = this.label,
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
                areaShape = this.areaShape,
                irregularAreaPattern = this.irregularAreaPattern,
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
        QComboWindow,          // 施放后开启连段窗口（如 W 连段）
        HotkeyOverride,        // 施放后临时覆盖指定热键槽位的技能
        FollowupCooldownGate,  // 施放后让后续热键覆盖技能延迟可用
        SmartJump,             // 通用智能施法跳跃/位移
        EShortJump,            // 兼容旧数据：热键触发位移到目标落点
        RStackDetonation,      // 两段：叠层-选点-延时爆发
        PeriodicPulse,         // 持续期间周期脉冲
        KillRefresh,           // 击杀后立即刷新技能冷却
        ShieldAbsorb,          // 护盾吸收转化为治疗/增伤
        ChainBounce,           // 命中后按最近目标弹射链
        ExecuteBonusDamage,    // 斩杀线以下目标受到额外伤害
        FullHealthBonusDamage, // 目标血量高于阈值时获得额外伤害
        MissingHealthBonusDamage, // 目标越残血伤害越高
        NearbyEnemyBonusDamage, // 目标周围敌人越多伤害越高
        IsolatedTargetBonusDamage, // 目标周围没有其他敌人时获得额外伤害
        MarkDetonation,        // 命中叠加标记并在达层时引爆
        ComboStacks,           // 连续命中叠层并提升后续收益
        HitSlowField,          // 命中后在目标点施加减速区
        PierceBonusDamage,     // 连续穿透目标时逐层增伤
        DashEmpoweredStrike,   // 位移后短时间强化下一次命中
        HitHeal,               // 命中后按伤害或固定值回复施法者
        HitCooldownRefund,     // 命中后返还指定槽位冷却
        ProjectileSplit,       // 命中后分裂为额外打击
        FlightState            // 进入持续飞行姿态
    }

    public enum AbilityRuntimeHotkeySlot
    {
        Q,
        W,
        E,
        R
    }

    public class AbilityRuntimeComponentConfig
    {
        public AbilityRuntimeComponentType type;
        public bool enabled = true;

        // 连段窗口组件
        public int comboWindowTicks = 12;

        // 热键覆盖组件
        public AbilityRuntimeHotkeySlot overrideHotkeySlot = AbilityRuntimeHotkeySlot.Q;
        public string overrideAbilityDefName = string.Empty;
        public int overrideDurationTicks = 60;

        // 后续技能延迟可用组件
        public AbilityRuntimeHotkeySlot followupCooldownHotkeySlot = AbilityRuntimeHotkeySlot.Q;
        public int followupCooldownTicks = 60;

        // 击杀刷新目标槽位
        public AbilityRuntimeHotkeySlot killRefreshHotkeySlot = AbilityRuntimeHotkeySlot.Q;

        // 位移/智能施法组件
        public int cooldownTicks = 120;
        public int jumpDistance = 6;
        public int findCellRadius = 3;
        public bool triggerAbilityEffectsAfterJump = true;
        public bool useMouseTargetCell = true;
        public int smartCastOffsetCells = 1;
        public bool smartCastClampToMaxDistance = true;
        public bool smartCastAllowFallbackForward = true;

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

        // 周期脉冲组件
        public int pulseIntervalTicks = 60;
        public int pulseTotalTicks = 240;
        public bool pulseStartsImmediately = true;

        // 击杀刷新组件
        public float killRefreshCooldownPercent = 1f;

        // 护盾吸收组件
        public float shieldMaxDamage = 120f;
        public float shieldDurationTicks = 240f;
        public float shieldHealRatio = 0.5f;
        public float shieldBonusDamageRatio = 0.25f;

        // 弹射链组件
        public int maxBounceCount = 4;
        public float bounceRange = 6f;
        public float bounceDamageFalloff = 0.2f;

        // 斩杀增伤组件
        public float executeThresholdPercent = 0.3f;
        public float executeBonusDamageScale = 0.5f;

        // 满血增伤组件
        public float fullHealthThresholdPercent = 0.95f;
        public float fullHealthBonusDamageScale = 0.35f;

        // 缺血增伤组件
        public float missingHealthBonusPerTenPercent = 0.05f;
        public float missingHealthBonusMaxScale = 0.5f;

        // 附近敌人增伤组件
        public int nearbyEnemyBonusMaxTargets = 4;
        public float nearbyEnemyBonusPerTarget = 0.08f;
        public float nearbyEnemyBonusRadius = 5f;

        // 孤立目标增伤组件
        public float isolatedTargetRadius = 4f;
        public float isolatedTargetBonusDamageScale = 0.35f;

        // 标记引爆组件
        public int markDurationTicks = 180;
        public int markMaxStacks = 3;
        public float markDetonationDamage = 40f;
        public DamageDef? markDamageDef;

        // 连击叠层组件
        public int comboStackWindowTicks = 180;
        public int comboStackMax = 5;
        public float comboStackBonusDamagePerStack = 0.06f;

        // 命中减速区组件
        public int slowFieldDurationTicks = 180;
        public float slowFieldRadius = 2.5f;
        public string slowFieldHediffDefName = string.Empty;

        // 穿透增伤组件
        public int pierceMaxTargets = 3;
        public float pierceBonusDamagePerTarget = 0.15f;
        public float pierceSearchRange = 5f;

        // 位移后强化普攻组件
        public int dashEmpowerDurationTicks = 180;
        public float dashEmpowerBonusDamageScale = 0.5f;

        // 命中回血组件
        public float hitHealAmount = 8f;
        public float hitHealRatio = 0f;

        // 击中返冷却组件
        public AbilityRuntimeHotkeySlot refundHotkeySlot = AbilityRuntimeHotkeySlot.Q;
        public float hitCooldownRefundPercent = 0.15f;

        // 弹道分裂组件
        public int splitProjectileCount = 2;
        public float splitDamageScale = 0.5f;
        public float splitSearchRange = 5f;

        // 飞行姿态组件
        public int flightDurationTicks = 180;
        public float flightHeightFactor = 0.35f;

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

                case AbilityRuntimeComponentType.HotkeyOverride:
                    if (string.IsNullOrWhiteSpace(overrideAbilityDefName))
                    {
                        result.AddError("CS_Ability_Validate_HotkeyOverrideAbilityDefName".Translate());
                    }
                    if (overrideDurationTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_HotkeyOverrideDurationTicks".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.FollowupCooldownGate:
                    if (followupCooldownTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_FollowupCooldownTicks".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.SmartJump:
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
                    if (type == AbilityRuntimeComponentType.SmartJump && smartCastOffsetCells <= 0)
                    {
                        result.AddError("CS_Ability_Validate_SmartJumpOffsetCells".Translate());
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

                case AbilityRuntimeComponentType.PeriodicPulse:
                    if (pulseIntervalTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_PeriodicPulseIntervalTicks".Translate());
                    }
                    if (pulseTotalTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_PeriodicPulseTotalTicks".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.KillRefresh:
                    if (killRefreshCooldownPercent <= 0f || killRefreshCooldownPercent > 1f)
                    {
                        result.AddError("CS_Ability_Validate_KillRefreshCooldownPercent".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.ShieldAbsorb:
                    if (shieldMaxDamage <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_ShieldMaxDamage".Translate());
                    }
                    if (shieldDurationTicks <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_ShieldDurationTicks".Translate());
                    }
                    if (shieldHealRatio < 0f || shieldBonusDamageRatio < 0f)
                    {
                        result.AddError("CS_Ability_Validate_ShieldRatios".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.ChainBounce:
                    if (maxBounceCount <= 0)
                    {
                        result.AddError("CS_Ability_Validate_ChainBounceCount".Translate());
                    }
                    if (bounceRange <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_ChainBounceRange".Translate());
                    }
                    if (bounceDamageFalloff < 0f || bounceDamageFalloff >= 1f)
                    {
                        result.AddError("CS_Ability_Validate_ChainBounceFalloff".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.ExecuteBonusDamage:
                    if (executeThresholdPercent <= 0f || executeThresholdPercent >= 1f)
                    {
                        result.AddError("CS_Ability_Validate_ExecuteThreshold".Translate());
                    }
                    if (executeBonusDamageScale <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_ExecuteBonusScale".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.FullHealthBonusDamage:
                    if (fullHealthThresholdPercent <= 0f || fullHealthThresholdPercent > 1f)
                    {
                        result.AddError("CS_Ability_Validate_FullHealthThreshold".Translate());
                    }
                    if (fullHealthBonusDamageScale <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_FullHealthBonusScale".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.MissingHealthBonusDamage:
                    if (missingHealthBonusPerTenPercent < 0f)
                    {
                        result.AddError("CS_Ability_Validate_MissingHealthPerTen".Translate());
                    }
                    if (missingHealthBonusMaxScale < 0f)
                    {
                        result.AddError("CS_Ability_Validate_MissingHealthMaxScale".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.NearbyEnemyBonusDamage:
                    if (nearbyEnemyBonusMaxTargets <= 0)
                    {
                        result.AddError("CS_Ability_Validate_NearbyEnemyBonusMaxTargets".Translate());
                    }
                    if (nearbyEnemyBonusPerTarget <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_NearbyEnemyBonusPerTarget".Translate());
                    }
                    if (nearbyEnemyBonusRadius <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_NearbyEnemyBonusRadius".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.IsolatedTargetBonusDamage:
                    if (isolatedTargetRadius <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_IsolatedTargetRadius".Translate());
                    }
                    if (isolatedTargetBonusDamageScale <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_IsolatedTargetBonusScale".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.MarkDetonation:
                    if (markDurationTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_MarkDurationTicks".Translate());
                    }
                    if (markMaxStacks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_MarkMaxStacks".Translate());
                    }
                    if (markDetonationDamage <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_MarkDetonationDamage".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.ComboStacks:
                    if (comboStackWindowTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_ComboStackWindowTicks".Translate());
                    }
                    if (comboStackMax <= 0)
                    {
                        result.AddError("CS_Ability_Validate_ComboStackMax".Translate());
                    }
                    if (comboStackBonusDamagePerStack < 0f)
                    {
                        result.AddError("CS_Ability_Validate_ComboStackBonusPerStack".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.HitSlowField:
                    if (slowFieldDurationTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_SlowFieldDurationTicks".Translate());
                    }
                    if (slowFieldRadius <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_SlowFieldRadius".Translate());
                    }
                    if (string.IsNullOrWhiteSpace(slowFieldHediffDefName))
                    {
                        result.AddError("CS_Ability_Validate_SlowFieldHediffDefName".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.PierceBonusDamage:
                    if (pierceMaxTargets <= 0)
                    {
                        result.AddError("CS_Ability_Validate_PierceMaxTargets".Translate());
                    }
                    if (pierceBonusDamagePerTarget < 0f)
                    {
                        result.AddError("CS_Ability_Validate_PierceBonusPerTarget".Translate());
                    }
                    if (pierceSearchRange <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_PierceSearchRange".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.DashEmpoweredStrike:
                    if (dashEmpowerDurationTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_DashEmpowerDurationTicks".Translate());
                    }
                    if (dashEmpowerBonusDamageScale <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_DashEmpowerBonusScale".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.HitHeal:
                    if (hitHealAmount < 0f || hitHealRatio < 0f)
                    {
                        result.AddError("CS_Ability_Validate_HitHealValues".Translate());
                    }
                    if (hitHealAmount <= 0f && hitHealRatio <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_HitHealRequired".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.HitCooldownRefund:
                    if (hitCooldownRefundPercent <= 0f || hitCooldownRefundPercent > 1f)
                    {
                        result.AddError("CS_Ability_Validate_HitCooldownRefundPercent".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.ProjectileSplit:
                    if (splitProjectileCount <= 0)
                    {
                        result.AddError("CS_Ability_Validate_SplitProjectileCount".Translate());
                    }
                    if (splitDamageScale <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_SplitDamageScale".Translate());
                    }
                    if (splitSearchRange <= 0f)
                    {
                        result.AddError("CS_Ability_Validate_SplitSearchRange".Translate());
                    }
                    break;

                case AbilityRuntimeComponentType.FlightState:
                    if (flightDurationTicks <= 0)
                    {
                        result.AddError("CS_Ability_Validate_FlightDurationTicks".Translate());
                    }
                    if (flightHeightFactor < 0f)
                    {
                        result.AddError("CS_Ability_Validate_FlightHeightFactor".Translate());
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

    public enum AbilityAreaShape
    {
        Circle,
        Line,
        Cone,
        Cross,
        Square,
        Irregular
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
        ExplosionEffect, // 爆炸闪光
        Preset,
        CustomTexture
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
    /// 贴图来源。
    /// </summary>
    public enum AbilityVisualEffectTextureSource
    {
        Vanilla,
        LocalPath
    }

    /// <summary>
    /// 视觉特效旧来源模式。仅用于兼容旧存档/XML；新逻辑应基于 type/source。
    /// </summary>
    public enum AbilityVisualEffectSourceMode
    {
        BuiltIn,
        Preset,
        CustomTexture
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
        /// 视觉特效类型。内建特效、预设、自定义贴图均统一在此枚举中表示。
        /// </summary>
        public AbilityVisualEffectType type = AbilityVisualEffectType.DustPuff;

        /// <summary>
        /// 自定义贴图来源；仅在 type=CustomTexture 时使用。
        /// </summary>
        public AbilityVisualEffectTextureSource textureSource = AbilityVisualEffectTextureSource.Vanilla;

        /// <summary>
        /// 旧来源模式。仅用于兼容旧 XML 读写与历史运行时分支。
        /// </summary>
        public AbilityVisualEffectSourceMode sourceMode = AbilityVisualEffectSourceMode.BuiltIn;

        /// <summary>
        /// CharacterStudio 原生预设名。仅在 type=Preset 时使用。
        /// </summary>
        public string presetDefName = string.Empty;

        /// <summary>
        /// 自定义贴图路径。Vanilla 来源下为游戏资源路径；LocalPath 来源下为本地绝对路径。
        /// </summary>
        public string customTexturePath = string.Empty;

        public VisualEffectTarget target = VisualEffectTarget.Target;

        /// <summary>
        /// 触发时机。
        /// </summary>
        public AbilityVisualEffectTrigger trigger = AbilityVisualEffectTrigger.OnTargetApply;

        /// <summary>在效果执行后延迟多少 ticks 再播放特效（0 = 立即）</summary>
        public int delayTicks = 0;

        /// <summary>特效可见持续时间（tick）。主要用于自定义贴图特效。</summary>
        public int displayDurationTicks = 27;

        /// <summary>技能触发时附带切换的表情。null = 不干预。</summary>
        public ExpressionType? linkedExpression = null;

        /// <summary>技能触发表情联动的持续时间（tick）。</summary>
        public int linkedExpressionDurationTicks = 30;

        /// <summary>额外瞳孔亮度偏移。0 = 不干预。</summary>
        public float linkedPupilBrightnessOffset = 0f;

        /// <summary>额外瞳孔对比度偏移。0 = 不干预。</summary>
        public float linkedPupilContrastOffset = 0f;

        /// <summary>特效规模缩放（1.0 = 正常）</summary>
        public float scale = 1.0f;

        /// <summary>自定义贴图绘制尺寸（世界单位）。</summary>
        public float drawSize = 1.5f;

        /// <summary>是否按施法者朝向解释前向/侧向偏移。</summary>
        public bool useCasterFacing = false;

        /// <summary>沿施法者前向的额外偏移。</summary>
        public float forwardOffset = 0f;

        /// <summary>沿施法者侧向的额外偏移。正值向右，负值向左。</summary>
        public float sideOffset = 0f;

        /// <summary>额外高度偏移（世界 Y 轴）。</summary>
        public float heightOffset = 0f;

        /// <summary>自定义贴图固定旋转角度（度）。</summary>
        public float rotation = 0f;

        /// <summary>自定义贴图二维缩放。X=宽度，Y=高度。</summary>
        public Vector2 textureScale = Vector2.one;

        /// <summary>重复次数（包含首次播放）；最小为 1。</summary>
        public int repeatCount = 1;

        /// <summary>重复播放间隔 tick。</summary>
        public int repeatIntervalTicks = 0;

        /// <summary>附加偏移，供后续预设/运行时扩展使用。</summary>
        public Vector3 offset = Vector3.zero;

        /// <summary>是否播放伴随音效。</summary>
        public bool playSound = false;

        /// <summary>音效 DefName。运行时可解析为 RimWorld / Verse 的 SoundDef。</summary>
        public string soundDefName = string.Empty;

        /// <summary>音效延迟 tick。用于和视觉特效错峰播放。</summary>
        public int soundDelayTicks = 0;

        /// <summary>音量缩放。</summary>
        public float soundVolume = 1f;

        /// <summary>音高缩放。</summary>
        public float soundPitch = 1f;

        /// <summary>是否附着到 Pawn（供后续预设播放扩展使用）。</summary>
        public bool attachToPawn = false;

        /// <summary>是否附着到目标格（供后续预设播放扩展使用）。</summary>
        public bool attachToTargetCell = false;

        /// <summary>是否启用此特效条目。</summary>
        public bool enabled = true;

        public bool UsesBuiltInType => type != AbilityVisualEffectType.Preset && type != AbilityVisualEffectType.CustomTexture;

        public bool UsesPresetType => type == AbilityVisualEffectType.Preset;

        public bool UsesCustomTextureType => type == AbilityVisualEffectType.CustomTexture;

        public void NormalizeLegacyData()
        {
            if (type == AbilityVisualEffectType.Preset || type == AbilityVisualEffectType.CustomTexture)
            {
                if (type == AbilityVisualEffectType.CustomTexture && string.IsNullOrWhiteSpace(customTexturePath))
                {
                    textureSource = AbilityVisualEffectTextureSource.LocalPath;
                }
                return;
            }

            switch (sourceMode)
            {
                case AbilityVisualEffectSourceMode.Preset:
                    type = AbilityVisualEffectType.Preset;
                    break;
                case AbilityVisualEffectSourceMode.CustomTexture:
                    type = AbilityVisualEffectType.CustomTexture;
                    if (string.IsNullOrWhiteSpace(customTexturePath))
                    {
                        textureSource = AbilityVisualEffectTextureSource.LocalPath;
                    }
                    break;
                default:
                    sourceMode = AbilityVisualEffectSourceMode.BuiltIn;
                    break;
            }
        }

        public void SyncLegacyFields()
        {
            sourceMode = type switch
            {
                AbilityVisualEffectType.Preset => AbilityVisualEffectSourceMode.Preset,
                AbilityVisualEffectType.CustomTexture => AbilityVisualEffectSourceMode.CustomTexture,
                _ => AbilityVisualEffectSourceMode.BuiltIn
            };
        }

        public AbilityVisualEffectConfig Clone()
        {
            AbilityVisualEffectConfig clone = (AbilityVisualEffectConfig)MemberwiseClone();
            clone.SyncLegacyFields();
            return clone;
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
            AbilityAreaShape normalizedShape = NormalizeAreaShape(ability);

            if (CarrierNeedsRange(normalizedCarrier, normalizedTarget) && ability.range <= 0)
            {
                result.AddWarning("CS_Ability_Validate_CarrierNeedsRange".Translate(GetCarrierTypeLabel(normalizedCarrier)));
            }

            if (ability.useRadius && normalizedShape != AbilityAreaShape.Irregular && ability.radius <= 0)
            {
                result.AddError("CS_Ability_Validate_AreaRadiusPositive".Translate());
            }

            if (ability.useRadius && normalizedShape == AbilityAreaShape.Irregular && string.IsNullOrWhiteSpace(ability.irregularAreaPattern))
            {
                result.AddError("CS_Ability_Validate_IrregularPatternRequired".Translate());
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

        public static AbilityAreaShape NormalizeAreaShape(ModularAbilityDef ability)
        {
            if (ability == null || !ability.useRadius)
            {
                return AbilityAreaShape.Circle;
            }

            return ability.areaShape;
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