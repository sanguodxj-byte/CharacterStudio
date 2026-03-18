using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Exporter;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;
using System.IO;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 皮肤编辑器主窗口
    /// 三栏布局：图层树 | 预览 | 属性面板
    /// </summary>
    public partial class Dialog_SkinEditor : Window
    {
        // ─────────────────────────────────────────────
        // 常量
        // ─────────────────────────────────────────────
        private const float LeftPanelWidth = 240f;
        private const float RightPanelWidth = 280f;
        private const float TopMenuHeight = 30f;
        private const float BottomStatusHeight = 25f;
        private const float LayerItemHeight = 28f;
        private const float ButtonHeight = 24f;
        private new const float Margin = 5f;

        // ─────────────────────────────────────────────
        // 状态
        // ─────────────────────────────────────────────
        private PawnSkinDef workingSkin;
        private List<ModularAbilityDef> workingAbilities = new List<ModularAbilityDef>();
        private int selectedLayerIndex = -1;
        private HashSet<int> selectedLayerIndices = new HashSet<int>();
        private readonly EditorHistory editorHistory = new EditorHistory(100);
        private Vector2 layerScrollPos;
        private Vector2 propsScrollPos;
        private Vector2 faceScrollPos;
        private Vector2 baseScrollPos;
        private bool isDirty = false;
        /// <summary>表情贴图路径内联编辑缓冲，每帧实时同步到 faceConfig</summary>
        private readonly Dictionary<ExpressionType, string> exprPathBuffer = new Dictionary<ExpressionType, string>();
 
        private enum EditorTab { BaseAppearance, Layers, Face, Attributes }
        private EditorTab currentTab = EditorTab.BaseAppearance;
        private string statusMessage = "";
        private float statusMessageTime = 0f;

        /// <summary>
        /// 推荐最小集表情：完成这 8 种即可覆盖绝大多数游戏状态。
        /// Sleeping 与 Blink 共用同一张闭眼贴图，Pain、Dead 亦可共用，
        /// 故实际美术文件可进一步减少。
        /// </summary>
        private static readonly HashSet<ExpressionType> MinSetExpressions = new HashSet<ExpressionType>
        {
            ExpressionType.Neutral,
            ExpressionType.Happy,
            ExpressionType.Sad,
            ExpressionType.Angry,
            ExpressionType.Sleeping,
            ExpressionType.Blink,
            ExpressionType.Dead,
            ExpressionType.Pain,
        };

        // 预览管理
        private MannequinManager? mannequin;
        private Rot4 previewRotation = Rot4.South;
        private float previewZoom = 1f;
        private bool previewExpressionOverrideEnabled = false;
        private ExpressionType previewExpression = ExpressionType.Neutral;
        private const KeyCode ReferenceGhostHotkey = KeyCode.F;
        private const float ReferenceGhostAlpha = 0.35f;
        /// <summary>
        /// F键虚影状态：在 DoWindowContents 最开始读取，避免被 IMGUI TextField 拦截。
        /// </summary>
        private bool isHoldingReferenceGhost = false;

        // 编辑目标 Pawn（可空，空表示仅预览模式）
        private Pawn? targetPawn;

        // ─────────────────────────────────────────────
        // 树状视图状态
        // ─────────────────────────────────────────────
        private HashSet<string> expandedPaths = new HashSet<string>();
        private HashSet<string> collapsedSections = new HashSet<string>(); // 记录折叠的属性区域
        private RenderNodeSnapshot? cachedRootSnapshot;
        private string selectedNodePath = "";
        private BaseAppearanceSlotType? selectedBaseSlotType;
        private float treeViewHeight = 0f;
        private const float TreeNodeHeight = 22f;
        private const float TreeIndentWidth = 16f;

        // ─────────────────────────────────────────────
        // 窗口设置
        // ─────────────────────────────────────────────

        public override Vector2 InitialSize => new Vector2(1680f, 980f);

        public Dialog_SkinEditor()
        {
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = false;
            this.resizeable = false;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = false;
            this.forcePause = false;

            // 创建新的工作皮肤
            workingSkin = new PawnSkinDef
            {
                defName = "CS_NewSkin",
                label = "CS_Studio_EditorTitle".Translate(),
                description = ""
            };
        }

        public Dialog_SkinEditor(PawnSkinDef existingSkin) : this()
        {
            if (existingSkin != null)
            {
                workingSkin = existingSkin.Clone();
            }
            SyncAbilitiesFromSkin();
        }

        public Dialog_SkinEditor(Pawn pawn) : this()
        {
            targetPawn = pawn;

            if (pawn?.def != null)
            {
                workingSkin.targetRaces = new List<string> { pawn.def.defName };
            }

            var comp = pawn?.GetComp<CompPawnSkin>();
            if (comp?.ActiveSkin != null)
            {
                workingSkin = comp.ActiveSkin.Clone();
            }

            SyncAbilitiesFromSkin();
        }

        private void SyncAbilitiesFromSkin()
        {
            workingAbilities.Clear();
            if (workingSkin.abilities == null) return;

            foreach (var ability in workingSkin.abilities)
            {
                if (ability != null)
                {
                    workingAbilities.Add(ability.Clone());
                }
            }
        }

        private void SyncAbilitiesToSkin()
        {
            workingSkin.abilities.Clear();
            if (workingAbilities == null) return;

            foreach (var ability in workingAbilities)
            {
                if (ability != null)
                {
                    workingSkin.abilities.Add(ability.Clone());
                }
            }
        }

        public override void PreOpen()
        {
            base.PreOpen();
            InitializeMannequin();
        }

        public override void PreClose()
        {
            base.PreClose();
            CleanupMannequin();
        }

        // ─────────────────────────────────────────────
        // 主绘制方法
        // ─────────────────────────────────────────────

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                // 更新热加载
                mannequin?.Update();

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
                
                // 绘制选项卡切换按钮（2行×2列）
                Rect tabRect = new Rect(leftRect.x, leftRect.y, leftRect.width, 56f);
                DrawTabButtons(tabRect);

                // 调整左侧面板内容区域
                Rect leftContentRect = new Rect(leftRect.x, leftRect.y + 60f, leftRect.width, leftRect.height - 60f);
 
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
                else
                {
                    DrawAttributesPanel(leftContentRect);
                }

                // 绘制中间预览面板
                float centerWidth = inRect.width - LeftPanelWidth - RightPanelWidth - Margin * 2;
                Rect centerRect = new Rect(LeftPanelWidth + Margin, contentY, centerWidth, contentHeight);
                DrawPreviewPanel(centerRect);

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
                // 尝试重置状态或关闭窗口以避免循环报错
                Close();
            }
        }

        // ─────────────────────────────────────────────
        // 选项卡切换
        // ─────────────────────────────────────────────

        private void DrawTabButtons(Rect rect)
        {
            // 2 行 × 2 列布局：每格宽 = rect.width/2，高 = rect.height/2
            float halfW = rect.width / 2f;
            float halfH = rect.height / 2f;
            float row0 = rect.y;
            float row1 = rect.y + halfH;

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
            DrawButton("CS_Studio_File_Save".Translate(), OnSaveSkin, isDirty);
            DrawButton("CS_Studio_Btn_Apply".Translate(), OnApplyToTargetPawn, true);
            DrawButton("CS_Studio_File_Export".Translate(), OnExportMod);
            DrawButton("CS_Studio_File_Import".Translate(), OnImportFromMap);
            DrawButton("CS_Studio_Btn_Reset".Translate(), OnResetParameters);

            x += 8f;
            if (x < maxButtonX)
            {
                Widgets.DrawBoxSolid(new Rect(x - 4f, rect.y + 8f, 1f, rect.height - 16f), new Color(1f, 1f, 1f, 0.08f));
            }

            DrawButton("CS_Studio_Skin_Settings".Translate(), OnOpenSkinSettings);
            DrawButton("CS_LLM_Settings_Title".Translate(), OnOpenLlmSettings);
            DrawButton("CS_Studio_Menu_Abilities".Translate(), () =>
            {
                SyncAbilitiesFromSkin();
                Find.WindowStack.Add(new Dialog_AbilityEditor(workingAbilities, workingSkin.abilityHotkeys, workingSkin));
            });

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

            string skinLabel = workingSkin.label ?? workingSkin.defName;
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
        // 图层面板 - 树状视图
        // ─────────────────────────────────────────────

        private void DrawLayerPanel(Rect rect)
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
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), "CS_Studio_Panel_Layers".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            float btnY = titleRect.yMax + 6f;
            float btnWidth = (rect.width - Margin * 8) / 7f;
            float btnHeight = Mathf.Max(ButtonHeight - 2f, 22f);

            bool DrawIconButton(Rect buttonRect, string label, string tooltip, Action action, bool accent = false)
            {
                Widgets.DrawBoxSolid(buttonRect, accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
                Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), accent ? UIHelper.AccentColor : new Color(1f, 1f, 1f, 0.05f));
                GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
                Widgets.DrawBox(buttonRect, 1);
                GUI.color = Color.white;

                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = accent ? Color.white : UIHelper.HeaderColor;
                Widgets.Label(buttonRect, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = prevFont;

                TooltipHandler.TipRegion(buttonRect, tooltip);
                if (Widgets.ButtonInvisible(buttonRect))
                {
                    action();
                    return true;
                }
                return false;
            }

            DrawIconButton(new Rect(rect.x + Margin, btnY, btnWidth, btnHeight), "↻", "CS_Studio_Panel_RefreshTree".Translate(), RefreshRenderTree);
            DrawIconButton(new Rect(rect.x + Margin * 2 + btnWidth, btnY, btnWidth, btnHeight), "▼", "CS_Studio_Panel_ExpandAll".Translate(), ExpandAllNodes);
            DrawIconButton(new Rect(rect.x + Margin * 3 + btnWidth * 2, btnY, btnWidth, btnHeight), "▶", "CS_Studio_Panel_CollapseAll".Translate(), CollapseAllNodes);
            DrawIconButton(new Rect(rect.x + Margin * 4 + btnWidth * 3, btnY, btnWidth, btnHeight), "+", "CS_Studio_Layer_AddCustom".Translate(), OnAddLayer, true);
            DrawIconButton(new Rect(rect.x + Margin * 5 + btnWidth * 4, btnY, btnWidth, btnHeight), "-", "CS_Studio_Tip_RemoveLayer".Translate(), OnRemoveLayer);
            DrawIconButton(new Rect(rect.x + Margin * 6 + btnWidth * 5, btnY, btnWidth, btnHeight), "↑", "CS_Studio_Tip_MoveUp".Translate(), () => MoveSelectedLayerUp());
            DrawIconButton(new Rect(rect.x + Margin * 7 + btnWidth * 6, btnY, btnWidth, btnHeight), "↓", "CS_Studio_Tip_MoveDown".Translate(), () => MoveSelectedLayerDown());

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
 
        private void DrawBaseAppearancePanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, ButtonHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "CS_Studio_Tab_BaseAppearance".Translate());
            Text.Font = GameFont.Small;

            float contentY = rect.y + Margin + ButtonHeight + Margin;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);

            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            workingSkin.baseAppearance.EnsureAllSlotsExist();

            float viewHeight = Mathf.Max(contentRect.height, workingSkin.baseAppearance.slots.Count * TreeNodeHeight + 40f);
            Rect viewRect = new Rect(0, 0, contentRect.width - 16, viewHeight);
            Widgets.BeginScrollView(contentRect, ref baseScrollPos, viewRect);

            float y = 0f;
            int rowIndex = 0;
            foreach (BaseAppearanceSlotType slotType in Enum.GetValues(typeof(BaseAppearanceSlotType)))
            {
                y = DrawBaseSlotRow(workingSkin.baseAppearance.GetSlot(slotType), y, viewRect.width, rowIndex++);
            }

            Widgets.EndScrollView();
        }

        private float DrawBaseSlotRow(BaseAppearanceSlotConfig slot, float y, float width, int rowIndex)
        {
            Rect rowRect = new Rect(0, y, width, TreeNodeHeight);
            UIHelper.DrawAlternatingRowBackground(rowRect, rowIndex);

            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }

            if (selectedBaseSlotType == slot.slotType)
            {
                Widgets.DrawBox(rowRect, 1);
            }

            Rect visRect = new Rect(4, y + 2, 18, 18);
            bool enabled = slot.enabled;
            GUI.color = enabled ? Color.white : Color.gray;
            if (Widgets.ButtonText(visRect, enabled ? "◉" : "◯", false))
            {
                CaptureUndoSnapshot();
                slot.enabled = !slot.enabled;
                isDirty = true;
                RefreshPreview();
                RefreshRenderTree();
            }
            GUI.color = Color.white;

            Rect nameRect = new Rect(28, y + 1, width - 84, TreeNodeHeight - 2);
            string slotLabel = BaseAppearanceUtility.GetDisplayName(slot.slotType);
            string summary = string.IsNullOrEmpty(slot.texPath) ? "CS_Studio_BaseSlot_Unset".Translate() : System.IO.Path.GetFileName(slot.texPath);
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, $"{slotLabel}  {summary}");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            Rect editRect = new Rect(width - 42, y + 2, 38, 18);
            GameFont buttonOldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(editRect, "CS_Studio_Btn_Edit".Translate()))
            {
                selectedBaseSlotType = slot.slotType;
                selectedNodePath = "";
                selectedLayerIndex = -1;
                selectedLayerIndices.Clear();
            }
            Text.Font = buttonOldFont;

            if (Widgets.ButtonInvisible(rowRect))
            {
                selectedBaseSlotType = slot.slotType;
                selectedNodePath = "";
                selectedLayerIndex = -1;
                selectedLayerIndices.Clear();
            }

            return y + TreeNodeHeight;
        }
 
        /// <summary>
        /// 绘制表情配置面板
        /// </summary>
        private static string GetExpressionTypeLabel(ExpressionType expression)
        {
            return ($"CS_Expression_{expression}").Translate();
        }

        private static string GetWorkerLabel(string workerKey)
        {
            return ($"CS_Studio_Worker_{workerKey}").Translate();
        }

        private static string GetDefaultLayerLabel(int index)
        {
            return "CS_Studio_Layer_DefaultName".Translate(index + 1);
        }

        private static string GetMountedLayerLabel(RenderNodeSnapshot node)
        {
            return "CS_Studio_Layer_MountedOn".Translate(node.tagDefName ?? node.workerClass);
        }

        private void ApplyPreviewExpressionOverride(bool enabled, ExpressionType expression)
        {
            previewExpressionOverrideEnabled = enabled;
            previewExpression = expression;

            var previewPawn = mannequin?.CurrentPawn;
            var skinComp = previewPawn?.GetComp<CompPawnSkin>();
            skinComp?.SetPreviewExpressionOverride(enabled ? expression : null);
            RefreshPreview();
        }

        private void OpenPreviewExpressionMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Face_PreviewAuto".Translate(), () => ApplyPreviewExpressionOverride(false, previewExpression))
            };

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                ExpressionType localExpression = expression;
                options.Add(new FloatMenuOption(GetExpressionTypeLabel(localExpression), () => ApplyPreviewExpressionOverride(true, localExpression)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawFacePanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, ButtonHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "CS_Studio_Face_Title".Translate());
            Text.Font = GameFont.Small;

            float contentY = rect.y + Margin + ButtonHeight + Margin;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            int exprCount = Enum.GetValues(typeof(ExpressionType)).Length;
            Rect viewRect = new Rect(0, 0, contentRect.width - 16, Enum.GetValues(typeof(ExpressionType)).Length * 28f + 200f);

            Widgets.BeginScrollView(contentRect, ref faceScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_Title".Translate());

            if (workingSkin.faceConfig == null)
            {
                if (Widgets.ButtonText(new Rect(0f, y, width, 28f), "CS_Studio_Face_Create".Translate()))
                {
                    workingSkin.faceConfig = new PawnFaceConfig();
                    isDirty = true;
                    RefreshPreview();
                }
                y += 36f;
                Widgets.EndScrollView();
                return;
            }

            var fc = workingSkin.faceConfig;
            bool enabled = fc.enabled;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Face_Enable".Translate(), ref enabled);
            if (enabled != fc.enabled)
            {
                fc.enabled = enabled;
                isDirty = true;
                RefreshPreview();
            }

            // 预览表情选择器
            Rect previewRowRect = new Rect(0f, y, width, 28f);
            string activePreviewLabel = previewExpressionOverrideEnabled
                ? GetExpressionTypeLabel(previewExpression)
                : "CS_Studio_Face_PreviewAuto".Translate();
            UIHelper.DrawPropertyFieldWithButton(ref y, width,
                "CS_Studio_Face_PreviewActive".Translate(), activePreviewLabel,
                OpenPreviewExpressionMenu);

            y += 4f;

            // 说明文字
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(0f, y, width, 32f), "CS_Studio_Face_HeadTexHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 36f;

            // 各表情 -> 头部整张贴图路径映射
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_ExpressionMappings".Translate());

            int assignedCount = fc.expressions.Count(e =>
                !string.IsNullOrEmpty(e.texPath) || (e.frames != null && e.frames.Count > 0));
            int minSetDone = fc.expressions.Count(e =>
                MinSetExpressions.Contains(e.expression) &&
                (!string.IsNullOrEmpty(e.texPath) || (e.frames != null && e.frames.Count > 0)));
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(0f, y, width, 18f),
                $"{assignedCount} / {exprCount} " + "CS_Studio_Face_Assigned".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 20f;
            // 最小集完成进度
            {
                bool minSetComplete = minSetDone >= MinSetExpressions.Count;
                GUI.color = minSetComplete ? new Color(0.4f, 0.9f, 0.4f) : new Color(1f, 0.85f, 0.3f);
                Text.Font = GameFont.Tiny;
                string minSetLabel = "CS_Studio_Face_MinSetProgress".Translate(minSetDone, MinSetExpressions.Count);
                Widgets.Label(new Rect(0f, y, width, 18f), "★ " + minSetLabel);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 18f;
            }
            y += 4f;

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                ExpressionType localExpr = expression;
                var exactMatch = fc.expressions.Find(e => e.expression == localExpr);
                bool isAnimated = exactMatch?.IsAnimated == true;
                bool hasContent = exactMatch != null &&
                    (!string.IsNullOrEmpty(exactMatch.texPath) || exactMatch.frames.Count > 0);

                // ── 第一行：表情名称（全宽标签）+ 右侧按钮组 ──
                // 背景条（区分不同表情）
                Widgets.DrawBoxSolid(new Rect(0f, y, width, 24f),
                    hasContent ? UIHelper.AccentSoftColor : UIHelper.AlternatingRowColor);

                // 表情名称（左侧，全宽减去按钮区）
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                bool isMinSet = MinSetExpressions.Contains(localExpr);
                if (isMinSet)
                {
                    GUI.color = hasContent ? new Color(1f, 0.95f, 0.5f) : new Color(1f, 0.85f, 0.3f, 0.9f);
                    Widgets.Label(new Rect(6f, y, 16f, 24f), "★");
                    GUI.color = Color.white;
                }
                Widgets.Label(new Rect(isMinSet ? 22f : 6f, y, width - (isMinSet ? 118f : 102f), 24f), GetExpressionTypeLabel(localExpr));
                Text.Anchor = TextAnchor.UpperLeft;

                // 右侧按钮组：[◉ 预览] [选择/编辑] [+F] [X]
                float bx = width - 96f;

                // [◉] 预览按钮：点击切换预览表情
                bool isPreviewActive = previewExpressionOverrideEnabled && previewExpression == localExpr;
                GUI.color = isPreviewActive ? UIHelper.AccentColor : UIHelper.SubtleColor;
                if (Widgets.ButtonText(new Rect(bx, y, 20f, 22f), "◉", false))
                    ApplyPreviewExpressionOverride(!isPreviewActive || previewExpression != localExpr, localExpr);
                TooltipHandler.TipRegion(new Rect(bx, y, 20f, 22f), "CS_Studio_Face_PreviewActive".Translate(GetExpressionTypeLabel(localExpr), GetExpressionTypeLabel(localExpr)));
                GUI.color = Color.white;

                // [选择/编辑] 按钮
                if (Widgets.ButtonText(new Rect(bx + 22f, y, 36f, 22f), isAnimated ? "…" : "..."))
                {
                    if (isAnimated)
                    {
                        var frameOpts = new List<FloatMenuOption>
                        {
                            new FloatMenuOption("CS_Studio_Face_AddFrame".Translate(), () =>
                            {
                                Find.WindowStack.Add(new Dialog_FileBrowser("", path =>
                                {
                                    if (!string.IsNullOrEmpty(path))
                                    {
                                        exactMatch!.frames.Add(new ExpressionFrame { texPath = path, durationTicks = 6 });
                                        isDirty = true;
                                        RefreshPreview();
                                    }
                                }));
                            }),
                            new FloatMenuOption("CS_Studio_Face_ConvertToStatic".Translate(), () =>
                            {
                                string firstPath = exactMatch!.frames.Count > 0 ? exactMatch.frames[0].texPath : string.Empty;
                                fc.SetTexPath(localExpr, firstPath);
                                isDirty = true;
                                RefreshPreview();
                            })
                        };
                        Find.WindowStack.Add(new FloatMenu(frameOpts));
                    }
                    else
                    {
                        string curPath = exactMatch?.texPath ?? string.Empty;
                        Find.WindowStack.Add(new Dialog_FileBrowser(curPath, path =>
                        {
                            fc.SetTexPath(localExpr, path);
                            isDirty = true;
                            if (previewExpressionOverrideEnabled && previewExpression == localExpr)
                                ApplyPreviewExpressionOverride(true, localExpr);
                            else
                                RefreshPreview();
                        }));
                    }
                }

                // [+F] 按钮：追加帧
                Rect addFrameRect = new Rect(bx + 60f, y, 18f, 22f);
                TooltipHandler.TipRegion(addFrameRect, "CS_Studio_Face_AddFrame".Translate());
                if (Widgets.ButtonText(addFrameRect, "+"))
                {
                    Find.WindowStack.Add(new Dialog_FileBrowser("", path =>
                    {
                        if (!string.IsNullOrEmpty(path))
                        {
                            fc.AddFrame(localExpr, path, durationTicks: 6);
                            isDirty = true;
                            RefreshPreview();
                        }
                    }));
                }

                // [X] 清除按钮
                Rect clearRect = new Rect(bx + 80f, y, 18f, 22f);
                TooltipHandler.TipRegion(clearRect, "CS_Studio_Face_ClearPath".Translate());
                if (Widgets.ButtonText(clearRect, "X"))
                {
                    fc.SetTexPath(localExpr, string.Empty);
                    isDirty = true;
                    RefreshPreview();
                }

                y += 26f;

                // ── 第二行：动画模式显示帧摘要；单帧模式改为内联路径编辑框 ──
                if (isAnimated)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UIHelper.SubtleColor;
                    Widgets.Label(new Rect(6f, y, width - 12f, 18f),
                        $"  ▶ {exactMatch!.frames.Count} " + "CS_Studio_Face_Frames".Translate());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    y += 20f;
                }
                else
                {
                    // 同步缓冲：若缓冲未初始化或当前无焦点则以数据层为准
                    if (!exprPathBuffer.TryGetValue(localExpr, out string bufPath))
                        bufPath = exactMatch?.texPath ?? string.Empty;

                    // TextField（占大部分宽度）+ [...] 文件浏览按钮
                    string controlName = $"ExprPath_{localExpr}";
                    GUI.SetNextControlName(controlName);
                    string newBuf = Widgets.TextField(new Rect(6f, y, width - 38f, 18f), bufPath);
                    if (newBuf != bufPath)
                    {
                        exprPathBuffer[localExpr] = newBuf;
                        // 实时写入：路径看起来合理（非空）时立即更新数据层
                        fc.SetTexPath(localExpr, newBuf);
                        isDirty = true;
                        if (previewExpressionOverrideEnabled && previewExpression == localExpr)
                            ApplyPreviewExpressionOverride(true, localExpr);
                        else
                            RefreshPreview();
                    }
                    else
                    {
                        // 无编辑时保持缓冲与数据层同步
                        exprPathBuffer[localExpr] = exactMatch?.texPath ?? string.Empty;
                    }

                    // [...] 打开文件浏览器
                    if (Widgets.ButtonText(new Rect(width - 30f, y, 26f, 18f), "…"))
                    {
                        string cur = exactMatch?.texPath ?? string.Empty;
                        Find.WindowStack.Add(new Dialog_FileBrowser(cur, path =>
                        {
                            fc.SetTexPath(localExpr, path);
                            exprPathBuffer[localExpr] = path;
                            isDirty = true;
                            if (previewExpressionOverrideEnabled && previewExpression == localExpr)
                                ApplyPreviewExpressionOverride(true, localExpr);
                            else
                                RefreshPreview();
                        }));
                    }
                    y += 22f;
                }

                // ── 帧动画展开列表（仅 isAnimated 时显示）──
                if (isAnimated && exactMatch != null)
                {
                    for (int fi = 0; fi < exactMatch.frames.Count; fi++)
                    {
                        int frameIdx = fi;
                        var frame = exactMatch.frames[fi];

                        // 帧行背景
                        Widgets.DrawBoxSolid(new Rect(20f, y, width - 20f, 22f),
                            UIHelper.PanelFillSoftColor);

                        // 帧序号
                        Text.Font = GameFont.Tiny;
                        GUI.color = UIHelper.AccentColor;
                        Widgets.Label(new Rect(22f, y + 3f, 22f, 16f), $"[{fi}]");
                        GUI.color = Color.white;

                        // 文件名（占据中间大部分空间，使用 Tiny 字体确保完整显示）
                        string fname = string.IsNullOrEmpty(frame.texPath)
                            ? (string)"CS_Studio_None".Translate()
                            : System.IO.Path.GetFileName(frame.texPath);
                        Widgets.Label(new Rect(46f, y + 3f, width - 172f, 16f), fname);
                        Text.Font = GameFont.Small;

                        // 时长显示（灰色小字）
                        Text.Font = GameFont.Tiny;
                        GUI.color = UIHelper.SubtleColor;
                        Widgets.Label(new Rect(width - 124f, y + 3f, 36f, 16f), $"{frame.durationTicks}t");
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;

                        // 操作按钮区（右侧固定宽度）
                        // [选] 贴图
                        if (Widgets.ButtonText(new Rect(width - 86f, y + 1f, 28f, 20f), "…"))
                        {
                            Find.WindowStack.Add(new Dialog_FileBrowser(frame.texPath, path =>
                            {
                                if (!string.IsNullOrEmpty(path))
                                {
                                    frame.texPath = path;
                                    isDirty = true;
                                    RefreshPreview();
                                }
                            }));
                        }

                        // [-] 时长减
                        if (Widgets.ButtonText(new Rect(width - 56f, y + 1f, 16f, 20f), "-"))
                        {
                            frame.durationTicks = Mathf.Max(1, frame.durationTicks - 1);
                            isDirty = true;
                        }
                        // [+] 时长加
                        if (Widgets.ButtonText(new Rect(width - 38f, y + 1f, 16f, 20f), "+"))
                        {
                            frame.durationTicks = Mathf.Min(600, frame.durationTicks + 1);
                            isDirty = true;
                        }
                        // [×] 删除帧
                        if (Widgets.ButtonText(new Rect(width - 20f, y + 1f, 18f, 20f), "×"))
                        {
                            fc.RemoveFrame(localExpr, frameIdx);
                            isDirty = true;
                            RefreshPreview();
                        }

                        y += 24f;
                    }
                }
            }

            // ── 眼睛方向配置段落 ──────────────────────────────────
            y += 8f;
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_EyeDir_Title".Translate());

            // 说明文字
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(0f, y, width, 28f), "CS_Studio_Face_EyeDir_Hint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 32f;

            // 若尚无 eyeDirectionConfig，提供创建按钮
            if (fc.eyeDirectionConfig == null)
            {
                if (Widgets.ButtonText(new Rect(0f, y, width, 28f), "CS_Studio_Face_EyeDir_Create".Translate()))
                {
                    fc.eyeDirectionConfig = new CharacterStudio.Core.PawnEyeDirectionConfig();
                    isDirty = true;
                    RefreshPreview();
                }
                y += 36f;
            }
            else
            {
                var eyeCfg = fc.eyeDirectionConfig;

                // 启用开关
                bool eyeEnabled = eyeCfg.enabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Face_EyeDir_Enable".Translate(), ref eyeEnabled);
                if (eyeEnabled != eyeCfg.enabled)
                {
                    eyeCfg.enabled = eyeEnabled;
                    isDirty = true;
                    RefreshPreview();
                }

                // 预览方向选择器
                if (eyeEnabled)
                {
                    var comp = mannequin?.CurrentPawn?.GetComp<CharacterStudio.Core.CompPawnSkin>();
                    string previewDirLabel = comp != null
                        ? comp.CurEyeDirection.ToString()
                        : CharacterStudio.Core.EyeDirection.Center.ToString();
                    UIHelper.DrawPropertyFieldWithButton(ref y, width,
                        "CS_Studio_Face_EyeDir_Preview".Translate(), previewDirLabel,
                        () => OpenPreviewEyeDirectionMenu(comp));
                    y += 4f;
                }

                // 瞳孔 UV 偏移模式（pupilMoveRange > 0 则开启代码驱动，无需 4 张方向贴图）
                y += 4f;
                float pupilRange = eyeCfg.pupilMoveRange;
                UIHelper.DrawPropertySlider(ref y, width,
                    "CS_Studio_Face_EyeDir_PupilMoveRange".Translate(),
                    ref pupilRange, 0f, 0.15f, "F3");
                if (pupilRange != eyeCfg.pupilMoveRange)
                {
                    eyeCfg.pupilMoveRange = pupilRange;
                    isDirty = true;
                    RefreshPreview();
                }

                y += 4f;
                // 5个方向的贴图路径输入（UV 模式下 Left/Right/Up/Down 贴图不再使用，折叠显示提示）
                if (eyeCfg.pupilMoveRange > 0f)
                {
                    // UV 偏移模式：只需要 Center 贴图
                    DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Center".Translate(),
                        eyeCfg.texCenter, path => { eyeCfg.texCenter = path; isDirty = true; RefreshPreview(); });
                    UIHelper.DrawPropertyLabel(ref y, width,
                        "CS_Studio_Face_EyeDir_UVModeHint".Translate(), "");
                }
                else
                {
                    // 贴图替换模式：5 方向贴图均可配置
                    DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Center".Translate(),
                        eyeCfg.texCenter, path => { eyeCfg.texCenter = path; isDirty = true; RefreshPreview(); });
                    DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Left".Translate(),
                        eyeCfg.texLeft,   path => { eyeCfg.texLeft   = path; isDirty = true; RefreshPreview(); });
                    DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Right".Translate(),
                        eyeCfg.texRight,  path => { eyeCfg.texRight  = path; isDirty = true; RefreshPreview(); });
                    DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Up".Translate(),
                        eyeCfg.texUp,     path => { eyeCfg.texUp     = path; isDirty = true; RefreshPreview(); });
                    DrawEyeDirTexRow(ref y, width, "CS_Studio_Face_EyeDir_Down".Translate(),
                        eyeCfg.texDown,   path => { eyeCfg.texDown   = path; isDirty = true; RefreshPreview(); });
                }

                // 删除整个配置按钮
                y += 4f;
                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (Widgets.ButtonText(new Rect(0f, y, width, 24f), "CS_Studio_Face_EyeDir_Remove".Translate()))
                {
                    fc.eyeDirectionConfig = null;
                    isDirty = true;
                    RefreshPreview();
                }
                GUI.color = Color.white;
                y += 28f;
            }

            viewRect.height = Mathf.Max(y + 10f, contentRect.height - 4f);
            Widgets.EndScrollView();
        }

        // ─────────────────────────────────────────────
        // 眼睛方向 UI 辅助
        // ─────────────────────────────────────────────

        /// <summary>绘制单行眼睛方向贴图输入行</summary>
        private void DrawEyeDirTexRow(ref float y, float width, string label, string currentPath, System.Action<string> onChanged)
        {
            float rowH = 24f;
            float labelW = 56f;
            float btnW   = 28f;
            float clearW = 20f;
            float pathW  = width - labelW - btnW - clearW - 6f;

            // 标签
            Widgets.Label(new Rect(0f, y + 3f, labelW, rowH), label);

            // 路径文本（只读显示，点击弹出 FileBrowser）
            string display = string.IsNullOrEmpty(currentPath) ? "—" : System.IO.Path.GetFileName(currentPath);
            Text.Font = GameFont.Tiny;
            GUI.color = string.IsNullOrEmpty(currentPath) ? UIHelper.SubtleColor : Color.white;
            Widgets.Label(new Rect(labelW + 2f, y + 5f, pathW, rowH), display);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // […] 选择贴图
            if (Widgets.ButtonText(new Rect(labelW + pathW + 2f, y + 2f, btnW, 20f), "…"))
            {
                string capturedPath = currentPath;
                Find.WindowStack.Add(new Dialog_FileBrowser(capturedPath, newPath =>
                {
                    if (!string.IsNullOrEmpty(newPath))
                        onChanged(newPath);
                }));
            }

            // [×] 清空
            if (!string.IsNullOrEmpty(currentPath))
            {
                if (Widgets.ButtonText(new Rect(labelW + pathW + btnW + 4f, y + 2f, clearW, 20f), "×"))
                    onChanged(string.Empty);
            }

            y += rowH;
        }

        /// <summary>弹出眼睛方向预览菜单</summary>
        private void OpenPreviewEyeDirectionMenu(CharacterStudio.Core.CompPawnSkin? comp)
        {
            var options = new List<FloatMenuOption>();

            // "自动" 选项
            options.Add(new FloatMenuOption("CS_Studio_Face_EyeDir_Auto".Translate(), () =>
            {
                comp?.SetPreviewEyeDirection(null);
                RefreshPreview();
            }));

            foreach (CharacterStudio.Core.EyeDirection dir in System.Enum.GetValues(typeof(CharacterStudio.Core.EyeDirection)))
            {
                var localDir = dir;
                options.Add(new FloatMenuOption(dir.ToString(), () =>
                {
                    comp?.SetPreviewEyeDirection(localDir);
                    RefreshPreview();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// 刷新渲染树快照
        /// </summary>
        private void RefreshRenderTree()
        {
            // 优先使用“原角色”目标 Pawn，避免树视图误指向预览人偶
            Pawn? pawn = targetPawn;

            // 仅当尚未绑定目标角色时，才回退到人偶
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
            if (cachedRootSnapshot == null) return;
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

        /// <summary>
        /// 绘制渲染节点行（递归）
        /// </summary>
        private float DrawNodeRow(RenderNodeSnapshot node, int indent, float y, float width, ref int rowIndex)
        {
            Rect rowRect = new Rect(0, y, width, TreeNodeHeight);
            
            // 绘制交替背景
            UIHelper.DrawAlternatingRowBackground(rowRect, rowIndex);
            rowIndex++;

            float indentOffset = indent * TreeIndentWidth;

            // 鼠标悬停高亮
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }

            // 选中高亮
            if (selectedNodePath == node.uniqueNodePath)
            {
                Widgets.DrawBox(rowRect, 1);
            }

            // 折叠/展开按钮
            bool hasChildren = node.children.Count > 0;
            bool isExpanded = expandedPaths.Contains(node.uniqueNodePath);
            
            Rect foldRect = new Rect(indentOffset, y + 2, 18, 18);
            if (hasChildren)
            {
                string foldIcon = isExpanded ? "▼" : "▶";
                if (Widgets.ButtonText(foldRect, foldIcon, false))
                {
                    if (isExpanded)
                        expandedPaths.Remove(node.uniqueNodePath);
                    else
                        expandedPaths.Add(node.uniqueNodePath);
                }
            }
            else
            {
                // 空白占位
                GUI.color = Color.gray;
                Widgets.Label(foldRect, "  •");
                GUI.color = Color.white;
            }

            // 可见性图标（眼睛）
            #pragma warning disable CS0618
            bool isHidden = workingSkin.hiddenPaths.Contains(node.uniqueNodePath) ||
                           workingSkin.hiddenTags.Contains(node.tagDefName);
            #pragma warning restore CS0618
            
            Rect eyeRect = new Rect(indentOffset + 20, y + 2, 18, 18);
            string eyeIcon = isHidden ? "◯" : "◉";
            GUI.color = isHidden ? Color.gray : Color.white;
            if (Widgets.ButtonText(eyeRect, eyeIcon, false))
            {
                ToggleNodeVisibility(node);
            }
            GUI.color = Color.white;
            TooltipHandler.TipRegion(eyeRect, isHidden ? "CS_Studio_Tip_ShowNode".Translate() : "CS_Studio_Tip_HideNode".Translate());

            // 节点名称
            Rect nameRect = new Rect(indentOffset + 42, y, width - indentOffset - 44, TreeNodeHeight);
            string displayName = GetNodeDisplayName(node);
            
            // 根据节点类型着色
            GUI.color = GetNodeColor(node);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, displayName);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            // 点击选中
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

                selectedLayerIndex = -1; // 清除自定义图层选择
                selectedLayerIndices.Clear();
            }

            // 右键菜单
            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                ShowNodeContextMenu(node);
                Event.current.Use();
            }

            // Tooltip
            TooltipHandler.TipRegion(rowRect, "CS_Studio_Node_Tooltip".Translate(node.uniqueNodePath, node.workerClass, node.texPath ?? "CS_Studio_None".Translate()));

            y += TreeNodeHeight;

            // 递归绘制子节点
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
        /// 绘制自定义图层行
        /// </summary>
        private float DrawCustomLayerRow(PawnLayerConfig layer, int index, float y, float width)
        {
            Rect rowRect = new Rect(0, y, width, TreeNodeHeight);

            // 绘制交替背景
            UIHelper.DrawAlternatingRowBackground(rowRect, index);

            // 鼠标悬停高亮
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
            }

            // 选中高亮
            if (selectedLayerIndices.Contains(index) || selectedLayerIndex == index)
            {
                Widgets.DrawBox(rowRect, 1);
            }

            // 自定义图层图标
            GUI.color = new Color(0.5f, 1f, 0.5f);
            Widgets.Label(new Rect(4, y + 2, 18, 18), "★");
            GUI.color = Color.white;

            // 可见性切换
            Rect visRect = new Rect(24, y + 2, 18, 18);
            bool visible = layer.visible;
            string visIcon = visible ? "◉" : "◯";
            GUI.color = visible ? Color.white : Color.gray;
            if (Widgets.ButtonText(visRect, visIcon, false))
            {
                bool newVisible = !layer.visible;
                layer.visible = newVisible;
                if (selectedLayerIndices.Contains(index))
                {
                    ApplyToOtherSelectedLayers(index, l => l.visible = newVisible);
                }
                isDirty = true;
                RefreshPreview();
            }
            GUI.color = Color.white;

            // 图层名称
            Rect nameRect = new Rect(46, y, width - 70, TreeNodeHeight);
            string displayName = string.IsNullOrEmpty(layer.layerName) ? GetDefaultLayerLabel(index) : layer.layerName;
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, displayName);
            Text.Anchor = TextAnchor.UpperLeft;

            // 删除按钮
            if (Widgets.ButtonText(new Rect(width - 22, y + 1, 20, 20), "×"))
            {
                CaptureUndoSnapshot();
                workingSkin.layers.RemoveAt(index);
                RemapSelectionAfterRemoveIndex(index);
                isDirty = true;
                RefreshPreview();
            }

            // 左键点击：单选 / Shift 多选
            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                HandleLayerRowLeftClick(index);
                selectedBaseSlotType = null;
                Event.current.Use();
            }

            // 右键菜单
            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                ShowCustomLayerContextMenu(layer, index);
                Event.current.Use();
            }

            return y + TreeNodeHeight;
        }

        /// <summary>
        /// 获取节点显示名称
        /// </summary>
        private string GetNodeDisplayName(RenderNodeSnapshot node)
        {
            if (!string.IsNullOrEmpty(node.tagDefName) && node.tagDefName != "Untagged")
            {
                return $"{node.tagDefName}";
            }
            
            // 从 workerClass 提取简短名称
            string workerName = node.workerClass;
            if (workerName.Contains("."))
            {
                workerName = workerName.Substring(workerName.LastIndexOf('.') + 1);
            }
            if (workerName.StartsWith("PawnRenderNodeWorker_"))
            {
                workerName = workerName.Substring("PawnRenderNodeWorker_".Length);
            }
            return workerName;
        }

        /// <summary>
        /// 根据节点类型获取颜色
        /// </summary>
        private Color GetNodeColor(RenderNodeSnapshot node)
        {
            if (TryResolveBaseSlotType(node) != null)
            {
                return new Color(0.72f, 0.88f, 1f);
            }

            string worker = node.workerClass;
            
            if (worker.Contains("Head")) return new Color(1f, 0.8f, 0.6f);
            if (worker.Contains("Body")) return new Color(0.8f, 1f, 0.8f);
            if (worker.Contains("Hair")) return new Color(1f, 0.9f, 0.5f);
            if (worker.Contains("Apparel")) return new Color(0.7f, 0.8f, 1f);
            if (worker.Contains("Attachment")) return new Color(1f, 0.7f, 1f);
            
            return Color.white;
        }

        private BaseAppearanceSlotType? TryResolveBaseSlotType(RenderNodeSnapshot node)
        {
            string tag = node.tagDefName ?? string.Empty;
            string label = node.debugLabel ?? string.Empty;
            string texPath = node.texPath ?? string.Empty;

            if (ContainsAny(tag, label, texPath, "body")) return BaseAppearanceSlotType.Body;
            if (ContainsAny(tag, label, texPath, "head")) return BaseAppearanceSlotType.Head;
            if (ContainsAny(tag, label, texPath, "hair")) return BaseAppearanceSlotType.Hair;
            if (ContainsAny(tag, label, texPath, "beard")) return BaseAppearanceSlotType.Beard;

            return null;
        }

        private static bool ContainsAny(string a, string b, string c, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if ((!string.IsNullOrEmpty(a) && a.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(b) && b.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c) && c.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 切换节点可见性
        /// </summary>
        private void ToggleNodeVisibility(RenderNodeSnapshot node)
        {
            if (workingSkin.hiddenPaths.Contains(node.uniqueNodePath))
            {
                workingSkin.hiddenPaths.Remove(node.uniqueNodePath);
                #pragma warning disable CS0618
                if (!string.IsNullOrEmpty(node.tagDefName))
                {
                    workingSkin.hiddenTags.Remove(node.tagDefName);
                }
                #pragma warning restore CS0618
                ShowStatus("CS_Studio_Msg_Shown".Translate(node.uniqueNodePath));
            }
            else
            {
                workingSkin.hiddenPaths.Add(node.uniqueNodePath);
                #pragma warning disable CS0618
                if (!string.IsNullOrEmpty(node.tagDefName) && !workingSkin.hiddenTags.Contains(node.tagDefName))
                {
                    workingSkin.hiddenTags.Add(node.tagDefName);
                }
                #pragma warning restore CS0618
                ShowStatus("CS_Studio_Msg_Hidden".Translate(node.uniqueNodePath));
            }
            isDirty = true;
            RefreshPreview();
            RefreshRenderTree();
        }

        /// <summary>
        /// 显示节点右键菜单
        /// </summary>
        private void ShowNodeContextMenu(RenderNodeSnapshot node)
        {
            var options = new List<FloatMenuOption>();

            // 复制路径
            options.Add(new FloatMenuOption("CS_Studio_Ctx_CopyPath".Translate(), () =>
            {
                GUIUtility.systemCopyBuffer = node.uniqueNodePath;
                ShowStatus("CS_Studio_Msg_Copied".Translate(node.uniqueNodePath));
            }));

            // 复制贴图路径
            if (!string.IsNullOrEmpty(node.texPath))
            {
                options.Add(new FloatMenuOption("CS_Studio_Ctx_CopyTexPath".Translate(), () =>
                {
                    GUIUtility.systemCopyBuffer = node.texPath;
                    ShowStatus("CS_Studio_Msg_Copied".Translate(node.texPath));
                }));
            }

            // 在此节点下挂载新层
            options.Add(new FloatMenuOption("CS_Studio_Prop_MountLayer".Translate(), () =>
            {
                var newLayer = new PawnLayerConfig
                {
                    layerName = GetMountedLayerLabel(node),
                    anchorPath = node.uniqueNodePath,
                    anchorTag = node.tagDefName ?? "Body"
                };
                workingSkin.layers.Add(newLayer);
                selectedLayerIndex = workingSkin.layers.Count - 1;
                selectedNodePath = "";
                isDirty = true;
                RefreshPreview();
                ShowStatus("CS_Studio_Msg_Appended".Translate(node.uniqueNodePath, "1")); // 修复: Translate 参数类型
            }));

            // 隐藏/显示
            #pragma warning disable CS0618
            bool isHidden = workingSkin.hiddenPaths.Contains(node.uniqueNodePath) ||
                           workingSkin.hiddenTags.Contains(node.tagDefName);
            #pragma warning restore CS0618
            
            options.Add(new FloatMenuOption(isHidden ? "CS_Studio_Prop_ShowNode".Translate() : "CS_Studio_Prop_HideNode".Translate(), () =>
            {
                ToggleNodeVisibility(node);
            }));

            // 展开/折叠子节点
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

        /// <summary>
        /// 显示自定义图层右键菜单
        /// </summary>
        private void ShowCustomLayerContextMenu(PawnLayerConfig layer, int index)
        {
            var options = new List<FloatMenuOption>();

            // 复制图层
            options.Add(new FloatMenuOption("CS_Studio_Ctx_CopyLayer".Translate(), () =>
            {
                CaptureUndoSnapshot();
                var copy = layer.Clone();
                copy.layerName += " (Copy)";
                workingSkin.layers.Insert(index + 1, copy);
                selectedLayerIndex = index + 1;
                selectedLayerIndices.Clear();
                selectedLayerIndices.Add(selectedLayerIndex);
                isDirty = true;
                RefreshPreview();
            }));

            // 上移
            if (index > 0)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveUp".Translate(), () =>
                {
                    selectedLayerIndex = index;
                    selectedLayerIndices.Clear();
                    selectedLayerIndices.Add(index);
                    MoveSelectedLayerUp();
                }));
            }

            // 下移
            if (index < workingSkin.layers.Count - 1)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveDown".Translate(), () =>
                {
                    selectedLayerIndex = index;
                    selectedLayerIndices.Clear();
                    selectedLayerIndices.Add(index);
                    MoveSelectedLayerDown();
                }));
            }

            // 删除
            options.Add(new FloatMenuOption("CS_Studio_Ctx_DeleteLayer".Translate(), () =>
            {
                CaptureUndoSnapshot();
                workingSkin.layers.RemoveAt(index);
                RemapSelectionAfterRemoveIndex(index);
                isDirty = true;
                RefreshPreview();
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // ─────────────────────────────────────────────
        // 预览面板
        // ─────────────────────────────────────────────

        private void DrawPreviewPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            // 标题
            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, ButtonHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "CS_Studio_Panel_Preview".Translate());
            Text.Font = GameFont.Small;

            // 控制按钮
            float btnY = rect.y + Margin + ButtonHeight + Margin;
            float btnWidth = 40f;
            float btnX = rect.x + Margin;

            // 旋转按钮
            if (Widgets.ButtonText(new Rect(btnX, btnY, btnWidth, ButtonHeight), "◀"))
            {
                previewRotation.Rotate(RotationDirection.Counterclockwise);
                RefreshPreview();
            }
            btnX += btnWidth + Margin;

            if (Widgets.ButtonText(new Rect(btnX, btnY, btnWidth, ButtonHeight), "▶"))
            {
                previewRotation.Rotate(RotationDirection.Clockwise);
                RefreshPreview();
            }
            btnX += btnWidth + Margin * 2;

            // 缩放按钮
            if (Widgets.ButtonText(new Rect(btnX, btnY, btnWidth, ButtonHeight), "-"))
            {
                previewZoom = Mathf.Max(0.5f, previewZoom - 0.1f);
            }
            btnX += btnWidth + Margin;

            if (Widgets.ButtonText(new Rect(btnX, btnY, btnWidth, ButtonHeight), "+"))
            {
                previewZoom = Mathf.Min(2f, previewZoom + 0.1f);
            }
            btnX += btnWidth + Margin;

            // 重置
            if (Widgets.ButtonText(new Rect(btnX, btnY, btnWidth + 20, ButtonHeight), "↺"))
            {
                previewRotation = Rot4.South;
                previewZoom = 1f;
                RefreshPreview();
            }

            // 表情控制
            btnY += ButtonHeight + Margin;
            if (mannequin != null)
            {
                Widgets.Label(new Rect(btnX, btnY + 4, 40, 24), "CS_Studio_Preview_Exp".Translate());
                if (Widgets.ButtonText(new Rect(btnX + 40, btnY, 140, 24), previewExpressionOverrideEnabled ? GetExpressionTypeLabel(previewExpression) : "CS_Studio_Face_PreviewAuto".Translate()))
                {
                    OpenPreviewExpressionMenu();
                }
            }

            // 预览区域
            float previewY = btnY + ButtonHeight + Margin;
            float previewHeight = rect.height - previewY + rect.y - Margin;
            Rect previewRect = new Rect(rect.x + Margin, previewY, rect.width - Margin * 2, previewHeight);

            // 绘制背景
            Widgets.DrawBoxSolid(previewRect, new Color(0.15f, 0.15f, 0.15f));

            // 绘制 Mannequin
            if (mannequin != null)
            {
                mannequin.DrawPreview(previewRect, previewRotation, previewZoom);
                DrawReferenceGhostOverlay(previewRect);
            }
            else
            {
                // 占位文本
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(previewRect, "CS_Studio_Status_Loading".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (targetPawn != null)
            {
                TooltipHandler.TipRegion(previewRect, "CS_Studio_Preview_RefHint".Translate());
            }

            // 处理交互输入
            HandlePreviewInput(previewRect);
            
            // 绘制选中图层的高亮指示
            DrawSelectedLayerHighlight(previewRect);
        }

        private void DrawReferenceGhostOverlay(Rect previewRect)
        {
            // 使用帧开始时预读的状态，避免 IMGUI TextField 拦截 Input.GetKey
            if (targetPawn == null || !isHoldingReferenceGhost)
            {
                return;
            }

            try
            {
                Vector2 portraitSize = new Vector2(256f, 256f);
                var portrait = PortraitsCache.Get(
                    targetPawn,
                    portraitSize,
                    previewRotation,
                    new Vector3(0f, 0f, 0.15f),
                    previewZoom
                );

                if (portrait == null)
                {
                    return;
                }

                float drawSize = Mathf.Min(previewRect.width, previewRect.height);

                // 按住 F 时：默认居中；若鼠标位于预览区域内则让参考虚影跟随鼠标
                float x = previewRect.x + (previewRect.width - drawSize) / 2f;
                float y = previewRect.y + (previewRect.height - drawSize) / 2f;
                if (Mouse.IsOver(previewRect))
                {
                    Vector2 mousePos = Event.current.mousePosition;
                    x = mousePos.x - drawSize * 0.5f;
                    y = mousePos.y - drawSize * 0.5f;

                    // 限制在预览框内，避免完全拖出可视区域
                    x = Mathf.Clamp(x, previewRect.x, previewRect.xMax - drawSize);
                    y = Mathf.Clamp(y, previewRect.y, previewRect.yMax - drawSize);
                }

                Rect drawRect = new Rect(x, y, drawSize, drawSize);

                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, ReferenceGhostAlpha);
                GUI.DrawTexture(drawRect, portrait, ScaleMode.ScaleToFit, true);
                GUI.color = prevColor;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 绘制参考虚影失败: {ex.Message}");
            }
        }

        private void DrawSelectedLayerHighlight(Rect previewRect)
        {
            if (selectedLayerIndex < 0 || selectedLayerIndex >= workingSkin.layers.Count) return;
            var layer = workingSkin.layers[selectedLayerIndex];
            if (!layer.visible) return;

            Vector2 center = previewRect.center;
            // 估算比例 (需与 TrySelectLayerAt 保持一致)
            float pixelsPerUnit = (Mathf.Min(previewRect.width, previewRect.height) / 1.5f) * previewZoom;
            Vector2 layerScreenPos = center + new Vector2(layer.offset.x, -layer.offset.z) * pixelsPerUnit;

            // 绘制黄色十字准星
            float size = 10f;
            Color highlightColor = new Color(1f, 0.8f, 0f, 0.6f);
            
            // 绘制
            GUI.color = highlightColor;
            // 横线
            Widgets.DrawLine(new Vector2(layerScreenPos.x - size, layerScreenPos.y), new Vector2(layerScreenPos.x + size, layerScreenPos.y), highlightColor, 2f);
            // 竖线
            Widgets.DrawLine(new Vector2(layerScreenPos.x, layerScreenPos.y - size), new Vector2(layerScreenPos.x, layerScreenPos.y + size), highlightColor, 2f);
            // 中心点
            Widgets.DrawBoxSolid(new Rect(layerScreenPos.x - 2, layerScreenPos.y - 2, 4, 4), highlightColor);
            
            GUI.color = Color.white;
        }

        private void HandlePreviewInput(Rect rect)
        {
            if (Mouse.IsOver(rect))
            {
                // 滚轮缩放
                if (Event.current.type == EventType.ScrollWheel)
                {
                    float delta = Event.current.delta.y;
                    if (delta > 0) previewZoom /= 1.1f;
                    else previewZoom *= 1.1f;
                    previewZoom = Mathf.Clamp(previewZoom, 0.1f, 5f);
                    Event.current.Use();
                }

                // Shift + 左键点击选中 (已根据反馈移除，但为了代码完整性保留方法定义，此处注释掉调用)
                /*
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.shift)
                {
                    TrySelectLayerAt(Event.current.mousePosition, rect);
                    Event.current.Use();
                }
                */

                // 鼠标拖拽调整偏移（支持多选同步；基础槽位与图层保持一致）
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 0 && !Event.current.shift)
                {
                    Vector2 delta = Event.current.delta;
 
                    // 灵敏度系数 (根据缩放调整)
                    float sensitivity = 0.005f / previewZoom;
                    float dx = delta.x * sensitivity;
                    float dz = -delta.y * sensitivity; // 屏幕Y向下，世界Z向上
 
                    var targets = GetSelectedLayerTargets();
                    if (targets.Count > 0)
                    {
                        foreach (int idx in targets)
                        {
                            var layer = workingSkin.layers[idx];
                            layer.offset.x += dx;
                            layer.offset.z += dz;
                        }
 
                        isDirty = true;
                        RefreshPreview();
                        Event.current.Use();
                    }
                    else if (TryApplyDragToSelectedBaseSlot(dx, dz))
                    {
                        Event.current.Use();
                    }
                }
            }

            // 方向键微调 (无需鼠标悬停，只要有选中的图层；支持多选同步)
            if (Event.current.type == EventType.KeyDown)
            {
                var targets = GetSelectedLayerTargets();
                if (targets.Count > 0)
                {
                    float step = 0.005f;
                    if (Event.current.shift) step *= 10f; // Shift 加速

                    float dx = 0f;
                    float dz = 0f;
                    bool handled = true;
                    switch (Event.current.keyCode)
                    {
                        case KeyCode.LeftArrow: dx = -step; break;
                        case KeyCode.RightArrow: dx = step; break;
                        case KeyCode.UpArrow: dz = step; break;
                        case KeyCode.DownArrow: dz = -step; break;
                        default: handled = false; break;
                    }

                    if (handled)
                    {
                        foreach (int idx in targets)
                        {
                            var layer = workingSkin.layers[idx];
                            layer.offset.x += dx;
                            layer.offset.z += dz;
                        }

                        isDirty = true;
                        RefreshPreview();
                        Event.current.Use();
                    }
                }
            }
        }

        private bool TryApplyDragToSelectedBaseSlot(float dx, float dz)
        {
            if (selectedBaseSlotType == null)
            {
                return false;
            }

            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            var slot = workingSkin.baseAppearance.GetSlot(selectedBaseSlotType.Value);
            Vector3 offset = GetEditableOffsetForPreview(slot);
            offset.x += dx;
            offset.z += dz;
            SetEditableOffsetForPreview(slot, offset);

            isDirty = true;
            RefreshPreview();
            return true;
        }

        private Vector3 GetEditableOffsetForPreview(BaseAppearanceSlotConfig slot)
        {
            if (previewRotation == Rot4.North)
            {
                return slot.offsetNorth;
            }

            if (previewRotation == Rot4.East || previewRotation == Rot4.West)
            {
                return slot.offsetEast;
            }

            return slot.offset;
        }

        private void SetEditableOffsetForPreview(BaseAppearanceSlotConfig slot, Vector3 value)
        {
            if (previewRotation == Rot4.North)
            {
                slot.offsetNorth = value;
                return;
            }

            if (previewRotation == Rot4.East || previewRotation == Rot4.West)
            {
                slot.offsetEast = value;
                return;
            }

            slot.offset = value;
        }
 
        private void TrySelectLayerAt(Vector2 mousePos, Rect previewRect)
        {
            // 简单的距离检测选中算法
            // 假设 Pawn 在预览框中心
            Vector2 center = previewRect.center;
            
            // 估算世界单位到屏幕像素的转换比例
            // 假设标准 Pawn 高度约 1.8 单位，占满预览框时 height 对应 1.8
            // 实际上还要考虑 previewZoom
            float pixelsPerUnit = (Mathf.Min(previewRect.width, previewRect.height) / 1.5f) * previewZoom;

            float minDistance = 30f; // 选中阈值 (像素)
            int bestIndex = -1;

            for (int i = 0; i < workingSkin.layers.Count; i++)
            {
                var layer = workingSkin.layers[i];
                if (!layer.visible) continue;

                // 计算图层的大致屏幕位置
                // 注意：这里只考虑了 Offset，没有考虑 Body/Head 等锚点的基础位置
                // 这是一个简化的估算，假设用户主要通过 Offset 调整位置
                Vector2 layerScreenPos = center + new Vector2(layer.offset.x, -layer.offset.z) * pixelsPerUnit;

                float dist = Vector2.Distance(mousePos, layerScreenPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestIndex = i;
                }
            }

            if (bestIndex != -1)
            {
                selectedLayerIndex = bestIndex;
                selectedNodePath = ""; // 清除节点选择
                // 自动滚动到列表位置
                // layerScrollPos.y = bestIndex * TreeNodeHeight; // 简单估算
                ShowStatus("CS_Studio_Msg_SelectedLayer".Translate(workingSkin.layers[bestIndex].layerName));
            }
        }

        // ─────────────────────────────────────────────
        // 属性面板
        // ─────────────────────────────────────────────

        private void DrawPropertiesPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            // 标题
            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, ButtonHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "CS_Studio_Panel_Properties".Translate());
            Text.Font = GameFont.Small;

            // 快速折叠/展开按钮
            float expandBtnWidth = 24f;
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - Margin - expandBtnWidth * 2 - 4, rect.y + Margin, expandBtnWidth, ButtonHeight), "+"))
            {
                // 展开全部 (清空折叠列表)
                collapsedSections.Clear();
            }
            TooltipHandler.TipRegion(new Rect(rect.x + rect.width - Margin - expandBtnWidth * 2 - 4, rect.y + Margin, expandBtnWidth, ButtonHeight), "CS_Studio_Tip_ExpandAll".Translate());

            if (Widgets.ButtonText(new Rect(rect.x + rect.width - Margin - expandBtnWidth, rect.y + Margin, expandBtnWidth, ButtonHeight), "-"))
            {
                // 折叠全部 (添加所有已知部分)
                var allSections = new[] { "Base", "Transform", "EastOffset", "NorthOffset", "Misc", "Animation", "HideVanilla", "NodeInfo", "NodeActions", "NodeRuntime" };
                foreach (var s in allSections) collapsedSections.Add(s);
            }
            TooltipHandler.TipRegion(new Rect(rect.x + rect.width - Margin - expandBtnWidth, rect.y + Margin, expandBtnWidth, ButtonHeight), "CS_Studio_Tip_CollapseAll".Translate());

            // 如果选中了渲染树节点，显示节点信息
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
 
            // 检查是否有选中的图层
            if (selectedLayerIndex < 0 || selectedLayerIndex >= workingSkin.layers.Count)
            {
                Rect hintRect = new Rect(rect.x + Margin, rect.y + 60, rect.width - Margin * 2, 40);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(hintRect, "CS_Studio_Msg_SelectEditorTarget".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
 
            var layer = workingSkin.layers[selectedLayerIndex];

            float propsY = rect.y + Margin + ButtonHeight + Margin;
            float propsHeight = rect.height - propsY + rect.y - Margin;
            Rect propsRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2, propsHeight);
            Rect viewRect = new Rect(0, 0, propsRect.width - 16, 750);

            Widgets.BeginScrollView(propsRect, ref propsScrollPos, viewRect);

            float y = 0;
            float width = viewRect.width;

            // 基本设置
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Base".Translate(), "Base"))
            {
                string oldName = layer.layerName;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Prop_LayerName".Translate(), ref layer.layerName);
                if (oldName != layer.layerName)
                {
                    CaptureUndoSnapshot();
                    isDirty = true;
                }

            UIHelper.DrawPropertyFieldWithButton(ref y, width, "CS_Studio_Prop_TexturePath".Translate(),
                layer.texPath, () => OnSelectTexture(layer));

            // 扩展锚点选项
            var anchorOptions = new[] { 
                "Head", "Body", "Hair", "Beard", 
                "Eyes", "Brow", "Mouth", "Nose", "Ear", "Jaw",
                "FaceTattoo", "BodyTattoo", "Apparel", "Headgear" 
            };

            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Prop_AnchorPoint".Translate(), layer.anchorTag,
                anchorOptions,
                tag => {
                    string key = $"CS_Studio_Anchor_{tag}";
                    return key.CanTranslate() ? key.Translate() : tag;
                },
                val =>
                {
                    layer.anchorTag = val;
                    ApplyToOtherSelectedLayers(l => l.anchorTag = val);
                    isDirty = true;
                    RefreshPreview();
                });
            } // End Base Section

            // 变换设置 (South - 默认方向)
            bool isSouthActive = previewRotation == Rot4.South;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Transform".Translate(), "Transform", isSouthActive))
            {
                float ox = layer.offset.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref ox, -1f, 1f);
                if (Math.Abs(ox - layer.offset.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offset.x = ox;
                    ApplyToOtherSelectedLayers(l => l.offset.x = ox);
                    isDirty = true;
                    RefreshPreview();
                }

                float oy = layer.offset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetYHeight".Translate(), ref oy, -1f, 1f);
                if (Math.Abs(oy - layer.offset.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offset.y = oy;
                    ApplyToOtherSelectedLayers(l => l.offset.y = oy);
                    isDirty = true;
                    RefreshPreview();
                }

                float oz = layer.offset.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref oz, -1f, 1f);
                if (Math.Abs(oz - layer.offset.z) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offset.z = oz;
                    ApplyToOtherSelectedLayers(l => l.offset.z = oz);
                    isDirty = true;
                    RefreshPreview();
                }
            }

            // 侧视图偏移 (East/West)
            bool isEastActive = previewRotation == Rot4.East || previewRotation == Rot4.West;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_EastOffset".Translate(), "EastOffset", isEastActive))
            {
                float ex = layer.offsetEast.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref ex, -1f, 1f);
                if (Math.Abs(ex - layer.offsetEast.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetEast.x = ex;
                    ApplyToOtherSelectedLayers(l => l.offsetEast.x = ex);
                    isDirty = true;
                    RefreshPreview();
                }

                float ey = layer.offsetEast.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref ey, -1f, 1f);
                if (Math.Abs(ey - layer.offsetEast.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetEast.y = ey;
                    ApplyToOtherSelectedLayers(l => l.offsetEast.y = ey);
                    isDirty = true;
                    RefreshPreview();
                }

                float ez = layer.offsetEast.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref ez, -1f, 1f);
                if (Math.Abs(ez - layer.offsetEast.z) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetEast.z = ez;
                    ApplyToOtherSelectedLayers(l => l.offsetEast.z = ez);
                    isDirty = true;
                    RefreshPreview();
                }
            }

            // 北向偏移 (North)
            bool isNorthActive = previewRotation == Rot4.North;
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_NorthOffset".Translate(), "NorthOffset", isNorthActive))
            {
                float nx = layer.offsetNorth.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref nx, -1f, 1f);
                if (Math.Abs(nx - layer.offsetNorth.x) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetNorth.x = nx;
                    ApplyToOtherSelectedLayers(l => l.offsetNorth.x = nx);
                    isDirty = true;
                    RefreshPreview();
                }

                float ny = layer.offsetNorth.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref ny, -1f, 1f);
                if (Math.Abs(ny - layer.offsetNorth.y) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetNorth.y = ny;
                    ApplyToOtherSelectedLayers(l => l.offsetNorth.y = ny);
                    isDirty = true;
                    RefreshPreview();
                }

                float nz = layer.offsetNorth.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref nz, -1f, 1f);
                if (Math.Abs(nz - layer.offsetNorth.z) > 0.0001f)
                {
                    CaptureUndoSnapshot();
                    layer.offsetNorth.z = nz;
                    ApplyToOtherSelectedLayers(l => l.offsetNorth.z = nz);
                    isDirty = true;
                    RefreshPreview();
                }
            }

            // 其他设置
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Misc".Translate(), "Misc"))
            {
                float s = layer.scale.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_Scale".Translate(), ref s, 0.1f, 3f);
                if (s != layer.scale.x)
                {
                    Vector2 desiredScale = new Vector2(s, s);
                    layer.scale = desiredScale;
                    ApplyToOtherSelectedLayers(l => l.scale = desiredScale);
                    isDirty = true;
                    RefreshPreview();
                }

                float rotation = layer.rotation;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_BaseSlot_Rotation".Translate(), ref rotation, -180f, 180f, "F0");
                if (Mathf.Abs(rotation - layer.rotation) > 0.01f)
                {
                    layer.rotation = rotation;
                    ApplyToOtherSelectedLayers(l => l.rotation = rotation);
                    isDirty = true;
                    RefreshPreview();
                }

                // 扩大 DrawOrder 范围以覆盖 Body(0), Head(50) 等基准层级
                    float newDrawOrder = layer.drawOrder;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_DrawOrder".Translate(), ref newDrawOrder, -200f, 200f, "F0");
                    if (Mathf.Abs(newDrawOrder - layer.drawOrder) > 0.0001f)
                    {
                        layer.drawOrder = newDrawOrder;
                        ApplyToOtherSelectedLayers(l => l.drawOrder = newDrawOrder);
                        isDirty = true;
                        RefreshPreview();
                    }
                    
                    // Worker 选择
                    string[] workers = { "Default", "FaceComponent" };
                    string currentWorker = layer.workerClass == typeof(CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent) ? "FaceComponent" : "Default";
                    
                    UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Prop_Worker".Translate(), currentWorker, workers,
                        GetWorkerLabel,
                        val => {
                            var newWorker = val == "FaceComponent"
                                ? typeof(CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent)
                                : null;
                            layer.workerClass = newWorker;
                            ApplyToOtherSelectedLayers(l => l.workerClass = newWorker);
                            isDirty = true;
                            RefreshPreview();
                        });
    
                    // FaceComponent 不再需要组件类型选择（整张头部贴图切换模式）
    
#pragma warning disable CS0618 // colorType 仅用于旧版图层编辑兼容
                    UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Prop_ColorType".Translate(), layer.colorType,
                        (LayerColorType[])Enum.GetValues(typeof(LayerColorType)),
                        type => $"CS_Studio_ColorType_{type}".Translate(),
                        val => { layer.colorType = val; ApplyToOtherSelectedLayers(l => l.colorType = val); isDirty = true; RefreshPreview(); });
    
                    // 仅当颜色类型为 Custom 时显示颜色选择器
                    if (layer.colorType == LayerColorType.Custom)
                    {
#pragma warning restore CS0618
                        UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_Prop_CustomColor".Translate(), layer.customColor,
                            col =>
                            {
                                layer.customColor = col;
                                ApplyToOtherSelectedLayers(l => l.customColor = col);
                                isDirty = true;
                                RefreshPreview();
                            });
                        
                        // 绘制第二颜色（Mask）选择器
                        UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_BaseSlot_SecondColorMask".Translate(), layer.customColorTwo,
                            col =>
                            {
                                layer.customColorTwo = col;
                                ApplyToOtherSelectedLayers(l => l.customColorTwo = col);
                                isDirty = true;
                                RefreshPreview();
                            });
                    }
    
                    bool flip = layer.flipHorizontal;
                    UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_FlipHorizontal".Translate(), ref flip);
                    if (flip != layer.flipHorizontal)
                    {
                        layer.flipHorizontal = flip;
                        ApplyToOtherSelectedLayers(l => l.flipHorizontal = flip);
                        isDirty = true;
                        RefreshPreview();
                    }
                } // End Misc Section
    
                // 动画设置
                if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Animation".Translate(), "Animation"))
                {
                    // 动画类型选择
                    UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Anim_Type".Translate(), layer.animationType,
                        (LayerAnimationType[])Enum.GetValues(typeof(LayerAnimationType)),
                        type => $"CS_Studio_Anim_{type}".Translate(),
                        val =>
                        {
                            layer.animationType = val;
                            ApplyToOtherSelectedLayers(l => l.animationType = val);
                            isDirty = true;
                            RefreshPreview();
                        });
    
                    // 仅当选择了动画类型时显示其他参数
                    if (layer.animationType != LayerAnimationType.None)
                    {
                        // 频率
                        float freq = layer.animFrequency;
                        UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_Frequency".Translate(), ref freq, 0.1f, 5f);
                        if (Math.Abs(freq - layer.animFrequency) > 0.0001f)
                        {
                            CaptureUndoSnapshot();
                            layer.animFrequency = freq;
                            ApplyToOtherSelectedLayers(l => l.animFrequency = freq);
                            isDirty = true;
                            RefreshPreview();
                        }
    
                        // 幅度
                        float amp = layer.animAmplitude;
                        UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_Amplitude".Translate(), ref amp, 1f, 45f);
                        if (Math.Abs(amp - layer.animAmplitude) > 0.0001f)
                        {
                            CaptureUndoSnapshot();
                            layer.animAmplitude = amp;
                            ApplyToOtherSelectedLayers(l => l.animAmplitude = amp);
                            isDirty = true;
                            RefreshPreview();
                        }
    
                        // 速度（仅 Twitch 类型使用）
                        if (layer.animationType == LayerAnimationType.Twitch)
                        {
                            float speed = layer.animSpeed;
                            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_Speed".Translate(), ref speed, 0.1f, 3f);
                            if (Math.Abs(speed - layer.animSpeed) > 0.0001f)
                            {
                                CaptureUndoSnapshot();
                                layer.animSpeed = speed;
                                ApplyToOtherSelectedLayers(l => l.animSpeed = speed);
                                isDirty = true;
                                RefreshPreview();
                            }
                        }
    
                        // 相位偏移（用于多图层错开动画）
                        float phase = layer.animPhaseOffset;
                        UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_PhaseOffset".Translate(), ref phase, 0f, 1f);
                        if (Math.Abs(phase - layer.animPhaseOffset) > 0.0001f)
                        {
                            CaptureUndoSnapshot();
                            layer.animPhaseOffset = phase;
                            ApplyToOtherSelectedLayers(l => l.animPhaseOffset = phase);
                            isDirty = true;
                            RefreshPreview();
                        }
    
                        // 位移动画开关
                        bool affectsOffset = layer.animAffectsOffset;
                        UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Anim_AffectsOffset".Translate(), ref affectsOffset);
                        if (affectsOffset != layer.animAffectsOffset)
                        {
                            CaptureUndoSnapshot();
                            layer.animAffectsOffset = affectsOffset;
                            ApplyToOtherSelectedLayers(l => l.animAffectsOffset = affectsOffset);
                            isDirty = true;
                            RefreshPreview();
                        }
    
                        // 位移幅度（仅当启用位移时显示）
                        if (layer.animAffectsOffset)
                        {
                            float offsetAmp = layer.animOffsetAmplitude;
                            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Anim_OffsetAmplitude".Translate(), ref offsetAmp, 0.001f, 0.1f, "F3");
                            if (Math.Abs(offsetAmp - layer.animOffsetAmplitude) > 0.0001f)
                            {
                                CaptureUndoSnapshot();
                                layer.animOffsetAmplitude = offsetAmp;
                                ApplyToOtherSelectedLayers(l => l.animOffsetAmplitude = offsetAmp);
                                isDirty = true;
                                RefreshPreview();
                            }
                        }
                    }
                }
    
                if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_HideVanilla".Translate(), "HideVanilla"))
                {
                    // 优先显示 hiddenPaths（精准隐藏）
                    if (workingSkin.hiddenPaths != null && workingSkin.hiddenPaths.Count > 0)
                    {
                        Widgets.Label(new Rect(0, y, propsRect.width - 20, 22), "CS_Studio_Hide_HiddenPaths".Translate());
                        y += 24;
                        foreach (var path in workingSkin.hiddenPaths.ToList())
                        {
                            Rect pathRect = new Rect(0, y, propsRect.width - 50, 22);
                            Widgets.Label(pathRect, $"  • {path}");
                            if (Widgets.ButtonText(new Rect(propsRect.width - 45, y, 40, 20), "×"))
                            {
                                workingSkin.hiddenPaths.Remove(path);
                                isDirty = true;
                                RefreshPreview();
                                RefreshRenderTree();
                            }
                            y += 24;
                        }
                    }
                    else
                    {
                        GUI.color = Color.gray;
                        Widgets.Label(new Rect(0, y, propsRect.width - 20, 22), "CS_Studio_Hide_NoHiddenPaths".Translate());
                        GUI.color = Color.white;
                        y += 24;
                    }

                    // 兼容旧 hiddenTags
                    #pragma warning disable CS0618
                    if (workingSkin.hiddenTags != null && workingSkin.hiddenTags.Count > 0)
                    {
                        Widgets.Label(new Rect(0, y, propsRect.width - 20, 22), "CS_Studio_Hide_HiddenTagsCompat".Translate());
                        y += 24;
                        foreach (var tag in workingSkin.hiddenTags.ToList())
                        {
                            Rect tagRect = new Rect(0, y, propsRect.width - 50, 22);
                            Widgets.Label(tagRect, $"  • {tag}");
                            if (Widgets.ButtonText(new Rect(propsRect.width - 45, y, 40, 20), "×"))
                            {
                                workingSkin.hiddenTags.Remove(tag);
                                isDirty = true;
                                RefreshPreview();
                                RefreshRenderTree();
                            }
                            y += 24;
                        }
                    }
                    #pragma warning restore CS0618

                    // 添加隐藏标签按钮（兼容入口）
                    if (Widgets.ButtonText(new Rect(0, y, propsRect.width - 20, 24), "CS_Studio_Btn_AddHidden".Translate()))
                    {
                        ShowHiddenTagsMenu();
                    }
                    y += 28;
                }

            Widgets.EndScrollView();
        }

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

            float propsY = rect.y + Margin + ButtonHeight + Margin;
            float propsHeight = rect.height - propsY + rect.y - Margin;
            Rect propsRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2, propsHeight);
            Rect viewRect = new Rect(0, 0, propsRect.width - 16, 700); // 增加高度以适应折叠展开

            Widgets.BeginScrollView(propsRect, ref propsScrollPos, viewRect);

            float y = 0;
            // 标签宽度自适应：取所有标签中最宽者，确保全部完整显示
            Text.Font = GameFont.Small;
            float labelWidth = new[]
            {
                "CS_Studio_Info_Path",
                "CS_Studio_Prop_NodeTag",
                "CS_Studio_Info_Worker",
                "CS_Studio_Prop_NodeTexture",
                "CS_Studio_Prop_NodeChildren"
            }.Max(k => Text.CalcSize((k).Translate()).x) + 10f;
            float fieldWidth = propsRect.width - labelWidth - 20;

            // 节点信息
            if (DrawCollapsibleSection(ref y, propsRect.width - 20, "CS_Studio_Prop_NodeInfo".Translate(), "NodeInfo"))
            {
                // 路径（可复制）
                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Info_Path".Translate());
                Rect pathRect = new Rect(labelWidth, y, fieldWidth - 32, 24);
                // 长路径用 Tiny 字体，确保完整显示
                Text.Font = GameFont.Tiny;
                Widgets.Label(pathRect, node.uniqueNodePath);
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(labelWidth + fieldWidth - 28, y, 28, 22), "📋"))
                {
                    GUIUtility.systemCopyBuffer = node.uniqueNodePath;
                    ShowStatus("CS_Studio_Msg_PathCopied".Translate());
                }
                y += 26;

                // 标签
                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Prop_NodeTag".Translate() + ":");
                Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), node.tagDefName ?? "CS_Studio_None".Translate());
                y += 26;

                // Worker 类型
                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Info_Worker".Translate());
                string shortWorker = node.workerClass;
                if (shortWorker.Contains("."))
                    shortWorker = shortWorker.Substring(shortWorker.LastIndexOf('.') + 1);
                Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), shortWorker);
                y += 26;

                // 贴图路径
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

                // 子节点数
                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Prop_NodeChildren".Translate() + ":");
                Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), node.children.Count.ToString());
                y += 30;
            }

            // 操作按钮
            if (DrawCollapsibleSection(ref y, propsRect.width - 20, "CS_Studio_Prop_Actions".Translate(), "NodeActions"))
            {
                // 隐藏/显示按钮
                #pragma warning disable CS0618
                string? nodeTag = node.tagDefName;
                bool isHiddenByLegacyTag = false;
                if (!string.IsNullOrEmpty(nodeTag))
                {
                    string legacyHiddenTag = nodeTag!;
                    isHiddenByLegacyTag = workingSkin.hiddenTags.Contains(legacyHiddenTag);
                }

                bool isHidden = workingSkin.hiddenPaths.Contains(node.uniqueNodePath) || isHiddenByLegacyTag;
                #pragma warning restore CS0618
                
                if (Widgets.ButtonText(new Rect(0, y, propsRect.width - 20, 28), isHidden ? "CS_Studio_Prop_ShowNode".Translate() : "CS_Studio_Prop_HideNode".Translate()))
                {
                    ToggleNodeVisibility(node);
                }
                y += 32;

                // 在此节点挂载图层
                if (Widgets.ButtonText(new Rect(0, y, propsRect.width - 20, 28), "CS_Studio_Prop_MountLayer".Translate()))
                {
                    var newLayer = new PawnLayerConfig
                    {
                        layerName = GetMountedLayerLabel(node),
                        anchorPath = node.uniqueNodePath,
                        anchorTag = node.tagDefName ?? "Body"
                    };
                    workingSkin.layers.Add(newLayer);
                    selectedLayerIndex = workingSkin.layers.Count - 1;
                    selectedNodePath = "";
                    isDirty = true;
                    RefreshPreview();
                    ShowStatus("CS_Studio_Msg_Appended".Translate(node.uniqueNodePath, "1"));
                }
                y += 32;
            }

            // ===== 运行时数据探针 (只读) =====
            if (DrawCollapsibleSection(ref y, propsRect.width - 20, "CS_Studio_Node_RuntimeData".Translate(), "NodeRuntime"))
            {
                if (node.runtimeDataValid)
                {
                    // 运行时偏移
                    bool offsetNonDefault = node.runtimeOffset != Vector3.zero;
                    GUI.color = offsetNonDefault ? Color.yellow : Color.white;
                    Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Runtime_Offset".Translate());
                    Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24),
                        $"({node.runtimeOffset.x:F3}, {node.runtimeOffset.y:F3}, {node.runtimeOffset.z:F3})");
                    GUI.color = Color.white;
                    y += 24;

                    // 运行时缩放
                    bool scaleNonDefault = node.runtimeScale != Vector3.one;
                    GUI.color = scaleNonDefault ? Color.yellow : Color.white;
                    Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Runtime_Scale".Translate());
                    Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24),
                        $"({node.runtimeScale.x:F3}, {node.runtimeScale.y:F3}, {node.runtimeScale.z:F3})");
                    GUI.color = Color.white;
                    y += 24;

                    // 运行时颜色
                    bool colorNonDefault = node.runtimeColor != Color.white;
                    GUI.color = colorNonDefault ? Color.yellow : Color.white;
                    Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Runtime_Color".Translate());
                    // 绘制颜色预览方块
                    Rect colorPreviewRect = new Rect(labelWidth, y + 2, 20, 20);
                    Widgets.DrawBoxSolid(colorPreviewRect, node.runtimeColor);
                    Widgets.DrawBox(colorPreviewRect, 1);
                    Widgets.Label(new Rect(labelWidth + 25, y, fieldWidth - 25, 24),
                        "CS_Studio_Runtime_ColorValue".Translate(node.runtimeColor.r.ToString("F2"), node.runtimeColor.g.ToString("F2"), node.runtimeColor.b.ToString("F2"), node.runtimeColor.a.ToString("F2")));
                    GUI.color = Color.white;
                    y += 24;

                    // 运行时旋转
                    bool rotNonDefault = Mathf.Abs(node.runtimeRotation) > 0.01f;
                    GUI.color = rotNonDefault ? Color.yellow : Color.white;
                    Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Runtime_Rotation".Translate());
                    Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), "CS_Studio_Runtime_RotationValue".Translate(node.runtimeRotation.ToString("F1")));
                    GUI.color = Color.white;
                    y += 24;

                    // 提示
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    Widgets.Label(new Rect(0, y, propsRect.width - 20, 16), "CS_Studio_Node_RuntimeNote".Translate());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    y += 20;
                }
                else
                {
                    // 无运行时数据
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

        private void DrawBaseAppearanceProperties(Rect rect, BaseAppearanceSlotType slotType)
        {
            workingSkin.baseAppearance ??= new BaseAppearanceConfig();
            var slot = workingSkin.baseAppearance.GetSlot(slotType);

            float propsY = rect.y + Margin + ButtonHeight + Margin;
            float propsHeight = rect.height - propsY + rect.y - Margin;
            Rect propsRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2, propsHeight);
            Rect viewRect = new Rect(0, 0, propsRect.width - 16, 760);

            Widgets.BeginScrollView(propsRect, ref propsScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            bool DrawPathFieldWithBrowser(ref float rowY, string label, ref string value, Action browseAction)
            {
                Rect rowRect = new Rect(0f, rowY, width, UIHelper.RowHeight);
                Text.Font = GameFont.Small;

                float actualLabelWidth = Mathf.Max(UIHelper.LabelWidth, Text.CalcSize(label).x + 10f);
                float buttonWidth = 30f;
                float spacing = 5f;
                float fieldWidth = Mathf.Max(40f, rowRect.width - actualLabelWidth - buttonWidth - spacing);

                Widgets.Label(new Rect(rowRect.x, rowRect.y, actualLabelWidth, 24f), label);

                string newValue = Widgets.TextField(
                    new Rect(rowRect.x + actualLabelWidth, rowRect.y, fieldWidth, 24f),
                    value ?? string.Empty);

                bool changed = false;
                if (newValue != value)
                {
                    value = UIHelper.SanitizeInput(newValue, 260);
                    changed = true;
                }

                if (Widgets.ButtonText(
                    new Rect(rowRect.x + actualLabelWidth + fieldWidth + spacing, rowRect.y, buttonWidth, 24f),
                    "..."))
                {
                    browseAction?.Invoke();
                }

                rowY += UIHelper.RowHeight;
                return changed;
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_BaseSlot_Section".Translate(BaseAppearanceUtility.GetDisplayName(slotType)), "BaseSlotBase"))
            {
                bool enabled = slot.enabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_BaseSlot_Enable".Translate(), ref enabled);
                if (enabled != slot.enabled)
                {
                    CaptureUndoSnapshot();
                    slot.enabled = enabled;
                    isDirty = true;
                    RefreshPreview();
                    RefreshRenderTree();
                }

                string texPath = slot.texPath ?? string.Empty;
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_Prop_TexturePath".Translate(), ref texPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(slot.texPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        slot.texPath = path ?? string.Empty;
                        slot.enabled = !string.IsNullOrWhiteSpace(slot.texPath);
                        isDirty = true;
                        RefreshPreview();
                        RefreshRenderTree();
                    }))))
                {
                    CaptureUndoSnapshot();
                    slot.texPath = texPath;
                    slot.enabled = !string.IsNullOrWhiteSpace(texPath);
                    isDirty = true;
                    RefreshPreview();
                    RefreshRenderTree();
                }

                string maskPath = slot.maskTexPath ?? string.Empty;
                if (DrawPathFieldWithBrowser(ref y, "CS_Studio_BaseSlot_MaskTexture".Translate(), ref maskPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(slot.maskTexPath ?? string.Empty, path =>
                    {
                        CaptureUndoSnapshot();
                        slot.maskTexPath = path ?? string.Empty;
                        isDirty = true;
                        RefreshPreview();
                        RefreshRenderTree();
                    }))))
                {
                    CaptureUndoSnapshot();
                    slot.maskTexPath = maskPath;
                    isDirty = true;
                    RefreshPreview();
                    RefreshRenderTree();
                }

                string shaderDefName = slot.shaderDefName ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_BaseSlot_Shader".Translate(), ref shaderDefName);
                if (shaderDefName != (slot.shaderDefName ?? string.Empty))
                {
                    CaptureUndoSnapshot();
                    slot.shaderDefName = shaderDefName;
                    isDirty = true;
                    RefreshPreview();
                }

                string anchorTag = BaseAppearanceUtility.GetAnchorTag(slotType);
                UIHelper.DrawPropertyLabel(ref y, width, "CS_Studio_Prop_AnchorPoint".Translate(), anchorTag);
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Transform".Translate(), "BaseSlotTransform", previewRotation == Rot4.South))
            {
                float ox = slot.offset.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref ox, -1f, 1f);
                if (Math.Abs(ox - slot.offset.x) > 0.0001f)
                {
                    slot.offset.x = ox;
                    isDirty = true;
                    RefreshPreview();
                }

                float oy = slot.offset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetYHeight".Translate(), ref oy, -1f, 1f);
                if (Math.Abs(oy - slot.offset.y) > 0.0001f)
                {
                    slot.offset.y = oy;
                    isDirty = true;
                    RefreshPreview();
                }

                float oz = slot.offset.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref oz, -1f, 1f);
                if (Math.Abs(oz - slot.offset.z) > 0.0001f)
                {
                    slot.offset.z = oz;
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_EastOffset".Translate(), "BaseSlotEast", previewRotation == Rot4.East || previewRotation == Rot4.West))
            {
                float ex = slot.offsetEast.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref ex, -1f, 1f);
                if (Math.Abs(ex - slot.offsetEast.x) > 0.0001f)
                {
                    slot.offsetEast.x = ex;
                    isDirty = true;
                    RefreshPreview();
                }

                float ey = slot.offsetEast.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref ey, -1f, 1f);
                if (Math.Abs(ey - slot.offsetEast.y) > 0.0001f)
                {
                    slot.offsetEast.y = ey;
                    isDirty = true;
                    RefreshPreview();
                }

                float ez = slot.offsetEast.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref ez, -1f, 1f);
                if (Math.Abs(ez - slot.offsetEast.z) > 0.0001f)
                {
                    slot.offsetEast.z = ez;
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_NorthOffset".Translate(), "BaseSlotNorth", previewRotation == Rot4.North))
            {
                float nx = slot.offsetNorth.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetX".Translate(), ref nx, -1f, 1f);
                if (Math.Abs(nx - slot.offsetNorth.x) > 0.0001f)
                {
                    slot.offsetNorth.x = nx;
                    isDirty = true;
                    RefreshPreview();
                }

                float ny = slot.offsetNorth.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetY".Translate(), ref ny, -1f, 1f);
                if (Math.Abs(ny - slot.offsetNorth.y) > 0.0001f)
                {
                    slot.offsetNorth.y = ny;
                    isDirty = true;
                    RefreshPreview();
                }

                float nz = slot.offsetNorth.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_OffsetZ".Translate(), ref nz, -1f, 1f);
                if (Math.Abs(nz - slot.offsetNorth.z) > 0.0001f)
                {
                    slot.offsetNorth.z = nz;
                    isDirty = true;
                    RefreshPreview();
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Misc".Translate(), "BaseSlotMisc"))
            {
                float scale = slot.scale.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Prop_Scale".Translate(), ref scale, 0.1f, 3f);
                if (Math.Abs(scale - slot.scale.x) > 0.0001f)
                {
                    slot.scale = new Vector2(scale, scale);
                    isDirty = true;
                    RefreshPreview();
                }

                float rotation = slot.rotation;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_BaseSlot_Rotation".Translate(), ref rotation, -180f, 180f, "F0");
                if (Math.Abs(rotation - slot.rotation) > 0.0001f)
                {
                    slot.rotation = rotation;
                    isDirty = true;
                    RefreshPreview();
                }

                float drawOrderOffset = slot.drawOrderOffset;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_BaseSlot_DrawOrderOffset".Translate(), ref drawOrderOffset, -50f, 50f, "F0");
                if (Math.Abs(drawOrderOffset - slot.drawOrderOffset) > 0.0001f)
                {
                    slot.drawOrderOffset = drawOrderOffset;
                    isDirty = true;
                    RefreshPreview();
                }

                bool flip = slot.flipHorizontal;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Prop_FlipHorizontal".Translate(), ref flip);
                if (flip != slot.flipHorizontal)
                {
                    slot.flipHorizontal = flip;
                    isDirty = true;
                    RefreshPreview();
                }

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_BaseSlot_PrimaryColorSource".Translate(), slot.colorSource,
                    (LayerColorSource[])Enum.GetValues(typeof(LayerColorSource)),
                    option => option.ToString(),
                    val =>
                    {
                        slot.colorSource = val;
                        isDirty = true;
                        RefreshPreview();
                    });

                if (slot.colorSource == LayerColorSource.Fixed)
                {
                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_BaseSlot_PrimaryColor".Translate(), slot.customColor, col =>
                    {
                        slot.customColor = col;
                        isDirty = true;
                        RefreshPreview();
                    });
                }

                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_BaseSlot_SecondaryColorSource".Translate(), slot.colorTwoSource,
                    (LayerColorSource[])Enum.GetValues(typeof(LayerColorSource)),
                    option => option.ToString(),
                    val =>
                    {
                        slot.colorTwoSource = val;
                        isDirty = true;
                        RefreshPreview();
                    });

                if (slot.colorTwoSource == LayerColorSource.Fixed)
                {
                    UIHelper.DrawPropertyColor(ref y, width, "CS_Studio_BaseSlot_SecondaryColor".Translate(), slot.customColorTwo, col =>
                    {
                        slot.customColorTwo = col;
                        isDirty = true;
                        RefreshPreview();
                    });
                }
            }

            Widgets.EndScrollView();
        }
 
        /// <summary>
        /// 按路径查找节点
        /// </summary>
        private RenderNodeSnapshot? FindNodeByPath(RenderNodeSnapshot root, string path)
        {
            if (root.uniqueNodePath == path)
                return root;

            foreach (var child in root.children)
            {
                var found = FindNodeByPath(child, path);
                if (found != null)
                    return found;
            }

            return null;
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
            else if (isDirty)
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

        // ─────────────────────────────────────────────
        // 属性绘制辅助方法
        // ─────────────────────────────────────────────



        // ─────────────────────────────────────────────
        // 事件处理
        // ─────────────────────────────────────────────

        private void OnNewSkin()
        {
            if (isDirty)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "CS_Studio_Msg_UnsavedChanges".Translate(),
                    () => CreateNewSkin(),
                    true));
            }
            else
            {
                CreateNewSkin();
            }
        }

        private void CreateNewSkin()
        {
            // 生成唯一的DefName
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            
            workingSkin = new PawnSkinDef
            {
                defName = $"CS_Skin_{uniqueId}",
                label = "CS_Studio_DefaultSkinLabel".Translate(),
                description = "",
                author = "",
                version = "1.0.0"
            };
            workingAbilities.Clear();
            selectedLayerIndex = -1;
            selectedLayerIndices.Clear();
            selectedNodePath = "";
            selectedBaseSlotType = null;
            currentTab = EditorTab.BaseAppearance;
            isDirty = false;
            exprPathBuffer.Clear();
            
            // 清除隐藏节点状态
            Rendering.Patch_PawnRenderTree.ClearHiddenNodes();

            // 强制将人偶重置为默认人类种族：
            // 若上次编辑的是其他种族皮肤，mannequin 仍保持那个种族的渲染树，
            // BaseAppearance 节点路径会找不到匹配项，导致贴图无法生效且预览不刷新。
            // ForceReset 不受同种族检查限制，始终销毁重建，彻底清除旧外观残留。
            mannequin?.ForceReset(RimWorld.ThingDefOf.Human);
            
            RefreshRenderTree();
            RefreshPreview();
        }


        private void OnExportMod()
        {
            bool hasCustomLayers = workingSkin.layers != null && workingSkin.layers.Count > 0;
            bool hasBaseSlots = workingSkin.baseAppearance != null && workingSkin.baseAppearance.EnabledSlots().Any();

            // 导出前验证
            if (!hasCustomLayers && !hasBaseSlots)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "CS_Studio_Warn_NoLayers".Translate(),
                    "CS_Studio_Btn_ContinueExport".Translate(),
                    () => Find.WindowStack.Add(new Dialog_ExportMod(workingSkin, workingAbilities)),
                    "CS_Studio_Btn_Cancel".Translate(),
                    null,
                    null,
                    false,
                    null,
                    null,
                    WindowLayer.Dialog
                ));
                return;
            }
            
            bool hasValidTexture = (workingSkin.layers?.Any(l => !string.IsNullOrEmpty(l.texPath)) ?? false)
                || (workingSkin.baseAppearance?.EnabledSlots().Any(s => !string.IsNullOrEmpty(s.texPath)) ?? false);
            if (!hasValidTexture)
            {
                ShowStatus("CS_Studio_Warn_NoTexture".Translate());
            }
            
            SyncAbilitiesToSkin();
            Find.WindowStack.Add(new Dialog_ExportMod(workingSkin, workingAbilities));
        }

        private void OnOpenSkinSettings()
        {
            SyncAbilitiesToSkin();
            Find.WindowStack.Add(new Dialog_SkinSettings(workingSkin, () => {
                isDirty = true;
                SyncAbilitiesFromSkin();
                RefreshPreview();
            }));
        }

        private void OnOpenLlmSettings()
        {
            Find.WindowStack.Add(new Dialog_LlmSettings(_ =>
            {
                isDirty = true;
                ShowStatus("CS_LLM_Settings_SaveSuccess".Translate());
            }));
        }

        private void OnResetParameters()
        {
            // 归位所有选中的图层和基础槽位的 Transform 参数
            var targets = GetSelectedLayerTargets();
            if (targets.Count > 0)
            {
                CaptureUndoSnapshot();
                foreach (int idx in targets)
                {
                    var layer = workingSkin.layers[idx];
                    layer.offset = Vector3.zero;
                    layer.offsetEast = Vector3.zero;
                    layer.offsetNorth = Vector3.zero;
                    layer.scale = Vector2.one;
                    layer.rotation = 0f;
                }
                isDirty = true;
                RefreshPreview();
                ShowStatus("CS_Studio_Msg_ParametersReset".Translate());
            }
            else if (selectedBaseSlotType != null)
            {
                CaptureUndoSnapshot();
                workingSkin.baseAppearance ??= new BaseAppearanceConfig();
                var slot = workingSkin.baseAppearance.GetSlot(selectedBaseSlotType.Value);
                slot.offset = Vector3.zero;
                slot.offsetEast = Vector3.zero;
                slot.offsetNorth = Vector3.zero;
                slot.scale = Vector2.one;
                slot.rotation = 0f;
                isDirty = true;
                RefreshPreview();
                ShowStatus("CS_Studio_Msg_ParametersReset".Translate());
            }
        }

        private void OnAddLayer()
        {
            CaptureUndoSnapshot();
            var newLayer = new PawnLayerConfig
            {
                layerName = GetDefaultLayerLabel(workingSkin.layers.Count),
                anchorTag = "Head"
            };
            workingSkin.layers.Add(newLayer);
            selectedLayerIndex = workingSkin.layers.Count - 1;
            selectedLayerIndices.Clear();
            selectedLayerIndices.Add(selectedLayerIndex);
            selectedNodePath = "";
            selectedBaseSlotType = null;
            currentTab = EditorTab.Layers;
            isDirty = true;
            RefreshPreview();
        }

        private void OnRemoveLayer()
        {
            if (selectedLayerIndex >= 0 && selectedLayerIndex < workingSkin.layers.Count)
            {
                int removedIndex = selectedLayerIndex;
                CaptureUndoSnapshot();
                workingSkin.layers.RemoveAt(removedIndex);
                RemapSelectionAfterRemoveIndex(removedIndex);
                isDirty = true;
                RefreshPreview();
            }
        }

        private void RemapSelectionAfterRemoveIndex(int removedIndex)
        {
            var remapped = new HashSet<int>();
            foreach (int idx in selectedLayerIndices)
            {
                if (idx == removedIndex) continue;
                remapped.Add(idx > removedIndex ? idx - 1 : idx);
            }
            selectedLayerIndices = remapped;

            if (selectedLayerIndex == removedIndex)
            {
                selectedLayerIndex = -1;
            }
            else if (selectedLayerIndex > removedIndex)
            {
                selectedLayerIndex--;
            }

            SanitizeLayerSelection();
        }

        private bool MoveSelectedLayerUp()
        {
            if (selectedLayerIndex <= 0 || selectedLayerIndex >= workingSkin.layers.Count)
            {
                return false;
            }

            CaptureUndoSnapshot();
            var layer = workingSkin.layers[selectedLayerIndex];
            workingSkin.layers.RemoveAt(selectedLayerIndex);
            workingSkin.layers.Insert(selectedLayerIndex - 1, layer);
            selectedLayerIndex--;
            selectedLayerIndices.Clear();
            selectedLayerIndices.Add(selectedLayerIndex);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private bool MoveSelectedLayerDown()
        {
            if (selectedLayerIndex < 0 || selectedLayerIndex >= workingSkin.layers.Count - 1)
            {
                return false;
            }

            CaptureUndoSnapshot();
            var layer = workingSkin.layers[selectedLayerIndex];
            workingSkin.layers.RemoveAt(selectedLayerIndex);
            workingSkin.layers.Insert(selectedLayerIndex + 1, layer);
            selectedLayerIndex++;
            selectedLayerIndices.Clear();
            selectedLayerIndices.Add(selectedLayerIndex);
            isDirty = true;
            RefreshPreview();
            return true;
        }

        private void ShowLayerOrderMenu()
        {
            if (selectedLayerIndex < 0) return;

            var options = new List<FloatMenuOption>();
            
            if (selectedLayerIndex > 0)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveUp".Translate(), () =>
                {
                    MoveSelectedLayerUp();
                }));
            }
            
            if (selectedLayerIndex < workingSkin.layers.Count - 1)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveDown".Translate(), () =>
                {
                    MoveSelectedLayerDown();
                }));
            }

            options.Add(new FloatMenuOption("CS_Studio_Panel_Duplicate".Translate(), () =>
            {
                CaptureUndoSnapshot();
                var layer = workingSkin.layers[selectedLayerIndex].Clone();
                layer.layerName += " (Copy)";
                workingSkin.layers.Insert(selectedLayerIndex + 1, layer);
                selectedLayerIndex++;
                selectedLayerIndices.Clear();
                selectedLayerIndices.Add(selectedLayerIndex);
                isDirty = true;
                RefreshPreview();
            }));

            if (options.Any())
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void OnSelectTexture(PawnLayerConfig layer)
        {
            Find.WindowStack.Add(new Dialog_FileBrowser(layer.texPath, path => {
                layer.texPath = path;
                isDirty = true;
                RefreshPreview();
            }));
        }

        private void ShowHiddenTagsMenu()
        {
            var options = new List<FloatMenuOption>();
            #pragma warning disable CS0618
            // 常用标签列表
            var commonTags = new[] { "Head", "Hair", "Body", "Beard", "Tattoo", "FaceTattoo", "BodyTattoo",
                                     "Headgear", "Eyes", "Brow", "Jaw", "Ear", "Nose", "Mouth" };
            
            foreach (var tag in commonTags)
            {
                if (workingSkin.hiddenTags == null || !workingSkin.hiddenTags.Contains(tag))
                {
                    options.Add(new FloatMenuOption(tag, () =>
                    {
                        if (workingSkin.hiddenTags == null)
                            workingSkin.hiddenTags = new List<string>();
                        workingSkin.hiddenTags.Add(tag);
                        isDirty = true;
                        RefreshPreview();
                        RefreshRenderTree();
                        ShowStatus("CS_Studio_Msg_AddedHiddenTag".Translate(tag));
                    }));
                }
            }

            // 从当前 Pawn 渲染树获取可用标签
            if (mannequin?.CurrentPawn != null)
            {
                options.Add(new FloatMenuOption("CS_Studio_Ctx_FromRenderTree".Translate(), () => ShowRenderTreeTagSelector()));
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                ShowStatus("CS_Studio_Msg_AllTagsAdded".Translate());
            }
            #pragma warning restore CS0618
        }

        private void ShowRenderTreeTagSelector()
        {
            var pawn = mannequin?.CurrentPawn;
            if (pawn?.Drawer?.renderer?.renderTree == null) return;

            var rootSnapshot = RenderTreeParser.Capture(pawn);
            if (rootSnapshot == null) return;

            // 展平节点树收集所有节点
            var allSnapshots = new List<RenderNodeSnapshot>();
            CollectSnapshots(rootSnapshot, allSnapshots);

            var options = new List<FloatMenuOption>();
            #pragma warning disable CS0618
            foreach (var snapshot in allSnapshots)
            {
                if (!string.IsNullOrEmpty(snapshot.tagDefName) && snapshot.tagDefName != "Untagged" &&
                    (workingSkin.hiddenTags == null || !workingSkin.hiddenTags.Contains(snapshot.tagDefName)))
                {
                    var tagName = snapshot.tagDefName;
                    options.Add(new FloatMenuOption($"{tagName} ({snapshot.workerClass})", () =>
                    {
                        if (workingSkin.hiddenTags == null)
                            workingSkin.hiddenTags = new List<string>();
                        workingSkin.hiddenTags.Add(tagName);
                        isDirty = true;
                        RefreshPreview();
                        RefreshRenderTree();
                        ShowStatus("CS_Studio_Msg_TagAdded".Translate(tagName));
                    }));
                }
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                ShowStatus("CS_Studio_Msg_NoMoreTags".Translate());
            }
            #pragma warning restore CS0618
        }


        /// <summary>
        /// 绘制带方向高亮的章节标题
        /// </summary>
        private void DrawDirectionalSectionTitle(ref float y, float width, string title, bool isActive)
        {
            // 当该方向激活时，使用高亮颜色
            if (isActive)
            {
                // 绘制背景高亮
                Rect bgRect = new Rect(0, y, width, 20);
                Widgets.DrawBoxSolid(bgRect, new Color(0.2f, 0.4f, 0.6f, 0.3f));
                
                // 添加激活指示器
                GUI.color = new Color(0.4f, 0.8f, 1f);
                Widgets.Label(new Rect(0, y, 16, 18), "▶");
                GUI.color = Color.white;
                
                // 绘制标题（带偏移）
                Text.Font = GameFont.Small;
                GUI.color = new Color(0.6f, 0.9f, 1f);
                Widgets.Label(new Rect(16, y, width - 16, 18), title);
                GUI.color = Color.white;
            }
            else
            {
                // 非激活状态使用普通样式
                UIHelper.DrawSectionTitle(ref y, width, title);
                return; // UIHelper.DrawSectionTitle 已经更新了 y
            }
            y += 22;
        }

        /// <summary>
        /// 递归收集所有节点快照
        /// </summary>
        private void CollectSnapshots(RenderNodeSnapshot node, List<RenderNodeSnapshot> result)
        {
            if (node == null) return;
            result.Add(node);
            foreach (var child in node.children)
            {
                CollectSnapshots(child, result);
            }
        }

        /// <summary>
        /// 绘制可折叠的属性区域
        /// </summary>
        private bool DrawCollapsibleSection(ref float y, float width, string title, string sectionKey, bool highlight = false)
        {
            bool isCollapsed = collapsedSections.Contains(sectionKey);
            
            y += 5f;
            Rect rect = new Rect(0, y, width, 24f);
            
            // 背景
            if (highlight)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.4f, 0.6f, 0.3f));
            }
            else
            {
                Widgets.DrawLightHighlight(rect);
            }
            
            // 标题
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = highlight ? new Color(0.6f, 0.9f, 1f) : UIHelper.HeaderColor;
            
            string icon = isCollapsed ? "▶" : "▼";
            string displayTitle = $"{icon} {title}";
            
            if (Widgets.ButtonInvisible(rect))
            {
                if (isCollapsed)
                    collapsedSections.Remove(sectionKey);
                else
                    collapsedSections.Add(sectionKey);
            }
            
            Widgets.Label(rect, displayTitle);
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            y += 28f;
            
            return !isCollapsed;
        }
    }
}