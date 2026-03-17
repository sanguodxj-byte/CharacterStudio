using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.AI;
using CharacterStudio.Core;
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
        private const float LeftPanelWidth = 260f;
        private const float RightPanelWidth = 360f;
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
        private Vector2 vfxScrollPos;
        private Vector2 rcScrollPos;
        /// <summary>右侧面板标签：0=效果 1=视觉特效 2=运行时组件</summary>
        private int rightPanelTab = 0;
        private string validationSummary = string.Empty;
        private string abilitySearchText = string.Empty;
        private string llmAbilityPrompt = string.Empty;
        private readonly SkinAbilityHotkeyConfig? boundHotkeys;
        // LLM 生成状态（异步）— 结果字段为 TODO：异步回调完成后读取
#pragma warning disable CS0414
        private bool llmAbilitiesGenerating = false;
        private (bool replaceExisting, List<ModularAbilityDef> result)? llmAbilitiesPendingResult = null;
        private string? llmAbilitiesPendingError = null;
#pragma warning restore CS0414

        private readonly PawnSkinDef? boundSkin;
        // 绑定目标 Pawn（可从编辑器直接授予/撤销技能）
        private Pawn? boundPawn;

        public override Vector2 InitialSize => new Vector2(1180f, 760f);

        public Dialog_AbilityEditor(List<ModularAbilityDef> abilityList, SkinAbilityHotkeyConfig? hotkeyConfig = null, PawnSkinDef? skin = null)
        {
            this.abilities = abilityList;
            this.boundHotkeys = hotkeyConfig;
            this.boundSkin = skin;
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

        public override void PreClose()
        {
            base.PreClose();
            // 当能力编辑器关闭时，自动将当前修改同步回皮肤对象
            if (boundSkin != null)
            {
                boundSkin.abilities.Clear();
                if (abilities != null)
                {
                    foreach (var ability in abilities)
                    {
                        if (ability != null)
                        {
                            boundSkin.abilities.Add(ability.Clone());
                        }
                    }
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30), "CS_Studio_Ability_EditorTitle".Translate());
            Text.Font = GameFont.Small;

            Rect summaryRect = new Rect(0, 34f, inRect.width, 58f);
            DrawTopSummary(summaryRect);

            float contentY = 98f;
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

                // 右侧：三标签（效果 / 视觉特效 / 运行时组件）
                Rect rightRect = new Rect(inRect.width - RightPanelWidth, contentY, RightPanelWidth, contentHeight);
                DrawRightTabPanel(rightRect);
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

            Rect inner = rect.ContractedBy(Margin);
            float buttonWidth = (inner.width - 10f) / 2f;

            if (Widgets.ButtonText(new Rect(inner.x, inner.y, buttonWidth, 24f), "CS_Studio_File_New".Translate()))
            {
                CreateNewAbility();
            }

            if (Widgets.ButtonText(new Rect(inner.x + buttonWidth + 10f, inner.y, buttonWidth, 24f), "CS_Studio_Panel_Duplicate".Translate()))
            {
                DuplicateSelectedAbility();
            }

            float secondRowY = inner.y + 30f;
            if (Widgets.ButtonText(new Rect(inner.x, secondRowY, buttonWidth, 24f), "CS_Studio_Ability_LoadQwerExamples".Translate()))
            {
                LoadQwerExamples();
            }

            if (Widgets.ButtonText(new Rect(inner.x + buttonWidth + 10f, secondRowY, buttonWidth, 24f), "CS_Studio_Btn_Delete".Translate()))
            {
                if (selectedAbility != null)
                {
                    abilities.Remove(selectedAbility);
                    if (abilities.Count > 0) selectedAbility = abilities[0];
                    else selectedAbility = null;
                }
            }

            float thirdRowY = secondRowY + 30f;
            GUI.enabled = !llmAbilitiesGenerating;
            string genLabel = llmAbilitiesGenerating ? "CS_LLM_Generating".Translate().ToString() : "CS_LLM_GenerateAbilities".Translate().ToString();
            if (Widgets.ButtonText(new Rect(inner.x, thirdRowY, buttonWidth, 24f), genLabel))
            {
                GenerateAbilitiesFromPrompt(false);
            }

            if (Widgets.ButtonText(new Rect(inner.x + buttonWidth + 10f, thirdRowY, buttonWidth, 24f), "CS_LLM_OpenSettings".Translate()))
            {
                Find.WindowStack.Add(new Dialog_LlmSettings());
            }

            float promptY = thirdRowY + 30f;
            Widgets.Label(new Rect(inner.x, promptY, inner.width, 24f), "CS_LLM_AbilityPrompt".Translate());
            llmAbilityPrompt = Widgets.TextArea(new Rect(inner.x, promptY + 22f, inner.width, 76f), llmAbilityPrompt ?? string.Empty);
            Widgets.Label(new Rect(inner.x, promptY + 102f, inner.width, 40f), "CS_LLM_EditorTool_AbilityHint".Translate());

            float promptButtonsY = promptY + 144f;
            if (Widgets.ButtonText(new Rect(inner.x, promptButtonsY, buttonWidth, 24f), "CS_LLM_ApplyReplace".Translate()))
            {
                GenerateAbilitiesFromPrompt(true);
            }

            if (Widgets.ButtonText(new Rect(inner.x + buttonWidth + 10f, promptButtonsY, buttonWidth, 24f), "CS_LLM_ApplyAppend".Translate()))
            {
                GenerateAbilitiesFromPrompt(false);
            }

            float searchY = promptButtonsY + 34f;
            Widgets.Label(new Rect(inner.x, searchY, inner.width, 24f), "CS_Studio_Ability_Search".Translate());
            abilitySearchText = Widgets.TextField(new Rect(inner.x, searchY + 22f, inner.width, 24f), abilitySearchText ?? string.Empty);

            List<ModularAbilityDef> filteredAbilities = GetFilteredAbilities();
            Widgets.Label(new Rect(inner.x, searchY + 50f, inner.width, 24f), "CS_Studio_Ability_CountSummary".Translate(filteredAbilities.Count, abilities.Count));

            float listY = searchY + 78f;
            float listHeight = rect.height - (listY - rect.y) - Margin;
            Rect listRect = new Rect(inner.x, listY, inner.width, listHeight);
            Rect viewRect = new Rect(0, 0, listRect.width - 16f, Mathf.Max(filteredAbilities.Count * 44f, listRect.height - 4f));

            Widgets.BeginScrollView(listRect, ref listScrollPos, viewRect);
            for (int i = 0; i < filteredAbilities.Count; i++)
            {
                var ability = filteredAbilities[i];
                Rect rowRect = new Rect(0, i * 44f, viewRect.width, 40f);
                DrawAbilityRow(rowRect, ability, i);
            }

            if (filteredAbilities.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0, 8f, viewRect.width, 40f), "CS_Studio_Ability_NoResults".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Widgets.EndScrollView();
        }

        private void DrawAbilityProperties(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect contentRect = rect.ContractedBy(Margin);

            // 预算高度：基础段 ~400 + 验证按钮
            float propsHeight = 420f;
            Widgets.BeginScrollView(contentRect, ref propsScrollPos, new Rect(0, 0, contentRect.width - 16, propsHeight));
            
            float y = 0;
            float labelWidth = 100f;
            float fieldWidth = contentRect.width - labelWidth - 30;

            float width = contentRect.width;

            DrawSelectedAbilitySummary(ref y, width);

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
                GetCarrierTypeLabel,
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

            // 运行时组件已移至右侧标签页，这里仅保留验证按钮
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

        private void DuplicateSelectedAbility()
        {
            if (selectedAbility == null) return;

            var copy = selectedAbility.Clone();
            copy.defName = $"{selectedAbility.defName}_Copy_{DateTime.Now.Ticks}";
            copy.label = "CS_Studio_Ability_CopyLabel".Translate(selectedAbility.label ?? "CS_Studio_Ability_DefaultName".Translate());
            abilities.Add(copy);
            selectedAbility = copy;
        }

        private void LoadQwerExamples()
        {
            var exampleAbilities = CreateQwerExampleAbilities();
            var exampleDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ability in exampleAbilities)
            {
                if (!string.IsNullOrEmpty(ability.defName))
                {
                    exampleDefNames.Add(ability.defName);
                }
            }

            abilities.RemoveAll(a => a != null && !string.IsNullOrEmpty(a.defName) && exampleDefNames.Contains(a.defName));
            abilities.AddRange(exampleAbilities);
            selectedAbility = exampleAbilities.Count > 0 ? exampleAbilities[0] : abilities.Count > 0 ? abilities[0] : null;

            if (boundHotkeys != null)
            {
                boundHotkeys.enabled = true;
                boundHotkeys.qAbilityDefName = "CS_Example_Q_ModeSlash";
                boundHotkeys.wAbilityDefName = "CS_Example_W_Pierce";
                boundHotkeys.eAbilityDefName = "CS_Example_E_ShadowStep";
                boundHotkeys.rAbilityDefName = "CS_Example_R_Annihilation";
                boundHotkeys.wComboAbilityDefName = "CS_Example_W_ComboBurst";
            }

            validationSummary = "CS_Studio_Ability_QwerExamplesLoaded".Translate();
        }

        private static List<ModularAbilityDef> CreateQwerExampleAbilities()
        {
            return new List<ModularAbilityDef>
            {
                CreateQExampleAbility(),
                CreateWExampleAbility(),
                CreateWComboExampleAbility(),
                CreateEExampleAbility(),
                CreateRExampleAbility()
            };
        }

        private static ModularAbilityDef CreateQExampleAbility()
        {
            return new ModularAbilityDef
            {
                defName = "CS_Example_Q_ModeSlash",
                label = "Q·四式斩",
                description = "示例：Q 键循环四种前向攻击形态，并开启 W 连段窗口。",
                cooldownTicks = 45f,
                warmupTicks = 0f,
                charges = 1,
                carrierType = AbilityCarrierType.Target,
                range = 5f,
                effects = new List<AbilityEffectConfig>
                {
                    new AbilityEffectConfig
                    {
                        type = AbilityEffectType.Damage,
                        amount = 18f,
                        chance = 1f,
                        damageDef = DamageDefOf.Cut
                    }
                },
                runtimeComponents = new List<AbilityRuntimeComponentConfig>
                {
                    new AbilityRuntimeComponentConfig
                    {
                        type = AbilityRuntimeComponentType.QComboWindow,
                        enabled = true,
                        comboWindowTicks = 18
                    }
                }
            };
        }

        private static ModularAbilityDef CreateWExampleAbility()
        {
            return new ModularAbilityDef
            {
                defName = "CS_Example_W_Pierce",
                label = "W·穿刺",
                description = "示例：W 键默认释放单点高伤穿刺。",
                cooldownTicks = 75f,
                warmupTicks = 8f,
                charges = 1,
                carrierType = AbilityCarrierType.Target,
                range = 2.5f,
                effects = new List<AbilityEffectConfig>
                {
                    new AbilityEffectConfig
                    {
                        type = AbilityEffectType.Damage,
                        amount = 30f,
                        chance = 1f,
                        damageDef = DamageDefOf.Stab
                    }
                }
            };
        }

        private static ModularAbilityDef CreateWComboExampleAbility()
        {
            return new ModularAbilityDef
            {
                defName = "CS_Example_W_ComboBurst",
                label = "W·裂阵追击",
                description = "示例：在 Q 连段窗口内按下 W，触发更大范围的追击爆发。",
                cooldownTicks = 90f,
                warmupTicks = 0f,
                charges = 1,
                carrierType = AbilityCarrierType.Area,
                range = 3f,
                radius = 1.9f,
                effects = new List<AbilityEffectConfig>
                {
                    new AbilityEffectConfig
                    {
                        type = AbilityEffectType.Damage,
                        amount = 42f,
                        chance = 1f,
                        damageDef = DamageDefOf.Bomb
                    }
                }
            };
        }

        private static ModularAbilityDef CreateEExampleAbility()
        {
            return new ModularAbilityDef
            {
                defName = "CS_Example_E_ShadowStep",
                label = "E·影踏",
                description = "示例：E 键执行短位移，落点触发一次伤害效果。",
                cooldownTicks = 120f,
                warmupTicks = 0f,
                charges = 1,
                carrierType = AbilityCarrierType.Target,
                range = 6f,
                effects = new List<AbilityEffectConfig>
                {
                    new AbilityEffectConfig
                    {
                        type = AbilityEffectType.Damage,
                        amount = 24f,
                        chance = 1f,
                        damageDef = DamageDefOf.Blunt
                    }
                },
                runtimeComponents = new List<AbilityRuntimeComponentConfig>
                {
                    new AbilityRuntimeComponentConfig
                    {
                        type = AbilityRuntimeComponentType.EShortJump,
                        enabled = true,
                        cooldownTicks = 120,
                        jumpDistance = 6,
                        findCellRadius = 3,
                        triggerAbilityEffectsAfterJump = true
                    }
                }
            };
        }

        private static ModularAbilityDef CreateRExampleAbility()
        {
            return new ModularAbilityDef
            {
                defName = "CS_Example_R_Annihilation",
                label = "R·歼灭界域",
                description = "示例：R 键先开启近战叠层，再选点延迟跃迁并释放三段爆炸波。",
                cooldownTicks = 240f,
                warmupTicks = 0f,
                charges = 1,
                carrierType = AbilityCarrierType.Area,
                range = 9f,
                radius = 3f,
                effects = new List<AbilityEffectConfig>
                {
                    new AbilityEffectConfig
                    {
                        type = AbilityEffectType.Damage,
                        amount = 12f,
                        chance = 1f,
                        damageDef = DamageDefOf.Bomb
                    }
                },
                runtimeComponents = new List<AbilityRuntimeComponentConfig>
                {
                    new AbilityRuntimeComponentConfig
                    {
                        type = AbilityRuntimeComponentType.RStackDetonation,
                        enabled = true,
                        requiredStacks = 7,
                        delayTicks = 180,
                        wave1Radius = 3f,
                        wave1Damage = 80f,
                        wave2Radius = 6f,
                        wave2Damage = 140f,
                        wave3Radius = 9f,
                        wave3Damage = 220f,
                        waveDamageDef = DamageDefOf.Bomb
                    }
                }
            };
        }

        private void CreateNewAbility()
        {
            var newAbility = new ModularAbilityDef
            {
                defName = $"CS_Ability_{DateTime.Now.Ticks}",
                label = "CS_Studio_Ability_DefaultName".Translate()
            };
            abilities.Add(newAbility);
            selectedAbility = newAbility;
            validationSummary = string.Empty;
        }

        private void DrawTopSummary(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);

            string selectedName  = selectedAbility == null
                ? "CS_Studio_Ability_SelectOrCreate".Translate()
                : (string.IsNullOrWhiteSpace(selectedAbility.label) ? selectedAbility.defName : selectedAbility.label);
            string validationText = selectedAbility == null ? "-" : GetValidationLabel(selectedAbility.Validate());
            string hotkeyText     = GetHotkeySummary();

            // 左侧（40%）：选中技能名 + 热键摘要（Tiny，允许换行）
            float leftW = inner.width * 0.40f;
            Widgets.Label(new Rect(inner.x, inner.y, leftW, 24f),
                "CS_Studio_Ability_SelectedSummary".Translate(selectedName));
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(inner.x, inner.y + 22f, leftW, 24f),
                "CS_Studio_Ability_HotkeySummary".Translate(hotkeyText));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // 中间（28%）：验证状态 + 效果数
            float midX = inner.x + inner.width * 0.42f;
            float midW = inner.width * 0.28f;
            Widgets.Label(new Rect(midX, inner.y,       midW, 24f),
                "CS_Studio_Ability_ValidationSummary".Translate(validationText));
            Widgets.Label(new Rect(midX, inner.y + 22f, midW, 24f),
                "CS_Studio_Ability_EffectsSummary".Translate(
                    selectedAbility?.effects?.Count ?? 0,
                    selectedAbility?.runtimeComponents?.Count ?? 0));

            // 右侧（30%）：绑定到角色
            float bindX = inner.x + inner.width * 0.72f;
            float bindW = inner.width * 0.28f;
            DrawBindToPawnSection(new Rect(bindX, inner.y, bindW, inner.height));
        }

        private void DrawBindToPawnSection(Rect rect)
        {
            // 当前绑定状态
            string pawnLabel = boundPawn != null
                ? boundPawn.LabelShort
                : "CS_Ability_NoPawnBound".Translate();

            Text.Font   = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color   = boundPawn != null ? new Color(0.4f, 1f, 0.5f) : Color.gray;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f),
                "CS_Ability_BoundPawn".Translate() + ": " + pawnLabel);
            GUI.color = Color.white;

            float btnH = 22f;
            float btnW = (rect.width - 4f) / 2f;

            // 选择角色
            if (Widgets.ButtonText(new Rect(rect.x, rect.y + 22f, btnW, btnH), "CS_Ability_SelectPawn".Translate()))
                ShowSelectPawnMenu();

            // 授予 / 撤销
            bool hasAbilities = abilities != null && abilities.Count > 0;
            if (boundPawn != null && hasAbilities)
            {
                if (Widgets.ButtonText(new Rect(rect.x + btnW + 4f, rect.y + 22f, btnW, btnH),
                    "CS_Ability_Grant".Translate()))
                {
                    GrantAbilitiesToBoundPawn();
                }
            }
            else if (boundPawn != null)
            {
                if (Widgets.ButtonText(new Rect(rect.x + btnW + 4f, rect.y + 22f, btnW, btnH),
                    "CS_Ability_Revoke".Translate()))
                {
                    CharacterStudio.Abilities.AbilityGrantUtility.RevokeAllCSAbilitiesFromPawn(boundPawn);
                    Messages.Message("CS_Ability_RevokeSuccess".Translate(boundPawn.LabelShort),
                        MessageTypeDefOf.NeutralEvent, false);
                }
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

            // 包含所有地图上的角色（殖民者、访客、俘虏、动物等）
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

            // 清除绑定
            options.Add(new FloatMenuOption("CS_Ability_ClearBinding".Translate(), () => boundPawn = null));

            foreach (var pawn in pawns)
            {
                var p = pawn;
                string label = p.LabelShort;
                if (boundPawn == p) label = "✓ " + label;
                options.Add(new FloatMenuOption(label, () => boundPawn = p));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void GrantAbilitiesToBoundPawn()
        {
            if (boundPawn == null || abilities == null || abilities.Count == 0) return;

            // 构建临时皮肤容器（不修改 boundSkin，使用独立临时对象）
            var tempSkin = new CharacterStudio.Core.PawnSkinDef();
            foreach (var a in abilities)
                if (a != null) tempSkin.abilities.Add(a);

            CharacterStudio.Abilities.AbilityGrantUtility.GrantSkinAbilitiesToPawn(boundPawn, tempSkin);

            Messages.Message(
                "CS_Ability_GrantSuccess".Translate(abilities.Count, boundPawn.LabelShort),
                MessageTypeDefOf.PositiveEvent, false);
        }

        private void DrawAbilityRow(Rect rowRect, ModularAbilityDef ability, int index)
        {
            UIHelper.DrawAlternatingRowBackground(rowRect, index);

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

            // subline 分两段：左侧 carrierType + CD，右侧 validation（Tiny 字体）
            string sublineLeft  = $"{GetCarrierTypeLabel(ability.carrierType)}  CD:{ability.cooldownTicks:0}t";
            string sublineRight = GetValidationLabel(validation);

            Widgets.Label(new Rect(rowRect.x + 6f, rowRect.y + 2f, rowRect.width - 12f, 20f), $"{statusIcon} {displayName}");
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rowRect.x + 20f, rowRect.y + 20f, rowRect.width * 0.6f - 20f, 16f), sublineLeft);
            GUI.color = validation.IsValid ? (validation.Warnings.Count > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.4f, 1f, 0.5f)) : new Color(1f, 0.35f, 0.35f);
            Widgets.Label(new Rect(rowRect.x + rowRect.width * 0.6f, rowRect.y + 20f, rowRect.width * 0.4f, 16f), sublineRight);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawSelectedAbilitySummary(ref float y, float width)
        {
            if (selectedAbility == null)
            {
                return;
            }

            Rect cardRect = new Rect(0, y, width, 62f);
            Widgets.DrawMenuSection(cardRect);
            Rect inner = cardRect.ContractedBy(8f);
            var validation = selectedAbility.Validate();
            string selectedName = string.IsNullOrWhiteSpace(selectedAbility.label) ? selectedAbility.defName : selectedAbility.label;

            // 左半：技能名（大字）+ defName（小字）
            Widgets.Label(new Rect(inner.x, inner.y, inner.width * 0.52f, 24f), selectedName);
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(inner.x, inner.y + 22f, inner.width * 0.52f, 18f),
                "CS_Studio_Ability_DefSummary".Translate(selectedAbility.defName));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // 右半：验证（顶）+ 热键（底，Tiny）
            Widgets.Label(new Rect(inner.x + inner.width * 0.54f, inner.y, inner.width * 0.46f, 24f),
                "CS_Studio_Ability_ValidationSummary".Translate(GetValidationLabel(validation)));
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(inner.x + inner.width * 0.54f, inner.y + 22f, inner.width * 0.46f, 18f),
                "CS_Studio_Ability_HotkeySummary".Translate(GetHotkeySummaryForSelected()));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            y += 70f;
        }

        private List<ModularAbilityDef> GetFilteredAbilities()
        {
            if (abilities == null || abilities.Count == 0)
            {
                return new List<ModularAbilityDef>();
            }

            string search = (abilitySearchText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(search))
            {
                return abilities;
            }

            return abilities.Where(a => a != null &&
                ((a.label?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                 (a.defName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                 GetCarrierTypeLabel(a.carrierType).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
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

        private static string GetCarrierTypeLabel(AbilityCarrierType type)
        {
            return ($"CS_Ability_CarrierType_{type}").Translate();
        }

        private static string GetEffectTypeLabel(AbilityEffectType type)
        {
            return ($"CS_Ability_EffectType_{type}").Translate();
        }

        private static string GetRuntimeComponentTypeLabel(AbilityRuntimeComponentType type)
        {
            return ($"CS_Ability_RuntimeComponentType_{type}").Translate();
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
        private void GenerateAbilitiesFromPrompt(bool replaceExisting)
        {
            try
            {
                var settings = LlmSettingsRepository.GetOrLoad();
                if (!settings.enabled || !settings.IsConfigured)
                {
                    validationSummary = "CS_LLM_Settings_NotConfigured".Translate();
                    Find.WindowStack.Add(new Dialog_LlmSettings());
                    return;
                }

                if (string.IsNullOrWhiteSpace(llmAbilityPrompt))
                {
                    validationSummary = "CS_LLM_AbilityPrompt_Empty".Translate();
                    return;
                }

                PawnSkinDef skinContext = boundSkin ?? new PawnSkinDef();
                var result = LlmGenerationService.GenerateAbilities(settings, llmAbilityPrompt, skinContext, abilities);
                List<ModularAbilityDef> generated = result.payload ?? new List<ModularAbilityDef>();
                generated = generated.Where(a => a != null).ToList();
                if (generated.Count == 0)
                {
                    validationSummary = "CS_LLM_GenerateAbilitiesEmpty".Translate();
                    return;
                }

                if (replaceExisting)
                {
                    abilities.Clear();
                }

                int beforeCount = abilities.Count;
                abilities.AddRange(generated);
                selectedAbility = generated[0];
                validationSummary = replaceExisting
                    ? "CS_LLM_GenerateAbilitiesReplaced".Translate(generated.Count)
                    : "CS_LLM_GenerateAbilitiesAppended".Translate(generated.Count, beforeCount, abilities.Count);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 技能 LLM 生成失败: {ex}");
                validationSummary = "CS_LLM_GenerateFailed".Translate(ex.Message);
            }
        }
    }
}




