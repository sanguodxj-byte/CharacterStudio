using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
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
            NormalizeEquipments(runtimeSkin);
            runtimeSkin.abilities = CompileRuntimeAbilities(runtimeSkin);

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

        private static void NormalizeEquipments(PawnSkinDef runtimeSkin)
        {
            runtimeSkin.equipments ??= new List<CharacterEquipmentDef>();

            foreach (var equipment in runtimeSkin.equipments)
            {
                equipment?.EnsureDefaults();
            }
        }

        private static List<ModularAbilityDef> CompileRuntimeAbilities(PawnSkinDef runtimeSkin)
        {
            var sourceAbilities = runtimeSkin.abilities ?? new List<ModularAbilityDef>();
            bool hasEquipmentAbilityBindings = runtimeSkin.equipments != null &&
                runtimeSkin.equipments.Any(e =>
                    e != null &&
                    e.enabled &&
                    e.abilityDefNames != null &&
                    e.abilityDefNames.Count > 0);

            // 若未使用装备绑定技能，则保持旧行为：运行时直接使用皮肤原有技能列表。
            if (!hasEquipmentAbilityBindings)
            {
                return CloneAbilities(sourceAbilities);
            }

            var abilityByDefName = new Dictionary<string, ModularAbilityDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var ability in sourceAbilities)
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.defName))
                {
                    continue;
                }

                if (!abilityByDefName.ContainsKey(ability.defName))
                {
                    abilityByDefName.Add(ability.defName, ability);
                }
            }

            var compiled = new List<ModularAbilityDef>();
            var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var equipments = runtimeSkin.equipments ?? new List<CharacterEquipmentDef>();

            foreach (var equipment in equipments)
            {
                if (equipment == null || !equipment.enabled || equipment.abilityDefNames == null)
                {
                    continue;
                }

                foreach (var defName in equipment.abilityDefNames)
                {
                    if (string.IsNullOrWhiteSpace(defName))
                    {
                        continue;
                    }

                    if (!added.Add(defName))
                    {
                        continue;
                    }

                    if (abilityByDefName.TryGetValue(defName, out var ability))
                    {
                        compiled.Add(ability.Clone());
                    }
                }
            }

            // 保留被热键引用的技能，防止装备技能与热键配置同时使用时丢失能力。
            foreach (var defName in EnumerateHotkeyAbilityNames(runtimeSkin.abilityHotkeys))
            {
                if (string.IsNullOrWhiteSpace(defName))
                {
                    continue;
                }

                if (!added.Add(defName))
                {
                    continue;
                }

                if (abilityByDefName.TryGetValue(defName, out var ability))
                {
                    compiled.Add(ability.Clone());
                }
            }

            return compiled;
        }

        private static IEnumerable<string> EnumerateHotkeyAbilityNames(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(hotkeys.qAbilityDefName))
                yield return hotkeys.qAbilityDefName;
            if (!string.IsNullOrWhiteSpace(hotkeys.wAbilityDefName))
                yield return hotkeys.wAbilityDefName;
            if (!string.IsNullOrWhiteSpace(hotkeys.eAbilityDefName))
                yield return hotkeys.eAbilityDefName;
            if (!string.IsNullOrWhiteSpace(hotkeys.rAbilityDefName))
                yield return hotkeys.rAbilityDefName;
            if (!string.IsNullOrWhiteSpace(hotkeys.wComboAbilityDefName))
                yield return hotkeys.wComboAbilityDefName;
        }

        private static List<ModularAbilityDef> CloneAbilities(IEnumerable<ModularAbilityDef>? abilities)
        {
            var cloned = new List<ModularAbilityDef>();
            if (abilities == null)
            {
                return cloned;
            }

            foreach (var ability in abilities)
            {
                if (ability != null)
                {
                    cloned.Add(ability.Clone());
                }
            }

            return cloned;
        }
    }
}