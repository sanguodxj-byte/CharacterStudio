using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents
{
    public static class RuntimeComponentRegistry
    {
        private static Dictionary<AbilityRuntimeComponentType, Type>? _typeMap;
        private static Dictionary<AbilityRuntimeComponentType, AbilityRuntimeComponentConfig>? _instanceCache;

        private static void EnsureInitialized()
        {
            if (_typeMap != null && _instanceCache != null) return;
            _typeMap = new Dictionary<AbilityRuntimeComponentType, Type>();
            _instanceCache = new Dictionary<AbilityRuntimeComponentType, AbilityRuntimeComponentConfig>();
            
            var types = typeof(AbilityRuntimeComponentConfig).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(AbilityRuntimeComponentConfig).IsAssignableFrom(t));
                
            foreach (var t in types)
            {
                var inst = (AbilityRuntimeComponentConfig)Activator.CreateInstance(t);
                _typeMap[inst.type] = t;
                _instanceCache[inst.type] = inst;
            }
        }

        public static AbilityRuntimeComponentConfig? Create(AbilityRuntimeComponentType type)
        {
            EnsureInitialized();
            if (_typeMap != null && _typeMap.TryGetValue(type, out var t))
            {
                var inst = (AbilityRuntimeComponentConfig)Activator.CreateInstance(t)!;
                inst.enabled = true;
                return inst;
            }
            return null;
        }

        public static bool IsSingleton(AbilityRuntimeComponentType type)
        {
            EnsureInitialized();
            if (_instanceCache != null && _instanceCache.TryGetValue(type, out var inst))
            {
                return inst.IsSingleton;
            }
            return false;
        }

        public static string GetDescription(AbilityRuntimeComponentType type)
        {
            return $"CS_Studio_Runtime_Desc_{type}".Translate();
        }
        
        public static string GetPreviewSummary(AbilityRuntimeComponentConfig config)
        {
            return config.GetPreviewSummary();
        }
    }
}
