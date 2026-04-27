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
            UIHelper.DrawDialogFrame(inRect, this);

            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Rect titleRect = UIHelper.DrawPanelShell(shellRect, "CS_Studio_Performance_Title".Translate(), 0f);

            Rect controlsRect = new Rect(inRect.x, titleRect.yMax + 6f, inRect.width, 32f);
            DrawControls(controlsRect);

            Rect scrollOutRect = new Rect(inRect.x, controlsRect.yMax + 8f, inRect.width, inRect.height - 92f);
            Rect viewRect = new Rect(0f, 0f, scrollOutRect.width - 16f, 900f);

            UIHelper.DrawContentCard(scrollOutRect);
            Widgets.BeginScrollView(scrollOutRect.ContractedBy(2f), ref scrollPosition, viewRect);
            DrawStats(viewRect);
            Widgets.EndScrollView();
        }

        private static void DrawControls(Rect rect)
        {
            float leftWidth = rect.width * 0.52f;
            Rect toggleRect = new Rect(rect.x, rect.y, leftWidth, rect.height);

            Widgets.DrawBoxSolid(toggleRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(toggleRect, 1);
            GUI.color = Color.white;

            bool captureEnabled = CharacterStudioPerformanceStats.CaptureEnabled;
            Widgets.CheckboxLabeled(toggleRect, "CS_Studio_Performance_EnableCapture".Translate(), ref captureEnabled);
            CharacterStudioPerformanceStats.CaptureEnabled = captureEnabled;

            Rect resetRect = new Rect(rect.x + rect.width - 120f, rect.y, 120f, rect.height);
            if (UIHelper.DrawToolbarButton(resetRect, "CS_Studio_Performance_Reset".Translate()))
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
                ? "CS_Studio_Performance_Status_On".Translate()
                : "CS_Studio_Performance_Status_Off".Translate());
            listing.GapLine();

            listing.Label("CS_Studio_Performance_Bootstrap".Translate());
            listing.Label("CS_Studio_Performance_LoadedGameCalls".Translate(snapshot.bootstrapLoadedGameCalls));
            listing.Label("CS_Studio_Performance_StartedNewGameCalls".Translate(snapshot.bootstrapStartedNewGameCalls));
            listing.Label("CS_Studio_Performance_FinalizeInitCalls".Translate(snapshot.bootstrapFinalizeInitCalls));
            listing.Label("CS_Studio_Performance_PassesExecuted".Translate(snapshot.bootstrapPassesExecuted));
            listing.Label("CS_Studio_Performance_PassesSkipped".Translate(snapshot.bootstrapPassesSkipped));
            listing.Label("CS_Studio_Performance_MapsVisited".Translate(snapshot.bootstrapMapsVisited));
            listing.Label("CS_Studio_Performance_PawnsVisited".Translate(snapshot.bootstrapPawnsVisited));
            listing.Label("CS_Studio_Performance_CompsAdded".Translate(snapshot.bootstrapCompsAdded));
            listing.Label("CS_Studio_Performance_DefaultSkinsApplied".Translate(snapshot.bootstrapDefaultSkinsApplied));
            listing.Label("CS_Studio_Performance_LoadoutsGranted".Translate(snapshot.bootstrapLoadoutsGranted));
            listing.Label("CS_Studio_Performance_LastEntryPoint".Translate(snapshot.lastBootstrapEntryPoint));
            listing.Label("CS_Studio_Performance_LastSignature".Translate(snapshot.lastBootstrapSignature));
            listing.Gap(12f);
            listing.GapLine();

            listing.Label("CS_Studio_Performance_RenderRefresh".Translate());
            listing.Label("CS_Studio_Performance_RefreshRequests".Translate(snapshot.renderRefreshRequests));
            listing.Label("CS_Studio_Performance_RefreshDispatched".Translate(snapshot.renderRefreshDispatched));
            listing.Label("CS_Studio_Performance_RefreshCoalesced".Translate(snapshot.renderRefreshCoalesced));
            listing.Label("CS_Studio_Performance_RefreshCoalescedRate".Translate(snapshot.RenderRefreshCoalescedRate.ToString("P1")));
            listing.Label(snapshot.lastRenderRefreshTick >= 0
                ? "CS_Studio_Performance_LastRefreshTick".Translate(snapshot.lastRenderRefreshTick)
                : "CS_Studio_Performance_LastRefreshTickNA".Translate());
            listing.Label("CS_Studio_Performance_GraphicDirtyTriggers".Translate(snapshot.graphicDirtyTriggers));
            listing.Label("CS_Studio_Performance_GraphicDirtySkipped".Translate(snapshot.graphicDirtySkippedByThrottle));
            listing.Gap(12f);
            listing.GapLine();

            listing.Label("CS_Studio_Performance_FaceCaches".Translate());
            listing.Label("CS_Studio_Performance_FaceTransformCacheHits".Translate(snapshot.faceTransformCacheHits));
            listing.Label("CS_Studio_Performance_FaceTransformCacheMisses".Translate(snapshot.faceTransformCacheMisses));
            listing.Label("CS_Studio_Performance_FacePathCacheHits".Translate(snapshot.facePathCacheHits));
            listing.Label("CS_Studio_Performance_FacePathCacheMisses".Translate(snapshot.facePathCacheMisses));
            listing.Gap(12f);
            listing.GapLine();

            listing.Label("CS_Studio_Performance_Notes".Translate());
            listing.Label("CS_Studio_Performance_Notes_Bootstrap".Translate());
            listing.Label("CS_Studio_Performance_Notes_Render".Translate());
            listing.Label("CS_Studio_Performance_Notes_Window".Translate());

            listing.End();
        }
    }
}
