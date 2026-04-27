using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    public static partial class Patch_PawnRenderTree
    {
        // ─────────────────────────────────────────────
        // 节点隐藏状态存储
        // ─────────────────────────────────────────────

        // 存储隐藏节点状态（按渲染树分组），避免跨 Pawn 引用污染与失效引用
        private static ConditionalWeakTable<PawnRenderTree, HashSet<PawnRenderNode>> hiddenNodesByTree =
            new ConditionalWeakTable<PawnRenderTree, HashSet<PawnRenderNode>>();

        // 保存被清空节点的原始 Graphics[] 数组，供恢复时使用
        private static ConditionalWeakTable<PawnRenderNode, Graphic[]> savedGraphicsByNode =
            new ConditionalWeakTable<PawnRenderNode, Graphic[]>();

        private static HashSet<PawnRenderNode> GetHiddenSet(PawnRenderTree tree)
        {
            return hiddenNodesByTree.GetOrCreateValue(tree);
        }

        // ─────────────────────────────────────────────
        // 隐藏 / 恢复 节点
        // ─────────────────────────────────────────────

        /// <summary>
        /// 隐藏单个节点（用于路径匹配后的隐藏）
        /// </summary>
        private static void HideNode(PawnRenderNode node)
        {
            if (node?.tree == null)
                return;

            var hidden = GetHiddenSet(node.tree);
            if (!hidden.Add(node))
                return;

            ClearNodeGraphicsCache(node);
            HideChildNodes(node, hidden);
        }

        /// <summary>
        /// 通过标签名隐藏节点
        /// </summary>
        private static void HideNodeByTagName(Dictionary<PawnRenderNodeTagDef, PawnRenderNode> nodesByTag, string tagName)
        {
            foreach (var kvp in nodesByTag)
            {
                if (kvp.Key.defName == tagName && kvp.Value != null)
                {
                    try { HideNode(kvp.Value); }
                    catch (Exception ex)
                    {
                        Log.Warning($"[CharacterStudio] 隐藏节点 {tagName} 时出错: {ex.Message}");
                    }
                }
            }
        }

        private static void ClearNodeGraphicsCache(PawnRenderNode node)
        {
            var field = GetGraphicsField();
            if (field == null) return;

            try
            {
                var val = field.GetValue(node);
                if (val is List<Graphic> list)
                {
                    if (list.Count > 0)
                    {
                        savedGraphicsByNode.Remove(node);
                        savedGraphicsByNode.Add(node, list.ToArray());
                        list.Clear();
                    }
                }
                else if (val is Graphic[] arr)
                {
                    if (arr.Length > 0)
                    {
                        savedGraphicsByNode.Remove(node);
                        savedGraphicsByNode.Add(node, arr);
                        ClearNodeGraphicsCacheDirect(node);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 清空图形缓存出错 ({node}): {ex.Message}");
            }
        }

        private static void RestoreNodeGraphicsCache(PawnRenderNode node)
        {
            var field = GetGraphicsField();
            if (field == null) return;

            try
            {
                if (savedGraphicsByNode.TryGetValue(node, out var saved))
                {
                    var val = field.GetValue(node);
                    if (val is List<Graphic> list)
                    {
                        list.Clear();
                        list.AddRange(saved);
                    }
                    else
                    {
                        ReplaceNodeGraphicsCache(node, saved != null && saved.Length > 0 ? saved[0] : null);
                    }
                    savedGraphicsByNode.Remove(node);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 恢复图形缓存出错 ({node}): {ex.Message}");
            }
        }

        /// <summary>
        /// 递归隐藏子节点（跳过 PawnRenderNode_Custom）
        /// </summary>
        private static void HideChildNodes(PawnRenderNode node, HashSet<PawnRenderNode> hidden)
        {
            if (node.children == null) return;

            foreach (var child in node.children)
            {
                if (child == null) continue;
                if (child is PawnRenderNode_Custom) continue;

                // 绝不连坐隐藏服装和武器，只有当 hideVanillaApparel 配置控制时才处理
                if (IsApparelLikeNode(child)) continue;
                if (child.Props?.workerClass != null && child.Props.workerClass.Name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                if (hidden.Add(child))
                {
                    ClearNodeGraphicsCache(child);
                    HideChildNodes(child, hidden);
                }
            }
        }

        // ─────────────────────────────────────────────
        // Body / Apparel 启发式隐藏
        // ─────────────────────────────────────────────

        private static void HideVanillaBodyFallback(PawnRenderTree tree, Dictionary<PawnRenderNodeTagDef, PawnRenderNode> nodesByTag)
        {
            if (tree?.rootNode == null) return;

            string[] commonBodyTags = { "Torso", "Core", "BodyBase", "NakedBody", "AlienBody", "Body" };
            foreach (var tag in commonBodyTags)
                HideNodeByTagName(nodesByTag, tag);

            HideBodyLikeNodesRecursive(tree.rootNode);
        }

        private static void HideBodyLikeNodesRecursive(PawnRenderNode node)
        {
            if (node == null) return;
            if (!(node is PawnRenderNode_Custom) && IsBodyLikeNode(node))
                HideNode(node);

            if (node.children == null) return;
            foreach (var child in node.children)
            {
                if (child == null) continue;
                HideBodyLikeNodesRecursive(child);
            }
        }

        private static bool IsBodyLikeNode(PawnRenderNode node)
        {
            var props = node.Props;
            string tag = props?.tagDef?.defName ?? string.Empty;
            string workerName = props?.workerClass?.Name ?? string.Empty;
            string label = node.ToString() ?? string.Empty;

            if (tag.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                tag.IndexOf("Headgear", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workerName.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workerName.IndexOf("Headgear", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return tag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tag.IndexOf("Torso", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tag.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   label.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   label.IndexOf("Torso", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void HideApparelLikeNodesRecursive(PawnRenderNode node)
        {
            if (node == null) return;
            
            // 允许皮肤系统主动明确地隐藏 apparel / headgear 节点。
            // 因为现在隐藏已经是完全动态、基于单树、且利用 ClearMat 的 0 污染方案，
            // 我们可以放心地移除原先为了防蓝图污染而加入的锁定限制！
            if (!(node is PawnRenderNode_Custom) && IsApparelLikeNode(node))
            {
                HideNode(node);
            }

            if (node.children == null) return;
            foreach (var child in node.children)
            {
                if (child == null) continue;
                HideApparelLikeNodesRecursive(child);
            }
        }

        private static bool IsApparelLikeNode(PawnRenderNode node)
        {
            if (node.apparel != null) return true;

            var props = node.Props;
            string tag = props?.tagDef?.defName ?? string.Empty;
            string workerName = props?.workerClass?.Name ?? string.Empty;

            return tag.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tag.IndexOf("Headgear", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   workerName.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   workerName.IndexOf("Headgear", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ─────────────────────────────────────────────
        // 按 TexPath 隐藏原版节点（跨路径兜底）
        // ─────────────────────────────────────────────

        private static void HideVanillaNodesByImportedTexPaths(PawnRenderTree tree, PawnSkinDef skinDef)
        {
            if (tree?.rootNode == null || skinDef?.layers == null || skinDef.layers.Count == 0) return;

            var importedTexPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in skinDef.layers)
            {
                if (layer == null || string.IsNullOrEmpty(layer.texPath)) continue;
                // 关键修复：只有可见的图层才应该隐藏原版纹理
                if (!layer.visible) continue;
                if (layer.texPath == "Dynamic/Unknown" || layer.texPath == "Unknown" ||
                    layer.texPath.StartsWith("Dynamic/", StringComparison.OrdinalIgnoreCase))
                    continue;
                importedTexPaths.Add(NormalizeTexPath(layer.texPath));
            }

            if (importedTexPaths.Count == 0) return;
            HideVanillaNodesByTexPathRecursive(tree.rootNode, tree.pawn, importedTexPaths);
        }

        private static void HideVanillaNodesByTexPathRecursive(PawnRenderNode node, Pawn? pawn, HashSet<string> importedTexPaths)
        {
            if (node == null) return;

            if (!(node is PawnRenderNode_Custom))
            {
                // 绝对禁止通过贴图路径匹配隐藏 vanilla apparel / headgear 节点。
                if (IsApparelLikeNode(node)) goto recurse;

                string nodeTexPath = ResolveNodeTexturePathForHide(node, pawn);
                if (!string.IsNullOrEmpty(nodeTexPath) && importedTexPaths.Contains(nodeTexPath))
                    HideNode(node);
            }

            recurse:
            if (node.children == null) return;
            foreach (var child in node.children)
            {
                if (child == null) continue;
                HideVanillaNodesByTexPathRecursive(child, pawn, importedTexPaths);
            }
        }

        private static string ResolveNodeTexturePathForHide(PawnRenderNode node, Pawn? pawn)
        {
            if (pawn != null)
            {
                try
                {
                    var g = node.GraphicFor(pawn);
                    if (g != null && !string.IsNullOrEmpty(g.path))
                        return NormalizeTexPath(g.path);
                }
                catch { }
            }

            try
            {
                var primary = node.PrimaryGraphic;
                if (primary != null && !string.IsNullOrEmpty(primary.path))
                    return NormalizeTexPath(primary.path);
            }
            catch { }

            try
            {
                var graphics = node.Graphics;
                if (graphics != null)
                {
                    foreach (var g in graphics)
                    {
                        if (g != null && !string.IsNullOrEmpty(g.path))
                            return NormalizeTexPath(g.path);
                    }
                }
            }
            catch { }

            return NormalizeTexPath(node.Props?.texPath ?? string.Empty);
        }

        private static string NormalizeTexPath(string path)
            => string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').Trim();

        private static IEnumerable<string> CollectRenderFixHiddenPaths(Pawn? pawn)
        {
            if (pawn == null)
            {
                yield break;
            }

            foreach (CharacterRenderFixPatch patch in RenderFixPatchRegistry.GetApplicablePatches(pawn))
            {
                if (patch?.hideNodePaths == null)
                {
                    continue;
                }

                foreach (string nodePath in patch.hideNodePaths)
                {
                    if (!string.IsNullOrWhiteSpace(nodePath))
                    {
                        yield return nodePath.Trim();
                    }
                }
            }
        }

        // ─────────────────────────────────────────────
        // 公共状态管理
        // ─────────────────────────────────────────────

        /// <summary>清除隐藏状态（皮肤切换时恢复）</summary>
        public static void ClearHiddenNodes()
        {
            hiddenNodesByTree = new ConditionalWeakTable<PawnRenderTree, HashSet<PawnRenderNode>>();
            savedGraphicsByNode = new ConditionalWeakTable<PawnRenderNode, Graphic[]>();
        }

        private static void RestoreAndRemoveHiddenForTree(PawnRenderTree tree)
        {
            if (tree == null) return;

            if (hiddenNodesByTree.TryGetValue(tree, out var oldHidden))
            {
                foreach (var oldNode in oldHidden)
                {
                    if (oldNode != null)
                        RestoreNodeGraphicsCache(oldNode);
                }
            }

            hiddenNodesByTree.Remove(tree);
        }
    }
}
