using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public class Dialog_FileBrowser : Window
    {
        private string currentPath;
        private Action<string> onFileSelected;
        private string filter;
        private Vector2 scrollPos;
        private string searchText = "";

        private List<FileSystemInfo> currentFiles = new List<FileSystemInfo>();

        public override Vector2 InitialSize => new Vector2(600f, 600f);

        public Dialog_FileBrowser(string initialPath, Action<string> onSelect, string fileFilter = "*.png")
        {
            this.currentPath = string.IsNullOrEmpty(initialPath) || !Directory.Exists(initialPath) 
                ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop) 
                : initialPath;
            this.onFileSelected = onSelect;
            this.filter = fileFilter;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.draggable = true;
            this.resizeable = true;

            RefreshFileList();
        }

        private void RefreshFileList()
        {
            currentFiles.Clear();
            try
            {
                DirectoryInfo dir = new DirectoryInfo(currentPath);
                
                // 添加所有目录
                currentFiles.AddRange(dir.GetDirectories());
                
                // 添加匹配的文件
                string[] filters = filter.Split('|');
                foreach (string f in filters)
                {
                    currentFiles.AddRange(dir.GetFiles(f));
                }

                // 排序：文件夹优先，然后按名称
                currentFiles = currentFiles.OrderByDescending(f => f is DirectoryInfo)
                                           .ThenBy(f => f.Name)
                                           .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] Error accessing directory {currentPath}: {ex.Message}");
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30), "CS_Studio_Browser_Title".Translate());
            Text.Font = GameFont.Small;

            float y = 40;

            // 路径栏和上级按钮
            if (Widgets.ButtonText(new Rect(0, y, 60, 24), "CS_Studio_Browser_Up".Translate()))
            {
                DirectoryInfo parent = Directory.GetParent(currentPath);
                if (parent != null)
                {
                    currentPath = parent.FullName;
                    RefreshFileList();
                }
            }

            Widgets.Label(new Rect(70, y, inRect.width - 70, 24), currentPath);
            y += 30;

            // 搜索栏
            searchText = Widgets.TextEntryLabeled(new Rect(0, y, inRect.width, 24), "CS_Studio_Browser_Search".Translate(), searchText);
            y += 30;

            // 文件列表
            Rect listRect = new Rect(0, y, inRect.width, inRect.height - y - 10);
            Rect viewRect = new Rect(0, 0, listRect.width - 16, currentFiles.Count * 24f);

            Widgets.BeginScrollView(listRect, ref scrollPos, viewRect);

            float entryY = 0;
            for (int i = 0; i < currentFiles.Count; i++)
            {
                FileSystemInfo item = currentFiles[i];
                
                // 简单的搜索过滤
                if (!string.IsNullOrEmpty(searchText) && !item.Name.ToLower().Contains(searchText.ToLower()))
                    continue;

                Rect entryRect = new Rect(0, entryY, viewRect.width, 24);
                
                if (i % 2 == 0) Widgets.DrawAltRect(entryRect);

                if (Widgets.ButtonInvisible(entryRect))
                {
                    if (item is DirectoryInfo)
                    {
                        currentPath = item.FullName;
                        searchText = ""; // 进入新目录清除搜索
                        RefreshFileList();
                        break;
                    }
                    else
                    {
                        onFileSelected?.Invoke(item.FullName);
                        Close();
                    }
                }

                Texture2D icon = (item is DirectoryInfo) ? BaseContent.GreyTex : BaseContent.WhiteTex; // 简化图标
                GUI.DrawTexture(new Rect(2, entryY + 2, 20, 20), icon);
                Widgets.Label(new Rect(26, entryY, viewRect.width - 26, 24), item.Name);

                entryY += 24;
            }

            Widgets.EndScrollView();
        }
    }
}