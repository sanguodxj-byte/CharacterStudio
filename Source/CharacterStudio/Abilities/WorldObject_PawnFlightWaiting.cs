using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 飞行中的世界物件（继承 TravellingTransporters 以复用移动逻辑）。
    /// 仅覆盖标签和材质，使其在世界地图上显示为飞行角色而非运输仓。
    /// </summary>
    public class TravellingTransporters_PawnFlight : TravellingTransporters
    {
    }

    /// <summary>
    /// 跨地图飞行等待着陆的世界物件。
    /// 持有到达的 pawn，提供"进入地图"和"组建远征队" Gizmo。
    /// </summary>
    public class WorldObject_PawnFlightWaiting : WorldObject, IThingHolder
    {
        private ThingOwner innerContainer;

        public WorldObject_PawnFlightWaiting()
        {
            innerContainer = new ThingOwner<Pawn>(this, oneStackOnly: false);
        }

        public ThingOwner GetDirectlyHeldThings() => innerContainer;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            // No nested holders
        }

        public List<Pawn> HeldPawns
        {
            get
            {
                List<Pawn> result = new List<Pawn>();
                for (int i = 0; i < innerContainer.Count; i++)
                {
                    if (innerContainer[i] is Pawn pawn)
                        result.Add(pawn);
                }
                return result;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", new object[] { this });
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
                yield return g;

            // "进入地图" 按钮
            yield return new Command_Action
            {
                defaultLabel = "CS_Ability_WorldMapFlight_EnterMap".Translate(),
                defaultDesc = "CS_Ability_WorldMapFlight_EnterMapDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/FormCaravan", false),
                action = ExecuteEnterMap
            };

            // "组建远征队" 按钮（取消着陆）
            yield return new Command_Action
            {
                defaultLabel = "CS_Ability_WorldMapFlight_FormCaravan".Translate(),
                defaultDesc = "CS_Ability_WorldMapFlight_FormCaravanDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", false),
                action = ExecuteFormCaravan
            };
        }

        private void ExecuteEnterMap()
        {
            PlanetTile tile = Tile;
            MapParent? mapParent = Find.WorldObjects.MapParentAt(tile);

            if (mapParent == null)
            {
                Messages.Message("CS_Ability_WorldMapFlight_NoMapParent".Translate(), MessageTypeDefOf.RejectInput, false);
                ExecuteFormCaravan();
                return;
            }

            // 如果 MapParent 还没有地图，先生成
            Map targetMap = mapParent.Map;
            if (targetMap == null)
            {
                targetMap = GetOrGenerateMapUtility.GetOrGenerateMap(tile, new IntVec3(250, 1, 250), null);
                if (targetMap == null)
                {
                    Log.Error("[CharacterStudio] PawnFlightWaiting: failed to generate map at tile " + tile);
                    ExecuteFormCaravan();
                    return;
                }
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
            }

            // 切换当前地图并跳转相机
            Current.Game.CurrentMap = targetMap;
            CameraJumper.TryJump(targetMap.Center, targetMap);

            // 启动地图内着陆点选择
            StartLandingTargeter(targetMap);
        }

        private void StartLandingTargeter(Map targetMap)
        {
            TargetingParameters targetParams = new TargetingParameters()
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = (TargetInfo target) =>
                {
                    return target.Cell.InBounds(targetMap) && target.Cell.Walkable(targetMap);
                }
            };

            WorldObject_PawnFlightWaiting capturedSelf = this;
            Map capturedMap = targetMap;

            Find.Targeter.BeginTargeting(
                targetParams,
                action: (LocalTargetInfo target) =>
                {
                    capturedSelf.ExecuteLanding(target.Cell, capturedMap);
                },
                highlightAction: null,
                targetValidator: (LocalTargetInfo target) =>
                {
                    if (!target.Cell.InBounds(capturedMap) || !target.Cell.Walkable(capturedMap))
                    {
                        Messages.Message("CS_Ability_Fail_LandingSpotNotWalkable".Translate(), MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    return true;
                },
                caster: null,
                actionWhenFinished: null,
                mouseAttachment: CompLaunchable.TargeterMouseAttachment
            );
        }

        private void ExecuteLanding(IntVec3 cell, Map map)
        {
            List<Pawn> pawns = new List<Pawn>(HeldPawns);
            if (pawns.Count == 0) return;

            Thing? lookTarget = null;

            for (int i = 0; i < pawns.Count; i++)
            {
                IntVec3 dropCell = cell;
                if (i > 0)
                    DropCellFinder.TryFindDropSpotNear(cell, map, out dropCell, false, true);

                Pawn pawn = pawns[i];
                innerContainer.Remove(pawn);
                PawnFlightArrivalUtility.SpawnPawnWithLanding(pawn, dropCell, map);

                if (lookTarget == null)
                    lookTarget = pawn;
            }

            Messages.Message("CS_Ability_WorldMapFlight_Landed".Translate(),
                lookTarget != null ? (LookTargets)lookTarget : (LookTargets)map.Parent,
                MessageTypeDefOf.TaskCompletion);

            CameraJumper.TryJump(cell, map);

            // 移除等待物件
            Find.WorldObjects.Remove(this);
        }

        private void ExecuteFormCaravan()
        {
            List<Pawn> pawns = HeldPawns;

            // 将 pawn 从容器中取出放入世界
            for (int i = innerContainer.Count - 1; i >= 0; i--)
            {
                Pawn? pawn = innerContainer[i] as Pawn;
                if (pawn != null)
                {
                    innerContainer.Remove(pawn);
                    if (!pawn.IsWorldPawn())
                        Find.WorldPawns.PassToWorld(pawn);
                }
            }

            // 创建远征队
            if (pawns.Count > 0)
            {
                Caravan caravan = CaravanMaker.MakeCaravan(pawns, Faction.OfPlayer, Tile, false);
                Messages.Message("CS_Ability_WorldMapFlight_Arrived".Translate(),
                    (LookTargets)caravan, MessageTypeDefOf.TaskCompletion);
            }

            Find.WorldObjects.Remove(this);
        }
    }
}
