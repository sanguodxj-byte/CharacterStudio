namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 效果类型的目标分类，决定在哪个阶段施加效果。
    /// </summary>
    public enum EffectTargetCategory
    {
        /// <summary>以实体（Pawn/Thing）为目标：Damage, Heal, Buff, Debuff, Control, Thought</summary>
        EntityDirected,
        /// <summary>以主目标格为中心：Teleport, WeatherChange, Summon</summary>
        PrimaryCell,
        /// <summary>以区域内每个格为目标：Terraform</summary>
        AreaCell
    }

    /// <summary>
    /// 效果类型的行为策略接口。
    /// 每种 AbilityEffectType 对应一个实现，封装该类型的全部类型特化逻辑。
    /// </summary>
    public interface IEffectBehavior
    {
        /// <summary>该效果类型的目标分类</summary>
        EffectTargetCategory Category { get; }

        /// <summary>展开区域额外行数（不含公共的 Amount+Chance 行）</summary>
        int GetExpandedRowCount(AbilityEffectConfig config);

        /// <summary>对该效果类型的专有字段做规范化</summary>
        void NormalizeForSave(AbilityEffectConfig config);

        /// <summary>对该效果类型做校验</summary>
        void Validate(AbilityEffectConfig config, AbilityValidationResult result);

        /// <summary>设置新建效果的初始默认值（类型切换时调用）</summary>
        void SetDefaults(AbilityEffectConfig config);
    }
}
