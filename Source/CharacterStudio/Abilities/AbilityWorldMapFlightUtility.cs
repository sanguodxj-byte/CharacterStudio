using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 跨地图飞行工具类。
    ///
    /// 起飞流程（不使用 FlyPawnLeaving Skyfaller）：
    /// 1. 角色施放技能 → 打开世界地图选择目标地块
    /// 2. 设置 FlightState（现有飞行组件渲染浮空上升）+ handoffTick
    /// 3. CompCharacterAbilityRuntime.CompTick 检测 handoffTick 到期
    /// 4. ExecuteHandoff：DeSpawn Pawn → 装入 ActiveTransporterInfo → 创建 TravellingTransporters
    ///
    /// 着陆流程（与之前相同）：
    /// TravellingTransporters 到达 → ArrivalAction.Arrived
    /// </summary>
    public static class AbilityWorldMapFlightUtility
    {
        /// <summary>默认起飞持续时间（ticks），约 2.5 秒</summary>
        private const int DefaultTakeoffTicks = 150;

        // ──────────── 公开 API ────────────

        public static bool CanLaunchWorldMapFlight(Pawn? caster, out string failureReason)
        {
            failureReason = string.Empty;

            if (caster == null) { failureReason = "caster is null"; return false; }
            if (caster.Map == null) { failureReason = "caster map is null"; return false; }
            if (!caster.IsColonist && !caster.IsPrisonerOfColony)
            { failureReason = "CS_Ability_Fail_NotColonist".Translate(); return false; }
            if (caster.Downed)
            { failureReason = "CS_Ability_Fail_Downed".Translate(); return false; }
            if (caster.Map.roofGrid.Roofed(caster.Position))
            { failureReason = "CS_Ability_Fail_Roofed".Translate(); return false; }

            CompCharacterAbilityRuntime? abilityComp = caster.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp != null && abilityComp.IsInWorldMapFlight)
            { failureReason = "CS_Ability_Fail_AlreadyInFlight".Translate(); return false; }

            return true;
        }

        public static bool TryLaunchWorldMapFlight(
            Pawn caster,
            PlanetTile destinationTile,
            TransportersArrivalAction? arrivalAction,
            AbilityRuntimeComponentConfig component,
            string sourceAbilityDefName,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (caster == null || caster.Map == null)
            { failureReason = "caster or map is null"; return false; }

            if (!destinationTile.Valid)
            { failureReason = "CS_Ability_Fail_InvalidDestination".Translate(); return false; }

            if (Find.World.Impassable(destinationTile))
            { failureReason = "CS_Ability_Fail_ImpassableDestination".Translate(); return false; }

            try
            {
                return LaunchInternal(caster, destinationTile, arrivalAction, component, sourceAbilityDefName, out failureReason);
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                Log.Error($"[CharacterStudio] TryLaunchWorldMapFlight failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// CompTick 检测到 handoffTick 到期时调用。
        /// DeSpawn Pawn，收集同行者，创建 TravellingTransporters 世界物件。
        /// </summary>
        public static void ExecuteHandoff(Pawn caster)
        {
            Map launchMap = caster.MapHeld;
            IntVec3 launchCell = caster.PositionHeld;
            CompCharacterAbilityRuntime? abilityComp = caster.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp == null) { ClearWorldMapFlightState(caster); return; }

            PlanetTile destinationTile = new PlanetTile(abilityComp.WorldMapFlightDestinationTile);
            bool mapParentRequired = abilityComp.WorldMapFlightMapParentRequired;

            // 播放飞走效果
            FleckMaker.ThrowDustPuff(launchCell, launchMap, 2f);

            // 收集同行者
            // 注意：handoffTick 检测在 CompTick 中，此时 component 引用不可用，
            // 只收集 caster 本人（同行者逻辑在 LaunchInternal 中已限制）
            List<Pawn> pawnsToTransport = new List<Pawn> { caster };

            // DeSpawn 并装入容器
            ActiveTransporterInfo transportInfo = new ActiveTransporterInfo();
            foreach (Pawn pawn in pawnsToTransport)
            {
                if (pawn.IsWorldPawn())
                    Find.WorldPawns.RemovePawn(pawn);
                else if (pawn.Spawned)
                    pawn.DeSpawn(DestroyMode.Vanish);
                transportInfo.innerContainer.TryAdd(pawn);
            }

            // 创建飞行中的世界物件（自定义 Def，显示为飞行角色而非运输仓）
            WorldObjectDef? flightDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("CS_PawnFlightTravelling");
            WorldObjectDef worldObjectDef = flightDef ?? WorldObjectDefOf.TravellingTransporters;
            int groupId = Find.UniqueIDsManager.GetNextTransporterGroupID();

            TravellingTransporters travellingTransporters = (TravellingTransporters)WorldObjectMaker.MakeWorldObject(worldObjectDef);
            travellingTransporters.SetFaction(Faction.OfPlayer);
            travellingTransporters.destinationTile = destinationTile;

      // 重新 ResolveArrivalAction（因为不序列化 arrivalAction）
            travellingTransporters.arrivalAction = ResolveArrivalAction(caster, destinationTile, mapParentRequired);

            PlanetTile startTile = launchMap.Tile;
            travellingTransporters.Tile = startTile;
            Find.WorldObjects.Add(travellingTransporters);
            travellingTransporters.AddTransporter(transportInfo, true);

            // 清理 FlightState：Pawn 已 DeSpawn，不会再收到 CompTick，
            // 因此 TickFlightState 的正常清理路径不会执行。
            // 必须在此处完整清理，包括 EquipmentAnimation、VanillaFlight 等。
            CompAbilityEffect_Modular.CleanupFlightState(caster, abilityComp);

            // 切换到世界地图，让玩家能看到飞行中的世界物件
            CameraJumper.TryJump((WorldObject)travellingTransporters);

            CSLogger.Debug($"跨地图飞行 handoff 完成: pawn={caster.LabelShortCap}, from={startTile}, to={destinationTile}", "WorldMapFlight");
        }

        // ──────────── 内部实现 ────────────

        private static bool LaunchInternal(
            Pawn caster,
            PlanetTile destinationTile,
            TransportersArrivalAction? arrivalAction,
            AbilityRuntimeComponentConfig component,
            string sourceAbilityDefName,
            out string failureReason)
        {
            failureReason = string.Empty;
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            CompCharacterAbilityRuntime? abilityComp = caster.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp == null)
            { failureReason = "no CompCharacterAbilityRuntime"; return false; }

            int takeoffTicks = DefaultTakeoffTicks;

            // ── 1. 播放起飞视觉效果 ──
            PlayTakeoffEffects(caster.PositionHeld, caster.MapHeld, component);

            // ── 2. 设置 FlightState（现有飞行组件渲染浮空上升） ──
            // FlightStateExpireTick 比 handoffTick 多 30 tick，避免 handoff 前
            // GetFlightLiftFactor01 的降落过渡把高度拉回 0（pawn 视觉回落）。
            // ExecuteHandoff 会在 DeSpawn 后 CleanupFlightState 清理。
            abilityComp.FlightStateStartTick = nowTick;
            abilityComp.FlightStateExpireTick = nowTick + takeoffTicks + 30;
            abilityComp.FlightStateHeightFactor = 100f; // 飞出屏幕高度

            // ── 3. 记录跨地图飞行状态 + handoffTick ──
            abilityComp.IsInWorldMapFlight = true;
            abilityComp.WorldMapFlightSourceAbilityDefName = sourceAbilityDefName ?? string.Empty;
            abilityComp.WorldMapFlightDestinationTile = destinationTile;
            abilityComp.WorldMapFlightStartTick = nowTick;
            abilityComp.WorldMapFlightHandoffTick = nowTick + takeoffTicks;
            abilityComp.WorldMapFlightTravelDurationTicks = component.worldMapTravelDurationTicks;

            // 标记目标地块是否有 MapParent（决定着陆方式）
            MapParent? mapParent = Find.WorldObjects.MapParentAt(destinationTile);
            abilityComp.WorldMapFlightMapParentRequired = mapParent != null;

            // ── 4. 播放起飞音效 ──
            if (!string.IsNullOrWhiteSpace(component.worldMapTakeoffSoundDefName))
            {
                SoundDef? soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(component.worldMapTakeoffSoundDefName);
                soundDef?.PlayOneShot(new TargetInfo(caster.PositionHeld, caster.MapHeld));
            }

            // ── 5. 显示消息 ──
            string messageKey = !string.IsNullOrWhiteSpace(component.worldMapFlightMessageKey)
                ? component.worldMapFlightMessageKey
                : "CS_Ability_WorldMapFlight_Launched";
            Messages.Message(messageKey.Translate(caster.LabelShortCap), MessageTypeDefOf.PositiveEvent, false);

            CSLogger.Debug($"启动跨地图飞行（FlightState模式）: pawn={caster.LabelShortCap}, handoffTick={abilityComp.WorldMapFlightHandoffTick}", "WorldMapFlight");

            return true;
        }

        // ──────────── 辅助方法 ───────────

        /// <summary>
        /// handoff 时重新确定着陆方式。
        /// </summary>
        private static TransportersArrivalAction? ResolveArrivalAction(Pawn caster, PlanetTile tile, bool mapParentRequired)
        {
            // 始终使用 PawnFlightLand：创建等待着陆世界物件，由玩家点击 Gizmo 进入地图
            return new TransportersArrivalAction_PawnFlightLand(tile);
        }

        private static void PlayTakeoffEffects(IntVec3 cell, Map map, AbilityRuntimeComponentConfig component)
        {
            if (component.worldMapTakeoffDustEffect)
            {
                FleckMaker.ThrowDustPuff(cell, map, 2f);
                FleckMaker.ThrowDustPuff(cell + IntVec3.North, map, 1.5f);
                FleckMaker.ThrowDustPuff(cell + IntVec3.South, map, 1.5f);
            }

            if (!string.IsNullOrWhiteSpace(component.worldMapTakeoffEffecterDefName))
            {
                EffecterDef? effecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail(component.worldMapTakeoffEffecterDefName);
                if (effecterDef != null)
                {
                    Effecter effecter = effecterDef.Spawn();
                    TargetInfo targetInfo = new TargetInfo(cell, map);
                    effecter.Trigger(targetInfo, targetInfo);
                    effecter.Cleanup();
                }
            }
        }

        public static void ClearWorldMapFlightState(Pawn? pawn)
        {
            CompCharacterAbilityRuntime? abilityComp = pawn?.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp == null) return;

            abilityComp.IsInWorldMapFlight = false;
            abilityComp.WorldMapFlightSourceAbilityDefName = string.Empty;
            abilityComp.WorldMapFlightDestinationTile = -1;
            abilityComp.WorldMapFlightStartTick = -1;
            abilityComp.WorldMapFlightHandoffTick = -1;
        }
    }
}
