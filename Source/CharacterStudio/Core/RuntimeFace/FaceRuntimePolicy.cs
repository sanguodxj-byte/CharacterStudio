using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 双轨面部系统的运行时决策策略。
    /// 当前版本：
    /// 1. 编辑器预览人偶始终走肖像轨；
    /// 2. 地图内 Pawn 优先按玩家当前可见范围判断是否进入肖像轨；
    /// 3. 仍保留较保守的 LOD 与更新间隔策略。
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
        /// 当前规则：
        /// - 未 Spawned / 预览人偶：Portrait
        /// - 当前被玩家单选：Portrait
        /// - 位于玩家当前视口可见范围，且通过缩放 + 距离预算筛选：Portrait
        /// - 其他：World
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

            if (CanUsePortraitTrackByVisibilityBudget(pawn))
                return FaceRenderTrack.Portrait;

            return FaceRenderTrack.World;
        }

        private static bool CanUsePortraitTrackByVisibilityBudget(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
                return false;

            if (pawn.Position.Fogged(pawn.Map))
                return false;

            CameraDriver? cameraDriver = Find.CameraDriver;
            CellRect visibleRect = cameraDriver?.CurrentViewRect ?? CellRect.Empty;
            if (!visibleRect.Contains(pawn.Position))
                return false;

            float rootSize = cameraDriver?.RootSize ?? 0f;
            float maxDistance = GetPortraitTrackDistanceBudget(rootSize, pawn);
            if (maxDistance <= 0f)
                return false;

            IntVec3 centerCell = visibleRect.CenterCell;
            IntVec3 delta = pawn.Position - centerCell;
            float distanceToCenter = delta.LengthHorizontal;
            return distanceToCenter <= maxDistance;
        }

        public static bool IsInVisibleRect(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
                return false;

            if (pawn.Position.Fogged(pawn.Map))
                return false;

            CameraDriver? cameraDriver = Find.CameraDriver;
            CellRect visibleRect = cameraDriver?.CurrentViewRect ?? CellRect.Empty;
            return visibleRect.Contains(pawn.Position);
        }

        private static float GetPortraitTrackDistanceBudget(float rootSize, Pawn pawn)
        {
            // RootSize 越大镜头越远，允许进入 Portrait 轨的距离预算越小；
            // 高优先级角色可获得更高预算，提升玩家关注对象的细节表现。
            float baseBudget;
            if (rootSize <= 18f)
                baseBudget = 999f;
            else if (rootSize <= 24f)
                baseBudget = 18f;
            else if (rootSize <= 32f)
                baseBudget = 10f;
            else if (rootSize <= 40f)
                baseBudget = 6f;
            else
                baseBudget = 0f;

            float priorityBonus = GetPortraitTrackPriorityBudgetBonus(pawn);
            if (baseBudget <= 0f)
                return priorityBonus >= 10f ? 4f : 0f;

            return baseBudget + priorityBonus;
        }

        private static float GetPortraitTrackPriorityBudgetBonus(Pawn pawn)
        {
            if (pawn == null)
                return 0f;

            float bonus = 0f;

            if (pawn.Drafted)
                bonus += 8f;

            if (pawn.IsColonistPlayerControlled)
                bonus += 6f;

            if (pawn.InMentalState || pawn.Downed)
                bonus += 5f;

            return bonus;
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

            if (!IsInVisibleRect(pawn))
                return FaceRenderLod.Dormant;

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
                case FaceRenderLod.Dormant:
                    return 999999;
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
                case FaceRenderLod.Dormant:
                    return 999999;
                default:
                    return 20;
            }
        }
    }
}