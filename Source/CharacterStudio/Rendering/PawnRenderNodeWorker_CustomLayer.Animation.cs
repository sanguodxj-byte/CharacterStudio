using System;
using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;
using CharacterStudio.Abilities;
using System.Linq;
using System.Collections.Generic;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 自定义图层渲染工作器 — 通用图层动画 + 触发式装备动画 + 外部图层动画
    /// </summary>
    public partial class PawnRenderNodeWorker_CustomLayer : PawnRenderNodeWorker
    {
        /// <summary>
        /// 计算动画效果
        /// 统一处理所有动画类型，计算旋转角度、位移偏移和缩放
        /// </summary>
        private void CalculateAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config)
        {
            Pawn? ownerPawn = customNode.tree?.pawn;
            int currentTick = (Current.ProgramState == ProgramState.Playing)
                ? AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(ownerPawn, Find.TickManager.TicksGame)
                : (int)(Time.realtimeSinceStartup * 60f);

            // P-PERF: 同一 tick 内不重复计算动画（Brownian/Spin 除外，它们有状态递增）
            if (customNode.lastAnimCalcTick == currentTick
                && config.animationType != LayerAnimationType.Brownian
                && config.animationType != LayerAnimationType.Spin)
            {
                return;
            }
            customNode.lastAnimCalcTick = currentTick;

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

                case LayerAnimationType.Brownian:
                    CalculateBrownianAnimation(customNode, config, currentTick);
                    break;
            }
        }

        private void CalculateBrownianAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config, int currentTick)
        {
            Pawn? pawn = customNode.tree?.pawn;
            if (pawn == null)
            {
                customNode.currentAnimAngle = 0f;
                customNode.currentAnimOffset = Vector3.zero;
                customNode.currentAnimScale = 1f;
                return;
            }

            if (customNode.lastBrownianTick < 0)
            {
                customNode.lastBrownianTick = currentTick;
                customNode.currentBrownianVelocity = Vector3.zero;
            }

            // P-PERF: 限制单帧最大迭代次数，避免暂停恢复/TimeStop结束时单帧卡顿
            const int MaxBrownianTicksPerFrame = 30;
            int rawDelta = currentTick - customNode.lastBrownianTick;
            int deltaTicks = Mathf.Min(MaxBrownianTicksPerFrame, Mathf.Max(1, rawDelta));
            customNode.lastBrownianTick = currentTick - (rawDelta > MaxBrownianTicksPerFrame ? rawDelta - MaxBrownianTicksPerFrame : 0);

            bool inCombat = pawn.Drafted
                || (pawn.mindState?.enemyTarget != null)
                || (pawn.stances?.curStance is Stance_Busy)
                || (pawn.CurJobDef != null && pawn.CurJobDef.alwaysShowWeapon);

            float targetRadius = inCombat
                ? Mathf.Max(0.01f, config.brownianCombatRadius)
                : Mathf.Max(0.01f, config.brownianRadius);

            float jitter = Mathf.Max(0f, config.brownianJitter) * deltaTicks;
            float damping = Mathf.Clamp01(config.brownianDamping);

            Vector3 offset = customNode.currentAnimOffset;
            Vector3 velocity = customNode.currentBrownianVelocity;

            for (int i = 0; i < deltaTicks; i++)
            {
                Vector3 randomForce = new Vector3(
                    Rand.Range(-jitter, jitter),
                    0f,
                    Rand.Range(-jitter, jitter));

                Vector3 centerPull = offset.sqrMagnitude > 0.0001f
                    ? (-offset.normalized) * (offset.magnitude / Mathf.Max(targetRadius, 0.0001f)) * jitter * 0.35f
                    : Vector3.zero;

                if (inCombat)
                {
                    centerPull *= 2f;
                }

                velocity += randomForce + centerPull;
                velocity *= damping;

                Vector3 candidate = offset + velocity;
                if (candidate.magnitude > targetRadius)
                {
                    candidate = candidate.normalized * targetRadius;
                    velocity *= 0.4f;
                }

                if (config.brownianRespectWalkability || config.brownianStayInRoom)
                {
                    IntVec3 pawnCell = pawn.PositionHeld;
                    Map? map = pawn.MapHeld;
                    if (map != null)
                    {
                        IntVec3 candidateCell = pawnCell + new IntVec3(
                            Mathf.RoundToInt(candidate.x / 0.1f),
                            0,
                            Mathf.RoundToInt(candidate.z / 0.1f));

                        bool blocked = false;
                        if (!candidateCell.InBounds(map))
                        {
                            blocked = true;
                        }
                        else
                        {
                            if (config.brownianRespectWalkability && !candidateCell.Walkable(map))
                            {
                                blocked = true;
                            }

                            if (!blocked && config.brownianStayInRoom)
                            {
                                Room? sourceRoom = pawnCell.GetRoom(map);
                                Room? targetRoom = candidateCell.GetRoom(map);
                                if (sourceRoom != null && targetRoom != sourceRoom)
                                {
                                    blocked = true;
                                }
                            }
                        }

                        if (blocked)
                        {
                            velocity *= -0.45f;
                            candidate = offset + velocity;
                            if (candidate.magnitude > targetRadius)
                            {
                                candidate = candidate.normalized * targetRadius;
                            }
                        }
                    }
                }

                offset = candidate;
            }

            customNode.currentBrownianVelocity = velocity;
            customNode.currentAnimOffset = offset;
            customNode.currentAnimAngle = 0f;
            customNode.currentAnimScale = 1f;
        }

        /// <summary>
        /// 计算抽动动画（兽耳）
        /// 随机触发的快速抖动
        /// </summary>
        private void CalculateTwitchAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config, int currentTick)
        {
            if (customNode.nextTwitchTick < 0)
            {
                int interval = (int)(600 / Mathf.Max(0.1f, config.animFrequency));
                customNode.nextTwitchTick = currentTick + Rand.Range(interval / 2, interval * 2) + customNode.GetHashCode() % 100;
            }

            if (!customNode.isTwitching && currentTick >= customNode.nextTwitchTick)
            {
                customNode.isTwitching = true;
                customNode.twitchStartTick = currentTick;

                int interval = (int)(600 / Mathf.Max(0.1f, config.animFrequency));
                customNode.nextTwitchTick = currentTick + Rand.Range(interval / 2, interval * 2);
            }

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
                    float twitchValue = Mathf.Sin(progress * Mathf.PI);
                    customNode.currentAnimAngle = twitchValue * config.animAmplitude;

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

            float primaryWave = Mathf.Sin(timeSec * freq + phaseOffset);
            float secondaryWave = Mathf.Sin(timeSec * freq * 2f + phaseOffset) * 0.3f;

            customNode.currentAnimAngle = (primaryWave + secondaryWave) * amp;

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
        /// </summary>
        private void CalculateIdleSwayAnimation(PawnRenderNode_Custom customNode, PawnLayerConfig config, float timeSec, float phaseOffset)
        {
            float freq = config.animFrequency;
            float amp = config.animAmplitude;

            float wave1 = Mathf.Sin(timeSec * freq + phaseOffset) * 0.6f;
            float wave2 = Mathf.Sin(timeSec * freq * 1.7f + phaseOffset + Mathf.PI / 3f) * 0.25f;
            float wave3 = Mathf.Sin(timeSec * freq * 0.5f + phaseOffset) * 0.15f;

            float compositeWave = wave1 + wave2 + wave3;
            customNode.currentAnimAngle = compositeWave * amp;

            if (config.animAffectsOffset)
            {
                float offsetX = compositeWave * config.animOffsetAmplitude;
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
            float freq = config.animFrequency * 0.5f;
            float amp = config.animAmplitude * 0.01f;

            float breatheValue = Mathf.Sin(timeSec * freq + phaseOffset);

            customNode.currentAnimScale = 1f + breatheValue * amp;
            customNode.currentAnimAngle = 0f;

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
        /// 计算持续旋转动画（Spin）
        /// 图层以枢轴点为中心匀速旋转，适用于旋翼/飞行器等。
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

            float degreesPerTick = config.animFrequency * 360f / 60f * config.animSpeed;
            customNode.currentSpinAngle = (customNode.currentSpinAngle + degreesPerTick * deltaTicks) % 360f;

            customNode.currentAnimAngle = customNode.currentSpinAngle;
            customNode.currentAnimOffset = Vector3.zero;
            customNode.currentAnimScale = 1f;

            Vector2 pivot = config.animPivotOffset;
            if (pivot != Vector2.zero)
            {
                float rad = customNode.currentSpinAngle * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                float rotX = cos * pivot.x - sin * pivot.y;
                float rotZ = sin * pivot.x + cos * pivot.y;
                customNode.currentAnimOffset = new Vector3(
                    pivot.x - rotX,
                    0f,
                    pivot.y - rotZ);
            }
        }

        private void EnsureExternalLayerAnimationUpdated(PawnRenderNode_Custom customNode, PawnDrawParms parms)
        {
            if (customNode.config == null || parms.pawn == null)
            {
                ResetExternalLayerAnimation(customNode);
                return;
            }

            if (!CharacterStudioLayerAnimationRegistry.HasProviders)
            {
                ResetExternalLayerAnimation(customNode);
                return;
            }

            int currentTick = (Current.ProgramState == ProgramState.Playing)
                ? (Find.TickManager?.TicksGame ?? 0)
                : (int)(Time.realtimeSinceStartup * 60f);

            if (customNode.lastExternalLayerAnimationTick == currentTick)
            {
                return;
            }

            customNode.lastExternalLayerAnimationTick = currentTick;
            CharacterStudioLayerAnimationContext context = new CharacterStudioLayerAnimationContext(
                parms.pawn,
                customNode.config,
                parms.facing,
                currentTick,
                Current.ProgramState != ProgramState.Playing);
            CharacterStudioLayerAnimationResult result = CharacterStudioLayerAnimationRegistry.Evaluate(context);
            customNode.currentExternalLayerAngle = result.angle;
            customNode.currentExternalLayerOffset = result.offset;
                customNode.currentExternalLayerScale = NormalizeExternalLayerScale(result.scaleMultiplier);
        }

        private static void ResetExternalLayerAnimation(PawnRenderNode_Custom customNode)
        {
            customNode.currentExternalLayerAngle = 0f;
            customNode.currentExternalLayerOffset = Vector3.zero;
            customNode.currentExternalLayerScale = Vector3.one;
        }

        private static Vector3 NormalizeExternalLayerScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
        }

        private bool IsTriggeredEquipmentLayerVisible(PawnRenderNode_Custom customNode, Pawn? pawn)
        {
            PawnLayerConfig? config = customNode.config;
            if (config == null || !config.useTriggeredEquipmentAnimation)
                return true;

            Rot4 facing = pawn?.Rotation ?? Rot4.South;
            EquipmentTriggeredAnimationOverride animationState = ResolveDirectionalTriggeredAnimationState(config, facing);
            if (!animationState.useTriggeredLocalAnimation)
                return true;

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            string triggerKey = string.IsNullOrWhiteSpace(animationState.triggerAbilityDefName)
                ? animationState.animationGroupKey
                : animationState.triggerAbilityDefName;
            if (skinComp == null || !skinComp.IsTriggeredEquipmentAnimationActive(triggerKey))
                return animationState.triggeredVisibleOutsideCycle;

            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(pawn, Find.TickManager?.TicksGame ?? 0);
            int localTick = Mathf.Max(0, now - skinComp.triggeredEquipmentAnimationStartTick);
            int deployTicks = Mathf.Max(1, animationState.triggeredDeployTicks);
            int holdTicks = Mathf.Max(0, animationState.triggeredHoldTicks);

            if (localTick < deployTicks)
                return animationState.triggeredAnimationRole == EquipmentTriggeredAnimationRole.EffectLayer
                    ? (animationState.triggeredUseVfxVisibility && animationState.triggeredVisibleDuringDeploy)
                    : animationState.triggeredVisibleDuringDeploy;

            if (localTick < deployTicks + holdTicks)
                return animationState.triggeredAnimationRole == EquipmentTriggeredAnimationRole.EffectLayer
                    ? (animationState.triggeredUseVfxVisibility && animationState.triggeredVisibleDuringHold)
                    : animationState.triggeredVisibleDuringHold;

            return animationState.triggeredAnimationRole == EquipmentTriggeredAnimationRole.EffectLayer
                ? (animationState.triggeredUseVfxVisibility && animationState.triggeredVisibleDuringReturn)
                : animationState.triggeredVisibleDuringReturn;
        }

        private void ApplyTriggeredEquipmentAnimation(PawnRenderNode_Custom customNode, Pawn? pawn)
        {
            PawnLayerConfig? config = customNode.config;
            if (config == null || !config.useTriggeredEquipmentAnimation)
                return;

            Rot4 facing = pawn?.Rotation ?? Rot4.South;
            EquipmentTriggeredAnimationOverride animationState = ResolveDirectionalTriggeredAnimationState(config, facing);
            if (!animationState.useTriggeredLocalAnimation)
            {
                customNode.currentAnimAngle = 0f;
                customNode.currentAnimOffset = Vector3.zero;
                return;
            }

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            string triggerKey = string.IsNullOrWhiteSpace(animationState.triggerAbilityDefName)
                ? animationState.animationGroupKey
                : animationState.triggerAbilityDefName;
            if (skinComp == null || !skinComp.IsTriggeredEquipmentAnimationActive(triggerKey))
            {
                customNode.currentAnimAngle = 0f;
                customNode.currentAnimOffset = Vector3.zero;
                return;
            }

            int now = AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(pawn, Find.TickManager?.TicksGame ?? 0);
            int localTick = Mathf.Max(0, now - skinComp.triggeredEquipmentAnimationStartTick);
            int deployTicks = Mathf.Max(1, animationState.triggeredDeployTicks);
            int holdTicks = Mathf.Max(0, animationState.triggeredHoldTicks);
            int returnTicks = Mathf.Max(1, animationState.triggeredReturnTicks);

            float angle;
            if (localTick < deployTicks)
            {
                float t = Mathf.Clamp01(localTick / (float)deployTicks);
                angle = Mathf.Lerp(animationState.triggeredReturnAngle, animationState.triggeredDeployAngle, t);
            }
            else if (localTick < deployTicks + holdTicks)
            {
                angle = animationState.triggeredDeployAngle;
            }
            else
            {
                float t = Mathf.Clamp01((localTick - deployTicks - holdTicks) / (float)returnTicks);
                angle = Mathf.Lerp(animationState.triggeredDeployAngle, animationState.triggeredReturnAngle, t);
            }

            customNode.currentAnimAngle = angle;

            Vector2 pivot = animationState.triggeredPivotOffset;
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

        private static EquipmentTriggeredAnimationOverride ResolveDirectionalTriggeredAnimationState(PawnLayerConfig config, Rot4 facing)
        {
            EquipmentTriggeredAnimationOverride? overrideData = facing == Rot4.North
                ? config.triggeredAnimationNorth
                : ((facing == Rot4.East || facing == Rot4.West) ? config.triggeredAnimationEastWest : config.triggeredAnimationSouth);

            return overrideData ?? new EquipmentTriggeredAnimationOverride
            {
                useTriggeredLocalAnimation = config.useTriggeredEquipmentAnimation,
                triggerAbilityDefName = config.triggerAbilityDefName ?? string.Empty,
                animationGroupKey = config.triggeredAnimationGroupKey ?? string.Empty,
                triggeredAnimationRole = config.triggeredAnimationRole,
                triggeredDeployAngle = config.triggeredDeployAngle,
                triggeredReturnAngle = config.triggeredReturnAngle,
                triggeredDeployTicks = config.triggeredDeployTicks,
                triggeredHoldTicks = config.triggeredHoldTicks,
                triggeredReturnTicks = config.triggeredReturnTicks,
                triggeredPivotOffset = config.triggeredPivotOffset,
                triggeredUseVfxVisibility = config.triggeredUseVfxVisibility,
                triggeredVisibleDuringDeploy = config.triggeredVisibleDuringDeploy,
                triggeredVisibleDuringHold = config.triggeredVisibleDuringHold,
                triggeredVisibleDuringReturn = config.triggeredVisibleDuringReturn,
                triggeredVisibleOutsideCycle = config.triggeredVisibleOutsideCycle
            };
        }
    }
}