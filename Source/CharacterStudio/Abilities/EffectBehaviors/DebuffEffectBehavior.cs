using Verse;

namespace CharacterStudio.Abilities
{
    public class DebuffEffectBehavior : IEffectBehavior
    {
        public EffectTargetCategory Category => EffectTargetCategory.EntityDirected;

        public int GetExpandedRowCount(AbilityEffectConfig config) => 0;

        public void NormalizeForSave(AbilityEffectConfig config) { }

        public void Validate(AbilityEffectConfig config, AbilityValidationResult result)
        {
            if (config.hediffDef == null)
                result.AddError("CS_Ability_Validate_HediffRequired".Translate(("CS_Ability_EffectType_Debuff").Translate()));
        }

        public void SetDefaults(AbilityEffectConfig config)
        {
            config.duration = 10f;
        }
    }
}
