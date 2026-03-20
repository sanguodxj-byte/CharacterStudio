using HarmonyLib;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 屏蔽 CharacterStudio 的普通信息日志，仅保留 Warning / Error。
    /// </summary>
    public static class Patch_LogSilencer
    {
        private static readonly string[] SuppressedPrefixes =
        {
            "[CharacterStudio]",
            "[CS.HAR.Debug]"
        };

        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(Log), nameof(Log.Message), new[] { typeof(string) });
            var prefix = AccessTools.Method(typeof(Patch_LogSilencer), nameof(Message_Prefix));

            if (target == null || prefix == null)
            {
                Log.Warning("[CharacterStudio] Patch_LogSilencer: 未找到 Verse.Log.Message(string)，跳过普通日志屏蔽。");
                return;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        }

        private static bool Message_Prefix(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            foreach (var prefix in SuppressedPrefixes)
            {
                if (text.StartsWith(prefix, System.StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
    }
}