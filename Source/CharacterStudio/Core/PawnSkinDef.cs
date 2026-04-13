using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.AI;
using CharacterStudio.Attributes;
using Verse;

namespace CharacterStudio.Core
{
    public class SkinAbilityHotkeyConfig : IExposable
    {
        public bool enabled = false;
        public string qAbilityDefName = "";
        public string wAbilityDefName = "";
        public string eAbilityDefName = "";
        public string rAbilityDefName = "";
        public string tAbilityDefName = "";
        public string aAbilityDefName = "";
        public string sAbilityDefName = "";
        public string dAbilityDefName = "";
        public string fAbilityDefName = "";
        public string zAbilityDefName = "";
        public string xAbilityDefName = "";
        public string cAbilityDefName = "";
        public string vAbilityDefName = "";

        public SkinAbilityHotkeyConfig Clone()
        {
            return new SkinAbilityHotkeyConfig
            {
                enabled = enabled,
                qAbilityDefName = qAbilityDefName,
                wAbilityDefName = wAbilityDefName,
                eAbilityDefName = eAbilityDefName,
                rAbilityDefName = rAbilityDefName,
                tAbilityDefName = tAbilityDefName,
                aAbilityDefName = aAbilityDefName,
                sAbilityDefName = sAbilityDefName,
                dAbilityDefName = dAbilityDefName,
                fAbilityDefName = fAbilityDefName,
                zAbilityDefName = zAbilityDefName,
                xAbilityDefName = xAbilityDefName,
                cAbilityDefName = cAbilityDefName,
                vAbilityDefName = vAbilityDefName
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Values.Look(ref qAbilityDefName, "qAbilityDefName", "");
            Scribe_Values.Look(ref wAbilityDefName, "wAbilityDefName", "");
            Scribe_Values.Look(ref eAbilityDefName, "eAbilityDefName", "");
            Scribe_Values.Look(ref rAbilityDefName, "rAbilityDefName", "");
            Scribe_Values.Look(ref tAbilityDefName, "tAbilityDefName", "");
            Scribe_Values.Look(ref aAbilityDefName, "aAbilityDefName", "");
            Scribe_Values.Look(ref sAbilityDefName, "sAbilityDefName", "");
            Scribe_Values.Look(ref dAbilityDefName, "dAbilityDefName", "");
            Scribe_Values.Look(ref fAbilityDefName, "fAbilityDefName", "");
            Scribe_Values.Look(ref zAbilityDefName, "zAbilityDefName", "");
            Scribe_Values.Look(ref xAbilityDefName, "xAbilityDefName", "");
            Scribe_Values.Look(ref cAbilityDefName, "cAbilityDefName", "");
            Scribe_Values.Look(ref vAbilityDefName, "vAbilityDefName", "");

            qAbilityDefName ??= string.Empty;
            wAbilityDefName ??= string.Empty;
            eAbilityDefName ??= string.Empty;
            rAbilityDefName ??= string.Empty;
            tAbilityDefName ??= string.Empty;
            aAbilityDefName ??= string.Empty;
            sAbilityDefName ??= string.Empty;
            dAbilityDefName ??= string.Empty;
            fAbilityDefName ??= string.Empty;
            zAbilityDefName ??= string.Empty;
            xAbilityDefName ??= string.Empty;
            cAbilityDefName ??= string.Empty;
            vAbilityDefName ??= string.Empty;
        }
    }

    /// <summary>
    /// 皮肤定义 (Def)
    /// 定义一个完整的角色皮肤，包含多个图层和配置选项
    /// </summary>
    public class PawnSkinDef : Def
    {
        private static readonly string[] ApparelNodeMarkers =
        {
            "Apparel",
            "Headgear"
        };

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

        /// <summary>全局纹理缩放（统一影响走 CustomLayer 的纹理渲染）</summary>
        public float globalTextureScale = 1f;

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

        /// <summary>属性增强 Buff 配置：通过统一 Hediff 对宿主种族的最终数值做安全叠加</summary>
        public CharacterStatModifierProfile statModifiers = new CharacterStatModifierProfile();

        /// <summary>动画与姿态渲染配置（呼吸、偏移、武器姿态等）</summary>
        public PawnAnimationConfig animationConfig = new PawnAnimationConfig();

        // ─────────────────────────────────────────────
        // 预览基准补偿
        // ─────────────────────────────────────────────

        /// <summary>皮肤创建/编辑时预览人偶的 Head 节点 Z 偏移，用于自动补偿不同体型的面部 Z 差异。0 表示未记录（不做补偿）。</summary>
        public float previewHeadOffsetZ = 0f;

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
                globalTextureScale = this.globalTextureScale,
                faceConfig = this.faceConfig?.Clone() ?? new PawnFaceConfig(),
                abilityHotkeys = this.abilityHotkeys?.Clone() ?? new SkinAbilityHotkeyConfig(),
                baseAppearance = this.baseAppearance?.Clone() ?? new BaseAppearanceConfig(),
                attributes = this.attributes?.Clone() ?? new CharacterAttributeProfile(),
                statModifiers = this.statModifiers?.Clone() ?? new CharacterStatModifierProfile(),
                animationConfig = this.animationConfig?.Clone() ?? new PawnAnimationConfig()
            };

            clone.xenotypeDefName = this.xenotypeDefName;
            clone.raceDisplayName = this.raceDisplayName;
            clone.previewHeadOffsetZ = this.previewHeadOffsetZ;
            clone.globalTextureScale = this.globalTextureScale;

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

        public void RemoveApparelHidingData()
        {
            hideVanillaApparel = false;

            hiddenPaths ??= new List<string>();
            hiddenPaths = hiddenPaths
                .Where(path => !IsApparelNodeReference(path))
                .ToList();

#pragma warning disable CS0618
            hiddenTags ??= new List<string>();
            hiddenTags = hiddenTags
                .Where(tag => !IsApparelNodeReference(tag))
                .ToList();
#pragma warning restore CS0618
        }

        private static bool IsApparelNodeReference(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalizedValue = value!;

            foreach (string marker in ApparelNodeMarkers)
            {
                if (normalizedValue.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
