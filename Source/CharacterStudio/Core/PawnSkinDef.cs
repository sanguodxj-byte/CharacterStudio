using System.Collections.Generic;
using CharacterStudio.Abilities;
using CharacterStudio.AI;
using Verse;

namespace CharacterStudio.Core
{
    public class SkinAbilityHotkeyConfig
    {
        public bool enabled = false;
        public string qAbilityDefName = "";
        public string wAbilityDefName = "";
        public string eAbilityDefName = "";
        public string rAbilityDefName = "";
        public string wComboAbilityDefName = "";

        public SkinAbilityHotkeyConfig Clone()
        {
            return new SkinAbilityHotkeyConfig
            {
                enabled = enabled,
                qAbilityDefName = qAbilityDefName,
                wAbilityDefName = wAbilityDefName,
                eAbilityDefName = eAbilityDefName,
                rAbilityDefName = rAbilityDefName,
                wComboAbilityDefName = wComboAbilityDefName
            };
        }
    }

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

        /// <summary>基础外观槽位</summary>
        public BaseAppearanceConfig baseAppearance = new BaseAppearanceConfig();

        /// <summary>图层列表</summary>
        public List<PawnLayerConfig> layers = new List<PawnLayerConfig>();

        // ─────────────────────────────────────────────
        // 目标限制
        // ─────────────────────────────────────────────

        /// <summary>目标种族列表（为空表示所有种族可用）</summary>
        public List<string> targetRaces = new List<string>();

        /// <summary>是否仅限人形（Humanlike）</summary>
        public bool humanlikeOnly = true;

        /// <summary>是否将此皮肤作为 targetRaces 对应种族的默认皮肤自动绑定</summary>
        public bool applyAsDefaultForTargetRaces = false;

        /// <summary>默认种族绑定优先级（同种族多套皮肤时，高优先级覆盖低优先级）</summary>
        public int defaultRacePriority = 0;

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

        /// <summary>技能列表（供运行时热键施法与导出使用）</summary>
        public List<ModularAbilityDef> abilities = new List<ModularAbilityDef>();

        /// <summary>QWER 技能热键映射</summary>
        public SkinAbilityHotkeyConfig abilityHotkeys = new SkinAbilityHotkeyConfig();

        /// <summary>角色属性画像（供编辑器与 LLM 生成使用）</summary>
        public CharacterAttributeProfile attributes = new CharacterAttributeProfile();

        /// <summary>武器渲染覆写配置（偏移 / 缩放）</summary>
        public WeaponRenderConfig weaponRenderConfig = new WeaponRenderConfig();

        /// <summary>装备定义列表（编辑器内创建/导入/导出的装备条目）</summary>
        public List<CharacterEquipmentDef> equipments = new List<CharacterEquipmentDef>();

        // ─────────────────────────────────────────────
        // 种族身份
        // ─────────────────────────────────────────────

        /// <summary>绑定的 XenotypeDef defName（留空则不绑定）</summary>
        public string xenotypeDefName = "";

        /// <summary>在角色卡/信息界面中覆盖显示的种族名称（留空则使用原版）</summary>
        public string raceDisplayName = "";

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
                defName = this.defName,
                label = this.label,
                description = this.description,
                hideVanillaHead = this.hideVanillaHead,
                hideVanillaHair = this.hideVanillaHair,
                hideVanillaBody = this.hideVanillaBody,
                hideVanillaApparel = this.hideVanillaApparel,
                humanlikeOnly = this.humanlikeOnly,
                applyAsDefaultForTargetRaces = this.applyAsDefaultForTargetRaces,
                defaultRacePriority = this.defaultRacePriority,
                author = this.author,
                version = this.version,
                previewTexPath = this.previewTexPath,
                faceConfig = this.faceConfig?.Clone() ?? new PawnFaceConfig(),
                abilityHotkeys = this.abilityHotkeys?.Clone() ?? new SkinAbilityHotkeyConfig(),
                baseAppearance = this.baseAppearance?.Clone() ?? new BaseAppearanceConfig(),
                attributes = this.attributes?.Clone() ?? new CharacterAttributeProfile(),
                weaponRenderConfig = this.weaponRenderConfig?.Clone() ?? new WeaponRenderConfig()
            };

            clone.xenotypeDefName = this.xenotypeDefName;
            clone.raceDisplayName = this.raceDisplayName;

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

            // 复制装备
            if (this.equipments != null)
            {
                foreach (var equipment in this.equipments)
                {
                    if (equipment != null)
                    {
                        clone.equipments.Add(equipment.Clone());
                    }
                }
            }

            // 复制技能
            if (this.abilities != null)
            {
                foreach (var ability in this.abilities)
                {
                    if (ability != null)
                    {
                        clone.abilities.Add(ability.Clone());
                    }
                }
            }

            return clone;
        }
    }
}