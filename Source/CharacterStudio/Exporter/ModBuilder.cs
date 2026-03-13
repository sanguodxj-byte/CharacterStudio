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
            var exportConfig = CloneExportConfig(config);

            ApplyExportModeDefaults(exportConfig);

            try
            {
                ExecuteExportPipeline(modPath, exportConfig);
                Log.Message($"[CharacterStudio] 模组导出成功: {modPath}");
                return modPath;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 导出失败: {ex}");
                CleanupFailedExport(modPath);
                throw;
            }
        }

        private ModExportConfig CloneExportConfig(ModExportConfig config)
        {
            return new ModExportConfig
            {
                ModName = config.ModName,
                Author = config.Author,
                Version = config.Version,
                Description = config.Description,
                OutputPath = config.OutputPath,
                SkinDef = config.SkinDef!.Clone(),
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
        }

        private void ExecuteExportPipeline(string modPath, ModExportConfig exportConfig)
        {
            CreateDirectoryStructure(modPath);
            AnalyzeAssetSources(exportConfig);
            GenerateAboutXml(modPath, exportConfig);

            if (exportConfig.CopyTextures)
            {
                CopyTextures(modPath, exportConfig);
            }

            GenerateDefinitionFiles(modPath, exportConfig);
            GenerateManifestXml(modPath, exportConfig);
        }

        private void GenerateDefinitionFiles(string modPath, ModExportConfig exportConfig)
        {
            if (exportConfig.IncludeSkinDef)
            {
                GenerateSkinDefXml(modPath, exportConfig);
            }

            if (exportConfig.IncludeGeneDef && exportConfig.ExportAsGene)
            {
                GenerateGeneDefXml(modPath, exportConfig);
            }

            if (exportConfig.IncludePawnKind)
            {
                GenerateUnitDefXml(modPath, exportConfig);
            }

            if (exportConfig.IncludeAbilities && exportConfig.Abilities.Count > 0)
            {
                GenerateAbilityDefXml(modPath, exportConfig);
            }
        }

        private void CleanupFailedExport(string modPath)
        {
            try
            {
                if (!Directory.Exists(modPath))
                {
                    return;
                }

                Directory.Delete(modPath, true);
                Log.Message($"[CharacterStudio] 已清理未完成的导出目录: {modPath}");
            }
            catch (Exception cleanupEx)
            {
                Log.Warning($"[CharacterStudio] 清理目录失败: {cleanupEx.Message}");
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
            config.AssetSources.Clear();
            config.DetectedDependencies.Clear();

            foreach (var texPath in EnumerateTexturePaths(config))
            {
                var sourceInfo = ExportAssetUtility.DetectAssetSource(texPath);
                config.AssetSources.Add(sourceInfo);

                string? packageId = sourceInfo.SourceModPackageId;
                if (sourceInfo.SourceType != AssetSourceType.ExternalMod || string.IsNullOrWhiteSpace(packageId))
                {
                    continue;
                }

                string dependencyPackageId = packageId!;
                if (config.DetectedDependencies.Contains(dependencyPackageId))
                {
                    continue;
                }

                config.DetectedDependencies.Add(dependencyPackageId);
                Log.Message($"[CharacterStudio] 检测到外部模组依赖: {sourceInfo.SourceModName} ({dependencyPackageId})");
            }
        }

        private IEnumerable<string> EnumerateTexturePaths(ModExportConfig config)
        {
            return ExportAssetUtility.EnumerateTexturePaths(config.SkinDef, config.Abilities, config.GeneIconPath);
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
                if (sourceInfo != null && !string.IsNullOrWhiteSpace(sourceInfo.SourceModName))
                {
                    displayName = sourceInfo.SourceModName!;
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
            if (config.SkinDef == null) return;

            string texturesPath = Path.Combine(modPath, "Textures", "CS", SanitizeFileName(config.ModName));
            Directory.CreateDirectory(texturesPath);

            var remap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int assetIndex = 0;

            foreach (var texPath in EnumerateTexturePaths(config))
            {
                var sourceInfo = config.AssetSources.FirstOrDefault(s => s.OriginalPath == texPath);
                if (sourceInfo != null)
                {
                    switch (sourceInfo.SourceType)
                    {
                        case AssetSourceType.VanillaContent:
                            Log.Message($"[CharacterStudio] 保留游戏内置资源路径: {texPath}");
                            continue;
                        case AssetSourceType.ExternalMod:
                            Log.Message($"[CharacterStudio] 保留外部模组资源路径: {texPath} (来自 {sourceInfo.SourceModName})");
                            continue;
                    }
                }

                string? sourceFile = FindSourceTexture(texPath, config.SourceTexturePaths);
                if (sourceFile == null)
                {
                    Log.Warning($"[CharacterStudio] 无法找到纹理: {texPath}");
                    continue;
                }

                string roleName = $"Asset_{assetIndex:D2}_{Path.GetFileNameWithoutExtension(sourceFile)}";
                string extension = Path.GetExtension(sourceFile);
                string destFile = Path.Combine(texturesPath, roleName + extension);

                try
                {
                    File.Copy(sourceFile, destFile, true);
                    remap[texPath] = $"CS/{SanitizeFileName(config.ModName)}/{roleName}";
                    assetIndex++;
                    Log.Message($"[CharacterStudio] 已复制本地纹理: {roleName}{extension}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 复制纹理失败: {ex.Message}");
                }
            }

            ApplyTextureRemap(config, remap);
        }

        private void ApplyTextureRemap(ModExportConfig config, Dictionary<string, string> remap)
        {
            if (config.SkinDef == null || remap.Count == 0)
            {
                return;
            }

            string Remap(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return path ?? string.Empty;
                }

                string normalizedPath = path!;
                return remap.TryGetValue(normalizedPath, out var mapped)
                    ? mapped ?? normalizedPath
                    : normalizedPath;
            }

            var skin = config.SkinDef;
            skin.previewTexPath = Remap(skin.previewTexPath);

            if (skin.layers != null)
            {
                foreach (var layer in skin.layers)
                {
                    if (layer == null) continue;
                    layer.texPath = Remap(layer.texPath);
                    layer.maskTexPath = Remap(layer.maskTexPath);
                }
            }

            if (skin.baseAppearance?.slots != null)
            {
                foreach (var slot in skin.baseAppearance.slots)
                {
                    if (slot == null) continue;
                    slot.texPath = Remap(slot.texPath);
                    slot.maskTexPath = Remap(slot.maskTexPath);
                }
            }

            if (skin.faceConfig?.components != null)
            {
                foreach (var component in skin.faceConfig.components)
                {
                    if (component?.expressions == null) continue;
                    foreach (var expression in component.expressions)
                    {
                        if (expression == null) continue;
                        expression.texPath = Remap(expression.texPath);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(config.GeneIconPath))
            {
                config.GeneIconPath = Remap(config.GeneIconPath);
            }

            foreach (var ability in config.Abilities ?? new List<ModularAbilityDef>())
            {
                if (ability == null) continue;
                ability.iconPath = Remap(ability.iconPath);
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

            var doc = ModExportXmlWriter.CreateSkinDefDocument(
                skin,
                config.ModName,
                config.Description,
                config.Author,
                config.Version,
                SanitizeFileName);

            string defsPath = Path.Combine(modPath, "Defs", "SkinDefs.xml");
            doc.Save(defsPath);
        }

        private XElement? GenerateBaseAppearanceXml(BaseAppearanceConfig? baseAppearance)
        {
            return ModExportXmlWriter.GenerateBaseAppearanceXml(baseAppearance);
        }

        private XElement GenerateLayersXml(List<PawnLayerConfig>? layers)
        {
            return ModExportXmlWriter.GenerateLayersXml(layers);
        }

        private XElement? GenerateStringListXml(string tagName, List<string>? values)
        {
            return ModExportXmlWriter.GenerateStringListXml(tagName, values);
        }

        private XElement GenerateFaceConfigXml(PawnFaceConfig config)
        {
            return ModExportXmlWriter.GenerateFaceConfigXml(config);
        }

        private XElement GenerateTargetRacesXml(List<string>? races)
        {
            return ModExportXmlWriter.GenerateTargetRacesXml(races);
        }

        private XElement? GenerateSkinAbilitiesXml(List<ModularAbilityDef>? abilities)
        {
            return ModExportXmlWriter.GenerateSkinAbilitiesXml(abilities);
        }

        private XElement? GenerateSkinAbilityEffectsXml(List<AbilityEffectConfig>? effects)
        {
            return ModExportXmlWriter.GenerateSkinAbilityEffectsXml(effects);
        }

        private XElement? GenerateRuntimeComponentsXml(List<AbilityRuntimeComponentConfig>? components)
        {
            return ModExportXmlWriter.GenerateRuntimeComponentsXml(components);
        }

        private XElement? GenerateAbilityHotkeysXml(SkinAbilityHotkeyConfig? hotkeys)
        {
            return ModExportXmlWriter.GenerateAbilityHotkeysXml(hotkeys);
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

            var doc = ModExportXmlWriter.CreateGeneDefDocument(config, safeName);

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

            var doc = ModExportXmlWriter.CreateUnitDefDocument(config, safeName);

            string unitDefsPath = Path.Combine(modPath, "Defs", "UnitDefs.xml");
            doc.Save(unitDefsPath);

            Log.Message($"[CharacterStudio] 单位定义已生成: {unitDefsPath}");
        }

        /// <summary>
        /// 创建 PawnKindDef 元素
        /// </summary>
        private XElement CreatePawnKindDefElement(string pawnKindName, ModExportConfig config, string skinDefName)
        {
            return ModExportXmlWriter.CreatePawnKindDefElement(pawnKindName, config, skinDefName);
        }

        // ─────────────────────────────────────────────
        // 技能定义生成
        // ─────────────────────────────────────────────

        private void GenerateAbilityDefXml(string modPath, ModExportConfig config)
        {
            var doc = ModExportXmlWriter.CreateAbilityDefDocument(config.Abilities);
            string abilityDefsPath = Path.Combine(modPath, "Defs", "AbilityDefs.xml");
            doc.Save(abilityDefsPath);

            Log.Message($"[CharacterStudio] 技能定义已生成: {abilityDefsPath}");
        }

        private XElement? GenerateAbilitiesXml(List<ModularAbilityDef>? abilities)
        {
            return ModExportXmlWriter.GenerateAbilityRefsXml(abilities);
        }

        private XElement GenerateEffectsXml(List<AbilityEffectConfig> effects)
        {
            return ModExportXmlWriter.GenerateAbilityEffectsXml(effects);
        }

        // ─────────────────────────────────────────────
        // Manifest.xml 生成
        // ─────────────────────────────────────────────

        /// <summary>
        /// 生成 Manifest.xml，记录 Character Studio 版本信息
        /// </summary>
        private void GenerateManifestXml(string modPath, ModExportConfig config)
        {
            var doc = ModExportXmlWriter.CreateManifestDocument(config, CS_VERSION);

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