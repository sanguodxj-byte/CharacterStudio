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
        private readonly SkinAbilityHotkeyConfig hotkeyConfig;
        private readonly List<ModularAbilityDef> availableAbilities;
        private readonly Action? onChanged;

        private bool tempEnabled;
        private string tempQAbilityDefName;
        private string tempWAbilityDefName;
        private string tempEAbilityDefName;
        private string tempRAbilityDefName;
        private string tempWComboAbilityDefName;

        public override Vector2 InitialSize => new Vector2(500f, 320f);

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
            tempWComboAbilityDefName = hotkeyConfig.wComboAbilityDefName ?? string.Empty;
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

            DrawHotkeyMappingField(ref y, width, "CS_Studio_Ability_Hotkey_Q".Translate(), () => tempQAbilityDefName, v => tempQAbilityDefName = v);
            DrawHotkeyMappingField(ref y, width, "CS_Studio_Ability_Hotkey_W".Translate(), () => tempWAbilityDefName, v => tempWAbilityDefName = v);
            DrawHotkeyMappingField(ref y, width, "CS_Studio_Ability_Hotkey_E".Translate(), () => tempEAbilityDefName, v => tempEAbilityDefName = v);
            DrawHotkeyMappingField(ref y, width, "CS_Studio_Ability_Hotkey_R".Translate(), () => tempRAbilityDefName, v => tempRAbilityDefName = v);
            DrawHotkeyMappingField(ref y, width, "CS_Studio_Ability_Hotkey_WCombo".Translate(), () => tempWComboAbilityDefName, v => tempWComboAbilityDefName = v);

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
            hotkeyConfig.wComboAbilityDefName = tempWComboAbilityDefName ?? string.Empty;

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