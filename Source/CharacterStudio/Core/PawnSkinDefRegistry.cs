using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using CharacterStudio.Abilities;
using CharacterStudio.Attributes;
using RimWorld;
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
        public static event Action<PawnSkinDef, bool>? RuntimeSkinRegisteredGlobal;

        private static readonly Dictionary<string, PawnSkinDef> runtimeDefsByName = new Dictionary<string, PawnSkinDef>();
        private static readonly HashSet<string> loggedInvalidSkinXmlFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> loggedSkinXmlWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool loaded;
        private static bool loading;

        // 静态构造器故意留空：初始化时机由 ModEntryPoint 统一控制（调用 LoadFromConfig），
        // 避免与 [StaticConstructorOnStartup] 触发的构造器产生双重加载。
        // TryGet / RegisterOrReplace 内部已有懒加载守卫，可安全调用。
        static PawnSkinDefRegistry() { }

        public static IEnumerable<PawnSkinDef> AllRuntimeDefs => runtimeDefsByName.Values;

        public static PawnSkinDef? GetDefaultSkinForRace(string? raceDefName)
        {
            if (string.IsNullOrWhiteSpace(raceDefName))
                return null;

            string resolvedRaceDefName = raceDefName!;

            if (!loaded && !loading)
                LoadFromConfig();

            return runtimeDefsByName.Values
                .Where(def => def != null && def.applyAsDefaultForTargetRaces)
                .Where(def => def.targetRaces != null && def.targetRaces.Contains(resolvedRaceDefName))
                .OrderByDescending(def => def.defaultRacePriority)
                .ThenBy(def => def.defName)
                .FirstOrDefault();
        }

        public static PawnSkinDef? GetDefaultSkinForRace(ThingDef? raceDef)
            => raceDef == null ? null : GetDefaultSkinForRace(raceDef.defName);

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
                RuntimeSkinRegisteredGlobal?.Invoke(oldDef, true);
                return oldDef;
            }

            DefDatabase<PawnSkinDef>.Add(def);
            DefDatabase<PawnSkinDef>.ResolveAllReferences(onlyExactlyMyType: false, parallel: false);
            runtimeDefsByName[def.defName] = def;
            RuntimeSkinRegisteredGlobal?.Invoke(def, false);

            return def;
        }

        /// <summary>
        /// 将 DefDatabase 中由 XML Defs 加载的 PawnSkinDef（如导出模组提供的）同步到运行时注册表。
        /// 这使得 GetDefaultSkinForRace / TryGet 等运行时查询能找到这些 Def，
        /// 从而让 PawnSkinBootstrapComponent 在加载存档时自动应用导出模组的皮肤。
        /// 
        /// 对于含 abilities 但缺少热键配置的 Def，自动按 Q/W/E/R... 顺序分配热键绑定，
        /// 确保自定义 Gizmo 能正确替换原版 Command_Ability。
        /// </summary>
        public static int SyncFromDefDatabase()
        {
            int count = 0;
            foreach (var def in DefDatabase<PawnSkinDef>.AllDefs)
            {
                if (def == null || string.IsNullOrEmpty(def.defName))
                    continue;

                // 跳过已在运行时注册表中的 Def（可能来自 Config 目录加载或显式 RegisterOrReplace）
                if (runtimeDefsByName.ContainsKey(def.defName))
                    continue;

                // 导出模组的 PawnSkinDef 通过 RimWorld 标准 DefDatabase 加载时，
                // abilityHotkeys.slotBindings（Dictionary<string,string>）无法被 DirectXmlToObject 正确反序列化，
                runtimeDefsByName[def.defName] = def;
                count++;
            }

            if (count > 0)
            {
                Log.Message($"[CharacterStudio] 已从 DefDatabase 同步 {count} 个皮肤定义到运行时注册表");
            }

            return count;
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
                    if (file.EndsWith(".character.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var def = LoadSkinDefFromFile(file);
                        if (def == null) continue;

                        RegisterOrReplace(def);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        LogSkinXmlWarningOnce(file, $"[CharacterStudio] 加载皮肤 XML 失败，已跳过: {file}, {ex.Message}");
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

        public static PawnSkinDef? LoadFromXml(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            return LoadSkinDefFromFile(filePath);
        }

        private static PawnSkinDef? LoadSkinDefFromFile(string file)
        {
            try
            {
                var xml = new XmlDocument();
                xml.Load(file);

                var root = xml.DocumentElement;
                if (root == null || !root.Name.Equals("Defs", StringComparison.OrdinalIgnoreCase))
                {
                    LogSkinXmlWarningOnce(file, $"[CharacterStudio] 皮肤文件根节点不是 Defs，已跳过: {file}");
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
                    LogSkinXmlWarningOnce(file, $"[CharacterStudio] 皮肤文件缺少 PawnSkinDef 节点，已跳过: {file}");
                    return null;
                }

                XmlNode parseNode = PrepareLegacyCompatibleDefNode(defNode);
                var def = DirectXmlToObject.ObjectFromXml<PawnSkinDef>(parseNode, true);
                DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
                if (def == null)
                {
                    LogSkinXmlWarningOnce(file, $"[CharacterStudio] 皮肤 XML 解析结果为空，已跳过: {file}");
                    return null;
                }

                RestoreStatModifiersFromXml(defNode, def);
                EnsureDefIdentity(def);
                return def;
            }
            catch (Exception ex)
            {
                LogSkinXmlWarningOnce(file, $"[CharacterStudio] 解析皮肤 XML 失败，已跳过: {file}, {ex.Message}");
                return null;
            }
        }

        private static XmlNode PrepareLegacyCompatibleDefNode(XmlNode defNode)
        {
            XmlNode clonedNode = defNode.CloneNode(deep: true);
            XmlNode? legacyHotkeysNode = clonedNode.SelectSingleNode("abilityHotkeys");
            if (legacyHotkeysNode != null)
            {
                legacyHotkeysNode.ParentNode?.RemoveChild(legacyHotkeysNode);
            }

            return clonedNode;
        }

        private static void LogSkinXmlWarningOnce(string filePath, string message)
        {
            string key = filePath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (loggedSkinXmlWarnings)
            {
                if (!loggedSkinXmlWarnings.Add(key))
                {
                    return;
                }
            }

            Log.Warning(message);
        }

        private static void RestoreStatModifiersFromXml(XmlNode defNode, PawnSkinDef def)
        {
            if (defNode == null || def == null)
            {
                return;
            }

            XmlNode? modifiersNode = defNode.SelectSingleNode("statModifiers");
            if (modifiersNode == null)
            {
                def.statModifiers ??= new CharacterStatModifierProfile();
                return;
            }

            var parsedProfile = new CharacterStatModifierProfile();
            XmlNodeList? entryNodes = modifiersNode.SelectNodes("entries/li");
            if (entryNodes == null || entryNodes.Count == 0)
            {
                entryNodes = modifiersNode.SelectNodes("li");
            }

            if (entryNodes != null)
            {
                foreach (XmlNode entryNode in entryNodes)
                {
                    string statDefName = entryNode.SelectSingleNode("statDefName")?.InnerText?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(statDefName))
                    {
                        continue;
                    }

                    CharacterStatModifierMode mode = CharacterStatModifierMode.Offset;
                    string modeText = entryNode.SelectSingleNode("mode")?.InnerText?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(modeText))
                    {
                        Enum.TryParse(modeText, ignoreCase: true, result: out mode);
                    }

                    float value = 0f;
                    string valueText = entryNode.SelectSingleNode("value")?.InnerText?.Trim() ?? "0";
                    float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

                    bool enabled = true;
                    string enabledText = entryNode.SelectSingleNode("enabled")?.InnerText?.Trim() ?? "true";
                    bool.TryParse(enabledText, out enabled);

                    parsedProfile.entries.Add(new CharacterStatModifierEntry { statDefName = statDefName, mode = mode, value = value, enabled = enabled });
                }
            }

            def.statModifiers = parsedProfile;
        }


        /// <summary>
        /// 将 source 的内容覆写到 target，保留 target 的 defName / defNameHash / modContentPack。
        /// 通过 Clone() 先完成深拷贝，再把需要同步的字段写回 target。
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
            target.applyAsDefaultForTargetRaces = cloned.applyAsDefaultForTargetRaces;
            target.defaultRacePriority = cloned.defaultRacePriority;
            target.author           = cloned.author;
            target.version          = cloned.version;
            target.previewTexPath   = cloned.previewTexPath;
            target.globalTextureScale = cloned.globalTextureScale;
            target.editLayerOffsetPerFacing = cloned.editLayerOffsetPerFacing;
            target.previewHeadOffsetZ = cloned.previewHeadOffsetZ;
            target.attributes       = cloned.attributes;
            target.statModifiers    = cloned.statModifiers;
            target.faceConfig       = cloned.faceConfig;
            target.baseAppearance   = cloned.baseAppearance;
            target.animationConfig  = cloned.animationConfig ?? new PawnAnimationConfig();
            target.xenotypeDefName  = cloned.xenotypeDefName;
            target.raceDisplayName  = cloned.raceDisplayName;

            target.layers.Clear();
            target.layers.AddRange(cloned.layers);

            target.expressions.Clear();
            target.expressions.AddRange(cloned.expressions);

            target.eyeDirection = cloned.eyeDirection;

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
