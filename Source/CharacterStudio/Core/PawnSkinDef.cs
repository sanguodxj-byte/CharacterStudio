using System.Collections.Generic;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 皮肤定义 (Def)
    /// 定义一个完整的角色皮肤，包含多个图层和配置选项
    /// </summary>
    public class PawnSkinDef : Def
    {
        // ─────────────────────────────────────────────
        // 基础配置
        // ─────────────────────────────────────────────

        /// <summary>是否隐藏原版头部</summary>
        public bool hideVanillaHead = false;

        /// <summary>是否隐藏原版头发</summary>
        public bool hideVanillaHair = false;

        /// <summary>是否隐藏原版身体</summary>
        public bool hideVanillaBody = false;

        /// <summary>是否隐藏原版服装</summary>
        public bool hideVanillaApparel = false;

        /// <summary>需要隐藏的原版节点标签列表（支持精确匹配）</summary>
        [System.Obsolete("使用 hiddenPaths 替代，此字段保留用于向后兼容")]
        public List<string> hiddenTags = new List<string>();

        /// <summary>需要隐藏的原版节点路径列表（NodePath 精准定位，如 'Root/Body/Head:0'）</summary>
        public List<string> hiddenPaths = new List<string>();

        // ─────────────────────────────────────────────
        // 图层配置
        // ─────────────────────────────────────────────

        /// <summary>图层列表</summary>
        public List<PawnLayerConfig> layers = new List<PawnLayerConfig>();

        // ─────────────────────────────────────────────
        // 目标限制
        // ─────────────────────────────────────────────

        /// <summary>目标种族列表（为空表示所有种族可用）</summary>
        public List<string> targetRaces = new List<string>();

        /// <summary>是否仅限人形（Humanlike）</summary>
        public bool humanlikeOnly = true;

        // ─────────────────────────────────────────────
        // 元数据
        // ─────────────────────────────────────────────

        /// <summary>作者</summary>
        public string author = "";

        /// <summary>版本号</summary>
        public string version = "1.0.0";

        /// <summary>预览图路径</summary>
        public string previewTexPath = "";

        /// <summary>面部表情配置</summary>
        public PawnFaceConfig faceConfig = new PawnFaceConfig();

        // ─────────────────────────────────────────────
        // 运行时方法
        // ─────────────────────────────────────────────

        /// <summary>
        /// 检查是否适用于指定的 Pawn
        /// </summary>
        public bool IsValidForPawn(Pawn pawn)
        {
            if (pawn == null) return false;

            // 检查人形限制
            if (humanlikeOnly && !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            // 检查种族限制
            if (targetRaces != null && targetRaces.Count > 0)
            {
                string raceDef = pawn.def.defName;
                if (!targetRaces.Contains(raceDef))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 深拷贝当前定义
        /// </summary>
        public PawnSkinDef Clone()
        {
            var clone = new PawnSkinDef
            {
                defName = this.defName + "_Copy",
                label = this.label + " (Copy)",
                description = this.description,
                hideVanillaHead = this.hideVanillaHead,
                hideVanillaHair = this.hideVanillaHair,
                hideVanillaBody = this.hideVanillaBody,
                hideVanillaApparel = this.hideVanillaApparel,
                humanlikeOnly = this.humanlikeOnly,
                author = this.author,
                version = this.version,
                previewTexPath = this.previewTexPath,
                faceConfig = this.faceConfig?.Clone() ?? new PawnFaceConfig()
            };

            // 复制图层
            foreach (var layer in this.layers)
            {
                clone.layers.Add(layer.Clone());
            }

            // 复制目标种族
            clone.targetRaces = new List<string>(this.targetRaces);

            // 复制隐藏标签
            #pragma warning disable CS0618
            clone.hiddenTags = new List<string>(this.hiddenTags);
            #pragma warning restore CS0618

            // 复制隐藏路径
            clone.hiddenPaths = new List<string>(this.hiddenPaths);

            return clone;
        }
    }
}
