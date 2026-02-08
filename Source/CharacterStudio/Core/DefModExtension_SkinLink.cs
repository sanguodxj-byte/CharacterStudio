using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// Def 扩展：皮肤关联
    /// 用于将 GeneDef 或其他 Def 与 PawnSkinDef 关联
    ///
    /// 使用方式：
    /// 在 GeneDef 中添加：
    /// <modExtensions>
    ///   <li Class="CharacterStudio.Core.DefModExtension_SkinLink">
    ///     <skinDefName>CS_Skin_Example</skinDefName>
    ///   </li>
    /// </modExtensions>
    /// </summary>
    public class DefModExtension_SkinLink : DefModExtension
    {
        /// <summary>
        /// 关联的皮肤定义名称
        /// </summary>
        public string skinDefName = "";

        /// <summary>
        /// 皮肤优先级（数值越高优先级越高）
        /// 用于解决多个基因皮肤冲突的情况
        /// </summary>
        public int priority = 0;

        /// <summary>
        /// 是否为叠加模式（不替换原版外观，只添加图层）
        /// </summary>
        public bool overlayMode = false;

        /// <summary>
        /// 是否隐藏原版头部（仅在非叠加模式时生效）
        /// </summary>
        public bool hideVanillaHead = true;

        /// <summary>
        /// 是否隐藏原版头发（仅在非叠加模式时生效）
        /// </summary>
        public bool hideVanillaHair = true;

        /// <summary>
        /// 获取关联的皮肤定义
        /// </summary>
        public PawnSkinDef? GetSkinDef()
        {
            if (string.IsNullOrEmpty(skinDefName))
            {
                return null;
            }
            return DefDatabase<PawnSkinDef>.GetNamedSilentFail(skinDefName);
        }
    }
}