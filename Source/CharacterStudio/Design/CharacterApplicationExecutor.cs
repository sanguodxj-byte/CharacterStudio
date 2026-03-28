using CharacterStudio.Core;

namespace CharacterStudio.Design
{
    /// <summary>
    /// 第二阶段最小计划执行器。
    /// 暂时仍复用 PawnSkinRuntimeUtility 作为底层运行时入口，
    /// 但由应用计划统一调度，避免预览与正式应用直接散落调用。
    /// </summary>
    public static class CharacterApplicationExecutor
    {
        public static bool Execute(CharacterApplicationPlan? plan)
        {
            if (plan == null)
            {
                return false;
            }

            if (plan.targetPawn == null)
            {
                plan.isValid = false;
                plan.statusMessage = "CS_Studio_Msg_TargetPawnRequired";
                return false;
            }

            plan.runtimeSkin = PawnSkinRuntimeValidator.PrepareForRuntime(plan.runtimeSkin);
            plan.runtimeSkin.RemoveApparelHidingData();

            if (string.IsNullOrWhiteSpace(plan.runtimeSkin.defName))
            {
                plan.warnings ??= new System.Collections.Generic.List<string>();
                plan.warnings.Add("CS_Studio_Warn_RuntimeSkinDefNameMissing");
            }

            plan.isValid = true;
            plan.statusMessage = plan.isPreview
                ? "CS_Studio_Msg_PreviewApplied"
                : "CS_Studio_Msg_AppliedToPawn";

            if (!PawnSkinRuntimeUtility.ApplySkinToPawn(
                plan.targetPawn,
                plan.runtimeSkin,
                fromDefaultRaceBinding: false,
                previewMode: plan.isPreview,
                applicationSource: plan.source ?? string.Empty))
            {
                plan.isValid = false;
                plan.statusMessage = "CS_Studio_Err_ApplyNoSkinComp";
                return false;
            }

            return true;
        }
    }
}
