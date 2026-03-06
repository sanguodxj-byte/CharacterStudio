using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// Pawn 渲染节点解析器：将游戏渲染树映射为编辑器可用数据模型。
    /// 当前复用 VanillaImportUtility 的成熟链路，确保与现有导入行为一致。
    /// </summary>
    public static class PawnRenderNodeParser
    {
        /// <summary>
        /// 从 Pawn 解析图层与隐藏节点信息。
        /// </summary>
        public static ImportResult ParseFromPawn(Pawn pawn)
        {
            return VanillaImportUtility.ImportFromPawnWithPaths(pawn);
        }
    }
}
