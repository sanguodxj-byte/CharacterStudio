using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 导出资产分析辅助工具，统一导出后端与 UI 侧的资源枚举和来源识别逻辑。
    /// </summary>
    public static class ExportAssetUtility
    {
        public static IEnumerable<string> EnumerateTexturePaths(PawnSkinDef? skinDef, IEnumerable<ModularAbilityDef>? abilities, string? geneIconPath = null)
        {
            if (skinDef == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in EnumerateRawTexturePaths(skinDef, abilities, geneIconPath))
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string normalizedPath = path!;
                if (seen.Add(normalizedPath))
                {
                    yield return normalizedPath;
                }
            }
        }

        public static AssetSourceInfo DetectAssetSource(string texPath)
        {
            if (string.IsNullOrWhiteSpace(texPath))
            {
                return new AssetSourceInfo
                {
                    OriginalPath = texPath ?? string.Empty,
                    ResolvedPath = texPath ?? string.Empty,
                    SourceType = AssetSourceType.LocalFile
                };
            }

            var info = new AssetSourceInfo
            {
                OriginalPath = texPath,
                ResolvedPath = texPath
            };

            if (Path.IsPathRooted(texPath))
            {
                info.SourceType = AssetSourceType.LocalFile;
                return info;
            }

            if (ContentFinder<Texture2D>.Get(texPath, false) == null)
            {
                info.SourceType = AssetSourceType.LocalFile;
                return info;
            }

            foreach (var mod in LoadedModManager.RunningMods)
            {
                string modTexPath = Path.Combine(mod.RootDir, "Textures", texPath.Replace('/', Path.DirectorySeparatorChar));
                string[] extensions = { ".png", ".PNG", ".jpg", ".JPG", ".jpeg", ".JPEG" };

                foreach (var ext in extensions)
                {
                    if (!File.Exists(modTexPath + ext))
                    {
                        continue;
                    }

                    if (mod.PackageId.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase))
                    {
                        info.SourceType = AssetSourceType.VanillaContent;
                    }
                    else
                    {
                        info.SourceType = AssetSourceType.ExternalMod;
                        info.SourceModPackageId = mod.PackageId;
                        info.SourceModName = mod.Name;
                    }

                    return info;
                }
            }

            info.SourceType = AssetSourceType.VanillaContent;
            return info;
        }

        public static List<string> BuildSourceTextureSearchPaths(PawnSkinDef? skinDef, IEnumerable<ModularAbilityDef>? abilities, string? geneIconPath = null, IEnumerable<string>? seedPaths = null)
        {
            var searchPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddDirectory(string? directory)
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    string normalizedDirectory = directory!;
                    searchPaths.Add(normalizedDirectory);
                }
            }

            if (seedPaths != null)
            {
                foreach (var seed in seedPaths)
                {
                    AddDirectory(seed);
                }
            }

            foreach (var texPath in EnumerateTexturePaths(skinDef, abilities, geneIconPath))
            {
                if (Path.IsPathRooted(texPath))
                {
                    AddDirectory(Path.GetDirectoryName(texPath));
                }
            }

            AddDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Textures"));
            AddDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textures"));

            return searchPaths.ToList();
        }

        private static IEnumerable<string?> EnumerateRawTexturePaths(PawnSkinDef skinDef, IEnumerable<ModularAbilityDef>? abilities, string? geneIconPath)
        {
            yield return skinDef.previewTexPath;

            if (skinDef.layers != null)
            {
                foreach (var layer in skinDef.layers)
                {
                    if (layer == null)
                    {
                        continue;
                    }

                    yield return layer.texPath;
                    yield return layer.maskTexPath;
                }
            }

            if (skinDef.baseAppearance?.slots != null)
            {
                foreach (var slot in skinDef.baseAppearance.slots)
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    yield return slot.texPath;
                    yield return slot.maskTexPath;
                }
            }

            if (skinDef.faceConfig?.expressions != null)
            {
                foreach (var expression in skinDef.faceConfig.expressions)
                {
                    if (expression != null)
                    {
                        yield return expression.texPath;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(geneIconPath))
            {
                yield return geneIconPath;
            }

            if (abilities == null)
            {
                yield break;
            }

            foreach (var ability in abilities)
            {
                if (ability == null)
                {
                    continue;
                }

                yield return ability.iconPath;
            }
        }
    }
}
