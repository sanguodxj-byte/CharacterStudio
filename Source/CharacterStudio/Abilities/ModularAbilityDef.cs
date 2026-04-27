// ─────────────────────────────────────────────
// 模块化能力主定义类（ModularAbilityDef）
//
// 相关类型已拆分至独立文件：
//   AbilityEnums.cs                      — 所有枚举
//   AbilityEffectConfig.cs               — 效果配置
//   AbilityVisualEffectConfig.cs         — 视觉特效配置
//   AbilityRuntimeComponentConfig.cs     — 运行时组件配置
//   ModularAbilityDef.Validation.cs      — 验证结果、规范化工具、验证扩展方法
// ─────────────────────────────────────────────

using System.Collections.Generic;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 模块化能力配置
    /// 用于编辑器内存存储，最终会导出为原版 AbilityDef
    /// </summary>
    public class ModularAbilityDef : Def, IExposable
    {
        public string iconPath = "";
        public float cooldownTicks = 600f;
        public float warmupTicks = 60f;
        public int charges = 1;
        public float aiCanUse = 1f;

        public AbilityCarrierType carrierType = AbilityCarrierType.Self;
        public AbilityTargetType targetType = AbilityTargetType.Self;
        public bool useTwoPointTargeting = false;
        public bool useRadius = false;
        public AbilityAreaCenter areaCenter = AbilityAreaCenter.Target;
        public AbilityAreaShape areaShape = AbilityAreaShape.Circle;
        public string irregularAreaPattern = string.Empty;
        public float range = 20f;
        public float radius = 0f;
        public ThingDef? projectileDef;

        public List<AbilityEffectConfig> effects = new List<AbilityEffectConfig>();
        public List<AbilityVisualEffectConfig> visualEffects = new List<AbilityVisualEffectConfig>();
        public List<AbilityRuntimeComponentConfig> runtimeComponents = new List<AbilityRuntimeComponentConfig>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref description, "description");

            Scribe_Values.Look(ref iconPath, "iconPath", "");
            Scribe_Values.Look(ref cooldownTicks, "cooldownTicks", 600f);
            Scribe_Values.Look(ref warmupTicks, "warmupTicks", 60f);
            Scribe_Values.Look(ref charges, "charges", 1);
            Scribe_Values.Look(ref aiCanUse, "aiCanUse", 1f);

            Scribe_Values.Look(ref carrierType, "carrierType", AbilityCarrierType.Self);
            Scribe_Values.Look(ref targetType, "targetType", AbilityTargetType.Self);
            Scribe_Values.Look(ref useTwoPointTargeting, "useTwoPointTargeting", false);
            Scribe_Values.Look(ref useRadius, "useRadius", false);
            Scribe_Values.Look(ref areaCenter, "areaCenter", AbilityAreaCenter.Target);
            Scribe_Values.Look(ref areaShape, "areaShape", AbilityAreaShape.Circle);
            Scribe_Values.Look(ref irregularAreaPattern, "irregularAreaPattern", string.Empty);
            Scribe_Values.Look(ref range, "range", 20f);
            Scribe_Values.Look(ref radius, "radius", 0f);
            Scribe_Defs.Look(ref projectileDef, "projectileDef");

            Scribe_Collections.Look(ref effects, "effects", LookMode.Deep);
            Scribe_Collections.Look(ref visualEffects, "visualEffects", LookMode.Deep);
            Scribe_Collections.Look(ref runtimeComponents, "runtimeComponents", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                effects ??= new List<AbilityEffectConfig>();
                visualEffects ??= new List<AbilityVisualEffectConfig>();
                runtimeComponents ??= new List<AbilityRuntimeComponentConfig>();
            }
        }

        public ModularAbilityDef Clone()
        {
            var clone = new ModularAbilityDef
            {
                defName = this.defName,
                label = this.label,
                description = this.description,
                iconPath = this.iconPath,
                cooldownTicks = this.cooldownTicks,
                warmupTicks = this.warmupTicks,
                charges = this.charges,
                aiCanUse = this.aiCanUse,
                carrierType = this.carrierType,
                targetType = this.targetType,
                useTwoPointTargeting = this.useTwoPointTargeting,
                useRadius = this.useRadius,
                areaCenter = this.areaCenter,
                areaShape = this.areaShape,
                irregularAreaPattern = this.irregularAreaPattern,
                range = this.range,
                radius = this.radius,
                projectileDef = this.projectileDef
            };

            foreach (var effect in this.effects)
            {
                clone.effects.Add(effect.Clone());
            }

            foreach (var component in this.runtimeComponents)
            {
                clone.runtimeComponents.Add(component.Clone());
            }

            foreach (var vfx in this.visualEffects)
            {
                clone.visualEffects.Add(vfx.Clone());
            }

            return clone;
        }

        public void NormalizeForSave()
        {
            iconPath = AbilityEditorNormalizationUtility.TrimOrEmpty(iconPath);
            irregularAreaPattern = irregularAreaPattern ?? string.Empty;

            cooldownTicks = AbilityEditorNormalizationUtility.ClampFloat(cooldownTicks, 0f, 100000f);
            warmupTicks = AbilityEditorNormalizationUtility.ClampFloat(warmupTicks, 0f, 100000f);
            charges = AbilityEditorNormalizationUtility.ClampInt(charges, 1, 999);
            aiCanUse = AbilityEditorNormalizationUtility.ClampFloat(aiCanUse, 0f, 1f);

            carrierType = ModularAbilityDefExtensions.NormalizeCarrierType(carrierType);
            targetType = ModularAbilityDefExtensions.NormalizeTargetType(this);
            areaCenter = ModularAbilityDefExtensions.NormalizeAreaCenter(this);
            areaShape = ModularAbilityDefExtensions.NormalizeAreaShape(this);

            range = AbilityEditorNormalizationUtility.ClampFloat(range, 0f, 100f);
            radius = AbilityEditorNormalizationUtility.ClampFloat(radius, 0f, 20f);
            if (useRadius && areaShape != AbilityAreaShape.Irregular && radius <= 0f)
            {
                radius = 0.1f;
            }

            effects ??= new List<AbilityEffectConfig>();
            runtimeComponents ??= new List<AbilityRuntimeComponentConfig>();
            visualEffects ??= new List<AbilityVisualEffectConfig>();

            for (int i = 0; i < effects.Count; i++)
            {
                effects[i]?.NormalizeForSave();
            }

            for (int i = 0; i < runtimeComponents.Count; i++)
            {
                runtimeComponents[i]?.NormalizeForSave();
            }

            for (int i = 0; i < visualEffects.Count; i++)
            {
                visualEffects[i]?.NormalizeForSave();
            }
        }
    }
}
