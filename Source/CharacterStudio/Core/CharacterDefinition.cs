using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.AI;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    public sealed class CharacterSkillEntry
    {
        public string skillDefName = string.Empty;
        public int level = 0;
        public Passion passion = Passion.None;

        public CharacterSkillEntry Clone()
        {
            return new CharacterSkillEntry
            {
                skillDefName = skillDefName ?? string.Empty,
                level = level,
                passion = passion
            };
        }
    }

    public sealed class CharacterDefinition
    {
        public string defName = string.Empty;
        public string displayName = string.Empty;
        public string title = string.Empty;
        public Gender gender = Gender.None;
        public Color? favoriteColor;
        public float biologicalAge = 25f;
        public float chronologicalAge = 25f;
        public string raceDefName = ThingDefOf.Human.defName;
        public string xenotypeDefName = string.Empty;
        public string bodyTypeDefName = string.Empty;
        public string headTypeDefName = string.Empty;
        public string hairDefName = string.Empty;
        public string childhoodBackstoryDefName = string.Empty;
        public string adulthoodBackstoryDefName = string.Empty;
        public List<string> traitDefNames = new List<string>();
        public List<string> startingApparelDefNames = new List<string>();
        public List<CharacterSkillEntry> skills = new List<CharacterSkillEntry>();
        public List<CharacterRuntimeTriggerDef> runtimeTriggers = new List<CharacterRuntimeTriggerDef>();
        public CharacterStudio.Attributes.CharacterStatModifierProfile statModifiers = new CharacterStudio.Attributes.CharacterStatModifierProfile();

        public CharacterDefinition Clone()
        {
            return new CharacterDefinition
            {
                defName = defName ?? string.Empty,
                displayName = displayName ?? string.Empty,
                title = title ?? string.Empty,
                gender = gender,
                favoriteColor = favoriteColor,
                biologicalAge = biologicalAge,
                chronologicalAge = chronologicalAge,
                raceDefName = raceDefName ?? ThingDefOf.Human.defName,
                xenotypeDefName = xenotypeDefName ?? string.Empty,
                bodyTypeDefName = bodyTypeDefName ?? string.Empty,
                headTypeDefName = headTypeDefName ?? string.Empty,
                hairDefName = hairDefName ?? string.Empty,
                childhoodBackstoryDefName = childhoodBackstoryDefName ?? string.Empty,
                adulthoodBackstoryDefName = adulthoodBackstoryDefName ?? string.Empty,
                traitDefNames = new List<string>(traitDefNames ?? new List<string>()),
                startingApparelDefNames = new List<string>(startingApparelDefNames ?? new List<string>()),
                skills = (skills ?? new List<CharacterSkillEntry>()).Where(static entry => entry != null).Select(static entry => entry.Clone()).ToList(),
                runtimeTriggers = (runtimeTriggers ?? new List<CharacterRuntimeTriggerDef>())
                    .Where(static trigger => trigger != null)
                    .Select(static trigger => trigger.Clone())
                    .ToList(),
                statModifiers = statModifiers?.Clone() ?? new CharacterStudio.Attributes.CharacterStatModifierProfile()
            };
        }

        public void EnsureDefaults(string fallbackDefName, ThingDef? fallbackRaceDef, CharacterAttributeProfile? attributes)
        {
            defName = string.IsNullOrWhiteSpace(defName) ? fallbackDefName : defName.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? (attributes?.title ?? fallbackDefName) : displayName.Trim();
            title = string.IsNullOrWhiteSpace(title) ? (attributes?.title ?? string.Empty) : title.Trim();
            raceDefName = string.IsNullOrWhiteSpace(raceDefName)
                ? (fallbackRaceDef?.defName ?? ThingDefOf.Human.defName)
                : raceDefName.Trim();
            biologicalAge = Math.Max(0f, biologicalAge <= 0f ? (attributes?.biologicalAge ?? 25f) : biologicalAge);
            chronologicalAge = Math.Max(biologicalAge, chronologicalAge <= 0f ? (attributes?.chronologicalAge ?? biologicalAge) : chronologicalAge);
            xenotypeDefName = string.IsNullOrWhiteSpace(xenotypeDefName) ? string.Empty : xenotypeDefName.Trim();
            bodyTypeDefName = string.IsNullOrWhiteSpace(bodyTypeDefName) ? attributes?.bodyTypeDefName ?? string.Empty : bodyTypeDefName.Trim();
            headTypeDefName = string.IsNullOrWhiteSpace(headTypeDefName) ? attributes?.headTypeDefName ?? string.Empty : headTypeDefName.Trim();
            hairDefName = string.IsNullOrWhiteSpace(hairDefName) ? attributes?.hairDefName ?? string.Empty : hairDefName.Trim();

            traitDefNames ??= new List<string>();
            startingApparelDefNames ??= new List<string>();
            skills ??= new List<CharacterSkillEntry>();
            runtimeTriggers ??= new List<CharacterRuntimeTriggerDef>();
            statModifiers ??= new CharacterStudio.Attributes.CharacterStatModifierProfile();

            if (traitDefNames.Count == 0 && attributes?.keyTraits != null)
            {
                traitDefNames.AddRange(attributes.keyTraits.Where(static value => !string.IsNullOrWhiteSpace(value)));
            }

            if (startingApparelDefNames.Count == 0 && attributes?.startingApparelDefs != null)
            {
                startingApparelDefNames.AddRange(attributes.startingApparelDefs.Where(static value => !string.IsNullOrWhiteSpace(value)));
            }

            traitDefNames = traitDefNames
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            startingApparelDefNames = startingApparelDefNames
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            skills = skills
                .Where(static entry => entry != null && !string.IsNullOrWhiteSpace(entry.skillDefName))
                .GroupBy(static entry => entry.skillDefName, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First().Clone())
                .ToList();
            runtimeTriggers = runtimeTriggers
                .Where(static trigger => trigger != null)
                .Select(static trigger => trigger.Clone())
                .ToList();
        }
    }
}
