using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 面部运行时 LOD 决策策略。
    /// 简化设计：直接根据角色状态判定 LOD 级别，
    /// Track 由 LOD 派生（HighFocus/Standard → Portrait, Reduced/Dormant → World）。
    /// </summary>
    public static class FaceRuntimePolicy
    {
        /// <summary>
        /// 更新 Pawn 的运行时状态（LOD / Track / 更新时机 / dirty flag）。
        /// </summary>
        public static void UpdateRuntimeState(
            Pawn pawn,
            CompPawnSkin comp,
            FaceRuntimeState runtimeState,
            FaceRuntimeCompiledData compiledData,
            int currentTick)
        {
            if (pawn == null || comp == null || runtimeState == null)
                return;

            FaceRenderLod nextLod = EvaluateLod(pawn);
            if (runtimeState.currentLod != nextLod)
            {
                runtimeState.currentLod = nextLod;
                runtimeState.lodDirty = true;
            }

            // Track 从 LOD 派生：高精度用 Portrait，低精度用 World
            FaceRenderTrack nextTrack = DeriveTrack(nextLod);
            if (runtimeState.currentTrack != nextTrack)
            {
                runtimeState.currentTrack = nextTrack;
                runtimeState.trackDirty = true;
            }

            runtimeState.nextWorldUpdateTick = currentTick + GetUpdateIntervalTicks(compiledData, nextLod);
        }

        /// <summary>
        /// 评估当前 Pawn 的面部 LOD。
        /// 规则：
        /// - 未 Spawned / 预览人偶 → HighFocus
        /// - 当前被玩家单选 → HighFocus
        /// - 征召中 → Standard
        /// - 不在可见区域 → Dormant
        /// - 其他 → Reduced
        /// </summary>
        public static FaceRenderLod EvaluateLod(Pawn pawn)
        {
            if (pawn == null)
                return FaceRenderLod.Reduced;

            // 编辑器预览人偶
            if (!pawn.Spawned || pawn.MapHeld == null)
                return FaceRenderLod.HighFocus;

            // 当前选中角色
            if (Find.Selector?.SingleSelectedThing is Pawn selectedPawn && selectedPawn == pawn)
                return FaceRenderLod.HighFocus;

            // 低缩放时（近景），视口内所有角色无条件进入 HighFocus
            float rootSize = Find.CameraDriver?.RootSize ?? 999f;
            if (rootSize <= HighFocusRootSizeThreshold && IsInVisibleRect(pawn))
                return FaceRenderLod.HighFocus;

            // 征召中
            if (pawn.Drafted)
                return FaceRenderLod.Standard;

            // 不可见
            if (!IsInVisibleRect(pawn))
                return FaceRenderLod.Dormant;

            // 视口内近处 → Standard（有状态评估，无动画）
            if (IsNearViewportCenter(pawn))
                return FaceRenderLod.Standard;

            return FaceRenderLod.Reduced;
        }

        /// <summary>低缩放阈值：RootSize ≤ 此值时视口内所有角色进入 HighFocus。</summary>
        public const float HighFocusRootSizeThreshold = 18f;

        /// <summary>
        /// 角色是否在视口中心附近的近距范围内。
        /// 用屏幕中心到角色的格子距离与阈值比较。
        /// </summary>
        public static bool IsNearViewportCenter(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
                return false;

            CellRect visibleRect = Find.CameraDriver?.CurrentViewRect ?? CellRect.Empty;
            if (!visibleRect.Contains(pawn.Position))
                return false;

            float threshold = CharacterStudioMod.Settings?.visibleRangeStandardLod ?? 15f;
            if (threshold <= 0f)
                return false;

            float dist = (pawn.Position - visibleRect.CenterCell).LengthHorizontal;
            return dist <= threshold;
        }

        /// <summary>从 LOD 派生 Track：高精度用 Portrait，低精度用 World。</summary>
        public static FaceRenderTrack DeriveTrack(FaceRenderLod lod)
            => lod is FaceRenderLod.HighFocus or FaceRenderLod.Standard
                ? FaceRenderTrack.Portrait
                : FaceRenderTrack.World;

        public static bool IsInVisibleRect(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
                return false;

            if (pawn.Position.Fogged(pawn.Map))
                return false;

            CellRect visibleRect = Find.CameraDriver?.CurrentViewRect ?? CellRect.Empty;
            return visibleRect.Contains(pawn.Position);
        }

        // ── 更新间隔 ──

        /// <summary>状态评估间隔（统一，不再区分 Portrait/World）。</summary>
        public static int GetUpdateIntervalTicks(FaceRuntimeCompiledData compiledData, FaceRenderLod lod)
        {
            // 优先读取编译结果中的 LOD 配置
            if (compiledData?.worldTrack?.lodUpdateIntervals != null
                && compiledData.worldTrack.lodUpdateIntervals.TryGetValue(lod, out int configured)
                && configured > 0)
            {
                return configured;
            }

            return IsFrozen(lod) ? 999999 : GetStateEvaluationInterval();
        }

        // ── LOD 行为查询 ──

        /// <summary>状态评估间隔（表情解析/LOD 判定）。所有可更新 LOD 共用。</summary>
        public static int GetStateEvaluationInterval()
            => CharacterStudioMod.Settings?.stateEvaluationInterval ?? 3000;

        /// <summary>HighFocus 动画帧推进间隔（眨眼/帧动画/程序动画）。</summary>
        public static int GetHighFocusAnimationInterval()
            => CharacterStudioMod.Settings?.highFocusAnimationInterval ?? 5;

        /// <summary>该 LOD 是否需要运行状态评估。</summary>
        public static bool ShouldEvaluateState(FaceRenderLod lod)
            => lod is FaceRenderLod.HighFocus or FaceRenderLod.Standard;

        /// <summary>该 LOD 是否完全冻结。</summary>
        public static bool IsFrozen(FaceRenderLod lod)
            => lod is FaceRenderLod.Reduced or FaceRenderLod.Dormant;
    }
}
