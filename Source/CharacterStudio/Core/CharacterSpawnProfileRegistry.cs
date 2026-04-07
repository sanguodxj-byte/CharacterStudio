using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Verse;

namespace CharacterStudio.Core
{
    [StaticConstructorOnStartup]
    public static class CharacterSpawnProfileRegistry
    {
        public static event Action<CharacterSpawnProfileDef, bool>? RuntimeSpawnProfileRegisteredGlobal;

        private static readonly Dictionary<string, CharacterSpawnProfileDef> runtimeProfilesByName = new Dictionary<string, CharacterSpawnProfileDef>(StringComparer.OrdinalIgnoreCase);
        private static bool loaded;
        private static bool loading;

        static CharacterSpawnProfileRegistry() { }

        public static IEnumerable<CharacterSpawnProfileDef> AllProfiles => EnumerateMergedProfiles();

        public static CharacterSpawnProfileDef? TryGet(string? defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return null;
            }

            EnsureLoaded();
            if (runtimeProfilesByName.TryGetValue(defName!, out CharacterSpawnProfileDef runtimeProfile))
            {
                return runtimeProfile;
            }

            return DefDatabase<CharacterSpawnProfileDef>.GetNamedSilentFail(defName);
        }

        public static CharacterSpawnProfileDef RegisterOrReplace(CharacterSpawnProfileDef? def)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }

            EnsureIdentity(def);
            bool replaced = runtimeProfilesByName.ContainsKey(def.defName);
            runtimeProfilesByName[def.defName] = def;
            RuntimeSpawnProfileRegisteredGlobal?.Invoke(def, replaced);
            return def;
        }

        public static bool Unregister(string? defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return false;
            }

            return runtimeProfilesByName.Remove(defName!);
        }

        public static int UnregisterOwnedBy(string? ownerCharacterDefName)
        {
            if (string.IsNullOrWhiteSpace(ownerCharacterDefName))
            {
                return 0;
            }

            List<string> toRemove = runtimeProfilesByName
                .Where(pair => string.Equals(pair.Value?.ownerCharacterDefName, ownerCharacterDefName, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToList();

            foreach (string key in toRemove)
            {
                runtimeProfilesByName.Remove(key);
            }

            return toRemove.Count;
        }

        public static int LoadFromConfig()
        {
            if (loading)
            {
                return 0;
            }

            loading = true;
            try
            {
                string dir = GetConfigDir();
                if (!Directory.Exists(dir))
                {
                    loaded = true;
                    return 0;
                }

                int loadedCount = 0;
                foreach (string file in Directory.GetFiles(dir, "*.xml", SearchOption.TopDirectoryOnly))
                {
                    foreach (CharacterSpawnProfileDef def in LoadProfileDefsFromFile(file))
                    {
                        RegisterOrReplace(def);
                        loadedCount++;
                    }
                }

                loaded = true;
                if (loadedCount > 0)
                {
                    Log.Message($"[CharacterStudio] 已从配置目录加载 {loadedCount} 个运行时角色配置");
                }

                return loadedCount;
            }
            finally
            {
                loading = false;
            }
        }

        public static string GetDefaultProfileDefName(string ownerCharacterDefName)
        {
            return $"CS_RuntimeProfile_{SanitizeDefName(ownerCharacterDefName)}";
        }

        public static string GetConfigDir()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "SpawnProfiles");
        }

        public static string SanitizeDefName(string? value)
        {
            string raw = value ?? string.Empty;
            char[] chars = raw
                .Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_')
                .ToArray();
            string sanitized = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(sanitized) ? "Character" : sanitized;
        }

        private static void EnsureLoaded()
        {
            if (!loaded && !loading)
            {
                LoadFromConfig();
            }
        }

        private static IEnumerable<CharacterSpawnProfileDef> EnumerateMergedProfiles()
        {
            EnsureLoaded();

            HashSet<string> yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CharacterSpawnProfileDef runtimeProfile in runtimeProfilesByName.Values)
            {
                if (runtimeProfile == null)
                {
                    continue;
                }

                yielded.Add(runtimeProfile.defName);
                yield return runtimeProfile;
            }

            foreach (CharacterSpawnProfileDef def in DefDatabase<CharacterSpawnProfileDef>.AllDefsListForReading)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.defName) || yielded.Contains(def.defName))
                {
                    continue;
                }

                yield return def;
            }
        }

        private static IEnumerable<CharacterSpawnProfileDef> LoadProfileDefsFromFile(string filePath)
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load(filePath);
            XmlNode? root = xmlDocument.DocumentElement;
            if (root == null || !string.Equals(root.Name, "Defs", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            foreach (XmlNode child in root.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!string.Equals(child.Name, nameof(CharacterSpawnProfileDef), StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(child.Name, typeof(CharacterSpawnProfileDef).FullName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CharacterSpawnProfileDef? def = DirectXmlToObject.ObjectFromXml<CharacterSpawnProfileDef>(child, true);
                if (def == null)
                {
                    continue;
                }

                EnsureIdentity(def);
                yield return def;
            }
        }

        private static void EnsureIdentity(CharacterSpawnProfileDef def)
        {
            if (string.IsNullOrWhiteSpace(def.defName))
            {
                def.defName = GetDefaultProfileDefName(def.ownerCharacterDefName);
            }

            if (string.IsNullOrWhiteSpace(def.label))
            {
                def.label = def.characterDefinition?.displayName;
            }

            def.characterDefinition ??= new CharacterDefinition();
            def.characterDefinition.runtimeTriggers ??= new List<CharacterRuntimeTriggerDef>();
        }
    }
}
