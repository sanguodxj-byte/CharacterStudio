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
        public int faceTransformCacheHits;
        public int faceTransformCacheMisses;
        public int facePathCacheHits;
        public int facePathCacheMisses;
        public int graphicDirtyTriggers;
        public int graphicDirtySkippedByThrottle;

        // 纹理加载统计
        public int textureLoadsFromDisk;
        public int textureCacheHits;
        public int textureLruEvictions;
        public int textureSafeDestroySkipped;
        public int textureSafeDestroyed;
        public int texturePressureReleases;
        public int pendingTextureQueuePeak;

        // 引用计数统计
        public int refTrackerTrackedTextures;

        // 池统计
        public int graphicRuntimePoolAvailable;

        // Worker 缓存快照
        public string customLayerCacheStats = "";
        public string faceComponentCacheStats = "";
        public string eyeDirectionCacheStats = "";
        public string runtimeAssetLoaderCacheStats = "";
        public string textureMemoryStats = "";

        public float RenderRefreshCoalescedRate
        {
            get
            {
                if (renderRefreshRequests <= 0)
                    return 0f;

                return (float)renderRefreshCoalesced / renderRefreshRequests;
            }
        }

        /// <summary>
        /// 生成可复制的纯文本报告，方便用户粘贴到 Issue / 讨论中。
        /// </summary>
        public string ToReportString()
        {
            int ftTotal = faceTransformCacheHits + faceTransformCacheMisses;
            float ftHitRate = ftTotal > 0 ? (float)faceTransformCacheHits / ftTotal : 0f;
            int fpTotal = facePathCacheHits + facePathCacheMisses;
            float fpHitRate = fpTotal > 0 ? (float)facePathCacheHits / fpTotal : 0f;
            int texTotal = textureLoadsFromDisk + textureCacheHits;
            float texHitRate = texTotal > 0 ? (float)textureCacheHits / texTotal : 0f;

            return $@"[CharacterStudio Performance Report]
Capture: {(captureEnabled ? "ON" : "OFF")}
Game: {Verse.Find.TickManager?.TicksGame ?? -1} ticks

--- Bootstrap ---
LoadedGame calls: {bootstrapLoadedGameCalls}
StartedNewGame calls: {bootstrapStartedNewGameCalls}
FinalizeInit calls: {bootstrapFinalizeInitCalls}
Passes executed: {bootstrapPassesExecuted}
Passes skipped: {bootstrapPassesSkipped}
Maps visited: {bootstrapMapsVisited}
Pawns visited: {bootstrapPawnsVisited}
Comps added: {bootstrapCompsAdded}
Default skins applied: {bootstrapDefaultSkinsApplied}
Loadouts granted: {bootstrapLoadoutsGranted}
Last entry point: {lastBootstrapEntryPoint}
Last signature: {lastBootstrapSignature}

--- Render Refresh ---
Requests: {renderRefreshRequests}
Dispatched: {renderRefreshDispatched}
Coalesced: {renderRefreshCoalesced}
Coalesce rate: {RenderRefreshCoalescedRate:P1}
Last refresh tick: {(lastRenderRefreshTick >= 0 ? lastRenderRefreshTick.ToString() : "N/A")}
Graphic dirty triggers: {graphicDirtyTriggers}
Graphic dirty throttled: {graphicDirtySkippedByThrottle}

--- Face Caches ---
Transform hits: {faceTransformCacheHits} / misses: {faceTransformCacheMisses} (hit rate: {ftHitRate:P1})
Path hits: {facePathCacheHits} / misses: {facePathCacheMisses} (hit rate: {fpHitRate:P1})

--- Texture Loading ---
Loads from disk: {textureLoadsFromDisk}
Cache hits: {textureCacheHits} (hit rate: {texHitRate:P1})
LRU evictions: {textureLruEvictions}
SafeDestroy: {textureSafeDestroyed} destroyed / {textureSafeDestroySkipped} skipped (still referenced)
Pressure releases: {texturePressureReleases}
Pending read queue peak: {pendingTextureQueuePeak}

--- Ref Tracker ---
Tracked textures: {refTrackerTrackedTextures}

--- Object Pool ---
Graphic_Runtime pool available: {graphicRuntimePoolAvailable}

--- Worker Caches ---
CustomLayer: {customLayerCacheStats}
FaceComponent: {faceComponentCacheStats}
EyeDirection: {eyeDirectionCacheStats}

--- Asset Loader ---
{runtimeAssetLoaderCacheStats}

--- Texture Memory ---
{textureMemoryStats}";
        }
    }

    public static class CharacterStudioPerformanceStats
    {
        private static Game? trackedGame;
        private static readonly Dictionary<int, int> lastRefreshTickByPawnId = new Dictionary<int, int>();
        private static int _nextPawnIdCleanupTick;
        private const int PawnIdCleanupIntervalTicks = 3600; // ~60 秒

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
        private static int faceTransformCacheHits;
        private static int faceTransformCacheMisses;
        private static int facePathCacheHits;
        private static int facePathCacheMisses;
        private static int graphicDirtyTriggers;
        private static int graphicDirtySkippedByThrottle;

        // 纹理加载统计
        private static int textureLoadsFromDisk;
        private static int textureCacheHits;
        private static int textureLruEvictions;
        private static int textureSafeDestroySkipped;
        private static int textureSafeDestroyed;
        private static int texturePressureReleases;
        private static int pendingTextureQueuePeak;

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

            // P-CAP: 定期清理已失效的 Pawn ID，防止长期运行后字典无限增长
            if (currentTick >= _nextPawnIdCleanupTick)
            {
                _nextPawnIdCleanupTick = currentTick + PawnIdCleanupIntervalTicks;
                CleanupStalePawnRefreshIds();
            }

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

        public static void RecordFaceTransformCacheLookup(bool hit)
        {
            EnsureGameContext();

            if (!CaptureEnabled)
                return;

            if (hit)
                faceTransformCacheHits++;
            else
                faceTransformCacheMisses++;
        }

        public static void RecordFacePathCacheLookup(bool hit)
        {
            EnsureGameContext();

            if (!CaptureEnabled)
                return;

            if (hit)
                facePathCacheHits++;
            else
                facePathCacheMisses++;
        }

        public static void RecordGraphicDirtyTrigger(bool throttled)
        {
            EnsureGameContext();

            if (!CaptureEnabled)
                return;

            if (throttled)
                graphicDirtySkippedByThrottle++;
            else
                graphicDirtyTriggers++;
        }

        public static void RecordTextureLoadFromDisk()
        {
            if (!CaptureEnabled) return;
            textureLoadsFromDisk++;
        }

        public static void RecordTextureCacheHit()
        {
            if (!CaptureEnabled) return;
            textureCacheHits++;
        }

        public static void RecordTextureLruEviction()
        {
            if (!CaptureEnabled) return;
            textureLruEvictions++;
        }

        public static void RecordTextureSafeDestroy(bool destroyed)
        {
            if (!CaptureEnabled) return;
            if (destroyed) textureSafeDestroyed++;
            else textureSafeDestroySkipped++;
        }

        public static void RecordPressureRelease()
        {
            if (!CaptureEnabled) return;
            texturePressureReleases++;
        }

        public static void UpdatePendingQueuePeak(int currentCount)
        {
            if (!CaptureEnabled) return;
            if (currentCount > pendingTextureQueuePeak)
                pendingTextureQueuePeak = currentCount;
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
                lastRenderRefreshTick = lastRenderRefreshTick,
                faceTransformCacheHits = faceTransformCacheHits,
                faceTransformCacheMisses = faceTransformCacheMisses,
                facePathCacheHits = facePathCacheHits,
                facePathCacheMisses = facePathCacheMisses,
                graphicDirtyTriggers = graphicDirtyTriggers,
                graphicDirtySkippedByThrottle = graphicDirtySkippedByThrottle,
                textureLoadsFromDisk = textureLoadsFromDisk,
                textureCacheHits = textureCacheHits,
                textureLruEvictions = textureLruEvictions,
                textureSafeDestroySkipped = textureSafeDestroySkipped,
                textureSafeDestroyed = textureSafeDestroyed,
                texturePressureReleases = texturePressureReleases,
                pendingTextureQueuePeak = pendingTextureQueuePeak,
                refTrackerTrackedTextures = Rendering.TextureRefTracker.TotalTrackedCount,
                graphicRuntimePoolAvailable = Rendering.GraphicRuntimePool.AvailableCount,
                customLayerCacheStats = Rendering.PawnRenderNodeWorker_CustomLayer.GetCacheStats(),
                faceComponentCacheStats = Rendering.PawnRenderNodeWorker_FaceComponent.GetCacheStats(),
                eyeDirectionCacheStats = Rendering.PawnRenderNodeWorker_EyeDirection.GetCacheStats(),
                runtimeAssetLoaderCacheStats = Rendering.RuntimeAssetLoader.GetCacheStats(),
                textureMemoryStats = Rendering.TextureMemoryMonitor.GetStats(),
            };
        }

        public static void ResetCapturedStats()
        {
            EnsureGameContext();
            ResetCounters();
        }

        /// <summary>
        /// 清理已不在地图上的 Pawn ID，防止字典长期累积。
        /// </summary>
        private static void CleanupStalePawnRefreshIds()
        {
            Game? game = Current.Game;
            if (game == null || game.Maps.Count == 0)
            {
                lastRefreshTickByPawnId.Clear();
                return;
            }

            var activeIds = new HashSet<int>();
            foreach (Map map in game.Maps)
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    activeIds.Add(pawn.thingIDNumber);

            // 字典无 RemoveWhere，遍历收集需要移除的 key
            var staleKeys = new List<int>();
            foreach (var kv in lastRefreshTickByPawnId)
            {
                if (!activeIds.Contains(kv.Key))
                    staleKeys.Add(kv.Key);
            }
            foreach (int key in staleKeys)
                lastRefreshTickByPawnId.Remove(key);
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
            faceTransformCacheHits = 0;
            faceTransformCacheMisses = 0;
            facePathCacheHits = 0;
            facePathCacheMisses = 0;
            graphicDirtyTriggers = 0;
            graphicDirtySkippedByThrottle = 0;
            textureLoadsFromDisk = 0;
            textureCacheHits = 0;
            textureLruEvictions = 0;
            textureSafeDestroySkipped = 0;
            textureSafeDestroyed = 0;
            texturePressureReleases = 0;
            pendingTextureQueuePeak = 0;
        }
    }
}