using UnityEngine;
using System.Collections.Generic;
using System;
using CharacterStudio.Exporter;

namespace CharacterStudio.Core
{
    public enum WeaponCarryVisualState
    {
        Undrafted,
        Drafted,
        Casting
    }

    /// <summary>
    /// 物品/武器视觉状态切换配置。
    /// </summary>
    public class WeaponCarryVisualConfig
    {
        [XmlExportField(BoolToLower = true)] public bool enabled = false;
        [XmlExportField] public string anchorTag = "Body";
        [XmlExportField(SkipEmptyString = true)] public string texUndrafted = string.Empty;
        [XmlExportField(SkipEmptyString = true)] public string texDrafted = string.Empty;
        [XmlExportField(SkipEmptyString = true)] public string texCasting = string.Empty;
        [XmlExportField] public Vector3 offset = Vector3.zero;
        [XmlExportField] public Vector3 offsetNorth = Vector3.zero;
        [XmlExportField] public Vector3 offsetEast = Vector3.zero;
        [XmlExportField] public Vector2 scale = Vector2.one;
        [XmlExportField] public Vector2 scaleNorthMultiplier = Vector2.one;
        [XmlExportField] public Vector2 scaleEastMultiplier = Vector2.one;
        [XmlExportField] public float rotation = 0f;
        [XmlExportField(SkipDefault = 0f, SkipDefaultFloat = true)] public float rotationNorthOffset = 0f;
        [XmlExportField(SkipDefault = 0f, SkipDefaultFloat = true)] public float rotationEastOffset = 0f;
        [XmlExportField(Format = "F3")] public float drawOrder = 80f;

        public string GetAnyTexPath()
        {
            if (!string.IsNullOrWhiteSpace(texUndrafted)) return texUndrafted;
            if (!string.IsNullOrWhiteSpace(texDrafted)) return texDrafted;
            if (!string.IsNullOrWhiteSpace(texCasting)) return texCasting;
            return string.Empty;
        }

        public string GetTexPath(WeaponCarryVisualState state)
        {
            return state switch
            {
                WeaponCarryVisualState.Casting when !string.IsNullOrWhiteSpace(texCasting) => texCasting,
                WeaponCarryVisualState.Drafted when !string.IsNullOrWhiteSpace(texDrafted) => texDrafted,
                _ => texUndrafted
            };
        }

        public Vector3 GetOffsetForRotation(Verse.Rot4 rot)
        {
            Vector3 directional = rot.AsInt switch
            {
                0 => offsetNorth,
                1 => offsetEast,
                3 => offsetEast,
                _ => Vector3.zero
            };
            return offset + directional;
        }

        public WeaponCarryVisualConfig Clone() => new WeaponCarryVisualConfig
        {
            enabled = this.enabled,
            anchorTag = this.anchorTag,
            texUndrafted = this.texUndrafted,
            texDrafted = this.texDrafted,
            texCasting = this.texCasting,
            offset = this.offset,
            offsetNorth = this.offsetNorth,
            offsetEast = this.offsetEast,
            scale = this.scale,
            scaleNorthMultiplier = this.scaleNorthMultiplier,
            scaleEastMultiplier = this.scaleEastMultiplier,
            rotation = this.rotation,
            rotationNorthOffset = this.rotationNorthOffset,
            rotationEastOffset = this.rotationEastOffset,
            drawOrder = this.drawOrder,
        };
    }

    /// <summary>
    /// 程序化动画参数（例如呼吸、悬浮）
    /// </summary>
    public class ProceduralAnimationConfig
    {
        [XmlExportField(BoolToLower = true)] public bool breathingEnabled = false;
        [XmlExportField] public float breathingSpeed = 1.0f;
        [XmlExportField] public float breathingAmplitude = 0.02f;

        [XmlExportField(BoolToLower = true)] public bool hoveringEnabled = false;
        [XmlExportField] public float hoveringSpeed = 1.0f;
        [XmlExportField] public float hoveringAmplitude = 0.05f;

        public ProceduralAnimationConfig Clone() => new ProceduralAnimationConfig
        {
            breathingEnabled = breathingEnabled,
            breathingSpeed = breathingSpeed,
            breathingAmplitude = breathingAmplitude,
            hoveringEnabled = hoveringEnabled,
            hoveringSpeed = hoveringSpeed,
            hoveringAmplitude = hoveringAmplitude
        };
    }

    /// <summary>
    /// 角色动画与姿态配置
    /// 原 WeaponRenderConfig 的升级版，现在支持全局程序化动画、多状态姿态覆写。
    /// </summary>
    public class PawnAnimationConfig
    {
        /// <summary>是否启用动画覆写系统</summary>
        [XmlExportField(BoolToLower = true)] public bool enabled = true;

        // --- 程序化动画 ---
        /// <summary>全局程序化效果（呼吸等）</summary>
        [XmlExportField] public ProceduralAnimationConfig procedural = new ProceduralAnimationConfig();

        // --- 武器/物品专项（保持原有字段名以兼容 XML） ---
        /// <summary>是否启用武器位置覆写</summary>
        [XmlExportField(BoolToLower = true)] public bool weaponOverrideEnabled = false;
        
        /// <summary>通用偏移</summary>
        [XmlExportField] public Vector3 offset = Vector3.zero;
        /// <summary>正面偏移</summary>
        [XmlExportField] public Vector3 offsetSouth = Vector3.zero;
        /// <summary>背面偏移</summary>
        [XmlExportField] public Vector3 offsetNorth = Vector3.zero;
        /// <summary>侧面偏移</summary>
        [XmlExportField] public Vector3 offsetEast = Vector3.zero;
        /// <summary>缩放乘数</summary>
        [XmlExportField] public Vector2 scale = Vector2.one;
        
        /// <summary>是否应用到副手</summary>
        [XmlExportField(BoolToLower = true)] public bool applyToOffHand = true;
        
        /// <summary>收纳/拔刀状态视觉</summary>
        [XmlExportField(Ignore = true)] public WeaponCarryVisualConfig carryVisual = new WeaponCarryVisualConfig();

        /// <summary>
        /// 根据 Pawn 朝向返回应叠加的武器偏移量。
        /// </summary>
        public Vector3 GetWeaponOffsetForRotation(Verse.Rot4 rot)
        {
            Vector3 dir = rot.AsInt switch
            {
                0 => offsetNorth,
                1 => offsetEast,
                2 => offsetSouth,
                3 => offsetEast,
                _ => Vector3.zero
            };
            return offset + dir;
        }

        public PawnAnimationConfig Clone() => new PawnAnimationConfig
        {
            enabled = this.enabled,
            procedural = this.procedural?.Clone() ?? new ProceduralAnimationConfig(),
            weaponOverrideEnabled = this.weaponOverrideEnabled,
            offset = this.offset,
            offsetSouth = this.offsetSouth,
            offsetNorth = this.offsetNorth,
            offsetEast = this.offsetEast,
            scale = this.scale,
            applyToOffHand = this.applyToOffHand,
            carryVisual = this.carryVisual?.Clone() ?? new WeaponCarryVisualConfig()
        };
    }
}
