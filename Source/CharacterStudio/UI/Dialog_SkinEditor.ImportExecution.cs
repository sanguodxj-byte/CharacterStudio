using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 导入执行 / 合并
        // ─────────────────────────────────────────────

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

            ReplaceWorkingSkin(skinDef.Clone(), skinDef.defName, skinDef.targetRaces.FirstOrDefault(), syncAbilities: true);

            var previewRace = ResolvePreferredPreviewRace(workingSkin);
            SyncPreviewRace(previewRace, null);
            ApplySelectionAfterImport(workingSkin.layers.Count, true, workingSkin.baseAppearance);

            Pawn? resolvedTargetPawn = ResolvePreferredTargetPawnForSkin(workingSkin, previewRace);
            FinalizeImportState(resolvedTargetPawn);
            ShowImportCompletionStatus(workingSkin.label ?? workingSkin.defName, workingSkin.layers.Count, resolvedTargetPawn, true);
            ShowImportMissingTextureWarning(
                workingSkin.label ?? workingSkin.defName,
                CollectMissingExternalTexturesForImport(skinDef));
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

            bool shouldHideVanillaBody;
            bool shouldHideVanillaHead;
            bool shouldHideVanillaHair;

            if (importResult.isFromExistingSkin)
            {
                var existingComp = pawn.GetComp<CharacterStudio.Core.CompPawnSkin>();
                var existingSkin = existingComp?.ActiveSkin;
                shouldHideVanillaBody = existingSkin?.hideVanillaBody ?? false;
                shouldHideVanillaHead = existingSkin?.hideVanillaHead ?? false;
                shouldHideVanillaHair = existingSkin?.hideVanillaHair ?? false;
            }
            else
            {
                bool hasImportedBodySlot = importedBaseAppearance.EnabledSlots().Any(slot => slot.slotType == BaseAppearanceSlotType.Body);
                bool hasImportedHeadSlot = importedBaseAppearance.EnabledSlots().Any(slot =>
                    slot.slotType == BaseAppearanceSlotType.Head);
                bool hasImportedHairSlot = importedBaseAppearance.EnabledSlots().Any(slot =>
                    slot.slotType == BaseAppearanceSlotType.Hair
                    || slot.slotType == BaseAppearanceSlotType.Beard);

                shouldHideVanillaBody = hasImportedBodySlot
                    || sourceTags.Any(tag =>
                        !string.IsNullOrEmpty(tag) && tag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0)
                    || layers.Any(layer =>
                        !string.IsNullOrEmpty(layer.anchorTag) && layer.anchorTag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0);

                shouldHideVanillaHead = hasImportedHeadSlot
                    || sourceTags.Any(tag =>
                        !string.IsNullOrEmpty(tag) && tag.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    || layers.Any(layer =>
                        !string.IsNullOrEmpty(layer.anchorTag) && layer.anchorTag.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0);

                shouldHideVanillaHair = hasImportedHairSlot
                    || sourceTags.Any(tag =>
                        !string.IsNullOrEmpty(tag) && (tag.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0 || tag.IndexOf("Beard", StringComparison.OrdinalIgnoreCase) >= 0))
                    || layers.Any(layer =>
                        !string.IsNullOrEmpty(layer.anchorTag) && (layer.anchorTag.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0 || layer.anchorTag.IndexOf("Beard", StringComparison.OrdinalIgnoreCase) >= 0));
            }

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
            if (workingSkin == null || workingDocument == null)
            {
                Log.Error("[CharacterStudio] 导入失败：编辑器工作状态未初始化");
                ShowStatus("CS_Studio_Err_ImportFailed".Translate(sourceLabel));
                return;
            }

            workingSkin.layers ??= new List<PawnLayerConfig>();
            workingSkin.hiddenPaths ??= new List<string>();
#pragma warning disable CS0618
            workingSkin.hiddenTags ??= new List<string>();
#pragma warning restore CS0618
            workingSkin.targetRaces ??= new List<string>();
            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            layers ??= new List<PawnLayerConfig>();
            sourcePaths ??= new List<string>();
            sourceTags ??= new List<string>();
            importedBaseAppearance ??= new BaseAppearanceConfig();

            if (replaceExisting)
            {
                workingSkin.layers.Clear();
                workingSkin.hiddenPaths.Clear();
#pragma warning disable CS0618
                workingSkin.hiddenTags.Clear();
#pragma warning restore CS0618
                workingSkin.hideVanillaBody = false;
                workingSkin.hideVanillaHead = false;
                workingSkin.hideVanillaHair = false;
                workingSkin.hideVanillaApparel = false;
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
                if (layer == null)
                {
                    continue;
                }

                workingSkin.layers.Add(layer.Clone());
            }

            foreach (var path in sourcePaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && !workingSkin.hiddenPaths.Contains(path))
                {
                    workingSkin.hiddenPaths.Add(path);
                }
            }

#pragma warning disable CS0618
            foreach (var tag in sourceTags)
            {
                if (!string.IsNullOrWhiteSpace(tag) && !workingSkin.hiddenTags.Contains(tag))
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

            RebuildNodeRulesFromWorkingSkin();

            Pawn? resolvedTargetPawn = boundPawn ?? ResolvePreferredTargetPawnForSkin(workingSkin, previewRace);
            ApplySelectionAfterImport(layers.Count, replaceExisting, importedBaseAppearance);
            FinalizeImportState(resolvedTargetPawn);
            ShowImportCompletionStatus(sourceLabel, layers.Count, resolvedTargetPawn, replaceExisting);
            ShowImportMissingTextureWarning(
                sourceLabel,
                CollectMissingExternalTexturesForImport(layers, importedBaseAppearance));
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

            string preferredRaceDefName = boundPawn?.def?.defName
                ?? workingSkin?.targetRaces?.FirstOrDefault()
                ?? mannequin?.CurrentPawn?.def?.defName
                ?? string.Empty;
            if (workingDocument != null)
            {
                workingDocument.preferredPreviewRaceDefName = preferredRaceDefName;
                workingDocument.preferredTargetRaceDefName = boundPawn?.def?.defName ?? preferredRaceDefName;
            }

            ThingDef? preferredRace = boundPawn?.def;
            if (preferredRace == null && !string.IsNullOrWhiteSpace(preferredRaceDefName))
            {
                preferredRace = DefDatabase<ThingDef>.GetNamedSilentFail(preferredRaceDefName);
            }

            ForceResetPreviewMannequin(preferredRace, boundPawn);
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

        private void ShowImportMissingTextureWarning(string sourceLabel, List<string> missingPaths)
        {
            if (missingPaths == null || missingPaths.Count == 0)
            {
                return;
            }

            string missingPathList = string.Join(Environment.NewLine, missingPaths.Select(path => $"• {path}"));
            ShowStatus("CS_Studio_Warn_ImportMissingTexturesStatus".Translate(missingPaths.Count));
            Messages.Message(
                "CS_Studio_Warn_ImportMissingTexturesDetail".Translate(sourceLabel, missingPaths.Count, missingPathList),
                MessageTypeDefOf.RejectInput,
                false);
        }

        private static List<string> CollectMissingExternalTexturesForImport(PawnSkinDef? importedSkin)
        {
            return PawnSkinRuntimeValidator.CollectMissingExternalTexturePaths(importedSkin);
        }

        private static List<string> CollectMissingExternalTexturesForImport(
            List<PawnLayerConfig> layers,
            BaseAppearanceConfig importedBaseAppearance)
        {
            var importedSkin = new PawnSkinDef
            {
                baseAppearance = importedBaseAppearance?.Clone() ?? new BaseAppearanceConfig()
            };

            importedSkin.layers ??= new List<PawnLayerConfig>();
            if (layers != null)
            {
                foreach (var layer in layers)
                {
                    if (layer == null)
                    {
                        continue;
                    }

                    importedSkin.layers.Add(layer.Clone());
                }
            }

            return PawnSkinRuntimeValidator.CollectMissingExternalTexturePaths(importedSkin);
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
    }
}