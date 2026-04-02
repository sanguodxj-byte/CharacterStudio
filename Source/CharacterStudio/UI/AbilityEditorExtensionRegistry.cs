using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
using UnityEngine;

namespace CharacterStudio.UI
{
    public delegate void AbilityEditorExtensionDrawer(Rect rect, ModularAbilityDef ability, Action<bool> markDirty);

    public sealed class AbilityEditorExtensionPanel
    {
        public string id = string.Empty;
        public string label = string.Empty;
        public AbilityEditorExtensionDrawer? drawer;
    }

    /// <summary>
    /// 技能编辑器扩展注册表。
    /// 允许外部模组以最小耦合方式追加自定义面板，而不必侵入内建标签页实现。
    /// </summary>
    public static class AbilityEditorExtensionRegistry
    {
        private static readonly Dictionary<string, AbilityEditorExtensionPanel> PanelsById =
            new Dictionary<string, AbilityEditorExtensionPanel>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<AbilityEditorExtensionPanel> Panels => PanelsById.Values;

        public static void RegisterPanel(string id, string label, AbilityEditorExtensionDrawer drawer)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Panel id cannot be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Panel label cannot be empty.", nameof(label));
            }

            if (drawer == null)
            {
                throw new ArgumentNullException(nameof(drawer));
            }

            string normalizedId = id.Trim();
            PanelsById[normalizedId] = new AbilityEditorExtensionPanel
            {
                id = normalizedId,
                label = label.Trim(),
                drawer = drawer
            };
        }

        public static bool TryGetPanel(string id, out AbilityEditorExtensionPanel? panel)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                panel = null;
                return false;
            }

            return PanelsById.TryGetValue(id.Trim(), out panel);
        }
    }
}
