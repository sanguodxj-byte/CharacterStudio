using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private int consecutiveRenderErrors = 0;

        // ─────────────────────────────────────────────
        // 主绘制方法
        // ─────────────────────────────────────────────

        public override void DoWindowContents(Rect inRect)
        {
            UIHelper.DrawDialogFrame(inRect, this);

            try
            {
                bool skipHeavyPreviewWork = suspendHeavyPreviewWork;
                bool forcePendingFacePreviewRefresh = Event.current != null
                    && (Event.current.type == EventType.MouseUp || Event.current.rawType == EventType.MouseUp);

                // 更新热加载
                if (!skipHeavyPreviewWork)
                {
                    FlushPendingThrottledFacePreviewRefresh(forcePendingFacePreviewRefresh);
                    mannequin?.Update();
                    UpdatePreviewAutoPlay();
                    UpdatePreviewFaceAnimation();
                }

                // 在任何 IMGUI 控件绘制前读取 F 键状态，避免 TextField 焦点拦截
                isHoldingReferenceGhost = Input.GetKey(ReferenceGhostHotkey);

                // 更新状态消息计时
                if (statusMessageTime > 0)
                {
                    statusMessageTime -= Time.deltaTime;
                }

                // 全局快捷键（Ctrl+A 全选图层，Ctrl+Z 撤回）
                HandleGlobalShortcuts();

                // 清理多选状态
                SanitizeLayerSelection();

                // 绘制顶部菜单栏
                Rect menuRect = new Rect(0, 0, inRect.width, TopMenuHeight);
                DrawTopMenu(menuRect);

                // 计算主内容区域
                float contentY = TopMenuHeight + Margin;
                float contentHeight = inRect.height - TopMenuHeight - BottomStatusHeight - Margin * 2;

                // 绘制左侧面板（图层树 或 表情配置）
                Rect leftRect = new Rect(0, contentY, LeftPanelWidth, contentHeight);

                // 绘制选项卡切换按钮（3行×2列）
                Rect tabRect = new Rect(leftRect.x, leftRect.y, leftRect.width, TabButtonAreaHeight);
                DrawTabButtons(tabRect);

                // 调整左侧面板内容区域
                Rect leftContentRect = new Rect(leftRect.x, leftRect.y + TabButtonAreaHeight + 4f, leftRect.width, leftRect.height - TabButtonAreaHeight - 4f);

                if (currentTab == EditorTab.BaseAppearance)
                {
                    DrawBaseAppearancePanel(leftContentRect);
                }
                else if (currentTab == EditorTab.Layers)
                {
                    DrawLayerPanel(leftContentRect);
                }
                else if (currentTab == EditorTab.Face)
                {
                    DrawFacePanel(leftContentRect);
                }
                else if (IsEquipmentOrItemsTab)
                {
                    DrawEquipmentPanel(leftContentRect);
                }
                else
                {
                    DrawAttributesPanel(leftContentRect);
                }

                // 绘制中间预览面板
                float centerWidth = inRect.width - LeftPanelWidth - RightPanelWidth - Margin * 2;
                Rect centerRect = new Rect(LeftPanelWidth + Margin, contentY, centerWidth, contentHeight);
                if (!skipHeavyPreviewWork)
                {
                    DrawPreviewPanel(centerRect);
                }
                else
                {
                    Widgets.DrawBoxSolid(centerRect, UIHelper.PanelFillColor);
                    GUI.color = UIHelper.BorderColor;
                    Widgets.DrawBox(centerRect, 1);
                    GUI.color = Color.white;
                }

                // 绘制右侧属性面板
                Rect rightRect = new Rect(inRect.width - RightPanelWidth, contentY, RightPanelWidth, contentHeight);
                DrawPropertiesPanel(rightRect);

                // 绘制底部状态栏
                Rect statusRect = new Rect(0, inRect.height - BottomStatusHeight, inRect.width, BottomStatusHeight);
                DrawStatusBar(statusRect);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 编辑器UI绘制错误: {ex}");
                consecutiveRenderErrors++;
                if (consecutiveRenderErrors >= 3)
                {
                    Log.Error("[CharacterStudio] 连续 3 次绘制异常，关闭编辑器以避免循环报错。");
                    Close();
                }
            }

            Event? currentEvent = Event.current;
            if (consecutiveRenderErrors > 0 && currentEvent != null && currentEvent.type == EventType.Repaint)
            {
                consecutiveRenderErrors = 0;
            }
        }

        // ─────────────────────────────────────────────
        // 选项卡切换
        // ─────────────────────────────────────────────

        private void DrawTabButtons(Rect rect)
        {
            // 3 行 × 2 列布局：预留第 5 个页签给武器，第 6 格为空白
            float halfW = rect.width / 2f;
            float halfH = rect.height / 3f;
            float row0 = rect.y;
            float row1 = rect.y + halfH;
            float row2 = rect.y + halfH * 2f;

            // 行 0
            if (UIHelper.DrawTabButton(
                new Rect(rect.x, row0, halfW, halfH),
                "CS_Studio_Tab_BaseAppearance".Translate(),
                currentTab == EditorTab.BaseAppearance))
                currentTab = EditorTab.BaseAppearance;

            if (UIHelper.DrawTabButton(
                new Rect(rect.x + halfW, row0, halfW, halfH),
                "CS_Studio_Tab_Layers".Translate(),
                currentTab == EditorTab.Layers))
                currentTab = EditorTab.Layers;

            // 行 1
            if (UIHelper.DrawTabButton(
                new Rect(rect.x, row1, halfW, halfH),
                "CS_Studio_Tab_Face".Translate(),
                currentTab == EditorTab.Face))
                currentTab = EditorTab.Face;

            if (UIHelper.DrawTabButton(
                new Rect(rect.x + halfW, row1, halfW, halfH),
                "CS_Studio_Tab_Attributes".Translate(),
                currentTab == EditorTab.Attributes))
                currentTab = EditorTab.Attributes;

            // 行 2
            if (UIHelper.DrawTabButton(
                new Rect(rect.x, row2, halfW, halfH),
                "CS_Studio_Tab_Equipment".Translate(),
                currentTab == EditorTab.Equipment))
                currentTab = EditorTab.Equipment;

            if (UIHelper.DrawTabButton(
                new Rect(rect.x + halfW, row2, halfW, halfH),
                "CS_Studio_Tab_Items".Translate(),
                currentTab == EditorTab.Items))
                currentTab = EditorTab.Items;
        }

        // ─────────────────────────────────────────────
        // 顶部菜单栏
        // ─────────────────────────────────────────────

        private void DrawTopMenu(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), UIHelper.AccentSoftColor);

            float x = rect.x + Margin;
            float buttonY = rect.y + 4f;
            float buttonHeight = Mathf.Max(ButtonHeight - 2f, 22f);
            float rightLabelWidth = 220f;
            float maxButtonX = rect.xMax - rightLabelWidth - Margin;

            bool DrawButton(string label, Action action, bool accent = false)
            {
                GameFont oldFont = Text.Font;
                Text.Font = GameFont.Tiny;
                float width = Mathf.Max(Text.CalcSize(label).x + 22f, 54f);
                Text.Font = oldFont;

                if (x + width > maxButtonX)
                {
                    return false;
                }

                Rect buttonRect = new Rect(x, buttonY, width, buttonHeight);
                Color fill = accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor;
                Widgets.DrawBoxSolid(buttonRect, fill);
                Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), accent ? UIHelper.AccentColor : new Color(1f, 1f, 1f, 0.06f));

                if (Mouse.IsOver(buttonRect))
                {
                    Widgets.DrawBoxSolid(buttonRect, new Color(1f, 1f, 1f, accent ? 0.04f : 0.06f));
                    GUI.color = UIHelper.HoverOutlineColor;
                    Widgets.DrawBox(buttonRect, 1);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = accent ? UIHelper.AccentSoftColor : UIHelper.BorderColor;
                    Widgets.DrawBox(buttonRect, 1);
                    GUI.color = Color.white;
                }

                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = accent ? Color.white : UIHelper.SubtleColor;
                Widgets.Label(buttonRect, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = prevFont;

                bool clicked = Widgets.ButtonInvisible(buttonRect);
                if (clicked)
                {
                    action();
                }

                x += width + 5f;
                return clicked;
            }

            DrawButton("CS_Studio_File_New".Translate(), OnNewSkin);
            DrawButton("CS_Studio_File_Save".Translate(), layerModificationWorkflowActive ? OnSaveRenderFixPatch : (Action)OnSaveSkin, isDirty);
            DrawButton("CS_Studio_SpawnNewPawnButton".Translate(), OnSpawnNewPawn, true);
            DrawButton("CS_Studio_File_OpenSkinFolder".Translate(), OnOpenSkinFolder);
            DrawButton("CS_Studio_File_Export".Translate(), OnExportMod);
            DrawButton("CS_Studio_File_Import".Translate(), OnImportFromMap, isDirty);

            x += 8f;
            if (x < maxButtonX)
            {
                Widgets.DrawBoxSolid(new Rect(x - 4f, rect.y + 8f, 1f, rect.height - 16f), new Color(1f, 1f, 1f, 0.08f));
            }

            DrawButton("CS_Studio_Skin_Settings".Translate(), OnOpenSkinSettings);
            DrawButton("CS_LLM_Settings_Title".Translate(), OnOpenLlmSettings);
            DrawButton("CS_Studio_Menu_Abilities".Translate(), OpenAbilityEditor);

            Rect helpRect = new Rect(Mathf.Min(x, maxButtonX - 28f), buttonY, 26f, buttonHeight);
            if (helpRect.xMax <= maxButtonX)
            {
                Widgets.DrawBoxSolid(helpRect, UIHelper.PanelFillSoftColor);
                GUI.color = Mouse.IsOver(helpRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
                Widgets.DrawBox(helpRect, 1);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = UIHelper.HeaderColor;
                Widgets.Label(helpRect, "?");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(helpRect))
                {
                    Find.WindowStack.Add(new Dialog_Glossary());
                }
            }

            Rect skinRect = new Rect(rect.xMax - rightLabelWidth, rect.y + 4f, rightLabelWidth - Margin, buttonHeight);
            Widgets.DrawBoxSolid(skinRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(skinRect.x, skinRect.yMax - 2f, skinRect.width, 2f), isDirty ? UIHelper.AccentColor : UIHelper.AccentSoftColor);
            GUI.color = isDirty ? UIHelper.AccentSoftColor : UIHelper.BorderColor;
            Widgets.DrawBox(skinRect, 1);
            GUI.color = Color.white;

            string skinLabel = layerModificationWorkflowActive
                ? (workingRenderFixPatch?.label ?? "CS_Studio_RenderFixPatchLabel".Translate().ToString())
                : (workingSkin.label ?? workingSkin.defName);
            if (isDirty)
            {
                skinLabel = "● " + skinLabel;
            }

            GameFont labelOldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = isDirty ? Color.white : UIHelper.SubtleColor;
            Widgets.Label(new Rect(skinRect.x + 8f, skinRect.y, skinRect.width - 12f, skinRect.height), skinLabel);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = labelOldFont;
        }

        // ─────────────────────────────────────────────
        // 状态栏
        // ─────────────────────────────────────────────

        private void DrawStatusBar(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 2f), isDirty ? UIHelper.AccentColor : UIHelper.AccentSoftColor);

            string status = "";
            if (statusMessageTime > 0 && !string.IsNullOrEmpty(statusMessage))
            {
                status = statusMessage;
            }
            else if (layerModificationWorkflowActive)
            {
               int hiddenCount = workingRenderFixPatch?.hideNodePaths?.Count ?? 0;
               int offsetCount = workingRenderFixPatch?.orderOverrides?.Count ?? 0;
               status = "CS_Studio_Patch_StatusMessage".Translate(hiddenCount, offsetCount);
            }            else if (isDirty)
            {
                status = "CS_Studio_Status_UnsavedChanges".Translate();
            }
            else
            {
                int baseCount = workingSkin.baseAppearance?.EnabledSlots().Count() ?? 0;
                status = "CS_Studio_Status_EditorSummary".Translate(baseCount, workingSkin.layers.Count, previewZoom.ToString("F1"), previewRotation.ToString());
            }

            Rect chipRect = new Rect(rect.x + Margin, rect.y + 4f, 56f, rect.height - 8f);
            Widgets.DrawBoxSolid(chipRect, isDirty ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
            GUI.color = isDirty ? UIHelper.AccentSoftColor : UIHelper.BorderColor;
            Widgets.DrawBox(chipRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = isDirty ? Color.white : UIHelper.SubtleColor;
            Widgets.Label(chipRect, isDirty ? "CS_Studio_Status_Edit".Translate() : "CS_Studio_Status_Ready".Translate());
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(chipRect.xMax + 8f, rect.y, rect.width - chipRect.width - Margin * 3, rect.height), status);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;
        }
    }
}
