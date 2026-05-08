using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    public class DamageEffectBehavior : IEffectBehavior
    {
        public EffectTargetCategory Category => EffectTargetCategory.EntityDirected;

        public int GetExpandedRowCount(AbilityEffectConfig config) => 2; // DamageDef + CanHurtSelf

        public void NormalizeForSave(AbilityEffectConfig config) { }

        public void Validate(AbilityEffectConfig config, AbilityValidationResult result)
        {
            if (config.amount <= 0)
                result.AddError("CS_Ability_Validate_DamageAmount".Translate());
        }

        public void SetDefaults(AbilityEffectConfig config)
        {
            config.amount = 10f;
            config.damageDef = DamageDefOf.Blunt;
        }
    }
}
