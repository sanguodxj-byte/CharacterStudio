using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 在主界面 GUI 事件阶段优先消费保留热键，避免文本框与其他 UI 抢占。
    /// </summary>
    public static class Patch_UIRoot_AbilityHotkeys
    {
        public static void Apply(Harmony harmony)
        {
            var originalMethod = AccessTools.Method(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI));
            var prefixMethod = AccessTools.Method(typeof(Patch_UIRoot_AbilityHotkeys), nameof(UIRootOnGUI_Prefix));
            if (originalMethod != null && prefixMethod != null)
            {
                harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
                Log.Message("[CharacterStudio] UIRoot 技能热键拦截补丁已应用");
            }
        }

        public static void Unpatch(Harmony harmony)
        {
            var originalMethod = AccessTools.Method(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI));
            if (originalMethod != null)
            {
                harmony.Unpatch(originalMethod, HarmonyPatchType.Prefix, harmony.Id);
            }
        }

        private static void UIRootOnGUI_Prefix()
        {
            if (!Abilities.AbilityHotkeyRuntimeComponent.GlobalHotkeysEnabled)
            {
                return;
            }

            Abilities.AbilityHotkeyRuntimeComponent.ConsumeReservedHotkeyKeys(Event.current);
        }
    }

}
