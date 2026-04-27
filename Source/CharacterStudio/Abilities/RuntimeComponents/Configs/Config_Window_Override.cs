using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_SlotOverrideWindow : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.SlotOverrideWindow;
        public override float EditorBlockHeight => 138f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            comboWindowTicks = AbilityEditorNormalizationUtility.ClampInt(comboWindowTicks, 1, 9999);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (comboWindowTicks <= 0)
                result.AddError("CS_Ability_Validate_QComboWindowTicks".Translate());
            if (string.IsNullOrWhiteSpace(comboTargetAbilityDefName))
                result.AddError("CS_Ability_Validate_HotkeyOverrideAbilityDefName".Translate());
            return result;
        }
    }

    public class Config_HotkeyOverride : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.HotkeyOverride;
        public override float EditorBlockHeight => 164f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave() { }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (string.IsNullOrWhiteSpace(overrideAbilityDefName))
                result.AddError("CS_Ability_Validate_HotkeyOverrideAbilityDefName".Translate());
            return result;
        }
    }

    public class Config_FollowupCooldownGate : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.FollowupCooldownGate;
        public override float EditorBlockHeight => 164f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            maxComboFollowupDelayTicks = AbilityEditorNormalizationUtility.ClampInt(maxComboFollowupDelayTicks, -1, 9999);
            cooldownPunishScale = Mathf.Clamp(cooldownPunishScale, 0.5f, 50f);
        }

        public override AbilityValidationResult Validate()
        {
            return new AbilityValidationResult();
        }
    }
}
