// ─────────────────────────────────────────────
// 技能效果配置（伤害、治疗、Buff、召唤等）
// 从 ModularAbilityDef.cs 提取
// ─────────────────────────────────────────────

using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    public class AbilityEffectConfig : IExposable
    {
        public AbilityEffectType type;
        public float amount = 0f;
        public float duration = 0f;
        public float chance = 1f;
        public DamageDef? damageDef;
        public HediffDef? hediffDef;
        public PawnKindDef? summonKind;
        public int summonCount = 1;
        public SummonFactionType summonFactionType = SummonFactionType.Player;
        public FactionDef? summonFactionDef;
        public string summonFactionDefName = string.Empty;

        public ControlEffectMode controlMode = ControlEffectMode.Stun;
        public int controlMoveDistance = 3;
        public TerraformEffectMode terraformMode = TerraformEffectMode.CleanFilth;
        public ThingDef? terraformThingDef;
        public TerrainDef? terraformTerrainDef;
        public int terraformSpawnCount = 1;
        public bool canHurtSelf = false;

        /// <summary>
        /// 效果延迟执行的 tick 数。按此值在施法时刻轴上排列执行顺序。
        /// 0 = 立即执行。
        /// </summary>
        public int delayTicks = 0;

        public string weatherDefName = string.Empty; // Added for WeatherChange
        public int weatherDurationTicks = 60000; // Added for WeatherChange (default 1 day)
        public int weatherTransitionTicks = 3000; // Added for WeatherChange (default 0.5 hour)

        public string thoughtLabel = string.Empty;
        public string thoughtDescription = string.Empty;
        public float thoughtMoodOffset = 5f;
        public float thoughtDurationDays = 1f;
        public int thoughtStackLimit = 1;
        public bool thoughtShowBubble = true;
        public string thoughtIconPath = string.Empty;

        public void ExposeData()
        {
            Scribe_Values.Look(ref type, "type");
            Scribe_Values.Look(ref amount, "amount", 0f);
            Scribe_Values.Look(ref duration, "duration", 0f);
            Scribe_Values.Look(ref chance, "chance", 1f);
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Defs.Look(ref hediffDef, "hediffDef");
            Scribe_Defs.Look(ref summonKind, "summonKind");
            Scribe_Values.Look(ref summonCount, "summonCount", 1);
            Scribe_Values.Look(ref summonFactionType, "summonFactionType", SummonFactionType.Player);
            Scribe_Defs.Look(ref summonFactionDef, "summonFactionDef");
            Scribe_Values.Look(ref summonFactionDefName, "summonFactionDefName", string.Empty);
            Scribe_Values.Look(ref controlMode, "controlMode", ControlEffectMode.Stun);
            Scribe_Values.Look(ref controlMoveDistance, "controlMoveDistance", 3);
            Scribe_Values.Look(ref terraformMode, "terraformMode", TerraformEffectMode.CleanFilth);
            Scribe_Defs.Look(ref terraformThingDef, "terraformThingDef");
            Scribe_Defs.Look(ref terraformTerrainDef, "terraformTerrainDef");
            Scribe_Values.Look(ref terraformSpawnCount, "terraformSpawnCount", 1);
            Scribe_Values.Look(ref canHurtSelf, "canHurtSelf", false);
            Scribe_Values.Look(ref delayTicks, "delayTicks", 0);
            Scribe_Values.Look(ref weatherDefName, "weatherDefName", string.Empty);
            Scribe_Values.Look(ref weatherDurationTicks, "weatherDurationTicks", 60000);
            Scribe_Values.Look(ref weatherTransitionTicks, "weatherTransitionTicks", 3000);
            Scribe_Values.Look(ref thoughtLabel, "thoughtLabel", string.Empty);
            Scribe_Values.Look(ref thoughtDescription, "thoughtDescription", string.Empty);
            Scribe_Values.Look(ref thoughtMoodOffset, "thoughtMoodOffset", 5f);
            Scribe_Values.Look(ref thoughtDurationDays, "thoughtDurationDays", 1f);
            Scribe_Values.Look(ref thoughtStackLimit, "thoughtStackLimit", 1);
            Scribe_Values.Look(ref thoughtShowBubble, "thoughtShowBubble", true);
            Scribe_Values.Look(ref thoughtIconPath, "thoughtIconPath", string.Empty);
        }

        public AbilityEffectConfig Clone()
        {
            return (AbilityEffectConfig)MemberwiseClone();
        }

        public void NormalizeForSave()
        {
            // 公共字段规范化
            duration = AbilityEditorNormalizationUtility.ClampFloat(duration, 0f, 999f);
            chance = AbilityEditorNormalizationUtility.ClampFloat(chance, 0.01f, 1f);
            delayTicks = AbilityEditorNormalizationUtility.ClampInt(delayTicks, 0, 60000);

            // 类型特化规范化委托给 Behavior
            EffectBehaviorRegistry.Get(type).NormalizeForSave(this);
        }

        public AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (amount < 0)
                result.AddWarning("CS_Ability_Validate_EffectNegativeAmount".Translate());
            if (chance <= 0 || chance > 1)
                result.AddError("CS_Ability_Validate_EffectChanceRange".Translate(chance));
            if (delayTicks < 0)
                result.AddWarning("CS_Ability_Validate_EffectDelayNonNegative".Translate());

            // 类型特化校验委托给 Behavior
            EffectBehaviorRegistry.Get(type).Validate(this, result);

            return result;
        }
    }
}
