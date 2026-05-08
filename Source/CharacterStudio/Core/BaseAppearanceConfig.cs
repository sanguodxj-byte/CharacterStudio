using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Exporter;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 基础外观槽位类型
    /// 只覆盖原版渲染树中有明确 tag 的顶层节点：Body / Head / Hair / Beard
    /// 面部表情（眼睛、嘴、眉毛等）通过 PawnFaceConfig 以整张头部贴图切换实现
    /// </summary>
    public enum BaseAppearanceSlotType
    {
        Body,
        Head,
        Hair,
        Beard
    }

    public class BaseAppearanceSlotConfig
    {
        [XmlExportField(BoolToLower = true)] public bool enabled = false;
        [XmlExportField] public BaseAppearanceSlotType slotType;
        [XmlExportField(SkipEmptyString = true)] public string texPath = "";
        [XmlExportField(SkipEmptyString = true)] public string maskTexPath = "";
        [XmlExportField(SkipEmptyString = true)] public string shaderDefName = "Cutout";
        [XmlExportField] public LayerColorSource colorSource = LayerColorSource.Fixed;
        [XmlExportField(Ignore = true)] public Color customColor = Color.white;
        [XmlExportField] public LayerColorSource colorTwoSource = LayerColorSource.Fixed;
        [XmlExportField(Ignore = true)] public Color customColorTwo = Color.white;
        [XmlExportField] public Vector2 scale = Vector2.one;
        [XmlExportField] public Vector2 scaleEastMultiplier = Vector2.one;
        [XmlExportField] public Vector2 scaleNorthMultiplier = Vector2.one;
        [XmlExportField(BoolToLower = true)] public bool useWestOffset = false;
        [XmlExportField] public Vector2 scaleWestMultiplier = Vector2.one;
        [XmlExportField] public Vector3 offset = Vector3.zero;
        [XmlExportField] public Vector3 offsetEast = Vector3.zero;
        [XmlExportField] public Vector3 offsetNorth = Vector3.zero;
        [XmlExportField] public Vector3 offsetWest = Vector3.zero;
        [XmlExportField] public float rotation = 0f;
        [XmlExportField(SkipDefault = 0f, SkipDefaultFloat = true)] public float rotationEastOffset = 0f;
        [XmlExportField(SkipDefault = 0f, SkipDefaultFloat = true)] public float rotationNorthOffset = 0f;
        [XmlExportField(SkipDefault = 0f, SkipDefaultFloat = true)] public float rotationWestOffset = 0f;
        [XmlExportField(BoolToLower = true)] public bool flipHorizontal = false;
        [XmlExportField] public float drawOrderOffset = 0f;
        [XmlExportField(Ignore = true)] public Type? graphicClass;

        public BaseAppearanceSlotConfig Clone()
        {
            return new BaseAppearanceSlotConfig
            {
                enabled = this.enabled,
                slotType = this.slotType,
                texPath = this.texPath,
                maskTexPath = this.maskTexPath,
                shaderDefName = this.shaderDefName,
                colorSource = this.colorSource,
                customColor = this.customColor,
                colorTwoSource = this.colorTwoSource,
                customColorTwo = this.customColorTwo,
                scale = this.scale,
                scaleEastMultiplier = this.scaleEastMultiplier,
                scaleNorthMultiplier = this.scaleNorthMultiplier,
                useWestOffset = this.useWestOffset,
                scaleWestMultiplier = this.scaleWestMultiplier,
                offset = this.offset,
                offsetEast = this.offsetEast,
                offsetNorth = this.offsetNorth,
                offsetWest = this.offsetWest,
                rotation = this.rotation,
                rotationEastOffset = this.rotationEastOffset,
                rotationNorthOffset = this.rotationNorthOffset,
                rotationWestOffset = this.rotationWestOffset,
                flipHorizontal = this.flipHorizontal,
                drawOrderOffset = this.drawOrderOffset,
                graphicClass = this.graphicClass
            };
        }

        public PawnLayerConfig ToPawnLayer(string layerName, string anchorTag, float baseDrawOrder, float drawSizeScale_unused = 1f)
        {
            // 全局缩放已在 PawnRenderNodeWorker_CustomLayer.ScaleFor() 中统一应用，此处仅使用槽位自身 scale
            return new PawnLayerConfig
            {
                layerName = layerName,
                texPath = texPath,
                anchorTag = anchorTag,
                anchorPath = string.Empty,
                offset = offset,
                offsetEast = offsetEast,
                offsetNorth = offsetNorth,
                useWestOffset = useWestOffset,
                offsetWest = offsetWest,
                drawOrder = baseDrawOrder + drawOrderOffset,
                scale = scale,
                scaleEastMultiplier = scaleEastMultiplier,
                scaleNorthMultiplier = scaleNorthMultiplier,
                scaleWestMultiplier = scaleWestMultiplier,
                rotation = rotation,
                rotationEastOffset = rotationEastOffset,
                rotationNorthOffset = rotationNorthOffset,
                rotationWestOffset = rotationWestOffset,
                flipHorizontal = flipHorizontal,
                shaderDefName = string.IsNullOrEmpty(shaderDefName) ? "Cutout" : shaderDefName,
                colorSource = colorSource,
                customColor = customColor,
                colorTwoSource = colorTwoSource,
                customColorTwo = customColorTwo,
                maskTexPath = maskTexPath,
                visible = enabled,
                graphicClass = graphicClass,
                // 基础槽位图层不应走 ResolveExpressionVariant 表情变体查找，
                // 设置 role 为 Base 使 UsesUnifiedVariantLogic 返回 true。
                role = LayerRole.Base
            };
        }
    }

    public class BaseAppearanceConfig
    {
        [XmlExportField] public float globalScale = 1f;
        [XmlExportField] public float drawSizeScale = 1f;
        [XmlExportField(SkipEmptyCollection = true)] public List<BaseAppearanceSlotConfig> slots = new List<BaseAppearanceSlotConfig>();

        public BaseAppearanceConfig()
        {
            EnsureAllSlotsExist();
        }

        public BaseAppearanceConfig Clone()
        {
            var clone = new BaseAppearanceConfig();
            clone.globalScale = globalScale;
            clone.drawSizeScale = drawSizeScale;
            clone.slots.Clear();
            foreach (var slot in slots)
            {
                if (slot != null)
                    clone.slots.Add(slot.Clone());
            }
            clone.EnsureAllSlotsExist();
            return clone;
        }

        public BaseAppearanceSlotConfig GetSlot(BaseAppearanceSlotType slotType)
        {
            EnsureAllSlotsExist();
            var slot = slots.FirstOrDefault(s => s != null && s.slotType == slotType);
            if (slot != null) return slot;

            slot = new BaseAppearanceSlotConfig { slotType = slotType };
            slots.Add(slot);
            return slot;
        }

        public IEnumerable<BaseAppearanceSlotConfig> EnabledSlots()
        {
            EnsureAllSlotsExist();
            return slots.Where(s => s != null && s.enabled && !string.IsNullOrWhiteSpace(s.texPath));
        }

        public void EnsureAllSlotsExist()
        {
            foreach (BaseAppearanceSlotType slotType in Enum.GetValues(typeof(BaseAppearanceSlotType)))
            {
                if (!slots.Any(s => s != null && s.slotType == slotType))
                    slots.Add(new BaseAppearanceSlotConfig { slotType = slotType });
            }
        }
    }
}
