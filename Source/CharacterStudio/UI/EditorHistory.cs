using System.Collections.Generic;
using CharacterStudio.Core;
using CharacterStudio.Design;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 编辑器历史记录（Undo/Redo）
    /// 基于 CharacterDesignDocument 深拷贝快照。
    /// </summary>
    public class EditorHistory
    {
        public sealed class Snapshot
        {
            public CharacterDesignDocument Document { get; }
            public int SelectedLayerIndex { get; }
            public HashSet<int> SelectedLayerIndices { get; }
            public int SelectedEquipmentIndex { get; }
            public string SelectedNodePath { get; }
            public BaseAppearanceSlotType? SelectedBaseSlotType { get; }
            public bool LayerModificationWorkflowActive { get; }
            public CharacterRenderFixPatch? WorkingRenderFixPatch { get; }

            public Snapshot(
                CharacterDesignDocument document,
                int selectedLayerIndex,
                HashSet<int> selectedLayerIndices,
                int selectedEquipmentIndex,
                string selectedNodePath,
                BaseAppearanceSlotType? selectedBaseSlotType,
                bool layerModificationWorkflowActive,
                CharacterRenderFixPatch? workingRenderFixPatch)
            {
                Document = document.Clone();
                SelectedLayerIndex = selectedLayerIndex;
                SelectedLayerIndices = new HashSet<int>(selectedLayerIndices);
                SelectedEquipmentIndex = selectedEquipmentIndex;
                SelectedNodePath = selectedNodePath ?? string.Empty;
                SelectedBaseSlotType = selectedBaseSlotType;
                LayerModificationWorkflowActive = layerModificationWorkflowActive;
                WorkingRenderFixPatch = workingRenderFixPatch?.Clone();
            }
        }

        private readonly Stack<Snapshot> undoStack = new Stack<Snapshot>();
        private readonly Stack<Snapshot> redoStack = new Stack<Snapshot>();
        private readonly int maxDepth;

        public EditorHistory(int maxDepth = 50)
        {
            this.maxDepth = maxDepth < 1 ? 1 : maxDepth;
        }

        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
        }

        public void PushUndo(
            CharacterDesignDocument currentDocument,
            int selectedLayerIndex,
            HashSet<int> selectedLayerIndices,
            int selectedEquipmentIndex,
            string selectedNodePath,
            BaseAppearanceSlotType? selectedBaseSlotType,
            bool layerModificationWorkflowActive,
            CharacterRenderFixPatch? workingRenderFixPatch)
        {
            undoStack.Push(new Snapshot(
                currentDocument,
                selectedLayerIndex,
                selectedLayerIndices,
                selectedEquipmentIndex,
                selectedNodePath,
                selectedBaseSlotType,
                layerModificationWorkflowActive,
                workingRenderFixPatch));
            TrimUndoDepth();
            redoStack.Clear();
        }

        public bool TryUndo(
            CharacterDesignDocument currentDocument,
            int selectedLayerIndex,
            HashSet<int> selectedLayerIndices,
            int selectedEquipmentIndex,
            string selectedNodePath,
            BaseAppearanceSlotType? selectedBaseSlotType,
            bool layerModificationWorkflowActive,
            CharacterRenderFixPatch? workingRenderFixPatch,
            out Snapshot? snapshot)
        {
            if (undoStack.Count == 0)
            {
                snapshot = null;
                return false;
            }

            redoStack.Push(new Snapshot(
                currentDocument,
                selectedLayerIndex,
                selectedLayerIndices,
                selectedEquipmentIndex,
                selectedNodePath,
                selectedBaseSlotType,
                layerModificationWorkflowActive,
                workingRenderFixPatch));
            snapshot = undoStack.Pop();
            return true;
        }

        public bool TryRedo(
            CharacterDesignDocument currentDocument,
            int selectedLayerIndex,
            HashSet<int> selectedLayerIndices,
            int selectedEquipmentIndex,
            string selectedNodePath,
            BaseAppearanceSlotType? selectedBaseSlotType,
            bool layerModificationWorkflowActive,
            CharacterRenderFixPatch? workingRenderFixPatch,
            out Snapshot? snapshot)
        {
            if (redoStack.Count == 0)
            {
                snapshot = null;
                return false;
            }

            undoStack.Push(new Snapshot(
                currentDocument,
                selectedLayerIndex,
                selectedLayerIndices,
                selectedEquipmentIndex,
                selectedNodePath,
                selectedBaseSlotType,
                layerModificationWorkflowActive,
                workingRenderFixPatch));
            snapshot = redoStack.Pop();
            return true;
        }

        private void TrimUndoDepth()
        {
            if (undoStack.Count <= maxDepth)
            {
                return;
            }

            // Stack 无法直接移除底部元素，重建一次（历史较小时开销可接受）
            var temp = undoStack.ToArray();
            undoStack.Clear();
            for (int i = maxDepth - 1; i >= 0; i--)
            {
                undoStack.Push(temp[i]);
            }
        }
    }
}
