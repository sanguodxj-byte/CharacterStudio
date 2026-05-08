using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 角色跨地图飞行到达时，以远征队形式着陆。
    /// 用于目标地块没有 MapParent（空旷地块）的情况。
    /// </summary>
    public class TransportersArrivalAction_PawnFlightFormCaravan : TransportersArrivalAction_FormCaravan
    {
        public TransportersArrivalAction_PawnFlightFormCaravan() : base() { }
        public TransportersArrivalAction_PawnFlightFormCaravan(string arrivalMessageKey) : base(arrivalMessageKey) { }

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            PawnFlightArrivalUtility.ClearFlightStateForPawns(transporters);
            base.Arrived(transporters, tile);
        }
    }

    /// <summary>
    /// 角色跨地图飞行到达后，创建等待着陆的世界物件。
    /// 玩家点击 Gizmo 生成/进入地图 → 选择落点 → 角色从天而降。
    /// </summary>
    public class TransportersArrivalAction_PawnFlightLand : TransportersArrivalAction
    {
        private PlanetTile destinationTile;

        public TransportersArrivalAction_PawnFlightLand() { }

        public TransportersArrivalAction_PawnFlightLand(PlanetTile tile)
        {
            destinationTile = tile;
        }

        public override bool GeneratesMap => false;

        public override bool ShouldUseLongEvent(List<ActiveTransporterInfo> pods, PlanetTile tile)
        {
            return false; // 不需要长加载，我们只是创建等待物件
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref destinationTile, "destinationTile");
        }

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            PawnFlightArrivalUtility.ClearFlightStateForPawns(transporters);
            PawnFlightWaitingUtility.CreateWaitingObject(transporters, tile);
        }

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile)
        {
            return true;
        }
    }

    /// <summary>
    /// 角色跨地图飞行着陆的共享工具类。
    /// </summary>
    public static class PawnFlightArrivalUtility
    {
        public const string PawnArrivingDefName = "CS_PawnArrivingWorldMap";

        public static void ClearFlightStateForPawns(List<ActiveTransporterInfo> transporters)
        {
            if (transporters == null) return;
            for (int i = 0; i < transporters.Count; i++)
            {
                ThingOwner innerContainer = transporters[i].innerContainer;
                for (int j = 0; j < innerContainer.Count; j++)
                {
                    if (innerContainer[j] is Pawn pawn)
                        AbilityWorldMapFlightUtility.ClearWorldMapFlightState(pawn);
                }
            }
        }

        /// <summary>
        /// 在地图上 spawn pawn 并启动反向 FlightState 降落动画。
        /// </summary>
        public static void SpawnPawnWithLanding(Pawn pawn, IntVec3 cell, Map map)
        {
            if (pawn.IsWorldPawn())
                Find.WorldPawns.RemovePawn(pawn);
            GenSpawn.Spawn(pawn, cell, map);
            pawn.Rotation = Rot4.South;

            // 启动反向 FlightState：从高空降落到地面
            CompCharacterAbilityRuntime? abilityComp = pawn.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp != null)
            {
                int nowTick = Find.TickManager?.TicksGame ?? 0;
                abilityComp.IsWorldMapLanding = true;
                abilityComp.WorldMapLandingStartTick = nowTick;
                abilityComp.WorldMapLandingDurationTicks = 150;
                abilityComp.WorldMapLandingHeightFactor = 100f;
            }
        }

        internal static void DoFallbackCaravan(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            TransportersArrivalActionUtility.RemovePawnsFromWorldPawns(transporters);
            try
            {
                TransportersArrivalAction_FormCaravan fallback = new TransportersArrivalAction_FormCaravan("CS_Ability_WorldMapFlight_Arrived");
                fallback.Arrived(transporters, tile);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] DoFallbackCaravan failed: {ex}");
                for (int i = 0; i < transporters.Count; i++)
                    transporters[i].innerContainer.ClearAndDestroyContentsOrPassToWorld(DestroyMode.Vanish);
            }
        }

        public static Pawn? FindFirstPawnInContainer(ThingOwner? container)
        {
            if (container == null || container.Count == 0) return null;
            for (int i = 0; i < container.Count; i++)
                if (container[i] is Pawn pawn) return pawn;
            return null;
        }

        public static Pawn? FindFirstPawnInActiveTransporter(ThingOwner container)
        {
            if (container == null || container.Count == 0) return null;
            if (container[0] is ActiveTransporter at)
                return FindFirstPawnInContainer(at.Contents?.innerContainer);
            return null;
        }
    }

    /// <summary>
    /// 等待着陆的世界物件管理工具。
    /// </summary>
    public static class PawnFlightWaitingUtility
    {
        /// <summary>
        /// 创建等待着陆的世界物件，替代直接着陆。
        /// </summary>
        public static void CreateWaitingObject(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            WorldObjectDef? waitingDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("CS_PawnFlightWaiting");
            if (waitingDef == null)
            {
                Log.Warning("[CharacterStudio] CS_PawnFlightWaiting WorldObjectDef not found, falling back to caravan");
                PawnFlightArrivalUtility.DoFallbackCaravan(transporters, tile);
                return;
            }

            WorldObject_PawnFlightWaiting waitingObj = (WorldObject_PawnFlightWaiting)WorldObjectMaker.MakeWorldObject(waitingDef);
            waitingObj.SetFaction(Faction.OfPlayer);
            waitingObj.Tile = tile;
            Find.WorldObjects.Add(waitingObj);

            // 将 transporters 中的 pawn 转移到等待物件
            for (int i = 0; i < transporters.Count; i++)
            {
                ThingOwner innerContainer = transporters[i].innerContainer;
                for (int j = innerContainer.Count - 1; j >= 0; j--)
                {
                    Thing thing = innerContainer[j];
                    innerContainer.Remove(thing);
                    waitingObj.GetDirectlyHeldThings().TryAdd(thing);
                }
            }

            Messages.Message("CS_Ability_WorldMapFlight_ArrivedWaiting".Translate(),
                (LookTargets)waitingObj, MessageTypeDefOf.TaskCompletion);
        }
    }
}
