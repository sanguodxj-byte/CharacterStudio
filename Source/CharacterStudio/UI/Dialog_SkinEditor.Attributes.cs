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
        // LLM 生成状态（异步）
        private bool llmCharacterGenerating = false;
        private LlmGeneratedCharacterDesign? llmCharacterPendingResult = null;
        private string? llmCharacterPendingError = null;

        private void DrawAttributesPanel(Rect rect)
        {
            Rect titleRect = UIHelper.DrawPanelShell(rect, "CS_Studio_Tab_Attributes".Translate(), Margin);

            workingSkin.attributes ??= new CharacterAttributeProfile();
            CharacterAttributeProfile attributes = workingSkin.attributes;
            CharacterStatModifierProfile statProfile = CharacterAttributeBuffService.GetOrCreateProfile(workingSkin);

            float contentY = titleRect.yMax + 8f;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            UIHelper.DrawContentCard(contentRect);

            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 780f);
            Widgets.BeginScrollView(contentRect.ContractedBy(2f), ref attributesScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            // ── 基础属性（合并称号、背景故事、年龄） ──
            Rect basicRect = UIHelper.DrawSectionCard(ref y, width, "CS_Attr_Section_Basic".Translate(), 280f);
            float basicY = basicRect.y;
            DrawTrackedPropertyField(ref basicY, basicRect.width, "CS_Attr_Title".Translate(), ref attributes.title);
            DrawTrackedMultilineField(ref basicY, basicRect.width, "CS_Attr_BackstorySummary".Translate(), ref attributes.backstorySummary, 80f);
            DrawTrackedNumericField(ref basicY, basicRect.width, "CS_Attr_BiologicalAge".Translate(), ref attributes.biologicalAge, 0f, 999f);
            DrawTrackedNumericField(ref basicY, basicRect.width, "CS_Attr_ChronologicalAge".Translate(), ref attributes.chronologicalAge, 0f, 9999f);

            // ── 属性增益 Buff ──
            Rect buffRect = UIHelper.DrawSectionCard(ref y, width, "CS_AttrBuff_Section".Translate(), 96f, accent: true);
            float buffY = buffRect.y;
            DrawAttributeBuffEntry(ref buffY, buffRect.width, statProfile);

            // ── 导出与角色定义 ──
            y += 4f;
            float halfBtnWidth = (width - 10f) / 2f;
            Rect exportBtnRect = new Rect(0f, y, halfBtnWidth, 28f);
            if (UIHelper.DrawToolbarButton(exportBtnRect, "CS_Studio_Attributes_ExportScattered".Translate(), accent: true))
            {
                ExportScatteredFromCurrentSkin();
            }
            Rect charDefBtnRect = new Rect(halfBtnWidth + 10f, y, halfBtnWidth, 28f);
            if (UIHelper.DrawToolbarButton(charDefBtnRect, "CS_Studio_CharacterDefinition_OpenButton".Translate()))
            {
                OpenCharacterDefinitionDialog();
            }
            y += 36f;

            // ── 辅助生成 ──
            Rect llmRect = UIHelper.DrawSectionCard(ref y, width, "CS_Studio_Attributes_Assistant".Translate(), 210f);
            float llmY = llmRect.y;
            UIHelper.DrawInfoBanner(ref llmY, llmRect.width, "CS_Studio_Attributes_AssistantHint".Translate());
            Widgets.Label(new Rect(0f, llmY, llmRect.width, 24f), "CS_LLM_CharacterPrompt".Translate());
            Rect promptRect = new Rect(0f, llmY + 26f, llmRect.width, 86f);
            Widgets.DrawBoxSolid(promptRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(promptRect, 1);
            GUI.color = Color.white;
            llmCharacterPrompt = Widgets.TextArea(promptRect.ContractedBy(4f), llmCharacterPrompt ?? string.Empty);
            llmY += 120f;

            float llmBtnWidth = (llmRect.width - 10f) / 2f;

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
            Rect generateRect = new Rect(0f, llmY, llmBtnWidth, 28f);
            if (UIHelper.DrawToolbarButton(generateRect, generateLabel, accent: !llmCharacterGenerating))
            {
                GenerateCharacterFromPrompt();
            }
            GUI.enabled = true;

            Rect settingsRect = new Rect(llmBtnWidth + 10f, llmY, llmBtnWidth, 28f);
            if (UIHelper.DrawToolbarButton(settingsRect, "CS_LLM_OpenSettings".Translate()))
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

        private void DrawAttributeBuffEntry(ref float y, float width, CharacterStatModifierProfile profile)
        {
            profile.entries ??= new List<CharacterStatModifierEntry>();

            int totalCount = profile.entries.Count;
            int enabledCount = 0;
            for (int i = 0; i < profile.entries.Count; i++)
            {
                if (profile.entries[i]?.enabled == true)
                {
                    enabledCount++;
                }
            }

            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(0f, y, width, 24f), totalCount == 0
                ? "CS_AttrBuff_Empty".Translate()
                : "CS_AttrBuff_Summary".Translate(totalCount, enabledCount));
            GUI.color = Color.white;
            y += 28f;

            Rect buttonRect = new Rect(0f, y, width, 28f);
            if (UIHelper.DrawToolbarButton(buttonRect, totalCount == 0 ? "CS_AttrBuff_OpenEditor".Translate() : "CS_AttrBuff_ManageBuff".Translate(), accent: true))
            {
                Find.WindowStack.Add(new Dialog_AttributeBuffEditor(
                    profile,
                    targetPawn,
                    () =>
                    {
                        isDirty = true;
                        CharacterAttributeBuffService.SyncAttributeBuff(targetPawn);
                    }));
            }
            y += 32f;
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

            MutateWithUndo(() =>
            {
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
            }, refreshPreview: true, refreshRenderTree: true);
        }

        private void OpenCharacterDefinitionDialog()
        {
            workingDocument.characterDefinition ??= new CharacterDefinition();
            workingDocument.characterDefinition.EnsureDefaults(
                workingSkin.defName ?? "CS_Character",
                ResolveSpawnRaceForCurrentDesign(BuildRuntimeSkinForExecution()),
                workingSkin.attributes);

            Find.WindowStack.Add(new Dialog_CharacterDefinition(workingDocument.characterDefinition, () =>
            {
                isDirty = true;
                RefreshPreview();
            }));
        }

        private void ExportScatteredFromCurrentSkin()
        {
            try
            {
                var runtimeSkin = BuildRuntimeSkinForExecution();
                if (runtimeSkin == null)
                {
                    ShowStatus("CS_Studio_Err_SaveFailed".Translate());
                    return;
                }

                workingDocument.characterDefinition ??= new CharacterDefinition();
                workingDocument.characterDefinition.EnsureDefaults(
                    runtimeSkin.defName ?? workingSkin.defName ?? "CS_Character",
                    ResolveSpawnRaceForCurrentDesign(runtimeSkin),
                    runtimeSkin.attributes);

                Exporter.ModBuilder.ExportScatteredLooseFiles(runtimeSkin, workingDocument.characterDefinition);
                ShowStatus("CS_Studio_Attributes_ExportScatteredSuccess".Translate());
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 散件导出失败: {ex}");
                ShowStatus("CS_Studio_Err_SaveFailed".Translate());
            }
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
