using System;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void DrawPropertiesHeader(Rect rect)
        {
            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, 26f);
            Widgets.DrawBoxSolid(titleRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(titleRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            string title = layerModificationWorkflowActive ? "图层修改补丁属性" : "CS_Studio_Panel_Properties".Translate();
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 72f, titleRect.height), title);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

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
            Widgets.DrawBoxSolid(buttonRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(buttonRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(buttonRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            TooltipHandler.TipRegion(buttonRect, tooltip);
            if (Widgets.ButtonInvisible(buttonRect))
            {
                onClick();
                return true;
            }

            return false;
        }

        private void DrawActivePropertiesContent(Rect rect)
        {
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
