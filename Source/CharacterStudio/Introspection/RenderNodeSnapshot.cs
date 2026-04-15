using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CharacterStudio.Introspection
{
    /// <summary>
    /// 代表渲染节点的深度快照。
    /// 用于编辑器可视化显示及导出配置分析。
    /// </summary>
    public class RenderNodeSnapshot
    {
        public string debugLabel = "Unnamed";       // 节点名称，如 "Body", "Head"
        public string tagDefName = "Untagged";      // 节点的渲染标签名称（对隐藏逻辑至关重要）
        public string texPath = "";                 // 纹理路径
        public string meshDefName = "N/A";          // 网格名称
        public bool isVisible = true;               // 运行时可见性
        public Color color = Color.white;           // 颜色参数
        public int treeLayer;                       // 在树中的层级深度
        
        // ─────────────────────────────────────────────
        // 偏移和缩放信息 (Def 配置值)
        // ─────────────────────────────────────────────
        
        /// <summary>节点偏移量 (BaseOffset)</summary>
        public Vector3 offset = Vector3.zero;
        
        /// <summary>节点缩放 (BaseScale)</summary>
        public float scale = 1f;
        
        /// <summary>绘制顺序 (BaseDrawOrder)</summary>
        public float drawOrder = 0f;
        
        // ─────────────────────────────────────────────
        // 运行时数据探针 (Runtime Probe)
        // ─────────────────────────────────────────────
        
        /// <summary>运行时偏移（相对于 South 朝向的实际计算值）</summary>
        public Vector3 runtimeOffset = Vector3.zero;
        
        /// <summary>运行时侧面偏移（East 朝向相对于 South 的差值）</summary>
        public Vector3 runtimeOffsetEast = Vector3.zero;
        
        /// <summary>运行时北向偏移（North 朝向相对于 South 的差值）</summary>
        public Vector3 runtimeOffsetNorth = Vector3.zero;
        
        /// <summary>运行时缩放（实际计算后的 Vector3）</summary>
        public Vector3 runtimeScale = Vector3.one;
        
        /// <summary>运行时颜色（当前帧实际绘制颜色）</summary>
        public Color runtimeColor = Color.white;
        
        /// <summary>运行时旋转角度（欧拉角）</summary>
        public float runtimeRotation = 0f;
        
        /// <summary>标记运行时数据是否成功获取</summary>
        public bool runtimeDataValid = false;
        
        // ─────────────────────────────────────────────
        // Graphic 信息
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
        // NodePath 系统 (精确定位)
        // ─────────────────────────────────────────────
        
        /// <summary>唯一节点路径，如 'Root/Body/Head:0'</summary>
        public string uniqueNodePath = "";
        
        /// <summary>同名节点索引</summary>
        public int childIndex = 0;
        
        // 子节点快照
        public List<RenderNodeSnapshot> children = new List<RenderNodeSnapshot>();

        // 外部节点识别信息
        public string workerClass = "Default";      // Worker 类型名称
        
        /// <summary>原始图形类类型</summary>
        public System.Type? graphicClass;

        // ─────────────────────────────────────────────
        // 外部渲染兼容性字段
        // ─────────────────────────────────────────────
        
        /// <summary>外部定义的颜色通道名称 (如 "hair", "skin")</summary>
        public string colorChannel = "";
    }
}