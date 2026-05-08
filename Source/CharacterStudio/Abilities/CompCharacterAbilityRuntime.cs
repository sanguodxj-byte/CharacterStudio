﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 独立的技能运行时组件，负责管理技能冷却、护盾状态、飞行状态、强制位移等
    /// 与外观渲染(CompPawnSkin)完全解耦的运行时数据。
    /// </summary>
    public class CompCharacterAbilityRuntime : ThingComp
    {
        public Pawn? Pawn => parent as Pawn;

        public AbilityHotkeyRuntimeState RuntimeState => abilityRuntimeState;

        public CharacterAbilityLoadout? ActiveAbilityLoadout
        {
            get => activeAbilityLoadout;
            set => activeAbilityLoadout = value?.Clone();
        }

        public bool HasExplicitAbilityLoadout => activeAbilityLoadout != null;

        private AbilityHotkeyRuntimeState abilityRuntimeState = new AbilityHotkeyRuntimeState();
        private CharacterAbilityLoadout? activeAbilityLoadout;

        // ── P-PERF: Per-frame rendering cache ──
        private int _lastFlightHeightFrameId = -1;
        private float _cachedTotalFlightHeight = 0f;

        public float TotalFlightHeight
        {
            get
            {
                int currentFrame = Time.frameCount;
                if (_lastFlightHeightFrameId == currentFrame)
                    return _cachedTotalFlightHeight;

                _lastFlightHeightFrameId = currentFrame;
                if (!IsFlightStateActive())
                {
                    _cachedTotalFlightHeight = 0f;
                }
                else if (abilityRuntimeState.isWorldMapLanding)
                {
                    // 着陆模式：使用着陆高度因子 + liftFactor（从 1→0）
                    float liftFactor = GetFlightLiftFactor01();
                    _cachedTotalFlightHeight = abilityRuntimeState.worldMapLandingHeightFactor * liftFactor;
                }
                else
                {
                    float liftFactor = GetFlightLiftFactor01();
                    _cachedTotalFlightHeight = (abilityRuntimeState.flightStateHeightFactor * liftFactor) + GetFlightHoverOffset();
                }
                return _cachedTotalFlightHeight;
            }
        }

        /// <summary>
        /// 稳定的飞行高度（不含 hover 波动），用于渲染层级排序。
        /// Y 轴（altitude）若包含 hover 正弦波动会导致全局渲染排序每帧重排，
        /// 造成所有角色 Z 轴持续跳变。
        /// </summary>
        public float StableFlightHeight
        {
            get
            {
                if (!IsFlightStateActive()) return 0f;
                if (abilityRuntimeState.isWorldMapLanding)
                    return abilityRuntimeState.worldMapLandingHeightFactor * GetFlightLiftFactor01();
                return abilityRuntimeState.flightStateHeightFactor * GetFlightLiftFactor01();
            }
        }

        // ── Cached Thing references ──
        internal Thing? attachedShieldVisualCached;
        internal Thing? projectileInterceptorShieldCached;
        private int lastShieldAbsorbTick = -9999;
        private Vector3 shieldImpactVector = Vector3.zero;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref activeAbilityLoadout, "activeAbilityLoadout");
            abilityRuntimeState.ExposeData();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                activeAbilityLoadout ??= null;
                abilityRuntimeState.Normalize();
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            TickForcedMove();
            TickWorldMapFlight();
            TickWorldMapLanding();
            TickAbilityFaceOverride();
        }

        // ── Slot Override Window ──
        public int SlotOverrideWindowEndTick
        {
            get => abilityRuntimeState.slotOverrideWindowEndTick;
            set => abilityRuntimeState.slotOverrideWindowEndTick = value;
        }
        public string SlotOverrideWindowAbilityDefName
        {
            get => abilityRuntimeState.slotOverrideWindowAbilityDefName;
            set => abilityRuntimeState.slotOverrideWindowAbilityDefName = value ?? string.Empty;
        }
        public string SlotOverrideWindowSlotId
        {
            get => abilityRuntimeState.slotOverrideWindowSlotId;
            set => abilityRuntimeState.slotOverrideWindowSlotId = value ?? string.Empty;
        }

        // ── Cooldown shortcuts ──
        public int GetCooldownUntilTick(AbilityRuntimeHotkeySlot slot) => abilityRuntimeState.GetCooldownUntilTick(slot);
        public void SetCooldownUntilTick(AbilityRuntimeHotkeySlot slot, int value) => abilityRuntimeState.SetCooldownUntilTick(slot, value);
        public void SetOverrideDefName(AbilityRuntimeHotkeySlot slot, string defName) => abilityRuntimeState.SetOverrideDefName(slot, defName);
        public void SetOverrideExpireTick(AbilityRuntimeHotkeySlot slot, int tick) => abilityRuntimeState.SetOverrideExpireTick(slot, tick);

        // ── Weapon Carry ──
        public int WeaponCarryCastingUntilTick
        {
            get => abilityRuntimeState.weaponCarryCastingUntilTick;
            set => abilityRuntimeState.weaponCarryCastingUntilTick = value;
        }

        // ── Periodic Pulse ──
        public int PeriodicPulseNextTick
        {
            get => abilityRuntimeState.periodicPulseNextTick;
            set => abilityRuntimeState.periodicPulseNextTick = value;
        }
        public int PeriodicPulseEndTick
        {
            get => abilityRuntimeState.periodicPulseEndTick;
            set => abilityRuntimeState.periodicPulseEndTick = value;
        }

        // ── Shield ──
        public float ShieldRemainingDamage
        {
            get => abilityRuntimeState.shieldRemainingDamage;
            set => abilityRuntimeState.shieldRemainingDamage = value;
        }
        public int ShieldExpireTick
        {
            get => abilityRuntimeState.shieldExpireTick;
            set => abilityRuntimeState.shieldExpireTick = value;
        }
        public float ShieldStoredHeal
        {
            get => abilityRuntimeState.shieldStoredHeal;
            set => abilityRuntimeState.shieldStoredHeal = value;
        }
        public float ShieldStoredBonusDamage
        {
            get => abilityRuntimeState.shieldStoredBonusDamage;
            set => abilityRuntimeState.shieldStoredBonusDamage = value;
        }

        // ── Attached Shield Visual ──
        public int AttachedShieldVisualExpireTick
        {
            get => abilityRuntimeState.attachedShieldVisualExpireTick;
            set => abilityRuntimeState.attachedShieldVisualExpireTick = value;
        }
        public float AttachedShieldVisualScale
        {
            get => abilityRuntimeState.attachedShieldVisualScale;
            set => abilityRuntimeState.attachedShieldVisualScale = value;
        }
        public float AttachedShieldVisualHeightOffset
        {
            get => abilityRuntimeState.attachedShieldVisualHeightOffset;
            set => abilityRuntimeState.attachedShieldVisualHeightOffset = value;
        }
        public string AttachedShieldVisualThingId
        {
            get => abilityRuntimeState.attachedShieldVisualThingId;
            set => abilityRuntimeState.attachedShieldVisualThingId = value ?? string.Empty;
        }

        // ── Projectile Interceptor Shield ──
        public int ProjectileInterceptorShieldExpireTick
        {
            get => abilityRuntimeState.projectileInterceptorShieldExpireTick;
            set => abilityRuntimeState.projectileInterceptorShieldExpireTick = value;
        }
        public string ProjectileInterceptorShieldThingId
        {
            get => abilityRuntimeState.projectileInterceptorShieldThingId;
            set => abilityRuntimeState.projectileInterceptorShieldThingId = value ?? string.Empty;
        }

        // ── Bezier Curve Wall ──
        public int BezierWallExpireTick
        {
            get => abilityRuntimeState.bezierWallExpireTick;
            set => abilityRuntimeState.bezierWallExpireTick = value;
        }
        public float BezierWallStartX
        {
            get => abilityRuntimeState.bezierWallStartX;
            set => abilityRuntimeState.bezierWallStartX = value;
        }
        public float BezierWallStartZ
        {
            get => abilityRuntimeState.bezierWallStartZ;
            set => abilityRuntimeState.bezierWallStartZ = value;
        }
        public float BezierWallEndX
        {
            get => abilityRuntimeState.bezierWallEndX;
            set => abilityRuntimeState.bezierWallEndX = value;
        }
        public float BezierWallEndZ
        {
            get => abilityRuntimeState.bezierWallEndZ;
            set => abilityRuntimeState.bezierWallEndZ = value;
        }
        public float BezierWallControlX
        {
            get => abilityRuntimeState.bezierWallControlX;
            set => abilityRuntimeState.bezierWallControlX = value;
        }
        public float BezierWallControlZ
        {
            get => abilityRuntimeState.bezierWallControlZ;
            set => abilityRuntimeState.bezierWallControlZ = value;
        }
        public float BezierWallThickness
        {
            get => abilityRuntimeState.bezierWallThickness;
            set => abilityRuntimeState.bezierWallThickness = value;
        }
        public int BezierWallSegmentCount
        {
            get => abilityRuntimeState.bezierWallSegmentCount;
            set => abilityRuntimeState.bezierWallSegmentCount = value;
        }
        public bool BezierWallBlockFriendly
        {
            get => abilityRuntimeState.bezierWallBlockFriendly;
            set => abilityRuntimeState.bezierWallBlockFriendly = value;
        }
        public float BezierWallAbsorbRemaining
        {
            get => abilityRuntimeState.bezierWallAbsorbRemaining;
            set => abilityRuntimeState.bezierWallAbsorbRemaining = value;
        }

        public int LastBezierWallApplyTick
        {
            get => abilityRuntimeState.lastBezierWallApplyTick;
            set => abilityRuntimeState.lastBezierWallApplyTick = value;
        }

        public bool IsBezierWallActive()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return abilityRuntimeState.bezierWallExpireTick >= now
                && abilityRuntimeState.bezierWallAbsorbRemaining > 0f;
        }

        // ── Offensive Mark ──
        public int OffensiveMarkExpireTick
        {
            get => abilityRuntimeState.offensiveMarkExpireTick;
            set => abilityRuntimeState.offensiveMarkExpireTick = value;
        }
        public int OffensiveMarkStacks
        {
            get => abilityRuntimeState.offensiveMarkStacks;
            set => abilityRuntimeState.offensiveMarkStacks = value;
        }

        // ── Offensive Combo ──
        public int OffensiveComboExpireTick
        {
            get => abilityRuntimeState.offensiveComboExpireTick;
            set => abilityRuntimeState.offensiveComboExpireTick = value;
        }
        public int OffensiveComboStacks
        {
            get => abilityRuntimeState.offensiveComboStacks;
            set => abilityRuntimeState.offensiveComboStacks = value;
        }

        // ── Dash Empower ──
        public int DashEmpowerExpireTick
        {
            get => abilityRuntimeState.dashEmpowerExpireTick;
            set => abilityRuntimeState.dashEmpowerExpireTick = value;
        }

        // ── Flight State ──
        public int FlightStateStartTick
        {
            get => abilityRuntimeState.flightStateStartTick;
            set => abilityRuntimeState.flightStateStartTick = value;
        }
        public int FlightStateExpireTick
        {
            get => abilityRuntimeState.flightStateExpireTick;
            set
            {
                abilityRuntimeState.flightStateExpireTick = value;
                NotifyFlyingStateChanged();
            }
        }
        public float FlightStateHeightFactor
        {
            get => abilityRuntimeState.flightStateHeightFactor;
            set => abilityRuntimeState.flightStateHeightFactor = value;
        }
        public bool SuppressCombatActionsDuringFlightState
        {
            get => abilityRuntimeState.suppressCombatActionsDuringFlightState;
            set => abilityRuntimeState.suppressCombatActionsDuringFlightState = value;
        }
        public bool IsInVanillaFlight
        {
            get => abilityRuntimeState.isInVanillaFlight;
            set => abilityRuntimeState.isInVanillaFlight = value;
        }
        public int VanillaFlightStartTick
        {
            get => abilityRuntimeState.vanillaFlightStartTick;
            set => abilityRuntimeState.vanillaFlightStartTick = value;
        }
        public int VanillaFlightExpireTick
        {
            get => abilityRuntimeState.vanillaFlightExpireTick;
            set => abilityRuntimeState.vanillaFlightExpireTick = value;
        }
        public string VanillaFlightSourceAbilityDefName
        {
            get => abilityRuntimeState.vanillaFlightSourceAbilityDefName;
            set => abilityRuntimeState.vanillaFlightSourceAbilityDefName = value ?? string.Empty;
        }
        public string VanillaFlightFollowupAbilityDefName
        {
            get => abilityRuntimeState.vanillaFlightFollowupAbilityDefName;
            set => abilityRuntimeState.vanillaFlightFollowupAbilityDefName = value ?? string.Empty;
        }
        public IntVec3 VanillaFlightReservedTargetCell
        {
            get => abilityRuntimeState.vanillaFlightReservedTargetCell;
            set => abilityRuntimeState.vanillaFlightReservedTargetCell = value;
        }
        public bool VanillaFlightHasReservedTargetCell
        {
            get => abilityRuntimeState.vanillaFlightHasReservedTargetCell;
            set => abilityRuntimeState.vanillaFlightHasReservedTargetCell = value;
        }
        public int VanillaFlightFollowupWindowEndTick
        {
            get => abilityRuntimeState.vanillaFlightFollowupWindowEndTick;
            set => abilityRuntimeState.vanillaFlightFollowupWindowEndTick = value;
        }
        public bool VanillaFlightPendingLandingBurst
        {
            get => abilityRuntimeState.vanillaFlightPendingLandingBurst;
            set => abilityRuntimeState.vanillaFlightPendingLandingBurst = value;
        }

        // ── World Map Flight ──
        public bool IsInWorldMapFlight
        {
            get => abilityRuntimeState.isInWorldMapFlight;
            set => abilityRuntimeState.isInWorldMapFlight = value;
        }
        public string WorldMapFlightSourceAbilityDefName
        {
            get => abilityRuntimeState.worldMapFlightSourceAbilityDefName;
            set => abilityRuntimeState.worldMapFlightSourceAbilityDefName = value ?? string.Empty;
        }
        public int WorldMapFlightDestinationTile
        {
            get => abilityRuntimeState.worldMapFlightDestinationTile;
            set => abilityRuntimeState.worldMapFlightDestinationTile = value;
        }
        public int WorldMapFlightStartTick
        {
            get => abilityRuntimeState.worldMapFlightStartTick;
            set => abilityRuntimeState.worldMapFlightStartTick = value;
        }
        public int WorldMapFlightHandoffTick
        {
            get => abilityRuntimeState.worldMapFlightHandoffTick;
            set => abilityRuntimeState.worldMapFlightHandoffTick = value;
        }
        public bool WorldMapFlightMapParentRequired
        {
            get => abilityRuntimeState.worldMapFlightMapParentRequired;
            set => abilityRuntimeState.worldMapFlightMapParentRequired = value;
        }
        public int WorldMapFlightTravelDurationTicks
        {
            get => abilityRuntimeState.worldMapFlightTravelDurationTicks;
            set => abilityRuntimeState.worldMapFlightTravelDurationTicks = value;
        }

        // ── World Map Landing ──
        public bool IsWorldMapLanding
        {
            get => abilityRuntimeState.isWorldMapLanding;
            set
            {
                abilityRuntimeState.isWorldMapLanding = value;
                NotifyFlyingStateChanged();
            }
        }
        public int WorldMapLandingStartTick
        {
            get => abilityRuntimeState.worldMapLandingStartTick;
            set => abilityRuntimeState.worldMapLandingStartTick = value;
        }
        public int WorldMapLandingDurationTicks
        {
            get => abilityRuntimeState.worldMapLandingDurationTicks;
            set => abilityRuntimeState.worldMapLandingDurationTicks = value;
        }
        public float WorldMapLandingHeightFactor
        {
            get => abilityRuntimeState.worldMapLandingHeightFactor;
            set => abilityRuntimeState.worldMapLandingHeightFactor = value;
        }

        // ── Triggered Equipment Animation ──
        public string TriggeredEquipmentAnimationAbilityDefName
        {
            get => abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName;
            set => abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName = value ?? string.Empty;
        }
        public int TriggeredEquipmentAnimationStartTick
        {
            get => abilityRuntimeState.triggeredEquipmentAnimationStartTick;
            set => abilityRuntimeState.triggeredEquipmentAnimationStartTick = value;
        }
        public int TriggeredEquipmentAnimationEndTick
        {
            get => abilityRuntimeState.triggeredEquipmentAnimationEndTick;
            set => abilityRuntimeState.triggeredEquipmentAnimationEndTick = value;
        }

        // ── Ability Face Override ──
        public int AbilityExpressionOverrideExpireTick
        {
            get => abilityRuntimeState.abilityExpressionOverrideExpireTick;
            set => abilityRuntimeState.abilityExpressionOverrideExpireTick = value;
        }
        public ExpressionType? AbilityExpressionOverride
        {
            get => abilityRuntimeState.abilityExpressionOverride;
            set => abilityRuntimeState.abilityExpressionOverride = value;
        }
        public float AbilityPupilBrightnessOffset
        {
            get => abilityRuntimeState.abilityPupilBrightnessOffset;
            set => abilityRuntimeState.abilityPupilBrightnessOffset = value;
        }
        public float AbilityPupilContrastOffset
        {
            get => abilityRuntimeState.abilityPupilContrastOffset;
            set => abilityRuntimeState.abilityPupilContrastOffset = value;
        }

        // ── Forced Move ──
        public bool ForcedMoveActive
        {
            get => abilityRuntimeState.forcedMoveActive;
            set => abilityRuntimeState.forcedMoveActive = value;
        }
        public IntVec3 ForcedMoveStartCell
        {
            get => abilityRuntimeState.forcedMoveStartCell;
            set => abilityRuntimeState.forcedMoveStartCell = value;
        }
        public IntVec3 ForcedMoveCurrentCell
        {
            get => abilityRuntimeState.forcedMoveCurrentCell;
            set => abilityRuntimeState.forcedMoveCurrentCell = value;
        }
        public IntVec3 ForcedMoveNextCell
        {
            get => abilityRuntimeState.forcedMoveNextCell;
            set => abilityRuntimeState.forcedMoveNextCell = value;
        }
        public int ForcedMoveStepStartTick
        {
            get => abilityRuntimeState.forcedMoveStepStartTick;
            set => abilityRuntimeState.forcedMoveStepStartTick = value;
        }
        public int ForcedMoveStepDurationTicks
        {
            get => abilityRuntimeState.forcedMoveStepDurationTicks;
            set => abilityRuntimeState.forcedMoveStepDurationTicks = value;
        }
        public int ForcedMoveQueuedSteps
        {
            get => abilityRuntimeState.forcedMoveQueuedSteps;
            set => abilityRuntimeState.forcedMoveQueuedSteps = value;
        }
        public int ForcedMoveDirectionX
        {
            get => abilityRuntimeState.forcedMoveDirectionX;
            set => abilityRuntimeState.forcedMoveDirectionX = value;
        }
        public int ForcedMoveDirectionZ
        {
            get => abilityRuntimeState.forcedMoveDirectionZ;
            set => abilityRuntimeState.forcedMoveDirectionZ = value;
        }
        public int ForcedMoveBusyUntilTick
        {
            get => abilityRuntimeState.forcedMoveBusyUntilTick;
            set => abilityRuntimeState.forcedMoveBusyUntilTick = value;
        }
        public bool ForcedMoveCollisionTriggered
        {
            get => abilityRuntimeState.forcedMoveCollisionTriggered;
            set => abilityRuntimeState.forcedMoveCollisionTriggered = value;
        }

        // ── Dash Deferred Effect Callbacks ──
        /// <summary>
        /// 碰撞回调：传入碰撞格和精确命中的敌人（可能为 null）。
        /// 支持穿透模式下的沿途逐目标触发。
        /// </summary>
        private System.Action<IntVec3, Pawn?>? _dashCollisionCallback;
        private System.Action<IntVec3>? _dashCompleteCallback;

        public void SetDashCallbacks(System.Action<IntVec3, Pawn?>? onCollision, System.Action<IntVec3>? onComplete)
        {
            _dashCollisionCallback = onCollision;
            _dashCompleteCallback = onComplete;
        }

        public void ClearDashCallbacks()
        {
            _dashCollisionCallback = null;
            _dashCompleteCallback = null;
        }

        // ── Ability Expression Override ──
        public bool IsAbilityExpressionOverrideActive()
        {
            return abilityRuntimeState.abilityExpressionOverrideExpireTick >= 0
                && (abilityRuntimeState.abilityExpressionOverride.HasValue
                    || Math.Abs(abilityRuntimeState.abilityPupilBrightnessOffset) > 0.001f
                    || Math.Abs(abilityRuntimeState.abilityPupilContrastOffset) > 0.001f);
        }

        public void ApplyAbilityFaceOverride(ExpressionType? expression, int durationTicks, float pupilBrightnessOffset = 0f, float pupilContrastOffset = 0f)
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (expression.HasValue)
            {
                abilityRuntimeState.abilityExpressionOverrideExpireTick = nowTick + Mathf.Max(1, durationTicks);
                abilityRuntimeState.abilityExpressionOverride = expression.Value;
                abilityRuntimeState.abilityPupilBrightnessOffset = pupilBrightnessOffset;
                abilityRuntimeState.abilityPupilContrastOffset = pupilContrastOffset;
            }
            else
            {
                abilityRuntimeState.abilityExpressionOverrideExpireTick = -1;
                abilityRuntimeState.abilityExpressionOverride = null;
                abilityRuntimeState.abilityPupilBrightnessOffset = 0f;
                abilityRuntimeState.abilityPupilContrastOffset = 0f;
            }
        }

        // ── Equipment Animation State ──
        public void TriggerEquipmentAnimationState(string abilityDefName, int startTick, int durationTicks)
        {
            abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName = abilityDefName ?? string.Empty;
            abilityRuntimeState.triggeredEquipmentAnimationStartTick = startTick;
            abilityRuntimeState.triggeredEquipmentAnimationEndTick = startTick + Mathf.Max(1, durationTicks);
        }

        public void ClearEquipmentAnimationState(string abilityDefName)
        {
            if (string.Equals(abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName, abilityDefName, StringComparison.OrdinalIgnoreCase))
            {
                abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName = string.Empty;
                abilityRuntimeState.triggeredEquipmentAnimationStartTick = -1;
                abilityRuntimeState.triggeredEquipmentAnimationEndTick = -1;
            }
        }

        public bool IsTriggeredEquipmentAnimationActive()
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            return abilityRuntimeState.triggeredEquipmentAnimationStartTick >= 0
                && nowTick >= abilityRuntimeState.triggeredEquipmentAnimationStartTick
                && nowTick <= abilityRuntimeState.triggeredEquipmentAnimationEndTick;
        }

        public bool IsWeaponCarryCastingNow()
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            return abilityRuntimeState.weaponCarryCastingUntilTick >= nowTick;
        }

        public void SetWeaponCarryCastingWindow(int durationTicks)
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int resolvedDuration = Math.Max(1, durationTicks);
            abilityRuntimeState.weaponCarryCastingUntilTick = nowTick + resolvedDuration;
        }

        // ── Begin Forced Move (convenience overload) ──
        public void BeginForcedMove(Vector2 direction, float distance, int durationTicks, bool isLanding = false, bool sweepAcrossPath = false)
        {
            Pawn? pawn = Pawn;
            if (pawn?.Map == null || !pawn.Spawned || distance <= 0f || durationTicks <= 0)
                return;

            if (Mathf.Approximately(direction.sqrMagnitude, 0f))
                return;

            Vector2 dir = direction.normalized;

            abilityRuntimeState.forcedMoveActive = true;
            abilityRuntimeState.forcedMoveIsLanding = isLanding;
            abilityRuntimeState.forcedMoveStartCell = pawn.Position;
            abilityRuntimeState.forcedMoveCurrentCell = pawn.Position;
            abilityRuntimeState.forcedMoveNextCell = pawn.Position;
            abilityRuntimeState.forcedMoveDirFloatX = dir.x;
            abilityRuntimeState.forcedMoveDirFloatZ = dir.y;
            abilityRuntimeState.forcedMoveTotalDistance = distance;
            abilityRuntimeState.forcedMoveTraveledDistance = 0f;
            abilityRuntimeState.forcedMoveSpeedPerTick = distance / Mathf.Max(1, durationTicks);
            abilityRuntimeState.forcedMoveStartTick = Find.TickManager?.TicksGame ?? 0;
            abilityRuntimeState.forcedMoveDurationTicks = durationTicks;
            abilityRuntimeState.forcedMoveSweepAcrossPath = sweepAcrossPath;
            abilityRuntimeState.forcedMoveHitPawns?.Clear();
            abilityRuntimeState.forcedMoveStepStartTick = abilityRuntimeState.forcedMoveStartTick;
            abilityRuntimeState.forcedMoveBusyUntilTick = abilityRuntimeState.forcedMoveStartTick + durationTicks + 6;
            abilityRuntimeState.forcedMoveCollisionTriggered = false;

            // 同步写入冗余字段以兼容存档序列化
            abilityRuntimeState.forcedMoveDirectionX = (int)Mathf.Round(dir.x);
            abilityRuntimeState.forcedMoveDirectionZ = (int)Mathf.Round(dir.y);
            abilityRuntimeState.forcedMoveQueuedSteps = Mathf.CeilToInt(distance);
            abilityRuntimeState.forcedMoveStepDurationTicks = durationTicks;

            pawn.Rotation = RotFromDirection(dir.x, dir.y);

            // 停止当前移动和排队的工作，但不施加眩晕状态（避免显示眩晕星星）
            pawn.pather?.StopDead();
            pawn.jobs?.ClearQueuedJobs();

            RequestSkinRenderRefresh();
        }

        // ── Shield PostDraw entry point (delegates to CompPawnSkin.ShieldRendering) ──
        // Shield rendering remains in CompPawnSkin but reads state from this component.

        /// <summary>
        /// 根据方向向量转换为最接近的 Rot4（用于冲刺朝向）。
        /// </summary>
        private static Rot4 RotFromDirection(float dirX, float dirZ)
        {
            if (Mathf.Abs(dirZ) >= Mathf.Abs(dirX))
                return dirZ > 0 ? Rot4.North : Rot4.South;
            return dirX > 0 ? Rot4.East : Rot4.West;
        }

        // ── Forced Move Tick (complete with collision, stun, resolve) ──
        private void TickForcedMove()
        {
            if (!abilityRuntimeState.forcedMoveActive)
                return;

            Pawn? pawn = Pawn;
            if (pawn == null || pawn.Map == null || !pawn.Spawned)
            {
                ClearForcedMoveState();
                return;
            }

            float speed = abilityRuntimeState.forcedMoveSpeedPerTick;
            if (speed <= 0f)
            {
                ClearForcedMoveState();
                return;
            }

            abilityRuntimeState.forcedMoveTraveledDistance += speed;

            // 计算当前世界位置
            float dx = abilityRuntimeState.forcedMoveDirFloatX * abilityRuntimeState.forcedMoveTraveledDistance;
            float dz = abilityRuntimeState.forcedMoveDirFloatZ * abilityRuntimeState.forcedMoveTraveledDistance;
            IntVec3 startCell = abilityRuntimeState.forcedMoveStartCell;
            float worldX = startCell.x + dx;
            float worldZ = startCell.z + dz;

            // 沿冲刺方向单调推进格点。
            // 使用 Mathf.FloorToInt 配合微小偏移，确保在跨越 0.5 边界时，
            // 逻辑格点的跳转与渲染层的视觉偏移补偿在同一个 Tick 内发生。
            int targetCellX = Mathf.FloorToInt(worldX + 0.5f);
            int targetCellZ = Mathf.FloorToInt(worldZ + 0.5f);
            IntVec3 nearestCell = new IntVec3(targetCellX, 0, targetCellZ);

            // 建筑阻挡检测：仅建筑物阻止冲刺移动
            if (nearestCell.InBounds(pawn.Map))
            {
                Building? blockingBuilding = nearestCell.GetEdifice(pawn.Map);
                if (blockingBuilding != null && blockingBuilding.def.blockWind)
                {
                    TriggerForcedMoveCollisionFeedback(pawn, pawn.Position, blockedImmediately: false);
                    ClearForcedMoveState();
                    return;
                }

                if (nearestCell != pawn.Position)
                {
                    pawn.Position = nearestCell;
                    RequestSkinRenderRefresh();
                }
            }
            else
            {
                TriggerForcedMoveCollisionFeedback(pawn, pawn.Position, blockedImmediately: true);
                ClearForcedMoveState();
                return;
            }
            abilityRuntimeState.forcedMoveCurrentCell = pawn.Position;

            // 碰撞检测
            IntVec3 checkCenter = pawn.Position;
            Map pawnMap = pawn.Map;
            bool hitAnythingThisTick = false;
            float closestDist = 1.2f;

            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    IntVec3 checkCell = new IntVec3(checkCenter.x + ox, 0, checkCenter.z + oz);
                    if (!checkCell.InBounds(pawnMap)) continue;
                    List<Thing> things = pawnMap.thingGrid.ThingsListAt(checkCell);
                    for (int i = 0; i < things.Count; i++)
                    {
                        if (things[i] is Pawn other && other != pawn && !other.Dead && other.Spawned)
                        {
                            float dist = Mathf.Abs(other.DrawPos.x - (worldX + 0.5f)) + Mathf.Abs(other.DrawPos.z - (worldZ + 0.5f));
                            if (dist < 1.2f)
                            {
                                if (abilityRuntimeState.forcedMoveSweepAcrossPath)
                                {
                                    if (abilityRuntimeState.forcedMoveHitPawns != null && !abilityRuntimeState.forcedMoveHitPawns.Contains(other))
                                    {
                                        abilityRuntimeState.forcedMoveHitPawns.Add(other);
                                        _dashCollisionCallback?.Invoke(other.Position, other);
                                        hitAnythingThisTick = true;
                                    }
                                }
                                else if (dist < closestDist)
                                {
                                    closestDist = dist;
                                    hitAnythingThisTick = true;
                                }
                            }
                        }
                    }
                }
            }

            if (hitAnythingThisTick && _dashCollisionCallback != null && !abilityRuntimeState.forcedMoveSweepAcrossPath)
            {
                var cb = _dashCollisionCallback;
                _dashCollisionCallback = null;
                cb(pawn.Position, null);
                if (_dashCompleteCallback == null)
                {
                    TriggerForcedMoveCollisionFeedback(pawn, pawn.Position, blockedImmediately: false);
                    ClearForcedMoveState();
                    return;
                }
            }

            // 检查是否到达总距离
            if (abilityRuntimeState.forcedMoveTraveledDistance >= abilityRuntimeState.forcedMoveTotalDistance)
            {
                if (_dashCompleteCallback != null)
                {
                    var cb = _dashCompleteCallback;
                    _dashCompleteCallback = null;
                    cb(pawn.Position);
                }
                ClearForcedMoveState();
                return;
            }

            // 检查是否超出地图
            if (!nearestCell.InBounds(pawn.Map))
            {
                TriggerForcedMoveCollisionFeedback(pawn, pawn.Position, blockedImmediately: true);
                ClearForcedMoveState();
                return;
            }
        }

        private void ClearForcedMoveState()
        {
            // 如果是落地冲刺，结束时清除飞行状态
            if (abilityRuntimeState.forcedMoveActive && abilityRuntimeState.forcedMoveIsLanding)
            {
                abilityRuntimeState.flightStateExpireTick = -1;
                NotifyFlyingStateChanged();
            }

            abilityRuntimeState.forcedMoveActive = false;
            abilityRuntimeState.forcedMoveIsLanding = false;
            abilityRuntimeState.forcedMoveQueuedSteps = 0;
            abilityRuntimeState.forcedMoveDirectionX = 0;
            abilityRuntimeState.forcedMoveDirectionZ = 0;
            abilityRuntimeState.forcedMoveBusyUntilTick = -1;
            abilityRuntimeState.forcedMoveCollisionTriggered = false;
            abilityRuntimeState.forcedMoveStartCell = IntVec3.Invalid;
            abilityRuntimeState.forcedMoveCurrentCell = IntVec3.Invalid;
            abilityRuntimeState.forcedMoveNextCell = IntVec3.Invalid;
            abilityRuntimeState.forcedMoveStepStartTick = -1;
            abilityRuntimeState.forcedMoveDirFloatX = 0f;
            abilityRuntimeState.forcedMoveDirFloatZ = 0f;
            abilityRuntimeState.forcedMoveTotalDistance = 0f;
            abilityRuntimeState.forcedMoveTraveledDistance = 0f;
            abilityRuntimeState.forcedMoveSpeedPerTick = 0f;
            abilityRuntimeState.forcedMoveStartTick = -1;
            abilityRuntimeState.forcedMoveDurationTicks = -1;
            abilityRuntimeState.forcedMoveHitPawns?.Clear();
            abilityRuntimeState.forcedMoveSweepAcrossPath = false;
            _dashCollisionCallback = null;
            _dashCompleteCallback = null;
        }

        private void TriggerForcedMoveCollisionFeedback(Pawn pawn, IntVec3 collisionCell, bool blockedImmediately)
        {
            if (abilityRuntimeState.forcedMoveCollisionTriggered || pawn.Map == null)
                return;

            abilityRuntimeState.forcedMoveCollisionTriggered = true;
            float dustScale = blockedImmediately ? 1.6f : 1.1f;
            FleckMaker.ThrowDustPuff(collisionCell, pawn.Map, dustScale);
            FleckMaker.ThrowMicroSparks(pawn.DrawPos, pawn.Map);

            if (pawn.stances?.stunner != null)
                pawn.stances.stunner.StunFor(blockedImmediately ? 12 : 8, pawn, false);

            abilityRuntimeState.forcedMoveBusyUntilTick = Math.Max(
                abilityRuntimeState.forcedMoveBusyUntilTick,
                (Find.TickManager?.TicksGame ?? 0) + (blockedImmediately ? 12 : 8));

            // 地形阻挡碰撞：触发碰撞回调
            if (_dashCollisionCallback != null)
            {
                var cb = _dashCollisionCallback;
                _dashCollisionCallback = null;
                _dashCompleteCallback = null;
                cb(collisionCell, null);
            }

            RequestSkinRenderRefresh();
        }

        // ── Forced Move Query ──
        public bool IsForcedMoveBusy()
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            return abilityRuntimeState.forcedMoveActive || abilityRuntimeState.forcedMoveBusyUntilTick >= nowTick;
        }


        public void CancelForcedMove()
        {
            ClearForcedMoveState();
        }

        // ── Triggered Equipment Animation (with string key overload) ──
        public bool IsTriggeredEquipmentAnimationActive(string abilityDefName)
        {
            if (string.IsNullOrWhiteSpace(abilityDefName))
                return IsTriggeredEquipmentAnimationActive();
            return string.Equals(abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName, abilityDefName, StringComparison.OrdinalIgnoreCase)
                && IsTriggeredEquipmentAnimationActive();
        }

        // ── Flight Visual Queries (for rendering layer) ──
        public bool IsFlightStateActive()
        {
            // 世界地图着陆模式：独立的飞行判定
            if (abilityRuntimeState.isWorldMapLanding)
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                return abilityRuntimeState.worldMapLandingStartTick >= 0
                    && now < abilityRuntimeState.worldMapLandingStartTick + abilityRuntimeState.worldMapLandingDurationTicks;
            }

            int now2 = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            return abilityRuntimeState.flightStateExpireTick >= now2;
        }

        public float GetFlightLiftFactor01()
        {
            // ── 世界地图着陆模式：高度从 1.0 平滑衰减到 0.0 ──
            if (abilityRuntimeState.isWorldMapLanding)
            {
                int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
                int startTick = abilityRuntimeState.worldMapLandingStartTick;
                int duration = abilityRuntimeState.worldMapLandingDurationTicks;
                if (startTick < 0 || duration <= 0) return 0f;
                float progress = Mathf.Clamp01((float)(now - startTick) / duration);
                // 从 1.0 到 0.0，使用 SmoothStep 使末段更平滑
                return Mathf.SmoothStep(1f, 0f, progress);
            }

            if (!IsFlightStateActive() || abilityRuntimeState.flightStateStartTick < 0 || abilityRuntimeState.flightStateExpireTick < abilityRuntimeState.flightStateStartTick)
                return 0f;

            int now2 = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            
            // 落地冲刺逻辑：高度随位移百分比下降
            if (abilityRuntimeState.forcedMoveActive && abilityRuntimeState.forcedMoveIsLanding && abilityRuntimeState.forcedMoveTotalDistance > 0.001f)
            {
                return Mathf.Clamp01(1f - (abilityRuntimeState.forcedMoveTraveledDistance / abilityRuntimeState.forcedMoveTotalDistance));
            }

            // 正常的起飞/降落过渡
            const float TransitionTicks = 30f;
            float age = now2 - abilityRuntimeState.flightStateStartTick;
            float takeoffFactor = Mathf.Clamp01(age / TransitionTicks);
            
            // 降落平滑过渡 (30 ticks)
            float remaining = abilityRuntimeState.flightStateExpireTick - now2;
            float landingFactor = Mathf.Clamp01(remaining / TransitionTicks);

            // 使用 SmoothStep 让过渡更自然
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(takeoffFactor, landingFactor));
        }

        public float GetFlightHoverOffset()
        {
            if (!IsFlightStateActive()) return 0f;
            
            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            // 悬停抖动：周期约 2.5 秒，振幅 0.08
            float phase = (now % 150) / 150f * Mathf.PI * 2f;
            return Mathf.Sin(phase) * 0.08f;
        }

        // ── Shield Damage Absorption ──
        public bool TryAbsorbShieldDamage(ref DamageInfo dinfo)
        {
            if (abilityRuntimeState.shieldRemainingDamage <= 0f)
                return false;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (abilityRuntimeState.shieldExpireTick < now)
            {
                abilityRuntimeState.shieldRemainingDamage = 0f;
                abilityRuntimeState.shieldExpireTick = -1;
                abilityRuntimeState.shieldStoredHeal = 0f;
                abilityRuntimeState.shieldStoredBonusDamage = 0f;
                return false;
            }

            float incomingDamage = UnityEngine.Mathf.Max(0f, dinfo.Amount);
            if (incomingDamage <= 0.001f)
                return false;

            float absorbedDamage = UnityEngine.Mathf.Min(abilityRuntimeState.shieldRemainingDamage, incomingDamage);
            if (absorbedDamage <= 0.001f)
                return false;

            abilityRuntimeState.shieldRemainingDamage -= absorbedDamage;
            abilityRuntimeState.shieldStoredHeal += absorbedDamage;
            abilityRuntimeState.shieldStoredBonusDamage += absorbedDamage;

            lastShieldAbsorbTick = now;
            Vector3 impactDir = dinfo.Instigator != null
                ? (dinfo.Instigator.Position - parent.Position).ToVector3().normalized
                : Vector3.up;
            shieldImpactVector = new Vector3(impactDir.x, impactDir.z, 0f);

            float remainingDamage = incomingDamage - absorbedDamage;
            if (remainingDamage <= 0.001f)
                return true;

            dinfo = new DamageInfo(
                dinfo.Def, remainingDamage, dinfo.ArmorPenetrationInt, dinfo.Angle,
                dinfo.Instigator, dinfo.HitPart, dinfo.Weapon, dinfo.Category,
                dinfo.IntendedTarget, dinfo.InstigatorGuilty, dinfo.SpawnFilth,
                dinfo.WeaponQuality, dinfo.CheckForJobOverride, dinfo.PreventCascade);
            return false;
        }

        public bool IsAttachedShieldVisualActive()
        {
            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            return abilityRuntimeState.attachedShieldVisualExpireTick >= now && !string.IsNullOrWhiteSpace(abilityRuntimeState.attachedShieldVisualThingId);
        }

        public bool IsProjectileInterceptorShieldActive()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return abilityRuntimeState.projectileInterceptorShieldExpireTick >= now && !string.IsNullOrWhiteSpace(abilityRuntimeState.projectileInterceptorShieldThingId);
        }

        // ── Shield Visual Data (for rendering layer) ──
        public int LastShieldAbsorbTick => lastShieldAbsorbTick;
        public Vector3 ShieldImpactVector => shieldImpactVector;

        // ── Render Refresh Notification ──
        public void RequestSkinRenderRefresh()
        {
            Pawn? pawn = Pawn;
            if (pawn == null) return;
            pawn.GetComp<Core.CompPawnSkin>()?.RequestRenderRefresh();
        }

        // P-PERF: 同步飞行状态到 Patch_PawnRenderer 的 HashSet 快速跳过集合
        private void NotifyFlyingStateChanged()
        {
            Pawn? pawn = Pawn;
            if (pawn == null) return;
            Rendering.Patch_PawnRenderer.SetPawnFlying(pawn, IsFlightStateActive());
        }

        // ── World Map Landing Tick ──
        private void TickWorldMapLanding()
        {
            if (!abilityRuntimeState.isWorldMapLanding) return;

            int now = Find.TickManager?.TicksGame ?? 0;
            int endTick = abilityRuntimeState.worldMapLandingStartTick + abilityRuntimeState.worldMapLandingDurationTicks;

            if (now >= endTick)
            {
                // 着陆动画完成，清理着陆状态
                abilityRuntimeState.isWorldMapLanding = false;
                abilityRuntimeState.worldMapLandingStartTick = -1;
                NotifyFlyingStateChanged();

                Pawn? pawn = Pawn;
                if (pawn != null && pawn.Spawned && pawn.Map != null)
                {
                    // 着陆尘埃效果
                    FleckMaker.ThrowDustPuff(pawn.Position, pawn.Map, 1.5f);
                    FleckMaker.ThrowLightningGlow(pawn.DrawPos, pawn.Map, 1.5f);
                    Find.CameraDriver.shaker.DoShake(0.3f);

                    // 着陆后方向朝南
                    pawn.Rotation = Rot4.South;
                    RequestSkinRenderRefresh();
                }
            }
        }

        // ── Ability Face Override Tick ──
        private void TickAbilityFaceOverride()
        {
            if (abilityRuntimeState.abilityExpressionOverrideExpireTick < 0) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now > abilityRuntimeState.abilityExpressionOverrideExpireTick)
            {
                abilityRuntimeState.abilityExpressionOverrideExpireTick = -1;
                abilityRuntimeState.abilityExpressionOverride = null;
                abilityRuntimeState.abilityPupilBrightnessOffset = 0f;
                abilityRuntimeState.abilityPupilContrastOffset = 0f;
            }
        }

        // ── World Map Flight Handoff Tick ──
        private void TickWorldMapFlight()
        {
            int handoffTick = abilityRuntimeState.worldMapFlightHandoffTick;
            if (handoffTick < 0) return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < handoffTick) return;

            Pawn? pawn = Pawn;
            if (pawn == null || pawn.Map == null) { AbilityWorldMapFlightUtility.ClearWorldMapFlightState(pawn); return; }

            // 到达移交时间 → DeSpawn 并创建 TravellingTransporters
            AbilityWorldMapFlightUtility.ExecuteHandoff(pawn);
        }
    }

    public class CompProperties_CharacterAbilityRuntime : CompProperties
    {
        public CompProperties_CharacterAbilityRuntime()
        {
            compClass = typeof(CompCharacterAbilityRuntime);
        }
    }
}