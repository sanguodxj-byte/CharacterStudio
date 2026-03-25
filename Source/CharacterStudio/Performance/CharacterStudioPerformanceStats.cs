using System;
using System.Collections.Generic;
using Verse;

namespace CharacterStudio.Performance
{
    public enum BootstrapEntryPoint
    {
        LoadedGame,
        StartedNewGame,
        FinalizeInit
    }

    public readonly struct BootstrapWorldSignature : IEquatable<BootstrapWorldSignature>
    {
        public readonly int mapCount;
        public readonly int spawnedPawnCount;
        public readonly int mapIdHash;

        public BootstrapWorldSignature(int mapCount, int spawnedPawnCount, int mapIdHash)
        {
            this.mapCount = mapCount;
            this.spawnedPawnCount = spawnedPawnCount;
            this.mapIdHash = mapIdHash;
        }

        public bool Equals(BootstrapWorldSignature other)
        {
            return mapCount == other.mapCount
                && spawnedPawnCount == other.spawnedPawnCount
                && mapIdHash == other.mapIdHash;
        }

        public override bool Equals(object? obj)
        {
            return obj is BootstrapWorldSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = mapCount;
                hash = (hash * 397) ^ spawnedPawnCount;
                hash = (hash * 397) ^ mapIdHash;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{mapCount} maps / {spawnedPawnCount} pawns / hash {mapIdHash}";
        }
    }

    public sealed class CharacterStudioPerformanceSnapshot
    {
        public bool captureEnabled;
        public int bootstrapLoadedGameCalls;
        public int bootstrapStartedNewGameCalls;
        public int bootstrapFinalizeInitCalls;
        public int bootstrapPassesExecuted;
        public int bootstrapPassesSkipped;
        public int bootstrapMapsVisited;
        public int bootstrapPawnsVisited;
        public int bootstrapCompsAdded;
        public int bootstrapDefaultSkinsApplied;
        public int bootstrapLoadoutsGranted;
        public string lastBootstrapEntryPoint = "N/A";
        public string lastBootstrapSignature = "N/A";
        public int renderRefreshRequests;
        public int renderRefreshDispatched;
        public int renderRefreshCoalesced;
        public int lastRenderRefreshTick = -1;

        public float RenderRefreshCoalescedRate
        {
            get
            {
                if (renderRefreshRequests <= 0)
                    return 0f;

                return (float)renderRefreshCoalesced / renderRefreshRequests;
            }
        }
    }

    public static class CharacterStudioPerformanceStats
    {
        private static Game? trackedGame;
        private static readonly Dictionary<int, int> lastRefreshTickByPawnId = new Dictionary<int, int>();

        public static bool CaptureEnabled { get; set; } = true;

        private static int bootstrapLoadedGameCalls;
        private static int bootstrapStartedNewGameCalls;
        private static int bootstrapFinalizeInitCalls;
        private static int bootstrapPassesExecuted;
        private static int bootstrapPassesSkipped;
        private static int bootstrapMapsVisited;
        private static int bootstrapPawnsVisited;
        private static int bootstrapCompsAdded;
        private static int bootstrapDefaultSkinsApplied;
        private static int bootstrapLoadoutsGranted;
        private static string lastBootstrapEntryPoint = "N/A";
        private static string lastBootstrapSignature = "N/A";

        private static int renderRefreshRequests;
        private static int renderRefreshDispatched;
        private static int renderRefreshCoalesced;
        private static int lastRenderRefreshTick = -1;

        public static void RecordBootstrapEntryCall(BootstrapEntryPoint entryPoint)
        {
            EnsureGameContext();

            if (!CaptureEnabled)
                return;

            switch (entryPoint)
            {
                case BootstrapEntryPoint.LoadedGame:
                    bootstrapLoadedGameCalls++;
                    break;
                case BootstrapEntryPoint.StartedNewGame:
                    bootstrapStartedNewGameCalls++;
                    break;
                case BootstrapEntryPoint.FinalizeInit:
                    bootstrapFinalizeInitCalls++;
                    break;
            }
        }

        public static void RecordBootstrapPassExecuted(
            BootstrapEntryPoint entryPoint,
            BootstrapWorldSignature signature,
            int mapsVisited,
            int pawnsVisited,
            int compsAdded,
            int defaultSkinsApplied,
            int loadoutsGranted)
        {
            EnsureGameContext();

            if (!CaptureEnabled)
                return;

            bootstrapPassesExecuted++;
            bootstrapMapsVisited += mapsVisited;
            bootstrapPawnsVisited += pawnsVisited;
            bootstrapCompsAdded += compsAdded;
            bootstrapDefaultSkinsApplied += defaultSkinsApplied;
            bootstrapLoadoutsGranted += loadoutsGranted;
            lastBootstrapEntryPoint = entryPoint.ToString();
            lastBootstrapSignature = signature.ToString();
        }

        public static void RecordBootstrapPassSkipped(BootstrapEntryPoint entryPoint, BootstrapWorldSignature signature)
        {
            EnsureGameContext();

            if (!CaptureEnabled)
                return;

            bootstrapPassesSkipped++;
            lastBootstrapEntryPoint = $"{entryPoint} (Skipped)";
            lastBootstrapSignature = signature.ToString();
        }

        public static bool TryBeginRenderRefresh(Pawn pawn, int currentTick)
        {
            EnsureGameContext();

            bool dispatched = true;
            if (pawn != null && currentTick >= 0)
            {
                int pawnId = pawn.thingIDNumber;
                if (lastRefreshTickByPawnId.TryGetValue(pawnId, out int lastTick) && lastTick == currentTick)
                {
                    dispatched = false;
                }
                else
                {
                    lastRefreshTickByPawnId[pawnId] = currentTick;
                }
            }

            if (CaptureEnabled)
            {
                renderRefreshRequests++;
                if (dispatched)
                {
                    renderRefreshDispatched++;
                    lastRenderRefreshTick = currentTick;
                }
                else
                {
                    renderRefreshCoalesced++;
                }
            }

            return dispatched;
        }

        public static CharacterStudioPerformanceSnapshot CreateSnapshot()
        {
            EnsureGameContext();

            return new CharacterStudioPerformanceSnapshot
            {
                captureEnabled = CaptureEnabled,
                bootstrapLoadedGameCalls = bootstrapLoadedGameCalls,
                bootstrapStartedNewGameCalls = bootstrapStartedNewGameCalls,
                bootstrapFinalizeInitCalls = bootstrapFinalizeInitCalls,
                bootstrapPassesExecuted = bootstrapPassesExecuted,
                bootstrapPassesSkipped = bootstrapPassesSkipped,
                bootstrapMapsVisited = bootstrapMapsVisited,
                bootstrapPawnsVisited = bootstrapPawnsVisited,
                bootstrapCompsAdded = bootstrapCompsAdded,
                bootstrapDefaultSkinsApplied = bootstrapDefaultSkinsApplied,
                bootstrapLoadoutsGranted = bootstrapLoadoutsGranted,
                lastBootstrapEntryPoint = lastBootstrapEntryPoint,
                lastBootstrapSignature = lastBootstrapSignature,
                renderRefreshRequests = renderRefreshRequests,
                renderRefreshDispatched = renderRefreshDispatched,
                renderRefreshCoalesced = renderRefreshCoalesced,
                lastRenderRefreshTick = lastRenderRefreshTick
            };
        }

        public static void ResetCapturedStats()
        {
            EnsureGameContext();
            ResetCounters();
        }

        private static void EnsureGameContext()
        {
            Game? currentGame = Current.Game;
            if (ReferenceEquals(currentGame, trackedGame))
                return;

            trackedGame = currentGame;
            lastRefreshTickByPawnId.Clear();
            ResetCounters();
        }

        private static void ResetCounters()
        {
            bootstrapLoadedGameCalls = 0;
            bootstrapStartedNewGameCalls = 0;
            bootstrapFinalizeInitCalls = 0;
            bootstrapPassesExecuted = 0;
            bootstrapPassesSkipped = 0;
            bootstrapMapsVisited = 0;
            bootstrapPawnsVisited = 0;
            bootstrapCompsAdded = 0;
            bootstrapDefaultSkinsApplied = 0;
            bootstrapLoadoutsGranted = 0;
            lastBootstrapEntryPoint = "N/A";
            lastBootstrapSignature = "N/A";

            renderRefreshRequests = 0;
            renderRefreshDispatched = 0;
            renderRefreshCoalesced = 0;
            lastRenderRefreshTick = -1;
        }
    }
}