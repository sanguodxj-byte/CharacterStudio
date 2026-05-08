namespace CharacterStudio.Abilities
{
    public class TeleportEffectBehavior : IEffectBehavior
    {
        public EffectTargetCategory Category => EffectTargetCategory.PrimaryCell;

        public int GetExpandedRowCount(AbilityEffectConfig config) => 0;

        public void NormalizeForSave(AbilityEffectConfig config) { }

        public void Validate(AbilityEffectConfig config, AbilityValidationResult result) { }

        public void SetDefaults(AbilityEffectConfig config) { }
    }
}
