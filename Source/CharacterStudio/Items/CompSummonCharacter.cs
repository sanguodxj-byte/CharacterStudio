using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CharacterStudio.Items
{
    /// <summary>
    /// 角色召唤组件
    /// 继承自 CompUseEffect，配合 CompUsable 使用
    /// </summary>
    public class CompSummonCharacter : CompUseEffect
    {
        public CompProperties_SummonCharacter Props => (CompProperties_SummonCharacter)props;

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);

            if (Props.pawnKind == null)
            {
                Log.Error("[CharacterStudio] 召唤失败：PawnKind 为空");
                return;
            }

            // 生成 Pawn
            PawnGenerationRequest request = new PawnGenerationRequest(
                Props.pawnKind,
                Props.isHostile ? Faction.OfAncientsHostile : Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                tile: -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true,
                colonistRelationChanceFactor: 0f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowPregnant: false,
                allowFood: false,
                allowAddictions: false
            );

            Pawn pawn = PawnGenerator.GeneratePawn(request);
            if (pawn == null)
            {
                Log.Error("[CharacterStudio] 召唤失败：生成 Pawn 失败");
                return;
            }

            // 生成位置 - 寻找附近可用位置
            IntVec3 loc = usedBy.Position;
            Map map = usedBy.Map;
            
            if (!loc.Standable(map))
            {
                // 尝试在附近找到可站立的位置
                if (!CellFinder.TryFindRandomCellNear(usedBy.Position, map, 5,
                    (IntVec3 c) => c.Standable(map) && !c.Fogged(map), out loc))
                {
                    loc = usedBy.Position; // 回退到原位置
                }
            }
            
            // 召唤方式
            if (Props.arrivalMode == SummonArrivalMode.DropPod)
            {
                SpawnViaDropPod(pawn, loc, map);
            }
            else
            {
                GenSpawn.Spawn(pawn, loc, map, WipeMode.Vanish);
            }

            // 发送消息
            Messages.Message("CS_Summon_Success".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent, true);
        }

        /// <summary>
        /// 通过空投仓生成角色
        /// </summary>
        private void SpawnViaDropPod(Pawn pawn, IntVec3 loc, Map map)
        {
            // 创建空投仓信息
            // 使用 RimWorld.ActiveTransporterInfo (1.5+ 版本中替代了 ActiveDropPodInfo)
            var dropPodInfo = new ActiveTransporterInfo();
            dropPodInfo.innerContainer.TryAdd(pawn);
            dropPodInfo.openDelay = 60; // 1秒后打开
            dropPodInfo.leaveSlag = false;
            
            // 生成空投仓
            DropPodUtility.MakeDropPodAt(loc, map, dropPodInfo);
        }
    }

    public enum SummonArrivalMode
    {
        Standing,
        DropPod
    }

    public class CompProperties_SummonCharacter : CompProperties_UseEffect
    {
        public PawnKindDef? pawnKind;
        public bool isHostile = false;
        public SummonArrivalMode arrivalMode = SummonArrivalMode.Standing;

        public CompProperties_SummonCharacter()
        {
            this.compClass = typeof(CompSummonCharacter);
        }
    }
}