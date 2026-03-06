using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 技能编辑器
    /// 用于创建和编辑模块化技能
    /// </summary>
    public class Dialog_AbilityEditor : Window
    {
        // ─────────────────────────────────────────────
        // 常量
        // ─────────────────────────────────────────────
        private const float LeftPanelWidth = 200f;
        private const float RightPanelWidth = 350f;
        private new const float Margin = 10f;
        private const float RowHeight = 30f;

        // ─────────────────────────────────────────────
        // 状态
        // ─────────────────────────────────────────────
        private List<ModularAbilityDef> abilities = new List<ModularAbilityDef>();
        private ModularAbilityDef? selectedAbility;
        private Vector2 listScrollPos;
        private Vector2 propsScrollPos;
        private Vector2 effectsScrollPos;
        private string validationSummary = string.Empty;

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public Dialog_AbilityEditor(List<ModularAbilityDef> abilityList)
        {
            this.abilities = abilityList;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = true;
            this.resizeable = true;
            this.forcePause = true;

            if (this.abilities.Count == 0)
            {
                // 初始化默认技能
                CreateNewAbility();
            }
            else
            {
                selectedAbility = this.abilities[0];
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30), "CS_Studio_Ability_EditorTitle".Translate());
            Text.Font = GameFont.Small;

            float contentY = 40f;
            float contentHeight = inRect.height - contentY;

            // 左侧列表
            Rect leftRect = new Rect(0, contentY, LeftPanelWidth, contentHeight);
            DrawAbilityList(leftRect);

            // 中间属性
            if (selectedAbility != null)
            {
                float centerWidth = inRect.width - LeftPanelWidth - RightPanelWidth - Margin * 2;
                Rect centerRect = new Rect(LeftPanelWidth + Margin, contentY, centerWidth, contentHeight);
                DrawAbilityProperties(centerRect);

                // 右侧效果
                Rect rightRect = new Rect(inRect.width - RightPanelWidth, contentY, RightPanelWidth, contentHeight);
                DrawEffectsPanel(rightRect);
            }
            else
            {
                Rect centerRect = new Rect(LeftPanelWidth + Margin, contentY, inRect.width - LeftPanelWidth - Margin, contentHeight);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(centerRect, "CS_Studio_Ability_SelectOrCreate".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawAbilityList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            // 工具栏
            if (Widgets.ButtonText(new Rect(rect.x + Margin, rect.y + Margin, 56, 24), "CS_Studio_File_New".Translate()))
            {
                CreateNewAbility();
            }

            if (Widgets.ButtonText(new Rect(rect.x + Margin + 60, rect.y + Margin, 56, 24), "CS_Studio_Panel_Duplicate".Translate()))
            {
                DuplicateSelectedAbility();
            }

            if (Widgets.ButtonText(new Rect(rect.x + rect.width - 66, rect.y + Margin, 56, 24), "CS_Studio_Btn_Delete".Translate()))
            {
                if (selectedAbility != null)
                {
                    abilities.Remove(selectedAbility);
                    if (abilities.Count > 0) selectedAbility = abilities[0];
                    else selectedAbility = null;
                }
            }

            // 列表
            float listY = rect.y + 40;
            float listHeight = rect.height - 50;
            Rect listRect = new Rect(rect.x + Margin, listY, rect.width - Margin * 2, listHeight);
            Rect viewRect = new Rect(0, 0, listRect.width - 16, abilities.Count * RowHeight);

            Widgets.BeginScrollView(listRect, ref listScrollPos, viewRect);
            for (int i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];
                Rect rowRect = new Rect(0, i * RowHeight, viewRect.width, RowHeight);
                
                if (selectedAbility == ability)
                {
                    Widgets.DrawHighlightSelected(rowRect);
                }

                if (Widgets.ButtonInvisible(rowRect))
                {
                    selectedAbility = ability;
                }

                var validation = ability.Validate();
                string statusIcon = validation.IsValid
                    ? (validation.Warnings.Count > 0 ? "⚠" : "✅")
                    : "❌";
                string displayName = string.IsNullOrWhiteSpace(ability.label) ? ability.defName : ability.label;
                Widgets.Label(new Rect(5, i * RowHeight, viewRect.width - 10, RowHeight), $"{statusIcon} {displayName}");
            }
            Widgets.EndScrollView();
        }

        private void DrawAbilityProperties(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect contentRect = rect.ContractedBy(Margin);

            Widgets.BeginScrollView(contentRect, ref propsScrollPos, new Rect(0, 0, contentRect.width - 16, 1200));
            
            float y = 0;
            float labelWidth = 100f;
            float fieldWidth = contentRect.width - labelWidth - 30;

            float width = contentRect.width;

            // 基础信息
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Section_AbilityBase".Translate());
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Ability_DefName".Translate(), ref selectedAbility!.defName);
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Ability_Name".Translate(), ref selectedAbility.label);
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Ability_IconPath".Translate(), ref selectedAbility.iconPath);

            Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Description".Translate());
            selectedAbility.description = Widgets.TextArea(new Rect(labelWidth, y, fieldWidth, 60), selectedAbility.description);
            y += 70;

            UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Cooldown".Translate(), ref selectedAbility.cooldownTicks, 0, 100000);
            UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Warmup".Translate(), ref selectedAbility.warmupTicks, 0, 100000);
            UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Charges".Translate(), ref selectedAbility.charges, 1, 999);

            // 载体设置
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Section_Carrier".Translate());
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Ability_Type".Translate(), selectedAbility.carrierType,
                (AbilityCarrierType[])Enum.GetValues(typeof(AbilityCarrierType)),
                type => type.ToString(),
                val => selectedAbility.carrierType = val);

            // 范围
            if (selectedAbility.carrierType != AbilityCarrierType.Self && selectedAbility.carrierType != AbilityCarrierType.Touch)
            {
                UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Range".Translate(), ref selectedAbility.range, 0, 100);
            }

            // 半径
            if (selectedAbility.carrierType == AbilityCarrierType.Area || selectedAbility.carrierType == AbilityCarrierType.Projectile)
            {
                UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Radius".Translate(), ref selectedAbility.radius, 0, 20);
            }

            if (selectedAbility.carrierType == AbilityCarrierType.Projectile)
            {
                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Ability_Projectile".Translate());
                if (Widgets.ButtonText(new Rect(labelWidth, y, fieldWidth, 24), selectedAbility.projectileDef?.label ?? "CS_Studio_None".Translate()))
                {
                    ShowProjectileSelector(selectedAbility);
                }
                y += RowHeight;
            }

            DrawRuntimeComponentsSection(ref y, width);

            y += 10f;
            if (Widgets.ButtonText(new Rect(0, y, width, 28f), "CS_Studio_Ability_Validate".Translate()))
            {
                var result = selectedAbility.Validate();
                validationSummary = result.IsValid
                    ? (result.Warnings.Count > 0
                        ? "CS_Studio_Ability_ValidWithWarnings".Translate() + " (" + result.Warnings.Count + ")"
                        : "CS_Studio_Ability_Valid".Translate())
                    : "CS_Studio_Ability_Invalid".Translate() + " (" + result.Errors.Count + ")";
            }

            if (!string.IsNullOrEmpty(validationSummary))
            {
                y += 34f;
                Widgets.Label(new Rect(0, y, width, 30f), validationSummary);
            }

            Widgets.EndScrollView();
        }

        private void DrawEffectsPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect contentRect = rect.ContractedBy(Margin);

            // 标题栏
            Widgets.Label(new Rect(contentRect.x, contentRect.y, 100, 24), "<b>" + "CS_Studio_Effect_Title".Translate() + "</b>");
            if (Widgets.ButtonText(new Rect(contentRect.x + contentRect.width - 80, contentRect.y, 80, 24), "CS_Studio_Effect_Add".Translate()))
            {
                ShowAddEffectMenu();
            }

            // 效果列表
            float listY = contentRect.y + 30;
            float listHeight = contentRect.height - 30;
            Rect listRect = new Rect(contentRect.x, listY, contentRect.width, listHeight);
            Rect viewRect = new Rect(0, 0, listRect.width - 16, selectedAbility!.effects.Count * 150); // 每个效果预留高度

            Widgets.BeginScrollView(listRect, ref effectsScrollPos, viewRect);
            
            float cy = 0;
            for (int i = 0; i < selectedAbility.effects.Count; i++)
            {
                var effect = selectedAbility.effects[i];
                DrawEffectItem(new Rect(0, cy, viewRect.width, 140), effect, i);
                cy += 150;
            }

            Widgets.EndScrollView();
        }

        private void DrawEffectItem(Rect rect, AbilityEffectConfig effect, int index)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5);

            // 标题与删除
            Widgets.Label(new Rect(inner.x, inner.y, 130, 24), $"#{index + 1} {effect.type}");

            float buttonX = inner.x + inner.width - 78;
            if (Widgets.ButtonText(new Rect(buttonX, inner.y, 24, 24), "▲") && selectedAbility != null && index > 0)
            {
                SwapEffects(index, index - 1);
                return;
            }
            buttonX += 26;
            if (Widgets.ButtonText(new Rect(buttonX, inner.y, 24, 24), "▼") && selectedAbility != null && index < selectedAbility.effects.Count - 1)
            {
                SwapEffects(index, index + 1);
                return;
            }
            buttonX += 26;
            if (Widgets.ButtonText(new Rect(buttonX, inner.y, 24, 24), "X"))
            {
                selectedAbility?.effects.RemoveAt(index);
                return;
            }

            float y = inner.y + 30;
            float labelWidth = 60f;
            float fieldWidth = (inner.width - labelWidth) / 2 - 10;

            // 数值
            Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_Amount".Translate());
            string amountStr = effect.amount.ToString();
            Widgets.TextFieldNumeric(new Rect(inner.x + labelWidth, y, fieldWidth, 24), ref effect.amount, ref amountStr);

            // 几率
            Widgets.Label(new Rect(inner.x + labelWidth + fieldWidth + 10, y, labelWidth, 24), "CS_Studio_Effect_Chance".Translate());
            string chanceStr = effect.chance.ToString();
            Widgets.TextFieldNumeric(new Rect(inner.x + labelWidth * 2 + fieldWidth + 10, y, fieldWidth, 24), ref effect.chance, ref chanceStr, 0, 1);
            
            y += 30;

            // 特定参数
            switch (effect.type)
            {
                case AbilityEffectType.Damage:
                    Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_DamageDef".Translate());
                    if (Widgets.ButtonText(new Rect(inner.x + labelWidth, y, fieldWidth * 2 + 10, 24), effect.damageDef?.label ?? "CS_Studio_None".Translate()))
                    {
                        ShowDamageDefSelector(effect);
                    }
                    break;
                case AbilityEffectType.Summon:
                    Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_PawnKind".Translate());
                    if (Widgets.ButtonText(new Rect(inner.x + labelWidth, y, fieldWidth * 2 + 10, 24), effect.summonKind?.label ?? "CS_Studio_None".Translate()))
                    {
                        ShowPawnKindSelector(effect);
                    }
                    y += 30;
                    Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_SummonCount".Translate());
                    string summonCountStr = effect.summonCount.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelWidth, y, fieldWidth * 2 + 10, 24), ref effect.summonCount, ref summonCountStr, 1, 100);
                    break;
                case AbilityEffectType.Buff:
                case AbilityEffectType.Debuff:
                    Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_Hediff".Translate());
                    if (Widgets.ButtonText(new Rect(inner.x + labelWidth, y, fieldWidth * 2 + 10, 24), effect.hediffDef?.label ?? "CS_Studio_None".Translate()))
                    {
                        ShowHediffSelector(effect);
                    }
                    y += 30;
                    Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_Duration".Translate());
                    string buffDurationStr = effect.duration.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelWidth, y, fieldWidth * 2 + 10, 24), ref effect.duration, ref buffDurationStr, 0f, 600f);
                    break;
                case AbilityEffectType.Control:
                    Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_Duration".Translate());
                    string controlDurationStr = effect.duration.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelWidth, y, fieldWidth * 2 + 10, 24), ref effect.duration, ref controlDurationStr, 0f, 600f);
                    break;
                case AbilityEffectType.Heal:
                case AbilityEffectType.Teleport:
                case AbilityEffectType.Terraform:
                    Widgets.Label(new Rect(inner.x, y, fieldWidth * 2 + labelWidth + 10, 24), "CS_Studio_Effect_NoExtraParams".Translate());
                    break;
            }
        }

        private void ShowAddEffectMenu()
        {
            var options = new List<FloatMenuOption>();
            foreach (AbilityEffectType type in Enum.GetValues(typeof(AbilityEffectType)))
            {
                options.Add(new FloatMenuOption(type.ToString(), () =>
                {
                    selectedAbility?.effects.Add(CreateDefaultEffectConfig(type));
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
                new FloatMenuOption("CS_Studio_None".Translate(), () => effect.damageDef = null)
            };

            var defs = DefDatabase<DamageDef>.AllDefsListForReading;
            var sorted = new List<DamageDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var damageDef in sorted)
            {
                var localDef = damageDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () => effect.damageDef = localDef));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowPawnKindSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () => effect.summonKind = null)
            };

            var kinds = DefDatabase<PawnKindDef>.AllDefsListForReading;
            var sorted = new List<PawnKindDef>(kinds);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var kind in sorted)
            {
                var localKind = kind;
                string label = localKind.label ?? localKind.defName;
                options.Add(new FloatMenuOption(label, () => effect.summonKind = localKind));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowHediffSelector(AbilityEffectConfig effect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () => effect.hediffDef = null)
            };

            var defs = DefDatabase<HediffDef>.AllDefsListForReading;
            var sorted = new List<HediffDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var hediff in sorted)
            {
                var localHediff = hediff;
                string label = localHediff.label ?? localHediff.defName;
                options.Add(new FloatMenuOption(label, () => effect.hediffDef = localHediff));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowProjectileSelector(ModularAbilityDef ability)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () => ability.projectileDef = null)
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
                options.Add(new FloatMenuOption(label, () => ability.projectileDef = localDef));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawRuntimeComponentsSection(ref float y, float width)
        {
            if (selectedAbility == null)
            {
                return;
            }

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Section_RuntimeComponents".Translate());

            if (Widgets.ButtonText(new Rect(0, y, width, 24f), "CS_Studio_Runtime_AddComponent".Translate()))
            {
                ShowAddRuntimeComponentMenu();
            }
            y += 28f;

            if (selectedAbility.runtimeComponents == null || selectedAbility.runtimeComponents.Count == 0)
            {
                Widgets.Label(new Rect(0, y, width, 24f), "CS_Studio_Runtime_Empty".Translate());
                y += 28f;
                return;
            }

            for (int i = 0; i < selectedAbility.runtimeComponents.Count; i++)
            {
                var comp = selectedAbility.runtimeComponents[i];
                if (comp == null)
                {
                    continue;
                }

                float blockHeight = 64f;
                switch (comp.type)
                {
                    case AbilityRuntimeComponentType.QComboWindow:
                        blockHeight = 86f;
                        break;
                    case AbilityRuntimeComponentType.EShortJump:
                        blockHeight = 140f;
                        break;
                    case AbilityRuntimeComponentType.RStackDetonation:
                        blockHeight = 250f;
                        break;
                }

                Rect block = new Rect(0, y, width, blockHeight);
                Widgets.DrawMenuSection(block);
                Rect inner = block.ContractedBy(5f);

                Widgets.Label(new Rect(inner.x, inner.y, inner.width - 100f, 24f), $"#{i + 1} {comp.type}");
                bool enabled = comp.enabled;
                Widgets.Checkbox(new Vector2(inner.x + inner.width - 96f, inner.y + 2f), ref enabled, 24f, false);
                comp.enabled = enabled;

                if (Widgets.ButtonText(new Rect(inner.x + inner.width - 68f, inner.y, 64f, 24f), "X"))
                {
                    selectedAbility.runtimeComponents.RemoveAt(i);
                    y += blockHeight + 6f;
                    continue;
                }

                float rowY = inner.y + 28f;
                float labelW = 160f;
                float valueW = inner.width - labelW - 6f;

                if (comp.type == AbilityRuntimeComponentType.QComboWindow)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_QWindowTicks".Translate());
                    string s = comp.comboWindowTicks.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.comboWindowTicks, ref s, 1, 9999);
                }
                else if (comp.type == AbilityRuntimeComponentType.EShortJump)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ECooldownTicks".Translate());
                    string cd = comp.cooldownTicks.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.cooldownTicks, ref cd, 0, 99999);
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_EJumpDistance".Translate());
                    string dist = comp.jumpDistance.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.jumpDistance, ref dist, 1, 100);
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_EFindRadius".Translate());
                    string find = comp.findCellRadius.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.findCellRadius, ref find, 0, 30);
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ETriggerEffects".Translate());
                    bool trigger = comp.triggerAbilityEffectsAfterJump;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref trigger, 24f, false);
                    comp.triggerAbilityEffectsAfterJump = trigger;
                }
                else if (comp.type == AbilityRuntimeComponentType.RStackDetonation)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RRequiredStacks".Translate());
                    string stacks = comp.requiredStacks.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.requiredStacks, ref stacks, 1, 999);
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RDelayTicks".Translate());
                    string delay = comp.delayTicks.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.delayTicks, ref delay, 0, 99999);
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RWave1".Translate());
                    string w1r = comp.wave1Radius.ToString();
                    string w1d = comp.wave1Damage.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave1Radius, ref w1r, 0.1f, 99f);
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave1Damage, ref w1d, 1f, 99999f);
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RWave2".Translate());
                    string w2r = comp.wave2Radius.ToString();
                    string w2d = comp.wave2Damage.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave2Radius, ref w2r, 0.1f, 99f);
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave2Damage, ref w2d, 1f, 99999f);
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RWave3".Translate());
                    string w3r = comp.wave3Radius.ToString();
                    string w3d = comp.wave3Damage.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave3Radius, ref w3r, 0.1f, 99f);
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave3Damage, ref w3d, 1f, 99999f);
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RDamageDef".Translate());
                    if (Widgets.ButtonText(new Rect(inner.x + labelW, rowY, valueW, 24f), comp.waveDamageDef?.label ?? "CS_Studio_None".Translate()))
                    {
                        ShowDamageDefSelectorForRuntime(comp);
                    }
                }

                y += blockHeight + 6f;
            }
        }

        private void ShowAddRuntimeComponentMenu()
        {
            if (selectedAbility == null)
            {
                return;
            }

            var options = new List<FloatMenuOption>();
            foreach (AbilityRuntimeComponentType type in Enum.GetValues(typeof(AbilityRuntimeComponentType)))
            {
                options.Add(new FloatMenuOption(type.ToString(), () =>
                {
                    selectedAbility.runtimeComponents ??= new List<AbilityRuntimeComponentConfig>();
                    selectedAbility.runtimeComponents.Add(CreateDefaultRuntimeComponent(type));
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static AbilityRuntimeComponentConfig CreateDefaultRuntimeComponent(AbilityRuntimeComponentType type)
        {
            var config = new AbilityRuntimeComponentConfig
            {
                type = type,
                enabled = true
            };

            if (type == AbilityRuntimeComponentType.RStackDetonation)
            {
                config.waveDamageDef = DamageDefOf.Bomb;
            }

            return config;
        }

        private void ShowDamageDefSelectorForRuntime(AbilityRuntimeComponentConfig component)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_None".Translate(), () => component.waveDamageDef = null)
            };

            var defs = DefDatabase<DamageDef>.AllDefsListForReading;
            var sorted = new List<DamageDef>(defs);
            sorted.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));

            foreach (var damageDef in sorted)
            {
                var localDef = damageDef;
                string label = localDef.label ?? localDef.defName;
                options.Add(new FloatMenuOption(label, () => component.waveDamageDef = localDef));
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
        }

        private void DuplicateSelectedAbility()
        {
            if (selectedAbility == null) return;

            var copy = selectedAbility.Clone();
            copy.defName = $"{selectedAbility.defName}_Copy_{DateTime.Now.Ticks}";
            copy.label = (selectedAbility.label ?? "Ability") + " Copy";
            abilities.Add(copy);
            selectedAbility = copy;
        }

        private void CreateNewAbility()
        {
            var newAbility = new ModularAbilityDef
            {
                defName = $"CS_Ability_{DateTime.Now.Ticks}",
                label = "New Ability"
            };
            abilities.Add(newAbility);
            selectedAbility = newAbility;
        }
    }
}
