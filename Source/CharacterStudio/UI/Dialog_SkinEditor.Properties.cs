using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 属性面板
        // ─────────────────────────────────────────────

        private float GetPropertiesContentTop(Rect rect)
        {
            return rect.y + Margin + ButtonHeight + Margin;
        }

        private void DrawPropertiesPanel(Rect rect)
        {
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
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 72f, titleRect.height), "CS_Studio_Panel_Properties".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            bool DrawHeaderButton(Rect buttonRect, string label, string tooltip, Action onClick)
            {
                Widgets.DrawBoxSolid(buttonRect, UIHelper.PanelFillSoftColor);
                Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), new Color(1f, 1f, 1f, 0.05f));
                GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
                Widgets.DrawBox(buttonRect, 1);
                GUI.color = Color.white;

                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = UIHelper.HeaderColor;
                Widgets.Label(buttonRect, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = prevFont;

                TooltipHandler.TipRegion(buttonRect, tooltip);
                if (Widgets.ButtonInvisible(buttonRect))
                {
                    onClick();
                    return true;
                }

                return false;
            }

            float expandBtnWidth = 24f;
            DrawHeaderButton(new Rect(rect.x + rect.width - Margin - expandBtnWidth * 2 - 4, rect.y + Margin + 1f, expandBtnWidth, 24f), "+", "CS_Studio_Tip_ExpandAll".Translate(), () =>
            {
                collapsedSections.Clear();
            });

            DrawHeaderButton(new Rect(rect.x + rect.width - Margin - expandBtnWidth, rect.y + Margin + 1f, expandBtnWidth, 24f), "-", "CS_Studio_Tip_CollapseAll".Translate(), () =>
            {
                var allSections = new[] { "Base", "Transform", "EastOffset", "NorthOffset", "Misc", "Variants", "Animation", "HideVanilla", "NodeInfo", "NodeActions", "NodeRuntime" };
                foreach (var s in allSections)
                {
                    collapsedSections.Add(s);
                }
            });

            if (!string.IsNullOrEmpty(selectedNodePath) && cachedRootSnapshot != null)
            {
                DrawNodeProperties(rect);
                return;
            }

            if (selectedBaseSlotType != null)
            {
                DrawBaseAppearanceProperties(rect, selectedBaseSlotType.Value);
                return;
            }

            SanitizeEquipmentSelection();
            if (currentTab == EditorTab.Equipment &&
                selectedEquipmentIndex >= 0 &&
                selectedEquipmentIndex < (workingSkin.equipments?.Count ?? 0))
            {
                DrawEquipmentProperties(rect);
                return;
            }

            if (selectedLayerIndex < 0 || selectedLayerIndex >= workingSkin.layers.Count)
            {
                Rect hintRect = new Rect(rect.x + Margin, rect.y + 60, rect.width - Margin * 2, 40);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(hintRect, "CS_Studio_Msg_SelectEditorTarget".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            var layer = workingSkin.layers[selectedLayerIndex];

            float propsY = GetPropertiesContentTop(rect);
            float propsHeight = rect.height - propsY + rect.y - Margin;
            Rect propsRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2, propsHeight);
            Widgets.DrawBoxSolid(propsRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(propsRect, 1);
            GUI.color = Color.white;
            Rect viewRect = new Rect(0, 0, propsRect.width - 20, 750);

            Widgets.BeginScrollView(propsRect.ContractedBy(2f), ref propsScrollPos, viewRect);

            float y = 0;
            float width = viewRect.width;

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Base".Translate(), "Base"))
            {
                string oldName = layer.layerName;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Prop_LayerName".Translate(), ref layer.layerName);
                if (oldName != layer.layerName)
                {
                    CaptureUndoSnapshot();
                    isDirty = true;
                }

                UIHelper.DrawPropertyFieldWithButton(ref y, width, "CS_Studio_Prop_TexturePath".Translate(),
                    layer.texPath, () => OnSelectTexture(layer));

                var anchorOptions = new[]
                {
                    "Head", "Body", "Hair", "Beard",
                    "Eyes", "Brow", "Mouth", "Nose", "Ear", "Jaw",
                    "FaceTattoo", "BodyTattoo", "Apparel", "Headgear"
                };

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Prop_AnchorPoint".Translate(), layer.anchorTag,
                    anchorOptions,
                    tag =>
                    {
                        string key = $"CS_Studio_Anchor_{tag}";
                        return key.CanTranslate() ? key.Translate() : tag;
                    },
                    val =>
                    {
                        layer.anchorTag = val;
                        ApplyToOtherSelectedLayers(l => l.anchorTag = val);
                        isDirty = true;
                        RefreshPreview();
                    });
            }

            bool isSouthActive = previewRotation == Rot4.South;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Transform".Translate(), "Transform", isSouthActive))
            {
                Vector3 editableOffset = GetEditableLayerOffsetForPreview(layer);

                float ox = editableOffset.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref ox, -1f, 1f);
                if (Math.Abs(ox - editableOffset.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    editableOffset.x = ox;
                    SetEditableLayerOffsetForPreview(layer, editableOffset);
                    ApplyToOtherSelectedLayers(l => { var v = GetEditableLayerOffsetForPreview(l); v.x = ox; SetEditableLayerOffsetForPreview(l, v); });
                    isDirty = true;
                    RefreshPreview();
                }

                float oy = editableOffset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetYHeight".Translate(), ref oy, -1f, 1f);
                if (Math.Abs(oy - editableOffset.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    editableOffset.y = oy;
                    SetEditableLayerOffsetForPreview(layer, editableOffset);
                    ApplyToOtherSelectedLayers(l => { var v = GetEditableLayerOffsetForPreview(l); v.y = oy; SetEditableLayerOffsetForPreview(l, v); });
                    isDirty = true;
                    RefreshPreview();
                }

                float oz = editableOffset.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref oz, -1f, 1f);
                if (Math.Abs(oz - editableOffset.z) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    editableOffset.z = oz;
                    SetEditableLayerOffsetForPreview(layer, editableOffset);
                    ApplyToOtherSelectedLayers(l => { var v = GetEditableLayerOffsetForPreview(l); v.z = oz; SetEditableLayerOffsetForPreview(l, v); });
                    isDirty = true;
                    RefreshPreview();
                }
            }

            bool isEastActive = previewRotation == Rot4.East || previewRotation == Rot4.West;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_EastOffset".Translate(), "EastOffset", isEastActive))
            {
                float ex = layer.offsetEast.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref ex, -1f, 1f);
                if (Math.Abs(ex - layer.offsetEast.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetEast.x = ex;
                    ApplyToOtherSelectedLayers(l => l.offsetEast.x = ex);
                    isDirty = true;
                    RefreshPreview();
                }

                float ey = layer.offsetEast.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref ey, -1f, 1f);
                if (Math.Abs(ey - layer.offsetEast.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetEast.y = ey;
                    ApplyToOtherSelectedLayers(l => l.offsetEast.y = ey);
                    isDirty = true;
                    RefreshPreview();
                }

                float ez = layer.offsetEast.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref ez, -1f, 1f);
                if (Math.Abs(ez - layer.offsetEast.z) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetEast.z = ez;
                    ApplyToOtherSelectedLayers(l => l.offsetEast.z = ez);
                    isDirty = true;
                    RefreshPreview();
                }
            }

            bool isNorthActive = previewRotation == Rot4.North;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_NorthOffset".Translate(), "NorthOffset", isNorthActive))
            {
                float nx = layer.offsetNorth.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref nx, -1f, 1f);
                if (Math.Abs(nx - layer.offsetNorth.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetNorth.x = nx;
                    ApplyToOtherSelectedLayers(l => l.offsetNorth.x = nx);
                    isDirty = true;
                    RefreshPreview();
                }

                float ny = layer.offsetNorth.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref ny, -1f, 1f);
                if (Math.Abs(ny - layer.offsetNorth.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetNorth.y = ny;
                    ApplyToOtherSelectedLayers(l => l.offsetNorth.y = ny);
                    isDirty = true;
                    RefreshPreview();
                }

                float nz = layer.offsetNorth.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref nz, -1f, 1f);
                if (Math.Abs(nz - layer.offsetNorth.z) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetNorth.z = nz;
                    ApplyToOtherSelectedLayers(l => l.offsetNorth.z = nz);
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Misc".Translate(), "Misc"))
            {
                DrawPropertyHint(ref y, width, "CS_Studio_Transform_GlobalHint".Translate());

                float scaleX = layer.scale.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_GlobalScaleX".Translate(), ref scaleX, 0.1f, 3f, "F3");
                if (Math.Abs(scaleX - layer.scale.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.scale.x = scaleX;
                    ApplyToOtherSelectedLayers(l => l.scale.x = scaleX);
                    isDirty = true;
                    RefreshPreview();
                }

                float scaleY = layer.scale.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_GlobalScaleY".Translate(), ref scaleY, 0.1f, 3f, "F3");
                if (Math.Abs(scaleY - layer.scale.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.scale.y = scaleY;
                    ApplyToOtherSelectedLayers(l => l.scale.y = scaleY);
                    isDirty = true;
                    RefreshPreview();
                }

                float baseRotation = layer.rotation;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_BaseRotation".Translate(), ref baseRotation, -180f, 180f, "F0");
                if (Math.Abs(baseRotation - layer.rotation) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.rotation = baseRotation;
                    ApplyToOtherSelectedLayers(l => l.rotation = baseRotation);
                    isDirty = true;
                    RefreshPreview();
                }

                float currentScaleX = GetEditableLayerScaleForPreview(layer).x;
                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Transform_PreviewScaleX".Translate(), currentScaleX.ToString("F3"));

                float currentScaleY = GetEditableLayerScaleForPreview(layer).y;
                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Transform_PreviewScaleY".Translate(), currentScaleY.ToString("F3"));

                float currentRotation = GetEditableLayerRotationForPreview(layer);
                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Transform_PreviewRotation".Translate(), currentRotation.ToString("F0"));

                float newDrawOrder = layer.drawOrder;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_DrawOrder".Translate(), ref newDrawOrder, -10f, 100f, "F0");
                if (Mathf.Abs(newDrawOrder - layer.drawOrder) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.drawOrder = newDrawOrder;
                    ApplyToOtherSelectedLayers(l => l.drawOrder = newDrawOrder);
                    isDirty = true;
                    RefreshPreview();
                }

                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Transform_FinalDrawOrder".Translate(), Mathf.Clamp(layer.drawOrder, -10f, 100f).ToString("F0"));

                bool flip = layer.flipHorizontal;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_FlipHorizontal".Translate(), ref flip);
                if (flip != layer.flipHorizontal)
                {
                    CaptureUndoSnapshot();
                    layer.flipHorizontal = flip;
                    ApplyToOtherSelectedLayers(l => l.flipHorizontal = flip);
                    isDirty = true;
                    RefreshPreview();
                }
            }

            bool isEastScaleActive = previewRotation == Rot4.East || previewRotation == Rot4.West;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Transform_EastRotation".Translate(), "EastScaleRotation", isEastScaleActive))
            {
                DrawPropertyHint(ref y, width, "CS_Studio_Transform_EastRotationHint".Translate());

                float eastRotationOffset = layer.rotationEastOffset;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_RotationOffset".Translate(), ref eastRotationOffset, -180f, 180f, "F0");
                if (Math.Abs(eastRotationOffset - layer.rotationEastOffset) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.rotationEastOffset = eastRotationOffset;
                    ApplyToOtherSelectedLayers(l => l.rotationEastOffset = eastRotationOffset);
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Rendering".Translate(), "Rendering"))
            {

                string[] workers = { "Default", "FaceComponent" };
                string currentWorker = layer.workerClass == typeof(CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent) ? "FaceComponent" : "Default";

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Prop_Worker".Translate(), currentWorker, workers,
                    GetWorkerLabel,
                    val =>
                    {
                        var newWorker = val == "FaceComponent"
                            ? typeof(CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent)
                            : null;
                        layer.workerClass = newWorker;
                        ApplyToOtherSelectedLayers(l => l.workerClass = newWorker);
                        isDirty = true;
                        RefreshPreview();
                    });

#pragma warning disable CS0618
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Prop_ColorType".Translate(), layer.colorType,
                    (LayerColorType[])Enum.GetValues(typeof(LayerColorType)),
                    type => $"CS_Studio_ColorType_{type}".Translate(),
                    val =>
                    {
                        layer.colorType = val;
                        ApplyToOtherSelectedLayers(l => l.colorType = val);
                        isDirty = true;
                        RefreshPreview();
                    });

                if (layer.colorType == LayerColorType.Custom)
                {
#pragma warning restore CS0618
                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_Prop_CustomColor".Translate(), layer.customColor,
                        col =>
                        {
                            layer.customColor = col;
                            ApplyToOtherSelectedLayers(l => l.customColor = col);
                            isDirty = true;
                            RefreshPreview();
                        });

                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_BaseSlot_SecondColorMask".Translate(), layer.customColorTwo,
                        col =>
                        {
                            layer.customColorTwo = col;
                            ApplyToOtherSelectedLayers(l => l.customColorTwo = col);
                            isDirty = true;
                            RefreshPreview();
                        });
                }

            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_VariantsExpression".Translate(), "Variants"))
            {
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Variant_LayerRole".Translate(), layer.role,
                    (LayerRole[])Enum.GetValues(typeof(LayerRole)),
                    option => option.ToString(),
                    val =>
                    {
                        CaptureUndoSnapshot();
                        layer.role = val;
                        ApplyToOtherSelectedLayers(l => l.role = val);
                        isDirty = true;
                        RefreshPreview();
                    });

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Variant_Logic".Translate(), layer.variantLogic,
                    (LayerVariantLogic[])Enum.GetValues(typeof(LayerVariantLogic)),
                    option => option.ToString(),
                    val =>
                    {
                        CaptureUndoSnapshot();
                        layer.variantLogic = val;
                        ApplyToOtherSelectedLayers(l => l.variantLogic = val);
                        isDirty = true;
                        RefreshPreview();
                    });

                string variantBaseName = layer.variantBaseName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Variant_BaseName".Translate(), ref variantBaseName);
                if (variantBaseName != (layer.variantBaseName ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    layer.variantBaseName = variantBaseName;
                    ApplyToOtherSelectedLayers(l => l.variantBaseName = variantBaseName);
                    isDirty = true;
                    RefreshPreview();
                }

                bool useDirectionalSuffix = layer.useDirectionalSuffix;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseDirectionalSuffix".Translate(), ref useDirectionalSuffix);
                if (useDirectionalSuffix != layer.useDirectionalSuffix)
                {
                    CaptureUndoSnapshot();
                    layer.useDirectionalSuffix = useDirectionalSuffix;
                    ApplyToOtherSelectedLayers(l => l.useDirectionalSuffix = useDirectionalSuffix);
                    isDirty = true;
                    RefreshPreview();
                }

                bool useExpressionSuffix = layer.useExpressionSuffix;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseExpressionSuffix".Translate(), ref useExpressionSuffix);
                if (useExpressionSuffix != layer.useExpressionSuffix)
                {
                    CaptureUndoSnapshot();
                    layer.useExpressionSuffix = useExpressionSuffix;
                    ApplyToOtherSelectedLayers(l => l.useExpressionSuffix = useExpressionSuffix);
                    isDirty = true;
                    RefreshPreview();
                }

                bool useEyeDirectionSuffix = layer.useEyeDirectionSuffix;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseEyeDirectionSuffix".Translate(), ref useEyeDirectionSuffix);
                if (useEyeDirectionSuffix != layer.useEyeDirectionSuffix)
                {
                    CaptureUndoSnapshot();
                    layer.useEyeDirectionSuffix = useEyeDirectionSuffix;
                    ApplyToOtherSelectedLayers(l => l.useEyeDirectionSuffix = useEyeDirectionSuffix);
                    isDirty = true;
                    RefreshPreview();
                }

                bool useBlinkSuffix = layer.useBlinkSuffix;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseBlinkSuffix".Translate(), ref useBlinkSuffix);
                if (useBlinkSuffix != layer.useBlinkSuffix)
                {
                    CaptureUndoSnapshot();
                    layer.useBlinkSuffix = useBlinkSuffix;
                    ApplyToOtherSelectedLayers(l => l.useBlinkSuffix = useBlinkSuffix);
                    isDirty = true;
                    RefreshPreview();
                }

                bool useFrameSequence = layer.useFrameSequence;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseFrameSequence".Translate(), ref useFrameSequence);
                if (useFrameSequence != layer.useFrameSequence)
                {
                    CaptureUndoSnapshot();
                    layer.useFrameSequence = useFrameSequence;
                    ApplyToOtherSelectedLayers(l => l.useFrameSequence = useFrameSequence);
                    isDirty = true;
                    RefreshPreview();
                }

                bool hideWhenMissingVariant = layer.hideWhenMissingVariant;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_HideWhenMissing".Translate(), ref hideWhenMissingVariant);
                if (hideWhenMissingVariant != layer.hideWhenMissingVariant)
                {
                    CaptureUndoSnapshot();
                    layer.hideWhenMissingVariant = hideWhenMissingVariant;
                    ApplyToOtherSelectedLayers(l => l.hideWhenMissingVariant = hideWhenMissingVariant);
                    isDirty = true;
                    RefreshPreview();
                }

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Variant_EyeRenderMode".Translate(), layer.eyeRenderMode,
                    (EyeRenderMode[])Enum.GetValues(typeof(EyeRenderMode)),
                    option => option.ToString(),
                    val =>
                    {
                        CaptureUndoSnapshot();
                        layer.eyeRenderMode = val;
                        ApplyToOtherSelectedLayers(l => l.eyeRenderMode = val);
                        isDirty = true;
                        RefreshPreview();
                    });

                if (layer.eyeRenderMode == EyeRenderMode.UvOffset)
                {
                    float eyeUvMoveRange = layer.eyeUvMoveRange;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Variant_EyeUvMoveRange".Translate(), ref eyeUvMoveRange, 0f, 0.2f, "F3");
                    if (Math.Abs(eyeUvMoveRange - layer.eyeUvMoveRange) > 0.0001f)
                    {
                        CaptureUndoSnapshot();
                        layer.eyeUvMoveRange = eyeUvMoveRange;
                        ApplyToOtherSelectedLayers(l => l.eyeUvMoveRange = eyeUvMoveRange);
                        isDirty = true;
                        RefreshPreview();
                    }
                }

                string visibleExpressionsText = string.Join(", ", layer.visibleExpressions ?? Array.Empty<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Variant_VisibleExpressions".Translate(), ref visibleExpressionsText);
                string normalizedVisibleExpressionsText = string.Join(", ", layer.visibleExpressions ?? Array.Empty<string>());
                if (visibleExpressionsText != normalizedVisibleExpressionsText)
                {
                    CaptureUndoSnapshot();
                    string[] parsed = ParseCommaSeparatedList(visibleExpressionsText);
                    layer.visibleExpressions = parsed;
                    ApplyToOtherSelectedLayers(l => l.visibleExpressions = (string[])parsed.Clone());
                    isDirty = true;
                    RefreshPreview();
                }

                string hiddenExpressionsText = string.Join(", ", layer.hiddenExpressions ?? Array.Empty<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Variant_HiddenExpressions".Translate(), ref hiddenExpressionsText);
                string normalizedHiddenExpressionsText = string.Join(", ", layer.hiddenExpressions ?? Array.Empty<string>());
                if (hiddenExpressionsText != normalizedHiddenExpressionsText)
                {
                    CaptureUndoSnapshot();
                    string[] parsed = ParseCommaSeparatedList(hiddenExpressionsText);
                    layer.hiddenExpressions = parsed;
                    ApplyToOtherSelectedLayers(l => l.hiddenExpressions = (string[])parsed.Clone());
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Animation".Translate(), "Animation"))
            {
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Anim_Type".Translate(), layer.animationType,
                    (LayerAnimationType[])Enum.GetValues(typeof(LayerAnimationType)),
                    type => $"CS_Studio_Anim_{type}".Translate(),
                    val =>
                    {
                        layer.animationType = val;
                        ApplyToOtherSelectedLayers(l => l.animationType = val);
                        isDirty = true;
                        RefreshPreview();
                    });

                if (layer.animationType != LayerAnimationType.None)
                {
                    float freq = layer.animFrequency;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_Frequency".Translate(), ref freq, 0.1f, 5f);
                    if (Math.Abs(freq - layer.animFrequency) > 0.0001f)
                    {
                        CaptureUndoSnapshot();
                        layer.animFrequency = freq;
                        ApplyToOtherSelectedLayers(l => l.animFrequency = freq);
                        isDirty = true;
                        RefreshPreview();
                    }

                    float amp = layer.animAmplitude;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_Amplitude".Translate(), ref amp, 1f, 45f);
                    if (Math.Abs(amp - layer.animAmplitude) > 0.0001f)
                    {
                        CaptureUndoSnapshot();
                        layer.animAmplitude = amp;
                        ApplyToOtherSelectedLayers(l => l.animAmplitude = amp);
                        isDirty = true;
                        RefreshPreview();
                    }

                    if (layer.animationType == LayerAnimationType.Twitch)
                    {
                        float speed = layer.animSpeed;
                        UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_Speed".Translate(), ref speed, 0.1f, 3f);
                        if (Math.Abs(speed - layer.animSpeed) > 0.0001f)
                        {
                            CaptureUndoSnapshot();
                            layer.animSpeed = speed;
                            ApplyToOtherSelectedLayers(l => l.animSpeed = speed);
                            isDirty = true;
                            RefreshPreview();
                        }
                    }

                    float phase = layer.animPhaseOffset;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_PhaseOffset".Translate(), ref phase, 0f, 1f);
                    if (Math.Abs(phase - layer.animPhaseOffset) > 0.0001f)
                    {
                        CaptureUndoSnapshot();
                        layer.animPhaseOffset = phase;
                        ApplyToOtherSelectedLayers(l => l.animPhaseOffset = phase);
                        isDirty = true;
                        RefreshPreview();
                    }

                    bool affectsOffset = layer.animAffectsOffset;
                    UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Anim_AffectsOffset".Translate(), ref affectsOffset);
                    if (affectsOffset != layer.animAffectsOffset)
                    {
                        CaptureUndoSnapshot();
                        layer.animAffectsOffset = affectsOffset;
                        ApplyToOtherSelectedLayers(l => l.animAffectsOffset = affectsOffset);
                        isDirty = true;
                        RefreshPreview();
                    }

                    if (layer.animAffectsOffset)
                    {
                        float offsetAmp = layer.animOffsetAmplitude;
                        UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_OffsetAmplitude".Translate(), ref offsetAmp, 0.001f, 0.1f, "F3");
                        if (Math.Abs(offsetAmp - layer.animOffsetAmplitude) > 0.0001f)
                        {
                            CaptureUndoSnapshot();
                            layer.animOffsetAmplitude = offsetAmp;
                            ApplyToOtherSelectedLayers(l => l.animOffsetAmplitude = offsetAmp);
                            isDirty = true;
                            RefreshPreview();
                        }
                    }

                    // Spin 专属：枢轴偏移
                    if (layer.animationType == LayerAnimationType.Spin)
                    {
                        float pivotX = layer.animPivotOffset.x;
                        float pivotY = layer.animPivotOffset.y;
                        UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_PivotX".Translate(), ref pivotX, -1f, 1f, "F3");
                        UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_PivotY".Translate(), ref pivotY, -1f, 1f, "F3");
                        var newPivot = new Vector2(pivotX, pivotY);
                        if (newPivot != layer.animPivotOffset)
                        {
                            CaptureUndoSnapshot();
                            layer.animPivotOffset = newPivot;
                            ApplyToOtherSelectedLayers(l => l.animPivotOffset = newPivot);
                            isDirty = true;
                            RefreshPreview();
                        }
                    }
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_HideVanilla".Translate(), "HideVanilla"))
            {
                if (workingSkin.hiddenPaths != null && workingSkin.hiddenPaths.Count > 0)
                {
                    Widgets.Label(new Rect(0, y, propsRect.width - 20, 22), "CS_Studio_Hide_HiddenPaths".Translate());
                    y += 24;
                    foreach (var path in workingSkin.hiddenPaths.ToList())
                    {
                        Rect removeRect = new Rect(propsRect.width - 48f, y - 1f, 32f, 22f);
                        Rect pathRect = new Rect(0, y, propsRect.width - 56f, 22);
                        Widgets.Label(pathRect, $"  • {path}");
                        if (UIHelper.DrawDangerButton(removeRect, tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            workingSkin.hiddenPaths.Remove(path);
                            isDirty = true;
                            RefreshPreview();
                            RefreshRenderTree();
                        }))
                        {
                        }
                        y += 24;
                    }
                }
                else
                {
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(0, y, propsRect.width - 20, 22), "CS_Studio_Hide_NoHiddenPaths".Translate());
                    GUI.color = Color.white;
                    y += 24;
                }

#pragma warning disable CS0618
                if (workingSkin.hiddenTags != null && workingSkin.hiddenTags.Count > 0)
                {
                    Widgets.Label(new Rect(0, y, propsRect.width - 20, 22), "CS_Studio_Hide_HiddenTagsCompat".Translate());
                    y += 24;
                    foreach (var tag in workingSkin.hiddenTags.ToList())
                    {
                        Rect removeRect = new Rect(propsRect.width - 48f, y - 1f, 32f, 22f);
                        Rect tagRect = new Rect(0, y, propsRect.width - 56f, 22);
                        Widgets.Label(tagRect, $"  • {tag}");
                        if (UIHelper.DrawDangerButton(removeRect, tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            workingSkin.hiddenTags.Remove(tag);
                            isDirty = true;
                            RefreshPreview();
                            RefreshRenderTree();
                        }))
                        {
                        }
                        y += 24;
                    }
                }
#pragma warning restore CS0618

                if (Widgets.ButtonText(new Rect(0, y, propsRect.width - 20, 24), "CS_Studio_Btn_AddHidden".Translate()))
                {
                    ShowHiddenTagsMenu();
                }
                y += 28;
            }

            Widgets.EndScrollView();
        }

        private void DrawBaseAppearanceProperties(Rect rect, BaseAppearanceSlotType slotType)
        {
            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            var slot = workingSkin.baseAppearance.GetSlot(slotType);

            float propsY = GetPropertiesContentTop(rect);
            float propsHeight = rect.height - propsY + rect.y - Margin;
            Rect propsRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2, propsHeight);
            Rect viewRect = new Rect(0, 0, propsRect.width - 16, 760);

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

                if (UIHelper.DrawBrowseButton(
                    new Rect(rowRect.x + actualLabelWidth + fieldWidth + spacing, rowRect.y, buttonWidth, 24f),
                    browseAction,
                    "…"))
                {
                }

                rowY += UIHelper.RowHeight;
                return changed;
            }

            void MarkBaseSlotDirty(bool refreshRenderTree = true)
            {
                isDirty = true;
                RefreshPreview();
                if (refreshRenderTree)
                {
                    RefreshRenderTree();
                }
            }

            RenderNodeSnapshot? FindSuggestedBaseSlotSnapshot(BaseAppearanceSlotType targetSlotType)
            {
                if (cachedRootSnapshot == null)
                {
                    return null;
                }

                var snapshots = new List<RenderNodeSnapshot>();
                CollectSnapshots(cachedRootSnapshot, snapshots);

                string primaryTag = targetSlotType.ToString();
                foreach (var candidate in snapshots)
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (string.Equals(candidate.tagDefName, primaryTag, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }

                foreach (var candidate in snapshots)
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    if ((!string.IsNullOrEmpty(candidate.tagDefName) && candidate.tagDefName.IndexOf(primaryTag, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrEmpty(candidate.debugLabel) && candidate.debugLabel.IndexOf(primaryTag, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return candidate;
                    }
                }

                return null;
            }

            bool TryAutoNormalizeBaseSlotTexture(BaseAppearanceSlotConfig targetSlot, BaseAppearanceSlotType targetSlotType, string texturePath)
            {
                if (string.IsNullOrWhiteSpace(texturePath))
                {
                    return false;
                }

                bool changed = false;
                var snapshot = FindSuggestedBaseSlotSnapshot(targetSlotType);
                if (snapshot != null)
                {
                    Vector2 snapshotScale = Vector2.one;
                    if (snapshot.runtimeDataValid)
                    {
                        snapshotScale = new Vector2(snapshot.runtimeScale.x, snapshot.runtimeScale.z);
                        if (Mathf.Abs(snapshotScale.x - 1f) < 0.05f && Mathf.Abs(snapshotScale.y - 1f) < 0.05f &&
                            snapshot.graphicDrawSize != Vector2.zero && snapshot.graphicDrawSize != Vector2.one &&
                            snapshot.graphicDrawSize.x < 5f && snapshot.graphicDrawSize.y < 5f)
                        {
                            snapshotScale = snapshot.graphicDrawSize;
                        }

                        if (targetSlot.offset != snapshot.runtimeOffset)
                        {
                            targetSlot.offset = snapshot.runtimeOffset;
                            changed = true;
                        }
                        if (targetSlot.offsetEast != snapshot.runtimeOffsetEast)
                        {
                            targetSlot.offsetEast = snapshot.runtimeOffsetEast;
                            changed = true;
                        }
                        if (targetSlot.offsetNorth != snapshot.runtimeOffsetNorth)
                        {
                            targetSlot.offsetNorth = snapshot.runtimeOffsetNorth;
                            changed = true;
                        }
                    }

                    if (snapshotScale.x > 0.001f && snapshotScale.y > 0.001f && targetSlot.scale != snapshotScale)
                    {
                        targetSlot.scale = snapshotScale;
                        changed = true;
                    }
                }

                if ((targetSlotType == BaseAppearanceSlotType.Body || targetSlotType == BaseAppearanceSlotType.Head) &&
                    System.IO.Path.IsPathRooted(texturePath))
                {
                    Vector2Int dimensions = CharacterStudio.Rendering.RuntimeAssetLoader.GetImageDimensions(texturePath);
                    if (dimensions.x >= 480 && dimensions.y >= 480)
                    {
                        Vector2 normalizedScale = targetSlotType == BaseAppearanceSlotType.Body
                            ? new Vector2(1.28f, 1.28f)
                            : new Vector2(1.18f, 1.18f);

                        if (targetSlot.scale == Vector2.one)
                        {
                            targetSlot.scale = normalizedScale;
                            changed = true;
                        }
                    }
                }

                return changed;
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_BaseSlot_Section".Translate(BaseAppearanceUtility.GetDisplayName(slotType)), "BaseSlotBase"))
            {
                bool enabled = slot.enabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_BaseSlot_Enable".Translate(), ref enabled);
                if (enabled != slot.enabled)
                {
                    CaptureUndoSnapshot();
                    slot.enabled = enabled;
                    isDirty = true;
                    RefreshPreview();
                    RefreshRenderTree();
                }

                string texPath = slot.texPath ?? string.Empty;
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_Prop_TexturePath".Translate(), ref texPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(slot.texPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        slot.texPath = path ?? string.Empty;
                        slot.enabled = !string.IsNullOrWhiteSpace(slot.texPath);
                        TryAutoNormalizeBaseSlotTexture(slot, slotType, slot.texPath);
                        MarkBaseSlotDirty();
                    }))))
                {
                    CaptureUndoSnapshot();
                    slot.texPath = texPath;
                    slot.enabled = !string.IsNullOrWhiteSpace(texPath);
                    TryAutoNormalizeBaseSlotTexture(slot, slotType, slot.texPath);
                    MarkBaseSlotDirty();
                }

                string maskPath = slot.maskTexPath ?? string.Empty;
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_BaseSlot_MaskTexture".Translate(), ref maskPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(slot.maskTexPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        slot.maskTexPath = path ?? string.Empty;
                        isDirty = true;
                        RefreshPreview();
                        RefreshRenderTree();
                    }))))
                {
                    CaptureUndoSnapshot();
                    slot.maskTexPath = maskPath;
                    isDirty = true;
                    RefreshPreview();
                    RefreshRenderTree();
                }

                string shaderDefName = slot.shaderDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_BaseSlot_Shader".Translate(), ref shaderDefName);
                if (shaderDefName != (slot.shaderDefName ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    slot.shaderDefName = shaderDefName;
                    isDirty = true;
                    RefreshPreview();
                }

                string anchorTag = BaseAppearanceUtility.GetAnchorTag(slotType);
                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Prop_AnchorPoint".Translate(), anchorTag);
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Transform".Translate(), "BaseSlotTransform", previewRotation == Rot4.South))
            {
                Vector3 editableOffset = GetEditableOffsetForPreview(slot);

                float ox = editableOffset.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref ox, -1f, 1f, "F3");
                if (Math.Abs(ox - editableOffset.x) > 0.0001f)
                {
                    editableOffset.x = ox;
                    SetEditableOffsetForPreview(slot, editableOffset);
                    isDirty = true;
                    RefreshPreview();
                }

                float oy = editableOffset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetYHeight".Translate(), ref oy, -1f, 1f, "F3");
                if (Math.Abs(oy - editableOffset.y) > 0.0001f)
                {
                    editableOffset.y = oy;
                    SetEditableOffsetForPreview(slot, editableOffset);
                    isDirty = true;
                    RefreshPreview();
                }

                float oz = editableOffset.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref oz, -1f, 1f, "F3");
                if (Math.Abs(oz - editableOffset.z) > 0.0001f)
                {
                    editableOffset.z = oz;
                    SetEditableOffsetForPreview(slot, editableOffset);
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_EastOffset".Translate(), "BaseSlotEast", previewRotation == Rot4.East || previewRotation == Rot4.West))
            {
                float ex = slot.offsetEast.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref ex, -1f, 1f, "F3");
                if (Math.Abs(ex - slot.offsetEast.x) > 0.0001f)
                {
                    slot.offsetEast.x = ex;
                    isDirty = true;
                    RefreshPreview();
                }

                float ey = slot.offsetEast.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref ey, -1f, 1f, "F3");
                if (Math.Abs(ey - slot.offsetEast.y) > 0.0001f)
                {
                    slot.offsetEast.y = ey;
                    isDirty = true;
                    RefreshPreview();
                }

                float ez = slot.offsetEast.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref ez, -1f, 1f, "F3");
                if (Math.Abs(ez - slot.offsetEast.z) > 0.0001f)
                {
                    slot.offsetEast.z = ez;
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_NorthOffset".Translate(), "BaseSlotNorth", previewRotation == Rot4.North))
            {
                float nx = slot.offsetNorth.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref nx, -1f, 1f, "F3");
                if (Math.Abs(nx - slot.offsetNorth.x) > 0.0001f)
                {
                    slot.offsetNorth.x = nx;
                    isDirty = true;
                    RefreshPreview();
                }

                float ny = slot.offsetNorth.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref ny, -1f, 1f, "F3");
                if (Math.Abs(ny - slot.offsetNorth.y) > 0.0001f)
                {
                    slot.offsetNorth.y = ny;
                    isDirty = true;
                    RefreshPreview();
                }

                float nz = slot.offsetNorth.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref nz, -1f, 1f, "F3");
                if (Math.Abs(nz - slot.offsetNorth.z) > 0.0001f)
                {
                    slot.offsetNorth.z = nz;
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Misc".Translate(), "BaseSlotMisc"))
            {
                DrawPropertyHint(ref y, width, "CS_Studio_Transform_GlobalHint".Translate());

                float scaleX = slot.scale.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_GlobalScaleX".Translate(), ref scaleX, 0.1f, 3f, "F3");
                if (Math.Abs(scaleX - slot.scale.x) > 0.0001f)
                {
                    slot.scale.x = scaleX;
                    isDirty = true;
                    RefreshPreview();
                }

                float scaleY = slot.scale.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_GlobalScaleY".Translate(), ref scaleY, 0.1f, 3f, "F3");
                if (Math.Abs(scaleY - slot.scale.y) > 0.0001f)
                {
                    slot.scale.y = scaleY;
                    isDirty = true;
                    RefreshPreview();
                }

                float baseRotation = slot.rotation;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_BaseRotation".Translate(), ref baseRotation, -180f, 180f, "F0");
                if (Math.Abs(baseRotation - slot.rotation) > 0.0001f)
                {
                    slot.rotation = baseRotation;
                    isDirty = true;
                    RefreshPreview();
                }

                float currentScaleX = GetEditableSlotScaleForPreview(slot).x;
                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Transform_PreviewScaleX".Translate(), currentScaleX.ToString("F3"));

                float currentScaleY = GetEditableSlotScaleForPreview(slot).y;
                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Transform_PreviewScaleY".Translate(), currentScaleY.ToString("F3"));

                float currentRotation = GetEditableSlotRotationForPreview(slot);
                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Transform_PreviewRotation".Translate(), currentRotation.ToString("F0"));

                float drawOrderOffset = slot.drawOrderOffset;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_BaseSlot_DrawOrderOffset".Translate(), ref drawOrderOffset, -50f, 50f, "F0");
                if (Math.Abs(drawOrderOffset - slot.drawOrderOffset) > 0.0001f)
                {
                    slot.drawOrderOffset = drawOrderOffset;
                    isDirty = true;
                    RefreshPreview();
                }

                float slotBaseLayer = BaseAppearanceUtility.GetBaseDrawOrder(slotType);
                float finalSlotLayer = Mathf.Clamp(slotBaseLayer + slot.drawOrderOffset, -10f, 100f);
                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Transform_FinalDrawOrder".Translate(), finalSlotLayer.ToString("F0"));

                bool flip = slot.flipHorizontal;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_FlipHorizontal".Translate(), ref flip);
                if (flip != slot.flipHorizontal)
                {
                    slot.flipHorizontal = flip;
                    isDirty = true;
                    RefreshPreview();
                }
            }

            bool isBaseSlotEastActive = previewRotation == Rot4.East || previewRotation == Rot4.West;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Transform_EastRotation".Translate(), "BaseSlotEastScaleRotation", isBaseSlotEastActive))
            {
                DrawPropertyHint(ref y, width, "CS_Studio_Transform_EastRotationHint".Translate());

                float eastRotationOffset = slot.rotationEastOffset;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_RotationOffset".Translate(), ref eastRotationOffset, -180f, 180f, "F0");
                if (Math.Abs(eastRotationOffset - slot.rotationEastOffset) > 0.0001f)
                {
                    slot.rotationEastOffset = eastRotationOffset;
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Rendering".Translate(), "BaseSlotRendering"))
            {

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_BaseSlot_PrimaryColorSource".Translate(), slot.colorSource,
                    (LayerColorSource[])Enum.GetValues(typeof(LayerColorSource)),
                    option => option.ToString(),
                    val =>
                    {
                        slot.colorSource = val;
                        isDirty = true;
                        RefreshPreview();
                    });

                if (slot.colorSource == LayerColorSource.Fixed)
                {
                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_BaseSlot_PrimaryColor".Translate(), slot.customColor, col =>
                    {
                        slot.customColor = col;
                        isDirty = true;
                        RefreshPreview();
                    });
                }

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_BaseSlot_SecondaryColorSource".Translate(), slot.colorTwoSource,
                    (LayerColorSource[])Enum.GetValues(typeof(LayerColorSource)),
                    option => option.ToString(),
                    val =>
                    {
                        slot.colorTwoSource = val;
                        isDirty = true;
                        RefreshPreview();
                    });

                if (slot.colorTwoSource == LayerColorSource.Fixed)
                {
                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_BaseSlot_SecondaryColor".Translate(), slot.customColorTwo, col =>
                    {
                        slot.customColorTwo = col;
                        isDirty = true;
                        RefreshPreview();
                    });
                }
            }

            Widgets.EndScrollView();
        }

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
                DrawPropertyHint(ref y, width,
                    string.IsNullOrWhiteSpace(defName) || UIHelper.IsValidDefName(defName)
                        ? "CS_Studio_Equip_DefName_Hint".Translate()
                        : "CS_Studio_Equip_DefName_Invalid".Translate());

                string label = equipment.label ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Label".Translate(), ref label);
                if (label != (equipment.label ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.label = label;
                    renderData.layerName = string.IsNullOrWhiteSpace(renderData.layerName) ? label : renderData.layerName;
                    MarkEquipmentDirty(false);
                }

                string description = equipment.description ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Description".Translate(), ref description);
                if (description != (equipment.description ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.description = description;
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
                DrawPropertyHint(ref y, width, "CS_Studio_Equip_SlotTag_Hint".Translate());

                string thingDefName = equipment.thingDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_LinkedThingDef".Translate(), ref thingDefName);
                if (thingDefName != (equipment.thingDefName ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.thingDefName = thingDefName;
                    MarkEquipmentDirty(false);
                }
                DrawPropertyHint(ref y, width, "CS_Studio_Equip_LinkedThingDef_Hint".Translate());

                string parentThingDefName = equipment.parentThingDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "ParentName", ref parentThingDefName);
                if (parentThingDefName != (equipment.parentThingDefName ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    equipment.parentThingDefName = string.IsNullOrWhiteSpace(parentThingDefName) ? "ApparelMakeableBase" : parentThingDefName;
                    MarkEquipmentDirty(false);
                }
                DrawPropertyHint(ref y, width, "ApparelMakeableBase / 可替换为其他 Apparel 父 Def");

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

            if (DrawCollapsibleSection(ref y, width, "ThingDef / Apparel", "EquipmentDefinition"))
            {
                string worldTexPath = equipment.worldTexPath ?? string.Empty;
                if (DrawPathFieldWithBrowser(ref y, "World TexPath", ref worldTexPath, () =>
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
                if (DrawPathFieldWithBrowser(ref y, "Worn TexPath", ref wornTexPath, () =>
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
                if (DrawPathFieldWithBrowser(ref y, "Apparel Mask", ref equipmentMaskTexPath, () =>
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
                    string.IsNullOrWhiteSpace(equipment.shaderDefName) ? "Cutout" : equipment.shaderDefName,
                    () => ShowEquipmentShaderSelector(equipment, () => MarkEquipmentDirty()));
                DrawPropertyHint(ref y, width, "CS_Studio_Equip_ShaderDef_Hint".Translate());

                bool useWornGraphicMask = equipment.useWornGraphicMask;
                UIHelper.DrawPropertyCheckbox(ref y, width, "useWornGraphicMask", ref useWornGraphicMask);
                if (useWornGraphicMask != equipment.useWornGraphicMask)
                {
                    CaptureUndoSnapshot();
                    equipment.useWornGraphicMask = useWornGraphicMask;
                    MarkEquipmentDirty(false);
                }

                string thingCategoriesText = string.Join(", ", equipment.thingCategories ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "ThingCategories", ref thingCategoriesText);
                string normalizedThingCategoriesText = string.Join(", ", equipment.thingCategories ?? new List<string>());
                if (thingCategoriesText != normalizedThingCategoriesText)
                {
                    CaptureUndoSnapshot();
                    equipment.thingCategories = ParseCommaSeparatedList(thingCategoriesText).ToList();
                    MarkEquipmentDirty(false);
                }

                string bodyPartGroupsText = string.Join(", ", equipment.bodyPartGroups ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "BodyPartGroups", ref bodyPartGroupsText);
                string normalizedBodyPartGroupsText = string.Join(", ", equipment.bodyPartGroups ?? new List<string>());
                if (bodyPartGroupsText != normalizedBodyPartGroupsText)
                {
                    CaptureUndoSnapshot();
                    equipment.bodyPartGroups = ParseCommaSeparatedList(bodyPartGroupsText).ToList();
                    MarkEquipmentDirty(false);
                }

                string apparelLayersText = string.Join(", ", equipment.apparelLayers ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "ApparelLayers", ref apparelLayersText);
                string normalizedApparelLayersText = string.Join(", ", equipment.apparelLayers ?? new List<string>());
                if (apparelLayersText != normalizedApparelLayersText)
                {
                    CaptureUndoSnapshot();
                    equipment.apparelLayers = ParseCommaSeparatedList(apparelLayersText).ToList();
                    MarkEquipmentDirty(false);
                }

                string apparelTagsText = string.Join(", ", equipment.apparelTags ?? new List<string>());
                UIHelper.DrawPropertyField(ref y, width, "ApparelTags", ref apparelTagsText);
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
                DrawPropertyHint(ref y, width, "CS_Studio_Equip_AnchorTag_Hint".Translate());

                string anchorPath = renderData.anchorPath ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Equip_AnchorPath".Translate(), ref anchorPath);
                if (anchorPath != (renderData.anchorPath ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    renderData.anchorPath = anchorPath;
                    MarkEquipmentDirty();
                }
                DrawPropertyHint(ref y, width, "CS_Studio_Equip_AnchorPath_Hint".Translate());

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

            if (DrawCollapsibleSection(ref y, width, "Triggered Animation", "EquipmentTriggeredAnimation"))
            {
                bool useTriggered = renderData.useTriggeredLocalAnimation;
                UIHelper.DrawPropertyCheckbox(ref y, width, "Enable Triggered Animation", ref useTriggered);
                if (useTriggered != renderData.useTriggeredLocalAnimation)
                {
                    CaptureUndoSnapshot();
                    renderData.useTriggeredLocalAnimation = useTriggered;
                    MarkEquipmentDirty();
                }

                if (renderData.useTriggeredLocalAnimation)
                {
                    string triggerAbilityDefName = renderData.triggerAbilityDefName ?? string.Empty;
                    UIHelper.DrawPropertyField(ref y, width, "Trigger Ability DefName", ref triggerAbilityDefName);
                    if (triggerAbilityDefName != (renderData.triggerAbilityDefName ?? string.Empty))
                    {
                        CaptureUndoSnapshot();
                        renderData.triggerAbilityDefName = triggerAbilityDefName;
                        MarkEquipmentDirty(false);
                    }

                    UIHelper.DrawPropertyDropdown(ref y, width, "Triggered Role", renderData.triggeredAnimationRole,
                        (EquipmentTriggeredAnimationRole[])Enum.GetValues(typeof(EquipmentTriggeredAnimationRole)),
                        option => option.ToString(),
                        val =>
                        {
                            CaptureUndoSnapshot();
                            renderData.triggeredAnimationRole = val;
                            MarkEquipmentDirty();
                        });

                    float deployAngle = renderData.triggeredDeployAngle;
                    UIHelper.DrawPropertySlider(ref y, width, "Deploy Angle", ref deployAngle, -180f, 180f, "F0");
                    if (Math.Abs(deployAngle - renderData.triggeredDeployAngle) > 0.0001f) { CaptureUndoSnapshot(); renderData.triggeredDeployAngle = deployAngle; MarkEquipmentDirty(); }

                    float returnAngle = renderData.triggeredReturnAngle;
                    UIHelper.DrawPropertySlider(ref y, width, "Return Angle", ref returnAngle, -180f, 180f, "F0");
                    if (Math.Abs(returnAngle - renderData.triggeredReturnAngle) > 0.0001f) { CaptureUndoSnapshot(); renderData.triggeredReturnAngle = returnAngle; MarkEquipmentDirty(); }

                    float deployTicksValue = renderData.triggeredDeployTicks;
                    UIHelper.DrawPropertySlider(ref y, width, "Deploy Ticks", ref deployTicksValue, 1f, 300f, "F0");
                    int deployTicks = Mathf.RoundToInt(deployTicksValue);
                    if (deployTicks != renderData.triggeredDeployTicks) { CaptureUndoSnapshot(); renderData.triggeredDeployTicks = deployTicks; MarkEquipmentDirty(false); }

                    float holdTicksValue = renderData.triggeredHoldTicks;
                    UIHelper.DrawPropertySlider(ref y, width, "Hold Ticks", ref holdTicksValue, 0f, 600f, "F0");
                    int holdTicks = Mathf.RoundToInt(holdTicksValue);
                    if (holdTicks != renderData.triggeredHoldTicks) { CaptureUndoSnapshot(); renderData.triggeredHoldTicks = holdTicks; MarkEquipmentDirty(false); }

                    float returnTicksValue = renderData.triggeredReturnTicks;
                    UIHelper.DrawPropertySlider(ref y, width, "Return Ticks", ref returnTicksValue, 1f, 300f, "F0");
                    int returnTicks = Mathf.RoundToInt(returnTicksValue);
                    if (returnTicks != renderData.triggeredReturnTicks) { CaptureUndoSnapshot(); renderData.triggeredReturnTicks = returnTicks; MarkEquipmentDirty(false); }

                    float pivotX = renderData.triggeredPivotOffset.x;
                    UIHelper.DrawPropertySlider(ref y, width, "Pivot X", ref pivotX, -1f, 1f, "F3");
                    float pivotY = renderData.triggeredPivotOffset.y;
                    UIHelper.DrawPropertySlider(ref y, width, "Pivot Y", ref pivotY, -1f, 1f, "F3");
                    Vector2 newPivot = new Vector2(pivotX, pivotY);
                    if (newPivot != renderData.triggeredPivotOffset) { CaptureUndoSnapshot(); renderData.triggeredPivotOffset = newPivot; MarkEquipmentDirty(); }

                    bool useVfxVisibility = renderData.triggeredUseVfxVisibility;
                    UIHelper.DrawPropertyCheckbox(ref y, width, "Effect Layer Visibility By Cycle", ref useVfxVisibility);
                    if (useVfxVisibility != renderData.triggeredUseVfxVisibility) { CaptureUndoSnapshot(); renderData.triggeredUseVfxVisibility = useVfxVisibility; MarkEquipmentDirty(); }
                }
            }

            Widgets.EndScrollView();
        }

        private static readonly string[] EquipmentShaderOptions =
        {
            "Cutout",
            "CutoutComplex",
            "Transparent",
            "TransparentPostLight",
            "MetaOverlay"
        };

        private void DrawSelectionPropertyButton(ref float y, float width, string label, string valueLabel, Action onClick, float labelWidth = UIHelper.LabelWidth)
        {
            Rect rect = new Rect(0f, y, width, UIHelper.RowHeight);

            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label).x + 10f);

            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24f), label);

            Rect buttonRect = new Rect(rect.x + actualLabelWidth, rect.y, rect.width - actualLabelWidth, 24f);
            Widgets.DrawBoxSolid(buttonRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(buttonRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UIHelper.HeaderColor;
            string displayValue = string.IsNullOrWhiteSpace(valueLabel)
                ? "CS_Studio_None".Translate().ToString()
                : valueLabel;
            Widgets.Label(buttonRect, displayValue);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            if (Widgets.ButtonInvisible(buttonRect))
            {
                onClick();
            }

            y += UIHelper.RowHeight;
        }

        private void DrawPropertyHint(ref float y, float width, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(0f, y - 4f, width, 26f), text);
            GUI.color = Color.white;
            Text.Font = oldFont;
            y += 18f;
        }

        private string GetEquipmentThingDefSelectionLabel(string defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return "CS_Studio_None".Translate();
            }

            ThingDef resolved = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (resolved == null)
            {
                return defName;
            }

            string label = string.IsNullOrWhiteSpace(resolved.label) ? resolved.defName : resolved.label;
            return $"{label} [{resolved.defName}]";
        }

        private void ShowEquipmentLinkedThingDefSelector(CharacterEquipmentDef equipment, Action onChanged)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    CaptureUndoSnapshot();
                    equipment.thingDefName = string.Empty;
                    onChanged();
                })
            };

            var defs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def != null && (def.apparel != null || def.category == ThingCategory.Item))
                .OrderBy(def => def.label ?? def.defName);

            foreach (ThingDef thingDef in defs)
            {
                ThingDef localDef = thingDef;
                string label = string.IsNullOrWhiteSpace(localDef.label) ? localDef.defName : localDef.label;
                options.Add(new FloatMenuOption($"{label} [{localDef.defName}]", () =>
                {
                    CaptureUndoSnapshot();
                    equipment.thingDefName = localDef.defName;
                    onChanged();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowEquipmentShaderSelector(CharacterEquipmentDef equipment, Action onChanged)
        {
            var options = new List<FloatMenuOption>();

            foreach (string shaderName in EquipmentShaderOptions)
            {
                string localShader = shaderName;
                options.Add(new FloatMenuOption(localShader, () =>
                {
                    CaptureUndoSnapshot();
                    equipment.shaderDefName = localShader;
                    equipment.renderData.shaderDefName = localShader;
                    onChanged();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string[] ParseCommaSeparatedList(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Array.Empty<string>();

            return input
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrEmpty(part))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// 绘制带方向高亮的章节标题
        /// </summary>
        private void DrawDirectionalSectionTitle(ref float y, float width, string title, bool isActive)
        {
            if (isActive)
            {
                Rect bgRect = new Rect(0, y, width, 20);
                Widgets.DrawBoxSolid(bgRect, new Color(0.2f, 0.4f, 0.6f, 0.3f));

                GUI.color = new Color(0.4f, 0.8f, 1f);
                Widgets.Label(new Rect(0, y, 16, 18), "▶");
                GUI.color = Color.white;

                Text.Font = GameFont.Small;
                GUI.color = new Color(0.6f, 0.9f, 1f);
                Widgets.Label(new Rect(16, y, width - 16, 18), title);
                GUI.color = Color.white;
            }
            else
            {
                UIHelper.DrawSectionTitle(ref y, width, title);
                return;
            }

            y += 22;
        }

        /// <summary>
        /// 绘制可折叠的属性区域
        /// </summary>
        private bool DrawCollapsibleSection(ref float y, float width, string title, string sectionKey, bool highlight = false)
        {
            bool isCollapsed = collapsedSections.Contains(sectionKey);

            y += 5f;
            Rect rect = new Rect(0, y, width, 24f);

            if (highlight)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.4f, 0.6f, 0.3f));
            }
            else
            {
                Widgets.DrawLightHighlight(rect);
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = highlight ? new Color(0.6f, 0.9f, 1f) : UIHelper.HeaderColor;

            string icon = isCollapsed ? "▶" : "▼";
            string displayTitle = $"{icon} {title}";

            if (Widgets.ButtonInvisible(rect))
            {
                if (isCollapsed)
                {
                    collapsedSections.Remove(sectionKey);
                }
                else
                {
                    collapsedSections.Add(sectionKey);
                }
            }

            Widgets.Label(rect, displayTitle);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            y += 28f;

            return !isCollapsed;
        }
    }
}