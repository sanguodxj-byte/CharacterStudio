using System;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 皮肤图层配置
    /// 定义单个渲染图层的属性
    /// </summary>
    public class PawnLayerConfig
    {
        /// <summary>图层名称（用于标识）</summary>
        public string layerName = "";

        /// <summary>纹理路径（相对于模组 Textures 文件夹）</summary>
        public string texPath = "";

        /// <summary>锚点标签（用于附加到 PawnRenderTree 的哪个节点）</summary>
        /// <remarks>
        /// 常用值: "Head", "Body", "Hair" 等
        /// 对应 PawnRenderNodeTagDef
        /// </remarks>
        public string anchorTag = "Head";

        /// <summary>锚点路径（NodePath 精准定位，优先于 anchorTag）</summary>
        /// <remarks>
        /// 格式: "Root/Body/Head:0"
        /// 如果设置了此字段，将优先使用路径匹配而非标签匹配
        /// </remarks>
        public string anchorPath = "";

        /// <summary>相对于锚点的偏移量</summary>
        public Vector3 offset = Vector3.zero;

        /// <summary>侧面朝向时的额外偏移量 (East/West)</summary>
        public Vector3 offsetEast = Vector3.zero;

        /// <summary>北向朝向时的额外偏移量 (North)</summary>
        public Vector3 offsetNorth = Vector3.zero;

        /// <summary>绘制顺序（用于确定图层的前后关系）</summary>
        public float drawOrder = 0f;

        /// <summary>图层缩放</summary>
        public Vector2 scale = Vector2.one;

        /// <summary>是否根据方向翻转</summary>
        public bool flipHorizontal = false;

        /// <summary>仅在特定朝向时显示</summary>
        public RotDrawMode rotDrawMode = RotDrawMode.Fresh | RotDrawMode.Rotting;

        /// <summary>自定义 Worker 类型（可选）</summary>
        public Type? workerClass = null;

        /// <summary>图形类类型（如 Graphic_Multi, Graphic_Single）</summary>
        public Type? graphicClass;

        /// <summary>着色器类型定义（可选）</summary>
        public string? shaderTypeDef = null;

        /// <summary>颜色类型</summary>
        public LayerColorType colorType = LayerColorType.Custom;

        /// <summary>自定义颜色</summary>
        public Color customColor = Color.white;

        /// <summary>是否可见</summary>
        public bool visible = true;

        /// <summary>面部组件类型（仅当 workerClass 为 FaceComponent 时使用）</summary>
        public FaceComponentType faceComponent = FaceComponentType.Eyes;

        /// <summary>动画类型</summary>
        public LayerAnimationType animationType = LayerAnimationType.None;

        /// <summary>动画频率（抽动间隔或摆动速度）</summary>
        public float animFrequency = 1f;

        /// <summary>动画幅度（角度）</summary>
        public float animAmplitude = 15f;

        /// <summary>动画速度（动作快慢）</summary>
        public float animSpeed = 1f;

        /// <summary>动画是否也影响位移（适用于尾巴根部摆动）</summary>
        public bool animAffectsOffset = false;

        /// <summary>位移动画幅度</summary>
        public float animOffsetAmplitude = 0.02f;

        /// <summary>动画相位偏移（0-1，用于错开多个图层）</summary>
        public float animPhaseOffset = 0f;

        /// <summary>
        /// 复制当前配置
        /// </summary>
        public PawnLayerConfig Clone()
        {
            return new PawnLayerConfig
            {
                layerName = this.layerName,
                texPath = this.texPath,
                anchorTag = this.anchorTag,
                anchorPath = this.anchorPath,
                offset = this.offset,
                offsetEast = this.offsetEast,
                offsetNorth = this.offsetNorth,
                drawOrder = this.drawOrder,
                scale = this.scale,
                flipHorizontal = this.flipHorizontal,
                rotDrawMode = this.rotDrawMode,
                workerClass = this.workerClass,
                graphicClass = this.graphicClass,
                shaderTypeDef = this.shaderTypeDef,
                colorType = this.colorType,
                customColor = this.customColor,
                visible = this.visible,
                faceComponent = this.faceComponent,
                animationType = this.animationType,
                animFrequency = this.animFrequency,
                animAmplitude = this.animAmplitude,
                animSpeed = this.animSpeed,
                animAffectsOffset = this.animAffectsOffset,
                animOffsetAmplitude = this.animOffsetAmplitude,
                animPhaseOffset = this.animPhaseOffset
            };
        }
    }

    /// <summary>
    /// 图层动画类型
    /// </summary>
    public enum LayerAnimationType
    {
        /// <summary>无动画</summary>
        None,
        /// <summary>随机抽动（如兽耳）</summary>
        Twitch,
        /// <summary>周期摆动（如尾巴硬摆）</summary>
        Swing,
        /// <summary>轻柔摇曳（如尾巴自然晃动，使用复合正弦波）</summary>
        IdleSway,
        /// <summary>呼吸起伏（缩放动画）</summary>
        Breathe
    }

    /// <summary>
    /// 图层颜色类型
    /// </summary>
    public enum LayerColorType
    {
        /// <summary>使用自定义颜色</summary>
        Custom,
        /// <summary>使用角色头发颜色</summary>
        Hair,
        /// <summary>使用角色皮肤颜色</summary>
        Skin,
        /// <summary>使用白色（不着色）</summary>
        White
    }
}
