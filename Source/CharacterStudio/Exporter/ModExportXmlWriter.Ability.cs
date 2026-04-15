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
        public static XElement? GenerateSkinAbilitiesXml(List<ModularAbilityDef>? abilities)
        {
            return AbilityXmlSerialization.GenerateAbilitiesElement(abilities);
        }

        public static XElement? GenerateSkinAbilityEffectsXml(List<AbilityEffectConfig>? effects)
        {
            return AbilityXmlSerialization.GenerateAbilityEffectsElement(effects);
        }

        public static XElement? GenerateRuntimeComponentsXml(List<AbilityRuntimeComponentConfig>? components)
        {
            return AbilityXmlSerialization.GenerateRuntimeComponentsElement(components);
        }

        public static XElement? GenerateAbilityHotkeysXml(SkinAbilityHotkeyConfig? hotkeys)
        {
            return AbilityXmlSerialization.GenerateAbilityHotkeysElement(hotkeys);
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
            return AbilityXmlSerialization.GenerateAbilityEffectsElement(effects) ?? new XElement("effects");
        }

        public static XElement? GenerateAbilityVisualEffectsXml(List<AbilityVisualEffectConfig>? visualEffects)
        {
            return AbilityXmlSerialization.GenerateAbilityVisualEffectsElement(visualEffects);
        }
        public static XDocument CreateAbilityDefDocument(List<ModularAbilityDef> abilities)
        {
            var element = new XElement("Defs");

            foreach (var ability in abilities)
            {
                AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
                AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);

                var targetParams = normalizedTarget switch
                {
                    AbilityTargetType.Cell => new XElement("targetParams",
                        new XElement("canTargetPawns", "false"),
                        new XElement("canTargetBuildings", "false"),
                        new XElement("canTargetLocations", "true"),
                        new XElement("canTargetSelf", "false")
                    ),
                    AbilityTargetType.Entity => new XElement("targetParams",
                        new XElement("canTargetPawns", "true"),
                        new XElement("canTargetBuildings", "true"),
                        new XElement("canTargetLocations", "false"),
                        new XElement("canTargetSelf", "false")
                    ),
                    _ => new XElement("targetParams",
                        new XElement("canTargetPawns", "false"),
                        new XElement("canTargetBuildings", "false"),
                        new XElement("canTargetLocations", "false"),
                        new XElement("canTargetSelf", "true")
                    )
                };

                float exportedRange = normalizedCarrier == AbilityCarrierType.Self
                    ? 0f
                    : Mathf.Max(ability.range, 1f);

                var verbProps = new XElement("verbProperties",
                    new XElement("warmupTime", ability.warmupTicks / 60f),
                    new XElement("range", exportedRange),
                    targetParams
                );

                switch (normalizedCarrier)
                {
                    case AbilityCarrierType.Self:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        verbProps.Add(new XElement("targetable", "false"));
                        break;
                    case AbilityCarrierType.Projectile:
                        verbProps.Add(new XElement("verbClass", "Verb_LaunchProjectile"));
                        if (ability.projectileDef != null)
                        {
                            verbProps.Add(new XElement("defaultProjectile", ability.projectileDef.defName));
                        }
                        break;
                    default:
                        verbProps.Add(new XElement("verbClass", "Verb_CastAbility"));
                        break;
                }

                if (ability.useRadius && ability.radius > 0f)
                {
                    verbProps.Add(new XElement("radius", ability.radius));
                }

                var abilityDef = new XElement("AbilityDef",
                    new XElement("defName", ability.defName),
                    new XElement("label", ability.label),
                    new XElement("description", ability.description),
                    new XElement("iconPath", ability.iconPath),
                    new XElement("cooldownTicksRange", ability.cooldownTicks),
                    verbProps,
                    new XElement("comps",
                        new XElement("li", new XAttribute("Class", "CharacterStudio.Abilities.CompProperties_AbilityModular"),
                            GenerateAbilityEffectsXml(ability.effects),
                            GenerateAbilityVisualEffectsXml(ability.visualEffects),
                            GenerateRuntimeComponentsXml(ability.runtimeComponents)
                        )
                    )
                );

                element.Add(abilityDef);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), element);
        }
    }
}
