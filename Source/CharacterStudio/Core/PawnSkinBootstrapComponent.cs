using System;
using System.Collections.Generic;
using HarmonyLib;
using CharacterStudio.Abilities;
using CharacterStudio.Performance;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 游戏组件：为现有 Pawn 补充 CompPawnSkin，并自动应用 defaultForRace 皮肤、补发 Ability。
    /// 同时作为皮肤状态的持久化层——因为 CompPawnSkin 是动态添加的 ThingComp，
    /// RimWorld 不会在读档时重建它，所以需要通过 GameComponent 保存/恢复皮肤映射。
    /// </summary>
    public class PawnSkinBootstrapComponent : GameComponent
    {
        private struct PawnSkinSaveRecord : IExposable
        {
            public string? skinDefName;
            public bool fromDefaultRaceBinding;
            public CharacterAbilityLoadout? abilityLoadout;

            public void ExposeData()
            {
                Scribe_Values.Look(ref skinDefName, "skinDefName");
                Scribe_Values.Look(ref fromDefaultRaceBinding, "fromDefaultRaceBinding", false);
                Scribe_Deep.Look(ref abilityLoadout, "abilityLoadout");
            }
        }

        private Dictionary<int, PawnSkinSaveRecord> savedPawnSkins = new Dictionary<int, PawnSkinSaveRecord>();
        private static readonly System.Reflection.FieldInfo? CompsField =
            AccessTools.Field(typeof(ThingWithComps), "comps");

        private readonly HashSet<int> compEnsuredPawnIds = new HashSet<int>();
        private readonly HashSet<int> defaultSkinProcessedPawnIds = new HashSet<int>();
        private readonly HashSet<int> loadoutGrantedPawnIds = new HashSet<int>();

        private BootstrapWorldSignature? lastBootstrapSignature;

        public PawnSkinBootstrapComponent(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                CollectPawnSkinStates();
            }

            List<PawnSkinSaveRecordEntry>? saveList = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                saveList = new List<PawnSkinSaveRecordEntry>(savedPawnSkins.Count);
                foreach (var kv in savedPawnSkins)
                {
                    saveList.Add(new PawnSkinSaveRecordEntry { thingIdNumber = kv.Key, record = kv.Value });
                }
            }

            Scribe_Collections.Look(ref saveList, "savedPawnSkins", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars && saveList != null)
            {
                savedPawnSkins.Clear();
                for (int i = 0; i < saveList.Count; i++)
                {
                    savedPawnSkins[saveList[i].thingIdNumber] = saveList[i].record;
                }
            }
        }

        private void CollectPawnSkinStates()
        {
            savedPawnSkins.Clear();
            Game? game = Current.Game;
            if (game == null) return;

            foreach (Map map in game.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    CompPawnSkin? comp = pawn.GetComp<CompPawnSkin>();
                    if (comp == null || !comp.HasActiveSkin) continue;

                    var record = new PawnSkinSaveRecord
                    {
                        skinDefName = comp.ActiveSkin?.defName,
                        fromDefaultRaceBinding = comp.ActiveSkinFromDefaultRaceBinding,
                    };

                    var abilityComp = pawn.GetComp<CompCharacterAbilityRuntime>();
                    if (abilityComp != null && abilityComp.HasExplicitAbilityLoadout)
                    {
                        record.abilityLoadout = abilityComp.ActiveAbilityLoadout;
                    }

                    savedPawnSkins[pawn.thingIDNumber] = record;
                }
            }
        }

        private void RestorePawnSkinStates()
        {
            if (savedPawnSkins.Count == 0) return;

            Game? game = Current.Game;
            if (game == null) return;

            List<Pawn> allPawns = new List<Pawn>();
            foreach (Map map in game.Maps)
            {
                allPawns.AddRange(map.mapPawns.AllPawnsSpawned);
            }

            List<int> restored = new List<int>();
            foreach (Pawn pawn in allPawns)
            {
                if (!savedPawnSkins.TryGetValue(pawn.thingIDNumber, out var record))
                    continue;

                try
                {
                    if (string.IsNullOrWhiteSpace(record.skinDefName))
                    {
                        restored.Add(pawn.thingIDNumber);
                        continue;
                    }

                    CompPawnSkin? comp = pawn.GetComp<CompPawnSkin>();

                    if (comp == null)
                        continue;

                    if (comp.HasActiveSkin)
                    {
                        if (record.abilityLoadout != null)
                        {
                            var abilityComp = pawn.GetComp<CompCharacterAbilityRuntime>();
                            if (abilityComp != null)
                            {
                                abilityComp.ActiveAbilityLoadout = record.abilityLoadout;
                            }
                        }

                        AbilityLoadoutRuntimeUtility.GrantEffectiveLoadoutToPawn(pawn);

                        restored.Add(pawn.thingIDNumber);
                        continue;
                    }

                    PawnSkinDef? skinDef = PawnSkinDefRegistry.TryGet(record.skinDefName)
                        ?? DefDatabase<PawnSkinDef>.GetNamedSilentFail(record.skinDefName);

                    if (skinDef != null)
                    {
                        PawnSkinRuntimeUtility.ApplySkinToPawn(pawn, skinDef.Clone(),
                            fromDefaultRaceBinding: record.fromDefaultRaceBinding,
                            applicationSource: "BootstrapRestore");

                        Log.Message($"[CharacterStudio] 已从 GameComponent 恢复皮肤: {pawn.LabelShort} -> {record.skinDefName}");
                    }
                    else
                    {
                        Log.Warning($"[CharacterStudio] 恢复皮肤失败，定义未找到: {record.skinDefName} (pawn={pawn.LabelShort})");
                    }

                    if (record.abilityLoadout != null)
                    {
                        var abilityComp = pawn.GetComp<CompCharacterAbilityRuntime>();
                        if (abilityComp != null)
                        {
                            abilityComp.ActiveAbilityLoadout = record.abilityLoadout;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] 恢复单个 Pawn 皮肤失败: {pawn.LabelShort} - {ex.Message}");
                }

                restored.Add(pawn.thingIDNumber);
            }
        }

        private class PawnSkinSaveRecordEntry : IExposable
        {
            public int thingIdNumber;
            public PawnSkinSaveRecord record;

            public void ExposeData()
            {
                Scribe_Values.Look(ref thingIdNumber, "thingId");
                record.ExposeData();
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            RunBootstrapPass(BootstrapEntryPoint.LoadedGame);
            // 延迟恢复皮肤状态到下一帧，避免在存档加载期间触发大量纹理创建导致显存不足崩溃
            _pendingRestore = true;
        }

        private bool _pendingRestore = false;

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            Rendering.RuntimeAssetLoader.TickProcessPendingTextures();
            Abilities.VfxGlobalFilterManager.Tick();

            if (_pendingRestore)
            {
                _pendingRestore = false;
                try
                {
                    RestorePawnSkinStates();
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] RestorePawnSkinStates failed: {ex.Message}\n{ex.StackTrace}");
                }
            }
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

            // 如果 GameComponent 里有该 Pawn 的保存记录，跳过自动应用默认皮肤，
            // 等待 RestorePawnSkinStates 恢复真实皮肤。
            if (savedPawnSkins.ContainsKey(pawnId))
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

            // 文件日志
            try
            {
                var abilityComp = pawn.GetComp<Abilities.CompCharacterAbilityRuntime>();
                var hasLoadout = abilityComp?.HasExplicitAbilityLoadout ?? false;
                var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CS_Debug.log");
                System.IO.File.AppendAllText(logPath,
                    $"[{System.DateTime.Now:HH:mm:ss.fff}] TryGrantLoadoutOnce pawn={pawn.LabelShort ?? pawn.ThingID} hasExplicitLoadout={hasLoadout} comp={comp != null}\n");
            }
            catch { }

            if (comp == null)
                return false;

            CharacterAbilityLoadout? loadout = AbilityLoadoutRuntimeUtility.GetEffectiveLoadout(pawn);
            bool hasEffectiveAbilities = loadout?.abilities != null && loadout.abilities.Count > 0;

            // 读档恢复时必须同步"当前生效装配"，而不是仅同步皮肤模板技能。
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