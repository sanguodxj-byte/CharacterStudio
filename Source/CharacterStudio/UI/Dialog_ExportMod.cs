using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Exporter;
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
        private bool copyTextures = true;
        
        // 资产来源预览
        private List<AssetSourceInfo> assetSources = new List<AssetSourceInfo>();
        private List<string> detectedDependencies = new List<string>();
        private bool showAssetPreview = false;
        
        private Vector2 scrollPos;
        private Vector2 assetScrollPos;

        public override Vector2 InitialSize => new Vector2(580f, 680f);

        public Dialog_ExportMod(PawnSkinDef skin, List<ModularAbilityDef>? abilityList = null)
        {
            this.skinDef = skin;
            this.abilities = abilityList ?? new List<ModularAbilityDef>();
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
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30), "CS_Studio_Export_Title".Translate());
            Text.Font = GameFont.Small;

            float y = 40;
            float labelWidth = 120f;
            float fieldWidth = inRect.width - labelWidth - 20;

            // 滚动区域
            Rect scrollRect = new Rect(0, y, inRect.width, inRect.height - y - 80);
            Rect viewRect = new Rect(0, 0, scrollRect.width - 16, showAssetPreview ? 700 : 550);

            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

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
                exportMode = ExportMode.CosmeticPack;
            
            Rect radioRect2 = new Rect(width / 2 + 5, vy, width / 2 - 5, 24);
            if (Widgets.RadioButtonLabeled(radioRect2, "CS_Studio_ExportMode_FullUnit".Translate(), exportMode == ExportMode.FullUnit))
                exportMode = ExportMode.FullUnit;
            vy += UIHelper.RowHeight;

            // 模块化开关
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Export_ModuleOptions".Translate());
            
            if (exportMode == ExportMode.CosmeticPack)
            {
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeSkinDef".Translate(), ref includeSkinDef);
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_AsGene".Translate(), ref exportAsGene);
                if (exportAsGene)
                {
                    UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeGeneDef".Translate(), ref includeGeneDef);
                    UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_OverlayMode".Translate(), ref overlayMode, "CS_Studio_Export_OverlayMode_Desc".Translate());
                    UIHelper.DrawPropertyDropdown(ref vy, width, "CS_Studio_Export_GeneCategory".Translate(), geneCategory,
                        new[] { "Cosmetic", "Miscellaneous", "Beauty", "Headbone" },
                        cat => cat,
                        val => geneCategory = val);
                }
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_CopyTextures".Translate(), ref copyTextures);
            }
            else if (exportMode == ExportMode.FullUnit)
            {
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeSkinDef".Translate(), ref includeSkinDef);
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludePawnKind".Translate(), ref includePawnKind);
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeSummonItem".Translate(), ref includeSummonItem);
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_IncludeAbilities".Translate(), ref includeAbilities);
                UIHelper.DrawPropertyCheckbox(ref vy, width, "CS_Studio_Export_CopyTextures".Translate(), ref copyTextures);
            }
            
            // 资产来源预览
            vy += 10;
            if (Widgets.ButtonText(new Rect(0, vy, 200, 24), showAssetPreview ? "CS_Studio_Export_HideAssets".Translate() : "CS_Studio_Export_PreviewAssets".Translate()))
            {
                showAssetPreview = !showAssetPreview;
                if (showAssetPreview)
                {
                    AnalyzeAssets();
                }
            }
            vy += 30;
            
            if (showAssetPreview)
            {
                DrawAssetPreview(ref vy, width);
            }

            // 输出设置
            UIHelper.DrawSectionTitle(ref vy, width, "CS_Studio_Export_OutputSettings".Translate());
            UIHelper.DrawPropertyFieldWithButton(ref vy, width, "CS_Studio_Export_OutputPath".Translate(),
                outputPath, OnBrowseOutputPath, "CS_Studio_Export_Browse".Translate());

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

            GUI.enabled = !isExporting && IsValidInput();
            if (Widgets.ButtonText(new Rect(inRect.width / 2 - btnWidth - 10, btnY, btnWidth, 30), "CS_Studio_Export_Confirm".Translate()))
            {
                OnExport();
            }
            GUI.enabled = true;

            if (Widgets.ButtonText(new Rect(inRect.width / 2 + 10, btnY, btnWidth, 30), "CS_Studio_Export_Cancel".Translate()))
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
            // 尝试获取 RimWorld Mods 目录
            try
            {
                string rimworldPath = GenFilePaths.ModsFolderPath;
                if (Directory.Exists(rimworldPath))
                {
                    return rimworldPath;
                }
            }
            catch { }

            // 回退到桌面
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
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

        /// <summary>
        /// 分析资产来源
        /// </summary>
        private void AnalyzeAssets()
        {
            assetSources.Clear();
            detectedDependencies.Clear();

            if (skinDef?.layers == null) return;

            var builder = new ModBuilder();
            foreach (var layer in skinDef.layers)
            {
                if (string.IsNullOrEmpty(layer.texPath)) continue;

                // 使用反射调用私有方法，或者直接内联检测逻辑
                var sourceInfo = DetectAssetSourceSimple(layer.texPath);
                assetSources.Add(sourceInfo);

                if (sourceInfo.SourceType == AssetSourceType.ExternalMod &&
                    !string.IsNullOrEmpty(sourceInfo.SourceModPackageId) &&
                    !detectedDependencies.Contains(sourceInfo.SourceModPackageId))
                {
                    detectedDependencies.Add(sourceInfo.SourceModPackageId);
                }
            }
        }

        /// <summary>
        /// 简化的资产来源检测
        /// </summary>
        private AssetSourceInfo DetectAssetSourceSimple(string texPath)
        {
            var info = new AssetSourceInfo { OriginalPath = texPath };

            // 检查是否是绝对路径
            if (texPath.Contains(":") || texPath.StartsWith("/"))
            {
                info.SourceType = AssetSourceType.LocalFile;
                info.ResolvedPath = texPath;
                return info;
            }

            // 检查是否存在于游戏内容
            if (ContentFinder<UnityEngine.Texture2D>.Get(texPath, false) != null)
            {
                // 尝试确定来源模组
                foreach (var mod in LoadedModManager.RunningMods)
                {
                    string modTexPath = Path.Combine(mod.RootDir, "Textures", texPath.Replace('/', Path.DirectorySeparatorChar));
                    string[] extensions = { ".png", ".PNG", ".jpg", ".JPG" };

                    foreach (var ext in extensions)
                    {
                        if (File.Exists(modTexPath + ext))
                        {
                            if (mod.PackageId.StartsWith("ludeon."))
                            {
                                info.SourceType = AssetSourceType.VanillaContent;
                            }
                            else
                            {
                                info.SourceType = AssetSourceType.ExternalMod;
                                info.SourceModPackageId = mod.PackageId;
                                info.SourceModName = mod.Name;
                            }
                            info.ResolvedPath = texPath;
                            return info;
                        }
                    }
                }

                info.SourceType = AssetSourceType.VanillaContent;
                info.ResolvedPath = texPath;
            }
            else
            {
                info.SourceType = AssetSourceType.LocalFile;
                info.ResolvedPath = texPath;
            }

            return info;
        }

        /// <summary>
        /// 绘制资产预览区域
        /// </summary>
        private void DrawAssetPreview(ref float y, float width)
        {
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Export_AssetSources".Translate());

            // 统计信息
            int localCount = assetSources.Count(s => s.SourceType == AssetSourceType.LocalFile);
            int vanillaCount = assetSources.Count(s => s.SourceType == AssetSourceType.VanillaContent);
            int externalCount = assetSources.Count(s => s.SourceType == AssetSourceType.ExternalMod);

            Widgets.Label(new Rect(0, y, width, 20),
                $"{"CS_Studio_Export_LocalFiles".Translate()}: {localCount}  |  " +
                $"{"CS_Studio_Export_VanillaContent".Translate()}: {vanillaCount}  |  " +
                $"{"CS_Studio_Export_ExternalMod".Translate()}: {externalCount}");
            y += 24;

            // 依赖列表
            if (detectedDependencies.Count > 0)
            {
                GUI.color = new Color(1f, 0.8f, 0.3f);
                Widgets.Label(new Rect(0, y, width, 20), "CS_Studio_Export_DetectedDeps".Translate(detectedDependencies.Count));
                GUI.color = Color.white;
                y += 22;

                foreach (var dep in detectedDependencies)
                {
                    var sourceInfo = assetSources.FirstOrDefault(s => s.SourceModPackageId == dep);
                    string displayName = sourceInfo?.SourceModName ?? dep;
                    Widgets.Label(new Rect(20, y, width - 20, 18), $"• {displayName}");
                    y += 20;
                }
            }

            // 资产列表滚动区域
            y += 5;
            Rect assetListRect = new Rect(0, y, width, 100);
            Rect assetViewRect = new Rect(0, 0, width - 16, assetSources.Count * 20);
            
            Widgets.BeginScrollView(assetListRect, ref assetScrollPos, assetViewRect);
            float ay = 0;
            foreach (var source in assetSources)
            {
                Color color = source.SourceType switch
                {
                    AssetSourceType.LocalFile => Color.white,
                    AssetSourceType.VanillaContent => Color.green,
                    AssetSourceType.ExternalMod => new Color(1f, 0.8f, 0.3f),
                    _ => Color.gray
                };
                GUI.color = color;
                
                string typeLabel = source.SourceType switch
                {
                    AssetSourceType.LocalFile => "[Local]",
                    AssetSourceType.VanillaContent => "[Vanilla]",
                    AssetSourceType.ExternalMod => $"[{source.SourceModName ?? "Mod"}]",
                    _ => "[?]"
                };
                
                Widgets.Label(new Rect(0, ay, width - 16, 18), $"{typeLabel} {source.OriginalPath}");
                GUI.color = Color.white;
                ay += 20;
            }
            Widgets.EndScrollView();
            
            y += 105;
        }

        private void OnExport()
        {
            if (!IsValidInput())
            {
                statusMessage = "CS_Studio_Export_Err_FillFields".Translate();
                return;
            }

            isExporting = true;
            statusMessage = "CS_Studio_Export_Status_Exporting".Translate();

            try
            {
                // 创建导出配置
                var config = new ModExportConfig
                {
                    ModName = modName,
                    Author = author,
                    Version = version,
                    Description = description,
                    OutputPath = outputPath,
                    SkinDef = skinDef,
                    Abilities = abilities,
                    Mode = exportMode,
                    ExportAsGene = exportAsGene,
                    GeneCategory = geneCategory,
                    OverlayMode = overlayMode,
                    // 模块化开关
                    IncludeSkinDef = includeSkinDef,
                    IncludeGeneDef = includeGeneDef,
                    IncludePawnKind = includePawnKind,
                    IncludeSummonItem = includeSummonItem,
                    IncludeAbilities = includeAbilities,
                    CopyTextures = copyTextures
                };

                // 调用导出逻辑
                new ModBuilder().Export(config);

                statusMessage = "CS_Studio_Export_Success".Translate();
                Messages.Message("CS_Studio_Export_Success".Translate(), MessageTypeDefOf.PositiveEvent);
                isExporting = false;
                Close();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] Export failed: {ex}");
                statusMessage = "CS_Studio_Export_Failed".Translate(ex.Message);
                isExporting = false;
            }
        }
    }
}
