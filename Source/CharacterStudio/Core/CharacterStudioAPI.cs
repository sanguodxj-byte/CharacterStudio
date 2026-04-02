using System;
using System.Collections.Generic;
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
    }
}
