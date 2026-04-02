using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
using Verse;

namespace CharacterStudio.AI
{
    public class CharacterStudioLlmSettings : IExposable
    {
        public const string DefaultSystemPrompt = "你是 Character Studio 的结构化生成助手。你必须严格输出 JSON，不要输出 markdown，不要输出注释，不要输出代码块。";
        public const string DefaultCharacterEditorPrompt = "你在扮演 Character Studio 的角色编辑工具。请尽量复用当前角色上下文，只返回可以直接写回编辑器的数据。若用户要求局部修改，仅修改相关字段，其余字段保持合理兼容。";
        public const string DefaultAbilityEditorPrompt = "你在扮演 Character Studio 的技能编辑工具。请尽量复用当前技能与角色上下文，只返回可以直接写回技能编辑器的数据。技能 defName 应稳定、可读且适合 RimWorld Def 命名。";

        public string baseUrl = "https://api.openai.com/v1";
        public string model = "gpt-4.1-mini";
        public string apiKey = "";
        public float temperature = 0.7f;
        public int maxTokens = 1800;
        public bool enabled = false;
        public string systemPrompt = DefaultSystemPrompt;
        public string characterEditorPrompt = DefaultCharacterEditorPrompt;
        public string abilityEditorPrompt = DefaultAbilityEditorPrompt;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(baseUrl)
            && !string.IsNullOrWhiteSpace(model)
            && !string.IsNullOrWhiteSpace(apiKey);

        public bool IsAvailable => enabled && IsConfigured;

        public string GetMaskedApiKey()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return string.Empty;
            }

            if (apiKey.Length <= 8)
            {
                return new string('*', apiKey.Length);
            }

            return apiKey.Substring(0, 4) + new string('*', apiKey.Length - 8) + apiKey.Substring(apiKey.Length - 4);
        }

        public string GetEffectiveSystemPrompt()
        {
            return string.IsNullOrWhiteSpace(systemPrompt) ? DefaultSystemPrompt : systemPrompt.Trim();
        }

        public string GetEffectiveCharacterEditorPrompt()
        {
            return string.IsNullOrWhiteSpace(characterEditorPrompt) ? DefaultCharacterEditorPrompt : characterEditorPrompt.Trim();
        }

        public string GetEffectiveAbilityEditorPrompt()
        {
            return string.IsNullOrWhiteSpace(abilityEditorPrompt) ? DefaultAbilityEditorPrompt : abilityEditorPrompt.Trim();
        }

        public void ResetPromptTemplates()
        {
            systemPrompt = DefaultSystemPrompt;
            characterEditorPrompt = DefaultCharacterEditorPrompt;
            abilityEditorPrompt = DefaultAbilityEditorPrompt;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref baseUrl, "baseUrl", "https://api.openai.com/v1");
            Scribe_Values.Look(ref model, "model", "gpt-4.1-mini");
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref temperature, "temperature", 0.7f);
            Scribe_Values.Look(ref maxTokens, "maxTokens", 1800);
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Values.Look(ref systemPrompt, "systemPrompt", DefaultSystemPrompt);
            Scribe_Values.Look(ref characterEditorPrompt, "characterEditorPrompt", DefaultCharacterEditorPrompt);
            Scribe_Values.Look(ref abilityEditorPrompt, "abilityEditorPrompt", DefaultAbilityEditorPrompt);
        }
    }

    public class CharacterAttributeProfile
    {
        public string title = "";
        public string factionRole = "";
        public string combatRole = "";
        public string backstorySummary = "";
        public string personality = "";
        public string bodyTypeDefName = "";
        public string headTypeDefName = "";
        public string hairDefName = "";
        public string favoriteColorHex = "#FFFFFF";
        public float biologicalAge = 25f;
        public float chronologicalAge = 25f;
        public float moveSpeedMultiplier = 1f;
        public float meleePower = 1f;
        public float shootingAccuracy = 1f;
        public float armorRating = 0f;
        public float psychicSensitivity = 1f;
        public float marketValue = 2000f;
        public List<string> tags = new List<string>();
        public List<string> keyTraits = new List<string>();
        public List<string> startingApparelDefs = new List<string>();

        public CharacterAttributeProfile Clone()
        {
            return new CharacterAttributeProfile
            {
                title = title,
                factionRole = factionRole,
                combatRole = combatRole,
                backstorySummary = backstorySummary,
                personality = personality,
                bodyTypeDefName = bodyTypeDefName,
                headTypeDefName = headTypeDefName,
                hairDefName = hairDefName,
                favoriteColorHex = favoriteColorHex,
                biologicalAge = biologicalAge,
                chronologicalAge = chronologicalAge,
                moveSpeedMultiplier = moveSpeedMultiplier,
                meleePower = meleePower,
                shootingAccuracy = shootingAccuracy,
                armorRating = armorRating,
                psychicSensitivity = psychicSensitivity,
                marketValue = marketValue,
                tags = new List<string>(tags ?? new List<string>()),
                keyTraits = new List<string>(keyTraits ?? new List<string>()),
                startingApparelDefs = new List<string>(startingApparelDefs ?? new List<string>())
            };
        }
    }

    public class LlmGeneratedCharacterDesign
    {
        public string summary = "";
        public string suggestedDefName = "";
        public string suggestedLabel = "";
        public string suggestedDescription = "";
        public CharacterAttributeProfile attributes = new CharacterAttributeProfile();
        public List<string> hiddenNodePaths = new List<string>();
        public List<string> targetRaceDefs = new List<string>();
        public List<string> visualDirectives = new List<string>();
        public List<ModularAbilityDef> abilities = new List<ModularAbilityDef>();
    }

    public class LlmGenerationEnvelope<T>
    {
        public bool success;
        public string message = "";
        public T? payload;
        public string rawResponse = "";
    }

    public enum LlmGenerationMode
    {
        AbilityOnly,
        CharacterOnly,
        FullDesign
    }
}
