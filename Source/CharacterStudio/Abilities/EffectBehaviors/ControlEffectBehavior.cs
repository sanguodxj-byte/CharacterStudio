using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.Abilities
{
    public class ControlEffectBehavior : IEffectBehavior
    {
        public EffectTargetCategory Category => EffectTargetCategory.EntityDirected;

        public int GetExpandedRowCount(AbilityEffectConfig config)
        {
            return config.controlMode != ControlEffectMode.Stun ? 1 : 0;
        }

        public void NormalizeForSave(AbilityEffectConfig config)
        {
            config.controlMoveDistance = AbilityEditorNormalizationUtility.ClampInt(config.controlMoveDistance, 1, 99);
        }

        public void Validate(AbilityEffectConfig config, AbilityValidationResult result)
        {
            if (config.controlMode != ControlEffectMode.Stun && config.controlMoveDistance <= 0)
                result.AddError("CS_Ability_Validate_ControlMoveDistance".Translate());
            if (config.duration < 0f)
                result.AddError("CS_Ability_Validate_ControlDuration".Translate());
        }

        public void SetDefaults(AbilityEffectConfig config)
        {
            config.duration = 3f;
        }
    }
}
