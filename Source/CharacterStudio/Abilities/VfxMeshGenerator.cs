// ─────────────────────────────────────────────
// 程序化 Mesh 生成器
//
// 为 CustomMesh VFX 系统提供运行时程序化 Mesh 构建。
// 所有 Mesh 均从顶点 + 三角形数组生成，不依赖任何预制资源。
//
// 支持的形状:
//   LightningBolt — 闪电锯齿线（随机偏移的垂直带状 Mesh）
//   Ring          — 环形冲击波（内/外半径定义的中空环）
//   Spiral        — 螺旋线（沿垂直轴螺旋上升的带状 Mesh）
//   Beam          — 锥形光束（从宽到窄的锥形）
// ─────────────────────────────────────────────

using System.Collections.Generic;
using UnityEngine;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 程序化 Mesh 生成器。
    /// 所有方法返回新的 Mesh 实例，调用方负责缓存和销毁。
    /// </summary>
    public static class VfxMeshGenerator
    {
        // ─────────────────────────────────────────────
        // LightningBolt: 闪电锯齿线
        // ─────────────────────────────────────────────

        /// <summary>
        /// 生成一道闪电锯齿 Mesh。
        /// 从原点沿 Y 轴向上延伸，顶点在 X 方向随机偏移形成锯齿。
        /// </summary>
        /// <param name="height">闪电总高度</param>
        /// <param name="width">闪电带宽度</param>
        /// <param name="segments">锯齿段数（越大越细碎）</param>
        /// <param name="jitter">X 方向最大随机偏移</param>
        public static Mesh GenerateLightningBolt(float height, float width, int segments, float jitter)
        {
            segments = Mathf.Max(2, segments);
            height = Mathf.Max(0.1f, height);
            width = Mathf.Max(0.01f, width);
            jitter = Mathf.Max(0f, jitter);

            // 每段 4 个顶点（左侧 + 右侧），段数 + 1 行顶点
            int vertexRows = segments + 1;
            int vertexCount = vertexRows * 2;
            int triangleCount = segments * 2; // 每段 2 个三角形

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[triangleCount * 3];

            for (int i = 0; i < vertexRows; i++)
            {
                float t = (float)i / segments;
                float y = t * height;
                float xOffset = (i == 0 || i == vertexRows - 1) ? 0f : Random.Range(-jitter, jitter);
                float halfW = width * 0.5f;

                vertices[i * 2]     = new Vector3(xOffset - halfW, y, 0f); // 左
                vertices[i * 2 + 1] = new Vector3(xOffset + halfW, y, 0f); // 右

                uv[i * 2]     = new Vector2(0f, t);
                uv[i * 2 + 1] = new Vector2(1f, t);
            }

            for (int i = 0; i < segments; i++)
            {
                int baseIdx = i * 2;
                int triBase = i * 6;

                // 三角形 1: 左下-右下-右上
                triangles[triBase]     = baseIdx;
                triangles[triBase + 1] = baseIdx + 1;
                triangles[triBase + 2] = baseIdx + 3;

                // 三角形 2: 左下-右上-左上
                triangles[triBase + 3] = baseIdx;
                triangles[triBase + 4] = baseIdx + 3;
                triangles[triBase + 5] = baseIdx + 2;
            }

            return BuildMesh(vertices, uv, triangles);
        }

        // ─────────────────────────────────────────────
        // Ring: 环形冲击波
        // ─────────────────────────────────────────────

        /// <summary>
        /// 生成一个水平放置的中空环状 Mesh（XZ 平面）。
        /// </summary>
        /// <param name="innerRadius">内半径</param>
        /// <param name="outerRadius">外半径</param>
        /// <param name="segments">环的细分段数</param>
        public static Mesh GenerateRing(float innerRadius, float outerRadius, int segments)
        {
            segments = Mathf.Max(6, segments);
            innerRadius = Mathf.Max(0.01f, innerRadius);
            outerRadius = Mathf.Max(innerRadius + 0.01f, outerRadius);

            int vertexCount = segments * 2; // 内圈 + 外圈各 segments 个顶点
            int triangleCount = segments * 2;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[triangleCount * 3];

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float nextAngle = (float)((i + 1) % segments) / segments * Mathf.PI * 2f;
                float t = (float)i / segments;

                // 内圈顶点
                vertices[i * 2] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                // 外圈顶点
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);

                uv[i * 2]     = new Vector2(t, 0f);
                uv[i * 2 + 1] = new Vector2(t, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = ((i + 1) % segments);
                int triBase = i * 6;

                // 三角形 1: 内i - 外i - 外next
                triangles[triBase]     = i * 2;
                triangles[triBase + 1] = i * 2 + 1;
                triangles[triBase + 2] = next * 2 + 1;

                // 三角形 2: 内i - 外next - 内next
                triangles[triBase + 3] = i * 2;
                triangles[triBase + 4] = next * 2 + 1;
                triangles[triBase + 5] = next * 2;
            }

            return BuildMesh(vertices, uv, triangles);
        }

        // ─────────────────────────────────────────────
        // Spiral: 螺旋线
        // ─────────────────────────────────────────────

        /// <summary>
        /// 生成沿 Y 轴螺旋上升的带状 Mesh。
        /// </summary>
        /// <param name="height">总高度</param>
        /// <param name="radius">螺旋半径</param>
        /// <param name="turns">旋转圈数</param>
        /// <param name="ribbonWidth">带状宽度</param>
        /// <param name="segments">细分段数</param>
        public static Mesh GenerateSpiral(float height, float radius, float turns, float ribbonWidth, int segments)
        {
            segments = Mathf.Max(6, segments);
            height = Mathf.Max(0.1f, height);
            radius = Mathf.Max(0.01f, radius);
            ribbonWidth = Mathf.Max(0.01f, ribbonWidth);
            turns = Mathf.Max(0.1f, turns);

            int vertexCount = (segments + 1) * 2;
            int triangleCount = segments * 2;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[triangleCount * 3];

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * turns * Mathf.PI * 2f;
                float y = t * height;

                Vector3 center = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                // 带状方向：沿螺旋切线的法线方向
                Vector3 tangent = new Vector3(-Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius).normalized;
                Vector3 offset = tangent * ribbonWidth * 0.5f;

                vertices[i * 2]     = center - offset; // 内侧
                vertices[i * 2 + 1] = center + offset; // 外侧

                uv[i * 2]     = new Vector2(0f, t);
                uv[i * 2 + 1] = new Vector2(1f, t);
            }

            for (int i = 0; i < segments; i++)
            {
                int triBase = i * 6;
                triangles[triBase]     = i * 2;
                triangles[triBase + 1] = i * 2 + 1;
                triangles[triBase + 2] = (i + 1) * 2 + 1;

                triangles[triBase + 3] = i * 2;
                triangles[triBase + 4] = (i + 1) * 2 + 1;
                triangles[triBase + 5] = (i + 1) * 2;
            }

            return BuildMesh(vertices, uv, triangles);
        }

        // ─────────────────────────────────────────────
        // Beam: 锥形光束
        // ─────────────────────────────────────────────

        /// <summary>
        /// 生成一个沿 Z 轴延伸的锥形光束 Mesh（从近端宽到远端窄）。
        /// </summary>
        /// <param name="length">光束长度</param>
        /// <param name="startWidth">起始宽度</param>
        /// <param name="endWidth">结束宽度</param>
        /// <param name="segments">长度方向细分段数</param>
        public static Mesh GenerateBeam(float length, float startWidth, float endWidth, int segments)
        {
            segments = Mathf.Max(1, segments);
            length = Mathf.Max(0.1f, length);
            startWidth = Mathf.Max(0.01f, startWidth);
            endWidth = Mathf.Max(0.01f, endWidth);

            int vertexRows = segments + 1;
            int vertexCount = vertexRows * 2;
            int triangleCount = segments * 2;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[triangleCount * 3];

            for (int i = 0; i < vertexRows; i++)
            {
                float t = (float)i / segments;
                float z = t * length;
                float w = Mathf.Lerp(startWidth, endWidth, t) * 0.5f;

                vertices[i * 2]     = new Vector3(-w, 0f, z);
                vertices[i * 2 + 1] = new Vector3(w, 0f, z);

                uv[i * 2]     = new Vector2(0f, t);
                uv[i * 2 + 1] = new Vector2(1f, t);
            }

            for (int i = 0; i < segments; i++)
            {
                int triBase = i * 6;
                triangles[triBase]     = i * 2;
                triangles[triBase + 1] = i * 2 + 1;
                triangles[triBase + 2] = (i + 1) * 2 + 1;

                triangles[triBase + 3] = i * 2;
                triangles[triBase + 4] = (i + 1) * 2 + 1;
                triangles[triBase + 5] = (i + 1) * 2;
            }

            return BuildMesh(vertices, uv, triangles);
        }

        // ─────────────────────────────────────────────
        // 通用构建
        // ─────────────────────────────────────────────

        private static Mesh BuildMesh(Vector3[] vertices, Vector2[] uv, int[] triangles)
        {
            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
