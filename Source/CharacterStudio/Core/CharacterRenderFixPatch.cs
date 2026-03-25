using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Verse;

namespace CharacterStudio.Core
{
    public class CharacterRenderFixPatch : IExposable
    {
        public string defName = string.Empty;
        public string label = string.Empty;
        public bool enabled = true;
        public List<string> targetRaceDefs = new List<string>();
        public List<string> hideNodePaths = new List<string>();
        public List<RenderNodeOrderOverride> orderOverrides = new List<RenderNodeOrderOverride>();

        public bool MatchesPawn(Pawn? pawn)
        {
            if (!enabled || pawn == null)
            {
                return false;
            }

            if (targetRaceDefs == null || targetRaceDefs.Count == 0)
            {
                return false;
            }

            if (!targetRaceDefs.Any(raceDefName => string.Equals(raceDefName, pawn.def.defName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return defName.EndsWith("_" + pawn.thingIDNumber, StringComparison.OrdinalIgnoreCase)
                ? string.Equals(label?.TrimEnd(), $"{label?.Replace($" [{pawn.LabelShortCap}]", string.Empty).TrimEnd()} [{pawn.LabelShortCap}]", StringComparison.Ordinal)
                : true;
        }

        public CharacterRenderFixPatch Clone()
        {
            return new CharacterRenderFixPatch
            {
                defName = defName,
                label = label,
                enabled = enabled,
                targetRaceDefs = new List<string>(targetRaceDefs ?? new List<string>()),
                hideNodePaths = new List<string>(hideNodePaths ?? new List<string>()),
                orderOverrides = (orderOverrides ?? new List<RenderNodeOrderOverride>())
                    .Where(overrideEntry => overrideEntry != null)
                    .Select(overrideEntry => overrideEntry.Clone())
                    .ToList()
            };
        }

        public void Normalize()
        {
            defName = defName?.Trim() ?? string.Empty;
            label = label?.Trim() ?? string.Empty;
            targetRaceDefs ??= new List<string>();
            hideNodePaths ??= new List<string>();
            orderOverrides ??= new List<RenderNodeOrderOverride>();

            targetRaceDefs = targetRaceDefs
                .Where(raceDefName => !string.IsNullOrWhiteSpace(raceDefName))
                .Select(raceDefName => raceDefName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            hideNodePaths = hideNodePaths
                .Where(nodePath => !string.IsNullOrWhiteSpace(nodePath))
                .Select(nodePath => nodePath.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            orderOverrides = orderOverrides
                .Where(overrideEntry => overrideEntry != null)
                .Select(overrideEntry =>
                {
                    overrideEntry.Normalize();
                    return overrideEntry;
                })
                .Where(overrideEntry => !string.IsNullOrWhiteSpace(overrideEntry.targetNodePath)
                    && Math.Abs(overrideEntry.drawOrderOffset) > 0.0001f)
                .GroupBy(overrideEntry => overrideEntry.targetNodePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => new RenderNodeOrderOverride
                {
                    targetNodePath = group.Key,
                    drawOrderOffset = group.Sum(entry => entry.drawOrderOffset)
                })
                .ToList();

            if (string.IsNullOrWhiteSpace(label))
            {
                label = string.IsNullOrWhiteSpace(defName) ? "Render Fix Patch" : defName;
            }

            if (string.IsNullOrWhiteSpace(defName))
            {
                defName = "CS_RenderFix_" + Guid.NewGuid().ToString("N");
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName", string.Empty);
            Scribe_Values.Look(ref label, "label", string.Empty);
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Collections.Look(ref targetRaceDefs, "targetRaceDefs", LookMode.Value);
            Scribe_Collections.Look(ref hideNodePaths, "hideNodePaths", LookMode.Value);
            Scribe_Collections.Look(ref orderOverrides, "orderOverrides", LookMode.Deep);

            Normalize();
        }
    }

    public class RenderNodeOrderOverride : IExposable
    {
        public string targetNodePath = string.Empty;
        public float drawOrderOffset = 0f;

        public RenderNodeOrderOverride Clone()
        {
            return new RenderNodeOrderOverride
            {
                targetNodePath = targetNodePath,
                drawOrderOffset = drawOrderOffset
            };
        }

        public void Normalize()
        {
            targetNodePath = targetNodePath?.Trim() ?? string.Empty;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref targetNodePath, "targetNodePath", string.Empty);
            Scribe_Values.Look(ref drawOrderOffset, "drawOrderOffset", 0f);
            Normalize();
        }
    }

    [StaticConstructorOnStartup]
    public static class RenderFixPatchRegistry
    {
        private static readonly List<CharacterRenderFixPatch> loadedPatches = new List<CharacterRenderFixPatch>();
        private static bool loaded;

        static RenderFixPatchRegistry() { }

        public static IEnumerable<CharacterRenderFixPatch> AllPatches
        {
            get
            {
                EnsureLoaded();
                return loadedPatches;
            }
        }

        public static IEnumerable<CharacterRenderFixPatch> GetApplicablePatches(Pawn? pawn)
        {
            EnsureLoaded();
            if (pawn == null)
            {
                yield break;
            }

            foreach (CharacterRenderFixPatch patch in loadedPatches)
            {
                if (patch != null && patch.MatchesPawn(pawn))
                {
                    yield return patch;
                }
            }
        }

        public static void ReloadFromConfig()
        {
            loaded = false;
            loadedPatches.Clear();
            EnsureLoaded();
        }

        public static CharacterRenderFixPatch CreatePatchForRace(string raceDefName, string? preferredDefName = null, string? preferredLabel = null)
        {
            EnsureLoaded();

            string normalizedRaceDefName = raceDefName?.Trim() ?? string.Empty;
            string defName = string.IsNullOrWhiteSpace(preferredDefName)
                ? $"CS_RenderFix_{normalizedRaceDefName}"
                : preferredDefName!.Trim();

            CharacterRenderFixPatch? existing = loadedPatches.FirstOrDefault(patch =>
                patch != null
                && !IsPawnScopedPatch(patch)
                && patch.targetRaceDefs != null
                && patch.targetRaceDefs.Count == 1
                && string.Equals(patch.targetRaceDefs[0], normalizedRaceDefName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(patch.defName, defName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return existing.Clone();
            }

            return new CharacterRenderFixPatch
            {
                defName = defName,
                label = string.IsNullOrWhiteSpace(preferredLabel) ? $"{normalizedRaceDefName} Render Fix" : preferredLabel!,
                enabled = true,
                targetRaceDefs = new List<string> { normalizedRaceDefName }
            };
        }

        public static void SavePatch(CharacterRenderFixPatch? patch)
        {
            if (patch == null)
            {
                return;
            }

            patch.Normalize();
            string directory = GetRenderFixDirectory();
            Directory.CreateDirectory(directory);

            string filePath = Path.Combine(directory, patch.defName + ".xml");
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false
            };

            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
            {
                WritePatchDocument(writer, patch);
            }

            int existingIndex = loadedPatches.FindIndex(existing => string.Equals(existing.defName, patch.defName, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                loadedPatches[existingIndex] = patch.Clone();
            }
            else
            {
                loadedPatches.Add(patch.Clone());
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            loadedPatches.Clear();

            string directory = GetRenderFixDirectory();
            Directory.CreateDirectory(directory);

            foreach (string file in Directory.GetFiles(directory, "*.xml"))
            {
                CharacterRenderFixPatch? patch = TryLoadPatchFile(file);
                if (patch != null)
                {
                    loadedPatches.Add(patch);
                }
            }
        }

        private static bool IsPawnScopedPatch(CharacterRenderFixPatch? patch)
        {
            if (patch == null || string.IsNullOrWhiteSpace(patch.defName))
            {
                return false;
            }

            int lastUnderscore = patch.defName.LastIndexOf('_');
            if (lastUnderscore < 0 || lastUnderscore >= patch.defName.Length - 1)
            {
                return false;
            }

            return int.TryParse(patch.defName.Substring(lastUnderscore + 1), out _);
        }

        private static CharacterRenderFixPatch? TryLoadPatchFile(string filePath)
        {
            try
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(filePath);
                XmlNode? root = xml.DocumentElement;
                if (root == null)
                {
                    return null;
                }

                CharacterRenderFixPatch? patch = null;
                if (string.Equals(root.Name, nameof(CharacterRenderFixPatch), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(root.Name, typeof(CharacterRenderFixPatch).FullName, StringComparison.OrdinalIgnoreCase))
                {
                    patch = DirectXmlToObject.ObjectFromXml<CharacterRenderFixPatch>(root, true);
                }
                else
                {
                    foreach (XmlNode child in root.ChildNodes)
                    {
                        if (child.NodeType != XmlNodeType.Element)
                        {
                            continue;
                        }

                        if (string.Equals(child.Name, nameof(CharacterRenderFixPatch), StringComparison.OrdinalIgnoreCase)
                            || string.Equals(child.Name, typeof(CharacterRenderFixPatch).FullName, StringComparison.OrdinalIgnoreCase))
                        {
                            patch = DirectXmlToObject.ObjectFromXml<CharacterRenderFixPatch>(child, true);
                            break;
                        }
                    }
                }

                patch?.Normalize();
                return patch;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 读取显示修正补丁失败: {filePath} - {ex.Message}");
                return null;
            }
        }

        private static void WritePatchDocument(XmlWriter writer, CharacterRenderFixPatch patch)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("RenderFixes");
            writer.WriteStartElement(nameof(CharacterRenderFixPatch));

            writer.WriteElementString("defName", patch.defName ?? string.Empty);
            writer.WriteElementString("label", patch.label ?? string.Empty);
            writer.WriteElementString("enabled", patch.enabled ? "true" : "false");

            writer.WriteStartElement("targetRaceDefs");
            foreach (string raceDefName in patch.targetRaceDefs ?? new List<string>())
            {
                writer.WriteElementString("li", raceDefName ?? string.Empty);
            }
            writer.WriteEndElement();

            writer.WriteStartElement("hideNodePaths");
            foreach (string nodePath in patch.hideNodePaths ?? new List<string>())
            {
                writer.WriteElementString("li", nodePath ?? string.Empty);
            }
            writer.WriteEndElement();

            writer.WriteStartElement("orderOverrides");
            foreach (RenderNodeOrderOverride orderOverride in patch.orderOverrides ?? new List<RenderNodeOrderOverride>())
            {
                if (orderOverride == null)
                {
                    continue;
                }

                writer.WriteStartElement("li");
                writer.WriteElementString("targetNodePath", orderOverride.targetNodePath ?? string.Empty);
                writer.WriteElementString("drawOrderOffset", orderOverride.drawOrderOffset.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        public static string GetRenderFixDirectory()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "RenderFixes");
        }
    }
}
