using System.Collections.Generic;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 效果类型行为注册表。为每种 AbilityEffectType 提供 O(1) 的 IEffectBehavior 查找。
    /// </summary>
    public static class EffectBehaviorRegistry
    {
        private static readonly Dictionary<AbilityEffectType, IEffectBehavior> _behaviors
            = new Dictionary<AbilityEffectType, IEffectBehavior>();

        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            Register(AbilityEffectType.Damage, new DamageEffectBehavior());
            Register(AbilityEffectType.Heal, new HealEffectBehavior());
            Register(AbilityEffectType.Buff, new BuffEffectBehavior());
            Register(AbilityEffectType.Debuff, new DebuffEffectBehavior());
            Register(AbilityEffectType.Summon, new SummonEffectBehavior());
            Register(AbilityEffectType.Teleport, new TeleportEffectBehavior());
            Register(AbilityEffectType.Control, new ControlEffectBehavior());
            Register(AbilityEffectType.Terraform, new TerraformEffectBehavior());
            Register(AbilityEffectType.WeatherChange, new WeatherChangeEffectBehavior());
            Register(AbilityEffectType.Thought, new ThoughtEffectBehavior());
        }

        public static IEffectBehavior Get(AbilityEffectType type)
        {
            EnsureInitialized();
            if (_behaviors.TryGetValue(type, out var behavior)) return behavior;
            Log.Warning($"[CharacterStudio] No IEffectBehavior registered for {type}, using HealEffectBehavior as fallback");
            return _behaviors[AbilityEffectType.Heal];
        }

        private static void Register(AbilityEffectType type, IEffectBehavior behavior)
        {
            _behaviors[type] = behavior;
        }
    }
}
