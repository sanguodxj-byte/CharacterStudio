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
    public class AbilityHotkeyRuntimeComponent : GameComponent
    {
        private enum AbilityHotkeySlot
        {
            Q,
            W,
            E,
            R,
            T,
            A,
            S,
            D,
            F,
            Z,
            X,
            C,
            V
        }

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
            KeyCode.W,
            KeyCode.E,
            KeyCode.R,
            KeyCode.T,
            KeyCode.A,
            KeyCode.S,
            KeyCode.D,
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

            // ── 热键检测：每 Unity 帧都必须检测（Input.GetKeyDown 仅在按下那帧返回 true），
            //    不能放在 lastProcessedTick 节流保护之内，否则同 tick 后续帧全部被 return 跳过。
            if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.Q))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.Q, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.W))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.W, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.E))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.E, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.R))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.R, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.T))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.T, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.A))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.A, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.S))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.S, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.D))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.D, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.F))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.F, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.Z))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.Z, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.X))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.X, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.C))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.C, tick);
            }
            else if (globalHotkeysEnabled && Input.GetKeyDown(KeyCode.V))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.V, tick);
            }

            // ── 以下逻辑基于 tick 驱动，每 tick 只需执行一次 ──
            if (tick == lastProcessedTick)
            {
                return;
            }
            lastProcessedTick = tick;

            UpdateRStackingStates();
            ProcessPendingRSecondStages(tick);
        }

        /// <summary>获取指定槽位的当前 CD 截止 Tick</summary>
        private static int GetSlotCooldownUntil(CompCharacterAbilityRuntime comp, AbilityHotkeySlot slot)
        {
            var state = comp.RuntimeState;
            switch (slot)
            {
                case AbilityHotkeySlot.Q: return state.qCooldownUntilTick;
                case AbilityHotkeySlot.W: return state.wCooldownUntilTick;
                case AbilityHotkeySlot.E: return state.eCooldownUntilTick;
                case AbilityHotkeySlot.R: return state.rCooldownUntilTick;
                case AbilityHotkeySlot.T: return state.tCooldownUntilTick;
                case AbilityHotkeySlot.A: return state.aCooldownUntilTick;
                case AbilityHotkeySlot.S: return state.sCooldownUntilTick;
                case AbilityHotkeySlot.D: return state.dCooldownUntilTick;
                case AbilityHotkeySlot.F: return state.fCooldownUntilTick;
                case AbilityHotkeySlot.Z: return state.zCooldownUntilTick;
                case AbilityHotkeySlot.X: return state.xCooldownUntilTick;
                case AbilityHotkeySlot.C: return state.cCooldownUntilTick;
                case AbilityHotkeySlot.V: return state.vCooldownUntilTick;
                default: return 0;
            }
        }

        /// <summary>写入指定槽位的 CD 截止 Tick</summary>
        private static void SetSlotCooldown(CompCharacterAbilityRuntime comp, AbilityHotkeySlot slot, int untilTick)
        {
            var state = comp.RuntimeState;
            switch (slot)
            {
                case AbilityHotkeySlot.Q: state.qCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.W: state.wCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.E: state.eCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.R: state.rCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.T: state.tCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.A: state.aCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.S: state.sCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.D: state.dCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.F: state.fCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.Z: state.zCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.X: state.xCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.C: state.cCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.V: state.vCooldownUntilTick = untilTick; break;
                default: break;
            }
        }

        private static void TryCastForSelectedPawn(AbilityHotkeySlot slot, int tick)
        {
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
                // 写入最小 CD（0.5s），取 max(当前CD, 最小CD) 以免覆盖 E 技能自己设置的更长 CD
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

        private static string ResolveAbilityDefNameBySlot(SkinAbilityHotkeyConfig hotkeys, AbilityHotkeySlot slot, CompCharacterAbilityRuntime abilityComp, int tick)
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

            string result = hotkeys[slot.ToString()];
            return !string.IsNullOrEmpty(result) ? result : hotkeys["R"];
        }

        private static string GetActiveSlotOverrideAbilityDefName(CompCharacterAbilityRuntime abilityComp, AbilityHotkeySlot slot, int tick)
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

        private static void GetSlotOverrideState(CompCharacterAbilityRuntime abilityComp, AbilityHotkeySlot slot, out string abilityDefName, out int expireTick)
        {
            var state = abilityComp.RuntimeState;
            switch (slot)
            {
                case AbilityHotkeySlot.Q:
                    abilityDefName = state.qOverrideAbilityDefName;
                    expireTick = state.qOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.W:
                    abilityDefName = state.wOverrideAbilityDefName;
                    expireTick = state.wOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.E:
                    abilityDefName = state.eOverrideAbilityDefName;
                    expireTick = state.eOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.T:
                    abilityDefName = state.tOverrideAbilityDefName;
                    expireTick = state.tOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.A:
                    abilityDefName = state.aOverrideAbilityDefName;
                    expireTick = state.aOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.S:
                    abilityDefName = state.sOverrideAbilityDefName;
                    expireTick = state.sOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.D:
                    abilityDefName = state.dOverrideAbilityDefName;
                    expireTick = state.dOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.F:
                    abilityDefName = state.fOverrideAbilityDefName;
                    expireTick = state.fOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.Z:
                    abilityDefName = state.zOverrideAbilityDefName;
                    expireTick = state.zOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.X:
                    abilityDefName = state.xOverrideAbilityDefName;
                    expireTick = state.xOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.C:
                    abilityDefName = state.cOverrideAbilityDefName;
                    expireTick = state.cOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.V:
                    abilityDefName = state.vOverrideAbilityDefName;
                    expireTick = state.vOverrideExpireTick;
                    break;
                default:
                    abilityDefName = state.rOverrideAbilityDefName;
                    expireTick = state.rOverrideExpireTick;
                    break;
            }
        }

        private static void ClearSlotOverrideState(CompCharacterAbilityRuntime abilityComp, AbilityHotkeySlot slot)
        {
            var state = abilityComp.RuntimeState;
            switch (slot)
            {
                case AbilityHotkeySlot.Q:
                    state.qOverrideAbilityDefName = string.Empty;
                    state.qOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.W:
                    state.wOverrideAbilityDefName = string.Empty;
                    state.wOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.E:
                    state.eOverrideAbilityDefName = string.Empty;
                    state.eOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.T:
                    state.tOverrideAbilityDefName = string.Empty;
                    state.tOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.A:
                    state.aOverrideAbilityDefName = string.Empty;
                    state.aOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.S:
                    state.sOverrideAbilityDefName = string.Empty;
                    state.sOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.D:
                    state.dOverrideAbilityDefName = string.Empty;
                    state.dOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.F:
                    state.fOverrideAbilityDefName = string.Empty;
                    state.fOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.Z:
                    state.zOverrideAbilityDefName = string.Empty;
                    state.zOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.X:
                    state.xOverrideAbilityDefName = string.Empty;
                    state.xOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.C:
                    state.cOverrideAbilityDefName = string.Empty;
                    state.cOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.V:
                    state.vOverrideAbilityDefName = string.Empty;
                    state.vOverrideExpireTick = -1;
                    break;
                default:
                    state.rOverrideAbilityDefName = string.Empty;
                    state.rOverrideExpireTick = -1;
                    break;
            }
        }

        private static bool TryCastConfiguredAbility(Pawn caster, CompCharacterAbilityRuntime abilityComp, ModularAbilityDef ability, int tick, AbilityHotkeySlot slot)
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

        private static bool TryHandleGeneralizedRStackHotkey(Pawn caster, CompCharacterAbilityRuntime abilityComp, ModularAbilityDef ability, int tick, AbilityHotkeySlot slot, AbilityRuntimeComponentConfig rComp)
        {
            int delayTicks = rComp.delayTicks >= 0 ? rComp.delayTicks : DefaultRSecondStageDelayTicks;
            int delaySec = Mathf.RoundToInt(delayTicks / 60f);

            if (!abilityComp.RSecondStageReady || !string.Equals(abilityComp.RStackAbilityDefName, ability.defName, System.StringComparison.OrdinalIgnoreCase))
            {
                abilityComp.RStackingEnabled = !abilityComp.RStackingEnabled;
                abilityComp.RStackAbilityDefName = abilityComp.RStackingEnabled ? (ability.defName ?? string.Empty) : string.Empty;
                if (abilityComp.RStackingEnabled)
                {
                    abilityComp.RStackCount = 0;
                    abilityComp.RSecondStageHasTarget = false;
                    abilityComp.RSecondStageTargetCell = IntVec3.Invalid;
                    abilityComp.RSecondStageExecuteTick = -1;
                    abilityComp.RSecondStageReady = false;
                    Messages.Message("CS_Ability_R_StackingOn".Translate(), MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message("CS_Ability_R_StackingOff".Translate(), MessageTypeDefOf.NeutralEvent, false);
                }
                return true;
            }

            IntVec3 targetCell = global::Verse.UI.MouseCell();
            if (caster.Map == null || !targetCell.InBounds(caster.Map))
            {
                Messages.Message("CS_Ability_R_TargetInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            abilityComp.RSecondStageHasTarget = true;
            abilityComp.RSecondStageTargetCell = targetCell;
            abilityComp.RSecondStageExecuteTick = tick + delayTicks;
            abilityComp.RSecondStageReady = false;
            abilityComp.RStackCount = 0;

            Messages.Message("CS_Ability_R_SecondStageArmed".Translate(delaySec), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        // 追踪当前帧是否有任何 Pawn 处于 rStacking 状态，避免无状态时的全量扫描
        private static int lastRStackingCheckTick = -1;
        private static bool anyRStackingActive = false;

        private static void UpdateRStackingStates()
        {
            if (Current.Game == null) return;

            // 每 30 Tick 重新检测一次是否有 Pawn 处于 rStacking，避免每帧全量扫描
            int tick = Find.TickManager?.TicksGame ?? 0;
            bool needFullScan = !anyRStackingActive || (tick - lastRStackingCheckTick >= 30);
            if (!needFullScan) return;
            lastRStackingCheckTick = tick;

            var allPawns = Current.Game.Maps
                .Where(m => m != null)
                .SelectMany(m => m.mapPawns.AllPawnsSpawned)
                .Where(p => p != null && p.Spawned)
                .ToList();

            anyRStackingActive = false;
            foreach (var pawn in allPawns)
            {
                if (!AbilityTimeStopRuntimeController.CanPawnAct(pawn))
                {
                    LastBusyStanceByPawn.Remove(pawn);
                    continue;
                }

                var comp = pawn.GetComp<CompCharacterAbilityRuntime>();
                if (comp == null)
                {
                    LastBusyStanceByPawn.Remove(pawn);
                    continue;
                }

                if (comp.RSecondStageHasTarget && comp.RSecondStageExecuteTick >= 0)
                {
                    anyRStackingActive = true;
                }

                if (!comp.RStackingEnabled || comp.RSecondStageReady)
                {
                    LastBusyStanceByPawn.Remove(pawn);
                    continue;
                }
                anyRStackingActive = true;

                AbilityRuntimeComponentConfig? rComp = ResolveRStackComponentConfig(pawn, comp);
                if (rComp == null || !rComp.enabled)
                {
                    LastBusyStanceByPawn.Remove(pawn);
                    continue;
                }

                int requiredStacks = rComp.requiredStacks > 0 ? rComp.requiredStacks : DefaultRRequiredStacks;

                var busy = pawn.stances?.curStance as Stance_Busy;
                if (busy == null || busy.verb == null)
                {
                    LastBusyStanceByPawn.Remove(pawn);
                    continue;
                }

                bool isAttackVerb = busy.verb.IsMeleeAttack || (busy.verb.verbProps != null && busy.verb.verbProps.IsMeleeAttack);
                if (!isAttackVerb)
                {
                    continue;
                }

                Stance_Busy lastBusy;
                bool hadLast = LastBusyStanceByPawn.TryGetValue(pawn, out lastBusy);
                if (!hadLast || lastBusy != busy)
                {
                    LastBusyStanceByPawn[pawn] = busy;
                    comp.RStackCount++;

                    if (comp.RStackCount >= requiredStacks)
                    {
                        comp.RStackCount = requiredStacks;
                        comp.RStackingEnabled = false;
                        comp.RSecondStageReady = true;
                        Messages.Message("CS_Ability_R_Ready".Translate(), MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        Messages.Message("CS_Ability_R_StackGain".Translate(comp.RStackCount, requiredStacks), MessageTypeDefOf.NeutralEvent, false);
                    }
                }
            }
        }

        private static void ProcessPendingRSecondStages(int tick)
        {
            if (Current.Game == null) return;
            // 无任何 Pawn 有挂起的二段蓄力时跳过全量扫描
            if (!anyRStackingActive) return;

            foreach (var map in Current.Game.Maps)
            {
                if (map == null) continue;

                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn == null) continue;
                    if (!AbilityTimeStopRuntimeController.CanPawnAct(pawn)) continue;

                    var comp = pawn.GetComp<CompCharacterAbilityRuntime>();
                    if (comp == null) continue;

                    if (!comp.RSecondStageHasTarget || comp.RSecondStageExecuteTick < 0)
                    {
                        continue;
                    }

                    if (tick < comp.RSecondStageExecuteTick)
                    {
                        continue;
                    }

                    AbilityRuntimeComponentConfig? rComp = ResolveRStackComponentConfig(pawn, comp);
                    ExecuteRSecondStage(pawn, comp, rComp);
                }
            }
        }

        private static void ExecuteRSecondStage(Pawn caster, CompCharacterAbilityRuntime abilityComp, AbilityRuntimeComponentConfig? rComp)
        {
            if (caster.Map == null || !abilityComp.RSecondStageHasTarget || !abilityComp.RSecondStageTargetCell.IsValid)
            {
                ResetRSecondStageState(abilityComp);
                return;
            }

            IntVec3 dest = abilityComp.RSecondStageTargetCell;
            if (!dest.InBounds(caster.Map) || !dest.Standable(caster.Map))
            {
                if (!CellFinder.TryFindRandomCellNear(dest, caster.Map, 3, c => c.Standable(caster.Map), out dest))
                {
                    ResetRSecondStageState(abilityComp);
                    return;
                }
            }

            caster.Position = dest;
            caster.Notify_Teleported(true, true);
            FleckMaker.ThrowDustPuff(dest, caster.Map, 2.0f);

            DamageDef damageDef = rComp?.waveDamageDef ?? DamageDefOf.Bomb;
            float wave1Radius = rComp?.wave1Radius > 0f ? rComp.wave1Radius : 3f;
            float wave2Radius = rComp?.wave2Radius > 0f ? rComp.wave2Radius : 6f;
            float wave3Radius = rComp?.wave3Radius > 0f ? rComp.wave3Radius : 9f;
            float wave1Damage = rComp?.wave1Damage > 0f ? rComp.wave1Damage : 80f;
            float wave2Damage = rComp?.wave2Damage > 0f ? rComp.wave2Damage : 140f;
            float wave3Damage = rComp?.wave3Damage > 0f ? rComp.wave3Damage : 220f;

            ApplyDamageWave(caster, dest, wave1Radius, wave1Damage, damageDef);
            ApplyDamageWave(caster, dest, wave2Radius, wave2Damage, damageDef);
            ApplyDamageWave(caster, dest, wave3Radius, wave3Damage, damageDef);

            Messages.Message("CS_Ability_R_SecondStageCast".Translate(), MessageTypeDefOf.PositiveEvent, false);
            ResetRSecondStageState(abilityComp);
        }

        private static void ResetRSecondStageState(CompCharacterAbilityRuntime abilityComp)
        {
            abilityComp.RSecondStageHasTarget = false;
            abilityComp.RSecondStageTargetCell = IntVec3.Invalid;
            abilityComp.RSecondStageExecuteTick = -1;
            abilityComp.RSecondStageReady = false;
            abilityComp.RStackCount = 0;
        }

        // 复用的伤害波 effect 对象，避免每波 new 分配
        private static readonly AbilityEffectConfig _waveEffectCache = new AbilityEffectConfig
        {
            type   = AbilityEffectType.Damage,
            chance = 1f
        };

        private static void ApplyDamageWave(Pawn caster, IntVec3 center, float radius, float amount, DamageDef damageDef)
        {
            if (caster.Map == null) return;

            GenExplosion.DoExplosion(
                center: center,
                map: caster.Map,
                radius: radius,
                damType: damageDef,
                instigator: caster,
                damAmount: Mathf.RoundToInt(amount),
                ignoredThings: new List<Thing> { caster },
                propagationSpeed: 1.5f
            );
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

            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);
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
            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);
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

            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);

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
            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);
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
            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);

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

                tex = RuntimeAssetLoader.LoadTextureRaw(iconPath, true);
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

            // 性能优化：提前按 effect.type 缓存 worker，避免每格重复 GetWorker 调用
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
                            // 复用 buffer 避免每格 List 分配
                            _thingBuffer.Clear();
                            _thingBuffer.AddRange(cell.GetThingList(map));
                            foreach (var thing in _thingBuffer)
                            {
                                // 根据 canHurtSelf 配置过滤自身伤害
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

        private static IEnumerable<IntVec3> BuildQModeCells(Pawn caster, int modeIndex)
        {
            var map = caster.Map;
            if (map == null)
            {
                return new List<IntVec3>();
            }

            IntVec3 pos = caster.Position;
            IntVec3 forward = caster.Rotation.FacingCell;
            IntVec3 right = GetRight(forward);

            var cells = new HashSet<IntVec3>();

            switch (modeIndex)
            {
                case 0:
                    cells.Add(pos + forward);
                    break;
                case 1:
                    cells.Add(pos + forward * 2);
                    break;
                case 2:
                    for (int d = 1; d <= 2; d++)
                    {
                        IntVec3 center = pos + forward * d;
                        int halfWidth = d - 1;
                        for (int w = -halfWidth; w <= halfWidth; w++)
                        {
                            cells.Add(center + right * w);
                        }
                    }
                    break;
                default:
                    int radius = 5;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        for (int dz = -radius; dz <= radius; dz++)
                        {
                            int sqrDist = dx * dx + dz * dz;
                            if (sqrDist > radius * radius)
                            {
                                continue;
                            }

                            int dot = dx * forward.x + dz * forward.z;
                            if (dot < 0)
                            {
                                continue;
                            }

                            cells.Add(pos + new IntVec3(dx, 0, dz));
                        }
                    }
                    break;
            }

            return cells.Where(c => c.InBounds(map));
        }

        private static IEnumerable<IntVec3> BuildAbilityAreaCells(Pawn caster, ModularAbilityDef ability, IntVec3 impactCenter)
        {
            return AbilityAreaUtility.BuildAbilityAreaCells(caster, ability, impactCenter);
        }

        private static IEnumerable<IntVec3> BuildIrregularPatternCells(IntVec3 anchor, IntVec3 forward, IntVec3 right, string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                yield break;
            }

            string normalized = pattern!.Replace("\r", string.Empty).Replace("/", "\n");
            string[] rows = normalized
                .Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(row => row.Trim())
                .Where(row => row.Length > 0)
                .ToArray();
            if (rows.Length == 0)
            {
                yield break;
            }

            int rowCenter = rows.Length / 2;
            int maxWidth = rows.Max(row => row.Length);
            int colCenter = maxWidth / 2;

            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                string row = rows[rowIndex];
                for (int colIndex = 0; colIndex < row.Length; colIndex++)
                {
                    if (!IsFilledPatternCell(row[colIndex]))
                    {
                        continue;
                    }

                    int forwardOffset = rowCenter - rowIndex;
                    int sideOffset = colIndex - colCenter;
                    yield return anchor + forward * forwardOffset + right * sideOffset;
                }
            }
        }

        private static bool IsFilledPatternCell(char token)
        {
            return token == '1' || token == 'x' || token == 'X' || token == '#';
        }

        private static IntVec3 GetFacingDirection(IntVec3 origin, IntVec3 target, IntVec3 fallback)
        {
            IntVec3 delta = target - origin;
            if (delta == IntVec3.Zero)
            {
                return fallback;
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
            {
                return delta.x >= 0 ? IntVec3.East : IntVec3.West;
            }

            return delta.z >= 0 ? IntVec3.North : IntVec3.South;
        }

        private static AbilityRuntimeComponentConfig? GetRuntimeComponent(ModularAbilityDef? ability, AbilityRuntimeComponentType type)
        {
            if (ability?.runtimeComponents == null)
            {
                return null;
            }

            return ability.runtimeComponents.FirstOrDefault(c => c != null && c.enabled && c.type == type);
        }

        private static AbilityRuntimeComponentConfig? GetSmartJumpComponent(ModularAbilityDef? ability)
        {
            AbilityRuntimeComponentConfig? smart = GetRuntimeComponent(ability, AbilityRuntimeComponentType.SmartJump);
            return smart ?? GetRuntimeComponent(ability, AbilityRuntimeComponentType.EShortJump);
        }

        private static bool TryCastSmartJumpAbility(Pawn caster, CompCharacterAbilityRuntime abilityComp, ModularAbilityDef ability, int tick, AbilityRuntimeComponentConfig jumpComp, AbilityHotkeySlot slot)
        {
            int cooldownUntil = GetSlotCooldownUntil(abilityComp, slot);
            if (tick < cooldownUntil)
            {
                int remain = cooldownUntil - tick;
                string key = slot == AbilityHotkeySlot.E
                    ? "CS_Ability_Hotkey_ECooldown"
                    : "CS_Ability_Hotkey_SlotCooldown";
                string slotLabel = slot.ToString();
                Messages.Message(
                    slot == AbilityHotkeySlot.E
                        ? key.Translate((remain / 60f).ToString("F1"))
                        : key.Translate(slotLabel, (remain / 60f).ToString("F1")),
                    MessageTypeDefOf.RejectInput,
                    false);
                return false;
            }

            AbilityDef? runtimeDef = AbilityGrantUtility.GetRuntimeAbilityDef(ability.defName);
            Ability? runtimeAbility = runtimeDef != null ? caster.abilities?.GetAbility(runtimeDef) : null;
            if (runtimeAbility == null)
            {
                return false;
            }

            Map? map = caster.Map;
            if (map == null)
            {
                return false;
            }

            IntVec3 mouseCell = global::Verse.UI.MouseCell();
            if (!mouseCell.InBounds(map))
            {
                Messages.Message("CS_Ability_R_TargetInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!TryResolveSmartJumpDestination(caster, jumpComp, slot, mouseCell, out IntVec3 jumpDestination))
            {
                Messages.Message("CS_Ability_R_TargetInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            LocalTargetInfo jumpTarget = new LocalTargetInfo(jumpDestination);
            runtimeAbility.QueueCastingJob(jumpTarget, LocalTargetInfo.Invalid);
            int cooldownTicks = jumpComp.cooldownTicks > 0 ? jumpComp.cooldownTicks : 120;
            SetSlotCooldown(abilityComp, slot, tick + cooldownTicks);
            return true;
        }

        private static bool TryResolveSmartJumpDestination(Pawn caster, AbilityRuntimeComponentConfig jumpComp, AbilityHotkeySlot slot, IntVec3 mouseCell, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            Map? map = caster.Map;
            if (map == null)
            {
                return false;
            }

            IntVec3 origin = caster.Position;
            int maxDistance = jumpComp.jumpDistance > 0 ? jumpComp.jumpDistance : 6;
            int fallbackRadius = jumpComp.findCellRadius >= 0 ? jumpComp.findCellRadius : 3;
            bool clampToMaxDistance = jumpComp.smartCastClampToMaxDistance;
            bool allowFallbackForward = jumpComp.smartCastAllowFallbackForward;

            IntVec3 desired = ResolveDesiredSmartJumpCell(origin, mouseCell, jumpComp, maxDistance, clampToMaxDistance, caster.Rotation.FacingCell);
            IntVec3 clamped = ClampJumpDestination(origin, desired, maxDistance);

            if (TryResolveStandableJumpCell(caster, clamped, fallbackRadius, allowFallbackForward, out destination))
            {
                return true;
            }

            if (clamped != mouseCell && TryResolveStandableJumpCell(caster, mouseCell, fallbackRadius, allowFallbackForward, out destination))
            {
                return true;
            }

            return false;
        }

        private static IntVec3 ResolveDesiredSmartJumpCell(IntVec3 origin, IntVec3 mouseCell, AbilityRuntimeComponentConfig jumpComp, int maxDistance, bool clampToMaxDistance, IntVec3 fallbackForward)
        {
            int offsetCells = jumpComp.smartCastOffsetCells > 0 ? jumpComp.smartCastOffsetCells : 1;
            if (!jumpComp.useMouseTargetCell)
            {
                if (fallbackForward == IntVec3.Zero)
                {
                    fallbackForward = IntVec3.North;
                }

                return origin + fallbackForward * offsetCells;
            }

            IntVec3 delta = mouseCell - origin;
            if (delta == IntVec3.Zero)
            {
                return origin;
            }

            if (clampToMaxDistance)
            {
                return ClampJumpDestination(origin, mouseCell, maxDistance);
            }

            IntVec3 step = new IntVec3(System.Math.Sign(delta.x), 0, System.Math.Sign(delta.z));
            if (offsetCells > 0)
            {
                int desiredSteps = System.Math.Max(System.Math.Abs(delta.x), System.Math.Abs(delta.z));
                int moveSteps = System.Math.Min(offsetCells, desiredSteps);
                return origin + step * moveSteps;
            }

            return mouseCell;
        }

        private static IntVec3 ClampJumpDestination(IntVec3 origin, IntVec3 desired, int maxDistance)
        {
            if (maxDistance <= 0)
            {
                return desired;
            }

            IntVec3 delta = desired - origin;
            int stepX = System.Math.Sign(delta.x);
            int stepZ = System.Math.Sign(delta.z);
            int steps = System.Math.Max(System.Math.Abs(delta.x), System.Math.Abs(delta.z));
            if (steps <= maxDistance)
            {
                return desired;
            }

            return origin + new IntVec3(stepX * maxDistance, 0, stepZ * maxDistance);
        }

        private static bool TryResolveStandableJumpCell(Pawn caster, IntVec3 desired, int fallbackRadius, bool allowFallbackForward, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            Map? map = caster.Map;
            if (map == null || !desired.InBounds(map))
            {
                return false;
            }

            if (desired.Standable(map))
            {
                destination = desired;
                return true;
            }

            if (allowFallbackForward && TryResolveForwardStandableJumpCell(caster, desired, out destination))
            {
                return true;
            }

            if (fallbackRadius > 0 && CellFinder.TryFindRandomCellNear(desired, map, fallbackRadius, c => c.InBounds(map) && c.Standable(map), out destination))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveForwardStandableJumpCell(Pawn caster, IntVec3 desired, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            Map? map = caster.Map;
            if (map == null)
            {
                return false;
            }

            IntVec3 current = caster.Position;
            IntVec3 delta = desired - current;
            IntVec3 step = new IntVec3(System.Math.Sign(delta.x), 0, System.Math.Sign(delta.z));
            if (step == IntVec3.Zero)
            {
                return false;
            }

            IntVec3 best = current;
            while (true)
            {
                IntVec3 next = best + step;
                if (!next.InBounds(map) || !next.Standable(map))
                {
                    break;
                }

                best = next;
                if (best == desired)
                {
                    destination = best;
                    return true;
                }
            }

            if (best != current)
            {
                destination = best;
                return true;
            }

            return false;
        }

        private static AbilityRuntimeComponentConfig? ResolveRStackComponentConfig(Pawn pawn, CompCharacterAbilityRuntime abilityComp)
        {
            CharacterAbilityLoadout? loadout = AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(pawn);
            SkinAbilityHotkeyConfig? hotkeys = loadout?.hotkeys;
            if (hotkeys == null || string.IsNullOrEmpty(hotkeys["R"]))
            {
                return null;
            }

            string sourceDefName = !string.IsNullOrWhiteSpace(abilityComp.RStackAbilityDefName)
                ? abilityComp.RStackAbilityDefName
                : hotkeys["R"];

            var rAbility = ResolveAbilityByDefName(loadout, sourceDefName);
            return GetRuntimeComponent(rAbility, AbilityRuntimeComponentType.RStackDetonation);
        }

        private static IntVec3 GetRight(IntVec3 forward)
        {
            if (forward == IntVec3.North)
            {
                return IntVec3.East;
            }
            if (forward == IntVec3.South)
            {
                return IntVec3.West;
            }
            if (forward == IntVec3.East)
            {
                return IntVec3.South;
            }
            return IntVec3.North;
        }
    }
}