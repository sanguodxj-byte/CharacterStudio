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

        private void DrawAnimationPanel(Rect rect)
        {
            Rect titleRect = UIHelper.DrawPanelShell(rect, "CS_Studio_Tab_Animation".Translate(), Margin);

            float btnY = titleRect.yMax + 6f;
            float btnCount = 3f;
            float btnWidth = (rect.width - Margin * (btnCount + 1f)) / btnCount;
            float btnHeight = Mathf.Max(ButtonHeight - 2f, 22f);

            bool DrawIconButton(Rect buttonRect, string label, string tooltip, Action action, bool accent = false)
            {
                Widgets.DrawBoxSolid(buttonRect, accent ? UIHelper.ActiveTabColor : UIHelper.PanelFillSoftColor);
                Widgets.DrawBoxSolid(
                    new Rect(buttonRect.x, buttonRect.yMax - 2f, buttonRect.width, 2f),
                    accent ? UIHelper.AccentColor : new Color(1f, 1f, 1f, 0.05f));
                GUI.color = Mouse.IsOver(buttonRect) ? UIHelper.HoverOutlineColor : UIHelper.BorderColor;
                Widgets.DrawBox(buttonRect, 1);
                GUI.color = Color.white;

                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = accent ? Color.white : UIHelper.HeaderColor;
                Widgets.Label(buttonRect, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = prevFont;

                TooltipHandler.TipRegion(buttonRect, tooltip);
                if (Widgets.ButtonInvisible(buttonRect))
                {
                    action();
                    return true;
                }

                return false;
            }

            float startX = rect.x + Margin;
            DrawIconButton(new Rect(startX + (btnWidth + Margin) * 0f, btnY, btnWidth, btnHeight), "↓", "CS_Studio_Section_Animation".Translate(), OpenAnimationImportXmlDialog);
            DrawIconButton(new Rect(startX + (btnWidth + Margin) * 1f, btnY, btnWidth, btnHeight), "↑", "CS_Studio_Equip_Btn_ExportXml".Translate(), ExportAnimationConfigToDefaultPath);
            DrawIconButton(new Rect(startX + (btnWidth + Margin) * 2f, btnY, btnWidth, btnHeight), "↺", "CS_Studio_Btn_Reset".Translate(), ResetAnimationConfig);

            float contentY = btnY + btnHeight + 8f;
            float contentHeight = rect.height - contentY + rect.y - Margin;
            Rect contentRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, contentHeight);
            UIHelper.DrawContentCard(contentRect);

            Rect viewRect = new Rect(0, 0, contentRect.width - 20f, Mathf.Max(contentRect.height + 320f, 980f));

            Widgets.BeginScrollView(contentRect.ContractedBy(2f), ref propsScrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;

            workingSkin.animationConfig ??= new PawnAnimationConfig();
            workingSkin.animationConfig.carryVisual ??= new WeaponCarryVisualConfig();
            var carry = workingSkin.animationConfig.carryVisual;
            var animationOverride = workingSkin.animationConfig;

            WeaponCarryVisualConfig carrySnapshot = carry.Clone();
            PawnAnimationConfig animationOverrideSnapshot = animationOverride.Clone();

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_AnimationCarryVisual".Translate(), "AnimationCarryVisual"))
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

                if (UIHelper.DrawToolbarButton(new Rect(0f, y, 140f, 24f), "CS_Studio_WeaponCarry_Reset".Translate()))
                {
                    MutateWithUndo(() => animationOverride.carryVisual = new WeaponCarryVisualConfig { enabled = true, anchorTag = carry.anchorTag }, refreshPreview: true, refreshRenderTree: targetPawn != null);
                }
                y += 32f;
            }

            if (!AreCarryVisualConfigsEqual(carrySnapshot, carry))
                FinalizeMutatedEditorState(refreshPreview: true, refreshRenderTree: targetPawn != null);

            if (DrawCollapsibleSection(ref y, width, "CS_Studio_Section_AnimationRender".Translate(), "AnimationRenderOverride"))
            {
                Rect renderSummaryRect = new Rect(0f, y, width, 40f);
                UIHelper.DrawContentCard(renderSummaryRect);
                Text.Font = GameFont.Tiny;
                GUI.color = UIHelper.SubtleColor;
                Widgets.Label(new Rect(8f, y + 4f, width - 16f, 16f),
                    "CS_Studio_Section_AnimationCarryVisual".Translate() + ": " + (string.IsNullOrWhiteSpace(carry.texUndrafted) ? 0 : 1) + (string.IsNullOrWhiteSpace(carry.texDrafted) ? 0 : 1) + (string.IsNullOrWhiteSpace(carry.texCasting) ? 0 : 1) + "/3");
                Widgets.Label(new Rect(8f, y + 20f, width - 16f, 16f),
                    "CS_Studio_WeaponRender_Enable".Translate() + ": " + (animationOverride.weaponOverrideEnabled ? "ON" : "OFF"));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 46f;

                bool overrideEnabled = animationOverride.weaponOverrideEnabled;
                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_WeaponRender_Enable".Translate(), ref overrideEnabled);
                if (overrideEnabled != animationOverride.weaponOverrideEnabled)
                {
                    MutateWithUndo(() => animationOverride.weaponOverrideEnabled = overrideEnabled, refreshPreview: true, refreshRenderTree: false);
                }

                UIHelper.DrawPropertyCheckbox(ref y, width, "CS_Studio_WeaponRender_ApplyOffHand".Translate(), ref animationOverride.applyToOffHand);

                if (UIHelper.DrawToolbarButton(new Rect(0f, y, 140f, 24f), "CS_Studio_Btn_Reset".Translate()))
                {
                    MutateWithUndo(() =>
                    {
                        var carrySaved = animationOverride.carryVisual;
                        animationOverride = new PawnAnimationConfig { carryVisual = carrySaved };
                        workingSkin.animationConfig = animationOverride;
                    }, refreshPreview: true, refreshRenderTree: targetPawn != null);
                }
                y += 32f;
            }

            if (!AreAnimationConfigsEqual(animationOverrideSnapshot, animationOverride))
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

        private static bool AreAnimationConfigsEqual(PawnAnimationConfig a, PawnAnimationConfig b)
        {
            if (a.weaponOverrideEnabled != b.weaponOverrideEnabled ||
                a.applyToOffHand != b.applyToOffHand ||
                a.offset != b.offset ||
                a.offsetSouth != b.offsetSouth ||
                a.offsetNorth != b.offsetNorth ||
                a.offsetEast != b.offsetEast ||
                a.scale != b.scale) return false;

            var ap = a.procedural;
            var bp = b.procedural;
            if (ap == null || bp == null) return ap == bp;

            return ap.breathingEnabled == bp.breathingEnabled &&
                   Math.Abs(ap.breathingSpeed - bp.breathingSpeed) < 0.001f &&
                   Math.Abs(ap.breathingAmplitude - bp.breathingAmplitude) < 0.0001f &&
                   ap.hoveringEnabled == bp.hoveringEnabled &&
                   Math.Abs(ap.hoveringSpeed - bp.hoveringSpeed) < 0.001f &&
                   Math.Abs(ap.hoveringAmplitude - bp.hoveringAmplitude) < 0.0001f;
        }

        private static string GetAnimationConfigExportDir()
        {
            return System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "AnimationConfigs");
        }

        private static string GetDefaultAnimationConfigExportFilePath()
        {
            return System.IO.Path.Combine(GetAnimationConfigExportDir(), "PawnAnimationConfig.xml");
        }

        private static string GetAnimationImportBrowseStartPath(string initialPath)
        {
            if (string.IsNullOrWhiteSpace(initialPath))
            {
                return GetAnimationConfigExportDir();
            }

            string normalizedPath = initialPath.Trim().Trim('"');
            if (System.IO.Directory.Exists(normalizedPath))
            {
                return normalizedPath;
            }

            if (System.IO.File.Exists(normalizedPath))
            {
                string directory = System.IO.Path.GetDirectoryName(normalizedPath);
                if (!string.IsNullOrWhiteSpace(directory) && System.IO.Directory.Exists(directory))
                {
                    return directory;
                }
            }

            return GetAnimationConfigExportDir();
        }

        private void OpenAnimationImportXmlDialog()
        {
            string initialPath = GetDefaultAnimationConfigExportFilePath();
            Find.WindowStack.Add(new Dialog_FileBrowser(GetAnimationImportBrowseStartPath(initialPath), selectedPath =>
            {
                string normalizedPath = selectedPath?.Trim().Trim('"') ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedPath) || !System.IO.File.Exists(normalizedPath)) return;
                
                try
                {
                    var xml = new System.Xml.XmlDocument();
                    xml.Load(normalizedPath);
                    
                    var imported = DirectXmlToObject.ObjectFromXml<PawnAnimationConfig>(xml.DocumentElement, true);
                    if (imported != null)
                    {
                        imported.carryVisual ??= new WeaponCarryVisualConfig();
                        MutateWithUndo(() =>
                        {
                            workingSkin.animationConfig = imported;
                        }, refreshPreview: true, refreshRenderTree: true);
                        ShowStatus("CS_Studio_Equip_ImportAppended".Translate(1, System.IO.Path.GetFileName(normalizedPath)));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] 动画配置 XML 导入失败: {ex}");
                    ShowStatus("CS_Studio_Equip_ImportFailed".Translate(ex.Message));
                }
            }, "*.xml"));
        }

        private void ExportAnimationConfigToDefaultPath()
        {
            try
            {
                string exportDir = GetAnimationConfigExportDir();
                System.IO.Directory.CreateDirectory(exportDir);
                string exportPath = GetDefaultAnimationConfigExportFilePath();
                
                workingSkin.animationConfig ??= new PawnAnimationConfig();
                
                string xml = DirectXmlSaver.XElementFromObject(workingSkin.animationConfig, typeof(PawnAnimationConfig)).ToString();
                System.Xml.Linq.XDocument doc = new System.Xml.Linq.XDocument(System.Xml.Linq.XElement.Parse(xml));
                doc.Save(exportPath);
                
                ShowStatus("CS_Studio_Equip_Exported".Translate(exportPath));
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 动画配置 XML 导出失败: {ex}");
                ShowStatus("CS_Studio_Equip_ExportFailed".Translate(ex.Message));
            }
        }

        private void ResetAnimationConfig()
        {
            MutateWithUndo(() =>
            {
                workingSkin.animationConfig = new PawnAnimationConfig();
            }, refreshPreview: true, refreshRenderTree: targetPawn != null);
            ShowStatus("CS_Studio_Btn_Reset".Translate());
        }
    }
}