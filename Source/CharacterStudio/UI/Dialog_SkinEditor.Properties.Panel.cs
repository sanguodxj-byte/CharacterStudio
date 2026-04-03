using System;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void DrawPropertiesHeader(Rect rect)
        {
            string title = layerModificationWorkflowActive ? "图层修改补丁属性" : "CS_Studio_Panel_Properties".Translate();
            Rect titleRect = UIHelper.DrawPanelShell(rect, title, Margin, 72f);

            float expandBtnWidth = 24f;
            DrawPropertiesHeaderButton(new Rect(rect.x + rect.width - Margin - expandBtnWidth * 2 - 4, rect.y + Margin + 1f, expandBtnWidth, 24f), "+", "CS_Studio_Tip_ExpandAll".Translate(), () =>
            {
                collapsedSections.Clear();
            });

            DrawPropertiesHeaderButton(new Rect(rect.x + rect.width - Margin - expandBtnWidth, rect.y + Margin + 1f, expandBtnWidth, 24f), "-", "CS_Studio_Tip_CollapseAll".Translate(), () =>
            {
                var allSections = new[]
                {
                    "Context",
                    "Base",
                    "Transform",
                    "EastOffset",
                    "NorthOffset",
                    "Advanced",
                    "Misc",
                    "Variants",
                    "Animation",
                    "Tools",
                    "HideVanilla",
                    "NodeInfo",
                    "NodePatch",
                    "NodeActions",
                    "NodeRuntime"
                };
                foreach (var s in allSections)
                {
                    collapsedSections.Add(s);
                }
            });
        }

        private bool DrawPropertiesHeaderButton(Rect buttonRect, string label, string tooltip, Action onClick)
        {
            if (UIHelper.DrawToolbarButton(buttonRect, label, tooltip: tooltip))
            {
                onClick();
                return true;
            }

            return false;
        }

        private void DrawActivePropertiesContent(Rect rect)
        {
            float propsY = GetPropertiesContentTop(rect);
            Rect summaryRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2f, 34f);
            UIHelper.DrawContentCard(summaryRect);

            string summaryText;
            if (!string.IsNullOrEmpty(selectedNodePath) && cachedRootSnapshot != null)
            {
                summaryText = "CS_Studio_Properties_Summary_Node".Translate();
            }
            else if (selectedBaseSlotType != null)
            {
                summaryText = "CS_Studio_Properties_Summary_BaseSlot".Translate();
            }
            else if (!layerModificationWorkflowActive &&
                currentTab == EditorTab.Equipment &&
                selectedEquipmentIndex >= 0 &&
                selectedEquipmentIndex < (workingSkin.equipments?.Count ?? 0))
            {
                summaryText = "CS_Studio_Properties_Summary_Equipment".Translate();
            }
            else
            {
                summaryText = "CS_Studio_Properties_Summary_Layer".Translate();
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(summaryRect.x + 8f, summaryRect.y, summaryRect.width - 16f, summaryRect.height), summaryText);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (!string.IsNullOrEmpty(selectedNodePath) && cachedRootSnapshot != null)
            {
                DrawNodeProperties(rect);
                return;
            }

            if (selectedBaseSlotType != null)
            {
                DrawBaseAppearanceProperties(rect, selectedBaseSlotType.Value);
                return;
            }

            SanitizeEquipmentSelection();
            if (!layerModificationWorkflowActive &&
                currentTab == EditorTab.Equipment &&
                selectedEquipmentIndex >= 0 &&
                selectedEquipmentIndex < (workingSkin.equipments?.Count ?? 0))
            {
                DrawEquipmentProperties(rect);
                return;
            }

            DrawSelectedLayerProperties(rect);
        }
    }
}
