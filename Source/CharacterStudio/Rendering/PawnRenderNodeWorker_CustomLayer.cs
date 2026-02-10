using System;
using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;
using System.Linq;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 自定义图层渲染工作器
    /// 用于渲染皮肤系统添加的自定义图层
    /// </summary>
    public class PawnRenderNodeWorker_CustomLayer : PawnRenderNodeWorker
    {
        /// <summary>
        /// 判断是否可以绘制
        /// </summary>
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
                return false;

            // 检查自定义节点的可见性配置
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                if (!customNode.config.visible)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 获取偏移量
        /// 优先从 config 读取偏移值，确保导入的数据被正确应用
        /// 同时支持位移动画
        /// </summary>
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            // base.OffsetFor 已经应用了 node.DebugOffset (即 config.offset)
            Vector3 baseOffset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNode_Custom customNode = node as PawnRenderNode_Custom;
            
            if (customNode != null && customNode.config != null)
            {
                // 不再重复应用 config.offset
                
                // 应用动画位移偏移
                if (customNode.config.animAffectsOffset && customNode.config.animationType != LayerAnimationType.None)
                {
                    baseOffset += customNode.currentAnimOffset;
                }
            }

            // 根据朝向应用额外的方向特定偏移
            Rot4 facing = parms.facing;
            
            // 侧面朝向（East或West）应用 offsetEast
            if (facing == Rot4.East || facing == Rot4.West)
            {
                Vector3 eastOffset = Vector3.zero;
                bool hasEastOffset = false;

                // 1. 优先从 config 获取
                if (customNode != null && customNode.config != null)
                {
                    eastOffset = customNode.config.offsetEast;
                    hasEastOffset = true;
                }
                // 2. 否则尝试从 RuntimeAssetLoader 获取 (兼容旧逻辑或非 Custom 节点)
                else
                {
                    int nodeId = node.GetHashCode();
                    if (RuntimeAssetLoader.TryGetOffsetEast(nodeId, out Vector3 loaderOffset))
                    {
                        eastOffset = loaderOffset;
                        hasEastOffset = true;
                    }
                }

                if (hasEastOffset && eastOffset != Vector3.zero)
                {
                    // 如果是西面朝向，需要翻转X轴偏移
                    if (facing == Rot4.West)
                    {
                        eastOffset.x = -eastOffset.x;
                    }
                    baseOffset += eastOffset;
                }
            }
            // 北面朝向应用 offsetNorth
            else if (facing == Rot4.North)
            {
                Vector3 northOffset = Vector3.zero;
                bool hasNorthOffset = false;

                // 1. 优先从 config 获取
                if (customNode != null && customNode.config != null)
                {
                    northOffset = customNode.config.offsetNorth;
                    hasNorthOffset = true;
                }
                // 2. 否则尝试从 RuntimeAssetLoader 获取 (兼容旧逻辑或非 Custom 节点)
                else
                {
                    int nodeId = node.GetHashCode();
                    if (RuntimeAssetLoader.TryGetOffsetNorth(nodeId, out Vector3 loaderOffset))
                    {
                        northOffset = loaderOffset;
                        hasNorthOffset = true;
                    }
                }

                if (hasNorthOffset && northOffset != Vector3.zero)
                {
                    baseOffset += northOffset;
                }
            }

            return baseOffset;
        }

        /// <summary>
        /// 获取缩放
        /// 优先从 config 读取缩放值，确保导入的数据被正确应用
        /// 同时支持呼吸动画缩放
        /// </summary>
        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            // 注意：base.ScaleFor 默认会应用 node.Props.drawSize。
            // 但我们在 GetGraphic 中已经使用 drawSize 创建了 Graphic，这决定了 Mesh 的大小。
            // 如果 ScaleFor 再返回 drawSize，会导致双重缩放 (Mesh大小 * 矩阵缩放)。
            // 因此，我们这里不调用 base.ScaleFor，而是手动构建缩放向量。
            
            Vector3 scale = Vector3.one;

            // 应用调试缩放
            if (node.debugScale != 1f)
            {
                scale *= node.debugScale;
            }
            
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                // 应用呼吸动画缩放
                if (customNode.config.animationType == LayerAnimationType.Breathe)
                {
                    scale *= customNode.currentAnimScale;
                }
            }

            return scale;
        }

        /// <summary>
        /// 获取旋转
        /// </summary>
        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion baseRot = base.RotationFor(node, parms);
            
            // 添加调试角度偏移
            if (Mathf.Abs(node.debugAngleOffset) > 0.01f)
            {
                baseRot *= Quaternion.Euler(0f, node.debugAngleOffset, 0f);
            }

            // 动画逻辑
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                var config = customNode.config;
                if (config.animationType != LayerAnimationType.None)
                {
                    // 计算动画
                    CalculateAnimation(customNode, config);
                    
                    // 应用旋转动画（Breathe 类型不应用旋转）
                    if (config.animationType != LayerAnimationType.Breathe)
                    {
                        float animAngle = customNode.currentAnimAngle;
                        if (Mathf.Abs(animAngle) > 0.01f)
                        {
                            baseRot *= Quaternion.Euler(0f, animAngle, 0f);
                        }
                    }
                }
            }

            return baseRot;
        }

        /// <summary>
        /// 计算动画效果
        /// 统一处理所有动画类型，计算旋转角度、位移偏移和缩放
        /// </summary>
        private void CalculateAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config)
        {
            int currentTick = (Current.ProgramState == ProgramState.Playing)
                ? Find.TickManager.TicksGame
                : (int)(Time.realtimeSinceStartup * 60f);

            // 初始化基础相位（基于节点哈希，确保不同图层错开）
            if (customNode.basePhase == 0f && config.animPhaseOffset == 0f)
            {
                customNode.basePhase = (customNode.GetHashCode() % 1000) / 1000f * Mathf.PI * 2f;
            }
            
            float phaseOffset = customNode.basePhase + config.animPhaseOffset * Mathf.PI * 2f;
            float timeSec = currentTick / 60f;

            switch (config.animationType)
            {
                case LayerAnimationType.Twitch:
                    CalculateTwitchAnimation(customNode, config, currentTick);
                    break;

                case LayerAnimationType.Swing:
                    CalculateSwingAnimation(customNode, config, timeSec, phaseOffset);
                    break;

                case LayerAnimationType.IdleSway:
                    CalculateIdleSwayAnimation(customNode, config, timeSec, phaseOffset);
                    break;

                case LayerAnimationType.Breathe:
                    CalculateBreatheAnimation(customNode, config, timeSec, phaseOffset);
                    break;
            }
        }

        /// <summary>
        /// 计算抽动动画（兽耳）
        /// 随机触发的快速抖动
        /// </summary>
        private void CalculateTwitchAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config, int currentTick)
        {
            // 初始化下次抽动时间
            if (customNode.nextTwitchTick < 0)
            {
                int interval = (int)(600 / Mathf.Max(0.1f, config.animFrequency));
                customNode.nextTwitchTick = currentTick + Rand.Range(interval / 2, interval * 2) + customNode.GetHashCode() % 100;
            }

            // 触发抽动
            if (!customNode.isTwitching && currentTick >= customNode.nextTwitchTick)
            {
                customNode.isTwitching = true;
                customNode.twitchStartTick = currentTick;
                
                int interval = (int)(600 / Mathf.Max(0.1f, config.animFrequency));
                customNode.nextTwitchTick = currentTick + Rand.Range(interval / 2, interval * 2);
            }

            // 计算抽动动画
            if (customNode.isTwitching)
            {
                int duration = (int)(15 / Mathf.Max(0.1f, config.animSpeed));
                float progress = (float)(currentTick - customNode.twitchStartTick) / duration;
                
                if (progress >= 1f)
                {
                    customNode.isTwitching = false;
                    customNode.currentAnimAngle = 0f;
                    customNode.currentAnimOffset = Vector3.zero;
                }
                else
                {
                    // 使用正弦波实现平滑的抽动
                    float twitchValue = Mathf.Sin(progress * Mathf.PI);
                    customNode.currentAnimAngle = twitchValue * config.animAmplitude;
                    
                    // 位移动画
                    if (config.animAffectsOffset)
                    {
                        customNode.currentAnimOffset = new Vector3(
                            twitchValue * config.animOffsetAmplitude * 0.5f,
                            0f,
                            twitchValue * config.animOffsetAmplitude
                        );
                    }
                }
            }
            else
            {
                customNode.currentAnimAngle = 0f;
                customNode.currentAnimOffset = Vector3.zero;
            }
        }

        /// <summary>
        /// 计算摆动动画（尾巴硬摆）
        /// 简单的正弦波摆动，带二次谐波
        /// </summary>
        private void CalculateSwingAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config, float timeSec, float phaseOffset)
        {
            float freq = config.animFrequency;
            float amp = config.animAmplitude;
            
            // 主波形 + 二次谐波（让动作更自然）
            float primaryWave = Mathf.Sin(timeSec * freq + phaseOffset);
            float secondaryWave = Mathf.Sin(timeSec * freq * 2f + phaseOffset) * 0.3f;
            
            customNode.currentAnimAngle = (primaryWave + secondaryWave) * amp;
            
            // 位移动画
            if (config.animAffectsOffset)
            {
                float offsetValue = primaryWave * config.animOffsetAmplitude;
                customNode.currentAnimOffset = new Vector3(offsetValue, 0f, 0f);
            }
            else
            {
                customNode.currentAnimOffset = Vector3.zero;
            }
        }

        /// <summary>
        /// 计算轻柔摇曳动画（尾巴自然晃动）
        /// 使用复合正弦波实现更自然的效果
        /// 公式: sin(t * f) * 0.6 + sin(t * f * 1.7 + π/3) * 0.25 + sin(t * f * 0.5) * 0.15
        /// </summary>
        private void CalculateIdleSwayAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config, float timeSec, float phaseOffset)
        {
            float freq = config.animFrequency;
            float amp = config.animAmplitude;
            
            // 复合正弦波：主波 + 快速波 + 慢速波
            // 这创造出更自然、不规则的摇曳效果
            float wave1 = Mathf.Sin(timeSec * freq + phaseOffset) * 0.6f;                           // 主波
            float wave2 = Mathf.Sin(timeSec * freq * 1.7f + phaseOffset + Mathf.PI / 3f) * 0.25f;   // 快速谐波
            float wave3 = Mathf.Sin(timeSec * freq * 0.5f + phaseOffset) * 0.15f;                   // 慢速调制
            
            float compositeWave = wave1 + wave2 + wave3;
            customNode.currentAnimAngle = compositeWave * amp;
            
            // 位移动画 - X轴随旋转方向轻微移动
            if (config.animAffectsOffset)
            {
                float offsetX = compositeWave * config.animOffsetAmplitude;
                // Z轴使用不同相位的波形
                float offsetZ = Mathf.Sin(timeSec * freq * 0.8f + phaseOffset + Mathf.PI / 2f) * config.animOffsetAmplitude * 0.5f;
                customNode.currentAnimOffset = new Vector3(offsetX, 0f, offsetZ);
            }
            else
            {
                customNode.currentAnimOffset = Vector3.zero;
            }
        }

        /// <summary>
        /// 计算呼吸动画（缩放起伏）
        /// 适用于胸部等需要呼吸效果的部位
        /// </summary>
        private void CalculateBreatheAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config, float timeSec, float phaseOffset)
        {
            float freq = config.animFrequency * 0.5f; // 呼吸频率较慢
            float amp = config.animAmplitude * 0.01f; // 振幅转换为缩放系数（15度 -> 0.15）
            
            // 使用平滑的正弦波
            float breatheValue = Mathf.Sin(timeSec * freq + phaseOffset);
            
            // 缩放在 1-amp 到 1+amp 之间变化
            customNode.currentAnimScale = 1f + breatheValue * amp;
            
            // 呼吸动画不影响旋转
            customNode.currentAnimAngle = 0f;
            
            // 呼吸时轻微上下移动
            if (config.animAffectsOffset)
            {
                float offsetZ = breatheValue * config.animOffsetAmplitude;
                customNode.currentAnimOffset = new Vector3(0f, 0f, offsetZ);
            }
            else
            {
                customNode.currentAnimOffset = Vector3.zero;
            }
        }

        /// <summary>
        /// 获取图层值（用于层级排序）
        /// AltitudeFor 不是 virtual，但 LayerFor 是 virtual
        /// </summary>
        public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
        {
            float baseLayer = base.LayerFor(node, parms);
            
            // 调试层级偏移已在基类中处理
            return baseLayer;
        }

        /// <summary>
        /// 获取指定源的颜色
        /// </summary>
        private Color GetColorFromSource(LayerColorSource source, Pawn pawn, Color fixedColor)
        {
            switch (source)
            {
                case LayerColorSource.PawnHair:
                    return pawn?.story?.HairColor ?? Color.white;
                case LayerColorSource.PawnSkin:
                    return pawn?.story?.SkinColor ?? Color.white;
                case LayerColorSource.PawnApparelPrimary:
                    return pawn?.apparel?.WornApparel?.FirstOrDefault()?.DrawColor ?? Color.white; // 简化处理
                case LayerColorSource.PawnApparelSecondary:
                    // 原版不支持 DrawColorTwo，这是 HAR 特性，暂时回退到主色或白色
                    return Color.white;
                case LayerColorSource.Fixed:
                default:
                    return fixedColor;
            }
        }

        /// <summary>
        /// 覆盖 GetMaterialPropertyBlock 以应用自定义颜色
        /// </summary>
        public override MaterialPropertyBlock GetMaterialPropertyBlock(
            PawnRenderNode node,
            Material material,
            PawnDrawParms parms)
        {
            MaterialPropertyBlock matPropBlock = node.MatPropBlock;
            
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                var config = customNode.config;
                var pawn = parms.pawn;

                // 1. 获取主颜色
                Color colorOne = GetColorFromSource(config.colorSource, pawn, config.customColor);
                
                // 2. 获取副颜色 (Mask)
                Color colorTwo = GetColorFromSource(config.colorTwoSource, pawn, config.customColorTwo);

                if (parms.Statue)
                {
                    matPropBlock.SetColor(ShaderPropertyIDs.Color, parms.statueColor.Value);
                }
                else
                {
                    // 应用主颜色与 tint 的组合
                    matPropBlock.SetColor(ShaderPropertyIDs.Color, parms.tint * colorOne);
                    
                    // 应用第二颜色（仅当使用支持 Mask 的 Shader 时有效）
                    // 始终设置 _ColorTwo，因为如果 Shader 不支持，设置属性也不会有负面影响
                    int colorTwoID = Shader.PropertyToID("_ColorTwo");
                    matPropBlock.SetColor(colorTwoID, colorTwo);
                }
            }
            else
            {
                // 非自定义节点的默认处理（虽然此 Worker 只用于自定义节点）
                base.GetMaterialPropertyBlock(node, material, parms);
            }
            
            return matPropBlock;
        }

        /// <summary>
        /// 获取图形
        /// 阶段3: 支持表情状态变体
        /// </summary>
        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            if (string.IsNullOrEmpty(node.Props?.texPath))
                return null;

            var texPath = node.Props?.texPath ?? "";
            
            // 阶段3: 尝试获取状态变体路径
            string resolvedTexPath = ResolveExpressionVariant(texPath, parms.pawn);
            
            // Log.Message($"[CharacterStudio] GetGraphic for {node.Props.debugLabel}: Path={texPath}, Resolved={resolvedTexPath}"); // 调试日志

            // 检查是否是绝对路径，或者是外部文件
            // 如果路径包含盘符或特定的外部路径标识，我们认为它是运行时资源
            bool isExternal = resolvedTexPath.Contains(":") ||
                              resolvedTexPath.StartsWith("/") ||
                              System.IO.File.Exists(resolvedTexPath);

            if (isExternal)
            {
                if (!System.IO.File.Exists(resolvedTexPath))
                {
                    Log.Error($"[CharacterStudio] 外部纹理文件不存在: {resolvedTexPath}");
                }

                Shader shader = node.ShaderFor(parms.pawn);
                if (shader == null)
                {
                    shader = ShaderDatabase.Cutout;
                }

                var props = node.Props;
                
                // 不使用 GraphicDatabase 缓存 Graphic_Runtime，以支持实时热加载
                // 每次 GetGraphic 被调用时（通常在 SetAllGraphicsDirty 后），我们都创建一个新的 Graphic 实例
                // 底层的 RuntimeAssetLoader 仍然会缓存纹理和材质，所以性能开销很小
                var req = new GraphicRequest(
                    typeof(Graphic_Runtime),
                    resolvedTexPath,
                    shader,
                    props?.drawSize ?? Vector2.one,
                    props?.color ?? Color.white,
                    Color.white, null, 0, null, null
                );
                
                var graphic = new Graphic_Runtime();
                graphic.Init(req);
                return graphic;
            }
            
            // 不再在这里进行预检查，因为：
            // 1. ContentFinder 的预检查可能与 GraphicDatabase 的实际加载行为不一致
            // 2. 外部模组的纹理路径格式可能有所不同
            // 3. GraphicDatabase.Get 内部会处理纹理加载和错误报告
            // 如果纹理真的找不到，GraphicDatabase 会产生粉色方块，这是更明显的错误指示

            // 统一处理：无论是原始路径还是变体路径，都尝试使用正确的 Graphic 类型
            // 修复: 必须使用原始类加载，否则 Graphic_Multi 的资源用 Graphic_Single 加载会失败（粉色方块）
            if (node is PawnRenderNode_Custom customNode)
            {
                Type graphicType = customNode.config?.graphicClass;
                
                // 排除 Graphic_Runtime，因为它只用于外部资源
                if (graphicType != typeof(Graphic_Runtime))
                {
                    // 智能探测：如果纹理存在性与类型不符，或者类型未知，尝试自动推断
                    // 这解决了导入时 graphicClass 可能不准确，或特殊 Graphic 类无法在缺乏数据时初始化的问题
                    if (graphicType == null || (graphicType != typeof(Graphic_Multi) && graphicType != typeof(Graphic_Single)))
                    {
                        // 检查是否存在 _north 纹理，如果存在则大概率是 Graphic_Multi
                        if (ContentFinder<Texture2D>.Get(resolvedTexPath + "_north", false) != null)
                        {
                            graphicType = typeof(Graphic_Multi);
                        }
                        // 否则如果存在主纹理，则是 Graphic_Single
                        else if (ContentFinder<Texture2D>.Get(resolvedTexPath, false) != null)
                        {
                            graphicType = typeof(Graphic_Single);
                        }
                        // 默认回退
                        if (graphicType == null) graphicType = typeof(Graphic_Multi);
                    }

                    // 支持 Shader 切换
                    Shader shader = null;
                    if (!string.IsNullOrEmpty(customNode.config.shaderDefName))
                    {
                        switch (customNode.config.shaderDefName)
                        {
                            case "Cutout": shader = ShaderDatabase.Cutout; break;
                            case "CutoutComplex": shader = ShaderDatabase.CutoutComplex; break;
                            case "Transparent": shader = ShaderDatabase.Transparent; break;
                            case "TransparentPostLight": shader = ShaderDatabase.TransparentPostLight; break;
                            case "MetaOverlay": shader = ShaderDatabase.MetaOverlay; break;
                        }
                    }
                    if (shader == null) shader = node.ShaderFor(parms.pawn) ?? ShaderDatabase.Cutout;
                    
                    var props = node.Props;
                    
                    // 确保 drawSize 合理，避免不可见
                    Vector2 drawSize = props?.drawSize ?? Vector2.one;
                    if (drawSize.x <= 0f) drawSize.x = 1f;
                    if (drawSize.y <= 0f) drawSize.y = 1f;

                    // 获取颜色（基于源）
                    Color color = GetColorFromSource(customNode.config.colorSource, parms.pawn, customNode.config.customColor);
                    Color colorTwo = GetColorFromSource(customNode.config.colorTwoSource, parms.pawn, customNode.config.customColorTwo);

                    if (graphicType == typeof(Graphic_Multi))
                    {
                        return GraphicDatabase.Get<Graphic_Multi>(resolvedTexPath, shader, drawSize, color, colorTwo);
                    }
                    else if (graphicType == typeof(Graphic_Single))
                    {
                        return GraphicDatabase.Get<Graphic_Single>(resolvedTexPath, shader, drawSize, color);
                    }
                    else
                    {
                        // 对于其他类型，尝试使用反射
                        // 优先尝试带 ColorTwo 的重载
                        var method = typeof(GraphicDatabase).GetMethod("Get", new Type[] { typeof(string), typeof(Shader), typeof(Vector2), typeof(Color), typeof(Color) });
                        if (method != null)
                        {
                            var genericMethod = method.MakeGenericMethod(graphicType);
                            return (Graphic)genericMethod.Invoke(null, new object[] {
                                resolvedTexPath,
                                shader,
                                drawSize,
                                color,
                                colorTwo
                            });
                        }
                        
                        // 回退到不带 ColorTwo 的重载
                        method = typeof(GraphicDatabase).GetMethod("Get", new Type[] { typeof(string), typeof(Shader), typeof(Vector2), typeof(Color) });
                        if (method != null)
                        {
                            var genericMethod = method.MakeGenericMethod(graphicType);
                            return (Graphic)genericMethod.Invoke(null, new object[] {
                                resolvedTexPath,
                                shader,
                                drawSize,
                                color
                            });
                        }
                        else
                        {
                            Log.Warning($"[CharacterStudio] 无法找到匹配的 GraphicDatabase.Get 方法用于类型 {graphicType}");
                        }
                        return null;
                    }
                }
            }

            // 否则使用默认逻辑（但仍使用变体路径）
            if (resolvedTexPath != texPath)
            {
                // 路径已改变，需要重新获取图形
                Shader shader = node.ShaderFor(parms.pawn);
                if (shader == null)
                {
                    shader = ShaderDatabase.Cutout;
                }
                var props = node.Props;
                // 默认回退到 Graphic_Single，但这可能会有问题如果其实是 Multi
                // 这里的回退主要针对非 Custom 节点
                return GraphicDatabase.Get<Graphic_Single>(
                    resolvedTexPath,
                    shader,
                    props?.drawSize ?? Vector2.one,
                    props?.color ?? Color.white
                );
            }
            
            return base.GetGraphic(node, parms);
        }

        /// <summary>
        /// 阶段3: 根据 Pawn 状态解析表情变体路径
        /// 后缀优先级: _Sleep > _Angry > _Blink > 原始路径
        /// </summary>
        private string ResolveExpressionVariant(string basePath, Pawn pawn)
        {
            if (pawn == null || string.IsNullOrEmpty(basePath))
                return basePath;

            try
            {
                // 检测睡眠状态（检查是否在床上）
                if (pawn.jobs?.curDriver != null && RestUtility.InBed(pawn))
                {
                    string sleepPath = basePath + "_Sleep";
                    if (TextureExists(sleepPath))
                        return sleepPath;
                }

                // 检测愤怒状态（被征召或精神崩溃中）
                if (pawn.Drafted || (pawn.MentalState != null && pawn.MentalState.def.IsAggro))
                {
                    string angryPath = basePath + "_Angry";
                    if (TextureExists(angryPath))
                        return angryPath;
                }

                // 眨眼状态（基于 tick 的周期性检测）
                // 每 200 ticks 眨眼一次，持续 10 ticks
                int tickCycle = Find.TickManager.TicksGame % 200;
                if (tickCycle < 10)
                {
                    string blinkPath = basePath + "_Blink";
                    if (TextureExists(blinkPath))
                        return blinkPath;
                }
            }
            catch
            {
                // 状态检测失败时使用原始路径
            }

            return basePath;
        }

        /// <summary>
        /// 检查纹理是否存在
        /// </summary>
        private bool TextureExists(string path)
        {
            // 对于外部路径
            if (path.Contains(":") || path.StartsWith("/"))
            {
                return System.IO.File.Exists(path) ||
                       System.IO.File.Exists(path + ".png") ||
                       System.IO.File.Exists(path + ".jpg");
            }
            
            // 对于游戏内路径，使用 ContentFinder
            return ContentFinder<Texture2D>.Get(path, false) != null;
        }
    }
}
