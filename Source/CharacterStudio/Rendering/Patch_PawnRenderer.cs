using HarmonyLib;
using Verse;
using UnityEngine;
using CharacterStudio.Abilities;

namespace CharacterStudio.Rendering
{
    public static class Patch_PawnRenderer
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.PropertyGetter(typeof(Pawn_DrawTracker), "DrawPos");
            var postfix = AccessTools.Method(typeof(Patch_PawnRenderer), nameof(DrawPos_Postfix));

            if (target != null && postfix != null)
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
        }

        public static void DrawPos_Postfix(Pawn_DrawTracker __instance, Pawn ___pawn, ref Vector3 __result)
        {
            if (___pawn == null || !___pawn.Spawned) return;

            var abilityComp = ___pawn.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp == null) return;

            // 飞行高度偏移
            if (abilityComp.IsFlightStateActive())
            {
                float height = abilityComp.TotalFlightHeight;
                if (height > 0.001f)
                {
                    __result.z += height;
                    if (___pawn.Position.Roofed(___pawn.Map))
                    {
                        var roofDef = ___pawn.Map.roofGrid.RoofAt(___pawn.Position);
                        if (roofDef != null && roofDef.isThickRoof)
                        {
                            __result.y = AltitudeLayer.Skyfaller.AltitudeFor();
                        }
                    }
                }
            }
        }
    }
}
