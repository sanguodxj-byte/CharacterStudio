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
    }

    public static class AbilityLoadoutRuntimeUtility
    {
        private enum VisibleAbilitySlot
        {
            Q,
            W,
            E,
            R
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
                || !string.IsNullOrWhiteSpace(hotkeys.wComboAbilityDefName);
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
                foreach (var ability in loadout.abilities)
                {
                    if (ability == null || string.IsNullOrWhiteSpace(ability.defName))
                    {
                        continue;
                    }

                    yield return new VisibleAbilitySlotEntry
                    {
                        slotId = string.Empty,
                        slotBadge = string.Empty,
                        abilityDefName = ability.defName
                    };
                }

                yield break;
            }

            int tick = Find.TickManager?.TicksGame ?? 0;
            foreach (VisibleAbilitySlot slot in new[] { VisibleAbilitySlot.Q, VisibleAbilitySlot.W, VisibleAbilitySlot.E, VisibleAbilitySlot.R })
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

            string comboAbilityDefName = loadout.hotkeys?.wComboAbilityDefName ?? string.Empty;
            bool isCombo = slot == VisibleAbilitySlot.W
                && !isOverride
                && tick <= comp.qComboWindowEndTick
                && !string.IsNullOrWhiteSpace(comboAbilityDefName)
                && string.Equals(comboAbilityDefName, defName, StringComparison.OrdinalIgnoreCase);

            bool isSecondStage = slot == VisibleAbilitySlot.R && comp.rSecondStageReady;

            return new VisibleAbilitySlotEntry
            {
                slotId = slot.ToString(),
                slotBadge = BuildSlotBadge(comp, slot, isCombo, isSecondStage),
                abilityDefName = defName,
                isCombo = isCombo,
                isOverride = isOverride,
                isSecondStage = isSecondStage,
                qModeIndex = slot == VisibleAbilitySlot.Q ? Math.Max(0, comp.qHotkeyModeIndex) : -1,
                rStackCount = slot == VisibleAbilitySlot.R ? Math.Max(0, comp.rStackCount) : 0
            };
        }

        private static string BuildSlotBadge(CompPawnSkin comp, VisibleAbilitySlot slot, bool isCombo, bool isSecondStage)
        {
            switch (slot)
            {
                case VisibleAbilitySlot.Q:
                    return $"Q{Math.Min(4, Math.Max(1, comp.qHotkeyModeIndex + 1))}";
                case VisibleAbilitySlot.W:
                    return isCombo ? "W2" : "W";
                case VisibleAbilitySlot.E:
                    return "E";
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

            if (slot == VisibleAbilitySlot.Q)
            {
                return ResolveQModeAbilityDefName(hotkeys.qAbilityDefName, comp.qHotkeyModeIndex);
            }

            if (slot == VisibleAbilitySlot.W
                && tick <= comp.qComboWindowEndTick
                && !string.IsNullOrWhiteSpace(hotkeys.wComboAbilityDefName))
            {
                return hotkeys.wComboAbilityDefName;
            }

            return slot switch
            {
                VisibleAbilitySlot.W => hotkeys.wAbilityDefName ?? string.Empty,
                VisibleAbilitySlot.E => hotkeys.eAbilityDefName ?? string.Empty,
                _ => hotkeys.rAbilityDefName ?? string.Empty
            };
        }

        private static string ResolveQModeAbilityDefName(string baseDefName, int modeIndex)
        {
            if (string.IsNullOrWhiteSpace(baseDefName))
            {
                return string.Empty;
            }

            int normalizedModeIndex = Math.Max(0, Math.Min(3, modeIndex));
            if (TryResolveSequentialQAbilityDefName(baseDefName, normalizedModeIndex, out string resolvedDefName))
            {
                return resolvedDefName;
            }

            return baseDefName;
        }

        private static bool TryResolveSequentialQAbilityDefName(string baseDefName, int modeIndex, out string resolvedDefName)
        {
            resolvedDefName = baseDefName;
            if (string.IsNullOrWhiteSpace(baseDefName))
            {
                return false;
            }

            int markerIndex = baseDefName.LastIndexOf("_Q", StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0 || markerIndex + 3 > baseDefName.Length)
            {
                return false;
            }

            char indexChar = baseDefName[markerIndex + 2];
            if (!char.IsDigit(indexChar))
            {
                return false;
            }

            string prefix = baseDefName.Substring(0, markerIndex + 2);
            string suffix = baseDefName.Substring(markerIndex + 3);
            resolvedDefName = $"{prefix}{modeIndex + 1}{suffix}";
            return true;
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
