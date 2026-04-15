using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
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
        private string tempXenotypeDefName;
        private string tempRaceDisplayName;
        private Dictionary<string, string> tempHotkeySlotBindings;

        public override Vector2 InitialSize => new Vector2(500f, 560f);

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
            tempXenotypeDefName = skin.xenotypeDefName ?? "";
            tempRaceDisplayName = skin.raceDisplayName ?? "";
            tempHotkeySlotBindings = new Dictionary<string, string>(skin.abilityHotkeys?.slotBindings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        }

        public override void PreClose()
        {
            base.PreClose();
            ApplyChanges();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Rect titleRect = UIHelper.DrawPanelShell(shellRect, "CS_Studio_Skin_Settings".Translate(), 0f);

            float y = titleRect.yMax + 8f;
            float labelWidth = 120f;
            float fieldWidth = inRect.width - labelWidth - 20;

            Rect scrollRect = new Rect(0, y, inRect.width, inRect.height - y - 50);
            Rect viewRect = new Rect(0, 0, scrollRect.width - 16, 930);

            UIHelper.DrawContentCard(scrollRect);

            Widgets.BeginScrollView(scrollRect.ContractedBy(2f), ref scrollPos, viewRect);

            float vy = 0;
            float width = viewRect.width;

            // 基本信息
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_SkinBase".Translate());
            UIHelper.DrawPropertyField(ref vy, width, "DefName", ref tempDefName);
            UIHelper.DrawPropertyField(ref vy, width, "CS_Studio_Label".Translate(), ref tempLabel);
            
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

            // 目标限制
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_Targets".Translate());
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Skin_HumanlikeOnly".Translate(), ref skinDef.humanlikeOnly);

            string racesText = skinDef.targetRaces != null && skinDef.targetRaces.Count > 0 ? string.Join(", ", skinDef.targetRaces) : "CS_Studio_AllRaces".Translate();
            UIHelper.DrawPropertyFieldWithButton(ref vy, width, "CS_Studio_Skin_TargetRaces".Translate(), racesText, ShowRaceSelector);
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Skin_DefaultForTargetRaces".Translate(), ref skinDef.applyAsDefaultForTargetRaces,
                "CS_Studio_Skin_DefaultForTargetRaces_Tip".Translate());
            UIHelper.DrawNumericField<int>(ref vy, width, "CS_Studio_Skin_DefaultRacePriority".Translate(), ref skinDef.defaultRacePriority, -9999, 9999);

            // ─────────────────────────────────────────────
            // 种族身份（Xenotype 绑定 & 显示名覆盖）
            // ─────────────────────────────────────────────
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_RaceIdentity".Translate());

            // 种族显示名：角色卡中替换"智人"等原版名称，留空则不覆盖
            UIHelper.DrawPropertyField(ref vy, width, "CS_Studio_RaceDisplayName".Translate(), ref tempRaceDisplayName);

            // Xenotype 绑定：激活皮肤时同步 pawn.genes 到指定 XenotypeDef，留空则不绑定
            // 使用浮动菜单从已加载的 XenotypeDef 中选择
            string xenotypeDisplay = string.IsNullOrEmpty(tempXenotypeDefName)
                ? "CS_Studio_None".Translate().ToString()
                : tempXenotypeDefName;
            UIHelper.DrawPropertyFieldWithButton(
                ref vy, width,
                "CS_Studio_XenotypeDefName".Translate(),
                xenotypeDisplay,
                ShowXenotypeSelector);

            // 技能热键映射
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_AbilityHotkeys".Translate());
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Ability_Hotkey_Enable".Translate(), ref tempHotkeysEnabled);

            DrawHotkeyMappingField(ref vy, width, "CS_Studio_Ability_Hotkey_Q".Translate(), () => tempHotkeySlotBindings.TryGetValue("Q", out string v) ? v : string.Empty, v => tempHotkeySlotBindings["Q"] = v);
            DrawHotkeyMappingField(ref vy, width, "CS_Studio_Ability_Hotkey_W".Translate(), () => tempHotkeySlotBindings.TryGetValue("W", out string v) ? v : string.Empty, v => tempHotkeySlotBindings["W"] = v);
            DrawHotkeyMappingField(ref vy, width, "CS_Studio_Ability_Hotkey_E".Translate(), () => tempHotkeySlotBindings.TryGetValue("E", out string v) ? v : string.Empty, v => tempHotkeySlotBindings["E"] = v);
            DrawHotkeyMappingField(ref vy, width, "CS_Studio_Ability_Hotkey_R".Translate(), () => tempHotkeySlotBindings.TryGetValue("R", out string v) ? v : string.Empty, v => tempHotkeySlotBindings["R"] = v);

            // 武器渲染覆写
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_WeaponRender".Translate());
            if (skinDef.animationConfig == null)
                skinDef.animationConfig = new CharacterStudio.Core.PawnAnimationConfig();
            var wrc = skinDef.animationConfig;
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_WeaponRender_Enable".Translate(), ref wrc.weaponOverrideEnabled);
            if (wrc.weaponOverrideEnabled)
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
                if (UIHelper.DrawToolbarButton(new Rect(0, vy, 120, 24), "CS_Studio_WeaponRender_Reset".Translate()))
                {
                    skinDef.animationConfig = new CharacterStudio.Core.PawnAnimationConfig { weaponOverrideEnabled = true };
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

            if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2 - btnWidth / 2, btnY, btnWidth, 30), "CS_Studio_Btn_OK".Translate(), accent: true))
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
            skinDef.abilityHotkeys.slotBindings.Clear();
            foreach (var kvp in tempHotkeySlotBindings)
            {
                skinDef.abilityHotkeys.slotBindings[kvp.Key] = kvp.Value ?? string.Empty;
            }

            skinDef.xenotypeDefName = tempXenotypeDefName;
            skinDef.raceDisplayName = tempRaceDisplayName;

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

        private void ShowXenotypeSelector()
        {
            var options = new List<FloatMenuOption>();

            // 清除绑定
            options.Add(new FloatMenuOption(
                "CS_Studio_None".Translate(),
                () =>
                {
                    tempXenotypeDefName = "";
                    onChanged?.Invoke();
                }));

            // 枚举所有已加载的 XenotypeDef
            var xenotypes = DefDatabase<XenotypeDef>.AllDefs
                .OrderBy(x => x.label ?? x.defName);

            foreach (var xeno in xenotypes)
            {
                string xenoLabel = string.IsNullOrEmpty(xeno.label)
                    ? xeno.defName
                    : $"{xeno.label} ({xeno.defName})";
                bool isSelected = tempXenotypeDefName == xeno.defName;
                string menuLabel = isSelected ? $"\u2713 {xenoLabel}" : xenoLabel;
                var captured = xeno;
                options.Add(new FloatMenuOption(menuLabel, () =>
                {
                    tempXenotypeDefName = captured.defName;
                    onChanged?.Invoke();
                }));
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
