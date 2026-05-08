using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using CharacterStudio.Core;
using CharacterStudio.Exporter;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private static string lastImportedEquipmentXmlPath = string.Empty;
        private static string lastExportedEquipmentXmlPath = string.Empty;
        private static readonly Dictionary<string, ThingDef> runtimeTestEquipmentDefs = new Dictionary<string, ThingDef>(StringComparer.OrdinalIgnoreCase);

        private void DrawEquipmentPanel(Rect rect)
        {
            WorkingEquipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();

            string panelTitle = currentTab == EditorTab.Items ? "CS_Studio_Tab_Items".Translate() : "CS_Studio_Tab_Equipment".Translate();
            Rect titleRect = UIHelper.DrawPanelShell(rect, panelTitle, Margin);

            float btnY = titleRect.yMax + 6f;
            const float cols = 3f;
            float btnWidth = (rect.width - Margin * (cols + 1f)) / cols;
            float btnHeight = Mathf.Max(ButtonHeight, 24f);

            float startX = rect.x + Margin;
            // 行 0：新建 / 删除 / 复制
            UIHelper.DrawIconButton(new Rect(startX + (btnWidth + Margin) * 0f, btnY, btnWidth, btnHeight), "CS_Studio_Equip_Btn_New".Translate(), "CS_Studio_Equip_Btn_New".Translate(), AddNewEquipment, true);
            UIHelper.DrawIconButton(new Rect(startX + (btnWidth + Margin) * 1f, btnY, btnWidth, btnHeight), "CS_Studio_Btn_Delete".Translate(), "CS_Studio_Btn_Delete".Translate(), DeleteSelectedEquipment);
            UIHelper.DrawIconButton(new Rect(startX + (btnWidth + Margin) * 2f, btnY, btnWidth, btnHeight), "CS_Studio_Panel_Duplicate".Translate(), "CS_Studio_Panel_Duplicate".Translate(), DuplicateSelectedEquipment);
            // 行 1：测试生成 / 导入 / 导出
            float row1Y = btnY + btnHeight + 2f;
            UIHelper.DrawIconButton(new Rect(startX + (btnWidth + Margin) * 0f, row1Y, btnWidth, btnHeight), "CS_Studio_Equip_Btn_TestSpawn".Translate(), "CS_Studio_Equip_Btn_TestSpawn".Translate(), SpawnSelectedEquipmentForTest, true);
            UIHelper.DrawIconButton(new Rect(startX + (btnWidth + Margin) * 1f, row1Y, btnWidth, btnHeight), "导入", "CS_Studio_Equip_ImportXmlTitle".Translate(), OpenEquipmentImportXmlDialog);
            UIHelper.DrawIconButton(new Rect(startX + (btnWidth + Margin) * 2f, row1Y, btnWidth, btnHeight), "导出", "CS_Studio_Equip_Btn_ExportXml".Translate(), ExportSelectedEquipmentToDefaultPath);

            float listY = row1Y + btnHeight + 8f;
            float listHeight = rect.height - listY + rect.y - Margin;
            Rect listRect = new Rect(rect.x + Margin, listY, rect.width - Margin * 2, listHeight);

            Widgets.DrawBoxSolid(listRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(listRect, 1);
            GUI.color = Color.white;

            float entriesHeight = WorkingEquipments.Count * 38f;
            float headerHeight = 44f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(headerHeight + entriesHeight + 6f, listRect.height - 6f));

            Widgets.BeginScrollView(listRect.ContractedBy(2f), ref equipmentScrollPos, viewRect);

            float y = 2f;

            Rect headerRect = new Rect(0f, y, viewRect.width, 18f);
            Widgets.DrawBoxSolid(headerRect, new Color(UIHelper.AccentColor.r, UIHelper.AccentColor.g, UIHelper.AccentColor.b, 0.10f));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            string listHeader = currentTab == EditorTab.Items ? "CS_Studio_Tab_Items".Translate() : "CS_Studio_Tab_Equipment".Translate();
            Widgets.Label(new Rect(6f, y, viewRect.width - 12f, 18f), listHeader);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            y += 20f;

            if (WorkingEquipments.Count > 0)
            {
                for (int i = 0; i < WorkingEquipments.Count; i++)
                {
                    DrawEquipmentListRow(i, ref y, viewRect.width);
                }
            }

            Text.Font = GameFont.Small;
            Widgets.EndScrollView();
        }

        private void DrawEquipmentListRow(int index, ref float y, float width)
        {
            if (index < 0 || index >= WorkingEquipments.Count)
            {
                return;
            }

            var equipment = WorkingEquipments[index];
            equipment ??= CreateDefaultEquipment(index);
            WorkingEquipments[index] = equipment;
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
                bool newEnabled = !equipment.enabled;
                MutateWithUndo(() => equipment.enabled = newEnabled, refreshPreview: true, refreshRenderTree: true);
            }

            Rect deleteRect = new Rect(width - 28f, y + 5f, 24f, 22f);
            Rect nameRect = new Rect(46f, y + 1f, width - 100f, 16f);
            Rect metaRect = new Rect(46f, y + 16f, width - 100f, 14f);

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

            if (UIHelper.DrawDangerButton(deleteRect, tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
            {
                SelectEquipment(index);
                DeleteSelectedEquipment();
            }))
            {
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
            WorkingEquipments ??= new List<CharacterEquipmentDef>();
            if (index < 0 || index >= WorkingEquipments.Count)
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
                OpenAbilityEditor();
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
            if (index < 0 || index >= WorkingEquipments.Count)
            {
                selectedEquipmentIndex = -1;
                return;
            }

            selectedEquipmentIndex = index;
            selectedLayerIndex = -1;
            selectedLayerIndices?.Clear();
            selectedNodePath = string.Empty;
            selectedBaseSlotType = null;
            equipmentPivotEditMode = false;
            isDraggingEquipmentPivot = false;
        }

        private void SanitizeEquipmentSelection()
        {
            int count = WorkingEquipments.Count;
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

            WorkingEquipments ??= new List<CharacterEquipmentDef>();

            var equipment = CreateDefaultEquipment(WorkingEquipments.Count);
            MutateWithUndo(() =>
            {
                WorkingEquipments.Add(equipment);
                SelectEquipment(WorkingEquipments.Count - 1);
            }, refreshPreview: true, refreshRenderTree: true);
            ShowStatus("CS_Studio_Equip_Added".Translate(equipment.GetDisplayLabel()));
        }

        private void DuplicateSelectedEquipment()
        {
            WorkingEquipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= WorkingEquipments.Count)
            {
                ShowStatus("CS_Studio_Equip_NoSelection".Translate());
                return;
            }

            var sourceEquipment = WorkingEquipments[selectedEquipmentIndex];
            if (sourceEquipment == null)
            {
                Log.Warning($"[CharacterStudio] 复制装备失败：选中索引 {selectedEquipmentIndex} 为空");
                ShowStatus("CS_Studio_Equip_InvalidSelection".Translate());
                return;
            }

            var duplicate = sourceEquipment.Clone();
            duplicate.defName = BuildUniqueEquipmentDefName(
                string.IsNullOrWhiteSpace(duplicate.defName) ? "CS_Studio_Equip_DefaultCopyDefName".Translate().ToString() : duplicate.defName + "_Copy",
                WorkingEquipments);
            duplicate.label = string.IsNullOrWhiteSpace(duplicate.label)
                ? duplicate.defName
                : "CS_Studio_Equip_Label_Copy".Translate(duplicate.label).ToString();
            duplicate.EnsureDefaults();

            MutateWithUndo(() =>
            {
                WorkingEquipments.Insert(selectedEquipmentIndex + 1, duplicate);
                SelectEquipment(selectedEquipmentIndex + 1);
            }, refreshPreview: true, refreshRenderTree: true);
            ShowStatus("CS_Studio_Equip_Duplicated".Translate(duplicate.GetDisplayLabel()));
        }

        private void DeleteSelectedEquipment()
        {
            WorkingEquipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= WorkingEquipments.Count)
            {
                ShowStatus("CS_Studio_Equip_NoSelection".Translate());
                return;
            }

            var selected = WorkingEquipments[selectedEquipmentIndex];
            if (selected == null)
            {
                Log.Warning($"[CharacterStudio] 删除装备失败：选中索引 {selectedEquipmentIndex} 为空");
                ShowStatus("CS_Studio_Equip_InvalidSelection".Translate());
                return;
            }

            string removedLabel = selected.GetDisplayLabel();
            MutateWithUndo(() =>
            {
                WorkingEquipments.RemoveAt(selectedEquipmentIndex);

                if (WorkingEquipments.Count == 0)
                {
                    selectedEquipmentIndex = -1;
                }
                else
                {
                    selectedEquipmentIndex = Mathf.Clamp(selectedEquipmentIndex, 0, WorkingEquipments.Count - 1);
                }
            }, refreshPreview: true, refreshRenderTree: true);
            ShowStatus("CS_Studio_Equip_Deleted".Translate(removedLabel));
        }

        private void SpawnSelectedEquipmentForTest()
        {
            WorkingEquipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= WorkingEquipments.Count)
            {
                ShowStatus("CS_Studio_Equip_NoSelection".Translate());
                return;
            }

            CharacterEquipmentDef? selected = WorkingEquipments[selectedEquipmentIndex]?.Clone();
            if (selected == null)
            {
                ShowStatus("CS_Studio_Equip_InvalidSelection".Translate());
                return;
            }

            selected.EnsureDefaults();
            Map? map = targetPawn?.Map ?? Find.CurrentMap;
            if (map == null)
            {
                ShowStatus("CS_Studio_Equip_TestSpawnFailed".Translate("Map unavailable"));
                return;
            }

            ThingDef? thingDef = ResolveEquipmentThingDefForTest(selected);
            if (thingDef == null)
            {
                ShowStatus("CS_Studio_Equip_TestSpawnFailed".Translate("ThingDef unavailable"));
                return;
            }

            if (Find.Targeter == null)
            {
                Log.Warning("[CharacterStudio] Targeter 不可用，无法启动选格模式");
                ShowStatus("CS_Studio_Equip_TestSpawnFailed".Translate("Targeter unavailable"));
                return;
            }

            string equipLabel = selected.GetDisplayLabel();
            try
            {
                var parms = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    canTargetItems = false,
                    validator = target =>
                    {
                        IntVec3 cell = target.Cell;
                        return cell.InBounds(map) && cell.Standable(map);
                    }
                };

                Find.Targeter.BeginTargeting(
                    parms,
                    target =>
                    {
                        try
                        {
                            IntVec3 spawnCell = target.Cell;
                            if (!spawnCell.InBounds(map) || !spawnCell.Standable(map))
                            {
                                ShowStatus("CS_Studio_Equip_TestSpawnFailed".Translate("Cell not standable"));
                                return;
                            }

                            Thing thing = ThingMaker.MakeThing(thingDef);
                            GenSpawn.Spawn(thing, spawnCell, map, WipeMode.Vanish);
                            ShowStatus("CS_Studio_Equip_TestSpawned".Translate(equipLabel));
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[CharacterStudio] 地图测试生成装备失败: {ex}");
                            ShowStatus("CS_Studio_Equip_TestSpawnFailed".Translate(ex.Message));
                        }
                    },
                    targetPawn,
                    null,
                    null,
                    true);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 启动选格模式失败: {ex}");
                ShowStatus("CS_Studio_Equip_TestSpawnFailed".Translate(ex.Message));
            }
        }

        private static void CopyVerbs(ThingDef source, ThingDef target)
        {
            var verbsField = typeof(ThingDef).GetField("verbs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (verbsField != null)
            {
                var sourceVerbs = verbsField.GetValue(source) as List<VerbProperties>;
                if (sourceVerbs != null)
                {
                    verbsField.SetValue(target, new List<VerbProperties>(sourceVerbs));
                }
            }
        }

        private static ThingDef? ResolveEquipmentThingDefForTest(CharacterEquipmentDef equipment)
        {
            string resolvedThingDefName = equipment.GetResolvedThingDefName();
            if (string.IsNullOrWhiteSpace(resolvedThingDefName))
            {
                return null;
            }

            ThingDef? runtimeDef = CreateRuntimeTestEquipmentThingDef(equipment, resolvedThingDefName);
            if (runtimeDef == null)
            {
                return null;
            }

            ThingDef? existing = DefDatabase<ThingDef>.GetNamedSilentFail(resolvedThingDefName);
            if (existing != null)
            {
                existing.label = runtimeDef.label;
                existing.description = runtimeDef.description;
                existing.statBases = runtimeDef.statBases;
                existing.equippedStatOffsets = runtimeDef.equippedStatOffsets;
                existing.apparel = runtimeDef.apparel;
                existing.tools = runtimeDef.tools;
                existing.equipmentType = runtimeDef.equipmentType;
                existing.weaponTags = runtimeDef.weaponTags;
                existing.weaponClasses = runtimeDef.weaponClasses;
                existing.comps = runtimeDef.comps;
                existing.graphicData = runtimeDef.graphicData;
                existing.uiIcon = runtimeDef.uiIcon;
                existing.thingCategories = runtimeDef.thingCategories;
                existing.building = runtimeDef.building;
                existing.size = runtimeDef.size;
                existing.passability = runtimeDef.passability;
                existing.pathCost = runtimeDef.pathCost;
                existing.fillPercent = runtimeDef.fillPercent;
                CopyVerbs(runtimeDef, existing);
                return existing;
            }

            if (runtimeTestEquipmentDefs.TryGetValue(resolvedThingDefName, out ThingDef cached))
            {
                cached.label = runtimeDef.label;
                cached.description = runtimeDef.description;
                cached.statBases = runtimeDef.statBases;
                cached.equippedStatOffsets = runtimeDef.equippedStatOffsets;
                cached.apparel = runtimeDef.apparel;
                cached.tools = runtimeDef.tools;
                cached.equipmentType = runtimeDef.equipmentType;
                cached.weaponTags = runtimeDef.weaponTags;
                cached.weaponClasses = runtimeDef.weaponClasses;
                cached.comps = runtimeDef.comps;
                cached.graphicData = runtimeDef.graphicData;
                cached.uiIcon = runtimeDef.uiIcon;
                cached.thingCategories = runtimeDef.thingCategories;
                cached.building = runtimeDef.building;
                cached.size = runtimeDef.size;
                cached.passability = runtimeDef.passability;
                cached.pathCost = runtimeDef.pathCost;
                cached.fillPercent = runtimeDef.fillPercent;
                CopyVerbs(runtimeDef, cached);
                return cached;
            }

            DefDatabase<ThingDef>.Add(runtimeDef);
            runtimeTestEquipmentDefs[resolvedThingDefName] = runtimeDef;
            return runtimeDef;
        }

        private static ThingDef? CreateRuntimeTestEquipmentThingDef(CharacterEquipmentDef equipment, string resolvedThingDefName)
        {
            string texPath = string.IsNullOrWhiteSpace(equipment.worldTexPath)
                ? (equipment.renderData?.GetResolvedTexPath() ?? string.Empty)
                : equipment.worldTexPath;
            if (string.IsNullOrWhiteSpace(texPath))
            {
                return null;
            }

            ThingDef? parentDef = DefDatabase<ThingDef>.GetNamedSilentFail(equipment.parentThingDefName);
            ShaderTypeDef shader = DefDatabase<ShaderTypeDef>.GetNamedSilentFail(equipment.shaderDefName) ?? ShaderTypeDefOf.Cutout;
            bool usesExternalTexture = CharacterStudio.Rendering.RuntimeAssetLoader.LooksLikeExternalTexturePath(texPath);
            Texture2D? externalUiIcon = null;

            if (usesExternalTexture)
            {
                externalUiIcon = CharacterStudio.Rendering.RuntimeAssetLoader.LoadTextureRaw(texPath);
                if (externalUiIcon == null)
                {
                    Log.Warning($"[CharacterStudio] 测试生成装备的外部图标加载失败，将继续使用默认图形路径: {texPath}");
                }
            }

            ThingDef runtimeDef = new ThingDef
            {
                defName = resolvedThingDefName,
                label = equipment.GetDisplayLabel(),
                description = equipment.description ?? string.Empty,
                thingClass = parentDef?.thingClass ?? typeof(Apparel),
                category = parentDef?.category ?? ThingCategory.Item,
                altitudeLayer = parentDef?.altitudeLayer ?? AltitudeLayer.Item,
                drawerType = parentDef?.drawerType ?? DrawerType.MapMeshOnly,
                useHitPoints = parentDef?.useHitPoints ?? true,
                selectable = true,
                drawGUIOverlay = true,
                rotatable = false,
                statBases = parentDef?.statBases != null ? new List<StatModifier>(parentDef.statBases) : new List<StatModifier>(),
                equippedStatOffsets = parentDef?.equippedStatOffsets != null ? new List<StatModifier>(parentDef.equippedStatOffsets) : new List<StatModifier>(),
                modExtensions = new List<DefModExtension>()
            };

            runtimeDef.graphicData = new GraphicData
            {
                texPath = texPath,
                graphicClass = usesExternalTexture ? typeof(CharacterStudio.Rendering.Graphic_Runtime) : typeof(Graphic_Single),
                shaderType = shader,
                drawSize = Vector2.one
            };

            if (externalUiIcon != null)
            {
                runtimeDef.uiIcon = externalUiIcon;
                runtimeDef.uiIconPath = string.Empty;
            }

            runtimeDef.thingCategories = equipment.thingCategories?
                .Select(name => DefDatabase<ThingCategoryDef>.GetNamedSilentFail(name))
                .Where(def => def != null)
                .Cast<ThingCategoryDef>()
                .ToList();

            if (equipment.itemType == CharacterStudio.Core.EquipmentType.Apparel)
            {
                runtimeDef.apparel = new ApparelProperties
                {
                    wornGraphicPath = string.IsNullOrWhiteSpace(equipment.wornTexPath) ? texPath : equipment.wornTexPath,
                    useWornGraphicMask = equipment.useWornGraphicMask,
                    bodyPartGroups = equipment.bodyPartGroups?
                        .Select(name => DefDatabase<BodyPartGroupDef>.GetNamedSilentFail(name))
                        .Where(def => def != null)
                        .Cast<BodyPartGroupDef>()
                        .ToList() ?? new List<BodyPartGroupDef>(),
                    layers = equipment.apparelLayers?
                        .Select(name => DefDatabase<ApparelLayerDef>.GetNamedSilentFail(name))
                        .Where(def => def != null)
                        .Cast<ApparelLayerDef>()
                        .ToList() ?? new List<ApparelLayerDef>(),
                    tags = equipment.apparelTags != null ? new List<string>(equipment.apparelTags) : new List<string>()
                };
            }
            else if (equipment.itemType == CharacterStudio.Core.EquipmentType.WeaponMelee || equipment.itemType == CharacterStudio.Core.EquipmentType.WeaponRanged)
            {
                if (parentDef != null)
                {
                    runtimeDef.equipmentType = parentDef.equipmentType;
                    var parentVerbsField = typeof(ThingDef).GetField("verbs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (parentVerbsField != null)
                    {
                        var parentVerbs = parentVerbsField.GetValue(parentDef) as List<VerbProperties>;
                        if (parentVerbs != null)
                        {
                            var verbsField = typeof(ThingDef).GetField("verbs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            verbsField?.SetValue(runtimeDef, new List<VerbProperties>(parentVerbs));
                        }
                    }
                    if (parentDef.tools != null)
                    {
                        runtimeDef.tools = new List<Tool>(parentDef.tools);
                    }
                    if (parentDef.comps != null)
                    {
                        runtimeDef.comps = new List<CompProperties>(parentDef.comps);
                    }
                }
                
                runtimeDef.weaponTags = equipment.weaponTags != null ? new List<string>(equipment.weaponTags) : new List<string>();
                runtimeDef.weaponClasses = equipment.weaponClasses?
                    .Select(name => DefDatabase<WeaponClassDef>.GetNamedSilentFail(name))
                    .Where(def => def != null)
                    .Cast<WeaponClassDef>()
                    .ToList() ?? new List<WeaponClassDef>();
            }
            else if (equipment.itemType == CharacterStudio.Core.EquipmentType.Building
                || equipment.itemType == CharacterStudio.Core.EquipmentType.Turret)
            {
                // 建筑/炮塔类型
                runtimeDef.category = ThingCategory.Building;
                runtimeDef.thingClass = equipment.itemType == CharacterStudio.Core.EquipmentType.Turret
                    ? typeof(Building_TurretGun)
                    : (parentDef?.thingClass ?? typeof(Building));
                runtimeDef.altitudeLayer = AltitudeLayer.Building;

                if (!string.IsNullOrWhiteSpace(equipment.drawerType))
                {
                    if (Enum.TryParse<DrawerType>(equipment.drawerType, out var dt))
                        runtimeDef.drawerType = dt;
                    else
                        runtimeDef.drawerType = DrawerType.MapMeshAndRealTime;
                }
                else
                {
                    runtimeDef.drawerType = DrawerType.MapMeshAndRealTime;
                }

                runtimeDef.selectable = true;
                runtimeDef.rotatable = false;

                // size
                if (!string.IsNullOrWhiteSpace(equipment.buildingSize))
                {
                    string[] sizeParts = equipment.buildingSize.Split(',');
                    if (sizeParts.Length == 2 && int.TryParse(sizeParts[0].Trim(), out int sx) && int.TryParse(sizeParts[1].Trim(), out int sy))
                    {
                        runtimeDef.size = new IntVec2(sx, sy);
                    }
                }

                // passability
                if (!string.IsNullOrWhiteSpace(equipment.passability))
                {
                    if (Enum.TryParse<Traversability>(equipment.passability, out var pass))
                        runtimeDef.passability = pass;
                }

                // fillPercent
                if (equipment.fillPercent > 0f)
                    runtimeDef.fillPercent = equipment.fillPercent;

                // pathCost
                if (equipment.pathCost > 0)
                    runtimeDef.pathCost = equipment.pathCost;

                // terrainAffordanceNeeded
                if (!string.IsNullOrWhiteSpace(equipment.terrainAffordanceNeeded))
                {
                    var terrainAfford = DefDatabase<TerrainAffordanceDef>.GetNamedSilentFail(equipment.terrainAffordanceNeeded);
                    if (terrainAfford != null)
                    {
                        var terrainAffordField = typeof(ThingDef).GetField("terrainAffordanceNeeded",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        terrainAffordField?.SetValue(runtimeDef, terrainAfford);
                    }
                }

                // building properties
                var buildingProps = new BuildingProperties
                {
                    buildingTags = equipment.buildingTags != null ? new List<string>(equipment.buildingTags) : new List<string>(),
                    isEdifice = true
                };

                if (equipment.combatPower > 0f)
                    buildingProps.combatPower = equipment.combatPower;

                if (equipment.roofCollapseDamageMultiplier > 0f)
                    buildingProps.roofCollapseDamageMultiplier = equipment.roofCollapseDamageMultiplier;

                if (!string.IsNullOrWhiteSpace(equipment.destroySound))
                {
                    var soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(equipment.destroySound);
                    if (soundDef != null)
                        buildingProps.destroySound = soundDef;
                }

                // 炮塔专有字段
                if (equipment.itemType == CharacterStudio.Core.EquipmentType.Turret)
                {
                    if (!string.IsNullOrWhiteSpace(equipment.turretGunDef))
                    {
                        ThingDef? gunDef = DefDatabase<ThingDef>.GetNamedSilentFail(equipment.turretGunDef);
                        if (gunDef != null)
                            buildingProps.turretGunDef = gunDef;
                    }

                    if (equipment.turretBurstWarmupTime > 0f)
                        buildingProps.turretBurstWarmupTime = new Verse.FloatRange(equipment.turretBurstWarmupTime, equipment.turretBurstWarmupTime);
                    if (equipment.turretBurstCooldownTime > 0f)
                        buildingProps.turretBurstCooldownTime = equipment.turretBurstCooldownTime;
                    if (equipment.turretInitialCooldownTime > 0f)
                        buildingProps.turretInitialCooldownTime = equipment.turretInitialCooldownTime;

                    if (equipment.isMechClusterThreat)
                        runtimeDef.isMechClusterThreat = true;
                }

                runtimeDef.building = buildingProps;

                // killedLeavings
                if (equipment.killedLeavings != null && equipment.killedLeavings.Count > 0)
                {
                    var leavings = new List<ThingDefCountClass>();
                    foreach (var entry in equipment.killedLeavings)
                    {
                        ThingDef? thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(entry.thingDefName);
                        if (thingDef != null)
                            leavings.Add(new ThingDefCountClass(thingDef, entry.count));
                    }
                    runtimeDef.killedLeavings = leavings;
                }

                // damageMultipliers
                if (equipment.damageMultipliers != null && equipment.damageMultipliers.Count > 0)
                {
                    runtimeDef.damageMultipliers = new List<DamageMultiplier>(equipment.damageMultipliers.Count);
                    foreach (var dm in equipment.damageMultipliers)
                    {
                        if (dm == null || string.IsNullOrWhiteSpace(dm.damageDefName)) continue;
                        DamageDef? damageDef = DefDatabase<DamageDef>.GetNamedSilentFail(dm.damageDefName);
                        if (damageDef != null)
                            runtimeDef.damageMultipliers.Add(new DamageMultiplier { damageDef = damageDef, multiplier = dm.multiplier });
                    }
                }

                // graphicData drawSize from buildingSize
                if (!string.IsNullOrWhiteSpace(equipment.graphicDrawSize))
                {
                    string[] drawParts = equipment.graphicDrawSize.Split(',');
                    if (drawParts.Length == 2 && float.TryParse(drawParts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dx)
                        && float.TryParse(drawParts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dy))
                    {
                        runtimeDef.graphicData.drawSize = new Vector2(dx, dy);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(equipment.buildingSize))
                {
                    string[] sizeParts = equipment.buildingSize.Split(',');
                    if (sizeParts.Length == 2 && float.TryParse(sizeParts[0].Trim(), out float bx) && float.TryParse(sizeParts[1].Trim(), out float by))
                    {
                        runtimeDef.graphicData.drawSize = new Vector2(bx, by);
                    }
                }

                // comps from parentDef (for buildings)
                if (parentDef?.comps != null)
                {
                    runtimeDef.comps = new List<CompProperties>(parentDef.comps);
                }
            }

            foreach (CharacterEquipmentStatEntry entry in equipment.statBases ?? new List<CharacterEquipmentStatEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.statDefName))
                {
                    continue;
                }

                StatDef? statDef = DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName);
                if (statDef != null)
                {
                    runtimeDef.statBases.Add(new StatModifier { stat = statDef, value = entry.value });
                }
            }

            foreach (CharacterEquipmentStatEntry entry in equipment.equippedStatOffsets ?? new List<CharacterEquipmentStatEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.statDefName))
                {
                    continue;
                }

                StatDef? statDef = DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName);
                if (statDef != null)
                {
                    runtimeDef.equippedStatOffsets.Add(new StatModifier { stat = statDef, value = entry.value });
                }
            }

            DefModExtension_EquipmentRender renderExtension = DefModExtension_EquipmentRender.FromEquipment(equipment);
            renderExtension.EnsureDefaults();
            runtimeDef.modExtensions.Add(renderExtension);
            runtimeDef.ResolveReferences();
            runtimeDef.PostLoad();
            return runtimeDef;
        }

        private CharacterEquipmentDef CreateDefaultEquipment(int index)
        {
            string defaultEquipmentLabel = "CS_Studio_Equip_DefaultLabel".Translate(index + 1).ToString();

            var equipment = new CharacterEquipmentDef
            {
                defName = BuildUniqueEquipmentDefName($"CS_Equipment_{index + 1}", WorkingEquipments),
                label = defaultEquipmentLabel,
                enabled = true,
                slotTag = CharacterEquipmentDef.DefaultSlotTag,
                renderData = new CharacterEquipmentRenderData
                {
                    layerName = defaultEquipmentLabel,
                    anchorTag = CharacterEquipmentDef.DefaultAnchorTag,
                    shaderDefName = CharacterEquipmentDef.DefaultShaderDefName,
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

        private void AddAircraftWingAnimationPreset()
        {
            if (workingSkin == null)
            {
                Log.Error("[CharacterStudio] 新增飞行翼预设失败：workingSkin 为空");
                ShowStatus("CS_Studio_Equip_InvalidSelection".Translate());
                return;
            }

            WorkingEquipments ??= new List<CharacterEquipmentDef>();

            var usedDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var existingEquipment in WorkingEquipments)
            {
                if (existingEquipment != null && !string.IsNullOrWhiteSpace(existingEquipment.defName))
                {
                    usedDefNames.Add(existingEquipment.defName);
                }
            }

            var presets = new List<CharacterEquipmentDef>
            {
                CreateAircraftPresetEquipment(
                    usedDefNames,
                    "Aircraft_Wing_L",
                    "AircraftWingLeft",
                    "CS_Studio_Equip_Preset_LeftWing".Translate().ToString(),
                    EquipmentTriggeredAnimationRole.MovablePart,
                    60f,
                    new Vector2(0.12f, 0f),
                    58f,
                    useVfxVisibility: false,
                    visibleDuringDeploy: true,
                    visibleDuringHold: true,
                    visibleDuringReturn: true,
                    visibleOutsideCycle: true),
                CreateAircraftPresetEquipment(
                    usedDefNames,
                    "Aircraft_Wing_R",
                    "AircraftWingRight",
                    "CS_Studio_Equip_Preset_RightWing".Translate().ToString(),
                    EquipmentTriggeredAnimationRole.MovablePart,
                    -60f,
                    new Vector2(-0.12f, 0f),
                    58f,
                    useVfxVisibility: false,
                    visibleDuringDeploy: true,
                    visibleDuringHold: true,
                    visibleDuringReturn: true,
                    visibleOutsideCycle: true),
                CreateAircraftPresetEquipment(
                    usedDefNames,
                    "Aircraft_VFX_Wing_L",
                    "AircraftWingLeft",
                    "CS_Studio_Equip_Preset_LeftWingVfx".Translate().ToString(),
                    EquipmentTriggeredAnimationRole.EffectLayer,
                    0f,
                    Vector2.zero,
                    70f,
                    useVfxVisibility: true,
                    visibleDuringDeploy: false,
                    visibleDuringHold: true,
                    visibleDuringReturn: false,
                    visibleOutsideCycle: false),
                CreateAircraftPresetEquipment(
                    usedDefNames,
                    "Aircraft_VFX_Wing_R",
                    "AircraftWingRight",
                    "CS_Studio_Equip_Preset_RightWingVfx".Translate().ToString(),
                    EquipmentTriggeredAnimationRole.EffectLayer,
                    0f,
                    Vector2.zero,
                    70f,
                    useVfxVisibility: true,
                    visibleDuringDeploy: false,
                    visibleDuringHold: true,
                    visibleDuringReturn: false,
                    visibleOutsideCycle: false)
            };

            MutateWithUndo(() =>
            {
                WorkingEquipments.AddRange(presets);
                SelectEquipment(Mathf.Max(0, WorkingEquipments.Count - presets.Count));
            }, refreshPreview: true, refreshRenderTree: true);
            ShowStatus("CS_Studio_Equip_PresetAdded".Translate());
        }

        private CharacterEquipmentDef CreateAircraftPresetEquipment(
            HashSet<string> usedDefNames,
            string defBase,
            string animationGroupKey,
            string label,
            EquipmentTriggeredAnimationRole role,
            float deployAngle,
            Vector2 pivot,
            float drawOrder,
            bool useVfxVisibility,
            bool visibleDuringDeploy,
            bool visibleDuringHold,
            bool visibleDuringReturn,
            bool visibleOutsideCycle)
        {
            string baseName = string.IsNullOrWhiteSpace(defBase) ? "CS_Studio_Equip_DefaultPresetDefName".Translate().ToString() : defBase.Trim();
            string uniqueDefName = baseName;
            int suffix = 1;
            while (!usedDefNames.Add(uniqueDefName))
            {
                uniqueDefName = $"{baseName}_{suffix++}";
            }
            var equipment = new CharacterEquipmentDef
            {
                defName = uniqueDefName,
                label = label,
                enabled = true,
                slotTag = CharacterEquipmentDef.DefaultSlotTag,
                renderData = new CharacterEquipmentRenderData
                {
                    layerName = label,
                    anchorTag = CharacterEquipmentDef.DefaultAnchorTag,
                    shaderDefName = CharacterEquipmentDef.DefaultShaderDefName,
                    colorSource = LayerColorSource.White,
                    colorTwoSource = LayerColorSource.White,
                    scale = Vector2.one,
                    visible = true,
                    drawOrder = drawOrder,
                    useTriggeredLocalAnimation = true,
                    triggerAbilityDefName = string.Empty,
                    animationGroupKey = animationGroupKey,
                    triggeredAnimationRole = role,
                    triggeredDeployAngle = deployAngle,
                    triggeredReturnAngle = 0f,
                    triggeredDeployTicks = 10,
                    triggeredHoldTicks = 20,
                    triggeredReturnTicks = 10,
                    triggeredPivotOffset = pivot,
                    triggeredUseVfxVisibility = useVfxVisibility,
                    triggeredVisibleDuringDeploy = visibleDuringDeploy,
                    triggeredVisibleDuringHold = visibleDuringHold,
                    triggeredVisibleDuringReturn = visibleDuringReturn,
                    triggeredVisibleOutsideCycle = visibleOutsideCycle
                }
            };

            equipment.EnsureDefaults();
            equipment.defName = uniqueDefName;
            return equipment;
        }

        private static string GetEquipmentExportDir()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Equipments");
        }

        private static string GetDefaultEquipmentExportFilePath()
        {
            return Path.Combine(GetEquipmentExportDir(), "EquipmentEditor_FormalDefs.xml");
        }

        private void OpenEquipmentImportXmlDialog()
        {
            string initialPath = !string.IsNullOrWhiteSpace(lastImportedEquipmentXmlPath)
                ? lastImportedEquipmentXmlPath
                : (!string.IsNullOrWhiteSpace(lastExportedEquipmentXmlPath) ? lastExportedEquipmentXmlPath : GetDefaultEquipmentExportFilePath());

            Find.WindowStack.Add(new Dialog_FileBrowser(GetEquipmentImportBrowseStartPath(initialPath), selectedPath =>
            {
                string normalizedPath = selectedPath?.Trim().Trim('"') ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    return;
                }

                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("CS_Studio_Equip_ImportReplaceAction".Translate(), () => ImportEquipmentsFromXmlPath(normalizedPath, true)),
                    new FloatMenuOption("CS_Studio_Equip_ImportAppendAction".Translate(), () => ImportEquipmentsFromXmlPath(normalizedPath, false)),
                    new FloatMenuOption("CS_Studio_Btn_Cancel".Translate(), () => { })
                }));
            }, "*.xml"));
        }

        private void ExportSelectedEquipmentToDefaultPath()
        {
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= WorkingEquipments.Count)
            {
                ShowStatus("CS_Studio_Equip_NoSelection".Translate());
                return;
            }

            try
            {
                string exportDir = GetEquipmentExportDir();
                Directory.CreateDirectory(exportDir);
                string exportPath = GetDefaultEquipmentExportFilePath();

                var selected = WorkingEquipments[selectedEquipmentIndex];
                if (selected == null)
                {
                    ShowStatus("CS_Studio_Equip_NoSelection".Translate());
                    return;
                }
                
                selected.EnsureDefaults();
                var cloned = selected.Clone();

                List<CharacterEquipmentDef> exportEquipments = ResolveExportEquipmentGroup(selected);
                bool includeModExtensions = currentTab == EditorTab.Equipment;
                CreateEquipmentsDocument(exportEquipments, includeModExtensions).Save(exportPath);

                XDocument recipeDoc = ModExportXmlWriter.CreateEquipmentRecipeDefsDocument(exportEquipments);
                recipeDoc.Save(Path.Combine(exportDir, "EquipmentEditor_FormalDefs_Recipes.xml"));

                XDocument bundleDoc = ModExportXmlWriter.CreateEquipmentBundleManifestDocument(exportEquipments);
                if (bundleDoc.Root != null && bundleDoc.Root.HasElements)
                {
                    bundleDoc.Save(Path.Combine(exportDir, "EquipmentEditor_FormalDefs_Bundles.xml"));
                }

                lastExportedEquipmentXmlPath = exportPath;
                ShowStatus("CS_Studio_Equip_Exported".Translate(exportPath));
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 装备 XML 导出失败: {ex}");
                ShowStatus("CS_Studio_Equip_ExportFailed".Translate(ex.Message));
            }
        }

        private static XDocument CreateEquipmentsDocument(List<CharacterEquipmentDef> equipmentList, bool includeModExtensions = false)
        {
            return ModExportXmlWriter.CreateEquipmentThingDefsDocument(equipmentList, includeModExtensions);
        }

        private List<CharacterEquipmentDef> ResolveExportEquipmentGroup(CharacterEquipmentDef selected)
        {
            WorkingEquipments ??= new List<CharacterEquipmentDef>();

            if (string.IsNullOrWhiteSpace(selected.exportGroupKey))
            {
                return new List<CharacterEquipmentDef> { selected };
            }

            return WorkingEquipments
                .Where(equipment => equipment != null && equipment.enabled && string.Equals(equipment.exportGroupKey, selected.exportGroupKey, StringComparison.OrdinalIgnoreCase))
                .Select(equipment => equipment.Clone())
                .ToList();
        }

        private void ImportEquipmentsFromXmlPath(string xmlPath, bool replaceExisting)
        {
            try
            {
                if (workingSkin == null)
                {
                    Log.Error("[CharacterStudio] 装备 XML 导入失败：workingSkin 为空");
                    ShowStatus("CS_Studio_Equip_ImportFailed".Translate("CS_Studio_Equip_WorkingSkinNull".Translate()));
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

                WorkingEquipments ??= new List<CharacterEquipmentDef>();
                MutateWithUndo(() =>
                {
                    if (replaceExisting)
                    {
                        WorkingEquipments.Clear();
                    }

                    NormalizeImportedEquipmentDefNames(importedEquipments, WorkingEquipments);
                    foreach (var imported in importedEquipments)
                    {
                        if (imported == null)
                        {
                            continue;
                        }

                        imported.EnsureDefaults();
                        WorkingEquipments.Add(imported.Clone());
                    }

                    lastImportedEquipmentXmlPath = normalizedPath;

                    if (WorkingEquipments.Count > 0)
                    {
                        SelectEquipment(replaceExisting ? 0 : WorkingEquipments.Count - importedEquipments.Count);
                    }
                    else
                    {
                        selectedEquipmentIndex = -1;
                    }
                }, refreshPreview: true, refreshRenderTree: true);

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

                return;
            }

            if (string.Equals(node.Name, "ThingDef", StringComparison.OrdinalIgnoreCase))
            {
                CharacterEquipmentDef? formalEquipment = ParseFormalThingDefNode(node);
                if (formalEquipment != null)
                {
                    result.Add(formalEquipment);
                }
            }
        }

        private static CharacterEquipmentDef? ParseCharacterEquipmentNode(XmlNode node)
        {
            try
            {
                var imported = DirectXmlToObject.ObjectFromXml<CharacterEquipmentDef>(node, true);
                DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
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

        private static CharacterEquipmentDef? ParseFormalThingDefNode(XmlNode node)
        {
            try
            {
                XmlNode? defNameNode = FindEquipmentChildNode(node, "defName");
                if (defNameNode == null || string.IsNullOrWhiteSpace(defNameNode.InnerText))
                    return null;

                string thingDefName = defNameNode.InnerText.Trim();
                string label = FindEquipmentChildNode(node, "label")?.InnerText?.Trim() ?? thingDefName;
                string description = FindEquipmentChildNode(node, "description")?.InnerText?.Trim() ?? string.Empty;

                // 通用字段提取
                string? parentName = node.Attributes?["ParentName"]?.Value;
                string? tradeability = FindEquipmentChildNode(node, "tradeability")?.InnerText?.Trim();
                bool allowTrading = tradeability == null || !string.Equals(tradeability, "None", StringComparison.OrdinalIgnoreCase);
                string? marketValueText = FindEquipmentChildNode(FindEquipmentChildNode(node, "statBases"), "MarketValue")?.InnerText?.Trim();
                float marketValue = float.TryParse(marketValueText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float mv) ? mv : 250f;

                XmlNode? graphicDataNode = FindEquipmentChildNode(node, "graphicData");
                string worldTexPath = FindEquipmentChildNode(graphicDataNode, "texPath")?.InnerText?.Trim() ?? string.Empty;
                string shaderDefName = FindEquipmentChildNode(graphicDataNode, "shaderType")?.InnerText?.Trim() ?? CharacterEquipmentDef.DefaultShaderDefName;

                XmlNode? apparelNode = FindEquipmentChildNode(node, "apparel");
                string wornTexPath = FindEquipmentChildNode(apparelNode, "wornGraphicPath")?.InnerText?.Trim() ?? string.Empty;
                bool useWornGraphicMask = string.Equals(FindEquipmentChildNode(apparelNode, "useWornGraphicMask")?.InnerText?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

                XmlNode? extensionNode = FindEquipmentRenderExtensionNode(node);
                DefModExtension_EquipmentRender? extension = null;
                if (extensionNode != null)
                {
                    extension = DirectXmlToObject.ObjectFromXml<DefModExtension_EquipmentRender>(extensionNode, true);
                    DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
                    extension?.EnsureDefaults();
                }

                string resolvedLabel = string.IsNullOrWhiteSpace(label) ? thingDefName : label;
                string resolvedTexPath = !string.IsNullOrWhiteSpace(worldTexPath) ? worldTexPath : (extension?.texPath ?? string.Empty);
                string resolvedWornTexPath = !string.IsNullOrWhiteSpace(wornTexPath) ? wornTexPath : (extension?.texPath ?? string.Empty);

                var renderData = new CharacterEquipmentRenderData
                {
                    layerName = !string.IsNullOrWhiteSpace(extension?.label) ? extension!.label : resolvedLabel,
                    texPath = extension?.texPath ?? resolvedTexPath,
                    maskTexPath = extension?.maskTexPath ?? string.Empty,
                    anchorTag = extension?.anchorTag ?? CharacterEquipmentDef.DefaultAnchorTag,
                    anchorPath = extension?.anchorPath ?? string.Empty,
                    shaderDefName = !string.IsNullOrWhiteSpace(extension?.shaderDefName) ? extension!.shaderDefName : shaderDefName,
                    directionalFacing = extension?.directionalFacing ?? "South",
                    offset = extension?.offset ?? Vector3.zero,
                    offsetEast = extension?.offsetEast ?? Vector3.zero,
                    offsetNorth = extension?.offsetNorth ?? Vector3.zero,
                    useWestOffset = extension?.useWestOffset ?? false,
                    offsetWest = extension?.offsetWest ?? Vector3.zero,
                    scale = extension?.scale ?? Vector2.one,
                    scaleEastMultiplier = extension?.scaleEastMultiplier ?? Vector2.one,
                    scaleNorthMultiplier = extension?.scaleNorthMultiplier ?? Vector2.one,
                    scaleWestMultiplier = extension?.scaleWestMultiplier ?? Vector2.one,
                    rotation = extension?.rotation ?? 0f,
                    rotationEastOffset = extension?.rotationEastOffset ?? 0f,
                    rotationNorthOffset = extension?.rotationNorthOffset ?? 0f,
                    rotationWestOffset = extension?.rotationWestOffset ?? 0f,
                    drawOrder = extension?.drawOrder ?? 0,
                    flipHorizontal = extension?.flipHorizontal ?? false,
                    visible = extension?.visible ?? true,
                    colorSource = extension?.colorSource ?? LayerColorSource.PawnSkin,
                    customColor = extension?.customColor ?? Color.white,
                    colorTwoSource = extension?.colorTwoSource ?? LayerColorSource.PawnHair,
                    customColorTwo = extension?.customColorTwo ?? Color.white,
                    useTriggeredLocalAnimation = extension?.useTriggeredLocalAnimation ?? false,
                    triggerAbilityDefName = extension?.triggerAbilityDefName ?? string.Empty,
                    animationGroupKey = extension?.animationGroupKey ?? string.Empty,
                    triggeredAnimationRole = extension?.triggeredAnimationRole ?? EquipmentTriggeredAnimationRole.MovablePart,
                    triggeredDeployAngle = extension?.triggeredDeployAngle ?? 0f,
                    triggeredReturnAngle = extension?.triggeredReturnAngle ?? 0f,
                    triggeredDeployTicks = extension?.triggeredDeployTicks ?? 0,
                    triggeredHoldTicks = extension?.triggeredHoldTicks ?? 0,
                    triggeredReturnTicks = extension?.triggeredReturnTicks ?? 0,
                    triggeredPivotOffset = extension?.triggeredPivotOffset ?? Vector2.zero,
                    triggeredUseVfxVisibility = extension?.triggeredUseVfxVisibility ?? false,
                    triggeredIdleTexPath = extension?.triggeredIdleTexPath ?? string.Empty,
                    triggeredDeployTexPath = extension?.triggeredDeployTexPath ?? string.Empty,
                    triggeredHoldTexPath = extension?.triggeredHoldTexPath ?? string.Empty,
                    triggeredReturnTexPath = extension?.triggeredReturnTexPath ?? string.Empty,
                    triggeredIdleMaskTexPath = extension?.triggeredIdleMaskTexPath ?? string.Empty,
                    triggeredDeployMaskTexPath = extension?.triggeredDeployMaskTexPath ?? string.Empty,
                    triggeredHoldMaskTexPath = extension?.triggeredHoldMaskTexPath ?? string.Empty,
                    triggeredReturnMaskTexPath = extension?.triggeredReturnMaskTexPath ?? string.Empty,
                    triggeredVisibleDuringDeploy = extension?.triggeredVisibleDuringDeploy ?? true,
                    triggeredVisibleDuringHold = extension?.triggeredVisibleDuringHold ?? true,
                    triggeredVisibleDuringReturn = extension?.triggeredVisibleDuringReturn ?? true,
                    triggeredVisibleOutsideCycle = extension?.triggeredVisibleOutsideCycle ?? true,
                    triggeredAnimationSouth = extension?.triggeredAnimationSouth?.Clone(),
                    triggeredAnimationEastWest = extension?.triggeredAnimationEastWest?.Clone(),
                    triggeredAnimationNorth = extension?.triggeredAnimationNorth?.Clone()
                };

                var equipment = new CharacterEquipmentDef
                {
                    defName = extension?.equipmentDefName ?? thingDefName,
                    thingDefName = thingDefName,
                    label = resolvedLabel,
                    description = description,
                    slotTag = extension?.slotTag ?? CharacterEquipmentDef.DefaultSlotTag,
                    worldTexPath = resolvedTexPath,
                    wornTexPath = resolvedWornTexPath,
                    maskTexPath = extension?.maskTexPath ?? string.Empty,
                    shaderDefName = !string.IsNullOrWhiteSpace(extension?.shaderDefName) ? extension!.shaderDefName : shaderDefName,
                    exportGroupKey = string.Empty,
                    flyerThingDefName = extension?.flyerThingDefName ?? string.Empty,
                    flyerClassName = extension?.flyerClassName ?? "CharacterStudio.Abilities.CharacterStudioPawnFlyer_Default",
                    flyerFlightSpeed = extension?.flyerFlightSpeed ?? 22f,
                    abilityDefNames = extension?.abilityDefNames != null ? new List<string>(extension.abilityDefNames) : new List<string>(),
                    allowTrading = allowTrading,
                    marketValue = marketValue,
                    useWornGraphicMask = useWornGraphicMask,
                    parentThingDefName = !string.IsNullOrWhiteSpace(parentName) ? parentName! : CharacterEquipmentDef.DefaultParentThingDefName,
                    renderData = renderData
                };

                // 提取 statBases（跳过 MarketValue，已有独立字段）
                XmlNode? statBasesNode = FindEquipmentChildNode(node, "statBases");
                if (statBasesNode != null)
                {
                    foreach (XmlNode child in statBasesNode.ChildNodes)
                    {
                        if (child.NodeType != XmlNodeType.Element || string.IsNullOrWhiteSpace(child.Name))
                            continue;
                        if (string.Equals(child.Name, "MarketValue", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (float.TryParse(child.InnerText.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float statVal))
                        {
                            equipment.statBases.Add(new CharacterEquipmentStatEntry { statDefName = child.Name, value = statVal });
                        }
                    }
                }

                // 提取 equippedStatOffsets
                XmlNode? equippedStatOffsetsNode = FindEquipmentChildNode(node, "equippedStatOffsets");
                if (equippedStatOffsetsNode != null)
                {
                    foreach (XmlNode child in equippedStatOffsetsNode.ChildNodes)
                    {
                        if (child.NodeType != XmlNodeType.Element || string.IsNullOrWhiteSpace(child.Name))
                            continue;
                        if (float.TryParse(child.InnerText.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float statVal))
                        {
                            equipment.equippedStatOffsets.Add(new CharacterEquipmentStatEntry { statDefName = child.Name, value = statVal });
                        }
                    }
                }

                // 提取 thingCategories
                XmlNode? thingCategoriesNode = FindEquipmentChildNode(node, "thingCategories");
                if (thingCategoriesNode != null)
                {
                    foreach (XmlNode child in thingCategoriesNode.ChildNodes)
                    {
                        if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                            equipment.thingCategories.Add(child.InnerText.Trim());
                    }
                }

                // 提取 apparel bodyPartGroups / layers / tags
                if (apparelNode != null)
                {
                    XmlNode? bodyPartGroupsNode = FindEquipmentChildNode(apparelNode, "bodyPartGroups");
                    if (bodyPartGroupsNode != null)
                    {
                        foreach (XmlNode child in bodyPartGroupsNode.ChildNodes)
                        {
                            if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                                equipment.bodyPartGroups.Add(child.InnerText.Trim());
                        }
                    }
                    XmlNode? apparelLayersNode = FindEquipmentChildNode(apparelNode, "layers");
                    if (apparelLayersNode != null)
                    {
                        foreach (XmlNode child in apparelLayersNode.ChildNodes)
                        {
                            if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                                equipment.apparelLayers.Add(child.InnerText.Trim());
                        }
                    }
                    XmlNode? apparelTagsNode = FindEquipmentChildNode(apparelNode, "tags");
                    if (apparelTagsNode != null)
                    {
                        foreach (XmlNode child in apparelTagsNode.ChildNodes)
                        {
                            if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                                equipment.apparelTags.Add(child.InnerText.Trim());
                        }
                    }
                }

                // 提取 weaponTags / weaponClasses
                XmlNode? weaponTagsNode = FindEquipmentChildNode(node, "weaponTags");
                if (weaponTagsNode != null)
                {
                    foreach (XmlNode child in weaponTagsNode.ChildNodes)
                    {
                        if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                            equipment.weaponTags.Add(child.InnerText.Trim());
                    }
                }
                XmlNode? weaponClassesNode = FindEquipmentChildNode(node, "weaponClasses");
                if (weaponClassesNode != null)
                {
                    foreach (XmlNode child in weaponClassesNode.ChildNodes)
                    {
                        if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                            equipment.weaponClasses.Add(child.InnerText.Trim());
                    }
                }

                // 提取 tradeTags
                XmlNode? tradeTagsNode = FindEquipmentChildNode(node, "tradeTags");
                if (tradeTagsNode != null)
                {
                    foreach (XmlNode child in tradeTagsNode.ChildNodes)
                    {
                        if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                            equipment.tradeTags.Add(child.InnerText.Trim());
                    }
                }

                // 根据 ThingDef 内容自动检测 itemType
                equipment.itemType = DetectEquipmentTypeFromNode(node, apparelNode);

                // ── ThingDef 完整字段导入 ──

                // thingClass
                equipment.thingClass = FindEquipmentChildNode(node, "thingClass")?.InnerText?.Trim() ?? string.Empty;

                // techLevel
                equipment.techLevel = FindEquipmentChildNode(node, "techLevel")?.InnerText?.Trim() ?? string.Empty;

                // 顶层布尔/数值字段
                string? useHitPointsText = FindEquipmentChildNode(node, "useHitPoints")?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(useHitPointsText))
                    equipment.useHitPoints = !string.Equals(useHitPointsText, "false", StringComparison.OrdinalIgnoreCase);
                equipment.altitudeLayer = FindEquipmentChildNode(node, "altitudeLayer")?.InnerText?.Trim() ?? string.Empty;
                equipment.tickerType = FindEquipmentChildNode(node, "tickerType")?.InnerText?.Trim() ?? string.Empty;
                string? pathCostText = FindEquipmentChildNode(node, "pathCost")?.InnerText?.Trim();
                if (int.TryParse(pathCostText, out int pc)) equipment.pathCost = pc;
                string? smeltableText = FindEquipmentChildNode(node, "smeltable")?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(smeltableText)) equipment.smeltable = string.Equals(smeltableText, "true", StringComparison.OrdinalIgnoreCase);
                string? rotatableText = FindEquipmentChildNode(node, "rotatable")?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(rotatableText)) equipment.rotatable = string.Equals(rotatableText, "true", StringComparison.OrdinalIgnoreCase);
                string? selectableText = FindEquipmentChildNode(node, "selectable")?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(selectableText)) equipment.selectable = !string.Equals(selectableText, "false", StringComparison.OrdinalIgnoreCase);
                string? drawGUIOverlayText = FindEquipmentChildNode(node, "drawGUIOverlay")?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(drawGUIOverlayText)) equipment.drawGUIOverlay = !string.Equals(drawGUIOverlayText, "false", StringComparison.OrdinalIgnoreCase);
                string? alwaysHaulableText = FindEquipmentChildNode(node, "alwaysHaulable")?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(alwaysHaulableText)) equipment.alwaysHaulable = !string.Equals(alwaysHaulableText, "false", StringComparison.OrdinalIgnoreCase);

                // graphicData 子字段
                if (graphicDataNode != null)
                {
                    equipment.graphicDrawSize = FindEquipmentChildNode(graphicDataNode, "drawSize")?.InnerText?.Trim() ?? string.Empty;
                    string? rotateAngleText = FindEquipmentChildNode(graphicDataNode, "onGroundRandomRotateAngle")?.InnerText?.Trim();
                    if (float.TryParse(rotateAngleText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ra))
                        equipment.graphicRandomRotateAngle = ra;
                }

                // stuffCategories
                XmlNode? stuffCategoriesNode = FindEquipmentChildNode(node, "stuffCategories");
                if (stuffCategoriesNode != null)
                {
                    foreach (XmlNode child in stuffCategoriesNode.ChildNodes)
                    {
                        if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                            equipment.stuffCategories.Add(child.InnerText.Trim());
                    }
                }

                // costStuffCount
                string? costStuffCountText = FindEquipmentChildNode(node, "costStuffCount")?.InnerText?.Trim();
                if (int.TryParse(costStuffCountText, out int csc)) equipment.costStuffCount = csc;

                // costList (ThingDef 级)
                XmlNode? costListNode = FindEquipmentChildNode(node, "costList");
                if (costListNode != null)
                {
                    foreach (XmlNode child in costListNode.ChildNodes)
                    {
                        if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.Name))
                        {
                            if (int.TryParse(child.InnerText?.Trim(), out int count) && count > 0)
                                equipment.costList.Add(new CharacterEquipmentCostEntry { thingDefName = child.Name.Trim(), count = count });
                        }
                    }
                }

                // recipeMaker 子字段
                XmlNode? recipeMakerNode = FindEquipmentChildNode(node, "recipeMaker");
                if (recipeMakerNode != null)
                {
                    equipment.recipeResearchPrerequisite = FindEquipmentChildNode(recipeMakerNode, "researchPrerequisite")?.InnerText?.Trim() ?? string.Empty;
                    equipment.recipeEffectWorking = FindEquipmentChildNode(recipeMakerNode, "effectWorking")?.InnerText?.Trim() ?? string.Empty;
                    equipment.recipeSoundWorking = FindEquipmentChildNode(recipeMakerNode, "soundWorking")?.InnerText?.Trim() ?? string.Empty;
                    equipment.recipeUnfinishedThingDef = FindEquipmentChildNode(recipeMakerNode, "unfinishedThingDef")?.InnerText?.Trim() ?? string.Empty;
                    equipment.recipeWorkSkill = FindEquipmentChildNode(recipeMakerNode, "workSkill")?.InnerText?.Trim() ?? string.Empty;
                    equipment.recipeWorkSpeedStat = FindEquipmentChildNode(recipeMakerNode, "workSpeedStat")?.InnerText?.Trim() ?? string.Empty;
                    string? displayPrioText = FindEquipmentChildNode(recipeMakerNode, "displayPriority")?.InnerText?.Trim();
                    if (float.TryParse(displayPrioText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dp))
                        equipment.recipeDisplayPriority = dp;

                    // skillRequirements
                    XmlNode? skillReqNode = FindEquipmentChildNode(recipeMakerNode, "skillRequirements");
                    if (skillReqNode != null)
                    {
                        foreach (XmlNode child in skillReqNode.ChildNodes)
                        {
                            if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.Name))
                            {
                                if (float.TryParse(child.InnerText?.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sv))
                                    equipment.recipeSkillRequirements.Add(new CharacterEquipmentStatEntry { statDefName = child.Name.Trim(), value = sv });
                            }
                        }
                    }

                    // recipeUsers
                    XmlNode? recipeUsersNode = FindEquipmentChildNode(recipeMakerNode, "recipeUsers");
                    if (recipeUsersNode != null)
                    {
                        foreach (XmlNode child in recipeUsersNode.ChildNodes)
                        {
                            if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                                equipment.recipeUsers.Add(child.InnerText.Trim());
                        }
                    }
                }

                // apparel 子字段扩展
                if (apparelNode != null)
                {
                    string? wearPerDayText = FindEquipmentChildNode(apparelNode, "wearPerDay")?.InnerText?.Trim();
                    if (float.TryParse(wearPerDayText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float wpd))
                        equipment.wearPerDay = wpd;

                    string? careIfDamagedText = FindEquipmentChildNode(apparelNode, "careIfDamaged")?.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(careIfDamagedText))
                        equipment.careIfDamaged = string.Equals(careIfDamagedText, "true", StringComparison.OrdinalIgnoreCase);

                    string? careIfCorpseText = FindEquipmentChildNode(apparelNode, "careIfWornByCorpse")?.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(careIfCorpseText))
                        equipment.careIfWornByCorpse = string.Equals(careIfCorpseText, "true", StringComparison.OrdinalIgnoreCase);

                    string? countsNudityText = FindEquipmentChildNode(apparelNode, "countsAsClothingForNudity")?.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(countsNudityText))
                        equipment.countsAsClothingForNudity = string.Equals(countsNudityText, "true", StringComparison.OrdinalIgnoreCase);

                    string? slaveApparelText = FindEquipmentChildNode(apparelNode, "slaveApparel")?.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(slaveApparelText))
                        equipment.slaveApparel = string.Equals(slaveApparelText, "true", StringComparison.OrdinalIgnoreCase);

                    equipment.developmentalStageFilter = FindEquipmentChildNode(apparelNode, "developmentalStageFilter")?.InnerText?.Trim() ?? string.Empty;
                    equipment.soundWear = FindEquipmentChildNode(apparelNode, "soundWear")?.InnerText?.Trim() ?? string.Empty;
                    equipment.soundRemove = FindEquipmentChildNode(apparelNode, "soundRemove")?.InnerText?.Trim() ?? string.Empty;

                    string? useDeflectText = FindEquipmentChildNode(apparelNode, "useDeflectMetalEffect")?.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(useDeflectText))
                        equipment.useDeflectMetalEffect = string.Equals(useDeflectText, "true", StringComparison.OrdinalIgnoreCase);

                    XmlNode? renderSkipNode = FindEquipmentChildNode(apparelNode, "renderSkipFlags");
                    if (renderSkipNode != null)
                    {
                        foreach (XmlNode child in renderSkipNode.ChildNodes)
                        {
                            if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText))
                                equipment.apparelRenderSkipFlags.Add(child.InnerText.Trim());
                        }
                    }

                    string? blocksVisionText = FindEquipmentChildNode(apparelNode, "blocksVision")?.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(blocksVisionText))
                        equipment.blocksVision = string.Equals(blocksVisionText, "true", StringComparison.OrdinalIgnoreCase);

                    string? ignoredNonViolentText = FindEquipmentChildNode(apparelNode, "ignoredByNonViolent")?.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(ignoredNonViolentText))
                        equipment.ignoredByNonViolent = string.Equals(ignoredNonViolentText, "true", StringComparison.OrdinalIgnoreCase);

                    string? scoreOffsetText = FindEquipmentChildNode(apparelNode, "scoreOffset")?.InnerText?.Trim();
                    if (float.TryParse(scoreOffsetText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float so))
                        equipment.apparelScoreOffset = so;

                    // drawData（原版 ApparelDrawData，保留原始 XML 以确保导出兼容性）
                    XmlNode? drawDataNode = FindEquipmentChildNode(apparelNode, "drawData");
                    if (drawDataNode != null)
                        equipment.apparelDrawDataXml = drawDataNode.InnerXml;
                }

                // 原始 XML 块（comps/verbs/tools 等）
                var rawXmlTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "comps", "verbs", "tools" };
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.NodeType != XmlNodeType.Element) continue;
                    if (rawXmlTags.Contains(child.Name))
                    {
                        equipment.rawXmlEntries.Add(new RawXmlEntry
                        {
                            tagName = child.Name,
                            innerXml = child.InnerXml
                        });
                    }
                }

                equipment.EnsureDefaults();
                if (string.IsNullOrWhiteSpace(equipment.defName))
                    equipment.defName = equipment.thingDefName;
                if (string.IsNullOrWhiteSpace(equipment.label))
                    equipment.label = equipment.defName;

                // 保存原始 ThingDef XML 以支持非破坏性导出
                equipment.rawOriginalThingDefXml = node.OuterXml;

                return equipment;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 解析正式装备 ThingDef 失败: {ex.Message}");
                return null;
            }
        }

        private static CharacterStudio.Core.EquipmentType DetectEquipmentTypeFromNode(XmlNode node, XmlNode? apparelNode)
        {
            string? parentName = node.Attributes?["ParentName"]?.Value;
            // 有 apparel 节点 → Apparel
            if (apparelNode != null)
                return CharacterStudio.Core.EquipmentType.Apparel;

            // 检查 ParentName 暗示的类型
            if (!string.IsNullOrWhiteSpace(parentName))
            {
                string lower = parentName!.ToLowerInvariant();
                if (lower.Contains("gun") || lower.Contains("ranged") || lower.Contains("bullet"))
                    return CharacterStudio.Core.EquipmentType.WeaponRanged;
                if (lower.Contains("melee") || lower.Contains("weapon") || lower.Contains("blade") || lower.Contains("blunt") || lower.Contains("sharp"))
                    return CharacterStudio.Core.EquipmentType.WeaponMelee;
                if (lower.Contains("building"))
                    return CharacterStudio.Core.EquipmentType.Building;
                if (lower.Contains("turret"))
                    return CharacterStudio.Core.EquipmentType.Turret;
            }

            // 检查 verbs 节点（有 verb 通常表示武器）
            XmlNode? verbsNode = FindEquipmentChildNode(node, "verbs");
            if (verbsNode != null)
            {
                // 有 projectile 相关 verb → 远程武器
                string verbsXml = verbsNode.InnerXml;
                if (verbsXml.IndexOf("Projectile", StringComparison.OrdinalIgnoreCase) >= 0
                    || verbsXml.IndexOf("LaunchProjectile", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CharacterStudio.Core.EquipmentType.WeaponRanged;
                // 有 MeleeAttack → 近战武器
                if (verbsXml.IndexOf("MeleeAttack", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CharacterStudio.Core.EquipmentType.WeaponMelee;
                // 有 verb 但不确定类型 → 默认近战
                return CharacterStudio.Core.EquipmentType.WeaponMelee;
            }

            // 检查 tools 节点（武器通常有 tools）
            XmlNode? toolsNode = FindEquipmentChildNode(node, "tools");
            if (toolsNode != null && toolsNode.ChildNodes.Count > 0)
                return CharacterStudio.Core.EquipmentType.WeaponMelee;

            // 检查 thingClass 暗示的类型
            string? thingClass = FindEquipmentChildNode(node, "thingClass")?.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(thingClass))
            {
                string tc = thingClass!;
                if (tc.IndexOf("Building", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CharacterStudio.Core.EquipmentType.Building;
                if (tc.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CharacterStudio.Core.EquipmentType.Turret;
                if (tc.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CharacterStudio.Core.EquipmentType.Apparel;
            }

            // 默认：Item
            return CharacterStudio.Core.EquipmentType.Item;
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
                || string.Equals(nodeName, typeof(PawnSkinDef).FullName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(nodeName, "CharacterEquipmentExportDef", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetEquipmentImportBrowseStartPath(string initialPath)
        {
            if (string.IsNullOrWhiteSpace(initialPath))
            {
                return GetEquipmentExportDir();
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

            return GetEquipmentExportDir();
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

        private static XmlNode? FindEquipmentRenderExtensionNode(XmlNode thingDefNode)
        {
            XmlNode? modExtensionsNode = FindEquipmentChildNode(thingDefNode, "modExtensions");
            if (modExtensionsNode == null)
                return null;

            foreach (XmlNode child in modExtensionsNode.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                    continue;

                if (string.Equals(child.Attributes?["Class"]?.Value, "CharacterStudio.Core.DefModExtension_EquipmentRender", StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            return null;
        }
    }

}
