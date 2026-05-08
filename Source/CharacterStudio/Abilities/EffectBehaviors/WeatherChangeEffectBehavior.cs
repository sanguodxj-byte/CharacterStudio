using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.Abilities
{
    public class WeatherChangeEffectBehavior : IEffectBehavior
    {
        public EffectTargetCategory Category => EffectTargetCategory.PrimaryCell;

        public int GetExpandedRowCount(AbilityEffectConfig config) => 2; // WeatherDef + Duration/Transition

        public void NormalizeForSave(AbilityEffectConfig config)
        {
            config.weatherDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(config.weatherDefName);
            config.weatherDurationTicks = AbilityEditorNormalizationUtility.ClampInt(config.weatherDurationTicks, 1, 9999999);
            config.weatherTransitionTicks = AbilityEditorNormalizationUtility.ClampInt(config.weatherTransitionTicks, 0, 99999);
        }

        public void Validate(AbilityEffectConfig config, AbilityValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(config.weatherDefName))
                result.AddError("CS_Ability_Validate_WeatherDefNameRequired".Translate());
            if (config.weatherDurationTicks <= 0)
                result.AddError("CS_Ability_Validate_WeatherDurationPositive".Translate());
            if (config.weatherTransitionTicks < 0)
                result.AddError("CS_Ability_Validate_WeatherTransitionNonNegative".Translate());
        }

        public void SetDefaults(AbilityEffectConfig config)
        {
            config.weatherDurationTicks = 60000;
            config.weatherTransitionTicks = 3000;
        }
    }
}
