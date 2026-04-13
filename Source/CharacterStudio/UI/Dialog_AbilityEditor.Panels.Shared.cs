using System;
using System.Collections.Generic;
using System.Linq;
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
            List<AbilityEditorExtensionPanel> extensionPanels = new List<AbilityEditorExtensionPanel>(AbilityEditorExtensionRegistry.Panels);

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

            int effectCount = selectedAbility?.effects?.Count ?? 0;
            int vfxCount = selectedAbility?.visualEffects?.Count ?? 0;
            int rcCount = selectedAbility?.runtimeComponents?.Count ?? 0;
            bool hasPreviewContent = effectCount > 0 || vfxCount > 0 || rcCount > 0;

            List<string> tabs = new List<string>
            {
                $"CS_Studio_Effect_Title".Translate() + (effectCount > 0 ? $" ({effectCount})" : ""),
                "CS_Studio_VFX_Title".Translate() + (vfxCount > 0 ? $" ({vfxCount})" : ""),
                "CS_Studio_Section_RuntimeComponents".Translate().RawText.Split('.')[0] + (rcCount > 0 ? $" ({rcCount})" : ""),
                "CS_Studio_Ability_PreviewTab".Translate() + (hasPreviewContent ? " •" : string.Empty)
            };

            foreach (AbilityEditorExtensionPanel extensionPanel in extensionPanels)
            {
                tabs.Add(extensionPanel.label);
            }

            float tabW = inner.width / Mathf.Max(1f, tabs.Count);

            for (int i = 0; i < tabs.Count; i++)
            {
                Rect tabRect = new Rect(inner.x + tabW * i, inner.y, tabW, 26f);
                bool active = rightPanelTab == i;
                if (DrawBarButton(tabRect, tabs[i], () =>
                {
                    rightPanelTab = i;
                    if (i >= 4)
                    {
                        int extensionIndex = i - 4;
                        selectedExtensionPanelId = extensionIndex >= 0 && extensionIndex < extensionPanels.Count
                            ? extensionPanels[extensionIndex].id
                            : string.Empty;
                    }
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
                case 3: DrawAbilityPreviewPanelBody(bodyRect); break;
                default:
                    DrawExtensionPanelBody(bodyRect, extensionPanels);
                    break;
            }
        }

        private void DrawExtensionPanelBody(Rect rect, List<AbilityEditorExtensionPanel> extensionPanels)
        {
            if (selectedAbility == null)
            {
                Widgets.DrawHighlight(rect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(rect, "CS_Studio_Ability_SelectHint".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            AbilityEditorExtensionPanel? panel = null;
            if (!string.IsNullOrWhiteSpace(selectedExtensionPanelId))
            {
                AbilityEditorExtensionRegistry.TryGetPanel(selectedExtensionPanelId, out panel);
            }

            if (panel == null)
            {
                int extensionIndex = rightPanelTab - 4;
                if (extensionIndex >= 0 && extensionIndex < extensionPanels.Count)
                {
                    panel = extensionPanels[extensionIndex];
                    selectedExtensionPanelId = panel.id;
                }
            }

            if (panel?.drawer == null)
            {
                Widgets.DrawHighlight(rect);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(rect, "No extension panel registered.");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            panel.drawer(rect, selectedAbility, resetPlayback => NotifyAbilityPreviewDirty(resetPlayback));
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

            float totalHeight = 0f;
            for (int i = 0; i < selectedAbility.effects.Count; i++)
            {
                totalHeight += GetEffectItemHeight(selectedAbility.effects[i]) + 6f;
            }

            Rect viewRect = new Rect(0, 0, listRect.width - 16f, Mathf.Max(totalHeight, listRect.height));
            Widgets.BeginScrollView(listRect, ref effectsScrollPos, viewRect);
            float cy = 0f;
            for (int i = 0; i < selectedAbility.effects.Count; i++)
            {
                float itemHeight = GetEffectItemHeight(selectedAbility.effects[i]);
                DrawEffectItem(new Rect(0f, cy, viewRect.width, itemHeight), selectedAbility.effects[i], i);
                cy += itemHeight + 6f;
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

            const float itemGap = 6f;
            float totalHeight = 6f;
            for (int i = 0; i < selectedAbility.visualEffects.Count; i++)
            {
                totalHeight += GetVfxItemHeight(selectedAbility.visualEffects[i]) + itemGap;
            }

            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, totalHeight));
            Widgets.BeginScrollView(listRect, ref vfxScrollPos, viewRect);

            float cy = 0f;
            for (int i = 0; i < selectedAbility.visualEffects.Count; i++)
            {
                var vfx = selectedAbility.visualEffects[i];
                float itemHeight = GetVfxItemHeight(vfx);
                DrawVfxItem(new Rect(0f, cy, viewRect.width, itemHeight), vfx, i);
                cy += itemHeight + itemGap;
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
            string truncated = GenText.Truncate(label, buttonRect.width - 6f);
            Widgets.Label(buttonRect.ContractedBy(3f, 0f), truncated);
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
                AbilityRuntimeComponentType.SlotOverrideWindow => 138f,
                AbilityRuntimeComponentType.HotkeyOverride => 164f,
                AbilityRuntimeComponentType.FollowupCooldownGate => 112f,
                AbilityRuntimeComponentType.SmartJump => 244f,
                AbilityRuntimeComponentType.EShortJump => 244f,
                AbilityRuntimeComponentType.RStackDetonation => 250f,
                AbilityRuntimeComponentType.PeriodicPulse => 112f,
                AbilityRuntimeComponentType.KillRefresh => 112f,
                AbilityRuntimeComponentType.ShieldAbsorb => 164f,
                AbilityRuntimeComponentType.AttachedShieldVisual => 112f,
                AbilityRuntimeComponentType.ProjectileInterceptorShield => 112f,
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
                AbilityRuntimeComponentType.FlightOnlyFollowup => 216f,
                AbilityRuntimeComponentType.FlightLandingBurst => 242f,
                AbilityRuntimeComponentType.TimeStop => 86f,
                AbilityRuntimeComponentType.WeatherChange => 112f,
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

                GameFont prevRCFont = Text.Font;
                Text.Font = GameFont.Tiny;
                float titleW = inner.width - 100f;
                Widgets.Label(new Rect(inner.x, inner.y, titleW, 24f),
                    GenText.Truncate($"#{i + 1} {GetRuntimeComponentTypeLabel(comp.type)}", titleW));
                Text.Font = prevRCFont;
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
                float labelW = Mathf.Min(160f, inner.width * 0.48f);
                float valueW = inner.width - labelW - 6f;

                void DrawRCRowLabel(float rx, float ry, string text, float rw)
                {
                    GameFont rlf = Text.Font;
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(rx, ry + 2f, rw, 20f), GenText.Truncate(text, rw));
                    Text.Font = rlf;
                }

                if (comp.type == AbilityRuntimeComponentType.SlotOverrideWindow)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ComboTargetSlot".Translate(), labelW);
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f),
                        ($"CS_Studio_Ability_Hotkey_{comp.comboTargetHotkeySlot}").Translate(), () =>
                        {
                            var options = new List<FloatMenuOption>();
                            foreach (AbilityRuntimeHotkeySlot slot in Enum.GetValues(typeof(AbilityRuntimeHotkeySlot)))
                            {
                                AbilityRuntimeHotkeySlot localSlot = slot;
                                options.Add(new FloatMenuOption(($"CS_Studio_Ability_Hotkey_{localSlot}").Translate(), () =>
                                {
                                    comp.comboTargetHotkeySlot = localSlot;
                                    NotifyAbilityPreviewDirty(true);
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        }))
                    {
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ComboTargetAbility".Translate(), labelW);
                    string comboAbilityLabel = string.IsNullOrWhiteSpace(comp.comboTargetAbilityDefName)
                        ? "CS_Studio_None".Translate()
                        : comp.comboTargetAbilityDefName;
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f), comboAbilityLabel, () => ShowAbilityDefSelectorForRuntime(comp)))
                    {
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_QWindowTicks".Translate(), labelW);
                    string s = comp.comboWindowTicks.ToString();
                    int comboWindowBefore = comp.comboWindowTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.comboWindowTicks, ref s, 1, 9999);
                    if (comp.comboWindowTicks != comboWindowBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.HotkeyOverride)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_HotkeyOverrideSlot".Translate(), labelW);
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

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_HotkeyOverrideAbility".Translate(), labelW);
                    string overrideAbilityLabel = string.IsNullOrWhiteSpace(comp.overrideAbilityDefName)
                        ? "CS_Studio_None".Translate()
                        : comp.overrideAbilityDefName;
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f), overrideAbilityLabel, () => ShowAbilityDefSelectorForRuntime(comp)))
                    {
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_HotkeyOverrideAbilityDefName".Translate(), labelW);
                    string overrideAbilityDefName = comp.overrideAbilityDefName ?? string.Empty;
                    string overrideAbilityBefore = overrideAbilityDefName;
                    overrideAbilityDefName = Widgets.TextField(new Rect(inner.x + labelW, rowY, valueW, 24f), overrideAbilityDefName);
                    if (!string.Equals(overrideAbilityBefore, overrideAbilityDefName, StringComparison.Ordinal))
                    {
                        comp.overrideAbilityDefName = overrideAbilityDefName;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_HotkeyOverrideDuration".Translate(), labelW);
                    string duration = comp.overrideDurationTicks.ToString();
                    int durationBefore = comp.overrideDurationTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.overrideDurationTicks, ref duration, 1, 99999);
                    if (comp.overrideDurationTicks != durationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.FollowupCooldownGate)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_FollowupCooldownSlot".Translate(), labelW);
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

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_FollowupCooldownTicks".Translate(), labelW);
                    string cooldownGate = comp.followupCooldownTicks.ToString();
                    int cooldownGateBefore = comp.followupCooldownTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.followupCooldownTicks, ref cooldownGate, 1, 99999);
                    if (comp.followupCooldownTicks != cooldownGateBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.SmartJump || comp.type == AbilityRuntimeComponentType.EShortJump)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ECooldownTicks".Translate(), labelW);
                    string cd = comp.cooldownTicks.ToString();
                    int cooldownBefore = comp.cooldownTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.cooldownTicks, ref cd, 0, 99999);
                    if (comp.cooldownTicks != cooldownBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_EJumpDistance".Translate(), labelW);
                    string dist = comp.jumpDistance.ToString();
                    int jumpDistanceBefore = comp.jumpDistance;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.jumpDistance, ref dist, 1, 100);
                    if (comp.jumpDistance != jumpDistanceBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_EFindRadius".Translate(), labelW);
                    string find = comp.findCellRadius.ToString();
                    int findRadiusBefore = comp.findCellRadius;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.findCellRadius, ref find, 0, 30);
                    if (comp.findCellRadius != findRadiusBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ETriggerEffects".Translate(), labelW);
                    bool trigger = comp.triggerAbilityEffectsAfterJump;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref trigger, 24f, false);
                    if (comp.triggerAbilityEffectsAfterJump != trigger)
                    {
                        comp.triggerAbilityEffectsAfterJump = trigger;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    if (comp.type == AbilityRuntimeComponentType.SmartJump || comp.type == AbilityRuntimeComponentType.EShortJump)
                    {
                        DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SmartJumpUseMouse".Translate(), labelW);
                        bool useMouse = comp.useMouseTargetCell;
                        Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref useMouse, 24f, false);
                        if (comp.useMouseTargetCell != useMouse)
                        {
                            comp.useMouseTargetCell = useMouse;
                            NotifyAbilityPreviewDirty(true);
                        }
                        rowY += 26f;

                        DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SmartJumpOffset".Translate(), labelW);
                        string offset = comp.smartCastOffsetCells.ToString();
                        int offsetBefore = comp.smartCastOffsetCells;
                        UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.smartCastOffsetCells, ref offset, 1, 100);
                        if (comp.smartCastOffsetCells != offsetBefore)
                        {
                            NotifyAbilityPreviewDirty(true);
                        }
                        rowY += 26f;

                        DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SmartJumpClamp".Translate(), labelW);
                        bool clamp = comp.smartCastClampToMaxDistance;
                        Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref clamp, 24f, false);
                        if (comp.smartCastClampToMaxDistance != clamp)
                        {
                            comp.smartCastClampToMaxDistance = clamp;
                            NotifyAbilityPreviewDirty(true);
                        }
                        rowY += 26f;

                        DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SmartJumpFallback".Translate(), labelW);
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
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_RRequiredStacks".Translate(), labelW);
                    string stacks = comp.requiredStacks.ToString();
                    int requiredStacksBefore = comp.requiredStacks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.requiredStacks, ref stacks, 1, 999);
                    if (comp.requiredStacks != requiredStacksBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_RDelayTicks".Translate(), labelW);
                    string delay = comp.delayTicks.ToString();
                    int delayBefore = comp.delayTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.delayTicks, ref delay, 0, 99999);
                    if (comp.delayTicks != delayBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_RWave1".Translate(), labelW);
                    string w1r = comp.wave1Radius.ToString();
                    string w1d = comp.wave1Damage.ToString();
                    float wave1RadiusBefore = comp.wave1Radius;
                    float wave1DamageBefore = comp.wave1Damage;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave1Radius, ref w1r, 0.1f, 99f);
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave1Damage, ref w1d, 1f, 99999f);
                    if (Math.Abs(comp.wave1Radius - wave1RadiusBefore) > 0.001f || Math.Abs(comp.wave1Damage - wave1DamageBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_RWave2".Translate(), labelW);
                    string w2r = comp.wave2Radius.ToString();
                    string w2d = comp.wave2Damage.ToString();
                    float wave2RadiusBefore = comp.wave2Radius;
                    float wave2DamageBefore = comp.wave2Damage;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave2Radius, ref w2r, 0.1f, 99f);
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave2Damage, ref w2d, 1f, 99999f);
                    if (Math.Abs(comp.wave2Radius - wave2RadiusBefore) > 0.001f || Math.Abs(comp.wave2Damage - wave2DamageBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_RWave3".Translate(), labelW);
                    string w3r = comp.wave3Radius.ToString();
                    string w3d = comp.wave3Damage.ToString();
                    float wave3RadiusBefore = comp.wave3Radius;
                    float wave3DamageBefore = comp.wave3Damage;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave3Radius, ref w3r, 0.1f, 99f);
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f), ref comp.wave3Damage, ref w3d, 1f, 99999f);
                    if (Math.Abs(comp.wave3Radius - wave3RadiusBefore) > 0.001f || Math.Abs(comp.wave3Damage - wave3DamageBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_RDamageDef".Translate(), labelW);
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f),
                        comp.waveDamageDef?.label ?? "CS_Studio_None".Translate(), () => ShowDamageDefSelectorForRuntime(comp)))
                    {
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.PeriodicPulse)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_PulseInterval".Translate(), labelW);
                    string interval = comp.pulseIntervalTicks.ToString();
                    int intervalBefore = comp.pulseIntervalTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pulseIntervalTicks, ref interval, 1, 99999);
                    if (comp.pulseIntervalTicks != intervalBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_PulseDuration".Translate(), labelW);
                    string duration = comp.pulseTotalTicks.ToString();
                    int durationBefore = comp.pulseTotalTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pulseTotalTicks, ref duration, 1, 99999);
                    if (comp.pulseTotalTicks != durationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_PulseImmediate".Translate(), labelW);
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
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_KillRefreshHotkeySlot".Translate(), labelW);
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

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_KillRefreshPercent".Translate(), labelW);
                    string refresh = comp.killRefreshCooldownPercent.ToString();
                    float refreshBefore = comp.killRefreshCooldownPercent;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.killRefreshCooldownPercent, ref refresh, 0.01f, 1f);
                    if (Math.Abs(comp.killRefreshCooldownPercent - refreshBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.ShieldAbsorb ||
                         comp.type == AbilityRuntimeComponentType.AttachedShieldVisual ||
                         comp.type == AbilityRuntimeComponentType.ProjectileInterceptorShield)
                {
                    DrawReflectionFields(inner, ref rowY, labelW, valueW, comp);
                }
                else if (comp.type == AbilityRuntimeComponentType.ChainBounce)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ChainBounceCount".Translate(), labelW);
                    string bounceCount = comp.maxBounceCount.ToString();
                    int bounceCountBefore = comp.maxBounceCount;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.maxBounceCount, ref bounceCount, 1, 99);
                    if (comp.maxBounceCount != bounceCountBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ChainBounceRange".Translate(), labelW);
                    string bounceRange = comp.bounceRange.ToString();
                    float bounceRangeBefore = comp.bounceRange;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.bounceRange, ref bounceRange, 0.1f, 99f);
                    if (Math.Abs(comp.bounceRange - bounceRangeBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ChainBounceFalloff".Translate(), labelW);
                    string falloff = comp.bounceDamageFalloff.ToString();
                    float falloffBefore = comp.bounceDamageFalloff;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.bounceDamageFalloff, ref falloff, -0.95f, 0.95f);
                    if (Math.Abs(comp.bounceDamageFalloff - falloffBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.ExecuteBonusDamage)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ExecuteThresholdPercent".Translate(), labelW);
                    string threshold = comp.executeThresholdPercent.ToString();
                    float thresholdBefore = comp.executeThresholdPercent;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.executeThresholdPercent, ref threshold, 0.01f, 0.99f);
                    if (Math.Abs(comp.executeThresholdPercent - thresholdBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ExecuteBonusDamageScale".Translate(), labelW);
                    string bonusScale = comp.executeBonusDamageScale.ToString();
                    float bonusScaleBefore = comp.executeBonusDamageScale;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.executeBonusDamageScale, ref bonusScale, 0.01f, 10f);
                    if (Math.Abs(comp.executeBonusDamageScale - bonusScaleBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.MissingHealthBonusDamage)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_MissingHealthBonusPerTenPercent".Translate(), labelW);
                    string bonusPerTen = comp.missingHealthBonusPerTenPercent.ToString();
                    float bonusPerTenBefore = comp.missingHealthBonusPerTenPercent;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.missingHealthBonusPerTenPercent, ref bonusPerTen, 0.01f, 10f);
                    if (Math.Abs(comp.missingHealthBonusPerTenPercent - bonusPerTenBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_MissingHealthBonusMaxScale".Translate(), labelW);
                    string maxScale = comp.missingHealthBonusMaxScale.ToString();
                    float maxScaleBefore = comp.missingHealthBonusMaxScale;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.missingHealthBonusMaxScale, ref maxScale, 0.01f, 10f);
                    if (Math.Abs(comp.missingHealthBonusMaxScale - maxScaleBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.FullHealthBonusDamage)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_FullHealthThresholdPercent".Translate(), labelW);
                    string threshold = comp.fullHealthThresholdPercent.ToString();
                    float thresholdBefore = comp.fullHealthThresholdPercent;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.fullHealthThresholdPercent, ref threshold, 0.01f, 1f);
                    if (Math.Abs(comp.fullHealthThresholdPercent - thresholdBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_FullHealthBonusDamageScale".Translate(), labelW);
                    string bonusScale = comp.fullHealthBonusDamageScale.ToString();
                    float bonusScaleBefore = comp.fullHealthBonusDamageScale;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.fullHealthBonusDamageScale, ref bonusScale, 0.01f, 10f);
                    if (Math.Abs(comp.fullHealthBonusDamageScale - bonusScaleBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.NearbyEnemyBonusDamage)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_NearbyEnemyBonusMaxTargets".Translate(), labelW);
                    string nearbyCount = comp.nearbyEnemyBonusMaxTargets.ToString();
                    int nearbyCountBefore = comp.nearbyEnemyBonusMaxTargets;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.nearbyEnemyBonusMaxTargets, ref nearbyCount, 1, 99);
                    if (comp.nearbyEnemyBonusMaxTargets != nearbyCountBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_NearbyEnemyBonusPerTarget".Translate(), labelW);
                    string nearbyBonus = comp.nearbyEnemyBonusPerTarget.ToString();
                    float nearbyBonusBefore = comp.nearbyEnemyBonusPerTarget;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.nearbyEnemyBonusPerTarget, ref nearbyBonus, 0.01f, 10f);
                    if (Math.Abs(comp.nearbyEnemyBonusPerTarget - nearbyBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_NearbyEnemyBonusRadius".Translate(), labelW);
                    string nearbyRadius = comp.nearbyEnemyBonusRadius.ToString();
                    float nearbyRadiusBefore = comp.nearbyEnemyBonusRadius;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.nearbyEnemyBonusRadius, ref nearbyRadius, 0.1f, 99f);
                    if (Math.Abs(comp.nearbyEnemyBonusRadius - nearbyRadiusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.IsolatedTargetBonusDamage)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_IsolatedTargetRadius".Translate(), labelW);
                    string isolatedRadius = comp.isolatedTargetRadius.ToString();
                    float isolatedRadiusBefore = comp.isolatedTargetRadius;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.isolatedTargetRadius, ref isolatedRadius, 0.1f, 99f);
                    if (Math.Abs(comp.isolatedTargetRadius - isolatedRadiusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_IsolatedTargetBonusDamageScale".Translate(), labelW);
                    string isolatedBonus = comp.isolatedTargetBonusDamageScale.ToString();
                    float isolatedBonusBefore = comp.isolatedTargetBonusDamageScale;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.isolatedTargetBonusDamageScale, ref isolatedBonus, 0.01f, 10f);
                    if (Math.Abs(comp.isolatedTargetBonusDamageScale - isolatedBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.MarkDetonation)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_MarkDurationTicks".Translate(), labelW);
                    string markDuration = comp.markDurationTicks.ToString();
                    int markDurationBefore = comp.markDurationTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.markDurationTicks, ref markDuration, 1, 99999);
                    if (comp.markDurationTicks != markDurationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_MarkMaxStacks".Translate(), labelW);
                    string markStacks = comp.markMaxStacks.ToString();
                    int markStacksBefore = comp.markMaxStacks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.markMaxStacks, ref markStacks, 1, 99);
                    if (comp.markMaxStacks != markStacksBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_MarkDetonationDamage".Translate(), labelW);
                    string detonationDamage = comp.markDetonationDamage.ToString();
                    float detonationDamageBefore = comp.markDetonationDamage;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.markDetonationDamage, ref detonationDamage, 0.01f, 99999f);
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
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ComboStackWindowTicks".Translate(), labelW);
                    string comboWindow = comp.comboStackWindowTicks.ToString();
                    int comboWindowTicksBefore = comp.comboStackWindowTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.comboStackWindowTicks, ref comboWindow, 1, 99999);
                    if (comp.comboStackWindowTicks != comboWindowTicksBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ComboStackMax".Translate(), labelW);
                    string comboMax = comp.comboStackMax.ToString();
                    int comboMaxBefore = comp.comboStackMax;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.comboStackMax, ref comboMax, 1, 99);
                    if (comp.comboStackMax != comboMaxBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ComboStackBonusDamagePerStack".Translate(), labelW);
                    string comboBonus = comp.comboStackBonusDamagePerStack.ToString();
                    float comboBonusBefore = comp.comboStackBonusDamagePerStack;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.comboStackBonusDamagePerStack, ref comboBonus, 0.01f, 10f);
                    if (Math.Abs(comp.comboStackBonusDamagePerStack - comboBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.HitSlowField)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SlowFieldDurationTicks".Translate(), labelW);
                    string slowDuration = comp.slowFieldDurationTicks.ToString();
                    int slowDurationBefore = comp.slowFieldDurationTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.slowFieldDurationTicks, ref slowDuration, 1, 99999);
                    if (comp.slowFieldDurationTicks != slowDurationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SlowFieldRadius".Translate(), labelW);
                    string slowRadius = comp.slowFieldRadius.ToString();
                    float slowRadiusBefore = comp.slowFieldRadius;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.slowFieldRadius, ref slowRadius, 0.1f, 99f);
                    if (Math.Abs(comp.slowFieldRadius - slowRadiusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SlowFieldHediff".Translate(), labelW);
                    string slowHediffLabel = string.IsNullOrWhiteSpace(comp.slowFieldHediffDefName)
                        ? "CS_Studio_None".Translate()
                        : comp.slowFieldHediffDefName;
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f), slowHediffLabel, () => ShowHediffDefSelectorForRuntime(comp)))
                    {
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SlowFieldHediffDefName".Translate(), labelW);
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
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_PierceMaxTargets".Translate(), labelW);
                    string pierceCount = comp.pierceMaxTargets.ToString();
                    int pierceCountBefore = comp.pierceMaxTargets;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pierceMaxTargets, ref pierceCount, 1, 99);
                    if (comp.pierceMaxTargets != pierceCountBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_PierceBonusDamagePerTarget".Translate(), labelW);
                    string pierceBonus = comp.pierceBonusDamagePerTarget.ToString();
                    float pierceBonusBefore = comp.pierceBonusDamagePerTarget;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pierceBonusDamagePerTarget, ref pierceBonus, 0.01f, 10f);
                    if (Math.Abs(comp.pierceBonusDamagePerTarget - pierceBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_PierceSearchRange".Translate(), labelW);
                    string pierceRange = comp.pierceSearchRange.ToString();
                    float pierceRangeBefore = comp.pierceSearchRange;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.pierceSearchRange, ref pierceRange, 0.1f, 99f);
                    if (Math.Abs(comp.pierceSearchRange - pierceRangeBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.DashEmpoweredStrike)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_DashEmpowerDurationTicks".Translate(), labelW);
                    string dashDuration = comp.dashEmpowerDurationTicks.ToString();
                    int dashDurationBefore = comp.dashEmpowerDurationTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.dashEmpowerDurationTicks, ref dashDuration, 1, 99999);
                    if (comp.dashEmpowerDurationTicks != dashDurationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_DashEmpowerBonusDamageScale".Translate(), labelW);
                    string dashBonus = comp.dashEmpowerBonusDamageScale.ToString();
                    float dashBonusBefore = comp.dashEmpowerBonusDamageScale;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.dashEmpowerBonusDamageScale, ref dashBonus, 0.01f, 10f);
                    if (Math.Abs(comp.dashEmpowerBonusDamageScale - dashBonusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.HitHeal)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_HitHealAmount".Translate(), labelW);
                    string healAmount = comp.hitHealAmount.ToString();
                    float healAmountBefore = comp.hitHealAmount;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.hitHealAmount, ref healAmount, 0f, 99999f);
                    if (Math.Abs(comp.hitHealAmount - healAmountBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_HitHealRatio".Translate(), labelW);
                    string healRatio = comp.hitHealRatio.ToString();
                    float hitHealRatioBefore = comp.hitHealRatio;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.hitHealRatio, ref healRatio, 0f, 10f);
                    if (Math.Abs(comp.hitHealRatio - hitHealRatioBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.HitCooldownRefund)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_RefundHotkeySlot".Translate(), labelW);
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

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_HitCooldownRefundPercent".Translate(), labelW);
                    string refundPercent = comp.hitCooldownRefundPercent.ToString();
                    float refundPercentBefore = comp.hitCooldownRefundPercent;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.hitCooldownRefundPercent, ref refundPercent, 0.01f, 1f);
                    if (Math.Abs(comp.hitCooldownRefundPercent - refundPercentBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.ProjectileSplit)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SplitProjectileCount".Translate(), labelW);
                    string splitCount = comp.splitProjectileCount.ToString();
                    int splitCountBefore = comp.splitProjectileCount;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.splitProjectileCount, ref splitCount, 1, 99);
                    if (comp.splitProjectileCount != splitCountBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SplitDamageScale".Translate(), labelW);
                    string splitScale = comp.splitDamageScale.ToString();
                    float splitScaleBefore = comp.splitDamageScale;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.splitDamageScale, ref splitScale, 0.01f, 10f);
                    if (Math.Abs(comp.splitDamageScale - splitScaleBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_SplitSearchRange".Translate(), labelW);
                    string splitRange = comp.splitSearchRange.ToString();
                    float splitRangeBefore = comp.splitSearchRange;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.splitSearchRange, ref splitRange, 0.1f, 99f);
                    if (Math.Abs(comp.splitSearchRange - splitRangeBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.FlightState)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_FlightDurationTicks".Translate(), labelW);
                    string flightDuration = comp.flightDurationTicks.ToString();
                    int flightDurationBefore = comp.flightDurationTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.flightDurationTicks, ref flightDuration, 1, 99999);
                    if (comp.flightDurationTicks != flightDurationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_FlightHeightFactor".Translate(), labelW);
                    string flightHeight = comp.flightHeightFactor.ToString();
                    float flightHeightBefore = comp.flightHeightFactor;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.flightHeightFactor, ref flightHeight, 0f, 5f);
                    if (Math.Abs(comp.flightHeightFactor - flightHeightBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_FlightSuppressCombat".Translate(), labelW);
                    bool suppressCombat = comp.suppressCombatActionsDuringFlightState;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref suppressCombat, 24f, false);
                    if (comp.suppressCombatActionsDuringFlightState != suppressCombat)
                    {
                        comp.suppressCombatActionsDuringFlightState = suppressCombat;
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.FlightOnlyFollowup)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_RequiredFlightSourceAbility".Translate(), labelW);
                    string requiredSourceAbilityLabel = string.IsNullOrWhiteSpace(comp.requiredFlightSourceAbilityDefName)
                        ? "CS_Studio_None".Translate()
                        : comp.requiredFlightSourceAbilityDefName;
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f), requiredSourceAbilityLabel, () => ShowAbilityDefSelectorForRuntime(comp)))
                    {
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_RequireReservedTargetCell".Translate(), labelW);
                    bool requireReservedTargetCell = comp.requireReservedTargetCell;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref requireReservedTargetCell, 24f, false);
                    if (comp.requireReservedTargetCell != requireReservedTargetCell)
                    {
                        comp.requireReservedTargetCell = requireReservedTargetCell;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_ConsumeFlightStateOnCast".Translate(), labelW);
                    bool consumeFlightStateOnCast = comp.consumeFlightStateOnCast;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref consumeFlightStateOnCast, 24f, false);
                    if (comp.consumeFlightStateOnCast != consumeFlightStateOnCast)
                    {
                        comp.consumeFlightStateOnCast = consumeFlightStateOnCast;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_OnlyUseDuringFlightWindow".Translate(), labelW);
                    bool onlyUseDuringFlightWindow = comp.onlyUseDuringFlightWindow;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref onlyUseDuringFlightWindow, 24f, false);
                    if (comp.onlyUseDuringFlightWindow != onlyUseDuringFlightWindow)
                    {
                        comp.onlyUseDuringFlightWindow = onlyUseDuringFlightWindow;
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.FlightLandingBurst)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_LandingBurstRadius".Translate(), labelW);
                    string landingBurstRadius = comp.landingBurstRadius.ToString();
                    float landingBurstRadiusBefore = comp.landingBurstRadius;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.landingBurstRadius, ref landingBurstRadius, 0.1f, 99f);
                    if (Math.Abs(comp.landingBurstRadius - landingBurstRadiusBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_LandingBurstDamage".Translate(), labelW);
                    string landingBurstDamage = comp.landingBurstDamage.ToString();
                    float landingBurstDamageBefore = comp.landingBurstDamage;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, (valueW - 6f) / 2f, 24f), ref comp.landingBurstDamage, ref landingBurstDamage, 0.01f, 99999f);
                    if (Math.Abs(comp.landingBurstDamage - landingBurstDamageBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW + (valueW - 6f) / 2f + 6f, rowY, (valueW - 6f) / 2f, 24f),
                        comp.landingBurstDamageDef?.label ?? "CS_Studio_None".Translate(), () => ShowDamageDefSelectorForRuntime(comp)))
                    {
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_LandingEffecterDefName".Translate(), labelW);
                    string landingEffecterDefName = comp.landingEffecterDefName ?? string.Empty;
                    string landingEffecterDefNameBefore = landingEffecterDefName;
                    landingEffecterDefName = Widgets.TextField(new Rect(inner.x + labelW, rowY, valueW, 24f), landingEffecterDefName);
                    if (!string.Equals(landingEffecterDefNameBefore, landingEffecterDefName, StringComparison.Ordinal))
                    {
                        comp.landingEffecterDefName = landingEffecterDefName;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_LandingSoundDefName".Translate(), labelW);
                    string landingSoundDefName = comp.landingSoundDefName ?? string.Empty;
                    string landingSoundDefNameBefore = landingSoundDefName;
                    landingSoundDefName = Widgets.TextField(new Rect(inner.x + labelW, rowY, valueW, 24f), landingSoundDefName);
                    if (!string.Equals(landingSoundDefNameBefore, landingSoundDefName, StringComparison.Ordinal))
                    {
                        comp.landingSoundDefName = landingSoundDefName;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_LandingAffectBuildings".Translate(), labelW);
                    bool affectBuildings = comp.affectBuildings;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref affectBuildings, 24f, false);
                    if (comp.affectBuildings != affectBuildings)
                    {
                        comp.affectBuildings = affectBuildings;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_LandingAffectCells".Translate(), labelW);
                    bool affectCells = comp.affectCells;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref affectCells, 24f, false);
                    if (comp.affectCells != affectCells)
                    {
                        comp.affectCells = affectCells;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_LandingKnockbackTargets".Translate(), labelW);
                    bool knockbackTargets = comp.knockbackTargets;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref knockbackTargets, 24f, false);
                    if (comp.knockbackTargets != knockbackTargets)
                    {
                        comp.knockbackTargets = knockbackTargets;
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_LandingKnockbackDistance".Translate(), labelW);
                    string landingKnockbackDistance = comp.knockbackDistance.ToString();
                    float landingKnockbackDistanceBefore = comp.knockbackDistance;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.knockbackDistance, ref landingKnockbackDistance, 0f, 99f);
                    if (Math.Abs(comp.knockbackDistance - landingKnockbackDistanceBefore) > 0.001f)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.TimeStop)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_TimeStopDurationTicks".Translate(), labelW);
                    string stopDuration = comp.timeStopDurationTicks.ToString();
                    int timeStopDurationBefore = comp.timeStopDurationTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.timeStopDurationTicks, ref stopDuration, 1, 99999);
                    if (comp.timeStopDurationTicks != timeStopDurationBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Runtime_TimeStopFreezeVisuals".Translate(), labelW);
                    bool freezeVisuals = comp.freezeVisualsDuringTimeStop;
                    Widgets.Checkbox(new Vector2(inner.x + labelW, rowY + 2f), ref freezeVisuals, 24f, false);
                    if (comp.freezeVisualsDuringTimeStop != freezeVisuals)
                    {
                        comp.freezeVisualsDuringTimeStop = freezeVisuals;
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (comp.type == AbilityRuntimeComponentType.WeatherChange)
                {
                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Effect_WeatherDef".Translate(), labelW);
                    string weatherLabel = string.IsNullOrWhiteSpace(comp.weatherDefName)
                        ? "CS_Studio_None".Translate()
                        : comp.weatherDefName;
                    if (DrawSelectionFieldButton(new Rect(inner.x + labelW, rowY, valueW, 24f), weatherLabel, () => ShowWeatherDefSelectorForRuntime(comp)))
                    {
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Effect_WeatherDuration".Translate(), labelW);
                    string durStr = comp.weatherDurationTicks.ToString();
                    int durBefore = comp.weatherDurationTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.weatherDurationTicks, ref durStr, 1, 9999999);
                    if (comp.weatherDurationTicks != durBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                    rowY += 26f;

                    DrawRCRowLabel(inner.x, rowY, "CS_Studio_Effect_WeatherTransition".Translate(), labelW);
                    string transStr = comp.weatherTransitionTicks.ToString();
                    int transBefore = comp.weatherTransitionTicks;
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref comp.weatherTransitionTicks, ref transStr, 0, 99999);
                    if (comp.weatherTransitionTicks != transBefore)
                    {
                        NotifyAbilityPreviewDirty(true);
                    }
                }

                y += blockHeight + 6f;
            }
        }

        private void DrawReflectionFields(Rect inner, ref float rowY, float labelW, float valueW, AbilityRuntimeComponentConfig comp)
        {
            var fields = comp.GetType().GetFields()
                .Where(f => f.GetCustomAttributes(typeof(CharacterStudio.Core.EditorFieldAttribute), false).Length > 0)
                .ToArray();

            foreach (var field in fields)
            {
                var attr = (CharacterStudio.Core.EditorFieldAttribute)field.GetCustomAttributes(typeof(CharacterStudio.Core.EditorFieldAttribute), false)[0];
                if (attr.ValidTypes != null && attr.ValidTypes.Length > 0 && !attr.ValidTypes.Contains(comp.type)) continue;

                GameFont prevRefFont = Text.Font;
                Text.Font = GameFont.Tiny;
                string labelText = GenText.Truncate(attr.LabelKey.Translate(), labelW);
                Widgets.Label(new Rect(inner.x, rowY + 2f, labelW, 20f), labelText);
                Text.Font = prevRefFont;
                if (field.FieldType == typeof(float))
                {
                    float val = (float)field.GetValue(comp);
                    float oldVal = val;
                    string strVal = val.ToString();
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref val, ref strVal, attr.Min, attr.Max);
                    if (Math.Abs(val - oldVal) > 0.001f)
                    {
                        field.SetValue(comp, val);
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (field.FieldType == typeof(int))
                {
                    int val = (int)field.GetValue(comp);
                    int oldVal = val;
                    string strVal = val.ToString();
                    UIHelper.TextFieldNumeric(new Rect(inner.x + labelW, rowY, valueW, 24f), ref val, ref strVal, (int)attr.Min, (int)attr.Max);
                    if (val != oldVal)
                    {
                        field.SetValue(comp, val);
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                else if (field.FieldType == typeof(string))
                {
                    string val = (string)field.GetValue(comp) ?? string.Empty;
                    string oldVal = val;
                    val = Widgets.TextField(new Rect(inner.x + labelW, rowY, valueW, 24f), val);
                    if (!string.Equals(val, oldVal, StringComparison.Ordinal))
                    {
                        field.SetValue(comp, val);
                        NotifyAbilityPreviewDirty(true);
                    }
                }
                rowY += 26f;
            }
        }
    }
}