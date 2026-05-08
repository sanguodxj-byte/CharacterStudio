using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_EquipmentAdvancedEditor : Window
    {
        private readonly CharacterEquipmentDef equipment;
        private readonly CharacterEquipmentRenderData renderData;
        private readonly Dialog_SkinEditor editor;
        private readonly HashSet<string> collapsedSections = new HashSet<string>();
        private Vector2 scrollPos;
        private int selectedTab;

        private static readonly string[] TabLabels =
        {
            "CS_Studio_Equip_Tab_ThingDef",
            "CS_Studio_Equip_Tab_Stats",
            "CS_Studio_Equip_Tab_Crafting",
            "CS_Studio_Equip_Tab_Equipment"
        };

        private static readonly string[] EquipmentDefaultCollapsedSections = new[]
        {
            "EquipmentThingDefCore",
            "EquipmentStats",
            "EquipmentApparel",
            "EquipmentWeapon",
            "EquipmentCrafting",
            "BuildingProperties",
            "TurretProperties",
            "EquipmentExtraFields"
        };

        public override Vector2 InitialSize => new Vector2(560f, 720f);

        public Dialog_EquipmentAdvancedEditor(CharacterEquipmentDef equipment, Dialog_SkinEditor editor)
        {
            this.equipment = equipment;
            this.renderData = equipment.renderData;
            this.editor = editor;
            this.doCloseX = false;
            this.draggable = true;
            this.resizeable = true;
            this.forcePause = true;

            foreach (var key in EquipmentDefaultCollapsedSections)
                collapsedSections.Add(key);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect contentRect = UIHelper.DrawDialogFrame(inRect, this);
            float width = contentRect.width;

            // Tab bar
            float tabHeight = 26f;
            float tabWidth = width / TabLabels.Length;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                int idx = i;
                Rect tabRect = new Rect(contentRect.x + i * tabWidth, contentRect.y, tabWidth, tabHeight);
                bool isActive = selectedTab == i;
                DrawTabButton(tabRect, TabLabels[i].Translate(), isActive, () => selectedTab = idx);
            }

            // Content area below tabs
            float contentY = contentRect.y + tabHeight + 4f;
            float contentH = contentRect.height - tabHeight - 4f;
            Rect contentArea = new Rect(contentRect.x, contentY, width, contentH);

            Rect viewRect = new Rect(0f, 0f, contentArea.width - 16f, 6000f);
            Widgets.BeginScrollView(contentArea, ref scrollPos, viewRect);
            float y = 0f;
            float innerWidth = viewRect.width;

            switch (selectedTab)
            {
                case 0:
                    DrawThingDefTab(ref y, innerWidth);
                    break;
                case 1:
                    DrawStatsTab(ref y, innerWidth);
                    break;
                case 2:
                    DrawCraftingTab(ref y, innerWidth);
                    break;
                case 3:
                    DrawEquipmentTab(ref y, innerWidth);
                    break;
            }

            Widgets.EndScrollView();
        }

        private void DrawTabButton(Rect rect, string label, bool active, Action action)
        {
            Widgets.DrawBoxSolid(rect, active ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), active ? UIHelper.AccentColor : UIHelper.AccentSoftColor);
            GUI.color = Mouse.IsOver(rect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = active ? Color.white : UIHelper.HeaderColor;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            if (Widgets.ButtonInvisible(rect))
                action();
        }

        private bool DrawCollapsibleSection(ref float y, float width, string title, string sectionKey)
        {
            bool isCollapsed = collapsedSections.Contains(sectionKey);

            y += 5f;
            Rect rect = new Rect(0, y, width, 24f);

            Widgets.DrawLightHighlight(rect);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UIHelper.HeaderColor;

            string icon = isCollapsed ? "\u25B6" : "\u25BC";
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

        private void MutateEquipmentWithUndo(Action mutation, bool refreshRenderTree = true)
        {
            editor.MutateWithUndo(mutation, refreshPreview: true, refreshRenderTree: refreshRenderTree);
        }

        private void MutateWithUndo(Action mutation, bool refreshRenderTree = false)
        {
            editor.MutateWithUndo(mutation, refreshPreview: true, refreshRenderTree: refreshRenderTree);
        }

        private new const float Margin = 5f;
    }
}
