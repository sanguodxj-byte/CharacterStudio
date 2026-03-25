using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.AI;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private void DrawAbilitySidebar(Rect rect)
        {
            DrawAbilityPanelShell(rect, leftSidebarCollapsed ? "CS_Studio_Ability_LeftPanelTitle".Translate() : "CS_Studio_Ability_LeftPanelTitle".Translate(), out Rect bodyRect);

            Rect toggleRect = new Rect(rect.xMax - 28f, rect.y + 3f, 24f, 20f);
            if (DrawCompactIconButton(toggleRect, leftSidebarCollapsed ? "▶" : "◀", () => leftSidebarCollapsed = !leftSidebarCollapsed, accent: true))
            {
                return;
            }

            if (leftSidebarCollapsed)
            {
                float compactY = bodyRect.y;
                float compactW = bodyRect.width;
                if (DrawToolbarButton(new Rect(bodyRect.x, compactY, compactW, 24f), "+", CreateNewAbility, true))
                {
                }
                compactY += 30f;
                if (DrawToolbarButton(new Rect(bodyRect.x, compactY, compactW, 24f), "⇅", DuplicateSelectedAbility))
                {
                }
                compactY += 30f;
                if (DrawToolbarButton(new Rect(bodyRect.x, compactY, compactW, 24f), "💾", () => PersistAbilityEditorState(true), true))
                {
                }
                return;
            }

            Rect viewRect = new Rect(0f, 0f, Mathf.Max(0f, bodyRect.width - 16f), 440f);
            Widgets.BeginScrollView(bodyRect, ref leftSidebarScrollPos, viewRect);

            Rect inner = new Rect(0f, 0f, viewRect.width, viewRect.height);
            float tripleButtonWidth = (inner.width - 20f) / 3f;
            float tripleButtonGap = 10f;
            float buttonWidth = (inner.width - 10f) / 2f;

            if (DrawToolbarButton(new Rect(inner.x, inner.y, tripleButtonWidth, 24f), "CS_Studio_File_New".Translate(), CreateNewAbility, true))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + tripleButtonWidth + tripleButtonGap, inner.y, tripleButtonWidth, 24f), "CS_Studio_Panel_Duplicate".Translate(), DuplicateSelectedAbility))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + (tripleButtonWidth + tripleButtonGap) * 2f, inner.y, tripleButtonWidth, 24f), "CS_Studio_Btn_Delete".Translate(), () =>
            {
                if (selectedAbility != null)
                {
                    abilities.Remove(selectedAbility);
                    if (abilities.Count > 0) selectedAbility = abilities[0];
                    else selectedAbility = null;
                    NotifyAbilityPreviewDirty(true);
                }
            }))
            {
            }

            float secondRowY = inner.y + 30f;
            if (DrawToolbarButton(new Rect(inner.x, secondRowY, tripleButtonWidth, 24f), "CS_Studio_Ability_LoadExamples".Translate(), LoadExampleAbilities, true))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + tripleButtonWidth + tripleButtonGap, secondRowY, tripleButtonWidth, 24f), "CS_Studio_Ability_ImportXml".Translate(), OpenImportXmlDialog))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + (tripleButtonWidth + tripleButtonGap) * 2f, secondRowY, tripleButtonWidth, 24f), "CS_Studio_File_Export".Translate(), ExportAbilitiesToDefaultPath))
            {
            }

            float saveRowY = secondRowY + 30f;
            if (DrawToolbarButton(new Rect(inner.x, saveRowY, inner.width, 24f), "CS_Studio_Ability_Save".Translate(), () => PersistAbilityEditorState(true), true))
            {
            }

            float settingsRowY = saveRowY + 34f;
            DrawAbilityInfoBanner(new Rect(inner.x, settingsRowY, inner.width, 28f), "CS_Studio_Ability_LlmBanner".Translate());

            float settingsButtonY = settingsRowY + 32f;
            if (DrawToolbarButton(new Rect(inner.x, settingsButtonY, inner.width, 24f), "CS_LLM_OpenSettings".Translate(), () => Find.WindowStack.Add(new Dialog_LlmSettings())))
            {
            }

            float thirdRowY = settingsButtonY + 30f;
            GUI.enabled = !llmAbilitiesGenerating;
            string genLabel = llmAbilitiesGenerating ? "CS_LLM_Generating".Translate().ToString() : "CS_LLM_GenerateAbilities".Translate().ToString();
            if (DrawToolbarButton(new Rect(inner.x, thirdRowY, inner.width, 24f), genLabel, () => GenerateAbilitiesFromPrompt(false), true))
            {
            }
            GUI.enabled = true;

            float promptY = thirdRowY + 30f;
            DrawAbilityInfoBanner(new Rect(inner.x, promptY, inner.width, 26f), "CS_LLM_AbilityPrompt".Translate(), true);
            Rect promptRect = new Rect(inner.x, promptY + 30f, inner.width, 82f);
            Widgets.DrawBoxSolid(promptRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(promptRect, 1);
            GUI.color = Color.white;
            llmAbilityPrompt = Widgets.TextArea(promptRect.ContractedBy(6f), llmAbilityPrompt ?? string.Empty);

            float promptButtonsY = promptY + 118f;
            if (DrawToolbarButton(new Rect(inner.x, promptButtonsY, buttonWidth, 24f), "CS_LLM_ApplyReplace".Translate(), () => GenerateAbilitiesFromPrompt(true)))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + buttonWidth + 10f, promptButtonsY, buttonWidth, 24f), "CS_LLM_ApplyAppend".Translate(), () => GenerateAbilitiesFromPrompt(false), true))
            {
            }

            Widgets.EndScrollView();
        }

        private void DrawAbilityListPanel(Rect rect)
        {
            DrawAbilityPanelShell(rect, "CS_Studio_Ability_ListTitle".Translate(), out Rect bodyRect);

            Rect inner = bodyRect;
            DrawAbilityInfoBanner(new Rect(inner.x, inner.y, inner.width, 26f), "CS_Studio_Ability_Search".Translate(), true);
            abilitySearchText = Widgets.TextField(new Rect(inner.x, inner.y + 30f, inner.width, 24f), abilitySearchText ?? string.Empty);

            List<ModularAbilityDef> filteredAbilities = GetFilteredAbilities();
            DrawAbilityInfoBanner(new Rect(inner.x, inner.y + 58f, inner.width, 24f), "CS_Studio_Ability_CountSummary".Translate(filteredAbilities.Count, abilities.Count));

            float listY = inner.y + 88f;
            float listHeight = rect.height - (listY - rect.y) - Margin;
            Rect listRect = new Rect(inner.x, listY, inner.width, listHeight);
            Widgets.DrawBoxSolid(listRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(listRect, 1);
            GUI.color = Color.white;

            Rect viewRect = new Rect(0, 0, listRect.width - 16f, Mathf.Max(filteredAbilities.Count * 44f, listRect.height - 4f));
            Widgets.BeginScrollView(listRect, ref listScrollPos, viewRect);
            for (int i = 0; i < filteredAbilities.Count; i++)
            {
                var ability = filteredAbilities[i];
                Rect rowRect = new Rect(0, i * 44f, viewRect.width, 40f);
                DrawAbilityRow(rowRect, ability, i);
            }

            if (filteredAbilities.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0, 8f, viewRect.width, 40f), "CS_Studio_Ability_NoResults".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Widgets.EndScrollView();
        }

        private void DuplicateSelectedAbility()
        {
            if (selectedAbility == null) return;

            var copy = selectedAbility.Clone();
            copy.defName = GetUniqueAbilityDefName($"{selectedAbility.defName}_Copy");
            copy.label = "CS_Studio_Ability_CopyLabel".Translate(selectedAbility.label ?? "CS_Studio_Ability_DefaultName".Translate());
            abilities.Add(copy);
            selectedAbility = copy;
            NotifyAbilityPreviewDirty(true);
        }

        private void LoadExampleAbilities()
        {
            var exampleAbilities = LoadExampleAbilitiesFromDefs();
            if (exampleAbilities.Count == 0)
            {
                validationSummary = "CS_Studio_Ability_ExamplesNotFound".Translate();
                Log.Warning("[CharacterStudio] 示例技能未在 DefDatabase<ModularAbilityDef> 中找到。");
                return;
            }

            var exampleDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ability in exampleAbilities)
            {
                if (!string.IsNullOrEmpty(ability.defName))
                {
                    exampleDefNames.Add(ability.defName);
                }
            }

            abilities.RemoveAll(a => a != null && !string.IsNullOrEmpty(a.defName) && exampleDefNames.Contains(a.defName));
            abilities.AddRange(exampleAbilities);
            selectedAbility = exampleAbilities.Count > 0 ? exampleAbilities[0] : abilities.Count > 0 ? abilities[0] : null;
            NotifyAbilityPreviewDirty(true);

            SkinAbilityHotkeyConfig hotkeyConfig = GetEditableHotkeyConfig();
            hotkeyConfig.enabled = true;
            hotkeyConfig.qAbilityDefName = "CS_Example_Q1_ModeSlash";
            hotkeyConfig.wAbilityDefName = "CS_Example_W_Pierce";
            hotkeyConfig.eAbilityDefName = "CS_Example_E_ShadowStep";
            hotkeyConfig.rAbilityDefName = "CS_Example_R_Annihilation";
            hotkeyConfig.wComboAbilityDefName = "CS_Example_W2_ComboBurst";
            SanitizeHotkeyConfigAgainstAbilities(GetCurrentHotkeyConfig());

            validationSummary = "CS_Studio_Ability_ExamplesLoaded".Translate();
        }

        private static List<ModularAbilityDef> LoadExampleAbilitiesFromDefs()
        {
            string[] exampleDefNames =
            {
                "CS_Example_Q1_ModeSlash",
                "CS_Example_Q2_ModeSlash",
                "CS_Example_Q3_ModeSlash",
                "CS_Example_Q4_ModeSlash",
                "CS_Example_W_Pierce",
                "CS_Example_W2_ComboBurst",
                "CS_Example_E_ShadowStep",
                "CS_Example_R_Annihilation"
            };

            var result = new List<ModularAbilityDef>();
            foreach (string defName in exampleDefNames)
            {
                ModularAbilityDef? def = DefDatabase<ModularAbilityDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    Log.Warning($"[CharacterStudio] 未找到示例技能 Def: {defName}");
                    continue;
                }

                result.Add(CreateEditableAbilityCopy(def));
            }

            return result;
        }

        private static ModularAbilityDef CreateEditableAbilityCopy(ModularAbilityDef source)
        {
            var copy = new ModularAbilityDef
            {
                defName = source.defName,
                label = source.label,
                description = source.description,
                iconPath = source.iconPath,
                cooldownTicks = source.cooldownTicks,
                warmupTicks = source.warmupTicks,
                charges = source.charges,
                aiCanUse = source.aiCanUse,
                carrierType = source.carrierType,
                targetType = source.targetType,
                useRadius = source.useRadius,
                areaCenter = source.areaCenter,
                range = source.range,
                radius = source.radius,
                projectileDef = source.projectileDef
            };

            if (source.effects != null)
            {
                foreach (var effect in source.effects)
                {
                    if (effect != null)
                    {
                        copy.effects.Add(effect.Clone());
                    }
                }
            }

            if (source.visualEffects != null)
            {
                foreach (var vfx in source.visualEffects)
                {
                    if (vfx != null)
                    {
                        copy.visualEffects.Add(vfx.Clone());
                    }
                }
            }

            if (source.runtimeComponents != null)
            {
                foreach (var component in source.runtimeComponents)
                {
                    if (component != null)
                    {
                        copy.runtimeComponents.Add(component.Clone());
                    }
                }
            }

            return copy;
        }

        private void CreateNewAbility()
        {
            var newAbility = new ModularAbilityDef
            {
                defName = GetUniqueAbilityDefName("CS_Ability"),
                label = "CS_Studio_Ability_DefaultName".Translate(),
                carrierType = AbilityCarrierType.Target,
                targetType = AbilityTargetType.Entity,
                areaCenter = AbilityAreaCenter.Target
            };
            abilities.Add(newAbility);
            selectedAbility = newAbility;
            validationSummary = string.Empty;
            NotifyAbilityPreviewDirty(true);
        }

        private void DrawAbilityRow(Rect rowRect, ModularAbilityDef ability, int index)
        {
            UIHelper.DrawAlternatingRowBackground(rowRect, index);

            if (selectedAbility == ability)
            {
                Widgets.DrawHighlightSelected(rowRect);
            }

            if (Widgets.ButtonInvisible(rowRect))
            {
                if (selectedAbility != ability)
                {
                    selectedAbility = ability;
                    NotifyAbilityPreviewDirty(true);
                }
            }

            var validation = ability.Validate();
            string statusIcon = validation.IsValid
                ? (validation.Warnings.Count > 0 ? "⚠" : "✅")
                : "❌";
            string displayName = string.IsNullOrWhiteSpace(ability.label) ? ability.defName : ability.label;

            string sublineLeft = $"{GetCarrierTypeLabel(ability.carrierType)} / {GetTargetTypeLabel(ModularAbilityDefExtensions.NormalizeTargetType(ability))}  CD:{ability.cooldownTicks:0}t";
            string sublineRight = GetValidationLabel(validation);

            Widgets.Label(new Rect(rowRect.x + 6f, rowRect.y + 2f, rowRect.width - 12f, 20f), $"{statusIcon} {displayName}");
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rowRect.x + 20f, rowRect.y + 20f, rowRect.width * 0.6f - 20f, 16f), sublineLeft);
            GUI.color = validation.IsValid ? (validation.Warnings.Count > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.4f, 1f, 0.5f)) : new Color(1f, 0.35f, 0.35f);
            Widgets.Label(new Rect(rowRect.x + rowRect.width * 0.6f, rowRect.y + 20f, rowRect.width * 0.4f, 16f), sublineRight);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private List<ModularAbilityDef> GetFilteredAbilities()
        {
            if (abilities == null || abilities.Count == 0)
            {
                return new List<ModularAbilityDef>();
            }

            string search = (abilitySearchText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(search))
            {
                return abilities;
            }

            return abilities.Where(a => a != null &&
                ((a.label?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                 (a.defName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                 GetCarrierTypeLabel(a.carrierType).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 GetTargetTypeLabel(ModularAbilityDefExtensions.NormalizeTargetType(a)).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }

        private void GenerateAbilitiesFromPrompt(bool replaceExisting)
        {
            try
            {
                var settings = LlmSettingsRepository.GetOrLoad();
                if (!settings.enabled || !settings.IsConfigured)
                {
                    validationSummary = "CS_LLM_Settings_NotConfigured".Translate();
                    Find.WindowStack.Add(new Dialog_LlmSettings());
                    return;
                }

                if (string.IsNullOrWhiteSpace(llmAbilityPrompt))
                {
                    validationSummary = "CS_LLM_AbilityPrompt_Empty".Translate();
                    return;
                }

                PawnSkinDef skinContext = boundSkin ?? new PawnSkinDef();
                var result = LlmGenerationService.GenerateAbilities(settings, llmAbilityPrompt, skinContext, abilities);
                List<ModularAbilityDef> generated = result.payload ?? new List<ModularAbilityDef>();
                generated = generated.Where(a => a != null).ToList();
                if (generated.Count == 0)
                {
                    validationSummary = "CS_LLM_GenerateAbilitiesEmpty".Translate();
                    return;
                }

                NormalizeImportedAbilityDefNames(generated, replaceExisting ? null : abilities);

                if (replaceExisting)
                {
                    abilities.Clear();
                }

                int beforeCount = abilities.Count;
                abilities.AddRange(generated);
                selectedAbility = generated[0];
                validationSummary = replaceExisting
                    ? "CS_LLM_GenerateAbilitiesReplaced".Translate(generated.Count)
                    : "CS_LLM_GenerateAbilitiesAppended".Translate(generated.Count, beforeCount, abilities.Count);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 技能 LLM 生成失败: {ex}");
                validationSummary = "CS_LLM_GenerateFailed".Translate(ex.Message);
            }
        }

        private string GetUniqueAbilityDefName(string desiredBase)
        {
            string baseName = string.IsNullOrWhiteSpace(desiredBase) ? "CS_Ability" : desiredBase.Trim();
            string candidate = baseName;
            int suffix = 1;

            while (abilities.Any(a => a != null && string.Equals(a.defName, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"{baseName}_{suffix++}";
            }

            return candidate;
        }

        private static void NormalizeImportedAbilityDefNames(IEnumerable<ModularAbilityDef> incoming, IEnumerable<ModularAbilityDef>? existing)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existing != null)
            {
                foreach (var ability in existing)
                {
                    if (ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                    {
                        used.Add(ability.defName);
                    }
                }
            }

            foreach (var ability in incoming)
            {
                if (ability == null) continue;
                string baseName = string.IsNullOrWhiteSpace(ability.defName) ? "CS_ImportedAbility" : ability.defName.Trim();
                string candidate = baseName;
                int suffix = 1;
                while (!used.Add(candidate))
                {
                    candidate = $"{baseName}_{suffix++}";
                }
                ability.defName = candidate;
            }
        }
    }
}