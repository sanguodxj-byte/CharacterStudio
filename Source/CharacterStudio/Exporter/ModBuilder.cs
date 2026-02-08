using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 导出模式
    /// </summary>
    public enum ExportMode
    {
        /// <summary>完整角色包（PawnKind + Abilities + AI + Item）</summary>
        FullUnit,
        /// <summary>化妆品包（仅皮肤 + 纹理）</summary>
        CosmeticPack,
        /// <summary>插件模式（仅皮肤定义，不含纹理复制）</summary>
        PluginOnly
    }

    /// <summary>
    /// 资产来源类型
    /// </summary>
    public enum AssetSourceType
    {
        /// <summary>本地文件（外部路径，需要复制）</summary>
        LocalFile,
        /// <summary>游戏内置资源（直接引用）</summary>
        VanillaContent,
        /// <summary>外部模组资源（添加依赖，不复制）</summary>
        ExternalMod
    }

    /// <summary>
    /// 资产来源信息
    /// </summary>
    public class AssetSourceInfo
    {
        public string OriginalPath { get; set; } = "";
        public AssetSourceType SourceType { get; set; }
        public string? SourceModPackageId { get; set; }
        public string? SourceModName { get; set; }
        public string ResolvedPath { get; set; } = "";
    }

    /// <summary>
    /// 导出配置
    /// </summary>
    public class ModExportConfig
    {
        public string ModName { get; set; } = "";
        public string Author { get; set; } = "";
        public string Version { get; set; } = "1.0.0";
        public string Description { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public PawnSkinDef? SkinDef { get; set; }
        public List<ModularAbilityDef> Abilities { get; set; } = new List<ModularAbilityDef>();
        public List<string> SourceTexturePaths { get; set; } = new List<string>();

        // 导出模式
        public ExportMode Mode { get; set; } = ExportMode.CosmeticPack;

        // 化妆品导出选项
        public bool ExportAsGene { get; set; } = true;
        public bool ExportAsTattoo { get; set; } = false;
        public bool OverlayMode { get; set; } = false;
        public string GeneCategory { get; set; } = "Cosmetic";
        public string GeneIconPath { get; set; } = "";

        // 模块化开关
        public bool IncludeSkinDef { get; set; } = true;
        public bool IncludeGeneDef { get; set; } = true;
        public bool IncludePawnKind { get; set; } = false;
        public bool IncludeSummonItem { get; set; } = false;
        public bool IncludeAbilities { get; set; } = false;
        public bool CopyTextures { get; set; } = true;

        // 资产来源检测结果
        public List<AssetSourceInfo> AssetSources { get; set; } = new List<AssetSourceInfo>();
        public List<string> DetectedDependencies { get; set; } = new List<string>();
    }

    /// <summary>
    /// 模组构建器
    /// 将内存中的皮肤定义导出为可加载的 RimWorld 模组
    /// </summary>
    public class ModBuilder
    {
        // ─────────────────────────────────────────────
        // 导出方法
        // ─────────────────────────────────────────────

        /// <summary>
        /// Character Studio 版本号
        /// </summary>
        private const string CS_VERSION = "1.0.0";

        /// <summary>
        /// 执行导出
        /// </summary>
        /// <returns>导出的模组路径</returns>
        public string Export(ModExportConfig config)
        {
            if (config == null || config.SkinDef == null)
            {
                throw new ArgumentNullException(nameof(config), "导出配置或皮肤定义为空");
            }

            string safeName = SanitizeFileName(config.ModName);
            string modPath = Path.Combine(config.OutputPath, safeName);

            // 克隆皮肤定义，避免污染源对象
            var exportSkinDef = config.SkinDef.Clone();
            var exportConfig = new ModExportConfig
            {
                ModName = config.ModName,
                Author = config.Author,
                Version = config.Version,
                Description = config.Description,
                OutputPath = config.OutputPath,
                SkinDef = exportSkinDef,
                Abilities = config.Abilities,
                SourceTexturePaths = config.SourceTexturePaths,
                Mode = config.Mode,
                ExportAsGene = config.ExportAsGene,
                ExportAsTattoo = config.ExportAsTattoo,
                OverlayMode = config.OverlayMode,
                GeneCategory = config.GeneCategory,
                GeneIconPath = config.GeneIconPath,
                IncludeSkinDef = config.IncludeSkinDef,
                IncludeGeneDef = config.IncludeGeneDef,
                IncludePawnKind = config.IncludePawnKind,
                IncludeSummonItem = config.IncludeSummonItem,
                IncludeAbilities = config.IncludeAbilities,
                CopyTextures = config.CopyTextures
            };

            // 根据导出模式设置模块化开关
            ApplyExportModeDefaults(exportConfig);

            try
            {
                // 创建目录结构
                CreateDirectoryStructure(modPath);

                // 分析资产来源
                AnalyzeAssetSources(exportConfig);

                // 生成 About.xml（包含依赖检测）
                GenerateAboutXml(modPath, exportConfig);

                // 复制纹理文件（仅复制本地文件）
                if (exportConfig.CopyTextures)
                {
                    CopyTextures(modPath, exportConfig);
                }

                // 生成皮肤定义 XML
                if (exportConfig.IncludeSkinDef)
                {
                    GenerateSkinDefXml(modPath, exportConfig);
                }

                // 生成基因定义 XML
                if (exportConfig.IncludeGeneDef && exportConfig.ExportAsGene)
                {
                    GenerateGeneDefXml(modPath, exportConfig);
                }

                // 生成 PawnKind 定义
                if (exportConfig.IncludePawnKind)
                {
                    GenerateUnitDefXml(modPath, exportConfig);
                }

                // 生成技能定义 XML
                if (exportConfig.IncludeAbilities && exportConfig.Abilities != null && exportConfig.Abilities.Count > 0)
                {
                    GenerateAbilityDefXml(modPath, exportConfig);
                }

                // 生成 Manifest.xml（CS 版本信息）
                GenerateManifestXml(modPath, exportConfig);

                Log.Message($"[CharacterStudio] 模组导出成功: {modPath}");
                return modPath;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 导出失败: {ex}");
                
                // 尝试清理未完成的目录
                try
                {
                    if (Directory.Exists(modPath))
                    {
                        Directory.Delete(modPath, true);
                        Log.Message($"[CharacterStudio] 已清理未完成的导出目录: {modPath}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    Log.Warning($"[CharacterStudio] 清理目录失败: {cleanupEx.Message}");
                }

                throw;
            }
        }

        /// <summary>
        /// 根据导出模式设置默认的模块化开关
        /// </summary>
        private void ApplyExportModeDefaults(ModExportConfig config)
        {
            switch (config.Mode)
            {
                case ExportMode.CosmeticPack:
                    config.IncludeSkinDef = true;
                    config.IncludeGeneDef = config.ExportAsGene;
                    config.IncludePawnKind = false;
                    config.IncludeSummonItem = false;
                    config.IncludeAbilities = false;
                    config.CopyTextures = true;
                    break;

                case ExportMode.FullUnit:
                    config.IncludeSkinDef = true;
                    config.IncludeGeneDef = config.ExportAsGene;
                    config.IncludePawnKind = true;
                    config.IncludeSummonItem = true;
                    config.IncludeAbilities = true;
                    config.CopyTextures = true;
                    break;

                case ExportMode.PluginOnly:
                    config.IncludeSkinDef = true;
                    config.IncludeGeneDef = false;
                    config.IncludePawnKind = false;
                    config.IncludeSummonItem = false;
                    config.IncludeAbilities = false;
                    config.CopyTextures = false;
                    break;
            }
        }

        // ─────────────────────────────────────────────
        // 资产来源分析
        // ─────────────────────────────────────────────

        /// <summary>
        /// 分析所有纹理资产的来源
        /// </summary>
        private void AnalyzeAssetSources(ModExportConfig config)
        {
            if (config.SkinDef?.layers == null) return;

            config.AssetSources.Clear();
            config.DetectedDependencies.Clear();

            foreach (var layer in config.SkinDef.layers)
            {
                if (string.IsNullOrEmpty(layer.texPath)) continue;

                var sourceInfo = DetectAssetSource(layer.texPath);
                config.AssetSources.Add(sourceInfo);

                // 收集外部模组依赖
                if (sourceInfo.SourceType == AssetSourceType.ExternalMod &&
                    !string.IsNullOrEmpty(sourceInfo.SourceModPackageId) &&
                    !config.DetectedDependencies.Contains(sourceInfo.SourceModPackageId))
                {
                    config.DetectedDependencies.Add(sourceInfo.SourceModPackageId);
                    Log.Message($"[CharacterStudio] 检测到外部模组依赖: {sourceInfo.SourceModName} ({sourceInfo.SourceModPackageId})");
                }
            }
        }

        /// <summary>
        /// 检测单个资产的来源类型
        /// </summary>
        private AssetSourceInfo DetectAssetSource(string texPath)
        {
            var info = new AssetSourceInfo { OriginalPath = texPath };

            // 检查是否是绝对路径（本地文件）
            if (texPath.Contains(":") || texPath.StartsWith("/"))
            {
                info.SourceType = AssetSourceType.LocalFile;
                info.ResolvedPath = texPath;
                return info;
            }

            // 检查是否存在于游戏内容数据库
            if (ContentFinder<UnityEngine.Texture2D>.Get(texPath, false) != null)
            {
                // 尝试确定来源模组
                foreach (var mod in LoadedModManager.RunningMods)
                {
                    // 检查模组的 Textures 目录
                    string modTexPath = Path.Combine(mod.RootDir, "Textures", texPath.Replace('/', Path.DirectorySeparatorChar));
                    string[] extensions = { ".png", ".PNG", ".jpg", ".JPG" };
                    
                    foreach (var ext in extensions)
                    {
                        if (File.Exists(modTexPath + ext))
                        {
                            // 判断是否是 Core 或 Royalty 等官方内容
                            if (mod.PackageId.StartsWith("ludeon."))
                            {
                                info.SourceType = AssetSourceType.VanillaContent;
                                info.ResolvedPath = texPath;
                            }
                            else
                            {
                                info.SourceType = AssetSourceType.ExternalMod;
                                info.SourceModPackageId = mod.PackageId;
                                info.SourceModName = mod.Name;
                                info.ResolvedPath = texPath;
                            }
                            return info;
                        }
                    }
                }

                // 未找到具体模组，假定为游戏内置
                info.SourceType = AssetSourceType.VanillaContent;
                info.ResolvedPath = texPath;
            }
            else
            {
                // 无法定位，标记为本地文件
                info.SourceType = AssetSourceType.LocalFile;
                info.ResolvedPath = texPath;
            }

            return info;
        }

        // ─────────────────────────────────────────────
        // 目录结构
        // ─────────────────────────────────────────────

        private void CreateDirectoryStructure(string modPath)
        {
            // 创建主目录
            Directory.CreateDirectory(modPath);

            // 创建子目录
            Directory.CreateDirectory(Path.Combine(modPath, "About"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs"));
            Directory.CreateDirectory(Path.Combine(modPath, "Textures"));
            Directory.CreateDirectory(Path.Combine(modPath, "1.6"));
            Directory.CreateDirectory(Path.Combine(modPath, "1.6", "Assemblies"));
            Directory.CreateDirectory(Path.Combine(modPath, "Languages"));
            Directory.CreateDirectory(Path.Combine(modPath, "Languages", "English"));
            Directory.CreateDirectory(Path.Combine(modPath, "Languages", "English", "Keyed"));
            Directory.CreateDirectory(Path.Combine(modPath, "Languages", "ChineseSimplified"));
            Directory.CreateDirectory(Path.Combine(modPath, "Languages", "ChineseSimplified", "Keyed"));
        }

        // ─────────────────────────────────────────────
        // About.xml 生成
        // ─────────────────────────────────────────────

        private void GenerateAboutXml(string modPath, ModExportConfig config)
        {
            string packageId = $"{SanitizePackageId(config.Author)}.{SanitizePackageId(config.ModName)}";

            // 构建依赖列表
            var modDependencies = new XElement("modDependencies",
                new XElement("li",
                    new XElement("packageId", "CharacterStudio.Main"),
                    new XElement("displayName", "Character Studio")
                )
            );

            // 添加检测到的外部模组依赖
            foreach (var depPackageId in config.DetectedDependencies)
            {
                // 查找模组名称
                string displayName = depPackageId;
                var sourceInfo = config.AssetSources.FirstOrDefault(s => s.SourceModPackageId == depPackageId);
                if (sourceInfo != null && !string.IsNullOrEmpty(sourceInfo.SourceModName))
                {
                    displayName = sourceInfo.SourceModName;
                }

                modDependencies.Add(new XElement("li",
                    new XElement("packageId", depPackageId),
                    new XElement("displayName", displayName)
                ));
            }

            // 构建 loadAfter 列表
            var loadAfter = new XElement("loadAfter",
                new XElement("li", "CharacterStudio.Main"),
                new XElement("li", "Ludeon.RimWorld")
            );

            // 添加外部模组到 loadAfter
            foreach (var depPackageId in config.DetectedDependencies)
            {
                loadAfter.Add(new XElement("li", depPackageId));
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("ModMetaData",
                    new XElement("name", config.ModName),
                    new XElement("author", config.Author),
                    new XElement("packageId", packageId),
                    new XElement("supportedVersions",
                        new XElement("li", "1.6")
                    ),
                    new XElement("description", config.Description),
                    modDependencies,
                    loadAfter
                )
            );

            string aboutPath = Path.Combine(modPath, "About", "About.xml");
            doc.Save(aboutPath);

            if (config.DetectedDependencies.Count > 0)
            {
                Log.Message($"[CharacterStudio] About.xml 已生成，包含 {config.DetectedDependencies.Count} 个外部模组依赖");
            }
        }

        // ─────────────────────────────────────────────
        // 纹理复制
        // ─────────────────────────────────────────────

        private void CopyTextures(string modPath, ModExportConfig config)
        {
            if (config.SkinDef?.layers == null) return;

            string texturesPath = Path.Combine(modPath, "Textures", "CS", SanitizeFileName(config.ModName));
            Directory.CreateDirectory(texturesPath);

            int layerIndex = 0;
            for (int i = 0; i < config.SkinDef.layers.Count; i++)
            {
                var layer = config.SkinDef.layers[i];
                if (string.IsNullOrEmpty(layer.texPath)) continue;

                // 查找对应的资产来源信息
                var sourceInfo = config.AssetSources.FirstOrDefault(s => s.OriginalPath == layer.texPath);

                // 根据资产来源类型决定处理方式
                if (sourceInfo != null)
                {
                    switch (sourceInfo.SourceType)
                    {
                        case AssetSourceType.VanillaContent:
                            // 游戏内置资源，保持原路径，不复制
                            Log.Message($"[CharacterStudio] 保留游戏内置资源路径: {layer.texPath}");
                            continue;

                        case AssetSourceType.ExternalMod:
                            // 外部模组资源，保持原路径，不复制（依赖已添加到 About.xml）
                            Log.Message($"[CharacterStudio] 保留外部模组资源路径: {layer.texPath} (来自 {sourceInfo.SourceModName})");
                            continue;

                        case AssetSourceType.LocalFile:
                            // 本地文件，需要复制
                            break;
                    }
                }

                // 复制本地文件
                string? sourceFile = FindSourceTexture(layer.texPath, config.SourceTexturePaths);
                if (sourceFile == null)
                {
                    Log.Warning($"[CharacterStudio] 无法找到纹理: {layer.texPath}");
                    continue;
                }

                // 生成新的文件名
                string newFileName = $"Layer_{layerIndex:D2}_{layer.layerName ?? "Unknown"}";
                string extension = Path.GetExtension(sourceFile);
                string destFile = Path.Combine(texturesPath, newFileName + extension);

                try
                {
                    File.Copy(sourceFile, destFile, true);

                    // 更新图层的纹理路径
                    layer.texPath = $"CS/{SanitizeFileName(config.ModName)}/{newFileName}";
                    layerIndex++;
                    Log.Message($"[CharacterStudio] 已复制本地纹理: {newFileName}{extension}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 复制纹理失败: {ex.Message}");
                }
            }
        }

        private string? FindSourceTexture(string texPath, List<string> searchPaths)
        {
            // 首先检查是否是完整路径
            if (File.Exists(texPath))
            {
                return texPath;
            }

            // 尝试不同的扩展名
            string[] extensions = { ".png", ".PNG", ".jpg", ".JPG", ".jpeg" };

            foreach (var searchPath in searchPaths)
            {
                foreach (var ext in extensions)
                {
                    string fullPath = Path.Combine(searchPath, texPath + ext);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return null;
        }

        // ─────────────────────────────────────────────
        // 皮肤定义 XML 生成
        // ─────────────────────────────────────────────

        private void GenerateSkinDefXml(string modPath, ModExportConfig config)
        {
            if (config.SkinDef == null) return;

            var skin = config.SkinDef;

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs",
                    new XElement("CharacterStudio.Core.PawnSkinDef",
                        new XElement("defName", skin.defName ?? $"Skin_{SanitizeFileName(config.ModName)}"),
                        new XElement("label", skin.label ?? config.ModName),
                        new XElement("description", skin.description ?? config.Description),
                        new XElement("hideVanillaHead", skin.hideVanillaHead.ToString().ToLower()),
                        new XElement("hideVanillaHair", skin.hideVanillaHair.ToString().ToLower()),
                        new XElement("hideVanillaBody", skin.hideVanillaBody.ToString().ToLower()),
                        new XElement("hideVanillaApparel", skin.hideVanillaApparel.ToString().ToLower()),
                        new XElement("humanlikeOnly", skin.humanlikeOnly.ToString().ToLower()),
                        new XElement("author", config.Author),
                        new XElement("version", config.Version),
                        GenerateLayersXml(skin.layers),
                        GenerateTargetRacesXml(skin.targetRaces)
                    )
                )
            );

            string defsPath = Path.Combine(modPath, "Defs", "SkinDefs.xml");
            doc.Save(defsPath);
        }

        private XElement GenerateLayersXml(List<PawnLayerConfig>? layers)
        {
            var element = new XElement("layers");

            if (layers == null || layers.Count == 0)
            {
                return element;
            }

            foreach (var layer in layers)
            {
                element.Add(new XElement("li",
                    new XElement("layerName", layer.layerName ?? ""),
                    new XElement("texPath", layer.texPath ?? ""),
                    new XElement("anchorTag", layer.anchorTag ?? "Head"),
                    new XElement("offset", $"({layer.offset.x:F3}, {layer.offset.y:F3}, {layer.offset.z:F3})"),
                    new XElement("drawOrder", layer.drawOrder),
                    new XElement("scale", $"({layer.scale.x:F2}, {layer.scale.y:F2})"),
                    new XElement("flipHorizontal", layer.flipHorizontal.ToString().ToLower()),
                    new XElement("colorType", layer.colorType.ToString()),
                    new XElement("visible", layer.visible.ToString().ToLower())
                ));
            }

            return element;
        }

        private XElement GenerateTargetRacesXml(List<string>? races)
        {
            var element = new XElement("targetRaces");

            if (races == null || races.Count == 0)
            {
                return element;
            }

            foreach (var race in races)
            {
                element.Add(new XElement("li", race));
            }

            return element;
        }

        // ─────────────────────────────────────────────
        // 基因定义生成
        // ─────────────────────────────────────────────

        private void GenerateGeneDefXml(string modPath, ModExportConfig config)
        {
            if (config.SkinDef == null) return;

            string safeName = SanitizeFileName(config.ModName);
            string geneDefName = $"CS_Gene_Face_{safeName}";
            string skinDefName = config.SkinDef.defName ?? $"Skin_{safeName}";
            string iconPath = string.IsNullOrEmpty(config.GeneIconPath)
                ? $"CS/{safeName}/Icon"
                : config.GeneIconPath;

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs",
                    new XElement("GeneDef",
                        new XElement("defName", geneDefName),
                        new XElement("label", $"Face: {config.ModName}"),
                        new XElement("description",
                            $"Changes the carrier's appearance to resemble {config.ModName}.\n\n" +
                            (config.OverlayMode
                                ? "This is an overlay effect that adds to the existing appearance."
                                : "This replaces the carrier's head appearance.")),
                        new XElement("iconPath", iconPath),
                        new XElement("biostatCpx", 0),
                        new XElement("biostatMet", 0),
                        new XElement("displayCategory", config.GeneCategory),
                        new XElement("displayOrderInCategory", 100),
                        new XElement("selectionWeight", 0), // 不在随机生成中出现
                        new XElement("modExtensions",
                            new XElement("li", new XAttribute("Class", "CharacterStudio.Core.DefModExtension_SkinLink"),
                                new XElement("skinDefName", skinDefName),
                                new XElement("priority", 100),
                                new XElement("overlayMode", config.OverlayMode.ToString().ToLower()),
                                new XElement("hideVanillaHead", (!config.OverlayMode).ToString().ToLower()),
                                new XElement("hideVanillaHair", (!config.OverlayMode && (config.SkinDef?.hideVanillaHair ?? true)).ToString().ToLower())
                            )
                        )
                    )
                )
            );

            string geneDefsPath = Path.Combine(modPath, "Defs", "GeneDefs.xml");
            doc.Save(geneDefsPath);

            Log.Message($"[CharacterStudio] 基因定义已生成: {geneDefsPath}");
        }

        // ─────────────────────────────────────────────
        // 单位定义生成
        // ─────────────────────────────────────────────

        private void GenerateUnitDefXml(string modPath, ModExportConfig config)
        {
            if (config.SkinDef == null) return;

            string safeName = SanitizeFileName(config.ModName);
            string pawnKindName = $"CS_PawnKind_{safeName}";
            string thingDefName = $"CS_Item_Summon_{safeName}";
            string skinDefName = config.SkinDef.defName ?? $"Skin_{safeName}";

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs",
                    // PawnKindDef
                    CreatePawnKindDefElement(pawnKindName, config, skinDefName),
                    // Summoning Item
                    new XElement("ThingDef", new XAttribute("ParentName", "ResourceBase"),
                        new XElement("defName", thingDefName),
                        new XElement("label", $"Summon: {config.ModName}"),
                        new XElement("description", $"Use to summon {config.ModName}."),
                        new XElement("graphicData",
                            new XElement("texPath", "Things/Item/Resource/UnfinishedComponent"), // 占位图
                            new XElement("graphicClass", "Graphic_Single")
                        ),
                        new XElement("statBases",
                            new XElement("MarketValue", 1000),
                            new XElement("Mass", 0.5)
                        ),
                        new XElement("thingCategories",
                            new XElement("li", "Items")
                        ),
                        new XElement("comps",
                            // CompUsable：负责提供使用动作
                            new XElement("li", new XAttribute("Class", "CompProperties_Usable"),
                                new XElement("useJob", "UseItem"),
                                new XElement("useLabel", "Summon")
                            ),
                            // CompSummonCharacter：负责实际效果（继承自 CompUseEffect）
                            new XElement("li", new XAttribute("Class", "CharacterStudio.Items.CompProperties_SummonCharacter"),
                                new XElement("pawnKind", pawnKindName),
                                new XElement("arrivalMode", "DropPod")
                            )
                        )
                    )
                )
            );

            string unitDefsPath = Path.Combine(modPath, "Defs", "UnitDefs.xml");
            doc.Save(unitDefsPath);

            Log.Message($"[CharacterStudio] 单位定义已生成: {unitDefsPath}");
        }

        /// <summary>
        /// 创建 PawnKindDef 元素
        /// </summary>
        private XElement CreatePawnKindDefElement(string pawnKindName, ModExportConfig config, string skinDefName)
        {
            var pawnKindDef = new XElement("PawnKindDef",
                new XElement("defName", pawnKindName),
                new XElement("label", config.ModName),
                new XElement("race", "Human"),
                new XElement("combatPower", 100),
                new XElement("defaultFactionType", "PlayerColony")
            );

            // 仅在有技能时添加技能元素
            var abilitiesXml = GenerateAbilitiesXml(config.Abilities);
            if (abilitiesXml != null)
            {
                pawnKindDef.Add(abilitiesXml);
            }

            // 添加 modExtensions
            pawnKindDef.Add(new XElement("modExtensions",
                new XElement("li", new XAttribute("Class", "CharacterStudio.Core.DefModExtension_SkinLink"),
                    new XElement("skinDefName", skinDefName),
                    new XElement("priority", 100),
                    new XElement("hideVanillaHead", "true"),
                    new XElement("hideVanillaHair", "true")
                ),
                new XElement("li", new XAttribute("Class", "CharacterStudio.AI.CompProperties_CustomAI"),
                    new XElement("behavior",
                        new XElement("behaviorType", "Normal")
                    )
                )
            ));

            return pawnKindDef;
        }

        // ─────────────────────────────────────────────
        // 技能定义生成
        // ─────────────────────────────────────────────

        private void GenerateAbilityDefXml(string modPath, ModExportConfig config)
        {
            var element = new XElement("Defs");

            foreach (var ability in config.Abilities)
            {
                // 构建 AbilityDef
                var abilityDef = new XElement("AbilityDef",
                    new XElement("defName", ability.defName),
                    new XElement("label", ability.label),
                    new XElement("description", ability.description),
                    new XElement("iconPath", ability.iconPath),
                    new XElement("cooldownTicksRange", ability.cooldownTicks),
                    new XElement("verbProperties",
                        new XElement("warmupTime", ability.warmupTicks / 60f),
                        new XElement("range", ability.range),
                        new XElement("targetParams",
                            new XElement("canTargetPawns", "true"),
                            new XElement("canTargetLocations", "true")
                        )
                    ),
                    new XElement("comps",
                        new XElement("li", new XAttribute("Class", "CharacterStudio.Abilities.CompProperties_AbilityModular"),
                            GenerateEffectsXml(ability.effects)
                        )
                    )
                );

                // 根据载体类型调整 verbProperties
                var verbProps = abilityDef.Element("verbProperties");
                switch (ability.carrierType)
                {
                    case AbilityCarrierType.Self:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        verbProps.Add(new XElement("targetable", "false"));
                        break;
                    case AbilityCarrierType.Touch:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbilityTouch"));
                        verbProps.Add(new XElement("range", "-1"));
                        break;
                    case AbilityCarrierType.Target:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        break;
                    case AbilityCarrierType.Projectile:
                        // 暂不支持投射物生成，回退到普通施法
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        break;
                    case AbilityCarrierType.Area:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        verbProps.Add(new XElement("radius", ability.radius));
                        break;
                }

                element.Add(abilityDef);
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), element);
            string abilityDefsPath = Path.Combine(modPath, "Defs", "AbilityDefs.xml");
            doc.Save(abilityDefsPath);

            Log.Message($"[CharacterStudio] 技能定义已生成: {abilityDefsPath}");
        }

        private XElement? GenerateAbilitiesXml(List<ModularAbilityDef>? abilities)
        {
            if (abilities == null || abilities.Count == 0)
            {
                return null;
            }

            var element = new XElement("abilities");
            foreach (var ability in abilities)
            {
                element.Add(new XElement("li", ability.defName));
            }
            return element;
        }

        private XElement GenerateEffectsXml(List<AbilityEffectConfig> effects)
        {
            var element = new XElement("effects");
            foreach (var effect in effects)
            {
                var effectEl = new XElement("li",
                    new XElement("type", effect.type.ToString()),
                    new XElement("amount", effect.amount),
                    new XElement("duration", effect.duration),
                    new XElement("chance", effect.chance)
                );

                if (effect.damageDef != null)
                    effectEl.Add(new XElement("damageDef", effect.damageDef.defName));
                if (effect.hediffDef != null)
                    effectEl.Add(new XElement("hediffDef", effect.hediffDef.defName));
                if (effect.summonKind != null)
                    effectEl.Add(new XElement("summonKind", effect.summonKind.defName));
                
                effectEl.Add(new XElement("summonCount", effect.summonCount));

                element.Add(effectEl);
            }
            return element;
        }

        // ─────────────────────────────────────────────
        // Manifest.xml 生成
        // ─────────────────────────────────────────────

        /// <summary>
        /// 生成 Manifest.xml，记录 Character Studio 版本信息
        /// </summary>
        private void GenerateManifestXml(string modPath, ModExportConfig config)
        {
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Manifest",
                    new XElement("GeneratorVersion", CS_VERSION),
                    new XElement("GeneratedAt", DateTime.UtcNow.ToString("o")),
                    new XElement("ExportMode", config.Mode.ToString()),
                    new XElement("ModName", config.ModName),
                    new XElement("ModVersion", config.Version),
                    new XElement("Author", config.Author),
                    new XElement("ExportSettings",
                        new XElement("IncludeSkinDef", config.IncludeSkinDef.ToString().ToLower()),
                        new XElement("IncludeGeneDef", config.IncludeGeneDef.ToString().ToLower()),
                        new XElement("IncludePawnKind", config.IncludePawnKind.ToString().ToLower()),
                        new XElement("IncludeSummonItem", config.IncludeSummonItem.ToString().ToLower()),
                        new XElement("IncludeAbilities", config.IncludeAbilities.ToString().ToLower()),
                        new XElement("CopyTextures", config.CopyTextures.ToString().ToLower()),
                        new XElement("ExportAsGene", config.ExportAsGene.ToString().ToLower()),
                        new XElement("OverlayMode", config.OverlayMode.ToString().ToLower())
                    ),
                    new XElement("AssetSources",
                        from source in config.AssetSources
                        select new XElement("Asset",
                            new XAttribute("type", source.SourceType.ToString()),
                            new XElement("OriginalPath", source.OriginalPath),
                            new XElement("ResolvedPath", source.ResolvedPath),
                            source.SourceModPackageId != null ? new XElement("SourceMod", source.SourceModPackageId) : null
                        )
                    ),
                    new XElement("Dependencies",
                        from dep in config.DetectedDependencies
                        select new XElement("Dependency", dep)
                    )
                )
            );

            string manifestPath = Path.Combine(modPath, "About", "Manifest.xml");
            doc.Save(manifestPath);

            Log.Message($"[CharacterStudio] Manifest.xml 已生成: {manifestPath}");
        }

        // ─────────────────────────────────────────────
        // 工具方法
        // ─────────────────────────────────────────────

        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Unknown";
            }

            // 移除不允许的字符
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());

            // 替换空格为下划线
            sanitized = sanitized.Replace(' ', '_');

            // 确保不为空
            if (string.IsNullOrEmpty(sanitized))
            {
                return "Unknown";
            }

            return sanitized;
        }

        private string SanitizePackageId(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "unknown";
            }

            // Package ID 只允许小写字母、数字和点
            var sanitized = new string(name.ToLower()
                .Where(c => char.IsLetterOrDigit(c) || c == '.')
                .ToArray());

            if (string.IsNullOrEmpty(sanitized))
            {
                return "unknown";
            }

            return sanitized;
        }
    }
}