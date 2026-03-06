using System.Collections.Generic;
using CharacterStudio.Core;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 编辑器历史记录（Undo/Redo）
    /// 基于 PawnSkinDef 深拷贝快照。
    /// </summary>
    public class EditorHistory
    {
        public sealed class Snapshot
        {
            public PawnSkinDef Skin { get; }
            public int SelectedLayerIndex { get; }
            public HashSet<int> SelectedLayerIndices { get; }

            public Snapshot(PawnSkinDef skin, int selectedLayerIndex, HashSet<int> selectedLayerIndices)
            {
                Skin = skin.Clone();
                SelectedLayerIndex = selectedLayerIndex;
                SelectedLayerIndices = new HashSet<int>(selectedLayerIndices);
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

        public void PushUndo(PawnSkinDef currentSkin, int selectedLayerIndex, HashSet<int> selectedLayerIndices)
        {
            undoStack.Push(new Snapshot(currentSkin, selectedLayerIndex, selectedLayerIndices));
            TrimUndoDepth();
            redoStack.Clear();
        }

        public bool TryUndo(PawnSkinDef currentSkin, int selectedLayerIndex, HashSet<int> selectedLayerIndices, out Snapshot? snapshot)
        {
            if (undoStack.Count == 0)
            {
                snapshot = null;
                return false;
            }

            redoStack.Push(new Snapshot(currentSkin, selectedLayerIndex, selectedLayerIndices));
            snapshot = undoStack.Pop();
            return true;
        }

        public bool TryRedo(PawnSkinDef currentSkin, int selectedLayerIndex, HashSet<int> selectedLayerIndices, out Snapshot? snapshot)
        {
            if (redoStack.Count == 0)
            {
                snapshot = null;
                return false;
            }

            undoStack.Push(new Snapshot(currentSkin, selectedLayerIndex, selectedLayerIndices));
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
