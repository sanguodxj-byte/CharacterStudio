using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Exporter;
using CharacterStudio.Items;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 导出模组对话框
    /// </summary>
    public class Dialog_ExportMod : Window
    {
        private PawnSkinDef skinDef;
        private List<ModularAbilityDef> abilities;
        private string modName = "";
        private string author = "";
        private string version = "1.0.0";
        private string description = "";
        private string outputPath = "";
        private string statusMessage = "";
        private bool isExporting = false;
        private CharacterDefinition characterDefinition = new CharacterDefinition();

        // 导出模式
        private ExportMode exportMode = ExportMode.CosmeticPack;

        // 化妆品选项
        private bool exportAsGene = true;
        private bool overlayMode = false;
        private string geneCategory = "Cosmetic";

        // 模块化开关
        private bool includeSkinDef = true;
        private bool includeGeneDef = true;
        private bool includePawnKind = false;
        private bool includeSummonItem = false;
        private bool includeAbilities = false;
        private bool includeRuntimeTriggers = false;
        private bool copyTextures = true;

        private List<string> skinDefXmlPaths = new List<string>();
        private List<string> pawnKindDefXmlPaths = new List<string>();
        private List<string> summonItemXmlPaths = new List<string>();
        private List<string> abilityXmlPaths = new List<string>();
        private SummonArrivalMode roleCardArrivalMode = SummonArrivalMode.DropPod;
        private SummonSpawnEventMode roleCardSpawnEvent = SummonSpawnEventMode.PositiveLetter;
        private SummonSpawnAnimationMode roleCardSpawnAnimation = SummonSpawnAnimationMode.ExplosionEffect;
        private float roleCardSpawnAnimationScale = 1f;
        private bool assetRightsConfirmed = false;
        private bool exportWarningAcknowledged = false;
        private float assetRightsConfirmStartTime = -1f;
        private const float ExportRightsConfirmWaitSeconds = 10f;

        private Vector2 scrollPos;
        private float scrollViewHeight = 800f;

        public override Vector2 InitialSize => new Vector2(580f, 680f);

        public Dialog_ExportMod(PawnSkinDef skin, List<ModularAbilityDef>? abilityList = null, CharacterDefinition? characterDefinition = null)
        {
            this.skinDef = skin;
            this.abilities = abilityList ?? new List<ModularAbilityDef>();
            this.characterDefinition = characterDefinition?.Clone() ?? new CharacterDefinition();
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;

            // 从皮肤定义初始化
            modName = skin.label ?? skin.defName ?? "My Character";
            author = skin.author ?? "";
            version = skin.version ?? "1.0.0";
            description = skin.description ?? "";

            // 默认输出到 RimWorld Mods 目录
            outputPath = GetDefaultOutputPath();
            this.characterDefinition.EnsureDefaults(
                skin.defName ?? "CS_Character",
                skin.targetRaces != null && skin.targetRaces.Count > 0 ? DefDatabase<ThingDef>.GetNamedSilentFail(skin.targetRaces[0]) : ThingDefOf.Human,
                skin.attributes);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Rect titleRect = UIHelper.DrawPanelShell(shellRect, "CS_Studio_Export_Title".Translate(), 0f);

            float y = titleRect.yMax + 8f;
            float labelWidth = 120f;
            float fieldWidth = inRect.width - labelWidth - 20;

            Rect scrollRect = new Rect(0, y, inRect.width, inRect.height - y - 80);
            Rect viewRect = new Rect(0, 0, scrollRect.width - 16, scrollViewHeight);

            UIHelper.DrawContentCard(scrollRect);

            Widgets.BeginScrollView(scrollRect.ContractedBy(2f), ref scrollPos, viewRect);

            float vy = 0;

            float width = viewRect.width;

            // 基本信息
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Section_Base".Translate());
            UIHelper.DrawPropertyField(ref vy, width, "CS_Studio_Export_ModName".Translate(), ref modName);
            UIHelper.DrawPropertyField(ref vy, width, "CS_Studio_Export_Author".Translate(), ref author);
            UIHelper.DrawPropertyField(ref vy, width, "CS_Studio_Export_Version".Translate(), ref version);

            Widgets.Label(new Rect(0, vy, labelWidth, 24), "CS_Studio_Export_Description".Translate());
            description = Widgets.TextArea(new Rect(labelWidth, vy, width - labelWidth, 50), description);
            vy += 55;

            // 导出模式
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_ExportMode".Translate());
            Rect radioRect1 = new Rect(0, vy, width / 2 - 5, 24);
            if (Widgets.RadioButtonLabeled(radioRect1, "CS_Studio_ExportMode_Cosmetic".Translate(), exportMode == ExportMode.CosmeticPack))
            {
                exportMode = ExportMode.CosmeticPack;
                ApplyModePreset();
            }

            Rect radioRect2 = new Rect(width / 2 + 5, vy, width / 2 - 5, 24);
            if (Widgets.RadioButtonLabeled(radioRect2, "CS_Studio_ExportMode_FullUnit".Translate(), exportMode == ExportMode.FullUnit))
            {
                exportMode = ExportMode.FullUnit;
                ApplyModePreset();
            }
            vy += UIHelper.RowHeight;

            Widgets.Label(new Rect(0, vy, width, 42f), "CS_Studio_Export_ModuleSummary".Translate(GetSelectedModulesSummary()));
            vy += 46f;

            // 模块化开关
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Export_ModuleOptions".Translate());
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeSkinDef".Translate(), ref includeSkinDef);
            if (includeSkinDef) DrawXmlSelectorRow(ref vy, width, "CS_Studio_Export_SkinDefXmlPath".Translate(), skinDefXmlPaths);

            if (exportMode == ExportMode.CosmeticPack)
            {
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_AsGene".Translate(), ref exportAsGene, "CS_Studio_Export_ModuleGeneHint".Translate());
                if (exportAsGene)
                {
                    UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeGeneDef".Translate(), ref includeGeneDef);
                    UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_OverlayMode".Translate(), ref overlayMode, "CS_Studio_Export_OverlayMode_Desc".Translate());
                    UIHelper.DrawPropertyDropdown(ref vy, width, "CS_Studio_Export_GeneCategory".Translate(), geneCategory,
                        new[] { "Cosmetic", "Miscellaneous", "Beauty", "Headbone" },
                        cat => cat,
                        val => geneCategory = val);
                }
            }
            else if (exportMode == ExportMode.FullUnit)
            {
                bool canExportAbilities = abilities.Count > 0;
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludePawnKind".Translate(), ref includePawnKind, "CS_Studio_Export_ModulePawnKindHint".Translate());
                if (includePawnKind) DrawXmlSelectorRow(ref vy, width, "CS_Studio_Export_PawnKindDefXmlPath".Translate(), pawnKindDefXmlPaths);

                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeSummonItem".Translate(), ref includeSummonItem, "CS_Studio_Export_ModuleSummonHint".Translate());
                if (includeSummonItem) DrawXmlSelectorRow(ref vy, width, "CS_Studio_Export_SummonItemXmlPath".Translate(), summonItemXmlPaths);
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeRuntimeTriggers".Translate(), ref includeRuntimeTriggers,
                    characterDefinition.runtimeTriggers != null && characterDefinition.runtimeTriggers.Count > 0
                        ? "CS_Studio_Export_IncludeRuntimeTriggers_Hint".Translate()
                        : "CS_Studio_Export_IncludeRuntimeTriggers_EmptyHint".Translate());

                if (includeRuntimeTriggers)
                {
                    Rect triggerBtnRect = new Rect(0, vy, width, 24f);
                    if (UIHelper.DrawToolbarButton(triggerBtnRect, "CS_Studio_RuntimeTriggers_Title".Translate()))
                    {
                        Find.WindowStack.Add(new Dialog_RuntimeTriggers(characterDefinition, () => { }));
                    }
                    vy += 30f;
                }

                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeAbilities".Translate(), ref includeAbilities,
                    canExportAbilities
                        ? "CS_Studio_Export_ModuleAbilitiesHint".Translate()
                        : "CS_Studio_Export_ModuleAbilitiesEmptyHint".Translate());
                if (includeAbilities) DrawXmlSelectorRow(ref vy, width, "CS_Studio_Export_AbilityXmlPath".Translate(), abilityXmlPaths);
            }

            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_CopyTextures".Translate(), ref copyTextures);
            NormalizeModuleSelectionForUi();

            // 导出前确认
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Export_ConfirmationSection".Translate());

            bool previousAssetRightsConfirmed = assetRightsConfirmed;
            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_ConfirmRights".Translate(), ref assetRightsConfirmed, "CS_Studio_Export_ConfirmRights_Desc".Translate());
            if (assetRightsConfirmed && !previousAssetRightsConfirmed)
            {
                assetRightsConfirmStartTime = Time.realtimeSinceStartup;
            }
            else if (!assetRightsConfirmed)
            {
                assetRightsConfirmStartTime = -1f;
                exportWarningAcknowledged = false;
            }

            UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_ConfirmRightsSecond".Translate(), ref exportWarningAcknowledged, "CS_Studio_Export_ConfirmRightsSecond_Desc".Translate());
            if (!assetRightsConfirmed && exportWarningAcknowledged)
            {
                exportWarningAcknowledged = false;
            }

            float remainingSeconds = GetRemainingConfirmationSeconds();
            Color previousColor = GUI.color;
            GUI.color = remainingSeconds > 0f ? new Color(1f, 0.85f, 0.3f) : Color.green;
            Widgets.Label(new Rect(0, vy, width, 36f), remainingSeconds > 0f
                ? "CS_Studio_Export_ConfirmCountdown".Translate(Mathf.CeilToInt(remainingSeconds))
                : "CS_Studio_Export_ConfirmCountdownReady".Translate());
            GUI.color = previousColor;
            vy += 40f;

            // 输出设置
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Export_OutputSettings".Translate());
            UIHelper.DrawPropertyFieldWithButton(ref vy, width, "CS_Studio_Export_OutputPath".Translate(),
                outputPath, OnBrowseOutputPath, "CS_Studio_Export_Browse".Translate());

            if (Event.current.type == EventType.Layout)
            {
                scrollViewHeight = vy;
            }
            Widgets.EndScrollView();

            // 状态消息
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUI.color = statusMessage.Contains("失败") || statusMessage.Contains("Failed") ? Color.red : Color.green;
                Widgets.Label(new Rect(0, y, inRect.width, 40), statusMessage);
                GUI.color = Color.white;
            }

            // 底部按钮
            float btnWidth = 120f;
            float btnY = inRect.height - 40;

            GUI.enabled = !isExporting && IsValidInput() && CanExportNow();
            if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2 - btnWidth - 10, btnY, btnWidth, 30), "CS_Studio_Export_Confirm".Translate(), accent: true))
            {
                OnExport();
            }
            GUI.enabled = true;

            if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2 + 10, btnY, btnWidth, 30), "CS_Studio_Export_Cancel".Translate()))
            {
                Close();
            }
        }

        private bool IsValidInput()
        {
            return !string.IsNullOrEmpty(modName) &&
                   !string.IsNullOrEmpty(outputPath) &&
                   Directory.Exists(outputPath);
        }

        private string GetDefaultOutputPath()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio");
        }

        private void DrawXmlSelectorRow(ref float y, float width, string label, List<string> pathList)
        {
            string displayStatus = pathList.Count > 0 ? "CS_Studio_Export_ExternalFilesSelected".Translate(pathList.Count) : "CS_Studio_Export_DefaultGenerated".Translate();
            UIHelper.DrawPropertyFieldWithButton(ref y, width, label, displayStatus, () => 
            {
                Find.WindowStack.Add(new Dialog_FileBrowser("", paths => 
                {
                    pathList.Clear();
                    pathList.AddRange(paths);
                }, "*.xml"));
            }, "...");
        }

        private void OnBrowseOutputPath()
        {
            // RimWorld 没有内置的文件夹选择对话框
            // 这里只能显示提示信息
            Find.WindowStack.Add(new Dialog_MessageBox(
                "CS_Studio_Export_PathHint".Translate(GenFilePaths.ModsFolderPath),
                "CS_Studio_Btn_OK".Translate()
            ));
        }

        private List<string> BuildSourceTextureSearchPaths()
        {
            return ExportAssetUtility.BuildSourceTextureSearchPaths(skinDef, abilities);
        }

        private float GetRemainingConfirmationSeconds()
        {
            if (!assetRightsConfirmed || assetRightsConfirmStartTime < 0f)
            {
                return ExportRightsConfirmWaitSeconds;
            }

            return Mathf.Max(0f, ExportRightsConfirmWaitSeconds - (Time.realtimeSinceStartup - assetRightsConfirmStartTime));
        }

        private bool CanExportNow()
        {
            return assetRightsConfirmed
                && exportWarningAcknowledged
                && GetRemainingConfirmationSeconds() <= 0f;
        }

        private string GetSelectedModulesSummary()
        {
            var list = new List<string>();
            if (includeSkinDef) list.Add("CS_Studio_Export_IncludeSkinDef".Translate());
            if (exportMode == ExportMode.CosmeticPack && exportAsGene) list.Add("CS_Studio_Export_AsGene".Translate());
            if (exportMode == ExportMode.FullUnit)
            {
                if (includePawnKind) list.Add("CS_Studio_Export_IncludePawnKind".Translate());
                if (includeSummonItem) list.Add("CS_Studio_Export_IncludeSummonItem".Translate());
                if (includeRuntimeTriggers) list.Add("CS_Studio_Export_IncludeRuntimeTriggers".Translate());
                if (includeAbilities) list.Add("CS_Studio_Export_IncludeAbilities".Translate());
            }
            if (copyTextures) list.Add("CS_Studio_Export_CopyTextures".Translate());

            return list.Count > 0 ? string.Join(" / ", list) : "CS_Studio_None".Translate().ToString();
        }

        private void ApplyModePreset()
        {
            switch (exportMode)
            {
                case ExportMode.CosmeticPack:
                    includeSkinDef = true;
                    exportAsGene = true;
                    includeGeneDef = true;
                    includePawnKind = false;
                    includeSummonItem = false;
                    includeAbilities = false;
                    copyTextures = true;
                    break;
                case ExportMode.FullUnit:
                    includeSkinDef = true;
                    includePawnKind = true;
                    includeSummonItem = true;
                    includeAbilities = abilities.Count > 0;
                    copyTextures = true;
                    break;
            }

            NormalizeModuleSelectionForUi();
        }

        private void NormalizeModuleSelectionForUi()
        {
            if (exportMode == ExportMode.CosmeticPack)
            {
                includePawnKind = false;
                includeSummonItem = false;
                includeAbilities = false;
                includeRuntimeTriggers = false;
                includeGeneDef = exportAsGene && includeGeneDef;
                if (includeGeneDef)
                {
                    includeSkinDef = true;
                }
            }
            else if (exportMode == ExportMode.FullUnit)
            {
                if (includeSummonItem)
                {
                    includePawnKind = true;
                }

                if (includeRuntimeTriggers)
                {
                    includePawnKind = true;
                    includeSkinDef = true;
                }
            }
        }

        private void OnExport()
        {
            if (!IsValidInput())
            {
                statusMessage = "CS_Studio_Export_Err_FillFields".Translate();
                return;
            }

            if (!assetRightsConfirmed)
            {
                statusMessage = "CS_Studio_Export_Err_MustConfirmRights".Translate();
                return;
            }

            if (!exportWarningAcknowledged)
            {
                statusMessage = "CS_Studio_Export_Err_MustConfirmRightsSecond".Translate();
                return;
            }

            if (GetRemainingConfirmationSeconds() > 0f)
            {
                statusMessage = "CS_Studio_Export_Err_WaitCountdown".Translate(Mathf.CeilToInt(GetRemainingConfirmationSeconds()));
                return;
            }

            NormalizeModuleSelectionForUi();

            isExporting = true;
            statusMessage = "CS_Studio_Export_Status_Exporting".Translate();

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "CS_Studio_Export_FinalConfirmMessage".Translate(),
                () =>
                {
                    isExporting = true;
                    statusMessage = "CS_Studio_Export_Status_Exporting".Translate();

                    try
                    {
                        var config = new ModExportConfig
                        {
                            ModName = modName,
                            Author = author,
                            Version = version,
                            Description = description,
                            OutputPath = outputPath,
                            SkinDef = skinDef,
                            Abilities = abilities,
                            SourceTexturePaths = BuildSourceTextureSearchPaths(),
                            Mode = exportMode,
                            ExportAsGene = exportAsGene,
                            GeneCategory = geneCategory,
                            OverlayMode = overlayMode,
                            IncludeSkinDef = includeSkinDef,
                            IncludeGeneDef = includeGeneDef,
                            IncludePawnKind = includePawnKind,
                            IncludeSummonItem = includeSummonItem,
                            IncludeAbilities = includeAbilities,
                            CopyTextures = copyTextures,

                            SkinDefXmlPaths = skinDefXmlPaths,
                            PawnKindDefXmlPaths = pawnKindDefXmlPaths,
                            SummonItemXmlPaths = summonItemXmlPaths,
                            AbilityXmlPaths = abilityXmlPaths,

                            RoleCardArrivalMode = roleCardArrivalMode,
                            RoleCardSpawnEvent = roleCardSpawnEvent,
                            RoleCardSpawnAnimation = roleCardSpawnAnimation,
                            RoleCardSpawnAnimationScale = roleCardSpawnAnimationScale,
                            CharacterDefinition = characterDefinition.Clone(),
                            IncludeRuntimeTriggers = includeRuntimeTriggers,
                            AssetRightsConfirmed = assetRightsConfirmed
                        };

                        string exportedModPath = new ModBuilder().Export(config);

                        statusMessage = "CS_Studio_Export_Success".Translate(exportedModPath);
                        Messages.Message("CS_Studio_Export_Success".Translate(exportedModPath), MessageTypeDefOf.PositiveEvent);
                        isExporting = false;
                        Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[CharacterStudio] Export failed: {ex}");
                        statusMessage = "CS_Studio_Export_Failed".Translate(ex.Message);
                        isExporting = false;
                    }
                },
                true));
        }
    }
}
