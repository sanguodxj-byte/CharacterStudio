using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private void DrawVisualEffectsPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect contentRect = rect.ContractedBy(Margin);

            Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width - 82f, 24f),
                "<b>" + "CS_Studio_VFX_Title".Translate() + "</b>");
            if (DrawPanelButton(new Rect(contentRect.x + contentRect.width - 72f, contentRect.y, 72f, 24f),
                "CS_Studio_VFX_Add".Translate(), ShowAddVfxMenu, true))
            {
            }

            float listY = contentRect.y + 28f;
            float listH = contentRect.height - 28f;
            Rect listRect = new Rect(contentRect.x, listY, contentRect.width, listH);

            if (selectedAbility == null || selectedAbility.visualEffects == null || selectedAbility.visualEffects.Count == 0)
            {
                Widgets.DrawHighlight(listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 50f),
                    "CS_Studio_VFX_EmptyHint".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float viewHeight = 6f;
            for (int i = 0; i < selectedAbility.visualEffects.Count; i++)
            {
                viewHeight += GetVfxItemHeight(selectedAbility.visualEffects[i]) + 6f;
            }

            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, viewHeight));
            Widgets.BeginScrollView(listRect, ref vfxScrollPos, viewRect);

            float cy = 0f;
            for (int i = 0; i < selectedAbility.visualEffects.Count; i++)
            {
                var vfx = selectedAbility.visualEffects[i];
                float itemHeight = GetVfxItemHeight(vfx);
                DrawVfxItem(new Rect(0f, cy, viewRect.width, itemHeight), vfx, i);
                cy += itemHeight + 6f;
            }

            Widgets.EndScrollView();
        }

        private static bool UsesCustomTextureSettings(AbilityVisualEffectConfig vfx)
        {
            return vfx.UsesCustomTextureType
                || vfx.type == AbilityVisualEffectType.LineTexture
                || vfx.type == AbilityVisualEffectType.WallTexture;
        }
 
        private static bool UsesPresetSource(AbilityVisualEffectConfig vfx)
        {
            return vfx.UsesPresetType;
        }

        private static bool UsesTextureSourceSelector(AbilityVisualEffectConfig vfx)
        {
            return vfx.UsesCustomTextureType;
        }

        private static bool SupportsRuntimeVfxTrigger(AbilityVisualEffectTrigger trigger)
        {
            return true;
        }

        private static AbilityVisualEffectTrigger NormalizeEditorVfxTrigger(AbilityVisualEffectTrigger trigger)
        {
            return trigger;
        }

        private static bool HasRegisteredPreset(string? presetDefName, IReadOnlyList<string> registeredPresetNames)
        {
            if (presetDefName is not string presetName || string.IsNullOrWhiteSpace(presetName))
            {
                return false;
            }

            string trimmedPresetName = presetName.Trim();
            for (int i = 0; i < registeredPresetNames.Count; i++)
            {
                if (string.Equals(registeredPresetNames[i], trimmedPresetName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool NormalizeVfxEditorState(AbilityVisualEffectConfig vfx)
        {
            bool changed = false;

            AbilityVisualEffectTrigger normalizedTrigger = NormalizeEditorVfxTrigger(vfx.trigger);
            if (vfx.trigger != normalizedTrigger)
            {
                vfx.trigger = normalizedTrigger;
                changed = true;
            }

            if (UsesPresetSource(vfx))
            {
                IReadOnlyList<string> registeredPresetNames = VisualEffectWorkerFactory.GetRegisteredPresetNames();
                if (!HasRegisteredPreset(vfx.presetDefName, registeredPresetNames))
                {
                    vfx.presetDefName = registeredPresetNames.Count > 0 ? registeredPresetNames[0] : string.Empty;
                    changed = true;
                }
            }

            return changed;
        }

        private static string GetPresetSelectionLabel(AbilityVisualEffectConfig vfx)
        {
            return string.IsNullOrWhiteSpace(vfx.presetDefName) ? "..." : vfx.presetDefName.Trim();
        }

        private float GetVfxItemHeight(AbilityVisualEffectConfig vfx)
        {
            float height = 34f;
            height += RowHeight;
            if (UsesPresetSource(vfx) || UsesTextureSourceSelector(vfx))
            {
                height += RowHeight;
            }

            height += RowHeight;
            height += RowHeight;

            if (UsesCustomTextureSettings(vfx))
            {
                height += RowHeight * 5f;
            }

            if (vfx.type == AbilityVisualEffectType.LineTexture || vfx.type == AbilityVisualEffectType.WallTexture)
            {
                height += RowHeight * 3f;
            }

            height += RowHeight * 2f;
            return height + 8f;
        }

        private string BuildVfxTitleLabel(AbilityVisualEffectConfig vfx, int index)
        {
            string titleLabel = $"#{index + 1} {GetVfxTypeLabel(vfx.type)}";
            if (UsesPresetSource(vfx) && !string.IsNullOrWhiteSpace(vfx.presetDefName))
            {
                titleLabel += $" [{vfx.presetDefName}]";
            }
            else if (UsesCustomTextureSettings(vfx) && !string.IsNullOrWhiteSpace(vfx.customTexturePath))
            {
                string textureLabel = vfx.textureSource == AbilityVisualEffectTextureSource.LocalPath
                    ? System.IO.Path.GetFileName(vfx.customTexturePath)
                    : vfx.customTexturePath;
                titleLabel += $" [{textureLabel}]";
            }

            return titleLabel;
        }

        private string BuildVfxSecondarySummary(AbilityVisualEffectConfig vfx)
        {
            string facingText = UsesCustomTextureSettings(vfx)
                ? GetVfxFacingModeLabel(vfx.facingMode)
                : "CS_Studio_VFX_FacingMode_None".Translate();
            return "CS_Studio_VFX_SummaryLine".Translate(
                GetVfxTargetLabel(vfx.target),
                GetVfxTriggerLabel(vfx.trigger),
                facingText);
        }

        private void DrawVfxItem(Rect rect, AbilityVisualEffectConfig vfx, int index)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5f);

            if (NormalizeVfxEditorState(vfx))
            {
                NotifyAbilityPreviewDirty(true);
            }

            GUI.color = vfx.enabled ? Color.white : Color.gray;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width - 120f, 24f), BuildVfxTitleLabel(vfx, index));
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(inner.x, inner.y + 18f, inner.width - 120f, 18f), BuildVfxSecondarySummary(vfx));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float enabledWidth = 112f;
            Rect enabledRect = new Rect(inner.x + inner.width - 186f, inner.y, enabledWidth, 24f);
            bool enabled = vfx.enabled;
            float enabledY = enabledRect.y;
            UIHelper.DrawPropertyCheckbox(ref enabledY, enabledRect.width, "CS_Studio_VFX_Enabled".Translate(), ref enabled, labelWidth: enabledWidth - 28f);
            if (vfx.enabled != enabled)
            {
                vfx.enabled = enabled;
                NotifyAbilityPreviewDirty();
            }

            float buttonX = inner.x + inner.width - 66f;
            if (selectedAbility != null && index > 0 && DrawCompactIconButton(new Rect(buttonX, inner.y, 20f, 24f), "▲", () => SwapVfx(index, index - 1)))
            {
                return;
            }

            buttonX += 22f;
            if (selectedAbility != null && index < selectedAbility.visualEffects.Count - 1 && DrawCompactIconButton(new Rect(buttonX, inner.y, 20f, 24f), "▼", () => SwapVfx(index, index + 1)))
            {
                return;
            }

            buttonX += 22f;
            if (DrawCompactIconButton(new Rect(buttonX, inner.y, 20f, 24f), "X", () =>
            {
                selectedAbility?.visualEffects.RemoveAt(index);
                NotifyAbilityPreviewDirty(true);
            }))
            {
                return;
            }

            float y = inner.y + 36f;
            float gap = 8f;
            float colW = (inner.width - gap) * 0.5f;
            float labelW = 42f;
            float fieldW = colW - labelW - 4f;
            float rightX = inner.x + colW + gap;

            void DrawNumberRow(float rowY, float x, string label, ref float value, ref string buffer, float min, float max)
            {
                Widgets.Label(new Rect(x, rowY, labelW, 24f), label);
                float before = value;
                UIHelper.TextFieldNumeric(new Rect(x + labelW, rowY, fieldW, 24f), ref value, ref buffer, min, max);
                if (Math.Abs(value - before) > 0.001f)
                {
                    NotifyAbilityPreviewDirty();
                }
            }

            void DrawIntRow(float rowY, float x, string label, ref int value, ref string buffer, int min, int max)
            {
                Widgets.Label(new Rect(x, rowY, labelW, 24f), label);
                int before = value;
                UIHelper.TextFieldNumeric(new Rect(x + labelW, rowY, fieldW, 24f), ref value, ref buffer, min, max);
                if (value != before)
                {
                    NotifyAbilityPreviewDirty(true);
                }
            }

            DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_TypeShort".Translate(), GetVfxTypeLabel(vfx.type), () =>
            {
                var options = new List<FloatMenuOption>();
                foreach (AbilityVisualEffectType t in Enum.GetValues(typeof(AbilityVisualEffectType)))
                {
                    var captured = t;
                    options.Add(new FloatMenuOption(GetVfxTypeLabel(captured), () =>
                    {
                        vfx.type = captured;
                        if (!vfx.UsesCustomTextureType)
                        {
                            vfx.textureSource = AbilityVisualEffectTextureSource.Vanilla;
                        }

                        if (vfx.UsesPresetType)
                        {
                            IReadOnlyList<string> registeredPresetNames = VisualEffectWorkerFactory.GetRegisteredPresetNames();
                            vfx.presetDefName = registeredPresetNames.Count > 0 ? registeredPresetNames[0] : string.Empty;
                        }

                        vfx.trigger = NormalizeEditorVfxTrigger(vfx.trigger);
                        vfx.SyncLegacyFields();
                        NotifyAbilityPreviewDirty(true);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            });
 
            DrawVfxDropdownRow(rightX, y, labelW, fieldW, "CS_Studio_VFX_SourceModeShort".Translate(), GetVfxSecondarySelectorLabel(vfx), () =>
            {
                if (UsesTextureSourceSelector(vfx))
                {
                    var sourceOptions = new List<FloatMenuOption>();
                    foreach (AbilityVisualEffectTextureSource source in Enum.GetValues(typeof(AbilityVisualEffectTextureSource)))
                    {
                        var captured = source;
                        sourceOptions.Add(new FloatMenuOption(GetVfxTextureSourceLabel(captured), () =>
                        {
                            vfx.textureSource = captured;
                            NotifyAbilityPreviewDirty(true);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(sourceOptions));
                    return;
                }

                if (UsesPresetSource(vfx))
                {
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        new FloatMenuOption(GetVfxSourceModeLabel(AbilityVisualEffectSourceMode.Preset), null)
                    }));
                    return;
                }

                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption(GetVfxSourceModeLabel(AbilityVisualEffectSourceMode.BuiltIn), null)
                }));
            });
            y += RowHeight;
 
            if (UsesPresetSource(vfx))
            {
                float presetLabelW = 44f;
                DrawVfxDropdownRow(inner.x, y, presetLabelW, inner.width - presetLabelW - 4f, "CS_Studio_VFX_PresetShort".Translate(), GetPresetSelectionLabel(vfx), () =>
                {
                    IReadOnlyList<string> registeredPresetNames = VisualEffectWorkerFactory.GetRegisteredPresetNames();
                    var options = new List<FloatMenuOption>();
                    for (int i = 0; i < registeredPresetNames.Count; i++)
                    {
                        string presetName = registeredPresetNames[i];
                        options.Add(new FloatMenuOption(presetName, () =>
                        {
                            vfx.presetDefName = presetName;
                            NotifyAbilityPreviewDirty(true);
                        }));
                    }

                    if (options.Count == 0)
                    {
                        options.Add(new FloatMenuOption("CS_Studio_VFX_NoPresetsRegistered".Translate(), null));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                });
                y += RowHeight;
            }
            else if (UsesCustomTextureSettings(vfx))
            {
                float pathLabelWidth = Mathf.Max(44f, Text.CalcSize("CS_Studio_VFX_TextureShort".Translate()).x + 4f);
                float browseButtonWidth = vfx.textureSource == AbilityVisualEffectTextureSource.LocalPath ? 30f : 0f;
                float pathSpacing = browseButtonWidth > 0f ? 4f : 0f;
                float pathFieldWidth = Mathf.Max(40f, inner.width - pathLabelWidth - browseButtonWidth - pathSpacing);
                Widgets.Label(new Rect(inner.x, y, pathLabelWidth, 24f), "CS_Studio_VFX_TextureShort".Translate());
                string texturePathBefore = vfx.customTexturePath ?? string.Empty;
                vfx.customTexturePath = Widgets.TextField(new Rect(inner.x + pathLabelWidth, y, pathFieldWidth, 24f), vfx.customTexturePath ?? string.Empty);
                if ((vfx.customTexturePath ?? string.Empty) != texturePathBefore)
                {
                    NotifyAbilityPreviewDirty();
                }
                if (vfx.textureSource == AbilityVisualEffectTextureSource.LocalPath)
                {
                    if (DrawToolbarButton(new Rect(inner.x + pathLabelWidth + pathFieldWidth + pathSpacing, y, browseButtonWidth, 24f), "...", () =>
                    {
                        Find.WindowStack.Add(new Dialog_FileBrowser(GetAbilityTextureBrowseStartPath(vfx.customTexturePath), path =>
                        {
                            vfx.customTexturePath = path ?? string.Empty;
                            NotifyAbilityPreviewDirty();
                        }));
                    }, true))
                    {
                    }
                }
                y += RowHeight;
            }

            DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_TargetShort".Translate(), GetVfxTargetLabel(vfx.target), () =>
            {
                var options = new List<FloatMenuOption>();
                foreach (VisualEffectTarget t in Enum.GetValues(typeof(VisualEffectTarget)))
                {
                    var captured = t;
                    options.Add(new FloatMenuOption(GetVfxTargetLabel(captured), () =>
                    {
                        vfx.target = captured;
                        NotifyAbilityPreviewDirty(true);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            });
            DrawVfxDropdownRow(rightX, y, labelW, fieldW, "CS_Studio_VFX_TriggerShort".Translate(), GetVfxTriggerLabel(vfx.trigger), () =>
            {
                var options = new List<FloatMenuOption>();
                foreach (AbilityVisualEffectTrigger trigger in Enum.GetValues(typeof(AbilityVisualEffectTrigger)))
                {
                    if (!SupportsRuntimeVfxTrigger(trigger))
                    {
                        continue;
                    }

                    var captured = trigger;
                    options.Add(new FloatMenuOption(GetVfxTriggerLabel(captured), () =>
                    {
                        vfx.trigger = captured;
                        NotifyAbilityPreviewDirty(true);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            });
            y += RowHeight;

            string delayStr = vfx.delayTicks.ToString();
            DrawIntRow(y, inner.x, "CS_Studio_VFX_DelayShort".Translate(), ref vfx.delayTicks, ref delayStr, 0, 60000);
            string durationStr = vfx.displayDurationTicks.ToString();
            DrawIntRow(y, rightX, "CS_Studio_VFX_DisplayDurationShort".Translate(), ref vfx.displayDurationTicks, ref durationStr, 1, 60000);
            y += RowHeight;

            string scaleStr = vfx.scale.ToString("F2");
            DrawNumberRow(y, inner.x, "CS_Studio_VFX_ScaleShort".Translate(), ref vfx.scale, ref scaleStr, 0.1f, 5f);
            string expressionDurationStr = vfx.linkedExpressionDurationTicks.ToString();
            DrawIntRow(y, rightX, "CS_Studio_VFX_ExpressionDurationShort".Translate(), ref vfx.linkedExpressionDurationTicks, ref expressionDurationStr, 1, 60000);
            y += RowHeight;

            if (UsesCustomTextureSettings(vfx))
            {
                string drawSizeStr = vfx.drawSize.ToString("F2");
                DrawNumberRow(y, inner.x, "CS_Studio_VFX_DrawSizeShort".Translate(), ref vfx.drawSize, ref drawSizeStr, 0.1f, 20f);
                string rotationStr = vfx.rotation.ToString("F1");
                DrawNumberRow(y, rightX, "CS_Studio_VFX_RotationShort".Translate(), ref vfx.rotation, ref rotationStr, -360f, 360f);
                y += RowHeight;

                string scaleXStr = vfx.textureScale.x.ToString("F2");
                float scaleX = vfx.textureScale.x;
                DrawNumberRow(y, inner.x, "CS_Studio_VFX_ScaleXShort".Translate(), ref scaleX, ref scaleXStr, 0.1f, 20f);
                string scaleYStr = vfx.textureScale.y.ToString("F2");
                float scaleY = vfx.textureScale.y;
                DrawNumberRow(y, rightX, "CS_Studio_VFX_ScaleYShort".Translate(), ref scaleY, ref scaleYStr, 0.1f, 20f);
                if (Math.Abs(vfx.textureScale.x - scaleX) > 0.001f || Math.Abs(vfx.textureScale.y - scaleY) > 0.001f)
                {
                    vfx.textureScale = new Vector2(scaleX, scaleY);
                    NotifyAbilityPreviewDirty();
                }
                y += RowHeight;

                DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_FacingModeShort".Translate(), GetVfxFacingModeLabel(vfx.facingMode), () =>
                {
                    var options = new List<FloatMenuOption>();
                    foreach (AbilityVisualFacingMode facingMode in Enum.GetValues(typeof(AbilityVisualFacingMode)))
                    {
                        AbilityVisualFacingMode captured = facingMode;
                        options.Add(new FloatMenuOption(GetVfxFacingModeLabel(captured), () =>
                        {
                            vfx.facingMode = captured;
                            vfx.SyncLegacyFields();
                            NotifyAbilityPreviewDirty();
                        }));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                });
                string heightStr = vfx.heightOffset.ToString("F2");
                DrawNumberRow(y, rightX, "CS_Studio_VFX_HeightShort".Translate(), ref vfx.heightOffset, ref heightStr, -10f, 10f);
                y += RowHeight;

                string forwardStr = vfx.forwardOffset.ToString("F2");
                DrawNumberRow(y, inner.x, "CS_Studio_VFX_ForwardOffsetShort".Translate(), ref vfx.forwardOffset, ref forwardStr, -20f, 20f);
                string sideStr = vfx.sideOffset.ToString("F2");
                DrawNumberRow(y, rightX, "CS_Studio_VFX_SideOffsetShort".Translate(), ref vfx.sideOffset, ref sideStr, -20f, 20f);
                y += RowHeight;

                Text.Font = GameFont.Tiny;
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(inner.x, y + 3f, inner.width, 18f), "CS_Studio_VFX_FacingModeHint".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += RowHeight;
            }

            if (vfx.type == AbilityVisualEffectType.LineTexture || vfx.type == AbilityVisualEffectType.WallTexture)
            {
                DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_SpatialShort".Translate(), GetVfxSpatialModeLabel(vfx.spatialMode), () =>
                {
                    var options = new List<FloatMenuOption>();
                    foreach (AbilityVisualSpatialMode spatialMode in Enum.GetValues(typeof(AbilityVisualSpatialMode)))
                    {
                        AbilityVisualSpatialMode captured = spatialMode;
                        options.Add(new FloatMenuOption(GetVfxSpatialModeLabel(captured), () =>
                        {
                            vfx.spatialMode = captured;
                            NotifyAbilityPreviewDirty(true);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                });
                DrawVfxDropdownRow(rightX, y, labelW, fieldW, "CS_Studio_VFX_PathShort".Translate(), GetVfxPathModeLabel(vfx.pathMode), () =>
                {
                    var options = new List<FloatMenuOption>();
                    foreach (AbilityVisualPathMode pathMode in Enum.GetValues(typeof(AbilityVisualPathMode)))
                    {
                        AbilityVisualPathMode captured = pathMode;
                        options.Add(new FloatMenuOption(GetVfxPathModeLabel(captured), () =>
                        {
                            vfx.pathMode = captured;
                            NotifyAbilityPreviewDirty(true);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                });
                y += RowHeight;

                DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_AnchorShort".Translate(), GetVfxAnchorModeLabel(vfx.anchorMode), () =>
                {
                    var options = new List<FloatMenuOption>();
                    foreach (AbilityVisualAnchorMode anchorMode in Enum.GetValues(typeof(AbilityVisualAnchorMode)))
                    {
                        AbilityVisualAnchorMode captured = anchorMode;
                        options.Add(new FloatMenuOption(GetVfxAnchorModeLabel(captured), () =>
                        {
                            vfx.anchorMode = captured;
                            NotifyAbilityPreviewDirty(true);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                });
                DrawVfxDropdownRow(rightX, y, labelW, fieldW, "CS_Studio_VFX_AnchorBShort".Translate(), GetVfxAnchorModeLabel(vfx.secondaryAnchorMode), () =>
                {
                    var options = new List<FloatMenuOption>();
                    foreach (AbilityVisualAnchorMode anchorMode in Enum.GetValues(typeof(AbilityVisualAnchorMode)))
                    {
                        AbilityVisualAnchorMode captured = anchorMode;
                        options.Add(new FloatMenuOption(GetVfxAnchorModeLabel(captured), () =>
                        {
                            vfx.secondaryAnchorMode = captured;
                            NotifyAbilityPreviewDirty(true);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                });
                y += RowHeight;

                if (vfx.type == AbilityVisualEffectType.LineTexture)
                {
                    string lineWidthStr = vfx.lineWidth.ToString("F2");
                    DrawNumberRow(y, inner.x, "CS_Studio_VFX_LineWidthShort".Translate(), ref vfx.lineWidth, ref lineWidthStr, 0.05f, 20f);
                }
                else
                {
                    string wallHeightStr = vfx.wallHeight.ToString("F2");
                    DrawNumberRow(y, inner.x, "CS_Studio_VFX_WallHeightShort".Translate(), ref vfx.wallHeight, ref wallHeightStr, 0.05f, 30f);
                    string wallThicknessStr = vfx.wallThickness.ToString("F2");
                    DrawNumberRow(y, rightX, "CS_Studio_VFX_WallThicknessShort".Translate(), ref vfx.wallThickness, ref wallThicknessStr, 0.05f, 20f);
                }

                string segmentCountStr = vfx.segmentCount.ToString();
                DrawIntRow(y, rightX, "CS_Studio_VFX_SegmentsShort".Translate(), ref vfx.segmentCount, ref segmentCountStr, 1, 512);
                y += RowHeight;
            }

            string repeatCountStr = vfx.repeatCount.ToString();
            DrawIntRow(y, inner.x, "CS_Studio_VFX_RepeatCountShort".Translate(), ref vfx.repeatCount, ref repeatCountStr, 1, 999);
            string repeatIntervalStr = vfx.repeatIntervalTicks.ToString();
            DrawIntRow(y, rightX, "CS_Studio_VFX_RepeatIntervalShort".Translate(), ref vfx.repeatIntervalTicks, ref repeatIntervalStr, 0, 60000);
            y += RowHeight;

            DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_LinkedExpressionShort".Translate(), GetLinkedExpressionLabel(vfx.linkedExpression), () =>
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("CS_Studio_VFX_LinkedExpression_None".Translate(), () =>
                    {
                        vfx.linkedExpression = null;
                        NotifyAbilityPreviewDirty();
                    })
                };

                foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
                {
                    var captured = expression;
                    options.Add(new FloatMenuOption(captured.ToString(), () =>
                    {
                        vfx.linkedExpression = captured;
                        NotifyAbilityPreviewDirty();
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            });

            string pupilBrightnessStr = vfx.linkedPupilBrightnessOffset.ToString("F2");
            DrawNumberRow(y, rightX, "CS_Studio_VFX_PupilBrightnessShort".Translate(), ref vfx.linkedPupilBrightnessOffset, ref pupilBrightnessStr, -2f, 2f);
            y += RowHeight;

            string pupilContrastStr = vfx.linkedPupilContrastOffset.ToString("F2");
            DrawNumberRow(y, inner.x, "CS_Studio_VFX_PupilContrastShort".Translate(), ref vfx.linkedPupilContrastOffset, ref pupilContrastStr, -2f, 2f);
        }

        private void ShowAddVfxMenu()
        {
            var options = new List<FloatMenuOption>();
            foreach (AbilityVisualEffectType t in Enum.GetValues(typeof(AbilityVisualEffectType)))
            {
                var captured = t;
                options.Add(new FloatMenuOption(GetVfxTypeLabel(captured), () =>
                {
                    AbilityVisualEffectConfig vfx = new AbilityVisualEffectConfig
                    {
                        type = captured,
                        target = VisualEffectTarget.Target,
                        trigger = AbilityVisualEffectTrigger.OnTargetApply,
                        delayTicks = 0,
                        scale = 1f,
                        textureSource = AbilityVisualEffectTextureSource.Vanilla
                    };

                    if (vfx.UsesPresetType)
                    {
                        IReadOnlyList<string> registeredPresetNames = VisualEffectWorkerFactory.GetRegisteredPresetNames();
                        vfx.presetDefName = registeredPresetNames.Count > 0 ? registeredPresetNames[0] : string.Empty;
                    }

                    vfx.SyncLegacyFields();
                    selectedAbility?.visualEffects.Add(vfx);
                    NotifyAbilityPreviewDirty(true);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string GetLinkedExpressionLabel(ExpressionType? expression)
        {
            return expression.HasValue ? expression.Value.ToString() : "CS_Studio_VFX_LinkedExpression_None".Translate();
        }

        private static string GetVfxTypeLabel(AbilityVisualEffectType type)
        {
            return ("CS_Studio_VFX_Type_" + type).Translate();
        }

        private static string GetVfxTargetLabel(VisualEffectTarget target)
        {
            return ("CS_Studio_VFX_Target_" + target).Translate();
        }

        private static string GetVfxSourceModeLabel(AbilityVisualEffectSourceMode mode)
        {
            return mode switch
            {
                AbilityVisualEffectSourceMode.BuiltIn => "CS_Studio_VFX_SourceMode_BuiltIn".Translate(),
                AbilityVisualEffectSourceMode.Preset => "CS_Studio_VFX_SourceMode_Preset".Translate(),
                AbilityVisualEffectSourceMode.CustomTexture => "CS_Studio_VFX_SourceMode_CustomTexture".Translate(),
                _ => mode.ToString()
            };
        }

        private static string GetVfxTextureSourceLabel(AbilityVisualEffectTextureSource source)
        {
            return source switch
            {
                AbilityVisualEffectTextureSource.Vanilla => "CS_Studio_VFX_TextureSource_Vanilla".Translate(),
                AbilityVisualEffectTextureSource.LocalPath => "CS_Studio_VFX_TextureSource_LocalPath".Translate(),
                _ => source.ToString()
            };
        }

        private static string GetVfxSecondarySelectorLabel(AbilityVisualEffectConfig vfx)
        {
            if (vfx.UsesCustomTextureType)
            {
                return GetVfxTextureSourceLabel(vfx.textureSource);
            }

            if (vfx.UsesPresetType)
            {
                return GetVfxSourceModeLabel(AbilityVisualEffectSourceMode.Preset);
            }

            return GetVfxSourceModeLabel(AbilityVisualEffectSourceMode.BuiltIn);
        }

        private static string GetVfxTriggerLabel(AbilityVisualEffectTrigger trigger)
        {
            return trigger switch
            {
                AbilityVisualEffectTrigger.OnCastStart => "CS_Studio_VFX_Trigger_OnCastStart".Translate(),
                AbilityVisualEffectTrigger.OnWarmup => "CS_Studio_VFX_Trigger_OnWarmup".Translate(),
                AbilityVisualEffectTrigger.OnCastFinish => "CS_Studio_VFX_Trigger_OnCastFinish".Translate(),
                AbilityVisualEffectTrigger.OnTargetApply => "CS_Studio_VFX_Trigger_OnTargetApply".Translate(),
                AbilityVisualEffectTrigger.OnDurationTick => "CS_Studio_VFX_Trigger_OnDurationTick".Translate(),
                AbilityVisualEffectTrigger.OnExpire => "CS_Studio_VFX_Trigger_OnExpire".Translate(),
                _ => trigger.ToString()
            };
        }

        private static string GetVfxFacingModeLabel(AbilityVisualFacingMode facingMode)
        {
            return ($"CS_Studio_VFX_FacingMode_{facingMode}").Translate();
        }

        private static string GetVfxSpatialModeLabel(AbilityVisualSpatialMode spatialMode)
        {
            return ($"CS_Studio_VFX_SpatialMode_{spatialMode}").Translate();
        }

        private static string GetVfxAnchorModeLabel(AbilityVisualAnchorMode anchorMode)
        {
            return ($"CS_Studio_VFX_AnchorMode_{anchorMode}").Translate();
        }

        private static string GetVfxPathModeLabel(AbilityVisualPathMode pathMode)
        {
            return ($"CS_Studio_VFX_PathMode_{pathMode}").Translate();
        }

        private void DrawVfxDropdownRow(float x, float y, float labelW, float fieldW, string label, string value, Action onClick)
        {
            Widgets.Label(new Rect(x, y, labelW, 24f), label);
            if (DrawSelectionFieldButton(new Rect(x + labelW, y, fieldW, 24f), value, onClick))
            {
            }
        }

        private void SwapVfx(int indexA, int indexB)
        {
            if (selectedAbility == null || selectedAbility.visualEffects == null) return;
            if (indexA < 0 || indexB < 0 || indexA >= selectedAbility.visualEffects.Count || indexB >= selectedAbility.visualEffects.Count)
            {
                return;
            }

            var temp = selectedAbility.visualEffects[indexA];
            selectedAbility.visualEffects[indexA] = selectedAbility.visualEffects[indexB];
            selectedAbility.visualEffects[indexB] = temp;
            NotifyAbilityPreviewDirty(true);
        }
    }
}
