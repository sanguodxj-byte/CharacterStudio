using System;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 标注在 public 字段上，控制 <see cref="AbilityXmlSerialization.SerializePublicFields"/> 的序列化行为。
    /// 未标注的字段走默认逻辑（技能路径不变）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class XmlExportFieldAttribute : Attribute
    {
        /// <summary>
        /// 字段值等于此值时跳过输出。
        /// <para>对 string：与空字符串比较（null 和 "" 都跳过）。</para>
        /// <para>对 float：配合 <see cref="SkipDefaultFloat"/> 使用 epsilon 比较。</para>
        /// <para>对 int/bool/enum：直接值比较。</para>
        /// <para>null（默认）= 不做值比较跳过，仅依赖类型默认行为。</para>
        /// </summary>
        public object? SkipDefault = null;

        /// <summary>
        /// float 字段与 <see cref="SkipDefault"/> 比较时使用 epsilon=0.0001f 容差。
        /// 默认 false（精确比较）。
        /// </summary>
        public bool SkipDefaultFloat = false;

        /// <summary>
        /// string.IsNullOrWhiteSpace 时跳过。默认 true。
        /// </summary>
        public bool SkipEmptyString = true;

        /// <summary>
        /// List/Array 元素数为 0 时跳过。默认 true。
        /// </summary>
        public bool SkipEmptyCollection = true;

        /// <summary>
        /// float/数值格式化字符串。null = 使用默认格式（"G"）。
        /// </summary>
        public string? Format = null;

        /// <summary>
        /// bool 值输出为小写 "true"/"false"。默认 true。
        /// </summary>
        public bool BoolToLower = true;

        /// <summary>
        /// 完全跳过此字段，不参与序列化。
        /// </summary>
        public bool Ignore = false;

        /// <summary>
        /// 自定义 XML 元素名。null = 使用字段名。
        /// </summary>
        public string? ElementName = null;
    }
}
