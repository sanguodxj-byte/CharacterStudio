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
            R
        }

        private const int DefaultQWComboWindowTicks = 12;      // 0.2s
        private const int MinSlotCooldownTicks = 30;           // 0.5s — 所有槽位的最短 CD
        private const int DefaultEShortJumpCooldownTicks = 120; // 2.0s
        private const int DefaultEShortJumpDistance = 6;
        private const int DefaultEShortJumpFindCellRadius = 3;
        private const int DefaultRRequiredStacks = 7;
        private const int DefaultRSecondStageDelayTicks = 180;  // 3.0s

        private int lastProcessedTick = -1;

        private static readonly Dictionary<Pawn, Stance_Busy> LastBusyStanceByPawn = new Dictionary<Pawn, Stance_Busy>();

        public AbilityHotkeyRuntimeComponent(Game game)
        {
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
            if (Input.GetKeyDown(KeyCode.Q))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.Q, tick);
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.W, tick);
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.E, tick);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                TryCastForSelectedPawn(AbilityHotkeySlot.R, tick);
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
        private static int GetSlotCooldownUntil(CompPawnSkin comp, AbilityHotkeySlot slot)
        {
            switch (slot)
            {
                case AbilityHotkeySlot.Q: return comp.qCooldownUntilTick;
                case AbilityHotkeySlot.W: return comp.wCooldownUntilTick;
                case AbilityHotkeySlot.E: return comp.eCooldownUntilTick;
                default:                  return comp.rCooldownUntilTick;
            }
        }

        /// <summary>写入指定槽位的 CD 截止 Tick</summary>
        private static void SetSlotCooldown(CompPawnSkin comp, AbilityHotkeySlot slot, int untilTick)
        {
            switch (slot)
            {
                case AbilityHotkeySlot.Q: comp.qCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.W: comp.wCooldownUntilTick = untilTick; break;
                case AbilityHotkeySlot.E: comp.eCooldownUntilTick = untilTick; break;
                default:                  comp.rCooldownUntilTick = untilTick; break;
            }
        }

        private static void TryCastForSelectedPawn(AbilityHotkeySlot slot, int tick)
        {
            Pawn? selectedPawn = Find.Selector?.SingleSelectedThing as Pawn;
            if (selectedPawn == null || !selectedPawn.Spawned || selectedPawn.Map == null || !selectedPawn.Drafted || !selectedPawn.IsColonistPlayerControlled)
            {
                return;
            }

            if (GUIUtility.keyboardControl != 0)
                return;

            if (Find.Targeter != null && Find.Targeter.IsTargeting)
                return;

            if (IsBlockingWindowOpen())
                return;

            Pawn pawn = selectedPawn;
            var skinComp = pawn.GetComp<CompPawnSkin>();
            CharacterAbilityLoadout? loadout = AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(pawn);
            SkinAbilityHotkeyConfig? hotkeys = loadout?.hotkeys;

            if (skinComp == null || hotkeys == null || !hotkeys.enabled)
            {
                return;
            }

            // ── 最小槽位 CD 门控（0.5s）──
            int slotCdUntil = GetSlotCooldownUntil(skinComp, slot);
            if (tick < slotCdUntil)
            {
                int remain = slotCdUntil - tick;
                Messages.Message(
                    "CS_Ability_Hotkey_SlotCooldown".Translate(slot.ToString(), (remain / 60f).ToString("F1")),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            string abilityDefName = ResolveAbilityDefNameBySlot(hotkeys, slot, skinComp, tick);
            ModularAbilityDef? ability = ResolveAbilityByDefName(loadout, abilityDefName);
            if (AbilityVanillaFlightUtility.TryNotifyFlightFollowupFailure(pawn, ability))
            {
                return;
            }

            if (ability == null && slot != AbilityHotkeySlot.R)
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

            bool casted = false;
            switch (slot)
            {
                case AbilityHotkeySlot.Q:
                    if (ability != null)
                    {
                        casted = TryCastQSmartAbility(pawn, skinComp, ability, tick);
                    }
                    break;
                case AbilityHotkeySlot.W:
                    if (ability != null)
                    {
                        casted = TryCastDefaultAbility(pawn, ability);
                    }
                    break;
                case AbilityHotkeySlot.E:
                    if (ability != null)
                    {
                        casted = TryCastEShortJump(pawn, skinComp, ability, tick);
                    }
                    break;
                case AbilityHotkeySlot.R:
                    casted = TryHandleRHotkey(pawn, skinComp, ability, tick);
                    break;
            }

            if (casted)
            {
                // 写入最小 CD（0.5s），取 max(当前CD, 最小CD) 以免覆盖 E 技能自己设置的更长 CD
                int minCdEnd = tick + MinSlotCooldownTicks;
                int curCdEnd = GetSlotCooldownUntil(skinComp, slot);
                SetSlotCooldown(skinComp, slot, Mathf.Max(curCdEnd, minCdEnd));

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

                return windows.Any(w => w != null && (w.absorbInputAroundWindow || w.forcePause));
            }
            catch
            {
                return false;
            }
        }

        private static ModularAbilityDef? ResolveAbilityByDefName(CharacterAbilityLoadout? loadout, string abilityDefName)
        {
            return AbilityLoadoutRuntimeUtility.ResolveAbilityByDefName(loadout, abilityDefName);
        }

        private static string ResolveAbilityDefNameBySlot(SkinAbilityHotkeyConfig hotkeys, AbilityHotkeySlot slot, CompPawnSkin skinComp, int tick)
        {
            string overrideDefName = GetActiveSlotOverrideAbilityDefName(skinComp, slot, tick);
            if (!string.IsNullOrEmpty(overrideDefName))
            {
                return overrideDefName;
            }

            if (slot == AbilityHotkeySlot.Q)
            {
                return ResolveQModeAbilityDefName(hotkeys.qAbilityDefName, skinComp.qHotkeyModeIndex);
            }

            if (slot == AbilityHotkeySlot.W)
            {
                if (tick <= skinComp.qComboWindowEndTick && !string.IsNullOrEmpty(hotkeys.wComboAbilityDefName))
                {
                    return hotkeys.wComboAbilityDefName;
                }
            }

            switch (slot)
            {
                case AbilityHotkeySlot.W:
                    return hotkeys.wAbilityDefName;
                case AbilityHotkeySlot.E:
                    return hotkeys.eAbilityDefName;
                default:
                    return hotkeys.rAbilityDefName;
            }
        }

        private static string ResolveQModeAbilityDefName(string baseDefName, int modeIndex)
        {
            if (string.IsNullOrWhiteSpace(baseDefName))
            {
                return string.Empty;
            }

            int normalizedModeIndex = Mathf.Clamp(modeIndex, 0, 3);
            if (TryResolveSequentialQAbilityDefName(baseDefName, normalizedModeIndex, out string resolvedDefName))
            {
                return resolvedDefName;
            }

            return baseDefName;
        }

        private static bool TryResolveSequentialQAbilityDefName(string baseDefName, int modeIndex, out string resolvedDefName)
        {
            resolvedDefName = baseDefName;
            if (string.IsNullOrWhiteSpace(baseDefName))
            {
                return false;
            }

            int markerIndex = baseDefName.LastIndexOf("_Q", System.StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0 || markerIndex + 3 > baseDefName.Length)
            {
                return false;
            }

            char indexChar = baseDefName[markerIndex + 2];
            if (!char.IsDigit(indexChar))
            {
                return false;
            }

            string prefix = baseDefName.Substring(0, markerIndex + 2);
            string suffix = baseDefName.Substring(markerIndex + 3);
            resolvedDefName = $"{prefix}{modeIndex + 1}{suffix}";
            return true;
        }

        private static string GetActiveSlotOverrideAbilityDefName(CompPawnSkin skinComp, AbilityHotkeySlot slot, int tick)
        {
            string abilityDefName;
            int expireTick;
            GetSlotOverrideState(skinComp, slot, out abilityDefName, out expireTick);
            if (string.IsNullOrEmpty(abilityDefName))
            {
                return string.Empty;
            }

            if (expireTick >= 0 && tick > expireTick)
            {
                ClearSlotOverrideState(skinComp, slot);
                return string.Empty;
            }

            return abilityDefName;
        }

        private static void GetSlotOverrideState(CompPawnSkin skinComp, AbilityHotkeySlot slot, out string abilityDefName, out int expireTick)
        {
            switch (slot)
            {
                case AbilityHotkeySlot.Q:
                    abilityDefName = skinComp.qOverrideAbilityDefName;
                    expireTick = skinComp.qOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.W:
                    abilityDefName = skinComp.wOverrideAbilityDefName;
                    expireTick = skinComp.wOverrideExpireTick;
                    break;
                case AbilityHotkeySlot.E:
                    abilityDefName = skinComp.eOverrideAbilityDefName;
                    expireTick = skinComp.eOverrideExpireTick;
                    break;
                default:
                    abilityDefName = skinComp.rOverrideAbilityDefName;
                    expireTick = skinComp.rOverrideExpireTick;
                    break;
            }
        }

        private static void ClearSlotOverrideState(CompPawnSkin skinComp, AbilityHotkeySlot slot)
        {
            switch (slot)
            {
                case AbilityHotkeySlot.Q:
                    skinComp.qOverrideAbilityDefName = string.Empty;
                    skinComp.qOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.W:
                    skinComp.wOverrideAbilityDefName = string.Empty;
                    skinComp.wOverrideExpireTick = -1;
                    break;
                case AbilityHotkeySlot.E:
                    skinComp.eOverrideAbilityDefName = string.Empty;
                    skinComp.eOverrideExpireTick = -1;
                    break;
                default:
                    skinComp.rOverrideAbilityDefName = string.Empty;
                    skinComp.rOverrideExpireTick = -1;
                    break;
            }
        }

        private static bool TryCastQSmartAbility(Pawn caster, CompPawnSkin skinComp, ModularAbilityDef ability, int tick)
        {
            AbilityRuntimeComponentConfig? jumpComp = GetSmartJumpComponent(ability);
            if (jumpComp == null || !jumpComp.enabled)
            {
                return TryCastQModeAbility(caster, skinComp, ability, tick);
            }

            bool casted = TryCastSmartJumpAbility(caster, skinComp, ability, tick, jumpComp, AbilityHotkeySlot.Q);
            if (casted)
            {
                skinComp.qComboWindowEndTick = tick + GetQComboWindowTicks(ability);
            }

            return casted;
        }

        private static bool TryCastQModeAbility(Pawn caster, CompPawnSkin skinComp, ModularAbilityDef ability, int tick)
        {
            int modeIndex = skinComp.qHotkeyModeIndex;
            var cells = BuildQModeCells(caster, modeIndex);
            bool casted = ExecuteAbilityOnCells(caster, ability, cells);

            if (casted)
            {
                int nextMode = (modeIndex + 1) % 4;
                skinComp.qHotkeyModeIndex = nextMode;
                skinComp.qComboWindowEndTick = tick + GetQComboWindowTicks(ability);
                Messages.Message(
                    "CS_Ability_Hotkey_QMode".Translate(nextMode + 1),
                    MessageTypeDefOf.NeutralEvent,
                    false);
            }

            return casted;
        }

        private static bool TryCastEShortJump(Pawn caster, CompPawnSkin skinComp, ModularAbilityDef ability, int tick)
        {
            if (ability == null)
            {
                return false;
            }

            AbilityRuntimeComponentConfig? jumpComp = GetSmartJumpComponent(ability);
            if (jumpComp == null || !jumpComp.enabled)
            {
                return TryCastDefaultAbility(caster, ability);
            }

            return TryCastSmartJumpAbility(caster, skinComp, ability, tick, jumpComp, AbilityHotkeySlot.E);
        }

        private static bool TryHandleRHotkey(Pawn caster, CompPawnSkin skinComp, ModularAbilityDef? ability, int tick)
        {
            if (ability == null)
            {
                return false;
            }

            AbilityRuntimeComponentConfig? rComp = GetRuntimeComponent(ability, AbilityRuntimeComponentType.RStackDetonation);
            if (rComp == null || !rComp.enabled)
            {
                return TryCastDefaultAbility(caster, ability);
            }

            int delayTicks = rComp.delayTicks >= 0 ? rComp.delayTicks : DefaultRSecondStageDelayTicks;
            int delaySec = Mathf.RoundToInt(delayTicks / 60f);

            if (!skinComp.rSecondStageReady)
            {
                skinComp.rStackingEnabled = !skinComp.rStackingEnabled;
                if (skinComp.rStackingEnabled)
                {
                    skinComp.rStackCount = 0;
                    skinComp.rSecondStageHasTarget = false;
                    skinComp.rSecondStageTargetCell = IntVec3.Invalid;
                    skinComp.rSecondStageExecuteTick = -1;
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

            skinComp.rSecondStageHasTarget = true;
            skinComp.rSecondStageTargetCell = targetCell;
            skinComp.rSecondStageExecuteTick = tick + delayTicks;
            skinComp.rSecondStageReady = false;
            skinComp.rStackCount = 0;

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
                var comp = pawn.GetComp<CompPawnSkin>();
                if (comp == null)
                {
                    LastBusyStanceByPawn.Remove(pawn);
                    continue;
                }

                if (comp.rSecondStageHasTarget && comp.rSecondStageExecuteTick >= 0)
                {
                    anyRStackingActive = true;
                }

                if (!comp.rStackingEnabled || comp.rSecondStageReady)
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
                    comp.rStackCount++;

                    if (comp.rStackCount >= requiredStacks)
                    {
                        comp.rStackCount = requiredStacks;
                        comp.rStackingEnabled = false;
                        comp.rSecondStageReady = true;
                        Messages.Message("CS_Ability_R_Ready".Translate(), MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        Messages.Message("CS_Ability_R_StackGain".Translate(comp.rStackCount, requiredStacks), MessageTypeDefOf.NeutralEvent, false);
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

                    var comp = pawn.GetComp<CompPawnSkin>();
                    if (comp == null) continue;

                    if (!comp.rSecondStageHasTarget || comp.rSecondStageExecuteTick < 0)
                    {
                        continue;
                    }

                    if (tick < comp.rSecondStageExecuteTick)
                    {
                        continue;
                    }

                    AbilityRuntimeComponentConfig? rComp = ResolveRStackComponentConfig(pawn, comp);
                    ExecuteRSecondStage(pawn, comp, rComp);
                }
            }
        }

        private static void ExecuteRSecondStage(Pawn caster, CompPawnSkin skinComp, AbilityRuntimeComponentConfig? rComp)
        {
            if (caster.Map == null || !skinComp.rSecondStageHasTarget || !skinComp.rSecondStageTargetCell.IsValid)
            {
                ResetRSecondStageState(skinComp);
                return;
            }

            IntVec3 dest = skinComp.rSecondStageTargetCell;
            if (!dest.InBounds(caster.Map) || !dest.Standable(caster.Map))
            {
                if (!CellFinder.TryFindRandomCellNear(dest, caster.Map, 3, c => c.Standable(caster.Map), out dest))
                {
                    ResetRSecondStageState(skinComp);
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
            ResetRSecondStageState(skinComp);
        }

        private static void ResetRSecondStageState(CompPawnSkin skinComp)
        {
            skinComp.rSecondStageHasTarget = false;
            skinComp.rSecondStageTargetCell = IntVec3.Invalid;
            skinComp.rSecondStageExecuteTick = -1;
            skinComp.rSecondStageReady = false;
            skinComp.rStackCount = 0;
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

            // 复用缓存对象，仅更新变化字段
            _waveEffectCache.amount    = amount;
            _waveEffectCache.damageDef = damageDef;

            var worker = EffectWorkerFactory.GetWorker(AbilityEffectType.Damage);
            var map    = caster.Map;

            foreach (var cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map)) continue;

                _thingBuffer.Clear();
                _thingBuffer.AddRange(cell.GetThingList(map));
                foreach (var thing in _thingBuffer)
                {
                    // 过滤自身：R 技能爆发波不应伤害施法者
                    if (thing == caster) continue;
                    worker.Apply(_waveEffectCache, new LocalTargetInfo(thing), caster);
                }
            }
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

            var skinComp = caster.GetComp<CompPawnSkin>();
            if (skinComp != null)
            {
                int visualTicks = Mathf.Max((int)ability.warmupTicks, 8) + 8;
                skinComp.SetWeaponCarryCastingWindow(visualTicks);
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
            Map? map = caster.Map;
            if (map == null)
            {
                return new List<IntVec3>();
            }

            IntVec3 anchor = ModularAbilityDefExtensions.NormalizeAreaCenter(ability) == AbilityAreaCenter.Self
                ? caster.Position
                : impactCenter;
            IntVec3 forward = GetFacingDirection(caster.Position, impactCenter, caster.Rotation.FacingCell);
            IntVec3 right = GetRight(forward);
            AbilityAreaShape shape = ModularAbilityDefExtensions.NormalizeAreaShape(ability);
            float rawRadius = shape == AbilityAreaShape.Irregular
                ? Mathf.Max(0f, ability.radius)
                : Mathf.Max(ability.radius, 1f);
            int discreteRadius = Mathf.Max(1, Mathf.CeilToInt(rawRadius));
            var cells = new HashSet<IntVec3>();

            switch (shape)
            {
                case AbilityAreaShape.Line:
                    for (int step = 0; step <= discreteRadius; step++)
                    {
                        cells.Add(anchor + forward * step);
                    }
                    break;
                case AbilityAreaShape.Cone:
                    for (int distance = 0; distance <= discreteRadius; distance++)
                    {
                        IntVec3 rowCenter = anchor + forward * distance;
                        int halfWidth = distance;
                        for (int side = -halfWidth; side <= halfWidth; side++)
                        {
                            cells.Add(rowCenter + right * side);
                        }
                    }
                    break;
                case AbilityAreaShape.Cross:
                    cells.Add(anchor);
                    for (int step = 1; step <= discreteRadius; step++)
                    {
                        cells.Add(anchor + forward * step);
                        cells.Add(anchor - forward * step);
                        cells.Add(anchor + right * step);
                        cells.Add(anchor - right * step);
                    }
                    break;
                case AbilityAreaShape.Square:
                    for (int forwardOffset = -discreteRadius; forwardOffset <= discreteRadius; forwardOffset++)
                    {
                        for (int sideOffset = -discreteRadius; sideOffset <= discreteRadius; sideOffset++)
                        {
                            cells.Add(anchor + forward * forwardOffset + right * sideOffset);
                        }
                    }
                    break;
                case AbilityAreaShape.Irregular:
                    bool addedIrregular = false;
                    foreach (IntVec3 cell in BuildIrregularPatternCells(anchor, forward, right, ability.irregularAreaPattern))
                    {
                        cells.Add(cell);
                        addedIrregular = true;
                    }

                    if (!addedIrregular)
                    {
                        cells.Add(anchor);
                    }
                    break;
                default:
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(anchor, rawRadius, true))
                    {
                        cells.Add(cell);
                    }
                    break;
            }

            return cells.Where(c => c.InBounds(map));
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

        private static bool TryCastSmartJumpAbility(Pawn caster, CompPawnSkin skinComp, ModularAbilityDef ability, int tick, AbilityRuntimeComponentConfig jumpComp, AbilityHotkeySlot slot)
        {
            int cooldownUntil = GetSlotCooldownUntil(skinComp, slot);
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
            int cooldownTicks = jumpComp.cooldownTicks > 0 ? jumpComp.cooldownTicks : DefaultEShortJumpCooldownTicks;
            SetSlotCooldown(skinComp, slot, tick + cooldownTicks);
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
            int maxDistance = jumpComp.jumpDistance > 0 ? jumpComp.jumpDistance : DefaultEShortJumpDistance;
            int fallbackRadius = jumpComp.findCellRadius >= 0 ? jumpComp.findCellRadius : DefaultEShortJumpFindCellRadius;
            bool clampToMaxDistance = jumpComp.smartCastClampToMaxDistance || slot == AbilityHotkeySlot.E;
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

        private static int GetQComboWindowTicks(ModularAbilityDef ability)
        {
            var qComp = GetRuntimeComponent(ability, AbilityRuntimeComponentType.QComboWindow);
            return qComp != null && qComp.comboWindowTicks > 0 ? qComp.comboWindowTicks : DefaultQWComboWindowTicks;
        }

        private static AbilityRuntimeComponentConfig? ResolveRStackComponentConfig(Pawn pawn, CompPawnSkin skinComp)
        {
            CharacterAbilityLoadout? loadout = AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(pawn);
            SkinAbilityHotkeyConfig? hotkeys = loadout?.hotkeys;
            if (hotkeys == null || string.IsNullOrEmpty(hotkeys.rAbilityDefName))
            {
                return null;
            }

            var rAbility = ResolveAbilityByDefName(loadout, hotkeys.rAbilityDefName);
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