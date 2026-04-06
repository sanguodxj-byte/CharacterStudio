using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
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
                new XElement("CharacterDefinition",
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
                    BuildSkillsElement(definition.skills)
                ));

            document.Save(filePath);
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
                    skills = ParseSkills(root.Element("skills"))
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
    }
}
