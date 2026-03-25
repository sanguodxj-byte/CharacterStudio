using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 使用自定义贴地 PawnFlyer 的直线位移技能 Verb。
    /// 保留原版 Verb_CastAbilityJump 的施法、落地与跳跃完成回调链路，
    /// 仅替换飞行载体，使位移表现从抛物线改为贴地直线冲刺。
    /// </summary>
    public class Verb_CastAbilityStraightJump : Verb_CastAbilityJump
    {
        private static ThingDef? cachedFlyerDef;

        public override ThingDef JumpFlyerDef
        {
            get
            {
                cachedFlyerDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("CS_PawnFlyer_StraightJump");
                return cachedFlyerDef ?? base.JumpFlyerDef;
            }
        }
    }
}