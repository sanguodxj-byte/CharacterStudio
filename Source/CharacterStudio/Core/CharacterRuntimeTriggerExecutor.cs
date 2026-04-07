using System;
using CharacterStudio.Design;
using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    public static class CharacterRuntimeTriggerExecutor
    {
        public static event Action<CharacterRuntimeTriggerDef, CharacterSpawnProfileDef?, Map, Pawn?>? RuntimeTriggerFiredGlobal;

        public static Pawn? TrySpawnProfile(
            CharacterSpawnProfileDef? profile,
            Map? map,
            IntVec3? spawnCellOverride = null,
            CharacterSpawnSettings? spawnSettingsOverride = null,
            string applicationSource = "ExternalAPI")
        {
            if (profile == null || map == null)
            {
                return null;
            }

            return TrySpawnProfileInternal(profile, map, spawnCellOverride, spawnSettingsOverride, applicationSource);
        }

        public static bool TryExecute(CharacterRuntimeTriggerDef? trigger, Map? map)
        {
            if (trigger == null || map == null || !trigger.enabled)
            {
                return false;
            }

            return TrySpawnAndNotify(trigger, map);
        }

        private static bool TrySpawnAndNotify(CharacterRuntimeTriggerDef trigger, Map map)
        {
            CharacterSpawnProfileDef? profile = CharacterSpawnProfileRegistry.TryGet(trigger.spawnProfileDefName);
            if (profile == null)
            {
                Log.Warning($"[CharacterStudio] 运行时触发器 {trigger.defName} 缺少可用的角色配置 {trigger.spawnProfileDefName}");
                return false;
            }

            Pawn? spawnedPawn = TrySpawnProfileInternal(
                profile,
                map,
                trigger.spawnNearColonist ? (IntVec3?)null : map.Center,
                trigger.spawnSettings,
                $"RuntimeTrigger:{trigger.defName}");
            if (spawnedPawn == null)
            {
                return false;
            }

            RuntimeTriggerFiredGlobal?.Invoke(trigger, profile, map, spawnedPawn);
            return true;
        }

        private static Pawn? TrySpawnProfileInternal(
            CharacterSpawnProfileDef profile,
            Map map,
            IntVec3? spawnCellOverride,
            CharacterSpawnSettings? spawnSettingsOverride,
            string applicationSource)
        {
            PawnSkinDef runtimeSkin = profile.ResolveSkin()?.Clone() ?? new PawnSkinDef
            {
                defName = $"{profile.defName}_RuntimeSkin",
                label = profile.GetDisplayLabel()
            };

            ThingDef raceDef = profile.ResolveRaceDef();
            CharacterDefinition characterDefinition = profile.characterDefinition?.Clone() ?? new CharacterDefinition();
            characterDefinition.runtimeTriggers?.Clear();
            characterDefinition.EnsureDefaults(runtimeSkin.defName ?? profile.defName ?? "CS_Character", raceDef, runtimeSkin.attributes);

            CharacterApplicationPlan plan = new CharacterApplicationPlan
            {
                source = applicationSource,
                runtimeSkin = runtimeSkin,
                spawnAsNewPawn = true,
                spawnRaceDef = raceDef,
                spawnPawnKind = profile.ResolvePawnKindDef(),
                spawnFaction = profile.ResolveFaction(map),
                spawnMap = map,
                desiredSpawnCell = spawnCellOverride ?? CharacterSpawnUtility.ResolveSpawnOrigin(map, null),
                spawnSettings = spawnSettingsOverride?.Clone() ?? new CharacterSpawnSettings(),
                characterDefinition = characterDefinition
            };

            plan.spawnSettings.sourceMapForConditionCheck = map;

            if (!CharacterApplicationExecutor.Execute(plan) || plan.targetPawn == null)
            {
                Log.Warning($"[CharacterStudio] 角色生成失败：{profile.defName}");
                return null;
            }

            return plan.targetPawn;
        }
    }
}
