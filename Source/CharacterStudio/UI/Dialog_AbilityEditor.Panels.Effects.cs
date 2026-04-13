using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private void DrawEffectsPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect contentRect = rect.ContractedBy(Margin);

            if (selectedAbility == null)
            {
                Widgets.Label(contentRect, "CS_Studio_Ability_SelectHint".Translate());
                return;
            }

            float buttonY = contentRect.y;
            float buttonWidth = 120f;
            if (DrawPanelButton(new Rect(contentRect.x, buttonY, buttonWidth, 28), "CS_Studio_Ability_AddEffect".Translate(), ShowAddEffectMenu, accent: true))
            {
            }

            Rect listRect = new Rect(contentRect.x, buttonY + 34f, contentRect.width, contentRect.height - 34f);
            float viewHeight = 6f;
            if (selectedAbility.effects != null)
            {
                for (int i = 0; i < selectedAbility.effects.Count; i++)
                {
                    viewHeight += GetEffectItemHeight(selectedAbility.effects[i]) + 6f;
                }
            }

            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, viewHeight));
            Widgets.BeginScrollView(listRect, ref effectsScrollPos, viewRect);
            float cy = 0f;
            if (selectedAbility.effects != null)
            {
                for (int i = 0; i < selectedAbility.effects.Count; i++)
                {
                    var effect = selectedAbility.effects[i];
                    float itemHeight = GetEffectItemHeight(effect);
                    DrawEffectItem(new Rect(0f, cy, viewRect.width, itemHeight), effect, i);
                    cy += itemHeight + 6f;
                }
            }
            Widgets.EndScrollView();
        }

        private float GetEffectItemHeight(AbilityEffectConfig effect)
        {
            // Header: title(22) + summary(16) + separator(1) + spacing(7) = 46
            float height = 46f;
            // Amount + Chance row
            height += RowHeight;
            switch (effect.type)
            {
                case AbilityEffectType.Summon:
                    height += RowHeight;  // SummonKind + SummonCount
                    height += RowHeight;  // SummonFaction
                    break;
                case AbilityEffectType.Terraform:
                    height += RowHeight;  // TerraformMode
                    if (effect.terraformMode == TerraformEffectMode.SpawnThing)
                    {
                        height += RowHeight; // TerraformThing
                        height += RowHeight; // SpawnCount
                    }
                    else if (effect.terraformMode == TerraformEffectMode.ReplaceTerrain)
                    {
                        height += RowHeight; // TerraformTerrain
                    }
                    break;
            }

            if (effect.type == AbilityEffectType.Damage)
            {
                height += RowHeight; // DamageDef
                height += RowHeight; // CanHurtSelf
            }

            if (effect.type == AbilityEffectType.Buff || effect.type == AbilityEffectType.Debuff)
            {
                // Hediff + Duration on the same row as Amount + Chance - already counted
            }

            if (effect.type == AbilityEffectType.Control)
            {
                // ControlMode + Duration on same row - no extra
                if (effect.controlMode != ControlEffectMode.Stun)
                {
                    height += RowHeight; // ControlMoveDistance
                }
            }

            if (effect.type == AbilityEffectType.WeatherChange)
            {
                height += RowHeight; // WeatherDef
                height += RowHeight; // Duration + Transition
            }

            return height + 10f;
        }

        private static string BuildEffectItemSummary(AbilityEffectConfig effect)
        {
            string amountText = effect.amount.ToString("F1");
            string chanceText = (effect.chance * 100f).ToString("F0") + "%";
            return $"{GetEffectTypeLabel(effect.type)}  ·  {amountText}  ·  {chanceText}";
        }

        private void DrawEffectItem(Rect rect, AbilityEffectConfig effect, int index)
        {
            // Draw card background
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect inner = rect.ContractedBy(6f);
            float y = inner.y;

            // ── Row 1: Title (effect type selector) + action buttons ──
            GameFont prevFont = Text.Font;
            float actionButtonsWidth = 96f;
            float typeSelectorWidth = Mathf.Max(100f, inner.width - actionButtonsWidth - 4f);
            if (DrawSelectionFieldButton(new Rect(inner.x, y, typeSelectorWidth, 22f), GetEffectTypeLabel(effect.type), () => ShowEffectTypeSelector(effect)))
            {
            }

            // Action buttons aligned to right
            float btnX = inner.x + inner.width - 96f;
            if (DrawCompactIconButton(new Rect(btnX, y, 28f, 22f), "▲", () => SwapEffects(index, index - 1)))
            {
                return;
            }
            btnX += 30f;
            if (DrawCompactIconButton(new Rect(btnX, y, 28f, 22f), "▼", () => SwapEffects(index, index + 1)))
            {
                return;
            }
            btnX += 30f;
            if (DrawCompactIconButton(new Rect(btnX, y, 34f, 22f), "X", () =>
            {
                selectedAbility?.effects.RemoveAt(index);
                NotifyAbilityPreviewDirty(true);
            }))
            {
                return;
            }

            // ── Row 2: Summary line ──
            y += 22f;
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(inner.x, y, inner.width, 16f), BuildEffectItemSummary(effect));
            GUI.color = Color.white;
            Text.Font = prevFont;

            // ── Separator ──
            y += 17f;
            Widgets.DrawBoxSolid(new Rect(inner.x, y, inner.width, 1f), UIHelper.AccentSoftColor);
            y += 6f;

            // ── Content area ──
            float gap = 12f;
            float colWidth = (inner.width - gap) * 0.5f;
            float labelW = Mathf.Clamp(inner.width * 0.20f, 58f, 90f);
            float fieldW = Mathf.Max(60f, colWidth - labelW - 4f);
            float rightX = inner.x + colWidth + gap;

            void DrawNumericRow(float rowY, float x, string label, ref float value, ref string buffer, float min = float.MinValue, float max = float.MaxValue)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(x, rowY + 2f, labelW, 20f), GenText.Truncate(label, labelW));
                Text.Font = prevFont;
                float before = value;
                UIHelper.TextFieldNumeric(new Rect(x + labelW, rowY, fieldW, 24f), ref value, ref buffer, min, max);
                if (!Mathf.Approximately(value, before))
                {
                    NotifyAbilityPreviewDirty(true);
                }
            }

            void DrawNumericRowInt(float rowY, float x, string label, ref int value, ref string buffer, int min, int max)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(x, rowY + 2f, labelW, 20f), GenText.Truncate(label, labelW));
                Text.Font = prevFont;
                int before = value;
                UIHelper.TextFieldNumeric(new Rect(x + labelW, rowY, fieldW, 24f), ref value, ref buffer, min, max);
                if (value != before)
                {
                    NotifyAbilityPreviewDirty(true);
                }
            }

            void DrawSelectRow(float rowY, float x, string label, string value, Action onClick, float? overrideLabelWidth = null, float? overrideFieldWidth = null)
            {
                float currentLabelW = overrideLabelWidth ?? labelW;
                float currentFieldW = overrideFieldWidth ?? fieldW;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(x, rowY + 2f, currentLabelW, 20f), GenText.Truncate(label, currentLabelW));
                Text.Font = prevFont;
                if (DrawSelectionFieldButton(new Rect(x + currentLabelW, rowY, currentFieldW, 24f), value, onClick))
                {
                }
            }

            string amountStr = effect.amount.ToString();
            DrawNumericRow(y, inner.x, "CS_Studio_Effect_Amount".Translate(), ref effect.amount, ref amountStr);
            string chanceStr = effect.chance.ToString();
            DrawNumericRow(y, rightX, "CS_Studio_Effect_Chance".Translate(), ref effect.chance, ref chanceStr, 0f, 1f);
            y += 26f;

            switch (effect.type)
            {
                case AbilityEffectType.Damage:
                    DrawSelectRow(y, inner.x, "CS_Studio_Effect_DamageDef".Translate(), effect.damageDef?.label ?? "CS_Studio_None".Translate(), () => ShowDamageDefSelector(effect), overrideLabelWidth: labelW, overrideFieldWidth: inner.width - labelW);
                    y += 26f;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(inner.x, y + 2f, labelW, 20f), GenText.Truncate("CS_Studio_Effect_CanHurtSelf".Translate(), labelW));
                    Text.Font = prevFont;
                    bool canHurtSelf = effect.canHurtSelf;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, y + 2f), ref canHurtSelf, 24f, false);
                    if (effect.canHurtSelf != canHurtSelf)
                    {
                        effect.canHurtSelf = canHurtSelf;
                        NotifyAbilityPreviewDirty(true);
                    }
                    break;
                case AbilityEffectType.Buff:
                case AbilityEffectType.Debuff:
                    DrawSelectRow(y, inner.x, "CS_Studio_Effect_Hediff".Translate(), effect.hediffDef?.label ?? "CS_Studio_None".Translate(), () => ShowHediffSelector(effect));
                    string durStr = effect.duration.ToString();
                    DrawNumericRow(y, rightX, "CS_Studio_Effect_Duration".Translate(), ref effect.duration, ref durStr, 0f, 999f);
                    break;
                case AbilityEffectType.Heal:
                    break;
                case AbilityEffectType.Summon:
                    DrawSelectRow(y, inner.x, "CS_Studio_Effect_SummonKind".Translate(), effect.summonKind?.label ?? "CS_Studio_None".Translate(), () => ShowPawnKindSelector(effect));
                    string summonStr = effect.summonCount.ToString();
                    DrawNumericRowInt(y, rightX, "CS_Studio_Effect_SummonCount".Translate(), ref effect.summonCount, ref summonStr, 1, 99);
                    y += 26f;
                    DrawSelectRow(y, inner.x, "CS_Studio_Effect_SummonFaction".Translate(), effect.summonFactionDef?.label ?? "CS_Studio_None".Translate(), () => ShowFactionSelector(effect), overrideLabelWidth: labelW, overrideFieldWidth: inner.width - labelW);
                    break;
                case AbilityEffectType.Control:
                    DrawSelectRow(y, inner.x, "CS_Studio_Effect_ControlMode".Translate(), GetControlModeLabel(effect.controlMode), () => ShowControlModeSelector(effect));
                    string controlDurStr = effect.duration.ToString();
                    DrawNumericRow(y, rightX, "CS_Studio_Effect_Duration".Translate(), ref effect.duration, ref controlDurStr, 0f, 999f);
                    if (effect.controlMode != ControlEffectMode.Stun)
                    {
                        y += 26f;
                        string controlMoveDistanceStr = effect.controlMoveDistance.ToString();
                        DrawNumericRowInt(y, inner.x, "CS_Studio_Effect_ControlMoveDistance".Translate(), ref effect.controlMoveDistance, ref controlMoveDistanceStr, 1, 99);
                    }
                    break;
                case AbilityEffectType.Terraform:
                    DrawSelectRow(y, inner.x, "CS_Studio_Effect_TerraformMode".Translate(), GetTerraformModeLabel(effect.terraformMode), () => ShowTerraformModeSelector(effect));
                    y += 26f;
                    float wideLabelW = Mathf.Clamp(inner.width * 0.24f, 82f, 120f);
                    float wideFieldW = Mathf.Max(80f, inner.width - wideLabelW);
                    switch (effect.terraformMode)
                    {
                        case TerraformEffectMode.SpawnThing:
                            DrawSelectRow(y, inner.x, "CS_Studio_Effect_TerraformThing".Translate(),
                                effect.terraformThingDef?.label ?? "CS_Studio_None".Translate(), () => ShowTerraformThingSelector(effect),
                                overrideLabelWidth: wideLabelW, overrideFieldWidth: wideFieldW);
                            y += 26f;

                            string spawnCountStr = effect.terraformSpawnCount.ToString();
                            Text.Font = GameFont.Tiny;
                            Widgets.Label(new Rect(inner.x, y + 2f, wideLabelW, 20f), GenText.Truncate("CS_Studio_Effect_TerraformSpawnCount".Translate(), wideLabelW));
                            Text.Font = prevFont;
                            int spawnCountBefore = effect.terraformSpawnCount;
                            UIHelper.TextFieldNumeric(new Rect(inner.x + wideLabelW, y, wideFieldW, 24f), ref effect.terraformSpawnCount, ref spawnCountStr, 1, 999);
                            if (effect.terraformSpawnCount != spawnCountBefore)
                            {
                                NotifyAbilityPreviewDirty(true);
                            }
                            break;
                        case TerraformEffectMode.ReplaceTerrain:
                            DrawSelectRow(y, inner.x, "CS_Studio_Effect_TerraformTerrain".Translate(),
                                effect.terraformTerrainDef?.label ?? "CS_Studio_None".Translate(), () => ShowTerraformTerrainSelector(effect),
                                overrideLabelWidth: wideLabelW, overrideFieldWidth: wideFieldW);
                            break;
                    }
                    break;
                case AbilityEffectType.WeatherChange:
                    DrawSelectRow(y, inner.x, "CS_Studio_Effect_WeatherDef".Translate(), !string.IsNullOrEmpty(effect.weatherDefName) ? effect.weatherDefName : "CS_Studio_None".Translate(), () => ShowWeatherDefSelector(effect), overrideLabelWidth: labelW, overrideFieldWidth: inner.width - labelW);
                    y += 26f;
                    string weatherDurStr = effect.weatherDurationTicks.ToString();
                    DrawNumericRowInt(y, inner.x, "CS_Studio_Effect_WeatherDuration".Translate(), ref effect.weatherDurationTicks, ref weatherDurStr, 1, 9999999);
                    string weatherTransStr = effect.weatherTransitionTicks.ToString();
                    DrawNumericRowInt(y, rightX, "CS_Studio_Effect_WeatherTransition".Translate(), ref effect.weatherTransitionTicks, ref weatherTransStr, 0, 99999);
                    break;
            }
        }

        private void ShowAddEffectMenu()
        {
            var options = new List<FloatMenuOption>();
            foreach (AbilityEffectType type in Enum.GetValues(typeof(AbilityEffectType)))
            {
                options.Add(new FloatMenuOption(GetEffectTypeLabel(type), () =>
                {
                    selectedAbility?.effects.Add(CreateDefaultEffectConfig(type));
                    NotifyAbilityPreviewDirty(true);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private AbilityEffectConfig CreateDefaultEffectConfig(AbilityEffectType type)
        {
            var config = new AbilityEffectConfig
            {
                type = type,
                chance = 1f
            };

            switch (type)
            {
                case AbilityEffectType.Damage:
                    config.amount = 10f;
                    config.damageDef = DamageDefOf.Blunt;
                    break;
                case AbilityEffectType.Heal:
                    config.amount = 8f;
                    break;
                case AbilityEffectType.Buff:
                case AbilityEffectType.Debuff:
                    config.duration = 10f;
                    break;
                case AbilityEffectType.Summon:
                    config.summonCount = 1;
                    break;
                case AbilityEffectType.Control:
                    config.duration = 3f;
                    break;
                case AbilityEffectType.WeatherChange:
                    config.weatherDurationTicks = 60000;
                    config.weatherTransitionTicks = 3000;
                    break;
            }

            return config;
        }

        private void ShowDamageDefSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    effect.damageDef = null;
                    NotifyAbilityPreviewDirty();
                })
            };

            var defs = DefDatabase<DamageDef>.AllDefsListForReading;
            var sorted = new List<DamageDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var damageDef in sorted)
            {
                var localDef = damageDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    effect.damageDef = localDef;
                    NotifyAbilityPreviewDirty();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowPawnKindSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    effect.summonKind = null;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var kinds = DefDatabase<PawnKindDef>.AllDefsListForReading;
            var sorted = new List<PawnKindDef>(kinds);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var kind in sorted)
            {
                var localKind = kind;
                string label = localKind.label ?? localKind.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    effect.summonKind = localKind;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowHediffSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    effect.hediffDef = null;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var defs = DefDatabase<HediffDef>.AllDefsListForReading;
            var sorted = new List<HediffDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var hediff in sorted)
            {
                var localHediff = hediff;
                string label = localHediff.label ?? localHediff.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    effect.hediffDef = localHediff;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowProjectileSelector(ModularAbilityDef ability)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    ability.projectileDef = null;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var defs = DefDatabase<ThingDef>.AllDefsListForReading;
            var sorted = new List<ThingDef>();
            foreach (var def in defs)
            {
                if (def.projectile != null)
                {
                    sorted.Add(def);
                }
            }
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var projectileDef in sorted)
            {
                var localDef = projectileDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    ability.projectileDef = localDef;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowFactionSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    effect.summonFactionDef = null;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            foreach (var factionDef in DefDatabase<FactionDef>.AllDefsListForReading.OrderBy(f => f.label ?? f.defName))
            {
                var localDef = factionDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    effect.summonFactionDef = localDef;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowControlModeSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>();
            foreach (ControlEffectMode mode in Enum.GetValues(typeof(ControlEffectMode)))
            {
                var localMode = mode;
                options.Add(new FloatMenuOption(GetControlModeLabel(localMode), () =>
                {
                    effect.controlMode = localMode;
                    NotifyAbilityPreviewDirty(true);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowEffectTypeSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>();
            foreach (AbilityEffectType type in Enum.GetValues(typeof(AbilityEffectType)))
            {
                AbilityEffectType localType = type;
                options.Add(new FloatMenuOption(GetEffectTypeLabel(localType), () => ApplyEffectType(effect, localType)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ApplyEffectType(AbilityEffectConfig effect, AbilityEffectType type)
        {
            if (effect.type == type)
            {
                return;
            }

            effect.type = type;
            effect.amount = 0f;
            effect.duration = 0f;
            effect.chance = 1f;
            effect.damageDef = null;
            effect.hediffDef = null;
            effect.summonKind = null;
            effect.summonCount = 1;
            effect.summonFactionDef = null;
            effect.controlMode = ControlEffectMode.Stun;
            effect.controlMoveDistance = 3;
            effect.terraformMode = TerraformEffectMode.CleanFilth;
            effect.terraformThingDef = null;
            effect.terraformTerrainDef = null;
            effect.terraformSpawnCount = 1;
            effect.canHurtSelf = false;

            AbilityEffectConfig defaults = CreateDefaultEffectConfig(type);
            effect.amount = defaults.amount;
            effect.duration = defaults.duration;
            effect.chance = defaults.chance;
            effect.damageDef = defaults.damageDef;
            effect.hediffDef = defaults.hediffDef;
            effect.summonKind = defaults.summonKind;
            effect.summonCount = defaults.summonCount;
            effect.summonFactionDef = defaults.summonFactionDef;
            effect.controlMode = defaults.controlMode;
            effect.controlMoveDistance = defaults.controlMoveDistance;
            effect.terraformMode = defaults.terraformMode;
            effect.terraformThingDef = defaults.terraformThingDef;
            effect.terraformTerrainDef = defaults.terraformTerrainDef;
            effect.terraformSpawnCount = defaults.terraformSpawnCount;
            effect.canHurtSelf = defaults.canHurtSelf;

            NotifyAbilityPreviewDirty(true);
        }

        private void ShowTerraformModeSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>();
            foreach (TerraformEffectMode mode in Enum.GetValues(typeof(TerraformEffectMode)))
            {
                var localMode = mode;
                options.Add(new FloatMenuOption(GetTerraformModeLabel(localMode), () =>
                {
                    effect.terraformMode = localMode;
                    NotifyAbilityPreviewDirty(true);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowTerraformThingSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    effect.terraformThingDef = null;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var defs = DefDatabase<ThingDef>.AllDefsListForReading;
            var sorted = new List<ThingDef>();
            foreach (var def in defs)
            {
                if (def != null && def.category != ThingCategory.Mote && def.category != ThingCategory.Ethereal)
                {
                    sorted.Add(def);
                }
            }
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var thingDef in sorted)
            {
                var localDef = thingDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    effect.terraformThingDef = localDef;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowTerraformTerrainSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    effect.terraformTerrainDef = null;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            foreach (var terrainDef in DefDatabase<TerrainDef>.AllDefsListForReading.OrderBy(t => t.label ?? t.defName))
            {
                var localDef = terrainDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    effect.terraformTerrainDef = localDef;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string GetControlModeLabel(ControlEffectMode mode)
        {
            return mode switch
            {
                ControlEffectMode.Knockback => "CS_Studio_Effect_ControlMode_Knockback".Translate(),
                ControlEffectMode.Pull => "CS_Studio_Effect_ControlMode_Pull".Translate(),
                _ => "CS_Studio_Effect_ControlMode_Stun".Translate()
            };
        }

        private static string GetTerraformModeLabel(TerraformEffectMode mode)
        {
            return mode switch
            {
                TerraformEffectMode.SpawnThing => "CS_Studio_Effect_TerraformMode_SpawnThing".Translate(),
                TerraformEffectMode.ReplaceTerrain => "CS_Studio_Effect_TerraformMode_ReplaceTerrain".Translate(),
                _ => "CS_Studio_Effect_TerraformMode_CleanFilth".Translate()
            };
        }

        private void ShowWeatherDefSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () =>
                {
                    effect.weatherDefName = string.Empty;
                    NotifyAbilityPreviewDirty(true);
                })
            };

            var defs = DefDatabase<WeatherDef>.AllDefsListForReading;
            var sorted = new List<WeatherDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var weatherDef in sorted)
            {
                var localDef = weatherDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () =>
                {
                    effect.weatherDefName = localDef.defName;
                    NotifyAbilityPreviewDirty(true);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void SwapEffects(int indexA, int indexB)
        {
            if (selectedAbility == null) return;
            if (indexA < 0 || indexB < 0 || indexA >= selectedAbility.effects.Count || indexB >= selectedAbility.effects.Count)
            {
                return;
            }

            var temp = selectedAbility.effects[indexA];
            selectedAbility.effects[indexA] = selectedAbility.effects[indexB];
            selectedAbility.effects[indexB] = temp;
            NotifyAbilityPreviewDirty(true);
        }
    }
}