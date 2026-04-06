using System;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // BaseAppearance 左侧面板
        // ─────────────────────────────────────────────

        private const float BaseSlotRowHeight = 36f;
        private const float BaseSlotEditButtonWidth = 30f;

        private void DrawBaseAppearancePanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

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
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), "CS_Studio_Tab_BaseAppearance".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            Rect summaryRect = new Rect(rect.x + Margin, titleRect.yMax + 6f, rect.width - Margin * 2, 24f);
            Widgets.DrawBoxSolid(summaryRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(summaryRect, 1);
            GUI.color = Color.white;

            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            workingSkin.baseAppearance.EnsureAllSlotsExist();
            int enabledCount = 0;
            foreach (var slot in workingSkin.baseAppearance.slots)
            {
                if (slot != null && slot.enabled)
                {
                    enabledCount++;
                }
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(summaryRect.x + 8f, summaryRect.y, summaryRect.width - 16f, summaryRect.height), $"已启用槽位：{enabledCount}/{workingSkin.baseAppearance.slots.Count}");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float contentY = summaryRect.yMax + 8f;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            Widgets.DrawBoxSolid(contentRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(contentRect, 1);
            GUI.color = Color.white;

            Rect scrollRect = contentRect.ContractedBy(2f);
            float viewHeight = Mathf.Max(scrollRect.height, workingSkin.baseAppearance.slots.Count * BaseSlotRowHeight + 8f);
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(0f, scrollRect.width - 18f), viewHeight);
            Widgets.BeginScrollView(scrollRect, ref baseScrollPos, viewRect);

            float y = 2f;
            int rowIndex = 0;
            foreach (BaseAppearanceSlotType slotType in Enum.GetValues(typeof(BaseAppearanceSlotType)))
            {
                y = DrawBaseSlotRow(workingSkin.baseAppearance.GetSlot(slotType), y, viewRect.width, rowIndex++);
            }

            Widgets.EndScrollView();
        }

        private float DrawBaseSlotRow(BaseAppearanceSlotConfig slot, float y, float width, int rowIndex)
        {
            Rect rowRect = new Rect(0f, y, width, BaseSlotRowHeight);
            UIHelper.DrawAlternatingRowBackground(rowRect, rowIndex);

            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }

            if (selectedBaseSlotType == slot.slotType)
            {
                GUI.color = UIHelper.BorderColor;
                Widgets.DrawBox(rowRect, 1);
                GUI.color = Color.white;
            }

            Rect statusRect = new Rect(4f, y + 8f, 18f, 18f);
            bool enabled = slot.enabled;
            GUI.color = enabled ? new Color(0.55f, 0.95f, 1f) : Color.gray;
            Widgets.Label(statusRect, enabled ? "◆" : "◇");
            GUI.color = Color.white;

            Rect toggleRect = new Rect(24f, y + 8f, 18f, 18f);
            GUI.color = enabled ? Color.white : Color.gray;
            if (Widgets.ButtonText(toggleRect, enabled ? "◉" : "◯", false))
            {
                bool newEnabled = !slot.enabled;
                MutateWithUndo(() => slot.enabled = newEnabled, refreshPreview: true, refreshRenderTree: true);
            }
            GUI.color = Color.white;

            float textX = 46f;
            float editX = width - BaseSlotEditButtonWidth - 4f;
            float textWidth = Mathf.Max(64f, editX - textX - 6f);
            Rect nameRect = new Rect(textX, y + 3f, textWidth, 16f);
            Rect metaRect = new Rect(textX, y + 18f, textWidth, 14f);
            string slotLabel = BaseAppearanceUtility.GetDisplayName(slot.slotType);
            string summary = string.IsNullOrEmpty(slot.texPath) ? "CS_Studio_BaseSlot_Unset".Translate() : System.IO.Path.GetFileName(slot.texPath);

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Small;
            GUI.color = enabled ? Color.white : Color.gray;
            Widgets.Label(nameRect, slotLabel);

            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(metaRect, summary);
            GUI.color = Color.white;
            Text.Font = oldFont;

            Rect editRect = new Rect(editX, y + 7f, BaseSlotEditButtonWidth, 22f);
            Widgets.DrawBoxSolid(editRect, selectedBaseSlotType == slot.slotType ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(editRect.x, editRect.yMax - 2f, editRect.width, 2f), selectedBaseSlotType == slot.slotType ? UIHelper.AccentColor : new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(editRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(editRect, 1);
            GUI.color = Color.white;

            GameFont buttonOldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = selectedBaseSlotType == slot.slotType ? Color.white : UIHelper.HeaderColor;
            Widgets.Label(editRect, "···");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = buttonOldFont;

            if (Widgets.ButtonInvisible(editRect))
            {
                selectedBaseSlotType = slot.slotType;
                selectedNodePath = "";
                selectedLayerIndex = -1;
                selectedLayerIndices.Clear();
            }

            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                selectedBaseSlotType = slot.slotType;
                selectedNodePath = "";
                selectedLayerIndex = -1;
                selectedLayerIndices.Clear();
                Event.current.Use();
            }

            return y + rowRect.height;
        }
    }
}
