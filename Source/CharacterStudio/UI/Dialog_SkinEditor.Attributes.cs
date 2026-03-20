using System;
using System.Collections.Generic;
using CharacterStudio.AI;
using CharacterStudio.Core;
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

            Rect summaryRect = new Rect(rect.x + Margin, titleRect.yMax + 6f, rect.width - Margin * 2, 24f);
            Widgets.DrawBoxSolid(summaryRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(summaryRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(summaryRect.x + 8f, summaryRect.y, summaryRect.width - 16f, summaryRect.height), $"标签 {attributes.tags?.Count ?? 0} · 特质 {attributes.keyTraits?.Count ?? 0} · 初始装备 {attributes.startingApparelDefs?.Count ?? 0}");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float contentY = summaryRect.yMax + 8f;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            Widgets.DrawBoxSolid(contentRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(contentRect, 1);
            GUI.color = Color.white;

            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, 980f);
            Widgets.BeginScrollView(contentRect.ContractedBy(2f), ref attributesScrollPos, viewRect);

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

            attributes.tags ??= new List<string>();
            attributes.keyTraits ??= new List<string>();
            attributes.startingApparelDefs ??= new List<string>();
            EnsureAttributeCsvBuffers(attributes);
            DrawStringListEditor(ref y, width, "CS_Attr_Tags".Translate(), attributes.tags, ref attributesTagsCsv);
            DrawStringListEditor(ref y, width, "CS_Attr_KeyTraits".Translate(), attributes.keyTraits, ref attributesTraitsCsv);
            DrawStringListEditor(ref y, width, "CS_Attr_StartingApparel".Translate(), attributes.startingApparelDefs, ref attributesApparelCsv);

            UIHelper.DrawSectionTitle(ref y, width, "CS_LLM_CharacterSection".Translate());

            string llmHint = "CS_LLM_EditorTool_CharacterHint".Translate();
            Text.Font = GameFont.Tiny;
            float llmHintHeight = Mathf.Max(40f, Text.CalcHeight(llmHint, Mathf.Max(40f, width - 16f)) + 12f);
            Rect llmHintRect = new Rect(0f, y, width, llmHintHeight);
            Widgets.DrawBoxSolid(llmHintRect, new Color(UIHelper.AccentColor.r, UIHelper.AccentColor.g, UIHelper.AccentColor.b, 0.10f));
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(llmHintRect, 1);
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(8f, y + 4f, width - 16f, llmHintHeight - 8f), llmHint);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += llmHintHeight + 6f;

            Widgets.Label(new Rect(0f, y, width, 24f), "CS_LLM_CharacterPrompt".Translate());
            Rect promptRect = new Rect(0f, y + 26f, width, 110f);
            Widgets.DrawBoxSolid(promptRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(promptRect, 1);
            GUI.color = Color.white;
            llmCharacterPrompt = Widgets.TextArea(promptRect.ContractedBy(4f), llmCharacterPrompt ?? string.Empty);
            y += 144f;

            float buttonWidth = (width - 10f) / 2f;

            // 处理异步回调结果（在主线程 UI 帧中消费）
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
            if (llmCharacterGenerating) return;

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
            llmCharacterGenerating = true;
            ShowStatus("CS_LLM_Generating".Translate());

            // 捕获不可变副本，避免后台线程访问主线程数据竞争
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
