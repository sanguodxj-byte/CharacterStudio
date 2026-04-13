using System;
using CharacterStudio.Abilities;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private void DrawAbilityProperties(Rect rect)
        {
            DrawAbilityPanelShell(rect, "CS_Studio_Ability_PropertiesTitle".Translate(), out Rect contentRect);

            Rect viewRect = new Rect(0f, 0f, Mathf.Max(0f, contentRect.width - 16f), Mathf.Max(contentRect.height, 960f));
            Widgets.BeginScrollView(contentRect, ref propsScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            DrawSelectedAbilitySummary(ref y, width);

            ModularAbilityDef ability = selectedAbility!;

            propsBaseExpanded = DrawAbilityPropertiesFoldout(ref y, width, "CS_Studio_Section_AbilityBase".Translate(), propsBaseExpanded);
            if (propsBaseExpanded)
            {
                DrawAbilityBasePropertiesSection(ref y, width, ability);
            }

            propsCarrierExpanded = DrawAbilityPropertiesFoldout(ref y, width, "CS_Studio_Section_Carrier".Translate(), propsCarrierExpanded);
            if (propsCarrierExpanded)
            {
                DrawAbilityCarrierPropertiesSection(ref y, width, ability);
            }

            propsAudioExpanded = DrawAbilityPropertiesFoldout(ref y, width, "CS_Studio_VFX_SoundSection".Translate(), propsAudioExpanded);
            if (propsAudioExpanded)
            {
                DrawAbilityAudioPropertiesSection(ref y, width, ability);
            }

            DrawAbilityValidationSection(ref y, width, ability);

            if (y > viewRect.height)
            {
                viewRect.height = y + 12f;
            }

            Widgets.EndScrollView();
        }

        private void DrawAbilityBasePropertiesSection(ref float y, float width, ModularAbilityDef ability)
        {
            string defNameBefore = ability.defName;
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Ability_DefName".Translate(), ref ability.defName);
            if (ability.defName != defNameBefore)
            {
                NotifyAbilityPreviewDirty();
            }

            string labelBefore = ability.label;
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Ability_Name".Translate(), ref ability.label);
            if (ability.label != labelBefore)
            {
                NotifyAbilityPreviewDirty();
            }

            string iconPath = ability.iconPath ?? string.Empty;
            if (DrawPathFieldWithBrowser(ref y, width, "CS_Studio_Ability_IconPath".Translate(), ref iconPath, () =>
                Find.WindowStack.Add(new Dialog_FileBrowser(GetAbilityTextureBrowseStartPath(ability.iconPath), path =>
                {
                    ability.iconPath = path ?? string.Empty;
                    NotifyAbilityPreviewDirty();
                }, defaultRoot: GetAbilityTextureRootDir()))))
            {
                ability.iconPath = iconPath;
                NotifyAbilityPreviewDirty();
            }

            DrawAbilityDescriptionField(ref y, width, ability);

            float cooldownBefore = ability.cooldownTicks;
            UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Cooldown".Translate(), ref ability.cooldownTicks, 0f, 100000f);
            if (Math.Abs(ability.cooldownTicks - cooldownBefore) > 0.001f)
            {
                NotifyAbilityPreviewDirty(true);
            }

            float warmupBefore = ability.warmupTicks;
            UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Warmup".Translate(), ref ability.warmupTicks, 0f, 100000f);
            if (Math.Abs(ability.warmupTicks - warmupBefore) > 0.001f)
            {
                NotifyAbilityPreviewDirty(true);
            }

            int chargesBefore = ability.charges;
            UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Charges".Translate(), ref ability.charges, 1, 999);
            if (ability.charges != chargesBefore)
            {
                NotifyAbilityPreviewDirty();
            }
        }

        private void DrawAbilityCarrierPropertiesSection(ref float y, float width, ModularAbilityDef ability)
        {
            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);

            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Ability_Type".Translate(), normalizedCarrier,
                new[] { AbilityCarrierType.Self, AbilityCarrierType.Target, AbilityCarrierType.Projectile },
                GetCarrierTypeLabel,
                val =>
                {
                    ability.carrierType = val;
                    if (val == AbilityCarrierType.Self)
                    {
                        ability.targetType = AbilityTargetType.Self;
                        ability.useRadius = false;
                        ability.areaCenter = AbilityAreaCenter.Self;
                    }
                    else if (ability.targetType == AbilityTargetType.Self)
                    {
                        ability.targetType = AbilityTargetType.Entity;
                    }

                    NotifyAbilityPreviewDirty(true);
                });

            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Ability_TargetType".Translate(), normalizedTarget,
                GetAvailableTargetTypes(ability),
                GetTargetTypeLabel,
                val =>
                {
                    ability.targetType = val;
                    NotifyAbilityPreviewDirty(true);
                });

            normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);

            if (ModularAbilityDefExtensions.CarrierNeedsRange(normalizedCarrier, normalizedTarget))
            {
                float rangeBefore = ability.range;
                UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Range".Translate(), ref ability.range, 0, 100);
                if (Math.Abs(ability.range - rangeBefore) > 0.001f)
                {
                    NotifyAbilityPreviewDirty(true);
                }
            }

            bool useRadius = ability.useRadius;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Ability_UseRadius".Translate(), ref useRadius);
            if (ability.useRadius != useRadius)
            {
                ability.useRadius = useRadius;
                NotifyAbilityPreviewDirty(true);
            }

            if (ability.useRadius)
            {
                DrawAbilityAreaProperties(ref y, width, ability);
            }

            if (normalizedCarrier == AbilityCarrierType.Projectile)
            {
                DrawProjectileSelectorRow(ref y, width, ability);
            }
        }

        private void DrawAbilityAreaProperties(ref float y, float width, ModularAbilityDef ability)
        {
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Ability_AreaCenter".Translate(), ModularAbilityDefExtensions.NormalizeAreaCenter(ability),
                GetAvailableAreaCenters(ability),
                GetAreaCenterLabel,
                val =>
                {
                    ability.areaCenter = val;
                    NotifyAbilityPreviewDirty(true);
                });

            AbilityAreaShape normalizedShape = ModularAbilityDefExtensions.NormalizeAreaShape(ability);
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Ability_AreaShape".Translate(), normalizedShape,
                new[]
                {
                    AbilityAreaShape.Circle,
                    AbilityAreaShape.Line,
                    AbilityAreaShape.Cone,
                    AbilityAreaShape.Cross,
                    AbilityAreaShape.Square,
                    AbilityAreaShape.Irregular
                },
                GetAreaShapeLabel,
                val =>
                {
                    ability.areaShape = val;
                    NotifyAbilityPreviewDirty(true);
                });

            if (normalizedShape == AbilityAreaShape.Irregular)
            {
                string irregularPattern = ability.irregularAreaPattern ?? string.Empty;
                UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Ability_IrregularPattern".Translate(), ref irregularPattern);
                if ((ability.irregularAreaPattern ?? string.Empty) != irregularPattern)
                {
                    ability.irregularAreaPattern = irregularPattern;
                    NotifyAbilityPreviewDirty(true);
                }

                Text.Font = GameFont.Tiny;
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(0f, y - 2f, width, 34f), "CS_Studio_Ability_IrregularPatternHint".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 32f;
                return;
            }

            float radiusBefore = ability.radius;
            UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Radius".Translate(), ref ability.radius, 0, 20);
            if (Math.Abs(ability.radius - radiusBefore) > 0.001f)
            {
                NotifyAbilityPreviewDirty(true);
            }
        }

        private void DrawAbilityAudioPropertiesSection(ref float y, float width, ModularAbilityDef ability)
        {
            if (ability.visualEffects.Count == 0)
            {
                Widgets.Label(new Rect(0f, y, width, 24f), "CS_Studio_VFX_SoundSectionEmpty".Translate());
                y += RowHeight;
                return;
            }

            for (int i = 0; i < ability.visualEffects.Count; i++)
            {
                AbilityVisualEffectConfig vfx = ability.visualEffects[i];
                string itemTitle = "CS_Studio_VFX_SoundItem".Translate((i + 1).ToString(), GetVfxTypeLabel(vfx.type));
                Widgets.DrawBoxSolid(new Rect(0f, y, width, 24f), new Color(1f, 1f, 1f, 0.035f));
                Widgets.Label(new Rect(8f, y + 2f, width - 16f, 20f), itemTitle);
                y += 26f;

                bool playSound = vfx.playSound;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_VFX_PlaySound".Translate(), ref playSound);
                if (vfx.playSound != playSound)
                {
                    vfx.playSound = playSound;
                    NotifyAbilityPreviewDirty();
                }

                if (vfx.playSound)
                {
                    DrawAbilityAudioFields(ref y, width, vfx);
                }

                y += 4f;
            }
        }

        private void DrawAbilityAudioFields(ref float y, float width, AbilityVisualEffectConfig vfx)
        {
            string soundDefName = vfx.soundDefName ?? string.Empty;
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_VFX_SoundDefName".Translate(), ref soundDefName);
            if ((vfx.soundDefName ?? string.Empty) != soundDefName)
            {
                vfx.soundDefName = soundDefName;
                NotifyAbilityPreviewDirty();
            }

            int soundDelayBefore = vfx.soundDelayTicks;
            UIHelper.DrawNumericField(ref y, width, "CS_Studio_VFX_SoundDelay".Translate(), ref vfx.soundDelayTicks, 0, 60000);
            if (vfx.soundDelayTicks != soundDelayBefore)
            {
                NotifyAbilityPreviewDirty(true);
            }

            float soundVolumeBefore = vfx.soundVolume;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_VFX_SoundVolume".Translate(), ref vfx.soundVolume, 0f, 4f, "F2");
            if (Math.Abs(vfx.soundVolume - soundVolumeBefore) > 0.001f)
            {
                NotifyAbilityPreviewDirty();
            }

            float soundPitchBefore = vfx.soundPitch;
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_VFX_SoundPitch".Translate(), ref vfx.soundPitch, 0.25f, 3f, "F2");
            if (Math.Abs(vfx.soundPitch - soundPitchBefore) > 0.001f)
            {
                NotifyAbilityPreviewDirty();
            }
        }

        private void DrawAbilityDescriptionField(ref float y, float width, ModularAbilityDef ability)
        {
            const float descriptionLabelWidth = 100f;
            float descriptionFieldWidth = Mathf.Max(120f, width - descriptionLabelWidth - 30f);

            Widgets.Label(new Rect(0f, y, descriptionLabelWidth, 24f), "CS_Studio_Description".Translate());
            string descriptionBefore = ability.description;
            ability.description = Widgets.TextArea(new Rect(descriptionLabelWidth, y, descriptionFieldWidth, 60f), ability.description);
            if (ability.description != descriptionBefore)
            {
                NotifyAbilityPreviewDirty();
            }

            y += 70f;
        }

        private void DrawProjectileSelectorRow(ref float y, float width, ModularAbilityDef ability)
        {
            const float labelWidth = 100f;
            float fieldWidth = Mathf.Max(120f, width - labelWidth - 30f);

            Widgets.Label(new Rect(0f, y, labelWidth, 24f), "CS_Studio_Ability_Projectile".Translate());
            if (DrawInlineValueButton(new Rect(labelWidth, y, fieldWidth, 24f), ability.projectileDef?.label ?? "CS_Studio_None".Translate(), () => ShowProjectileSelector(ability)))
            {
            }

            y += RowHeight;
        }

        private void DrawAbilityValidationSection(ref float y, float width, ModularAbilityDef ability)
        {
            y += 10f;
            if (DrawToolbarButton(new Rect(0f, y, width, 28f), "CS_Studio_Ability_Validate".Translate(), () =>
            {
                ApplyValidationResult(ability.Validate());
            }, true))
            {
            }

            if (!string.IsNullOrEmpty(validationSummary))
            {
                y += 34f;
                Widgets.Label(new Rect(0f, y, width, 30f), validationSummary);
                y += 34f;

                string detailsText = BuildValidationDetailsText();
                if (!string.IsNullOrWhiteSpace(detailsText))
                {
                    Text.Font = GameFont.Tiny;
                    bool oldWrap = Text.WordWrap;
                    Text.WordWrap = true;

                    float detailHeight = Mathf.Max(72f, Text.CalcHeight(detailsText, Mathf.Max(80f, width - 20f)) + 16f);
                    Rect detailRect = new Rect(0f, y, width, detailHeight);
                    Widgets.DrawBoxSolid(detailRect, UIHelper.PanelFillSoftColor);
                    GUI.color = UIHelper.BorderColor;
                    Widgets.DrawBox(detailRect, 1);
                    GUI.color = Color.white;

                    Rect innerRect = detailRect.ContractedBy(8f);
                    Widgets.Label(innerRect, detailsText);
                    TooltipHandler.TipRegion(detailRect, detailsText);

                    Text.WordWrap = oldWrap;
                    Text.Font = GameFont.Small;
                    y += detailHeight + 8f;
                }
            }
            else
            {
                y += 8f;
            }
        }

        private bool DrawPathFieldWithBrowser(ref float rowY, float rowWidth, string label, ref string value, Action browseAction)
        {
            Rect rowRect = new Rect(0f, rowY, rowWidth, RowHeight);
            Text.Font = GameFont.Small;

            float actualLabelWidth = Mathf.Max(100f, Text.CalcSize(label).x + 10f);
            float buttonWidth = 30f;
            float spacing = 5f;
            float fieldWidth = Mathf.Max(40f, rowRect.width - actualLabelWidth - buttonWidth - spacing);

            Widgets.Label(new Rect(rowRect.x, rowRect.y, actualLabelWidth, 24f), label);

            string newValue = Widgets.TextField(
                new Rect(rowRect.x + actualLabelWidth, rowRect.y, fieldWidth, 24f),
                value ?? string.Empty);

            bool changed = false;
            if (newValue != value)
            {
                value = UIHelper.SanitizeInput(newValue, 260);
                changed = true;
            }

            if (DrawToolbarButton(
                new Rect(rowRect.x + actualLabelWidth + fieldWidth + spacing, rowRect.y, buttonWidth, 24f),
                "...",
                browseAction,
                true))
            {
            }

            rowY += RowHeight;
            return changed;
        }

        private bool DrawAbilityPropertiesFoldout(ref float rowY, float rowWidth, string title, bool expanded)
        {
            Rect headerRect = new Rect(0f, rowY, rowWidth, 28f);
            Widgets.DrawBoxSolid(headerRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(headerRect.x, headerRect.yMax - 2f, headerRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = Mouse.IsOver(headerRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(headerRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(headerRect.x + 8f, headerRect.y + 2f, headerRect.width - 40f, 24f), title);
            GUI.color = UIHelper.AccentColor;
            Widgets.Label(new Rect(headerRect.xMax - 28f, headerRect.y + 2f, 20f, 24f), expanded ? "▼" : "▶");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonInvisible(headerRect))
            {
                expanded = !expanded;
            }

            rowY += 32f;
            return expanded;
        }
    }
}
