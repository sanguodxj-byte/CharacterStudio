using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_FlightState : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.FlightState;
        public override float EditorBlockHeight => 112f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            flyerThingDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(flyerThingDefName);
            flightOnlyAbilityDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(flightOnlyAbilityDefName);
            flightDurationTicks = AbilityEditorNormalizationUtility.ClampInt(flightDurationTicks, 1, 99999);
            flightHeightFactor = AbilityEditorNormalizationUtility.ClampFloat(flightHeightFactor, 0f, 5f);
            flyerWarmupTicks = AbilityEditorNormalizationUtility.ClampInt(flyerWarmupTicks, 0, 99999);
            flightOnlyWindowTicks = AbilityEditorNormalizationUtility.ClampInt(flightOnlyWindowTicks, 1, 99999);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (flightDurationTicks <= 0) result.AddError("CS_Ability_Validate_FlightDurationTicks".Translate());
            if (flightHeightFactor < 0f) result.AddError("CS_Ability_Validate_FlightHeightFactor".Translate());
            if (flightHeightFactor > 5f) result.AddWarning("CS_Ability_Validate_FlightHeightFactorWarning".Translate());
            return result;
        }
    }

    public class Config_FlightOnlyFollowup : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.FlightOnlyFollowup;
        public override float EditorBlockHeight => 216f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            requiredFlightSourceAbilityDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(requiredFlightSourceAbilityDefName);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (onlyUseDuringFlightWindow && string.IsNullOrWhiteSpace(requiredFlightSourceAbilityDefName))
                result.AddWarning("CS_Ability_Validate_FlightOnlyFollowupSourceRecommended".Translate());
            if (requireReservedTargetCell && !onlyUseDuringFlightWindow)
                result.AddWarning("CS_Ability_Validate_FlightOnlyFollowupReservedTargetRecommended".Translate());
            return result;
        }
    }

    public class Config_FlightLandingBurst : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.FlightLandingBurst;
        public override float EditorBlockHeight => 242f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            landingEffecterDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(landingEffecterDefName);
            landingSoundDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(landingSoundDefName);
            landingBurstRadius = AbilityEditorNormalizationUtility.ClampFloat(landingBurstRadius, 0.1f, 99f);
            landingBurstDamage = AbilityEditorNormalizationUtility.ClampFloat(landingBurstDamage, 0.01f, 99999f);
            knockbackDistance = AbilityEditorNormalizationUtility.ClampFloat(knockbackDistance, 0f, 99f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (landingBurstRadius <= 0f) result.AddError("CS_Ability_Validate_LandingBurstRadius".Translate());
            if (landingBurstDamage <= 0f) result.AddError("CS_Ability_Validate_LandingBurstDamage".Translate());
            return result;
        }
    }
}
