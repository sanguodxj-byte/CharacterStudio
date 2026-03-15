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
            return nodesByTag.FirstOrDefault(k => k.Key.defName == directTag).Value;
        }
    }
}
