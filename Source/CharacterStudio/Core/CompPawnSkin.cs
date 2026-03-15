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

            // 每 30 Tick (约 0.5s) 更新一次表情环境
            if (Pawn.IsHashIntervalTick(30))
            {
                UpdateExpressionState();
            }

            // 眨眼逻辑
            if (activeSkin?.faceConfig?.enabled == true)
            {
                UpdateBlinkLogic();
            }
        }

        private void UpdateExpressionState()
        {
            if (activeSkin?.faceConfig?.enabled != true) return;

            var oldExp = curExpression;
            var pawn   = Pawn!;

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
            else if (pawn.CurJob?.def == JobDefOf.Ingest)
            {
                curExpression = ExpressionType.Eating;
            }
            else if (pawn.InMentalState)
            {
                // 根据心理状态细分表情
                var stateDef = pawn.MentalStateDef;
                bool isPanic = stateDef != null &&
                    (stateDef.defName.IndexOf("Flee", StringComparison.OrdinalIgnoreCase) >= 0
                    || stateDef.defName.IndexOf("Panic", StringComparison.OrdinalIgnoreCase) >= 0
                    || stateDef.defName.IndexOf("WildMan", StringComparison.OrdinalIgnoreCase) >= 0);
                curExpression = isPanic ? ExpressionType.Scared : ExpressionType.Angry;
            }
            else
            {
                float rest = pawn.needs?.rest?.CurLevel ?? 1f;
                float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;

                if (rest < 0.15f)
                    curExpression = ExpressionType.Tired;
                else if (mood < 0.2f)
                    curExpression = ExpressionType.Sad;
                else if (mood > 0.8f)
                    curExpression = ExpressionType.Happy;
                else
                    curExpression = ExpressionType.Neutral;
            }

            if (oldExp != curExpression)
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
            {
                return previewExpressionOverride.Value;
            }
            if (blinkTimer > 0) return ExpressionType.Blink;
            return curExpression;
        }

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
