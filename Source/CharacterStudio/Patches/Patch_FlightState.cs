using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using CharacterStudio.Abilities;
using CharacterStudio.Rendering;
using Unity.Collections;

namespace CharacterStudio.Patches
{
    /// <summary>
    /// 飞行状态 Harmony 补丁集（9 个 patch）。
    ///
    /// 寻路原理：
    /// - CS_FlightPathGrid 覆盖 CalculatedCostAt 返回固定代价 5
    /// - Map 初始化后替换 Pathing.Flying 的 PathGrid 实例
    /// - 路由飞行 pawn 到 Flying context → WalkableBy/StandableBy/CostToMoveIntoCell 自动通过
    /// - ParameterizeGridJob 使用 flyingCost 的均匀网格
    ///
    /// 补丁分 4 层：
    /// 1. 基础设施（3）：Map 初始化替换 PathGrid + 路由到 Flying context
    /// 2. A* 数据（1）：用飞行代价网格替代 normalCost
    /// 3. Region/执行（3）：绕过 Region 可达性 + 忽略建筑阻挡和门等待
    /// 4. 战斗（2）：免疫地面近战攻击
    /// </summary>
    public static class Patch_FlightState
    {
        public static void Apply(Harmony harmony)
        {
            // ─── 基础设施：Map 初始化替换 Flying PathGrid ───
            TryPatch(harmony, typeof(Map), "FinalizeInit", null, null, typeof(ReplaceFlyingPathGrid));
            TryPatch(harmony, typeof(Map), "FinalizeLoading", null, null, typeof(ReplaceFlyingPathGrid));

            // ─── 基础设施：路由飞行 pawn 到 Flying PathingContext ───
            TryPatch(harmony, typeof(Pawn), "GetPathContext", null, typeof(RedirectGetPathContext), null);
            TryPatch(harmony, typeof(Pathing), "For", new[] { typeof(TraverseParms) }, typeof(RedirectPathingForParms), null);

            // ─── A* 数据：用飞行代价网格替代 normalCost ───
            TryPatch(harmony, typeof(PathFinderMapData), "ParameterizeGridJob", null, null, typeof(UseFlightCostGrid));

            // ─── Region 可达性绕过（覆盖了 ReachabilityUtility 的间接调用） ───
            TryPatch(harmony, typeof(Reachability), "CanReach",
                new[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms) },
                typeof(BypassReachability), null);

            // ─── 路径执行：忽略建筑阻挡、门等待、不可占据检测 ───
            TryPatch(harmony, typeof(Pawn_PathFollower), "BuildingBlockingNextPathCell", null, null, typeof(IgnoreBuildingBlock));
            TryPatch(harmony, typeof(Pawn_PathFollower), "NextCellDoorToWaitForOrManuallyOpen", null, null, typeof(IgnoreDoorWait));
            TryPatch(harmony, typeof(Pawn_PathFollower), "PawnCanOccupy", null, null, typeof(AnywhereOccupiable));

            // ─── 战斗：免疫地面近战 ───
            TryPatch(harmony, typeof(Verb_MeleeAttack), "CanHitTargetFrom", null, typeof(BlockMeleePatch), null);
        }

        private static void TryPatch(Harmony harmony, Type targetType, string methodName,
            Type[]? paramTypes, Type? prefixClass, Type? postfixClass)
        {
            var mi = paramTypes != null
                ? AccessTools.Method(targetType, methodName, paramTypes)
                : AccessTools.Method(targetType, methodName);
            if (mi == null) return;

            var pre = prefixClass != null ? AccessTools.Method(prefixClass, "Prefix") : null;
            var post = postfixClass != null ? AccessTools.Method(postfixClass, "Postfix") : null;

            if (pre != null || post != null)
                harmony.Patch(mi,
                    pre != null ? new HarmonyMethod(pre) : null,
                    post != null ? new HarmonyMethod(post) : null);
        }

        private static bool IsAirborne(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead) return false;
            var comp = pawn.GetComp<CompCharacterAbilityRuntime>();
            return comp != null && comp.IsFlightStateActive();
        }

        // ─── 基础设施：替换 Flying PathGrid ───

        public static class ReplaceFlyingPathGrid
        {
            public static void Postfix(Map __instance)
            {
                try
                {
                    var pathing = __instance.pathing;
                    if (pathing == null) return;

                    var flyingContext = pathing.Flying;
                    if (flyingContext == null) return;
                    if (flyingContext.pathGrid is CS_FlightPathGrid) return;

                    var def = flyingContext.pathGrid.def;
                    var oldGrid = flyingContext.pathGrid;
                    var newGrid = new CS_FlightPathGrid(__instance, def);

                    var pathGridField = typeof(PathingContext).GetField("pathGrid",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (pathGridField != null)
                        pathGridField.SetValue(flyingContext, newGrid);

                    oldGrid.Dispose();
                    newGrid.RecalculateAllPerceivedPathCosts();

                    var mapData = __instance.pathFinder?.MapData;
                    if (mapData != null)
                    {
                        var notifyMethod = typeof(PathFinderMapData).GetMethod("Notify_MapDirtied",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        notifyMethod?.Invoke(mapData, null);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 替换飞行寻路网格失败: {ex.Message}");
                }
            }
        }

        // ─── 路由层 ───

        public static class RedirectGetPathContext
        {
            public static bool Prefix(Pawn __instance, Pathing pathing, ref PathingContext __result)
            {
                if (!IsAirborne(__instance)) return true;
                var ctx = pathing.Flying;
                if (ctx == null) return true;
                __result = ctx;
                return false;
            }
        }

        public static class RedirectPathingForParms
        {
            public static bool Prefix(Pathing __instance, ref PathingContext __result, TraverseParms parms)
            {
                if (parms.pawn == null || !IsAirborne(parms.pawn)) return true;
                var ctx = __instance.Flying;
                if (ctx == null) return true;
                __result = ctx;
                return false;
            }
        }

        // ─── A* 数据层 ───

        public static class UseFlightCostGrid
        {
            public static void Postfix(PathFinderMapData __instance, PathRequest request,
                ref PathGridJob job, Map ___map)
            {
                if (request.pawn == null || !IsAirborne(request.pawn)) return;
                var ctx = ___map?.pathing?.Flying;
                if (ctx == null) return;
                job.pathGridDirect = ctx.pathGrid.Grid_Unsafe.AsReadOnly();
            }
        }

        // ─── Region 可达性绕过 ───

        public static class BypassReachability
        {
            public static bool Prefix(IntVec3 start, LocalTargetInfo dest,
                PathEndMode peMode, TraverseParms traverseParams, ref bool __result, Map ___map)
            {
                if (traverseParams.pawn == null || !IsAirborne(traverseParams.pawn)) return true;
                if (!start.InBounds(___map) || !dest.IsValid) return true;
                if (dest.HasThing && dest.Thing.MapHeld != ___map) return true;
                if (!dest.Cell.InBounds(___map)) return true;
                __result = true;
                return false;
            }
        }

        // ─── 路径执行层 ───

        public static class IgnoreBuildingBlock
        {
            public static void Postfix(ref Building __result, Pawn ___pawn)
            {
                if (IsAirborne(___pawn)) __result = null!;
            }
        }

        public static class IgnoreDoorWait
        {
            public static void Postfix(ref Building_Door __result, Pawn ___pawn)
            {
                if (IsAirborne(___pawn)) __result = null!;
            }
        }

        public static class AnywhereOccupiable
        {
            public static void Postfix(ref bool __result, Pawn ___pawn)
            {
                if (IsAirborne(___pawn)) __result = true;
            }
        }

        // ─── 战斗层 ───

        public static class BlockMeleePatch
        {
            public static bool Prefix(Verb __instance, LocalTargetInfo targ, ref bool __result)
            {
                if (targ.Thing is Pawn target && IsAirborne(target))
                {
                    var comp = target.GetComp<CompCharacterAbilityRuntime>();
                    if (comp != null && comp.TotalFlightHeight > 0.6f)
                    {
                        __result = false;
                        return false;
                    }
                }
                return true;
            }
        }

    }
}
