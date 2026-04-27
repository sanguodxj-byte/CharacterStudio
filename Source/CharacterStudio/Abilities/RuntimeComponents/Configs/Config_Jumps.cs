using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_SmartJump : AbilityRuntimeComponentConfig
    {
        [EditorField("CS_Studio_Runtime_CooldownTicks", AbilityRuntimeComponentType.SmartJump)]
        public new int cooldownTicks = 120;
        [EditorField("CS_Studio_Runtime_JumpDistance", AbilityRuntimeComponentType.SmartJump)]
        public new int jumpDistance = 6;
        [EditorField("CS_Studio_Runtime_FindCellRadius", AbilityRuntimeComponentType.SmartJump)]
        public new int findCellRadius = 3;
        [EditorField("CS_Studio_Runtime_TriggerEffectsAfterJump", AbilityRuntimeComponentType.SmartJump)]
        public new bool triggerAbilityEffectsAfterJump = true;
        [EditorField("CS_Studio_Runtime_UseMouseTargetCell", AbilityRuntimeComponentType.SmartJump)]
        public new bool useMouseTargetCell = true;
        [EditorField("CS_Studio_Runtime_SmartCastOffsetCells", AbilityRuntimeComponentType.SmartJump)]
        public new int smartCastOffsetCells = 1;
        [EditorField("CS_Studio_Runtime_SmartCastClampToMaxDistance", AbilityRuntimeComponentType.SmartJump)]
        public new bool smartCastClampToMaxDistance = true;
        [EditorField("CS_Studio_Runtime_SmartCastAllowFallbackForward", AbilityRuntimeComponentType.SmartJump)]
        public new bool smartCastAllowFallbackForward = true;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.SmartJump;
        public override float EditorBlockHeight => 244f;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cooldownTicks, "cooldownTicks", 120);
            Scribe_Values.Look(ref jumpDistance, "jumpDistance", 6);
            Scribe_Values.Look(ref findCellRadius, "findCellRadius", 3);
            Scribe_Values.Look(ref triggerAbilityEffectsAfterJump, "triggerAbilityEffectsAfterJump", true);
            Scribe_Values.Look(ref useMouseTargetCell, "useMouseTargetCell", true);
            Scribe_Values.Look(ref smartCastOffsetCells, "smartCastOffsetCells", 1);
            Scribe_Values.Look(ref smartCastClampToMaxDistance, "smartCastClampToMaxDistance", true);
            Scribe_Values.Look(ref smartCastAllowFallbackForward, "smartCastAllowFallbackForward", true);
        }

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
        [EditorField("CS_Studio_Runtime_CooldownTicks", AbilityRuntimeComponentType.EShortJump)]
        public new int cooldownTicks = 120;
        [EditorField("CS_Studio_Runtime_JumpDistance", AbilityRuntimeComponentType.EShortJump)]
        public new int jumpDistance = 6;
        [EditorField("CS_Studio_Runtime_FindCellRadius", AbilityRuntimeComponentType.EShortJump)]
        public new int findCellRadius = 3;
        [EditorField("CS_Studio_Runtime_TriggerEffectsAfterJump", AbilityRuntimeComponentType.EShortJump)]
        public new bool triggerAbilityEffectsAfterJump = true;
        [EditorField("CS_Studio_Runtime_UseMouseTargetCell", AbilityRuntimeComponentType.EShortJump)]
        public new bool useMouseTargetCell = true;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.EShortJump;
        public override float EditorBlockHeight => 244f;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cooldownTicks, "cooldownTicks", 120);
            Scribe_Values.Look(ref jumpDistance, "jumpDistance", 6);
            Scribe_Values.Look(ref findCellRadius, "findCellRadius", 3);
            Scribe_Values.Look(ref triggerAbilityEffectsAfterJump, "triggerAbilityEffectsAfterJump", true);
            Scribe_Values.Look(ref useMouseTargetCell, "useMouseTargetCell", true);
        }

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
        [EditorField("CS_Studio_Runtime_DashDistance", AbilityRuntimeComponentType.Dash)]
        public new int dashDistance = 6;
        [EditorField("CS_Studio_Runtime_DashStepDurationTicks", AbilityRuntimeComponentType.Dash)]
        public new int dashStepDurationTicks = 3;
        [EditorField("CS_Studio_Runtime_DashEffectTiming", AbilityRuntimeComponentType.Dash)]
        public new DashEffectTiming dashEffectTiming = DashEffectTiming.OnCollisionStop;
        [EditorField("CS_Studio_Runtime_DashUseAbilityRange", AbilityRuntimeComponentType.Dash)]
        public new bool dashUseAbilityRange = false;
        [EditorField("CS_Studio_Runtime_DashLanding", AbilityRuntimeComponentType.Dash)]
        public new bool dashLanding = false;
        [EditorField("CS_Studio_Runtime_TriggerEquipmentAnimationOnApply", AbilityRuntimeComponentType.Dash)]
        public new bool triggerEquipmentAnimationOnApply = false;
        [EditorField("CS_Studio_Runtime_EquipmentAnimationTriggerKey", AbilityRuntimeComponentType.Dash)]
        public new string equipmentAnimationTriggerKey = "Dash";
        [EditorField("CS_Studio_Runtime_EquipmentAnimationDurationTicks", AbilityRuntimeComponentType.Dash)]
        public new int equipmentAnimationDurationTicks = 30;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.Dash;
        public override float EditorBlockHeight => 244f;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dashDistance, "dashDistance", 6);
            Scribe_Values.Look(ref dashStepDurationTicks, "dashStepDurationTicks", 3);
            Scribe_Values.Look(ref dashEffectTiming, "dashEffectTiming", DashEffectTiming.OnCollisionStop);
            Scribe_Values.Look(ref dashUseAbilityRange, "dashUseAbilityRange", false);
            Scribe_Values.Look(ref dashLanding, "dashLanding", false);
            Scribe_Values.Look(ref triggerEquipmentAnimationOnApply, "triggerEquipmentAnimationOnApply", false);
            Scribe_Values.Look(ref equipmentAnimationTriggerKey, "equipmentAnimationTriggerKey", "Dash");
            Scribe_Values.Look(ref equipmentAnimationDurationTicks, "equipmentAnimationDurationTicks", 30);
        }

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


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dashEmpowerDurationTicks, "dashEmpowerDurationTicks", 180);
            Scribe_Values.Look(ref dashEmpowerBonusDamageScale, "dashEmpowerBonusDamageScale", 0.5f);
        }

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
