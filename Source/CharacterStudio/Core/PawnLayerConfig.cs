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

        /// <summary>侧向缩放乘数（East/West，1=不覆盖）</summary>
        public Vector2 scaleEastMultiplier = Vector2.one;

        /// <summary>北向缩放乘数（North，1=不覆盖）</summary>
        public Vector2 scaleNorthMultiplier = Vector2.one;

        /// <summary>旋转角度（度）</summary>
        public float rotation = 0f;
        public float rotationEastOffset = 0f;
        public float rotationNorthOffset = 0f;

        /// <summary>是否根据方向翻转</summary>
        public bool flipHorizontal = false;

        /// <summary>仅在特定朝向时显示</summary>
        public RotDrawMode rotDrawMode = RotDrawMode.Fresh | RotDrawMode.Rotting;

        /// <summary>自定义 Worker 类型（可选）</summary>
        public Type? workerClass = null;

        /// <summary>图形类类型（如 Graphic_Multi, Graphic_Single）</summary>
        public Type? graphicClass;

        /// <summary>
        /// [废弃] 保留字段，仅供旧版 XML 反序列化兼容使用，运行时不读取。
        /// 请使用 <see cref="shaderDefName"/> 指定 Shader。
        /// </summary>
        [Obsolete("Use shaderDefName instead. This field exists only for backward XML compatibility.")]
        public string? shaderTypeDef = null;

        /// <summary>着色器名称（用于 ShaderDatabase.LoadShader 加载，如 "Cutout", "CutoutComplex"）</summary>
        public string shaderDefName = "Cutout";

        /// <summary>
        /// 颜色来源。
        /// 默认 PawnHair：尾巴、耳朵、呆毛等附加图层绝大多数情况下应跟随发色，
        /// 用户可在属性面板随时修改。
        /// </summary>
        public LayerColorSource colorSource = LayerColorSource.PawnHair;

        /// <summary>
        /// [废弃] 旧版颜色类型桥接属性，将旧 LayerColorType 枚举映射到 colorSource。
        /// 保留仅为已存储皮肤的 XML 反序列化兼容，新代码请直接读写 <see cref="colorSource"/>。
        /// </summary>
        [Obsolete("Use colorSource instead. This property exists only for backward XML compatibility.")]
        public LayerColorType colorType
        {
            get => (LayerColorType)(int)colorSource;
            set => colorSource = (LayerColorSource)(int)value;
        }

        /// <summary>自定义颜色</summary>
        public Color customColor = Color.white;

        /// <summary>
        /// 第二颜色来源 (Mask)。
        /// 默认 PawnHair：与主颜色来源保持一致，通常用于 Mask 通道着色。
        /// </summary>
        public LayerColorSource colorTwoSource = LayerColorSource.PawnHair;

        /// <summary>自定义颜色2（用于 Mask 通道等）</summary>
        public Color customColorTwo = Color.white;

        /// <summary>Mask 纹理路径</summary>
        public string maskTexPath = "";

        /// <summary>可选：武器状态视觉配置（供动态武器携带层使用）</summary>
        public WeaponCarryVisualConfig? weaponCarryVisual;

        /// <summary>是否可见</summary>
        public bool visible = true;

        /// <summary>图层在统一表情系统中的角色</summary>
        public LayerRole role = LayerRole.Decoration;

        /// <summary>图层纹理变体解析逻辑</summary>
        public LayerVariantLogic variantLogic = LayerVariantLogic.None;

        /// <summary>
        /// 变体解析基础名称。为空时回退到 <see cref="texPath"/>。
        /// 可用于让多个状态图层共享同一基础命名空间。
        /// </summary>
        public string variantBaseName = "";

        /// <summary>是否启用朝向后缀解析（例如 _east / _north）</summary>
        public bool useDirectionalSuffix = true;

        /// <summary>是否启用高层表情后缀解析（例如 _Happy / _Angry）</summary>
        public bool useExpressionSuffix = false;

        /// <summary>是否启用眼睛方向后缀解析（例如 _Left / _Right）</summary>
        public bool useEyeDirectionSuffix = false;

        /// <summary>是否启用 Blink 后缀解析（例如 _Blink）</summary>
        public bool useBlinkSuffix = false;

        /// <summary>是否启用帧序列后缀解析（例如 _f0 / _f1）</summary>
        public bool useFrameSequence = false;

        /// <summary>当找不到任何可用变体时是否隐藏图层，而不是回退到基础图</summary>
        public bool hideWhenMissingVariant = false;

        /// <summary>眼睛层的渲染模式</summary>
        public EyeRenderMode eyeRenderMode = EyeRenderMode.TextureSwap;

        /// <summary>眼球 UV 偏移范围（供后续 UvOffset 模式使用）</summary>
        public float eyeUvMoveRange = 0f;

        /// <summary>
        /// 指定可见的高层表情名称列表；为空表示不限制。
        /// 使用 <see cref="ExpressionType"/> 的名称进行匹配。
        /// </summary>
        public string[] visibleExpressions = Array.Empty<string>();

        /// <summary>
        /// 指定隐藏的高层表情名称列表；为空表示不限制。
        /// 使用 <see cref="ExpressionType"/> 的名称进行匹配。
        /// </summary>
        public string[] hiddenExpressions = Array.Empty<string>();

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
                scaleEastMultiplier = this.scaleEastMultiplier,
                scaleNorthMultiplier = this.scaleNorthMultiplier,
                rotation = this.rotation,
                rotationEastOffset = this.rotationEastOffset,
                rotationNorthOffset = this.rotationNorthOffset,
                flipHorizontal = this.flipHorizontal,
                rotDrawMode = this.rotDrawMode,
                workerClass = this.workerClass,
                graphicClass = this.graphicClass,
#pragma warning disable CS0618
                shaderTypeDef = this.shaderTypeDef,
#pragma warning restore CS0618
                shaderDefName = this.shaderDefName,
                colorSource = this.colorSource,
                customColor = this.customColor,
                colorTwoSource = this.colorTwoSource,
                customColorTwo = this.customColorTwo,
                maskTexPath = this.maskTexPath,
                weaponCarryVisual = this.weaponCarryVisual?.Clone(),
                visible = this.visible,
                role = this.role,
                variantLogic = this.variantLogic,
                variantBaseName = this.variantBaseName,
                useDirectionalSuffix = this.useDirectionalSuffix,
                useExpressionSuffix = this.useExpressionSuffix,
                useEyeDirectionSuffix = this.useEyeDirectionSuffix,
                useBlinkSuffix = this.useBlinkSuffix,
                useFrameSequence = this.useFrameSequence,
                hideWhenMissingVariant = this.hideWhenMissingVariant,
                eyeRenderMode = this.eyeRenderMode,
                eyeUvMoveRange = this.eyeUvMoveRange,
                visibleExpressions = (string[])this.visibleExpressions.Clone(),
                hiddenExpressions = (string[])this.hiddenExpressions.Clone(),
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
    /// 图层角色
    /// </summary>
    public enum LayerRole
    {
        Base,
        Head,
        Brow,
        Eye,
        Lid,
        Mouth,
        Emotion,
        SkinMark,
        Decoration
    }

    /// <summary>
    /// 图层变体逻辑
    /// </summary>
    public enum LayerVariantLogic
    {
        None,
        ExpressionOnly,
        EyeDirectionOnly,
        BlinkOnly,
        ExpressionAndDirection,
        ChannelState,
        Sequence
    }

    /// <summary>
    /// 眼睛渲染模式
    /// </summary>
    public enum EyeRenderMode
    {
        TextureSwap,
        UvOffset
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
    /// 图层颜色来源
    /// </summary>
    public enum LayerColorSource
    {
        /// <summary>使用自定义固定颜色</summary>
        Fixed,
        /// <summary>使用角色头发颜色 (story.HairColor)</summary>
        PawnHair,
        /// <summary>使用角色皮肤颜色 (story.SkinColor)</summary>
        PawnSkin,
        /// <summary>使用服装主色 (apparel.DrawColor)</summary>
        PawnApparelPrimary,
        /// <summary>使用服装副色 (apparel.DrawColorTwo)</summary>
        PawnApparelSecondary,
        /// <summary>使用白色（不着色）</summary>
        White
    }

    /// <summary>
    /// [Obsolete] 图层颜色类型 (旧版兼容)
    /// </summary>
    [Obsolete("Use LayerColorSource instead")]
    public enum LayerColorType
    {
        Custom,
        Hair,
        Skin,
        White
    }
}