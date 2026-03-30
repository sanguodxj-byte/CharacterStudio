using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Rendering;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private readonly struct ScannedOverlayCandidate
        {
            public readonly string FilePath;
            public readonly string FileName;
            public readonly string SuggestedOverlayId;

            public ScannedOverlayCandidate(string filePath, string fileName, string suggestedOverlayId)
            {
                FilePath = filePath;
                FileName = fileName;
                SuggestedOverlayId = suggestedOverlayId;
            }
        }

        /// <summary>
        /// 绘制表情配置面板
        /// </summary>
        private static string GetExpressionTypeLabel(ExpressionType expression)
        {
            return ($"CS_Expression_{expression}").Translate();
        }

        private static string GetWorkerLabel(string workerKey)
        {
            return ($"CS_Studio_Worker_{workerKey}").Translate();
        }

        private static string GetFaceWorkflowModeLabel(FaceWorkflowMode mode)
        {
            return ($"CS_Studio_Face_Workflow_{mode}").Translate();
        }

        private static string GetLayeredFacePartTypeLabel(LayeredFacePartType partType)
        {
            return ($"CS_Studio_Face_LayeredPart_{partType}").Translate();
        }

        private static string GetLayeredPartBufferKey(
            LayeredFacePartType partType,
            ExpressionType expression,
            string? overlayId = null,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            if (PawnFaceConfig.IsOverlayPart(partType))
            {
                string resolvedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
                return $"{partType}|{resolvedOverlayId}|{expression}";
            }

            LayeredFacePartSide normalizedSide = PawnFaceConfig.NormalizePartSide(partType, side);
            return normalizedSide == LayeredFacePartSide.None
                ? $"{partType}|{expression}"
                : $"{partType}|{normalizedSide}|{expression}";
        }

        private static string NormalizePathForComparison(string? path)
        {
            string normalizedPath = path ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalizedPath)
                ? string.Empty
                : normalizedPath.Trim().Replace('\\', '/');
        }

        private static bool ArePathStringsEquivalent(string? left, string? right)
        {
            return string.Equals(
                NormalizePathForComparison(left),
                NormalizePathForComparison(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool DrawFaceToolbarButton(Rect buttonRect, string label, string tooltip, Action action, bool accent = false)
        {
            Widgets.DrawBoxSolid(buttonRect, accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(
                new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f),
                accent ? UIHelper.AccentColor : new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(buttonRect, 1);
            GUI.color = Color.white;

            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = accent ? Color.white : UIHelper.HeaderColor;
            Widgets.Label(buttonRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = prevFont;

            TooltipHandler.TipRegion(buttonRect, tooltip);
            if (Widgets.ButtonInvisible(buttonRect))
            {
                action();
                return true;
            }

            return false;
        }

        private void DrawFaceInfoBanner(ref float y, float width, string text, bool accent = false)
        {
            Text.Font = GameFont.Tiny;
            float textHeight = Text.CalcHeight(text, Mathf.Max(40f, width - 16f));
            float bannerHeight = Mathf.Max(36f, textHeight + 12f);
            Rect bannerRect = new Rect(0f, y, width, bannerHeight);
            Widgets.DrawBoxSolid(
                bannerRect,
                accent
                    ? new Color(UIHelper.AccentColor.r, UIHelper.AccentColor.g, UIHelper.AccentColor.b, 0.12f)
                    : UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(bannerRect, 1);
            GUI.color = accent ? UIHelper.HeaderColor : UIHelper.SubtleColor;
            Widgets.Label(new Rect(bannerRect.x + 8f, bannerRect.y + 4f, bannerRect.width - 16f, bannerRect.height - 8f), text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += bannerHeight + 4f;
        }

        private void DrawFacePanel(Rect rect)
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
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), "CS_Studio_Face_Title".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            PawnFaceConfig? fc = workingSkin.faceConfig;
            float contentY = titleRect.yMax + 8f;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            Widgets.DrawBoxSolid(contentRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(contentRect, 1);
            GUI.color = Color.white;

            int exprCount = Enum.GetValues(typeof(ExpressionType)).Length;
            float estimatedViewHeight = fc == null
                ? contentRect.height
                : fc.workflowMode == FaceWorkflowMode.FullFaceSwap
                    ? Mathf.Max(contentRect.height + 240f, 620f)
                    : Mathf.Max(contentRect.height + 1200f, 2400f);
            Rect viewRect = new Rect(0, 0, contentRect.width - 20f, estimatedViewHeight);

            Widgets.BeginScrollView(contentRect.ContractedBy(2f), ref faceScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_Title".Translate());

            if (fc == null)
            {
                if (Widgets.ButtonText(new Rect(0f, y, width, 28f), "CS_Studio_Face_Create".Translate()))
                {
                    workingSkin.faceConfig = new PawnFaceConfig();
                    isDirty = true;
                    RefreshPreview();
                }

                y += 36f;
                Widgets.EndScrollView();
                return;
            }

            bool enabled = fc.enabled;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Face_Enable".Translate(), ref enabled);
            if (enabled != fc.enabled)
            {
                fc.enabled = enabled;
                isDirty = true;
                RefreshPreview();
            }

            UIHelper.DrawPropertyFieldWithButton(
                ref y,
                width,
                "CS_Studio_Face_Workflow".Translate(),
                GetFaceWorkflowModeLabel(fc.workflowMode),
                OpenFaceWorkflowMenu);

            if (fc.workflowMode == FaceWorkflowMode.FullFaceSwap)
            {
                DrawFullFaceSwapSection(fc, ref y, width, exprCount);
            }
            else
            {
                DrawLayeredDynamicSection(fc, ref y, width, exprCount);
            }

            y += 8f;
            DrawFaceAnimationPreviewSection(ref y, width);
            DrawFaceMovementSettingsLauncher(ref y, width);

            viewRect.height = Mathf.Max(y + 10f, contentRect.height - 4f);
            Widgets.EndScrollView();
        }

        private void DrawFaceAnimationPreviewSection(ref float y, float width)
        {
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_AnimationPreview_Title".Translate());

            float btnHeight = 24f;
            float btnWidth = 100f;
            float spacing = 8f;
            
            Rect rowRect = new Rect(0f, y, width, btnHeight);
            
            Rect playRect = new Rect(0f, y, btnWidth, btnHeight);
            bool isPlayingOnce = previewFaceAnimationPlaying && !previewFaceAnimationLoop;
            if (DrawFaceToolbarButton(playRect, 
                (isPlayingOnce ? "▶ " : string.Empty) + "CS_Studio_Face_PreviewBlinkPlay".Translate(),
                "CS_Studio_Face_PreviewBlinkTooltip".Translate(),
                () => TogglePreviewFaceAnimation(loop: false),
                accent: isPlayingOnce))
            {
            }

            Rect loopRect = new Rect(btnWidth + spacing, y, btnWidth, btnHeight);
            bool isLooping = previewFaceAnimationPlaying && previewFaceAnimationLoop;
            if (DrawFaceToolbarButton(loopRect, 
                (isLooping ? "▶ " : string.Empty) + "CS_Studio_Face_PreviewBlinkLoop".Translate(),
                "CS_Studio_Face_PreviewBlinkTooltip".Translate(),
                () => TogglePreviewFaceAnimation(loop: true),
                accent: isLooping))
            {
            }

            y += btnHeight + 12f;
        }

        private void DrawFaceMovementSettingsLauncher(ref float y, float width)
        {
            Rect buttonRect = new Rect(0f, y, width, 28f);
            if (DrawFaceToolbarButton(buttonRect,
                "CS_Studio_Face_MovementLauncher_Button".Translate(),
                "CS_Studio_Face_MovementLauncher_Tooltip".Translate(),
                OpenFaceMovementSettingsDialog,
                accent: true))
            {
            }

            y += 34f;
        }

        private void OpenFaceMovementSettingsDialog()
        {
            Find.WindowStack.Add(new Dialog_FaceMovementSettings(this));
        }

        internal void DrawFaceMovementDialogContents(ref float y, float width)
        {
            PawnFaceConfig? fc = workingSkin.faceConfig;
            if (fc == null)
            {
                DrawPropertyHint(ref y, width, "CS_Studio_Face_MovementDialog_NoFaceConfig".Translate());
                return;
            }

            if (fc.workflowMode == FaceWorkflowMode.LayeredDynamic)
            {
                DrawLayeredPartMotionSection(fc, ref y, width);
            }
            else
            {
                DrawPropertyHint(ref y, width, "CS_Studio_Face_MovementDialog_FullFaceHint".Translate());
            }

            DrawEyeDirectionSection(fc, ref y, width, includeHeader: true, includeTextureRows: true);
        }

        internal void DrawSelectedLayerMovementDialogContents(ref float y, float width)
        {
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_MovementDialog_LayerSection".Translate());
            if (selectedLayerIndex < 0 || selectedLayerIndex >= workingSkin.layers.Count)
            {
                DrawPropertyHint(ref y, width, "CS_Studio_Face_MovementDialog_NoLayerSelected".Translate());
                return;
            }

            DrawSelectedLayerExpressionMovementSection(ref y, width, workingSkin.layers[selectedLayerIndex]);
        }

        private void OpenFaceWorkflowMenu()
        {
            if (workingSkin.faceConfig == null)
            {
                return;
            }

            var fc = workingSkin.faceConfig;
            var options = new List<FloatMenuOption>();

            foreach (FaceWorkflowMode mode in Enum.GetValues(typeof(FaceWorkflowMode)))
            {
                FaceWorkflowMode localMode = mode;
                options.Add(new FloatMenuOption(GetFaceWorkflowModeLabel(localMode), () =>
                {
                    if (fc.workflowMode != localMode)
                    {
                        fc.workflowMode = localMode;
                        ForceResetPreviewMannequin();
                        isDirty = true;
                        RefreshPreview();
                        RefreshRenderTree();
                    }
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawFullFaceSwapSection(PawnFaceConfig fc, ref float y, float width, int exprCount)
        {
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_ExpressionMappings".Translate());

            UIHelper.DrawPropertyFieldWithButton(
                ref y,
                width,
                "CS_Studio_Face_FullFaceAutoImport_BaseTexture".Translate(),
                string.IsNullOrWhiteSpace(fc.GetTexPath(ExpressionType.Neutral))
                    ? "CS_Studio_Face_FullFaceAutoImport_SelectBase".Translate().ToString()
                    : fc.GetTexPath(ExpressionType.Neutral),
                () => OpenFullFaceAutoImportDialog(fc));

            y += 4f;
        }

        private void DrawLayeredDynamicSection(PawnFaceConfig fc, ref float y, float width, int exprCount)
        {
            UIHelper.DrawPropertyFieldWithButton(
                ref y,
                width,
                "CS_Studio_Face_Layered_SourceRoot".Translate(),
                string.IsNullOrWhiteSpace(fc.layeredSourceRoot) ? "CS_Studio_None".Translate() : fc.layeredSourceRoot,
                () =>
                {
                    Find.WindowStack.Add(new Dialog_FileBrowser(fc.layeredSourceRoot ?? string.Empty, path =>
                    {
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            AutoPopulateLayeredFacePartsFromDirectory(fc, path);
                        }
                    }));
                });

            y += 4f;

            if (fc.HasAnyLayeredPart())
            {
                DrawLayeredPartConfigSection(fc, ref y, width);
                DrawScannedOverlayMappingSection(fc, ref y, width);
            }
        }

        private void DrawScannedOverlayMappingSection(PawnFaceConfig fc, ref float y, float width)
        {
            EnsureScannedOverlayCandidates(fc);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_OverlayMapping_Title".Translate());
            DrawPropertyHint(ref y, width, "CS_Studio_Face_OverlayMapping_Hint".Translate());

            if (!string.IsNullOrWhiteSpace(scannedOverlayCacheError))
            {
                DrawPropertyHint(ref y, width, scannedOverlayCacheError);
                return;
            }

            if (scannedOverlayCandidates.Count == 0)
            {
                DrawPropertyHint(ref y, width, "CS_Studio_Face_OverlayMapping_Empty".Translate());
                return;
            }

            foreach (ScannedOverlayCandidate candidate in scannedOverlayCandidates)
            {
                DrawCompactOverlayMappingRow(fc, ref y, width, candidate);
            }

            y += 2f;
        }

        private void DrawCompactOverlayMappingRow(PawnFaceConfig fc, ref float y, float width, ScannedOverlayCandidate candidate)
        {
            string mappedOverlayId = GetMappedOverlayIdForPath(fc, candidate.FilePath);
            string displayLabel = string.IsNullOrWhiteSpace(mappedOverlayId)
                ? (string.IsNullOrWhiteSpace(candidate.SuggestedOverlayId)
                    ? "CS_Studio_Face_OverlayMapping_Unmapped".Translate().ToString()
                    : "CS_Studio_Face_OverlayMapping_Suggested".Translate(candidate.SuggestedOverlayId).ToString())
                : mappedOverlayId;

            Rect rowRect = new Rect(0f, y, width, 22f);
            Widgets.DrawBoxSolid(rowRect, UIHelper.AlternatingRowColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rowRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(6f, y, Mathf.Max(60f, width - 92f), 22f), candidate.FileName);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Rect buttonRect = new Rect(width - 84f, y + 1f, 84f, 20f);
            if (UIHelper.DrawSelectionButton(buttonRect, displayLabel))
            {
                OpenScannedOverlayMappingMenu(fc, candidate);
            }

            y += 24f;
        }

        private void EnsureScannedOverlayCandidates(PawnFaceConfig fc)
        {
            string sourceRoot = fc.layeredSourceRoot ?? string.Empty;
            if (ArePathStringsEquivalent(scannedOverlayCacheSourceRoot, sourceRoot)
                && (scannedOverlayCandidates.Count > 0 || !string.IsNullOrWhiteSpace(scannedOverlayCacheError)))
            {
                return;
            }

            scannedOverlayCacheSourceRoot = sourceRoot;
            scannedOverlayCandidates.Clear();
            scannedOverlayCacheError = string.Empty;

            if (string.IsNullOrWhiteSpace(sourceRoot))
            {
                scannedOverlayCacheError = "请先设置 layered source root。";
                return;
            }

            if (!Directory.Exists(sourceRoot))
            {
                scannedOverlayCacheError = $"目录不存在：{sourceRoot}";
                return;
            }

            try
            {
                foreach (string filePath in Directory.EnumerateFiles(sourceRoot)
                    .Where(IsSupportedLayeredFaceTextureFile)
                    .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase))
                {
                    if (!TryParseLayeredFaceFileName(filePath, out LayeredFacePartType partType, out _, out string? overlayId, out _, out _)
                        || partType != LayeredFacePartType.Overlay)
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(filePath) ?? filePath;
                    string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
                    scannedOverlayCandidates.Add(new ScannedOverlayCandidate(filePath, fileName, normalizedOverlayId));
                }
            }
            catch (Exception ex)
            {
                scannedOverlayCandidates.Clear();
                scannedOverlayCacheError = $"扫描 overlay 纹理失败：{ex.Message}";
            }
        }

        private static readonly string[] ManualOverlayIds =
        {
            "Blush",
            "Tear",
            "Sweat",
            "Sleep",
            "Gloomy"
        };

        private string GetMappedOverlayIdForPath(PawnFaceConfig fc, string filePath)
        {
            foreach (string overlayId in fc.GetOrderedOverlayIds())
            {
                LayeredFacePartConfig? overlayPart = fc.GetLayeredPartConfig(LayeredFacePartType.Overlay, ExpressionType.Neutral, overlayId);
                if (overlayPart != null && ArePathStringsEquivalent(overlayPart.texPath, filePath))
                {
                    return overlayId;
                }
            }

            return string.Empty;
        }

        private void ClearOverlayAssignmentsForPath(PawnFaceConfig fc, string filePath)
        {
            foreach (string overlayId in fc.GetOrderedOverlayIds().ToList())
            {
                LayeredFacePartConfig? overlayPart = fc.GetLayeredPartConfig(LayeredFacePartType.Overlay, ExpressionType.Neutral, overlayId);
                if (overlayPart != null && ArePathStringsEquivalent(overlayPart.texPath, filePath))
                {
                    fc.RemoveLayeredPart(LayeredFacePartType.Overlay, ExpressionType.Neutral, overlayId);
                }
            }
        }

        private void OpenScannedOverlayMappingMenu(PawnFaceConfig fc, ScannedOverlayCandidate candidate)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("未映射", () =>
                {
                    ClearOverlayAssignmentsForPath(fc, candidate.FilePath);
                    layeredPartPathBuffer.Clear();
                    isDirty = true;
                    RefreshPreview();
                })
            };

            IEnumerable<string> overlayIds = ManualOverlayIds
                .Concat(fc.GetOrderedOverlayIds())
                .Concat(string.IsNullOrWhiteSpace(candidate.SuggestedOverlayId)
                    ? Enumerable.Empty<string>()
                    : new[] { candidate.SuggestedOverlayId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => PawnFaceConfig.GetCanonicalOverlayOrder(id))
                .ThenBy(id => id, StringComparer.OrdinalIgnoreCase);

            foreach (string overlayId in overlayIds)
            {
                string localOverlayId = overlayId;
                options.Add(new FloatMenuOption(localOverlayId, () =>
                {
                    ClearOverlayAssignmentsForPath(fc, candidate.FilePath);
                    fc.SetLayeredPart(LayeredFacePartType.Overlay, ExpressionType.Neutral, candidate.FilePath, localOverlayId, fc.GetOverlayOrder(localOverlayId));
                    layeredPartPathBuffer.Clear();
                    isDirty = true;
                    RefreshPreview();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawLayeredPartConfigSection(PawnFaceConfig fc, ref float y, float width)
        {
            UIHelper.DrawSectionTitle(ref y, width, "通用表情与部件调整");

            foreach (LayeredFacePartType partType in EnumerateLayeredPartEditorTypes())
            {
                DrawLayeredPartEditorGroup(fc, ref y, width, partType);
            }

            // 绘制所有覆盖层风格的部件 (Overlay, Hair 等)
            foreach (LayeredFacePartType partType in Enum.GetValues(typeof(LayeredFacePartType)))
            {
                if (!PawnFaceConfig.IsOverlayPart(partType)) continue;

                List<string> overlayIds = fc.GetOrderedOverlayIds(partType);
                if (overlayIds.Any())
                {
                    UIHelper.DrawSectionTitle(ref y, width, GetLayeredFacePartTypeLabel(partType));
                    foreach (string overlayId in overlayIds)
                    {
                        DrawLayeredPartRow(fc, ref y, width, partType, previewExpression, overlayId: overlayId);
                    }
                }
            }
        }

        private IEnumerable<LayeredFacePartType> EnumerateLayeredPartEditorTypes()
        {
            yield return LayeredFacePartType.Base;
            yield return LayeredFacePartType.Eye;
            yield return LayeredFacePartType.Pupil;
            yield return LayeredFacePartType.UpperLid;
            yield return LayeredFacePartType.LowerLid;
            yield return LayeredFacePartType.Brow;
            yield return LayeredFacePartType.Mouth;
        }

        private void DrawLayeredPartEditorGroup(PawnFaceConfig fc, ref float y, float width, LayeredFacePartType partType)
        {
            UIHelper.DrawSectionTitle(ref y, width, GetLayeredFacePartTypeLabel(partType));

            if (PawnFaceConfig.SupportsSideSpecificParts(partType))
            {
                bool hasExplicitSidedContent =
                    fc.CountLayeredParts(partType, LayeredFacePartSide.Left) > 0
                    || fc.CountLayeredParts(partType, LayeredFacePartSide.Right) > 0;

                if (hasExplicitSidedContent)
                {
                    DrawLayeredPartRow(fc, ref y, width, partType, previewExpression, side: LayeredFacePartSide.Left);
                    DrawLayeredPartRow(fc, ref y, width, partType, previewExpression, side: LayeredFacePartSide.Right);
                    y += 2f;
                    return;
                }
            }

            DrawLayeredPartRow(fc, ref y, width, partType, previewExpression);
            y += 2f;
        }

        private static string GetLayeredPartEditorLabel(LayeredFacePartType partType, LayeredFacePartSide side)
        {
            string label = GetLayeredFacePartTypeLabel(partType);
            LayeredFacePartSide normalizedSide = PawnFaceConfig.NormalizePartSide(partType, side);
            if (normalizedSide == LayeredFacePartSide.None)
            {
                return label;
            }

            string sideLabel = normalizedSide == LayeredFacePartSide.Left ? "左侧" : "右侧";
            return $"{label}[{sideLabel}]";
        }

        private void DrawLayeredPartRow(
            PawnFaceConfig fc,
            ref float y,
            float width,
            LayeredFacePartType partType,
            ExpressionType expression,
            string? overlayId = null,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            bool isOverlay = PawnFaceConfig.IsOverlayPart(partType);
            string resolvedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
            if (isOverlay && string.IsNullOrWhiteSpace(resolvedOverlayId))
                return;

            LayeredFacePartSide normalizedSide = PawnFaceConfig.NormalizePartSide(partType, side);
            string bufferKey = GetLayeredPartBufferKey(partType, expression, resolvedOverlayId, normalizedSide);

            LayeredFacePartConfig? existing = isOverlay
                ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                : fc.GetLayeredPartConfig(partType, expression, normalizedSide);

            string actualPath = existing?.texPath ?? string.Empty;
            bool enabled = existing?.enabled ?? !string.IsNullOrWhiteSpace(actualPath);

            Rect partRowRect = new Rect(0f, y, width, 24f);
            Widgets.DrawBoxSolid(partRowRect,
                !string.IsNullOrWhiteSpace(actualPath) ? UIHelper.AccentSoftColor : UIHelper.AlternatingRowColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(partRowRect, 1);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(
                new Rect(6f, y, 84f, 24f),
                isOverlay ? resolvedOverlayId : GetLayeredPartEditorLabel(partType, normalizedSide));
            Text.Anchor = TextAnchor.UpperLeft;

            Rect toggleRect = new Rect(92f, y + 3f, 24f, 18f);
            bool newEnabled = enabled;
            Widgets.Checkbox(toggleRect.position, ref newEnabled, 18f, false);
            TooltipHandler.TipRegion(toggleRect, "CS_Studio_Face_Layered_Enable".Translate());

            if (newEnabled != enabled)
            {
                if (existing != null)
                {
                    existing.enabled = newEnabled;
                }
                else if (newEnabled && !string.IsNullOrWhiteSpace(actualPath))
                {
                    if (isOverlay)
                    {
                        fc.SetLayeredPart(partType, expression, actualPath, resolvedOverlayId, fc.GetOverlayOrder(resolvedOverlayId));
                    }
                    else if (normalizedSide != LayeredFacePartSide.None)
                    {
                        fc.SetLayeredPart(partType, expression, actualPath, normalizedSide);
                    }
                    else
                    {
                        fc.SetLayeredPart(partType, expression, actualPath);
                    }
                }

                isDirty = true;
                RefreshPreview();
                existing = isOverlay
                    ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                    : fc.GetLayeredPartConfig(partType, expression, normalizedSide);
                actualPath = existing?.texPath ?? string.Empty;
            }

            if (!layeredPartPathBuffer.TryGetValue(bufferKey, out string bufferPath))
            {
                bufferPath = actualPath;
            }

            float pathX = 120f;
            float pathWidth = width - pathX - 58f;
            string newBufferPath = Widgets.TextField(new Rect(pathX, y + 2f, pathWidth, 20f), bufferPath);
            if (!ArePathStringsEquivalent(newBufferPath, bufferPath))
            {
                layeredPartPathBuffer[bufferKey] = newBufferPath;

                if (isOverlay)
                {
                    fc.SetLayeredPart(partType, expression, newBufferPath, resolvedOverlayId, fc.GetOverlayOrder(resolvedOverlayId));
                }
                else if (normalizedSide != LayeredFacePartSide.None)
                {
                    fc.SetLayeredPart(partType, expression, newBufferPath, normalizedSide);
                }
                else
                {
                    fc.SetLayeredPart(partType, expression, newBufferPath);
                }

                LayeredFacePartConfig? changedPart = isOverlay
                    ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                    : fc.GetLayeredPartConfig(partType, expression, normalizedSide);
                if (changedPart != null)
                {
                    changedPart.enabled = !string.IsNullOrWhiteSpace(newBufferPath) && (existing?.enabled ?? true);
                }

                TryAutoPopulateLayeredFacePartsFromBase(fc, partType, expression, newBufferPath);

                FaceRuntimeCompiler.ClearCache();
                PawnRenderNodeWorker_FaceComponent.ClearCache();
                isDirty = true;
                RefreshPreview();
                existing = changedPart;
                actualPath = changedPart?.texPath ?? string.Empty;
            }
            else if (!layeredPartPathBuffer.ContainsKey(bufferKey))
            {
                layeredPartPathBuffer[bufferKey] = actualPath;
            }

            if (UIHelper.DrawBrowseButton(new Rect(width - 58f, y + 1f, 30f, 22f), () =>
            {
                string browsePath = actualPath;
                Find.WindowStack.Add(new Dialog_FileBrowser(browsePath, path =>
                {
                    if (isOverlay)
                    {
                        fc.SetLayeredPart(partType, expression, path ?? string.Empty, resolvedOverlayId, fc.GetOverlayOrder(resolvedOverlayId));
                    }
                    else if (normalizedSide != LayeredFacePartSide.None)
                    {
                        fc.SetLayeredPart(partType, expression, path ?? string.Empty, normalizedSide);
                    }
                    else
                    {
                        fc.SetLayeredPart(partType, expression, path ?? string.Empty);
                    }

                    LayeredFacePartConfig? changedPart = isOverlay
                        ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                        : fc.GetLayeredPartConfig(partType, expression, normalizedSide);
                    if (changedPart != null)
                    {
                        changedPart.enabled = !string.IsNullOrWhiteSpace(path);
                    }
                    layeredPartPathBuffer[bufferKey] = path ?? string.Empty;

                    TryAutoPopulateLayeredFacePartsFromBase(fc, partType, expression, path);

                    FaceRuntimeCompiler.ClearCache();
                    PawnRenderNodeWorker_FaceComponent.ClearCache();
                    isDirty = true;
                    RefreshPreview();
                }));
            }))
            {
            }

            if (UIHelper.DrawDangerButton(new Rect(width - 28f, y + 1f, 24f, 22f), tooltip: "CS_Studio_Clear".Translate(), onClick: () =>
            {
                if (isOverlay)
                {
                    fc.RemoveLayeredPart(partType, expression, resolvedOverlayId);
                }
                else if (normalizedSide != LayeredFacePartSide.None)
                {
                    fc.RemoveLayeredPart(partType, expression, normalizedSide);
                }
                else
                {
                    fc.RemoveLayeredPart(partType, expression);
                }

                layeredPartPathBuffer[bufferKey] = string.Empty;
                FaceRuntimeCompiler.ClearCache();
                PawnRenderNodeWorker_FaceComponent.ClearCache();
                isDirty = true;
                RefreshPreview();
                existing = null;
                actualPath = string.Empty;
            }))
            {
            }

            y += 26f;
        }

        private void DrawEyeDirectionSection(PawnFaceConfig fc, ref float y, float width, bool includeHeader = true, bool includeTextureRows = true)
        {
            if (includeHeader)
            {
                UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_EyeDir_Title".Translate());
                DrawFaceInfoBanner(ref y, width, "CS_Studio_Face_EyeDir_Hint".Translate());
            }

            if (fc.eyeDirectionConfig == null)
            {
                if (Widgets.ButtonText(new Rect(0f, y, width, 28f), "CS_Studio_Face_EyeDir_Create".Translate()))
                {
                    fc.eyeDirectionConfig = new PawnEyeDirectionConfig();
                    isDirty = true;
                    RefreshPreview();
                }

                y += 36f;
            }
            else
            {
                var eyeCfg = fc.eyeDirectionConfig;

                bool eyeEnabled = eyeCfg.enabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Face_EyeDir_Enable".Translate(), ref eyeEnabled);
                if (eyeEnabled != eyeCfg.enabled)
                {
                    eyeCfg.enabled = eyeEnabled;
                    isDirty = true;
                    RefreshPreview();
                }

                if (eyeEnabled)
                {
                    var comp = mannequin?.CurrentPawn?.GetComp<CompPawnSkin>();
                    string previewDirLabel = comp != null
                        ? GetEyeDirectionLabel(comp.CurEyeDirection)
                        : GetEyeDirectionLabel(EyeDirection.Center);
                    UIHelper.DrawPropertyFieldWithButton(ref y, width,
                        "CS_Studio_Face_EyeDir_Preview".Translate(), previewDirLabel,
                        () => OpenPreviewEyeDirectionMenu(comp));
                    y += 4f;
                }

                y += 4f;
                DrawEyeDirectionMovementControls(ref y, width, eyeCfg, includeTextureRows);

                y += 4f;
                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (Widgets.ButtonText(new Rect(0f, y, width, 24f), "CS_Studio_Face_EyeDir_Remove".Translate()))
                {
                    fc.eyeDirectionConfig = null;
                    isDirty = true;
                    RefreshPreview();
                }
                GUI.color = Color.white;
                y += 28f;
            }
        }

        private void DrawLayeredPartMotionSection(PawnFaceConfig fc, ref float y, float width)
        {
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_MovementDialog_FaceSection".Translate());
            DrawPropertyHint(ref y, width, "CS_Studio_Face_MovementDialog_FaceHint".Translate());

            UIHelper.DrawPropertyFieldWithButton(
                ref y,
                width,
                "CS_Studio_Face_MovementDialog_PreviewExpression".Translate(),
                GetPreviewOverrideLabel(previewExpressionOverrideEnabled, GetExpressionTypeLabel(previewExpression)),
                OpenPreviewExpressionMenu);
            y += 2f;

            UIHelper.DrawPropertyFieldWithButton(
                ref y,
                width,
                "CS_Studio_Face_MovementDialog_PreviewEyeDirection".Translate(),
                GetPreviewOverrideLabel(previewEyeDirectionOverrideEnabled, previewEyeDirection.ToString()),
                OpenPreviewEyeDirectionMenu);
            y += 4f;

            foreach (LayeredFacePartType partType in EnumerateLayeredPartEditorTypes())
            {
                DrawLayeredPartMotionEditorGroup(fc, ref y, width, partType);
            }

            // 绘制所有覆盖层风格的部件 (Overlay, Hair 等) 的运动设置
            foreach (LayeredFacePartType partType in Enum.GetValues(typeof(LayeredFacePartType)))
            {
                if (!PawnFaceConfig.IsOverlayPart(partType)) continue;

                List<string> overlayIds = fc.GetOrderedOverlayIds(partType);
                if (overlayIds.Any())
                {
                    UIHelper.DrawSectionTitle(ref y, width, GetLayeredFacePartTypeLabel(partType));
                    foreach (string overlayId in overlayIds)
                    {
                        DrawLayeredPartMotionRow(fc, ref y, width, partType, previewExpression, overlayId: overlayId);
                    }
                }
            }
        }

        private void DrawLayeredPartMotionEditorGroup(PawnFaceConfig fc, ref float y, float width, LayeredFacePartType partType)
        {
            UIHelper.DrawSectionTitle(ref y, width, GetLayeredFacePartTypeLabel(partType));

            if (PawnFaceConfig.SupportsSideSpecificParts(partType))
            {
                bool hasExplicitSidedContent =
                    fc.CountLayeredParts(partType, LayeredFacePartSide.Left) > 0
                    || fc.CountLayeredParts(partType, LayeredFacePartSide.Right) > 0;

                if (hasExplicitSidedContent)
                {
                    DrawLayeredPartMotionRow(fc, ref y, width, partType, previewExpression, side: LayeredFacePartSide.Left);
                    DrawLayeredPartMotionRow(fc, ref y, width, partType, previewExpression, side: LayeredFacePartSide.Right);
                    y += 2f;
                    return;
                }
            }

            DrawLayeredPartMotionRow(fc, ref y, width, partType, previewExpression);
            y += 2f;
        }

        private void DrawLayeredPartMotionRow(
            PawnFaceConfig fc,
            ref float y,
            float width,
            LayeredFacePartType partType,
            ExpressionType expression,
            string? overlayId = null,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            bool isOverlay = PawnFaceConfig.IsOverlayPart(partType);
            string resolvedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
            if (isOverlay && string.IsNullOrWhiteSpace(resolvedOverlayId))
            {
                return;
            }

            LayeredFacePartSide normalizedSide = PawnFaceConfig.NormalizePartSide(partType, side);
            LayeredFacePartConfig? editablePart = isOverlay
                ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                : fc.GetLayeredPartConfig(partType, expression, normalizedSide);
            if (editablePart == null)
            {
                return;
            }

            editablePart.SyncLegacyMotionAmplitude();

            string label = isOverlay ? resolvedOverlayId : GetLayeredPartEditorLabel(partType, normalizedSide);
            UIHelper.DrawPropertyLabel(ref y, width, label, editablePart.motionAmplitude.ToString("F4"));

            float motionAmplitude = editablePart.motionAmplitude;
            UIHelper.DrawPropertySlider(ref y, width,
                "CS_Studio_Face_Layered_MotionAmplitude".Translate(),
                ref motionAmplitude, 0f, 0.01f, "F4", 20f);
            if (!Mathf.Approximately(motionAmplitude, editablePart.motionAmplitude))
            {
                editablePart.motionAmplitude = motionAmplitude;
                editablePart.anchorCorrection = Vector2.zero;
                FaceRuntimeCompiler.ClearCache();
                isDirty = true;
                RefreshPreview();
            }

            y += 4f;
        }

        private void DrawEyeDirectionMovementControls(ref float y, float width, PawnEyeDirectionConfig eyeCfg, bool includeTextureRows)
        {
            y += 4f;
            float pupilRange = eyeCfg.pupilMoveRange;
            UIHelper.DrawPropertySlider(ref y, width,
                "CS_Studio_Face_EyeDir_PupilMoveRange".Translate(),
                ref pupilRange, 0f, 0.15f, "F3");
            if (pupilRange != eyeCfg.pupilMoveRange)
            {
                eyeCfg.pupilMoveRange = pupilRange;
                isDirty = true;
                RefreshPreview();
            }

            y += 4f;
            float upperLidMoveDown = eyeCfg.upperLidMoveDown;
            UIHelper.DrawPropertySlider(ref y, width,
                "CS_Studio_Face_EyeDir_UpperLidMoveDown".Translate(),
                ref upperLidMoveDown, 0f, 0.02f, "F4");
            if (upperLidMoveDown != eyeCfg.upperLidMoveDown)
            {
                eyeCfg.upperLidMoveDown = upperLidMoveDown;
                isDirty = true;
                RefreshPreview();
            }

            if (!includeTextureRows)
            {
                return;
            }

            y += 4f;
            if (eyeCfg.pupilMoveRange > 0f)
            {
                DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Center".Translate(),
                    eyeCfg.texCenter, path =>
                    {
                        eyeCfg.texCenter = path;
                        isDirty = true;
                        RefreshPreview();
                    });
                UIHelper.DrawPropertyLabel(ref y, width,
                    "CS_Studio_Face_EyeDir_UVModeHint".Translate(), "");
            }
            else
            {
                DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Center".Translate(),
                    eyeCfg.texCenter, path =>
                    {
                        eyeCfg.texCenter = path;
                        isDirty = true;
                        RefreshPreview();
                    });
                DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Left".Translate(),
                    eyeCfg.texLeft, path =>
                    {
                        eyeCfg.texLeft = path;
                        isDirty = true;
                        RefreshPreview();
                    });
                DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Right".Translate(),
                    eyeCfg.texRight, path =>
                    {
                        eyeCfg.texRight = path;
                        isDirty = true;
                        RefreshPreview();
                    });
                DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Up".Translate(),
                    eyeCfg.texUp, path =>
                    {
                        eyeCfg.texUp = path;
                        isDirty = true;
                        RefreshPreview();
                    });
                DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Down".Translate(),
                    eyeCfg.texDown, path =>
                    {
                        eyeCfg.texDown = path;
                        isDirty = true;
                        RefreshPreview();
                    });
            }
        }

        // ─────────────────────────────────────────────
        // 眼睛方向 UI 辅助
        // ─────────────────────────────────────────────

        /// <summary>绘制单行眼睛方向贴图输入行</summary>
        private void DrawEyeDirTexRow(ref float y, float width, string label, string currentPath, Action<string> onChanged)
        {
            float rowH = 24f;
            float labelW = 56f;
            float btnW = 28f;
            float clearW = 20f;
            float pathW = width - labelW - btnW - clearW - 6f;

            Widgets.Label(new Rect(0f, y + 3f, labelW, rowH), label);

            string display = string.IsNullOrEmpty(currentPath) ? "—" : System.IO.Path.GetFileName(currentPath);
            Text.Font = GameFont.Tiny;
            GUI.color = string.IsNullOrEmpty(currentPath) ? UIHelper.SubtleColor : Color.white;
            Widgets.Label(new Rect(labelW + 2f, y + 5f, pathW, rowH), display);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (UIHelper.DrawBrowseButton(new Rect(labelW + pathW + 2f, y + 1f, btnW, 22f), () =>
            {
                string capturedPath = currentPath;
                Find.WindowStack.Add(new Dialog_FileBrowser(capturedPath, newPath =>
                {
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        onChanged(newPath);
                    }
                }));
            }))
            {
            }

            if (!string.IsNullOrEmpty(currentPath))
            {
                if (UIHelper.DrawDangerButton(new Rect(labelW + pathW + btnW + 2f, y + 1f, clearW + 4f, 22f), tooltip: "CS_Studio_Clear".Translate(), onClick: () => onChanged(string.Empty)))
                {
                }
            }

            y += rowH;
        }

        /// <summary>弹出眼睛方向预览菜单</summary>
        private void OpenPreviewEyeDirectionMenu(CompPawnSkin? comp)
        {
            var options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption("CS_Studio_Face_EyeDir_Auto".Translate(), () =>
            {
                comp?.SetPreviewEyeDirection(null);
                RefreshPreview();
            }));

            foreach (EyeDirection dir in System.Enum.GetValues(typeof(EyeDirection)))
            {
                var localDir = dir;
                options.Add(new FloatMenuOption(GetEyeDirectionLabel(localDir), () =>
                {
                    comp?.SetPreviewEyeDirection(localDir);
                    RefreshPreview();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
