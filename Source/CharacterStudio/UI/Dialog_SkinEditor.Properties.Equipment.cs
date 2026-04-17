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
        private static readonly LayerColorSource[] CachedLayerColorSources =
            (LayerColorSource[])Enum.GetValues(typeof(LayerColorSource));
        private static readonly EquipmentTriggeredAnimationRole[] CachedEquipmentTriggeredAnimationRoles =
            (EquipmentTriggeredAnimationRole[])Enum.GetValues(typeof(EquipmentTriggeredAnimationRole));

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

            void MutateEquipmentWithUndo(Action mutation, bool refreshRenderTree = true)
            {
                MutateWithUndo(mutation, refreshPreview: true, refreshRenderTree: refreshRenderTree);
            }

            void MarkEquipmentDirty(bool refreshRenderTree = true, string? statusMessage = null)
            {
                FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: refreshRenderTree, statusMessage: statusMessage);
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_Base".Translate(), "EquipmentBase"))
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
                            MutateWithUndo(() => equipment.itemType = val, refreshPreview: true, refreshRenderTree: false);
                        }
                    });

                bool enabled = equipment.enabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_BaseSlot_Enable".Translate(), ref enabled);
                if (enabled != equipment.enabled)
                {
                    MutateWithUndo(() => equipment.enabled = enabled, refreshPreview: true, refreshRenderTree: true);
                }

                string defName = equipment.defName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_DefName".Translate(), ref defName);
                if (defName != (equipment.defName ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.defName = defName, refreshPreview: true, refreshRenderTree: false);
                }

                string label = equipment.label ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Label".Translate(), ref label);
                if (label != (equipment.label ?? string.Empty))
                {
                    MutateWithUndo(() =>
                    {
                        equipment.label = label;
                        renderData.layerName = string.IsNullOrWhiteSpace(renderData.layerName) ? label : renderData.layerName;
                    }, refreshPreview: true, refreshRenderTree: false);
                }

                string slotTag = equipment.slotTag ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_SlotTag".Translate(), ref slotTag);
                if (slotTag != (equipment.slotTag ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.slotTag = slotTag, refreshPreview: true, refreshRenderTree: true);
                }

                string thingDefName = equipment.thingDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_LinkedThingDef".Translate(), ref thingDefName);
                if (thingDefName != (equipment.thingDefName ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.thingDefName = thingDefName, refreshPreview: true, refreshRenderTree: false);
                }

                string exportGroupKey = equipment.exportGroupKey ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ExportGroupKey".Translate(), ref exportGroupKey);
                if (exportGroupKey != (equipment.exportGroupKey ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.exportGroupKey = exportGroupKey, refreshPreview: true, refreshRenderTree: false);
                }

                string parentThingDefName = equipment.parentThingDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ParentThingDefName".Translate(), ref parentThingDefName);
                if (parentThingDefName != (equipment.parentThingDefName ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.parentThingDefName = string.IsNullOrWhiteSpace(parentThingDefName) ? CharacterEquipmentDef.DefaultParentThingDefName : parentThingDefName, refreshPreview: true, refreshRenderTree: false);
                }

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
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_SourceNote".Translate(), ref sourceNote);
                if (sourceNote != (equipment.sourceNote ?? string.Empty))
                {
                    MutateWithUndo(() => equipment.sourceNote = sourceNote, refreshPreview: true, refreshRenderTree: false);
                }

                string tagsText = string.Join(", ", equipment.tags ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Attr_Tags".Translate(), ref tagsText);
                string normalizedTagsText = string.Join(", ", equipment.tags ?? new List<string>());
                if (tagsText != normalizedTagsText)
                {
                    MutateWithUndo(() => equipment.tags = ParseCommaSeparatedList(tagsText).ToList(), refreshPreview: true, refreshRenderTree: false);
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
                            MutateWithUndo(() =>
                            {
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
                            }, refreshPreview: true, refreshRenderTree: false);
                        }
                    }
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_Definition".Translate(), "EquipmentDefinition"))
            {
                string worldTexPath = equipment.worldTexPath ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_Equip_WorldTexPath".Translate(), ref worldTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.worldTexPath ?? string.Empty, path =>
                    {
                        MutateEquipmentWithUndo(() => equipment.worldTexPath = path ?? string.Empty, refreshRenderTree: false);
                    }))))
                {
                    MutateEquipmentWithUndo(() => equipment.worldTexPath = worldTexPath, refreshRenderTree: false);
                }

                string wornTexPath = equipment.wornTexPath ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_Equip_WornTexPath".Translate(), ref wornTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.wornTexPath ?? string.Empty, path =>
                    {
                        MutateEquipmentWithUndo(() => equipment.wornTexPath = path ?? string.Empty, refreshRenderTree: false);
                    }))))
                {
                    MutateEquipmentWithUndo(() => equipment.wornTexPath = wornTexPath, refreshRenderTree: false);
                }

                string equipmentMaskTexPath = equipment.maskTexPath ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_Equip_ApparelMask".Translate(), ref equipmentMaskTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.maskTexPath ?? string.Empty, path =>
                    {
                        MutateEquipmentWithUndo(() => equipment.maskTexPath = path ?? string.Empty, refreshRenderTree: false);
                    }))))
                {
                    MutateEquipmentWithUndo(() => equipment.maskTexPath = equipmentMaskTexPath, refreshRenderTree: false);
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
                    MutateEquipmentWithUndo(() => equipment.useWornGraphicMask = useWornGraphicMask, refreshRenderTree: false);
                }

                string thingCategoriesText = string.Join(", ", equipment.thingCategories ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ThingCategories".Translate(), ref thingCategoriesText);
                string normalizedThingCategoriesText = string.Join(", ", equipment.thingCategories ?? new List<string>());
                if (thingCategoriesText != normalizedThingCategoriesText)
                {
                    MutateEquipmentWithUndo(() => equipment.thingCategories = ParseCommaSeparatedList(thingCategoriesText).ToList(), refreshRenderTree: false);
                }

                string bodyPartGroupsText = string.Join(", ", equipment.bodyPartGroups ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_BodyPartGroups".Translate(), ref bodyPartGroupsText);
                string normalizedBodyPartGroupsText = string.Join(", ", equipment.bodyPartGroups ?? new List<string>());
                if (bodyPartGroupsText != normalizedBodyPartGroupsText)
                {
                    MutateEquipmentWithUndo(() => equipment.bodyPartGroups = ParseCommaSeparatedList(bodyPartGroupsText).ToList(), refreshRenderTree: false);
                }

                string apparelLayersText = string.Join(", ", equipment.apparelLayers ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ApparelLayers".Translate(), ref apparelLayersText);
                string normalizedApparelLayersText = string.Join(", ", equipment.apparelLayers ?? new List<string>());
                if (apparelLayersText != normalizedApparelLayersText)
                {
                    MutateEquipmentWithUndo(() => equipment.apparelLayers = ParseCommaSeparatedList(apparelLayersText).ToList(), refreshRenderTree: false);
                }

                string apparelTagsText = string.Join(", ", equipment.apparelTags ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_ApparelTags".Translate(), ref apparelTagsText);
                string normalizedApparelTagsText = string.Join(", ", equipment.apparelTags ?? new List<string>());
                if (apparelTagsText != normalizedApparelTagsText)
                {
                    MutateEquipmentWithUndo(() => equipment.apparelTags = ParseCommaSeparatedList(apparelTagsText).ToList(), refreshRenderTree: false);
                }

                bool allowCrafting = equipment.allowCrafting;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_AllowCrafting".Translate(), ref allowCrafting);
                if (allowCrafting != equipment.allowCrafting)
                {
                    MutateEquipmentWithUndo(() => equipment.allowCrafting = allowCrafting, refreshRenderTree: false);
                }

                string recipeDefName = equipment.recipeDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_RecipeDefName".Translate(), ref recipeDefName);
                if (recipeDefName != (equipment.recipeDefName ?? string.Empty))
                {
                    MutateEquipmentWithUndo(() => equipment.recipeDefName = recipeDefName, refreshRenderTree: false);
                }

                string recipeWorkbenchDefName = equipment.recipeWorkbenchDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_RecipeWorkbenchDefName".Translate(), ref recipeWorkbenchDefName);
                if (recipeWorkbenchDefName != (equipment.recipeWorkbenchDefName ?? string.Empty))
                {
                    MutateEquipmentWithUndo(() => equipment.recipeWorkbenchDefName = recipeWorkbenchDefName, refreshRenderTree: false);
                }

                float recipeWorkAmount = equipment.recipeWorkAmount;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_RecipeWorkAmount".Translate(), ref recipeWorkAmount, 1f, 20000f, "F0");
                if (Math.Abs(recipeWorkAmount - equipment.recipeWorkAmount) > 0.0001f)
                {
                    MutateEquipmentWithUndo(() => equipment.recipeWorkAmount = recipeWorkAmount, refreshRenderTree: false);
                }

                int recipeProductCount = equipment.recipeProductCount;
                UIHelper.DrawNumericField(ref y, width, "CS_Studio_Equip_RecipeProductCount".Translate(), ref recipeProductCount, 1, 999);
                if (recipeProductCount != equipment.recipeProductCount)
                {
                    MutateEquipmentWithUndo(() => equipment.recipeProductCount = recipeProductCount, refreshRenderTree: false);
                }

                string recipeIngredientsText = string.Join(", ", (equipment.recipeIngredients ?? new List<CharacterEquipmentCostEntry>()).Select(entry => $"{entry.thingDefName}:{entry.count}"));
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_RecipeIngredients".Translate(), ref recipeIngredientsText);
                string normalizedRecipeIngredientsText = string.Join(", ", (equipment.recipeIngredients ?? new List<CharacterEquipmentCostEntry>()).Select(entry => $"{entry.thingDefName}:{entry.count}"));
                if (recipeIngredientsText != normalizedRecipeIngredientsText)
                {
                    MutateEquipmentWithUndo(() => equipment.recipeIngredients = ParseEquipmentCostEntries(recipeIngredientsText).ToList(), refreshRenderTree: false);
                }

                bool allowTrading = equipment.allowTrading;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_AllowTrading".Translate(), ref allowTrading);
                if (allowTrading != equipment.allowTrading)
                {
                    MutateEquipmentWithUndo(() => equipment.allowTrading = allowTrading, refreshRenderTree: false);
                }

                float marketValue = equipment.marketValue;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_MarketValue".Translate(), ref marketValue, 0.01f, 100000f, "F2");
                if (Math.Abs(marketValue - equipment.marketValue) > 0.0001f)
                {
                    MutateEquipmentWithUndo(() => equipment.marketValue = marketValue, refreshRenderTree: false);
                }

                string tradeTagsText = string.Join(", ", equipment.tradeTags ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_TradeTags".Translate(), ref tradeTagsText);
                string normalizedTradeTagsText = string.Join(", ", equipment.tradeTags ?? new List<string>());
                if (tradeTagsText != normalizedTradeTagsText)
                {
                    MutateEquipmentWithUndo(() => equipment.tradeTags = ParseCommaSeparatedList(tradeTagsText).ToList(), refreshRenderTree: false);
                }

                if (equipment.itemType == CharacterStudio.Core.EquipmentType.WeaponMelee || equipment.itemType == CharacterStudio.Core.EquipmentType.WeaponRanged)
                {
                    string weaponTagsText = string.Join(", ", equipment.weaponTags ?? new List<string>());
                    UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_WeaponTags".Translate(), ref weaponTagsText);
                    string normalizedWeaponTagsText = string.Join(", ", equipment.weaponTags ?? new List<string>());
                    if (weaponTagsText != normalizedWeaponTagsText)
                    {
                        MutateEquipmentWithUndo(() => equipment.weaponTags = ParseCommaSeparatedList(weaponTagsText).ToList(), refreshRenderTree: false);
                    }

                    string weaponClassesText = string.Join(", ", equipment.weaponClasses ?? new List<string>());
                    UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_WeaponClasses".Translate(), ref weaponClassesText);
                    string normalizedWeaponClassesText = string.Join(", ", equipment.weaponClasses ?? new List<string>());
                    if (weaponClassesText != normalizedWeaponClassesText)
                    {
                        MutateEquipmentWithUndo(() => equipment.weaponClasses = ParseCommaSeparatedList(weaponClassesText).ToList(), refreshRenderTree: false);
                    }
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

            string triggerAbilityDefName = overrideData.triggerAbilityDefName ?? string.Empty;
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_Override_TriggerAbilityDefName".Translate(), ref triggerAbilityDefName);
            if (triggerAbilityDefName != (overrideData.triggerAbilityDefName ?? string.Empty))
            {
                CaptureUndoSnapshot();
                overrideData.triggerAbilityDefName = triggerAbilityDefName;
                markEquipmentDirty(false);
            }

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
    }
}
