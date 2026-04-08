using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    public sealed class CharacterAbilityLoadout : IExposable
    {
        public List<ModularAbilityDef> abilities = new List<ModularAbilityDef>();
        public SkinAbilityHotkeyConfig hotkeys = new SkinAbilityHotkeyConfig();
        private List<string> serializedAbilityDefNames = new List<string>();

        public CharacterAbilityLoadout Clone()
        {
            var clone = new CharacterAbilityLoadout
            {
                hotkeys = hotkeys?.Clone() ?? new SkinAbilityHotkeyConfig(),
                serializedAbilityDefNames = GetAbilityDefNamesSnapshot()
            };

            if (abilities != null)
            {
                foreach (var ability in abilities)
                {
                    if (ability != null)
                    {
                        clone.abilities.Add(ability.Clone());
                    }
                }
            }

            if (clone.abilities.Count > 0)
            {
                clone.SyncSerializedAbilityDefNamesFromAbilities();
            }

            return clone;
        }

        public void ExposeData()
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars)
            {
                SyncSerializedAbilityDefNamesFromAbilities();
            }

            Scribe_Collections.Look(ref serializedAbilityDefNames, "abilityDefNames", LookMode.Value);
            Scribe_Deep.Look(ref hotkeys, "hotkeys");

            serializedAbilityDefNames ??= new List<string>();
            serializedAbilityDefNames = serializedAbilityDefNames
                .Where(static defName => !string.IsNullOrWhiteSpace(defName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            abilities ??= new List<ModularAbilityDef>();
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                abilities.Clear();
            }
            else
            {
                abilities.RemoveAll(static ability => ability == null || string.IsNullOrWhiteSpace(ability.defName));
                SyncSerializedAbilityDefNamesFromAbilities();
            }

            hotkeys ??= new SkinAbilityHotkeyConfig();
        }

        public void RehydrateAbilities(IEnumerable<ModularAbilityDef>? preferredAbilities = null)
        {
            List<ModularAbilityDef> resolvedAbilities = new List<ModularAbilityDef>();
            List<ModularAbilityDef> preferredAbilityList = preferredAbilities?
                .Where(static ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                .ToList() ?? new List<ModularAbilityDef>();

            foreach (string defName in GetAbilityDefNamesSnapshot())
            {
                ModularAbilityDef? source = preferredAbilityList
                    .FirstOrDefault(ability => string.Equals(ability.defName, defName, StringComparison.OrdinalIgnoreCase))
                    ?? DefDatabase<ModularAbilityDef>.GetNamedSilentFail(defName);

                if (source != null)
                {
                    resolvedAbilities.Add(source.Clone());
                }
            }

            abilities = resolvedAbilities;
            SyncSerializedAbilityDefNamesFromAbilities();
        }

        public void EnsureAbilitiesRehydrated(IEnumerable<ModularAbilityDef>? preferredAbilities = null)
        {
            bool hasResolvedAbilities = abilities != null
                && abilities.Any(static ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName));
            if (hasResolvedAbilities)
            {
                SyncSerializedAbilityDefNamesFromAbilities();
                return;
            }

            bool hasSerializedAbilities = serializedAbilityDefNames != null
                && serializedAbilityDefNames.Any(static defName => !string.IsNullOrWhiteSpace(defName));
            if (hasSerializedAbilities)
            {
                RehydrateAbilities(preferredAbilities);
            }
        }

        private List<string> GetAbilityDefNamesSnapshot()
        {
            if (serializedAbilityDefNames == null || serializedAbilityDefNames.Count == 0)
            {
                SyncSerializedAbilityDefNamesFromAbilities();
            }

            return serializedAbilityDefNames?
                .Where(static defName => !string.IsNullOrWhiteSpace(defName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }

        private void SyncSerializedAbilityDefNamesFromAbilities()
        {
            serializedAbilityDefNames = abilities?
                .Where(static ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                .Select(static ability => ability.defName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }
    }

    public sealed class VisibleAbilitySlotEntry
    {
        public string slotId = string.Empty;
        public string slotBadge = string.Empty;
        public string abilityDefName = string.Empty;
        public bool isCombo;
        public bool isOverride;
        public bool isSecondStage;
        public int qModeIndex = -1;
        public int rStackCount = 0;
        public int charges = 1;
        public string runtimeTag = string.Empty;
        public string runtimeSummary = string.Empty;
    }

    public static class AbilityLoadoutRuntimeUtility
    {
        private enum VisibleAbilitySlot
        {
            Q,
            W,
            E,
            R,
            T,
            A,
            S,
            D,
            F,
            Z,
            X,
            C,
            V
        }

        public static CharacterAbilityLoadout CreateLoadout(IEnumerable<ModularAbilityDef>? abilities, SkinAbilityHotkeyConfig? hotkeys)
        {
            var loadout = new CharacterAbilityLoadout
            {
                hotkeys = hotkeys?.Clone() ?? new SkinAbilityHotkeyConfig()
            };

            if (abilities != null)
            {
                foreach (var ability in abilities)
                {
                    if (ability != null)
                    {
                        loadout.abilities.Add(ability.Clone());
                    }
                }
            }

            return loadout;
        }

        public static CharacterAbilityLoadout? GetExplicitLoadout(Pawn? pawn)
        {
            return pawn?.GetComp<CompPawnSkin>()?.ActiveAbilityLoadout;
        }

        public static CharacterAbilityLoadout? GetEffectiveLoadout(Pawn? pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            var comp = pawn.GetComp<CompPawnSkin>();
            CharacterAbilityLoadout? explicitLoadout = comp?.ActiveAbilityLoadout;

            PawnSkinDef? skin = comp?.ActiveSkin;
            explicitLoadout?.EnsureAbilitiesRehydrated(skin?.abilities);

            CharacterAbilityLoadout? skinLoadout = skin == null
                ? null
                : CreateLoadout(skin.abilities, skin.abilityHotkeys);

            if (explicitLoadout == null)
            {
                return MergeEquipmentAbilitiesIntoLoadout(pawn, skinLoadout);
            }

            if (skinLoadout == null)
            {
                return MergeEquipmentAbilitiesIntoLoadout(pawn, explicitLoadout);
            }

            bool explicitHasAbilities = HasAbilities(explicitLoadout);
            bool explicitHasConfiguredHotkeys = HasConfiguredHotkeys(explicitLoadout.hotkeys);
            bool skinHasConfiguredHotkeys = HasConfiguredHotkeys(skinLoadout.hotkeys);

            if (!explicitHasAbilities)
            {
                return MergeEquipmentAbilitiesIntoLoadout(pawn, skinLoadout);
            }

            if (explicitHasConfiguredHotkeys || !skinHasConfiguredHotkeys)
            {
                return MergeEquipmentAbilitiesIntoLoadout(pawn, explicitLoadout);
            }

            var repairedLoadout = explicitLoadout.Clone();
            repairedLoadout.hotkeys = skinLoadout.hotkeys?.Clone() ?? new SkinAbilityHotkeyConfig();
            return MergeEquipmentAbilitiesIntoLoadout(pawn, repairedLoadout);
        }

        private static CharacterAbilityLoadout? MergeEquipmentAbilitiesIntoLoadout(Pawn? pawn, CharacterAbilityLoadout? baseLoadout)
        {
            if (pawn == null)
            {
                return baseLoadout;
            }

            List<ModularAbilityDef> equipmentAbilities = CollectEquipmentAbilities(pawn);
            if (equipmentAbilities.Count == 0)
            {
                return baseLoadout;
            }

            CharacterAbilityLoadout merged = baseLoadout?.Clone() ?? new CharacterAbilityLoadout();
            merged.abilities ??= new List<ModularAbilityDef>();

            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModularAbilityDef ability in merged.abilities)
            {
                if (ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                {
                    existing.Add(ability.defName);
                }
            }

            foreach (ModularAbilityDef ability in equipmentAbilities)
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.defName) || !existing.Add(ability.defName))
                {
                    continue;
                }

                merged.abilities.Add(ability.Clone());
            }

            return merged;
        }

        private static List<ModularAbilityDef> CollectEquipmentAbilities(Pawn pawn)
        {
            var result = new List<ModularAbilityDef>();
            List<Apparel>? wornApparel = pawn.apparel?.WornApparel;
            if (wornApparel == null || wornApparel.Count == 0)
            {
                return result;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Apparel apparel in wornApparel)
            {
                DefModExtension_EquipmentRender? extension = apparel?.def?.GetModExtension<DefModExtension_EquipmentRender>();
                if (extension?.abilityDefNames == null)
                {
                    continue;
                }

                foreach (string defName in extension.abilityDefNames)
                {
                    if (string.IsNullOrWhiteSpace(defName) || !seen.Add(defName))
                    {
                        continue;
                    }

                    ModularAbilityDef resolved = DefDatabase<ModularAbilityDef>.GetNamedSilentFail(defName);
                    if (resolved != null)
                    {
                        result.Add(resolved);
                    }
                }
            }

            return result;
        }

        private static bool HasAbilities(CharacterAbilityLoadout? loadout)
        {
            return loadout?.abilities != null
                && loadout.abilities.Any(ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName));
        }

        private static bool HasConfiguredHotkeys(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null || !hotkeys.enabled)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(hotkeys.qAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.wAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.eAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.rAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.tAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.aAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.sAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.dAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.fAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.zAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.xAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.cAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeys.vAbilityDefName);
        }

        public static void SetExplicitLoadout(Pawn? pawn, CharacterAbilityLoadout? loadout)
        {
            var comp = pawn?.GetComp<CompPawnSkin>();
            if (comp == null)
            {
                return;
            }

            comp.ActiveAbilityLoadout = loadout;
        }

        public static void ApplyExplicitLoadout(Pawn? pawn, IEnumerable<ModularAbilityDef>? abilities, SkinAbilityHotkeyConfig? hotkeys)
        {
            if (pawn == null)
            {
                return;
            }

            CharacterAbilityLoadout loadout = CreateLoadout(abilities, hotkeys);
            SetExplicitLoadout(pawn, loadout);
            AbilityGrantUtility.GrantAbilitiesToPawn(pawn, loadout.abilities);
        }

        public static void ClearExplicitLoadout(Pawn? pawn)
        {
            var comp = pawn?.GetComp<CompPawnSkin>();
            if (comp == null)
            {
                return;
            }

            comp.ActiveAbilityLoadout = null;
        }

        public static void GrantEffectiveLoadoutToPawn(Pawn? pawn)
        {
            if (pawn == null)
            {
                return;
            }

            CharacterAbilityLoadout? loadout = GetEffectiveLoadout(pawn);
            if (loadout?.abilities == null || loadout.abilities.Count == 0)
            {
                AbilityGrantUtility.RevokeAllCSAbilitiesFromPawn(pawn);
                return;
            }

            AbilityGrantUtility.GrantAbilitiesToPawn(pawn, loadout.abilities);
        }

        public static ModularAbilityDef? ResolveAbilityByDefName(Pawn? pawn, string abilityDefName)
        {
            return ResolveAbilityByDefName(GetEffectiveLoadout(pawn), abilityDefName);
        }

        public static ModularAbilityDef? ResolveAbilityByDefName(CharacterAbilityLoadout? loadout, string abilityDefName)
        {
            if (string.IsNullOrWhiteSpace(abilityDefName))
            {
                return null;
            }

            ModularAbilityDef? resolved = loadout?.abilities?
                .FirstOrDefault(a => a != null && string.Equals(a.defName, abilityDefName, StringComparison.OrdinalIgnoreCase));

            if (resolved != null)
            {
                return resolved;
            }

            return DefDatabase<ModularAbilityDef>.GetNamedSilentFail(abilityDefName);
        }

        public static IEnumerable<VisibleAbilitySlotEntry> EnumerateVisibleAbilitySlots(Pawn? pawn)
        {
            if (pawn == null)
            {
                yield break;
            }

            var comp = pawn.GetComp<CompPawnSkin>();
            CharacterAbilityLoadout? loadout = GetEffectiveLoadout(pawn);
            if (loadout?.abilities == null || loadout.abilities.Count == 0)
            {
                yield break;
            }

            SkinAbilityHotkeyConfig? hotkeys = loadout.hotkeys;
            if (comp == null || hotkeys == null || !hotkeys.enabled)
            {
                yield break;
            }

            int tick = Find.TickManager?.TicksGame ?? 0;
            foreach (VisibleAbilitySlot slot in new[] { VisibleAbilitySlot.Q, VisibleAbilitySlot.W, VisibleAbilitySlot.E, VisibleAbilitySlot.R, VisibleAbilitySlot.T, VisibleAbilitySlot.A, VisibleAbilitySlot.S, VisibleAbilitySlot.D, VisibleAbilitySlot.F, VisibleAbilitySlot.Z, VisibleAbilitySlot.X, VisibleAbilitySlot.C, VisibleAbilitySlot.V })
            {
                string defName = ResolveVisibleAbilityDefName(comp, loadout, slot, tick);
                if (string.IsNullOrWhiteSpace(defName))
                {
                    continue;
                }

                yield return BuildVisibleAbilitySlotEntry(comp, loadout, slot, defName, tick);
            }
        }

        public static IEnumerable<ModularAbilityDef> EnumerateVisibleAbilities(Pawn? pawn)
        {
            CharacterAbilityLoadout? loadout = GetEffectiveLoadout(pawn);
            if (loadout?.abilities == null || loadout.abilities.Count == 0)
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (VisibleAbilitySlotEntry entry in EnumerateVisibleAbilitySlots(pawn))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.abilityDefName) || !yielded.Add(entry.abilityDefName))
                {
                    continue;
                }

                ModularAbilityDef? ability = ResolveAbilityByDefName(loadout, entry.abilityDefName);
                if (ability != null)
                {
                    yield return ability;
                }
            }
        }

        private static VisibleAbilitySlotEntry BuildVisibleAbilitySlotEntry(
            CompPawnSkin comp,
            CharacterAbilityLoadout loadout,
            VisibleAbilitySlot slot,
            string defName,
            int tick)
        {
            string overrideDefName = GetActiveSlotOverrideAbilityDefName(comp, slot, tick);
            bool isOverride = !string.IsNullOrWhiteSpace(overrideDefName)
                && string.Equals(overrideDefName, defName, StringComparison.OrdinalIgnoreCase);

            bool isCombo = !isOverride
                && tick <= comp.slotOverrideWindowEndTick
                && !string.IsNullOrWhiteSpace(comp.slotOverrideWindowSlotId)
                && string.Equals(comp.slotOverrideWindowSlotId, slot.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(comp.slotOverrideWindowAbilityDefName)
                && string.Equals(comp.slotOverrideWindowAbilityDefName, defName, StringComparison.OrdinalIgnoreCase);

            bool isSecondStage = slot == VisibleAbilitySlot.R && comp.rSecondStageReady;

            return new VisibleAbilitySlotEntry
            {
                slotId = slot.ToString(),
                slotBadge = BuildSlotBadge(comp, slot, isCombo, isSecondStage),
                abilityDefName = defName,
                isCombo = isCombo,
                isOverride = isOverride,
                isSecondStage = isSecondStage,
                qModeIndex = -1,
                rStackCount = slot == VisibleAbilitySlot.R ? Math.Max(0, comp.rStackCount) : 0,
                charges = Math.Max(1, ResolveAbilityByDefName(loadout, defName)?.charges ?? 1),
                runtimeTag = BuildRuntimeTag(ResolveAbilityByDefName(loadout, defName)),
                runtimeSummary = BuildRuntimeSummary(ResolveAbilityByDefName(loadout, defName))
            };
        }

        private static string BuildRuntimeTag(ModularAbilityDef? ability)
        {
            if (ability?.runtimeComponents == null)
            {
                return string.Empty;
            }

            if (ability.runtimeComponents.Any(c => c != null && c.enabled && (c.type == AbilityRuntimeComponentType.SmartJump || c.type == AbilityRuntimeComponentType.EShortJump))) return "跃";
            if (ability.runtimeComponents.Any(c => c != null && c.enabled && c.type == AbilityRuntimeComponentType.ShieldAbsorb)) return "盾";
            if (ability.runtimeComponents.Any(c => c != null && c.enabled && c.type == AbilityRuntimeComponentType.RStackDetonation)) return "爆";
            if (ability.runtimeComponents.Any(c => c != null && c.enabled && c.type == AbilityRuntimeComponentType.ChainBounce)) return "链";
            if (ability.runtimeComponents.Any(c => c != null && c.enabled && c.type == AbilityRuntimeComponentType.PeriodicPulse)) return "脉";
            if (ability.runtimeComponents.Any(c => c != null && c.enabled && c.type == AbilityRuntimeComponentType.SlotOverrideWindow)) return "连";
            return string.Empty;
        }

        private static string BuildRuntimeSummary(ModularAbilityDef? ability)
        {
            if (ability?.runtimeComponents == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            foreach (AbilityRuntimeComponentConfig component in ability.runtimeComponents.Where(static c => c != null && c.enabled).Take(3))
            {
                string key = $"CS_Ability_RuntimeComponentType_{component.type}";
                parts.Add(key.CanTranslate() ? (string)key.Translate() : component.type.ToString());
            }

            return string.Join(" / ", parts);
        }

        private static string BuildSlotBadge(CompPawnSkin comp, VisibleAbilitySlot slot, bool isCombo, bool isSecondStage)
        {
            switch (slot)
            {
                case VisibleAbilitySlot.Q:
                    return "Q";
                case VisibleAbilitySlot.W:
                    return isCombo ? "W*" : "W";
                case VisibleAbilitySlot.E:
                    return "E";
                case VisibleAbilitySlot.T:
                    return "T";
                case VisibleAbilitySlot.A:
                    return "A";
                case VisibleAbilitySlot.S:
                    return "S";
                case VisibleAbilitySlot.D:
                    return "D";
                case VisibleAbilitySlot.F:
                    return "F";
                case VisibleAbilitySlot.Z:
                    return "Z";
                case VisibleAbilitySlot.X:
                    return "X";
                case VisibleAbilitySlot.C:
                    return "C";
                case VisibleAbilitySlot.V:
                    return "V";
                default:
                    return isSecondStage ? "R2" : "R";
            }
        }

        private static string ResolveVisibleAbilityDefName(CompPawnSkin comp, CharacterAbilityLoadout loadout, VisibleAbilitySlot slot, int tick)
        {
            SkinAbilityHotkeyConfig? hotkeys = loadout.hotkeys;
            if (hotkeys == null)
            {
                return string.Empty;
            }

            string overrideDefName = GetActiveSlotOverrideAbilityDefName(comp, slot, tick);
            if (!string.IsNullOrWhiteSpace(overrideDefName))
            {
                return overrideDefName;
            }

            if (tick <= comp.slotOverrideWindowEndTick
                && !string.IsNullOrWhiteSpace(comp.slotOverrideWindowSlotId)
                && string.Equals(comp.slotOverrideWindowSlotId, slot.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(comp.slotOverrideWindowAbilityDefName))
            {
                return comp.slotOverrideWindowAbilityDefName;
            }

            return slot switch
            {
                VisibleAbilitySlot.Q => hotkeys.qAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.W => hotkeys.wAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.E => hotkeys.eAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.T => hotkeys.tAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.A => hotkeys.aAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.S => hotkeys.sAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.D => hotkeys.dAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.F => hotkeys.fAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.Z => hotkeys.zAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.X => hotkeys.xAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.C => hotkeys.cAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.V => hotkeys.vAbilityDefName ?? string.Empty,
                _ => hotkeys.rAbilityDefName ?? string.Empty
            };
        }

        private static string GetActiveSlotOverrideAbilityDefName(CompPawnSkin comp, VisibleAbilitySlot slot, int tick)
        {
            string abilityDefName;
            int expireTick;

            switch (slot)
            {
                case VisibleAbilitySlot.Q:
                    abilityDefName = comp.qOverrideAbilityDefName;
                    expireTick = comp.qOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.W:
                    abilityDefName = comp.wOverrideAbilityDefName;
                    expireTick = comp.wOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.E:
                    abilityDefName = comp.eOverrideAbilityDefName;
                    expireTick = comp.eOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.T:
                    abilityDefName = comp.tOverrideAbilityDefName;
                    expireTick = comp.tOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.A:
                    abilityDefName = comp.aOverrideAbilityDefName;
                    expireTick = comp.aOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.S:
                    abilityDefName = comp.sOverrideAbilityDefName;
                    expireTick = comp.sOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.D:
                    abilityDefName = comp.dOverrideAbilityDefName;
                    expireTick = comp.dOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.F:
                    abilityDefName = comp.fOverrideAbilityDefName;
                    expireTick = comp.fOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.Z:
                    abilityDefName = comp.zOverrideAbilityDefName;
                    expireTick = comp.zOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.X:
                    abilityDefName = comp.xOverrideAbilityDefName;
                    expireTick = comp.xOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.C:
                    abilityDefName = comp.cOverrideAbilityDefName;
                    expireTick = comp.cOverrideExpireTick;
                    break;
                case VisibleAbilitySlot.V:
                    abilityDefName = comp.vOverrideAbilityDefName;
                    expireTick = comp.vOverrideExpireTick;
                    break;
                default:
                    abilityDefName = comp.rOverrideAbilityDefName;
                    expireTick = comp.rOverrideExpireTick;
                    break;
            }

            if (string.IsNullOrWhiteSpace(abilityDefName))
            {
                return string.Empty;
            }

            if (expireTick >= 0 && tick > expireTick)
            {
                return string.Empty;
            }

            return abilityDefName;
        }
    }
}
