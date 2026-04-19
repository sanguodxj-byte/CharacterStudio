using System;
using System.Collections.Generic;
using System.Linq;
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
            float height = 50f; // header: title(22) + summary(18) + spacing(10)

            // 全局滤镜类型：紧凑布局（类型 + 触发 + 延时/持续 + 全局滤镜区域）
            if (vfx.IsGlobalFilterType)
            {
                height += SectionPadding; // Basic section
                height += RowHeight * 3;  // Type + Trigger + Delay/Duration
                // 全局滤镜区域：分隔线(4) + 标题(20) + SectionPadding + 2行
                height += 4f + 20f + SectionPadding + RowHeight * 2;
                return height + 10f;
            }

            // Section 1: 基础设置
            int basicRows = 4; // Type+Source, Target+Trigger, Delay+Duration, Scale+Expression
            if (UsesPresetSource(vfx) || UsesTextureSourceSelector(vfx)) basicRows++;
            height += SectionPadding + basicRows * RowHeight;

            // Section 2: 贴图设置
            if (UsesCustomTextureSettings(vfx))
            {
                int texRows = 6; // DrawSize+Rot, ScaleX+Y, Facing+Height, Forward+Side, VfxSourceLayer, Hint
                if (vfx.enableFrameAnimation) texRows += 2;
                height += SectionPadding + texRows * RowHeight;
            }

            // Section 3: 空间设置
            if (vfx.type == AbilityVisualEffectType.LineTexture || vfx.type == AbilityVisualEffectType.WallTexture)
            {
                height += SectionPadding + RowHeight * 3;
            }

            // Section 4: 动画/表情
            height += SectionPadding + RowHeight * 3;

            // 全局滤镜区域：分隔线(4) + 标题(20) + SectionPadding + 2行
            height += 4f + 20f + SectionPadding + RowHeight * 2;
            return height + 10f;
        }

        private string BuildVfxTitleLabel(AbilityVisualEffectConfig vfx, int index)
        {
            string titleLabel = $"#{index + 1} {GetVfxCategoryLabel(vfx)}";
            if (UsesPresetSource(vfx) && !string.IsNullOrWhiteSpace(vfx.presetDefName))
            {
                titleLabel += $" [{GetVfxPresetDisplayName(vfx.presetDefName)}]";
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
            string summary = "CS_Studio_VFX_SummaryLine".Translate(
                GetVfxTargetLabel(vfx.target),
                GetVfxTriggerLabel(vfx.trigger),
                facingText);

            string descKey = $"CS_Studio_VFX_Desc_{vfx.type}";
            if (descKey.CanTranslate())
            {
                summary += " · " + descKey.Translate();
            }

            return summary;
        }

        private void DrawVfxItem(Rect rect, AbilityVisualEffectConfig vfx, int index)
        {
            // Draw background with subtle color coding
            Color bgColor = vfx.enabled ? UIHelper.PanelFillSoftColor : new Color(0.12f, 0.12f, 0.14f, 0.9f);
            Widgets.DrawBoxSolid(rect, bgColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect inner = rect.ContractedBy(6f);

            if (NormalizeVfxEditorState(vfx))
            {
                NotifyAbilityPreviewDirty(true);
            }

            // ── Row 1: Title + action buttons ──
            float headerY = inner.y;

            // Title label (left side)
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Small;
            GUI.color = vfx.enabled ? UIHelper.HeaderColor : Color.gray;
            string titleText = BuildVfxTitleLabel(vfx, index);
            float titleWidth = inner.width - 70f;
            Widgets.Label(new Rect(inner.x, headerY, titleWidth, 22f), titleText);
            GUI.color = Color.white;
            Text.Font = prevFont;

            // Action buttons (right side, all in one row)
            float btnX = inner.x + inner.width - 66f;
            if (selectedAbility != null && index > 0 && DrawCompactIconButton(new Rect(btnX, headerY, 20f, 22f), "▲", () => SwapVfx(index, index - 1)))
            {
                return;
            }
            btnX += 22f;
            if (selectedAbility != null && index < selectedAbility.visualEffects.Count - 1 && DrawCompactIconButton(new Rect(btnX, headerY, 20f, 22f), "▼", () => SwapVfx(index, index + 1)))
            {
                return;
            }
            btnX += 22f;
            if (DrawCompactIconButton(new Rect(btnX, headerY, 22f, 22f), "X", () =>
            {
                selectedAbility?.visualEffects.RemoveAt(index);
                NotifyAbilityPreviewDirty(true);
            }))
            {
                return;
            }

            // ── Row 2: Summary line (Tiny font, subtle color) ──
            float summaryY = headerY + 22f;
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(inner.x, summaryY, inner.width - 120f, 16f), BuildVfxSecondarySummary(vfx));
            GUI.color = Color.white;
            Text.Font = prevFont;

            // Enabled checkbox (right side of summary row)
            bool enabled = vfx.enabled;
            float checkboxX = inner.x + inner.width - 100f;
            Rect checkboxRect = new Rect(checkboxX, summaryY, 100f, 16f);
            Widgets.Label(new Rect(checkboxRect.x, checkboxRect.y, 60f, checkboxRect.height), "CS_Studio_VFX_Enabled".Translate());
            Widgets.Checkbox(new Vector2(checkboxRect.x + 62f, checkboxRect.y - 2f), ref enabled, 18f, false);
            if (vfx.enabled != enabled)
            {
                vfx.enabled = enabled;
                NotifyAbilityPreviewDirty();
            }

            // ── Separator line ──
            float sepY = summaryY + 18f;
            Widgets.DrawBoxSolid(new Rect(inner.x, sepY, inner.width, 1f), UIHelper.AccentSoftColor);

            // ── Content rows start after header ──
            float y = sepY + 6f;

            // Two-column layout with auto-sizing labels (Tiny font for content rows)
            float gap = 10f;
            float colW = (inner.width - gap) * 0.5f;
            Text.Font = GameFont.Tiny;
            float labelW = 68f;
            float fieldW = Mathf.Max(50f, colW - labelW - 4f);
            float rightX = inner.x + colW + gap;
            Text.Font = prevFont;

            // ── 全局滤镜类型：只绘制基础行 + 全局滤镜区域 ──
            if (vfx.IsGlobalFilterType)
            {
                DrawVfxGlobalFilterOnlySection(inner, y, vfx, labelW, fieldW, rightX, prevFont);
                return;
            }

            void DrawNumberRow(float rowY, float x, string label, ref float value, ref string buffer, float min, float max)
            {
                Text.Font = GameFont.Tiny;
                Rect labelRect = new Rect(x, rowY + 2f, labelW, 20f);
                Widgets.Label(labelRect, label.Truncate(labelRect.width));
                Text.Font = prevFont;
                float before = value;
                UIHelper.TextFieldNumeric(new Rect(x + labelW, rowY, fieldW, 24f), ref value, ref buffer, min, max);
                if (Math.Abs(value - before) > 0.001f)
                {
                    NotifyAbilityPreviewDirty();
                }
            }

            void DrawIntRow(float rowY, float x, string label, ref int value, ref string buffer, int min, int max)
            {
                Text.Font = GameFont.Tiny;
                Rect labelRect = new Rect(x, rowY + 2f, labelW, 20f);
                Widgets.Label(labelRect, label.Truncate(labelRect.width));
                Text.Font = prevFont;
                int before = value;
                UIHelper.TextFieldNumeric(new Rect(x + labelW, rowY, fieldW, 24f), ref value, ref buffer, min, max);
                if (value != before)
                {
                    NotifyAbilityPreviewDirty(true);
                }
            }

            // ── Section 1: 基础设置 ──
            int basicRows = 4; // Type+Source, Target+Trigger, Delay+Duration, Scale+Expression
            if (UsesPresetSource(vfx) || UsesCustomTextureSettings(vfx)) basicRows++;
            DrawSectionBg(inner.x, y, inner.width, basicRows * RowHeight);

            DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_TypeShort".Translate(), GetVfxCategoryLabel(vfx), () =>
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.Preset), () =>
                    {
                        vfx.type = AbilityVisualEffectType.Preset;
                        if (string.IsNullOrWhiteSpace(vfx.presetDefName)
                            || !VisualEffectWorkerFactory.GetRegisteredPresetNames().Contains(vfx.presetDefName))
                        {
                            IReadOnlyList<string> names = VisualEffectWorkerFactory.GetRegisteredPresetNames();
                            vfx.presetDefName = names.Count > 0 ? names[0] : string.Empty;
                        }
                        vfx.textureSource = AbilityVisualEffectTextureSource.Vanilla;
                        vfx.SyncLegacyFields();
                        NotifyAbilityPreviewDirty(true);
                    }),
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.CustomTexture), () =>
                    {
                        vfx.type = AbilityVisualEffectType.CustomTexture;
                        vfx.textureSource = AbilityVisualEffectTextureSource.LocalPath;
                        vfx.SyncLegacyFields();
                        NotifyAbilityPreviewDirty(true);
                    }),
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.LineTexture), () =>
                    {
                        vfx.type = AbilityVisualEffectType.LineTexture;
                        vfx.textureSource = AbilityVisualEffectTextureSource.LocalPath;
                        vfx.pathMode = AbilityVisualPathMode.DirectLineCasterToTarget;
                        vfx.SyncLegacyFields();
                        NotifyAbilityPreviewDirty(true);
                    }),
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.WallTexture), () =>
                    {
                        vfx.type = AbilityVisualEffectType.WallTexture;
                        vfx.textureSource = AbilityVisualEffectTextureSource.LocalPath;
                        vfx.pathMode = AbilityVisualPathMode.DirectLineCasterToTarget;
                        vfx.SyncLegacyFields();
                        NotifyAbilityPreviewDirty(true);
                    }),
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.GlobalFilter), () =>
                    {
                        vfx.type = AbilityVisualEffectType.GlobalFilter;
                        vfx.target = VisualEffectTarget.Caster;
                        NotifyAbilityPreviewDirty(true);
                    })
                };
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
                string presetDisplayLabel = GetVfxPresetDisplayName(vfx.presetDefName);
                DrawVfxDropdownRow(inner.x, y, presetLabelW, inner.width - presetLabelW - 4f, "CS_Studio_VFX_PresetShort".Translate(), presetDisplayLabel, () =>
                {
                    IReadOnlyList<string> registeredPresetNames = VisualEffectWorkerFactory.GetRegisteredPresetNames();
                    var options = new List<FloatMenuOption>();
                    for (int i = 0; i < registeredPresetNames.Count; i++)
                    {
                        string presetName = registeredPresetNames[i];
                        options.Add(new FloatMenuOption(GetVfxPresetDisplayName(presetName), () =>
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
                        }, defaultRoot: GetAbilityTextureRootDir()));
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

            // ── Section 2: 贴图设置（CustomTexture/Line/Wall 共用） ──
            if (UsesCustomTextureSettings(vfx))
            {
                int texRows = 6; // DrawSize+Rot, ScaleX+Y, Facing+Height, Forward+Side, VfxSourceLayer, Hint
                texRows++; // FrameAnim toggle
                if (vfx.enableFrameAnimation) texRows += 2;
                DrawSectionBg(inner.x, y, inner.width, texRows * RowHeight);

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

                // VFX 起点图层名（浮游炮等动态发射源）— 下拉选择
                string currentLayerLabel = string.IsNullOrWhiteSpace(vfx.vfxSourceLayerName)
                    ? "CS_Studio_VFX_SourceLayerNone".Translate()
                    : vfx.vfxSourceLayerName;
                DrawVfxDropdownRow(inner.x, y, labelW, inner.width - labelW - 4f, "CS_Studio_VFX_SourceLayerShort".Translate(), currentLayerLabel, () =>
                {
                    var layerOptions = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("CS_Studio_VFX_SourceLayerNone".Translate(), () =>
                        {
                            vfx.vfxSourceLayerName = string.Empty;
                            NotifyAbilityPreviewDirty();
                        })
                    };

                    if (boundSkin?.layers != null)
                    {
                        foreach (var layer in boundSkin.layers)
                        {
                            if (string.IsNullOrWhiteSpace(layer.layerName)) continue;
                            string captured = layer.layerName;
                            layerOptions.Add(new FloatMenuOption(captured, () =>
                            {
                                vfx.vfxSourceLayerName = captured;
                                NotifyAbilityPreviewDirty();
                            }));
                        }
                    }

                    Find.WindowStack.Add(new FloatMenu(layerOptions));
                });
                y += RowHeight;

                Text.Font = GameFont.Tiny;
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(inner.x, y + 3f, inner.width, 18f), "CS_Studio_VFX_FacingModeHint".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += RowHeight;

                // Frame animation controls
                bool frameAnimBefore = vfx.enableFrameAnimation;
                Widgets.Checkbox(new Vector2(inner.x, y + 2f), ref vfx.enableFrameAnimation, 24f, false);
                Widgets.Label(new Rect(inner.x + 28f, y, inner.width - 28f, RowHeight), "CS_Studio_VFX_FrameAnimToggle".Translate());
                if (vfx.enableFrameAnimation != frameAnimBefore)
                {
                    NotifyAbilityPreviewDirty();
                }
                y += RowHeight;

                if (vfx.enableFrameAnimation)
                {
                    string frameCountStr = vfx.frameCount.ToString();
                    DrawIntRow(y, inner.x, "CS_Studio_VFX_FrameCountShort".Translate(), ref vfx.frameCount, ref frameCountStr, 2, 120);
                    string frameIntervalStr = vfx.frameIntervalTicks.ToString();
                    DrawIntRow(y, rightX, "CS_Studio_VFX_FrameIntervalShort".Translate(), ref vfx.frameIntervalTicks, ref frameIntervalStr, 1, 60000);
                    y += RowHeight;

                    bool frameLoopBefore = vfx.frameLoop;
                    Widgets.Checkbox(new Vector2(inner.x, y + 2f), ref vfx.frameLoop, 24f, false);
                    Widgets.Label(new Rect(inner.x + 28f, y, 120f, RowHeight), "CS_Studio_VFX_FrameLoop".Translate());
                    if (vfx.frameLoop != frameLoopBefore)
                    {
                        NotifyAbilityPreviewDirty();
                    }
                    Text.Font = GameFont.Tiny;
                    GUI.color = UIHelper.SubtleColor;
                    Widgets.Label(new Rect(rightX, y + 3f, inner.width - rightX + inner.x, 18f), "CS_Studio_VFX_FrameAnimHint".Translate());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    y += RowHeight;
                }
            }

            // ── Section 3: 空间设置（Line/Wall） ──
            if (vfx.type == AbilityVisualEffectType.LineTexture || vfx.type == AbilityVisualEffectType.WallTexture)
            {
                DrawSectionBg(inner.x, y, inner.width, 3 * RowHeight);

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

            // ── Section 4: 动画/表情 ──
            DrawSectionBg(inner.x, y, inner.width, 3 * RowHeight);

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
            y += RowHeight;

            // ── 全局滤镜 ──
            DrawVfxGlobalFilterSection(inner, y, vfx, labelW, fieldW, rightX);
        }

        private void DrawVfxGlobalFilterSection(Rect inner, float startY, AbilityVisualEffectConfig vfx, float labelW, float fieldW, float rightX)
        {
            float y = startY;

            // 分隔线
            Widgets.DrawBoxSolid(new Rect(inner.x, y, inner.width, 1f), UIHelper.AccentSoftColor);
            y += 4f;

            // 标题
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f), "CS_Studio_VFX_GlobalFilter".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 20f;

            // Section card 包裹滤镜配置行（2行：模式+过渡，过渡时间）
            DrawSectionBg(inner.x, y, inner.width, 2 * RowHeight);

            // 全局滤镜模式选择
            string filterLabel = string.IsNullOrWhiteSpace(vfx.globalFilterMode)
                ? "CS_Studio_VFX_GlobalFilter_None".Translate()
                : vfx.globalFilterMode;
            DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_GlobalFilterMode".Translate(), filterLabel, () =>
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("CS_Studio_VFX_GlobalFilter_None".Translate(), () =>
                    {
                        vfx.globalFilterMode = string.Empty;
                        NotifyAbilityPreviewDirty();
                    }),
                    new FloatMenuOption("Grayscale", () =>
                    {
                        vfx.globalFilterMode = VfxGlobalFilterManager.ModeGrayscale;
                        NotifyAbilityPreviewDirty();
                    }),
                    new FloatMenuOption("Desaturate", () =>
                    {
                        vfx.globalFilterMode = VfxGlobalFilterManager.ModeDesaturate;
                        NotifyAbilityPreviewDirty();
                    }),
                    new FloatMenuOption("Sepia", () =>
                    {
                        vfx.globalFilterMode = VfxGlobalFilterManager.ModeSepia;
                        NotifyAbilityPreviewDirty();
                    }),
                    new FloatMenuOption("Tint", () =>
                    {
                        vfx.globalFilterMode = VfxGlobalFilterManager.ModeTint;
                        NotifyAbilityPreviewDirty();
                    }),
                    new FloatMenuOption("Negative", () =>
                    {
                        vfx.globalFilterMode = VfxGlobalFilterManager.ModeNegative;
                        NotifyAbilityPreviewDirty();
                    })
                };
                Find.WindowStack.Add(new FloatMenu(options));
            });

            // 过渡方式选择
            string transitionLabel = string.IsNullOrWhiteSpace(vfx.globalFilterTransition)
                ? "CS_Studio_VFX_GlobalFilterTransition_None".Translate()
                : vfx.globalFilterTransition;
            DrawVfxDropdownRow(rightX, y, labelW, fieldW, "CS_Studio_VFX_GlobalFilterTransition".Translate(), transitionLabel, () =>
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("CS_Studio_VFX_GlobalFilterTransition_None".Translate(), () =>
                    {
                        vfx.globalFilterTransition = string.Empty;
                        NotifyAbilityPreviewDirty();
                    }),
                    new FloatMenuOption("Linear", () =>
                    {
                        vfx.globalFilterTransition = "Linear";
                        NotifyAbilityPreviewDirty();
                    }),
                    new FloatMenuOption("EaseIn", () =>
                    {
                        vfx.globalFilterTransition = "EaseIn";
                        NotifyAbilityPreviewDirty();
                    }),
                    new FloatMenuOption("EaseOut", () =>
                    {
                        vfx.globalFilterTransition = "EaseOut";
                        NotifyAbilityPreviewDirty();
                    })
                };
                Find.WindowStack.Add(new FloatMenu(options));
            });
            y += RowHeight;

            // 过渡时间
            string transitionTicksStr = vfx.globalFilterTransitionTicks.ToString();
            void DrawIntRowLocal(float rowY, float x, string label, ref int value, ref string buffer, int min, int max)
            {
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                Rect labelRect = new Rect(x, rowY + 2f, labelW, 20f);
                Widgets.Label(labelRect, label.Truncate(labelRect.width));
                Text.Font = prevFont;
                int before = value;
                UIHelper.TextFieldNumeric(new Rect(x + labelW, rowY, fieldW, 24f), ref value, ref buffer, min, max);
                if (value != before)
                {
                    NotifyAbilityPreviewDirty(true);
                }
            }

            DrawIntRowLocal(y, inner.x, "CS_Studio_VFX_GlobalFilterTransitionTicks".Translate(),
                ref vfx.globalFilterTransitionTicks, ref transitionTicksStr, 0, 600);
        }

        /// <summary>
        /// 全局滤镜类型专用绘制：只显示类型、触发时机、延时、持续时间和全局滤镜配置。
        /// </summary>
        private void DrawVfxGlobalFilterOnlySection(Rect inner, float startY, AbilityVisualEffectConfig vfx, float labelW, float fieldW, float rightX, GameFont prevFont)
        {
            float y = startY;
            float gap = 10f;
            float colW = (inner.width - gap) * 0.5f;

            void DrawIntRowLocal(float rowY, float x, string label, ref int value, ref string buffer, int min, int max)
            {
                Text.Font = GameFont.Tiny;
                Rect labelRect = new Rect(x, rowY + 2f, labelW, 20f);
                Widgets.Label(labelRect, label.Truncate(labelRect.width));
                Text.Font = prevFont;
                int before = value;
                UIHelper.TextFieldNumeric(new Rect(x + labelW, rowY, fieldW, 24f), ref value, ref buffer, min, max);
                if (value != before)
                {
                    NotifyAbilityPreviewDirty(true);
                }
            }

            // ── Section: 基础设置 ──
            DrawSectionBg(inner.x, y, inner.width, 3 * RowHeight);

            // 类型（复用大类下拉）
            DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_TypeShort".Translate(), GetVfxCategoryLabel(vfx), () =>
            {
                var typeOptions = new List<FloatMenuOption>
                {
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.Preset), () => { vfx.type = AbilityVisualEffectType.Preset; NotifyAbilityPreviewDirty(true); }),
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.CustomTexture), () => { vfx.type = AbilityVisualEffectType.CustomTexture; NotifyAbilityPreviewDirty(true); }),
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.LineTexture), () => { vfx.type = AbilityVisualEffectType.LineTexture; NotifyAbilityPreviewDirty(true); }),
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.WallTexture), () => { vfx.type = AbilityVisualEffectType.WallTexture; NotifyAbilityPreviewDirty(true); }),
                    new FloatMenuOption(GetVfxCategoryLabel(AbilityVisualEffectType.GlobalFilter), () => { vfx.type = AbilityVisualEffectType.GlobalFilter; NotifyAbilityPreviewDirty(true); })
                };
                Find.WindowStack.Add(new FloatMenu(typeOptions));
            });
            y += RowHeight;

            // 触发时机
            DrawVfxDropdownRow(inner.x, y, labelW, fieldW, "CS_Studio_VFX_TriggerShort".Translate(), GetVfxTriggerLabel(vfx.trigger), () =>
            {
                var triggerOptions = new List<FloatMenuOption>();
                foreach (AbilityVisualEffectTrigger trigger in Enum.GetValues(typeof(AbilityVisualEffectTrigger)))
                {
                    var captured = trigger;
                    triggerOptions.Add(new FloatMenuOption(GetVfxTriggerLabel(captured), () =>
                    {
                        vfx.trigger = captured;
                        NotifyAbilityPreviewDirty(true);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(triggerOptions));
            });
            y += RowHeight;

            // 延时 + 持续时间
            string delayStr = vfx.delayTicks.ToString();
            DrawIntRowLocal(y, inner.x, "CS_Studio_VFX_DelayShort".Translate(), ref vfx.delayTicks, ref delayStr, 0, 60000);
            string durationStr = vfx.displayDurationTicks.ToString();
            DrawIntRowLocal(y, rightX, "CS_Studio_VFX_DisplayDurationShort".Translate(), ref vfx.displayDurationTicks, ref durationStr, 1, 60000);
            y += RowHeight;

            // 全局滤镜配置
            DrawVfxGlobalFilterSection(inner, y, vfx, labelW, fieldW, rightX);
        }

        private void ShowAddVfxMenu()
        {
            var options = new List<FloatMenuOption>();

            // 预设特效——添加后在展开字段中选择具体预设
            options.Add(new FloatMenuOption("CS_Studio_VFX_AddCategory_Preset".Translate(), () =>
            {
                IReadOnlyList<string> names = VisualEffectWorkerFactory.GetRegisteredPresetNames();
                var vfx = new AbilityVisualEffectConfig
                {
                    type = AbilityVisualEffectType.Preset,
                    presetDefName = names.Count > 0 ? names[0] : string.Empty,
                    target = VisualEffectTarget.Target,
                    trigger = AbilityVisualEffectTrigger.OnTargetApply,
                    delayTicks = 0,
                    scale = 1f,
                    textureSource = AbilityVisualEffectTextureSource.Vanilla
                };
                vfx.SyncLegacyFields();
                selectedAbility?.visualEffects.Add(vfx);
                NotifyAbilityPreviewDirty(true);
            }));

            // 自定义贴图
            options.Add(new FloatMenuOption("CS_Studio_VFX_AddCategory_CustomTexture".Translate(), () =>
            {
                var vfx = new AbilityVisualEffectConfig
                {
                    type = AbilityVisualEffectType.CustomTexture,
                    target = VisualEffectTarget.Target,
                    trigger = AbilityVisualEffectTrigger.OnTargetApply,
                    delayTicks = 0,
                    scale = 1f,
                    textureSource = AbilityVisualEffectTextureSource.LocalPath
                };
                vfx.SyncLegacyFields();
                selectedAbility?.visualEffects.Add(vfx);
                NotifyAbilityPreviewDirty(true);
            }));

            // 线段贴图
            options.Add(new FloatMenuOption("CS_Studio_VFX_AddCategory_LineTexture".Translate(), () =>
            {
                var vfx = new AbilityVisualEffectConfig
                {
                    type = AbilityVisualEffectType.LineTexture,
                    target = VisualEffectTarget.Target,
                    trigger = AbilityVisualEffectTrigger.OnTargetApply,
                    delayTicks = 0,
                    scale = 1f,
                    textureSource = AbilityVisualEffectTextureSource.LocalPath,
                    pathMode = AbilityVisualPathMode.DirectLineCasterToTarget
                };
                vfx.SyncLegacyFields();
                selectedAbility?.visualEffects.Add(vfx);
                NotifyAbilityPreviewDirty(true);
            }));

            // 墙壁贴图
            options.Add(new FloatMenuOption("CS_Studio_VFX_AddCategory_WallTexture".Translate(), () =>
            {
                var vfx = new AbilityVisualEffectConfig
                {
                    type = AbilityVisualEffectType.WallTexture,
                    target = VisualEffectTarget.Target,
                    trigger = AbilityVisualEffectTrigger.OnTargetApply,
                    delayTicks = 0,
                    scale = 1f,
                    textureSource = AbilityVisualEffectTextureSource.LocalPath,
                    pathMode = AbilityVisualPathMode.DirectLineCasterToTarget
                };
                vfx.SyncLegacyFields();
                selectedAbility?.visualEffects.Add(vfx);
                NotifyAbilityPreviewDirty(true);
            }));

            // 全局滤镜
            options.Add(new FloatMenuOption("CS_Studio_VFX_AddCategory_GlobalFilter".Translate(), () =>
            {
                var vfx = new AbilityVisualEffectConfig
                {
                    type = AbilityVisualEffectType.GlobalFilter,
                    target = VisualEffectTarget.Caster,
                    trigger = AbilityVisualEffectTrigger.OnTargetApply,
                    delayTicks = 0,
                    displayDurationTicks = 60,
                    scale = 1f
                };
                vfx.SyncLegacyFields();
                selectedAbility?.visualEffects.Add(vfx);
                NotifyAbilityPreviewDirty(true);
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// 获取预设特效的显示名称（带翻译）。
        /// </summary>
        private static string GetVfxPresetDisplayName(string presetName)
        {
            string translationKey = "CS_Studio_VFX_Preset_" + presetName;
            if (translationKey.CanTranslate())
            {
                return translationKey.Translate();
            }
            return presetName;
        }

        private static string GetLinkedExpressionLabel(ExpressionType? expression)
        {
            return expression.HasValue ? expression.Value.ToString() : "CS_Studio_VFX_LinkedExpression_None".Translate();
        }

        private static string GetVfxTypeLabel(AbilityVisualEffectType type)
        {
            return ("CS_Studio_VFX_Type_" + type).Translate();
        }

        /// <summary>
        /// 获取 VFX 大类标签（Preset/CustomTexture/LineTexture/WallTexture）。
        /// </summary>
        private static string GetVfxCategoryLabel(AbilityVisualEffectConfig vfx)
        {
            return GetVfxCategoryLabel(vfx.type);
        }

        private static string GetVfxCategoryLabel(AbilityVisualEffectType type)
        {
            // 内置特效类型（DustPuff~FlameSurge）统一显示为"预设特效"
            if (type != AbilityVisualEffectType.CustomTexture
                && type != AbilityVisualEffectType.LineTexture
                && type != AbilityVisualEffectType.WallTexture
                && type != AbilityVisualEffectType.GlobalFilter)
            {
                return "CS_Studio_VFX_Category_Preset".Translate();
            }
            return ("CS_Studio_VFX_Category_" + type).Translate();
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
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(x, y + 2f, labelW, 20f), label.Truncate(labelW));
            Text.Font = prevFont;
            if (DrawSelectionFieldButton(new Rect(x + labelW, y, fieldW, 24f), value.Truncate(fieldW), onClick))
            {
            }
        }

        /// <summary>
        /// 画一个 section card 背景（先画背景，再在上面画控件）。
        /// padding 每侧 3px。
        /// </summary>
        private static void DrawSectionBg(float x, float y, float width, float contentHeight)
        {
            float pad = 3f;
            Rect bgRect = new Rect(x - pad, y - pad, width + pad * 2f, contentHeight + pad * 2f);
            Widgets.DrawBoxSolid(bgRect, new Color(0.16f, 0.17f, 0.20f, 0.6f));
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            Widgets.DrawBox(bgRect, 1);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 计算 section card 的额外垂直 padding（上下各 3px + 间距）。
        /// </summary>
        private const float SectionPadding = 10f; // 3 top + 3 bottom + 4 gap

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