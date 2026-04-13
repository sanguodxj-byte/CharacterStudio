using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities.RuntimeComponents
{
    /// <summary>
    /// 运行时组件处理器基接口。
    /// 所有 AbilityRuntimeComponentType 对应的处理器都实现此接口。
    /// </summary>
    public interface IRuntimeComponentHandler
    {
        /// <summary>此处理器负责的组件类型</summary>
        AbilityRuntimeComponentType ComponentType { get; }
    }

    /// <summary>
    /// 在技能施放时执行的处理器（HandleRuntimeComponentsAtApply 调用点）
    /// </summary>
    public interface IOnApplyHandler : IRuntimeComponentHandler
    {
        void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, LocalTargetInfo target, int nowTick);
    }

    /// <summary>
    /// 不依赖 skinComp 的全局施放处理器（如 TimeStop、WeatherChange）
    /// </summary>
    public interface IGlobalOnApplyHandler : IRuntimeComponentHandler
    {
        void OnApply(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, LocalTargetInfo target, int nowTick);
    }

    /// <summary>
    /// 在命中后执行的处理器（HandlePostHitRuntimeComponents 调用点）
    /// </summary>
    public interface IPostHitHandler : IRuntimeComponentHandler
    {
        void OnPostHit(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin casterSkin, LocalTargetInfo target, Pawn targetPawn, CompPawnSkin targetSkin, float appliedDamage, int nowTick);
    }

    /// <summary>
    /// 修改伤害倍率的处理器（GetRuntimeDamageScale 调用点）
    /// </summary>
    public interface IDamageScaleModifier : IRuntimeComponentHandler
    {
        float GetDamageScale(AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin casterSkin, LocalTargetInfo target, Pawn targetPawn, CompPawnSkin targetSkin, bool allowDashConsume, int nowTick);
    }

    /// <summary>
    /// 需要 CompTick 驱动的处理器
    /// </summary>
    public interface ITickHandler : IRuntimeComponentHandler
    {
        void OnTick(CompAbilityEffect_Modular source, AbilityRuntimeComponentConfig config, Pawn caster, CompPawnSkin skinComp, int nowTick);
    }
}