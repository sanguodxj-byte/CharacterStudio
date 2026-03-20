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
    public partial class Dialog_AbilityEditor : Window
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
                var persisted = TryLoadAbilityEditorSessionFromDisk();
                if (persisted.Count > 0)
                {
                    this.abilities = persisted;
                    selectedAbility = this.abilities[0];
                }
                else
                {
                    // 初始化默认技能
                    CreateNewAbility();
                }
            }
            else
            {
                selectedAbility = this.abilities[0];
            }
        }

        private bool DrawToolbarButton(Rect buttonRect, string label, Action action, bool accent = false)
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

        private bool DrawInlineValueButton(Rect buttonRect, string label, Action action, bool accent = false)
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

        public override void PreClose()
        {
            base.PreClose();
            SaveAbilityEditorSessionToDisk();
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
            float tripleButtonWidth = (inner.width - 20f) / 3f;
            float tripleButtonGap = 10f;
            float buttonWidth = (inner.width - 10f) / 2f;

            if (DrawToolbarButton(new Rect(inner.x, inner.y, tripleButtonWidth, 24f), "CS_Studio_File_New".Translate(), CreateNewAbility, true))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + tripleButtonWidth + tripleButtonGap, inner.y, tripleButtonWidth, 24f), "CS_Studio_Panel_Duplicate".Translate(), DuplicateSelectedAbility))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + (tripleButtonWidth + tripleButtonGap) * 2f, inner.y, tripleButtonWidth, 24f), "CS_Studio_Btn_Delete".Translate(), () =>
            {
                if (selectedAbility != null)
                {
                    abilities.Remove(selectedAbility);
                    if (abilities.Count > 0) selectedAbility = abilities[0];
                    else selectedAbility = null;
                }
            }))
            {
            }

            float secondRowY = inner.y + 30f;
            if (DrawToolbarButton(new Rect(inner.x, secondRowY, tripleButtonWidth, 24f), "CS_Studio_Ability_LoadQwerExamples".Translate(), LoadQwerExamples))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + tripleButtonWidth + tripleButtonGap, secondRowY, tripleButtonWidth, 24f), "CS_Studio_Ability_ImportXml".Translate(), OpenImportXmlDialog))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + (tripleButtonWidth + tripleButtonGap) * 2f, secondRowY, tripleButtonWidth, 24f), "CS_Studio_File_Export".Translate(), ExportAbilitiesToDefaultPath))
            {
            }

            float settingsRowY = secondRowY + 30f;
            if (DrawToolbarButton(new Rect(inner.x, settingsRowY, inner.width, 24f), "CS_LLM_OpenSettings".Translate(), () => Find.WindowStack.Add(new Dialog_LlmSettings())))
            {
            }

            float thirdRowY = settingsRowY + 30f;
            GUI.enabled = !llmAbilitiesGenerating;
            string genLabel = llmAbilitiesGenerating ? "CS_LLM_Generating".Translate().ToString() : "CS_LLM_GenerateAbilities".Translate().ToString();
            if (DrawToolbarButton(new Rect(inner.x, thirdRowY, inner.width, 24f), genLabel, () => GenerateAbilitiesFromPrompt(false), true))
            {
            }

            float promptY = thirdRowY + 30f;
            Widgets.Label(new Rect(inner.x, promptY, inner.width, 24f), "CS_LLM_AbilityPrompt".Translate());
            llmAbilityPrompt = Widgets.TextArea(new Rect(inner.x, promptY + 22f, inner.width, 76f), llmAbilityPrompt ?? string.Empty);
            Widgets.Label(new Rect(inner.x, promptY + 102f, inner.width, 40f), "CS_LLM_EditorTool_AbilityHint".Translate());

            float promptButtonsY = promptY + 144f;
            if (DrawToolbarButton(new Rect(inner.x, promptButtonsY, buttonWidth, 24f), "CS_LLM_ApplyReplace".Translate(), () => GenerateAbilitiesFromPrompt(true)))
            {
            }

            if (DrawToolbarButton(new Rect(inner.x + buttonWidth + 10f, promptButtonsY, buttonWidth, 24f), "CS_LLM_ApplyAppend".Translate(), () => GenerateAbilitiesFromPrompt(false), true))
            {
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
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Ability_Type".Translate(), ModularAbilityDefExtensions.NormalizeCarrierType(selectedAbility.carrierType),
                new[] { AbilityCarrierType.Self, AbilityCarrierType.Target, AbilityCarrierType.Projectile },
                GetCarrierTypeLabel,
                val =>
                {
                    selectedAbility.carrierType = val;
                    if (val == AbilityCarrierType.Self)
                    {
                        selectedAbility.targetType = AbilityTargetType.Self;
                        selectedAbility.useRadius = false;
                        selectedAbility.areaCenter = AbilityAreaCenter.Self;
                    }
                    else if (selectedAbility.targetType == AbilityTargetType.Self)
                    {
                        selectedAbility.targetType = AbilityTargetType.Entity;
                    }
                });

            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Ability_TargetType".Translate(), ModularAbilityDefExtensions.NormalizeTargetType(selectedAbility),
                GetAvailableTargetTypes(selectedAbility),
                GetTargetTypeLabel,
                val => selectedAbility.targetType = val);

            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(selectedAbility.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(selectedAbility);

            if (ModularAbilityDefExtensions.CarrierNeedsRange(normalizedCarrier, normalizedTarget))
            {
                UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Range".Translate(), ref selectedAbility.range, 0, 100);
            }

            bool useRadius = selectedAbility.useRadius;
            Widgets.Checkbox(new Vector2(0, y), ref useRadius, 24f);
            selectedAbility.useRadius = useRadius;
            Widgets.Label(new Rect(28f, y, width - 28f, 24f), "CS_Studio_Ability_UseRadius".Translate());
            y += RowHeight;

            if (selectedAbility.useRadius)
            {
                UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Ability_AreaCenter".Translate(), ModularAbilityDefExtensions.NormalizeAreaCenter(selectedAbility),
                    GetAvailableAreaCenters(selectedAbility),
                    GetAreaCenterLabel,
                    val => selectedAbility.areaCenter = val);
                UIHelper.DrawNumericField(ref y, width, "CS_Studio_Ability_Radius".Translate(), ref selectedAbility.radius, 0, 20);
            }

            if (normalizedCarrier == AbilityCarrierType.Projectile)
            {
                Widgets.Label(new Rect(0, y, labelWidth, 24), "CS_Studio_Ability_Projectile".Translate());
                if (DrawInlineValueButton(new Rect(labelWidth, y, fieldWidth, 24), selectedAbility.projectileDef?.label ?? "CS_Studio_None".Translate(), () => ShowProjectileSelector(selectedAbility)))
                {
                }
                y += RowHeight;
            }

            // 运行时组件已移至右侧标签页，这里仅保留验证按钮
            y += 10f;
            if (DrawToolbarButton(new Rect(0, y, width, 28f), "CS_Studio_Ability_Validate".Translate(), () =>
            {
                var result = selectedAbility.Validate();
                validationSummary = result.IsValid
                    ? (result.Warnings.Count > 0
                        ? "CS_Studio_Ability_ValidWithWarnings".Translate() + " (" + result.Warnings.Count + ")"
                        : "CS_Studio_Ability_Valid".Translate())
                    : "CS_Studio_Ability_Invalid".Translate() + " (" + result.Errors.Count + ")";
            }, true))
            {
            }

            if (!string.IsNullOrEmpty(validationSummary))
            {
                y += 34f;
                Widgets.Label(new Rect(0, y, width, 30f), validationSummary);
            }

            Widgets.EndScrollView();
        }

        private void DuplicateSelectedAbility()
        {
            if (selectedAbility == null) return;

            var copy = selectedAbility.Clone();
            copy.defName = GetUniqueAbilityDefName($"{selectedAbility.defName}_Copy");
            copy.label = "CS_Studio_Ability_CopyLabel".Translate(selectedAbility.label ?? "CS_Studio_Ability_DefaultName".Translate());
            abilities.Add(copy);
            selectedAbility = copy;
        }

        private void LoadQwerExamples()
        {
            var exampleAbilities = LoadQwerExampleAbilitiesFromDefs();
            if (exampleAbilities.Count == 0)
            {
                validationSummary = "QWER example abilities were not found in DefDatabase.";
                Log.Warning("[CharacterStudio] QWER 示例技能未在 DefDatabase<ModularAbilityDef> 中找到。");
                return;
            }

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

        private static List<ModularAbilityDef> LoadQwerExampleAbilitiesFromDefs()
        {
            string[] exampleDefNames =
            {
                "CS_Example_Q_ModeSlash",
                "CS_Example_W_Pierce",
                "CS_Example_W_ComboBurst",
                "CS_Example_E_ShadowStep",
                "CS_Example_R_Annihilation"
            };

            var result = new List<ModularAbilityDef>();
            foreach (string defName in exampleDefNames)
            {
                ModularAbilityDef? def = DefDatabase<ModularAbilityDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    Log.Warning($"[CharacterStudio] 未找到示例技能 Def: {defName}");
                    continue;
                }

                result.Add(CreateEditableAbilityCopy(def));
            }

            return result;
        }

        private static ModularAbilityDef CreateEditableAbilityCopy(ModularAbilityDef source)
        {
            var copy = new ModularAbilityDef
            {
                defName = source.defName,
                label = source.label,
                description = source.description,
                iconPath = source.iconPath,
                cooldownTicks = source.cooldownTicks,
                warmupTicks = source.warmupTicks,
                charges = source.charges,
                aiCanUse = source.aiCanUse,
                carrierType = source.carrierType,
                targetType = source.targetType,
                useRadius = source.useRadius,
                areaCenter = source.areaCenter,
                range = source.range,
                radius = source.radius,
                projectileDef = source.projectileDef
            };

            if (source.effects != null)
            {
                foreach (var effect in source.effects)
                {
                    if (effect != null)
                    {
                        copy.effects.Add(effect.Clone());
                    }
                }
            }

            if (source.visualEffects != null)
            {
                foreach (var vfx in source.visualEffects)
                {
                    if (vfx != null)
                    {
                        copy.visualEffects.Add(vfx.Clone());
                    }
                }
            }

            if (source.runtimeComponents != null)
            {
                foreach (var component in source.runtimeComponents)
                {
                    if (component != null)
                    {
                        copy.runtimeComponents.Add(component.Clone());
                    }
                }
            }

            return copy;
        }

        private void CreateNewAbility()
        {
            var newAbility = new ModularAbilityDef
            {
                defName = GetUniqueAbilityDefName("CS_Ability"),
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
            if (DrawToolbarButton(new Rect(rect.x, rect.y + 22f, btnW, btnH), "CS_Ability_SelectPawn".Translate(), ShowSelectPawnMenu))
            {
            }

            bool hasAbilities = abilities != null && abilities.Count > 0;
            if (boundPawn != null)
            {
                if (hasAbilities)
                {
                    if (DrawToolbarButton(new Rect(rect.x + btnW + 4f, rect.y + 22f, btnW, btnH),
                        "CS_Ability_Grant".Translate(), GrantAbilitiesToBoundPawn, true))
                    {
                    }
                }

                if (DrawToolbarButton(new Rect(rect.x, rect.y + 48f, rect.width, btnH),
                    "CS_Ability_Revoke".Translate(), () =>
                {
                    CharacterStudio.Abilities.AbilityGrantUtility.RevokeAllCSAbilitiesFromPawn(boundPawn);
                    Messages.Message("CS_Ability_RevokeSuccess".Translate(boundPawn.LabelShort),
                        MessageTypeDefOf.NeutralEvent, false);
                }))
                {
                }
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x + btnW + 4f, rect.y + 22f, btnW, btnH), "CS_Ability_Grant".Translate());
                GUI.color = Color.white;
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
            string sublineLeft  = $"{GetCarrierTypeLabel(ability.carrierType)} / {GetTargetTypeLabel(ModularAbilityDefExtensions.NormalizeTargetType(ability))}  CD:{ability.cooldownTicks:0}t";
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
                 GetCarrierTypeLabel(a.carrierType).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 GetTargetTypeLabel(ModularAbilityDefExtensions.NormalizeTargetType(a)).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
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
            return ($"CS_Ability_CarrierType_{ModularAbilityDefExtensions.NormalizeCarrierType(type)}").Translate();
        }

        private static string GetTargetTypeLabel(AbilityTargetType type)
        {
            return ($"CS_Ability_TargetType_{type}").Translate();
        }

        private static string GetAreaCenterLabel(AbilityAreaCenter center)
        {
            return ($"CS_Ability_AreaCenter_{center}").Translate();
        }

        private static AbilityTargetType[] GetAvailableTargetTypes(ModularAbilityDef ability)
        {
            AbilityCarrierType carrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            return carrier switch
            {
                AbilityCarrierType.Self => new[] { AbilityTargetType.Self },
                AbilityCarrierType.Projectile => new[] { AbilityTargetType.Entity, AbilityTargetType.Cell },
                _ => new[] { AbilityTargetType.Entity, AbilityTargetType.Cell }
            };
        }

        private static AbilityAreaCenter[] GetAvailableAreaCenters(ModularAbilityDef ability)
        {
            AbilityTargetType target = ModularAbilityDefExtensions.NormalizeTargetType(ability);
            return target == AbilityTargetType.Self
                ? new[] { AbilityAreaCenter.Self }
                : new[] { AbilityAreaCenter.Self, AbilityAreaCenter.Target };
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

                NormalizeImportedAbilityDefNames(generated, replaceExisting ? null : abilities);

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

        private string GetUniqueAbilityDefName(string desiredBase)
        {
            string baseName = string.IsNullOrWhiteSpace(desiredBase) ? "CS_Ability" : desiredBase.Trim();
            string candidate = baseName;
            int suffix = 1;

            while (abilities.Any(a => a != null && string.Equals(a.defName, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"{baseName}_{suffix++}";
            }

            return candidate;
        }

        private static void NormalizeImportedAbilityDefNames(IEnumerable<ModularAbilityDef> incoming, IEnumerable<ModularAbilityDef>? existing)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existing != null)
            {
                foreach (var ability in existing)
                    if (ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                        used.Add(ability.defName);
            }

            foreach (var ability in incoming)
            {
                if (ability == null) continue;
                string baseName = string.IsNullOrWhiteSpace(ability.defName) ? "CS_ImportedAbility" : ability.defName.Trim();
                string candidate = baseName;
                int suffix = 1;
                while (!used.Add(candidate))
                    candidate = $"{baseName}_{suffix++}";
                ability.defName = candidate;
            }
        }
    }
}