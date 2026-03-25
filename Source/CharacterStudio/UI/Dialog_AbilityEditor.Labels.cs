using CharacterStudio.Abilities;
using System;
using System.Collections.Generic;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private static string GetCarrierTypeLabel(AbilityCarrierType type)
        {
            return ($"CS_Ability_CarrierType_{ModularAbilityDefExtensions.NormalizeCarrierType(type)}").Translate();
        }

        private static string GetTargetTypeLabel(AbilityTargetType type)
        {
            return ($"CS_Ability_TargetType_{type}").Translate();
        }

        private static string GetAreaCenterLabel(AbilityAreaCenter center)
        {
            return ($"CS_Ability_AreaCenter_{center}").Translate();
        }

        private static string GetAreaShapeLabel(AbilityAreaShape shape)
        {
            return ($"CS_Studio_Ability_AreaShape_{shape}").Translate();
        }

        private static AbilityTargetType[] GetAvailableTargetTypes(ModularAbilityDef ability)
        {
            AbilityCarrierType carrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            return carrier switch
            {
                AbilityCarrierType.Self => new[] { AbilityTargetType.Self },
                AbilityCarrierType.Projectile => new[] { AbilityTargetType.Entity, AbilityTargetType.Cell },
                _ => new[] { AbilityTargetType.Entity, AbilityTargetType.Cell }
            };
        }

        private static AbilityAreaCenter[] GetAvailableAreaCenters(ModularAbilityDef ability)
        {
            AbilityTargetType target = ModularAbilityDefExtensions.NormalizeTargetType(ability);
            return target == AbilityTargetType.Self
                ? new[] { AbilityAreaCenter.Self }
                : new[] { AbilityAreaCenter.Self, AbilityAreaCenter.Target };
        }

        private static string GetEffectTypeLabel(AbilityEffectType type)
        {
            return ($"CS_Ability_EffectType_{type}").Translate();
        }

        private static string GetRuntimeComponentTypeLabel(AbilityRuntimeComponentType type)
        {
            if (type == AbilityRuntimeComponentType.VanillaPawnFlyer)
            {
                return "CS_Ability_RuntimeComponentType_VanillaPawnFlyer_Short".Translate();
            }

            if (type == AbilityRuntimeComponentType.FlightOnlyFollowup)
            {
                return "CS_Ability_RuntimeComponentType_FlightOnlyFollowup_Short".Translate();
            }
            return ($"CS_Ability_RuntimeComponentType_{type}").Translate();
        }
    }
}
