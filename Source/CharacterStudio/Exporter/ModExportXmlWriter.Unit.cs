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
            string? raceDefName = config.CharacterDefinition?.raceDefName;
            if (string.IsNullOrWhiteSpace(raceDefName) && config.SkinDef?.targetRaces != null && config.SkinDef.targetRaces.Count > 0)
            {
                raceDefName = config.SkinDef.targetRaces[0];
            }
            raceDefName = string.IsNullOrWhiteSpace(raceDefName) ? "Human" : raceDefName;

            var pawnKindDef = new XElement("PawnKindDef",
                new XElement("defName", pawnKindName),
                new XElement("label", config.ModName),
                new XElement("race", raceDefName),
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

            string safeDefName = ModBuilder.SanitizeDefName(config.SkinDef.defName ?? config.ModName);

            string pawnKindName = $"CS_PawnKind_{safeDefName}";
            string thingDefName = $"CS_Item_Summon_{safeDefName}";
            string skinDefName = ModBuilder.SanitizeDefName(config.SkinDef.defName ?? $"Skin_{config.ModName}");

            var defsRoot = new XElement("Defs",
                CreatePawnKindDefElement(pawnKindName, config, skinDefName)
            );

            // ── XenotypeDef（若皮肤绑定了 xenotypeDefName 则生成）────────────
            if (!string.IsNullOrEmpty(config.SkinDef.xenotypeDefName))
            {
                var xenoDef = BuildXenotypeDef(
                    ModBuilder.SanitizeDefName(config.SkinDef.xenotypeDefName),
                    string.IsNullOrEmpty(config.SkinDef.raceDisplayName)
                        ? config.ModName
                        : config.SkinDef.raceDisplayName,
                    skinDefName);
                defsRoot.AddFirst(xenoDef); // XenotypeDef 放在 PawnKindDef 前面
            }

            if (config.IncludeSummonItem)
            {
                defsRoot.Add(
                    new XElement("ThingDef", new XAttribute("ParentName", "ResourceBase"),
                        new XElement("defName", thingDefName),
                        new XElement("label", $"Character: {config.ModName}"),
                        new XElement("description", $"Use this item to generate {config.ModName} on the map."),
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
                                new XElement("useLabel", "Generate Character")
                            ),
                            new XElement("li", new XAttribute("Class", "CharacterStudio.Items.CompProperties_SummonCharacter"),
                                new XElement("pawnKind", pawnKindName),
                                new XElement("skinDefName", skinDefName),
                                new XElement("characterDefFileName", $"{safeName}_Character.xml"),
                                new XElement("arrivalMode", config.RoleCardArrivalMode.ToString()),
                                new XElement("spawnEvent", config.RoleCardSpawnEvent.ToString()),
                                new XElement("spawnAnimation", config.RoleCardSpawnAnimation.ToString()),
                                new XElement("spawnAnimationScale", config.RoleCardSpawnAnimationScale.ToString(System.Globalization.CultureInfo.InvariantCulture))
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
                    )
                )
            );
        }
        public static XElement BuildXenotypeDef(string xenoDefName, string xenoLabel, string skinDefName)
        {
            return new XElement("XenotypeDef",
                new XComment($" Generated by CharacterStudio — linked skin: {skinDefName} "),
                new XElement("defName", xenoDefName),
                new XElement("label", xenoLabel),
                new XElement("description", $"A xenotype defined by the CharacterStudio skin '{skinDefName}'."),
                // iconDef 留空，使用默认图标；作者可手动指定
                // genes 留空列表，外观完全由 CompPawnSkin 图层驱动
                new XElement("genes")
            );
        }

        /// <summary>
        /// 生成独立的 XenotypeDef XDocument（供 ModBuilder 写入单独文件使用）。
        /// </summary>
        public static XDocument CreateXenotypeDefDocument(string xenoDefName, string xenoLabel, string skinDefName)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Defs",
                    BuildXenotypeDef(xenoDefName, xenoLabel, skinDefName)
                )
            );
        }
    }
}