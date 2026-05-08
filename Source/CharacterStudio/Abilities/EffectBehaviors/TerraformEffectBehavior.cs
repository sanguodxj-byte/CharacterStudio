using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.Abilities
{
    public class TerraformEffectBehavior : IEffectBehavior
    {
        public EffectTargetCategory Category => EffectTargetCategory.AreaCell;

        public int GetExpandedRowCount(AbilityEffectConfig config)
        {
            int rows = 1; // TerraformMode
            if (config.terraformMode == TerraformEffectMode.SpawnThing)
                rows += 2; // TerraformThing + SpawnCount
            else if (config.terraformMode == TerraformEffectMode.ReplaceTerrain)
                rows += 1; // TerraformTerrain
            return rows;
        }

        public void NormalizeForSave(AbilityEffectConfig config)
        {
            config.terraformSpawnCount = AbilityEditorNormalizationUtility.ClampInt(config.terraformSpawnCount, 1, 999);
        }

        public void Validate(AbilityEffectConfig config, AbilityValidationResult result)
        {
            switch (config.terraformMode)
            {
                case TerraformEffectMode.SpawnThing:
                    if (config.terraformThingDef == null)
                        result.AddError("CS_Ability_Validate_TerraformThingRequired".Translate());
                    if (config.terraformSpawnCount <= 0)
                        result.AddError("CS_Ability_Validate_TerraformSpawnCount".Translate());
                    break;
                case TerraformEffectMode.ReplaceTerrain:
                    if (config.terraformTerrainDef == null)
                        result.AddError("CS_Ability_Validate_TerraformTerrainRequired".Translate());
                    break;
            }
        }

        public void SetDefaults(AbilityEffectConfig config) { }
    }
}
