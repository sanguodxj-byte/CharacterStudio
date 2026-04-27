using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Items;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public sealed class Dialog_RuntimeTriggers : Window
    {
        private readonly CharacterDefinition definition;
        private readonly Action onChanged;
        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(760f, 700f);

        public Dialog_RuntimeTriggers(CharacterDefinition definition, Action onChanged)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
            this.definition.runtimeTriggers ??= new List<CharacterRuntimeTriggerDef>();
        }

        public override void DoWindowContents(Rect inRect)
        {
            UIHelper.DrawDialogFrame(inRect, this);

            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Rect titleRect = UIHelper.DrawPanelShell(shellRect, "CS_Studio_RuntimeTriggers_Title".Translate(), 0f);

            Rect scrollRect = new Rect(0f, titleRect.yMax + 8f, inRect.width, inRect.height - titleRect.yMax - 52f);
            float contentHeight = CalculateViewHeight(scrollRect.width - 16f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, Mathf.Max(scrollRect.height - 4f, contentHeight));
            UIHelper.DrawContentCard(scrollRect);

            Widgets.BeginScrollView(scrollRect.ContractedBy(2f), ref scrollPos, viewRect);
            float y = 0f;
            float width = viewRect.width;

            UIHelper.DrawInfoBanner(ref y, width, "CS_Studio_RuntimeTriggers_Hint".Translate(), accent: true);

            if (UIHelper.DrawToolbarButton(new Rect(0f, y, 180f, 28f), "CS_Studio_RuntimeTriggers_Add".Translate(), accent: true))
            {
                definition.runtimeTriggers.Add(CreateDefaultTrigger(definition.runtimeTriggers.Count + 1));
            }
            if (UIHelper.DrawToolbarButton(new Rect(188f, y, 180f, 28f), "CS_Studio_RuntimeTriggers_Status".Translate()))
            {
                Find.WindowStack.Add(new Dialog_RuntimeTriggerStatus());
            }
            y += 36f;

            for (int i = 0; i < definition.runtimeTriggers.Count; i++)
            {
                CharacterRuntimeTriggerDef trigger = definition.runtimeTriggers[i];
                if (trigger == null)
                {
                    continue;
                }

                DrawTriggerCard(ref y, width, trigger, i);
            }

            Widgets.EndScrollView();

            float buttonY = inRect.height - 36f;
            if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f - 60f, buttonY, 120f, 28f), "CS_Studio_Btn_OK".Translate(), accent: true))
            {
                onChanged();
                Close();
            }
        }

        private void DrawTriggerCard(ref float y, float width, CharacterRuntimeTriggerDef trigger, int index)
        {
            float cardHeight = CalculateTriggerCardHeight(trigger);
            Rect cardRect = UIHelper.DrawSectionCard(ref y, width, $"{index + 1}. {GetTriggerHeader(trigger, index)}", cardHeight, accent: trigger.enabled);
            float sectionY = cardRect.y;

            string label = trigger.label ?? string.Empty;
            UIHelper.DrawPropertyField(ref sectionY, cardRect.width, "CS_Studio_RuntimeTriggers_Label".Translate(), ref label);
            trigger.label = label;

            UIHelper.DrawPropertyCheckbox(ref sectionY, cardRect.width, "CS_Studio_RuntimeTriggers_Enabled".Translate(), ref trigger.enabled);
            UIHelper.DrawPropertyDropdown(ref sectionY, cardRect.width, "CS_Studio_RuntimeTriggers_ConditionLogic".Translate(), trigger.conditionLogic,
                (CharacterSpawnConditionLogic[])Enum.GetValues(typeof(CharacterSpawnConditionLogic)),
                GetConditionLogicLabel,
                value => trigger.conditionLogic = value);

            int evaluationIntervalTicks = trigger.evaluationIntervalTicks;
            UIHelper.DrawNumericField(ref sectionY, cardRect.width, "CS_Studio_RuntimeTriggers_EvaluationInterval".Translate(), ref evaluationIntervalTicks, 60, 600000);
            trigger.evaluationIntervalTicks = evaluationIntervalTicks;

            int cooldownTicks = trigger.cooldownTicks;
            UIHelper.DrawNumericField(ref sectionY, cardRect.width, "CS_Studio_RuntimeTriggers_CooldownTicks".Translate(), ref cooldownTicks, 0, 6000000);
            trigger.cooldownTicks = cooldownTicks;

            UIHelper.DrawPropertyCheckbox(ref sectionY, cardRect.width, "CS_Studio_RuntimeTriggers_RequireHomeMap".Translate(), ref trigger.requirePlayerHomeMap);
            UIHelper.DrawPropertyCheckbox(ref sectionY, cardRect.width, "CS_Studio_RuntimeTriggers_SpawnNearColonist".Translate(), ref trigger.spawnNearColonist);
            UIHelper.DrawPropertyCheckbox(ref sectionY, cardRect.width, "CS_Studio_RuntimeTriggers_OncePerGame".Translate(), ref trigger.fireOncePerGame);
            UIHelper.DrawPropertyCheckbox(ref sectionY, cardRect.width, "CS_Studio_RuntimeTriggers_OncePerMap".Translate(), ref trigger.fireOncePerMap);

            DrawConditionsEditor(ref sectionY, cardRect.width, trigger);

            float buttonWidth = 150f;
            Rect editSpawnRect = new Rect(0f, sectionY + 2f, buttonWidth, 28f);
            if (UIHelper.DrawToolbarButton(editSpawnRect, "CS_Studio_RuntimeTriggers_EditSpawn".Translate(), accent: true))
            {
                Find.WindowStack.Add(new Dialog_SpawnCharacter(trigger.spawnSettings, settings =>
                {
                    trigger.spawnSettings = settings?.Clone() ?? new CharacterSpawnSettings();
                }));
            }

            Rect removeRect = new Rect(editSpawnRect.xMax + 8f, sectionY + 2f, 120f, 28f);
            if (UIHelper.DrawToolbarButton(removeRect, "CS_Studio_RuntimeTriggers_Remove".Translate()))
            {
                definition.runtimeTriggers.RemoveAt(index);
                return;
            }
        }

        private void DrawConditionsEditor(ref float y, float width, CharacterRuntimeTriggerDef trigger)
        {
            trigger.requiredConditions ??= new List<CharacterRuntimeTriggerCondition>();

            Widgets.Label(new Rect(0f, y, width, 24f), "CS_Studio_RuntimeTriggers_Conditions".Translate());
            y += 26f;

            for (int i = 0; i < trigger.requiredConditions.Count; i++)
            {
                CharacterRuntimeTriggerCondition condition = trigger.requiredConditions[i];
                float rowHeight = RequiresSecondaryValueRow(condition) ? 56f : 28f;
                Rect rowRect = new Rect(0f, y, width, rowHeight);
                float typeWidth = 220f;
                if (UIHelper.DrawSelectionButton(new Rect(rowRect.x, rowRect.y, typeWidth, 24f), GetConditionLabel(condition.conditionType)))
                {
                    Find.WindowStack.Add(new Dialog_OptionBrowser<CharacterRuntimeTriggerConditionType>(
                        "CS_Studio_RuntimeTriggers_ConditionBrowser_Title".Translate().ToString(),
                        Enum.GetValues(typeof(CharacterRuntimeTriggerConditionType)).Cast<CharacterRuntimeTriggerConditionType>(),
                        type => condition.conditionType = type,
                        GetConditionLabel));
                }

                string valueLabel = GetConditionValueLabel(condition);
                if (UIHelper.DrawSelectionButton(new Rect(rowRect.x + typeWidth + 8f, rowRect.y, width - typeWidth - 136f, 24f), valueLabel))
                {
                    OpenConditionValueEditor(condition);
                }

                if (RequiresSecondaryValueRow(condition))
                {
                    DrawConditionSecondaryControls(condition, rowRect, width);
                }

                if (UIHelper.DrawToolbarButton(new Rect(rowRect.x + width - 100f, rowRect.y, 100f, 24f), "CS_Studio_RuntimeTriggers_RemoveCondition".Translate()))
                {
                    trigger.requiredConditions.RemoveAt(i);
                    break;
                }

                y += rowHeight + 4f;
            }

            if (UIHelper.DrawToolbarButton(new Rect(0f, y, 160f, 24f), "CS_Studio_RuntimeTriggers_AddCondition".Translate()))
            {
                trigger.requiredConditions.Add(new CharacterRuntimeTriggerCondition());
            }
            y += 32f;
        }

        private void OpenConditionValueEditor(CharacterRuntimeTriggerCondition condition)
        {
            switch (condition.conditionType)
            {
                case CharacterRuntimeTriggerConditionType.ColonyThingDefCount:
                    Find.WindowStack.Add(new Dialog_DefBrowser<ThingDef>(
                        "CS_Studio_RuntimeTriggers_Condition_ColonyThingDefCount".Translate().ToString(),
                        DefDatabase<ThingDef>.AllDefsListForReading.OrderBy(static def => def.label ?? def.defName),
                        def => condition.thingDefName = def.defName,
                        def => $"{(def.label ?? def.defName)} ({def.defName})",
                        def => def.description ?? string.Empty));
                    return;

                case CharacterRuntimeTriggerConditionType.ColonyThingCategoryCount:
                    Find.WindowStack.Add(new Dialog_DefBrowser<ThingCategoryDef>(
                        "CS_Studio_RuntimeTriggers_Condition_ColonyThingCategoryCount".Translate().ToString(),
                        DefDatabase<ThingCategoryDef>.AllDefsListForReading.OrderBy(static def => def.label ?? def.defName),
                        def => condition.categoryDefName = def.defName,
                        def => $"{(def.label ?? def.defName)} ({def.defName})",
                        _ => string.Empty));
                    return;

                case CharacterRuntimeTriggerConditionType.ColonyTradeTagCount:
                    Find.WindowStack.Add(new Dialog_EditStringValue(
                        "CS_Studio_RuntimeTriggers_Condition_ColonyTradeTagCount".Translate(),
                        condition.tradeTag,
                        value => condition.tradeTag = value?.Trim() ?? string.Empty));
                    return;

                case CharacterRuntimeTriggerConditionType.ColonyFoodTypeCount:
                    Find.WindowStack.Add(new Dialog_OptionBrowser<FoodTypeFlags>(
                        "CS_Studio_RuntimeTriggers_Condition_ColonyFoodTypeCount".Translate().ToString(),
                        Enum.GetValues(typeof(FoodTypeFlags)).Cast<FoodTypeFlags>().Where(static value => value != FoodTypeFlags.None),
                        value => condition.foodType = value,
                        GetFoodTypeLabel));
                    return;

                case CharacterRuntimeTriggerConditionType.ColonyFoodPreferabilityCount:
                    Find.WindowStack.Add(new Dialog_OptionBrowser<FoodPreferability>(
                        "CS_Studio_RuntimeTriggers_Condition_ColonyFoodPreferabilityCount".Translate().ToString(),
                        Enum.GetValues(typeof(FoodPreferability)).Cast<FoodPreferability>().Where(static value => value != FoodPreferability.Undefined),
                        value => condition.foodPreferability = value,
                        GetFoodPreferabilityLabel));
                    return;

                case CharacterRuntimeTriggerConditionType.ColonistCountAtLeast:
                    Find.WindowStack.Add(new Dialog_EditNumericValue(
                        "CS_Studio_RuntimeTriggers_Condition_ColonistCountAtLeast".Translate(),
                        condition.minColonistCount,
                        value => condition.minColonistCount = Math.Max(1, value),
                        minimum: 1));
                    return;

                case CharacterRuntimeTriggerConditionType.DaysPassedAtLeast:
                    Find.WindowStack.Add(new Dialog_EditFloatValue(
                        "CS_Studio_RuntimeTriggers_Condition_DaysPassedAtLeast".Translate(),
                        condition.minDaysPassed,
                        value => condition.minDaysPassed = Math.Max(0f, value),
                        minimum: 0f));
                    return;

                case CharacterRuntimeTriggerConditionType.Always:
                default:
                    return;
            }
        }

        private static CharacterRuntimeTriggerDef CreateDefaultTrigger(int index)
        {
            return new CharacterRuntimeTriggerDef
            {
                label = "CS_Studio_RuntimeTriggers_DefaultLabel".Translate(index),
                enabled = true,
                conditionLogic = CharacterSpawnConditionLogic.All,
                evaluationIntervalTicks = 250,
                cooldownTicks = 60000,
                requirePlayerHomeMap = true,
                spawnNearColonist = true,
                spawnSettings = new CharacterSpawnSettings
                {
                    arrivalDefName = "CS_SpawnArrival_Standing",
                    arrivalMode = SummonArrivalMode.Standing,
                    spawnEventDefName = "CS_SpawnEvent_Message",
                    spawnEvent = SummonSpawnEventMode.Message,
                    spawnAnimationDefName = "CS_SpawnAnimation_None",
                    spawnAnimation = SummonSpawnAnimationMode.None,
                    spawnAnimationScale = 1f
                },
                requiredConditions = new List<CharacterRuntimeTriggerCondition>
                {
                    new CharacterRuntimeTriggerCondition()
                }
            };
        }

        private static string GetTriggerHeader(CharacterRuntimeTriggerDef trigger, int index)
        {
            return string.IsNullOrWhiteSpace(trigger.label)
                ? "CS_Studio_RuntimeTriggers_DefaultLabel".Translate(index + 1)
                : trigger.label;
        }

        private float CalculateViewHeight(float width)
        {
            float y = 0f;
            y += GetInfoBannerHeight(width, "CS_Studio_RuntimeTriggers_Hint".Translate()) + 6f;
            y += 36f;

            foreach (CharacterRuntimeTriggerDef trigger in definition.runtimeTriggers)
            {
                if (trigger == null)
                {
                    continue;
                }

                y += CalculateTriggerCardHeight(trigger) + 8f;
            }

            return y + 12f;
        }

        private static float CalculateTriggerCardHeight(CharacterRuntimeTriggerDef trigger)
        {
            trigger.requiredConditions ??= new List<CharacterRuntimeTriggerCondition>();

            float height = 248f;
            for (int i = 1; i < trigger.requiredConditions.Count; i++)
            {
                CharacterRuntimeTriggerCondition condition = trigger.requiredConditions[i];
                float rowHeight = RequiresSecondaryValueRow(condition) ? 56f : 28f;
                height += rowHeight - 28f + 4f;
            }

            if (trigger.requiredConditions.Count == 0)
            {
                height -= 4f;
            }

            return height;
        }

        private static float GetInfoBannerHeight(float width, string text)
        {
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float textHeight = Text.CalcHeight(text, Mathf.Max(40f, width - 16f));
            Text.Font = oldFont;
            return Mathf.Max(34f, textHeight + 12f);
        }

        private static string GetConditionLabel(CharacterRuntimeTriggerConditionType conditionType)
        {
            return $"CS_Studio_RuntimeTriggers_Condition_{conditionType}".Translate();
        }

        private static string GetConditionLogicLabel(CharacterSpawnConditionLogic logic)
        {
            return logic switch
            {
                CharacterSpawnConditionLogic.All => "CS_Studio_RuntimeTriggers_ConditionLogic_All".Translate(),
                CharacterSpawnConditionLogic.Any => "CS_Studio_RuntimeTriggers_ConditionLogic_Any".Translate(),
                CharacterSpawnConditionLogic.Not => "CS_Studio_RuntimeTriggers_ConditionLogic_Not".Translate(),
                _ => logic.ToString()
            };
        }

        private static string GetConditionValueLabel(CharacterRuntimeTriggerCondition condition)
        {
            return condition.conditionType switch
            {
                CharacterRuntimeTriggerConditionType.ColonyThingDefCount => $"{condition.thingDefName} x{Math.Max(1, condition.minCount)}",
                CharacterRuntimeTriggerConditionType.ColonyThingCategoryCount => $"{condition.categoryDefName} x{Math.Max(1, condition.minCount)}",
                CharacterRuntimeTriggerConditionType.ColonyTradeTagCount => $"{condition.tradeTag} x{Math.Max(1, condition.minCount)}",
                CharacterRuntimeTriggerConditionType.ColonyFoodTypeCount => $"{GetFoodTypeLabel(condition.foodType)} x{Math.Max(1, condition.minCount)}",
                CharacterRuntimeTriggerConditionType.ColonyFoodPreferabilityCount => $"{GetFoodPreferabilityLabel(condition.foodPreferability)} x{Math.Max(1, condition.minCount)}",
                CharacterRuntimeTriggerConditionType.ColonistCountAtLeast => $">= {Math.Max(1, condition.minColonistCount)}",
                CharacterRuntimeTriggerConditionType.DaysPassedAtLeast => $">= {Math.Max(0f, condition.minDaysPassed):0.##}",
                _ => "CS_Studio_RuntimeTriggers_ConditionValue_Default".Translate()
            };
        }

        private static string GetFoodTypeLabel(FoodTypeFlags foodType)
        {
            return foodType switch
            {
                FoodTypeFlags.Meal => "CS_Studio_RuntimeTriggers_FoodType_Meal".Translate(),
                FoodTypeFlags.Meat => "CS_Studio_RuntimeTriggers_FoodType_Meat".Translate(),
                FoodTypeFlags.VegetableOrFruit => "CS_Studio_RuntimeTriggers_FoodType_VegetableOrFruit".Translate(),
                FoodTypeFlags.Seed => "CS_Studio_RuntimeTriggers_FoodType_Seed".Translate(),
                FoodTypeFlags.Corpse => "CS_Studio_RuntimeTriggers_FoodType_Corpse".Translate(),
                _ => foodType.ToString()
            };
        }

        private static string GetFoodPreferabilityLabel(FoodPreferability foodPreferability)
        {
            return foodPreferability switch
            {
                FoodPreferability.DesperateOnly => "CS_Studio_RuntimeTriggers_FoodPreferability_DesperateOnly".Translate(),
                FoodPreferability.RawBad => "CS_Studio_RuntimeTriggers_FoodPreferability_RawBad".Translate(),
                FoodPreferability.RawTasty => "CS_Studio_RuntimeTriggers_FoodPreferability_RawTasty".Translate(),
                FoodPreferability.NeverForNutrition => "CS_Studio_RuntimeTriggers_FoodPreferability_NeverForNutrition".Translate(),
                FoodPreferability.MealAwful => "CS_Studio_RuntimeTriggers_FoodPreferability_MealAwful".Translate(),
                FoodPreferability.MealSimple => "CS_Studio_RuntimeTriggers_FoodPreferability_MealSimple".Translate(),
                FoodPreferability.MealFine => "CS_Studio_RuntimeTriggers_FoodPreferability_MealFine".Translate(),
                FoodPreferability.MealLavish => "CS_Studio_RuntimeTriggers_FoodPreferability_MealLavish".Translate(),
                _ => foodPreferability.ToString()
            };
        }

        private static bool RequiresSecondaryValueRow(CharacterRuntimeTriggerCondition condition)
        {
            return condition.conditionType == CharacterRuntimeTriggerConditionType.ColonyThingDefCount
                || condition.conditionType == CharacterRuntimeTriggerConditionType.ColonyThingCategoryCount
                || condition.conditionType == CharacterRuntimeTriggerConditionType.ColonyTradeTagCount
                || condition.conditionType == CharacterRuntimeTriggerConditionType.ColonyFoodTypeCount
                || condition.conditionType == CharacterRuntimeTriggerConditionType.ColonyFoodPreferabilityCount;
        }

        private static void DrawConditionSecondaryControls(CharacterRuntimeTriggerCondition condition, Rect rowRect, float width)
        {
            float controlsY = rowRect.y + 28f;
            Rect countLabelRect = new Rect(rowRect.x, controlsY, 70f, 24f);
            Widgets.Label(countLabelRect, "CS_Studio_RuntimeTriggers_MinCount".Translate());

            int minCount = condition.minCount;
            string countBuffer = minCount.ToString();
            Widgets.TextFieldNumeric(new Rect(countLabelRect.xMax + 4f, controlsY, 96f, 24f), ref minCount, ref countBuffer, 1, 99999999);
            condition.minCount = Math.Max(1, minCount);

            Rect stackToggleRect = new Rect(countLabelRect.xMax + 110f, controlsY, Mathf.Min(260f, width - (countLabelRect.xMax + 110f)), 24f);
            Widgets.CheckboxLabeled(stackToggleRect, "CS_Studio_RuntimeTriggers_CountStackCount".Translate(), ref condition.countStackCount);
        }

        private sealed class Dialog_EditNumericValue : Window
        {
            private readonly string title;
            private readonly Action<int> onConfirm;
            private readonly int minimum;
            private int value;

            public override Vector2 InitialSize => new Vector2(320f, 140f);

            public Dialog_EditNumericValue(string title, int initialValue, Action<int> onConfirm, int minimum)
            {
                this.title = title;
                this.onConfirm = onConfirm;
                this.minimum = minimum;
                value = initialValue;
                doCloseX = true;
                absorbInputAroundWindow = true;
                closeOnClickedOutside = false;
                draggable = true;
            }

            public override void DoWindowContents(Rect inRect)
            {
                UIHelper.DrawDialogFrame(inRect, this);

                float y = 0f;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f), title);
                y += 30f;
                UIHelper.DrawNumericField(ref y, inRect.width, "CS_AttrBuff_Value".Translate(), ref value, minimum, 999999f);
                if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f - 60f, inRect.height - 34f, 120f, 28f), "CS_Studio_Btn_OK".Translate(), accent: true))
                {
                    onConfirm(Math.Max(minimum, value));
                    Close();
                }
            }
        }

        private sealed class Dialog_EditFloatValue : Window
        {
            private readonly string title;
            private readonly Action<float> onConfirm;
            private readonly float minimum;
            private float value;

            public override Vector2 InitialSize => new Vector2(320f, 140f);

            public Dialog_EditFloatValue(string title, float initialValue, Action<float> onConfirm, float minimum)
            {
                this.title = title;
                this.onConfirm = onConfirm;
                this.minimum = minimum;
                value = initialValue;
                doCloseX = true;
                absorbInputAroundWindow = true;
                closeOnClickedOutside = false;
                draggable = true;
            }

            public override void DoWindowContents(Rect inRect)
            {
                UIHelper.DrawDialogFrame(inRect, this);

                float y = 0f;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f), title);
                y += 30f;
                UIHelper.DrawNumericField(ref y, inRect.width, "CS_AttrBuff_Value".Translate(), ref value, minimum, 999999f);
                if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f - 60f, inRect.height - 34f, 120f, 28f), "CS_Studio_Btn_OK".Translate(), accent: true))
                {
                    onConfirm(Math.Max(minimum, value));
                    Close();
                }
            }
        }

        private sealed class Dialog_EditStringValue : Window
        {
            private readonly string title;
            private readonly Action<string> onConfirm;
            private string value;

            public override Vector2 InitialSize => new Vector2(420f, 140f);

            public Dialog_EditStringValue(string title, string initialValue, Action<string> onConfirm)
            {
                this.title = title;
                this.onConfirm = onConfirm;
                value = initialValue ?? string.Empty;
                doCloseX = true;
                absorbInputAroundWindow = true;
                closeOnClickedOutside = false;
                draggable = true;
            }

            public override void DoWindowContents(Rect inRect)
            {
                UIHelper.DrawDialogFrame(inRect, this);

                float y = 0f;
                Widgets.Label(new Rect(0f, y, inRect.width, 24f), title);
                y += 30f;
                UIHelper.DrawPropertyField(ref y, inRect.width, "CS_Studio_RuntimeTriggers_StringValue".Translate(), ref value);
                if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f - 60f, inRect.height - 34f, 120f, 28f), "CS_Studio_Btn_OK".Translate(), accent: true))
                {
                    onConfirm(value);
                    Close();
                }
            }
        }
    }
}
