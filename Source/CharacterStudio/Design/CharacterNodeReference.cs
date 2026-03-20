using System;

namespace CharacterStudio.Design
{
    /// <summary>
    /// 编辑期节点引用。
    /// 用于稳定描述一个节点，而不是仅依赖 anchorPath / anchorTag。
    /// 当前阶段先提供最小可落地字段，后续再补充更强的树签名与命中策略。
    /// </summary>
    public class CharacterNodeReference
    {
        public string exactNodePath = "";
        public string terminalTag = "";
        public string texPathHint = "";
        public string workerClassHint = "";
        public int siblingIndexHint = -1;
        public string sourceRaceDefName = "";
        public string sourceTreeSignature = "";

        public CharacterNodeReference Clone()
        {
            return new CharacterNodeReference
            {
                exactNodePath = exactNodePath ?? string.Empty,
                terminalTag = terminalTag ?? string.Empty,
                texPathHint = texPathHint ?? string.Empty,
                workerClassHint = workerClassHint ?? string.Empty,
                siblingIndexHint = siblingIndexHint,
                sourceRaceDefName = sourceRaceDefName ?? string.Empty,
                sourceTreeSignature = sourceTreeSignature ?? string.Empty
            };
        }

        public static CharacterNodeReference FromRuntimeAnchor(
            string? exactNodePath,
            string? terminalTag,
            string? texPathHint = null,
            string? workerClassHint = null,
            int siblingIndexHint = -1,
            string? sourceRaceDefName = null,
            string? sourceTreeSignature = null)
        {
            return new CharacterNodeReference
            {
                exactNodePath = exactNodePath ?? string.Empty,
                terminalTag = terminalTag ?? string.Empty,
                texPathHint = texPathHint ?? string.Empty,
                workerClassHint = workerClassHint ?? string.Empty,
                siblingIndexHint = siblingIndexHint,
                sourceRaceDefName = sourceRaceDefName ?? string.Empty,
                sourceTreeSignature = sourceTreeSignature ?? string.Empty
            };
        }

        public bool MatchesExactPath(string? nodePath)
        {
            return string.Equals(exactNodePath ?? string.Empty, nodePath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }
}
