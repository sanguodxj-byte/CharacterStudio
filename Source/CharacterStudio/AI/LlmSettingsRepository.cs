using System;
using System.IO;
using System.Xml;
using Verse;

namespace CharacterStudio.AI
{
    public static class LlmSettingsRepository
    {
        private static CharacterStudioLlmSettings? cachedSettings;

        public static CharacterStudioLlmSettings GetOrLoad()
        {
            if (cachedSettings != null)
            {
                return cachedSettings;
            }

            string path = GetSettingsPath();
            if (!File.Exists(path))
            {
                cachedSettings = new CharacterStudioLlmSettings();
                return cachedSettings;
            }

            try
            {
                var xml = new XmlDocument();
                xml.Load(path);
                XmlNode? root = xml.DocumentElement;
                if (root == null)
                {
                    cachedSettings = new CharacterStudioLlmSettings();
                    return cachedSettings;
                }

                cachedSettings = DirectXmlToObject.ObjectFromXml<CharacterStudioLlmSettings>(root, true);
                return cachedSettings ?? new CharacterStudioLlmSettings();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 加载 LLM 设置失败: {ex}");
                cachedSettings = new CharacterStudioLlmSettings();
                return cachedSettings;
            }
        }

        public static void Save(CharacterStudioLlmSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            string path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? GenFilePaths.ConfigFolderPath);

            try
            {
                DirectXmlSaver.SaveDataObject(settings, path);
                cachedSettings = settings;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 保存 LLM 设置失败: {ex}");
                throw;
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "LlmSettings.xml");
        }
    }
}
