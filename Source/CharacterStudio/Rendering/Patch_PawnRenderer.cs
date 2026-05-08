using System.Collections.Generic;
using HarmonyLib;
using Verse;
using UnityEngine;
using CharacterStudio.Abilities;

namespace CharacterStudio.Rendering
{
    public static class Patch_PawnRenderer
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.PropertyGetter(typeof(Pawn_DrawTracker), "DrawPos");
            var postfix = AccessTools.Method(typeof(Patch_PawnRenderer), nameof(DrawPos_Postfix));

            if (target != null && postfix != null)
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
        }

        // P-PERF: 飞行中的 Pawn ID 集合，O(1) 快速跳过非飞行 Pawn
        // 与 CharacterAttributeBuffService.HasActiveBuffFast 同模式
        private static readonly HashSet<int> flyingPawnIds = new HashSet<int>();

        /// <summary>飞行状态变更时调用，注册/注销 Pawn ID</summary>
        public static void SetPawnFlying(Pawn pawn, bool flying)
        {
            if (pawn == null) return;
            if (flying)
                flyingPawnIds.Add(pawn.thingIDNumber);
            else
                flyingPawnIds.Remove(pawn.thingIDNumber);
        }

        /// <summary>游戏结束时清理</summary>
        public static void ClearFlyingTracker()
        {
            flyingPawnIds.Clear();
        }

        // P-PERF: 仅对飞行中的 Pawn 缓存 Comp 引用，避免对地面 Pawn 的 ConditionalWeakTable 查找
        private static Pawn? _lastPawn;
        private static CompCharacterAbilityRuntime? _lastComp;

        public static void DrawPos_Postfix(Pawn_DrawTracker __instance, Pawn ___pawn, ref Vector3 __result)
        {
            if (___pawn == null || !___pawn.Spawned) return;

            // P-PERF: O(1) 快速跳过 — 绝大多数 Pawn 不在飞行
            if (!flyingPawnIds.Contains(___pawn.thingIDNumber)) return;

            // P-PERF: 引用比较缓存（仅飞行 Pawn 走到此，数量极少）
            CompCharacterAbilityRuntime? abilityComp;
            if (ReferenceEquals(_lastPawn, ___pawn))
            {
                abilityComp = _lastComp;
            }
            else
            {
                abilityComp = ___pawn.GetComp<CompCharacterAbilityRuntime>();
                _lastPawn = ___pawn;
                _lastComp = abilityComp;
            }

            if (abilityComp == null) return;

            // 飞行高度偏移 — 直接用 TotalFlightHeight 判断，帧缓存已在内部避免重复 IsFlightStateActive
            float height = abilityComp.TotalFlightHeight;
            if (height > 0.001f)
            {
                __result.z += height;
                if (___pawn.Position.Roofed(___pawn.Map))
                {
                    var roofDef = ___pawn.Map.roofGrid.RoofAt(___pawn.Position);
                    if (roofDef != null && roofDef.isThickRoof)
                    {
                        __result.y = AltitudeLayer.Skyfaller.AltitudeFor();
                    }
                }
            }
        }
    }
}
