using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public sealed class Dialog_CharacterDefinition : Window
    {
        private readonly CharacterDefinition definition;
        private readonly Action onChanged;
        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(560f, 680f);

        public Dialog_CharacterDefinition(CharacterDefinition definition, Action onChanged)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Rect titleRect = UIHelper.DrawPanelShell(shellRect, "CS_Studio_CharacterDefinition_Title".Translate(), 0f);

            Rect scrollRect = new Rect(0f, titleRect.yMax + 8f, inRect.width, inRect.height - titleRect.yMax - 52f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, 980f);
            UIHelper.DrawContentCard(scrollRect);

            Widgets.BeginScrollView(scrollRect.ContractedBy(2f), ref scrollPos, viewRect);
            float y = 0f;
            float width = viewRect.width;

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_CharacterDefinition_Basic".Translate());
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_DefName".Translate(), ref definition.defName);
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Label".Translate(), ref definition.displayName);
            DrawSelectionButton(ref y, width, "CS_Studio_CharacterDefinition_Gender".Translate(), GetGenderLabel(definition.gender), ShowGenderSelector);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_BiologicalAge".Translate(), ref definition.biologicalAge, 0f, 999f);
            UIHelper.DrawNumericField(ref y, width, "CS_Attr_ChronologicalAge".Translate(), ref definition.chronologicalAge, 0f, 9999f);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_CharacterDefinition_Race".Translate());
            DrawSelectionButton(ref y, width, "CS_Studio_Skin_TargetRaces".Translate(), GetThingDefLabel(definition.raceDefName), ShowRaceSelector);
            DrawSelectionButton(ref y, width, "CS_Studio_XenotypeDefName".Translate(), GetXenotypeLabel(definition.xenotypeDefName), ShowXenotypeSelector);
            DrawSelectionButton(ref y, width, "CS_Studio_CharacterDefinition_BodyType".Translate(), GetBodyTypeLabel(definition.bodyTypeDefName), ShowBodyTypeSelector);
            DrawSelectionButton(ref y, width, "CS_Studio_CharacterDefinition_HeadType".Translate(), GetHeadTypeLabel(definition.headTypeDefName), ShowHeadTypeSelector);
            DrawSelectionButton(ref y, width, "CS_Studio_CharacterDefinition_Hair".Translate(), GetHairLabel(definition.hairDefName), ShowHairSelector);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_CharacterDefinition_Backstory".Translate());
            DrawSelectionButton(ref y, width, "CS_Studio_CharacterDefinition_Childhood".Translate(), GetBackstoryLabel(definition.childhoodBackstoryDefName), () => ShowBackstorySelector(true));
            DrawSelectionButton(ref y, width, "CS_Studio_CharacterDefinition_Adulthood".Translate(), GetBackstoryLabel(definition.adulthoodBackstoryDefName), () => ShowBackstorySelector(false));

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_CharacterDefinition_Traits".Translate());
            DrawListSummary(ref y, width, "CS_Studio_CharacterDefinition_TraitsSummary".Translate(definition.traitDefNames.Count), ShowTraitSelector);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_CharacterDefinition_Skills".Translate());
            DrawSkillsEditor(ref y, width);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_CharacterDefinition_Apparel".Translate());
            DrawListSummary(ref y, width, "CS_Studio_CharacterDefinition_ApparelSummary".Translate(definition.startingApparelDefNames.Count), ShowApparelSelector);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_RuntimeTriggers_Section".Translate());
            DrawListSummary(ref y, width, "CS_Studio_RuntimeTriggers_Summary".Translate(definition.runtimeTriggers?.Count ?? 0), ShowRuntimeTriggersEditor);

            Widgets.EndScrollView();

            float buttonY = inRect.height - 36f;
            if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f - 60f, buttonY, 120f, 28f), "CS_Studio_Btn_OK".Translate(), accent: true))
            {
                onChanged();
                Close();
            }
        }

        private void DrawSelectionButton(ref float y, float width, string label, string value, Action onClick)
        {
            UIHelper.DrawPropertyFieldWithButton(ref y, width, label, string.IsNullOrWhiteSpace(value) ? "CS_Studio_None".Translate() : value, onClick);
        }

        private void DrawListSummary(ref float y, float width, string label, Action onClick)
        {
            Rect rect = new Rect(0f, y, width, 28f);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 120f, 24f), label);
            if (UIHelper.DrawToolbarButton(new Rect(rect.x + rect.width - 120f, rect.y, 120f, 24f), "CS_Studio_Manage".Translate(), accent: true))
            {
                onClick();
            }
            y += 32f;
        }

        private void DrawSkillsEditor(ref float y, float width)
        {
            definition.skills ??= new List<CharacterSkillEntry>();
            foreach (SkillDef skillDef in DefDatabase<SkillDef>.AllDefsListForReading.OrderBy(static def => def.label ?? def.defName))
            {
                CharacterSkillEntry entry = definition.skills.FirstOrDefault(existing => string.Equals(existing.skillDefName, skillDef.defName, StringComparison.OrdinalIgnoreCase))
                    ?? new CharacterSkillEntry { skillDefName = skillDef.defName };
                if (!definition.skills.Contains(entry))
                {
                    definition.skills.Add(entry);
                }

                Rect row = new Rect(0f, y, width, 24f);
                Widgets.Label(new Rect(row.x, row.y, 120f, 24f), skillDef.label ?? skillDef.defName);
                int level = entry.level;
                UIHelper.DrawNumericField<int>(ref y, width - 110f, string.Empty, ref level, 0, 20);
                entry.level = level;

                if (UIHelper.DrawToolbarButton(new Rect(width - 100f, row.y, 100f, 24f), GetPassionLabel(entry.passion), accent: entry.passion != Passion.None))
                {
                    Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(Passion)).Cast<Passion>().Select(passion =>
                        new FloatMenuOption(GetPassionLabel(passion), () => entry.passion = passion)).ToList()));
                }
                y = row.yMax + 4f;
            }
        }

        private void ShowGenderSelector()
        {
            Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(Gender)).Cast<Gender>()
                .Select(gender => new FloatMenuOption(GetGenderLabel(gender), () => definition.gender = gender)).ToList()));
        }

        private void ShowRaceSelector()
        {
            List<FloatMenuOption> options = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(static def => def.race != null && def.race.Humanlike)
                .OrderBy(static def => def.label ?? def.defName)
                .Select(def => new FloatMenuOption($"{(def.label ?? def.defName)} ({def.defName})", () => definition.raceDefName = def.defName))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowXenotypeSelector()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () => definition.xenotypeDefName = string.Empty)
            };
            options.AddRange(DefDatabase<XenotypeDef>.AllDefsListForReading
                .OrderBy(static def => def.label ?? def.defName)
                .Select(def => new FloatMenuOption($"{(def.label ?? def.defName)} ({def.defName})", () => definition.xenotypeDefName = def.defName)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowBodyTypeSelector()
        {
            Find.WindowStack.Add(new FloatMenu(DefDatabase<BodyTypeDef>.AllDefsListForReading
                .OrderBy(static def => def.label ?? def.defName)
                .Select(def => new FloatMenuOption($"{(def.label ?? def.defName)} ({def.defName})", () => definition.bodyTypeDefName = def.defName)).ToList()));
        }

        private void ShowHeadTypeSelector()
        {
            Find.WindowStack.Add(new FloatMenu(DefDatabase<HeadTypeDef>.AllDefsListForReading
                .OrderBy(static def => def.label ?? def.defName)
                .Select(def => new FloatMenuOption($"{(def.label ?? def.defName)} ({def.defName})", () => definition.headTypeDefName = def.defName)).ToList()));
        }

        private void ShowHairSelector()
        {
            Find.WindowStack.Add(new FloatMenu(DefDatabase<HairDef>.AllDefsListForReading
                .OrderBy(static def => def.label ?? def.defName)
                .Select(def => new FloatMenuOption($"{(def.label ?? def.defName)} ({def.defName})", () => definition.hairDefName = def.defName)).ToList()));
        }

        private void ShowBackstorySelector(bool childhood)
        {
            Find.WindowStack.Add(new FloatMenu(DefDatabase<BackstoryDef>.AllDefsListForReading
                .OrderBy(static def => def.title ?? def.defName)
                .Select(def => new FloatMenuOption($"{(def.title ?? def.defName)} ({def.defName})", () =>
                {
                    if (childhood)
                    {
                        definition.childhoodBackstoryDefName = def.defName;
                    }
                    else
                    {
                        definition.adulthoodBackstoryDefName = def.defName;
                    }
                })).ToList()));
        }

        private void ShowTraitSelector()
        {
            List<FloatMenuOption> options = DefDatabase<TraitDef>.AllDefsListForReading
                .OrderBy(static def => def.degreeDatas?.FirstOrDefault()?.label ?? def.defName)
                .Select(def => new FloatMenuOption($"{(def.degreeDatas?.FirstOrDefault()?.label ?? def.defName)} ({def.defName})", () => ToggleListValue(definition.traitDefNames, def.defName)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowApparelSelector()
        {
            List<FloatMenuOption> options = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(static def => def.apparel != null)
                .OrderBy(static def => def.label ?? def.defName)
                .Select(def => new FloatMenuOption($"{(def.label ?? def.defName)} ({def.defName})", () => ToggleListValue(definition.startingApparelDefNames, def.defName)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowRuntimeTriggersEditor()
        {
            Find.WindowStack.Add(new Dialog_RuntimeTriggers(definition, onChanged));
        }

        private static void ToggleListValue(List<string> values, string value)
        {
            values ??= new List<string>();
            if (values.Contains(value))
            {
                values.Remove(value);
            }
            else
            {
                values.Add(value);
            }
        }

        private static string GetThingDefLabel(string defName)
        {
            ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def == null ? defName : $"{(def.label ?? def.defName)} ({def.defName})";
        }

        private static string GetXenotypeLabel(string defName)
        {
            XenotypeDef? def = DefDatabase<XenotypeDef>.GetNamedSilentFail(defName);
            return def == null ? defName : $"{(def.label ?? def.defName)} ({def.defName})";
        }

        private static string GetBodyTypeLabel(string defName)
        {
            BodyTypeDef? def = DefDatabase<BodyTypeDef>.GetNamedSilentFail(defName);
            return def == null ? defName : $"{(def.label ?? def.defName)} ({def.defName})";
        }

        private static string GetHeadTypeLabel(string defName)
        {
            HeadTypeDef? def = DefDatabase<HeadTypeDef>.GetNamedSilentFail(defName);
            return def == null ? defName : $"{(def.label ?? def.defName)} ({def.defName})";
        }

        private static string GetHairLabel(string defName)
        {
            HairDef? def = DefDatabase<HairDef>.GetNamedSilentFail(defName);
            return def == null ? defName : $"{(def.label ?? def.defName)} ({def.defName})";
        }

        private static string GetBackstoryLabel(string defName)
        {
            BackstoryDef? def = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            return def == null ? defName : $"{(def.title ?? def.defName)} ({def.defName})";
        }

        private static string GetGenderLabel(Gender gender)
        {
            return gender switch
            {
                Gender.Male => "CS_Studio_Gender_Male".Translate().ToString(),
                Gender.Female => "CS_Studio_Gender_Female".Translate().ToString(),
                _ => "CS_Studio_Gender_None".Translate().ToString()
            };
        }

        private static string GetPassionLabel(Passion passion)
        {
            return passion switch
            {
                Passion.Minor => "CS_Studio_Passion_Minor".Translate().ToString(),
                Passion.Major => "CS_Studio_Passion_Major".Translate().ToString(),
                _ => "CS_Studio_Passion_None".Translate().ToString()
            };
        }
    }
}
