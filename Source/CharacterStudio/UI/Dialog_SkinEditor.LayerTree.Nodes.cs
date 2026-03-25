using System.Collections.Generic;
using CharacterStudio.Core;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 图层树 - 渲染节点
        // ─────────────────────────────────────────────

        /// <summary>
        /// 绘制渲染节点行（递归）
        /// </summary>
        private float DrawNodeRow(RenderNodeSnapshot node, int indent, float y, float width, ref int rowIndex)
        {
            Rect rowRect = new Rect(0, y, width, TreeNodeHeight);

            UIHelper.DrawAlternatingRowBackground(rowRect, rowIndex);
            rowIndex++;

            float indentOffset = indent * TreeIndentWidth;

            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }

            if (selectedNodePath == node.uniqueNodePath)
            {
                Widgets.DrawBox(rowRect, 1);
            }

            bool hasChildren = node.children.Count > 0;
            bool isExpanded = expandedPaths.Contains(node.uniqueNodePath);

            Rect foldRect = new Rect(indentOffset, y + 2, 18, 18);
            if (hasChildren)
            {
                string foldIcon = isExpanded ? "▼" : "▶";
                if (Widgets.ButtonText(foldRect, foldIcon, false))
                {
                    if (isExpanded)
                    {
                        expandedPaths.Remove(node.uniqueNodePath);
                    }
                    else
                    {
                        expandedPaths.Add(node.uniqueNodePath);
                    }
                }
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(foldRect, "  •");
                GUI.color = Color.white;
            }

            bool isHidden = IsNodeHiddenInCurrentMode(node);

            Rect eyeRect = new Rect(indentOffset + 20, y + 2, 18, 18);
            string eyeIcon = isHidden ? "◯" : "◉";
            GUI.color = isHidden ? Color.gray : Color.white;
            if (Widgets.ButtonText(eyeRect, eyeIcon, false))
            {
                ToggleNodeVisibilityInCurrentMode(node);
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(eyeRect, isHidden ? "CS_Studio_Tip_ShowNode".Translate() : "CS_Studio_Tip_HideNode".Translate());

            Rect nameRect = new Rect(indentOffset + 42, y, width - indentOffset - 44, TreeNodeHeight);
            string displayName = GetNodeDisplayName(node);

            GUI.color = GetNodeColor(node);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, displayName);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(rowRect))
            {
                BaseAppearanceSlotType? baseSlotType = TryResolveBaseSlotType(node);
                if (baseSlotType != null)
                {
                    selectedBaseSlotType = baseSlotType.Value;
                    selectedNodePath = "";
                    currentTab = EditorTab.BaseAppearance;
                }
                else
                {
                    selectedNodePath = node.uniqueNodePath;
                    selectedBaseSlotType = null;
                }

                selectedLayerIndex = -1;
                selectedLayerIndices.Clear();
            }

            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                ShowNodeContextMenu(node);
                Event.current.Use();
            }

            TooltipHandler.TipRegion(rowRect, "CS_Studio_Node_Tooltip".Translate(node.uniqueNodePath, node.workerClass, node.texPath ?? "CS_Studio_None".Translate()));

            y += TreeNodeHeight;

            if (isExpanded && hasChildren)
            {
                foreach (var child in node.children)
                {
                    y = DrawNodeRow(child, indent + 1, y, width, ref rowIndex);
                }
            }

            return y;
        }

        /// <summary>
        /// 切换节点可见性
        /// </summary>
        private void ToggleNodeVisibility(RenderNodeSnapshot node)
        {
            ToggleNodeVisibilityInCurrentMode(node);
        }

        /// <summary>
        /// 显示节点右键菜单
        /// </summary>
        private void ShowNodeContextMenu(RenderNodeSnapshot node)
        {
            var options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption("CS_Studio_Ctx_CopyPath".Translate(), () =>
            {
                GUIUtility.systemCopyBuffer = node.uniqueNodePath;
                ShowStatus("CS_Studio_Msg_Copied".Translate(node.uniqueNodePath));
            }));

            if (!string.IsNullOrEmpty(node.texPath))
            {
                options.Add(new FloatMenuOption("CS_Studio_Ctx_CopyTexPath".Translate(), () =>
                {
                    GUIUtility.systemCopyBuffer = node.texPath;
                    ShowStatus("CS_Studio_Msg_Copied".Translate(node.texPath));
                }));
            }

            if (!layerModificationWorkflowActive)
            {
                options.Add(new FloatMenuOption("CS_Studio_Prop_MountLayer".Translate(), () =>
                {
                    var newLayer = new PawnLayerConfig
                    {
                        layerName = GetMountedLayerLabel(node),
                        anchorPath = node.uniqueNodePath,
                        anchorTag = node.tagDefName ?? "Body"
                    };
                    workingSkin.layers.Add(newLayer);
                    AppendAttachNodeRule(node, newLayer);
                    selectedLayerIndex = workingSkin.layers.Count - 1;
                    selectedNodePath = "";
                    isDirty = true;
                    RefreshPreview();
                    ShowStatus("CS_Studio_Msg_Appended".Translate(node.uniqueNodePath, "1"));
                }));
            }

            bool isHidden = IsNodeHiddenInCurrentMode(node);

            options.Add(new FloatMenuOption(isHidden ? "CS_Studio_Prop_ShowNode".Translate() : "CS_Studio_Prop_HideNode".Translate(), () =>
            {
                ToggleNodeVisibilityInCurrentMode(node);
            }));

            if (node.children.Count > 0)
            {
                if (expandedPaths.Contains(node.uniqueNodePath))
                {
                    options.Add(new FloatMenuOption("CS_Studio_Ctx_CollapseChildren".Translate(), () =>
                    {
                        expandedPaths.Remove(node.uniqueNodePath);
                    }));
                }
                else
                {
                    options.Add(new FloatMenuOption("CS_Studio_Ctx_ExpandChildren".Translate(), () =>
                    {
                        expandedPaths.Add(node.uniqueNodePath);
                    }));
                }
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
