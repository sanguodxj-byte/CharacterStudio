using System.Collections.Generic;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public class Dialog_RenderTreeInspector : Window
    {
        private Pawn pawn;
        private RenderNodeSnapshot? rootNode;
        private Vector2 scrollPos;
        private RenderNodeSnapshot? selectedNode;

        public override Vector2 InitialSize => new Vector2(800f, 600f);

        public Dialog_RenderTreeInspector(Pawn pawn)
        {
            this.pawn = pawn;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.draggable = true;
            this.resizeable = true;
            
            RefreshTree();
        }

        private void RefreshTree()
        {
            if (pawn != null && pawn.Drawer != null && pawn.Drawer.renderer != null)
            {
                // 使用 RenderTreeParser 解析当前渲染树
                rootNode = RenderTreeParser.Capture(pawn);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            UIHelper.DrawDialogFrame(inRect, this);

            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Rect titleRect = UIHelper.DrawPanelShell(shellRect, "CS_Studio_Inspector_Title".Translate(pawn.Name.ToStringShort), 0f);

            float y = titleRect.yMax + 8f;

            if (UIHelper.DrawToolbarButton(new Rect(0f, y, 120f, 26f), "CS_Studio_Inspector_Refresh".Translate(), accent: true))
            {
                RefreshTree();
            }
            y += 34f;

            // 分割视图：左侧树状图，右侧详情
            float splitX = inRect.width * 0.4f;
            Rect treeRect = new Rect(0, y, splitX - 5, inRect.height - y - 10);
            Rect detailRect = new Rect(splitX + 5, y, inRect.width - splitX - 5, inRect.height - y - 10);

            // 绘制左侧树
            UIHelper.DrawContentCard(treeRect);
            float treeHeight = rootNode != null ? CalculateTreeHeight(rootNode) : 0f;
            Widgets.BeginScrollView(treeRect.ContractedBy(2f), ref scrollPos, new Rect(0, 0, treeRect.width - 20, treeHeight));
            
            if (rootNode != null)
            {
                float curY = 0;
                DrawNodeRecursive(rootNode, 0, ref curY, treeRect.width - 16);
            }
            else
            {
                Widgets.Label(new Rect(5, 5, treeRect.width, 24), "CS_Studio_Inspector_NoDataAvailable".Translate());
            }

            Widgets.EndScrollView();

            // 绘制右侧详情
            UIHelper.DrawContentCard(detailRect);
            if (selectedNode != null)
            {
                DrawNodeDetails(detailRect.ContractedBy(10), selectedNode);
            }
            else
            {
                Widgets.Label(detailRect.ContractedBy(10), "CS_Studio_Inspector_SelectNodePrompt".Translate());
            }
        }

        private float CalculateTreeHeight(RenderNodeSnapshot node)
        {
            if (node == null) return 0;
            float height = 24f;
            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    height += CalculateTreeHeight(child);
                }
            }
            return height;
        }

        private void DrawNodeRecursive(RenderNodeSnapshot node, int indent, ref float curY, float width)
        {
            Rect rowRect = new Rect(0, curY, width, 24f);
            
            // 选中高亮
            if (selectedNode == node)
            {
                Widgets.DrawHighlightSelected(rowRect);
            }
            else
            {
                Widgets.DrawHighlightIfMouseover(rowRect);
            }

            // 点击选择
            if (Widgets.ButtonInvisible(rowRect))
            {
                selectedNode = node;
            }

            // 绘制节点信息
            float indentPixels = indent * 15f;
            string label = node.debugLabel ?? "CS_Studio_Inspector_UnknownNode".Translate();
            Widgets.Label(new Rect(indentPixels + 5, curY, width - indentPixels - 5, 24f), label);

            curY += 24f;

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    DrawNodeRecursive(child, indent + 1, ref curY, width);
                }
            }
        }

        private void DrawNodeDetails(Rect rect, RenderNodeSnapshot node)
        {
            float y = rect.y;
            float width = rect.width;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, width, 30), node.debugLabel);
            Text.Font = GameFont.Small;
            y += 35;

            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Inspector_Field_Type".Translate(), node.workerClass);
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Inspector_Field_Graphic".Translate(), node.texPath ?? "CS_Studio_None".Translate());
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Inspector_Field_Color".Translate(), node.color.ToString());
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Inspector_Field_Visible".Translate(), node.isVisible ? "CS_Studio_UI_On".Translate() : "CS_Studio_UI_Off".Translate());
            
            // 注意：RenderNodeSnapshot 没有 Tags 属性，可能是 tagDefName
            // 原代码可能想显示 TagDef
            UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Inspector_Field_Tag".Translate(), string.IsNullOrWhiteSpace(node.tagDefName) ? "CS_Studio_None".Translate() : node.tagDefName);
        }
    }
}
