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
            float height = 34f + 28f;
            switch (effect.type)
            {
                case AbilityEffectType.Summon:
                case AbilityEffectType.Terraform:
                    height += 28f;
                    break;
            }

            if (effect.type == AbilityEffectType.Damage)
            {
                height += 28f;
            }

            if (effect.type == AbilityEffectType.Control && effect.controlMode != ControlEffectMode.Stun)
            {
                height += 28f;
            }

            if (effect.type == AbilityEffectType.Terraform && effect.terraformMode == TerraformEffectMode.SpawnThing)
            {
                height += 28f;
            }

            return height + 6f;
        }

        private void DrawEffectItem(Rect rect, AbilityEffectConfig effect, int index)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5f);
            float y = inner.y;

            float actionButtonsWidth = 100f;
            Widgets.Label(new Rect(inner.x, y, Mathf.Max(100f, inner.width - actionButtonsWidth - 4f), 24f), GetEffectTypeLabel(effect.type));
            if (DrawCompactIconButton(new Rect(inner.x + inner.width - 100f, y, 30f, 24f), "↑", () => SwapEffects(index, index - 1)))
            {
            }
            if (DrawCompactIconButton(new Rect(inner.x + inner.width - 66f, y, 30f, 24f), "↓", () => SwapEffects(index, index + 1)))
            {
            }
            if (DrawCompactIconButton(new Rect(inner.x + inner.width - 32f, y, 30f, 24f), "X", () =>
            {
                selectedAbility?.effects.RemoveAt(index);
                NotifyAbilityPreviewDirty(true);
            }))
            {
            }
            y += 28f;

            float gap = 8f;
            float colWidth = (inner.width - gap) * 0.5f;
            float labelW = Mathf.Clamp(inner.width * 0.18f, 54f, 82f);
            float fieldW = Mathf.Max(60f, colWidth - labelW);
            float rightX = inner.x + colWidth + gap;

            void DrawNumericRow(float rowY, float x, string label, ref float value, ref string buffer, float min = float.MinValue, float max = float.MaxValue)
            {
                Widgets.Label(new Rect(x, rowY, labelW, 24f), label);
                float before = value;
                UIHelper.TextFieldNumeric(new Rect(x + labelW, rowY, fieldW, 24f), ref value, ref buffer, min, max);
                if (!Mathf.Approximately(value, before))
                {
                    NotifyAbilityPreviewDirty(true);
                }
            }

            void DrawNumericRowInt(float rowY, float x, string label, ref int value, ref string buffer, int min, int max)
            {
                Widgets.Label(new Rect(x, rowY, labelW, 24f), label);
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
                Widgets.Label(new Rect(x, rowY, currentLabelW, 24f), label);
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
                    Widgets.Label(new Rect(inner.x, y, labelW, 24f), "CS_Studio_Effect_CanHurtSelf".Translate());
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
                            Widgets.Label(new Rect(inner.x, y, wideLabelW, 24f), "CS_Studio_Effect_TerraformSpawnCount".Translate());
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