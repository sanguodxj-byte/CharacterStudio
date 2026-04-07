using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 技能热键设置窗口。
    /// 直接基于当前技能编辑器中的实时技能列表进行绑定，
    /// 避免依赖皮肤对象中尚未同步回写的 abilities。
    /// </summary>
    public class Dialog_AbilityHotkeySettings : Window
    {
        private sealed class HotkeyBindingEntry
        {
            public string labelKey = string.Empty;
            public Func<string> getter = static () => string.Empty;
            public Action<string> setter = static _ => { };
        }

        private readonly SkinAbilityHotkeyConfig hotkeyConfig;
        private readonly List<ModularAbilityDef> availableAbilities;
        private readonly Action? onChanged;
        private readonly List<HotkeyBindingEntry> bindingEntries = new();
        private Vector2 scrollPosition;

        private bool tempEnabled;
        private string tempQAbilityDefName;
        private string tempWAbilityDefName;
        private string tempEAbilityDefName;
        private string tempRAbilityDefName;
        private string tempTAbilityDefName;
        private string tempAAbilityDefName;
        private string tempSAbilityDefName;
        private string tempDAbilityDefName;
        private string tempFAbilityDefName;
        private string tempZAbilityDefName;
        private string tempXAbilityDefName;
        private string tempCAbilityDefName;
        private string tempVAbilityDefName;

        public override Vector2 InitialSize => new Vector2(560f, 560f);

        public Dialog_AbilityHotkeySettings(
            SkinAbilityHotkeyConfig? config,
            IEnumerable<ModularAbilityDef> abilities,
            Action? onChangedCallback = null)
        {
            hotkeyConfig = config ?? new SkinAbilityHotkeyConfig();
            availableAbilities = (abilities ?? Enumerable.Empty<ModularAbilityDef>())
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.defName))
                .GroupBy(a => a.defName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => string.IsNullOrWhiteSpace(a.label) ? a.defName : a.label)
                .ToList();

            onChanged = onChangedCallback;
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            forcePause = true;

            tempEnabled = hotkeyConfig.enabled;
            tempQAbilityDefName = hotkeyConfig.qAbilityDefName ?? string.Empty;
            tempWAbilityDefName = hotkeyConfig.wAbilityDefName ?? string.Empty;
            tempEAbilityDefName = hotkeyConfig.eAbilityDefName ?? string.Empty;
            tempRAbilityDefName = hotkeyConfig.rAbilityDefName ?? string.Empty;
            tempTAbilityDefName = hotkeyConfig.tAbilityDefName ?? string.Empty;
            tempAAbilityDefName = hotkeyConfig.aAbilityDefName ?? string.Empty;
            tempSAbilityDefName = hotkeyConfig.sAbilityDefName ?? string.Empty;
            tempDAbilityDefName = hotkeyConfig.dAbilityDefName ?? string.Empty;
            tempFAbilityDefName = hotkeyConfig.fAbilityDefName ?? string.Empty;
            tempZAbilityDefName = hotkeyConfig.zAbilityDefName ?? string.Empty;
            tempXAbilityDefName = hotkeyConfig.xAbilityDefName ?? string.Empty;
            tempCAbilityDefName = hotkeyConfig.cAbilityDefName ?? string.Empty;
            tempVAbilityDefName = hotkeyConfig.vAbilityDefName ?? string.Empty;
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_Q", getter = () => tempQAbilityDefName, setter = v => tempQAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_W", getter = () => tempWAbilityDefName, setter = v => tempWAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_E", getter = () => tempEAbilityDefName, setter = v => tempEAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_R", getter = () => tempRAbilityDefName, setter = v => tempRAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_T", getter = () => tempTAbilityDefName, setter = v => tempTAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_A", getter = () => tempAAbilityDefName, setter = v => tempAAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_S", getter = () => tempSAbilityDefName, setter = v => tempSAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_D", getter = () => tempDAbilityDefName, setter = v => tempDAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_F", getter = () => tempFAbilityDefName, setter = v => tempFAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_Z", getter = () => tempZAbilityDefName, setter = v => tempZAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_X", getter = () => tempXAbilityDefName, setter = v => tempXAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_C", getter = () => tempCAbilityDefName, setter = v => tempCAbilityDefName = v });
            bindingEntries.Add(new HotkeyBindingEntry { labelKey = "CS_Studio_Ability_Hotkey_V", getter = () => tempVAbilityDefName, setter = v => tempVAbilityDefName = v });
        }

        public override void PreClose()
        {
            base.PreClose();
            ApplyChanges();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "CS_Studio_Section_AbilityHotkeys".Translate());
            Text.Font = GameFont.Small;

            float y = 40f;
            float width = inRect.width;

            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Ability_Hotkey_Enable".Translate(), ref tempEnabled);

            float buttonAreaHeight = 40f;
            Rect outRect = new Rect(0f, y, width, inRect.height - y - buttonAreaHeight - 10f);
            float viewHeight = bindingEntries.Count * 34f + 8f;
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float innerY = 0f;
            foreach (HotkeyBindingEntry entry in bindingEntries)
            {
                DrawHotkeyMappingField(ref innerY, viewRect.width, entry.labelKey.Translate(), entry.getter, entry.setter);
            }
            Widgets.EndScrollView();

            float btnWidth = 100f;
            float btnY = inRect.height - 35f;
            if (Widgets.ButtonText(new Rect(inRect.width / 2f - btnWidth / 2f, btnY, btnWidth, 30f), "CS_Studio_Btn_OK".Translate()))
            {
                Close();
            }
        }

        private void ApplyChanges()
        {
            hotkeyConfig.enabled = tempEnabled;
            hotkeyConfig.qAbilityDefName = tempQAbilityDefName ?? string.Empty;
            hotkeyConfig.wAbilityDefName = tempWAbilityDefName ?? string.Empty;
            hotkeyConfig.eAbilityDefName = tempEAbilityDefName ?? string.Empty;
            hotkeyConfig.rAbilityDefName = tempRAbilityDefName ?? string.Empty;
            hotkeyConfig.tAbilityDefName = tempTAbilityDefName ?? string.Empty;
            hotkeyConfig.aAbilityDefName = tempAAbilityDefName ?? string.Empty;
            hotkeyConfig.sAbilityDefName = tempSAbilityDefName ?? string.Empty;
            hotkeyConfig.dAbilityDefName = tempDAbilityDefName ?? string.Empty;
            hotkeyConfig.fAbilityDefName = tempFAbilityDefName ?? string.Empty;
            hotkeyConfig.zAbilityDefName = tempZAbilityDefName ?? string.Empty;
            hotkeyConfig.xAbilityDefName = tempXAbilityDefName ?? string.Empty;
            hotkeyConfig.cAbilityDefName = tempCAbilityDefName ?? string.Empty;
            hotkeyConfig.vAbilityDefName = tempVAbilityDefName ?? string.Empty;
            onChanged?.Invoke();
        }

        private void DrawHotkeyMappingField(ref float y, float width, string label, Func<string> getter, Action<string> setter)
        {
            string current = getter();
            string display = FormatAbilityDisplay(current);
            UIHelper.DrawPropertyFieldWithButton(ref y, width, label, display, () => ShowAbilitySelector(setter));
        }

        private string FormatAbilityDisplay(string defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return "CS_Studio_Ability_Hotkey_None".Translate();
            }

            ModularAbilityDef? match = availableAbilities.FirstOrDefault(a =>
                string.Equals(a.defName, defName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                return defName;
            }

            if (string.IsNullOrWhiteSpace(match.label) || string.Equals(match.label, match.defName, StringComparison.OrdinalIgnoreCase))
            {
                return match.defName;
            }

            return $"{match.label} ({match.defName})";
        }

        private void ShowAbilitySelector(Action<string> onSelect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Ability_Hotkey_None".Translate(), () => onSelect(string.Empty))
            };

            foreach (var ability in availableAbilities)
            {
                var captured = ability;
                string label = string.IsNullOrWhiteSpace(captured.label) || string.Equals(captured.label, captured.defName, StringComparison.OrdinalIgnoreCase)
                    ? captured.defName
                    : $"{captured.label} ({captured.defName})";

                options.Add(new FloatMenuOption(label, () => onSelect(captured.defName)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
