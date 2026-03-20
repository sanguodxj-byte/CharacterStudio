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
            string? overlayId = null)
        {
            if (partType == LayeredFacePartType.Overlay)
            {
                string resolvedOverlayId = string.IsNullOrWhiteSpace(overlayId) ? "Overlay" : overlayId!;
                return $"{partType}|{resolvedOverlayId}|{expression}";
            }

            return $"{partType}|{expression}";
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
            float toolbarY = titleRect.yMax + 6f;
            float toolbarWidth = (rect.width - Margin * 5) / 4f;
            float toolbarHeight = Mathf.Max(ButtonHeight - 2f, 22f);

            if (fc != null)
            {
                DrawFaceToolbarButton(
                    new Rect(rect.x + Margin, toolbarY, toolbarWidth, toolbarHeight),
                    "★",
                    "切换表情预览开关",
                    () =>
                    {
                        ApplyPreviewExpressionOverride(!previewExpressionOverrideEnabled, previewExpression);
                    },
                    previewExpressionOverrideEnabled);

                DrawFaceToolbarButton(
                    new Rect(rect.x + Margin * 2 + toolbarWidth, toolbarY, toolbarWidth, toolbarHeight),
                    "⟳",
                    "刷新预览",
                    RefreshPreview);

                DrawFaceToolbarButton(
                    new Rect(rect.x + Margin * 3 + toolbarWidth * 2, toolbarY, toolbarWidth, toolbarHeight),
                    fc.workflowMode == FaceWorkflowMode.FullFaceSwap ? "FF" : "LD",
                    "切换面部工作流",
                    OpenFaceWorkflowMenu,
                    true);

                DrawFaceToolbarButton(
                    new Rect(rect.x + Margin * 4 + toolbarWidth * 3, toolbarY, toolbarWidth, toolbarHeight),
                    "…",
                    "设置当前预览表情",
                    OpenPreviewExpressionMenu);
            }

            float contentY = toolbarY + toolbarHeight + 8f;
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

            string activePreviewLabel = previewExpressionOverrideEnabled
                ? GetExpressionTypeLabel(previewExpression)
                : "CS_Studio_Face_PreviewAuto".Translate();
            UIHelper.DrawPropertyFieldWithButton(ref y, width,
                "CS_Studio_Face_PreviewActive".Translate(), activePreviewLabel,
                OpenPreviewExpressionMenu);

            y += 4f;
            DrawFaceInfoBanner(ref y, width, "CS_Studio_Face_HeadTexHint".Translate());

            UIHelper.DrawPropertyFieldWithButton(
                ref y,
                width,
                "CS_Studio_Face_Workflow".Translate(),
                GetFaceWorkflowModeLabel(fc.workflowMode),
                OpenFaceWorkflowMenu);

            string workflowHint = fc.workflowMode == FaceWorkflowMode.FullFaceSwap
                ? "CS_Studio_Face_Workflow_FullFaceSwap_Desc".Translate()
                : "CS_Studio_Face_Workflow_LayeredDynamic_Desc".Translate();
            DrawFaceInfoBanner(ref y, width, workflowHint, accent: true);

            if (fc.workflowMode == FaceWorkflowMode.FullFaceSwap)
            {
                DrawFullFaceSwapSection(fc, ref y, width, exprCount);
            }
            else
            {
                DrawLayeredDynamicSection(fc, ref y, width, exprCount);
            }

            y += 8f;
            DrawEyeDirectionSection(fc, ref y, width);

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
                        isDirty = true;
                        RefreshPreview();
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
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_Layered_Title".Translate());
            DrawFaceInfoBanner(ref y, width, "CS_Studio_Face_Layered_Hint".Translate() + " 当前左侧面板仅显示各部件摘要数量，不再展开完整表情路径列表。", accent: true);

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
            if (newRoot != currentRoot)
            {
                fc.layeredSourceRoot = newRoot;
                isDirty = true;
            }

            if (Widgets.ButtonText(new Rect(width - 30f, y, 30f, 20f), "×"))
            {
                fc.layeredSourceRoot = string.Empty;
                isDirty = true;
            }

            y += 30f;

            int assignedEntries = fc.layeredParts?.Count(p => p != null && !string.IsNullOrWhiteSpace(p.texPath)) ?? 0;
            int configuredPartTypes = Enum.GetValues(typeof(LayeredFacePartType)).Cast<LayeredFacePartType>()
                .Count(partType => fc.CountLayeredParts(partType) > 0);
            int totalPartTypes = Enum.GetValues(typeof(LayeredFacePartType)).Length;

            Rect layeredStatsRect = new Rect(0f, y, width, 58f);
            Widgets.DrawBoxSolid(layeredStatsRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(layeredStatsRect, 1);
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(8f, y + 6f, width - 16f, 16f),
                $"部件载入：{assignedEntries}/{exprCount * totalPartTypes}");
            Widgets.Label(new Rect(8f, y + 24f, width - 16f, 16f),
                $"已配置类型：{configuredPartTypes}/{totalPartTypes}");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 64f;

            foreach (LayeredFacePartType partType in Enum.GetValues(typeof(LayeredFacePartType)))
            {
                UIHelper.DrawSectionTitle(ref y, width, GetLayeredFacePartTypeLabel(partType));

                int partCount = fc.CountLayeredParts(partType);
                Rect partSummaryRect = new Rect(0f, y, width, 24f);
                Widgets.DrawBoxSolid(partSummaryRect, partCount > 0 ? UIHelper.AccentSoftColor : UIHelper.PanelFillSoftColor);
                GUI.color = UIHelper.BorderColor;
                Widgets.DrawBox(partSummaryRect, 1);
                GUI.color = partCount > 0 ? UIHelper.HeaderColor : UIHelper.SubtleColor;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(8f, y + 4f, width - 16f, 16f),
                    $"{partCount}/{exprCount}");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 30f;

                if (partType == LayeredFacePartType.Overlay)
                {
                    DrawOverlayGroupsSection(fc, ref y, width, exprCount);
                }
            }
        }

        private void DrawOverlayGroupsSection(PawnFaceConfig fc, ref float y, float width, int exprCount)
        {
            List<string> overlayIds = fc.GetOrderedOverlayIds();
            if (overlayIds.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(0f, y, width, 18f), "0/0");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 22f;
                return;
            }

            for (int i = 0; i < overlayIds.Count; i++)
            {
                string overlayId = overlayIds[i];
                int assignedCount = fc.CountLayeredParts(LayeredFacePartType.Overlay, overlayId);

                Rect overlayHeaderRect = new Rect(0f, y, width, 24f);
                Widgets.DrawBoxSolid(overlayHeaderRect, assignedCount > 0 ? UIHelper.AccentSoftColor : UIHelper.PanelFillSoftColor);
                GUI.color = UIHelper.BorderColor;
                Widgets.DrawBox(overlayHeaderRect, 1);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(6f, y, width - 132f, 24f), $"{i + 1}. {overlayId}  {assignedCount}/{exprCount}");
                Text.Anchor = TextAnchor.UpperLeft;

                float buttonX = width - 78f;
                if (Widgets.ButtonText(new Rect(buttonX, y + 2f, 22f, 20f), "↑") && i > 0)
                {
                    MoveOverlayGroup(fc, overlayId, -1);
                    return;
                }

                if (Widgets.ButtonText(new Rect(buttonX + 24f, y + 2f, 22f, 20f), "↓") && i < overlayIds.Count - 1)
                {
                    MoveOverlayGroup(fc, overlayId, 1);
                    return;
                }

                if (Widgets.ButtonText(new Rect(buttonX + 48f, y + 2f, 22f, 20f), "×"))
                {
                    fc.RemoveOverlayGroup(overlayId);
                    isDirty = true;
                    RefreshPreview();
                    return;
                }

                y += 30f;
            }
        }

        private void MoveOverlayGroup(PawnFaceConfig fc, string overlayId, int direction)
        {
            List<string> overlayIds = fc.GetOrderedOverlayIds();
            int currentIndex = overlayIds.FindIndex(id => id.Equals(overlayId, StringComparison.OrdinalIgnoreCase));
            int targetIndex = currentIndex + direction;

            if (currentIndex < 0 || targetIndex < 0 || targetIndex >= overlayIds.Count)
            {
                return;
            }

            string otherOverlayId = overlayIds[targetIndex];
            int currentOrder = fc.GetOverlayOrder(overlayId);
            int otherOrder = fc.GetOverlayOrder(otherOverlayId);

            fc.SetOverlayOrder(overlayId, otherOrder);
            fc.SetOverlayOrder(otherOverlayId, currentOrder);
            fc.NormalizeOverlayOrders();

            isDirty = true;
            RefreshPreview();
        }

        private void DrawLayeredPartRow(
            PawnFaceConfig fc,
            ref float y,
            float width,
            LayeredFacePartType partType,
            ExpressionType expression,
            string? overlayId = null)
        {
            bool isOverlay = partType == LayeredFacePartType.Overlay;
            string resolvedOverlayId = string.IsNullOrWhiteSpace(overlayId) ? "Overlay" : overlayId!;
            string bufferKey = GetLayeredPartBufferKey(partType, expression, resolvedOverlayId);

            LayeredFacePartConfig? existing = isOverlay
                ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                : fc.GetLayeredPartConfig(partType, expression);

            string actualPath = existing?.texPath ?? string.Empty;
            bool enabled = existing?.enabled ?? !string.IsNullOrWhiteSpace(actualPath);

            Rect partRowRect = new Rect(0f, y, width, 24f);
            Widgets.DrawBoxSolid(partRowRect,
                !string.IsNullOrWhiteSpace(actualPath) ? UIHelper.AccentSoftColor : UIHelper.AlternatingRowColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(partRowRect, 1);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(6f, y, 84f, 24f), GetExpressionTypeLabel(expression));
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
                    else
                    {
                        fc.SetLayeredPart(partType, expression, actualPath);
                    }
                }

                isDirty = true;
                RefreshPreview();
                existing = isOverlay
                    ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                    : fc.GetLayeredPartConfig(partType, expression);
                actualPath = existing?.texPath ?? string.Empty;
            }

            if (!layeredPartPathBuffer.TryGetValue(bufferKey, out string bufferPath))
            {
                bufferPath = actualPath;
            }

            float pathX = 120f;
            float pathWidth = width - pathX - 58f;
            string newBufferPath = Widgets.TextField(new Rect(pathX, y + 2f, pathWidth, 20f), bufferPath);
            if (newBufferPath != bufferPath)
            {
                layeredPartPathBuffer[bufferKey] = newBufferPath;

                if (isOverlay)
                {
                    fc.SetLayeredPart(partType, expression, newBufferPath, resolvedOverlayId, fc.GetOverlayOrder(resolvedOverlayId));
                }
                else
                {
                    fc.SetLayeredPart(partType, expression, newBufferPath);
                }

                LayeredFacePartConfig? changedPart = isOverlay
                    ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                    : fc.GetLayeredPartConfig(partType, expression);
                if (changedPart != null)
                {
                    changedPart.enabled = !string.IsNullOrWhiteSpace(newBufferPath) && (existing?.enabled ?? true);
                }

                TryAutoPopulateLayeredFacePartsFromBase(fc, partType, expression, newBufferPath);

                isDirty = true;
                RefreshPreview();
            }
            else
            {
                layeredPartPathBuffer[bufferKey] = actualPath;
            }

            if (Widgets.ButtonText(new Rect(width - 54f, y + 2f, 26f, 20f), "…"))
            {
                string browsePath = actualPath;
                Find.WindowStack.Add(new Dialog_FileBrowser(browsePath, path =>
                {
                    if (isOverlay)
                    {
                        fc.SetLayeredPart(partType, expression, path ?? string.Empty, resolvedOverlayId, fc.GetOverlayOrder(resolvedOverlayId));
                    }
                    else
                    {
                        fc.SetLayeredPart(partType, expression, path ?? string.Empty);
                    }

                    LayeredFacePartConfig? changedPart = isOverlay
                        ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                        : fc.GetLayeredPartConfig(partType, expression);
                    if (changedPart != null)
                    {
                        changedPart.enabled = !string.IsNullOrWhiteSpace(path);
                    }
                    layeredPartPathBuffer[bufferKey] = path ?? string.Empty;

                    TryAutoPopulateLayeredFacePartsFromBase(fc, partType, expression, path);

                    isDirty = true;
                    RefreshPreview();
                }));
            }

            if (Widgets.ButtonText(new Rect(width - 26f, y + 2f, 22f, 20f), "×"))
            {
                if (isOverlay)
                {
                    fc.RemoveLayeredPart(partType, expression, resolvedOverlayId);
                }
                else
                {
                    fc.RemoveLayeredPart(partType, expression);
                }

                layeredPartPathBuffer[bufferKey] = string.Empty;
                isDirty = true;
                RefreshPreview();
            }

            y += 26f;
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
                        ? comp.CurEyeDirection.ToString()
                        : EyeDirection.Center.ToString();
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

            if (Widgets.ButtonText(new Rect(labelW + pathW + 2f, y + 2f, btnW, 20f), "…"))
            {
                string capturedPath = currentPath;
                Find.WindowStack.Add(new Dialog_FileBrowser(capturedPath, newPath =>
                {
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        onChanged(newPath);
                    }
                }));
            }

            if (!string.IsNullOrEmpty(currentPath))
            {
                if (Widgets.ButtonText(new Rect(labelW + pathW + btnW + 4f, y + 2f, clearW, 20f), "×"))
                {
                    onChanged(string.Empty);
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
                options.Add(new FloatMenuOption(dir.ToString(), () =>
                {
                    comp?.SetPreviewEyeDirection(localDir);
                    RefreshPreview();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}