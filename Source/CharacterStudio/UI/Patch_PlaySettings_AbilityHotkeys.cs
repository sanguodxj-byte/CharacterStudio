using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 在原版右下角 PlaySettings 复选功能栏中插入“技能热键”开关。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_PlaySettings_AbilityHotkeys
    {
        private static readonly Texture2D ToggleIcon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", false)
            ?? ContentFinder<Texture2D>.Get("UI/Designators/Strip", true);

        public static void Apply(Harmony harmony)
        {
            var originalMethod = AccessTools.Method(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls));
            var postfixMethod = AccessTools.Method(typeof(Patch_PlaySettings_AbilityHotkeys), nameof(DoPlaySettingsGlobalControls_Postfix));
            if (originalMethod != null && postfixMethod != null)
            {
                harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                Log.Message("[CharacterStudio] PlaySettings 技能热键开关补丁已应用");
            }
        }

        public static void Unpatch(Harmony harmony)
        {
            var originalMethod = AccessTools.Method(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls));
            if (originalMethod != null)
            {
                harmony.Unpatch(originalMethod, HarmonyPatchType.Postfix, harmony.Id);
            }
        }

        private static void DoPlaySettingsGlobalControls_Postfix(WidgetRow row, bool worldView)
        {
            if (worldView || row == null)
            {
                return;
            }

            bool enabled = Abilities.AbilityHotkeyRuntimeComponent.GlobalHotkeysEnabled;
            row.ToggleableIcon(
                ref enabled,
                ToggleIcon,
                "CS_Ability_Hotkey_ToggleDesc".Translate(),
                SoundDefOf.Mouseover_ButtonToggle,
                null);

            if (enabled != Abilities.AbilityHotkeyRuntimeComponent.GlobalHotkeysEnabled)
            {
                Abilities.AbilityHotkeyRuntimeComponent.GlobalHotkeysEnabled = enabled;
            }
        }
    }
}
