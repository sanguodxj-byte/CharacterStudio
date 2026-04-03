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

        public CharacterAbilityLoadout? ActiveAbilityLoadout
        {
            get => activeAbilityLoadout;
            set => activeAbilityLoadout = value?.Clone();
        }

        public bool HasActiveSkin => activeSkin != null;
        public bool HasExplicitAbilityLoadout => activeAbilityLoadout != null;
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
            Scribe_Deep.Look(ref activeAbilityLoadout, "activeAbilityLoadout");
            abilityRuntimeState.ExposeData();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                activeAbilityLoadout ??= null;
                SkinLifecycleRecoveryCoordinator.RestoreAfterLoad(this);
            }

            abilityRuntimeState.Normalize();
        }
    }
}
