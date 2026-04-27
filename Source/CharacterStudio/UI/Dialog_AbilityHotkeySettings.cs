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
        private Dictionary<string, string> tempSlotBindings;

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
            tempSlotBindings = new Dictionary<string, string>(hotkeyConfig.slotBindings, StringComparer.OrdinalIgnoreCase);

            string[] slotKeys = { "Q", "E", "R", "T", "F", "Z", "X", "C", "V" };
            string[] labelKeys = {
                "CS_Studio_Ability_Hotkey_Q", "CS_Studio_Ability_Hotkey_E",
                "CS_Studio_Ability_Hotkey_R", "CS_Studio_Ability_Hotkey_T",
                "CS_Studio_Ability_Hotkey_F", "CS_Studio_Ability_Hotkey_Z",
                "CS_Studio_Ability_Hotkey_X", "CS_Studio_Ability_Hotkey_C",
                "CS_Studio_Ability_Hotkey_V"
            };

            for (int i = 0; i < slotKeys.Length; i++)
            {
                string slot = slotKeys[i];
                string lk = labelKeys[i];
                bindingEntries.Add(new HotkeyBindingEntry { labelKey = lk, getter = () => tempSlotBindings.TryGetValue(slot, out string v) ? v : string.Empty, setter = v => tempSlotBindings[slot] = v });
            }
        }

        public override void PreClose()
        {
            base.PreClose();
            ApplyChanges();
        }

        public override void DoWindowContents(Rect inRect)
        {
            UIHelper.DrawDialogFrame(inRect, this);

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
            hotkeyConfig.slotBindings.Clear();
            foreach (var kvp in tempSlotBindings)
            {
                hotkeyConfig.slotBindings[kvp.Key] = kvp.Value ?? string.Empty;
            }
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
