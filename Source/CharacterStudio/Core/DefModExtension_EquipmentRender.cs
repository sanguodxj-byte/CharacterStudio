using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// Def 扩展：独立装备渲染数据
    /// 挂载到 Apparel ThingDef 上，供 CharacterStudio 在正式运行时
    /// 从 pawn.apparel.WornApparel 读取并注入自定义可视层。
    /// </summary>
    public class DefModExtension_EquipmentRender : DefModExtension
    {
        /// <summary>编辑器内装备定义名，仅用于追踪来源。</summary>
        public string equipmentDefName = "";

        /// <summary>显示标签，仅用于调试/追踪。</summary>
        public string label = "";

        /// <summary>编辑器内语义槽位。</summary>
        public string slotTag = "Apparel";

        /// <summary>是否启用该扩展渲染。</summary>
        public bool enabled = true;

        /// <summary>主贴图路径。</summary>
        public string texPath = "";

        /// <summary>可选蒙版贴图路径。</summary>
        public string maskTexPath = "";

        /// <summary>锚点标签。</summary>
        public string anchorTag = "Apparel";

        /// <summary>可选精确锚点路径。</summary>
        public string anchorPath = "";

        /// <summary>ShaderDef 名称。</summary>
        public string shaderDefName = "Cutout";

        public Vector3 offset = Vector3.zero;
        public Vector3 offsetEast = Vector3.zero;
        public Vector3 offsetNorth = Vector3.zero;
        public Vector2 scale = Vector2.one;
        public Vector2 scaleEastMultiplier = Vector2.one;
        public Vector2 scaleNorthMultiplier = Vector2.one;
        public float rotation = 0f;
        public float rotationEastOffset = 0f;
        public float rotationNorthOffset = 0f;
        public float drawOrder = 50f;
        public bool flipHorizontal = false;
        public bool visible = true;
        public LayerColorSource colorSource = LayerColorSource.White;
        public Color customColor = Color.white;
        public LayerColorSource colorTwoSource = LayerColorSource.White;
        public Color customColorTwo = Color.white;

        /// <summary>
        /// 预留字段：导出后的装备仍可附带能力绑定信息。
        /// 当前主要用于保留编辑器语义，不直接参与渲染。
        /// </summary>
        public List<string> abilityDefNames = new List<string>();

        public void EnsureDefaults()
        {
            equipmentDefName ??= string.Empty;
            label ??= string.Empty;
            slotTag = string.IsNullOrWhiteSpace(slotTag) ? "Apparel" : slotTag;
            texPath ??= string.Empty;
            maskTexPath ??= string.Empty;
            anchorTag = string.IsNullOrWhiteSpace(anchorTag) ? "Apparel" : anchorTag;
            anchorPath ??= string.Empty;
            shaderDefName = string.IsNullOrWhiteSpace(shaderDefName) ? "Cutout" : shaderDefName;
            abilityDefNames ??= new List<string>();
        }

        public bool HasRenderableTexture()
        {
            return !string.IsNullOrWhiteSpace(texPath);
        }

        public PawnLayerConfig ToPawnLayerConfig(string fallbackLabel)
        {
            EnsureDefaults();

            return new PawnLayerConfig
            {
                layerName = string.IsNullOrWhiteSpace(label) ? fallbackLabel : label,
                texPath = texPath,
                maskTexPath = maskTexPath,
                anchorTag = anchorTag,
                anchorPath = anchorPath,
                shaderDefName = shaderDefName,
                offset = offset,
                offsetEast = offsetEast,
                offsetNorth = offsetNorth,
                scale = scale,
                scaleEastMultiplier = scaleEastMultiplier,
                scaleNorthMultiplier = scaleNorthMultiplier,
                rotation = rotation,
                rotationEastOffset = rotationEastOffset,
                rotationNorthOffset = rotationNorthOffset,
                drawOrder = drawOrder,
                flipHorizontal = flipHorizontal,
                visible = visible,
                colorSource = colorSource,
                customColor = customColor,
                colorTwoSource = colorTwoSource,
                customColorTwo = customColorTwo
            };
        }

        public static DefModExtension_EquipmentRender FromEquipment(CharacterEquipmentDef equipment)
        {
            equipment.EnsureDefaults();
            var renderData = equipment.renderData ?? CharacterEquipmentRenderData.CreateDefault();

            return new DefModExtension_EquipmentRender
            {
                equipmentDefName = equipment.defName ?? string.Empty,
                label = equipment.GetDisplayLabel(),
                slotTag = equipment.slotTag ?? "Apparel",
                enabled = equipment.enabled,
                texPath = renderData.GetResolvedTexPath(),
                maskTexPath = renderData.maskTexPath ?? string.Empty,
                anchorTag = string.IsNullOrWhiteSpace(renderData.anchorTag) ? "Apparel" : renderData.anchorTag,
                anchorPath = renderData.anchorPath ?? string.Empty,
                shaderDefName = string.IsNullOrWhiteSpace(renderData.shaderDefName) ? equipment.shaderDefName : renderData.shaderDefName,
                offset = renderData.offset,
                offsetEast = renderData.offsetEast,
                offsetNorth = renderData.offsetNorth,
                scale = renderData.scale,
                scaleEastMultiplier = renderData.scaleEastMultiplier,
                scaleNorthMultiplier = renderData.scaleNorthMultiplier,
                rotation = renderData.rotation,
                rotationEastOffset = renderData.rotationEastOffset,
                rotationNorthOffset = renderData.rotationNorthOffset,
                drawOrder = renderData.drawOrder,
                flipHorizontal = renderData.flipHorizontal,
                visible = renderData.visible,
                colorSource = renderData.colorSource,
                customColor = renderData.customColor,
                colorTwoSource = renderData.colorTwoSource,
                customColorTwo = renderData.customColorTwo,
                abilityDefNames = equipment.abilityDefNames != null
                    ? new List<string>(equipment.abilityDefNames)
                    : new List<string>()
            };
        }
    }
}