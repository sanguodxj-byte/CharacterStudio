using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Exporter;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    public static class CharacterDefinitionXmlUtility
    {
        public static void Save(CharacterDefinition definition, string filePath)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            XDocument document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                ToXElement("CharacterDefinition", definition));

            document.Save(filePath);
        }

        public static XElement ToXElement(string elementName, CharacterDefinition definition, bool includeRuntimeTriggers = true)
        {
            return new XElement(elementName,
                new XElement("defName", definition.defName ?? string.Empty),
                new XElement("displayName", definition.displayName ?? string.Empty),
                new XElement("gender", definition.gender.ToString()),
                new XElement("biologicalAge", definition.biologicalAge.ToString(CultureInfo.InvariantCulture)),
                new XElement("chronologicalAge", definition.chronologicalAge.ToString(CultureInfo.InvariantCulture)),
                new XElement("raceDefName", definition.raceDefName ?? ThingDefOf.Human.defName),
                string.IsNullOrWhiteSpace(definition.xenotypeDefName) ? null : new XElement("xenotypeDefName", definition.xenotypeDefName),
                string.IsNullOrWhiteSpace(definition.bodyTypeDefName) ? null : new XElement("bodyTypeDefName", definition.bodyTypeDefName),
                string.IsNullOrWhiteSpace(definition.headTypeDefName) ? null : new XElement("headTypeDefName", definition.headTypeDefName),
                string.IsNullOrWhiteSpace(definition.hairDefName) ? null : new XElement("hairDefName", definition.hairDefName),
                string.IsNullOrWhiteSpace(definition.childhoodBackstoryDefName) ? null : new XElement("childhoodBackstoryDefName", definition.childhoodBackstoryDefName),
                string.IsNullOrWhiteSpace(definition.adulthoodBackstoryDefName) ? null : new XElement("adulthoodBackstoryDefName", definition.adulthoodBackstoryDefName),
                BuildStringList("traitDefNames", definition.traitDefNames),
                BuildStringList("startingApparelDefNames", definition.startingApparelDefNames),
                BuildSkillsElement(definition.skills),
                AbilityXmlSerialization.GenerateAbilitiesElement(definition.abilityLoadout?.abilities),
                AbilityXmlSerialization.GenerateAbilityHotkeysElement(definition.abilityLoadout?.hotkeys),
                ModExportXmlWriter.GenerateEquipmentsXml(definition.equipments),
                includeRuntimeTriggers ? BuildRuntimeTriggersElement(definition.runtimeTriggers) : null
            );
        }

        public static CharacterDefinition? Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                XDocument document = XDocument.Load(filePath);
                XElement? root = document.Root;
                if (root == null || !string.Equals(root.Name.LocalName, "CharacterDefinition", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning($"[CharacterStudio] CharacterDefinition XML 根节点无效，已跳过: {filePath}");
                    return null;
                }

                CharacterDefinition definition = new CharacterDefinition
                {
                    defName = root.Element("defName")?.Value ?? string.Empty,
                    displayName = root.Element("displayName")?.Value ?? string.Empty,
                    raceDefName = root.Element("raceDefName")?.Value ?? ThingDefOf.Human.defName,
                    xenotypeDefName = root.Element("xenotypeDefName")?.Value ?? string.Empty,
                    bodyTypeDefName = root.Element("bodyTypeDefName")?.Value ?? string.Empty,
                    headTypeDefName = root.Element("headTypeDefName")?.Value ?? string.Empty,
                    hairDefName = root.Element("hairDefName")?.Value ?? string.Empty,
                    childhoodBackstoryDefName = root.Element("childhoodBackstoryDefName")?.Value ?? string.Empty,
                    adulthoodBackstoryDefName = root.Element("adulthoodBackstoryDefName")?.Value ?? string.Empty,
                    traitDefNames = ParseStringList(root.Element("traitDefNames")),
                    startingApparelDefNames = ParseStringList(root.Element("startingApparelDefNames")),
                    skills = ParseSkills(root.Element("skills")),
                    abilityLoadout = new CharacterAbilityLoadout
                    {
                        abilities = AbilityXmlSerialization.ParseAbilities(root.Element("abilities")),
                        hotkeys = AbilityXmlSerialization.ParseHotkeys(root.Element("abilityHotkeys"))
                    },
                    equipments = ParseEquipments(root.Element("equipments")),
                    runtimeTriggers = ParseRuntimeTriggers(root.Element("runtimeTriggers"))
                };

                if (Enum.TryParse(root.Element("gender")?.Value ?? string.Empty, true, out Gender parsedGender))
                {
                    definition.gender = parsedGender;
                }

                if (float.TryParse(root.Element("biologicalAge")?.Value ?? string.Empty, NumberStyles.Float, CultureInfo.InvariantCulture, out float biologicalAge))
                {
                    definition.biologicalAge = biologicalAge;
                }

                if (float.TryParse(root.Element("chronologicalAge")?.Value ?? string.Empty, NumberStyles.Float, CultureInfo.InvariantCulture, out float chronologicalAge))
                {
                    definition.chronologicalAge = chronologicalAge;
                }

                definition.EnsureDefaults(definition.defName, DefDatabase<ThingDef>.GetNamedSilentFail(definition.raceDefName) ?? ThingDefOf.Human, null);
                return definition;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] CharacterDefinition XML 加载失败，已跳过: {filePath}, {ex.Message}");
                return null;
            }
        }

        private static XElement? BuildStringList(string name, IEnumerable<string>? values)
        {
            List<string> items = values?
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            if (items.Count == 0)
            {
                return null;
            }

            return new XElement(name, items.Select(static value => new XElement("li", value)));
        }

        private static XElement? BuildSkillsElement(IEnumerable<CharacterSkillEntry>? skills)
        {
            List<CharacterSkillEntry> entries = skills?
                .Where(static entry => entry != null && !string.IsNullOrWhiteSpace(entry.skillDefName))
                .ToList() ?? new List<CharacterSkillEntry>();
            if (entries.Count == 0)
            {
                return null;
            }

            return new XElement("skills",
                entries.Select(static entry => new XElement("li",
                    new XElement("skillDefName", entry.skillDefName),
                    new XElement("level", entry.level),
                    new XElement("passion", entry.passion.ToString()))));
        }

        private static List<string> ParseStringList(XElement? element)
        {
            return element?.Elements("li")
                .Select(static item => (item.Value ?? string.Empty).Trim())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }

        private static List<CharacterSkillEntry> ParseSkills(XElement? element)
        {
            List<CharacterSkillEntry> result = new List<CharacterSkillEntry>();
            if (element == null)
            {
                return result;
            }

            foreach (XElement item in element.Elements("li"))
            {
                CharacterSkillEntry entry = new CharacterSkillEntry
                {
                    skillDefName = item.Element("skillDefName")?.Value ?? string.Empty,
                    level = int.TryParse(item.Element("level")?.Value ?? string.Empty, out int level) ? level : 0
                };

                if (Enum.TryParse(item.Element("passion")?.Value ?? string.Empty, true, out Passion passion))
                {
                    entry.passion = passion;
                }

                if (!string.IsNullOrWhiteSpace(entry.skillDefName))
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        private static XElement? BuildRuntimeTriggersElement(IEnumerable<CharacterRuntimeTriggerDef>? runtimeTriggers)
        {
            List<CharacterRuntimeTriggerDef> entries = runtimeTriggers?
                .Where(static trigger => trigger != null)
                .Select(static trigger => trigger.Clone())
                .ToList() ?? new List<CharacterRuntimeTriggerDef>();
            if (entries.Count == 0)
            {
                return null;
            }

            return new XElement("runtimeTriggers",
                entries.Select(static trigger => CharacterRuntimeTriggerXmlUtility.BuildRuntimeTriggerEntryElement(trigger)));
        }

        private static List<CharacterRuntimeTriggerDef> ParseRuntimeTriggers(XElement? element)
        {
            List<CharacterRuntimeTriggerDef> result = new List<CharacterRuntimeTriggerDef>();
            if (element == null)
            {
                return result;
            }

            foreach (XElement item in element.Elements("li"))
            {
                CharacterRuntimeTriggerDef trigger = CharacterRuntimeTriggerXmlUtility.ParseRuntimeTriggerEntryElement(item);
                if (!string.IsNullOrWhiteSpace(trigger.defName) || !string.IsNullOrWhiteSpace(trigger.label) || trigger.requiredConditions.Count > 0)
                {
                    result.Add(trigger);
                }
            }

            return result;
        }

        private static List<CharacterEquipmentDef> ParseEquipments(XElement? element)
        {
            List<CharacterEquipmentDef> result = new List<CharacterEquipmentDef>();
            if (element == null)
            {
                return result;
            }

            foreach (XElement item in element.Elements("li"))
            {
                CharacterEquipmentDef equipment = ParseEquipmentElement(item);
                equipment.EnsureDefaults();
                result.Add(equipment);
            }

            return result;
        }

        private static CharacterEquipmentDef ParseEquipmentElement(XElement item)
        {
            CharacterEquipmentDef equipment = new CharacterEquipmentDef
            {
                defName = item.Element("defName")?.Value ?? string.Empty,
                label = item.Element("label")?.Value ?? string.Empty,
                description = item.Element("description")?.Value ?? string.Empty,
                enabled = ParseBool(item.Element("enabled")?.Value, true),
                slotTag = item.Element("slotTag")?.Value ?? CharacterEquipmentDef.DefaultSlotTag,
                exportGroupKey = item.Element("exportGroupKey")?.Value ?? string.Empty,
                thingDefName = item.Element("thingDefName")?.Value ?? string.Empty,
                parentThingDefName = item.Element("parentThingDefName")?.Value ?? CharacterEquipmentDef.DefaultParentThingDefName,
                worldTexPath = item.Element("worldTexPath")?.Value ?? string.Empty,
                wornTexPath = item.Element("wornTexPath")?.Value ?? string.Empty,
                maskTexPath = item.Element("maskTexPath")?.Value ?? string.Empty,
                shaderDefName = item.Element("shaderDefName")?.Value ?? CharacterEquipmentDef.DefaultShaderDefName,
                useWornGraphicMask = ParseBool(item.Element("useWornGraphicMask")?.Value, false),
                allowCrafting = ParseBool(item.Element("allowCrafting")?.Value, false),
                recipeDefName = item.Element("recipeDefName")?.Value ?? string.Empty,
                recipeWorkbenchDefName = item.Element("recipeWorkbenchDefName")?.Value ?? "TableMachining",
                recipeWorkAmount = ParseFloat(item.Element("recipeWorkAmount")?.Value, 1200f),
                recipeProductCount = ParseInt(item.Element("recipeProductCount")?.Value, 1),
                allowTrading = ParseBool(item.Element("allowTrading")?.Value, true),
                marketValue = ParseFloat(item.Element("marketValue")?.Value, 250f),
                previewTexPath = item.Element("previewTexPath")?.Value ?? string.Empty,
                sourceNote = item.Element("sourceNote")?.Value ?? string.Empty,
                flyerThingDefName = item.Element("flyerThingDefName")?.Value ?? string.Empty,
                flyerClassName = item.Element("flyerClassName")?.Value ?? "CharacterStudio.Abilities.CharacterStudioPawnFlyer_Default",
                flyerFlightSpeed = ParseFloat(item.Element("flyerFlightSpeed")?.Value, 22f),
                tags = ParseStringList(item.Element("tags")),
                tradeTags = ParseStringList(item.Element("tradeTags")),
                abilityDefNames = ParseStringList(item.Element("abilityDefNames")),
                thingCategories = ParseStringList(item.Element("thingCategories")),
                bodyPartGroups = ParseStringList(item.Element("bodyPartGroups")),
                apparelLayers = ParseStringList(item.Element("apparelLayers")),
                apparelTags = ParseStringList(item.Element("apparelTags")),
                recipeIngredients = ParseEquipmentCostEntries(item.Element("recipeIngredients")),
                statBases = ParseEquipmentStatEntries(item.Element("statBases")),
                equippedStatOffsets = ParseEquipmentStatEntries(item.Element("equippedStatOffsets")),
                renderData = ParseEquipmentRenderData(item.Element("renderData"))
            };

            return equipment;
        }

        private static List<CharacterEquipmentStatEntry> ParseEquipmentStatEntries(XElement? element)
        {
            List<CharacterEquipmentStatEntry> result = new List<CharacterEquipmentStatEntry>();
            if (element == null)
            {
                return result;
            }

            foreach (XElement item in element.Elements("li"))
            {
                string statDefName = item.Element("statDefName")?.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(statDefName))
                {
                    continue;
                }

                result.Add(new CharacterEquipmentStatEntry
                {
                    statDefName = statDefName,
                    value = ParseFloat(item.Element("value")?.Value, 0f)
                });
            }

            return result;
        }

        private static List<CharacterEquipmentCostEntry> ParseEquipmentCostEntries(XElement? element)
        {
            List<CharacterEquipmentCostEntry> result = new List<CharacterEquipmentCostEntry>();
            if (element == null)
            {
                return result;
            }

            foreach (XElement item in element.Elements("li"))
            {
                string thingDefName = item.Element("thingDefName")?.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(thingDefName))
                {
                    continue;
                }

                result.Add(new CharacterEquipmentCostEntry
                {
                    thingDefName = thingDefName,
                    count = ParseInt(item.Element("count")?.Value, 1)
                });
            }

            return result;
        }

        private static CharacterEquipmentRenderData ParseEquipmentRenderData(XElement? element)
        {
            CharacterEquipmentRenderData data = CharacterEquipmentRenderData.CreateDefault();
            if (element == null)
            {
                return data;
            }

            data.layerName = element.Element("layerName")?.Value ?? data.layerName;
            data.texPath = element.Element("texPath")?.Value ?? data.texPath;
            data.anchorTag = element.Element("anchorTag")?.Value ?? data.anchorTag;
            data.anchorPath = element.Element("anchorPath")?.Value ?? data.anchorPath;
            data.maskTexPath = element.Element("maskTexPath")?.Value ?? data.maskTexPath;
            data.shaderDefName = element.Element("shaderDefName")?.Value ?? data.shaderDefName;
            data.directionalFacing = element.Element("directionalFacing")?.Value ?? data.directionalFacing;
            data.offset = ParseVector3(element.Element("offset")?.Value, data.offset);
            data.offsetEast = ParseVector3(element.Element("offsetEast")?.Value, data.offsetEast);
            data.offsetNorth = ParseVector3(element.Element("offsetNorth")?.Value, data.offsetNorth);
            data.useWestOffset = ParseBool(element.Element("useWestOffset")?.Value, data.useWestOffset);
            data.offsetWest = ParseVector3(element.Element("offsetWest")?.Value, data.offsetWest);
            data.scale = ParseVector2(element.Element("scale")?.Value, data.scale);
            data.scaleEastMultiplier = ParseVector2(element.Element("scaleEastMultiplier")?.Value, data.scaleEastMultiplier);
            data.scaleNorthMultiplier = ParseVector2(element.Element("scaleNorthMultiplier")?.Value, data.scaleNorthMultiplier);
            data.scaleWestMultiplier = ParseVector2(element.Element("scaleWestMultiplier")?.Value, data.scaleWestMultiplier);
            data.rotation = ParseFloat(element.Element("rotation")?.Value, data.rotation);
            data.rotationEastOffset = ParseFloat(element.Element("rotationEastOffset")?.Value, data.rotationEastOffset);
            data.rotationNorthOffset = ParseFloat(element.Element("rotationNorthOffset")?.Value, data.rotationNorthOffset);
            data.rotationWestOffset = ParseFloat(element.Element("rotationWestOffset")?.Value, data.rotationWestOffset);
            data.drawOrder = ParseFloat(element.Element("drawOrder")?.Value, data.drawOrder);
            data.flipHorizontal = ParseBool(element.Element("flipHorizontal")?.Value, data.flipHorizontal);
            data.visible = ParseBool(element.Element("visible")?.Value, data.visible);
            data.colorSource = ParseEnum(element.Element("colorSource")?.Value, data.colorSource);
            data.customColor = ParseColor(element.Element("customColor")?.Value, data.customColor);
            data.colorTwoSource = ParseEnum(element.Element("colorTwoSource")?.Value, data.colorTwoSource);
            data.customColorTwo = ParseColor(element.Element("customColorTwo")?.Value, data.customColorTwo);
            data.useTriggeredLocalAnimation = ParseBool(element.Element("useTriggeredLocalAnimation")?.Value, data.useTriggeredLocalAnimation);
            data.triggerAbilityDefName = element.Element("triggerAbilityDefName")?.Value ?? data.triggerAbilityDefName;
            data.animationGroupKey = element.Element("animationGroupKey")?.Value ?? data.animationGroupKey;
            data.triggeredAnimationRole = ParseEnum(element.Element("triggeredAnimationRole")?.Value, data.triggeredAnimationRole);
            data.triggeredDeployAngle = ParseFloat(element.Element("triggeredDeployAngle")?.Value, data.triggeredDeployAngle);
            data.triggeredReturnAngle = ParseFloat(element.Element("triggeredReturnAngle")?.Value, data.triggeredReturnAngle);
            data.triggeredDeployTicks = ParseInt(element.Element("triggeredDeployTicks")?.Value, data.triggeredDeployTicks);
            data.triggeredHoldTicks = ParseInt(element.Element("triggeredHoldTicks")?.Value, data.triggeredHoldTicks);
            data.triggeredReturnTicks = ParseInt(element.Element("triggeredReturnTicks")?.Value, data.triggeredReturnTicks);
            data.triggeredPivotOffset = ParseVector2(element.Element("triggeredPivotOffset")?.Value, data.triggeredPivotOffset);
            data.triggeredUseVfxVisibility = ParseBool(element.Element("triggeredUseVfxVisibility")?.Value, data.triggeredUseVfxVisibility);
            data.triggeredIdleTexPath = element.Element("triggeredIdleTexPath")?.Value ?? data.triggeredIdleTexPath;
            data.triggeredDeployTexPath = element.Element("triggeredDeployTexPath")?.Value ?? data.triggeredDeployTexPath;
            data.triggeredHoldTexPath = element.Element("triggeredHoldTexPath")?.Value ?? data.triggeredHoldTexPath;
            data.triggeredReturnTexPath = element.Element("triggeredReturnTexPath")?.Value ?? data.triggeredReturnTexPath;
            data.triggeredIdleMaskTexPath = element.Element("triggeredIdleMaskTexPath")?.Value ?? data.triggeredIdleMaskTexPath;
            data.triggeredDeployMaskTexPath = element.Element("triggeredDeployMaskTexPath")?.Value ?? data.triggeredDeployMaskTexPath;
            data.triggeredHoldMaskTexPath = element.Element("triggeredHoldMaskTexPath")?.Value ?? data.triggeredHoldMaskTexPath;
            data.triggeredReturnMaskTexPath = element.Element("triggeredReturnMaskTexPath")?.Value ?? data.triggeredReturnMaskTexPath;
            data.triggeredVisibleDuringDeploy = ParseBool(element.Element("triggeredVisibleDuringDeploy")?.Value, data.triggeredVisibleDuringDeploy);
            data.triggeredVisibleDuringHold = ParseBool(element.Element("triggeredVisibleDuringHold")?.Value, data.triggeredVisibleDuringHold);
            data.triggeredVisibleDuringReturn = ParseBool(element.Element("triggeredVisibleDuringReturn")?.Value, data.triggeredVisibleDuringReturn);
            data.triggeredVisibleOutsideCycle = ParseBool(element.Element("triggeredVisibleOutsideCycle")?.Value, data.triggeredVisibleOutsideCycle);
            return data;
        }

        private static bool ParseBool(string? value, bool fallback)
            => bool.TryParse(value, out bool parsed) ? parsed : fallback;

        private static int ParseInt(string? value, int fallback)
            => int.TryParse(value, out int parsed) ? parsed : fallback;

        private static float ParseFloat(string? value, float fallback)
            => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;

        private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
            => Enum.TryParse(value ?? string.Empty, true, out TEnum parsed) ? parsed : fallback;

        private static Vector2 ParseVector2(string? value, Vector2 fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string trimmed = (value ?? string.Empty).Trim().Trim('(', ')');
            string[] parts = trimmed.Split(',');
            if (parts.Length < 2)
            {
                return fallback;
            }

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
            {
                return fallback;
            }

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                return fallback;
            }

            return new Vector2(x, y);
        }

        private static Vector3 ParseVector3(string? value, Vector3 fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string trimmed = (value ?? string.Empty).Trim().Trim('(', ')');
            string[] parts = trimmed.Split(',');
            if (parts.Length < 3)
            {
                return fallback;
            }

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
            {
                return fallback;
            }

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                return fallback;
            }

            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return fallback;
            }

            return new Vector3(x, y, z);
        }

        private static Color ParseColor(string? value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string trimmed = (value ?? string.Empty).Trim().Trim('(', ')');
            string[] parts = trimmed.Split(',');
            if (parts.Length < 3)
            {
                return fallback;
            }

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r))
            {
                return fallback;
            }

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g))
            {
                return fallback;
            }

            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            {
                return fallback;
            }

            float a = 1f;
            if (parts.Length >= 4)
            {
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out a);
            }

            return new Color(r, g, b, a <= 0f ? 1f : a);
        }
    }
}
