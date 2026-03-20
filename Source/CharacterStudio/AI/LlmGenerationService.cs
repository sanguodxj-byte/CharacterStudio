using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.AI
{
    public static class LlmGenerationService
    {
        // ─────────────────────────────────────────────
        // 异步请求入口（避免主线程阻塞）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 在后台线程异步生成角色设计。
        /// onComplete 在后台线程回调；调用方负责将结果 marshal 到主线程（如通过队列）。
        /// onError 在后台线程回调，参数为异常信息字符串。
        /// </summary>
        public static void GenerateCharacterDesignAsync(
            CharacterStudioLlmSettings settings,
            string userPrompt,
            PawnSkinDef skin,
            List<ModularAbilityDef> currentAbilities,
            Action<LlmGenerationEnvelope<LlmGeneratedCharacterDesign>> onComplete,
            Action<string> onError)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var result = GenerateCharacterDesign(settings, userPrompt, skin, currentAbilities);
                    onComplete?.Invoke(result);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex.Message);
                }
            });
        }

        /// <summary>
        /// 在后台线程异步生成技能列表。
        /// </summary>
        public static void GenerateAbilitiesAsync(
            CharacterStudioLlmSettings settings,
            string userPrompt,
            PawnSkinDef skin,
            List<ModularAbilityDef> currentAbilities,
            Action<LlmGenerationEnvelope<List<ModularAbilityDef>>> onComplete,
            Action<string> onError)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var result = GenerateAbilities(settings, userPrompt, skin, currentAbilities);
                    onComplete?.Invoke(result);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex.Message);
                }
            });
        }

        // ─────────────────────────────────────────────
        // 同步实现（供 Async 包装器内部调用，或测试用）
        // ─────────────────────────────────────────────

        public static LlmGenerationEnvelope<LlmGeneratedCharacterDesign> GenerateCharacterDesign(
            CharacterStudioLlmSettings settings,
            string userPrompt,
            PawnSkinDef skin,
            List<ModularAbilityDef> currentAbilities)
        {
            string schema = BuildCharacterSchemaPrompt();
            string context = BuildCharacterContext(skin, currentAbilities);
            string editorPrompt = settings.GetEffectiveCharacterEditorPrompt();
            string content = RequestJson(settings, userPrompt, schema, context, editorPrompt);
            var payload = DeserializeJson<LlmGeneratedCharacterDesign>(content) ?? new LlmGeneratedCharacterDesign();

            return new LlmGenerationEnvelope<LlmGeneratedCharacterDesign>
            {
                success = true,
                message = "OK",
                payload = payload,
                rawResponse = content
            };
        }

        public static LlmGenerationEnvelope<List<ModularAbilityDef>> GenerateAbilities(
            CharacterStudioLlmSettings settings,
            string userPrompt,
            PawnSkinDef skin,
            List<ModularAbilityDef> currentAbilities)
        {
            string schema = BuildAbilitySchemaPrompt();
            string context = BuildCharacterContext(skin, currentAbilities);
            string editorPrompt = settings.GetEffectiveAbilityEditorPrompt();
            string content = RequestJson(settings, userPrompt, schema, context, editorPrompt);
            var payload = DeserializeJson<List<ModularAbilityDef>>(content) ?? new List<ModularAbilityDef>();

            return new LlmGenerationEnvelope<List<ModularAbilityDef>>
            {
                success = true,
                message = "OK",
                payload = payload,
                rawResponse = content
            };
        }

        private static string RequestJson(CharacterStudioLlmSettings settings, string userPrompt, string schemaPrompt, string contextPrompt, string editorPrompt)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!settings.IsConfigured)
            {
                throw new InvalidOperationException("LLM 配置未完成，请先填写 Base URL、Model 与 API Key。");
            }

            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                throw new ArgumentException("提示词不能为空", nameof(userPrompt));
            }

            string endpoint = NormalizeChatEndpoint(settings.baseUrl);
            var requestDto = new ChatCompletionRequestDto
            {
                model = settings.model,
                temperature = settings.temperature,
                max_tokens = settings.maxTokens,
                response_format = new ResponseFormatDto { type = "json_object" },
                messages = new List<ChatMessageDto>
                {
                    new ChatMessageDto { role = "system", content = settings.GetEffectiveSystemPrompt() },
                    new ChatMessageDto { role = "system", content = editorPrompt },
                    new ChatMessageDto { role = "system", content = schemaPrompt },
                    new ChatMessageDto { role = "system", content = contextPrompt },
                    new ChatMessageDto { role = "user", content = userPrompt }
                }
            };

            string requestJson = SerializeJson(requestDto);
            string responseJson = PostJson(endpoint, requestJson, settings.apiKey);
            var responseDto = DeserializeJson<ChatCompletionResponseDto>(responseJson);
            string content = responseDto?.choices != null && responseDto.choices.Count > 0
                ? responseDto.choices[0]?.message?.content ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("LLM 未返回可解析内容。");
            }

            return content.Trim();
        }

        private static string BuildCharacterSchemaPrompt()
        {
            return "输出 JSON 对象，字段包含：summary, suggestedDefName, suggestedLabel, suggestedDescription, attributes, hiddenNodePaths, targetRaceDefs, visualDirectives, abilities。" +
                   "attributes 必须包含：title, factionRole, combatRole, backstorySummary, personality, bodyTypeDefName, headTypeDefName, hairDefName, favoriteColorHex, biologicalAge, chronologicalAge, moveSpeedMultiplier, meleePower, shootingAccuracy, armorRating, psychicSensitivity, marketValue, tags, keyTraits, startingApparelDefs。" +
                   "abilities 必须是技能数组，每个技能字段包含：defName, label, description, iconPath, cooldownTicks, warmupTicks, charges, aiCanUse, carrierType, targetType, useRadius, areaCenter, range, radius, projectileDef, effects, runtimeComponents。" +
                   "carrierType 只能是 Self/Target/Projectile。targetType 只能是 Self/Entity/Cell。areaCenter 只能是 Self/Target。effect.type 只能是 Damage/Heal/Buff/Debuff/Summon/Teleport/Control/Terraform。" +
                   "effect 对象字段包含：type, amount, duration, chance, damageDef, hediffDef, summonKind, summonCount, controlMode, controlMoveDistance, terraformMode, terraformThingDef, terraformTerrainDef, terraformSpawnCount。" +
                   "runtimeComponents 对象字段包含：type, enabled, comboWindowTicks, cooldownTicks, jumpDistance, findCellRadius, triggerAbilityEffectsAfterJump, requiredStacks, delayTicks, wave1Radius, wave1Damage, wave2Radius, wave2Damage, wave3Radius, wave3Damage, waveDamageDef。不要输出 schema 之外的说明文字。";
        }

        private static string BuildAbilitySchemaPrompt()
        {
            return "输出 JSON 数组。数组中的每个技能对象字段必须包含：defName, label, description, iconPath, cooldownTicks, warmupTicks, charges, aiCanUse, carrierType, targetType, useRadius, areaCenter, range, radius, projectileDef, effects, runtimeComponents。" +
                   "carrierType 只能是 Self/Target/Projectile。targetType 只能是 Self/Entity/Cell。areaCenter 只能是 Self/Target。" +
                   "effect 对象字段包含：type, amount, duration, chance, damageDef, hediffDef, summonKind, summonCount, controlMode, controlMoveDistance, terraformMode, terraformThingDef, terraformTerrainDef, terraformSpawnCount。" +
                   "runtimeComponents 对象字段包含：type, enabled, comboWindowTicks, cooldownTicks, jumpDistance, findCellRadius, triggerAbilityEffectsAfterJump, requiredStacks, delayTicks, wave1Radius, wave1Damage, wave2Radius, wave2Damage, wave3Radius, wave3Damage, waveDamageDef。" +
                   "严格返回 JSON 数组。";
        }

        private static string BuildCharacterContext(PawnSkinDef skin, List<ModularAbilityDef> currentAbilities)
        {
            var sb = new StringBuilder();
            sb.AppendLine("当前角色编辑上下文：");
            sb.AppendLine($"defName={skin.defName}");
            sb.AppendLine($"label={skin.label}");
            sb.AppendLine($"description={skin.description}");
            sb.AppendLine($"layers={skin.layers?.Count ?? 0}");
            sb.AppendLine($"targetRaces={string.Join(",", skin.targetRaces ?? new List<string>())}");
            sb.AppendLine($"hiddenPaths={string.Join(",", skin.hiddenPaths ?? new List<string>())}");
            sb.AppendLine($"abilities={currentAbilities?.Count ?? 0}");
            return sb.ToString();
        }

        private static string NormalizeChatEndpoint(string baseUrl)
        {
            string normalized = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return normalized + "/chat/completions";
            }

            return normalized + "/v1/chat/completions";
        }

        private static string PostJson(string url, string json, string apiKey)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Headers[HttpRequestHeader.Authorization] = "Bearer " + apiKey;
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;

            byte[] body = Encoding.UTF8.GetBytes(json);
            request.ContentLength = body.Length;

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(body, 0, body.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                string errorBody = string.Empty;
                if (ex.Response != null)
                {
                    using (var stream = ex.Response.GetResponseStream())
                    using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                    {
                        errorBody = reader.ReadToEnd();
                    }
                }

                throw new InvalidOperationException("LLM 请求失败: " + errorBody, ex);
            }
        }

        private static string SerializeJson<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static T? DeserializeJson<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                object? result = serializer.ReadObject(ms);
                if (result is T typed)
                {
                    return typed;
                }
            }

            return default;
        }

        [DataContract]
        private sealed class ChatCompletionRequestDto
        {
            [DataMember(Order = 1)] public string model = "";
            [DataMember(Order = 2)] public List<ChatMessageDto> messages = new List<ChatMessageDto>();
            [DataMember(Order = 3)] public float temperature = 0.7f;
            [DataMember(Order = 4)] public int max_tokens = 1800;
            [DataMember(Order = 5)] public ResponseFormatDto? response_format;
        }

        [DataContract]
        private sealed class ChatMessageDto
        {
            [DataMember(Order = 1)] public string role = "";
            [DataMember(Order = 2)] public string content = "";
        }

        [DataContract]
        private sealed class ResponseFormatDto
        {
            [DataMember(Order = 1)] public string type = "json_object";
        }

        [DataContract]
        private sealed class ChatCompletionResponseDto
        {
            [DataMember(Order = 1)] public List<ChoiceDto>? choices = new List<ChoiceDto>();
        }

        [DataContract]
        private sealed class ChoiceDto
        {
            [DataMember(Order = 1)] public MessageDto? message = new MessageDto();
        }

        [DataContract]
        private sealed class MessageDto
        {
            [DataMember(Order = 1)] public string content = "";
        }
    }
}
