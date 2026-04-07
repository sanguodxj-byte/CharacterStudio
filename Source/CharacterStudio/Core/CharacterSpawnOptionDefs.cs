using System.Collections.Generic;
using RimWorld;
using Verse;
using CharacterStudio.Items;

namespace CharacterStudio.Core
{
    public abstract class CharacterSpawnOptionDef : Def
    {
        public bool enabled = true;
        public int sortOrder = 0;
        public string labelKey = string.Empty;
        public List<CharacterSpawnConditionDef> requiredConditions = new List<CharacterSpawnConditionDef>();
        public CharacterSpawnConditionLogic conditionLogic = CharacterSpawnConditionLogic.All;

        public string GetDisplayLabel()
        {
            if (!string.IsNullOrWhiteSpace(labelKey))
                return labelKey.Translate();

            return !string.IsNullOrWhiteSpace(label)
                ? LabelCap.ToString()
                : defName;
        }
    }

    public class CharacterSpawnArrivalDef : CharacterSpawnOptionDef
    {
        public SummonArrivalMode mode = SummonArrivalMode.Standing;
    }

    public class CharacterSpawnEventDef : CharacterSpawnOptionDef
    {
        public SummonSpawnEventMode mode = SummonSpawnEventMode.Message;
    }

    public class CharacterSpawnAnimationDef : CharacterSpawnOptionDef
    {
        public SummonSpawnAnimationMode mode = SummonSpawnAnimationMode.None;
        public float defaultScale = 1f;
    }

    public enum CharacterSpawnConditionMatchMode
    {
        SpecificThingDef,
        ThingCategory,
        TradeTag,
        FoodType,
        FoodPreferability
    }

    public enum CharacterSpawnConditionLogic
    {
        All,
        Any,
        Not
    }

    public class CharacterSpawnConditionDef
    {
        public string labelKey = string.Empty;
        public CharacterSpawnConditionMatchMode matchMode = CharacterSpawnConditionMatchMode.SpecificThingDef;
        public string thingDefName = string.Empty;
        public string categoryDefName = string.Empty;
        public string tradeTag = string.Empty;
        public FoodTypeFlags foodType = FoodTypeFlags.None;
        public FoodPreferability foodPreferability = FoodPreferability.Undefined;
        public int minCount = 1;
        public bool countStackCount = true;

        public string GetDisplayLabel()
        {
            if (!string.IsNullOrWhiteSpace(labelKey))
                return labelKey.Translate();

            return matchMode.ToString();
        }
    }
}
