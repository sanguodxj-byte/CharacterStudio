using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.Design
{
    /// <summary>
    /// 第二阶段最小计划构建器。
    /// 当前只统一编译结果与目标 Pawn 绑定，
    /// 为预览/应用/保存后的自动应用提供共同入口。
    /// </summary>
    public static class CharacterApplicationPlanBuilder
    {
        public static CharacterApplicationPlan Build(
            CharacterDesignDocument? document,
            Pawn? targetPawn,
            bool isPreview,
            string source)
        {
            return new CharacterApplicationPlan
            {
                targetPawn = targetPawn,
                runtimeSkin = CharacterDesignCompiler.CompileRuntimeSkin(document),
                isPreview = isPreview,
                source = source ?? string.Empty
            };
        }
    }
}
