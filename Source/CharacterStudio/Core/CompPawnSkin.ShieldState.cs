using System;
using CharacterStudio.Abilities;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    public partial class CompPawnSkin
    {
        // ─── 护盾状态查询（转发到 CompCharacterAbilityRuntime） ───

        public bool IsAttachedShieldVisualActive()
        {
            return AbilityComp?.IsAttachedShieldVisualActive() ?? false;
        }

        public bool IsProjectileInterceptorShieldActive()
        {
            return AbilityComp?.IsProjectileInterceptorShieldActive() ?? false;
        }
    }
}
