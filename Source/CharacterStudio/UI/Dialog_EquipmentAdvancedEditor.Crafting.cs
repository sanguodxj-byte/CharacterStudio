using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_EquipmentAdvancedEditor
    {
        private void DrawCraftingTab(ref float y, float width)
        {
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_ItemsRecipe".Translate(), "EquipmentCrafting"))
            {
                bool allowCrafting = equipment.allowCrafting;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_AllowCrafting".Translate(), ref allowCrafting, tooltip: "CS_Studio_Equip_AllowCrafting_Tip".Translate());
                if (allowCrafting != equipment.allowCrafting)
                    MutateEquipmentWithUndo(() => equipment.allowCrafting = allowCrafting, refreshRenderTree: false);
                string recipeDefDisplay = string.IsNullOrWhiteSpace(equipment.recipeDefName)
                    ? "CS_Studio_None".Translate()
                    : Dialog_SkinEditor.GetRecipeDefSelectionLabel(equipment.recipeDefName);
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Studio_Equip_RecipeDefName".Translate(), equipment.recipeDefName, () => recipeDefDisplay,
                    () => editor.ShowRecipeDefSelector(selected => MutateEquipmentWithUndo(() => equipment.recipeDefName = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.recipeDefName = string.Empty, refreshRenderTree: false));
                string recipeWorkbenchDisplay = string.IsNullOrWhiteSpace(equipment.recipeWorkbenchDefName)
                    ? "CS_Studio_None".Translate()
                    : Dialog_SkinEditor.GetThingDefSelectionLabel(equipment.recipeWorkbenchDefName);
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Studio_Equip_RecipeWorkbenchDefName".Translate(), equipment.recipeWorkbenchDefName, () => recipeWorkbenchDisplay,
                    () => editor.ShowThingDefSelector(selected => MutateEquipmentWithUndo(() => equipment.recipeWorkbenchDefName = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.recipeWorkbenchDefName = string.Empty, refreshRenderTree: false));
                float recipeWorkAmount = equipment.recipeWorkAmount;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_RecipeWorkAmount".Translate(), ref recipeWorkAmount, 1f, 20000f, "F0", tooltip: "CS_Studio_Equip_RecipeWorkAmount_Tip".Translate());
                if (Math.Abs(recipeWorkAmount - equipment.recipeWorkAmount) > 0.0001f)
                    MutateEquipmentWithUndo(() => equipment.recipeWorkAmount = recipeWorkAmount, refreshRenderTree: false);
                int recipeProductCount = equipment.recipeProductCount;
                UIHelper.DrawNumericField(ref y, width, "CS_Studio_Equip_RecipeProductCount".Translate(), ref recipeProductCount, 1, 999);
                if (recipeProductCount != equipment.recipeProductCount)
                    MutateEquipmentWithUndo(() => equipment.recipeProductCount = recipeProductCount, refreshRenderTree: false);
                // ── recipeIngredients ──
                y += 2f;
                UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Equip_RecipeIngredients".Translate());
                equipment.recipeIngredients ??= new List<CharacterEquipmentCostEntry>();
                for (int ri = equipment.recipeIngredients.Count - 1; ri >= 0; ri--)
                {
                    if (equipment.recipeIngredients[ri] == null || string.IsNullOrWhiteSpace(equipment.recipeIngredients[ri].thingDefName))
                        equipment.recipeIngredients.RemoveAt(ri);
                }
                for (int ri = 0; ri < equipment.recipeIngredients.Count; ri++)
                {
                    int ridx = ri;
                    var ingEntry = equipment.recipeIngredients[ri];
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    float ingHeaderY = y;
                    Widgets.Label(new Rect(4, y, width - 8, 20f), $"  {"CS_Studio_Equip_RecipeIngredients".Translate()} [{ridx}]");
                    y += 22f;
                    string ingDisplay = string.IsNullOrWhiteSpace(ingEntry.thingDefName)
                        ? "CS_Studio_None".Translate()
                        : Dialog_SkinEditor.GetThingDefDisplayLabel(ingEntry.thingDefName);
                    editor.DrawSelectionPropertyButton(
                        ref y, width,
                        "CS_Field_ThingDef".Translate(),
                        ingDisplay,
                        () => editor.ShowThingDefSelector(selected => MutateEquipmentWithUndo(() => { ingEntry.thingDefName = selected; }, refreshRenderTree: false)));
                    int ingCountVal = ingEntry.count;
                    UIHelper.DrawNumericField(ref y, width, "CS_Field_Count".Translate(), ref ingCountVal, 1, 9999);
                    if (ingCountVal != ingEntry.count)
                    {
                        int capturedIdx = ridx;
                        int capturedVal = ingCountVal;
                        MutateEquipmentWithUndo(() => { equipment.recipeIngredients[capturedIdx].count = capturedVal; }, refreshRenderTree: false);
                    }
                    if (UIHelper.DrawDangerButton(new Rect(width - 28f, ingHeaderY + 2f, 24f, 22f),
                        tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            int delIdx = ridx;
                            MutateEquipmentWithUndo(() => { if (equipment.recipeIngredients.Count > delIdx) equipment.recipeIngredients.RemoveAt(delIdx); }, refreshRenderTree: false);
                        }))
                    { }
                }
                float riBtnW = (width - Margin * 2) / 3f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, riBtnW, 22f), "+", "CS_Studio_Equip_Btn_New".Translate(), () =>
                {
                    editor.ShowThingDefSelector(selected => MutateEquipmentWithUndo(() =>
                    {
                        equipment.recipeIngredients ??= new List<CharacterEquipmentCostEntry>();
                        equipment.recipeIngredients.Add(new CharacterEquipmentCostEntry { thingDefName = selected, count = 1 });
                    }, refreshRenderTree: false));
                }))
                { }
                if (equipment.recipeIngredients.Count > 0 && UIHelper.DrawIconButton(new Rect(riBtnW + Margin, y, riBtnW, 22f), "\u00D7", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    MutateEquipmentWithUndo(() => { if (equipment.recipeIngredients.Count > 0) equipment.recipeIngredients.RemoveAt(equipment.recipeIngredients.Count - 1); }, refreshRenderTree: false);
                }))
                { }
                y += 26f;
                bool allowTrading = equipment.allowTrading;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_AllowTrading".Translate(), ref allowTrading, tooltip: "CS_Studio_Equip_AllowTrading_Tip".Translate());
                if (allowTrading != equipment.allowTrading)
                    MutateEquipmentWithUndo(() => equipment.allowTrading = allowTrading, refreshRenderTree: false);
                float marketValue = equipment.marketValue;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Equip_MarketValue".Translate(), ref marketValue, 0.01f, 100000f, "F2", tooltip: "CS_Studio_Equip_MarketValue_Tip".Translate());
                if (Math.Abs(marketValue - equipment.marketValue) > 0.0001f)
                    MutateEquipmentWithUndo(() => equipment.marketValue = marketValue, refreshRenderTree: false);
                string tradeTagsDisplay = (equipment.tradeTags != null && equipment.tradeTags.Count > 0) ? string.Join(", ", equipment.tradeTags) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Studio_Equip_TradeTags".Translate(), string.Join(",", equipment.tradeTags ?? new List<string>()), () => tradeTagsDisplay,
                    () => editor.ShowTagSelector(Dialog_SkinEditor.CollectExistingTags(d => d.tradeTags), equipment.tradeTags ?? new List<string>(), newTags => MutateEquipmentWithUndo(() => equipment.tradeTags = newTags, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.tradeTags = new List<string>(), refreshRenderTree: false));
                string recipeResearchDisplay = string.IsNullOrWhiteSpace(equipment.recipeResearchPrerequisite) ? "CS_Studio_None".Translate() : equipment.recipeResearchPrerequisite;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_RecipeResearchPrerequisite".Translate(), equipment.recipeResearchPrerequisite, () => recipeResearchDisplay,
                    () => editor.ShowResearchProjectDefSelector(selected => MutateEquipmentWithUndo(() => equipment.recipeResearchPrerequisite = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.recipeResearchPrerequisite = string.Empty, refreshRenderTree: false));
                // ── recipeSkillRequirements ──
                y += 2f;
                UIHelper.DrawSectionTitle(ref y, width, "CS_Field_RecipeSkillRequirements".Translate());
                equipment.recipeSkillRequirements ??= new List<CharacterEquipmentStatEntry>();
                for (int sri = equipment.recipeSkillRequirements.Count - 1; sri >= 0; sri--)
                {
                    if (equipment.recipeSkillRequirements[sri] == null || string.IsNullOrWhiteSpace(equipment.recipeSkillRequirements[sri].statDefName))
                        equipment.recipeSkillRequirements.RemoveAt(sri);
                }
                for (int sri = 0; sri < equipment.recipeSkillRequirements.Count; sri++)
                {
                    int sridx = sri;
                    var skillEntry = equipment.recipeSkillRequirements[sri];
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    float skillHeaderY = y;
                    Widgets.Label(new Rect(4, y, width - 8, 20f), $"  {"CS_Field_RecipeSkillRequirements".Translate()} [{sridx}]");
                    y += 22f;
                    string skillDisplay = string.IsNullOrWhiteSpace(skillEntry.statDefName)
                        ? "CS_Studio_None".Translate()
                        : Dialog_SkinEditor.GetSkillDefDisplayLabel(skillEntry.statDefName);
                    editor.DrawSelectionPropertyButton(
                        ref y, width,
                        "CS_Field_SkillDef".Translate(),
                        skillDisplay,
                        () => editor.ShowSkillDefSelector(selected => MutateEquipmentWithUndo(() => { skillEntry.statDefName = selected; }, refreshRenderTree: false)));
                    float skillLevelVal = skillEntry.value;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_SkillLevel".Translate(), ref skillLevelVal, 0f, 20f, "F0", tooltip: "CS_Field_SkillLevel_Tip".Translate());
                    if (Math.Abs(skillLevelVal - skillEntry.value) > 0.0001f)
                    {
                        int capturedIdx = sridx;
                        float capturedVal = skillLevelVal;
                        MutateEquipmentWithUndo(() => { equipment.recipeSkillRequirements[capturedIdx].value = capturedVal; }, refreshRenderTree: false);
                    }
                    if (UIHelper.DrawDangerButton(new Rect(width - 28f, skillHeaderY + 2f, 24f, 22f),
                        tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            int delIdx = sridx;
                            MutateEquipmentWithUndo(() => { if (equipment.recipeSkillRequirements.Count > delIdx) equipment.recipeSkillRequirements.RemoveAt(delIdx); }, refreshRenderTree: false);
                        }))
                    { }
                }
                float rsrBtnW = (width - Margin * 2) / 3f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, rsrBtnW, 22f), "+", "CS_Studio_Equip_Btn_New".Translate(), () =>
                {
                    editor.ShowSkillDefSelector(selected => MutateEquipmentWithUndo(() =>
                    {
                        equipment.recipeSkillRequirements ??= new List<CharacterEquipmentStatEntry>();
                        equipment.recipeSkillRequirements.Add(new CharacterEquipmentStatEntry { statDefName = selected, value = 1f });
                    }, refreshRenderTree: false));
                }))
                { }
                if (equipment.recipeSkillRequirements.Count > 0 && UIHelper.DrawIconButton(new Rect(rsrBtnW + Margin, y, rsrBtnW, 22f), "\u00D7", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    MutateEquipmentWithUndo(() => { if (equipment.recipeSkillRequirements.Count > 0) equipment.recipeSkillRequirements.RemoveAt(equipment.recipeSkillRequirements.Count - 1); }, refreshRenderTree: false);
                }))
                { }
                y += 26f;
                string recipeEffectWorkingDisplay = string.IsNullOrWhiteSpace(equipment.recipeEffectWorking) ? "CS_Studio_None".Translate() : equipment.recipeEffectWorking;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_RecipeEffectWorking".Translate(), equipment.recipeEffectWorking, () => recipeEffectWorkingDisplay,
                    () => editor.ShowEffecterDefSelector(selected => MutateEquipmentWithUndo(() => equipment.recipeEffectWorking = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.recipeEffectWorking = string.Empty, refreshRenderTree: false));
                string recipeSoundWorkingDisplay = string.IsNullOrWhiteSpace(equipment.recipeSoundWorking) ? "CS_Studio_None".Translate() : equipment.recipeSoundWorking;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_RecipeSoundWorking".Translate(), equipment.recipeSoundWorking, () => recipeSoundWorkingDisplay,
                    () => editor.ShowSoundDefSelector(selected => MutateEquipmentWithUndo(() => equipment.recipeSoundWorking = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.recipeSoundWorking = string.Empty, refreshRenderTree: false));
                string recipeUsersDisplay = (equipment.recipeUsers != null && equipment.recipeUsers.Count > 0) ? string.Join(", ", equipment.recipeUsers) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_RecipeUsers".Translate(), string.Join(",", equipment.recipeUsers ?? new List<string>()), () => recipeUsersDisplay,
                    () => editor.ShowTagSelector(Dialog_SkinEditor.CollectExistingTags(d => d.recipes != null ? d.recipes.Select(r => r.defName) : null), equipment.recipeUsers ?? new List<string>(), newTags => MutateEquipmentWithUndo(() => equipment.recipeUsers = newTags, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.recipeUsers = new List<string>(), refreshRenderTree: false));
                string recipeUnfinishedThingDefVal = equipment.recipeUnfinishedThingDef ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Field_UnfinishedThingDef".Translate(), ref recipeUnfinishedThingDefVal, tooltip: "CS_Field_UnfinishedThingDef_Tip".Translate());
                if (recipeUnfinishedThingDefVal != (equipment.recipeUnfinishedThingDef ?? string.Empty))
                    MutateEquipmentWithUndo(() => equipment.recipeUnfinishedThingDef = recipeUnfinishedThingDefVal, refreshRenderTree: false);
                string recipeWorkSkillDisplay = string.IsNullOrWhiteSpace(equipment.recipeWorkSkill) ? "CS_Studio_None".Translate() : equipment.recipeWorkSkill;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_RecipeWorkSkill".Translate(), equipment.recipeWorkSkill, () => recipeWorkSkillDisplay,
                    () => editor.ShowSkillDefSelector(selected => MutateEquipmentWithUndo(() => equipment.recipeWorkSkill = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.recipeWorkSkill = string.Empty, refreshRenderTree: false));
                string recipeWorkSpeedStatDisplay = string.IsNullOrWhiteSpace(equipment.recipeWorkSpeedStat) ? "CS_Studio_None".Translate() : equipment.recipeWorkSpeedStat;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_RecipeWorkSpeedStat".Translate(), equipment.recipeWorkSpeedStat, () => recipeWorkSpeedStatDisplay,
                    () => editor.ShowStatDefSelector(selected => MutateEquipmentWithUndo(() => equipment.recipeWorkSpeedStat = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.recipeWorkSpeedStat = string.Empty, refreshRenderTree: false));
                float recipeDisplayPriorityVal = equipment.recipeDisplayPriority;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_DisplayPriority".Translate(), ref recipeDisplayPriorityVal, 0f, 1000f, "F0", tooltip: "CS_Field_DisplayPriority_Tip".Translate());
                if (Math.Abs(recipeDisplayPriorityVal - equipment.recipeDisplayPriority) > 0.0001f)
                    MutateEquipmentWithUndo(() => equipment.recipeDisplayPriority = recipeDisplayPriorityVal, refreshRenderTree: false);
            }
        }
    }
}
