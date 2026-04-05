using System;
using System.Linq;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void DrawSelectedLayerProperties(Rect rect)
        {
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
            Rect viewRect = new Rect(0, 0, propsRect.width - 20, 1600);

            Widgets.BeginScrollView(propsRect.ContractedBy(2f), ref propsScrollPos, viewRect);

            float y = 0;
            float width = viewRect.width;

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Context".Translate(), "Context"))
            {
                DrawLayerContextSection(ref y, width, layer);
            }

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
                    ApplyToOtherSelectedLayers(l =>
                    {
                        var v = GetEditableLayerOffsetForPreview(l);
                        v.x = ox;
                        SetEditableLayerOffsetForPreview(l, v);
                    });
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
                    ApplyToOtherSelectedLayers(l =>
                    {
                        var v = GetEditableLayerOffsetForPreview(l);
                        v.y = oy;
                        SetEditableLayerOffsetForPreview(l, v);
                    });
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
                    ApplyToOtherSelectedLayers(l =>
                    {
                        var v = GetEditableLayerOffsetForPreview(l);
                        v.z = oz;
                        SetEditableLayerOffsetForPreview(l, v);
                    });
                    isDirty = true;
                    RefreshPreview();
                }

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

                float newDrawOrder = layer.drawOrder;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_DrawOrder".Translate(), ref newDrawOrder, -10f, 100f, "F3");
                if (Mathf.Abs(newDrawOrder - layer.drawOrder) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.drawOrder = newDrawOrder;
                    ApplyToOtherSelectedLayers(l => l.drawOrder = newDrawOrder);
                    isDirty = true;
                    RefreshPreview();
                }

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

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Advanced".Translate(), "Advanced"))
            {
                bool visible = layer.visible;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_Visible".Translate(), ref visible);
                if (visible != layer.visible)
                {
                    CaptureUndoSnapshot();
                    layer.visible = visible;
                    ApplyToOtherSelectedLayers(l => l.visible = visible);
                    isDirty = true;
                    RefreshPreview();
                }

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
                DrawPropertyHint(ref y, width, "CS_Studio_Variant_SectionHint".Translate());

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Variant_LayerRole".Translate(), layer.role,
                    (LayerRole[])Enum.GetValues(typeof(LayerRole)),
                    option => ($"CS_Studio_LayerRole_{option}").Translate(),
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
                    option => ($"CS_Studio_VariantLogic_{option}").Translate(),
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

                string[] directionalFacingOptions = { string.Empty, "South", "North", "East", "West", "EastWest" };
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Variant_DirectionalFacing".Translate(), layer.directionalFacing ?? string.Empty,
                    directionalFacingOptions,
                    option => GetDirectionalFacingLabel(option),
                    val =>
                    {
                        CaptureUndoSnapshot();
                        layer.directionalFacing = val;
                        ApplyToOtherSelectedLayers(l => l.directionalFacing = val);
                        isDirty = true;
                        RefreshPreview();
                    });

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

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Tools".Translate(), "Tools"))
            {
                DrawLayerToolsSection(ref y, width);
            }

            Widgets.EndScrollView();
        }

        private void DrawLayerContextSection(ref float y, float width, PawnLayerConfig layer)
        {
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Panel_Preview".Translate(), previewRotation.ToString());

            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Variant_Logic".Translate(), ($"CS_Studio_VariantLogic_{layer.variantLogic}").Translate());
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Anim_Type".Translate(), GetLayerAnimationSummary(layer));
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Section_HideVanilla".Translate(), GetHiddenVanillaSummary());
        }

        private void DrawLayerToolsSection(ref float y, float width)
        {
            string hiddenSummary = GetHiddenVanillaSummary();
            DrawSelectionPropertyButton(ref y, width, "CS_Studio_Section_HideVanilla".Translate(), hiddenSummary, ShowHiddenTagsMenu);

            if (workingSkin.hiddenPaths != null && workingSkin.hiddenPaths.Count > 0)
            {
                Widgets.Label(new Rect(0, y, width, 22f), "CS_Studio_Hide_HiddenPaths".Translate());
                y += 24f;
                foreach (var path in workingSkin.hiddenPaths.ToList())
                {
                    Rect removeRect = new Rect(width - 36f, y - 1f, 32f, 22f);
                    Rect pathRect = new Rect(0f, y, width - 44f, 22f);
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

                    y += 24f;
                }
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, y, width, 22f), "CS_Studio_Hide_NoHiddenPaths".Translate());
                GUI.color = Color.white;
                y += 24f;
            }

#pragma warning disable CS0618
            if (workingSkin.hiddenTags != null && workingSkin.hiddenTags.Count > 0)
            {
                Widgets.Label(new Rect(0f, y, width, 22f), "CS_Studio_Hide_HiddenTagsCompat".Translate());
                y += 24f;
                foreach (var tag in workingSkin.hiddenTags.ToList())
                {
                    Rect removeRect = new Rect(width - 36f, y - 1f, 32f, 22f);
                    Rect tagRect = new Rect(0f, y, width - 44f, 22f);
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

                    y += 24f;
                }
            }
#pragma warning restore

            if (Widgets.ButtonText(new Rect(0f, y, width, 24f), "CS_Studio_Btn_AddHidden".Translate()))
            {
                ShowHiddenTagsMenu();
            }

            y += 28f;
        }

        private static string GetDirectionalFacingLabel(string option)
        {
            string key = string.IsNullOrWhiteSpace(option)
                ? "CS_Studio_Variant_DirectionalFacing_Any"
                : $"CS_Studio_Variant_DirectionalFacing_{option}";

            return key.CanTranslate() ? key.Translate() : option;
        }

        internal void DrawSelectedLayerExpressionMovementSection(ref float y, float width, PawnLayerConfig layer)
        {
            DrawPropertyHint(ref y, width, "CS_Studio_Face_MovementDialog_LayerHint".Translate());
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Prop_LayerName".Translate(), string.IsNullOrWhiteSpace(layer.layerName) ? "CS_Studio_None".Translate() : layer.layerName);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Section_Animation".Translate());
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Anim_Type".Translate(), layer.animationType,
                (LayerAnimationType[])Enum.GetValues(typeof(LayerAnimationType)),
                type => $"CS_Studio_Anim_{type}".Translate(),
                val =>
                {
                    CaptureUndoSnapshot();
                    layer.animationType = val;
                    ApplyToOtherSelectedLayers(l => l.animationType = val);
                    isDirty = true;
                    RefreshPreview();
                });

            if (layer.animationType == LayerAnimationType.None)
            {
                return;
            }

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

            if (layer.animationType != LayerAnimationType.Brownian)
            {
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

            if (layer.animationType == LayerAnimationType.Brownian)
            {
                float radius = layer.brownianRadius;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_BrownianRadius".Translate(), ref radius, 0.02f, 0.6f, "F3");
                if (Math.Abs(radius - layer.brownianRadius) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.brownianRadius = radius;
                    ApplyToOtherSelectedLayers(l => l.brownianRadius = radius);
                    isDirty = true;
                    RefreshPreview();
                }

                float jitter = layer.brownianJitter;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_BrownianJitter".Translate(), ref jitter, 0.001f, 0.05f, "F3");
                if (Math.Abs(jitter - layer.brownianJitter) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.brownianJitter = jitter;
                    ApplyToOtherSelectedLayers(l => l.brownianJitter = jitter);
                    isDirty = true;
                    RefreshPreview();
                }

                float damping = layer.brownianDamping;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_BrownianDamping".Translate(), ref damping, 0.7f, 0.99f, "F3");
                if (Math.Abs(damping - layer.brownianDamping) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.brownianDamping = damping;
                    ApplyToOtherSelectedLayers(l => l.brownianDamping = damping);
                    isDirty = true;
                    RefreshPreview();
                }

                float combatRadius = layer.brownianCombatRadius;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_BrownianCombatRadius".Translate(), ref combatRadius, 0.01f, 0.3f, "F3");
                if (Math.Abs(combatRadius - layer.brownianCombatRadius) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.brownianCombatRadius = combatRadius;
                    ApplyToOtherSelectedLayers(l => l.brownianCombatRadius = combatRadius);
                    isDirty = true;
                    RefreshPreview();
                }

                bool respectWalkability = layer.brownianRespectWalkability;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Anim_BrownianRespectWalkability".Translate(), ref respectWalkability);
                if (respectWalkability != layer.brownianRespectWalkability)
                {
                    CaptureUndoSnapshot();
                    layer.brownianRespectWalkability = respectWalkability;
                    ApplyToOtherSelectedLayers(l => l.brownianRespectWalkability = respectWalkability);
                    isDirty = true;
                    RefreshPreview();
                }

                bool stayInRoom = layer.brownianStayInRoom;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Anim_BrownianStayInRoom".Translate(), ref stayInRoom);
                if (stayInRoom != layer.brownianStayInRoom)
                {
                    CaptureUndoSnapshot();
                    layer.brownianStayInRoom = stayInRoom;
                    ApplyToOtherSelectedLayers(l => l.brownianStayInRoom = stayInRoom);
                    isDirty = true;
                    RefreshPreview();
                }
            }
        }

        private string GetLayerAnimationSummary(PawnLayerConfig layer)
        {
            string translatedType = $"CS_Studio_Anim_{layer.animationType}".Translate();
            if (layer.animationType == LayerAnimationType.None)
            {
                return translatedType;
            }

            if (layer.animationType == LayerAnimationType.Brownian)
            {
                return $"{translatedType} · R {layer.brownianRadius:F3}";
            }

            return $"{translatedType} · {layer.animFrequency:F2} / {layer.animAmplitude:F1}";
        }

        private string GetHiddenVanillaSummary()
        {
            int hiddenPathsCount = workingSkin.hiddenPaths?.Count ?? 0;
#pragma warning disable CS0618
            int hiddenTagsCount = workingSkin.hiddenTags?.Count ?? 0;
#pragma warning restore
            int total = hiddenPathsCount + hiddenTagsCount;
            return total <= 0 ? "CS_Studio_None".Translate() : total.ToString();
        }
    }
}
