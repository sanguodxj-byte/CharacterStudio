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

            // 避免与文本输入框冲突：输入控件聚焦时不拦截 Ctrl+A
            if (evt.keyCode == KeyCode.A && GUIUtility.keyboardControl != 0)
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
                    ShowStatus($"已全选 {workingSkin.layers.Count} 个图层");
                }
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
            editorHistory.PushUndo(workingSkin, selectedLayerIndex, selectedLayerIndices);
        }

        private void ApplyUndoSnapshot()
        {
            if (!editorHistory.TryUndo(workingSkin, selectedLayerIndex, selectedLayerIndices, out var snapshot) || snapshot == null)
            {
                ShowStatus("没有可撤销的改动");
                return;
            }

            workingSkin = snapshot.Skin.Clone();
            selectedLayerIndex = snapshot.SelectedLayerIndex;
            selectedLayerIndices = new HashSet<int>(snapshot.SelectedLayerIndices);

            SanitizeLayerSelection();
            isDirty = true;
            RefreshPreview();
            RefreshRenderTree();
            ShowStatus("已撤销上次改动");
        }

        private void ApplyRedoSnapshot()
        {
            if (!editorHistory.TryRedo(workingSkin, selectedLayerIndex, selectedLayerIndices, out var snapshot) || snapshot == null)
            {
                ShowStatus("没有可重做的改动");
                return;
            }

            workingSkin = snapshot.Skin.Clone();
            selectedLayerIndex = snapshot.SelectedLayerIndex;
            selectedLayerIndices = new HashSet<int>(snapshot.SelectedLayerIndices);

            SanitizeLayerSelection();
            isDirty = true;
            RefreshPreview();
            RefreshRenderTree();
            ShowStatus("已重做上次改动");
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

            bool shift = Event.current.shift;

            if (shift)
            {
                // Shift+左键：增量多选（再次点击可取消）
                if (!selectedLayerIndices.Add(index))
                {
                    selectedLayerIndices.Remove(index);
                }
                selectedLayerIndex = selectedLayerIndices.Count > 0 ? selectedLayerIndices.Min() : -1;
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

        private int ApplyUniformScaleToSelectedLayers(float uniformScale, bool captureUndo)
        {
            var targets = GetSelectedLayerTargets();
            if (targets.Count == 0)
            {
                return 0;
            }

            uniformScale = Mathf.Clamp(uniformScale, 0.1f, 3f);

            if (captureUndo)
            {
                CaptureUndoSnapshot();
            }

            int changed = 0;
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

            if (changed > 0)
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

            if (captureUndo)
            {
                CaptureUndoSnapshot();
            }

            int changed = 0;
            foreach (int idx in targets)
            {
                var layer = workingSkin.layers[idx];
                if (Mathf.Abs(layer.drawOrder - drawOrder) > 0.0001f)
                {
                    layer.drawOrder = drawOrder;
                    changed++;
                }
            }

            if (changed > 0)
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

            if (captureUndo)
            {
                CaptureUndoSnapshot();
            }

            int changed = 0;
            foreach (int idx in targets)
            {
                var layer = workingSkin.layers[idx];
                if (layer.visible != visible)
                {
                    layer.visible = visible;
                    changed++;
                }
            }

            if (changed > 0)
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

            if (captureUndo)
            {
                CaptureUndoSnapshot();
            }

            int changed = 0;
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

            if (changed > 0)
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
