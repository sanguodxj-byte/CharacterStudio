namespace CharacterStudio.Abilities
{
    public class HealEffectBehavior : IEffectBehavior
    {
        public EffectTargetCategory Category => EffectTargetCategory.EntityDirected;

        public int GetExpandedRowCount(AbilityEffectConfig config) => 0;

        public void NormalizeForSave(AbilityEffectConfig config) { }

        public void Validate(AbilityEffectConfig config, AbilityValidationResult result) { }

        public void SetDefaults(AbilityEffectConfig config)
        {
            config.amount = 8f;
        }
    }
}
