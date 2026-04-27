using System;
using System.Reflection;
using CharacterStudio.Abilities;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Patches
{
    /// <summary>
    /// Harmony Patch：在投射物每 Tick 移动后检测是否穿越了贝塞尔曲线墙。
    /// 若检测到交叉则销毁投射物，由墙壁吸收伤害。
    /// </summary>
    public static class Patch_ProjectileBezierWallIntercept
    {
        // 缓存反射的字段，用于获取投射物上一帧位置
        private static FieldInfo? _lastExactPosField;
        private static FieldInfo? _originField;
        private static FieldInfo? _launcherField;
        private static bool _fieldsResolved = false;

        public static void Apply(Harmony harmony)
        {
            // TODO: RimWorld 1.6 API 变化 - Projectile.Tick() 可见性变更，需更新反射目标
            var target = AccessTools.Method(typeof(Projectile), "Tick");
            if (target == null)
            {
                Log.Warning("[CharacterStudio] Patch_ProjectileBezierWallIntercept: 未找到 Projectile.Tick，跳过注册。");
                return;
            }

            var postfix = new HarmonyMethod(typeof(Patch_ProjectileBezierWallIntercept), nameof(Projectile_Tick_Postfix));
            harmony.Patch(target, postfix: postfix);
        }

        private static void ResolveFields()
        {
            if (_fieldsResolved) return;
            _fieldsResolved = true;

            Type projType = typeof(Projectile);
            _originField = AccessTools.Field(projType, "origin");
            _lastExactPosField = AccessTools.Field(projType, "lastExactPos");
            _launcherField = AccessTools.Field(projType, "launcher");
        }

        /// <summary>
        /// Postfix：在 Projectile.Tick() 执行后检测贝塞尔墙拦截。
        /// 使用 Postfix 而非 Prefix，确保投射物已更新位置后再检测。
        /// </summary>
        public static void Projectile_Tick_Postfix(Projectile __instance)
        {
            // 快速路径：无活跃墙壁时直接跳过
            if (BezierCurveWallManager.ActiveWalls.Count == 0) return;
            if (__instance.Destroyed) return;

            Map? map = __instance.Map;
            if (map == null) return;

            ResolveFields();

            // 获取投射物当前位置
            Vector3 currentPos = __instance.ExactPosition;

            // 获取上一帧位置（如果无法获取则使用 origin）
            Vector3 lastPos = currentPos;
            if (_lastExactPosField != null)
            {
                object? val = _lastExactPosField.GetValue(__instance);
                if (val is Vector3 v) lastPos = v;
            }
            else if (_originField != null)
            {
                object? val = _originField.GetValue(__instance);
                if (val is Vector3 v) lastPos = v;
            }

            // 如果两帧位置完全相同，跳过
            if ((currentPos - lastPos).sqrMagnitude < 0.001f) return;

            // 获取射手阵营
            Faction? shooterFaction = null;
            if (_launcherField != null)
            {
                object? launcher = _launcherField.GetValue(__instance);
                if (launcher is Thing launcherThing)
                    shooterFaction = launcherThing.Faction;
            }

            // 获取投射物伤害
            float damage = 0f;
            if (__instance.def?.projectile != null)
                damage = __instance.def.projectile.GetDamageAmount(__instance); // TODO: RimWorld 1.6 API

            if (BezierCurveWallManager.CheckLineIntersectsAnyWall(
                lastPos, currentPos, shooterFaction, map, damage, out Vector3 hitPoint, out BezierWallInstance? hitWall))
            {
                // 在拦截点产生视觉反馈
                try
                {
                    FleckMaker.ThrowMicroSparks(hitPoint, map);
                    FleckMaker.ThrowLightningGlow(hitPoint, map, 0.5f);
                }
                catch { /* 忽略视觉效果异常 */ }

                // 投射物反弹逻辑
                if (hitWall != null && hitWall.ReflectsProjectiles && !__instance.Destroyed)
                {
                    object? originalLauncher = _launcherField != null ? _launcherField.GetValue(__instance) : null;
                    if (originalLauncher is Thing targetThing)
                    {
                        var newProj = (Projectile)GenSpawn.Spawn(__instance.def, hitPoint.ToIntVec3(), map);
                        Pawn? wallOwner = hitWall.OwnerComp?.parent as Pawn;
                        if (wallOwner == null) wallOwner = originalLauncher as Pawn;
                        newProj.Launch(wallOwner, hitPoint, new LocalTargetInfo(targetThing), new LocalTargetInfo(targetThing), ProjectileHitFlags.All, false, null, null);
                    }
                }

                // 销毁投射物
                if (!__instance.Destroyed)
                    __instance.Destroy(DestroyMode.Vanish);
            }
        }
    }
}
