using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 纹理内化服务。
    /// 将皮肤中引用的外部绝对路径纹理复制到独立子 mod 目录，
    /// 并将路径重映射为 ContentFinder 可识别的相对路径，
    /// 消除运行时 RuntimeAssetLoader 磁盘 I/O 开销。
    /// </summary>
    public static class TextureInternalizer
    {
        /// <summary>子 mod 文件夹名称</summary>
        public const string SubModFolderName = "CharacterStudio_UserTextures";

        /// <summary>
        /// 内化操作结果
        /// </summary>
        public class InternalizeResult
        {
            public bool success;
            public int copiedCount;
            public int skippedCount;
            public string subModPath = string.Empty;
            public string? errorMessage;
        }

        /// <summary>
        /// 快速检查皮肤是否包含外部（绝对路径）纹理
        /// </summary>
        public static int CountExternalTextures(PawnSkinDef? skin, List<ModularAbilityDef>? abilities)
        {
            if (skin == null) return 0;

            int count = 0;
            foreach (string texPath in ExportAssetUtility.EnumerateTexturePaths(skin, abilities))
            {
                if (IsExternalPath(texPath))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 执行纹理内化：复制外部纹理到子 mod，重映射皮肤路径。
        /// 会直接修改传入的 skin 对象中的路径字段。
        /// </summary>
        public static Dictionary<string, string> BuildPathRemap(PawnSkinDef skin, List<ModularAbilityDef>? abilities, out InternalizeResult result)
        {
            result = new InternalizeResult();
            var remap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string subModPath = GetUserTexturesModPath();
                result.subModPath = subModPath;
                EnsureSubModStructure(subModPath);

                string skinFolderName = SanitizeFolderName(skin.defName ?? "UnnamedSkin");
                string texturesDir = Path.Combine(subModPath, "Textures", $"CS_{skinFolderName}");
                Directory.CreateDirectory(texturesDir);

                var usedInternalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int nextAssetIndex = 0;

                foreach (string texPath in ExportAssetUtility.EnumerateTexturePaths(skin, abilities))
                {
                    if (!IsExternalPath(texPath) && texPath.StartsWith($"CS_{skinFolderName}/", StringComparison.OrdinalIgnoreCase))
                    {
                        usedInternalPaths.Add(texPath);
                        string fileName = Path.GetFileName(texPath);
                        if (fileName.StartsWith("asset_"))
                        {
                            int underscorePos = fileName.IndexOf('_', 6);
                            if (underscorePos > 6 && int.TryParse(fileName.Substring(6, underscorePos - 6), out int idx) && idx >= nextAssetIndex)
                            {
                                nextAssetIndex = idx + 1;
                            }
                        }
                    }
                }

                var resolvedSourceToRelativePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int copiedCount = 0;

                foreach (string texPath in ExportAssetUtility.EnumerateTexturePaths(skin, abilities))
                {
                    if (!IsExternalPath(texPath))
                    {
                        result.skippedCount++;
                        continue;
                    }

                    string? resolvedPath = TryFindExternalFile(texPath);
                    if (resolvedPath == null)
                    {
                        Log.Warning($"[CharacterStudio] 内化跳过：找不到外部纹理文件: {texPath}");
                        result.skippedCount++;
                        continue;
                    }

                    string normalizedResolvedPath = Path.GetFullPath(resolvedPath);
                    if (resolvedSourceToRelativePath.TryGetValue(normalizedResolvedPath, out string existingRelativePath))
                    {
                        remap[texPath] = existingRelativePath;
                        usedInternalPaths.Add(existingRelativePath);
                        continue;
                    }

                    string baseName = Path.GetFileNameWithoutExtension(resolvedPath);
                    string extension = Path.GetExtension(resolvedPath);
                    DecomposeDirectionalBaseName(baseName, out string normalizedBaseName, out string? explicitDirectionSuffix);
                    string roleName = $"asset_{nextAssetIndex:D2}_{normalizedBaseName}";
                    string relativePath = $"CS_{skinFolderName}/{roleName}";
                    string destFile = Path.Combine(texturesDir, roleName + extension);

                    if (!string.IsNullOrEmpty(explicitDirectionSuffix))
                    {
                        string directionalRoleName = roleName + explicitDirectionSuffix;
                        destFile = Path.Combine(texturesDir, directionalRoleName + extension);
                        relativePath = $"CS_{skinFolderName}/{directionalRoleName}";
                    }

                    File.Copy(resolvedPath, destFile, true);
                    remap[texPath] = relativePath;
                    resolvedSourceToRelativePath[normalizedResolvedPath] = relativePath;
                    usedInternalPaths.Add(relativePath);
                    nextAssetIndex++;
                    copiedCount++;

                    copiedCount += TryCopyDirectionalVariants(
                        resolvedPath, normalizedBaseName, explicitDirectionSuffix, extension, roleName, texturesDir,
                        skinFolderName, usedInternalPaths);
                }

                if (Directory.Exists(texturesDir))
                {
                    foreach (string file in Directory.GetFiles(texturesDir))
                    {
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        string correspondingRelativePath = $"CS_{skinFolderName}/{fileNameWithoutExt}";
                        if (!usedInternalPaths.Contains(correspondingRelativePath))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                }

                result.copiedCount = copiedCount;
                result.success = true;
                return remap;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 纹理内化失败: {ex}");
                result.success = false;
                result.errorMessage = ex.Message;
                return remap;
            }
        }

        public static InternalizeResult Internalize(PawnSkinDef skin, List<ModularAbilityDef>? abilities)
        {
            var remap = BuildPathRemap(skin, abilities, out var result);
            if (result.success && remap.Count > 0)
            {
                ApplyPathRemap(skin, abilities, remap);
            }
            return result;
        }

        public static string GetUserTexturesSkinsDir()
        {
            return Path.Combine(GetUserTexturesModPath(), "Textures", "Skins");
        }

        public static string GetUserTexturesEquipmentsDir()
        {
            return Path.Combine(GetUserTexturesModPath(), "Textures", "Equipments");
        }

        public static string GetUserTexturesAbilitiesDir()
        {
            return Path.Combine(GetUserTexturesModPath(), "Textures", "Abilities");
        }

        public static void EnsureUserTexturesSubModExists()
        {
            string subModPath = GetUserTexturesModPath();
            EnsureSubModStructure(subModPath);
            Directory.CreateDirectory(GetUserTexturesSkinsDir());
            Directory.CreateDirectory(GetUserTexturesEquipmentsDir());
            Directory.CreateDirectory(GetUserTexturesAbilitiesDir());
        }

        /// <summary>
        /// 获取子 mod 的绝对路径（位于 RimWorld Mods/ 同级目录）
        /// </summary>
        public static string GetUserTexturesModPath()
        {
            // RimWorld 的 Mods/ 目录
            string? modsDir = null;

            // 从 CharacterStudio 主 mod 的路径推算 Mods/ 目录
            var modContent = CharacterStudioMod.ModContent;
            if (modContent != null)
            {
                string modRootDir = modContent.RootDir;
                modsDir = Path.GetDirectoryName(modRootDir);
            }

            if (string.IsNullOrEmpty(modsDir))
            {
                // 回退：使用 GenFilePaths
                modsDir = Path.Combine(Directory.GetCurrentDirectory(), "Mods");
            }

            return Path.Combine(modsDir!, SubModFolderName);
        }

        /// <summary>
        /// 确保子 mod 目录结构存在
        /// </summary>
        private static void EnsureSubModStructure(string subModPath)
        {
            string aboutDir = Path.Combine(subModPath, "About");
            string texturesDir = Path.Combine(subModPath, "Textures");
            string aboutXmlPath = Path.Combine(aboutDir, "About.xml");

            Directory.CreateDirectory(aboutDir);
            Directory.CreateDirectory(texturesDir);

            // 只在 About.xml 不存在时创建
            if (!File.Exists(aboutXmlPath))
            {
                var aboutXml = new XDocument(
                    new XElement("ModMetaData",
                        new XElement("name", "Character Studio - User Textures"),
                        new XElement("author", "CharacterStudio (Auto-generated)"),
                        new XElement("description", "Auto-generated texture pack for Character Studio. Contains internalized textures for improved runtime performance.\n\nCharacter Studio 自动生成的纹理包。包含内化后的纹理，用于提升运行时性能。"),
                        new XElement("packageId", "characterstudio.usertextures"),
                        new XElement("supportedVersions",
                            new XElement("li", "1.5"),
                            new XElement("li", "1.6")
                        ),
                        new XElement("modDependencies",
                            new XElement("li",
                                new XElement("packageId", "CharacterStudio.Main"),
                                new XElement("displayName", "Character Studio")
                            )
                        )
                    )
                );
                aboutXml.Save(aboutXmlPath);
                Log.Message($"[CharacterStudio] 已创建子 mod: {subModPath}");
            }
        }

        /// <summary>
        /// 重映射皮肤和能力定义中的纹理路径
        /// </summary>
        private static void ApplyPathRemap(PawnSkinDef skin, List<ModularAbilityDef>? abilities, Dictionary<string, string> remap)
        {
            string Remap(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return path ?? string.Empty;
                return remap.TryGetValue(path!, out var mapped) ? mapped : path!;
            }

            // 预览纹理
            skin.previewTexPath = Remap(skin.previewTexPath);

            // 图层
            if (skin.layers != null)
            {
                foreach (var layer in skin.layers)
                {
                    if (layer == null) continue;
                    layer.texPath = Remap(layer.texPath);
                    layer.maskTexPath = Remap(layer.maskTexPath);
                    layer.triggeredIdleTexPath = Remap(layer.triggeredIdleTexPath);
                    layer.triggeredDeployTexPath = Remap(layer.triggeredDeployTexPath);
                    layer.triggeredHoldTexPath = Remap(layer.triggeredHoldTexPath);
                    layer.triggeredReturnTexPath = Remap(layer.triggeredReturnTexPath);
                    layer.triggeredIdleMaskTexPath = Remap(layer.triggeredIdleMaskTexPath);
                    layer.triggeredDeployMaskTexPath = Remap(layer.triggeredDeployMaskTexPath);
                    layer.triggeredHoldMaskTexPath = Remap(layer.triggeredHoldMaskTexPath);
                    layer.triggeredReturnMaskTexPath = Remap(layer.triggeredReturnMaskTexPath);

                    if (layer.triggeredAnimationSouth != null)
                    {
                        layer.triggeredAnimationSouth.triggeredIdleTexPath = Remap(layer.triggeredAnimationSouth.triggeredIdleTexPath);
                        layer.triggeredAnimationSouth.triggeredDeployTexPath = Remap(layer.triggeredAnimationSouth.triggeredDeployTexPath);
                        layer.triggeredAnimationSouth.triggeredHoldTexPath = Remap(layer.triggeredAnimationSouth.triggeredHoldTexPath);
                        layer.triggeredAnimationSouth.triggeredReturnTexPath = Remap(layer.triggeredAnimationSouth.triggeredReturnTexPath);
                        layer.triggeredAnimationSouth.triggeredIdleMaskTexPath = Remap(layer.triggeredAnimationSouth.triggeredIdleMaskTexPath);
                        layer.triggeredAnimationSouth.triggeredDeployMaskTexPath = Remap(layer.triggeredAnimationSouth.triggeredDeployMaskTexPath);
                        layer.triggeredAnimationSouth.triggeredHoldMaskTexPath = Remap(layer.triggeredAnimationSouth.triggeredHoldMaskTexPath);
                        layer.triggeredAnimationSouth.triggeredReturnMaskTexPath = Remap(layer.triggeredAnimationSouth.triggeredReturnMaskTexPath);
                    }

                    if (layer.triggeredAnimationEastWest != null)
                    {
                        layer.triggeredAnimationEastWest.triggeredIdleTexPath = Remap(layer.triggeredAnimationEastWest.triggeredIdleTexPath);
                        layer.triggeredAnimationEastWest.triggeredDeployTexPath = Remap(layer.triggeredAnimationEastWest.triggeredDeployTexPath);
                        layer.triggeredAnimationEastWest.triggeredHoldTexPath = Remap(layer.triggeredAnimationEastWest.triggeredHoldTexPath);
                        layer.triggeredAnimationEastWest.triggeredReturnTexPath = Remap(layer.triggeredAnimationEastWest.triggeredReturnTexPath);
                        layer.triggeredAnimationEastWest.triggeredIdleMaskTexPath = Remap(layer.triggeredAnimationEastWest.triggeredIdleMaskTexPath);
                        layer.triggeredAnimationEastWest.triggeredDeployMaskTexPath = Remap(layer.triggeredAnimationEastWest.triggeredDeployMaskTexPath);
                        layer.triggeredAnimationEastWest.triggeredHoldMaskTexPath = Remap(layer.triggeredAnimationEastWest.triggeredHoldMaskTexPath);
                        layer.triggeredAnimationEastWest.triggeredReturnMaskTexPath = Remap(layer.triggeredAnimationEastWest.triggeredReturnMaskTexPath);
                    }

                    if (layer.triggeredAnimationNorth != null)
                    {
                        layer.triggeredAnimationNorth.triggeredIdleTexPath = Remap(layer.triggeredAnimationNorth.triggeredIdleTexPath);
                        layer.triggeredAnimationNorth.triggeredDeployTexPath = Remap(layer.triggeredAnimationNorth.triggeredDeployTexPath);
                        layer.triggeredAnimationNorth.triggeredHoldTexPath = Remap(layer.triggeredAnimationNorth.triggeredHoldTexPath);
                        layer.triggeredAnimationNorth.triggeredReturnTexPath = Remap(layer.triggeredAnimationNorth.triggeredReturnTexPath);
                        layer.triggeredAnimationNorth.triggeredIdleMaskTexPath = Remap(layer.triggeredAnimationNorth.triggeredIdleMaskTexPath);
                        layer.triggeredAnimationNorth.triggeredDeployMaskTexPath = Remap(layer.triggeredAnimationNorth.triggeredDeployMaskTexPath);
                        layer.triggeredAnimationNorth.triggeredHoldMaskTexPath = Remap(layer.triggeredAnimationNorth.triggeredHoldMaskTexPath);
                        layer.triggeredAnimationNorth.triggeredReturnMaskTexPath = Remap(layer.triggeredAnimationNorth.triggeredReturnMaskTexPath);
                    }
                }
            }

            // 基础外观
            if (skin.baseAppearance?.slots != null)
            {
                foreach (var slot in skin.baseAppearance.slots)
                {
                    if (slot == null) continue;
                    slot.texPath = Remap(slot.texPath);
                    slot.maskTexPath = Remap(slot.maskTexPath);
                }
            }

            // 表情
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
                                frame.texPath = Remap(frame.texPath);
                        }
                    }
                }
            }

            // 分层面部
            if (skin.faceConfig?.layeredParts != null)
            {
                foreach (var part in skin.faceConfig.layeredParts)
                {
                    if (part != null)
                    {
                        part.texPath = Remap(part.texPath);
                        part.texPathSouth = Remap(part.texPathSouth);
                        part.texPathEast = Remap(part.texPathEast);
                        part.texPathNorth = Remap(part.texPathNorth);
                    }
                }
            }

            // 眼睛方向
            if (skin.faceConfig?.eyeDirectionConfig != null)
            {
                var eyeCfg = skin.faceConfig.eyeDirectionConfig;
                eyeCfg.texCenter = Remap(eyeCfg.texCenter);
                eyeCfg.texLeft = Remap(eyeCfg.texLeft);
                eyeCfg.texRight = Remap(eyeCfg.texRight);
                eyeCfg.texUp = Remap(eyeCfg.texUp);
                eyeCfg.texDown = Remap(eyeCfg.texDown);
            }

            // 技能
            if (abilities != null)
            {
                foreach (var ability in abilities)
                {
                    if (ability == null) continue;
                    ability.iconPath = Remap(ability.iconPath);

                    if (ability.visualEffects != null)
                    {
                        foreach (var vfx in ability.visualEffects)
                        {
                            if (vfx != null)
                                vfx.customTexturePath = Remap(vfx.customTexturePath);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 判断路径是否为外部绝对路径
        /// </summary>
        private static bool IsExternalPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            return path!.Contains(":") || path.StartsWith("/") || Path.IsPathRooted(path);
        }

        /// <summary>
        /// 尝试查找外部文件（自动尝试不同扩展名）
        /// </summary>
        private static string? TryFindExternalFile(string texPath)
        {
            if (File.Exists(texPath))
                return texPath;

            string[] extensions = { ".png", ".PNG", ".jpg", ".JPG", ".jpeg", ".JPEG" };
            foreach (var ext in extensions)
            {
                string withExt = texPath + ext;
                if (File.Exists(withExt))
                    return withExt;
            }

            // 尝试去掉扩展名后再加
            string withoutExt = Path.ChangeExtension(texPath, null);
            if (withoutExt != texPath)
            {
                foreach (var ext in extensions)
                {
                    string candidate = withoutExt + ext;
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// 复制主纹理的同时，扫描并复制同目录下的方向变体纹理（_north, _east, _south, _west）。
        /// 返回实际复制的变体数量。
        /// </summary>
        private static int TryCopyDirectionalVariants(
            string resolvedPath,
            string normalizedBaseName,
            string? explicitDirectionSuffix,
            string extension,
            string roleName,
            string texturesDir,
            string skinFolderName,
            HashSet<string> usedInternalPaths)
        {
            string? sourceDir = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                return 0;

            string[] directions = { "_north", "_east", "_south", "_west" };
            int copied = 0;

            foreach (string suffix in directions)
            {
                if (string.Equals(suffix, explicitDirectionSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string variantBaseName = normalizedBaseName + suffix;
                string variantFileName = variantBaseName + extension;
                string variantPath = Path.Combine(sourceDir, variantFileName);

                if (!File.Exists(variantPath))
                    continue;

                string variantRoleName = roleName + suffix;
                string variantDestFile = Path.Combine(texturesDir, variantRoleName + extension);

                try
                {
                    File.Copy(variantPath, variantDestFile, true);
                    string variantRelativePath = $"CS_{skinFolderName}/{variantRoleName}";
                    usedInternalPaths.Add(variantRelativePath);
                    copied++;

                    Log.Message($"[CharacterStudio] 已内化方向变体: {variantFileName} → {variantRelativePath}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 复制方向变体失败: {variantPath} → {variantDestFile}: {ex.Message}");
                }
            }

            return copied;
        }

        private static void DecomposeDirectionalBaseName(string baseName, out string normalizedBaseName, out string? explicitDirectionSuffix)
        {
            normalizedBaseName = baseName;
            explicitDirectionSuffix = null;

            if (string.IsNullOrWhiteSpace(baseName))
                return;

            string[] directions = { "_north", "_east", "_south", "_west" };
            foreach (string suffix in directions)
            {
                if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedBaseName = baseName.Substring(0, baseName.Length - suffix.Length);
                    explicitDirectionSuffix = suffix;
                    return;
                }
            }
        }

        /// <summary>
        /// 清理文件夹名称（移除非法字符）
        /// </summary>
        private static string SanitizeFolderName(string name)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                sanitized.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
            }
            return sanitized.ToString();
        }
    }
}
