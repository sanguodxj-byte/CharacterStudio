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
        private CharacterAbilityLoadout? activeAbilityLoadout;

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

        internal readonly AbilityHotkeyRuntimeState abilityRuntimeState = new AbilityHotkeyRuntimeState();

        // 瞬时缓存，用于性能优化，不序列化
        internal Thing? attachedShieldVisualCached;
        internal Thing? projectileInterceptorShieldCached;
        private int lastShieldAbsorbTick = -9999;
        private Vector3 shieldImpactVector = Vector3.zero;

        public int slotOverrideWindowEndTick
        {
            get => abilityRuntimeState.slotOverrideWindowEndTick;
            set => abilityRuntimeState.slotOverrideWindowEndTick = value;
        }

        public string slotOverrideWindowAbilityDefName
        {
            get => abilityRuntimeState.slotOverrideWindowAbilityDefName;
            set => abilityRuntimeState.slotOverrideWindowAbilityDefName = value ?? string.Empty;
        }

        public string slotOverrideWindowSlotId
        {
            get => abilityRuntimeState.slotOverrideWindowSlotId;
            set => abilityRuntimeState.slotOverrideWindowSlotId = value ?? string.Empty;
        }

        public string qOverrideAbilityDefName
        {
            get => abilityRuntimeState.qOverrideAbilityDefName;
            set => abilityRuntimeState.qOverrideAbilityDefName = value ?? string.Empty;
        }

        public int qOverrideExpireTick
        {
            get => abilityRuntimeState.qOverrideExpireTick;
            set => abilityRuntimeState.qOverrideExpireTick = value;
        }

        public string wOverrideAbilityDefName
        {
            get => abilityRuntimeState.wOverrideAbilityDefName;
            set => abilityRuntimeState.wOverrideAbilityDefName = value ?? string.Empty;
        }

        public int wOverrideExpireTick
        {
            get => abilityRuntimeState.wOverrideExpireTick;
            set => abilityRuntimeState.wOverrideExpireTick = value;
        }

        public string eOverrideAbilityDefName
        {
            get => abilityRuntimeState.eOverrideAbilityDefName;
            set => abilityRuntimeState.eOverrideAbilityDefName = value ?? string.Empty;
        }

        public int eOverrideExpireTick
        {
            get => abilityRuntimeState.eOverrideExpireTick;
            set => abilityRuntimeState.eOverrideExpireTick = value;
        }

        public string rOverrideAbilityDefName
        {
            get => abilityRuntimeState.rOverrideAbilityDefName;
            set => abilityRuntimeState.rOverrideAbilityDefName = value ?? string.Empty;
        }

        public int rOverrideExpireTick
        {
            get => abilityRuntimeState.rOverrideExpireTick;
            set => abilityRuntimeState.rOverrideExpireTick = value;
        }

        public string tOverrideAbilityDefName
        {
            get => abilityRuntimeState.tOverrideAbilityDefName;
            set => abilityRuntimeState.tOverrideAbilityDefName = value ?? string.Empty;
        }

        public int tOverrideExpireTick
        {
            get => abilityRuntimeState.tOverrideExpireTick;
            set => abilityRuntimeState.tOverrideExpireTick = value;
        }

        public string aOverrideAbilityDefName
        {
            get => abilityRuntimeState.aOverrideAbilityDefName;
            set => abilityRuntimeState.aOverrideAbilityDefName = value ?? string.Empty;
        }

        public int aOverrideExpireTick
        {
            get => abilityRuntimeState.aOverrideExpireTick;
            set => abilityRuntimeState.aOverrideExpireTick = value;
        }

        public string sOverrideAbilityDefName
        {
            get => abilityRuntimeState.sOverrideAbilityDefName;
            set => abilityRuntimeState.sOverrideAbilityDefName = value ?? string.Empty;
        }

        public int sOverrideExpireTick
        {
            get => abilityRuntimeState.sOverrideExpireTick;
            set => abilityRuntimeState.sOverrideExpireTick = value;
        }

        public string dOverrideAbilityDefName
        {
            get => abilityRuntimeState.dOverrideAbilityDefName;
            set => abilityRuntimeState.dOverrideAbilityDefName = value ?? string.Empty;
        }

        public int dOverrideExpireTick
        {
            get => abilityRuntimeState.dOverrideExpireTick;
            set => abilityRuntimeState.dOverrideExpireTick = value;
        }

        public string fOverrideAbilityDefName
        {
            get => abilityRuntimeState.fOverrideAbilityDefName;
            set => abilityRuntimeState.fOverrideAbilityDefName = value ?? string.Empty;
        }

        public int fOverrideExpireTick
        {
            get => abilityRuntimeState.fOverrideExpireTick;
            set => abilityRuntimeState.fOverrideExpireTick = value;
        }

        public string zOverrideAbilityDefName
        {
            get => abilityRuntimeState.zOverrideAbilityDefName;
            set => abilityRuntimeState.zOverrideAbilityDefName = value ?? string.Empty;
        }

        public int zOverrideExpireTick
        {
            get => abilityRuntimeState.zOverrideExpireTick;
            set => abilityRuntimeState.zOverrideExpireTick = value;
        }

        public string xOverrideAbilityDefName
        {
            get => abilityRuntimeState.xOverrideAbilityDefName;
            set => abilityRuntimeState.xOverrideAbilityDefName = value ?? string.Empty;
        }

        public int xOverrideExpireTick
        {
            get => abilityRuntimeState.xOverrideExpireTick;
            set => abilityRuntimeState.xOverrideExpireTick = value;
        }

        public string cOverrideAbilityDefName
        {
            get => abilityRuntimeState.cOverrideAbilityDefName;
            set => abilityRuntimeState.cOverrideAbilityDefName = value ?? string.Empty;
        }

        public int cOverrideExpireTick
        {
            get => abilityRuntimeState.cOverrideExpireTick;
            set => abilityRuntimeState.cOverrideExpireTick = value;
        }

        public string vOverrideAbilityDefName
        {
            get => abilityRuntimeState.vOverrideAbilityDefName;
            set => abilityRuntimeState.vOverrideAbilityDefName = value ?? string.Empty;
        }

        public int vOverrideExpireTick
        {
            get => abilityRuntimeState.vOverrideExpireTick;
            set => abilityRuntimeState.vOverrideExpireTick = value;
        }

        // 各槽位技能 CD（单位：Tick）
        // E 槽由 AbilityHotkeyRuntimeComponent 写入，Q/W/R 由统一门控写入
        public int qCooldownUntilTick
        {
            get => abilityRuntimeState.qCooldownUntilTick;
            set => abilityRuntimeState.qCooldownUntilTick = value;
        }

        public int wCooldownUntilTick
        {
            get => abilityRuntimeState.wCooldownUntilTick;
            set => abilityRuntimeState.wCooldownUntilTick = value;
        }

        public int eCooldownUntilTick
        {
            get => abilityRuntimeState.eCooldownUntilTick;
            set => abilityRuntimeState.eCooldownUntilTick = value;
        }

        public int rCooldownUntilTick
        {
            get => abilityRuntimeState.rCooldownUntilTick;
            set => abilityRuntimeState.rCooldownUntilTick = value;
        }

        public int tCooldownUntilTick
        {
            get => abilityRuntimeState.tCooldownUntilTick;
            set => abilityRuntimeState.tCooldownUntilTick = value;
        }

        public int aCooldownUntilTick
        {
            get => abilityRuntimeState.aCooldownUntilTick;
            set => abilityRuntimeState.aCooldownUntilTick = value;
        }

        public int sCooldownUntilTick
        {
            get => abilityRuntimeState.sCooldownUntilTick;
            set => abilityRuntimeState.sCooldownUntilTick = value;
        }

        public int dCooldownUntilTick
        {
            get => abilityRuntimeState.dCooldownUntilTick;
            set => abilityRuntimeState.dCooldownUntilTick = value;
        }

        public int fCooldownUntilTick
        {
            get => abilityRuntimeState.fCooldownUntilTick;
            set => abilityRuntimeState.fCooldownUntilTick = value;
        }

        public int zCooldownUntilTick
        {
            get => abilityRuntimeState.zCooldownUntilTick;
            set => abilityRuntimeState.zCooldownUntilTick = value;
        }

        public int xCooldownUntilTick
        {
            get => abilityRuntimeState.xCooldownUntilTick;
            set => abilityRuntimeState.xCooldownUntilTick = value;
        }

        public int cCooldownUntilTick
        {
            get => abilityRuntimeState.cCooldownUntilTick;
            set => abilityRuntimeState.cCooldownUntilTick = value;
        }

        public int vCooldownUntilTick
        {
            get => abilityRuntimeState.vCooldownUntilTick;
            set => abilityRuntimeState.vCooldownUntilTick = value;
        }

        // R 两段机制状态
        public bool rStackingEnabled
        {
            get => abilityRuntimeState.rStackingEnabled;
            set => abilityRuntimeState.rStackingEnabled = value;
        }

        public int rStackCount
        {
            get => abilityRuntimeState.rStackCount;
            set => abilityRuntimeState.rStackCount = value;
        }

        public bool rSecondStageReady
        {
            get => abilityRuntimeState.rSecondStageReady;
            set => abilityRuntimeState.rSecondStageReady = value;
        }

        public int rSecondStageExecuteTick
        {
            get => abilityRuntimeState.rSecondStageExecuteTick;
            set => abilityRuntimeState.rSecondStageExecuteTick = value;
        }

        public bool rSecondStageHasTarget
        {
            get => abilityRuntimeState.rSecondStageHasTarget;
            set => abilityRuntimeState.rSecondStageHasTarget = value;
        }

        public IntVec3 rSecondStageTargetCell
        {
            get => abilityRuntimeState.rSecondStageTargetCell;
            set => abilityRuntimeState.rSecondStageTargetCell = value;
        }

        public string rStackAbilityDefName
        {
            get => abilityRuntimeState.rStackAbilityDefName;
            set => abilityRuntimeState.rStackAbilityDefName = value ?? string.Empty;
        }

        // 状态武器视觉：施法中窗口
        public int weaponCarryCastingUntilTick
        {
            get => abilityRuntimeState.weaponCarryCastingUntilTick;
            set => abilityRuntimeState.weaponCarryCastingUntilTick = value;
        }

        // 周期脉冲运行时状态
        public int periodicPulseNextTick
        {
            get => abilityRuntimeState.periodicPulseNextTick;
            set => abilityRuntimeState.periodicPulseNextTick = value;
        }

        public int periodicPulseEndTick
        {
            get => abilityRuntimeState.periodicPulseEndTick;
            set => abilityRuntimeState.periodicPulseEndTick = value;
        }

        // 护盾吸收运行时状态
        public float shieldRemainingDamage
        {
            get => abilityRuntimeState.shieldRemainingDamage;
            set => abilityRuntimeState.shieldRemainingDamage = value;
        }

        public int shieldExpireTick
        {
            get => abilityRuntimeState.shieldExpireTick;
            set => abilityRuntimeState.shieldExpireTick = value;
        }

        public float shieldStoredHeal
        {
            get => abilityRuntimeState.shieldStoredHeal;
            set => abilityRuntimeState.shieldStoredHeal = value;
        }

        public float shieldStoredBonusDamage
        {
            get => abilityRuntimeState.shieldStoredBonusDamage;
            set => abilityRuntimeState.shieldStoredBonusDamage = value;
        }

        public int attachedShieldVisualExpireTick
        {
            get => abilityRuntimeState.attachedShieldVisualExpireTick;
            set => abilityRuntimeState.attachedShieldVisualExpireTick = value;
        }

        public float attachedShieldVisualScale
        {
            get => abilityRuntimeState.attachedShieldVisualScale;
            set => abilityRuntimeState.attachedShieldVisualScale = value;
        }

        public float attachedShieldVisualHeightOffset
        {
            get => abilityRuntimeState.attachedShieldVisualHeightOffset;
            set => abilityRuntimeState.attachedShieldVisualHeightOffset = value;
        }

        public string attachedShieldVisualThingId
        {
            get => abilityRuntimeState.attachedShieldVisualThingId;
            set => abilityRuntimeState.attachedShieldVisualThingId = value ?? string.Empty;
        }

        public int projectileInterceptorShieldExpireTick
        {
            get => abilityRuntimeState.projectileInterceptorShieldExpireTick;
            set => abilityRuntimeState.projectileInterceptorShieldExpireTick = value;
        }

        public string projectileInterceptorShieldThingId
        {
            get => abilityRuntimeState.projectileInterceptorShieldThingId;
            set => abilityRuntimeState.projectileInterceptorShieldThingId = value ?? string.Empty;
        }

        public int offensiveMarkExpireTick
        {
            get => abilityRuntimeState.offensiveMarkExpireTick;
            set => abilityRuntimeState.offensiveMarkExpireTick = value;
        }

        public int offensiveMarkStacks
        {
            get => abilityRuntimeState.offensiveMarkStacks;
            set => abilityRuntimeState.offensiveMarkStacks = value;
        }

        public int offensiveComboExpireTick
        {
            get => abilityRuntimeState.offensiveComboExpireTick;
            set => abilityRuntimeState.offensiveComboExpireTick = value;
        }

        public int offensiveComboStacks
        {
            get => abilityRuntimeState.offensiveComboStacks;
            set => abilityRuntimeState.offensiveComboStacks = value;
        }

        public int dashEmpowerExpireTick
        {
            get => abilityRuntimeState.dashEmpowerExpireTick;
            set => abilityRuntimeState.dashEmpowerExpireTick = value;
        }

        public int flightStateStartTick
        {
            get => abilityRuntimeState.flightStateStartTick;
            set => abilityRuntimeState.flightStateStartTick = value;
        }

        public int flightStateExpireTick
        {
            get => abilityRuntimeState.flightStateExpireTick;
            set => abilityRuntimeState.flightStateExpireTick = value;
        }

        public float flightStateHeightFactor
        {
            get => abilityRuntimeState.flightStateHeightFactor;
            set => abilityRuntimeState.flightStateHeightFactor = value;
        }

        public bool IsFlightStateActive()
        {
            if (Pawn?.flight?.Flying == true)
            {
                return false;
            }

            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            return flightStateExpireTick >= now;
        }

        public bool IsAttachedShieldVisualActive()
        {
            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            return attachedShieldVisualExpireTick >= now && !string.IsNullOrWhiteSpace(attachedShieldVisualThingId);
        }

        public bool IsProjectileInterceptorShieldActive()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return projectileInterceptorShieldExpireTick >= now && !string.IsNullOrWhiteSpace(projectileInterceptorShieldThingId);
        }

        public string triggeredEquipmentAnimationAbilityDefName
        {
            get => abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName;
            set => abilityRuntimeState.triggeredEquipmentAnimationAbilityDefName = value ?? string.Empty;
        }

        public int triggeredEquipmentAnimationStartTick
        {
            get => abilityRuntimeState.triggeredEquipmentAnimationStartTick;
            set => abilityRuntimeState.triggeredEquipmentAnimationStartTick = value;
        }

        public float GetFlightLiftFactor01()
        {
            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            if (!IsFlightStateActive() || flightStateStartTick < 0 || flightStateExpireTick < flightStateStartTick)
                return 0f;

            const int easeTicks = 18;
            float fadeIn = Mathf.Clamp01((now - flightStateStartTick) / (float)easeTicks);
            float fadeOut = Mathf.Clamp01((flightStateExpireTick - now) / (float)easeTicks);
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
        }

        public float GetFlightHoverOffset()
        {
            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            if (!IsFlightStateActive()) return 0f;
            float amplitude = Mathf.Max(0.015f, flightStateHeightFactor * 0.18f) * GetFlightLiftFactor01();
            return Mathf.Sin((now + (Pawn?.thingIDNumber ?? 0)) * 0.14f) * amplitude;
        }

        public int triggeredEquipmentAnimationEndTick
        {
            get => abilityRuntimeState.triggeredEquipmentAnimationEndTick;
            set => abilityRuntimeState.triggeredEquipmentAnimationEndTick = value;
        }

        public bool suppressCombatActionsDuringFlightState
        {
            get => abilityRuntimeState.suppressCombatActionsDuringFlightState;
            set => abilityRuntimeState.suppressCombatActionsDuringFlightState = value;
        }

        public bool isInVanillaFlight
        {
            get => abilityRuntimeState.isInVanillaFlight;
            set => abilityRuntimeState.isInVanillaFlight = value;
        }

        public int vanillaFlightStartTick
        {
            get => abilityRuntimeState.vanillaFlightStartTick;
            set => abilityRuntimeState.vanillaFlightStartTick = value;
        }

        public int vanillaFlightExpireTick
        {
            get => abilityRuntimeState.vanillaFlightExpireTick;
            set => abilityRuntimeState.vanillaFlightExpireTick = value;
        }

        public string vanillaFlightSourceAbilityDefName
        {
            get => abilityRuntimeState.vanillaFlightSourceAbilityDefName;
            set => abilityRuntimeState.vanillaFlightSourceAbilityDefName = value ?? string.Empty;
        }

        public string vanillaFlightFollowupAbilityDefName
        {
            get => abilityRuntimeState.vanillaFlightFollowupAbilityDefName;
            set => abilityRuntimeState.vanillaFlightFollowupAbilityDefName = value ?? string.Empty;
        }

        public IntVec3 vanillaFlightReservedTargetCell
        {
            get => abilityRuntimeState.vanillaFlightReservedTargetCell;
            set => abilityRuntimeState.vanillaFlightReservedTargetCell = value;
        }

        public bool vanillaFlightHasReservedTargetCell
        {
            get => abilityRuntimeState.vanillaFlightHasReservedTargetCell;
            set => abilityRuntimeState.vanillaFlightHasReservedTargetCell = value;
        }

        public int vanillaFlightFollowupWindowEndTick
        {
            get => abilityRuntimeState.vanillaFlightFollowupWindowEndTick;
            set => abilityRuntimeState.vanillaFlightFollowupWindowEndTick = value;
        }

        public bool vanillaFlightPendingLandingBurst
        {
            get => abilityRuntimeState.vanillaFlightPendingLandingBurst;
            set => abilityRuntimeState.vanillaFlightPendingLandingBurst = value;
        }

        public bool IsTriggeredEquipmentAnimationActive(string? abilityDefName)
        {
            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(Pawn, Find.TickManager?.TicksGame ?? 0);
            return triggeredEquipmentAnimationEndTick >= now
                && !string.IsNullOrWhiteSpace(triggeredEquipmentAnimationAbilityDefName)
                && (string.IsNullOrWhiteSpace(abilityDefName)
                    || string.Equals(triggeredEquipmentAnimationAbilityDefName, abilityDefName, StringComparison.OrdinalIgnoreCase));
        }

        public void TriggerEquipmentAnimationState(string triggerKey, int startTick, int durationTicks)
        {
            if (string.IsNullOrWhiteSpace(triggerKey))
            {
                return;
            }

            triggeredEquipmentAnimationAbilityDefName = triggerKey;
            triggeredEquipmentAnimationStartTick = startTick;
            triggeredEquipmentAnimationEndTick = startTick + Math.Max(1, durationTicks);
            RequestRenderRefresh();
        }

        public void ClearEquipmentAnimationState(string? triggerKey = null)
        {
            if (!string.IsNullOrWhiteSpace(triggerKey)
                && !string.Equals(triggeredEquipmentAnimationAbilityDefName, triggerKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            triggeredEquipmentAnimationAbilityDefName = string.Empty;
            triggeredEquipmentAnimationStartTick = -1;
            triggeredEquipmentAnimationEndTick = -1;
            RequestRenderRefresh();
        }

        public int abilityExpressionOverrideExpireTick
        {
            get => abilityRuntimeState.abilityExpressionOverrideExpireTick;
            set => abilityRuntimeState.abilityExpressionOverrideExpireTick = value;
        }

        public ExpressionType? abilityExpressionOverride
        {
            get => abilityRuntimeState.abilityExpressionOverride;
            set => abilityRuntimeState.abilityExpressionOverride = value;
        }

        public float abilityPupilBrightnessOffset
        {
            get => abilityRuntimeState.abilityPupilBrightnessOffset;
            set => abilityRuntimeState.abilityPupilBrightnessOffset = value;
        }

        public float abilityPupilContrastOffset
        {
            get => abilityRuntimeState.abilityPupilContrastOffset;
            set => abilityRuntimeState.abilityPupilContrastOffset = value;
        }

        public bool forcedMoveActive
        {
            get => abilityRuntimeState.forcedMoveActive;
            set => abilityRuntimeState.forcedMoveActive = value;
        }

        public IntVec3 forcedMoveStartCell
        {
            get => abilityRuntimeState.forcedMoveStartCell;
            set => abilityRuntimeState.forcedMoveStartCell = value;
        }

        public IntVec3 forcedMoveCurrentCell
        {
            get => abilityRuntimeState.forcedMoveCurrentCell;
            set => abilityRuntimeState.forcedMoveCurrentCell = value;
        }

        public IntVec3 forcedMoveNextCell
        {
            get => abilityRuntimeState.forcedMoveNextCell;
            set => abilityRuntimeState.forcedMoveNextCell = value;
        }

        public int forcedMoveStepStartTick
        {
            get => abilityRuntimeState.forcedMoveStepStartTick;
            set => abilityRuntimeState.forcedMoveStepStartTick = value;
        }

        public int forcedMoveStepDurationTicks
        {
            get => abilityRuntimeState.forcedMoveStepDurationTicks;
            set => abilityRuntimeState.forcedMoveStepDurationTicks = value;
        }

        public int forcedMoveQueuedSteps
        {
            get => abilityRuntimeState.forcedMoveQueuedSteps;
            set => abilityRuntimeState.forcedMoveQueuedSteps = value;
        }

        public int forcedMoveDirectionX
        {
            get => abilityRuntimeState.forcedMoveDirectionX;
            set => abilityRuntimeState.forcedMoveDirectionX = value;
        }

        public int forcedMoveDirectionZ
        {
            get => abilityRuntimeState.forcedMoveDirectionZ;
            set => abilityRuntimeState.forcedMoveDirectionZ = value;
        }

        public int forcedMoveBusyUntilTick
        {
            get => abilityRuntimeState.forcedMoveBusyUntilTick;
            set => abilityRuntimeState.forcedMoveBusyUntilTick = value;
        }

        public bool forcedMoveCollisionTriggered
        {
            get => abilityRuntimeState.forcedMoveCollisionTriggered;
            set => abilityRuntimeState.forcedMoveCollisionTriggered = value;
        }

        public Pawn? Pawn => parent as Pawn;

        public override void CompTick()
        {
            base.CompTick();
            Pawn? pawn = Pawn;
            if (pawn == null || !pawn.Spawned) return;

            TickForcedMove(pawn);
            FaceRuntimeRefreshCoordinator.FlushDeferredRefresh(this);
            TickAbilityFaceOverride();

            if (FaceRuntimeActivationGuard.IsFaceRuntimeEnabled(this))
            {
                FaceRuntimeTickCoordinator.Tick(this, pawn);
            }
        }

        public void BeginForcedMove(IntVec3 direction, int steps, int stepDurationTicks = 4)
        {
            Pawn? pawn = Pawn;
            if (pawn?.Map == null || !pawn.Spawned || steps <= 0)
            {
                return;
            }

            int dirX = Math.Sign(direction.x);
            int dirZ = Math.Sign(direction.z);
            if (dirX == 0 && dirZ == 0)
            {
                return;
            }

            forcedMoveActive = true;
            forcedMoveDirectionX = dirX;
            forcedMoveDirectionZ = dirZ;
            forcedMoveQueuedSteps = Math.Max(1, steps);
            forcedMoveStepDurationTicks = Math.Max(2, stepDurationTicks);
            forcedMoveCurrentCell = pawn.Position;
            forcedMoveStartCell = pawn.Position;
            forcedMoveCollisionTriggered = false;
            forcedMoveNextCell = ResolveNextForcedMoveCell(pawn, forcedMoveCurrentCell);
            forcedMoveStepStartTick = Find.TickManager?.TicksGame ?? 0;
            forcedMoveBusyUntilTick = forcedMoveStepStartTick + (forcedMoveQueuedSteps * forcedMoveStepDurationTicks) + 6;

            if (pawn.stances?.stunner != null)
            {
                pawn.stances.stunner.StunFor(Math.Max(2, forcedMoveStepDurationTicks), pawn, false);
            }

            if (!forcedMoveNextCell.IsValid || forcedMoveNextCell == forcedMoveCurrentCell)
            {
                TriggerForcedMoveCollisionFeedback(pawn, pawn.Position, blockedImmediately: true);
                ClearForcedMoveState();
                return;
            }

            RequestRenderRefresh();
        }

        public Vector3 GetForcedMoveVisualOffset()
        {
            Pawn? pawn = Pawn;
            if (pawn == null || !forcedMoveActive || !forcedMoveCurrentCell.IsValid || !forcedMoveNextCell.IsValid)
            {
                return Vector3.zero;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            int duration = Math.Max(1, forcedMoveStepDurationTicks);
            float t = Mathf.Clamp01((now - forcedMoveStepStartTick) / (float)duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            IntVec3 delta = forcedMoveNextCell - forcedMoveCurrentCell;
            return new Vector3(delta.x * eased, 0f, delta.z * eased);
        }

        private void TickForcedMove(Pawn pawn)
        {
            if (!forcedMoveActive)
            {
                return;
            }

            if (pawn.Map == null || !pawn.Spawned)
            {
                ClearForcedMoveState();
                return;
            }

            if (!forcedMoveCurrentCell.IsValid)
            {
                forcedMoveCurrentCell = pawn.Position;
            }

            if (!forcedMoveNextCell.IsValid || forcedMoveNextCell == forcedMoveCurrentCell)
            {
                forcedMoveNextCell = ResolveNextForcedMoveCell(pawn, forcedMoveCurrentCell);
                if (!forcedMoveNextCell.IsValid || forcedMoveNextCell == forcedMoveCurrentCell)
                {
                    TriggerForcedMoveCollisionFeedback(pawn, forcedMoveCurrentCell, blockedImmediately: true);
                    ClearForcedMoveState();
                    return;
                }
                forcedMoveStepStartTick = Find.TickManager?.TicksGame ?? 0;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now - forcedMoveStepStartTick < Math.Max(1, forcedMoveStepDurationTicks))
            {
                return;
            }

            pawn.Position = forcedMoveNextCell;
            pawn.Notify_Teleported(true, false);

            forcedMoveCurrentCell = forcedMoveNextCell;
            forcedMoveQueuedSteps--;

            if (forcedMoveQueuedSteps <= 0)
            {
                ClearForcedMoveState();
                return;
            }

            forcedMoveStartCell = forcedMoveCurrentCell;
            forcedMoveNextCell = ResolveNextForcedMoveCell(pawn, forcedMoveCurrentCell);
            forcedMoveStepStartTick = now;
            forcedMoveBusyUntilTick = Math.Max(forcedMoveBusyUntilTick, now + forcedMoveStepDurationTicks + 4);

            if (!forcedMoveNextCell.IsValid || forcedMoveNextCell == forcedMoveCurrentCell)
            {
                TriggerForcedMoveCollisionFeedback(pawn, forcedMoveCurrentCell, blockedImmediately: false);
                ClearForcedMoveState();
                return;
            }

            RequestRenderRefresh();
        }

        private IntVec3 ResolveNextForcedMoveCell(Pawn pawn, IntVec3 origin)
        {
            Map? map = pawn.Map;
            if (map == null)
            {
                return origin;
            }

            IntVec3 next = origin + new IntVec3(forcedMoveDirectionX, 0, forcedMoveDirectionZ);
            if (!next.InBounds(map) || !next.Standable(map))
            {
                return origin;
            }

            return next;
        }

        private void ClearForcedMoveState()
        {
            forcedMoveActive = false;
            forcedMoveQueuedSteps = 0;
            forcedMoveDirectionX = 0;
            forcedMoveDirectionZ = 0;
            forcedMoveBusyUntilTick = -1;
            forcedMoveCollisionTriggered = false;
            forcedMoveStartCell = IntVec3.Invalid;
            forcedMoveCurrentCell = IntVec3.Invalid;
            forcedMoveNextCell = IntVec3.Invalid;
            forcedMoveStepStartTick = -1;
        }

        public bool IsForcedMoveBusy()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return forcedMoveActive || forcedMoveBusyUntilTick >= now;
        }

        private void TriggerForcedMoveCollisionFeedback(Pawn pawn, IntVec3 collisionCell, bool blockedImmediately)
        {
            if (forcedMoveCollisionTriggered || pawn.Map == null)
            {
                return;
            }

            forcedMoveCollisionTriggered = true;
            float dustScale = blockedImmediately ? 1.6f : 1.1f;
            FleckMaker.ThrowDustPuff(collisionCell, pawn.Map, dustScale);
            FleckMaker.ThrowMicroSparks(pawn.DrawPos, pawn.Map);

            if (pawn.stances?.stunner != null)
            {
                pawn.stances.stunner.StunFor(blockedImmediately ? 12 : 8, pawn, false);
            }

            forcedMoveBusyUntilTick = Math.Max(forcedMoveBusyUntilTick, (Find.TickManager?.TicksGame ?? 0) + (blockedImmediately ? 12 : 8));
            RequestRenderRefresh();
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            absorbed = TryAbsorbShieldDamage(ref dinfo);

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

        private bool TryAbsorbShieldDamage(ref DamageInfo dinfo)
        {
            if (shieldRemainingDamage <= 0f)
            {
                return false;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            if (shieldExpireTick < now)
            {
                shieldRemainingDamage = 0f;
                shieldExpireTick = -1;
                shieldStoredHeal = 0f;
                shieldStoredBonusDamage = 0f;
                return false;
            }

            float incomingDamage = Mathf.Max(0f, dinfo.Amount);
            if (incomingDamage <= 0.001f)
            {
                return false;
            }

            float absorbedDamage = Mathf.Min(shieldRemainingDamage, incomingDamage);
            if (absorbedDamage <= 0.001f)
            {
                return false;
            }

            shieldRemainingDamage -= absorbedDamage;
            shieldStoredHeal += absorbedDamage;
            shieldStoredBonusDamage += absorbedDamage;

            // 记录撞击效果数据 (仿原版护盾腰带)
            lastShieldAbsorbTick = now;
            Vector3 impactDir = dinfo.Instigator != null ? (dinfo.Instigator.Position - parent.Position).ToVector3().normalized : Vector3.up;
            shieldImpactVector = new Vector3(impactDir.x, impactDir.z, 0f);

            float remainingDamage = incomingDamage - absorbedDamage;
            if (remainingDamage <= 0.001f)
            {
                return true;
            }

            dinfo = new DamageInfo(
                dinfo.Def,
                remainingDamage,
                dinfo.ArmorPenetrationInt,
                dinfo.Angle,
                dinfo.Instigator,
                dinfo.HitPart,
                dinfo.Weapon,
                dinfo.Category,
                dinfo.IntendedTarget,
                dinfo.InstigatorGuilty,
                dinfo.SpawnFilth,
                dinfo.WeaponQuality,
                dinfo.CheckForJobOverride,
                dinfo.PreventCascade);

            return false;
        }

        public void ApplyAbilityFaceOverride(ExpressionType? expression, int durationTicks, float pupilBrightnessOffset, float pupilContrastOffset)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int resolvedDuration = Math.Max(1, durationTicks);
            abilityExpressionOverride = expression;
            abilityExpressionOverrideExpireTick = now + resolvedDuration;
            abilityPupilBrightnessOffset = pupilBrightnessOffset;
            abilityPupilContrastOffset = pupilContrastOffset;

            if (expression.HasValue)
            {
                faceExpressionState.ClearBlink();
                faceExpressionState.ResetAnimatedFrameTracking();
            }

            RequestRenderRefresh();
        }

        public bool IsAbilityExpressionOverrideActive()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return abilityExpressionOverrideExpireTick >= now
                && (abilityExpressionOverride.HasValue
                    || Math.Abs(abilityPupilBrightnessOffset) > 0.001f
                    || Math.Abs(abilityPupilContrastOffset) > 0.001f);
        }

        public float GetAbilityPupilBrightnessOffset()
            => IsAbilityExpressionOverrideActive() ? abilityPupilBrightnessOffset : 0f;

        public float GetAbilityPupilContrastOffset()
            => IsAbilityExpressionOverrideActive() ? abilityPupilContrastOffset : 0f;

        private void TickAbilityFaceOverride()
        {
            if (abilityExpressionOverrideExpireTick < 0)
            {
                return;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            if (abilityExpressionOverrideExpireTick >= now)
            {
                return;
            }

            if (abilityExpressionOverride.HasValue
                || Math.Abs(abilityPupilBrightnessOffset) > 0.001f
                || Math.Abs(abilityPupilContrastOffset) > 0.001f)
            {
                abilityExpressionOverride = null;
                abilityExpressionOverrideExpireTick = -1;
                abilityPupilBrightnessOffset = 0f;
                abilityPupilContrastOffset = 0f;
                faceExpressionState.ResetAnimatedFrameTracking();
                RequestRenderRefresh();
            }
        }

        public void SetWeaponCarryCastingWindow(int durationTicks)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int resolvedDuration = Math.Max(1, durationTicks);
            abilityRuntimeState.weaponCarryCastingUntilTick = now + resolvedDuration;
            RequestRenderRefresh();
        }

        public bool IsWeaponCarryCastingNow()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return abilityRuntimeState.weaponCarryCastingUntilTick >= now;
        }

        public int GetExpressionAnimTick() => faceExpressionState.expressionAnimTick;

        private static readonly Material DefaultShieldBubbleMat = MaterialPool.MatFrom("Things/Pawn/Effects/Shield", ShaderDatabase.Transparent);

        public override void PostDraw()
        {
            base.PostDraw();

            Pawn? pawn = Pawn;
            if (pawn == null) return;

            if (IsAttachedShieldVisualActive())
            {
                // 仿原版护盾腰带绘制逻辑 (ShieldBelt.DrawWornExtras)
                // 基础大小随剩余护盾值微调 (模拟能量脉冲感)
                float energyPercent = (shieldRemainingDamage > 0) ? Mathf.Clamp01(shieldRemainingDamage / 60f) : 1f;
                float baseScale = attachedShieldVisualScale;
                float drawScale = baseScale * (0.9f + (energyPercent * 0.2f));
                
                Vector3 pos = pawn.Drawer.DrawPos;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + attachedShieldVisualHeightOffset;

                // 处理撞击抖动效果
                int ticksSinceImpact = Find.TickManager.TicksGame - lastShieldAbsorbTick;
                if (ticksSinceImpact < 8)
                {
                    float jitterFactor = (float)(8 - ticksSinceImpact) / 8f * 0.05f;
                    pos.x -= shieldImpactVector.x * jitterFactor;
                    pos.z -= shieldImpactVector.y * jitterFactor;
                    drawScale -= jitterFactor;
                }

                // 旋转逻辑：使用稳定的随机种子或基于时间，避免逐帧乱闪
                float angle = (float)(pawn.thingIDNumber % 360) + (Find.TickManager.TicksGame * 0.5f) % 360f;
                
                Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(drawScale, 1f, drawScale));

                Material? mat = null;
                if (attachedShieldVisualCached != null)
                {
                    mat = attachedShieldVisualCached.Graphic?.MatSingle;
                }
                
                // 如果没有指定特殊材质，使用原版护盾材质
                mat ??= DefaultShieldBubbleMat;

                if (mat != null)
                {
                    Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);
                }
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