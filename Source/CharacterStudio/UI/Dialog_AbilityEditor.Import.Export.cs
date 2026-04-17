using System;
using System.Collections.Generic;
using System.IO;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Exporter;
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

            Find.WindowStack.Add(new Dialog_FileBrowser(GetAbilityImportBrowseStartPath(initialPath), selectedPath =>
            {
                string normalizedPath = selectedPath?.Trim().Trim('"') ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    return;
                }

                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("CS_Studio_Ability_ImportReplace".Translate(), () => ImportAbilitiesFromXmlPath(normalizedPath, true)),
                    new FloatMenuOption("CS_Studio_Ability_ImportAppend".Translate(), () => ImportAbilitiesFromXmlPath(normalizedPath, false)),
                    new FloatMenuOption("CS_Studio_Btn_Cancel".Translate(), () => { })
                }));
            }, "*.xml", defaultRoot: GetAbilityExportDir()));
        }

        private static string GetAbilityImportBrowseStartPath(string initialPath)
        {
            if (string.IsNullOrWhiteSpace(initialPath))
            {
                return GetAbilityExportDir();
            }

            string normalizedPath = initialPath.Trim().Trim('"');
            if (Directory.Exists(normalizedPath))
            {
                return normalizedPath;
            }

            if (File.Exists(normalizedPath))
            {
                string? directory = Path.GetDirectoryName(normalizedPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
            }

            return GetAbilityExportDir();
        }

        private void ExportAbilitiesToDefaultPath()
        {
            try
            {
                string exportDir = GetAbilityExportDir();
                Directory.CreateDirectory(exportDir);
                string exportPath = GetDefaultAbilityExportFilePath();
                var doc = CreateAbilitiesDocument(abilities, GetCurrentHotkeyConfig());
                doc.Save(exportPath);
                lastExportedAbilityXmlPath = exportPath;
                validationSummary = "CS_Studio_Ability_ExportSuccess".Translate(exportPath);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 技能 XML 导出失败: {ex}");
                validationSummary = "CS_Studio_Ability_ExportFailed".Translate(ex.Message);
            }
        }

        private void ExportSelectedAbilityToXml()
        {
            if (selectedAbility == null)
            {
                validationSummary = "CS_Studio_Ability_SelectHint".Translate();
                return;
            }

            try
            {
                string exportDir = GetAbilityExportDir();
                Directory.CreateDirectory(exportDir);

                string safeName = string.IsNullOrWhiteSpace(selectedAbility.defName)
                    ? $"Ability_{Guid.NewGuid():N}"
                    : string.Join("_", selectedAbility.defName.Split(System.IO.Path.GetInvalidFileNameChars()));
                string exportPath = Path.Combine(exportDir, safeName + ".xml");

                var doc = CreateAbilitiesDocument(new List<ModularAbilityDef> { selectedAbility }, null);
                doc.Save(exportPath);
                lastExportedAbilityXmlPath = exportPath;
                validationSummary = "CS_Studio_Ability_ExportSuccess".Translate(exportPath);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 技能 XML 导出失败: {ex}");
                validationSummary = "CS_Studio_Ability_ExportFailed".Translate(ex.Message);
            }
        }

        private void SaveAbilityEditorSessionToDisk()
        {
            try
            {
                string exportDir = GetAbilityExportDir();
                Directory.CreateDirectory(exportDir);
                string sessionPath = Path.Combine(exportDir, "AbilityEditor_LastSession.xml");
                string? selectedDefName = selectedAbility?.defName;
                CreateAbilitiesDocument(abilities, GetCurrentHotkeyConfig(), selectedDefName).Save(sessionPath);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 持久化技能编辑器列表失败: {ex.Message}");
            }
        }

        private static List<ModularAbilityDef> TryLoadAbilityEditorSessionFromDisk(out SkinAbilityHotkeyConfig? hotkeys, out string? selectedAbilityDefName)
        {
            selectedAbilityDefName = null;
            try
            {
                string sessionPath = Path.Combine(GetAbilityExportDir(), "AbilityEditor_LastSession.xml");
                if (!File.Exists(sessionPath))
                {
                    hotkeys = null;
                    return new List<ModularAbilityDef>();
                }

                var result = LoadAbilitiesFromXmlFile(sessionPath, out hotkeys);

                // 尝试从 XML 提取编辑器选中状态
                try
                {
                    var doc = System.Xml.Linq.XDocument.Load(sessionPath);
                    selectedAbilityDefName = AbilityXmlSerialization.ExtractSelectedAbilityDefName(doc);
                }
                catch
                {
                    // 提取选中状态失败不影响主流程
                }

                return result;
            }
            catch
            {
                hotkeys = null;
                return new List<ModularAbilityDef>();
            }
        }

        /// <summary>
        /// 委托给 AbilityXmlSerialization.CreateEditorSessionDocument 以消除重复序列化代码。
        /// 所有字段序列化逻辑由 AbilityXmlSerialization 统一维护。
        /// </summary>
        private static System.Xml.Linq.XDocument CreateAbilitiesDocument(List<ModularAbilityDef> abilityList, SkinAbilityHotkeyConfig? hotkeys = null, string? selectedAbilityDefName = null)
        {
            return AbilityXmlSerialization.CreateEditorSessionDocument(abilityList, hotkeys, selectedAbilityDefName);
        }
    }
}
