using System;
using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;
using CharacterStudio.Abilities;
using System.Linq;
using System.Collections.Generic;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 自定义图层渲染工作器 — 纹理变体/表情逻辑、方向后缀、帧序列、缓存
    /// </summary>
    public partial class PawnRenderNodeWorker_CustomLayer : PawnRenderNodeWorker
    {
        private bool IsExpressionVisibleForLayer(PawnLayerConfig config, Pawn? pawn, CompPawnSkin? cachedSkinComp = null)
        {
            if (pawn == null)
                return true;

            string expressionName = GetEffectiveExpressionName(pawn, cachedSkinComp);

            if (config.hiddenExpressions != null)
            {
                for (int i = 0; i < config.hiddenExpressions.Length; i++)
                {
                    if (string.Equals(config.hiddenExpressions[i], expressionName, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            if (config.visibleExpressions != null && config.visibleExpressions.Length > 0)
            {
                for (int i = 0; i < config.visibleExpressions.Length; i++)
                {
                    if (string.Equals(config.visibleExpressions[i], expressionName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            return true;
        }

        private bool UsesUnifiedVariantLogic(PawnLayerConfig config)
        {
            return config.variantLogic != LayerVariantLogic.None
                || !string.IsNullOrEmpty(config.variantBaseName)
                || config.useExpressionSuffix
                || config.useEyeDirectionSuffix
                || config.useBlinkSuffix
                || config.useFrameSequence
                || config.hideWhenMissingVariant
                || config.role != LayerRole.Decoration;
        }

        private string? ResolveConfiguredTexPath(PawnLayerConfig config, Pawn? pawn, Rot4 facing, out bool matchedVariant, out bool attemptedVariant, string? basePathOverride = null)
        {
            matchedVariant = false;
            attemptedVariant = false;

            string basePath = !string.IsNullOrEmpty(basePathOverride)
                ? basePathOverride!
                : (!string.IsNullOrEmpty(config.variantBaseName) ? config.variantBaseName : config.texPath);

            if (string.IsNullOrEmpty(basePath))
                return null;

            if (pawn == null)
                return basePath;

            List<string> logicalSuffixes = GetLogicalSuffixCandidates(config, pawn, out bool suppressWhenNoLogicalSuffix);
            if (logicalSuffixes.Count == 0 && suppressWhenNoLogicalSuffix)
                return null;

            string? directionalToken = config.useDirectionalSuffix ? GetDirectionalToken(facing) : null;
            List<string> prefixes = BuildVariantPrefixes(basePath, logicalSuffixes, directionalToken);
            attemptedVariant = prefixes.Any(prefix => !string.Equals(prefix, basePath, StringComparison.Ordinal));

            foreach (string prefix in prefixes)
            {
                bool isBasePrefix = string.Equals(prefix, basePath, StringComparison.Ordinal);

                if (config.useFrameSequence && TryResolveFrameSequencePath(prefix, pawn, out string framePath))
                {
                    matchedVariant = !string.Equals(framePath, basePath, StringComparison.Ordinal);
                    return framePath;
                }

                if (TextureExists(prefix))
                {
                    matchedVariant = !isBasePrefix;
                    return prefix;
                }
            }

            return basePath;
        }

        private List<string> GetLogicalSuffixCandidates(PawnLayerConfig config, Pawn? pawn, out bool suppressWhenNoLogicalSuffix)
        {
            var results = new List<string>();
            suppressWhenNoLogicalSuffix = false;

            if (pawn == null)
                return results;

            // P-PERF: 优先从缓存的渲染树节点获取，避免重复 TryGetComp
            CompPawnSkin? skinComp = _lastTextureResolveCachedSkinComp ?? pawn.TryGetComp<CompPawnSkin>();
            ExpressionType? expression = skinComp != null ? skinComp.GetEffectiveExpression() : TryGetFallbackExpression(pawn);

            switch (config.variantLogic)
            {
                case LayerVariantLogic.ExpressionOnly:
                case LayerVariantLogic.ExpressionAndDirection:
                    AddExpressionSuffixCandidates(results, expression);
                    break;

                case LayerVariantLogic.EyeDirectionOnly:
                    AddUnique(results, (skinComp?.CurEyeDirection ?? EyeDirection.Center).ToString());
                    break;

                case LayerVariantLogic.BlinkOnly:
                    suppressWhenNoLogicalSuffix = true;
                    if (skinComp?.IsBlinkActive() == true)
                        AddUnique(results, "Blink");
                    break;

                case LayerVariantLogic.ChannelState:
                    string? channelState = skinComp?.GetChannelStateSuffix(config.role);
                    if (!string.IsNullOrEmpty(channelState))
                        AddUnique(results, channelState);

                    if (config.role == LayerRole.Emotion)
                        suppressWhenNoLogicalSuffix = true;
                    break;

                case LayerVariantLogic.Sequence:
                    string? sequenceState = skinComp?.GetChannelStateSuffix(config.role);
                    if (!string.IsNullOrEmpty(sequenceState))
                        AddUnique(results, sequenceState);
                    else
                        AddExpressionSuffixCandidates(results, expression);
                    break;
            }

            if (config.useBlinkSuffix && config.variantLogic != LayerVariantLogic.BlinkOnly && skinComp?.IsBlinkActive() == true)
                AddUnique(results, "Blink");

            if (config.useEyeDirectionSuffix && config.variantLogic != LayerVariantLogic.EyeDirectionOnly)
                AddUnique(results, (skinComp?.CurEyeDirection ?? EyeDirection.Center).ToString());

            if (config.useExpressionSuffix
                && config.variantLogic != LayerVariantLogic.ExpressionOnly
                && config.variantLogic != LayerVariantLogic.ExpressionAndDirection)
            {
                AddExpressionSuffixCandidates(results, expression);
            }

            return results;
        }

        private void AddExpressionSuffixCandidates(List<string> results, ExpressionType? expression)
        {
            if (!expression.HasValue)
                return;

            AddUnique(results, expression.Value.ToString());

            switch (expression.Value)
            {
                case ExpressionType.Sleeping:
                case ExpressionType.Dead:
                    AddUnique(results, "Sleep");
                    break;

                case ExpressionType.Angry:
                case ExpressionType.AttackMelee:
                case ExpressionType.AttackRanged:
                case ExpressionType.WaitCombat:
                case ExpressionType.Scared:
                    AddUnique(results, "Angry");
                    break;

                case ExpressionType.Blink:
                    AddUnique(results, "Blink");
                    break;
            }
        }

        private static void AddUnique(List<string> values, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            string nonNullValue = value!;
            if (!values.Contains(nonNullValue))
                values.Add(nonNullValue);
        }

        private ExpressionType? TryGetFallbackExpression(Pawn? pawn)
        {
            if (pawn == null)
                return null;

            if (pawn.Dead)
                return ExpressionType.Dead;

            if (RestUtility.InBed(pawn))
                return ExpressionType.Sleeping;

            if (pawn.Drafted || (pawn.MentalState != null && pawn.MentalState.def.IsAggro))
                return ExpressionType.Angry;

            return null;
        }

        private string GetEffectiveExpressionName(Pawn pawn, CompPawnSkin? cachedSkinComp = null)
        {
            CompPawnSkin? skinComp = cachedSkinComp ?? pawn.TryGetComp<CompPawnSkin>();
            if (skinComp != null)
                return skinComp.GetEffectiveExpression().ToString();

            return (TryGetFallbackExpression(pawn) ?? ExpressionType.Neutral).ToString();
        }

        private List<string> BuildVariantPrefixes(string basePath, List<string> logicalSuffixes, string? directionalToken)
        {
            var prefixes = new List<string>();

            if (logicalSuffixes.Count > 0)
            {
                foreach (string suffix in logicalSuffixes)
                {
                    if (!string.IsNullOrEmpty(directionalToken))
                    {
                        AddUnique(prefixes, AppendVariantToken(AppendVariantToken(basePath, suffix), directionalToken));
                        AddUnique(prefixes, AppendVariantToken(AppendVariantToken(basePath, directionalToken), suffix));
                    }

                    AddUnique(prefixes, AppendVariantToken(basePath, suffix));
                }
            }

            if (!string.IsNullOrEmpty(directionalToken))
                AddUnique(prefixes, AppendVariantToken(basePath, directionalToken));

            AddUnique(prefixes, basePath);
            return prefixes;
        }

        private bool TryResolveFrameSequencePath(string prefix, Pawn pawn, out string resolvedPath)
        {
            resolvedPath = prefix;
            if (string.IsNullOrEmpty(prefix))
                return false;

            if (!frameSequenceCountCache.TryGetValue(prefix, out int frameCount))
            {
                string firstFramePath = AppendVariantToken(prefix, "f0");
                if (!TextureExists(firstFramePath))
                {
                    frameSequenceCountCache[prefix] = 0;
                    return false;
                }

                frameCount = 1;
                for (int i = 1; i < 16; i++)
                {
                    if (TextureExists(AppendVariantToken(prefix, $"f{i}")))
                        frameCount++;
                    else
                        break;
                }

                frameSequenceCountCache[prefix] = frameCount;
            }

            if (frameCount <= 0)
                return false;

            int animTick = pawn.TryGetComp<CompPawnSkin>()?.GetExpressionAnimTick()
                ?? AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(pawn, Find.TickManager?.TicksGame ?? 0);
            int frameIndex = Mathf.Abs(animTick) % frameCount;
            resolvedPath = AppendVariantToken(prefix, $"f{frameIndex}");
            return true;
        }

        private string ResolveDirectionalVariant(string basePath, Rot4 facing)
        {
            if (string.IsNullOrEmpty(basePath))
                return basePath;

            string? directionalToken = GetDirectionalToken(facing);
            if (string.IsNullOrEmpty(directionalToken))
                return basePath;

            string directionalPath = AppendVariantToken(basePath, directionalToken);
            if (TextureExists(directionalPath))
                return directionalPath;

            return basePath;
        }

        private string? GetDirectionalToken(Rot4 facing)
        {
            switch (facing.AsInt)
            {
                case 0:
                    return "north";
                case 1:
                case 3:
                    return "east";
                default:
                    return null;
            }
        }

        private static string AppendVariantToken(string basePath, string? token)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(token))
                return basePath;

            string nonNullToken = token!;
            string cleanToken = nonNullToken.StartsWith("_", StringComparison.Ordinal) ? nonNullToken.Substring(1) : nonNullToken;
            string extension = System.IO.Path.GetExtension(basePath);
            if (!string.IsNullOrEmpty(extension))
            {
                string withoutExtension = basePath.Substring(0, basePath.Length - extension.Length);
                return withoutExtension + "_" + cleanToken + extension;
            }

            return basePath + "_" + cleanToken;
        }

        private string ResolveExpressionVariant(string? basePath, Pawn? pawn, CompPawnSkin? cachedSkinComp = null)
        {
            if (string.IsNullOrEmpty(basePath) || pawn == null)
                return basePath ?? string.Empty;

            string nonNullBasePath = basePath!;

            try
            {
                var skinComp = cachedSkinComp ?? pawn.TryGetComp<CharacterStudio.Core.CompPawnSkin>();
                if (skinComp != null && skinComp.HasActiveSkin)
                {
                    var expr = skinComp.GetEffectiveExpression();

                    if (expr == ExpressionType.Sleeping || expr == ExpressionType.Dead)
                    {
                        string sleepPath = AppendVariantToken(nonNullBasePath, "Sleep");
                        if (TextureExists(sleepPath))
                            return sleepPath;
                    }

                    if (expr == ExpressionType.Angry ||
                        expr == ExpressionType.AttackMelee ||
                        expr == ExpressionType.AttackRanged ||
                        expr == ExpressionType.WaitCombat ||
                        expr == ExpressionType.Scared)
                    {
                        string angryPath = AppendVariantToken(nonNullBasePath, "Angry");
                        if (TextureExists(angryPath))
                            return angryPath;
                    }

                    if (expr == ExpressionType.Blink)
                    {
                        return nonNullBasePath;
                    }

                    return nonNullBasePath;
                }

                if (pawn.jobs?.curDriver != null && RestUtility.InBed(pawn))
                {
                    string sleepPath = AppendVariantToken(nonNullBasePath, "Sleep");
                    if (TextureExists(sleepPath))
                        return sleepPath;
                }

                if (pawn.Drafted || (pawn.MentalState != null && pawn.MentalState.def.IsAggro))
                {
                    string angryPath = AppendVariantToken(nonNullBasePath, "Angry");
                    if (TextureExists(angryPath))
                        return angryPath;
                }
            }
            catch
            {
            }

            return nonNullBasePath;
        }

        private ExpressionType GetCurrentExpressionForPawn(Pawn? pawn, CompPawnSkin? cachedSkinComp = null)
        {
            if (pawn == null)
                return ExpressionType.Neutral;

            CompPawnSkin? skinComp = cachedSkinComp ?? pawn.TryGetComp<CompPawnSkin>();
            if (skinComp != null)
                return skinComp.GetEffectiveExpression();

            return TryGetFallbackExpression(pawn) ?? ExpressionType.Neutral;
        }

        private List<string> GetEyeAnimationVariantTokens(EyeAnimationVariant eyeVariant, ExpressionType expression)
        {
            var results = new List<string>();

            AddUnique(results, eyeVariant.ToString());

            switch (eyeVariant)
            {
                case EyeAnimationVariant.HappyOpen:
                case EyeAnimationVariant.HappySoft:
                case EyeAnimationVariant.HappyClosedPeak:
                    AddUnique(results, "Happy");
                    break;

                case EyeAnimationVariant.WorkFocusCenter:
                case EyeAnimationVariant.WorkFocusDown:
                case EyeAnimationVariant.WorkFocusUp:
                    AddUnique(results, "WorkFocus");
                    AddUnique(results, expression == ExpressionType.Reading ? "Reading" : "Working");
                    break;

                case EyeAnimationVariant.ShockWide:
                    AddUnique(results, "Shock");
                    break;

                case EyeAnimationVariant.ScaredWide:
                case EyeAnimationVariant.ScaredFlinch:
                    AddUnique(results, "Scared");
                    break;

                case EyeAnimationVariant.NeutralOpen:
                case EyeAnimationVariant.NeutralSoft:
                case EyeAnimationVariant.NeutralLookDown:
                case EyeAnimationVariant.NeutralGlance:
                    AddUnique(results, "Neutral");
                    break;

                case EyeAnimationVariant.BlinkClosed:
                    AddUnique(results, "Blink");
                    AddUnique(results, "Close");
                    break;
            }

            switch (expression)
            {
                case ExpressionType.Happy:
                case ExpressionType.Cheerful:
                case ExpressionType.Lovin:
                case ExpressionType.SocialRelax:
                    AddUnique(results, expression.ToString());
                    AddUnique(results, "Happy");
                    break;

                case ExpressionType.Working:
                case ExpressionType.Reading:
                    AddUnique(results, expression.ToString());
                    break;

                case ExpressionType.Shock:
                    AddUnique(results, "Shock");
                    break;

                case ExpressionType.Scared:
                    AddUnique(results, "Scared");
                    break;
            }

            return results;
        }
    }
}