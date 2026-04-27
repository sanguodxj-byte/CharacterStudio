using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_SmartJump : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.SmartJump;
        public override float EditorBlockHeight => 244f;

        public override void NormalizeForSave()
        {
            cooldownTicks = AbilityEditorNormalizationUtility.ClampInt(cooldownTicks, 0, 99999);
            jumpDistance = AbilityEditorNormalizationUtility.ClampInt(jumpDistance, 1, 100);
            findCellRadius = AbilityEditorNormalizationUtility.ClampInt(findCellRadius, 0, 30);
            smartCastOffsetCells = AbilityEditorNormalizationUtility.ClampInt(smartCastOffsetCells, 1, 100);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (cooldownTicks < 0) result.AddError("CS_Ability_Validate_EShortJumpCooldown".Translate());
            if (jumpDistance <= 0) result.AddError("CS_Ability_Validate_EShortJumpDistance".Translate());
            if (findCellRadius < 0) result.AddError("CS_Ability_Validate_EShortJumpFindCellRadius".Translate());
            if (smartCastOffsetCells <= 0) result.AddError("CS_Ability_Validate_SmartJumpOffsetCells".Translate());
            return result;
        }
    }

    public class Config_EShortJump : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.EShortJump;
        public override float EditorBlockHeight => 244f;

        public override void NormalizeForSave()
        {
            cooldownTicks = AbilityEditorNormalizationUtility.ClampInt(cooldownTicks, 0, 99999);
            jumpDistance = AbilityEditorNormalizationUtility.ClampInt(jumpDistance, 1, 100);
            findCellRadius = AbilityEditorNormalizationUtility.ClampInt(findCellRadius, 0, 30);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (cooldownTicks < 0) result.AddError("CS_Ability_Validate_EShortJumpCooldown".Translate());
            if (jumpDistance <= 0) result.AddError("CS_Ability_Validate_EShortJumpDistance".Translate());
            if (findCellRadius < 0) result.AddError("CS_Ability_Validate_EShortJumpFindCellRadius".Translate());
            return result;
        }
    }

    public class Config_Dash : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.Dash;
        public override float EditorBlockHeight => 244f;

        public override void NormalizeForSave()
        {
            dashDistance = AbilityEditorNormalizationUtility.ClampInt(dashDistance, 1, 100);
            dashStepDurationTicks = AbilityEditorNormalizationUtility.ClampInt(dashStepDurationTicks, 1, 60);
            equipmentAnimationDurationTicks = AbilityEditorNormalizationUtility.ClampInt(equipmentAnimationDurationTicks, 1, 99999);
            equipmentAnimationTriggerKey = AbilityEditorNormalizationUtility.TrimOrEmpty(equipmentAnimationTriggerKey);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (dashDistance <= 0) result.AddError("CS_Ability_Validate_DashDistance".Translate());
            if (dashStepDurationTicks <= 0) result.AddError("CS_Ability_Validate_DashStepDuration".Translate());
            if (triggerEquipmentAnimationOnApply && string.IsNullOrWhiteSpace(equipmentAnimationTriggerKey))
                result.AddError("CS_Ability_Validate_DashEquipAnimKey".Translate());
            return result;
        }
    }

    public class Config_DashEmpoweredStrike : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.DashEmpoweredStrike;
        public override float EditorBlockHeight => 112f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            dashEmpowerDurationTicks = AbilityEditorNormalizationUtility.ClampInt(dashEmpowerDurationTicks, 1, 99999);
            dashEmpowerBonusDamageScale = AbilityEditorNormalizationUtility.ClampFloat(dashEmpowerBonusDamageScale, 0.01f, 10f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (dashEmpowerDurationTicks <= 0) result.AddError("CS_Ability_Validate_DashEmpowerDurationTicks".Translate());
            if (dashEmpowerBonusDamageScale <= 0f) result.AddError("CS_Ability_Validate_DashEmpowerBonusScale".Translate());
            return result;
        }
    }
}
