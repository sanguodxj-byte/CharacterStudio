using System;
using System.Collections.Generic;
using System.Linq;
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
        public bool enabled = false;
        public BaseAppearanceSlotType slotType;
        public string texPath = "";
        public string maskTexPath = "";
        public string shaderDefName = "Cutout";
        public LayerColorSource colorSource = LayerColorSource.Fixed;
        public Color customColor = Color.white;
        public LayerColorSource colorTwoSource = LayerColorSource.Fixed;
        public Color customColorTwo = Color.white;
        public Vector2 scale = Vector2.one;
        public Vector2 scaleEastMultiplier = Vector2.one;
        public Vector2 scaleNorthMultiplier = Vector2.one;
        public Vector3 offset = Vector3.zero;
        public Vector3 offsetEast = Vector3.zero;
        public Vector3 offsetNorth = Vector3.zero;
        public float rotation = 0f;
        public float rotationEastOffset = 0f;
        public float rotationNorthOffset = 0f;
        public bool flipHorizontal = false;
        public float drawOrderOffset = 0f;
        public Type? graphicClass;

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
                offset = this.offset,
                offsetEast = this.offsetEast,
                offsetNorth = this.offsetNorth,
                rotation = this.rotation,
                rotationEastOffset = this.rotationEastOffset,
                rotationNorthOffset = this.rotationNorthOffset,
                flipHorizontal = this.flipHorizontal,
                drawOrderOffset = this.drawOrderOffset,
                graphicClass = this.graphicClass
            };
        }

        public PawnLayerConfig ToPawnLayer(string layerName, string anchorTag, float baseDrawOrder)
        {
            return new PawnLayerConfig
            {
                layerName = layerName,
                texPath = texPath,
                anchorTag = anchorTag,
                anchorPath = string.Empty,
                offset = offset,
                offsetEast = offsetEast,
                offsetNorth = offsetNorth,
                drawOrder = baseDrawOrder + drawOrderOffset,
                scale = scale,
                scaleEastMultiplier = scaleEastMultiplier,
                scaleNorthMultiplier = scaleNorthMultiplier,
                rotation = rotation,
                rotationEastOffset = rotationEastOffset,
                rotationNorthOffset = rotationNorthOffset,
                flipHorizontal = flipHorizontal,
                shaderDefName = string.IsNullOrEmpty(shaderDefName) ? "Cutout" : shaderDefName,
                colorSource = colorSource,
                customColor = customColor,
                colorTwoSource = colorTwoSource,
                customColorTwo = customColorTwo,
                maskTexPath = maskTexPath,
                visible = enabled,
                graphicClass = graphicClass
            };
        }
    }

    public class BaseAppearanceConfig
    {
        public List<BaseAppearanceSlotConfig> slots = new List<BaseAppearanceSlotConfig>();

        public BaseAppearanceConfig()
        {
            EnsureAllSlotsExist();
        }

        public BaseAppearanceConfig Clone()
        {
            var clone = new BaseAppearanceConfig();
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
