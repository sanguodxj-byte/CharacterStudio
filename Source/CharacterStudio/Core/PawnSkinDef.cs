using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.AI;
using CharacterStudio.Attributes;
using UnityEngine;
using Verse;
using RimWorld;

namespace CharacterStudio.Core
{
    public class SkinAbilityHotkeyConfig : IExposable
    {
        public bool enabled = false;
        public Dictionary<string, string> slotBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] DefaultSlotKeys = AbilityHotkeySlotUtility.SupportedSlotKeys;

        public string this[string slotKey]
        {
            get => slotBindings.TryGetValue(slotKey ?? string.Empty, out string val) ? val : string.Empty;
            set => slotBindings[slotKey ?? string.Empty] = value ?? string.Empty;
        }

        public SkinAbilityHotkeyConfig Clone()
        {
            return new SkinAbilityHotkeyConfig
            {
                enabled = enabled,
                slotBindings = new Dictionary<string, string>(slotBindings, StringComparer.OrdinalIgnoreCase)
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Collections.Look(ref slotBindings, "slotBindings", LookMode.Value, LookMode.Value);
            NormalizeToSupportedSlots();
        }

        public void NormalizeToSupportedSlots()
        {
            slotBindings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            List<string> keys = slotBindings.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!AbilityHotkeySlotUtility.IsSupportedSlotKey(keys[i]))
                {
                    slotBindings.Remove(keys[i]);
                }
            }

            foreach (string key in DefaultSlotKeys)
            {
                if (!slotBindings.ContainsKey(key))
                {
                    slotBindings[key] = string.Empty;
                }
            }

            if (!HasAnyBinding())
            {
                enabled = false;
            }
        }

        public bool HasAnyBinding() => slotBindings.Values.Any(v => !string.IsNullOrWhiteSpace(v));

        public IEnumerable<string> EnumerateBoundAbilityDefNames()
        {
            foreach (var val in slotBindings.Values)
                if (!string.IsNullOrWhiteSpace(val)) yield return val;
        }
    }

    public class CharacterAbilityLoadout : IExposable
    {
        public List<ModularAbilityDef> abilities = new List<ModularAbilityDef>();
        public SkinAbilityHotkeyConfig hotkeys = new SkinAbilityHotkeyConfig();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref abilities, "abilities", LookMode.Deep);
            Scribe_Deep.Look(ref hotkeys, "hotkeys");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                abilities ??= new List<ModularAbilityDef>();
                hotkeys ??= new SkinAbilityHotkeyConfig();
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                abilities ??= new List<ModularAbilityDef>();
                hotkeys ??= new SkinAbilityHotkeyConfig();
                hotkeys.NormalizeToSupportedSlots();
                AbilityGrantUtility.WarmupRuntimeAbilityDefs(abilities);
            }
        }

        public CharacterAbilityLoadout Clone()
        {
            return new CharacterAbilityLoadout
            {
                abilities = abilities.Select(a => a?.Clone()).Where(a => a != null).ToList()!,
                hotkeys = hotkeys?.Clone() ?? new SkinAbilityHotkeyConfig()
            };
        }
    }

    public class ExpressionConfig : IExposable
    {
        public ExpressionType type;
        public List<PawnLayerConfig> layers = new List<PawnLayerConfig>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref type, "type");
            Scribe_Collections.Look(ref layers, "layers", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                layers ??= new List<PawnLayerConfig>();
        }

        public ExpressionConfig Clone()
        {
            return new ExpressionConfig
            {
                type = type,
                layers = layers.Select(l => l.Clone()).ToList()
            };
        }
    }

    public class EyeDirectionConfig : IExposable
    {
        public bool enabled = false;
        public float horizontalRange = 0.2f;
        public float verticalRange = 0.15f;
        public float transitionSpeed = 0.1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Values.Look(ref horizontalRange, "horizontalRange", 0.2f);
            Scribe_Values.Look(ref verticalRange, "verticalRange", 0.15f);
            Scribe_Values.Look(ref transitionSpeed, "transitionSpeed", 0.1f);
        }

        public EyeDirectionConfig Clone() => (EyeDirectionConfig)MemberwiseClone();
    }

    public class PawnSkinDef : Def, IExposable
    {
        public string author = "Unknown";
        public string version = "1.0";
        public bool humanlikeOnly = true;

        public bool hideVanillaHead = false;
        public bool hideVanillaHair = false;
        public bool hideVanillaBody = false;
        public bool hideVanillaApparel = false;

        public List<PawnLayerConfig> layers = new List<PawnLayerConfig>();
        public List<ExpressionConfig> expressions = new List<ExpressionConfig>();
        public EyeDirectionConfig? eyeDirection;
        public CharacterAttributeProfile attributes = new CharacterAttributeProfile();
        public BaseAppearanceConfig baseAppearance = new BaseAppearanceConfig();
        public PawnAnimationConfig animationConfig = new PawnAnimationConfig();
        public PawnFaceConfig faceConfig = new PawnFaceConfig();

        public List<string> targetRaces = new List<string>();
        public string? raceDisplayName;
        public string? xenotypeDefName;
        public string? previewTexPath;
        public int defaultRacePriority = 0;
        public bool applyAsDefaultForTargetRaces = false;

        public List<string> hiddenPaths = new List<string>();
        public List<string> hiddenTags = new List<string>();
        public float previewHeadOffsetZ = 0f;
        public bool editLayerOffsetPerFacing = false;
        public float globalTextureScale = 1f;

        public CharacterStatModifierProfile statModifiers = new CharacterStatModifierProfile();
        public List<ModularAbilityDef> abilities = new List<ModularAbilityDef>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref author, "author", "Unknown");
            Scribe_Values.Look(ref version, "version", "1.0");
            Scribe_Values.Look(ref humanlikeOnly, "humanlikeOnly", true);

            Scribe_Values.Look(ref hideVanillaHead, "hideVanillaHead", false);
            Scribe_Values.Look(ref hideVanillaHair, "hideVanillaHair", false);
            Scribe_Values.Look(ref hideVanillaBody, "hideVanillaBody", false);
            Scribe_Values.Look(ref hideVanillaApparel, "hideVanillaApparel", false);

            Scribe_Collections.Look(ref layers, "layers", LookMode.Deep);
            Scribe_Collections.Look(ref expressions, "expressions", LookMode.Deep);
            Scribe_Deep.Look(ref eyeDirection, "eyeDirection");
            Scribe_Deep.Look(ref attributes, "attributes");
            Scribe_Deep.Look(ref baseAppearance, "baseAppearance");
            Scribe_Deep.Look(ref animationConfig, "animationConfig");
            Scribe_Deep.Look(ref faceConfig, "faceConfig");

            Scribe_Collections.Look(ref targetRaces, "targetRaces", LookMode.Value);
            Scribe_Values.Look(ref raceDisplayName, "raceDisplayName");
            Scribe_Values.Look(ref xenotypeDefName, "xenotypeDefName");
            Scribe_Values.Look(ref previewTexPath, "previewTexPath");
            Scribe_Values.Look(ref defaultRacePriority, "defaultRacePriority", 0);
            Scribe_Values.Look(ref applyAsDefaultForTargetRaces, "applyAsDefaultForTargetRaces", false);

            Scribe_Collections.Look(ref hiddenPaths, "hiddenPaths", LookMode.Value);
            Scribe_Collections.Look(ref hiddenTags, "hiddenTags", LookMode.Value);
            Scribe_Values.Look(ref previewHeadOffsetZ, "previewHeadOffsetZ", 0f);
            Scribe_Values.Look(ref editLayerOffsetPerFacing, "editLayerOffsetPerFacing", false);
            Scribe_Values.Look(ref globalTextureScale, "globalTextureScale", 1f);
            Scribe_Deep.Look(ref statModifiers, "statModifiers");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                layers ??= new List<PawnLayerConfig>();
                expressions ??= new List<ExpressionConfig>();
                baseAppearance ??= new BaseAppearanceConfig();
                animationConfig ??= new PawnAnimationConfig();
                faceConfig ??= new PawnFaceConfig();
                targetRaces ??= new List<string>();
                hiddenPaths ??= new List<string>();
                hiddenTags ??= new List<string>();
                attributes ??= new CharacterAttributeProfile();
                statModifiers ??= new CharacterStatModifierProfile();
            }
        }

        public PawnSkinDef Clone()
        {
            var clone = (PawnSkinDef)MemberwiseClone();
            clone.layers = layers.Select(l => l.Clone()).ToList();
            clone.expressions = expressions.Select(e => e.Clone()).ToList();
            clone.eyeDirection = eyeDirection?.Clone();
            clone.attributes = attributes?.Clone() ?? new CharacterAttributeProfile();
            clone.baseAppearance = baseAppearance?.Clone() ?? new BaseAppearanceConfig();
            clone.animationConfig = animationConfig?.Clone() ?? new PawnAnimationConfig();
            clone.faceConfig = faceConfig?.Clone() ?? new PawnFaceConfig();
            clone.targetRaces = targetRaces?.ToList() ?? new List<string>();
            clone.hiddenPaths = hiddenPaths?.ToList() ?? new List<string>();
            clone.hiddenTags = hiddenTags?.ToList() ?? new List<string>();
            clone.statModifiers = statModifiers?.Clone() ?? new CharacterStatModifierProfile();
            clone.abilities = abilities?.Select(a => a?.Clone()).Where(a => a != null).Cast<ModularAbilityDef>().ToList() ?? new List<ModularAbilityDef>();
            return clone;
        }

        public void RemoveApparelHidingData()
        {
            hiddenPaths?.Clear();
            hiddenTags?.Clear();
        }

        public bool IsValidForPawn(Pawn pawn)
        {
            if (pawn == null) return false;
            if (humanlikeOnly && !pawn.RaceProps.Humanlike) return false;
            if (targetRaces != null && targetRaces.Count > 0)
            {
                return targetRaces.Contains(pawn.def.defName);
            }
            return true;
        }
    }
}
