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

                bool anyNodesInjected = InjectCustomLayers(tree, pawn, skinDef);
                bool anyLayeredFaceInjected = InjectLayeredFaceLayers(tree, pawn, skinDef);

                // 注入眼睛方向覆盖层（仅当配置启用且未被新的分层面部系统接管时）
                InjectEyeDirectionLayer(tree, pawn, skinDef);

                if ((anyNodesInjected || anyLayeredFaceInjected) && IsGraphicsReadyForVanillaNodes(tree))
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
            if (node.children == null || node.children.Length == 0)
                return;

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
                // 先收集需要保留的子节点，再以数组形式写回（与 PawnRenderNode.children 类型一致）
                var newChildren = new List<PawnRenderNode>(node.children.Length);
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
            var allLayers = new List<PawnLayerConfig>();
            if (skinDef.layers != null)
                allLayers.AddRange(skinDef.layers);

            allLayers.AddRange(BaseAppearanceUtility.BuildSyntheticLayers(skinDef));
            allLayers.AddRange(BuildEquipmentVisualLayers(skinDef));

            var carryVisualLayer = BuildWeaponCarryVisualLayer(skinDef);
            if (carryVisualLayer != null)
                allLayers.Add(carryVisualLayer);

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
            bool isExternalTexture = !string.IsNullOrWhiteSpace(layer.texPath)
                && (System.IO.Path.IsPathRooted(layer.texPath)
                    || layer.texPath.StartsWith("/", StringComparison.Ordinal)
                    || System.IO.File.Exists(layer.texPath));

            var props = new PawnRenderNodeProperties
            {
                texPath      = isExternalTexture ? string.Empty : layer.texPath,
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

        private static IEnumerable<PawnLayerConfig> BuildEquipmentVisualLayers(PawnSkinDef skinDef)
        {
            if (skinDef?.equipments == null || skinDef.equipments.Count == 0)
                yield break;

            foreach (var equipment in skinDef.equipments)
            {
                if (equipment == null || !equipment.enabled)
                    continue;

                equipment.EnsureDefaults();

                if (equipment.visual == null || string.IsNullOrWhiteSpace(equipment.visual.texPath))
                    continue;

                var layer = equipment.visual.Clone();
                layer.layerName = string.IsNullOrWhiteSpace(layer.layerName)
                    ? $"[Equipment] {equipment.GetDisplayLabel()}"
                    : $"[Equipment] {layer.layerName}";
                layer.anchorTag = string.IsNullOrWhiteSpace(layer.anchorTag) ? "Apparel" : layer.anchorTag;
                layer.shaderDefName = string.IsNullOrWhiteSpace(layer.shaderDefName) ? "Cutout" : layer.shaderDefName;
                yield return layer;
            }
        }

        private static PawnLayerConfig? BuildWeaponCarryVisualLayer(PawnSkinDef skinDef)
        {
            var carryVisual = skinDef?.weaponRenderConfig?.carryVisual;
            if (carryVisual == null || !carryVisual.enabled)
                return null;

            string texPath = carryVisual.GetAnyTexPath();
            if (string.IsNullOrWhiteSpace(texPath))
                return null;

            return new PawnLayerConfig
            {
                layerName = "[WeaponCarry] State Visual",
                texPath = texPath,
                anchorTag = string.IsNullOrWhiteSpace(carryVisual.anchorTag) ? "Body" : carryVisual.anchorTag,
                offset = Vector3.zero,
                offsetEast = Vector3.zero,
                offsetNorth = Vector3.zero,
                scale = Vector2.one,
                drawOrder = carryVisual.drawOrder,
                workerClass = typeof(PawnRenderNodeWorker_WeaponCarryVisual),
                weaponCarryVisual = carryVisual.Clone()
            };
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

        // ─────────────────────────────────────────────
        // LayeredDynamic 面部分层节点注入
        // ─────────────────────────────────────────────

        private static bool InjectLayeredFaceLayers(PawnRenderTree tree, Pawn pawn, PawnSkinDef skinDef)
        {
            try
            {
                var faceConfig = skinDef?.faceConfig;
                if (faceConfig == null
                    || !faceConfig.enabled
                    || faceConfig.workflowMode != FaceWorkflowMode.LayeredDynamic
                    || !faceConfig.HasAnyLayeredPart())
                    return false;

                PawnRenderNode? parentNode = FindParentNode(tree, "Head");
                if (parentNode == null)
                    return false;

                bool anyInjected = false;
                foreach (LayeredFacePartType partType in GetLayeredFaceInjectionOrder())
                {
                    if (partType == LayeredFacePartType.Overlay)
                    {
                        List<string> overlayIds = faceConfig.GetOrderedOverlayIds();
                        foreach (string overlayId in overlayIds)
                        {
                            string overlayTexPath = faceConfig.GetAnyLayeredPartPath(partType, overlayId);
                            if (string.IsNullOrWhiteSpace(overlayTexPath))
                                continue;

                            int overlayOrder = faceConfig.GetOverlayOrder(overlayId);
                            PawnLayerConfig overlayLayer = BuildLayeredFacePartLayer(faceConfig, partType, overlayTexPath, overlayId, overlayOrder);
                            PawnRenderNodeProperties overlayProps = CreateNodeProperties(overlayLayer);

                            var overlayNode = (PawnRenderNode)Activator.CreateInstance(
                                typeof(PawnRenderNode_Custom),
                                new object[] { pawn, overlayProps, tree });

                            if (overlayNode is not PawnRenderNode_Custom overlayCustomNode)
                                continue;

                            overlayCustomNode.config = overlayLayer;
                            overlayCustomNode.layeredFacePartType = partType;
                            overlayCustomNode.layeredOverlayId = overlayId;
                            overlayCustomNode.layeredOverlayOrder = overlayOrder;
                            overlayNode.parent = parentNode;
                            overlayNode.debugOffset = overlayLayer.offset;

                            AddChildToNode(parentNode, overlayNode);
                            anyInjected = true;
                        }

                        continue;
                    }

                    string texPath = faceConfig.GetAnyLayeredPartPath(partType);
                    if (string.IsNullOrWhiteSpace(texPath))
                        continue;

                    PawnLayerConfig layer = BuildLayeredFacePartLayer(faceConfig, partType, texPath);
                    PawnRenderNodeProperties props = CreateNodeProperties(layer);

                    var node = (PawnRenderNode)Activator.CreateInstance(
                        typeof(PawnRenderNode_Custom),
                        new object[] { pawn, props, tree });

                    if (node is not PawnRenderNode_Custom customNode)
                        continue;

                    customNode.config = layer;
                    customNode.layeredFacePartType = partType;
                    node.parent = parentNode;
                    node.debugOffset = layer.offset;

                    AddChildToNode(parentNode, node);
                    anyInjected = true;
                }

                if (anyInjected)
                    ReinitializeNodeAncestors(tree);

                return anyInjected;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 注入 LayeredDynamic 面部分层节点时出错: {ex.Message}");
                return false;
            }
        }

        private static IEnumerable<LayeredFacePartType> GetLayeredFaceInjectionOrder()
        {
            yield return LayeredFacePartType.Base;
            yield return LayeredFacePartType.Eye;
            yield return LayeredFacePartType.Pupil;
            yield return LayeredFacePartType.UpperLid;
            yield return LayeredFacePartType.LowerLid;
            yield return LayeredFacePartType.Brow;
            yield return LayeredFacePartType.Mouth;
            yield return LayeredFacePartType.Blush;
            yield return LayeredFacePartType.Tear;
            yield return LayeredFacePartType.Sweat;
            yield return LayeredFacePartType.Overlay;
        }

        private static PawnLayerConfig BuildLayeredFacePartLayer(
            PawnFaceConfig faceConfig,
            LayeredFacePartType partType,
            string texPath,
            string overlayId = "",
            int overlayOrder = 0)
        {
            float pupilMoveRange = faceConfig.eyeDirectionConfig?.pupilMoveRange ?? 0f;
            string overlayLabel = string.IsNullOrWhiteSpace(overlayId) ? string.Empty : $"[{overlayId}]";

            return new PawnLayerConfig
            {
                layerName = $"[Face] {partType}{overlayLabel}",
                texPath = texPath,
                anchorTag = "Head",
                anchorPath = string.Empty,
                offset = Vector3.zero,
                offsetEast = Vector3.zero,
                offsetNorth = Vector3.zero,
                scale = Vector2.one,
                rotation = 0f,
                drawOrder = GetLayeredFaceDrawOrder(partType, overlayOrder),
                workerClass = typeof(PawnRenderNodeWorker_CustomLayer),
                shaderDefName = "Cutout",
                colorSource = LayerColorSource.White,
                customColor = Color.white,
                colorTwoSource = LayerColorSource.White,
                customColorTwo = Color.white,
                visible = true,
                role = GetLayeredFaceRole(partType),
                variantLogic = LayerVariantLogic.None,
                useDirectionalSuffix = true,
                eyeRenderMode = partType == LayeredFacePartType.Pupil && pupilMoveRange > 0f
                    ? EyeRenderMode.UvOffset
                    : EyeRenderMode.TextureSwap,
                eyeUvMoveRange = partType == LayeredFacePartType.Pupil ? pupilMoveRange : 0f
            };
        }

        private static LayerRole GetLayeredFaceRole(LayeredFacePartType partType)
        {
            switch (partType)
            {
                case LayeredFacePartType.Base:
                    return LayerRole.Head;
                case LayeredFacePartType.Brow:
                    return LayerRole.Brow;
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                    return LayerRole.Lid;
                case LayeredFacePartType.Pupil:
                    return LayerRole.Eye;
                case LayeredFacePartType.Mouth:
                    return LayerRole.Mouth;
                case LayeredFacePartType.Blush:
                case LayeredFacePartType.Sweat:
                case LayeredFacePartType.Tear:
                case LayeredFacePartType.Overlay:
                    return LayerRole.Emotion;
                default:
                    return LayerRole.Decoration;
            }
        }

        private static float GetLayeredFaceDrawOrder(LayeredFacePartType partType, int overlayOrder = 0)
        {
            switch (partType)
            {
                case LayeredFacePartType.Base:
                    return 50.05f;
                case LayeredFacePartType.Eye:
                    return 50.12f;
                case LayeredFacePartType.Pupil:
                    return 50.14f;
                case LayeredFacePartType.UpperLid:
                    return 50.145f;
                case LayeredFacePartType.LowerLid:
                    return 50.147f;
                case LayeredFacePartType.Brow:
                    return 50.16f;
                case LayeredFacePartType.Mouth:
                    return 50.18f;
                case LayeredFacePartType.Blush:
                    return 50.22f;
                case LayeredFacePartType.Tear:
                    return 50.24f;
                case LayeredFacePartType.Sweat:
                    return 50.26f;
                case LayeredFacePartType.Overlay:
                    return 50.30f + overlayOrder * 0.002f;
                default:
                    return 50.20f;
            }
        }

        // ─────────────────────────────────────────────
        // 眼睛方向覆盖层注入
        // ─────────────────────────────────────────────

        /// <summary>
        /// 当 faceConfig.eyeDirectionConfig 启用时，
        /// 在头部节点下注入一个 PawnRenderNodeWorker_EyeDirection 覆盖层，
        /// 用于渲染当前眼睛方向贴图。
        ///
        /// 空值防护：任何前置条件不满足时直接返回，不抛出异常。
        /// 重复防护：RefreshHiddenNodes 调用链在此方法前已执行 RemoveAllCustomNodes，
        ///           所有 PawnRenderNode_Custom（含眼睛方向层）均已被清除，无需额外处理。
        /// </summary>
        private static void InjectEyeDirectionLayer(PawnRenderTree tree, Pawn pawn, Core.PawnSkinDef skinDef)
        {
            try
            {
                CompPawnSkin? skinComp = pawn?.GetComp<CompPawnSkin>();
                FaceRuntimeCompiledData? compiledData = skinComp?.CurrentFaceRuntimeCompiledData;
                FaceEyeDirectionRuntimeData? eyeData = compiledData?.portraitTrack?.eyeDirection;

                var faceConfig = skinDef?.faceConfig;
                if (eyeData == null || !eyeData.enabled || !eyeData.HasAnyTex())
                    return;

                // LayeredDynamic 已存在 Eye / Pupil 分层时，旧的眼睛方向覆盖层退出，
                // 避免与新的分层面部系统重复绘制。
                if (faceConfig?.workflowMode == FaceWorkflowMode.LayeredDynamic
                    && ((faceConfig.CountLayeredParts(LayeredFacePartType.Eye) > 0)
                        || (faceConfig.CountLayeredParts(LayeredFacePartType.Pupil) > 0)))
                    return;

                // 尝试挂载到头部节点；找不到时回退到根节点
                PawnRenderNode? parentNode = FindParentNode(tree, "Head");
                if (parentNode == null || parentNode == tree.rootNode)
                    parentNode = tree.rootNode;
                if (parentNode == null) return; // rootNode 为 null 时安全退出

                PawnRenderNode resolvedParentNode = parentNode;

                var props = new PawnRenderNodeProperties
                {
                    texPath     = string.Empty, // 贴图由 Worker 动态提供
                    workerClass = typeof(PawnRenderNodeWorker_EyeDirection),
                    nodeClass   = typeof(PawnRenderNode_Custom),
                    baseLayer   = 0.001f,       // 紧贴头部之上
                    debugLabel  = "[CS] EyeDirection",
                };

                object? createdNode = Activator.CreateInstance(
                    typeof(PawnRenderNode_Custom),
                    new object?[] { pawn, props, tree });

                if (createdNode is not PawnRenderNode node)
                    return;

                node.parent = resolvedParentNode;
                AddChildToNode(resolvedParentNode, node);

                ReinitializeNodeAncestors(tree);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 注入眼睛方向覆盖层时出错: {ex.Message}");
            }
        }

    }
}