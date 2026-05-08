using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CharacterStudio.Exporter;

namespace CharacterStudio.Core
{
    public enum EquipmentType
    {
        Apparel,
        WeaponMelee,
        WeaponRanged,
        Item,
        Building,
        Turret
    }

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

        /// <summary>模板模式下各类型对应的 CS 抽象基类名</summary>
        public static string GetTemplateBaseName(EquipmentType type) => type switch
        {
            EquipmentType.Apparel => "CS_ApparelBase",
            EquipmentType.WeaponRanged => "CS_GunBase",
            EquipmentType.WeaponMelee => "CS_MeleeSharpBase",
            EquipmentType.Building => "CS_BuildingBase",
            EquipmentType.Turret => "CS_TurretBase",
            EquipmentType.Item => "CS_ItemBase",
            _ => "ApparelMakeableBase"
        };

        /// <summary>全量模式下各类型的 RimWorld 原版基类名</summary>
        public static string GetFullModeBaseName(EquipmentType type) => type switch
        {
            EquipmentType.Apparel => "ApparelMakeableBase",
            EquipmentType.WeaponRanged => "BaseHumanMakeableGun",
            EquipmentType.WeaponMelee => "BaseMeleeWeapon_Sharp_Quality",
            EquipmentType.Building => "BuildingBase",
            EquipmentType.Turret => "BuildingBase",
            EquipmentType.Item => "ResourceBase",
            _ => "ApparelMakeableBase"
        };

        /// <summary>物品类型，决定导出格式</summary>
        public EquipmentType itemType = EquipmentType.Apparel;
        
        /// <summary>唯一标识名，建议使用 DefName 风格。</summary>
        public string defName = "";

        /// <summary>显示名称。</summary>
        public string label = "";

        /// <summary>描述文本。</summary>
        public string description = "";

        /// <summary>是否启用该装备。</summary>
        public bool enabled = true;

        /// <summary>
        /// 编辑模式：true = 模板模式（继承CS基类，主面板精简），false = 全量模式（继承原版基类，所有字段展开）
        /// </summary>
        public bool useTemplateMode = true;

        /// <summary>
        /// 额外字段存储（低频字段补充器添加的 key-value 对）。
        /// 导出时作为 &lt;key&gt;value&lt;/key&gt; 直接写入 ThingDef。
        /// </summary>
        public Dictionary<string, string> extraFields = new Dictionary<string, string>();

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

        /// <summary>导出的 PawnFlyer ThingDef 名称；为空时可由导出器按能力绑定自动生成。</summary>
        public string flyerThingDefName = "";

        /// <summary>导出的 PawnFlyer thingClass 完整类型名。</summary>
        public string flyerClassName = "CharacterStudio.Abilities.CharacterStudioPawnFlyer_Default";

        /// <summary>导出的 PawnFlyer flightSpeed。</summary>
        public float flyerFlightSpeed = 22f;

        /// <summary>标签列表，供分类与检索使用。</summary>
        public List<string> tags = new List<string>();

        /// <summary>导出分组键；同组装备可视为一个导出包。</summary>
        public string exportGroupKey = "";

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

        /// <summary>贸易标签（tradeTags）。</summary>
        public List<string> tradeTags = new List<string>();

        /// <summary>武器标签（weaponTags）。</summary>
        public List<string> weaponTags = new List<string>();

        /// <summary>武器类别（weaponClasses）。</summary>
        public List<string> weaponClasses = new List<string>();

        /// <summary>是否允许通过制作获得。</summary>
        public bool allowCrafting = false;

        /// <summary>配方 Def 名称。</summary>
        public string recipeDefName = "";

        /// <summary>工作台 ThingDef 名称。</summary>
        public string recipeWorkbenchDefName = "TableMachining";

        /// <summary>制作工时。</summary>
        public float recipeWorkAmount = 1200f;

        /// <summary>每次制作产量。</summary>
        public int recipeProductCount = 1;

        /// <summary>制作原料列表。</summary>
        public List<CharacterEquipmentCostEntry> recipeIngredients = new List<CharacterEquipmentCostEntry>();

        /// <summary>是否允许参与交易/购买。</summary>
        public bool allowTrading = true;

        /// <summary>市场价值。</summary>
        public float marketValue = 250f;

        /// <summary>是否使用穿戴蒙版。</summary>
        public bool useWornGraphicMask = false;

        /// <summary>基础数值（statBases）。</summary>
        public List<CharacterEquipmentStatEntry> statBases = new List<CharacterEquipmentStatEntry>();

        /// <summary>装备后数值偏移（equippedStatOffsets）。</summary>
        public List<CharacterEquipmentStatEntry> equippedStatOffsets = new List<CharacterEquipmentStatEntry>();

        // ── ThingDef 完整字段补全 ──

        /// <summary>自定义 thingClass 完整类型名（如 Axolotl.ApparelWithOutColor）。</summary>
        public string thingClass = "";

        /// <summary>科技等级（Neolithic/Medieval/Industrial/Spacer/Ultra 等）。</summary>
        public string techLevel = "";

        /// <summary>材料类别列表（stuffCategories，如 Fabric/Leathery/Metallic）。</summary>
        public List<string> stuffCategories = new List<string>();

        /// <summary>材料消耗数量（costStuffCount）。0 表示不输出。</summary>
        public int costStuffCount = 0;

        /// <summary>ThingDef 级直接材料消耗列表（costList，如 Steel&gt;120）。</summary>
        public List<CharacterEquipmentCostEntry> costList = new List<CharacterEquipmentCostEntry>();

        /// <summary>graphicData/drawSize（默认 1,1）。</summary>
        public string graphicDrawSize = "";

        /// <summary>graphicData/onGroundRandomRotateAngle。</summary>
        public float graphicRandomRotateAngle = 0f;

        /// <summary>pathCost。</summary>
        public int pathCost = 0;

        /// <summary>useHitPoints。</summary>
        public bool useHitPoints = true;

        /// <summary>altitudeLayer（如 Item/Mote/Misc 等）。</summary>
        public string altitudeLayer = "";

        /// <summary>tickerType（如 Never/Normal/Rare 等）。</summary>
        public string tickerType = "";

        /// <summary>smeltable。</summary>
        public bool smeltable = false;

        /// <summary>rotatable。</summary>
        public bool rotatable = false;

        /// <summary>selectable。</summary>
        public bool selectable = true;

        /// <summary>drawGUIOverlay。</summary>
        public bool drawGUIOverlay = true;

        /// <summary>alwaysHaulable。</summary>
        public bool alwaysHaulable = true;

        /// <summary>useHitPoints 的物品耐久相关。</summary>
        public bool burningByRecipe = false;

        // ── recipeMaker 子字段 ──

        /// <summary>前置研究 defName。</summary>
        public string recipeResearchPrerequisite = "";

        /// <summary>技能需求列表（skillRequirements，如 Crafting=5,Artistic=3）。</summary>
        public List<CharacterEquipmentStatEntry> recipeSkillRequirements = new List<CharacterEquipmentStatEntry>();

        /// <summary>工作特效（effectWorking，如 Tailor/Smith）。</summary>
        public string recipeEffectWorking = "";

        /// <summary>工作音效（soundWorking）。</summary>
        public string recipeSoundWorking = "";

        /// <summary>制作台列表（recipeUsers，覆盖 recipeWorkbenchDefName）。</summary>
        public List<string> recipeUsers = new List<string>();

        /// <summary>半成品 Def（unfinishedThingDef）。</summary>
        public string recipeUnfinishedThingDef = "";

        /// <summary>显示优先级（displayPriority）。</summary>
        public float recipeDisplayPriority = 0f;

        /// <summary>工作技能（workSkill，如 Crafting）。</summary>
        public string recipeWorkSkill = "";

        /// <summary>工作速度 stat（workSpeedStat，如 GeneralLaborSpeed）。</summary>
        public string recipeWorkSpeedStat = "";

        /// <summary>每日磨损率（apparel/wearPerDay）。</summary>
        public float wearPerDay = 0f;

        /// <summary>是否检查损坏（apparel/careIfDamaged）。null=不输出。</summary>
        public bool? careIfDamaged = null;

        /// <summary>是否检查尸体穿着（apparel/careIfWornByCorpse）。null=不输出。</summary>
        public bool? careIfWornByCorpse = null;

        /// <summary>算作服装避免裸体惩罚（apparel/countsAsClothingForNudity）。null=不输出。</summary>
        public bool? countsAsClothingForNudity = null;

        /// <summary>是否奴隶服装（apparel/slaveApparel）。null=不输出。</summary>
        public bool? slaveApparel = null;

        /// <summary>是否可被Ideo需求（apparel/canBeDesignedForIdeo）。null=不输出。</summary>
        public bool? canBeDesiredForIdeo = null;

        /// <summary>穿戴音效（apparel/soundWear）。</summary>
        public string soundWear = "";

        /// <summary>脱下音效（apparel/soundRemove）。</summary>
        public string soundRemove = "";

        /// <summary>使用金属偏转特效（apparel/useDeflectMetalEffect）。</summary>
        public bool useDeflectMetalEffect = false;

        /// <summary>渲染跳过标记（apparel/renderSkipFlags）。</summary>
        public List<string> apparelRenderSkipFlags = new List<string>();

        /// <summary>发育阶段过滤（apparel/developmentalStageFilter，如 Child/Adult）。</summary>
        public string developmentalStageFilter = "";

        /// <summary>是否遮挡视线（apparel/blocksVision）。null=不输出。</summary>
        public bool? blocksVision = null;

        /// <summary>不可由非暴力角色穿戴（apparel/ignoredByNonViolent）。null=不输出。</summary>
        public bool? ignoredByNonViolent = null;

        /// <summary>裸体评分偏移（apparel/scoreOffset）。</summary>
        public float apparelScoreOffset = 0f;

        /// <summary>
        /// apparel/drawData 的原始 XML 内容。RimWorld 1.6 原生 ApparelDrawData 结构，
        /// 包含每方向渲染层级偏移。CS renderData 用 drawOrder 替代其功能，
        /// 但为保持原版兼容性，导入时保留此原始XML，导出时原样输出。
        /// </summary>
        public string apparelDrawDataXml = "";

        // ── 原始 XML 块（comps/verbs/tools — 无法类型化的高级内容） ──

        /// <summary>
        /// 原始 XML 条目，用于 comps/verbs/tools 等无法类型化的高级 ThingDef 子节点。
        /// 每项的 tagName 决定输出节点名（如 "comps"/"verbs"/"tools"），
        /// innerXml 为该节点内部的原始 XML 文本。
        /// </summary>
        public List<RawXmlEntry> rawXmlEntries = new List<RawXmlEntry>();

        /// <summary>
        /// 从正式 ThingDef XML 导入时保存的完整原始 XML。
        /// 非破坏性导出时在此 XML 上做 patch，保留编辑器未管理的字段不变。
        /// 为空时走常规从零构建导出路径。
        /// </summary>
        public string rawOriginalThingDefXml = "";

        // ── 建筑专有字段（itemType == Building / Turret 时有效） ──

        /// <summary>建筑占地尺寸，如 "2,2"。</summary>
        public string buildingSize = "";

        /// <summary>通行类型（PassThroughOnly / Impassable / Standable 等）。</summary>
        public string passability = "";

        /// <summary>填充百分比（0.0-1.0），影响遮挡和射击掩体。</summary>
        public float fillPercent = 0f;

        /// <summary>地形需求（Heavy / Medium / Light）。</summary>
        public string terrainAffordanceNeeded = "";

        /// <summary>是否阻挡风。</summary>
        public bool? blockWind = null;

        /// <summary>是否投射边缘阴影。</summary>
        public bool? castEdgeShadows = null;

        /// <summary>绘制类型（MapMeshOnly / MapMeshAndRealTime / RealtimeOnly）。</summary>
        public string drawerType = "";

        /// <summary>是否可与区域重叠。</summary>
        public bool? canOverlapZones = null;

        /// <summary>是否有交互格子。</summary>
        public bool hasInteractionCell = false;

        /// <summary>交互格子偏移，如 "(0,0,1)"。</summary>
        public string interactionCellOffset = "";

        /// <summary>被摧毁时的掉落物列表。</summary>
        public List<CharacterEquipmentCostEntry> killedLeavings = new List<CharacterEquipmentCostEntry>();

        /// <summary>伤害倍率列表（DamageDef:multiplier）。</summary>
        public List<CharacterEquipmentDamageMultiplierEntry> damageMultipliers = new List<CharacterEquipmentDamageMultiplierEntry>();

        /// <summary>建筑分类目录（如 Security / Misc / Structure）。</summary>
        public string designationCategory = "";

        /// <summary>建筑标签列表（building/buildingTags）。</summary>
        public List<string> buildingTags = new List<string>();

        /// <summary>建筑战斗强度（building/combatPower）。</summary>
        public float combatPower = 0f;

        /// <summary>屋顶坍塌伤害倍率（building/roofCollapseDamageMultiplier）。</summary>
        public float roofCollapseDamageMultiplier = 0f;

        /// <summary>建筑销毁音效（building/destroySound）。</summary>
        public string destroySound = "";

        // ── 炮塔专有字段（itemType == Turret 时有效） ──

        /// <summary>炮塔关联的武器 ThingDef defName（building/turretGunDef）。</summary>
        public string turretGunDef = "";

        /// <summary>炮塔射击预热时间（building/turretBurstWarmupTime）。</summary>
        public float turretBurstWarmupTime = 0f;

        /// <summary>炮塔射击冷却时间（building/turretBurstCooldownTime）。</summary>
        public float turretBurstCooldownTime = 0f;

        /// <summary>炮塔初始冷却时间（building/turretInitialCooldownTime）。</summary>
        public float turretInitialCooldownTime = 0f;

        /// <summary>是否为机械集群威胁（building/isMechClusterThreat）。</summary>
        public bool isMechClusterThreat = false;

        /// <summary>CharacterStudio 自定义渲染数据。</summary>
        public CharacterEquipmentRenderData renderData = CharacterEquipmentRenderData.CreateDefault();

        /// <summary>
        /// 根据 useTemplateMode 和 itemType 刷新 parentThingDefName。
        /// 如果 parentThingDefName 是已知的基类名（模板或原版），则自动切换；用户自定义的不动。
        /// </summary>
        public void RefreshParentThingDefName()
        {
            string templateBase = GetTemplateBaseName(itemType);
            string fullModeBase = GetFullModeBaseName(itemType);
            // 已知的所有模板基类名和原版基类名
            var allKnownBases = new HashSet<string>
            {
                "CS_ApparelBase", "CS_GunBase", "CS_MeleeSharpBase", "CS_MeleeBluntBase",
                "CS_BuildingBase", "CS_TurretBase", "CS_ItemBase",
                "ApparelMakeableBase", "BaseHumanMakeableGun",
                "BaseMeleeWeapon_Sharp_Quality", "BaseMeleeWeapon_Blunt_Quality",
                "BuildingBase", "ResourceBase", "BaseMakeableGrenade"
            };
            if (allKnownBases.Contains(parentThingDefName) || string.IsNullOrWhiteSpace(parentThingDefName))
            {
                parentThingDefName = useTemplateMode ? templateBase : fullModeBase;
            }
        }

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
                itemType = itemType,
                defName = defName,
                label = label,
                description = description,
                enabled = enabled,
                useTemplateMode = useTemplateMode,
                extraFields = extraFields != null ? new Dictionary<string, string>(extraFields) : new Dictionary<string, string>(),
                slotTag = slotTag,
                thingDefName = thingDefName,
                parentThingDefName = parentThingDefName,
                previewTexPath = previewTexPath,
                sourceNote = sourceNote,
                flyerThingDefName = flyerThingDefName,
                flyerClassName = flyerClassName,
                flyerFlightSpeed = flyerFlightSpeed,
                tags = tags != null ? new List<string>(tags) : new List<string>(),
                exportGroupKey = exportGroupKey,
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
                tradeTags = tradeTags != null ? new List<string>(tradeTags) : new List<string>(),
                weaponTags = weaponTags != null ? new List<string>(weaponTags) : new List<string>(),
                weaponClasses = weaponClasses != null ? new List<string>(weaponClasses) : new List<string>(),
                allowCrafting = allowCrafting,
                recipeDefName = recipeDefName,
                recipeWorkbenchDefName = recipeWorkbenchDefName,
                recipeWorkAmount = recipeWorkAmount,
                recipeProductCount = recipeProductCount,
                recipeIngredients = CloneCostEntries(recipeIngredients),
                allowTrading = allowTrading,
                marketValue = marketValue,
                useWornGraphicMask = useWornGraphicMask,
                statBases = CloneStatEntries(statBases),
                equippedStatOffsets = CloneStatEntries(equippedStatOffsets),
                renderData = renderData?.Clone() ?? CharacterEquipmentRenderData.CreateDefault(),
                // ThingDef 完整字段
                thingClass = thingClass,
                techLevel = techLevel,
                stuffCategories = stuffCategories != null ? new List<string>(stuffCategories) : new List<string>(),
                costStuffCount = costStuffCount,
                costList = CloneCostEntries(costList),
                graphicDrawSize = graphicDrawSize,
                graphicRandomRotateAngle = graphicRandomRotateAngle,
                pathCost = pathCost,
                useHitPoints = useHitPoints,
                altitudeLayer = altitudeLayer,
                tickerType = tickerType,
                smeltable = smeltable,
                rotatable = rotatable,
                selectable = selectable,
                drawGUIOverlay = drawGUIOverlay,
                alwaysHaulable = alwaysHaulable,
                burningByRecipe = burningByRecipe,
                // recipeMaker 子字段
                recipeResearchPrerequisite = recipeResearchPrerequisite,
                recipeSkillRequirements = CloneStatEntries(recipeSkillRequirements),
                recipeEffectWorking = recipeEffectWorking,
                recipeSoundWorking = recipeSoundWorking,
                recipeUsers = recipeUsers != null ? new List<string>(recipeUsers) : new List<string>(),
                recipeUnfinishedThingDef = recipeUnfinishedThingDef,
                recipeDisplayPriority = recipeDisplayPriority,
                recipeWorkSkill = recipeWorkSkill,
                recipeWorkSpeedStat = recipeWorkSpeedStat,
                // apparel 子字段
                wearPerDay = wearPerDay,
                careIfDamaged = careIfDamaged,
                careIfWornByCorpse = careIfWornByCorpse,
                countsAsClothingForNudity = countsAsClothingForNudity,
                slaveApparel = slaveApparel,
                canBeDesiredForIdeo = canBeDesiredForIdeo,
                soundWear = soundWear,
                soundRemove = soundRemove,
                useDeflectMetalEffect = useDeflectMetalEffect,
                apparelRenderSkipFlags = apparelRenderSkipFlags != null ? new List<string>(apparelRenderSkipFlags) : new List<string>(),
                developmentalStageFilter = developmentalStageFilter,
                blocksVision = blocksVision,
                ignoredByNonViolent = ignoredByNonViolent,
                apparelScoreOffset = apparelScoreOffset,
                // 原始 XML
                rawXmlEntries = rawXmlEntries != null ? rawXmlEntries.Where(e => e != null).Select(e => e.Clone()).ToList() : new List<RawXmlEntry>(),
                apparelDrawDataXml = apparelDrawDataXml ?? string.Empty,
                rawOriginalThingDefXml = rawOriginalThingDefXml ?? string.Empty,
                // 建筑专有字段
                buildingSize = buildingSize ?? string.Empty,
                passability = passability ?? string.Empty,
                fillPercent = fillPercent,
                terrainAffordanceNeeded = terrainAffordanceNeeded ?? string.Empty,
                blockWind = blockWind,
                castEdgeShadows = castEdgeShadows,
                drawerType = drawerType ?? string.Empty,
                canOverlapZones = canOverlapZones,
                hasInteractionCell = hasInteractionCell,
                interactionCellOffset = interactionCellOffset ?? string.Empty,
                killedLeavings = CloneCostEntries(killedLeavings),
                damageMultipliers = damageMultipliers != null ? damageMultipliers.Where(e => e != null).Select(e => e.Clone()).ToList() : new List<CharacterEquipmentDamageMultiplierEntry>(),
                designationCategory = designationCategory ?? string.Empty,
                buildingTags = buildingTags != null ? new List<string>(buildingTags) : new List<string>(),
                combatPower = combatPower,
                roofCollapseDamageMultiplier = roofCollapseDamageMultiplier,
                destroySound = destroySound ?? string.Empty,
                // 炮塔专有字段
                turretGunDef = turretGunDef ?? string.Empty,
                turretBurstWarmupTime = turretBurstWarmupTime,
                turretBurstCooldownTime = turretBurstCooldownTime,
                turretInitialCooldownTime = turretInitialCooldownTime,
                isMechClusterThreat = isMechClusterThreat
            };
        }

        public void EnsureDefaults()
        {
            slotTag = string.IsNullOrWhiteSpace(slotTag) ? DefaultSlotTag : slotTag;
            parentThingDefName = string.IsNullOrWhiteSpace(parentThingDefName)
                ? (useTemplateMode ? GetTemplateBaseName(itemType) : GetFullModeBaseName(itemType))
                : parentThingDefName;
            shaderDefName = string.IsNullOrWhiteSpace(shaderDefName) ? DefaultShaderDefName : shaderDefName;
            extraFields ??= new Dictionary<string, string>();

            tags ??= new List<string>();
            exportGroupKey = exportGroupKey?.Trim() ?? string.Empty;
            abilityDefNames ??= new List<string>();
            thingCategories ??= new List<string>();
            bodyPartGroups ??= new List<string>();
            apparelLayers ??= new List<string>();
            apparelTags ??= new List<string>();
            tradeTags ??= new List<string>();
            weaponTags ??= new List<string>();
            weaponClasses ??= new List<string>();
            statBases ??= new List<CharacterEquipmentStatEntry>();
            equippedStatOffsets ??= new List<CharacterEquipmentStatEntry>();
            recipeIngredients ??= new List<CharacterEquipmentCostEntry>();
            renderData ??= CharacterEquipmentRenderData.CreateDefault();
            flyerThingDefName ??= string.Empty;
            recipeDefName ??= string.Empty;
            recipeWorkbenchDefName = string.IsNullOrWhiteSpace(recipeWorkbenchDefName) ? "TableMachining" : recipeWorkbenchDefName;
            recipeWorkAmount = Math.Max(1f, recipeWorkAmount);
            recipeProductCount = Math.Max(1, recipeProductCount);
            marketValue = Math.Max(0.01f, marketValue);
            flyerClassName = string.IsNullOrWhiteSpace(flyerClassName)
                ? "CharacterStudio.Abilities.CharacterStudioPawnFlyer_Default"
                : flyerClassName.Trim();
            flyerFlightSpeed = Math.Max(0.05f, flyerFlightSpeed);

            if (string.IsNullOrWhiteSpace(thingDefName))
            {
                thingDefName = defName;
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

            NormalizeCostEntries(recipeIngredients);

            renderData.EnsureDefaults(GetDisplayLabel(), wornTexPath, maskTexPath, shaderDefName);
            NormalizeStatEntries(statBases);
            NormalizeStatEntries(equippedStatOffsets);

            // ThingDef 完整字段默认值
            stuffCategories ??= new List<string>();
            costList ??= new List<CharacterEquipmentCostEntry>();
            NormalizeCostEntries(costList);
            recipeSkillRequirements ??= new List<CharacterEquipmentStatEntry>();
            NormalizeStatEntries(recipeSkillRequirements);
            recipeUsers ??= new List<string>();
            apparelRenderSkipFlags ??= new List<string>();
            rawXmlEntries ??= new List<RawXmlEntry>();
            for (int i = rawXmlEntries.Count - 1; i >= 0; i--)
            {
                if (rawXmlEntries[i] == null || string.IsNullOrWhiteSpace(rawXmlEntries[i].tagName))
                    rawXmlEntries.RemoveAt(i);
            }
            thingClass ??= string.Empty;
            techLevel ??= string.Empty;
            altitudeLayer ??= string.Empty;
            tickerType ??= string.Empty;
            recipeResearchPrerequisite ??= string.Empty;
            recipeEffectWorking ??= string.Empty;
            recipeSoundWorking ??= string.Empty;
            recipeUnfinishedThingDef ??= string.Empty;
            recipeWorkSkill ??= string.Empty;
            recipeWorkSpeedStat ??= string.Empty;
            soundWear ??= string.Empty;
            soundRemove ??= string.Empty;
            developmentalStageFilter ??= string.Empty;
            graphicDrawSize ??= string.Empty;
            apparelDrawDataXml ??= string.Empty;
            rawOriginalThingDefXml ??= string.Empty;

            // 建筑专有字段默认值
            buildingSize ??= string.Empty;
            passability ??= string.Empty;
            terrainAffordanceNeeded ??= string.Empty;
            drawerType ??= string.Empty;
            interactionCellOffset ??= string.Empty;
            designationCategory ??= string.Empty;
            destroySound ??= string.Empty;
            killedLeavings ??= new List<CharacterEquipmentCostEntry>();
            NormalizeCostEntries(killedLeavings);
            damageMultipliers ??= new List<CharacterEquipmentDamageMultiplierEntry>();
            for (int i = damageMultipliers.Count - 1; i >= 0; i--)
            {
                if (damageMultipliers[i] == null || string.IsNullOrWhiteSpace(damageMultipliers[i].damageDefName))
                    damageMultipliers.RemoveAt(i);
            }
            buildingTags ??= new List<string>();
            // 炮塔专有字段默认值
            turretGunDef ??= string.Empty;
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

        private static List<CharacterEquipmentCostEntry> CloneCostEntries(List<CharacterEquipmentCostEntry>? entries)
        {
            var result = new List<CharacterEquipmentCostEntry>();
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

        private static void NormalizeCostEntries(List<CharacterEquipmentCostEntry>? entries)
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

                entry.thingDefName = entry.thingDefName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(entry.thingDefName) || entry.count <= 0)
                {
                    entries.RemoveAt(i);
                    continue;
                }

                entry.count = Math.Max(1, entry.count);
            }
        }
    }

    public class CharacterEquipmentCostEntry
    {
        public string thingDefName = "";
        public int count = 1;

        public CharacterEquipmentCostEntry Clone()
        {
            return new CharacterEquipmentCostEntry
            {
                thingDefName = thingDefName,
                count = count
            };
        }
    }

    public class CharacterEquipmentRenderData
    {
        [XmlExportField] public string layerName = "Equipment";
        [XmlExportField] public string texPath = "";
        [XmlExportField(SkipEmptyString = true)] public string maskTexPath = "";
        [XmlExportField] public string anchorTag = "Apparel";
        [XmlExportField(SkipEmptyString = true)] public string anchorPath = "";
        [XmlExportField] public string shaderDefName = "Cutout";
        [XmlExportField(SkipEmptyString = true)] public string directionalFacing = "";
        [XmlExportField] public Vector3 offset = Vector3.zero;
        [XmlExportField] public Vector3 offsetEast = Vector3.zero;
        [XmlExportField] public Vector3 offsetNorth = Vector3.zero;
        [XmlExportField(BoolToLower = true)] public bool useWestOffset = false;
        [XmlExportField] public Vector3 offsetWest = Vector3.zero;
        [XmlExportField] public Vector2 scale = Vector2.one;
        [XmlExportField] public Vector2 scaleEastMultiplier = Vector2.one;
        [XmlExportField] public Vector2 scaleNorthMultiplier = Vector2.one;
        [XmlExportField] public Vector2 scaleWestMultiplier = Vector2.one;
        [XmlExportField] public float rotation = 0f;
        [XmlExportField(SkipDefault = 0f, SkipDefaultFloat = true)] public float rotationEastOffset = 0f;
        [XmlExportField(SkipDefault = 0f, SkipDefaultFloat = true)] public float rotationNorthOffset = 0f;
        [XmlExportField(SkipDefault = 0f, SkipDefaultFloat = true)] public float rotationWestOffset = 0f;
        [XmlExportField] public float drawOrder = 50f;
        [XmlExportField(BoolToLower = true)] public bool flipHorizontal = false;
        [XmlExportField(BoolToLower = true)] public bool visible = true;
        [XmlExportField] public LayerColorSource colorSource = LayerColorSource.White;
        [XmlExportField(Ignore = true)] public Color customColor = Color.white;
        [XmlExportField] public LayerColorSource colorTwoSource = LayerColorSource.White;
        [XmlExportField(Ignore = true)] public Color customColorTwo = Color.white;

        // 技能驱动的闭环局部动画（装备侧）
        [XmlExportField(BoolToLower = true)] public bool useTriggeredLocalAnimation = false;
        [XmlExportField(SkipEmptyString = true)] public string triggerAbilityDefName = string.Empty;
        [XmlExportField(SkipEmptyString = true)] public string animationGroupKey = string.Empty;
        [XmlExportField] public EquipmentTriggeredAnimationRole triggeredAnimationRole = EquipmentTriggeredAnimationRole.MovablePart;
        [XmlExportField(SkipDefault = 45f, SkipDefaultFloat = true)] public float triggeredDeployAngle = 45f;
        [XmlExportField] public float triggeredReturnAngle = 0f;
        [XmlExportField(SkipDefault = 12)] public int triggeredDeployTicks = 12;
        [XmlExportField(SkipDefault = 24)] public int triggeredHoldTicks = 24;
        [XmlExportField(SkipDefault = 12)] public int triggeredReturnTicks = 12;
        [XmlExportField] public Vector2 triggeredPivotOffset = Vector2.zero;
        [XmlExportField] public Vector3 triggeredDeployOffset = Vector3.zero;
        [XmlExportField(BoolToLower = true)] public bool triggeredUseVfxVisibility = false;

        [XmlExportField(SkipEmptyString = true)] public string triggeredIdleTexPath = string.Empty;
        [XmlExportField(SkipEmptyString = true)] public string triggeredDeployTexPath = string.Empty;
        [XmlExportField(SkipEmptyString = true)] public string triggeredHoldTexPath = string.Empty;
        [XmlExportField(SkipEmptyString = true)] public string triggeredReturnTexPath = string.Empty;

        [XmlExportField(SkipEmptyString = true)] public string triggeredIdleMaskTexPath = string.Empty;
        [XmlExportField(SkipEmptyString = true)] public string triggeredDeployMaskTexPath = string.Empty;
        [XmlExportField(SkipEmptyString = true)] public string triggeredHoldMaskTexPath = string.Empty;
        [XmlExportField(SkipEmptyString = true)] public string triggeredReturnMaskTexPath = string.Empty;

        [XmlExportField] public bool triggeredVisibleDuringDeploy = true;
        [XmlExportField] public bool triggeredVisibleDuringHold = true;
        [XmlExportField] public bool triggeredVisibleDuringReturn = true;
        [XmlExportField] public bool triggeredVisibleOutsideCycle = true;

        [XmlExportField] public EquipmentTriggeredAnimationOverride? triggeredAnimationSouth;
        [XmlExportField] public EquipmentTriggeredAnimationOverride? triggeredAnimationEastWest;
        [XmlExportField] public EquipmentTriggeredAnimationOverride? triggeredAnimationNorth;

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
                directionalFacing = directionalFacing,
                offset = offset,
                offsetEast = offsetEast,
                offsetNorth = offsetNorth,
                useWestOffset = useWestOffset,
                offsetWest = offsetWest,
                scale = scale,
                scaleEastMultiplier = scaleEastMultiplier,
                scaleNorthMultiplier = scaleNorthMultiplier,
                scaleWestMultiplier = scaleWestMultiplier,
                rotation = rotation,
                rotationEastOffset = rotationEastOffset,
                rotationNorthOffset = rotationNorthOffset,
                rotationWestOffset = rotationWestOffset,
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
                triggeredDeployOffset = triggeredDeployOffset,
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
                triggeredVisibleOutsideCycle = triggeredVisibleOutsideCycle,
                triggeredAnimationSouth = triggeredAnimationSouth?.Clone(),
                triggeredAnimationEastWest = triggeredAnimationEastWest?.Clone(),
                triggeredAnimationNorth = triggeredAnimationNorth?.Clone()
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
            triggeredAnimationSouth?.EnsureDefaults(layerName, triggerAbilityDefName, animationGroupKey);
            triggeredAnimationEastWest?.EnsureDefaults(layerName, triggerAbilityDefName, animationGroupKey);
            triggeredAnimationNorth?.EnsureDefaults(layerName, triggerAbilityDefName, animationGroupKey);
        }

        public string GetResolvedTexPath()
        {
            return texPath ?? string.Empty;
        }

        /// <summary>
        /// 将装备渲染数据转换为渲染管线可用的 PawnLayerConfig。
        /// 用于编辑器预览和运行时图层注入。
        /// </summary>
        public PawnLayerConfig ToPawnLayerConfig()
        {
            return new PawnLayerConfig
            {
                layerName = string.IsNullOrWhiteSpace(layerName) ? "Equipment" : layerName,
                texPath = texPath ?? string.Empty,
                maskTexPath = maskTexPath ?? string.Empty,
                anchorTag = string.IsNullOrWhiteSpace(anchorTag) ? "Apparel" : anchorTag,
                anchorPath = anchorPath ?? string.Empty,
                shaderDefName = string.IsNullOrWhiteSpace(shaderDefName) ? "Cutout" : shaderDefName,
                directionalFacing = directionalFacing ?? string.Empty,
                offset = offset,
                offsetEast = offsetEast,
                offsetNorth = offsetNorth,
                useWestOffset = useWestOffset,
                offsetWest = offsetWest,
                scale = scale,
                scaleEastMultiplier = scaleEastMultiplier,
                scaleNorthMultiplier = scaleNorthMultiplier,
                scaleWestMultiplier = scaleWestMultiplier,
                rotation = rotation,
                rotationEastOffset = rotationEastOffset,
                rotationNorthOffset = rotationNorthOffset,
                rotationWestOffset = rotationWestOffset,
                drawOrder = drawOrder,
                flipHorizontal = flipHorizontal,
                visible = visible,
                colorSource = colorSource,
                customColor = customColor,
                colorTwoSource = colorTwoSource,
                customColorTwo = customColorTwo,
                useTriggeredEquipmentAnimation = useTriggeredLocalAnimation,
                triggerAbilityDefName = triggerAbilityDefName ?? string.Empty,
                triggeredAnimationGroupKey = animationGroupKey ?? string.Empty,
                triggeredAnimationRole = triggeredAnimationRole,
                triggeredDeployAngle = triggeredDeployAngle,
                triggeredReturnAngle = triggeredReturnAngle,
                triggeredDeployTicks = triggeredDeployTicks,
                triggeredHoldTicks = triggeredHoldTicks,
                triggeredReturnTicks = triggeredReturnTicks,
                triggeredPivotOffset = triggeredPivotOffset,
                triggeredDeployOffset = triggeredDeployOffset,
                triggeredUseVfxVisibility = triggeredUseVfxVisibility,
                triggeredIdleTexPath = triggeredIdleTexPath ?? string.Empty,
                triggeredDeployTexPath = triggeredDeployTexPath ?? string.Empty,
                triggeredHoldTexPath = triggeredHoldTexPath ?? string.Empty,
                triggeredReturnTexPath = triggeredReturnTexPath ?? string.Empty,
                triggeredIdleMaskTexPath = triggeredIdleMaskTexPath ?? string.Empty,
                triggeredDeployMaskTexPath = triggeredDeployMaskTexPath ?? string.Empty,
                triggeredHoldMaskTexPath = triggeredHoldMaskTexPath ?? string.Empty,
                triggeredReturnMaskTexPath = triggeredReturnMaskTexPath ?? string.Empty,
                triggeredVisibleDuringDeploy = triggeredVisibleDuringDeploy,
                triggeredVisibleDuringHold = triggeredVisibleDuringHold,
                triggeredVisibleDuringReturn = triggeredVisibleDuringReturn,
                triggeredVisibleOutsideCycle = triggeredVisibleOutsideCycle,
                triggeredAnimationSouth = triggeredAnimationSouth?.Clone(),
                triggeredAnimationEastWest = triggeredAnimationEastWest?.Clone(),
                triggeredAnimationNorth = triggeredAnimationNorth?.Clone()
            };
        }
    }

    public enum EquipmentTriggeredAnimationRole
    {
        MovablePart,
        EffectLayer,
        Vfx = EffectLayer
    }

    public class EquipmentTriggeredAnimationOverride
    {
        public bool useTriggeredLocalAnimation = true;
        public string triggerAbilityDefName = string.Empty;
        public string animationGroupKey = string.Empty;
        public EquipmentTriggeredAnimationRole triggeredAnimationRole = EquipmentTriggeredAnimationRole.MovablePart;
        public float triggeredDeployAngle = 45f;
        public float triggeredReturnAngle = 0f;
        public int triggeredDeployTicks = 12;
        public int triggeredHoldTicks = 24;
        public int triggeredReturnTicks = 12;
        public Vector2 triggeredPivotOffset = Vector2.zero;
        public Vector3 triggeredDeployOffset = Vector3.zero;
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

        public EquipmentTriggeredAnimationOverride Clone()
        {
            return new EquipmentTriggeredAnimationOverride
            {
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
                triggeredDeployOffset = triggeredDeployOffset,
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

        public void EnsureDefaults(string fallbackLayerName, string fallbackAbilityDefName, string fallbackAnimationGroupKey)
        {
            triggerAbilityDefName ??= string.Empty;
            if (string.IsNullOrWhiteSpace(triggerAbilityDefName))
            {
                triggerAbilityDefName = fallbackAbilityDefName ?? string.Empty;
            }

            animationGroupKey = string.IsNullOrWhiteSpace(animationGroupKey)
                ? (string.IsNullOrWhiteSpace(fallbackAnimationGroupKey) ? fallbackLayerName : fallbackAnimationGroupKey)
                : animationGroupKey;
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

    /// <summary>
    /// 原始 XML 条目，用于 comps/verbs/tools 等 RimWorld 原生但无法在编辑器中类型化的 ThingDef 子节点。
    /// </summary>
    public class CharacterEquipmentDamageMultiplierEntry
    {
        /// <summary>伤害类型 defName（如 Flame、Bomb、Thump）。</summary>
        public string damageDefName = "";

        /// <summary>伤害倍率（0 = 免疫，1 = 正常，>1 = 增伤）。</summary>
        public float multiplier = 1f;

        public CharacterEquipmentDamageMultiplierEntry Clone()
        {
            return new CharacterEquipmentDamageMultiplierEntry
            {
                damageDefName = damageDefName,
                multiplier = multiplier
            };
        }
    }

    /// <summary>
    /// 武器 tools 条目，用于结构化编辑近战/远程武器的攻击动作。
    /// </summary>
    public class WeaponToolEntry
    {
        public string label = "";
        /// <summary>攻击类型列表，逗号分隔 (Cut/Blunt/Stab/Poke 等)</summary>
        public string capacities = "";
        public float power = 0f;
        public float cooldownTime = 0f;
        public float armorPenetration = -1f;
        /// <summary>可选，-1 表示不输出</summary>
        public float chanceFactor = -1f;
        public bool labelUsedInLogging = true;

        public WeaponToolEntry Clone()
        {
            return new WeaponToolEntry
            {
                label = label,
                capacities = capacities,
                power = power,
                cooldownTime = cooldownTime,
                armorPenetration = armorPenetration,
                chanceFactor = chanceFactor,
                labelUsedInLogging = labelUsedInLogging
            };
        }

        /// <summary>从 XML 字符串解析 tools 列表</summary>
        public static List<WeaponToolEntry> ParseFromXml(string innerXml)
        {
            var result = new List<WeaponToolEntry>();
            if (string.IsNullOrWhiteSpace(innerXml)) return result;
            try
            {
                var root = System.Xml.Linq.XElement.Parse($"<root>{innerXml}</root>");
                foreach (var li in root.Elements("li"))
                {
                    var entry = new WeaponToolEntry();
                    entry.label = (string)li.Element("label") ?? "";
                    var capsEl = li.Element("capacities");
                    if (capsEl != null)
                        entry.capacities = string.Join(", ", capsEl.Elements("li").Select(e => e.Value));
                    entry.power = (float?)li.Element("power") ?? 0f;
                    entry.cooldownTime = (float?)li.Element("cooldownTime") ?? 0f;
                    entry.armorPenetration = (float?)li.Element("armorPenetration") ?? -1f;
                    entry.chanceFactor = (float?)li.Element("chanceFactor") ?? -1f;
                    entry.labelUsedInLogging = (bool?)li.Element("labelUsedInLogging") ?? true;
                    result.Add(entry);
                }
            }
            catch { }
            return result;
        }

        /// <summary>将 tools 列表序列化为 XML innerXml 字符串</summary>
        public static string SerializeToXml(List<WeaponToolEntry> tools)
        {
            if (tools == null || tools.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var t in tools)
            {
                sb.AppendLine("      <li>");
                if (!string.IsNullOrWhiteSpace(t.label))
                    sb.AppendLine($"        <label>{t.label}</label>");
                if (!t.labelUsedInLogging)
                    sb.AppendLine("        <labelUsedInLogging>false</labelUsedInLogging>");
                if (!string.IsNullOrWhiteSpace(t.capacities))
                {
                    sb.AppendLine("        <capacities>");
                    foreach (var cap in t.capacities.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = cap.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            sb.AppendLine($"          <li>{trimmed}</li>");
                    }
                    sb.AppendLine("        </capacities>");
                }
                if (t.power > 0f)
                    sb.AppendLine($"        <power>{t.power}</power>");
                if (t.cooldownTime > 0f)
                    sb.AppendLine($"        <cooldownTime>{t.cooldownTime}</cooldownTime>");
                if (t.armorPenetration >= 0f)
                    sb.AppendLine($"        <armorPenetration>{t.armorPenetration}</armorPenetration>");
                if (t.chanceFactor >= 0f)
                    sb.AppendLine($"        <chanceFactor>{t.chanceFactor}</chanceFactor>");
                sb.AppendLine("      </li>");
            }
            return sb.ToString().TrimEnd('\r', '\n');
        }
    }

    /// <summary>
    /// 武器 verbs 条目，用于结构化编辑远程武器的射击属性。
    /// </summary>
    public class WeaponVerbEntry
    {
        public string verbClass = "Verb_Shoot";
        public bool hasStandardCommand = true;
        /// <summary>默认弹射物 ThingDef defName</summary>
        public string defaultProjectile = "";
        public float warmupTime = 0f;
        public float range = 0f;
        /// <summary>射击音效 SoundDef defName</summary>
        public string soundCast = "";
        /// <summary>射击尾音 SoundDef defName</summary>
        public string soundCastTail = "";
        public float muzzleFlashScale = 9f;
        public int burstShotCount = 0;
        public float burstShotDelay = 0f;

        public WeaponVerbEntry Clone()
        {
            return new WeaponVerbEntry
            {
                verbClass = verbClass,
                hasStandardCommand = hasStandardCommand,
                defaultProjectile = defaultProjectile,
                warmupTime = warmupTime,
                range = range,
                soundCast = soundCast,
                soundCastTail = soundCastTail,
                muzzleFlashScale = muzzleFlashScale,
                burstShotCount = burstShotCount,
                burstShotDelay = burstShotDelay
            };
        }

        public static List<WeaponVerbEntry> ParseFromXml(string innerXml)
        {
            var result = new List<WeaponVerbEntry>();
            if (string.IsNullOrWhiteSpace(innerXml)) return result;
            try
            {
                var root = System.Xml.Linq.XElement.Parse($"<root>{innerXml}</root>");
                foreach (var li in root.Elements("li"))
                {
                    var entry = new WeaponVerbEntry();
                    entry.verbClass = (string)li.Element("verbClass") ?? "Verb_Shoot";
                    entry.hasStandardCommand = (bool?)li.Element("hasStandardCommand") ?? true;
                    entry.defaultProjectile = (string)li.Element("defaultProjectile") ?? "";
                    entry.warmupTime = (float?)li.Element("warmupTime") ?? 0f;
                    entry.range = (float?)li.Element("range") ?? 0f;
                    entry.soundCast = (string)li.Element("soundCast") ?? "";
                    entry.soundCastTail = (string)li.Element("soundCastTail") ?? "";
                    entry.muzzleFlashScale = (float?)li.Element("muzzleFlashScale") ?? 9f;
                    entry.burstShotCount = (int?)li.Element("burstShotCount") ?? 0;
                    entry.burstShotDelay = (float?)li.Element("burstShotDelay") ?? 0f;
                    result.Add(entry);
                }
            }
            catch { }
            return result;
        }

        public static string SerializeToXml(List<WeaponVerbEntry> verbs)
        {
            if (verbs == null || verbs.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var v in verbs)
            {
                sb.AppendLine("      <li>");
                if (!string.IsNullOrWhiteSpace(v.verbClass))
                    sb.AppendLine($"        <verbClass>{v.verbClass}</verbClass>");
                if (!v.hasStandardCommand)
                    sb.AppendLine("        <hasStandardCommand>false</hasStandardCommand>");
                if (!string.IsNullOrWhiteSpace(v.defaultProjectile))
                    sb.AppendLine($"        <defaultProjectile>{v.defaultProjectile}</defaultProjectile>");
                if (v.warmupTime > 0f)
                    sb.AppendLine($"        <warmupTime>{v.warmupTime}</warmupTime>");
                if (v.range > 0f)
                    sb.AppendLine($"        <range>{v.range}</range>");
                if (!string.IsNullOrWhiteSpace(v.soundCast))
                    sb.AppendLine($"        <soundCast>{v.soundCast}</soundCast>");
                if (!string.IsNullOrWhiteSpace(v.soundCastTail))
                    sb.AppendLine($"        <soundCastTail>{v.soundCastTail}</soundCastTail>");
                if (Math.Abs(v.muzzleFlashScale - 9f) > 0.01f)
                    sb.AppendLine($"        <muzzleFlashScale>{v.muzzleFlashScale}</muzzleFlashScale>");
                if (v.burstShotCount > 0)
                    sb.AppendLine($"        <burstShotCount>{v.burstShotCount}</burstShotCount>");
                if (v.burstShotDelay > 0f)
                    sb.AppendLine($"        <burstShotDelay>{v.burstShotDelay}</burstShotDelay>");
                sb.AppendLine("      </li>");
            }
            return sb.ToString().TrimEnd('\r', '\n');
        }
    }

    public class RawXmlEntry
    {
        /// <summary>输出 XML 节点名（如 "comps"、"verbs"、"tools"）。</summary>
        public string tagName = "";

        /// <summary>节点内部的原始 XML 文本。</summary>
        public string innerXml = "";

        public RawXmlEntry Clone()
        {
            return new RawXmlEntry
            {
                tagName = tagName,
                innerXml = innerXml
            };
        }
    }
}
