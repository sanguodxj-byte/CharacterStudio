// ─────────────────────────────────────────────
// 技能效果配置（伤害、治疗、Buff、召唤等）
// 从 ModularAbilityDef.cs 提取
// ─────────────────────────────────────────────

using System;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    public class AbilityEffectConfig
    {
        public AbilityEffectType type;
        public float amount = 0f;
        public float duration = 0f;
        public float chance = 1f;
        public DamageDef? damageDef;
        public HediffDef? hediffDef;
        public PawnKindDef? summonKind;
        public int summonCount = 1;
        public SummonFactionType summonFactionType = SummonFactionType.Player;
        public FactionDef? summonFactionDef;
        public string summonFactionDefName = string.Empty;

        public ControlEffectMode controlMode = ControlEffectMode.Stun;
        public int controlMoveDistance = 3;
        public TerraformEffectMode terraformMode = TerraformEffectMode.CleanFilth;
        public ThingDef? terraformThingDef;
        public TerrainDef? terraformTerrainDef;
        public int terraformSpawnCount = 1;
        public bool canHurtSelf = false;

        public string weatherDefName = string.Empty; // Added for WeatherChange
        public int weatherDurationTicks = 60000; // Added for WeatherChange (default 1 day)
        public int weatherTransitionTicks = 3000; // Added for WeatherChange (default 0.5 hour)

        public AbilityEffectConfig Clone()
        {
            return (AbilityEffectConfig)MemberwiseClone();
        }

        public void NormalizeForSave()
        {
            duration = AbilityEditorNormalizationUtility.ClampFloat(duration, 0f, 999f);
            chance = AbilityEditorNormalizationUtility.ClampFloat(chance, 0.01f, 1f);
            summonCount = AbilityEditorNormalizationUtility.ClampInt(summonCount, 1, 99);
            controlMoveDistance = AbilityEditorNormalizationUtility.ClampInt(controlMoveDistance, 1, 99);
            terraformSpawnCount = AbilityEditorNormalizationUtility.ClampInt(terraformSpawnCount, 1, 999);

            if (summonFactionDef != null)
            {
                summonFactionDefName = summonFactionDef.defName;
            }

            // Added for WeatherChange
            weatherDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(weatherDefName);
            weatherDurationTicks = AbilityEditorNormalizationUtility.ClampInt(weatherDurationTicks, 1, 9999999);
            weatherTransitionTicks = AbilityEditorNormalizationUtility.ClampInt(weatherTransitionTicks, 0, 99999);
        }

        public AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (amount < 0)
                result.AddWarning("CS_Ability_Validate_EffectNegativeAmount".Translate());
            if (chance <= 0 || chance > 1)
                result.AddError("CS_Ability_Validate_EffectChanceRange".Translate(chance));

            switch (type)
            {
                case AbilityEffectType.Damage:
                    if (amount <= 0)
                        result.AddError("CS_Ability_Validate_DamageAmount".Translate());
                    break;
                case AbilityEffectType.Buff:
                case AbilityEffectType.Debuff:
                    if (hediffDef == null)
                        result.AddError("CS_Ability_Validate_HediffRequired".Translate(($"CS_Ability_EffectType_{type}").Translate()));
                    break;
                case AbilityEffectType.Summon:
                    if (summonKind == null)
                        result.AddError("CS_Ability_Validate_SummonKindRequired".Translate());
                    if (summonCount <= 0)
                        result.AddError("CS_Ability_Validate_SummonCount".Translate());
                    break;
                case AbilityEffectType.Control:
                    if (controlMode != ControlEffectMode.Stun && controlMoveDistance <= 0)
                        result.AddError("CS_Ability_Validate_ControlMoveDistance".Translate());
                    if (duration < 0f)
                        result.AddError("CS_Ability_Validate_ControlDuration".Translate());
                    break;
                case AbilityEffectType.Terraform:
                    switch (terraformMode)
                    {
                        case TerraformEffectMode.SpawnThing:
                            if (terraformThingDef == null)
                                result.AddError("CS_Ability_Validate_TerraformThingRequired".Translate());
                            if (terraformSpawnCount <= 0)
                                result.AddError("CS_Ability_Validate_TerraformSpawnCount".Translate());
                            break;
                        case TerraformEffectMode.ReplaceTerrain:
                            if (terraformTerrainDef == null)
                                result.AddError("CS_Ability_Validate_TerraformTerrainRequired".Translate());
                            break;
                    }
                    break;
                case AbilityEffectType.WeatherChange: // Added validation for WeatherChange
                    if (string.IsNullOrWhiteSpace(weatherDefName))
                        result.AddError("CS_Ability_Validate_WeatherDefNameRequired".Translate());
                    if (weatherDurationTicks <= 0)
                        result.AddError("CS_Ability_Validate_WeatherDurationPositive".Translate());
                    if (weatherTransitionTicks < 0)
                        result.AddError("CS_Ability_Validate_WeatherTransitionNonNegative".Translate());
                    break;
            }

            return result;
        }
    }
}
