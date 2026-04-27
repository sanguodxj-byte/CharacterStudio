using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Verse;

namespace CharacterStudio.Core
{
    [StaticConstructorOnStartup]
    public static class CharacterRuntimeTriggerRegistry
    {
        public static event Action<CharacterRuntimeTriggerDef, bool>? RuntimeTriggerRegisteredGlobal;

        private static readonly Dictionary<string, CharacterRuntimeTriggerDef> runtimeTriggersByName = new Dictionary<string, CharacterRuntimeTriggerDef>(StringComparer.OrdinalIgnoreCase);
        private static bool loaded;
        private static bool loading;

        static CharacterRuntimeTriggerRegistry() { }

        public static IEnumerable<CharacterRuntimeTriggerDef> AllTriggers => EnumerateMergedTriggers();

        public static CharacterRuntimeTriggerDef? TryGet(string? defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return null;
            }

            EnsureLoaded();
            if (runtimeTriggersByName.TryGetValue(defName!, out CharacterRuntimeTriggerDef runtimeTrigger))
            {
                return runtimeTrigger;
            }

            return DefDatabase<CharacterRuntimeTriggerDef>.GetNamedSilentFail(defName);
        }

        public static CharacterRuntimeTriggerDef RegisterOrReplace(CharacterRuntimeTriggerDef? def)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }

            EnsureIdentity(def);
            bool replaced = runtimeTriggersByName.ContainsKey(def.defName);
            runtimeTriggersByName[def.defName] = def;
            RuntimeTriggerRegisteredGlobal?.Invoke(def, replaced);
            return def;
        }

        public static bool Unregister(string? defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return false;
            }

            return runtimeTriggersByName.Remove(defName!);
        }

        public static int UnregisterOwnedBy(string? ownerCharacterDefName)
        {
            if (string.IsNullOrWhiteSpace(ownerCharacterDefName))
            {
                return 0;
            }

            List<string> toRemove = runtimeTriggersByName
                .Where(pair => string.Equals(pair.Value?.ownerCharacterDefName, ownerCharacterDefName, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToList();

            foreach (string key in toRemove)
            {
                runtimeTriggersByName.Remove(key);
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
                    foreach (CharacterRuntimeTriggerDef def in LoadTriggerDefsFromFile(file))
                    {
                        RegisterOrReplace(def);
                        loadedCount++;
                    }
                }

                loaded = true;
                if (loadedCount > 0)
                {
                    Log.Message($"[CharacterStudio] 已从配置目录加载 {loadedCount} 个运行时触发器");
                }

                return loadedCount;
            }
            finally
            {
                loading = false;
            }
        }

        public static string GetConfigDir()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "RuntimeTriggers");
        }

        public static string GetDefaultTriggerDefName(string ownerCharacterDefName, int index)
        {
            return $"CS_RuntimeTrigger_{CharacterSpawnProfileRegistry.SanitizeDefName(ownerCharacterDefName)}_{Math.Max(1, index)}";
        }

        private static void EnsureLoaded()
        {
            if (!loaded && !loading)
            {
                LoadFromConfig();
            }
        }

        private static IEnumerable<CharacterRuntimeTriggerDef> EnumerateMergedTriggers()
        {
            EnsureLoaded();

            HashSet<string> yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CharacterRuntimeTriggerDef runtimeTrigger in runtimeTriggersByName.Values)
            {
                if (runtimeTrigger == null)
                {
                    continue;
                }

                yielded.Add(runtimeTrigger.defName);
                yield return runtimeTrigger;
            }

            foreach (CharacterRuntimeTriggerDef def in DefDatabase<CharacterRuntimeTriggerDef>.AllDefsListForReading)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.defName) || yielded.Contains(def.defName))
                {
                    continue;
                }

                yield return def;
            }
        }

        private static IEnumerable<CharacterRuntimeTriggerDef> LoadTriggerDefsFromFile(string filePath)
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

                if (!string.Equals(child.Name, nameof(CharacterRuntimeTriggerDef), StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(child.Name, typeof(CharacterRuntimeTriggerDef).FullName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CharacterRuntimeTriggerDef? def = DirectXmlToObject.ObjectFromXml<CharacterRuntimeTriggerDef>(child, true);
                DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
                if (def == null)
                {
                    continue;
                }

                EnsureIdentity(def);
                yield return def;
            }
        }

        private static void EnsureIdentity(CharacterRuntimeTriggerDef def)
        {
            if (string.IsNullOrWhiteSpace(def.defName))
            {
                def.defName = GetDefaultTriggerDefName(def.ownerCharacterDefName, 1);
            }

            if (string.IsNullOrWhiteSpace(def.label))
            {
                def.label = def.defName;
            }

            def.requiredConditions ??= new List<CharacterRuntimeTriggerCondition>();
            def.spawnSettings ??= new CharacterSpawnSettings();
        }
    }
}
