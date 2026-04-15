using System.Collections.Generic;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents
{
    // ── Damage Scale Modifiers ──

    public class ExecuteBonusDamageHandler : IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.ExecuteBonusDamage;
        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, bool allowDashConsume, int nowTick)
        {
            if (targetPawn?.health == null) return 0f;
            float summary = targetPawn.health.summaryHealth.SummaryHealthPercent;
            return summary <= config.executeThresholdPercent ? Mathf.Max(0f, config.executeBonusDamageScale) : 0f;
        }
    }

    public class MissingHealthBonusDamageHandler : IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.MissingHealthBonusDamage;
        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, bool allowDashConsume, int nowTick)
        {
            if (targetPawn?.health == null) return 0f;
            float missing = 1f - targetPawn.health.summaryHealth.SummaryHealthPercent;
            float steps = Mathf.Max(0f, missing / 0.1f);
            float bonus = steps * Mathf.Max(0f, config.missingHealthBonusPerTenPercent);
            return Mathf.Min(Mathf.Max(0f, config.missingHealthBonusMaxScale), bonus);
        }
    }

    public class FullHealthBonusDamageHandler : IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.FullHealthBonusDamage;
        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, bool allowDashConsume, int nowTick)
        {
            if (targetPawn?.health == null) return 0f;
            float summary = targetPawn.health.summaryHealth.SummaryHealthPercent;
            return summary >= Mathf.Clamp01(config.fullHealthThresholdPercent) ? Mathf.Max(0f, config.fullHealthBonusDamageScale) : 0f;
        }
    }

    public class NearbyEnemyBonusDamageHandler : IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.NearbyEnemyBonusDamage;
        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, bool allowDashConsume, int nowTick)
        {
            if (targetPawn == null || caster.Map == null) return 0f;
            int count = CompAbilityEffect_Modular.CountNearbyEnemyPawns(caster, target.Cell,
                Mathf.Max(0.1f, config.nearbyEnemyBonusRadius), Mathf.Max(1, config.nearbyEnemyBonusMaxTargets), targetPawn);
            return count * Mathf.Max(0f, config.nearbyEnemyBonusPerTarget);
        }
    }

    public class IsolatedTargetBonusDamageHandler : IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.IsolatedTargetBonusDamage;
        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, bool allowDashConsume, int nowTick)
        {
            if (targetPawn == null || caster.Map == null) return 0f;
            int nearbyCount = CompAbilityEffect_Modular.CountNearbyEnemyPawns(caster, target.Cell,
                Mathf.Max(0.1f, config.isolatedTargetRadius), 1, targetPawn);
            return nearbyCount == 0 ? Mathf.Max(0f, config.isolatedTargetBonusDamageScale) : 0f;
        }
    }

    public class ComboStacksDamageHandler : IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.ComboStacks;
        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, bool allowDashConsume, int nowTick)
        {
            if (casterAbility != null && casterAbility.OffensiveComboExpireTick >= nowTick)
                return casterAbility.OffensiveComboStacks * Mathf.Max(0f, config.comboStackBonusDamagePerStack);
            return 0f;
        }
    }

    public class PierceBonusDamageHandler : IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.PierceBonusDamage;
        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, bool allowDashConsume, int nowTick)
        {
            if (caster.Map == null) return 0f;
            int hitCount = CompAbilityEffect_Modular.CountNearbyEnemyPawns(caster, target.Cell,
                Mathf.Max(0.1f, config.pierceSearchRange), Mathf.Max(1, config.pierceMaxTargets));
            return hitCount * Mathf.Max(0f, config.pierceBonusDamagePerTarget);
        }
    }

    public class MarkDetonationDamageHandler : IDamageScaleModifier
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.MarkDetonation;
        public float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, bool allowDashConsume, int nowTick)
        {
            if (targetAbility != null && targetAbility.OffensiveMarkExpireTick >= nowTick && targetAbility.OffensiveMarkStacks > 0)
                return Mathf.Min(0.5f, targetAbility.OffensiveMarkStacks * 0.05f);
            return 0f;
        }
    }

    // ── Post-Hit Handlers ──

    public class MarkDetonationPostHitHandler : IPostHitHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.MarkDetonation;
        public void OnPostHit(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, float appliedDamage, int nowTick)
        {
            if (targetAbility == null) return;

            if (targetAbility.OffensiveMarkExpireTick < nowTick)
                targetAbility.OffensiveMarkStacks = 0;

            targetAbility.OffensiveMarkExpireTick = nowTick + Mathf.Max(1, config.markDurationTicks);
            targetAbility.OffensiveMarkStacks = Mathf.Min(Mathf.Max(1, config.markMaxStacks), targetAbility.OffensiveMarkStacks + 1);

            if (targetAbility.OffensiveMarkStacks >= Mathf.Max(1, config.markMaxStacks) && targetPawn != null && !targetPawn.Dead)
            {
                CompAbilityEffect_Modular.ApplyDirectDamageToPawn(caster, targetPawn, config.markDamageDef, config.markDetonationDamage);
                targetAbility.OffensiveMarkStacks = 0;
                targetAbility.OffensiveMarkExpireTick = -1;
            }
        }
    }

    public class ComboStacksPostHitHandler : IPostHitHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.ComboStacks;
        public void OnPostHit(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, float appliedDamage, int nowTick)
        {
            if (casterAbility == null) return;
            if (casterAbility.OffensiveComboExpireTick < nowTick)
                casterAbility.OffensiveComboStacks = 0;
            casterAbility.OffensiveComboExpireTick = nowTick + Mathf.Max(1, config.comboStackWindowTicks);
            casterAbility.OffensiveComboStacks = Mathf.Min(Mathf.Max(1, config.comboStackMax), casterAbility.OffensiveComboStacks + 1);
        }
    }

    public class HitSlowFieldHandler : IPostHitHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.HitSlowField;
        public void OnPostHit(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, float appliedDamage, int nowTick)
        {
            if (caster.Map == null || string.IsNullOrWhiteSpace(config.slowFieldHediffDefName)) return;

            HediffDef? hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(config.slowFieldHediffDefName.Trim());
            if (hediffDef == null) return;

            int durationTicks = Mathf.Max(1, config.slowFieldDurationTicks);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(target.Cell, config.slowFieldRadius, true))
            {
                if (!cell.InBounds(caster.Map)) continue;
                List<Thing> thingsInCell = cell.GetThingList(caster.Map);
                for (int i = 0; i < thingsInCell.Count; i++)
                {
                    if (thingsInCell[i] is not Pawn pawn || pawn == null || pawn.Dead || pawn.Faction == caster.Faction) continue;
                    Hediff hediff = pawn.health.AddHediff(hediffDef);
                    if (hediff.TryGetComp<HediffComp_Disappears>() is HediffComp_Disappears disappears)
                        disappears.ticksToDisappear = durationTicks;
                }
            }
        }
    }

    public class HitHealHandler : IPostHitHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.HitHeal;
        public void OnPostHit(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, float appliedDamage, int nowTick)
        {
            float heal = Mathf.Max(0f, config.hitHealAmount) + (Mathf.Max(0f, config.hitHealRatio) * Mathf.Max(0f, appliedDamage));
            CompAbilityEffect_Modular.ApplyShieldHeal(caster, heal);
        }
    }

    public class HitCooldownRefundHandler : IPostHitHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.HitCooldownRefund;
        public void OnPostHit(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, float appliedDamage, int nowTick)
        {
            if (casterAbility == null) return;
            float percent = Mathf.Clamp01(config.hitCooldownRefundPercent);
            int currentCooldown = Mathf.Max(0, casterAbility.GetCooldownUntilTick(config.refundHotkeySlot) - nowTick);
            casterAbility.SetCooldownUntilTick(config.refundHotkeySlot, nowTick + Mathf.RoundToInt(currentCooldown * (1f - percent)));
        }
    }

    public class ProjectileSplitHandler : IPostHitHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.ProjectileSplit;
        public void OnPostHit(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompCharacterAbilityRuntime casterAbility, LocalTargetInfo target, Pawn targetPawn, CompCharacterAbilityRuntime targetAbility, float appliedDamage, int nowTick)
            => source.TriggerProjectileSplit(config, caster, target);
    }
}