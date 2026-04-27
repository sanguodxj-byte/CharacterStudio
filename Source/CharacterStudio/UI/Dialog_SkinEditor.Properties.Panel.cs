using System;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void DrawPropertiesHeader(Rect rect)
        {
            string title = layerModificationWorkflowActive ? "CS_Studio_Patch_Title".Translate() : "CS_Studio_Panel_Properties".Translate();
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
                currentTab == EditorTab.Items &&
                selectedEquipmentIndex >= 0 &&
                selectedEquipmentIndex < (WorkingEquipments?.Count ?? 0))
            {
                DrawEquipmentProperties(rect, equipmentMode: false);
                return;
            }

            if (!layerModificationWorkflowActive && currentTab == EditorTab.Equipment)
            {
                if (selectedEquipmentIndex >= 0 &&
                    selectedEquipmentIndex < (WorkingEquipments?.Count ?? 0))
                {
                    DrawEquipmentProperties(rect, equipmentMode: true);
                }
                else
                {
                    DrawAnimationProperties(rect);
                }
                return;
            }

            DrawSelectedLayerProperties(rect);
        }
    }
}