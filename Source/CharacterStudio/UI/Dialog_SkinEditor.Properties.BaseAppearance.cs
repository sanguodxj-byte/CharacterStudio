using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
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
    }
}
