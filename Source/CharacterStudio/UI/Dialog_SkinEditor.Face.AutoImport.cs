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
                { "Sclera", LayeredFacePartType.Eye },
                { "Pupil", LayeredFacePartType.Pupil },
                { "UpperLid", LayeredFacePartType.UpperLid },
                { "LidUpper", LayeredFacePartType.UpperLid },
                { "LowerLid", LayeredFacePartType.LowerLid },
                { "LidLower", LayeredFacePartType.LowerLid },
                { "Mouth", LayeredFacePartType.Mouth },
                { "Blush", LayeredFacePartType.Overlay },
                { "Tear", LayeredFacePartType.Overlay },
                { "Sweat", LayeredFacePartType.Overlay },
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
                    isDirty = true;
                    RefreshPreview();
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

                fc.enabled = true;
                fc.workflowMode = FaceWorkflowMode.FullFaceSwap;
                fc.expressions.Clear();
                fc.layeredParts?.Clear();
                fc.layeredSourceRoot = string.Empty;
                layeredPartPathBuffer.Clear();

                foreach (var pair in matchedExpressionPaths.OrderBy(pair => (int)pair.Key))
                {
                    fc.SetTexPath(pair.Key, pair.Value);
                }

                exprPathBuffer.Clear();
                foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
                {
                    exprPathBuffer[expression] = fc.GetTexPath(expression);
                }

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

        private void AutoPopulateLayeredFacePartsFromDirectory(PawnFaceConfig fc, string directoryPath, bool switchedFromFullFaceMode = false)
        {
            try
            {
                fc.layeredParts ??= new List<LayeredFacePartConfig>();
                fc.layeredParts.Clear();
                fc.expressions.Clear();
                layeredPartPathBuffer.Clear();
                exprPathBuffer.Clear();

                fc.enabled = true;
                fc.workflowMode = FaceWorkflowMode.LayeredDynamic;
                fc.layeredSourceRoot = directoryPath;

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
                    string normalizedOverlayId = string.IsNullOrWhiteSpace(overlayId) ? "Overlay" : overlayId!;
                    if (!overlayOrderById.TryGetValue(normalizedOverlayId, out int overlayOrder))
                    {
                        overlayOrder = overlayOrderById.Count;
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
                    string? overlayId = null)
                {
                    int overlayOrder = partType == LayeredFacePartType.Overlay ? GetOverlayOrder(overlayId) : 0;
                    ApplyAutoScannedLayeredPart(fc, partType, expression, texturePath, overlayId, overlayOrder);

                    recognizedFileNames.Add(sourceStem);
                    matchedLines.Add($"• {FormatLayeredAutoImportMatchLabel(partType, expression, overlayId)} ← {Path.GetFileName(texturePath)}");
                    return true;
                }

                bool TryApplyFirstAvailableStem(
                    LayeredFacePartType partType,
                    ExpressionType expression,
                    IEnumerable<string> stemCandidates,
                    string? overlayId = null)
                {
                    if (!TryGetFirstExistingPath(stemCandidates, out string resolvedPath, out string matchedStem))
                    {
                        return false;
                    }

                    return ApplyRecognized(partType, expression, resolvedPath, matchedStem, overlayId);
                }

                bool TryApplyExpressionGroup(
                    LayeredFacePartType partType,
                    IEnumerable<ExpressionType> expressions,
                    IEnumerable<string> stemCandidates,
                    string? overlayId = null)
                {
                    if (!TryGetFirstExistingPath(stemCandidates, out string resolvedPath, out string matchedStem))
                    {
                        return false;
                    }

                    bool applied = false;
                    foreach (ExpressionType expression in expressions)
                    {
                        ApplyRecognized(partType, expression, resolvedPath, matchedStem, overlayId);
                        applied = true;
                    }

                    return applied;
                }

                bool TryApplyVirtualNeutral(
                    LayeredFacePartType partType,
                    string virtualStem,
                    IEnumerable<string> sampleStemCandidates,
                    string? overlayId = null)
                {
                    string existingPath = partType == LayeredFacePartType.Overlay
                        ? fc.GetLayeredPartPath(partType, ExpressionType.Neutral, string.IsNullOrWhiteSpace(overlayId) ? "Overlay" : overlayId!)
                        : fc.GetLayeredPartPath(partType, ExpressionType.Neutral);

                    if (!string.IsNullOrWhiteSpace(existingPath))
                    {
                        return false;
                    }

                    if (!TryGetFirstExistingPath(sampleStemCandidates, out string samplePath, out string matchedStem))
                    {
                        return false;
                    }

                    int overlayOrder = partType == LayeredFacePartType.Overlay ? GetOverlayOrder(overlayId) : 0;
                    string virtualPath = BuildVirtualVariantBasePath(samplePath, virtualStem);

                    ApplyAutoScannedLayeredPart(fc, partType, ExpressionType.Neutral, virtualPath, overlayId, overlayOrder);
                    recognizedFileNames.Add(matchedStem);
                    synthesizedLines.Add($"• {FormatLayeredAutoImportMatchLabel(partType, ExpressionType.Neutral, overlayId)} ← {Path.GetFileName(samplePath)}（合成基础路径：{Path.GetFileName(virtualPath)}）");
                    return true;
                }

                TryApplyFirstAvailableStem(LayeredFacePartType.Base, ExpressionType.Neutral, new[] { "Base" });
                TryApplyFirstAvailableStem(LayeredFacePartType.Brow, ExpressionType.Neutral, new[] { "Brows", "Brow" });
                TryApplyFirstAvailableStem(LayeredFacePartType.Eye, ExpressionType.Neutral, new[] { "Sclera", "Eye" });
                TryApplyFirstAvailableStem(LayeredFacePartType.UpperLid, ExpressionType.Neutral, new[] { "LidUpper", "UpperLid" });
                TryApplyFirstAvailableStem(LayeredFacePartType.LowerLid, ExpressionType.Neutral, new[] { "LidLower", "LowerLid" });
                TryApplyFirstAvailableStem(LayeredFacePartType.Mouth, ExpressionType.Neutral, new[] { "Mouth" });

                bool hasConcretePupilBase = TryApplyFirstAvailableStem(LayeredFacePartType.Pupil, ExpressionType.Neutral, new[] { "Pupil" });
                if (!hasConcretePupilBase)
                {
                    hasConcretePupilBase = TryApplyVirtualNeutral(LayeredFacePartType.Pupil, "Pupil", new[] { "Pupil_Center" });
                }

                if (!hasConcretePupilBase)
                {
                    TryApplyFirstAvailableStem(
                        LayeredFacePartType.Pupil,
                        ExpressionType.Neutral,
                        new[]
                        {
                            "Pupil_Left",
                            "Pupil_Right",
                            "Pupil_Up",
                            "Pupil_Down",
                            "Pupil_east",
                        });
                }

                TryApplyFirstAvailableStem(LayeredFacePartType.Eye, ExpressionType.Blink, new[] { "Eye_blink" });

                bool hasDeadEye = TryApplyFirstAvailableStem(LayeredFacePartType.Eye, ExpressionType.Dead, new[] { "Eye_death", "Eye_dead" });
                TryApplyFirstAvailableStem(LayeredFacePartType.Eye, ExpressionType.Sleeping, new[] { "Eye_closed" });
                if (!hasDeadEye)
                {
                    TryApplyFirstAvailableStem(LayeredFacePartType.Eye, ExpressionType.Dead, new[] { "Eye_closed" });
                }

                TryApplyExpressionGroup(
                    LayeredFacePartType.Eye,
                    new[] { ExpressionType.Happy, ExpressionType.Cheerful, ExpressionType.Lovin },
                    new[] { "Eye_closed_happy" });

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

                TryApplyExpressionGroup(
                    LayeredFacePartType.Overlay,
                    new[] { ExpressionType.Lovin },
                    new[] { "Overlay_love", "Overlay_lovin" },
                    "Overlay");

                TryApplyExpressionGroup(
                    LayeredFacePartType.Overlay,
                    new[] { ExpressionType.Gloomy, ExpressionType.Sad, ExpressionType.Hopeless, ExpressionType.Pain },
                    new[] { "Overlay_black", "Overlay_Nolight", "Overlay_NoLight", "Overlay_dark" },
                    "Overlay");

                TryApplyFirstAvailableStem(LayeredFacePartType.Overlay, ExpressionType.Neutral, new[] { "Overlay" }, "Overlay");

                foreach (string filePath in files)
                {
                    string fileStem = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
                    if (recognizedFileNames.Contains(fileStem))
                    {
                        continue;
                    }

                    if (!TryParseLayeredFaceFileName(filePath, out LayeredFacePartType scannedPartType, out string? overlayId, out string? suffix))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(suffix))
                    {
                        ApplyRecognized(scannedPartType, ExpressionType.Neutral, filePath, fileStem, overlayId);
                        continue;
                    }

                    if (Enum.TryParse(suffix, true, out ExpressionType scannedExpression))
                    {
                        ApplyRecognized(scannedPartType, scannedExpression, filePath, fileStem, overlayId);
                    }
                }

                AutoConfigureProgrammaticLayeredFaceLogic(fc, detectedFileNames, detectedFilePathMap);

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

                ShowLayeredFaceAutoImportSummary(
                    directoryPath,
                    fc,
                    matchedLines,
                    synthesizedLines,
                    runtimeVariantFiles,
                    ignoredFiles,
                    switchedFromFullFaceMode);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 自动扫描分层表情目录失败: {ex.Message}");
                Find.WindowStack.Add(new Dialog_MessageBox($"自动扫描分层表情目录失败：\n{ex.Message}"));
            }
        }

        private void ApplyAutoScannedLayeredPart(
            PawnFaceConfig fc,
            LayeredFacePartType partType,
            ExpressionType expression,
            string texturePath,
            string? overlayId = null,
            int overlayOrder = 0)
        {
            if (partType == LayeredFacePartType.Overlay)
            {
                string resolvedOverlayId = string.IsNullOrWhiteSpace(overlayId) ? "Overlay" : overlayId!;
                fc.SetLayeredPart(partType, expression, texturePath, resolvedOverlayId, overlayOrder);

                LayeredFacePartConfig? overlayConfig = fc.GetLayeredPartConfig(partType, expression, resolvedOverlayId);
                if (overlayConfig != null)
                {
                    overlayConfig.enabled = true;
                    overlayConfig.overlayId = resolvedOverlayId;
                    overlayConfig.overlayOrder = overlayOrder;
                }

                layeredPartPathBuffer[GetLayeredPartBufferKey(partType, expression, resolvedOverlayId)] = texturePath;
                return;
            }

            fc.SetLayeredPart(partType, expression, texturePath);

            LayeredFacePartConfig? partConfig = fc.GetLayeredPartConfig(partType, expression);
            if (partConfig != null)
            {
                partConfig.enabled = true;
            }

            layeredPartPathBuffer[GetLayeredPartBufferKey(partType, expression)] = texturePath;
        }

        private void AutoConfigureProgrammaticLayeredFaceLogic(
            PawnFaceConfig fc,
            HashSet<string> detectedFileNames,
            Dictionary<string, string> detectedFilePathMap)
        {
            bool hasPupilBase = !string.IsNullOrWhiteSpace(fc.GetLayeredPartPath(LayeredFacePartType.Pupil, ExpressionType.Neutral));
            bool hasDirectionalPupilTextures =
                detectedFileNames.Contains("Pupil_Left")
                || detectedFileNames.Contains("Pupil_Right")
                || detectedFileNames.Contains("Pupil_Up")
                || detectedFileNames.Contains("Pupil_Down")
                || detectedFileNames.Contains("Pupil_Center");

            bool hasConcreteCenteredPupilTexture =
                detectedFileNames.Contains("Pupil")
                || detectedFileNames.Contains("Pupil_Center");

            if (hasDirectionalPupilTextures && hasConcreteCenteredPupilTexture)
            {
                fc.eyeDirectionConfig ??= new PawnEyeDirectionConfig();
                fc.eyeDirectionConfig.enabled = true;
                fc.eyeDirectionConfig.pupilMoveRange = 0f;

                if (detectedFilePathMap.TryGetValue("Pupil_Center", out string centerPath))
                {
                    fc.eyeDirectionConfig.texCenter = centerPath;
                }

                if (detectedFilePathMap.TryGetValue("Pupil_Left", out string leftPath))
                {
                    fc.eyeDirectionConfig.texLeft = leftPath;
                }

                if (detectedFilePathMap.TryGetValue("Pupil_Right", out string rightPath))
                {
                    fc.eyeDirectionConfig.texRight = rightPath;
                }

                if (detectedFilePathMap.TryGetValue("Pupil_Up", out string upPath))
                {
                    fc.eyeDirectionConfig.texUp = upPath;
                }

                if (detectedFilePathMap.TryGetValue("Pupil_Down", out string downPath))
                {
                    fc.eyeDirectionConfig.texDown = downPath;
                }

                if (string.IsNullOrWhiteSpace(fc.eyeDirectionConfig.texCenter)
                    && detectedFilePathMap.TryGetValue("Pupil", out string fallbackCenterPath))
                {
                    fc.eyeDirectionConfig.texCenter = fallbackCenterPath;
                }

                return;
            }

            if (hasPupilBase)
            {
                fc.eyeDirectionConfig ??= new PawnEyeDirectionConfig();
                fc.eyeDirectionConfig.enabled = true;
                fc.eyeDirectionConfig.texLeft = string.Empty;
                fc.eyeDirectionConfig.texRight = string.Empty;
                fc.eyeDirectionConfig.texUp = string.Empty;
                fc.eyeDirectionConfig.texDown = string.Empty;

                if (string.IsNullOrWhiteSpace(fc.eyeDirectionConfig.texCenter))
                {
                    string basePupilPath = fc.GetLayeredPartPath(LayeredFacePartType.Pupil, ExpressionType.Neutral);
                    if (!string.IsNullOrWhiteSpace(basePupilPath))
                    {
                        fc.eyeDirectionConfig.texCenter = basePupilPath;
                    }
                }

                if (fc.eyeDirectionConfig.pupilMoveRange <= 0f)
                {
                    fc.eyeDirectionConfig.pupilMoveRange = 0.035f;
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
            out string? overlayId,
            out string? suffix)
        {
            partType = LayeredFacePartType.Base;
            overlayId = null;
            suffix = null;

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
            while (tailSegments.Count > 0 && DirectionalVariantTokens.Contains(tailSegments[tailSegments.Count - 1]))
            {
                tailSegments.RemoveAt(tailSegments.Count - 1);
            }

            bool hadOnlyDirectionalTail = segments.Length > 1 && tailSegments.Count == 0;

            if (segments[0].Equals("Overlay", StringComparison.OrdinalIgnoreCase))
            {
                partType = LayeredFacePartType.Overlay;

                if (segments.Length == 1)
                {
                    overlayId = "Overlay";
                    return true;
                }

                if (hadOnlyDirectionalTail)
                {
                    return false;
                }

                if (tailSegments.Count == 0)
                {
                    overlayId = "Overlay";
                    return true;
                }

                if (tailSegments.Count == 1)
                {
                    if (Enum.TryParse(tailSegments[0], true, out ExpressionType overlayExpression))
                    {
                        overlayId = "Overlay";
                        suffix = overlayExpression.ToString();
                    }
                    else
                    {
                        overlayId = tailSegments[0];
                    }

                    return true;
                }

                string lastSegment = tailSegments[tailSegments.Count - 1];
                if (Enum.TryParse(lastSegment, true, out ExpressionType parsedExpression))
                {
                    overlayId = string.Join("_", tailSegments.Take(tailSegments.Count - 1));
                    suffix = parsedExpression.ToString();
                }
                else
                {
                    overlayId = string.Join("_", tailSegments);
                }

                if (string.IsNullOrWhiteSpace(overlayId))
                {
                    overlayId = "Overlay";
                }

                return true;
            }

            if (segments[0].Equals(nameof(LayeredFacePartType.Blush), StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals(nameof(LayeredFacePartType.Tear), StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals(nameof(LayeredFacePartType.Sweat), StringComparison.OrdinalIgnoreCase))
            {
                if (hadOnlyDirectionalTail)
                {
                    return false;
                }

                partType = LayeredFacePartType.Overlay;
                overlayId = segments[0];

                if (tailSegments.Count > 0)
                {
                    suffix = string.Join("_", tailSegments);
                }

                return true;
            }

            if (!LayeredFacePartFileAliases.TryGetValue(segments[0], out partType))
            {
                return false;
            }

            if (hadOnlyDirectionalTail)
            {
                return false;
            }

            if (tailSegments.Count > 0)
            {
                suffix = string.Join("_", tailSegments);
            }

            return true;
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
            if (firstSegment.Equals("Overlay", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!LayeredFacePartFileAliases.ContainsKey(firstSegment))
            {
                return false;
            }

            return !firstSegment.Equals("Base", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildVirtualVariantBasePath(string sampleFilePath, string virtualStem)
        {
            string directory = Path.GetDirectoryName(sampleFilePath) ?? string.Empty;
            string extension = Path.GetExtension(sampleFilePath);
            return Path.Combine(directory, virtualStem + extension);
        }

        private string FormatLayeredAutoImportMatchLabel(
            LayeredFacePartType partType,
            ExpressionType expression,
            string? overlayId = null)
        {
            string partLabel = GetLayeredFacePartTypeLabel(partType);
            if (partType == LayeredFacePartType.Overlay)
            {
                string resolvedOverlayId = string.IsNullOrWhiteSpace(overlayId) ? "Overlay" : overlayId!;
                return expression == ExpressionType.Neutral
                    ? $"{partLabel} [{resolvedOverlayId}]"
                    : $"{partLabel} [{resolvedOverlayId}] / {GetExpressionTypeLabel(expression)}";
            }

            return expression == ExpressionType.Neutral
                ? partLabel
                : $"{partLabel} / {GetExpressionTypeLabel(expression)}";
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

            List<string> configuredParts = Enum.GetValues(typeof(LayeredFacePartType))
                .Cast<LayeredFacePartType>()
                .Where(partType => fc.CountLayeredParts(partType) > 0)
                .Select(partType => $"• {GetLayeredFacePartTypeLabel(partType)}: {fc.CountLayeredParts(partType)}")
                .ToList();

            string summary =
                (switchedFromFullFaceMode
                    ? "检测到该目录符合分层面部命名规则，已自动切换为 LayeredDynamic 工作流。\n\n"
                    : "分层面部自动识别完成。\n\n")
                + $"目录：{directoryPath}\n"
                + $"已配置条目：{assignedEntries}\n"
                + $"已覆盖部件：{configuredParts.Count} / {Enum.GetValues(typeof(LayeredFacePartType)).Length}\n\n"
                + "部件概览：\n"
                + (configuredParts.Count > 0 ? string.Join("\n", configuredParts) : "（无）")
                + "\n\n已写入配置：\n"
                + (matchedLines.Count > 0 ? string.Join("\n", matchedLines) : "（无）")
                + "\n\n合成基础路径：\n"
                + (synthesizedLines.Count > 0 ? string.Join("\n", synthesizedLines) : "（无）")
                + "\n\n保留为运行时自动变体：\n"
                + (runtimeVariantFiles.Count > 0 ? string.Join("\n", runtimeVariantFiles) : "（无）")
                + "\n\n未识别文件：\n"
                + (ignoredFiles.Count > 0 ? string.Join("\n", ignoredFiles) : "（无）")
                + "\n\n说明：像 east / left / right 这类方向后缀，以及 mouth / eye / overlay 的状态变体，会在运行时继续按命名规则自动匹配，无需逐条手工配置。";

            Find.WindowStack.Add(new Dialog_MessageBox(summary));
        }
    }
}