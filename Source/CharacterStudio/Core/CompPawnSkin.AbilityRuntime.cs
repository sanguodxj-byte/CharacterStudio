using System;
using CharacterStudio.Abilities;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    public partial class CompPawnSkin
    {
        // ─── 技能运行时转发（所有 ability 状态已迁移到 CompCharacterAbilityRuntime） ───

        public void ApplyAbilityFaceOverride(ExpressionType? expression, int durationTicks, float pupilBrightnessOffset = 0f, float pupilContrastOffset = 0f)
        {
            AbilityComp?.ApplyAbilityFaceOverride(expression, durationTicks, pupilBrightnessOffset, pupilContrastOffset);
        }

        public bool IsTriggeredEquipmentAnimationActive(string abilityDefName)
        {
            return AbilityComp?.IsTriggeredEquipmentAnimationActive(abilityDefName) ?? false;
        }

        public int TriggeredEquipmentAnimationStartTick
        {
            get => AbilityComp?.TriggeredEquipmentAnimationStartTick ?? -1;
        }

        public void TriggerEquipmentAnimationState(string abilityDefName, int startTick, int durationTicks)
        {
            AbilityComp?.TriggerEquipmentAnimationState(abilityDefName, startTick, durationTicks);
        }

        public void ClearEquipmentAnimationState(string abilityDefName)
        {
            AbilityComp?.ClearEquipmentAnimationState(abilityDefName);
        }

        public void ClearEquipmentAnimationState()
        {
            // Clear all equipment animation state
            AbilityComp?.ClearEquipmentAnimationState(AbilityComp?.TriggeredEquipmentAnimationAbilityDefName ?? string.Empty);
        }

        public void SetWeaponCarryCastingWindow(int visualTicks)
        {
            AbilityComp?.SetWeaponCarryCastingWindow(visualTicks);
        }

        // ── Ability Expression Override forwarding ──
        public bool IsAbilityExpressionOverrideActive()
        {
            return AbilityComp?.IsAbilityExpressionOverrideActive() ?? false;
        }

        public ExpressionType? AbilityExpressionOverride
        {
            get => AbilityComp?.AbilityExpressionOverride;
        }

        public float AbilityPupilBrightnessOffset
        {
            get => AbilityComp?.AbilityPupilBrightnessOffset ?? 0f;
        }

        public float AbilityPupilContrastOffset
        {
            get => AbilityComp?.AbilityPupilContrastOffset ?? 0f;
        }
    }
}
