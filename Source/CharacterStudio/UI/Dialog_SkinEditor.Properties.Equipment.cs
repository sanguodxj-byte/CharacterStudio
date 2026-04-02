using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void DrawEquipmentProperties(Rect rect)
        {
            workingSkin.equipments ??= new List<CharacterEquipmentDef>();
            SanitizeEquipmentSelection();

            if (selectedEquipmentIndex < 0 || selectedEquipmentIndex >= workingSkin.equipments.Count)
            {
                Rect hintRect = new Rect(rect.x + Margin, rect.y + 60, rect.width - Margin * 2, 40);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(hintRect, "CS_Studio_Equip_SelectEntry".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            var equipment = workingSkin.equipments[selectedEquipmentIndex];
            equipment ??= new CharacterEquipmentDef();
            equipment.EnsureDefaults();
            CharacterEquipmentRenderData renderData = equipment.renderData;
            workingSkin.equipments[selectedEquipmentIndex] = equipment;

            float propsY = GetPropertiesContentTop(rect);
            float propsHeight = rect.height - propsY + rect.y - Margin;
            Rect propsRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2, propsHeight);
            Rect viewRect = new Rect(0, 0, propsRect.width - 16, 1500);

            Widgets.BeginScrollView(propsRect, ref propsScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            bool DrawPathFieldWithBrowser(ref float rowY, string label, ref string value, Action browseAction)
            {
                Rect rowRect = new Rect(0f, rowY, width, UIHelper.RowHeight);
                Text.Font = GameFont.Small;

                float actualLabelWidth = Mathf.Max(UIHelper.LabelWidth, Text.CalcSize(label).x + 10f);
                float buttonWidth = 30f;
                float spacing = 5f;
                float fieldWidth = Mathf.Max(40f, rowRect.width - actualLabelWidth - buttonWidth - spacing);

                Widgets.Label(new Rect(rowRect.x, rowRect.y, actualLabelWidth, 24f), label);

                string newValue = Widgets.TextField(
                    new Rect(rowRect.x + actualLabelWidth, rowRect.y, fieldWidth, 24f),
                    value ?? string.Empty);

                bool changed = false;
                if (newValue != value)
                {
                    value = UIHelper.SanitizeInput(newValue, 260);
                    changed = true;
                }

                if (Widgets.ButtonText(
                    new Rect(rowRect.x + actualLabelWidth + fieldWidth + spacing, rowRect.y, buttonWidth, 24f),
                    "..."))
                {
                    browseAction?.Invoke();
                }

                rowY += UIHelper.RowHeight;
                return changed;
            }

            void MarkEquipmentDirty(bool refreshRenderTree = true)
            {
                isDirty = true;
                RefreshPreview();
                if (refreshRenderTree)
                {
                    RefreshRenderTree();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_Base".Translate(), "EquipmentBase"))
            {
                bool enabled = equipment.enabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_BaseSlot_Enable".Translate(), ref enabled);
                if (enabled != equipment.enabled)
                {
                    CaptureUndoSnapshot();
                    equipment.enabled = enabled;
                    MarkEquipmentDirty();
                }

                string defName = equipment.defName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_DefName".Translate(), ref defName);
                if (defName != (equipment.defName ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.defName = defName;
                    MarkEquipmentDirty(false);
                }

                string label = equipment.label ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Label".Translate(), ref label);
                if (label != (equipment.label ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.label = label;
                    renderData.layerName = string.IsNullOrWhiteSpace(renderData.layerName) ? label : renderData.layerName;
                    MarkEquipmentDirty(false);
                }

                string slotTag = equipment.slotTag ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_SlotTag".Translate(), ref slotTag);
                if (slotTag != (equipment.slotTag ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.slotTag = slotTag;
                    MarkEquipmentDirty();
                }

                string thingDefName = equipment.thingDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_LinkedThingDef".Translate(), ref thingDefName);
                if (thingDefName != (equipment.thingDefName ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.thingDefName = thingDefName;
                    MarkEquipmentDirty(false);
                }

                string parentThingDefName = equipment.parentThingDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ParentThingDefName".Translate(), ref parentThingDefName);
                if (parentThingDefName != (equipment.parentThingDefName ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.parentThingDefName = string.IsNullOrWhiteSpace(parentThingDefName) ? CharacterEquipmentDef.DefaultParentThingDefName : parentThingDefName;
                    MarkEquipmentDirty(false);
                }

                string previewTexPath = equipment.previewTexPath ?? string.Empty;
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_Equip_PreviewTexture".Translate(), ref previewTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.previewTexPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        equipment.previewTexPath = path ?? string.Empty;
                        MarkEquipmentDirty(false);
                    }))))
                {
                    CaptureUndoSnapshot();
                    equipment.previewTexPath = previewTexPath;
                    MarkEquipmentDirty(false);
                }

                string sourceNote = equipment.sourceNote ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_SourceNote".Translate(), ref sourceNote);
                if (sourceNote != (equipment.sourceNote ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.sourceNote = sourceNote;
                    MarkEquipmentDirty(false);
                }

                string tagsText = string.Join(", ", equipment.tags ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Attr_Tags".Translate(), ref tagsText);
                string normalizedTagsText = string.Join(", ", equipment.tags ?? new List<string>());
                if (tagsText != normalizedTagsText)
                {
                    CaptureUndoSnapshot();
                    equipment.tags = ParseCommaSeparatedList(tagsText).ToList();
                    MarkEquipmentDirty(false);
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_Abilities".Translate(), "EquipmentAbilities"))
            {
                if (Widgets.ButtonText(new Rect(0f, y, width, 24f), "CS_Studio_Equip_OpenAbilityEditor".Translate()))
                {
                    SyncAbilitiesFromSkin();
                    Find.WindowStack.Add(new Dialog_AbilityEditor(workingAbilities, workingSkin.abilityHotkeys, workingSkin));
                }
                y += 30f;

                if (workingAbilities == null || workingAbilities.Count == 0)
                {
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(0f, y, width, 24f), "CS_Studio_Equip_NoAbilityPool".Translate());
                    GUI.color = Color.white;
                    y += 26f;
                }
                else
                {
                    equipment.abilityDefNames ??= new List<string>();
                    foreach (var ability in workingAbilities)
                    {
                        if (ability == null || string.IsNullOrWhiteSpace(ability.defName))
                        {
                            continue;
                        }

                        bool bound = equipment.abilityDefNames.Contains(ability.defName);
                        bool newBound = bound;
                        UIHelper.DrawPropertyCheckbox(ref y, width, $"{ability.label ?? ability.defName} [{ability.defName}]", ref newBound);
                        if (newBound != bound)
                        {
                            CaptureUndoSnapshot();
                            if (newBound)
                            {
                                if (!equipment.abilityDefNames.Contains(ability.defName))
                                {
                                    equipment.abilityDefNames.Add(ability.defName);
                                }
                            }
                            else
                            {
                                equipment.abilityDefNames.RemoveAll(x => string.Equals(x, ability.defName, StringComparison.OrdinalIgnoreCase));
                            }

                            MarkEquipmentDirty(false);
                        }
                    }
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_Definition".Translate(), "EquipmentDefinition"))
            {
                string worldTexPath = equipment.worldTexPath ?? string.Empty;
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_Equip_WorldTexPath".Translate(), ref worldTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.worldTexPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        equipment.worldTexPath = path ?? string.Empty;
                        MarkEquipmentDirty(false);
                    }))))
                {
                    CaptureUndoSnapshot();
                    equipment.worldTexPath = worldTexPath;
                    MarkEquipmentDirty(false);
                }

                string wornTexPath = equipment.wornTexPath ?? string.Empty;
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_Equip_WornTexPath".Translate(), ref wornTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.wornTexPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        equipment.wornTexPath = path ?? string.Empty;
                        MarkEquipmentDirty(false);
                    }))))
                {
                    CaptureUndoSnapshot();
                    equipment.wornTexPath = wornTexPath;
                    MarkEquipmentDirty(false);
                }

                string equipmentMaskTexPath = equipment.maskTexPath ?? string.Empty;
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_Equip_ApparelMask".Translate(), ref equipmentMaskTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.maskTexPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        equipment.maskTexPath = path ?? string.Empty;
                        MarkEquipmentDirty(false);
                    }))))
                {
                    CaptureUndoSnapshot();
                    equipment.maskTexPath = equipmentMaskTexPath;
                    MarkEquipmentDirty(false);
                }

                DrawSelectionPropertyButton(
                    ref y,
                    width,
                    "CS_Studio_Equip_ShaderDef".Translate(),
                    GetEquipmentShaderSelectionLabel(equipment.shaderDefName),
                    () => ShowEquipmentShaderSelector(equipment, () => MarkEquipmentDirty()));

                bool useWornGraphicMask = equipment.useWornGraphicMask;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_UseWornGraphicMask".Translate(), ref useWornGraphicMask);
                if (useWornGraphicMask != equipment.useWornGraphicMask)
                {
                    CaptureUndoSnapshot();
                    equipment.useWornGraphicMask = useWornGraphicMask;
                    MarkEquipmentDirty(false);
                }

                string thingCategoriesText = string.Join(", ", equipment.thingCategories ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ThingCategories".Translate(), ref thingCategoriesText);
                string normalizedThingCategoriesText = string.Join(", ", equipment.thingCategories ?? new List<string>());
                if (thingCategoriesText != normalizedThingCategoriesText)
                {
                    CaptureUndoSnapshot();
                    equipment.thingCategories = ParseCommaSeparatedList(thingCategoriesText).ToList();
                    MarkEquipmentDirty(false);
                }

                string bodyPartGroupsText = string.Join(", ", equipment.bodyPartGroups ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_BodyPartGroups".Translate(), ref bodyPartGroupsText);
                string normalizedBodyPartGroupsText = string.Join(", ", equipment.bodyPartGroups ?? new List<string>());
                if (bodyPartGroupsText != normalizedBodyPartGroupsText)
                {
                    CaptureUndoSnapshot();
                    equipment.bodyPartGroups = ParseCommaSeparatedList(bodyPartGroupsText).ToList();
                    MarkEquipmentDirty(false);
                }

                string apparelLayersText = string.Join(", ", equipment.apparelLayers ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ApparelLayers".Translate(), ref apparelLayersText);
                string normalizedApparelLayersText = string.Join(", ", equipment.apparelLayers ?? new List<string>());
                if (apparelLayersText != normalizedApparelLayersText)
                {
                    CaptureUndoSnapshot();
                    equipment.apparelLayers = ParseCommaSeparatedList(apparelLayersText).ToList();
                    MarkEquipmentDirty(false);
                }

                string apparelTagsText = string.Join(", ", equipment.apparelTags ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ApparelTags".Translate(), ref apparelTagsText);
                string normalizedApparelTagsText = string.Join(", ", equipment.apparelTags ?? new List<string>());
                if (apparelTagsText != normalizedApparelTagsText)
                {
                    CaptureUndoSnapshot();
                    equipment.apparelTags = ParseCommaSeparatedList(apparelTagsText).ToList();
                    MarkEquipmentDirty(false);
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_VisualBase".Translate(), "EquipmentVisualBase"))
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
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_Prop_TexturePath".Translate(), ref texPath, () =>
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
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_BaseSlot_MaskTexture".Translate(), ref previewMaskTexPath, () =>
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

                string anchorPath = renderData.anchorPath ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_AnchorPath".Translate(), ref anchorPath);
                if (anchorPath != (renderData.anchorPath ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    renderData.anchorPath = anchorPath;
                    MarkEquipmentDirty();
                }

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
                    (LayerColorSource[])Enum.GetValues(typeof(LayerColorSource)),
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
                    (LayerColorSource[])Enum.GetValues(typeof(LayerColorSource)),
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

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_VisualTransform".Translate(), "EquipmentVisualTransform"))
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

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_TriggeredAnimation".Translate(), "EquipmentTriggeredAnimation"))
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
                        GetAbilitySelectionLabel(renderData.triggerAbilityDefName, workingAbilities ?? Enumerable.Empty<ModularAbilityDef>()),
                        () => ShowEquipmentTriggeredAbilitySelector(renderData, workingAbilities ?? Enumerable.Empty<ModularAbilityDef>(), () => MarkEquipmentDirty(false)));

                    string triggerAbilityDefName = renderData.triggerAbilityDefName ?? string.Empty;
                    UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_TriggeredAbilityDefName".Translate() + " Raw", ref triggerAbilityDefName);
                    if (triggerAbilityDefName != (renderData.triggerAbilityDefName ?? string.Empty))
                    {
                        CaptureUndoSnapshot();
                        renderData.triggerAbilityDefName = triggerAbilityDefName;
                        MarkEquipmentDirty(false);
                    }
                    string animationGroupKey = renderData.animationGroupKey ?? string.Empty;
                    UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_TriggeredAnimationGroupKey".Translate(), ref animationGroupKey);
                    if (animationGroupKey != (renderData.animationGroupKey ?? string.Empty))
                    {
                        CaptureUndoSnapshot();
                        renderData.animationGroupKey = animationGroupKey;
                        MarkEquipmentDirty(false);
                    }

                    UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Equip_TriggeredAnimation_Role".Translate(), renderData.triggeredAnimationRole,
                        (EquipmentTriggeredAnimationRole[])Enum.GetValues(typeof(EquipmentTriggeredAnimationRole)),
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

                    bool useVfxVisibility = renderData.triggeredUseVfxVisibility;
                    UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_TriggeredAnimation_UseVfxVisibility".Translate(), ref useVfxVisibility);
                    if (useVfxVisibility != renderData.triggeredUseVfxVisibility) { CaptureUndoSnapshot(); renderData.triggeredUseVfxVisibility = useVfxVisibility; MarkEquipmentDirty(); }

                    DrawTriggeredAnimationOverrideSection(ref y, width, "South Override", ref renderData.triggeredAnimationSouth, MarkEquipmentDirty);
                    DrawTriggeredAnimationOverrideSection(ref y, width, "East/West Override", ref renderData.triggeredAnimationEastWest, MarkEquipmentDirty);
                    DrawTriggeredAnimationOverrideSection(ref y, width, "North Override", ref renderData.triggeredAnimationNorth, MarkEquipmentDirty);
                }
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
            UIHelper.DrawPropertyCheckbox(ref y, width, "Enabled", ref enabled);
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
            UIHelper.DrawPropertyCheckbox(ref y, width, "Use Triggered Local Animation", ref useTriggeredLocalAnimation);
            if (useTriggeredLocalAnimation != overrideData.useTriggeredLocalAnimation)
            {
                CaptureUndoSnapshot();
                overrideData.useTriggeredLocalAnimation = useTriggeredLocalAnimation;
                markEquipmentDirty(false);
            }

            string triggerAbilityDefName = overrideData.triggerAbilityDefName ?? string.Empty;
            UIHelper.DrawPropertyField(ref y, width, "Trigger Ability DefName", ref triggerAbilityDefName);
            if (triggerAbilityDefName != (overrideData.triggerAbilityDefName ?? string.Empty))
            {
                CaptureUndoSnapshot();
                overrideData.triggerAbilityDefName = triggerAbilityDefName;
                markEquipmentDirty(false);
            }

            string animationGroupKey = overrideData.animationGroupKey ?? string.Empty;
            UIHelper.DrawPropertyField(ref y, width, "Animation Group Key", ref animationGroupKey);
            if (animationGroupKey != (overrideData.animationGroupKey ?? string.Empty))
            {
                CaptureUndoSnapshot();
                overrideData.animationGroupKey = animationGroupKey;
                markEquipmentDirty(false);
            }

            UIHelper.DrawPropertyDropdown(ref y, width, "Role", overrideData.triggeredAnimationRole,
                (EquipmentTriggeredAnimationRole[])Enum.GetValues(typeof(EquipmentTriggeredAnimationRole)),
                option => option.ToString(),
                val =>
                {
                    CaptureUndoSnapshot();
                    overrideData.triggeredAnimationRole = val;
                    markEquipmentDirty(false);
                });

            float deployAngle = overrideData.triggeredDeployAngle;
            UIHelper.DrawPropertySlider(ref y, width, "Deploy Angle", ref deployAngle, -180f, 180f, "F0");
            if (Math.Abs(deployAngle - overrideData.triggeredDeployAngle) > 0.0001f)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredDeployAngle = deployAngle;
                markEquipmentDirty(false);
            }

            float returnAngle = overrideData.triggeredReturnAngle;
            UIHelper.DrawPropertySlider(ref y, width, "Return Angle", ref returnAngle, -180f, 180f, "F0");
            if (Math.Abs(returnAngle - overrideData.triggeredReturnAngle) > 0.0001f)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredReturnAngle = returnAngle;
                markEquipmentDirty(false);
            }

            float deployTicksValue = overrideData.triggeredDeployTicks;
            UIHelper.DrawPropertySlider(ref y, width, "Deploy Ticks", ref deployTicksValue, 1f, 300f, "F0");
            int deployTicks = Mathf.RoundToInt(deployTicksValue);
            if (deployTicks != overrideData.triggeredDeployTicks)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredDeployTicks = deployTicks;
                markEquipmentDirty(false);
            }

            float holdTicksValue = overrideData.triggeredHoldTicks;
            UIHelper.DrawPropertySlider(ref y, width, "Hold Ticks", ref holdTicksValue, 0f, 600f, "F0");
            int holdTicks = Mathf.RoundToInt(holdTicksValue);
            if (holdTicks != overrideData.triggeredHoldTicks)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredHoldTicks = holdTicks;
                markEquipmentDirty(false);
            }

            float returnTicksValue = overrideData.triggeredReturnTicks;
            UIHelper.DrawPropertySlider(ref y, width, "Return Ticks", ref returnTicksValue, 1f, 300f, "F0");
            int returnTicks = Mathf.RoundToInt(returnTicksValue);
            if (returnTicks != overrideData.triggeredReturnTicks)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredReturnTicks = returnTicks;
                markEquipmentDirty(false);
            }

            float pivotX = overrideData.triggeredPivotOffset.x;
            UIHelper.DrawPropertySlider(ref y, width, "Pivot X", ref pivotX, -1f, 1f, "F3");
            float pivotY = overrideData.triggeredPivotOffset.y;
            UIHelper.DrawPropertySlider(ref y, width, "Pivot Y", ref pivotY, -1f, 1f, "F3");
            Vector2 newPivot = new Vector2(pivotX, pivotY);
            if (newPivot != overrideData.triggeredPivotOffset)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredPivotOffset = newPivot;
                markEquipmentDirty(false);
            }

            bool triggeredUseVfxVisibility = overrideData.triggeredUseVfxVisibility;
            UIHelper.DrawPropertyCheckbox(ref y, width, "Use VFX Visibility", ref triggeredUseVfxVisibility);
            if (triggeredUseVfxVisibility != overrideData.triggeredUseVfxVisibility)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredUseVfxVisibility = triggeredUseVfxVisibility;
                markEquipmentDirty(false);
            }

            bool visibleDuringDeploy = overrideData.triggeredVisibleDuringDeploy;
            UIHelper.DrawPropertyCheckbox(ref y, width, "Visible During Deploy", ref visibleDuringDeploy);
            if (visibleDuringDeploy != overrideData.triggeredVisibleDuringDeploy)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredVisibleDuringDeploy = visibleDuringDeploy;
                markEquipmentDirty(false);
            }

            bool visibleDuringHold = overrideData.triggeredVisibleDuringHold;
            UIHelper.DrawPropertyCheckbox(ref y, width, "Visible During Hold", ref visibleDuringHold);
            if (visibleDuringHold != overrideData.triggeredVisibleDuringHold)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredVisibleDuringHold = visibleDuringHold;
                markEquipmentDirty(false);
            }

            bool visibleDuringReturn = overrideData.triggeredVisibleDuringReturn;
            UIHelper.DrawPropertyCheckbox(ref y, width, "Visible During Return", ref visibleDuringReturn);
            if (visibleDuringReturn != overrideData.triggeredVisibleDuringReturn)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredVisibleDuringReturn = visibleDuringReturn;
                markEquipmentDirty(false);
            }

            bool visibleOutsideCycle = overrideData.triggeredVisibleOutsideCycle;
            UIHelper.DrawPropertyCheckbox(ref y, width, "Visible Outside Cycle", ref visibleOutsideCycle);
            if (visibleOutsideCycle != overrideData.triggeredVisibleOutsideCycle)
            {
                CaptureUndoSnapshot();
                overrideData.triggeredVisibleOutsideCycle = visibleOutsideCycle;
                markEquipmentDirty(false);
            }
        }
    }
}
