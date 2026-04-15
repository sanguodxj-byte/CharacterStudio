using System;
using CharacterStudio.Abilities;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    public partial class CompPawnSkin
    {
        // ─── 飞行状态视觉查询（转发到 CompCharacterAbilityRuntime） ───

        public bool IsFlightStateActive()
        {
            return AbilityComp?.IsFlightStateActive() ?? false;
        }

        public float GetFlightLiftFactor01()
        {
            return AbilityComp?.GetFlightLiftFactor01() ?? 0f;
        }

        public float GetFlightHoverOffset()
        {
            return AbilityComp?.GetFlightHoverOffset() ?? 0f;
        }

        internal float FlightStateHeightFactor => AbilityComp?.FlightStateHeightFactor ?? 0f;
    }
}
