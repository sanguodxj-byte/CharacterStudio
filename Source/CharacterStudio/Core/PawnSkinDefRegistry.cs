using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using CharacterStudio.Abilities;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 运行时 SkinDef 注册器
    /// 负责从 Config/CharacterStudio/Skins 目录加载 XML，并注册到 DefDatabase
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PawnSkinDefRegistry
    {
        private static readonly Dictionary<string, PawnSkinDef> runtimeDefsByName = new Dictionary<string, PawnSkinDef>();
        private static bool loaded;
        private static bool loading;

        static PawnSkinDefRegistry()
        {
            try
            {
                LoadFromConfig();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 初始化 PawnSkinDefRegistry 失败: {ex}");
            }
        }

        public static IEnumerable<PawnSkinDef> AllRuntimeDefs => runtimeDefsByName.Values;

        public static PawnSkinDef? TryGet(string? defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;

            if (!loaded && !loading)
            {
                LoadFromConfig();
            }

            runtimeDefsByName.TryGetValue(defName!, out var def);
            return def;
        }

        public static PawnSkinDef RegisterOrReplace(PawnSkinDef? def)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }

            EnsureDefIdentity(def);

            if (!loaded && !loading)
            {
                LoadFromConfig();
            }

            var oldDef = DefDatabase<PawnSkinDef>.GetNamedSilentFail(def.defName);
            if (oldDef != null)
            {
                OverwriteDefContent(oldDef, def);
                oldDef.ResolveDefNameHash();
                DefDatabase<PawnSkinDef>.ResolveAllReferences(onlyExactlyMyType: false, parallel: false);
                runtimeDefsByName[oldDef.defName] = oldDef;
                return oldDef;
            }

            DefDatabase<PawnSkinDef>.Add(def);
            DefDatabase<PawnSkinDef>.ResolveAllReferences(onlyExactlyMyType: false, parallel: false);
            runtimeDefsByName[def.defName] = def;

            return def;
        }

        public static int LoadFromConfig()
        {
            if (loading) return 0;
            loading = true;

            try
            {
                string dir = GetConfigDir();
                if (!Directory.Exists(dir))
                {
                    loaded = true;
                    return 0;
                }

                int count = 0;
                foreach (var file in Directory.GetFiles(dir, "*.xml", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var def = LoadSkinDefFromFile(file);
                        if (def == null) continue;

                        RegisterOrReplace(def);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[CharacterStudio] 加载皮肤 XML 失败: {file}, {ex}");
                    }
                }

                loaded = true;
                if (count > 0)
                {
                    Log.Message($"[CharacterStudio] 已从配置目录加载 {count} 个皮肤定义");
                }
                return count;
            }
            finally
            {
                loading = false;
            }
        }

        private static void EnsureDefIdentity(PawnSkinDef def)
        {
            if (string.IsNullOrEmpty(def.defName))
            {
                def.defName = $"CS_RuntimeSkin_{Guid.NewGuid():N}";
            }

            if (string.IsNullOrEmpty(def.label))
            {
                def.label = def.defName;
            }

            def.ResolveDefNameHash();
        }

        private static string GetConfigDir()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Skins");
        }

        private static PawnSkinDef? LoadSkinDefFromFile(string file)
        {
            var xml = new XmlDocument();
            xml.Load(file);

            var root = xml.DocumentElement;
            if (root == null || !root.Name.Equals("Defs", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning($"[CharacterStudio] 皮肤文件不是 Defs 根节点: {file}");
                return null;
            }

            XmlNode? defNode = null;
            foreach (XmlNode child in root.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;
                if (child.Name == nameof(PawnSkinDef) || child.Name == typeof(PawnSkinDef).FullName)
                {
                    defNode = child;
                    break;
                }
            }

            if (defNode == null)
            {
                return null;
            }

            var def = DirectXmlToObject.ObjectFromXml<PawnSkinDef>(defNode, true);
            EnsureDefIdentity(def);
            return def;
        }

        private static void OverwriteDefContent(PawnSkinDef target, PawnSkinDef source)
        {
            target.label = source.label;
            target.description = source.description;
            target.hideVanillaHead = source.hideVanillaHead;
            target.hideVanillaHair = source.hideVanillaHair;
            target.hideVanillaBody = source.hideVanillaBody;
            target.hideVanillaApparel = source.hideVanillaApparel;
            target.humanlikeOnly = source.humanlikeOnly;
            target.author = source.author;
            target.version = source.version;
            target.previewTexPath = source.previewTexPath;

            target.abilityHotkeys = source.abilityHotkeys?.Clone() ?? new SkinAbilityHotkeyConfig();

            target.abilities.Clear();
            if (source.abilities != null)
            {
                foreach (var ability in source.abilities)
                {
                    if (ability != null)
                    {
                        target.abilities.Add(ability.Clone());
                    }
                }
            }

            target.layers.Clear();
            if (source.layers != null)
            {
                foreach (var layer in source.layers)
                {
                    target.layers.Add(layer.Clone());
                }
            }

            target.targetRaces.Clear();
            if (source.targetRaces != null)
            {
                target.targetRaces.AddRange(source.targetRaces);
            }

            target.hiddenPaths.Clear();
            if (source.hiddenPaths != null)
            {
                target.hiddenPaths.AddRange(source.hiddenPaths);
            }

            #pragma warning disable CS0618
            target.hiddenTags.Clear();
            if (source.hiddenTags != null)
            {
                target.hiddenTags.AddRange(source.hiddenTags);
            }
            #pragma warning restore CS0618

            if (target.faceConfig == null)
            {
                target.faceConfig = new PawnFaceConfig();
            }
            CopyFaceConfig(target.faceConfig, source.faceConfig);
        }

        private static void CopyFaceConfig(PawnFaceConfig target, PawnFaceConfig? source)
        {
            if (source == null)
            {
                target.enabled = false;
                target.components.Clear();
                return;
            }

            target.enabled = source.enabled;
            target.components.Clear();

            if (source.components == null) return;

            foreach (var component in source.components)
            {
                var compClone = new FaceComponentMapping { type = component.type };
                if (component.expressions != null)
                {
                    foreach (var expr in component.expressions)
                    {
                        compClone.expressions.Add(new ExpressionTexPath
                        {
                            expression = expr.expression,
                            texPath = expr.texPath
                        });
                    }
                }
                target.components.Add(compClone);
            }
        }
    }
}
