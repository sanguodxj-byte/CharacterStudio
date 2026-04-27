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
        private string tempXenotypeDefName;
        private string tempRaceDisplayName;
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
            tempXenotypeDefName = skin.xenotypeDefName ?? "";
            tempRaceDisplayName = skin.raceDisplayName ?? "";
            }

        public override void PreClose()
        {
            base.PreClose();
            ApplyChanges();
        }

        public override void DoWindowContents(Rect inRect)
        {
            UIHelper.DrawDialogFrame(inRect, this);

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
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Skin_HideVanillaApparel".Translate(), ref skinDef.hideVanillaApparel);

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
            skinDef.xenotypeDefName = tempXenotypeDefName;
            skinDef.raceDisplayName = tempRaceDisplayName;

            onChanged?.Invoke();
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
