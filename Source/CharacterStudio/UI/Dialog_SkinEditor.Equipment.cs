using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using CharacterStudio.Core;
using CharacterStudio.Exporter;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private static string lastImportedEquipmentXmlPath = string.Empty;
        private static string lastExportedEquipmentXmlPath = string.Empty;

        private void DrawEquipmentPanel(Rect rect)
        {
            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();

            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, 26f);
            Widgets.DrawBoxSolid(titleRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(titleRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), "CS_Studio_Tab_Equipment".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            float btnY = titleRect.yMax + 6f;
            float btnCount = 6f;
            float btnWidth = (rect.width - Margin * (btnCount + 1f)) / btnCount;
            float btnHeight = Mathf.Max(ButtonHeight - 2f, 22f);

            bool DrawIconButton(Rect buttonRect, string label, string tooltip, Action action, bool accent = false)
            {
                Widgets.DrawBoxSolid(buttonRect, accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
                Widgets.DrawBoxSolid(
                    new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f),
                    accent ? UIHelper.AccentColor : new Color(1f, 1f, 1f, 0.05f));
                GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
                Widgets.DrawBox(buttonRect, 1);
                GUI.color = Color.white;

                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = accent ? Color.white : UIHelper.HeaderColor;
                Widgets.Label(buttonRect, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = prevFont;

                TooltipHandler.TipRegion(buttonRect, tooltip);
                if (Widgets.ButtonInvisible(buttonRect))
                {
                    action();
                    return true;
                }

                return false;
            }

            float startX = rect.x + Margin;
            DrawIconButton(new Rect(startX + (btnWidth + Margin) * 0f, btnY, btnWidth, btnHeight), "+", "CS_Studio_Equip_Btn_New".Translate(), AddNewEquipment, true);
            DrawIconButton(new Rect(startX + (btnWidth + Margin) * 1f, btnY, btnWidth, btnHeight), "-", "CS_Studio_Btn_Delete".Translate(), DeleteSelectedEquipment);
            DrawIconButton(new Rect(startX + (btnWidth + Margin) * 2f, btnY, btnWidth, btnHeight), "C", "CS_Studio_Panel_Duplicate".Translate(), DuplicateSelectedEquipment);
            DrawIconButton(new Rect(startX + (btnWidth + Margin) * 3f, btnY, btnWidth, btnHeight), "A", "CS_Studio_Equip_Btn_Abilities".Translate(), () =>
            {
                SyncAbilitiesFromSkin();
                Find.WindowStack.Add(new Dialog_AbilityEditor(workingAbilities, workingSkin.abilityHotkeys, workingSkin));
            });
            DrawIconButton(new Rect(startX + (btnWidth + Margin) * 4f, btnY, btnWidth, btnHeight), "↓", "CS_Studio_Ability_ImportXml".Translate(), OpenEquipmentImportXmlDialog);
            DrawIconButton(new Rect(startX + (btnWidth + Margin) * 5f, btnY, btnWidth, btnHeight), "↑", "CS_Studio_Equip_Btn_ExportXml".Translate(), ExportSelectedEquipmentToDefaultPath);

            float listY = btnY + btnHeight + 8f;
            float listHeight = rect.height - listY + rect.y - Margin;
            Rect listRect = new Rect(rect.x + Margin, listY, rect.width - Margin * 2, listHeight);

            Widgets.DrawBoxSolid(listRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(listRect, 1);
            GUI.color = Color.white;

            float entriesHeight = workingSkin.equipments.Count * 38f;
            float headerHeight = 44f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(headerHeight + entriesHeight + 6f, listRect.height - 6f));

            Widgets.BeginScrollView(listRect.ContractedBy(2f), ref equipmentScrollPos, viewRect);

            float y = 2f;

            Rect headerRect = new Rect(0f, y, viewRect.width, 18f);
            Widgets.DrawBoxSolid(headerRect, new Color(UIHelper.AccentColor.r, UIHelper.AccentColor.g, UIHelper.AccentColor.b, 0.10f));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(6f, y, viewRect.width - 12f, 18f), "CS_Studio_Tab_Equipment".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            y += 20f;

            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(6f, y, viewRect.width - 12f, 20f), "CS_Studio_Equip_SelectHint".Translate());
            GUI.color = Color.white;
            y += 24f;

            if (workingSkin.equipments.Count == 0)
            {
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(6f, y, viewRect.width - 12f, 22f), "CS_Studio_Equip_NoSelection".Translate());
                GUI.color = Color.white;
            }
            else
            {
                for (int i = 0; i < workingSkin.equipments.Count; i++)
                {
                    DrawEquipmentListRow(i, ref y, viewRect.width);
                }
            }

            Text.Font = GameFont.Small;
            Widgets.EndScrollView();
        }

        private void DrawEquipmentListRow(int index, ref float y, float width)
        {
            if (index < 0 || index >= workingSkin.equipments.Count)
            {
                return;
            }

            var equipment = workingSkin.equipments[index];
            equipment ??= CreateDefaultEquipment(index);
            workingSkin.equipments[index] = equipment;
            equipment.EnsureDefaults();

            Rect rowRect = new Rect(0f, y, width, 34f);

            UIHelper.DrawAlternatingRowBackground(rowRect, index);

            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }

            if (index == selectedEquipmentIndex)
            {
                GUI.color = UIHelper.BorderColor;
                Widgets.DrawBox(rowRect, 1);
                GUI.color = Color.white;
            }

            GUI.color = equipment.enabled ? new Color(0.55f, 0.95f, 1f) : Color.gray;
            Widgets.Label(new Rect(4f, y + 2f, 18f, 18f), "◆");
            GUI.color = Color.white;

            Rect toggleRect = new Rect(24f, y + 7f, 18f, 18f);
            string toggleIcon = equipment.enabled ? "◉" : "◯";
            GUI.color = equipment.enabled ? Color.white : Color.gray;
            if (Widgets.ButtonText(toggleRect, toggleIcon, false))
            {
                CaptureUndoSnapshot();
                equipment.enabled = !equipment.enabled;
                isDirty = true;
                RefreshPreview();
                RefreshRenderTree();
            }

            Rect nameRect = new Rect(46f, y + 1f, width - 92f, 16f);
            Rect metaRect = new Rect(46f, y + 16f, width - 92f, 14f);

            Text.Font = GameFont.Small;
            GUI.color = equipment.enabled ? Color.white : Color.gray;
            Widgets.Label(nameRect, equipment.GetDisplayLabel());

            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            string meta = string.IsNullOrWhiteSpace(equipment.slotTag)
                ? (equipment.HasAbilityBindings() ? "CS_Studio_Equip_BoundAbilities".Translate() : "CS_Studio_Equip_NoAbilities".Translate())
                : $"{equipment.slotTag} · {(equipment.HasAbilityBindings() ? "CS_Studio_Equip_BoundAbilities".Translate() : "CS_Studio_Equip_NoAbilities".Translate())}";
            Widgets.Label(metaRect, meta);
            GUI.color = Color.white;

            Rect deleteRect = new Rect(width - 22f, y + 7f, 20f, 20f);
            if (Widgets.ButtonText(deleteRect, "×"))
            {
                SelectEquipment(index);
                DeleteSelectedEquipment();
                return;
            }

            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                SelectEquipment(index);
                Event.current.Use();
            }

            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                ShowEquipmentContextMenu(index);
                Event.current.Use();
            }

            Text.Font = GameFont.Small;
            y += rowRect.height;
        }

        private void ShowEquipmentContextMenu(int index)
        {
            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            if (index < 0 || index >= workingSkin.equipments.Count)
            {
                return;
            }

            var options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption("CS_Studio_Panel_Duplicate".Translate(), () =>
            {
                SelectEquipment(index);
                DuplicateSelectedEquipment();
            }));

            options.Add(new FloatMenuOption("CS_Studio_Equip_Btn_Abilities".Translate(), () =>
            {
                SelectEquipment(index);
                SyncAbilitiesFromSkin();
                Find.WindowStack.Add(new Dialog_AbilityEditor(workingAbilities, workingSkin.abilityHotkeys, workingSkin));
            }));

            options.Add(new FloatMenuOption("CS_Studio_Equip_Btn_ExportXml".Translate(), () =>
            {
                SelectEquipment(index);
                ExportSelectedEquipmentToDefaultPath();
            }));

            options.Add(new FloatMenuOption("CS_Studio_Btn_Delete".Translate(), () =>
            {
                SelectEquipment(index);
                DeleteSelectedEquipment();
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void SelectEquipment(int index)
        {
            if (index < 0 || index >= workingSkin.equipments.Count)
            {
                selectedEquipmentIndex = -1;
                return;
            }

            selectedEquipmentIndex = index;
            selectedLayerIndex = -1;
            selectedLayerIndices.Clear();
            selectedNodePath = string.Empty;
            selectedBaseSlotType = null;
            currentTab = EditorTab.Equipment;
        }

        private void SanitizeEquipmentSelection()
        {
            int count = workingSkin.equipments?.Count ?? 0;
            if (count <= 0)
            {
                selectedEquipmentIndex = -1;
                return;
            }

            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= count)
            {
                selectedEquipmentIndex = Mathf.Clamp(selectedEquipmentIndex, 0, count - 1);
            }
        }

        private void AddNewEquipment()
        {
            if (workingSkin == null)
            {
                Log.Error("[CharacterStudio] 新增装备失败：workingSkin 为空");
                ShowStatus("CS_Studio_Equip_InvalidSelection".Translate());
                return;
            }

            CaptureUndoSnapshot();
            workingSkin.equipments ??= new List<CharacterEquipmentDef>();

            var equipment = CreateDefaultEquipment(workingSkin.equipments.Count);
            workingSkin.equipments.Add(equipment);
            SelectEquipment(workingSkin.equipments.Count - 1);

            isDirty = true;
            RefreshPreview();
            RefreshRenderTree();
            ShowStatus("CS_Studio_Equip_Added".Translate(equipment.GetDisplayLabel()));
        }

        private void DuplicateSelectedEquipment()
        {
            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                ShowStatus("CS_Studio_Equip_NoSelection".Translate());
                return;
            }

            var sourceEquipment = workingSkin.equipments[selectedEquipmentIndex];
            if (sourceEquipment == null)
            {
                Log.Warning($"[CharacterStudio] 复制装备失败：选中索引 {selectedEquipmentIndex} 为空");
                ShowStatus("CS_Studio_Equip_InvalidSelection".Translate());
                return;
            }

            CaptureUndoSnapshot();

            var duplicate = sourceEquipment.Clone();
            duplicate.defName = BuildUniqueEquipmentDefName(
                string.IsNullOrWhiteSpace(duplicate.defName) ? "Equipment_Copy" : duplicate.defName + "_Copy",
                workingSkin.equipments);
            duplicate.label = string.IsNullOrWhiteSpace(duplicate.label) ? duplicate.defName : duplicate.label + " Copy";
            duplicate.EnsureDefaults();

            workingSkin.equipments.Insert(selectedEquipmentIndex + 1, duplicate);
            SelectEquipment(selectedEquipmentIndex + 1);

            isDirty = true;
            RefreshPreview();
            RefreshRenderTree();
            ShowStatus("CS_Studio_Equip_Duplicated".Translate(duplicate.GetDisplayLabel()));
        }

        private void DeleteSelectedEquipment()
        {
            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                ShowStatus("CS_Studio_Equip_NoSelection".Translate());
                return;
            }

            var selected = workingSkin.equipments[selectedEquipmentIndex];
            if (selected == null)
            {
                Log.Warning($"[CharacterStudio] 删除装备失败：选中索引 {selectedEquipmentIndex} 为空");
                ShowStatus("CS_Studio_Equip_InvalidSelection".Translate());
                return;
            }

            CaptureUndoSnapshot();

            string removedLabel = selected.GetDisplayLabel();
            workingSkin.equipments.RemoveAt(selectedEquipmentIndex);

            if (workingSkin.equipments.Count == 0)
            {
                selectedEquipmentIndex = -1;
            }
            else
            {
                selectedEquipmentIndex = Mathf.Clamp(selectedEquipmentIndex, 0, workingSkin.equipments.Count - 1);
            }

            isDirty = true;
            RefreshPreview();
            RefreshRenderTree();
            ShowStatus("CS_Studio_Equip_Deleted".Translate(removedLabel));
        }

        private CharacterEquipmentDef CreateDefaultEquipment(int index)
        {
            var equipment = new CharacterEquipmentDef
            {
                defName = BuildUniqueEquipmentDefName($"CS_Equipment_{index + 1}", workingSkin.equipments),
                label = $"Equipment {index + 1}",
                enabled = true,
                slotTag = "Apparel",
                visual = new PawnLayerConfig
                {
                    layerName = $"Equipment {index + 1}",
                    anchorTag = "Apparel",
                    shaderDefName = "Cutout",
                    colorSource = LayerColorSource.White,
                    colorTwoSource = LayerColorSource.White,
                    scale = Vector2.one,
                    visible = true,
                    drawOrder = 50f
                }
            };

            equipment.EnsureDefaults();
            return equipment;
        }

        private static string GetEquipmentExportDir()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Equipments");
        }

        private static string GetDefaultEquipmentExportFilePath()
        {
            return Path.Combine(GetEquipmentExportDir(), "EquipmentEditor_Export.xml");
        }

        private void OpenEquipmentImportXmlDialog()
        {
            string initialPath = !string.IsNullOrWhiteSpace(lastImportedEquipmentXmlPath)
                ? lastImportedEquipmentXmlPath
                : (!string.IsNullOrWhiteSpace(lastExportedEquipmentXmlPath) ? lastExportedEquipmentXmlPath : GetDefaultEquipmentExportFilePath());

            Find.WindowStack.Add(new Dialog_EquipmentXmlImport(initialPath, ImportEquipmentsFromXmlPath));
        }

        private void ExportSelectedEquipmentToDefaultPath()
        {
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                ShowStatus("CS_Studio_Equip_NoSelection".Translate());
                return;
            }

            try
            {
                string exportDir = GetEquipmentExportDir();
                Directory.CreateDirectory(exportDir);
                string exportPath = GetDefaultEquipmentExportFilePath();

                var selected = workingSkin.equipments[selectedEquipmentIndex]?.Clone();
                if (selected == null)
                {
                    ShowStatus("CS_Studio_Equip_InvalidSelection".Translate());
                    return;
                }

                selected.EnsureDefaults();
                CreateEquipmentsDocument(new List<CharacterEquipmentDef> { selected }).Save(exportPath);
                lastExportedEquipmentXmlPath = exportPath;
                ShowStatus("CS_Studio_Equip_Exported".Translate(exportPath));
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 装备 XML 导出失败: {ex}");
                ShowStatus("CS_Studio_Equip_ExportFailed".Translate(ex.Message));
            }
        }

        private static XDocument CreateEquipmentsDocument(List<CharacterEquipmentDef> equipmentList)
        {
            var defs = new XElement("Defs");
            var skinRoot = new XElement(nameof(PawnSkinDef));
            var equipmentsEl = ModExportXmlWriter.GenerateEquipmentsXml(equipmentList);
            if (equipmentsEl != null)
            {
                skinRoot.Add(equipmentsEl);
            }

            defs.Add(skinRoot);
            return new XDocument(defs);
        }

        private void ImportEquipmentsFromXmlPath(string xmlPath, bool replaceExisting)
        {
            try
            {
                if (workingSkin == null)
                {
                    Log.Error("[CharacterStudio] 装备 XML 导入失败：workingSkin 为空");
                    ShowStatus("CS_Studio_Equip_ImportFailed".Translate("workingSkin is null"));
                    return;
                }

                if (string.IsNullOrWhiteSpace(xmlPath))
                {
                    ShowStatus("CS_Studio_Msg_InvalidPath".Translate());
                    return;
                }

                string normalizedPath = xmlPath.Trim().Trim('"');
                if (!Path.IsPathRooted(normalizedPath))
                {
                    normalizedPath = Path.GetFullPath(normalizedPath);
                }

                if (!File.Exists(normalizedPath))
                {
                    ShowStatus("CS_Studio_Msg_InvalidPath".Translate() + $": {normalizedPath}");
                    return;
                }

                List<CharacterEquipmentDef> importedEquipments = LoadEquipmentsFromXmlFile(normalizedPath);
                if (importedEquipments.Count == 0)
                {
                    ShowStatus("CS_Studio_Equip_ImportNoResults".Translate());
                    return;
                }

                CaptureUndoSnapshot();

                workingSkin.equipments ??= new List<CharacterEquipmentDef>();
                if (replaceExisting)
                {
                    workingSkin.equipments.Clear();
                }

                NormalizeImportedEquipmentDefNames(importedEquipments, workingSkin.equipments);
                foreach (var imported in importedEquipments)
                {
                    if (imported == null)
                    {
                        continue;
                    }

                    imported.EnsureDefaults();
                    workingSkin.equipments.Add(imported.Clone());
                }

                lastImportedEquipmentXmlPath = normalizedPath;

                if (workingSkin.equipments.Count > 0)
                {
                    SelectEquipment(replaceExisting ? 0 : workingSkin.equipments.Count - importedEquipments.Count);
                }
                else
                {
                    selectedEquipmentIndex = -1;
                }

                isDirty = true;
                RefreshPreview();
                RefreshRenderTree();

                string sourceLabel = Path.GetFileName(normalizedPath);
                ShowStatus(replaceExisting
                    ? "CS_Studio_Equip_ImportReplaced".Translate(importedEquipments.Count, sourceLabel)
                    : "CS_Studio_Equip_ImportAppended".Translate(importedEquipments.Count, sourceLabel));
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 装备 XML 导入失败: {ex}");
                ShowStatus("CS_Studio_Equip_ImportFailed".Translate(ex.Message));
            }
        }

        private static List<CharacterEquipmentDef> LoadEquipmentsFromXmlFile(string path)
        {
            var xml = new XmlDocument();
            xml.Load(path);

            var result = new List<CharacterEquipmentDef>();
            XmlNode? root = xml.DocumentElement;
            if (root == null)
            {
                return result;
            }

            CollectEquipmentsFromNode(root, result);
            return result;
        }

        private static void CollectEquipmentsFromNode(XmlNode node, List<CharacterEquipmentDef> result)
        {
            if (node.NodeType != XmlNodeType.Element)
            {
                return;
            }

            if (string.Equals(node.Name, "Defs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    CollectEquipmentsFromNode(child, result);
                }

                return;
            }

            if (IsPawnSkinDefNode(node.Name))
            {
                XmlNode? equipmentsNode = FindEquipmentChildNode(node, "equipments");
                if (equipmentsNode != null)
                {
                    foreach (XmlNode child in equipmentsNode.ChildNodes)
                    {
                        if (child.NodeType != XmlNodeType.Element)
                        {
                            continue;
                        }

                        if (string.Equals(child.Name, "li", StringComparison.OrdinalIgnoreCase) || IsCharacterEquipmentNode(child.Name))
                        {
                            CharacterEquipmentDef? equipment = ParseCharacterEquipmentNode(child);
                            if (equipment != null)
                            {
                                result.Add(equipment);
                            }
                        }
                    }
                }

                return;
            }

            if (string.Equals(node.Name, "equipments", StringComparison.OrdinalIgnoreCase))
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.NodeType != XmlNodeType.Element)
                    {
                        continue;
                    }

                    if (string.Equals(child.Name, "li", StringComparison.OrdinalIgnoreCase) || IsCharacterEquipmentNode(child.Name))
                    {
                        CharacterEquipmentDef? equipment = ParseCharacterEquipmentNode(child);
                        if (equipment != null)
                        {
                            result.Add(equipment);
                        }
                    }
                }

                return;
            }

            if (IsCharacterEquipmentNode(node.Name))
            {
                CharacterEquipmentDef? equipment = ParseCharacterEquipmentNode(node);
                if (equipment != null)
                {
                    result.Add(equipment);
                }
            }
        }

        private static CharacterEquipmentDef? ParseCharacterEquipmentNode(XmlNode node)
        {
            try
            {
                var imported = DirectXmlToObject.ObjectFromXml<CharacterEquipmentDef>(node, true);
                if (imported == null)
                {
                    return null;
                }

                imported.defName = string.IsNullOrWhiteSpace(imported.defName)
                    ? $"CS_ImportedEquipment_{Guid.NewGuid():N}"
                    : imported.defName;
                imported.label = string.IsNullOrWhiteSpace(imported.label) ? imported.defName : imported.label;
                imported.EnsureDefaults();
                return imported;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 解析装备 XML 失败: {ex.Message}");
                return null;
            }
        }

        private static void NormalizeImportedEquipmentDefNames(List<CharacterEquipmentDef> importedEquipments, List<CharacterEquipmentDef>? existingEquipments)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (existingEquipments != null)
            {
                foreach (var equipment in existingEquipments)
                {
                    if (equipment != null && !string.IsNullOrWhiteSpace(equipment.defName))
                    {
                        used.Add(equipment.defName);
                    }
                }
            }

            foreach (var equipment in importedEquipments)
            {
                if (equipment == null)
                {
                    continue;
                }

                string baseName = string.IsNullOrWhiteSpace(equipment.defName)
                    ? $"CS_ImportedEquipment_{Guid.NewGuid():N}"
                    : equipment.defName.Trim();

                string finalName = baseName;
                int suffix = 1;
                while (!used.Add(finalName))
                {
                    finalName = $"{baseName}_{suffix++}";
                }

                equipment.defName = finalName;
                if (string.IsNullOrWhiteSpace(equipment.label))
                {
                    equipment.label = finalName;
                }

                equipment.EnsureDefaults();
            }
        }

        private string BuildUniqueEquipmentDefName(string desiredName, IEnumerable<CharacterEquipmentDef>? existingEquipments)
        {
            string baseName = string.IsNullOrWhiteSpace(desiredName) ? "CS_Equipment" : desiredName.Trim();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (existingEquipments != null)
            {
                foreach (var equipment in existingEquipments)
                {
                    if (equipment != null && !string.IsNullOrWhiteSpace(equipment.defName))
                    {
                        used.Add(equipment.defName);
                    }
                }
            }

            string finalName = baseName;
            int suffix = 1;
            while (used.Contains(finalName))
            {
                finalName = $"{baseName}_{suffix++}";
            }

            return finalName;
        }

        private static bool IsPawnSkinDefNode(string nodeName)
        {
            return string.Equals(nodeName, nameof(PawnSkinDef), StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeName, typeof(PawnSkinDef).FullName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCharacterEquipmentNode(string nodeName)
        {
            return string.Equals(nodeName, nameof(CharacterEquipmentDef), StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeName, typeof(CharacterEquipmentDef).FullName, StringComparison.OrdinalIgnoreCase);
        }

        private static XmlNode? FindEquipmentChildNode(XmlNode? parent, string childName)
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
    }

    internal class Dialog_EquipmentXmlImport : Window
    {
        private string xmlPath;
        private readonly Action<string, bool> onImport;

        public override Vector2 InitialSize => new Vector2(720f, 220f);

        public Dialog_EquipmentXmlImport(string initialPath, Action<string, bool> onImport)
        {
            xmlPath = initialPath ?? string.Empty;
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
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "CS_Studio_Equip_ImportXmlTitle".Translate());
            Text.Font = GameFont.Small;

            Widgets.Label(new Rect(0f, 38f, inRect.width, 44f), "CS_Studio_Equip_ImportXmlHint".Translate());

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