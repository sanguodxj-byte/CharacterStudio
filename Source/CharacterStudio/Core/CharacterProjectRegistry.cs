using System;
using System.IO;
using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 启动时从项目角色文件（*.character.xml）恢复运行时角色资产注册。
    /// 让编辑器保存到 Config/CharacterStudio/Skins 的项目角色，在重启与读档后也能继续作为运行时角色配置使用。
    /// </summary>
    public static class CharacterProjectRegistry
    {
        public static int LoadProjectCharactersFromConfig()
        {
            string skinsDir = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Skins");
            if (!Directory.Exists(skinsDir))
                return 0;

            int registeredCount = 0;
            foreach (string characterFilePath in Directory.GetFiles(skinsDir, "*.character.xml", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    CharacterDefinition? definition = CharacterDefinitionXmlUtility.Load(characterFilePath);
                    if (definition == null)
                        continue;

                    string fileName = Path.GetFileNameWithoutExtension(characterFilePath);
                    if (fileName.EndsWith(".character", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = fileName.Substring(0, fileName.Length - ".character".Length);
                    }

                    PawnSkinDef? skinDef = PawnSkinDefRegistry.TryGet(fileName)
                        ?? DefDatabase<PawnSkinDef>.GetNamedSilentFail(fileName);
                    if (skinDef == null)
                    {
                        Log.Warning($"[CharacterStudio] 项目角色文件缺少对应皮肤定义，已跳过注册: {characterFilePath}");
                        continue;
                    }

                    definition.EnsureDefaults(
                        !string.IsNullOrWhiteSpace(definition.defName) ? definition.defName : (skinDef.defName ?? fileName),
                        ResolveRaceDef(skinDef, definition),
                        skinDef.attributes);

                    CharacterRuntimeTriggerConfigUtility.SyncCharacterAssets(skinDef.Clone(), definition);
                    registeredCount++;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 注册项目角色失败，已跳过: {characterFilePath}, {ex.Message}");
                }
            }

            if (registeredCount > 0)
            {
                Log.Message($"[CharacterStudio] 已注册 {registeredCount} 个项目角色运行时资产");
            }

            return registeredCount;
        }

        private static ThingDef ResolveRaceDef(PawnSkinDef skinDef, CharacterDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.raceDefName))
            {
                ThingDef? raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(definition.raceDefName);
                if (raceDef?.race != null)
                    return raceDef;
            }

            if (skinDef.targetRaces != null)
            {
                for (int i = 0; i < skinDef.targetRaces.Count; i++)
                {
                    string raceDefName = skinDef.targetRaces[i];
                    if (string.IsNullOrWhiteSpace(raceDefName))
                        continue;

                    ThingDef? raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
                    if (raceDef?.race != null)
                        return raceDef;
                }
            }

            return ThingDefOf.Human;
        }
    }
}
