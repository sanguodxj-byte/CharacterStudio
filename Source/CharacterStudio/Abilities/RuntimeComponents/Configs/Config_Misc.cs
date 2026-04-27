using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_PeriodicPulse : AbilityRuntimeComponentConfig
    {
        [EditorField("CS_Studio_Runtime_PulseIntervalTicks", AbilityRuntimeComponentType.PeriodicPulse)]
        public new int pulseIntervalTicks = 60;
        [EditorField("CS_Studio_Runtime_PulseTotalTicks", AbilityRuntimeComponentType.PeriodicPulse)]
        public new int pulseTotalTicks = 240;
        [EditorField("CS_Studio_Runtime_PulseStartsImmediately", AbilityRuntimeComponentType.PeriodicPulse)]
        public new bool pulseStartsImmediately = true;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.PeriodicPulse;
        public override float EditorBlockHeight => 112f;
        public override bool IsSingleton => true;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pulseIntervalTicks, "pulseIntervalTicks", 60);
            Scribe_Values.Look(ref pulseTotalTicks, "pulseTotalTicks", 240);
            Scribe_Values.Look(ref pulseStartsImmediately, "pulseStartsImmediately", true);
        }

        public override void NormalizeForSave()
        {
            pulseIntervalTicks = AbilityEditorNormalizationUtility.ClampInt(pulseIntervalTicks, 1, 99999);
            pulseTotalTicks = AbilityEditorNormalizationUtility.ClampInt(pulseTotalTicks, 1, 99999);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (pulseIntervalTicks <= 0) result.AddError("CS_Ability_Validate_PeriodicPulseIntervalTicks".Translate());
            if (pulseTotalTicks <= 0) result.AddError("CS_Ability_Validate_PeriodicPulseTotalTicks".Translate());
            return result;
        }
    }

    public class Config_ProjectileSplit : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.ProjectileSplit;
        public override float EditorBlockHeight => 138f;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref splitProjectileCount, "splitProjectileCount", 2);
            Scribe_Values.Look(ref splitDamageScale, "splitDamageScale", 0.5f);
            Scribe_Values.Look(ref splitSearchRange, "splitSearchRange", 5f);
        }

        public override void NormalizeForSave()
        {
            splitProjectileCount = AbilityEditorNormalizationUtility.ClampInt(splitProjectileCount, 1, 99);
            splitDamageScale = AbilityEditorNormalizationUtility.ClampFloat(splitDamageScale, 0.01f, 10f);
            splitSearchRange = AbilityEditorNormalizationUtility.ClampFloat(splitSearchRange, 0.1f, 99f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (splitProjectileCount <= 0) result.AddError("CS_Ability_Validate_SplitProjectileCount".Translate());
            if (splitDamageScale <= 0f) result.AddError("CS_Ability_Validate_SplitDamageScale".Translate());
            if (splitSearchRange <= 0f) result.AddError("CS_Ability_Validate_SplitSearchRange".Translate());
            return result;
        }
    }

    public class Config_TimeStop : AbilityRuntimeComponentConfig
    {
        [EditorField("CS_Studio_Runtime_TimeStopDurationTicks", AbilityRuntimeComponentType.TimeStop)]
        public new int timeStopDurationTicks = 60;
        [EditorField("CS_Studio_Runtime_FreezeVisualsDuringTimeStop", AbilityRuntimeComponentType.TimeStop)]
        public new bool freezeVisualsDuringTimeStop = true;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.TimeStop;
        public override float EditorBlockHeight => 86f;
        public override bool IsSingleton => true;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref timeStopDurationTicks, "timeStopDurationTicks", 60);
            Scribe_Values.Look(ref freezeVisualsDuringTimeStop, "freezeVisualsDuringTimeStop", true);
        }

        public override void NormalizeForSave()
        {
            timeStopDurationTicks = AbilityEditorNormalizationUtility.ClampInt(timeStopDurationTicks, 1, 99999);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (timeStopDurationTicks <= 0) result.AddError("CS_Ability_Validate_TimeStopDurationTicks".Translate());
            return result;
        }

    }

    public class Config_WeatherChange : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.WeatherChange;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref weatherDefName, "weatherDefName", string.Empty);
            Scribe_Values.Look(ref weatherDurationTicks, "weatherDurationTicks", 60000);
            Scribe_Values.Look(ref weatherTransitionTicks, "weatherTransitionTicks", 3000);
        }

        public override void NormalizeForSave()
        {
            weatherDurationTicks = AbilityEditorNormalizationUtility.ClampInt(weatherDurationTicks, 1, int.MaxValue);
            weatherTransitionTicks = AbilityEditorNormalizationUtility.ClampInt(weatherTransitionTicks, 0, 99999);
        }

        public override AbilityValidationResult Validate()
        {
            return new AbilityValidationResult();
        }

    }

    /// <summary>
    /// BezierCurveWall 的配置子类。
    /// 所有运行时字段已在基类 AbilityRuntimeComponentConfig 中定义和序列化，
    /// 此处仅提供类型标识、编辑器尺寸和验证逻辑。
    /// </summary>
    public class Config_BezierCurveWall : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.BezierCurveWall;
        public override float EditorBlockHeight => 246f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            bezierWallDurationTicks = AbilityEditorNormalizationUtility.ClampInt(bezierWallDurationTicks, 1, 99999);
            bezierWallThickness = AbilityEditorNormalizationUtility.ClampFloat(bezierWallThickness, 0.1f, 5f);
            bezierWallControlPointHeight = AbilityEditorNormalizationUtility.ClampFloat(bezierWallControlPointHeight, -20f, 20f);
            bezierWallSegmentCount = AbilityEditorNormalizationUtility.ClampInt(bezierWallSegmentCount, 4, 64);
            bezierWallCurveDirection = bezierWallCurveDirection >= 0 ? 1 : -1;
            bezierWallAbsorbMax = AbilityEditorNormalizationUtility.ClampFloat(bezierWallAbsorbMax, 1f, 99999f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (bezierWallDurationTicks <= 0) result.AddError("CS_Ability_Validate_BezierWallDuration".Translate());
            if (bezierWallThickness <= 0f) result.AddError("CS_Ability_Validate_BezierWallThickness".Translate());
            if (bezierWallSegmentCount <= 0) result.AddError("CS_Ability_Validate_BezierWallSegments".Translate());
            if (bezierWallAbsorbMax <= 0f) result.AddError("CS_Ability_Validate_BezierWallAbsorbMax".Translate());
            return result;
        }

    }
}
