using System;
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

        // 表情状态
        public ExpressionType curExpression = ExpressionType.Neutral;
        private ExpressionType? previewExpressionOverride = null;
        private int blinkTimer = 0;
        private const int BlinkDuration = 10;

        // 帧动画 Tick 计数器（每 Tick +1，用于多帧表情序列定位）
        private int expressionAnimTick = 0;

        // Q 键四段轮换模式索引（0..3）
        public int qHotkeyModeIndex = 0;

        // Q->W 连段窗口（单位：Tick）
        public int qComboWindowEndTick = 0;

        // E 技能短 CD
        public int eCooldownUntilTick = 0;

        // R 两段机制状态
        public bool rStackingEnabled = false;
        public int rStackCount = 0;
        public bool rSecondStageReady = false;
        public int rSecondStageExecuteTick = -1;
        public bool rSecondStageHasTarget = false;
        public IntVec3 rSecondStageTargetCell = IntVec3.Invalid;

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
        }

        public bool HasActiveSkin => activeSkin != null;
        public Pawn? Pawn => parent as Pawn;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!string.IsNullOrEmpty(activeSkinDefName) && activeSkin == null)
            {
                activeSkin = DefDatabase<PawnSkinDef>.GetNamedSilentFail(activeSkinDefName);
            }
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
                // 帧动画 Tick 累加（驱动多帧表情序列）
                expressionAnimTick++;

                // 每 30 Tick (约 0.5s) 更新一次表情状态
                if (Pawn.IsHashIntervalTick(30))
                    UpdateExpressionState();

                // 帧动画帧推进：检测当前帧是否应切换到下一帧
                UpdateAnimatedExpressionFrame();

                // 眨眼逻辑
                UpdateBlinkLogic();
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
            else if (job != null && (
                job.defName == "LayDown" || job == JobDefOf.Lovin
                || job.defName.IndexOf("LayDown", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.LayDown;
            }
            else if (job != null && (
                job.defName.IndexOf("Strip", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.Strip;
            }
            else if (job != null && (
                job.defName.IndexOf("Haul", StringComparison.OrdinalIgnoreCase) >= 0
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
                || job.defName.IndexOf("Social", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Chat", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.SocialRelax;
            }
            else if (job != null && (
                job.defName.IndexOf("DoBill", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Craft", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Cook", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.Working;
            }
            else if (job != null && (
                job.defName.IndexOf("WaitCombat", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("AttackStatic", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.WaitCombat;
            }
            else if (job != null && (
                job.defName.IndexOf("AttackMelee", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.AttackMelee;
            }
            else if (job != null && (
                job.defName.IndexOf("Shoot", StringComparison.OrdinalIgnoreCase) >= 0
                || job.defName.IndexOf("Burst", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                curExpression = ExpressionType.AttackRanged;
            }
            else if (job != null && (
                job.defName.IndexOf("Goto", StringComparison.OrdinalIgnoreCase) >= 0
                || job == JobDefOf.Goto))
            {
                curExpression = ExpressionType.Goto;
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

        /// <summary>获取当前帧动画 Tick（供 FaceComponent 渲染时定位帧）</summary>
        public int GetExpressionAnimTick() => expressionAnimTick;

        public void RequestRenderRefresh()
        {
            if (Pawn?.Drawer?.renderer != null)
            {
                Pawn.Drawer.renderer.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(Pawn);
            }
        }

        public void ClearSkin()
        {
            activeSkin = null;
            activeSkinDefName = null;
            RequestRenderRefresh();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref activeSkinDefName, "activeSkinDefName");
            Scribe_Values.Look(ref qHotkeyModeIndex, "qHotkeyModeIndex", 0);
            Scribe_Values.Look(ref qComboWindowEndTick, "qComboWindowEndTick", 0);
            Scribe_Values.Look(ref eCooldownUntilTick, "eCooldownUntilTick", 0);

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
}
