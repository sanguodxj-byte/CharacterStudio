using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public sealed class Dialog_DefBrowser<TDef> : Window where TDef : Def
    {
        private readonly List<TDef> allDefs;
        private readonly Action<TDef> onSelected;
        private readonly Func<TDef, string> labelGetter;
        private readonly Func<TDef, string> metaGetter;
        private readonly string title;

        // 双模式
        private int activeTab; // 0 = 内存, 1 = 外部文件
        private string searchText = string.Empty;
        private List<TDef> cachedVisibleDefs = new List<TDef>();
        private Vector2 scrollPos;
        private bool searchDirty;

        // 外部文件模式
        private string externalRootPath;
        private string currentExternalPath;
        private List<ExternalDefEntry> externalDefs = new List<ExternalDefEntry>();
        private List<ExternalDefEntry> cachedVisibleExternalDefs = new List<ExternalDefEntry>();
        private Vector2 externalScrollPos;

        // 视图模式: 0=详细(72px), 1=紧凑(22px), 2=网格(多列)
        private int viewMode;

        private const float DetailRowHeight = 72f;
        private const float CompactRowHeight = 22f;
        private const float GridCellWidth = 140f;
        private const float GridCellHeight = 26f;

        private TDef? hoveredDef;

        public override Vector2 InitialSize => new Vector2(640f, 680f);

        /// <summary>
        /// 表示从外部 XML 文件中提取的 Def 条目。
        /// </summary>
        public class ExternalDefEntry
        {
            public string defName = "";
            public string label = "";
            public string parentName = "";
            public string thingClass = "";
            public string filePath = "";
            public string fileName = "";
        }

        /// <summary>
        /// 标准构造函数。自动将外部文件路径默认为 CharacterStudio 配置目录。
        /// </summary>
        public Dialog_DefBrowser(string title, IEnumerable<TDef> defs, Action<TDef> onSelected, Func<TDef, string> labelGetter, Func<TDef, string>? metaGetter = null, string? externalPath = null)
        {
            this.title = title;
            this.allDefs = defs?.Where(def => def != null).ToList() ?? new List<TDef>();
            this.onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
            this.labelGetter = labelGetter ?? throw new ArgumentNullException(nameof(labelGetter));
            this.metaGetter = metaGetter ?? (_ => string.Empty);
            this.externalRootPath = externalPath ?? Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio");
            this.currentExternalPath = this.externalRootPath;
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            resizeable = true;
            absorbInputAroundWindow = true;

            UpdateVisibleDefsCache();
            RefreshExternalDefs();
        }

        public override void DoWindowContents(Rect inRect)
        {
            UIHelper.DrawDialogFrame(inRect, this);

            // ── 标题栏 ──
            Rect titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.DrawBoxSolid(titleRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(titleRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), title);

            // 右侧显示扫描到的数量
            int defCount = activeTab == 0 ? allDefs.Count : externalDefs.Count;
            string countLabel = $"({defCount})";
            float countWidth = Text.CalcSize(countLabel).x;
            GUI.color = UIHelper.SubtleColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), countLabel);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // ── 模式标签页 ──
            float y = 32f;
            float tabWidth = inRect.width / 2f;
            for (int i = 0; i < 2; i++)
            {
                Rect tabRect = new Rect(i * tabWidth, y, tabWidth, 24f);
                bool isActive = activeTab == i;
                Widgets.DrawBoxSolid(tabRect, isActive ? UIHelper.AccentSoftColor : UIHelper.PanelFillSoftColor);
                GUI.color = UIHelper.BorderColor;
                Widgets.DrawBox(tabRect, 1);
                GUI.color = Color.white;

                string tabLabel = i == 0
                    ? "CS_Studio_Browser_TabMemory".Translate()
                    : "CS_Studio_Browser_TabExternal".Translate();
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = isActive ? UIHelper.HeaderColor : UIHelper.SubtleColor;
                Widgets.Label(tabRect, tabLabel);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(tabRect))
                {
                    if (activeTab != i)
                    {
                        activeTab = i;
                        searchText = string.Empty;
                        if (i == 0) UpdateVisibleDefsCache();
                        else UpdateVisibleExternalDefsCache();
                    }
                }
            }

            y += 28f;

            if (activeTab == 0)
            {
                DrawMemoryMode(inRect, y);
            }
            else
            {
                DrawExternalMode(inRect, y);
            }
        }

        // ── 视图模式循环切换按钮 ──

        private void DrawViewModeToggle(float x, float y)
        {
            float btnW = 56f;
            float btnH = 22f;
            Rect btnRect = new Rect(x, y, btnW, btnH);

            Widgets.DrawBoxSolid(btnRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(btnRect, 1);
            GUI.color = Color.white;

            // 显示当前模式图标 + 名称
            string modeIcon = viewMode switch
            {
                0 => "≡ Detail",
                1 => "☰ List",
                2 => "▦ Grid",
                _ => "?"
            };

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(btnRect, modeIcon);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonInvisible(btnRect))
            {
                viewMode = (viewMode + 1) % 3;
            }
        }

        // ── 内存扫描模式 ──

        private void DrawMemoryMode(Rect inRect, float y)
        {
            // 搜索栏 + 视图切换
            float searchWidth = inRect.width - 64f;
            Rect searchRect = new Rect(0f, y + 4f, searchWidth, 24f);
            string newSearchText = Widgets.TextField(searchRect, searchText);
            bool composing = Input.imeIsSelected && !string.IsNullOrEmpty(Input.compositionString);
            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                if (composing)
                    searchDirty = true;
                else
                    UpdateVisibleDefsCache();
            }
            else if (searchDirty && !composing)
            {
                searchDirty = false;
                UpdateVisibleDefsCache();
            }

            // 视图模式切换按钮
            DrawViewModeToggle(searchWidth + 4f, y + 4f);

            y += 40f;
            List<TDef> visibleDefs = cachedVisibleDefs;

            if (viewMode == 2)
            {
                DrawMemoryGrid(inRect, y, visibleDefs);
            }
            else
            {
                float rowHeight = viewMode == 0 ? DetailRowHeight : CompactRowHeight;
                Rect listRect = new Rect(0f, y, inRect.width, inRect.height - y - 10f);
                float contentHeight = Mathf.Max(listRect.height - 4f, visibleDefs.Count * rowHeight);
                Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, contentHeight);

                Widgets.DrawBoxSolid(listRect, UIHelper.PanelFillColor);
                GUI.color = UIHelper.BorderColor;
                Widgets.DrawBox(listRect, 1);
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(new Rect(listRect.x, listRect.y, listRect.width, 2f), UIHelper.AccentSoftColor);

                hoveredDef = null;
                Widgets.BeginScrollView(listRect.ContractedBy(2f), ref scrollPos, viewRect);

                int firstVisible = Mathf.Max(0, Mathf.FloorToInt(scrollPos.y / rowHeight));
                int lastVisible = Mathf.Min(visibleDefs.Count - 1, Mathf.FloorToInt((scrollPos.y + listRect.height) / rowHeight));

                for (int i = firstVisible; i <= lastVisible; i++)
                {
                    if (viewMode == 0)
                        DrawDefEntryDetail(visibleDefs[i], i, viewRect.width, i * rowHeight);
                    else
                        DrawDefEntryCompact(visibleDefs[i], i, viewRect.width, i * rowHeight);
                }
                Widgets.EndScrollView();
            }
        }

        private void UpdateVisibleDefsCache()
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                cachedVisibleDefs = allDefs;
            }
            else
            {
                string loweredSearch = searchText.Trim().ToLowerInvariant();
                cachedVisibleDefs = allDefs
                    .Where(def => (labelGetter(def) ?? string.Empty).ToLowerInvariant().Contains(loweredSearch)
                        || (metaGetter(def) ?? string.Empty).ToLowerInvariant().Contains(loweredSearch)
                        || (def.defName ?? string.Empty).ToLowerInvariant().Contains(loweredSearch))
                    .ToList();
            }
        }

        // ── 详细模式条目 (72px) ──

        private void DrawDefEntryDetail(TDef def, int index, float width, float y)
        {
            Rect entryRect = new Rect(0f, y, width, DetailRowHeight);
            bool isHovered = Mouse.IsOver(entryRect);

            UIHelper.DrawAlternatingRowBackground(entryRect, index);
            if (isHovered)
            {
                Widgets.DrawBoxSolid(entryRect, new Color(1f, 1f, 1f, 0.04f));
                hoveredDef = def;
            }

            GUI.color = isHovered ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(entryRect, 1);
            GUI.color = Color.white;

            if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                onSelected(def);
                Close();
            }

            Rect iconRect = new Rect(entryRect.x + 8f, entryRect.y + 8f, 56f, 56f);
            Widgets.DrawBoxSolid(iconRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(iconRect.x, iconRect.yMax - 2f, iconRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(iconRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UIHelper.AccentColor;
            Widgets.Label(iconRect, "DEF");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float textX = iconRect.xMax + 10f;
            float textWidth = Mathf.Max(40f, entryRect.width - textX - 12f);
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(textX, entryRect.y + 8f, textWidth, 24f), labelGetter(def));
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(textX, entryRect.y + 34f, textWidth, 18f), def.defName ?? string.Empty);
            string meta = metaGetter(def) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(meta))
            {
                Widgets.Label(new Rect(textX, entryRect.y + 50f, textWidth, 16f), meta);
            }

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(entryRect, $"{labelGetter(def)}\n{def.defName}\n{meta}".Trim());
        }

        // ── 紧凑模式条目 (22px) ──

        private void DrawDefEntryCompact(TDef def, int index, float width, float y)
        {
            Rect entryRect = new Rect(0f, y, width, CompactRowHeight);
            bool isHovered = Mouse.IsOver(entryRect);

            UIHelper.DrawAlternatingRowBackground(entryRect, index);
            if (isHovered)
            {
                Widgets.DrawBoxSolid(entryRect, new Color(1f, 1f, 1f, 0.06f));
                hoveredDef = def;
            }

            if (isHovered)
            {
                GUI.color = UIHelper.AccentSoftColor;
                Widgets.DrawBox(entryRect, 1);
                GUI.color = Color.white;
            }

            if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                onSelected(def);
                Close();
            }

            float x = entryRect.x + 4f;
            float w = entryRect.width - 8f;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            // label | defName | meta
            float labelW = Mathf.Min(w * 0.35f, Text.CalcSize(labelGetter(def)).x + 8f);
            float defNameX = x + labelW;
            float defNameW = Mathf.Min(w * 0.35f, Text.CalcSize(def.defName ?? "").x + 8f);

            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(x, entryRect.y, labelW, CompactRowHeight), labelGetter(def));

            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(defNameX, entryRect.y, defNameW, CompactRowHeight), def.defName ?? string.Empty);

            string meta = metaGetter(def) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(meta))
            {
                float metaX = defNameX + defNameW;
                float metaW = Mathf.Max(0f, entryRect.xMax - metaX - 4f);
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(new Rect(metaX, entryRect.y, metaW, CompactRowHeight), meta);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(entryRect, $"{labelGetter(def)}\n{def.defName}\n{meta}".Trim());
        }

        // ── 网格模式 ──

        private void DrawMemoryGrid(Rect inRect, float y, List<TDef> visibleDefs)
        {
            Rect gridRect = new Rect(0f, y, inRect.width, inRect.height - y - 10f);
            float scrollWidth = gridRect.width - 16f;
            int cols = Mathf.Max(1, Mathf.FloorToInt(scrollWidth / GridCellWidth));
            int rows = Mathf.CeilToInt((float)visibleDefs.Count / cols);
            float contentHeight = Mathf.Max(gridRect.height - 4f, rows * GridCellHeight);
            Rect viewRect = new Rect(0f, 0f, scrollWidth, contentHeight);

            Widgets.DrawBoxSolid(gridRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(gridRect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(gridRect.x, gridRect.y, gridRect.width, 2f), UIHelper.AccentSoftColor);

            hoveredDef = null;
            Widgets.BeginScrollView(gridRect.ContractedBy(2f), ref scrollPos, viewRect);

            int firstRow = Mathf.Max(0, Mathf.FloorToInt(scrollPos.y / GridCellHeight));
            int lastRow = Mathf.Min(rows - 1, Mathf.FloorToInt((scrollPos.y + gridRect.height) / GridCellHeight));

            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int idx = row * cols + col;
                    if (idx >= visibleDefs.Count) break;
                    DrawDefEntryGrid(visibleDefs[idx], col * GridCellWidth, row * GridCellHeight, cols);
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawDefEntryGrid(TDef def, float x, float y, int cols)
        {
            Rect cellRect = new Rect(x + 1f, y + 1f, GridCellWidth - 2f, GridCellHeight - 2f);
            bool isHovered = Mouse.IsOver(cellRect);

            Widgets.DrawBoxSolid(cellRect, isHovered ? UIHelper.AccentSoftColor : UIHelper.PanelFillSoftColor);

            if (isHovered)
            {
                GUI.color = UIHelper.HoverOutlineColor;
                Widgets.DrawBox(cellRect, 1);
                GUI.color = Color.white;
            }

            if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                onSelected(def);
                Close();
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            string defName = def.defName ?? "";
            if (defName.Length > 20) defName = defName.Substring(0, 18) + "..";
            GUI.color = isHovered ? UIHelper.HeaderColor : UIHelper.SubtleColor;
            Widgets.Label(new Rect(cellRect.x + 3f, cellRect.y, cellRect.width - 6f, cellRect.height), defName);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(cellRect, $"{labelGetter(def)}\n{def.defName}\n{metaGetter(def)}".Trim());
        }

        // ── 外部文件模式 ──

        private void DrawExternalMode(Rect inRect, float y)
        {
            // 路径栏
            Rect pathRect = new Rect(0f, y, inRect.width - 70f, 24f);
            Widgets.DrawBoxSolid(pathRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(pathRect, 1);
            GUI.color = Color.white;
            string displayPath = currentExternalPath;
            if (displayPath.Length > 60) displayPath = "..." + displayPath.Substring(displayPath.Length - 57);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(pathRect.ContractedBy(4f), displayPath);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // 浏览按钮
            Rect browseRect = new Rect(inRect.width - 66f, y, 66f, 24f);
            if (Widgets.ButtonText(browseRect, "CS_Studio_Browser_Browse".Translate()))
            {
                Find.WindowStack.Add(new Dialog_FileBrowser(currentExternalPath, folderPath =>
                {
                    currentExternalPath = folderPath;
                    RefreshExternalDefs();
                }, true));
            }

            y += 28f;

            // 搜索栏 + 视图切换
            float searchWidth = inRect.width - 64f;
            Rect searchRect = new Rect(0f, y + 4f, searchWidth, 24f);
            string newSearchText = Widgets.TextField(searchRect, searchText);
            bool composing = Input.imeIsSelected && !string.IsNullOrEmpty(Input.compositionString);
            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                if (composing)
                    searchDirty = true;
                else
                    UpdateVisibleExternalDefsCache();
            }
            else if (searchDirty && !composing)
            {
                searchDirty = false;
                UpdateVisibleExternalDefsCache();
            }

            DrawViewModeToggle(searchWidth + 4f, y + 4f);

            y += 40f;
            List<ExternalDefEntry> visibleEntries = cachedVisibleExternalDefs;

            if (viewMode == 2)
            {
                DrawExternalGrid(inRect, y, visibleEntries);
            }
            else
            {
                float rowHeight = viewMode == 0 ? DetailRowHeight : CompactRowHeight;
                Rect listRect = new Rect(0f, y, inRect.width, inRect.height - y - 10f);
                float contentHeight = Mathf.Max(listRect.height - 4f, visibleEntries.Count * rowHeight);
                Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, contentHeight);

                Widgets.DrawBoxSolid(listRect, UIHelper.PanelFillColor);
                GUI.color = UIHelper.BorderColor;
                Widgets.DrawBox(listRect, 1);
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(new Rect(listRect.x, listRect.y, listRect.width, 2f), UIHelper.AccentSoftColor);

                Widgets.BeginScrollView(listRect.ContractedBy(2f), ref externalScrollPos, viewRect);

                int firstVisible = Mathf.Max(0, Mathf.FloorToInt(externalScrollPos.y / rowHeight));
                int lastVisible = Mathf.Min(visibleEntries.Count - 1, Mathf.FloorToInt((externalScrollPos.y + listRect.height) / rowHeight));

                for (int i = firstVisible; i <= lastVisible; i++)
                {
                    if (viewMode == 0)
                        DrawExternalEntryDetail(visibleEntries[i], i, viewRect.width, i * rowHeight);
                    else
                        DrawExternalEntryCompact(visibleEntries[i], i, viewRect.width, i * rowHeight);
                }
                Widgets.EndScrollView();
            }
        }

        private void RefreshExternalDefs()
        {
            externalDefs.Clear();
            try
            {
                if (!Directory.Exists(currentExternalPath)) return;
                var xmlFiles = Directory.GetFiles(currentExternalPath, "*.xml", SearchOption.AllDirectories);
                string defTypeName = typeof(TDef).Name;

                foreach (var file in xmlFiles)
                {
                    try
                    {
                        var doc = XDocument.Load(file);
                        if (doc.Root == null) continue;

                        foreach (var element in doc.Root.Elements())
                        {
                            string elementName = element.Name.LocalName;
                            bool match = string.Equals(elementName, defTypeName, StringComparison.OrdinalIgnoreCase)
                                || (typeof(TDef) == typeof(ThingDef) && elementName.IndexOf("ThingDef", StringComparison.OrdinalIgnoreCase) >= 0);

                            if (!match) continue;

                            var entry = new ExternalDefEntry
                            {
                                defName = element.Element("defName")?.Value ?? "",
                                label = element.Element("label")?.Value ?? "",
                                parentName = element.Attribute("ParentName")?.Value ?? "",
                                thingClass = element.Element("thingClass")?.Value ?? "",
                                filePath = file,
                                fileName = Path.GetFileName(file)
                            };

                            if (!string.IsNullOrWhiteSpace(entry.defName))
                                externalDefs.Add(entry);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            UpdateVisibleExternalDefsCache();
        }

        private void UpdateVisibleExternalDefsCache()
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                cachedVisibleExternalDefs = externalDefs;
            }
            else
            {
                string loweredSearch = searchText.Trim().ToLowerInvariant();
                cachedVisibleExternalDefs = externalDefs
                    .Where(e => e.defName.ToLowerInvariant().Contains(loweredSearch)
                        || e.label.ToLowerInvariant().Contains(loweredSearch)
                        || e.parentName.ToLowerInvariant().Contains(loweredSearch)
                        || e.fileName.ToLowerInvariant().Contains(loweredSearch))
                    .ToList();
            }
        }

        // ── 外部条目详细模式 (72px) ──

        private void DrawExternalEntryDetail(ExternalDefEntry entry, int index, float width, float y)
        {
            Rect entryRect = new Rect(0f, y, width, DetailRowHeight);
            bool isHovered = Mouse.IsOver(entryRect);

            UIHelper.DrawAlternatingRowBackground(entryRect, index);
            if (isHovered)
            {
                Widgets.DrawBoxSolid(entryRect, new Color(1f, 1f, 1f, 0.04f));
            }

            GUI.color = isHovered ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(entryRect, 1);
            GUI.color = Color.white;

            if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                TDef? existingDef = DefDatabase<TDef>.GetNamedSilentFail(entry.defName);
                if (existingDef != null)
                {
                    onSelected(existingDef);
                }
                Close();
            }

            Rect iconRect = new Rect(entryRect.x + 8f, entryRect.y + 8f, 56f, 56f);
            Widgets.DrawBoxSolid(iconRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(iconRect.x, iconRect.yMax - 2f, iconRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(iconRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.6f, 0.8f, 1f);
            Widgets.Label(iconRect, "XML");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float textX = iconRect.xMax + 10f;
            float textWidth = Mathf.Max(40f, entryRect.width - textX - 12f);
            GUI.color = UIHelper.HeaderColor;
            string displayLabel = !string.IsNullOrWhiteSpace(entry.label) ? entry.label : entry.defName;
            Widgets.Label(new Rect(textX, entryRect.y + 8f, textWidth, 24f), displayLabel);
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(textX, entryRect.y + 34f, textWidth, 18f), entry.defName);

            var metaParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(entry.parentName)) metaParts.Add($"Parent: {entry.parentName}");
            if (!string.IsNullOrWhiteSpace(entry.thingClass)) metaParts.Add(entry.thingClass);
            metaParts.Add(entry.fileName);
            string meta = string.Join(" | ", metaParts);
            Widgets.Label(new Rect(textX, entryRect.y + 50f, textWidth, 16f), meta);

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(entryRect, $"{displayLabel}\n{entry.defName}\n{entry.filePath}");
        }

        // ── 外部条目紧凑模式 (22px) ──

        private void DrawExternalEntryCompact(ExternalDefEntry entry, int index, float width, float y)
        {
            Rect entryRect = new Rect(0f, y, width, CompactRowHeight);
            bool isHovered = Mouse.IsOver(entryRect);

            UIHelper.DrawAlternatingRowBackground(entryRect, index);
            if (isHovered)
            {
                Widgets.DrawBoxSolid(entryRect, new Color(1f, 1f, 1f, 0.06f));
            }

            if (isHovered)
            {
                GUI.color = UIHelper.AccentSoftColor;
                Widgets.DrawBox(entryRect, 1);
                GUI.color = Color.white;
            }

            if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                TDef? existingDef = DefDatabase<TDef>.GetNamedSilentFail(entry.defName);
                if (existingDef != null)
                {
                    onSelected(existingDef);
                }
                Close();
            }

            float x = entryRect.x + 4f;
            float w = entryRect.width - 8f;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            string displayLabel = !string.IsNullOrWhiteSpace(entry.label) ? entry.label : entry.defName;
            float labelW = Mathf.Min(w * 0.35f, Text.CalcSize(displayLabel).x + 8f);
            float defNameX = x + labelW;
            float defNameW = Mathf.Min(w * 0.35f, Text.CalcSize(entry.defName).x + 8f);

            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(x, entryRect.y, labelW, CompactRowHeight), displayLabel);

            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(defNameX, entryRect.y, defNameW, CompactRowHeight), entry.defName);

            float metaX = defNameX + defNameW;
            float metaW = Mathf.Max(0f, entryRect.xMax - metaX - 4f);
            if (metaW > 10f)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(new Rect(metaX, entryRect.y, metaW, CompactRowHeight), entry.fileName);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(entryRect, $"{displayLabel}\n{entry.defName}\n{entry.filePath}");
        }

        // ── 外部网格模式 ──

        private void DrawExternalGrid(Rect inRect, float y, List<ExternalDefEntry> visibleEntries)
        {
            Rect gridRect = new Rect(0f, y, inRect.width, inRect.height - y - 10f);
            float scrollWidth = gridRect.width - 16f;
            int cols = Mathf.Max(1, Mathf.FloorToInt(scrollWidth / GridCellWidth));
            int rows = Mathf.CeilToInt((float)visibleEntries.Count / cols);
            float contentHeight = Mathf.Max(gridRect.height - 4f, rows * GridCellHeight);
            Rect viewRect = new Rect(0f, 0f, scrollWidth, contentHeight);

            Widgets.DrawBoxSolid(gridRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(gridRect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(gridRect.x, gridRect.y, gridRect.width, 2f), UIHelper.AccentSoftColor);

            Widgets.BeginScrollView(gridRect.ContractedBy(2f), ref externalScrollPos, viewRect);

            int firstRow = Mathf.Max(0, Mathf.FloorToInt(externalScrollPos.y / GridCellHeight));
            int lastRow = Mathf.Min(rows - 1, Mathf.FloorToInt((externalScrollPos.y + gridRect.height) / GridCellHeight));

            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int idx = row * cols + col;
                    if (idx >= visibleEntries.Count) break;
                    DrawExternalEntryGrid(visibleEntries[idx], col * GridCellWidth, row * GridCellHeight);
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawExternalEntryGrid(ExternalDefEntry entry, float x, float y)
        {
            Rect cellRect = new Rect(x + 1f, y + 1f, GridCellWidth - 2f, GridCellHeight - 2f);
            bool isHovered = Mouse.IsOver(cellRect);

            Widgets.DrawBoxSolid(cellRect, isHovered ? UIHelper.AccentSoftColor : UIHelper.PanelFillSoftColor);

            if (isHovered)
            {
                GUI.color = UIHelper.HoverOutlineColor;
                Widgets.DrawBox(cellRect, 1);
                GUI.color = Color.white;
            }

            if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                TDef? existingDef = DefDatabase<TDef>.GetNamedSilentFail(entry.defName);
                if (existingDef != null)
                {
                    onSelected(existingDef);
                }
                Close();
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            string defName = entry.defName;
            if (defName.Length > 20) defName = defName.Substring(0, 18) + "..";
            GUI.color = isHovered ? UIHelper.HeaderColor : new Color(0.6f, 0.8f, 1f);
            Widgets.Label(new Rect(cellRect.x + 3f, cellRect.y, cellRect.width - 6f, cellRect.height), defName);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(cellRect, $"{entry.label}\n{entry.defName}\n{entry.filePath}");
        }
    }
}
