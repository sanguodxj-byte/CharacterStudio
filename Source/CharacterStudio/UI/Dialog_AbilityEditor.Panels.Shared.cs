using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        /// <summary>
        /// 右侧三标签容器（效果 / 视觉特效 / 运行时组件）
        /// </summary>
        private void DrawRightTabPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(Margin);

            bool DrawBarButton(Rect buttonRect, string label, Action action, bool active = false)
            {
                Widgets.DrawBoxSolid(buttonRect, active ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
                Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), active ? UIHelper.AccentColor : UIHelper.AccentSoftColor);
                GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
                Widgets.DrawBox(buttonRect, 1);
                GUI.color = Color.white;

                GameFont oldFont = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = active ? Color.white : UIHelper.HeaderColor;
                Widgets.Label(buttonRect, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = oldFont;

                if (!Widgets.ButtonInvisible(buttonRect)) return false;
                action();
                return true;
            }

            float tabW = inner.width / 4f;
            int effectCount = selectedAbility?.effects?.Count ?? 0;
            int vfxCount = selectedAbility?.visualEffects?.Count ?? 0;
            int rcCount = selectedAbility?.runtimeComponents?.Count ?? 0;
            bool hasPreviewContent = effectCount > 0 || vfxCount > 0 || rcCount > 0;

            string[] tabs = {
                $"CS_Studio_Effect_Title".Translate() + (effectCount > 0 ? $" ({effectCount})" : ""),
                "CS_Studio_VFX_Title".Translate() + (vfxCount > 0 ? $" ({vfxCount})" : ""),
                "CS_Studio_Section_RuntimeComponents".Translate().RawText.Split('.')[0] + (rcCount > 0 ? $" ({rcCount})" : ""),
                "CS_Studio_Ability_PreviewTab".Translate() + (hasPreviewContent ? " •" : string.Empty)
            };

            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tabRect = new Rect(inner.x + tabW * i, inner.y, tabW, 26f);
                bool active = rightPanelTab == i;
                if (DrawBarButton(tabRect, tabs[i], () =>
                {
                    rightPanelTab = i;
                }, active))
                {
                }
            }

            Rect bodyRect = new Rect(inner.x, inner.y + 28f, inner.width, inner.height - 28f);

            switch (rightPanelTab)
            {
                case 0: DrawEffectsPanelBody(bodyRect); break;
                case 1: DrawVisualEffectsPanelBody(bodyRect); break;
                case 2: DrawRCPanelBody(bodyRect); break;
                default: DrawAbilityPreviewPanelBody(bodyRect); break;
            }
        }

        private void DrawEffectsPanelBody(Rect rect)
        {
            Rect addRect = new Rect(rect.x, rect.y, rect.width, 24f);
            Widgets.DrawBoxSolid(addRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(addRect.x, addRect.yMax - 2f, addRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = Mouse.IsOver(addRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(addRect, 1);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(addRect, "CS_Studio_Effect_Add".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(addRect))
                ShowAddEffectMenu();

            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(rect.x, rect.y + 26f, rect.width, 20f),
                "CS_Studio_Ability_EffectsSummary".Translate(
                    selectedAbility?.effects?.Count ?? 0,
                    selectedAbility?.runtimeComponents?.Count ?? 0));
            GUI.color = Color.white;

            float listY = rect.y + 48f;
            float listH = rect.height - 48f;
            Rect listRect = new Rect(rect.x, listY, rect.width, listH);

            if (selectedAbility == null || selectedAbility.effects == null || selectedAbility.effects.Count == 0)
            {
                Widgets.DrawHighlight(listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 60f),
                    "CS_Studio_Effect_EmptyHint".Translate());
                GUI.color = Color.white;
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

        private void DrawVisualEffectsPanelBody(Rect rect)
        {
            Rect addRect = new Rect(rect.x, rect.y, rect.width, 24f);
            Widgets.DrawBoxSolid(addRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(addRect.x, addRect.yMax - 2f, addRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = Mouse.IsOver(addRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(addRect, 1);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(addRect, "CS_Studio_VFX_Add".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(addRect))
                ShowAddVfxMenu();

            float listY = rect.y + 28f;
            float listH = rect.height - 28f;
            Rect listRect = new Rect(rect.x, listY, rect.width, listH);

            if (selectedAbility == null || selectedAbility.visualEffects == null || selectedAbility.visualEffects.Count == 0)
            {
                Widgets.DrawHighlight(listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 50f),
                    "CS_Studio_VFX_EmptyHint".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            const float ItemH = 188f;
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

        private void DrawRCPanelBody(Rect rect)
        {
            if (selectedAbility == null) return;

            float libraryHeight = DrawRuntimeComponentLibrary(rect);
            float listY = rect.y + libraryHeight + 6f;
            float listH = Mathf.Max(0f, rect.height - libraryHeight - 6f);
            Rect listRect = new Rect(rect.x, listY, rect.width, listH);

            if (selectedAbility.runtimeComponents == null || selectedAbility.runtimeComponents.Count == 0)
            {
                Widgets.DrawHighlight(listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 50f),
                    "CS_Studio_Runtime_Empty".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float totalH = 0f;
            foreach (var comp in selectedAbility.runtimeComponents)
            {
                if (comp == null) continue;
                totalH += GetRuntimeComponentBlockHeight(comp.type) + 6f;
            }

            Rect viewRect = new Rect(0, 0, listRect.width - 16f, Mathf.Max(totalH, listRect.height));
            Widgets.BeginScrollView(listRect, ref rcScrollPos, viewRect);

            float y2 = 0f;
            DrawRuntimeComponentsInBody(ref y2, viewRect.width);

            Widgets.EndScrollView();
        }

        private float DrawRuntimeComponentLibrary(Rect rect)
        {
            const float headerHeight = 20f;
            const float gap = 4f;
            const float buttonHeight = 20f;
            const float minButtonWidth = 84f;

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, headerHeight);
            Widgets.Label(headerRect, "CS_Studio_Runtime_AddComponent".Translate());

            float contentY = headerRect.yMax + gap;
            float availableWidth = rect.width;
            int columnCount = Mathf.Max(1, Mathf.FloorToInt((availableWidth + gap) / (minButtonWidth + gap)));
            float buttonWidth = (availableWidth - ((columnCount - 1) * gap)) / columnCount;

            int index = 0;
            foreach (AbilityRuntimeComponentType type in GetRuntimeComponentLibraryTypes())
            {
                int row = index / columnCount;
                int col = index % columnCount;
                Rect buttonRect = new Rect(
                    rect.x + col * (buttonWidth + gap),
                    contentY + row * (buttonHeight + gap),
                    buttonWidth,
                    buttonHeight);

                string tooltip = GetRuntimeComponentTypeLabel(type) + "\n" + GetRuntimeComponentTypeDescription(type);
                TooltipHandler.TipRegion(buttonRect, tooltip);
                DrawPanelButton(buttonRect, GetRuntimeComponentTypeLabel(type), () => AddRuntimeComponent(type));
                index++;
            }

            int rowCount = Mathf.Max(1, Mathf.CeilToInt(index / (float)columnCount));
            return headerHeight + gap + rowCount * buttonHeight + Mathf.Max(0, rowCount - 1) * gap;
        }

        private bool DrawPanelButton(Rect buttonRect, string label, Action action, bool accent = false)
        {
            Widgets.DrawBoxSolid(buttonRect, accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), accent ? UIHelper.AccentColor : UIHelper.AccentSoftColor);
            GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(buttonRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = accent ? Color.white : UIHelper.HeaderColor;
            Widgets.Label(buttonRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            if (Widgets.ButtonInvisible(buttonRect))
            {
                action();
                return true;
            }

            return false;
        }

        private bool DrawCompactIconButton(Rect buttonRect, string label, Action action, bool accent = false)
        {
            Widgets.DrawBoxSolid(buttonRect, accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), accent ? UIHelper.AccentColor : new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(buttonRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = accent ? Color.white : UIHelper.HeaderColor;
            Widgets.Label(buttonRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            if (Widgets.ButtonInvisible(buttonRect))
            {
                action();
                return true;
            }

            return false;
        }

        private bool DrawSelectionFieldButton(Rect buttonRect, string label, Action action)
        {
            Widgets.DrawBoxSolid(buttonRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f), new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
            Widgets.DrawBox(buttonRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(buttonRect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            if (Widgets.ButtonInvisible(buttonRect))
            {
                action();
                return true;
            }

            return false;
        }

        private static float GetRuntimeComponentBlockHeight(AbilityRuntimeComponentType type)
        {
            return type switch
            {
                AbilityRuntimeComponentType.QComboWindow => 86f,
                AbilityRuntimeComponentType.HotkeyOverride => 164f,
                AbilityRuntimeComponentType.FollowupCooldownGate => 112f,
                AbilityRuntimeComponentType.SmartJump => 244f,
                AbilityRuntimeComponentType.EShortJump => 192f,
                AbilityRuntimeComponentType.RStackDetonation => 250f,
                AbilityRuntimeComponentType.PeriodicPulse => 112f,
                AbilityRuntimeComponentType.KillRefresh => 112f,
                AbilityRuntimeComponentType.ShieldAbsorb => 164f,
                AbilityRuntimeComponentType.ChainBounce => 112f,
                AbilityRuntimeComponentType.ExecuteBonusDamage => 112f,
                AbilityRuntimeComponentType.MissingHealthBonusDamage => 112f,
                AbilityRuntimeComponentType.FullHealthBonusDamage => 112f,
                AbilityRuntimeComponentType.NearbyEnemyBonusDamage => 138f,
                AbilityRuntimeComponentType.IsolatedTargetBonusDamage => 112f,
                AbilityRuntimeComponentType.MarkDetonation => 164f,
                AbilityRuntimeComponentType.ComboStacks => 138f,
                AbilityRuntimeComponentType.HitSlowField => 190f,
                AbilityRuntimeComponentType.PierceBonusDamage => 138f,
                AbilityRuntimeComponentType.DashEmpoweredStrike => 112f,
                AbilityRuntimeComponentType.HitHeal => 112f,
                AbilityRuntimeComponentType.HitCooldownRefund => 112f,
                AbilityRuntimeComponentType.ProjectileSplit => 138f,
                AbilityRuntimeComponentType.FlightState => 112f,
                _ => 64f
            };
        }

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

                float blockHeight = GetRuntimeComponentBlockHeight(comp.type);

                Rect block = new Rect(0, y, width, blockHeight);
                Widgets.DrawBoxSolid(block, UIHelper.PanelFillSoftColor);
                Widgets.DrawBoxSolid(new Rect(block.x, block.yMax - 2f, block.width, 2f), UIHelper.AccentSoftColor);
                GUI.color = UIHelper.BorderColor;
                Widgets.DrawBox(block, 1);
                GUI.color = Color.white;
                Rect inner = block.ContractedBy(5f);

                Widgets.Label(new Rect(inner.x, inner.y, inner.width - 100f, 24f),
                    $"#{i + 1} {GetRuntimeComponentTypeLabel(comp.type)}");
                bool enabled = comp.enabled;
                Widgets.Checkbox(new Vector2(inner.x + inner.width - 96f, inner.y + 2f), ref enabled, 24f, false);
                if (comp.enabled != enabled)
                {
                    comp.enabled = enabled;
                    NotifyAbilityPreviewDirty();
                }

                if (DrawCompactIconButton(new Rect(inner.x + inner.width - 68f, inner.y, 64f, 24f), "X", () =>
                {
                    selectedAbility.runtimeComponents.RemoveAt(i);
                    NotifyAbilityPreviewDirty(true);
                }))
                {
                    i--;
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
                    int comboWindowBefore = comp.comboWindowTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.comboWindowTicks, ref s, 1, 9999);
                    if (comp.comboWindowTicks != comboWindowBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.HotkeyOverride)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_HotkeyOverrideSlot".Translate());
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f),
                        ($"CS_Studio_Ability_Hotkey_{comp.overrideHotkeySlot}").Translate(), () =>
                        {
                            var options = new List<FloatMenuOption>();
                            foreach (AbilityRuntimeHotkeySlot slot in Enum.GetValues(typeof(AbilityRuntimeHotkeySlot)))
                            {
                                AbilityRuntimeHotkeySlot localSlot = slot;
                                options.Add(new FloatMenuOption(($"CS_Studio_Ability_Hotkey_{localSlot}").Translate(), () =>
                                {
                                    comp.overrideHotkeySlot = localSlot;
                                    NotifyAbilityPreviewDirty(true);
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        }))
                    {
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_HotkeyOverrideAbility".Translate());
                    string overrideAbilityLabel = string.IsNullOrWhiteSpace(comp.overrideAbilityDefName)
                        ? "CS_Studio_None".Translate()
                        : comp.overrideAbilityDefName;
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f), overrideAbilityLabel, () => ShowAbilityDefSelectorForRuntime(comp)))
                    {
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_HotkeyOverrideAbilityDefName".Translate());
                    string overrideAbilityDefName = comp.overrideAbilityDefName ?? string.Empty;
                    string overrideAbilityBefore = overrideAbilityDefName;
                    overrideAbilityDefName = Widgets.TextField(new Rect(inner.x + labelW, rowY, valueW, 24f), overrideAbilityDefName);
                    if (!string.Equals(overrideAbilityBefore, overrideAbilityDefName, StringComparison.Ordinal))
                    {
                        comp.overrideAbilityDefName = overrideAbilityDefName;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_HotkeyOverrideDuration".Translate());
                    string duration = comp.overrideDurationTicks.ToString();
                    int durationBefore = comp.overrideDurationTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.overrideDurationTicks, ref duration, 1, 99999);
                    if (comp.overrideDurationTicks != durationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.FollowupCooldownGate)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_FollowupCooldownSlot".Translate());
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f),
                        ($"CS_Studio_Ability_Hotkey_{comp.followupCooldownHotkeySlot}").Translate(), () =>
                        {
                            var options = new List<FloatMenuOption>();
                            foreach (AbilityRuntimeHotkeySlot slot in Enum.GetValues(typeof(AbilityRuntimeHotkeySlot)))
                            {
                                AbilityRuntimeHotkeySlot localSlot = slot;
                                options.Add(new FloatMenuOption(($"CS_Studio_Ability_Hotkey_{localSlot}").Translate(), () =>
                                {
                                    comp.followupCooldownHotkeySlot = localSlot;
                                    NotifyAbilityPreviewDirty(true);
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        }))
                    {
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_FollowupCooldownTicks".Translate());
                    string cooldownGate = comp.followupCooldownTicks.ToString();
                    int cooldownGateBefore = comp.followupCooldownTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.followupCooldownTicks, ref cooldownGate, 1, 99999);
                    if (comp.followupCooldownTicks != cooldownGateBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.SmartJump || comp.type == AbilityRuntimeComponentType.EShortJump)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ECooldownTicks".Translate());
                    string cd = comp.cooldownTicks.ToString();
                    int cooldownBefore = comp.cooldownTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.cooldownTicks, ref cd, 0, 99999);
                    if (comp.cooldownTicks != cooldownBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_EJumpDistance".Translate());
                    string dist = comp.jumpDistance.ToString();
                    int jumpDistanceBefore = comp.jumpDistance;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.jumpDistance, ref dist, 1, 100);
                    if (comp.jumpDistance != jumpDistanceBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_EFindRadius".Translate());
                    string find = comp.findCellRadius.ToString();
                    int findRadiusBefore = comp.findCellRadius;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.findCellRadius, ref find, 0, 30);
                    if (comp.findCellRadius != findRadiusBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ETriggerEffects".Translate());
                    bool trigger = comp.triggerAbilityEffectsAfterJump;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref trigger, 24f, false);
                    if (comp.triggerAbilityEffectsAfterJump != trigger)
                    {
                        comp.triggerAbilityEffectsAfterJump = trigger;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    if (comp.type == AbilityRuntimeComponentType.SmartJump)
                    {
                        Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SmartJumpUseMouse".Translate());
                        bool useMouse = comp.useMouseTargetCell;
                        Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref useMouse, 24f, false);
                        if (comp.useMouseTargetCell != useMouse)
                        {
                            comp.useMouseTargetCell = useMouse;
                            NotifyAbilityPreviewDirty(true);
                        }
                        rowY += 26f;

                        Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SmartJumpOffset".Translate());
                        string offset = comp.smartCastOffsetCells.ToString();
                        int offsetBefore = comp.smartCastOffsetCells;
                        Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.smartCastOffsetCells, ref offset, 1, 100);
                        if (comp.smartCastOffsetCells != offsetBefore)
                        {
                            NotifyAbilityPreviewDirty(true);
                        }
                        rowY += 26f;

                        Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SmartJumpClamp".Translate());
                        bool clamp = comp.smartCastClampToMaxDistance;
                        Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref clamp, 24f, false);
                        if (comp.smartCastClampToMaxDistance != clamp)
                        {
                            comp.smartCastClampToMaxDistance = clamp;
                            NotifyAbilityPreviewDirty(true);
                        }
                        rowY += 26f;

                        Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SmartJumpFallback".Translate());
                        bool fallback = comp.smartCastAllowFallbackForward;
                        Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref fallback, 24f, false);
                        if (comp.smartCastAllowFallbackForward != fallback)
                        {
                            comp.smartCastAllowFallbackForward = fallback;
                            NotifyAbilityPreviewDirty(true);
                        }
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.RStackDetonation)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RRequiredStacks".Translate());
                    string stacks = comp.requiredStacks.ToString();
                    int requiredStacksBefore = comp.requiredStacks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.requiredStacks, ref stacks, 1, 999);
                    if (comp.requiredStacks != requiredStacksBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RDelayTicks".Translate());
                    string delay = comp.delayTicks.ToString();
                    int delayBefore = comp.delayTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.delayTicks, ref delay, 0, 99999);
                    if (comp.delayTicks != delayBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RWave1".Translate());
                    string w1r = comp.wave1Radius.ToString();
                    string w1d = comp.wave1Damage.ToString();
                    float wave1RadiusBefore = comp.wave1Radius;
                    float wave1DamageBefore = comp.wave1Damage;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave1Radius, ref w1r, 0.1f, 99f);
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave1Damage, ref w1d, 1f, 99999f);
                    if (Math.Abs(comp.wave1Radius - wave1RadiusBefore) > 0.001f || Math.Abs(comp.wave1Damage - wave1DamageBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RWave2".Translate());
                    string w2r = comp.wave2Radius.ToString();
                    string w2d = comp.wave2Damage.ToString();
                    float wave2RadiusBefore = comp.wave2Radius;
                    float wave2DamageBefore = comp.wave2Damage;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave2Radius, ref w2r, 0.1f, 99f);
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave2Damage, ref w2d, 1f, 99999f);
                    if (Math.Abs(comp.wave2Radius - wave2RadiusBefore) > 0.001f || Math.Abs(comp.wave2Damage - wave2DamageBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RWave3".Translate());
                    string w3r = comp.wave3Radius.ToString();
                    string w3d = comp.wave3Damage.ToString();
                    float wave3RadiusBefore = comp.wave3Radius;
                    float wave3DamageBefore = comp.wave3Damage;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave3Radius, ref w3r, 0.1f, 99f);
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave3Damage, ref w3d, 1f, 99999f);
                    if (Math.Abs(comp.wave3Radius - wave3RadiusBefore) > 0.001f || Math.Abs(comp.wave3Damage - wave3DamageBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RDamageDef".Translate());
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f),
                        comp.waveDamageDef?.label ?? "CS_Studio_None".Translate(), () => ShowDamageDefSelectorForRuntime(comp)))
                    {
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.PeriodicPulse)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_PulseInterval".Translate());
                    string interval = comp.pulseIntervalTicks.ToString();
                    int intervalBefore = comp.pulseIntervalTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pulseIntervalTicks, ref interval, 1, 99999);
                    if (comp.pulseIntervalTicks != intervalBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_PulseDuration".Translate());
                    string duration = comp.pulseTotalTicks.ToString();
                    int durationBefore = comp.pulseTotalTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pulseTotalTicks, ref duration, 1, 99999);
                    if (comp.pulseTotalTicks != durationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_PulseImmediate".Translate());
                    bool immediate = comp.pulseStartsImmediately;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref immediate, 24f, false);
                    if (comp.pulseStartsImmediately != immediate)
                    {
                        comp.pulseStartsImmediately = immediate;
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.KillRefresh)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_KillRefreshHotkeySlot".Translate());
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f),
                        ($"CS_Studio_Ability_Hotkey_{comp.killRefreshHotkeySlot}").Translate(), () =>
                        {
                            var options = new List<FloatMenuOption>();
                            foreach (AbilityRuntimeHotkeySlot slot in Enum.GetValues(typeof(AbilityRuntimeHotkeySlot)))
                            {
                                AbilityRuntimeHotkeySlot localSlot = slot;
                                options.Add(new FloatMenuOption(($"CS_Studio_Ability_Hotkey_{localSlot}").Translate(), () =>
                                {
                                    comp.killRefreshHotkeySlot = localSlot;
                                    NotifyAbilityPreviewDirty(true);
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        }))
                    {
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_KillRefreshPercent".Translate());
                    string refresh = comp.killRefreshCooldownPercent.ToString();
                    float refreshBefore = comp.killRefreshCooldownPercent;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.killRefreshCooldownPercent, ref refresh, 0.01f, 1f);
                    if (Math.Abs(comp.killRefreshCooldownPercent - refreshBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.ShieldAbsorb)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ShieldMaxDamage".Translate());
                    string maxDamage = comp.shieldMaxDamage.ToString();
                    float maxDamageBefore = comp.shieldMaxDamage;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.shieldMaxDamage, ref maxDamage, 1f, 99999f);
                    if (Math.Abs(comp.shieldMaxDamage - maxDamageBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ShieldDuration".Translate());
                    string shieldDuration = comp.shieldDurationTicks.ToString();
                    float shieldDurationBefore = comp.shieldDurationTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.shieldDurationTicks, ref shieldDuration, 1f, 99999f);
                    if (Math.Abs(comp.shieldDurationTicks - shieldDurationBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ShieldHealRatio".Translate());
                    string healRatio = comp.shieldHealRatio.ToString();
                    float healRatioBefore = comp.shieldHealRatio;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.shieldHealRatio, ref healRatio, 0f, 10f);
                    if (Math.Abs(comp.shieldHealRatio - healRatioBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ShieldBonusDamageRatio".Translate());
                    string bonusRatio = comp.shieldBonusDamageRatio.ToString();
                    float bonusRatioBefore = comp.shieldBonusDamageRatio;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.shieldBonusDamageRatio, ref bonusRatio, 0f, 10f);
                    if (Math.Abs(comp.shieldBonusDamageRatio - bonusRatioBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.ChainBounce)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ChainBounceCount".Translate());
                    string bounceCount = comp.maxBounceCount.ToString();
                    int bounceCountBefore = comp.maxBounceCount;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.maxBounceCount, ref bounceCount, 1, 99);
                    if (comp.maxBounceCount != bounceCountBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ChainBounceRange".Translate());
                    string bounceRange = comp.bounceRange.ToString();
                    float bounceRangeBefore = comp.bounceRange;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.bounceRange, ref bounceRange, 0.1f, 99f);
                    if (Math.Abs(comp.bounceRange - bounceRangeBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ChainBounceFalloff".Translate());
                    string falloff = comp.bounceDamageFalloff.ToString();
                    float falloffBefore = comp.bounceDamageFalloff;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.bounceDamageFalloff, ref falloff, 0f, 0.95f);
                    if (Math.Abs(comp.bounceDamageFalloff - falloffBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.ExecuteBonusDamage)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ExecuteThresholdPercent".Translate());
                    string threshold = comp.executeThresholdPercent.ToString();
                    float thresholdBefore = comp.executeThresholdPercent;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.executeThresholdPercent, ref threshold, 0.01f, 0.99f);
                    if (Math.Abs(comp.executeThresholdPercent - thresholdBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ExecuteBonusDamageScale".Translate());
                    string bonusScale = comp.executeBonusDamageScale.ToString();
                    float bonusScaleBefore = comp.executeBonusDamageScale;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.executeBonusDamageScale, ref bonusScale, 0.01f, 10f);
                    if (Math.Abs(comp.executeBonusDamageScale - bonusScaleBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.MissingHealthBonusDamage)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_MissingHealthBonusPerTenPercent".Translate());
                    string bonusPerTen = comp.missingHealthBonusPerTenPercent.ToString();
                    float bonusPerTenBefore = comp.missingHealthBonusPerTenPercent;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.missingHealthBonusPerTenPercent, ref bonusPerTen, 0.01f, 10f);
                    if (Math.Abs(comp.missingHealthBonusPerTenPercent - bonusPerTenBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_MissingHealthBonusMaxScale".Translate());
                    string maxScale = comp.missingHealthBonusMaxScale.ToString();
                    float maxScaleBefore = comp.missingHealthBonusMaxScale;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.missingHealthBonusMaxScale, ref maxScale, 0.01f, 10f);
                    if (Math.Abs(comp.missingHealthBonusMaxScale - maxScaleBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.FullHealthBonusDamage)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_FullHealthThresholdPercent".Translate());
                    string threshold = comp.fullHealthThresholdPercent.ToString();
                    float thresholdBefore = comp.fullHealthThresholdPercent;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.fullHealthThresholdPercent, ref threshold, 0.01f, 1f);
                    if (Math.Abs(comp.fullHealthThresholdPercent - thresholdBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_FullHealthBonusDamageScale".Translate());
                    string bonusScale = comp.fullHealthBonusDamageScale.ToString();
                    float bonusScaleBefore = comp.fullHealthBonusDamageScale;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.fullHealthBonusDamageScale, ref bonusScale, 0.01f, 10f);
                    if (Math.Abs(comp.fullHealthBonusDamageScale - bonusScaleBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.NearbyEnemyBonusDamage)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_NearbyEnemyBonusMaxTargets".Translate());
                    string nearbyCount = comp.nearbyEnemyBonusMaxTargets.ToString();
                    int nearbyCountBefore = comp.nearbyEnemyBonusMaxTargets;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.nearbyEnemyBonusMaxTargets, ref nearbyCount, 1, 99);
                    if (comp.nearbyEnemyBonusMaxTargets != nearbyCountBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_NearbyEnemyBonusPerTarget".Translate());
                    string nearbyBonus = comp.nearbyEnemyBonusPerTarget.ToString();
                    float nearbyBonusBefore = comp.nearbyEnemyBonusPerTarget;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.nearbyEnemyBonusPerTarget, ref nearbyBonus, 0.01f, 10f);
                    if (Math.Abs(comp.nearbyEnemyBonusPerTarget - nearbyBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_NearbyEnemyBonusRadius".Translate());
                    string nearbyRadius = comp.nearbyEnemyBonusRadius.ToString();
                    float nearbyRadiusBefore = comp.nearbyEnemyBonusRadius;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.nearbyEnemyBonusRadius, ref nearbyRadius, 0.1f, 99f);
                    if (Math.Abs(comp.nearbyEnemyBonusRadius - nearbyRadiusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.IsolatedTargetBonusDamage)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_IsolatedTargetRadius".Translate());
                    string isolatedRadius = comp.isolatedTargetRadius.ToString();
                    float isolatedRadiusBefore = comp.isolatedTargetRadius;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.isolatedTargetRadius, ref isolatedRadius, 0.1f, 99f);
                    if (Math.Abs(comp.isolatedTargetRadius - isolatedRadiusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_IsolatedTargetBonusDamageScale".Translate());
                    string isolatedBonus = comp.isolatedTargetBonusDamageScale.ToString();
                    float isolatedBonusBefore = comp.isolatedTargetBonusDamageScale;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.isolatedTargetBonusDamageScale, ref isolatedBonus, 0.01f, 10f);
                    if (Math.Abs(comp.isolatedTargetBonusDamageScale - isolatedBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.MarkDetonation)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_MarkDurationTicks".Translate());
                    string markDuration = comp.markDurationTicks.ToString();
                    int markDurationBefore = comp.markDurationTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.markDurationTicks, ref markDuration, 1, 99999);
                    if (comp.markDurationTicks != markDurationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_MarkMaxStacks".Translate());
                    string markStacks = comp.markMaxStacks.ToString();
                    int markStacksBefore = comp.markMaxStacks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.markMaxStacks, ref markStacks, 1, 99);
                    if (comp.markMaxStacks != markStacksBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_MarkDetonationDamage".Translate());
                    string detonationDamage = comp.markDetonationDamage.ToString();
                    float detonationDamageBefore = comp.markDetonationDamage;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.markDetonationDamage, ref detonationDamage, 0.01f, 99999f);
                    if (Math.Abs(comp.markDetonationDamage - detonationDamageBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f),
                        comp.markDamageDef?.label ?? "CS_Studio_None".Translate(), () => ShowDamageDefSelectorForRuntime(comp)))
                    {
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.ComboStacks)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ComboStackWindowTicks".Translate());
                    string comboWindow = comp.comboStackWindowTicks.ToString();
                    int comboWindowTicksBefore = comp.comboStackWindowTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.comboStackWindowTicks, ref comboWindow, 1, 99999);
                    if (comp.comboStackWindowTicks != comboWindowTicksBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ComboStackMax".Translate());
                    string comboMax = comp.comboStackMax.ToString();
                    int comboMaxBefore = comp.comboStackMax;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.comboStackMax, ref comboMax, 1, 99);
                    if (comp.comboStackMax != comboMaxBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_ComboStackBonusDamagePerStack".Translate());
                    string comboBonus = comp.comboStackBonusDamagePerStack.ToString();
                    float comboBonusBefore = comp.comboStackBonusDamagePerStack;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.comboStackBonusDamagePerStack, ref comboBonus, 0.01f, 10f);
                    if (Math.Abs(comp.comboStackBonusDamagePerStack - comboBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.HitSlowField)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SlowFieldDurationTicks".Translate());
                    string slowDuration = comp.slowFieldDurationTicks.ToString();
                    int slowDurationBefore = comp.slowFieldDurationTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.slowFieldDurationTicks, ref slowDuration, 1, 99999);
                    if (comp.slowFieldDurationTicks != slowDurationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SlowFieldRadius".Translate());
                    string slowRadius = comp.slowFieldRadius.ToString();
                    float slowRadiusBefore = comp.slowFieldRadius;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.slowFieldRadius, ref slowRadius, 0.1f, 99f);
                    if (Math.Abs(comp.slowFieldRadius - slowRadiusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SlowFieldHediff".Translate());
                    string slowHediffLabel = string.IsNullOrWhiteSpace(comp.slowFieldHediffDefName)
                        ? "CS_Studio_None".Translate()
                        : comp.slowFieldHediffDefName;
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f), slowHediffLabel, () => ShowHediffDefSelectorForRuntime(comp)))
                    {
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SlowFieldHediffDefName".Translate());
                    string slowHediffDefName = comp.slowFieldHediffDefName ?? string.Empty;
                    string slowHediffBefore = slowHediffDefName;
                    slowHediffDefName = Widgets.TextField(new Rect(inner.x + labelW, rowY, valueW, 24f), slowHediffDefName);
                    if (!string.Equals(slowHediffBefore, slowHediffDefName, StringComparison.Ordinal))
                    {
                        comp.slowFieldHediffDefName = slowHediffDefName;
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.PierceBonusDamage)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_PierceMaxTargets".Translate());
                    string pierceCount = comp.pierceMaxTargets.ToString();
                    int pierceCountBefore = comp.pierceMaxTargets;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pierceMaxTargets, ref pierceCount, 1, 99);
                    if (comp.pierceMaxTargets != pierceCountBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_PierceBonusDamagePerTarget".Translate());
                    string pierceBonus = comp.pierceBonusDamagePerTarget.ToString();
                    float pierceBonusBefore = comp.pierceBonusDamagePerTarget;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pierceBonusDamagePerTarget, ref pierceBonus, 0.01f, 10f);
                    if (Math.Abs(comp.pierceBonusDamagePerTarget - pierceBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_PierceSearchRange".Translate());
                    string pierceRange = comp.pierceSearchRange.ToString();
                    float pierceRangeBefore = comp.pierceSearchRange;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pierceSearchRange, ref pierceRange, 0.1f, 99f);
                    if (Math.Abs(comp.pierceSearchRange - pierceRangeBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.DashEmpoweredStrike)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_DashEmpowerDurationTicks".Translate());
                    string dashDuration = comp.dashEmpowerDurationTicks.ToString();
                    int dashDurationBefore = comp.dashEmpowerDurationTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.dashEmpowerDurationTicks, ref dashDuration, 1, 99999);
                    if (comp.dashEmpowerDurationTicks != dashDurationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_DashEmpowerBonusDamageScale".Translate());
                    string dashBonus = comp.dashEmpowerBonusDamageScale.ToString();
                    float dashBonusBefore = comp.dashEmpowerBonusDamageScale;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.dashEmpowerBonusDamageScale, ref dashBonus, 0.01f, 10f);
                    if (Math.Abs(comp.dashEmpowerBonusDamageScale - dashBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.HitHeal)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_HitHealAmount".Translate());
                    string healAmount = comp.hitHealAmount.ToString();
                    float healAmountBefore = comp.hitHealAmount;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.hitHealAmount, ref healAmount, 0f, 99999f);
                    if (Math.Abs(comp.hitHealAmount - healAmountBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_HitHealRatio".Translate());
                    string healRatio = comp.hitHealRatio.ToString();
                    float hitHealRatioBefore = comp.hitHealRatio;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.hitHealRatio, ref healRatio, 0f, 10f);
                    if (Math.Abs(comp.hitHealRatio - hitHealRatioBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.HitCooldownRefund)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_RefundHotkeySlot".Translate());
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f),
                        ($"CS_Studio_Ability_Hotkey_{comp.refundHotkeySlot}").Translate(), () =>
                        {
                            var options = new List<FloatMenuOption>();
                            foreach (AbilityRuntimeHotkeySlot slot in Enum.GetValues(typeof(AbilityRuntimeHotkeySlot)))
                            {
                                AbilityRuntimeHotkeySlot localSlot = slot;
                                options.Add(new FloatMenuOption(($"CS_Studio_Ability_Hotkey_{localSlot}").Translate(), () =>
                                {
                                    comp.refundHotkeySlot = localSlot;
                                    NotifyAbilityPreviewDirty(true);
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        }))
                    {
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_HitCooldownRefundPercent".Translate());
                    string refundPercent = comp.hitCooldownRefundPercent.ToString();
                    float refundPercentBefore = comp.hitCooldownRefundPercent;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.hitCooldownRefundPercent, ref refundPercent, 0.01f, 1f);
                    if (Math.Abs(comp.hitCooldownRefundPercent - refundPercentBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.ProjectileSplit)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SplitProjectileCount".Translate());
                    string splitCount = comp.splitProjectileCount.ToString();
                    int splitCountBefore = comp.splitProjectileCount;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.splitProjectileCount, ref splitCount, 1, 99);
                    if (comp.splitProjectileCount != splitCountBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SplitDamageScale".Translate());
                    string splitScale = comp.splitDamageScale.ToString();
                    float splitScaleBefore = comp.splitDamageScale;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.splitDamageScale, ref splitScale, 0.01f, 10f);
                    if (Math.Abs(comp.splitDamageScale - splitScaleBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_SplitSearchRange".Translate());
                    string splitRange = comp.splitSearchRange.ToString();
                    float splitRangeBefore = comp.splitSearchRange;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.splitSearchRange, ref splitRange, 0.1f, 99f);
                    if (Math.Abs(comp.splitSearchRange - splitRangeBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.FlightState)
                {
                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_FlightDurationTicks".Translate());
                    string flightDuration = comp.flightDurationTicks.ToString();
                    int flightDurationBefore = comp.flightDurationTicks;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.flightDurationTicks, ref flightDuration, 1, 99999);
                    if (comp.flightDurationTicks != flightDurationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    Widgets.Label(new Rect(inner.x, rowY, labelW, 24f), "CS_Studio_Runtime_FlightHeightFactor".Translate());
                    string flightHeight = comp.flightHeightFactor.ToString();
                    float flightHeightBefore = comp.flightHeightFactor;
                    Widgets.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.flightHeightFactor, ref flightHeight, 0f, 5f);
                    if (Math.Abs(comp.flightHeightFactor - flightHeightBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }

                y += blockHeight + 6f;
            }
        }
    }
}