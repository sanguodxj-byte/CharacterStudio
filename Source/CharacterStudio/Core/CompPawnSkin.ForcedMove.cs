using System;
using CharacterStudio.Abilities;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    public partial class CompPawnSkin
    {
        // ─── 强制位移（转发到 CompCharacterAbilityRuntime） ───

        public void BeginForcedMove(IntVec3 direction, int steps, int stepDurationTicks = 4)
        {
            AbilityComp?.BeginForcedMove(direction, steps, stepDurationTicks);
        }

        public Vector3 GetForcedMoveVisualOffset()
        {
            return AbilityComp?.GetForcedMoveVisualOffset() ?? Vector3.zero;
        }

        public bool IsForcedMoveBusy()
        {
            return AbilityComp?.IsForcedMoveBusy() ?? false;
        }
    }
}
