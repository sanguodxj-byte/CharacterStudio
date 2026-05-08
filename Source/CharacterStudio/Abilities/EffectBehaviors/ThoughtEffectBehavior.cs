using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    public class ThoughtEffectBehavior : IEffectBehavior
    {
        public EffectTargetCategory Category => EffectTargetCategory.EntityDirected;

        public int GetExpandedRowCount(AbilityEffectConfig config) => 5;
        // Label + Description + MoodOffset+DurationDays + StackLimit+ShowBubble + IconPath

        public void NormalizeForSave(AbilityEffectConfig config)
        {
            config.thoughtLabel = AbilityEditorNormalizationUtility.TrimOrEmpty(config.thoughtLabel);
            config.thoughtDescription = AbilityEditorNormalizationUtility.TrimOrEmpty(config.thoughtDescription);
            config.thoughtDurationDays = AbilityEditorNormalizationUtility.ClampFloat(config.thoughtDurationDays, 0.01f, 999f);
            config.thoughtStackLimit = AbilityEditorNormalizationUtility.ClampInt(config.thoughtStackLimit, 1, 100);
            config.thoughtIconPath = AbilityEditorNormalizationUtility.TrimOrEmpty(config.thoughtIconPath);
        }

        public void Validate(AbilityEffectConfig config, AbilityValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(config.thoughtLabel))
                result.AddError("CS_Ability_Validate_ThoughtLabelRequired".Translate());
            if (Mathf.Approximately(config.thoughtMoodOffset, 0f))
                result.AddWarning("CS_Ability_Validate_ThoughtMoodZero".Translate());
        }

        public void SetDefaults(AbilityEffectConfig config)
        {
            config.thoughtMoodOffset = 5f;
            config.thoughtDurationDays = 1f;
            config.thoughtStackLimit = 1;
            config.thoughtShowBubble = true;
        }
    }
}
