using System;
using System.Collections.Generic;
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

            Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width - 90f, 24), "<b>" + "CS_Studio_Effect_Title".Translate() + "</b>");
            if (Widgets.ButtonText(new Rect(contentRect.x + contentRect.width - 80, contentRect.y, 80, 24), "CS_Studio_Effect_Add".Translate()))
            {
                ShowAddEffectMenu();
            }

            Widgets.Label(new Rect(contentRect.x, contentRect.y + 24f, contentRect.width, 24f), "CS_Studio_Ability_EffectsSummary".Translate(selectedAbility?.effects?.Count ?? 0, selectedAbility?.runtimeComponents?.Count ?? 0));

            float listY = contentRect.y + 52f;
            float listHeight = contentRect.height - 52f;
            Rect listRect = new Rect(contentRect.x, listY, contentRect.width, listHeight);

            if (selectedAbility == null || selectedAbility.effects == null || selectedAbility.effects.Count == 0)
            {
                Widgets.DrawHighlight(listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(listRect.x + 10f, listRect.y + 20f, listRect.width - 20f, 70f), "CS_Studio_Effect_EmptyHint".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonText(new Rect(listRect.x + 30f, listRect.y + 92f, listRect.width - 60f, 28f), "CS_Studio_Effect_Add".Translate()))
                {
                    ShowAddEffectMenu();
                }
                return;
            }

            Rect viewRect = new Rect(0, 0, listRect.width - 16, selectedAbility.effects.Count * 150f);
            Widgets.BeginScrollView(listRect, ref effectsScrollPos, viewRect);
            
            float cy = 0;
            for (int i = 0; i < selectedAbility.effects.Count; i++)
            {
                var effect = selectedAbility.effects[i];
                DrawEffectItem(new Rect(0, cy, viewRect.width, 140), effect, i);
                cy += 150f;
            }

            Widgets.EndScrollView();
        }

        /// <summary>
        /// 右侧三标签容器（效果 / 视觉特效 / 运行时组件）
        /// </summary>
        private void DrawRightTabPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(Margin);

            // 标签按钮行
            float tabW = inner.width / 3f;
            int effectCount = selectedAbility?.effects?.Count ?? 0;
            int vfxCount    = selectedAbility?.visualEffects?.Count ?? 0;
            int rcCount     = selectedAbility?.runtimeComponents?.Count ?? 0;

            string[] tabs = {
                $"CS_Studio_Effect_Title".Translate() + (effectCount > 0 ? $" ({effectCount})" : ""),
                "CS_Studio_VFX_Title".Translate()    + (vfxCount > 0    ? $" ({vfxCount})"    : ""),
                "CS_Studio_Section_RuntimeComponents".Translate().RawText.Split('.')[0] + (rcCount > 0 ? $" ({rcCount})" : "")
            };

            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tabRect = new Rect(inner.x + tabW * i, inner.y, tabW, 26f);
                bool active  = rightPanelTab == i;
                if (active) Widgets.DrawHighlight(tabRect);
                if (Widgets.ButtonText(tabRect, tabs[i]))
                    rightPanelTab = i;
            }

            Rect bodyRect = new Rect(inner.x, inner.y + 28f, inner.width, inner.height - 28f);

            switch (rightPanelTab)
            {
                case 0:  DrawEffectsPanelBody(bodyRect);         break;
                case 1:  DrawVisualEffectsPanelBody(bodyRect);   break;
                default: DrawRCPanelBody(bodyRect);              break;
            }
        }

        /// <summary>
        /// 效果列表正文（原 DrawEffectsPanel 去掉外框）
        /// </summary>
        private void DrawEffectsPanelBody(Rect rect)
        {
            // 添加按钮行
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, rect.width, 24f), "CS_Studio_Effect_Add".Translate()))
                ShowAddEffectMenu();

            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(rect.x, rect.y + 26f, rect.width, 20f),
                "CS_Studio_Ability_EffectsSummary".Translate(
                    selectedAbility?.effects?.Count ?? 0,
                    selectedAbility?.runtimeComponents?.Count ?? 0));
            GUI.color = Color.white;

            float listY = rect.y + 48f;
            float listH = rect.height - 48f;
            Rect  listRect = new Rect(rect.x, listY, rect.width, listH);

            if (selectedAbility == null || selectedAbility.effects == null || selectedAbility.effects.Count == 0)
            {
                Widgets.DrawHighlight(listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = Color.gray;
                Widgets.Label(new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 60f),
                    "CS_Studio_Effect_EmptyHint".Translate());
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Rect viewRect = new Rect(0, 0, listRect.width - 16f, selectedAbility.effects.Count * 150f);
            Widgets.BeginScrollView(listRect, ref effectsScrollPos, viewRect);
            float cy = 0;
            for (int i = 0; i < selectedAbility.effects.Count; i++)
            {
                DrawEffectItem(new Rect(0, cy, viewRect.width, 140f), selectedAbility.effects[i], i);
                cy += 150f;
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// 视觉特效正文
        /// </summary>
        private void DrawVisualEffectsPanelBody(Rect rect)
        {
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, rect.width, 24f), "CS_Studio_VFX_Add".Translate()))
                ShowAddVfxMenu();

            float listY    = rect.y + 28f;
            float listH    = rect.height - 28f;
            Rect  listRect = new Rect(rect.x, listY, rect.width, listH);

            if (selectedAbility == null || selectedAbility.visualEffects == null || selectedAbility.visualEffects.Count == 0)
            {
                Widgets.DrawHighlight(listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = Color.gray;
                Widgets.Label(new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 50f),
                    "CS_Studio_VFX_EmptyHint".Translate());
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            const float ItemH = 120f;
            const float ItemGap = 4f;
            Rect viewRect = new Rect(0, 0, listRect.width - 16f,
                selectedAbility.visualEffects.Count * (ItemH + ItemGap));
            Widgets.BeginScrollView(listRect, ref vfxScrollPos, viewRect);
            float cy = 0;
            for (int i = 0; i < selectedAbility.visualEffects.Count; i++)
            {
                DrawVfxItem(new Rect(0, cy, viewRect.width, ItemH), selectedAbility.visualEffects[i], i);
                cy += ItemH + ItemGap;
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// 运行时组件正文（原 DrawRuntimeComponentsSection 内容拆出，带自有滚动）
        /// </summary>
        private void DrawRCPanelBody(Rect rect)
        {
            if (selectedAbility == null) return;

            if (Widgets.ButtonText(new Rect(rect.x, rect.y, rect.width, 24f),
                "CS_Studio_Runtime_AddComponent".Translate()))
                ShowAddRuntimeComponentMenu();

            float listY    = rect.y + 28f;
            float listH    = rect.height - 28f;
            Rect  listRect = new Rect(rect.x, listY, rect.width, listH);

            if (selectedAbility.runtimeComponents == null || selectedAbility.runtimeComponents.Count == 0)
            {
                Widgets.DrawHighlight(listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = Color.gray;
                Widgets.Label(new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 50f),
                    "CS_Studio_Runtime_Empty".Translate());
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // 计算动态内容高度
            float totalH = 0;
            foreach (var comp in selectedAbility.runtimeComponents)
            {
                if (comp == null) continue;
                float bh = comp.type switch
                {
                    AbilityRuntimeComponentType.QComboWindow       => 86f,
                    AbilityRuntimeComponentType.EShortJump         => 140f,
                    AbilityRuntimeComponentType.RStackDetonation   => 250f,
                    _                                              => 64f
                };
                totalH += bh + 6f;
            }

            Rect viewRect = new Rect(0, 0, listRect.width - 16f, Mathf.Max(totalH, listRect.height));
            Widgets.BeginScrollView(listRect, ref rcScrollPos, viewRect);

            // DrawRuntimeComponentsSection 内部自行遍历所有组件并累加 y
            // 这里传入 y=0（滚动视图局部坐标）和视图宽度即可
            float y2 = 0f;
            DrawRuntimeComponentsInBody(ref y2, viewRect.width);

            Widgets.EndScrollView();
        }

        /// <summary>
        /// 在滚动视图内绘制所有运行时组件块（不含标题/添加按钮，供 DrawRCPanelBody 使用）
        /// </summary>
        private void DrawRuntimeComponentsInBody(ref float y, float width)
        {
            if (selectedAbility == null) return;

            if (selectedAbility.runtimeComponents == null || selectedAbility.runtimeComponents.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0, y, width, 24f), "CS_Studio_Runtime_Empty".Translate());
                GUI.color = Color.white;
                y += 28f;
                return;
            }

            for (int i = 0; i < selectedAbility.runtimeComponents.Count; i++)
            {
                var comp = selectedAbility.runtimeComponents[i];
                if (comp == null) continue;

                float blockHeight = 64f;
                switch (comp.type)
                {
                    case AbilityRuntimeComponentType.QComboWindow:     blockHeight = 86f;  break;
                    case AbilityRuntimeComponentType.EShortJump:       blockHeight = 140f; break;
                    case AbilityRuntimeComponentType.RStackDetonation: blockHeight = 250f; break;
                }

                Rect block = new Rect(0, y, width, blockHeight);
                Widgets.DrawMenuSection(block);
                Rect inner = block.ContractedBy(5f);

                Widgets.Label(new Rect(inner.x, inner.y, inner.width - 100f, 24f),
                    $"#{i + 1} {GetRuntimeComponentTypeLabel(comp.type)}");
                bool enabled = comp.enabled;
                Widgets.Checkbox(new Vector2(inner.x + inner.width - 96f, inner.y + 2f), ref enabled, 24f, false);
                comp.enabled = enabled;

                if (Widgets.ButtonText(new Rect(inner.x + inner.width - 68f, inner.y, 64f, 24f), "X"))
                {
                    selectedAbility.runtimeComponents.RemoveAt(i);
                    i--;
                    y += blockHeight + 6f;
                    continue;
                }

                float rowY  = inner.y + 28f;
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
                    if (Widgets.ButtonText(new Rect(inner.x + labelW, rowY, valueW, 24f),
                        comp.waveDamageDef?.label ?? "CS_Studio_None".Translate()))
                        ShowDamageDefSelectorForRuntime(comp);
                }

                y += blockHeight + 6f;
            }
        }

        private void DrawVisualEffectsPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect contentRect = rect.ContractedBy(Margin);

            // 标题行
            Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width - 82f, 24f),
                "<b>" + "CS_Studio_VFX_Title".Translate() + "</b>");
            if (Widgets.ButtonText(new Rect(contentRect.x + contentRect.width - 72f, contentRect.y, 72f, 24f),
                "CS_Studio_VFX_Add".Translate()))
            {
                ShowAddVfxMenu();
            }

            float listY    = contentRect.y + 28f;
            float listH    = contentRect.height - 28f;
            Rect  listRect = new Rect(contentRect.x, listY, contentRect.width, listH);

            if (selectedAbility == null || selectedAbility.visualEffects == null || selectedAbility.visualEffects.Count == 0)
            {
                Widgets.DrawHighlight(listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = Color.gray;
                Widgets.Label(new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 50f),
                    "CS_Studio_VFX_EmptyHint".Translate());
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            const float ItemH = 120f;
            const float ItemGap = 4f;
            Rect viewRect = new Rect(0, 0, listRect.width - 16f, selectedAbility.visualEffects.Count * (ItemH + ItemGap));
            Widgets.BeginScrollView(listRect, ref vfxScrollPos, viewRect);

            float cy = 0;
            for (int i = 0; i < selectedAbility.visualEffects.Count; i++)
            {
                var vfx = selectedAbility.visualEffects[i];
                DrawVfxItem(new Rect(0, cy, viewRect.width, ItemH), vfx, i);
                cy += ItemH + ItemGap;
            }

            Widgets.EndScrollView();
        }

        private void DrawVfxItem(Rect rect, CharacterStudio.Abilities.AbilityVisualEffectConfig vfx, int index)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5);

            // 标题 + 删除
            string titleLabel = $"#{index + 1} {GetVfxTypeLabel(vfx.type)}";
            Widgets.Label(new Rect(inner.x, inner.y, inner.width - 28f, 24f), titleLabel);
            if (Widgets.ButtonText(new Rect(inner.x + inner.width - 26f, inner.y, 24f, 24f), "X"))
            {
                selectedAbility?.visualEffects.RemoveAt(index);
                return;
            }

            float y       = inner.y + 28f;
            float labelW  = Mathf.Max(56f, Text.CalcSize("CS_Studio_VFX_Target".Translate()).x + 6f);
            float fieldW  = inner.width - labelW - 4f;

            // 特效类型
            Widgets.Label(new Rect(inner.x, y, labelW, 24f), "CS_Studio_VFX_Type".Translate());
            if (Widgets.ButtonText(new Rect(inner.x + labelW, y, fieldW, 24f), GetVfxTypeLabel(vfx.type)))
            {
                var options = new System.Collections.Generic.List<Verse.FloatMenuOption>();
                foreach (CharacterStudio.Abilities.AbilityVisualEffectType t
                    in System.Enum.GetValues(typeof(CharacterStudio.Abilities.AbilityVisualEffectType)))
                {
                    var captured = t;
                    options.Add(new Verse.FloatMenuOption(GetVfxTypeLabel(captured), () => vfx.type = captured));
                }
                Find.WindowStack.Add(new Verse.FloatMenu(options));
            }
            y += RowHeight;

            // 作用目标
            Widgets.Label(new Rect(inner.x, y, labelW, 24f), "CS_Studio_VFX_Target".Translate());
            if (Widgets.ButtonText(new Rect(inner.x + labelW, y, fieldW, 24f), GetVfxTargetLabel(vfx.target)))
            {
                var options = new System.Collections.Generic.List<Verse.FloatMenuOption>();
                foreach (CharacterStudio.Abilities.VisualEffectTarget t
                    in System.Enum.GetValues(typeof(CharacterStudio.Abilities.VisualEffectTarget)))
                {
                    var captured = t;
                    options.Add(new Verse.FloatMenuOption(GetVfxTargetLabel(captured), () => vfx.target = captured));
                }
                Find.WindowStack.Add(new Verse.FloatMenu(options));
            }
            y += RowHeight;

            // 延迟 ticks
            Widgets.Label(new Rect(inner.x, y, labelW, 24f), "CS_Studio_VFX_Delay".Translate());
            string delayStr = vfx.delayTicks.ToString();
            Widgets.TextFieldNumeric(new Rect(inner.x + labelW, y, fieldW, 24f),
                ref vfx.delayTicks, ref delayStr, 0, 600);
            y += RowHeight;

            // 规模
            Widgets.Label(new Rect(inner.x, y, labelW, 24f), "CS_Studio_VFX_Scale".Translate());
            string scaleStr = vfx.scale.ToString("F2");
            Widgets.TextFieldNumeric(new Rect(inner.x + labelW, y, fieldW, 24f),
                ref vfx.scale, ref scaleStr, 0.1f, 5f);
        }

        private void ShowAddVfxMenu()
        {
            var options = new System.Collections.Generic.List<Verse.FloatMenuOption>();
            foreach (CharacterStudio.Abilities.AbilityVisualEffectType t
                in System.Enum.GetValues(typeof(CharacterStudio.Abilities.AbilityVisualEffectType)))
            {
                var captured = t;
                options.Add(new Verse.FloatMenuOption(GetVfxTypeLabel(captured), () =>
                {
                    selectedAbility?.visualEffects.Add(new CharacterStudio.Abilities.AbilityVisualEffectConfig
                    {
                        type        = captured,
                        target      = CharacterStudio.Abilities.VisualEffectTarget.Target,
                        delayTicks  = 0,
                        scale       = 1f
                    });
                }));
            }
            Find.WindowStack.Add(new Verse.FloatMenu(options));
        }

        private static string GetVfxTypeLabel(CharacterStudio.Abilities.AbilityVisualEffectType type)
        {
            return ("CS_Studio_VFX_Type_" + type).Translate();
        }

        private static string GetVfxTargetLabel(CharacterStudio.Abilities.VisualEffectTarget target)
        {
            return ("CS_Studio_VFX_Target_" + target).Translate();
        }

        private void DrawEffectItem(Rect rect, AbilityEffectConfig effect, int index)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5);

            // 标题与删除
            Widgets.Label(new Rect(inner.x, inner.y, 130, 24), $"#{index + 1} {GetEffectTypeLabel(effect.type)}");

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
            // 标签宽度自适应：确保中文标签完整显示
            float labelWidth = Mathf.Max(60f, Text.CalcSize("CS_Studio_Effect_Amount".Translate()).x + 8f);
            float halfW      = (inner.width - 8f) / 2f;
            float fieldWidth = halfW - labelWidth - 4f;

            // 数值（左半）
            Widgets.Label(new Rect(inner.x, y, labelWidth, 24), "CS_Studio_Effect_Amount".Translate());
            string amountStr = effect.amount.ToString();
            Widgets.TextFieldNumeric(new Rect(inner.x + labelWidth, y, fieldWidth, 24), ref effect.amount, ref amountStr);

            // 几率（右半）
            float chanceX     = inner.x + halfW + 8f;
            float chanceLabelW = Mathf.Max(60f, Text.CalcSize("CS_Studio_Effect_Chance".Translate()).x + 8f);
            float chanceFieldW = halfW - chanceLabelW - 4f;
            Widgets.Label(new Rect(chanceX, y, chanceLabelW, 24), "CS_Studio_Effect_Chance".Translate());
            string chanceStr = effect.chance.ToString();
            Widgets.TextFieldNumeric(new Rect(chanceX + chanceLabelW, y, chanceFieldW, 24), ref effect.chance, ref chanceStr, 0, 1);
            
            y += 30;

            // 特定参数 — 使用全行宽，标签自适应宽度
            float extraLabelW = Mathf.Max(72f,
                Text.CalcSize("CS_Studio_Effect_DamageDef".Translate()).x + 8f);
            float extraFieldW = inner.width - extraLabelW;

            switch (effect.type)
            {
                case AbilityEffectType.Damage:
                    Widgets.Label(new Rect(inner.x, y, extraLabelW, 24), "CS_Studio_Effect_DamageDef".Translate());
                    if (Widgets.ButtonText(new Rect(inner.x + extraLabelW, y, extraFieldW, 24),
                        effect.damageDef?.label ?? "CS_Studio_None".Translate()))
                        ShowDamageDefSelector(effect);
                    y += 30;
                    // 自伤选项
                    bool canHurtSelf = effect.canHurtSelf;
                    Widgets.Checkbox(new Vector2(inner.x, y), ref canHurtSelf, 24f);
                    effect.canHurtSelf = canHurtSelf;
                    Widgets.Label(new Rect(inner.x + 28, y, inner.width - 28, 24), "CS_Studio_Effect_CanHurtSelf".Translate());
                    break;
                case AbilityEffectType.Summon:
                    Widgets.Label(new Rect(inner.x, y, extraLabelW, 24), "CS_Studio_Effect_PawnKind".Translate());
                    if (Widgets.ButtonText(new Rect(inner.x + extraLabelW, y, extraFieldW, 24),
                        effect.summonKind?.label ?? "CS_Studio_None".Translate()))
                        ShowPawnKindSelector(effect);
                    y += 30;
                    Widgets.Label(new Rect(inner.x, y, extraLabelW, 24), "CS_Studio_Effect_SummonCount".Translate());
                    string summonCountStr = effect.summonCount.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + extraLabelW, y, extraFieldW, 24),
                        ref effect.summonCount, ref summonCountStr, 1, 100);
                    break;
                case AbilityEffectType.Buff:
                case AbilityEffectType.Debuff:
                    Widgets.Label(new Rect(inner.x, y, extraLabelW, 24), "CS_Studio_Effect_Hediff".Translate());
                    if (Widgets.ButtonText(new Rect(inner.x + extraLabelW, y, extraFieldW, 24),
                        effect.hediffDef?.label ?? "CS_Studio_None".Translate()))
                        ShowHediffSelector(effect);
                    y += 30;
                    Widgets.Label(new Rect(inner.x, y, extraLabelW, 24), "CS_Studio_Effect_Duration".Translate());
                    string buffDurationStr = effect.duration.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + extraLabelW, y, extraFieldW, 24),
                        ref effect.duration, ref buffDurationStr, 0f, 600f);
                    break;
                case AbilityEffectType.Control:
                    Widgets.Label(new Rect(inner.x, y, extraLabelW, 24), "CS_Studio_Effect_Duration".Translate());
                    string controlDurationStr = effect.duration.ToString();
                    Widgets.TextFieldNumeric(new Rect(inner.x + extraLabelW, y, extraFieldW, 24),
                        ref effect.duration, ref controlDurationStr, 0f, 600f);
                    break;
                case AbilityEffectType.Heal:
                case AbilityEffectType.Teleport:
                case AbilityEffectType.Terraform:
                    GUI.color = UIHelper.SubtleColor;
                    Widgets.Label(new Rect(inner.x, y, inner.width, 24), "CS_Studio_Effect_NoExtraParams".Translate());
                    GUI.color = Color.white;
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

                Widgets.Label(new Rect(inner.x, inner.y, inner.width - 100f, 24f), $"#{i + 1} {GetRuntimeComponentTypeLabel(comp.type)}");
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
                options.Add(new FloatMenuOption(GetRuntimeComponentTypeLabel(type), () =>
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
    }
}
