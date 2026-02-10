using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 皮肤编辑器主窗口
    /// 三栏布局：图层树 | 预览 | 属性面板
    /// </summary>
    public class Dialog_SkinEditor : Window
    {
        // ─────────────────────────────────────────────
        // 常量
        // ─────────────────────────────────────────────
        private const float LeftPanelWidth = 200f;
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
        private Vector2 layerScrollPos;
        private Vector2 propsScrollPos;
        private Vector2 faceScrollPos;
        private bool isDirty = false;

        private enum EditorTab { Layers, Face }
        private EditorTab currentTab = EditorTab.Layers;
        private string statusMessage = "";
        private float statusMessageTime = 0f;

        // 预览管理
        private MannequinManager? mannequin;
        private Rot4 previewRotation = Rot4.South;
        private float previewZoom = 1f;

        // ─────────────────────────────────────────────
        // 树状视图状态
        // ─────────────────────────────────────────────
        private HashSet<string> expandedPaths = new HashSet<string>();
        private HashSet<string> collapsedSections = new HashSet<string>(); // 记录折叠的属性区域
        private RenderNodeSnapshot? cachedRootSnapshot;
        private string selectedNodePath = "";
        private float treeViewHeight = 0f;
        private const float TreeNodeHeight = 22f;
        private const float TreeIndentWidth = 16f;

        // ─────────────────────────────────────────────
        // 窗口设置
        // ─────────────────────────────────────────────

        public override Vector2 InitialSize => new Vector2(1024f, 700f);

        public Dialog_SkinEditor()
        {
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = true;
            this.resizeable = true;
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

                // 更新状态消息计时
                if (statusMessageTime > 0)
                {
                    statusMessageTime -= Time.deltaTime;
                }

                // 绘制顶部菜单栏
                Rect menuRect = new Rect(0, 0, inRect.width, TopMenuHeight);
                DrawTopMenu(menuRect);

                // 计算主内容区域
                float contentY = TopMenuHeight + Margin;
                float contentHeight = inRect.height - TopMenuHeight - BottomStatusHeight - Margin * 2;

                // 绘制左侧面板（图层树 或 表情配置）
                Rect leftRect = new Rect(0, contentY, LeftPanelWidth, contentHeight);
                
                // 绘制选项卡切换按钮
                Rect tabRect = new Rect(leftRect.x, leftRect.y, leftRect.width, 30f);
                DrawTabButtons(tabRect);

                // 调整左侧面板内容区域
                Rect leftContentRect = new Rect(leftRect.x, leftRect.y + 35f, leftRect.width, leftRect.height - 35f);

                if (currentTab == EditorTab.Layers)
                {
                    DrawLayerPanel(leftContentRect);
                }
                else
                {
                    DrawFacePanel(leftContentRect);
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
            float tabWidth = rect.width / 2f;
            
            // 图层选项卡
            if (UIHelper.DrawTabButton(new Rect(rect.x, rect.y, tabWidth, rect.height), "CS_Studio_Tab_Layers".Translate(), currentTab == EditorTab.Layers))
            {
                currentTab = EditorTab.Layers;
            }

            // 表情选项卡
            if (UIHelper.DrawTabButton(new Rect(rect.x + tabWidth, rect.y, tabWidth, rect.height), "CS_Studio_Tab_Face".Translate(), currentTab == EditorTab.Face))
            {
                currentTab = EditorTab.Face;
            }
        }

        // ─────────────────────────────────────────────
        // 顶部菜单栏
        // ─────────────────────────────────────────────

        private void DrawTopMenu(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            float x = Margin;
            
            // 辅助方法：绘制自适应宽度的按钮
            bool DrawButton(string label, Action action)
            {
                float width = Text.CalcSize(label).x + 20f; // 文本宽度 + padding
                width = Mathf.Max(width, 60f); // 最小宽度
                
                if (Widgets.ButtonText(new Rect(x, 2, width, ButtonHeight), label))
                {
                    action();
                    return true;
                }
                x += width + Margin;
                return false;
            }

            // 新建
            DrawButton("CS_Studio_File_New".Translate(), OnNewSkin);

            // 保存
            DrawButton("CS_Studio_File_Save".Translate(), OnSaveSkin);

            // 导出
            DrawButton("CS_Studio_File_Export".Translate(), OnExportMod);

            // 从地图导入
            DrawButton("CS_Studio_File_Import".Translate(), OnImportFromMap);

            // 分隔符
            x += 10;

            // 皮肤设置按钮
            DrawButton("CS_Studio_Skin_Settings".Translate(), OnOpenSkinSettings);

            // 技能编辑器按钮
            DrawButton("CS_Studio_Menu_Abilities".Translate(), () => Find.WindowStack.Add(new Dialog_AbilityEditor(workingAbilities)));

            // 帮助按钮
            if (Widgets.ButtonText(new Rect(x, 2, 24, ButtonHeight), "?"))
            {
                Find.WindowStack.Add(new Dialog_Glossary());
            }

            // 右侧显示皮肤名称
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.width - 200, 2, 190, ButtonHeight), workingSkin.label ?? workingSkin.defName);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ─────────────────────────────────────────────
        // 图层面板 - 树状视图
        // ─────────────────────────────────────────────

        private void DrawLayerPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            // 标题
            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, ButtonHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "CS_Studio_Panel_Layers".Translate());
            Text.Font = GameFont.Small;

            // 按钮栏
            float btnY = rect.y + Margin + ButtonHeight + Margin;
            float btnWidth = (rect.width - Margin * 5) / 4;

            // 刷新按钮
            if (Widgets.ButtonText(new Rect(rect.x + Margin, btnY, btnWidth, ButtonHeight), "↻"))
            {
                RefreshRenderTree();
            }
            TooltipHandler.TipRegion(new Rect(rect.x + Margin, btnY, btnWidth, ButtonHeight), "CS_Studio_Panel_RefreshTree".Translate());

            // 展开全部
            if (Widgets.ButtonText(new Rect(rect.x + Margin * 2 + btnWidth, btnY, btnWidth, ButtonHeight), "▼"))
            {
                ExpandAllNodes();
            }
            TooltipHandler.TipRegion(new Rect(rect.x + Margin * 2 + btnWidth, btnY, btnWidth, ButtonHeight), "CS_Studio_Panel_ExpandAll".Translate());

            // 折叠全部
            if (Widgets.ButtonText(new Rect(rect.x + Margin * 3 + btnWidth * 2, btnY, btnWidth, ButtonHeight), "▶"))
            {
                CollapseAllNodes();
            }
            TooltipHandler.TipRegion(new Rect(rect.x + Margin * 3 + btnWidth * 2, btnY, btnWidth, ButtonHeight), "CS_Studio_Panel_CollapseAll".Translate());

            // 添加图层
            if (Widgets.ButtonText(new Rect(rect.x + Margin * 4 + btnWidth * 3, btnY, btnWidth, ButtonHeight), "+"))
            {
                OnAddLayer();
            }
            TooltipHandler.TipRegion(new Rect(rect.x + Margin * 4 + btnWidth * 3, btnY, btnWidth, ButtonHeight), "CS_Studio_Layer_AddCustom".Translate());

            // 树状列表区域
            float listY = btnY + ButtonHeight + Margin;
            float listHeight = rect.height - listY + rect.y - Margin;
            Rect listRect = new Rect(rect.x + Margin, listY, rect.width - Margin * 2, listHeight);

            // 刷新快照（如果需要）
            if (cachedRootSnapshot == null)
            {
                RefreshRenderTree();
            }

            // 计算树视图高度
            treeViewHeight = 0f;
            if (cachedRootSnapshot != null)
            {
                treeViewHeight = CalculateTreeHeight(cachedRootSnapshot);
            }
            // 添加自定义图层的高度
            treeViewHeight += workingSkin.layers.Count * TreeNodeHeight + 30;

            Rect viewRect = new Rect(0, 0, listRect.width - 16, Mathf.Max(treeViewHeight, listRect.height));

            Widgets.BeginScrollView(listRect, ref layerScrollPos, viewRect);

            float currentY = 0f;

            // 使用 Tiny 字体以避免列表过长时显示不全
            Text.Font = GameFont.Tiny;

            // 绘制渲染树
            if (cachedRootSnapshot != null)
            {
                int rowIndex = 0;
                currentY = DrawNodeRow(cachedRootSnapshot, 0, currentY, viewRect.width, ref rowIndex);
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0, currentY, viewRect.width, TreeNodeHeight), "  " + "CS_Studio_Layer_NoTree".Translate());
                GUI.color = Color.white;
                currentY += TreeNodeHeight;
            }

            // 分隔线
            currentY += 8;
            Widgets.DrawLineHorizontal(0, currentY, viewRect.width);
            currentY += 8;

            // 自定义图层标题
            GUI.color = new Color(0.7f, 0.9f, 1f);
            Widgets.Label(new Rect(0, currentY, viewRect.width, 18), "CS_Studio_Layer_CustomSection".Translate());
            GUI.color = Color.white;
            currentY += 20;

            // 绘制自定义图层列表
            for (int i = 0; i < workingSkin.layers.Count; i++)
            {
                currentY = DrawCustomLayerRow(workingSkin.layers[i], i, currentY, viewRect.width);
            }

            Text.Font = GameFont.Small; // 恢复字体

            Widgets.EndScrollView();
        }

        /// <summary>
        /// 绘制表情配置面板
        /// </summary>
        private void DrawFacePanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            // 标题
            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, ButtonHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "CS_Studio_Face_Title".Translate());
            Text.Font = GameFont.Small;

            float contentY = rect.y + Margin + ButtonHeight + Margin;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            Rect viewRect = new Rect(0, 0, contentRect.width - 16, 400);

            Widgets.BeginScrollView(contentRect, ref faceScrollPos, viewRect);

            float y = 0;
            float width = viewRect.width;

            // 表情配置面板占位
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0, y, width, 60), "CS_Studio_Face_Dev".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            y += 80;

            // FaceConfig 信息
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_Config".Translate());
            
            if (workingSkin.faceConfig != null)
            {
                var fc = workingSkin.faceConfig;

                // 启用状态
                bool enabled = fc.enabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Face_Enable".Translate(), ref enabled);
                if (enabled != fc.enabled) { fc.enabled = enabled; isDirty = true; }

                y += 10;

                // 各组件的表情配置
                foreach (var component in fc.components)
                {
                    UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_Type".Translate(component.type));
                    
                    // 显示该组件的表情映射
                    foreach (ExpressionType expr in Enum.GetValues(typeof(ExpressionType)))
                    {
                        var existing = component.expressions.Find(e => e.expression == expr);
                        string currentPath = existing?.texPath ?? "";
                        
                        UIHelper.DrawPropertyFieldWithButton(ref y, width, expr.ToString(),
                            string.IsNullOrEmpty(currentPath) ? "CS_Studio_None".Translate() : currentPath,
                            () => {
                                Find.WindowStack.Add(new Dialog_FileBrowser(currentPath, path => {
                                    if (existing != null)
                                    {
                                        existing.texPath = path;
                                    }
                                    else
                                    {
                                        component.expressions.Add(new ExpressionTexPath { expression = expr, texPath = path });
                                    }
                                    isDirty = true;
                                }));
                            });
                    }
                    y += 8;
                }
            }
            else
            {
                // 创建 FaceConfig 按钮
                if (Widgets.ButtonText(new Rect(0, y, width, 28), "CS_Studio_Face_Create".Translate()))
                {
                    workingSkin.faceConfig = new PawnFaceConfig();
                    isDirty = true;
                }
                y += 32;
            }

            Widgets.EndScrollView();
        }

        /// <summary>
        /// 刷新渲染树快照
        /// </summary>
        private void RefreshRenderTree()
        {
            var pawn = mannequin?.CurrentPawn;
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
                selectedNodePath = node.uniqueNodePath;
                selectedLayerIndex = -1; // 清除自定义图层选择
            }

            // 右键菜单
            if (Mouse.IsOver(rowRect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                ShowNodeContextMenu(node);
                Event.current.Use();
            }

            // Tooltip
            TooltipHandler.TipRegion(rowRect, $"路径: {node.uniqueNodePath}\n类型: {node.workerClass}\n贴图: {node.texPath ?? "(无)"}");

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
            if (selectedLayerIndex == index)
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
                layer.visible = !layer.visible;
                isDirty = true;
                RefreshPreview();
            }
            GUI.color = Color.white;

            // 图层名称
            Rect nameRect = new Rect(46, y, width - 70, TreeNodeHeight);
            string displayName = string.IsNullOrEmpty(layer.layerName) ? $"Layer {index + 1}" : layer.layerName;
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, displayName);
            Text.Anchor = TextAnchor.UpperLeft;

            // 删除按钮
            if (Widgets.ButtonText(new Rect(width - 22, y + 1, 20, 20), "×"))
            {
                workingSkin.layers.RemoveAt(index);
                if (selectedLayerIndex >= workingSkin.layers.Count)
                    selectedLayerIndex = workingSkin.layers.Count - 1;
                isDirty = true;
                RefreshPreview();
            }

            // 点击选中
            if (Widgets.ButtonInvisible(rowRect))
            {
                selectedLayerIndex = index;
                selectedNodePath = ""; // 清除节点选择
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
            string worker = node.workerClass;
            
            if (worker.Contains("Head")) return new Color(1f, 0.8f, 0.6f);
            if (worker.Contains("Body")) return new Color(0.8f, 1f, 0.8f);
            if (worker.Contains("Hair")) return new Color(1f, 0.9f, 0.5f);
            if (worker.Contains("Apparel")) return new Color(0.7f, 0.8f, 1f);
            if (worker.Contains("Attachment")) return new Color(1f, 0.7f, 1f);
            
            return Color.white;
        }

        /// <summary>
        /// 切换节点可见性
        /// </summary>
        private void ToggleNodeVisibility(RenderNodeSnapshot node)
        {
            if (workingSkin.hiddenPaths.Contains(node.uniqueNodePath))
            {
                workingSkin.hiddenPaths.Remove(node.uniqueNodePath);
                ShowStatus("CS_Studio_Msg_Shown".Translate(node.uniqueNodePath));
            }
            else
            {
                workingSkin.hiddenPaths.Add(node.uniqueNodePath);
                ShowStatus("CS_Studio_Msg_Hidden".Translate(node.uniqueNodePath));
            }
            isDirty = true;
            RefreshPreview();
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
                    layerName = $"Layer on {GetNodeDisplayName(node)}",
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
                var copy = layer.Clone();
                copy.layerName += " (Copy)";
                workingSkin.layers.Insert(index + 1, copy);
                selectedLayerIndex = index + 1;
                isDirty = true;
                RefreshPreview();
            }));

            // 上移
            if (index > 0)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveUp".Translate(), () =>
                {
                    workingSkin.layers.RemoveAt(index);
                    workingSkin.layers.Insert(index - 1, layer);
                    selectedLayerIndex = index - 1;
                    isDirty = true;
                    RefreshPreview();
                }));
            }

            // 下移
            if (index < workingSkin.layers.Count - 1)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveDown".Translate(), () =>
                {
                    workingSkin.layers.RemoveAt(index);
                    workingSkin.layers.Insert(index + 1, layer);
                    selectedLayerIndex = index + 1;
                    isDirty = true;
                    RefreshPreview();
                }));
            }

            // 删除
            options.Add(new FloatMenuOption("CS_Studio_Ctx_DeleteLayer".Translate(), () =>
            {
                workingSkin.layers.RemoveAt(index);
                if (selectedLayerIndex >= workingSkin.layers.Count)
                    selectedLayerIndex = workingSkin.layers.Count - 1;
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
                if (Widgets.ButtonText(new Rect(btnX + 40, btnY, 80, 24), mannequin.currentExpression))
                {
                    var options = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("Neutral", () => { mannequin.currentExpression = "Neutral"; RefreshPreview(); }),
                        new FloatMenuOption("Happy", () => { mannequin.currentExpression = "Happy"; RefreshPreview(); }),
                        new FloatMenuOption("Sad", () => { mannequin.currentExpression = "Sad"; RefreshPreview(); }),
                        new FloatMenuOption("Angry", () => { mannequin.currentExpression = "Angry"; RefreshPreview(); })
                    };
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                Widgets.Label(new Rect(btnX + 130, btnY + 4, 30, 24), "CS_Studio_Preview_Var".Translate());
                if (Widgets.ButtonText(new Rect(btnX + 160, btnY, 30, 24), mannequin.currentVariant.ToString()))
                {
                    mannequin.currentVariant = (mannequin.currentVariant + 1) % 6; // 0-5
                    RefreshPreview();
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

            // 处理交互输入
            HandlePreviewInput(previewRect);
            
            // 绘制选中图层的高亮指示
            DrawSelectedLayerHighlight(previewRect);
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

                // 鼠标拖拽调整偏移
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 0 && !Event.current.shift)
                {
                    if (selectedLayerIndex >= 0 && selectedLayerIndex < workingSkin.layers.Count)
                    {
                        var layer = workingSkin.layers[selectedLayerIndex];
                        Vector2 delta = Event.current.delta;
                        
                        // 灵敏度系数 (根据缩放调整)
                        float sensitivity = 0.005f / previewZoom;
                        
                        layer.offset.x += delta.x * sensitivity;
                        layer.offset.z -= delta.y * sensitivity; // 屏幕Y向下，世界Z向上
                        
                        isDirty = true;
                        RefreshPreview();
                        Event.current.Use();
                    }
                }
            }

            // 方向键微调 (无需鼠标悬停，只要有选中的图层)
            if (Event.current.type == EventType.KeyDown && selectedLayerIndex >= 0 && selectedLayerIndex < workingSkin.layers.Count)
            {
                var layer = workingSkin.layers[selectedLayerIndex];
                float step = 0.005f;
                if (Event.current.shift) step *= 10f; // Shift 加速

                bool handled = true;
                switch (Event.current.keyCode)
                {
                    case KeyCode.LeftArrow: layer.offset.x -= step; break;
                    case KeyCode.RightArrow: layer.offset.x += step; break;
                    case KeyCode.UpArrow: layer.offset.z += step; break;
                    case KeyCode.DownArrow: layer.offset.z -= step; break;
                    default: handled = false; break;
                }

                if (handled)
                {
                    isDirty = true;
                    RefreshPreview();
                    Event.current.Use();
                }
            }
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
                ShowStatus($"已选中: {workingSkin.layers[bestIndex].layerName}");
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

            // 如果选中了渲染树节点，显示节点信息
            if (!string.IsNullOrEmpty(selectedNodePath) && cachedRootSnapshot != null)
            {
                DrawNodeProperties(rect);
                return;
            }

            // 检查是否有选中的图层
            if (selectedLayerIndex < 0 || selectedLayerIndex >= workingSkin.layers.Count)
            {
                Rect hintRect = new Rect(rect.x + Margin, rect.y + 60, rect.width - Margin * 2, 40);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(hintRect, "CS_Studio_Msg_SelectLayer".Translate());
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
            if (DrawCollapsibleSection(ref y, width, "基本设置", "Base"))
            {
                string oldName = layer.layerName;
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Prop_LayerName".Translate(), ref layer.layerName);
            if (oldName != layer.layerName) isDirty = true;

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
                val => { layer.anchorTag = val; isDirty = true; RefreshPreview(); });
            } // End Base Section

            // 变换设置 (South - 默认方向)
            bool isSouthActive = previewRotation == Rot4.South;
            if (DrawCollapsibleSection(ref y, width, "基础变换 (南向)", "Transform", isSouthActive))
            {
                float ox = layer.offset.x;
                UIHelper.DrawPropertySlider(ref y, width, "X 偏移", ref ox, -1f, 1f);
                if (ox != layer.offset.x) { layer.offset.x = ox; isDirty = true; RefreshPreview(); }

                float oy = layer.offset.y;
                UIHelper.DrawPropertySlider(ref y, width, "Y 偏移(高度)", ref oy, -1f, 1f);
                if (oy != layer.offset.y) { layer.offset.y = oy; isDirty = true; RefreshPreview(); }

                float oz = layer.offset.z;
                UIHelper.DrawPropertySlider(ref y, width, "Z 偏移", ref oz, -1f, 1f);
                if (oz != layer.offset.z) { layer.offset.z = oz; isDirty = true; RefreshPreview(); }
            }

            // 侧视图偏移 (East/West)
            bool isEastActive = previewRotation == Rot4.East || previewRotation == Rot4.West;
            if (DrawCollapsibleSection(ref y, width, "侧面偏移 (东西向)", "EastOffset", isEastActive))
            {
                float ex = layer.offsetEast.x;
                UIHelper.DrawPropertySlider(ref y, width, "X 偏移", ref ex, -1f, 1f);
                if (ex != layer.offsetEast.x) { layer.offsetEast.x = ex; isDirty = true; RefreshPreview(); }

                float ey = layer.offsetEast.y;
                UIHelper.DrawPropertySlider(ref y, width, "Y 偏移", ref ey, -1f, 1f);
                if (ey != layer.offsetEast.y) { layer.offsetEast.y = ey; isDirty = true; RefreshPreview(); }

                float ez = layer.offsetEast.z;
                UIHelper.DrawPropertySlider(ref y, width, "Z 偏移", ref ez, -1f, 1f);
                if (ez != layer.offsetEast.z) { layer.offsetEast.z = ez; isDirty = true; RefreshPreview(); }
            }

            // 北向偏移 (North)
            bool isNorthActive = previewRotation == Rot4.North;
            if (DrawCollapsibleSection(ref y, width, "背面偏移 (北向)", "NorthOffset", isNorthActive))
            {
                float nx = layer.offsetNorth.x;
                UIHelper.DrawPropertySlider(ref y, width, "X 偏移", ref nx, -1f, 1f);
                if (nx != layer.offsetNorth.x) { layer.offsetNorth.x = nx; isDirty = true; RefreshPreview(); }

                float ny = layer.offsetNorth.y;
                UIHelper.DrawPropertySlider(ref y, width, "Y 偏移", ref ny, -1f, 1f);
                if (ny != layer.offsetNorth.y) { layer.offsetNorth.y = ny; isDirty = true; RefreshPreview(); }

                float nz = layer.offsetNorth.z;
                UIHelper.DrawPropertySlider(ref y, width, "Z 偏移", ref nz, -1f, 1f);
                if (nz != layer.offsetNorth.z) { layer.offsetNorth.z = nz; isDirty = true; RefreshPreview(); }
            }

            // 其他设置
            if (DrawCollapsibleSection(ref y, width, "其他设置", "Misc"))
            {
                float s = layer.scale.x;
                UIHelper.DrawPropertySlider(ref y, width, "缩放", ref s, 0.1f, 3f);
                if (s != layer.scale.x) { layer.scale = new Vector2(s, s); isDirty = true; RefreshPreview(); }
    
                // 扩大 DrawOrder 范围以覆盖 Body(0), Head(50) 等基准层级
                    UIHelper.DrawPropertySlider(ref y, width, "层级(DrawOrder)", ref layer.drawOrder, -200f, 200f, "F0");
                    
                    // Worker 选择
                    string[] workers = { "Default", "FaceComponent" };
                    string currentWorker = layer.workerClass == typeof(CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent) ? "FaceComponent" : "Default";
                    
                    UIHelper.DrawPropertyDropdown(ref y, width, "Worker", currentWorker, workers,
                        w => w,
                        val => {
                            if (val == "FaceComponent")
                                layer.workerClass = typeof(CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent);
                            else
                                layer.workerClass = null;
                            isDirty = true;
                            RefreshPreview();
                        });
    
                    // 如果是 FaceComponent，显示额外配置
                    if (currentWorker == "FaceComponent")
                    {
                        // Face Component Type
                        UIHelper.DrawPropertyDropdown(ref y, width, "面部部件类型", layer.faceComponent,
                            (FaceComponentType[])Enum.GetValues(typeof(FaceComponentType)),
                            type => type.ToString(),
                            val => { layer.faceComponent = val; isDirty = true; RefreshPreview(); });
                    }
    
                    UIHelper.DrawPropertyDropdown(ref y, width, "颜色类型", layer.colorType,
                        (LayerColorType[])Enum.GetValues(typeof(LayerColorType)),
                        type => $"CS_Studio_ColorType_{type}".Translate(),
                        val => { layer.colorType = val; isDirty = true; RefreshPreview(); });
    
                    // 仅当颜色类型为 Custom 时显示颜色选择器
                    if (layer.colorType == LayerColorType.Custom)
                    {
                        UIHelper.DrawPropertyColor(ref y, width, "颜色", layer.customColor,
                            col => { layer.customColor = col; isDirty = true; RefreshPreview(); });
                        
                        // 绘制第二颜色（Mask）选择器
                        UIHelper.DrawPropertyColor(ref y, width, "第二颜色(Mask)", layer.customColorTwo,
                            col => { layer.customColorTwo = col; isDirty = true; RefreshPreview(); });
                    }
    
                    bool flip = layer.flipHorizontal;
                    UIHelper.DrawPropertyCheckbox(ref y, width, "水平翻转", ref flip);
                    if (flip != layer.flipHorizontal) { layer.flipHorizontal = flip; isDirty = true; RefreshPreview(); }
                } // End Misc Section
    
                // 动画设置
                if (DrawCollapsibleSection(ref y, width, "动画设置", "Animation"))
                {
                    // 动画类型选择
                    UIHelper.DrawPropertyDropdown(ref y, width, "动画类型", layer.animationType,
                        (LayerAnimationType[])Enum.GetValues(typeof(LayerAnimationType)),
                        type => $"CS_Studio_Anim_{type}".Translate(),
                        val => { layer.animationType = val; isDirty = true; RefreshPreview(); });
    
                    // 仅当选择了动画类型时显示其他参数
                    if (layer.animationType != LayerAnimationType.None)
                    {
                        // 频率
                        float freq = layer.animFrequency;
                        UIHelper.DrawPropertySlider(ref y, width, "频率", ref freq, 0.1f, 5f);
                        if (freq != layer.animFrequency) { layer.animFrequency = freq; isDirty = true; RefreshPreview(); }
    
                        // 幅度
                        float amp = layer.animAmplitude;
                        UIHelper.DrawPropertySlider(ref y, width, "幅度", ref amp, 1f, 45f);
                        if (amp != layer.animAmplitude) { layer.animAmplitude = amp; isDirty = true; RefreshPreview(); }
    
                        // 速度（仅 Twitch 类型使用）
                        if (layer.animationType == LayerAnimationType.Twitch)
                        {
                            float speed = layer.animSpeed;
                            UIHelper.DrawPropertySlider(ref y, width, "速度", ref speed, 0.1f, 3f);
                            if (speed != layer.animSpeed) { layer.animSpeed = speed; isDirty = true; RefreshPreview(); }
                        }
    
                        // 相位偏移（用于多图层错开动画）
                        float phase = layer.animPhaseOffset;
                        UIHelper.DrawPropertySlider(ref y, width, "相位偏移", ref phase, 0f, 1f);
                        if (phase != layer.animPhaseOffset) { layer.animPhaseOffset = phase; isDirty = true; RefreshPreview(); }
    
                        // 位移动画开关
                        bool affectsOffset = layer.animAffectsOffset;
                        UIHelper.DrawPropertyCheckbox(ref y, width, "动画影响位移", ref affectsOffset);
                        if (affectsOffset != layer.animAffectsOffset) { layer.animAffectsOffset = affectsOffset; isDirty = true; RefreshPreview(); }
    
                        // 位移幅度（仅当启用位移时显示）
                        if (layer.animAffectsOffset)
                        {
                            float offsetAmp = layer.animOffsetAmplitude;
                            UIHelper.DrawPropertySlider(ref y, width, "位移幅度", ref offsetAmp, 0.001f, 0.1f, "F3");
                            if (offsetAmp != layer.animOffsetAmplitude) { layer.animOffsetAmplitude = offsetAmp; isDirty = true; RefreshPreview(); }
                        }
                    }
                }
    
                if (DrawCollapsibleSection(ref y, width, "隐藏原版部件", "HideVanilla"))
                {
                    // 显示当前隐藏的标签
                    #pragma warning disable CS0618
                    if (workingSkin.hiddenTags != null && workingSkin.hiddenTags.Count > 0)
                    {
                        foreach (var tag in workingSkin.hiddenTags.ToList())
                        {
                            Rect tagRect = new Rect(0, y, propsRect.width - 50, 22);
                            Widgets.Label(tagRect, $"  • {tag}");
                            if (Widgets.ButtonText(new Rect(propsRect.width - 45, y, 40, 20), "×"))
                            {
                                workingSkin.hiddenTags.Remove(tag);
                                isDirty = true;
                                RefreshPreview();
                            }
                            y += 24;
                        }
                    }
                    #pragma warning restore CS0618
                    else
                    {
                        GUI.color = Color.gray;
                        Widgets.Label(new Rect(0, y, propsRect.width - 20, 22), "  " + "CS_Studio_Msg_NoHidden".Translate());
                        GUI.color = Color.white;
                        y += 24;
                    }
    
                    // 添加隐藏标签按钮
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
            Rect viewRect = new Rect(0, 0, propsRect.width - 16, 550);

            Widgets.BeginScrollView(propsRect, ref propsScrollPos, viewRect);

            float y = 0;
            float labelWidth = 70f;
            float fieldWidth = propsRect.width - labelWidth - 20;

            // 节点路径
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0, y, propsRect.width - 20, 16), "CS_Studio_Prop_NodeInfo".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 18;

            // 路径（可复制）
            Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Info_Path".Translate());
            Rect pathRect = new Rect(labelWidth, y, fieldWidth - 30, 24);
            Widgets.Label(pathRect, node.uniqueNodePath);
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
                Rect texPathRect = new Rect(labelWidth, y, fieldWidth - 30, 24);
                Widgets.Label(texPathRect, node.texPath);
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

            // 分隔线
            Widgets.DrawLineHorizontal(0, y, propsRect.width - 20);
            y += 10;

            // 操作按钮
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0, y, propsRect.width - 20, 16), "CS_Studio_Prop_Actions".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 20;

            // 隐藏/显示按钮
            #pragma warning disable CS0618
            bool isHidden = workingSkin.hiddenPaths.Contains(node.uniqueNodePath) ||
                           workingSkin.hiddenTags.Contains(node.tagDefName);
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
                    layerName = $"Layer on {GetNodeDisplayName(node)}",
                    anchorPath = node.uniqueNodePath,
                    anchorTag = node.tagDefName ?? "Body"
                };
                workingSkin.layers.Add(newLayer);
                selectedLayerIndex = workingSkin.layers.Count - 1;
                selectedNodePath = "";
                isDirty = true;
                RefreshPreview();
                ShowStatus("CS_Studio_Msg_Appended".Translate(node.uniqueNodePath, "1")); // 参数类型修复
            }
            y += 32;

            // ===== 运行时数据探针 (只读) =====
            if (node.runtimeDataValid)
            {
                // 分隔线
                Widgets.DrawLineHorizontal(0, y, propsRect.width - 20);
                y += 10;

                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.5f, 0.8f, 1f);
                Widgets.Label(new Rect(0, y, propsRect.width - 20, 16), "运行时数据 (只读)");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 20;

                // 运行时偏移
                bool offsetNonDefault = node.runtimeOffset != Vector3.zero;
                GUI.color = offsetNonDefault ? Color.yellow : Color.white;
                Widgets.Label(new Rect(0, y, labelWidth, 24), "Offset:");
                Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24),
                    $"({node.runtimeOffset.x:F3}, {node.runtimeOffset.y:F3}, {node.runtimeOffset.z:F3})");
                GUI.color = Color.white;
                y += 24;

                // 运行时缩放
                bool scaleNonDefault = node.runtimeScale != Vector3.one;
                GUI.color = scaleNonDefault ? Color.yellow : Color.white;
                Widgets.Label(new Rect(0, y, labelWidth, 24), "Scale:");
                Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24),
                    $"({node.runtimeScale.x:F3}, {node.runtimeScale.y:F3}, {node.runtimeScale.z:F3})");
                GUI.color = Color.white;
                y += 24;

                // 运行时颜色
                bool colorNonDefault = node.runtimeColor != Color.white;
                GUI.color = colorNonDefault ? Color.yellow : Color.white;
                Widgets.Label(new Rect(0, y, labelWidth, 24), "Color:");
                // 绘制颜色预览方块
                Rect colorPreviewRect = new Rect(labelWidth, y + 2, 20, 20);
                Widgets.DrawBoxSolid(colorPreviewRect, node.runtimeColor);
                Widgets.DrawBox(colorPreviewRect, 1);
                Widgets.Label(new Rect(labelWidth + 25, y, fieldWidth - 25, 24),
                    $"R:{node.runtimeColor.r:F2} G:{node.runtimeColor.g:F2} B:{node.runtimeColor.b:F2} A:{node.runtimeColor.a:F2}");
                GUI.color = Color.white;
                y += 24;

                // 运行时旋转
                bool rotNonDefault = Mathf.Abs(node.runtimeRotation) > 0.01f;
                GUI.color = rotNonDefault ? Color.yellow : Color.white;
                Widgets.Label(new Rect(0, y, labelWidth, 24), "Rotation:");
                Widgets.Label(new Rect(labelWidth, y, fieldWidth, 24), $"{node.runtimeRotation:F1}°");
                GUI.color = Color.white;
                y += 24;

                // 提示
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(0, y, propsRect.width - 20, 16), "(黄色 = 非默认值)");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 20;
            }
            else
            {
                // 无运行时数据
                y += 10;
                GUI.color = Color.gray;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0, y, propsRect.width - 20, 16), "(无运行时数据)");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 20;
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
            Widgets.DrawMenuSection(rect);

            string status = "";
            if (statusMessageTime > 0 && !string.IsNullOrEmpty(statusMessage))
            {
                status = statusMessage;
            }
            else if (isDirty)
            {
                status = "* 有未保存的更改";
            }
            else
            {
                status = $"图层: {workingSkin.layers.Count} | 缩放: {previewZoom:F1}x | 方向: {previewRotation}";
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + Margin, rect.y, rect.width - Margin * 2, rect.height), status);
            Text.Anchor = TextAnchor.UpperLeft;
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
                label = "New Skin",
                description = "",
                author = "",
                version = "1.0.0"
            };
            selectedLayerIndex = -1;
            isDirty = false;
            
            // 清除隐藏节点状态
            Rendering.Patch_PawnRenderTree.ClearHiddenNodes();
            
            RefreshPreview();
        }

        private void OnSaveSkin()
        {
            // 验证必填字段
            if (string.IsNullOrEmpty(workingSkin.defName))
            {
                ShowStatus("CS_Studio_Err_DefNameEmpty".Translate());
                return;
            }
            
            if (!UIHelper.IsValidDefName(workingSkin.defName))
            {
                ShowStatus("CS_Studio_Err_DefNameInvalid".Translate());
                return;
            }
            
            if (string.IsNullOrEmpty(workingSkin.label))
            {
                workingSkin.label = workingSkin.defName;
            }
            
            // TODO: 实现实际保存逻辑（保存到文件或数据库）
            // 当前仅标记为已保存
            ShowStatus("CS_Studio_Msg_SaveSuccess".Translate());
            isDirty = false;
        }

        private void OnExportMod()
        {
            // 导出前验证
            if (workingSkin.layers == null || workingSkin.layers.Count == 0)
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
            
            // 检查图层是否有有效纹理
            bool hasValidTexture = workingSkin.layers.Any(l => !string.IsNullOrEmpty(l.texPath));
            if (!hasValidTexture)
            {
                ShowStatus("CS_Studio_Warn_NoTexture".Translate());
            }
            
            Find.WindowStack.Add(new Dialog_ExportMod(workingSkin, workingAbilities));
        }

        private void OnOpenSkinSettings()
        {
            Find.WindowStack.Add(new Dialog_SkinSettings(workingSkin, () => { isDirty = true; RefreshPreview(); }));
        }

        private void OnAddLayer()
        {
            var newLayer = new PawnLayerConfig
            {
                layerName = $"Layer {workingSkin.layers.Count + 1}",
                anchorTag = "Head"
            };
            workingSkin.layers.Add(newLayer);
            selectedLayerIndex = workingSkin.layers.Count - 1;
            isDirty = true;
            RefreshPreview();
        }

        private void OnRemoveLayer()
        {
            if (selectedLayerIndex >= 0 && selectedLayerIndex < workingSkin.layers.Count)
            {
                workingSkin.layers.RemoveAt(selectedLayerIndex);
                if (selectedLayerIndex >= workingSkin.layers.Count)
                {
                    selectedLayerIndex = workingSkin.layers.Count - 1;
                }
                isDirty = true;
                RefreshPreview();
            }
        }

        private void ShowLayerOrderMenu()
        {
            if (selectedLayerIndex < 0) return;

            var options = new List<FloatMenuOption>();
            
            if (selectedLayerIndex > 0)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveUp".Translate(), () =>
                {
                    var layer = workingSkin.layers[selectedLayerIndex];
                    workingSkin.layers.RemoveAt(selectedLayerIndex);
                    workingSkin.layers.Insert(selectedLayerIndex - 1, layer);
                    selectedLayerIndex--;
                    isDirty = true;
                    RefreshPreview();
                }));
            }
            
            if (selectedLayerIndex < workingSkin.layers.Count - 1)
            {
                options.Add(new FloatMenuOption("CS_Studio_Panel_MoveDown".Translate(), () =>
                {
                    var layer = workingSkin.layers[selectedLayerIndex];
                    workingSkin.layers.RemoveAt(selectedLayerIndex);
                    workingSkin.layers.Insert(selectedLayerIndex + 1, layer);
                    selectedLayerIndex++;
                    isDirty = true;
                    RefreshPreview();
                }));
            }

            options.Add(new FloatMenuOption("CS_Studio_Panel_Duplicate".Translate(), () =>
            {
                var layer = workingSkin.layers[selectedLayerIndex].Clone();
                layer.layerName += " (Copy)";
                workingSkin.layers.Insert(selectedLayerIndex + 1, layer);
                selectedLayerIndex++;
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
                        ShowStatus($"已添加隐藏标签: {tag}");
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

        private void OnImportFromMap()
        {
            // 获取地图上所有可选的 Pawn
            var map = Find.CurrentMap;
            if (map == null)
            {
                ShowStatus("CS_Studio_Err_NoMap".Translate());
                return;
            }

            var pawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && p.Drawer?.renderer?.renderTree != null)
                .ToList();

            if (pawns.Count == 0)
            {
                ShowStatus("CS_Studio_Err_NoPawns".Translate());
                return;
            }

            // 显示选择菜单
            var options = new List<FloatMenuOption>();
            foreach (var pawn in pawns)
            {
                var p = pawn; // 捕获变量
                options.Add(new FloatMenuOption(
                    $"{p.LabelShort} ({p.kindDef.label})",
                    () => DoImportFromPawn(p)
                ));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DoImportFromPawn(Pawn pawn)
        {
            var importResult = VanillaImportUtility.ImportFromPawnWithPaths(pawn);
            var layers = importResult.layers;
            var sourcePaths = importResult.sourcePaths;
            var sourceTags = importResult.sourceTags;
            
            if (layers.Count == 0)
            {
                ShowStatus("CS_Studio_Err_ImportFailed".Translate(pawn.LabelShort));
                return;
            }

            // 同步人偶的种族，确保预览使用正确的种族模型
            // 这对于 HAR 等自定义种族尤为重要
            if (mannequin != null && pawn.def != null)
            {
                mannequin.SetRace(pawn.def);
                mannequin.CopyAppearanceFrom(pawn);
                Log.Message($"[CharacterStudio] 已将人偶种族同步为 {pawn.def.defName} 并复制外观");
            }

            // 询问是否清空现有图层
            if (workingSkin.layers.Count > 0)
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("CS_Studio_Ctx_ClearAndImport".Translate(), () =>
                    {
                        workingSkin.layers.Clear();
                        workingSkin.hiddenPaths.Clear();
                        workingSkin.hiddenTags.Clear();
                        foreach (var layer in layers)
                        {
                            workingSkin.layers.Add(layer);
                        }
                        // 隐藏对应的原生节点，避免与导入的图层重叠
                        foreach (var path in sourcePaths)
                        {
                            if (!string.IsNullOrEmpty(path) && !workingSkin.hiddenPaths.Contains(path))
                            {
                                workingSkin.hiddenPaths.Add(path);
                            }
                        }
                        // 使用标签作为回退隐藏机制（当路径匹配失败时）
                        foreach (var tag in sourceTags)
                        {
                            if (!string.IsNullOrEmpty(tag) && !workingSkin.hiddenTags.Contains(tag))
                            {
                                workingSkin.hiddenTags.Add(tag);
                            }
                        }
                        selectedLayerIndex = 0;
                        isDirty = true;
                        RefreshPreview();
                        // 刷新渲染树快照，以便在树视图中显示新注入的节点
                        RefreshRenderTree();
                        ShowStatus("CS_Studio_Msg_Imported".Translate(pawn.LabelShort, layers.Count));
                    }),
                    new FloatMenuOption("CS_Studio_Ctx_AppendImport".Translate(), () =>
                    {
                        foreach (var layer in layers)
                        {
                            workingSkin.layers.Add(layer);
                        }
                        // 隐藏对应的原生节点，避免与导入的图层重叠
                        foreach (var path in sourcePaths)
                        {
                            if (!string.IsNullOrEmpty(path) && !workingSkin.hiddenPaths.Contains(path))
                            {
                                workingSkin.hiddenPaths.Add(path);
                            }
                        }
                        // 使用标签作为回退隐藏机制（当路径匹配失败时）
                        foreach (var tag in sourceTags)
                        {
                            if (!string.IsNullOrEmpty(tag) && !workingSkin.hiddenTags.Contains(tag))
                            {
                                workingSkin.hiddenTags.Add(tag);
                            }
                        }
                        selectedLayerIndex = workingSkin.layers.Count - layers.Count;
                        isDirty = true;
                        RefreshPreview();
                        // 刷新渲染树快照
                        RefreshRenderTree();
                        ShowStatus("CS_Studio_Msg_Appended".Translate(pawn.LabelShort, layers.Count));
                    }),
                    new FloatMenuOption("CS_Studio_Btn_Cancel".Translate(), () => { })
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                // 直接导入
                foreach (var layer in layers)
                {
                    workingSkin.layers.Add(layer);
                }
                // 隐藏对应的原生节点，避免与导入的图层重叠
                foreach (var path in sourcePaths)
                {
                    if (!string.IsNullOrEmpty(path) && !workingSkin.hiddenPaths.Contains(path))
                    {
                        workingSkin.hiddenPaths.Add(path);
                    }
                }
                // 使用标签作为回退隐藏机制（当路径匹配失败时）
                foreach (var tag in sourceTags)
                {
                    if (!string.IsNullOrEmpty(tag) && !workingSkin.hiddenTags.Contains(tag))
                    {
                        workingSkin.hiddenTags.Add(tag);
                    }
                }
                selectedLayerIndex = 0;
                isDirty = true;
                RefreshPreview();
                // 刷新渲染树快照
                RefreshRenderTree();
                ShowStatus("CS_Studio_Msg_Imported".Translate(pawn.LabelShort, layers.Count));
            }
        }

        // ─────────────────────────────────────────────
        // Mannequin 管理
        // ─────────────────────────────────────────────

        private void InitializeMannequin()
        {
            try
            {
                mannequin = new MannequinManager();
                mannequin.Initialize();
                RefreshPreview();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 初始化人偶失败: {ex}");
                ShowStatus("CS_Studio_Err_MannequinFailed".Translate());
            }
        }

        private void CleanupMannequin()
        {
            mannequin?.Cleanup();
            mannequin = null;
        }

        private void RefreshPreview()
        {
            mannequin?.ApplySkin(workingSkin);
        }

        private void ShowStatus(string message)
        {
            statusMessage = message;
            statusMessageTime = 3f;
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
