using System;
using CharacterStudio.Performance;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    public partial class CompPawnSkin
    {
        /// <summary>
        /// 当前 Pawn 的双轨运行时状态。
        /// 第一阶段先作为状态同步与后续渲染接入的统一入口。
        /// </summary>
        public FaceRuntimeState CurrentFaceRuntimeState => faceRuntimeState ??= new FaceRuntimeState();

        /// <summary>
        /// 当前 Pawn 的面部编译缓存。
        /// 由 Runtime Compiler 按皮肤内容签名构建并缓存。
        /// </summary>
        public FaceRuntimeCompiledData CurrentFaceRuntimeCompiledData
            => faceRuntimeCompiledData ??= FaceRuntimeCompiler.GetOrBuild(activeSkin);

        private void MarkFaceRuntimeDirty()
        {
            faceRuntimeCompiledData = null;

            if (faceRuntimeState == null)
                faceRuntimeState = new FaceRuntimeState();
            else
                faceRuntimeState.MarkAllDirty();
        }

        private EffectiveFaceStateSnapshot BuildEffectiveFaceStateSnapshot()
            => EffectiveFaceStateEvaluator.BuildSnapshot(this);

        private void EnsureFaceRuntimeStateUpdated()
        {
            if (!FaceRuntimeActivationGuard.CanProcessFaceRuntime(this, Pawn))
                return;

            var runtimeState = CurrentFaceRuntimeState;
            var compiledData = CurrentFaceRuntimeCompiledData;
            int currentTick = Find.TickManager?.TicksGame ?? 0;

            bool shouldRefresh = FaceRuntimeSyncCoordinator.UpdateTrackAndLodIfNeeded(
                Pawn!,
                this,
                runtimeState,
                compiledData,
                currentTick);

            EffectiveFaceStateSnapshot effectiveState = BuildEffectiveFaceStateSnapshot();
            FaceRuntimeSyncCoordinator.SyncEffectiveState(runtimeState, effectiveState);
            MarkFaceTransformDirty();

            if (shouldRefresh)
                MarkFaceGraphicDirty();
        }
    }
}
