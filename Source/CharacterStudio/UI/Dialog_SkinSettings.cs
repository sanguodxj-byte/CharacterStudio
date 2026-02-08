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
            Rect viewRect = new Rect(0, 0, scrollRect.width - 16, 500);

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

            Widgets.EndScrollView();

            // ─────────────────────────────────────────────
            // 底部按钮
            // ─────────────────────────────────────────────

            float btnWidth = 100f;
            float btnY = inRect.height - 35;

            if (Widgets.ButtonText(new Rect(inRect.width / 2 - btnWidth - 10, btnY, btnWidth, 30), "CS_Studio_Btn_OK".Translate()))
            {
                ApplyChanges();
                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2 + 10, btnY, btnWidth, 30), "CS_Studio_Btn_Cancel".Translate()))
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

            onChanged?.Invoke();
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
