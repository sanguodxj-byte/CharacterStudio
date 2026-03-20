using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using CharacterStudio.Design;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 图层树 - 辅助方法
        // ─────────────────────────────────────────────

        private static string GetDefaultLayerLabel(int index)
        {
            return "CS_Studio_Layer_DefaultName".Translate(index + 1);
        }

        private static string GetMountedLayerLabel(RenderNodeSnapshot node)
        {
            return "CS_Studio_Layer_MountedOn".Translate(node.tagDefName ?? node.workerClass);
        }

        /// <summary>
        /// 获取节点显示名称
        /// </summary>
        private string GetNodeDisplayName(RenderNodeSnapshot node)
        {
            if (!string.IsNullOrEmpty(node.tagDefName) && node.tagDefName != "Untagged")
            {
                return $"{node.tagDefName}";
            }

            string workerName = node.workerClass;
            if (workerName.Contains("."))
            {
                workerName = workerName.Substring(workerName.LastIndexOf('.') + 1);
            }

            if (workerName.StartsWith("PawnRenderNodeWorker_"))
            {
                workerName = workerName.Substring("PawnRenderNodeWorker_".Length);
            }

            return workerName;
        }

        /// <summary>
        /// 根据节点类型获取颜色
        /// </summary>
        private Color GetNodeColor(RenderNodeSnapshot node)
        {
            if (TryResolveBaseSlotType(node) != null)
            {
                return new Color(0.72f, 0.88f, 1f);
            }

            string worker = node.workerClass;

            if (worker.Contains("Head")) return new Color(1f, 0.8f, 0.6f);
            if (worker.Contains("Body")) return new Color(0.8f, 1f, 0.8f);
            if (worker.Contains("Hair")) return new Color(1f, 0.9f, 0.5f);
            if (worker.Contains("Apparel")) return new Color(0.7f, 0.8f, 1f);
            if (worker.Contains("Attachment")) return new Color(1f, 0.7f, 1f);

            return Color.white;
        }

        private BaseAppearanceSlotType? TryResolveBaseSlotType(RenderNodeSnapshot node)
        {
            string tag = node.tagDefName ?? string.Empty;
            string label = node.debugLabel ?? string.Empty;
            string texPath = node.texPath ?? string.Empty;

            if (ContainsAny(tag, label, texPath, "body")) return BaseAppearanceSlotType.Body;
            if (ContainsAny(tag, label, texPath, "head")) return BaseAppearanceSlotType.Head;
            if (ContainsAny(tag, label, texPath, "hair")) return BaseAppearanceSlotType.Hair;
            if (ContainsAny(tag, label, texPath, "beard")) return BaseAppearanceSlotType.Beard;

            return null;
        }

        private static bool ContainsAny(string a, string b, string c, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if ((!string.IsNullOrEmpty(a) && a.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(b) && b.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c) && c.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }

            return false;
        }

        private CharacterNodeReference CreateNodeReference(RenderNodeSnapshot node)
        {
            string sourceRaceDefName = workingDocument?.preferredPreviewRaceDefName ?? string.Empty;
            return CharacterNodeReference.FromRuntimeAnchor(
                node.uniqueNodePath,
                node.tagDefName,
                node.texPath,
                node.workerClass,
                -1,
                sourceRaceDefName,
                string.Empty);
        }

        private void UpsertHideNodeRule(RenderNodeSnapshot node, bool hidden)
        {
            workingDocument.nodeRules ??= new List<CharacterNodeRule>();
            workingDocument.nodeRules.RemoveAll(rule =>
                rule != null &&
                rule.operationType == CharacterNodeOperationType.Hide &&
                rule.targetNode != null &&
                rule.targetNode.MatchesExactPath(node.uniqueNodePath));

            if (hidden)
            {
                workingDocument.nodeRules.Add(CharacterNodeRule.CreateHide(CreateNodeReference(node)));
            }
        }

        private void AppendAttachNodeRule(RenderNodeSnapshot node, PawnLayerConfig layer)
        {
            workingDocument.nodeRules ??= new List<CharacterNodeRule>();
            workingDocument.nodeRules.Add(CharacterNodeRule.CreateAttach(CreateNodeReference(node), layer));
        }

        /// <summary>
        /// 按路径查找节点
        /// </summary>
        private RenderNodeSnapshot? FindNodeByPath(RenderNodeSnapshot root, string path)
        {
            if (root.uniqueNodePath == path)
            {
                return root;
            }

            foreach (var child in root.children)
            {
                var found = FindNodeByPath(child, path);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// 递归收集所有节点快照
        /// </summary>
        private void CollectSnapshots(RenderNodeSnapshot node, List<RenderNodeSnapshot> result)
        {
            if (node == null)
            {
                return;
            }

            result.Add(node);
            foreach (var child in node.children)
            {
                CollectSnapshots(child, result);
            }
        }
    }
}