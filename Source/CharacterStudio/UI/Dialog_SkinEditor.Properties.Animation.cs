using System;
using UnityEngine;
using Verse;
using CharacterStudio.Core;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void DrawAnimationProperties(Rect rect)
        {
            float propsY = GetPropertiesContentTop(rect);
            float propsHeight = rect.height - propsY + rect.y - Margin;
            Rect propsRect = new Rect(rect.x + Margin, propsY, rect.width - Margin * 2, propsHeight);
            Rect viewRect = new Rect(0, 0, propsRect.width - 16, 800);

            Widgets.BeginScrollView(propsRect, ref propsScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            DrawSkinAnimationSections(ref y, width);

            viewRect.height = Mathf.Max(y + 10f, propsRect.height - 4f);
            Widgets.EndScrollView();
        }

        /// <summary>
        /// 绘制皮肤级动画配置段（程序化动画、持握视觉、渲染覆盖）。
        /// 可从 DrawAnimationProperties 或 DrawEquipmentProperties(equipmentMode) 复用。
        /// </summary>
        private void DrawSkinAnimationSections(ref float y, float width)
        {
            workingSkin.animationConfig ??= new PawnAnimationConfig();
            workingSkin.animationConfig.carryVisual ??= new WeaponCarryVisualConfig();
            var carry = workingSkin.animationConfig.carryVisual;
            var animationOverride = workingSkin.animationConfig;

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_Animation".Translate(), "ProceduralAnimation"))
            {
                var proc = animationOverride.procedural;

                bool breathing = proc.breathingEnabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Anim_Breathe".Translate(), ref breathing);
                if (breathing != proc.breathingEnabled) { MutateWithUndo(() => proc.breathingEnabled = breathing, refreshPreview: true); }

                if (proc.breathingEnabled)
                {
                    float bSpeed = proc.breathingSpeed;
                    UIHelper.DrawPropertySlider(ref y, width, "  " + "CS_Studio_Anim_Speed".Translate(), ref bSpeed, 0.1f, 5f, "F2");
                    if (Math.Abs(bSpeed - proc.breathingSpeed) > 0.001f) { MutateWithUndo(() => proc.breathingSpeed = bSpeed, refreshPreview: true); }

                    float bAmp = proc.breathingAmplitude;
                    UIHelper.DrawPropertySlider(ref y, width, "  " + "CS_Studio_Anim_Amplitude".Translate(), ref bAmp, 0.001f, 0.1f, "F3");
                    if (Math.Abs(bAmp - proc.breathingAmplitude) > 0.0001f) { MutateWithUndo(() => proc.breathingAmplitude = bAmp, refreshPreview: true); }
                }

                bool hovering = proc.hoveringEnabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_Anim_IdleSway".Translate(), ref hovering);
                if (hovering != proc.hoveringEnabled) { MutateWithUndo(() => proc.hoveringEnabled = hovering, refreshPreview: true); }

                if (proc.hoveringEnabled)
                {
                    float hSpeed = proc.hoveringSpeed;
                    UIHelper.DrawPropertySlider(ref y, width, "  " + "CS_Studio_Anim_Speed".Translate(), ref hSpeed, 0.1f, 5f, "F2");
                    if (Math.Abs(hSpeed - proc.hoveringSpeed) > 0.001f) { MutateWithUndo(() => proc.hoveringSpeed = hSpeed, refreshPreview: true); }

                    float hAmp = proc.hoveringAmplitude;
                    UIHelper.DrawPropertySlider(ref y, width, "  " + "CS_Studio_Anim_Amplitude".Translate(), ref hAmp, 0.001f, 0.2f, "F3");
                    if (Math.Abs(hAmp - proc.hoveringAmplitude) > 0.0001f) { MutateWithUndo(() => proc.hoveringAmplitude = hAmp, refreshPreview: true); }
                }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_AnimationCarryVisual".Translate(), "AnimationCarryVisualTextures"))
            {
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
                    Find.WindowStack.Add(new Dialog_FileBrowser(GetEquipmentTextureBrowseStartPath(carry.texUndrafted), path =>
                    {
                        MutateWithUndo(() => carry.texUndrafted = path ?? string.Empty, refreshPreview: true, refreshRenderTree: targetPawn != null);
                    }, defaultRoot: GetEquipmentRootDir()))))
                {
                    MutateWithUndo(() => carry.texUndrafted = texUndrafted, refreshPreview: true, refreshRenderTree: targetPawn != null);
                }

                string texDrafted = carry.texDrafted ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_WeaponCarry_TexDrafted".Translate(), ref texDrafted, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(GetEquipmentTextureBrowseStartPath(carry.texDrafted), path =>
                    {
                        MutateWithUndo(() => carry.texDrafted = path ?? string.Empty, refreshPreview: true, refreshRenderTree: targetPawn != null);
                    }, defaultRoot: GetEquipmentRootDir()))))
                {
                    MutateWithUndo(() => carry.texDrafted = texDrafted, refreshPreview: true, refreshRenderTree: targetPawn != null);
                }

                string texCasting = carry.texCasting ?? string.Empty;
                if (UIHelper.DrawPathFieldWithBrowser(ref y, width, "CS_Studio_WeaponCarry_TexCasting".Translate(), ref texCasting, () =>
                    Find.WindowStack.Add(new Dialog_FileBrowser(GetEquipmentTextureBrowseStartPath(carry.texCasting), path =>
                    {
                        MutateWithUndo(() => carry.texCasting = path ?? string.Empty, refreshPreview: true, refreshRenderTree: targetPawn != null);
                    }, defaultRoot: GetEquipmentRootDir()))))
                {
                    MutateWithUndo(() => carry.texCasting = texCasting, refreshPreview: true, refreshRenderTree: targetPawn != null);
                }

                UIHelper.DrawInfoBanner(ref y, width, "CS_Studio_WeaponCarry_Hint".Translate());
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_AnimationCarryVisual".Translate() + " - Transform", "AnimationCarryVisualTransform"))
            {
                float drawOrder = carry.drawOrder;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_DrawOrder".Translate(), ref drawOrder, -10f, 120f, "F0");
                if (Math.Abs(drawOrder - carry.drawOrder) > 0.0001f) { MutateWithUndo(() => carry.drawOrder = drawOrder, refreshPreview: true, refreshRenderTree: targetPawn != null); }

                float scaleX = carry.scale.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_ScaleX".Translate(), ref scaleX, 0.1f, 3f, "F2");
                if (Math.Abs(scaleX - carry.scale.x) > 0.0001f) { MutateWithUndo(() => carry.scale.x = scaleX, refreshPreview: true, refreshRenderTree: targetPawn != null); }

                float scaleY = carry.scale.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_ScaleY".Translate(), ref scaleY, 0.1f, 3f, "F2");
                if (Math.Abs(scaleY - carry.scale.y) > 0.0001f) { MutateWithUndo(() => carry.scale.y = scaleY, refreshPreview: true, refreshRenderTree: targetPawn != null); }

                float offsetX = carry.offset.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffsetX".Translate(), ref offsetX, -2f, 2f, "F3");
                if (Math.Abs(offsetX - carry.offset.x) > 0.0001f) { MutateWithUndo(() => carry.offset.x = offsetX, refreshPreview: true, refreshRenderTree: targetPawn != null); }

                float offsetY = carry.offset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffsetY".Translate(), ref offsetY, -2f, 2f, "F3");
                if (Math.Abs(offsetY - carry.offset.y) > 0.0001f) { MutateWithUndo(() => carry.offset.y = offsetY, refreshPreview: true, refreshRenderTree: targetPawn != null); }

                float offsetZ = carry.offset.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffsetZ".Translate(), ref offsetZ, -2f, 2f, "F3");
                if (Math.Abs(offsetZ - carry.offset.z) > 0.0001f) { MutateWithUndo(() => carry.offset.z = offsetZ, refreshPreview: true, refreshRenderTree: targetPawn != null); }

                UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_WeaponCarry_DirOffsets".Translate());
                float offNorthX = carry.offsetNorth.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffNorthX".Translate(), ref offNorthX, -2f, 2f, "F3");
                if (Math.Abs(offNorthX - carry.offsetNorth.x) > 0.0001f) { MutateWithUndo(() => carry.offsetNorth.x = offNorthX, refreshPreview: true, refreshRenderTree: targetPawn != null); }

                float offNorthZ = carry.offsetNorth.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffNorthZ".Translate(), ref offNorthZ, -2f, 2f, "F3");
                if (Math.Abs(offNorthZ - carry.offsetNorth.z) > 0.0001f) { MutateWithUndo(() => carry.offsetNorth.z = offNorthZ, refreshPreview: true, refreshRenderTree: targetPawn != null); }

                float offEastX = carry.offsetEast.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffEastX".Translate(), ref offEastX, -2f, 2f, "F3");
                if (Math.Abs(offEastX - carry.offsetEast.x) > 0.0001f) { MutateWithUndo(() => carry.offsetEast.x = offEastX, refreshPreview: true, refreshRenderTree: targetPawn != null); }

                float offEastZ = carry.offsetEast.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponCarry_OffEastZ".Translate(), ref offEastZ, -2f, 2f, "F3");
                if (Math.Abs(offEastZ - carry.offsetEast.z) > 0.0001f) { MutateWithUndo(() => carry.offsetEast.z = offEastZ, refreshPreview: true, refreshRenderTree: targetPawn != null); }
            }

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_AnimationRender".Translate() + " - Transform", "AnimationRenderOverrideTransform"))
            {
                float scaleX = animationOverride.scale.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_ScaleX".Translate(), ref scaleX, 0.1f, 3f, "F2");
                if (Math.Abs(scaleX - animationOverride.scale.x) > 0.0001f) { MutateWithUndo(() => animationOverride.scale.x = scaleX, refreshPreview: true, refreshRenderTree: false); }

                float scaleY = animationOverride.scale.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_ScaleY".Translate(), ref scaleY, 0.1f, 3f, "F2");
                if (Math.Abs(scaleY - animationOverride.scale.y) > 0.0001f) { MutateWithUndo(() => animationOverride.scale.y = scaleY, refreshPreview: true, refreshRenderTree: false); }

                float offsetX = animationOverride.offset.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffsetX".Translate(), ref offsetX, -2f, 2f, "F3");
                if (Math.Abs(offsetX - animationOverride.offset.x) > 0.0001f) { MutateWithUndo(() => animationOverride.offset.x = offsetX, refreshPreview: true, refreshRenderTree: false); }

                float offsetY = animationOverride.offset.y;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffsetY".Translate(), ref offsetY, -2f, 2f, "F3");
                if (Math.Abs(offsetY - animationOverride.offset.y) > 0.0001f) { MutateWithUndo(() => animationOverride.offset.y = offsetY, refreshPreview: true, refreshRenderTree: false); }

                float offsetZ = animationOverride.offset.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffsetZ".Translate(), ref offsetZ, -2f, 2f, "F3");
                if (Math.Abs(offsetZ - animationOverride.offset.z) > 0.0001f) { MutateWithUndo(() => animationOverride.offset.z = offsetZ, refreshPreview: true, refreshRenderTree: false); }

                UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_WeaponRender_DirOffsets".Translate());
                float offNorthX = animationOverride.offsetNorth.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffNorthX".Translate(), ref offNorthX, -2f, 2f, "F3");
                if (Math.Abs(offNorthX - animationOverride.offsetNorth.x) > 0.0001f) { MutateWithUndo(() => animationOverride.offsetNorth.x = offNorthX, refreshPreview: true, refreshRenderTree: false); }

                float offNorthZ = animationOverride.offsetNorth.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffNorthZ".Translate(), ref offNorthZ, -2f, 2f, "F3");
                if (Math.Abs(offNorthZ - animationOverride.offsetNorth.z) > 0.0001f) { MutateWithUndo(() => animationOverride.offsetNorth.z = offNorthZ, refreshPreview: true, refreshRenderTree: false); }

                float offSouthX = animationOverride.offsetSouth.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffSouthX".Translate(), ref offSouthX, -2f, 2f, "F3");
                if (Math.Abs(offSouthX - animationOverride.offsetSouth.x) > 0.0001f) { MutateWithUndo(() => animationOverride.offsetSouth.x = offSouthX, refreshPreview: true, refreshRenderTree: false); }

                float offSouthZ = animationOverride.offsetSouth.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffSouthZ".Translate(), ref offSouthZ, -2f, 2f, "F3");
                if (Math.Abs(offSouthZ - animationOverride.offsetSouth.z) > 0.0001f) { MutateWithUndo(() => animationOverride.offsetSouth.z = offSouthZ, refreshPreview: true, refreshRenderTree: false); }

                float offEastX = animationOverride.offsetEast.x;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffEastX".Translate(), ref offEastX, -2f, 2f, "F3");
                if (Math.Abs(offEastX - animationOverride.offsetEast.x) > 0.0001f) { MutateWithUndo(() => animationOverride.offsetEast.x = offEastX, refreshPreview: true, refreshRenderTree: false); }

                float offEastZ = animationOverride.offsetEast.z;
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_WeaponRender_OffEastZ".Translate(), ref offEastZ, -2f, 2f, "F3");
                if (Math.Abs(offEastZ - animationOverride.offsetEast.z) > 0.0001f) { MutateWithUndo(() => animationOverride.offsetEast.z = offEastZ, refreshPreview: true, refreshRenderTree: false); }
            }
        }
    }
}
