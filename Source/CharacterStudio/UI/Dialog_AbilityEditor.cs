using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
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
            if (Widgets.ButtonText(new Rect(rect.x + Margin, rect.y + Margin, 80, 24), "CS_Studio_File_New".Translate()))
            {
                CreateNewAbility();
            }
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - 90, rect.y + Margin, 80, 24), "CS_Studio_Btn_Delete".Translate()))
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

                Widgets.Label(new Rect(5, i * RowHeight, viewRect.width - 10, RowHeight), ability.label);
            }
            Widgets.EndScrollView();
        }

        private void DrawAbilityProperties(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect contentRect = rect.ContractedBy(Margin);

            Widgets.BeginScrollView(contentRect, ref propsScrollPos, new Rect(0, 0, contentRect.width - 16, 500));
            
            float y = 0;
            float labelWidth = 100f;
            float fieldWidth = contentRect.width - labelWidth - 30;

            float width = contentRect.width;

            // 基础信息
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Section_AbilityBase".Translate());
            UIHelper.DrawPropertyField(ref y, width, "CS_Studio_Ability_Name".Translate(), ref selectedAbility!.label);
            
            Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Description".Translate());
            selectedAbility.description = Widgets.TextArea(new Rect(labelWidth, y, fieldWidth, 60), selectedAbility.description);
            y += 70;

            UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Cooldown".Translate(), ref selectedAbility.cooldownTicks, 0, 100000);

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
            Rect viewRect = new Rect(0, 0, listRect.width - 16, selectedAbility!.effects.Count * 120); // 每个效果预留高度

            Widgets.BeginScrollView(listRect, ref effectsScrollPos, viewRect);
            
            float cy = 0;
            for (int i = 0; i < selectedAbility.effects.Count; i++)
            {
                var effect = selectedAbility.effects[i];
                DrawEffectItem(new Rect(0, cy, viewRect.width, 110), effect, i);
                cy += 120;
            }

            Widgets.EndScrollView();
        }

        private void DrawEffectItem(Rect rect, AbilityEffectConfig effect, int index)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5);

            // 标题与删除
            Widgets.Label(new Rect(inner.x, inner.y, 150, 24), $"#{index + 1} {effect.type}");
            if (Widgets.ButtonText(new Rect(inner.x + inner.width - 24, inner.y, 24, 24), "X"))
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
                case AbilityEffectType.Summon:
                    Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_PawnKind".Translate());
                    // TODO: 添加 PawnKind 选择器
                    Widgets.Label(new Rect(inner.x + labelWidth, y, fieldWidth * 2 + 10, 24), effect.summonKind?.label ?? "None");
                    break;
                case AbilityEffectType.Buff:
                case AbilityEffectType.Debuff:
                    Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_Hediff".Translate());
                    // TODO: 添加 Hediff 选择器
                    Widgets.Label(new Rect(inner.x + labelWidth, y, fieldWidth * 2 + 10, 24), effect.hediffDef?.label ?? "None");
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
                    selectedAbility?.effects.Add(new AbilityEffectConfig { type = type });
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
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
