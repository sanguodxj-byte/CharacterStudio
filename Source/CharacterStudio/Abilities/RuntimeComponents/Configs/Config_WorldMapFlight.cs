using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_WorldMapFlight : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.WorldMapFlight;
        public override float EditorBlockHeight => 300f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            worldMapTakeoffEffecterDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(worldMapTakeoffEffecterDefName);
            worldMapTakeoffSoundDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(worldMapTakeoffSoundDefName);
            worldMapFlightMessageKey = AbilityEditorNormalizationUtility.TrimOrEmpty(worldMapFlightMessageKey);
            if (worldMapMaxLaunchDistance < 0)
                worldMapMaxLaunchDistance = 0;
            if (worldMapTravelDurationTicks < 30)
                worldMapTravelDurationTicks = 30;
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;

            if (worldMapTravelDurationTicks < 30)
                result.AddWarning("CS_Ability_Validate_WorldMapTravelDurationTooShort".Translate());

            return result;
        }
    }
}
