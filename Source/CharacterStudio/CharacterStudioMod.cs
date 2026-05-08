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

            // ── 预设按钮 ──
            listing.Label("CS_Settings_Presets".Translate());
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

            // ── 更新频率 ──
            listing.GapLine();
            listing.Label("CS_Settings_UpdateFrequency".Translate());
            listing.GapLine();
            DrawIntSlider(listing, "CS_Settings_StateEvalInterval".Translate(), ref Settings.stateEvaluationInterval, 500, 10000);
            DrawIntSlider(listing, "CS_Settings_HighFocusAnimInterval".Translate(), ref Settings.highFocusAnimationInterval, 1, 30);

            // ── 视距阈值 ──
            listing.GapLine();
            listing.Label("CS_Settings_VisibleRange".Translate());
            listing.GapLine();
            DrawSlider(listing, "CS_Settings_VisibleRangeStandardLod".Translate(), ref Settings.visibleRangeStandardLod, 0f, 50f);

            if (listing.ButtonText("CS_Settings_ResetToDefaults".Translate()))
            {
                Settings.ResetToDefaults();
                WriteSettings();
            }

            listing.Gap(12f);
            listing.GapLine();

            // ── 调试工具 ──
            listing.Label("CS_Settings_DebugTools".Translate());
            listing.GapLine();

            bool showOverlay = Settings.showPerformanceOverlay;
            Widgets.CheckboxLabeled(listing.GetRect(30f), "CS_Settings_ShowPerformanceOverlay".Translate(), ref showOverlay);
            if (showOverlay != Settings.showPerformanceOverlay)
            {
                Settings.showPerformanceOverlay = showOverlay;
                Settings.Write();
            }

            listing.End();
            Settings.Write();
        }

        private static void DrawIntSlider(Listing_Standard listing, string label, ref int value, int min, int max)
        {
            listing.Label($"{label}: {value}");
            float fVal = value;
            fVal = Widgets.HorizontalSlider(listing.GetRect(22f), fVal, min, max, true);
            value = Mathf.RoundToInt(fVal);
            listing.Gap(4f);
        }

        private static void DrawSlider(Listing_Standard listing, string label, ref float value, float min, float max)
        {
            listing.Label($"{label}: {value:F1}");
            value = Widgets.HorizontalSlider(listing.GetRect(22f), value, min, max, true);
            listing.Gap(4f);
        }
    }
}
