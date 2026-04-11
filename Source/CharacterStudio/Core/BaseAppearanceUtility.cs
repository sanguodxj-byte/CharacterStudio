using System;
using System.Collections.Generic;
using Verse;

namespace CharacterStudio.Core
{
    public static class BaseAppearanceUtility
    {
        /// <summary>获取槽位对应的原版渲染树 tag 名称</summary>
        public static string GetAnchorTag(BaseAppearanceSlotType slotType)
        {
            switch (slotType)
            {
                case BaseAppearanceSlotType.Body:  return "Body";
                case BaseAppearanceSlotType.Head:  return "Head";
                case BaseAppearanceSlotType.Hair:  return "Hair";
                case BaseAppearanceSlotType.Beard: return "Beard";
                default: return "Head";
            }
        }

        /// <summary>获取槽位的基础绘制顺序</summary>
        public static float GetBaseDrawOrder(BaseAppearanceSlotType slotType)
        {
            switch (slotType)
            {
                case BaseAppearanceSlotType.Body:  return 0f;
                case BaseAppearanceSlotType.Head:  return 50f;
                case BaseAppearanceSlotType.Hair:  return 80f;
                case BaseAppearanceSlotType.Beard: return 78f;
                default: return 70f;
            }
        }

        /// <summary>获取槽位的显示名称</summary>
        public static string GetDisplayName(BaseAppearanceSlotType slotType)
        {
            string key = $"CS_Studio_BaseSlot_{slotType}";
            return key.CanTranslate() ? key.Translate().ToString() : slotType.ToString();
        }

        /// <summary>构建合成图层列表（供自定义图层注入使用）</summary>
        public static IEnumerable<PawnLayerConfig> BuildSyntheticLayers(PawnSkinDef? skin)
        {
            if (skin?.baseAppearance == null)
                yield break;

            skin.baseAppearance.EnsureAllSlotsExist();
            foreach (var slot in skin.baseAppearance.EnabledSlots())
            {
                yield return slot.ToPawnLayer(
                    $"[Base] {GetDisplayName(slot.slotType)}",
                    GetAnchorTag(slot.slotType),
                    GetBaseDrawOrder(slot.slotType),
                    skin.baseAppearance.drawSizeScale);
            }
        }

        /// <summary>判断图层是否为基础槽位的合成图层</summary>
        public static bool IsBaseSyntheticLayer(PawnLayerConfig? layer)
        {
            return layer != null &&
                   !string.IsNullOrEmpty(layer.layerName) &&
                   layer.layerName.StartsWith("[Base] ", StringComparison.Ordinal);
        }
    }
}
