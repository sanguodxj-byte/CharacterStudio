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
                // 导致 hotkeys.enabled=false 或 slotBindings 为空。
                // 自定义 Gizmo 的显示依赖 EnumerateVisibleAbilitySlots → hotkeys.enabled && HasAnyBinding()，
                // 所以这里需要自动补齐。
                EnsureDefaultHotkeyBindings(def);

                runtimeDefsByName[def.defName] = def;
                count++;
            }

            if (count > 0)
            {
                Log.Message($"[CharacterStudio] 已从 DefDatabase 同步 {count} 个皮肤定义到运行时注册表");
            }

            return count;
        }

        /// <summary>
        /// 为含 abilities 但缺少有效热键配置的 PawnSkinDef 自动分配默认热键绑定。
        /// 按 Q/W/E/R/T/A/S/D/F/Z/X/C/V 顺序将技能映射到槽位。
        /// </summary>
        private static void EnsureDefaultHotkeyBindings(PawnSkinDef def)
        {
            if (def.abilities == null || def.abilities.Count == 0)
                return;

            // 如果 hotkeys 已经有有效绑定，不需要覆盖
            if (def.abilityHotkeys != null && def.abilityHotkeys.enabled && def.abilityHotkeys.HasAnyBinding())
                return;

            def.abilityHotkeys ??= new SkinAbilityHotkeyConfig();
            def.abilityHotkeys.enabled = true;

            // 收集有效技能 defName，按原始顺序
            var validAbilities = def.abilities
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.defName))
                .ToList();

            if (validAbilities.Count == 0)
                return;

            // 按 Q/W/E/R/T/A/S/D/F/Z/X/C/V 顺序分配
            string[] slotKeys = { "Q", "W", "E", "R", "T", "A", "S", "D", "F", "Z", "X", "C", "V" };
            for (int i = 0; i < validAbilities.Count && i < slotKeys.Length; i++)
            {
                def.abilityHotkeys[slotKeys[i]] = validAbilities[i].defName!;
            }
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

                var def = DirectXmlToObject.ObjectFromXml<PawnSkinDef>(defNode, true);
                if (def == null)
                {
                    LogSkinXmlWarningOnce(file, $"[CharacterStudio] 皮肤 XML 解析结果为空，已跳过: {file}");
                    return null;
                }

                RestoreStatModifiersFromXml(defNode, def);
                RestoreAbilitiesFromXml(defNode, def);
                RestoreAbilityHotkeysFromXml(defNode, def);
                EnsureDefIdentity(def);
                return def;
            }
            catch (Exception ex)
            {
                LogSkinXmlWarningOnce(file, $"[CharacterStudio] 解析皮肤 XML 失败，已跳过: {file}, {ex.Message}");
                return null;
            }
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
        /// 从皮肤 XML 节点手动恢复技能列表。
        /// DirectXmlToObject 对嵌套 ModularAbilityDef（继承 Def）的子对象
        /// 反序列化不完整，需要手动解析并解析 Def 引用。
        /// </summary>
        private static void RestoreAbilitiesFromXml(XmlNode defNode, PawnSkinDef def)
        {
            if (defNode == null || def == null)
            {
                return;
            }

            XmlNode? abilitiesNode = defNode.SelectSingleNode("abilities");
            if (abilitiesNode == null)
            {
                def.abilities ??= new List<ModularAbilityDef>();
                return;
            }

            var restored = new List<ModularAbilityDef>();
            foreach (XmlNode child in abilitiesNode.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!string.Equals(child.Name, "li", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var ability = DirectXmlToObject.ObjectFromXml<ModularAbilityDef>(child, true);
                    if (ability != null)
                    {
                        ResolveAbilityDefReferences(ability, child);
                        if (string.IsNullOrWhiteSpace(ability.defName))
                        {
                            ability.defName = $"CS_RestoredAbility_{restored.Count}";
                        }

                        if (string.IsNullOrWhiteSpace(ability.label))
                        {
                            ability.label = ability.defName;
                        }

                        // 注册到 DefDatabase，以便 WarmupAllRuntimeAbilityDefs 能预热运行时 AbilityDef，
                        // 以及存档加载时 RehydrateAbilities 能通过 DefDatabase 查找恢复。
                        RegisterOrReplaceAbilityDef(ability);

                        restored.Add(ability);
                    }
                }
                catch (Exception ex)
                {
                    LogSkinXmlWarningOnce("(abilities)", $"[CharacterStudio] 恢复技能数据失败: {ex.Message}");
                }
            }

            def.abilities = restored;
        }

        /// <summary>
        /// 将 ModularAbilityDef 注册到 DefDatabase。
        /// 如果已存在同名 def 则跳过（保持首次注册的版本），
        /// 否则调用 Add 注册新 def。
        /// 这确保 WarmupAllRuntimeAbilityDefs 能预热运行时 AbilityDef，
        /// 以及存档加载时 RehydrateAbilities 能通过 DefDatabase 查找恢复。
        /// </summary>
        private static void RegisterOrReplaceAbilityDef(ModularAbilityDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.defName))
                return;

            try
            {
                if (DefDatabase<ModularAbilityDef>.GetNamedSilentFail(def.defName) != null)
                    return;

                DefDatabase<ModularAbilityDef>.Add(def);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 注册 ModularAbilityDef [{def.defName}] 到 DefDatabase 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 手动解析技能 XML 节点中的 Def 引用，确保 ThingDef / DamageDef 等在运行时可用。
        /// </summary>
        private static void ResolveAbilityDefReferences(ModularAbilityDef ability, XmlNode abilityNode)
        {
            // 顶层 projectileDef
            string? projectileDefName = abilityNode.SelectSingleNode("projectileDef")?.InnerText?.Trim();
            ability.projectileDef = !string.IsNullOrWhiteSpace(projectileDefName)
                ? DefDatabase<ThingDef>.GetNamedSilentFail(projectileDefName)
                : null;

            // 效果列表中的 Def 引用
            if (ability.effects != null)
            {
                XmlNode? effectsNode = abilityNode.SelectSingleNode("effects");
                XmlNodeList? effectNodes = effectsNode?.SelectNodes("li");
                if (effectNodes != null)
                {
                    for (int i = 0; i < effectNodes.Count && i < ability.effects.Count; i++)
                    {
                        ResolveEffectDefReferences(ability.effects[i], effectNodes[i]);
                    }
                }
            }

            // 视觉特效：归一化遗留数据
            if (ability.visualEffects != null)
            {
                foreach (var vfx in ability.visualEffects)
                {
                    vfx?.NormalizeLegacyData();
                    vfx?.SyncLegacyFields();
                }
            }

            // 运行时组件中的 Def 引用
            if (ability.runtimeComponents != null)
            {
                XmlNode? rcNode = abilityNode.SelectSingleNode("runtimeComponents");
                XmlNodeList? rcNodes = rcNode?.SelectNodes("li");
                if (rcNodes != null)
                {
                    for (int i = 0; i < rcNodes.Count && i < ability.runtimeComponents.Count; i++)
                    {
                        ResolveRuntimeComponentDefReferences(ability.runtimeComponents[i], rcNodes[i]);
                    }
                }
            }
        }

        private static void ResolveEffectDefReferences(AbilityEffectConfig? effect, XmlNode? effectNode)
        {
            if (effect == null || effectNode == null)
            {
                return;
            }

            effect.damageDef = ResolveDef<DamageDef>(effectNode, "damageDef");
            effect.hediffDef = ResolveDef<HediffDef>(effectNode, "hediffDef");
            effect.summonKind = ResolveDef<PawnKindDef>(effectNode, "summonKind");
            effect.summonFactionDef = ResolveDef<FactionDef>(effectNode, "summonFactionDef");
            effect.terraformThingDef = ResolveDef<ThingDef>(effectNode, "terraformThingDef");
            effect.terraformTerrainDef = ResolveDef<TerrainDef>(effectNode, "terraformTerrainDef");
        }

        private static void ResolveRuntimeComponentDefReferences(AbilityRuntimeComponentConfig? component, XmlNode? componentNode)
        {
            if (component == null || componentNode == null)
            {
                return;
            }

            component.waveDamageDef = ResolveDef<DamageDef>(componentNode, "waveDamageDef");
            component.markDamageDef = ResolveDef<DamageDef>(componentNode, "markDamageDef");
            component.landingBurstDamageDef = ResolveDef<DamageDef>(componentNode, "landingBurstDamageDef");
        }

        /// <summary>
        /// 从 XML 子节点按名称查找 defName 文本，再从 DefDatabase 获取对应 Def。
        /// 找不到或为空时返回 null。
        /// </summary>
        private static T? ResolveDef<T>(XmlNode parentNode, string elementName) where T : Def, new()
        {
            string? defName = parentNode.SelectSingleNode(elementName)?.InnerText?.Trim();
            return !string.IsNullOrWhiteSpace(defName)
                ? DefDatabase<T>.GetNamedSilentFail(defName)
                : null;
        }

        /// <summary>
        /// 从皮肤 XML 节点手动恢复热键配置。
        /// SkinAbilityHotkeyConfig.slotBindings 使用动态键名（如 qAbilityDefName），
        /// DirectXmlToObject 无法将其映射回 Dictionary，因此需要手动解析。
        /// </summary>
        private static void RestoreAbilityHotkeysFromXml(XmlNode defNode, PawnSkinDef def)
        {
            if (defNode == null || def == null)
            {
                return;
            }

            XmlNode? hotkeysNode = defNode.SelectSingleNode("abilityHotkeys");
            if (hotkeysNode == null)
            {
                def.abilityHotkeys ??= new SkinAbilityHotkeyConfig();
                return;
            }

            var config = new SkinAbilityHotkeyConfig();

            string enabledText = hotkeysNode.SelectSingleNode("enabled")?.InnerText?.Trim() ?? "false";
            config.enabled = bool.TryParse(enabledText, out bool enabled) && enabled;

            foreach (string slotKey in new[] { "Q", "W", "E", "R", "T", "A", "S", "D", "F", "Z", "X", "C", "V" })
            {
                string elementName = slotKey.ToLowerInvariant() + "AbilityDefName";
                string? rawValue = hotkeysNode.SelectSingleNode(elementName)?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(rawValue))
                {
                    config[slotKey] = rawValue!;
                }
            }

            def.abilityHotkeys = config;
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
            target.attributes       = cloned.attributes;
            target.statModifiers    = cloned.statModifiers;
            target.abilityHotkeys   = cloned.abilityHotkeys;
            target.faceConfig       = cloned.faceConfig;
            target.baseAppearance   = cloned.baseAppearance;
            target.animationConfig  = cloned.animationConfig ?? new PawnAnimationConfig();
            target.xenotypeDefName  = cloned.xenotypeDefName;
            target.raceDisplayName  = cloned.raceDisplayName;

            target.equipments.Clear();
            target.equipments.AddRange(cloned.equipments);

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
