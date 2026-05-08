using System;
using System.Collections.Generic;
using System.IO;
using CharacterStudio.Core;
using CharacterStudio.Design;
using CharacterStudio.Rendering;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 预览生命周期 / 状态消息
        // ─────────────────────────────────────────────

        private bool EnsureMannequinReady()
        {
            if (mannequin == null)
            {
                InitializeMannequin();
            }

            if (mannequin == null)
            {
                ShowStatus("CS_Studio_Err_MannequinFailed".Translate());
                return false;
            }

            return true;
        }

        private void SyncPreviewRace(ThingDef? previewRace, Pawn? sourcePawn)
        {
            if (previewRace == null || !EnsureMannequinReady())
            {
                return;
            }

            mannequin!.SetRace(previewRace);
            if (sourcePawn != null)
            {
                mannequin.CopyAppearanceFrom(sourcePawn);
                Log.Message($"[CharacterStudio] 已将人偶种族同步为 {previewRace.defName} 并复制外观");
            }
        }

        private ThingDef ResolvePreviewRaceForReset(ThingDef? preferredRace = null)
        {
            return preferredRace
                ?? targetPawn?.def
                ?? mannequin?.CurrentPawn?.def
                ?? DefDatabase<ThingDef>.GetNamed("Human");
        }

        private void ForceResetPreviewMannequin(ThingDef? preferredRace = null, Pawn? sourcePawn = null)
        {
            if (!EnsureMannequinReady())
            {
                return;
            }

            ThingDef previewRace = ResolvePreviewRaceForReset(preferredRace);
            mannequin!.ForceReset(previewRace);
            if (sourcePawn != null)
            {
                mannequin.CopyAppearanceFrom(sourcePawn);
                Log.Message($"[CharacterStudio] 已强制重置人偶并复制外观: {previewRace.defName}");
            }
        }

        private void InitializeMannequin()
        {
            try
            {
                mannequin = new MannequinManager();
                mannequin.Initialize();

                if (targetPawn != null)
                {
                    mannequin.SetRace(targetPawn.def);
                    mannequin.CopyAppearanceFrom(targetPawn);
                }

                RefreshPreview();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 初始化人偶失败: {ex}");
                mannequin = null;
                ShowStatus("CS_Studio_Err_MannequinFailed".Translate());
            }
        }

        private void CleanupMannequin()
        {
            mannequin?.Cleanup();
            mannequin = null;
        }

        internal MannequinManager? EditorMannequin => mannequin;

        internal bool IsPreviewEquipmentAnimationPlaying => previewEquipmentAnimationPlaying;

        internal bool EnsureMannequinReadyForRefresh()
        {
            return EnsureMannequinReady();
        }

        internal CharacterApplicationPlan BuildPreviewApplicationPlan()
        {
            return BuildApplicationPlan(null, true, "EditorPreview");
        }

        internal void ShowPreviewStatus(string message)
        {
            ShowStatus(message);
        }

        internal string GetPreviewEquipmentAnimationTriggerKey()
        {
            return GetSelectedEquipmentAnimationTriggerKey();
        }

        internal int GetPreviewEquipmentAnimationDurationTicks()
        {
            return GetSelectedEquipmentAnimationDurationTicks();
        }

        internal void SetPreviewEquipmentAnimationTriggerKey(string value)
        {
            previewEquipmentAnimationTriggerKey = value;
        }

        internal void ApplyPreviewOverridesToSkinComp(CompPawnSkin skinComp)
        {
            skinComp.SetPreviewExpressionOverride(previewExpressionOverrideEnabled ? previewExpression : null);
            skinComp.SetPreviewMouthState(previewMouthStateOverrideEnabled ? previewMouthState : null);
            skinComp.SetPreviewLidState(previewLidStateOverrideEnabled ? previewLidState : null);
            skinComp.SetPreviewBrowState(previewBrowStateOverrideEnabled ? previewBrowState : null);
            skinComp.SetPreviewEmotionOverlayState(previewEmotionStateOverrideEnabled ? previewEmotionState : null);
            skinComp.SetPreviewEyeDirection(previewEyeDirectionOverrideEnabled ? previewEyeDirection : null);
            skinComp.SetPreviewGazeOffset(previewGazeCursorEnabled ? previewGazeCursorOffset : (Vector2?)null);
            skinComp.EnsureFaceRuntimeStateReadyForPreview();
        }

        internal void ApplyPreviewEquipmentAnimationToSkinComp(CompPawnSkin skinComp, string triggerKey, int durationTicks)
        {
            ApplyPreviewEquipmentAnimationState(skinComp, triggerKey, durationTicks);
        }

        private void RefreshPreview()
        {
            SkinEditorPreviewRefresher.Refresh(this);
            CapturePreviewLoadedTextureInfo();
        }

        private void RequestThrottledFacePreviewRefresh(bool force = false)
        {
            float now = Time.realtimeSinceStartup;
            if (force || now >= nextFacePreviewRefreshRealtime)
            {
                pendingFacePreviewRefresh = false;
                nextFacePreviewRefreshRealtime = now + FacePreviewRefreshThrottleSeconds;
                RefreshPreview();
                return;
            }

            pendingFacePreviewRefresh = true;
        }

        private void FlushPendingThrottledFacePreviewRefresh(bool force = false)
        {
            if (!pendingFacePreviewRefresh)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (!force && now < nextFacePreviewRefreshRealtime)
            {
                return;
            }

            pendingFacePreviewRefresh = false;
            nextFacePreviewRefreshRealtime = now + FacePreviewRefreshThrottleSeconds;
            RefreshPreview();
        }

        private void ShowStatus(string message)
        {
            statusMessage = message;
            statusMessageTime = 3f;
        }

        /// <summary>
        /// 在预览刷新后收集当前 workingSkin 中所有载入的纹理路径，
        /// 记录到 <see cref="previewLoadedTexName"/> 并重置淡出计时器。
        /// 每个纹理名称后会标注是否包含半透明像素（α标记）。
        /// 同名文件会追加父目录以区分；装备的 maskTexPath 也会收集。
        /// </summary>
        private void CapturePreviewLoadedTextureInfo()
        {
            if (workingSkin == null) return;

            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new List<string>();

            // 从基础外观槽位收集
            var slots = workingSkin.baseAppearance?.EnabledSlots();
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    if (!string.IsNullOrWhiteSpace(slot.texPath) && seenPaths.Add(slot.texPath))
                    {
                        paths.Add(slot.texPath);
                    }
                }
            }

            // 从可见的自定义图层收集
            if (workingSkin.layers != null)
            {
                foreach (var layer in workingSkin.layers)
                {
                    if (layer.visible && !string.IsNullOrWhiteSpace(layer.texPath) && seenPaths.Add(layer.texPath))
                    {
                        paths.Add(layer.texPath);
                    }
                }
            }

            // 从装备收集（texPath + maskTexPath）
            if (WorkingEquipments != null)
            {
                foreach (var equipment in WorkingEquipments)
                {
                    if (equipment?.renderData == null) continue;
                    var rd = equipment.renderData;
                    if (!string.IsNullOrWhiteSpace(rd.texPath) && seenPaths.Add(rd.texPath))
                    {
                        paths.Add(rd.texPath);
                    }
                    if (!string.IsNullOrWhiteSpace(rd.maskTexPath) && seenPaths.Add(rd.maskTexPath))
                    {
                        paths.Add(rd.maskTexPath);
                    }
                }
            }

            // 从 Face 配置收集当前朝向的纹理
            if (workingSkin.faceConfig != null && currentTab == EditorTab.Face)
            {
                CollectFaceTexturesForCurrentRotation(workingSkin.faceConfig, seenPaths, paths);
            }

            if (paths.Count == 0) return;

            // 先生成所有显示名，再按显示名去重：同名时追加父目录区分
            var displayNames = new List<string>(paths.Count);
            foreach (string path in paths)
            {
                displayNames.Add(FormatTexDisplayName(path));
            }

            // 检测显示名重复，对重复项追加父目录
            var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in displayNames)
            {
                string key = name;
                if (nameCounts.ContainsKey(key))
                    nameCounts[key]++;
                else
                    nameCounts[key] = 1;
            }

            var duplicateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in nameCounts)
            {
                if (kv.Value > 1)
                    duplicateNames.Add(kv.Key);
            }

            var finalNames = new List<string>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                if (duplicateNames.Contains(displayNames[i]))
                {
                    // 追加父目录名以区分
                    string parentDir = GetParentDirectoryName(paths[i]);
                    string baseName = ExtractTexFileName(paths[i]);
                    string suffix = displayNames[i].Substring(baseName.Length); // 保留 α 标记等后缀
                    finalNames.Add(parentDir + "/" + baseName + suffix);
                }
                else
                {
                    finalNames.Add(displayNames[i]);
                }
            }

            previewLoadedTexName = string.Join("\n", finalNames);
            previewLoadedTexNameTime = 30f;
        }

        /// <summary>
        /// 格式化纹理显示名称：文件名 + 半透明像素标记。
        /// 如果纹理包含半透明像素（0 < alpha < 255），追加 " α" 标记。
        /// </summary>
        private static string FormatTexDisplayName(string texPath)
        {
            string displayName = ExtractTexFileName(texPath);

            try
            {
                Texture2D? tex = ContentFinder<Texture2D>.Get(texPath, false)
                    ?? RuntimeAssetLoader.LoadTextureRaw(texPath, true);
                if (tex != null && RuntimeAssetLoader.TextureHasSemiTransparentPixels(tex))
                {
                    displayName += " α";
                }
            }
            catch
            {
                // 纹理加载或检测失败时不追加标记
            }

            return displayName;
        }

        /// <summary>
        /// 获取路径的父目录名（仅一级），用于同名文件区分。
        /// 例如 "Skins/MySkin/Base.png" → "MySkin"
        /// </summary>
        private static string GetParentDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            int lastSep = path.LastIndexOfAny(new[] { '/', '\\' });
            if (lastSep <= 0) return string.Empty;
            int parentSep = path.LastIndexOfAny(new[] { '/', '\\' }, lastSep - 1);
            return parentSep >= 0 && parentSep < lastSep - 1
                ? path.Substring(parentSep + 1, lastSep - parentSep - 1)
                : path.Substring(0, lastSep);
        }

        /// <summary>
        /// 从纹理完整路径中提取文件名部分用于显示。
        /// 与外部代码一致，使用 <see cref="Path.GetFileName"/>。
        /// </summary>
        private static string ExtractTexFileName(string path)
        {
            return Path.GetFileName(path) ?? path;
        }

        /// <summary>
        /// 根据当前预览朝向收集 Face 配置中的纹理路径。
        /// 对于每个启用的部件，根据 previewRotation 选择对应的纹理路径。
        /// </summary>
        private void CollectFaceTexturesForCurrentRotation(PawnFaceConfig faceConfig, HashSet<string> seenPaths, List<string> paths)
        {
            if (faceConfig.layeredParts == null) return;

            foreach (var part in faceConfig.layeredParts)
            {
                if (!part.enabled) continue;

                string texPath = GetTexPathForRotation(part);
                if (!string.IsNullOrWhiteSpace(texPath) && seenPaths.Add(texPath))
                {
                    paths.Add(texPath);
                }
            }
        }

        /// <summary>
        /// 根据当前预览朝向获取部件的纹理路径。
        /// </summary>
        private string GetTexPathForRotation(LayeredFacePartConfig part)
        {
            // 优先使用朝向特定的纹理路径
            if (previewRotation == Rot4.North && !string.IsNullOrWhiteSpace(part.texPathNorth))
            {
                return part.texPathNorth;
            }

            if ((previewRotation == Rot4.East || previewRotation == Rot4.West) && !string.IsNullOrWhiteSpace(part.texPathEast))
            {
                return part.texPathEast;
            }

            if (previewRotation == Rot4.South && !string.IsNullOrWhiteSpace(part.texPathSouth))
            {
                return part.texPathSouth;
            }

            // 回退到通用路径
            if (!string.IsNullOrWhiteSpace(part.texPath))
            {
                return part.texPath;
            }

            // 最后回退到 South 路径
            if (!string.IsNullOrWhiteSpace(part.texPathSouth))
            {
                return part.texPathSouth;
            }

            return string.Empty;
        }
    }
}
