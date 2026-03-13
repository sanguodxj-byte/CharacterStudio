using System;
using System.Collections.Generic;
using CharacterStudio.AI;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private Vector2 attributesScrollPos;
        private string llmCharacterPrompt = string.Empty;
        private string attributesTagsCsv = string.Empty;
        private string attributesTraitsCsv = string.Empty;
        private string attributesApparelCsv = string.Empty;

        private void DrawAttributesPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect contentRect = rect.ContractedBy(Margin);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, 980f);
            Widgets.BeginScrollView(contentRect, ref attributesScrollPos, viewRect);

            workingSkin.attributes ??= new CharacterAttributeProfile();
            CharacterAttributeProfile attributes = workingSkin.attributes;

            float y = 0f;
            float width = viewRect.width;

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Tab_Attributes".Translate());
            UIHelper.DrawPropertyField(ref y, width, "CS_Attr_Title".Translate(), ref attributes.title);
            UIHelper.DrawPropertyField(ref y, width, "CS_Attr_FactionRole".Translate(), ref attributes.factionRole);
            UIHelper.DrawPropertyField(ref y, width, "CS_Attr_CombatRole".Translate(), ref attributes.combatRole);
            UIHelper.DrawPropertyField(ref y, width, "CS_Attr_Personality".Translate(), ref attributes.personality);
            UIHelper.DrawPropertyField(ref y, width, "CS_Attr_BodyType".Translate(), ref attributes.bodyTypeDefName);
            UIHelper.DrawPropertyField(ref y, width, "CS_Attr_HeadType".Translate(), ref attributes.headTypeDefName);
            UIHelper.DrawPropertyField(ref y, width, "CS_Attr_HairDef".Translate(), ref attributes.hairDefName);
            UIHelper.DrawPropertyField(ref y, width, "CS_Attr_FavoriteColor".Translate(), ref attributes.favoriteColorHex);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_BiologicalAge".Translate(), ref attributes.biologicalAge, 0f, 999f);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_ChronologicalAge".Translate(), ref attributes.chronologicalAge, 0f, 9999f);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_MoveSpeed".Translate(), ref attributes.moveSpeedMultiplier, 0f, 10f);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_MeleePower".Translate(), ref attributes.meleePower, 0f, 10f);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_ShootingAccuracy".Translate(), ref attributes.shootingAccuracy, 0f, 10f);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_ArmorRating".Translate(), ref attributes.armorRating, 0f, 200f);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_PsychicSensitivity".Translate(), ref attributes.psychicSensitivity, 0f, 10f);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_MarketValue".Translate(), ref attributes.marketValue, 0f, 100000f);

            EnsureAttributeCsvBuffers(attributes);
            DrawStringListEditor(ref y, width, "CS_Attr_Tags".Translate(), attributes.tags, ref attributesTagsCsv);
            DrawStringListEditor(ref y, width, "CS_Attr_KeyTraits".Translate(), attributes.keyTraits, ref attributesTraitsCsv);
            DrawStringListEditor(ref y, width, "CS_Attr_StartingApparel".Translate(), attributes.startingApparelDefs, ref attributesApparelCsv);

            UIHelper.DrawSectionTitle(ref y, width, "CS_LLM_CharacterSection".Translate());
            Widgets.Label(new Rect(0f, y, width, 24f), "CS_LLM_CharacterPrompt".Translate());
            llmCharacterPrompt = Widgets.TextArea(new Rect(0f, y + 26f, width, 110f), llmCharacterPrompt ?? string.Empty);
            y += 140f;
            Widgets.Label(new Rect(0f, y, width, 40f), "CS_LLM_EditorTool_CharacterHint".Translate());
            y += 44f;

            float buttonWidth = (width - 10f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, y, buttonWidth, 28f), "CS_LLM_GenerateCharacter".Translate()))
            {
                GenerateCharacterFromPrompt();
            }

            if (Widgets.ButtonText(new Rect(buttonWidth + 10f, y, buttonWidth, 28f), "CS_LLM_OpenSettings".Translate()))
            {
                OnOpenLlmSettings();
            }

            Widgets.EndScrollView();
        }

        private void DrawStringListEditor(ref float y, float width, string label, List<string> values, ref string buffer)
        {
            values ??= new List<string>();
            string display = values.Count == 0 ? "-" : string.Join(", ", values);
            UIHelper.DrawPropertyLabel(ref y, width, label, display);
            UIHelper.DrawPropertyField(ref y, width, label + " CSV", ref buffer);
            ReplaceListFromCsv(values, buffer);
        }

        private void EnsureAttributeCsvBuffers(CharacterAttributeProfile attributes)
        {
            if (string.IsNullOrEmpty(attributesTagsCsv))
            {
                attributesTagsCsv = string.Join(", ", attributes.tags ?? new List<string>());
            }

            if (string.IsNullOrEmpty(attributesTraitsCsv))
            {
                attributesTraitsCsv = string.Join(", ", attributes.keyTraits ?? new List<string>());
            }

            if (string.IsNullOrEmpty(attributesApparelCsv))
            {
                attributesApparelCsv = string.Join(", ", attributes.startingApparelDefs ?? new List<string>());
            }
        }

        private void ReplaceListFromCsv(List<string> target, string csv)
        {
            target.Clear();
            if (string.IsNullOrWhiteSpace(csv))
            {
                isDirty = true;
                return;
            }

            string[] parts = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in parts)
            {
                string value = raw.Trim();
                if (!string.IsNullOrEmpty(value) && !target.Contains(value))
                {
                    target.Add(value);
                }
            }

            isDirty = true;
        }

        private void GenerateCharacterFromPrompt()
        {
            try
            {
                var settings = LlmSettingsRepository.GetOrLoad();
                if (!settings.enabled || !settings.IsConfigured)
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
                var result = LlmGenerationService.GenerateCharacterDesign(settings, llmCharacterPrompt, workingSkin, workingAbilities);
                ApplyGeneratedCharacter(result.payload);
                ShowStatus("CS_LLM_GenerateCharacterSuccess".Translate());
                isDirty = true;
                RefreshPreview();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 角色 LLM 生成失败: {ex}");
                ShowStatus("CS_LLM_GenerateFailed".Translate(ex.Message));
            }
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
            workingSkin.attributes = design.attributes?.Clone() ?? new CharacterAttributeProfile();
            attributesTagsCsv = string.Join(", ", workingSkin.attributes.tags ?? new List<string>());
            attributesTraitsCsv = string.Join(", ", workingSkin.attributes.keyTraits ?? new List<string>());
            attributesApparelCsv = string.Join(", ", workingSkin.attributes.startingApparelDefs ?? new List<string>());

            workingSkin.hiddenPaths.Clear();
            if (design.hiddenNodePaths != null)
            {
                workingSkin.hiddenPaths.AddRange(design.hiddenNodePaths);
            }

            workingSkin.targetRaces.Clear();
            if (design.targetRaceDefs != null)
            {
                workingSkin.targetRaces.AddRange(design.targetRaceDefs);
            }

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
    }
}
