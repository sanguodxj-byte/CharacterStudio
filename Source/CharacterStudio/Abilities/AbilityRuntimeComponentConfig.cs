// ─────────────────────────────────────────────
// 运行时组件配置（热键覆写、护盾、飞行、弹跳等）
// 从 ModularAbilityDef.cs 提取
// ─────────────────────────────────────────────

using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    public class AbilityRuntimeComponentConfig
    {
        public AbilityRuntimeComponentType type;
        public bool enabled = true;

        public int comboWindowTicks = 12;
        public AbilityRuntimeHotkeySlot comboTargetHotkeySlot = AbilityRuntimeHotkeySlot.W;
        public string comboTargetAbilityDefName = string.Empty;
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
        [EditorField("CS_Studio_Runtime_ShieldMaxDamage", AbilityRuntimeComponentType.ShieldAbsorb, Min = 1f, Max = 99999f)]
        public float shieldMaxDamage = 120f;
        [EditorField("CS_Studio_Runtime_Duration", AbilityRuntimeComponentType.ShieldAbsorb, AbilityRuntimeComponentType.AttachedShieldVisual, Min = 1f, Max = 99999f)]
        public float shieldDurationTicks = 240f;
        [EditorField("CS_Studio_Runtime_ShieldHealRatio", AbilityRuntimeComponentType.ShieldAbsorb, Min = 0f, Max = 10f)]
        public float shieldHealRatio = 0.5f;
        [EditorField("CS_Studio_Runtime_ShieldBonusDamageRatio", AbilityRuntimeComponentType.ShieldAbsorb, Min = 0f, Max = 10f)]
        public float shieldBonusDamageRatio = 0.25f;
        [EditorField("CS_Studio_Runtime_ShieldVisualScale", AbilityRuntimeComponentType.AttachedShieldVisual, AbilityRuntimeComponentType.ProjectileInterceptorShield, Min = 0.1f, Max = 10f)]
        public float shieldVisualScale = 1f;
        [EditorField("CS_Studio_Runtime_ShieldVisualHeightOffset", AbilityRuntimeComponentType.AttachedShieldVisual, Min = -5f, Max = 5f)]
        public float shieldVisualHeightOffset = 0f;
        [EditorField("CS_Studio_Runtime_ShieldInterceptorThingDefName", AbilityRuntimeComponentType.ProjectileInterceptorShield)]
        public string shieldInterceptorThingDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_Duration", AbilityRuntimeComponentType.ProjectileInterceptorShield, Min = 1f, Max = 99999f)]
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

        [EditorField("CS_Studio_Effect_WeatherDef", AbilityRuntimeComponentType.WeatherChange)]
        public string weatherDefName = string.Empty;
        [EditorField("CS_Studio_Effect_WeatherDuration", AbilityRuntimeComponentType.WeatherChange, Min = 1f, Max = 9999999f)]
        public int weatherDurationTicks = 60000;
        [EditorField("CS_Studio_Effect_WeatherTransition", AbilityRuntimeComponentType.WeatherChange, Min = 0f, Max = 99999f)]
        public int weatherTransitionTicks = 3000;

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
                case AbilityRuntimeComponentType.SlotOverrideWindow:
                    if (comboWindowTicks <= 0)
                        result.AddError("CS_Ability_Validate_QComboWindowTicks".Translate());
                    if (string.IsNullOrWhiteSpace(comboTargetAbilityDefName))
                        result.AddError("CS_Ability_Validate_HotkeyOverrideAbilityDefName".Translate());
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
}
