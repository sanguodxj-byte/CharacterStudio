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

            // 可见性开关 (菱形方块)
            Rect visRect = new Rect(2, y + 1, 24, 22);
            bool visible = layer.visible;
            GUI.color = visible ? new Color(0.37f, 0.82f, 1f) : new Color(0.4f, 0.45f, 0.5f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            if (Widgets.ButtonText(visRect, visible ? "◆" : "◇", false))
            {
                bool newVisible = !layer.visible;
                MutateWithUndo(() =>
                {
                    layer.visible = newVisible;
                    if (selectedLayerIndices.Contains(index))
                    {
                        ApplyToOtherSelectedLayers(index, l => l.visible = newVisible);
                    }
                });
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect deleteRect = new Rect(width - 28f, y, 24f, 22f);
            Rect nameRect = new Rect(32, y, width - 66f, TreeNodeHeight);
            string displayName = string.IsNullOrEmpty(layer.layerName) ? GetDefaultLayerLabel(index) : layer.layerName;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, displayName);
            Text.Anchor = TextAnchor.UpperLeft;

            if (UIHelper.DrawDangerButton(deleteRect, tooltip: "CS_Studio_Delete".Translate(), onClick: () => DeleteSelectedLayers(index)))
            {
                return y + TreeNodeHeight;
            }

            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 0
                && !deleteRect.Contains(Event.current.mousePosition) && !visRect.Contains(Event.current.mousePosition))
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
                MutateWithUndo(() =>
                {
                    var copy = layer.Clone();
                    copy.layerName += " (Copy)";
                    workingSkin.layers.Insert(index + 1, copy);
                    selectedLayerIndex = index + 1;
                    selectedLayerIndices.Clear();
                    selectedLayerIndices.Add(selectedLayerIndex);
                });
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
                DeleteSelectedLayers(index);
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
