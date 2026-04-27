using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 贝塞尔曲线墙实例数据，包含曲线参数和预计算的碰撞线段。
    /// </summary>
    public class BezierWallInstance
    {
        public Vector2 Start;
        public Vector2 End;
        public Vector2 ControlPoint;
        public float Thickness;
        public int SegmentCount;
        public int ExpireTick;
        public bool BlockFriendly;
        public Faction? OwnerFaction;
        public Map Map = default!;
        public CompCharacterAbilityRuntime OwnerComp = default!;
        public string CustomTexturePath = string.Empty;
        public bool ReflectsProjectiles = false;

        /// <summary>预计算的曲线离散化点（长度 = SegmentCount + 1）</summary>
        public Vector2[] SegmentPoints = Array.Empty<Vector2>();

        /// <summary>
        /// 计算二次贝塞尔曲线上 t 处的点。
        /// B(t) = (1-t)²·P0 + 2(1-t)t·P1 + t²·P2
        /// </summary>
        public static Vector2 EvaluateBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        /// <summary>根据曲线参数预计算离散化线段点</summary>
        public void BuildSegmentPoints()
        {
            int n = Mathf.Max(2, SegmentCount);
            SegmentPoints = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                SegmentPoints[i] = EvaluateBezier(Start, ControlPoint, End, t);
            }
        }
    }

    /// <summary>
    /// 全局静态管理器，维护当前所有活跃的贝塞尔曲线墙。
    /// 供 Harmony Patch 的投射物拦截检测和渲染绘制使用。
    /// </summary>
    public static class BezierCurveWallManager
    {
        private static readonly List<BezierWallInstance> _activeWalls = new List<BezierWallInstance>();
        // 用于迭代期间安全移除
        private static readonly List<BezierWallInstance> _removalBuffer = new List<BezierWallInstance>();

        public static void Register(BezierWallInstance wall)
        {
            if (wall != null && !_activeWalls.Contains(wall))
                _activeWalls.Add(wall);
        }

        public static void Unregister(BezierWallInstance wall)
        {
            _activeWalls.Remove(wall);
        }

        public static IReadOnlyList<BezierWallInstance> ActiveWalls => _activeWalls;

        /// <summary>
        /// 清理所有过期的墙壁实例（由 GameComponent 或 Tick 调用）
        /// </summary>
        public static void CleanupExpired(int nowTick)
        {
            _removalBuffer.Clear();
            for (int i = 0; i < _activeWalls.Count; i++)
            {
                var wall = _activeWalls[i];
                if (wall.ExpireTick < nowTick || wall.OwnerComp == null || !wall.OwnerComp.IsBezierWallActive())
                    _removalBuffer.Add(wall);
            }
            for (int i = 0; i < _removalBuffer.Count; i++)
                _activeWalls.Remove(_removalBuffer[i]);
            _removalBuffer.Clear();
        }

        /// <summary>
        /// 检测线段 (from→to) 是否与任何活跃的贝塞尔墙相交。
        /// 若相交则扣减墙壁吸收量并返回 true。
        /// </summary>
        public static bool CheckLineIntersectsAnyWall(
            Vector3 from, Vector3 to,
            Faction? shooterFaction, Map map,
            float damage,
            out Vector3 hitPoint,
            out BezierWallInstance? hitWall)
        {
            hitPoint = to;
            hitWall = null;
            Vector2 a = new Vector2(from.x, from.z);
            Vector2 b = new Vector2(to.x, to.z);

            for (int wi = _activeWalls.Count - 1; wi >= 0; wi--)
            {
                var wall = _activeWalls[wi];
                if (wall.Map != map) continue;
                if (!wall.BlockFriendly && wall.OwnerFaction != null && shooterFaction == wall.OwnerFaction) continue;

                for (int si = 0; si < wall.SegmentPoints.Length - 1; si++)
                {
                    Vector2 c = wall.SegmentPoints[si];
                    Vector2 d = wall.SegmentPoints[si + 1];

                    if (SegmentIntersectsThickSegment(a, b, c, d, wall.Thickness, out Vector2 intersection))
                    {
                        hitPoint = new Vector3(intersection.x, 0f, intersection.y);
                        hitWall = wall;

                        // 吸收伤害
                        wall.OwnerComp.BezierWallAbsorbRemaining -= Mathf.Max(0f, damage);
                        if (wall.OwnerComp.BezierWallAbsorbRemaining <= 0f)
                        {
                            wall.OwnerComp.BezierWallAbsorbRemaining = 0f;
                            wall.OwnerComp.BezierWallExpireTick = -1;
                            _activeWalls.RemoveAt(wi);
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 检测线段 AB 是否与加粗线段 CD（半径 thickness）相交。
        /// 方法：将 CD 扩展为矩形（平行于 CD 方向，宽度 = 2*thickness），
        /// 检测 AB 与矩形四条边的交点。简化实现：检测 AB 与 CD 的最短距离是否小于 thickness。
        /// </summary>
        private static bool SegmentIntersectsThickSegment(
            Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            float thickness, out Vector2 intersection)
        {
            intersection = Vector2.zero;

            // 先做快速 AABB 排除
            float minABx = Mathf.Min(a.x, b.x) - thickness;
            float maxABx = Mathf.Max(a.x, b.x) + thickness;
            float minABy = Mathf.Min(a.y, b.y) - thickness;
            float maxABy = Mathf.Max(a.y, b.y) + thickness;
            float minCDx = Mathf.Min(c.x, d.x) - thickness;
            float maxCDx = Mathf.Max(c.x, d.x) + thickness;
            float minCDy = Mathf.Min(c.y, d.y) - thickness;
            float maxCDy = Mathf.Max(c.y, d.y) + thickness;

            if (maxABx < minCDx || minABx > maxCDx || maxABy < minCDy || minABy > maxCDy)
                return false;

            // 计算两线段最短距离
            float dist = SegmentSegmentDistance(a, b, c, d, out float tAB, out float _tCD);
            if (dist <= thickness)
            {
                intersection = Vector2.Lerp(a, b, tAB);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 计算两条线段 (P1-P2) 和 (P3-P4) 的最短距离。
        /// 输出 t1 和 t2 为各自线段上的参数。
        /// </summary>
        private static float SegmentSegmentDistance(
            Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4,
            out float t1, out float t2)
        {
            Vector2 d1 = p2 - p1;
            Vector2 d2 = p4 - p3;
            Vector2 r = p1 - p3;

            float a = Vector2.Dot(d1, d1);
            float e = Vector2.Dot(d2, d2);
            float f = Vector2.Dot(d2, r);

            const float epsilon = 1e-6f;
            t1 = 0f;
            t2 = 0f;

            if (a <= epsilon && e <= epsilon)
            {
                return (p1 - p3).magnitude;
            }

            if (a <= epsilon)
            {
                t2 = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector2.Dot(d1, r);
                if (e <= epsilon)
                {
                    t1 = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector2.Dot(d1, d2);
                    float denom = a * e - b * b;

                    if (Mathf.Abs(denom) > epsilon)
                        t1 = Mathf.Clamp01((b * f - c * e) / denom);

                    t2 = (b * t1 + f) / e;

                    if (t2 < 0f)
                    {
                        t2 = 0f;
                        t1 = Mathf.Clamp01(-c / a);
                    }
                    else if (t2 > 1f)
                    {
                        t2 = 1f;
                        t1 = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            Vector2 closest1 = p1 + d1 * t1;
            Vector2 closest2 = p3 + d2 * t2;
            return (closest1 - closest2).magnitude;
        }

        /// <summary>地图切换或游戏加载时清除所有缓存</summary>
        public static void ClearAll()
        {
            _activeWalls.Clear();
            _removalBuffer.Clear();
        }
    }
}
