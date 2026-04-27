using System;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 技能运行时状态数据（冷却、槽位覆写、护盾、飞行、强制位移等）。
    /// 已从 CompPawnSkin 剥离至独立的 Abilities 命名空间。
    /// </summary>
    public sealed class AbilityHotkeyRuntimeState
    {
        public int slotOverrideWindowEndTick = 0;
        public string slotOverrideWindowAbilityDefName = string.Empty;
        public string slotOverrideWindowSlotId = string.Empty;

        // Override Ability DefNames
        public string qOverrideAbilityDefName = string.Empty;
        public int qOverrideExpireTick = -1;
        public string wOverrideAbilityDefName = string.Empty;
        public int wOverrideExpireTick = -1;
        public string eOverrideAbilityDefName = string.Empty;
        public int eOverrideExpireTick = -1;
        public string rOverrideAbilityDefName = string.Empty;
        public int rOverrideExpireTick = -1;
        public string tOverrideAbilityDefName = string.Empty;
        public int tOverrideExpireTick = -1;
        public string aOverrideAbilityDefName = string.Empty;
        public int aOverrideExpireTick = -1;
        public string sOverrideAbilityDefName = string.Empty;
        public int sOverrideExpireTick = -1;
        public string dOverrideAbilityDefName = string.Empty;
        public int dOverrideExpireTick = -1;
        public string fOverrideAbilityDefName = string.Empty;
        public int fOverrideExpireTick = -1;
        public string zOverrideAbilityDefName = string.Empty;
        public int zOverrideExpireTick = -1;
        public string xOverrideAbilityDefName = string.Empty;
        public int xOverrideExpireTick = -1;
        public string cOverrideAbilityDefName = string.Empty;
        public int cOverrideExpireTick = -1;
        public string vOverrideAbilityDefName = string.Empty;
        public int vOverrideExpireTick = -1;

        // Cooldowns
        public int qCooldownUntilTick = 0;
        public int wCooldownUntilTick = 0;
        public int eCooldownUntilTick = 0;
        public int rCooldownUntilTick = 0;
        public int tCooldownUntilTick = 0;
        public int aCooldownUntilTick = 0;
        public int sCooldownUntilTick = 0;
        public int dCooldownUntilTick = 0;
        public int fCooldownUntilTick = 0;
        public int zCooldownUntilTick = 0;
        public int xCooldownUntilTick = 0;
        public int cCooldownUntilTick = 0;
        public int vCooldownUntilTick = 0;

        public bool rStackingEnabled = false;
        public int rStackCount = 0;
        public bool rSecondStageReady = false;
        public int rSecondStageExecuteTick = -1;
        public bool rSecondStageHasTarget = false;
        public IntVec3 rSecondStageTargetCell = IntVec3.Invalid;
        public string rStackAbilityDefName = string.Empty;

        public int weaponCarryCastingUntilTick = -1;
        public int periodicPulseNextTick = -1;
        public int periodicPulseEndTick = -1;

        public float shieldRemainingDamage = 0f;
        public int shieldExpireTick = -1;
        public float shieldStoredHeal = 0f;
        public float shieldStoredBonusDamage = 0f;

        public int attachedShieldVisualExpireTick = -1;
        public float attachedShieldVisualScale = 1f;
        public float attachedShieldVisualHeightOffset = 0f;
        public string attachedShieldVisualThingId = string.Empty;

        public int projectileInterceptorShieldExpireTick = -1;
        public string projectileInterceptorShieldThingId = string.Empty;

        public int offensiveMarkExpireTick = -1;
        public int offensiveMarkStacks = 0;
        public int offensiveComboExpireTick = -1;
        public int offensiveComboStacks = 0;
        public int dashEmpowerExpireTick = -1;

        public int flightStateStartTick = -1;
        public int flightStateExpireTick = -1;
        public float flightStateHeightFactor = 0.35f;
        public bool suppressCombatActionsDuringFlightState = true;

        public bool isInVanillaFlight = false;
        public int vanillaFlightStartTick = -1;
        public int vanillaFlightExpireTick = -1;
        public string vanillaFlightSourceAbilityDefName = string.Empty;
        public string vanillaFlightFollowupAbilityDefName = string.Empty;
        public IntVec3 vanillaFlightReservedTargetCell = IntVec3.Invalid;
        public bool vanillaFlightHasReservedTargetCell = false;
        public int vanillaFlightFollowupWindowEndTick = -1;
        public bool vanillaFlightPendingLandingBurst = false;

        public string triggeredEquipmentAnimationAbilityDefName = string.Empty;
        public int triggeredEquipmentAnimationStartTick = -1;
        public int triggeredEquipmentAnimationEndTick = -1;

        public int abilityExpressionOverrideExpireTick = -1;
        public ExpressionType? abilityExpressionOverride = null;
        public float abilityPupilBrightnessOffset = 0f;
        public float abilityPupilContrastOffset = 0f;

        public bool forcedMoveActive = false;
        public IntVec3 forcedMoveStartCell = IntVec3.Invalid;
        public IntVec3 forcedMoveCurrentCell = IntVec3.Invalid;
        public IntVec3 forcedMoveNextCell = IntVec3.Invalid;
        public int forcedMoveStepStartTick = -1;
        public int forcedMoveStepDurationTicks = 4;
        public int forcedMoveQueuedSteps = 0;
        public int forcedMoveDirectionX = 0;
        public int forcedMoveDirectionZ = 0;
        public int forcedMoveBusyUntilTick = -1;
        public bool forcedMoveCollisionTriggered = false;
        public bool forcedMoveIsLanding = false;
        public float forcedMoveDirFloatX = 0f;
        public float forcedMoveDirFloatZ = 0f;
        public float forcedMoveTotalDistance = 0f;
        public float forcedMoveTraveledDistance = 0f;
        public float forcedMoveSpeedPerTick = 0f;
        public int forcedMoveStartTick = -1;
        public int forcedMoveDurationTicks = -1;
        public bool forcedMoveSweepAcrossPath = false;
        public System.Collections.Generic.HashSet<Pawn> forcedMoveHitPawns = new System.Collections.Generic.HashSet<Pawn>();

        // ── Bezier Curve Wall ──
        public int bezierWallExpireTick = -1;
        public float bezierWallStartX;
        public float bezierWallStartZ;
        public float bezierWallEndX;
        public float bezierWallEndZ;
        public float bezierWallControlX;
        public float bezierWallControlZ;
        public float bezierWallThickness;
        public int bezierWallSegmentCount;
        public bool bezierWallBlockFriendly;
        public float bezierWallAbsorbRemaining;

        public int GetCooldownUntilTick(AbilityRuntimeHotkeySlot slot)
        {
            return slot switch
            {
                AbilityRuntimeHotkeySlot.Q => qCooldownUntilTick,
                AbilityRuntimeHotkeySlot.W => wCooldownUntilTick,
                AbilityRuntimeHotkeySlot.E => eCooldownUntilTick,
                AbilityRuntimeHotkeySlot.R => rCooldownUntilTick,
                AbilityRuntimeHotkeySlot.T => tCooldownUntilTick,
                AbilityRuntimeHotkeySlot.A => aCooldownUntilTick,
                AbilityRuntimeHotkeySlot.S => sCooldownUntilTick,
                AbilityRuntimeHotkeySlot.D => dCooldownUntilTick,
                AbilityRuntimeHotkeySlot.F => fCooldownUntilTick,
                AbilityRuntimeHotkeySlot.Z => zCooldownUntilTick,
                AbilityRuntimeHotkeySlot.X => xCooldownUntilTick,
                AbilityRuntimeHotkeySlot.C => cCooldownUntilTick,
                AbilityRuntimeHotkeySlot.V => vCooldownUntilTick,
                _ => 0
            };
        }

        public void SetCooldownUntilTick(AbilityRuntimeHotkeySlot slot, int value)
        {
            switch (slot)
            {
                case AbilityRuntimeHotkeySlot.Q: qCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.W: wCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.E: eCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.R: rCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.T: tCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.A: aCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.S: sCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.D: dCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.F: fCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.Z: zCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.X: xCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.C: cCooldownUntilTick = value; break;
                case AbilityRuntimeHotkeySlot.V: vCooldownUntilTick = value; break;
            }
        }

        public void SetOverrideDefName(AbilityRuntimeHotkeySlot slot, string defName)
        {
            switch (slot)
            {
                case AbilityRuntimeHotkeySlot.Q: qOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.W: wOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.E: eOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.R: rOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.T: tOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.A: aOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.S: sOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.D: dOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.F: fOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.Z: zOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.X: xOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.C: cOverrideAbilityDefName = defName; break;
                case AbilityRuntimeHotkeySlot.V: vOverrideAbilityDefName = defName; break;
            }
        }

        public void SetOverrideExpireTick(AbilityRuntimeHotkeySlot slot, int tick)
        {
            switch (slot)
            {
                case AbilityRuntimeHotkeySlot.Q: qOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.W: wOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.E: eOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.R: rOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.T: tOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.A: aOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.S: sOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.D: dOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.F: fOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.Z: zOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.X: xOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.C: cOverrideExpireTick = tick; break;
                case AbilityRuntimeHotkeySlot.V: vOverrideExpireTick = tick; break;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref slotOverrideWindowEndTick, "slotOverrideWindowEndTick", 0);
            Scribe_Values.Look(ref slotOverrideWindowAbilityDefName, "slotOverrideWindowAbilityDefName", string.Empty);
            Scribe_Values.Look(ref slotOverrideWindowSlotId, "slotOverrideWindowSlotId", string.Empty);

            Scribe_Values.Look(ref qOverrideAbilityDefName, "qOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref qOverrideExpireTick, "qOverrideExpireTick", -1);
            Scribe_Values.Look(ref wOverrideAbilityDefName, "wOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref wOverrideExpireTick, "wOverrideExpireTick", -1);
            Scribe_Values.Look(ref eOverrideAbilityDefName, "eOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref eOverrideExpireTick, "eOverrideExpireTick", -1);
            Scribe_Values.Look(ref rOverrideAbilityDefName, "rOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref rOverrideExpireTick, "rOverrideExpireTick", -1);
            Scribe_Values.Look(ref tOverrideAbilityDefName, "tOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref tOverrideExpireTick, "tOverrideExpireTick", -1);
            Scribe_Values.Look(ref aOverrideAbilityDefName, "aOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref aOverrideExpireTick, "aOverrideExpireTick", -1);
            Scribe_Values.Look(ref sOverrideAbilityDefName, "sOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref sOverrideExpireTick, "sOverrideExpireTick", -1);
            Scribe_Values.Look(ref dOverrideAbilityDefName, "dOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref dOverrideExpireTick, "dOverrideExpireTick", -1);
            Scribe_Values.Look(ref fOverrideAbilityDefName, "fOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref fOverrideExpireTick, "fOverrideExpireTick", -1);
            Scribe_Values.Look(ref zOverrideAbilityDefName, "zOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref zOverrideExpireTick, "zOverrideExpireTick", -1);
            Scribe_Values.Look(ref xOverrideAbilityDefName, "xOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref xOverrideExpireTick, "xOverrideExpireTick", -1);
            Scribe_Values.Look(ref cOverrideAbilityDefName, "cOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref cOverrideExpireTick, "cOverrideExpireTick", -1);
            Scribe_Values.Look(ref vOverrideAbilityDefName, "vOverrideAbilityDefName", string.Empty);
            Scribe_Values.Look(ref vOverrideExpireTick, "vOverrideExpireTick", -1);

            Scribe_Values.Look(ref qCooldownUntilTick, "qCooldownUntilTick", 0);
            Scribe_Values.Look(ref wCooldownUntilTick, "wCooldownUntilTick", 0);
            Scribe_Values.Look(ref eCooldownUntilTick, "eCooldownUntilTick", 0);
            Scribe_Values.Look(ref rCooldownUntilTick, "rCooldownUntilTick", 0);
            Scribe_Values.Look(ref tCooldownUntilTick, "tCooldownUntilTick", 0);
            Scribe_Values.Look(ref aCooldownUntilTick, "aCooldownUntilTick", 0);
            Scribe_Values.Look(ref sCooldownUntilTick, "sCooldownUntilTick", 0);
            Scribe_Values.Look(ref dCooldownUntilTick, "dCooldownUntilTick", 0);
            Scribe_Values.Look(ref fCooldownUntilTick, "fCooldownUntilTick", 0);
            Scribe_Values.Look(ref zCooldownUntilTick, "zCooldownUntilTick", 0);
            Scribe_Values.Look(ref xCooldownUntilTick, "xCooldownUntilTick", 0);
            Scribe_Values.Look(ref cCooldownUntilTick, "cCooldownUntilTick", 0);
            Scribe_Values.Look(ref vCooldownUntilTick, "vCooldownUntilTick", 0);
            Scribe_Values.Look(ref bezierWallEndZ, "bezierWallEndZ", 0f);
            Scribe_Values.Look(ref bezierWallControlX, "bezierWallControlX", 0f);
            Scribe_Values.Look(ref bezierWallControlZ, "bezierWallControlZ", 0f);
            Scribe_Values.Look(ref bezierWallThickness, "bezierWallThickness", 0.5f);
            Scribe_Values.Look(ref bezierWallSegmentCount, "bezierWallSegmentCount", 16);
            Scribe_Values.Look(ref bezierWallBlockFriendly, "bezierWallBlockFriendly", false);
            Scribe_Values.Look(ref bezierWallAbsorbRemaining, "bezierWallAbsorbRemaining", 0f);
        }

        public void Normalize()
        {
            qOverrideAbilityDefName ??= string.Empty;
            slotOverrideWindowAbilityDefName ??= string.Empty;
            slotOverrideWindowSlotId ??= string.Empty;
            wOverrideAbilityDefName ??= string.Empty;
            eOverrideAbilityDefName ??= string.Empty;
            rOverrideAbilityDefName ??= string.Empty;
            tOverrideAbilityDefName ??= string.Empty;
            aOverrideAbilityDefName ??= string.Empty;
            sOverrideAbilityDefName ??= string.Empty;
            dOverrideAbilityDefName ??= string.Empty;
            fOverrideAbilityDefName ??= string.Empty;
            zOverrideAbilityDefName ??= string.Empty;
            xOverrideAbilityDefName ??= string.Empty;
            cOverrideAbilityDefName ??= string.Empty;
            vOverrideAbilityDefName ??= string.Empty;

            if (rStackCount < 0) rStackCount = 0;
            if (rStackCount > 7) rStackCount = 7;
            if (!rSecondStageHasTarget) rSecondStageTargetCell = IntVec3.Invalid;
            rStackAbilityDefName ??= string.Empty;

            if (shieldRemainingDamage < 0f) shieldRemainingDamage = 0f;
            if (shieldStoredHeal < 0f) shieldStoredHeal = 0f;
            if (shieldStoredBonusDamage < 0f) shieldStoredBonusDamage = 0f;
            if (offensiveMarkStacks < 0) offensiveMarkStacks = 0;
            if (offensiveComboStacks < 0) offensiveComboStacks = 0;
            if (flightStateExpireTick < -1) flightStateExpireTick = -1;
            if (flightStateStartTick < -1) flightStateStartTick = -1;
            if (flightStateHeightFactor < 0f) flightStateHeightFactor = 0f;
            if (abilityExpressionOverrideExpireTick < -1) abilityExpressionOverrideExpireTick = -1;
            if (attachedShieldVisualScale <= 0f) attachedShieldVisualScale = 1f;
            attachedShieldVisualThingId ??= string.Empty;
            projectileInterceptorShieldThingId ??= string.Empty;
            if (forcedMoveStepDurationTicks <= 0) forcedMoveStepDurationTicks = 4;
            if (forcedMoveQueuedSteps < 0) forcedMoveQueuedSteps = 0;
            if (forcedMoveBusyUntilTick < -1) forcedMoveBusyUntilTick = -1;
            if (forcedMoveTotalDistance < 0f) forcedMoveTotalDistance = 0f;
            if (forcedMoveTraveledDistance < 0f) forcedMoveTraveledDistance = 0f;
            if (forcedMoveSpeedPerTick < 0f) forcedMoveSpeedPerTick = 0f;
            triggeredEquipmentAnimationAbilityDefName ??= string.Empty;
            if (triggeredEquipmentAnimationEndTick < triggeredEquipmentAnimationStartTick)
            {
                triggeredEquipmentAnimationStartTick = -1;
            }

            // ── Bezier Curve Wall ──
            if (bezierWallExpireTick < -1) bezierWallExpireTick = -1;
            if (bezierWallThickness < 0f) bezierWallThickness = 0.5f;
            if (bezierWallSegmentCount <= 0) bezierWallSegmentCount = 16;
            if (bezierWallAbsorbRemaining < 0f) bezierWallAbsorbRemaining = 0f;
        }
    }
}
