﻿using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 皮肤组件
    /// 附加到 Pawn 上，处理皮肤应用和动态表情逻辑
    /// </summary>
    public class CompPawnSkin : ThingComp
    {
        private PawnSkinDef? activeSkin;
        private string? activeSkinDefName;
        private bool needsRefresh = false;
        private bool activeSkinFromDefaultRaceBinding = false;

        // 双轨面部运行时状态（第一阶段：先接入状态层，不直接改动渲染 worker）
        private FaceRuntimeState? faceRuntimeState = null;
        private FaceRuntimeCompiledData? faceRuntimeCompiledData = null;

        // 表情状态
        public ExpressionType curExpression = ExpressionType.Neutral;
        private ExpressionType? previewExpressionOverride = null;
        private int blinkTimer = 0;
        private const int BlinkDuration = 10;

        // 帧动画 Tick 计数器（每 Tick +1，用于多帧表情序列定位）
        private int expressionAnimTick = 0;

        // 眼睛注视方向
        public EyeDirection curEyeDirection = EyeDirection.Center;
        /// <summary>编辑器预览强制覆盖方向（null = 自动）</summary>
        private EyeDirection? previewEyeDirectionOverride = null;

        // 统一表情图层的通道状态预览覆盖
        private MouthState? previewMouthStateOverride = null;
        private LidState? previewLidStateOverride = null;
        private BrowState? previewBrowStateOverride = null;
        private EmotionOverlayState? previewEmotionOverlayStateOverride = null;

        // Q 键四段轮换模式索引（0..3）
        public int qHotkeyModeIndex = 0;

        // Q->W 连段窗口（单位：Tick）
        public int qComboWindowEndTick = 0;

        // 各槽位技能 CD（单位：Tick）
        // E 槽由 AbilityHotkeyRuntimeComponent 写入，Q/W/R 由统一门控写入
        public int qCooldownUntilTick = 0;
        public int wCooldownUntilTick = 0;
        public int eCooldownUntilTick = 0;
        public int rCooldownUntilTick = 0;

        // R 两段机制状态
        public bool rStackingEnabled = false;
        public int rStackCount = 0;
        public bool rSecondStageReady = false;
        public int rSecondStageExecuteTick = -1;
        public bool rSecondStageHasTarget = false;
        public IntVec3 rSecondStageTargetCell = IntVec3.Invalid;

        // 状态武器视觉：施法中窗口
        public int weaponCarryCastingUntilTick = -1;

        public PawnSkinDef? ActiveSkin
        {
            get => activeSkin;
            set
            {
                if (activeSkin != value)
                {
                    activeSkin = value;
                    activeSkinDefName = value?.defName;
                    needsRefresh = true;
                    RequestRenderRefresh();
                    SyncXenotype(value);
                    MarkFaceRuntimeDirty();
                }
            }
        }

        /// <summary>
        /// 静默赋值：仅更新数据，不触发 RequestRenderRefresh()。
        /// 供 PawnSkinRuntimeUtility 使用，避免与随后的 RefreshHiddenNodes /
        /// ForceRebuildRenderTree 产生多次冗余重绘。
        /// </summary>
        internal void SetActiveSkinSilent(PawnSkinDef? skin)
        {
            activeSkin        = skin;
            activeSkinDefName = skin?.defName;
            needsRefresh      = false; // 调用方负责触发刷新
            MarkFaceRuntimeDirty();
        }

        public bool HasActiveSkin => activeSkin != null;
        public bool ActiveSkinFromDefaultRaceBinding => activeSkinFromDefaultRaceBinding;

        internal void SetActiveSkinSource(bool fromDefaultRaceBinding)
        {
            activeSkinFromDefaultRaceBinding = fromDefaultRaceBinding;
        }

        internal void SetActiveSkinWithSource(PawnSkinDef? skin, bool fromDefaultRaceBinding)
        {
            SetActiveSkinSilent(skin);
            activeSkinFromDefaultRaceBinding = fromDefaultRaceBinding;
        }

        // ─────────────────────────────────────────────
        // Xenotype 同步
        // ─────────────────────────────────────────────

        /// <summary>
        /// 应用皮肤时，将 pawn 的 Xenotype 同步到皮肤绑定的 XenotypeDef。
        /// xenotypeDefName 为空时静默跳过，保持向后兼容。
        /// </summary>
        private void SyncXenotype(PawnSkinDef? skin)
        {
            if (skin == null || string.IsNullOrEmpty(skin.xenotypeDefName))
                return;

            var pawn = Pawn;
            if (pawn == null || pawn.genes == null)
                return;

            // 避免在 spawning 阶段之前执行（地图尚未初始化时 Spawned 为 false）
            if (!pawn.Spawned)
                return;

            var xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(skin.xenotypeDefName);
            if (xenotype == null)
            {
                Log.Warning($"[CharacterStudio] SyncXenotype: XenotypeDef '{skin.xenotypeDefName}' not found for skin '{skin.defName}'.");
                return;
            }

            pawn.genes.SetXenotype(xenotype);
        }

        public Pawn? Pawn => parent as Pawn;

        /// <summary>
        /// 当前 Pawn 的双轨运行时状态。
        /// 第一阶段先作为状态同步与后续渲染接入的统一入口。
        /// </summary>
        public FaceRuntimeState CurrentFaceRuntimeState => faceRuntimeState ??= new FaceRuntimeState();

        /// <summary>
        /// 当前 Pawn 的面部编译缓存。
        /// 由 Runtime Compiler 按皮肤内容签名构建并缓存。
        /// </summary>
        public FaceRuntimeCompiledData CurrentFaceRuntimeCompiledData
            => faceRuntimeCompiledData ??= FaceRuntimeCompiler.GetOrBuild(activeSkin);

        private void MarkFaceRuntimeDirty()
        {
            faceRuntimeCompiledData = null;

            if (faceRuntimeState == null)
                faceRuntimeState = new FaceRuntimeState();
            else
                faceRuntimeState.MarkAllDirty();
        }

        private void EnsureFaceRuntimeStateUpdated()
        {
            if (Pawn == null || activeSkin?.faceConfig?.enabled != true)
                return;

            var runtimeState = CurrentFaceRuntimeState;
            var compiledData = CurrentFaceRuntimeCompiledData;
            int currentTick = Find.TickManager?.TicksGame ?? 0;

            bool shouldUpdateTrackAndLod = runtimeState.trackDirty
                || runtimeState.lodDirty
                || runtimeState.compiledDataDirty;

            if (runtimeState.currentTrack == FaceRenderTrack.Portrait)
                shouldUpdateTrackAndLod |= currentTick >= runtimeState.nextPortraitUpdateTick;
            else
                shouldUpdateTrackAndLod |= currentTick >= runtimeState.nextWorldUpdateTick;

            if (shouldUpdateTrackAndLod)
            {
                FaceRuntimePolicy.UpdateRuntimeState(Pawn, this, runtimeState, compiledData, currentTick);

                if (runtimeState.trackDirty || runtimeState.lodDirty)
                    RequestRenderRefresh();

                runtimeState.trackDirty = false;
                runtimeState.lodDirty = false;
                runtimeState.compiledDataDirty = false;
            }

            ExpressionType effectiveExpression = GetEffectiveExpression();
            EyeDirection effectiveEyeDirection = CurEyeDirection;
            MouthState effectiveMouthState = GetEffectiveMouthState();
            LidState effectiveLidState = GetEffectiveLidState();
            BrowState effectiveBrowState = GetEffectiveBrowState();
            EmotionOverlayState effectiveEmotionOverlayState = GetEffectiveEmotionOverlayState();

            runtimeState.expressionDirty =
                runtimeState.currentExpression != effectiveExpression
                || runtimeState.currentEyeDirection != effectiveEyeDirection
                || runtimeState.currentMouthState != effectiveMouthState
                || runtimeState.currentLidState != effectiveLidState
                || runtimeState.currentBrowState != effectiveBrowState
                || runtimeState.currentEmotionOverlayState != effectiveEmotionOverlayState;

            runtimeState.currentExpression = effectiveExpression;
            runtimeState.currentEyeDirection = effectiveEyeDirection;
            runtimeState.currentMouthState = effectiveMouthState;
            runtimeState.currentLidState = effectiveLidState;
            runtimeState.currentBrowState = effectiveBrowState;
            runtimeState.currentEmotionOverlayState = effectiveEmotionOverlayState;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!string.IsNullOrEmpty(activeSkinDefName) && activeSkin == null)
            {
                activeSkin = DefDatabase<PawnSkinDef>.GetNamedSilentFail(activeSkinDefName);
            }
            if (activeSkin == null)
                TryApplyDefaultRaceSkinIfNeeded();

            if (activeSkin != null)
            {
                RequestRenderRefresh();
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Pawn == null || !Pawn.Spawned) return;

            if (needsRefresh)
            {
                RequestRenderRefresh();
                needsRefresh = false;
            }

            if (activeSkin?.faceConfig?.enabled == true)
            {
                EnsureFaceRuntimeStateUpdated();

                // 帧动画 Tick 累加（驱动多帧表情序列）
                expressionAnimTick++;

                // 每 30 Tick (约 0.5s) 更新一次表情状态
                if (Pawn.IsHashIntervalTick(30))
                    UpdateExpressionState();

                // 帧动画帧推进：检测当前帧是否应切换到下一帧
                UpdateAnimatedExpressionFrame();

                // 眨眼逻辑
                UpdateBlinkLogic();

                // 眼睛方向推断（仅当配置启用时）
                if (activeSkin?.faceConfig?.eyeDirectionConfig?.enabled == true)
                {
                    if (Pawn.IsHashIntervalTick(15))
                        UpdateEyeDirectionState();
                }
            }
        }

        private void UpdateExpressionState()
        {
            if (activeSkin?.faceConfig?.enabled != true) return;

            var oldExp = curExpression;
            var pawn   = Pawn!;
            var job    = pawn.CurJob?.def;

            if (pawn.Dead)
            {
                curExpression = ExpressionType.Dead;
            }
            else if (pawn.Downed)
            {
                curExpression = ExpressionType.Pain;
            }
            else if (RestUtility.InBed(pawn))
            {
                curExpression = ExpressionType.Sleeping;
            }
            // ── Job 驱动（对应 NL FA ForJobs）──
            else if (job == JobDefOf.Ingest)
            {
                curExpression = ExpressionType.Eating;
            }
            else if (job == JobDefOf.Lovin)
            {
                curExpression = ExpressionType.Lovin;
            }
            else if (job != null && (
                job.defName == "LayDown"
                || job.defName.IndexOf("LayDown", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.LayDown;
            }
            else if (job != null && (
                job == JobDefOf.Strip
                || job.defName.IndexOf("Strip", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.Strip;
            }
            else if (job != null && (
                job == JobDefOf.HaulToCell
                || job == JobDefOf.HaulToContainer
                || job.defName.IndexOf("Haul", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Carry", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.Hauling;
            }
            else if (job != null && (
                job.defName.IndexOf("Read", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Study", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.Reading;
            }
            else if (job != null && (
                job.defName.IndexOf("SocialRelax", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Chat", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.SocialRelax;
            }
            else if (job != null && (
                job == JobDefOf.DoBill
                || job.defName.IndexOf("DoBill", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Craft", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Cook", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Smelt", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Repair", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Build", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Sow", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Harvest", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Clean", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Train", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Tend", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.Working;
            }
            else if (job != null && (
                job.defName.IndexOf("WaitCombat", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.WaitCombat;
            }
            else if (job != null && (
                job.defName.IndexOf("AttackMelee", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.AttackMelee;
            }
            else if (job != null && (
                job.defName.IndexOf("AttackStatic", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Shoot", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Burst", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.AttackRanged;
            }
            // ── 精神状态（Mental State）──
            else if (pawn.InMentalState)
            {
                var stateDef = pawn.MentalStateDef;
                bool isPanic = stateDef != null &&
                    (stateDef.defName.IndexOf("Flee", StringComparison.OrdinalIgnoreCase) >= 0
                    || stateDef.defName.IndexOf("Panic", StringComparison.OrdinalIgnoreCase) >= 0
                    || stateDef.defName.IndexOf("WildMan", StringComparison.OrdinalIgnoreCase) >= 0);
                curExpression = isPanic ? ExpressionType.Scared : ExpressionType.Angry;
            }
            // ── 情绪/生理 ──
            else
            {
                float rest = pawn.needs?.rest?.CurLevel ?? 1f;
                float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;

                if (rest < 0.15f)
                    curExpression = ExpressionType.Tired;
                else if (mood < 0.1f)
                    curExpression = ExpressionType.Hopeless;
                else if (mood < 0.2f)
                    curExpression = ExpressionType.Sad;
                else if (mood < 0.4f)
                    curExpression = ExpressionType.Gloomy;
                else if (mood > 0.9f)
                    curExpression = ExpressionType.Cheerful;
                else if (mood > 0.8f)
                    curExpression = ExpressionType.Happy;
                else
                    curExpression = ExpressionType.Neutral;
            }

            if (oldExp != curExpression)
                RequestRenderRefresh();
        }

        /// <summary>
        /// 检测当前帧动画是否应触发渲染更新（贴图切换时刷新）
        /// </summary>
        private void UpdateAnimatedExpressionFrame()
        {
            if (previewExpressionOverride.HasValue) return;
            var faceConfig = activeSkin?.faceConfig;
            if (faceConfig == null) return;

            var expEntry = faceConfig.GetExpression(curExpression);
            if (expEntry == null || !expEntry.IsAnimated) return;

            // 当前帧将在下一个 durationTick 边界切换，提前通知渲染系统
            // 计算帧边界时刻，只在边界 Tick 触发刷新，避免每帧刷新
            int totalDuration = 0;
            foreach (var f in expEntry.frames)
                totalDuration += f.durationTicks > 0 ? f.durationTicks : 1;

            if (totalDuration > 0 && expressionAnimTick % totalDuration == 0)
                RequestRenderRefresh();
        }

        private void UpdateBlinkLogic()
        {
            if (previewExpressionOverride.HasValue) return;
            if (curExpression == ExpressionType.Sleeping || curExpression == ExpressionType.Dead) return;

            if (blinkTimer > 0)
            {
                blinkTimer--;
                if (blinkTimer == 0) RequestRenderRefresh();
            }
            else
            {
                // 优化：眨眼概率检测由每 Tick 降低为每 10 Tick（约 0.17s）一次，
                // 调整概率保持等效眨眼频率（0.008 * 60 ≈ 0.08/s → 0.08 * 10 = 约0.08s触发间隔不变）
                if (Pawn!.IsHashIntervalTick(10) && Rand.Value < 0.08f)
                {
                    blinkTimer = BlinkDuration;
                    RequestRenderRefresh();
                }
            }
        }

        public void SetPreviewExpressionOverride(ExpressionType? expression)
        {
            bool changed = previewExpressionOverride != expression;
            previewExpressionOverride = expression;

            if (expression.HasValue)
            {
                blinkTimer = 0;
            }

            if (changed)
            {
                RequestRenderRefresh();
            }
        }

        public ExpressionType GetEffectiveExpression()
        {
            if (previewExpressionOverride.HasValue)
                return previewExpressionOverride.Value;
            if (blinkTimer > 0)
                return ExpressionType.Blink;
            return curExpression;
        }

        /// <summary>当前是否处于 Blink 有效态（包含预览覆盖）</summary>
        public bool IsBlinkActive() => GetEffectiveExpression() == ExpressionType.Blink;

        public void SetPreviewMouthState(MouthState? state)
        {
            bool changed = previewMouthStateOverride != state;
            previewMouthStateOverride = state;
            if (changed)
                RequestRenderRefresh();
        }

        public void SetPreviewLidState(LidState? state)
        {
            bool changed = previewLidStateOverride != state;
            previewLidStateOverride = state;
            if (changed)
                RequestRenderRefresh();
        }

        public void SetPreviewBrowState(BrowState? state)
        {
            bool changed = previewBrowStateOverride != state;
            previewBrowStateOverride = state;
            if (changed)
                RequestRenderRefresh();
        }

        public void SetPreviewEmotionOverlayState(EmotionOverlayState? state)
        {
            bool changed = previewEmotionOverlayStateOverride != state;
            previewEmotionOverlayStateOverride = state;
            if (changed)
                RequestRenderRefresh();
        }

        public void ClearPreviewChannelOverrides()
        {
            bool changed = previewMouthStateOverride.HasValue
                || previewLidStateOverride.HasValue
                || previewBrowStateOverride.HasValue
                || previewEmotionOverlayStateOverride.HasValue;

            previewMouthStateOverride = null;
            previewLidStateOverride = null;
            previewBrowStateOverride = null;
            previewEmotionOverlayStateOverride = null;

            if (changed)
                RequestRenderRefresh();
        }

        public MouthState GetEffectiveMouthState()
        {
            if (previewMouthStateOverride.HasValue)
                return previewMouthStateOverride.Value;

            return ResolveMouthState(GetEffectiveExpression());
        }

        public LidState GetEffectiveLidState()
        {
            if (previewLidStateOverride.HasValue)
                return previewLidStateOverride.Value;

            return ResolveLidState(GetEffectiveExpression());
        }

        public BrowState GetEffectiveBrowState()
        {
            if (previewBrowStateOverride.HasValue)
                return previewBrowStateOverride.Value;

            return ResolveBrowState(GetEffectiveExpression());
        }

        public EmotionOverlayState GetEffectiveEmotionOverlayState()
        {
            if (previewEmotionOverlayStateOverride.HasValue)
                return previewEmotionOverlayStateOverride.Value;

            return ResolveEmotionOverlayState(GetEffectiveExpression());
        }

        /// <summary>
        /// 统一图层系统按 LayerRole 获取当前推荐状态后缀。
        /// 返回 null 表示该角色当前没有可用状态后缀。
        /// </summary>
        public string? GetChannelStateSuffix(LayerRole role)
        {
            switch (role)
            {
                case LayerRole.Mouth:
                    return GetEffectiveMouthState().ToString();

                case LayerRole.Lid:
                    return GetEffectiveLidState().ToString();

                case LayerRole.Brow:
                    return GetEffectiveBrowState().ToString();

                case LayerRole.Emotion:
                    EmotionOverlayState emotionState = GetEffectiveEmotionOverlayState();
                    return emotionState == EmotionOverlayState.None ? null : emotionState.ToString();

                case LayerRole.Eye:
                    return CurEyeDirection.ToString();

                default:
                    return null;
            }
        }

        private MouthState ResolveMouthState(ExpressionType expression)
        {
            switch (expression)
            {
                case ExpressionType.Happy:
                case ExpressionType.Cheerful:
                case ExpressionType.Lovin:
                case ExpressionType.SocialRelax:
                    return MouthState.Smile;

                case ExpressionType.Eating:
                case ExpressionType.AttackMelee:
                case ExpressionType.AttackRanged:
                case ExpressionType.Scared:
                    return MouthState.Open;

                case ExpressionType.Gloomy:
                case ExpressionType.Sad:
                case ExpressionType.Hopeless:
                case ExpressionType.Pain:
                case ExpressionType.Tired:
                    return MouthState.Down;

                case ExpressionType.Sleeping:
                case ExpressionType.LayDown:
                case ExpressionType.Dead:
                    return MouthState.Sleep;

                default:
                    return MouthState.Normal;
            }
        }

        private LidState ResolveLidState(ExpressionType expression)
        {
            switch (expression)
            {
                case ExpressionType.Blink:
                    return LidState.Blink;

                case ExpressionType.Sleeping:
                case ExpressionType.Dead:
                    return LidState.Close;

                case ExpressionType.Tired:
                case ExpressionType.Gloomy:
                case ExpressionType.Sad:
                case ExpressionType.Hopeless:
                case ExpressionType.Pain:
                case ExpressionType.LayDown:
                    return LidState.Half;

                case ExpressionType.Happy:
                case ExpressionType.Cheerful:
                case ExpressionType.Lovin:
                    return LidState.Happy;

                default:
                    return LidState.Normal;
            }
        }

        private BrowState ResolveBrowState(ExpressionType expression)
        {
            switch (expression)
            {
                case ExpressionType.Angry:
                case ExpressionType.WaitCombat:
                case ExpressionType.AttackMelee:
                case ExpressionType.AttackRanged:
                    return BrowState.Angry;

                case ExpressionType.Gloomy:
                case ExpressionType.Sad:
                case ExpressionType.Hopeless:
                case ExpressionType.Tired:
                case ExpressionType.Pain:
                case ExpressionType.Dead:
                    return BrowState.Sad;

                case ExpressionType.Happy:
                case ExpressionType.Cheerful:
                case ExpressionType.Lovin:
                    return BrowState.Happy;

                default:
                    return BrowState.Normal;
            }
        }

        private EmotionOverlayState ResolveEmotionOverlayState(ExpressionType expression)
        {
            switch (expression)
            {
                case ExpressionType.Lovin:
                    return EmotionOverlayState.Lovin;

                case ExpressionType.Gloomy:
                case ExpressionType.Sad:
                case ExpressionType.Hopeless:
                    return EmotionOverlayState.Gloomy;

                default:
                    return EmotionOverlayState.None;
            }
        }

        public void SetWeaponCarryCastingWindow(int durationTicks)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int resolvedDuration = Math.Max(1, durationTicks);
            weaponCarryCastingUntilTick = now + resolvedDuration;
            RequestRenderRefresh();
        }

        public bool IsWeaponCarryCastingNow()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            return weaponCarryCastingUntilTick >= now;
        }

        /// <summary>获取当前帧动画 Tick（供 FaceComponent 渲染时定位帧）</summary>
        public int GetExpressionAnimTick() => expressionAnimTick;

        // ─────────────────────────────────────────────
        // 眼睛方向 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 获取当前有效的眼睛方向。
        /// 若编辑器设置了预览覆盖，优先返回覆盖值；否则返回运行时推断值。
        /// </summary>
        public EyeDirection CurEyeDirection
        {
            get => previewEyeDirectionOverride ?? curEyeDirection;
        }

        /// <summary>设置编辑器预览方向覆盖（null = 取消覆盖，恢复自动）</summary>
        public void SetPreviewEyeDirection(EyeDirection? dir)
        {
            previewEyeDirectionOverride = dir;
            RequestRenderRefresh();
        }

        /// <summary>
        /// 根据 Pawn 当前状态推断眼睛注视方向，并在发生变化时触发渲染刷新。
        /// 推断规则（优先级从高到低）：
        ///   1. 死亡/倒地/睡眠 → Center
        ///   2. 有 Job 目标单元 → 按目标相对方向映射 Left/Right/Up/Down
        ///   3. 当前行走朝向推断（Rotation） → 对应方向
        ///   4. 默认 Center
        /// </summary>
        private void UpdateEyeDirectionState()
        {
            if (Pawn == null) return;
            if (activeSkin?.faceConfig?.eyeDirectionConfig?.enabled != true) return;

            var pawn = Pawn!;
            var oldDir = curEyeDirection;

            // 死亡 / 倒地 / 睡眠 → 始终 Center（眼神涣散 / 闭眼）
            if (pawn.Dead || pawn.Downed || RestUtility.InBed(pawn))
            {
                curEyeDirection = EyeDirection.Center;
            }
            else
            {
                // 有 Job 目标：根据目标相对方向决定眼神
                var targetCell = GetJobTargetCell(pawn);
                if (targetCell.IsValid && pawn.Position.IsValid)
                {
                    IntVec3 delta = targetCell - pawn.Position;
                    curEyeDirection = MapDeltaToEyeDirection(delta, pawn.Rotation);
                }
                else
                {
                    // 无目标时按角色面朝方向给出轻微方向感
                    curEyeDirection = MapRotationToEyeDirection(pawn.Rotation);
                }
            }

            if (oldDir != curEyeDirection)
                RequestRenderRefresh();
        }

        /// <summary>获取 Pawn 当前 Job 的目标单元（安全获取，失败返回 IntVec3.Invalid）</summary>
        private static IntVec3 GetJobTargetCell(Pawn pawn)
        {
            try
            {
                var job = pawn.CurJob;
                if (job == null) return IntVec3.Invalid;

                // 优先取 targetA 位置
                var targetA = job.targetA;
                if (targetA.HasThing && targetA.Thing?.Position.IsValid == true)
                    return targetA.Thing.Position;
                if (targetA.Cell.IsValid)
                    return targetA.Cell;

                return IntVec3.Invalid;
            }
            catch
            {
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// 将目标相对向量映射到 EyeDirection。
        /// 以 Pawn 自身朝向为参考系：Pawn 面南（默认 Rot4.South）时，
        ///   屏幕 X+ = Right，Z+ = Down（向画面底部）。
        /// </summary>
        private static EyeDirection MapDeltaToEyeDirection(IntVec3 delta, Rot4 rot)
        {
            if (delta == IntVec3.Zero) return EyeDirection.Center;

            // 将世界坐标 delta 转换到角色面向局部坐标
            // Rot4.South (face down) → local(x,z) = world(x, z)
            // Rot4.North (face up)   → local(x,z) = world(-x, -z)
            // Rot4.East  (face right)→ local(x,z) = world(z, -x)
            // Rot4.West  (face left) → local(x,z) = world(-z, x)
            int localX, localZ;
            if (rot == Rot4.North)      { localX = -delta.x; localZ = -delta.z; }
            else if (rot == Rot4.East)  { localX =  delta.z; localZ = -delta.x; }
            else if (rot == Rot4.West)  { localX = -delta.z; localZ =  delta.x; }
            else /* South */            { localX =  delta.x; localZ =  delta.z; }

            int absX = Math.Abs(localX);
            int absZ = Math.Abs(localZ);

            if (absX <= 1 && absZ <= 1) return EyeDirection.Center;

            if (absX >= absZ)
                return localX > 0 ? EyeDirection.Right : EyeDirection.Left;
            else
                return localZ > 0 ? EyeDirection.Down : EyeDirection.Up;
        }

        /// <summary>无目标时按面朝方向给出默认眼神方向</summary>
        private static EyeDirection MapRotationToEyeDirection(Rot4 rot)
        {
            // 等距视角：面北时略偏上，面南时略偏下，左右适中 → 均默认 Center
            return EyeDirection.Center;
        }

        public void RequestRenderRefresh()
        {
            if (Pawn?.Drawer?.renderer != null)
            {
                Pawn.Drawer.renderer.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(Pawn);
            }
        }

        public void EnsureFaceRuntimeStateReadyForPreview()
        {
            if (Pawn == null || activeSkin?.faceConfig?.enabled != true)
                return;

            var runtimeState = CurrentFaceRuntimeState;
            var compiledData = CurrentFaceRuntimeCompiledData;
            int currentTick = Find.TickManager?.TicksGame ?? 0;

            FaceRuntimePolicy.UpdateRuntimeState(Pawn, this, runtimeState, compiledData, currentTick);

            runtimeState.trackDirty = false;
            runtimeState.lodDirty = false;
            runtimeState.compiledDataDirty = false;

            runtimeState.currentExpression = GetEffectiveExpression();
            runtimeState.currentEyeDirection = CurEyeDirection;
            runtimeState.currentMouthState = GetEffectiveMouthState();
            runtimeState.currentLidState = GetEffectiveLidState();
            runtimeState.currentBrowState = GetEffectiveBrowState();
            runtimeState.currentEmotionOverlayState = GetEffectiveEmotionOverlayState();
            runtimeState.expressionDirty = false;
        }

        public void ClearSkin()
        {
            activeSkin = null;
            activeSkinDefName = null;
            activeSkinFromDefaultRaceBinding = false;
            MarkFaceRuntimeDirty();
            RequestRenderRefresh();
        }

        private void TryApplyDefaultRaceSkinIfNeeded()
        {
            if (Pawn == null || Pawn.def == null) return;
            if (!Pawn.RaceProps.Humanlike) return;
            if (activeSkin != null) return;

            var defaultSkin = PawnSkinDefRegistry.GetDefaultSkinForRace(Pawn.def);
            if (defaultSkin == null) return;

            activeSkin = defaultSkin.Clone();
            activeSkinDefName = activeSkin.defName;
            activeSkinFromDefaultRaceBinding = true;
            MarkFaceRuntimeDirty();
            needsRefresh = true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref activeSkinDefName, "activeSkinDefName");
            Scribe_Values.Look(ref activeSkinFromDefaultRaceBinding, "activeSkinFromDefaultRaceBinding", false);
            Scribe_Values.Look(ref qHotkeyModeIndex, "qHotkeyModeIndex", 0);
            Scribe_Values.Look(ref qComboWindowEndTick, "qComboWindowEndTick", 0);
            Scribe_Values.Look(ref qCooldownUntilTick, "qCooldownUntilTick", 0);
            Scribe_Values.Look(ref wCooldownUntilTick, "wCooldownUntilTick", 0);
            Scribe_Values.Look(ref eCooldownUntilTick, "eCooldownUntilTick", 0);
            Scribe_Values.Look(ref rCooldownUntilTick, "rCooldownUntilTick", 0);

            Scribe_Values.Look(ref rStackingEnabled, "rStackingEnabled", false);
            Scribe_Values.Look(ref rStackCount, "rStackCount", 0);
            Scribe_Values.Look(ref rSecondStageReady, "rSecondStageReady", false);
            Scribe_Values.Look(ref rSecondStageExecuteTick, "rSecondStageExecuteTick", -1);
            Scribe_Values.Look(ref rSecondStageHasTarget, "rSecondStageHasTarget", false);
            Scribe_Values.Look(ref rSecondStageTargetCell, "rSecondStageTargetCell", IntVec3.Invalid);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && !string.IsNullOrEmpty(activeSkinDefName))
            {
                activeSkin = DefDatabase<PawnSkinDef>.GetNamedSilentFail(activeSkinDefName);
                if (activeSkin == null)
                {
                    activeSkin = PawnSkinDefRegistry.TryGet(activeSkinDefName);
                    if (activeSkin != null)
                    {
                        activeSkinDefName = activeSkin.defName;
                    }
                }
            }

            if (qHotkeyModeIndex < 0 || qHotkeyModeIndex > 3)
            {
                qHotkeyModeIndex = 0;
            }

            if (rStackCount < 0)
            {
                rStackCount = 0;
            }
            if (rStackCount > 7)
            {
                rStackCount = 7;
            }

            if (!rSecondStageHasTarget)
            {
                rSecondStageTargetCell = IntVec3.Invalid;
            }
        }
    }

    public class CompProperties_PawnSkin : CompProperties
    {
        public CompProperties_PawnSkin() => this.compClass = typeof(CompPawnSkin);
    }

    public enum MouthState
    {
        Normal,
        Smile,
        Open,
        Down,
        Sleep
    }

    public enum LidState
    {
        Normal,
        Blink,
        Half,
        Close,
        Happy
    }

    public enum BrowState
    {
        Normal,
        Angry,
        Sad,
        Happy
    }

    public enum EmotionOverlayState
    {
        None,
        Blush,
        Tear,
        Gloomy,
        Lovin
    }
}