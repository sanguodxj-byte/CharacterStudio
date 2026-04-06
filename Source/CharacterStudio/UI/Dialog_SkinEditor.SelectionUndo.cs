using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 交互与快捷键（多选 / 归一化 / 撤回）
        // ─────────────────────────────────────────────

        private void HandleGlobalShortcuts()
        {
            var evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown)
            {
                return;
            }

            bool ctrl = evt.control || evt.command;
            if (!ctrl)
            {
                return;
            }

            // 避免与文本输入框冲突：输入控件聚焦时不拦截 Ctrl+A / Ctrl+D
            if ((evt.keyCode == KeyCode.A || evt.keyCode == KeyCode.D) && GUIUtility.keyboardControl != 0)
            {
                return;
            }

            // Ctrl+A：全选图层
            if (evt.keyCode == KeyCode.A)
            {
                if (workingSkin.layers.Count > 0)
                {
                    selectedLayerIndices.Clear();
                    for (int i = 0; i < workingSkin.layers.Count; i++)
                    {
                        selectedLayerIndices.Add(i);
                    }
                    selectedLayerIndex = 0;
                    selectedNodePath = "";
                    ShowStatus("CS_Studio_Msg_SelectAllLayers".Translate(workingSkin.layers.Count));
                }
                evt.Use();
                return;
            }

            // Ctrl+D：取消选择
            if (evt.keyCode == KeyCode.D)
            {
                selectedLayerIndices.Clear();
                selectedLayerIndex = -1;
                selectedNodePath = "";
                ShowStatus("CS_Studio_Msg_SelectionCleared".Translate());
                evt.Use();
                return;
            }

            // Ctrl+Z：撤销
            if (evt.keyCode == KeyCode.Z)
            {
                ApplyUndoSnapshot();
                evt.Use();
                return;
            }

            // Ctrl+Y：重做
            if (evt.keyCode == KeyCode.Y)
            {
                ApplyRedoSnapshot();
                evt.Use();
            }
        }

        private void CaptureUndoSnapshot()
        {
            SyncWorkingDocumentFromWorkingSkin();
            editorHistory.PushUndo(
                workingDocument,
                selectedLayerIndex,
                selectedLayerIndices,
                selectedEquipmentIndex,
                selectedNodePath,
                selectedBaseSlotType,
                layerModificationWorkflowActive,
                workingRenderFixPatch);
        }

        private void RebuildEditorBuffersFromWorkingState()
        {
            if (workingSkin?.faceConfig != null)
            {
                RebuildFaceImportBuffers(workingSkin.faceConfig);
            }
            else
            {
                layeredPartPathBuffer.Clear();
                exprPathBuffer.Clear();
            }

            UIHelper.ClearNumericFieldFocusAndBuffers();
        }

        private void FinalizeMutatedEditorState(bool refreshPreview = true, bool refreshRenderTree = false, string? statusMessage = null)
        {
            isDirty = true;

            if (refreshPreview)
                RefreshPreview();

            if (refreshRenderTree)
                RefreshRenderTree();

            if (!string.IsNullOrWhiteSpace(statusMessage))
                ShowStatus(statusMessage!);
        }

        private void MutateWithUndo(Action mutation, bool refreshPreview = true, bool refreshRenderTree = false, string? statusMessage = null)
        {
            if (mutation == null)
                return;

            bool isOutermostMutation = undoMutationDepth == 0;

            if (isOutermostMutation)
                CaptureUndoSnapshot();

            undoMutationDepth++;
            try
            {
                mutation();
            }
            finally
            {
                undoMutationDepth--;
            }

            if (isOutermostMutation)
                FinalizeMutatedEditorState(refreshPreview, refreshRenderTree, statusMessage);
        }

        private void MutateFloatWithUndo(ref float target, float newValue, Action? onMutated = null, bool refreshPreview = true, bool refreshRenderTree = false, string? statusMessage = null)
        {
            bool isOutermostMutation = undoMutationDepth == 0;

            if (isOutermostMutation)
                CaptureUndoSnapshot();

            undoMutationDepth++;
            try
            {
                target = newValue;
                onMutated?.Invoke();
            }
            finally
            {
                undoMutationDepth--;
            }

            if (isOutermostMutation)
                FinalizeMutatedEditorState(refreshPreview, refreshRenderTree, statusMessage);
        }

        private void MutateRefWithUndo<T>(ref T target, T newValue, Action? onMutated = null, bool refreshPreview = true, bool refreshRenderTree = false, string? statusMessage = null)
        {
            bool isOutermostMutation = undoMutationDepth == 0;

            if (isOutermostMutation)
                CaptureUndoSnapshot();

            undoMutationDepth++;
            try
            {
                target = newValue;
                onMutated?.Invoke();
            }
            finally
            {
                undoMutationDepth--;
            }

            if (isOutermostMutation)
                FinalizeMutatedEditorState(refreshPreview, refreshRenderTree, statusMessage);
        }

        private void ApplyUndoSnapshot()
        {
            SyncWorkingDocumentFromWorkingSkin();

            if (!editorHistory.TryUndo(
                    workingDocument,
                    selectedLayerIndex,
                    selectedLayerIndices,
                    selectedEquipmentIndex,
                    selectedNodePath,
                    selectedBaseSlotType,
                    layerModificationWorkflowActive,
                    workingRenderFixPatch,
                    out var snapshot)
                || snapshot == null)
            {
                ShowStatus("CS_Studio_Msg_NothingToUndo".Translate());
                return;
            }

            workingDocument = snapshot.Document.Clone();
            workingSkin = workingDocument.runtimeSkin;
            SyncAbilitiesFromSkin();
            selectedLayerIndex = snapshot.SelectedLayerIndex;
            selectedLayerIndices = new HashSet<int>(snapshot.SelectedLayerIndices);
            selectedEquipmentIndex = snapshot.SelectedEquipmentIndex;
            selectedNodePath = snapshot.SelectedNodePath ?? string.Empty;
            selectedBaseSlotType = snapshot.SelectedBaseSlotType;
            layerModificationWorkflowActive = snapshot.LayerModificationWorkflowActive;
            workingRenderFixPatch = snapshot.WorkingRenderFixPatch?.Clone();

            SanitizeLayerSelection();
            RebuildEditorBuffersFromWorkingState();
            FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: true);
            ShowStatus("CS_Studio_Msg_UndoSuccess".Translate());
        }

        private void ApplyRedoSnapshot()
        {
            SyncWorkingDocumentFromWorkingSkin();

            if (!editorHistory.TryRedo(
                    workingDocument,
                    selectedLayerIndex,
                    selectedLayerIndices,
                    selectedEquipmentIndex,
                    selectedNodePath,
                    selectedBaseSlotType,
                    layerModificationWorkflowActive,
                    workingRenderFixPatch,
                    out var snapshot)
                || snapshot == null)
            {
                ShowStatus("CS_Studio_Msg_NothingToRedo".Translate());
                return;
            }

            workingDocument = snapshot.Document.Clone();
            workingSkin = workingDocument.runtimeSkin;
            SyncAbilitiesFromSkin();
            selectedLayerIndex = snapshot.SelectedLayerIndex;
            selectedLayerIndices = new HashSet<int>(snapshot.SelectedLayerIndices);
            selectedEquipmentIndex = snapshot.SelectedEquipmentIndex;
            selectedNodePath = snapshot.SelectedNodePath ?? string.Empty;
            selectedBaseSlotType = snapshot.SelectedBaseSlotType;
            layerModificationWorkflowActive = snapshot.LayerModificationWorkflowActive;
            workingRenderFixPatch = snapshot.WorkingRenderFixPatch?.Clone();

            SanitizeLayerSelection();
            RebuildEditorBuffersFromWorkingState();
            FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: true);
            ShowStatus("CS_Studio_Msg_RedoSuccess".Translate());
        }

        private void SanitizeLayerSelection()
        {
            if (workingSkin.layers.Count == 0)
            {
                selectedLayerIndices.Clear();
                selectedLayerIndex = -1;
                return;
            }

            selectedLayerIndices.RemoveWhere(i => i < 0 || i >= workingSkin.layers.Count);

            if (selectedLayerIndex < -1 || selectedLayerIndex >= workingSkin.layers.Count)
            {
                selectedLayerIndex = -1;
            }

            // 若主选中无效但存在多选，取最小索引作为主选中
            if (selectedLayerIndex < 0 && selectedLayerIndices.Count > 0)
            {
                selectedLayerIndex = selectedLayerIndices.Min();
            }

            // 保证主选中包含在多选集合中（有主选中时）
            if (selectedLayerIndex >= 0)
            {
                selectedLayerIndices.Add(selectedLayerIndex);
            }
        }

        private void HandleLayerRowLeftClick(int index)
        {
            if (index < 0 || index >= workingSkin.layers.Count)
            {
                return;
            }

            UIHelper.ClearNumericFieldFocusAndBuffers();

            var evt = Event.current;
            bool shift = evt.shift;
            bool ctrl = evt.control || evt.command;
            int anchorIndex = selectedLayerIndex;

            if (shift)
            {
                // Shift+左键：按主选中做范围多选；与 Ctrl 组合时在现有多选上追加范围
                if (!ctrl)
                {
                    selectedLayerIndices.Clear();
                }

                if (anchorIndex >= 0 && anchorIndex < workingSkin.layers.Count)
                {
                    int start = Math.Min(anchorIndex, index);
                    int end = Math.Max(anchorIndex, index);
                    for (int i = start; i <= end; i++)
                    {
                        selectedLayerIndices.Add(i);
                    }
                }
                else
                {
                    selectedLayerIndices.Add(index);
                }

                selectedLayerIndex = index;
            }
            else if (ctrl)
            {
                // Ctrl+左键：切换单项多选状态
                if (!selectedLayerIndices.Add(index))
                {
                    selectedLayerIndices.Remove(index);
                }

                if (selectedLayerIndices.Contains(index))
                {
                    selectedLayerIndex = index;
                }
                else
                {
                    selectedLayerIndex = selectedLayerIndices.Count > 0 ? selectedLayerIndices.Min() : -1;
                }
            }
            else
            {
                // 普通左键：单选
                selectedLayerIndices.Clear();
                selectedLayerIndices.Add(index);
                selectedLayerIndex = index;
            }

            selectedNodePath = "";
            SanitizeLayerSelection();
        }

        private List<int> GetSelectedLayerTargets()
        {
            var targets = selectedLayerIndices
                .Where(i => i >= 0 && i < workingSkin.layers.Count)
                .Distinct()
                .ToList();

            if (targets.Count == 0 && selectedLayerIndex >= 0 && selectedLayerIndex < workingSkin.layers.Count)
            {
                targets.Add(selectedLayerIndex);
            }

            return targets;
        }

        private bool DeleteSelectedLayers(int? requestedIndex = null)
        {
            List<int> targets;

            if (requestedIndex.HasValue)
            {
                int index = requestedIndex.Value;
                if (index < 0 || index >= workingSkin.layers.Count)
                {
                    return false;
                }

                bool shouldDeleteSelection = selectedLayerIndices.Contains(index) && GetSelectedLayerTargets().Count > 1;
                targets = shouldDeleteSelection ? GetSelectedLayerTargets() : new List<int> { index };
            }
            else
            {
                targets = GetSelectedLayerTargets();
            }

            if (targets.Count == 0)
            {
                return false;
            }

            MutateWithUndo(() =>
            {
                List<PawnLayerConfig> removedLayers = targets
                    .Where(index => index >= 0 && index < workingSkin.layers.Count)
                    .Select(index => workingSkin.layers[index])
                    .ToList();

                foreach (PawnLayerConfig removedLayer in removedLayers)
                {
                    TryRemoveEditableFaceLayerFromFaceConfig(removedLayer);
                }

                foreach (int index in targets.OrderByDescending(i => i))
                {
                    workingSkin.layers.RemoveAt(index);
                }

                selectedLayerIndices.Clear();
                if (workingSkin.layers.Count == 0)
                {
                    selectedLayerIndex = -1;
                }
                else
                {
                    selectedLayerIndex = Mathf.Clamp(targets.Min(), 0, workingSkin.layers.Count - 1);
                    selectedLayerIndices.Add(selectedLayerIndex);
                }

                SanitizeLayerSelection();
            });
            return true;
        }

        private int ApplyUniformScaleToSelectedLayers(float uniformScale, bool captureUndo)
        {
            var targets = GetSelectedLayerTargets();
            if (targets.Count == 0)
            {
                return 0;
            }

            uniformScale = Mathf.Clamp(uniformScale, 0.1f, 3f);

            int changed = 0;
            Action apply = () =>
            {
                foreach (int idx in targets)
                {
                    var layer = workingSkin.layers[idx];
                    var desired = new Vector2(uniformScale, uniformScale);
                    if (layer.scale != desired)
                    {
                        layer.scale = desired;
                        changed++;
                    }
                }
            };

            if (captureUndo)
                MutateWithUndo(apply);
            else
                apply();

            if (changed > 0 && !captureUndo)
            {
                isDirty = true;
                RefreshPreview();
            }

            return changed;
        }

        private int ApplyUniformDrawOrderToSelectedLayers(float drawOrder, bool captureUndo)
        {
            var targets = GetSelectedLayerTargets();
            if (targets.Count == 0)
            {
                return 0;
            }

            int changed = 0;
            Action apply = () =>
            {
                foreach (int idx in targets)
                {
                    var layer = workingSkin.layers[idx];
                    if (Mathf.Abs(layer.drawOrder - drawOrder) > 0.0001f)
                    {
                        layer.drawOrder = drawOrder;
                        changed++;
                    }
                }
            };

            if (captureUndo)
                MutateWithUndo(apply);
            else
                apply();

            if (changed > 0 && !captureUndo)
            {
                isDirty = true;
                RefreshPreview();
            }

            return changed;
        }

        private int ApplyVisibilityToSelectedLayers(bool visible, bool captureUndo)
        {
            var targets = GetSelectedLayerTargets();
            if (targets.Count == 0)
            {
                return 0;
            }

            int changed = 0;
            Action apply = () =>
            {
                foreach (int idx in targets)
                {
                    var layer = workingSkin.layers[idx];
                    if (layer.visible != visible)
                    {
                        layer.visible = visible;
                        changed++;
                    }
                }
            };

            if (captureUndo)
                MutateWithUndo(apply);
            else
                apply();

            if (changed > 0 && !captureUndo)
            {
                isDirty = true;
                RefreshPreview();
            }

            return changed;
        }

        private int ApplyColorSettingsToSelectedLayers(
            LayerColorSource colorOneSource,
            Color colorOne,
            LayerColorSource colorTwoSource,
            Color colorTwo,
            bool captureUndo)
        {
            var targets = GetSelectedLayerTargets();
            if (targets.Count == 0)
            {
                return 0;
            }

            int changed = 0;
            Action apply = () =>
            {
                foreach (int idx in targets)
                {
                    var layer = workingSkin.layers[idx];
                    bool layerChanged = false;

                    if (layer.colorSource != colorOneSource)
                    {
                        layer.colorSource = colorOneSource;
                        layerChanged = true;
                    }

                    if (layer.customColor != colorOne)
                    {
                        layer.customColor = colorOne;
                        layerChanged = true;
                    }

                    if (layer.colorTwoSource != colorTwoSource)
                    {
                        layer.colorTwoSource = colorTwoSource;
                        layerChanged = true;
                    }

                    if (layer.customColorTwo != colorTwo)
                    {
                        layer.customColorTwo = colorTwo;
                        layerChanged = true;
                    }

                    if (layerChanged)
                    {
                        changed++;
                    }
                }
            };

            if (captureUndo)
                MutateWithUndo(apply);
            else
                apply();

            if (changed > 0 && !captureUndo)
            {
                isDirty = true;
                RefreshPreview();
            }

            return changed;
        }

        private void ApplyToOtherSelectedLayers(Action<PawnLayerConfig> apply)
        {
            ApplyToOtherSelectedLayers(selectedLayerIndex, apply);
        }

        private void ApplyToOtherSelectedLayers(int sourceIndex, Action<PawnLayerConfig> apply)
        {
            if (apply == null)
            {
                return;
            }

            var targets = GetSelectedLayerTargets();
            if (targets.Count <= 1)
            {
                return;
            }

            foreach (int idx in targets)
            {
                if (idx == sourceIndex || idx < 0 || idx >= workingSkin.layers.Count)
                {
                    continue;
                }

                apply(workingSkin.layers[idx]);
            }
        }
    }
}
