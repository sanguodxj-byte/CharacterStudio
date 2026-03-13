using System;
using CharacterStudio.AI;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// LLM 接入设置窗口。
    /// 提供 OpenAI 兼容接口所需的 Base URL、Model 与 API Key 配置。
    /// </summary>
    public class Dialog_LlmSettings : Window
    {
        private readonly Action<CharacterStudioLlmSettings>? onSaved;
        private readonly CharacterStudioLlmSettings workingCopy;
        private Vector2 scrollPos;
        private string statusMessage = string.Empty;
        private float statusExpireTime;

        public override Vector2 InitialSize => new Vector2(760f, 760f);

        public Dialog_LlmSettings(Action<CharacterStudioLlmSettings>? onSavedCallback = null)
        {
            onSaved = onSavedCallback;
            workingCopy = LlmSettingsRepository.GetOrLoad();
            workingCopy = Clone(workingCopy);
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (statusExpireTime > 0f)
            {
                statusExpireTime -= Time.deltaTime;
                if (statusExpireTime <= 0f)
                {
                    statusMessage = string.Empty;
                }
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "CS_LLM_Settings_Title".Translate());
            Text.Font = GameFont.Small;

            Rect infoRect = new Rect(0f, 36f, inRect.width, 48f);
            Widgets.Label(infoRect, "CS_LLM_Settings_Desc".Translate());

            Rect scrollRect = new Rect(0f, 90f, inRect.width, inRect.height - 140f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, 980f);
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;
            UIHelper.DrawSectionTitle(ref y, width, "CS_LLM_Settings_Section_Connection".Translate());
            UIHelper.DrawPropertyCheckbox(ref y, width, "CS_LLM_Settings_Enabled".Translate(), ref workingCopy.enabled);
            UIHelper.DrawPropertyField(ref y, width, "CS_LLM_Settings_BaseUrl".Translate(), ref workingCopy.baseUrl);
            UIHelper.DrawPropertyField(ref y, width, "CS_LLM_Settings_Model".Translate(), ref workingCopy.model);
            UIHelper.DrawPropertyField(ref y, width, "CS_LLM_Settings_ApiKey".Translate(), ref workingCopy.apiKey);

            UIHelper.DrawSectionTitle(ref y, width, "CS_LLM_Settings_Section_Request".Translate());
            UIHelper.DrawPropertySlider(ref y, width, "CS_LLM_Settings_Temperature".Translate(), ref workingCopy.temperature, 0f, 2f, "F2");
            UIHelper.DrawNumericField(ref y, width, "CS_LLM_Settings_MaxTokens".Translate(), ref workingCopy.maxTokens, 128, 8192);

            string masked = string.IsNullOrEmpty(workingCopy.apiKey)
                ? "CS_LLM_Settings_NotConfigured".Translate()
                : workingCopy.GetMaskedApiKey();
            UIHelper.DrawPropertyLabel(ref y, width, "CS_LLM_Settings_ApiKeyPreview".Translate(), masked);
            UIHelper.DrawPropertyLabel(ref y, width, "CS_LLM_Settings_EndpointPreview".Translate(), PreviewEndpoint());
            UIHelper.DrawPropertyLabel(ref y, width, "CS_LLM_Settings_Status".Translate(), workingCopy.IsConfigured ? "CS_LLM_Settings_Configured".Translate() : "CS_LLM_Settings_NotConfigured".Translate());

            UIHelper.DrawSectionTitle(ref y, width, "CS_LLM_Settings_Section_Prompts".Translate());
            Widgets.Label(new Rect(0f, y, width, 24f), "CS_LLM_Settings_SystemPrompt".Translate());
            workingCopy.systemPrompt = Widgets.TextArea(new Rect(0f, y + 24f, width, 110f), workingCopy.systemPrompt ?? string.Empty);
            y += 144f;

            Widgets.Label(new Rect(0f, y, width, 24f), "CS_LLM_Settings_CharacterEditorPrompt".Translate());
            workingCopy.characterEditorPrompt = Widgets.TextArea(new Rect(0f, y + 24f, width, 130f), workingCopy.characterEditorPrompt ?? string.Empty);
            y += 164f;

            Widgets.Label(new Rect(0f, y, width, 24f), "CS_LLM_Settings_AbilityEditorPrompt".Translate());
            workingCopy.abilityEditorPrompt = Widgets.TextArea(new Rect(0f, y + 24f, width, 130f), workingCopy.abilityEditorPrompt ?? string.Empty);
            y += 164f;

            if (Widgets.ButtonText(new Rect(0f, y, 180f, 28f), "CS_LLM_Settings_ResetPrompts".Translate()))
            {
                workingCopy.ResetPromptTemplates();
                statusMessage = "CS_LLM_Settings_PromptsReset".Translate();
                statusExpireTime = 3f;
            }

            Widgets.EndScrollView();

            Rect statusRect = new Rect(0f, inRect.height - 44f, inRect.width * 0.58f, 28f);
            GUI.color = string.IsNullOrEmpty(statusMessage) ? Color.gray : Color.white;
            Widgets.Label(statusRect, statusMessage);
            GUI.color = Color.white;

            float buttonWidth = 110f;
            float buttonY = inRect.height - 38f;
            Rect cancelRect = new Rect(inRect.width - buttonWidth, buttonY, buttonWidth, 30f);
            Rect saveRect = new Rect(cancelRect.x - buttonWidth - 10f, buttonY, buttonWidth, 30f);

            if (Widgets.ButtonText(saveRect, "CS_Studio_Btn_OK".Translate()))
            {
                Save();
            }

            if (Widgets.ButtonText(cancelRect, "CS_Studio_Btn_Cancel".Translate()))
            {
                Close();
            }
        }

        private void Save()
        {
            try
            {
                NormalizeSettings();
                LlmSettingsRepository.Save(workingCopy);
                onSaved?.Invoke(workingCopy);
                statusMessage = "CS_LLM_Settings_SaveSuccess".Translate();
                statusExpireTime = 3f;
                Close();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 保存 LLM 设置失败: {ex}");
                statusMessage = "CS_LLM_Settings_SaveFailed".Translate(ex.Message);
                statusExpireTime = 5f;
            }
        }

        private void NormalizeSettings()
        {
            workingCopy.baseUrl = (workingCopy.baseUrl ?? string.Empty).Trim();
            workingCopy.model = (workingCopy.model ?? string.Empty).Trim();
            workingCopy.apiKey = (workingCopy.apiKey ?? string.Empty).Trim();
            workingCopy.systemPrompt = (workingCopy.systemPrompt ?? string.Empty).Trim();
            workingCopy.characterEditorPrompt = (workingCopy.characterEditorPrompt ?? string.Empty).Trim();
            workingCopy.abilityEditorPrompt = (workingCopy.abilityEditorPrompt ?? string.Empty).Trim();
            if (workingCopy.maxTokens < 128)
            {
                workingCopy.maxTokens = 128;
            }

            if (string.IsNullOrWhiteSpace(workingCopy.systemPrompt)
                || string.IsNullOrWhiteSpace(workingCopy.characterEditorPrompt)
                || string.IsNullOrWhiteSpace(workingCopy.abilityEditorPrompt))
            {
                workingCopy.ResetPromptTemplates();
            }
        }

        private string PreviewEndpoint()
        {
            string baseUrl = (workingCopy.baseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
            {
                return "-";
            }

            if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return baseUrl;
            }

            if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return baseUrl + "/chat/completions";
            }

            return baseUrl + "/v1/chat/completions";
        }

        private static CharacterStudioLlmSettings Clone(CharacterStudioLlmSettings source)
        {
            return new CharacterStudioLlmSettings
            {
                baseUrl = source.baseUrl,
                model = source.model,
                apiKey = source.apiKey,
                temperature = source.temperature,
                maxTokens = source.maxTokens,
                enabled = source.enabled,
                systemPrompt = source.systemPrompt,
                characterEditorPrompt = source.characterEditorPrompt,
                abilityEditorPrompt = source.abilityEditorPrompt
            };
        }
    }
}
