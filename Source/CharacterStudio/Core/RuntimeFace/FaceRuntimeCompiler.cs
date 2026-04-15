using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 面部运行时编译器。
    /// 将 PawnSkinDef / PawnFaceConfig 编译为双轨渲染可直接读取的缓存结构，
    /// 避免运行期在各个 worker 中重复做路径回退、Overlay 排序与部件查找。
    /// </summary>
    public static class FaceRuntimeCompiler
    {
        private static readonly Dictionary<string, FaceRuntimeCompiledData> compiledCache
            = new Dictionary<string, FaceRuntimeCompiledData>(StringComparer.Ordinal);

        private static readonly LayeredFacePartType[] nonOverlayParts = new[]
        {
            LayeredFacePartType.Base,
            LayeredFacePartType.Brow,
            LayeredFacePartType.Eye,
            LayeredFacePartType.Sclera,
            LayeredFacePartType.Pupil,
            LayeredFacePartType.UpperLid,
            LayeredFacePartType.LowerLid,
            LayeredFacePartType.Mouth,
            LayeredFacePartType.Blush,
            LayeredFacePartType.Sweat,
            LayeredFacePartType.Tear
        };

        private static readonly LayeredFacePartSide[] unsidedOnly = new[]
        {
            LayeredFacePartSide.None
        };

        private static readonly LayeredFacePartSide[] pairedSides = new[]
        {
            LayeredFacePartSide.Left,
            LayeredFacePartSide.Right
        };

        /// <summary>
        /// 获取或构建指定皮肤的运行时编译结果。
        /// 当前阶段先做轻量缓存：按皮肤定义内容签名缓存单份编译结果。
        /// </summary>
        public static FaceRuntimeCompiledData GetOrBuild(PawnSkinDef? skin)
        {
            if (skin == null)
                return new FaceRuntimeCompiledData();

            string buildStamp = ComputeBuildStamp(skin);
            string cacheKey = (skin.defName ?? string.Empty) + "|" + buildStamp;

            if (compiledCache.TryGetValue(cacheKey, out FaceRuntimeCompiledData cached) && cached != null)
                return cached;

            FaceRuntimeCompiledData compiled = BuildInternal(skin, buildStamp);
            compiledCache[cacheKey] = compiled;
            return compiled;
        }

        public static void ClearCache()
        {
            compiledCache.Clear();
        }

        private static FaceRuntimeCompiledData BuildInternal(PawnSkinDef skin, string buildStamp)
        {
            PawnFaceConfig? faceConfig = skin.faceConfig;
            bool faceConfigEnabled = faceConfig?.enabled == true;

            var compiled = new FaceRuntimeCompiledData
            {
                skinDefName = skin.defName ?? string.Empty,
                buildStamp = buildStamp,
                faceConfigEnabled = faceConfigEnabled,
                isLayeredDynamic = faceConfig?.workflowMode == FaceWorkflowMode.LayeredDynamic
            };

            if (!faceConfigEnabled || faceConfig == null)
                return compiled;

            CompileWorldTrack(faceConfig, compiled.worldTrack);
            CompilePortraitTrack(faceConfig, compiled.portraitTrack, compiled.worldTrack);
            CompileEyeDirection(faceConfig, compiled.portraitTrack);

            return compiled;
        }

        private static void CompileWorldTrack(PawnFaceConfig faceConfig, FaceWorldTrackData worldTrack)
        {
            worldTrack.defaultPath = ResolveWorldPath(faceConfig, ExpressionType.Neutral);
            if (string.IsNullOrWhiteSpace(worldTrack.defaultPath))
                worldTrack.defaultPath = ResolveAnyWorldPath(faceConfig);

            // 第一阶段能力声明保持保守：
            // - Blink 仅在世界轨确实有独立资源时才视为支持；
            // - 眼球方向 / 嘴型 / 情绪 Overlay 仍默认关闭，避免误用到未裁剪的旧路径。
            worldTrack.supportsBlink = HasDistinctWorldPath(faceConfig, ExpressionType.Blink);
            worldTrack.supportsEyeDirection = false;
            worldTrack.supportsMouthOpen = false;
            worldTrack.supportsEmotionOverlay = false;

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                string worldPath = ResolveWorldPath(faceConfig, expression);
                if (string.IsNullOrWhiteSpace(worldPath))
                    continue;

                worldTrack.expressionCaches[expression] = new FaceExpressionRuntimeCache
                {
                    expression = expression,
                    worldPath = worldPath
                };
            }
        }

        private static void CompilePortraitTrack(
            PawnFaceConfig faceConfig,
            FacePortraitTrackData portraitTrack,
            FaceWorldTrackData worldTrack)
        {
            portraitTrack.basePath = faceConfig.workflowMode == FaceWorkflowMode.LayeredDynamic
                ? ResolveLayeredBasePath(faceConfig, ExpressionType.Neutral)
                : worldTrack.defaultPath;

            if (string.IsNullOrWhiteSpace(portraitTrack.basePath))
                portraitTrack.basePath = worldTrack.defaultPath;

            List<string> orderedOverlayIds = faceConfig.GetOrderedOverlayIds();
            List<string> orderedHairOverlayIds = faceConfig.GetOrderedOverlayIds(LayeredFacePartType.Hair);
            portraitTrack.orderedOverlayIds = orderedOverlayIds;

            foreach (string overlayId in orderedOverlayIds)
            {
                portraitTrack.overlayOrders[overlayId] = faceConfig.GetOverlayOrder(overlayId);
            }

            foreach (string overlayId in orderedHairOverlayIds)
            {
                portraitTrack.overlayOrders[overlayId] = faceConfig.GetOverlayOrder(overlayId, LayeredFacePartType.Hair);
            }

            if (faceConfig.workflowMode == FaceWorkflowMode.LayeredDynamic)
            {
                foreach (LayeredFacePartType partType in nonOverlayParts)
                {
                    foreach (LayeredFacePartSide side in GetCompiledSides(partType))
                    {
                        string referencePath = faceConfig.GetAnyLayeredPartPath(partType, side);
                        LayeredFacePartConfig? referencePartConfig = faceConfig.GetLayeredPartConfig(partType, ExpressionType.Neutral, side)
                            ?? faceConfig.GetLayeredPartConfig(partType, ExpressionType.Blink, side)
                            ?? faceConfig.GetLayeredPartConfig(partType, ExpressionType.Sleeping, side)
                            ?? faceConfig.GetLayeredPartConfig(partType, ExpressionType.Dead, side);
                        if (string.IsNullOrWhiteSpace(referencePath) && referencePartConfig == null)
                            continue;

                        portraitTrack.SetDirectionAvailability(partType, BuildDirectionAvailability(referencePath, referencePartConfig), side);
                    }
                }

                foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
                {
                    var cache = new FaceExpressionRuntimeCache
                    {
                        expression = expression,
                        worldPath = ResolveWorldPath(faceConfig, expression)
                    };

                    foreach (LayeredFacePartType partType in nonOverlayParts)
                    {
                        foreach (LayeredFacePartSide side in GetCompiledSides(partType))
                        {
                            string path = faceConfig.GetLayeredPartPath(partType, expression, side);
                            if (!string.IsNullOrWhiteSpace(path))
                                cache.SetPortraitPartPath(partType, path, side);

                            LayeredFacePartConfig? partConfig = faceConfig.GetLayeredPartConfig(partType, expression, side);
                            if (partConfig != null)
                            {
                                cache.SetPortraitPartDirectionAvailability(partType, BuildDirectionAvailability(path, partConfig), side);
                                partConfig.SyncLegacyMotionAmplitude();
                                AddMotionAmplitude(portraitTrack, partType, expression, partConfig.motionAmplitude, side);
                            }
                        }
                    }

                    foreach (string overlayId in orderedOverlayIds)
                    {
                        string overlayPath = faceConfig.GetLayeredPartPath(LayeredFacePartType.Overlay, expression, overlayId);
                        if (!string.IsNullOrWhiteSpace(overlayPath))
                            cache.SetPortraitOverlayPath(overlayId, overlayPath);

                        LayeredFacePartConfig? overlayConfig = faceConfig.GetLayeredPartConfig(LayeredFacePartType.Overlay, expression, overlayId);
                        if (overlayConfig != null)
                            cache.SetPortraitOverlayDirectionAvailability(overlayId, BuildDirectionAvailability(overlayPath, overlayConfig));
                    }

                    foreach (string overlayId in orderedHairOverlayIds)
                    {
                        string overlayPath = faceConfig.GetLayeredPartPath(LayeredFacePartType.Hair, expression, overlayId);
                        if (!string.IsNullOrWhiteSpace(overlayPath))
                            cache.SetPortraitOverlayPath(overlayId, overlayPath);

                        LayeredFacePartConfig? overlayConfig = faceConfig.GetLayeredPartConfig(LayeredFacePartType.Hair, expression, overlayId);
                        if (overlayConfig != null)
                            cache.SetPortraitOverlayDirectionAvailability(overlayId, BuildDirectionAvailability(overlayPath, overlayConfig));
                    }

                    if (cache.portraitPartPaths.Count > 0
                        || cache.portraitPartDirections.Count > 0
                        || cache.portraitOverlayPaths.Count > 0
                        || cache.portraitOverlayDirections.Count > 0
                        || !string.IsNullOrWhiteSpace(cache.worldPath))
                    {
                        portraitTrack.expressionCaches[expression] = cache;
                    }
                }
            }
            else
            {
                portraitTrack.SetDirectionAvailability(LayeredFacePartType.Base, BuildDirectionAvailability(portraitTrack.basePath));

                foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
                {
                    string path = faceConfig.GetTexPath(expression, 0);
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    var cache = new FaceExpressionRuntimeCache
                    {
                        expression = expression,
                        worldPath = path
                    };
                    cache.SetPortraitPartPath(LayeredFacePartType.Base, path);
                    portraitTrack.expressionCaches[expression] = cache;
                }
            }
        }

        private static IEnumerable<LayeredFacePartSide> GetCompiledSides(LayeredFacePartType partType)
        {
            return PawnFaceConfig.SupportsSideSpecificParts(partType)
                ? pairedSides
                : unsidedOnly;
        }

        private static void AddMotionAmplitude(
            FacePortraitTrackData portraitTrack,
            LayeredFacePartType partType,
            ExpressionType expression,
            float amplitude,
            LayeredFacePartSide side)
        {
            portraitTrack.SetMotionAmplitude(partType, expression, amplitude, side);
        }

        private static void CompileEyeDirection(PawnFaceConfig faceConfig, FacePortraitTrackData portraitTrack)
        {
            PawnEyeDirectionConfig? eyeCfg = faceConfig.eyeDirectionConfig;
            if (eyeCfg == null || !eyeCfg.enabled)
                return;

            FaceEyeDirectionRuntimeData runtimeData = portraitTrack.eyeDirection;
            runtimeData.enabled = true;
            runtimeData.upperLidMoveDown = Mathf.Max(0f, eyeCfg.upperLidMoveDown);

            runtimeData.texCenter = eyeCfg.texCenter ?? string.Empty;
            runtimeData.texLeft = eyeCfg.texLeft ?? string.Empty;
            runtimeData.texRight = eyeCfg.texRight ?? string.Empty;
            runtimeData.texUp = eyeCfg.texUp ?? string.Empty;
            runtimeData.texDown = eyeCfg.texDown ?? string.Empty;

            if (string.IsNullOrWhiteSpace(runtimeData.texCenter))
            {
                runtimeData.texCenter = eyeCfg.GetTexPath(EyeDirection.Center);
            }
        }

        private static string ResolveWorldPath(PawnFaceConfig faceConfig, ExpressionType expression)
        {
            if (faceConfig.workflowMode == FaceWorkflowMode.LayeredDynamic)
            {
                string basePath = ResolveLayeredBasePath(faceConfig, expression);
                if (!string.IsNullOrWhiteSpace(basePath))
                    return basePath;

                return faceConfig.GetAnyLayeredPartPath(LayeredFacePartType.Base);
            }

            return faceConfig.GetTexPath(expression, 0);
        }

        private static string ResolveAnyWorldPath(PawnFaceConfig faceConfig)
        {
            if (faceConfig.workflowMode == FaceWorkflowMode.LayeredDynamic)
                return faceConfig.GetAnyLayeredPartPath(LayeredFacePartType.Base);

            ExpressionTexPath? neutral = faceConfig.GetExpression(ExpressionType.Neutral);
            if (neutral != null)
                return neutral.GetTexPathAtTick(0);

            foreach (ExpressionType expression in Enum.GetValues(typeof(ExpressionType)))
            {
                string path = faceConfig.GetTexPath(expression, 0);
                if (!string.IsNullOrWhiteSpace(path))
                    return path;
            }

            return string.Empty;
        }

        private static string ResolveLayeredBasePath(PawnFaceConfig faceConfig, ExpressionType expression)
        {
            string path = faceConfig.GetLayeredPartPath(LayeredFacePartType.Base, expression);
            if (!string.IsNullOrWhiteSpace(path))
                return path;

            string neutralPath = faceConfig.GetLayeredPartPath(LayeredFacePartType.Base, ExpressionType.Neutral);
            if (!string.IsNullOrWhiteSpace(neutralPath))
                return neutralPath;

            return faceConfig.GetAnyLayeredPartPath(LayeredFacePartType.Base);
        }

        private static bool HasDistinctWorldPath(PawnFaceConfig faceConfig, ExpressionType expression)
        {
            string neutral = ResolveWorldPath(faceConfig, ExpressionType.Neutral);
            string current = ResolveWorldPath(faceConfig, expression);

            if (string.IsNullOrWhiteSpace(current))
                return false;

            if (string.IsNullOrWhiteSpace(neutral))
                return true;

            return !string.Equals(neutral, current, StringComparison.OrdinalIgnoreCase);
        }

        private static FaceDirectionAvailability BuildDirectionAvailability(string basePath)
        {
            return BuildDirectionAvailability(basePath, null);
        }

        private static FaceDirectionAvailability BuildDirectionAvailability(string basePath, LayeredFacePartConfig? partConfig)
        {
            partConfig?.SyncDirectionalTexPathsFromLegacy();

            bool hasSouth = partConfig != null
                ? !string.IsNullOrWhiteSpace(partConfig.texPathSouth)
                : !string.IsNullOrWhiteSpace(basePath);
            bool hasEast = !string.IsNullOrWhiteSpace(partConfig?.texPathEast)
                || (!string.IsNullOrWhiteSpace(basePath) && TextureExists(AppendDirectionalSuffix(basePath, "_east")));
            bool hasNorth = !string.IsNullOrWhiteSpace(partConfig?.texPathNorth)
                || (!string.IsNullOrWhiteSpace(basePath) && TextureExists(AppendDirectionalSuffix(basePath, "_north")));

            return new FaceDirectionAvailability
            {
                south = hasSouth,
                east = hasEast,
                north = hasNorth
            };
        }

        private static string AppendDirectionalSuffix(string basePath, string suffix)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(suffix))
                return basePath;

            string extension = System.IO.Path.GetExtension(basePath);
            if (!string.IsNullOrEmpty(extension))
            {
                string withoutExtension = basePath.Substring(0, basePath.Length - extension.Length);
                return withoutExtension + suffix + extension;
            }

            return basePath + suffix;
        }

        private static bool TextureExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (System.IO.Path.IsPathRooted(path) || path.StartsWith("/"))
                return System.IO.File.Exists(path);

            return ContentFinder<Texture2D>.Get(path, false) != null;
        }

        private static string ComputeBuildStamp(PawnSkinDef skin)
        {
            unchecked
            {
                int hash = 17;
                hash = CombineHash(hash, skin.defName);
                hash = CombineHash(hash, skin.version);

                PawnFaceConfig? faceConfig = skin.faceConfig;
                hash = CombineHash(hash, faceConfig?.enabled == true);
                hash = CombineHash(hash, (int)(faceConfig?.workflowMode ?? FaceWorkflowMode.FullFaceSwap));
                hash = CombineHash(hash, faceConfig?.layeredSourceRoot);

                if (faceConfig?.expressions != null)
                {
                    hash = CombineHash(hash, faceConfig.expressions.Count);
                    foreach (ExpressionTexPath expression in faceConfig.expressions)
                    {
                        if (expression == null)
                            continue;

                        hash = CombineHash(hash, (int)expression.expression);
                        hash = CombineHash(hash, expression.texPath);

                        if (expression.frames != null)
                        {
                            hash = CombineHash(hash, expression.frames.Count);
                            foreach (ExpressionFrame frame in expression.frames)
                            {
                                if (frame == null)
                                    continue;

                                hash = CombineHash(hash, frame.texPath);
                                hash = CombineHash(hash, frame.durationTicks);
                            }
                        }
                    }
                }

                if (faceConfig?.layeredParts != null)
                {
                    hash = CombineHash(hash, faceConfig.layeredParts.Count);
                    foreach (LayeredFacePartConfig part in faceConfig.layeredParts)
                    {
                        if (part == null)
                            continue;

                        hash = CombineHash(hash, (int)part.partType);
                        hash = CombineHash(hash, (int)part.expression);
                        hash = CombineHash(hash, part.texPath);
                        hash = CombineHash(hash, part.texPathSouth);
                        hash = CombineHash(hash, part.texPathEast);
                        hash = CombineHash(hash, part.texPathNorth);
                        hash = CombineHash(hash, part.enabled);
                        hash = CombineHash(hash, (int)part.side);
                        hash = CombineHash(hash, part.overlayId);
                        hash = CombineHash(hash, part.overlayOrder);
                        float resolvedMotionAmplitude = part.motionAmplitude > 0f
                            ? part.motionAmplitude
                            : Mathf.Max(Mathf.Abs(part.anchorCorrection.x), Mathf.Abs(part.anchorCorrection.y));
                        hash = CombineHash(hash, resolvedMotionAmplitude);
                    }
                }

                PawnEyeDirectionConfig? eyeConfig = faceConfig?.eyeDirectionConfig;
                hash = CombineHash(hash, eyeConfig?.enabled == true);
                hash = CombineHash(hash, eyeConfig?.texCenter);
                hash = CombineHash(hash, eyeConfig?.texLeft);
                hash = CombineHash(hash, eyeConfig?.texRight);
                hash = CombineHash(hash, eyeConfig?.texUp);
                hash = CombineHash(hash, eyeConfig?.texDown);
                hash = CombineHash(hash, eyeConfig?.upperLidMoveDown ?? 0f);

                return (skin.version ?? "0") + "-" + hash.ToString("X8");
            }
        }

        private static int CombineHash(int current, string? value)
        {
            return (current * 31) + StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
        }

        private static int CombineHash(int current, bool value)
        {
            return (current * 31) + (value ? 1 : 0);
        }

        private static int CombineHash(int current, int value)
        {
            return (current * 31) + value;
        }

        private static int CombineHash(int current, float value)
        {
            return (current * 31) + value.GetHashCode();
        }
    }
}
