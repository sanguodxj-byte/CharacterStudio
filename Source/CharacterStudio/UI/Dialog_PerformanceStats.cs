using UnityEngine;
using Verse;
using CharacterStudio.Performance;

namespace CharacterStudio.UI
{
    /// <summary>
    /// CharacterStudio 性能统计窗口。
    /// 用于单独查看与配置 bootstrap / 渲染刷新两类统计信息。
    /// </summary>
    public class Dialog_PerformanceStats : Window
    {
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(620f, 520f);

        public Dialog_PerformanceStats()
        {
            doCloseButton = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "Character Studio 性能统计");
            Text.Font = GameFont.Small;

            Rect controlsRect = new Rect(inRect.x, titleRect.yMax + 6f, inRect.width, 32f);
            DrawControls(controlsRect);

            Rect scrollOutRect = new Rect(inRect.x, controlsRect.yMax + 8f, inRect.width, inRect.height - 92f);
            Rect viewRect = new Rect(0f, 0f, scrollOutRect.width - 16f, 560f);

            Widgets.BeginScrollView(scrollOutRect, ref scrollPosition, viewRect);
            DrawStats(viewRect);
            Widgets.EndScrollView();
        }

        private static void DrawControls(Rect rect)
        {
            float leftWidth = rect.width * 0.52f;
            Rect toggleRect = new Rect(rect.x, rect.y, leftWidth, rect.height);

            bool captureEnabled = CharacterStudioPerformanceStats.CaptureEnabled;
            Widgets.CheckboxLabeled(toggleRect, "启用统计采集", ref captureEnabled);
            CharacterStudioPerformanceStats.CaptureEnabled = captureEnabled;

            Rect resetRect = new Rect(rect.x + rect.width - 120f, rect.y, 120f, rect.height);
            if (Widgets.ButtonText(resetRect, "重置统计"))
            {
                CharacterStudioPerformanceStats.ResetCapturedStats();
            }
        }

        private static void DrawStats(Rect rect)
        {
            CharacterStudioPerformanceSnapshot snapshot = CharacterStudioPerformanceStats.CreateSnapshot();

            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label(snapshot.captureEnabled
                ? "统计状态：已启用"
                : "统计状态：已关闭（优化逻辑仍生效）");
            listing.GapLine();

            listing.Label("Bootstrap 统计");
            listing.Label($"- LoadedGame 调用次数：{snapshot.bootstrapLoadedGameCalls}");
            listing.Label($"- StartedNewGame 调用次数：{snapshot.bootstrapStartedNewGameCalls}");
            listing.Label($"- FinalizeInit 调用次数：{snapshot.bootstrapFinalizeInitCalls}");
            listing.Label($"- 实际执行 pass 次数：{snapshot.bootstrapPassesExecuted}");
            listing.Label($"- 因世界签名未变化而跳过次数：{snapshot.bootstrapPassesSkipped}");
            listing.Label($"- 累计扫描地图数：{snapshot.bootstrapMapsVisited}");
            listing.Label($"- 累计扫描 Pawn 数：{snapshot.bootstrapPawnsVisited}");
            listing.Label($"- 新增 CompPawnSkin 数：{snapshot.bootstrapCompsAdded}");
            listing.Label($"- 应用默认皮肤次数：{snapshot.bootstrapDefaultSkinsApplied}");
            listing.Label($"- 补发有效 Loadout 次数：{snapshot.bootstrapLoadoutsGranted}");
            listing.Label($"- 最近一次 bootstrap 入口：{snapshot.lastBootstrapEntryPoint}");
            listing.Label($"- 最近一次世界签名：{snapshot.lastBootstrapSignature}");
            listing.Gap(12f);
            listing.GapLine();

            listing.Label("渲染刷新统计");
            listing.Label($"- 刷新请求总数：{snapshot.renderRefreshRequests}");
            listing.Label($"- 实际下发刷新数：{snapshot.renderRefreshDispatched}");
            listing.Label($"- 同 tick 合并次数：{snapshot.renderRefreshCoalesced}");
            listing.Label($"- 合并比例：{snapshot.RenderRefreshCoalescedRate:P1}");
            listing.Label(snapshot.lastRenderRefreshTick >= 0
                ? $"- 最近一次实际刷新 Tick：{snapshot.lastRenderRefreshTick}"
                : "- 最近一次实际刷新 Tick：N/A");
            listing.Gap(12f);
            listing.GapLine();

            listing.Label("说明");
            listing.Label("- Bootstrap 优化将原本的多次全图扫描合并为单次扫描，并对相同世界状态做跳过。");
            listing.Label("- 渲染刷新优化会将同一 Pawn 在同一 Tick 内的重复 RequestRenderRefresh 合并为一次实际 dirty。");
            listing.Label("- 该窗口只负责统计与开关，不改变渲染语义与 Comp 生命周期。");

            listing.End();
        }
    }
}