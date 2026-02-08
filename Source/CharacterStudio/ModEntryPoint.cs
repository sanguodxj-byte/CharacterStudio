using System.Reflection;
using HarmonyLib;
using CharacterStudio.Rendering;
using Verse;

namespace CharacterStudio
{
    /// <summary>
    /// 模组入口点
    /// 初始化 Harmony 补丁和核心系统
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ModEntryPoint
    {
        /// <summary>
        /// Harmony 实例 ID
        /// </summary>
        public const string HarmonyId = "CharacterStudio.Main";

        /// <summary>
        /// Harmony 实例
        /// </summary>
        public static Harmony? HarmonyInstance { get; private set; }

        /// <summary>
        /// 模组版本
        /// </summary>
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        static ModEntryPoint()
        {
            Log.Message($"[CharacterStudio] Character Studio v{Version} 正在初始化...");

            // 初始化 Harmony
            InitializeHarmony();

            // 应用补丁
            ApplyPatches();

            Log.Message($"[CharacterStudio] 初始化完成！");
        }

        /// <summary>
        /// 初始化 Harmony
        /// </summary>
        private static void InitializeHarmony()
        {
            try
            {
                HarmonyInstance = new Harmony(HarmonyId);
                Log.Message($"[CharacterStudio] Harmony 实例已创建: {HarmonyId}");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 初始化 Harmony 失败: {ex}");
            }
        }

        /// <summary>
        /// 应用所有补丁
        /// </summary>
        private static void ApplyPatches()
        {
            if (HarmonyInstance == null)
            {
                Log.Error("[CharacterStudio] Harmony 实例为空，无法应用补丁");
                return;
            }

            try
            {
                // 应用 PawnRenderTree 补丁
                Patch_PawnRenderTree.Apply(HarmonyInstance);

                // 应用 Pawn Gizmo 补丁（外观更换按钮）
                UI.Patch_PawnGizmos.Apply(HarmonyInstance);

                Log.Message("[CharacterStudio] 所有补丁已应用");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用补丁时出错: {ex}");
            }
        }
    }
}