using System.Reflection;
using HarmonyLib;
using CharacterStudio.Core;
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
            // 初始化 Harmony
            InitializeHarmony();

            // 先屏蔽普通信息日志，避免初始化过程刷屏
            ApplyLogSilencer();

            // 加载运行时皮肤定义
            PawnSkinDefRegistry.LoadFromConfig();
            CharacterSpawnProfileRegistry.LoadFromConfig();
            CharacterRuntimeTriggerRegistry.LoadFromConfig();

            // 将 DefDatabase 中由 XML Defs 加载的 PawnSkinDef（如导出模组提供的）同步到运行时注册表，
            // 使 GetDefaultSkinForRace 等运行时查询能找到这些 Def，
            // 从而让 PawnSkinBootstrapComponent 在加载存档时自动应用导出模组的皮肤到匹配种族的 Pawn。
            PawnSkinDefRegistry.SyncFromDefDatabase();

            // 预热运行时技能 Def，确保旧存档中的 CS_RT_* AbilityDef 引用在读档期即可被解析
            Abilities.AbilityGrantUtility.WarmupAllRuntimeAbilityDefs();

            // 预热字符串规范化缓存，避免渲染管线首次调用时分配
            Core.PawnFaceConfig.PrewarmOverlayIdCache();
            Core.PawnFaceConfig.PrewarmSemanticKeyCache();

            // 应用补丁
            ApplyPatches();
        }

        /// <summary>
        /// 初始化 Harmony
        /// </summary>
        private static void InitializeHarmony()
        {
            try
            {
                HarmonyInstance = new Harmony(HarmonyId);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 初始化 Harmony 失败: {ex}");
            }
        }

        private static void ApplyLogSilencer()
        {
            if (HarmonyInstance == null)
                return;

            try
            {
                Patch_LogSilencer.Apply(HarmonyInstance);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用日志屏蔽补丁时出错: {ex}");
            }
        }

        /// <summary>
        /// 单独应用某个补丁，避免单个补丁失败阻断后续补丁注册。
        /// </summary>
        private static void ApplyPatch(string patchName, System.Action applyAction)
        {
            try
            {
                applyAction();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用补丁失败 [{patchName}]: {ex}");
            }
        }

        /// <summary>
        /// 应用所有补丁
        /// </summary>
        private static void ApplyPatches()
        {
            var harmony = HarmonyInstance;
            if (harmony == null)
            {
                Log.Error("[CharacterStudio] Harmony 实例为空，无法应用补丁");
                return;
            }

            ApplyPatch("PawnRenderTree", () => Patch_PawnRenderTree.Apply(harmony));
            ApplyPatch("PawnRenderer", () => Rendering.Patch_PawnRenderer.Apply(harmony));
            ApplyPatch("FlightState", () => Patches.Patch_FlightState.Apply(harmony));
            ApplyPatch("GameComponentBootstrap", () => Patches.Patch_GameComponentBootstrap.Apply(harmony));
            ApplyPatch("PawnGizmos", () => UI.Patch_PawnGizmos.Apply(harmony));
            ApplyPatch("AbilityGizmos", () => UI.Patch_AbilityGizmos.Apply(harmony));
            ApplyPatch("PlaySettingsAbilityHotkeys", () => UI.Patch_PlaySettings_AbilityHotkeys.Apply(harmony));
            ApplyPatch("UIRootAbilityHotkeys", () => UI.Patch_UIRoot_AbilityHotkeys.Apply(harmony));
            ApplyPatch("AnimationRender", () => Rendering.Patch_AnimationRender.Apply(harmony));
            ApplyPatch("RaceLabel", () => Patches.Patch_RaceLabel.Apply(harmony));
            ApplyPatch("CharacterAttributeBuffStat", () => Patches.Patch_CharacterAttributeBuffStat.Apply(harmony));
            ApplyPatch("AbilityTimeStop", () => Abilities.Patch_AbilityTimeStop.Apply(harmony));
            ApplyPatch("AbilityRuntimeTick", () => Abilities.Patch_AbilityRuntimeTick.Apply(harmony));

            Log.Message("[CharacterStudio] 补丁应用流程完成");
        }
    }
}
