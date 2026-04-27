using System;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents.Configs
{
    public class Config_HitSlowField : AbilityRuntimeComponentConfig
    {
        [EditorField("CS_Studio_Runtime_SlowFieldDurationTicks", AbilityRuntimeComponentType.HitSlowField)]
        public new int slowFieldDurationTicks = 180;
        [EditorField("CS_Studio_Runtime_SlowFieldRadius", AbilityRuntimeComponentType.HitSlowField)]
        public new float slowFieldRadius = 2.5f;
        [EditorField("CS_Studio_Runtime_SlowFieldHediffDefName", AbilityRuntimeComponentType.HitSlowField)]
        public new string slowFieldHediffDefName = string.Empty;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.HitSlowField;
        public override float EditorBlockHeight => 190f;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref slowFieldDurationTicks, "slowFieldDurationTicks", 180);
            Scribe_Values.Look(ref slowFieldRadius, "slowFieldRadius", 2.5f);
            Scribe_Values.Look(ref slowFieldHediffDefName, "slowFieldHediffDefName", string.Empty);
        }

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
        [EditorField("CS_Studio_Runtime_HitHealAmount", AbilityRuntimeComponentType.HitHeal)]
        public new float hitHealAmount = 8f;
        [EditorField("CS_Studio_Runtime_HitHealRatio", AbilityRuntimeComponentType.HitHeal)]
        public new float hitHealRatio = 0f;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.HitHeal;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hitHealAmount, "hitHealAmount", 8f);
            Scribe_Values.Look(ref hitHealRatio, "hitHealRatio", 0f);
        }

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
        [EditorField("CS_Studio_Runtime_RefundHotkeySlot", AbilityRuntimeComponentType.HitCooldownRefund)]
        public new AbilityRuntimeHotkeySlot refundHotkeySlot = AbilityRuntimeHotkeySlot.None;
        [EditorField("CS_Studio_Runtime_HitCooldownRefundPercent", AbilityRuntimeComponentType.HitCooldownRefund)]
        public new float hitCooldownRefundPercent = 0.15f;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.HitCooldownRefund;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref refundHotkeySlot, "refundHotkeySlot", AbilityRuntimeHotkeySlot.Q);
            Scribe_Values.Look(ref hitCooldownRefundPercent, "hitCooldownRefundPercent", 0.15f);
        }

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
        [EditorField("CS_Studio_Runtime_KillRefreshHotkeySlot", AbilityRuntimeComponentType.KillRefresh)]
        public new AbilityRuntimeHotkeySlot killRefreshHotkeySlot = AbilityRuntimeHotkeySlot.None;
        [EditorField("CS_Studio_Runtime_KillRefreshCooldownPercent", AbilityRuntimeComponentType.KillRefresh)]
        public new float killRefreshCooldownPercent = 1f;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.KillRefresh;
        public override float EditorBlockHeight => 112f;
        public override bool IsSingleton => true;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref killRefreshHotkeySlot, "killRefreshHotkeySlot", AbilityRuntimeHotkeySlot.Q);
            Scribe_Values.Look(ref killRefreshCooldownPercent, "killRefreshCooldownPercent", 1f);
        }

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
        [EditorField("CS_Studio_Runtime_MaxBounceCount", AbilityRuntimeComponentType.ChainBounce)]
        public new int maxBounceCount = 4;
        [EditorField("CS_Studio_Runtime_BounceRange", AbilityRuntimeComponentType.ChainBounce)]
        public new float bounceRange = 6f;
        [EditorField("CS_Studio_Runtime_BounceDamageFalloff", AbilityRuntimeComponentType.ChainBounce)]
        public new float bounceDamageFalloff = 0.2f;

        public override AbilityRuntimeComponentType type => AbilityRuntimeComponentType.ChainBounce;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maxBounceCount, "maxBounceCount", 4);
            Scribe_Values.Look(ref bounceRange, "bounceRange", 6f);
            Scribe_Values.Look(ref bounceDamageFalloff, "bounceDamageFalloff", 0.2f);
        }

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
