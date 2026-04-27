// ─────────────────────────────────────────────
// 自定义 Mesh VFX 生命周期管理器
//
// 管理 Graphics.DrawMesh 渲染的自定义 Mesh 特效实例。
// 因为 Graphics.DrawMesh 不像 Mote 系统有自动生命周期，
// 需要手动 Tick 跟踪每个活跃特效并在过期后清理。
//
// 设计:
//   - 每个 VfxMeshEntry 持有 Mesh + Material + 位置/旋转/缩放 + age/duration
//   - 每帧在 Map.MeshPoolDraw 窗口调用 Draw() 提交所有活跃特效
//   - 过期条目在 Tick 中标记为待清理
//   - 材质通过 FadedMaterialPool 实现淡入淡出
// ─────────────────────────────────────────────

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 单个自定义 Mesh 特效实例的运行时状态。
    /// </summary>
    internal class VfxMeshEntry
    {
        public Mesh? mesh;
        public Material? material;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public int age;
        public int duration;
        public int fadeInTicks;
        public bool expired;
        public AltitudeLayer altitude = AltitudeLayer.Weather;

        /// <summary>
        /// 计算当前帧的透明度（0~1），用于淡入淡出。
        /// 前 fadeInTicks 帧线性淡入，之后线性淡出到 duration。
        /// </summary>
        public float CurrentAlpha
        {
            get
            {
                if (fadeInTicks > 0 && age < fadeInTicks)
                    return (float)age / fadeInTicks;
                return Mathf.Clamp01(1f - (float)age / duration);
            }
        }
    }

    /// <summary>
    /// 自定义 Mesh VFX 生命周期管理器。
    /// 持有所有活跃的 Mesh 特效实例，每帧渲染。
    /// </summary>
    public static class VfxCustomMeshManager
    {
        private static readonly List<VfxMeshEntry> activeEntries = new List<VfxMeshEntry>();
        private static readonly List<VfxMeshEntry> pendingRemoval = new List<VfxMeshEntry>();

        /// <summary>当前活跃的特效数量。</summary>
        public static int ActiveCount => activeEntries.Count;

        /// <summary>
        /// 生成一个新的自定义 Mesh 特效。
        /// </summary>
        /// <param name="mesh">程序化生成的 Mesh</param>
        /// <param name="baseMaterial">基础材质（通常是 MoteGlow 或 Transparent）</param>
        /// <param name="position">世界坐标位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="scale">缩放</param>
        /// <param name="durationTicks">持续 tick 数</param>
        /// <param name="fadeInTicks">淡入 tick 数</param>
        /// <param name="altitude">渲染高度层</param>
        public static void Spawn(
            Mesh mesh,
            Material baseMaterial,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            int durationTicks,
            int fadeInTicks = 3,
            AltitudeLayer altitude = AltitudeLayer.Weather)
        {
            if (mesh == null || baseMaterial == null) return;

            var entry = new VfxMeshEntry
            {
                mesh = mesh,
                material = baseMaterial,
                position = position,
                rotation = rotation,
                scale = scale,
                age = 0,
                duration = Mathf.Max(1, durationTicks),
                fadeInTicks = Mathf.Max(0, fadeInTicks),
                expired = false,
                altitude = altitude
            };

            activeEntries.Add(entry);
        }

        /// <summary>
        /// 每个游戏 Tick 调用，推进所有活跃特效的年龄。
        /// </summary>
        public static void Tick()
        {
            for (int i = activeEntries.Count - 1; i >= 0; i--)
            {
                var entry = activeEntries[i];
                entry.age++;
                if (entry.age >= entry.duration)
                {
                    entry.expired = true;
                    pendingRemoval.Add(entry);
                    activeEntries.RemoveAt(i);
                }
            }

            // 清理过期条目的 Mesh 资源
            for (int i = pendingRemoval.Count - 1; i >= 0; i--)
            {
                var entry = pendingRemoval[i];
                if (entry.mesh != null)
                {
                    Object.Destroy(entry.mesh);
                    entry.mesh = null!;
                }
                pendingRemoval.RemoveAt(i);
            }
        }

        /// <summary>
        /// 每帧调用，渲染所有活跃的自定义 Mesh 特效。
        /// 应在 MapUpdate 或后缀补丁中调用。
        /// </summary>
        public static void Draw()
        {
            for (int i = 0; i < activeEntries.Count; i++)
            {
                var entry = activeEntries[i];
                if (entry.mesh == null || entry.material == null) continue;

                float alpha = entry.CurrentAlpha;
                if (alpha <= 0.001f) continue;

                Material fadedMat = FadedMaterialPool.FadedVersionOf(entry.material, alpha);
                Matrix4x4 matrix = Matrix4x4.TRS(entry.position, entry.rotation, entry.scale);
                Graphics.DrawMesh(entry.mesh, matrix, fadedMat, 0);
            }
        }

        /// <summary>
        /// 立即清除所有活跃特效并释放资源。
        /// </summary>
        public static void ClearAll()
        {
            foreach (var entry in activeEntries)
            {
                if (entry.mesh != null)
                {
                    Object.Destroy(entry.mesh);
                }
            }
            activeEntries.Clear();

            foreach (var entry in pendingRemoval)
            {
                if (entry.mesh != null)
                {
                    Object.Destroy(entry.mesh);
                }
            }
            pendingRemoval.Clear();
        }
    }
}
