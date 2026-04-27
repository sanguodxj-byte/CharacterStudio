using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;

namespace CharacterStudio.Design
{
    /// <summary>
    /// 第一阶段最小编译器：
    /// 将编辑期 CharacterDesignDocument 编译为可保存/可应用的 PawnSkinDef。
    /// 当前实现以无损克隆为基础，并补充运行时需要的装备默认值与技能编译规则。
    /// </summary>
    public static class CharacterDesignCompiler
    {
        public static PawnSkinDef CompileRuntimeSkin(CharacterDesignDocument? document)
        {
            if (document == null)
            {
                return new PawnSkinDef();
            }

            document.SyncMetadataFromRuntimeSkin();

            var runtimeSkin = document.runtimeSkin?.Clone() ?? new PawnSkinDef();
            ApplyNodeRules(document, runtimeSkin);
            ApplyEquipmentVisualLayers(document, runtimeSkin);

            return runtimeSkin;
        }

        private static void ApplyNodeRules(CharacterDesignDocument document, PawnSkinDef runtimeSkin)
        {
            runtimeSkin.hiddenPaths ??= new List<string>();
            runtimeSkin.layers ??= new List<PawnLayerConfig>();

            foreach (var rule in document.nodeRules ?? new List<CharacterNodeRule>())
            {
                if (rule == null || rule.targetNode == null)
                {
                    continue;
                }

                switch (rule.operationType)
                {
                    case CharacterNodeOperationType.Hide:
                        ApplyHideRule(rule, runtimeSkin);
                        break;
                    case CharacterNodeOperationType.Attach:
                        ApplyAttachRule(rule, runtimeSkin);
                        break;
                }
            }
        }

        private static void ApplyHideRule(CharacterNodeRule rule, PawnSkinDef runtimeSkin)
        {
            string hiddenPath = rule.targetNode?.exactNodePath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(hiddenPath))
            {
                return;
            }

            if (!runtimeSkin.hiddenPaths.Any(path => string.Equals(path, hiddenPath, StringComparison.OrdinalIgnoreCase)))
            {
                runtimeSkin.hiddenPaths.Add(hiddenPath);
            }
        }

        private static void ApplyAttachRule(CharacterNodeRule rule, PawnSkinDef runtimeSkin)
        {
            var attachedLayer = rule.attachedLayer?.Clone();
            if (attachedLayer == null)
            {
                return;
            }

            string anchorPath = rule.targetNode?.exactNodePath ?? string.Empty;
            string anchorTag = rule.targetNode?.terminalTag ?? string.Empty;

            if (string.IsNullOrWhiteSpace(attachedLayer.anchorPath) && !string.IsNullOrWhiteSpace(anchorPath))
            {
                attachedLayer.anchorPath = anchorPath;
            }

            if (string.IsNullOrWhiteSpace(attachedLayer.anchorTag) && !string.IsNullOrWhiteSpace(anchorTag))
            {
                attachedLayer.anchorTag = anchorTag;
            }

            if (runtimeSkin.layers.Any(existing => LayerSemanticallyEquals(existing, attachedLayer)))
            {
                return;
            }

            runtimeSkin.layers.Add(attachedLayer);
        }

        /// <summary>
        /// 将装备编辑器中的视觉数据编译为渲染图层，
        /// 使装备纹理在编辑器预览和运行时渲染树注入中可见。
        /// </summary>
        private static void ApplyEquipmentVisualLayers(CharacterDesignDocument document, PawnSkinDef runtimeSkin)
        {
            var equipments = document.characterDefinition?.equipments;
            if (equipments == null || equipments.Count == 0)
            {
                return;
            }

            runtimeSkin.layers ??= new List<PawnLayerConfig>();

            // Remove all previously compiled equipment layers so that deleted/renamed
            // equipment does not leave stale entries in the runtime skin.
            runtimeSkin.layers.RemoveAll(existing =>
                existing != null &&
                existing.layerName != null &&
                existing.layerName.StartsWith(EquipmentLayerMarker.Prefix, StringComparison.OrdinalIgnoreCase));

            foreach (var equipment in equipments)
            {
                if (equipment == null || !equipment.HasRenderTexture())
                {
                    continue;
                }

                equipment.renderData.EnsureDefaults(
                    equipment.GetDisplayLabel(),
                    equipment.renderData.GetResolvedTexPath(),
                    equipment.renderData.maskTexPath ?? string.Empty,
                    equipment.renderData.shaderDefName ?? "Cutout");

                var layerConfig = equipment.renderData.ToPawnLayerConfig();
                layerConfig.layerName = $"{EquipmentLayerMarker.Prefix} {layerConfig.layerName}";

                runtimeSkin.layers.Add(layerConfig);
            }
        }

        private static bool LayerSemanticallyEquals(PawnLayerConfig? a, PawnLayerConfig? b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return string.Equals(a.layerName ?? string.Empty, b.layerName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.texPath ?? string.Empty, b.texPath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.anchorPath ?? string.Empty, b.anchorPath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.anchorTag ?? string.Empty, b.anchorTag ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && a.drawOrder.Equals(b.drawOrder);
        }

    }
}
