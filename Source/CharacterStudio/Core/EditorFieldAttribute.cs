using System;
using CharacterStudio.Abilities;

namespace CharacterStudio.Core
{
    [AttributeUsage(AttributeTargets.Field)]
    public class EditorFieldAttribute : Attribute
    {
        public string LabelKey { get; }
        public float Min { get; set; } = float.MinValue;
        public float Max { get; set; } = float.MaxValue;
        public string TooltipKey { get; set; } = string.Empty;
        public AbilityRuntimeComponentType[] ValidTypes { get; }

        public EditorFieldAttribute(string labelKey, params AbilityRuntimeComponentType[] validTypes)
        {
            LabelKey = labelKey;
            ValidTypes = validTypes;
        }
    }
}
