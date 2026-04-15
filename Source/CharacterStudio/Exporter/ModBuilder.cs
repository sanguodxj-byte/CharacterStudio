using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Items;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
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
        CosmeticPack
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
        
        public List<string> SkinDefXmlPaths { get; set; } = new List<string>();
        public List<string> PawnKindDefXmlPaths { get; set; } = new List<string>();
        public List<string> SummonItemXmlPaths { get; set; } = new List<string>();
        public List<string> AbilityXmlPaths { get; set; } = new List<string>();

        public SummonArrivalMode RoleCardArrivalMode { get; set; } = SummonArrivalMode.DropPod;
        public SummonSpawnAnimationMode RoleCardSpawnAnimation { get; set; } = SummonSpawnAnimationMode.ExplosionEffect;
        public float RoleCardSpawnAnimationScale { get; set; } = 1f;
        public SummonSpawnEventMode RoleCardSpawnEvent { get; set; } = SummonSpawnEventMode.PositiveLetter;
        public CharacterDefinition CharacterDefinition { get; set; } = new CharacterDefinition();
        public bool IncludeRuntimeTriggers { get; set; } = false;

        public bool AssetRightsConfirmed { get; set; } = false;
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

            NormalizeModuleSelection(exportConfig);

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
                Abilities = config.Abilities?
                    .Select(a => a?.Clone())
                    .OfType<Abilities.ModularAbilityDef>()
                    .ToList() ?? new List<Abilities.ModularAbilityDef>(),
                SourceTexturePaths = config.SourceTexturePaths != null ? new List<string>(config.SourceTexturePaths) : new List<string>(),
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
                CopyTextures = config.CopyTextures,
                SkinDefXmlPaths = config.SkinDefXmlPaths != null ? new List<string>(config.SkinDefXmlPaths) : new List<string>(),
                PawnKindDefXmlPaths = config.PawnKindDefXmlPaths != null ? new List<string>(config.PawnKindDefXmlPaths) : new List<string>(),
                SummonItemXmlPaths = config.SummonItemXmlPaths != null ? new List<string>(config.SummonItemXmlPaths) : new List<string>(),
                AbilityXmlPaths = config.AbilityXmlPaths != null ? new List<string>(config.AbilityXmlPaths) : new List<string>(),
                RoleCardArrivalMode = config.RoleCardArrivalMode,
                RoleCardSpawnAnimation = config.RoleCardSpawnAnimation,
                RoleCardSpawnAnimationScale = config.RoleCardSpawnAnimationScale,
                RoleCardSpawnEvent = config.RoleCardSpawnEvent,
                CharacterDefinition = config.CharacterDefinition?.Clone() ?? new CharacterDefinition(),
                IncludeRuntimeTriggers = config.IncludeRuntimeTriggers,
                AssetRightsConfirmed = config.AssetRightsConfirmed
            };
        }

        private void ExecuteExportPipeline(string modPath, ModExportConfig exportConfig)
        {
            CreateDirectoryStructure(modPath);
            ValidateAssetRights(exportConfig);
            PrepareEquipmentExportBindings(exportConfig);
            GenerateAboutXml(modPath, exportConfig);

            if (exportConfig.CopyTextures)
            {
                CopyTextures(modPath, exportConfig);
            }

            GenerateDefinitionFiles(modPath, exportConfig);
            GenerateManifestXml(modPath, exportConfig);
        }

        private void PrepareEquipmentExportBindings(ModExportConfig config)
        {
            if (config.SkinDef?.equipments == null || config.SkinDef.equipments.Count == 0)
            {
                return;
            }

            config.Abilities ??= new List<ModularAbilityDef>();
            List<ModularAbilityDef> skinAbilities = config.SkinDef.abilities ?? new List<ModularAbilityDef>();
            HashSet<string> existingAbilityNames = new HashSet<string>(
                config.Abilities
                    .Where(static ability => ability != null && !string.IsNullOrWhiteSpace(ability.defName))
                    .Select(static ability => ability.defName),
                StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> flyerThingDefByAbility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (CharacterEquipmentDef equipment in config.SkinDef.equipments)
            {
                if (equipment == null || !equipment.enabled)
                {
                    continue;
                }

                equipment.EnsureDefaults();
                string? sharedFlyerThingDefName = equipment.abilityDefNames?
                    .FirstOrDefault(abilityDefName => !string.IsNullOrWhiteSpace(abilityDefName)
                        && flyerThingDefByAbility.TryGetValue(abilityDefName, out _));
                if (!string.IsNullOrWhiteSpace(sharedFlyerThingDefName)
                    && flyerThingDefByAbility.TryGetValue(sharedFlyerThingDefName!, out string existingFlyerThingDefName))
                {
                    equipment.flyerThingDefName = existingFlyerThingDefName;
                }
                else if (string.IsNullOrWhiteSpace(equipment.flyerThingDefName) && EquipmentNeedsFlyerExport(equipment, skinAbilities, config.Abilities))
                {
                    equipment.flyerThingDefName = $"{equipment.GetResolvedThingDefName()}_Flyer";
                }

                if (!string.IsNullOrWhiteSpace(equipment.flyerThingDefName))
                {
                    foreach (string abilityDefName in equipment.abilityDefNames ?? new List<string>())
                    {
                        if (!string.IsNullOrWhiteSpace(abilityDefName))
                        {
                            flyerThingDefByAbility[abilityDefName] = equipment.flyerThingDefName;
                        }
                    }
                }

                foreach (string abilityDefName in equipment.abilityDefNames ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(abilityDefName) || !existingAbilityNames.Add(abilityDefName))
                    {
                        continue;
                    }

                    ModularAbilityDef? sourceAbility = skinAbilities.FirstOrDefault(ability =>
                        ability != null && string.Equals(ability.defName, abilityDefName, StringComparison.OrdinalIgnoreCase));
                    if (sourceAbility != null)
                    {
                        config.Abilities.Add(sourceAbility.Clone());
                    }
                }
            }

            foreach (CharacterEquipmentDef equipment in config.SkinDef.equipments)
            {
                if (equipment == null || !equipment.enabled || string.IsNullOrWhiteSpace(equipment.flyerThingDefName))
                {
                    continue;
                }

                ApplyFlyerThingDefToBoundAbilities(equipment, config.Abilities);
                ApplyFlyerThingDefToBoundAbilities(equipment, skinAbilities);
            }
        }

        private static bool EquipmentNeedsFlyerExport(
            CharacterEquipmentDef equipment,
            IEnumerable<ModularAbilityDef> skinAbilities,
            IEnumerable<ModularAbilityDef> exportAbilities)
        {
            IEnumerable<ModularAbilityDef> combined = (skinAbilities ?? Enumerable.Empty<ModularAbilityDef>())
                .Concat(exportAbilities ?? Enumerable.Empty<ModularAbilityDef>());

            foreach (string abilityDefName in equipment.abilityDefNames ?? new List<string>())
            {
                ModularAbilityDef? ability = combined.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(candidate.defName, abilityDefName, StringComparison.OrdinalIgnoreCase));
                if (ability?.runtimeComponents == null)
                {
                    continue;
                }

                if (ability.runtimeComponents.Any(component =>
                        component != null
                        && component.enabled
                        && component.type == AbilityRuntimeComponentType.VanillaPawnFlyer))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyFlyerThingDefToBoundAbilities(CharacterEquipmentDef equipment, IEnumerable<ModularAbilityDef> abilities)
        {
            if (string.IsNullOrWhiteSpace(equipment.flyerThingDefName) || abilities == null)
            {
                return;
            }

            foreach (string abilityDefName in equipment.abilityDefNames ?? new List<string>())
            {
                ModularAbilityDef? ability = abilities.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(candidate.defName, abilityDefName, StringComparison.OrdinalIgnoreCase));
                if (ability?.runtimeComponents == null)
                {
                    continue;
                }

                foreach (AbilityRuntimeComponentConfig component in ability.runtimeComponents)
                {
                    if (component != null
                        && component.enabled
                        && component.type == AbilityRuntimeComponentType.VanillaPawnFlyer
                        && string.IsNullOrWhiteSpace(component.flyerThingDefName))
                    {
                        component.flyerThingDefName = equipment.flyerThingDefName;
                    }
                }
            }
        }

        private void CopyExternalXmls(string modPath, List<string> sourcePaths)
        {
            if (sourcePaths == null || sourcePaths.Count == 0) return;
            string defsDir = Path.Combine(modPath, "Defs", "CharacterStudio");
            if (!Directory.Exists(defsDir)) Directory.CreateDirectory(defsDir);

            foreach (string path in sourcePaths)
            {
                if (File.Exists(path))
                {
                    string target = Path.Combine(defsDir, Path.GetFileName(path));
                    File.Copy(path, target, true);
                }
            }
        }

        private void GenerateDefinitionFiles(string modPath, ModExportConfig exportConfig)
        {
            // 1. 处理皮肤定义
            if (exportConfig.IncludeSkinDef)
            {
                GenerateSkinDefXml(modPath, exportConfig);
                if (exportConfig.SkinDefXmlPaths != null && exportConfig.SkinDefXmlPaths.Count > 0)
                    CopyExternalXmls(modPath, exportConfig.SkinDefXmlPaths);
            }

            // 2. 处理基因定义
            if (exportConfig.IncludeGeneDef)
            {
                GenerateGeneDefXml(modPath, exportConfig);
            }

            // 3. 处理单位/角色定义 (PawnKind)
            if (exportConfig.IncludePawnKind)
            {
                GenerateUnitDefXml(modPath, exportConfig);
                GenerateCharacterDefinitionXml(modPath, exportConfig);
                
                if (exportConfig.PawnKindDefXmlPaths != null && exportConfig.PawnKindDefXmlPaths.Count > 0)
                    CopyExternalXmls(modPath, exportConfig.PawnKindDefXmlPaths);
            }

            // 4. 处理运行时触发器
            if (exportConfig.IncludeRuntimeTriggers)
            {
                GenerateRuntimeTriggerXml(modPath, exportConfig);
            }

            // 5. 处理装备/召唤物
            if (exportConfig.IncludeSummonItem)
            {
                GenerateEquipmentThingDefXml(modPath, exportConfig);
                GenerateEquipmentRecipeDefXml(modPath, exportConfig);
                GenerateEquipmentBundleManifestXml(modPath, exportConfig);
                
                if (exportConfig.SummonItemXmlPaths != null && exportConfig.SummonItemXmlPaths.Count > 0)
                    CopyExternalXmls(modPath, exportConfig.SummonItemXmlPaths);
            }

            // 6. 处理技能
            if (exportConfig.IncludeAbilities)
            {
                GenerateAbilityDefXml(modPath, exportConfig);
                
                if (exportConfig.AbilityXmlPaths != null && exportConfig.AbilityXmlPaths.Count > 0)
                    CopyExternalXmls(modPath, exportConfig.AbilityXmlPaths);
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
        /// 规范化模块勾选，保留用户选择，同时修正无效组合
        /// </summary>
        private void NormalizeModuleSelection(ModExportConfig config)
        {
            config.IncludeGeneDef = config.ExportAsGene && config.IncludeGeneDef;

            if (config.IncludeSummonItem)
            {
                config.IncludePawnKind = true;
            }

            if (config.IncludePawnKind || config.IncludeGeneDef)
            {
                config.IncludeSkinDef = true;
            }

            if (config.IncludeRuntimeTriggers)
            {
                config.IncludePawnKind = true;
                config.IncludeSkinDef = true;
            }

            if ((config.Abilities == null || config.Abilities.Count == 0) && (config.AbilityXmlPaths == null || config.AbilityXmlPaths.Count == 0))
            {
                config.IncludeAbilities = false;
            }

            switch (config.Mode)
            {
                case ExportMode.CosmeticPack:
                    config.IncludePawnKind = false;
                    config.IncludeSummonItem = false;
                    config.IncludeAbilities = false;
                    break;

                case ExportMode.FullUnit:
                    if (!config.IncludePawnKind && config.IncludeSummonItem)
                    {
                        config.IncludePawnKind = true;
                    }
                    break;
            }
        }

        // ─────────────────────────────────────────────
        // 导出确认
        // ─────────────────────────────────────────────

        private void ValidateAssetRights(ModExportConfig config)
        {
            if (!config.AssetRightsConfirmed)
            {
                throw new InvalidOperationException("导出模组前，必须先完成资源使用权确认。\n请阅读提示、勾选两次确认，并等待倒计时结束后再导出。");
            }
        }

        // ─────────────────────────────────────────────
        // 目录结构
        // ─────────────────────────────────────────────

        private void CreateDirectoryStructure(string modPath)
        {
            // 创建主目录
            Directory.CreateDirectory(modPath);

            // 创建子目录（参考 SpecialCharacters 结构）
            Directory.CreateDirectory(Path.Combine(modPath, "About"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs", "AbilityDefs"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs", "CharacterTriggers"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs", "GeneDefs"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs", "PawnKindDefs"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs", "SpawnProfileDefs"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs", "RecipeDefs"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs", "SkinDefs"));
            Directory.CreateDirectory(Path.Combine(modPath, "Defs", "ThingDef"));
            Directory.CreateDirectory(Path.Combine(modPath, "Textures"));
            Directory.CreateDirectory(Path.Combine(modPath, "Textures", "Characters"));
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

            var modDependencies = new XElement("modDependencies",
                new XElement("li",
                    new XElement("packageId", "CharacterStudio.Main"),
                    new XElement("displayName", "Character Studio")
                )
            );

            var loadAfter = new XElement("loadAfter",
                new XElement("li", "CharacterStudio.Main"),
                new XElement("li", "Ludeon.RimWorld")
            );

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

            Log.Message("[CharacterStudio] About.xml 已生成");
        }

        // ─────────────────────────────────────────────
        // 纹理复制
        // ─────────────────────────────────────────────

        private void CopyTextures(string modPath, ModExportConfig config)
        {
            if (config.SkinDef == null) return;

            string texturesPath = Path.Combine(modPath, "Textures", "Characters", SanitizeFileName(config.ModName));
            Directory.CreateDirectory(texturesPath);

            var remap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int assetIndex = 0;

            foreach (var texPath in ExportAssetUtility.EnumerateTexturePaths(config.SkinDef, config.Abilities, config.GeneIconPath))
            {
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
                    remap[texPath] = $"Characters/{SanitizeFileName(config.ModName)}/{roleName}";
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

            if (skin.faceConfig?.expressions != null)
            {
                foreach (var expression in skin.faceConfig.expressions)
                {
                    if (expression == null) continue;
                    expression.texPath = Remap(expression.texPath);
                    if (expression.frames != null)
                    {
                        foreach (var frame in expression.frames)
                        {
                            if (frame != null)
                            {
                                frame.texPath = Remap(frame.texPath);
                            }
                        }
                    }
                }
            }

            if (skin.faceConfig?.layeredParts != null)
            {
                foreach (var part in skin.faceConfig.layeredParts)
                {
                    if (part != null)
                    {
                        part.texPath = Remap(part.texPath);
                    }
                }
            }

            if (skin.faceConfig?.eyeDirectionConfig != null)
            {
                var eyeCfg = skin.faceConfig.eyeDirectionConfig;
                eyeCfg.texCenter = Remap(eyeCfg.texCenter);
                eyeCfg.texLeft   = Remap(eyeCfg.texLeft);
                eyeCfg.texRight  = Remap(eyeCfg.texRight);
                eyeCfg.texUp     = Remap(eyeCfg.texUp);
                eyeCfg.texDown   = Remap(eyeCfg.texDown);
            }

            if (skin.equipments != null)
            {
                foreach (var equipment in skin.equipments)
                {
                    if (equipment == null) continue;

                    equipment.worldTexPath = Remap(equipment.worldTexPath);
                    equipment.wornTexPath = Remap(equipment.wornTexPath);
                    equipment.maskTexPath = Remap(equipment.maskTexPath);
                    equipment.previewTexPath = Remap(equipment.previewTexPath);

                    if (equipment.renderData != null)
                    {
                        equipment.renderData.texPath = Remap(equipment.renderData.texPath);
                        equipment.renderData.maskTexPath = Remap(equipment.renderData.maskTexPath);
                        equipment.renderData.triggeredIdleTexPath = Remap(equipment.renderData.triggeredIdleTexPath);
                        equipment.renderData.triggeredDeployTexPath = Remap(equipment.renderData.triggeredDeployTexPath);
                        equipment.renderData.triggeredHoldTexPath = Remap(equipment.renderData.triggeredHoldTexPath);
                        equipment.renderData.triggeredReturnTexPath = Remap(equipment.renderData.triggeredReturnTexPath);
                        equipment.renderData.triggeredIdleMaskTexPath = Remap(equipment.renderData.triggeredIdleMaskTexPath);
                        equipment.renderData.triggeredDeployMaskTexPath = Remap(equipment.renderData.triggeredDeployMaskTexPath);
                        equipment.renderData.triggeredHoldMaskTexPath = Remap(equipment.renderData.triggeredHoldMaskTexPath);
                        equipment.renderData.triggeredReturnMaskTexPath = Remap(equipment.renderData.triggeredReturnMaskTexPath);
                        RemapAnimationOverride(equipment.renderData.triggeredAnimationSouth, Remap);
                        RemapAnimationOverride(equipment.renderData.triggeredAnimationEastWest, Remap);
                        RemapAnimationOverride(equipment.renderData.triggeredAnimationNorth, Remap);
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

                if (ability.visualEffects != null)
                {
                    foreach (var vfx in ability.visualEffects)
                    {
                        if (vfx != null)
                        {
                            vfx.customTexturePath = Remap(vfx.customTexturePath);
                        }
                    }
                }
            }
        }

        private static void RemapAnimationOverride(EquipmentTriggeredAnimationOverride? animationOverride, Func<string?, string> remap)
        {
            if (animationOverride == null)
            {
                return;
            }

            animationOverride.triggeredIdleTexPath = remap(animationOverride.triggeredIdleTexPath);
            animationOverride.triggeredDeployTexPath = remap(animationOverride.triggeredDeployTexPath);
            animationOverride.triggeredHoldTexPath = remap(animationOverride.triggeredHoldTexPath);
            animationOverride.triggeredReturnTexPath = remap(animationOverride.triggeredReturnTexPath);
            animationOverride.triggeredIdleMaskTexPath = remap(animationOverride.triggeredIdleMaskTexPath);
            animationOverride.triggeredDeployMaskTexPath = remap(animationOverride.triggeredDeployMaskTexPath);
            animationOverride.triggeredHoldMaskTexPath = remap(animationOverride.triggeredHoldMaskTexPath);
            animationOverride.triggeredReturnMaskTexPath = remap(animationOverride.triggeredReturnMaskTexPath);
        }

        private string? FindSourceTexture(string texPath, List<string> searchPaths)
        {
            // 首先检查是否是完整路径
            if (File.Exists(texPath))
            {
                return texPath;
            }

            // 尝试不同的扩展名
            string[] extensions = { ".png", ".PNG", ".jpg", ".JPG", ".jpeg", ".JPEG" };

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

            string defsPath = Path.Combine(modPath, "Defs", "SkinDefs", $"{SanitizeFileName(config.ModName)}_SkinDefs.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(defsPath));
            doc.Save(defsPath);
        }

        private void GenerateEquipmentThingDefXml(string modPath, ModExportConfig config)
        {
            var equipments = config.SkinDef?.equipments;
            if (equipments == null || equipments.Count == 0)
            {
                return;
            }

            bool hasEnabledEquipments = equipments.Any(equipment => equipment != null && equipment.enabled);
            if (!hasEnabledEquipments)
            {
                return;
            }

            var doc = ModExportXmlWriter.CreateEquipmentThingDefsDocument(
                equipments.Where(equipment => equipment != null && equipment.enabled).ToList());

            string defsPath = Path.Combine(modPath, "Defs", "ThingDef", $"{SanitizeFileName(config.ModName)}_Apparels.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(defsPath));
            doc.Save(defsPath);

            Log.Message($"[CharacterStudio] 装备 ThingDef 已生成: {defsPath}");
        }

        private void GenerateEquipmentRecipeDefXml(string modPath, ModExportConfig config)
        {
            var equipments = config.SkinDef?.equipments;
            if (equipments == null || equipments.Count == 0)
            {
                return;
            }

            List<XElement> recipeDefs = equipments
                .Where(equipment => equipment != null && equipment.enabled && equipment.allowCrafting)
                .Select(ModExportXmlWriter.GenerateEquipmentRecipeDefXml)
                .Where(element => element != null)
                .Cast<XElement>()
                .ToList();

            if (recipeDefs.Count == 0)
            {
                return;
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs", recipeDefs));

            string defsPath = Path.Combine(modPath, "Defs", "RecipeDefs", $"{SanitizeFileName(config.ModName)}_EquipmentRecipes.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(defsPath));
            doc.Save(defsPath);

            Log.Message($"[CharacterStudio] 装备 RecipeDef 已生成: {defsPath}");
        }

        private void GenerateEquipmentBundleManifestXml(string modPath, ModExportConfig config)
        {
            var equipments = config.SkinDef?.equipments;
            if (equipments == null || equipments.Count == 0)
                return;

            bool hasBundleGroups = equipments.Any(equipment => equipment != null && equipment.enabled && !string.IsNullOrWhiteSpace(equipment.exportGroupKey));
            if (!hasBundleGroups)
                return;

            var doc = ModExportXmlWriter.CreateEquipmentBundleManifestDocument(
                equipments.Where(equipment => equipment != null && equipment.enabled).ToList());

            string defsPath = Path.Combine(modPath, "Defs", "ThingDef", $"{SanitizeFileName(config.ModName)}_EquipmentBundles.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(defsPath));
            doc.Save(defsPath);

            Log.Message($"[CharacterStudio] 装备包清单已生成: {defsPath}");
        }

        // ─────────────────────────────────────────────
        // 基因定义生成
        // ─────────────────────────────────────────────

        private void GenerateGeneDefXml(string modPath, ModExportConfig config)
        {
            if (config.SkinDef == null) return;

            string safeName = SanitizeFileName(config.ModName);
            var doc = ModExportXmlWriter.CreateGeneDefDocument(config, safeName);

            string geneDefsPath = Path.Combine(modPath, "Defs", "GeneDefs", $"{safeName}_GeneDefs.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(geneDefsPath));
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

            var doc = ModExportXmlWriter.CreateUnitDefDocument(config, safeName);

            string unitDefsPath = Path.Combine(modPath, "Defs", "PawnKindDefs", $"{safeName}_PawnKinds.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(unitDefsPath));
            doc.Save(unitDefsPath);

            // ── XenotypeDef 独立文件（可选）────────────────────────────────
            if (!string.IsNullOrEmpty(config.SkinDef.xenotypeDefName))
            {
                string xenoLabel = string.IsNullOrEmpty(config.SkinDef.raceDisplayName)
                    ? config.ModName
                    : config.SkinDef.raceDisplayName;

                var xenoDoc = ModExportXmlWriter.CreateXenotypeDefDocument(
                    config.SkinDef.xenotypeDefName,
                    xenoLabel,
                    config.SkinDef.defName);

                string xenoDefsPath = Path.Combine(modPath, "Defs", "PawnKindDefs", $"{safeName}_Xenotypes.xml");
                Directory.CreateDirectory(Path.GetDirectoryName(xenoDefsPath));
                xenoDoc.Save(xenoDefsPath);

                Log.Message($"[CharacterStudio] XenotypeDef 已生成: {xenoDefsPath}");
            }

            Log.Message($"[CharacterStudio] 单位定义已生成: {unitDefsPath}");
        }

        private void GenerateCharacterDefinitionXml(string modPath, ModExportConfig config)
        {
            if (config.SkinDef == null)
            {
                return;
            }

            CharacterDefinition definition = config.CharacterDefinition?.Clone() ?? new CharacterDefinition();
            ThingDef fallbackRace = config.SkinDef.targetRaces != null && config.SkinDef.targetRaces.Count > 0
                ? DefDatabase<ThingDef>.GetNamedSilentFail(config.SkinDef.targetRaces[0]) ?? ThingDefOf.Human
                : ThingDefOf.Human;
            definition.EnsureDefaults(config.SkinDef.defName ?? SanitizeFileName(config.ModName), fallbackRace, config.SkinDef.attributes);
            
            // 同步皮肤中的进阶属性
            definition.statModifiers = config.SkinDef.statModifiers?.Clone() ?? new Attributes.CharacterStatModifierProfile();
            definition.gender = config.CharacterDefinition?.gender ?? Gender.None;
            var attrs = config.SkinDef.attributes;
            if (attrs != null && !string.IsNullOrWhiteSpace(attrs.favoriteColorHex))
            {
                if (UnityEngine.ColorUtility.TryParseHtmlString(attrs.favoriteColorHex, out Color color))
                {
                    definition.favoriteColor = color;
                }
            }

            string safeName = SanitizeFileName(config.ModName);
            string path = Path.Combine(modPath, "Defs", "PawnKindDefs", $"{safeName}_Character.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            CharacterDefinitionXmlUtility.Save(definition, path);
            Log.Message($"[CharacterStudio] 角色定义已生成: {path}");
        }

        private void GenerateRuntimeTriggerXml(string modPath, ModExportConfig config)
        {
            if (config.SkinDef == null || config.CharacterDefinition == null)
            {
                return;
            }

            List<CharacterRuntimeTriggerDef> triggers = config.CharacterDefinition.runtimeTriggers?
                .Where(static trigger => trigger != null)
                .Select(static trigger => trigger.Clone())
                .ToList() ?? new List<CharacterRuntimeTriggerDef>();
            if (triggers.Count == 0)
            {
                return;
            }

            string ownerCharacterDefName = config.CharacterDefinition.defName;
            if (string.IsNullOrWhiteSpace(ownerCharacterDefName))
            {
                ownerCharacterDefName = config.SkinDef.defName ?? SanitizeFileName(config.ModName);
            }

            CharacterSpawnProfileDef profile = new CharacterSpawnProfileDef
            {
                defName = CharacterSpawnProfileRegistry.GetDefaultProfileDefName(ownerCharacterDefName),
                label = config.CharacterDefinition.displayName,
                description = config.Description,
                ownerCharacterDefName = ownerCharacterDefName,
                skinDefName = config.SkinDef.defName ?? string.Empty,
                raceDefName = config.CharacterDefinition.raceDefName,
                characterDefinition = config.CharacterDefinition.Clone(),
                forcePlayerFaction = true
            };
            profile.characterDefinition.runtimeTriggers?.Clear();

            for (int i = 0; i < triggers.Count; i++)
            {
                CharacterRuntimeTriggerDef trigger = triggers[i];
                if (string.IsNullOrWhiteSpace(trigger.defName))
                {
                    trigger.defName = CharacterRuntimeTriggerRegistry.GetDefaultTriggerDefName(ownerCharacterDefName, i + 1);
                }

                trigger.ownerCharacterDefName = ownerCharacterDefName;
                trigger.spawnProfileDefName = profile.defName;
            }

            string safeName = SanitizeFileName(config.ModName);
            string profilePath = Path.Combine(modPath, "Defs", "SpawnProfileDefs", $"{safeName}_SpawnProfiles.xml");
            string triggerPath = Path.Combine(modPath, "Defs", "CharacterTriggers", $"{safeName}_RuntimeTriggers.xml");
            
            Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
            Directory.CreateDirectory(Path.GetDirectoryName(triggerPath));
            
            CharacterRuntimeTriggerXmlUtility.SaveSpawnProfiles(new[] { profile }, profilePath);
            CharacterRuntimeTriggerXmlUtility.SaveRuntimeTriggers(triggers, triggerPath);
            Log.Message($"[CharacterStudio] 运行时角色配置已生成: {profilePath}");
            Log.Message($"[CharacterStudio] 运行时触发器已生成: {triggerPath}");
        }

        // ─────────────────────────────────────────────
        // 技能定义生成
        // ─────────────────────────────────────────────

        private void GenerateAbilityDefXml(string modPath, ModExportConfig config)
        {
            var doc = ModExportXmlWriter.CreateAbilityDefDocument(config.Abilities);
            string abilityDefsPath = Path.Combine(modPath, "Defs", "AbilityDefs", $"{SanitizeFileName(config.ModName)}_Abilities.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(abilityDefsPath));
            doc.Save(abilityDefsPath);

            Log.Message($"[CharacterStudio] 技能定义已生成: {abilityDefsPath}");
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

        private static string SanitizeFileName(string name)
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
        public static string SanitizeDefName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unnamed";
            var sb = new System.Text.StringBuilder(value!.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
            }
            return sb.Length == 0 ? "Unnamed" : sb.ToString();
        }

        public static void ExportScatteredLooseFiles(PawnSkinDef activeSkin, CharacterDefinition characterDef)
        {
            try
            {
                string safeName = SanitizeFileName(activeSkin.defName ?? "CS_Character");
                string exportDir = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "ScatteredComponents", safeName);
                
                if (Directory.Exists(exportDir))
                {
                    Directory.Delete(exportDir, true);
                }
                Directory.CreateDirectory(exportDir);

                ModExportConfig dummyConfig = new ModExportConfig
                {
                    ModName = safeName,
                    SkinDef = activeSkin.Clone(),
                    CharacterDefinition = characterDef.Clone(),
                    Mode = ExportMode.FullUnit,
                    ExportAsGene = true,
                    CopyTextures = false
                };

                // Because Remap relies on dumping textures to "Textures/" directory, we bypass texture dumping
                // by overriding modName temporarily or just letting the Builder run in "dry-run" mode essentially 
                // but for now let it just generate into exportDir blindly (textures will be placed in scattered textues folder too, which is very helpful)
                var builder = new ModBuilder();
                
                // Set the active builder states manually if necessary, or just run them:
                builder.GenerateEquipmentThingDefXml(exportDir, dummyConfig);
                builder.GenerateEquipmentRecipeDefXml(exportDir, dummyConfig);
                builder.GenerateEquipmentBundleManifestXml(exportDir, dummyConfig);
                builder.GenerateGeneDefXml(exportDir, dummyConfig);
                builder.GenerateUnitDefXml(exportDir, dummyConfig);
                builder.GenerateRuntimeTriggerXml(exportDir, dummyConfig);

                Log.Message($"[CharacterStudio] 散碎配置 XML 成功脱机写出至: {exportDir}");
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 离机生成脱散 XML 失败: {ex}");
            }
        }
    }
}
