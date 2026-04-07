using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Items;
using RimWorld;

namespace CharacterStudio.Core
{
    public static class CharacterRuntimeTriggerXmlUtility
    {
        public static void SaveSpawnProfiles(IEnumerable<CharacterSpawnProfileDef> profiles, string filePath)
        {
            SaveDefsDocument(profiles.Select(BuildSpawnProfileElement), filePath);
        }

        public static void SaveRuntimeTriggers(IEnumerable<CharacterRuntimeTriggerDef> triggers, string filePath)
        {
            SaveDefsDocument(triggers.Select(BuildRuntimeTriggerDefElement), filePath);
        }

        public static XElement BuildRuntimeTriggerEntryElement(CharacterRuntimeTriggerDef trigger, string elementName = "li")
        {
            return new XElement(elementName,
                new XElement("defName", trigger.defName ?? string.Empty),
                string.IsNullOrWhiteSpace(trigger.label) ? null : new XElement("label", trigger.label),
                new XElement("enabled", trigger.enabled.ToString().ToLowerInvariant()),
                trigger.sortOrder != 0 ? new XElement("sortOrder", trigger.sortOrder) : null,
                string.IsNullOrWhiteSpace(trigger.ownerCharacterDefName) ? null : new XElement("ownerCharacterDefName", trigger.ownerCharacterDefName),
                string.IsNullOrWhiteSpace(trigger.spawnProfileDefName) ? null : new XElement("spawnProfileDefName", trigger.spawnProfileDefName),
                !trigger.spawnNearColonist ? new XElement("spawnNearColonist", trigger.spawnNearColonist.ToString().ToLowerInvariant()) : null,
                !trigger.requirePlayerHomeMap ? new XElement("requirePlayerHomeMap", trigger.requirePlayerHomeMap.ToString().ToLowerInvariant()) : null,
                trigger.evaluationIntervalTicks != 250 ? new XElement("evaluationIntervalTicks", trigger.evaluationIntervalTicks) : null,
                trigger.cooldownTicks != 60000 ? new XElement("cooldownTicks", trigger.cooldownTicks) : null,
                trigger.fireOncePerGame ? new XElement("fireOncePerGame", trigger.fireOncePerGame.ToString().ToLowerInvariant()) : null,
                trigger.fireOncePerMap ? new XElement("fireOncePerMap", trigger.fireOncePerMap.ToString().ToLowerInvariant()) : null,
                trigger.conditionLogic != CharacterSpawnConditionLogic.All ? new XElement("conditionLogic", trigger.conditionLogic.ToString()) : null,
                BuildRuntimeConditionsElement(trigger.requiredConditions),
                BuildSpawnSettingsElement(trigger.spawnSettings)
            );
        }

        public static CharacterRuntimeTriggerDef ParseRuntimeTriggerEntryElement(XElement element)
        {
            CharacterRuntimeTriggerDef trigger = new CharacterRuntimeTriggerDef
            {
                defName = element.Element("defName")?.Value ?? string.Empty,
                label = element.Element("label")?.Value ?? string.Empty,
                ownerCharacterDefName = element.Element("ownerCharacterDefName")?.Value ?? string.Empty,
                spawnProfileDefName = element.Element("spawnProfileDefName")?.Value ?? string.Empty,
                requiredConditions = ParseRuntimeConditions(element.Element("requiredConditions")),
                spawnSettings = ParseSpawnSettings(element.Element("spawnSettings"))
            };

            if (bool.TryParse(element.Element("enabled")?.Value ?? string.Empty, out bool enabled))
            {
                trigger.enabled = enabled;
            }

            if (int.TryParse(element.Element("sortOrder")?.Value ?? string.Empty, out int sortOrder))
            {
                trigger.sortOrder = sortOrder;
            }

            if (bool.TryParse(element.Element("spawnNearColonist")?.Value ?? string.Empty, out bool spawnNearColonist))
            {
                trigger.spawnNearColonist = spawnNearColonist;
            }

            if (bool.TryParse(element.Element("requirePlayerHomeMap")?.Value ?? string.Empty, out bool requirePlayerHomeMap))
            {
                trigger.requirePlayerHomeMap = requirePlayerHomeMap;
            }

            if (int.TryParse(element.Element("evaluationIntervalTicks")?.Value ?? string.Empty, out int evaluationIntervalTicks))
            {
                trigger.evaluationIntervalTicks = evaluationIntervalTicks;
            }

            if (int.TryParse(element.Element("cooldownTicks")?.Value ?? string.Empty, out int cooldownTicks))
            {
                trigger.cooldownTicks = cooldownTicks;
            }

            if (bool.TryParse(element.Element("fireOncePerGame")?.Value ?? string.Empty, out bool fireOncePerGame))
            {
                trigger.fireOncePerGame = fireOncePerGame;
            }

            if (bool.TryParse(element.Element("fireOncePerMap")?.Value ?? string.Empty, out bool fireOncePerMap))
            {
                trigger.fireOncePerMap = fireOncePerMap;
            }

            if (Enum.TryParse(element.Element("conditionLogic")?.Value ?? string.Empty, true, out CharacterSpawnConditionLogic conditionLogic))
            {
                trigger.conditionLogic = conditionLogic;
            }

            return trigger;
        }

        public static XElement BuildSpawnProfileElement(CharacterSpawnProfileDef profile)
        {
            CharacterDefinition profileDefinition = profile.characterDefinition?.Clone() ?? new CharacterDefinition();
            profileDefinition.runtimeTriggers?.Clear();

            return new XElement(typeof(CharacterSpawnProfileDef).FullName ?? nameof(CharacterSpawnProfileDef),
                new XElement("defName", profile.defName ?? string.Empty),
                string.IsNullOrWhiteSpace(profile.label) ? null : new XElement("label", profile.label),
                string.IsNullOrWhiteSpace(profile.description) ? null : new XElement("description", profile.description),
                string.IsNullOrWhiteSpace(profile.ownerCharacterDefName) ? null : new XElement("ownerCharacterDefName", profile.ownerCharacterDefName),
                string.IsNullOrWhiteSpace(profile.skinDefName) ? null : new XElement("skinDefName", profile.skinDefName),
                string.IsNullOrWhiteSpace(profile.raceDefName) ? null : new XElement("raceDefName", profile.raceDefName),
                string.IsNullOrWhiteSpace(profile.pawnKindDefName) ? null : new XElement("pawnKindDefName", profile.pawnKindDefName),
                string.IsNullOrWhiteSpace(profile.factionDefName) ? null : new XElement("factionDefName", profile.factionDefName),
                !profile.forcePlayerFaction ? new XElement("forcePlayerFaction", profile.forcePlayerFaction.ToString().ToLowerInvariant()) : null,
                CharacterDefinitionXmlUtility.ToXElement("characterDefinition", profileDefinition, includeRuntimeTriggers: false)
            );
        }

        public static XElement BuildRuntimeTriggerDefElement(CharacterRuntimeTriggerDef trigger)
        {
            XElement element = BuildRuntimeTriggerEntryElement(trigger, typeof(CharacterRuntimeTriggerDef).FullName ?? nameof(CharacterRuntimeTriggerDef));
            return element;
        }

        public static XElement? BuildRuntimeConditionsElement(IEnumerable<CharacterRuntimeTriggerCondition>? conditions)
        {
            List<CharacterRuntimeTriggerCondition> list = conditions?
                .Where(static condition => condition != null)
                .Select(static condition => condition.Clone())
                .ToList() ?? new List<CharacterRuntimeTriggerCondition>();
            if (list.Count == 0)
            {
                return null;
            }

            return new XElement("requiredConditions", list.Select(BuildRuntimeConditionElement));
        }

        public static List<CharacterRuntimeTriggerCondition> ParseRuntimeConditions(XElement? element)
        {
            List<CharacterRuntimeTriggerCondition> result = new List<CharacterRuntimeTriggerCondition>();
            if (element == null)
            {
                return result;
            }

            foreach (XElement item in element.Elements("li"))
            {
                CharacterRuntimeTriggerCondition condition = new CharacterRuntimeTriggerCondition
                {
                    thingDefName = item.Element("thingDefName")?.Value ?? string.Empty,
                    categoryDefName = item.Element("categoryDefName")?.Value ?? string.Empty,
                    tradeTag = item.Element("tradeTag")?.Value ?? string.Empty
                };

                if (Enum.TryParse(item.Element("conditionType")?.Value ?? string.Empty, true, out CharacterRuntimeTriggerConditionType conditionType))
                {
                    condition.conditionType = conditionType;
                }

                if (Enum.TryParse(item.Element("foodType")?.Value ?? string.Empty, true, out FoodTypeFlags foodType))
                {
                    condition.foodType = foodType;
                }

                if (Enum.TryParse(item.Element("foodPreferability")?.Value ?? string.Empty, true, out FoodPreferability foodPreferability))
                {
                    condition.foodPreferability = foodPreferability;
                }

                if (int.TryParse(item.Element("minCount")?.Value ?? string.Empty, out int minCount))
                {
                    condition.minCount = minCount;
                }

                if (bool.TryParse(item.Element("countStackCount")?.Value ?? string.Empty, out bool countStackCount))
                {
                    condition.countStackCount = countStackCount;
                }

                if (int.TryParse(item.Element("minColonistCount")?.Value ?? string.Empty, out int minColonistCount))
                {
                    condition.minColonistCount = minColonistCount;
                }

                if (float.TryParse(item.Element("minDaysPassed")?.Value ?? string.Empty, NumberStyles.Float, CultureInfo.InvariantCulture, out float minDaysPassed))
                {
                    condition.minDaysPassed = minDaysPassed;
                }

                result.Add(condition);
            }

            return result;
        }

        public static XElement? BuildSpawnSettingsElement(CharacterSpawnSettings? settings)
        {
            if (settings == null)
            {
                return null;
            }

            return new XElement("spawnSettings",
                string.IsNullOrWhiteSpace(settings.arrivalDefName) ? null : new XElement("arrivalDefName", settings.arrivalDefName),
                new XElement("arrivalMode", settings.arrivalMode.ToString()),
                string.IsNullOrWhiteSpace(settings.spawnEventDefName) ? null : new XElement("spawnEventDefName", settings.spawnEventDefName),
                new XElement("spawnEvent", settings.spawnEvent.ToString()),
                string.IsNullOrWhiteSpace(settings.eventMessageText) ? null : new XElement("eventMessageText", settings.eventMessageText),
                string.IsNullOrWhiteSpace(settings.eventLetterTitle) ? null : new XElement("eventLetterTitle", settings.eventLetterTitle),
                string.IsNullOrWhiteSpace(settings.spawnAnimationDefName) ? null : new XElement("spawnAnimationDefName", settings.spawnAnimationDefName),
                new XElement("spawnAnimation", settings.spawnAnimation.ToString()),
                settings.spawnAnimationScale != 1f ? new XElement("spawnAnimationScale", settings.spawnAnimationScale.ToString(CultureInfo.InvariantCulture)) : null
            );
        }

        public static CharacterSpawnSettings ParseSpawnSettings(XElement? element)
        {
            CharacterSpawnSettings settings = new CharacterSpawnSettings
            {
                arrivalDefName = element?.Element("arrivalDefName")?.Value ?? string.Empty,
                spawnEventDefName = element?.Element("spawnEventDefName")?.Value ?? string.Empty,
                eventMessageText = element?.Element("eventMessageText")?.Value ?? string.Empty,
                eventLetterTitle = element?.Element("eventLetterTitle")?.Value ?? string.Empty,
                spawnAnimationDefName = element?.Element("spawnAnimationDefName")?.Value ?? string.Empty
            };

            if (Enum.TryParse(element?.Element("arrivalMode")?.Value ?? string.Empty, true, out SummonArrivalMode arrivalMode))
            {
                settings.arrivalMode = arrivalMode;
            }

            if (Enum.TryParse(element?.Element("spawnEvent")?.Value ?? string.Empty, true, out SummonSpawnEventMode spawnEvent))
            {
                settings.spawnEvent = spawnEvent;
            }

            if (Enum.TryParse(element?.Element("spawnAnimation")?.Value ?? string.Empty, true, out SummonSpawnAnimationMode spawnAnimation))
            {
                settings.spawnAnimation = spawnAnimation;
            }

            if (float.TryParse(element?.Element("spawnAnimationScale")?.Value ?? string.Empty, NumberStyles.Float, CultureInfo.InvariantCulture, out float spawnAnimationScale))
            {
                settings.spawnAnimationScale = spawnAnimationScale;
            }

            return settings;
        }

        private static XElement BuildRuntimeConditionElement(CharacterRuntimeTriggerCondition condition)
        {
            return new XElement("li",
                new XElement("conditionType", condition.conditionType.ToString()),
                string.IsNullOrWhiteSpace(condition.thingDefName) ? null : new XElement("thingDefName", condition.thingDefName),
                string.IsNullOrWhiteSpace(condition.categoryDefName) ? null : new XElement("categoryDefName", condition.categoryDefName),
                string.IsNullOrWhiteSpace(condition.tradeTag) ? null : new XElement("tradeTag", condition.tradeTag),
                condition.foodType != FoodTypeFlags.None ? new XElement("foodType", condition.foodType.ToString()) : null,
                condition.foodPreferability != FoodPreferability.Undefined ? new XElement("foodPreferability", condition.foodPreferability.ToString()) : null,
                condition.minCount != 1 ? new XElement("minCount", condition.minCount) : null,
                !condition.countStackCount ? new XElement("countStackCount", condition.countStackCount.ToString().ToLowerInvariant()) : null,
                condition.minColonistCount != 1 ? new XElement("minColonistCount", condition.minColonistCount) : null,
                Math.Abs(condition.minDaysPassed) > 0.0001f ? new XElement("minDaysPassed", condition.minDaysPassed.ToString(CultureInfo.InvariantCulture)) : null
            );
        }

        private static void SaveDefsDocument(IEnumerable<XElement> elements, string filePath)
        {
            List<XElement> nodes = elements?.Where(static element => element != null).ToList() ?? new List<XElement>();
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            XDocument document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs", nodes));
            document.Save(filePath);
        }
    }
}
