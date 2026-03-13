using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Exporter;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void OnSaveSkin()
        {
            // 验证必填字段
            if (string.IsNullOrEmpty(workingSkin.defName))
            {
                ShowStatus("CS_Studio_Err_DefNameEmpty".Translate());
                return;
            }

            if (!UIHelper.IsValidDefName(workingSkin.defName))
            {
                ShowStatus("CS_Studio_Err_DefNameInvalid".Translate());
                return;
            }

            if (string.IsNullOrEmpty(workingSkin.label))
            {
                workingSkin.label = workingSkin.defName;
            }

            // 保存到配置目录下的 CharacterStudio/Skins 文件夹
            string exportDir = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Skins");
            string fileName = workingSkin.defName + ".xml";
            string filePath = Path.Combine(exportDir, fileName);

            try
            {
                SkinSaver.SaveSkinDef(workingSkin, filePath);

                // 注册到运行时 DefDatabase，确保无需重启即可在菜单中出现
                var registered = PawnSkinDefRegistry.RegisterOrReplace(workingSkin.Clone());
                workingSkin = registered;

                // 如有目标 Pawn，保存时立即应用并触发渲染刷新
                if (targetPawn != null)
                {
                    if (PawnSkinRuntimeUtility.ApplySkinToPawn(targetPawn, registered))
                    {
                        Messages.Message(
                            "CS_Appearance_Applied".Translate(registered.label ?? registered.defName, targetPawn.LabelShort),
                            MessageTypeDefOf.PositiveEvent,
                            false
                        );
                    }
                }

                ShowStatus("CS_Studio_Msg_SaveSuccess".Translate());
                Messages.Message("CS_Studio_Msg_SavePath".Translate(filePath), MessageTypeDefOf.PositiveEvent, false);
                isDirty = false;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 保存失败: {ex}");
                ShowStatus("CS_Studio_Err_SaveFailed".Translate());
            }
        }

        private void OnApplyToTargetPawn()
        {
            if (targetPawn == null)
            {
                ShowStatus("CS_Studio_Msg_TargetPawnRequired".Translate());
                return;
            }

            try
            {
                // 应用时使用克隆，避免运行时引用与编辑态对象互相污染
                var runtimeSkin = workingSkin.Clone();
                if (PawnSkinRuntimeUtility.ApplySkinToPawn(targetPawn, runtimeSkin))
                {
                    // 显式触发一次隐藏节点刷新，确保编辑器“应用”语义稳定
                    CharacterStudio.Rendering.Patch_PawnRenderTree.RefreshHiddenNodes(targetPawn);
                    RefreshRenderTree();
                    ShowStatus("CS_Studio_Msg_AppliedToPawn".Translate(targetPawn.LabelShort));
                    Messages.Message(
                        "CS_Appearance_Applied".Translate(runtimeSkin.label ?? runtimeSkin.defName, targetPawn.LabelShort),
                        MessageTypeDefOf.PositiveEvent,
                        false
                    );
                }
                else
                {
                    ShowStatus("CS_Studio_Err_ApplyNoSkinComp".Translate());
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用到目标角色失败: {ex}");
                ShowStatus("CS_Studio_Err_ApplyFailedCheckLog".Translate());
            }
        }

        private void OnImportFromMap()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Import_Source_Map".Translate(), ShowMapPawnImportMenu),
                new FloatMenuOption("CS_Studio_Import_Source_Race".Translate(), OnImportFromRaceList),
                new FloatMenuOption("CS_Studio_Import_Source_ProjectSkin".Translate(), OnImportFromProjectSkins)
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowMapPawnImportMenu()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                ShowStatus("CS_Studio_Err_NoMap".Translate());
                return;
            }

            var pawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && p.Drawer?.renderer?.renderTree != null)
                .ToList();

            if (pawns.Count == 0)
            {
                ShowStatus("CS_Studio_Err_NoPawns".Translate());
                return;
            }

            var options = new List<FloatMenuOption>();
            foreach (var pawn in pawns)
            {
                var p = pawn;
                options.Add(new FloatMenuOption(
                    $"{p.LabelShort} ({p.kindDef.label})",
                    () => DoImportFromPawn(p, p, p.def, p.LabelShort, false)
                ));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OnImportFromRaceList()
        {
            var races = MannequinManager.GetAvailableRaces();
            if (races.Length == 0)
            {
                ShowStatus("CS_Studio_Err_NoRaces".Translate());
                return;
            }

            var options = new List<FloatMenuOption>();
            foreach (var race in races)
            {
                var raceLocal = race;
                string displayName = string.IsNullOrWhiteSpace(raceLocal.label) ? raceLocal.defName : raceLocal.label;
                options.Add(new FloatMenuOption(
                    $"{displayName} ({raceLocal.defName})",
                    () => DoImportFromRace(raceLocal)
                ));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OnImportFromProjectSkins()
        {
            PawnSkinDefRegistry.LoadFromConfig();
            var skins = PawnSkinDefRegistry.AllRuntimeDefs
                .Where(skin => skin != null)
                .OrderBy(skin => string.IsNullOrWhiteSpace(skin.label) ? skin.defName : skin.label)
                .ToList();

            if (skins.Count == 0)
            {
                ShowStatus("CS_Studio_Err_NoProjectSkins".Translate());
                return;
            }

            var options = new List<FloatMenuOption>();
            foreach (var skin in skins)
            {
                var skinLocal = skin;
                string displayName = string.IsNullOrWhiteSpace(skinLocal.label) ? skinLocal.defName : skinLocal.label;
                options.Add(new FloatMenuOption(
                    $"{displayName} ({skinLocal.defName})",
                    () => DoImportFromProjectSkin(skinLocal)
                ));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DoImportFromRace(ThingDef raceDef)
        {
            if (raceDef == null)
            {
                return;
            }

            if (!EnsureMannequinReady())
            {
                return;
            }

            mannequin!.SetRace(raceDef);
            var previewPawn = mannequin.CurrentPawn;
            if (previewPawn == null)
            {
                ShowStatus("CS_Studio_Err_ImportFailed".Translate(raceDef.label ?? raceDef.defName));
                return;
            }

            DoImportFromPawn(previewPawn, null, raceDef, raceDef.label ?? raceDef.defName, true);
        }

        private void DoImportFromProjectSkin(PawnSkinDef skinDef)
        {
            if (skinDef == null)
            {
                return;
            }

            workingSkin = skinDef.Clone();
            SyncAbilitiesFromSkin();

            var previewRace = ResolvePreferredPreviewRace(workingSkin);
            SyncPreviewRace(previewRace, null);
            ApplySelectionAfterImport(workingSkin.layers.Count, true, workingSkin.baseAppearance);

            Pawn? resolvedTargetPawn = ResolvePreferredTargetPawnForSkin(workingSkin, previewRace);
            FinalizeImportState(resolvedTargetPawn);
            ShowImportCompletionStatus(workingSkin.label ?? workingSkin.defName, workingSkin.layers.Count, resolvedTargetPawn, true);
        }

        private void DoImportFromPawn(
            Pawn pawn,
            Pawn? boundPawn,
            ThingDef? previewRace,
            string? sourceLabel,
            bool replaceTargetRaces)
        {
            var importResult = VanillaImportUtility.ImportFromPawnWithPaths(pawn);
            var layers = importResult.layers;
            var sourcePaths = importResult.sourcePaths;
            var sourceTags = importResult.sourceTags;
            var importedBaseAppearance = importResult.baseAppearance ?? new BaseAppearanceConfig();

            bool hasImportedBodySlot = importedBaseAppearance.EnabledSlots().Any(slot => slot.slotType == BaseAppearanceSlotType.Body);
            bool hasImportedHeadSlot = importedBaseAppearance.EnabledSlots().Any(slot =>
                slot.slotType == BaseAppearanceSlotType.Head
                || slot.slotType == BaseAppearanceSlotType.Eyes
                || slot.slotType == BaseAppearanceSlotType.Brow
                || slot.slotType == BaseAppearanceSlotType.Mouth
                || slot.slotType == BaseAppearanceSlotType.Nose
                || slot.slotType == BaseAppearanceSlotType.Ear);
            bool hasImportedHairSlot = importedBaseAppearance.EnabledSlots().Any(slot =>
                slot.slotType == BaseAppearanceSlotType.Hair
                || slot.slotType == BaseAppearanceSlotType.Beard);

            bool shouldHideVanillaBody = hasImportedBodySlot
                || sourceTags.Any(tag =>
                    !string.IsNullOrEmpty(tag) && tag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0)
                || layers.Any(layer =>
                    !string.IsNullOrEmpty(layer.anchorTag) && layer.anchorTag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0);

            bool shouldHideVanillaHead = hasImportedHeadSlot
                || sourceTags.Any(tag =>
                    !string.IsNullOrEmpty(tag) && tag.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                || layers.Any(layer =>
                    !string.IsNullOrEmpty(layer.anchorTag) && layer.anchorTag.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0);

            bool shouldHideVanillaHair = hasImportedHairSlot
                || sourceTags.Any(tag =>
                    !string.IsNullOrEmpty(tag) && (tag.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0 || tag.IndexOf("Beard", StringComparison.OrdinalIgnoreCase) >= 0))
                || layers.Any(layer =>
                    !string.IsNullOrEmpty(layer.anchorTag) && (layer.anchorTag.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0 || layer.anchorTag.IndexOf("Beard", StringComparison.OrdinalIgnoreCase) >= 0));

            string resolvedSourceLabel = sourceLabel ?? pawn.LabelShort;
            if (layers.Count == 0 && !importedBaseAppearance.EnabledSlots().Any())
            {
                ShowStatus("CS_Studio_Err_ImportFailed".Translate(resolvedSourceLabel));
                return;
            }

            SyncPreviewRace(previewRace, boundPawn);

            Action<bool> applyImport = replaceExisting => ApplyImportedAppearance(
                layers,
                sourcePaths,
                sourceTags,
                importedBaseAppearance,
                shouldHideVanillaBody,
                shouldHideVanillaHead,
                shouldHideVanillaHair,
                boundPawn,
                previewRace,
                resolvedSourceLabel,
                replaceTargetRaces,
                replaceExisting);

            if (workingSkin.layers.Count > 0 || workingSkin.baseAppearance.EnabledSlots().Any())
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("CS_Studio_Ctx_ClearAndImport".Translate(), () => applyImport(true)),
                    new FloatMenuOption("CS_Studio_Ctx_AppendImport".Translate(), () => applyImport(false)),
                    new FloatMenuOption("CS_Studio_Btn_Cancel".Translate(), () => { })
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                applyImport(true);
            }
        }

        private void ApplyImportedAppearance(
            List<PawnLayerConfig> layers,
            List<string> sourcePaths,
            List<string> sourceTags,
            BaseAppearanceConfig importedBaseAppearance,
            bool shouldHideVanillaBody,
            bool shouldHideVanillaHead,
            bool shouldHideVanillaHair,
            Pawn? boundPawn,
            ThingDef? previewRace,
            string sourceLabel,
            bool replaceTargetRaces,
            bool replaceExisting)
        {
            if (replaceExisting)
            {
                workingSkin.layers.Clear();
                workingSkin.hiddenPaths.Clear();
#pragma warning disable CS0618 // hiddenTags 仅用于旧数据兼容
                workingSkin.hiddenTags.Clear();
#pragma warning restore CS0618
                workingSkin.hideVanillaBody = false;
                workingSkin.hideVanillaHead = false;
                workingSkin.hideVanillaHair = false;
                workingSkin.baseAppearance = importedBaseAppearance.Clone();

                if (replaceTargetRaces)
                {
                    workingSkin.targetRaces.Clear();
                }
            }
            else
            {
                MergeBaseAppearanceSlots(workingSkin.baseAppearance, importedBaseAppearance);
            }

            foreach (var layer in layers)
            {
                workingSkin.layers.Add(layer);
            }

            foreach (var path in sourcePaths)
            {
                if (!string.IsNullOrEmpty(path) && !workingSkin.hiddenPaths.Contains(path))
                {
                    workingSkin.hiddenPaths.Add(path);
                }
            }

#pragma warning disable CS0618 // hiddenTags 仅用于旧数据兼容
            foreach (var tag in sourceTags)
            {
                if (!string.IsNullOrEmpty(tag) && !workingSkin.hiddenTags.Contains(tag))
                {
                    workingSkin.hiddenTags.Add(tag);
                }
            }
#pragma warning restore CS0618

            if (shouldHideVanillaBody)
            {
                workingSkin.hideVanillaBody = true;
            }
            if (shouldHideVanillaHead)
            {
                workingSkin.hideVanillaHead = true;
            }
            if (shouldHideVanillaHair)
            {
                workingSkin.hideVanillaHair = true;
            }

            if (replaceTargetRaces && previewRace != null && !workingSkin.targetRaces.Contains(previewRace.defName))
            {
                workingSkin.targetRaces.Add(previewRace.defName);
            }

            Pawn? resolvedTargetPawn = boundPawn ?? ResolvePreferredTargetPawnForSkin(workingSkin, previewRace);
            ApplySelectionAfterImport(layers.Count, replaceExisting, importedBaseAppearance);
            FinalizeImportState(resolvedTargetPawn);
            ShowImportCompletionStatus(sourceLabel, layers.Count, resolvedTargetPawn, replaceExisting);
        }

        private ThingDef? ResolvePreferredPreviewRace(PawnSkinDef skinDef)
        {
            if (skinDef.targetRaces != null)
            {
                foreach (var raceDefName in skinDef.targetRaces)
                {
                    var raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
                    if (raceDef?.race != null && raceDef.race.Humanlike)
                    {
                        return raceDef;
                    }
                }
            }

            return targetPawn?.def ?? mannequin?.CurrentPawn?.def ?? ThingDefOf.Human;
        }

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

        private void ApplySelectionAfterImport(int importedLayerCount, bool replaceExisting, BaseAppearanceConfig importedBaseAppearance)
        {
            selectedLayerIndex = importedLayerCount > 0
                ? (replaceExisting ? 0 : workingSkin.layers.Count - importedLayerCount)
                : -1;
            selectedLayerIndices.Clear();
            if (selectedLayerIndex >= 0)
            {
                selectedLayerIndices.Add(selectedLayerIndex);
            }

            selectedNodePath = string.Empty;
            if (importedBaseAppearance.EnabledSlots().Any())
            {
                selectedBaseSlotType = importedBaseAppearance.EnabledSlots().First().slotType;
                currentTab = EditorTab.BaseAppearance;
            }
            else
            {
                selectedBaseSlotType = null;
                currentTab = EditorTab.Layers;
            }
        }

        private void FinalizeImportState(Pawn? boundPawn)
        {
            targetPawn = boundPawn;
            isDirty = true;
            RefreshPreview();
            RefreshRenderTree();
        }

        private Pawn? ResolvePreferredTargetPawnForSkin(PawnSkinDef skinDef, ThingDef? preferredRace)
        {
            if (targetPawn != null && skinDef.IsValidForPawn(targetPawn))
            {
                return targetPawn;
            }

            var map = Find.CurrentMap;
            if (map == null)
            {
                return null;
            }

            IEnumerable<Pawn> candidates = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && p.Drawer?.renderer?.renderTree != null)
                .Where(skinDef.IsValidForPawn);

            if (preferredRace != null)
            {
                var sameRacePawn = candidates.FirstOrDefault(p => p.def == preferredRace);
                if (sameRacePawn != null)
                {
                    return sameRacePawn;
                }
            }

            return candidates.FirstOrDefault();
        }

        private void ShowImportCompletionStatus(string sourceLabel, int layerCount, Pawn? boundPawn, bool replaceExisting)
        {
            if (boundPawn != null)
            {
                ShowStatus(replaceExisting
                    ? "CS_Studio_Msg_ImportedBoundPawn".Translate(boundPawn.LabelShort)
                    : "CS_Studio_Msg_AppendedImportedBoundPawn".Translate(boundPawn.LabelShort));
                return;
            }

            ShowStatus(replaceExisting
                ? "CS_Studio_Msg_ImportedNoBoundPawn".Translate(sourceLabel, layerCount)
                : "CS_Studio_Msg_AppendedNoBoundPawn".Translate(sourceLabel, layerCount));
        }

        private static void MergeBaseAppearanceSlots(BaseAppearanceConfig target, BaseAppearanceConfig source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.EnsureAllSlotsExist();
            source.EnsureAllSlotsExist();
            foreach (var sourceSlot in source.EnabledSlots())
            {
                var targetSlot = target.GetSlot(sourceSlot.slotType);
                var cloned = sourceSlot.Clone();
                targetSlot.enabled = cloned.enabled;
                targetSlot.slotType = cloned.slotType;
                targetSlot.texPath = cloned.texPath;
                targetSlot.maskTexPath = cloned.maskTexPath;
                targetSlot.shaderDefName = cloned.shaderDefName;
                targetSlot.colorSource = cloned.colorSource;
                targetSlot.customColor = cloned.customColor;
                targetSlot.colorTwoSource = cloned.colorTwoSource;
                targetSlot.customColorTwo = cloned.customColorTwo;
                targetSlot.scale = cloned.scale;
                targetSlot.offset = cloned.offset;
                targetSlot.offsetEast = cloned.offsetEast;
                targetSlot.offsetNorth = cloned.offsetNorth;
                targetSlot.rotation = cloned.rotation;
                targetSlot.flipHorizontal = cloned.flipHorizontal;
                targetSlot.drawOrderOffset = cloned.drawOrderOffset;
                targetSlot.graphicClass = cloned.graphicClass;
            }
        }

        // ─────────────────────────────────────────────
        // Mannequin 管理
        // ─────────────────────────────────────────────

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
                ShowStatus("CS_Studio_Err_MannequinFailed".Translate());
            }
        }

        private void CleanupMannequin()
        {
            mannequin?.Cleanup();
            mannequin = null;
        }

        private void RefreshPreview()
        {
            mannequin?.ApplySkin(workingSkin);
            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewExpressionOverride(previewExpressionOverrideEnabled ? previewExpression : null);
        }

        private void ShowStatus(string message)
        {
            statusMessage = message;
            statusMessageTime = 3f;
        }
    }
}
