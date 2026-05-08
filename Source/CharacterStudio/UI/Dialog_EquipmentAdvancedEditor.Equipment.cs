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
        private void DrawEquipmentTab(ref float y, float width)
        {
            DrawApparelSection(ref y, width);
            DrawWeaponSection(ref y, width);
            DrawBuildingSection(ref y, width);
            DrawTurretSection(ref y, width);
        }

        private void DrawApparelSection(ref float y, float width)
        {
            if (equipment.itemType != CharacterStudio.Core.EquipmentType.Apparel) return;
            if (!DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_ItemsApparel".Translate(), "EquipmentApparel")) return;
            string wornTexPath = equipment.wornTexPath ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_Equip_WornTexPath".Translate(), ref wornTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.wornTexPath ?? string.Empty, path =>
                    {
                        MutateEquipmentWithUndo(() => equipment.wornTexPath = path ?? string.Empty, refreshRenderTree: false);
                    }))))
                {
                    MutateEquipmentWithUndo(() => equipment.wornTexPath = wornTexPath, refreshRenderTree: false);
                }
                string equipmentMaskTexPath = equipment.maskTexPath ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_Equip_ApparelMask".Translate(), ref equipmentMaskTexPath, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(equipment.maskTexPath ?? string.Empty, path =>
                    {
                        MutateEquipmentWithUndo(() => equipment.maskTexPath = path ?? string.Empty, refreshRenderTree: false);
                    }))))
                {
                    MutateEquipmentWithUndo(() => equipment.maskTexPath = equipmentMaskTexPath, refreshRenderTree: false);
                }
                bool useWornGraphicMask = equipment.useWornGraphicMask;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Equip_UseWornGraphicMask".Translate(), ref useWornGraphicMask, tooltip: "CS_Studio_Equip_UseWornGraphicMask_Tip".Translate());
                if (useWornGraphicMask != equipment.useWornGraphicMask)
                    MutateEquipmentWithUndo(() => equipment.useWornGraphicMask = useWornGraphicMask, refreshRenderTree: false);
                string bodyPartGroupsLabel = (equipment.bodyPartGroups != null && equipment.bodyPartGroups.Count > 0)
                    ? string.Join(", ", equipment.bodyPartGroups.Select(defName =>
                    {
                        var def = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail(defName);
                        return def != null ? (def.label ?? def.defName) : defName;
                    }))
                    : "CS_Studio_None".Translate();
                editor.DrawSelectionPropertyButton(
                    ref y,
                    width,
                    "CS_Studio_Equip_BodyPartGroups".Translate(),
                    bodyPartGroupsLabel,
                    () => editor.ShowBodyPartGroupDefSelector(selected =>
                    {
                        MutateWithUndo(() =>{
                            equipment.bodyPartGroups ??= new List<string>();
                            if (!equipment.bodyPartGroups.Contains(selected))
                            {
                                equipment.bodyPartGroups.Add(selected);
                            }
                        }, false);
                    }, equipment));
                string apparelLayersLabel = (equipment.apparelLayers != null && equipment.apparelLayers.Count > 0)
                    ? string.Join(", ", equipment.apparelLayers.Select(defName =>
                    {
                        var def = DefDatabase<ApparelLayerDef>.GetNamedSilentFail(defName);
                        return def != null ? (def.label ?? def.defName) : defName;
                    }))
                    : "CS_Studio_None".Translate();
                editor.DrawSelectionPropertyButton(
                    ref y,
                    width,
                    "CS_Studio_Equip_ApparelLayers".Translate(),
                    apparelLayersLabel,
                    () => editor.ShowApparelLayerDefSelector(selected =>
                    {
                        MutateWithUndo(() =>{
                            equipment.apparelLayers ??= new List<string>();
                            if (!equipment.apparelLayers.Contains(selected))
                            {
                                equipment.apparelLayers.Add(selected);
                            }
                        }, false);
                    }, equipment));
                string apparelTagsDisplay = (equipment.apparelTags != null && equipment.apparelTags.Count > 0) ? string.Join(", ", equipment.apparelTags) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Studio_Equip_ApparelTags".Translate(), string.Join(",", equipment.apparelTags ?? new List<string>()), () => apparelTagsDisplay,
                    () => editor.ShowTagSelector(Dialog_SkinEditor.CollectExistingTags(d => d.apparel?.tags), equipment.apparelTags ?? new List<string>(), newTags => MutateEquipmentWithUndo(() => equipment.apparelTags = newTags, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.apparelTags = new List<string>(), refreshRenderTree: false));
                float wearPerDayVal = equipment.wearPerDay;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_WearPerDay".Translate(), ref wearPerDayVal, 0f, 10f, "F2", tooltip: "CS_Field_WearPerDay_Tip".Translate());
                if (Math.Abs(wearPerDayVal - equipment.wearPerDay) > 0.0001f)
                    MutateEquipmentWithUndo(() => equipment.wearPerDay = wearPerDayVal, refreshRenderTree: false);
                bool careIfDamagedVal = equipment.careIfDamaged ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_CareIfDamaged".Translate(), ref careIfDamagedVal, tooltip: "CS_Field_CareIfDamaged_Tip".Translate());
                if (careIfDamagedVal != (equipment.careIfDamaged ?? false))
                    MutateEquipmentWithUndo(() => equipment.careIfDamaged = careIfDamagedVal, refreshRenderTree: false);
                bool careIfWornByCorpseVal = equipment.careIfWornByCorpse ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_CareIfWornByCorpse".Translate(), ref careIfWornByCorpseVal, tooltip: "CS_Field_CareIfWornByCorpse_Tip".Translate());
                if (careIfWornByCorpseVal != (equipment.careIfWornByCorpse ?? false))
                    MutateEquipmentWithUndo(() => equipment.careIfWornByCorpse = careIfWornByCorpseVal, refreshRenderTree: false);
                bool countsAsClothingVal = equipment.countsAsClothingForNudity ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_CountsAsClothingForNudity".Translate(), ref countsAsClothingVal, tooltip: "CS_Field_CountsAsClothingForNudity_Tip".Translate());
                if (countsAsClothingVal != (equipment.countsAsClothingForNudity ?? false))
                    MutateEquipmentWithUndo(() => equipment.countsAsClothingForNudity = countsAsClothingVal, refreshRenderTree: false);
                bool slaveApparelVal = equipment.slaveApparel ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_SlaveApparel".Translate(), ref slaveApparelVal, tooltip: "CS_Field_SlaveApparel_Tip".Translate());
                if (slaveApparelVal != (equipment.slaveApparel ?? false))
                    MutateEquipmentWithUndo(() => equipment.slaveApparel = slaveApparelVal, refreshRenderTree: false);
                bool canBeDesiredForIdeoVal = equipment.canBeDesiredForIdeo ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_CanBeDesiredForIdeo".Translate(), ref canBeDesiredForIdeoVal, tooltip: "CS_Field_CanBeDesiredForIdeo_Tip".Translate());
                if (canBeDesiredForIdeoVal != (equipment.canBeDesiredForIdeo ?? false))
                    MutateEquipmentWithUndo(() => equipment.canBeDesiredForIdeo = canBeDesiredForIdeoVal, refreshRenderTree: false);
                string soundWearDisplay = string.IsNullOrWhiteSpace(equipment.soundWear) ? "CS_Studio_None".Translate() : equipment.soundWear;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_SoundWear".Translate(), equipment.soundWear, () => soundWearDisplay,
                    () => editor.ShowSoundDefSelector(selected => MutateEquipmentWithUndo(() => equipment.soundWear = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.soundWear = string.Empty, refreshRenderTree: false));
                string soundRemoveDisplay = string.IsNullOrWhiteSpace(equipment.soundRemove) ? "CS_Studio_None".Translate() : equipment.soundRemove;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_SoundRemove".Translate(), equipment.soundRemove, () => soundRemoveDisplay,
                    () => editor.ShowSoundDefSelector(selected => MutateEquipmentWithUndo(() => equipment.soundRemove = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.soundRemove = string.Empty, refreshRenderTree: false));
                bool useDeflectMetalEffectVal = equipment.useDeflectMetalEffect;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_UseDeflectMetalEffect".Translate(), ref useDeflectMetalEffectVal, tooltip: "CS_Field_UseDeflectMetalEffect_Tip".Translate());
                if (useDeflectMetalEffectVal != equipment.useDeflectMetalEffect)
                    MutateEquipmentWithUndo(() => equipment.useDeflectMetalEffect = useDeflectMetalEffectVal, refreshRenderTree: false);
                bool blocksVisionVal = equipment.blocksVision ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_BlocksVision".Translate(), ref blocksVisionVal, tooltip: "CS_Field_BlocksVision_Tip".Translate());
                if (blocksVisionVal != (equipment.blocksVision ?? false))
                    MutateEquipmentWithUndo(() => equipment.blocksVision = blocksVisionVal, refreshRenderTree: false);
                bool ignoredByNonViolentVal = equipment.ignoredByNonViolent ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_IgnoredByNonViolent".Translate(), ref ignoredByNonViolentVal, tooltip: "CS_Field_IgnoredByNonViolent_Tip".Translate());
                if (ignoredByNonViolentVal != (equipment.ignoredByNonViolent ?? false))
                    MutateEquipmentWithUndo(() => equipment.ignoredByNonViolent = ignoredByNonViolentVal, refreshRenderTree: false);
                string renderSkipFlagsDisplay = (equipment.apparelRenderSkipFlags != null && equipment.apparelRenderSkipFlags.Count > 0) ? string.Join(", ", equipment.apparelRenderSkipFlags) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_ApparelRenderSkipFlags".Translate(), string.Join(",", equipment.apparelRenderSkipFlags ?? new List<string>()), () => renderSkipFlagsDisplay,
                    () => editor.ShowTagSelector(DefDatabase<RenderSkipFlagDef>.AllDefsListForReading.Select(d => d.defName).OrderBy(n => n), equipment.apparelRenderSkipFlags ?? new List<string>(), newTags => MutateEquipmentWithUndo(() => equipment.apparelRenderSkipFlags = newTags, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.apparelRenderSkipFlags = new List<string>(), refreshRenderTree: false));
                string developmentalStageFilterVal = equipment.developmentalStageFilter ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Field_DevelopmentalStageFilter".Translate(), ref developmentalStageFilterVal, tooltip: "CS_Field_DevelopmentalStageFilter_Tip".Translate());
                if (developmentalStageFilterVal != (equipment.developmentalStageFilter ?? string.Empty))
                    MutateEquipmentWithUndo(() => equipment.developmentalStageFilter = developmentalStageFilterVal, refreshRenderTree: false);
                float apparelScoreOffsetVal = equipment.apparelScoreOffset;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_ApparelScoreOffset".Translate(), ref apparelScoreOffsetVal, -100f, 100f, "F1", tooltip: "CS_Field_ApparelScoreOffset_Tip".Translate());
                if (Math.Abs(apparelScoreOffsetVal - equipment.apparelScoreOffset) > 0.0001f)
                    MutateEquipmentWithUndo(() => equipment.apparelScoreOffset = apparelScoreOffsetVal, refreshRenderTree: false);
                string apparelDrawDataXmlVal = equipment.apparelDrawDataXml ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Field_DrawData".Translate(), ref apparelDrawDataXmlVal, tooltip: "CS_Field_DrawData_Tip".Translate());
                if (apparelDrawDataXmlVal != (equipment.apparelDrawDataXml ?? string.Empty))
                    MutateEquipmentWithUndo(() => equipment.apparelDrawDataXml = apparelDrawDataXmlVal, refreshRenderTree: false);
        }

        private void DrawWeaponSection(ref float y, float width)
        {
            if (equipment.itemType != CharacterStudio.Core.EquipmentType.WeaponMelee && equipment.itemType != CharacterStudio.Core.EquipmentType.WeaponRanged) return;
            if (!DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_ItemsWeapon".Translate(), "EquipmentWeapon")) return;
            string weaponTagsDisplay = (equipment.weaponTags != null && equipment.weaponTags.Count > 0) ? string.Join(", ", equipment.weaponTags) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Studio_Equip_WeaponTags".Translate(), string.Join(",", equipment.weaponTags ?? new List<string>()), () => weaponTagsDisplay,
                    () => editor.ShowTagSelector(Dialog_SkinEditor.CollectExistingTags(d => d.weaponTags), equipment.weaponTags ?? new List<string>(), newTags => MutateEquipmentWithUndo(() => equipment.weaponTags = newTags, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.weaponTags = new List<string>(), refreshRenderTree: false));
                string weaponClassesDisplay = (equipment.weaponClasses != null && equipment.weaponClasses.Count > 0) ? string.Join(", ", equipment.weaponClasses) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Studio_Equip_WeaponClasses".Translate(), string.Join(",", equipment.weaponClasses ?? new List<string>()), () => weaponClassesDisplay,
                    () => editor.ShowTagSelector(Dialog_SkinEditor.CollectExistingTags(d => d.weaponClasses?.Select(wc => wc.defName)), equipment.weaponClasses ?? new List<string>(), newTags => MutateEquipmentWithUndo(() => equipment.weaponClasses = newTags, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.weaponClasses = new List<string>(), refreshRenderTree: false));
                // ── Tools 结构化编辑 ──
                y += 4f;
                UIHelper.DrawSectionTitle(ref y, width, "CS_Section_Tools".Translate());
                // 从 rawXmlEntries 中找到 tagName=="tools" 的条目并解析
                var toolsEntry = equipment.rawXmlEntries.FirstOrDefault(e => e.tagName == "tools");
                var tools = WeaponToolEntry.ParseFromXml(toolsEntry?.innerXml ?? "");
                bool toolsDirty = false;
                for (int ti = 0; ti < tools.Count; ti++)
                {
                    int tidx = ti;
                    var tool = tools[tidx];
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    Widgets.Label(new Rect(4, y, width - 8, 20f), $"  {"CS_Section_Tools".Translate()} [{tidx}]");
                    y += 22f;
                    string toolLabel = tool.label ?? string.Empty;
                    UIHelper.DrawPropertyField(ref y, width, "CS_Field_ToolLabel".Translate(), ref toolLabel, tooltip: "CS_Field_ToolLabel_Tip".Translate());
                    if (toolLabel != (tool.label ?? string.Empty)) { tool.label = toolLabel; toolsDirty = true; }
                    string capsDisplay = string.IsNullOrWhiteSpace(tool.capacities) ? "CS_Studio_None".Translate() : tool.capacities;
                    UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_ToolCapacities".Translate(), tool.capacities ?? string.Empty, () => capsDisplay,
                        () => editor.ShowToolCapacityDefSelector(selected => { var cur = string.IsNullOrWhiteSpace(tool.capacities) ? new List<string>() : (tool.capacities ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(); if (!cur.Contains(selected)) cur.Add(selected); tool.capacities = string.Join(", ", cur); toolsDirty = true; }),
                        () => { tool.capacities = string.Empty; toolsDirty = true; });
                    float powerVal = tool.power;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_ToolPower".Translate(), ref powerVal, 0f, 200f, "F1", tooltip: "CS_Field_ToolPower_Tip".Translate());
                    if (Math.Abs(powerVal - tool.power) > 0.001f) { tool.power = powerVal; toolsDirty = true; }
                    float cdVal = tool.cooldownTime;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_ToolCooldown".Translate(), ref cdVal, 0f, 10f, "F2", tooltip: "CS_Field_ToolCooldown_Tip".Translate());
                    if (Math.Abs(cdVal - tool.cooldownTime) > 0.001f) { tool.cooldownTime = cdVal; toolsDirty = true; }
                    float apVal = tool.armorPenetration;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_ToolArmorPen".Translate(), ref apVal, -1f, 2f, "F2", tooltip: "CS_Field_ToolArmorPen_Tip".Translate());
                    if (Math.Abs(apVal - tool.armorPenetration) > 0.001f) { tool.armorPenetration = apVal; toolsDirty = true; }
                    float cfVal = tool.chanceFactor;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_ToolChanceFactor".Translate(), ref cfVal, -1f, 5f, "F2", tooltip: "CS_Field_ToolChanceFactor_Tip".Translate());
                    if (Math.Abs(cfVal - tool.chanceFactor) > 0.001f) { tool.chanceFactor = cfVal; toolsDirty = true; }
                }
                float toolBtnW = (width - Margin * 2) / 3f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, toolBtnW, 22f), "+", "CS_Studio_Equip_Btn_New".Translate(), () =>
                {
                    var newTools = new List<WeaponToolEntry>(tools) { new WeaponToolEntry { label = "edge", capacities = "Cut", power = 10, cooldownTime = 2f, armorPenetration = 0.3f } };
                    Dialog_SkinEditor.SyncToolsToRawXml(equipment, newTools);
                }))
                { }
                if (tools.Count > 0 && UIHelper.DrawIconButton(new Rect(toolBtnW + Margin, y, toolBtnW, 22f), "×", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    var newTools = new List<WeaponToolEntry>(tools);
                    newTools.RemoveAt(newTools.Count - 1);
                    Dialog_SkinEditor.SyncToolsToRawXml(equipment, newTools);
                }))
                { }
                if (toolsDirty)
                {
                    MutateEquipmentWithUndo(() => Dialog_SkinEditor.SyncToolsToRawXml(equipment, tools), refreshRenderTree: false);
                }
                y += 26f;
                // ── Verbs 结构化编辑（远程武器/两用） ──
                y += 4f;
                UIHelper.DrawSectionTitle(ref y, width, "CS_Section_Verbs".Translate());
                var verbsEntry = equipment.rawXmlEntries.FirstOrDefault(e => e.tagName == "verbs");
                var verbs = WeaponVerbEntry.ParseFromXml(verbsEntry?.innerXml ?? "");
                bool verbsDirty = false;
                for (int vi = 0; vi < verbs.Count; vi++)
                {
                    int vidx = vi;
                    var verb = verbs[vidx];
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    Widgets.Label(new Rect(4, y, width - 8, 20f), $"  {"CS_Section_Verbs".Translate()} [{vidx}]");
                    y += 22f;
                    string projDisplay = string.IsNullOrWhiteSpace(verb.defaultProjectile) ? "CS_Studio_None".Translate() : verb.defaultProjectile;
                    UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_VerbProjectile".Translate(), verb.defaultProjectile, () => projDisplay,
                        () => editor.ShowProjectileDefSelector(selected => { verb.defaultProjectile = selected; verbsDirty = true; }),
                        () => { verb.defaultProjectile = string.Empty; verbsDirty = true; });
                    string vcDisplay = string.IsNullOrWhiteSpace(verb.verbClass) ? "CS_Studio_None".Translate() : verb.verbClass;
                    UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_VerbClass".Translate(), verb.verbClass, () => vcDisplay,
                        () => editor.ShowEnumSelector(new[] { "Verb_Shoot", "Verb_LaunchProjectile", "Verb_MeleeAttack", "Verb_MeleeAttackDamage" }, selected => { verb.verbClass = selected; verbsDirty = true; }),
                        () => { verb.verbClass = string.Empty; verbsDirty = true; });
                    float warmupVal = verb.warmupTime;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_VerbWarmup".Translate(), ref warmupVal, 0f, 10f, "F2", tooltip: "CS_Field_VerbWarmup_Tip".Translate());
                    if (Math.Abs(warmupVal - verb.warmupTime) > 0.001f) { verb.warmupTime = warmupVal; verbsDirty = true; }
                    float rangeVal = verb.range;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_VerbRange".Translate(), ref rangeVal, 0f, 100f, "F1", tooltip: "CS_Field_VerbRange_Tip".Translate());
                    if (Math.Abs(rangeVal - verb.range) > 0.001f) { verb.range = rangeVal; verbsDirty = true; }
                    int burstVal = verb.burstShotCount;
                    UIHelper.DrawNumericField(ref y, width, "CS_Field_VerbBurstCount".Translate(), ref burstVal, 0, 100);
                    if (burstVal != verb.burstShotCount) { verb.burstShotCount = burstVal; verbsDirty = true; }
                    float burstDelayVal = verb.burstShotDelay;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_VerbBurstDelay".Translate(), ref burstDelayVal, 0f, 2f, "F2", tooltip: "CS_Field_VerbBurstDelay_Tip".Translate());
                    if (Math.Abs(burstDelayVal - verb.burstShotDelay) > 0.001f) { verb.burstShotDelay = burstDelayVal; verbsDirty = true; }
                    string scDisplay = string.IsNullOrWhiteSpace(verb.soundCast) ? "CS_Studio_None".Translate() : verb.soundCast;
                    UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_VerbSoundCast".Translate(), verb.soundCast, () => scDisplay,
                        () => editor.ShowSoundDefSelector(selected => { verb.soundCast = selected; verbsDirty = true; }),
                        () => { verb.soundCast = string.Empty; verbsDirty = true; });
                    string stDisplay = string.IsNullOrWhiteSpace(verb.soundCastTail) ? "CS_Studio_None".Translate() : verb.soundCastTail;
                    UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_VerbSoundTail".Translate(), verb.soundCastTail, () => stDisplay,
                        () => editor.ShowSoundDefSelector(selected => { verb.soundCastTail = selected; verbsDirty = true; }),
                        () => { verb.soundCastTail = string.Empty; verbsDirty = true; });
                    float muzzleVal = verb.muzzleFlashScale;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_VerbMuzzle".Translate(), ref muzzleVal, 0f, 30f, "F1", tooltip: "CS_Field_VerbMuzzle_Tip".Translate());
                    if (Math.Abs(muzzleVal - verb.muzzleFlashScale) > 0.001f) { verb.muzzleFlashScale = muzzleVal; verbsDirty = true; }
                }
                float verbBtnW = (width - Margin * 2) / 3f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, verbBtnW, 22f), "+", "CS_Studio_Equip_Btn_New".Translate(), () =>
                {
                    var newVerbs = new List<WeaponVerbEntry>(verbs) { new WeaponVerbEntry { verbClass = "Verb_Shoot", defaultProjectile = "Bullet_Single", warmupTime = 0.5f, range = 30f, hasStandardCommand = true } };
                    Dialog_SkinEditor.SyncVerbsToRawXml(equipment, newVerbs);
                }))
                { }
                if (verbs.Count > 0 && UIHelper.DrawIconButton(new Rect(verbBtnW + Margin, y, verbBtnW, 22f), "×", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    var newVerbs = new List<WeaponVerbEntry>(verbs);
                    newVerbs.RemoveAt(newVerbs.Count - 1);
                    Dialog_SkinEditor.SyncVerbsToRawXml(equipment, newVerbs);
                }))
                { }
                if (verbsDirty)
                {
                    MutateEquipmentWithUndo(() => Dialog_SkinEditor.SyncVerbsToRawXml(equipment, verbs), refreshRenderTree: false);
                }
                y += 26f;
        }

        private void DrawBuildingSection(ref float y, float width)
        {
            bool isBuildingOrTurret = equipment.itemType == CharacterStudio.Core.EquipmentType.Building || equipment.itemType == CharacterStudio.Core.EquipmentType.Turret;
            if (!isBuildingOrTurret) return;
            if (!DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_Building".Translate(), "BuildingProperties")) return;
            string buildingSize = equipment.buildingSize ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Field_BuildingSize".Translate(), ref buildingSize);
                if (buildingSize != (equipment.buildingSize ?? string.Empty)) MutateEquipmentWithUndo(() => equipment.buildingSize = buildingSize, refreshRenderTree: false);
                string passabilityVal = equipment.passability ?? string.Empty;
                var passabilityOptions = Enum.GetNames(typeof(Traversability)).Cast<string>().ToList();
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Field_Passability".Translate(), passabilityVal, passabilityOptions, v => v, val => MutateEquipmentWithUndo(() => equipment.passability = val, refreshRenderTree: false));
                float fillPercentVal = equipment.fillPercent;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_FillPercent".Translate(), ref fillPercentVal, 0f, 1f, "F2", tooltip: "CS_Field_FillPercent_Tip".Translate());
                if (Math.Abs(fillPercentVal - equipment.fillPercent) > 0.0001f) MutateEquipmentWithUndo(() => equipment.fillPercent = fillPercentVal, refreshRenderTree: false);
                string terrainAffordDisplay = string.IsNullOrWhiteSpace(equipment.terrainAffordanceNeeded) ? "CS_Studio_None".Translate() : equipment.terrainAffordanceNeeded;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_TerrainAffordance".Translate(), equipment.terrainAffordanceNeeded, () => terrainAffordDisplay,
                    () => editor.ShowTerrainAffordanceDefSelector(selected => MutateEquipmentWithUndo(() => equipment.terrainAffordanceNeeded = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.terrainAffordanceNeeded = string.Empty, refreshRenderTree: false));
                bool blockWindVal = equipment.blockWind ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_BlockWind".Translate(), ref blockWindVal, tooltip: "CS_Field_BlockWind_Tip".Translate());
                if (blockWindVal != (equipment.blockWind ?? false)) MutateEquipmentWithUndo(() => equipment.blockWind = blockWindVal, refreshRenderTree: false);
                bool castEdgeShadowsVal = equipment.castEdgeShadows ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_CastEdgeShadows".Translate(), ref castEdgeShadowsVal, tooltip: "CS_Field_CastEdgeShadows_Tip".Translate());
                if (castEdgeShadowsVal != (equipment.castEdgeShadows ?? false)) MutateEquipmentWithUndo(() => equipment.castEdgeShadows = castEdgeShadowsVal, refreshRenderTree: false);
                string drawerTypeVal = equipment.drawerType ?? string.Empty;
                var drawerTypeOptions = Enum.GetNames(typeof(DrawerType)).Cast<string>().ToList();
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Field_DrawerType".Translate(), drawerTypeVal, drawerTypeOptions, v => v, val => MutateEquipmentWithUndo(() => equipment.drawerType = val, refreshRenderTree: false));
                bool canOverlapVal = equipment.canOverlapZones ?? false;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_CanOverlapZones".Translate(), ref canOverlapVal, tooltip: "CS_Field_CanOverlapZones_Tip".Translate());
                if (canOverlapVal != (equipment.canOverlapZones ?? false)) MutateEquipmentWithUndo(() => equipment.canOverlapZones = canOverlapVal, refreshRenderTree: false);
                bool hasInteraction = equipment.hasInteractionCell;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_HasInteractionCell".Translate(), ref hasInteraction);
                if (hasInteraction != equipment.hasInteractionCell) MutateEquipmentWithUndo(() => equipment.hasInteractionCell = hasInteraction, refreshRenderTree: false);
                if (equipment.hasInteractionCell)
                {
                    string interactionOffset = equipment.interactionCellOffset ?? string.Empty;
                    UIHelper.DrawPropertyField(ref y, width, "CS_Field_InteractionCellOffset".Translate(), ref interactionOffset);
                    if (interactionOffset != (equipment.interactionCellOffset ?? string.Empty)) MutateEquipmentWithUndo(() => equipment.interactionCellOffset = interactionOffset, refreshRenderTree: false);
                }
                // ── killedLeavings 结构化编辑 ──
                y += 2f;
                UIHelper.DrawSectionTitle(ref y, width, "CS_Field_KilledLeavings".Translate());
                equipment.killedLeavings ??= new List<CharacterEquipmentCostEntry>();
                for (int ki = equipment.killedLeavings.Count - 1; ki >= 0; ki--)
                {
                    if (equipment.killedLeavings[ki] == null || string.IsNullOrWhiteSpace(equipment.killedLeavings[ki].thingDefName))
                        equipment.killedLeavings.RemoveAt(ki);
                }
                for (int ki = 0; ki < equipment.killedLeavings.Count; ki++)
                {
                    int kidx = ki;
                    var leavingEntry = equipment.killedLeavings[ki];
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    float leavingHeaderY = y;
                    Widgets.Label(new Rect(4, y, width - 8, 20f), $"  {"CS_Field_KilledLeavings".Translate()} [{kidx}]");
                    y += 22f;
                    string leavingDisplay = string.IsNullOrWhiteSpace(leavingEntry.thingDefName)
                        ? "CS_Studio_None".Translate()
                        : Dialog_SkinEditor.GetThingDefDisplayLabel(leavingEntry.thingDefName);
                    editor.DrawSelectionPropertyButton(
                        ref y, width,
                        "CS_Field_ThingDef".Translate(),
                        leavingDisplay,
                        () => editor.ShowThingDefSelector(selected => MutateEquipmentWithUndo(() => { leavingEntry.thingDefName = selected; }, refreshRenderTree: false)));
                    int leavingCountVal = leavingEntry.count;
                    UIHelper.DrawNumericField(ref y, width, "CS_Field_Count".Translate(), ref leavingCountVal, 1, 9999);
                    if (leavingCountVal != leavingEntry.count)
                    {
                        int capturedIdx = kidx;
                        int capturedVal = leavingCountVal;
                        MutateEquipmentWithUndo(() => { equipment.killedLeavings[capturedIdx].count = capturedVal; }, refreshRenderTree: false);
                    }
                    if (UIHelper.DrawDangerButton(new Rect(width - 28f, leavingHeaderY + 2f, 24f, 22f),
                        tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            int delIdx = kidx;
                            MutateEquipmentWithUndo(() => { if (equipment.killedLeavings.Count > delIdx) equipment.killedLeavings.RemoveAt(delIdx); }, refreshRenderTree: false);
                        }))
                    { }
                }
                float klBtnW = (width - Margin * 2) / 3f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, klBtnW, 22f), "+", "CS_Studio_Equip_Btn_New".Translate(), () =>
                {
                    editor.ShowThingDefSelector(selected => MutateEquipmentWithUndo(() =>
                    {
                        equipment.killedLeavings ??= new List<CharacterEquipmentCostEntry>();
                        equipment.killedLeavings.Add(new CharacterEquipmentCostEntry { thingDefName = selected, count = 1 });
                    }, refreshRenderTree: false));
                }))
                { }
                if (equipment.killedLeavings.Count > 0 && UIHelper.DrawIconButton(new Rect(klBtnW + Margin, y, klBtnW, 22f), "×", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    MutateEquipmentWithUndo(() => { if (equipment.killedLeavings.Count > 0) equipment.killedLeavings.RemoveAt(equipment.killedLeavings.Count - 1); }, refreshRenderTree: false);
                }))
                { }
                y += 26f;
                // ── damageMultipliers 结构化编辑 ──
                y += 2f;
                UIHelper.DrawSectionTitle(ref y, width, "CS_Field_DamageMultipliers".Translate());
                equipment.damageMultipliers ??= new List<CharacterEquipmentDamageMultiplierEntry>();
                for (int di = equipment.damageMultipliers.Count - 1; di >= 0; di--)
                {
                    if (equipment.damageMultipliers[di] == null || string.IsNullOrWhiteSpace(equipment.damageMultipliers[di].damageDefName))
                        equipment.damageMultipliers.RemoveAt(di);
                }
                for (int di = 0; di < equipment.damageMultipliers.Count; di++)
                {
                    int didx = di;
                    var dmgEntry = equipment.damageMultipliers[di];
                    y += 2f;
                    Widgets.DrawLightHighlight(new Rect(0, y, width, UIHelper.RowHeight));
                    float dmgHeaderY = y;
                    Widgets.Label(new Rect(4, y, width - 8, 20f), $"  {"CS_Field_DamageMultipliers".Translate()} [{didx}]");
                    y += 22f;
                    string dmgDisplay = string.IsNullOrWhiteSpace(dmgEntry.damageDefName)
                        ? "CS_Studio_None".Translate()
                        : Dialog_SkinEditor.GetDamageDefDisplayLabel(dmgEntry.damageDefName);
                    editor.DrawSelectionPropertyButton(
                        ref y, width,
                        "CS_Field_DamageDef".Translate(),
                        dmgDisplay,
                        () => editor.ShowDamageDefSelector(selected => MutateEquipmentWithUndo(() => { dmgEntry.damageDefName = selected; }, refreshRenderTree: false)));
                    float multVal = dmgEntry.multiplier;
                    UIHelper.DrawPropertySlider(ref y, width, "CS_Field_Multiplier".Translate(), ref multVal, 0f, 10f, "F2", tooltip: "CS_Field_Multiplier_Tip".Translate());
                    if (Math.Abs(multVal - dmgEntry.multiplier) > 0.0001f)
                    {
                        int capturedIdx = didx;
                        float capturedVal = multVal;
                        MutateEquipmentWithUndo(() => { equipment.damageMultipliers[capturedIdx].multiplier = capturedVal; }, refreshRenderTree: false);
                    }
                    if (UIHelper.DrawDangerButton(new Rect(width - 28f, dmgHeaderY + 2f, 24f, 22f),
                        tooltip: "CS_Studio_Delete".Translate(), onClick: () =>
                        {
                            int delIdx = didx;
                            MutateEquipmentWithUndo(() => { if (equipment.damageMultipliers.Count > delIdx) equipment.damageMultipliers.RemoveAt(delIdx); }, refreshRenderTree: false);
                        }))
                    { }
                }
                float dmBtnW = (width - Margin * 2) / 3f;
                if (UIHelper.DrawIconButton(new Rect(0f, y, dmBtnW, 22f), "+", "CS_Studio_Equip_Btn_New".Translate(), () =>
                {
                    editor.ShowDamageDefSelector(selected => MutateEquipmentWithUndo(() =>
                    {
                        equipment.damageMultipliers ??= new List<CharacterEquipmentDamageMultiplierEntry>();
                        equipment.damageMultipliers.Add(new CharacterEquipmentDamageMultiplierEntry { damageDefName = selected, multiplier = 1f });
                    }, refreshRenderTree: false));
                }))
                { }
                if (equipment.damageMultipliers.Count > 0 && UIHelper.DrawIconButton(new Rect(dmBtnW + Margin, y, dmBtnW, 22f), "×", "CS_Studio_Btn_Delete".Translate(), () =>
                {
                    MutateEquipmentWithUndo(() => { if (equipment.damageMultipliers.Count > 0) equipment.damageMultipliers.RemoveAt(equipment.damageMultipliers.Count - 1); }, refreshRenderTree: false);
                }))
                { }
                y += 26f;
                string designationCatDisplay = string.IsNullOrWhiteSpace(equipment.designationCategory) ? "CS_Studio_None".Translate() : equipment.designationCategory;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_DesignationCategory".Translate(), equipment.designationCategory, () => designationCatDisplay,
                    () => editor.ShowDesignationCategoryDefSelector(selected => MutateEquipmentWithUndo(() => equipment.designationCategory = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.designationCategory = string.Empty, refreshRenderTree: false));
                string buildingTagsDisplay = (equipment.buildingTags != null && equipment.buildingTags.Count > 0) ? string.Join(", ", equipment.buildingTags) : "CS_Studio_None".Translate();
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_BuildingTags".Translate(), string.Join(",", equipment.buildingTags ?? new List<string>()), () => buildingTagsDisplay,
                    () => editor.ShowTagSelector(Dialog_SkinEditor.CollectExistingTags(d => d.building?.buildingTags), equipment.buildingTags ?? new List<string>(), newTags => MutateEquipmentWithUndo(() => equipment.buildingTags = newTags, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.buildingTags = new List<string>(), refreshRenderTree: false));
                float combatPowerVal = equipment.combatPower;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_CombatPower".Translate(), ref combatPowerVal, 0f, 10000f, "F0");
                if (Math.Abs(combatPowerVal - equipment.combatPower) > 0.001f) MutateEquipmentWithUndo(() => equipment.combatPower = combatPowerVal, refreshRenderTree: false);
                float roofCollapseMult = equipment.roofCollapseDamageMultiplier;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_RoofCollapseMult".Translate(), ref roofCollapseMult, 0f, 5f, "F2");
                if (Math.Abs(roofCollapseMult - equipment.roofCollapseDamageMultiplier) > 0.001f) MutateEquipmentWithUndo(() => equipment.roofCollapseDamageMultiplier = roofCollapseMult, refreshRenderTree: false);
                string destroySoundDisplay = string.IsNullOrWhiteSpace(equipment.destroySound) ? "CS_Studio_None".Translate() : equipment.destroySound;
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_DestroySound".Translate(), equipment.destroySound, () => destroySoundDisplay,
                    () => editor.ShowSoundDefSelector(selected => MutateEquipmentWithUndo(() => equipment.destroySound = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.destroySound = string.Empty, refreshRenderTree: false));
        }

        private void DrawTurretSection(ref float y, float width)
        {
            if (equipment.itemType != CharacterStudio.Core.EquipmentType.Turret) return;
            if (!DrawCollapsibleSection(ref y, width, "CS_Studio_Equip_Section_Turret".Translate(), "TurretProperties")) return;
            string turretGunDisplay = string.IsNullOrWhiteSpace(equipment.turretGunDef) ? "CS_Studio_None".Translate() : Dialog_SkinEditor.GetThingDefSelectionLabel(equipment.turretGunDef);
                UIHelper.DrawControlledReferenceField(ref y, width, "CS_Field_TurretGunDef".Translate(), equipment.turretGunDef, () => turretGunDisplay,
                    () => editor.ShowTurretGunDefSelector(selected => MutateEquipmentWithUndo(() => equipment.turretGunDef = selected, refreshRenderTree: false)),
                    () => MutateEquipmentWithUndo(() => equipment.turretGunDef = string.Empty, refreshRenderTree: false));
                float warmupTime = equipment.turretBurstWarmupTime;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_TurretBurstWarmupTime".Translate(), ref warmupTime, 0f, 30f, "F1");
                if (Math.Abs(warmupTime - equipment.turretBurstWarmupTime) > 0.001f) MutateEquipmentWithUndo(() => equipment.turretBurstWarmupTime = warmupTime, refreshRenderTree: false);
                float cooldownTime = equipment.turretBurstCooldownTime;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_TurretBurstCooldownTime".Translate(), ref cooldownTime, 0f, 30f, "F1");
                if (Math.Abs(cooldownTime - equipment.turretBurstCooldownTime) > 0.001f) MutateEquipmentWithUndo(() => equipment.turretBurstCooldownTime = cooldownTime, refreshRenderTree: false);
                float initialCooldown = equipment.turretInitialCooldownTime;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Field_TurretInitialCooldownTime".Translate(), ref initialCooldown, 0f, 30f, "F1");
                if (Math.Abs(initialCooldown - equipment.turretInitialCooldownTime) > 0.001f) MutateEquipmentWithUndo(() => equipment.turretInitialCooldownTime = initialCooldown, refreshRenderTree: false);
                bool isMechThreat = equipment.isMechClusterThreat;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Field_IsMechClusterThreat".Translate(), ref isMechThreat);
                if (isMechThreat != equipment.isMechClusterThreat) MutateEquipmentWithUndo(() => equipment.isMechClusterThreat = isMechThreat, refreshRenderTree: false);
        }
    }
}
