using System;
using System.Collections.Generic;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 图层树 - 面板 / 刷新 / 展开折叠
        // ─────────────────────────────────────────────

        private void DrawLayerPanel(Rect rect)
        {
            Rect titleRect = UIHelper.DrawPanelShell(rect, "CS_Studio_Panel_Layers".Translate(), Margin);

            float btnY = titleRect.yMax + 6f;
            float btnWidth = (rect.width - Margin * 8) / 7f;
            float btnHeight = Mathf.Max(ButtonHeight - 2f, 22f);

            UIHelper.DrawIconButton(new Rect(rect.x + Margin, btnY, btnWidth, btnHeight), "↻", "CS_Studio_Panel_RefreshTree".Translate(), RefreshRenderTree);
            UIHelper.DrawIconButton(new Rect(rect.x + Margin * 2 + btnWidth, btnY, btnWidth, btnHeight), "▼", "CS_Studio_Panel_ExpandAll".Translate(), ExpandAllNodes);
            UIHelper.DrawIconButton(new Rect(rect.x + Margin * 3 + btnWidth * 2, btnY, btnWidth, btnHeight), "▶", "CS_Studio_Panel_CollapseAll".Translate(), CollapseAllNodes);
            UIHelper.DrawIconButton(new Rect(rect.x + Margin * 4 + btnWidth * 3, btnY, btnWidth, btnHeight), "+", "CS_Studio_Layer_AddCustom".Translate(), OnAddLayer, true);
            UIHelper.DrawIconButton(new Rect(rect.x + Margin * 5 + btnWidth * 4, btnY, btnWidth, btnHeight), "-", "CS_Studio_Tip_RemoveLayer".Translate(), OnRemoveLayer);
            UIHelper.DrawIconButton(new Rect(rect.x + Margin * 6 + btnWidth * 5, btnY, btnWidth, btnHeight), "↑", "CS_Studio_Tip_MoveUp".Translate(), () => MoveSelectedLayerUp());
            UIHelper.DrawIconButton(new Rect(rect.x + Margin * 7 + btnWidth * 6, btnY, btnWidth, btnHeight), "↓", "CS_Studio_Tip_MoveDown".Translate(), () => MoveSelectedLayerDown());

            float listY = btnY + btnHeight + 8f;
            float listHeight = rect.height - listY + rect.y - Margin;

            Rect listRect = new Rect(rect.x + Margin, listY, rect.width - Margin * 2, listHeight);
            Widgets.DrawBoxSolid(listRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(listRect, 1);
            GUI.color = Color.white;

            if (cachedRootSnapshot == null)
            {
                RefreshRenderTree();
            }

            treeViewHeight = 0f;
            if (cachedRootSnapshot != null)
            {
                treeViewHeight = CalculateTreeHeight(cachedRootSnapshot);
            }

            treeViewHeight += workingSkin.layers.Count * TreeNodeHeight + 38f;

            Rect viewRect = new Rect(0, 0, listRect.width - 16, Mathf.Max(treeViewHeight, listRect.height - 6f));

            Widgets.BeginScrollView(listRect.ContractedBy(2f), ref layerScrollPos, viewRect);

            float currentY = 2f;
            Text.Font = GameFont.Tiny;

            if (cachedRootSnapshot != null)
            {
                int rowIndex = 0;
                currentY = DrawNodeRow(cachedRootSnapshot, 0, currentY, viewRect.width, ref rowIndex);
            }
            else
            {
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(6f, currentY, viewRect.width - 12f, TreeNodeHeight), "CS_Studio_Layer_NoTree".Translate());
                GUI.color = Color.white;
                currentY += TreeNodeHeight;
            }

            currentY += 8f;
            Widgets.DrawBoxSolid(new Rect(0f, currentY, viewRect.width, 1f), UIHelper.AccentSoftColor);
            currentY += 6f;

            Rect customHeaderRect = new Rect(0f, currentY, viewRect.width, 18f);
            Widgets.DrawBoxSolid(customHeaderRect, new Color(UIHelper.AccentColor.r, UIHelper.AccentColor.g, UIHelper.AccentColor.b, 0.10f));
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(6f, currentY, viewRect.width - 12f, 18f), "CS_Studio_Layer_CustomSection".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            currentY += 22f;

            for (int i = 0; i < workingSkin.layers.Count; i++)
            {
                currentY = DrawCustomLayerRow(workingSkin.layers[i], i, currentY, viewRect.width);
            }

            Text.Font = GameFont.Small;
            Widgets.EndScrollView();
        }

        /// <summary>
        /// 刷新渲染树快照
        /// </summary>
        private void RefreshRenderTree()
        {
            Pawn? pawn = targetPawn;
            if (pawn == null)
            {
                pawn = mannequin?.CurrentPawn;
            }

            if (pawn?.Drawer?.renderer?.renderTree != null)
            {
                cachedRootSnapshot = RenderTreeParser.Capture(pawn);
                ShowStatus("CS_Studio_Msg_TreeRefreshed".Translate());
            }
            else
            {
                cachedRootSnapshot = null;
                ShowStatus("CS_Studio_Msg_TreeError".Translate());
            }
        }

        /// <summary>
        /// 计算树视图总高度
        /// </summary>
        private float CalculateTreeHeight(RenderNodeSnapshot node)
        {
            float height = TreeNodeHeight;
            if (expandedPaths.Contains(node.uniqueNodePath) && node.children.Count > 0)
            {
                foreach (var child in node.children)
                {
                    height += CalculateTreeHeight(child);
                }
            }

            return height;
        }

        /// <summary>
        /// 展开全部节点
        /// </summary>
        private void ExpandAllNodes()
        {
            if (cachedRootSnapshot == null)
            {
                return;
            }

            CollectAllPaths(cachedRootSnapshot, expandedPaths);
        }

        /// <summary>
        /// 折叠全部节点
        /// </summary>
        private void CollapseAllNodes()
        {
            expandedPaths.Clear();
        }

        /// <summary>
        /// 递归收集所有路径
        /// </summary>
        private void CollectAllPaths(RenderNodeSnapshot node, HashSet<string> paths)
        {
            if (!string.IsNullOrEmpty(node.uniqueNodePath))
            {
                paths.Add(node.uniqueNodePath);
            }

            foreach (var child in node.children)
            {
                CollectAllPaths(child, paths);
            }
        }
    }
}