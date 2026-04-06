using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
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
                if (!System.IO.Path.IsPathRooted(normalizedPath))
                {
                    normalizedPath = System.IO.Path.GetFullPath(normalizedPath);
                }

                if (!System.IO.File.Exists(normalizedPath))
                {
                    validationSummary = "CS_Studio_Msg_InvalidPath".Translate() + $": {normalizedPath}";
                    return;
                }

                List<ModularAbilityDef> importedAbilities = LoadAbilitiesFromXmlFile(normalizedPath, out SkinAbilityHotkeyConfig? importedHotkeys);
                Dictionary<ModularAbilityDef, string> originalImportedDefNames = importedAbilities
                    .Where(ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                    .ToDictionary(ability => ability, ability => ability.defName!);
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
                if (importedHotkeys != null)
                {
                    RemapImportedHotkeyConfig(importedHotkeys, originalImportedDefNames);
                }

                abilities.AddRange(importedAbilities);
                selectedAbility = importedAbilities[0];
                if (importedHotkeys != null)
                {
                    ApplyHotkeyConfig(importedHotkeys);
                }
                lastImportedAbilityXmlPath = normalizedPath;
                NotifyAbilityPreviewDirty(true);

                string sourceLabel = System.IO.Path.GetFileName(normalizedPath);
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
            return LoadAbilitiesFromXmlFile(path, out _);
        }

        private static List<ModularAbilityDef> LoadAbilitiesFromXmlFile(string path, out SkinAbilityHotkeyConfig? hotkeys)
        {
            var xml = new XmlDocument();
            xml.Load(path);

            var result = new List<ModularAbilityDef>();
            hotkeys = null;
            XmlNode? root = xml.DocumentElement;
            if (root == null)
            {
                return result;
            }

            CollectAbilitiesFromNode(root, result, ref hotkeys);
            return result;
        }

        private static void CollectAbilitiesFromNode(XmlNode node, List<ModularAbilityDef> result, ref SkinAbilityHotkeyConfig? hotkeys)
        {
            if (node.NodeType != XmlNodeType.Element)
            {
                return;
            }

            if (string.Equals(node.Name, "Defs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    CollectAbilitiesFromNode(child, result, ref hotkeys);
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
                CollectAbilitiesFromPawnSkinNode(node, result, ref hotkeys);
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

        private static void CollectAbilitiesFromPawnSkinNode(XmlNode pawnSkinNode, List<ModularAbilityDef> result, ref SkinAbilityHotkeyConfig? hotkeys)
        {
            XmlNode? abilitiesNode = FindChildNode(pawnSkinNode, "abilities");
            if (abilitiesNode != null)
            {
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

            hotkeys ??= ParseAbilityHotkeysNode(FindChildNode(pawnSkinNode, "abilityHotkeys"));
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
                        vfx.NormalizeLegacyData();
                        vfx.SyncLegacyFields();
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
                        if (runtimeComponent.type == AbilityRuntimeComponentType.VanillaPawnFlyer)
                        {
                            runtimeComponent.type = AbilityRuntimeComponentType.FlightState;
                            runtimeComponent.flyerThingDefName = string.Empty;
                            runtimeComponent.flyerWarmupTicks = 0;
                            runtimeComponent.launchFromCasterPosition = true;
                            runtimeComponent.requireValidTargetCell = false;
                            runtimeComponent.storeTargetForFollowup = false;
                            runtimeComponent.enableFlightOnlyWindow = false;
                            runtimeComponent.flightOnlyWindowTicks = 180;
                            runtimeComponent.flightOnlyAbilityDefName = string.Empty;
                            runtimeComponent.hideCasterDuringTakeoff = false;
                            runtimeComponent.autoExpireFlightMarkerOnLanding = true;
                        }

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

        private static SkinAbilityHotkeyConfig? ParseAbilityHotkeysNode(XmlNode? node)
        {
            if (node == null)
            {
                return null;
            }

            return new SkinAbilityHotkeyConfig
            {
                enabled = ParseBool(GetChildText(node, "enabled"), false),
                qAbilityDefName = GetChildText(node, "qAbilityDefName"),
                wAbilityDefName = GetChildText(node, "wAbilityDefName"),
                eAbilityDefName = GetChildText(node, "eAbilityDefName"),
                rAbilityDefName = GetChildText(node, "rAbilityDefName"),
                wComboAbilityDefName = GetChildText(node, "wComboAbilityDefName")
            };
        }

        private static void RemapImportedHotkeyConfig(SkinAbilityHotkeyConfig hotkeys, Dictionary<ModularAbilityDef, string> originalDefNames)
        {
            hotkeys.qAbilityDefName = RemapImportedHotkeyDefName(hotkeys.qAbilityDefName, originalDefNames);
            hotkeys.wAbilityDefName = RemapImportedHotkeyDefName(hotkeys.wAbilityDefName, originalDefNames);
            hotkeys.eAbilityDefName = RemapImportedHotkeyDefName(hotkeys.eAbilityDefName, originalDefNames);
            hotkeys.rAbilityDefName = RemapImportedHotkeyDefName(hotkeys.rAbilityDefName, originalDefNames);
            hotkeys.wComboAbilityDefName = RemapImportedHotkeyDefName(hotkeys.wComboAbilityDefName, originalDefNames);
        }

        private static string RemapImportedHotkeyDefName(string? defName, Dictionary<ModularAbilityDef, string> originalDefNames)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return string.Empty;
            }

            string normalizedDefName = defName!.Trim();
            foreach ((ModularAbilityDef ability, string originalDefName) in originalDefNames)
            {
                if (string.Equals(originalDefName, normalizedDefName, StringComparison.OrdinalIgnoreCase))
                {
                    return ability.defName ?? string.Empty;
                }
            }

            return normalizedDefName;
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
}
