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

        private const float BaseSlotRowHeight = 40f;
        private const float BaseSlotEditButtonWidth = 32f;
        private string baseAppearanceGlobalScaleBuffer = "1.000";

        private static readonly BaseAppearanceSlotType[] CachedBaseAppearanceSlotTypes =
            (BaseAppearanceSlotType[])Enum.GetValues(typeof(BaseAppearanceSlotType));

        private void DrawBaseAppearancePanel(Rect rect)
        {
            Rect titleRect = UIHelper.DrawPanelShell(rect, "CS_Studio_Tab_BaseAppearance".Translate(), Margin);

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
            Widgets.Label(new Rect(summaryRect.x + 8f, summaryRect.y, summaryRect.width - 16f, summaryRect.height), "CS_Studio_BaseSlot_EnabledCount".Translate(enabledCount, workingSkin.baseAppearance.slots.Count));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // 全局缩放区域美化
            Rect globalScaleRect = new Rect(rect.x + Margin, summaryRect.yMax + 6f, rect.width - Margin * 2, 48f);
            Widgets.DrawBoxSolid(globalScaleRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(globalScaleRect, 1);
            GUI.color = Color.white;

            float globalScale = Mathf.Clamp(workingSkin.globalTextureScale, 0.1f, 3f);
            Rect labelRect = new Rect(globalScaleRect.x + 8f, globalScaleRect.y + 4f, globalScaleRect.width - 16f, 18f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.SubtleColor;
            // 修复文本显示不全：增加高度并确保锚点正确
            Widgets.Label(labelRect, $"DrawSize / {"CS_Studio_Transform_GlobalScale".Translate()}");
            GUI.color = Color.white;

            Rect sliderRect = new Rect(globalScaleRect.x + 8f, globalScaleRect.y + 24f, globalScaleRect.width - 66f, 16f);
            float editedGlobalScale = Widgets.HorizontalSlider(sliderRect, globalScale, 0.1f, 3f, true, null, null, null, 0.001f);

            Rect valueRect = new Rect(globalScaleRect.xMax - 52f, globalScaleRect.y + 22f, 44f, 20f);
            baseAppearanceGlobalScaleBuffer = editedGlobalScale.ToString("F3");
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(valueRect, baseAppearanceGlobalScaleBuffer);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (Math.Abs(editedGlobalScale - workingSkin.globalTextureScale) > 0.0001f)
            {
                MutateWithUndo(() =>
                {
                    workingSkin.globalTextureScale = editedGlobalScale;
                    workingSkin.baseAppearance.drawSizeScale = editedGlobalScale;
                    workingSkin.baseAppearance.globalScale = editedGlobalScale;
                }, refreshPreview: true, refreshRenderTree: true);
            }

            float contentY = globalScaleRect.yMax + 8f;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            UIHelper.DrawContentCard(contentRect);

            Rect scrollRect = contentRect.ContractedBy(2f);
            float viewHeight = Mathf.Max(scrollRect.height, workingSkin.baseAppearance.slots.Count * BaseSlotRowHeight + 8f);
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(0f, scrollRect.width - 18f), viewHeight);
            Widgets.BeginScrollView(scrollRect, ref baseScrollPos, viewRect);

            float y = 2f;
            int rowIndex = 0;
            foreach (BaseAppearanceSlotType slotType in CachedBaseAppearanceSlotTypes)
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

            bool isSelected = selectedBaseSlotType == slot.slotType;
            if (isSelected)
            {
                GUI.color = UIHelper.AccentColor;
                Widgets.DrawBox(rowRect, 1);
                GUI.color = Color.white;
            }

            float iconSize = 28f;
            float iconY = y + (BaseSlotRowHeight - iconSize) / 2f;

            // 启用状态开关 (菱形方块)
            Rect toggleRect = new Rect(4f, iconY, iconSize, iconSize);
            bool enabled = slot.enabled;
            GUI.color = enabled ? new Color(0.37f, 0.82f, 1f) : new Color(0.4f, 0.45f, 0.5f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            if (Widgets.ButtonText(toggleRect, enabled ? "◆" : "◇", false))
            {
                bool newEnabled = !slot.enabled;
                MutateWithUndo(() => slot.enabled = newEnabled, refreshPreview: true, refreshRenderTree: true);
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(toggleRect, enabled ? "CS_Studio_BaseSlot_Disable".Translate() : "CS_Studio_BaseSlot_Enable".Translate());

            float textX = 38f;
            float editX = width - BaseSlotEditButtonWidth - 8f;
            float textWidth = Mathf.Max(64f, editX - textX - 6f);
            
            // 文字对齐美化
            Rect nameRect = new Rect(textX, y + 4f, textWidth, 20f);
            Rect metaRect = new Rect(textX, y + 22f, textWidth, 16f);
            string slotLabel = BaseAppearanceUtility.GetDisplayName(slot.slotType);
            string summary = string.IsNullOrEmpty(slot.texPath) ? $"({"CS_Studio_BaseSlot_Unset".Translate()})" : System.IO.Path.GetFileName(slot.texPath);

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Small;
            GUI.color = enabled ? Color.white : UIHelper.SubtleColor;
            Widgets.Label(nameRect, slotLabel);

            Text.Font = GameFont.Tiny;
            GUI.color = enabled ? UIHelper.SubtleColor : new Color(0.4f, 0.45f, 0.5f);
            Widgets.Label(metaRect, summary);
            GUI.color = Color.white;
            Text.Font = oldFont;

            // 编辑按钮美化
            Rect editRect = new Rect(editX, y + (BaseSlotRowHeight - 24f) / 2f, BaseSlotEditButtonWidth, 24f);
            bool isHovered = Mouse.IsOver(editRect);
            
            Widgets.DrawBoxSolid(editRect, isSelected ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(editRect.x, editRect.yMax - 2f, editRect.width, 2f), isSelected ? UIHelper.AccentColor : new Color(1f, 1f, 1f, 0.05f));
            GUI.color = isHovered ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(editRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = isSelected ? Color.white : UIHelper.HeaderColor;
            Widgets.Label(editRect, "···");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

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
