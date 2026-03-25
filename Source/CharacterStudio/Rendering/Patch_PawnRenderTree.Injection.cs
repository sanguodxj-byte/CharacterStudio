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
                bool expectsInjectedOverrides = HasPotentialInjectedOverrides(pawn, skinDef);

                ProcessVanillaHiding(tree, skinDef, skinDef.hideVanillaHead, skinDef.hideVanillaHair, out _);

                bool anyNodesInjected = InjectCustomLayers(tree, pawn, skinDef);
                bool anyLayeredFaceInjected = InjectLayeredFaceLayers(tree, pawn, skinDef);

                // 注入眼睛方向覆盖层（仅当配置启用且未被新的分层面部系统接管时）
                InjectEyeDirectionLayer(tree, pawn, skinDef);

                if (expectsInjectedOverrides && !anyNodesInjected && !anyLayeredFaceInjected)
                {
                    RestoreAndRemoveHiddenForTree(tree);
                    Log.Warning($"[CharacterStudio] 图层注入失败，已恢复原始渲染节点以避免角色部件被隐藏: {pawn.LabelShortCap}");
                }
                else if ((anyNodesInjected || anyLayeredFaceInjected) && IsGraphicsReadyForVanillaNodes(tree))
                {
                    HideVanillaNodesByImportedTexPaths(tree, skinDef);
                }
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

            RemoveAllCustomNodes(tree);
            var injected = InjectCustomLayers(tree, pawn, skinDef);
            if (injected)
                Log.Message($"[CharacterStudio] 注入了 {configuredLayerCount} 个自定义图层");
        }

        /// <summary>注入自定义图层，anchorPath 优先于 anchorTag</summary>
        private static bool InjectCustomLayers(PawnRenderTree tree, Pawn pawn, PawnSkinDef skinDef)
        {
            var allLayers = new List<PawnLayerConfig>();
            if (skinDef.layers != null)
                allLayers.AddRange(skinDef.layers);

            allLayers.AddRange(BaseAppearanceUtility.BuildSyntheticLayers(skinDef));
            allLayers.AddRange(BuildEquipmentVisualLayers(pawn, skinDef));

            var carryVisualLayer = BuildWeaponCarryVisualLayer(skinDef);
            if (carryVisualLayer != null)
                allLayers.Add(carryVisualLayer);

            if (allLayers.Count == 0) return false;

            bool anyNodesInjected = false;

            foreach (var layer in allLayers)
            {
                if (ShouldSkipGenericLayerInjection(layer, skinDef)) continue;
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

                    if (parentNode == null)
                    {
                        string? fallbackAnchorTag = InferFallbackAnchorTag(layer.anchorTag);
                        if (!string.IsNullOrEmpty(fallbackAnchorTag)
                            && !string.Equals(fallbackAnchorTag, layer.anchorTag, StringComparison.OrdinalIgnoreCase))
                        {
                            parentNode = FindParentNode(tree, fallbackAnchorTag);
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

        private static bool HasPotentialInjectedOverrides(Pawn pawn, PawnSkinDef skinDef)
        {
            if (skinDef == null)
                return false;

            foreach (PawnLayerConfig layer in EnumeratePotentialInjectedLayers(pawn, skinDef))
            {
                if (IsInjectableLayerCandidate(layer, skinDef))
                    return true;
            }

            PawnFaceConfig? faceConfig = skinDef.faceConfig;
            return faceConfig != null
                && faceConfig.enabled
                && faceConfig.workflowMode == FaceWorkflowMode.LayeredDynamic
                && faceConfig.HasAnyLayeredPart();
        }

        private static IEnumerable<PawnLayerConfig> EnumeratePotentialInjectedLayers(Pawn pawn, PawnSkinDef skinDef)
        {
            if (skinDef?.layers != null)
            {
                foreach (PawnLayerConfig layer in skinDef.layers)
                    yield return layer;
            }

            foreach (PawnLayerConfig layer in BaseAppearanceUtility.BuildSyntheticLayers(skinDef))
                yield return layer;

            foreach (PawnLayerConfig layer in BuildEquipmentVisualLayers(pawn, skinDef))
                yield return layer;

            PawnLayerConfig? carryVisualLayer = BuildWeaponCarryVisualLayer(skinDef);
            if (carryVisualLayer != null)
                yield return carryVisualLayer;
        }

        private static bool IsInjectableLayerCandidate(PawnLayerConfig layer, PawnSkinDef skinDef)
        {
            if (layer == null)
                return false;

            if (ShouldSkipGenericLayerInjection(layer, skinDef))
                return false;

            if (!layer.visible)
                return false;

            if (string.IsNullOrWhiteSpace(layer.texPath))
                return false;

            if (layer.texPath == "Dynamic/Unknown"
                || layer.texPath == "Error"
                || layer.texPath == "No Graphic (Logic Only)"
                || layer.texPath.Contains("Unknown")
                || layer.texPath.StartsWith("Dynamic/", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static bool ShouldSkipGenericLayerInjection(PawnLayerConfig layer, PawnSkinDef skinDef)
        {
            return layer != null
                && !string.IsNullOrWhiteSpace(layer.layerName)
                && layer.layerName.StartsWith("[Face] ", StringComparison.OrdinalIgnoreCase)
                && skinDef?.faceConfig != null
                && skinDef.faceConfig.enabled
                && skinDef.faceConfig.workflowMode == FaceWorkflowMode.LayeredDynamic;
        }

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
                pawnType     = PawnRenderNodeProperties.RenderNodePawnType.Any,
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

        private static IEnumerable<PawnLayerConfig> BuildEquipmentVisualLayers(Pawn pawn, PawnSkinDef? skinDef)
        {
            if (pawn == null)
                yield break;

            HashSet<string> injectedSkinEquipmentThingDefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (skinDef?.equipments != null && skinDef.equipments.Count > 0)
            {
                foreach (var equipment in skinDef.equipments)
                {
                    if (equipment == null || !equipment.enabled)
                        continue;

                    equipment.EnsureDefaults();

                    if (!equipment.HasRenderTexture())
                        continue;

                    string resolvedThingDefName = equipment.GetResolvedThingDefName();
                    if (!string.IsNullOrWhiteSpace(resolvedThingDefName))
                    {
                        injectedSkinEquipmentThingDefs.Add(resolvedThingDefName);
                    }

                    CharacterEquipmentRenderData renderData = equipment.renderData ?? CharacterEquipmentRenderData.CreateDefault();
                    string resolvedLayerName = string.IsNullOrWhiteSpace(renderData.layerName)
                        ? equipment.GetDisplayLabel()
                        : renderData.layerName;

                    yield return new PawnLayerConfig
                    {
                        layerName = $"[EquipmentPreview] {resolvedLayerName}",
                        texPath = renderData.GetResolvedTexPath(),
                        maskTexPath = renderData.maskTexPath ?? string.Empty,
                        anchorTag = string.IsNullOrWhiteSpace(renderData.anchorTag) ? "Apparel" : renderData.anchorTag,
                        anchorPath = renderData.anchorPath ?? string.Empty,
                        shaderDefName = string.IsNullOrWhiteSpace(renderData.shaderDefName) ? "Cutout" : renderData.shaderDefName,
                        offset = renderData.offset,
                        offsetEast = renderData.offsetEast,
                        offsetNorth = renderData.offsetNorth,
                        scale = renderData.scale,
                        scaleEastMultiplier = renderData.scaleEastMultiplier,
                        scaleNorthMultiplier = renderData.scaleNorthMultiplier,
                        rotation = renderData.rotation,
                        rotationEastOffset = renderData.rotationEastOffset,
                        rotationNorthOffset = renderData.rotationNorthOffset,
                        drawOrder = renderData.drawOrder,
                        flipHorizontal = renderData.flipHorizontal,
                        visible = renderData.visible,
                        colorSource = renderData.colorSource,
                        customColor = renderData.customColor,
                        colorTwoSource = renderData.colorTwoSource,
                        customColorTwo = renderData.customColorTwo,
                        useTriggeredEquipmentAnimation = renderData.useTriggeredLocalAnimation,
                        triggerAbilityDefName = renderData.triggerAbilityDefName ?? string.Empty,
                        triggeredAnimationGroupKey = renderData.animationGroupKey ?? string.Empty,
                        triggeredAnimationRole = renderData.triggeredAnimationRole,
                        triggeredDeployAngle = renderData.triggeredDeployAngle,
                        triggeredReturnAngle = renderData.triggeredReturnAngle,
                        triggeredDeployTicks = renderData.triggeredDeployTicks,
                        triggeredHoldTicks = renderData.triggeredHoldTicks,
                        triggeredReturnTicks = renderData.triggeredReturnTicks,
                        animPivotOffset = renderData.triggeredPivotOffset,
                        triggeredUseVfxVisibility = renderData.triggeredUseVfxVisibility,
                        triggeredIdleTexPath = renderData.triggeredIdleTexPath ?? string.Empty,
                        triggeredDeployTexPath = renderData.triggeredDeployTexPath ?? string.Empty,
                        triggeredHoldTexPath = renderData.triggeredHoldTexPath ?? string.Empty,
                        triggeredReturnTexPath = renderData.triggeredReturnTexPath ?? string.Empty,
                        triggeredIdleMaskTexPath = renderData.triggeredIdleMaskTexPath ?? string.Empty,
                        triggeredDeployMaskTexPath = renderData.triggeredDeployMaskTexPath ?? string.Empty,
                        triggeredHoldMaskTexPath = renderData.triggeredHoldMaskTexPath ?? string.Empty,
                        triggeredReturnMaskTexPath = renderData.triggeredReturnMaskTexPath ?? string.Empty,
                        triggeredVisibleDuringDeploy = renderData.triggeredVisibleDuringDeploy,
                        triggeredVisibleDuringHold = renderData.triggeredVisibleDuringHold,
                        triggeredVisibleDuringReturn = renderData.triggeredVisibleDuringReturn,
                        triggeredVisibleOutsideCycle = renderData.triggeredVisibleOutsideCycle
                    };
                }
            }

            if (pawn.apparel?.WornApparel == null || pawn.apparel.WornApparel.Count == 0)
                yield break;

            foreach (var apparel in pawn.apparel.WornApparel)
            {
                if (apparel?.def == null)
                    continue;

                if (injectedSkinEquipmentThingDefs.Contains(apparel.def.defName))
                    continue;

                var renderExtension = apparel.def.GetModExtension<DefModExtension_EquipmentRender>();
                if (renderExtension == null || !renderExtension.enabled || !renderExtension.HasRenderableTexture())
                    continue;

                string fallbackLabel = apparel.LabelCap;
                PawnLayerConfig layer = renderExtension.ToPawnLayerConfig(fallbackLabel);
                layer.layerName = $"[EquipmentWorn] {layer.layerName}";
                yield return layer;
            }
        }

        private static PawnLayerConfig? BuildWeaponCarryVisualLayer(PawnSkinDef? skinDef)
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

        private static PawnRenderNode? FindParentNode(PawnRenderTree tree, string? anchorTag)
        {
            if (tree?.rootNode == null)
                return null;

            if (string.IsNullOrWhiteSpace(anchorTag)
                || string.Equals(anchorTag, "Root", StringComparison.OrdinalIgnoreCase))
                return tree.rootNode;

            return FindAnchorNode(tree, anchorTag);
        }

        private static string? InferFallbackAnchorTag(string? anchorTag)
        {
            if (string.IsNullOrWhiteSpace(anchorTag))
                return "Root";

            string normalizedAnchorTag = anchorTag!;

            if (normalizedAnchorTag.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Mouth", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Face", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Head";

            if (normalizedAnchorTag.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Fur", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Hair";

            if (normalizedAnchorTag.IndexOf("Beard", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Moustache", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Beard";

            if (normalizedAnchorTag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Torso", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedAnchorTag.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Body";

            return null;
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
                    if (partType == LayeredFacePartType.Base)
                    {
                        continue;
                    }

                    if (partType == LayeredFacePartType.Overlay)
                    {
                        List<string> overlayIds = faceConfig.GetOrderedOverlayIds();
                        foreach (string overlayId in overlayIds)
                        {
                            string overlayTexPath = faceConfig.GetAnyLayeredPartPath(partType, overlayId);
                            if (string.IsNullOrWhiteSpace(overlayTexPath))
                                continue;

                            int overlayOrder = faceConfig.GetOverlayOrder(overlayId);
                            PawnLayerConfig overlayLayer = BuildLayeredFacePartLayer(skinDef, faceConfig, partType, overlayTexPath, overlayId, overlayOrder);
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

                    foreach (LayeredFacePartSide side in GetLayeredFaceInjectionSides(faceConfig, partType))
                    {
                        string texPath = faceConfig.GetAnyLayeredPartPath(partType, side);
                        if (string.IsNullOrWhiteSpace(texPath))
                            continue;

                        PawnLayerConfig layer = BuildLayeredFacePartLayer(skinDef, faceConfig, partType, texPath, side: side);
                        PawnRenderNodeProperties props = CreateNodeProperties(layer);

                        var node = (PawnRenderNode)Activator.CreateInstance(
                            typeof(PawnRenderNode_Custom),
                            new object[] { pawn, props, tree });

                        if (node is not PawnRenderNode_Custom customNode)
                            continue;

                        customNode.config = layer;
                        customNode.layeredFacePartType = partType;
                        customNode.layeredFacePartSide = side;
                        node.parent = parentNode;
                        node.debugOffset = layer.offset;

                        AddChildToNode(parentNode, node);
                        anyInjected = true;
                    }
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
            // Base 改由 baseAppearance Head 槽位接管，避免与 [Base] Head / [Face] Base 双重注入共存。
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

        private static IEnumerable<LayeredFacePartSide> GetLayeredFaceInjectionSides(PawnFaceConfig faceConfig, LayeredFacePartType partType)
        {
            if (!PawnFaceConfig.SupportsSideSpecificParts(partType))
            {
                yield return LayeredFacePartSide.None;
                yield break;
            }

            bool hasExplicitSidedContent =
                faceConfig.CountLayeredParts(partType, LayeredFacePartSide.Left) > 0
                || faceConfig.CountLayeredParts(partType, LayeredFacePartSide.Right) > 0;

            if (!hasExplicitSidedContent)
            {
                yield return LayeredFacePartSide.None;
                yield break;
            }

            string leftPath = faceConfig.GetAnyLayeredPartPath(partType, LayeredFacePartSide.Left);
            if (!string.IsNullOrWhiteSpace(leftPath))
                yield return LayeredFacePartSide.Left;

            string rightPath = faceConfig.GetAnyLayeredPartPath(partType, LayeredFacePartSide.Right);
            if (!string.IsNullOrWhiteSpace(rightPath))
                yield return LayeredFacePartSide.Right;
        }

        private static PawnLayerConfig BuildLayeredFacePartLayer(
            PawnSkinDef? skinDef,
            PawnFaceConfig faceConfig,
            LayeredFacePartType partType,
            string texPath,
            string overlayId = "",
            int overlayOrder = 0,
            LayeredFacePartSide side = LayeredFacePartSide.None)
        {
            float pupilMoveRange = faceConfig.eyeDirectionConfig?.pupilMoveRange ?? 0f;
            string normalizedOverlayId = PawnFaceConfig.NormalizeOverlayId(overlayId);
            string overlayLabel = string.IsNullOrWhiteSpace(normalizedOverlayId) ? string.Empty : $"[{normalizedOverlayId}]";
            LayeredFacePartType displayPartType = partType == LayeredFacePartType.Overlay
                ? PawnFaceConfig.GetOverlayDisplayPartType(normalizedOverlayId)
                : partType;
            string sideLabel = side == LayeredFacePartSide.None ? string.Empty : $"[{side}]";
            string layerName = $"[Face] {displayPartType}{overlayLabel}{sideLabel}";
            string fallbackLayerName = $"[Face] {displayPartType}{overlayLabel}";
            PawnLayerConfig? editableLayer = skinDef?.layers?.FirstOrDefault(layer =>
                layer != null
                && string.Equals(layer.layerName, layerName, StringComparison.OrdinalIgnoreCase));

            if (editableLayer == null && side != LayeredFacePartSide.None)
            {
                editableLayer = skinDef?.layers?.FirstOrDefault(layer =>
                    layer != null
                    && string.Equals(layer.layerName, fallbackLayerName, StringComparison.OrdinalIgnoreCase));
            }

            EyeRenderMode defaultEyeRenderMode = partType == LayeredFacePartType.Pupil && pupilMoveRange > 0f
                ? EyeRenderMode.UvOffset
                : EyeRenderMode.TextureSwap;

            PawnLayerConfig resolvedLayer = editableLayer?.Clone() ?? new PawnLayerConfig();
            resolvedLayer.layerName = layerName;
            resolvedLayer.texPath = texPath;
            resolvedLayer.anchorTag = string.IsNullOrWhiteSpace(resolvedLayer.anchorTag) ? "Head" : resolvedLayer.anchorTag;
            resolvedLayer.anchorPath ??= string.Empty;
            resolvedLayer.workerClass = typeof(PawnRenderNodeWorker_CustomLayer);
            resolvedLayer.shaderDefName = string.IsNullOrWhiteSpace(resolvedLayer.shaderDefName) ? "Cutout" : resolvedLayer.shaderDefName;
            resolvedLayer.customColor = resolvedLayer.customColor == default ? Color.white : resolvedLayer.customColor;
            resolvedLayer.customColorTwo = resolvedLayer.customColorTwo == default ? Color.white : resolvedLayer.customColorTwo;
            resolvedLayer.visible = editableLayer?.visible ?? true;
            resolvedLayer.role = editableLayer?.role ?? GetLayeredFaceRole(displayPartType);
            resolvedLayer.useDirectionalSuffix = editableLayer?.useDirectionalSuffix ?? true;
            resolvedLayer.drawOrder = editableLayer?.drawOrder ?? GetLayeredFaceDrawOrder(partType, overlayOrder, normalizedOverlayId);
            resolvedLayer.eyeRenderMode = editableLayer?.eyeRenderMode ?? defaultEyeRenderMode;
            resolvedLayer.variantLogic = GetResolvedLayeredFaceVariantLogic(editableLayer, displayPartType, resolvedLayer.eyeRenderMode);
            resolvedLayer.eyeUvMoveRange = partType == LayeredFacePartType.Pupil
                ? (editableLayer?.eyeUvMoveRange ?? pupilMoveRange)
                : 0f;
            return resolvedLayer;
        }

        private static LayerVariantLogic GetResolvedLayeredFaceVariantLogic(
            PawnLayerConfig? editableLayer,
            LayeredFacePartType partType,
            EyeRenderMode eyeRenderMode)
        {
            LayerVariantLogic defaultVariantLogic = GetLayeredFaceVariantLogic(partType, eyeRenderMode);
            if (editableLayer == null)
                return defaultVariantLogic;

            if (ShouldPromoteLegacyLayeredFaceVariantLogic(editableLayer, partType, defaultVariantLogic))
                return defaultVariantLogic;

            return editableLayer.variantLogic;
        }

        private static bool ShouldPromoteLegacyLayeredFaceVariantLogic(
            PawnLayerConfig editableLayer,
            LayeredFacePartType partType,
            LayerVariantLogic defaultVariantLogic)
        {
            if (defaultVariantLogic == LayerVariantLogic.None)
                return false;

            if (editableLayer.variantLogic != LayerVariantLogic.None)
                return false;

            if (editableLayer.role != GetLayeredFaceRole(partType))
                return false;

            if (!string.IsNullOrWhiteSpace(editableLayer.variantBaseName)
                || editableLayer.useExpressionSuffix
                || editableLayer.useEyeDirectionSuffix
                || editableLayer.useBlinkSuffix
                || editableLayer.useFrameSequence
                || editableLayer.hideWhenMissingVariant)
            {
                return false;
            }

            if ((editableLayer.visibleExpressions?.Length ?? 0) > 0
                || (editableLayer.hiddenExpressions?.Length ?? 0) > 0)
            {
                return false;
            }

            return true;
        }

        private static LayerVariantLogic GetLayeredFaceVariantLogic(LayeredFacePartType partType, EyeRenderMode eyeRenderMode)
        {
            switch (partType)
            {
                case LayeredFacePartType.Brow:
                case LayeredFacePartType.Eye:
                case LayeredFacePartType.UpperLid:
                case LayeredFacePartType.LowerLid:
                case LayeredFacePartType.Mouth:
                case LayeredFacePartType.Blush:
                case LayeredFacePartType.Sweat:
                case LayeredFacePartType.Tear:
                case LayeredFacePartType.Overlay:
                    return LayerVariantLogic.ChannelState;
                case LayeredFacePartType.Pupil:
                    return eyeRenderMode == EyeRenderMode.UvOffset
                        ? LayerVariantLogic.None
                        : LayerVariantLogic.EyeDirectionOnly;
                default:
                    return LayerVariantLogic.None;
            }
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

        private static float GetLayeredFaceDrawOrder(LayeredFacePartType partType, int overlayOrder = 0, string overlayId = "")
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
                {
                    switch (PawnFaceConfig.GetOverlayKind(overlayId))
                    {
                        case LayeredOverlayKind.Blush:
                            return 50.22f;
                        case LayeredOverlayKind.Tear:
                            return 50.24f;
                        case LayeredOverlayKind.Sweat:
                            return 50.26f;
                        case LayeredOverlayKind.Sleep:
                            return 50.28f;
                        default:
                            return 50.30f + Math.Max(0, overlayOrder - 4) * 0.002f;
                    }
                }
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
                if (faceConfig != null
                    && faceConfig.workflowMode == FaceWorkflowMode.LayeredDynamic
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
                    pawnType    = PawnRenderNodeProperties.RenderNodePawnType.Any,
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