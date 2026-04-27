using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;

namespace CharacterStudio.Abilities
{
    public static class AbilityGrantUtility
    {
        public static event Action<Pawn, IReadOnlyCollection<string>>? AbilitiesGrantedGlobal;
        public static event Action<Pawn, IReadOnlyCollection<string>>? AbilitiesRevokedGlobal;

        private static readonly Dictionary<int, HashSet<string>> grantedAbilityNames = new Dictionary<int, HashSet<string>>();
        private static readonly Dictionary<string, AbilityDef> runtimeAbilityDefs = new Dictionary<string, AbilityDef>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> runtimeAbilityFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ModularAbilityDef> runtimeAbilitySourceDefs = new Dictionary<string, ModularAbilityDef>(StringComparer.OrdinalIgnoreCase);

        private const int MinAbilityCooldownTicks = 30;

        public static void GrantAbilitiesToPawn(Pawn pawn, IEnumerable<ModularAbilityDef> abilities)
        {
            if (pawn == null || abilities == null) return;
            if (pawn.abilities == null) return;

            var grantedSet = GetOrCreateGrantedSet(pawn);
            HashSet<string> desiredAbilityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> newlyGrantedAbilityNames = new List<string>();

            foreach (var modAbility in abilities)
            {
                if (modAbility == null || string.IsNullOrEmpty(modAbility.defName)) continue;

                desiredAbilityNames.Add(modAbility.defName);

                string fingerprint = BuildAbilityFingerprint(modAbility);
                bool fingerprintChanged = false;
                if (runtimeAbilityFingerprints.TryGetValue(modAbility.defName, out var oldFingerprint))
                {
                    fingerprintChanged = oldFingerprint != fingerprint;
                }

                var abilityDef = GetOrBuildRuntimeAbilityDef(modAbility);
                if (abilityDef == null) continue;

                var existingAbility = pawn.abilities.GetAbility(abilityDef);

                int savedCooldown = 0;
                if (existingAbility != null && fingerprintChanged)
                {
                    savedCooldown = existingAbility.CooldownTicksRemaining;
                    pawn.abilities.RemoveAbility(abilityDef);
                    existingAbility = null;
                }

                if (existingAbility != null)
                {
                    grantedSet.Add(modAbility.defName);
                    continue;
                }

                pawn.abilities.GainAbility(abilityDef);
                var newAbility = pawn.abilities.GetAbility(abilityDef);
                if (newAbility != null && fingerprintChanged)
                {
                    if (savedCooldown > 0) newAbility.StartCooldown(savedCooldown);
                }

                grantedSet.Add(modAbility.defName);
                newlyGrantedAbilityNames.Add(modAbility.defName);
            }

            List<string> revokedAbilityNames = grantedSet
                .Where(defName => !desiredAbilityNames.Contains(defName))
                .ToList();
            foreach (var defName in revokedAbilityNames)
            {
                if (runtimeAbilityDefs.TryGetValue(defName, out var abilityDef))
                    pawn.abilities.RemoveAbility(abilityDef);
                grantedSet.Remove(defName);
            }

            if (newlyGrantedAbilityNames.Count > 0)
                AbilitiesGrantedGlobal?.Invoke(pawn, newlyGrantedAbilityNames);
            if (revokedAbilityNames.Count > 0)
                AbilitiesRevokedGlobal?.Invoke(pawn, revokedAbilityNames);
        }

        public static void RevokeAllCSAbilitiesFromPawn(Pawn pawn)
        {
            if (pawn == null) return;
            if (!grantedAbilityNames.TryGetValue(pawn.thingIDNumber, out var grantedSet)) return;

            List<string> revokedAbilityNames = grantedSet.ToList();
            foreach (var defName in revokedAbilityNames)
            {
                if (runtimeAbilityDefs.TryGetValue(defName, out var abilityDef))
                    pawn.abilities.RemoveAbility(abilityDef);
            }
            grantedAbilityNames.Remove(pawn.thingIDNumber);
            if (revokedAbilityNames.Count > 0)
                AbilitiesRevokedGlobal?.Invoke(pawn, revokedAbilityNames);
        }

        public static IReadOnlyCollection<string> GetGrantedAbilityNames(Pawn pawn)
        {
            if (pawn == null) return Array.Empty<string>();
            return grantedAbilityNames.TryGetValue(pawn.thingIDNumber, out var set) ? set : (IReadOnlyCollection<string>)Array.Empty<string>();
        }

        public static AbilityDef? GetRuntimeAbilityDef(string modAbilityDefName)
        {
            return runtimeAbilityDefs.TryGetValue(modAbilityDefName, out var def) ? def : null;
        }

        public static ModularAbilityDef? GetRuntimeAbilitySourceDef(string modAbilityDefName)
        {
            return runtimeAbilitySourceDefs.TryGetValue(modAbilityDefName, out var def) ? def.Clone() : null;
        }

        public static AbilityDef? GetOrBuildRuntimeAbilityDef(ModularAbilityDef modAbility)
        {
            string key = modAbility.defName;
            string fingerprint = BuildAbilityFingerprint(modAbility);
            RegisterModularAbilityDefIfMissing(modAbility);

            if (runtimeAbilityDefs.TryGetValue(key, out var cached))
            {
                if (runtimeAbilityFingerprints.TryGetValue(key, out var oldFingerprint) && oldFingerprint == fingerprint)
                {
                    CacheRuntimeAbilitySource(key, modAbility);
                    return cached;
                }
                ConfigureRuntimeAbilityDef(cached, modAbility, key);
                runtimeAbilityFingerprints[key] = fingerprint;
                CacheRuntimeAbilitySource(key, modAbility);
                return cached;
            }

            var abilityDef = new AbilityDef();
            ConfigureRuntimeAbilityDef(abilityDef, modAbility, key);
            DefDatabase<AbilityDef>.Add(abilityDef);
            runtimeAbilityDefs[key] = abilityDef;
            runtimeAbilityFingerprints[key] = fingerprint;
            CacheRuntimeAbilitySource(key, modAbility);
            return abilityDef;
        }

        private static void ConfigureRuntimeAbilityDef(AbilityDef abilityDef, ModularAbilityDef modAbility, string key)
        {
            int resolvedCd = Mathf.Max((int)modAbility.cooldownTicks, MinAbilityCooldownTicks);
            abilityDef.defName = "CS_RT_" + key;
            abilityDef.label = modAbility.label ?? key;
            abilityDef.description = modAbility.description ?? string.Empty;
            abilityDef.iconPath = string.IsNullOrEmpty(modAbility.iconPath) ? "UI/Designators/Strip" : modAbility.iconPath;
            abilityDef.cooldownTicksRange = new IntRange(resolvedCd, resolvedCd);
            abilityDef.charges = modAbility.charges;
            abilityDef.aiCanUse = modAbility.aiCanUse > 0.5f;

            AbilityCarrierType normalizedCarrier = modAbility.carrierType;
            AbilityTargetType normalizedTarget = modAbility.targetType;

            bool usesJumpVerb = modAbility.runtimeComponents != null
                && modAbility.runtimeComponents.Any(c => c != null && c.enabled
                    && (c.type == AbilityRuntimeComponentType.SmartJump || c.type == AbilityRuntimeComponentType.EShortJump));

            VerbProperties verbProps = new VerbProperties
            {
                verbClass = usesJumpVerb ? typeof(Verb_CastAbilityStraightJump) : typeof(Verb_CastAbility),
                range = Mathf.Max(modAbility.range, 1f),
                warmupTime = modAbility.warmupTicks / 60f,
                targetParams = BuildTargetingParameters(normalizedTarget)
            };

            if (normalizedCarrier == AbilityCarrierType.Self)
            {
                verbProps.range = 0f;
                verbProps.targetParams.canTargetSelf = true;
                verbProps.targetParams.canTargetPawns = false;
                verbProps.targetParams.canTargetLocations = false;
            }

            abilityDef.verbProperties = verbProps;

            var compProps = new CompProperties_AbilityModular
            {
                carrierType = normalizedCarrier,
                targetType = normalizedTarget,
                useRadius = modAbility.useRadius,
                radius = modAbility.radius,
                areaCenter = modAbility.areaCenter,
                areaShape = modAbility.areaShape,
                irregularAreaPattern = modAbility.irregularAreaPattern ?? string.Empty,
                range = modAbility.range,
                projectileDef = modAbility.projectileDef
            };

            if (modAbility.effects != null) compProps.effects.AddRange(modAbility.effects.Where(e => e != null).Select(e => e.Clone()));
            if (modAbility.visualEffects != null) compProps.visualEffects.AddRange(modAbility.visualEffects.Where(v => v != null).Select(v => v.Clone()));
            if (modAbility.runtimeComponents != null) compProps.runtimeComponents.AddRange(modAbility.runtimeComponents.Where(c => c != null).Select(c => c.Clone()));
            
            abilityDef.comps = new List<AbilityCompProperties> { compProps };
            abilityDef.ResolveReferences();
        }

        private static string BuildAbilityFingerprint(ModularAbilityDef modAbility)
        {
            var parts = new List<string>
            {
                modAbility.defName ?? string.Empty,
                modAbility.label ?? string.Empty,
                modAbility.description ?? string.Empty,
                modAbility.iconPath ?? string.Empty,
                modAbility.cooldownTicks.ToString("F3"),
                modAbility.warmupTicks.ToString("F3"),
                modAbility.charges.ToString(),
                modAbility.aiCanUse.ToString("F3"),
                modAbility.carrierType.ToString(),
                modAbility.targetType.ToString(),
                modAbility.useRadius.ToString(),
                modAbility.areaCenter.ToString(),
                modAbility.areaShape.ToString(),
                modAbility.irregularAreaPattern ?? string.Empty,
                modAbility.range.ToString("F3"),
                modAbility.radius.ToString("F3"),
                modAbility.projectileDef?.defName ?? string.Empty
            };

            if (modAbility.effects != null)
            {
                foreach (var effect in modAbility.effects)
                {
                    if (effect == null) continue;
                    parts.Add($"E:{effect.type}|{effect.amount:F3}|{effect.duration:F3}|{effect.chance:F3}|{effect.damageDef?.defName}|{effect.hediffDef?.defName}|{effect.summonKind?.defName}|{effect.summonCount}|{effect.summonFactionType}|{effect.summonFactionDefName}|{effect.summonFactionDef?.defName}|{effect.controlMode}|{effect.controlMoveDistance}|{effect.terraformMode}|{effect.terraformThingDef?.defName}|{effect.terraformTerrainDef?.defName}|{effect.terraformSpawnCount}|{effect.canHurtSelf}|{effect.weatherDefName}|{effect.weatherDurationTicks}|{effect.weatherTransitionTicks}");
                }
            }

            if (modAbility.visualEffects != null)
            {
                foreach (var vfx in modAbility.visualEffects)
                {
                    if (vfx == null) continue;
                    vfx.NormalizeLegacyData();
                    vfx.SyncLegacyFields();
                    parts.Add($"V:{vfx.type}|{vfx.sourceMode}|{vfx.textureSource}|{vfx.presetDefName}|{vfx.customTexturePath}|{vfx.target}|{vfx.trigger}|{vfx.delayTicks}|{vfx.displayDurationTicks}|{vfx.linkedExpression}|{vfx.linkedExpressionDurationTicks}|{vfx.linkedPupilBrightnessOffset:F3}|{vfx.linkedPupilContrastOffset:F3}|{vfx.scale:F3}|{vfx.drawSize:F3}|{vfx.useCasterFacing}|{vfx.forwardOffset:F3}|{vfx.sideOffset:F3}|{vfx.heightOffset:F3}|{vfx.rotation:F3}|{vfx.textureScale.x:F3},{vfx.textureScale.y:F3}|{vfx.repeatCount}|{vfx.repeatIntervalTicks}|{vfx.offset.x:F3},{vfx.offset.y:F3},{vfx.offset.z:F3}|{vfx.playSound}|{vfx.soundDefName}|{vfx.soundDelayTicks}|{vfx.soundVolume:F3}|{vfx.soundPitch:F3}|{vfx.attachToPawn}|{vfx.attachToTargetCell}|{vfx.enabled}");
                }
            }

            if (modAbility.runtimeComponents != null)
            {
                foreach (var component in modAbility.runtimeComponents)
                {
                    if (component == null) continue;
                    parts.Add($"R:{component.type}|{component.enabled}|{component.comboWindowTicks}|{component.cooldownTicks}|{component.jumpDistance}|{component.findCellRadius}|{component.triggerAbilityEffectsAfterJump}|{component.useMouseTargetCell}|{component.smartCastOffsetCells}|{component.smartCastClampToMaxDistance}|{component.smartCastAllowFallbackForward}|{component.overrideHotkeySlot}|{component.overrideAbilityDefName}|{component.overrideDurationTicks}|{component.followupCooldownHotkeySlot}|{component.followupCooldownTicks}|{component.requiredStacks}|{component.delayTicks}|{component.wave1Radius:F3}|{component.wave1Damage:F3}|{component.wave2Radius:F3}|{component.wave2Damage:F3}|{component.wave3Radius:F3}|{component.wave3Damage:F3}|{component.waveDamageDef?.defName}|{component.pulseIntervalTicks}|{component.pulseTotalTicks}|{component.pulseStartsImmediately}|{component.killRefreshHotkeySlot}|{component.killRefreshCooldownPercent:F3}|{component.shieldMaxDamage:F3}|{component.shieldDurationTicks:F3}|{component.shieldHealRatio:F3}|{component.shieldBonusDamageRatio:F3}|{component.maxBounceCount}|{component.bounceRange:F3}|{component.bounceDamageFalloff:F3}|{component.executeThresholdPercent:F3}|{component.executeBonusDamageScale:F3}|{component.missingHealthBonusPerTenPercent:F3}|{component.missingHealthBonusMaxScale:F3}|{component.fullHealthThresholdPercent:F3}|{component.fullHealthBonusDamageScale:F3}|{component.nearbyEnemyBonusMaxTargets}|{component.nearbyEnemyBonusPerTarget:F3}|{component.nearbyEnemyBonusRadius:F3}|{component.isolatedTargetRadius:F3}|{component.isolatedTargetBonusDamageScale:F3}|{component.markDurationTicks}|{component.markMaxStacks}|{component.markDetonationDamage:F3}|{component.markDamageDef?.defName}|{component.comboStackWindowTicks}|{component.comboStackMax}|{component.comboStackBonusDamagePerStack:F3}|{component.slowFieldDurationTicks}|{component.slowFieldRadius:F3}|{component.slowFieldHediffDefName}|{component.pierceMaxTargets}|{component.pierceBonusDamagePerTarget:F3}|{component.pierceSearchRange:F3}|{component.dashDistance:F3}|{component.dashLanding}|{component.dashEmpowerDurationTicks}|{component.dashEmpowerBonusDamageScale:F3}|{component.hitHealAmount:F3}|{component.hitHealRatio:F3}|{component.refundHotkeySlot}|{component.hitCooldownRefundPercent:F3}|{component.splitProjectileCount}|{component.splitDamageScale:F3}|{component.splitSearchRange:F3}|{component.flightDurationTicks}|{component.flightHeightFactor:F3}|{component.timeStopDurationTicks}|{component.freezeVisualsDuringTimeStop}");
                }
            }

            return string.Join("||", parts);
        }

        private static TargetingParameters BuildTargetingParameters(AbilityTargetType targetType)
        {
            return targetType switch
            {
                AbilityTargetType.Cell => new TargetingParameters { canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false, canTargetSelf = false },
                AbilityTargetType.Entity => new TargetingParameters { canTargetPawns = true, canTargetBuildings = true, canTargetLocations = false, canTargetSelf = false },
                _ => new TargetingParameters { canTargetSelf = true, canTargetPawns = false, canTargetBuildings = false, canTargetLocations = false }
            };
        }

        private static void RegisterModularAbilityDefIfMissing(ModularAbilityDef modAbility)
        {
            if (modAbility == null || string.IsNullOrWhiteSpace(modAbility.defName)) return;
            if (DefDatabase<ModularAbilityDef>.GetNamedSilentFail(modAbility.defName) != null) return;
            DefDatabase<ModularAbilityDef>.Add(modAbility);
        }

        private static void CacheRuntimeAbilitySource(string key, ModularAbilityDef modAbility)
        {
            runtimeAbilitySourceDefs[key] = modAbility.Clone();
        }

        public static void UpdatePawnAbilitiesFromLoadout(Pawn pawn)
        {
            if (pawn == null) return;
            var loadout = AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(pawn);
            if (loadout == null) { RevokeAllCSAbilitiesFromPawn(pawn); return; }
            GrantAbilitiesToPawn(pawn, loadout.abilities);
        }
        private static HashSet<string> GetOrCreateGrantedSet(Pawn pawn)
        {
            if (!grantedAbilityNames.TryGetValue(pawn.thingIDNumber, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                grantedAbilityNames[pawn.thingIDNumber] = set;
            }
            return set;
        }

        public static void WarmupAllRuntimeAbilityDefs()
        {
            foreach (var modAbility in DefDatabase<ModularAbilityDef>.AllDefsListForReading)
            {
                if (modAbility != null && !string.IsNullOrWhiteSpace(modAbility.defName))
                    GetOrBuildRuntimeAbilityDef(modAbility);
            }
        }

        public static void WarmupRuntimeAbilityDefs(IEnumerable<ModularAbilityDef>? abilities)
        {
            if (abilities == null)
            {
                return;
            }

            foreach (ModularAbilityDef? modAbility in abilities)
            {
                if (modAbility != null && !string.IsNullOrWhiteSpace(modAbility.defName))
                {
                    GetOrBuildRuntimeAbilityDef(modAbility);
                }
            }
        }
    }
}
