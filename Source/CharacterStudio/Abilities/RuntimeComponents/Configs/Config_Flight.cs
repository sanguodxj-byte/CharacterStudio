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


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref flightDurationTicks, "flightDurationTicks", 180);
            Scribe_Values.Look(ref flightHeightFactor, "flightHeightFactor", 0.35f);
            Scribe_Values.Look(ref suppressCombatActionsDuringFlightState, "suppressCombatActionsDuringFlightState", true);
            Scribe_Values.Look(ref flyerThingDefName, "flyerThingDefName", string.Empty);
            Scribe_Values.Look(ref flyerWarmupTicks, "flyerWarmupTicks", 0);
            Scribe_Values.Look(ref launchFromCasterPosition, "launchFromCasterPosition", true);
            Scribe_Values.Look(ref requireValidTargetCell, "requireValidTargetCell", true);
            Scribe_Values.Look(ref enableFlightOnlyWindow, "enableFlightOnlyWindow", false);
            Scribe_Values.Look(ref flightOnlyWindowTicks, "flightOnlyWindowTicks", 180);
            Scribe_Values.Look(ref flightOnlyAbilityDefName, "flightOnlyAbilityDefName", string.Empty);
            Scribe_Values.Look(ref hideCasterDuringTakeoff, "hideCasterDuringTakeoff", true);
            Scribe_Values.Look(ref autoExpireFlightMarkerOnLanding, "autoExpireFlightMarkerOnLanding", true);
        }

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


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref requiredFlightSourceAbilityDefName, "requiredFlightSourceAbilityDefName", string.Empty);
            Scribe_Values.Look(ref requireReservedTargetCell, "requireReservedTargetCell", false);
            Scribe_Values.Look(ref consumeFlightStateOnCast, "consumeFlightStateOnCast", false);
            Scribe_Values.Look(ref onlyUseDuringFlightWindow, "onlyUseDuringFlightWindow", true);
        }

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


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref landingBurstRadius, "landingBurstRadius", 3f);
            Scribe_Values.Look(ref landingBurstDamage, "landingBurstDamage", 30f);
            Scribe_Defs.Look(ref landingBurstDamageDef, "landingBurstDamageDef");
            Scribe_Values.Look(ref landingEffecterDefName, "landingEffecterDefName", string.Empty);
            Scribe_Values.Look(ref landingSoundDefName, "landingSoundDefName", string.Empty);
            Scribe_Values.Look(ref affectBuildings, "affectBuildings", false);
            Scribe_Values.Look(ref affectCells, "affectCells", true);
            Scribe_Values.Look(ref knockbackTargets, "knockbackTargets", false);
            Scribe_Values.Look(ref knockbackDistance, "knockbackDistance", 1.5f);
        }

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
