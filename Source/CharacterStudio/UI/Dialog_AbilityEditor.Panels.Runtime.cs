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
            AbilityRuntimeComponentType.SlotOverrideWindow,
            AbilityRuntimeComponentType.HotkeyOverride,
            AbilityRuntimeComponentType.FollowupCooldownGate,
            AbilityRuntimeComponentType.SmartJump,
            AbilityRuntimeComponentType.EShortJump,
            AbilityRuntimeComponentType.RStackDetonation,
            AbilityRuntimeComponentType.PeriodicPulse,
            AbilityRuntimeComponentType.KillRefresh,
            AbilityRuntimeComponentType.ShieldAbsorb,
            AbilityRuntimeComponentType.AttachedShieldVisual,
            AbilityRuntimeComponentType.ProjectileInterceptorShield,
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
            AbilityRuntimeComponentType.FlightState,
            AbilityRuntimeComponentType.FlightOnlyFollowup,
            AbilityRuntimeComponentType.FlightLandingBurst,
            AbilityRuntimeComponentType.TimeStop,
            AbilityRuntimeComponentType.WeatherChange
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
            if (WouldDuplicateSingletonRuntimeComponent(selectedAbility.runtimeComponents, type))
            {
                Messages.Message(
                    "CS_Studio_Runtime_DuplicateSingletonBlocked".Translate(("CS_Ability_RuntimeComponentType_" + type).Translate()),
                    MessageTypeDefOf.CautionInput,
                    false);
                return;
            }

            selectedAbility.runtimeComponents.Add(CreateDefaultRuntimeComponent(type));
            NotifyAbilityPreviewDirty(true);
        }

        private static bool WouldDuplicateSingletonRuntimeComponent(List<AbilityRuntimeComponentConfig> runtimeComponents, AbilityRuntimeComponentType type)
        {
            if (!IsSingletonLikeRuntimeComponent(type))
            {
                return false;
            }

            return runtimeComponents.Exists(c => c != null && c.type == type);
        }

        private static bool IsSingletonLikeRuntimeComponent(AbilityRuntimeComponentType type)
        {
            return type == AbilityRuntimeComponentType.SlotOverrideWindow
                || type == AbilityRuntimeComponentType.HotkeyOverride
                || type == AbilityRuntimeComponentType.FollowupCooldownGate
                || type == AbilityRuntimeComponentType.KillRefresh
                || type == AbilityRuntimeComponentType.PeriodicPulse
                || type == AbilityRuntimeComponentType.ShieldAbsorb
                || type == AbilityRuntimeComponentType.AttachedShieldVisual
                || type == AbilityRuntimeComponentType.ProjectileInterceptorShield
                || type == AbilityRuntimeComponentType.MarkDetonation
                || type == AbilityRuntimeComponentType.ComboStacks
                || type == AbilityRuntimeComponentType.DashEmpoweredStrike
                || type == AbilityRuntimeComponentType.FlightState
                || type == AbilityRuntimeComponentType.FlightOnlyFollowup
                || type == AbilityRuntimeComponentType.FlightLandingBurst
                || type == AbilityRuntimeComponentType.TimeStop;
        }

        private static string GetRuntimeComponentTypeDescription(AbilityRuntimeComponentType type)
        {
            return type switch
            {
                AbilityRuntimeComponentType.SlotOverrideWindow => "CS_Studio_Runtime_Desc_SlotOverrideWindow".Translate(),
                AbilityRuntimeComponentType.HotkeyOverride => "CS_Studio_Runtime_Desc_HotkeyOverride".Translate(),
                AbilityRuntimeComponentType.FollowupCooldownGate => "CS_Studio_Runtime_Desc_FollowupCooldownGate".Translate(),
                AbilityRuntimeComponentType.SmartJump => "CS_Studio_Runtime_Desc_SmartJump".Translate(),
                AbilityRuntimeComponentType.EShortJump => "CS_Studio_Runtime_Desc_EShortJump".Translate(),
                AbilityRuntimeComponentType.RStackDetonation => "CS_Studio_Runtime_Desc_RStackDetonation".Translate(),
                AbilityRuntimeComponentType.PeriodicPulse => "CS_Studio_Runtime_Desc_PeriodicPulse".Translate(),
                AbilityRuntimeComponentType.KillRefresh => "CS_Studio_Runtime_Desc_KillRefresh".Translate(),
                AbilityRuntimeComponentType.ShieldAbsorb => "CS_Studio_Runtime_Desc_ShieldAbsorb".Translate(),
                AbilityRuntimeComponentType.AttachedShieldVisual => "CS_Studio_Runtime_Desc_AttachedShieldVisual".Translate(),
                AbilityRuntimeComponentType.ProjectileInterceptorShield => "CS_Studio_Runtime_Desc_ProjectileInterceptorShield".Translate(),
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
                AbilityRuntimeComponentType.FlightOnlyFollowup => "CS_Studio_Runtime_Desc_FlightOnlyFollowup".Translate(),
                AbilityRuntimeComponentType.FlightLandingBurst => "CS_Studio_Runtime_Desc_FlightLandingBurst".Translate(),
                AbilityRuntimeComponentType.ProjectileSplit => "CS_Studio_Runtime_Desc_ProjectileSplit".Translate(),
                AbilityRuntimeComponentType.TimeStop => "CS_Studio_Runtime_Desc_TimeStop".Translate(),
                AbilityRuntimeComponentType.WeatherChange => "CS_Studio_Runtime_Desc_WeatherChange".Translate(),
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
                case AbilityRuntimeComponentType.AttachedShieldVisual:
                    config.shieldVisualScale = 1.35f;
                    config.shieldVisualHeightOffset = 0f;
                    break;
                case AbilityRuntimeComponentType.ProjectileInterceptorShield:
                    config.shieldInterceptorThingDefName = "CS_ProjectileInterceptorShield";
                    config.shieldInterceptorDurationTicks = 240;
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
                    config.suppressCombatActionsDuringFlightState = true;
                    break;
                case AbilityRuntimeComponentType.FlightOnlyFollowup:
                    config.requiredFlightSourceAbilityDefName = string.Empty;
                    config.requireReservedTargetCell = false;
                    config.consumeFlightStateOnCast = false;
                    config.onlyUseDuringFlightWindow = true;
                    break;
                case AbilityRuntimeComponentType.FlightLandingBurst:
                    config.landingBurstRadius = 3f;
                    config.landingBurstDamage = 30f;
                    config.landingBurstDamageDef = DamageDefOf.Bomb;
                    config.landingEffecterDefName = string.Empty;
                    config.landingSoundDefName = string.Empty;
                    config.affectBuildings = false;
                    config.affectCells = true;
                    config.knockbackTargets = false;
                    config.knockbackDistance = 1.5f;
                    break;
                case AbilityRuntimeComponentType.TimeStop:
                    config.timeStopDurationTicks = 60;
                    config.freezeVisualsDuringTimeStop = true;
                    break;
                case AbilityRuntimeComponentType.WeatherChange:
                    config.weatherDurationTicks = 60000;
                    config.weatherTransitionTicks = 3000;
                    break;
            }

            return config;
        }

        private static IEnumerable<AbilityRuntimeHotkeySlot> GetRuntimeHotkeySlotOptions()
        {
            yield return AbilityRuntimeHotkeySlot.Q;
            yield return AbilityRuntimeHotkeySlot.W;
            yield return AbilityRuntimeHotkeySlot.E;
            yield return AbilityRuntimeHotkeySlot.R;
            yield return AbilityRuntimeHotkeySlot.T;
            yield return AbilityRuntimeHotkeySlot.A;
            yield return AbilityRuntimeHotkeySlot.S;
            yield return AbilityRuntimeHotkeySlot.D;
            yield return AbilityRuntimeHotkeySlot.F;
            yield return AbilityRuntimeHotkeySlot.Z;
            yield return AbilityRuntimeHotkeySlot.X;
            yield return AbilityRuntimeHotkeySlot.C;
            yield return AbilityRuntimeHotkeySlot.V;
        }

        private void ShowDamageDefSelectorForRuntime(AbilityRuntimeComponentConfig component)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    component.waveDamageDef = null;
                    component.markDamageDef = null;
                    component.landingBurstDamageDef = null;
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
                    switch (component.type)
                    {
                        case AbilityRuntimeComponentType.MarkDetonation:
                            component.markDamageDef = localDef;
                            break;
                        case AbilityRuntimeComponentType.FlightLandingBurst:
                            component.landingBurstDamageDef = localDef;
                            break;
                        default:
                            component.waveDamageDef = localDef;
                            break;
                    }
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
                    if (component.type == AbilityRuntimeComponentType.FlightOnlyFollowup)
                    {
                        component.requiredFlightSourceAbilityDefName = string.Empty;
                    }
                    else
                    {
                        component.overrideAbilityDefName = string.Empty;
                        component.flightOnlyAbilityDefName = string.Empty;
                    }
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var sorted = new List<ModularAbilityDef>();
            var seenDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (abilities != null)
            {
                foreach (var ability in abilities)
                {
                    if (ability == null || string.IsNullOrWhiteSpace(ability.defName) || !seenDefNames.Add(ability.defName))
                    {
                        continue;
                    }

                    sorted.Add(ability);
                }
            }

            sorted.Sort((a, b) =>
            {
                string aLabel = string.IsNullOrWhiteSpace(a.label) ? a.defName : a.label;
                string bLabel = string.IsNullOrWhiteSpace(b.label) ? b.defName : b.label;
                return string.Compare(aLabel, bLabel, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var ability in sorted)
            {
                var localAbility = ability;
                string displayName = string.IsNullOrWhiteSpace(localAbility.label) ? localAbility.defName : localAbility.label;
                string label = $"{displayName} ({localAbility.defName})";
                options.Add(new FloatMenuOption(label, () =>
                {
                    switch (component.type)
                    {
                        case AbilityRuntimeComponentType.FlightOnlyFollowup:
                            component.requiredFlightSourceAbilityDefName = localAbility.defName;
                            break;
                        case AbilityRuntimeComponentType.SlotOverrideWindow:
                            component.comboTargetAbilityDefName = localAbility.defName;
                            break;
                        default:
                            component.overrideAbilityDefName = localAbility.defName;
                            break;
                    }
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

        private void ShowWeatherDefSelectorForRuntime(AbilityRuntimeComponentConfig component)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    component.weatherDefName = string.Empty;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var defs = DefDatabase<WeatherDef>.AllDefsListForReading;
            var sorted = new List<WeatherDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var weatherDef in sorted)
            {
                var localDef = weatherDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    component.weatherDefName = localDef.defName;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
