using System.IO;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio
{
    public sealed class CharacterStudioMod : Mod
    {
        public static CharacterStudioSettings Settings { get; private set; } = null!;
        public static ModContentPack? ModContent { get; private set; }

        public CharacterStudioMod(ModContentPack content) : base(content)
        {
            ModContent = content;
            Settings = GetSettings<CharacterStudioSettings>();
            EnsureTexturesDirectoryExists(content);
        }

        /// <summary>
        /// 确保 mod 目录下的 Textures 文件夹存在。
        /// RimWorld 启动时自动扫描每个 mod 的 Textures/ 目录并载入其中的纹理，
        /// 用户事先放入的 PNG/JPG 会通过原版 ContentFinder 管线加载，
        /// 无需运行时磁盘 I/O（RuntimeAssetLoader），渲染性能更优。
        /// </summary>
        private static void EnsureTexturesDirectoryExists(ModContentPack content)
        {
            try
            {
                string texturesDir = Path.Combine(content.RootDir, "Textures");
                if (!Directory.Exists(texturesDir))
                {
                    Directory.CreateDirectory(texturesDir);
                    Log.Message($"[CharacterStudio] 已创建 Textures 目录: {texturesDir}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[CharacterStudio] 创建 Textures 目录失败: {ex.Message}");
            }
        }

        public override string SettingsCategory()
            => "CS_Settings_Category".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("CS_Settings_FaceTrackThresholds".Translate());
            listing.GapLine();

            Rect presetRow = listing.GetRect(30f);
            float presetButtonWidth = (presetRow.width - 12f) / 3f;
            if (Widgets.ButtonText(new Rect(presetRow.x, presetRow.y, presetButtonWidth, presetRow.height), "CS_Settings_Preset_Balanced".Translate()))
            {
                Settings.ApplyBalancedPreset();
                WriteSettings();
            }
            if (Widgets.ButtonText(new Rect(presetRow.x + presetButtonWidth + 6f, presetRow.y, presetButtonWidth, presetRow.height), "CS_Settings_Preset_Performance".Translate()))
            {
                Settings.ApplyPerformancePreset();
                WriteSettings();
            }
            if (Widgets.ButtonText(new Rect(presetRow.x + (presetButtonWidth + 6f) * 2f, presetRow.y, presetButtonWidth, presetRow.height), "CS_Settings_Preset_UltraPerformance".Translate()))
            {
                Settings.ApplyUltraPerformancePreset();
                WriteSettings();
            }
            listing.Gap(6f);

            DrawSlider(listing, "CS_Settings_RootSizeNear".Translate(), ref Settings.portraitTrackRootSizeNear, 1f, 64f);
            DrawSlider(listing, "CS_Settings_BudgetNear".Translate(), ref Settings.portraitTrackBudgetNear, 0f, 999f);
            DrawSlider(listing, "CS_Settings_RootSizeMid1".Translate(), ref Settings.portraitTrackRootSizeMid1, 1f, 64f);
            DrawSlider(listing, "CS_Settings_BudgetMid1".Translate(), ref Settings.portraitTrackBudgetMid1, 0f, 64f);
            DrawSlider(listing, "CS_Settings_RootSizeMid2".Translate(), ref Settings.portraitTrackRootSizeMid2, 1f, 64f);
            DrawSlider(listing, "CS_Settings_BudgetMid2".Translate(), ref Settings.portraitTrackBudgetMid2, 0f, 64f);
            DrawSlider(listing, "CS_Settings_RootSizeFar".Translate(), ref Settings.portraitTrackRootSizeFar, 1f, 64f);
            DrawSlider(listing, "CS_Settings_BudgetFar".Translate(), ref Settings.portraitTrackBudgetFar, 0f, 64f);
            DrawSlider(listing, "CS_Settings_FallbackBudget".Translate(), ref Settings.portraitTrackBudgetFallback, 0f, 32f);
            DrawSlider(listing, "CS_Settings_FallbackPriorityThreshold".Translate(), ref Settings.portraitTrackFallbackPriorityThreshold, 0f, 32f);
            DrawSlider(listing, "CS_Settings_FallbackPriorityBudget".Translate(), ref Settings.portraitTrackFallbackPriorityBudget, 0f, 32f);

            listing.GapLine();
            listing.Label("CS_Settings_PriorityBonuses".Translate());
            DrawSlider(listing, "CS_Settings_DraftedBonus".Translate(), ref Settings.draftedPriorityBonus, 0f, 32f);
            DrawSlider(listing, "CS_Settings_ColonistBonus".Translate(), ref Settings.colonistPriorityBonus, 0f, 32f);
            DrawSlider(listing, "CS_Settings_UnstableBonus".Translate(), ref Settings.unstablePriorityBonus, 0f, 32f);

            if (listing.ButtonText("CS_Settings_ResetToDefaults".Translate()))
            {
                Settings.ResetToDefaults();
                WriteSettings();
            }

            listing.End();
            Settings.Write();
        }

        private static void DrawSlider(Listing_Standard listing, string label, ref float value, float min, float max)
        {
            listing.Label($"{label}: {value:F1}");
            value = Widgets.HorizontalSlider(listing.GetRect(22f), value, min, max, true);
            listing.Gap(4f);
        }
    }
}
