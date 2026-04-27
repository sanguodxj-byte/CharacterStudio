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
    public sealed class Dialog_SpawnCharacter : Window
    {
        private readonly Action<CharacterSpawnSettings> onConfirm;
        private readonly Action<CharacterSpawnSettings>? onTriggerConfirm;
        private readonly Action<bool>? onBusyStateChanged;
        private CharacterSpawnArrivalDef? arrivalDef;
        private CharacterSpawnEventDef? spawnEventDef;
        private CharacterSpawnAnimationDef? spawnAnimationDef;
        private string eventMessageText = string.Empty;
        private string eventLetterTitle = string.Empty;
        private float spawnAnimationScale;
        private Map? cachedMap;
        private int cachedMapThingCount = -1;
        private List<CharacterSpawnArrivalDef> cachedArrivalOptions = new List<CharacterSpawnArrivalDef>();
        private List<CharacterSpawnEventDef> cachedEventOptions = new List<CharacterSpawnEventDef>();
        private List<CharacterSpawnAnimationDef> cachedAnimationOptions = new List<CharacterSpawnAnimationDef>();

        public override Vector2 InitialSize => new Vector2(520f, 440f);

        public Dialog_SpawnCharacter(
            CharacterSpawnSettings? initialSettings,
            Action<CharacterSpawnSettings> onConfirm,
            Action<CharacterSpawnSettings>? onTriggerConfirm = null,
            Action<bool>? onBusyStateChanged = null)
        {
            this.onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));
            this.onTriggerConfirm = onTriggerConfirm;
            this.onBusyStateChanged = onBusyStateChanged;
            CharacterSpawnSettings settings = initialSettings?.Clone() ?? new CharacterSpawnSettings();
            arrivalDef = CharacterSpawnUtility.ResolveArrivalDef(settings);
            spawnEventDef = CharacterSpawnUtility.ResolveSpawnEventDef(settings);
            spawnAnimationDef = CharacterSpawnUtility.ResolveSpawnAnimationDef(settings);
            eventMessageText = settings.eventMessageText ?? string.Empty;
            eventLetterTitle = settings.eventLetterTitle ?? string.Empty;
            spawnAnimationScale = settings.spawnAnimationScale;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            onBusyStateChanged?.Invoke(true);
            RefreshCachedOptions(force: true);
        }

        public override void PreClose()
        {
            onBusyStateChanged?.Invoke(false);
            base.PreClose();
        }

        private static List<CharacterSpawnArrivalDef> GetArrivalOptions()
        {
            return DefDatabase<CharacterSpawnArrivalDef>.AllDefsListForReading
                .Where(def => def != null && def.enabled)
                .OrderBy(def => def.sortOrder)
                .ThenBy(def => def.label ?? def.defName)
                .ToList();
        }

        private static List<CharacterSpawnEventDef> GetSpawnEventOptions()
        {
            return DefDatabase<CharacterSpawnEventDef>.AllDefsListForReading
                .Where(def => def != null && def.enabled)
                .OrderBy(def => def.sortOrder)
                .ThenBy(def => def.label ?? def.defName)
                .ToList();
        }

        private static List<CharacterSpawnAnimationDef> GetSpawnAnimationOptions()
        {
            return DefDatabase<CharacterSpawnAnimationDef>.AllDefsListForReading
                .Where(def => def != null && def.enabled)
                .OrderBy(def => def.sortOrder)
                .ThenBy(def => def.label ?? def.defName)
                .ToList();
        }

        public override void DoWindowContents(Rect inRect)
        {
            UIHelper.DrawDialogFrame(inRect, this);

            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Widgets.DrawBoxSolid(shellRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(shellRect, 1);
            GUI.color = Color.white;

            float y = 8f;
            float width = inRect.width;
            RefreshCachedOptions(force: false);

            List<CharacterSpawnArrivalDef> arrivalOptions = cachedArrivalOptions;
            List<CharacterSpawnEventDef> eventOptions = cachedEventOptions;
            List<CharacterSpawnAnimationDef> animationOptions = cachedAnimationOptions;

            arrivalDef ??= arrivalOptions.FirstOrDefault();
            spawnEventDef ??= eventOptions.FirstOrDefault();
            spawnAnimationDef ??= animationOptions.FirstOrDefault();

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_SpawnNewPawnSection".Translate());
            DrawDefSelector(ref y, width, "CS_Studio_Export_RoleCardArrival".Translate(), arrivalDef, arrivalOptions, def =>
            {
                arrivalDef = def;
            });
            DrawConditionHint(ref y, width, arrivalDef);

            DrawDefSelector(ref y, width, "CS_Studio_Export_RoleCardEvent".Translate(), spawnEventDef, eventOptions, def =>
            {
                spawnEventDef = def;
            });
            DrawConditionHint(ref y, width, spawnEventDef);

            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_SpawnEvent_MessageText".Translate(), ref eventMessageText,
                "CS_Studio_SpawnEvent_MessageText_Hint".Translate());
            if ((spawnEventDef?.mode ?? SummonSpawnEventMode.None) == SummonSpawnEventMode.PositiveLetter)
            {
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_SpawnEvent_LetterTitle".Translate(), ref eventLetterTitle,
                    "CS_Studio_SpawnEvent_LetterTitle_Hint".Translate());
            }

            DrawDefSelector(ref y, width, "CS_Studio_Export_RoleCardAnimation".Translate(), spawnAnimationDef, animationOptions, def =>
            {
                spawnAnimationDef = def;
                spawnAnimationScale = spawnAnimationDef?.defaultScale ?? 1f;
            });
            DrawConditionHint(ref y, width, spawnAnimationDef);

            if ((spawnAnimationDef?.mode ?? SummonSpawnAnimationMode.None) != SummonSpawnAnimationMode.None)
            {
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Export_RoleCardAnimationScale".Translate(), ref spawnAnimationScale, 0.1f, 5f, "F2");
            }

            float btnWidth = 120f;
            float btnY = inRect.height - 40f;
            if (onTriggerConfirm != null)
            {
                float totalWidth = btnWidth * 3f + 16f;
                float startX = inRect.width / 2f - totalWidth / 2f;

                if (UIHelper.DrawToolbarButton(new Rect(startX, btnY, btnWidth, 28f), "CS_Studio_SpawnUseTriggerConfirm".Translate(), accent: true))
                {
                    onTriggerConfirm(BuildCurrentSettings(arrivalOptions, eventOptions, animationOptions));
                    Close();
                }

                if (UIHelper.DrawToolbarButton(new Rect(startX + btnWidth + 8f, btnY, btnWidth, 28f), "CS_Studio_SpawnNewPawnConfirm".Translate(), accent: true))
                {
                    onConfirm(BuildCurrentSettings(arrivalOptions, eventOptions, animationOptions));
                    Close();
                }

                if (UIHelper.DrawToolbarButton(new Rect(startX + (btnWidth + 8f) * 2f, btnY, btnWidth, 28f), "CS_Studio_Btn_Cancel".Translate()))
                {
                    Close();
                }
            }
            else
            {
                if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f - btnWidth - 8f, btnY, btnWidth, 28f), "CS_Studio_SpawnNewPawnConfirm".Translate(), accent: true))
                {
                    onConfirm(BuildCurrentSettings(arrivalOptions, eventOptions, animationOptions));
                    Close();
                }

                if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f + 8f, btnY, btnWidth, 28f), "CS_Studio_Btn_Cancel".Translate()))
                {
                    Close();
                }
            }
        }

        private CharacterSpawnSettings BuildCurrentSettings(
            List<CharacterSpawnArrivalDef> arrivalOptions,
            List<CharacterSpawnEventDef> eventOptions,
            List<CharacterSpawnAnimationDef> animationOptions)
        {
            CharacterSpawnArrivalDef resolvedArrival = arrivalDef ?? arrivalOptions.FirstOrDefault();
            CharacterSpawnEventDef resolvedEvent = spawnEventDef ?? eventOptions.FirstOrDefault();
            CharacterSpawnAnimationDef resolvedAnimation = spawnAnimationDef ?? animationOptions.FirstOrDefault();

            return new CharacterSpawnSettings
            {
                sourceMapForConditionCheck = Find.CurrentMap,
                arrivalDefName = resolvedArrival?.defName ?? string.Empty,
                arrivalMode = resolvedArrival?.mode ?? SummonArrivalMode.Standing,
                spawnEventDefName = resolvedEvent?.defName ?? string.Empty,
                spawnEvent = resolvedEvent?.mode ?? SummonSpawnEventMode.Message,
                eventMessageText = eventMessageText,
                eventLetterTitle = eventLetterTitle,
                spawnAnimationDefName = resolvedAnimation?.defName ?? string.Empty,
                spawnAnimation = resolvedAnimation?.mode ?? SummonSpawnAnimationMode.None,
                spawnAnimationScale = spawnAnimationScale
            };
        }

        private void RefreshCachedOptions(bool force)
        {
            Map? currentMap = Find.CurrentMap;
            int currentThingCount = currentMap?.listerThings?.AllThings?.Count ?? -1;
            bool mapChanged = currentMap != cachedMap || currentThingCount != cachedMapThingCount;
            if (!force && !mapChanged)
            {
                return;
            }

            cachedMap = currentMap;
            cachedMapThingCount = currentThingCount;

            cachedArrivalOptions = GetArrivalOptions()
                .Where(def => CharacterSpawnUtility.AreConditionsSatisfied(currentMap, def.requiredConditions))
                .ToList();
            cachedEventOptions = GetSpawnEventOptions()
                .Where(def => CharacterSpawnUtility.AreConditionsSatisfied(currentMap, def.requiredConditions))
                .ToList();
            cachedAnimationOptions = GetSpawnAnimationOptions()
                .Where(def => CharacterSpawnUtility.AreConditionsSatisfied(currentMap, def.requiredConditions))
                .ToList();
        }

        private static void DrawConditionHint(ref float y, float width, CharacterSpawnOptionDef? optionDef)
        {
            string description = CharacterSpawnUtility.DescribeConditions(optionDef);
            if (string.IsNullOrWhiteSpace(description))
                return;

            UIHelper.DrawInfoBanner(ref y, width, description, accent: true);
        }

        private static void DrawDefSelector<TDef>(ref float y, float width, string label, TDef? currentDef, List<TDef> options, Action<TDef> onSelected)
            where TDef : CharacterSpawnOptionDef
        {
            Rect rowRect = new Rect(0f, y, width, UIHelper.RowHeight);
            float actualLabelWidth = Mathf.Max(UIHelper.LabelWidth, Text.CalcSize(label).x + 10f);
            Widgets.Label(new Rect(rowRect.x, rowRect.y, actualLabelWidth, 24f), label);

            Rect buttonRect = new Rect(rowRect.x + actualLabelWidth, rowRect.y, rowRect.width - actualLabelWidth, 24f);
            string currentLabel = currentDef?.GetDisplayLabel() ?? "CS_Studio_None".Translate().ToString();
            string tooltip = currentDef != null
                ? $"{currentDef.GetDisplayLabel()}\n{currentDef.defName}\n{CharacterSpawnUtility.DescribeConditions(currentDef)}".Trim()
                : "CS_Studio_None".Translate().ToString();

            if (UIHelper.DrawSelectionButton(buttonRect, currentLabel))
            {
                Find.WindowStack.Add(new Dialog_DefBrowser<TDef>(
                    label,
                    options,
                    onSelected,
                    def => def.GetDisplayLabel(),
                    def => CharacterSpawnUtility.DescribeConditions(def)));
            }

            TooltipHandler.TipRegion(buttonRect, tooltip);
            y += UIHelper.RowHeight;
        }
    }
}
