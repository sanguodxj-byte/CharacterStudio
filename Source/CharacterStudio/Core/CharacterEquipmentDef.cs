using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 角色装备定义。
    /// 作为 PawnSkinDef 的嵌套编辑/保存对象存在，
    /// 用于描述一个可在编辑器中创建、导入、导出并参与运行时渲染的独立装备物品条目。
    /// </summary>
    public class CharacterEquipmentDef
    {
        public const string DefaultSlotTag = "Apparel";
        public const string DefaultAnchorTag = "Apparel";
        public const string DefaultShaderDefName = "Cutout";
        public const string DefaultParentThingDefName = "ApparelMakeableBase";
        /// <summary>唯一标识名，建议使用 DefName 风格。</summary>
        public string defName = "";

        /// <summary>显示名称。</summary>
        public string label = "";

        /// <summary>描述文本。</summary>
        public string description = "";

        /// <summary>是否启用该装备。</summary>
        public bool enabled = true;

        /// <summary>
        /// 编辑器内语义槽位。
        /// 例如：Apparel / Headgear / Accessory / Utility。
        /// </summary>
        public string slotTag = DefaultSlotTag;

        /// <summary>
        /// 导出后的独立装备物品 ThingDef 名称。
        /// 若为空，则默认回退到 defName。
        /// </summary>
        public string thingDefName = "";

        /// <summary>
        /// 可选抽象 ParentName。
        /// 默认导出到 ApparelMakeableBase。
        /// </summary>
        public string parentThingDefName = DefaultParentThingDefName;

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

        /// <summary>世界物品/图标贴图路径。</summary>
        public string worldTexPath = "";

        /// <summary>穿戴贴图路径（apparel.wornGraphicPath）。</summary>
        public string wornTexPath = "";

        /// <summary>可选蒙版贴图路径。</summary>
        public string maskTexPath = "";

        /// <summary>可选 shader 类型名称。</summary>
        public string shaderDefName = DefaultShaderDefName;

        /// <summary>物品类别标签（ThingCategories）。</summary>
        public List<string> thingCategories = new List<string>();

        /// <summary>覆盖的身体部位组（apparel.bodyPartGroups）。</summary>
        public List<string> bodyPartGroups = new List<string>();

        /// <summary>服装层（apparel.layers）。</summary>
        public List<string> apparelLayers = new List<string>();

        /// <summary>服装 tags（apparel.tags）。</summary>
        public List<string> apparelTags = new List<string>();

        /// <summary>是否使用穿戴蒙版。</summary>
        public bool useWornGraphicMask = false;

        /// <summary>基础数值（statBases）。</summary>
        public List<CharacterEquipmentStatEntry> statBases = new List<CharacterEquipmentStatEntry>();

        /// <summary>装备后数值偏移（equippedStatOffsets）。</summary>
        public List<CharacterEquipmentStatEntry> equippedStatOffsets = new List<CharacterEquipmentStatEntry>();

        /// <summary>CharacterStudio 自定义渲染数据。</summary>
        public CharacterEquipmentRenderData renderData = CharacterEquipmentRenderData.CreateDefault();

        /// <summary>
        /// 兼容旧存档/旧 XML 导入：
        /// 旧版本将导出目标写在 linkedThingDefName，并把可视参数写在 visual。
        /// 新版本不再把它们作为主数据来源，但保留字段避免旧数据直接丢失。
        /// </summary>
        [Obsolete("Use thingDefName/renderData instead.")]
        public string linkedThingDefName = "";

        /// <summary>
        /// 兼容旧存档/旧 XML 导入：
        /// 旧版本将装备当作 PawnLayerConfig 图层编辑。
        /// 新版本仅在导入迁移时读取，不应再作为主编辑模型。
        /// </summary>
        [Obsolete("Use renderData instead.")]
        public PawnLayerConfig? visual = null;

        public string GetDisplayLabel()
        {
            if (!string.IsNullOrWhiteSpace(label))
                return label;
            if (!string.IsNullOrWhiteSpace(defName))
                return defName;
            return "Equipment";
        }

        public string GetResolvedThingDefName()
        {
            if (!string.IsNullOrWhiteSpace(thingDefName))
                return thingDefName;

#pragma warning disable CS0618
            if (!string.IsNullOrWhiteSpace(linkedThingDefName))
                return linkedThingDefName;
#pragma warning restore CS0618

            if (!string.IsNullOrWhiteSpace(defName))
                return defName;

            return "CS_Equipment";
        }

        public bool HasRenderTexture()
        {
            return renderData != null && !string.IsNullOrWhiteSpace(renderData.GetResolvedTexPath());
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
                thingDefName = thingDefName,
                parentThingDefName = parentThingDefName,
                previewTexPath = previewTexPath,
                sourceNote = sourceNote,
                tags = tags != null ? new List<string>(tags) : new List<string>(),
                abilityDefNames = abilityDefNames != null
                    ? new List<string>(abilityDefNames)
                    : new List<string>(),
                worldTexPath = worldTexPath,
                wornTexPath = wornTexPath,
                maskTexPath = maskTexPath,
                shaderDefName = shaderDefName,
                thingCategories = thingCategories != null ? new List<string>(thingCategories) : new List<string>(),
                bodyPartGroups = bodyPartGroups != null ? new List<string>(bodyPartGroups) : new List<string>(),
                apparelLayers = apparelLayers != null ? new List<string>(apparelLayers) : new List<string>(),
                apparelTags = apparelTags != null ? new List<string>(apparelTags) : new List<string>(),
                useWornGraphicMask = useWornGraphicMask,
                statBases = CloneStatEntries(statBases),
                equippedStatOffsets = CloneStatEntries(equippedStatOffsets),
                renderData = renderData?.Clone() ?? CharacterEquipmentRenderData.CreateDefault(),
#pragma warning disable CS0618
                linkedThingDefName = linkedThingDefName,
                visual = visual?.Clone()
#pragma warning restore CS0618
            };
        }

        public void EnsureDefaults()
        {
            slotTag = string.IsNullOrWhiteSpace(slotTag) ? "Apparel" : slotTag;
            parentThingDefName = string.IsNullOrWhiteSpace(parentThingDefName) ? "ApparelMakeableBase" : parentThingDefName;
            shaderDefName = string.IsNullOrWhiteSpace(shaderDefName) ? "Cutout" : shaderDefName;

            tags ??= new List<string>();
            abilityDefNames ??= new List<string>();
            thingCategories ??= new List<string>();
            bodyPartGroups ??= new List<string>();
            apparelLayers ??= new List<string>();
            apparelTags ??= new List<string>();
            statBases ??= new List<CharacterEquipmentStatEntry>();
            equippedStatOffsets ??= new List<CharacterEquipmentStatEntry>();
            renderData ??= CharacterEquipmentRenderData.CreateDefault();

            MigrateLegacyVisualIfNeeded();

            if (string.IsNullOrWhiteSpace(thingDefName))
            {
#pragma warning disable CS0618
                thingDefName = !string.IsNullOrWhiteSpace(linkedThingDefName)
                    ? linkedThingDefName
                    : defName;
#pragma warning restore CS0618
            }

            if (string.IsNullOrWhiteSpace(worldTexPath) && !string.IsNullOrWhiteSpace(renderData.texPath))
            {
                worldTexPath = renderData.texPath;
            }

            if (string.IsNullOrWhiteSpace(wornTexPath) && !string.IsNullOrWhiteSpace(renderData.texPath))
            {
                wornTexPath = renderData.texPath;
            }

            if (string.IsNullOrWhiteSpace(maskTexPath) && !string.IsNullOrWhiteSpace(renderData.maskTexPath))
            {
                maskTexPath = renderData.maskTexPath;
            }

            if (bodyPartGroups.Count == 0)
            {
                bodyPartGroups.Add("Torso");
            }

            if (apparelLayers.Count == 0)
            {
                apparelLayers.Add("OnSkin");
            }

            renderData.EnsureDefaults(GetDisplayLabel(), wornTexPath, maskTexPath, shaderDefName);
            NormalizeStatEntries(statBases);
            NormalizeStatEntries(equippedStatOffsets);
        }

        private void MigrateLegacyVisualIfNeeded()
        {
#pragma warning disable CS0618
            if (visual == null)
                return;

            bool hasModernRenderData = renderData != null && (
                !string.IsNullOrWhiteSpace(renderData.texPath)
                || !string.IsNullOrWhiteSpace(renderData.layerName)
                || renderData.offset != Vector3.zero
                || renderData.offsetEast != Vector3.zero
                || renderData.offsetNorth != Vector3.zero
                || renderData.drawOrder != 50f
                || renderData.scale != Vector2.one);

            if (hasModernRenderData)
                return;

            renderData ??= CharacterEquipmentRenderData.CreateDefault();
            renderData.layerName = string.IsNullOrWhiteSpace(renderData.layerName) ? visual.layerName : renderData.layerName;
            renderData.texPath = string.IsNullOrWhiteSpace(renderData.texPath) ? visual.texPath : renderData.texPath;
            renderData.maskTexPath = string.IsNullOrWhiteSpace(renderData.maskTexPath) ? visual.maskTexPath : renderData.maskTexPath;
            renderData.anchorTag = string.IsNullOrWhiteSpace(renderData.anchorTag) ? visual.anchorTag : renderData.anchorTag;
            renderData.anchorPath = string.IsNullOrWhiteSpace(renderData.anchorPath) ? visual.anchorPath : renderData.anchorPath;
            renderData.shaderDefName = string.IsNullOrWhiteSpace(renderData.shaderDefName) ? visual.shaderDefName : renderData.shaderDefName;
            renderData.offset = visual.offset;
            renderData.offsetEast = visual.offsetEast;
            renderData.offsetNorth = visual.offsetNorth;
            renderData.scale = visual.scale;
            renderData.scaleEastMultiplier = visual.scaleEastMultiplier;
            renderData.scaleNorthMultiplier = visual.scaleNorthMultiplier;
            renderData.rotation = visual.rotation;
            renderData.rotationEastOffset = visual.rotationEastOffset;
            renderData.rotationNorthOffset = visual.rotationNorthOffset;
            renderData.drawOrder = visual.drawOrder;
            renderData.flipHorizontal = visual.flipHorizontal;
            renderData.visible = visual.visible;
            renderData.colorSource = visual.colorSource;
            renderData.customColor = visual.customColor;
            renderData.colorTwoSource = visual.colorTwoSource;
            renderData.customColorTwo = visual.customColorTwo;
#pragma warning restore CS0618
        }

        private static List<CharacterEquipmentStatEntry> CloneStatEntries(List<CharacterEquipmentStatEntry>? entries)
        {
            var result = new List<CharacterEquipmentStatEntry>();
            if (entries == null)
                return result;

            foreach (var entry in entries)
            {
                if (entry != null)
                    result.Add(entry.Clone());
            }

            return result;
        }

        private static void NormalizeStatEntries(List<CharacterEquipmentStatEntry>? entries)
        {
            if (entries == null)
                return;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    entries.RemoveAt(i);
                    continue;
                }

                entry.statDefName = entry.statDefName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(entry.statDefName))
                {
                    entries.RemoveAt(i);
                }
            }
        }
    }

    public class CharacterEquipmentRenderData
    {
        public string layerName = "Equipment";
        public string texPath = "";
        public string maskTexPath = "";
        public string anchorTag = "Apparel";
        public string anchorPath = "";
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

        // 技能驱动的闭环局部动画（装备侧）
        public bool useTriggeredLocalAnimation = false;
        public string triggerAbilityDefName = string.Empty;
        public string animationGroupKey = string.Empty;
        public EquipmentTriggeredAnimationRole triggeredAnimationRole = EquipmentTriggeredAnimationRole.MovablePart;
        public float triggeredDeployAngle = 45f;
        public float triggeredReturnAngle = 0f;
        public int triggeredDeployTicks = 12;
        public int triggeredHoldTicks = 24;
        public int triggeredReturnTicks = 12;
        public Vector2 triggeredPivotOffset = Vector2.zero;
        public bool triggeredUseVfxVisibility = false;

        public string triggeredIdleTexPath = string.Empty;
        public string triggeredDeployTexPath = string.Empty;
        public string triggeredHoldTexPath = string.Empty;
        public string triggeredReturnTexPath = string.Empty;

        public string triggeredIdleMaskTexPath = string.Empty;
        public string triggeredDeployMaskTexPath = string.Empty;
        public string triggeredHoldMaskTexPath = string.Empty;
        public string triggeredReturnMaskTexPath = string.Empty;

        public bool triggeredVisibleDuringDeploy = true;
        public bool triggeredVisibleDuringHold = true;
        public bool triggeredVisibleDuringReturn = true;
        public bool triggeredVisibleOutsideCycle = true;

        public static CharacterEquipmentRenderData CreateDefault()
        {
            return new CharacterEquipmentRenderData();
        }

        public CharacterEquipmentRenderData Clone()
        {
            return new CharacterEquipmentRenderData
            {
                layerName = layerName,
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
                customColorTwo = customColorTwo,
                useTriggeredLocalAnimation = useTriggeredLocalAnimation,
                triggerAbilityDefName = triggerAbilityDefName,
                animationGroupKey = animationGroupKey,
                triggeredAnimationRole = triggeredAnimationRole,
                triggeredDeployAngle = triggeredDeployAngle,
                triggeredReturnAngle = triggeredReturnAngle,
                triggeredDeployTicks = triggeredDeployTicks,
                triggeredHoldTicks = triggeredHoldTicks,
                triggeredReturnTicks = triggeredReturnTicks,
                triggeredPivotOffset = triggeredPivotOffset,
                triggeredUseVfxVisibility = triggeredUseVfxVisibility,
                triggeredIdleTexPath = triggeredIdleTexPath,
                triggeredDeployTexPath = triggeredDeployTexPath,
                triggeredHoldTexPath = triggeredHoldTexPath,
                triggeredReturnTexPath = triggeredReturnTexPath,
                triggeredIdleMaskTexPath = triggeredIdleMaskTexPath,
                triggeredDeployMaskTexPath = triggeredDeployMaskTexPath,
                triggeredHoldMaskTexPath = triggeredHoldMaskTexPath,
                triggeredReturnMaskTexPath = triggeredReturnMaskTexPath,
                triggeredVisibleDuringDeploy = triggeredVisibleDuringDeploy,
                triggeredVisibleDuringHold = triggeredVisibleDuringHold,
                triggeredVisibleDuringReturn = triggeredVisibleDuringReturn,
                triggeredVisibleOutsideCycle = triggeredVisibleOutsideCycle
            };
        }

        public void EnsureDefaults(string fallbackLabel, string fallbackTexPath, string fallbackMaskTexPath, string fallbackShader)
        {
            layerName = string.IsNullOrWhiteSpace(layerName) ? fallbackLabel : layerName;
            texPath = string.IsNullOrWhiteSpace(texPath) ? fallbackTexPath : texPath;
            maskTexPath = string.IsNullOrWhiteSpace(maskTexPath) ? fallbackMaskTexPath : maskTexPath;
            anchorTag = string.IsNullOrWhiteSpace(anchorTag) ? "Apparel" : anchorTag;
            shaderDefName = string.IsNullOrWhiteSpace(shaderDefName) ? fallbackShader : shaderDefName;
            triggerAbilityDefName ??= string.Empty;
            animationGroupKey = string.IsNullOrWhiteSpace(animationGroupKey) ? layerName : animationGroupKey;
            triggeredIdleTexPath ??= string.Empty;
            triggeredDeployTexPath ??= string.Empty;
            triggeredHoldTexPath ??= string.Empty;
            triggeredReturnTexPath ??= string.Empty;
            triggeredIdleMaskTexPath ??= string.Empty;
            triggeredDeployMaskTexPath ??= string.Empty;
            triggeredHoldMaskTexPath ??= string.Empty;
            triggeredReturnMaskTexPath ??= string.Empty;
            triggeredDeployTicks = Math.Max(1, triggeredDeployTicks);
            triggeredHoldTicks = Math.Max(0, triggeredHoldTicks);
            triggeredReturnTicks = Math.Max(1, triggeredReturnTicks);
        }

        public string GetResolvedTexPath()
        {
            return texPath ?? string.Empty;
        }
    }

    public enum EquipmentTriggeredAnimationRole
    {
        MovablePart,
        EffectLayer,
        Vfx = EffectLayer
    }

    public class CharacterEquipmentStatEntry
    {
        public string statDefName = "";
        public float value = 0f;

        public CharacterEquipmentStatEntry Clone()
        {
            return new CharacterEquipmentStatEntry
            {
                statDefName = statDefName,
                value = value
            };
        }
    }
}