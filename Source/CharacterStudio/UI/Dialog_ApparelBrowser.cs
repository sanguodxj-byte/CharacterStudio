using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 服装选择浏览器
    /// 用于在编辑器预览中快速选择和穿戴原版服装
    /// </summary>
    public class Dialog_ApparelBrowser : Window
    {
        private Vector2 scrollPos;
        private string searchText = "";
        private List<ThingDef> allApparel = new List<ThingDef>();
        private List<ThingDef> filteredApparel = new List<ThingDef>();
        private Action<ThingDef?> onSelect;

        public override Vector2 InitialSize => new Vector2(450f, 600f);

        public Dialog_ApparelBrowser(Action<ThingDef?> onSelect)
        {
            this.onSelect = onSelect;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.draggable = true;
            this.resizeable = true;
            this.absorbInputAroundWindow = false;
            this.closeOnClickedOutside = true;

            // 获取所有服装 Def
            allApparel = MannequinManager.GetAvailableApparel().ToList();
            FilterApparel();
        }

        private void FilterApparel()
        {
            if (string.IsNullOrEmpty(searchText))
            {
                filteredApparel = new List<ThingDef>(allApparel);
                return;
            }

            filteredApparel = allApparel.FindAll(a => 
                (a.label != null && a.label.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) || 
                a.defName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
            );
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect contentRect = UIHelper.DrawDialogFrame(inRect, this);

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 30), "CS_Studio_Preview_SelectApparel".Translate());
            Text.Font = GameFont.Small;

            float y = contentRect.y + 40;

            // 搜索栏
            Rect searchRect = new Rect(contentRect.x, y, contentRect.width, 24);
            string newSearch = Widgets.TextField(searchRect, searchText);
            if (newSearch != searchText)
            {
                searchText = newSearch;
                FilterApparel();
            }
            
            // 搜索占位符提示
            if (string.IsNullOrEmpty(searchText))
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect placeholderRect = new Rect(searchRect.x + 4f, searchRect.y, searchRect.width - 8f, searchRect.height);
                Widgets.Label(placeholderRect, "CS_Studio_Browser_Search".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            
            y += 32;

            // “清除所有”按钮
            Rect clearRect = new Rect(contentRect.x, y, contentRect.width, 30f);
            if (Widgets.ButtonText(clearRect, "CS_Studio_Preview_ClearApparel".Translate()))
            {
                onSelect(null);
                Close();
            }
            y += 35;

            // 列表区域
            Rect listRect = new Rect(contentRect.x, y, contentRect.width, contentRect.y + contentRect.height - y - 45); // 留出关闭按钮空间
            float rowHeight = 36f;
            Rect viewRect = new Rect(0, 0, listRect.width - 16, filteredApparel.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref scrollPos, viewRect);

            float curY = 0;
            foreach (var apparel in filteredApparel)
            {
                Rect rowRect = new Rect(0, curY, viewRect.width, rowHeight);
                
                if (Widgets.ButtonInvisible(rowRect))
                {
                    onSelect(apparel);
                    Close();
                }

                Widgets.DrawHighlightIfMouseover(rowRect);
                
                // 图标
                Rect iconRect = new Rect(rowRect.x + 4, rowRect.y + 2, 32, 32);
                Widgets.ThingIcon(iconRect, apparel);
                
                // 标签和 DefName
                Rect labelRect = new Rect(iconRect.xMax + 8, rowRect.y, rowRect.width - iconRect.width - 12, rowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, apparel.LabelCap);
                
                // 右侧灰色显示 defName (小字)
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                float nameWidth = Text.CalcSize(apparel.defName).x;
                Widgets.Label(new Rect(rowRect.xMax - nameWidth - 5, rowRect.y, nameWidth, rowHeight), apparel.defName);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                
                Text.Anchor = TextAnchor.UpperLeft;

                curY += rowHeight;
            }

            Widgets.EndScrollView();
        }
    }
}
