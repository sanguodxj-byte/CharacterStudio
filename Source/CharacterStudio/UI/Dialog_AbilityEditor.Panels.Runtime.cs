using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private static readonly AbilityRuntimeComponentType[] RuntimeComponentLibraryOrder =
        {
            AbilityRuntimeComponentType.QComboWindow,
            AbilityRuntimeComponentType.HotkeyOverride,
            AbilityRuntimeComponentType.FollowupCooldownGate,
            AbilityRuntimeComponentType.SmartJump,
            AbilityRuntimeComponentType.EShortJump,
            AbilityRuntimeComponentType.RStackDetonation,
            AbilityRuntimeComponentType.PeriodicPulse,
            AbilityRuntimeComponentType.KillRefresh,
            AbilityRuntimeComponentType.ShieldAbsorb,
            AbilityRuntimeComponentType.ChainBounce,
            AbilityRuntimeComponentType.ExecuteBonusDamage,
            AbilityRuntimeComponentType.MissingHealthBonusDamage,
            AbilityRuntimeComponentType.FullHealthBonusDamage,
            AbilityRuntimeComponentType.NearbyEnemyBonusDamage,
            AbilityRuntimeComponentType.IsolatedTargetBonusDamage,
            AbilityRuntimeComponentType.MarkDetonation,
            AbilityRuntimeComponentType.ComboStacks,
            AbilityRuntimeComponentType.HitSlowField,
            AbilityRuntimeComponentType.PierceBonusDamage,
            AbilityRuntimeComponentType.DashEmpoweredStrike,
            AbilityRuntimeComponentType.HitHeal,
            AbilityRuntimeComponentType.HitCooldownRefund,
            AbilityRuntimeComponentType.ProjectileSplit,
            AbilityRuntimeComponentType.FlightState
        };

        private static IEnumerable<AbilityRuntimeComponentType> GetRuntimeComponentLibraryTypes()
        {
            return RuntimeComponentLibraryOrder;
        }

        private void AddRuntimeComponent(AbilityRuntimeComponentType type)
        {
            if (selectedAbility == null)
            {
                return;
            }

            selectedAbility.runtimeComponents ??= new List<AbilityRuntimeComponentConfig>();
            selectedAbility.runtimeComponents.Add(CreateDefaultRuntimeComponent(type));
            NotifyAbilityPreviewDirty(true);
        }

        private static string GetRuntimeComponentTypeDescription(AbilityRuntimeComponentType type)
        {
            return type switch
            {
                AbilityRuntimeComponentType.QComboWindow => "CS_Studio_Runtime_Desc_QComboWindow".Translate(),
                AbilityRuntimeComponentType.HotkeyOverride => "CS_Studio_Runtime_Desc_HotkeyOverride".Translate(),
                AbilityRuntimeComponentType.FollowupCooldownGate => "CS_Studio_Runtime_Desc_FollowupCooldownGate".Translate(),
                AbilityRuntimeComponentType.SmartJump => "CS_Studio_Runtime_Desc_SmartJump".Translate(),
                AbilityRuntimeComponentType.EShortJump => "CS_Studio_Runtime_Desc_EShortJump".Translate(),
                AbilityRuntimeComponentType.RStackDetonation => "CS_Studio_Runtime_Desc_RStackDetonation".Translate(),
                AbilityRuntimeComponentType.PeriodicPulse => "CS_Studio_Runtime_Desc_PeriodicPulse".Translate(),
                AbilityRuntimeComponentType.KillRefresh => "CS_Studio_Runtime_Desc_KillRefresh".Translate(),
                AbilityRuntimeComponentType.ShieldAbsorb => "CS_Studio_Runtime_Desc_ShieldAbsorb".Translate(),
                AbilityRuntimeComponentType.ChainBounce => "CS_Studio_Runtime_Desc_ChainBounce".Translate(),
                AbilityRuntimeComponentType.ExecuteBonusDamage => "CS_Studio_Runtime_Desc_ExecuteBonusDamage".Translate(),
                AbilityRuntimeComponentType.MissingHealthBonusDamage => "CS_Studio_Runtime_Desc_MissingHealthBonusDamage".Translate(),
                AbilityRuntimeComponentType.FullHealthBonusDamage => "CS_Studio_Runtime_Desc_FullHealthBonusDamage".Translate(),
                AbilityRuntimeComponentType.NearbyEnemyBonusDamage => "CS_Studio_Runtime_Desc_NearbyEnemyBonusDamage".Translate(),
                AbilityRuntimeComponentType.IsolatedTargetBonusDamage => "CS_Studio_Runtime_Desc_IsolatedTargetBonusDamage".Translate(),
                AbilityRuntimeComponentType.MarkDetonation => "CS_Studio_Runtime_Desc_MarkDetonation".Translate(),
                AbilityRuntimeComponentType.ComboStacks => "CS_Studio_Runtime_Desc_ComboStacks".Translate(),
                AbilityRuntimeComponentType.HitSlowField => "CS_Studio_Runtime_Desc_HitSlowField".Translate(),
                AbilityRuntimeComponentType.PierceBonusDamage => "CS_Studio_Runtime_Desc_PierceBonusDamage".Translate(),
                AbilityRuntimeComponentType.DashEmpoweredStrike => "CS_Studio_Runtime_Desc_DashEmpoweredStrike".Translate(),
                AbilityRuntimeComponentType.HitHeal => "CS_Studio_Runtime_Desc_HitHeal".Translate(),
                AbilityRuntimeComponentType.HitCooldownRefund => "CS_Studio_Runtime_Desc_HitCooldownRefund".Translate(),
                AbilityRuntimeComponentType.FlightState => "CS_Studio_Runtime_Desc_FlightState".Translate(),
                AbilityRuntimeComponentType.ProjectileSplit => "CS_Studio_Runtime_Desc_ProjectileSplit".Translate(),
                _ => type.ToString()
            };
        }

        private static AbilityRuntimeComponentConfig CreateDefaultRuntimeComponent(AbilityRuntimeComponentType type)
        {
            var config = new AbilityRuntimeComponentConfig
            {
                type = type,
                enabled = true
            };

            switch (type)
            {
                case AbilityRuntimeComponentType.HotkeyOverride:
                    config.overrideHotkeySlot = AbilityRuntimeHotkeySlot.Q;
                    config.overrideAbilityDefName = string.Empty;
                    config.overrideDurationTicks = 60;
                    break;
                case AbilityRuntimeComponentType.FollowupCooldownGate:
                    config.followupCooldownHotkeySlot = AbilityRuntimeHotkeySlot.Q;
                    config.followupCooldownTicks = 60;
                    break;
                case AbilityRuntimeComponentType.SmartJump:
                    config.cooldownTicks = 120;
                    config.jumpDistance = 6;
                    config.findCellRadius = 3;
                    config.triggerAbilityEffectsAfterJump = true;
                    config.useMouseTargetCell = true;
                    config.smartCastOffsetCells = 1;
                    config.smartCastClampToMaxDistance = true;
                    config.smartCastAllowFallbackForward = true;
                    break;
                case AbilityRuntimeComponentType.EShortJump:
                    config.cooldownTicks = 120;
                    config.jumpDistance = 6;
                    config.findCellRadius = 3;
                    config.triggerAbilityEffectsAfterJump = true;
                    config.useMouseTargetCell = true;
                    config.smartCastOffsetCells = 0;
                    config.smartCastClampToMaxDistance = true;
                    config.smartCastAllowFallbackForward = true;
                    break;
                case AbilityRuntimeComponentType.RStackDetonation:
                    config.waveDamageDef = DamageDefOf.Bomb;
                    break;
                case AbilityRuntimeComponentType.PeriodicPulse:
                    config.pulseIntervalTicks = 60;
                    config.pulseTotalTicks = 240;
                    config.pulseStartsImmediately = true;
                    break;
                case AbilityRuntimeComponentType.KillRefresh:
                    config.killRefreshHotkeySlot = AbilityRuntimeHotkeySlot.Q;
                    config.killRefreshCooldownPercent = 1f;
                    break;
                case AbilityRuntimeComponentType.ShieldAbsorb:
                    config.shieldMaxDamage = 120f;
                    config.shieldDurationTicks = 240f;
                    config.shieldHealRatio = 0.5f;
                    config.shieldBonusDamageRatio = 0.25f;
                    break;
                case AbilityRuntimeComponentType.ChainBounce:
                    config.maxBounceCount = 4;
                    config.bounceRange = 6f;
                    config.bounceDamageFalloff = 0.2f;
                    break;
                case AbilityRuntimeComponentType.ExecuteBonusDamage:
                    config.executeThresholdPercent = 0.3f;
                    config.executeBonusDamageScale = 0.5f;
                    break;
                case AbilityRuntimeComponentType.MissingHealthBonusDamage:
                    config.missingHealthBonusPerTenPercent = 0.05f;
                    config.missingHealthBonusMaxScale = 0.5f;
                    break;
                case AbilityRuntimeComponentType.FullHealthBonusDamage:
                    config.fullHealthThresholdPercent = 0.95f;
                    config.fullHealthBonusDamageScale = 0.35f;
                    break;
                case AbilityRuntimeComponentType.NearbyEnemyBonusDamage:
                    config.nearbyEnemyBonusMaxTargets = 4;
                    config.nearbyEnemyBonusPerTarget = 0.08f;
                    config.nearbyEnemyBonusRadius = 5f;
                    break;
                case AbilityRuntimeComponentType.IsolatedTargetBonusDamage:
                    config.isolatedTargetRadius = 4f;
                    config.isolatedTargetBonusDamageScale = 0.35f;
                    break;
                case AbilityRuntimeComponentType.MarkDetonation:
                    config.markDurationTicks = 180;
                    config.markMaxStacks = 3;
                    config.markDetonationDamage = 40f;
                    config.markDamageDef = DamageDefOf.Bomb;
                    break;
                case AbilityRuntimeComponentType.ComboStacks:
                    config.comboStackWindowTicks = 180;
                    config.comboStackMax = 5;
                    config.comboStackBonusDamagePerStack = 0.06f;
                    break;
                case AbilityRuntimeComponentType.HitSlowField:
                    config.slowFieldDurationTicks = 180;
                    config.slowFieldRadius = 2.5f;
                    config.slowFieldHediffDefName = string.Empty;
                    break;
                case AbilityRuntimeComponentType.PierceBonusDamage:
                    config.pierceMaxTargets = 3;
                    config.pierceBonusDamagePerTarget = 0.15f;
                    config.pierceSearchRange = 5f;
                    break;
                case AbilityRuntimeComponentType.DashEmpoweredStrike:
                    config.dashEmpowerDurationTicks = 180;
                    config.dashEmpowerBonusDamageScale = 0.5f;
                    break;
                case AbilityRuntimeComponentType.HitHeal:
                    config.hitHealAmount = 8f;
                    config.hitHealRatio = 0f;
                    break;
                case AbilityRuntimeComponentType.HitCooldownRefund:
                    config.refundHotkeySlot = AbilityRuntimeHotkeySlot.Q;
                    config.hitCooldownRefundPercent = 0.15f;
                    break;
                case AbilityRuntimeComponentType.ProjectileSplit:
                    config.splitProjectileCount = 2;
                    config.splitDamageScale = 0.5f;
                    config.splitSearchRange = 5f;
                    break;
                case AbilityRuntimeComponentType.FlightState:
                    config.flightDurationTicks = 180;
                    config.flightHeightFactor = 0.35f;
                    break;
            }

            return config;
        }

        private void ShowDamageDefSelectorForRuntime(AbilityRuntimeComponentConfig component)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    component.waveDamageDef = null;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var defs = DefDatabase<DamageDef>.AllDefsListForReading;
            var sorted = new List<DamageDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var damageDef in sorted)
            {
                var localDef = damageDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    component.waveDamageDef = localDef;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowAbilityDefSelectorForRuntime(AbilityRuntimeComponentConfig component)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    component.overrideAbilityDefName = string.Empty;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var defs = DefDatabase<AbilityDef>.AllDefsListForReading;
            var sorted = new List<AbilityDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var abilityDef in sorted)
            {
                var localDef = abilityDef;
                string label = (localDef.label ?? localDef.defName) + $" ({localDef.defName})";
                options.Add(new FloatMenuOption(label, () =>
                {
                    component.overrideAbilityDefName = localDef.defName;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowHediffDefSelectorForRuntime(AbilityRuntimeComponentConfig component)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    component.slowFieldHediffDefName = string.Empty;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var defs = DefDatabase<HediffDef>.AllDefsListForReading;
            var sorted = new List<HediffDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var hediffDef in sorted)
            {
                var localDef = hediffDef;
                string label = (localDef.label ?? localDef.defName) + $" ({localDef.defName})";
                options.Add(new FloatMenuOption(label, () =>
                {
                    component.slowFieldHediffDefName = localDef.defName;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}