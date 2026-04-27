using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_RStackDetonation : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.RStackDetonation;
        public override float EditorBlockHeight => 250f;

        public override void NormalizeForSave()
        {
            requiredStacks = AbilityEditorNormalizationUtility.ClampInt(requiredStacks, 1, 999);
            delayTicks = AbilityEditorNormalizationUtility.ClampInt(delayTicks, 0, 99999);
            wave1Radius = AbilityEditorNormalizationUtility.ClampFloat(wave1Radius, 0.1f, 99f);
            wave1Damage = AbilityEditorNormalizationUtility.ClampFloat(wave1Damage, 1f, 99999f);
            wave2Radius = AbilityEditorNormalizationUtility.ClampFloat(wave2Radius, 0.1f, 99f);
            wave2Damage = AbilityEditorNormalizationUtility.ClampFloat(wave2Damage, 1f, 99999f);
            wave3Radius = AbilityEditorNormalizationUtility.ClampFloat(wave3Radius, 0.1f, 99f);
            wave3Damage = AbilityEditorNormalizationUtility.ClampFloat(wave3Damage, 1f, 99999f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (requiredStacks <= 0) result.AddError("CS_Ability_Validate_RRequiredStacks".Translate());
            if (delayTicks < 0) result.AddError("CS_Ability_Validate_RDelayTicks".Translate());
            if (wave1Radius <= 0 || wave2Radius <= 0 || wave3Radius <= 0) result.AddError("CS_Ability_Validate_RWaveRadius".Translate());
            if (wave1Damage <= 0 || wave2Damage <= 0 || wave3Damage <= 0) result.AddError("CS_Ability_Validate_RWaveDamage".Translate());
            return result;
        }
    }

    public class Config_ComboStacks : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.ComboStacks;
        public override float EditorBlockHeight => 138f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            comboStackWindowTicks = AbilityEditorNormalizationUtility.ClampInt(comboStackWindowTicks, 1, 99999);
            comboStackMax = AbilityEditorNormalizationUtility.ClampInt(comboStackMax, 1, 99);
            comboStackBonusDamagePerStack = AbilityEditorNormalizationUtility.ClampFloat(comboStackBonusDamagePerStack, 0.01f, 10f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (comboStackWindowTicks <= 0) result.AddError("CS_Ability_Validate_ComboStackWindowTicks".Translate());
            if (comboStackMax <= 0) result.AddError("CS_Ability_Validate_ComboStackMax".Translate());
            if (comboStackBonusDamagePerStack < 0f) result.AddError("CS_Ability_Validate_ComboStackBonusPerStack".Translate());
            return result;
        }
    }

    public class Config_MarkDetonation : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.MarkDetonation;
        public override float EditorBlockHeight => 164f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            markDurationTicks = AbilityEditorNormalizationUtility.ClampInt(markDurationTicks, 1, 99999);
            markMaxStacks = AbilityEditorNormalizationUtility.ClampInt(markMaxStacks, 1, 99);
            markDetonationDamage = AbilityEditorNormalizationUtility.ClampFloat(markDetonationDamage, 0.01f, 99999f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (markDurationTicks <= 0) result.AddError("CS_Ability_Validate_MarkDurationTicks".Translate());
            if (markMaxStacks <= 0) result.AddError("CS_Ability_Validate_MarkMaxStacks".Translate());
            if (markDetonationDamage <= 0f) result.AddError("CS_Ability_Validate_MarkDetonationDamage".Translate());
            return result;
        }
    }
}
