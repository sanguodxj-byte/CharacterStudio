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
        private void DrawThingDefTab(ref float y, float width)
        {
            DrawEquipmentThingDefCore(ref y, width);
            DrawEquipmentExtraFields(ref y, width);
        }

        private void DrawEquipmentThingDefCore(ref float y, float width)
        {
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_ThingDefCore".Translate(), "EquipmentThingDefCore"))
            {
                string worldTexPath = equipment.worldTexPath ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_Equip_WorldTexPath".Translate(), ref worldTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.worldTexPath ?? string.Empty, path =>
                    {
                        MutateEquipmentWithUndo(() => equipment.worldTexPath = path ?? string.Empty, refreshRenderTree: false);
                    }))))
                {
                    MutateEquipmentWithUndo(() => equipment.worldTexPath = worldTexPath, refreshRenderTree: false);
                }
                editor.DrawSelectionPropertyButton(
                    ref y,
                    width,
                    "CS_Studio_Equip_ShaderDef".Translate(),
                    Dialog_SkinEditor.GetEquipmentShaderSelectionLabel(equipment.shaderDefName),
                    () => editor.ShowEquipmentShaderSelector(equipment, () => MarkEquipmentDirty()));
                string thingCatDisplay = (equipment.thingCategories != null && equipment.thingCategories.Count > 0) ? string.Join(", ", equipment.thingCategories) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Studio_Equip_ThingCategories".Translate(), string.Join(",", equipment.thingCategories ?? new List<string>()), () => thingCatDisplay,
                    () => editor.ShowTagSelector(DefDatabase<ThingCategoryDef>.AllDefsListForReading.Select(d => d.defName).OrderBy(n => n).ToList(), equipment.thingCategories ?? new List<string>(), newTags => MutateEquipmentWithUndo(() => equipment.thingCategories = newTags, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.thingCategories = new List<string>(), refreshRenderTree: false));
                {
                }
                string descriptionVal = equipment.description ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Field_Description".Translate(), ref descriptionVal, tooltip: "CS_Field_Description_Tip".Translate());
                if (descriptionVal != (equipment.description ?? string.Empty))
                    MutateEquipmentWithUndo(() => equipment.description = descriptionVal, refreshRenderTree: false);
                string thingClassDisplay = string.IsNullOrWhiteSpace(equipment.thingClass) ? "CS_Studio_None".Translate() : equipment.thingClass;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_ThingClass".Translate(), equipment.thingClass, () => thingClassDisplay,
                    () => editor.ShowEnumSelector(new[] { "", "ThingWithComps", "Apparel", "Bullet", "Thing", "Mote" }, selected => MutateEquipmentWithUndo(() => equipment.thingClass = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.thingClass = string.Empty, refreshRenderTree: false));
                string techLevelVal = equipment.techLevel ?? string.Empty;
                var techLevelOptions = new[] { "", "Neolithic", "Medieval", "Industrial", "Spacer", "Ultra", "Transhumanist", "Archotech" }.ToList();
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Field_TechLevel".Translate(), techLevelVal, techLevelOptions, v => string.IsNullOrEmpty(v) ? "-" : v, val => MutateEquipmentWithUndo(() => equipment.techLevel = val, refreshRenderTree: false));
                string altLayerDisplay = string.IsNullOrWhiteSpace(equipment.altitudeLayer) ? "CS_Studio_None".Translate() : equipment.altitudeLayer;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_AltitudeLayer".Translate(), equipment.altitudeLayer, () => altLayerDisplay,
                    () => editor.ShowEnumSelector(Enum.GetNames(typeof(AltitudeLayer)).Cast<string>().ToList(), selected => MutateEquipmentWithUndo(() => equipment.altitudeLayer = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.altitudeLayer = string.Empty, refreshRenderTree: false));
                string tickerDisplay = string.IsNullOrWhiteSpace(equipment.tickerType) ? "CS_Studio_None".Translate() : equipment.tickerType;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_TickerType".Translate(), equipment.tickerType, () => tickerDisplay,
                    () => editor.ShowEnumSelector(Enum.GetNames(typeof(TickerType)).Cast<string>().ToList(), selected => MutateEquipmentWithUndo(() => equipment.tickerType = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.tickerType = string.Empty, refreshRenderTree: false));
                string graphicDrawSizeVal = equipment.graphicDrawSize ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Field_DrawSize".Translate(), ref graphicDrawSizeVal, tooltip: "CS_Field_DrawSize_Tip".Translate());
                if (graphicDrawSizeVal != (equipment.graphicDrawSize ?? string.Empty))
                    MutateEquipmentWithUndo(() => equipment.graphicDrawSize = graphicDrawSizeVal, refreshRenderTree: false);
                float graphicRandomRotateAngleVal = equipment.graphicRandomRotateAngle;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_RandomRotateAngle".Translate(), ref graphicRandomRotateAngleVal, 0f, 360f, "F0", tooltip: "CS_Field_RandomRotateAngle_Tip".Translate());
                if (Math.Abs(graphicRandomRotateAngleVal - equipment.graphicRandomRotateAngle) > 0.0001f)
                    MutateEquipmentWithUndo(() => equipment.graphicRandomRotateAngle = graphicRandomRotateAngleVal, refreshRenderTree: false);
                bool useHitPointsVal = equipment.useHitPoints;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_UseHitPoints".Translate(), ref useHitPointsVal, tooltip: "CS_Field_UseHitPoints_Tip".Translate());
                if (useHitPointsVal != equipment.useHitPoints)
                    MutateEquipmentWithUndo(() => equipment.useHitPoints = useHitPointsVal, refreshRenderTree: false);
                if (!equipment.useTemplateMode)
                {
                    bool smeltableVal = equipment.smeltable;
                    UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_Smeltable".Translate(), ref smeltableVal, tooltip: "CS_Field_Smeltable_Tip".Translate());
                    if (smeltableVal != equipment.smeltable)
                        MutateEquipmentWithUndo(() => equipment.smeltable = smeltableVal, refreshRenderTree: false);
                }
                float pathCostVal = equipment.pathCost;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_PathCost".Translate(), ref pathCostVal, 0f, 1000f, "F0", tooltip: "CS_Field_PathCost_Tip".Translate());
                if (Math.Abs(pathCostVal - equipment.pathCost) > 0.0001f)
                    MutateEquipmentWithUndo(() => equipment.pathCost = Mathf.RoundToInt(pathCostVal), refreshRenderTree: false);
                string stuffCatDisplay = (equipment.stuffCategories != null && equipment.stuffCategories.Count > 0) ? string.Join(", ", equipment.stuffCategories) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_StuffCategories".Translate(), string.Join(",", equipment.stuffCategories ?? new List<string>()), () => stuffCatDisplay,
                    () => editor.ShowTagSelector(DefDatabase<StuffCategoryDef>.AllDefsListForReading.Select(d => d.defName).OrderBy(n => n).ToList(), equipment.stuffCategories ?? new List<string>(), newTags => MutateEquipmentWithUndo(() => equipment.stuffCategories = newTags, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.stuffCategories = new List<string>(), refreshRenderTree: false));
                int costStuffCountVal = equipment.costStuffCount;
                UIHelper.DrawNumericField(ref y, width, "CS_Field_CostStuffCount".Translate(), ref costStuffCountVal, 0, 9999);
                if (costStuffCountVal != equipment.costStuffCount)
                    MutateEquipmentWithUndo(() => equipment.costStuffCount = costStuffCountVal, refreshRenderTree: false);
                y += 2f;
                UIHelper.DrawSectionTitle(ref y, width, "CS_Field_CostList".Translate());
                equipment.costList ??= new List<CharacterEquipmentCostEntry>();
                for (int ci = equipment.costList.Count - 1; ci >= 0; ci--)
                {
                    if (equipment.costList[ci] == null || string.IsNullOrWhiteSpace(equipment.costList[ci].thingDefName))
                        equipment.costList.RemoveAt(ci);
                }
                for (int ci = 0; ci < equipment.costList.Count; ci++)
                {
                    int cidx = ci;
                    var costEntry = equipment.costList[ci];
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    float costHeaderY = y;
                    Widgets.Label(new Rect(4, y, width - 8, 20f), $"  {"CS_Field_CostList".Translate()} [{cidx}]");
                    y += 22f;
                    string costThingDisplay = string.IsNullOrWhiteSpace(costEntry.thingDefName)
                        ? "CS_Studio_None".Translate()
                        : Dialog_SkinEditor.GetThingDefDisplayLabel(costEntry.thingDefName);
                    editor.DrawSelectionPropertyButton(
                        ref y, width,
                        "CS_Field_ThingDef".Translate(),
                        costThingDisplay,
                        () => editor.ShowThingDefSelector(selected => MutateEquipmentWithUndo(() => { costEntry.thingDefName = selected; }, refreshRenderTree: false)));
                    int costCountVal = costEntry.count;
                    UIHelper.DrawNumericField(ref y, width, "CS_Field_Count".Translate(), ref costCountVal, 1, 9999);
                    if (costCountVal != costEntry.count)
                    {
                        int capturedIdx = cidx;
                        int capturedVal = costCountVal;
                        MutateEquipmentWithUndo(() => { equipment.costList[capturedIdx].count = capturedVal; }, refreshRenderTree: false);
                    }
                    if (UIHelper.DrawDangerButton(new Rect(width - 28f, costHeaderY + 2f, 24f, 22f),
                        tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            int delIdx = cidx;
                            MutateEquipmentWithUndo(() => { if (equipment.costList.Count > delIdx) equipment.costList.RemoveAt(delIdx); }, refreshRenderTree: false);
                        }))
                    { }
                }
                float clBtnW = (width - Margin * 2) / 3f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, clBtnW, 22f), "+", "CS_Studio_Equip_Btn_New".Translate(), () =>
                {
                    editor.ShowThingDefSelector(selected => MutateEquipmentWithUndo(() =>
                    {
                        equipment.costList ??= new List<CharacterEquipmentCostEntry>();
                        equipment.costList.Add(new CharacterEquipmentCostEntry { thingDefName = selected, count = 1 });
                    }, refreshRenderTree: false));
                }))
                { }
                if (equipment.costList.Count > 0 && UIHelper.DrawIconButton(new Rect(clBtnW + Margin, y, clBtnW, 22f), "\u00D7", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    MutateEquipmentWithUndo(() => { if (equipment.costList.Count > 0) equipment.costList.RemoveAt(equipment.costList.Count - 1); }, refreshRenderTree: false);
                }))
                { }
                y += 26f;
            }
        }

        private void DrawEquipmentExtraFields(ref float y, float width)
        {
            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_ExtraFields".Translate(), "EquipmentExtraFields"))
            {
                equipment.extraFields ??= new Dictionary<string, string>();
                var keys = equipment.extraFields.Keys.ToList();
                for (int i = keys.Count - 1; i >= 0; i--)
                {
                    if (string.IsNullOrWhiteSpace(keys[i]))
                    {
                        equipment.extraFields.Remove(keys[i]);
                    }
                }
                keys = equipment.extraFields.Keys.ToList();
                for (int fi = 0; fi < keys.Count; fi++)
                {
                    int fidx = fi;
                    string fKey = keys[fi];
                    string fVal = equipment.extraFields[fKey] ?? "";
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    float fHeaderY = y;
                    Widgets.Label(new Rect(4, y, width - 60, 20f), $"  {fKey}");
                    y += 22f;
                    string fValEdit = fVal;
                    UIHelper.DrawPropertyField(ref y, width, "CS_Field_Value".Translate(), ref fValEdit);
                    if (fValEdit != fVal)
                    {
                        string capturedKey = fKey;
                        string capturedVal = fValEdit;
                        MutateEquipmentWithUndo(() => { equipment.extraFields[capturedKey] = capturedVal; }, refreshRenderTree: false);
                    }
                    if (UIHelper.DrawDangerButton(new Rect(width - 28f, fHeaderY + 2f, 24f, 22f),
                        tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            string delKey = fKey;
                            MutateEquipmentWithUndo(() => { equipment.extraFields.Remove(delKey); }, refreshRenderTree: false);
                        }))
                    { }
                }
                float efBtnW = (width - Margin * 2) / 2f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, efBtnW, 22f), "+", "CS_Studio_Equip_AddExtraField".Translate(), () =>
                {
                    Find.WindowStack.Add(new Dialog_ExtraFieldBrowser(equipment.extraFields, selectedKey =>
                    {
                        MutateEquipmentWithUndo(() =>
                        {
                            equipment.extraFields ??= new Dictionary<string, string>();
                            if (!equipment.extraFields.ContainsKey(selectedKey))
                                equipment.extraFields[selectedKey] = "";
                        }, refreshRenderTree: false);
                    }));
                }))
                { }
                if (keys.Count > 0 && UIHelper.DrawIconButton(new Rect(efBtnW + Margin, y, efBtnW, 22f), "\u00D7", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    MutateEquipmentWithUndo(() =>
                    {
                        if (equipment.extraFields.Count > 0)
                        {
                            var lastKey = equipment.extraFields.Keys.Last();
                            equipment.extraFields.Remove(lastKey);
                        }
                    }, refreshRenderTree: false);
                }))
                { }
                y += 26f;
            }
        }

        private void MarkEquipmentDirty(bool refreshRenderTree = true, string? statusMessage = null)
        {
            editor.FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: refreshRenderTree, statusMessage: statusMessage);
        }
    }
}
