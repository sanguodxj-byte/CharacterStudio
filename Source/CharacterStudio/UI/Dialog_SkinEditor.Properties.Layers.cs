using System;
using System.Linq;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
#pragma warning disable CS0618
        private static readonly LayerColorType[] CachedLayerColorTypes =
            (LayerColorType[])Enum.GetValues(typeof(LayerColorType));
#pragma warning restore CS0618
        private static readonly LayerRole[] CachedLayerRoles =
            (LayerRole[])Enum.GetValues(typeof(LayerRole));
        private static readonly LayerVariantLogic[] CachedLayerVariantLogics =
            (LayerVariantLogic[])Enum.GetValues(typeof(LayerVariantLogic));
        private static readonly LayerAnimationType[] CachedLayerAnimationTypes =
            (LayerAnimationType[])Enum.GetValues(typeof(LayerAnimationType));

        private static string BuildVariantLogicTooltip(LayerVariantLogic logic)
        {
            string tooltip = "CS_Studio_Variant_LogicTip".Translate();
            string detailKey = $"CS_Studio_VariantLogicTip_{logic}";
            if (detailKey.CanTranslate())
            {
                tooltip += "\n\n" + detailKey.Translate();
            }

            return tooltip;
        }

        private void MutateSelectedLayersWithUndo(PawnLayerConfig primaryLayer, Action<PawnLayerConfig> mutateLayer, bool refreshRenderTree = false)
        {
            MutateWithUndo(() =>
            {
                mutateLayer(primaryLayer);
                ApplyToOtherSelectedLayers(mutateLayer);
            }, refreshPreview: true, refreshRenderTree: refreshRenderTree);
        }

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
            Rect viewRect = new Rect(0, 0, propsRect.width - 20, lastLayersPanelHeight);

            Widgets.BeginScrollView(propsRect.ContractedBy(2f), ref propsScrollPos, viewRect);

            float y = 0;
            float width = viewRect.width;

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Context".Translate(), "Context"))
            {
                DrawLayerContextSection(ref y, width, layer);
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Base".Translate(), "Base"))
            {
                string layerName = layer.layerName;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Prop_LayerName".Translate(), ref layerName);
                if (!string.Equals(layerName, layer.layerName, StringComparison.Ordinal))
                {
                    MutateSelectedLayersWithUndo(layer, l => l.layerName = layerName);
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
                        MutateSelectedLayersWithUndo(layer, l => l.anchorTag = val);
                    });
            }

            bool isSouthActive = previewRotation == Rot4.South;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Transform".Translate(), "Transform", isSouthActive))
            {
                // 属性面板始终根据预览朝向读写偏移，不受 editLayerOffsetPerFacing 影响
                Vector3 currentOffset = GetLayerOffsetForRotation(layer, previewRotation);

                float ox = currentOffset.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref ox, -1f, 1f);
                if (Math.Abs(ox - currentOffset.x) > 0.0001f)
                {
                    float val = ox;
                    MutateSelectedLayersWithUndo(layer, l => SetLayerOffsetForRotation(l, previewRotation, newOffsetX: val));
                }

                float oy = currentOffset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetYHeight".Translate(), ref oy, -1f, 1f);
                if (Math.Abs(oy - currentOffset.y) > 0.0001f)
                {
                    float val = oy;
                    MutateSelectedLayersWithUndo(layer, l => SetLayerOffsetForRotation(l, previewRotation, newOffsetY: val));
                }

                float oz = currentOffset.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref oz, -1f, 1f);
                if (Math.Abs(oz - currentOffset.z) > 0.0001f)
                {
                    float val = oz;
                    MutateSelectedLayersWithUndo(layer, l => SetLayerOffsetForRotation(l, previewRotation, newOffsetZ: val));
                }

                float uniformScale = layer.scale.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_GlobalScale".Translate(), ref uniformScale, 0.1f, 3f, "F3");
                if (Math.Abs(uniformScale - layer.scale.x) > 0.0001f || Math.Abs(uniformScale - layer.scale.y) > 0.0001f)
                {
                    Vector2 newScale = new Vector2(uniformScale, uniformScale);
                    MutateSelectedLayersWithUndo(layer, l => l.scale = newScale);
                }

                float baseRotation = layer.rotation;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_BaseRotation".Translate(), ref baseRotation, -180f, 180f, "F0");
                if (Math.Abs(baseRotation - layer.rotation) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.rotation = baseRotation);
                }

                float newDrawOrder = layer.drawOrder;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_DrawOrder".Translate(), ref newDrawOrder, -10f, 100f, "F3");
                if (Mathf.Abs(newDrawOrder - layer.drawOrder) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.drawOrder = newDrawOrder);
                }

                bool flip = layer.flipHorizontal;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_FlipHorizontal".Translate(), ref flip);
                if (flip != layer.flipHorizontal)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.flipHorizontal = flip);
                }
            }

            bool isEastActive = previewRotation == Rot4.East || previewRotation == Rot4.West;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_EastOffset".Translate(), "EastOffset", isEastActive))
            {
                bool useWest = layer.useWestOffset;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_IndependentWest".Translate(), ref useWest);
                if (useWest != layer.useWestOffset)
                {
                    bool newValue = useWest;
                    MutateSelectedLayersWithUndo(layer, l =>
                    {
                        l.useWestOffset = newValue;
                        if (newValue && l.offsetWest == Vector3.zero)
                            l.offsetWest = new Vector3(-l.offsetEast.x, l.offsetEast.y, l.offsetEast.z);
                    });
                }

                bool editingWest = (layer.useWestOffset || editLayerOffsetPerFacing) && previewRotation == Rot4.West;
                Vector3 sideOffset;
                if (editingWest)
                {
                    if (!layer.useWestOffset && layer.offsetWest == Vector3.zero)
                        sideOffset = new Vector3(-layer.offsetEast.x, layer.offsetEast.y, layer.offsetEast.z);
                    else
                        sideOffset = layer.offsetWest;
                }
                else
                {
                    sideOffset = layer.offsetEast;
                }

                float sx = sideOffset.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref sx, -1f, 1f);
                if (Math.Abs(sx - sideOffset.x) > 0.0001f)
                {
                    float val = sx;
                    if (editingWest)
                        MutateSelectedLayersWithUndo(layer, l =>
                        {
                            if (!l.useWestOffset) { l.useWestOffset = true; l.offsetWest = new Vector3(-l.offsetEast.x, l.offsetEast.y, l.offsetEast.z); }
                            l.offsetWest.x = val;
                        });
                    else
                        MutateSelectedLayersWithUndo(layer, l => l.offsetEast.x = val);
                }

                float sy = sideOffset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref sy, -1f, 1f);
                if (Math.Abs(sy - sideOffset.y) > 0.0001f)
                {
                    float val = sy;
                    if (editingWest)
                        MutateSelectedLayersWithUndo(layer, l =>
                        {
                            if (!l.useWestOffset) { l.useWestOffset = true; l.offsetWest = new Vector3(-l.offsetEast.x, l.offsetEast.y, l.offsetEast.z); }
                            l.offsetWest.y = val;
                        });
                    else
                        MutateSelectedLayersWithUndo(layer, l => l.offsetEast.y = val);
                }

                float sz = sideOffset.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref sz, -1f, 1f);
                if (Math.Abs(sz - sideOffset.z) > 0.0001f)
                {
                    float val = sz;
                    if (editingWest)
                        MutateSelectedLayersWithUndo(layer, l =>
                        {
                            if (!l.useWestOffset) { l.useWestOffset = true; l.offsetWest = new Vector3(-l.offsetEast.x, l.offsetEast.y, l.offsetEast.z); }
                            l.offsetWest.z = val;
                        });
                    else
                        MutateSelectedLayersWithUndo(layer, l => l.offsetEast.z = val);
                }
            }

            bool isNorthActive = previewRotation == Rot4.North;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_NorthOffset".Translate(), "NorthOffset", isNorthActive))
            {
                float nx = layer.offsetNorth.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref nx, -1f, 1f);
                if (Math.Abs(nx - layer.offsetNorth.x) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.offsetNorth.x = nx);
                }

                float ny = layer.offsetNorth.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref ny, -1f, 1f);
                if (Math.Abs(ny - layer.offsetNorth.y) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.offsetNorth.y = ny);
                }

                float nz = layer.offsetNorth.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref nz, -1f, 1f);
                if (Math.Abs(nz - layer.offsetNorth.z) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.offsetNorth.z = nz);
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Advanced".Translate(), "Advanced"))
            {
                bool visible = layer.visible;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_Visible".Translate(), ref visible);
                if (visible != layer.visible)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.visible = visible);
                }

                string[] shaderOptions = { "Cutout", "CutoutComplex", "Transparent", "TransparentPostLight", "TransparentZWrite", "ItemTransparent", "MetaOverlay", "Custom" };
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Prop_Shader".Translate(), layer.shaderDefName ?? "Cutout",
                    shaderOptions,
                    val => val,
                    val =>
                    {
                        MutateSelectedLayersWithUndo(layer, l => l.shaderDefName = val, refreshRenderTree: true);
                    },
                    tooltip: "CS_Studio_Prop_Shader_Tooltip".Translate());

                // Custom shader path
                if (layer.shaderDefName == "Custom")
                {
                    string customShaderPath = layer.customShaderPath ?? string.Empty;
                    UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Prop_CustomShaderPath".Translate(), ref customShaderPath, tooltip: "CS_Studio_Prop_CustomShaderPath_Tooltip".Translate());
                    if (customShaderPath != (layer.customShaderPath ?? string.Empty))
                    {
                        MutateSelectedLayersWithUndo(layer, l => l.customShaderPath = customShaderPath, refreshRenderTree: true);
                    }
                }

                // Alpha slider for transparent shaders
                bool isTransparentShader = layer.shaderDefName == "Transparent"
                    || layer.shaderDefName == "TransparentPostLight"
                    || layer.shaderDefName == "TransparentZWrite"
                    || layer.shaderDefName == "ItemTransparent"
                    || layer.shaderDefName == "Custom";
                if (isTransparentShader)
                {
                    float alpha = layer.alpha;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_Alpha".Translate(), ref alpha, 0f, 1f, "F2");
                    if (Math.Abs(alpha - layer.alpha) > 0.0001f)
                    {
                        MutateSelectedLayersWithUndo(layer, l => l.alpha = alpha);
                    }
                }

                // ZWrite checkbox
                bool zWrite = layer.zWrite;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_ZWrite".Translate(), ref zWrite, tooltip: "CS_Studio_Prop_ZWrite_Tooltip".Translate());
                if (zWrite != layer.zWrite)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.zWrite = zWrite, refreshRenderTree: true);
                }

                // Mask texture path
                string maskTexPath = layer.maskTexPath ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Prop_MaskTexPath".Translate(), ref maskTexPath, tooltip: "CS_Studio_Prop_MaskTexPath_Tooltip".Translate());
                if (maskTexPath != (layer.maskTexPath ?? string.Empty))
                {
                    MutateSelectedLayersWithUndo(layer, l => l.maskTexPath = maskTexPath, refreshRenderTree: true);
                }

                float eastRotationOffset = layer.rotationEastOffset;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Transform_RotationOffset".Translate(), ref eastRotationOffset, -180f, 180f, "F0");
                if (Math.Abs(eastRotationOffset - layer.rotationEastOffset) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.rotationEastOffset = eastRotationOffset);
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
                        MutateSelectedLayersWithUndo(layer, l => l.workerClass = newWorker);
                    });

#pragma warning disable CS0618
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Prop_ColorType".Translate(), layer.colorType,
                    CachedLayerColorTypes,
                    type => $"CS_Studio_ColorType_{type}".Translate(),
                    val =>
                    {
                        MutateSelectedLayersWithUndo(layer, l => l.colorType = val);
                    });

                if (layer.colorType == LayerColorType.Custom)
                {
#pragma warning restore CS0618
                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_Prop_CustomColor".Translate(), layer.customColor,
                        col =>
                        {
                            MutateSelectedLayersWithUndo(layer, l => l.customColor = col);
                        });

                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_BaseSlot_SecondColorMask".Translate(), layer.customColorTwo,
                        col =>
                        {
                            MutateSelectedLayersWithUndo(layer, l => l.customColorTwo = col);
                        });
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Animation".Translate(), "Animation"))
            {
                DrawSelectedLayerExpressionMovementSection(ref y, width, layer);
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_VariantsExpression".Translate(), "Variants"))
            {
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Variant_LayerRole".Translate(), layer.role,
                    CachedLayerRoles,
                    option => ($"CS_Studio_LayerRole_{option}").Translate(),
                    val =>
                    {
                        MutateSelectedLayersWithUndo(layer, l => l.role = val);
                    }, tooltip: "CS_Studio_Variant_LayerRoleTip".Translate());

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Variant_Logic".Translate(), layer.variantLogic,
                    CachedLayerVariantLogics,
                    option => ($"CS_Studio_VariantLogic_{option}").Translate(),
                    val =>
                    {
                        MutateSelectedLayersWithUndo(layer, l => l.variantLogic = val);
                    }, tooltip: BuildVariantLogicTooltip(layer.variantLogic));

                string variantBaseName = layer.variantBaseName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Variant_BaseName".Translate(), ref variantBaseName, tooltip: "CS_Studio_Variant_BaseNameTip".Translate());
                if (variantBaseName != (layer.variantBaseName ?? string.Empty))
                {
                    MutateSelectedLayersWithUndo(layer, l => l.variantBaseName = variantBaseName);
                }

                bool useDirectionalSuffix = layer.useDirectionalSuffix;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseDirectionalSuffix".Translate(), ref useDirectionalSuffix, tooltip: "CS_Studio_Variant_UseDirectionalSuffixTip".Translate());
                if (useDirectionalSuffix != layer.useDirectionalSuffix)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.useDirectionalSuffix = useDirectionalSuffix);
                }

                string[] directionalFacingOptions = { string.Empty, "South", "North", "East", "West", "EastWest" };
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Variant_DirectionalFacing".Translate(), layer.directionalFacing ?? string.Empty,
                    directionalFacingOptions,
                    option => GetDirectionalFacingLabel(option),
                    val =>
                    {
                        MutateSelectedLayersWithUndo(layer, l => l.directionalFacing = val);
                    }, tooltip: "CS_Studio_Variant_DirectionalFacingTip".Translate());

                bool useExpressionSuffix = layer.useExpressionSuffix;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseExpressionSuffix".Translate(), ref useExpressionSuffix, tooltip: "CS_Studio_Variant_UseExpressionSuffixTip".Translate());
                if (useExpressionSuffix != layer.useExpressionSuffix)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.useExpressionSuffix = useExpressionSuffix);
                }

                bool useEyeDirectionSuffix = layer.useEyeDirectionSuffix;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseEyeDirectionSuffix".Translate(), ref useEyeDirectionSuffix, tooltip: "CS_Studio_Variant_UseEyeDirectionSuffixTip".Translate());
                if (useEyeDirectionSuffix != layer.useEyeDirectionSuffix)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.useEyeDirectionSuffix = useEyeDirectionSuffix);
                }

                bool useBlinkSuffix = layer.useBlinkSuffix;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseBlinkSuffix".Translate(), ref useBlinkSuffix, tooltip: "CS_Studio_Variant_UseBlinkSuffixTip".Translate());
                if (useBlinkSuffix != layer.useBlinkSuffix)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.useBlinkSuffix = useBlinkSuffix);
                }

                bool useFrameSequence = layer.useFrameSequence;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_UseFrameSequence".Translate(), ref useFrameSequence, tooltip: "CS_Studio_Variant_UseFrameSequenceTip".Translate());
                if (useFrameSequence != layer.useFrameSequence)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.useFrameSequence = useFrameSequence);
                }

                bool hideWhenMissingVariant = layer.hideWhenMissingVariant;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Variant_HideWhenMissing".Translate(), ref hideWhenMissingVariant, tooltip: "CS_Studio_Variant_HideWhenMissingTip".Translate());
                if (hideWhenMissingVariant != layer.hideWhenMissingVariant)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.hideWhenMissingVariant = hideWhenMissingVariant);
                }

                string visibleExpressionsText = string.Join(", ", layer.visibleExpressions ?? Array.Empty<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Variant_VisibleExpressions".Translate(), ref visibleExpressionsText, tooltip: "CS_Studio_Variant_VisibleExpressionsTip".Translate());
                string normalizedVisibleExpressionsText = string.Join(", ", layer.visibleExpressions ?? Array.Empty<string>());
                if (visibleExpressionsText != normalizedVisibleExpressionsText)
                {
                    string[] parsed = ParseCommaSeparatedList(visibleExpressionsText);
                    MutateSelectedLayersWithUndo(layer, l => l.visibleExpressions = (string[])parsed.Clone());
                }

                string hiddenExpressionsText = string.Join(", ", layer.hiddenExpressions ?? Array.Empty<string>());
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Variant_HiddenExpressions".Translate(), ref hiddenExpressionsText, tooltip: "CS_Studio_Variant_HiddenExpressionsTip".Translate());
                string normalizedHiddenExpressionsText = string.Join(", ", layer.hiddenExpressions ?? Array.Empty<string>());
                if (hiddenExpressionsText != normalizedHiddenExpressionsText)
                {
                    string[] parsed = ParseCommaSeparatedList(hiddenExpressionsText);
                    MutateSelectedLayersWithUndo(layer, l => l.hiddenExpressions = (string[])parsed.Clone());
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
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Panel_Preview".Translate(), GetPreviewRotationLabel(previewRotation));

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
                UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Hide_HiddenPaths".Translate());
                foreach (var path in workingSkin.hiddenPaths.ToList())
                {
                    Rect removeRect = new Rect(width - 36f, y - 1f, 32f, 22f);
                    Rect pathRect = new Rect(0f, y, width - 44f, 22f);
                    Widgets.Label(pathRect, $"  • {path}");
                    if (UIHelper.DrawDangerButton(removeRect, tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                    {
                        MutateWithUndo(() => workingSkin.hiddenPaths.Remove(path), refreshPreview: true, refreshRenderTree: true);
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
                UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Hide_HiddenTagsCompat".Translate());
                foreach (var tag in workingSkin.hiddenTags.ToList())
                {
                    Rect removeRect = new Rect(width - 36f, y - 1f, 32f, 22f);
                    Rect tagRect = new Rect(0f, y, width - 44f, 22f);
                    Widgets.Label(tagRect, $"  • {tag}");
                    if (UIHelper.DrawDangerButton(removeRect, tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                    {
                        MutateWithUndo(() => workingSkin.hiddenTags.Remove(tag), refreshPreview: true, refreshRenderTree: true);
                    }))
                    {
                    }

                    y += 24f;
                }
            }
#pragma warning restore

            int hiddenTotal = (workingSkin.hiddenPaths?.Count ?? 0)
#pragma warning disable CS0618
                + (workingSkin.hiddenTags?.Count ?? 0);
#pragma warning restore
            DrawSelectionPropertyButton(ref y, width, "CS_Studio_Btn_AddHidden".Translate(),
                hiddenTotal > 0 ? hiddenTotal.ToString() : "0",
                ShowHiddenTagsMenu);
        }

        private static string GetDirectionalFacingLabel(string option)
        {
            string key = string.IsNullOrWhiteSpace(option)
                ? "CS_Studio_Variant_DirectionalFacing_Any"
                : $"CS_Studio_Variant_DirectionalFacing_{option}";

            return key.CanTranslate() ? key.Translate() : option;
        }

        private static string GetPreviewRotationLabel(Rot4 rot)
        {
            string key = $"CS_Studio_Rotation_{rot}";
            return key.CanTranslate() ? key.Translate() : rot.ToString();
        }

        private static string GetLayerColorSourceLabel(LayerColorSource source)
        {
            string key = $"CS_Studio_ColorSource_{source}";
            return key.CanTranslate() ? key.Translate() : source.ToString();
        }

        internal void DrawSelectedLayerExpressionMovementSection(ref float y, float width, PawnLayerConfig layer)
        {
            DrawPropertyHint(ref y, width, "CS_Studio_Face_MovementDialog_LayerHint".Translate());
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Prop_LayerName".Translate(), string.IsNullOrWhiteSpace(layer.layerName) ? "CS_Studio_None".Translate() : layer.layerName);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Section_Animation".Translate());
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Anim_Type".Translate(), layer.animationType,
                CachedLayerAnimationTypes,
                type => $"CS_Studio_Anim_{type}".Translate(),
                val =>
                {
                    MutateSelectedLayersWithUndo(layer, l => l.animationType = val);
                });

            if (layer.animationType == LayerAnimationType.None)
            {
                return;
            }

            float freq = layer.animFrequency;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_Frequency".Translate(), ref freq, 0.1f, 20f, tooltip: "CS_Studio_Anim_Frequency_Tip".Translate());
            if (Math.Abs(freq - layer.animFrequency) > 0.0001f)
            {
                MutateSelectedLayersWithUndo(layer, l => l.animFrequency = freq);
            }

            if (layer.animationType != LayerAnimationType.Brownian)
            {
                float amp = layer.animAmplitude;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_Amplitude".Translate(), ref amp, 1f, 180f, tooltip: "CS_Studio_Anim_Amplitude_Tip".Translate());
                if (Math.Abs(amp - layer.animAmplitude) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.animAmplitude = amp);
                }
            }

            if (layer.animationType == LayerAnimationType.Twitch)
            {
                float speed = layer.animSpeed;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_Speed".Translate(), ref speed, 0.1f, 10f, tooltip: "CS_Studio_Anim_Speed_Tip".Translate());
                if (Math.Abs(speed - layer.animSpeed) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.animSpeed = speed);
                }
            }

            float phase = layer.animPhaseOffset;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_PhaseOffset".Translate(), ref phase, 0f, 1f, tooltip: "CS_Studio_Anim_PhaseOffset_Tip".Translate());
            if (Math.Abs(phase - layer.animPhaseOffset) > 0.0001f)
            {
                MutateSelectedLayersWithUndo(layer, l => l.animPhaseOffset = phase);
            }

            bool affectsOffset = layer.animAffectsOffset;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Anim_AffectsOffset".Translate(), ref affectsOffset, "CS_Studio_Anim_AffectsOffset_Tip".Translate());
            if (affectsOffset != layer.animAffectsOffset)
            {
                MutateSelectedLayersWithUndo(layer, l => l.animAffectsOffset = affectsOffset);
            }

            if (layer.animAffectsOffset)
            {
                float offsetAmp = layer.animOffsetAmplitude;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_OffsetAmplitude".Translate(), ref offsetAmp, 0.001f, 10f, "F3", tooltip: "CS_Studio_Anim_OffsetAmplitude_Tip".Translate());
                if (Math.Abs(offsetAmp - layer.animOffsetAmplitude) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.animOffsetAmplitude = offsetAmp);
                }
            }

            if (layer.animationType == LayerAnimationType.Spin)
            {
                float pivotX = layer.animPivotOffset.x;
                float pivotY = layer.animPivotOffset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_PivotX".Translate(), ref pivotX, -5f, 5f, "F3", tooltip: "CS_Studio_Anim_Pivot_Tip".Translate());
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_PivotY".Translate(), ref pivotY, -5f, 5f, "F3", tooltip: "CS_Studio_Anim_Pivot_Tip".Translate());
                var newPivot = new Vector2(pivotX, pivotY);
                if (newPivot != layer.animPivotOffset)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.animPivotOffset = newPivot);
                }
            }

            if (layer.animationType == LayerAnimationType.Brownian)
            {
                float radius = layer.brownianRadius;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_BrownianRadius".Translate(), ref radius, 0.02f, 10f, "F2", tooltip: "CS_Studio_Anim_BrownianRadius_Tip".Translate());
                if (Math.Abs(radius - layer.brownianRadius) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.brownianRadius = radius);
                }

                float jitter = layer.brownianJitter;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_BrownianJitter".Translate(), ref jitter, 0.001f, 1f, "F3", tooltip: "CS_Studio_Anim_BrownianJitter_Tip".Translate());
                if (Math.Abs(jitter - layer.brownianJitter) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.brownianJitter = jitter);
                }

                float damping = layer.brownianDamping;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_BrownianDamping".Translate(), ref damping, 0.7f, 0.999f, "F3", tooltip: "CS_Studio_Anim_BrownianDamping_Tip".Translate());
                if (Math.Abs(damping - layer.brownianDamping) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.brownianDamping = damping);
                }

                float combatRadius = layer.brownianCombatRadius;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_BrownianCombatRadius".Translate(), ref combatRadius, 0.01f, 5f, "F2", tooltip: "CS_Studio_Anim_BrownianCombatRadius_Tip".Translate());
                if (Math.Abs(combatRadius - layer.brownianCombatRadius) > 0.0001f)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.brownianCombatRadius = combatRadius);
                }

                bool respectWalkability = layer.brownianRespectWalkability;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Anim_BrownianRespectWalkability".Translate(), ref respectWalkability, "CS_Studio_Anim_BrownianRespectWalkability_Tip".Translate());
                if (respectWalkability != layer.brownianRespectWalkability)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.brownianRespectWalkability = respectWalkability);
                }

                bool stayInRoom = layer.brownianStayInRoom;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Anim_BrownianStayInRoom".Translate(), ref stayInRoom, "CS_Studio_Anim_BrownianStayInRoom_Tip".Translate());
                if (stayInRoom != layer.brownianStayInRoom)
                {
                    MutateSelectedLayersWithUndo(layer, l => l.brownianStayInRoom = stayInRoom);
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