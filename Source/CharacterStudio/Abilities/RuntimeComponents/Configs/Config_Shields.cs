using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_ShieldAbsorb : AbilityRuntimeComponentConfig
    {
        [EditorField("CS_Studio_Runtime_ShieldMaxDamage", AbilityRuntimeComponentType.ShieldAbsorb)]
        public new float shieldMaxDamage = 120f;
        [EditorField("CS_Studio_Runtime_ShieldDurationTicks", AbilityRuntimeComponentType.ShieldAbsorb)]
        public new int shieldDurationTicks = 240;
        [EditorField("CS_Studio_Runtime_ShieldHealRatio", AbilityRuntimeComponentType.ShieldAbsorb)]
        public new float shieldHealRatio = 0.5f;
        [EditorField("CS_Studio_Runtime_ShieldBonusDamageRatio", AbilityRuntimeComponentType.ShieldAbsorb)]
        public new float shieldBonusDamageRatio = 0.25f;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.ShieldAbsorb;
        public override float EditorBlockHeight => 164f;
        public override bool IsSingleton => true;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref shieldMaxDamage, "shieldMaxDamage", 120f);
            Scribe_Values.Look(ref shieldDurationTicks, "shieldDurationTicks", 240);
            Scribe_Values.Look(ref shieldHealRatio, "shieldHealRatio", 0.5f);
            Scribe_Values.Look(ref shieldBonusDamageRatio, "shieldBonusDamageRatio", 0.25f);
        }

        public override void NormalizeForSave()
        {
            shieldMaxDamage = AbilityEditorNormalizationUtility.ClampFloat(shieldMaxDamage, 1f, 99999f);
            shieldDurationTicks = AbilityEditorNormalizationUtility.ClampInt(shieldDurationTicks, 1, 99999);
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
        [EditorField("CS_Studio_Runtime_ShieldDurationTicks", AbilityRuntimeComponentType.AttachedShieldVisual)]
        public new int shieldDurationTicks = 240;
        [EditorField("CS_Studio_Runtime_ShieldVisualScale", AbilityRuntimeComponentType.AttachedShieldVisual)]
        public new float shieldVisualScale = 1f;
        [EditorField("CS_Studio_Runtime_ShieldVisualHeightOffset", AbilityRuntimeComponentType.AttachedShieldVisual)]
        public new float shieldVisualHeightOffset = 0f;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.AttachedShieldVisual;
        public override float EditorBlockHeight => 112f;
        public override bool IsSingleton => true;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref shieldDurationTicks, "shieldDurationTicks", 240);
            Scribe_Values.Look(ref shieldVisualScale, "shieldVisualScale", 1f);
            Scribe_Values.Look(ref shieldVisualHeightOffset, "shieldVisualHeightOffset", 0f);
        }

        public override void NormalizeForSave()
        {
            shieldDurationTicks = AbilityEditorNormalizationUtility.ClampInt(shieldDurationTicks, 1, 99999);
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
        [EditorField("CS_Studio_Runtime_ShieldInterceptorThingDefName", AbilityRuntimeComponentType.ProjectileInterceptorShield)]
        public new string shieldInterceptorThingDefName = string.Empty;
        [EditorField("CS_Studio_Runtime_ShieldInterceptorDurationTicks", AbilityRuntimeComponentType.ProjectileInterceptorShield)]
        public new int shieldInterceptorDurationTicks = 240;
        [EditorField("CS_Studio_Runtime_ShieldVisualScale", AbilityRuntimeComponentType.ProjectileInterceptorShield)]
        public new float shieldVisualScale = 1f;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.ProjectileInterceptorShield;
        public override float EditorBlockHeight => 112f;
        public override bool IsSingleton => true;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref shieldInterceptorThingDefName, "shieldInterceptorThingDefName", string.Empty);
            Scribe_Values.Look(ref shieldInterceptorDurationTicks, "shieldInterceptorDurationTicks", 240);
            Scribe_Values.Look(ref shieldVisualScale, "shieldVisualScale", 1f);
        }

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
