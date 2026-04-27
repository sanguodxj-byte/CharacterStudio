using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents
{
    public class TimeStopHandler : IGlobalOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.TimeStop;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, LocalTargetInfo target, LocalTargetInfo dest, int nowTick)
            => AbilityTimeStopRuntimeController.ActivateForCaster(caster, config, nowTick);
    }

    public class WeatherChangeHandler : IGlobalOnApplyHandler
    {
        public AbilityRuntimeComponentType ComponentType => AbilityRuntimeComponentType.WeatherChange;
        public void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, LocalTargetInfo target, LocalTargetInfo dest, int nowTick)
        {
            var dummyConfig = new AbilityEffectConfig
            {
                type = AbilityEffectType.WeatherChange,
                weatherDefName = config.weatherDefName,
                weatherDurationTicks = config.weatherDurationTicks,
                weatherTransitionTicks = config.weatherTransitionTicks
            };
            EffectWorkerFactory.GetWorker(AbilityEffectType.WeatherChange).Apply(dummyConfig, target, caster);
        }
    }
}