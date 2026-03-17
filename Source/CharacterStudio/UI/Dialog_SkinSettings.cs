using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 皮肤设置对话框
    /// </summary>
    public class Dialog_SkinSettings : Window
    {
        private PawnSkinDef skinDef;
        private Action? onChanged;
        private Vector2 scrollPos;
        private string tempDefName;
        private string tempLabel;
        private string tempDescription;
        private string tempAuthor;
        private string tempVersion;
        private bool tempHotkeysEnabled;
        private string tempQAbilityDefName;
        private string tempWAbilityDefName;
        private string tempEAbilityDefName;
        private string tempRAbilityDefName;
        private string tempWComboAbilityDefName;

        public override Vector2 InitialSize => new Vector2(500f, 500f);

        public Dialog_SkinSettings(PawnSkinDef skin, Action? onChangedCallback = null)
        {
            this.skinDef = skin;
            this.onChanged = onChangedCallback;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;

            // 初始化临时值
            tempDefName = skin.defName ?? "";
            tempLabel = skin.label ?? "";
            tempDescription = skin.description ?? "";
            tempAuthor = skin.author ?? "";
            tempVersion = skin.version ?? "1.0.0";
            tempHotkeysEnabled = skin.abilityHotkeys?.enabled ?? false;
            tempQAbilityDefName = skin.abilityHotkeys?.qAbilityDefName ?? "";
            tempWAbilityDefName = skin.abilityHotkeys?.wAbilityDefName ?? "";
            tempEAbilityDefName = skin.abilityHotkeys?.eAbilityDefName ?? "";
            tempRAbilityDefName = skin.abilityHotkeys?.rAbilityDefName ?? "";
            tempWComboAbilityDefName = skin.abilityHotkeys?.wComboAbilityDefName ?? "";
        }

        public override void PreClose()
        {
            base.PreClose();
            ApplyChanges();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30), "CS_Studio_Skin_Settings".Translate());
            Text.Font = GameFont.Small;

            float y = 40;
            float labelWidth = 120f;
            float fieldWidth = inRect.width - labelWidth - 20;

            Rect scrollRect = new Rect(0, y, inRect.width, inRect.height - y - 50);
            Rect viewRect = new Rect(0, 0, scrollRect.width - 16, 760);

            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            float vy = 0;
            float width = viewRect.width;

            // 基本信息
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_SkinBase".Translate());
            UIHelper.DrawPropertyField(ref vy, width, "DefName", ref tempDefName);
            UIHelper.DrawPropertyField(ref vy, width, "CS_Studio_Label".Translate(), ref tempLabel);
            
            // 描述使用原生的 TextArea，UIHelper 暂未包含
            Widgets.Label(new Rect(0, vy, 100, 24), "CS_Studio_Description".Translate());
            tempDescription = Widgets.TextArea(new Rect(100, vy, width - 100, 60), tempDescription);
            vy += 65;

            UIHelper.DrawPropertyField(ref vy, width, "CS_Studio_Author".Translate(), ref tempAuthor);
            UIHelper.DrawPropertyField(ref vy, width, "CS_Studio_Version".Translate(), ref tempVersion);

            // 隐藏设置
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_HideVanilla".Translate());
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Skin_HideVanillaHead".Translate(), ref skinDef.hideVanillaHead);
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Skin_HideVanillaHair".Translate(), ref skinDef.hideVanillaHair);
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Skin_HideVanillaBody".Translate(), ref skinDef.hideVanillaBody);
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Skin_HideVanillaApparel".Translate(), ref skinDef.hideVanillaApparel);

            // 目标限制
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_Targets".Translate());
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Skin_HumanlikeOnly".Translate(), ref skinDef.humanlikeOnly);

            string racesText = skinDef.targetRaces != null && skinDef.targetRaces.Count > 0 ? string.Join(", ", skinDef.targetRaces) : "CS_Studio_AllRaces".Translate();
            UIHelper.DrawPropertyFieldWithButton(ref vy, width, "CS_Studio_Skin_TargetRaces".Translate(), racesText, ShowRaceSelector);

            // 技能热键映射
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_AbilityHotkeys".Translate());
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Ability_Hotkey_Enable".Translate(), ref tempHotkeysEnabled);

            DrawHotkeyMappingField(ref vy, width, "CS_Studio_Ability_Hotkey_Q".Translate(), () => tempQAbilityDefName, v => tempQAbilityDefName = v);
            DrawHotkeyMappingField(ref vy, width, "CS_Studio_Ability_Hotkey_W".Translate(), () => tempWAbilityDefName, v => tempWAbilityDefName = v);
            DrawHotkeyMappingField(ref vy, width, "CS_Studio_Ability_Hotkey_E".Translate(), () => tempEAbilityDefName, v => tempEAbilityDefName = v);
            DrawHotkeyMappingField(ref vy, width, "CS_Studio_Ability_Hotkey_R".Translate(), () => tempRAbilityDefName, v => tempRAbilityDefName = v);
            DrawHotkeyMappingField(ref vy, width, "CS_Studio_Ability_Hotkey_WCombo".Translate(), () => tempWComboAbilityDefName, v => tempWComboAbilityDefName = v);

            // 武器渲染覆写
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_WeaponRender".Translate());
            if (skinDef.weaponRenderConfig == null)
                skinDef.weaponRenderConfig = new CharacterStudio.Core.WeaponRenderConfig();
            var wrc = skinDef.weaponRenderConfig;
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_WeaponRender_Enable".Translate(), ref wrc.enabled);
            if (wrc.enabled)
            {
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_WeaponRender_ApplyOffHand".Translate(), ref wrc.applyToOffHand);
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_ScaleX".Translate(), ref wrc.scale.x, 0.1f, 3f, "F2");
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_ScaleY".Translate(), ref wrc.scale.y, 0.1f, 3f, "F2");
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_OffsetX".Translate(), ref wrc.offset.x, -2f, 2f, "F3");
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_OffsetY".Translate(), ref wrc.offset.y, -2f, 2f, "F3");
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_OffsetZ".Translate(), ref wrc.offset.z, -2f, 2f, "F3");
                // 方向特定偏移 (南/北/东)
                UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_WeaponRender_DirOffsets".Translate());
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_OffSouthX".Translate(), ref wrc.offsetSouth.x, -2f, 2f, "F3");
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_OffSouthZ".Translate(), ref wrc.offsetSouth.z, -2f, 2f, "F3");
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_OffNorthX".Translate(), ref wrc.offsetNorth.x, -2f, 2f, "F3");
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_OffNorthZ".Translate(), ref wrc.offsetNorth.z, -2f, 2f, "F3");
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_OffEastX".Translate(), ref wrc.offsetEast.x, -2f, 2f, "F3");
                UIHelper.DrawPropertySlider(ref vy, width, "CS_Studio_WeaponRender_OffEastZ".Translate(), ref wrc.offsetEast.z, -2f, 2f, "F3");

                // 重置按钮
                if (Widgets.ButtonText(new Rect(0, vy, 120, 24), "CS_Studio_WeaponRender_Reset".Translate()))
                {
                    skinDef.weaponRenderConfig = new CharacterStudio.Core.WeaponRenderConfig { enabled = true };
                    onChanged?.Invoke();
                }
                vy += 30f;
            }

            Widgets.EndScrollView();

            // ─────────────────────────────────────────────
            // 底部按钮
            // ─────────────────────────────────────────────

            float btnWidth = 100f;
            float btnY = inRect.height - 35;

            if (Widgets.ButtonText(new Rect(inRect.width / 2 - btnWidth / 2, btnY, btnWidth, 30), "CS_Studio_Btn_OK".Translate()))
            {
                Close();
            }
        }

        private void ApplyChanges()
        {
            skinDef.defName = tempDefName;
            skinDef.label = tempLabel;
            skinDef.description = tempDescription;
            skinDef.author = tempAuthor;
            skinDef.version = tempVersion;
            if (skinDef.abilityHotkeys == null)
            {
                skinDef.abilityHotkeys = new SkinAbilityHotkeyConfig();
            }

            skinDef.abilityHotkeys.enabled = tempHotkeysEnabled;
            skinDef.abilityHotkeys.qAbilityDefName = tempQAbilityDefName;
            skinDef.abilityHotkeys.wAbilityDefName = tempWAbilityDefName;
            skinDef.abilityHotkeys.eAbilityDefName = tempEAbilityDefName;
            skinDef.abilityHotkeys.rAbilityDefName = tempRAbilityDefName;
            skinDef.abilityHotkeys.wComboAbilityDefName = tempWComboAbilityDefName;

            onChanged?.Invoke();
        }

        private void DrawHotkeyMappingField(ref float y, float width, string label, Func<string> getter, Action<string> setter)
        {
            string current = getter();
            string display = string.IsNullOrEmpty(current) ? "CS_Studio_Ability_Hotkey_None".Translate() : current;
            UIHelper.DrawPropertyFieldWithButton(ref y, width, label, display, () => ShowAbilitySelector(setter));
        }

        private void ShowAbilitySelector(Action<string> onSelect)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Ability_Hotkey_None".Translate(), () =>
                {
                    onSelect("");
                    onChanged?.Invoke();
                })
            };

            if (skinDef.abilities != null)
            {
                foreach (var ability in skinDef.abilities)
                {
                    if (ability == null || string.IsNullOrEmpty(ability.defName)) continue;
                    string abilityLabel = string.IsNullOrEmpty(ability.label) ? ability.defName : $"{ability.label} ({ability.defName})";

                    options.Add(new FloatMenuOption(abilityLabel, () =>
                    {
                        onSelect(ability.defName);
                        onChanged?.Invoke();
                    }));
                }
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowRaceSelector()
        {
            var options = new List<FloatMenuOption>();

            // 清除选项
            options.Add(new FloatMenuOption("CS_Studio_AllRaces".Translate(), () =>
            {
                skinDef.targetRaces.Clear();
                onChanged?.Invoke();
            }));

            // 获取所有人形种族
            var races = DefDatabase<ThingDef>.AllDefs
                .Where(d => d.race != null && d.race.Humanlike)
                .OrderBy(d => d.label);

            foreach (var race in races)
            {
                bool isSelected = skinDef.targetRaces.Contains(race.defName);
                string label = isSelected ? $"✓ {race.label}" : race.label;

                options.Add(new FloatMenuOption(label, () =>
                {
                    if (isSelected)
                    {
                        skinDef.targetRaces.Remove(race.defName);
                    }
                    else
                    {
                        skinDef.targetRaces.Add(race.defName);
                    }
                    onChanged?.Invoke();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
