using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private void DrawTopSummary(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), UIHelper.AccentSoftColor);

            Rect inner = rect.ContractedBy(8f);

            string selectedName = selectedAbility == null
                ? "CS_Studio_Ability_SelectOrCreate".Translate()
                : (string.IsNullOrWhiteSpace(selectedAbility.label) ? selectedAbility.defName : selectedAbility.label);
            string validationText = selectedAbility == null ? "-" : GetValidationLabel(selectedAbility.Validate());
            string hotkeyText = GetHotkeySummary();

            float leftW = inner.width * 0.40f;
            Widgets.Label(new Rect(inner.x, inner.y, leftW, 24f),
                "CS_Studio_Ability_SelectedSummary".Translate(selectedName));
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(inner.x, inner.y + 22f, leftW, 24f),
                "CS_Studio_Ability_HotkeySummary".Translate(hotkeyText));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float midX = inner.x + inner.width * 0.42f;
            float midW = inner.width * 0.28f;
            Widgets.Label(new Rect(midX, inner.y, midW, 24f),
                "CS_Studio_Ability_ValidationSummary".Translate(validationText));
            Widgets.Label(new Rect(midX, inner.y + 22f, midW, 24f),
                "CS_Studio_Ability_EffectsSummary".Translate(
                    selectedAbility?.effects?.Count ?? 0,
                    selectedAbility?.runtimeComponents?.Count ?? 0));

            float bindX = inner.x + inner.width * 0.72f;
            float bindW = inner.width * 0.28f;
            DrawBindToPawnSection(new Rect(bindX, inner.y, bindW, inner.height));

            if (boundSkin != null)
            {
                float hotkeyBtnWidth = Mathf.Min(96f, bindW);
                Rect hotkeyRect = new Rect(bindX + bindW - hotkeyBtnWidth, inner.y, hotkeyBtnWidth, 20f);
                if (DrawToolbarButton(hotkeyRect, "CS_Studio_Ability_HotkeySettings".Translate(), () =>
                {
                    var hotkeyConfig = boundSkin.abilityHotkeys ?? new SkinAbilityHotkeyConfig();
                    boundSkin.abilityHotkeys = hotkeyConfig;
                    Find.WindowStack.Add(new Dialog_AbilityHotkeySettings(hotkeyConfig, abilities, () => validationSummary = "CS_Studio_Section_AbilityHotkeys".Translate()));
                }))
                {
                }
            }
        }

        private void DrawBindToPawnSection(Rect rect)
        {
            string pawnLabel = boundPawn != null
                ? boundPawn.LabelShort
                : "CS_Ability_NoPawnBound".Translate();

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = boundPawn != null ? new Color(0.4f, 1f, 0.5f) : Color.gray;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f),
                "CS_Ability_BoundPawn".Translate() + ": " + pawnLabel);
            GUI.color = Color.white;

            float btnH = 22f;
            float btnW = (rect.width - 4f) / 2f;

            if (DrawToolbarButton(new Rect(rect.x, rect.y + 22f, btnW, btnH), "CS_Ability_SelectPawn".Translate(), ShowSelectPawnMenu))
            {
            }

            bool hasAbilities = abilities != null && abilities.Count > 0;
            if (boundPawn != null)
            {
                if (hasAbilities)
                {
                    if (DrawToolbarButton(new Rect(rect.x + btnW + 4f, rect.y + 22f, btnW, btnH),
                        "CS_Ability_Grant".Translate(), GrantAbilitiesToBoundPawn, true))
                    {
                    }
                }

                if (DrawToolbarButton(new Rect(rect.x, rect.y + 48f, rect.width, btnH),
                    "CS_Ability_Revoke".Translate(), () =>
                {
                    AbilityLoadoutRuntimeUtility.ClearExplicitLoadout(boundPawn);
                    AbilityGrantUtility.RevokeAllCSAbilitiesFromPawn(boundPawn);
                    Messages.Message("CS_Ability_RevokeSuccess".Translate(boundPawn.LabelShort),
                        MessageTypeDefOf.NeutralEvent, false);
                }))
                {
                }
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x + btnW + 4f, rect.y + 22f, btnW, btnH), "CS_Ability_Grant".Translate());
                GUI.color = Color.white;
            }
        }

        private void ShowSelectPawnMenu()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("CS_Studio_Err_NoMap".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var pawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.abilities != null)
                .OrderByDescending(p => p.IsColonist)
                .ThenBy(p => p.LabelShort)
                .ToList();

            if (pawns.Count == 0)
            {
                Messages.Message("CS_Studio_Err_NoPawns".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption("CS_Ability_ClearBinding".Translate(), () => boundPawn = null));

            foreach (var pawn in pawns)
            {
                var p = pawn;
                string label = p.LabelShort;
                if (boundPawn == p)
                {
                    label = "✓ " + label;
                }

                options.Add(new FloatMenuOption(label, () => boundPawn = p));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void GrantAbilitiesToBoundPawn()
        {
            if (boundPawn == null || abilities == null || abilities.Count == 0)
            {
                return;
            }

            AbilityLoadoutRuntimeUtility.ApplyExplicitLoadout(boundPawn, abilities, boundHotkeys);

            Messages.Message(
                "CS_Ability_GrantSuccess".Translate(abilities.Count, boundPawn.LabelShort),
                MessageTypeDefOf.PositiveEvent, false);
        }

        private void DrawSelectedAbilitySummary(ref float y, float width)
        {
            if (selectedAbility == null)
            {
                return;
            }

            Rect cardRect = new Rect(0, y, width, 68f);
            Widgets.DrawBoxSolid(cardRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(cardRect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(cardRect.x, cardRect.yMax - 2f, cardRect.width, 2f), UIHelper.AccentSoftColor);
            Rect inner = cardRect.ContractedBy(8f);
            var validation = selectedAbility.Validate();
            string selectedName = string.IsNullOrWhiteSpace(selectedAbility.label) ? selectedAbility.defName : selectedAbility.label;

            Widgets.Label(new Rect(inner.x, inner.y, inner.width * 0.52f, 24f), selectedName);
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(inner.x, inner.y + 22f, inner.width * 0.52f, 18f),
                "CS_Studio_Ability_DefSummary".Translate(selectedAbility.defName));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Widgets.Label(new Rect(inner.x + inner.width * 0.54f, inner.y, inner.width * 0.46f, 24f),
                "CS_Studio_Ability_ValidationSummary".Translate(GetValidationLabel(validation)));
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(inner.x + inner.width * 0.54f, inner.y + 22f, inner.width * 0.46f, 18f),
                "CS_Studio_Ability_HotkeySummary".Translate(GetHotkeySummaryForSelected()));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            y += 76f;
        }

        private string GetValidationLabel(AbilityValidationResult validation)
        {
            if (!validation.IsValid)
            {
                return "CS_Studio_Ability_Invalid".Translate();
            }

            return validation.Warnings.Count > 0
                ? "CS_Studio_Ability_ValidWithWarnings".Translate()
                : "CS_Studio_Ability_Valid".Translate();
        }

        private string GetHotkeySummary()
        {
            if (boundHotkeys == null || !boundHotkeys.enabled)
            {
                return "CS_Studio_Ability_Hotkey_None".Translate();
            }

            return string.Join(" / ", new[]
            {
                FormatHotkeySlotSummary("CS_Studio_Ability_HotkeySlot_Q".Translate(), boundHotkeys.qAbilityDefName),
                FormatHotkeySlotSummary("CS_Studio_Ability_HotkeySlot_W".Translate(), boundHotkeys.wAbilityDefName),
                FormatHotkeySlotSummary("CS_Studio_Ability_HotkeySlot_E".Translate(), boundHotkeys.eAbilityDefName),
                FormatHotkeySlotSummary("CS_Studio_Ability_HotkeySlot_R".Translate(), boundHotkeys.rAbilityDefName),
                FormatHotkeySlotSummary("CS_Studio_Ability_HotkeySlot_WCombo".Translate(), boundHotkeys.wComboAbilityDefName)
            });
        }

        private string GetHotkeySummaryForSelected()
        {
            if (selectedAbility == null || boundHotkeys == null || !boundHotkeys.enabled)
            {
                return "CS_Studio_Ability_Hotkey_None".Translate();
            }

            var slots = new List<string>();
            if (string.Equals(boundHotkeys.qAbilityDefName, selectedAbility.defName, StringComparison.OrdinalIgnoreCase)) slots.Add("CS_Studio_Ability_HotkeySlot_Q".Translate());
            if (string.Equals(boundHotkeys.wAbilityDefName, selectedAbility.defName, StringComparison.OrdinalIgnoreCase)) slots.Add("CS_Studio_Ability_HotkeySlot_W".Translate());
            if (string.Equals(boundHotkeys.eAbilityDefName, selectedAbility.defName, StringComparison.OrdinalIgnoreCase)) slots.Add("CS_Studio_Ability_HotkeySlot_E".Translate());
            if (string.Equals(boundHotkeys.rAbilityDefName, selectedAbility.defName, StringComparison.OrdinalIgnoreCase)) slots.Add("CS_Studio_Ability_HotkeySlot_R".Translate());
            if (string.Equals(boundHotkeys.wComboAbilityDefName, selectedAbility.defName, StringComparison.OrdinalIgnoreCase)) slots.Add("CS_Studio_Ability_HotkeySlot_WCombo".Translate());
            return slots.Count > 0 ? string.Join(", ", slots) : "CS_Studio_Ability_Hotkey_None".Translate();
        }

        private string FormatHotkeySlotSummary(string slotLabel, string defName)
        {
            return "CS_Studio_Ability_HotkeySlotSummary".Translate(slotLabel, FormatHotkeyAbility(defName));
        }

        private string FormatHotkeyAbility(string defName)
        {
            return FormatHotkeyAbilityStatic(defName, abilities);
        }

        private static string FormatHotkeyAbilityStatic(string defName, List<ModularAbilityDef> availableAbilities)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return "CS_Studio_Ability_Unassigned".Translate();
            }

            ModularAbilityDef? match = availableAbilities?.FirstOrDefault(a => a != null && string.Equals(a.defName, defName, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                return defName;
            }

            return string.IsNullOrWhiteSpace(match.label) ? match.defName : match.label;
        }
    }
}
