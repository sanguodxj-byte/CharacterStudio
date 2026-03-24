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
        private static string GetAbilityTextureRootDir()
        {
            string path = System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Abilities", "Textures");
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        private static string GetAbilityTextureBrowseStartPath(string? currentPath)
        {
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                string trimmed = currentPath!.Trim();
                if (System.IO.Directory.Exists(trimmed))
                {
                    return trimmed;
                }

                string? directory = System.IO.Path.GetDirectoryName(trimmed);
                if (!string.IsNullOrWhiteSpace(directory) && System.IO.Directory.Exists(directory))
                {
                    return directory;
                }
            }

            return GetAbilityTextureRootDir();
        }
        // ─────────────────────────────────────────────
        // 常量
        // ─────────────────────────────────────────────
        private const float LeftSidebarExpandedWidth = 250f;
        private const float LeftSidebarCollapsedWidth = 40f;
        private const float AbilityListPanelWidth = 280f;
        private const float RightPanelWidth = 360f;
        private new const float Margin = 10f;
        private const float RowHeight = 30f;

        // ─────────────────────────────────────────────
        // 状态
        // ─────────────────────────────────────────────
        private List<ModularAbilityDef> abilities = new List<ModularAbilityDef>();
        private ModularAbilityDef? selectedAbility;
        private Vector2 leftSidebarScrollPos;
        private Vector2 listScrollPos;
        private Vector2 propsScrollPos;
        private Vector2 effectsScrollPos;
        private Vector2 vfxScrollPos;
        private Vector2 rcScrollPos;
        private bool propsBaseExpanded = true;
        private bool propsCarrierExpanded = true;
        private bool propsAudioExpanded = true;
        /// <summary>右侧面板标签：0=效果 1=视觉特效 2=运行时组件 3=预览</summary>
        private int rightPanelTab = 0;
        private bool leftSidebarCollapsed;
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

        public override Vector2 InitialSize => new Vector2(1360f, 760f);

        public Dialog_AbilityEditor(List<ModularAbilityDef> abilityList, SkinAbilityHotkeyConfig? hotkeyConfig = null, PawnSkinDef? skin = null)
        {
            this.abilities = abilityList;
            this.boundHotkeys = hotkeyConfig;
            this.boundSkin = skin;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = false;
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

        private Rect DrawAbilityPanelShell(Rect rect, string title, out Rect bodyRect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect titleRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 28f);
            Widgets.DrawBoxSolid(titleRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), UIHelper.AccentSoftColor);

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), title);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            bodyRect = new Rect(rect.x + Margin, titleRect.yMax + 6f, rect.width - Margin * 2f, rect.height - (titleRect.yMax - rect.y) - Margin - 6f);
            return titleRect;
        }

        private void DrawAbilityInfoBanner(Rect rect, string text, bool accent = false)
        {
            Widgets.DrawBoxSolid(rect, accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
            GUI.color = accent ? UIHelper.AccentColor : UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = accent ? UIHelper.HeaderColor : UIHelper.SubtleColor;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, rect.height - 8f), text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        public override void PreClose()
        {
            base.PreClose();
            PersistAbilityEditorState(false);
        }

        private void PersistAbilityEditorState(bool notifyUser)
        {
            SaveAbilityEditorSessionToDisk();

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

                boundSkin.abilityHotkeys ??= new SkinAbilityHotkeyConfig();
                SanitizeHotkeyConfigAgainstAbilities(boundSkin.abilityHotkeys);
            }

            if (boundHotkeys != null)
            {
                SanitizeHotkeyConfigAgainstAbilities(boundHotkeys);
            }

            if (boundPawn != null)
            {
                AbilityLoadoutRuntimeUtility.ApplyExplicitLoadout(boundPawn, abilities, boundHotkeys ?? boundSkin?.abilityHotkeys);
            }

            if (notifyUser)
            {
                validationSummary = "CS_Studio_Ability_SaveSuccess".Translate();
            }
        }

        private void SanitizeHotkeyConfigAgainstAbilities(SkinAbilityHotkeyConfig? hotkeyConfig)
        {
            if (hotkeyConfig == null)
            {
                return;
            }

            hotkeyConfig.qAbilityDefName = ResolveExistingAbilityDefName(hotkeyConfig.qAbilityDefName);
            hotkeyConfig.wAbilityDefName = ResolveExistingAbilityDefName(hotkeyConfig.wAbilityDefName);
            hotkeyConfig.eAbilityDefName = ResolveExistingAbilityDefName(hotkeyConfig.eAbilityDefName);
            hotkeyConfig.rAbilityDefName = ResolveExistingAbilityDefName(hotkeyConfig.rAbilityDefName);
            hotkeyConfig.wComboAbilityDefName = ResolveExistingAbilityDefName(hotkeyConfig.wComboAbilityDefName);
            hotkeyConfig.enabled = !string.IsNullOrWhiteSpace(hotkeyConfig.qAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeyConfig.wAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeyConfig.eAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeyConfig.rAbilityDefName)
                || !string.IsNullOrWhiteSpace(hotkeyConfig.wComboAbilityDefName);
        }

        private string ResolveExistingAbilityDefName(string? desiredDefName)
        {
            if (string.IsNullOrWhiteSpace(desiredDefName))
            {
                return string.Empty;
            }

            return abilities.Any(a => a != null && string.Equals(a.defName, desiredDefName, StringComparison.OrdinalIgnoreCase))
                ? desiredDefName
                : string.Empty;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect titleRect = new Rect(0f, 0f, inRect.width, 28f);
            Widgets.DrawBoxSolid(titleRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), UIHelper.AccentSoftColor);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 140f, titleRect.height), "CS_Studio_Ability_EditorTitle".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float titleButtonWidth = 58f;
            if (DrawToolbarButton(new Rect(titleRect.xMax - titleButtonWidth, titleRect.y + 2f, titleButtonWidth, 24f), "CS_Studio_Ability_Save".Translate(), () => PersistAbilityEditorState(true), true))
            {
            }

            Rect summaryRect = new Rect(0, 34f, inRect.width, 62f);
            DrawTopSummary(summaryRect);

            float contentY = 102f;
            float contentHeight = inRect.height - contentY;
            float sidebarWidth = leftSidebarCollapsed ? LeftSidebarCollapsedWidth : LeftSidebarExpandedWidth;
            float propertiesX = sidebarWidth + Margin + AbilityListPanelWidth + Margin;

            Rect sidebarRect = new Rect(0f, contentY, sidebarWidth, contentHeight);
            DrawAbilitySidebar(sidebarRect);

            Rect listRect = new Rect(sidebarRect.xMax + Margin, contentY, AbilityListPanelWidth, contentHeight);
            DrawAbilityListPanel(listRect);

            if (selectedAbility != null)
            {
                float centerWidth = inRect.width - propertiesX - RightPanelWidth - Margin;
                Rect centerRect = new Rect(propertiesX, contentY, centerWidth, contentHeight);
                DrawAbilityProperties(centerRect);

                Rect rightRect = new Rect(inRect.width - RightPanelWidth, contentY, RightPanelWidth, contentHeight);
                DrawRightTabPanel(rightRect);
            }
            else
            {
                Rect centerRect = new Rect(propertiesX, contentY, inRect.width - propertiesX, contentHeight);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(centerRect, "CS_Studio_Ability_SelectOrCreate".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }


    }
}