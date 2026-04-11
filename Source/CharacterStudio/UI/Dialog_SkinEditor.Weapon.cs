using System;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private static readonly WeaponCarryVisualState[] CachedWeaponCarryVisualStates =
            (WeaponCarryVisualState[])Enum.GetValues(typeof(WeaponCarryVisualState));

        private void DrawWeaponPanel(Rect rect)
        {
            Rect titleRect = UIHelper.DrawPanelShell(rect, "CS_Studio_Tab_Weapon".Translate(), Margin);

            float contentY = titleRect.yMax + 8f;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            UIHelper.DrawContentCard(contentRect);

            Rect viewRect = new Rect(0, 0, contentRect.width - 20f, Mathf.Max(contentRect.height + 320f, 980f));

            Widgets.BeginScrollView(contentRect.ContractedBy(2f), ref propsScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            workingSkin.weaponRenderConfig ??= new WeaponRenderConfig();
            workingSkin.weaponRenderConfig.carryVisual ??= new WeaponCarryVisualConfig();
            var carry = workingSkin.weaponRenderConfig.carryVisual;
            var weaponOverride = workingSkin.weaponRenderConfig;

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Section_WeaponCarryVisual".Translate());

            WeaponCarryVisualConfig carrySnapshot = carry.Clone();
            WeaponRenderConfig weaponOverrideSnapshot = weaponOverride.Clone();

            bool carryEnabled = carry.enabled;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_WeaponCarry_Enable".Translate(), ref carryEnabled);
            if (carryEnabled != carry.enabled)
            {
                MutateWithUndo(() => carry.enabled = carryEnabled, refreshPreview: true, refreshRenderTree: targetPawn != null);
            }

            UIHelper.DrawPropertyDropdown(ref y, width,
                "CS_Studio_WeaponCarry_PreviewState".Translate(),
                previewWeaponCarryState,
                CachedWeaponCarryVisualStates,
                state => ($"CS_Studio_WeaponCarry_State_{state}").Translate(),
                state =>
                {
                    previewWeaponCarryState = state;
                    RefreshPreview();
                });

            string[] anchorOptions = { "Body", "Head", "Root" };
            UIHelper.DrawPropertyDropdown(ref y, width,
                "CS_Studio_WeaponCarry_Anchor".Translate(),
                carry.anchorTag,
                anchorOptions,
                option => option,
                value =>
                {
                    MutateWithUndo(() => carry.anchorTag = value, refreshPreview: true, refreshRenderTree: targetPawn != null);
                });

            string texUndrafted = carry.texUndrafted ?? string.Empty;
            if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_WeaponCarry_TexUndrafted".Translate(), ref texUndrafted, () =>
                Find.WindowStack.Add(new Dialog_FileBrowser(carry.texUndrafted ?? string.Empty, path =>
                {
                    MutateWithUndo(() => carry.texUndrafted = path ?? string.Empty, refreshPreview: true, refreshRenderTree: targetPawn != null);
                }))))
            {
                MutateWithUndo(() => carry.texUndrafted = texUndrafted, refreshPreview: true, refreshRenderTree: targetPawn != null);
            }

            string texDrafted = carry.texDrafted ?? string.Empty;
            if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_WeaponCarry_TexDrafted".Translate(), ref texDrafted, () =>
                Find.WindowStack.Add(new Dialog_FileBrowser(carry.texDrafted ?? string.Empty, path =>
                {
                    MutateWithUndo(() => carry.texDrafted = path ?? string.Empty, refreshPreview: true, refreshRenderTree: targetPawn != null);
                }))))
            {
                MutateWithUndo(() => carry.texDrafted = texDrafted, refreshPreview: true, refreshRenderTree: targetPawn != null);
            }

            string texCasting = carry.texCasting ?? string.Empty;
            if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_WeaponCarry_TexCasting".Translate(), ref texCasting, () =>
                Find.WindowStack.Add(new Dialog_FileBrowser(carry.texCasting ?? string.Empty, path =>
                {
                    MutateWithUndo(() => carry.texCasting = path ?? string.Empty, refreshPreview: true, refreshRenderTree: targetPawn != null);
                }))))
            {
                MutateWithUndo(() => carry.texCasting = texCasting, refreshPreview: true, refreshRenderTree: targetPawn != null);
            }

            Text.Font = GameFont.Tiny;
            string carryHint = "CS_Studio_WeaponCarry_Hint".Translate();
            float carryHintHeight = Mathf.Max(36f, Text.CalcHeight(carryHint, Mathf.Max(40f, width - 16f)) + 12f);
            Rect carryHintRect = new Rect(0f, y, width, carryHintHeight);
            Widgets.DrawBoxSolid(carryHintRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(carryHintRect, 1);
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(8f, y + 4f, width - 16f, carryHintHeight - 8f), carryHint);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += carryHintHeight + 6f;

            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_DrawOrder".Translate(), ref carry.drawOrder, -10f, 120f, "F0");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_ScaleX".Translate(), ref carry.scale.x, 0.1f, 3f, "F2");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_ScaleY".Translate(), ref carry.scale.y, 0.1f, 3f, "F2");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffsetX".Translate(), ref carry.offset.x, -2f, 2f, "F3");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffsetY".Translate(), ref carry.offset.y, -2f, 2f, "F3");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffsetZ".Translate(), ref carry.offset.z, -2f, 2f, "F3");

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_WeaponCarry_DirOffsets".Translate());
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffNorthX".Translate(), ref carry.offsetNorth.x, -2f, 2f, "F3");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffNorthZ".Translate(), ref carry.offsetNorth.z, -2f, 2f, "F3");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffEastX".Translate(), ref carry.offsetEast.x, -2f, 2f, "F3");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffEastZ".Translate(), ref carry.offsetEast.z, -2f, 2f, "F3");

            if (UIHelper.DrawToolbarButton(new Rect(0f, y, 140f, 24f), "CS_Studio_WeaponCarry_Reset".Translate()))
            {
                MutateWithUndo(() => weaponOverride.carryVisual = new WeaponCarryVisualConfig { enabled = true, anchorTag = carry.anchorTag }, refreshPreview: true, refreshRenderTree: targetPawn != null);
            }
            y += 32f;

            if (!AreCarryVisualConfigsEqual(carrySnapshot, carry))
                FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: targetPawn != null);

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Section_WeaponRender".Translate());

            Rect renderSummaryRect = new Rect(0f, y, width, 40f);
            UIHelper.DrawContentCard(renderSummaryRect);
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(8f, y + 4f, width - 16f, 16f),
                "CS_Studio_WeaponRender_SummaryCarry".Translate((string.IsNullOrWhiteSpace(carry.texUndrafted) ? 0 : 1) + (string.IsNullOrWhiteSpace(carry.texDrafted) ? 0 : 1) + (string.IsNullOrWhiteSpace(carry.texCasting) ? 0 : 1), 3));
            Widgets.Label(new Rect(8f, y + 20f, width - 16f, 16f),
                "CS_Studio_WeaponRender_SummaryOverride".Translate(weaponOverride.enabled ? 1 : 0, 1));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 46f;

            bool overrideEnabled = weaponOverride.enabled;
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_WeaponRender_Enable".Translate(), ref overrideEnabled);
            if (overrideEnabled != weaponOverride.enabled)
            {
                MutateWithUndo(() => weaponOverride.enabled = overrideEnabled, refreshPreview: true, refreshRenderTree: false);
            }

            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_WeaponRender_ApplyOffHand".Translate(), ref weaponOverride.applyToOffHand);
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_ScaleX".Translate(), ref weaponOverride.scale.x, 0.1f, 3f, "F2");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_ScaleY".Translate(), ref weaponOverride.scale.y, 0.1f, 3f, "F2");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffsetX".Translate(), ref weaponOverride.offset.x, -2f, 2f, "F3");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffsetY".Translate(), ref weaponOverride.offset.y, -2f, 2f, "F3");
            UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffsetZ".Translate(), ref weaponOverride.offset.z, -2f, 2f, "F3");

            if (!AreWeaponRenderConfigsEqual(weaponOverrideSnapshot, weaponOverride))
                FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: targetPawn != null);

            viewRect.height = Mathf.Max(y + 10f, contentRect.height - 4f);
            Widgets.EndScrollView();
        }

        private static bool AreCarryVisualConfigsEqual(WeaponCarryVisualConfig a, WeaponCarryVisualConfig b)
        {
            return a.enabled == b.enabled &&
                   a.anchorTag == b.anchorTag &&
                   a.texUndrafted == b.texUndrafted &&
                   a.texDrafted == b.texDrafted &&
                   a.texCasting == b.texCasting &&
                   a.offset == b.offset &&
                   a.offsetNorth == b.offsetNorth &&
                   a.offsetEast == b.offsetEast &&
                   a.scale == b.scale &&
                   Math.Abs(a.drawOrder - b.drawOrder) < 0.0001f;
        }

        private static bool AreWeaponRenderConfigsEqual(WeaponRenderConfig a, WeaponRenderConfig b)
        {
            return a.enabled == b.enabled &&
                   a.applyToOffHand == b.applyToOffHand &&
                   a.offset == b.offset &&
                   a.offsetSouth == b.offsetSouth &&
                   a.offsetNorth == b.offsetNorth &&
                   a.offsetEast == b.offsetEast &&
                   a.scale == b.scale;
        }
    }
}
