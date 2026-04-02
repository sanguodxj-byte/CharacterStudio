using System;
using System.Collections.Generic;
using CharacterStudio.AI;
using CharacterStudio.Attributes;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private Vector2 attributesScrollPos;
        private string llmCharacterPrompt = string.Empty;
        private Vector2 attributeBuffScrollPos;
        private string attributeBuffSearchText = string.Empty;
        // LLM 生成状态（异步）
        private bool llmCharacterGenerating = false;
        private LlmGeneratedCharacterDesign? llmCharacterPendingResult = null;
        private string? llmCharacterPendingError = null;

        private void DrawAttributesPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect titleRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, 26f);
            Widgets.DrawBoxSolid(titleRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(titleRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), "CS_Studio_Tab_Attributes".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            workingSkin.attributes ??= new CharacterAttributeProfile();
            CharacterAttributeProfile attributes = workingSkin.attributes;
            CharacterStatModifierProfile statProfile = CharacterAttributeBuffService.GetOrCreateProfile(workingSkin);

            float contentY = titleRect.yMax + 8f;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            Widgets.DrawBoxSolid(contentRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(contentRect, 1);
            GUI.color = Color.white;

            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 1360f);
            Widgets.BeginScrollView(contentRect.ContractedBy(2f), ref attributesScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            UIHelper.DrawSectionTitle(ref y, width, "CS_Attr_Section_Basic".Translate());
            DrawTrackedPropertyField(ref y, width, "CS_Attr_Title".Translate(), ref attributes.title);
            DrawTrackedMultilineField(ref y, width, "CS_Attr_BackstorySummary".Translate(), ref attributes.backstorySummary, 104f);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Attr_Section_Stats".Translate());
            DrawTrackedNumericField(ref y, width, "CS_Attr_BiologicalAge".Translate(), ref attributes.biologicalAge, 0f, 999f);
            DrawTrackedNumericField(ref y, width, "CS_Attr_ChronologicalAge".Translate(), ref attributes.chronologicalAge, 0f, 9999f);

            UIHelper.DrawSectionTitle(ref y, width, "CS_AttrBuff_Section".Translate());
            DrawAttributeBuffEditor(ref y, width, statProfile);

            Widgets.Label(new Rect(0f, y, width, 24f), "CS_LLM_CharacterPrompt".Translate());
            Rect promptRect = new Rect(0f, y + 26f, width, 110f);
            Widgets.DrawBoxSolid(promptRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(promptRect, 1);
            GUI.color = Color.white;
            llmCharacterPrompt = Widgets.TextArea(promptRect.ContractedBy(4f), llmCharacterPrompt ?? string.Empty);
            y += 144f;

            float buttonWidth = (width - 10f) / 2f;

            if (llmCharacterPendingResult != null)
            {
                ApplyGeneratedCharacter(llmCharacterPendingResult);
                llmCharacterPendingResult = null;
                llmCharacterGenerating = false;
                ShowStatus("CS_LLM_GenerateCharacterSuccess".Translate());
                isDirty = true;
                RefreshPreview();
            }
            if (llmCharacterPendingError != null)
            {
                ShowStatus("CS_LLM_GenerateFailed".Translate(llmCharacterPendingError));
                llmCharacterPendingError = null;
                llmCharacterGenerating = false;
            }

            GUI.enabled = !llmCharacterGenerating;
            string generateLabel = llmCharacterGenerating ? "CS_LLM_Generating".Translate() : "CS_LLM_GenerateCharacter".Translate();
            Rect generateRect = new Rect(0f, y, buttonWidth, 28f);
            Widgets.DrawBoxSolid(generateRect, llmCharacterGenerating ? UIHelper.PanelFillSoftColor : UIHelper.ActiveTabColor);
            Widgets.DrawBoxSolid(new Rect(generateRect.x, generateRect.yMax - 2f, generateRect.width, 2f), llmCharacterGenerating ? UIHelper.AccentSoftColor : UIHelper.AccentColor);
            GUI.color = Mouse.IsOver(generateRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(generateRect, 1);
            GUI.color = llmCharacterGenerating ? UIHelper.SubtleColor : Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(generateRect, generateLabel);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(generateRect))
            {
                GenerateCharacterFromPrompt();
            }
            GUI.enabled = true;

            Rect settingsRect = new Rect(buttonWidth + 10f, y, buttonWidth, 28f);
            Widgets.DrawBoxSolid(settingsRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(settingsRect.x, settingsRect.yMax - 2f, settingsRect.width, 2f), new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(settingsRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(settingsRect, 1);
            GUI.color = UIHelper.HeaderColor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(settingsRect, "CS_LLM_OpenSettings".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(settingsRect))
            {
                OnOpenLlmSettings();
            }

            Widgets.EndScrollView();
        }

        private void DrawTrackedPropertyField(ref float y, float width, string label, ref string value)
        {
            string before = value ?? string.Empty;
            string current = value ?? string.Empty;
            UIHelper.DrawPropertyField(ref y, width, label, ref current);
            value = current;
            if (!string.Equals(before, current, StringComparison.Ordinal))
            {
                isDirty = true;
            }
        }

        private void DrawTrackedMultilineField(ref float y, float width, string label, ref string value, float height)
        {
            Widgets.Label(new Rect(0f, y, width, 24f), label);
            Rect textRect = new Rect(0f, y + 24f, width, height);
            Widgets.DrawBoxSolid(textRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(textRect, 1);
            GUI.color = Color.white;

            string before = value ?? string.Empty;
            string after = Widgets.TextArea(textRect.ContractedBy(4f), before);
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                value = after;
                isDirty = true;
            }

            y += height + 32f;
        }

        private void DrawTrackedNumericField(ref float y, float width, string label, ref float value, float min, float max)
        {
            float before = value;
            UIHelper.DrawNumericField(ref y, width, label, ref value, min, max);
            if (Math.Abs(before - value) > 0.0001f)
            {
                isDirty = true;
            }
        }

        private void DrawAttributeBuffEditor(ref float y, float width, CharacterStatModifierProfile profile)
        {
            profile.entries ??= new List<CharacterStatModifierEntry>();

            float gap = 8f;
            float buttonWidth = (width - gap * 2f) / 3f;
            Rect toolbarRect = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(new Rect(toolbarRect.x, toolbarRect.y, buttonWidth, 24f), "CS_AttrBuff_AddCommon".Translate()))
            {
                ShowCommonStatSelectionMenu(profile);
            }
            if (Widgets.ButtonText(new Rect(toolbarRect.x + buttonWidth + gap, toolbarRect.y, buttonWidth, 24f), "CS_AttrBuff_AddAny".Translate()))
            {
                ShowAnyStatSelectionMenu(profile);
            }
            if (Widgets.ButtonText(new Rect(toolbarRect.x + (buttonWidth + gap) * 2f, toolbarRect.y, buttonWidth, 24f), "CS_AttrBuff_ClearAll".Translate()))
            {
                profile.entries.Clear();
                isDirty = true;
            }
            y += 30f;

            string newSearch = Widgets.TextEntryLabeled(new Rect(0f, y, width, 24f), "CS_AttrBuff_Search".Translate(), attributeBuffSearchText);
            if (!string.Equals(newSearch, attributeBuffSearchText, StringComparison.Ordinal))
            {
                attributeBuffSearchText = newSearch;
            }
            y += 30f;

            if (profile.entries.Count == 0)
            {
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(0f, y, width, 24f), "CS_AttrBuff_Empty".Translate());
                GUI.color = Color.white;
                y += 28f;
                return;
            }

            float listHeight = Mathf.Clamp(profile.entries.Count * 66f + 6f, 120f, 360f);
            Rect outerRect = new Rect(0f, y, width, listHeight);
            Widgets.DrawBoxSolid(outerRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(outerRect, 1);
            GUI.color = Color.white;

            Rect listView = new Rect(0f, 0f, outerRect.width - 16f, profile.entries.Count * 66f + 4f);
            Widgets.BeginScrollView(outerRect.ContractedBy(2f), ref attributeBuffScrollPos, listView);

            float rowY = 0f;
            for (int i = 0; i < profile.entries.Count; i++)
            {
                CharacterStatModifierEntry entry = profile.entries[i];
                if (entry == null)
                {
                    continue;
                }

                Rect rowRect = new Rect(0f, rowY, listView.width, 62f);
                Widgets.DrawBoxSolid(rowRect, i % 2 == 0 ? UIHelper.PanelFillSoftColor : UIHelper.PanelFillColor);

                Rect enabledRect = new Rect(4f, rowY + 4f, 22f, 22f);
                bool previousEnabled = entry.enabled;
                Widgets.Checkbox(enabledRect.position, ref entry.enabled);
                if (entry.enabled != previousEnabled)
                {
                    isDirty = true;
                }

                StatDef selectedStat = DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName);
                string statLabel = CharacterStatModifierCatalog.GetDisplayLabel(selectedStat);
                if (Widgets.ButtonText(new Rect(30f, rowY + 4f, 220f, 24f), statLabel))
                {
                    ShowStatSelectionMenu(profile, entry, includeAllUseful: true);
                }

                if (Widgets.ButtonText(new Rect(258f, rowY + 4f, 88f, 24f), CharacterStatModifierCatalog.GetModeLabel(entry.mode)))
                {
                    ToggleModifierMode(entry);
                }

                float beforeValue = entry.value;
                UIHelper.DrawNumericField(ref rowY, listView.width - 120f, "CS_AttrBuff_Value".Translate(), ref entry.value, -100f, 100f, 80f);
                rowY -= UIHelper.RowHeight;
                if (Math.Abs(beforeValue - entry.value) > 0.0001f)
                {
                    isDirty = true;
                }

                string explain = entry.mode == CharacterStatModifierMode.Offset
                    ? "CS_AttrBuff_Mode_Offset_Desc".Translate()
                    : "CS_AttrBuff_Mode_Factor_Desc".Translate();
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(30f, rowY + 32f, 360f, 24f), explain + "  " + "CS_AttrBuff_CurrentValue".Translate(CharacterStatModifierCatalog.FormatValuePreview(entry.mode, entry.value)));
                GUI.color = Color.white;

                if (Widgets.ButtonText(new Rect(listView.width - 74f, rowY + 4f, 70f, 24f), "CS_Studio_Btn_Delete".Translate()))
                {
                    profile.entries.RemoveAt(i);
                    isDirty = true;
                    CharacterAttributeBuffService.SyncAttributeBuff(targetPawn);
                    break;
                }

                rowY += 66f;
            }

            Widgets.EndScrollView();
            y += listHeight + 8f;

            CharacterAttributeBuffService.SyncAttributeBuff(targetPawn);
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
            isDirty = true;
        }

        private void ShowCommonStatSelectionMenu(CharacterStatModifierProfile profile)
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
                options.Add(new FloatMenuOption(CharacterStatModifierCatalog.GetMenuLabel(captured), () => AddOrUpdateStatModifier(profile, captured)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowAnyStatSelectionMenu(CharacterStatModifierProfile profile)
        {
            ShowStatSelectionMenu(profile, null!, includeAllUseful: true);
        }

        private void ShowStatSelectionMenu(CharacterStatModifierProfile profile, CharacterStatModifierEntry existingEntry, bool includeAllUseful)
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
                        AddOrUpdateStatModifier(profile, captured);
                    }
                    else
                    {
                        existingEntry.statDefName = captured.defName;
                        isDirty = true;
                    }
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AddOrUpdateStatModifier(CharacterStatModifierProfile profile, StatDef stat)
        {
            if (profile == null || stat == null)
            {
                return;
            }

            CharacterStatModifierEntry existing = profile.entries.Find(e => e != null && string.Equals(e.statDefName, stat.defName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.enabled = true;
                isDirty = true;
                return;
            }

            profile.entries.Add(new CharacterStatModifierEntry
            {
                statDefName = stat.defName,
                mode = CharacterStatModifierMode.Offset,
                value = 0f,
                enabled = true
            });
            isDirty = true;
        }

        private void GenerateCharacterFromPrompt()
        {
            if (llmCharacterGenerating) return;

            var settings = LlmSettingsRepository.GetOrLoad();
            if (!settings.IsAvailable)
            {
                ShowStatus("CS_LLM_Settings_NotConfigured".Translate());
                Find.WindowStack.Add(new Dialog_LlmSettings());
                return;
            }

            if (string.IsNullOrWhiteSpace(llmCharacterPrompt))
            {
                ShowStatus("CS_LLM_CharacterPrompt_Empty".Translate());
                return;
            }

            SyncAbilitiesToSkin();
            llmCharacterGenerating = true;
            ShowStatus("CS_LLM_Generating".Translate());

            var skinSnapshot = workingSkin.Clone();
            var abilitiesSnapshot = new System.Collections.Generic.List<Abilities.ModularAbilityDef>(workingAbilities);

            LlmGenerationService.GenerateCharacterDesignAsync(
                settings,
                llmCharacterPrompt,
                skinSnapshot,
                abilitiesSnapshot,
                result  => { llmCharacterPendingResult = result.payload; },
                errMsg  => { llmCharacterPendingError  = errMsg; }
            );
        }

        private void ApplyGeneratedCharacter(LlmGeneratedCharacterDesign? design)
        {
            if (design == null)
            {
                return;
            }

            workingSkin.defName = string.IsNullOrWhiteSpace(design.suggestedDefName) ? workingSkin.defName : design.suggestedDefName;
            workingSkin.label = string.IsNullOrWhiteSpace(design.suggestedLabel) ? workingSkin.label : design.suggestedLabel;
            workingSkin.description = string.IsNullOrWhiteSpace(design.suggestedDescription) ? workingSkin.description : design.suggestedDescription;

            workingSkin.attributes ??= new CharacterAttributeProfile();
            MergeGeneratedCharacterAttributes(workingSkin.attributes, design.attributes);

            workingAbilities.Clear();
            if (design.abilities != null)
            {
                foreach (var ability in design.abilities)
                {
                    if (ability != null)
                    {
                        workingAbilities.Add(ability);
                    }
                }
            }

            SyncAbilitiesToSkin();
        }

        private void MergeGeneratedCharacterAttributes(CharacterAttributeProfile current, CharacterAttributeProfile? incoming)
        {
            if (current == null || incoming == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(incoming.title))
            {
                current.title = incoming.title;
            }

            if (!string.IsNullOrWhiteSpace(incoming.backstorySummary))
            {
                current.backstorySummary = incoming.backstorySummary;
            }

            if (incoming.biologicalAge > 0f)
            {
                current.biologicalAge = incoming.biologicalAge;
            }

            if (incoming.chronologicalAge > 0f)
            {
                current.chronologicalAge = incoming.chronologicalAge;
            }
        }
    }
}
