using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 导出 XML 片段写入器。
    /// 将 ModBuilder 中的 Def/XML 片段序列化职责集中到此处，避免构建流程类承担过多 XML 拼装细节。
    /// </summary>
    public static class ModExportXmlWriter
    {
        public static XElement? GenerateBaseAppearanceXml(BaseAppearanceConfig? baseAppearance)
        {
            if (baseAppearance == null || baseAppearance.slots == null || baseAppearance.slots.Count == 0)
            {
                return null;
            }

            var root = new XElement("baseAppearance");
            var slotsEl = new XElement("slots");

            foreach (var slot in baseAppearance.slots)
            {
                if (slot == null) continue;

                slotsEl.Add(new XElement("li",
                    new XElement("slotType", slot.slotType.ToString()),
                    new XElement("enabled", slot.enabled.ToString().ToLower()),
                    !string.IsNullOrEmpty(slot.texPath) ? new XElement("texPath", slot.texPath) : null,
                    !string.IsNullOrEmpty(slot.maskTexPath) ? new XElement("maskTexPath", slot.maskTexPath) : null,
                    !string.IsNullOrEmpty(slot.shaderDefName) ? new XElement("shaderDefName", slot.shaderDefName) : null,
                    new XElement("colorSource", slot.colorSource.ToString()),
                    slot.colorSource == LayerColorSource.Fixed ? new XElement("customColor", FormatColor(slot.customColor)) : null,
                    new XElement("colorTwoSource", slot.colorTwoSource.ToString()),
                    slot.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", FormatColor(slot.customColorTwo)) : null,
                    new XElement("scale", FormatVector2(slot.scale)),
                    new XElement("offset", FormatVector3(slot.offset)),
                    slot.offsetEast != Vector3.zero ? new XElement("offsetEast", FormatVector3(slot.offsetEast)) : null,
                    slot.offsetNorth != Vector3.zero ? new XElement("offsetNorth", FormatVector3(slot.offsetNorth)) : null,
                    new XElement("rotation", slot.rotation),
                    new XElement("flipHorizontal", slot.flipHorizontal.ToString().ToLower()),
                    new XElement("drawOrderOffset", slot.drawOrderOffset),
                    slot.graphicClass != null ? new XElement("graphicClass", slot.graphicClass.FullName) : null
                ));
            }

            root.Add(slotsEl);
            return root;
        }

        public static XElement GenerateLayersXml(List<PawnLayerConfig>? layers)
        {
            var element = new XElement("layers");

            if (layers == null || layers.Count == 0)
            {
                return element;
            }

            foreach (var layer in layers)
            {
                element.Add(new XElement("li",
                    new XElement("layerName", layer.layerName ?? ""),
                    new XElement("texPath", layer.texPath ?? ""),
                    new XElement("anchorTag", layer.anchorTag ?? "Head"),
                    !string.IsNullOrEmpty(layer.anchorPath) ? new XElement("anchorPath", layer.anchorPath) : null,
                    !string.IsNullOrEmpty(layer.maskTexPath) ? new XElement("maskTexPath", layer.maskTexPath) : null,
                    !string.IsNullOrEmpty(layer.shaderDefName) ? new XElement("shaderDefName", layer.shaderDefName) : null,
                    new XElement("offset", FormatVector3(layer.offset)),
                    layer.offsetEast != Vector3.zero ? new XElement("offsetEast", FormatVector3(layer.offsetEast)) : null,
                    layer.offsetNorth != Vector3.zero ? new XElement("offsetNorth", FormatVector3(layer.offsetNorth)) : null,
                    new XElement("drawOrder", layer.drawOrder),
                    new XElement("scale", FormatVector2(layer.scale)),
                    new XElement("rotation", layer.rotation),
                    new XElement("flipHorizontal", layer.flipHorizontal.ToString().ToLower()),
                    new XElement("colorSource", layer.colorSource.ToString()),
                    layer.colorSource == LayerColorSource.Fixed ? new XElement("customColor", FormatColor(layer.customColor)) : null,
                    new XElement("colorTwoSource", layer.colorTwoSource.ToString()),
                    layer.colorTwoSource == LayerColorSource.Fixed ? new XElement("customColorTwo", FormatColor(layer.customColorTwo)) : null,
                    new XElement("visible", layer.visible.ToString().ToLower()),
                    layer.workerClass != null ? new XElement("workerClass", layer.workerClass.FullName) : null,
                    layer.graphicClass != null ? new XElement("graphicClass", layer.graphicClass.FullName) : null,
                    layer.workerClass != null && layer.workerClass.Name.Contains("FaceComponent") ? new XElement("faceComponent", layer.faceComponent.ToString()) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animationType", layer.animationType.ToString()) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animFrequency", layer.animFrequency) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animAmplitude", layer.animAmplitude) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animSpeed", layer.animSpeed) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animPhaseOffset", layer.animPhaseOffset) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animAffectsOffset", layer.animAffectsOffset.ToString().ToLower()) : null,
                    layer.animationType != LayerAnimationType.None ? new XElement("animOffsetAmplitude", layer.animOffsetAmplitude) : null
                ));
            }

            return element;
        }

        public static XElement? GenerateStringListXml(string tagName, List<string>? values)
        {
            if (values == null || values.Count == 0)
            {
                return null;
            }

            var element = new XElement(tagName);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    element.Add(new XElement("li", value));
                }
            }

            return element.HasElements ? element : null;
        }

        public static XElement GenerateFaceConfigXml(PawnFaceConfig config)
        {
            var element = new XElement("faceConfig",
                new XElement("enabled", config.enabled.ToString().ToLower())
            );

            if (config.components != null && config.components.Count > 0)
            {
                var componentsEl = new XElement("components");
                foreach (var component in config.components)
                {
                    if (component == null) continue;

                    var compEl = new XElement("li",
                        new XElement("type", component.type.ToString())
                    );

                    if (component.expressions != null && component.expressions.Count > 0)
                    {
                        var exprsEl = new XElement("expressions");
                        foreach (var expression in component.expressions)
                        {
                            if (expression == null || string.IsNullOrEmpty(expression.texPath)) continue;

                            exprsEl.Add(new XElement("li",
                                new XElement("expression", expression.expression.ToString()),
                                new XElement("texPath", expression.texPath)
                            ));
                        }

                        if (exprsEl.HasElements)
                        {
                            compEl.Add(exprsEl);
                        }
                    }

                    componentsEl.Add(compEl);
                }

                if (componentsEl.HasElements)
                {
                    element.Add(componentsEl);
                }
            }

            return element;
        }

        public static XElement GenerateTargetRacesXml(List<string>? races)
        {
            var element = new XElement("targetRaces");

            if (races == null || races.Count == 0)
            {
                return element;
            }

            foreach (var race in races)
            {
                element.Add(new XElement("li", race));
            }

            return element;
        }

        public static XElement? GenerateSkinAbilitiesXml(List<ModularAbilityDef>? abilities)
        {
            if (abilities == null || abilities.Count == 0)
            {
                return null;
            }

            var element = new XElement("abilities");
            foreach (var ability in abilities)
            {
                if (ability == null || string.IsNullOrEmpty(ability.defName)) continue;

                var abilityEl = new XElement("li",
                    new XElement("defName", ability.defName),
                    !string.IsNullOrEmpty(ability.label) ? new XElement("label", ability.label) : null,
                    !string.IsNullOrEmpty(ability.description) ? new XElement("description", ability.description) : null,
                    !string.IsNullOrEmpty(ability.iconPath) ? new XElement("iconPath", ability.iconPath) : null,
                    new XElement("cooldownTicks", ability.cooldownTicks),
                    new XElement("warmupTicks", ability.warmupTicks),
                    new XElement("charges", ability.charges),
                    new XElement("aiCanUse", ability.aiCanUse),
                    new XElement("carrierType", ability.carrierType.ToString()),
                    new XElement("range", ability.range),
                    new XElement("radius", ability.radius),
                    ability.projectileDef != null ? new XElement("projectileDef", ability.projectileDef.defName) : null,
                    GenerateSkinAbilityEffectsXml(ability.effects),
                    GenerateRuntimeComponentsXml(ability.runtimeComponents)
                );

                element.Add(abilityEl);
            }

            return element;
        }

        public static XElement? GenerateSkinAbilityEffectsXml(List<AbilityEffectConfig>? effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return null;
            }

            var effectsEl = new XElement("effects");
            foreach (var effect in effects)
            {
                if (effect == null) continue;

                var effectEl = new XElement("li",
                    new XElement("type", effect.type.ToString()),
                    new XElement("amount", effect.amount),
                    new XElement("duration", effect.duration),
                    new XElement("chance", effect.chance),
                    effect.damageDef != null ? new XElement("damageDef", effect.damageDef.defName) : null,
                    effect.hediffDef != null ? new XElement("hediffDef", effect.hediffDef.defName) : null,
                    effect.summonKind != null ? new XElement("summonKind", effect.summonKind.defName) : null,
                    new XElement("summonCount", effect.summonCount)
                );

                effectsEl.Add(effectEl);
            }

            return effectsEl;
        }

        public static XElement? GenerateRuntimeComponentsXml(List<AbilityRuntimeComponentConfig>? components)
        {
            if (components == null || components.Count == 0)
            {
                return null;
            }

            var root = new XElement("runtimeComponents");
            foreach (var component in components)
            {
                if (component == null) continue;

                var compEl = new XElement("li",
                    new XElement("type", component.type.ToString()),
                    new XElement("enabled", component.enabled.ToString().ToLower()),
                    new XElement("comboWindowTicks", component.comboWindowTicks),
                    new XElement("cooldownTicks", component.cooldownTicks),
                    new XElement("jumpDistance", component.jumpDistance),
                    new XElement("findCellRadius", component.findCellRadius),
                    new XElement("triggerAbilityEffectsAfterJump", component.triggerAbilityEffectsAfterJump.ToString().ToLower()),
                    new XElement("requiredStacks", component.requiredStacks),
                    new XElement("delayTicks", component.delayTicks),
                    new XElement("wave1Radius", component.wave1Radius),
                    new XElement("wave1Damage", component.wave1Damage),
                    new XElement("wave2Radius", component.wave2Radius),
                    new XElement("wave2Damage", component.wave2Damage),
                    new XElement("wave3Radius", component.wave3Radius),
                    new XElement("wave3Damage", component.wave3Damage),
                    component.waveDamageDef != null ? new XElement("waveDamageDef", component.waveDamageDef.defName) : null
                );

                root.Add(compEl);
            }

            return root;
        }

        public static XElement? GenerateAbilityHotkeysXml(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null)
            {
                return null;
            }

            return new XElement("abilityHotkeys",
                new XElement("enabled", hotkeys.enabled.ToString().ToLower()),
                !string.IsNullOrEmpty(hotkeys.qAbilityDefName) ? new XElement("qAbilityDefName", hotkeys.qAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.wAbilityDefName) ? new XElement("wAbilityDefName", hotkeys.wAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.eAbilityDefName) ? new XElement("eAbilityDefName", hotkeys.eAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.rAbilityDefName) ? new XElement("rAbilityDefName", hotkeys.rAbilityDefName) : null,
                !string.IsNullOrEmpty(hotkeys.wComboAbilityDefName) ? new XElement("wComboAbilityDefName", hotkeys.wComboAbilityDefName) : null
            );
        }

        public static XElement? GenerateAbilityRefsXml(List<ModularAbilityDef>? abilities)
        {
            if (abilities == null || abilities.Count == 0)
            {
                return null;
            }

            var element = new XElement("abilities");
            foreach (var ability in abilities)
            {
                element.Add(new XElement("li", ability.defName));
            }

            return element;
        }

        public static XElement GenerateAbilityEffectsXml(List<AbilityEffectConfig> effects)
        {
            var element = new XElement("effects");
            foreach (var effect in effects)
            {
                var effectEl = new XElement("li",
                    new XElement("type", effect.type.ToString()),
                    new XElement("amount", effect.amount),
                    new XElement("duration", effect.duration),
                    new XElement("chance", effect.chance)
                );

                if (effect.damageDef != null)
                    effectEl.Add(new XElement("damageDef", effect.damageDef.defName));
                if (effect.hediffDef != null)
                    effectEl.Add(new XElement("hediffDef", effect.hediffDef.defName));
                if (effect.summonKind != null)
                    effectEl.Add(new XElement("summonKind", effect.summonKind.defName));

                effectEl.Add(new XElement("summonCount", effect.summonCount));
                element.Add(effectEl);
            }

            return element;
        }

        private static XElement? GenerateAttributesXml(CharacterStudio.AI.CharacterAttributeProfile? attributes)
        {
            if (attributes == null)
            {
                return null;
            }

            return new XElement("attributes",
                !string.IsNullOrEmpty(attributes.title) ? new XElement("title", attributes.title) : null,
                !string.IsNullOrEmpty(attributes.factionRole) ? new XElement("factionRole", attributes.factionRole) : null,
                !string.IsNullOrEmpty(attributes.combatRole) ? new XElement("combatRole", attributes.combatRole) : null,
                !string.IsNullOrEmpty(attributes.backstorySummary) ? new XElement("backstorySummary", attributes.backstorySummary) : null,
                !string.IsNullOrEmpty(attributes.personality) ? new XElement("personality", attributes.personality) : null,
                !string.IsNullOrEmpty(attributes.bodyTypeDefName) ? new XElement("bodyTypeDefName", attributes.bodyTypeDefName) : null,
                !string.IsNullOrEmpty(attributes.headTypeDefName) ? new XElement("headTypeDefName", attributes.headTypeDefName) : null,
                !string.IsNullOrEmpty(attributes.hairDefName) ? new XElement("hairDefName", attributes.hairDefName) : null,
                !string.IsNullOrEmpty(attributes.favoriteColorHex) ? new XElement("favoriteColorHex", attributes.favoriteColorHex) : null,
                new XElement("biologicalAge", attributes.biologicalAge),
                new XElement("chronologicalAge", attributes.chronologicalAge),
                new XElement("moveSpeedMultiplier", attributes.moveSpeedMultiplier),
                new XElement("meleePower", attributes.meleePower),
                new XElement("shootingAccuracy", attributes.shootingAccuracy),
                new XElement("armorRating", attributes.armorRating),
                new XElement("psychicSensitivity", attributes.psychicSensitivity),
                new XElement("marketValue", attributes.marketValue),
                GenerateStringListXml("tags", attributes.tags),
                GenerateStringListXml("keyTraits", attributes.keyTraits),
                GenerateStringListXml("startingApparelDefs", attributes.startingApparelDefs)
            );
        }

        public static XDocument CreateSkinDefDocument(PawnSkinDef skin, string modName, string description, string author, string version, Func<string, string> sanitizeFileName)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs",
                    new XElement("CharacterStudio.Core.PawnSkinDef",
                        new XElement("defName", skin.defName ?? $"Skin_{sanitizeFileName(modName)}"),
                        new XElement("label", skin.label ?? modName),
                        new XElement("description", skin.description ?? description),
                        new XElement("hideVanillaHead", skin.hideVanillaHead.ToString().ToLower()),
                        new XElement("hideVanillaHair", skin.hideVanillaHair.ToString().ToLower()),
                        new XElement("hideVanillaBody", skin.hideVanillaBody.ToString().ToLower()),
                        new XElement("hideVanillaApparel", skin.hideVanillaApparel.ToString().ToLower()),
                        new XElement("humanlikeOnly", skin.humanlikeOnly.ToString().ToLower()),
                        new XElement("author", author),
                        new XElement("version", version),
                        !string.IsNullOrEmpty(skin.previewTexPath) ? new XElement("previewTexPath", skin.previewTexPath) : null,
                        GenerateAttributesXml(skin.attributes),
                        GenerateStringListXml("hiddenPaths", skin.hiddenPaths),
#pragma warning disable CS0618
                        GenerateStringListXml("hiddenTags", skin.hiddenTags),
#pragma warning restore CS0618
                        GenerateBaseAppearanceXml(skin.baseAppearance),
                        GenerateLayersXml(skin.layers),
                        GenerateTargetRacesXml(skin.targetRaces),
                        skin.faceConfig != null && (skin.faceConfig.enabled || (skin.faceConfig.components?.Count ?? 0) > 0) ? GenerateFaceConfigXml(skin.faceConfig) : null,
                        GenerateSkinAbilitiesXml(skin.abilities),
                        GenerateAbilityHotkeysXml(skin.abilityHotkeys)
                    )
                )
            );
        }

        public static XDocument CreateGeneDefDocument(ModExportConfig config, string safeName)
        {
            if (config.SkinDef == null)
            {
                throw new ArgumentNullException(nameof(config), "导出配置缺少皮肤定义");
            }

            string geneDefName = $"CS_Gene_Face_{safeName}";
            string skinDefName = config.SkinDef.defName ?? $"Skin_{safeName}";
            string iconPath = string.IsNullOrEmpty(config.GeneIconPath)
                ? $"CS/{safeName}/Icon"
                : config.GeneIconPath;

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs",
                    new XElement("GeneDef",
                        new XElement("defName", geneDefName),
                        new XElement("label", $"Face: {config.ModName}"),
                        new XElement("description",
                            $"Changes the carrier's appearance to resemble {config.ModName}.\n\n" +
                            (config.OverlayMode
                                ? "This is an overlay effect that adds to the existing appearance."
                                : "This replaces the carrier's head appearance.")),
                        new XElement("iconPath", iconPath),
                        new XElement("biostatCpx", 0),
                        new XElement("biostatMet", 0),
                        new XElement("displayCategory", config.GeneCategory),
                        new XElement("displayOrderInCategory", 100),
                        new XElement("selectionWeight", 0),
                        new XElement("modExtensions",
                            new XElement("li", new XAttribute("Class", "CharacterStudio.Core.DefModExtension_SkinLink"),
                                new XElement("skinDefName", skinDefName),
                                new XElement("priority", 100),
                                new XElement("overlayMode", config.OverlayMode.ToString().ToLower()),
                                new XElement("hideVanillaHead", (!config.OverlayMode).ToString().ToLower()),
                                new XElement("hideVanillaHair", (!config.OverlayMode && (config.SkinDef?.hideVanillaHair ?? true)).ToString().ToLower())
                            )
                        )
                    )
                )
            );
        }

        public static XElement CreatePawnKindDefElement(string pawnKindName, ModExportConfig config, string skinDefName)
        {
            var pawnKindDef = new XElement("PawnKindDef",
                new XElement("defName", pawnKindName),
                new XElement("label", config.ModName),
                new XElement("race", "Human"),
                new XElement("combatPower", 100),
                new XElement("defaultFactionType", "PlayerColony")
            );

            var abilitiesXml = GenerateAbilityRefsXml(config.Abilities);
            if (abilitiesXml != null)
            {
                pawnKindDef.Add(abilitiesXml);
            }

            pawnKindDef.Add(new XElement("modExtensions",
                new XElement("li", new XAttribute("Class", "CharacterStudio.Core.DefModExtension_SkinLink"),
                    new XElement("skinDefName", skinDefName),
                    new XElement("priority", 100),
                    new XElement("hideVanillaHead", "true"),
                    new XElement("hideVanillaHair", "true")
                ),
                new XElement("li", new XAttribute("Class", "CharacterStudio.AI.CompProperties_CustomAI"),
                    new XElement("behavior",
                        new XElement("behaviorType", "Normal")
                    )
                )
            ));

            return pawnKindDef;
        }

        public static XDocument CreateUnitDefDocument(ModExportConfig config, string safeName)
        {
            if (config.SkinDef == null)
            {
                throw new ArgumentNullException(nameof(config), "导出配置缺少皮肤定义");
            }

            string pawnKindName = $"CS_PawnKind_{safeName}";
            string thingDefName = $"CS_Item_Summon_{safeName}";
            string skinDefName = config.SkinDef.defName ?? $"Skin_{safeName}";

            var defsRoot = new XElement("Defs",
                CreatePawnKindDefElement(pawnKindName, config, skinDefName)
            );

            if (config.IncludeSummonItem)
            {
                defsRoot.Add(
                    new XElement("ThingDef", new XAttribute("ParentName", "ResourceBase"),
                        new XElement("defName", thingDefName),
                        new XElement("label", $"Summon: {config.ModName}"),
                        new XElement("description", $"Use to summon {config.ModName}."),
                        new XElement("graphicData",
                            new XElement("texPath", "Things/Item/Resource/UnfinishedComponent"),
                            new XElement("graphicClass", "Graphic_Single")
                        ),
                        new XElement("statBases",
                            new XElement("MarketValue", 1000),
                            new XElement("Mass", 0.5)
                        ),
                        new XElement("thingCategories",
                            new XElement("li", "Items")
                        ),
                        new XElement("comps",
                            new XElement("li", new XAttribute("Class", "CompProperties_Usable"),
                                new XElement("useJob", "UseItem"),
                                new XElement("useLabel", "Summon")
                            ),
                            new XElement("li", new XAttribute("Class", "CharacterStudio.Items.CompProperties_SummonCharacter"),
                                new XElement("pawnKind", pawnKindName),
                                new XElement("arrivalMode", "DropPod")
                            )
                        )
                    )
                );
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                defsRoot
            );
        }

        public static XDocument CreateAbilityDefDocument(List<ModularAbilityDef> abilities)
        {
            var element = new XElement("Defs");

            foreach (var ability in abilities)
            {
                var abilityDef = new XElement("AbilityDef",
                    new XElement("defName", ability.defName),
                    new XElement("label", ability.label),
                    new XElement("description", ability.description),
                    new XElement("iconPath", ability.iconPath),
                    new XElement("cooldownTicksRange", ability.cooldownTicks),
                    new XElement("verbProperties",
                        new XElement("warmupTime", ability.warmupTicks / 60f),
                        new XElement("range", ability.range),
                        new XElement("targetParams",
                            new XElement("canTargetPawns", "true"),
                            new XElement("canTargetLocations", "true")
                        )
                    ),
                    new XElement("comps",
                        new XElement("li", new XAttribute("Class", "CharacterStudio.Abilities.CompProperties_AbilityModular"),
                            GenerateAbilityEffectsXml(ability.effects)
                        )
                    )
                );

                var verbProps = abilityDef.Element("verbProperties");
                switch (ability.carrierType)
                {
                    case AbilityCarrierType.Self:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        verbProps.Add(new XElement("targetable", "false"));
                        break;
                    case AbilityCarrierType.Touch:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbilityTouch"));
                        verbProps.Add(new XElement("range", "-1"));
                        break;
                    case AbilityCarrierType.Target:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        break;
                    case AbilityCarrierType.Projectile:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        break;
                    case AbilityCarrierType.Area:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        verbProps.Add(new XElement("radius", ability.radius));
                        break;
                }

                element.Add(abilityDef);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), element);
        }

        public static XDocument CreateManifestDocument(ModExportConfig config, string generatorVersion)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Manifest",
                    new XElement("GeneratorVersion", generatorVersion),
                    new XElement("GeneratedAt", DateTime.UtcNow.ToString("o")),
                    new XElement("ExportMode", config.Mode.ToString()),
                    new XElement("ModName", config.ModName),
                    new XElement("ModVersion", config.Version),
                    new XElement("Author", config.Author),
                    new XElement("ExportSettings",
                        new XElement("IncludeSkinDef", config.IncludeSkinDef.ToString().ToLower()),
                        new XElement("IncludeGeneDef", config.IncludeGeneDef.ToString().ToLower()),
                        new XElement("IncludePawnKind", config.IncludePawnKind.ToString().ToLower()),
                        new XElement("IncludeSummonItem", config.IncludeSummonItem.ToString().ToLower()),
                        new XElement("IncludeAbilities", config.IncludeAbilities.ToString().ToLower()),
                        new XElement("CopyTextures", config.CopyTextures.ToString().ToLower()),
                        new XElement("ExportAsGene", config.ExportAsGene.ToString().ToLower()),
                        new XElement("OverlayMode", config.OverlayMode.ToString().ToLower())
                    ),
                    new XElement("AssetSources",
                        from source in config.AssetSources
                        select new XElement("Asset",
                            new XAttribute("type", source.SourceType.ToString()),
                            new XElement("OriginalPath", source.OriginalPath),
                            new XElement("ResolvedPath", source.ResolvedPath),
                            source.SourceModPackageId != null ? new XElement("SourceMod", source.SourceModPackageId) : null
                        )
                    ),
                    new XElement("Dependencies",
                        from dep in config.DetectedDependencies
                        select new XElement("Dependency", dep)
                    )
                )
            );
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
        }

        private static string FormatVector2(Vector2 value)
        {
            return $"({value.x:F2}, {value.y:F2})";
        }

        private static string FormatColor(Color value)
        {
            return $"({value.r:F3}, {value.g:F3}, {value.b:F3}, {value.a:F3})";
        }
    }
}
