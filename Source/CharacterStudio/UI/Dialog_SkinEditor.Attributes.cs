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
            CharacterStatModifierProfile statProfile = CharacterAttributeBuffService.GetOrCreateProfile(workingSkin);

            float contentY = titleRect.yMax + 8f;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            UIHelper.DrawContentCard(contentRect);

            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 580f);
            Widgets.BeginScrollView(contentRect.ContractedBy(2f), ref attributesScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            // ── 导出为 XML ──
            Rect exportBtnRect = new Rect(0f, y, width, 32f);
            if (UIHelper.DrawToolbarButton(exportBtnRect, "CS_Studio_Attributes_ExportScattered".Translate(), accent: true))
            {
                ExportScatteredFromCurrentSkin();
            }
            y += 42f;

            // ── 基础属性弹窗入口 ──
            Rect basicCardRect = UIHelper.DrawSectionCard(ref y, width, "CS_Attr_Section_Basic".Translate(), 82f);
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            string basicTitle = !string.IsNullOrWhiteSpace(workingSkin.attributes.title) ? workingSkin.attributes.title : "CS_Studio_None".Translate().ToString();
            string basicSummary = "CS_Attr_BasicSummary".Translate(basicTitle, workingSkin.attributes.biologicalAge.ToString("F0"));
            Widgets.Label(basicCardRect, basicSummary);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (UIHelper.DrawToolbarButton(new Rect(basicCardRect.x, basicCardRect.yMax - 26f, basicCardRect.width, 26f), "CS_Attr_ConfigureBasic".Translate()))
            {
                OpenCharacterDefinitionDialog();
            }

            // ── 属性增益弹窗入口 ──
            int totalBuffs = statProfile.entries != null ? statProfile.entries.Count : 0;
            int enabledBuffs = statProfile.entries != null ? statProfile.entries.Count(e => e.enabled) : 0;
            Rect buffCardRect = UIHelper.DrawSectionCard(ref y, width, "CS_AttrBuff_Section".Translate(), 82f, accent: true);
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            string buffSummary = "CS_AttrBuff_Summary".Translate(totalBuffs, enabledBuffs);
            Widgets.Label(buffCardRect, buffSummary);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (UIHelper.DrawToolbarButton(new Rect(buffCardRect.x, buffCardRect.yMax - 26f, buffCardRect.width, 26f), "CS_AttrBuff_ManageBuff".Translate(), accent: true))
            {
                Find.WindowStack.Add(new Dialog_AttributeBuffEditor(
                    statProfile,
                    targetPawn,
                    () =>
                    {
                        isDirty = true;
                        CharacterAttributeBuffService.SyncAttributeBuff(targetPawn);
                    }));
            }

            // ── 辅助生成 (LLM) ──
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
