﻿using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
using CharacterStudio.Attributes;
using CharacterStudio.Performance;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 皮肤组件
    /// 附加到 Pawn 上，处理皮肤应用和动态表情逻辑
    /// 技能运行时状态已迁移至 CompCharacterAbilityRuntime
    /// </summary>
    [StaticConstructorOnStartup]
    public partial class CompPawnSkin : ThingComp
    {
        public static event Action<Pawn, PawnSkinDef?, bool, bool, string>? SkinChangedGlobal;

        private PawnSkinDef? activeSkin;
        public PawnSkinDef? CurrentSkinDef => activeSkin;
        public PawnSkinDef? EffectiveSkinDef => activeSkin;

        private string? activeSkinDefName;
        private bool needsRefresh = false;
        private bool activeSkinFromDefaultRaceBinding = false;
        private bool activeSkinPreviewMode = false;
        private string activeSkinApplicationSource = string.Empty;

        // 双轨面部运行时状态（第一阶段：先接入状态层，不直接改动渲染 worker）
        private FaceRuntimeState? faceRuntimeState = null;
        private FaceRuntimeCompiledData? faceRuntimeCompiledData = null;

        // 表情状态
        private readonly FaceExpressionRuntimeState faceExpressionState = new FaceExpressionRuntimeState();
        private ExpressionType curExpression
        {
            get => faceExpressionState.currentExpression;
            set => faceExpressionState.currentExpression = value;
        }

        private readonly FacePreviewOverrideState previewOverrides = new FacePreviewOverrideState();
        private const int BlinkDuration = 10;
        private const int ShockExpressionDuration = 24;

        // 眼睛注视方向
        private readonly EyeDirectionRuntimeState eyeDirectionState = new EyeDirectionRuntimeState();
        private EyeDirection curEyeDirection
        {
            get => eyeDirectionState.currentEyeDirection;
            set => eyeDirectionState.currentEyeDirection = value;
        }

        // ── Ability component access helper ──
        private CompCharacterAbilityRuntime? _cachedAbilityComp;
        private int _cachedAbilityCompPawnId = -1;
        public CompCharacterAbilityRuntime? AbilityComp
        {
            get
            {
                Pawn? pawn = Pawn;
                int pawnId = pawn?.thingIDNumber ?? -1;
                if (_cachedAbilityCompPawnId != pawnId || _cachedAbilityComp == null)
                {
                    _cachedAbilityComp = pawn?.GetComp<CompCharacterAbilityRuntime>();
                    _cachedAbilityCompPawnId = pawnId;
                }
                return _cachedAbilityComp;
            }
        }

        // ── Shield visual rendering cache (for CompPawnSkin.PostDraw) ──
        private static Material? cachedDefaultShieldBubbleMat;
        private static Material? DefaultShieldBubbleMat
        {
            get
            {
                if (cachedDefaultShieldBubbleMat != null)
                    return cachedDefaultShieldBubbleMat;

                if (!Rendering.RuntimeAssetLoader.IsMainThread())
                    return null;

                cachedDefaultShieldBubbleMat = MaterialPool.MatFrom("Things/Pawn/Effects/Shield", ShaderDatabase.Transparent);
                return cachedDefaultShieldBubbleMat;
            }
        }
        private static Material? CachedFlightShadowMat;
        private static Material? FlightShadowMat
        {
            get
            {
                if (!Rendering.RuntimeAssetLoader.IsMainThread())
                    return CachedFlightShadowMat;

                if (CachedFlightShadowMat == null)
                    CachedFlightShadowMat = DefDatabase<ThingDef>.GetNamedSilentFail("PawnFlyer")?.pawnFlyer?.ShadowMaterial;
                return CachedFlightShadowMat;
            }
        }

        public Pawn? Pawn => parent as Pawn;

        public override void CompTick()
        {
            base.CompTick();
            Pawn? pawn = Pawn;
            if (pawn == null || !pawn.Spawned) return;

            // Ability ticking (forced move, face override) is now handled by CompCharacterAbilityRuntime.CompTick()
            FaceRuntimeRefreshCoordinator.FlushDeferredRefresh(this);

            if (FaceRuntimeActivationGuard.IsFaceRuntimeEnabled(this))
            {
                FaceRuntimeTickCoordinator.Tick(this, pawn);
            }
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            // Delegate shield absorption to ability runtime component
            var abilityComp = AbilityComp;
            if (abilityComp != null)
            {
                absorbed = abilityComp.TryAbsorbShieldDamage(ref dinfo);
            }
            else
            {
                absorbed = false;
            }

            if (!absorbed)
            {
                TriggerShockExpression(dinfo);
            }
        }

        private void TriggerShockExpression(DamageInfo dinfo)
        {
            Pawn? pawn = Pawn;
            if (pawn == null || !pawn.Spawned)
                return;

            if (pawn.Dead || pawn.Downed || RestUtility.InBed(pawn))
                return;

            float incomingDamage = Mathf.Max(0f, dinfo.Amount);
            if (incomingDamage <= 0.001f)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            faceExpressionState.TriggerShock(now, ShockExpressionDuration);
            faceExpressionState.ClearBlink();
            faceExpressionState.ResetAnimatedFrameTracking();
            MarkFaceGraphicDirty();
        }

        public int GetExpressionAnimTick() => faceExpressionState.expressionAnimTick;

        public override void PostDraw()
        {
            base.PostDraw();

            Pawn? pawn = Pawn;
            if (pawn == null) return;

            // Shield rendering reads state from CompCharacterAbilityRuntime
            var abilityComp = AbilityComp;
            if (abilityComp != null && abilityComp.IsAttachedShieldVisualActive())
            {
                float energyPercent = (abilityComp.ShieldRemainingDamage > 0)
                    ? Mathf.Clamp01(abilityComp.ShieldRemainingDamage / 60f)
                    : 1f;
                float baseScale = abilityComp.AttachedShieldVisualScale;
                float drawScale = baseScale * (0.9f + (energyPercent * 0.2f));

                Vector3 pos = pawn.Drawer.DrawPos;
                // 修复：护盾层级应跟随人物当前高度（含飞行高度带来的大幅提升），
                // 在 Postfix 修改后的基础上再略微提升，确保罩在角色最上方。
                pos.y += 0.05f + abilityComp.AttachedShieldVisualHeightOffset;

                // Impact jitter effect
                int ticksSinceImpact = Find.TickManager.TicksGame - abilityComp.LastShieldAbsorbTick;
                if (ticksSinceImpact < 8)
                {
                    Vector3 impactVec = abilityComp.ShieldImpactVector;
                    float jitterFactor = (float)(8 - ticksSinceImpact) / 8f * 0.05f;
                    pos.x -= impactVec.x * jitterFactor;
                    pos.z -= impactVec.y * jitterFactor;
                    drawScale -= jitterFactor;
                }

                float angle = (float)(pawn.thingIDNumber % 360) + (Find.TickManager.TicksGame * 0.5f) % 360f;
                Vector3 shieldScale = new Vector3(drawScale, 1f, drawScale);
                Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angle, Vector3.up), shieldScale);

                Material? mat = null;
                if (abilityComp.attachedShieldVisualCached != null)
                    mat = abilityComp.attachedShieldVisualCached.Graphic?.MatSingle;

                mat ??= DefaultShieldBubbleMat;

                if (mat != null)
                    Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
            }

            if (abilityComp != null && abilityComp.IsFlightStateActive())
            {
                float liftFactor = abilityComp.GetFlightLiftFactor01();
                if (liftFactor > 0.01f)
                {
                    float flightBaseHeight = abilityComp.FlightStateHeightFactor * liftFactor;
                    float currentHover = abilityComp.GetFlightHoverOffset();
                    float totalHeight = flightBaseHeight + currentHover;

                    if (totalHeight > 0.01f)
                    {
                        Material? shadowMat = FlightShadowMat;
                        if (shadowMat != null)
                        {
                            float scale = Mathf.Lerp(1f, 0.6f, totalHeight);
                            Vector3 s = new Vector3(scale, 1f, scale);
                            // 修复：由于 Patch_PawnRenderer 现在将飞行单位提升到了极高的 Altitude (Y)，
                            // 阴影必须补偿这个巨大的 Y 偏移才能回落到地面（Pawn 层级）。
                            Vector3 shadowPos = pawn.Drawer.DrawPos;
                            shadowPos.z -= totalHeight; // 抵消 Z 轴视觉偏移
                            shadowPos.y = AltitudeLayer.Pawn.AltitudeFor() - 0.01f; // 强制回落到地面层级
                            Matrix4x4 matrix = Matrix4x4.TRS(shadowPos, Quaternion.identity, s);
                            Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMat, 0);
                        }
                    }
                }
            }
        }

        private int lastPortraitDirtyTick = -1;
        private int lastGraphicDirtyTick = -1;
        private const int MinGraphicDirtyIntervalTicks = 3;

        private int faceTransformVersion = 0;
        private int faceGraphicVersion = 0;
        private readonly Dictionary<FaceTransformCacheKey, FaceTransformCacheEntry> faceTransformCache = new Dictionary<FaceTransformCacheKey, FaceTransformCacheEntry>();
        private readonly Dictionary<FacePathCacheKey, FacePathCacheEntry> facePathCache = new Dictionary<FacePathCacheKey, FacePathCacheEntry>();

        public int FaceTransformVersion => faceTransformVersion;
        public int FaceGraphicVersion => faceGraphicVersion;

        public readonly struct FaceTransformCacheKey : IEquatable<FaceTransformCacheKey>
        {
            private readonly LayeredFacePartType partType;
            private readonly LayeredFacePartSide side;
            private readonly string overlayId;
            private readonly int tick;

            public FaceTransformCacheKey(LayeredFacePartType partType, LayeredFacePartSide side, string? overlayId, int tick)
            {
                this.partType = partType;
                this.side = side;
                this.overlayId = overlayId ?? string.Empty;
                this.tick = tick;
            }

            public bool Equals(FaceTransformCacheKey other)
                => partType == other.partType
                && side == other.side
                && tick == other.tick
                && string.Equals(overlayId, other.overlayId, StringComparison.Ordinal);

            public override bool Equals(object? obj)
                => obj is FaceTransformCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)partType;
                    hash = (hash * 397) ^ (int)side;
                    hash = (hash * 397) ^ tick;
                    hash = (hash * 397) ^ overlayId.GetHashCode();
                    return hash;
                }
            }
        }

        public readonly struct FaceTransformCacheEntry
        {
            public readonly float angle;
            public readonly Vector3 offset;
            public readonly Vector3 scale;
            public readonly float targetAlpha;

            public FaceTransformCacheEntry(float angle, Vector3 offset, Vector3 scale, float targetAlpha)
            {
                this.angle = angle;
                this.offset = offset;
                this.scale = scale;
                this.targetAlpha = targetAlpha;
            }
        }

        public readonly struct FacePathCacheKey : IEquatable<FacePathCacheKey>
        {
            private readonly LayeredFacePartType partType;
            private readonly LayeredFacePartSide side;
            private readonly string overlayId;
            private readonly int facing;
            private readonly int version;

            public FacePathCacheKey(LayeredFacePartType partType, LayeredFacePartSide side, string? overlayId, Rot4 facing, int version)
            {
                this.partType = partType;
                this.side = side;
                this.overlayId = overlayId ?? string.Empty;
                this.facing = facing.AsInt;
                this.version = version;
            }

            public bool Equals(FacePathCacheKey other)
                => partType == other.partType
                && side == other.side
                && facing == other.facing
                && version == other.version
                && string.Equals(overlayId, other.overlayId, StringComparison.Ordinal);

            public override bool Equals(object? obj)
                => obj is FacePathCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)partType;
                    hash = (hash * 397) ^ (int)side;
                    hash = (hash * 397) ^ facing;
                    hash = (hash * 397) ^ version;
                    hash = (hash * 397) ^ overlayId.GetHashCode();
                    return hash;
                }
            }
        }

        public readonly struct FacePathCacheEntry
        {
            public readonly bool hasPath;
            public readonly string? path;

            public FacePathCacheEntry(string? path)
            {
                this.hasPath = !string.IsNullOrWhiteSpace(path);
                this.path = path;
            }
        }

        public bool TryGetCachedFaceTransform(FaceTransformCacheKey key, out FaceTransformCacheEntry entry)
            => faceTransformCache.TryGetValue(key, out entry);

        public void SetCachedFaceTransform(FaceTransformCacheKey key, FaceTransformCacheEntry entry)
            => faceTransformCache[key] = entry;

        public bool TryGetCachedFacePath(FacePathCacheKey key, out FacePathCacheEntry entry)
            => facePathCache.TryGetValue(key, out entry);

        public void SetCachedFacePath(FacePathCacheKey key, FacePathCacheEntry entry)
            => facePathCache[key] = entry;

        public void MarkFaceTransformDirty()
        {
            faceTransformVersion++;
            faceTransformCache.Clear();
            InvalidateEffectiveStateCache();
        }

        public void MarkFaceGraphicDirty(bool dirtyPortrait = false)
        {
            faceGraphicVersion++;
            facePathCache.Clear();
            MarkFaceTransformDirty();
            RequestRenderRefresh(dirtyPortrait);
        }

        public void RequestTransformRefresh()
        {
            MarkFaceTransformDirty();
        }

        // ─── P-PERF: Per-frame effective state cache ───
        // GetEffectiveExpression() etc. are called 50-100+ times per pawn per frame
        // during ParallelGetPreRenderResults. Cache all derived states per-frame
        // to eliminate redundant re-evaluation.
        private int _effectiveStateCacheFrameId = -1;
        private ExpressionType _cachedEffectiveExpression;
        private MouthState _cachedEffectiveMouthState;
        private LidState _cachedEffectiveLidState;
        private BrowState _cachedEffectiveBrowState;
        private EmotionOverlayState _cachedEffectiveEmotionOverlayState;
        private string _cachedEffectiveOverlaySemanticKey = string.Empty;
        private EyeAnimationVariant _cachedEffectiveEyeVariant;
        private PupilScaleVariant _cachedEffectivePupilVariant;
        private EyeDirection _cachedEffectiveEyeDirection;

        public void RequestRenderRefresh(bool dirtyPortrait = false)
        {
            Pawn? pawn = Pawn;
            if (pawn?.Drawer?.renderer == null)
                return;

            int currentTick = Find.TickManager?.TicksGame ?? -1;
            if (!CharacterStudioPerformanceStats.TryBeginRenderRefresh(pawn, currentTick))
                return;

            // P-PERF: 令每帧有效状态缓存失效，下次访问时重建
            _effectiveStateCacheFrameId = -1;

            bool shouldDirtyGraphics = currentTick < 0
                || lastGraphicDirtyTick < 0
                || currentTick - lastGraphicDirtyTick >= MinGraphicDirtyIntervalTicks;
            if (shouldDirtyGraphics)
            {
                pawn.Drawer.renderer.SetAllGraphicsDirty();
                lastGraphicDirtyTick = currentTick;
                CharacterStudioPerformanceStats.RecordGraphicDirtyTrigger(throttled: false);
            }
            else
            {
                CharacterStudioPerformanceStats.RecordGraphicDirtyTrigger(throttled: true);
            }
            
            // Throttle PortraitsCache to avoid Colonist Bar performance death spiral
            if (dirtyPortrait || currentTick < 0 || currentTick - lastPortraitDirtyTick > 60)
            {
                PortraitsCache.SetDirty(pawn);
                lastPortraitDirtyTick = currentTick;
            }
        }

    }

    public class CompProperties_PawnSkin : CompProperties
    {
        public CompProperties_PawnSkin() => this.compClass = typeof(CompPawnSkin);
    }

    public enum MouthState
    {
        Normal,
        Smile,
        Open,
        Down,
        Sleep
    }

    public enum LidState
    {
        Normal,
        Blink,
        Half,
        Close,
        Happy
    }

    public enum BrowState
    {
        Normal,
        Angry,
        Sad,
        Happy
    }

    public enum EmotionOverlayState
    {
        None,
        Blush,
        Tear,
        Sweat,
        Gloomy,
        Lovin
    }

    public enum BlinkPhase
    {
        None,
        ClosingLid,
        HideBaseEyeParts,
        ShowReplacementEye,
        RestoreBaseEyeParts,
        OpeningLid
    }

    public enum EyeAnimationVariant
    {
        NeutralOpen,
        NeutralSoft,
        NeutralLookDown,
        NeutralGlance,
        WorkFocusCenter,
        WorkFocusDown,
        WorkFocusUp,
        HappyOpen,
        HappySoft,
        HappyClosedPeak,
        ShockWide,
        ScaredWide,
        ScaredFlinch,
        BlinkClosed
    }

    public enum PupilScaleVariant
    {
        Neutral,
        Focus,
        SlightlyContracted,
        Contracted,
        Dilated,
        DilatedMax,
        ScaredPulse,
        BlinkHidden
    }

    /// <summary>
    /// CompPawnSkin 的图层世界位置查询扩展方法。
    /// 用于技能 VFX 系统获取指定图层的实际渲染世界坐标。
    /// </summary>
    public static class CompPawnSkinLayerPositionExtensions
    {
        /// <summary>
        /// 获取指定图层名在当前帧的实际世界渲染位置。
        /// 包含：基础偏移 + 图层动画偏移 + 外部动画偏移。
        /// 如果未找到图层，返回 null。
        /// </summary>
        public static Vector3? GetCurrentLayerWorldPosition(this CompPawnSkin skinComp, string layerName)
        {
            if (skinComp == null || string.IsNullOrWhiteSpace(layerName))
                return null;

            Pawn? pawn = skinComp.Pawn;
            if (pawn == null || !pawn.Spawned)
                return null;

            var renderTree = pawn.Drawer?.renderer?.renderTree;
            if (renderTree?.rootNode == null)
                return null;

            Rendering.PawnRenderNode_Custom? foundNode = FindCustomNodeByName(renderTree.rootNode, layerName);
            if (foundNode == null || foundNode.config == null)
                return null;

            // 基础世界位置 = Pawn.DrawPos
            Vector3 worldPos = pawn.DrawPos;

            // 加上图层配置的基础偏移
            Vector3 baseOffset = foundNode.config.offset;
            worldPos += baseOffset;

            // 加上动画系统计算的偏移（包含触发式装备动画）
            worldPos += foundNode.currentAnimOffset;

            // 加上外部图层动画偏移（如环绕轨道动画）
            worldPos += foundNode.currentExternalLayerOffset;

            // 根据当前朝向加上方向特定偏移（与渲染系统 OffsetFor 保持一致）
            Rot4 facing = pawn.Rotation;
            bool usePerFacingOffsets = skinComp.ActiveSkin?.editLayerOffsetPerFacing ?? false;
            if (usePerFacingOffsets && (facing == Rot4.East || facing == Rot4.West))
            {
                if (facing == Rot4.West && foundNode.config.useWestOffset)
                {
                    worldPos += foundNode.config.offsetWest;
                }
                else
                {
                    Vector3 eastOffset = foundNode.config.offsetEast;
                    if (facing == Rot4.West)
                        eastOffset.x = -eastOffset.x;
                    worldPos += eastOffset;
                }
            }
            else if (usePerFacingOffsets && facing == Rot4.North)
            {
                worldPos += foundNode.config.offsetNorth;
            }

            return worldPos;
        }

        private static Rendering.PawnRenderNode_Custom? FindCustomNodeByName(PawnRenderNode node, string layerName)
        {
            if (node is Rendering.PawnRenderNode_Custom customNode
                && customNode.config != null
                && string.Equals(customNode.config.layerName, layerName, StringComparison.OrdinalIgnoreCase))
            {
                return customNode;
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    var found = FindCustomNodeByName(child, layerName);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }
    }
}
