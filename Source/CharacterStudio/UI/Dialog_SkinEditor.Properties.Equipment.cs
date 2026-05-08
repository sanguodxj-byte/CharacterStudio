using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;
namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private static readonly LayerColorSource[] CachedLayerColorSources =
            (LayerColorSource[])Enum.GetValues(typeof(LayerColorSource));
        private static readonly EquipmentTriggeredAnimationRole[] CachedEquipmentTriggeredAnimationRoles =
            (EquipmentTriggeredAnimationRole[])Enum.GetValues(typeof(EquipmentTriggeredAnimationRole));
        private static readonly string[] EquipmentDefaultCollapsedSections = new[]
        {
            "EquipmentAbilities",
            "EquipmentThingDefCore",
            "EquipmentStats",
            "EquipmentApparel",
            "EquipmentWeapon",
            "EquipmentCrafting",
            "BuildingProperties",
            "TurretProperties"
        };
        private void DrawEquipmentProperties(Rect rect, bool equipmentMode = false)
        {
            // Items 模式下，除基础信息外的 section 首次进入时默认折叠
            if (!equipmentMode && !itemsCollapseInitialized)
            {
                itemsCollapseInitialized = true;
                foreach (var key in EquipmentDefaultCollapsedSections)
                    collapsedSections.Add(key);
            }
            WorkingEquipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();
            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= WorkingEquipments.Count)
            {
                Rect hintRect = new Rect(rect.x + Margin, rect.y + 60, rect.width - Margin * 2, 40);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(hintRect, "CS_Studio_Equip_SelectEntry".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            var equipment = WorkingEquipments[selectedEquipmentIndex];
            equipment ??= new CharacterEquipmentDef();
            equipment.EnsureDefaults();
            CharacterEquipmentRenderData renderData = equipment.renderData;
            WorkingEquipments[selectedEquipmentIndex] = equipment;
            float propsY = GetPropertiesContentTop(rect);
            float propsHeight = rect.height - propsY + rect.y - Margin;
            Rect propsRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2, propsHeight);
            Rect viewRect = new Rect(0, 0, propsRect.width - 16, equipmentMode ? 8000 : 6000);
            Widgets.BeginScrollView(propsRect, ref propsScrollPos, viewRect);
            float y = 0f;
            float width = viewRect.width;
            void MarkEquipmentDirty(bool refreshRenderTree = true, string? statusMessage = null)
            {
                FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: refreshRenderTree, statusMessage: statusMessage);
            }
            // ── Items 标签：物品属性（基础、技能、定义）──
            if (!equipmentMode && DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_Base".Translate(), "EquipmentBase"))
            {
                CharacterStudio.Core.EquipmentType currentItemType = equipment.itemType;
                var equipmentTypeOptions = (CharacterStudio.Core.EquipmentType[])Enum.GetValues(typeof(CharacterStudio.Core.EquipmentType));
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Equip_ItemType".Translate(), currentItemType,
                    equipmentTypeOptions,
                    option => option.ToString(),
                    val =>
                    {
                        if (val != equipment.itemType)
                        {
                            MutateWithUndo(() =>
                            {
                                equipment.itemType = val;
                                equipment.RefreshParentThingDefName();
                            }, refreshPreview: true, refreshRenderTree: false);
                        }
                    });
                // 模板/全量模式切换
                bool templateMode = equipment.useTemplateMode;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_TemplateMode".Translate(), ref templateMode, tooltip: "CS_Studio_Equip_TemplateMode_Tip".Translate());
                if (templateMode != equipment.useTemplateMode)
                {
                    MutateWithUndo(() =>
                    {
                        equipment.useTemplateMode = templateMode;
                        equipment.RefreshParentThingDefName();
                    }, refreshPreview: true, refreshRenderTree: false);
                }
                bool enabled = equipment.enabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_BaseSlot_Enable".Translate(), ref enabled);
                if (enabled != equipment.enabled)
                {
                    MutateWithUndo(() => equipment.enabled = enabled, refreshPreview: true, refreshRenderTree: true);
                }
                string defName = equipment.defName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_DefName".Translate(), ref defName, tooltip: "CS_Studio_Equip_DefName_Tip".Translate());
                if (defName != (equipment.defName ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.defName = defName, refreshPreview: true, refreshRenderTree: false);
                }
                string label = equipment.label ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Label".Translate(), ref label, tooltip: "CS_Studio_Label_Tip".Translate());
                if (label != (equipment.label ?? string.Empty))
                {
                    MutateWithUndo(() =>
                    {
                        equipment.label = label;
                        renderData.layerName = string.IsNullOrWhiteSpace(renderData.layerName) ? label : renderData.layerName;
                    }, refreshPreview: true, refreshRenderTree: false);
                }
                string slotTag = equipment.slotTag ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_SlotTag".Translate(), ref slotTag, tooltip: "CS_Studio_Equip_SlotTag_Tip".Translate());
                if (slotTag != (equipment.slotTag ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.slotTag = slotTag, refreshPreview: true, refreshRenderTree: true);
                }
                string linkedThingDisplay = string.IsNullOrWhiteSpace(equipment.thingDefName)
                    ? "CS_Studio_None".Translate()
                    : GetThingDefSelectionLabel(equipment.thingDefName);
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Studio_Equip_LinkedThingDef".Translate(), equipment.thingDefName, () => linkedThingDisplay,
                    () => ShowThingDefSelector(selected => MutateWithUndo(() => equipment.thingDefName = selected, refreshPreview: true, refreshRenderTree: false)),
                    () => MutateWithUndo(() => equipment.thingDefName = string.Empty, refreshPreview: true, refreshRenderTree: false));
                string exportGroupKey = equipment.exportGroupKey ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ExportGroupKey".Translate(), ref exportGroupKey, tooltip: "CS_Studio_Equip_ExportGroupKey_Tip".Translate());
                if (exportGroupKey != (equipment.exportGroupKey ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.exportGroupKey = exportGroupKey, refreshPreview: true, refreshRenderTree: false);
                }
                string parentThingDisplay = string.IsNullOrWhiteSpace(equipment.parentThingDefName)
                    ? GetThingDefSelectionLabel(CharacterEquipmentDef.DefaultParentThingDefName)
                    : GetThingDefSelectionLabel(equipment.parentThingDefName);
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Studio_Equip_ParentThingDefName".Translate(), equipment.parentThingDefName, () => parentThingDisplay,
                    () => ShowThingDefSelector(selected => MutateWithUndo(() => equipment.parentThingDefName = selected, refreshPreview: true, refreshRenderTree: false)),
                    () => MutateWithUndo(() => { equipment.parentThingDefName = ""; equipment.RefreshParentThingDefName(); }, refreshPreview: true, refreshRenderTree: false));
                string previewTexPath = equipment.previewTexPath ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_Equip_PreviewTexture".Translate(), ref previewTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.previewTexPath ?? string.Empty, path =>
                    {
                        MutateWithUndo(() => equipment.previewTexPath = path ?? string.Empty, refreshPreview: true, refreshRenderTree: false);
                    }))))
                {
                    MutateWithUndo(() => equipment.previewTexPath = previewTexPath, refreshPreview: true, refreshRenderTree: false);
                }
                string sourceNote = equipment.sourceNote ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_SourceNote".Translate(), ref sourceNote, tooltip: "CS_Studio_Equip_SourceNote_Tip".Translate());
                if (sourceNote != (equipment.sourceNote ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.sourceNote = sourceNote, refreshPreview: true, refreshRenderTree: false);
                }
                string tagsDisplay = (equipment.tags != null && equipment.tags.Count > 0) ? string.Join(", ", equipment.tags) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Attr_Tags".Translate(), string.Join(",", equipment.tags ?? new List<string>()), () => tagsDisplay,
                    () => ShowTagSelector(CollectExistingTags(d => d.apparel?.tags), equipment.tags ?? new List<string>(), newTags => MutateWithUndo(() => equipment.tags = newTags, refreshPreview: true, refreshRenderTree: false)),
                    () => MutateWithUndo(() => equipment.tags = new List<string>(), refreshPreview: true, refreshRenderTree: false));
            }
            // ── Items 标签：技能绑定 ──
            if (!equipmentMode && DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_Abilities".Translate(), "EquipmentAbilities"))
            {
                float abilityBtnWidth = (width - Margin) / 2f;
                UIHelper.DrawIconButton(new Rect(0f, y, abilityBtnWidth, 24f), "A", "CS_Studio_Equip_OpenAbilityEditor".Translate(), OpenAbilityEditor);
                UIHelper.DrawIconButton(new Rect(abilityBtnWidth + Margin, y, abilityBtnWidth, 24f), "↓", "CS_Studio_Equip_ImportAbilities".Translate(), OpenAbilityXmlImportDialog);
                y += 30f;
                DrawAbilityBindingList(ref y, width, equipment);
            }
            // ── Items 标签：高级编辑（弹窗）──
            if (!equipmentMode)
            {
                y += 4f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, width, 28f), "CS_Studio_Equip_AdvancedEdit".Translate(), "CS_Studio_Equip_AdvancedEdit".Translate(), () =>
                {
                    Find.WindowStack.Add(new Dialog_EquipmentAdvancedEditor(equipment, this));
                }))
                { }
                y += 32f;
            }
            // ── Equipment 标签：视觉基础 ──
            if (equipmentMode && DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_VisualBase".Translate(), "EquipmentVisualBase"))
            {
                string layerName = renderData.layerName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_VisualLayerName".Translate(), ref layerName);
                if (layerName != (renderData.layerName ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    renderData.layerName = layerName;
                    MarkEquipmentDirty();
                }
                string texPath = renderData.texPath ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_Prop_TexturePath".Translate(), ref texPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(renderData.texPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        renderData.texPath = path ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(equipment.worldTexPath))
                        {
                            equipment.worldTexPath = renderData.texPath;
                        }
                        if (string.IsNullOrWhiteSpace(equipment.wornTexPath))
                        {
                            equipment.wornTexPath = renderData.texPath;
                        }
                        MarkEquipmentDirty();
                    }))))
                {
                    CaptureUndoSnapshot();
                    renderData.texPath = texPath;
                    if (string.IsNullOrWhiteSpace(equipment.worldTexPath))
                    {
                        equipment.worldTexPath = renderData.texPath;
                    }
                    if (string.IsNullOrWhiteSpace(equipment.wornTexPath))
                    {
                        equipment.wornTexPath = renderData.texPath;
                    }
                    MarkEquipmentDirty();
                }
                string previewMaskTexPath = renderData.maskTexPath ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_BaseSlot_MaskTexture".Translate(), ref previewMaskTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(renderData.maskTexPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        renderData.maskTexPath = path ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(equipment.maskTexPath))
                        {
                            equipment.maskTexPath = renderData.maskTexPath;
                        }
                        MarkEquipmentDirty();
                    }))))
                {
                    CaptureUndoSnapshot();
                    renderData.maskTexPath = previewMaskTexPath;
                    if (string.IsNullOrWhiteSpace(equipment.maskTexPath))
                    {
                        equipment.maskTexPath = renderData.maskTexPath;
                    }
                    MarkEquipmentDirty();
                }
                var anchorOptions = new[]
                {
                    "Head", "Body", "Hair", "Beard",
                    "Eyes", "Brow", "Mouth", "Nose", "Ear", "Jaw",
                    "FaceTattoo", "BodyTattoo", "Apparel", "Headgear", "Root"
                };
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Equip_AnchorTag".Translate(), renderData.anchorTag,
                    anchorOptions,
                    option =>
                    {
                        string key = $"CS_Studio_Anchor_{option}";
                        return key.CanTranslate() ? key.Translate() : option;
                    },
                    val =>
                    {
                        CaptureUndoSnapshot();
                        renderData.anchorTag = val;
                        MarkEquipmentDirty();
                    });
                string currentAnchorPath = renderData.anchorPath ?? string.Empty;
                DrawSelectionPropertyButton(
                    ref y,
                    width,
                    "CS_Studio_Equip_AnchorPath".Translate(),
                    string.IsNullOrWhiteSpace(currentAnchorPath) ? "CS_Studio_None".Translate() : currentAnchorPath,
                    () =>
                    {
                        var pathOptions = new List<FloatMenuOption>
                        {
                            new FloatMenuOption("CS_Studio_None".Translate(), () =>
                            {
                                CaptureUndoSnapshot();
                                renderData.anchorPath = string.Empty;
                                MarkEquipmentDirty();
                            })
                        };
                        if (cachedRootSnapshot != null)
                        {
                            var collectedPaths = new List<string>();
                            CollectNodePaths(cachedRootSnapshot, collectedPaths);
                            foreach (var path in collectedPaths.Where(p => !string.IsNullOrWhiteSpace(p))
                                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                            {
                                string localPath = path;
                                pathOptions.Add(new FloatMenuOption(localPath, () =>
                                {
                                    CaptureUndoSnapshot();
                                    renderData.anchorPath = localPath;
                                    MarkEquipmentDirty();
                                }));
                            }
                        }
                        Find.WindowStack.Add(new FloatMenu(pathOptions));
                    });
                string[] directionalFacingOptions = { string.Empty, "South", "North", "East", "West", "EastWest" };
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Variant_DirectionalFacing".Translate(), renderData.directionalFacing ?? string.Empty,
                    directionalFacingOptions,
                    option => GetDirectionalFacingLabel(option),
                    val =>
                    {
                        CaptureUndoSnapshot();
                        renderData.directionalFacing = val;
                        MarkEquipmentDirty(false);
                    });
                bool visible = renderData.visible;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_Visible".Translate(), ref visible);
                if (visible != renderData.visible)
                {
                    CaptureUndoSnapshot();
                    renderData.visible = visible;
                    MarkEquipmentDirty();
                }
                float drawOrder = renderData.drawOrder;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_DrawOrder".Translate(), ref drawOrder, -10f, 120f, "F0");
                if (Math.Abs(drawOrder - renderData.drawOrder) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    renderData.drawOrder = drawOrder;
                    MarkEquipmentDirty();
                }
                bool flip = renderData.flipHorizontal;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_FlipHorizontal".Translate(), ref flip);
                if (flip != renderData.flipHorizontal)
                {
                    CaptureUndoSnapshot();
                    renderData.flipHorizontal = flip;
                    MarkEquipmentDirty();
                }
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_BaseSlot_PrimaryColorSource".Translate(), renderData.colorSource,
                    CachedLayerColorSources,
                    option => option.ToString(),
                    val =>
                    {
                        CaptureUndoSnapshot();
                        renderData.colorSource = val;
                        MarkEquipmentDirty();
                    });
                if (renderData.colorSource == LayerColorSource.Fixed)
                {
                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_BaseSlot_PrimaryColor".Translate(), renderData.customColor, col =>
                    {
                        CaptureUndoSnapshot();
                        renderData.customColor = col;
                        MarkEquipmentDirty();
                    });
                }
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_BaseSlot_SecondaryColorSource".Translate(), renderData.colorTwoSource,
                    CachedLayerColorSources,
                    option => option.ToString(),
                    val =>
                    {
                        CaptureUndoSnapshot();
                        renderData.colorTwoSource = val;
                        MarkEquipmentDirty();
                    });
                if (renderData.colorTwoSource == LayerColorSource.Fixed)
                {
                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_BaseSlot_SecondaryColor".Translate(), renderData.customColorTwo, col =>
                    {
                        CaptureUndoSnapshot();
                        renderData.customColorTwo = col;
                        MarkEquipmentDirty();
                    });
                }
            }
            // ── Equipment 标签：视觉变换 ──
            if (equipmentMode && DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_VisualTransform".Translate(), "EquipmentVisualTransform"))
            {
                Vector3 editableOffset = GetEditableLayerOffsetForPreview(renderData);
                float offsetX = editableOffset.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref offsetX, -2f, 2f, "F3");
                if (Math.Abs(offsetX - editableOffset.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    editableOffset.x = offsetX;
                    SetEditableLayerOffsetForPreview(renderData, editableOffset);
                    MarkEquipmentDirty();
                }
                editableOffset = GetEditableLayerOffsetForPreview(renderData);
                float offsetY = editableOffset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref offsetY, -2f, 2f, "F3");
                if (Math.Abs(offsetY - editableOffset.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    editableOffset.y = offsetY;
                    SetEditableLayerOffsetForPreview(renderData, editableOffset);
                    MarkEquipmentDirty();
                }
                editableOffset = GetEditableLayerOffsetForPreview(renderData);
                float offsetZ = editableOffset.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref offsetZ, -2f, 2f, "F3");
                if (Math.Abs(offsetZ - editableOffset.z) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    editableOffset.z = offsetZ;
                    SetEditableLayerOffsetForPreview(renderData, editableOffset);
                    MarkEquipmentDirty();
                }
                float eastX = renderData.offsetEast.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_EastOffsetX".Translate(), ref eastX, -2f, 2f, "F3");
                if (Math.Abs(eastX - renderData.offsetEast.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    renderData.offsetEast.x = eastX;
                    MarkEquipmentDirty();
                }
                float eastY = renderData.offsetEast.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_EastOffsetY".Translate(), ref eastY, -2f, 2f, "F3");
                if (Math.Abs(eastY - renderData.offsetEast.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    renderData.offsetEast.y = eastY;
                    MarkEquipmentDirty();
                }
                float eastZ = renderData.offsetEast.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_EastOffsetZ".Translate(), ref eastZ, -2f, 2f, "F3");
                if (Math.Abs(eastZ - renderData.offsetEast.z) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    renderData.offsetEast.z = eastZ;
                    MarkEquipmentDirty();
                }
                float northX = renderData.offsetNorth.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_NorthOffsetX".Translate(), ref northX, -2f, 2f, "F3");
                if (Math.Abs(northX - renderData.offsetNorth.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    renderData.offsetNorth.x = northX;
                    MarkEquipmentDirty();
                }
                float northY = renderData.offsetNorth.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_NorthOffsetY".Translate(), ref northY, -2f, 2f, "F3");
                if (Math.Abs(northY - renderData.offsetNorth.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    renderData.offsetNorth.y = northY;
                    MarkEquipmentDirty();
                }
                float northZ = renderData.offsetNorth.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_NorthOffsetZ".Translate(), ref northZ, -2f, 2f, "F3");
                if (Math.Abs(northZ - renderData.offsetNorth.z) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    renderData.offsetNorth.z = northZ;
                    MarkEquipmentDirty();
                }
                Vector2 editableScale = GetEditableLayerScaleForPreview(renderData);
                float scaleX = editableScale.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_ScaleX".Translate(), ref scaleX, 0.1f, 3f, "F2");
                if (Math.Abs(scaleX - editableScale.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    editableScale.x = scaleX;
                    SetEditableLayerScaleForPreview(renderData, editableScale);
                    MarkEquipmentDirty();
                }
                editableScale = GetEditableLayerScaleForPreview(renderData);
                float scaleY = editableScale.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_ScaleY".Translate(), ref scaleY, 0.1f, 3f, "F2");
                if (Math.Abs(scaleY - editableScale.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    editableScale.y = scaleY;
                    SetEditableLayerScaleForPreview(renderData, editableScale);
                    MarkEquipmentDirty();
                }
                float rotation = GetEditableLayerRotationForPreview(renderData);
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_BaseSlot_Rotation".Translate(), ref rotation, -180f, 180f, "F0");
                if (Math.Abs(rotation - GetEditableLayerRotationForPreview(renderData)) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    SetEditableLayerRotationForPreview(renderData, rotation);
                    MarkEquipmentDirty();
                }
                float rotationEastOffset = renderData.rotationEastOffset;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_RotationEastOffset".Translate(), ref rotationEastOffset, -180f, 180f, "F0");
                if (Math.Abs(rotationEastOffset - renderData.rotationEastOffset) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    renderData.rotationEastOffset = rotationEastOffset;
                    MarkEquipmentDirty();
                }
                float rotationNorthOffset = renderData.rotationNorthOffset;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_RotationNorthOffset".Translate(), ref rotationNorthOffset, -180f, 180f, "F0");
                if (Math.Abs(rotationNorthOffset - renderData.rotationNorthOffset) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    renderData.rotationNorthOffset = rotationNorthOffset;
                    MarkEquipmentDirty();
                }
            }
            // ── Equipment 标签：触发动画 ──
            if (equipmentMode && DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_TriggeredAnimation".Translate(), "EquipmentTriggeredAnimation"))
            {
                bool useTriggered = renderData.useTriggeredLocalAnimation;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_TriggeredAnimation_Enable".Translate(), ref useTriggered);
                if (useTriggered != renderData.useTriggeredLocalAnimation)
                {
                    CaptureUndoSnapshot();
                    renderData.useTriggeredLocalAnimation = useTriggered;
                    MarkEquipmentDirty();
                }
                if (renderData.useTriggeredLocalAnimation)
                {
                    // ── 预设选择器 ──
                    DrawSelectionPropertyButton(
                        ref y,
                        width,
                        "CS_Studio_Equip_SwingPreset".Translate(),
                        "CS_Studio_Equip_SwingPreset_None".Translate(),
                        () =>
                        {
                            var options = new List<FloatMenuOption>
                            {
                                new FloatMenuOption("CS_Studio_Equip_SwingPreset_None".Translate(), () => { })
                            };
                            foreach (var preset in WeaponSwingPresetLibrary.Presets)
                            {
                                var localPreset = preset;
                                options.Add(new FloatMenuOption(preset.nameKey.Translate(), () =>
                                {
                                    CaptureUndoSnapshot();
                                    localPreset.ApplyTo(renderData);
                                    MarkEquipmentDirty();
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        });
                    Rect pivotToggleRect = new Rect(0f, y, 110f, 24f);
                    if (Widgets.ButtonText(pivotToggleRect, (equipmentPivotEditMode ? "CS_Studio_Equip_Btn_DisablePivotEdit" : "CS_Studio_Equip_Btn_EnablePivotEdit").Translate()))
                    {
                        equipmentPivotEditMode = !equipmentPivotEditMode;
                        isDraggingEquipmentPivot = false;
                    }
                    y += 28f;
                    DrawSelectionPropertyButton(
                        ref y,
                        width,
                        "CS_Studio_Equip_TriggeredAbilityDefName".Translate(),
                        GetAbilitySelectionLabel(renderData.triggerAbilityDefName, workingDocument?.characterDefinition?.abilityLoadout?.abilities ?? Enumerable.Empty<ModularAbilityDef>()),
                        () => ShowEquipmentTriggeredAbilitySelector(renderData, workingDocument?.characterDefinition?.abilityLoadout?.abilities ?? Enumerable.Empty<ModularAbilityDef>(), () => MarkEquipmentDirty(false)));
                    string animationGroupKey = renderData.animationGroupKey ?? string.Empty;
                    UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_TriggeredAnimationGroupKey".Translate(), ref animationGroupKey);
                    if (animationGroupKey != (renderData.animationGroupKey ?? string.Empty))
                    {
                        CaptureUndoSnapshot();
                        renderData.animationGroupKey = animationGroupKey;
                        MarkEquipmentDirty(false);
                    }
                    UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Equip_TriggeredAnimation_Role".Translate(), renderData.triggeredAnimationRole,
                        CachedEquipmentTriggeredAnimationRoles,
                        option => option.ToString(),
                        val =>
                        {
                            CaptureUndoSnapshot();
                            renderData.triggeredAnimationRole = val;
                            MarkEquipmentDirty();
                        });
                    float deployAngle = renderData.triggeredDeployAngle;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_TriggeredAnimation_DeployAngle".Translate(), ref deployAngle, -180f, 180f, "F0");
                    if (Math.Abs(deployAngle - renderData.triggeredDeployAngle) > 0.0001f) { CaptureUndoSnapshot(); renderData.triggeredDeployAngle = deployAngle; MarkEquipmentDirty(); }
                    float returnAngle = renderData.triggeredReturnAngle;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_TriggeredAnimation_ReturnAngle".Translate(), ref returnAngle, -180f, 180f, "F0");
                    if (Math.Abs(returnAngle - renderData.triggeredReturnAngle) > 0.0001f) { CaptureUndoSnapshot(); renderData.triggeredReturnAngle = returnAngle; MarkEquipmentDirty(); }
                    float deployTicksValue = renderData.triggeredDeployTicks;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_TriggeredAnimation_DeployTicks".Translate(), ref deployTicksValue, 1f, 300f, "F0");
                    int deployTicks = Mathf.RoundToInt(deployTicksValue);
                    if (deployTicks != renderData.triggeredDeployTicks) { CaptureUndoSnapshot(); renderData.triggeredDeployTicks = deployTicks; MarkEquipmentDirty(false); }
                    float holdTicksValue = renderData.triggeredHoldTicks;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_TriggeredAnimation_HoldTicks".Translate(), ref holdTicksValue, 0f, 600f, "F0");
                    int holdTicks = Mathf.RoundToInt(holdTicksValue);
                    if (holdTicks != renderData.triggeredHoldTicks) { CaptureUndoSnapshot(); renderData.triggeredHoldTicks = holdTicks; MarkEquipmentDirty(false); }
                    float returnTicksValue = renderData.triggeredReturnTicks;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_TriggeredAnimation_ReturnTicks".Translate(), ref returnTicksValue, 1f, 300f, "F0");
                    int returnTicks = Mathf.RoundToInt(returnTicksValue);
                    if (returnTicks != renderData.triggeredReturnTicks) { CaptureUndoSnapshot(); renderData.triggeredReturnTicks = returnTicks; MarkEquipmentDirty(false); }
                    float pivotX = renderData.triggeredPivotOffset.x;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_TriggeredAnimation_PivotX".Translate(), ref pivotX, -1f, 1f, "F3");
                    float pivotY = renderData.triggeredPivotOffset.y;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_TriggeredAnimation_PivotY".Translate(), ref pivotY, -1f, 1f, "F3");
                    Vector2 newPivot = new Vector2(pivotX, pivotY);
                    if (newPivot != renderData.triggeredPivotOffset) { CaptureUndoSnapshot(); renderData.triggeredPivotOffset = newPivot; MarkEquipmentDirty(); }
                    float dOffsetX = renderData.triggeredDeployOffset.x;
                    UIHelper.DrawPropertySlider(ref y, width, "Deploy Offset X".Translate(), ref dOffsetX, -5f, 5f, "F3");
                    float dOffsetY = renderData.triggeredDeployOffset.y;
                    UIHelper.DrawPropertySlider(ref y, width, "Deploy Offset Y".Translate(), ref dOffsetY, -5f, 5f, "F3");
                    float dOffsetZ = renderData.triggeredDeployOffset.z;
                    UIHelper.DrawPropertySlider(ref y, width, "Deploy Offset Z".Translate(), ref dOffsetZ, -5f, 5f, "F3");
                    Vector3 newDOffset = new Vector3(dOffsetX, dOffsetY, dOffsetZ);
                    if (newDOffset != renderData.triggeredDeployOffset) { CaptureUndoSnapshot(); renderData.triggeredDeployOffset = newDOffset; MarkEquipmentDirty(); }
                    bool useVfxVisibility = renderData.triggeredUseVfxVisibility;
                    UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_TriggeredAnimation_UseVfxVisibility".Translate(), ref useVfxVisibility);
                    if (useVfxVisibility != renderData.triggeredUseVfxVisibility) { CaptureUndoSnapshot(); renderData.triggeredUseVfxVisibility = useVfxVisibility; MarkEquipmentDirty(); }
                    DrawTriggeredAnimationOverrideSection(ref y, width, "CS_Studio_Equip_Override_South".Translate(), ref renderData.triggeredAnimationSouth, refreshRenderTree => MarkEquipmentDirty(refreshRenderTree));
                    DrawTriggeredAnimationOverrideSection(ref y, width, "CS_Studio_Equip_Override_EastWest".Translate(), ref renderData.triggeredAnimationEastWest, refreshRenderTree => MarkEquipmentDirty(refreshRenderTree));
                    DrawTriggeredAnimationOverrideSection(ref y, width, "CS_Studio_Equip_Override_North".Translate(), ref renderData.triggeredAnimationNorth, refreshRenderTree => MarkEquipmentDirty(refreshRenderTree));
                }
            }
            // ── Equipment 标签：皮肤级动画配置 ──
            if (equipmentMode)
            {
                DrawSkinAnimationSections(ref y, width);
            }
            Widgets.EndScrollView();
        }
        private void DrawTriggeredAnimationOverrideSection(
            ref float y,
            float width,
            string title,
            ref EquipmentTriggeredAnimationOverride? animationOverride,
            Action<bool> markEquipmentDirty)
        {
            if (!DrawCollapsibleSection(ref y, width, title, $"EquipmentTriggeredOverride_{title}"))
            {
                return;
            }
            bool enabled = animationOverride != null;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_Override_Enabled".Translate(), ref enabled);
            if (!enabled)
            {
                if (animationOverride != null)
                {
                    CaptureUndoSnapshot();
                    animationOverride = null;
                    markEquipmentDirty(false);
                }
                return;
            }
            animationOverride ??= new EquipmentTriggeredAnimationOverride();
            animationOverride.EnsureDefaults(string.Empty, string.Empty, string.Empty);
            EquipmentTriggeredAnimationOverride overrideData = animationOverride;
            bool useTriggeredLocalAnimation = overrideData.useTriggeredLocalAnimation;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_Override_UseTriggeredLocal".Translate(), ref useTriggeredLocalAnimation);
            if (useTriggeredLocalAnimation != overrideData.useTriggeredLocalAnimation)
            {
                CaptureUndoSnapshot();
                overrideData.useTriggeredLocalAnimation = useTriggeredLocalAnimation;
                markEquipmentDirty(false);
            }
            string triggerAbilityDisplay = GetAbilitySelectionLabel(overrideData.triggerAbilityDefName, workingDocument?.characterDefinition?.abilityLoadout?.abilities ?? Enumerable.Empty<ModularAbilityDef>());
            DrawSelectionPropertyButton(
                ref y,
                width,
                "CS_Studio_Equip_Override_TriggerAbilityDefName".Translate(),
                triggerAbilityDisplay,
                () => ShowEquipmentTriggeredAbilitySelector(overrideData, workingDocument?.characterDefinition?.abilityLoadout?.abilities ?? Enumerable.Empty<ModularAbilityDef>(), () => markEquipmentDirty(false)));
            string animationGroupKey = overrideData.animationGroupKey ?? string.Empty;
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_Override_AnimGroupKey".Translate(), ref animationGroupKey);
            if (animationGroupKey != (overrideData.animationGroupKey ?? string.Empty))
            {
                CaptureUndoSnapshot();
                overrideData.animationGroupKey = animationGroupKey;
                markEquipmentDirty(false);
            }
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Equip_Override_Role".Translate(), overrideData.triggeredAnimationRole,
                CachedEquipmentTriggeredAnimationRoles,
                option => option.ToString(),
                val =>
                {
                    CaptureUndoSnapshot();
                    overrideData.triggeredAnimationRole = val;
                    markEquipmentDirty(false);
                });
            float deployAngle = overrideData.triggeredDeployAngle;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_Override_DeployAngle".Translate(), ref deployAngle, -180f, 180f, "F0");
            if (Math.Abs(deployAngle - overrideData.triggeredDeployAngle) > 0.0001f)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredDeployAngle = deployAngle;
                markEquipmentDirty(false);
            }
            float returnAngle = overrideData.triggeredReturnAngle;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_Override_ReturnAngle".Translate(), ref returnAngle, -180f, 180f, "F0");
            if (Math.Abs(returnAngle - overrideData.triggeredReturnAngle) > 0.0001f)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredReturnAngle = returnAngle;
                markEquipmentDirty(false);
            }
            float deployTicksValue = overrideData.triggeredDeployTicks;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_Override_DeployTicks".Translate(), ref deployTicksValue, 1f, 300f, "F0");
            int deployTicks = Mathf.RoundToInt(deployTicksValue);
            if (deployTicks != overrideData.triggeredDeployTicks)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredDeployTicks = deployTicks;
                markEquipmentDirty(false);
            }
            float holdTicksValue = overrideData.triggeredHoldTicks;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_Override_HoldTicks".Translate(), ref holdTicksValue, 0f, 600f, "F0");
            int holdTicks = Mathf.RoundToInt(holdTicksValue);
            if (holdTicks != overrideData.triggeredHoldTicks)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredHoldTicks = holdTicks;
                markEquipmentDirty(false);
            }
            float returnTicksValue = overrideData.triggeredReturnTicks;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_Override_ReturnTicks".Translate(), ref returnTicksValue, 1f, 300f, "F0");
            int returnTicks = Mathf.RoundToInt(returnTicksValue);
            if (returnTicks != overrideData.triggeredReturnTicks)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredReturnTicks = returnTicks;
                markEquipmentDirty(false);
            }
            float pivotX = overrideData.triggeredPivotOffset.x;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_Override_PivotX".Translate(), ref pivotX, -1f, 1f, "F3");
            float pivotY = overrideData.triggeredPivotOffset.y;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_Override_PivotY".Translate(), ref pivotY, -1f, 1f, "F3");
            Vector2 newPivot = new Vector2(pivotX, pivotY);
            if (newPivot != overrideData.triggeredPivotOffset)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredPivotOffset = newPivot;
                markEquipmentDirty(false);
            }
            float dOffsetX = overrideData.triggeredDeployOffset.x;
            UIHelper.DrawPropertySlider(ref y, width, "Deploy Offset X".Translate(), ref dOffsetX, -5f, 5f, "F3");
            float dOffsetY = overrideData.triggeredDeployOffset.y;
            UIHelper.DrawPropertySlider(ref y, width, "Deploy Offset Y".Translate(), ref dOffsetY, -5f, 5f, "F3");
            float dOffsetZ = overrideData.triggeredDeployOffset.z;
            UIHelper.DrawPropertySlider(ref y, width, "Deploy Offset Z".Translate(), ref dOffsetZ, -5f, 5f, "F3");
            Vector3 newDOffset = new Vector3(dOffsetX, dOffsetY, dOffsetZ);
            if (newDOffset != overrideData.triggeredDeployOffset)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredDeployOffset = newDOffset;
                markEquipmentDirty(false);
            }
            bool triggeredUseVfxVisibility = overrideData.triggeredUseVfxVisibility;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_Override_UseVfxVisibility".Translate(), ref triggeredUseVfxVisibility);
            if (triggeredUseVfxVisibility != overrideData.triggeredUseVfxVisibility)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredUseVfxVisibility = triggeredUseVfxVisibility;
                markEquipmentDirty(false);
            }
            bool visibleDuringDeploy = overrideData.triggeredVisibleDuringDeploy;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_Override_VisibleDuringDeploy".Translate(), ref visibleDuringDeploy);
            if (visibleDuringDeploy != overrideData.triggeredVisibleDuringDeploy)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredVisibleDuringDeploy = visibleDuringDeploy;
                markEquipmentDirty(false);
            }
            bool visibleDuringHold = overrideData.triggeredVisibleDuringHold;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_Override_VisibleDuringHold".Translate(), ref visibleDuringHold);
            if (visibleDuringHold != overrideData.triggeredVisibleDuringHold)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredVisibleDuringHold = visibleDuringHold;
                markEquipmentDirty(false);
            }
            bool visibleDuringReturn = overrideData.triggeredVisibleDuringReturn;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_Override_VisibleDuringReturn".Translate(), ref visibleDuringReturn);
            if (visibleDuringReturn != overrideData.triggeredVisibleDuringReturn)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredVisibleDuringReturn = visibleDuringReturn;
                markEquipmentDirty(false);
            }
            bool visibleOutsideCycle = overrideData.triggeredVisibleOutsideCycle;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_Override_VisibleOutsideCycle".Translate(), ref visibleOutsideCycle);
            if (visibleOutsideCycle != overrideData.triggeredVisibleOutsideCycle)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredVisibleOutsideCycle = visibleOutsideCycle;
                markEquipmentDirty(false);
            }
        }
        internal static string GetThingDefSelectionLabel(string? defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return "CS_Studio_None".Translate();
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return defName ?? string.Empty;
            }
            string label = def.label ?? def.defName;
            return $"{label} ({def.defName})";
        }
        internal static string GetRecipeDefSelectionLabel(string? defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return "CS_Studio_None".Translate();
            }
            RecipeDef def = DefDatabase<RecipeDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return defName ?? string.Empty;
            }
            string label = def.label ?? def.defName;
            return $"{label} ({def.defName})";
        }
        internal void ShowThingDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<ThingDef>(
                "CS_Studio_Equip_SelectThingDef".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName,
                def => def.FirstThingCategory?.LabelCap ?? string.Empty));
        }
        internal void ShowRecipeDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<RecipeDef>.AllDefsListForReading
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<RecipeDef>(
                "CS_Studio_Equip_SelectRecipeDef".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName,
                def =>
                {
                    var parts = new List<string>();
                    if (def.ProducedThingDef != null)
                        parts.Add($"→ {def.ProducedThingDef.LabelCap}");
                    return string.Join(" | ", parts);
                }));
        }
        internal void ShowBodyPartGroupDefSelector(Action<string> onSelected, CharacterEquipmentDef equipment)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    MutateWithUndo(() => equipment.bodyPartGroups = new List<string>(), refreshPreview: true, refreshRenderTree: false);
                })
            };
            var sortedDefs = DefDatabase<BodyPartGroupDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var def in sortedDefs)
            {
                var localDef = def;
                string label = string.IsNullOrWhiteSpace(localDef.label) ? localDef.defName : localDef.label;
                bool alreadySelected = equipment.bodyPartGroups?.Contains(localDef.defName) == true;
                string prefix = alreadySelected ? "✓ " : "  ";
                options.Add(new FloatMenuOption($"{prefix}{label} [{localDef.defName}]", () =>
                {
                    onSelected(localDef.defName);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        internal void ShowApparelLayerDefSelector(Action<string> onSelected, CharacterEquipmentDef equipment)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    MutateWithUndo(() => equipment.apparelLayers = new List<string>(), refreshPreview: true, refreshRenderTree: false);
                })
            };
            var sortedDefs = DefDatabase<ApparelLayerDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var def in sortedDefs)
            {
                var localDef = def;
                string label = string.IsNullOrWhiteSpace(localDef.label) ? localDef.defName : localDef.label;
                bool alreadySelected = equipment.apparelLayers?.Contains(localDef.defName) == true;
                string prefix = alreadySelected ? "✓ " : "  ";
                options.Add(new FloatMenuOption($"{prefix}{label} [{localDef.defName}]", () =>
                {
                    onSelected(localDef.defName);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private void ShowEquipmentTriggeredAbilitySelector(EquipmentTriggeredAnimationOverride overrideData, IEnumerable<ModularAbilityDef> availableAbilities, Action onChanged)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    CaptureUndoSnapshot();
                    overrideData.triggerAbilityDefName = string.Empty;
                    onChanged?.Invoke();
                })
            };
            foreach (ModularAbilityDef ability in availableAbilities
                .Where(ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                .OrderBy(ability => ability.label ?? ability.defName, StringComparer.OrdinalIgnoreCase))
            {
                ModularAbilityDef localAbility = ability;
                string label = GetAbilitySelectionLabel(localAbility.defName, availableAbilities);
                options.Add(new FloatMenuOption(label, () =>
                {
                    CaptureUndoSnapshot();
                    overrideData.triggerAbilityDefName = localAbility.defName;
                    onChanged?.Invoke();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private void DrawAbilityBindingList(ref float y, float width, CharacterEquipmentDef equipment)
        {
            equipment.abilityDefNames ??= new List<string>();
            IEnumerable<ModularAbilityDef> availableAbilities = workingDocument?.characterDefinition?.abilityLoadout?.abilities
                ?? Enumerable.Empty<ModularAbilityDef>();
            List<ModularAbilityDef> sortedAbilities = availableAbilities
                .Where(ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                .OrderBy(ability => ability.label ?? ability.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (sortedAbilities.Count == 0)
            {
                UIHelper.DrawInfoBanner(ref y, width, "CS_Studio_Equip_NoAbilitiesToBind".Translate(), accent: false);
                return;
            }
            foreach (ModularAbilityDef ability in sortedAbilities)
            {
                string defName = ability.defName;
                bool bound = equipment.abilityDefNames.Contains(defName);
                bool newBound = bound;
                UIHelper.DrawPropertyCheckbox(ref y, width, $"{(ability.label ?? defName)} [{defName}]", ref newBound);
                if (newBound != bound)
                {
                    MutateWithUndo(() =>
                    {
                        if (newBound)
                        {
                            if (!equipment.abilityDefNames.Contains(defName))
                            {
                                equipment.abilityDefNames.Add(defName);
                            }
                        }
                        else
                        {
                            equipment.abilityDefNames.RemoveAll(x => string.Equals(x, defName, StringComparison.OrdinalIgnoreCase));
                        }
                    }, refreshPreview: true, refreshRenderTree: false);
                }
            }
        }
        /// <summary>
        /// 递归收集渲染树节点路径
        /// </summary>
        private static void CollectNodePaths(Introspection.RenderNodeSnapshot node, List<string> paths)
        {
            if (node == null) return;
            if (!string.IsNullOrWhiteSpace(node.uniqueNodePath))
                paths.Add(node.uniqueNodePath);
            if (node.children != null)
            {
                foreach (var child in node.children)
                    CollectNodePaths(child, paths);
            }
        }
        /// <summary>
        /// 打开技能 XML 导入对话框，将导入的技能添加到当前文档的技能池
        /// </summary>
        private void OpenAbilityXmlImportDialog()
        {
            string defaultDir = System.IO.Path.Combine(
                Verse.GenFilePaths.ConfigFolderPath, "CharacterStudio", "Abilities");
            string initialPath = System.IO.Directory.Exists(defaultDir) ? defaultDir : string.Empty;
            Find.WindowStack.Add(new Dialog_FileBrowser(initialPath, selectedPath =>
            {
                string normalizedPath = selectedPath?.Trim().Trim('"') ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedPath) || !System.IO.File.Exists(normalizedPath))
                    return;
                try
                {
                    var doc = System.Xml.Linq.XDocument.Load(normalizedPath);
                    var importedAbilities = Exporter.AbilityXmlSerialization.ParseAbilities(doc.Root);
                    if (importedAbilities == null || importedAbilities.Count == 0)
                    {
                        ShowStatus("CS_Studio_Equip_NoAbilitiesImported".Translate());
                        return;
                    }
                    workingDocument.characterDefinition ??= new CharacterDefinition();
                    workingDocument.characterDefinition.abilityLoadout ??= new CharacterAbilityLoadout();
                    workingDocument.characterDefinition.abilityLoadout.abilities ??= new List<ModularAbilityDef>();
                    foreach (var ability in importedAbilities)
                    {
                        if (ability == null || string.IsNullOrWhiteSpace(ability.defName)) continue;
                        if (!workingDocument.characterDefinition.abilityLoadout.abilities
                            .Any(a => string.Equals(a.defName, ability.defName, StringComparison.OrdinalIgnoreCase)))
                        {
                            workingDocument.characterDefinition.abilityLoadout.abilities.Add(ability);
                        }
                    }
                    ShowStatus("CS_Studio_Equip_AbilitiesImported".Translate(importedAbilities.Count));
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] 导入技能 XML 失败: {ex.Message}");
                    ShowStatus("CS_Studio_Equip_AbilityImportFailed".Translate());
                }
            }, "*.xml", defaultRoot: defaultDir));
        }
        internal void ShowSoundDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<SoundDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<SoundDef>(
                "CS_Studio_Equip_SelectSoundDef".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.defName));
        }
        internal void ShowTerrainAffordanceDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<TerrainAffordanceDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<TerrainAffordanceDef>(
                "CS_Studio_Equip_SelectTerrainAffordance".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName));
        }
        internal static string GetStatDefDisplayLabel(string statDefName)
        {
            if (string.IsNullOrWhiteSpace(statDefName))
                return "CS_Studio_None".Translate();
            var statDef = DefDatabase<StatDef>.GetNamedSilentFail(statDefName);
            if (statDef == null) return statDefName;
            try { return statDef.LabelCap; }
            catch { return statDef.label ?? statDef.defName; }
        }
        internal static string GetThingDefDisplayLabel(string thingDefName)
        {
            if (string.IsNullOrWhiteSpace(thingDefName))
                return "CS_Studio_None".Translate();
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (thingDef == null) return thingDefName;
            try { return thingDef.LabelCap; }
            catch { return thingDef.label ?? thingDef.defName; }
        }
        internal static string GetDamageDefDisplayLabel(string damageDefName)
        {
            if (string.IsNullOrWhiteSpace(damageDefName))
                return "CS_Studio_None".Translate();
            var damageDef = DefDatabase<DamageDef>.GetNamedSilentFail(damageDefName);
            if (damageDef == null) return damageDefName;
            try { return damageDef.LabelCap; }
            catch { return damageDef.label ?? damageDef.defName; }
        }
        internal static string GetSkillDefDisplayLabel(string skillDefName)
        {
            if (string.IsNullOrWhiteSpace(skillDefName))
                return "CS_Studio_None".Translate();
            var skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(skillDefName);
            if (skillDef == null) return skillDefName;
            try { return skillDef.LabelCap; }
            catch { return skillDef.label ?? skillDef.defName; }
        }
        internal void ShowDesignationCategoryDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<DesignationCategoryDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<DesignationCategoryDef>(
                "CS_Studio_Equip_SelectDesignationCategory".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName));
        }
        internal void ShowTurretGunDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def != null && (def.IsWeapon || def.IsRangedWeapon || def.thingClass != null))
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<ThingDef>(
                "CS_Studio_Equip_SelectTurretGunDef".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName,
                def =>
                {
                    var parts = new List<string>();
                    if (def.thingClass != null) parts.Add(def.thingClass.Name);
                    if (def.IsRangedWeapon) parts.Add("Ranged");
                    else if (def.IsMeleeWeapon) parts.Add("Melee");
                    return string.Join(" | ", parts);
                }));
        }
        internal void ShowResearchProjectDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<ResearchProjectDef>(
                "CS_Studio_Equip_SelectResearchProjectDef".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName));
        }
        internal void ShowEffecterDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<EffecterDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<EffecterDef>(
                "CS_Studio_Equip_SelectEffecterDef".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName));
        }
        internal void ShowSkillDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<SkillDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<SkillDef>(
                "CS_Studio_Equip_SelectSkillDef".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName));
        }
        internal void ShowDamageDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<DamageDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<DamageDef>(
                "CS_Studio_Equip_SelectDamageDef".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName));
        }
        internal void ShowStatDefSelector(Action<string> onSelected)
        {
            var sortedDefs = DefDatabase<StatDef>.AllDefsListForReading
                .Where(def => def != null)
                .OrderBy(def => def.label ?? def.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<StatDef>(
                "CS_Studio_Equip_SelectStatDef".Translate(),
                sortedDefs,
                def => onSelected(def.defName),
                def => def.label ?? def.defName,
                def =>
                {
                    var parts = new List<string>();
                    if (def.category != null) parts.Add(def.category.label ?? def.category.defName);
                    return string.Join(" | ", parts);
                }));
        }
        private static string GetAbilitySelectionLabel(string defName, IEnumerable<ModularAbilityDef> availableAbilities)
        {
            if (string.IsNullOrWhiteSpace(defName)) return "CS_Studio_None".Translate();
            var match = availableAbilities?.FirstOrDefault(a => a?.defName == defName);
            return match != null ? (match.label ?? match.defName) : defName;
        }
        private void ShowEquipmentTriggeredAbilitySelector(CharacterEquipmentRenderData renderDataRef, IEnumerable<ModularAbilityDef> availableAbilities, Action onChanged)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    CaptureUndoSnapshot();
                    renderDataRef.triggerAbilityDefName = string.Empty;
                    onChanged();
                })
            };
            foreach (ModularAbilityDef ability in availableAbilities
                .Where(ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                .OrderBy(ability => ability.label ?? ability.defName, StringComparer.OrdinalIgnoreCase))
            {
                ModularAbilityDef localAbility = ability;
                string label = GetAbilitySelectionLabel(localAbility.defName, availableAbilities);
                options.Add(new FloatMenuOption(label, () =>
                {
                    CaptureUndoSnapshot();
                    renderDataRef.triggerAbilityDefName = localAbility.defName;
                    onChanged();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private static string FormatDamageMultiplierEntries(List<CharacterEquipmentDamageMultiplierEntry> entries)
        {
            if (entries == null || entries.Count == 0) return string.Empty;
            return string.Join(", ", entries.Where(e => e != null && !string.IsNullOrWhiteSpace(e.damageDefName)).Select(e => $"{e.damageDefName}:{e.multiplier}"));
        }
        private static List<CharacterEquipmentDamageMultiplierEntry> ParseDamageMultiplierEntries(string input)
        {
            var result = new List<CharacterEquipmentDamageMultiplierEntry>();
            if (string.IsNullOrWhiteSpace(input)) return result;
            foreach (var part in input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                string[] pair = trimmed.Split(new[] { ':' }, 2);
                string defName = pair.Length > 0 ? pair[0].Trim() : "";
                float mult = pair.Length > 1 && float.TryParse(pair[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 1f;
                if (!string.IsNullOrWhiteSpace(defName))
                    result.Add(new CharacterEquipmentDamageMultiplierEntry { damageDefName = defName, multiplier = mult });
            }
            return result;
        }
        private static List<CharacterEquipmentStatEntry> ParseStatEntries(string input)
        {
            var result = new List<CharacterEquipmentStatEntry>();
            if (string.IsNullOrWhiteSpace(input)) return result;
            foreach (var part in input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                string[] pair = trimmed.Split(new[] { ':' }, 2);
                string statDefName = pair.Length > 0 ? pair[0].Trim() : "";
                float value = pair.Length > 1 && float.TryParse(pair[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
                if (!string.IsNullOrWhiteSpace(statDefName))
                    result.Add(new CharacterEquipmentStatEntry { statDefName = statDefName, value = value });
            }
            return result;
        }
        internal static void SyncToolsToRawXml(CharacterEquipmentDef equipment, List<WeaponToolEntry> tools)
        {
            // Remove existing tools entry
            equipment.rawXmlEntries.RemoveAll(e => e.tagName == "tools");
            // Add updated tools entry
            var xml = WeaponToolEntry.SerializeToXml(tools);
            if (!string.IsNullOrWhiteSpace(xml))
            {
                equipment.rawXmlEntries.Add(new RawXmlEntry { tagName = "tools", innerXml = xml });
            }
        }
        // ── Phase 1: Def/Enum selectors ──
        internal void ShowToolCapacityDefSelector(Action<string> onSelected)
        {
            var options = DefDatabase<ToolCapacityDef>.AllDefsListForReading
                .Where(d => d != null)
                .OrderBy(d => d.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<ToolCapacityDef>(
                "Select Tool Capacity", options, def => onSelected(def.defName),
                def => $"{def.defName} ({def.label ?? ""})"));
        }
        internal void ShowProjectileDefSelector(Action<string> onSelected)
        {
            var projectiles = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d != null && d.category == ThingCategory.Projectile)
                .OrderBy(d => d.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<ThingDef>(
                "Select Projectile", projectiles, def => onSelected(def.defName),
                def => $"{def.defName} ({def.label ?? ""})"));
        }
        private void ShowStuffCategoryDefSelector(Action<string> onSelected)
        {
            var options = DefDatabase<StuffCategoryDef>.AllDefsListForReading
                .Where(d => d != null)
                .OrderBy(d => d.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<StuffCategoryDef>(
                "Select Stuff Category", options, def => onSelected(def.defName),
                def => def.label ?? def.defName));
        }
        private void ShowThingCategoryDefSelector(Action<string> onSelected)
        {
            var options = DefDatabase<ThingCategoryDef>.AllDefsListForReading
                .Where(d => d != null)
                .OrderBy(d => d.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Find.WindowStack.Add(new Dialog_DefBrowser<ThingCategoryDef>(
                "Select Thing Category", options, def => onSelected(def.defName),
                def => def.label ?? def.defName));
        }
        internal void ShowEnumSelector(IEnumerable<string> options, Action<string> onSelected)
        {
            var floatMenuOptions = options
                .Select(o => new FloatMenuOption(string.IsNullOrEmpty(o) ? "(None)" : o, () => onSelected(o)))
                .ToList();
            if (floatMenuOptions.Count > 0)
                Find.WindowStack.Add(new FloatMenu(floatMenuOptions));
        }
        // ── Phase 2: Tag scanners + multi-select ──
        internal static List<string> CollectExistingTags(Func<ThingDef, IEnumerable<string>?> tagGetter)
        {
            var tags = new HashSet<string>();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var list = tagGetter(def);
                if (list != null)
                    foreach (var t in list)
                        if (!string.IsNullOrWhiteSpace(t)) tags.Add(t);
            }
            return tags.OrderBy(t => t).ToList();
        }
        internal void ShowTagSelector(IEnumerable<string> availableTags, List<string> currentTags, Action<List<string>> onUpdated)
        {
            var options = new List<FloatMenuOption>();
            foreach (var tag in availableTags)
            {
                bool isSelected = currentTags.Contains(tag);
                string prefix = isSelected ? "✓ " : "  ";
                var capturedTag = tag;
                options.Add(new FloatMenuOption(prefix + tag, () =>
                {
                    var newTags = new List<string>(currentTags);
                    if (isSelected) newTags.Remove(capturedTag);
                    else newTags.Add(capturedTag);
                    onUpdated(newTags);
                }));
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }
        internal static void SyncVerbsToRawXml(CharacterEquipmentDef equipment, List<WeaponVerbEntry> verbs)
        {
            equipment.rawXmlEntries.RemoveAll(e => e.tagName == "verbs");
            var xml = WeaponVerbEntry.SerializeToXml(verbs);
            if (!string.IsNullOrWhiteSpace(xml))
            {
                equipment.rawXmlEntries.Add(new RawXmlEntry { tagName = "verbs", innerXml = xml });
            }
        }
    }
}
