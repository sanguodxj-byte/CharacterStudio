using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace CharacterStudio.Core
{
    public static class CharacterRuntimeTriggerConfigUtility
    {
        public static void SyncCharacterAssets(PawnSkinDef? skinDef, CharacterDefinition? definition)
        {
            if (skinDef == null || definition == null)
            {
                return;
            }

            string ownerCharacterDefName = !string.IsNullOrWhiteSpace(definition.defName)
                ? definition.defName
                : (skinDef.defName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(ownerCharacterDefName))
            {
                return;
            }

            string profilePath = Path.Combine(CharacterSpawnProfileRegistry.GetConfigDir(), $"{CharacterSpawnProfileRegistry.SanitizeDefName(ownerCharacterDefName)}.xml");
            string triggerPath = Path.Combine(CharacterRuntimeTriggerRegistry.GetConfigDir(), $"{CharacterSpawnProfileRegistry.SanitizeDefName(ownerCharacterDefName)}.xml");

            CharacterSpawnProfileRegistry.UnregisterOwnedBy(ownerCharacterDefName);
            CharacterRuntimeTriggerRegistry.UnregisterOwnedBy(ownerCharacterDefName);
            DeleteIfExists(profilePath);
            DeleteIfExists(triggerPath);

            List<CharacterRuntimeTriggerDef> normalizedTriggers = NormalizeTriggers(definition.runtimeTriggers, ownerCharacterDefName).ToList();
            definition.runtimeTriggers = normalizedTriggers.Select(static trigger => trigger.Clone()).ToList();

            if (normalizedTriggers.Count == 0)
            {
                return;
            }

            CharacterSpawnProfileDef profile = BuildProfileDef(skinDef, definition, ownerCharacterDefName);
            CharacterRuntimeTriggerXmlUtility.SaveSpawnProfiles(new[] { profile }, profilePath);
            CharacterSpawnProfileRegistry.RegisterOrReplace(profile);

            CharacterRuntimeTriggerXmlUtility.SaveRuntimeTriggers(normalizedTriggers, triggerPath);
            foreach (CharacterRuntimeTriggerDef trigger in normalizedTriggers)
            {
                CharacterRuntimeTriggerRegistry.RegisterOrReplace(trigger);
            }
        }

        private static CharacterSpawnProfileDef BuildProfileDef(PawnSkinDef skinDef, CharacterDefinition definition, string ownerCharacterDefName)
        {
            CharacterDefinition profileDefinition = definition.Clone();
            profileDefinition.runtimeTriggers?.Clear();

            string raceDefName = !string.IsNullOrWhiteSpace(profileDefinition.raceDefName)
                ? profileDefinition.raceDefName
                : skinDef.targetRaces?.FirstOrDefault() ?? string.Empty;

            return new CharacterSpawnProfileDef
            {
                defName = CharacterSpawnProfileRegistry.GetDefaultProfileDefName(ownerCharacterDefName),
                label = string.IsNullOrWhiteSpace(profileDefinition.displayName) ? (skinDef.label ?? skinDef.defName) : profileDefinition.displayName,
                description = skinDef.description ?? string.Empty,
                ownerCharacterDefName = ownerCharacterDefName,
                skinDefName = skinDef.defName ?? string.Empty,
                characterDefinition = profileDefinition,
                raceDefName = raceDefName,
                forcePlayerFaction = true
            };
        }

        private static IEnumerable<CharacterRuntimeTriggerDef> NormalizeTriggers(IEnumerable<CharacterRuntimeTriggerDef>? triggers, string ownerCharacterDefName)
        {
            List<CharacterRuntimeTriggerDef> cloned = triggers?
                .Where(static trigger => trigger != null)
                .Select(static trigger => trigger.Clone())
                .ToList() ?? new List<CharacterRuntimeTriggerDef>();

            string profileDefName = CharacterSpawnProfileRegistry.GetDefaultProfileDefName(ownerCharacterDefName);
            for (int i = 0; i < cloned.Count; i++)
            {
                CharacterRuntimeTriggerDef trigger = cloned[i];
                trigger.ownerCharacterDefName = ownerCharacterDefName;
                if (string.IsNullOrWhiteSpace(trigger.defName))
                {
                    trigger.defName = CharacterRuntimeTriggerRegistry.GetDefaultTriggerDefName(ownerCharacterDefName, i + 1);
                }

                if (string.IsNullOrWhiteSpace(trigger.label))
                {
                    trigger.label = $"{ownerCharacterDefName} Trigger {i + 1}";
                }

                trigger.spawnProfileDefName = profileDefName;
                trigger.requiredConditions ??= new List<CharacterRuntimeTriggerCondition>();
                trigger.spawnSettings ??= new CharacterSpawnSettings();
            }

            return cloned;
        }

        private static void DeleteIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
