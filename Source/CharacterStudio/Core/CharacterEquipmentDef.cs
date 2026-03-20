using System;
using System.Collections.Generic;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 角色装备定义。
    /// 作为 PawnSkinDef 的嵌套编辑/保存对象存在，
    /// 用于描述一个可在编辑器中创建、导入、导出并参与运行时渲染的装备条目。
    /// </summary>
    public class CharacterEquipmentDef
    {
        /// <summary>唯一标识名，建议使用 DefName 风格。</summary>
        public string defName = "";

        /// <summary>显示名称。</summary>
        public string label = "";

        /// <summary>描述文本。</summary>
        public string description = "";

        /// <summary>是否启用该装备。</summary>
        public bool enabled = true;

        /// <summary>
        /// 装备槽位标签，仅作为编辑期语义分类使用。
        /// 例如：Apparel / Headgear / Accessory / WeaponAttachment。
        /// </summary>
        public string slotTag = "Apparel";

        /// <summary>
        /// 可选：关联的 ThingDef / ApparelDef 名称。
        /// 主要用于导出时保留语义映射，并不强制参与运行时注入。
        /// </summary>
        public string linkedThingDefName = "";

        /// <summary>可选预览图路径。</summary>
        public string previewTexPath = "";

        /// <summary>可选来源/备注信息。</summary>
        public string sourceNote = "";

        /// <summary>标签列表，供分类与检索使用。</summary>
        public List<string> tags = new List<string>();

        /// <summary>
        /// 绑定到该装备的技能 defName 列表。
        /// 这些技能定义实际来自 PawnSkinDef.abilities 共享技能池。
        /// </summary>
        public List<string> abilityDefNames = new List<string>();

        /// <summary>
        /// 装备视觉配置。
        /// 直接复用 PawnLayerConfig，确保渲染字段与图层系统一致。
        /// </summary>
        public PawnLayerConfig visual = new PawnLayerConfig
        {
            layerName = "Equipment",
            anchorTag = "Apparel",
            shaderDefName = "Cutout",
            colorSource = LayerColorSource.White,
            colorTwoSource = LayerColorSource.White
        };

        public string GetDisplayLabel()
        {
            if (!string.IsNullOrWhiteSpace(label))
                return label;
            if (!string.IsNullOrWhiteSpace(defName))
                return defName;
            return "Equipment";
        }

        public bool HasVisual()
        {
            return visual != null && !string.IsNullOrWhiteSpace(visual.texPath);
        }

        public bool HasAbilityBindings()
        {
            return abilityDefNames != null && abilityDefNames.Count > 0;
        }

        public CharacterEquipmentDef Clone()
        {
            return new CharacterEquipmentDef
            {
                defName = defName,
                label = label,
                description = description,
                enabled = enabled,
                slotTag = slotTag,
                linkedThingDefName = linkedThingDefName,
                previewTexPath = previewTexPath,
                sourceNote = sourceNote,
                tags = tags != null ? new List<string>(tags) : new List<string>(),
                abilityDefNames = abilityDefNames != null
                    ? new List<string>(abilityDefNames)
                    : new List<string>(),
                visual = visual?.Clone() ?? new PawnLayerConfig
                {
                    layerName = "Equipment",
                    anchorTag = "Apparel",
                    shaderDefName = "Cutout",
                    colorSource = LayerColorSource.White,
                    colorTwoSource = LayerColorSource.White
                }
            };
        }

        public void EnsureDefaults()
        {
            slotTag = string.IsNullOrWhiteSpace(slotTag) ? "Apparel" : slotTag;
            visual ??= new PawnLayerConfig();
            visual.layerName = string.IsNullOrWhiteSpace(visual.layerName)
                ? GetDisplayLabel()
                : visual.layerName;
            visual.anchorTag = string.IsNullOrWhiteSpace(visual.anchorTag)
                ? "Apparel"
                : visual.anchorTag;
            visual.shaderDefName = string.IsNullOrWhiteSpace(visual.shaderDefName)
                ? "Cutout"
                : visual.shaderDefName;
        }
    }
}