using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Items;
using UnityEngine;

namespace CharacterStudio.Exporter
{
    public static partial class ModExportXmlWriter
    {
        public static XElement? GenerateEquipmentsXml(List<CharacterEquipmentDef>? equipments)
        {
            if (equipments == null || equipments.Count == 0)
            {
                return null;
            }

            var element = new XElement("equipments");
            foreach (var equipment in equipments)
            {
                if (equipment == null)
                {
                    continue;
                }

                equipment.EnsureDefaults();

                var equipmentEl = new XElement("li", AbilityXmlSerialization.SerializePublicFields(equipment));
                element.Add(equipmentEl);
            }

            return element.HasElements ? element : null;
        }

        private static XElement? GenerateEquipmentStatEntriesXml(string tagName, List<CharacterEquipmentStatEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var root = new XElement(tagName);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.statDefName))
                {
                    continue;
                }

                root.Add(new XElement("li",
                    new XElement("statDefName", entry.statDefName),
                    new XElement("value", entry.value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                ));
            }

            return root.HasElements ? root : null;
        }

        private static XElement? GenerateEquipmentCostEntriesXml(string tagName, List<CharacterEquipmentCostEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var root = new XElement(tagName);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.thingDefName) || entry.count <= 0)
                {
                    continue;
                }

                root.Add(new XElement("li",
                    new XElement("thingDefName", entry.thingDefName),
                    new XElement("count", entry.count)
                ));
            }

            return root.HasElements ? root : null;
        }

        private static XElement? GenerateEquipmentRenderDataXml(CharacterEquipmentRenderData? renderData)
        {
            if (renderData == null)
            {
                return null;
            }

            var elements = AbilityXmlSerialization.SerializePublicFields(renderData);
            if (elements == null || elements.Length == 0)
            {
                return null;
            }

            return new XElement("renderData", elements);
        }

        private static XElement? GenerateEquipmentTriggeredAnimationOverrideXml(string tagName, EquipmentTriggeredAnimationOverride? animation)
        {
            if (animation == null)
            {
                return null;
            }

            animation.EnsureDefaults(string.Empty, string.Empty, string.Empty);

            return new XElement(tagName,
                new XElement("useTriggeredLocalAnimation", animation.useTriggeredLocalAnimation.ToString().ToLower()),
                !string.IsNullOrWhiteSpace(animation.triggerAbilityDefName) ? new XElement("triggerAbilityDefName", animation.triggerAbilityDefName) : null,
                !string.IsNullOrWhiteSpace(animation.animationGroupKey) ? new XElement("animationGroupKey", animation.animationGroupKey) : null,
                new XElement("triggeredAnimationRole", animation.triggeredAnimationRole.ToString()),
                new XElement("triggeredDeployAngle", animation.triggeredDeployAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                animation.triggeredReturnAngle != 0f ? new XElement("triggeredReturnAngle", animation.triggeredReturnAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                new XElement("triggeredDeployTicks", animation.triggeredDeployTicks),
                new XElement("triggeredHoldTicks", animation.triggeredHoldTicks),
                new XElement("triggeredReturnTicks", animation.triggeredReturnTicks),
                animation.triggeredPivotOffset != Vector2.zero ? new XElement("triggeredPivotOffset", FormatVector2(animation.triggeredPivotOffset)) : null,
                new XElement("triggeredUseVfxVisibility", animation.triggeredUseVfxVisibility.ToString().ToLower()),
                !string.IsNullOrWhiteSpace(animation.triggeredIdleTexPath) ? new XElement("triggeredIdleTexPath", animation.triggeredIdleTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredDeployTexPath) ? new XElement("triggeredDeployTexPath", animation.triggeredDeployTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredHoldTexPath) ? new XElement("triggeredHoldTexPath", animation.triggeredHoldTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredReturnTexPath) ? new XElement("triggeredReturnTexPath", animation.triggeredReturnTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredIdleMaskTexPath) ? new XElement("triggeredIdleMaskTexPath", animation.triggeredIdleMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredDeployMaskTexPath) ? new XElement("triggeredDeployMaskTexPath", animation.triggeredDeployMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredHoldMaskTexPath) ? new XElement("triggeredHoldMaskTexPath", animation.triggeredHoldMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(animation.triggeredReturnMaskTexPath) ? new XElement("triggeredReturnMaskTexPath", animation.triggeredReturnMaskTexPath) : null,
                new XElement("triggeredVisibleDuringDeploy", animation.triggeredVisibleDuringDeploy.ToString().ToLower()),
                new XElement("triggeredVisibleDuringHold", animation.triggeredVisibleDuringHold.ToString().ToLower()),
                new XElement("triggeredVisibleDuringReturn", animation.triggeredVisibleDuringReturn.ToString().ToLower()),
                new XElement("triggeredVisibleOutsideCycle", animation.triggeredVisibleOutsideCycle.ToString().ToLower())
            );
        }
        public static XDocument CreateEquipmentThingDefsDocument(List<CharacterEquipmentDef>? equipments, bool includeModExtensions = false)
        {
            var defsRoot = new XElement("Defs");

            if (equipments != null)
            {
                foreach (var equipment in equipments)
                {
                    var equipmentEl = GenerateEquipmentThingDefXml(equipment, includeModExtensions);
                    if (equipmentEl != null)
                    {
                        defsRoot.Add(equipmentEl);
                    }

                    XElement? flyerDef = GenerateEquipmentFlyerThingDefXml(equipment);
                    if (flyerDef != null)
                    {
                        defsRoot.Add(flyerDef);
                    }
                }
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                defsRoot
            );
        }

        public static XElement? GenerateEquipmentThingDefXml(CharacterEquipmentDef? equipment, bool includeModExtensions = false)
        {
            if (equipment == null)
            {
                return null;
            }

            equipment.EnsureDefaults();

            // 非破坏性导出：有原始 ThingDef XML 时，在原始 XML 上做 patch
            if (!string.IsNullOrWhiteSpace(equipment.rawOriginalThingDefXml))
            {
                try
                {
                    XElement? patched = MergeWithOriginalXml(equipment, includeModExtensions);
                    if (patched != null)
                        return patched;
                    // patch 失败则回退到常规路径
                    UnityEngine.Debug.LogWarning("[CharacterStudio] 非破坏性装备导出失败，回退到常规导出");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[CharacterStudio] 非破坏性装备导出异常，回退到常规导出: {ex.Message}");
                }
            }

            string resolvedThingDefName = equipment.GetResolvedThingDefName();
            if (string.IsNullOrWhiteSpace(resolvedThingDefName))
            {
                return null;
            }

            var I = System.Globalization.CultureInfo.InvariantCulture;

            var content = new List<object?>
            {
                new XElement("defName", resolvedThingDefName),
                new XElement("label", equipment.GetDisplayLabel()),
                !string.IsNullOrWhiteSpace(equipment.description) ? new XElement("description", equipment.description) : null,
                // tradeability（原版 ThingDef 合法字段）
                !equipment.allowTrading ? new XElement("tradeability", "None") : null,
                // thingClass
                !string.IsNullOrWhiteSpace(equipment.thingClass) ? new XElement("thingClass", equipment.thingClass) : null,
                // techLevel
                !string.IsNullOrWhiteSpace(equipment.techLevel) ? new XElement("techLevel", equipment.techLevel) : null,
                // 顶层布尔/数值字段（非默认才输出）
                equipment.pathCost > 0 ? new XElement("pathCost", equipment.pathCost.ToString(I)) : null,
                !equipment.useHitPoints ? new XElement("useHitPoints", "false") : null,
                !string.IsNullOrWhiteSpace(equipment.altitudeLayer) ? new XElement("altitudeLayer", equipment.altitudeLayer) : null,
                !string.IsNullOrWhiteSpace(equipment.tickerType) ? new XElement("tickerType", equipment.tickerType) : null,
                equipment.smeltable ? new XElement("smeltable", "true") : null,
                equipment.rotatable ? new XElement("rotatable", "true") : null,
                !equipment.selectable ? new XElement("selectable", "false") : null,
                !equipment.drawGUIOverlay ? new XElement("drawGUIOverlay", "false") : null,
                !equipment.alwaysHaulable ? new XElement("alwaysHaulable", "false") : null,
                // graphicData
                GenerateEquipmentGraphicDataXml(equipment),
                // stuffCategories + costStuffCount
                GenerateStringListXml("stuffCategories", equipment.stuffCategories),
                equipment.costStuffCount > 0 ? new XElement("costStuffCount", equipment.costStuffCount.ToString(I)) : null,
                // costList (ThingDef 级直接材料消耗)
                GenerateEquipmentCostListXml(equipment.costList),
                // 分类与标签
                GenerateEquipmentThingCategoriesXml(equipment.thingCategories),
                GenerateEquipmentTradeTagsXml(equipment.tradeTags),
                equipment.itemType == EquipmentType.WeaponMelee || equipment.itemType == EquipmentType.WeaponRanged ? GenerateWeaponTagsXml(equipment.weaponTags) : null,
                equipment.itemType == EquipmentType.WeaponMelee || equipment.itemType == EquipmentType.WeaponRanged ? GenerateWeaponClassesXml(equipment.weaponClasses) : null,
                // statBases
                GenerateEquipmentStatEntryContainerXml("statBases", equipment.statBases),
                // equippedStatOffsets
                GenerateEquipmentStatEntryContainerXml("equippedStatOffsets", equipment.equippedStatOffsets),
                // apparel 节点
                equipment.itemType == EquipmentType.Apparel ? GenerateEquipmentApparelXml(equipment) : null,
                // building 节点（Building / Turret）
                equipment.itemType == EquipmentType.Building || equipment.itemType == EquipmentType.Turret ? GenerateEquipmentBuildingXml(equipment) : null,
                // 建筑/炮塔专有顶层字段（展开列表）
            };

            // 建筑专有顶层字段展开
            if (equipment.itemType == EquipmentType.Building || equipment.itemType == EquipmentType.Turret)
            {
                var buildingFields = GenerateBuildingTopLevelFieldsXml(equipment);
                content.AddRange(buildingFields);
            }

            // recipeMaker 内联节点
            content.Add(GenerateEquipmentRecipeMakerXml(equipment));

            var thingDef = new XElement("ThingDef",
                new XAttribute("ParentName", string.IsNullOrWhiteSpace(equipment.parentThingDefName)
                    ? (equipment.itemType == EquipmentType.Building || equipment.itemType == EquipmentType.Turret ? "BuildingBase" : "ApparelMakeableBase")
                    : equipment.parentThingDefName),
                content.Where(c => c != null).Cast<object>().ToArray());

            // 保证 statBases 存在并注入 MarketValue（仅非建筑类型）
            bool isBuildingType = equipment.itemType == EquipmentType.Building || equipment.itemType == EquipmentType.Turret;
            if (!isBuildingType)
            {
                XElement statBasesEl = thingDef.Element("statBases") ?? new XElement("statBases");
                if (thingDef.Element("statBases") == null)
                    thingDef.Add(statBasesEl);
                if (statBasesEl.Element("MarketValue") == null)
                {
                    statBasesEl.Add(new XElement("MarketValue", equipment.marketValue.ToString(I)));
                }
            }

            // 原始 XML 块（comps/verbs/tools 等）
            if (equipment.rawXmlEntries != null)
            {
                foreach (var entry in equipment.rawXmlEntries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.tagName) || string.IsNullOrWhiteSpace(entry.innerXml))
                        continue;
                    try
                    {
                        var parsed = XElement.Parse($"<{entry.tagName}>{entry.innerXml}</{entry.tagName}>");
                        thingDef.Add(parsed);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[CharacterStudio] 装备导出：rawXmlEntries 的 {entry.tagName} 解析失败: {ex.Message}");
                    }
                }
            }

            // 额外字段（字段补充器添加的 key-value 对）
            if (equipment.extraFields != null && equipment.extraFields.Count > 0)
            {
                foreach (var kv in equipment.extraFields)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                    thingDef.Add(new XElement(kv.Key, kv.Value ?? ""));
                }
            }

            if (includeModExtensions)
            {
                thingDef.Add(GenerateEquipmentModExtensionsXml(equipment));
            }

            return thingDef;
        }

        public static XElement? GenerateEquipmentRecipeDefXml(CharacterEquipmentDef? equipment)
        {
            if (equipment == null)
                return null;

            equipment.EnsureDefaults();
            if (!equipment.allowCrafting)
                return null;

            string resolvedThingDefName = equipment.GetResolvedThingDefName();
            string recipeDefName = string.IsNullOrWhiteSpace(equipment.recipeDefName)
                ? $"Recipe_{resolvedThingDefName}"
                : equipment.recipeDefName;

            var recipeDef = new XElement("RecipeDef",
                new XAttribute("ParentName", "MakeRecipeBase"),
                new XElement("defName", recipeDefName),
                new XElement("label", $"Make {equipment.GetDisplayLabel()}"),
                new XElement("jobString", $"Making {equipment.GetDisplayLabel()}"),
                new XElement("workAmount", equipment.recipeWorkAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("products",
                    new XElement("li",
                        new XElement("thingDef", resolvedThingDefName),
                        new XElement("count", equipment.recipeProductCount)
                    )),
                new XElement("recipeUsers",
                    new XElement("li", string.IsNullOrWhiteSpace(equipment.recipeWorkbenchDefName) ? "TableMachining" : equipment.recipeWorkbenchDefName))
            );

            XElement? ingredients = GenerateEquipmentRecipeIngredientsXml(equipment.recipeIngredients);
            if (ingredients != null)
                recipeDef.Add(ingredients);

            return recipeDef;
        }

        public static XDocument CreateEquipmentRecipeDefsDocument(List<CharacterEquipmentDef>? equipments)
        {
            var defsRoot = new XElement("Defs");

            if (equipments != null)
            {
                foreach (var equipment in equipments)
                {
                    XElement? recipeDef = GenerateEquipmentRecipeDefXml(equipment);
                    if (recipeDef != null)
                    {
                        defsRoot.Add(recipeDef);
                    }
                }
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                defsRoot
            );
        }

        public static XDocument CreateEquipmentBundleManifestDocument(List<CharacterEquipmentDef>? equipments)
        {
            var defsRoot = new XElement("Defs");
            if (equipments == null)
                return new XDocument(new XDeclaration("1.0", "utf-8", null), defsRoot);

            foreach (var group in equipments
                .Where(equipment => equipment != null && equipment.enabled && !string.IsNullOrWhiteSpace(equipment.exportGroupKey))
                .GroupBy(equipment => equipment.exportGroupKey, StringComparer.OrdinalIgnoreCase))
            {
                var manifest = new XElement("CharacterStudioEquipmentBundleDef",
                    new XElement("defName", $"CS_EquipBundle_{group.Key}"),
                    new XElement("label", group.Key),
                    new XElement("equipmentThingDefs",
                        group.Select(item => new XElement("li", item.GetResolvedThingDefName()))),
                    new XElement("startingApparelDefNames",
                        group.Select(item => new XElement("li", item.GetResolvedThingDefName()))));

                defsRoot.Add(manifest);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), defsRoot);
        }

        public static XElement? GenerateEquipmentFlyerThingDefXml(CharacterEquipmentDef? equipment)
        {
            if (equipment == null)
            {
                return null;
            }

            equipment.EnsureDefaults();
            if (string.IsNullOrWhiteSpace(equipment.flyerThingDefName))
            {
                return null;
            }

            string flyerClassName = string.IsNullOrWhiteSpace(equipment.flyerClassName)
                ? "CharacterStudio.Abilities.CharacterStudioPawnFlyer_Default"
                : equipment.flyerClassName;

            return new XElement("ThingDef",
                new XAttribute("ParentName", "PawnFlyerBase"),
                new XElement("defName", equipment.flyerThingDefName),
                new XElement("label", $"{equipment.GetDisplayLabel()} flyer"),
                new XElement("thingClass", flyerClassName),
                new XElement("drawOffscreen", "true"),
                new XElement("drawerType", "RealtimeOnly"),
                new XElement("altitudeLayer", "Pawn"),
                new XElement("pawnFlyer",
                    new XElement("flightDurationMin", "0.35"),
                    new XElement("flightSpeed", equipment.flyerFlightSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement("workerClass", "PawnFlyerWorker"),
                    new XElement("heightFactor", "0")
                )
            );
        }

        private static XElement? GenerateEquipmentGraphicDataXml(CharacterEquipmentDef equipment)
        {
            string texPath = string.IsNullOrWhiteSpace(equipment.worldTexPath)
                ? equipment.renderData?.GetResolvedTexPath() ?? string.Empty
                : equipment.worldTexPath;

            if (string.IsNullOrWhiteSpace(texPath))
            {
                return null;
            }

            bool usesExternalTexture = Rendering.RuntimeAssetLoader.LooksLikeExternalTexturePath(texPath);
            string graphicClass = usesExternalTexture
                ? "CharacterStudio.Rendering.Graphic_Runtime"
                : "Graphic_Single";

            var graphicContent = new List<object?>
            {
                new XElement("texPath", texPath),
                !string.IsNullOrWhiteSpace(equipment.shaderDefName) ? new XElement("shaderType", equipment.shaderDefName) : null,
                new XElement("graphicClass", graphicClass),
                !string.IsNullOrWhiteSpace(equipment.graphicDrawSize) ? new XElement("drawSize", equipment.graphicDrawSize) : null,
                equipment.graphicRandomRotateAngle > 0f ? new XElement("onGroundRandomRotateAngle", equipment.graphicRandomRotateAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
            };

            return new XElement("graphicData", graphicContent.Where(c => c != null).Cast<object>().ToArray());
        }

        private static XElement? GenerateEquipmentThingCategoriesXml(List<string>? categories)
        {
            return GenerateStringListXml("thingCategories", categories);
        }

        private static XElement? GenerateEquipmentTradeTagsXml(List<string>? tradeTags)
        {
            return GenerateStringListXml("tradeTags", tradeTags);
        }

        private static XElement? GenerateWeaponTagsXml(List<string>? weaponTags)
        {
            return GenerateStringListXml("weaponTags", weaponTags);
        }

        private static XElement? GenerateWeaponClassesXml(List<string>? weaponClasses)
        {
            return GenerateStringListXml("weaponClasses", weaponClasses);
        }

        private static XElement? GenerateEquipmentRecipeIngredientsXml(List<CharacterEquipmentCostEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
                return null;

            var ingredients = new XElement("ingredients");
            foreach (CharacterEquipmentCostEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.thingDefName) || entry.count <= 0)
                    continue;

                ingredients.Add(new XElement("li",
                    new XElement("filter",
                        new XElement("thingDefs",
                            new XElement("li", entry.thingDefName))),
                    new XElement("count", entry.count)));
            }

            return ingredients.HasElements ? ingredients : null;
        }

        /// <summary>
        /// 生成建筑顶层字段（size、passability、fillPercent 等 ThingDef 直接字段）。
        /// 空字段不写入。
        /// </summary>
        private static List<object?> GenerateBuildingTopLevelFieldsXml(CharacterEquipmentDef equipment)
        {
            var I = System.Globalization.CultureInfo.InvariantCulture;
            var fields = new List<object?>();

            if (!string.IsNullOrWhiteSpace(equipment.buildingSize))
                fields.Add(new XElement("size", equipment.buildingSize));
            if (!string.IsNullOrWhiteSpace(equipment.passability))
                fields.Add(new XElement("passability", equipment.passability));
            if (equipment.fillPercent > 0f)
                fields.Add(new XElement("fillPercent", equipment.fillPercent.ToString(I)));
            if (!string.IsNullOrWhiteSpace(equipment.terrainAffordanceNeeded))
                fields.Add(new XElement("terrainAffordanceNeeded", equipment.terrainAffordanceNeeded));
            if (equipment.blockWind.HasValue && equipment.blockWind.Value)
                fields.Add(new XElement("blockWind", "true"));
            if (equipment.castEdgeShadows.HasValue && equipment.castEdgeShadows.Value)
                fields.Add(new XElement("castEdgeShadows", "true"));
            if (!string.IsNullOrWhiteSpace(equipment.drawerType))
                fields.Add(new XElement("drawerType", equipment.drawerType));
            if (equipment.canOverlapZones.HasValue && !equipment.canOverlapZones.Value)
                fields.Add(new XElement("canOverlapZones", "false"));
            if (equipment.hasInteractionCell)
                fields.Add(new XElement("hasInteractionCell", "true"));
            if (!string.IsNullOrWhiteSpace(equipment.interactionCellOffset))
                fields.Add(new XElement("interactionCellOffset", equipment.interactionCellOffset));
            if (!string.IsNullOrWhiteSpace(equipment.designationCategory))
                fields.Add(new XElement("designationCategory", equipment.designationCategory));
            if (equipment.isMechClusterThreat)
                fields.Add(new XElement("isMechClusterThreat", "true"));

            // killedLeavings
            if (equipment.killedLeavings != null && equipment.killedLeavings.Count > 0)
            {
                var leavingsEl = new XElement("killedLeavings");
                foreach (var entry in equipment.killedLeavings)
                {
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.thingDefName) && entry.count > 0)
                        leavingsEl.Add(new XElement(entry.thingDefName, entry.count.ToString(I)));
                }
                if (leavingsEl.HasElements)
                    fields.Add(leavingsEl);
            }

            // damageMultipliers
            if (equipment.damageMultipliers != null && equipment.damageMultipliers.Count > 0)
            {
                var dmgEl = new XElement("damageMultipliers");
                foreach (var entry in equipment.damageMultipliers)
                {
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.damageDefName))
                        dmgEl.Add(new XElement("li",
                            new XElement("damageDef", entry.damageDefName),
                            new XElement("multiplier", entry.multiplier.ToString(I))));
                }
                if (dmgEl.HasElements)
                    fields.Add(dmgEl);
            }

            return fields;
        }

        /// <summary>
        /// 生成 building 子节点。空字段不写入。
        /// </summary>
        private static XElement? GenerateEquipmentBuildingXml(CharacterEquipmentDef equipment)
        {
            var I = System.Globalization.CultureInfo.InvariantCulture;
            var children = new List<object?>();

            // buildingTags
            if (equipment.buildingTags != null && equipment.buildingTags.Count > 0)
            {
                var tagsEl = new XElement("buildingTags");
                foreach (var tag in equipment.buildingTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                        tagsEl.Add(new XElement("li", tag));
                }
                if (tagsEl.HasElements)
                    children.Add(tagsEl);
            }

            if (equipment.combatPower > 0f)
                children.Add(new XElement("combatPower", equipment.combatPower.ToString(I)));
            if (equipment.roofCollapseDamageMultiplier > 0f)
                children.Add(new XElement("roofCollapseDamageMultiplier", equipment.roofCollapseDamageMultiplier.ToString(I)));
            if (!string.IsNullOrWhiteSpace(equipment.destroySound))
                children.Add(new XElement("destroySound", equipment.destroySound));

            // 炮塔专有字段
            if (equipment.itemType == EquipmentType.Turret)
            {
                if (!string.IsNullOrWhiteSpace(equipment.turretGunDef))
                    children.Add(new XElement("turretGunDef", equipment.turretGunDef));
                if (equipment.turretBurstWarmupTime > 0f)
                    children.Add(new XElement("turretBurstWarmupTime", equipment.turretBurstWarmupTime.ToString(I)));
                if (equipment.turretBurstCooldownTime > 0f)
                    children.Add(new XElement("turretBurstCooldownTime", equipment.turretBurstCooldownTime.ToString(I)));
                if (equipment.turretInitialCooldownTime > 0f)
                    children.Add(new XElement("turretInitialCooldownTime", equipment.turretInitialCooldownTime.ToString(I)));
            }

            if (children.Count == 0)
                return null;

            return new XElement("building", children.Where(c => c != null).Cast<object>().ToArray());
        }

        private static XElement GenerateEquipmentApparelXml(CharacterEquipmentDef equipment)
        {
            // 如果 wornTexPath 是外部绝对路径（如 D:/...），RimWorld 原生 ContentFinder 无法解析，
            // 会导致穿戴后显示红色 X。此时不输出 wornGraphicPath，
            // 让 vanilla 回退到 graphicData（已使用 Graphic_Runtime 支持外部路径），
            // 同时 CS 自定义图层注入系统通过 DefModExtension_EquipmentRender 接管精确渲染。
            bool wornTexPathIsExternal = !string.IsNullOrWhiteSpace(equipment.wornTexPath)
                && Rendering.RuntimeAssetLoader.LooksLikeExternalTexturePath(equipment.wornTexPath);

            var apparelContent = new List<object?>
            {
                !string.IsNullOrWhiteSpace(equipment.wornTexPath) && !wornTexPathIsExternal
                    ? new XElement("wornGraphicPath", equipment.wornTexPath)
                    : null,
                equipment.useWornGraphicMask ? new XElement("useWornGraphicMask", equipment.useWornGraphicMask.ToString().ToLower()) : null,
                GenerateStringListXml("bodyPartGroups", equipment.bodyPartGroups),
                GenerateStringListXml("layers", equipment.apparelLayers),
                GenerateStringListXml("tags", equipment.apparelTags),
                // 新增 apparel 子字段
                equipment.wearPerDay > 0f ? new XElement("wearPerDay", equipment.wearPerDay.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                equipment.careIfDamaged.HasValue ? new XElement("careIfDamaged", equipment.careIfDamaged.Value.ToString().ToLower()) : null,
                equipment.careIfWornByCorpse.HasValue ? new XElement("careIfWornByCorpse", equipment.careIfWornByCorpse.Value.ToString().ToLower()) : null,
                equipment.countsAsClothingForNudity.HasValue ? new XElement("countsAsClothingForNudity", equipment.countsAsClothingForNudity.Value.ToString().ToLower()) : null,
                equipment.slaveApparel.HasValue && equipment.slaveApparel.Value ? new XElement("slaveApparel", "true") : null,
                equipment.canBeDesiredForIdeo.HasValue ? new XElement("canBeDesiredForIdeo", equipment.canBeDesiredForIdeo.Value.ToString().ToLower()) : null,
                !string.IsNullOrWhiteSpace(equipment.soundWear) ? new XElement("soundWear", equipment.soundWear) : null,
                !string.IsNullOrWhiteSpace(equipment.soundRemove) ? new XElement("soundRemove", equipment.soundRemove) : null,
                equipment.useDeflectMetalEffect ? new XElement("useDeflectMetalEffect", "true") : null,
                equipment.apparelRenderSkipFlags != null && equipment.apparelRenderSkipFlags.Count > 0 ? GenerateStringListXml("renderSkipFlags", equipment.apparelRenderSkipFlags) : null,
                !string.IsNullOrWhiteSpace(equipment.developmentalStageFilter) ? new XElement("developmentalStageFilter", equipment.developmentalStageFilter) : null,
                equipment.blocksVision.HasValue && equipment.blocksVision.Value ? new XElement("blocksVision", "true") : null,
                equipment.ignoredByNonViolent.HasValue && equipment.ignoredByNonViolent.Value ? new XElement("ignoredByNonViolent", "true") : null,
                Math.Abs(equipment.apparelScoreOffset) > 0.0001f ? new XElement("scoreOffset", equipment.apparelScoreOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                // drawData（原版 ApparelDrawData 原始 XML，导入时保留，导出时原样输出）
                !string.IsNullOrWhiteSpace(equipment.apparelDrawDataXml) ? ParseRawXmlBlock("drawData", equipment.apparelDrawDataXml) : null,
            };

            return new XElement("apparel", apparelContent.Where(c => c != null).Cast<object>().ToArray());
        }

        private static XElement? GenerateEquipmentStatEntryContainerXml(string tagName, List<CharacterEquipmentStatEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var element = new XElement(tagName);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.statDefName))
                {
                    continue;
                }

                element.Add(new XElement(entry.statDefName, entry.value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            return element.HasElements ? element : null;
        }

        /// <summary>
        /// 生成 ThingDef 级 costList（如 &lt;Steel&gt;120&lt;/Steel&gt;）。
        /// </summary>
        private static XElement? GenerateEquipmentCostListXml(List<CharacterEquipmentCostEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
                return null;

            var root = new XElement("costList");
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.thingDefName) || entry.count <= 0)
                    continue;
                root.Add(new XElement(entry.thingDefName, entry.count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            return root.HasElements ? root : null;
        }

        /// <summary>
        /// 生成内联 recipeMaker 节点。仅在有至少一个非空子字段时输出。
        /// </summary>
        private static XElement? GenerateEquipmentRecipeMakerXml(CharacterEquipmentDef equipment)
        {
            var I = System.Globalization.CultureInfo.InvariantCulture;
            var children = new List<object?>();

            if (!string.IsNullOrWhiteSpace(equipment.recipeResearchPrerequisite))
                children.Add(new XElement("researchPrerequisite", equipment.recipeResearchPrerequisite));

            if (equipment.recipeSkillRequirements != null && equipment.recipeSkillRequirements.Count > 0)
            {
                var skillReq = new XElement("skillRequirements");
                foreach (var entry in equipment.recipeSkillRequirements)
                {
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.statDefName))
                        skillReq.Add(new XElement(entry.statDefName, entry.value.ToString(I)));
                }
                if (skillReq.HasElements)
                    children.Add(skillReq);
            }

            if (!string.IsNullOrWhiteSpace(equipment.recipeEffectWorking))
                children.Add(new XElement("effectWorking", equipment.recipeEffectWorking));

            if (!string.IsNullOrWhiteSpace(equipment.recipeSoundWorking))
                children.Add(new XElement("soundWorking", equipment.recipeSoundWorking));

            // recipeUsers：优先使用 recipeUsers 列表，否则回退到 recipeWorkbenchDefName
            var users = equipment.recipeUsers != null && equipment.recipeUsers.Count > 0
                ? equipment.recipeUsers
                : (!string.IsNullOrWhiteSpace(equipment.recipeWorkbenchDefName)
                    ? new List<string> { equipment.recipeWorkbenchDefName }
                    : null);
            if (users != null && users.Count > 0)
            {
                var usersEl = new XElement("recipeUsers");
                foreach (var user in users)
                {
                    if (!string.IsNullOrWhiteSpace(user))
                        usersEl.Add(new XElement("li", user));
                }
                if (usersEl.HasElements)
                    children.Add(usersEl);
            }

            if (!string.IsNullOrWhiteSpace(equipment.recipeUnfinishedThingDef))
                children.Add(new XElement("unfinishedThingDef", equipment.recipeUnfinishedThingDef));

            if (equipment.recipeDisplayPriority > 0f)
                children.Add(new XElement("displayPriority", equipment.recipeDisplayPriority.ToString(I)));

            if (!string.IsNullOrWhiteSpace(equipment.recipeWorkSkill))
                children.Add(new XElement("workSkill", equipment.recipeWorkSkill));

            if (!string.IsNullOrWhiteSpace(equipment.recipeWorkSpeedStat))
                children.Add(new XElement("workSpeedStat", equipment.recipeWorkSpeedStat));

            if (children.Count == 0)
                return null;

            return new XElement("recipeMaker", children.Where(c => c != null).Cast<object>().ToArray());
        }

        private static XElement GenerateEquipmentModExtensionsXml(CharacterEquipmentDef equipment)
        {
            var renderExtension = DefModExtension_EquipmentRender.FromEquipment(equipment);
            renderExtension.EnsureDefaults();

            return new XElement("modExtensions",
                new XElement("li",
                    new XAttribute("Class", "CharacterStudio.Core.DefModExtension_EquipmentRender"),
                    new XElement("equipmentDefName", renderExtension.equipmentDefName),
                    !string.IsNullOrWhiteSpace(renderExtension.label) ? new XElement("label", renderExtension.label) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.slotTag) ? new XElement("slotTag", renderExtension.slotTag) : null,
                    new XElement("enabled", renderExtension.enabled.ToString().ToLower()),
                    new XElement("texPath", renderExtension.texPath ?? string.Empty),
                    !string.IsNullOrWhiteSpace(renderExtension.maskTexPath) ? new XElement("maskTexPath", renderExtension.maskTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.directionalFacing) ? new XElement("directionalFacing", renderExtension.directionalFacing) : null,
                    new XElement("anchorTag", string.IsNullOrWhiteSpace(renderExtension.anchorTag) ? "Apparel" : renderExtension.anchorTag),
                    !string.IsNullOrWhiteSpace(renderExtension.anchorPath) ? new XElement("anchorPath", renderExtension.anchorPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.shaderDefName) ? new XElement("shaderDefName", renderExtension.shaderDefName) : null,
                    new XElement("offset", FormatVector3(renderExtension.offset)),
                    renderExtension.offsetEast != Vector3.zero ? new XElement("offsetEast", FormatVector3(renderExtension.offsetEast)) : null,
                    renderExtension.offsetNorth != Vector3.zero ? new XElement("offsetNorth", FormatVector3(renderExtension.offsetNorth)) : null,
                    renderExtension.useWestOffset ? new XElement("useWestOffset", "true") : null,
                    renderExtension.offsetWest != Vector3.zero ? new XElement("offsetWest", FormatVector3(renderExtension.offsetWest)) : null,
                    new XElement("scale", FormatVector2(renderExtension.scale)),
                    renderExtension.scaleEastMultiplier != Vector2.one ? new XElement("scaleEastMultiplier", FormatVector2(renderExtension.scaleEastMultiplier)) : null,
                    renderExtension.scaleNorthMultiplier != Vector2.one ? new XElement("scaleNorthMultiplier", FormatVector2(renderExtension.scaleNorthMultiplier)) : null,
                    renderExtension.scaleWestMultiplier != Vector2.one ? new XElement("scaleWestMultiplier", FormatVector2(renderExtension.scaleWestMultiplier)) : null,
                    new XElement("rotation", renderExtension.rotation),
                    renderExtension.rotationEastOffset != 0f ? new XElement("rotationEastOffset", renderExtension.rotationEastOffset) : null,
                    renderExtension.rotationNorthOffset != 0f ? new XElement("rotationNorthOffset", renderExtension.rotationNorthOffset) : null,
                    renderExtension.rotationWestOffset != 0f ? new XElement("rotationWestOffset", renderExtension.rotationWestOffset) : null,
                    new XElement("drawOrder", renderExtension.drawOrder),
                    new XElement("flipHorizontal", renderExtension.flipHorizontal.ToString().ToLower()),
                    new XElement("visible", renderExtension.visible.ToString().ToLower()),
                    new XElement("colorSource", renderExtension.colorSource.ToString()),
                    renderExtension.colorSource == LayerColorSource.Fixed ? new XElement("customColor", FormatColor(renderExtension.customColor)) : null,
                    new XElement("colorTwoSource", renderExtension.colorTwoSource.ToString()),
                    renderExtension.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", FormatColor(renderExtension.customColorTwo)) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.flyerThingDefName) ? new XElement("flyerThingDefName", renderExtension.flyerThingDefName) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.flyerClassName) ? new XElement("flyerClassName", renderExtension.flyerClassName) : null,
                    Math.Abs(renderExtension.flyerFlightSpeed - 1f) > 0.0001f ? new XElement("flyerFlightSpeed", renderExtension.flyerFlightSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    renderExtension.useTriggeredLocalAnimation ? new XElement("useTriggeredLocalAnimation", renderExtension.useTriggeredLocalAnimation.ToString().ToLower()) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggerAbilityDefName) ? new XElement("triggerAbilityDefName", renderExtension.triggerAbilityDefName) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.animationGroupKey) ? new XElement("animationGroupKey", renderExtension.animationGroupKey) : null,
                    renderExtension.triggeredAnimationRole != EquipmentTriggeredAnimationRole.MovablePart ? new XElement("triggeredAnimationRole", renderExtension.triggeredAnimationRole.ToString()) : null,
                    renderExtension.triggeredDeployAngle != 45f ? new XElement("triggeredDeployAngle", renderExtension.triggeredDeployAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    renderExtension.triggeredReturnAngle != 0f ? new XElement("triggeredReturnAngle", renderExtension.triggeredReturnAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    renderExtension.triggeredDeployTicks != 12 ? new XElement("triggeredDeployTicks", renderExtension.triggeredDeployTicks) : null,
                    renderExtension.triggeredHoldTicks != 24 ? new XElement("triggeredHoldTicks", renderExtension.triggeredHoldTicks) : null,
                    renderExtension.triggeredReturnTicks != 12 ? new XElement("triggeredReturnTicks", renderExtension.triggeredReturnTicks) : null,
                    renderExtension.triggeredPivotOffset != Vector2.zero ? new XElement("triggeredPivotOffset", FormatVector2(renderExtension.triggeredPivotOffset)) : null,
                    renderExtension.triggeredUseVfxVisibility ? new XElement("triggeredUseVfxVisibility", renderExtension.triggeredUseVfxVisibility.ToString().ToLower()) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredIdleTexPath) ? new XElement("triggeredIdleTexPath", renderExtension.triggeredIdleTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredDeployTexPath) ? new XElement("triggeredDeployTexPath", renderExtension.triggeredDeployTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredHoldTexPath) ? new XElement("triggeredHoldTexPath", renderExtension.triggeredHoldTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredReturnTexPath) ? new XElement("triggeredReturnTexPath", renderExtension.triggeredReturnTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredIdleMaskTexPath) ? new XElement("triggeredIdleMaskTexPath", renderExtension.triggeredIdleMaskTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredDeployMaskTexPath) ? new XElement("triggeredDeployMaskTexPath", renderExtension.triggeredDeployMaskTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredHoldMaskTexPath) ? new XElement("triggeredHoldMaskTexPath", renderExtension.triggeredHoldMaskTexPath) : null,
                    !string.IsNullOrWhiteSpace(renderExtension.triggeredReturnMaskTexPath) ? new XElement("triggeredReturnMaskTexPath", renderExtension.triggeredReturnMaskTexPath) : null,
                    !renderExtension.triggeredVisibleDuringDeploy ? new XElement("triggeredVisibleDuringDeploy", renderExtension.triggeredVisibleDuringDeploy.ToString().ToLower()) : null,
                    !renderExtension.triggeredVisibleDuringHold ? new XElement("triggeredVisibleDuringHold", renderExtension.triggeredVisibleDuringHold.ToString().ToLower()) : null,
                    !renderExtension.triggeredVisibleDuringReturn ? new XElement("triggeredVisibleDuringReturn", renderExtension.triggeredVisibleDuringReturn.ToString().ToLower()) : null,
                    !renderExtension.triggeredVisibleOutsideCycle ? new XElement("triggeredVisibleOutsideCycle", renderExtension.triggeredVisibleOutsideCycle.ToString().ToLower()) : null,
                    GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationSouth", renderExtension.triggeredAnimationSouth),
                    GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationEastWest", renderExtension.triggeredAnimationEastWest),
                    GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationNorth", renderExtension.triggeredAnimationNorth),
                    GenerateStringListXml("abilityDefNames", renderExtension.abilityDefNames)
                )
            );
        }

        /// <summary>
        /// 将原始 XML 文本包裹在 tagName 节点中解析为 XElement。
        /// 用于 drawData 等需要原样保留的嵌套 XML 块。
        /// </summary>
        private static XElement? ParseRawXmlBlock(string tagName, string innerXml)
        {
            if (string.IsNullOrWhiteSpace(innerXml))
                return null;
            try
            {
                return XElement.Parse($"<{tagName}>{innerXml}</{tagName}>");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[CharacterStudio] XML 块解析失败 ({tagName}): {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────────
        // 非破坏性导出：在原始 ThingDef XML 上做 patch
        // ─────────────────────────────────────────────

        /// <summary>
        /// 编辑器已知管理的顶层子节点名称。
        /// 这些节点会被编辑器值覆盖；不在此集合中的节点原样保留。
        /// </summary>
        private static readonly HashSet<string> ManagedTopLevelNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "defName", "label", "description", "tradeability",
            "thingClass", "techLevel",
            "pathCost", "useHitPoints", "altitudeLayer", "tickerType",
            "smeltable", "rotatable", "selectable", "drawGUIOverlay", "alwaysHaulable",
            "graphicData", "stuffCategories", "costStuffCount", "costList",
            "thingCategories", "tradeTags", "weaponTags", "weaponClasses",
            "statBases", "equippedStatOffsets",
            "apparel", "recipeMaker", "building",
            "comps", "verbs", "tools",
            "modExtensions",
            // 建筑专有顶层字段
            "size", "passability", "fillPercent", "terrainAffordanceNeeded",
            "blockWind", "castEdgeShadows", "drawerType", "canOverlapZones",
            "hasInteractionCell", "interactionCellOffset",
            "killedLeavings", "damageMultipliers",
            "designationCategory", "isMechClusterThreat",
        };

        /// <summary>
        /// 在原始 ThingDef XML 上做非破坏性 patch：
        /// 已知管理的节点用编辑器当前值替换/新增/删除；
        /// 未知节点（如 alienRace、raceRestriction 等）原样保留。
        /// </summary>
        private static XElement? MergeWithOriginalXml(CharacterEquipmentDef equipment, bool includeModExtensions)
        {
            XElement original = XElement.Parse(equipment.rawOriginalThingDefXml);

            // 如果原始节点名不是 ThingDef，无法 patch
            if (!string.Equals(original.Name.LocalName, "ThingDef", StringComparison.OrdinalIgnoreCase))
                return null;

            var I = System.Globalization.CultureInfo.InvariantCulture;

            // 1. 收集需要替换/新增的已知节点
            var replacementNodes = new Dictionary<string, XElement?>(StringComparer.OrdinalIgnoreCase);
            void SetOrRemove(string name, XElement? node)
            {
                replacementNodes[name] = node;
            }

            string resolvedThingDefName = equipment.GetResolvedThingDefName();

            // defName
            SetOrRemove("defName", new XElement("defName", resolvedThingDefName));
            // label
            SetOrRemove("label", new XElement("label", equipment.GetDisplayLabel()));
            // description
            SetOrRemove("description", !string.IsNullOrWhiteSpace(equipment.description)
                ? new XElement("description", equipment.description) : null);
            // tradeability
            SetOrRemove("tradeability", !equipment.allowTrading ? new XElement("tradeability", "None") : null);
            // thingClass
            SetOrRemove("thingClass", !string.IsNullOrWhiteSpace(equipment.thingClass)
                ? new XElement("thingClass", equipment.thingClass) : null);
            // techLevel
            SetOrRemove("techLevel", !string.IsNullOrWhiteSpace(equipment.techLevel)
                ? new XElement("techLevel", equipment.techLevel) : null);
            // 顶层布尔/数值
            SetOrRemove("pathCost", equipment.pathCost > 0 ? new XElement("pathCost", equipment.pathCost.ToString(I)) : null);
            SetOrRemove("useHitPoints", !equipment.useHitPoints ? new XElement("useHitPoints", "false") : null);
            SetOrRemove("altitudeLayer", !string.IsNullOrWhiteSpace(equipment.altitudeLayer) ? new XElement("altitudeLayer", equipment.altitudeLayer) : null);
            SetOrRemove("tickerType", !string.IsNullOrWhiteSpace(equipment.tickerType) ? new XElement("tickerType", equipment.tickerType) : null);
            SetOrRemove("smeltable", equipment.smeltable ? new XElement("smeltable", "true") : null);
            SetOrRemove("rotatable", equipment.rotatable ? new XElement("rotatable", "true") : null);
            SetOrRemove("selectable", !equipment.selectable ? new XElement("selectable", "false") : null);
            SetOrRemove("drawGUIOverlay", !equipment.drawGUIOverlay ? new XElement("drawGUIOverlay", "false") : null);
            SetOrRemove("alwaysHaulable", !equipment.alwaysHaulable ? new XElement("alwaysHaulable", "false") : null);
            // graphicData
            SetOrRemove("graphicData", GenerateEquipmentGraphicDataXml(equipment));
            // stuffCategories
            SetOrRemove("stuffCategories", GenerateStringListXml("stuffCategories", equipment.stuffCategories));
            // costStuffCount
            SetOrRemove("costStuffCount", equipment.costStuffCount > 0 ? new XElement("costStuffCount", equipment.costStuffCount.ToString(I)) : null);
            // costList
            SetOrRemove("costList", GenerateEquipmentCostListXml(equipment.costList));
            // 分类与标签
            SetOrRemove("thingCategories", GenerateEquipmentThingCategoriesXml(equipment.thingCategories));
            SetOrRemove("tradeTags", GenerateEquipmentTradeTagsXml(equipment.tradeTags));
            SetOrRemove("weaponTags", equipment.itemType == EquipmentType.WeaponMelee || equipment.itemType == EquipmentType.WeaponRanged
                ? GenerateWeaponTagsXml(equipment.weaponTags) : null);
            SetOrRemove("weaponClasses", equipment.itemType == EquipmentType.WeaponMelee || equipment.itemType == EquipmentType.WeaponRanged
                ? GenerateWeaponClassesXml(equipment.weaponClasses) : null);
            // statBases（含 MarketValue 注入）
            {
                var statEl = GenerateEquipmentStatEntryContainerXml("statBases", equipment.statBases);
                if (statEl == null)
                    statEl = new XElement("statBases");
                // 确保 MarketValue 存在
                if (statEl.Element("MarketValue") == null)
                    statEl.Add(new XElement("MarketValue", equipment.marketValue.ToString(I)));
                SetOrRemove("statBases", statEl);
            }
            // equippedStatOffsets
            SetOrRemove("equippedStatOffsets", GenerateEquipmentStatEntryContainerXml("equippedStatOffsets", equipment.equippedStatOffsets));
            // apparel
            SetOrRemove("apparel", equipment.itemType == EquipmentType.Apparel ? GenerateEquipmentApparelXml(equipment) : null);
            // building
            SetOrRemove("building", equipment.itemType == EquipmentType.Building || equipment.itemType == EquipmentType.Turret
                ? GenerateEquipmentBuildingXml(equipment) : null);
            // 建筑专有顶层字段
            {
                bool isBldg = equipment.itemType == EquipmentType.Building || equipment.itemType == EquipmentType.Turret;
                SetOrRemove("size", isBldg && !string.IsNullOrWhiteSpace(equipment.buildingSize) ? new XElement("size", equipment.buildingSize) : null);
                SetOrRemove("passability", isBldg && !string.IsNullOrWhiteSpace(equipment.passability) ? new XElement("passability", equipment.passability) : null);
                SetOrRemove("fillPercent", isBldg && equipment.fillPercent > 0f ? new XElement("fillPercent", equipment.fillPercent.ToString(I)) : null);
                SetOrRemove("terrainAffordanceNeeded", isBldg && !string.IsNullOrWhiteSpace(equipment.terrainAffordanceNeeded) ? new XElement("terrainAffordanceNeeded", equipment.terrainAffordanceNeeded) : null);
                SetOrRemove("blockWind", isBldg && equipment.blockWind.HasValue && equipment.blockWind.Value ? new XElement("blockWind", "true") : null);
                SetOrRemove("castEdgeShadows", isBldg && equipment.castEdgeShadows.HasValue && equipment.castEdgeShadows.Value ? new XElement("castEdgeShadows", "true") : null);
                SetOrRemove("drawerType", isBldg && !string.IsNullOrWhiteSpace(equipment.drawerType) ? new XElement("drawerType", equipment.drawerType) : null);
                SetOrRemove("canOverlapZones", isBldg && equipment.canOverlapZones.HasValue && !equipment.canOverlapZones.Value ? new XElement("canOverlapZones", "false") : null);
                SetOrRemove("hasInteractionCell", isBldg && equipment.hasInteractionCell ? new XElement("hasInteractionCell", "true") : null);
                SetOrRemove("interactionCellOffset", isBldg && !string.IsNullOrWhiteSpace(equipment.interactionCellOffset) ? new XElement("interactionCellOffset", equipment.interactionCellOffset) : null);
                SetOrRemove("killedLeavings", null); // managed via rawXmlEntries or building fields
                SetOrRemove("damageMultipliers", null); // managed via rawXmlEntries or building fields
                SetOrRemove("designationCategory", isBldg && !string.IsNullOrWhiteSpace(equipment.designationCategory) ? new XElement("designationCategory", equipment.designationCategory) : null);
                SetOrRemove("isMechClusterThreat", isBldg && equipment.isMechClusterThreat ? new XElement("isMechClusterThreat", "true") : null);
            }
            // recipeMaker
            SetOrRemove("recipeMaker", GenerateEquipmentRecipeMakerXml(equipment));
            // rawXmlEntries (comps/verbs/tools 等)
            SetOrRemove("comps", null);
            SetOrRemove("verbs", null);
            SetOrRemove("tools", null);
            // modExtensions
            SetOrRemove("modExtensions", includeModExtensions ? GenerateEquipmentModExtensionsXml(equipment) : null);

            // 2. 重建节点序列
            var result = new XElement(original.Name, original.Attributes());
            var insertedManaged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 2a. 遍历原始子节点：保留未管理的，替换管理的
            foreach (var child in original.Elements())
            {
                string childName = child.Name.LocalName;
                if (ManagedTopLevelNodes.Contains(childName))
                {
                    // 已管理节点：首次遇到时插入替换值
                    if (!insertedManaged.Contains(childName))
                    {
                        if (replacementNodes.TryGetValue(childName, out XElement? replacement) && replacement != null)
                            result.Add(replacement);
                        insertedManaged.Add(childName);
                    }
                    // 已插入过则跳过（合并多个同名的情况）
                }
                else
                {
                    // 未管理节点：原样保留
                    result.Add(child);
                }
            }

            // 2b. 追加原始 XML 中不存在但需要新增的已管理节点
            foreach (var kvp in replacementNodes)
            {
                if (!insertedManaged.Contains(kvp.Key))
                {
                    if (kvp.Value != null)
                        result.Add(kvp.Value);
                    insertedManaged.Add(kvp.Key);
                }
            }

            // 2c. 追加 rawXmlEntries（comps/verbs/tools 等）
            if (equipment.rawXmlEntries != null)
            {
                foreach (var entry in equipment.rawXmlEntries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.tagName) || string.IsNullOrWhiteSpace(entry.innerXml))
                        continue;
                    try
                    {
                        var parsed = XElement.Parse($"<{entry.tagName}>{entry.innerXml}</{entry.tagName}>");
                        result.Add(parsed);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[CharacterStudio] 装备非破坏性导出：rawXmlEntries 的 {entry.tagName} 解析失败: {ex.Message}");
                    }
                }
            }

            // 2d. 更新 ParentName 属性
            var parentAttr = result.Attribute("ParentName");
            string newParent = string.IsNullOrWhiteSpace(equipment.parentThingDefName)
                ? "ApparelMakeableBase" : equipment.parentThingDefName;
            if (parentAttr != null)
                parentAttr.Value = newParent;
            else
                result.Add(new XAttribute("ParentName", newParent));

            return result;
        }

        /// <summary>
        /// 生成 CS 抽象基类定义 XML（模板模式时导出到 CS_BaseClasses.xml）。
        /// </summary>
        public static XElement GenerateBaseClassesXml()
        {
            var defs = new XElement("Defs");

            // 远程武器基类
            defs.Add(new XElement("ThingDef",
                new XAttribute("Name", "CS_GunBase"),
                new XAttribute("ParentName", "BaseHumanMakeableGun"),
                new XAttribute("Abstract", "True"),
                new XElement("tradeability", "All"),
                new XElement("smeltable", "true"),
                new XElement("weaponTags", new XAttribute("Inherit", "false")),
                new XElement("tradeTags", new XAttribute("Inherit", "false"))
            ));

            // 近战锐器基类
            defs.Add(new XElement("ThingDef",
                new XAttribute("Name", "CS_MeleeSharpBase"),
                new XAttribute("ParentName", "BaseMeleeWeapon_Sharp_Quality"),
                new XAttribute("Abstract", "True"),
                new XElement("smeltable", "true"),
                new XElement("weaponTags", new XAttribute("Inherit", "false")),
                new XElement("tradeTags", new XAttribute("Inherit", "false"))
            ));

            // 近战钝器基类
            defs.Add(new XElement("ThingDef",
                new XAttribute("Name", "CS_MeleeBluntBase"),
                new XAttribute("ParentName", "BaseMeleeWeapon_Blunt_Quality"),
                new XAttribute("Abstract", "True"),
                new XElement("smeltable", "true"),
                new XElement("weaponTags", new XAttribute("Inherit", "false")),
                new XElement("tradeTags", new XAttribute("Inherit", "false"))
            ));

            // 服装基类
            defs.Add(new XElement("ThingDef",
                new XAttribute("Name", "CS_ApparelBase"),
                new XAttribute("ParentName", "ApparelMakeableBase"),
                new XAttribute("Abstract", "True"),
                new XElement("smeltable", "true"),
                new XElement("tradeTags", new XAttribute("Inherit", "false"))
            ));

            // 建筑基类
            defs.Add(new XElement("ThingDef",
                new XAttribute("Name", "CS_BuildingBase"),
                new XAttribute("ParentName", "BuildingBase"),
                new XAttribute("Abstract", "True")
            ));

            // 炮塔基类
            defs.Add(new XElement("ThingDef",
                new XAttribute("Name", "CS_TurretBase"),
                new XAttribute("ParentName", "BuildingBase"),
                new XAttribute("Abstract", "True")
            ));

            // 物品基类
            defs.Add(new XElement("ThingDef",
                new XAttribute("Name", "CS_ItemBase"),
                new XAttribute("ParentName", "ResourceBase"),
                new XAttribute("Abstract", "True")
            ));

            return defs;
        }

        /// <summary>
        /// 判断给定的装备列表中是否有任何装备使用模板模式。
        /// </summary>
        public static bool HasTemplateModeEquipment(List<CharacterEquipmentDef> equipments)
        {
            return equipments != null && equipments.Any(e => e != null && e.useTemplateMode);
        }
    }
}