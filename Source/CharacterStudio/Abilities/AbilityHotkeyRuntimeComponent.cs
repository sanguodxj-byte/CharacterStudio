using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
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
            if (tick == lastProcessedTick)
            {
                return;
            }
            lastProcessedTick = tick;

            UpdateRStackingStates();
            ProcessPendingRSecondStages(tick);

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
        }

        private static void TryCastForSelectedPawn(AbilityHotkeySlot slot, int tick)
        {
            Pawn? selectedPawn = Find.Selector?.SingleSelectedThing as Pawn;
            if (selectedPawn == null || !selectedPawn.Spawned || selectedPawn.Map == null)
            {
                return;
            }

            Pawn pawn = selectedPawn;
            var skinComp = pawn.GetComp<CompPawnSkin>();
            var skin = skinComp?.ActiveSkin;
            if (skinComp == null || skin == null || skin.abilityHotkeys == null || !skin.abilityHotkeys.enabled)
            {
                return;
            }

            string abilityDefName = ResolveAbilityDefNameBySlot(skin.abilityHotkeys, slot, skinComp, tick);
            ModularAbilityDef? ability = ResolveAbilityByDefName(skin, abilityDefName);

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
                        casted = TryCastQModeAbility(pawn, skinComp, ability, tick);
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

            if (casted && ability != null)
            {
                Messages.Message(
                    "CS_Ability_Hotkey_CastSuccess".Translate(slot.ToString(), ability.label ?? ability.defName),
                    MessageTypeDefOf.NeutralEvent,
                    false);
            }
        }

        private static ModularAbilityDef? ResolveAbilityByDefName(PawnSkinDef skin, string abilityDefName)
        {
            if (string.IsNullOrEmpty(abilityDefName) || skin.abilities == null || skin.abilities.Count == 0)
            {
                return null;
            }

            return skin.abilities.FirstOrDefault(a => a != null && a.defName == abilityDefName);
        }

        private static string ResolveAbilityDefNameBySlot(SkinAbilityHotkeyConfig hotkeys, AbilityHotkeySlot slot, CompPawnSkin skinComp, int tick)
        {
            if (slot == AbilityHotkeySlot.W)
            {
                if (tick <= skinComp.qComboWindowEndTick && !string.IsNullOrEmpty(hotkeys.wComboAbilityDefName))
                {
                    return hotkeys.wComboAbilityDefName;
                }
            }

            switch (slot)
            {
                case AbilityHotkeySlot.Q:
                    return hotkeys.qAbilityDefName;
                case AbilityHotkeySlot.W:
                    return hotkeys.wAbilityDefName;
                case AbilityHotkeySlot.E:
                    return hotkeys.eAbilityDefName;
                default:
                    return hotkeys.rAbilityDefName;
            }
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

            AbilityRuntimeComponentConfig? jumpComp = GetRuntimeComponent(ability, AbilityRuntimeComponentType.EShortJump);
            if (jumpComp == null || !jumpComp.enabled)
            {
                return TryCastDefaultAbility(caster, ability);
            }

            int cooldownTicks = jumpComp.cooldownTicks > 0 ? jumpComp.cooldownTicks : DefaultEShortJumpCooldownTicks;
            int jumpDistance = jumpComp.jumpDistance > 0 ? jumpComp.jumpDistance : DefaultEShortJumpDistance;
            int findCellRadius = jumpComp.findCellRadius >= 0 ? jumpComp.findCellRadius : DefaultEShortJumpFindCellRadius;

            if (tick < skinComp.eCooldownUntilTick)
            {
                int remain = skinComp.eCooldownUntilTick - tick;
                Messages.Message(
                    "CS_Ability_Hotkey_ECooldown".Translate((remain / 60f).ToString("F1")),
                    MessageTypeDefOf.RejectInput,
                    false);
                return false;
            }

            if (caster.Map == null)
            {
                return false;
            }

            IntVec3 desired = caster.Position + caster.Rotation.FacingCell * jumpDistance;
            IntVec3 dest = desired;

            if (!dest.InBounds(caster.Map) || !dest.Standable(caster.Map))
            {
                if (!CellFinder.TryFindRandomCellNear(desired, caster.Map, findCellRadius, c => c.Standable(caster.Map), out dest))
                {
                    return false;
                }
            }

            caster.Position = dest;
            caster.Notify_Teleported(true, true);
            FleckMaker.ThrowDustPuff(dest, caster.Map, 1.2f);

            if (jumpComp.triggerAbilityEffectsAfterJump)
            {
                ExecuteAbilityOnCells(caster, ability, new List<IntVec3> { dest });
            }

            skinComp.eCooldownUntilTick = tick + cooldownTicks;
            return true;
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

        private static void UpdateRStackingStates()
        {
            if (Current.Game == null) return;

            var allPawns = Current.Game.Maps
                .Where(m => m != null)
                .SelectMany(m => m.mapPawns.AllPawnsSpawned)
                .Where(p => p != null && p.Spawned)
                .ToList();

            foreach (var pawn in allPawns)
            {
                var comp = pawn.GetComp<CompPawnSkin>();
                if (comp == null || !comp.rStackingEnabled || comp.rSecondStageReady)
                {
                    LastBusyStanceByPawn.Remove(pawn);
                    continue;
                }

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

        private static void ApplyDamageWave(Pawn caster, IntVec3 center, float radius, float amount, DamageDef damageDef)
        {
            if (caster.Map == null) return;

            var effect = new AbilityEffectConfig
            {
                type = AbilityEffectType.Damage,
                amount = amount,
                chance = 1f,
                damageDef = damageDef
            };
            var worker = EffectWorkerFactory.GetWorker(AbilityEffectType.Damage);

            var cells = GenRadial.RadialCellsAround(center, radius, true);
            foreach (var cell in cells)
            {
                if (!cell.InBounds(caster.Map)) continue;

                var things = cell.GetThingList(caster.Map).ToList();
                foreach (var thing in things)
                {
                    worker.Apply(effect, new LocalTargetInfo(thing), caster);
                }
            }
        }

        private static bool TryCastDefaultAbility(Pawn caster, ModularAbilityDef ability)
        {
            if (ability == null)
            {
                return false;
            }

            IEnumerable<IntVec3> cells;
            IntVec3 center = caster.Position + caster.Rotation.FacingCell;

            switch (ability.carrierType)
            {
                case AbilityCarrierType.Self:
                    cells = new List<IntVec3> { caster.Position };
                    break;
                case AbilityCarrierType.Area:
                    float radius = Mathf.Max(ability.radius, 1f);
                    cells = GenRadial.RadialCellsAround(center, radius, true);
                    break;
                case AbilityCarrierType.Touch:
                case AbilityCarrierType.Target:
                case AbilityCarrierType.Projectile:
                default:
                    cells = new List<IntVec3> { center };
                    break;
            }

            return ExecuteAbilityOnCells(caster, ability, cells);
        }

        private static bool ExecuteAbilityOnCells(Pawn caster, ModularAbilityDef ability, IEnumerable<IntVec3> rawCells)
        {
            if (caster.Map == null || ability == null || ability.effects == null || ability.effects.Count == 0)
            {
                return false;
            }

            bool applied = false;
            var map = caster.Map;
            var distinctCells = new HashSet<IntVec3>(rawCells.Where(c => c.InBounds(map)));

            foreach (var cell in distinctCells)
            {
                foreach (var effect in ability.effects)
                {
                    if (Rand.Value > effect.chance)
                    {
                        continue;
                    }

                    var worker = EffectWorkerFactory.GetWorker(effect.type);
                    switch (effect.type)
                    {
                        case AbilityEffectType.Teleport:
                        case AbilityEffectType.Terraform:
                        case AbilityEffectType.Summon:
                            worker.Apply(effect, new LocalTargetInfo(cell), caster);
                            applied = true;
                            break;
                        default:
                            var things = cell.GetThingList(map).ToList();
                            foreach (var thing in things)
                            {
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

        private static AbilityRuntimeComponentConfig? GetRuntimeComponent(ModularAbilityDef? ability, AbilityRuntimeComponentType type)
        {
            if (ability?.runtimeComponents == null)
            {
                return null;
            }

            return ability.runtimeComponents.FirstOrDefault(c => c != null && c.enabled && c.type == type);
        }

        private static int GetQComboWindowTicks(ModularAbilityDef ability)
        {
            var qComp = GetRuntimeComponent(ability, AbilityRuntimeComponentType.QComboWindow);
            return qComp != null && qComp.comboWindowTicks > 0 ? qComp.comboWindowTicks : DefaultQWComboWindowTicks;
        }

        private static AbilityRuntimeComponentConfig? ResolveRStackComponentConfig(Pawn pawn, CompPawnSkin skinComp)
        {
            var skin = skinComp?.ActiveSkin;
            var hotkeys = skin?.abilityHotkeys;
            if (skin == null || hotkeys == null || string.IsNullOrEmpty(hotkeys.rAbilityDefName))
            {
                return null;
            }

            var rAbility = ResolveAbilityByDefName(skin, hotkeys.rAbilityDefName);
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
