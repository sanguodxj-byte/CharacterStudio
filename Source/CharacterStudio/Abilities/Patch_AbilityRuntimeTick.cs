using HarmonyLib;
using RimWorld;

namespace CharacterStudio.Abilities
{
    public static class Patch_AbilityRuntimeTick
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(Ability), nameof(Ability.AbilityTick));
            var postfix = AccessTools.Method(typeof(Patch_AbilityRuntimeTick), nameof(AbilityTick_Postfix));
            if (target != null && postfix != null)
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
        }

        public static void AbilityTick_Postfix(Ability __instance)
        {
            if (__instance == null)
            {
                return;
            }

            var modularComp = __instance.CompOfType<CompAbilityEffect_Modular>();
            modularComp?.CompTick();
        }
    }
}
