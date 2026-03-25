using System;
using System.Linq;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 属性面板 - 节点属性
        // ─────────────────────────────────────────────

        /// <summary>
        /// 绘制节点属性面板
        /// </summary>
        private void DrawNodeProperties(Rect rect)
        {
            var node = FindNodeByPath(cachedRootSnapshot!, selectedNodePath);
            if (node == null)
            {
                selectedNodePath = "";
                return;
            }

            float propsY = GetPropertiesContentTop(rect);
            float propsHeight = rect.height - propsY + rect.y - Margin;
            Rect propsRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2, propsHeight);
            Rect viewRect = new Rect(0, 0, propsRect.width - 16, 820);

            Widgets.BeginScrollView(propsRect, ref propsScrollPos, viewRect);

            float y = 0;
            Text.Font = GameFont.Small;
            float labelWidth = new[]
            {
                "CS_Studio_Info_Path",
                "CS_Studio_Prop_NodeTag",
                "CS_Studio_Info_Worker",
                "CS_Studio_Prop_NodeTexture",
                "CS_Studio_Prop_NodeChildren"
            }.Max(k => Text.CalcSize(k.Translate()).x) + 10f;
            float fieldWidth = propsRect.width - labelWidth - 20;

            if (DrawCollapsibleSection(ref y, propsRect.width - 20, "CS_Studio_Prop_NodeInfo".Translate(), "NodeInfo"))
            {
                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Info_Path".Translate());
                Rect pathRect = new Rect(labelWidth, y, fieldWidth - 32, 24);
                Text.Font = GameFont.Tiny;
                Widgets.Label(pathRect, node.uniqueNodePath);
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(labelWidth + fieldWidth - 28, y, 28, 22), "📋"))
                {
                    GUIUtility.systemCopyBuffer = node.uniqueNodePath;
                    ShowStatus("CS_Studio_Msg_PathCopied".Translate());
                }
                y += 26;

                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Prop_NodeTag".Translate() + ":");
                Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), node.tagDefName ?? "CS_Studio_None".Translate());
                y += 26;

                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Info_Worker".Translate());
                string shortWorker = node.workerClass;
                if (shortWorker.Contains("."))
                {
                    shortWorker = shortWorker.Substring(shortWorker.LastIndexOf('.') + 1);
                }
                Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), shortWorker);
                y += 26;

                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Prop_NodeTexture".Translate() + ":");
                if (!string.IsNullOrEmpty(node.texPath))
                {
                    Rect texPathRect = new Rect(labelWidth, y, fieldWidth - 32, 24);
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(texPathRect, node.texPath);
                    Text.Font = GameFont.Small;
                    if (Widgets.ButtonText(new Rect(labelWidth + fieldWidth - 28, y, 28, 22), "📋"))
                    {
                        GUIUtility.systemCopyBuffer = node.texPath;
                        ShowStatus("CS_Studio_Msg_TexCopied".Translate());
                    }
                }
                else
                {
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), "CS_Studio_None".Translate());
                    GUI.color = Color.white;
                }
                y += 26;

                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Prop_NodeChildren".Translate() + ":");
                Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), node.children.Count.ToString());
                y += 30;
            }

            if (layerModificationWorkflowActive && DrawCollapsibleSection(ref y, propsRect.width - 20, "图层修改补丁".Translate(), "NodePatch"))
            {
                bool isHidden = IsNodeHiddenInCurrentMode(node);
                if (Widgets.ButtonText(new Rect(0, y, propsRect.width - 20, 28), isHidden ? "从补丁中显示节点" : "加入补丁隐藏节点"))
                {
                    ToggleNodeVisibilityInCurrentMode(node);
                }
                y += 32;

                float patchOffset = GetNodePatchDrawOrderOffset(node.uniqueNodePath);
                Widgets.Label(new Rect(0, y, labelWidth, 24), "DrawOrder Offset");
                string patchOffsetBuffer = patchOffset.ToString("F2");
                string newOffsetText = Widgets.TextField(new Rect(labelWidth, y, fieldWidth, 24), patchOffsetBuffer);
                if (float.TryParse(newOffsetText, out float parsedOffset) && Math.Abs(parsedOffset - patchOffset) > 0.0001f)
                {
                    SetNodePatchDrawOrderOffset(node.uniqueNodePath, parsedOffset);
                    ShowStatus($"已设置节点补丁层级偏移：{node.uniqueNodePath} => {parsedOffset:F2}");
                    RefreshPreview();
                    RefreshRenderTree();
                }
                y += 30;

                float buttonWidth = (propsRect.width - 32f) / 4f;
                if (Widgets.ButtonText(new Rect(0f, y, buttonWidth, 26f), "+1"))
                {
                    SetNodePatchDrawOrderOffset(node.uniqueNodePath, patchOffset + 1f);
                    RefreshPreview();
                    RefreshRenderTree();
                }
                if (Widgets.ButtonText(new Rect(buttonWidth + 4f, y, buttonWidth, 26f), "-1"))
                {
                    SetNodePatchDrawOrderOffset(node.uniqueNodePath, patchOffset - 1f);
                    RefreshPreview();
                    RefreshRenderTree();
                }
                if (Widgets.ButtonText(new Rect((buttonWidth + 4f) * 2f, y, buttonWidth, 26f), "+5"))
                {
                    SetNodePatchDrawOrderOffset(node.uniqueNodePath, patchOffset + 5f);
                    RefreshPreview();
                    RefreshRenderTree();
                }
                if (Widgets.ButtonText(new Rect((buttonWidth + 4f) * 3f, y, buttonWidth, 26f), "清除"))
                {
                    SetNodePatchDrawOrderOffset(node.uniqueNodePath, 0f);
                    RefreshPreview();
                    RefreshRenderTree();
                }
                y += 34;
            }

            if (DrawCollapsibleSection(ref y, propsRect.width - 20, "CS_Studio_Prop_Actions".Translate(), "NodeActions"))
            {
                bool isHidden = IsNodeHiddenInCurrentMode(node);

                if (Widgets.ButtonText(new Rect(0, y, propsRect.width - 20, 28), isHidden ? "CS_Studio_Prop_ShowNode".Translate() : "CS_Studio_Prop_HideNode".Translate()))
                {
                    ToggleNodeVisibilityInCurrentMode(node);
                }
                y += 32;

                if (!layerModificationWorkflowActive)
                {
                    if (Widgets.ButtonText(new Rect(0, y, propsRect.width - 20, 28), "CS_Studio_Prop_MountLayer".Translate()))
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
                    }
                    y += 32;
                }
            }

            if (DrawCollapsibleSection(ref y, propsRect.width - 20, "CS_Studio_Node_RuntimeData".Translate(), "NodeRuntime"))
            {
                if (node.runtimeDataValid)
                {
                    bool offsetNonDefault = node.runtimeOffset != Vector3.zero;
                    GUI.color = offsetNonDefault ? Color.yellow : Color.white;
                    Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Runtime_Offset".Translate());
                    Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24),
                        $"({node.runtimeOffset.x:F3}, {node.runtimeOffset.y:F3}, {node.runtimeOffset.z:F3})");
                    GUI.color = Color.white;
                    y += 24;

                    bool scaleNonDefault = node.runtimeScale != Vector3.one;
                    GUI.color = scaleNonDefault ? Color.yellow : Color.white;
                    Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Runtime_Scale".Translate());
                    Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24),
                        $"({node.runtimeScale.x:F3}, {node.runtimeScale.y:F3}, {node.runtimeScale.z:F3})");
                    GUI.color = Color.white;
                    y += 24;

                    bool colorNonDefault = node.runtimeColor != Color.white;
                    GUI.color = colorNonDefault ? Color.yellow : Color.white;
                    Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Runtime_Color".Translate());
                    Rect colorPreviewRect = new Rect(labelWidth, y + 2, 20, 20);
                    Widgets.DrawBoxSolid(colorPreviewRect, node.runtimeColor);
                    Widgets.DrawBox(colorPreviewRect, 1);
                    Widgets.Label(new Rect(labelWidth + 25, y, fieldWidth - 25, 24),
                        "CS_Studio_Runtime_ColorValue".Translate(node.runtimeColor.r.ToString("F2"), node.runtimeColor.g.ToString("F2"), node.runtimeColor.b.ToString("F2"), node.runtimeColor.a.ToString("F2")));
                    GUI.color = Color.white;
                    y += 24;

                    bool rotNonDefault = Mathf.Abs(node.runtimeRotation) > 0.01f;
                    GUI.color = rotNonDefault ? Color.yellow : Color.white;
                    Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Runtime_Rotation".Translate());
                    Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), "CS_Studio_Runtime_RotationValue".Translate(node.runtimeRotation.ToString("F1")));
                    GUI.color = Color.white;
                    y += 24;

                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    Widgets.Label(new Rect(0, y, propsRect.width - 20, 16), "CS_Studio_Node_RuntimeNote".Translate());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    y += 20;
                }
                else
                {
                    GUI.color = Color.gray;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(0, y, propsRect.width - 20, 16), "CS_Studio_Node_NoRuntimeData".Translate());
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                    y += 20;
                }
            }

            Widgets.EndScrollView();
        }
    }
}
