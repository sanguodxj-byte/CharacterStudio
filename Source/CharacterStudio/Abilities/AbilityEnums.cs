using System;
using System.Linq;

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
        WeatherChange,
        BezierCurveWall
    }

    public enum AbilityRuntimeHotkeySlot
    {
        None,
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

    public static class AbilityHotkeySlotUtility
    {
        public static readonly AbilityRuntimeHotkeySlot[] SupportedRuntimeSlots =
        {
            AbilityRuntimeHotkeySlot.Q,
            AbilityRuntimeHotkeySlot.E,
            AbilityRuntimeHotkeySlot.R,
            AbilityRuntimeHotkeySlot.T,
            AbilityRuntimeHotkeySlot.F,
            AbilityRuntimeHotkeySlot.Z,
            AbilityRuntimeHotkeySlot.X,
            AbilityRuntimeHotkeySlot.C,
            AbilityRuntimeHotkeySlot.V
        };

        public static readonly string[] SupportedSlotKeys = SupportedRuntimeSlots
            .Select(static slot => slot.ToString())
            .ToArray();

        public static bool IsSupportedRuntimeSlot(AbilityRuntimeHotkeySlot slot)
        {
            for (int i = 0; i < SupportedRuntimeSlots.Length; i++)
            {
                if (SupportedRuntimeSlots[i] == slot)
                {
                    return true;
                }
            }

            return false;
        }

        public static AbilityRuntimeHotkeySlot NormalizeSupportedRuntimeSlot(AbilityRuntimeHotkeySlot slot)
        {
            return IsSupportedRuntimeSlot(slot) ? slot : AbilityRuntimeHotkeySlot.None;
        }

        public static bool IsSupportedSlotKey(string? slotKey)
        {
            if (string.IsNullOrWhiteSpace(slotKey))
            {
                return false;
            }

            for (int i = 0; i < SupportedSlotKeys.Length; i++)
            {
                if (string.Equals(SupportedSlotKeys[i], slotKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
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
        Target,
        Caster
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
        GlobalFilter,
        CustomMesh,
        Fleck,
        FleckConnectingLine
    }

    /// <summary>
    /// 自定义 Mesh VFX 的形状类型。
    /// 所有 Mesh 均在运行时程序化生成，不需要 AssetBundle 或 Unity Editor。
    /// </summary>
    public enum VfxMeshShape
    {
        /// <summary>闪电锯齿线：沿直线方向随机偏移顶点，形成不规则锯齿带状 Mesh</summary>
        LightningBolt,
        /// <summary>环形冲击波：由内半径和外半径定义的中空环状 Mesh</summary>
        Ring,
        /// <summary>螺旋线：沿垂直轴螺旋上升的带状 Mesh</summary>
        Spiral,
        /// <summary>锥形光束：从起点到终点逐渐变窄的锥形 Mesh</summary>
        Beam
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

    public enum AbilityLineRenderMode
    {
        VanillaConnectingLine,
        SegmentedLine
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
