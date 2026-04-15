using CharacterStudio.Abilities;
using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    public partial class CompPawnSkin
    {
        public PawnSkinDef? ActiveSkin
        {
            get => activeSkin;
            set
            {
                if (activeSkin != value || activeSkinFromDefaultRaceBinding)
                {
                    SkinApplicationCoordinator.ApplyManual(this, value);
                }
            }
        }

        // ActiveAbilityLoadout is now on CompCharacterAbilityRuntime
        // This property forwards for backward compatibility
        public CharacterAbilityLoadout? ActiveAbilityLoadout
        {
            get => AbilityComp?.ActiveAbilityLoadout;
            set { if (AbilityComp != null) AbilityComp.ActiveAbilityLoadout = value; }
        }

        public bool HasActiveSkin => activeSkin != null;
        public bool HasExplicitAbilityLoadout => AbilityComp?.HasExplicitAbilityLoadout ?? false;
        public bool ActiveSkinFromDefaultRaceBinding => activeSkinFromDefaultRaceBinding;
        public bool ActiveSkinPreviewMode => activeSkinPreviewMode;
        public string ActiveSkinApplicationSource => activeSkinApplicationSource ?? string.Empty;
        public bool ShouldInjectEquipmentRenderDataDirectly => activeSkinPreviewMode;

        private void RaiseSkinChangedGlobal()
        {
            Pawn? pawn = Pawn;
            if (pawn == null)
            {
                return;
            }

            SkinChangedGlobal?.Invoke(
                pawn,
                activeSkin,
                activeSkinFromDefaultRaceBinding,
                activeSkinPreviewMode,
                ActiveSkinApplicationSource);
        }

        internal SkinApplicationWriteResult SetActiveSkinWithSource(PawnSkinDef? skin, bool fromDefaultRaceBinding)
        {
            return SkinApplicationCoordinator.SetWithSourceSilently(this, skin, fromDefaultRaceBinding, false, string.Empty);
        }

        internal SkinApplicationWriteResult SetActiveSkinWithSource(
            PawnSkinDef? skin,
            bool fromDefaultRaceBinding,
            bool previewMode,
            string applicationSource)
        {
            return SkinApplicationCoordinator.SetWithSourceSilently(
                this,
                skin,
                fromDefaultRaceBinding,
                previewMode,
                applicationSource);
        }

        private void SyncXenotype(PawnSkinDef? skin)
        {
            if (skin == null || string.IsNullOrEmpty(skin.xenotypeDefName))
                return;

            var pawn = Pawn;
            if (pawn == null || pawn.genes == null)
                return;

            if (!pawn.Spawned)
                return;

            var xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(skin.xenotypeDefName);
            if (xenotype == null)
            {
                Log.Warning($"[CharacterStudio] SyncXenotype: XenotypeDef '{skin.xenotypeDefName}' not found for skin '{skin.defName}'.");
                return;
            }

            pawn.genes.SetXenotype(xenotype);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            SkinLifecycleRecoveryCoordinator.RestoreAfterSpawn(this);
        }

        internal SkinApplicationWriteResult ClearSkinWithResult()
        {
            return SkinApplicationCoordinator.Clear(this);
        }

        private void TryApplyDefaultRaceSkinIfNeeded()
        {
            SkinLifecycleRecoveryCoordinator.TryApplyDefaultRaceSkin(this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref activeSkinDefName, "activeSkinDefName");
            Scribe_Values.Look(ref activeSkinFromDefaultRaceBinding, "activeSkinFromDefaultRaceBinding", false);

            // Migration: read old ability data from CompPawnSkin and forward to CompCharacterAbilityRuntime
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // Old saves had activeAbilityLoadout here - forward to new component
                CharacterAbilityLoadout? migratedLoadout = null;
                Scribe_Deep.Look(ref migratedLoadout, "activeAbilityLoadout");
                if (migratedLoadout != null)
                {
                    // Will be applied after comps are initialized
                    _migratedLoadout = migratedLoadout;
                }

                // Old saves had abilityRuntimeState.ExposeData() here
                // Read and discard - the data is also saved in CompCharacterAbilityRuntime now
                var legacyState = new AbilityHotkeyRuntimeState();
                legacyState.ExposeData();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SkinLifecycleRecoveryCoordinator.RestoreAfterLoad(this);

                // Apply migrated loadout (only clear if successfully forwarded)
                if (_migratedLoadout != null)
                {
                    var abilityComp = Pawn?.GetComp<CompCharacterAbilityRuntime>();
                    if (abilityComp != null)
                    {
                        abilityComp.ActiveAbilityLoadout = _migratedLoadout;
                        _migratedLoadout = null;
                    }
                    // If abilityComp is null (old save before comp existed),
                    // retain _migratedLoadout for later retry via PawnSkinBootstrapComponent
                }
            }
        }

        private CharacterAbilityLoadout? _migratedLoadout;
    }
}
