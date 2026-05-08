using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.Abilities
{
    public class SummonEffectBehavior : IEffectBehavior
    {
        public EffectTargetCategory Category => EffectTargetCategory.PrimaryCell;

        public int GetExpandedRowCount(AbilityEffectConfig config)
        {
            int rows = 2; // SummonKind+Count + SummonFaction
            if (config.summonFactionType == SummonFactionType.FixedDef)
                rows++;
            return rows;
        }

        public void NormalizeForSave(AbilityEffectConfig config)
        {
            config.summonCount = AbilityEditorNormalizationUtility.ClampInt(config.summonCount, 1, 99);
            if (config.summonFactionDef != null)
            {
                config.summonFactionDefName = config.summonFactionDef.defName;
            }
        }

        public void Validate(AbilityEffectConfig config, AbilityValidationResult result)
        {
            if (config.summonKind == null)
                result.AddError("CS_Ability_Validate_SummonKindRequired".Translate());
            if (config.summonCount <= 0)
                result.AddError("CS_Ability_Validate_SummonCount".Translate());
        }

        public void SetDefaults(AbilityEffectConfig config)
        {
            config.summonCount = 1;
        }
    }
}
