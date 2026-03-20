using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 双轨面部系统的第一阶段运行时决策策略。
    /// 当前版本刻意保持保守：
    /// 1. 不依赖复杂相机/屏幕可见性判断；
    /// 2. 优先用稳定且易维护的游戏状态决定 Track / LOD；
    /// 3. 为后续引入视口、性能预算、重要目标优先级预留统一入口。
    /// </summary>
    public static class FaceRuntimePolicy
    {
        /// <summary>
        /// 更新 Pawn 的双轨运行时状态。
        /// 这里只负责轨道 / LOD / 更新时机与 dirty flag，
        /// 不直接介入具体渲染 worker。
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

            FaceRenderTrack nextTrack = EvaluateTrack(pawn);
            if (runtimeState.currentTrack != nextTrack)
            {
                runtimeState.currentTrack = nextTrack;
                runtimeState.trackDirty = true;
            }

            FaceRenderLod nextLod = EvaluateLod(pawn, nextTrack);
            if (runtimeState.currentLod != nextLod)
            {
                runtimeState.currentLod = nextLod;
                runtimeState.lodDirty = true;
            }

            if (nextTrack == FaceRenderTrack.Portrait)
            {
                runtimeState.nextPortraitUpdateTick = currentTick + GetPortraitUpdateIntervalTicks(nextLod);
            }
            else
            {
                runtimeState.nextWorldUpdateTick = currentTick + GetWorldUpdateIntervalTicks(compiledData, nextLod);
            }
        }

        /// <summary>
        /// 是否使用肖像轨。
        /// 第一阶段规则：
        /// - 当前被玩家单选中的 Pawn 进入肖像轨；
        /// - 其他一律走世界轨。
        /// </summary>
        public static FaceRenderTrack EvaluateTrack(Pawn pawn)
        {
            if (pawn == null)
                return FaceRenderTrack.World;

            // 编辑器预览人偶通常不会出现在地图中，也不会进入游戏选择器。
            // 若仍按 World Track 处理，会导致 LayeredDynamic 只保留 Base 节点，
            // 进而让表情/嘴型/眼睑/眉毛等预览覆盖看起来全部失效。
            if (!pawn.Spawned || pawn.MapHeld == null)
                return FaceRenderTrack.Portrait;

            Pawn? selectedPawn = Find.Selector?.SingleSelectedThing as Pawn;
            if (selectedPawn == pawn)
                return FaceRenderTrack.Portrait;

            return FaceRenderTrack.World;
        }

        /// <summary>
        /// 评估当前 Pawn 的面部 LOD。
        /// 第一阶段使用保守规则：
        /// - 肖像轨对象始终 HighFocus；
        /// - 玩家草稿兵、玩家控制人形、或当前激活心灵/战斗状态的单位 -> Standard；
        /// - 其他对象 -> Reduced。
        /// </summary>
        public static FaceRenderLod EvaluateLod(Pawn pawn, FaceRenderTrack track)
        {
            if (pawn == null)
                return FaceRenderLod.Reduced;

            if (track == FaceRenderTrack.Portrait)
                return FaceRenderLod.HighFocus;

            if (pawn.Drafted)
                return FaceRenderLod.Standard;

            if (pawn.IsColonistPlayerControlled)
                return FaceRenderLod.Standard;

            if (pawn.InMentalState || pawn.Downed)
                return FaceRenderLod.Standard;

            return FaceRenderLod.Reduced;
        }

        /// <summary>
        /// 世界轨更新间隔。
        /// 优先读取编译结果中的 LOD 配置；缺失时使用保底值。
        /// </summary>
        public static int GetWorldUpdateIntervalTicks(FaceRuntimeCompiledData compiledData, FaceRenderLod lod)
        {
            if (compiledData?.worldTrack?.lodUpdateIntervals != null
                && compiledData.worldTrack.lodUpdateIntervals.TryGetValue(lod, out int configured)
                && configured > 0)
            {
                return configured;
            }

            switch (lod)
            {
                case FaceRenderLod.HighFocus:
                    return 10;
                case FaceRenderLod.Standard:
                    return 20;
                default:
                    return 45;
            }
        }

        /// <summary>
        /// 肖像轨更新间隔。
        /// 当前先使用固定策略，后续可改为配置化。
        /// </summary>
        public static int GetPortraitUpdateIntervalTicks(FaceRenderLod lod)
        {
            switch (lod)
            {
                case FaceRenderLod.HighFocus:
                    return 5;
                case FaceRenderLod.Standard:
                    return 10;
                default:
                    return 20;
            }
        }
    }
}