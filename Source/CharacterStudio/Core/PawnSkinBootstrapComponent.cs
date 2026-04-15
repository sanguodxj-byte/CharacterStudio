using System.Collections.Generic;
using HarmonyLib;
using CharacterStudio.Abilities;
using CharacterStudio.Performance;
using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 游戏组件：为现有 Pawn 补充 CompPawnSkin，并自动应用 defaultForRace 皮肤、补发 Ability。
    /// 优化：
    /// 1. 将原来的三次全图扫描合并为单次扫描。
    /// 2. 使用世界签名去重，避免 LoadedGame / StartedNewGame / FinalizeInit 对同一世界状态重复执行。
    /// 3. 对每个 Pawn 记录已完成的 bootstrap 项，避免世界状态变化后重复处理老 Pawn。
    /// </summary>
    public class PawnSkinBootstrapComponent : GameComponent
    {
        private static readonly System.Reflection.FieldInfo? CompsField =
            AccessTools.Field(typeof(ThingWithComps), "comps");

        private readonly HashSet<int> compEnsuredPawnIds = new HashSet<int>();
        private readonly HashSet<int> defaultSkinProcessedPawnIds = new HashSet<int>();
        private readonly HashSet<int> loadoutGrantedPawnIds = new HashSet<int>();

        private BootstrapWorldSignature? lastBootstrapSignature;

        public PawnSkinBootstrapComponent(Game game) { }

        public override void LoadedGame()
        {
            base.LoadedGame();
            RunBootstrapPass(BootstrapEntryPoint.LoadedGame);
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            RunBootstrapPass(BootstrapEntryPoint.StartedNewGame);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RunBootstrapPass(BootstrapEntryPoint.FinalizeInit);
        }

        private void RunBootstrapPass(BootstrapEntryPoint entryPoint)
        {
            CharacterStudioPerformanceStats.RecordBootstrapEntryCall(entryPoint);

            Game? game = Current.Game;
            if (game == null)
                return;

            BootstrapWorldSignature signature = CaptureWorldSignature(game);
            if (lastBootstrapSignature.HasValue && lastBootstrapSignature.Value.Equals(signature))
            {
                CharacterStudioPerformanceStats.RecordBootstrapPassSkipped(entryPoint, signature);
                return;
            }

            int mapsVisited = 0;
            int pawnsVisited = 0;
            int compsAdded = 0;
            int defaultSkinsApplied = 0;
            int loadoutsGranted = 0;

            foreach (Map map in game.Maps)
            {
                mapsVisited++;

                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    pawnsVisited++;

                    CompPawnSkin? comp = EnsureSkinCompForPawn(pawn, ref compsAdded);

                    if (!pawn.RaceProps.Humanlike)
                        continue;

                    if (TryApplyDefaultSkinOnce(pawn, comp))
                        defaultSkinsApplied++;

                    if (TryGrantLoadoutOnce(pawn, comp))
                        loadoutsGranted++;
                }
            }

            lastBootstrapSignature = signature;
            CharacterStudioPerformanceStats.RecordBootstrapPassExecuted(
                entryPoint,
                signature,
                mapsVisited,
                pawnsVisited,
                compsAdded,
                defaultSkinsApplied,
                loadoutsGranted);
        }

        private static BootstrapWorldSignature CaptureWorldSignature(Game game)
        {
            int mapCount = 0;
            int spawnedPawnCount = 0;
            int mapIdHash = 17;

            foreach (Map map in game.Maps)
            {
                mapCount++;
                spawnedPawnCount += map.mapPawns.AllPawnsSpawned.Count;
                unchecked
                {
                    mapIdHash = (mapIdHash * 31) + map.uniqueID;
                }
            }

            return new BootstrapWorldSignature(mapCount, spawnedPawnCount, mapIdHash);
        }

        private CompPawnSkin? EnsureSkinCompForPawn(Pawn pawn, ref int compsAdded)
        {
            int pawnId = pawn.thingIDNumber;
            if (compEnsuredPawnIds.Contains(pawnId))
                return pawn.GetComp<CompPawnSkin>();

            CompPawnSkin? comp = pawn.GetComp<CompPawnSkin>();
            if (comp == null && AddCompToPawn(pawn))
            {
                compsAdded++;
                comp = pawn.GetComp<CompPawnSkin>();
            }

            if (comp != null)
                compEnsuredPawnIds.Add(pawnId);

            return comp;
        }

        private bool TryApplyDefaultSkinOnce(Pawn pawn, CompPawnSkin? comp)
        {
            int pawnId = pawn.thingIDNumber;
            if (defaultSkinProcessedPawnIds.Contains(pawnId))
                return false;

            if (comp == null)
                return false;

            defaultSkinProcessedPawnIds.Add(pawnId);

            if (comp.HasActiveSkin)
                return false;

            if (!ShouldAutoApplyDefaultSkinDuringBootstrap(pawn, comp))
                return false;

            PawnSkinDef? skin = PawnSkinDefRegistry.GetDefaultSkinForRace(pawn.def);
            if (skin == null)
                return false;

            comp.ActiveSkin = skin.Clone();
            return true;
        }

        private static bool ShouldAutoApplyDefaultSkinDuringBootstrap(Pawn pawn, CompPawnSkin comp)
        {
            if (pawn == null || comp == null)
                return false;

            if (!pawn.RaceProps.Humanlike)
                return false;

            if (comp.HasActiveSkin)
                return false;

            if (!string.IsNullOrWhiteSpace(comp.ActiveSkinApplicationSource))
                return false;

            if (comp.ActiveSkinFromDefaultRaceBinding)
                return true;

            bool hasExplicitAppearanceState = !string.IsNullOrWhiteSpace(comp.ActiveSkinApplicationSource)
                || !string.IsNullOrWhiteSpace(GetSerializedActiveSkinDefName(comp));
            if (hasExplicitAppearanceState)
                return false;

            return pawn.Faction == Faction.OfPlayer
                || pawn.IsColonist
                || pawn.IsPrisonerOfColony
                || pawn.IsSlaveOfColony;
        }

        private static string? GetSerializedActiveSkinDefName(CompPawnSkin comp)
        {
            var field = AccessTools.Field(typeof(CompPawnSkin), "activeSkinDefName");
            return field?.GetValue(comp) as string;
        }

        private bool TryGrantLoadoutOnce(Pawn pawn, CompPawnSkin? comp)
        {
            int pawnId = pawn.thingIDNumber;
            if (loadoutGrantedPawnIds.Contains(pawnId))
                return false;

            loadoutGrantedPawnIds.Add(pawnId);
            if (comp == null)
                return false;

            CharacterAbilityLoadout? loadout = AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(pawn);
            bool hasEffectiveAbilities = loadout?.abilities != null && loadout.abilities.Count > 0;

            // 读档恢复时必须同步“当前生效装配”，而不是仅同步皮肤模板技能。
            // 否则显式 loadout 会在存档恢复后被皮肤技能覆盖，导致热键、Gizmo 与运行时 Ability 链路失配。
            AbilityLoadoutRuntimeUtility.GrantEffectiveLoadoutToPawn(pawn);
            return hasEffectiveAbilities;
        }

        private static bool AddCompToPawn(Pawn pawn)
        {
            if (CompsField == null)
            {
                Log.Error("[CharacterStudio] Failed to find ThingWithComps.comps field.");
                return false;
            }

            List<ThingComp>? comps = CompsField.GetValue(pawn) as List<ThingComp>;
            if (comps == null)
            {
                comps = new List<ThingComp>();
                CompsField.SetValue(pawn, comps);
            }

            foreach (ThingComp existing in comps)
            {
                if (existing is CompPawnSkin)
                    return false;
            }

            var comp = new CompPawnSkin { parent = pawn };
            comps.Add(comp);
            comp.Initialize(new CompProperties_PawnSkin());
            comp.PostSpawnSetup(pawn.Spawned);

            // Also ensure CompCharacterAbilityRuntime exists for ability state management
            EnsureAbilityRuntimeComp(pawn, comps);

            Log.Message($"[CharacterStudio] Added CompPawnSkin to existing pawn: {pawn.LabelShort}");
            return true;
        }

        private static void EnsureAbilityRuntimeComp(Pawn pawn, List<ThingComp> comps)
        {
            foreach (ThingComp existing in comps)
            {
                if (existing is CompCharacterAbilityRuntime)
                    return;
            }

            var abilityComp = new CompCharacterAbilityRuntime { parent = pawn };
            comps.Add(abilityComp);
            abilityComp.Initialize(new CompProperties_CharacterAbilityRuntime());
        }

        public static void ApplyDefaultSkinsToCurrentGame()
        {
            Game? game = Current.Game;
            if (game == null)
            {
                return;
            }

            foreach (Map map in game.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn == null || !pawn.RaceProps.Humanlike)
                        continue;

                    CompPawnSkin? comp = pawn.GetComp<CompPawnSkin>();
                    if (comp == null || !ShouldAutoApplyDefaultSkinDuringBootstrap(pawn, comp))
                        continue;

                    PawnSkinDef? defaultSkin = PawnSkinDefRegistry.GetDefaultSkinForRace(pawn.def);
                    if (defaultSkin != null)
                        PawnSkinRuntimeUtility.ApplySkinToPawn(pawn, defaultSkin.Clone(), fromDefaultRaceBinding: true);
                }
            }
        }
    }
}