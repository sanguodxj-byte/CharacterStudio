using System;
using UnityEngine;
using Verse;
using CharacterStudio.Performance;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 性能统计窗口。
    /// 继承 Window 以获得正确的拖动、滚动、按钮点击支持。
    /// 通过 Mod 设置中的开关控制显示。
    /// </summary>
    public class Dialog_PerformanceStats : Window
    {
        // 配色
        private static readonly Color HeaderBg = UIHelper.PanelFillSoftColor;
        private static readonly Color SectionCol = UIHelper.HeaderColor;
        private static readonly Color AccentSoftCol = UIHelper.AccentSoftColor;
        private static readonly Color LabelCol = UIHelper.SubtleColor;
        private static readonly Color ValueCol = new Color(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color GoodCol = new Color(0.4f, 0.9f, 0.5f, 1f);
        private static readonly Color WarnCol = new Color(1f, 0.75f, 0.3f, 1f);

        private const float LineHeight = 20f;
        private const float ContentMargin = 6f;
        private Vector2 scrollPosition;
        private float contentHeight = 800f;

        public override Vector2 InitialSize => new Vector2(440f, 520f);

        public Dialog_PerformanceStats()
        {
            draggable = true;
            preventCameraMotion = false;
            closeOnAccept = false;
            closeOnCancel = false;
            focusWhenOpened = false;
            layer = WindowLayer.GameUI;
            doCloseX = false;
        }

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect(16f, 16f, InitialSize.x, InitialSize.y);
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 检查开关
            if (CharacterStudioMod.Settings?.showPerformanceOverlay != true)
            {
                Close();
                return;
            }

            float w = inRect.width;
            float y = 0f;

            // ── 控制按钮栏 ──
            float btnH = 22f;
            float btnY = y + 2f;
            float rightEdge = w;

            float resetW = 48f;
            rightEdge -= resetW;
            if (Widgets.ButtonText(new Rect(rightEdge, btnY, resetW, btnH),
                "CS_Studio_Performance_Reset".Translate()))
            {
                CharacterStudioPerformanceStats.ResetCapturedStats();
                contentHeight = 800f;
            }

            float copyW = 52f;
            rightEdge -= copyW + 4f;
            if (Widgets.ButtonText(new Rect(rightEdge, btnY, copyW, btnH),
                "CS_Studio_Performance_Copy".Translate()))
            {
                try
                {
                    string report = CharacterStudioPerformanceStats.CreateSnapshot().ToReportString();
                    GUIUtility.systemCopyBuffer = report;
                    Log.Message($"[CharacterStudio] 性能报告已复制到剪贴板 ({report.Length} 字符)");
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] 复制性能报告失败: {ex.Message}");
                }
            }

            float toggleW = 60f;
            rightEdge -= toggleW + 4f;
            bool captureEnabled = CharacterStudioPerformanceStats.CaptureEnabled;
            Widgets.CheckboxLabeled(new Rect(rightEdge, btnY, toggleW, btnH),
                "CS_Studio_Performance_EnableCapture_Short".Translate(), ref captureEnabled);
            CharacterStudioPerformanceStats.CaptureEnabled = captureEnabled;

            y = btnY + btnH + 4f;

            // ── 滚动内容 ──
            Rect scrollRect = new Rect(0f, y, w, inRect.height - y);
            Rect viewRect = new Rect(0f, 0f, w - 16f, contentHeight);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
            float actualHeight = DrawStatsContent(viewRect.width);
            Widgets.EndScrollView();

            if (actualHeight > 0f)
                contentHeight = actualHeight;
        }

        private float DrawStatsContent(float width)
        {
            var s = CharacterStudioPerformanceStats.CreateSnapshot();
            Text.Font = GameFont.Tiny;

            float y = 0f;
            float innerW = width - 8f;
            float x = 4f;

            DrawSection(ref y, innerW, x, "CS_Studio_Perf_Sec_TextureMemory");
            float mb = Rendering.TextureMemoryMonitor.CurrentEstimatedUsage / (1024f * 1024f);
            float budgetMb = Rendering.TextureMemoryMonitor.MemoryBudgetBytes / (1024f * 1024f);
            float pressure = Rendering.TextureMemoryMonitor.MemoryPressure;
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_Usage", $"{mb:F1} / {budgetMb:F0} MB", pressure < 0.7f ? GoodCol : WarnCol);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_Pressure", $"{pressure:P0}", pressure < 0.85f ? GoodCol : WarnCol);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_Tracked", $"{s.refTrackerTrackedTextures}");
            y += 4f;

            DrawSection(ref y, innerW, x, "CS_Studio_Perf_Sec_TextureLoading");
            int texTotal = s.textureLoadsFromDisk + s.textureCacheHits;
            float texHitRate = texTotal > 0 ? (float)s.textureCacheHits / texTotal : 0f;
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_CacheHitRate", $"{texHitRate:P0}", texHitRate > 0.9f ? GoodCol : WarnCol);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_LoadsHits", $"{s.textureLoadsFromDisk} / {s.textureCacheHits}");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_LruEvictions", $"{s.textureLruEvictions}");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_DestroyedSkipped", $"{s.textureSafeDestroyed} / {s.textureSafeDestroySkipped}");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_PressureReleases", $"{s.texturePressureReleases}");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_QueuePeak", $"{s.pendingTextureQueuePeak}");
            y += 4f;

            DrawSection(ref y, innerW, x, "CS_Studio_Perf_Sec_WorkerCaches");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_CustomLayer", s.customLayerCacheStats);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_FaceComp", s.faceComponentCacheStats);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_EyeDir", s.eyeDirectionCacheStats);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_AssetLoader", s.runtimeAssetLoaderCacheStats);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_PoolAvailable", $"{s.graphicRuntimePoolAvailable}");
            y += 4f;

            DrawSection(ref y, innerW, x, "CS_Studio_Perf_Sec_RenderRefresh");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_Requests", $"{s.renderRefreshRequests}");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_DispatchedCoalesced", $"{s.renderRefreshDispatched} / {s.renderRefreshCoalesced}");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_CoalesceRate", $"{s.RenderRefreshCoalescedRate:P0}",
                s.RenderRefreshCoalescedRate > 0.5f ? GoodCol : ValueCol);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_DirtyThrottled", $"{s.graphicDirtyTriggers} / {s.graphicDirtySkippedByThrottle}");
            y += 4f;

            DrawSection(ref y, innerW, x, "CS_Studio_Perf_Sec_FaceCaches");
            int ftTotal = s.faceTransformCacheHits + s.faceTransformCacheMisses;
            float ftRate = ftTotal > 0 ? (float)s.faceTransformCacheHits / ftTotal : 0f;
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_TransformHit", $"{ftRate:P0}", ftRate > 0.8f ? GoodCol : WarnCol);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_TransformHM", $"{s.faceTransformCacheHits} / {s.faceTransformCacheMisses}");
            int fpTotal = s.facePathCacheHits + s.facePathCacheMisses;
            float fpRate = fpTotal > 0 ? (float)s.facePathCacheHits / fpTotal : 0f;
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_PathHit", $"{fpRate:P0}", fpRate > 0.8f ? GoodCol : WarnCol);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_PathHM", $"{s.facePathCacheHits} / {s.facePathCacheMisses}");
            y += 4f;

            DrawSection(ref y, innerW, x, "CS_Studio_Perf_Sec_Camera");
            float rootSize = Find.CameraDriver?.RootSize ?? 0f;
            float highFocusThreshold = Core.FaceRuntimePolicy.HighFocusRootSizeThreshold;
            bool isHighFocusZoom = rootSize <= highFocusThreshold && rootSize > 0;
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_CameraZoom", $"{rootSize:F1}", isHighFocusZoom ? GoodCol : ValueCol);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_HighFocusThreshold", $"{highFocusThreshold:F0}", isHighFocusZoom ? GoodCol : ValueCol);
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_HighFocusZoom", isHighFocusZoom ? "✓" : "✗", isHighFocusZoom ? GoodCol : WarnCol);

            DrawSection(ref y, innerW, x, "CS_Studio_Perf_Sec_Bootstrap");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_ExecSkipped", $"{s.bootstrapPassesExecuted} / {s.bootstrapPassesSkipped}");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_PawnsComps", $"{s.bootstrapPawnsVisited} / {s.bootstrapCompsAdded}");
            DrawKV(ref y, innerW, x, "CS_Studio_Perf_SkinsLoadouts", $"{s.bootstrapDefaultSkinsApplied} / {s.bootstrapLoadoutsGranted}");

            return y;
        }

        private void DrawSection(ref float y, float width, float x, string titleKey)
        {
            Widgets.DrawLine(new Vector2(x, y + 2f), new Vector2(x + width, y + 2f), AccentSoftCol, 1f);
            y += 6f;

            Rect secRect = new Rect(x, y, width, 20f);
            Widgets.DrawBoxSolid(secRect, new Color(UIHelper.AccentColor.r, UIHelper.AccentColor.g, UIHelper.AccentColor.b, 0.08f));

            GUI.color = SectionCol;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x + 4f, y, width - 8f, 20f), $"▸ {titleKey.Translate()}");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            y += 20f;
        }

        private void DrawKV(ref float y, float width, float x, string key, string value, Color? valueColor = null)
        {
            float halfW = (width - 8f) * 0.48f;

            GUI.color = LabelCol;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x + 4f, y, halfW, LineHeight), key.Translate());

            GUI.color = valueColor ?? ValueCol;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(x + 4f + halfW, y, (width - 8f) - halfW, LineHeight), value);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            y += LineHeight;
        }

        // ── 窗口管理（替代旧的静态 DrawOverlay） ──

        /// <summary>
        /// 确保窗口在需要时打开。从 Postfix 中调用。
        /// </summary>
        public static void EnsureOpen()
        {
            if (CharacterStudioMod.Settings?.showPerformanceOverlay == true
                && !Find.WindowStack.IsOpen<Dialog_PerformanceStats>())
            {
                Find.WindowStack.Add(new Dialog_PerformanceStats());
            }
        }
    }
}
