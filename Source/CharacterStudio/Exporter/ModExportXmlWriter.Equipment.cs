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

                var equipmentEl = new XElement("li",
                    !string.IsNullOrWhiteSpace(equipment.defName) ? new XElement("defName", equipment.defName) : null,
                    !string.IsNullOrWhiteSpace(equipment.label) ? new XElement("label", equipment.label) : null,
                    !string.IsNullOrWhiteSpace(equipment.description) ? new XElement("description", equipment.description) : null,
                    new XElement("enabled", equipment.enabled.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(equipment.slotTag) ? new XElement("slotTag", equipment.slotTag) : null,
                    !string.IsNullOrWhiteSpace(equipment.exportGroupKey) ? new XElement("exportGroupKey", equipment.exportGroupKey) : null,
                    !string.IsNullOrWhiteSpace(equipment.thingDefName) ? new XElement("thingDefName", equipment.thingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.parentThingDefName) ? new XElement("parentThingDefName", equipment.parentThingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.worldTexPath) ? new XElement("worldTexPath", equipment.worldTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.wornTexPath) ? new XElement("wornTexPath", equipment.wornTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.maskTexPath) ? new XElement("maskTexPath", equipment.maskTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.shaderDefName) ? new XElement("shaderDefName", equipment.shaderDefName) : null,
                    equipment.useWornGraphicMask ? new XElement("useWornGraphicMask", equipment.useWornGraphicMask.ToString().ToLower()) : null,
                    new XElement("allowCrafting", equipment.allowCrafting.ToString().ToLower()),
                    !string.IsNullOrWhiteSpace(equipment.recipeDefName) ? new XElement("recipeDefName", equipment.recipeDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.recipeWorkbenchDefName) ? new XElement("recipeWorkbenchDefName", equipment.recipeWorkbenchDefName) : null,
                    Math.Abs(equipment.recipeWorkAmount - 1200f) > 0.0001f ? new XElement("recipeWorkAmount", equipment.recipeWorkAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    equipment.recipeProductCount != 1 ? new XElement("recipeProductCount", equipment.recipeProductCount) : null,
                    new XElement("allowTrading", equipment.allowTrading.ToString().ToLower()),
                    Math.Abs(equipment.marketValue - 250f) > 0.0001f ? new XElement("marketValue", equipment.marketValue.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    !string.IsNullOrWhiteSpace(equipment.previewTexPath) ? new XElement("previewTexPath", equipment.previewTexPath) : null,
                    !string.IsNullOrWhiteSpace(equipment.sourceNote) ? new XElement("sourceNote", equipment.sourceNote) : null,
                    !string.IsNullOrWhiteSpace(equipment.flyerThingDefName) ? new XElement("flyerThingDefName", equipment.flyerThingDefName) : null,
                    !string.IsNullOrWhiteSpace(equipment.flyerClassName) ? new XElement("flyerClassName", equipment.flyerClassName) : null,
                    Math.Abs(equipment.flyerFlightSpeed - 22f) > 0.0001f ? new XElement("flyerFlightSpeed", equipment.flyerFlightSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                    GenerateStringListXml("tags", equipment.tags),
                    GenerateStringListXml("tradeTags", equipment.tradeTags),
                    GenerateStringListXml("abilityDefNames", equipment.abilityDefNames),
                    GenerateStringListXml("thingCategories", equipment.thingCategories),
                    GenerateStringListXml("bodyPartGroups", equipment.bodyPartGroups),
                    GenerateStringListXml("apparelLayers", equipment.apparelLayers),
                    GenerateStringListXml("apparelTags", equipment.apparelTags),
                    GenerateEquipmentCostEntriesXml("recipeIngredients", equipment.recipeIngredients),
                    GenerateEquipmentStatEntriesXml("statBases", equipment.statBases),
                    GenerateEquipmentStatEntriesXml("equippedStatOffsets", equipment.equippedStatOffsets),
                    GenerateEquipmentRenderDataXml(equipment.renderData)
                );

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

            return new XElement("renderData",
                !string.IsNullOrWhiteSpace(renderData.layerName) ? new XElement("layerName", renderData.layerName) : null,
                !string.IsNullOrWhiteSpace(renderData.texPath) ? new XElement("texPath", renderData.texPath) : null,
                !string.IsNullOrWhiteSpace(renderData.anchorTag) ? new XElement("anchorTag", renderData.anchorTag) : null,
                !string.IsNullOrWhiteSpace(renderData.anchorPath) ? new XElement("anchorPath", renderData.anchorPath) : null,
                !string.IsNullOrWhiteSpace(renderData.maskTexPath) ? new XElement("maskTexPath", renderData.maskTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.shaderDefName) ? new XElement("shaderDefName", renderData.shaderDefName) : null,
                !string.IsNullOrWhiteSpace(renderData.directionalFacing) ? new XElement("directionalFacing", renderData.directionalFacing) : null,
                new XElement("offset", FormatVector3(renderData.offset)),
                renderData.offsetEast != Vector3.zero ? new XElement("offsetEast", FormatVector3(renderData.offsetEast)) : null,
                renderData.offsetNorth != Vector3.zero ? new XElement("offsetNorth", FormatVector3(renderData.offsetNorth)) : null,
                renderData.useWestOffset ? new XElement("useWestOffset", "true") : null,
                renderData.offsetWest != Vector3.zero ? new XElement("offsetWest", FormatVector3(renderData.offsetWest)) : null,
                new XElement("scale", FormatVector2(renderData.scale)),
                renderData.scaleEastMultiplier != Vector2.one ? new XElement("scaleEastMultiplier", FormatVector2(renderData.scaleEastMultiplier)) : null,
                renderData.scaleNorthMultiplier != Vector2.one ? new XElement("scaleNorthMultiplier", FormatVector2(renderData.scaleNorthMultiplier)) : null,
                renderData.scaleWestMultiplier != Vector2.one ? new XElement("scaleWestMultiplier", FormatVector2(renderData.scaleWestMultiplier)) : null,
                new XElement("rotation", renderData.rotation),
                renderData.rotationEastOffset != 0f ? new XElement("rotationEastOffset", renderData.rotationEastOffset) : null,
                renderData.rotationNorthOffset != 0f ? new XElement("rotationNorthOffset", renderData.rotationNorthOffset) : null,
                renderData.rotationWestOffset != 0f ? new XElement("rotationWestOffset", renderData.rotationWestOffset) : null,
                new XElement("drawOrder", renderData.drawOrder),
                new XElement("flipHorizontal", renderData.flipHorizontal.ToString().ToLower()),
                new XElement("visible", renderData.visible.ToString().ToLower()),
                new XElement("colorSource", renderData.colorSource.ToString()),
                renderData.colorSource == LayerColorSource.Fixed ? new XElement("customColor", FormatColor(renderData.customColor)) : null,
                new XElement("colorTwoSource", renderData.colorTwoSource.ToString()),
                renderData.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", FormatColor(renderData.customColorTwo)) : null,
                renderData.useTriggeredLocalAnimation ? new XElement("useTriggeredLocalAnimation", renderData.useTriggeredLocalAnimation.ToString().ToLower()) : null,
                !string.IsNullOrWhiteSpace(renderData.triggerAbilityDefName) ? new XElement("triggerAbilityDefName", renderData.triggerAbilityDefName) : null,
                !string.IsNullOrWhiteSpace(renderData.animationGroupKey) ? new XElement("animationGroupKey", renderData.animationGroupKey) : null,
                renderData.triggeredAnimationRole != EquipmentTriggeredAnimationRole.MovablePart ? new XElement("triggeredAnimationRole", renderData.triggeredAnimationRole.ToString()) : null,
                renderData.triggeredDeployAngle != 45f ? new XElement("triggeredDeployAngle", renderData.triggeredDeployAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                renderData.triggeredReturnAngle != 0f ? new XElement("triggeredReturnAngle", renderData.triggeredReturnAngle.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null,
                renderData.triggeredDeployTicks != 12 ? new XElement("triggeredDeployTicks", renderData.triggeredDeployTicks) : null,
                renderData.triggeredHoldTicks != 24 ? new XElement("triggeredHoldTicks", renderData.triggeredHoldTicks) : null,
                renderData.triggeredReturnTicks != 12 ? new XElement("triggeredReturnTicks", renderData.triggeredReturnTicks) : null,
                renderData.triggeredPivotOffset != Vector2.zero ? new XElement("triggeredPivotOffset", FormatVector2(renderData.triggeredPivotOffset)) : null,
                renderData.triggeredUseVfxVisibility ? new XElement("triggeredUseVfxVisibility", renderData.triggeredUseVfxVisibility.ToString().ToLower()) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredIdleTexPath) ? new XElement("triggeredIdleTexPath", renderData.triggeredIdleTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredDeployTexPath) ? new XElement("triggeredDeployTexPath", renderData.triggeredDeployTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredHoldTexPath) ? new XElement("triggeredHoldTexPath", renderData.triggeredHoldTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredReturnTexPath) ? new XElement("triggeredReturnTexPath", renderData.triggeredReturnTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredIdleMaskTexPath) ? new XElement("triggeredIdleMaskTexPath", renderData.triggeredIdleMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredDeployMaskTexPath) ? new XElement("triggeredDeployMaskTexPath", renderData.triggeredDeployMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredHoldMaskTexPath) ? new XElement("triggeredHoldMaskTexPath", renderData.triggeredHoldMaskTexPath) : null,
                !string.IsNullOrWhiteSpace(renderData.triggeredReturnMaskTexPath) ? new XElement("triggeredReturnMaskTexPath", renderData.triggeredReturnMaskTexPath) : null,
                !renderData.triggeredVisibleDuringDeploy ? new XElement("triggeredVisibleDuringDeploy", renderData.triggeredVisibleDuringDeploy.ToString().ToLower()) : null,
                !renderData.triggeredVisibleDuringHold ? new XElement("triggeredVisibleDuringHold", renderData.triggeredVisibleDuringHold.ToString().ToLower()) : null,
                !renderData.triggeredVisibleDuringReturn ? new XElement("triggeredVisibleDuringReturn", renderData.triggeredVisibleDuringReturn.ToString().ToLower()) : null,
                !renderData.triggeredVisibleOutsideCycle ? new XElement("triggeredVisibleOutsideCycle", renderData.triggeredVisibleOutsideCycle.ToString().ToLower()) : null,
                GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationSouth", renderData.triggeredAnimationSouth),
                GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationEastWest", renderData.triggeredAnimationEastWest),
                GenerateEquipmentTriggeredAnimationOverrideXml("triggeredAnimationNorth", renderData.triggeredAnimationNorth)
            );
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

            string resolvedThingDefName = equipment.GetResolvedThingDefName();
            if (string.IsNullOrWhiteSpace(resolvedThingDefName))
            {
                return null;
            }

            var thingDef = new XElement("ThingDef",
                new XAttribute("ParentName", string.IsNullOrWhiteSpace(equipment.parentThingDefName) ? "ApparelMakeableBase" : equipment.parentThingDefName),
                new XElement("defName", resolvedThingDefName),
                new XElement("label", equipment.GetDisplayLabel()),
                !string.IsNullOrWhiteSpace(equipment.description) ? new XElement("description", equipment.description) : null,
                new XElement("tradeability", equipment.allowTrading ? "All" : "None"),
                GenerateEquipmentGraphicDataXml(equipment),
                GenerateEquipmentThingCategoriesXml(equipment.thingCategories),
                GenerateEquipmentTradeTagsXml(equipment.tradeTags),
                equipment.itemType == EquipmentType.WeaponMelee || equipment.itemType == EquipmentType.WeaponRanged ? GenerateWeaponTagsXml(equipment.weaponTags) : null,
                equipment.itemType == EquipmentType.WeaponMelee || equipment.itemType == EquipmentType.WeaponRanged ? GenerateWeaponClassesXml(equipment.weaponClasses) : null,
                GenerateEquipmentStatEntryContainerXml("statBases", equipment.statBases),
                GenerateEquipmentStatEntryContainerXml("equippedStatOffsets", equipment.equippedStatOffsets),
                equipment.itemType == EquipmentType.Apparel ? GenerateEquipmentApparelXml(equipment) : null
            );

            XElement statBasesEl = thingDef.Element("statBases") ?? new XElement("statBases");
            if (thingDef.Element("statBases") == null)
                thingDef.Add(statBasesEl);
            if (statBasesEl.Element("MarketValue") == null)
            {
                statBasesEl.Add(new XElement("MarketValue", equipment.marketValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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

            return new XElement("graphicData",
                new XElement("texPath", texPath),
                !string.IsNullOrWhiteSpace(equipment.shaderDefName) ? new XElement("shaderType", equipment.shaderDefName) : null,
                new XElement("graphicClass", graphicClass)
            );
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

        private static XElement GenerateEquipmentApparelXml(CharacterEquipmentDef equipment)
        {
            // 如果 wornTexPath 是外部绝对路径（如 D:/...），RimWorld 原生 ContentFinder 无法解析，
            // 会导致穿戴后显示红色 X。此时不输出 wornGraphicPath，
            // 让 vanilla 回退到 graphicData（已使用 Graphic_Runtime 支持外部路径），
            // 同时 CS 自定义图层注入系统通过 DefModExtension_EquipmentRender 接管精确渲染。
            bool wornTexPathIsExternal = !string.IsNullOrWhiteSpace(equipment.wornTexPath)
                && Rendering.RuntimeAssetLoader.LooksLikeExternalTexturePath(equipment.wornTexPath);

            return new XElement("apparel",
                !string.IsNullOrWhiteSpace(equipment.wornTexPath) && !wornTexPathIsExternal
                    ? new XElement("wornGraphicPath", equipment.wornTexPath)
                    : null,
                equipment.useWornGraphicMask ? new XElement("useWornGraphicMask", equipment.useWornGraphicMask.ToString().ToLower()) : null,
                GenerateStringListXml("bodyPartGroups", equipment.bodyPartGroups),
                GenerateStringListXml("layers", equipment.apparelLayers),
                GenerateStringListXml("tags", equipment.apparelTags)
            );
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
    }
}