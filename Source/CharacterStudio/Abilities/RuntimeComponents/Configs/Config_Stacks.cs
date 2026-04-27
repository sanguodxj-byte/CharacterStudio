using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_RStackDetonation : AbilityRuntimeComponentConfig
    {
        [EditorField("CS_Studio_Runtime_RequiredStacks", AbilityRuntimeComponentType.RStackDetonation)]
        public new int requiredStacks = 7;
        [EditorField("CS_Studio_Runtime_DelayTicks", AbilityRuntimeComponentType.RStackDetonation)]
        public new int delayTicks = 180;
        [EditorField("CS_Studio_Runtime_Wave1Radius", AbilityRuntimeComponentType.RStackDetonation)]
        public new float wave1Radius = 3f;
        [EditorField("CS_Studio_Runtime_Wave1Damage", AbilityRuntimeComponentType.RStackDetonation)]
        public new float wave1Damage = 80f;
        [EditorField("CS_Studio_Runtime_Wave2Radius", AbilityRuntimeComponentType.RStackDetonation)]
        public new float wave2Radius = 6f;
        [EditorField("CS_Studio_Runtime_Wave2Damage", AbilityRuntimeComponentType.RStackDetonation)]
        public new float wave2Damage = 140f;
        [EditorField("CS_Studio_Runtime_Wave3Radius", AbilityRuntimeComponentType.RStackDetonation)]
        public new float wave3Radius = 9f;
        [EditorField("CS_Studio_Runtime_Wave3Damage", AbilityRuntimeComponentType.RStackDetonation)]
        public new float wave3Damage = 220f;
        [EditorField("CS_Studio_Runtime_WaveDamageDef", AbilityRuntimeComponentType.RStackDetonation)]
        public new DamageDef? waveDamageDef;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.RStackDetonation;
        public override float EditorBlockHeight => 250f;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref requiredStacks, "requiredStacks", 7);
            Scribe_Values.Look(ref delayTicks, "delayTicks", 180);
            Scribe_Values.Look(ref wave1Radius, "wave1Radius", 3f);
            Scribe_Values.Look(ref wave1Damage, "wave1Damage", 80f);
            Scribe_Values.Look(ref wave2Radius, "wave2Radius", 6f);
            Scribe_Values.Look(ref wave2Damage, "wave2Damage", 140f);
            Scribe_Values.Look(ref wave3Radius, "wave3Radius", 9f);
            Scribe_Values.Look(ref wave3Damage, "wave3Damage", 220f);
            Scribe_Defs.Look(ref waveDamageDef, "waveDamageDef");
        }

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
        [EditorField("CS_Studio_Runtime_ComboStackWindowTicks", AbilityRuntimeComponentType.ComboStacks)]
        public new int comboStackWindowTicks = 180;
        [EditorField("CS_Studio_Runtime_ComboStackMax", AbilityRuntimeComponentType.ComboStacks)]
        public new int comboStackMax = 5;
        [EditorField("CS_Studio_Runtime_ComboStackBonusDamagePerStack", AbilityRuntimeComponentType.ComboStacks)]
        public new float comboStackBonusDamagePerStack = 0.06f;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.ComboStacks;
        public override float EditorBlockHeight => 138f;
        public override bool IsSingleton => true;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref comboStackWindowTicks, "comboStackWindowTicks", 180);
            Scribe_Values.Look(ref comboStackMax, "comboStackMax", 5);
            Scribe_Values.Look(ref comboStackBonusDamagePerStack, "comboStackBonusDamagePerStack", 0.06f);
        }

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
        [EditorField("CS_Studio_Runtime_MarkDurationTicks", AbilityRuntimeComponentType.MarkDetonation)]
        public new int markDurationTicks = 180;
        [EditorField("CS_Studio_Runtime_MarkMaxStacks", AbilityRuntimeComponentType.MarkDetonation)]
        public new int markMaxStacks = 3;
        [EditorField("CS_Studio_Runtime_MarkDetonationDamage", AbilityRuntimeComponentType.MarkDetonation)]
        public new float markDetonationDamage = 40f;
        [EditorField("CS_Studio_Runtime_MarkDamageDef", AbilityRuntimeComponentType.MarkDetonation)]
        public new DamageDef? markDamageDef;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.MarkDetonation;
        public override float EditorBlockHeight => 164f;
        public override bool IsSingleton => true;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref markDurationTicks, "markDurationTicks", 180);
            Scribe_Values.Look(ref markMaxStacks, "markMaxStacks", 3);
            Scribe_Values.Look(ref markDetonationDamage, "markDetonationDamage", 40f);
            Scribe_Defs.Look(ref markDamageDef, "markDamageDef");
        }

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
