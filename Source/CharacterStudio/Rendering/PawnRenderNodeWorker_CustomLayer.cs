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
    /// 自定义图层渲染工作器
    /// 用于渲染皮肤系统添加的自定义图层
    /// 
    /// 本文件仅包含核心渲染管线入口方法（CanDrawNow / OffsetFor / ScaleFor / RotationFor）
    /// 及其直接依赖的辅助方法。其余逻辑分散在各 partial 文件中：
    ///   .Animation.cs   — 通用图层动画 + 触发式装备动画 + 外部图层动画
    ///   .FaceTransform.cs — 程序化面部变换计算
    ///   .FaceTexture.cs  — 分层面部纹理路径解析 + 覆盖层逻辑
    ///   .Variant.cs      — 变体后缀 / 表情变体 / 帧序列
    ///   .Graphic.cs      — Graphic 获取 / 颜色 / 材质属性 / 缓存
    /// </summary>
    public partial class PawnRenderNodeWorker_CustomLayer : PawnRenderNodeWorker
    {
        private static readonly Dictionary<string, Graphic> externalGraphicCache
            = new Dictionary<string, Graphic>(StringComparer.Ordinal);
        // P-PERF: Queue 跟踪插入顺序，确保 FIFO 淘汰可靠性（Dictionary.Keys 顺序不保证）
        private static readonly Queue<string> externalGraphicEvictionQueue = new Queue<string>();
        private const int MaxExternalGraphicCacheSize = 2048;

        // 避免在每帧重复打印同一节点的缩放回退日志
        private static readonly System.Collections.Generic.HashSet<int> _loggedScaleFallbackNodes = new System.Collections.Generic.HashSet<int>();
        private static readonly Dictionary<string, bool> textureExistsCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static readonly Queue<string> textureExistsEvictionQueue = new Queue<string>();
        private static readonly Dictionary<string, int> frameSequenceCountCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> missingExternalTextureWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // P4: 缓存 Shader.PropertyToID 结果（避免每帧字符串哈希查找）
        private static readonly int CachedColorTwoID = Shader.PropertyToID("_ColorTwo");
        private static readonly int CachedMainTexSTID = Shader.PropertyToID("_MainTex_ST");

        // P6: ContentFinder 图形类型探测结果缓存（避免重复资源查找）
        private static readonly Dictionary<string, Type> graphicTypeProbeCache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<string> graphicTypeProbeEvictionQueue = new Queue<string>();
        private const int MaxGraphicTypeProbeCacheSize = 4096;

        // P2: 方向匹配预解析缓存（避免每帧字符串比较和 Trim 分配）
        [Flags]
        private enum DirectionalFacingFlags : byte
        {
            None  = 0,
            South = 1,
            North = 2,
            East  = 4,
            West  = 8,
            Any   = South | North | East | West,
        }
        private static readonly Dictionary<string, DirectionalFacingFlags> directionalFacingParseCache
            = new Dictionary<string, DirectionalFacingFlags>(StringComparer.OrdinalIgnoreCase);

        private const float ProgrammaticFaceFadeInStep = 0.12f;
        private const float ProgrammaticFaceFadeOutStep = 0.08f;
        private const float ProgrammaticFaceAlphaSnapThreshold = 0.01f;
        private const int MaxTextureExistsCacheSize = 4096;

        // P3: _lastTextureResolveCachedSkinComp 线程本地缓存
        [ThreadStatic]
        private static CompPawnSkin? _lastTextureResolveCachedSkinComp;

        /// <summary>
        /// 判断是否可以绘制
        /// P-PERF: 利用 PawnRenderNode_Custom 中已有的 _cachedCanDrawTick / _cachedCanDrawResult
        /// 在同一 tick、同一 facing 下直接返回缓存结果，跳过全部面部分析。
        /// </summary>
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
                return false;

            // 非自定义节点 → 始终可绘制（is 模式匹配声明 non-nullable 变量）
            if (!(node is PawnRenderNode_Custom customNode))
                return true;
            // 无配置 → 始终可绘制
            if (customNode.config == null)
                return true;

            // P-PERF: CanDrawNow 结果缓存 —— 同一 tick + 同一 facing 不重复计算
            int currentTick = (Current.ProgramState == ProgramState.Playing)
                ? (Find.TickManager?.TicksGame ?? 0)
                : (int)(Time.realtimeSinceStartup * 60f);
            int facingInt = parms.facing.AsInt;
            if (customNode._cachedCanDrawTick == currentTick
                && customNode._cachedCanDrawFacing == facingInt)
            {
                return customNode._cachedCanDrawResult;
            }

            bool result = EvaluateCanDrawNowInternal(customNode, parms);

            customNode._cachedCanDrawTick = currentTick;
            customNode._cachedCanDrawFacing = facingInt;
            customNode._cachedCanDrawResult = result;
            return result;
        }

        /// <summary>
        /// CanDrawNow 核心判定逻辑（已从 CanDrawNow 中提取以便缓存包装）
        /// </summary>
        private bool EvaluateCanDrawNowInternal(PawnRenderNode_Custom customNode, PawnDrawParms parms)
        {
            if (customNode.config == null || !customNode.config.visible)
                return false;

            if (!MatchesDirectionalFacing(customNode.config, parms.facing))
                return false;

            if (!IsTriggeredEquipmentLayerVisible(customNode, parms.pawn))
                return false;

            if (!IsExpressionVisibleForLayer(customNode.config, parms.pawn))
                return false;

            // 统一预览与游戏内的 LayeredDynamic 渲染结果：
            // 这里不再因为 World Track 而直接裁掉局部面部部件，
            // 否则游戏内会与编辑器预览使用两套完全不同的面部表现。
            if (customNode.layeredFacePartType.HasValue)
            {
                if (customNode.layeredFacePartType == LayeredFacePartType.ReplacementMouth && parms.pawn != null)
                {
                    CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
                    PawnFaceConfig? faceConfig = skinComp?.ActiveSkin?.faceConfig;
                    if (skinComp == null || faceConfig == null || faceConfig.workflowMode != FaceWorkflowMode.LayeredDynamic)
                        return false;

                    string? replacementPath = ResolveReplacementMouthPath(faceConfig, skinComp, parms.facing);
                    if (string.IsNullOrWhiteSpace(replacementPath))
                        return false;
                }

                if (customNode.layeredFacePartType == LayeredFacePartType.Mouth && parms.pawn != null)
                {
                    CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
                    PawnFaceConfig? faceConfig = skinComp?.ActiveSkin?.faceConfig;
                    if (skinComp != null
                        && faceConfig != null
                        && faceConfig.workflowMode == FaceWorkflowMode.LayeredDynamic
                        && HasReplacementMouthTextureForCurrentState(faceConfig, skinComp)
                        && skinComp.GetEffectiveExpression() != ExpressionType.Neutral)
                    {
                        return false;
                    }
                }

                EnsureProgrammaticFaceStateUpdated(customNode, parms.pawn);
                if (customNode.targetProgrammaticAlpha <= ProgrammaticFaceAlphaSnapThreshold
                    && customNode.currentProgrammaticAlpha <= ProgrammaticFaceAlphaSnapThreshold)
                {
                    return false;
                }
            }

            return true;
        }

        private static DirectionalFacingFlags FacingToFlag(Rot4 facing)
        {
            if (facing == Rot4.South) return DirectionalFacingFlags.South;
            if (facing == Rot4.North) return DirectionalFacingFlags.North;
            if (facing == Rot4.East)  return DirectionalFacingFlags.East;
            if (facing == Rot4.West)  return DirectionalFacingFlags.West;
            return DirectionalFacingFlags.Any;
        }

        private static DirectionalFacingFlags ParseDirectionalFacing(string raw)
        {
            string trimmed = raw.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return DirectionalFacingFlags.Any;

            // 快捷别名（单次匹配，无分配）
            if (string.Equals(trimmed, "EastWest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "Side", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "Sides", StringComparison.OrdinalIgnoreCase))
                return DirectionalFacingFlags.East | DirectionalFacingFlags.West;

            if (string.Equals(trimmed, "SouthNorth", StringComparison.OrdinalIgnoreCase))
                return DirectionalFacingFlags.South | DirectionalFacingFlags.North;

            // 逗号分隔解析：支持 "South, North" / "South,East" 等
            if (trimmed.Contains(','))
            {
                DirectionalFacingFlags flags = DirectionalFacingFlags.None;
                string[] parts = trimmed.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    if (string.Equals(part, "South", StringComparison.OrdinalIgnoreCase))
                        flags |= DirectionalFacingFlags.South;
                    else if (string.Equals(part, "North", StringComparison.OrdinalIgnoreCase))
                        flags |= DirectionalFacingFlags.North;
                    else if (string.Equals(part, "East", StringComparison.OrdinalIgnoreCase))
                        flags |= DirectionalFacingFlags.East;
                    else if (string.Equals(part, "West", StringComparison.OrdinalIgnoreCase))
                        flags |= DirectionalFacingFlags.West;
                }
                return flags == DirectionalFacingFlags.None ? DirectionalFacingFlags.Any : flags;
            }

            // 单方向
            if (string.Equals(trimmed, "South", StringComparison.OrdinalIgnoreCase))
                return DirectionalFacingFlags.South;
            if (string.Equals(trimmed, "North", StringComparison.OrdinalIgnoreCase))
                return DirectionalFacingFlags.North;
            if (string.Equals(trimmed, "East", StringComparison.OrdinalIgnoreCase))
                return DirectionalFacingFlags.East;
            if (string.Equals(trimmed, "West", StringComparison.OrdinalIgnoreCase))
                return DirectionalFacingFlags.West;

            return DirectionalFacingFlags.Any;
        }

        private static bool MatchesDirectionalFacing(PawnLayerConfig config, Rot4 facing)
        {
            // P2: 使用预解析缓存，避免每帧 Trim() 分配和多次字符串比较
            string raw = config.directionalFacing ?? string.Empty;
            if (!directionalFacingParseCache.TryGetValue(raw, out DirectionalFacingFlags flags))
            {
                flags = ParseDirectionalFacing(raw);
                directionalFacingParseCache[raw] = flags;
            }

            if (flags == DirectionalFacingFlags.Any)
                return true;

            return (flags & FacingToFlag(facing)) != 0;
        }

        /// <summary>
        /// 获取偏移量
        /// 优先从 config 读取偏移值，确保导入的数据被正确应用
        /// 同时支持位移动画
        /// </summary>
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            // base.OffsetFor 已经应用了 node.DebugOffset (即 config.offset)
            Vector3 baseOffset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNode_Custom? customNode = node as PawnRenderNode_Custom;
            
            if (customNode != null && customNode.config != null)
            {
                // 不再重复应用 config.offset
                
                // 应用动画位移偏移
                if ((customNode.config.animAffectsOffset && customNode.config.animationType != LayerAnimationType.None)
                    || customNode.config.useTriggeredEquipmentAnimation)
                {
                    baseOffset += customNode.currentAnimOffset;
                }

                EnsureProgrammaticFaceStateUpdated(customNode, parms.pawn);
                baseOffset += customNode.currentProgrammaticOffset;
                EnsureExternalLayerAnimationUpdated(customNode, parms);
                baseOffset += customNode.currentExternalLayerOffset;
            }

            // 根据朝向应用额外的方向特定偏移
            Rot4 facing = parms.facing;
CompPawnSkin? skinComp = (node is PawnRenderNode_Custom cn) ? cn.GetCachedSkinComp() : parms.pawn?.TryGetComp<CompPawnSkin>();
// 注意：飞行高度偏移现在由 Patch_PawnRenderer 在 GetBodyDrawPos 中全局处理。
// 不再在这里重复累加，否则自定义图层会产生双倍偏移。
/*
if (skinComp != null && skinComp.IsFlightStateActive())
{
    float liftFactor = skinComp.GetFlightLiftFactor01();
    float flightBaseHeight = skinComp.FlightStateHeightFactor * liftFactor;
    baseOffset.z += flightBaseHeight + skinComp.GetFlightHoverOffset();
}
*/

// 根据朝向应用额外的方向特定偏移
            // 侧面朝向（East或West）应用 offsetEast
            if (facing == Rot4.East || facing == Rot4.West)
            {
                Vector3 eastOffset = Vector3.zero;
                bool hasEastOffset = false;

                // 1. 优先从 config 获取
                if (customNode != null && customNode.config != null)
                {
                    // P-PERF: Support independent West offset
                    if (facing == Rot4.West && customNode.config.useWestOffset)
                    {
                        baseOffset += customNode.config.offsetWest;
                        return baseOffset;
                    }

                    eastOffset = customNode.config.offsetEast;
                    hasEastOffset = true;
                }
                // 2. 否则尝试从 RuntimeAssetLoader 获取 (兼容旧逻辑或非 Custom 节点)
                else
                {
                    int nodeId = node.GetHashCode();
                    if (RuntimeAssetLoader.TryGetOffsetEast(nodeId, out Vector3 loaderOffset))
                    {
                        eastOffset = loaderOffset;
                        hasEastOffset = true;
                    }
                }

                if (hasEastOffset && eastOffset != Vector3.zero)
                {
                    // 如果是西面朝向，需要翻转X轴偏移（除非启用了独立的 offsetWest）
                    if (facing == Rot4.West)
                    {
                        eastOffset.x = -eastOffset.x;
                    }
                    baseOffset += eastOffset;
                }
            }
            // 北面朝向应用 offsetNorth
            else if (facing == Rot4.North)
            {
                Vector3 northOffset = Vector3.zero;
                bool hasNorthOffset = false;

                // 1. 优先从 config 获取
                if (customNode != null && customNode.config != null)
                {
                    northOffset = customNode.config.offsetNorth;
                    hasNorthOffset = true;
                }
                // 2. 否则尝试从 RuntimeAssetLoader 获取 (兼容旧逻辑或非 Custom 节点)
                else
                {
                    int nodeId = node.GetHashCode();
                    if (RuntimeAssetLoader.TryGetOffsetNorth(nodeId, out Vector3 loaderOffset))
                    {
                        northOffset = loaderOffset;
                        hasNorthOffset = true;
                    }
                }

                if (hasNorthOffset && northOffset != Vector3.zero)
                {
                    baseOffset += northOffset;
                }
            }

            return baseOffset;
        }

        /// <summary>
        /// 获取缩放
        /// 优先从 config 读取缩放值，确保导入的数据被正确应用
        /// 同时支持呼吸动画缩放
        /// </summary>
        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                Vector2 cfg = GetConfiguredScale(customNode.config, parms.facing);
                float sx = cfg.x <= 0f ? 1f : cfg.x;
                float sy = cfg.y <= 0f ? 1f : cfg.y;

                Vector2 originalDrawSize = node.Props.drawSize;
                node.Props.drawSize = Vector2.one;
                Vector3 raceScale;
                try
                {
                    raceScale = base.ScaleFor(node, parms);
                }
                finally
                {
                    node.Props.drawSize = originalDrawSize;
                }

                Vector3 scale = new Vector3(sx * raceScale.x, 1f, sy * raceScale.z);

                if (customNode.config.animationType == LayerAnimationType.Breathe)
                {
                    scale *= customNode.currentAnimScale;
                }

                EnsureProgrammaticFaceStateUpdated(customNode, parms.pawn);
                scale = new Vector3(
                    scale.x * customNode.currentProgrammaticScale.x,
                    scale.y * customNode.currentProgrammaticScale.y,
                    scale.z * customNode.currentProgrammaticScale.z);

                EnsureExternalLayerAnimationUpdated(customNode, parms);
                scale = new Vector3(
                    scale.x * customNode.currentExternalLayerScale.x,
                    scale.y * customNode.currentExternalLayerScale.y,
                    scale.z * customNode.currentExternalLayerScale.z);

                if (node.debugScale != 1f)
                {
                    scale *= node.debugScale;
                }

                // 仅当没有原版祖先（已通过矩阵层级传递全局缩放）时才自行应用
                if (!Patch_PawnRenderTree.HasAnyVanillaAncestor(node))
                {
                    float globalScale = GetGlobalDrawSizeScale(customNode);
                    if (globalScale != 1f)
                    {
                        scale = new Vector3(scale.x * globalScale, scale.y, scale.z * globalScale);
                    }
                }

                return scale;
            }

            Vector3 baseScale = base.ScaleFor(node, parms);
            if (node.debugScale != 1f)
            {
                baseScale *= node.debugScale;
            }

            return baseScale;
        }

        /// <summary>
        /// 从缓存的 CompPawnSkin 中获取全局 DrawSize 缩放因子。
        /// 返回 1f 表示无缩放。
        /// </summary>
        private static float GetGlobalDrawSizeScale(PawnRenderNode_Custom customNode)
        {
            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            if (skinComp?.ActiveSkin == null)
                return 1f;

            float gs = skinComp.ActiveSkin.globalTextureScale;
            return (float.IsNaN(gs) || float.IsInfinity(gs) || gs <= 0f) ? 1f : gs;
        }

        /// <summary>
        /// 获取旋转
        /// </summary>
        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion baseRot = base.RotationFor(node, parms);
            
            // 添加调试角度偏移
            if (Mathf.Abs(node.debugAngleOffset) > 0.01f)
            {
                baseRot *= Quaternion.Euler(0f, node.debugAngleOffset, 0f);
            }

            // 应用配置旋转与动画旋转
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                var config = customNode.config;

                if (Mathf.Abs(GetConfiguredRotation(config, parms.facing)) > 0.01f)
                {
                    baseRot *= Quaternion.Euler(0f, GetConfiguredRotation(config, parms.facing), 0f);
                }

                if (config.animationType != LayerAnimationType.None)
                {
                    // 计算动画
                    CalculateAnimation(customNode, config);
                    
                    // 应用旋转动画（Breathe 类型不应用旋转）
                    if (config.animationType != LayerAnimationType.Breathe)
                    {
                        float animAngle = customNode.currentAnimAngle;
                        if (Mathf.Abs(animAngle) > 0.01f)
                        {
                            baseRot *= Quaternion.Euler(0f, animAngle, 0f);
                        }
                    }
                }

                ApplyTriggeredEquipmentAnimation(customNode, parms.pawn);
                if (customNode.config.useTriggeredEquipmentAnimation && Mathf.Abs(customNode.currentAnimAngle) > 0.01f)
                {
                    baseRot *= Quaternion.Euler(0f, customNode.currentAnimAngle, 0f);
                }

                EnsureProgrammaticFaceStateUpdated(customNode, parms.pawn);
                if (Mathf.Abs(customNode.currentProgrammaticAngle) > 0.01f)
                {
                    baseRot *= Quaternion.Euler(0f, customNode.currentProgrammaticAngle, 0f);
                }

                EnsureExternalLayerAnimationUpdated(customNode, parms);
                if (Mathf.Abs(customNode.currentExternalLayerAngle) > 0.01f)
                {
                    baseRot *= Quaternion.Euler(0f, customNode.currentExternalLayerAngle, 0f);
                }
            }

            return baseRot;
        }

        private static Vector2 GetConfiguredScale(PawnLayerConfig config, Rot4 facing)
        {
            Vector2 scale = config.scale;
            if (facing == Rot4.North)
                return new Vector2(scale.x * config.scaleNorthMultiplier.x, scale.y * config.scaleNorthMultiplier.y);

            if (facing == Rot4.East)
                return new Vector2(scale.x * config.scaleEastMultiplier.x, scale.y * config.scaleEastMultiplier.y);

            if (facing == Rot4.West)
            {
                if (config.useWestOffset)
                    return new Vector2(scale.x * config.scaleWestMultiplier.x, scale.y * config.scaleWestMultiplier.y);
                else
                    return new Vector2(scale.x * config.scaleEastMultiplier.x, scale.y * config.scaleEastMultiplier.y);
            }

            return scale;
        }

        private static float GetConfiguredRotation(PawnLayerConfig config, Rot4 facing)
        {
            float rotation = config.rotation;
            if (facing == Rot4.North)
                return rotation + config.rotationNorthOffset;

            if (facing == Rot4.East)
                return rotation + config.rotationEastOffset;

            if (facing == Rot4.West)
            {
                if (config.useWestOffset)
                    return rotation + config.rotationWestOffset;
                else
                    return rotation - config.rotationEastOffset;
            }

            return rotation;
        }

        /// <summary>
        /// 获取图层值（用于层级排序）
        /// AltitudeFor 不是 virtual，但 LayerFor 是 virtual
        /// </summary>
        public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
        {
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                // 显式返回用户配置的 drawOrder，确保编辑器内的层级调整绝对生效。
                return customNode.config.drawOrder;
            }
            return base.LayerFor(node, parms);
        }
    }
}