using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterStudio.Rendering;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public class Dialog_FileBrowser : Window
    {
        private const float HeaderHeight = 30f;
        private const float ToolbarGap = 10f;
        private const float SearchHeight = 24f;
        private const float SectionGap = 10f;
        private const float HoverPreviewSize = 300f;
        private const float EntryHeight = 72f;
        private const float ThumbnailSize = 56f;
        private const float EntryPadding = 8f;
        private const float BorderThickness = 1f;

        private static readonly HashSet<string> SupportedThumbnailExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
            ".gif",
            ".tga",
            ".psd",
            ".tif",
            ".tiff",
            ".webp"
        };

        private readonly Dictionary<string, Texture2D?> thumbnailCache = new Dictionary<string, Texture2D?>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> thumbnailLoadFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> pendingThumbnailLoads = new Queue<string>();
        private readonly HashSet<string> queuedThumbnailLoads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string currentPath;
        private readonly Action<string>? onFileSelected;
        private readonly string filter;
        private Vector2 scrollPos;
        private string searchText = "";
        private string? selectedFilePath;
        private string? hoveredFilePath;
        
        private readonly bool multiSelect;
        private readonly Action<List<string>>? onFilesSelected;
        private readonly HashSet<string> multiSelectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private List<FileSystemInfo> currentFiles = new List<FileSystemInfo>();

        public override Vector2 InitialSize => new Vector2(640f, 680f);

        public Dialog_FileBrowser(string initialPath, Action<string> onSelect, string fileFilter = "*.png", string? defaultRoot = null)
        {
            string fallbackPath = !string.IsNullOrWhiteSpace(defaultRoot) ? defaultRoot! : Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio");
            try
            {
                if (!Directory.Exists(fallbackPath))
                {
                    Directory.CreateDirectory(fallbackPath);
                }
            }
            catch { }

            currentPath = ResolveStartingPath(initialPath, fallbackPath);

            onFileSelected = onSelect;
            filter = fileFilter;
            doCloseX = true;
            doCloseButton = true;
            draggable = true;
            resizeable = true;
            absorbInputAroundWindow = true;

            RefreshFileList();
        }

        public Dialog_FileBrowser(string initialPath, Action<List<string>> onSelectMulti, string fileFilter = "*.xml", string? defaultRoot = null)
        {
            string fallbackPath = !string.IsNullOrWhiteSpace(defaultRoot) ? defaultRoot! : Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio");
            try
            {
                if (!Directory.Exists(fallbackPath))
                {
                    Directory.CreateDirectory(fallbackPath);
                }
            }
            catch { }

            currentPath = ResolveStartingPath(initialPath, fallbackPath);

            this.onFilesSelected = onSelectMulti;
            this.multiSelect = true;
            filter = fileFilter;
            doCloseX = true;
            doCloseButton = true;
            draggable = true;
            resizeable = true;
            absorbInputAroundWindow = true;
            optionalTitle = "CS_Studio_Browser_Title".Translate();

            RefreshFileList();
        }

        private static string ResolveStartingPath(string? path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return fallback;
            }

            try
            {
                string trimmed = path!.Trim();
                if (Directory.Exists(trimmed))
                {
                    return trimmed;
                }

                if (File.Exists(trimmed))
                {
                    string? dir = Path.GetDirectoryName(trimmed);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    {
                        return dir;
                    }
                }

                // Try to see if the path is a valid absolute path format but doesn't exist yet
                if (Path.IsPathRooted(trimmed))
                {
                    string? dir = Path.GetDirectoryName(trimmed);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    {
                        return dir;
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return fallback;
        }

        private void RefreshFileList()
        {
            currentFiles.Clear();
            pendingThumbnailLoads.Clear();
            queuedThumbnailLoads.Clear();
            try
            {
                DirectoryInfo dir = new DirectoryInfo(currentPath);

                currentFiles.AddRange(dir.GetDirectories());

                HashSet<string> seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string[] filters = filter.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (filters.Length == 0)
                {
                    filters = new[] { "*.*" };
                }

                foreach (string fileFilter in filters)
                {
                    foreach (FileInfo file in dir.GetFiles(fileFilter.Trim()))
                    {
                        if (seenFiles.Add(file.FullName))
                        {
                            currentFiles.Add(file);
                        }
                    }
                }

                currentFiles = currentFiles
                    .OrderByDescending(static f => f is DirectoryInfo)
                    .ThenBy(static f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!string.IsNullOrWhiteSpace(selectedFilePath)
                    && currentFiles.All(f => !string.Equals(f.FullName, selectedFilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    selectedFilePath = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] Error accessing directory {currentPath}: {ex.Message}");
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            ProcessPendingThumbnails();

            Rect titleRect = new Rect(0f, 0f, inRect.width, HeaderHeight);
            Widgets.DrawBoxSolid(titleRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(titleRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), "CS_Studio_Browser_Title".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float y = HeaderHeight + ToolbarGap;

            DrawNavigationBar(inRect, ref y);
            DrawSearchBar(inRect, ref y);
            DrawBody(inRect, y);
            DrawHoverPreviewOverlay(inRect);
        }

        private void DrawNavigationBar(Rect inRect, ref float y)
        {
            Rect upButtonRect = new Rect(0f, y, 78f, 26f);
            if (DrawBrowserButton(upButtonRect, "CS_Studio_Browser_Up".Translate(), accent: true))
            {
                DirectoryInfo parent = Directory.GetParent(currentPath);
                if (parent != null)
                {
                    currentPath = parent.FullName;
                    searchText = "";
                    scrollPos = Vector2.zero;
                    RefreshFileList();
                }
            }

            Rect pathRect = new Rect(upButtonRect.xMax + 8f, y, inRect.width - upButtonRect.width - 8f, 42f);
            Widgets.DrawBoxSolid(pathRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(pathRect.x, pathRect.yMax - 2f, pathRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(pathRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(pathRect.ContractedBy(6f), currentPath);
            GUI.color = Color.white;
            Text.Font = oldFont;
            TooltipHandler.TipRegion(pathRect, currentPath);

            y += 48f;
        }

        private void DrawSearchBar(Rect inRect, ref float y)
        {
            Rect searchRect = new Rect(0f, y, inRect.width, 30f);
            Widgets.DrawBoxSolid(searchRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(searchRect.x, searchRect.yMax - 2f, searchRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(searchRect, 1);
            GUI.color = Color.white;

            Rect inner = searchRect.ContractedBy(4f);
            float modsBtnWidth = 64f;
            
            // Mods 快速访问按钮
            Rect modsBtnRect = new Rect(inner.x, inner.y, modsBtnWidth, inner.height);
            if (DrawBrowserButton(modsBtnRect, "Mods", accent: true))
            {
                currentPath = GenFilePaths.ModsFolderPath;
                searchText = "";
                scrollPos = Vector2.zero;
                RefreshFileList();
            }
            TooltipHandler.TipRegion(modsBtnRect, "CS_Studio_Browser_ModsDir".Translate());

            // 搜索输入框
            Rect searchInnerRect = new Rect(modsBtnRect.xMax + 8f, inner.y, inner.width - modsBtnWidth - 8f, inner.height);
            searchText = Widgets.TextEntryLabeled(searchInnerRect, "CS_Studio_Browser_Search".Translate() + " ", searchText);
            y += searchRect.height + SectionGap;
        }

        private void DrawBody(Rect inRect, float y)
        {
            float bottomReserved = multiSelect ? 40f : 0f;
            Rect bodyRect = new Rect(0f, y, inRect.width, inRect.height - y - bottomReserved - 10f);
            DrawFileList(bodyRect);
            
            if (multiSelect)
            {
                Rect bottomBar = new Rect(0f, inRect.height - 40f, inRect.width, 40f);
                if (UIHelper.DrawToolbarButton(new Rect(bottomBar.xMax - 120f, bottomBar.y + 4f, 120f, 32f), "CS_Studio_Btn_OK".Translate(), accent: true))
                {
                    onFilesSelected?.Invoke(multiSelectedPaths.ToList());
                    Close();
                }
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(bottomBar.x, bottomBar.y, bottomBar.width - 130f, 40f), "已选择 {0} 个文件".Formatted(multiSelectedPaths.Count));
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawFileList(Rect listRect)
        {
            List<FileSystemInfo> visibleFiles = GetVisibleFiles();
            float contentHeight = Mathf.Max(listRect.height - 4f, visibleFiles.Count * (EntryHeight + 4f));
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, contentHeight);

            Widgets.DrawBoxSolid(listRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(listRect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(listRect.x, listRect.y, listRect.width, 2f), UIHelper.AccentSoftColor);

            hoveredFilePath = null;

            Widgets.BeginScrollView(listRect.ContractedBy(2f), ref scrollPos, viewRect);

            for (int i = 0; i < visibleFiles.Count; i++)
            {
                DrawFileEntry(visibleFiles[i], i, viewRect.width, i * (EntryHeight + 4f));
            }

            Widgets.EndScrollView();
        }

        private void DrawFileEntry(FileSystemInfo item, int index, float width, float y)
        {
            Rect entryRect = new Rect(0f, y, width, EntryHeight);
            
            bool isSelected = false;
            if (multiSelect && item is FileInfo)
            {
                isSelected = multiSelectedPaths.Contains(item.FullName);
            }
            else
            {
                isSelected = !string.IsNullOrWhiteSpace(selectedFilePath)
                    && string.Equals(selectedFilePath, item.FullName, StringComparison.OrdinalIgnoreCase);
            }
            bool isHovered = Mouse.IsOver(entryRect);

            UIHelper.DrawAlternatingRowBackground(entryRect, index);

            if (isSelected)
            {
                Widgets.DrawBoxSolid(entryRect, UIHelper.ActiveTabColor);
                Widgets.DrawBoxSolid(new Rect(entryRect.x, entryRect.yMax - 2f, entryRect.width, 2f), UIHelper.AccentColor);
            }
            else if (isHovered)
            {
                Widgets.DrawBoxSolid(entryRect, new Color(1f, 1f, 1f, 0.04f));
            }

            GUI.color = isHovered ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(entryRect, 1);
            GUI.color = Color.white;

            if (isHovered)
            {
                hoveredFilePath = item.FullName;
            }

            if (Widgets.ButtonInvisible(entryRect))
            {
                HandleItemClick(item);
            }

            Rect thumbRect = new Rect(entryRect.x + EntryPadding, entryRect.y + (EntryHeight - ThumbnailSize) / 2f, ThumbnailSize, ThumbnailSize);
            DrawEntryThumbnail(item, thumbRect);

            float textX = thumbRect.xMax + 10f;
            float textWidth = Mathf.Max(40f, entryRect.width - textX - EntryPadding - 20f);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = isSelected ? Color.white : UIHelper.HeaderColor;
            Widgets.Label(new Rect(textX, entryRect.y + 8f, textWidth, 24f), item.Name);

            Text.Font = GameFont.Tiny;
            GUI.color = isSelected ? new Color(1f, 1f, 1f, 0.82f) : UIHelper.SubtleColor;
            Widgets.Label(new Rect(textX, entryRect.y + 34f, textWidth, 18f), GetMetadataLabel(item));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            if (isHovered && item is FileInfo)
            {
                Rect hoverHintRect = new Rect(entryRect.xMax - 20f, entryRect.y + 8f, 14f, 14f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = UIHelper.AccentColor;
                Widgets.Label(hoverHintRect, "◈");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }

            TooltipHandler.TipRegion(entryRect, item.FullName);
        }

        private void HandleItemClick(FileSystemInfo item)
        {
            if (item is DirectoryInfo)
            {
                currentPath = item.FullName;
                searchText = "";
                scrollPos = Vector2.zero;
                RefreshFileList();
                return;
            }

            if (multiSelect)
            {
                if (multiSelectedPaths.Contains(item.FullName))
                {
                    multiSelectedPaths.Remove(item.FullName);
                }
                else
                {
                    multiSelectedPaths.Add(item.FullName);
                }
            }
            else
            {
                selectedFilePath = item.FullName;
                onFileSelected?.Invoke(item.FullName);
                Close();
            }
        }

        private void DrawEntryThumbnail(FileSystemInfo item, Rect rect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Texture2D texture = GetThumbnailTexture(item);
            if (texture != null)
            {
                DrawScaledTexture(rect.ContractedBy(3f), texture);
                return;
            }

            DrawFallbackGlyph(rect, item is DirectoryInfo ? "DIR" : "IMG");
        }

        private List<FileSystemInfo> GetVisibleFiles()
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return currentFiles;
            }

            string loweredSearch = searchText.Trim().ToLowerInvariant();
            return currentFiles
                .Where(item => item.Name.ToLowerInvariant().Contains(loweredSearch))
                .ToList();
        }

        private FileSystemInfo? ResolveSelectedItem()
        {
            if (string.IsNullOrWhiteSpace(selectedFilePath))
            {
                return null;
            }

            return currentFiles.FirstOrDefault(item => string.Equals(item.FullName, selectedFilePath, StringComparison.OrdinalIgnoreCase));
        }

        private void ProcessPendingThumbnails()
        {
            int processedCount = 0;
            // 处理队列中的缩略图，每帧最多处理 2 个，以避免阻塞主线程
            while (processedCount < 2 && pendingThumbnailLoads.Count > 0)
            {
                string resolvedPath = pendingThumbnailLoads.Dequeue();
                queuedThumbnailLoads.Remove(resolvedPath);

                if (thumbnailCache.ContainsKey(resolvedPath) || thumbnailLoadFailures.Contains(resolvedPath))
                {
                    continue;
                }

                Texture2D? texture = RuntimeAssetLoader.LoadTextureRaw(resolvedPath);
                if (texture == null)
                {
                    thumbnailLoadFailures.Add(resolvedPath);
                    thumbnailCache[resolvedPath] = null;
                }
                else
                {
                    thumbnailCache[resolvedPath] = texture;
                }
                processedCount++;
            }
        }

        private Texture2D GetThumbnailTexture(FileSystemInfo item)
        {
            if (item is DirectoryInfo)
            {
                return BaseContent.GreyTex;
            }

            if (!(item is FileInfo file) || !CanShowThumbnail(file.FullName))
            {
                return BaseContent.BadTex;
            }

            string resolvedPath = ResolvePreviewPath(file.FullName);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return BaseContent.BadTex;
            }

            if (thumbnailCache.TryGetValue(resolvedPath, out Texture2D? cachedTexture))
            {
                return cachedTexture ?? BaseContent.BadTex;
            }

            if (thumbnailLoadFailures.Contains(resolvedPath))
            {
                return BaseContent.BadTex;
            }

            if (!queuedThumbnailLoads.Contains(resolvedPath))
            {
                pendingThumbnailLoads.Enqueue(resolvedPath);
                queuedThumbnailLoads.Add(resolvedPath);
            }

            return BaseContent.GreyTex;
        }

        private static bool CanShowThumbnail(string fullPath)
        {
            string extension = Path.GetExtension(fullPath);
            return !string.IsNullOrWhiteSpace(extension) && SupportedThumbnailExtensions.Contains(extension);
        }

        private static string ResolvePreviewPath(string fullPath)
        {
            return RuntimeAssetLoader.ResolveTexturePathForLoad(fullPath);
        }

        private static string GetMetadataLabel(FileSystemInfo item)
        {
            if (item is DirectoryInfo)
            {
                return "文件夹";
            }

            if (item is FileInfo file)
            {
                return $"{file.Extension.ToUpperInvariant()}  ·  {FormatFileSize(file.Length)}";
            }

            return "文件";
        }

        private void DrawHoverPreviewOverlay(Rect inRect)
        {
            FileSystemInfo? hoveredItem = ResolveHoveredItem();
            if (hoveredItem == null || hoveredItem is DirectoryInfo)
            {
                return;
            }

            Vector2 mousePos = Event.current.mousePosition;
            float width = HoverPreviewSize;
            float height = HoverPreviewSize + 52f;
            float x = Mathf.Min(inRect.xMax - width, mousePos.x + 18f);
            float y = Mathf.Min(inRect.yMax - height, mousePos.y + 18f);
            x = Mathf.Max(inRect.x, x);
            y = Mathf.Max(inRect.y, y);

            Rect outerRect = new Rect(x, y, width, height);
            Widgets.DrawBoxSolid(outerRect, UIHelper.PanelFillColor);
            Widgets.DrawBoxSolid(new Rect(outerRect.x, outerRect.yMax - 2f, outerRect.width, 2f), UIHelper.AccentColor);
            GUI.color = UIHelper.HoverOutlineColor;
            Widgets.DrawBox(outerRect, 1);
            GUI.color = Color.white;

            Rect titleRect = new Rect(outerRect.x + 8f, outerRect.y + 6f, outerRect.width - 16f, 18f);
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(titleRect, hoveredItem.Name);
            GUI.color = Color.white;

            Rect previewRect = new Rect(outerRect.x + (outerRect.width - HoverPreviewSize + 24f) / 2f, titleRect.yMax + 6f, HoverPreviewSize - 24f, HoverPreviewSize - 24f);
            DrawPreviewFrame(previewRect);

            Texture2D texture = GetThumbnailTexture(hoveredItem);
            if (texture != null)
            {
                DrawScaledTexture(previewRect.ContractedBy(6f), texture);
            }
            else
            {
                DrawFallbackGlyph(previewRect, "N/A");
            }

            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(outerRect.x + 8f, previewRect.yMax + 6f, outerRect.width - 16f, 16f), GetMetadataLabel(hoveredItem));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private FileSystemInfo? ResolveHoveredItem()
        {
            if (string.IsNullOrWhiteSpace(hoveredFilePath))
            {
                return null;
            }

            return currentFiles.FirstOrDefault(item => string.Equals(item.FullName, hoveredFilePath, StringComparison.OrdinalIgnoreCase));
        }

        private static void DrawPreviewFrame(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
        }

        private static bool DrawBrowserButton(Rect rect, string label, bool accent = false)
        {
            Widgets.DrawBoxSolid(rect, accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), accent ? UIHelper.AccentColor : UIHelper.AccentSoftColor);
            GUI.color = Mouse.IsOver(rect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = accent ? Color.white : UIHelper.HeaderColor;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            return Widgets.ButtonInvisible(rect);
        }

        private static string FormatFileSize(long byteCount)
        {
            if (byteCount < 1024)
            {
                return $"{byteCount} B";
            }

            if (byteCount < 1024 * 1024)
            {
                return $"{byteCount / 1024f:F1} KB";
            }

            return $"{byteCount / (1024f * 1024f):F1} MB";
        }

        private static void DrawScaledTexture(Rect rect, Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            float sourceWidth = Mathf.Max(1f, texture.width);
            float sourceHeight = Mathf.Max(1f, texture.height);
            float widthScale = rect.width / sourceWidth;
            float heightScale = rect.height / sourceHeight;
            float scale = Mathf.Min(widthScale, heightScale);

            float drawWidth = sourceWidth * scale;
            float drawHeight = sourceHeight * scale;
            Rect drawRect = new Rect(
                rect.x + (rect.width - drawWidth) / 2f,
                rect.y + (rect.height - drawHeight) / 2f,
                drawWidth,
                drawHeight);

            GUI.DrawTexture(drawRect, texture, ScaleMode.StretchToFill, true);
        }

        private static void DrawFallbackGlyph(Rect rect, string label)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.72f);
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
