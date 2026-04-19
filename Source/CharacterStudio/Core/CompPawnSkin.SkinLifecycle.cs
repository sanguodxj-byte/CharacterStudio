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

            // CompCharacterAbilityRuntime 是运行时动态添加的 ThingComp，
            // RimWorld 不会序列化它。因此通过 CompPawnSkin 代理保存/恢复 activeAbilityLoadout。
            CharacterAbilityLoadout? loadoutForSerialize = null;
            if (Scribe.mode != LoadSaveMode.LoadingVars)
            {
                // 保存时：从 CompCharacterAbilityRuntime 读取当前 loadout
                var abilityComp = Pawn?.GetComp<Abilities.CompCharacterAbilityRuntime>();
                if (abilityComp != null && abilityComp.HasExplicitAbilityLoadout)
                {
                    loadoutForSerialize = abilityComp.ActiveAbilityLoadout;
                }
            }

            Scribe_Deep.Look(ref loadoutForSerialize, "activeAbilityLoadout");

            // 文件日志——绕过 RimWorld 日志系统
            try
            {
                var logPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "CS_Debug.log");
                var msg = $"[{System.DateTime.Now:HH:mm:ss.fff}] PostExposeData mode={Scribe.mode}";
                System.IO.File.AppendAllText(logPath, msg + "\n");
            }
            catch { }

            if (Scribe.mode == LoadSaveMode.LoadingVars && loadoutForSerialize != null)
            {
                _migratedLoadout = loadoutForSerialize;
            }

            // 旧存档的 abilityRuntimeState 迁移：读取并丢弃
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                var legacyState = new Abilities.AbilityHotkeyRuntimeState();
                legacyState.ExposeData();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SkinLifecycleRecoveryCoordinator.RestoreAfterLoad(this);

                if (_migratedLoadout != null)
                {
                    var abilityComp = Pawn?.GetComp<Abilities.CompCharacterAbilityRuntime>();
                    try
                    {
                        var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CS_Debug.log");
                        System.IO.File.AppendAllText(logPath,
                            $"[{System.DateTime.Now:HH:mm:ss.fff}] PostLoadInit migratedLoadout abilities={_migratedLoadout.abilities?.Count ?? -1} abilityComp={abilityComp != null} pawn={Pawn?.LabelShort ?? "null"}\n");
                    }
                    catch { }

                    if (abilityComp != null)
                    {
                        abilityComp.ActiveAbilityLoadout = _migratedLoadout;
                        _migratedLoadout = null;
                    }
                }
            }
        }

        private CharacterAbilityLoadout? _migratedLoadout;
    }
}
