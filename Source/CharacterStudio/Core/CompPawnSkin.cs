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
        private int blinkTimer = 0;
        private const int BlinkDuration = 10;

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
            var pawn = Pawn!;

            if (pawn.Dead) curExpression = ExpressionType.Dead;
            else if (pawn.Downed) curExpression = ExpressionType.Pain;
            else if (RestUtility.InBed(pawn)) curExpression = ExpressionType.Sleeping;
            else if (pawn.CurJob?.def == JobDefOf.Ingest) curExpression = ExpressionType.Eating;
            else
            {
                if (pawn.InMentalState) curExpression = ExpressionType.Angry;
                else
                {
                    float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;
                    if (mood < 0.2f) curExpression = ExpressionType.Sad;
                    else if (mood > 0.8f) curExpression = ExpressionType.Happy;
                    else curExpression = ExpressionType.Neutral;
                }
            }

            if (oldExp != curExpression)
            {
                RequestRenderRefresh();
            }
        }

        private void UpdateBlinkLogic()
        {
            if (curExpression == ExpressionType.Sleeping || curExpression == ExpressionType.Dead) return;

            if (blinkTimer > 0)
            {
                blinkTimer--;
                if (blinkTimer == 0) RequestRenderRefresh();
            }
            else
            {
                if (Rand.Value < 0.008f)
                {
                    blinkTimer = BlinkDuration;
                    RequestRenderRefresh();
                }
            }
        }

        public ExpressionType GetEffectiveExpression()
        {
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !string.IsNullOrEmpty(activeSkinDefName))
            {
                activeSkin = DefDatabase<PawnSkinDef>.GetNamedSilentFail(activeSkinDefName);
            }
        }
    }

    public class CompProperties_PawnSkin : CompProperties
    {
        public CompProperties_PawnSkin() => this.compClass = typeof(CompPawnSkin);
    }
}