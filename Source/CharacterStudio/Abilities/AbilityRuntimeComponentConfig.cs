using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using CharacterStudio.Core;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 运行时组件配置基类
    /// 为兼容现有的 UI 和逻辑代码，保留所有字段。
    /// 子类应只提供类型声明和特有验证，不应重新定义字段以免发生 CS0108。
    /// </summary>
    public class AbilityRuntimeComponentConfig : IExposable
    {
        private static readonly Dictionary<string, FieldInfo> XmlFieldMap = typeof(AbilityRuntimeComponentConfig)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(static field => !field.IsStatic)
            .ToDictionary(static field => field.Name, StringComparer.OrdinalIgnoreCase);

        private AbilityRuntimeComponentType loadedTypeValue = AbilityRuntimeComponentType.SlotOverrideWindow;
        private bool loadedTypeInitialized;

        public virtual AbilityRuntimeComponentType type => loadedTypeInitialized ? loadedTypeValue : AbilityRuntimeComponentType.SlotOverrideWindow;
        public virtual float EditorBlockHeight { get; } = 100f;
        public virtual bool IsSingleton { get; } = false;

    [EditorField("CS_Studio_Runtime_Enabled", AbilityRuntimeComponentType.SlotOverrideWindow)]
    public bool enabled = true;

    // SlotOverride
    [EditorField("CS_Studio_Runtime_ComboWindowTicks", AbilityRuntimeComponentType.SlotOverrideWindow)]
    public int comboWindowTicks = 12;
    [EditorField("CS_Studio_Runtime_ComboTargetHotkeySlot", AbilityRuntimeComponentType.SlotOverrideWindow)]
    public AbilityRuntimeHotkeySlot comboTargetHotkeySlot = AbilityRuntimeHotkeySlot.None;
    [EditorField("CS_Studio_Runtime_ComboTargetAbilityDefName", AbilityRuntimeComponentType.SlotOverrideWindow)]
    public string comboTargetAbilityDefName = string.Empty;

    // HotkeyOverride
    [EditorField("CS_Studio_Runtime_OverrideHotkeySlot", AbilityRuntimeComponentType.HotkeyOverride)]
    public AbilityRuntimeHotkeySlot overrideHotkeySlot = AbilityRuntimeHotkeySlot.None;
    [EditorField("CS_Studio_Runtime_OverrideAbilityDefName", AbilityRuntimeComponentType.HotkeyOverride)]
    public string overrideAbilityDefName = string.Empty;
    [EditorField("CS_Studio_Runtime_OverrideDurationTicks", AbilityRuntimeComponentType.HotkeyOverride)]
    public int overrideDurationTicks = 60;

    // CooldownGate
    [EditorField("CS_Studio_Runtime_FollowupCooldownHotkeySlot", AbilityRuntimeComponentType.FollowupCooldownGate)]
    public AbilityRuntimeHotkeySlot followupCooldownHotkeySlot = AbilityRuntimeHotkeySlot.None;
    [EditorField("CS_Studio_Runtime_FollowupCooldownTicks", AbilityRuntimeComponentType.FollowupCooldownGate)]
    public int followupCooldownTicks = 60;
    [EditorField("CS_Studio_Runtime_MaxComboFollowupDelayTicks", AbilityRuntimeComponentType.FollowupCooldownGate, Min = -1f, Max = 9999f)]
    public int maxComboFollowupDelayTicks = 180;
    [EditorField("CS_Studio_Runtime_CooldownPunishScale", AbilityRuntimeComponentType.FollowupCooldownGate, Min = 0.5f, Max = 50f)]
    public float cooldownPunishScale = 1.0f;

        // Jump / SmartJump
        [EditorField("CS_Studio_Runtime_CooldownTicks", AbilityRuntimeComponentType.SmartJump)]
        public int cooldownTicks = 120;
        [EditorField("CS_Studio_Runtime_JumpDistance", AbilityRuntimeComponentType.SmartJump)]
        public int jumpDistance = 6;
        [EditorField("CS_Studio_Runtime_FindCellRadius", AbilityRuntimeComponentType.SmartJump)]
        public int findCellRadius = 3;
        [EditorField("CS_Studio_Runtime_TriggerEffectsAfterJump", AbilityRuntimeComponentType.SmartJump)]
        public bool triggerAbilityEffectsAfterJump = true;
        [EditorField("CS_Studio_Runtime_UseMouseTargetCell", AbilityRuntimeComponentType.SmartJump)]
        public bool useMouseTargetCell = true;
        [EditorField("CS_Studio_Runtime_SmartCastOffsetCells", AbilityRuntimeComponentType.SmartJump)]
        public int smartCastOffsetCells = 1;
        [EditorField("CS_Studio_Runtime_SmartCastClampToMaxDistance", AbilityRuntimeComponentType.SmartJump)]
        public bool smartCastClampToMaxDistance = true;
        [EditorField("CS_Studio_Runtime_SmartCastAllowFallbackForward", AbilityRuntimeComponentType.SmartJump)]
        public bool smartCastAllowFallbackForward = true;

        // RStack
        [EditorField("CS_Studio_Runtime_RequiredStacks", AbilityRuntimeComponentType.RStackDetonation)]
        public int requiredStacks = 7;
        [EditorField("CS_Studio_Runtime_DelayTicks", AbilityRuntimeComponentType.RStackDetonation)]
        public int delayTicks = 180;
        [EditorField("CS_Studio_Runtime_Wave1Radius", AbilityRuntimeComponentType.RStackDetonation)]
        public float wave1Radius = 3f;
        [EditorField("CS_Studio_Runtime_Wave1Damage", AbilityRuntimeComponentType.RStackDetonation)]
        public float wave1Damage = 80f;
        [EditorField("CS_Studio_Runtime_Wave2Radius", AbilityRuntimeComponentType.RStackDetonation)]
        public float wave2Radius = 6f;
        [EditorField("CS_Studio_Runtime_Wave2Damage", AbilityRuntimeComponentType.RStackDetonation)]
        public float wave2Damage = 140f;
        [EditorField("CS_Studio_Runtime_Wave3Radius", AbilityRuntimeComponentType.RStackDetonation)]
        public float wave3Radius = 9f;
        [EditorField("CS_Studio_Runtime_Wave3Damage", AbilityRuntimeComponentType.RStackDetonation)]
        public float wave3Damage = 220f;
        [EditorField("CS_Studio_Runtime_WaveDamageDef", AbilityRuntimeComponentType.RStackDetonation)]
        public DamageDef? waveDamageDef;

        // Pulse
        [EditorField("CS_Studio_Runtime_PulseIntervalTicks", AbilityRuntimeComponentType.PeriodicPulse)]
        public int pulseIntervalTicks = 60;
        [EditorField("CS_Studio_Runtime_PulseTotalTicks", AbilityRuntimeComponentType.PeriodicPulse)]
        public int pulseTotalTicks = 240;
        [EditorField("CS_Studio_Runtime_PulseStartsImmediately", AbilityRuntimeComponentType.PeriodicPulse)]
        public bool pulseStartsImmediately = true;

        // KillRefresh
        [EditorField("CS_Studio_Runtime_KillRefreshHotkeySlot", AbilityRuntimeComponentType.HitCooldownRefund)]
        public AbilityRuntimeHotkeySlot killRefreshHotkeySlot = AbilityRuntimeHotkeySlot.None;
        [EditorField("CS_Studio_Runtime_KillRefreshCooldownPercent", AbilityRuntimeComponentType.HitCooldownRefund)]
        public float killRefreshCooldownPercent = 1f;

        // Shield
        [EditorField("CS_Studio_Runtime_ShieldMaxDamage", AbilityRuntimeComponentType.ShieldAbsorb)]
        public float shieldMaxDamage = 120f;
        [EditorField("CS_Studio_Runtime_ShieldDurationTicks", AbilityRuntimeComponentType.ShieldAbsorb)]
        public float shieldDurationTicks = 240f;
        [EditorField("CS_Studio_Runtime_ShieldHealRatio", AbilityRuntimeComponentType.ShieldAbsorb)]
        public float shieldHealRatio = 0.5f;
        [EditorField("CS_Studio_Runtime_ShieldBonusDamageRatio", AbilityRuntimeComponentType.ShieldAbsorb)]
        public float shieldBonusDamageRatio = 0.25f;
        [EditorField("CS_Studio_Runtime_ShieldVisualScale", AbilityRuntimeComponentType.AttachedShieldVisual)]
        public float shieldVisualScale = 1f;
        [EditorField("CS_Studio_Runtime_ShieldVisualHeightOffset", AbilityRuntimeComponentType.AttachedShieldVisual)]
        public float shieldVisualHeightOffset = 0f;
        [EditorField("CS_Studio_Runtime_ShieldInterceptorThingDefName", AbilityRuntimeComponentType.ProjectileInterceptorShield)]
        public string shieldInterceptorThingDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_ShieldInterceptorDurationTicks", AbilityRuntimeComponentType.ProjectileInterceptorShield)]
        public int shieldInterceptorDurationTicks = 240;

        // ChainBounce
        [EditorField("CS_Studio_Runtime_MaxBounceCount", AbilityRuntimeComponentType.ChainBounce)]
        public int maxBounceCount = 4;
        [EditorField("CS_Studio_Runtime_BounceRange", AbilityRuntimeComponentType.ChainBounce)]
        public float bounceRange = 6f;
        [EditorField("CS_Studio_Runtime_BounceDamageFalloff", AbilityRuntimeComponentType.ChainBounce)]
        public float bounceDamageFalloff = 0.2f;

        // Execute / Health Bonus
        [EditorField("CS_Studio_Runtime_ExecuteThresholdPercent", AbilityRuntimeComponentType.RStackDetonation)]
        public float executeThresholdPercent = 0.3f;
        [EditorField("CS_Studio_Runtime_ExecuteBonusDamageScale", AbilityRuntimeComponentType.RStackDetonation)]
        public float executeBonusDamageScale = 0.5f;
        [EditorField("CS_Studio_Runtime_FullHealthThresholdPercent", AbilityRuntimeComponentType.RStackDetonation)]
        public float fullHealthThresholdPercent = 0.95f;
        [EditorField("CS_Studio_Runtime_FullHealthBonusDamageScale", AbilityRuntimeComponentType.RStackDetonation)]
        public float fullHealthBonusDamageScale = 0.35f;
        [EditorField("CS_Studio_Runtime_MissingHealthBonusPerTenPercent", AbilityRuntimeComponentType.RStackDetonation)]
        public float missingHealthBonusPerTenPercent = 0.05f;
        [EditorField("CS_Studio_Runtime_MissingHealthBonusMaxScale", AbilityRuntimeComponentType.RStackDetonation)]
        public float missingHealthBonusMaxScale = 0.5f;

        // Nearby / Isolated
        [EditorField("CS_Studio_Runtime_NearbyEnemyBonusMaxTargets", AbilityRuntimeComponentType.RStackDetonation)]
        public int nearbyEnemyBonusMaxTargets = 4;
        [EditorField("CS_Studio_Runtime_NearbyEnemyBonusPerTarget", AbilityRuntimeComponentType.RStackDetonation)]
        public float nearbyEnemyBonusPerTarget = 0.08f;
        [EditorField("CS_Studio_Runtime_NearbyEnemyBonusRadius", AbilityRuntimeComponentType.RStackDetonation)]
        public float nearbyEnemyBonusRadius = 5f;
        [EditorField("CS_Studio_Runtime_IsolatedTargetRadius", AbilityRuntimeComponentType.RStackDetonation)]
        public float isolatedTargetRadius = 4f;
        [EditorField("CS_Studio_Runtime_IsolatedTargetBonusDamageScale", AbilityRuntimeComponentType.RStackDetonation)]
        public float isolatedTargetBonusDamageScale = 0.35f;

        // Mark
        [EditorField("CS_Studio_Runtime_MarkDurationTicks", AbilityRuntimeComponentType.MarkDetonation)]
        public int markDurationTicks = 180;
        [EditorField("CS_Studio_Runtime_MarkMaxStacks", AbilityRuntimeComponentType.MarkDetonation)]
        public int markMaxStacks = 3;
        [EditorField("CS_Studio_Runtime_MarkDetonationDamage", AbilityRuntimeComponentType.MarkDetonation)]
        public float markDetonationDamage = 40f;
        [EditorField("CS_Studio_Runtime_MarkDamageDef", AbilityRuntimeComponentType.MarkDetonation)]
        public DamageDef? markDamageDef;

        // Combo
        [EditorField("CS_Studio_Runtime_ComboStackWindowTicks", AbilityRuntimeComponentType.ComboStacks)]
        public int comboStackWindowTicks = 180;
        [EditorField("CS_Studio_Runtime_ComboStackMax", AbilityRuntimeComponentType.ComboStacks)]
        public int comboStackMax = 5;
        [EditorField("CS_Studio_Runtime_ComboStackBonusDamagePerStack", AbilityRuntimeComponentType.ComboStacks)]
        public float comboStackBonusDamagePerStack = 0.06f;

        // Slow / Pierce
        [EditorField("CS_Studio_Runtime_SlowFieldDurationTicks", AbilityRuntimeComponentType.HitSlowField)]
        public int slowFieldDurationTicks = 180;
        [EditorField("CS_Studio_Runtime_SlowFieldRadius", AbilityRuntimeComponentType.HitSlowField)]
        public float slowFieldRadius = 2.5f;
        [EditorField("CS_Studio_Runtime_SlowFieldHediffDefName", AbilityRuntimeComponentType.HitSlowField)]
        public string slowFieldHediffDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_PierceMaxTargets", AbilityRuntimeComponentType.ChainBounce)]
        public int pierceMaxTargets = 3;
        [EditorField("CS_Studio_Runtime_PierceBonusDamagePerTarget", AbilityRuntimeComponentType.ChainBounce)]
        public float pierceBonusDamagePerTarget = 0.15f;
        [EditorField("CS_Studio_Runtime_PierceSearchRange", AbilityRuntimeComponentType.ChainBounce)]
        public float pierceSearchRange = 5f;

        // DashEmpower / Heal / Refund
        [EditorField("CS_Studio_Runtime_DashEmpowerDurationTicks", AbilityRuntimeComponentType.DashEmpoweredStrike)]
        public int dashEmpowerDurationTicks = 180;
        [EditorField("CS_Studio_Runtime_DashEmpowerBonusDamageScale", AbilityRuntimeComponentType.DashEmpoweredStrike)]
        public float dashEmpowerBonusDamageScale = 0.5f;
        [EditorField("CS_Studio_Runtime_HitHealAmount", AbilityRuntimeComponentType.HitHeal)]
        public float hitHealAmount = 8f;
        [EditorField("CS_Studio_Runtime_HitHealRatio", AbilityRuntimeComponentType.HitHeal)]
        public float hitHealRatio = 0f;
        [EditorField("CS_Studio_Runtime_RefundHotkeySlot", AbilityRuntimeComponentType.HitCooldownRefund)]
        public AbilityRuntimeHotkeySlot refundHotkeySlot = AbilityRuntimeHotkeySlot.None;
        [EditorField("CS_Studio_Runtime_HitCooldownRefundPercent", AbilityRuntimeComponentType.HitCooldownRefund)]
        public float hitCooldownRefundPercent = 0.15f;

        // Projectile / Flight
        [EditorField("CS_Studio_Runtime_SplitProjectileCount", AbilityRuntimeComponentType.ProjectileSplit)]
        public int splitProjectileCount = 2;
        [EditorField("CS_Studio_Runtime_SplitDamageScale", AbilityRuntimeComponentType.ProjectileSplit)]
        public float splitDamageScale = 0.5f;
        [EditorField("CS_Studio_Runtime_SplitSearchRange", AbilityRuntimeComponentType.ProjectileSplit)]
        public float splitSearchRange = 5f;
        [EditorField("CS_Studio_Runtime_FlightDurationTicks", AbilityRuntimeComponentType.FlightState)]
        public int flightDurationTicks = 180;
        [EditorField("CS_Studio_Runtime_FlightHeightFactor", AbilityRuntimeComponentType.FlightState)]
        public float flightHeightFactor = 0.35f;
        [EditorField("CS_Studio_Runtime_SuppressCombatActionsDuringFlightState", AbilityRuntimeComponentType.FlightState)]
        public bool suppressCombatActionsDuringFlightState = true;

        // Flyer / Landing
        [EditorField("CS_Studio_Runtime_FlyerThingDefName", AbilityRuntimeComponentType.VanillaPawnFlyer)]
        public string flyerThingDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_FlyerWarmupTicks", AbilityRuntimeComponentType.VanillaPawnFlyer)]
        public int flyerWarmupTicks = 0;
        [EditorField("CS_Studio_Runtime_LaunchFromCasterPosition", AbilityRuntimeComponentType.VanillaPawnFlyer)]
        public bool launchFromCasterPosition = true;
        [EditorField("CS_Studio_Runtime_RequireValidTargetCell", AbilityRuntimeComponentType.VanillaPawnFlyer)]
        public bool requireValidTargetCell = true;
        [EditorField("CS_Studio_Runtime_StoreTargetForFollowup", AbilityRuntimeComponentType.VanillaPawnFlyer)]
        public bool storeTargetForFollowup = true;
        [EditorField("CS_Studio_Runtime_EnableFlightOnlyWindow", AbilityRuntimeComponentType.FlightOnlyFollowup)]
        public bool enableFlightOnlyWindow = false;
        [EditorField("CS_Studio_Runtime_FlightOnlyWindowTicks", AbilityRuntimeComponentType.FlightOnlyFollowup)]
        public int flightOnlyWindowTicks = 180;
        [EditorField("CS_Studio_Runtime_FlightOnlyAbilityDefName", AbilityRuntimeComponentType.FlightOnlyFollowup)]
        public string flightOnlyAbilityDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_HideCasterDuringTakeoff", AbilityRuntimeComponentType.VanillaPawnFlyer)]
        public bool hideCasterDuringTakeoff = true;
        [EditorField("CS_Studio_Runtime_AutoExpireFlightMarkerOnLanding", AbilityRuntimeComponentType.FlightLandingBurst)]
        public bool autoExpireFlightMarkerOnLanding = true;
        [EditorField("CS_Studio_Runtime_RequiredFlightSourceAbilityDefName", AbilityRuntimeComponentType.VanillaPawnFlyer)]
        public string requiredFlightSourceAbilityDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_RequireReservedTargetCell", AbilityRuntimeComponentType.VanillaPawnFlyer)]
        public bool requireReservedTargetCell = false;
        [EditorField("CS_Studio_Runtime_ConsumeFlightStateOnCast", AbilityRuntimeComponentType.VanillaPawnFlyer)]
        public bool consumeFlightStateOnCast = false;
        [EditorField("CS_Studio_Runtime_OnlyUseDuringFlightWindow", AbilityRuntimeComponentType.FlightOnlyFollowup)]
        public bool onlyUseDuringFlightWindow = true;

        [EditorField("CS_Studio_Runtime_LandingBurstRadius", AbilityRuntimeComponentType.FlightLandingBurst)]
        public float landingBurstRadius = 3f;
        [EditorField("CS_Studio_Runtime_LandingBurstDamage", AbilityRuntimeComponentType.FlightLandingBurst)]
        public float landingBurstDamage = 30f;
        [EditorField("CS_Studio_Runtime_LandingBurstDamageDef", AbilityRuntimeComponentType.FlightLandingBurst)]
        public DamageDef? landingBurstDamageDef;
        [EditorField("CS_Studio_Runtime_LandingEffecterDefName", AbilityRuntimeComponentType.FlightLandingBurst)]
        public string landingEffecterDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_LandingSoundDefName", AbilityRuntimeComponentType.FlightLandingBurst)]
        public string landingSoundDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_AffectBuildings", AbilityRuntimeComponentType.FlightLandingBurst)]
        public bool affectBuildings = false;
        [EditorField("CS_Studio_Runtime_AffectCells", AbilityRuntimeComponentType.FlightLandingBurst)]
        public bool affectCells = true;
        [EditorField("CS_Studio_Runtime_KnockbackTargets", AbilityRuntimeComponentType.FlightLandingBurst)]
        public bool knockbackTargets = false;
        [EditorField("CS_Studio_Runtime_KnockbackDistance", AbilityRuntimeComponentType.FlightLandingBurst)]
        public float knockbackDistance = 1.5f;

        // TimeStop / Dash
        [EditorField("CS_Studio_Runtime_TimeStopDurationTicks", AbilityRuntimeComponentType.TimeStop)]
        public int timeStopDurationTicks = 60;
        [EditorField("CS_Studio_Runtime_FreezeVisualsDuringTimeStop", AbilityRuntimeComponentType.TimeStop)]
        public bool freezeVisualsDuringTimeStop = true;
        [EditorField("CS_Studio_Runtime_DashDistance", AbilityRuntimeComponentType.Dash)]
        public int dashDistance = 6;
        [EditorField("CS_Studio_Runtime_DashStepDurationTicks", AbilityRuntimeComponentType.Dash)]
        public int dashStepDurationTicks = 3;
        [EditorField("CS_Studio_Runtime_DashEffectTiming", AbilityRuntimeComponentType.Dash)]
        public DashEffectTiming dashEffectTiming = DashEffectTiming.OnCollisionStop;
        [EditorField("CS_Studio_Runtime_DashUseAbilityRange", AbilityRuntimeComponentType.Dash)]
        public bool dashUseAbilityRange = false;
        [EditorField("CS_Studio_Runtime_DashLanding", AbilityRuntimeComponentType.Dash)]
        public bool dashLanding = false;
        [EditorField("CS_Studio_Runtime_DashSweepAcrossPath", AbilityRuntimeComponentType.Dash)]
        public bool dashSweepAcrossPath = false;

        // Tags
        [EditorField("CS_Studio_Runtime_TriggerEquipmentAnimationOnApply", AbilityRuntimeComponentType.DashEmpoweredStrike)]
        public bool triggerEquipmentAnimationOnApply = false;
        [EditorField("CS_Studio_Runtime_EquipmentAnimationTriggerKey", AbilityRuntimeComponentType.DashEmpoweredStrike)]
        public string equipmentAnimationTriggerKey = "Dash";
        [EditorField("CS_Studio_Runtime_EquipmentAnimationDurationTicks", AbilityRuntimeComponentType.DashEmpoweredStrike)]
        public int equipmentAnimationDurationTicks = 30;

        // Weather / Bezier
        [EditorField("CS_Studio_Runtime_WeatherDefName", AbilityRuntimeComponentType.WeatherChange)]
        public string weatherDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_WeatherDurationTicks", AbilityRuntimeComponentType.WeatherChange)]
        public int weatherDurationTicks = 60000;
        [EditorField("CS_Studio_Runtime_WeatherTransitionTicks", AbilityRuntimeComponentType.WeatherChange)]
        public int weatherTransitionTicks = 3000;
        [EditorField("CS_Studio_Runtime_BezierWallDurationTicks", AbilityRuntimeComponentType.BezierCurveWall)]
        public int bezierWallDurationTicks = 300;
        [EditorField("CS_Studio_Runtime_BezierWallThickness", AbilityRuntimeComponentType.BezierCurveWall)]
        public float bezierWallThickness = 0.5f;
        [EditorField("CS_Studio_Runtime_BezierWallControlPointHeight", AbilityRuntimeComponentType.BezierCurveWall)]
        public float bezierWallControlPointHeight = 3f;
        [EditorField("CS_Studio_Runtime_BezierWallSegmentCount", AbilityRuntimeComponentType.BezierCurveWall)]
        public int bezierWallSegmentCount = 16;
        [EditorField("CS_Studio_Runtime_BezierWallBlockFriendly", AbilityRuntimeComponentType.BezierCurveWall)]
        public bool bezierWallBlockFriendly = false;
        [EditorField("CS_Studio_Runtime_BezierWallCurveDirection", AbilityRuntimeComponentType.BezierCurveWall)]
        public int bezierWallCurveDirection = 1;
        [EditorField("CS_Studio_Runtime_BezierWallAbsorbMax", AbilityRuntimeComponentType.BezierCurveWall)]
        public float bezierWallAbsorbMax = 200f;
        [EditorField("CS_Studio_Runtime_BezierWallCustomTexture", AbilityRuntimeComponentType.BezierCurveWall)]
        public string bezierWallCustomTexture = string.Empty;
        [EditorField("CS_Studio_Runtime_BezierWallReflectsProjectiles", AbilityRuntimeComponentType.BezierCurveWall)]
        public bool bezierWallReflectsProjectiles = false;

        public virtual void ExposeData()
        {
            AbilityRuntimeComponentType tempType = type;
            Scribe_Values.Look(ref tempType, "type");
            loadedTypeValue = tempType;
            loadedTypeInitialized = true;
            Scribe_Values.Look(ref enabled, "enabled", true);

            Scribe_Values.Look(ref comboWindowTicks, "comboWindowTicks", 12);
            Scribe_Values.Look(ref comboTargetHotkeySlot, "comboTargetHotkeySlot", AbilityRuntimeHotkeySlot.None);
            Scribe_Values.Look(ref comboTargetAbilityDefName, "comboTargetAbilityDefName", string.Empty);
            Scribe_Values.Look(ref overrideHotkeySlot, "overrideHotkeySlot", AbilityRuntimeHotkeySlot.None);
            Scribe_Values.Look(ref overrideAbilityDefName, "overrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref overrideDurationTicks, "overrideDurationTicks", 60);
            Scribe_Values.Look(ref followupCooldownHotkeySlot, "followupCooldownHotkeySlot", AbilityRuntimeHotkeySlot.None);
            Scribe_Values.Look(ref followupCooldownTicks, "followupCooldownTicks", 60);
            Scribe_Values.Look(ref maxComboFollowupDelayTicks, "maxComboFollowupDelayTicks", 180);
            Scribe_Values.Look(ref cooldownPunishScale, "cooldownPunishScale", 1.0f);

            Scribe_Values.Look(ref cooldownTicks, "cooldownTicks", 120);
            Scribe_Values.Look(ref jumpDistance, "jumpDistance", 6);
            Scribe_Values.Look(ref findCellRadius, "findCellRadius", 3);
            Scribe_Values.Look(ref triggerAbilityEffectsAfterJump, "triggerAbilityEffectsAfterJump", true);
            Scribe_Values.Look(ref useMouseTargetCell, "useMouseTargetCell", true);
            Scribe_Values.Look(ref smartCastOffsetCells, "smartCastOffsetCells", 1);
            Scribe_Values.Look(ref smartCastClampToMaxDistance, "smartCastClampToMaxDistance", true);
            Scribe_Values.Look(ref smartCastAllowFallbackForward, "smartCastAllowFallbackForward", true);

            Scribe_Values.Look(ref requiredStacks, "requiredStacks", 7);
            Scribe_Values.Look(ref delayTicks, "delayTicks", 180);
            Scribe_Values.Look(ref wave1Radius, "wave1Radius", 3f);
            Scribe_Values.Look(ref wave1Damage, "wave1Damage", 80f);
            Scribe_Values.Look(ref wave2Radius, "wave2Radius", 6f);
            Scribe_Values.Look(ref wave2Damage, "wave2Damage", 140f);
            Scribe_Values.Look(ref wave3Radius, "wave3Radius", 9f);
            Scribe_Values.Look(ref wave3Damage, "wave3Damage", 220f);
            Scribe_Defs.Look(ref waveDamageDef, "waveDamageDef");

            Scribe_Values.Look(ref pulseIntervalTicks, "pulseIntervalTicks", 60);
            Scribe_Values.Look(ref pulseTotalTicks, "pulseTotalTicks", 240);
            Scribe_Values.Look(ref pulseStartsImmediately, "pulseStartsImmediately", true);

            Scribe_Values.Look(ref killRefreshHotkeySlot, "killRefreshHotkeySlot", AbilityRuntimeHotkeySlot.None);
            Scribe_Values.Look(ref killRefreshCooldownPercent, "killRefreshCooldownPercent", 1f);

            Scribe_Values.Look(ref shieldMaxDamage, "shieldMaxDamage", 120f);
            Scribe_Values.Look(ref shieldDurationTicks, "shieldDurationTicks", 240f);
            Scribe_Values.Look(ref shieldHealRatio, "shieldHealRatio", 0.5f);
            Scribe_Values.Look(ref shieldBonusDamageRatio, "shieldBonusDamageRatio", 0.25f);
            Scribe_Values.Look(ref shieldVisualScale, "shieldVisualScale", 1f);
            Scribe_Values.Look(ref shieldVisualHeightOffset, "shieldVisualHeightOffset", 0f);
            Scribe_Values.Look(ref shieldInterceptorThingDefName, "shieldInterceptorThingDefName", string.Empty);
            Scribe_Values.Look(ref shieldInterceptorDurationTicks, "shieldInterceptorDurationTicks", 240);

            Scribe_Values.Look(ref maxBounceCount, "maxBounceCount", 4);
            Scribe_Values.Look(ref bounceRange, "bounceRange", 6f);
            Scribe_Values.Look(ref bounceDamageFalloff, "bounceDamageFalloff", 0.2f);

            Scribe_Values.Look(ref executeThresholdPercent, "executeThresholdPercent", 0.3f);
            Scribe_Values.Look(ref executeBonusDamageScale, "executeBonusDamageScale", 0.5f);
            Scribe_Values.Look(ref fullHealthThresholdPercent, "fullHealthThresholdPercent", 0.95f);
            Scribe_Values.Look(ref fullHealthBonusDamageScale, "fullHealthBonusDamageScale", 0.35f);
            Scribe_Values.Look(ref missingHealthBonusPerTenPercent, "missingHealthBonusPerTenPercent", 0.05f);
            Scribe_Values.Look(ref missingHealthBonusMaxScale, "missingHealthBonusMaxScale", 0.5f);

            Scribe_Values.Look(ref nearbyEnemyBonusMaxTargets, "nearbyEnemyBonusMaxTargets", 4);
            Scribe_Values.Look(ref nearbyEnemyBonusPerTarget, "nearbyEnemyBonusPerTarget", 0.08f);
            Scribe_Values.Look(ref nearbyEnemyBonusRadius, "nearbyEnemyBonusRadius", 5f);
            Scribe_Values.Look(ref isolatedTargetRadius, "isolatedTargetRadius", 4f);
            Scribe_Values.Look(ref isolatedTargetBonusDamageScale, "isolatedTargetBonusDamageScale", 0.35f);

            Scribe_Values.Look(ref markDurationTicks, "markDurationTicks", 180);
            Scribe_Values.Look(ref markMaxStacks, "markMaxStacks", 3);
            Scribe_Values.Look(ref markDetonationDamage, "markDetonationDamage", 40f);
            Scribe_Defs.Look(ref markDamageDef, "markDamageDef");

            Scribe_Values.Look(ref comboStackWindowTicks, "comboStackWindowTicks", 180);
            Scribe_Values.Look(ref comboStackMax, "comboStackMax", 5);
            Scribe_Values.Look(ref comboStackBonusDamagePerStack, "comboStackBonusDamagePerStack", 0.06f);

            Scribe_Values.Look(ref slowFieldDurationTicks, "slowFieldDurationTicks", 180);
            Scribe_Values.Look(ref slowFieldRadius, "slowFieldRadius", 2.5f);
            Scribe_Values.Look(ref slowFieldHediffDefName, "slowFieldHediffDefName", string.Empty);
            Scribe_Values.Look(ref pierceMaxTargets, "pierceMaxTargets", 3);
            Scribe_Values.Look(ref pierceBonusDamagePerTarget, "pierceBonusDamagePerTarget", 0.15f);
            Scribe_Values.Look(ref pierceSearchRange, "pierceSearchRange", 5f);

            Scribe_Values.Look(ref dashEmpowerDurationTicks, "dashEmpowerDurationTicks", 180);
            Scribe_Values.Look(ref dashEmpowerBonusDamageScale, "dashEmpowerBonusDamageScale", 0.5f);
            Scribe_Values.Look(ref hitHealAmount, "hitHealAmount", 8f);
            Scribe_Values.Look(ref hitHealRatio, "hitHealRatio", 0f);
            Scribe_Values.Look(ref refundHotkeySlot, "refundHotkeySlot", AbilityRuntimeHotkeySlot.None);
            Scribe_Values.Look(ref hitCooldownRefundPercent, "hitCooldownRefundPercent", 0.15f);

            Scribe_Values.Look(ref splitProjectileCount, "splitProjectileCount", 2);
            Scribe_Values.Look(ref splitDamageScale, "splitDamageScale", 0.5f);
            Scribe_Values.Look(ref splitSearchRange, "splitSearchRange", 5f);
            Scribe_Values.Look(ref flightDurationTicks, "flightDurationTicks", 180);
            Scribe_Values.Look(ref flightHeightFactor, "flightHeightFactor", 0.35f);
            Scribe_Values.Look(ref suppressCombatActionsDuringFlightState, "suppressCombatActionsDuringFlightState", true);

            Scribe_Values.Look(ref flyerThingDefName, "flyerThingDefName", string.Empty);
            Scribe_Values.Look(ref flyerWarmupTicks, "flyerWarmupTicks", 0);
            Scribe_Values.Look(ref launchFromCasterPosition, "launchFromCasterPosition", true);
            Scribe_Values.Look(ref requireValidTargetCell, "requireValidTargetCell", true);
            Scribe_Values.Look(ref storeTargetForFollowup, "storeTargetForFollowup", true);
            Scribe_Values.Look(ref enableFlightOnlyWindow, "enableFlightOnlyWindow", false);
            Scribe_Values.Look(ref flightOnlyWindowTicks, "flightOnlyWindowTicks", 180);
            Scribe_Values.Look(ref flightOnlyAbilityDefName, "flightOnlyAbilityDefName", string.Empty);
            Scribe_Values.Look(ref hideCasterDuringTakeoff, "hideCasterDuringTakeoff", true);
            Scribe_Values.Look(ref autoExpireFlightMarkerOnLanding, "autoExpireFlightMarkerOnLanding", true);
            Scribe_Values.Look(ref requiredFlightSourceAbilityDefName, "requiredFlightSourceAbilityDefName", string.Empty);
            Scribe_Values.Look(ref requireReservedTargetCell, "requireReservedTargetCell", false);
            Scribe_Values.Look(ref consumeFlightStateOnCast, "consumeFlightStateOnCast", false);
            Scribe_Values.Look(ref onlyUseDuringFlightWindow, "onlyUseDuringFlightWindow", true);

            Scribe_Values.Look(ref landingBurstRadius, "landingBurstRadius", 3f);
            Scribe_Values.Look(ref landingBurstDamage, "landingBurstDamage", 30f);
            Scribe_Defs.Look(ref landingBurstDamageDef, "landingBurstDamageDef");
            Scribe_Values.Look(ref landingEffecterDefName, "landingEffecterDefName", string.Empty);
            Scribe_Values.Look(ref landingSoundDefName, "landingSoundDefName", string.Empty);
            Scribe_Values.Look(ref affectBuildings, "affectBuildings", false);
            Scribe_Values.Look(ref affectCells, "affectCells", true);
            Scribe_Values.Look(ref knockbackTargets, "knockbackTargets", false);
            Scribe_Values.Look(ref knockbackDistance, "knockbackDistance", 1.5f);

            Scribe_Values.Look(ref timeStopDurationTicks, "timeStopDurationTicks", 60);
            Scribe_Values.Look(ref freezeVisualsDuringTimeStop, "freezeVisualsDuringTimeStop", true);
            Scribe_Values.Look(ref dashDistance, "dashDistance", 6);
            Scribe_Values.Look(ref dashStepDurationTicks, "dashStepDurationTicks", 3);
            Scribe_Values.Look(ref dashEffectTiming, "dashEffectTiming", DashEffectTiming.OnCollisionStop);
            Scribe_Values.Look(ref dashUseAbilityRange, "dashUseAbilityRange", false);
            Scribe_Values.Look(ref dashLanding, "dashLanding", false);
            Scribe_Values.Look(ref dashSweepAcrossPath, "dashSweepAcrossPath", false);

            Scribe_Values.Look(ref triggerEquipmentAnimationOnApply, "triggerEquipmentAnimationOnApply", false);
            Scribe_Values.Look(ref equipmentAnimationTriggerKey, "equipmentAnimationTriggerKey", "Dash");
            Scribe_Values.Look(ref equipmentAnimationDurationTicks, "equipmentAnimationDurationTicks", 30);

            Scribe_Values.Look(ref weatherDefName, "weatherDefName", string.Empty);
            Scribe_Values.Look(ref weatherDurationTicks, "weatherDurationTicks", 60000);
            Scribe_Values.Look(ref weatherTransitionTicks, "weatherTransitionTicks", 3000);
            Scribe_Values.Look(ref bezierWallDurationTicks, "bezierWallDurationTicks", 300);
            Scribe_Values.Look(ref bezierWallThickness, "bezierWallThickness", 0.5f);
            Scribe_Values.Look(ref bezierWallControlPointHeight, "bezierWallControlPointHeight", 3f);
            Scribe_Values.Look(ref bezierWallSegmentCount, "bezierWallSegmentCount", 16);
            Scribe_Values.Look(ref bezierWallBlockFriendly, "bezierWallBlockFriendly", false);
            Scribe_Values.Look(ref bezierWallCurveDirection, "bezierWallCurveDirection", 1);
            Scribe_Values.Look(ref bezierWallAbsorbMax, "bezierWallAbsorbMax", 200f);
            Scribe_Values.Look(ref bezierWallCustomTexture, "bezierWallCustomTexture", string.Empty);
            Scribe_Values.Look(ref bezierWallReflectsProjectiles, "bezierWallReflectsProjectiles", false);
        }

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            if (xmlRoot == null)
            {
                return;
            }

            foreach (XmlNode child in xmlRoot.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (string.Equals(child.Name, "type", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse(child.InnerText?.Trim(), true, out AbilityRuntimeComponentType parsedType))
                    {
                        loadedTypeValue = parsedType;
                        loadedTypeInitialized = true;
                    }

                    continue;
                }

                if (!XmlFieldMap.TryGetValue(child.Name, out FieldInfo? field))
                {
                    continue;
                }

                TryAssignFieldFromXml(field, child);
            }
        }

        private void TryAssignFieldFromXml(FieldInfo field, XmlNode child)
        {
            string rawValue = child.InnerText?.Trim() ?? string.Empty;
            if (field.FieldType == typeof(string))
            {
                field.SetValue(this, rawValue);
                return;
            }

            if (typeof(Def).IsAssignableFrom(field.FieldType))
            {
                if (!string.IsNullOrWhiteSpace(rawValue))
                {
                    DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, field.Name, rawValue);
                }

                return;
            }

            if (field.FieldType == typeof(AbilityRuntimeHotkeySlot))
            {
                if (TryParseHotkeySlot(rawValue, out AbilityRuntimeHotkeySlot slot))
                {
                    field.SetValue(this, slot);
                }

                return;
            }

            if (field.FieldType.IsEnum)
            {
                try
                {
                    object enumValue = Enum.Parse(field.FieldType, rawValue, true);
                    field.SetValue(this, enumValue);
                }
                catch
                {
                }

                return;
            }

            object? parsedValue = ParseHelper.FromString(rawValue, field.FieldType);
            if (parsedValue != null || !field.FieldType.IsValueType)
            {
                field.SetValue(this, parsedValue);
            }
        }

        private static bool TryParseHotkeySlot(string rawValue, out AbilityRuntimeHotkeySlot slot)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                slot = AbilityRuntimeHotkeySlot.None;
                return false;
            }

            if (!Enum.TryParse(rawValue.Trim(), true, out slot))
            {
                slot = AbilityRuntimeHotkeySlot.None;
                return false;
            }

            slot = AbilityHotkeySlotUtility.NormalizeSupportedRuntimeSlot(slot);
            return slot != AbilityRuntimeHotkeySlot.None;
        }

        public virtual AbilityRuntimeComponentConfig Clone()
        {
            return (AbilityRuntimeComponentConfig)MemberwiseClone();
        }

        public virtual void NormalizeForSave()
        {
        }
        
        public virtual AbilityValidationResult Validate()
        {
            return new AbilityValidationResult();
        }
        
        public virtual float DrawEditorUI(float x, float y, float width, float labelW, float valueW)
        {
            return 0f;
        }

        public virtual string GetPreviewSummary()
        {
            return string.Empty;
        }
    }
}
