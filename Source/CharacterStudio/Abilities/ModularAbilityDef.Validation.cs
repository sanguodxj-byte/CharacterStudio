// ─────────────────────────────────────────────
// 验证结果、规范化工具、验证扩展方法
// 从 ModularAbilityDef.cs 提取
// ─────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 能力验证结果
    /// </summary>
    public class AbilityValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (!IsValid)
            {
                sb.AppendLine("CS_Ability_Validate_Failed".Translate());
                foreach (var error in Errors)
                {
                    sb.AppendLine($"  ✗ {error}");
                }
            }
            if (Warnings.Count > 0)
            {
                sb.AppendLine("CS_Ability_Validate_Warnings".Translate());
                foreach (var warning in Warnings)
                {
                    sb.AppendLine($"  ⚠ {warning}");
                }
            }
            return sb.ToString();
        }
    }

    internal static class AbilityEditorNormalizationUtility
    {
        public static int ClampInt(int value, int min, int max) => Mathf.Clamp(value, min, max);
        public static float ClampFloat(float value, float min, float max) => Mathf.Clamp(value, min, max);
        public static string TrimOrEmpty(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string nonNullValue = value ?? string.Empty;
            return nonNullValue.Trim();
        }
    }

    public static class ModularAbilityDefExtensions
    {
        public static AbilityValidationResult Validate(this ModularAbilityDef ability)
        {
            var result = new AbilityValidationResult();
            if (string.IsNullOrEmpty(ability.defName))
                result.AddError("CS_Ability_Validate_DefNameEmpty".Translate());
            else if (!IsValidDefName(ability.defName))
                result.AddError("CS_Ability_Validate_DefNameInvalid".Translate());
            if (string.IsNullOrEmpty(ability.label))
                result.AddWarning("CS_Ability_Validate_LabelEmpty".Translate());
            if (ability.cooldownTicks < 0)
                result.AddError("CS_Ability_Validate_CooldownNegative".Translate());
            if (ability.warmupTicks < 0)
                result.AddError("CS_Ability_Validate_WarmupNegative".Translate());
            if (ability.charges <= 0)
                result.AddWarning("CS_Ability_Validate_ChargesNonPositive".Translate());
            if (ability.range < 0)
                result.AddError("CS_Ability_Validate_RangeNegative".Translate());
            if (ability.radius < 0)
                result.AddError("CS_Ability_Validate_RadiusNegative".Translate());

            AbilityCarrierType normalizedCarrier = NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = NormalizeTargetType(ability);
            AbilityAreaShape normalizedShape = NormalizeAreaShape(ability);
            if (CarrierNeedsRange(normalizedCarrier, normalizedTarget) && ability.range <= 0)
                result.AddWarning("CS_Ability_Validate_CarrierNeedsRange".Translate(GetCarrierTypeLabel(normalizedCarrier)));
            if (ability.useRadius && normalizedShape != AbilityAreaShape.Irregular && ability.radius <= 0)
                result.AddError("CS_Ability_Validate_AreaRadiusPositive".Translate());
            if (ability.useRadius && normalizedShape == AbilityAreaShape.Irregular && string.IsNullOrWhiteSpace(ability.irregularAreaPattern))
                result.AddError("CS_Ability_Validate_IrregularPatternRequired".Translate());
            if (normalizedCarrier == AbilityCarrierType.Projectile && ability.projectileDef == null)
                result.AddWarning("CS_Ability_Validate_ProjectileDefMissing".Translate());

            if (ability.effects == null || ability.effects.Count == 0)
            {
                result.AddWarning("CS_Ability_Validate_NoEffects".Translate());
            }
            else
            {
                for (int i = 0; i < ability.effects.Count; i++)
                {
                    var effectResult = ability.effects[i].Validate();
                    if (!effectResult.IsValid)
                    {
                        foreach (var error in effectResult.Errors)
                            result.AddError("CS_Ability_Validate_EffectIndexError".Translate(i + 1, error));
                    }
                    foreach (var warning in effectResult.Warnings)
                        result.AddWarning("CS_Ability_Validate_EffectIndexWarning".Translate(i + 1, warning));
                }
            }

            if (ability.runtimeComponents != null)
            {
                for (int i = 0; i < ability.runtimeComponents.Count; i++)
                {
                    var component = ability.runtimeComponents[i];
                    if (component == null)
                    {
                        result.AddWarning("CS_Ability_Validate_RuntimeComponentNull".Translate(i + 1));
                        continue;
                    }
                    string componentLabel = GetRuntimeComponentTypeLabel(component.type);
                    var compResult = component.Validate();
                    if (!compResult.IsValid)
                    {
                        foreach (var error in compResult.Errors)
                            result.AddError("CS_Ability_Validate_RuntimeComponentError".Translate(i + 1, componentLabel, error));
                    }
                    foreach (var warning in compResult.Warnings)
                        result.AddWarning("CS_Ability_Validate_RuntimeComponentWarning".Translate(i + 1, componentLabel, warning));
                }

                AddRuntimeComponentConflictWarnings(ability.runtimeComponents, ability.carrierType, result);
            }

            if (ability.visualEffects != null)
            {
                for (int i = 0; i < ability.visualEffects.Count; i++)
                {
                    var vfx = ability.visualEffects[i];
                    if (vfx == null)
                    {
                        result.AddWarning("CS_Ability_Validate_VfxNull".Translate(i + 1));
                        continue;
                    }

                    var vfxResult = vfx.Validate();
                    if (!vfxResult.IsValid)
                    {
                        foreach (var error in vfxResult.Errors)
                        {
                            result.AddError("CS_Ability_Validate_VfxIndexError".Translate(i + 1, error));
                        }
                    }

                    foreach (var warning in vfxResult.Warnings)
                    {
                        result.AddWarning("CS_Ability_Validate_VfxIndexWarning".Translate(i + 1, warning));
                    }
                }
            }

            return result;
        }

        public static AbilityCarrierType NormalizeCarrierType(AbilityCarrierType type)
        {
            return type switch
            {
                AbilityCarrierType.Touch => AbilityCarrierType.Target,
                AbilityCarrierType.Area => AbilityCarrierType.Target,
                _ => type
            };
        }

        public static AbilityTargetType NormalizeTargetType(ModularAbilityDef ability)
        {
            if (ability == null)
                return AbilityTargetType.Self;
            if (ability.targetType != AbilityTargetType.Self || ability.useRadius || ability.projectileDef != null)
                return ability.targetType;
            return ability.carrierType switch
            {
                AbilityCarrierType.Self => AbilityTargetType.Self,
                AbilityCarrierType.Area => AbilityTargetType.Cell,
                AbilityCarrierType.Projectile => AbilityTargetType.Entity,
                AbilityCarrierType.Touch => AbilityTargetType.Entity,
                AbilityCarrierType.Target => AbilityTargetType.Entity,
                _ => AbilityTargetType.Entity
            };
        }

        public static AbilityAreaCenter NormalizeAreaCenter(ModularAbilityDef ability)
        {
            if (ability == null)
                return AbilityAreaCenter.Target;
            if (ability.areaCenter != AbilityAreaCenter.Target || ability.targetType != AbilityTargetType.Self)
                return ability.areaCenter;
            return ability.carrierType == AbilityCarrierType.Area ? AbilityAreaCenter.Target : AbilityAreaCenter.Self;
        }

        public static AbilityAreaShape NormalizeAreaShape(ModularAbilityDef ability)
        {
            if (ability == null || !ability.useRadius)
                return AbilityAreaShape.Circle;
            return ability.areaShape;
        }

        public static bool CarrierNeedsRange(AbilityCarrierType carrierType, AbilityTargetType targetType)
        {
            carrierType = NormalizeCarrierType(carrierType);
            return carrierType == AbilityCarrierType.Projectile || carrierType == AbilityCarrierType.Target && targetType != AbilityTargetType.Self;
        }

        private static string GetCarrierTypeLabel(AbilityCarrierType type)
        {
            return ($"CS_Ability_CarrierType_{NormalizeCarrierType(type)}").Translate();
        }

        private static string GetRuntimeComponentTypeLabel(AbilityRuntimeComponentType type)
        {
            return ($"CS_Ability_RuntimeComponentType_{type}").Translate();
        }

        private static bool IsValidDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            foreach (char c in defName)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            return true;
        }

        private static void AddRuntimeComponentConflictWarnings(List<AbilityRuntimeComponentConfig> runtimeComponents, AbilityCarrierType carrierType, AbilityValidationResult result)
        {
            List<(AbilityRuntimeComponentConfig component, int index)> enabledComponents = runtimeComponents
                .Select((component, index) => new { component, index })
                .Where(entry => entry.component != null && entry.component.enabled)
                .Select(entry => (entry.component!, entry.index))
                .ToList();
            if (enabledComponents.Count == 0)
            {
                return;
            }

            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.SlotOverrideWindow);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.HotkeyOverride);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.FollowupCooldownGate);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.RStackDetonation);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.KillRefresh);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.PeriodicPulse);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.ShieldAbsorb);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.AttachedShieldVisual);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.ProjectileInterceptorShield);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.MarkDetonation);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.ComboStacks);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.DashEmpoweredStrike);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.FlightState);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.FlightOnlyFollowup);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.FlightLandingBurst);
            WarnForDuplicateSingleton(result, enabledComponents, AbilityRuntimeComponentType.TimeStop);

            List<(AbilityRuntimeComponentConfig component, int index)> movementComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.SmartJump || entry.component.type == AbilityRuntimeComponentType.EShortJump || entry.component.type == AbilityRuntimeComponentType.Dash)
                .ToList();
            if (movementComponents.Count > 1)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_MovementDuplicate".Translate(FormatRuntimeComponentEntries(movementComponents)));
            }

            List<(AbilityRuntimeComponentConfig component, int index)> flightStateComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.FlightState)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> flightOnlyFollowupComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.FlightOnlyFollowup)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> flightLandingBurstComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.FlightLandingBurst)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> dashEmpoweredStrikeComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.DashEmpoweredStrike)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> projectileSplitComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.ProjectileSplit)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> chainBounceComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.ChainBounce)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> shieldAbsorbComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.ShieldAbsorb)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> interceptorShieldComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.ProjectileInterceptorShield)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> hitHealComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.HitHeal)
                .ToList();
            List<(AbilityRuntimeComponentConfig component, int index)> hitCooldownRefundComponents = enabledComponents
                .Where(entry => entry.component.type == AbilityRuntimeComponentType.HitCooldownRefund)
                .ToList();
            bool hasMovement = movementComponents.Count > 0;
            AbilityCarrierType normalizedCarrier = NormalizeCarrierType(carrierType);

            if (dashEmpoweredStrikeComponents.Count > 0 && !hasMovement)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_DashEmpowerNeedsMovement".Translate(FormatRuntimeComponentEntries(dashEmpoweredStrikeComponents)));
            }

            if (flightOnlyFollowupComponents.Count > 0 && flightStateComponents.Count == 0)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_FlightOnlyNeedsFlightState".Translate(
                    FormatRuntimeComponentEntries(flightOnlyFollowupComponents)));
            }

            if (flightLandingBurstComponents.Count > 0 && flightStateComponents.Count == 0)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_LandingBurstNeedsFlightState".Translate(
                    FormatRuntimeComponentEntries(flightLandingBurstComponents)));
            }

            // 投射物分裂和连锁弹跳需要投射物载具类型
            if (projectileSplitComponents.Count > 0 && normalizedCarrier != AbilityCarrierType.Projectile)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_ProjectileSplitNeedsProjectile".Translate(
                    FormatRuntimeComponentEntries(projectileSplitComponents)));
            }

            if (chainBounceComponents.Count > 0 && normalizedCarrier != AbilityCarrierType.Projectile)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_ChainBounceNeedsProjectile".Translate(
                    FormatRuntimeComponentEntries(chainBounceComponents)));
            }

            // 护盾吸收 + 拦截护盾同时存在可能功能重叠
            if (shieldAbsorbComponents.Count > 0 && interceptorShieldComponents.Count > 0)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_ShieldOverlap".Translate(
                    FormatRuntimeComponentEntries(shieldAbsorbComponents),
                    FormatRuntimeComponentEntries(interceptorShieldComponents)));
            }

            // 命中触发类组件（HitHeal、HitCooldownRefund）对自身载具无意义
            if (hitHealComponents.Count > 0 && normalizedCarrier == AbilityCarrierType.Self)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_HitEffectNeedsTarget".Translate(
                    FormatRuntimeComponentEntries(hitHealComponents)));
            }

            if (hitCooldownRefundComponents.Count > 0 && normalizedCarrier == AbilityCarrierType.Self)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_HitEffectNeedsTarget".Translate(
                    FormatRuntimeComponentEntries(hitCooldownRefundComponents)));
            }
        }

        private static string FormatRuntimeComponentEntries(IEnumerable<(AbilityRuntimeComponentConfig component, int index)> components)
        {
            return string.Join(", ", components.Select(entry => $"#{entry.index + 1} {GetRuntimeComponentTypeLabel(entry.component.type)}"));
        }

        private static void WarnForDuplicateSingleton(AbilityValidationResult result, IEnumerable<(AbilityRuntimeComponentConfig component, int index)> enabledComponents, AbilityRuntimeComponentType type)
        {
            List<(AbilityRuntimeComponentConfig component, int index)> duplicates = enabledComponents
                .Where(entry => entry.component.type == type)
                .ToList();
            if (duplicates.Count > 1)
            {
                result.AddWarning("CS_Ability_Validate_RuntimeConflict_DuplicateSingleton".Translate(
                    GetRuntimeComponentTypeLabel(type),
                    duplicates.Count,
                    FormatRuntimeComponentEntries(duplicates)));
            }
        }
    }
}
