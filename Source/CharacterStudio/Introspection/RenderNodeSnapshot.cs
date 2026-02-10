using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CharacterStudio.Introspection
{
    /// <summary>
    /// Represents a clean snapshot of a single Render Node.
    /// Used for UI display and Export analysis.
    /// </summary>
    public class RenderNodeSnapshot
    {
        public string debugLabel = "Unnamed";       // e.g. "Body", "Head", "RatkinEarLeft"
        public string tagDefName = "Untagged";      // The defName of the node's tag (crucial for hiding)
        public string texPath = "";                 // The path to the texture (if available)
        public string meshDefName = "N/A";          // e.g. "Plane10", "HeadMesh"
        public bool isVisible = true;               // Current runtime visibility
        public Color color = Color.white;           // The color tint
        public int treeLayer;                       // Depth in the hierarchy
        
        // ─────────────────────────────────────────────
        // 偏移和缩放信息
        // ─────────────────────────────────────────────
        
        /// <summary>节点偏移量</summary>
        public Vector3 offset = Vector3.zero;
        
        /// <summary>节点缩放</summary>
        public float scale = 1f;
        
        /// <summary>绘制顺序（原始值）</summary>
        public float drawOrder = 0f;
        
        // ─────────────────────────────────────────────
        // 运行时数据探针 (Runtime Probe)
        // 这些是 Worker 实时计算的值，用于 UI 显示
        // ─────────────────────────────────────────────
        
        /// <summary>运行时偏移（South 朝向基准值，Worker 计算后的实际值）</summary>
        public Vector3 runtimeOffset = Vector3.zero;
        
        /// <summary>运行时侧面偏移（East 朝向相对于 South 的差值）</summary>
        public Vector3 runtimeOffsetEast = Vector3.zero;
        
        /// <summary>运行时北向偏移（North 朝向相对于 South 的差值）</summary>
        public Vector3 runtimeOffsetNorth = Vector3.zero;
        
        /// <summary>运行时缩放（Worker 计算后的实际值）</summary>
        public Vector3 runtimeScale = Vector3.one;
        
        /// <summary>运行时颜色（Worker 计算后的实际值）</summary>
        public Color runtimeColor = Color.white;
        
        /// <summary>运行时旋转角度（欧拉角 Y 分量）</summary>
        public float runtimeRotation = 0f;
        
        /// <summary>标记运行时数据是否成功获取</summary>
        public bool runtimeDataValid = false;
        
        // ─────────────────────────────────────────────
        // Graphic 信息 (从 Graphic 对象直接提取)
        // ─────────────────────────────────────────────
        
        /// <summary>Graphic 的绘制尺寸</summary>
        public Vector2 graphicDrawSize = Vector2.one;
        
        /// <summary>Graphic 的主颜色</summary>
        public Color graphicColor = Color.white;
        
        /// <summary>Graphic 的第二颜色 (Mask)</summary>
        public Color graphicColorTwo = Color.white;
        
        /// <summary>Shader 名称</summary>
        public string shaderName = "";
        
        /// <summary>Mask 纹理路径</summary>
        public string maskPath = "";

        // ─────────────────────────────────────────────
        // NodePath 系统 (CS 风格精准定位)
        // ─────────────────────────────────────────────
        
        /// <summary>唯一节点路径，如 'Root/Body/Head:0'</summary>
        public string uniqueNodePath = "";
        
        /// <summary>在父节点中同名子节点的索引（用于区分多个同名节点）</summary>
        public int childIndex = 0;
        
        // Children nodes
        public List<RenderNodeSnapshot> children = new List<RenderNodeSnapshot>();

        // Debug info for HAR/FA identification
        public string workerClass = "Default";      // The Type name of the node's worker
        
        /// <summary>原始图形类类型（如 Graphic_Multi, Graphic_Single）</summary>
        public System.Type graphicClass;

        // ─────────────────────────────────────────────
        // HAR 兼容性字段
        // ─────────────────────────────────────────────
        
        /// <summary>HAR 定义的颜色通道 (e.g., "hair", "skin")</summary>
        public string colorChannel = "";
    }
}