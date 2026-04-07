using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public sealed class Dialog_RuntimeTriggerStatus : Window
    {
        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(860f, 700f);

        public Dialog_RuntimeTriggerStatus()
        {
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
            optionalTitle = "CS_Studio_RuntimeTriggerStatus_Title".Translate();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Rect titleRect = UIHelper.DrawPanelShell(shellRect, "CS_Studio_RuntimeTriggerStatus_Title".Translate(), 0f);

            Rect scrollRect = new Rect(0f, titleRect.yMax + 8f, inRect.width, inRect.height - titleRect.yMax - 52f);
            UIHelper.DrawContentCard(scrollRect);

            List<CharacterRuntimeTriggerDef> triggers = CharacterRuntimeTriggerRegistry.AllTriggers
                .Where(static trigger => trigger != null)
                .OrderBy(static trigger => trigger.sortOrder)
                .ThenBy(static trigger => trigger.defName)
                .ToList();
            float contentHeight = Mathf.Max(scrollRect.height - 4f, 80f + (triggers.Count * 144f));
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(scrollRect.ContractedBy(2f), ref scrollPos, viewRect);
            float y = 0f;
            float width = viewRect.width;

            UIHelper.DrawInfoBanner(ref y, width, "CS_Studio_RuntimeTriggerStatus_Hint".Translate(), accent: true);

            if (triggers.Count == 0)
            {
                UIHelper.DrawInfoBanner(ref y, width, "CS_Studio_RuntimeTriggerStatus_Empty".Translate());
            }
            else
            {
                CharacterRuntimeTriggerComponent? component = Current.Game?.GetComponent<CharacterRuntimeTriggerComponent>();
                int currentTick = Find.TickManager?.TicksGame ?? 0;
                Widgets.Label(new Rect(0f, y, width - 140f, 24f), "CS_Studio_RuntimeTriggerStatus_LiveHint".Translate(currentTick));
                if (UIHelper.DrawToolbarButton(new Rect(width - 132f, y, 132f, 24f), "CS_Studio_RuntimeTriggerStatus_ResetScroll".Translate()))
                {
                    scrollPos = Vector2.zero;
                }
                y += 30f;

                foreach (CharacterRuntimeTriggerDef trigger in triggers)
                {
                    DrawTriggerStatusCard(ref y, width, trigger, component, currentTick);
                }
            }

            Widgets.EndScrollView();

            float buttonY = inRect.height - 36f;
            if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f - 60f, buttonY, 120f, 28f), "CS_Studio_Btn_OK".Translate(), accent: true))
            {
                Close();
            }
        }

        private static void DrawTriggerStatusCard(ref float y, float width, CharacterRuntimeTriggerDef trigger, CharacterRuntimeTriggerComponent? component, int currentTick)
        {
            List<Map> maps = Current.Game?.Maps?.ToList() ?? new List<Map>();
            float height = 88f + Math.Max(1, maps.Count) * 32f;
            Rect cardRect = UIHelper.DrawSectionCard(ref y, width, trigger.GetDisplayLabel(), height, accent: trigger.enabled);

            float sectionY = cardRect.y;
            Widgets.Label(new Rect(0f, sectionY, cardRect.width, 24f), $"{trigger.defName} | {trigger.DescribeConditions()}");
            sectionY += 24f;

            if (maps.Count == 0)
            {
                Widgets.Label(new Rect(0f, sectionY, cardRect.width, 24f), "CS_Studio_RuntimeTriggerStatus_NoMap".Translate());
                return;
            }

            foreach (Map map in maps)
            {
                CharacterRuntimeTriggerSnapshot snapshot = component?.GetSnapshot(trigger, map, currentTick) ?? CharacterRuntimeTriggerSnapshot.Empty(trigger.defName ?? string.Empty, map.uniqueID);
                DrawMapStatusRow(ref sectionY, cardRect.width, map, snapshot, trigger, component, currentTick);
            }
        }

        private static void DrawMapStatusRow(ref float y, float width, Map map, CharacterRuntimeTriggerSnapshot snapshot, CharacterRuntimeTriggerDef trigger, CharacterRuntimeTriggerComponent? component, int currentTick)
        {
            Rect rowRect = new Rect(0f, y, width, 28f);
            bool isCurrentMap = map == Find.CurrentMap;
            Color background = snapshot.CanFire && snapshot.ConditionsSatisfied
                ? new Color(0.18f, 0.34f, 0.22f, 0.65f)
                : snapshot.ConditionsSatisfied
                    ? new Color(0.34f, 0.28f, 0.12f, 0.65f)
                    : new Color(0.18f, 0.18f, 0.18f, 0.45f);
            Widgets.DrawBoxSolid(rowRect, background);
            GUI.color = isCurrentMap ? UIHelper.AccentColor : UIHelper.BorderColor;
            Widgets.DrawBox(rowRect, 1);
            GUI.color = Color.white;

            string mapLabel = map.Parent?.LabelCap ?? $"Map {map.uniqueID}";
            string line = "CS_Studio_RuntimeTriggerStatus_Line".Translate(
                mapLabel,
                snapshot.ShouldEvaluate ? "CS_Studio_RuntimeTriggerStatus_Yes".Translate() : "CS_Studio_RuntimeTriggerStatus_No".Translate(),
                snapshot.ConditionsSatisfied ? "CS_Studio_RuntimeTriggerStatus_Yes".Translate() : "CS_Studio_RuntimeTriggerStatus_No".Translate(),
                snapshot.CanFire ? "CS_Studio_RuntimeTriggerStatus_Yes".Translate() : "CS_Studio_RuntimeTriggerStatus_No".Translate(),
                snapshot.LastTriggeredTick >= 0 ? snapshot.LastTriggeredTick.ToString() : "-",
                snapshot.RemainingCooldownTicks > 0 ? snapshot.RemainingCooldownTicks.ToString() : "0");
            Widgets.Label(new Rect(rowRect.x + 8f, rowRect.y + 3f, rowRect.width - 118f, 22f), line);

            if (isCurrentMap && UIHelper.DrawToolbarButton(new Rect(rowRect.x + rowRect.width - 104f, rowRect.y + 2f, 96f, 24f), "CS_Studio_RuntimeTriggerStatus_Test".Translate()))
            {
                CharacterRuntimeTriggerSnapshot currentSnapshot = component?.GetSnapshot(trigger, map, currentTick) ?? snapshot;
                string message = "CS_Studio_RuntimeTriggerStatus_TestMessage".Translate(
                    trigger.GetDisplayLabel(),
                    currentSnapshot.ShouldEvaluate ? "CS_Studio_RuntimeTriggerStatus_Yes".Translate() : "CS_Studio_RuntimeTriggerStatus_No".Translate(),
                    currentSnapshot.ConditionsSatisfied ? "CS_Studio_RuntimeTriggerStatus_Yes".Translate() : "CS_Studio_RuntimeTriggerStatus_No".Translate(),
                    currentSnapshot.CanFire ? "CS_Studio_RuntimeTriggerStatus_Yes".Translate() : "CS_Studio_RuntimeTriggerStatus_No".Translate(),
                    currentSnapshot.RemainingCooldownTicks > 0 ? currentSnapshot.RemainingCooldownTicks.ToString() : "0");
                Messages.Message(message, MessageTypeDefOf.NeutralEvent, false);
            }

            y += 32f;
        }
    }
}
