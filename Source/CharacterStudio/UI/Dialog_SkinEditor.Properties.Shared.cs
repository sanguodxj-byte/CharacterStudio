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
    public partial class Dialog_SkinEditor
    {
        private static readonly string[] EquipmentShaderOptions =
        {
            "Cutout",
            "CutoutComplex",
            "Transparent",
            "TransparentPostLight",
            "MetaOverlay"
        };

        private void DrawSelectionPropertyButton(ref float y, float width, string label, string valueLabel, Action onClick, float labelWidth = UIHelper.LabelWidth)
        {
            Rect rect = new Rect(0f, y, width, UIHelper.RowHeight);

            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label).x + 10f);

            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24f), label);

            Rect buttonRect = new Rect(rect.x + actualLabelWidth, rect.y, rect.width - actualLabelWidth, 24f);
            Widgets.DrawBoxSolid(buttonRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(buttonRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UIHelper.HeaderColor;
            string displayValue = string.IsNullOrWhiteSpace(valueLabel)
                ? "CS_Studio_None".Translate().ToString()
                : valueLabel;
            Widgets.Label(buttonRect, displayValue);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            if (Widgets.ButtonInvisible(buttonRect))
            {
                onClick();
            }

            y += UIHelper.RowHeight;
        }

        private void DrawPropertyHint(ref float y, float width, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(0f, y - 4f, width, 26f), text);
            GUI.color = Color.white;
            Text.Font = oldFont;
            y += 18f;
        }

        private string GetEquipmentThingDefSelectionLabel(string defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return "CS_Studio_None".Translate();
            }

            ThingDef resolved = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (resolved == null)
            {
                return defName;
            }

            string label = string.IsNullOrWhiteSpace(resolved.label) ? resolved.defName : resolved.label;
            return $"{label} [{resolved.defName}]";
        }

        private void ShowEquipmentLinkedThingDefSelector(CharacterEquipmentDef equipment, Action onChanged)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    CaptureUndoSnapshot();
                    equipment.thingDefName = string.Empty;
                    onChanged();
                })
            };

            var defs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def != null && (def.apparel != null || def.category == ThingCategory.Item))
                .OrderBy(def => def.label ?? def.defName);

            foreach (ThingDef thingDef in defs)
            {
                ThingDef localDef = thingDef;
                string label = string.IsNullOrWhiteSpace(localDef.label) ? localDef.defName : localDef.label;
                options.Add(new FloatMenuOption($"{label} [{localDef.defName}]", () =>
                {
                    CaptureUndoSnapshot();
                    equipment.thingDefName = localDef.defName;
                    onChanged();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string GetEquipmentShaderSelectionLabel(string shaderDefName)
        {
            string resolvedShader = string.IsNullOrWhiteSpace(shaderDefName)
                ? EquipmentShaderOptions[0]
                : shaderDefName;

            string key = $"CS_Studio_Equip_Shader_{resolvedShader}";
            return key.CanTranslate() ? key.Translate() : resolvedShader;
        }

        private static string GetAbilitySelectionLabel(string defName, IEnumerable<ModularAbilityDef> abilities)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return "CS_Studio_None".Translate();
            }

            ModularAbilityDef? resolved = abilities?
                .FirstOrDefault(ability => ability != null && string.Equals(ability.defName, defName, StringComparison.OrdinalIgnoreCase));

            if (resolved == null)
            {
                return defName;
            }

            string displayName = string.IsNullOrWhiteSpace(resolved.label) ? resolved.defName : resolved.label;
            return $"{displayName} ({resolved.defName})";
        }

        private void ShowEquipmentTriggeredAbilitySelector(CharacterEquipmentRenderData renderData, IEnumerable<ModularAbilityDef> abilities, Action onChanged)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    CaptureUndoSnapshot();
                    renderData.triggerAbilityDefName = string.Empty;
                    onChanged();
                })
            };

            var sorted = abilities?
                .Where(ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                .GroupBy(ability => ability.defName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(ability => string.IsNullOrWhiteSpace(ability.label) ? ability.defName : ability.label, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<ModularAbilityDef>();

            foreach (ModularAbilityDef ability in sorted)
            {
                ModularAbilityDef localAbility = ability;
                string displayName = string.IsNullOrWhiteSpace(localAbility.label) ? localAbility.defName : localAbility.label;
                options.Add(new FloatMenuOption($"{displayName} ({localAbility.defName})", () =>
                {
                    CaptureUndoSnapshot();
                    renderData.triggerAbilityDefName = localAbility.defName;
                    onChanged();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowEquipmentShaderSelector(CharacterEquipmentDef equipment, Action onChanged)
        {
            var options = new List<FloatMenuOption>();

            foreach (string shaderName in EquipmentShaderOptions)
            {
                string localShader = shaderName;
                string key = $"CS_Studio_Equip_Shader_{localShader}";
                string label = key.CanTranslate() ? key.Translate() : localShader;
                options.Add(new FloatMenuOption(label, () =>
                {
                    CaptureUndoSnapshot();
                    equipment.shaderDefName = localShader;
                    equipment.renderData.shaderDefName = localShader;
                    onChanged();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string[] ParseCommaSeparatedList(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Array.Empty<string>();

            return input
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrEmpty(part))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

    }
}
