using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 双轨面部渲染使用的运行时轨道类型。
    /// </summary>
    public enum FaceRenderTrack
    {
        World = 0,
        Portrait = 1
    }

    /// <summary>
    /// 世界轨/肖像轨的运行时 LOD 等级。
    /// </summary>
    public enum FaceRenderLod
    {
        HighFocus = 0,
        Standard = 1,
        Reduced = 2
    }

    /// <summary>
    /// 记录某个部件在不同朝向上的资源可用性。
    /// 这里不区分 West，因为当前资源规则中 West 通常复用 East。
    /// </summary>
    public sealed class FaceDirectionAvailability
    {
        public bool south = true;
        public bool east = false;
        public bool north = false;

        public FaceDirectionAvailability Clone()
        {
            return new FaceDirectionAvailability
            {
                south = south,
                east = east,
                north = north
            };
        }
    }

    /// <summary>
    /// LayeredDynamic 运行时缓存中使用的部件键。
    /// 将 partType + side 组合为一个可直接作为 Dictionary Key 的值类型。
    /// </summary>
    public struct LayeredFacePartRuntimeKey : IEquatable<LayeredFacePartRuntimeKey>
    {
        public LayeredFacePartType partType;
        public LayeredFacePartSide side;

        public LayeredFacePartRuntimeKey(LayeredFacePartType partType, LayeredFacePartSide side)
        {
            this.partType = partType;
            this.side = PawnFaceConfig.NormalizePartSide(partType, side);
        }

        public bool Equals(LayeredFacePartRuntimeKey other)
        {
            return partType == other.partType
                && side == other.side;
        }

        public override bool Equals(object? obj)
        {
            return obj is LayeredFacePartRuntimeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)partType * 397) ^ (int)side;
            }
        }

        /// <summary>
        /// 枚举运行时缓存查询的候选 key。
        /// side 查询时：先精确 side，再回退 unsided；
        /// 默认查询时：先 unsided，再回退任意 sided（Left/Right）。
        /// </summary>
        public static IEnumerable<LayeredFacePartRuntimeKey> EnumerateLookupKeys(
            LayeredFacePartType partType,
            LayeredFacePartSide side)
        {
            LayeredFacePartRuntimeKey requestedKey = new LayeredFacePartRuntimeKey(partType, side);
            yield return requestedKey;

            if (requestedKey.side != LayeredFacePartSide.None)
            {
                yield return new LayeredFacePartRuntimeKey(partType, LayeredFacePartSide.None);
                yield break;
            }

            if (PawnFaceConfig.SupportsSideSpecificParts(partType))
            {
                yield return new LayeredFacePartRuntimeKey(partType, LayeredFacePartSide.Left);
                yield return new LayeredFacePartRuntimeKey(partType, LayeredFacePartSide.Right);
            }
        }
    }

    /// <summary>
    /// 表达式到运行时资源路径的缓存映射。
    /// worldPath 用于世界轨轻量渲染；
    /// portraitPartPaths 用于肖像轨分层路径快速查找。
    /// </summary>
    public sealed class FaceExpressionRuntimeCache
    {
        public ExpressionType expression = ExpressionType.Neutral;
        public string worldPath = string.Empty;
        public Dictionary<LayeredFacePartRuntimeKey, string> portraitPartPaths = new Dictionary<LayeredFacePartRuntimeKey, string>();
        public Dictionary<LayeredFacePartRuntimeKey, FaceDirectionAvailability> portraitPartDirections = new Dictionary<LayeredFacePartRuntimeKey, FaceDirectionAvailability>();
        public Dictionary<string, string> portraitOverlayPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, FaceDirectionAvailability> portraitOverlayDirections = new Dictionary<string, FaceDirectionAvailability>(StringComparer.OrdinalIgnoreCase);

        public void SetPortraitPartPath(
            LayeredFacePartType partType,
            string path,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            LayeredFacePartRuntimeKey key = new LayeredFacePartRuntimeKey(partType, side);
            if (string.IsNullOrWhiteSpace(path))
            {
                portraitPartPaths.Remove(key);
                portraitPartDirections.Remove(key);
                return;
            }

            portraitPartPaths[key] = path;
        }

        public void SetPortraitPartDirectionAvailability(
            LayeredFacePartType partType,
            FaceDirectionAvailability availability,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            LayeredFacePartRuntimeKey key = new LayeredFacePartRuntimeKey(partType, side);
            if (availability == null)
            {
                portraitPartDirections.Remove(key);
                return;
            }

            portraitPartDirections[key] = availability.Clone();
        }

        public bool TryGetPortraitPartPath(LayeredFacePartType partType, out string path)
        {
            return TryGetPortraitPartPath(partType, LayeredFacePartSide.None, out path);
        }

        public bool TryGetPortraitPartPath(
            LayeredFacePartType partType,
            LayeredFacePartSide side,
            out string path)
        {
            path = string.Empty;
            if (portraitPartPaths == null || portraitPartPaths.Count == 0)
                return false;

            foreach (LayeredFacePartRuntimeKey key in LayeredFacePartRuntimeKey.EnumerateLookupKeys(partType, side))
            {
                if (portraitPartPaths.TryGetValue(key, out string candidatePath)
                    && !string.IsNullOrWhiteSpace(candidatePath))
                {
                    path = candidatePath;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetPortraitPartDirectionAvailability(
            LayeredFacePartType partType,
            LayeredFacePartSide side,
            out FaceDirectionAvailability? availability)
        {
            availability = null;
            if (portraitPartDirections == null || portraitPartDirections.Count == 0)
                return false;

            foreach (LayeredFacePartRuntimeKey key in LayeredFacePartRuntimeKey.EnumerateLookupKeys(partType, side))
            {
                if (portraitPartDirections.TryGetValue(key, out FaceDirectionAvailability candidate)
                    && candidate != null)
                {
                    availability = candidate;
                    return true;
                }
            }

            return false;
        }

        public void SetPortraitOverlayPath(string overlayId, string path)
        {
            string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
            if (string.IsNullOrWhiteSpace(path))
            {
                portraitOverlayPaths.Remove(normalizedOverlayId);
                portraitOverlayDirections.Remove(normalizedOverlayId);
                return;
            }

            portraitOverlayPaths[normalizedOverlayId] = path;
        }

        public void SetPortraitOverlayDirectionAvailability(string overlayId, FaceDirectionAvailability availability)
        {
            string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
            if (availability == null)
            {
                portraitOverlayDirections.Remove(normalizedOverlayId);
                return;
            }

            portraitOverlayDirections[normalizedOverlayId] = availability.Clone();
        }

        public bool TryGetPortraitOverlayPath(string overlayId, out string path)
        {
            path = string.Empty;
            if (portraitOverlayPaths == null || portraitOverlayPaths.Count == 0)
                return false;

            return portraitOverlayPaths.TryGetValue(PawnFaceConfig.NormalizeOverlayId(overlayId), out path)
                && !string.IsNullOrWhiteSpace(path);
        }

        public bool TryGetPortraitOverlayDirectionAvailability(string overlayId, out FaceDirectionAvailability? availability)
        {
            availability = null;
            if (portraitOverlayDirections == null || portraitOverlayDirections.Count == 0)
                return false;

            return portraitOverlayDirections.TryGetValue(PawnFaceConfig.NormalizeOverlayId(overlayId), out availability)
                && availability != null;
        }

        public FaceExpressionRuntimeCache Clone()
        {
            Dictionary<LayeredFacePartRuntimeKey, FaceDirectionAvailability> clonedPartDirections = new Dictionary<LayeredFacePartRuntimeKey, FaceDirectionAvailability>();
            foreach (KeyValuePair<LayeredFacePartRuntimeKey, FaceDirectionAvailability> pair in portraitPartDirections)
            {
                clonedPartDirections[pair.Key] = pair.Value?.Clone() ?? new FaceDirectionAvailability();
            }

            Dictionary<string, FaceDirectionAvailability> clonedOverlayDirections = new Dictionary<string, FaceDirectionAvailability>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, FaceDirectionAvailability> pair in portraitOverlayDirections)
            {
                clonedOverlayDirections[pair.Key] = pair.Value?.Clone() ?? new FaceDirectionAvailability();
            }

            return new FaceExpressionRuntimeCache
            {
                expression = expression,
                worldPath = worldPath ?? string.Empty,
                portraitPartPaths = new Dictionary<LayeredFacePartRuntimeKey, string>(portraitPartPaths),
                portraitPartDirections = clonedPartDirections,
                portraitOverlayPaths = new Dictionary<string, string>(portraitOverlayPaths, StringComparer.OrdinalIgnoreCase),
                portraitOverlayDirections = clonedOverlayDirections
            };
        }
    }

    /// <summary>
    /// 眼睛方向运行时缓存。
    /// 供旧 EyeDirection 覆盖层在肖像轨中读取，避免运行时直接反复查 faceConfig。
    /// </summary>
    public sealed class FaceEyeDirectionRuntimeData
    {
        public bool enabled = false;
        public float upperLidMoveDown = 0.0044f;

        public string texCenter = string.Empty;
        public string texLeft = string.Empty;
        public string texRight = string.Empty;
        public string texUp = string.Empty;
        public string texDown = string.Empty;

        public string GetTexPath(EyeDirection direction)
        {
            string? path = direction switch
            {
                EyeDirection.Left => texLeft,
                EyeDirection.Right => texRight,
                EyeDirection.Up => texUp,
                EyeDirection.Down => texDown,
                _ => texCenter
            };

            if (string.IsNullOrEmpty(path) && direction != EyeDirection.Center)
                path = texCenter;

            return path ?? string.Empty;
        }

        public bool HasAnyTex()
        {
            return !string.IsNullOrEmpty(texCenter)
                || !string.IsNullOrEmpty(texLeft)
                || !string.IsNullOrEmpty(texRight)
                || !string.IsNullOrEmpty(texUp)
                || !string.IsNullOrEmpty(texDown);
        }

        public FaceEyeDirectionRuntimeData Clone()
        {
            return new FaceEyeDirectionRuntimeData
            {
                enabled = enabled,
                upperLidMoveDown = upperLidMoveDown,
                texCenter = texCenter ?? string.Empty,
                texLeft = texLeft ?? string.Empty,
                texRight = texRight ?? string.Empty,
                texUp = texUp ?? string.Empty,
                texDown = texDown ?? string.Empty
            };
        }
    }

    /// <summary>
    /// 世界轨运行时描述。
    /// 第一阶段以 FullFaceSwap+ 保底实现为主，因此保留主贴图路径与基础参数开关。
    /// </summary>
    public sealed class FaceWorldTrackData
    {
        /// <summary>世界轨默认主路径（通常为 Base 或整脸输出）。</summary>
        public string defaultPath = string.Empty;

        /// <summary>世界轨是否允许使用轻量 Blink 表现。</summary>
        public bool supportsBlink = true;

        /// <summary>世界轨是否允许使用轻量眼球方向变化。</summary>
        public bool supportsEyeDirection = false;

        /// <summary>世界轨是否允许使用轻量嘴部开合。</summary>
        public bool supportsMouthOpen = false;

        /// <summary>世界轨是否允许使用轻量 overlay 强度变化。</summary>
        public bool supportsEmotionOverlay = false;

        /// <summary>世界轨默认更新间隔（tick）。</summary>
        public int defaultUpdateIntervalTicks = 15;

        /// <summary>LOD 对应的更新间隔配置。</summary>
        public Dictionary<FaceRenderLod, int> lodUpdateIntervals = new Dictionary<FaceRenderLod, int>
        {
            { FaceRenderLod.HighFocus, 10 },
            { FaceRenderLod.Standard, 20 },
            { FaceRenderLod.Reduced, 45 }
        };

        /// <summary>表达式缓存，用于世界轨快速命中。</summary>
        public Dictionary<ExpressionType, FaceExpressionRuntimeCache> expressionCaches = new Dictionary<ExpressionType, FaceExpressionRuntimeCache>();

        public FaceWorldTrackData Clone()
        {
            var clone = new FaceWorldTrackData
            {
                defaultPath = defaultPath ?? string.Empty,
                supportsBlink = supportsBlink,
                supportsEyeDirection = supportsEyeDirection,
                supportsMouthOpen = supportsMouthOpen,
                supportsEmotionOverlay = supportsEmotionOverlay,
                defaultUpdateIntervalTicks = defaultUpdateIntervalTicks
            };

            foreach (var pair in lodUpdateIntervals)
                clone.lodUpdateIntervals[pair.Key] = pair.Value;

            foreach (var pair in expressionCaches)
                clone.expressionCaches[pair.Key] = pair.Value?.Clone() ?? new FaceExpressionRuntimeCache();

            return clone;
        }
    }

    /// <summary>
    /// 肖像轨运行时描述。
    /// 保留分层映射、overlay 排序、anchor correction 等高质量渲染所需信息。
    /// </summary>
    public sealed class FacePortraitTrackData
    {
        /// <summary>肖像轨默认基础路径。</summary>
        public string basePath = string.Empty;

        /// <summary>各部件的方向资源可用性缓存。</summary>
        public Dictionary<LayeredFacePartRuntimeKey, FaceDirectionAvailability> directionAvailability
            = new Dictionary<LayeredFacePartRuntimeKey, FaceDirectionAvailability>();

        /// <summary>表达式 -> 分层资源缓存。</summary>
        public Dictionary<ExpressionType, FaceExpressionRuntimeCache> expressionCaches
            = new Dictionary<ExpressionType, FaceExpressionRuntimeCache>();

        /// <summary>Overlay 排序后的 ID 列表。</summary>
        public List<string> orderedOverlayIds = new List<string>();

        /// <summary>Overlay 顺序缓存。</summary>
        public Dictionary<string, int> overlayOrders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>程序动画位移幅度缓存（partType+side -> expression -> amplitude）。</summary>
        public Dictionary<LayeredFacePartRuntimeKey, Dictionary<ExpressionType, float>> motionAmplitudes
            = new Dictionary<LayeredFacePartRuntimeKey, Dictionary<ExpressionType, float>>();

        /// <summary>肖像轨是否允许使用程序化表情补偿。</summary>
        public bool supportsProgrammaticAdjustments = true;

        /// <summary>旧 EyeDirection 覆盖层使用的眼方向资源缓存。</summary>
        public FaceEyeDirectionRuntimeData eyeDirection = new FaceEyeDirectionRuntimeData();

        public void SetDirectionAvailability(
            LayeredFacePartType partType,
            FaceDirectionAvailability availability,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            directionAvailability[new LayeredFacePartRuntimeKey(partType, side)] = availability?.Clone() ?? new FaceDirectionAvailability();
        }

        public FaceDirectionAvailability? GetDirectionAvailability(
            LayeredFacePartType partType,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            if (directionAvailability == null || directionAvailability.Count == 0)
                return null;

            foreach (LayeredFacePartRuntimeKey key in LayeredFacePartRuntimeKey.EnumerateLookupKeys(partType, side))
            {
                if (directionAvailability.TryGetValue(key, out FaceDirectionAvailability? availability)
                    && availability != null)
                {
                    return availability;
                }
            }

            return null;
        }

        public void SetMotionAmplitude(
            LayeredFacePartType partType,
            ExpressionType expression,
            float amplitude,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            LayeredFacePartRuntimeKey key = new LayeredFacePartRuntimeKey(partType, side);
            if (!motionAmplitudes.TryGetValue(key, out Dictionary<ExpressionType, float>? byExpression)
                || byExpression == null)
            {
                byExpression = new Dictionary<ExpressionType, float>();
                motionAmplitudes[key] = byExpression;
            }

            byExpression[expression] = Mathf.Max(0f, amplitude);
        }

        public float GetMotionAmplitude(
            LayeredFacePartType partType,
            ExpressionType expression,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            if (motionAmplitudes == null || motionAmplitudes.Count == 0)
                return 0f;

            foreach (LayeredFacePartRuntimeKey key in LayeredFacePartRuntimeKey.EnumerateLookupKeys(partType, side))
            {
                if (!motionAmplitudes.TryGetValue(key, out Dictionary<ExpressionType, float>? byExpression)
                    || byExpression == null)
                {
                    continue;
                }

                if (byExpression.TryGetValue(expression, out float exact))
                    return exact;

                if (expression != ExpressionType.Neutral
                    && byExpression.TryGetValue(ExpressionType.Neutral, out float neutral))
                {
                    return neutral;
                }
            }

            return 0f;
        }

        public FacePortraitTrackData Clone()
        {
            var clone = new FacePortraitTrackData
            {
                basePath = basePath ?? string.Empty,
                supportsProgrammaticAdjustments = supportsProgrammaticAdjustments,
                eyeDirection = eyeDirection?.Clone() ?? new FaceEyeDirectionRuntimeData()
            };

            foreach (var pair in directionAvailability)
                clone.directionAvailability[pair.Key] = pair.Value?.Clone() ?? new FaceDirectionAvailability();

            foreach (var pair in expressionCaches)
                clone.expressionCaches[pair.Key] = pair.Value?.Clone() ?? new FaceExpressionRuntimeCache();

            clone.orderedOverlayIds = new List<string>(orderedOverlayIds);

            foreach (var pair in overlayOrders)
                clone.overlayOrders[pair.Key] = pair.Value;

            foreach (var partPair in motionAmplitudes)
            {
                var inner = new Dictionary<ExpressionType, float>();
                foreach (var expPair in partPair.Value)
                    inner[expPair.Key] = expPair.Value;

                clone.motionAmplitudes[partPair.Key] = inner;
            }

            return clone;
        }
    }

    /// <summary>
    /// 单个皮肤的面部运行时编译结果。
    /// 当前阶段先作为纯缓存数据容器，后续可扩展 atlas / mask / shader 参数协议。
    /// </summary>
    public sealed class FaceRuntimeCompiledData
    {
        /// <summary>用于识别该缓存归属于哪一个皮肤定义。</summary>
        public string skinDefName = string.Empty;

        /// <summary>用于区分缓存版本，后续可由资源变更或导出流程驱动更新。</summary>
        public string buildStamp = string.Empty;

        /// <summary>该缓存是否基于启用的 faceConfig 生成。</summary>
        public bool faceConfigEnabled = false;

        /// <summary>是否启用了 LayeredDynamic 工作流。</summary>
        public bool isLayeredDynamic = false;

        /// <summary>世界轨缓存。</summary>
        public FaceWorldTrackData worldTrack = new FaceWorldTrackData();

        /// <summary>肖像轨缓存。</summary>
        public FacePortraitTrackData portraitTrack = new FacePortraitTrackData();

        public FaceRuntimeCompiledData Clone()
        {
            return new FaceRuntimeCompiledData
            {
                skinDefName = skinDefName ?? string.Empty,
                buildStamp = buildStamp ?? string.Empty,
                faceConfigEnabled = faceConfigEnabled,
                isLayeredDynamic = isLayeredDynamic,
                worldTrack = worldTrack?.Clone() ?? new FaceWorldTrackData(),
                portraitTrack = portraitTrack?.Clone() ?? new FacePortraitTrackData()
            };
        }
    }

    /// <summary>
    /// Pawn 级面部运行时状态。
    /// 只记录当前状态，不直接持有重资源。
    /// </summary>
    public sealed class FaceRuntimeState
    {
        public FaceRenderTrack currentTrack = FaceRenderTrack.World;
        public FaceRenderLod currentLod = FaceRenderLod.Standard;

        public ExpressionType currentExpression = ExpressionType.Neutral;
        public ExpressionType baseExpressionBeforeBlink = ExpressionType.Neutral;
        public EyeDirection currentEyeDirection = EyeDirection.Center;
        public MouthState currentMouthState = MouthState.Normal;
        public LidState currentLidState = LidState.Normal;
        public BrowState currentBrowState = BrowState.Normal;
        public EmotionOverlayState currentEmotionOverlayState = EmotionOverlayState.None;
        public string currentOverlaySemanticKey = string.Empty;
        public EyeAnimationVariant eyeDirectionRuntimeVariant = EyeAnimationVariant.NeutralOpen;
        public PupilScaleVariant pupilScaleRuntimeVariant = PupilScaleVariant.Neutral;

        // --- 全局程序动画驱动源 (Global Procedural Animation Drive) ---
        /// <summary>当前眨眼的缓动进度 (0=张开, 1=完全闭合，经过 Cos 曲线缓动)</summary>
        public float blinkEased = 0f;
        
        /// <summary>当前视线的二维偏移矢量 (基于实际目标坐标映射)</summary>
        public UnityEngine.Vector2 gazeOffset = UnityEngine.Vector2.zero;
        
        /// <summary>当前呼吸起伏系数 (-1 到 1 的简谐运动)</summary>
        public float breathingPulse = 0f;

        /// <summary>下次允许更新世界轨状态的 Tick。</summary>
        public int nextWorldUpdateTick = 0;

        /// <summary>下次允许更新肖像轨状态的 Tick。</summary>
        public int nextPortraitUpdateTick = 0;

        /// <summary>轨道切换时置位，供渲染入口决定是否刷新。</summary>
        public bool trackDirty = true;

        /// <summary>LOD 变化时置位。</summary>
        public bool lodDirty = true;

        /// <summary>表情状态变化时置位。</summary>
        public bool expressionDirty = true;

        /// <summary>当运行时缓存需要重新获取时置位。</summary>
        public bool compiledDataDirty = true;

        public void MarkAllDirty()
        {
            trackDirty = true;
            lodDirty = true;
            expressionDirty = true;
            compiledDataDirty = true;
        }
    }
}
