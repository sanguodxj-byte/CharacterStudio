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
        private void DrawStatsTab(ref float y, float width)
        {
            if (DrawCollapsibleSection(ref y, width, "CS_Field_StatBases".Translate(), "EquipmentStats"))
            {
                y += 2f;
                UIHelper.DrawSectionTitle(ref y, width, "CS_Field_StatBases".Translate());
                equipment.statBases ??= new List<CharacterEquipmentStatEntry>();
                for (int si = equipment.statBases.Count - 1; si >= 0; si--)
                {
                    if (equipment.statBases[si] == null || string.IsNullOrWhiteSpace(equipment.statBases[si].statDefName))
                        equipment.statBases.RemoveAt(si);
                }
                for (int si = 0; si < equipment.statBases.Count; si++)
                {
                    int sidx = si;
                    var statEntry = equipment.statBases[si];
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    float statHeaderY = y;
                    Widgets.Label(new Rect(4, y, width - 8, 20f), $"  {"CS_Field_StatBases".Translate()} [{sidx}]");
                    y += 22f;
                    string statDisplay = string.IsNullOrWhiteSpace(statEntry.statDefName)
                        ? "CS_Studio_None".Translate()
                        : Dialog_SkinEditor.GetStatDefDisplayLabel(statEntry.statDefName);
                    editor.DrawSelectionPropertyButton(
                        ref y, width,
                        "CS_Field_StatDef".Translate(),
                        statDisplay,
                        () => editor.ShowStatDefSelector(selected => MutateEquipmentWithUndo(() => { statEntry.statDefName = selected; }, refreshRenderTree: false)));
                    float statValueVal = statEntry.value;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_StatValue".Translate(), ref statValueVal, -1000f, 10000f, "F2", tooltip: "CS_Field_StatValue_Tip".Translate());
                    if (Math.Abs(statValueVal - statEntry.value) > 0.0001f)
                    {
                        int capturedIdx = sidx;
                        float capturedVal = statValueVal;
                        MutateEquipmentWithUndo(() => { equipment.statBases[capturedIdx].value = capturedVal; }, refreshRenderTree: false);
                    }
                    if (UIHelper.DrawDangerButton(new Rect(width - 28f, statHeaderY + 2f, 24f, 22f),
                        tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            int delIdx = sidx;
                            MutateEquipmentWithUndo(() => { if (equipment.statBases.Count > delIdx) equipment.statBases.RemoveAt(delIdx); }, refreshRenderTree: false);
                        }))
                    { }
                }
                float sbBtnW = (width - Margin * 2) / 3f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, sbBtnW, 22f), "+", "CS_Studio_Equip_Btn_New".Translate(), () =>
                {
                    editor.ShowStatDefSelector(selected => MutateEquipmentWithUndo(() =>
                    {
                        equipment.statBases ??= new List<CharacterEquipmentStatEntry>();
                        equipment.statBases.Add(new CharacterEquipmentStatEntry { statDefName = selected, value = 1f });
                    }, refreshRenderTree: false));
                }))
                { }
                if (equipment.statBases.Count > 0 && UIHelper.DrawIconButton(new Rect(sbBtnW + Margin, y, sbBtnW, 22f), "\u00D7", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    MutateEquipmentWithUndo(() => { if (equipment.statBases.Count > 0) equipment.statBases.RemoveAt(equipment.statBases.Count - 1); }, refreshRenderTree: false);
                }))
                { }
                y += 26f;

                // ── equippedStatOffsets ──
                y += 2f;
                UIHelper.DrawSectionTitle(ref y, width, "CS_Field_EquippedStatOffsets".Translate());
                equipment.equippedStatOffsets ??= new List<CharacterEquipmentStatEntry>();
                for (int oi = equipment.equippedStatOffsets.Count - 1; oi >= 0; oi--)
                {
                    if (equipment.equippedStatOffsets[oi] == null || string.IsNullOrWhiteSpace(equipment.equippedStatOffsets[oi].statDefName))
                        equipment.equippedStatOffsets.RemoveAt(oi);
                }
                for (int oi = 0; oi < equipment.equippedStatOffsets.Count; oi++)
                {
                    int oidx = oi;
                    var offsetEntry = equipment.equippedStatOffsets[oi];
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    float offsetHeaderY = y;
                    Widgets.Label(new Rect(4, y, width - 8, 20f), $"  {"CS_Field_EquippedStatOffsets".Translate()} [{oidx}]");
                    y += 22f;
                    string offsetStatDisplay = string.IsNullOrWhiteSpace(offsetEntry.statDefName)
                        ? "CS_Studio_None".Translate()
                        : Dialog_SkinEditor.GetStatDefDisplayLabel(offsetEntry.statDefName);
                    editor.DrawSelectionPropertyButton(
                        ref y, width,
                        "CS_Field_StatDef".Translate(),
                        offsetStatDisplay,
                        () => editor.ShowStatDefSelector(selected => MutateEquipmentWithUndo(() => { offsetEntry.statDefName = selected; }, refreshRenderTree: false)));
                    float offsetVal = offsetEntry.value;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_StatValue".Translate(), ref offsetVal, -1000f, 10000f, "F2", tooltip: "CS_Field_StatValue_Tip".Translate());
                    if (Math.Abs(offsetVal - offsetEntry.value) > 0.0001f)
                    {
                        int capturedIdx = oidx;
                        float capturedVal = offsetVal;
                        MutateEquipmentWithUndo(() => { equipment.equippedStatOffsets[capturedIdx].value = capturedVal; }, refreshRenderTree: false);
                    }
                    if (UIHelper.DrawDangerButton(new Rect(width - 28f, offsetHeaderY + 2f, 24f, 22f),
                        tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            int delIdx = oidx;
                            MutateEquipmentWithUndo(() => { if (equipment.equippedStatOffsets.Count > delIdx) equipment.equippedStatOffsets.RemoveAt(delIdx); }, refreshRenderTree: false);
                        }))
                    { }
                }
                float esoBtnW = (width - Margin * 2) / 3f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, esoBtnW, 22f), "+", "CS_Studio_Equip_Btn_New".Translate(), () =>
                {
                    editor.ShowStatDefSelector(selected => MutateEquipmentWithUndo(() =>
                    {
                        equipment.equippedStatOffsets ??= new List<CharacterEquipmentStatEntry>();
                        equipment.equippedStatOffsets.Add(new CharacterEquipmentStatEntry { statDefName = selected, value = 0f });
                    }, refreshRenderTree: false));
                }))
                { }
                if (equipment.equippedStatOffsets.Count > 0 && UIHelper.DrawIconButton(new Rect(esoBtnW + Margin, y, esoBtnW, 22f), "\u00D7", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    MutateEquipmentWithUndo(() => { if (equipment.equippedStatOffsets.Count > 0) equipment.equippedStatOffsets.RemoveAt(equipment.equippedStatOffsets.Count - 1); }, refreshRenderTree: false);
                }))
                { }
                y += 26f;
            }
        }
    }
}
