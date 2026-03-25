using System;
using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;
using System.Linq;
using System.Collections.Generic;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 自定义图层渲染工作器
    /// 用于渲染皮肤系统添加的自定义图层
    /// </summary>
    public class PawnRenderNodeWorker_CustomLayer : PawnRenderNodeWorker
    {
        // 避免在每帧重复打印同一节点的缩放回退日志
        private static readonly System.Collections.Generic.HashSet<int> _loggedScaleFallbackNodes = new System.Collections.Generic.HashSet<int>();
        private static readonly Dictionary<string, bool> textureExistsCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> frameSequenceCountCache = new Dictionary<string, int>(StringComparer.Ordinal);
        private const float ProgrammaticFaceFadeInStep = 0.12f;
        private const float ProgrammaticFaceFadeOutStep = 0.08f;
        private const float ProgrammaticFaceAlphaSnapThreshold = 0.01f;
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

                if (!IsTriggeredEquipmentLayerVisible(customNode, parms.pawn))
                    return false;

                if (!IsExpressionVisibleForLayer(customNode.config, parms.pawn))
                    return false;

                // 双轨运行时裁剪：
                // World Track 下，LayeredDynamic 仅保留 Base 节点作为保底世界轨脸；
                // 其他眉眼嘴/Overlay/Pupil/Lid 等局部节点全部关闭。
                if (customNode.layeredFacePartType.HasValue)
                {
                    FaceRenderTrack currentTrack = parms.pawn?.TryGetComp<CompPawnSkin>()?.CurrentFaceRuntimeState.currentTrack
                        ?? FaceRenderTrack.World;

                    if (currentTrack == FaceRenderTrack.World
                        && customNode.layeredFacePartType.Value != LayeredFacePartType.Base)
                    {
                        return false;
                    }

                    EnsureProgrammaticFaceStateUpdated(customNode, parms.pawn);
                    if (customNode.targetProgrammaticAlpha <= ProgrammaticFaceAlphaSnapThreshold
                        && customNode.currentProgrammaticAlpha <= ProgrammaticFaceAlphaSnapThreshold)
                    {
                        return false;
                    }
                }
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
            PawnRenderNode_Custom? customNode = node as PawnRenderNode_Custom;
            
            if (customNode != null && customNode.config != null)
            {
                // 不再重复应用 config.offset
                
                // 应用动画位移偏移
                if ((customNode.config.animAffectsOffset && customNode.config.animationType != LayerAnimationType.None)
                    || customNode.config.useTriggeredEquipmentAnimation)
                {
                    baseOffset += customNode.currentAnimOffset;
                }

                EnsureProgrammaticFaceStateUpdated(customNode, parms.pawn);
                baseOffset += customNode.currentProgrammaticOffset;
            }

            // 根据朝向应用额外的方向特定偏移
            Rot4 facing = parms.facing;

            CompPawnSkin? skinComp = parms.pawn?.TryGetComp<CompPawnSkin>();
            if (skinComp != null && skinComp.IsFlightStateActive())
            {
                float liftFactor = skinComp.GetFlightLiftFactor01();
                float flightBaseHeight = skinComp.flightStateHeightFactor * liftFactor;
                baseOffset.y += flightBaseHeight + skinComp.GetFlightHoverOffset();
            }
            
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
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                Vector2 cfg = GetConfiguredScale(customNode.config, parms.facing);
                float sx = cfg.x <= 0f ? 1f : cfg.x;
                float sy = cfg.y <= 0f ? 1f : cfg.y;

                Vector2 originalDrawSize = node.Props.drawSize;
                node.Props.drawSize = Vector2.one;
                Vector3 raceScale;
                try
                {
                    raceScale = base.ScaleFor(node, parms);
                }
                finally
                {
                    node.Props.drawSize = originalDrawSize;
                }

                Vector3 scale = new Vector3(sx * raceScale.x, 1f, sy * raceScale.z);

                if (customNode.config.animationType == LayerAnimationType.Breathe)
                {
                    scale *= customNode.currentAnimScale;
                }

                EnsureProgrammaticFaceStateUpdated(customNode, parms.pawn);
                scale = new Vector3(
                    scale.x * customNode.currentProgrammaticScale.x,
                    scale.y * customNode.currentProgrammaticScale.y,
                    scale.z * customNode.currentProgrammaticScale.z);

                if (node.debugScale != 1f)
                {
                    scale *= node.debugScale;
                }

                return scale;
            }

            Vector3 baseScale = base.ScaleFor(node, parms);
            if (node.debugScale != 1f)
            {
                baseScale *= node.debugScale;
            }

            return baseScale;
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

            // 应用配置旋转与动画旋转
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                var config = customNode.config;

                if (Mathf.Abs(GetConfiguredRotation(config, parms.facing)) > 0.01f)
                {
                    baseRot *= Quaternion.Euler(0f, GetConfiguredRotation(config, parms.facing), 0f);
                }

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

                ApplyTriggeredEquipmentAnimation(customNode, parms.pawn);
                if (customNode.config.useTriggeredEquipmentAnimation && Mathf.Abs(customNode.currentAnimAngle) > 0.01f)
                {
                    baseRot *= Quaternion.Euler(0f, customNode.currentAnimAngle, 0f);
                }

                EnsureProgrammaticFaceStateUpdated(customNode, parms.pawn);
                if (Mathf.Abs(customNode.currentProgrammaticAngle) > 0.01f)
                {
                    baseRot *= Quaternion.Euler(0f, customNode.currentProgrammaticAngle, 0f);
                }
            }

            return baseRot;
        }

        private static Vector2 GetConfiguredScale(PawnLayerConfig config, Rot4 facing)
        {
            Vector2 scale = config.scale;
            if (facing == Rot4.East || facing == Rot4.West)
                return new Vector2(scale.x * config.scaleEastMultiplier.x, scale.y * config.scaleEastMultiplier.y);

            return scale;
        }

        private static float GetConfiguredRotation(PawnLayerConfig config, Rot4 facing)
        {
            float rotation = config.rotation;
            if (facing == Rot4.North)
                return rotation + config.rotationNorthOffset;

            if (facing == Rot4.East)
                return rotation + config.rotationEastOffset;

            if (facing == Rot4.West)
                return rotation - config.rotationEastOffset;

            return rotation;
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

                case LayerAnimationType.Spin:
                    CalculateSpinAnimation(customNode, config, currentTick);
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
        /// <summary>
        /// 计算持续旋转动画（Spin）
        /// 图层以枢轴点为中心匀速旋转，适用于旋翼/飞行器等。
        /// animFrequency 控制转速（转/秒），animAmplitude 无效（360° 全旋）。
        /// config.animPivotOffset 指定枢轴点相对于锚点的偏移量（X/Z）。
        /// </summary>
        private void CalculateSpinAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config, int currentTick)
        {
            if (customNode.lastSpinTick < 0)
            {
                customNode.lastSpinTick = currentTick;
                customNode.currentSpinAngle = 0f;
            }

            int deltaTicks = currentTick - customNode.lastSpinTick;
            customNode.lastSpinTick = currentTick;

            // 每 tick 旋转角度：animFrequency 转/秒，每秒 60 tick
            float degreesPerTick = config.animFrequency * 360f / 60f * config.animSpeed;
            customNode.currentSpinAngle = (customNode.currentSpinAngle + degreesPerTick * deltaTicks) % 360f;

            // 通用路径通过 currentAnimAngle 传递旋转值
            customNode.currentAnimAngle = customNode.currentSpinAngle;
            customNode.currentAnimOffset = Vector3.zero;
            customNode.currentAnimScale = 1f;

            // 枢轴偏移：将图层在旋转前先平移到枢轴点，旋转后再平移回去
            // 这里将枢轴偏移存入 currentAnimOffset 以供 OffsetFor 叠加
            // 枢轴旋转公式：offset = pivot - R(pivot, angle)
            // 其中 pivot = animPivotOffset, R 为绕 Y 轴旋转矩阵
            Vector2 pivot = config.animPivotOffset;
            if (pivot != Vector2.zero)
            {
                float rad = customNode.currentSpinAngle * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                // 世界平面：X=东西, Z=南北
                float rotX = cos * pivot.x - sin * pivot.y;
                float rotZ = sin * pivot.x + cos * pivot.y;
                // 偏移 = 原始枢轴 - 旋转后枢轴位置
                customNode.currentAnimOffset = new Vector3(
                    pivot.x - rotX,
                    0f,
                    pivot.y - rotZ);
            }
        }

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

        private void EnsureProgrammaticFaceStateUpdated(PawnRenderNode_Custom customNode, Pawn? pawn)
        {
            if (!customNode.layeredFacePartType.HasValue || pawn == null)
            {
                ResetProgrammaticFaceTransform(customNode);
                return;
            }

            int currentTick = (Current.ProgramState == ProgramState.Playing)
                ? (Find.TickManager?.TicksGame ?? 0)
                : (int)(Time.realtimeSinceStartup * 60f);

            if (customNode.lastProgrammaticFaceTick == currentTick)
                return;

            customNode.lastProgrammaticFaceTick = currentTick;
            CalculateProgrammaticFaceTransform(customNode, pawn, currentTick);
        }

        private void ResetProgrammaticFaceTransform(PawnRenderNode_Custom customNode)
        {
            customNode.currentProgrammaticAngle = 0f;
            customNode.currentProgrammaticOffset = Vector3.zero;
            customNode.currentProgrammaticScale = Vector3.one;
            customNode.targetProgrammaticAlpha = 1f;
        }

        private void HideProgrammaticFacePart(PawnRenderNode_Custom customNode)
        {
            customNode.currentProgrammaticAngle = 0f;
            customNode.currentProgrammaticOffset = Vector3.zero;
            customNode.currentProgrammaticScale = Vector3.one;
            customNode.targetProgrammaticAlpha = 0f;
        }

        private void SetProgrammaticFaceTransform(PawnRenderNode_Custom customNode, float angle, Vector3 offset, Vector3 scale, float targetAlpha = 1f)
        {
            customNode.currentProgrammaticAngle = angle;
            customNode.currentProgrammaticOffset = offset;
            customNode.currentProgrammaticScale = scale;
            customNode.targetProgrammaticAlpha = Mathf.Clamp01(targetAlpha);
        }

        private void UpdateProgrammaticFaceAlpha(PawnRenderNode_Custom customNode)
        {
            float targetAlpha = Mathf.Clamp01(customNode.targetProgrammaticAlpha);
            if (!customNode.hasProgrammaticAlphaInitialized)
            {
                customNode.currentProgrammaticAlpha = targetAlpha;
                customNode.hasProgrammaticAlphaInitialized = true;
                return;
            }

            float step = customNode.currentProgrammaticAlpha < targetAlpha
                ? ProgrammaticFaceFadeInStep
                : ProgrammaticFaceFadeOutStep;

            customNode.currentProgrammaticAlpha = Mathf.MoveTowards(customNode.currentProgrammaticAlpha, targetAlpha, step);
            if (Mathf.Abs(customNode.currentProgrammaticAlpha - targetAlpha) <= ProgrammaticFaceAlphaSnapThreshold)
            {
                customNode.currentProgrammaticAlpha = targetAlpha;
            }
        }

        private void CalculateProgrammaticFaceTransform(PawnRenderNode_Custom customNode, Pawn pawn, int currentTick)
        {
            ResetProgrammaticFaceTransform(customNode);

            if (!customNode.layeredFacePartType.HasValue)
                return;

            CompPawnSkin? skinComp = pawn.TryGetComp<CompPawnSkin>();
            if (skinComp == null)
                return;

            if (ShouldHideLayeredEyePartForReplacement(customNode, pawn, skinComp))
            {
                HideProgrammaticFacePart(customNode);
                UpdateProgrammaticFaceAlpha(customNode);
                return;
            }

            if (ShouldHideUpperLidAtBlinkEndpoint(customNode, skinComp))
            {
                HideProgrammaticFacePart(customNode);
                UpdateProgrammaticFaceAlpha(customNode);
                return;
            }

            float phase = customNode.basePhase;
            if (Mathf.Approximately(phase, 0f))
            {
                phase = (customNode.GetHashCode() % 1024) / 1024f * Mathf.PI * 2f;
                customNode.basePhase = phase;
            }

            float timeSec = currentTick / 60f;
            float primaryWave = Mathf.Sin(timeSec * 2.25f + phase);
            float slowWave = Mathf.Sin(timeSec * 0.9f + phase * 0.5f);
            ExpressionType expression = skinComp.GetEffectiveExpression();

            switch (customNode.layeredFacePartType.Value)
            {
                case LayeredFacePartType.Brow:
                    ApplyProgrammaticBrowTransform(customNode, skinComp.GetEffectiveBrowState(), primaryWave, slowWave);
                    break;

                case LayeredFacePartType.Eye:
                    ApplyProgrammaticEyeTransform(
                        customNode,
                        skinComp.GetEffectiveLidState(),
                        skinComp.CurEyeDirection,
                        expression,
                        primaryWave,
                        slowWave);
                    break;

                case LayeredFacePartType.Pupil:
                    ApplyProgrammaticPupilTransform(
                        customNode,
                        skinComp.GetEffectiveLidState(),
                        skinComp.CurEyeDirection,
                        expression,
                        primaryWave,
                        slowWave);
                    break;

                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                    ApplyProgrammaticLidTransform(
                        customNode,
                        customNode.layeredFacePartType.Value,
                        skinComp.GetEffectiveLidState(),
                        primaryWave,
                        slowWave);
                    break;

                case LayeredFacePartType.Mouth:
                    ApplyProgrammaticMouthTransform(customNode, skinComp.GetEffectiveMouthState(), expression, primaryWave, slowWave);
                    break;

                case LayeredFacePartType.Blush:
                case LayeredFacePartType.Tear:
                case LayeredFacePartType.Sweat:
                case LayeredFacePartType.Overlay:
                    ApplyProgrammaticEmotionTransform(
                        customNode,
                        GetProgrammaticEmotionPartType(customNode),
                        skinComp.GetEffectiveEmotionOverlayState(),
                        expression,
                        primaryWave,
                        slowWave);
                    break;
            }

            customNode.currentProgrammaticOffset = ApplyLayeredFacePartMotionAmplitude(
                customNode,
                customNode.currentProgrammaticOffset,
                primaryWave,
                slowWave);

            UpdateProgrammaticFaceAlpha(customNode);
        }

        private void ApplyProgrammaticBrowTransform(PawnRenderNode_Custom customNode, BrowState state, float primaryWave, float slowWave)
        {
            switch (state)
            {
                case BrowState.Angry:
                    SetProgrammaticFaceTransform(
                        customNode,
                        -4.5f + primaryWave * 0.6f,
                        new Vector3(0f, 0f, -0.004f + slowWave * 0.0008f),
                        new Vector3(1.04f, 1f, 0.97f));
                    break;

                case BrowState.Sad:
                    SetProgrammaticFaceTransform(
                        customNode,
                        3.25f + primaryWave * 0.45f,
                        new Vector3(0f, 0f, 0.0045f + slowWave * 0.0008f),
                        new Vector3(1.02f, 1f, 0.98f));
                    break;

                case BrowState.Happy:
                    SetProgrammaticFaceTransform(
                        customNode,
                        -1.5f + primaryWave * 0.25f,
                        new Vector3(0f, 0f, -0.0015f + slowWave * 0.0004f),
                        new Vector3(1.03f, 1f, 0.97f));
                    break;

                default:
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, slowWave * 0.0006f),
                        Vector3.one);
                    break;
            }
        }

        private static float GetLayeredFaceSideSign(PawnRenderNode_Custom customNode)
        {
            switch (customNode.layeredFacePartSide)
            {
                case LayeredFacePartSide.Left:
                    return -1f;
                case LayeredFacePartSide.Right:
                    return 1f;
                default:
                    return 0f;
            }
        }

        private static float GetLayeredFaceSideBias(PawnRenderNode_Custom customNode, float magnitude)
        {
            return GetLayeredFaceSideSign(customNode) * magnitude;
        }

        private void ApplyProgrammaticEyeTransform(
            PawnRenderNode_Custom customNode,
            LidState lidState,
            EyeDirection eyeDirection,
            ExpressionType expression,
            float primaryWave,
            float slowWave)
        {
            if (lidState == LidState.Blink || lidState == LidState.Close)
            {
                HideProgrammaticFacePart(customNode);
                return;
            }

            Pawn? pawn = customNode.tree?.pawn;
            CompPawnSkin? skinComp = pawn?.TryGetComp<CompPawnSkin>();
            EyeAnimationVariant eyeVariant = skinComp?.GetEffectiveEyeAnimationVariant() ?? EyeAnimationVariant.NeutralOpen;

            float sideSign = GetLayeredFaceSideSign(customNode);
            float offsetX = GetLayeredFaceSideBias(customNode, 0.0002f);
            float offsetZ = 0.0004f * primaryWave;
            switch (eyeDirection)
            {
                case EyeDirection.Left:
                    offsetX = -0.0012f;
                    break;
                case EyeDirection.Right:
                    offsetX = 0.0012f;
                    break;
                case EyeDirection.Up:
                    offsetZ -= 0.0010f;
                    break;
                case EyeDirection.Down:
                    offsetZ += 0.0012f;
                    break;
            }

            switch (eyeVariant)
            {
                case EyeAnimationVariant.NeutralSoft:
                    offsetZ += 0.0005f;
                    break;
                case EyeAnimationVariant.NeutralLookDown:
                    offsetZ += 0.0010f;
                    break;
                case EyeAnimationVariant.NeutralGlance:
                    offsetX += (primaryWave > 0f ? 0.0008f : -0.0008f) + sideSign * 0.00035f;
                    break;
                case EyeAnimationVariant.WorkFocusDown:
                    offsetZ += 0.0016f;
                    break;
                case EyeAnimationVariant.WorkFocusUp:
                    offsetZ -= 0.0012f;
                    break;
                case EyeAnimationVariant.HappySoft:
                    offsetZ -= 0.0006f;
                    break;
                case EyeAnimationVariant.ShockWide:
                    offsetZ -= 0.0018f;
                    break;
                case EyeAnimationVariant.ScaredWide:
                    offsetZ -= 0.0012f;
                    offsetX += primaryWave * 0.0006f + sideSign * 0.0003f;
                    break;
                case EyeAnimationVariant.ScaredFlinch:
                    offsetZ += 0.0008f;
                    offsetX += slowWave * 0.0007f + sideSign * 0.00045f;
                    break;
                case EyeAnimationVariant.HappyClosedPeak:
                    HideProgrammaticFacePart(customNode);
                    return;
            }

            float scaleZ = lidState == LidState.Half ? 0.92f : 1f;
            if (expression == ExpressionType.Shock || expression == ExpressionType.Scared)
                scaleZ = Mathf.Max(scaleZ, 1.06f + Mathf.Abs(primaryWave) * 0.02f);
            else if (expression == ExpressionType.Sleeping)
                scaleZ = Mathf.Min(scaleZ, 0.88f);
            else if (eyeVariant == EyeAnimationVariant.HappySoft)
                scaleZ = Mathf.Min(scaleZ, 0.90f);

            if (eyeVariant == EyeAnimationVariant.NeutralSoft)
                scaleZ = Mathf.Min(scaleZ, 0.95f);
            else if (eyeVariant == EyeAnimationVariant.NeutralLookDown)
                scaleZ = Mathf.Min(scaleZ, 0.93f);
            else if (eyeVariant == EyeAnimationVariant.ShockWide)
                scaleZ = Mathf.Max(scaleZ, 1.12f + Mathf.Abs(primaryWave) * 0.03f);
            else if (eyeVariant == EyeAnimationVariant.ScaredWide)
                scaleZ = Mathf.Max(scaleZ, 1.08f + Mathf.Abs(slowWave) * 0.02f);
            else if (eyeVariant == EyeAnimationVariant.ScaredFlinch)
                scaleZ = Mathf.Min(scaleZ, 0.94f);

            SetProgrammaticFaceTransform(
                customNode,
                primaryWave * 0.15f,
                new Vector3(offsetX, 0f, offsetZ + slowWave * 0.0004f),
                new Vector3(1.01f + Mathf.Abs(slowWave) * 0.01f, 1f, scaleZ));
        }

        private void ApplyProgrammaticPupilTransform(
            PawnRenderNode_Custom customNode,
            LidState lidState,
            EyeDirection eyeDirection,
            ExpressionType expression,
            float primaryWave,
            float slowWave)
        {
            if (lidState == LidState.Blink || lidState == LidState.Close)
            {
                HideProgrammaticFacePart(customNode);
                return;
            }

            Pawn? pawn = customNode.tree?.pawn;
            CompPawnSkin? skinComp = pawn?.TryGetComp<CompPawnSkin>();
            EyeAnimationVariant eyeVariant = skinComp?.GetEffectiveEyeAnimationVariant() ?? EyeAnimationVariant.NeutralOpen;
            PupilScaleVariant pupilVariant = skinComp?.GetEffectivePupilScaleVariant() ?? PupilScaleVariant.Neutral;

            float sideSign = GetLayeredFaceSideSign(customNode);
            float offsetX = GetLayeredFaceSideBias(customNode, 0.00028f);
            float offsetZ = slowWave * 0.0005f;
            switch (eyeDirection)
            {
                case EyeDirection.Left:
                    offsetX = -0.0018f;
                    break;
                case EyeDirection.Right:
                    offsetX = 0.0018f;
                    break;
                case EyeDirection.Up:
                    offsetZ -= 0.0014f;
                    break;
                case EyeDirection.Down:
                    offsetZ += 0.0016f;
                    break;
            }

            switch (eyeVariant)
            {
                case EyeAnimationVariant.NeutralSoft:
                    offsetZ += 0.0004f;
                    break;
                case EyeAnimationVariant.NeutralLookDown:
                    offsetZ += 0.0012f;
                    break;
                case EyeAnimationVariant.NeutralGlance:
                    offsetX += (primaryWave > 0f ? 0.0010f : -0.0010f) + sideSign * 0.00045f;
                    break;
                case EyeAnimationVariant.WorkFocusDown:
                    offsetZ += 0.0020f;
                    break;
                case EyeAnimationVariant.WorkFocusUp:
                    offsetZ -= 0.0015f;
                    break;
                case EyeAnimationVariant.HappyOpen:
                    offsetZ -= 0.0003f;
                    break;
                case EyeAnimationVariant.ShockWide:
                    offsetZ -= 0.0012f;
                    break;
                case EyeAnimationVariant.ScaredWide:
                    offsetZ -= 0.0008f;
                    offsetX += primaryWave * 0.0008f + sideSign * 0.0004f;
                    break;
                case EyeAnimationVariant.ScaredFlinch:
                    offsetZ += 0.0008f;
                    offsetX += slowWave * 0.0009f + sideSign * 0.00055f;
                    break;
                case EyeAnimationVariant.HappyClosedPeak:
                case EyeAnimationVariant.BlinkClosed:
                    HideProgrammaticFacePart(customNode);
                    return;
            }

            float scale = pupilVariant switch
            {
                PupilScaleVariant.Focus => 0.94f + Mathf.Abs(slowWave) * 0.01f,
                PupilScaleVariant.SlightlyContracted => 0.88f + Mathf.Abs(slowWave) * 0.01f,
                PupilScaleVariant.Contracted => 0.78f + Mathf.Abs(slowWave) * 0.015f,
                PupilScaleVariant.Dilated => 1.12f + Mathf.Abs(primaryWave) * 0.02f,
                PupilScaleVariant.DilatedMax => 1.22f + Mathf.Abs(primaryWave) * 0.03f,
                PupilScaleVariant.ScaredPulse => 1.16f + Mathf.Abs(primaryWave) * 0.05f,
                PupilScaleVariant.BlinkHidden => 0f,
                _ => 1f,
            };

            if (expression == ExpressionType.Shock || expression == ExpressionType.Scared)
                scale = Mathf.Max(scale, 1.08f + Mathf.Abs(primaryWave) * 0.03f);
            else if (expression == ExpressionType.Happy || expression == ExpressionType.Cheerful)
                scale = Mathf.Min(scale, 0.96f + Mathf.Abs(slowWave) * 0.01f);
            else if (expression == ExpressionType.Sleeping)
                scale = 0.9f;
            else if (eyeVariant == EyeAnimationVariant.WorkFocusDown)
                scale = Mathf.Min(scale, 0.98f);

            if (eyeVariant == EyeAnimationVariant.NeutralSoft)
                scale = Mathf.Min(scale, 0.96f);
            else if (eyeVariant == EyeAnimationVariant.NeutralLookDown)
                scale = Mathf.Min(scale, 0.94f);
            else if (eyeVariant == EyeAnimationVariant.ShockWide)
                scale = Mathf.Max(scale, 1.18f + Mathf.Abs(primaryWave) * 0.02f);
            else if (eyeVariant == EyeAnimationVariant.ScaredWide)
                scale = Mathf.Max(scale, 1.14f + Mathf.Abs(slowWave) * 0.03f);
            else if (eyeVariant == EyeAnimationVariant.ScaredFlinch)
                scale = Mathf.Max(scale, 1.04f + Mathf.Abs(primaryWave) * 0.01f);

            if (pupilVariant == PupilScaleVariant.BlinkHidden)
            {
                HideProgrammaticFacePart(customNode);
                return;
            }

            SetProgrammaticFaceTransform(
                customNode,
                primaryWave * 0.35f,
                new Vector3(offsetX + primaryWave * 0.0004f, 0f, offsetZ),
                new Vector3(scale, 1f, scale));
        }

        private void ApplyProgrammaticLidTransform(
            PawnRenderNode_Custom customNode,
            LayeredFacePartType partType,
            LidState state,
            float primaryWave,
            float slowWave)
        {
            if (partType == LayeredFacePartType.UpperLid)
            {
                float replacementMoveDown = GetLayeredUpperLidMoveDown(customNode);
                float sideBiasX = GetLayeredFaceSideBias(customNode, 0.00035f);
                switch (state)
                {
                    case LidState.Blink:
                    {
                        Pawn? pawn = customNode.tree?.pawn;
                        CompPawnSkin? blinkSkinComp = pawn?.TryGetComp<CompPawnSkin>();
                        BlinkPhase blinkPhase = blinkSkinComp?.GetBlinkPhase() ?? BlinkPhase.None;
                        float phaseProgress = blinkSkinComp?.GetBlinkProgress01() ?? 0f;
                        float animatedMoveDown = blinkPhase switch
                        {
                            BlinkPhase.ClosingLid => Mathf.Lerp(0f, replacementMoveDown, Mathf.Clamp01(phaseProgress / 0.5f)),
                            BlinkPhase.HideBaseEyeParts => replacementMoveDown,
                            BlinkPhase.ShowReplacementEye => replacementMoveDown,
                            BlinkPhase.RestoreBaseEyeParts => replacementMoveDown,
                            BlinkPhase.OpeningLid => Mathf.Lerp(replacementMoveDown, 0f, Mathf.Clamp01((phaseProgress - 0.6f) / 0.4f)),
                            _ => 0f,
                        };

                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, animatedMoveDown),
                            new Vector3(1.01f, 1f, 0.88f));
                        break;
                    }

                    case LidState.Close:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, replacementMoveDown),
                            new Vector3(1.01f, 1f, 0.90f));
                        break;

                    case LidState.Half:
                    {
                        Pawn? pawn = customNode.tree?.pawn;
                        CompPawnSkin? skinComp = pawn?.TryGetComp<CompPawnSkin>();
                        EyeAnimationVariant eyeVariant = skinComp?.GetEffectiveEyeAnimationVariant() ?? EyeAnimationVariant.NeutralSoft;

                        float halfOffset = Mathf.Max(0f, replacementMoveDown - 0.0016f);
                        float halfScale = 0.95f;
                        if (eyeVariant == EyeAnimationVariant.NeutralSoft)
                        {
                            halfOffset += 0.0003f;
                            halfScale = 0.93f;
                        }
                        else if (eyeVariant == EyeAnimationVariant.NeutralLookDown)
                        {
                            halfOffset += 0.0008f;
                            halfScale = 0.91f;
                        }
                        else if (eyeVariant == EyeAnimationVariant.ScaredFlinch)
                        {
                            halfOffset += 0.0010f;
                            halfScale = 0.89f;
                        }

                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, halfOffset + slowWave * 0.0004f),
                            new Vector3(1.01f, 1f, halfScale));
                        break;
                    }

                    case LidState.Happy:
                    {
                        Pawn? pawn = customNode.tree?.pawn;
                        CompPawnSkin? skinComp = pawn?.TryGetComp<CompPawnSkin>();
                        EyeAnimationVariant eyeVariant = skinComp?.GetEffectiveEyeAnimationVariant() ?? EyeAnimationVariant.HappyOpen;
                        if (eyeVariant == EyeAnimationVariant.HappyClosedPeak)
                        {
                            HideProgrammaticFacePart(customNode);
                            return;
                        }

                        float happyOffset = eyeVariant == EyeAnimationVariant.HappySoft ? -0.0014f : -0.0008f;
                        float happyScale = eyeVariant == EyeAnimationVariant.HappySoft ? 0.90f : 0.95f;
                        SetProgrammaticFaceTransform(
                            customNode,
                            -1.2f + primaryWave * 0.2f,
                            new Vector3(sideBiasX, 0f, happyOffset + slowWave * 0.0004f),
                            new Vector3(1.02f, 1f, happyScale));
                        break;
                    }

                    default:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, slowWave * 0.0003f),
                            Vector3.one);
                        break;
                }

                return;
            }

            if (partType == LayeredFacePartType.LowerLid)
            {
                float sideBiasX = GetLayeredFaceSideBias(customNode, 0.0002f);
                switch (state)
                {
                    case LidState.Blink:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, -0.0024f),
                            new Vector3(1.00f, 1f, 0.96f));
                        break;

                    case LidState.Close:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, -0.0018f),
                            new Vector3(1.00f, 1f, 0.97f));
                        break;

                    case LidState.Half:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, -0.0012f + slowWave * 0.0003f),
                            new Vector3(1.00f, 1f, 0.985f));
                        break;

                    case LidState.Happy:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0.85f + primaryWave * 0.15f,
                            new Vector3(sideBiasX, 0f, -0.0008f + slowWave * 0.0003f),
                            new Vector3(1.01f, 1f, 0.98f));
                        break;

                    default:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, -slowWave * 0.0002f),
                            Vector3.one);
                        break;
                }

                return;
            }

            switch (state)
            {
                case LidState.Blink:
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, 0.0045f),
                        new Vector3(1.02f, 1f, 0.72f));
                    break;

                case LidState.Close:
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, 0.0035f),
                        new Vector3(1.01f, 1f, 0.78f));
                    break;

                case LidState.Half:
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, 0.0022f + slowWave * 0.0005f),
                        new Vector3(1.01f, 1f, 0.89f));
                    break;

                case LidState.Happy:
                    SetProgrammaticFaceTransform(
                        customNode,
                        -1.1f + primaryWave * 0.25f,
                        new Vector3(0f, 0f, -0.001f + slowWave * 0.0005f),
                        new Vector3(1.03f, 1f, 0.91f));
                    break;

                default:
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, slowWave * 0.0004f),
                        new Vector3(1f, 1f, 0.99f + Mathf.Abs(primaryWave) * 0.01f));
                    break;
            }
        }

        private void ApplyProgrammaticMouthTransform(
            PawnRenderNode_Custom customNode,
            MouthState state,
            ExpressionType expression,
            float primaryWave,
            float slowWave)
        {
            switch (state)
            {
                case MouthState.Smile:
                    SetProgrammaticFaceTransform(
                        customNode,
                        slowWave * 0.6f,
                        new Vector3(0f, 0f, -0.001f + primaryWave * 0.0006f),
                        new Vector3(1.06f + Mathf.Abs(primaryWave) * 0.02f, 1f, 0.94f));
                    break;

                case MouthState.Open:
                    SetProgrammaticFaceTransform(
                        customNode,
                        primaryWave * 0.8f,
                        new Vector3(0f, 0f, 0.004f + Mathf.Abs(primaryWave) * 0.0015f),
                        new Vector3(1.03f, 1f, 1.14f + Mathf.Abs(slowWave) * 0.04f));
                    break;

                case MouthState.Down:
                    SetProgrammaticFaceTransform(
                        customNode,
                        -0.75f + primaryWave * 0.3f,
                        new Vector3(0f, 0f, 0.0025f + slowWave * 0.0006f),
                        new Vector3(0.99f, 1f, 0.90f));
                    break;

                case MouthState.Sleep:
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, 0.002f),
                        new Vector3(0.97f, 1f, 0.84f));
                    break;

                default:
                    if (expression == ExpressionType.Eating)
                    {
                        SetProgrammaticFaceTransform(
                            customNode,
                            primaryWave * 1.25f,
                            new Vector3(0f, 0f, 0.002f + Mathf.Abs(primaryWave) * 0.001f),
                            new Vector3(1.01f, 1f, 1.05f + Mathf.Abs(primaryWave) * 0.04f));
                    }
                    else if (expression == ExpressionType.Shock || expression == ExpressionType.Scared)
                    {
                        SetProgrammaticFaceTransform(
                            customNode,
                            primaryWave * 0.75f,
                            new Vector3(0f, 0f, 0.0032f + Mathf.Abs(primaryWave) * 0.001f),
                            new Vector3(1.02f, 1f, 1.10f + Mathf.Abs(slowWave) * 0.03f));
                    }
                    else
                    {
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(0f, 0f, slowWave * 0.0005f),
                            Vector3.one);
                    }
                    break;
            }
        }

        private float GetLayeredUpperLidMoveDown(PawnRenderNode_Custom customNode)
        {
            Pawn? pawn = customNode.tree?.pawn;
            CompPawnSkin? skinComp = pawn?.TryGetComp<CompPawnSkin>();

            // 预览面板中 LidState 可单独覆盖，但并不会强制启用 eyeDirectionConfig。
            // 之前这里优先读取编译态 portraitTrack.eyeDirection.upperLidMoveDown，
            // 而编译态仅在 eyeDirectionConfig.enabled == true 时才写入；
            // 因此当用户只切换 LidState（Blink / Half / Close）而未启用眼方向模块时，
            // 会命中默认 0.0044f 或旧编译缓存，导致预览里看起来“没有任何变化”。
            // 对程序化眼睑位移来说，源配置值本身更可靠，应优先直读 active skin。
            float configuredMoveDown = Mathf.Max(0f, skinComp?.ActiveSkin?.faceConfig?.eyeDirectionConfig?.upperLidMoveDown ?? 0.0044f);

            FaceEyeDirectionRuntimeData? eyeData = skinComp?.CurrentFaceRuntimeCompiledData?.portraitTrack?.eyeDirection;
            if (eyeData?.enabled == true)
                return Mathf.Max(configuredMoveDown, eyeData.upperLidMoveDown);

            return configuredMoveDown;
        }

        private bool ShouldHideLayeredEyePartForReplacement(PawnRenderNode_Custom customNode, Pawn pawn, CompPawnSkin skinComp)
        {
            LayeredFacePartType? partType = customNode.layeredFacePartType;
            if (!partType.HasValue)
                return false;

            switch (partType.Value)
            {
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.Pupil:
                case LayeredFacePartType.LowerLid:
                    break;
                default:
                    return false;
            }

            if (!HasActiveLayeredEyeReplacement(customNode, pawn, skinComp))
                return false;

            if (skinComp.IsBlinkActive())
            {
                BlinkPhase blinkPhase = skinComp.GetBlinkPhase();
                return blinkPhase == BlinkPhase.HideBaseEyeParts
                    || blinkPhase == BlinkPhase.ShowReplacementEye
                    || blinkPhase == BlinkPhase.RestoreBaseEyeParts;
            }

            ExpressionType expression = skinComp.GetEffectiveExpression();
            return expression == ExpressionType.Dead;
        }

        private bool ShouldHideUpperLidAtBlinkEndpoint(PawnRenderNode_Custom customNode, CompPawnSkin skinComp)
        {
            if (customNode.layeredFacePartType != LayeredFacePartType.UpperLid)
                return false;

            if (!skinComp.IsBlinkActive())
                return false;

            BlinkPhase blinkPhase = skinComp.GetBlinkPhase();
            return blinkPhase == BlinkPhase.ShowReplacementEye;
        }

        private bool HasActiveLayeredEyeReplacement(PawnRenderNode_Custom customNode, Pawn pawn, CompPawnSkin skinComp)
        {
            if (skinComp.GetEffectiveExpression() == ExpressionType.Neutral && !skinComp.IsBlinkActive())
                return false;

            PawnFaceConfig? faceConfig = skinComp.ActiveSkin?.faceConfig;
            if (faceConfig == null || faceConfig.workflowMode != FaceWorkflowMode.LayeredDynamic)
                return false;

            string overlayId = GetEffectiveLayeredOverlayId(customNode, pawn);
            LayeredFacePartType replacementPartType = PawnFaceConfig.GetOverlayDisplayPartType(overlayId);
            if (replacementPartType != LayeredFacePartType.Eye)
                return false;

            return faceConfig.CountLayeredParts(LayeredFacePartType.Overlay, overlayId) > 0;
        }

        private LayeredFacePartType GetProgrammaticEmotionPartType(PawnRenderNode_Custom customNode)
        {
            LayeredFacePartType? configuredType = customNode.layeredFacePartType;
            if (!configuredType.HasValue)
                return LayeredFacePartType.Overlay;

            if (configuredType.Value != LayeredFacePartType.Overlay)
                return configuredType.Value;

            return PawnFaceConfig.GetOverlayDisplayPartType(customNode.layeredOverlayId);
        }

        private string GetEffectiveLayeredOverlayId(PawnRenderNode_Custom customNode, Pawn? pawn = null)
        {
            string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(customNode.layeredOverlayId);
            if (!string.Equals(normalizedOverlayId, "Overlay", StringComparison.OrdinalIgnoreCase))
                return normalizedOverlayId;

            if (pawn == null)
                return string.Empty;

            CompPawnSkin? skinComp = pawn.TryGetComp<CompPawnSkin>();
            PawnFaceConfig? faceConfig = skinComp?.ActiveSkin?.faceConfig;
            if (skinComp == null || faceConfig == null)
                return string.Empty;

            string semanticOverlayId = ResolveSemanticOverlayId(skinComp.GetEffectiveEmotionOverlayState(), skinComp.GetEffectiveExpression());
            if (string.IsNullOrWhiteSpace(semanticOverlayId))
                return string.Empty;

            return faceConfig.CountLayeredParts(LayeredFacePartType.Overlay, semanticOverlayId) > 0
                ? semanticOverlayId
                : string.Empty;
        }

        private string ResolveSemanticOverlayId(EmotionOverlayState emotionState, ExpressionType expression)
        {
            if (expression == ExpressionType.Sleeping)
                return "Sleep";

            switch (emotionState)
            {
                case EmotionOverlayState.Blush:
                case EmotionOverlayState.Lovin:
                    return "Blush";
                case EmotionOverlayState.Tear:
                    return "Tear";
                case EmotionOverlayState.Gloomy:
                    return "Gloomy";
                case EmotionOverlayState.Sweat:
                    return "Sweat";
                default:
                    return string.Empty;
            }
        }

        private void ApplyProgrammaticEmotionTransform(
            PawnRenderNode_Custom customNode,
            LayeredFacePartType partType,
            EmotionOverlayState emotionState,
            ExpressionType expression,
            float primaryWave,
            float slowWave)
        {
            switch (partType)
            {
                case LayeredFacePartType.Eye:
                {
                    Pawn? pawn = customNode.tree?.pawn;
                    CompPawnSkin? skinComp = pawn?.TryGetComp<CompPawnSkin>();
                    if (pawn == null || skinComp == null || !HasActiveLayeredEyeReplacement(customNode, pawn, skinComp))
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    if (skinComp.IsBlinkActive())
                    {
                        BlinkPhase blinkPhase = skinComp.GetBlinkPhase();
                        bool visible = blinkPhase == BlinkPhase.ShowReplacementEye;
                        if (!visible)
                        {
                            HideProgrammaticFacePart(customNode);
                            return;
                        }
                    }

                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        Vector3.zero,
                        Vector3.one);
                    return;
                }

                case LayeredFacePartType.Blush:
                {
                    bool active = emotionState == EmotionOverlayState.Lovin
                        || emotionState == EmotionOverlayState.Blush;

                    if (!active)
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    float pulse = 1.04f + Mathf.Abs(primaryWave) * 0.05f;
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, -0.001f + slowWave * 0.001f),
                        new Vector3(pulse, 1f, 1.02f + Mathf.Abs(slowWave) * 0.02f));
                    return;
                }

                case LayeredFacePartType.Tear:
                {
                    bool active = emotionState == EmotionOverlayState.Tear
                        || emotionState == EmotionOverlayState.Gloomy;

                    if (!active)
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    float pulse = 1.01f + Mathf.Abs(slowWave) * 0.02f;
                    SetProgrammaticFaceTransform(
                        customNode,
                        primaryWave * 0.5f,
                        new Vector3(0f, 0f, 0.002f + Mathf.Abs(primaryWave) * 0.0015f),
                        new Vector3(pulse, 1f, pulse));
                    return;
                }

                case LayeredFacePartType.Sweat:
                {
                    bool active = emotionState == EmotionOverlayState.Sweat;

                    if (!active)
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    float pulse = 1f + Mathf.Abs(primaryWave) * 0.03f;
                    SetProgrammaticFaceTransform(
                        customNode,
                        primaryWave * 2.5f,
                        new Vector3(primaryWave * 0.0025f, 0f, 0.0015f + Mathf.Abs(slowWave) * 0.001f),
                        new Vector3(pulse, 1f, pulse));
                    return;
                }

                case LayeredFacePartType.Overlay:
                {
                    Pawn? pawn = customNode.tree?.pawn;
                    string overlayId = GetEffectiveLayeredOverlayId(customNode, pawn);
                    if (string.IsNullOrWhiteSpace(overlayId))
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    bool isSleepOverlay = overlayId.Equals("Sleep", StringComparison.OrdinalIgnoreCase);
                    bool isPositiveOverlay = overlayId.Equals("Blush", StringComparison.OrdinalIgnoreCase);
                    bool active = isSleepOverlay
                        ? expression == ExpressionType.Sleeping
                        : (overlayId.Equals("Tear", StringComparison.OrdinalIgnoreCase) && emotionState == EmotionOverlayState.Tear)
                            || (overlayId.Equals("Gloomy", StringComparison.OrdinalIgnoreCase) && emotionState == EmotionOverlayState.Gloomy)
                            || (overlayId.Equals("Sweat", StringComparison.OrdinalIgnoreCase) && emotionState == EmotionOverlayState.Sweat)
                            || (overlayId.Equals("Blush", StringComparison.OrdinalIgnoreCase)
                                && (emotionState == EmotionOverlayState.Blush || emotionState == EmotionOverlayState.Lovin));

                    if (!active)
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    if (isSleepOverlay)
                    {
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(0f, 0f, 0.0015f + slowWave * 0.0004f),
                            new Vector3(1.01f, 1f, 0.97f));
                        return;
                    }

                    if (isPositiveOverlay)
                    {
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(0f, 0f, -0.001f + slowWave * 0.001f),
                            new Vector3(1.03f + Mathf.Abs(primaryWave) * 0.03f, 1f, 1.01f));
                    }
                    else
                    {
                        SetProgrammaticFaceTransform(
                            customNode,
                            primaryWave * 0.35f,
                            new Vector3(0f, 0f, 0.002f + Mathf.Abs(slowWave) * 0.001f),
                            new Vector3(1.01f, 1f, 0.98f));
                    }
                    return;
                }
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

                EnsureProgrammaticFaceStateUpdated(customNode, pawn);
                float programmaticAlpha = customNode.layeredFacePartType.HasValue
                    ? Mathf.Clamp01(customNode.currentProgrammaticAlpha)
                    : 1f;

                // 1. 获取主颜色
                Color colorOne = GetColorFromSource(config.colorSource, pawn, config.customColor);
                
                // 2. 获取副颜色 (Mask)
                Color colorTwo = GetColorFromSource(config.colorTwoSource, pawn, config.customColorTwo);

                if (parms.Statue)
                {
                    Color statueColor = parms.statueColor ?? Color.white;
                    statueColor.a *= programmaticAlpha;
                    matPropBlock.SetColor(ShaderPropertyIDs.Color, statueColor);
                }
                else
                {
                    Color finalColor = parms.tint * colorOne;
                    finalColor.a *= programmaticAlpha;

                    Color finalColorTwo = colorTwo;
                    finalColorTwo.a *= programmaticAlpha;

                    // 应用主颜色与 tint 的组合
                    matPropBlock.SetColor(ShaderPropertyIDs.Color, finalColor);
                    
                    // 应用第二颜色（仅当使用支持 Mask 的 Shader 时有效）
                    // 始终设置 _ColorTwo，因为如果 Shader 不支持，设置属性也不会有负面影响
                    int colorTwoID = Shader.PropertyToID("_ColorTwo");
                    matPropBlock.SetColor(colorTwoID, finalColorTwo);
                }

                int stID = Shader.PropertyToID("_MainTex_ST");
                matPropBlock.SetVector(stID, new Vector4(1f, 1f, 0f, 0f));

                if (config.eyeRenderMode == EyeRenderMode.UvOffset)
                {
                    CompPawnSkin? skinComp = pawn?.TryGetComp<CompPawnSkin>();
                    float range = config.eyeUvMoveRange > 0f
                        ? config.eyeUvMoveRange
                        : skinComp?.ActiveSkin?.faceConfig?.eyeDirectionConfig?.pupilMoveRange ?? 0f;

                    if (range > 0f)
                    {
                        EyeDirection eyeDirection = skinComp?.CurEyeDirection ?? EyeDirection.Center;
                        float offsetX = 0f;
                        float offsetY = 0f;

                        switch (eyeDirection)
                        {
                            case EyeDirection.Left:
                                offsetX = +range;
                                break;
                            case EyeDirection.Right:
                                offsetX = -range;
                                break;
                            case EyeDirection.Up:
                                offsetY = +range;
                                break;
                            case EyeDirection.Down:
                                offsetY = -range;
                                break;
                        }

                        matPropBlock.SetVector(stID, new Vector4(1f, 1f, offsetX, offsetY));
                    }
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
            bool hasCustomConfig = node is PawnRenderNode_Custom customNodeWithConfig && customNodeWithConfig.config != null;
            if (string.IsNullOrEmpty(node.Props?.texPath) && !hasCustomConfig)
                return null;

            string texPath = node.Props?.texPath ?? "";
            string resolvedTexPath = texPath;
            bool matchedVariant = false;
            bool attemptedVariant = false;

            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                PawnLayerConfig config = customNode.config;
                string? layeredBasePath = ResolveLayeredFacePartBasePath(customNode, parms.pawn, parms.facing);
                string? configuredPath = ResolveConfiguredTexPath(
                    config,
                    parms.pawn,
                    parms.facing,
                    out matchedVariant,
                    out attemptedVariant,
                    layeredBasePath);

                if (string.IsNullOrEmpty(configuredPath))
                    return null;

                resolvedTexPath = configuredPath!;

                if (!UsesUnifiedVariantLogic(config))
                {
                    resolvedTexPath = ResolveExpressionVariant(resolvedTexPath, parms.pawn);
                }

                if (attemptedVariant && !matchedVariant && config.hideWhenMissingVariant)
                    return null;
            }
            else
            {
                string directionalTexPath = ResolveDirectionalVariant(texPath, parms.facing);
                resolvedTexPath = ResolveExpressionVariant(directionalTexPath, parms.pawn);
            }

            bool looksLikeExternal = resolvedTexPath.Contains(":")
                || resolvedTexPath.StartsWith("/")
                || System.IO.Path.IsPathRooted(resolvedTexPath);

            if (looksLikeExternal)
            {
                bool externalExists = RuntimeAssetLoader.ExternalTextureExists(resolvedTexPath, out string resolvedExternalPath);
                if (!externalExists)
                {
                    Log.Error($"[CharacterStudio] 外部纹理文件不存在: {resolvedTexPath}");
                }

                Shader shader = node.ShaderFor(parms.pawn);
                if (shader == null)
                {
                    shader = ShaderDatabase.Cutout;
                }

                var props = node.Props;

                var req = new GraphicRequest(
                    typeof(Graphic_Runtime),
                    externalExists ? resolvedExternalPath : resolvedTexPath,
                    shader,
                    Vector2.one,
                    props?.color ?? Color.white,
                    Color.white, null, 0, null, null
                );

                var graphic = new Graphic_Runtime();
                graphic.Init(req);
                return graphic;
            }

            if (node is PawnRenderNode_Custom customNode2)
            {
                var config = customNode2.config;
                if (config == null)
                {
                    return base.GetGraphic(node, parms);
                }

                Type? graphicType = config.graphicClass;

                if (graphicType != typeof(Graphic_Runtime))
                {
                    if (graphicType == null || (graphicType != typeof(Graphic_Multi) && graphicType != typeof(Graphic_Single)))
                    {
                        if (!RuntimeAssetLoader.IsMainThread())
                        {
                            graphicType = typeof(Graphic_Multi);
                        }
                        else if (ContentFinder<Texture2D>.Get(resolvedTexPath + "_north", false) != null)
                        {
                            graphicType = typeof(Graphic_Multi);
                        }
                        else if (ContentFinder<Texture2D>.Get(resolvedTexPath, false) != null)
                        {
                            graphicType = typeof(Graphic_Single);
                        }

                        if (graphicType == null) graphicType = typeof(Graphic_Multi);
                    }

                    Shader? shader = null;
                    if (!string.IsNullOrEmpty(config.shaderDefName))
                    {
                        switch (config.shaderDefName)
                        {
                            case "Cutout": shader = ShaderDatabase.Cutout; break;
                            case "CutoutComplex": shader = ShaderDatabase.CutoutComplex; break;
                            case "Transparent": shader = ShaderDatabase.Transparent; break;
                            case "TransparentPostLight": shader = ShaderDatabase.TransparentPostLight; break;
                            case "MetaOverlay": shader = ShaderDatabase.MetaOverlay; break;
                        }
                    }
                    if (shader == null) shader = node.ShaderFor(parms.pawn) ?? ShaderDatabase.Cutout;

                    Vector2 drawSize = Vector2.one;
                    Color color = GetColorFromSource(config.colorSource, parms.pawn, config.customColor);
                    Color colorTwo = GetColorFromSource(config.colorTwoSource, parms.pawn, config.customColorTwo);

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
                        var method = typeof(GraphicDatabase).GetMethod("Get", new Type[] { typeof(string), typeof(Shader), typeof(Vector2), typeof(Color), typeof(Color) });
                        if (method != null)
                        {
                            var genericMethod = method.MakeGenericMethod(graphicType!);
                            return (Graphic)genericMethod.Invoke(null, new object[] {
                                resolvedTexPath,
                                shader,
                                drawSize,
                                color,
                                colorTwo
                            });
                        }

                        method = typeof(GraphicDatabase).GetMethod("Get", new Type[] { typeof(string), typeof(Shader), typeof(Vector2), typeof(Color) });
                        if (method != null)
                        {
                            var genericMethod = method.MakeGenericMethod(graphicType!);
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

            if (resolvedTexPath != texPath)
            {
                Shader shader = node.ShaderFor(parms.pawn);
                if (shader == null)
                {
                    shader = ShaderDatabase.Cutout;
                }
                var props = node.Props;
                return GraphicDatabase.Get<Graphic_Single>(
                    resolvedTexPath,
                    shader,
                    Vector2.one,
                    props?.color ?? Color.white
                );
            }

            return base.GetGraphic(node, parms);
        }

        private bool IsExpressionVisibleForLayer(PawnLayerConfig config, Pawn? pawn)
        {
            if (pawn == null)
                return true;

            string expressionName = GetEffectiveExpressionName(pawn);
            if (config.hiddenExpressions != null && config.hiddenExpressions.Any(x => string.Equals(x, expressionName, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (config.visibleExpressions != null && config.visibleExpressions.Length > 0)
                return config.visibleExpressions.Any(x => string.Equals(x, expressionName, StringComparison.OrdinalIgnoreCase));

            return true;
        }

        private bool UsesUnifiedVariantLogic(PawnLayerConfig config)
        {
            return config.variantLogic != LayerVariantLogic.None
                || !string.IsNullOrEmpty(config.variantBaseName)
                || config.useExpressionSuffix
                || config.useEyeDirectionSuffix
                || config.useBlinkSuffix
                || config.useFrameSequence
                || config.hideWhenMissingVariant
                || config.role != LayerRole.Decoration;
        }

        private string? ResolveConfiguredTexPath(PawnLayerConfig config, Pawn? pawn, Rot4 facing, out bool matchedVariant, out bool attemptedVariant, string? basePathOverride = null)
        {
            matchedVariant = false;
            attemptedVariant = false;

            string basePath = !string.IsNullOrEmpty(basePathOverride)
                ? basePathOverride!
                : (!string.IsNullOrEmpty(config.variantBaseName) ? config.variantBaseName : config.texPath);

            if (string.IsNullOrEmpty(basePath))
                return null;

            if (pawn == null)
                return basePath;

            List<string> logicalSuffixes = GetLogicalSuffixCandidates(config, pawn, out bool suppressWhenNoLogicalSuffix);
            if (logicalSuffixes.Count == 0 && suppressWhenNoLogicalSuffix)
                return null;

            string? directionalToken = config.useDirectionalSuffix ? GetDirectionalToken(facing) : null;
            List<string> prefixes = BuildVariantPrefixes(basePath, logicalSuffixes, directionalToken);
            attemptedVariant = prefixes.Any(prefix => !string.Equals(prefix, basePath, StringComparison.Ordinal));

            foreach (string prefix in prefixes)
            {
                bool isBasePrefix = string.Equals(prefix, basePath, StringComparison.Ordinal);

                if (config.useFrameSequence && TryResolveFrameSequencePath(prefix, pawn, out string framePath))
                {
                    matchedVariant = !string.Equals(framePath, basePath, StringComparison.Ordinal);
                    return framePath;
                }

                if (TextureExists(prefix))
                {
                    matchedVariant = !isBasePrefix;
                    return prefix;
                }
            }

            return basePath;
        }

        private List<string> GetLogicalSuffixCandidates(PawnLayerConfig config, Pawn? pawn, out bool suppressWhenNoLogicalSuffix)
        {
            var results = new List<string>();
            suppressWhenNoLogicalSuffix = false;

            if (pawn == null)
                return results;

            CompPawnSkin? skinComp = pawn.TryGetComp<CompPawnSkin>();
            ExpressionType? expression = skinComp != null ? skinComp.GetEffectiveExpression() : TryGetFallbackExpression(pawn);

            switch (config.variantLogic)
            {
                case LayerVariantLogic.ExpressionOnly:
                case LayerVariantLogic.ExpressionAndDirection:
                    AddExpressionSuffixCandidates(results, expression);
                    break;

                case LayerVariantLogic.EyeDirectionOnly:
                    AddUnique(results, (skinComp?.CurEyeDirection ?? EyeDirection.Center).ToString());
                    break;

                case LayerVariantLogic.BlinkOnly:
                    suppressWhenNoLogicalSuffix = true;
                    if (skinComp?.IsBlinkActive() == true)
                        AddUnique(results, "Blink");
                    break;

                case LayerVariantLogic.ChannelState:
                    string? channelState = skinComp?.GetChannelStateSuffix(config.role);
                    if (!string.IsNullOrEmpty(channelState))
                        AddUnique(results, channelState);

                    if (config.role == LayerRole.Emotion)
                        suppressWhenNoLogicalSuffix = true;
                    break;

                case LayerVariantLogic.Sequence:
                    string? sequenceState = skinComp?.GetChannelStateSuffix(config.role);
                    if (!string.IsNullOrEmpty(sequenceState))
                        AddUnique(results, sequenceState);
                    else
                        AddExpressionSuffixCandidates(results, expression);
                    break;
            }

            if (config.useBlinkSuffix && config.variantLogic != LayerVariantLogic.BlinkOnly && skinComp?.IsBlinkActive() == true)
                AddUnique(results, "Blink");

            if (config.useEyeDirectionSuffix && config.variantLogic != LayerVariantLogic.EyeDirectionOnly)
                AddUnique(results, (skinComp?.CurEyeDirection ?? EyeDirection.Center).ToString());

            if (config.useExpressionSuffix
                && config.variantLogic != LayerVariantLogic.ExpressionOnly
                && config.variantLogic != LayerVariantLogic.ExpressionAndDirection)
            {
                AddExpressionSuffixCandidates(results, expression);
            }

            return results;
        }

        private void AddExpressionSuffixCandidates(List<string> results, ExpressionType? expression)
        {
            if (!expression.HasValue)
                return;

            AddUnique(results, expression.Value.ToString());

            switch (expression.Value)
            {
                case ExpressionType.Sleeping:
                case ExpressionType.Dead:
                    AddUnique(results, "Sleep");
                    break;

                case ExpressionType.Angry:
                case ExpressionType.AttackMelee:
                case ExpressionType.AttackRanged:
                case ExpressionType.WaitCombat:
                case ExpressionType.Scared:
                    AddUnique(results, "Angry");
                    break;

                case ExpressionType.Blink:
                    AddUnique(results, "Blink");
                    break;
            }
        }

        private static void AddUnique(List<string> values, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            string nonNullValue = value!;
            if (!values.Contains(nonNullValue))
                values.Add(nonNullValue);
        }

        private ExpressionType? TryGetFallbackExpression(Pawn? pawn)
        {
            if (pawn == null)
                return null;

            if (pawn.Dead)
                return ExpressionType.Dead;

            if (RestUtility.InBed(pawn))
                return ExpressionType.Sleeping;

            if (pawn.Drafted || (pawn.MentalState != null && pawn.MentalState.def.IsAggro))
                return ExpressionType.Angry;

            return null;
        }

        private string GetEffectiveExpressionName(Pawn pawn)
        {
            CompPawnSkin? skinComp = pawn.TryGetComp<CompPawnSkin>();
            if (skinComp != null)
                return skinComp.GetEffectiveExpression().ToString();

            return (TryGetFallbackExpression(pawn) ?? ExpressionType.Neutral).ToString();
        }

        private List<string> BuildVariantPrefixes(string basePath, List<string> logicalSuffixes, string? directionalToken)
        {
            var prefixes = new List<string>();

            if (logicalSuffixes.Count > 0)
            {
                foreach (string suffix in logicalSuffixes)
                {
                    if (!string.IsNullOrEmpty(directionalToken))
                    {
                        AddUnique(prefixes, AppendVariantToken(AppendVariantToken(basePath, suffix), directionalToken));
                        AddUnique(prefixes, AppendVariantToken(AppendVariantToken(basePath, directionalToken), suffix));
                    }

                    AddUnique(prefixes, AppendVariantToken(basePath, suffix));
                }
            }

            if (!string.IsNullOrEmpty(directionalToken))
                AddUnique(prefixes, AppendVariantToken(basePath, directionalToken));

            AddUnique(prefixes, basePath);
            return prefixes;
        }

        private bool TryResolveFrameSequencePath(string prefix, Pawn pawn, out string resolvedPath)
        {
            resolvedPath = prefix;
            if (string.IsNullOrEmpty(prefix))
                return false;

            if (!frameSequenceCountCache.TryGetValue(prefix, out int frameCount))
            {
                string firstFramePath = AppendVariantToken(prefix, "f0");
                if (!TextureExists(firstFramePath))
                {
                    frameSequenceCountCache[prefix] = 0;
                    return false;
                }

                frameCount = 1;
                for (int i = 1; i < 16; i++)
                {
                    if (TextureExists(AppendVariantToken(prefix, $"f{i}")))
                        frameCount++;
                    else
                        break;
                }

                frameSequenceCountCache[prefix] = frameCount;
            }

            if (frameCount <= 0)
                return false;

            int animTick = pawn.TryGetComp<CompPawnSkin>()?.GetExpressionAnimTick() ?? (Find.TickManager?.TicksGame ?? 0);
            int frameIndex = Mathf.Abs(animTick) % frameCount;
            resolvedPath = AppendVariantToken(prefix, $"f{frameIndex}");
            return true;
        }

        private string ResolveDirectionalVariant(string basePath, Rot4 facing)
        {
            if (string.IsNullOrEmpty(basePath))
                return basePath;

            string? directionalToken = GetDirectionalToken(facing);
            if (string.IsNullOrEmpty(directionalToken))
                return basePath;

            string directionalPath = AppendVariantToken(basePath, directionalToken);
            if (TextureExists(directionalPath))
                return directionalPath;

            return basePath;
        }

        private string? GetDirectionalToken(Rot4 facing)
        {
            switch (facing.AsInt)
            {
                case 0:
                    return "north";
                case 1:
                case 3:
                    return "east";
                default:
                    return null;
            }
        }

        private static string AppendVariantToken(string basePath, string? token)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(token))
                return basePath;

            string nonNullToken = token!;
            string cleanToken = nonNullToken.StartsWith("_", StringComparison.Ordinal) ? nonNullToken.Substring(1) : nonNullToken;
            string extension = System.IO.Path.GetExtension(basePath);
            if (!string.IsNullOrEmpty(extension))
            {
                string withoutExtension = basePath.Substring(0, basePath.Length - extension.Length);
                return withoutExtension + "_" + cleanToken + extension;
            }

            return basePath + "_" + cleanToken;
        }

        private string ResolveExpressionVariant(string? basePath, Pawn? pawn)
        {
            if (string.IsNullOrEmpty(basePath) || pawn == null)
                return basePath ?? string.Empty;

            string nonNullBasePath = basePath!;

            try
            {
                var skinComp = pawn.TryGetComp<CharacterStudio.Core.CompPawnSkin>();
                if (skinComp != null && skinComp.HasActiveSkin)
                {
                    var expr = skinComp.GetEffectiveExpression();

                    if (expr == ExpressionType.Sleeping || expr == ExpressionType.Dead)
                    {
                        string sleepPath = AppendVariantToken(nonNullBasePath, "Sleep");
                        if (TextureExists(sleepPath))
                            return sleepPath;
                    }

                    if (expr == ExpressionType.Angry ||
                        expr == ExpressionType.AttackMelee ||
                        expr == ExpressionType.AttackRanged ||
                        expr == ExpressionType.WaitCombat ||
                        expr == ExpressionType.Scared)
                    {
                        string angryPath = AppendVariantToken(nonNullBasePath, "Angry");
                        if (TextureExists(angryPath))
                            return angryPath;
                    }

                    if (expr == ExpressionType.Blink)
                    {
                        string blinkPath = AppendVariantToken(nonNullBasePath, "Blink");
                        if (TextureExists(blinkPath))
                            return blinkPath;
                    }

                    return nonNullBasePath;
                }

                if (pawn.jobs?.curDriver != null && RestUtility.InBed(pawn))
                {
                    string sleepPath = AppendVariantToken(nonNullBasePath, "Sleep");
                    if (TextureExists(sleepPath))
                        return sleepPath;
                }

                if (pawn.Drafted || (pawn.MentalState != null && pawn.MentalState.def.IsAggro))
                {
                    string angryPath = AppendVariantToken(nonNullBasePath, "Angry");
                    if (TextureExists(angryPath))
                        return angryPath;
                }
            }
            catch
            {
            }

            return nonNullBasePath;
        }

        /// <summary>
        /// 检查纹理是否存在
        /// </summary>
        private string? ResolveLayeredFacePartBasePath(PawnRenderNode_Custom customNode, Pawn? pawn, Rot4 facing)
        {
            if (!customNode.layeredFacePartType.HasValue || pawn == null)
                return null;

            CompPawnSkin? skinComp = pawn.TryGetComp<CompPawnSkin>();
            PawnFaceConfig? faceConfig = skinComp?.ActiveSkin?.faceConfig;
            FaceRuntimeCompiledData? compiledData = skinComp?.CurrentFaceRuntimeCompiledData;
            FaceRenderTrack currentTrack = skinComp?.CurrentFaceRuntimeState.currentTrack ?? FaceRenderTrack.World;

            if (faceConfig == null || !faceConfig.enabled)
                return null;

            LayeredFacePartType partType = customNode.layeredFacePartType.Value;
            LayeredFacePartSide side = PawnFaceConfig.NormalizePartSide(partType, customNode.layeredFacePartSide);
            ExpressionType expression = GetCurrentExpressionForPawn(pawn);
            bool isOverlay = partType == LayeredFacePartType.Overlay;
            string overlayId = GetEffectiveLayeredOverlayId(customNode, pawn);

            string? compiledPath = ResolveCompiledLayeredFacePartPath(
                compiledData,
                currentTrack,
                partType,
                expression,
                overlayId,
                side,
                facing);
            if (!string.IsNullOrWhiteSpace(compiledPath))
                return compiledPath;

            string neutralPath = isOverlay
                ? faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, overlayId, facing)
                : faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, side, facing);

            if ((partType == LayeredFacePartType.Eye
                    || partType == LayeredFacePartType.Pupil
                    || partType == LayeredFacePartType.UpperLid
                    || partType == LayeredFacePartType.LowerLid)
                && (expression == ExpressionType.Blink
                    || expression == ExpressionType.Sleeping
                    || expression == ExpressionType.Dead))
            {
                LayeredFacePartConfig? closedStatePart = isOverlay
                    ? faceConfig.GetLayeredPartConfig(partType, expression, overlayId)
                    : faceConfig.GetLayeredPartConfig(partType, expression, side);

                if (closedStatePart != null)
                    return closedStatePart.GetDirectionalTexPath(facing);
            }

            string? channelVariantPath = TryResolveLayeredChannelVariantPath(customNode, pawn, neutralPath);
            if (!string.IsNullOrWhiteSpace(channelVariantPath))
                return channelVariantPath;

            string resolvedPath = isOverlay
                ? faceConfig.GetLayeredDirectionalPartPath(partType, expression, overlayId, facing)
                : faceConfig.GetLayeredDirectionalPartPath(partType, expression, side, facing);
            if (!string.IsNullOrWhiteSpace(resolvedPath))
                return resolvedPath;

            if (!string.IsNullOrWhiteSpace(neutralPath))
                return neutralPath;

            if (partType == LayeredFacePartType.Base)
            {
                string anyPath = faceConfig.GetAnyDirectionalLayeredPartPath(partType, facing);
                if (!string.IsNullOrWhiteSpace(anyPath))
                    return anyPath;
            }

            if (isOverlay)
            {
                string anyOverlayPath = faceConfig.GetLayeredDirectionalPartPath(partType, ExpressionType.Neutral, overlayId, facing);
                if (string.IsNullOrWhiteSpace(anyOverlayPath))
                    anyOverlayPath = faceConfig.GetAnyLayeredPartPath(partType, overlayId);
                if (!string.IsNullOrWhiteSpace(anyOverlayPath))
                    return anyOverlayPath;
            }

            return null;
        }

        private string? TryResolveLayeredPairedPartVariantPath(
            PawnRenderNode_Custom customNode,
            Pawn pawn,
            string? neutralPath,
            ExpressionType expression)
        {
            if (string.IsNullOrWhiteSpace(neutralPath))
                return null;

            LayeredFacePartType? partType = customNode.layeredFacePartType;
            if (!partType.HasValue || !IsPairedLayeredFacePart(partType.Value))
                return null;

            CompPawnSkin? skinComp = pawn.TryGetComp<CompPawnSkin>();
            if (skinComp == null)
                return null;

            List<string> sideTokens = GetPairedPartSideTokens(partType.Value, customNode.config, skinComp);
            if (sideTokens.Count == 0)
                return null;

            List<string> expressionTokens = GetPairedPartExpressionTokens(expression, customNode.config, skinComp);

            string resolvedNeutralPath = neutralPath!;
            foreach (string sideToken in sideTokens)
            {
                foreach (string expressionToken in expressionTokens)
                {
                    string sideThenExpressionPath = AppendVariantToken(AppendVariantToken(resolvedNeutralPath, sideToken), expressionToken);
                    if (TextureExists(sideThenExpressionPath))
                        return sideThenExpressionPath;

                    string expressionThenSidePath = AppendVariantToken(AppendVariantToken(resolvedNeutralPath, expressionToken), sideToken);
                    if (TextureExists(expressionThenSidePath))
                        return expressionThenSidePath;
                }

                string sideOnlyPath = AppendVariantToken(resolvedNeutralPath, sideToken);
                if (TextureExists(sideOnlyPath))
                    return sideOnlyPath;
            }

            return null;
        }

        private string? TryResolveLayeredChannelVariantPath(PawnRenderNode_Custom customNode, Pawn pawn, string? neutralPath)
        {
            if (string.IsNullOrWhiteSpace(neutralPath))
                return null;

            string resolvedNeutralPath = neutralPath!;

            LayeredFacePartType? partType = customNode.layeredFacePartType;
            if (!partType.HasValue)
                return null;

            if (!ShouldUseChannelVariantForPart(partType.Value))
                return null;

            CompPawnSkin? skinComp = pawn.TryGetComp<CompPawnSkin>();
            if (skinComp == null)
                return null;

            PawnLayerConfig? config = customNode.config;
            if (partType.Value == LayeredFacePartType.Pupil
                && config != null
                && config.eyeRenderMode == EyeRenderMode.UvOffset
                && config.eyeUvMoveRange > 0f)
            {
                return resolvedNeutralPath;
            }

            LayerRole role = config?.role ?? GetLayerRoleForLayeredPart(partType.Value);
            string? channelState = skinComp.GetChannelStateSuffix(role);
            if (string.IsNullOrWhiteSpace(channelState))
                return null;

            string variantPath = AppendVariantToken(resolvedNeutralPath, channelState);
            if (TextureExists(variantPath))
                return variantPath;

            return null;
        }

        private static bool IsPairedLayeredFacePart(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Brow:
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.Pupil:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                    return true;
                default:
                    return false;
            }
        }

        private List<string> GetPairedPartSideTokens(
            LayeredFacePartType partType,
            PawnLayerConfig? config,
            CompPawnSkin skinComp)
        {
            var results = new List<string>();

            if (partType == LayeredFacePartType.Pupil
                && config != null
                && config.eyeRenderMode == EyeRenderMode.UvOffset
                && config.eyeUvMoveRange > 0f)
            {
                return results;
            }

            EyeDirection eyeDirection = skinComp.CurEyeDirection;
            switch (eyeDirection)
            {
                case EyeDirection.Left:
                    AddUnique(results, "East");
                    AddUnique(results, "east");
                    AddUnique(results, "Left");
                    AddUnique(results, "left");
                    break;
                case EyeDirection.Right:
                    AddUnique(results, "West");
                    AddUnique(results, "west");
                    AddUnique(results, "Right");
                    AddUnique(results, "right");
                    break;
                case EyeDirection.Up:
                    AddUnique(results, "Up");
                    AddUnique(results, "up");
                    break;
                case EyeDirection.Down:
                    AddUnique(results, "Down");
                    AddUnique(results, "down");
                    break;
                default:
                    AddUnique(results, "Center");
                    AddUnique(results, "center");
                    AddUnique(results, "Left");
                    AddUnique(results, "left");
                    AddUnique(results, "Right");
                    AddUnique(results, "right");
                    break;
            }

            return results;
        }

        private List<string> GetPairedPartExpressionTokens(
            ExpressionType expression,
            PawnLayerConfig? config,
            CompPawnSkin skinComp)
        {
            var results = new List<string>();

            LayerRole role = config?.role ?? LayerRole.Decoration;
            string? channelState = skinComp.GetChannelStateSuffix(role);
            AddUnique(results, channelState);

            switch (expression)
            {
                case ExpressionType.Blink:
                    AddUnique(results, "Blink");
                    break;
                case ExpressionType.Sleeping:
                case ExpressionType.Dead:
                    AddUnique(results, "Close");
                    AddUnique(results, "Sleep");
                    break;
            }

            return results;
        }

        private bool ShouldUseChannelVariantForPart(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Brow:
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.Pupil:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                case LayeredFacePartType.Mouth:
                case LayeredFacePartType.Blush:
                case LayeredFacePartType.Tear:
                case LayeredFacePartType.Sweat:
                case LayeredFacePartType.Overlay:
                    return true;
                default:
                    return false;
            }
        }

        private LayerRole GetLayerRoleForLayeredPart(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Brow:
                    return LayerRole.Brow;
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                    return LayerRole.Lid;
                case LayeredFacePartType.Pupil:
                    return LayerRole.Eye;
                case LayeredFacePartType.Mouth:
                    return LayerRole.Mouth;
                case LayeredFacePartType.Blush:
                case LayeredFacePartType.Tear:
                case LayeredFacePartType.Sweat:
                case LayeredFacePartType.Overlay:
                    return LayerRole.Emotion;
                default:
                    return LayerRole.Decoration;
            }
        }

        private float GetLayeredFacePartMotionAmplitude(PawnRenderNode_Custom customNode, Pawn? pawn)
        {
            if (!customNode.layeredFacePartType.HasValue || pawn == null)
                return 0f;

            CompPawnSkin? skinComp = pawn.TryGetComp<CompPawnSkin>();
            FaceRuntimeCompiledData? compiledData = skinComp?.CurrentFaceRuntimeCompiledData;
            PawnFaceConfig? faceConfig = skinComp?.ActiveSkin?.faceConfig;
            List<LayeredFacePartConfig>? layeredParts = faceConfig?.layeredParts;

            LayeredFacePartType partType = customNode.layeredFacePartType.Value;
            LayeredFacePartSide side = PawnFaceConfig.NormalizePartSide(partType, customNode.layeredFacePartSide);
            ExpressionType expression = GetCurrentExpressionForPawn(pawn);
            float compiledAmplitude = ResolveCompiledMotionAmplitude(compiledData, partType, expression, side);
            if (compiledAmplitude > 0f)
                return compiledAmplitude;

            if (faceConfig == null || layeredParts == null || layeredParts.Count == 0)
                return 0f;

            PawnFaceConfig resolvedFaceConfig = faceConfig;
            bool isOverlay = partType == LayeredFacePartType.Overlay;
            string overlayId = GetEffectiveLayeredOverlayId(customNode, pawn);

            LayeredFacePartConfig? exact = isOverlay
                ? resolvedFaceConfig.GetLayeredPartConfig(partType, expression, overlayId)
                : resolvedFaceConfig.GetLayeredPartConfig(partType, expression, side);

            if (exact != null)
            {
                exact.SyncLegacyMotionAmplitude();
                return exact.motionAmplitude;
            }

            LayeredFacePartConfig? neutral = isOverlay
                ? resolvedFaceConfig.GetLayeredPartConfig(partType, ExpressionType.Neutral, overlayId)
                : resolvedFaceConfig.GetLayeredPartConfig(partType, ExpressionType.Neutral, side);

            if (neutral != null)
            {
                neutral.SyncLegacyMotionAmplitude();
                return neutral.motionAmplitude;
            }

            return 0f;
        }

        private Vector3 ApplyLayeredFacePartMotionAmplitude(
            PawnRenderNode_Custom customNode,
            Vector3 baseOffset,
            float primaryWave,
            float slowWave)
        {
            float motionAmplitude = GetLayeredFacePartMotionAmplitude(customNode, customNode.tree?.pawn);
            if (motionAmplitude <= 0f)
                return baseOffset;

            return new Vector3(
                baseOffset.x + motionAmplitude * primaryWave,
                baseOffset.y,
                baseOffset.z + motionAmplitude * slowWave);
        }

        private string? ResolveCompiledLayeredFacePartPath(
            FaceRuntimeCompiledData? compiledData,
            FaceRenderTrack currentTrack,
            LayeredFacePartType partType,
            ExpressionType expression,
            string overlayId,
            LayeredFacePartSide side,
            Rot4 facing)
        {
            if (compiledData == null)
                return null;

            FaceWorldTrackData? worldTrack = compiledData.worldTrack;
            FacePortraitTrackData? portraitTrack = compiledData.portraitTrack;

            if (currentTrack == FaceRenderTrack.World
                && partType == LayeredFacePartType.Base)
            {
                string worldPath = worldTrack?.defaultPath ?? string.Empty;
                if (worldTrack?.expressionCaches != null
                    && worldTrack.expressionCaches.TryGetValue(expression, out FaceExpressionRuntimeCache? worldCache)
                    && worldCache != null
                    && !string.IsNullOrWhiteSpace(worldCache.worldPath))
                {
                    worldPath = worldCache.worldPath;
                }

                if (!string.IsNullOrWhiteSpace(worldPath))
                    return worldPath;
            }

            if (portraitTrack?.expressionCaches != null
                && portraitTrack.expressionCaches.TryGetValue(expression, out FaceExpressionRuntimeCache? portraitCache)
                && portraitCache != null)
            {
                if (partType == LayeredFacePartType.Overlay)
                {
                    if (portraitCache.TryGetPortraitOverlayPath(overlayId, out string overlayPath)
                        && !string.IsNullOrWhiteSpace(overlayPath))
                    {
                        if (TryResolveDirectionalCompiledPath(overlayPath, facing, portraitCache.TryGetPortraitOverlayDirectionAvailability(overlayId, out FaceDirectionAvailability? overlayAvailability) ? overlayAvailability : null, out string resolvedOverlayPath))
                            return resolvedOverlayPath;

                        return overlayPath;
                    }
                }
                else
                {
                    if (portraitCache.TryGetPortraitPartPath(partType, side, out string partPath)
                        && !string.IsNullOrWhiteSpace(partPath))
                    {
                        if (TryResolveDirectionalCompiledPath(partPath, facing, portraitCache.TryGetPortraitPartDirectionAvailability(partType, side, out FaceDirectionAvailability? partAvailability) ? partAvailability : null, out string resolvedPartPath))
                            return resolvedPartPath;

                        return partPath;
                    }
                }
            }

            if (partType == LayeredFacePartType.Base
                && portraitTrack != null
                && !string.IsNullOrWhiteSpace(portraitTrack.basePath))
            {
                FaceDirectionAvailability? baseAvailability = portraitTrack.GetDirectionAvailability(LayeredFacePartType.Base);
                if (TryResolveDirectionalCompiledPath(portraitTrack.basePath, facing, baseAvailability, out string resolvedBasePath))
                    return resolvedBasePath;

                return portraitTrack.basePath;
            }

            return null;
        }

        private bool TryResolveDirectionalCompiledPath(
            string basePath,
            Rot4 facing,
            FaceDirectionAvailability? availability,
            out string resolvedPath)
        {
            resolvedPath = basePath;
            if (string.IsNullOrWhiteSpace(basePath))
                return false;

            if (facing == Rot4.North)
            {
                if (availability?.north == true)
                {
                    resolvedPath = ResolveDirectionalVariant(basePath, facing);
                    return true;
                }

                return false;
            }

            if (facing == Rot4.East || facing == Rot4.West)
            {
                if (availability?.east == true)
                {
                    resolvedPath = ResolveDirectionalVariant(basePath, facing);
                    return true;
                }

                return false;
            }

            return false;
        }

        private float ResolveCompiledMotionAmplitude(
            FaceRuntimeCompiledData? compiledData,
            LayeredFacePartType partType,
            ExpressionType expression,
            LayeredFacePartSide side)
        {
            return compiledData?.portraitTrack?.GetMotionAmplitude(partType, expression, side) ?? 0f;
        }

        private ExpressionType GetCurrentExpressionForPawn(Pawn? pawn)
        {
            if (pawn == null)
                return ExpressionType.Neutral;

            CompPawnSkin? skinComp = pawn.TryGetComp<CompPawnSkin>();
            if (skinComp != null)
                return skinComp.GetEffectiveExpression();

            return TryGetFallbackExpression(pawn) ?? ExpressionType.Neutral;
        }

        private bool TextureExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (textureExistsCache.TryGetValue(path, out bool cachedExists))
                return cachedExists;

            bool exists;
            if (System.IO.Path.IsPathRooted(path) || path.StartsWith("/"))
            {
                exists = RuntimeAssetLoader.ExternalTextureExists(path, out _);
            }
            else if (!RuntimeAssetLoader.IsMainThread())
            {
                exists = true;
            }
            else if (ContentFinder<Texture2D>.Get(path, false) != null)
            {
                exists = true;
            }
            else
            {
                exists = ContentFinder<Texture2D>.Get(path + "_north", false) != null;
            }

            textureExistsCache[path] = exists;
            return exists;
        }

        private bool IsTriggeredEquipmentLayerVisible(PawnRenderNode_Custom customNode, Pawn? pawn)
        {
            PawnLayerConfig? config = customNode.config;
            if (config == null || !config.useTriggeredEquipmentAnimation)
                return true;

            CompPawnSkin? skinComp = pawn?.TryGetComp<CompPawnSkin>();
            string triggerKey = string.IsNullOrWhiteSpace(config.triggerAbilityDefName)
                ? config.triggeredAnimationGroupKey
                : config.triggerAbilityDefName;
            if (skinComp == null || !skinComp.IsTriggeredEquipmentAnimationActive(triggerKey))
                return config.triggeredVisibleOutsideCycle;

            int now = Find.TickManager?.TicksGame ?? 0;
            int localTick = Mathf.Max(0, now - skinComp.triggeredEquipmentAnimationStartTick);
            int deployTicks = Mathf.Max(1, config.triggeredDeployTicks);
            int holdTicks = Mathf.Max(0, config.triggeredHoldTicks);

            if (localTick < deployTicks)
                return config.triggeredAnimationRole == EquipmentTriggeredAnimationRole.EffectLayer
                    ? (config.triggeredUseVfxVisibility && config.triggeredVisibleDuringDeploy)
                    : config.triggeredVisibleDuringDeploy;

            if (localTick < deployTicks + holdTicks)
                return config.triggeredAnimationRole == EquipmentTriggeredAnimationRole.EffectLayer
                    ? (config.triggeredUseVfxVisibility && config.triggeredVisibleDuringHold)
                    : config.triggeredVisibleDuringHold;

            return config.triggeredAnimationRole == EquipmentTriggeredAnimationRole.EffectLayer
                ? (config.triggeredUseVfxVisibility && config.triggeredVisibleDuringReturn)
                : config.triggeredVisibleDuringReturn;
        }

        private void ApplyTriggeredEquipmentAnimation(PawnRenderNode_Custom customNode, Pawn? pawn)
        {
            PawnLayerConfig? config = customNode.config;
            if (config == null || !config.useTriggeredEquipmentAnimation)
                return;

            CompPawnSkin? skinComp = pawn?.TryGetComp<CompPawnSkin>();
            string triggerKey = string.IsNullOrWhiteSpace(config.triggerAbilityDefName)
                ? config.triggeredAnimationGroupKey
                : config.triggerAbilityDefName;
            if (skinComp == null || !skinComp.IsTriggeredEquipmentAnimationActive(triggerKey))
            {
                customNode.currentAnimAngle = 0f;
                customNode.currentAnimOffset = Vector3.zero;
                return;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            int localTick = Mathf.Max(0, now - skinComp.triggeredEquipmentAnimationStartTick);
            int deployTicks = Mathf.Max(1, config.triggeredDeployTicks);
            int holdTicks = Mathf.Max(0, config.triggeredHoldTicks);
            int returnTicks = Mathf.Max(1, config.triggeredReturnTicks);

            float angle;
            if (localTick < deployTicks)
            {
                float t = Mathf.Clamp01(localTick / (float)deployTicks);
                angle = Mathf.Lerp(config.triggeredReturnAngle, config.triggeredDeployAngle, t);
            }
            else if (localTick < deployTicks + holdTicks)
            {
                angle = config.triggeredDeployAngle;
            }
            else
            {
                float t = Mathf.Clamp01((localTick - deployTicks - holdTicks) / (float)returnTicks);
                angle = Mathf.Lerp(config.triggeredDeployAngle, config.triggeredReturnAngle, t);
            }

            customNode.currentAnimAngle = angle;

            Vector2 pivot = config.animPivotOffset;
            if (pivot == Vector2.zero)
            {
                customNode.currentAnimOffset = Vector3.zero;
                return;
            }

            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float rotX = cos * pivot.x - sin * pivot.y;
            float rotZ = sin * pivot.x + cos * pivot.y;
            customNode.currentAnimOffset = new Vector3(pivot.x - rotX, 0f, pivot.y - rotZ);
        }

        public static void ClearCache()
        {
            textureExistsCache.Clear();
            frameSequenceCountCache.Clear();
        }
    }
}