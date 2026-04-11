using System;
using System.Collections.Generic;
using System.Linq;
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
        private string searchText = string.Empty;
        private string cachedSearchText = string.Empty;
        private List<TDef> cachedVisibleDefs = new List<TDef>();
        private Vector2 scrollPos;
        private TDef? hoveredDef;

        public override Vector2 InitialSize => new Vector2(640f, 680f);

        public Dialog_DefBrowser(string title, IEnumerable<TDef> defs, Action<TDef> onSelected, Func<TDef, string> labelGetter, Func<TDef, string>? metaGetter = null)
        {
            this.title = title;
            this.allDefs = defs?.Where(def => def != null).ToList() ?? new List<TDef>();
            this.onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
            this.labelGetter = labelGetter ?? throw new ArgumentNullException(nameof(labelGetter));
            this.metaGetter = metaGetter ?? (_ => string.Empty);
            doCloseX = true;
            doCloseButton = true;
            draggable = true;
            resizeable = true;
            absorbInputAroundWindow = true;
            optionalTitle = title;
            
            UpdateVisibleDefsCache();
        }

        public override void DoWindowContents(Rect inRect)
        {
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
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float y = 40f;
            Rect searchRect = new Rect(0f, y, inRect.width, 30f);
            Widgets.DrawBoxSolid(searchRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(searchRect.x, searchRect.yMax - 2f, searchRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(searchRect, 1);
            GUI.color = Color.white;
            
            string newSearchText = Widgets.TextEntryLabeled(searchRect.ContractedBy(6f), "CS_Studio_Browser_Search".Translate(), searchText);
            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                UpdateVisibleDefsCache();
            }

            y += 40f;
            Rect listRect = new Rect(0f, y, inRect.width, inRect.height - y - 10f);
            List<TDef> visibleDefs = cachedVisibleDefs;
            float contentHeight = Mathf.Max(listRect.height - 4f, visibleDefs.Count * 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, contentHeight);

            Widgets.DrawBoxSolid(listRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(listRect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(listRect.x, listRect.y, listRect.width, 2f), UIHelper.AccentSoftColor);

            hoveredDef = null;
            Widgets.BeginScrollView(listRect.ContractedBy(2f), ref scrollPos, viewRect);
            
            float itemHeight = 76f;
            int firstVisible = Mathf.Max(0, Mathf.FloorToInt(scrollPos.y / itemHeight));
            int lastVisible = Mathf.Min(visibleDefs.Count - 1, Mathf.FloorToInt((scrollPos.y + listRect.height) / itemHeight));

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                DrawDefEntry(visibleDefs[i], i, viewRect.width, i * itemHeight);
            }
            Widgets.EndScrollView();
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
                    .Where(def => labelGetter(def).ToLowerInvariant().Contains(loweredSearch)
                        || (metaGetter(def) ?? string.Empty).ToLowerInvariant().Contains(loweredSearch)
                        || (def.defName ?? string.Empty).ToLowerInvariant().Contains(loweredSearch))
                    .ToList();
            }
        }

        private void DrawDefEntry(TDef def, int index, float width, float y)
        {
            Rect entryRect = new Rect(0f, y, width, 72f);
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
    }
}
