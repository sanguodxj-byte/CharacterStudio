using CharacterStudio.Core;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace CharacterStudio.Abilities
{
    public class EffectWorker_WeatherChange : EffectWorker
    {
        public override void Apply(AbilityEffectConfig effectConfig, LocalTargetInfo target, Pawn caster)
        {
            if (caster.Map == null || effectConfig == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(effectConfig.weatherDefName))
            {
                Log.Warning("[CharacterStudio] Weather change failed: weatherDefName is empty.");
                return;
            }

            WeatherDef? weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(effectConfig.weatherDefName.Trim());
            if (weatherDef == null)
            {
                Log.Warning($"[CharacterStudio] Ability weather change failed: WeatherDef '{effectConfig.weatherDefName}' not found.");
                return;
            }

            Map map = caster.Map;
            if (map.weatherManager == null)
            {
                return;
            }

            Log.Message($"[CharacterStudio] Force changing weather to '{weatherDef.defName}' on map '{map.uniqueID}'");

            // Use Harmony Traverse to access internal/private fields and methods
            var traverse = Traverse.Create(map.weatherManager);

            // Immediate force switch
            // Set both last and current to new weather to skip transition period
            traverse.Field("lastWeather").SetValue(weatherDef);
            traverse.Field("curWeather").SetValue(weatherDef);
            traverse.Field("curWeatherDuration").SetValue(effectConfig.weatherDurationTicks);
            traverse.Field("transitionEndTick").SetValue(Find.TickManager.TicksGame);

            // Access internal method to force update
            traverse.Method("InternalTick").GetValue();
            
            // Visual feedback: message to player
            Messages.Message("CS_Ability_WeatherChanged".Translate(weatherDef.LabelCap), MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
