using System;
using RimWorld;
using CharacterStudio.Core;
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

            if (usedBy == null || usedBy.Map == null)
            {
                Log.Error("[CharacterStudio] 召唤失败：使用者或地图为空");
                return;
            }

            if (Props.pawnKind == null)
            {
                Log.Error("[CharacterStudio] 召唤失败：PawnKind 为空");
                return;
            }

            Faction spawnFaction = Props.isHostile
                ? (Faction.OfAncientsHostile ?? Faction.OfPlayer)
                : Faction.OfPlayer;
            Pawn? pawn = CharacterSpawnUtility.GeneratePawn(Props.pawnKind, spawnFaction);
            if (pawn == null)
            {
                Log.Error("[CharacterStudio] 召唤失败：生成 Pawn 失败");
                return;
            }

            if (!Props.isHostile && pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            // 生成位置 - 寻找附近可用位置
            IntVec3 loc = usedBy.Position;
            Map map = usedBy.Map;
            CharacterSpawnSettings spawnSettings = new CharacterSpawnSettings
            {
                arrivalMode = Props.arrivalMode,
                spawnAnimation = Props.spawnAnimation,
                spawnAnimationScale = Props.spawnAnimationScale,
                spawnEvent = Props.spawnEvent
            };

            CharacterSpawnUtility.TryFindSpawnCell(map, usedBy.Position, 5, out loc);
            
            CharacterSpawnUtility.SpawnPawnWithSettings(pawn, map, loc, spawnSettings);
            CharacterSpawnUtility.SendSpawnEvent(
                spawnSettings,
                pawn,
                map,
                loc,
                "CS_Summon_Success".Translate(pawn.LabelShort),
                "CS_RoleCard_LetterLabel".Translate(pawn.LabelShort));
        }
    }

    public enum SummonArrivalMode
    {
        Standing,
        DropPod
    }

    public enum SummonSpawnAnimationMode
    {
        None,
        DustPuff,
        MicroSparks,
        LightningGlow,
        FireGlow,
        Smoke,
        ExplosionEffect
    }

    public enum SummonSpawnEventMode
    {
        None,
        Message,
        PositiveLetter
    }

    public class CompProperties_SummonCharacter : CompProperties_UseEffect
    {
        public PawnKindDef? pawnKind;
        public bool isHostile = false;
        public SummonArrivalMode arrivalMode = SummonArrivalMode.Standing;
        public SummonSpawnAnimationMode spawnAnimation = SummonSpawnAnimationMode.None;
        public float spawnAnimationScale = 1f;
        public SummonSpawnEventMode spawnEvent = SummonSpawnEventMode.Message;

        public CompProperties_SummonCharacter()
        {
            this.compClass = typeof(CompSummonCharacter);
        }
    }
}
