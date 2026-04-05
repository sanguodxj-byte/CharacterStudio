using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Introspection;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 事件处理 / 工作流
        // ─────────────────────────────────────────────

        private void OnNewSkin()
        {
            if (isDirty)
            {
                if (Time.realtimeSinceStartup < nextNewSkinPromptAllowedTime)
                {
                    return;
                }

                nextNewSkinPromptAllowedTime = Time.realtimeSinceStartup + NewSkinPromptCooldownSeconds;

                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "CS_Studio_Msg_UnsavedChanges".Translate(),
                    ShowNewSkinWorkflowMenu,
                    true));
            }
            else
            {
                ShowNewSkinWorkflowMenu();
            }
        }

        private void ShowNewSkinWorkflowMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption(
                    "CS_Studio_Workflow_StandardLayers".Translate(),
                    () => CreateNewSkin(NewSkinWorkflow.StandardLayers)),
                new FloatMenuOption(
                    "CS_Studio_Workflow_CompositeBase".Translate(),
                    () => CreateNewSkin(NewSkinWorkflow.CompositeBase)),
                new FloatMenuOption(
                    "CS_Studio_Workflow_AnimalStandardLayers".Translate(),
                    () => CreateNewSkin(NewSkinWorkflow.AnimalStandardLayers)),
                new FloatMenuOption(
                    "CS_Studio_Workflow_MechanoidStandardLayers".Translate(),
                    () => CreateNewSkin(NewSkinWorkflow.MechanoidStandardLayers))
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void CreateNewSkin(NewSkinWorkflow workflow)
        {
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            ThingDef previewRace = ResolveDefaultRaceForWorkflow(workflow);
            bool isNonHumanlikeWorkflow = workflow == NewSkinWorkflow.AnimalStandardLayers
                || workflow == NewSkinWorkflow.MechanoidStandardLayers;

            nextNewSkinPromptAllowedTime = 0f;

            ReplaceWorkingSkin(new PawnSkinDef
            {
                defName = $"CS_Skin_{uniqueId}",
                label = "CS_Studio_DefaultSkinLabel".Translate(),
                description = "",
                author = "",
                version = "1.0.0",
                humanlikeOnly = !isNonHumanlikeWorkflow
            }, preferredRaceDefName: previewRace.defName, syncAbilities: false);

            workingSkin.layers.Clear();
            workingSkin.baseAppearance = new BaseAppearanceConfig();
            workingSkin.hiddenPaths.Clear();
#pragma warning disable CS0618
            workingSkin.hiddenTags.Clear();
#pragma warning restore CS0618
            workingSkin.hideVanillaBody = false;
            workingSkin.hideVanillaHead = false;
            workingSkin.hideVanillaHair = false;
            workingSkin.hideVanillaApparel = false;
            workingSkin.targetRaces.Clear();
            workingSkin.targetRaces.Add(previewRace.defName);

            if (workflow == NewSkinWorkflow.CompositeBase)
            {
                workingSkin.hideVanillaBody = true;
                workingSkin.hideVanillaHead = true;
                workingSkin.hideVanillaHair = true;

                workingSkin.layers.Add(new PawnLayerConfig
                {
                    layerName = "CS_Studio_Workflow_CompositeBaseLayerName".Translate(),
                    anchorTag = "Body",
                    drawOrder = 0f,
                    colorSource = LayerColorSource.Fixed,
                    colorTwoSource = LayerColorSource.Fixed,
                    customColor = Color.white,
                    customColorTwo = Color.white,
                    scale = Vector2.one,
                    visible = true
                });

                selectedLayerIndex = 0;
                selectedLayerIndices.Clear();
                selectedLayerIndices.Add(0);
                currentTab = EditorTab.Layers;
                ShowStatus("CS_Studio_Workflow_CompositeBase_Hint".Translate());
            }
            else
            {
                ShowStatus(GetWorkflowStatusKey(workflow).Translate());
            }

            if (workingDocument != null)
            {
                workingDocument.preferredPreviewRaceDefName = previewRace.defName;
                workingDocument.preferredTargetRaceDefName = previewRace.defName;
            }

            workingAbilities.Clear();
            if (workflow != NewSkinWorkflow.CompositeBase)
            {
                selectedLayerIndex = -1;
                selectedLayerIndices.Clear();
                currentTab = EditorTab.BaseAppearance;
            }

            selectedNodePath = "";
            selectedBaseSlotType = null;
            isDirty = false;
            exprPathBuffer.Clear();

            mannequin?.ForceReset(previewRace);

            RefreshRenderTree();
            RefreshPreview();
        }

        private ThingDef ResolveDefaultRaceForWorkflow(NewSkinWorkflow workflow)
        {
            switch (workflow)
            {
                case NewSkinWorkflow.AnimalStandardLayers:
                    return MannequinManager.GetAvailableRaces(includeHumanlike: false, includeAnimals: true, includeMechanoids: false)
                        .FirstOrDefault() ?? ThingDefOf.Human;
                case NewSkinWorkflow.MechanoidStandardLayers:
                    return MannequinManager.GetAvailableRaces(includeHumanlike: false, includeAnimals: false, includeMechanoids: true)
                        .FirstOrDefault() ?? ThingDefOf.Human;
                default:
                    return ThingDefOf.Human;
            }
        }

        private string GetWorkflowStatusKey(NewSkinWorkflow workflow)
        {
            switch (workflow)
            {
                case NewSkinWorkflow.AnimalStandardLayers:
                    return "CS_Studio_Workflow_AnimalStandardLayers";
                case NewSkinWorkflow.MechanoidStandardLayers:
                    return "CS_Studio_Workflow_MechanoidStandardLayers";
                default:
                    return "CS_Studio_Workflow_StandardLayers";
            }
        }

        private void OnExportMod()
        {
            bool hasCustomLayers = workingSkin.layers != null && workingSkin.layers.Count > 0;
            bool hasBaseSlots = workingSkin.baseAppearance != null && workingSkin.baseAppearance.EnabledSlots().Any();

            if (!hasCustomLayers && !hasBaseSlots)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "CS_Studio_Warn_NoLayers".Translate(),
                    "CS_Studio_Btn_ContinueExport".Translate(),
                    () => Find.WindowStack.Add(new Dialog_ExportMod(workingSkin, workingAbilities, workingDocument.characterDefinition)),
                    "CS_Studio_Btn_Cancel".Translate(),
                    null,
                    null,
                    false,
                    null,
                    null,
                    WindowLayer.Dialog
                ));
                return;
            }

            bool hasValidTexture = (workingSkin.layers?.Any(l => !string.IsNullOrEmpty(l.texPath)) ?? false)
                || (workingSkin.baseAppearance?.EnabledSlots().Any(s => !string.IsNullOrEmpty(s.texPath)) ?? false);
            if (!hasValidTexture)
            {
                ShowStatus("CS_Studio_Warn_NoTexture".Translate());
            }

            SyncAbilitiesToSkin();
            Find.WindowStack.Add(new Dialog_ExportMod(workingSkin, workingAbilities, workingDocument.characterDefinition));
        }

        private void OnOpenSkinFolder()
        {
            string skinDir = System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Skins");

            try
            {
                System.IO.Directory.CreateDirectory(skinDir);

                var openFolderInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = skinDir,
                    UseShellExecute = true,
                    Verb = "open",
                    WorkingDirectory = skinDir
                };

                var process = System.Diagnostics.Process.Start(openFolderInfo);
                if (process == null)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/e," + '"' + skinDir + '"',
                        UseShellExecute = true,
                        WorkingDirectory = skinDir
                    });
                }

                ShowStatus("CS_Studio_Msg_OpenSkinFolder".Translate(skinDir));
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 打开皮肤文件夹失败: {ex.Message}");
                GUIUtility.systemCopyBuffer = skinDir;
                ShowStatus("CS_Studio_Err_OpenSkinFolder".Translate(skinDir));
            }
        }

        private void OnOpenSkinSettings()
        {
            SyncAbilitiesToSkin();
            Find.WindowStack.Add(new Dialog_SkinSettings(workingSkin, () =>
            {
                isDirty = true;
                SyncAbilitiesFromSkin();
                RefreshPreview();
            }));
        }

        private void OnOpenLlmSettings()
        {
            Find.WindowStack.Add(new Dialog_LlmSettings(_ =>
            {
                isDirty = true;
                ShowStatus("CS_LLM_Settings_SaveSuccess".Translate());
            }));
        }

        private void OnResetParameters()
        {
            var targets = GetSelectedLayerTargets();
            if (targets.Count > 0)
            {
                CaptureUndoSnapshot();
                foreach (int idx in targets)
                {
                    var layer = workingSkin.layers[idx];
                    layer.offset = Vector3.zero;
                    layer.offsetEast = Vector3.zero;
                    layer.offsetNorth = Vector3.zero;
                    layer.scale = Vector2.one;
                    layer.rotation = 0f;
                }
                isDirty = true;
                RefreshPreview();
                ShowStatus("CS_Studio_Msg_ParametersReset".Translate());
            }
            else if (selectedBaseSlotType != null)
            {
                CaptureUndoSnapshot();
                workingSkin.baseAppearance ??= new BaseAppearanceConfig();
                var slot = workingSkin.baseAppearance.GetSlot(selectedBaseSlotType.Value);
                slot.offset = Vector3.zero;
                slot.offsetEast = Vector3.zero;
                slot.offsetNorth = Vector3.zero;
                slot.scale = Vector2.one;
                slot.rotation = 0f;
                isDirty = true;
                RefreshPreview();
                ShowStatus("CS_Studio_Msg_ParametersReset".Translate());
            }
        }

        private void OnAddLayer()
        {
            CaptureUndoSnapshot();
            var newLayer = new PawnLayerConfig
            {
                layerName = GetDefaultLayerLabel(workingSkin.layers.Count),
                anchorTag = "Head"
            };
            workingSkin.layers.Add(newLayer);
            selectedLayerIndex = workingSkin.layers.Count - 1;
            selectedLayerIndices.Clear();
            selectedLayerIndices.Add(selectedLayerIndex);
            selectedNodePath = "";
            selectedBaseSlotType = null;
            currentTab = EditorTab.Layers;
            isDirty = true;
            RefreshPreview();
        }

        private void OnRemoveLayer()
        {
            DeleteSelectedLayers();
        }

        private void RemapSelectionAfterRemoveIndex(int removedIndex)
        {
            var remapped = new HashSet<int>();
            foreach (int idx in selectedLayerIndices)
            {
                if (idx == removedIndex)
                {
                    continue;
                }

                remapped.Add(idx > removedIndex ? idx - 1 : idx);
            }
            selectedLayerIndices = remapped;

            if (selectedLayerIndex == removedIndex)
            {
                selectedLayerIndex = -1;
            }
            else if (selectedLayerIndex > removedIndex)
            {
                selectedLayerIndex--;
            }

            SanitizeLayerSelection();
        }

        private bool MoveSelectedLayerUp()
        {
            if (selectedLayerIndex <= 0 || selectedLayerIndex >= workingSkin.layers.Count)
            {
                return false;
            }

            CaptureUndoSnapshot();
            var layer = workingSkin.layers[selectedLayerIndex];
            workingSkin.layers.RemoveAt(selectedLayerIndex);
            workingSkin.layers.Insert(selectedLayerIndex - 1, layer);
            selectedLayerIndex--;
            selectedLayerIndices.Clear();
            selectedLayerIndices.Add(selectedLayerIndex);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool MoveSelectedLayerDown()
        {
            if (selectedLayerIndex < 0 || selectedLayerIndex >= workingSkin.layers.Count - 1)
            {
                return false;
            }

            CaptureUndoSnapshot();
            var layer = workingSkin.layers[selectedLayerIndex];
            workingSkin.layers.RemoveAt(selectedLayerIndex);
            workingSkin.layers.Insert(selectedLayerIndex + 1, layer);
            selectedLayerIndex++;
            selectedLayerIndices.Clear();
            selectedLayerIndices.Add(selectedLayerIndex);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private void ShowLayerOrderMenu()
        {
            if (selectedLayerIndex < 0)
            {
                return;
            }

            var options = new List<FloatMenuOption>();

            if (selectedLayerIndex > 0)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveUp".Translate(), () =>
                {
                    MoveSelectedLayerUp();
                }));
            }

            if (selectedLayerIndex < workingSkin.layers.Count - 1)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveDown".Translate(), () =>
                {
                    MoveSelectedLayerDown();
                }));
            }

            options.Add(new FloatMenuOption("CS_Studio_Panel_Duplicate".Translate(), () =>
            {
                CaptureUndoSnapshot();
                var layer = workingSkin.layers[selectedLayerIndex].Clone();
                layer.layerName += " (Copy)";
                workingSkin.layers.Insert(selectedLayerIndex + 1, layer);
                selectedLayerIndex++;
                selectedLayerIndices.Clear();
                selectedLayerIndices.Add(selectedLayerIndex);
                isDirty = true;
                RefreshPreview();
            }));

            if (options.Any())
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void OnSelectTexture(PawnLayerConfig layer)
        {
            Find.WindowStack.Add(new Dialog_FileBrowser(layer.texPath, path =>
            {
                layer.texPath = path;
                TrySyncEditableFaceLayerTextureToFaceConfig(layer);
                isDirty = true;
                RefreshPreview();
                RefreshRenderTree();
            }));
        }

        private void ShowHiddenTagsMenu()
        {
            var options = new List<FloatMenuOption>();
#pragma warning disable CS0618
            var commonTags = new[]
            {
                "Head", "Hair", "Body", "Beard", "Tattoo", "FaceTattoo", "BodyTattoo",
                "Headgear", "Eyes", "Brow", "Jaw", "Ear", "Nose", "Mouth"
            };

            foreach (var tag in commonTags)
            {
                if (workingSkin.hiddenTags == null || !workingSkin.hiddenTags.Contains(tag))
                {
                    options.Add(new FloatMenuOption(tag, () =>
                    {
                        if (workingSkin.hiddenTags == null)
                        {
                            workingSkin.hiddenTags = new List<string>();
                        }
                        workingSkin.hiddenTags.Add(tag);
                        isDirty = true;
                        RefreshPreview();
                        RefreshRenderTree();
                        ShowStatus("CS_Studio_Msg_AddedHiddenTag".Translate(tag));
                    }));
                }
            }

            if (mannequin?.CurrentPawn != null)
            {
                options.Add(new FloatMenuOption("CS_Studio_Ctx_FromRenderTree".Translate(), () => ShowRenderTreeTagSelector()));
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                ShowStatus("CS_Studio_Msg_AllTagsAdded".Translate());
            }
#pragma warning restore CS0618
        }

        private void ShowRenderTreeTagSelector()
        {
            var pawn = mannequin?.CurrentPawn;
            if (pawn?.Drawer?.renderer?.renderTree == null)
            {
                return;
            }

            var rootSnapshot = RenderTreeParser.Capture(pawn);
            if (rootSnapshot == null)
            {
                return;
            }

            var allSnapshots = new List<RenderNodeSnapshot>();
            CollectSnapshots(rootSnapshot, allSnapshots);

            var options = new List<FloatMenuOption>();
#pragma warning disable CS0618
            foreach (var snapshot in allSnapshots)
            {
                if (!string.IsNullOrEmpty(snapshot.tagDefName) && snapshot.tagDefName != "Untagged" &&
                    (workingSkin.hiddenTags == null || !workingSkin.hiddenTags.Contains(snapshot.tagDefName)))
                {
                    var tagName = snapshot.tagDefName;
                    options.Add(new FloatMenuOption($"{tagName} ({snapshot.workerClass})", () =>
                    {
                        if (workingSkin.hiddenTags == null)
                        {
                            workingSkin.hiddenTags = new List<string>();
                        }
                        workingSkin.hiddenTags.Add(tagName);
                        isDirty = true;
                        RefreshPreview();
                        RefreshRenderTree();
                        ShowStatus("CS_Studio_Msg_TagAdded".Translate(tagName));
                    }));
                }
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                ShowStatus("CS_Studio_Msg_NoMoreTags".Translate());
            }
#pragma warning restore CS0618
        }
    }
}
