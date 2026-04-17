using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents
{
    /// <summary>
    /// 运行时组件处理器注册表。
    /// 自动发现并索引所有 IRuntimeComponentHandler 实现，
    /// 为 CompAbilityEffect_Modular 提供按类型查找的 O(1) 分发。
    /// </summary>
    public static class RuntimeComponentHandlerRegistry
    {
        private static readonly Dictionary<AbilityRuntimeComponentType, IGlobalOnApplyHandler> _globalApplyHandlers
            = new Dictionary<AbilityRuntimeComponentType, IGlobalOnApplyHandler>();
        private static readonly Dictionary<AbilityRuntimeComponentType, IOnApplyHandler> _onApplyHandlers
            = new Dictionary<AbilityRuntimeComponentType, IOnApplyHandler>();
        private static readonly Dictionary<AbilityRuntimeComponentType, IPostHitHandler> _postHitHandlers
            = new Dictionary<AbilityRuntimeComponentType, IPostHitHandler>();
        private static readonly Dictionary<AbilityRuntimeComponentType, IDamageScaleModifier> _damageScaleModifiers
            = new Dictionary<AbilityRuntimeComponentType, IDamageScaleModifier>();
        private static readonly Dictionary<AbilityRuntimeComponentType, ITickHandler> _tickHandlers
            = new Dictionary<AbilityRuntimeComponentType, ITickHandler>();

        private static bool _initialized = false;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            // 全局施放处理器（不需要 skinComp）
            Register(new TimeStopHandler());
            Register(new WeatherChangeHandler());

            // OnApply 处理器
            Register(new SlotOverrideWindowHandler());
            Register(new HotkeyOverrideHandler());
            Register(new FollowupCooldownGateHandler());
            Register(new RStackDetonationHandler());
            Register(new PeriodicPulseHandler());
            Register(new ShieldAbsorbHandler());
            Register(new AttachedShieldVisualHandler());
            Register(new ProjectileInterceptorShieldHandler());
            Register(new ChainBounceHandler());
            Register(new DashEmpoweredStrikeHandler());
            Register(new FlightStateHandler());
            Register(new FlightOnlyFollowupHandler());
            Register(new FlightLandingBurstHandler());

            // Damage Scale Modifiers
            Register(new ExecuteBonusDamageHandler());
            Register(new MissingHealthBonusDamageHandler());
            Register(new FullHealthBonusDamageHandler());
            Register(new NearbyEnemyBonusDamageHandler());
            Register(new IsolatedTargetBonusDamageHandler());
            Register(new ComboStacksDamageHandler());
            Register(new PierceBonusDamageHandler());
            Register(new MarkDetonationDamageHandler());

            // Post-Hit Handlers
            Register(new MarkDetonationPostHitHandler());
            Register(new ComboStacksPostHitHandler());
            Register(new HitSlowFieldHandler());
            Register(new HitHealHandler());
            Register(new HitCooldownRefundHandler());
            Register(new ProjectileSplitHandler());
        }

        private static void Register(IRuntimeComponentHandler handler)
        {
            if (handler is IGlobalOnApplyHandler g) _globalApplyHandlers[handler.ComponentType] = g;
            if (handler is IOnApplyHandler a) _onApplyHandlers[handler.ComponentType] = a;
            if (handler is IPostHitHandler p) _postHitHandlers[handler.ComponentType] = p;
            if (handler is IDamageScaleModifier d) _damageScaleModifiers[handler.ComponentType] = d;
            if (handler is ITickHandler t) _tickHandlers[handler.ComponentType] = t;
        }

        public static bool TryGetGlobalApply(AbilityRuntimeComponentType type, out IGlobalOnApplyHandler? handler)
            => _globalApplyHandlers.TryGetValue(type, out handler);

        public static bool TryGetOnApply(AbilityRuntimeComponentType type, out IOnApplyHandler? handler)
            => _onApplyHandlers.TryGetValue(type, out handler);

        public static bool TryGetPostHit(AbilityRuntimeComponentType type, out IPostHitHandler? handler)
            => _postHitHandlers.TryGetValue(type, out handler);

        public static bool TryGetDamageScale(AbilityRuntimeComponentType type, out IDamageScaleModifier? handler)
            => _damageScaleModifiers.TryGetValue(type, out handler);

        public static bool TryGetTick(AbilityRuntimeComponentType type, out ITickHandler? handler)
            => _tickHandlers.TryGetValue(type, out handler);

        public static IEnumerable<KeyValuePair<AbilityRuntimeComponentType, IDamageScaleModifier>> AllDamageScaleModifiers => _damageScaleModifiers;
        public static IEnumerable<KeyValuePair<AbilityRuntimeComponentType, IPostHitHandler>> AllPostHitHandlers => _postHitHandlers;
        public static IEnumerable<KeyValuePair<AbilityRuntimeComponentType, ITickHandler>> AllTickHandlers => _tickHandlers;
    }
}