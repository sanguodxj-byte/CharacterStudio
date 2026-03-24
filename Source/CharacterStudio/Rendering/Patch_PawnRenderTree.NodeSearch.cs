using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    public static partial class Patch_PawnRenderTree
    {
        // ─────────────────────────────────────────────
        // NodePath 路径解析与节点查找
        // ─────────────────────────────────────────────

        /// <summary>
        /// 通过 NodePath 路径查找节点，格式: "Root/Body:0/Head:0"
        /// </summary>
        private static PawnRenderNode? FindNodeByPath(PawnRenderTree tree, string path, bool warnOnFail = true)
        {
            if (string.IsNullOrEmpty(path) || tree.rootNode == null) return null;

            try
            {
                var segments = path.Split('/');
                if (segments.Length == 0) return null;

                var (firstTag, _) = ParsePathSegment(segments[0]);
                if (firstTag != "Root")
                {
                    if (warnOnFail) Log.Warning($"[CharacterStudio] NodePath 必须以 'Root' 开头: {path}");
                    return null;
                }

                PawnRenderNode currentNode = tree.rootNode;
                for (int i = 1; i < segments.Length; i++)
                {
                    var (tagName, index) = ParsePathSegment(segments[i]);
                    var matchedChild = FindChildByTagAndIndex(currentNode, tagName, index)
                                   ?? FindChildByTagFirst(currentNode, tagName);
                    if (matchedChild == null)
                    {
                        if (warnOnFail) Log.Warning($"[CharacterStudio] 在路径 '{path}' 中找不到段 '{segments[i]}'");
                        return null;
                    }
                    currentNode = matchedChild;
                }
                return currentNode;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 解析路径 '{path}' 时出错: {ex.Message}");
                return null;
            }
        }

        private static (string tagName, int index) ParsePathSegment(string segment)
        {
            int colonIndex = segment.LastIndexOf(':');
            if (colonIndex > 0 && colonIndex < segment.Length - 1)
            {
                if (int.TryParse(segment.Substring(colonIndex + 1), out int idx))
                    return (segment.Substring(0, colonIndex), idx);
            }
            return (segment, 0);
        }

        private static PawnRenderNode? FindChildByTagAndIndex(PawnRenderNode parent, string tagName, int targetIndex)
        {
            if (parent.children == null) return null;
            int count = 0;
            foreach (var child in parent.children)
            {
                if (child == null) continue;
                if ((child.Props?.tagDef?.defName ?? "Untagged") == tagName)
                {
                    if (count == targetIndex) return child;
                    count++;
                }
            }
            return null;
        }

        private static PawnRenderNode? FindChildByTagFirst(PawnRenderNode parent, string tagName)
        {
            if (parent.children == null) return null;
            foreach (var child in parent.children)
            {
                if (child == null) continue;
                if ((child.Props?.tagDef?.defName ?? "Untagged") == tagName)
                    return child;
            }
            return null;
        }

        /// <summary>
        /// 从 NodePath 提取末段 tag（如 Root/Body:0/Head:0 -> Head）
        /// </summary>
        private static string ExtractTerminalTagFromNodePath(string nodePath)
        {
            if (string.IsNullOrEmpty(nodePath)) return string.Empty;

            var segments = nodePath.Split('/');
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(segments[i])) continue;
                var (tagName, _) = ParsePathSegment(segments[i]);
                if (!string.IsNullOrEmpty(tagName) && !tagName.Equals("Root", StringComparison.OrdinalIgnoreCase))
                    return tagName;
            }
            return string.Empty;
        }

        // ─────────────────────────────────────────────
        // BaseAppearance 槽位节点查找
        // Body/Head/Hair/Beard 通过 nodesByTag 直接查找
        // ─────────────────────────────────────────────

        private static readonly string[] BodyAnchorAliases = { "Body", "AlienBody", "NakedBody", "BodyBase", "Torso", "Core" };
        private static readonly string[] HeadAnchorAliases = { "Head", "AlienHead", "Face", "Skull" };
        private static readonly string[] HairAnchorAliases = { "Hair", "AlienHair", "Fur" };
        private static readonly string[] BeardAnchorAliases = { "Beard", "Moustache" };
        private static readonly string[] ApparelAnchorAliases = { "Apparel", "Body", "AlienBody", "NakedBody", "BodyBase", "Torso", "Core" };
        private static readonly string[] RootAnchorAliases = { "Root" };

        private static PawnRenderNode? FindAnchorNode(PawnRenderTree tree, string? anchorTag)
        {
            if (tree?.rootNode == null)
                return null;

            foreach (string candidate in GetAnchorAliases(anchorTag))
            {
                PawnRenderNode? exactNode = FindNodeByExactTag(tree, candidate);
                if (exactNode != null)
                    return exactNode;
            }

            foreach (string candidate in GetAnchorAliases(anchorTag))
            {
                PawnRenderNode? semanticNode = FindNodeBySemanticHintRecursive(tree.rootNode, candidate);
                if (semanticNode != null)
                    return semanticNode;
            }

            return null;
        }

        private static IEnumerable<string> GetAnchorAliases(string? anchorTag)
        {
            if (string.IsNullOrWhiteSpace(anchorTag))
                return RootAnchorAliases;

            if (string.Equals(anchorTag, "Root", StringComparison.OrdinalIgnoreCase))
                return RootAnchorAliases;

            string normalizedAnchorTag = anchorTag!;

            if (normalizedAnchorTag.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) >= 0)
                return ApparelAnchorAliases;

            if (normalizedAnchorTag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Torso", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                return BodyAnchorAliases;

            if (normalizedAnchorTag.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Face", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Mouth", StringComparison.OrdinalIgnoreCase) >= 0)
                return HeadAnchorAliases;

            if (normalizedAnchorTag.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Fur", StringComparison.OrdinalIgnoreCase) >= 0)
                return HairAnchorAliases;

            if (normalizedAnchorTag.IndexOf("Beard", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Moustache", StringComparison.OrdinalIgnoreCase) >= 0)
                return BeardAnchorAliases;

            return new[] { normalizedAnchorTag };
        }

        private static PawnRenderNode? FindNodeByExactTag(PawnRenderTree tree, string tagName)
        {
            if (tree == null || string.IsNullOrWhiteSpace(tagName))
                return null;

            var nodesByTagField = AccessTools.Field(typeof(PawnRenderTree), "nodesByTag");
            var nodesByTag = nodesByTagField?.GetValue(tree) as Dictionary<PawnRenderNodeTagDef, PawnRenderNode>;
            if (nodesByTag == null)
                return null;

            foreach (var kvp in nodesByTag)
            {
                if (kvp.Key.defName == tagName && kvp.Value != null)
                    return kvp.Value;
            }

            return null;
        }

        private static PawnRenderNode? FindNodeBySemanticHintRecursive(PawnRenderNode? node, string hint)
        {
            if (node == null || string.IsNullOrWhiteSpace(hint))
                return null;

            if (NodeMatchesSemanticHint(node, hint))
                return node;

            if (node.children == null)
                return null;

            foreach (var child in node.children)
            {
                PawnRenderNode? found = FindNodeBySemanticHintRecursive(child, hint);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool NodeMatchesSemanticHint(PawnRenderNode node, string hint)
        {
            string tag = node.Props?.tagDef?.defName ?? string.Empty;
            string workerName = node.Props?.workerClass?.Name ?? string.Empty;
            string label = node.ToString() ?? string.Empty;

            return tag.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0
                || workerName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 根据槽位类型查找对应的渲染节点
        /// </summary>
        private static PawnRenderNode? FindNodeForBaseSlot(
            PawnRenderTree tree,
            Dictionary<PawnRenderNodeTagDef, PawnRenderNode> nodesByTag,
            BaseAppearanceSlotType slotType)
        {
            string? directTag = slotType switch
            {
                BaseAppearanceSlotType.Body  => "Body",
                BaseAppearanceSlotType.Head  => "Head",
                BaseAppearanceSlotType.Hair  => "Hair",
                BaseAppearanceSlotType.Beard => "Beard",
                _ => null
            };

            if (directTag == null) return null;
            return FindAnchorNode(tree, directTag);
        }
    }
}