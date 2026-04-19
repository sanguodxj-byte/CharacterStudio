// ─────────────────────────────────────────────
// 技能系统全部枚举定义
// 从 ModularAbilityDef.cs 提取以改善可读性
// ─────────────────────────────────────────────

namespace CharacterStudio.Abilities
{
    public enum AbilityRuntimeComponentType
    {
        SlotOverrideWindow,
        HotkeyOverride,
        FollowupCooldownGate,
        SmartJump,
        EShortJump,
        Dash,
        RStackDetonation,
        PeriodicPulse,
        KillRefresh,
        ShieldAbsorb,
        AttachedShieldVisual,
        ProjectileInterceptorShield,
        ChainBounce,
        ExecuteBonusDamage,
        FullHealthBonusDamage,
        MissingHealthBonusDamage,
        NearbyEnemyBonusDamage,
        IsolatedTargetBonusDamage,
        MarkDetonation,
        ComboStacks,
        HitSlowField,
        PierceBonusDamage,
        DashEmpoweredStrike,
        HitHeal,
        HitCooldownRefund,
        ProjectileSplit,
        FlightState,
        VanillaPawnFlyer,
        FlightOnlyFollowup,
        FlightLandingBurst,
        TimeStop,
        WeatherChange
    }

    public enum AbilityRuntimeHotkeySlot
    {
        Q,
        W,
        E,
        R,
        T,
        A,
        S,
        D,
        F,
        Z,
        X,
        C,
        V
    }

    public enum AbilityCarrierType
    {
        Self,
        Target,
        Projectile,
        Touch,
        Area
    }

    public enum AbilityTargetType
    {
        Self,
        Entity,
        Cell
    }

    public enum AbilityAreaCenter
    {
        Self,
        Target
    }

    public enum AbilityAreaShape
    {
        Circle,
        Line,
        Cone,
        Cross,
        Square,
        Irregular
    }

    public enum AbilityEffectType
    {
        Damage,
        Heal,
        Buff,
        Debuff,
        Summon,
        Teleport,
        Control,
        Terraform,
        WeatherChange
    }

    public enum AbilityVisualEffectType
    {
        DustPuff,
        MicroSparks,
        LightningGlow,
        FireGlow,
        Smoke,
        ExplosionEffect,
        SteamBurst,
        EmberBurst,
        ShockBurst,
        DustRing,
        ArcSparkBurst,
        FlameSurge,
        LineTexture,
        WallTexture,
        Preset,
        CustomTexture,
        GlobalFilter
    }

    public enum AbilityVisualSpatialMode
    {
        Point,
        Line,
        Wall
    }

    public enum AbilityVisualAnchorMode
    {
        Caster,
        Target,
        TargetCell,
        AreaCenter
    }

    public enum AbilityVisualPathMode
    {
        None,
        DirectLineCasterToTarget
    }

    public enum VisualEffectTarget
    {
        Caster,
        Target,
        Both
    }

    public enum AbilityVisualEffectTextureSource
    {
        Vanilla,
        LocalPath
    }

    public enum AbilityVisualEffectSourceMode
    {
        BuiltIn,
        Preset,
        CustomTexture
    }

    public enum AbilityVisualFacingMode
    {
        None,
        CasterFacing,
        CastDirection
    }

    public enum VfxBundleRenderStrategy
    {
        Default,
        CustomMote
    }

    public enum AbilityVisualEffectTrigger
    {
        OnCastStart,
        OnWarmup,
        OnCastFinish,
        OnTargetApply,
        OnDurationTick,
        OnExpire
    }

    public enum ControlEffectMode
    {
        Stun,
        Knockback,
        Pull
    }

    public enum TerraformEffectMode
    {
        CleanFilth,
        SpawnThing,
        ReplaceTerrain
    }

    public enum DashEffectTiming
    {
        OnCollisionStop,
        OnCollisionPassThrough,
        OnComplete
    }

    public enum SummonFactionType
    {
        Player,
        Caster,
        Hostile,
        Neutral,
        FixedDef
    }
}
