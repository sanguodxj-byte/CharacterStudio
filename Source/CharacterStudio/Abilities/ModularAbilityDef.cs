using System;
using System.Collections.Generic;
using System.Linq;
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
        public string iconPath = "";
        public float cooldownTicks = 600f;
        public float warmupTicks = 60f;
        public int charges = 1;
        public float aiCanUse = 1f;

        public AbilityCarrierType carrierType = AbilityCarrierType.Self;
        public AbilityTargetType targetType = AbilityTargetType.Self;
        public bool useRadius = false;
        public AbilityAreaCenter areaCenter = AbilityAreaCenter.Target;
        public AbilityAreaShape areaShape = AbilityAreaShape.Circle;
        public string irregularAreaPattern = string.Empty;
        public float range = 20f;
        public float radius = 0f;
        public ThingDef? projectileDef;

        public List<AbilityEffectConfig> effects = new List<AbilityEffectConfig>();
        public List<AbilityVisualEffectConfig> visualEffects = new List<AbilityVisualEffectConfig>();
        public List<AbilityRuntimeComponentConfig> runtimeComponents = new List<AbilityRuntimeComponentConfig>();

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

        public void NormalizeForSave()
        {
            iconPath = AbilityEditorNormalizationUtility.TrimOrEmpty(iconPath);
            irregularAreaPattern = irregularAreaPattern ?? string.Empty;

            cooldownTicks = AbilityEditorNormalizationUtility.ClampFloat(cooldownTicks, 0f, 100000f);
            warmupTicks = AbilityEditorNormalizationUtility.ClampFloat(warmupTicks, 0f, 100000f);
            charges = AbilityEditorNormalizationUtility.ClampInt(charges, 1, 999);
            aiCanUse = AbilityEditorNormalizationUtility.ClampFloat(aiCanUse, 0f, 1f);

            carrierType = ModularAbilityDefExtensions.NormalizeCarrierType(carrierType);
            targetType = ModularAbilityDefExtensions.NormalizeTargetType(this);
            areaCenter = ModularAbilityDefExtensions.NormalizeAreaCenter(this);
            areaShape = ModularAbilityDefExtensions.NormalizeAreaShape(this);

            range = AbilityEditorNormalizationUtility.ClampFloat(range, 0f, 100f);
            radius = AbilityEditorNormalizationUtility.ClampFloat(radius, 0f, 20f);
            if (useRadius && areaShape != AbilityAreaShape.Irregular && radius <= 0f)
            {
                radius = 0.1f;
            }

            effects ??= new List<AbilityEffectConfig>();
            runtimeComponents ??= new List<AbilityRuntimeComponentConfig>();
            visualEffects ??= new List<AbilityVisualEffectConfig>();

            for (int i = 0; i < effects.Count; i++)
            {
                effects[i]?.NormalizeForSave();
            }

            for (int i = 0; i < runtimeComponents.Count; i++)
            {
                runtimeComponents[i]?.NormalizeForSave();
            }

            for (int i = 0; i < visualEffects.Count; i++)
            {
                visualEffects[i]?.NormalizeForSave();
            }
        }
    }

    public enum AbilityRuntimeComponentType
    {
        QComboWindow,
        HotkeyOverride,
        FollowupCooldownGate,
        SmartJump,
        EShortJump,
        RStackDetonation,
        PeriodicPulse,
        KillRefresh,
        ShieldAbsorb,
        AttachedShieldVisual,
        ProjectileInterceptorShield,
        ChainBounce,
        ExecuteBonusDamage,
        FullHealthBonusDamage,
        MissingHealthBonusDamage,
        NearbyEnemyBonusDamage,
        IsolatedTargetBonusDamage,
        MarkDetonation,
        ComboStacks,
        HitSlowField,
        PierceBonusDamage,
        DashEmpoweredStrike,
        HitHeal,
        HitCooldownRefund,
        ProjectileSplit,
        FlightState,
        VanillaPawnFlyer,
        FlightOnlyFollowup,
        FlightLandingBurst,
        TimeStop
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

        public int comboWindowTicks = 12;
        public AbilityRuntimeHotkeySlot overrideHotkeySlot = AbilityRuntimeHotkeySlot.Q;
        public string overrideAbilityDefName = string.Empty;
        public int overrideDurationTicks = 60;
        public AbilityRuntimeHotkeySlot followupCooldownHotkeySlot = AbilityRuntimeHotkeySlot.Q;
        public int followupCooldownTicks = 60;
        public AbilityRuntimeHotkeySlot killRefreshHotkeySlot = AbilityRuntimeHotkeySlot.Q;
        public int cooldownTicks = 120;
        public int jumpDistance = 6;
        public int findCellRadius = 3;
        public bool triggerAbilityEffectsAfterJump = true;
        public bool useMouseTargetCell = true;
        public int smartCastOffsetCells = 1;
        public bool smartCastClampToMaxDistance = true;
        public bool smartCastAllowFallbackForward = true;
        public int requiredStacks = 7;
        public int delayTicks = 180;
        public float wave1Radius = 3f;
        public float wave1Damage = 80f;
        public float wave2Radius = 6f;
        public float wave2Damage = 140f;
        public float wave3Radius = 9f;
        public float wave3Damage = 220f;
        public DamageDef? waveDamageDef;
        public int pulseIntervalTicks = 60;
        public int pulseTotalTicks = 240;
        public bool pulseStartsImmediately = true;
        public float killRefreshCooldownPercent = 1f;
        public float shieldMaxDamage = 120f;
        public float shieldDurationTicks = 240f;
        public float shieldHealRatio = 0.5f;
        public float shieldBonusDamageRatio = 0.25f;
        public float shieldVisualScale = 1f;
        public float shieldVisualHeightOffset = 0f;
        public string shieldInterceptorThingDefName = string.Empty;
        public int shieldInterceptorDurationTicks = 240;
        public int maxBounceCount = 4;
        public float bounceRange = 6f;
        public float bounceDamageFalloff = 0.2f;
        public float executeThresholdPercent = 0.3f;
        public float executeBonusDamageScale = 0.5f;
        public float fullHealthThresholdPercent = 0.95f;
        public float fullHealthBonusDamageScale = 0.35f;
        public float missingHealthBonusPerTenPercent = 0.05f;
        public float missingHealthBonusMaxScale = 0.5f;
        public int nearbyEnemyBonusMaxTargets = 4;
        public float nearbyEnemyBonusPerTarget = 0.08f;
        public float nearbyEnemyBonusRadius = 5f;
        public float isolatedTargetRadius = 4f;
        public float isolatedTargetBonusDamageScale = 0.35f;
        public int markDurationTicks = 180;
        public int markMaxStacks = 3;
        public float markDetonationDamage = 40f;
        public DamageDef? markDamageDef;
        public int comboStackWindowTicks = 180;
        public int comboStackMax = 5;
        public float comboStackBonusDamagePerStack = 0.06f;
        public int slowFieldDurationTicks = 180;
        public float slowFieldRadius = 2.5f;
        public string slowFieldHediffDefName = string.Empty;
        public int pierceMaxTargets = 3;
        public float pierceBonusDamagePerTarget = 0.15f;
        public float pierceSearchRange = 5f;
        public int dashEmpowerDurationTicks = 180;
        public float dashEmpowerBonusDamageScale = 0.5f;
        public float hitHealAmount = 8f;
        public float hitHealRatio = 0f;
        public AbilityRuntimeHotkeySlot refundHotkeySlot = AbilityRuntimeHotkeySlot.Q;
        public float hitCooldownRefundPercent = 0.15f;
        public int splitProjectileCount = 2;
        public float splitDamageScale = 0.5f;
        public float splitSearchRange = 5f;
        public int flightDurationTicks = 180;
        public float flightHeightFactor = 0.35f;
        public bool suppressCombatActionsDuringFlightState = true;

        public string flyerThingDefName = string.Empty;
        public int flyerWarmupTicks = 0;
        public bool launchFromCasterPosition = true;
        public bool requireValidTargetCell = true;
        public bool storeTargetForFollowup = true;
        public bool enableFlightOnlyWindow = false;
        public int flightOnlyWindowTicks = 180;
        public string flightOnlyAbilityDefName = string.Empty;
        public bool hideCasterDuringTakeoff = true;
        public bool autoExpireFlightMarkerOnLanding = true;

        public string requiredFlightSourceAbilityDefName = string.Empty;
        public bool requireReservedTargetCell = false;
        public bool consumeFlightStateOnCast = false;
        public bool onlyUseDuringFlightWindow = true;

        public float landingBurstRadius = 3f;
        public float landingBurstDamage = 30f;
        public DamageDef? landingBurstDamageDef;
        public string landingEffecterDefName = string.Empty;
        public string landingSoundDefName = string.Empty;
        public bool affectBuildings = false;
        public bool affectCells = true;
        public bool knockbackTargets = false;
        public float knockbackDistance = 1.5f;
        public int timeStopDurationTicks = 60;
        public bool freezeVisualsDuringTimeStop = true;

        public AbilityRuntimeComponentConfig Clone()
        {
            return (AbilityRuntimeComponentConfig)MemberwiseClone();
        }

        public void NormalizeForSave()
        {
            overrideAbilityDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(overrideAbilityDefName);
            slowFieldHediffDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(slowFieldHediffDefName);
            flyerThingDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(flyerThingDefName);
            flightOnlyAbilityDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(flightOnlyAbilityDefName);
            requiredFlightSourceAbilityDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(requiredFlightSourceAbilityDefName);
            landingEffecterDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(landingEffecterDefName);
            landingSoundDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(landingSoundDefName);

            comboWindowTicks = AbilityEditorNormalizationUtility.ClampInt(comboWindowTicks, 1, 9999);
            overrideDurationTicks = AbilityEditorNormalizationUtility.ClampInt(overrideDurationTicks, 1, 99999);
            followupCooldownTicks = AbilityEditorNormalizationUtility.ClampInt(followupCooldownTicks, 1, 99999);
            cooldownTicks = AbilityEditorNormalizationUtility.ClampInt(cooldownTicks, 0, 99999);
            jumpDistance = AbilityEditorNormalizationUtility.ClampInt(jumpDistance, 1, 100);
            findCellRadius = AbilityEditorNormalizationUtility.ClampInt(findCellRadius, 0, 30);
            smartCastOffsetCells = AbilityEditorNormalizationUtility.ClampInt(smartCastOffsetCells, 1, 100);
            requiredStacks = AbilityEditorNormalizationUtility.ClampInt(requiredStacks, 1, 999);
            delayTicks = AbilityEditorNormalizationUtility.ClampInt(delayTicks, 0, 99999);
            wave1Radius = AbilityEditorNormalizationUtility.ClampFloat(wave1Radius, 0.1f, 99f);
            wave1Damage = AbilityEditorNormalizationUtility.ClampFloat(wave1Damage, 1f, 99999f);
            wave2Radius = AbilityEditorNormalizationUtility.ClampFloat(wave2Radius, 0.1f, 99f);
            wave2Damage = AbilityEditorNormalizationUtility.ClampFloat(wave2Damage, 1f, 99999f);
            wave3Radius = AbilityEditorNormalizationUtility.ClampFloat(wave3Radius, 0.1f, 99f);
            wave3Damage = AbilityEditorNormalizationUtility.ClampFloat(wave3Damage, 1f, 99999f);
            pulseIntervalTicks = AbilityEditorNormalizationUtility.ClampInt(pulseIntervalTicks, 1, 99999);
            pulseTotalTicks = AbilityEditorNormalizationUtility.ClampInt(pulseTotalTicks, 1, 99999);
            killRefreshCooldownPercent = AbilityEditorNormalizationUtility.ClampFloat(killRefreshCooldownPercent, 0.01f, 1f);
            shieldMaxDamage = AbilityEditorNormalizationUtility.ClampFloat(shieldMaxDamage, 1f, 99999f);
            shieldDurationTicks = AbilityEditorNormalizationUtility.ClampFloat(shieldDurationTicks, 1f, 99999f);
            shieldHealRatio = AbilityEditorNormalizationUtility.ClampFloat(shieldHealRatio, 0f, 10f);
            shieldBonusDamageRatio = AbilityEditorNormalizationUtility.ClampFloat(shieldBonusDamageRatio, 0f, 10f);
            shieldVisualScale = AbilityEditorNormalizationUtility.ClampFloat(shieldVisualScale, 0.1f, 10f);
            shieldVisualHeightOffset = AbilityEditorNormalizationUtility.ClampFloat(shieldVisualHeightOffset, -5f, 5f);
            shieldInterceptorThingDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(shieldInterceptorThingDefName);
            shieldInterceptorDurationTicks = AbilityEditorNormalizationUtility.ClampInt(shieldInterceptorDurationTicks, 1, 99999);
            maxBounceCount = AbilityEditorNormalizationUtility.ClampInt(maxBounceCount, 1, 99);
            bounceRange = AbilityEditorNormalizationUtility.ClampFloat(bounceRange, 0.1f, 99f);
            bounceDamageFalloff = AbilityEditorNormalizationUtility.ClampFloat(bounceDamageFalloff, -0.95f, 0.95f);
            executeThresholdPercent = AbilityEditorNormalizationUtility.ClampFloat(executeThresholdPercent, 0.01f, 0.99f);
            executeBonusDamageScale = AbilityEditorNormalizationUtility.ClampFloat(executeBonusDamageScale, 0.01f, 10f);
            fullHealthThresholdPercent = AbilityEditorNormalizationUtility.ClampFloat(fullHealthThresholdPercent, 0.01f, 1f);
            fullHealthBonusDamageScale = AbilityEditorNormalizationUtility.ClampFloat(fullHealthBonusDamageScale, 0.01f, 10f);
            missingHealthBonusPerTenPercent = AbilityEditorNormalizationUtility.ClampFloat(missingHealthBonusPerTenPercent, 0.01f, 10f);
            missingHealthBonusMaxScale = AbilityEditorNormalizationUtility.ClampFloat(missingHealthBonusMaxScale, 0.01f, 10f);
            nearbyEnemyBonusMaxTargets = AbilityEditorNormalizationUtility.ClampInt(nearbyEnemyBonusMaxTargets, 1, 99);
            nearbyEnemyBonusPerTarget = AbilityEditorNormalizationUtility.ClampFloat(nearbyEnemyBonusPerTarget, 0.01f, 10f);
            nearbyEnemyBonusRadius = AbilityEditorNormalizationUtility.ClampFloat(nearbyEnemyBonusRadius, 0.1f, 99f);
            isolatedTargetRadius = AbilityEditorNormalizationUtility.ClampFloat(isolatedTargetRadius, 0.1f, 99f);
            isolatedTargetBonusDamageScale = AbilityEditorNormalizationUtility.ClampFloat(isolatedTargetBonusDamageScale, 0.01f, 10f);
            markDurationTicks = AbilityEditorNormalizationUtility.ClampInt(markDurationTicks, 1, 99999);
            markMaxStacks = AbilityEditorNormalizationUtility.ClampInt(markMaxStacks, 1, 99);
            markDetonationDamage = AbilityEditorNormalizationUtility.ClampFloat(markDetonationDamage, 0.01f, 99999f);
            comboStackWindowTicks = AbilityEditorNormalizationUtility.ClampInt(comboStackWindowTicks, 1, 99999);
            comboStackMax = AbilityEditorNormalizationUtility.ClampInt(comboStackMax, 1, 99);
            comboStackBonusDamagePerStack = AbilityEditorNormalizationUtility.ClampFloat(comboStackBonusDamagePerStack, 0.01f, 10f);
            slowFieldDurationTicks = AbilityEditorNormalizationUtility.ClampInt(slowFieldDurationTicks, 1, 99999);
            slowFieldRadius = AbilityEditorNormalizationUtility.ClampFloat(slowFieldRadius, 0.1f, 99f);
            pierceMaxTargets = AbilityEditorNormalizationUtility.ClampInt(pierceMaxTargets, 1, 99);
            pierceBonusDamagePerTarget = AbilityEditorNormalizationUtility.ClampFloat(pierceBonusDamagePerTarget, 0.01f, 10f);
            pierceSearchRange = AbilityEditorNormalizationUtility.ClampFloat(pierceSearchRange, 0.1f, 99f);
            dashEmpowerDurationTicks = AbilityEditorNormalizationUtility.ClampInt(dashEmpowerDurationTicks, 1, 99999);
            dashEmpowerBonusDamageScale = AbilityEditorNormalizationUtility.ClampFloat(dashEmpowerBonusDamageScale, 0.01f, 10f);
            hitHealAmount = AbilityEditorNormalizationUtility.ClampFloat(hitHealAmount, 0f, 99999f);
            hitHealRatio = AbilityEditorNormalizationUtility.ClampFloat(hitHealRatio, 0f, 10f);
            hitCooldownRefundPercent = AbilityEditorNormalizationUtility.ClampFloat(hitCooldownRefundPercent, 0.01f, 1f);
            splitProjectileCount = AbilityEditorNormalizationUtility.ClampInt(splitProjectileCount, 1, 99);
            splitDamageScale = AbilityEditorNormalizationUtility.ClampFloat(splitDamageScale, 0.01f, 10f);
            splitSearchRange = AbilityEditorNormalizationUtility.ClampFloat(splitSearchRange, 0.1f, 99f);
            flightDurationTicks = AbilityEditorNormalizationUtility.ClampInt(flightDurationTicks, 1, 99999);
            flightHeightFactor = AbilityEditorNormalizationUtility.ClampFloat(flightHeightFactor, 0f, 5f);
            flyerWarmupTicks = AbilityEditorNormalizationUtility.ClampInt(flyerWarmupTicks, 0, 99999);
            flightOnlyWindowTicks = AbilityEditorNormalizationUtility.ClampInt(flightOnlyWindowTicks, 1, 99999);
            landingBurstRadius = AbilityEditorNormalizationUtility.ClampFloat(landingBurstRadius, 0.1f, 99f);
            landingBurstDamage = AbilityEditorNormalizationUtility.ClampFloat(landingBurstDamage, 0.01f, 99999f);
            knockbackDistance = AbilityEditorNormalizationUtility.ClampFloat(knockbackDistance, 0f, 99f);
            timeStopDurationTicks = AbilityEditorNormalizationUtility.ClampInt(timeStopDurationTicks, 1, 99999);
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
                        result.AddError("CS_Ability_Validate_QComboWindowTicks".Translate());
                    break;
                case AbilityRuntimeComponentType.HotkeyOverride:
                    if (string.IsNullOrWhiteSpace(overrideAbilityDefName))
                        result.AddError("CS_Ability_Validate_HotkeyOverrideAbilityDefName".Translate());
                    if (overrideDurationTicks <= 0)
                        result.AddError("CS_Ability_Validate_HotkeyOverrideDurationTicks".Translate());
                    break;
                case AbilityRuntimeComponentType.FollowupCooldownGate:
                    if (followupCooldownTicks <= 0)
                        result.AddError("CS_Ability_Validate_FollowupCooldownTicks".Translate());
                    break;
                case AbilityRuntimeComponentType.SmartJump:
                case AbilityRuntimeComponentType.EShortJump:
                    if (cooldownTicks < 0)
                        result.AddError("CS_Ability_Validate_EShortJumpCooldown".Translate());
                    if (jumpDistance <= 0)
                        result.AddError("CS_Ability_Validate_EShortJumpDistance".Translate());
                    if (findCellRadius < 0)
                        result.AddError("CS_Ability_Validate_EShortJumpFindCellRadius".Translate());
                    if (type == AbilityRuntimeComponentType.SmartJump && smartCastOffsetCells <= 0)
                        result.AddError("CS_Ability_Validate_SmartJumpOffsetCells".Translate());
                    break;
                case AbilityRuntimeComponentType.RStackDetonation:
                    if (requiredStacks <= 0)
                        result.AddError("CS_Ability_Validate_RRequiredStacks".Translate());
                    if (delayTicks < 0)
                        result.AddError("CS_Ability_Validate_RDelayTicks".Translate());
                    if (wave1Radius <= 0 || wave2Radius <= 0 || wave3Radius <= 0)
                        result.AddError("CS_Ability_Validate_RWaveRadius".Translate());
                    if (wave1Damage <= 0 || wave2Damage <= 0 || wave3Damage <= 0)
                        result.AddError("CS_Ability_Validate_RWaveDamage".Translate());
                    break;
                case AbilityRuntimeComponentType.PeriodicPulse:
                    if (pulseIntervalTicks <= 0)
                        result.AddError("CS_Ability_Validate_PeriodicPulseIntervalTicks".Translate());
                    if (pulseTotalTicks <= 0)
                        result.AddError("CS_Ability_Validate_PeriodicPulseTotalTicks".Translate());
                    break;
                case AbilityRuntimeComponentType.KillRefresh:
                    if (killRefreshCooldownPercent <= 0f || killRefreshCooldownPercent > 1f)
                        result.AddError("CS_Ability_Validate_KillRefreshCooldownPercent".Translate());
                    break;
                case AbilityRuntimeComponentType.ShieldAbsorb:
                    if (shieldMaxDamage <= 0f)
                        result.AddError("CS_Ability_Validate_ShieldMaxDamage".Translate());
                    if (shieldDurationTicks <= 0f)
                        result.AddError("CS_Ability_Validate_ShieldDurationTicks".Translate());
                    if (shieldHealRatio < 0f || shieldBonusDamageRatio < 0f)
                        result.AddError("CS_Ability_Validate_ShieldRatios".Translate());
                    break;
                case AbilityRuntimeComponentType.AttachedShieldVisual:
                    if (shieldVisualScale <= 0f)
                        result.AddError("CS_Ability_Validate_AttachedShieldVisualScale".Translate());
                    break;
                case AbilityRuntimeComponentType.ProjectileInterceptorShield:
                    if (string.IsNullOrWhiteSpace(shieldInterceptorThingDefName))
                        result.AddError("CS_Ability_Validate_ProjectileInterceptorThingDef".Translate());
                    if (shieldInterceptorDurationTicks <= 0)
                        result.AddError("CS_Ability_Validate_ProjectileInterceptorDuration".Translate());
                    break;
                case AbilityRuntimeComponentType.ChainBounce:
                    if (maxBounceCount <= 0)
                        result.AddError("CS_Ability_Validate_ChainBounceCount".Translate());
                    if (bounceRange <= 0f)
                        result.AddError("CS_Ability_Validate_ChainBounceRange".Translate());
                    if (bounceDamageFalloff < 0f || bounceDamageFalloff >= 1f)
                        result.AddError("CS_Ability_Validate_ChainBounceFalloff".Translate());
                    break;
                case AbilityRuntimeComponentType.ExecuteBonusDamage:
                    if (executeThresholdPercent <= 0f || executeThresholdPercent >= 1f)
                        result.AddError("CS_Ability_Validate_ExecuteThreshold".Translate());
                    if (executeBonusDamageScale <= 0f)
                        result.AddError("CS_Ability_Validate_ExecuteBonusScale".Translate());
                    break;
                case AbilityRuntimeComponentType.FullHealthBonusDamage:
                    if (fullHealthThresholdPercent <= 0f || fullHealthThresholdPercent > 1f)
                        result.AddError("CS_Ability_Validate_FullHealthThreshold".Translate());
                    if (fullHealthBonusDamageScale <= 0f)
                        result.AddError("CS_Ability_Validate_FullHealthBonusScale".Translate());
                    break;
                case AbilityRuntimeComponentType.MissingHealthBonusDamage:
                    if (missingHealthBonusPerTenPercent < 0f)
                        result.AddError("CS_Ability_Validate_MissingHealthPerTen".Translate());
                    if (missingHealthBonusMaxScale < 0f)
                        result.AddError("CS_Ability_Validate_MissingHealthMaxScale".Translate());
                    break;
                case AbilityRuntimeComponentType.NearbyEnemyBonusDamage:
                    if (nearbyEnemyBonusMaxTargets <= 0)
                        result.AddError("CS_Ability_Validate_NearbyEnemyBonusMaxTargets".Translate());
                    if (nearbyEnemyBonusPerTarget <= 0f)
                        result.AddError("CS_Ability_Validate_NearbyEnemyBonusPerTarget".Translate());
                    if (nearbyEnemyBonusRadius <= 0f)
                        result.AddError("CS_Ability_Validate_NearbyEnemyBonusRadius".Translate());
                    break;
                case AbilityRuntimeComponentType.IsolatedTargetBonusDamage:
                    if (isolatedTargetRadius <= 0f)
                        result.AddError("CS_Ability_Validate_IsolatedTargetRadius".Translate());
                    if (isolatedTargetBonusDamageScale <= 0f)
                        result.AddError("CS_Ability_Validate_IsolatedTargetBonusScale".Translate());
                    break;
                case AbilityRuntimeComponentType.MarkDetonation:
                    if (markDurationTicks <= 0)
                        result.AddError("CS_Ability_Validate_MarkDurationTicks".Translate());
                    if (markMaxStacks <= 0)
                        result.AddError("CS_Ability_Validate_MarkMaxStacks".Translate());
                    if (markDetonationDamage <= 0f)
                        result.AddError("CS_Ability_Validate_MarkDetonationDamage".Translate());
                    break;
                case AbilityRuntimeComponentType.ComboStacks:
                    if (comboStackWindowTicks <= 0)
                        result.AddError("CS_Ability_Validate_ComboStackWindowTicks".Translate());
                    if (comboStackMax <= 0)
                        result.AddError("CS_Ability_Validate_ComboStackMax".Translate());
                    if (comboStackBonusDamagePerStack < 0f)
                        result.AddError("CS_Ability_Validate_ComboStackBonusPerStack".Translate());
                    break;
                case AbilityRuntimeComponentType.HitSlowField:
                    if (slowFieldDurationTicks <= 0)
                        result.AddError("CS_Ability_Validate_SlowFieldDurationTicks".Translate());
                    if (slowFieldRadius <= 0f)
                        result.AddError("CS_Ability_Validate_SlowFieldRadius".Translate());
                    if (string.IsNullOrWhiteSpace(slowFieldHediffDefName))
                        result.AddError("CS_Ability_Validate_SlowFieldHediffDefName".Translate());
                    break;
                case AbilityRuntimeComponentType.PierceBonusDamage:
                    if (pierceMaxTargets <= 0)
                        result.AddError("CS_Ability_Validate_PierceMaxTargets".Translate());
                    if (pierceBonusDamagePerTarget < 0f)
                        result.AddError("CS_Ability_Validate_PierceBonusPerTarget".Translate());
                    if (pierceSearchRange <= 0f)
                        result.AddError("CS_Ability_Validate_PierceSearchRange".Translate());
                    break;
                case AbilityRuntimeComponentType.DashEmpoweredStrike:
                    if (dashEmpowerDurationTicks <= 0)
                        result.AddError("CS_Ability_Validate_DashEmpowerDurationTicks".Translate());
                    if (dashEmpowerBonusDamageScale <= 0f)
                        result.AddError("CS_Ability_Validate_DashEmpowerBonusScale".Translate());
                    break;
                case AbilityRuntimeComponentType.HitHeal:
                    if (hitHealAmount < 0f || hitHealRatio < 0f)
                        result.AddError("CS_Ability_Validate_HitHealValues".Translate());
                    if (hitHealAmount <= 0f && hitHealRatio <= 0f)
                        result.AddError("CS_Ability_Validate_HitHealRequired".Translate());
                    break;
                case AbilityRuntimeComponentType.HitCooldownRefund:
                    if (hitCooldownRefundPercent <= 0f || hitCooldownRefundPercent > 1f)
                        result.AddError("CS_Ability_Validate_HitCooldownRefundPercent".Translate());
                    break;
                case AbilityRuntimeComponentType.ProjectileSplit:
                    if (splitProjectileCount <= 0)
                        result.AddError("CS_Ability_Validate_SplitProjectileCount".Translate());
                    if (splitDamageScale <= 0f)
                        result.AddError("CS_Ability_Validate_SplitDamageScale".Translate());
                    if (splitSearchRange <= 0f)
                        result.AddError("CS_Ability_Validate_SplitSearchRange".Translate());
                    break;
                case AbilityRuntimeComponentType.FlightState:
                    if (flightDurationTicks <= 0)
                        result.AddError("CS_Ability_Validate_FlightDurationTicks".Translate());
                    if (flightHeightFactor < 0f)
                        result.AddError("CS_Ability_Validate_FlightHeightFactor".Translate());
                    if (flightHeightFactor > 5f)
                        result.AddWarning("CS_Ability_Validate_FlightHeightFactorWarning".Translate());
                    break;
                case AbilityRuntimeComponentType.VanillaPawnFlyer:
                    if (string.IsNullOrWhiteSpace(flyerThingDefName))
                        result.AddError("CS_Ability_Validate_VanillaFlyerThingDefRequired".Translate());
                    if (flightDurationTicks <= 0)
                        result.AddError("CS_Ability_Validate_FlightDurationTicks".Translate());
                    if (enableFlightOnlyWindow && flightOnlyWindowTicks <= 0)
                        result.AddError("CS_Ability_Validate_VanillaFlightWindowTicks".Translate());
                    break;
                case AbilityRuntimeComponentType.FlightOnlyFollowup:
                    if (onlyUseDuringFlightWindow && string.IsNullOrWhiteSpace(requiredFlightSourceAbilityDefName))
                        result.AddWarning("CS_Ability_Validate_FlightOnlyFollowupSourceRecommended".Translate());
                    if (requireReservedTargetCell && !onlyUseDuringFlightWindow)
                        result.AddWarning("CS_Ability_Validate_FlightOnlyFollowupReservedTargetRecommended".Translate());
                    break;
                case AbilityRuntimeComponentType.FlightLandingBurst:
                    if (landingBurstRadius <= 0f)
                        result.AddError("CS_Ability_Validate_LandingBurstRadius".Translate());
                    if (landingBurstDamage <= 0f)
                        result.AddError("CS_Ability_Validate_LandingBurstDamage".Translate());
                    break;
                case AbilityRuntimeComponentType.TimeStop:
                    if (timeStopDurationTicks <= 0)
                        result.AddError("CS_Ability_Validate_TimeStopDurationTicks".Translate());
                    break;
            }

            return result;
        }
    }

    public enum AbilityCarrierType
    {
        Self,
        Target,
        Projectile,
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

    public enum AbilityEffectType
    {
        Damage,
        Heal,
        Buff,
        Debuff,
        Summon,
        Teleport,
        Control,
        Terraform
    }

    public enum AbilityVisualEffectType
    {
        DustPuff,
        MicroSparks,
        LightningGlow,
        FireGlow,
        Smoke,
        ExplosionEffect,
        LineTexture,
        WallTexture,
        Preset,
        CustomTexture
    }

    public enum AbilityVisualSpatialMode
    {
        Point,
        Line,
        Wall
    }

    public enum AbilityVisualAnchorMode
    {
        Caster,
        Target,
        TargetCell,
        AreaCenter
    }

    public enum AbilityVisualPathMode
    {
        None,
        DirectLineCasterToTarget
    }

    public enum VisualEffectTarget
    {
        Caster,
        Target,
        Both
    }

    public enum AbilityVisualEffectTextureSource
    {
        Vanilla,
        LocalPath
    }

    public enum AbilityVisualEffectSourceMode
    {
        BuiltIn,
        Preset,
        CustomTexture
    }

    public enum AbilityVisualFacingMode
    {
        None,
        CasterFacing,
        CastDirection
    }

    public enum AbilityVisualEffectTrigger
    {
        OnCastStart,
        OnWarmup,
        OnCastFinish,
        OnTargetApply,
        OnDurationTick,
        OnExpire
    }

    public class AbilityVisualEffectConfig
    {
        public AbilityVisualEffectType type = AbilityVisualEffectType.DustPuff;
        public AbilityVisualSpatialMode spatialMode = AbilityVisualSpatialMode.Point;
        public AbilityVisualAnchorMode anchorMode = AbilityVisualAnchorMode.Target;
        public AbilityVisualAnchorMode secondaryAnchorMode = AbilityVisualAnchorMode.Target;
        public AbilityVisualPathMode pathMode = AbilityVisualPathMode.None;
        public AbilityVisualEffectTextureSource textureSource = AbilityVisualEffectTextureSource.Vanilla;
        public AbilityVisualEffectSourceMode sourceMode = AbilityVisualEffectSourceMode.BuiltIn;
        public string presetDefName = string.Empty;
        public string customTexturePath = string.Empty;
        public VisualEffectTarget target = VisualEffectTarget.Target;
        public AbilityVisualEffectTrigger trigger = AbilityVisualEffectTrigger.OnTargetApply;
        public int delayTicks = 0;
        public int displayDurationTicks = 27;
        public ExpressionType? linkedExpression = null;
        public int linkedExpressionDurationTicks = 30;
        public float linkedPupilBrightnessOffset = 0f;
        public float linkedPupilContrastOffset = 0f;
        public float scale = 1.0f;
        public float drawSize = 1.5f;
        public AbilityVisualFacingMode facingMode = AbilityVisualFacingMode.None;
        public bool useCasterFacing = false;
        public float forwardOffset = 0f;
        public float sideOffset = 0f;
        public float heightOffset = 0f;
        public float rotation = 0f;
        public Vector2 textureScale = Vector2.one;
        public float lineWidth = 0.35f;
        public float wallHeight = 2.5f;
        public float wallThickness = 0.2f;
        public bool tileByLength = true;
        public bool followGround = false;
        public int segmentCount = 1;
        public bool revealBySegments = false;
        public int segmentRevealIntervalTicks = 3;
        public int repeatCount = 1;
        public int repeatIntervalTicks = 0;
        public Vector3 offset = Vector3.zero;
        public bool playSound = false;
        public string soundDefName = string.Empty;
        public int soundDelayTicks = 0;
        public float soundVolume = 1f;
        public float soundPitch = 1f;
        public bool attachToPawn = false;
        public bool attachToTargetCell = false;
        public bool enabled = true;

        public bool UsesBuiltInType => type != AbilityVisualEffectType.Preset && type != AbilityVisualEffectType.CustomTexture;
        public bool UsesPresetType => type == AbilityVisualEffectType.Preset;
        public bool UsesCustomTextureType => type == AbilityVisualEffectType.CustomTexture;
        public bool UsesSpatialLine => type == AbilityVisualEffectType.LineTexture || spatialMode == AbilityVisualSpatialMode.Line;
        public bool UsesSpatialWall => type == AbilityVisualEffectType.WallTexture || spatialMode == AbilityVisualSpatialMode.Wall;
        public bool RequiresTexturePath => UsesCustomTextureType || type == AbilityVisualEffectType.LineTexture || type == AbilityVisualEffectType.WallTexture;

        public void NormalizeLegacyData()
        {
            if (!System.Enum.IsDefined(typeof(AbilityVisualSpatialMode), spatialMode))
            {
                spatialMode = type == AbilityVisualEffectType.WallTexture
                    ? AbilityVisualSpatialMode.Wall
                    : type == AbilityVisualEffectType.LineTexture
                        ? AbilityVisualSpatialMode.Line
                        : AbilityVisualSpatialMode.Point;
            }

            if (!System.Enum.IsDefined(typeof(AbilityVisualAnchorMode), anchorMode))
            {
                anchorMode = AbilityVisualAnchorMode.Target;
            }

            if (!System.Enum.IsDefined(typeof(AbilityVisualAnchorMode), secondaryAnchorMode))
            {
                secondaryAnchorMode = AbilityVisualAnchorMode.Target;
            }

            if (!System.Enum.IsDefined(typeof(AbilityVisualPathMode), pathMode))
            {
                pathMode = AbilityVisualPathMode.None;
            }

            if (!System.Enum.IsDefined(typeof(AbilityVisualFacingMode), facingMode))
            {
                facingMode = useCasterFacing ? AbilityVisualFacingMode.CasterFacing : AbilityVisualFacingMode.None;
            }

            if (facingMode == AbilityVisualFacingMode.None && useCasterFacing)
            {
                facingMode = AbilityVisualFacingMode.CasterFacing;
            }

            useCasterFacing = facingMode == AbilityVisualFacingMode.CasterFacing;

            if (type == AbilityVisualEffectType.LineTexture)
            {
                spatialMode = AbilityVisualSpatialMode.Line;
                if (pathMode == AbilityVisualPathMode.None)
                {
                    pathMode = AbilityVisualPathMode.DirectLineCasterToTarget;
                }
                if (anchorMode == AbilityVisualAnchorMode.Target)
                {
                    anchorMode = AbilityVisualAnchorMode.Caster;
                }
                if (secondaryAnchorMode == AbilityVisualAnchorMode.Caster)
                {
                    secondaryAnchorMode = AbilityVisualAnchorMode.Target;
                }
            }
            else if (type == AbilityVisualEffectType.WallTexture)
            {
                spatialMode = AbilityVisualSpatialMode.Wall;
                if (pathMode == AbilityVisualPathMode.None)
                {
                    pathMode = AbilityVisualPathMode.DirectLineCasterToTarget;
                }
                if (anchorMode == AbilityVisualAnchorMode.Target)
                {
                    anchorMode = AbilityVisualAnchorMode.Caster;
                }
                if (secondaryAnchorMode == AbilityVisualAnchorMode.Caster)
                {
                    secondaryAnchorMode = AbilityVisualAnchorMode.Target;
                }
            }
            else if (spatialMode != AbilityVisualSpatialMode.Point)
            {
                spatialMode = AbilityVisualSpatialMode.Point;
            }

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

            useCasterFacing = facingMode == AbilityVisualFacingMode.CasterFacing;
        }

        public AbilityVisualEffectConfig Clone()
        {
            AbilityVisualEffectConfig clone = (AbilityVisualEffectConfig)MemberwiseClone();
            clone.SyncLegacyFields();
            return clone;
        }

        public void NormalizeForSave()
        {
            presetDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(presetDefName);
            customTexturePath = AbilityEditorNormalizationUtility.TrimOrEmpty(customTexturePath);
            soundDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(soundDefName);
            NormalizeLegacyData();
            trigger = trigger switch
            {
                AbilityVisualEffectTrigger.OnCastStart => AbilityVisualEffectTrigger.OnCastStart,
                AbilityVisualEffectTrigger.OnWarmup => AbilityVisualEffectTrigger.OnWarmup,
                AbilityVisualEffectTrigger.OnCastFinish => AbilityVisualEffectTrigger.OnCastFinish,
                AbilityVisualEffectTrigger.OnTargetApply => AbilityVisualEffectTrigger.OnTargetApply,
                AbilityVisualEffectTrigger.OnDurationTick => AbilityVisualEffectTrigger.OnDurationTick,
                AbilityVisualEffectTrigger.OnExpire => AbilityVisualEffectTrigger.OnExpire,
                _ => AbilityVisualEffectTrigger.OnTargetApply
            };
            delayTicks = AbilityEditorNormalizationUtility.ClampInt(delayTicks, 0, 60000);
            displayDurationTicks = AbilityEditorNormalizationUtility.ClampInt(displayDurationTicks, 1, 60000);
            linkedExpressionDurationTicks = AbilityEditorNormalizationUtility.ClampInt(linkedExpressionDurationTicks, 1, 60000);
            scale = AbilityEditorNormalizationUtility.ClampFloat(scale, 0.1f, 5f);
            drawSize = AbilityEditorNormalizationUtility.ClampFloat(drawSize, 0.1f, 20f);
            heightOffset = AbilityEditorNormalizationUtility.ClampFloat(heightOffset, -10f, 10f);
            rotation = AbilityEditorNormalizationUtility.ClampFloat(rotation, -360f, 360f);
            textureScale = new Vector2(
                AbilityEditorNormalizationUtility.ClampFloat(textureScale.x, 0.1f, 20f),
                AbilityEditorNormalizationUtility.ClampFloat(textureScale.y, 0.1f, 20f));
            lineWidth = AbilityEditorNormalizationUtility.ClampFloat(lineWidth, 0.05f, 20f);
            wallHeight = AbilityEditorNormalizationUtility.ClampFloat(wallHeight, 0.05f, 30f);
            wallThickness = AbilityEditorNormalizationUtility.ClampFloat(wallThickness, 0.05f, 20f);
            segmentCount = AbilityEditorNormalizationUtility.ClampInt(segmentCount, 1, 512);
            segmentRevealIntervalTicks = AbilityEditorNormalizationUtility.ClampInt(segmentRevealIntervalTicks, 0, 60000);
            repeatCount = AbilityEditorNormalizationUtility.ClampInt(repeatCount, 1, 999);
            repeatIntervalTicks = AbilityEditorNormalizationUtility.ClampInt(repeatIntervalTicks, 0, 60000);
            linkedPupilBrightnessOffset = AbilityEditorNormalizationUtility.ClampFloat(linkedPupilBrightnessOffset, -2f, 2f);
            linkedPupilContrastOffset = AbilityEditorNormalizationUtility.ClampFloat(linkedPupilContrastOffset, -2f, 2f);
            soundDelayTicks = AbilityEditorNormalizationUtility.ClampInt(soundDelayTicks, 0, 60000);
            soundVolume = AbilityEditorNormalizationUtility.ClampFloat(soundVolume, 0f, 4f);
            soundPitch = AbilityEditorNormalizationUtility.ClampFloat(soundPitch, 0.25f, 3f);
            SyncLegacyFields();
        }

        public AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled)
            {
                return result;
            }

            if (RequiresTexturePath && string.IsNullOrWhiteSpace(customTexturePath))
            {
                result.AddError("CS_Ability_Validate_VfxTexturePathRequired".Translate());
            }

            if (type == AbilityVisualEffectType.LineTexture && lineWidth <= 0f)
            {
                result.AddError("CS_Ability_Validate_VfxLineWidthPositive".Translate());
            }

            if (type == AbilityVisualEffectType.WallTexture)
            {
                if (wallHeight <= 0f)
                {
                    result.AddError("CS_Ability_Validate_VfxWallHeightPositive".Translate());
                }

                if (wallThickness <= 0f)
                {
                    result.AddError("CS_Ability_Validate_VfxWallThicknessPositive".Translate());
                }
            }

            if (segmentCount <= 0)
            {
                result.AddError("CS_Ability_Validate_VfxSegmentCountPositive".Translate());
            }

            if (segmentRevealIntervalTicks < 0)
            {
                result.AddError("CS_Ability_Validate_VfxSegmentRevealNonNegative".Translate());
            }

            if (repeatCount <= 0)
            {
                result.AddError("CS_Ability_Validate_VfxRepeatCountPositive".Translate());
            }

            if (displayDurationTicks <= 0)
            {
                result.AddError("CS_Ability_Validate_VfxDurationPositive".Translate());
            }

            return result;
        }
    }

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
        public float amount = 0f;
        public float duration = 0f;
        public float chance = 1f;
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
        public bool canHurtSelf = false;

        public AbilityEffectConfig Clone()
        {
            return (AbilityEffectConfig)MemberwiseClone();
        }

        public void NormalizeForSave()
        {
            duration = AbilityEditorNormalizationUtility.ClampFloat(duration, 0f, 999f);
            chance = AbilityEditorNormalizationUtility.ClampFloat(chance, 0.01f, 1f);
            summonCount = AbilityEditorNormalizationUtility.ClampInt(summonCount, 1, 99);
            controlMoveDistance = AbilityEditorNormalizationUtility.ClampInt(controlMoveDistance, 1, 99);
            terraformSpawnCount = AbilityEditorNormalizationUtility.ClampInt(terraformSpawnCount, 1, 999);
        }

        public AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (amount < 0)
                result.AddWarning("CS_Ability_Validate_EffectNegativeAmount".Translate());
            if (chance <= 0 || chance > 1)
                result.AddError("CS_Ability_Validate_EffectChanceRange".Translate(chance));

            switch (type)
            {
                case AbilityEffectType.Damage:
                    if (amount <= 0)
                        result.AddError("CS_Ability_Validate_DamageAmount".Translate());
                    break;
                case AbilityEffectType.Buff:
                case AbilityEffectType.Debuff:
                    if (hediffDef == null)
                        result.AddError("CS_Ability_Validate_HediffRequired".Translate(($"CS_Ability_EffectType_{type}").Translate()));
                    break;
                case AbilityEffectType.Summon:
                    if (summonKind == null)
                        result.AddError("CS_Ability_Validate_SummonKindRequired".Translate());
                    if (summonCount <= 0)
                        result.AddError("CS_Ability_Validate_SummonCount".Translate());
                    break;
                case AbilityEffectType.Control:
                    if (controlMode != ControlEffectMode.Stun && controlMoveDistance <= 0)
                        result.AddError("CS_Ability_Validate_ControlMoveDistance".Translate());
                    if (duration < 0f)
                        result.AddError("CS_Ability_Validate_ControlDuration".Translate());
                    break;
                case AbilityEffectType.Terraform:
                    switch (terraformMode)
                    {
                        case TerraformEffectMode.SpawnThing:
                            if (terraformThingDef == null)
                                result.AddError("CS_Ability_Validate_TerraformThingRequired".Translate());
                            if (terraformSpawnCount <= 0)
                                result.AddError("CS_Ability_Validate_TerraformSpawnCount".Translate());
                            break;
                        case TerraformEffectMode.ReplaceTerrain:
                            if (terraformTerrainDef == null)
                                result.AddError("CS_Ability_Validate_TerraformTerrainRequired".Translate());
                            break;
                    }
                    break;
            }

            return result;
        }
    }

    internal static class AbilityEditorNormalizationUtility
    {
        public static int ClampInt(int value, int min, int max) => Mathf.Clamp(value, min, max);
        public static float ClampFloat(float value, float min, float max) => Mathf.Clamp(value, min, max);
        public static string TrimOrEmpty(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string nonNullValue = value ?? string.Empty;
            return nonNullValue.Trim();
        }
    }

    public static class ModularAbilityDefExtensions
    {
        public static AbilityValidationResult Validate(this ModularAbilityDef ability)
        {
            var result = new AbilityValidationResult();
            if (string.IsNullOrEmpty(ability.defName))
                result.AddError("CS_Ability_Validate_DefNameEmpty".Translate());
            else if (!IsValidDefName(ability.defName))
                result.AddError("CS_Ability_Validate_DefNameInvalid".Translate());
            if (string.IsNullOrEmpty(ability.label))
                result.AddWarning("CS_Ability_Validate_LabelEmpty".Translate());
            if (ability.cooldownTicks < 0)
                result.AddError("CS_Ability_Validate_CooldownNegative".Translate());
            if (ability.warmupTicks < 0)
                result.AddError("CS_Ability_Validate_WarmupNegative".Translate());
            if (ability.charges <= 0)
                result.AddWarning("CS_Ability_Validate_ChargesNonPositive".Translate());
            if (ability.range < 0)
                result.AddError("CS_Ability_Validate_RangeNegative".Translate());
            if (ability.radius < 0)
                result.AddError("CS_Ability_Validate_RadiusNegative".Translate());

            AbilityCarrierType normalizedCarrier = NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = NormalizeTargetType(ability);
            AbilityAreaShape normalizedShape = NormalizeAreaShape(ability);
            if (CarrierNeedsRange(normalizedCarrier, normalizedTarget) && ability.range <= 0)
                result.AddWarning("CS_Ability_Validate_CarrierNeedsRange".Translate(GetCarrierTypeLabel(normalizedCarrier)));
            if (ability.useRadius && normalizedShape != AbilityAreaShape.Irregular && ability.radius <= 0)
                result.AddError("CS_Ability_Validate_AreaRadiusPositive".Translate());
            if (ability.useRadius && normalizedShape == AbilityAreaShape.Irregular && string.IsNullOrWhiteSpace(ability.irregularAreaPattern))
                result.AddError("CS_Ability_Validate_IrregularPatternRequired".Translate());
            if (normalizedCarrier == AbilityCarrierType.Projectile && ability.projectileDef == null)
                result.AddWarning("CS_Ability_Validate_ProjectileDefMissing".Translate());

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
                            result.AddError("CS_Ability_Validate_EffectIndexError".Translate(i + 1, error));
                    }
                    foreach (var warning in effectResult.Warnings)
                        result.AddWarning("CS_Ability_Validate_EffectIndexWarning".Translate(i + 1, warning));
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
                    string componentLabel = GetRuntimeComponentTypeLabel(component.type);
                    var compResult = component.Validate();
                    if (!compResult.IsValid)
                    {
                        foreach (var error in compResult.Errors)
                            result.AddError("CS_Ability_Validate_RuntimeComponentError".Translate(i + 1, componentLabel, error));
                    }
                    foreach (var warning in compResult.Warnings)
                        result.AddWarning("CS_Ability_Validate_RuntimeComponentWarning".Translate(i + 1, componentLabel, warning));
                }

                AddRuntimeComponentConflictWarnings(ability.runtimeComponents, result);
            }

            if (ability.visualEffects != null)
            {
                for (int i = 0; i < ability.visualEffects.Count; i++)
                {
                    var vfx = ability.visualEffects[i];
                    if (vfx == null)
                    {
                        result.AddWarning("CS_Ability_Validate_VfxNull".Translate(i + 1));
                        continue;
                    }

                    var vfxResult = vfx.Validate();
                    if (!vfxResult.IsValid)
                    {
                        foreach (var error in vfxResult.Errors)
                        {
                            result.AddError("CS_Ability_Validate_VfxIndexError".Translate(i + 1, error));
                        }
                    }

                    foreach (var warning in vfxResult.Warnings)
                    {
                        result.AddWarning("CS_Ability_Validate_VfxIndexWarning".Translate(i + 1, warning));
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
                return AbilityTargetType.Self;
            if (ability.targetType != AbilityTargetType.Self || ability.useRadius || ability.projectileDef != null)
                return ability.targetType;
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
                return AbilityAreaCenter.Target;
            if (ability.areaCenter != AbilityAreaCenter.Target || ability.targetType != AbilityTargetType.Self)
                return ability.areaCenter;
            return ability.carrierType == AbilityCarrierType.Area ? AbilityAreaCenter.Target : AbilityAreaCenter.Self;
        }

        public static AbilityAreaShape NormalizeAreaShape(ModularAbilityDef ability)
        {
            if (ability == null || !ability.useRadius)
                return AbilityAreaShape.Circle;
            return ability.areaShape;
        }

        public static bool CarrierNeedsRange(AbilityCarrierType carrierType, AbilityTargetType targetType)
        {
            carrierType = NormalizeCarrierType(carrierType);
            return carrierType == AbilityCarrierType.Projectile || carrierType == AbilityCarrierType.Target && targetType != AbilityTargetType.Self;
        }

        private static string GetCarrierTypeLabel(AbilityCarrierType type)
        {
            return ($"CS_Ability_CarrierType_{NormalizeCarrierType(type)}").Translate();
        }

        private static string GetRuntimeComponentTypeLabel(AbilityRuntimeComponentType type)
        {
            return ($"CS_Ability_RuntimeComponentType_{type}").Translate();
        }

        private static bool IsValidDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            foreach (char c in defName)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            return true;
        }

        private static void AddRuntimeComponentConflictWarnings(List<AbilityRuntimeComponentConfig> runtimeComponents, AbilityValidationResult result)
        {
            List<(AbilityRuntimeComponentConfig component, int index)> enabledComponents = runtimeComponents
                .Select((component, index) => new { component, index })
                .Where(entry => entry.component != null && entry.component.enabled)
                .Select(entry => (entry.component!, entry.index))
                .ToList();
            if (enabledComponents.Count == 0)
            {
                return;
            }

            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.QComboWindow);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.HotkeyOverride);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.FollowupCooldownGate);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.RStackDetonation);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.KillRefresh);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.PeriodicPulse);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.ShieldAbsorb);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.MarkDetonation);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.ComboStacks);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.DashEmpoweredStrike);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.FlightState);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.VanillaPawnFlyer);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.FlightOnlyFollowup);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.FlightLandingBurst);

            List<(AbilityRuntimeComponentConfig component, int index)> movementComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.SmartJump || entry.component.type == AbilityRuntimeComponentType.EShortJump)
                .ToList();
            if (movementComponents.Count > 1)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_MovementDuplicate".Translate(FormatRuntimeComponentEntries(movementComponents)));
            }

            List<(AbilityRuntimeComponentConfig component, int index)> vanillaPawnFlyerComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.VanillaPawnFlyer)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> flightStateComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.FlightState)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> flightOnlyFollowupComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.FlightOnlyFollowup)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> flightLandingBurstComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.FlightLandingBurst)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> dashEmpoweredStrikeComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.DashEmpoweredStrike)
                .ToList();
            bool hasMovement = movementComponents.Count > 0;

            if (dashEmpoweredStrikeComponents.Count > 0 && !hasMovement)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_DashEmpowerNeedsMovement".Translate(FormatRuntimeComponentEntries(dashEmpoweredStrikeComponents)));
            }

            if (flightStateComponents.Count > 0 && vanillaPawnFlyerComponents.Count > 0)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_FlightStateWithVanillaFlyer".Translate(
                    FormatRuntimeComponentEntries(flightStateComponents),
                    FormatRuntimeComponentEntries(vanillaPawnFlyerComponents)));
            }

            if (flightOnlyFollowupComponents.Count > 0 && vanillaPawnFlyerComponents.Count == 0)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_FlightOnlyNeedsVanillaFlyer".Translate(
                    FormatRuntimeComponentEntries(flightOnlyFollowupComponents)));
            }

            if (flightLandingBurstComponents.Count > 0 && vanillaPawnFlyerComponents.Count == 0)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_LandingBurstNeedsVanillaFlyer".Translate(
                    FormatRuntimeComponentEntries(flightLandingBurstComponents)));
            }
        }

        private static string FormatRuntimeComponentEntries(IEnumerable<(AbilityRuntimeComponentConfig component, int index)> components)
        {
            return string.Join(", ", components.Select(entry => $"#{entry.index + 1} {GetRuntimeComponentTypeLabel(entry.component.type)}"));
        }

        private static void WarnForDuplicateSingleton(AbilityValidationResult result, IEnumerable<(AbilityRuntimeComponentConfig component, int index)> enabledComponents, AbilityRuntimeComponentType type)
        {
            List<(AbilityRuntimeComponentConfig component, int index)> duplicates = enabledComponents
                .Where(entry => entry.component.type == type)
                .ToList();
            if (duplicates.Count > 1)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_DuplicateSingleton".Translate(
                    GetRuntimeComponentTypeLabel(type),
                    duplicates.Count,
                    FormatRuntimeComponentEntries(duplicates)));
            }
        }
    }
}
