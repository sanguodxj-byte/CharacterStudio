using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    public class ProjectileInterceptorThing : ThingWithComps
    {
        public Pawn? trackedPawn;

        public override Vector3 DrawPos
        {
            get
            {
                if (trackedPawn != null && trackedPawn.Spawned && trackedPawn.Drawer != null)
                {
                    return trackedPawn.Drawer.DrawPos;
                }
                return base.DrawPos;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref trackedPawn, "trackedPawn");
        }
    }
}
