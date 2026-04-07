using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public sealed class Dialog_OptionBrowser<TItem> : Window
    {
        private readonly List<TItem> allItems;
        private readonly Action<TItem> onSelected;
        private readonly Func<TItem, string> labelGetter;
        private readonly Func<TItem, string> metaGetter;
        private readonly string title;
        private string searchText = string.Empty;
        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(560f, 660f);

        public Dialog_OptionBrowser(string title, IEnumerable<TItem> items, Action<TItem> onSelected, Func<TItem, string> labelGetter, Func<TItem, string>? metaGetter = null)
        {
            this.title = title;
            this.allItems = items?.Where(static item => item != null).ToList() ?? new List<TItem>();
            this.onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
            this.labelGetter = labelGetter ?? throw new ArgumentNullException(nameof(labelGetter));
            this.metaGetter = metaGetter ?? (_ => string.Empty);
            doCloseX = true;
            doCloseButton = true;
            draggable = true;
            resizeable = true;
            absorbInputAroundWindow = true;
            optionalTitle = title;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Rect titleRect = UIHelper.DrawPanelShell(shellRect, title, 0f);

            float y = titleRect.yMax + 8f;
            Rect searchRect = new Rect(0f, y, inRect.width, 30f);
            Widgets.DrawBoxSolid(searchRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(searchRect.x, searchRect.yMax - 2f, searchRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(searchRect, 1);
            GUI.color = Color.white;
            searchText = Widgets.TextEntryLabeled(searchRect.ContractedBy(6f), "CS_Studio_Browser_Search".Translate(), searchText);

            y += 40f;
            Rect listRect = new Rect(0f, y, inRect.width, inRect.height - y - 8f);
            List<TItem> visibleItems = GetVisibleItems();
            float contentHeight = Mathf.Max(listRect.height - 4f, visibleItems.Count * 56f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, contentHeight);

            UIHelper.DrawContentCard(listRect);
            Widgets.BeginScrollView(listRect.ContractedBy(2f), ref scrollPos, viewRect);
            for (int i = 0; i < visibleItems.Count; i++)
            {
                DrawItemEntry(visibleItems[i], i, viewRect.width, i * 56f);
            }
            Widgets.EndScrollView();
        }

        private List<TItem> GetVisibleItems()
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return allItems;
            }

            string loweredSearch = searchText.Trim().ToLowerInvariant();
            return allItems
                .Where(item => (labelGetter(item) ?? string.Empty).ToLowerInvariant().Contains(loweredSearch)
                    || (metaGetter(item) ?? string.Empty).ToLowerInvariant().Contains(loweredSearch))
                .ToList();
        }

        private void DrawItemEntry(TItem item, int index, float width, float y)
        {
            Rect entryRect = new Rect(0f, y, width, 52f);
            bool isHovered = Mouse.IsOver(entryRect);
            UIHelper.DrawAlternatingRowBackground(entryRect, index);
            if (isHovered)
            {
                Widgets.DrawBoxSolid(entryRect, new Color(1f, 1f, 1f, 0.04f));
            }

            GUI.color = isHovered ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(entryRect, 1);
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(entryRect))
            {
                onSelected(item);
                Close();
            }

            string label = labelGetter(item) ?? string.Empty;
            string meta = metaGetter(item) ?? string.Empty;

            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(entryRect.x + 10f, entryRect.y + 6f, entryRect.width - 20f, 20f), label);
            GUI.color = UIHelper.SubtleColor;
            if (!string.IsNullOrWhiteSpace(meta))
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(entryRect.x + 10f, entryRect.y + 28f, entryRect.width - 20f, 18f), meta);
                Text.Font = GameFont.Small;
            }

            GUI.color = Color.white;
            TooltipHandler.TipRegion(entryRect, string.IsNullOrWhiteSpace(meta) ? label : $"{label}\n{meta}");
        }
    }
}
