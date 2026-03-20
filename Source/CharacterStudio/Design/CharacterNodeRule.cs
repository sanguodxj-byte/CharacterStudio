using CharacterStudio.Core;

namespace CharacterStudio.Design
{
    /// <summary>
    /// 编辑期节点规则。
    /// 当前阶段先提供 Hide / Attach 两类最小语义，
    /// 用于替代部分散落在 hiddenPaths / layer.anchorPath 上的隐式表达。
    /// </summary>
    public class CharacterNodeRule
    {
        public CharacterNodeOperationType operationType = CharacterNodeOperationType.Hide;
        public CharacterNodeReference targetNode = new CharacterNodeReference();
        public PawnLayerConfig attachedLayer = new PawnLayerConfig();
        public bool strictMatch = false;
        public string notes = "";

        public CharacterNodeRule Clone()
        {
            return new CharacterNodeRule
            {
                operationType = operationType,
                targetNode = targetNode?.Clone() ?? new CharacterNodeReference(),
                attachedLayer = attachedLayer?.Clone() ?? new PawnLayerConfig(),
                strictMatch = strictMatch,
                notes = notes ?? string.Empty
            };
        }

        public static CharacterNodeRule CreateHide(CharacterNodeReference targetNode, bool strictMatch = false, string? notes = null)
        {
            return new CharacterNodeRule
            {
                operationType = CharacterNodeOperationType.Hide,
                targetNode = targetNode?.Clone() ?? new CharacterNodeReference(),
                attachedLayer = new PawnLayerConfig(),
                strictMatch = strictMatch,
                notes = notes ?? string.Empty
            };
        }

        public static CharacterNodeRule CreateAttach(CharacterNodeReference targetNode, PawnLayerConfig attachedLayer, bool strictMatch = false, string? notes = null)
        {
            return new CharacterNodeRule
            {
                operationType = CharacterNodeOperationType.Attach,
                targetNode = targetNode?.Clone() ?? new CharacterNodeReference(),
                attachedLayer = attachedLayer?.Clone() ?? new PawnLayerConfig(),
                strictMatch = strictMatch,
                notes = notes ?? string.Empty
            };
        }
    }

    public enum CharacterNodeOperationType
    {
        Hide,
        Attach,
        Replace,
        Override
    }
}
