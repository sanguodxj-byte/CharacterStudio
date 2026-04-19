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
    /// 自定义图层渲染工作器 — 程序化面部变换（眉毛、眼睛、瞳孔、眼睑、嘴巴、情绪叠加层）
    /// </summary>
    public partial class PawnRenderNodeWorker_CustomLayer : PawnRenderNodeWorker
    {
        private void EnsureProgrammaticFaceStateUpdated(PawnRenderNode_Custom customNode, Pawn? pawn)
        {
            if (!customNode.layeredFacePartType.HasValue || pawn == null)
            {
                ResetProgrammaticFaceTransform(customNode);
                return;
            }

            int currentTick = (Current.ProgramState == ProgramState.Playing)
                ? AbilityTimeStopRuntimeController.ResolveVisualTickForPawn(pawn, Find.TickManager?.TicksGame ?? 0)
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

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            if (skinComp == null)
                return;

            ExpressionType expression = skinComp.GetEffectiveExpression();

            bool hideForReplacement = ShouldHideLayeredEyePartForReplacement(customNode, pawn, skinComp, expression);
            if (hideForReplacement)
            {
                HideProgrammaticFacePart(customNode);
                UpdateProgrammaticFaceAlpha(customNode);
                return;
            }

            bool hideAtBlinkEndpoint = ShouldHideUpperLidAtBlinkEndpoint(customNode, skinComp, expression);
            if (hideAtBlinkEndpoint)
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
            EyeDirection eyeDirection = skinComp.CurEyeDirection;
            LidState lidState = skinComp.GetEffectiveLidState();
            BrowState browState = skinComp.GetEffectiveBrowState();
            MouthState mouthState = skinComp.GetEffectiveMouthState();
            EmotionOverlayState emotionState = skinComp.GetEffectiveEmotionOverlayState();
            string overlaySemanticKey = skinComp.GetEffectiveOverlaySemanticKey();
            bool isBlinkActive = skinComp.IsBlinkActive();
            BlinkPhase blinkPhase = skinComp.GetBlinkPhase();
            float blinkPhaseProgress = skinComp.GetBlinkPhaseProgress01();
            bool hasReplacementEyeOverlay = HasActiveReplacementEye(customNode, pawn, skinComp);
            EyeAnimationVariant eyeVariant = skinComp.GetEffectiveEyeAnimationVariant();
            PupilScaleVariant pupilVariant = skinComp.GetEffectivePupilScaleVariant();
            Vector2 gazeOffset = skinComp.CurrentFaceRuntimeState.gazeOffset;

            var transformContext = new FaceTransformContext(
                customNode.layeredFacePartType.Value,
                customNode.layeredFacePartSide,
                customNode.layeredOverlayId,
                eyeDirection,
                lidState,
                browState,
                mouthState,
                emotionState,
                isBlinkActive,
                blinkPhase,
                blinkPhaseProgress,
                hasReplacementEyeOverlay,
                eyeVariant,
                pupilVariant,
                skinComp.CurrentFaceRuntimeState.winkSide,
                expression,
                pawn.Rotation,
                gazeOffset,
                primaryWave,
                slowWave,
                Mathf.Abs(primaryWave),
                Mathf.Abs(slowWave));

            // ── Profile 驱动路径：Eye, Pupil, UpperLid, LowerLid, Brow, Mouth ──
            // 这六个通道有偏移动画，使用皮肤实例 profile + FaceBlendState
            LayeredFacePartType partType = customNode.layeredFacePartType.Value;
            bool useProfilePath = partType == LayeredFacePartType.Eye
                || partType == LayeredFacePartType.Sclera
                || partType == LayeredFacePartType.Pupil
                || partType == LayeredFacePartType.UpperLid
                || partType == LayeredFacePartType.LowerLid
                || partType == LayeredFacePartType.Brow
                || partType == LayeredFacePartType.Mouth;

            if (useProfilePath)
            {
                ApplyProfileDrivenTransform(customNode, transformContext, skinComp, currentTick);
            }
            else
            {
                // ── 旧路径：ReplacementEye, Hair, Blush, Tear, Sweat, Overlay ──
                switch (partType)
                {
                    case LayeredFacePartType.ReplacementEye:
                    case LayeredFacePartType.Hair:
                    case LayeredFacePartType.Blush:
                    case LayeredFacePartType.Tear:
                    case LayeredFacePartType.Sweat:
                    case LayeredFacePartType.Overlay:
                        ApplyEvaluatedFaceTransform(customNode, transformContext, GetLayeredLidMotionConfig(customNode));
                        break;
                }
            }

            customNode.currentProgrammaticOffset = ApplyLayeredFacePartMotionAmplitude(
                customNode,
                customNode.currentProgrammaticOffset,
                primaryWave,
                slowWave);

            UpdateProgrammaticFaceAlpha(customNode);
        }

        /// <summary>
        /// 基于 FaceChannelProfileSet（从皮肤实例读取）+ FaceBlendState 的数据驱动求值。
        /// 用于 Eye, Pupil, UpperLid, LowerLid, Brow, Mouth 通道。
        /// Profile 数据来自 skinComp.ActiveSkin.faceConfig，
        /// 支持编辑器实时修改后立即反映到预览。
        /// </summary>
        private void ApplyProfileDrivenTransform(
            PawnRenderNode_Custom customNode,
            FaceTransformContext context,
            CompPawnSkin skinComp,
            int currentTick)
        {
            FaceRuntimeState runtimeState = skinComp.CurrentFaceRuntimeState;
            PawnFaceConfig? faceCfg = skinComp.ActiveSkin?.faceConfig;
            PawnEyeDirectionConfig? eyeDirCfg = faceCfg?.eyeDirectionConfig;

            // 如果没有显式配置眼睛方向，则始终回退到默认配置以确保眨眼等动画驱动逻辑正常运行。
            // 即使 faceCfg 为 null（皮肤未配置面部参数），也需要 Default 配置来驱动眼睑闭合动画。
            if (eyeDirCfg == null)
            {
                eyeDirCfg = PawnEyeDirectionConfig.Default;
            }

            if (!runtimeState.blendStatesInitialized)
            {
                InitializeBlendStates(runtimeState, context, currentTick, faceCfg);
                runtimeState.blendStatesInitialized = true;
            }

            FaceTransformResult result = FaceTransformResult.Visible(0f, Vector3.zero, Vector3.one);

            switch (context.partType)
            {
                case LayeredFacePartType.Eye:
                {
                    // Close/HappyClosedPeak 时立即隐藏眼球
                    // Blink 时：眼睑动画可遮盖眼球，无需隐藏。
                    // 仅在有替换眼且处于 HideBaseEyeParts 阶段时才隐藏基础部件（与替换眼同步）
                    if (context.eyeVariant == EyeAnimationVariant.HappyClosedPeak
                        || context.lidState == LidState.Close
                        || (context.hasReplacementEyeOverlay && IsBlinkHidingBaseParts(context)))
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    string eyeKey = FaceTransformEvaluator.ResolveEyeStateKey(context.eyeVariant, context.lidState, context.expression);

                    bool eyeChanged = runtimeState.lastEyeVariant != context.eyeVariant;
                    if (eyeChanged && eyeDirCfg != null)
                    {
                        FaceStateProfile? profile = eyeDirCfg.GetOrBuildEyeProfiles().GetProfileOrDefault(eyeKey);
                        runtimeState.eyeBlend.BeginTransition(profile, currentTick);
                        runtimeState.lastEyeVariant = context.eyeVariant;
                    }
                    result = FaceTransformEvaluator.EvaluateEyeFromProfile(runtimeState.eyeBlend, context, currentTick);
                    break;
                }

                case LayeredFacePartType.Sclera:
                {
                    // Close 时立即隐藏眼白
                    // Blink 时：眼睑动画可遮盖眼白，无需隐藏。
                    // 仅在有替换眼时才隐藏（与替换眼同步）
                    if (context.lidState == LidState.Close
                        || (context.hasReplacementEyeOverlay && IsBlinkHidingBaseParts(context)))
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }
                    // 正常状态下眼白无专属程序动画，仅参与通用 motionAmplitude
                    break;
                }

                case LayeredFacePartType.Pupil:
                {
                    // 瞳孔隐藏条件：
                    // - 特定变体要求隐藏（BlinkHidden/HappyClosedPeak/BlinkClosed）
                    // - Close 时立即隐藏
                    // - Blink 时：眼睑动画可遮盖瞳孔，无需隐藏。
                    //   仅在有替换眼时才隐藏基础部件（与替换眼显示同步）
                    if (context.pupilVariant == PupilScaleVariant.BlinkHidden
                        || context.eyeVariant == EyeAnimationVariant.HappyClosedPeak
                        || context.eyeVariant == EyeAnimationVariant.BlinkClosed
                        || context.lidState == LidState.Close
                        || (context.hasReplacementEyeOverlay && IsBlinkHidingBaseParts(context)))
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    string pupilKey = FaceTransformEvaluator.ResolvePupilStateKey(context.pupilVariant, context.expression, context.eyeVariant);
                    bool pupilChanged = runtimeState.lastPupilVariant != context.pupilVariant;
                    if (pupilChanged && eyeDirCfg != null)
                    {
                        FaceStateProfile? profile = eyeDirCfg.GetOrBuildPupilProfiles().GetProfileOrDefault(pupilKey);
                        runtimeState.pupilBlend.BeginTransition(profile, currentTick);
                        runtimeState.lastPupilVariant = context.pupilVariant;
                    }
                    result = FaceTransformEvaluator.EvaluatePupilFromProfile(runtimeState.pupilBlend, context, currentTick);

                    // 叠加注视方向偏移（gazeOffset + eyeDirection）
                    // 这是 Profile 路径之前遗漏的关键逻辑
                    if (eyeDirCfg != null && !result.hidden)
                    {
                        Vector3 dirOffset = FaceTransformEvaluator.ComputePupilDirectionOffset(context, eyeDirCfg.pupilMotion ?? new PawnEyeDirectionConfig.PupilMotionConfig());
                        result = FaceTransformResult.Visible(result.angle, result.offset + dirOffset, result.scale);
                    }
                    break;
                }

                case LayeredFacePartType.UpperLid:
                {
                    // Profile 驱动路径：眨眼时强制使用 "Blink" Profile，否则按 lidState 解析
                    string upperLidKey = context.isBlinkActive
                        ? "Blink"
                        : FaceTransformEvaluator.ResolveUpperLidStateKey(context.lidState, context.eyeVariant);
                    bool upperLidChanged = runtimeState.lastUpperLidState != context.lidState
                        || runtimeState.lastUpperLidEyeVariant != context.eyeVariant
                        || (context.isBlinkActive && !runtimeState.wasBlinkActiveForUpperLid)
                        || (!context.isBlinkActive && runtimeState.wasBlinkActiveForUpperLid);
                    if (upperLidChanged && eyeDirCfg != null)
                    {
                        FaceStateProfile? profile = eyeDirCfg.GetOrBuildUpperLidProfiles().GetProfileOrDefault(upperLidKey);
                        runtimeState.upperLidBlend.BeginTransition(profile, currentTick);
                        runtimeState.lastUpperLidState = context.lidState;
                        runtimeState.lastUpperLidEyeVariant = context.eyeVariant;
                    }
                    float ulMoveDown = GetLayeredUpperLidMoveDown(customNode);
                    result = FaceTransformEvaluator.EvaluateUpperLidFromProfile(runtimeState.upperLidBlend, context, currentTick, ulMoveDown);
                    runtimeState.wasBlinkActiveForUpperLid = context.isBlinkActive;
                    break;
                }

                case LayeredFacePartType.LowerLid:
                {
                    // Profile 驱动路径：眨眼时强制使用 "Blink" Profile，否则按 lidState 解析
                    string lowerLidKey = context.isBlinkActive
                        ? "Blink"
                        : FaceTransformEvaluator.ResolveLowerLidStateKey(context.lidState);
                    bool lowerLidChanged = runtimeState.lastLowerLidState != context.lidState
                        || (context.isBlinkActive && !runtimeState.wasBlinkActiveForLowerLid)
                        || (!context.isBlinkActive && runtimeState.wasBlinkActiveForLowerLid);
                    if (lowerLidChanged && eyeDirCfg != null)
                    {
                        FaceStateProfile? profile = eyeDirCfg.GetOrBuildLowerLidProfiles().GetProfileOrDefault(lowerLidKey);
                        runtimeState.lowerLidBlend.BeginTransition(profile, currentTick);
                        runtimeState.lastLowerLidState = context.lidState;
                    }
                    result = FaceTransformEvaluator.EvaluateLowerLidFromProfile(runtimeState.lowerLidBlend, context, currentTick);
                    runtimeState.wasBlinkActiveForLowerLid = context.isBlinkActive;
                    break;
                }

                case LayeredFacePartType.Brow:
                {
                    string browKey = FaceTransformEvaluator.ResolveBrowStateKey(context.browState);
                    bool browChanged = runtimeState.lastBrowState != context.browState;
                    if (browChanged && faceCfg != null)
                    {
                        FaceStateProfile? profile = faceCfg.GetOrBuildBrowProfiles().GetProfileOrDefault(browKey);
                        runtimeState.browBlend.BeginTransition(profile, currentTick);
                        runtimeState.lastBrowState = context.browState;
                    }
                    result = FaceTransformEvaluator.EvaluateBrowFromProfile(runtimeState.browBlend, context, currentTick);
                    break;
                }

                case LayeredFacePartType.Mouth:
                {
                    string mouthKey = FaceTransformEvaluator.ResolveMouthStateKey(context.mouthState, context.expression);
                    bool mouthChanged = runtimeState.lastMouthState != context.mouthState
                        || runtimeState.lastMouthExpression != context.expression;
                    if (mouthChanged && faceCfg != null)
                    {
                        FaceStateProfile? profile = faceCfg.GetOrBuildMouthProfiles().GetProfileOrDefault(mouthKey);
                        runtimeState.mouthBlend.BeginTransition(profile, currentTick);
                        runtimeState.lastMouthState = context.mouthState;
                        runtimeState.lastMouthExpression = context.expression;
                    }
                    result = FaceTransformEvaluator.EvaluateMouthFromProfile(runtimeState.mouthBlend, context, currentTick);
                    break;
                }
            }

            if (result.hidden)
            {
                HideProgrammaticFacePart(customNode);
                return;
            }

            SetProgrammaticFaceTransform(customNode, result.angle, result.offset, result.scale);
        }

        /// <summary>
        /// 判断当前是否处于眨眼的"隐藏基础眼部部件"阶段。
        /// 眨眼通过 isBlinkActive 触发（非 LidState.Blink）。
        /// 仅在 HideBaseEyeParts/ShowReplacementEye/RestoreBaseEyeParts 阶段返回 true，
        /// ClosingLid/OpeningLid 阶段返回 false（此时眼睑尚未完全闭合/已开始打开，眼球应保持可见）。
        /// </summary>
        private static bool IsBlinkHidingBaseParts(FaceTransformContext context)
        {
            if (!context.isBlinkActive) return false;
            return context.blinkPhase == BlinkPhase.HideBaseEyeParts
                || context.blinkPhase == BlinkPhase.ShowReplacementEye
                || context.blinkPhase == BlinkPhase.RestoreBaseEyeParts;
        }

        /// <summary>
        /// 首次初始化所有 blend 状态（使用 ForceSet 而非 BeginTransition）。
        /// 从皮肤实例的 faceConfig 读取 profile。
        /// </summary>
        private void InitializeBlendStates(FaceRuntimeState runtimeState, FaceTransformContext context, int currentTick, PawnFaceConfig? faceCfg)
        {
            PawnEyeDirectionConfig? eyeDirCfg = faceCfg?.eyeDirectionConfig;

            // Eye / Pupil / UpperLid / LowerLid（需要 eyeDirectionConfig）
            if (eyeDirCfg != null)
            {
                string eyeKey = FaceTransformEvaluator.ResolveEyeStateKey(context.eyeVariant, context.lidState, context.expression);
                runtimeState.eyeBlend.ForceSet(eyeDirCfg.GetOrBuildEyeProfiles().GetProfileOrDefault(eyeKey));
                runtimeState.lastEyeVariant = context.eyeVariant;

                string pupilKey = FaceTransformEvaluator.ResolvePupilStateKey(context.pupilVariant, context.expression, context.eyeVariant);
                runtimeState.pupilBlend.ForceSet(eyeDirCfg.GetOrBuildPupilProfiles().GetProfileOrDefault(pupilKey));
                runtimeState.lastPupilVariant = context.pupilVariant;

                string upperLidKey = FaceTransformEvaluator.ResolveUpperLidStateKey(context.lidState, context.eyeVariant);
                runtimeState.upperLidBlend.ForceSet(eyeDirCfg.GetOrBuildUpperLidProfiles().GetProfileOrDefault(upperLidKey));
                runtimeState.lastUpperLidState = context.lidState;
                runtimeState.lastUpperLidEyeVariant = context.eyeVariant;

                string lowerLidKey = FaceTransformEvaluator.ResolveLowerLidStateKey(context.lidState);
                runtimeState.lowerLidBlend.ForceSet(eyeDirCfg.GetOrBuildLowerLidProfiles().GetProfileOrDefault(lowerLidKey));
                runtimeState.lastLowerLidState = context.lidState;
            }

            // Brow / Mouth（需要 faceConfig）
            if (faceCfg != null)
            {
                string browKey = FaceTransformEvaluator.ResolveBrowStateKey(context.browState);
                runtimeState.browBlend.ForceSet(faceCfg.GetOrBuildBrowProfiles().GetProfileOrDefault(browKey));
                runtimeState.lastBrowState = context.browState;

                string mouthKey = FaceTransformEvaluator.ResolveMouthStateKey(context.mouthState, context.expression);
                runtimeState.mouthBlend.ForceSet(faceCfg.GetOrBuildMouthProfiles().GetProfileOrDefault(mouthKey));
                runtimeState.lastMouthState = context.mouthState;
                runtimeState.lastMouthExpression = context.expression;
            }
        }

        private void ApplyEvaluatedFaceTransform(
            PawnRenderNode_Custom customNode,
            FaceTransformContext context,
            PawnEyeDirectionConfig.LidMotionConfig lidMotion)
        {
            PawnEyeDirectionConfig.EyeMotionConfig eyeMotion = GetLayeredEyeMotionConfig(customNode);
            PawnEyeDirectionConfig.PupilMotionConfig pupilMotion = GetLayeredPupilMotionConfig(customNode);
            PawnFaceConfig.BrowMotionConfig browMotion = GetBrowMotionConfig(customNode);
            PawnFaceConfig.MouthMotionConfig mouthMotion = GetMouthMotionConfig(customNode);
            PawnFaceConfig.EmotionOverlayMotionConfig emotionOverlayMotion = GetEmotionOverlayMotionConfig(customNode);
            FaceTransformResult result = FaceTransformEvaluator.Evaluate(context, browMotion, mouthMotion, emotionOverlayMotion, lidMotion, eyeMotion, pupilMotion, GetLayeredUpperLidMoveDown(customNode));
            if (result.hidden)
            {
                HideProgrammaticFacePart(customNode);
                return;
            }

            SetProgrammaticFaceTransform(customNode, result.angle, result.offset, result.scale);
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

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            EyeAnimationVariant eyeVariant = skinComp?.GetEffectiveEyeAnimationVariant() ?? EyeAnimationVariant.NeutralOpen;

            float sideSign = GetLayeredFaceSideSign(customNode);
            float offsetX = GetLayeredFaceSideBias(customNode, 0.0002f);
            float offsetZ = 0.0004f * primaryWave;

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

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            EyeAnimationVariant eyeVariant = skinComp?.GetEffectiveEyeAnimationVariant() ?? EyeAnimationVariant.NeutralOpen;
            PupilScaleVariant pupilVariant = skinComp?.GetEffectivePupilScaleVariant() ?? PupilScaleVariant.Neutral;

            float sideSign = GetLayeredFaceSideSign(customNode);
            float offsetX = GetLayeredFaceSideBias(customNode, 0.000028f);
            float offsetZ = slowWave * 0.00005f;
            switch (eyeDirection)
            {
                case EyeDirection.Left:
                    offsetX = -0.00018f;
                    break;
                case EyeDirection.Right:
                    offsetX = 0.00018f;
                    break;
                case EyeDirection.Up:
                    offsetZ -= 0.00014f;
                    break;
                case EyeDirection.Down:
                    offsetZ += 0.00016f;
                    break;
            }

            switch (eyeVariant)
            {
                case EyeAnimationVariant.NeutralSoft:
                    offsetZ += 0.00004f;
                    break;
                case EyeAnimationVariant.NeutralLookDown:
                    offsetZ += 0.00012f;
                    break;
                case EyeAnimationVariant.NeutralGlance:
                    offsetX += (primaryWave > 0f ? 0.00010f : -0.00010f) + sideSign * 0.000045f;
                    break;
                case EyeAnimationVariant.WorkFocusDown:
                    offsetZ += 0.00020f;
                    break;
                case EyeAnimationVariant.WorkFocusUp:
                    offsetZ -= 0.00015f;
                    break;
                case EyeAnimationVariant.HappyOpen:
                    offsetZ -= 0.00003f;
                    break;
                case EyeAnimationVariant.ShockWide:
                    offsetZ -= 0.00012f;
                    break;
                case EyeAnimationVariant.ScaredWide:
                    offsetZ -= 0.00008f;
                    offsetX += primaryWave * 0.00008f + sideSign * 0.00004f;
                    break;
                case EyeAnimationVariant.ScaredFlinch:
                    offsetZ += 0.00008f;
                    offsetX += slowWave * 0.00009f + sideSign * 0.000055f;
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
                new Vector3(offsetX + primaryWave * 0.00004f, 0f, offsetZ),
                new Vector3(scale, 1f, scale));
        }

        private void ApplyProgrammaticLidTransform(
            PawnRenderNode_Custom customNode,
            LayeredFacePartType partType,
            LidState state,
            float primaryWave,
            float slowWave)
        {
            PawnEyeDirectionConfig.LidMotionConfig lidMotion = GetLayeredLidMotionConfig(customNode);

            if (partType == LayeredFacePartType.UpperLid)
            {
                float replacementMoveDown = GetLayeredUpperLidMoveDown(customNode);
                float sideBiasX = GetLayeredFaceSideBias(customNode, lidMotion.upperSideBiasX);
                switch (state)
                {
                    case LidState.Blink:
                    {
                        CompPawnSkin? blinkSkinComp = customNode.GetCachedSkinComp();
                        BlinkPhase blinkPhase = blinkSkinComp?.GetBlinkPhase() ?? BlinkPhase.None;
                        float phaseProgress = blinkSkinComp?.GetBlinkPhaseProgress01() ?? 0f;
                        float animatedMoveDown = blinkPhase switch
                        {
                            BlinkPhase.ClosingLid => Mathf.Lerp(0f, replacementMoveDown, phaseProgress),
                            BlinkPhase.HideBaseEyeParts => replacementMoveDown,
                            BlinkPhase.ShowReplacementEye => replacementMoveDown,
                            BlinkPhase.RestoreBaseEyeParts => replacementMoveDown,
                            BlinkPhase.OpeningLid => Mathf.Lerp(replacementMoveDown, 0f, phaseProgress),
                            _ => 0f,
                        };

                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, animatedMoveDown),
                            new Vector3(lidMotion.upperBlinkScaleX, 1f, lidMotion.upperBlinkScaleZ));
                        break;
                    }

                    case LidState.Close:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, replacementMoveDown),
                            new Vector3(lidMotion.upperCloseScaleX, 1f, lidMotion.upperCloseScaleZ));
                        break;

                    case LidState.Half:
                    {
                        CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
                        EyeAnimationVariant eyeVariant = skinComp?.GetEffectiveEyeAnimationVariant() ?? EyeAnimationVariant.NeutralSoft;

                        float halfOffset = Mathf.Max(0f, replacementMoveDown - lidMotion.upperHalfBaseOffsetSubtract);
                        float halfScale = lidMotion.upperHalfScaleDefault;
                        if (eyeVariant == EyeAnimationVariant.NeutralSoft)
                        {
                            halfOffset += lidMotion.upperHalfNeutralSoftExtraOffset;
                            halfScale = lidMotion.upperHalfScaleNeutralSoft;
                        }
                        else if (eyeVariant == EyeAnimationVariant.NeutralLookDown)
                        {
                            halfOffset += lidMotion.upperHalfLookDownExtraOffset;
                            halfScale = lidMotion.upperHalfScaleLookDown;
                        }
                        else if (eyeVariant == EyeAnimationVariant.ScaredFlinch)
                        {
                            halfOffset += lidMotion.upperHalfScaredExtraOffset;
                            halfScale = lidMotion.upperHalfScaleScared;
                        }

                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, halfOffset + slowWave * lidMotion.upperHalfSlowWaveOffset),
                            new Vector3(lidMotion.upperCloseScaleX, 1f, halfScale));
                        break;
                    }

                    case LidState.Happy:
                    {
                        CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
                        EyeAnimationVariant eyeVariant = skinComp?.GetEffectiveEyeAnimationVariant() ?? EyeAnimationVariant.HappyOpen;
                        if (eyeVariant == EyeAnimationVariant.HappyClosedPeak)
                        {
                            HideProgrammaticFacePart(customNode);
                            return;
                        }

                        float happyOffset = eyeVariant == EyeAnimationVariant.HappySoft ? lidMotion.upperHappySoftOffset : lidMotion.upperHappyOpenOffset;
                        float happyScale = eyeVariant == EyeAnimationVariant.HappySoft ? lidMotion.upperHappySoftScale : lidMotion.upperHappyOpenScale;
                        SetProgrammaticFaceTransform(
                            customNode,
                            lidMotion.upperHappyAngleBase + primaryWave * lidMotion.upperHappyAngleWave,
                            new Vector3(sideBiasX, 0f, happyOffset + slowWave * lidMotion.upperHappySlowWaveOffset),
                            new Vector3(lidMotion.upperHappyScaleX, 1f, happyScale));
                        break;
                    }

                    default:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, slowWave * lidMotion.upperDefaultSlowWaveOffset),
                            Vector3.one);
                        break;
                }

                return;
            }

            if (partType == LayeredFacePartType.LowerLid)
            {
                float sideBiasX = GetLayeredFaceSideBias(customNode, lidMotion.lowerSideBiasX);
                switch (state)
                {
                    case LidState.Blink:
                    {
                        CompPawnSkin? blinkSkinComp = customNode.GetCachedSkinComp();
                        BlinkPhase blinkPhase = blinkSkinComp?.GetBlinkPhase() ?? BlinkPhase.None;
                        float phaseProgress = blinkSkinComp?.GetBlinkPhaseProgress01() ?? 0f;
                        float animatedMoveDown = blinkPhase switch
                        {
                            BlinkPhase.ClosingLid => Mathf.Lerp(0f, lidMotion.lowerBlinkOffset, phaseProgress),
                            BlinkPhase.HideBaseEyeParts => lidMotion.lowerBlinkOffset,
                            BlinkPhase.ShowReplacementEye => lidMotion.lowerBlinkOffset,
                            BlinkPhase.RestoreBaseEyeParts => lidMotion.lowerBlinkOffset,
                            BlinkPhase.OpeningLid => Mathf.Lerp(lidMotion.lowerBlinkOffset, 0f, phaseProgress),
                            _ => 0f,
                        };

                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, animatedMoveDown),
                            new Vector3(lidMotion.lowerBlinkScaleX, 1f, lidMotion.lowerBlinkScaleZ));
                        break;
                    }

                    case LidState.Close:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, lidMotion.lowerCloseOffset),
                            new Vector3(lidMotion.lowerCloseScaleX, 1f, lidMotion.lowerCloseScaleZ));
                        break;

                    case LidState.Half:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, lidMotion.lowerHalfOffset + slowWave * lidMotion.lowerHalfSlowWaveOffset),
                            new Vector3(lidMotion.lowerHalfScaleX, 1f, lidMotion.lowerHalfScaleZ));
                        break;

                    case LidState.Happy:
                        SetProgrammaticFaceTransform(
                            customNode,
                            lidMotion.lowerHappyAngleBase + primaryWave * lidMotion.lowerHappyAngleWave,
                            new Vector3(sideBiasX, 0f, lidMotion.lowerHappyOffset + slowWave * lidMotion.lowerHappySlowWaveOffset),
                            new Vector3(lidMotion.lowerHappyScaleX, 1f, lidMotion.lowerHappyScaleZ));
                        break;

                    default:
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            new Vector3(sideBiasX, 0f, -slowWave * lidMotion.lowerDefaultSlowWaveOffset),
                            Vector3.one);
                        break;
                }

                return;
            }

            switch (state)
            {
                case LidState.Blink:
                {
                    CompPawnSkin? blinkSkinComp = customNode.GetCachedSkinComp();
                    BlinkPhase blinkPhase = blinkSkinComp?.GetBlinkPhase() ?? BlinkPhase.None;
                    float phaseProgress = blinkSkinComp?.GetBlinkPhaseProgress01() ?? 0f;
                    float animatedMoveDown = blinkPhase switch
                    {
                        BlinkPhase.ClosingLid => Mathf.Lerp(0f, lidMotion.genericBlinkOffset, phaseProgress),
                        BlinkPhase.HideBaseEyeParts => lidMotion.genericBlinkOffset,
                        BlinkPhase.ShowReplacementEye => lidMotion.genericBlinkOffset,
                        BlinkPhase.RestoreBaseEyeParts => lidMotion.genericBlinkOffset,
                        BlinkPhase.OpeningLid => Mathf.Lerp(lidMotion.genericBlinkOffset, 0f, phaseProgress),
                        _ => 0f,
                    };

                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, animatedMoveDown),
                        new Vector3(lidMotion.genericBlinkScaleX, 1f, lidMotion.genericBlinkScaleZ));
                    break;
                }

                case LidState.Close:
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, lidMotion.genericCloseOffset),
                        new Vector3(lidMotion.genericCloseScaleX, 1f, lidMotion.genericCloseScaleZ));
                    break;

                case LidState.Half:
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, lidMotion.genericHalfOffset + slowWave * lidMotion.genericHalfSlowWaveOffset),
                        new Vector3(lidMotion.genericHalfScaleX, 1f, lidMotion.genericHalfScaleZ));
                    break;

                case LidState.Happy:
                    SetProgrammaticFaceTransform(
                        customNode,
                        lidMotion.genericHappyAngleBase + primaryWave * lidMotion.genericHappyAngleWave,
                        new Vector3(0f, 0f, lidMotion.genericHappyOffset + slowWave * lidMotion.genericHappySlowWaveOffset),
                        new Vector3(lidMotion.genericHappyScaleX, 1f, lidMotion.genericHappyScaleZ));
                    break;

                default:
                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        new Vector3(0f, 0f, slowWave * lidMotion.genericDefaultSlowWaveOffset),
                        new Vector3(1f, 1f, lidMotion.genericDefaultScaleZBase + Mathf.Abs(primaryWave) * lidMotion.genericDefaultScaleZWaveAmplitude));
                    break;
            }
        }

        private PawnEyeDirectionConfig.LidMotionConfig GetLayeredLidMotionConfig(PawnRenderNode_Custom customNode)
        {
            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            return skinComp?.ActiveSkin?.faceConfig?.eyeDirectionConfig?.lidMotion
                ?? new PawnEyeDirectionConfig.LidMotionConfig();
        }

        private PawnEyeDirectionConfig.EyeMotionConfig GetLayeredEyeMotionConfig(PawnRenderNode_Custom customNode)
        {
            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            return skinComp?.ActiveSkin?.faceConfig?.eyeDirectionConfig?.eyeMotion
                ?? new PawnEyeDirectionConfig.EyeMotionConfig();
        }

        private PawnEyeDirectionConfig.PupilMotionConfig GetLayeredPupilMotionConfig(PawnRenderNode_Custom customNode)
        {
            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            return skinComp?.ActiveSkin?.faceConfig?.eyeDirectionConfig?.pupilMotion
                ?? new PawnEyeDirectionConfig.PupilMotionConfig();
        }

        private PawnFaceConfig.BrowMotionConfig GetBrowMotionConfig(PawnRenderNode_Custom customNode)
        {
            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            return skinComp?.ActiveSkin?.faceConfig?.browMotion
                ?? new PawnFaceConfig.BrowMotionConfig();
        }

        private PawnFaceConfig.MouthMotionConfig GetMouthMotionConfig(PawnRenderNode_Custom customNode)
        {
            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            return skinComp?.ActiveSkin?.faceConfig?.mouthMotion
                ?? new PawnFaceConfig.MouthMotionConfig();
        }

        private PawnFaceConfig.EmotionOverlayMotionConfig GetEmotionOverlayMotionConfig(PawnRenderNode_Custom customNode)
        {
            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            return skinComp?.ActiveSkin?.faceConfig?.emotionOverlayMotion
                ?? new PawnFaceConfig.EmotionOverlayMotionConfig();
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
            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            const float defaultUpperLidMoveDown = 0.008f;

            float configuredMoveDown = Mathf.Max(0f, skinComp?.ActiveSkin?.faceConfig?.eyeDirectionConfig?.upperLidMoveDown ?? 0f);

            FaceEyeDirectionRuntimeData? eyeData = skinComp?.CurrentFaceRuntimeCompiledData?.portraitTrack?.eyeDirection;
            if (eyeData?.enabled == true)
                return Mathf.Max(defaultUpperLidMoveDown, configuredMoveDown, eyeData.upperLidMoveDown);

            return configuredMoveDown > 0f ? configuredMoveDown : defaultUpperLidMoveDown;
        }

        private bool ShouldHideLayeredEyePartForReplacement(PawnRenderNode_Custom customNode, Pawn pawn, CompPawnSkin skinComp, ExpressionType expression)
        {
            LayeredFacePartType? partType = customNode.layeredFacePartType;
            if (!partType.HasValue)
                return false;

            switch (partType.Value)
            {
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.Sclera:
                case LayeredFacePartType.Pupil:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                    break;
                default:
                    return false;
            }

            if (!HasActiveReplacementEye(customNode, pawn, skinComp))
                return false;

            LayeredFacePartSide activeReplacementSide = GetActiveReplacementEyeSide(customNode, skinComp, expression);
            if (activeReplacementSide != LayeredFacePartSide.None
                && customNode.layeredFacePartSide != LayeredFacePartSide.None
                && customNode.layeredFacePartSide != activeReplacementSide)
            {
                return false;
            }

            if (expression == ExpressionType.Blink)
                return true;

            if (skinComp.IsBlinkActive())
            {
                BlinkPhase blinkPhase = skinComp.GetBlinkPhase();
                return blinkPhase == BlinkPhase.HideBaseEyeParts
                    || blinkPhase == BlinkPhase.ShowReplacementEye
                    || blinkPhase == BlinkPhase.RestoreBaseEyeParts;
            }

            if (expression == ExpressionType.Dead)
                return true;

            return true;
        }

        private static LayeredFacePartSide GetActiveReplacementEyeSide(
            PawnRenderNode_Custom customNode,
            CompPawnSkin skinComp,
            ExpressionType expression)
        {
            if (expression == ExpressionType.Wink)
            {
                LayeredFacePartSide winkSide = skinComp.GetEffectiveWinkSide();
                if (winkSide == LayeredFacePartSide.Left || winkSide == LayeredFacePartSide.Right)
                    return winkSide;
            }

            if (customNode.layeredFacePartType == LayeredFacePartType.ReplacementEye)
                return customNode.layeredFacePartSide;

            return LayeredFacePartSide.None;
        }

        private bool ShouldHideUpperLidAtBlinkEndpoint(PawnRenderNode_Custom customNode, CompPawnSkin skinComp, ExpressionType expression)
        {
            if (customNode.layeredFacePartType != LayeredFacePartType.UpperLid)
                return false;

            Pawn? pawn = customNode.tree?.pawn;
            if (pawn == null || !HasActiveReplacementEye(customNode, pawn, skinComp))
                return false;

            if (expression == ExpressionType.Blink)
                return false;

            if (!skinComp.IsBlinkActive())
                return false;

            BlinkPhase blinkPhase = skinComp.GetBlinkPhase();
            return blinkPhase == BlinkPhase.ShowReplacementEye;
        }

        private bool HasActiveReplacementEye(PawnRenderNode_Custom customNode, Pawn pawn, CompPawnSkin skinComp)
        {
            if (skinComp.GetEffectiveExpression() == ExpressionType.Neutral && !skinComp.IsBlinkActive())
                return false;

            PawnFaceConfig? faceConfig = skinComp.ActiveSkin?.faceConfig;
            if (faceConfig == null || faceConfig.workflowMode != FaceWorkflowMode.LayeredDynamic)
                return false;

            return HasReplacementEyeTextureForCurrentState(faceConfig, skinComp);
        }

        private bool HasReplacementEyeTextureForCurrentState(PawnFaceConfig faceConfig, CompPawnSkin skinComp)
        {
            if (faceConfig.CountLayeredParts(LayeredFacePartType.ReplacementEye) == 0)
                return false;

            LayeredFacePartSide preferredSide = skinComp.GetEffectiveExpression() == ExpressionType.Wink
                ? skinComp.GetEffectiveWinkSide()
                : LayeredFacePartSide.None;

            foreach (ExpressionType candidate in EnumerateReplacementEyeExpressions(skinComp))
            {
                if (HasReplacementEyeTexture(faceConfig, candidate, preferredSide))
                    return true;
            }

            return false;
        }

        private bool HasReplacementMouthTextureForCurrentState(PawnFaceConfig faceConfig, CompPawnSkin skinComp)
        {
            return !string.IsNullOrWhiteSpace(ResolveReplacementMouthPath(faceConfig, skinComp, Rot4.South));
        }

        private static IEnumerable<ExpressionType> EnumerateReplacementEyeExpressions(CompPawnSkin skinComp)
        {
            if (skinComp.IsBlinkActive())
            {
                yield return ExpressionType.Blink;
                yield break;
            }

            ExpressionType expression = skinComp.GetEffectiveExpression();
            yield return expression;

            if (expression != ExpressionType.Neutral)
                yield return ExpressionType.Neutral;
        }

        private static IEnumerable<ExpressionType> EnumerateReplacementMouthExpressions(CompPawnSkin skinComp)
        {
            ExpressionType expression = skinComp.GetEffectiveExpression();
            yield return expression;

            if (expression != ExpressionType.Neutral)
                yield return ExpressionType.Neutral;
        }

        private static string? ResolveReplacementMouthPath(PawnFaceConfig faceConfig, CompPawnSkin skinComp, Rot4 facing)
        {
            if (faceConfig.CountLayeredParts(LayeredFacePartType.ReplacementMouth) == 0)
                return null;

            foreach (ExpressionType candidate in EnumerateReplacementMouthExpressions(skinComp))
            {
                string directPath = faceConfig.GetLayeredDirectionalPartPath(LayeredFacePartType.ReplacementMouth, candidate, facing);
                if (!string.IsNullOrWhiteSpace(directPath))
                    return directPath;

                LayeredFacePartConfig? config = faceConfig.GetLayeredPartConfig(LayeredFacePartType.ReplacementMouth, candidate);
                if (config != null && config.enabled && config.HasAnyTexture())
                    return config.GetDirectionalTexPath(facing);
            }

            return null;
        }

        private static bool HasReplacementEyeTexture(PawnFaceConfig faceConfig, ExpressionType expression, LayeredFacePartSide preferredSide)
        {
            foreach (LayeredFacePartSide side in EnumerateReplacementEyeSides(preferredSide))
            {
                string directPath = faceConfig.GetLayeredDirectionalPartPath(LayeredFacePartType.ReplacementEye, expression, side, Rot4.South);
                if (!string.IsNullOrWhiteSpace(directPath))
                    return true;

                LayeredFacePartConfig? config = faceConfig.GetLayeredPartConfig(LayeredFacePartType.ReplacementEye, expression, side);
                if (config != null && config.enabled && config.HasAnyTexture())
                    return true;
            }

            return false;
        }

        private static IEnumerable<LayeredFacePartSide> EnumerateReplacementEyeSides(LayeredFacePartSide preferredSide)
        {
            if (preferredSide != LayeredFacePartSide.None)
                yield return preferredSide;

            yield return LayeredFacePartSide.None;

            if (preferredSide != LayeredFacePartSide.Left)
                yield return LayeredFacePartSide.Left;

            if (preferredSide != LayeredFacePartSide.Right)
                yield return LayeredFacePartSide.Right;
        }

        private void ApplyProgrammaticHairTransform(
            PawnRenderNode_Custom customNode,
            CompPawnSkin skinComp,
            float primaryWave,
            float slowWave)
        {
            SetProgrammaticFaceTransform(
                customNode,
                0f,
                Vector3.zero,
                Vector3.one);
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
                case LayeredFacePartType.ReplacementEye:
                {
                    Pawn? pawn = customNode.tree?.pawn;
                    CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
                    if (pawn == null || skinComp == null || !HasActiveReplacementEye(customNode, pawn, skinComp))
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    if (expression == ExpressionType.Blink)
                    {
                        SetProgrammaticFaceTransform(
                            customNode,
                            0f,
                            Vector3.zero,
                            Vector3.one);
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
                case LayeredFacePartType.OverlayTop:
                {
                    Pawn? pawn = customNode.tree?.pawn;
                    CompPawnSkin? skinComp = customNode.GetCachedSkinComp();

                    if (pawn != null && skinComp != null)
                    {
                        string overlayId = GetEffectiveLayeredOverlayId(customNode, pawn);
                        if (!string.IsNullOrWhiteSpace(overlayId) && !IsOverlayActiveForCurrentSemantic(customNode, pawn, skinComp))
                        {
                            HideProgrammaticFacePart(customNode);
                            return;
                        }

                        string followTarget = ResolveOverlayFollowTarget(skinComp.ActiveSkin?.faceConfig, overlayId);
                        switch (followTarget)
                        {
                            case "Pupil":
                            {
                                if (TryGetFollowSourceTransform(customNode, pawn, LayeredFacePartType.Pupil, out float pupilAngle, out Vector3 pupilOffset))
                                {
                                    SetProgrammaticFaceTransform(customNode, pupilAngle, pupilOffset, Vector3.one);
                                    return;
                                }
                                return;
                            }
                            case "Face":
                            {
                                if (TryGetFollowSourceTransform(customNode, pawn, LayeredFacePartType.Eye, out _, out Vector3 eyeOffset))
                                {
                                    SetProgrammaticFaceTransform(customNode, 0f, eyeOffset, Vector3.one);
                                    return;
                                }
                                return;
                            }
                        }
                    }

                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        Vector3.zero,
                        Vector3.one);
                    return;
                }

                case LayeredFacePartType.ReplacementMouth:
                {
                    Pawn? pawn = customNode.tree?.pawn;
                    CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
                    PawnFaceConfig? faceConfig = skinComp?.ActiveSkin?.faceConfig;
                    if (pawn == null || skinComp == null || faceConfig == null || !HasReplacementMouthTextureForCurrentState(faceConfig, skinComp))
                    {
                        HideProgrammaticFacePart(customNode);
                        return;
                    }

                    SetProgrammaticFaceTransform(
                        customNode,
                        0f,
                        Vector3.zero,
                        Vector3.one);
                    return;
                }
            }
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

        private float GetLayeredFacePartMotionAmplitude(PawnRenderNode_Custom customNode, Pawn? pawn)
        {
            if (!customNode.layeredFacePartType.HasValue || pawn == null)
                return 0f;

            CompPawnSkin? skinComp = customNode.GetCachedSkinComp();
            FaceRuntimeCompiledData? compiledData = skinComp?.CurrentFaceRuntimeCompiledData;
            PawnFaceConfig? faceConfig = skinComp?.ActiveSkin?.faceConfig;
            List<LayeredFacePartConfig>? layeredParts = faceConfig?.layeredParts;

            LayeredFacePartType partType = customNode.layeredFacePartType.Value;
            LayeredFacePartSide side = PawnFaceConfig.NormalizePartSide(partType, customNode.layeredFacePartSide);
            ExpressionType expression = GetCurrentExpressionForPawn(pawn, skinComp);
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
                return exact.motionAmplitude;
            }

            LayeredFacePartConfig? neutral = isOverlay
                ? resolvedFaceConfig.GetLayeredPartConfig(partType, ExpressionType.Neutral, overlayId)
                : resolvedFaceConfig.GetLayeredPartConfig(partType, ExpressionType.Neutral, side);

            if (neutral != null)
            {
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
            if (customNode.layeredFacePartType == LayeredFacePartType.Pupil)
                return baseOffset;

            float motionAmplitude = GetLayeredFacePartMotionAmplitude(customNode, customNode.tree?.pawn);
            if (motionAmplitude <= 0f)
                return baseOffset;

            return new Vector3(
                baseOffset.x + motionAmplitude * primaryWave,
                baseOffset.y,
                baseOffset.z + motionAmplitude * slowWave);
        }
    }
}