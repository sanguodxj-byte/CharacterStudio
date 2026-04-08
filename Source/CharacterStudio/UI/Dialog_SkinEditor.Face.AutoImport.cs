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
        private static readonly Dictionary<string, ExpressionType> FullFaceExpressionFileAliases =
            new Dictionary<string, ExpressionType>(StringComparer.OrdinalIgnoreCase)
            {
                { "Base", ExpressionType.Neutral },
                { "Neutral", ExpressionType.Neutral },
                { "Idle", ExpressionType.Neutral },
                { "Default", ExpressionType.Neutral },
                { "Sleep", ExpressionType.Sleeping },
                { "Sleeping", ExpressionType.Sleeping },
                { "Wink", ExpressionType.Wink },
                { "Combat", ExpressionType.WaitCombat },
                { "Melee", ExpressionType.AttackMelee },
                { "Ranged", ExpressionType.AttackRanged },
            };

        private static readonly Dictionary<string, LayeredFacePartType> LayeredFacePartFileAliases =
            new Dictionary<string, LayeredFacePartType>(StringComparer.OrdinalIgnoreCase)
            {
                { "Base", LayeredFacePartType.Base },
                { "Brow", LayeredFacePartType.Brow },
                { "Brows", LayeredFacePartType.Brow },
                { "Eye", LayeredFacePartType.Eye },
                { "Eyes", LayeredFacePartType.Eye },
                { "Sclera", LayeredFacePartType.Eye },
                { "Pupil", LayeredFacePartType.Pupil },
                { "Pupils", LayeredFacePartType.Pupil },
                { "UpperLid", LayeredFacePartType.UpperLid },
                { "LidUpper", LayeredFacePartType.UpperLid },
                { "UpperLids", LayeredFacePartType.UpperLid },
                { "LowerLid", LayeredFacePartType.LowerLid },
                { "LidLower", LayeredFacePartType.LowerLid },
                { "LowerLids", LayeredFacePartType.LowerLid },
                { "ReplacementEye", LayeredFacePartType.ReplacementEye },
                { "ReplacementEyes", LayeredFacePartType.ReplacementEye },
                { "Mouth", LayeredFacePartType.Mouth },
                { "Hair", LayeredFacePartType.Hair },
                { "OverlayTop", LayeredFacePartType.OverlayTop },
            };

        private static readonly HashSet<string> DirectionalVariantTokens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "north",
                "south",
                "east",
                "west",
                "left",
                "right",
                "up",
                "down",
                "center",
            };

        private static readonly HashSet<string> ViewDirectionalVariantTokens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "north",
                "south",
                "east",
                "west",
                "up",
                "down",
                "center",
            };

        private void OpenFullFaceAutoImportDialog(PawnFaceConfig fc)
        {
            string currentBasePath = fc.GetTexPath(ExpressionType.Neutral);
            Find.WindowStack.Add(new Dialog_FileBrowser(currentBasePath, path =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                if (TryAutoPopulateFullFaceExpressionsFromBase(fc, path))
                {
                    return;
                }
            }));
        }

        private bool TryAutoPopulateFullFaceExpressionsFromBase(PawnFaceConfig fc, string? selectedPath)
        {
            if (fc == null || string.IsNullOrWhiteSpace(selectedPath))
            {
                return false;
            }

            string resolvedPath = selectedPath!.Trim();
            if (!File.Exists(resolvedPath))
            {
                Find.WindowStack.Add(new Dialog_MessageBox($"未找到文件：\n{resolvedPath}"));
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(resolvedPath) ?? string.Empty;
            if (!fileName.Equals("Base", StringComparison.OrdinalIgnoreCase)
                && !fileName.Equals("Neutral", StringComparison.OrdinalIgnoreCase))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "自动识别需要先选择基底贴图。\n\n请选择命名为 Base.png 或 Neutral.png 的文件，系统会自动扫描同文件夹中的其余表情贴图。"));
                return false;
            }

            string? directory = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                Find.WindowStack.Add(new Dialog_MessageBox("所选基底贴图所在目录无效，无法执行自动识别。"));
                return false;
            }

            AutoPopulateFullFaceExpressionsFromDirectory(fc, directory, resolvedPath);
            return true;
        }

        private void AutoPopulateFullFaceExpressionsFromDirectory(PawnFaceConfig fc, string directoryPath, string selectedBasePath)
        {
            try
            {
                if (undoMutationDepth == 0)
                    CaptureUndoSnapshot();

                List<string> files = Directory.EnumerateFiles(directoryPath)
                    .Where(IsSupportedLayeredFaceTextureFile)
                    .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (LooksLikeLayeredFaceDirectory(files))
                {
                    AutoPopulateLayeredFacePartsFromDirectory(fc, directoryPath, switchedFromFullFaceMode: true);
                    return;
                }

                var matchedExpressionPaths = new Dictionary<ExpressionType, string>();
                var ignoredFiles = new List<string>();

                foreach (string filePath in files)
                {
                    if (TryParseFullFaceExpressionFileName(filePath, out ExpressionType expression))
                    {
                        matchedExpressionPaths[expression] = filePath;
                    }
                    else
                    {
                        ignoredFiles.Add(Path.GetFileName(filePath) ?? filePath);
                    }
                }

                matchedExpressionPaths[ExpressionType.Neutral] = selectedBasePath;

                PawnFaceConfig importedFaceConfig = fc.Clone();
                importedFaceConfig.enabled = true;
                importedFaceConfig.workflowMode = FaceWorkflowMode.FullFaceSwap;
                importedFaceConfig.expressions.Clear();
                importedFaceConfig.layeredParts.Clear();
                importedFaceConfig.layeredSourceRoot = string.Empty;
                importedFaceConfig.eyeDirectionConfig = null;

                foreach (var pair in matchedExpressionPaths.OrderBy(pair => (int)pair.Key))
                {
                    importedFaceConfig.SetTexPath(pair.Key, pair.Value);
                }

                ApplyImportedFaceConfig(fc, importedFaceConfig);
                RebuildFaceImportBuffers(fc);
                SyncLayeredFacePartsToEditableLayers(fc);
                workingSkin.hideVanillaHead = false;
                ForceResetPreviewMannequin();
                FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: true);

                ShowFullFaceAutoImportSummary(directoryPath, selectedBasePath, matchedExpressionPaths, ignoredFiles);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 自动扫描整脸表情目录失败: {ex.Message}");
                Find.WindowStack.Add(new Dialog_MessageBox($"自动扫描整脸表情目录失败：\n{ex.Message}"));
            }
        }

        private bool TryParseFullFaceExpressionFileName(string filePath, out ExpressionType expression)
        {
            expression = ExpressionType.Neutral;

            string fileName = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string normalized = fileName.Trim().Replace("-", "_").Replace(" ", string.Empty);
            if (FullFaceExpressionFileAliases.TryGetValue(normalized, out expression))
            {
                return true;
            }

            return Enum.TryParse(normalized, true, out expression);
        }

        private static bool TryParseExpressionToken(string? token, out ExpressionType expression)
        {
            expression = ExpressionType.Neutral;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            string normalized = token!.Trim().Replace("-", "_").Replace(" ", string.Empty);
            if (FullFaceExpressionFileAliases.TryGetValue(normalized, out expression))
                return true;

            return Enum.TryParse(normalized, true, out expression);
        }

        private void ShowFullFaceAutoImportSummary(
            string directoryPath,
            string selectedBasePath,
            Dictionary<ExpressionType, string> matchedExpressionPaths,
            List<string> ignoredFiles)
        {
            List<string> matchedLines = matchedExpressionPaths
                .OrderBy(pair => (int)pair.Key)
                .Select(pair => $"• {GetExpressionTypeLabel(pair.Key)} ← {Path.GetFileName(pair.Value)}")
                .ToList();

            List<string> missingMinSet = MinSetExpressions
                .Where(expression => !matchedExpressionPaths.ContainsKey(expression))
                .OrderBy(expression => (int)expression)
                .Select(expression => $"• {GetExpressionTypeLabel(expression)}")
                .ToList();

            string summary =
                "表情自动识别完成。\n\n"
                + $"基底贴图：{Path.GetFileName(selectedBasePath)}\n"
                + $"目录：{directoryPath}\n"
                + $"识别到表情：{matchedExpressionPaths.Count} / {Enum.GetValues(typeof(ExpressionType)).Length}\n\n"
                + "已匹配文件：\n"
                + (matchedLines.Count > 0 ? string.Join("\n", matchedLines) : "（无）")
                + "\n\n推荐最小集缺失：\n"
                + (missingMinSet.Count > 0 ? string.Join("\n", missingMinSet) : "（已完整）")
                + "\n\n未识别文件："
                + (ignoredFiles.Count > 0 ? $"\n{string.Join("\n", ignoredFiles.Select(file => $"• {file}"))}" : "（无）")
                + "\n\n命名规则：Base / Neutral / Happy / Sad / Angry / Sleeping / Blink / Dead ...";

            Find.WindowStack.Add(new Dialog_MessageBox(summary));
        }

        private bool TryAutoPopulateLayeredFacePartsFromBase(
            PawnFaceConfig fc,
            LayeredFacePartType partType,
            ExpressionType expression,
            string? selectedPath)
        {
            if (partType != LayeredFacePartType.Base
                || expression != ExpressionType.Neutral
                || string.IsNullOrWhiteSpace(selectedPath))
            {
                return false;
            }

            string resolvedPath = selectedPath!.Trim();
            if (!File.Exists(resolvedPath))
            {
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(resolvedPath) ?? string.Empty;
            if (!fileName.Equals("Base", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string? directory = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            AutoPopulateLayeredFacePartsFromDirectory(fc, directory);
            return true;
        }

        private bool TryAppendLayeredFacePartsFromSelection(PawnFaceConfig fc, string? selectedPath)
        {
            if (fc == null || string.IsNullOrWhiteSpace(selectedPath))
                return false;

            string resolvedPath = selectedPath!.Trim();
            string? directory = null;

            if (Directory.Exists(resolvedPath))
            {
                directory = resolvedPath;
            }
            else if (File.Exists(resolvedPath))
            {
                directory = Path.GetDirectoryName(resolvedPath);
            }

            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return false;

            AutoPopulateLayeredFacePartsFromDirectory(fc, directory!, appendOnly: true);
            return true;
        }

        private void AutoPopulateLayeredFacePartsFromDirectory(PawnFaceConfig fc, string directoryPath, bool switchedFromFullFaceMode = false, bool appendOnly = false)
        {
            Dictionary<string, string> originalLayeredPartPathBuffer = new Dictionary<string, string>(layeredPartPathBuffer, StringComparer.OrdinalIgnoreCase);
            Dictionary<ExpressionType, string> originalExprPathBuffer = new Dictionary<ExpressionType, string>(exprPathBuffer);

            try
            {
                if (undoMutationDepth == 0)
                    CaptureUndoSnapshot();

                PawnFaceConfig importedFaceConfig = fc.Clone();
                if (!appendOnly)
                    importedFaceConfig.layeredParts.Clear();
                importedFaceConfig.expressions.Clear();
                importedFaceConfig.enabled = true;
                importedFaceConfig.workflowMode = FaceWorkflowMode.LayeredDynamic;
                importedFaceConfig.layeredSourceRoot = directoryPath;
                if (!appendOnly)
                    importedFaceConfig.eyeDirectionConfig = null;

                List<string> files = Directory.EnumerateFiles(directoryPath)
                    .Where(IsSupportedLayeredFaceTextureFile)
                    .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (files.Count == 0)
                {
                    Find.WindowStack.Add(new Dialog_MessageBox($"未在目录中找到可识别贴图：\n{directoryPath}"));
                    return;
                }

                var detectedFileNames = new HashSet<string>(
                    files.Select(file => Path.GetFileNameWithoutExtension(file) ?? string.Empty),
                    StringComparer.OrdinalIgnoreCase);
                var detectedFilePathMap = files
                    .GroupBy(file => Path.GetFileNameWithoutExtension(file) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                var recognizedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var matchedLines = new List<string>();
                var synthesizedLines = new List<string>();
                var overlayOrderById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                int GetOverlayOrder(string? overlayId)
                {
                    string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
                    if (!overlayOrderById.TryGetValue(normalizedOverlayId, out int overlayOrder))
                    {
                        overlayOrder = PawnFaceConfig.GetCanonicalOverlayOrder(normalizedOverlayId);
                        if (overlayOrderById.Values.Contains(overlayOrder))
                        {
                            overlayOrder = overlayOrderById.Values.DefaultIfEmpty(2).Max() + 1;
                        }

                        overlayOrderById[normalizedOverlayId] = overlayOrder;
                    }

                    return overlayOrder;
                }

                bool TryGetFirstExistingPath(IEnumerable<string> stemCandidates, out string resolvedPath, out string matchedStem)
                {
                    foreach (string stem in stemCandidates)
                    {
                        if (detectedFilePathMap.TryGetValue(stem, out string filePath))
                        {
                            resolvedPath = filePath;
                            matchedStem = stem;
                            return true;
                        }
                    }

                    resolvedPath = string.Empty;
                    matchedStem = string.Empty;
                    return false;
                }

                bool ApplyRecognized(
                    LayeredFacePartType partType,
                    ExpressionType expression,
                    string texturePath,
                    string sourceStem,
                    string? overlayId = null,
                    LayeredFacePartSide side = LayeredFacePartSide.None,
                    Rot4? facing = null)
                {
                    Rot4 resolvedFacing = facing ?? Rot4.South;
                    if (appendOnly && HasImportedLayeredEntry(importedFaceConfig, partType, expression, overlayId, side, resolvedFacing))
                        return false;

                    ResolveAutoImportDirectionalPaths(
                        detectedFilePathMap,
                        texturePath,
                        sourceStem,
                        resolvedFacing,
                        out string texPathSouth,
                        out string texPathEast,
                        out string texPathNorth,
                        out List<string> recognizedDirectionalStems);

                    int overlayOrder = PawnFaceConfig.IsOverlayPart(partType) ? GetOverlayOrder(overlayId) : 0;
                    ApplyAutoScannedLayeredPart(
                        importedFaceConfig,
                        partType,
                        expression,
                        texPathSouth,
                        texPathEast,
                        texPathNorth,
                        overlayId,
                        overlayOrder,
                        side);

                    foreach (string recognizedStem in recognizedDirectionalStems)
                    {
                        recognizedFileNames.Add(recognizedStem);
                    }

                    recognizedFileNames.Add(sourceStem);

                    List<string> directionalFiles = new List<string>();
                    if (!string.IsNullOrWhiteSpace(texPathSouth))
                    {
                        directionalFiles.Add($"S:{Path.GetFileName(texPathSouth)}");
                    }

                    if (!string.IsNullOrWhiteSpace(texPathEast))
                    {
                        directionalFiles.Add($"E/W:{Path.GetFileName(texPathEast)}");
                    }

                    if (!string.IsNullOrWhiteSpace(texPathNorth))
                    {
                        directionalFiles.Add($"N:{Path.GetFileName(texPathNorth)}");
                    }

                    string importDetail = directionalFiles.Count > 0
                        ? string.Join(", ", directionalFiles)
                        : Path.GetFileName(texturePath);
                    matchedLines.Add($"• {FormatLayeredAutoImportMatchLabel(partType, expression, overlayId, side)} ← {importDetail}");
                    return true;
                }

                bool TryApplyFirstAvailableStem(
                    LayeredFacePartType partType,
                    ExpressionType expression,
                    IEnumerable<string> stemCandidates,
                    string? overlayId = null,
                    LayeredFacePartSide side = LayeredFacePartSide.None)
                {
                    if (!TryGetFirstExistingPath(stemCandidates, out string resolvedPath, out string matchedStem))
                    {
                        return false;
                    }

                    return ApplyRecognized(partType, expression, resolvedPath, matchedStem, overlayId, side);
                }

                bool TryApplyExpressionGroup(
                    LayeredFacePartType partType,
                    IEnumerable<ExpressionType> expressions,
                    IEnumerable<string> stemCandidates,
                    string? overlayId = null,
                    LayeredFacePartSide side = LayeredFacePartSide.None)
                {
                    if (!TryGetFirstExistingPath(stemCandidates, out string resolvedPath, out string matchedStem))
                    {
                        return false;
                    }

                    bool applied = false;
                    foreach (ExpressionType expression in expressions)
                    {
                        ApplyRecognized(partType, expression, resolvedPath, matchedStem, overlayId, side);
                        applied = true;
                    }

                    return applied;
                }

                bool TryApplyPairedNeutralParts(
                    LayeredFacePartType partType,
                    IEnumerable<string> leftStemCandidates,
                    IEnumerable<string> rightStemCandidates)
                {
                    bool applied = false;
                    applied |= TryApplyFirstAvailableStem(
                        partType,
                        ExpressionType.Neutral,
                        leftStemCandidates,
                        side: LayeredFacePartSide.Left);
                    applied |= TryApplyFirstAvailableStem(
                        partType,
                        ExpressionType.Neutral,
                        rightStemCandidates,
                        side: LayeredFacePartSide.Right);
                    return applied;
                }

                bool hasLayeredFaceBase = TryApplyFirstAvailableStem(LayeredFacePartType.Base, ExpressionType.Neutral, new[] { "Base" });

                TryApplyFirstAvailableStem(LayeredFacePartType.Brow, ExpressionType.Neutral, new[] { "Brows", "Brow" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.Brow,
                    new[] { "Brow_Left", "Brows_Left" },
                    new[] { "Brow_Right", "Brows_Right" });

                TryApplyFirstAvailableStem(LayeredFacePartType.Eye, ExpressionType.Neutral, new[] { "Sclera", "Eye" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.Eye,
                    new[] { "Eye_Left", "Sclera_Left" },
                    new[] { "Eye_Right", "Sclera_Right" });

                TryApplyFirstAvailableStem(LayeredFacePartType.UpperLid, ExpressionType.Neutral, new[] { "LidUpper", "UpperLid" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.UpperLid,
                    new[] { "UpperLid_Left", "LidUpper_Left" },
                    new[] { "UpperLid_Right", "LidUpper_Right" });

                TryApplyFirstAvailableStem(LayeredFacePartType.LowerLid, ExpressionType.Neutral, new[] { "LidLower", "LowerLid" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.LowerLid,
                    new[] { "LowerLid_Left", "LidLower_Left" },
                    new[] { "LowerLid_Right", "LidLower_Right" });

                TryApplyFirstAvailableStem(LayeredFacePartType.Mouth, ExpressionType.Neutral, new[] { "Mouth" });

                TryApplyFirstAvailableStem(LayeredFacePartType.Pupil, ExpressionType.Neutral, new[] { "Pupil", "Pupil_Center" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.Pupil,
                    new[] { "Pupil_Left" },
                    new[] { "Pupil_Right" });

                TryApplyFirstAvailableStem(LayeredFacePartType.Hair, ExpressionType.Neutral, new[] { "Hair_front" }, "front");
                TryApplyFirstAvailableStem(LayeredFacePartType.Hair, ExpressionType.Neutral, new[] { "Hair_back" }, "back");
                TryApplyFirstAvailableStem(LayeredFacePartType.Hair, ExpressionType.Neutral, new[] { "Hair_east", "Hair_side" }, "east");
                TryApplyFirstAvailableStem(LayeredFacePartType.Hair, ExpressionType.Neutral, new[] { "Hair_north" }, "north");
                TryApplyFirstAvailableStem(LayeredFacePartType.Hair, ExpressionType.Neutral, new[] { "Hair" });

                TryApplyFirstAvailableStem(LayeredFacePartType.ReplacementEye, ExpressionType.Blink, new[] { "Eye_blink" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.ReplacementEye,
                    new[] { "Eye_blink_Left", "ReplacementEye_Left_blink", "ReplacementEye_Left_Blink" },
                    new[] { "Eye_blink_Right", "ReplacementEye_Right_blink", "ReplacementEye_Right_Blink" });

                bool hasDeadEye = TryApplyFirstAvailableStem(LayeredFacePartType.ReplacementEye, ExpressionType.Dead, new[] { "Eye_death", "Eye_dead" });
                TryApplyFirstAvailableStem(LayeredFacePartType.ReplacementEye, ExpressionType.Sleeping, new[] { "Eye_closed" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.ReplacementEye,
                    new[] { "Eye_closed_Left", "ReplacementEye_Left_closed", "ReplacementEye_Left_Close" },
                    new[] { "Eye_closed_Right", "ReplacementEye_Right_closed", "ReplacementEye_Right_Close" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.ReplacementEye,
                    new[] { "Eye_death_Left", "Eye_dead_Left", "ReplacementEye_Left_dead", "ReplacementEye_Left_Death" },
                    new[] { "Eye_death_Right", "Eye_dead_Right", "ReplacementEye_Right_dead", "ReplacementEye_Right_Death" });
                if (!hasDeadEye)
                {
                    TryApplyFirstAvailableStem(LayeredFacePartType.ReplacementEye, ExpressionType.Dead, new[] { "Eye_closed" });
                }

                TryApplyExpressionGroup(
                    LayeredFacePartType.ReplacementEye,
                    new[] { ExpressionType.Happy, ExpressionType.Cheerful, ExpressionType.Lovin, ExpressionType.SocialRelax },
                    new[] { "Eye_closed_happy" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.ReplacementEye,
                    new[] { "Eye_closed_happy_Left", "Eye_happy_closed_Left", "ReplacementEye_Left_happy" },
                    new[] { "Eye_closed_happy_Right", "Eye_happy_closed_Right", "ReplacementEye_Right_happy" });
                TryApplyExpressionGroup(
                    LayeredFacePartType.ReplacementEye,
                    new[] { ExpressionType.Shock, ExpressionType.Scared },
                    new[] { "Eye_shock", "Eye_scared", "Eye_wide" });
                TryApplyPairedNeutralParts(
                    LayeredFacePartType.ReplacementEye,
                    new[] { "Eye_shock_Left", "Eye_scared_Left", "Eye_wide_Left" },
                    new[] { "Eye_shock_Right", "Eye_scared_Right", "Eye_wide_Right" });

                TryApplyExpressionGroup(
                    LayeredFacePartType.Mouth,
                    new[] { ExpressionType.Happy, ExpressionType.Cheerful, ExpressionType.SocialRelax, ExpressionType.Lovin },
                    new[] { "Mouth_smile" });

                TryApplyExpressionGroup(
                    LayeredFacePartType.Mouth,
                    new[] { ExpressionType.Eating, ExpressionType.AttackMelee, ExpressionType.AttackRanged, ExpressionType.Scared },
                    new[] { "Mouth_speak", "Mouth_ohoho", "Mouth_open" });

                TryApplyExpressionGroup(
                    LayeredFacePartType.Mouth,
                    new[] { ExpressionType.Gloomy, ExpressionType.Sad, ExpressionType.Hopeless, ExpressionType.Pain, ExpressionType.Tired, ExpressionType.LayDown },
                    new[] { "Mouth_think", "Mouth_down" });

                TryApplyExpressionGroup(
                    LayeredFacePartType.Mouth,
                    new[] { ExpressionType.Sleeping, ExpressionType.Dead },
                    new[] { "Mouth_sleep", "Mouth_closed" });

                TryApplyFirstAvailableStem(LayeredFacePartType.Overlay, ExpressionType.Neutral, new[] { "Overlay_blush", "Blush" }, "Blush");
                TryApplyFirstAvailableStem(LayeredFacePartType.Overlay, ExpressionType.Neutral, new[] { "Overlay_tear", "Tear" }, "Tear");
                TryApplyFirstAvailableStem(LayeredFacePartType.Overlay, ExpressionType.Neutral, new[] { "Overlay_sweat", "Sweat" }, "Sweat");
                TryApplyFirstAvailableStem(LayeredFacePartType.Overlay, ExpressionType.Neutral, new[] { "Overlay_sleep", "Sleep" }, "Sleep");
                TryApplyFirstAvailableStem(LayeredFacePartType.Overlay, ExpressionType.Neutral, new[] { "Overlay_gloomy", "Gloomy" }, "Gloomy");

                TryApplyExpressionGroup(
                    LayeredFacePartType.Overlay,
                    new[] { ExpressionType.Lovin },
                    new[] { "Overlay_love", "Overlay_lovin" },
                    "Blush");

                TryApplyExpressionGroup(
                    LayeredFacePartType.Overlay,
                    new[] { ExpressionType.Gloomy, ExpressionType.Sad, ExpressionType.Hopeless, ExpressionType.Pain },
                    new[] { "Overlay_black", "Overlay_Nolight", "Overlay_NoLight", "Overlay_dark" },
                    "Gloomy");

                foreach (string filePath in files)
                {
                    string fileStem = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
                    if (recognizedFileNames.Contains(fileStem))
                    {
                        continue;
                    }

                    if (!TryParseLayeredFaceFileName(
                            filePath,
                            out LayeredFacePartType scannedPartType,
                            out LayeredFacePartSide scannedSide,
                            out string? overlayId,
                            out string? suffix,
                            out Rot4 scannedFacing))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(suffix))
                    {
                        ApplyRecognized(scannedPartType, ExpressionType.Neutral, filePath, fileStem, overlayId, scannedSide, scannedFacing);
                        continue;
                    }

                    if (TryParseExpressionToken(suffix, out ExpressionType scannedExpression))
                    {
                        ApplyRecognized(scannedPartType, scannedExpression, filePath, fileStem, overlayId, scannedSide, scannedFacing);
                    }
                }

                foreach (string filePath in files)
                {
                    string fileStem = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
                    if (recognizedFileNames.Contains(fileStem) || !LooksLikeLayeredVariantFileName(fileStem))
                        continue;

                    if (!TryParseLayeredFaceFileName(
                            filePath,
                            out LayeredFacePartType scannedPartType,
                            out LayeredFacePartSide scannedSide,
                            out string? overlayId,
                            out string? suffix,
                            out Rot4 scannedFacing))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(suffix) || !TryParseExpressionToken(suffix, out ExpressionType scannedExpression))
                        continue;

                    string virtualBasePath = BuildVirtualVariantBasePath(filePath, fileStem);
                    ApplyRecognized(scannedPartType, scannedExpression, virtualBasePath, fileStem, overlayId, scannedSide, scannedFacing);
                    synthesizedLines.Add($"• {FormatLayeredAutoImportMatchLabel(scannedPartType, scannedExpression, overlayId, scannedSide)} ← {Path.GetFileName(filePath)} (虚拟基底)");
                }

                AutoConfigureProgrammaticLayeredFaceLogic(importedFaceConfig, detectedFileNames, detectedFilePathMap);
                InheritUnsidedDirectionalPathsForPairedParts(importedFaceConfig);
                NormalizeImportedPairedLayeredFaceParts(importedFaceConfig);

                List<string> runtimeVariantFiles = files
                    .Where(file =>
                    {
                        string stem = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
                        return !recognizedFileNames.Contains(stem) && LooksLikeLayeredVariantFileName(stem);
                    })
                    .Select(file => $"• {Path.GetFileName(file)}")
                    .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                List<string> ignoredFiles = files
                    .Where(file =>
                    {
                        string stem = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
                        return !recognizedFileNames.Contains(stem) && !LooksLikeLayeredVariantFileName(stem);
                    })
                    .Select(file => $"• {Path.GetFileName(file)}")
                    .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                ApplyImportedFaceConfig(fc, importedFaceConfig);
                SyncImportedLayeredFaceBaseToHeadSlot(fc, hasLayeredFaceBase);
                RebuildFaceImportBuffers(fc);
                SyncLayeredFacePartsToEditableLayers(fc);
                if (!appendOnly)
                    workingSkin.hideVanillaHead = hasLayeredFaceBase;

                ForceResetPreviewMannequin();
                FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: true);

                ShowLayeredFaceAutoImportSummary(
                    directoryPath,
                    fc,
                    matchedLines,
                    synthesizedLines,
                    runtimeVariantFiles,
                    ignoredFiles,
                    switchedFromFullFaceMode || appendOnly);
            }
            catch (Exception ex)
            {
                RestoreFaceImportBuffers(originalLayeredPartPathBuffer, originalExprPathBuffer);
                Log.Warning($"[CharacterStudio] 自动扫描分层表情目录失败: {ex.Message}");
                Find.WindowStack.Add(new Dialog_MessageBox($"自动扫描分层表情目录失败：\n{ex.Message}"));
            }
        }

        private void ApplyAutoScannedLayeredPart(
            PawnFaceConfig fc,
            LayeredFacePartType partType,
            ExpressionType expression,
            string texPathSouth,
            string texPathEast,
            string texPathNorth,
            string? overlayId = null,
            int overlayOrder = 0,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            string primaryTexturePath = !string.IsNullOrWhiteSpace(texPathSouth)
                ? texPathSouth
                : !string.IsNullOrWhiteSpace(texPathEast)
                    ? texPathEast
                    : texPathNorth;

            if (PawnFaceConfig.IsOverlayPart(partType))
            {
                string resolvedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
                if (string.IsNullOrWhiteSpace(resolvedOverlayId))
                    return;

                fc.SetLayeredPartDirectional(partType, expression, texPathSouth, texPathEast, texPathNorth, resolvedOverlayId, overlayOrder);

                LayeredFacePartConfig? overlayConfig = fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId);
                if (overlayConfig != null)
                {
                    overlayConfig.enabled = !string.IsNullOrWhiteSpace(texPathSouth)
                        || !string.IsNullOrWhiteSpace(texPathEast)
                        || !string.IsNullOrWhiteSpace(texPathNorth);
                    overlayConfig.overlayId = resolvedOverlayId;
                    overlayConfig.overlayOrder = overlayOrder;
                }

                layeredPartPathBuffer[GetLayeredPartBufferKey(partType, expression, resolvedOverlayId)] = primaryTexturePath ?? string.Empty;
                return;
            }

            LayeredFacePartSide normalizedSide = PawnFaceConfig.NormalizePartSide(partType, side);
            fc.SetLayeredPartDirectional(partType, expression, texPathSouth, texPathEast, texPathNorth, normalizedSide);

            LayeredFacePartConfig? partConfig = fc.GetLayeredPartConfig(partType, expression, normalizedSide);
            if (partConfig != null)
            {
                partConfig.enabled = !string.IsNullOrWhiteSpace(texPathSouth)
                    || !string.IsNullOrWhiteSpace(texPathEast)
                    || !string.IsNullOrWhiteSpace(texPathNorth);
                partConfig.side = normalizedSide;
            }

            layeredPartPathBuffer[GetLayeredPartBufferKey(partType, expression, side: normalizedSide)] = primaryTexturePath ?? string.Empty;
        }

        private void ApplyImportedFaceConfig(PawnFaceConfig target, PawnFaceConfig source)
        {
            target.enabled = source.enabled;
            target.workflowMode = source.workflowMode;
            target.layeredSourceRoot = source.layeredSourceRoot ?? string.Empty;

            target.expressions.Clear();
            foreach (ExpressionTexPath exp in source.expressions)
            {
                var clonedExp = new ExpressionTexPath
                {
                    expression = exp.expression,
                    texPath = exp.texPath
                };

                if (exp.frames != null)
                {
                    foreach (ExpressionFrame frame in exp.frames)
                    {
                        clonedExp.frames.Add(new ExpressionFrame
                        {
                            texPath = frame.texPath,
                            durationTicks = frame.durationTicks
                        });
                    }
                }

                target.expressions.Add(clonedExp);
            }

            target.layeredParts ??= new List<LayeredFacePartConfig>();
            target.layeredParts.Clear();
            if (source.layeredParts != null)
            {
                foreach (LayeredFacePartConfig part in source.layeredParts)
                {
                    if (part != null)
                    {
                        target.layeredParts.Add(part.Clone());
                    }
                }
            }

            target.eyeDirectionConfig = source.eyeDirectionConfig?.Clone();
        }

        private void RebuildFaceImportBuffers(PawnFaceConfig fc)
        {
            layeredPartPathBuffer.Clear();
            exprPathBuffer.Clear();

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                exprPathBuffer[expression] = fc.GetTexPath(expression);
            }

            if (fc?.layeredParts == null)
            {
                return;
            }

            foreach (LayeredFacePartConfig part in fc.layeredParts)
            {
                if (part == null)
                {
                    continue;
                }

                string bufferKey = PawnFaceConfig.IsOverlayPart(part.partType)
                    ? GetLayeredPartBufferKey(part.partType, part.expression, PawnFaceConfig.NormalizeOverlayId(part.overlayId))
                    : GetLayeredPartBufferKey(part.partType, part.expression, side: part.side);

                layeredPartPathBuffer[bufferKey] = part.texPath ?? string.Empty;
            }
        }

        private void RestoreFaceImportBuffers(
            Dictionary<string, string> layeredBufferSnapshot,
            Dictionary<ExpressionType, string> expressionBufferSnapshot)
        {
            layeredPartPathBuffer.Clear();
            foreach (var pair in layeredBufferSnapshot)
            {
                layeredPartPathBuffer[pair.Key] = pair.Value;
            }

            exprPathBuffer.Clear();
            foreach (var pair in expressionBufferSnapshot)
            {
                exprPathBuffer[pair.Key] = pair.Value;
            }
        }

        private void SyncImportedLayeredFaceBaseToHeadSlot(PawnFaceConfig fc, bool hasLayeredFaceBase)
        {
            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            workingSkin.baseAppearance.EnsureAllSlotsExist();

            BaseAppearanceSlotConfig headSlot = workingSkin.baseAppearance.GetSlot(BaseAppearanceSlotType.Head);
            if (!hasLayeredFaceBase)
            {
                if (headSlot != null
                    && headSlot.enabled
                    && string.IsNullOrWhiteSpace(headSlot.texPath))
                {
                    headSlot.enabled = false;
                }

                return;
            }

            string layeredBasePath = fc.GetAnyDirectionalLayeredPartPath(LayeredFacePartType.Base, Rot4.South);
            if (string.IsNullOrWhiteSpace(layeredBasePath))
            {
                layeredBasePath = fc.GetAnyLayeredPartPath(LayeredFacePartType.Base);
            }

            if (string.IsNullOrWhiteSpace(layeredBasePath))
            {
                return;
            }

            headSlot.enabled = true;
            headSlot.slotType = BaseAppearanceSlotType.Head;
            headSlot.texPath = layeredBasePath;
            if (string.IsNullOrWhiteSpace(headSlot.shaderDefName))
            {
                headSlot.shaderDefName = "Cutout";
            }
        }

        private void SyncLayeredFacePartsToEditableLayers(PawnFaceConfig fc)
        {
            workingSkin.layers ??= new List<PawnLayerConfig>();

            int insertIndex = workingSkin.layers.FindIndex(layer => IsEditableFaceLayer(layer));
            if (insertIndex < 0)
            {
                insertIndex = workingSkin.layers.Count;
            }

            Dictionary<string, PawnLayerConfig> existingFaceLayers = workingSkin.layers
                .Where(layer => IsEditableFaceLayer(layer))
                .GroupBy(layer => layer.layerName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Clone(), StringComparer.OrdinalIgnoreCase);

            workingSkin.layers.RemoveAll(layer => IsEditableFaceLayer(layer));

            if (fc?.layeredParts == null || fc.layeredParts.Count == 0)
            {
                return;
            }

            var logicalLayers = fc.layeredParts
                .Where(part => part != null
                    && part.partType != LayeredFacePartType.Base
                    && part.enabled
                    && (!string.IsNullOrWhiteSpace(part.texPath)
                        || !string.IsNullOrWhiteSpace(part.texPathSouth)
                        || !string.IsNullOrWhiteSpace(part.texPathEast)
                        || !string.IsNullOrWhiteSpace(part.texPathNorth)))
                .Select(part =>
                {
                    LayeredFacePartType displayPartType = PawnFaceConfig.IsOverlayPart(part.partType)
                        ? PawnFaceConfig.GetOverlayDisplayPartType(part.overlayId, part.partType)
                        : part.partType;
                    string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(part.overlayId);
                    LayeredFacePartSide normalizedSide = PawnFaceConfig.IsOverlayPart(part.partType)
                        ? LayeredFacePartSide.None
                        : PawnFaceConfig.NormalizePartSide(part.partType, part.side);
                    string preferredTexturePath = !string.IsNullOrWhiteSpace(part.texPathSouth)
                        ? part.texPathSouth
                        : !string.IsNullOrWhiteSpace(part.texPath)
                            ? part.texPath
                            : !string.IsNullOrWhiteSpace(part.texPathEast)
                                ? part.texPathEast
                                : part.texPathNorth;

                    return new
                    {
                        Part = part,
                        DisplayPartType = displayPartType,
                        NormalizedOverlayId = normalizedOverlayId,
                        Side = normalizedSide,
                        PreferredTexturePath = preferredTexturePath,
                        LayerName = GetLayeredFaceEditableLayerName(displayPartType, normalizedOverlayId, normalizedSide)
                    };
                })
                .GroupBy(entry => entry.LayerName, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var preferred = group
                        .OrderBy(entry => entry.Part.expression != ExpressionType.Neutral)
                        .ThenBy(entry => entry.Part.overlayOrder)
                        .ThenBy(entry => entry.Part.expression)
                        .First();

                    return new
                    {
                        preferred.LayerName,
                        preferred.DisplayPartType,
                        preferred.NormalizedOverlayId,
                        preferred.Side,
                        OverlayOrder = group.Min(entry => entry.Part.overlayOrder),
                        TexturePath = preferred.PreferredTexturePath
                    };
                })
                .OrderBy(entry => GetLayeredFaceDefaultDrawOrder(entry.DisplayPartType, entry.OverlayOrder, entry.NormalizedOverlayId))
                .ThenBy(entry => entry.Side)
                .ThenBy(entry => entry.LayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<PawnLayerConfig> syncedFaceLayers = logicalLayers
                .Select(entry =>
                {
                        existingFaceLayers.TryGetValue(entry.LayerName, out PawnLayerConfig? existingLayer);
                        return BuildSyncedLayeredFaceEditableLayer(
                            existingLayer,
                            entry.DisplayPartType,
                            entry.NormalizedOverlayId,
                            entry.TexturePath,
                            entry.OverlayOrder,
                            entry.Side);
                })
                .ToList();

            workingSkin.layers.InsertRange(insertIndex, syncedFaceLayers);
        }

        private static bool IsEditableFaceLayer(PawnLayerConfig? layer)
        {
            return layer != null
                && !string.IsNullOrWhiteSpace(layer.layerName)
                && layer.layerName.StartsWith("[Face] ", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEditableFaceOverlayDisplayPart(LayeredFacePartType partType)
        {
            return partType == LayeredFacePartType.Blush
                || partType == LayeredFacePartType.Tear
                || partType == LayeredFacePartType.Sweat
                || PawnFaceConfig.IsOverlayPart(partType);
        }

        private static string ResolveEditableFaceOverlayId(LayeredFacePartType displayPartType, string overlayId)
        {
            if (!string.IsNullOrWhiteSpace(overlayId))
            {
                return PawnFaceConfig.NormalizeOverlayId(overlayId);
            }

            switch (displayPartType)
            {
                case LayeredFacePartType.Blush:
                    return PawnFaceConfig.NormalizeOverlayId("Blush");
                case LayeredFacePartType.Tear:
                    return PawnFaceConfig.NormalizeOverlayId("Tear");
                case LayeredFacePartType.Sweat:
                    return PawnFaceConfig.NormalizeOverlayId("Sweat");
                case LayeredFacePartType.Overlay:
                    return string.Empty;
                default:
                    return string.Empty;
            }
        }

        private bool TrySyncEditableFaceLayerTextureToFaceConfig(PawnLayerConfig? layer)
        {
            if (!TryResolveEditableFaceLayerTarget(layer, out PawnFaceConfig? fc, out LayeredFacePartType partType, out string overlayId, out LayeredFacePartSide side))
                return false;

            PawnFaceConfig resolvedFaceConfig = fc!;
            string texPath = layer?.texPath ?? string.Empty;

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                if (PawnFaceConfig.IsOverlayPart(partType))
                {
                    LayeredFacePartConfig? existing = resolvedFaceConfig.GetLayeredPartConfig(partType, expression, overlayId);
                    if (existing != null)
                    {
                        resolvedFaceConfig.SetLayeredPart(
                            partType,
                            expression,
                            texPath,
                            overlayId,
                            resolvedFaceConfig.GetOverlayOrder(overlayId));
                    }
                }
                else
                {
                    LayeredFacePartConfig? existing = resolvedFaceConfig.GetLayeredPartConfig(partType, expression, side);
                    if (existing != null)
                    {
                        resolvedFaceConfig.SetLayeredPart(partType, expression, texPath, side);
                    }
                }
            }

            RebuildFaceImportBuffers(resolvedFaceConfig);
            return true;
        }

        private bool TryRemoveEditableFaceLayerFromFaceConfig(PawnLayerConfig? layer)
        {
            if (!TryResolveEditableFaceLayerTarget(layer, out PawnFaceConfig? fc, out LayeredFacePartType partType, out string overlayId, out LayeredFacePartSide side))
                return false;

            PawnFaceConfig resolvedFaceConfig = fc!;
            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                if (PawnFaceConfig.IsOverlayPart(partType))
                {
                    LayeredFacePartConfig? existing = resolvedFaceConfig.GetLayeredPartConfig(partType, expression, overlayId);
                    if (existing != null)
                    {
                        resolvedFaceConfig.RemoveLayeredPart(partType, expression, overlayId);
                    }
                }
                else
                {
                    LayeredFacePartConfig? existing = resolvedFaceConfig.GetLayeredPartConfig(partType, expression, side);
                    if (existing != null)
                    {
                        resolvedFaceConfig.RemoveLayeredPart(partType, expression, side);
                    }
                }
            }

            RebuildFaceImportBuffers(resolvedFaceConfig);
            return true;
        }

        private bool TryResolveEditableFaceLayerTarget(
            PawnLayerConfig? layer,
            out PawnFaceConfig? fc,
            out LayeredFacePartType partType,
            out string overlayId,
            out LayeredFacePartSide side)
        {
            fc = workingSkin?.faceConfig;
            partType = LayeredFacePartType.Base;
            overlayId = string.Empty;
            side = LayeredFacePartSide.None;

            if (fc == null || !IsEditableFaceLayer(layer))
                return false;

            string layerName = layer!.layerName ?? string.Empty;
            string nameBody = layerName.Substring("[Face] ".Length);
            if (string.IsNullOrWhiteSpace(nameBody))
                return false;

            int bracketIndex = nameBody.IndexOf('[');
            string partToken = (bracketIndex >= 0
                ? nameBody.Substring(0, bracketIndex)
                : nameBody).Trim();

            if (!Enum.TryParse(partToken, true, out LayeredFacePartType parsedDisplayPartType))
                return false;

            bool isOverlayStyle = PawnFaceConfig.IsOverlayPart(parsedDisplayPartType);
            partType = parsedDisplayPartType;

            int scanIndex = bracketIndex;
            while (scanIndex >= 0 && scanIndex < nameBody.Length)
            {
                int closeIndex = nameBody.IndexOf(']', scanIndex + 1);
                if (closeIndex <= scanIndex)
                    break;

                string token = nameBody.Substring(scanIndex + 1, closeIndex - scanIndex - 1);
                if (!isOverlayStyle
                    && Enum.TryParse(token, true, out LayeredFacePartSide parsedSide))
                {
                    side = PawnFaceConfig.NormalizePartSide(partType, parsedSide);
                }
                else if (isOverlayStyle && !string.IsNullOrWhiteSpace(token))
                {
                    overlayId = PawnFaceConfig.NormalizeOverlayId(token);
                }

                scanIndex = nameBody.IndexOf('[', closeIndex + 1);
            }

            if (isOverlayStyle)
            {
                overlayId = ResolveEditableFaceOverlayId(parsedDisplayPartType, overlayId);
                side = LayeredFacePartSide.None;
            }

            return true;
        }

        private PawnLayerConfig BuildSyncedLayeredFaceEditableLayer(
            PawnLayerConfig? existingLayer,
            LayeredFacePartType displayPartType,
            string normalizedOverlayId,
            string texturePath,
            int overlayOrder,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            PawnLayerConfig layer = existingLayer?.Clone() ?? new PawnLayerConfig
            {
                anchorTag = "Head",
                anchorPath = string.Empty,
                offset = Vector3.zero,
                offsetEast = Vector3.zero,
                offsetNorth = Vector3.zero,
                scale = Vector2.one,
                rotation = 0f,
                drawOrder = GetLayeredFaceDefaultDrawOrder(displayPartType, overlayOrder, normalizedOverlayId),
                workerClass = typeof(CharacterStudio.Rendering.PawnRenderNodeWorker_CustomLayer),
                shaderDefName = "Cutout",
                colorSource = LayerColorSource.White,
                customColor = Color.white,
                colorTwoSource = LayerColorSource.White,
                customColorTwo = Color.white,
                visible = true
            };

            layer.layerName = GetLayeredFaceEditableLayerName(displayPartType, normalizedOverlayId, side);
            layer.texPath = texturePath;
            layer.workerClass = typeof(CharacterStudio.Rendering.PawnRenderNodeWorker_CustomLayer);
            layer.role = GetLayeredFaceDefaultRole(displayPartType);
            layer.variantLogic = GetLayeredFaceDefaultVariantLogic(displayPartType);
            layer.useDirectionalSuffix = true;
            layer.useExpressionSuffix = false;
            layer.useEyeDirectionSuffix = false;
            layer.useBlinkSuffix = false;
            layer.useFrameSequence = false;
            layer.hideWhenMissingVariant = false;

            if (existingLayer == null)
            {
                layer.anchorTag = "Head";
                layer.anchorPath = string.Empty;
                layer.drawOrder = GetLayeredFaceDefaultDrawOrder(displayPartType, overlayOrder, normalizedOverlayId);
            }
            else if (ShouldMigrateLegacyOverlayDrawOrder(displayPartType, layer.drawOrder, overlayOrder, normalizedOverlayId))
            {
                layer.drawOrder = GetLayeredFaceDefaultDrawOrder(displayPartType, overlayOrder, normalizedOverlayId);
            }

            if (displayPartType == LayeredFacePartType.Hair
                && normalizedOverlayId.Equals("back", StringComparison.OrdinalIgnoreCase))
            {
                layer.drawOrder = GetLayeredFaceDefaultDrawOrder(displayPartType, overlayOrder, normalizedOverlayId);
            }

            return layer;
        }

        private static string GetLayeredFaceEditableLayerName(
            LayeredFacePartType displayPartType,
            string normalizedOverlayId,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            bool isOverlayStyle = PawnFaceConfig.IsOverlayPart(displayPartType);
            string overlayLabel = isOverlayStyle && !string.IsNullOrWhiteSpace(normalizedOverlayId)
                ? $" [{normalizedOverlayId}]"
                : string.Empty;
            LayeredFacePartSide normalizedSide = PawnFaceConfig.NormalizePartSide(displayPartType, side);
            string sideLabel = normalizedSide == LayeredFacePartSide.None
                ? string.Empty
                : $" [{normalizedSide}]";
            return $"[Face] {displayPartType}{overlayLabel}{sideLabel}";
        }

        private static LayerVariantLogic GetLayeredFaceDefaultVariantLogic(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Brow:
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                case LayeredFacePartType.ReplacementEye:
                case LayeredFacePartType.Mouth:
                case LayeredFacePartType.Blush:
                case LayeredFacePartType.Sweat:
                case LayeredFacePartType.Tear:
                case LayeredFacePartType.Overlay:
                case LayeredFacePartType.OverlayTop:
                    return LayerVariantLogic.ChannelState;
                case LayeredFacePartType.Pupil:
                    return LayerVariantLogic.EyeDirectionOnly;
                case LayeredFacePartType.Hair:
                    return LayerVariantLogic.ChannelState;
                default:
                    return LayerVariantLogic.None;
            }
        }

        private static LayerRole GetLayeredFaceDefaultRole(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Base:
                    return LayerRole.Head;
                case LayeredFacePartType.Brow:
                    return LayerRole.Brow;
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                case LayeredFacePartType.ReplacementEye:
                    return LayerRole.Lid;
                case LayeredFacePartType.Pupil:
                    return LayerRole.Eye;
                case LayeredFacePartType.Mouth:
                    return LayerRole.Mouth;
                case LayeredFacePartType.Hair:
                    return LayerRole.Decoration;
                case LayeredFacePartType.Blush:
                case LayeredFacePartType.Sweat:
                case LayeredFacePartType.Tear:
                case LayeredFacePartType.Overlay:
                case LayeredFacePartType.OverlayTop:
                    return LayerRole.Emotion;
                default:
                    return LayerRole.Decoration;
            }
        }

        private static float GetLayeredFaceDefaultDrawOrder(LayeredFacePartType partType, int overlayOrder = 0, string overlayId = "")
        {
            switch (partType)
            {
                case LayeredFacePartType.Base:
                    return 50.05f;
                case LayeredFacePartType.Hair:
                {
                    if (overlayId.Contains("back")) return 50.31f;
                    return 50.15f;
                }
                case LayeredFacePartType.Eye:
                    return 50.12f;
                case LayeredFacePartType.Pupil:
                    return 50.14f;
                case LayeredFacePartType.UpperLid:
                    return 50.145f;
                case LayeredFacePartType.LowerLid:
                    return 50.147f;
                case LayeredFacePartType.ReplacementEye:
                    return 50.149f;
                case LayeredFacePartType.Brow:
                    return 50.16f;
                case LayeredFacePartType.Mouth:
                    return 50.18f;
                case LayeredFacePartType.Blush:
                    return 50.142f;
                case LayeredFacePartType.Tear:
                    return 50.144f;
                case LayeredFacePartType.Sweat:
                    return 50.146f;
                case LayeredFacePartType.Overlay:
                    switch (PawnFaceConfig.GetOverlayKind(overlayId))
                    {
                        case LayeredOverlayKind.Blush:
                            return 50.142f;
                        case LayeredOverlayKind.Tear:
                            return 50.144f;
                        case LayeredOverlayKind.Sweat:
                            return 50.146f;
                        case LayeredOverlayKind.Sleep:
                            return 50.148f;
                        default:
                            return 50.149f + Math.Min(4, Math.Max(0, overlayOrder - 4)) * 0.0002f;
                    }
                default:
                    return 50.20f;
            }
        }

        private static bool HasImportedLayeredEntry(
            PawnFaceConfig fc,
            LayeredFacePartType partType,
            ExpressionType expression,
            string? overlayId,
            LayeredFacePartSide side,
            Rot4 facing)
        {
            if (PawnFaceConfig.IsOverlayPart(partType))
            {
                LayeredFacePartConfig? existingOverlay = fc.GetLayeredPartConfig(partType, expression, PawnFaceConfig.NormalizeOverlayId(overlayId));
                return existingOverlay != null && existingOverlay.enabled && existingOverlay.HasAnyTexture();
            }

            LayeredFacePartSide normalizedSide = PawnFaceConfig.NormalizePartSide(partType, side);
            LayeredFacePartConfig? existing = fc.GetLayeredPartConfig(partType, expression, normalizedSide);
            return existing != null && existing.enabled && existing.HasAnyTexture();
        }

        private static float GetLegacyLayeredFaceDefaultDrawOrder(LayeredFacePartType partType, int overlayOrder = 0, string overlayId = "")
        {
            switch (partType)
            {
                case LayeredFacePartType.Blush:
                    return 50.22f;
                case LayeredFacePartType.Tear:
                    return 50.24f;
                case LayeredFacePartType.Sweat:
                    return 50.26f;
                case LayeredFacePartType.Overlay:
                    switch (PawnFaceConfig.GetOverlayKind(overlayId))
                    {
                        case LayeredOverlayKind.Blush:
                            return 50.22f;
                        case LayeredOverlayKind.Tear:
                            return 50.24f;
                        case LayeredOverlayKind.Sweat:
                            return 50.26f;
                        case LayeredOverlayKind.Sleep:
                            return 50.28f;
                        default:
                            return 50.30f + Math.Max(0, overlayOrder - 4) * 0.002f;
                    }
                default:
                    return GetLayeredFaceDefaultDrawOrder(partType, overlayOrder, overlayId);
            }
        }

        private static float GetLegacyRuntimeLayeredFaceDrawOrder(LayeredFacePartType partType, int overlayOrder = 0, string overlayId = "")
        {
            switch (partType)
            {
                case LayeredFacePartType.Blush:
                    return 0.146f;
                case LayeredFacePartType.Tear:
                    return 0.148f;
                case LayeredFacePartType.Sweat:
                    return 0.150f;
                case LayeredFacePartType.Overlay:
                    switch (PawnFaceConfig.GetOverlayKind(overlayId))
                    {
                        case LayeredOverlayKind.Blush:
                            return 0.146f;
                        case LayeredOverlayKind.Tear:
                            return 0.148f;
                        case LayeredOverlayKind.Sweat:
                            return 0.150f;
                        case LayeredOverlayKind.Sleep:
                            return 0.152f;
                        default:
                            return 0.154f + Math.Max(0, overlayOrder - 4) * 0.0002f;
                    }
                default:
                    return 0f;
            }
        }

        private static bool IsOverlayRenderableDisplayPart(LayeredFacePartType partType)
        {
            return partType == LayeredFacePartType.Blush
                || partType == LayeredFacePartType.Tear
                || partType == LayeredFacePartType.Sweat
                || partType == LayeredFacePartType.Overlay;
        }

        private static bool ShouldMigrateLegacyOverlayDrawOrder(LayeredFacePartType partType, float drawOrder, int overlayOrder, string overlayId)
        {
            if (!IsOverlayRenderableDisplayPart(partType))
                return false;

            float legacyEditorDrawOrder = GetLegacyLayeredFaceDefaultDrawOrder(partType, overlayOrder, overlayId);
            if (Mathf.Abs(drawOrder - legacyEditorDrawOrder) <= 0.0001f)
                return true;

            float legacyRuntimeDrawOrder = GetLegacyRuntimeLayeredFaceDrawOrder(partType, overlayOrder, overlayId);
            return Mathf.Abs(drawOrder - legacyRuntimeDrawOrder) <= 0.0001f;
        }

        private void AutoConfigureProgrammaticLayeredFaceLogic(
            PawnFaceConfig fc,
            HashSet<string> detectedFileNames,
            Dictionary<string, string> detectedFilePathMap)
        {
            bool hasPupilBase = !string.IsNullOrWhiteSpace(fc.GetAnyLayeredPartPath(LayeredFacePartType.Pupil));
            if (!hasPupilBase)
            {
                return;
            }

            fc.eyeDirectionConfig ??= new PawnEyeDirectionConfig();
            fc.eyeDirectionConfig.enabled = true;
            fc.eyeDirectionConfig.texCenter = string.Empty;
            fc.eyeDirectionConfig.texLeft = string.Empty;
            fc.eyeDirectionConfig.texRight = string.Empty;
            fc.eyeDirectionConfig.texUp = string.Empty;
            fc.eyeDirectionConfig.texDown = string.Empty;
        }

        private static void NormalizeImportedPairedLayeredFaceParts(PawnFaceConfig fc)
        {
            if (fc?.layeredParts == null || fc.layeredParts.Count == 0)
                return;

            LayeredFacePartType[] pairedTypes =
            {
                LayeredFacePartType.Brow,
                LayeredFacePartType.Eye,
                LayeredFacePartType.Pupil,
                LayeredFacePartType.UpperLid,
                LayeredFacePartType.LowerLid,
            };

            foreach (LayeredFacePartType partType in pairedTypes)
            {
                foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
                {
                    bool hasExplicitSided = fc.layeredParts.Any(part =>
                        part != null
                        && part.partType == partType
                        && part.expression == expression
                        && part.enabled
                        && PawnFaceConfig.NormalizePartSide(partType, part.side) != LayeredFacePartSide.None
                        && part.HasAnyTexture());

                    if (!hasExplicitSided)
                        continue;

                    fc.RemoveLayeredPart(partType, expression, LayeredFacePartSide.None);
                }
            }
        }

        private static void InheritUnsidedDirectionalPathsForPairedParts(PawnFaceConfig fc)
        {
            if (fc?.layeredParts == null || fc.layeredParts.Count == 0)
                return;

            LayeredFacePartType[] pairedTypes =
            {
                LayeredFacePartType.Brow,
                LayeredFacePartType.Eye,
                LayeredFacePartType.Pupil,
                LayeredFacePartType.UpperLid,
                LayeredFacePartType.LowerLid,
            };

            foreach (LayeredFacePartType partType in pairedTypes)
            {
                foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
                {
                    LayeredFacePartConfig? unsided = fc.GetLayeredPartConfig(partType, expression, LayeredFacePartSide.None);
                    if (unsided == null)
                        continue;

                    bool hasExplicitSided = false;
                    foreach (LayeredFacePartSide side in new[] { LayeredFacePartSide.Left, LayeredFacePartSide.Right })
                    {
                        LayeredFacePartConfig? sided = fc.GetLayeredPartConfig(partType, expression, side);
                        if (sided == null)
                            continue;

                        hasExplicitSided = true;

                        string resolvedSouth = !string.IsNullOrWhiteSpace(sided.texPathSouth)
                            ? sided.texPathSouth
                            : sided.texPath;
                        string resolvedEast = !string.IsNullOrWhiteSpace(sided.texPathEast)
                            ? sided.texPathEast
                            : unsided.texPathEast;
                        string resolvedNorth = !string.IsNullOrWhiteSpace(sided.texPathNorth)
                            ? sided.texPathNorth
                            : unsided.texPathNorth;

                        fc.SetLayeredPartDirectional(partType, expression, resolvedSouth, resolvedEast, resolvedNorth, side);
                    }

                    if (!hasExplicitSided)
                        continue;
                }
            }
        }

        private bool IsSupportedLayeredFaceTextureFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tga", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".psd", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryParseLayeredFaceFileName(
            string filePath,
            out LayeredFacePartType partType,
            out LayeredFacePartSide side,
            out string? overlayId,
            out string? suffix,
            out Rot4 facing)
        {
            partType = LayeredFacePartType.Base;
            side = LayeredFacePartSide.None;
            overlayId = null;
            suffix = null;
            facing = Rot4.South;

            string fileName = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string[] segments = fileName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            List<string> tailSegments = segments.Skip(1).ToList();

            if (segments[0].Equals("Overlay", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("OverlayTop", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("Hair", StringComparison.OrdinalIgnoreCase))
            {
                partType = segments[0].Equals("Hair", StringComparison.OrdinalIgnoreCase)
                    ? LayeredFacePartType.Hair
                    : segments[0].Equals("OverlayTop", StringComparison.OrdinalIgnoreCase)
                        ? LayeredFacePartType.OverlayTop
                        : LayeredFacePartType.Overlay;

                while (tailSegments.Count > 0 && DirectionalVariantTokens.Contains(tailSegments[tailSegments.Count - 1]))
                {
                    TryParseViewFacingToken(tailSegments[tailSegments.Count - 1], ref facing);
                    tailSegments.RemoveAt(tailSegments.Count - 1);
                }

                if (segments.Length > 1 && tailSegments.Count == 0)
                {
                    return false;
                }

                if (tailSegments.Count == 0)
                {
                    return false;
                }

                if (tailSegments.Count == 1)
                {
                    if (Enum.TryParse<ExpressionType>(tailSegments[0], true, out _))
                    {
                        return false;
                    }

                    overlayId = PawnFaceConfig.NormalizeOverlayId(tailSegments[0]);
                    return !string.IsNullOrWhiteSpace(overlayId);
                }

                string lastSegment = tailSegments[tailSegments.Count - 1];
                if (TryParseExpressionToken(lastSegment, out ExpressionType parsedExpression))
                {
                    overlayId = PawnFaceConfig.NormalizeOverlayId(string.Join("_", tailSegments.Take(tailSegments.Count - 1)));
                    suffix = parsedExpression.ToString();
                }
                else
                {
                    overlayId = PawnFaceConfig.NormalizeOverlayId(string.Join("_", tailSegments));
                }

                return !string.IsNullOrWhiteSpace(overlayId);
            }

            if (segments[0].Equals(nameof(LayeredFacePartType.Blush), StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals(nameof(LayeredFacePartType.Tear), StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals(nameof(LayeredFacePartType.Sweat), StringComparison.OrdinalIgnoreCase))
            {
                while (tailSegments.Count > 0 && DirectionalVariantTokens.Contains(tailSegments[tailSegments.Count - 1]))
                {
                    TryParseViewFacingToken(tailSegments[tailSegments.Count - 1], ref facing);
                    tailSegments.RemoveAt(tailSegments.Count - 1);
                }

                if (segments.Length > 1 && tailSegments.Count == 0)
                    return false;

                partType = LayeredFacePartType.Overlay;
                overlayId = PawnFaceConfig.NormalizeOverlayId(segments[0]);
                suffix = tailSegments.Count == 0 ? null : string.Join("_", tailSegments);
                if (!string.IsNullOrWhiteSpace(suffix) && TryParseExpressionToken(suffix, out ExpressionType overlayExpression))
                    suffix = overlayExpression.ToString();

                return true;
            }

            if (!LayeredFacePartFileAliases.TryGetValue(segments[0], out partType))
            {
                return false;
            }

            string? parsedExpressionSuffix = null;
            if (tailSegments.Count > 0 && TryParseExpressionToken(tailSegments[tailSegments.Count - 1], out ExpressionType parsedPartExpression))
            {
                parsedExpressionSuffix = parsedPartExpression.ToString();
                tailSegments.RemoveAt(tailSegments.Count - 1);
            }

            bool removedViewDirectionalTokens = false;
            while (tailSegments.Count > 0 && ViewDirectionalVariantTokens.Contains(tailSegments[tailSegments.Count - 1]))
            {
                TryParseViewFacingToken(tailSegments[tailSegments.Count - 1], ref facing);
                tailSegments.RemoveAt(tailSegments.Count - 1);
                removedViewDirectionalTokens = true;
            }

            if (tailSegments.Count > 0
                && TryParseLayeredFacePartSideToken(partType, tailSegments[tailSegments.Count - 1], out LayeredFacePartSide trailingSide))
            {
                side = trailingSide;
                tailSegments.RemoveAt(tailSegments.Count - 1);
            }
            else
            {
                for (int i = tailSegments.Count - 1; i >= 0; i--)
                {
                    if (TryParseLayeredFacePartSideToken(partType, tailSegments[i], out LayeredFacePartSide parsedSide))
                    {
                        side = parsedSide;
                        tailSegments.RemoveAt(i);
                        break;
                    }
                }
            }

            if (partType == LayeredFacePartType.Eye && !string.IsNullOrWhiteSpace(parsedExpressionSuffix))
            {
                partType = LayeredFacePartType.ReplacementEye;
            }

            if (partType == LayeredFacePartType.Mouth && !string.IsNullOrWhiteSpace(parsedExpressionSuffix))
            {
                partType = LayeredFacePartType.ReplacementMouth;
            }

            if (tailSegments.Count == 0)
            {
                suffix = parsedExpressionSuffix;
                return removedViewDirectionalTokens
                    || side != LayeredFacePartSide.None
                    || !string.IsNullOrWhiteSpace(parsedExpressionSuffix)
                    || segments.Length == 1;
            }

            if (!string.IsNullOrWhiteSpace(parsedExpressionSuffix)
                && tailSegments.All(segment => ViewDirectionalVariantTokens.Contains(segment)))
            {
                suffix = parsedExpressionSuffix;
                return true;
            }

            suffix = string.Join("_", tailSegments);
            return !string.IsNullOrWhiteSpace(suffix);
        }

        private static bool IsPairedLayeredFacePart(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Brow:
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.Pupil:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                case LayeredFacePartType.ReplacementEye:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseLayeredFacePartSideToken(
            LayeredFacePartType partType,
            string token,
            out LayeredFacePartSide side)
        {
            side = LayeredFacePartSide.None;

            if (!IsPairedLayeredFacePart(partType) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (token.Equals("Left", StringComparison.OrdinalIgnoreCase))
            {
                side = LayeredFacePartSide.Left;
                return true;
            }

            if (token.Equals("Right", StringComparison.OrdinalIgnoreCase))
            {
                side = LayeredFacePartSide.Right;
                return true;
            }

            return false;
        }

        private static void ResolveAutoImportDirectionalPaths(
            Dictionary<string, string> detectedFilePathMap,
            string texturePath,
            string sourceStem,
            Rot4 facing,
            out string texPathSouth,
            out string texPathEast,
            out string texPathNorth,
            out List<string> recognizedStems)
        {
            texPathSouth = string.Empty;
            texPathEast = string.Empty;
            texPathNorth = string.Empty;
            recognizedStems = new List<string>();

            if (sourceStem.StartsWith("Hair_", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(texturePath))
                {
                    string normalizedStem = sourceStem.Trim();
                    if (normalizedStem.EndsWith("_north", StringComparison.OrdinalIgnoreCase))
                    {
                        texPathNorth = texturePath;
                    }
                    else if (normalizedStem.EndsWith("_east", StringComparison.OrdinalIgnoreCase)
                        || normalizedStem.EndsWith("_west", StringComparison.OrdinalIgnoreCase)
                        || normalizedStem.EndsWith("_side", StringComparison.OrdinalIgnoreCase))
                    {
                        texPathEast = texturePath;
                    }
                    else
                    {
                        texPathSouth = texturePath;
                    }
                }

                if (!string.IsNullOrWhiteSpace(sourceStem))
                {
                    recognizedStems.Add(sourceStem);
                }

                return;
            }

            string directionalStem = StripViewDirectionalSuffix(sourceStem);
            bool sourceHasDirection = !directionalStem.Equals(sourceStem, StringComparison.OrdinalIgnoreCase);
            string baseStem = sourceHasDirection ? directionalStem : sourceStem;

            List<KeyValuePair<string, DirectionAssignment>> candidates = new List<KeyValuePair<string, DirectionAssignment>>
            {
                new KeyValuePair<string, DirectionAssignment>(baseStem, DirectionAssignment.South),
                new KeyValuePair<string, DirectionAssignment>(baseStem + "_south", DirectionAssignment.South),
                new KeyValuePair<string, DirectionAssignment>(baseStem + "_front", DirectionAssignment.South),
                new KeyValuePair<string, DirectionAssignment>(baseStem + "_back", DirectionAssignment.South),
                new KeyValuePair<string, DirectionAssignment>(baseStem + "_east", DirectionAssignment.East),
                new KeyValuePair<string, DirectionAssignment>(baseStem + "_west", DirectionAssignment.East),
                new KeyValuePair<string, DirectionAssignment>(baseStem + "_side", DirectionAssignment.East),
                new KeyValuePair<string, DirectionAssignment>(baseStem + "_north", DirectionAssignment.North)
            };

            foreach (KeyValuePair<string, DirectionAssignment> candidate in candidates)
            {
                string stem = candidate.Key;
                if (string.IsNullOrWhiteSpace(stem) || !detectedFilePathMap.TryGetValue(stem, out string resolvedPath))
                    continue;

                switch (candidate.Value)
                {
                    case DirectionAssignment.South:
                        texPathSouth = resolvedPath;
                        break;
                    case DirectionAssignment.East:
                        texPathEast = resolvedPath;
                        break;
                    case DirectionAssignment.North:
                        texPathNorth = resolvedPath;
                        break;
                }

                bool alreadyRecorded = false;
                foreach (string existing in recognizedStems)
                {
                    if (existing.Equals(stem, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyRecorded = true;
                        break;
                    }
                }

                if (!alreadyRecorded)
                    recognizedStems.Add(stem);
            }

            if (!string.IsNullOrWhiteSpace(texturePath))
            {
                if (facing == Rot4.North)
                {
                    if (string.IsNullOrWhiteSpace(texPathNorth))
                    {
                        texPathNorth = texturePath;
                    }
                }
                else if (facing == Rot4.East || facing == Rot4.West)
                {
                    if (string.IsNullOrWhiteSpace(texPathEast))
                    {
                        texPathEast = texturePath;
                    }
                }
                else if (string.IsNullOrWhiteSpace(texPathSouth))
                {
                    texPathSouth = texturePath;
                }
            }
        }

        private enum DirectionAssignment
        {
            South,
            East,
            North
        }

        private static string StripViewDirectionalSuffix(string stem)
        {
            if (string.IsNullOrWhiteSpace(stem))
            {
                return string.Empty;
            }

            string[] segments = stem.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> result = segments.ToList();
            while (result.Count > 1 && ViewDirectionalVariantTokens.Contains(result[result.Count - 1]))
            {
                result.RemoveAt(result.Count - 1);
            }

            return result.Count > 0 ? string.Join("_", result) : stem;
        }

        private static void TryParseViewFacingToken(string token, ref Rot4 facing)
        {
            if (token.Equals("north", StringComparison.OrdinalIgnoreCase))
            {
                facing = Rot4.North;
                return;
            }

            if (token.Equals("east", StringComparison.OrdinalIgnoreCase)
                || token.Equals("west", StringComparison.OrdinalIgnoreCase)
                || token.Equals("side", StringComparison.OrdinalIgnoreCase))
            {
                facing = Rot4.East;
                return;
            }

            if (token.Equals("south", StringComparison.OrdinalIgnoreCase)
                || token.Equals("back", StringComparison.OrdinalIgnoreCase)
                || token.Equals("front", StringComparison.OrdinalIgnoreCase))
            {
                facing = Rot4.South;
                return;
            }

            if (token.Equals("south", StringComparison.OrdinalIgnoreCase))
            {
                facing = Rot4.South;
            }
        }

        private bool LooksLikeLayeredFaceDirectory(IEnumerable<string> files)
        {
            foreach (string filePath in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
                if (LooksLikeLayeredVariantFileName(fileName))
                {
                    return true;
                }
            }

            return false;
        }

        private bool LooksLikeLayeredVariantFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string[] segments = fileName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            string firstSegment = segments[0];
            if (firstSegment.Equals("Overlay", StringComparison.OrdinalIgnoreCase)
                || firstSegment.Equals("OverlayTop", StringComparison.OrdinalIgnoreCase)
                || firstSegment.Equals("Eye", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private string BuildVirtualVariantBasePath(string sampleFilePath, string virtualStem)
        {
            string directory = Path.GetDirectoryName(sampleFilePath) ?? string.Empty;
            string extension = Path.GetExtension(sampleFilePath);
            string sampleStem = Path.GetFileNameWithoutExtension(sampleFilePath) ?? string.Empty;

            string[] segments = sampleStem.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> stemSegments = segments.ToList();
            while (stemSegments.Count > 1
                && DirectionalVariantTokens.Contains(stemSegments[stemSegments.Count - 1]))
            {
                stemSegments.RemoveAt(stemSegments.Count - 1);
            }

            string resolvedStem = stemSegments.Count > 0
                ? string.Join("_", stemSegments)
                : virtualStem;

            if (string.IsNullOrWhiteSpace(resolvedStem))
            {
                resolvedStem = virtualStem;
            }

            return Path.Combine(directory, resolvedStem + extension);
        }

        private string FormatLayeredAutoImportMatchLabel(
            LayeredFacePartType partType,
            ExpressionType expression,
            string? overlayId = null,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            string partLabel = GetLayeredFacePartTypeLabel(partType);
            if (PawnFaceConfig.IsOverlayPart(partType))
            {
                string resolvedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
                return expression == ExpressionType.Neutral
                    ? $"{partLabel} [{resolvedOverlayId}]"
                    : $"{partLabel} [{resolvedOverlayId}] / {GetExpressionTypeLabel(expression)}";
            }

            LayeredFacePartSide normalizedSide = PawnFaceConfig.NormalizePartSide(partType, side);
            string sideLabel = normalizedSide == LayeredFacePartSide.None
                ? string.Empty
                : $" [{normalizedSide}]";

            return expression == ExpressionType.Neutral
                ? $"{partLabel}{sideLabel}"
                : $"{partLabel}{sideLabel} / {GetExpressionTypeLabel(expression)}";
        }

        private void ShowLayeredFaceAutoImportSummary(
            string directoryPath,
            PawnFaceConfig fc,
            List<string> matchedLines,
            List<string> synthesizedLines,
            List<string> runtimeVariantFiles,
            List<string> ignoredFiles,
            bool switchedFromFullFaceMode)
        {
            int assignedEntries = fc.layeredParts?.Count(p =>
                p != null
                && p.enabled
                && !string.IsNullOrWhiteSpace(p.texPath)) ?? 0;

            bool hasBaseSlot = workingSkin?.baseAppearance?.GetSlot(BaseAppearanceSlotType.Head)?.enabled == true
                && !string.IsNullOrWhiteSpace(workingSkin.baseAppearance.GetSlot(BaseAppearanceSlotType.Head)?.texPath);

            List<LayeredFacePartType> configuredPartTypes = Enum.GetValues(typeof(LayeredFacePartType))
                .Cast<LayeredFacePartType>()
                .Where(partType => partType != LayeredFacePartType.Base && fc.CountLayeredParts(partType) > 0)
                .ToList();

            List<string> missingPartLabels = Enum.GetValues(typeof(LayeredFacePartType))
                .Cast<LayeredFacePartType>()
                .Where(partType => partType != LayeredFacePartType.Base && fc.CountLayeredParts(partType) <= 0)
                .Select(GetLayeredFacePartTypeLabel)
                .ToList();

            List<string> configuredParts = configuredPartTypes
                .Select(partType => $"• {GetLayeredFacePartTypeLabel(partType)}: {fc.CountLayeredParts(partType)}")
                .ToList();

            List<string> baseSlotLines = new List<string>();
            if (hasBaseSlot)
            {
                string? basePath = workingSkin?.baseAppearance?.GetSlot(BaseAppearanceSlotType.Head)?.texPath;
                baseSlotLines.Add($"• 基础槽位 / Head ← {Path.GetFileName(basePath ?? string.Empty)}");
            }

            string summary =
                (switchedFromFullFaceMode
                    ? "检测到该目录符合分层面部命名规则，已自动切换为 LayeredDynamic 工作流。\n\n"
                    : "分层面部自动识别完成。\n\n")
                + $"目录：{directoryPath}\n"
                + $"已配置条目：{assignedEntries}\n"
                + $"已覆盖部件：{configuredPartTypes.Count} / {Enum.GetValues(typeof(LayeredFacePartType)).Length - 1}\n"
                + "未配置部件：\n"
                + (missingPartLabels.Count > 0 ? string.Join("\n", missingPartLabels.Select(label => $"• {label}")) : "（无）")
                + "\n\n基础槽位：\n"
                + (baseSlotLines.Count > 0 ? string.Join("\n", baseSlotLines) : "（未导入）")
                + "\n\n"
                + "部件概览：\n"
                + (configuredParts.Count > 0 ? string.Join("\n", configuredParts) : "（无）")
                + "\n\n已写入配置：\n"
                + (matchedLines.Count > 0 ? string.Join("\n", matchedLines) : "（无）")
                + "\n\n未直接写入的派生文件：\n"
                + (runtimeVariantFiles.Count > 0 ? string.Join("\n", runtimeVariantFiles) : "（无）")
                + "\n\n未识别文件：\n"
                + (ignoredFiles.Count > 0 ? string.Join("\n", ignoredFiles) : "（无）")
                + "\n\n说明：paired 部件的 left / right 会作为左右独立层导入；_east / _west 会写入侧视资源，_north 会写入后视资源；Base 会导入至基础槽位；仍未直接映射的命名才会保留在派生文件列表中。"
                + "\n分层面部条目会同步写入图层编辑器中的 [Face] 图层，左右独立部件会拆分为独立图层，后续可直接调整层级、位置与显示。";

            Find.WindowStack.Add(new Dialog_CopyableText("分层面部自动导入摘要", summary));
        }
    }
}
