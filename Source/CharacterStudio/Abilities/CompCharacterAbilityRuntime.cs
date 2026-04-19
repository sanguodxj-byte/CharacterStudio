using System;
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

        // ── R-Stack ──
        public bool RStackingEnabled
        {
            get => abilityRuntimeState.rStackingEnabled;
            set => abilityRuntimeState.rStackingEnabled = value;
        }
        public int RStackCount
        {
            get => abilityRuntimeState.rStackCount;
            set => abilityRuntimeState.rStackCount = value;
        }
        public bool RSecondStageReady
        {
            get => abilityRuntimeState.rSecondStageReady;
            set => abilityRuntimeState.rSecondStageReady = value;
        }
        public int RSecondStageExecuteTick
        {
            get => abilityRuntimeState.rSecondStageExecuteTick;
            set => abilityRuntimeState.rSecondStageExecuteTick = value;
        }
        public bool RSecondStageHasTarget
        {
            get => abilityRuntimeState.rSecondStageHasTarget;
            set => abilityRuntimeState.rSecondStageHasTarget = value;
        }
        public IntVec3 RSecondStageTargetCell
        {
            get => abilityRuntimeState.rSecondStageTargetCell;
            set => abilityRuntimeState.rSecondStageTargetCell = value;
        }
        public string RStackAbilityDefName
        {
            get => abilityRuntimeState.rStackAbilityDefName;
            set => abilityRuntimeState.rStackAbilityDefName = value ?? string.Empty;
        }

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
            set => abilityRuntimeState.flightStateExpireTick = value;
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
        private System.Action<IntVec3>? _dashCollisionCallback;
        private System.Action<IntVec3>? _dashCompleteCallback;

        public void SetDashCallbacks(System.Action<IntVec3>? onCollision, System.Action<IntVec3>? onComplete)
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
        public void BeginForcedMove(Vector2 direction, float distance, int durationTicks)
        {
            Pawn? pawn = Pawn;
            if (pawn?.Map == null || !pawn.Spawned || distance <= 0f || durationTicks <= 0)
                return;

            if (Mathf.Approximately(direction.sqrMagnitude, 0f))
                return;

            Vector2 dir = direction.normalized;

            abilityRuntimeState.forcedMoveActive = true;
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
            abilityRuntimeState.forcedMoveStepStartTick = abilityRuntimeState.forcedMoveStartTick;
            abilityRuntimeState.forcedMoveBusyUntilTick = abilityRuntimeState.forcedMoveStartTick + durationTicks + 6;
            abilityRuntimeState.forcedMoveCollisionTriggered = false;

            // 旧字段兼容
            abilityRuntimeState.forcedMoveDirectionX = (int)Mathf.Round(dir.x);
            abilityRuntimeState.forcedMoveDirectionZ = (int)Mathf.Round(dir.y);
            abilityRuntimeState.forcedMoveQueuedSteps = Mathf.CeilToInt(distance);
            abilityRuntimeState.forcedMoveStepDurationTicks = durationTicks;

            pawn.Rotation = RotFromDirection(dir.x, dir.y);

            if (pawn.stances?.stunner != null)
                pawn.stances.stunner.StunFor(Mathf.Max(2, durationTicks), pawn, false);

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
            IntVec3 nearestCell = new IntVec3(Mathf.RoundToInt(worldX), 0, Mathf.RoundToInt(worldZ));

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
                    pawn.Notify_Teleported(true, false);
                }
            }
            else
            {
                TriggerForcedMoveCollisionFeedback(pawn, pawn.Position, blockedImmediately: true);
                ClearForcedMoveState();
                return;
            }
            abilityRuntimeState.forcedMoveCurrentCell = pawn.Position;

            // 碰撞检测：只检查当前格子及相邻格子的 Pawn（避免遍历全地图）
            Pawn? hitPawn = null;
            IntVec3 checkCenter = pawn.Position;
            Map pawnMap = pawn.Map;
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
                            if (dist < closestDist)
                            {
                                closestDist = dist;
                                hitPawn = other;
                            }
                        }
                    }
                }
            }

            if (hitPawn != null && _dashCollisionCallback != null)
            {
                var cb = _dashCollisionCallback;
                _dashCollisionCallback = null;
                cb(pawn.Position);
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
            abilityRuntimeState.forcedMoveActive = false;
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
                cb(collisionCell);
            }

            RequestSkinRenderRefresh();
        }

        // ── Forced Move Query ──
        public bool IsForcedMoveBusy()
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            return abilityRuntimeState.forcedMoveActive || abilityRuntimeState.forcedMoveBusyUntilTick >= nowTick;
        }

        public Vector3 GetForcedMoveVisualOffset()
        {
            if (!abilityRuntimeState.forcedMoveActive || abilityRuntimeState.forcedMoveSpeedPerTick <= 0f)
                return Vector3.zero;

            float dx = abilityRuntimeState.forcedMoveDirFloatX * abilityRuntimeState.forcedMoveTraveledDistance;
            float dz = abilityRuntimeState.forcedMoveDirFloatZ * abilityRuntimeState.forcedMoveTraveledDistance;
            IntVec3 startCell = abilityRuntimeState.forcedMoveStartCell;
            // 精确世界位置减去当前格子位置
            float offsetX = (startCell.x + dx) - abilityRuntimeState.forcedMoveCurrentCell.x;
            float offsetZ = (startCell.z + dz) - abilityRuntimeState.forcedMoveCurrentCell.z;
            return new Vector3(offsetX, 0f, offsetZ);
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
            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            return abilityRuntimeState.flightStateExpireTick >= now;
        }

        public float GetFlightLiftFactor01()
        {
            if (!IsFlightStateActive() || abilityRuntimeState.flightStateStartTick < 0 || abilityRuntimeState.flightStateExpireTick < abilityRuntimeState.flightStateStartTick)
                return 0f;

            return 1f;
        }

        public float GetFlightHoverOffset() => 0f;

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
    }

    public class CompProperties_CharacterAbilityRuntime : CompProperties
    {
        public CompProperties_CharacterAbilityRuntime()
        {
            compClass = typeof(CompCharacterAbilityRuntime);
        }
    }
}