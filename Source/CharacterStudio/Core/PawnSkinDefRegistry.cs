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

        // 静态构造器故意留空：初始化时机由 ModEntryPoint 统一控制（调用 LoadFromConfig），
        // 避免与 [StaticConstructorOnStartup] 触发的构造器产生双重加载。
        // TryGet / RegisterOrReplace 内部已有懒加载守卫，可安全调用。
        static PawnSkinDefRegistry() { }

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

        /// <summary>
        /// 将 source 的所有内容覆写到 target，保留 target 的 defName / defNameHash / modContentPack。
        /// 使用 Clone() 实现，避免手工字段列举遗漏新增字段的风险。
        /// </summary>
        private static void OverwriteDefContent(PawnSkinDef target, PawnSkinDef source)
        {
            // 保存需要保持不变的标识字段
            string savedDefName      = target.defName;
            int    savedDefNameHash  = target.shortHash;

            // 用 Clone 复制所有内容字段（包括未来新增的字段）
            var cloned = source.Clone();

            // 将 clone 的所有字段反写到 target（逐字段赋值保持对象引用不变，
            // 因为 DefDatabase / runtimeDefsByName 持有 target 的引用）
            target.label            = cloned.label;
            target.description      = cloned.description;
            target.hideVanillaHead  = cloned.hideVanillaHead;
            target.hideVanillaHair  = cloned.hideVanillaHair;
            target.hideVanillaBody  = cloned.hideVanillaBody;
            target.hideVanillaApparel= cloned.hideVanillaApparel;
            target.humanlikeOnly    = cloned.humanlikeOnly;
            target.author           = cloned.author;
            target.version          = cloned.version;
            target.previewTexPath   = cloned.previewTexPath;
            target.attributes       = cloned.attributes;
            target.abilityHotkeys   = cloned.abilityHotkeys;
            target.faceConfig       = cloned.faceConfig;
            target.baseAppearance   = cloned.baseAppearance;

            target.abilities.Clear();
            target.abilities.AddRange(cloned.abilities);

            target.layers.Clear();
            target.layers.AddRange(cloned.layers);

            target.targetRaces.Clear();
            target.targetRaces.AddRange(cloned.targetRaces);

            target.hiddenPaths.Clear();
            target.hiddenPaths.AddRange(cloned.hiddenPaths);

#pragma warning disable CS0618
            target.hiddenTags.Clear();
            target.hiddenTags.AddRange(cloned.hiddenTags);
#pragma warning restore CS0618

            // 恢复标识字段（不能被 Clone 覆盖）
            target.defName   = savedDefName;
            target.shortHash = (ushort)savedDefNameHash;
        }
    }
}

