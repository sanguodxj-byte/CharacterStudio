using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 角色垂直上升的离场飞行动画。
    /// 继承 FlyShipLeaving 以复用 LeaveMap → TravellingTransporters 管线。
    /// 完全 Override Tick/DrawAt 实现垂直上升，不使用穿梭机的 reversed 系统。
    /// </summary>
    public class FlyPawnLeaving : FlyShipLeaving
    {
        private int ticksAlive;
        private Pawn? cachedPawn;

        private const int TakeoffDuration = 90;
        private const float MaxHeight = 18f;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            ticksAlive = 0;
            // Spawn 时缓存 Pawn 引用，之后容器不会被改动
            cachedPawn = PawnFlightArrivalUtility.FindFirstPawnInContainer(Contents?.innerContainer);
        }

        protected override void Tick()
        {
            ticksAlive++;

            if (ticksAlive % 6 == 0 && Map != null)
                FleckMaker.ThrowDustPuff(Position.ToVector3Shifted() + Gen.RandomHorizontalVector(0.3f), Map, 0.6f);

            if (ticksAlive >= TakeoffDuration)
                LeaveMap();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Pawn? pawn = cachedPawn;
            if (pawn != null)
            {
                float progress = Mathf.Clamp01((float)ticksAlive / TakeoffDuration);
                float height = MaxHeight * (1f - Mathf.Pow(1f - progress, 2f));
                Vector3 pos = drawLoc;
                pos.y += height;
                pawn.DrawNowAt(pos, flip);
            }
            else
                base.DrawAt(drawLoc, flip);
        }

        protected override void DrawDropSpotShadow()
        {
            Material shadowMaterial = ShadowMaterial;
            if (shadowMaterial == null) return;
            Skyfaller.DrawDropSpotShadow(
                DrawPos with { y = AltitudeLayer.Shadows.AltitudeFor(), z = Position.ToVector3Shifted().z },
                Rotation, shadowMaterial, def.skyfaller.shadowSize, ticksToImpact);
        }
    }

    /// <summary>
    /// 角色从天而降的进场 Skyfaller。
    /// 到达时直接释放 Pawn 到地图上，不走 ActiveDropPod 的延迟打开。
    /// </summary>
    public class Skyfaller_PawnArriving : Skyfaller
    {
        private bool hasJumpedCamera;

        private Pawn? RenderPawn => PawnFlightArrivalUtility.FindFirstPawnInActiveTransporter(innerContainer);

        protected override void Tick()
        {
            base.Tick();
            if (!hasJumpedCamera && ticksToImpact <= 30 && ticksToImpact > 0 && Map != null)
            {
                hasJumpedCamera = true;
                CameraJumper.TryJump(Position, Map);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Pawn? pawn = RenderPawn;
            if (pawn != null)
            {
                GetDrawPositionAndRotation(ref drawLoc, out float _);
                pawn.DrawNowAt(drawLoc, flip);
                DrawDropSpotShadow();
            }
            else
                base.DrawAt(drawLoc, flip);
        }

        protected override void Impact()
        {
            for (int i = 0; i < 6; i++)
                FleckMaker.ThrowDustPuff(Position.ToVector3Shifted() + Gen.RandomHorizontalVector(1f), Map, 1.2f);
            FleckMaker.ThrowLightningGlow(Position.ToVector3Shifted(), Map, 2f);
            if (Map == Find.CurrentMap)
                Find.CameraDriver.shaker.DoShake(0.5f);
            GenClamor.DoClamor(this, 15f, ClamorDefOf.Impact);
            base.Impact();
        }

        protected override void SpawnThings()
        {
            if (innerContainer == null || innerContainer.Count == 0) return;
            if (innerContainer[0] is ActiveTransporter at && at.Contents?.innerContainer != null)
            {
                ThingOwner contents = at.Contents.innerContainer;
                for (int i = contents.Count - 1; i >= 0; i--)
                {
                    Thing thing = contents[i];
                    contents.Remove(thing);
                    GenSpawn.Spawn(thing, Position, Map);
                    if (thing is Pawn pawn)
                        pawn.Rotation = Rot4.South;
                }
            }
            else
                base.SpawnThings();
        }
    }
}
