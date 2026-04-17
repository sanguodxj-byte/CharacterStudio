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
    /// 自定义图层渲染工作器 — 面部纹理路径解析 + 情绪叠加层逻辑 + 替换眼/嘴系统
    /// </summary>
    public partial class PawnRenderNodeWorker_CustomLayer : PawnRenderNodeWorker
    {
        /// <summary>
        /// 解析面部图层的基础纹理路径
        /// </summary>
        private string? ResolveLayeredFacePartBasePath(PawnRenderNode_Custom customNode, Pawn? pawn, Rot4 facing)
        {
            if (!customNode.layeredFacePartType.HasValue || pawn == null)
                return null;

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            PawnFaceConfig? faceConfig = skinComp?.ActiveSkin?.faceConfig;
            FaceRuntimeCompiledData? compiledData = skinComp?.CurrentFaceRuntimeCompiledData;
            FaceRenderTrack currentTrack = skinComp?.CurrentFaceRuntimeState.currentTrack ?? FaceRenderTrack.World;

            if (faceConfig == null || !faceConfig.enabled)
                return null;

            LayeredFacePartType partType = customNode.layeredFacePartType.Value;

            LayeredFacePartSide side = PawnFaceConfig.NormalizePartSide(partType, customNode.layeredFacePartSide);
            ExpressionType expression = GetCurrentExpressionForPawn(pawn, skinComp);
            bool isOverlay = PawnFaceConfig.IsOverlayPart(partType);
            string overlayId = GetEffectiveLayeredOverlayId(customNode, pawn);

            bool isLayeredEyePart = partType == LayeredFacePartType.Eye
                || partType == LayeredFacePartType.Pupil
                || partType == LayeredFacePartType.UpperLid
                || partType == LayeredFacePartType.LowerLid;
            bool shouldDelayBlinkTextureReplacement = ShouldDelayBlinkTextureReplacement(partType, expression, skinComp);
            bool isClosedEyeState = expression == ExpressionType.Blink
                || expression == ExpressionType.Sleeping
                || expression == ExpressionType.Dead;
            bool isSideFacing = facing == Rot4.East || facing == Rot4.West;
            bool shouldPreferExplicitDirectionalPath = isSideFacing
                && (!isLayeredEyePart || !isClosedEyeState);

            if (shouldPreferExplicitDirectionalPath)
            {
                string explicitDirectionalPath = isOverlay
                    ? faceConfig.GetLayeredDirectionalPartPath(partType, expression, overlayId, facing)
                    : faceConfig.GetLayeredDirectionalPartPath(partType, expression, side, facing);
                if (!string.IsNullOrWhiteSpace(explicitDirectionalPath) && !shouldDelayBlinkTextureReplacement)
                {
                    return explicitDirectionalPath;
                }

                ExpressionType needsExpr = CharacterStudio.Core.CompPawnSkin.FaceExpressionStateResolver.ResolveNeedsExpression(pawn);
                if (needsExpr != expression && needsExpr != ExpressionType.Neutral)
                {
                    string needsDirectionalPath = isOverlay
                        ? faceConfig.GetLayeredDirectionalPartPath(partType, needsExpr, overlayId, facing)
                        : faceConfig.GetLayeredDirectionalPartPath(partType, needsExpr, side, facing);
                    if (!string.IsNullOrWhiteSpace(needsDirectionalPath) && !shouldDelayBlinkTextureReplacement)
                    {
                        return needsDirectionalPath;
                    }
                }

                string explicitNeutralDirectionalPath = isOverlay
                    ? faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, overlayId, facing)
                    : faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, side, facing);
                if (!string.IsNullOrWhiteSpace(explicitNeutralDirectionalPath))
                {
                    return explicitNeutralDirectionalPath;
                }
            }

            if (facing == Rot4.North)
            {
                string explicitNorthPath = isOverlay
                    ? faceConfig.GetLayeredDirectionalPartPath(partType, expression, overlayId, facing)
                    : faceConfig.GetLayeredDirectionalPartPath(partType, expression, side, facing);
                if (!string.IsNullOrWhiteSpace(explicitNorthPath) && !shouldDelayBlinkTextureReplacement)
                    return explicitNorthPath;

                ExpressionType needsExpr = CharacterStudio.Core.CompPawnSkin.FaceExpressionStateResolver.ResolveNeedsExpression(pawn);
                if (needsExpr != expression && needsExpr != ExpressionType.Neutral)
                {
                    string needsNorthPath = isOverlay
                        ? faceConfig.GetLayeredDirectionalPartPath(partType, needsExpr, overlayId, facing)
                        : faceConfig.GetLayeredDirectionalPartPath(partType, needsExpr, side, facing);
                    if (!string.IsNullOrWhiteSpace(needsNorthPath) && !shouldDelayBlinkTextureReplacement)
                        return needsNorthPath;
                }

                string neutralNorthPath = isOverlay
                    ? faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, overlayId, facing)
                    : faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, side, facing);
                if (!string.IsNullOrWhiteSpace(neutralNorthPath))
                    return neutralNorthPath;

                return null;
            }

            if (partType == LayeredFacePartType.Eye)
            {
                string eyeNeutralBasePath = faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, side, facing);
                if (string.IsNullOrWhiteSpace(eyeNeutralBasePath))
                    eyeNeutralBasePath = faceConfig.GetAnyDirectionalLayeredPartPath(partType, side, facing);
                string? pairedVariantPath = TryResolveLayeredPairedPartVariantPath(
                    customNode,
                    pawn,
                    eyeNeutralBasePath,
                    expression);
                if (!string.IsNullOrWhiteSpace(pairedVariantPath))
                    return pairedVariantPath;
            }

            string neutralPath = isOverlay
                ? faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, overlayId, facing)
                : faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, side, facing);

            if (!isOverlay
                && PawnFaceConfig.UsesStrictExpressionFallback(partType)
                && string.IsNullOrWhiteSpace(neutralPath))
            {
                neutralPath = faceConfig.GetAnyDirectionalLayeredPartPath(partType, side, facing);
            }

            if (partType == LayeredFacePartType.ReplacementEye && skinComp != null)
            {
                string? replacementEyePath = ResolveReplacementEyePath(faceConfig, skinComp, facing, side);
                if (!string.IsNullOrWhiteSpace(replacementEyePath))
                    return replacementEyePath;

                return null;
            }

            if (partType == LayeredFacePartType.ReplacementMouth && skinComp != null)
            {
                return ResolveReplacementMouthPath(faceConfig, skinComp, facing);
            }

            if (!shouldDelayBlinkTextureReplacement)
            {
                string? compiledPath = ResolveCompiledLayeredFacePartPath(
                    compiledData,
                    currentTrack,
                    partType,
                    expression,
                    overlayId,
                    side,
                    facing);
                if (!string.IsNullOrWhiteSpace(compiledPath))
                    return compiledPath;
            }

            if ((partType == LayeredFacePartType.Eye
                    || partType == LayeredFacePartType.Pupil
                    || partType == LayeredFacePartType.UpperLid
                    || partType == LayeredFacePartType.LowerLid)
                && (((partType != LayeredFacePartType.Eye && partType != LayeredFacePartType.Pupil) && expression == ExpressionType.Blink)
                    || expression == ExpressionType.Sleeping
                    || expression == ExpressionType.Dead))
            {
                if (!shouldDelayBlinkTextureReplacement)
                {
                    LayeredFacePartConfig? closedStatePart = isOverlay
                        ? faceConfig.GetLayeredPartConfig(partType, expression, overlayId)
                        : faceConfig.GetLayeredPartConfig(partType, expression, side);

                    if (closedStatePart != null)
                        return closedStatePart.GetDirectionalTexPath(facing);
                }
            }

            string? eyeVariantPath = TryResolveLayeredEyeAnimationVariantPath(customNode, pawn, neutralPath);
            if (!string.IsNullOrWhiteSpace(eyeVariantPath))
                return eyeVariantPath;

            string? channelVariantPath = TryResolveLayeredChannelVariantPath(customNode, pawn, neutralPath);
            if (!string.IsNullOrWhiteSpace(channelVariantPath))
                return channelVariantPath;

            if (shouldDelayBlinkTextureReplacement)
            {
                if (!string.IsNullOrWhiteSpace(neutralPath))
                    return neutralPath;
            }

            string resolvedPath = isOverlay
                ? faceConfig.GetLayeredDirectionalPartPath(partType, expression, overlayId, facing)
                : faceConfig.GetLayeredDirectionalPartPath(partType, expression, side, facing);
            if (!string.IsNullOrWhiteSpace(resolvedPath))
                return resolvedPath;

            if (!string.IsNullOrWhiteSpace(neutralPath))
                return neutralPath;

            if (!isOverlay && !PawnFaceConfig.UsesStrictExpressionFallback(partType))
            {
                string anyPath = partType == LayeredFacePartType.Base
                    ? faceConfig.GetAnyDirectionalLayeredPartPath(partType, facing)
                    : faceConfig.GetAnyDirectionalLayeredPartPath(partType, side, facing);
                if (!string.IsNullOrWhiteSpace(anyPath))
                    return anyPath;
            }

            if (isOverlay)
            {
                string anyOverlayPath = faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, overlayId, facing);
                if (string.IsNullOrWhiteSpace(anyOverlayPath))
                    anyOverlayPath = faceConfig.GetAnyDirectionalLayeredPartPath(partType, overlayId, facing);
                if (string.IsNullOrWhiteSpace(anyOverlayPath))
                    anyOverlayPath = faceConfig.GetAnyLayeredPartPath(partType, overlayId);
                if (!string.IsNullOrWhiteSpace(anyOverlayPath))
                    return anyOverlayPath;
            }

            return null;
        }

        private string? TryResolveLayeredPairedPartVariantPath(
            PawnRenderNode_Custom customNode,
            Pawn pawn,
            string? neutralPath,
            ExpressionType expression)
        {
            if (string.IsNullOrWhiteSpace(neutralPath))
                return null;

            LayeredFacePartType? partType = customNode.layeredFacePartType;
            if (!partType.HasValue || !IsPairedLayeredFacePart(partType.Value))
                return null;

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            if (skinComp == null)
                return null;

            List<string> sideTokens = GetPairedPartSideTokens(partType.Value, customNode.config, skinComp);
            if (sideTokens.Count == 0)
                return null;

            List<string> expressionTokens = GetPairedPartExpressionTokens(expression, customNode.config, skinComp);

            string resolvedNeutralPath = neutralPath!;
            foreach (string sideToken in sideTokens)
            {
                foreach (string expressionToken in expressionTokens)
                {
                    string sideThenExpressionPath = AppendVariantToken(AppendVariantToken(resolvedNeutralPath, sideToken), expressionToken);
                    if (TextureExists(sideThenExpressionPath))
                        return sideThenExpressionPath;

                    string expressionThenSidePath = AppendVariantToken(AppendVariantToken(resolvedNeutralPath, expressionToken), sideToken);
                    if (TextureExists(expressionThenSidePath))
                        return expressionThenSidePath;
                }

                string sideOnlyPath = AppendVariantToken(resolvedNeutralPath, sideToken);
                if (TextureExists(sideOnlyPath))
                    return sideOnlyPath;
            }

            return null;
        }

        private string? TryResolveLayeredChannelVariantPath(PawnRenderNode_Custom customNode, Pawn pawn, string? neutralPath)
        {
            if (string.IsNullOrWhiteSpace(neutralPath))
                return null;

            string resolvedNeutralPath = neutralPath!;

            LayeredFacePartType? partType = customNode.layeredFacePartType;
            if (!partType.HasValue)
                return null;

            if (!ShouldUseChannelVariantForPart(partType.Value))
                return null;

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            if (skinComp == null)
                return null;

            PawnLayerConfig? config = customNode.config;
            LayerRole role = config?.role ?? GetLayerRoleForLayeredPart(partType.Value);
            string? channelState = skinComp.GetChannelStateSuffix(role);
            if (string.IsNullOrWhiteSpace(channelState))
                return null;

            string variantPath = AppendVariantToken(resolvedNeutralPath, channelState);
            if (TextureExists(variantPath))
                return variantPath;

            return null;
        }

        private string? ResolveReplacementEyePath(PawnFaceConfig faceConfig, CompPawnSkin skinComp, Rot4 facing, LayeredFacePartSide preferredSide)
        {
            foreach (ExpressionType candidate in EnumerateReplacementEyeExpressions(skinComp))
            {
                foreach (LayeredFacePartSide side in EnumerateReplacementEyeSides(preferredSide))
                {
                    string resolved = faceConfig.GetLayeredDirectionalPartPath(
                        LayeredFacePartType.ReplacementEye,
                        candidate,
                        side,
                        facing);

                    if (!string.IsNullOrWhiteSpace(resolved))
                        return resolved;
                }
            }

            return null;
        }

        private string? TryResolveLayeredEyeAnimationVariantPath(PawnRenderNode_Custom customNode, Pawn pawn, string? neutralPath)
        {
            if (string.IsNullOrWhiteSpace(neutralPath))
                return null;

            LayeredFacePartType? partType = customNode.layeredFacePartType;
            if (!partType.HasValue || !IsEyeVariantDrivenLayeredFacePart(partType.Value))
                return null;

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            if (skinComp == null)
                return null;

            if (skinComp.IsBlinkActive())
            {
                if (partType == LayeredFacePartType.Eye)
                {
                    EyeAnimationVariant eyeVariantDuringBlink = skinComp.GetEffectiveEyeAnimationVariant();
                    ExpressionType blinkSemanticExpression = skinComp.CurrentFaceRuntimeState.baseExpressionBeforeBlink;
                    List<string> blinkTokens = GetEyeAnimationVariantTokens(eyeVariantDuringBlink, blinkSemanticExpression);
                    string blinkNeutralPath = neutralPath!;
                    foreach (string token in blinkTokens)
                    {
                        string variantPath = AppendVariantToken(blinkNeutralPath, token);
                        if (TextureExists(variantPath))
                            return variantPath;
                    }

                    string closeHappyPath = AppendVariantToken(AppendVariantToken(blinkNeutralPath, "Close"), "Happy");
                    if (TextureExists(closeHappyPath))
                        return closeHappyPath;

                    string happyClosePath = AppendVariantToken(AppendVariantToken(blinkNeutralPath, "Happy"), "Close");
                    if (TextureExists(happyClosePath))
                        return happyClosePath;
                }

                return null;
            }

            EyeAnimationVariant eyeVariant = skinComp.GetEffectiveEyeAnimationVariant();
            List<string> tokens = GetEyeAnimationVariantTokens(eyeVariant, skinComp.GetEffectiveExpression());
            if (tokens.Count == 0)
                return null;

            string resolvedNeutralPath = neutralPath!;
            foreach (string token in tokens)
            {
                string variantPath = AppendVariantToken(resolvedNeutralPath, token);
                if (TextureExists(variantPath))
                    return variantPath;
            }

            return null;
        }

        private static bool ShouldDelayBlinkTextureReplacement(
            LayeredFacePartType partType,
            ExpressionType expression,
            CompPawnSkin? skinComp)
        {
            if (!IsEyeVariantDrivenLayeredFacePart(partType))
                return false;

            if (expression != ExpressionType.Blink || skinComp == null || !skinComp.IsBlinkActive())
                return false;

            if (partType == LayeredFacePartType.Pupil)
                return true;

            return skinComp.GetBlinkPhase() != BlinkPhase.ShowReplacementEye;
        }

        private static bool IsEyeVariantDrivenLayeredFacePart(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.Pupil:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPairedLayeredFacePart(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Brow:
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.Sclera:
                case LayeredFacePartType.Pupil:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ShouldUseChannelVariantForPart(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Brow:
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.Sclera:
                case LayeredFacePartType.Pupil:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                case LayeredFacePartType.Mouth:
                case LayeredFacePartType.ReplacementMouth:
                case LayeredFacePartType.Blush:
                case LayeredFacePartType.Tear:
                case LayeredFacePartType.Sweat:
                case LayeredFacePartType.Overlay:
                    return true;
                default:
                    return false;
            }
        }

        private static LayerRole GetLayerRoleForLayeredPart(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Brow:
                    return LayerRole.Brow;
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.Sclera:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                    return LayerRole.Lid;
                case LayeredFacePartType.ReplacementEye:
                    return LayerRole.Eye;
                case LayeredFacePartType.Pupil:
                    return LayerRole.Eye;
                case LayeredFacePartType.Mouth:
                case LayeredFacePartType.ReplacementMouth:
                    return LayerRole.Mouth;
                case LayeredFacePartType.Blush:
                case LayeredFacePartType.Tear:
                case LayeredFacePartType.Sweat:
                case LayeredFacePartType.Overlay:
                    return LayerRole.Emotion;
                default:
                    return LayerRole.Decoration;
            }
        }

        private List<string> GetPairedPartSideTokens(
            LayeredFacePartType partType,
            PawnLayerConfig? config,
            CompPawnSkin skinComp)
        {
            var results = new List<string>();

            EyeDirection eyeDirection = skinComp.CurEyeDirection;
            switch (eyeDirection)
            {
                case EyeDirection.Left:
                    AddUnique(results, "East");
                    AddUnique(results, "east");
                    AddUnique(results, "Left");
                    AddUnique(results, "left");
                    break;
                case EyeDirection.Right:
                    AddUnique(results, "West");
                    AddUnique(results, "west");
                    AddUnique(results, "Right");
                    AddUnique(results, "right");
                    break;
                case EyeDirection.Up:
                    AddUnique(results, "Up");
                    AddUnique(results, "up");
                    break;
                case EyeDirection.Down:
                    AddUnique(results, "Down");
                    AddUnique(results, "down");
                    break;
                default:
                    AddUnique(results, "Center");
                    AddUnique(results, "center");
                    AddUnique(results, "Left");
                    AddUnique(results, "left");
                    AddUnique(results, "Right");
                    AddUnique(results, "right");
                    break;
            }

            return results;
        }

        private List<string> GetPairedPartExpressionTokens(
            ExpressionType expression,
            PawnLayerConfig? config,
            CompPawnSkin skinComp)
        {
            var results = new List<string>();

            LayerRole role = config?.role ?? LayerRole.Decoration;
            string? channelState = skinComp.GetChannelStateSuffix(role);
            AddUnique(results, channelState);

            switch (expression)
            {
                case ExpressionType.Blink:
                    AddUnique(results, "Blink");
                    break;
                case ExpressionType.Sleeping:
                case ExpressionType.Dead:
                    AddUnique(results, "Close");
                    AddUnique(results, "Sleep");
                    break;
            }

            return results;
        }

        private string? ResolveCompiledLayeredFacePartPath(
            FaceRuntimeCompiledData? compiledData,
            FaceRenderTrack currentTrack,
            LayeredFacePartType partType,
            ExpressionType expression,
            string overlayId,
            LayeredFacePartSide side,
            Rot4 facing)
        {
            if (compiledData == null)
                return null;

            FaceWorldTrackData? worldTrack = compiledData.worldTrack;
            FacePortraitTrackData? portraitTrack = compiledData.portraitTrack;

            if (currentTrack == FaceRenderTrack.World
                && partType == LayeredFacePartType.Base)
            {
                string worldPath = worldTrack?.defaultPath ?? string.Empty;
                if (worldTrack?.expressionCaches != null
                    && worldTrack.expressionCaches.TryGetValue(expression, out FaceExpressionRuntimeCache? worldCache)
                    && worldCache != null
                    && !string.IsNullOrWhiteSpace(worldCache.worldPath))
                {
                    worldPath = worldCache.worldPath;
                }

                if (!string.IsNullOrWhiteSpace(worldPath))
                    return worldPath;
            }

            if (portraitTrack?.expressionCaches != null
                && portraitTrack.expressionCaches.TryGetValue(expression, out FaceExpressionRuntimeCache? portraitCache)
                && portraitCache != null)
            {
                if (PawnFaceConfig.IsOverlayPart(partType))
                {
                    if (portraitCache.TryGetPortraitOverlayPath(overlayId, out string overlayPath)
                        && !string.IsNullOrWhiteSpace(overlayPath))
                    {
                        if (TryResolveDirectionalCompiledPath(overlayPath, facing, portraitCache.TryGetPortraitOverlayDirectionAvailability(overlayId, out FaceDirectionAvailability? overlayAvailability) ? overlayAvailability : null, out string resolvedOverlayPath))
                            return resolvedOverlayPath;

                        return overlayPath;
                    }
                }
                else
                {
                    if (portraitCache.TryGetPortraitPartPath(partType, side, out string partPath)
                        && !string.IsNullOrWhiteSpace(partPath))
                    {
                        if (TryResolveDirectionalCompiledPath(partPath, facing, portraitCache.TryGetPortraitPartDirectionAvailability(partType, side, out FaceDirectionAvailability? partAvailability) ? partAvailability : null, out string resolvedPartPath))
                            return resolvedPartPath;

                        return partPath;
                    }
                }
            }

            if (partType == LayeredFacePartType.Base
                && portraitTrack != null
                && !string.IsNullOrWhiteSpace(portraitTrack.basePath))
            {
                FaceDirectionAvailability? baseAvailability = portraitTrack.GetDirectionAvailability(LayeredFacePartType.Base);
                if (TryResolveDirectionalCompiledPath(portraitTrack.basePath, facing, baseAvailability, out string resolvedBasePath))
                    return resolvedBasePath;

                return portraitTrack.basePath;
            }

            return null;
        }

        private bool TryResolveDirectionalCompiledPath(
            string basePath,
            Rot4 facing,
            FaceDirectionAvailability? availability,
            out string resolvedPath)
        {
            resolvedPath = basePath;
            if (string.IsNullOrWhiteSpace(basePath))
                return false;

            if (facing == Rot4.North)
            {
                if (availability?.north == true)
                {
                    resolvedPath = ResolveDirectionalVariant(basePath, facing);
                    return true;
                }

                return false;
            }

            if (facing == Rot4.East || facing == Rot4.West)
            {
                if (availability?.east == true)
                {
                    resolvedPath = ResolveDirectionalVariant(basePath, facing);
                    return true;
                }

                return false;
            }

            return false;
        }

        private float ResolveCompiledMotionAmplitude(
            FaceRuntimeCompiledData? compiledData,
            LayeredFacePartType partType,
            ExpressionType expression,
            LayeredFacePartSide side)
        {
            return compiledData?.portraitTrack?.GetMotionAmplitude(partType, expression, side) ?? 0f;
        }

        // ─── 情绪叠加层系统 ───

        private string GetEffectiveLayeredOverlayId(PawnRenderNode_Custom customNode, Pawn? pawn = null)
        {
            string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(customNode.layeredOverlayId);
            if (string.IsNullOrWhiteSpace(normalizedOverlayId))
                return string.Empty;

            return normalizedOverlayId;
        }

        private static bool IsGenericOverlayGroupId(string? overlayId)
        {
            return string.IsNullOrWhiteSpace(overlayId)
                || string.Equals(PawnFaceConfig.NormalizeOverlayId(overlayId), "Overlay", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsOverlayAllowedByExplicitRules(string activeOverlayId, List<string> explicitOverlayIds)
        {
            if (explicitOverlayIds.Count == 0)
                return false;

            // P-PERF: 用 for 循环替代 LINQ .Any(lambda)，避免闭包委托分配
            if (IsGenericOverlayGroupId(activeOverlayId))
            {
                for (int i = 0; i < explicitOverlayIds.Count; i++)
                {
                    if (string.Equals(PawnFaceConfig.NormalizeOverlayId(explicitOverlayIds[i]), "Overlay", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            string normalizedActiveOverlayId = PawnFaceConfig.NormalizeOverlayId(activeOverlayId);
            for (int i = 0; i < explicitOverlayIds.Count; i++)
            {
                if (string.Equals(PawnFaceConfig.NormalizeOverlayId(explicitOverlayIds[i]), normalizedActiveOverlayId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsEmotionOverlayVisualPart(LayeredFacePartType partType)
        {
            return partType == LayeredFacePartType.Blush
                || partType == LayeredFacePartType.Tear
                || partType == LayeredFacePartType.Sweat
                || partType == LayeredFacePartType.Overlay
                || partType == LayeredFacePartType.OverlayTop;
        }

        private string ResolveActiveEmotionOverlayId(PawnRenderNode_Custom customNode, Pawn? pawn = null)
        {
            if (!customNode.layeredFacePartType.HasValue)
                return string.Empty;

            switch (customNode.layeredFacePartType.Value)
            {
                case LayeredFacePartType.Blush:
                    return PawnFaceConfig.NormalizeOverlayId("Blush");
                case LayeredFacePartType.Tear:
                    return PawnFaceConfig.NormalizeOverlayId("Tear");
                case LayeredFacePartType.Sweat:
                    return PawnFaceConfig.NormalizeOverlayId("Sweat");
                case LayeredFacePartType.Overlay:
                case LayeredFacePartType.OverlayTop:
                    return GetEffectiveLayeredOverlayId(customNode, pawn);
                default:
                    return string.Empty;
            }
        }

        private string ResolveExplicitOverlaySemanticKey(PawnFaceConfig faceConfig, CompPawnSkin? skinComp, ExpressionType expression)
        {
            string? mappedOverlayId = null;
            if (skinComp?.CurrentFaceRuntimeState != null)
            {
                mappedOverlayId = skinComp.CurrentFaceRuntimeState.currentOverlaySemanticKey;
            }

            string normalizedMappedOverlayId = PawnFaceConfig.NormalizeOverlayId(mappedOverlayId);
            if (!string.IsNullOrWhiteSpace(normalizedMappedOverlayId))
            {
                // P-PERF: 用 for 循环替代 LINQ .FirstOrDefault/.Any 嵌套闭包，避免委托分配和 new List 分配
                PawnFaceConfig.EmotionOverlayRule? mappedRule = null;
                var rules = faceConfig.emotionOverlayRules;
                for (int r = 0; r < rules.Count; r++)
                {
                    var rule = rules[r];
                    var ids = rule.overlayIds;
                    if (ids == null) continue;
                    for (int j = 0; j < ids.Count; j++)
                    {
                        if (string.Equals(PawnFaceConfig.NormalizeOverlayId(ids[j]), normalizedMappedOverlayId, StringComparison.OrdinalIgnoreCase))
                        {
                            mappedRule = rule;
                            goto MappedRuleFound;
                        }
                    }
                }
                MappedRuleFound:
                if (mappedRule != null && !string.IsNullOrWhiteSpace(mappedRule.semanticKey))
                    return PawnFaceConfig.NormalizeOverlaySemanticKey(mappedRule.semanticKey);
            }

            string runtimeSemanticKey = PawnFaceConfig.NormalizeOverlaySemanticKey(skinComp?.CurrentFaceRuntimeState.currentOverlaySemanticKey);
            if (!string.IsNullOrWhiteSpace(runtimeSemanticKey))
                return runtimeSemanticKey;

            return string.Empty;
        }

        private List<string> ResolveExplicitOverlayIds(PawnFaceConfig faceConfig, CompPawnSkin? skinComp, string? semanticKey, ExpressionType expression)
        {
            string normalizedSemanticKey = PawnFaceConfig.NormalizeOverlaySemanticKey(semanticKey);
            if (string.IsNullOrWhiteSpace(normalizedSemanticKey))
                normalizedSemanticKey = ResolveExplicitOverlaySemanticKey(faceConfig, skinComp, expression);

            if (string.IsNullOrWhiteSpace(normalizedSemanticKey))
                return new List<string>();

            return faceConfig.ResolveOverlayIds(normalizedSemanticKey, expression);
        }

        private bool ShouldSuppressEmotionOverlayRendering(PawnRenderNode_Custom customNode, Pawn? pawn)
        {
            if (pawn == null || !customNode.layeredFacePartType.HasValue)
                return false;

            LayeredFacePartType partType = customNode.layeredFacePartType.Value;
            if (!IsEmotionOverlayVisualPart(partType))
                return false;

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            PawnFaceConfig? faceConfig = skinComp?.ActiveSkin?.faceConfig;
            if (faceConfig == null || !faceConfig.enabled)
                return false;

            ExpressionType expression = skinComp?.GetEffectiveExpression()
                ?? (TryGetFallbackExpression(pawn) ?? ExpressionType.Neutral);
            string activeOverlayId = ResolveActiveEmotionOverlayId(customNode, pawn);

            string semanticKey = ResolveExplicitOverlaySemanticKey(faceConfig, skinComp, expression);
            if (string.IsNullOrWhiteSpace(semanticKey))
                return true;

            List<string> explicitOverlayIds = ResolveExplicitOverlayIds(faceConfig, skinComp, semanticKey, expression);
            return !IsOverlayAllowedByExplicitRules(activeOverlayId, explicitOverlayIds);
        }

        private bool IsOverlayActiveForCurrentSemantic(PawnRenderNode_Custom customNode, Pawn pawn, CompPawnSkin skinComp)
        {
            string overlayId = ResolveActiveEmotionOverlayId(customNode, pawn);
            if (string.IsNullOrWhiteSpace(overlayId))
                return false;

            PawnFaceConfig? faceConfig = skinComp.ActiveSkin?.faceConfig;
            if (faceConfig == null)
                return false;

            ExpressionType expression = skinComp.GetEffectiveExpression();
            string semanticKey = ResolveExplicitOverlaySemanticKey(faceConfig, skinComp, expression);
            if (string.IsNullOrWhiteSpace(semanticKey))
                return false;

            List<string> activeOverlayIds = ResolveExplicitOverlayIds(faceConfig, skinComp, semanticKey, expression);
            return IsOverlayAllowedByExplicitRules(overlayId, activeOverlayIds);
        }

        private string ResolveSemanticOverlayId(PawnFaceConfig? faceConfig, EmotionOverlayState emotionState, ExpressionType expression)
        {
            if (faceConfig != null)
                return faceConfig.ResolveOverlayId(emotionState, expression);

            if (expression == ExpressionType.Sleeping)
                return "Sleep";

            switch (emotionState)
            {
                case EmotionOverlayState.Blush:
                case EmotionOverlayState.Lovin:
                    return "Blush";
                case EmotionOverlayState.Tear:
                    return "Tear";
                case EmotionOverlayState.Gloomy:
                    return "Gloomy";
                case EmotionOverlayState.Sweat:
                    return "Sweat";
                default:
                    return string.Empty;
            }
        }

        private string ResolveOverlayFollowTarget(PawnFaceConfig? faceConfig, string overlayId)
        {
            if (faceConfig?.layeredParts == null)
                return string.Empty;

            // P-PERF: 用 for 循环替代 LINQ .FirstOrDefault(lambda)，避免闭包委托分配
            string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
            var parts = faceConfig.layeredParts;
            LayeredFacePartConfig? part = null;
            for (int i = 0; i < parts.Count; i++)
            {
                var existing = parts[i];
                if (existing == null) continue;
                if (existing.partType != LayeredFacePartType.Overlay && existing.partType != LayeredFacePartType.OverlayTop) continue;
                if (string.Equals(PawnFaceConfig.NormalizeOverlayId(existing.overlayId), normalizedOverlayId, StringComparison.OrdinalIgnoreCase))
                {
                    part = existing;
                    break;
                }
            }
            return (part?.followTarget ?? string.Empty).Trim();
        }

        private bool TryGetFollowSourceTransform(PawnRenderNode_Custom overlayNode, Pawn pawn, LayeredFacePartType sourceType, out float angle, out Vector3 offset)
        {
            angle = 0f;
            offset = Vector3.zero;

            PawnRenderTree? tree = overlayNode.tree;
            if (tree?.rootNode == null)
                return false;

            Queue<PawnRenderNode> queue = new Queue<PawnRenderNode>();
            queue.Enqueue(tree.rootNode);
            while (queue.Count > 0)
            {
                PawnRenderNode node = queue.Dequeue();
                if (node is PawnRenderNode_Custom custom
                    && custom != overlayNode
                    && custom.layeredFacePartType == sourceType)
                {
                    angle = custom.currentProgrammaticAngle;
                    offset = custom.currentProgrammaticOffset;
                    return true;
                }

                if (node.children != null)
                {
                    foreach (PawnRenderNode child in node.children)
                    {
                        if (child != null)
                            queue.Enqueue(child);
                    }
                }
            }

            return false;
        }
    }
}