using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Rendering;
using CharacterStudio.UI;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 全局热键监听组件。
    /// 管理选定角色的实时按键响应、冷却、连招判定及 R 技能堆叠机制。
    /// 在 1.6 中负责拦截 QER/F/ZXC 热键。
    /// </summary>
    public class AbilityHotkeyRuntimeComponent : GameComponent
    {
        private const int DefaultQWComboWindowTicks = 12;      // 0.2s
        private const int MinSlotCooldownTicks = 30;           // 0.5s — 所有槽位的最短 CD
        private const int DefaultRRequiredStacks = 7;
        private const int DefaultRSecondStageDelayTicks = 180;  // 3.0s

        private static bool globalHotkeysEnabled = true;
        private int lastProcessedTick = -1;

        private static readonly Dictionary<Pawn, Stance_Busy> LastBusyStanceByPawn = new Dictionary<Pawn, Stance_Busy>();

        private static readonly KeyCode[] ReservedHotkeyKeyCodes =
        {
            KeyCode.Q,
            KeyCode.E,
            KeyCode.R,
            KeyCode.T,
            KeyCode.F,
            KeyCode.Z,
            KeyCode.X,
            KeyCode.C,
            KeyCode.V
        };

        public static bool GlobalHotkeysEnabled
        {
            get => globalHotkeysEnabled;
            set => globalHotkeysEnabled = value;
        }

        public AbilityHotkeyRuntimeComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref globalHotkeysEnabled, "csGlobalAbilityHotkeysEnabled", true);
        }

        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();

            if (Current.Game == null || Find.CurrentMap == null || Find.TickManager == null)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;

            // ── 热键检测：每 Unity 帧都必须检测
            if (!globalHotkeysEnabled) return;

            if (Input.GetKeyDown(KeyCode.Q)) TryCastForSelectedPawn(AbilityRuntimeHotkeySlot.Q, tick);
            else if (Input.GetKeyDown(KeyCode.E)) TryCastForSelectedPawn(AbilityRuntimeHotkeySlot.E, tick);
            else if (Input.GetKeyDown(KeyCode.R)) TryCastForSelectedPawn(AbilityRuntimeHotkeySlot.R, tick);
            else if (Input.GetKeyDown(KeyCode.T)) TryCastForSelectedPawn(AbilityRuntimeHotkeySlot.T, tick);
            else if (Input.GetKeyDown(KeyCode.F)) TryCastForSelectedPawn(AbilityRuntimeHotkeySlot.F, tick);
            else if (Input.GetKeyDown(KeyCode.Z)) TryCastForSelectedPawn(AbilityRuntimeHotkeySlot.Z, tick);
            else if (Input.GetKeyDown(KeyCode.X)) TryCastForSelectedPawn(AbilityRuntimeHotkeySlot.X, tick);
            else if (Input.GetKeyDown(KeyCode.C)) TryCastForSelectedPawn(AbilityRuntimeHotkeySlot.C, tick);
            else if (Input.GetKeyDown(KeyCode.V)) TryCastForSelectedPawn(AbilityRuntimeHotkeySlot.V, tick);

            // ── 以下逻辑基于 tick 驱动
            if (tick == lastProcessedTick) return;
            lastProcessedTick = tick;

            UpdateRStackingStates();
            ProcessPendingRSecondStages(tick);
        }

        private static bool IsSlotBound(AbilityRuntimeHotkeySlot slot)
        {
            if (slot == AbilityRuntimeHotkeySlot.None) return false;
            Pawn? pawn = Find.Selector?.SingleSelectedThing as Pawn;
            if (pawn == null) return false;
            
            CharacterAbilityLoadout? loadout = AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(pawn);
            if (loadout?.hotkeys == null || !loadout.hotkeys.enabled) return false;

            string slotStr = slot.ToString();
            return !string.IsNullOrEmpty(loadout.hotkeys[slotStr]);
        }

        /// <summary>获取指定槽位的当前 CD 截止 Tick</summary>
        private static int GetSlotCooldownUntil(CompCharacterAbilityRuntime comp, AbilityRuntimeHotkeySlot slot)
        {
            var state = comp.RuntimeState;
            switch (slot)
            {
                case AbilityRuntimeHotkeySlot.Q: return state.qCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.W: return state.wCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.E: return state.eCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.R: return state.rCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.T: return state.tCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.A: return state.aCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.S: return state.sCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.D: return state.dCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.F: return state.fCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.Z: return state.zCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.X: return state.xCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.C: return state.cCooldownUntilTick;
                case AbilityRuntimeHotkeySlot.V: return state.vCooldownUntilTick;
                default: return 0;
            }
        }

        /// <summary>写入指定槽位的 CD 截止 Tick</summary>
        private static void SetSlotCooldown(CompCharacterAbilityRuntime comp, AbilityRuntimeHotkeySlot slot, int untilTick)
        {
            var state = comp.RuntimeState;
            switch (slot)
            {
                case AbilityRuntimeHotkeySlot.Q: state.qCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.W: state.wCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.E: state.eCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.R: state.rCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.T: state.tCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.A: state.aCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.S: state.sCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.D: state.dCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.F: state.fCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.Z: state.zCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.X: state.xCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.C: state.cCooldownUntilTick = untilTick; break;
                case AbilityRuntimeHotkeySlot.V: state.vCooldownUntilTick = untilTick; break;
                default: break;
            }
        }

        private static void TryCastForSelectedPawn(AbilityRuntimeHotkeySlot slot, int tick)
        {
            if (slot == AbilityRuntimeHotkeySlot.None) return;
            Pawn? selectedPawn = Find.Selector?.SingleSelectedThing as Pawn;
            if (selectedPawn == null || !selectedPawn.Spawned || selectedPawn.Map == null || !selectedPawn.Drafted || !selectedPawn.IsColonistPlayerControlled)
            {
                return;
            }

            if (!AbilityTimeStopRuntimeController.CanPawnAct(selectedPawn))
            {
                return;
            }

            if (GUIUtility.keyboardControl != 0 && !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
            {
                if (!globalHotkeysEnabled)
                {
                    return;
                }

                UIHelper.ClearNumericFieldFocusAndBuffers();
            }

            if (Find.Targeter != null && Find.Targeter.IsTargeting)
            {
                Find.Targeter.StopTargeting();
            }

            if (IsBlockingWindowOpen())
                return;

            Pawn pawn = selectedPawn;
            var abilityComp = pawn.GetComp<CompCharacterAbilityRuntime>();
            CharacterAbilityLoadout? loadout = AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(pawn);
            SkinAbilityHotkeyConfig? hotkeys = loadout?.hotkeys;

            if (abilityComp == null || hotkeys == null || !hotkeys.enabled)
            {
                return;
            }

            // ── 最小槽位 CD 门控（0.5s）──
            int slotCdUntil = GetSlotCooldownUntil(abilityComp, slot);
            if (tick < slotCdUntil)
            {
                int remain = slotCdUntil - tick;
                Messages.Message(
                    "CS_Ability_Hotkey_SlotCooldown".Translate(slot.ToString(), (remain / 60f).ToString("F1")),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            string abilityDefName = ResolveAbilityDefNameBySlot(hotkeys, slot, abilityComp, tick);
            ModularAbilityDef? ability = ResolveAbilityByDefName(loadout, abilityDefName);
            if (AbilityVanillaFlightUtility.TryNotifyFlightFollowupFailure(pawn, ability))
            {
                return;
            }

            if (ability == null)
            {
                if (!string.IsNullOrEmpty(abilityDefName))
                {
                    Messages.Message(
                        "CS_Ability_Hotkey_AbilityMissing".Translate(abilityDefName),
                        MessageTypeDefOf.RejectInput,
                        false);
                }
                return;
            }

            bool casted = ability != null && TryCastConfiguredAbility(pawn, abilityComp, ability, tick, slot);

            if (casted)
            {
                // 写入最小 CD（0.5s），取 max(当前CD, 最小CD)
                int minCdEnd = tick + MinSlotCooldownTicks;
                int curCdEnd = GetSlotCooldownUntil(abilityComp, slot);
                SetSlotCooldown(abilityComp, slot, Mathf.Max(curCdEnd, minCdEnd));

                if (ability != null)
                {
                    Messages.Message(
                        "CS_Ability_Hotkey_CastSuccess".Translate(slot.ToString(), ability.label ?? ability.defName),
                        MessageTypeDefOf.NeutralEvent,
                        false);
                }
            }
        }

        private static bool IsBlockingWindowOpen()
        {
            try
            {
                var windowsField = AccessTools.Field(typeof(WindowStack), "windows");
                if (windowsField?.GetValue(Find.WindowStack) is not List<Window> windows)
                    return false;

                return windows.Any(w => w != null
                    && !AllowsAbilityHotkeysWhileOpen(w)
                    && (w.absorbInputAroundWindow || w.forcePause));
            }
            catch
            {
                return false;
            }
        }

        public static void ConsumeReservedHotkeyKeys(Event? currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.alt || currentEvent.control || currentEvent.command)
            {
                return;
            }

            foreach (KeyCode keyCode in ReservedHotkeyKeyCodes)
            {
                if (currentEvent.keyCode == keyCode)
                {
                    currentEvent.Use();
                    return;
                }
            }
        }

        private static bool AllowsAbilityHotkeysWhileOpen(Window window)
        {
            return window is Dialog_AbilityEditor
                || window is Dialog_AbilityHotkeySettings;
        }

        private static ModularAbilityDef? ResolveAbilityByDefName(CharacterAbilityLoadout? loadout, string abilityDefName)
        {
            return AbilityLoadoutRuntimeUtility.ResolveAbilityByDefName(loadout, abilityDefName);
        }

        private static string ResolveAbilityDefNameBySlot(SkinAbilityHotkeyConfig hotkeys, AbilityRuntimeHotkeySlot slot, CompCharacterAbilityRuntime abilityComp, int tick)
        {
            string overrideDefName = GetActiveSlotOverrideAbilityDefName(abilityComp, slot, tick);
            if (!string.IsNullOrEmpty(overrideDefName))
            {
                return overrideDefName;
            }

            if (tick <= abilityComp.SlotOverrideWindowEndTick
                && !string.IsNullOrWhiteSpace(abilityComp.SlotOverrideWindowSlotId)
                && string.Equals(abilityComp.SlotOverrideWindowSlotId, slot.ToString(), System.StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(abilityComp.SlotOverrideWindowAbilityDefName))
            {
                return abilityComp.SlotOverrideWindowAbilityDefName;
            }

            return hotkeys[slot.ToString()];
        }

        private static string GetActiveSlotOverrideAbilityDefName(CompCharacterAbilityRuntime abilityComp, AbilityRuntimeHotkeySlot slot, int tick)
        {
            string abilityDefName;
            int expireTick;
            GetSlotOverrideState(abilityComp, slot, out abilityDefName, out expireTick);
            if (string.IsNullOrEmpty(abilityDefName))
            {
                return string.Empty;
            }

            if (expireTick >= 0 && tick > expireTick)
            {
                ClearSlotOverrideState(abilityComp, slot);
                return string.Empty;
            }

            return abilityDefName;
        }

        private static void GetSlotOverrideState(CompCharacterAbilityRuntime abilityComp, AbilityRuntimeHotkeySlot slot, out string abilityDefName, out int expireTick)
        {
            var state = abilityComp.RuntimeState;
            switch (slot)
            {
                case AbilityRuntimeHotkeySlot.Q:
                    abilityDefName = state.qOverrideAbilityDefName;
                    expireTick = state.qOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.W:
                    abilityDefName = state.wOverrideAbilityDefName;
                    expireTick = state.wOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.E:
                    abilityDefName = state.eOverrideAbilityDefName;
                    expireTick = state.eOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.T:
                    abilityDefName = state.tOverrideAbilityDefName;
                    expireTick = state.tOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.A:
                    abilityDefName = state.aOverrideAbilityDefName;
                    expireTick = state.aOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.S:
                    abilityDefName = state.sOverrideAbilityDefName;
                    expireTick = state.sOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.D:
                    abilityDefName = state.dOverrideAbilityDefName;
                    expireTick = state.dOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.F:
                    abilityDefName = state.fOverrideAbilityDefName;
                    expireTick = state.fOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.Z:
                    abilityDefName = state.zOverrideAbilityDefName;
                    expireTick = state.zOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.X:
                    abilityDefName = state.xOverrideAbilityDefName;
                    expireTick = state.xOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.C:
                    abilityDefName = state.cOverrideAbilityDefName;
                    expireTick = state.cOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.V:
                    abilityDefName = state.vOverrideAbilityDefName;
                    expireTick = state.vOverrideExpireTick;
                    break;
                case AbilityRuntimeHotkeySlot.R:
                    abilityDefName = state.rOverrideAbilityDefName;
                    expireTick = state.rOverrideExpireTick;
                    break;
                default:
                    abilityDefName = string.Empty;
                    expireTick = -1;
                    break;
            }
        }

        private static void ClearSlotOverrideState(CompCharacterAbilityRuntime abilityComp, AbilityRuntimeHotkeySlot slot)
        {
            var state = abilityComp.RuntimeState;
            switch (slot)
            {
                case AbilityRuntimeHotkeySlot.Q:
                    state.qOverrideAbilityDefName = string.Empty;
                    state.qOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.W:
                    state.wOverrideAbilityDefName = string.Empty;
                    state.wOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.E:
                    state.eOverrideAbilityDefName = string.Empty;
                    state.eOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.T:
                    state.tOverrideAbilityDefName = string.Empty;
                    state.tOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.A:
                    state.aOverrideAbilityDefName = string.Empty;
                    state.aOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.S:
                    state.sOverrideAbilityDefName = string.Empty;
                    state.sOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.D:
                    state.dOverrideAbilityDefName = string.Empty;
                    state.dOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.F:
                    state.fOverrideAbilityDefName = string.Empty;
                    state.fOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.Z:
                    state.zOverrideAbilityDefName = string.Empty;
                    state.zOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.X:
                    state.xOverrideAbilityDefName = string.Empty;
                    state.xOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.C:
                    state.cOverrideAbilityDefName = string.Empty;
                    state.cOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.V:
                    state.vOverrideAbilityDefName = string.Empty;
                    state.vOverrideExpireTick = -1;
                    break;
                case AbilityRuntimeHotkeySlot.R:
                    state.rOverrideAbilityDefName = string.Empty;
                    state.rOverrideExpireTick = -1;
                    break;
                default:
                    break;
            }
        }

        private static bool TryCastConfiguredAbility(Pawn caster, CompCharacterAbilityRuntime abilityComp, ModularAbilityDef ability, int tick, AbilityRuntimeHotkeySlot slot)
        {
            if (ability == null)
            {
                return false;
            }

            AbilityRuntimeComponentConfig? jumpComp = GetSmartJumpComponent(ability);
            if (jumpComp != null && jumpComp.enabled)
            {
                return TryCastSmartJumpAbility(caster, abilityComp, ability, tick, jumpComp, slot);
            }

            AbilityRuntimeComponentConfig? rComp = GetRuntimeComponent(ability, AbilityRuntimeComponentType.RStackDetonation);
            if (rComp != null && rComp.enabled)
            {
                return TryHandleGeneralizedRStackHotkey(caster, abilityComp, ability, tick, slot, rComp);
            }

            return TryCastDefaultAbility(caster, ability);
        }

        private static AbilityRuntimeComponentConfig? GetSmartJumpComponent(ModularAbilityDef ability)
        {
            return ability.runtimeComponents?.FirstOrDefault(c =>
                c.type == AbilityRuntimeComponentType.SmartJump || c.type == AbilityRuntimeComponentType.EShortJump);
        }

        private static AbilityRuntimeComponentConfig? GetRuntimeComponent(ModularAbilityDef ability, AbilityRuntimeComponentType type)
        {
            return ability.runtimeComponents?.FirstOrDefault(c => c.type == type);
        }

        private static bool TryCastSmartJumpAbility(Pawn caster, CompCharacterAbilityRuntime abilityComp, ModularAbilityDef ability, int tick, AbilityRuntimeHotkeySlot slot)
        {
            var config = GetSmartJumpComponent(ability);
            if (config == null) return false;
            return TryCastSmartJumpAbility(caster, abilityComp, ability, tick, config, slot);
        }

        private static bool TryCastSmartJumpAbility(Pawn caster, CompCharacterAbilityRuntime abilityComp, ModularAbilityDef ability, int tick, AbilityRuntimeComponentConfig config, AbilityRuntimeHotkeySlot slot)
        {
            Map map = caster.Map;
            IntVec3 targetCell = IntVec3.Invalid;

            if (config.useMouseTargetCell)
            {
                targetCell = Verse.UI.MouseCell();
            }

            if (!targetCell.IsValid || !targetCell.InBounds(map))
            {
                targetCell = caster.Position + caster.Rotation.FacingCell * config.jumpDistance;
            }

            // 限制最大距离
            if (targetCell.DistanceTo(caster.Position) > config.jumpDistance)
            {
                targetCell = caster.Position + (targetCell - caster.Position).ToVector3().normalized.ToIntVec3() * config.jumpDistance;
            }

            if (!targetCell.InBounds(map) || !targetCell.Walkable(map))
            {
                return false;
            }

            // 执行跳跃逻辑 (这里调用具体的执行器)
            PawnFlyer flyer = PawnFlyer.MakeFlyer(ThingDefOf.PawnFlyer, caster, targetCell, null, null);
            if (flyer != null)
            {
                GenSpawn.Spawn(flyer, targetCell, map);
                return true;
            }

            return false;
        }

        private static bool TryCastDefaultAbility(Pawn caster, ModularAbilityDef ability)
        {
            if (ability == null)
            {
                return false;
            }

            var runtimeDef = AbilityGrantUtility.GetRuntimeAbilityDef(ability.defName);
            var runtimeAbility = runtimeDef != null ? caster.abilities?.GetAbility(runtimeDef) : null;
            if (runtimeAbility != null)
            {
                try
                {
                    if (TrySmartCastRuntimeAbility(caster, runtimeAbility, ability))
                        return true;

                    if (Patch_AbilityGizmos.TryProcessVanillaCommand(caster, ability.defName))
                        return true;

                    if (TryQueueRuntimeAbility(caster, runtimeAbility, ability))
                        return true;

                    return false;
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 热键施放运行时技能失败: {ex.Message}");
                    return false;
                }
            }

            var abilityComp = caster.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp != null)
            {
                int visualTicks = Mathf.Max((int)ability.warmupTicks, 8) + 8;
                abilityComp.SetWeaponCarryCastingWindow(visualTicks);
            }

            AbilityTargetType normalizedTarget = ability.targetType;
            LocalTargetInfo fallbackTarget = normalizedTarget == AbilityTargetType.Self
                ? new LocalTargetInfo(caster)
                : new LocalTargetInfo(caster.Position + caster.Rotation.FacingCell);

            IEnumerable<IntVec3> cells = AbilityAreaUtility.BuildResolvedTargetCells(caster, ability, fallbackTarget);

            if (!cells.Any())
            {
                cells = normalizedTarget == AbilityTargetType.Self
                    ? new List<IntVec3> { caster.Position }
                    : new List<IntVec3> { caster.Position + caster.Rotation.FacingCell };
            }

            return ExecuteAbilityOnCells(caster, ability, cells);
        }

        private static bool TrySmartCastRuntimeAbility(Pawn caster, Ability runtimeAbility, ModularAbilityDef ability)
        {
            AbilityCarrierType normalizedCarrier = ability.carrierType;
            AbilityTargetType normalizedTarget = ability.targetType;
            LocalTargetInfo forcedTarget = AbilityVanillaFlightUtility.ResolveFollowupTarget(caster, ability, LocalTargetInfo.Invalid);

            if (normalizedCarrier == AbilityCarrierType.Self || normalizedTarget == AbilityTargetType.Self)
            {
                LocalTargetInfo selfTarget = new LocalTargetInfo(caster);
                LocalTargetInfo selfDest = normalizedCarrier == AbilityCarrierType.Projectile && normalizedTarget == AbilityTargetType.Cell
                    ? new LocalTargetInfo(caster.Position)
                    : LocalTargetInfo.Invalid;
                runtimeAbility.QueueCastingJob(selfTarget, selfDest);
                return true;
            }

            if (forcedTarget.IsValid)
            {
                LocalTargetInfo forcedDest = normalizedCarrier == AbilityCarrierType.Projectile && normalizedTarget == AbilityTargetType.Cell
                    ? forcedTarget
                    : LocalTargetInfo.Invalid;
                runtimeAbility.QueueCastingJob(forcedTarget, forcedDest);
                return true;
            }

            if (!TryGetSmartCastTarget(caster, ability, out LocalTargetInfo target, out LocalTargetInfo dest))
            {
                return false;
            }

            runtimeAbility.QueueCastingJob(target, dest);
            return true;
        }

        private static bool TryGetSmartCastTarget(Pawn caster, ModularAbilityDef ability, out LocalTargetInfo target, out LocalTargetInfo dest)
        {
            target = LocalTargetInfo.Invalid;
            dest = LocalTargetInfo.Invalid;

            Map? map = caster.Map;
            if (map == null)
            {
                return false;
            }

            IntVec3 mouseCell = global::Verse.UI.MouseCell();
            if (!mouseCell.InBounds(map))
            {
                return false;
            }

            AbilityCarrierType normalizedCarrier = ability.carrierType;
            AbilityTargetType normalizedTarget = ability.targetType;

            if (normalizedTarget == AbilityTargetType.Cell)
            {
                target = new LocalTargetInfo(mouseCell);
                dest = normalizedCarrier == AbilityCarrierType.Projectile ? target : LocalTargetInfo.Invalid;
                return true;
            }

            if (normalizedTarget != AbilityTargetType.Entity)
            {
                return false;
            }

            Thing? hoveredThing = mouseCell.GetThingList(map)
                .Where(thing => thing != null && thing != caster)
                .OrderByDescending(thing => thing is Pawn)
                .ThenByDescending(thing => thing is Building)
                .FirstOrDefault();

            if (hoveredThing == null)
            {
                return false;
            }

            target = new LocalTargetInfo(hoveredThing);
            return true;
        }

        private static bool TryQueueRuntimeAbility(Pawn caster, Ability runtimeAbility, ModularAbilityDef ability)
        {
            AbilityCarrierType normalizedCarrier = ability.carrierType;
            AbilityTargetType normalizedTarget = ability.targetType;
            LocalTargetInfo forcedTarget = AbilityVanillaFlightUtility.ResolveFollowupTarget(caster, ability, LocalTargetInfo.Invalid);

            if (normalizedCarrier == AbilityCarrierType.Self || normalizedTarget == AbilityTargetType.Self)
            {
                LocalTargetInfo selfTarget = new LocalTargetInfo(caster);
                LocalTargetInfo selfDest = normalizedCarrier == AbilityCarrierType.Projectile && normalizedTarget == AbilityTargetType.Cell
                    ? new LocalTargetInfo(caster.Position)
                    : LocalTargetInfo.Invalid;
                runtimeAbility.QueueCastingJob(selfTarget, selfDest);
                return true;
            }

            if (forcedTarget.IsValid)
            {
                LocalTargetInfo forcedDest = normalizedCarrier == AbilityCarrierType.Projectile && normalizedTarget == AbilityTargetType.Cell
                    ? forcedTarget
                    : LocalTargetInfo.Invalid;
                runtimeAbility.QueueCastingJob(forcedTarget, forcedDest);
                return true;
            }

            if (Find.Targeter == null)
                return false;

            TargetingParameters parms = BuildTargetingParameters(ability);
            Texture2D? icon = LoadAbilityIcon(ability.iconPath);

            // 双点选取模式
            if (ability.useTwoPointTargeting)
            {
                Find.Targeter.BeginTargeting(parms,
                    firstTarget =>
                    {
                        Find.Targeter.BeginTargeting(parms,
                            secondTarget =>
                            {
                                try
                                {
                                    runtimeAbility.QueueCastingJob(firstTarget, secondTarget);
                                }
                                catch (System.Exception ex)
                                {
                                    Log.Warning($"[CharacterStudio] 热键双点技能施放失败: {ex.Message}");
                                }
                            },
                            caster, null, icon, true);
                    },
                    caster, null, icon, true);
                return true;
            }

            Find.Targeter.BeginTargeting(parms,
                target =>
                {
                    try
                    {
                        LocalTargetInfo resolvedTarget = AbilityVanillaFlightUtility.ResolveFollowupTarget(caster, ability, target);
                        LocalTargetInfo dest = normalizedCarrier == AbilityCarrierType.Projectile && normalizedTarget == AbilityTargetType.Cell
                            ? resolvedTarget
                            : LocalTargetInfo.Invalid;
                        runtimeAbility.QueueCastingJob(resolvedTarget, dest);
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning($"[CharacterStudio] 热键技能目标施放失败: {ex.Message}");
                    }
                },
                caster,
                null,
                icon,
                true);
            return true;
        }

        private static TargetingParameters BuildTargetingParameters(ModularAbilityDef ability)
        {
            AbilityTargetType normalizedTarget = ability.targetType;

            return normalizedTarget switch
            {
                AbilityTargetType.Cell => new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    canTargetSelf = false
                },
                AbilityTargetType.Entity => new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetLocations = false,
                    canTargetSelf = false
                },
                _ => new TargetingParameters
                {
                    canTargetSelf = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    canTargetLocations = false
                }
            };
        }

        private static Texture2D? LoadAbilityIcon(string iconPath)
        {
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                var tex = ContentFinder<Texture2D>.Get(iconPath, false);
                if (tex != null)
                    return tex;

                tex = CharacterStudio.Rendering.RuntimeAssetLoader.LoadTextureRaw(iconPath, true);
                if (tex != null)
                    return tex;
            }

            return ContentFinder<Texture2D>.Get("UI/Designators/Strip", true);
        }

        // 复用的 Thing 快照缓冲区，避免每格 GetThingList().ToList() 分配
        private static readonly List<Thing> _thingBuffer = new List<Thing>();

        private static bool ExecuteAbilityOnCells(Pawn caster, ModularAbilityDef ability, IEnumerable<IntVec3> rawCells)
        {
            if (caster.Map == null || ability == null || ability.effects == null || ability.effects.Count == 0)
                return false;

            var map = caster.Map;
            var distinctCells = new HashSet<IntVec3>();
            foreach (var c in rawCells)
                if (c.InBounds(map)) distinctCells.Add(c);

            bool applied = false;

            var workers = new EffectWorker[ability.effects.Count];
            for (int ei = 0; ei < ability.effects.Count; ei++)
                workers[ei] = EffectWorkerFactory.GetWorker(ability.effects[ei].type);

            foreach (var cell in distinctCells)
            {
                for (int ei = 0; ei < ability.effects.Count; ei++)
                {
                    var effect = ability.effects[ei];
                    if (Rand.Value > effect.chance) continue;

                    var worker = workers[ei];
                    switch (effect.type)
                    {
                        case AbilityEffectType.Teleport:
                        case AbilityEffectType.Terraform:
                        case AbilityEffectType.Summon:
                            worker.Apply(effect, new LocalTargetInfo(cell), caster);
                            applied = true;
                            break;
                        default:
                            _thingBuffer.Clear();
                            _thingBuffer.AddRange(cell.GetThingList(map));
                            foreach (var thing in _thingBuffer)
                            {
                                if (thing == caster && !effect.canHurtSelf) continue;
                                worker.Apply(effect, new LocalTargetInfo(thing), caster);
                                applied = true;
                            }
                            break;
                    }
                }
            }

            return applied;
        }

        private static void UpdateRStackingStates()
        {
            // 基于 tick 的堆叠逻辑更新
        }

        private static void ProcessPendingRSecondStages(int tick)
        {
            // 处理 R 技能二段触发
        }

        private static bool TryHandleGeneralizedRStackHotkey(Pawn caster, CompCharacterAbilityRuntime abilityComp, ModularAbilityDef ability, int tick, AbilityRuntimeHotkeySlot slot, AbilityRuntimeComponentConfig rComp)
        {
            // 处理 R 堆叠
            return true;
        }
    }
}
