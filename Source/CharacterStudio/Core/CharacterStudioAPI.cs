using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 面向外部模组的轻量级公开门面。
    /// 只暴露稳定、低耦合的查询与注入入口，避免外部直接依赖内部协同细节。
    /// </summary>
    public static class CharacterStudioAPI
    {
        public static bool IsCustomized(Pawn? pawn)
            => pawn?.GetComp<CompPawnSkin>()?.HasActiveSkin == true;

        public static PawnSkinDef? GetActiveSkin(Pawn? pawn)
            => pawn?.GetComp<CompPawnSkin>()?.ActiveSkin?.Clone();

        public static CharacterAbilityLoadout? GetExplicitAbilityLoadout(Pawn? pawn)
            => AbilityLoadoutRuntimeUtility.GetExplicitLoadout(pawn)?.Clone();

        public static CharacterAbilityLoadout? GetEffectiveAbilityLoadout(Pawn? pawn)
            => AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(pawn)?.Clone();

        public static IReadOnlyCollection<string> GetGrantedAbilityNames(Pawn? pawn)
            => pawn == null ? Array.Empty<string>() : AbilityGrantUtility.GetGrantedAbilityNames(pawn);

        public static PawnSkinDef RegisterOrReplaceSkin(PawnSkinDef def)
            => PawnSkinDefRegistry.RegisterOrReplace(def);

        public static void GrantAbilities(Pawn pawn, IEnumerable<ModularAbilityDef> abilities)
            => AbilityGrantUtility.GrantAbilitiesToPawn(pawn, abilities);

        public static void GrantEffectiveLoadout(Pawn pawn)
            => AbilityLoadoutRuntimeUtility.GrantEffectiveLoadoutToPawn(pawn);

        public static void RevokeGrantedAbilities(Pawn pawn)
            => AbilityGrantUtility.RevokeAllCSAbilitiesFromPawn(pawn);

        public static void RegisterLayerAnimationProvider(string id, CharacterStudioLayerAnimationProvider provider)
            => CharacterStudioLayerAnimationRegistry.RegisterProvider(id, provider);

        public static void RegisterLayerAnimationProvider(
            string id,
            CharacterStudioLayerAnimationMatcher matcher,
            CharacterStudioLayerAnimationProvider provider)
            => CharacterStudioLayerAnimationRegistry.RegisterProvider(id, matcher, provider);

        public static CharacterSpawnProfileDef RegisterRuntimeSpawnProfile(CharacterSpawnProfileDef def)
            => CharacterSpawnProfileRegistry.RegisterOrReplace(def);

        public static CharacterRuntimeTriggerDef RegisterRuntimeTrigger(CharacterRuntimeTriggerDef def)
            => CharacterRuntimeTriggerRegistry.RegisterOrReplace(def);

        public static Pawn? TrySpawnRuntimeSpawnProfile(
            string profileDefName,
            Map map,
            IntVec3? spawnCell = null,
            CharacterSpawnSettings? overrideSettings = null,
            string applicationSource = "ExternalAPI")
        {
            CharacterSpawnProfileDef? profile = CharacterSpawnProfileRegistry.TryGet(profileDefName);
            if (profile == null)
            {
                return null;
            }

            return CharacterRuntimeTriggerExecutor.TrySpawnProfile(profile, map, spawnCell, overrideSettings, applicationSource);
        }

        public static Pawn? TrySpawnRuntimeSpawnProfile(
            CharacterSpawnProfileDef profile,
            Map map,
            IntVec3? spawnCell = null,
            CharacterSpawnSettings? overrideSettings = null,
            string applicationSource = "ExternalAPI")
            => CharacterRuntimeTriggerExecutor.TrySpawnProfile(profile, map, spawnCell, overrideSettings, applicationSource);

        public static bool UnregisterRuntimeSpawnProfile(string defName)
            => CharacterSpawnProfileRegistry.Unregister(defName);

        public static bool UnregisterRuntimeTrigger(string defName)
            => CharacterRuntimeTriggerRegistry.Unregister(defName);

        public static IReadOnlyCollection<CharacterSpawnProfileDef> GetRegisteredRuntimeSpawnProfiles()
            => CharacterSpawnProfileRegistry.AllProfiles.ToList().AsReadOnly();

        public static IReadOnlyCollection<CharacterRuntimeTriggerDef> GetRegisteredRuntimeTriggers()
            => CharacterRuntimeTriggerRegistry.AllTriggers.ToList().AsReadOnly();

        public static bool UnregisterLayerAnimationProvider(string id)
            => CharacterStudioLayerAnimationRegistry.UnregisterProvider(id);

        public static event Action<Pawn, PawnSkinDef?, bool, bool, string>? SkinChangedGlobal
        {
            add => CompPawnSkin.SkinChangedGlobal += value;
            remove => CompPawnSkin.SkinChangedGlobal -= value;
        }

        public static event Action<Pawn, IReadOnlyCollection<string>>? AbilitiesGrantedGlobal
        {
            add => AbilityGrantUtility.AbilitiesGrantedGlobal += value;
            remove => AbilityGrantUtility.AbilitiesGrantedGlobal -= value;
        }

        public static event Action<Pawn, IReadOnlyCollection<string>>? AbilitiesRevokedGlobal
        {
            add => AbilityGrantUtility.AbilitiesRevokedGlobal += value;
            remove => AbilityGrantUtility.AbilitiesRevokedGlobal -= value;
        }

        public static event Action<PawnSkinDef, bool>? RuntimeSkinRegisteredGlobal
        {
            add => PawnSkinDefRegistry.RuntimeSkinRegisteredGlobal += value;
            remove => PawnSkinDefRegistry.RuntimeSkinRegisteredGlobal -= value;
        }

        public static event Action<CharacterSpawnProfileDef, bool>? RuntimeSpawnProfileRegisteredGlobal
        {
            add => CharacterSpawnProfileRegistry.RuntimeSpawnProfileRegisteredGlobal += value;
            remove => CharacterSpawnProfileRegistry.RuntimeSpawnProfileRegisteredGlobal -= value;
        }

        public static event Action<CharacterRuntimeTriggerDef, bool>? RuntimeTriggerRegisteredGlobal
        {
            add => CharacterRuntimeTriggerRegistry.RuntimeTriggerRegisteredGlobal += value;
            remove => CharacterRuntimeTriggerRegistry.RuntimeTriggerRegisteredGlobal -= value;
        }

        public static event Action<CharacterRuntimeTriggerDef, CharacterSpawnProfileDef?, Map, Pawn?>? RuntimeTriggerFiredGlobal
        {
            add => CharacterRuntimeTriggerExecutor.RuntimeTriggerFiredGlobal += value;
            remove => CharacterRuntimeTriggerExecutor.RuntimeTriggerFiredGlobal -= value;
        }
    }
}
