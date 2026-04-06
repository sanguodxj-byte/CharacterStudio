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

        private static string GetFaceMotionSectionLabel(string key)
        {
            return ($"CS_Studio_Face_MotionSection_{key}").Translate();
        }

        private static string GetFaceMotionLabel(string key)
        {
            return ($"CS_Studio_Face_Motion_{key}").Translate();
        }

        private static string GetEmotionOverlayStateLabel(EmotionOverlayState state)
        {
            return ($"CS_Studio_Preview_EmotionState_{state}").Translate();
        }

        private static string GetEmotionOverlayStateLabel(string? stateKey)
        {
            if (string.IsNullOrWhiteSpace(stateKey)
                || !Enum.TryParse(stateKey, true, out EmotionOverlayState parsed))
            {
                return stateKey ?? string.Empty;
            }

            return GetEmotionOverlayStateLabel(parsed);
        }

        private static string GetOverlaySemanticLabel(string? overlayId)
        {
            string normalized = PawnFaceConfig.NormalizeOverlayId(overlayId);
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            string key = $"CS_Studio_Face_OverlaySemantic_{normalized}";
            TaggedString translated = key.Translate();
            return translated.RawText == key ? normalized : translated.ToString();
        }

        private static string GetOverlaySemanticKeyLabel(string? semanticKey)
        {
            string normalized = PawnFaceConfig.NormalizeOverlaySemanticKey(semanticKey);
            if (string.IsNullOrWhiteSpace(normalized))
                return "CS_Studio_None".Translate();

            string key = $"CS_Studio_Face_OverlaySemanticKey_{normalized}";
            TaggedString translated = key.Translate();
            if (translated.RawText != key)
                return translated.ToString();

            return normalized.Replace("_", " ");
        }

        private static string GetOverlayIdsSummary(IEnumerable<string>? overlayIds)
        {
            if (overlayIds == null)
                return "—";

            List<string> labels = overlayIds
                .Select(GetOverlaySemanticLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return labels.Count == 0 ? "—" : string.Join(" + ", labels);
        }

        private static string GetOverlayRoutingHint(PawnFaceConfig.EmotionOverlayRule rule)
        {
            int count = rule.overlayIds?.Count ?? 0;
            return count == 0 ? "未选择 overlay" : $"已选择 {count} 个 overlay";
        }

        private sealed class OverlayRouteGroup
        {
            public string signature = string.Empty;
            public List<string> overlayIds = new List<string>();
            public List<PawnFaceConfig.EmotionOverlayRule> rules = new List<PawnFaceConfig.EmotionOverlayRule>();
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
                    MutateWithUndo(() =>
                    {
                        workingSkin.faceConfig = new PawnFaceConfig();
                        RebuildEditorBuffersFromWorkingState();
                    });
                }

                y += 36f;
                Widgets.EndScrollView();
                return;
            }

            bool enabled = fc.enabled;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Face_Enable".Translate(), ref enabled);
            if (enabled != fc.enabled)
            {
                MutateWithUndo(() => fc.enabled = enabled);
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

        internal void DrawFaceMovementDialogContents(
            ref float y,
            float width,
            Func<string, bool>? isSectionExpanded = null,
            Action<string>? toggleSectionExpanded = null)
        {
            PawnFaceConfig? fc = workingSkin.faceConfig;
            if (fc == null)
            {
                DrawPropertyHint(ref y, width, "CS_Studio_Face_MovementDialog_NoFaceConfig".Translate());
                return;
            }

            if (fc.workflowMode == FaceWorkflowMode.LayeredDynamic)
            {
                DrawLayeredPartMotionSection(fc, ref y, width, isSectionExpanded, toggleSectionExpanded);
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

        internal void DrawFaceRuntimeTuningDialogContents(
            ref float y,
            float width,
            Func<string, bool>? isSectionExpanded = null,
            Action<string>? toggleSectionExpanded = null)
        {
            PawnFaceConfig? fc = workingSkin.faceConfig;
            if (fc == null)
            {
                return;
            }

            PawnEyeDirectionConfig eyeCfg = fc.eyeDirectionConfig ??= new PawnEyeDirectionConfig();
            eyeCfg.pupilMotion ??= new PawnEyeDirectionConfig.PupilMotionConfig();

            DrawPupilOffsetCoreSection(ref y, width, eyeCfg.pupilMotion);

            DrawFaceTuningSections(ref y, width, fc, isSectionExpanded, toggleSectionExpanded);
        }

        private void DrawPupilOffsetCoreSection(ref float y, float width, PawnEyeDirectionConfig.PupilMotionConfig pupilMotion)
        {
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_PupilCore_Title".Translate());
            DrawPropertyHint(ref y, width, "CS_Studio_Face_PupilCore_Hint".Translate());
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_DirLeftOffsetX"), ref pupilMotion.dirLeftOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_DirRightOffsetX"), ref pupilMotion.dirRightOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_DirUpOffsetZ"), ref pupilMotion.dirUpOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_DirDownOffsetZ"), ref pupilMotion.dirDownOffsetZ, -0.01f, 0.01f, "F6");
            y += 4f;
        }

        private void DrawOverlayMappingSection(ref float y, float width, PawnFaceConfig fc)
        {
            fc.EnsureDefaultOverlayRules();

            y += 6f;
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_OverlayRule_Title".Translate());
            DrawPropertyHint(ref y, width, "CS_Studio_Face_OverlayRule_Hint".Translate());

            foreach (PawnFaceConfig.ExpressionOverlayRule rule in fc.expressionOverlayRules)
            {
                UIHelper.DrawPropertyFieldWithButton(ref y, width, GetExpressionTypeLabel(rule.expression), GetOverlaySemanticKeyLabel(rule.semanticKey), () =>
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (string semanticKey in GetKnownOverlaySemanticKeys(fc))
                    {
                        string localSemanticKey = semanticKey;
                        options.Add(new FloatMenuOption(GetOverlaySemanticKeyLabel(localSemanticKey), () =>
                        {
                            MutateWithUndo(() => rule.semanticKey = localSemanticKey);
                        }));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                });
                y += 2f;
            }

            UIHelper.DrawPropertyFieldWithButton(ref y, width, "CS_Studio_Face_OverlayRule_AddExpression".Translate(), "CS_Studio_Add".Translate(), () =>
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
                {
                    ExpressionType localExpression = expression;
                    options.Add(new FloatMenuOption(GetExpressionTypeLabel(localExpression), () =>
                    {
                        MutateWithUndo(() => fc.expressionOverlayRules.Add(new PawnFaceConfig.ExpressionOverlayRule
                        {
                            expression = localExpression,
                            semanticKey = PawnFaceConfig.NormalizeOverlaySemanticKey(localExpression.ToString())
                        }));
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            });
            y += 4f;

            y += 4f;
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_OverlayRule_RoutingTitle".Translate());
            DrawPropertyHint(ref y, width, "为扫描到的 overlay 纹理指定 happy、sleep 等表情语义；界面会同步显示这些语义当前关联到哪些表情。没有语义绑定的 overlay 不会响应表情。");
            DrawScannedOverlaySemanticSection(fc, ref y, width);
            y += 4f;
        }

        private IEnumerable<string> GetKnownOverlaySemanticKeys(PawnFaceConfig fc)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (PawnFaceConfig.ExpressionOverlayRule rule in fc.expressionOverlayRules)
            {
                string key = PawnFaceConfig.NormalizeOverlaySemanticKey(rule.semanticKey);
                if (!string.IsNullOrWhiteSpace(key))
                    keys.Add(key);
            }

            foreach (PawnFaceConfig.EmotionOverlayRule rule in fc.emotionOverlayRules)
            {
                string key = PawnFaceConfig.NormalizeOverlaySemanticKey(rule.semanticKey);
                if (!string.IsNullOrWhiteSpace(key))
                    keys.Add(key);
            }

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                string key = PawnFaceConfig.NormalizeOverlaySemanticKey(expression.ToString());
                if (!string.IsNullOrWhiteSpace(key))
                    keys.Add(key);
            }

            return keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase);
        }

        private void DrawScannedOverlaySemanticSection(PawnFaceConfig fc, ref float y, float width)
        {
            EnsureScannedOverlayCandidates(fc);

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
                DrawScannedOverlaySemanticRow(fc, ref y, width, candidate);
            }
        }

        private void DrawScannedOverlaySemanticRow(PawnFaceConfig fc, ref float y, float width, ScannedOverlayCandidate candidate)
        {
            string mappedOverlayId = GetMappedOverlayIdForPath(fc, candidate.FilePath);
            List<string> activeSemanticKeys = GetSemanticKeysForOverlayId(fc, mappedOverlayId);
            List<ExpressionType> linkedExpressions = GetExpressionsForSemanticKeys(fc, activeSemanticKeys);
            string displayLabel = activeSemanticKeys.Count == 0
                ? "未配置表情语义"
                : string.Join(" / ", activeSemanticKeys.Select(GetOverlaySemanticKeyLabel));
            string expressionLabel = linkedExpressions.Count == 0
                ? "未关联表情"
                : string.Join(" / ", linkedExpressions.Select(GetExpressionTypeLabel));

            Rect rowRect = new Rect(0f, y, width, 38f);
            Widgets.DrawBoxSolid(rowRect, UIHelper.AlternatingRowColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rowRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(6f, y + 1f, Mathf.Max(60f, width - 140f), 18f), candidate.FileName);
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(6f, y + 19f, Mathf.Max(60f, width - 140f), 18f), expressionLabel);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Rect buttonRect = new Rect(width - 132f, y + 9f, 128f, 20f);
            if (UIHelper.DrawSelectionButton(buttonRect, displayLabel))
            {
                OpenScannedOverlaySemanticMenu(fc, candidate, mappedOverlayId, activeSemanticKeys);
            }

            TooltipHandler.TipRegion(rowRect, $"{candidate.FileName}\n语义: {displayLabel}\n表情: {expressionLabel}");

            y += 39f;
        }

        private List<ExpressionType> GetExpressionsForSemanticKeys(PawnFaceConfig fc, IEnumerable<string> semanticKeys)
        {
            HashSet<string> normalizedKeys = new HashSet<string>(semanticKeys.Select(PawnFaceConfig.NormalizeOverlaySemanticKey), StringComparer.OrdinalIgnoreCase);
            if (normalizedKeys.Count == 0)
                return new List<ExpressionType>();

            return fc.expressionOverlayRules
                .Where(rule => normalizedKeys.Contains(PawnFaceConfig.NormalizeOverlaySemanticKey(rule.semanticKey)))
                .Select(rule => rule.expression)
                .Distinct()
                .OrderBy(expression => expression.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<string> GetSemanticKeysForOverlayId(PawnFaceConfig fc, string overlayId)
        {
            string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
            if (string.IsNullOrWhiteSpace(normalizedOverlayId))
                return new List<string>();

            return fc.emotionOverlayRules
                .Where(rule => (rule.overlayIds ?? new List<string>()).Any(id => string.Equals(PawnFaceConfig.NormalizeOverlayId(id), normalizedOverlayId, StringComparison.OrdinalIgnoreCase)))
                .Select(rule => PawnFaceConfig.NormalizeOverlaySemanticKey(rule.semanticKey))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void OpenScannedOverlaySemanticMenu(PawnFaceConfig fc, ScannedOverlayCandidate candidate, string mappedOverlayId, List<string> activeSemanticKeys)
        {
            string resolvedOverlayId = string.IsNullOrWhiteSpace(mappedOverlayId)
                ? PawnFaceConfig.NormalizeOverlayId(candidate.SuggestedOverlayId)
                : PawnFaceConfig.NormalizeOverlayId(mappedOverlayId);

            if (string.IsNullOrWhiteSpace(resolvedOverlayId))
            {
                resolvedOverlayId = PawnFaceConfig.NormalizeOverlayId(Path.GetFileNameWithoutExtension(candidate.FileName));
            }

            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("清空全部语义", () =>
                {
                    MutateWithUndo(() => ApplySemanticSelectionForOverlay(fc, resolvedOverlayId, null, false, candidate.FilePath));
                })
            };

            foreach (string semanticKey in GetKnownOverlaySemanticKeys(fc))
            {
                string localSemanticKey = semanticKey;
                bool enabled = activeSemanticKeys.Any(key => string.Equals(key, localSemanticKey, StringComparison.OrdinalIgnoreCase));
                string label = (enabled ? "[√] " : "[ ] ") + GetOverlaySemanticKeyLabel(localSemanticKey);
                options.Add(new FloatMenuOption(label, () =>
                {
                    MutateWithUndo(() => ApplySemanticSelectionForOverlay(fc, resolvedOverlayId, localSemanticKey, !enabled, candidate.FilePath));
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplySemanticSelectionForOverlay(PawnFaceConfig fc, string overlayId, string? semanticKey, bool add, string filePath)
        {
            string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
            if (string.IsNullOrWhiteSpace(normalizedOverlayId))
                return;

            ClearOverlayAssignmentsForPath(fc, filePath);
            fc.SetLayeredPart(LayeredFacePartType.Overlay, ExpressionType.Neutral, filePath, normalizedOverlayId, fc.GetOverlayOrder(normalizedOverlayId));

            string normalizedSemanticKey = PawnFaceConfig.NormalizeOverlaySemanticKey(semanticKey);
            foreach (PawnFaceConfig.EmotionOverlayRule rule in fc.emotionOverlayRules.ToList())
            {
                rule.overlayIds ??= new List<string>();
                rule.overlayIds.RemoveAll(id => string.Equals(PawnFaceConfig.NormalizeOverlayId(id), normalizedOverlayId, StringComparison.OrdinalIgnoreCase));
                rule.overlayId = rule.overlayIds.FirstOrDefault() ?? string.Empty;
            }

            fc.emotionOverlayRules.RemoveAll(rule => (rule.overlayIds == null || rule.overlayIds.Count == 0)
                && string.IsNullOrWhiteSpace(PawnFaceConfig.NormalizeOverlaySemanticKey(rule.semanticKey)) == false);

            if (add && !string.IsNullOrWhiteSpace(normalizedSemanticKey))
            {
                PawnFaceConfig.EmotionOverlayRule? rule = fc.emotionOverlayRules.FirstOrDefault(existing =>
                    string.Equals(PawnFaceConfig.NormalizeOverlaySemanticKey(existing.semanticKey), normalizedSemanticKey, StringComparison.OrdinalIgnoreCase));
                if (rule == null)
                {
                    rule = new PawnFaceConfig.EmotionOverlayRule
                    {
                        semanticKey = normalizedSemanticKey,
                        overlayIds = new List<string>()
                    };
                    fc.emotionOverlayRules.Add(rule);
                }

                if (!rule.overlayIds.Any(id => string.Equals(PawnFaceConfig.NormalizeOverlayId(id), normalizedOverlayId, StringComparison.OrdinalIgnoreCase)))
                    rule.overlayIds.Add(normalizedOverlayId);

                rule.overlayId = rule.overlayIds.FirstOrDefault() ?? string.Empty;

                EnsureExpressionOverlayRuleExists(fc, normalizedSemanticKey);
            }
        }

        private void EnsureExpressionOverlayRuleExists(PawnFaceConfig fc, string normalizedSemanticKey)
        {
            if (string.IsNullOrWhiteSpace(normalizedSemanticKey))
                return;

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                string expressionKey = PawnFaceConfig.NormalizeOverlaySemanticKey(expression.ToString());
                if (!string.Equals(expressionKey, normalizedSemanticKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!fc.expressionOverlayRules.Any(rule => rule.expression == expression))
                {
                    fc.expressionOverlayRules.Add(new PawnFaceConfig.ExpressionOverlayRule
                    {
                        expression = expression,
                        semanticKey = normalizedSemanticKey
                    });
                }

                return;
            }
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
                        MutateWithUndo(() =>
                        {
                            fc.workflowMode = localMode;
                            ForceResetPreviewMannequin();
                            RebuildEditorBuffersFromWorkingState();
                        }, refreshPreview: true, refreshRenderTree: true);
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

        }

        private void MoveOverlayOrder(PawnFaceConfig fc, LayeredFacePartType partType, string overlayId, int direction)
        {
            List<string> orderedIds = fc.GetOrderedOverlayIds(partType);
            int currentIndex = orderedIds.FindIndex(id => string.Equals(id, overlayId, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                return;
            }

            int targetIndex = currentIndex + direction;
            if (targetIndex < 0 || targetIndex >= orderedIds.Count)
            {
                return;
            }

            string currentId = orderedIds[currentIndex];
            string targetId = orderedIds[targetIndex];
            int currentOrder = fc.GetOverlayOrder(currentId, partType);
            int targetOrder = fc.GetOverlayOrder(targetId, partType);

            MutateWithUndo(() =>
            {
                fc.SetOverlayOrder(currentId, targetOrder, partType);
                fc.SetOverlayOrder(targetId, currentOrder, partType);
                fc.NormalizeOverlayOrders();
                layeredPartPathBuffer.Clear();
                FaceRuntimeCompiler.ClearCache();
                PawnRenderNodeWorker_FaceComponent.ClearCache();
                PawnRenderNodeWorker_CustomLayer.ClearExternalGraphicCache();
            });
        }

        private void DrawScannedOverlayMappingSection(PawnFaceConfig fc, ref float y, float width)
        {
            EnsureScannedOverlayCandidates(fc);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_OverlayMapping_Title".Translate());

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
                    : "CS_Studio_Face_OverlayMapping_Suggested".Translate(GetOverlaySemanticLabel(candidate.SuggestedOverlayId)).ToString())
                : GetOverlaySemanticLabel(mappedOverlayId);

            Rect rowRect = new Rect(0f, y, width, 20f);
            Widgets.DrawBoxSolid(rowRect, UIHelper.AlternatingRowColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rowRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(6f, y, Mathf.Max(60f, width - 84f), 20f), candidate.FileName);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Rect buttonRect = new Rect(width - 78f, y, 78f, 20f);
            if (UIHelper.DrawSelectionButton(buttonRect, displayLabel))
            {
                OpenScannedOverlayMappingMenu(fc, candidate);
            }

            y += 21f;
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
                    MutateWithUndo(() =>
                    {
                        ClearOverlayAssignmentsForPath(fc, candidate.FilePath);
                        layeredPartPathBuffer.Clear();
                    });
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
                options.Add(new FloatMenuOption(GetOverlaySemanticLabel(localOverlayId), () =>
                {
                    MutateWithUndo(() =>
                    {
                        ClearOverlayAssignmentsForPath(fc, candidate.FilePath);
                        fc.SetLayeredPart(LayeredFacePartType.Overlay, ExpressionType.Neutral, candidate.FilePath, localOverlayId, fc.GetOverlayOrder(localOverlayId));
                        layeredPartPathBuffer.Clear();
                    });
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawLayeredPartConfigSection(PawnFaceConfig fc, ref float y, float width)
        {
            foreach (LayeredFacePartType partType in EnumerateLayeredPartEditorTypes())
            {
                DrawLayeredPartEditorGroup(fc, ref y, width, partType);
            }
        }

        private IEnumerable<LayeredFacePartType> EnumerateLayeredPartEditorTypes()
        {
            yield return LayeredFacePartType.Base;
            yield return LayeredFacePartType.Eye;
            yield return LayeredFacePartType.Pupil;
            yield return LayeredFacePartType.UpperLid;
            yield return LayeredFacePartType.LowerLid;
            yield return LayeredFacePartType.ReplacementEye;
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

            if (isOverlay)
            {
                DrawCompactOverlayPartRow(fc, ref y, width, partType, expression, resolvedOverlayId, existing, actualPath);
                return;
            }

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

            if (isOverlay)
            {
                DrawOverlayOrderControls(fc, partType, resolvedOverlayId, ref y, width);
            }

            Rect toggleRect = new Rect(92f, y + 3f, 24f, 18f);
            bool newEnabled = enabled;
            Widgets.Checkbox(toggleRect.position, ref newEnabled, 18f, false);
            TooltipHandler.TipRegion(toggleRect, "CS_Studio_Face_Layered_Enable".Translate());

            if (newEnabled != enabled)
            {
                MutateWithUndo(() =>
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
                });
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
                MutateWithUndo(() =>
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
                    PawnRenderNodeWorker_CustomLayer.ClearExternalGraphicCache();
                });
                LayeredFacePartConfig? changedPart = isOverlay
                    ? fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId)
                    : fc.GetLayeredPartConfig(partType, expression, normalizedSide);
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
                    MutateWithUndo(() =>
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
                        PawnRenderNodeWorker_CustomLayer.ClearExternalGraphicCache();
                    });
                }));
            }))
            {
            }

            if (UIHelper.DrawDangerButton(new Rect(width - 28f, y + 1f, 24f, 22f), tooltip: "CS_Studio_Clear".Translate(), onClick: () =>
            {
                MutateWithUndo(() =>
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
                    PawnRenderNodeWorker_CustomLayer.ClearExternalGraphicCache();
                });
                existing = null;
                actualPath = string.Empty;
            }))
            {
            }

            y += 26f;
        }

        private void DrawCompactOverlayPartRow(
            PawnFaceConfig fc,
            ref float y,
            float width,
            LayeredFacePartType partType,
            ExpressionType expression,
            string overlayId,
            LayeredFacePartConfig? existing,
            string actualPath)
        {
            Rect rowRect = new Rect(0f, y, width, 22f);
            Widgets.DrawBoxSolid(rowRect,
                !string.IsNullOrWhiteSpace(actualPath) ? UIHelper.AccentSoftColor : UIHelper.AlternatingRowColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rowRect, 1);
            GUI.color = Color.white;

            string displayPath = string.IsNullOrWhiteSpace(actualPath)
                ? "—"
                : Path.GetFileName(actualPath);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(6f, y, 74f, 22f), GetOverlaySemanticLabel(overlayId));

            Text.Font = GameFont.Tiny;
            GUI.color = string.IsNullOrWhiteSpace(actualPath) ? UIHelper.SubtleColor : Color.white;
            Widgets.Label(new Rect(82f, y, Mathf.Max(60f, width - 192f), 22f), displayPath);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            DrawOverlayOrderControls(fc, partType, overlayId, ref y, width);

            if (UIHelper.DrawBrowseButton(new Rect(width - 44f, y, 20f, 20f), () =>
            {
                string browsePath = actualPath;
                Find.WindowStack.Add(new Dialog_FileBrowser(browsePath, path =>
                {
                    MutateWithUndo(() =>
                    {
                        fc.SetLayeredPart(partType, expression, path ?? string.Empty, overlayId, fc.GetOverlayOrder(overlayId, partType));

                        LayeredFacePartConfig? changedPart = fc.GetLayeredPartConfig(partType, expression, overlayId);
                        if (changedPart != null)
                        {
                            changedPart.enabled = !string.IsNullOrWhiteSpace(path);
                        }

                        layeredPartPathBuffer.Clear();
                        FaceRuntimeCompiler.ClearCache();
                        PawnRenderNodeWorker_FaceComponent.ClearCache();
                        PawnRenderNodeWorker_CustomLayer.ClearExternalGraphicCache();
                    });
                }));
            }))
            {
            }

            if (UIHelper.DrawDangerButton(new Rect(width - 22f, y, 20f, 20f), tooltip: "CS_Studio_Clear".Translate(), onClick: () =>
            {
                MutateWithUndo(() =>
                {
                    fc.RemoveLayeredPart(partType, expression, overlayId);
                    layeredPartPathBuffer.Clear();
                    FaceRuntimeCompiler.ClearCache();
                    PawnRenderNodeWorker_FaceComponent.ClearCache();
                    PawnRenderNodeWorker_CustomLayer.ClearExternalGraphicCache();
                });
            }))
            {
            }

            y += 23f;
        }

        private void DrawOverlayOrderControls(PawnFaceConfig fc, LayeredFacePartType partType, string overlayId, ref float y, float width)
        {
            List<string> orderedIds = fc.GetOrderedOverlayIds(partType);
            int currentIndex = orderedIds.FindIndex(id => string.Equals(id, overlayId, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                return;
            }

            int order = fc.GetOverlayOrder(overlayId, partType);
            Rect labelRect = new Rect(width - 136f, y + 1f, 26f, 20f);
            Rect valueRect = new Rect(width - 112f, y + 1f, 18f, 20f);
            Rect upRect = new Rect(width - 90f, y + 1f, 20f, 20f);
            Rect downRect = new Rect(width - 68f, y + 1f, 20f, 20f);

            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(labelRect, "CS_Studio_Face_OverlayOrder_Label".Translate());
            GUI.color = Color.white;
            Widgets.Label(valueRect, order.ToString());
            Text.Font = GameFont.Small;

            bool canMoveUp = currentIndex > 0;
            bool canMoveDown = currentIndex < orderedIds.Count - 1;

            GUI.enabled = canMoveUp;
            if (Widgets.ButtonText(upRect, "↑"))
            {
                MoveOverlayOrder(fc, partType, overlayId, -1);
            }
            GUI.enabled = canMoveDown;
            if (Widgets.ButtonText(downRect, "↓"))
            {
                MoveOverlayOrder(fc, partType, overlayId, 1);
            }
            GUI.enabled = true;

            TooltipHandler.TipRegion(upRect, "CS_Studio_Face_OverlayOrder_MoveUp".Translate());
            TooltipHandler.TipRegion(downRect, "CS_Studio_Face_OverlayOrder_MoveDown".Translate());
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
                    MutateWithUndo(() => fc.eyeDirectionConfig = new PawnEyeDirectionConfig());
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
                    MutateWithUndo(() => eyeCfg.enabled = eyeEnabled);
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
                    MutateWithUndo(() => fc.eyeDirectionConfig = null);
                }
                GUI.color = Color.white;
                y += 28f;
            }
        }

        private void DrawLayeredPartMotionSection(
            PawnFaceConfig fc,
            ref float y,
            float width,
            Func<string, bool>? isSectionExpanded = null,
            Action<string>? toggleSectionExpanded = null)
        {
            if (!DrawCollapsibleFaceSectionHeader(ref y, width, "FaceSection", "CS_Studio_Face_MovementDialog_FaceSection".Translate(), isSectionExpanded, toggleSectionExpanded))
                return;

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

            y += 4f;
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
                MutateWithUndo(() =>
                {
                    editablePart.motionAmplitude = motionAmplitude;
                    editablePart.anchorCorrection = Vector2.zero;
                    FaceRuntimeCompiler.ClearCache();
                });
            }

            y += 4f;
        }

        private void DrawEyeDirectionMovementControls(ref float y, float width, PawnEyeDirectionConfig eyeCfg, bool includeTextureRows)
        {
            eyeCfg.eyeMotion ??= new PawnEyeDirectionConfig.EyeMotionConfig();
            eyeCfg.pupilMotion ??= new PawnEyeDirectionConfig.PupilMotionConfig();
            eyeCfg.lidMotion ??= new PawnEyeDirectionConfig.LidMotionConfig();

            y += 4f;
            float upperLidMoveDown = eyeCfg.upperLidMoveDown;
            UIHelper.DrawPropertySlider(ref y, width,
                "CS_Studio_Face_EyeDir_UpperLidMoveDown".Translate(),
                ref upperLidMoveDown, 0f, 0.02f, "F4");
            if (upperLidMoveDown != eyeCfg.upperLidMoveDown)
            {
                MutateWithUndo(() => eyeCfg.upperLidMoveDown = upperLidMoveDown);
            }

            if (!includeTextureRows)
            {
                DrawEyeMotionConfigSection(ref y, width, eyeCfg.eyeMotion, eyeCfg.pupilMotion);
                return;
            }

            y += 4f;
            DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Center".Translate(),
                eyeCfg.texCenter, path =>
                {
                    MutateWithUndo(() => eyeCfg.texCenter = path);
                });
            DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Left".Translate(),
                eyeCfg.texLeft, path =>
                {
                    MutateWithUndo(() => eyeCfg.texLeft = path);
                });
            DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Right".Translate(),
                eyeCfg.texRight, path =>
                {
                    MutateWithUndo(() => eyeCfg.texRight = path);
                });
            DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Up".Translate(),
                eyeCfg.texUp, path =>
                {
                    MutateWithUndo(() => eyeCfg.texUp = path);
                });
            DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Down".Translate(),
                eyeCfg.texDown, path =>
                {
                    MutateWithUndo(() => eyeCfg.texDown = path);
                });

            DrawEyeMotionConfigSection(ref y, width, eyeCfg.eyeMotion, eyeCfg.pupilMotion);
        }

        private void DrawEyeMotionConfigSection(
            ref float y,
            float width,
            PawnEyeDirectionConfig.EyeMotionConfig eyeMotion,
            PawnEyeDirectionConfig.PupilMotionConfig pupilMotion,
            Func<string, bool>? isSectionExpanded = null,
            Action<string>? toggleSectionExpanded = null)
        {
            if (DrawCollapsibleFaceSectionHeader(ref y, width, "EyeMotion", GetFaceMotionSectionLabel("EyeMotion"), isSectionExpanded, toggleSectionExpanded))
            {
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_SideBiasX"), ref eyeMotion.sideBiasX, 0f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_PrimaryWaveOffsetZ"), ref eyeMotion.primaryWaveOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_DirLeftOffsetX"), ref eyeMotion.dirLeftOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_DirRightOffsetX"), ref eyeMotion.dirRightOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_DirUpOffsetZ"), ref eyeMotion.dirUpOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_DirDownOffsetZ"), ref eyeMotion.dirDownOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_NeutralSoftOffsetZ"), ref eyeMotion.neutralSoftOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_NeutralLookDownOffsetZ"), ref eyeMotion.neutralLookDownOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_NeutralGlanceWaveOffsetX"), ref eyeMotion.neutralGlanceWaveOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_NeutralGlanceSideOffsetX"), ref eyeMotion.neutralGlanceSideOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_WorkFocusDownOffsetZ"), ref eyeMotion.workFocusDownOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_WorkFocusUpOffsetZ"), ref eyeMotion.workFocusUpOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_HappySoftOffsetZ"), ref eyeMotion.happySoftOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_ShockWideOffsetZ"), ref eyeMotion.shockWideOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_ScaredWideOffsetZ"), ref eyeMotion.scaredWideOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_ScaredWideWaveOffsetX"), ref eyeMotion.scaredWideWaveOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_ScaredWideSideOffsetX"), ref eyeMotion.scaredWideSideOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_ScaredFlinchOffsetZ"), ref eyeMotion.scaredFlinchOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_ScaredFlinchWaveOffsetX"), ref eyeMotion.scaredFlinchWaveOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_ScaredFlinchSideOffsetX"), ref eyeMotion.scaredFlinchSideOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_BaseAngleWave"), ref eyeMotion.baseAngleWave, -2f, 2f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_SlowWaveOffsetZ"), ref eyeMotion.slowWaveOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_ScaleXBase"), ref eyeMotion.scaleXBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Eye_ScaleXWaveAmplitude"), ref eyeMotion.scaleXWaveAmplitude, 0f, 1f, "F4");
            y += 4f;
            }

            if (DrawCollapsibleFaceSectionHeader(ref y, width, "PupilMotion", GetFaceMotionSectionLabel("PupilMotion"), isSectionExpanded, toggleSectionExpanded))
            {
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_PupilAdvanced_Title".Translate());
            DrawPropertyHint(ref y, width, "CS_Studio_Face_PupilAdvanced_Hint".Translate());
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_SideBiasX"), ref pupilMotion.sideBiasX, 0f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_SlowWaveOffsetZ"), ref pupilMotion.slowWaveOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_NeutralSoftOffsetZ"), ref pupilMotion.neutralSoftOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_NeutralLookDownOffsetZ"), ref pupilMotion.neutralLookDownOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_NeutralGlanceWaveOffsetX"), ref pupilMotion.neutralGlanceWaveOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_NeutralGlanceSideOffsetX"), ref pupilMotion.neutralGlanceSideOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_WorkFocusDownOffsetZ"), ref pupilMotion.workFocusDownOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_WorkFocusUpOffsetZ"), ref pupilMotion.workFocusUpOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_HappyOpenOffsetZ"), ref pupilMotion.happyOpenOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_ShockWideOffsetZ"), ref pupilMotion.shockWideOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_ScaredWideOffsetZ"), ref pupilMotion.scaredWideOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_ScaredWideWaveOffsetX"), ref pupilMotion.scaredWideWaveOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_ScaredWideSideOffsetX"), ref pupilMotion.scaredWideSideOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_ScaredFlinchOffsetZ"), ref pupilMotion.scaredFlinchOffsetZ, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_ScaredFlinchWaveOffsetX"), ref pupilMotion.scaredFlinchWaveOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_ScaredFlinchSideOffsetX"), ref pupilMotion.scaredFlinchSideOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_TransformAngleWave"), ref pupilMotion.transformAngleWave, -2f, 2f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_FinalWaveOffsetX"), ref pupilMotion.finalWaveOffsetX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_FocusScaleBase"), ref pupilMotion.focusScaleBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_DilatedScaleBase"), ref pupilMotion.dilatedScaleBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_DilatedMaxScaleBase"), ref pupilMotion.dilatedMaxScaleBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Pupil_ScaredPulseScaleBase"), ref pupilMotion.scaredPulseScaleBase, 0f, 3f, "F4");
            y += 4f;
            }
        }

        private bool DrawCollapsibleFaceSectionHeader(ref float y, float width, string sectionKey, string title, Func<string, bool>? isSectionExpanded, Action<string>? toggleSectionExpanded)
        {
            bool expanded = isSectionExpanded?.Invoke(sectionKey) ?? true;
            int itemCount = GetFaceSectionItemCount(sectionKey);
            Rect rect = new Rect(0f, y + 4f, width, 22f);
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            string titleWithCount = itemCount > 0 ? $"{title} ({itemCount})" : title;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 32f, rect.height), (expanded ? "▼ " : "▶ ") + titleWithCount);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonInvisible(rect))
                toggleSectionExpanded?.Invoke(sectionKey);

            y += 26f;
            return expanded;
        }

        private static int GetFaceSectionItemCount(string sectionKey)
        {
            return sectionKey switch
            {
                "FaceSection" => 2,
                "EyeMotion" => 22,
                "PupilMotion" => 26,
                "LidMotion" => 58,
                "BrowMotion" => 13,
                "MouthMotion" => 31,
                "EmotionOverlayMotion" => 17,
                _ => 0
            };
        }

        private void DrawFaceTuningSections(
            ref float y,
            float width,
            PawnFaceConfig fc,
            Func<string, bool>? isSectionExpanded = null,
            Action<string>? toggleSectionExpanded = null)
        {
            fc.browMotion ??= new PawnFaceConfig.BrowMotionConfig();
            fc.mouthMotion ??= new PawnFaceConfig.MouthMotionConfig();
            fc.emotionOverlayMotion ??= new PawnFaceConfig.EmotionOverlayMotionConfig();
            PawnEyeDirectionConfig eyeCfg = fc.eyeDirectionConfig ??= new PawnEyeDirectionConfig();
            eyeCfg.lidMotion ??= new PawnEyeDirectionConfig.LidMotionConfig();

            if (DrawCollapsibleFaceSectionHeader(ref y, width, "LidMotion", GetFaceMotionSectionLabel("LidMotion"), isSectionExpanded, toggleSectionExpanded))
            {
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperSideBiasX".Translate(), ref eyeCfg.lidMotion.upperSideBiasX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperBlinkScaleX".Translate(), ref eyeCfg.lidMotion.upperBlinkScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperBlinkScaleZ".Translate(), ref eyeCfg.lidMotion.upperBlinkScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperCloseScaleX".Translate(), ref eyeCfg.lidMotion.upperCloseScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperCloseScaleZ".Translate(), ref eyeCfg.lidMotion.upperCloseScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHalfBaseOffsetSubtract".Translate(), ref eyeCfg.lidMotion.upperHalfBaseOffsetSubtract, 0f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHalfSoftOffset".Translate(), ref eyeCfg.lidMotion.upperHalfNeutralSoftExtraOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHalfLookDownOffset".Translate(), ref eyeCfg.lidMotion.upperHalfLookDownExtraOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHalfScaredOffset".Translate(), ref eyeCfg.lidMotion.upperHalfScaredExtraOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHalfSlowWaveOffset".Translate(), ref eyeCfg.lidMotion.upperHalfSlowWaveOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHalfScaleDefault".Translate(), ref eyeCfg.lidMotion.upperHalfScaleDefault, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHalfScaleSoft".Translate(), ref eyeCfg.lidMotion.upperHalfScaleNeutralSoft, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHalfScaleLookDown".Translate(), ref eyeCfg.lidMotion.upperHalfScaleLookDown, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHalfScaleScared".Translate(), ref eyeCfg.lidMotion.upperHalfScaleScared, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHappyOffsetSoft".Translate(), ref eyeCfg.lidMotion.upperHappySoftOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHappyOffsetOpen".Translate(), ref eyeCfg.lidMotion.upperHappyOpenOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHappyScaleSoft".Translate(), ref eyeCfg.lidMotion.upperHappySoftScale, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHappyScaleOpen".Translate(), ref eyeCfg.lidMotion.upperHappyOpenScale, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHappyScaleX".Translate(), ref eyeCfg.lidMotion.upperHappyScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHappyAngleBase".Translate(), ref eyeCfg.lidMotion.upperHappyAngleBase, -20f, 20f, "F3");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHappyAngleWave".Translate(), ref eyeCfg.lidMotion.upperHappyAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperHappySlowWaveOffset".Translate(), ref eyeCfg.lidMotion.upperHappySlowWaveOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_UpperDefaultSlowWaveOffset".Translate(), ref eyeCfg.lidMotion.upperDefaultSlowWaveOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerSideBiasX".Translate(), ref eyeCfg.lidMotion.lowerSideBiasX, -0.01f, 0.01f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerBlinkOffset".Translate(), ref eyeCfg.lidMotion.lowerBlinkOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerBlinkScaleX".Translate(), ref eyeCfg.lidMotion.lowerBlinkScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerBlinkScaleZ".Translate(), ref eyeCfg.lidMotion.lowerBlinkScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerCloseOffset".Translate(), ref eyeCfg.lidMotion.lowerCloseOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerCloseScaleX".Translate(), ref eyeCfg.lidMotion.lowerCloseScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerCloseScaleZ".Translate(), ref eyeCfg.lidMotion.lowerCloseScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHalfOffset".Translate(), ref eyeCfg.lidMotion.lowerHalfOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHalfSlowWaveOffset".Translate(), ref eyeCfg.lidMotion.lowerHalfSlowWaveOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHalfScaleX".Translate(), ref eyeCfg.lidMotion.lowerHalfScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHalfScaleZ".Translate(), ref eyeCfg.lidMotion.lowerHalfScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHappyAngleBase".Translate(), ref eyeCfg.lidMotion.lowerHappyAngleBase, -20f, 20f, "F3");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHappyAngleWave".Translate(), ref eyeCfg.lidMotion.lowerHappyAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHappyOffset".Translate(), ref eyeCfg.lidMotion.lowerHappyOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHappySlowWaveOffset".Translate(), ref eyeCfg.lidMotion.lowerHappySlowWaveOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHappyScaleX".Translate(), ref eyeCfg.lidMotion.lowerHappyScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerHappyScaleZ".Translate(), ref eyeCfg.lidMotion.lowerHappyScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_LowerDefaultSlowWaveOffset".Translate(), ref eyeCfg.lidMotion.lowerDefaultSlowWaveOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericBlinkOffset".Translate(), ref eyeCfg.lidMotion.genericBlinkOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericBlinkScaleX".Translate(), ref eyeCfg.lidMotion.genericBlinkScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericBlinkScaleZ".Translate(), ref eyeCfg.lidMotion.genericBlinkScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericCloseOffset".Translate(), ref eyeCfg.lidMotion.genericCloseOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericCloseScaleX".Translate(), ref eyeCfg.lidMotion.genericCloseScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericCloseScaleZ".Translate(), ref eyeCfg.lidMotion.genericCloseScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHalfOffset".Translate(), ref eyeCfg.lidMotion.genericHalfOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHalfSlowWaveOffset".Translate(), ref eyeCfg.lidMotion.genericHalfSlowWaveOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHalfScaleX".Translate(), ref eyeCfg.lidMotion.genericHalfScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHalfScaleZ".Translate(), ref eyeCfg.lidMotion.genericHalfScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHappyAngleBase".Translate(), ref eyeCfg.lidMotion.genericHappyAngleBase, -20f, 20f, "F3");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHappyAngleWave".Translate(), ref eyeCfg.lidMotion.genericHappyAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHappyOffset".Translate(), ref eyeCfg.lidMotion.genericHappyOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHappySlowWaveOffset".Translate(), ref eyeCfg.lidMotion.genericHappySlowWaveOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHappyScaleX".Translate(), ref eyeCfg.lidMotion.genericHappyScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericHappyScaleZ".Translate(), ref eyeCfg.lidMotion.genericHappyScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericDefaultSlowWaveOffset".Translate(), ref eyeCfg.lidMotion.genericDefaultSlowWaveOffset, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, "CS_Studio_Face_LidMotion_GenericDefaultScaleZBase".Translate(), ref eyeCfg.lidMotion.genericDefaultScaleZBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Lid_GenericDefaultScaleZWaveAmplitude"), ref eyeCfg.lidMotion.genericDefaultScaleZWaveAmplitude, 0f, 1f, "F4");
            y += 4f;
            }

            if (DrawCollapsibleFaceSectionHeader(ref y, width, "BrowMotion", GetFaceMotionSectionLabel("BrowMotion"), isSectionExpanded, toggleSectionExpanded))
            {
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_AngryAngleBase"), ref fc.browMotion.angryAngleBase, -20f, 20f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_AngryAngleWave"), ref fc.browMotion.angryAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_AngryOffsetZBase"), ref fc.browMotion.angryOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_AngrySlowWaveOffsetZ"), ref fc.browMotion.angrySlowWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_AngryScaleX"), ref fc.browMotion.angryScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_AngryScaleZ"), ref fc.browMotion.angryScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_SadAngleBase"), ref fc.browMotion.sadAngleBase, -20f, 20f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_SadAngleWave"), ref fc.browMotion.sadAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_SadOffsetZBase"), ref fc.browMotion.sadOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_SadSlowWaveOffsetZ"), ref fc.browMotion.sadSlowWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_SadScaleX"), ref fc.browMotion.sadScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_SadScaleZ"), ref fc.browMotion.sadScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_HappyAngleBase"), ref fc.browMotion.happyAngleBase, -20f, 20f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_HappyAngleWave"), ref fc.browMotion.happyAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_HappyOffsetZBase"), ref fc.browMotion.happyOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_HappySlowWaveOffsetZ"), ref fc.browMotion.happySlowWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_HappyScaleX"), ref fc.browMotion.happyScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_HappyScaleZ"), ref fc.browMotion.happyScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Brow_DefaultSlowWaveOffsetZ"), ref fc.browMotion.defaultSlowWaveOffsetZ, -0.02f, 0.02f, "F6");
            y += 4f;
            }

            if (DrawCollapsibleFaceSectionHeader(ref y, width, "MouthMotion", GetFaceMotionSectionLabel("MouthMotion"), isSectionExpanded, toggleSectionExpanded))
            {
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_SmileAngleWave"), ref fc.mouthMotion.smileAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_SmileOffsetZBase"), ref fc.mouthMotion.smileOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_SmilePrimaryWaveOffsetZ"), ref fc.mouthMotion.smilePrimaryWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_SmileScaleXBase"), ref fc.mouthMotion.smileScaleXBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_SmileScaleXWave"), ref fc.mouthMotion.smileScaleXWave, 0f, 1f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_SmileScaleZ"), ref fc.mouthMotion.smileScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_OpenAngleWave"), ref fc.mouthMotion.openAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_OpenOffsetZBase"), ref fc.mouthMotion.openOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_OpenPrimaryWaveOffsetZ"), ref fc.mouthMotion.openPrimaryWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_OpenScaleX"), ref fc.mouthMotion.openScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_OpenScaleZBase"), ref fc.mouthMotion.openScaleZBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_OpenScaleZWave"), ref fc.mouthMotion.openScaleZWave, 0f, 1f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_DownAngleBase"), ref fc.mouthMotion.downAngleBase, -20f, 20f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_DownAngleWave"), ref fc.mouthMotion.downAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_DownOffsetZBase"), ref fc.mouthMotion.downOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_DownSlowWaveOffsetZ"), ref fc.mouthMotion.downSlowWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_DownScaleX"), ref fc.mouthMotion.downScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_DownScaleZ"), ref fc.mouthMotion.downScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_SleepOffsetZ"), ref fc.mouthMotion.sleepOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_SleepScaleX"), ref fc.mouthMotion.sleepScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_SleepScaleZ"), ref fc.mouthMotion.sleepScaleZ, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_EatingAngleWave"), ref fc.mouthMotion.eatingAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_EatingOffsetZBase"), ref fc.mouthMotion.eatingOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_EatingPrimaryWaveOffsetZ"), ref fc.mouthMotion.eatingPrimaryWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_EatingScaleX"), ref fc.mouthMotion.eatingScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_EatingScaleZBase"), ref fc.mouthMotion.eatingScaleZBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_EatingScaleZWave"), ref fc.mouthMotion.eatingScaleZWave, 0f, 1f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_ShockScaredAngleWave"), ref fc.mouthMotion.shockScaredAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_ShockScaredOffsetZBase"), ref fc.mouthMotion.shockScaredOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_ShockScaredPrimaryWaveOffsetZ"), ref fc.mouthMotion.shockScaredPrimaryWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_ShockScaredScaleX"), ref fc.mouthMotion.shockScaredScaleX, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_ShockScaredScaleZBase"), ref fc.mouthMotion.shockScaredScaleZBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_ShockScaredScaleZWave"), ref fc.mouthMotion.shockScaredScaleZWave, 0f, 1f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Mouth_DefaultSlowWaveOffsetZ"), ref fc.mouthMotion.defaultSlowWaveOffsetZ, -0.02f, 0.02f, "F6");
            y += 4f;
            }

            if (DrawCollapsibleFaceSectionHeader(ref y, width, "EmotionOverlayMotion", GetFaceMotionSectionLabel("EmotionOverlayMotion"), isSectionExpanded, toggleSectionExpanded))
            {
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_BlushPulseBase"), ref fc.emotionOverlayMotion.blushPulseBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_BlushPulseWave"), ref fc.emotionOverlayMotion.blushPulseWave, 0f, 1f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_BlushOffsetZBase"), ref fc.emotionOverlayMotion.blushOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_BlushSlowWaveOffsetZ"), ref fc.emotionOverlayMotion.blushSlowWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_BlushScaleZBase"), ref fc.emotionOverlayMotion.blushScaleZBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_BlushScaleZWave"), ref fc.emotionOverlayMotion.blushScaleZWave, 0f, 1f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_TearPulseBase"), ref fc.emotionOverlayMotion.tearPulseBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_TearPulseWave"), ref fc.emotionOverlayMotion.tearPulseWave, 0f, 1f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_TearAngleWave"), ref fc.emotionOverlayMotion.tearAngleWave, -5f, 5f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_TearOffsetZBase"), ref fc.emotionOverlayMotion.tearOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_TearPrimaryWaveOffsetZ"), ref fc.emotionOverlayMotion.tearPrimaryWaveOffsetZ, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_SweatPulseBase"), ref fc.emotionOverlayMotion.sweatPulseBase, 0f, 3f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_SweatPulseWave"), ref fc.emotionOverlayMotion.sweatPulseWave, 0f, 1f, "F4");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_SweatAngleWave"), ref fc.emotionOverlayMotion.sweatAngleWave, -10f, 10f, "F3");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_SweatOffsetXWave"), ref fc.emotionOverlayMotion.sweatOffsetXWave, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_SweatOffsetZBase"), ref fc.emotionOverlayMotion.sweatOffsetZBase, -0.02f, 0.02f, "F6");
            DrawFloatProperty(ref y, width, GetFaceMotionLabel("Emotion_SweatSlowWaveOffsetZ"), ref fc.emotionOverlayMotion.sweatSlowWaveOffsetZ, -0.02f, 0.02f, "F6");
            y += 4f;
            }
        }

        private void DrawFloatProperty(ref float y, float width, string label, ref float value, float min, float max, string format)
        {
            const float compactRowHeight = 22f;
            Rect rect = new Rect(0f, y, width, compactRowHeight);

            Text.Font = GameFont.Tiny;
            float actualLabelWidth = Mathf.Max(116f, Text.CalcSize(label).x + 8f);
            Widgets.Label(new Rect(rect.x, rect.y + 2f, actualLabelWidth, 18f), label);

            float inputWidth = 58f;
            float sliderWidth = Mathf.Max(40f, rect.width - actualLabelWidth - inputWidth - 5f);
            int decimals = GetDecimalsFromNumericFormatInternal(format);

            float sliderValueBefore = Mathf.Clamp(value, min, max);
            float edited = Widgets.HorizontalSlider(new Rect(rect.x + actualLabelWidth, rect.y + 3f, sliderWidth, 14f), sliderValueBefore, min, max);
            if (Math.Abs(edited - sliderValueBefore) > 0.0001f)
                edited = QuantizeFloatInternal(edited, decimals);

            string buffer = edited.ToString(string.IsNullOrWhiteSpace(format) ? "F2" : format, System.Globalization.CultureInfo.InvariantCulture);
            Rect inputRect = new Rect(rect.x + actualLabelWidth + sliderWidth + 5f, rect.y, inputWidth, 20f);
            UIHelper.TextFieldNumeric(inputRect, ref edited, ref buffer, min, max, format, label);
            edited = QuantizeFloatInternal(edited, decimals);

            if (!Mathf.Approximately(edited, value))
            {
                MutateFloatWithUndo(ref value, edited, () => FaceRuntimeCompiler.ClearCache());
            }

            Text.Font = GameFont.Small;
            y += compactRowHeight;
        }

        private static int GetDecimalsFromNumericFormatInternal(string format)
        {
            if (string.IsNullOrWhiteSpace(format) || format.Length < 2)
                return 2;

            char prefix = char.ToUpperInvariant(format[0]);
            if (prefix != 'F' && prefix != 'N')
                return 2;

            return int.TryParse(format.Substring(1), out int decimals)
                ? Mathf.Clamp(decimals, 0, 6)
                : 2;
        }

        private static float QuantizeFloatInternal(float value, int decimals)
        {
            float multiplier = Mathf.Pow(10f, Mathf.Clamp(decimals, 0, 6));
            return Mathf.Round(value * multiplier) / multiplier;
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
