using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 图层树 - 自定义图层
        // ─────────────────────────────────────────────

        /// <summary>
        /// 绘制自定义图层行
        /// </summary>
        private float DrawCustomLayerRow(PawnLayerConfig layer, int index, float y, float width)
        {
            Rect rowRect = new Rect(0, y, width, TreeNodeHeight);

            UIHelper.DrawAlternatingRowBackground(rowRect, index);

            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }

            if (selectedLayerIndices.Contains(index) || selectedLayerIndex == index)
            {
                Widgets.DrawBox(rowRect, 1);
            }

            GUI.color = new Color(0.5f, 1f, 0.5f);
            Widgets.Label(new Rect(4, y + 2, 18, 18), "★");
            GUI.color = Color.white;

            Rect visRect = new Rect(24, y + 2, 18, 18);
            bool visible = layer.visible;
            string visIcon = visible ? "◉" : "◯";
            GUI.color = visible ? Color.white : Color.gray;
            if (Widgets.ButtonText(visRect, visIcon, false))
            {
                bool newVisible = !layer.visible;
                layer.visible = newVisible;
                if (selectedLayerIndices.Contains(index))
                {
                    ApplyToOtherSelectedLayers(index, l => l.visible = newVisible);
                }
                isDirty = true;
                RefreshPreview();
            }
            GUI.color = Color.white;

            Rect nameRect = new Rect(46, y, width - 70, TreeNodeHeight);
            string displayName = string.IsNullOrEmpty(layer.layerName) ? GetDefaultLayerLabel(index) : layer.layerName;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, displayName);
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonText(new Rect(width - 22, y + 1, 20, 20), "×"))
            {
                DeleteSelectedLayers(index);
            }

            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                HandleLayerRowLeftClick(index);
                selectedBaseSlotType = null;
                Event.current.Use();
            }

            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                ShowCustomLayerContextMenu(layer, index);
                Event.current.Use();
            }

            return y + TreeNodeHeight;
        }

        /// <summary>
        /// 显示自定义图层右键菜单
        /// </summary>
        private void ShowCustomLayerContextMenu(PawnLayerConfig layer, int index)
        {
            var options = new System.Collections.Generic.List<FloatMenuOption>();

            options.Add(new FloatMenuOption("CS_Studio_Ctx_CopyLayer".Translate(), () =>
            {
                CaptureUndoSnapshot();
                var copy = layer.Clone();
                copy.layerName += " (Copy)";
                workingSkin.layers.Insert(index + 1, copy);
                selectedLayerIndex = index + 1;
                selectedLayerIndices.Clear();
                selectedLayerIndices.Add(selectedLayerIndex);
                isDirty = true;
                RefreshPreview();
            }));

            if (index > 0)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveUp".Translate(), () =>
                {
                    selectedLayerIndex = index;
                    selectedLayerIndices.Clear();
                    selectedLayerIndices.Add(index);
                    MoveSelectedLayerUp();
                }));
            }

            if (index < workingSkin.layers.Count - 1)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveDown".Translate(), () =>
                {
                    selectedLayerIndex = index;
                    selectedLayerIndices.Clear();
                    selectedLayerIndices.Add(index);
                    MoveSelectedLayerDown();
                }));
            }

            options.Add(new FloatMenuOption("CS_Studio_Ctx_DeleteLayer".Translate(), () =>
            {
                CaptureUndoSnapshot();
                workingSkin.layers.RemoveAt(index);
                RemapSelectionAfterRemoveIndex(index);
                isDirty = true;
                RefreshPreview();
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}