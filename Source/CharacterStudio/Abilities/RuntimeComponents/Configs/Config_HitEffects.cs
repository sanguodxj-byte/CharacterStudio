using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_HitSlowField : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.HitSlowField;
        public override float EditorBlockHeight => 190f;

        public override void NormalizeForSave()
        {
            slowFieldDurationTicks = AbilityEditorNormalizationUtility.ClampInt(slowFieldDurationTicks, 1, 99999);
            slowFieldRadius = AbilityEditorNormalizationUtility.ClampFloat(slowFieldRadius, 0.1f, 99f);
            slowFieldHediffDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(slowFieldHediffDefName);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (slowFieldDurationTicks <= 0) result.AddError("CS_Ability_Validate_SlowFieldDurationTicks".Translate());
            if (slowFieldRadius <= 0f) result.AddError("CS_Ability_Validate_SlowFieldRadius".Translate());
            if (string.IsNullOrWhiteSpace(slowFieldHediffDefName)) result.AddError("CS_Ability_Validate_SlowFieldHediffDefName".Translate());
            return result;
        }
    }

    public class Config_HitHeal : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.HitHeal;

        public override void NormalizeForSave()
        {
            hitHealAmount = AbilityEditorNormalizationUtility.ClampFloat(hitHealAmount, 0f, 99999f);
            hitHealRatio = AbilityEditorNormalizationUtility.ClampFloat(hitHealRatio, 0f, 10f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (hitHealAmount < 0f || hitHealRatio < 0f) result.AddError("CS_Ability_Validate_HitHealValues".Translate());
            if (hitHealAmount <= 0f && hitHealRatio <= 0f) result.AddError("CS_Ability_Validate_HitHealRequired".Translate());
            return result;
        }
    }

    public class Config_HitCooldownRefund : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.HitCooldownRefund;

        public override void NormalizeForSave()
        {
            hitCooldownRefundPercent = AbilityEditorNormalizationUtility.ClampFloat(hitCooldownRefundPercent, 0.01f, 1f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (hitCooldownRefundPercent <= 0f || hitCooldownRefundPercent > 1f) result.AddError("CS_Ability_Validate_HitCooldownRefundPercent".Translate());
            return result;
        }
    }

    public class Config_KillRefresh : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.KillRefresh;
        public override float EditorBlockHeight => 112f;
        public override bool IsSingleton => true;

        public override void NormalizeForSave()
        {
            killRefreshCooldownPercent = AbilityEditorNormalizationUtility.ClampFloat(killRefreshCooldownPercent, 0.01f, 1f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (killRefreshCooldownPercent <= 0f || killRefreshCooldownPercent > 1f) result.AddError("CS_Ability_Validate_KillRefreshCooldownPercent".Translate());
            return result;
        }
    }

    public class Config_ChainBounce : AbilityRuntimeComponentConfig
    {
        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.ChainBounce;

        public override void NormalizeForSave()
        {
            maxBounceCount = AbilityEditorNormalizationUtility.ClampInt(maxBounceCount, 1, 99);
            bounceRange = AbilityEditorNormalizationUtility.ClampFloat(bounceRange, 0.1f, 99f);
            bounceDamageFalloff = AbilityEditorNormalizationUtility.ClampFloat(bounceDamageFalloff, -0.95f, 0.95f);
        }

        public override AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled) return result;
            if (maxBounceCount <= 0) result.AddError("CS_Ability_Validate_ChainBounceCount".Translate());
            if (bounceRange <= 0f) result.AddError("CS_Ability_Validate_ChainBounceRange".Translate());
            if (bounceDamageFalloff < 0f || bounceDamageFalloff >= 1f) result.AddError("CS_Ability_Validate_ChainBounceFalloff".Translate());
            return result;
        }
    }
}
