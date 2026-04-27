using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_ShieldAbsorb : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.ShieldAbsorb;
        public override float EditorBlockHeight => 164f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            shieldMaxDamage = AbilityEditorNormalizationUtility.ClampFloat(shieldMaxDamage, 1f, 99999f);
            shieldDurationTicks = AbilityEditorNormalizationUtility.ClampFloat(shieldDurationTicks, 1f, 99999f);
            shieldHealRatio = AbilityEditorNormalizationUtility.ClampFloat(shieldHealRatio, 0f, 10f);
            shieldBonusDamageRatio = AbilityEditorNormalizationUtility.ClampFloat(shieldBonusDamageRatio, 0f, 10f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (shieldMaxDamage <= 0f) result.AddError("CS_Ability_Validate_ShieldMaxDamage".Translate());
            if (shieldDurationTicks <= 0f) result.AddError("CS_Ability_Validate_ShieldDurationTicks".Translate());
            if (shieldHealRatio < 0f || shieldBonusDamageRatio < 0f) result.AddError("CS_Ability_Validate_ShieldRatios".Translate());
            return result;
        }
    }

    public class Config_AttachedShieldVisual : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.AttachedShieldVisual;
        public override float EditorBlockHeight => 112f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            shieldDurationTicks = AbilityEditorNormalizationUtility.ClampFloat(shieldDurationTicks, 1f, 99999f);
            shieldVisualScale = AbilityEditorNormalizationUtility.ClampFloat(shieldVisualScale, 0.1f, 10f);
            shieldVisualHeightOffset = AbilityEditorNormalizationUtility.ClampFloat(shieldVisualHeightOffset, -5f, 5f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (shieldVisualScale <= 0f) result.AddError("CS_Ability_Validate_AttachedShieldVisualScale".Translate());
            return result;
        }
    }

    public class Config_ProjectileInterceptorShield : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.ProjectileInterceptorShield;
        public override float EditorBlockHeight => 112f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            shieldInterceptorThingDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(shieldInterceptorThingDefName);
            shieldInterceptorDurationTicks = AbilityEditorNormalizationUtility.ClampInt(shieldInterceptorDurationTicks, 1, 99999);
            shieldVisualScale = AbilityEditorNormalizationUtility.ClampFloat(shieldVisualScale, 0.1f, 10f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (string.IsNullOrWhiteSpace(shieldInterceptorThingDefName)) result.AddError("CS_Ability_Validate_ProjectileInterceptorThingDef".Translate());
            if (shieldInterceptorDurationTicks <= 0) result.AddError("CS_Ability_Validate_ProjectileInterceptorDuration".Translate());
            return result;
        }
    }
}
