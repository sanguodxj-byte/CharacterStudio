using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
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
            if (partType == LayeredFacePartType.Overlay)
            {
                string resolvedOverlayId = string.IsNullOrWhiteSpace(overlayId) ? "Overlay" : overlayId!;
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
                    : Mathf.Max(contentRect.height + 420f, 1080f);
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

            if (fc.workflowMode == FaceWorkflowMode.FullFaceSwap)
            {
                y += 8f;
                DrawEyeDirectionSection(fc, ref y, width);
            }

            viewRect.height = Mathf.Max(y + 10f, contentRect.height - 4f);
            Widgets.EndScrollView();
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
            DrawFaceInfoBanner(ref y, width, "自动识别模式：只需选择 Base.png（或 Neutral.png），系统会按命名规则自动扫描同目录中的表情文件。已收敛为摘要展示，不再展开完整表情列表与路径。", accent: true);

            UIHelper.DrawPropertyFieldWithButton(
                ref y,
                width,
                "基底贴图",
                string.IsNullOrWhiteSpace(fc.GetTexPath(ExpressionType.Neutral))
                    ? "选择 Base.png 后自动识别"
                    : fc.GetTexPath(ExpressionType.Neutral),
                () => OpenFullFaceAutoImportDialog(fc));

            y += 4f;

            int assignedCount = fc.expressions.Count(e =>
                !string.IsNullOrEmpty(e.texPath) || (e.frames != null && e.frames.Count > 0));
            int animatedCount = fc.expressions.Count(e => e != null && e.frames != null && e.frames.Count > 0);
            int minSetDone = fc.expressions.Count(e =>
                MinSetExpressions.Contains(e.expression) &&
                (!string.IsNullOrEmpty(e.texPath) || (e.frames != null && e.frames.Count > 0)));

            Rect statsRect = new Rect(0f, y, width, 78f);
            Widgets.DrawBoxSolid(statsRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(statsRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(8f, y + 6f, width - 16f, 16f),
                $"表情载入：{assignedCount}/{exprCount}");
            Widgets.Label(new Rect(8f, y + 24f, width - 16f, 16f),
                $"动画表情：{animatedCount}/{exprCount}");

            bool minSetComplete = minSetDone >= MinSetExpressions.Count;
            GUI.color = minSetComplete ? new Color(0.4f, 0.9f, 0.4f) : new Color(1f, 0.85f, 0.3f);
            Widgets.Label(new Rect(8f, y + 42f, width - 16f, 16f),
                $"核心集合：{minSetDone}/{MinSetExpressions.Count}");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(new Rect(width - 98f, y + 54f, 92f, 20f), "重新导入"))
            {
                OpenFullFaceAutoImportDialog(fc);
            }

            y += 84f;
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
                        fc.layeredSourceRoot = path ?? string.Empty;
                        isDirty = true;
                    }));
                });

            string currentRoot = fc.layeredSourceRoot ?? string.Empty;
            string newRoot = Widgets.TextField(new Rect(0f, y, width - 34f, 20f), currentRoot);
            if (!ArePathStringsEquivalent(newRoot, currentRoot))
            {
                fc.layeredSourceRoot = newRoot;
                isDirty = true;
            }

            if (UIHelper.DrawDangerButton(new Rect(width - 32f, y - 1f, 28f, 22f), tooltip: "CS_Studio_Clear".Translate(), onClick: () =>
            {
                fc.layeredSourceRoot = string.Empty;
                isDirty = true;
            }))
            {
            }

            y += 30f;
            DrawLayeredPartConfigSection(fc, ref y, width);
        }

        private void DrawLayeredPartConfigSection(PawnFaceConfig fc, ref float y, float width)
        {
            UIHelper.DrawSectionTitle(ref y, width, "通用表情与部件调整");

            UIHelper.DrawPropertyFieldWithButton(
                ref y,
                width,
                "表情",
                GetPreviewOverrideLabel(previewExpressionOverrideEnabled, GetExpressionTypeLabel(previewExpression)),
                OpenPreviewExpressionMenu);
            y += 4f;

            UIHelper.DrawPropertyFieldWithButton(
                ref y,
                width,
                "眼球朝向",
                GetPreviewOverrideLabel(previewEyeDirectionOverrideEnabled, previewEyeDirection.ToString()),
                OpenPreviewEyeDirectionMenu);
            y += 8f;

            foreach (LayeredFacePartType partType in EnumerateLayeredPartEditorTypes())
            {
                DrawLayeredPartEditorGroup(fc, ref y, width, partType);
            }

            List<string> overlayIds = fc.GetOrderedOverlayIds();
            if (!overlayIds.Any())
            {
                overlayIds.Add("Overlay");
            }

            UIHelper.DrawSectionTitle(ref y, width, "Overlay 图层");
            foreach (string overlayId in overlayIds)
            {
                DrawLayeredPartRow(fc, ref y, width, LayeredFacePartType.Overlay, previewExpression, overlayId: overlayId);
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
                    y += 4f;
                    return;
                }
            }

            DrawLayeredPartRow(fc, ref y, width, partType, previewExpression);
            y += 4f;
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
            bool isOverlay = partType == LayeredFacePartType.Overlay;
            string resolvedOverlayId = string.IsNullOrWhiteSpace(overlayId) ? "Overlay" : overlayId!;
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

                isDirty = true;
                RefreshPreview();
                existing = changedPart;
                actualPath = changedPart?.texPath ?? string.Empty;
            }
            else
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
                isDirty = true;
                RefreshPreview();
                existing = null;
                actualPath = string.Empty;
            }))
            {
            }

            y += 26f;

            LayeredFacePartConfig? editablePart = isOverlay
                ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                : fc.GetLayeredPartConfig(partType, expression, normalizedSide);
            if (editablePart == null)
                return;

            float correctionX = editablePart.anchorCorrection.x;
            UIHelper.DrawPropertySlider(ref y, width,
                "CS_Studio_Face_Layered_MotionAmplitudeX".Translate(),
                ref correctionX, -0.01f, 0.01f, "F4", 20f);
            if (!Mathf.Approximately(correctionX, editablePart.anchorCorrection.x))
            {
                editablePart.anchorCorrection.x = correctionX;
                isDirty = true;
                RefreshPreview();
            }

            float correctionY = editablePart.anchorCorrection.y;
            UIHelper.DrawPropertySlider(ref y, width,
                "CS_Studio_Face_Layered_MotionAmplitudeY".Translate(),
                ref correctionY, -0.01f, 0.01f, "F4", 20f);
            if (!Mathf.Approximately(correctionY, editablePart.anchorCorrection.y))
            {
                editablePart.anchorCorrection.y = correctionY;
                isDirty = true;
                RefreshPreview();
            }

            y += 4f;
        }

        private void DrawEyeDirectionSection(PawnFaceConfig fc, ref float y, float width)
        {
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_EyeDir_Title".Translate());
            DrawFaceInfoBanner(ref y, width, "CS_Studio_Face_EyeDir_Hint".Translate());

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