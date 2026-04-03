using System;
using System.Collections.Generic;
using CharacterStudio.Attributes;
using UnityEngine;
using Verse;
using RimWorld;

namespace CharacterStudio.UI
{
    public class Dialog_AttributeBuffEditor : Window
    {
        private readonly CharacterStatModifierProfile profile;
        private readonly Pawn? targetPawn;
        private readonly Action? onChanged;

        private Vector2 attributeBuffScrollPos;
        private string attributeBuffSearchText = string.Empty;

        public override Vector2 InitialSize => new Vector2(860f, 700f);

        public Dialog_AttributeBuffEditor(CharacterStatModifierProfile profile, Pawn? targetPawn, Action? onChanged = null)
        {
            this.profile = profile ?? new CharacterStatModifierProfile();
            this.targetPawn = targetPawn;
            this.onChanged = onChanged;

            this.profile.entries ??= new List<CharacterStatModifierEntry>();

            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "CS_AttrBuff_Section".Translate());
            Text.Font = GameFont.Small;

            float y = 40f;
            float width = inRect.width;

            DrawToolbar(ref y, width);

            string newSearch = Widgets.TextEntryLabeled(new Rect(0f, y, width, 24f), "CS_AttrBuff_Search".Translate(), attributeBuffSearchText);
            if (!string.Equals(newSearch, attributeBuffSearchText, StringComparison.Ordinal))
            {
                attributeBuffSearchText = newSearch;
            }
            y += 34f;

            DrawEntryList(new Rect(0f, y, width, inRect.height - y - 42f));

            Rect closeRect = new Rect(inRect.width - 110f, inRect.height - 34f, 110f, 28f);
            if (UIHelper.DrawToolbarButton(closeRect, "CS_Studio_Btn_OK".Translate(), accent: true))
            {
                Close();
            }
        }

        private void DrawToolbar(ref float y, float width)
        {
            float gap = 8f;
            float buttonWidth = (width - gap * 2f) / 3f;
            Rect toolbarRect = new Rect(0f, y, width, 28f);
            if (UIHelper.DrawToolbarButton(new Rect(toolbarRect.x, toolbarRect.y, buttonWidth, 24f), "CS_AttrBuff_AddCommon".Translate()))
            {
                ShowCommonStatSelectionMenu();
            }
            if (UIHelper.DrawToolbarButton(new Rect(toolbarRect.x + buttonWidth + gap, toolbarRect.y, buttonWidth, 24f), "CS_AttrBuff_AddAny".Translate()))
            {
                ShowAnyStatSelectionMenu();
            }
            if (UIHelper.DrawToolbarButton(new Rect(toolbarRect.x + (buttonWidth + gap) * 2f, toolbarRect.y, buttonWidth, 24f), "CS_AttrBuff_ClearAll".Translate()))
            {
                profile.entries.Clear();
                NotifyChanged();
            }
            y += 30f;
        }

        private void DrawEntryList(Rect rect)
        {
            if (profile.entries.Count == 0)
            {
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "CS_AttrBuff_Empty".Translate());
                GUI.color = Color.white;
                return;
            }

            const float rowHeight = 78f;
            float listHeight = Mathf.Max(profile.entries.Count * rowHeight + 4f, rect.height - 4f);
            UIHelper.DrawContentCard(rect);

            Rect listView = new Rect(0f, 0f, rect.width - 16f, listHeight);
            Widgets.BeginScrollView(rect.ContractedBy(2f), ref attributeBuffScrollPos, listView);

            float rowY = 0f;
            for (int i = 0; i < profile.entries.Count; i++)
            {
                CharacterStatModifierEntry entry = profile.entries[i];
                if (entry == null)
                {
                    continue;
                }

                Rect rowRect = new Rect(0f, rowY, listView.width, rowHeight - 4f);
                UIHelper.DrawAlternatingRowBackground(rowRect, i);

                const float top = 6f;
                Rect enabledRect = new Rect(8f, rowY + top + 2f, 22f, 22f);
                bool previousEnabled = entry.enabled;
                Widgets.Checkbox(enabledRect.position, ref entry.enabled);
                if (entry.enabled != previousEnabled)
                {
                    NotifyChanged();
                }

                float statX = 36f;
                float statWidth = Mathf.Min(260f, listView.width * 0.36f);
                float modeX = statX + statWidth + 8f;
                float modeWidth = 96f;
                float deleteWidth = 72f;
                float valueWidth = 96f;
                float valueLabelWidth = 56f;
                float deleteX = listView.width - deleteWidth - 8f;
                float valueX = deleteX - 8f - valueWidth;
                float valueLabelX = valueX - valueLabelWidth - 4f;

                StatDef selectedStat = DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName);
                string statLabel = CharacterStatModifierCatalog.GetDisplayLabel(selectedStat);
                Rect statRect = new Rect(statX, rowY + top, Mathf.Max(120f, statWidth), 24f);
                if (UIHelper.DrawSelectionButton(statRect, statLabel))
                {
                    ShowStatSelectionMenu(entry);
                }

                Rect modeRect = new Rect(modeX, rowY + top, modeWidth, 24f);
                if (UIHelper.DrawSelectionButton(modeRect, CharacterStatModifierCatalog.GetModeLabel(entry.mode)))
                {
                    ToggleModifierMode(entry);
                }

                float beforeValue = entry.value;
                Widgets.Label(new Rect(valueLabelX, rowY + top, valueLabelWidth, 24f), "值");
                string buffer = entry.value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                float editedValue = entry.value;
                UIHelper.TextFieldNumeric(new Rect(valueX, rowY + top, valueWidth, 24f), ref editedValue, ref buffer, -100f, 100f, null, $"AttrBuffValue_{i}");
                entry.value = editedValue;
                if (Math.Abs(beforeValue - entry.value) > 0.0001f)
                {
                    NotifyChanged();
                }

                string explain = entry.mode == CharacterStatModifierMode.Offset
                    ? "CS_AttrBuff_Mode_Offset_Desc".Translate()
                    : "CS_AttrBuff_Mode_Factor_Desc".Translate();
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(statX, rowY + 38f, listView.width - statX - deleteWidth - 20f, 28f), explain + "  " + "CS_AttrBuff_CurrentValue".Translate(CharacterStatModifierCatalog.FormatValuePreview(entry.mode, entry.value)));
                GUI.color = Color.white;

                if (UIHelper.DrawDangerButton(new Rect(deleteX, rowY + top, deleteWidth, 24f), "CS_Studio_Btn_Delete".Translate()))
                {
                    profile.entries.RemoveAt(i);
                    NotifyChanged();
                    break;
                }

                rowY += rowHeight;
            }

            Widgets.EndScrollView();
        }

        private void ToggleModifierMode(CharacterStatModifierEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.mode = entry.mode == CharacterStatModifierMode.Offset
                ? CharacterStatModifierMode.Factor
                : CharacterStatModifierMode.Offset;
            NotifyChanged();
        }

        private void ShowCommonStatSelectionMenu()
        {
            var options = new List<FloatMenuOption>();
            foreach (string defName in CharacterStatModifierCatalog.CommonStatDefNames)
            {
                StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(defName);
                if (stat == null)
                {
                    continue;
                }

                StatDef captured = stat;
                options.Add(new FloatMenuOption(CharacterStatModifierCatalog.GetMenuLabel(captured), () => AddOrUpdateStatModifier(captured)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowAnyStatSelectionMenu()
        {
            ShowStatSelectionMenu(null);
        }

        private void ShowStatSelectionMenu(CharacterStatModifierEntry? existingEntry)
        {
            var options = new List<FloatMenuOption>();
            string search = (attributeBuffSearchText ?? string.Empty).Trim().ToLowerInvariant();
            foreach (StatDef stat in CharacterStatModifierCatalog.GetAvailableStatDefs())
            {
                if (!string.IsNullOrEmpty(search))
                {
                    string haystack = (CharacterStatModifierCatalog.GetDisplayLabel(stat) + " " + stat.defName + " " + CharacterStatModifierCatalog.GetCategoryLabel(stat))
                        .ToLowerInvariant();
                    if (!haystack.Contains(search))
                    {
                        continue;
                    }
                }

                StatDef captured = stat;
                options.Add(new FloatMenuOption(CharacterStatModifierCatalog.GetMenuLabel(captured), () =>
                {
                    if (existingEntry == null)
                    {
                        AddOrUpdateStatModifier(captured);
                    }
                    else
                    {
                        existingEntry.statDefName = captured.defName;
                        NotifyChanged();
                    }
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AddOrUpdateStatModifier(StatDef stat)
        {
            if (stat == null)
            {
                return;
            }

            CharacterStatModifierEntry existing = profile.entries.Find(e => e != null && string.Equals(e.statDefName, stat.defName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.enabled = true;
                NotifyChanged();
                return;
            }

            profile.entries.Add(new CharacterStatModifierEntry
            {
                statDefName = stat.defName,
                mode = CharacterStatModifierMode.Offset,
                value = 0f,
                enabled = true
            });
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            CharacterAttributeBuffService.SyncAttributeBuff(targetPawn);
            onChanged?.Invoke();
        }
    }
}
