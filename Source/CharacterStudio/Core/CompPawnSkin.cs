using System;
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
    public partial class CompPawnSkin : ThingComp
    {
        public static event Action<Pawn, PawnSkinDef?, bool, bool, string>? SkinChangedGlobal;

        private PawnSkinDef? activeSkin;
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
        private static readonly Material DefaultShieldBubbleMat = MaterialPool.MatFrom("Things/Pawn/Effects/Shield", ShaderDatabase.Transparent);

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
            RequestRenderRefresh();
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
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + abilityComp.AttachedShieldVisualHeightOffset;

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
                Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(drawScale, 1f, drawScale));

                Material? mat = null;
                if (abilityComp.attachedShieldVisualCached != null)
                    mat = abilityComp.attachedShieldVisualCached.Graphic?.MatSingle;

                mat ??= DefaultShieldBubbleMat;

                if (mat != null)
                    Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
            }
        }

        private int lastPortraitDirtyTick = -1;

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

            pawn.Drawer.renderer.SetAllGraphicsDirty();
            
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
}
