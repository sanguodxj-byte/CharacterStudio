using System;
using System.Collections.Generic;

namespace CharacterStudio.Core
{
    public static class BaseAppearanceUtility
    {
        public static string GetAnchorTag(BaseAppearanceSlotType slotType)
        {
            switch (slotType)
            {
                case BaseAppearanceSlotType.Body:
                    return "Body";
                case BaseAppearanceSlotType.Head:
                    return "Head";
                case BaseAppearanceSlotType.Hair:
                    return "Hair";
                case BaseAppearanceSlotType.Beard:
                    return "Beard";
                case BaseAppearanceSlotType.Eyes:
                case BaseAppearanceSlotType.Brow:
                case BaseAppearanceSlotType.Mouth:
                case BaseAppearanceSlotType.Nose:
                case BaseAppearanceSlotType.Ear:
                    return "Head";
                default:
                    return "Head";
            }
        }

        public static float GetBaseDrawOrder(BaseAppearanceSlotType slotType)
        {
            switch (slotType)
            {
                case BaseAppearanceSlotType.Body:
                    return 0f;
                case BaseAppearanceSlotType.Head:
                    return 50f;
                case BaseAppearanceSlotType.Hair:
                    return 80f;
                case BaseAppearanceSlotType.Beard:
                    return 78f;
                case BaseAppearanceSlotType.Eyes:
                    return 60f;
                case BaseAppearanceSlotType.Brow:
                    return 61f;
                case BaseAppearanceSlotType.Mouth:
                    return 62f;
                case BaseAppearanceSlotType.Nose:
                    return 63f;
                case BaseAppearanceSlotType.Ear:
                    return 64f;
                default:
                    return 70f;
            }
        }

        public static string GetDisplayName(BaseAppearanceSlotType slotType)
        {
            switch (slotType)
            {
                case BaseAppearanceSlotType.Body:
                    return "Body";
                case BaseAppearanceSlotType.Head:
                    return "Head";
                case BaseAppearanceSlotType.Hair:
                    return "Hair";
                case BaseAppearanceSlotType.Beard:
                    return "Beard";
                case BaseAppearanceSlotType.Eyes:
                    return "Eyes";
                case BaseAppearanceSlotType.Brow:
                    return "Brow";
                case BaseAppearanceSlotType.Mouth:
                    return "Mouth";
                case BaseAppearanceSlotType.Nose:
                    return "Nose";
                case BaseAppearanceSlotType.Ear:
                    return "Ear";
                default:
                    return slotType.ToString();
            }
        }

        public static IEnumerable<PawnLayerConfig> BuildSyntheticLayers(PawnSkinDef? skin)
        {
            if (skin?.baseAppearance == null)
            {
                yield break;
            }

            skin.baseAppearance.EnsureAllSlotsExist();
            foreach (var slot in skin.baseAppearance.EnabledSlots())
            {
                yield return slot.ToPawnLayer(
                    $"[Base] {GetDisplayName(slot.slotType)}",
                    GetAnchorTag(slot.slotType),
                    GetBaseDrawOrder(slot.slotType));
            }
        }

        public static bool IsBaseSyntheticLayer(PawnLayerConfig? layer)
        {
            return layer != null && !string.IsNullOrEmpty(layer.layerName) && layer.layerName.StartsWith("[Base] ", StringComparison.Ordinal);
        }
    }
}
