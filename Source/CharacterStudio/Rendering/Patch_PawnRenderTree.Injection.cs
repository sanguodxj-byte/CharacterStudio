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
        // 公共刷新 / 重建入口
        // ─────────────────────────────────────────────

        /// <summary>刷新指定 Pawn 的隐藏节点（皮肤变更时调用）</summary>
        public static void RefreshHiddenNodes(Pawn pawn)
        {
            if (pawn?.Drawer?.renderer?.renderTree == null) return;

            var tree = pawn.Drawer.renderer.renderTree;
            RestoreAndRemoveHiddenForTree(tree);
            RemoveAllCustomNodes(tree);

            var skinComp = pawn.GetComp<Core.CompPawnSkin>();
            var skinDef  = skinComp?.ActiveSkin;

            if (skinDef != null)
            {
                ProcessVanillaHiding(tree, skinDef, skinDef.hideVanillaHead, skinDef.hideVanillaHair, out _);

                // 修复：恢复与 TrySetupGraphIfNeeded_Postfix 相同的调用顺序：
                // BaseAppearance 覆写必须在自定义图层注入之前，
                // 否则手动刷新后槽位贴图（Body/Head/Hair）会丢失。
                ApplyBaseAppearanceOverrides(tree, pawn, skinDef);

                bool anyNodesInjected = InjectCustomLayers(tree, pawn, skinDef);

                if (anyNodesInjected && IsGraphicsReadyForVanillaNodes(tree))
                    HideVanillaNodesByImportedTexPaths(tree, skinDef);
            }

            try { tree.SetDirty(); }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 刷新渲染树状态时出错: {ex.Message}");
            }
        }

        /// <summary>强制重建 Pawn 的渲染树</summary>
        public static void ForceRebuildRenderTree(Pawn pawn)
        {
            if (pawn?.Drawer?.renderer == null) return;

            try
            {
                var tree = renderTreeField_Cached?.GetValue(pawn.Drawer.renderer) as PawnRenderTree;
                if (tree != null)
                {
                    setDirtyMethod_Cached?.Invoke(tree, null);
                    setupCompleteField_Cached?.SetValue(tree, false);
                }
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 强制重建渲染树时出错: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // 自定义节点增删
        // ─────────────────────────────────────────────

        private static void RemoveAllCustomNodes(PawnRenderTree tree)
        {
            if (tree?.rootNode == null) return;
            RemoveCustomNodesRecursive(tree.rootNode);
        }

        private static void RemoveCustomNodesRecursive(PawnRenderNode node)
        {
            if (node.children == null || node.children.Length == 0) return;

            bool hasCustomChild = false;
            foreach (var child in node.children)
            {
                if (child is PawnRenderNode_Custom)
                {
                    hasCustomChild = true;
                    RuntimeAssetLoader.UnregisterNodeOffsets(child.GetHashCode());
                }
                else
                {
                    RemoveCustomNodesRecursive(child);
                }
            }

            if (hasCustomChild)
            {
                var newChildren = new List<PawnRenderNode>();
                foreach (var child in node.children)
                    if (!(child is PawnRenderNode_Custom))
                        newChildren.Add(child);
                node.children = newChildren.ToArray();
            }
        }

        private static void CheckForCustomNodes(PawnRenderNode? node, ref bool found)
        {
            if (node == null || found) return;
            if (node is PawnRenderNode_Custom) { found = true; return; }
            if (node.children != null)
                foreach (var child in node.children)
                {
                    CheckForCustomNodes(child, ref found);
                    if (found) return;
                }
        }

        // ─────────────────────────────────────────────
        // 图层注入核心
        // ─────────────────────────────────────────────

        private static void InjectCustomLayersIfNeeded(PawnRenderTree tree, Pawn pawn, PawnSkinDef skinDef)
        {
            int configuredLayerCount = (skinDef.layers?.Count ?? 0) +
                                       BaseAppearanceUtility.BuildSyntheticLayers(skinDef).Count();
            if (configuredLayerCount == 0) return;

            bool hasCustomNodes = false;
            CheckForCustomNodes(tree.rootNode, ref hasCustomNodes);
            if (!hasCustomNodes)
            {
                var injected = InjectCustomLayers(tree, pawn, skinDef);
                if (injected)
                    Log.Message($"[CharacterStudio] 注入了 {configuredLayerCount} 个自定义图层");
            }
        }

        /// <summary>注入自定义图层，anchorPath 优先于 anchorTag</summary>
        private static bool InjectCustomLayers(PawnRenderTree tree, Pawn pawn, PawnSkinDef skinDef)
        {
            var allLayers = skinDef.layers ?? new List<PawnLayerConfig>();
            if (allLayers.Count == 0) return false;

            bool anyNodesInjected = false;

            foreach (var layer in allLayers)
            {
                if (!layer.visible) continue;
                if (string.IsNullOrEmpty(layer.texPath)) continue;
                if (layer.texPath == "Dynamic/Unknown" || layer.texPath == "Error" ||
                    layer.texPath == "No Graphic (Logic Only)" ||
                    layer.texPath.Contains("Unknown") || layer.texPath.StartsWith("Dynamic/")) continue;

                try
                {
                    var props = CreateNodeProperties(layer);

                    // 优先通过 anchorPath 定位
                    PawnRenderNode? parentNode = null;
                    if (!string.IsNullOrEmpty(layer.anchorPath))
                    {
                        parentNode = FindNodeByPath(tree, layer.anchorPath);
                        if (parentNode == null)
                            Log.Warning($"[CharacterStudio] 无法通过路径找到锚点: {layer.anchorPath}，回退到 anchorTag");
                    }

                    if (parentNode == null)
                        parentNode = FindParentNode(tree, layer.anchorTag);

                    // 智能回退：anchorTag 找不到时按类型推断
                    if (parentNode == tree.rootNode && layer.anchorTag != "Root")
                    {
                        if (layer.anchorTag.Contains("Head") || layer.anchorTag.Contains("Eye") ||
                            layer.anchorTag.Contains("Mouth") || layer.anchorTag.Contains("Face"))
                        {
                            var headNode = FindParentNode(tree, "Head");
                            if (headNode != tree.rootNode) { parentNode = headNode; }
                        }
                        else if (layer.anchorTag.Contains("Body") || layer.anchorTag.Contains("Apparel"))
                        {
                            var bodyNode = FindParentNode(tree, "Body");
                            if (bodyNode != tree.rootNode) { parentNode = bodyNode; }
                        }
                    }

                    if (parentNode == null)
                    {
                        Log.Warning($"[CharacterStudio] 无法找到锚点节点: {layer.anchorPath} / {layer.anchorTag}");
                        continue;
                    }

                    var node = (PawnRenderNode)Activator.CreateInstance(
                        props.nodeClass,
                        new object[] { pawn, props, tree }
                    );

                    if (node is PawnRenderNode_Custom customNode)
                        customNode.config = layer;

                    if (node != null)
                    {
                        node.Props.drawSize = layer.scale;
                        node.parent = parentNode;
                        AddChildToNode(parentNode, node);
                        anyNodesInjected = true;

                        node.debugOffset = layer.offset;

                        if (layer.offsetEast != Vector3.zero)
                            RuntimeAssetLoader.RegisterNodeOffsetEast(node.GetHashCode(), layer.offsetEast);
                        if (layer.offsetNorth != Vector3.zero)
                            RuntimeAssetLoader.RegisterNodeOffsetNorth(node.GetHashCode(), layer.offsetNorth);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] 注入图层 {layer.layerName} 时出错: {ex}");
                }
            }

            if (anyNodesInjected)
                ReinitializeNodeAncestors(tree);

            return anyNodesInjected;
        }

        // ─────────────────────────────────────────────
        // 节点属性 / 锚点 / 子节点辅助
        // ─────────────────────────────────────────────

        private static PawnRenderNodeProperties CreateNodeProperties(PawnLayerConfig layer)
        {
            var props = new PawnRenderNodeProperties
            {
                texPath      = layer.texPath,
                workerClass  = layer.workerClass ?? typeof(PawnRenderNodeWorker_CustomLayer),
                nodeClass    = typeof(PawnRenderNode_Custom),
                baseLayer    = layer.drawOrder,
                flipGraphic  = layer.flipHorizontal,
                rotDrawMode  = layer.rotDrawMode,
                drawSize     = layer.scale,
                debugLabel   = layer.layerName
            };

            switch (layer.colorSource)
            {
                case LayerColorSource.PawnHair:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Hair; break;
                case LayerColorSource.PawnSkin:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Skin; break;
                case LayerColorSource.White:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Custom;
                    props.color = UnityEngine.Color.white; break;
                default:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Custom;
                    props.color = layer.customColor; break;
            }

            return props;
        }

        private static PawnRenderNode? FindParentNode(PawnRenderTree tree, string anchorTag)
        {
            if (string.IsNullOrEmpty(anchorTag)) return tree.rootNode;

            var nodesByTagField = AccessTools.Field(typeof(PawnRenderTree), "nodesByTag");
            if (nodesByTagField == null) return tree.rootNode;

            var nodesByTag = nodesByTagField.GetValue(tree) as Dictionary<PawnRenderNodeTagDef, PawnRenderNode>;
            if (nodesByTag == null) return tree.rootNode;

            foreach (var kvp in nodesByTag)
                if (kvp.Key.defName == anchorTag)
                    return kvp.Value;

            return tree.rootNode;
        }

        private static void AddChildToNode(PawnRenderNode parent, PawnRenderNode child)
        {
            if (parent.children == null)
            {
                parent.children = new PawnRenderNode[] { child };
            }
            else
            {
                var newChildren = new PawnRenderNode[parent.children.Length + 1];
                Array.Copy(parent.children, newChildren, parent.children.Length);
                newChildren[parent.children.Length] = child;
                parent.children = newChildren;
            }
        }

        // ─────────────────────────────────────────────
        // 节点祖先重建
        // ─────────────────────────────────────────────

        private static void ReinitializeNodeAncestors(PawnRenderTree tree)
        {
            try
            {
                if (nodeAncestorsField_Cached == null) return;

                var nodeAncestors = nodeAncestorsField_Cached.GetValue(tree)
                                    as Dictionary<PawnRenderNode, List<PawnRenderNode>>
                                    ?? new Dictionary<PawnRenderNode, List<PawnRenderNode>>();

                nodeAncestors.Clear();
                nodeAncestorsField_Cached.SetValue(tree, nodeAncestors);

                if (initAncestorsMethod_Cached != null)
                    initAncestorsMethod_Cached.Invoke(tree, null);
                else
                    ManuallyInitializeAncestors(tree.rootNode, nodeAncestors, new List<PawnRenderNode>());
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 重新初始化节点祖先时出错: {ex.Message}");
            }
        }

        private static void ManuallyInitializeAncestors(
            PawnRenderNode? node,
            Dictionary<PawnRenderNode, List<PawnRenderNode>> nodeAncestors,
            List<PawnRenderNode> currentPath)
        {
            if (node == null) return;

            var pathWithCurrent = new List<PawnRenderNode>(currentPath) { node };
            nodeAncestors[node] = new List<PawnRenderNode>(pathWithCurrent);

            if (node.children != null)
                foreach (var child in node.children)
                    ManuallyInitializeAncestors(child, nodeAncestors, pathWithCurrent);
        }
    }
}
