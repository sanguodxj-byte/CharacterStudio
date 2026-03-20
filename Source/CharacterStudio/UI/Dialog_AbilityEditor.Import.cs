using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private static string lastImportedAbilityXmlPath = string.Empty;
        private static string lastExportedAbilityXmlPath = string.Empty;

        private static string GetAbilityExportDir()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Abilities");
        }

        private static string GetDefaultAbilityExportFilePath()
        {
            return Path.Combine(GetAbilityExportDir(), "AbilityEditor_Export.xml");
        }

        private void OpenImportXmlDialog()
        {
            string initialPath = !string.IsNullOrWhiteSpace(lastImportedAbilityXmlPath)
                ? lastImportedAbilityXmlPath
                : (!string.IsNullOrWhiteSpace(lastExportedAbilityXmlPath) ? lastExportedAbilityXmlPath : GetDefaultAbilityExportFilePath());
            Find.WindowStack.Add(new Dialog_AbilityXmlImport(initialPath, ImportAbilitiesFromXmlPath));
        }

        private void ExportAbilitiesToDefaultPath()
        {
            try
            {
                string exportDir = GetAbilityExportDir();
                Directory.CreateDirectory(exportDir);
                string exportPath = GetDefaultAbilityExportFilePath();
                var doc = CreateAbilitiesDocument(abilities);
                doc.Save(exportPath);
                lastExportedAbilityXmlPath = exportPath;
                validationSummary = "导出成功: " + exportPath;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 技能 XML 导出失败: {ex}");
                validationSummary = "导出失败: " + ex.Message;
            }
        }

        private void SaveAbilityEditorSessionToDisk()
        {
            try
            {
                string exportDir = GetAbilityExportDir();
                Directory.CreateDirectory(exportDir);
                string sessionPath = Path.Combine(exportDir, "AbilityEditor_LastSession.xml");
                CreateAbilitiesDocument(abilities).Save(sessionPath);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 持久化技能编辑器列表失败: {ex.Message}");
            }
        }

        private static List<ModularAbilityDef> TryLoadAbilityEditorSessionFromDisk()
        {
            try
            {
                string sessionPath = Path.Combine(GetAbilityExportDir(), "AbilityEditor_LastSession.xml");
                if (!File.Exists(sessionPath))
                    return new List<ModularAbilityDef>();

                return LoadAbilitiesFromXmlFile(sessionPath);
            }
            catch
            {
                return new List<ModularAbilityDef>();
            }
        }

        private static System.Xml.Linq.XDocument CreateAbilitiesDocument(List<ModularAbilityDef> abilityList)
        {
            var defs = new System.Xml.Linq.XElement("Defs");
            var skinRoot = new System.Xml.Linq.XElement(nameof(PawnSkinDef));
            var abilitiesEl = new System.Xml.Linq.XElement("abilities");

            if (abilityList != null)
            {
                foreach (var ability in abilityList)
                {
                    if (ability == null || string.IsNullOrWhiteSpace(ability.defName))
                        continue;

                    var abilityEl = new System.Xml.Linq.XElement("li",
                        new System.Xml.Linq.XElement("defName", ability.defName),
                        !string.IsNullOrWhiteSpace(ability.label) ? new System.Xml.Linq.XElement("label", ability.label) : null,
                        !string.IsNullOrWhiteSpace(ability.description) ? new System.Xml.Linq.XElement("description", ability.description) : null,
                        !string.IsNullOrWhiteSpace(ability.iconPath) ? new System.Xml.Linq.XElement("iconPath", ability.iconPath) : null,
                        new System.Xml.Linq.XElement("cooldownTicks", ability.cooldownTicks),
                        new System.Xml.Linq.XElement("warmupTicks", ability.warmupTicks),
                        new System.Xml.Linq.XElement("charges", ability.charges),
                        new System.Xml.Linq.XElement("aiCanUse", ability.aiCanUse),
                        new System.Xml.Linq.XElement("carrierType", ability.carrierType),
                        new System.Xml.Linq.XElement("targetType", ability.targetType),
                        new System.Xml.Linq.XElement("useRadius", ability.useRadius.ToString().ToLowerInvariant()),
                        new System.Xml.Linq.XElement("areaCenter", ability.areaCenter),
                        new System.Xml.Linq.XElement("range", ability.range),
                        new System.Xml.Linq.XElement("radius", ability.radius),
                        ability.projectileDef != null ? new System.Xml.Linq.XElement("projectileDef", ability.projectileDef.defName) : null,
                        GenerateEffectsXml(ability.effects),
                        GenerateVisualEffectsXml(ability.visualEffects),
                        GenerateRuntimeComponentsXml(ability.runtimeComponents)
                    );

                    abilitiesEl.Add(abilityEl);
                }
            }

            skinRoot.Add(abilitiesEl);
            defs.Add(skinRoot);
            return new System.Xml.Linq.XDocument(defs);
        }

        private static System.Xml.Linq.XElement? GenerateEffectsXml(List<AbilityEffectConfig>? effects)
        {
            if (effects == null || effects.Count == 0) return null;

            var effectsEl = new System.Xml.Linq.XElement("effects");
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                effectsEl.Add(new System.Xml.Linq.XElement("li",
                    new System.Xml.Linq.XElement("type", effect.type.ToString()),
                    new System.Xml.Linq.XElement("amount", effect.amount),
                    new System.Xml.Linq.XElement("duration", effect.duration),
                    new System.Xml.Linq.XElement("chance", effect.chance),
                    effect.damageDef != null ? new System.Xml.Linq.XElement("damageDef", effect.damageDef.defName) : null,
                    effect.hediffDef != null ? new System.Xml.Linq.XElement("hediffDef", effect.hediffDef.defName) : null,
                    effect.summonKind != null ? new System.Xml.Linq.XElement("summonKind", effect.summonKind.defName) : null,
                    new System.Xml.Linq.XElement("summonCount", effect.summonCount)
                ));
            }
            return effectsEl;
        }

        private static System.Xml.Linq.XElement? GenerateVisualEffectsXml(List<AbilityVisualEffectConfig>? visualEffects)
        {
            if (visualEffects == null || visualEffects.Count == 0) return null;

            var root = new System.Xml.Linq.XElement("visualEffects");
            foreach (var vfx in visualEffects)
            {
                if (vfx == null) continue;
                root.Add(new System.Xml.Linq.XElement("li",
                    new System.Xml.Linq.XElement("type", vfx.type.ToString()),
                    new System.Xml.Linq.XElement("sourceMode", vfx.sourceMode.ToString()),
                    !string.IsNullOrEmpty(vfx.presetDefName) ? new System.Xml.Linq.XElement("presetDefName", vfx.presetDefName) : null,
                    new System.Xml.Linq.XElement("target", vfx.target.ToString()),
                    new System.Xml.Linq.XElement("trigger", vfx.trigger.ToString()),
                    new System.Xml.Linq.XElement("delayTicks", vfx.delayTicks),
                    new System.Xml.Linq.XElement("scale", vfx.scale),
                    new System.Xml.Linq.XElement("repeatCount", vfx.repeatCount),
                    new System.Xml.Linq.XElement("repeatIntervalTicks", vfx.repeatIntervalTicks),
                    new System.Xml.Linq.XElement("enabled", vfx.enabled.ToString().ToLower())
                ));
            }
            return root;
        }

        private static System.Xml.Linq.XElement? GenerateRuntimeComponentsXml(List<AbilityRuntimeComponentConfig>? components)
        {
            if (components == null || components.Count == 0) return null;

            var root = new System.Xml.Linq.XElement("runtimeComponents");
            foreach (var component in components)
            {
                if (component == null) continue;
                root.Add(new System.Xml.Linq.XElement("li",
                    new System.Xml.Linq.XElement("type", component.type.ToString()),
                    new System.Xml.Linq.XElement("enabled", component.enabled.ToString().ToLower()),
                    new System.Xml.Linq.XElement("comboWindowTicks", component.comboWindowTicks),
                    new System.Xml.Linq.XElement("cooldownTicks", component.cooldownTicks),
                    new System.Xml.Linq.XElement("jumpDistance", component.jumpDistance),
                    new System.Xml.Linq.XElement("findCellRadius", component.findCellRadius),
                    new System.Xml.Linq.XElement("triggerAbilityEffectsAfterJump", component.triggerAbilityEffectsAfterJump.ToString().ToLower())
                ));
            }
            return root;
        }

        private void ImportAbilitiesFromXmlPath(string xmlPath, bool replaceExisting)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(xmlPath))
                {
                    validationSummary = "CS_Studio_Msg_InvalidPath".Translate();
                    return;
                }

                string normalizedPath = xmlPath.Trim().Trim('"');
                if (!Path.IsPathRooted(normalizedPath))
                {
                    normalizedPath = Path.GetFullPath(normalizedPath);
                }

                if (!File.Exists(normalizedPath))
                {
                    validationSummary = "CS_Studio_Msg_InvalidPath".Translate() + $": {normalizedPath}";
                    return;
                }

                List<ModularAbilityDef> importedAbilities = LoadAbilitiesFromXmlFile(normalizedPath);
                if (importedAbilities.Count == 0)
                {
                    validationSummary = "CS_Studio_Ability_ImportNoAbilities".Translate();
                    return;
                }

                int beforeCount = abilities.Count;
                if (replaceExisting)
                {
                    abilities.Clear();
                }

                NormalizeImportedAbilityDefNames(importedAbilities, replaceExisting ? null : abilities);

                abilities.AddRange(importedAbilities);
                selectedAbility = importedAbilities[0];
                lastImportedAbilityXmlPath = normalizedPath;

                string sourceLabel = Path.GetFileName(normalizedPath);
                validationSummary = replaceExisting
                    ? "CS_Studio_Ability_ImportReplaced".Translate(importedAbilities.Count, sourceLabel)
                    : "CS_Studio_Ability_ImportAppended".Translate(importedAbilities.Count, sourceLabel, beforeCount, abilities.Count);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 技能 XML 导入失败: {ex}");
                validationSummary = "CS_Studio_Ability_ImportFailed".Translate(ex.Message);
            }
        }

        private static List<ModularAbilityDef> LoadAbilitiesFromXmlFile(string path)
        {
            var xml = new XmlDocument();
            xml.Load(path);

            var result = new List<ModularAbilityDef>();
            XmlNode? root = xml.DocumentElement;
            if (root == null)
            {
                return result;
            }

            CollectAbilitiesFromNode(root, result);
            return result;
        }

        private static void CollectAbilitiesFromNode(XmlNode node, List<ModularAbilityDef> result)
        {
            if (node.NodeType != XmlNodeType.Element)
            {
                return;
            }

            if (string.Equals(node.Name, "Defs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    CollectAbilitiesFromNode(child, result);
                }
                return;
            }

            if (IsModularAbilityNode(node.Name))
            {
                ModularAbilityDef? ability = ParseModularAbilityNode(node);
                if (ability != null)
                {
                    result.Add(ability);
                }
                return;
            }

            if (IsPawnSkinDefNode(node.Name))
            {
                CollectAbilitiesFromPawnSkinNode(node, result);
                return;
            }

            if (string.Equals(node.Name, "AbilityDef", StringComparison.OrdinalIgnoreCase))
            {
                ModularAbilityDef? ability = ParseAbilityDefNode(node);
                if (ability != null)
                {
                    result.Add(ability);
                }
            }
        }

        private static void CollectAbilitiesFromPawnSkinNode(XmlNode pawnSkinNode, List<ModularAbilityDef> result)
        {
            XmlNode? abilitiesNode = FindChildNode(pawnSkinNode, "abilities");
            if (abilitiesNode == null)
            {
                return;
            }

            foreach (XmlNode child in abilitiesNode.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (child.Name == "li" || IsModularAbilityNode(child.Name))
                {
                    ModularAbilityDef? ability = ParseModularAbilityNode(child);
                    if (ability != null)
                    {
                        result.Add(ability);
                    }
                }
            }
        }

        private static ModularAbilityDef? ParseModularAbilityNode(XmlNode node)
        {
            try
            {
                var imported = DirectXmlToObject.ObjectFromXml<ModularAbilityDef>(node, true);
                return FinalizeImportedAbility(imported);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 解析 ModularAbilityDef XML 失败: {ex.Message}");
                return null;
            }
        }

        private static ModularAbilityDef? ParseAbilityDefNode(XmlNode node)
        {
            try
            {
                string defName = GetChildText(node, "defName");
                string label = GetChildText(node, "label");
                string description = GetChildText(node, "description");
                string iconPath = GetChildText(node, "iconPath");
                string projectileDefName = GetChildText(node, "projectileDef");

                XmlNode? verbPropsNode = FindChildNode(node, "verbProperties");
                string verbClass = GetChildText(verbPropsNode, "verbClass");
                bool targetable = ParseBool(GetChildText(verbPropsNode, "targetable"), true);
                float warmupSeconds = ParseFirstFloat(GetChildText(verbPropsNode, "warmupTime"));
                float range = ParseFirstFloat(GetChildText(verbPropsNode, "range"));
                float radius = ParseFirstFloat(GetChildText(verbPropsNode, "radius"));
                float cooldownTicks = ParseFirstFloat(GetChildText(node, "cooldownTicksRange"));
                int charges = ParseInt(GetChildText(node, "charges"), 1);

                AbilityCarrierType resolvedCarrierType = ResolveCarrierType(verbClass, targetable, radius, projectileDefName);
                AbilityTargetType resolvedTargetType = ResolveTargetType(GetChildText(node, "targetType"), resolvedCarrierType, radius, projectileDefName);
                bool useRadius = ParseBool(GetChildText(node, "useRadius"), radius > 0.001f);
                AbilityAreaCenter areaCenter = ResolveAreaCenter(GetChildText(node, "areaCenter"), resolvedTargetType, useRadius);

                var ability = new ModularAbilityDef
                {
                    defName = defName,
                    label = string.IsNullOrWhiteSpace(label) ? defName : label,
                    description = description,
                    iconPath = iconPath,
                    cooldownTicks = cooldownTicks,
                    warmupTicks = warmupSeconds * 60f,
                    charges = charges,
                    carrierType = resolvedCarrierType,
                    targetType = resolvedTargetType,
                    useRadius = useRadius,
                    areaCenter = areaCenter,
                    range = range,
                    radius = radius
                };

                if (!string.IsNullOrWhiteSpace(projectileDefName))
                {
                    ability.projectileDef = DefDatabase<ThingDef>.GetNamedSilentFail(projectileDefName);
                }

                XmlNode? compsNode = FindChildNode(node, "comps");
                if (compsNode != null)
                {
                    foreach (XmlNode compNode in compsNode.ChildNodes)
                    {
                        if (compNode.NodeType != XmlNodeType.Element)
                        {
                            continue;
                        }

                        string className = compNode.Attributes?["Class"]?.Value ?? string.Empty;
                        if (!className.EndsWith("CompProperties_AbilityModular", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        ParseEffectList(compNode, "effects", ability.effects);
                        ParseVisualEffectList(compNode, "visualEffects", ability.visualEffects);
                        ParseRuntimeComponentList(compNode, "runtimeComponents", ability.runtimeComponents);
                    }
                }

                return FinalizeImportedAbility(ability);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 解析 AbilityDef XML 失败: {ex.Message}");
                return null;
            }
        }

        private static void ParseEffectList(XmlNode parentNode, string wrapperName, List<AbilityEffectConfig> result)
        {
            XmlNode? wrapperNode = FindChildNode(parentNode, wrapperName);
            if (wrapperNode == null)
            {
                return;
            }

            foreach (XmlNode child in wrapperNode.ChildNodes)
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
                    var effect = DirectXmlToObject.ObjectFromXml<AbilityEffectConfig>(child, true);
                    if (effect != null)
                    {
                        result.Add(effect);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 解析技能效果失败: {ex.Message}");
                }
            }
        }

        private static void ParseVisualEffectList(XmlNode parentNode, string wrapperName, List<AbilityVisualEffectConfig> result)
        {
            XmlNode? wrapperNode = FindChildNode(parentNode, wrapperName);
            if (wrapperNode == null)
            {
                return;
            }

            foreach (XmlNode child in wrapperNode.ChildNodes)
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
                    var vfx = DirectXmlToObject.ObjectFromXml<AbilityVisualEffectConfig>(child, true);
                    if (vfx != null)
                    {
                        result.Add(vfx);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 解析技能视觉特效失败: {ex.Message}");
                }
            }
        }

        private static void ParseRuntimeComponentList(XmlNode parentNode, string wrapperName, List<AbilityRuntimeComponentConfig> result)
        {
            XmlNode? wrapperNode = FindChildNode(parentNode, wrapperName);
            if (wrapperNode == null)
            {
                return;
            }

            foreach (XmlNode child in wrapperNode.ChildNodes)
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
                    var runtimeComponent = DirectXmlToObject.ObjectFromXml<AbilityRuntimeComponentConfig>(child, true);
                    if (runtimeComponent != null)
                    {
                        result.Add(runtimeComponent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 解析技能运行时组件失败: {ex.Message}");
                }
            }
        }

        private static ModularAbilityDef? FinalizeImportedAbility(ModularAbilityDef? imported)
        {
            if (imported == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(imported.defName))
            {
                imported.defName = $"CS_ImportedAbility_{Guid.NewGuid():N}";
            }

            if (string.IsNullOrWhiteSpace(imported.label))
            {
                imported.label = imported.defName;
            }

            return CreateEditableAbilityCopy(imported);
        }

        private static AbilityCarrierType ResolveCarrierType(string verbClass, bool targetable, float radius, string projectileDefName)
        {
            if (!string.IsNullOrWhiteSpace(projectileDefName))
            {
                return AbilityCarrierType.Projectile;
            }

            if (!targetable)
            {
                return AbilityCarrierType.Self;
            }

            if (!string.IsNullOrWhiteSpace(verbClass) &&
                verbClass.IndexOf("Touch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AbilityCarrierType.Target;
            }

            if (radius > 0.001f)
            {
                return AbilityCarrierType.Target;
            }

            return AbilityCarrierType.Target;
        }

        private static AbilityTargetType ResolveTargetType(string rawValue, AbilityCarrierType carrierType, float radius, string projectileDefName)
        {
            if (Enum.TryParse(rawValue, true, out AbilityTargetType parsed))
            {
                return parsed;
            }

            if (carrierType == AbilityCarrierType.Self)
            {
                return AbilityTargetType.Self;
            }

            if (radius > 0.001f)
            {
                return AbilityTargetType.Cell;
            }

            if (!string.IsNullOrWhiteSpace(projectileDefName))
            {
                return AbilityTargetType.Entity;
            }

            return AbilityTargetType.Entity;
        }

        private static AbilityAreaCenter ResolveAreaCenter(string rawValue, AbilityTargetType targetType, bool useRadius)
        {
            if (Enum.TryParse(rawValue, true, out AbilityAreaCenter parsed))
            {
                return parsed;
            }

            if (!useRadius)
            {
                return targetType == AbilityTargetType.Self
                    ? AbilityAreaCenter.Self
                    : AbilityAreaCenter.Target;
            }

            return targetType == AbilityTargetType.Self
                ? AbilityAreaCenter.Self
                : AbilityAreaCenter.Target;
        }

        private static bool IsModularAbilityNode(string nodeName)
        {
            return string.Equals(nodeName, nameof(ModularAbilityDef), StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeName, typeof(ModularAbilityDef).FullName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPawnSkinDefNode(string nodeName)
        {
            return string.Equals(nodeName, nameof(PawnSkinDef), StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeName, typeof(PawnSkinDef).FullName, StringComparison.OrdinalIgnoreCase);
        }

        private static XmlNode? FindChildNode(XmlNode? parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (string.Equals(child.Name, childName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        private static string GetChildText(XmlNode? parent, string childName)
        {
            XmlNode? child = FindChildNode(parent, childName);
            return child?.InnerText?.Trim() ?? string.Empty;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (bool.TryParse(value, out bool result))
            {
                return result;
            }

            return fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int direct))
            {
                return direct;
            }

            Match match = Regex.Match(value, @"-?\d+");
            if (match.Success &&
                int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int extracted))
            {
                return extracted;
            }

            return fallback;
        }

        private static float ParseFirstFloat(string value, float fallback = 0f)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float direct))
            {
                return direct;
            }

            Match match = Regex.Match(value, @"-?\d+(\.\d+)?");
            if (match.Success &&
                float.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float extracted))
            {
                return extracted;
            }

            return fallback;
        }
    }

    internal class Dialog_AbilityXmlImport : Window
    {
        private string xmlPath;
        private readonly Action<string, bool> onImport;

        public override Vector2 InitialSize => new Vector2(720f, 220f);

        public Dialog_AbilityXmlImport(string initialPath, Action<string, bool> onImport)
        {
            this.xmlPath = initialPath ?? string.Empty;
            this.onImport = onImport;
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            resizeable = false;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "CS_Studio_Ability_ImportXmlTitle".Translate());
            Text.Font = GameFont.Small;

            Widgets.Label(new Rect(0f, 38f, inRect.width, 44f), "CS_Studio_Ability_ImportXmlHint".Translate());

            Widgets.Label(new Rect(0f, 86f, 110f, 24f), "CS_Studio_Ability_ImportXmlPath".Translate());
            xmlPath = Widgets.TextField(new Rect(112f, 86f, inRect.width - 112f, 24f), xmlPath ?? string.Empty);

            float buttonY = inRect.height - 34f;
            float buttonWidth = (inRect.width - 10f) / 3f;

            if (Widgets.ButtonText(new Rect(0f, buttonY, buttonWidth, 30f), "CS_Studio_Ability_ImportReplace".Translate()))
            {
                onImport?.Invoke(xmlPath, true);
                Close();
            }

            if (Widgets.ButtonText(new Rect(buttonWidth + 5f, buttonY, buttonWidth, 30f), "CS_Studio_Ability_ImportAppend".Translate()))
            {
                onImport?.Invoke(xmlPath, false);
                Close();
            }

            if (Widgets.ButtonText(new Rect((buttonWidth + 5f) * 2f, buttonY, buttonWidth, 30f), "CS_Studio_Btn_Cancel".Translate()))
            {
                Close();
            }
        }
    }
}