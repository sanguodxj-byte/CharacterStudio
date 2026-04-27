using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using HarmonyLib;
using CharacterStudio.Core;
using UnityEngine;
using Verse;
using RimWorld;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// PawnRenderTree 补丁（主文件）
    /// 职责：字段缓存、Harmony 注册/卸载、核心 Postfix/Prefix
    /// 子文件：
    ///   .Hiding.cs         — 节点隐藏 / 恢复 / Body-Apparel 启发式
    ///   .NodeSearch.cs     — NodePath 路径解析 / 面部子节点递归搜索
    ///   .Injection.cs      — 自定义图层注入、RefreshHiddenNodes
    ///   .BaseAppearance.cs — BaseAppearance 槽位覆写
    /// </summary>
    [StaticConstructorOnStartup]
    public static partial class Patch_PawnRenderTree
    {
        private static Harmony? harmony;

        private static readonly object patchStateLock = new object();
        private static readonly HashSet<MethodInfo> patchedWorkerMethods = new HashSet<MethodInfo>();

        // ─────────────────────────────────────────────
        // 反射字段缓存
        // ─────────────────────────────────────────────

        private static FieldInfo? graphicsField;
        private static readonly string[] knownGraphicsFieldNames = { "graphics", "_graphics", "cachedGraphics", "graphicCache" };
        private static readonly FieldInfo? primaryGraphicField = AccessTools.Field(typeof(PawnRenderNode), "primaryGraphic");

        private static readonly FieldInfo? nodesByTagField_Cached =
            AccessTools.Field(typeof(PawnRenderTree), "nodesByTag");
        private static readonly FieldInfo? renderTreeField_Cached =
            AccessTools.Field(typeof(PawnRenderer), "renderTree");
        private static readonly MethodInfo? setDirtyMethod_Cached =
            AccessTools.Method(typeof(PawnRenderTree), "SetDirty");
        private static readonly FieldInfo? setupCompleteField_Cached =
            AccessTools.Field(typeof(PawnRenderTree), "setupComplete");
        private static readonly FieldInfo? nodeAncestorsField_Cached =
            AccessTools.Field(typeof(PawnRenderTree), "nodeAncestors");
        private static readonly MethodInfo? initAncestorsMethod_Cached =
            AccessTools.Method(typeof(PawnRenderTree), "InitializeAncestors");

        private static FieldInfo? GetGraphicsField()
        {
            if (graphicsField != null) return graphicsField;

            foreach (var name in knownGraphicsFieldNames)
            {
                graphicsField = AccessTools.Field(typeof(PawnRenderNode), name);
                if (graphicsField != null) return graphicsField;
            }

            foreach (var field in typeof(PawnRenderNode).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.FieldType == typeof(List<Graphic>) || field.FieldType == typeof(Graphic[]))
                {
                    graphicsField = field;
                    if (Prefs.DevMode)
                        Log.Message($"[CharacterStudio] 自动探测到 Graphics 缓存字段: {field.Name}");
                    return graphicsField;
                }
            }

            Log.Warning("[CharacterStudio] 未找到 PawnRenderNode.Graphics 缓存字段");
            return null;
        }

        private static void ReplaceNodeGraphicsCache(PawnRenderNode node, Graphic? graphic)
        {
            var field = GetGraphicsField();
            if (field == null) return;

            var val = field.GetValue(node);
            if (val is List<Graphic> list)
            {
                list.Clear();
                if (graphic != null)
                {
                    list.Add(graphic);
                }
            }
            else
            {
                field.SetValue(node, graphic != null ? new Graphic[] { graphic } : Array.Empty<Graphic>());
            }

            primaryGraphicField?.SetValue(node, graphic);
        }

        private static void ClearNodeGraphicsCacheDirect(PawnRenderNode node)
        {
            var field = GetGraphicsField();
            if (field == null) return;

            var val = field.GetValue(node);
            if (val is List<Graphic> list)
            {
                list.Clear();
            }
            else
            {
                field.SetValue(node, Array.Empty<Graphic>());
            }

            primaryGraphicField?.SetValue(node, null);
        }

        static Patch_PawnRenderTree() { }

        // ─────────────────────────────────────────────
        // Harmony Apply / Unpatch
        // ─────────────────────────────────────────────

        public static void Apply(Harmony harmonyInstance)
        {
            harmony = harmonyInstance;

            try
            {
                var setupMethod  = AccessTools.Method(typeof(PawnRenderTree), "TrySetupGraphIfNeeded");
                var postfixMethod = AccessTools.Method(typeof(Patch_PawnRenderTree), nameof(TrySetupGraphIfNeeded_Postfix));

                if (setupMethod != null && postfixMethod != null)
                {
                    harmony.Patch(setupMethod, postfix: new HarmonyMethod(postfixMethod));
                    Log.Message("[CharacterStudio] PawnRenderTree.TrySetupGraphIfNeeded 补丁已应用");
                }

                var materialMethod = AccessTools.Method(typeof(PawnRenderNodeWorker), "GetFinalizedMaterial");
                var materialPostfix = AccessTools.Method(typeof(Patch_PawnRenderTree), nameof(GetFinalizedMaterial_Postfix));
                
                if (materialMethod != null && materialPostfix != null)
                {
                    harmony.Patch(materialMethod, postfix: new HarmonyMethod(materialPostfix));
                    lock (patchStateLock) { patchedWorkerMethods.Add(materialMethod); }
                }

                if (materialPostfix != null)
                    PatchAllDerivedWorkerMethods(harmony, materialPostfix);

                // 全局 DrawSize / 全向缩放：对所有渲染节点（含原版身体/头发/装备）统一应用缩放
                var scaleForMethod = AccessTools.Method(typeof(PawnRenderNodeWorker), "ScaleFor",
                    new[] { typeof(PawnRenderNode), typeof(PawnDrawParms) });
                var scaleForPostfix = AccessTools.Method(typeof(Patch_PawnRenderTree), nameof(ScaleFor_GlobalPostfix));
                if (scaleForMethod != null && scaleForPostfix != null)
                {
                    harmony.Patch(scaleForMethod, postfix: new HarmonyMethod(scaleForPostfix));
                    lock (patchStateLock) { patchedWorkerMethods.Add(scaleForMethod); }
                    Log.Message("[CharacterStudio] PawnRenderNodeWorker.ScaleFor 全局缩放补丁已应用");
                }

                // 派生类可能重写了 ScaleFor（如 PawnRenderNodeWorker_Body/Head），
                // 仅补丁基类不够，必须遍历所有派生 Worker 单独补丁。
                if (scaleForPostfix != null)
                    PatchAllDerivedWorkerScaleMethods(harmony, scaleForPostfix);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用补丁时出错: {ex}");
            }
        }

        private static void PatchAllDerivedWorkerMethods(Harmony harmony, MethodInfo workerPrefix)
        {
            try
            {
                var workerType   = typeof(PawnRenderNodeWorker);
                var patchedTypes = new HashSet<Type> { workerType };

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (patchedTypes.Contains(type)) continue;
                            if (!workerType.IsAssignableFrom(type)) continue;
                            if (type.IsAbstract) continue;

                            if (workerPrefix != null)
                            {
                                var matMethod = type.GetMethod("GetFinalizedMaterial", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                                if (matMethod != null)
                                {
                                    try
                                    {
                                        harmony.Patch(matMethod, postfix: new HarmonyMethod(workerPrefix));
                                        lock (patchStateLock) { patchedWorkerMethods.Add(matMethod); }
                                    }
                                    catch (Exception ex) { Log.Warning($"[CharacterStudio] 无法补丁 {type.FullName}.GetFinalizedMaterial: {ex.Message}"); }
                                }
                            }

                            patchedTypes.Add(type);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 补丁派生 Worker 时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 遍历所有 PawnRenderNodeWorker 派生类，对其重写的 ScaleFor 方法应用全局缩放补丁。
        /// 派生类（如 PawnRenderNodeWorker_Body/Head/Hair）如果重写了 ScaleFor，
        /// 基类补丁不会生效，必须逐个补丁。
        /// 仅扫描 Verse 程序集和 CharacterStudio 自身，避免 patch 其他模组的子类。
        /// </summary>
        private static void PatchAllDerivedWorkerScaleMethods(Harmony harmony, MethodInfo scalePostfix)
        {
            try
            {
                var workerType = typeof(PawnRenderNodeWorker);
                var scaleParamTypes = new[] { typeof(PawnRenderNode), typeof(PawnDrawParms) };
                var csAssembly = typeof(Patch_PawnRenderTree).Assembly;
                var verseAssembly = workerType.Assembly;

                var assembliesToScan = new[] { verseAssembly, csAssembly };

                foreach (var assembly in assembliesToScan)
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type == workerType) continue;
                            if (!workerType.IsAssignableFrom(type)) continue;
                            if (type.IsAbstract) continue;
                            // 跳过 CS 自定义图层 Worker（已内部处理 globalTextureScale）
                            if (type == typeof(PawnRenderNodeWorker_CustomLayer)) continue;

                            var scaleMethod = type.GetMethod("ScaleFor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, scaleParamTypes, null);
                            if (scaleMethod != null)
                            {
                                try
                                {
                                    harmony.Patch(scaleMethod, postfix: new HarmonyMethod(scalePostfix));
                                    lock (patchStateLock) { patchedWorkerMethods.Add(scaleMethod); }
                                    Log.Message($"[CharacterStudio] 已补丁 {type.Name}.ScaleFor 全局缩放");
                                }
                                catch (Exception ex) { Log.Warning($"[CharacterStudio] 无法补丁 {type.FullName}.ScaleFor: {ex.Message}"); }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 补丁派生 Worker ScaleFor 时出错: {ex.Message}");
            }
        }

        public static void Unpatch(Harmony harmonyInstance)
        {
            try
            {
                var originalMethod = AccessTools.Method(typeof(PawnRenderTree), "TrySetupGraphIfNeeded");
                if (originalMethod != null)
                    harmonyInstance.Unpatch(originalMethod, HarmonyPatchType.Postfix, harmonyInstance.Id);

                List<MethodInfo> methodsToUnpatch;
                lock (patchStateLock)
                {
                    methodsToUnpatch = new List<MethodInfo>(patchedWorkerMethods);
                    patchedWorkerMethods.Clear();
                }

                foreach (var method in methodsToUnpatch)
                {
                    if (method == null) continue;
                    harmonyInstance.Unpatch(method, HarmonyPatchType.Prefix, harmonyInstance.Id);
                }

                Log.Message("[CharacterStudio] 补丁已移除");
                ClearHiddenNodes();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 移除补丁时出错: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        // Harmony 后缀：ScaleFor — 全局角色缩放
        // ─────────────────────────────────────────────

        /// <summary>
        /// 对所有渲染节点（含原版身体、头发、装备等）统一乘以 globalTextureScale。
        /// CS 自定义图层（PawnRenderNode_Custom）跳过，因为它们的 Worker 中已经单独应用了。
        /// </summary>
        // P9: 缓存最近一次 GlobalPostfix 的 (Pawn, CompPawnSkin) 对，
        // 因为同一帧内所有原版节点都属于同一个 pawn，避免重复 GetComp。
        private static Pawn? _globalPostfixLastPawn;
        private static CompPawnSkin? _globalPostfixLastComp;
        private static bool _globalPostfixLastHasRelevantSkin;

        public static void ScaleFor_GlobalPostfix(PawnRenderNode __0, PawnDrawParms __1, ref Vector3 __result)
        {
            PawnRenderNode node = __0;
            PawnDrawParms parms = __1;
            if (node == null || node is PawnRenderNode_Custom) return;

            // 只对根级原版节点应用全局缩放，子节点通过矩阵层级自动继承
            if (HasAnyVanillaAncestor(node)) return;

            try
            {
                Pawn? pawn = parms.pawn ?? node.tree?.pawn;
                if (pawn == null) return;

                // P13: 使用缓存避免每个节点都调用 GetComp，并缓存“是否有相关皮肤”布尔值
                CompPawnSkin? skinComp;
                bool hasRelevantSkin;
                if (pawn == _globalPostfixLastPawn)
                {
                    skinComp = _globalPostfixLastComp;
                    hasRelevantSkin = _globalPostfixLastHasRelevantSkin;
                }
                else
                {
                    skinComp = pawn.GetComp<CompPawnSkin>();
                    hasRelevantSkin = skinComp?.ActiveSkin != null;
                    _globalPostfixLastPawn = pawn;
                    _globalPostfixLastComp = skinComp;
                    _globalPostfixLastHasRelevantSkin = hasRelevantSkin;
                }

                if (!hasRelevantSkin || skinComp == null) return;

                PawnSkinDef? activeSkin = skinComp.ActiveSkin;
                if (activeSkin == null) return;

                float gs = activeSkin.globalTextureScale;
                if (float.IsNaN(gs) || float.IsInfinity(gs) || gs <= 0f || Math.Abs(gs - 1f) < 0.001f) return;

                __result = new Vector3(__result.x * gs, __result.y, __result.z * gs);
            }
            catch { }
        }

        /// <summary>
        /// 判断节点是否有任何非Custom的祖先节点（无论该祖先是否被隐藏）。
        /// 如果有，说明该祖先的 ScaleFor 已被 postfix 应用了全局缩放，
        /// 子节点通过矩阵层级自动继承，无需重复应用。
        /// </summary>
        public static bool HasAnyVanillaAncestor(PawnRenderNode node)
        {
            if (node is PawnRenderNode_Custom customNode)
            {
                int cached = customNode._hasVanillaAncestorState;
                if (cached >= 0)
                    return cached != 0;
            }

            bool result = false;
            PawnRenderNode? ancestor = node.parent;
            while (ancestor != null)
            {
                if (!(ancestor is PawnRenderNode_Custom))
                {
                    result = true;
                    break;
                }
                ancestor = ancestor.parent;
            }

            if (node is PawnRenderNode_Custom cn)
                cn._hasVanillaAncestorState = result ? 1 : 0;

            return result;
        }

        /// <summary>
        /// 检查节点是否被CS系统隐藏（存在于 hiddenNodesByTree 中）。
        /// </summary>
        public static bool IsNodeHidden(PawnRenderNode node)
        {
            if (node?.tree == null) return false;
            return hiddenNodesByTree.TryGetValue(node.tree, out var hidden)
                && hidden != null && hidden.Contains(node);
        }

        // ─────────────────────────────────────────────
        // Harmony 后缀：GetFinalizedMaterial
        // ─────────────────────────────────────────────

        public static void GetFinalizedMaterial_Postfix(PawnRenderNode node, ref Material __result)
        {
            if (node == null || node.tree == null) return;
            if (node is PawnRenderNode_Custom) return;

            try
            {
                if (hiddenNodesByTree.TryGetValue(node.tree, out var hidden) && hidden != null && hidden.Contains(node))
                {
                    if (__result != null)
                    {
                        // 渲染绕过逻辑：
                        // 为了确保节点完全透明且不影响子节点（如服装或自定义层），
                        // 这里使用 BaseContent.ClearMat 替代默认材质。
                        // 这实现了透明效果与渲染树结构完整性的平衡。
                        __result = BaseContent.ClearMat;
                    }
                }
            }
            catch { }
        }

        // ─────────────────────────────────────────────
        // Harmony 后缀：TrySetupGraphIfNeeded
        // ─────────────────────────────────────────────

        private static void TrySetupGraphIfNeeded_Postfix(PawnRenderTree __instance)
        {
            try
            {
                var pawn = __instance.pawn;
                if (pawn == null) return;

                if (!RuntimeAssetLoader.IsMainThread())
                {
                    return;
                }

                PawnSkinDef? skinDef = null;
                bool hideVanillaHead = false;
                bool hideVanillaHair = false;
                bool overlayMode    = false;

                var geneSkin = TryGetGeneSkin(pawn);
                if (geneSkin.skinDef != null)
                {
                    skinDef         = geneSkin.skinDef;
                    hideVanillaHead = geneSkin.hideHead;
                    hideVanillaHair = geneSkin.hideHair;
                    overlayMode     = geneSkin.overlayMode;
                }
                else
                {
                    var skinComp = pawn.GetComp<CompPawnSkin>();
                    if (skinComp != null && skinComp.HasActiveSkin)
                    {
                        skinDef = skinComp.ActiveSkin;
                        if (skinDef != null)
                        {
                            hideVanillaHead = skinDef.hideVanillaHead;
                            hideVanillaHair = skinDef.hideVanillaHair;
                        }
                    }
                }

                if (skinDef == null && !RenderFixPatchRegistry.GetApplicablePatches(pawn).Any()) return;

                // 已注入则跳过（防止重复注入）
                bool hasCustomNodes = false;
                CheckForCustomNodes(__instance.rootNode, ref hasCustomNodes);
                if (hasCustomNodes) return;

                bool expectsInjectedOverrides = skinDef != null && HasPotentialInjectedOverrides(pawn, skinDef);

                if (skinDef != null && !overlayMode)
                    ProcessVanillaHiding(__instance, skinDef, hideVanillaHead, hideVanillaHair, out _);
                else
                    ProcessRenderFixOnlyHiding(__instance, pawn);

                bool anyNodesInjected = false;
                bool anyLayeredFaceInjected = false;

                if (skinDef != null)
                {
                    anyNodesInjected = InjectCustomLayers(__instance, pawn, skinDef);
                    anyLayeredFaceInjected = InjectLayeredFaceLayers(__instance, pawn, skinDef);

                    // 注入眼睛方向覆盖层（与 RefreshHiddenNodes 保持一致）
                    InjectEyeDirectionLayer(__instance, pawn, skinDef);
                }

                if (expectsInjectedOverrides && !anyNodesInjected && !anyLayeredFaceInjected)
                {
                    RestoreAndRemoveHiddenForTree(__instance);
                    Log.Warning($"[CharacterStudio] 图层注入失败，已恢复原始渲染节点以避免角色部件被隐藏: {pawn.LabelShortCap}");
                }
                else if (skinDef != null && !overlayMode && (anyNodesInjected || anyLayeredFaceInjected))
                {
                    try
                    {
                        if (__instance.rootNode != null && !pawn.Dead &&
                            IsGraphicsReadyForVanillaNodes(__instance))
                        {
                            HideVanillaNodesByImportedTexPaths(__instance, skinDef);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[CharacterStudio] 纹理路径隐藏跳过: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 处理渲染树时出错: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        // 图形就绪检测
        // ─────────────────────────────────────────────

        private static bool IsGraphicsReadyForVanillaNodes(PawnRenderTree tree)
        {
            if (tree?.rootNode == null) return false;
            return IsNodeGraphicsReadyRecursive(tree.rootNode);
        }

        // 贴图路径排除列表：这些路径表示节点图形尚未就绪（占位符或烟雾特效）。
        // 可在此扩展，无需修改检测逻辑。
        private static readonly HashSet<string> GraphicsNotReadyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Things/Mote/Smoke",
        };

        private static readonly string[] GraphicsNotReadySuffixes = { "/Blank" };

        private static bool IsNodeGraphicsReadyRecursive(PawnRenderNode node)
        {
            if (node == null) return true;

            if (!(node is PawnRenderNode_Custom))
            {
                try
                {
                    var g = node.PrimaryGraphic;
                    if (g == null) return false;

                    if (g is Graphic_Runtime runtimeGraphic)
                    {
                        if (!runtimeGraphic.IsInitializedSuccessfully)
                            return false;

                        if (Graphic_Runtime.IsPendingMainThreadInitialization(runtimeGraphic.path))
                            return false;
                    }

                    string path = g.path ?? string.Empty;
                    if (GraphicsNotReadyPaths.Contains(path))
                        return false;
                    foreach (var suffix in GraphicsNotReadySuffixes)
                        if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                            return false;
                }
                catch { return false; }
            }

            if (node.children == null) return true;
            foreach (var child in node.children)
            {
                if (child == null) continue;
                if (!IsNodeGraphicsReadyRecursive(child)) return false;
            }
            return true;
        }

        // ─────────────────────────────────────────────
        // 基因皮肤解析
        // ─────────────────────────────────────────────

        private static (PawnSkinDef? skinDef, bool hideHead, bool hideHair, bool overlayMode) TryGetGeneSkin(Pawn pawn)
        {
            if (pawn.genes == null) return (null, false, false, false);

            DefModExtension_SkinLink? bestLink = null;
            int bestPriority = int.MinValue;

            foreach (var gene in pawn.genes.GenesListForReading)
            {
                var ext = DefModExtensionCache.GetSkinLink(gene.def);
                if (ext != null && ext.priority > bestPriority)
                {
                    if (ext.GetSkinDef() != null)
                    {
                        bestLink = ext;
                        bestPriority = ext.priority;
                    }
                }
            }

            if (bestLink != null)
                return (bestLink.GetSkinDef(), bestLink.hideVanillaHead, bestLink.hideVanillaHair, bestLink.overlayMode);

            return (null, false, false, false);
        }

        // ─────────────────────────────────────────────
        // 处理原版元素隐藏（委托 Hiding.cs 中的方法）
        // ─────────────────────────────────────────────

        private static void ProcessVanillaHiding(
            PawnRenderTree tree, PawnSkinDef skinDef,
            bool hideHead, bool hideHair,
            out bool tagHidingAvailable)
        {
            var nodesByTag = nodesByTagField_Cached?.GetValue(tree) as Dictionary<PawnRenderNodeTagDef, PawnRenderNode>;
            tagHidingAvailable = nodesByTag != null;
            bool canUseTag = tagHidingAvailable;

            // 自动检测 BaseAppearance 启用的槽位，决定是否隐藏对应的原版节点。
            // 分层面部 Base 现在也会以可编辑图层形式接管头部底图，因此同样需要触发原版 Head 隐藏。
            bool hasHeadBaseSlot = HasEnabledBaseSlot(skinDef, BaseAppearanceSlotType.Head);
            bool hasHairBaseSlot = HasEnabledBaseSlot(skinDef, BaseAppearanceSlotType.Hair);
            bool hasBodyBaseSlot = HasEnabledBaseSlot(skinDef, BaseAppearanceSlotType.Body);
            bool hasEditableLayeredFaceBase = HasEnabledEditableLayeredFaceBase(skinDef);

            bool shouldHideHead = hideHead
                || skinDef.hideVanillaHead
                || hasHeadBaseSlot
                || hasEditableLayeredFaceBase;
            bool shouldHideHair = hideHair
                || skinDef.hideVanillaHair
                || hasHairBaseSlot;
            bool shouldHideBody = skinDef.hideVanillaBody || hasBodyBaseSlot;

            if (canUseTag && shouldHideHead)
                HideNodeByTagName(nodesByTag!, "Head");

            if (canUseTag && shouldHideHair)
                HideNodeByTagName(nodesByTag!, "Hair");

            if (shouldHideBody)
            {
                if (canUseTag)
                {
                    HideNodeByTagName(nodesByTag!, "Body");
                    HideVanillaBodyFallback(tree, nodesByTag!);
                }
                else
                {
                    HideBodyLikeNodesRecursive(tree.rootNode);
                }
            }

            if (skinDef.hideVanillaApparel)
            {
                if (canUseTag)
                {
                    HideNodeByTagName(nodesByTag!, "Apparel");
                    HideNodeByTagName(nodesByTag!, "Headgear");
                }
                
                // 强制进行启发式递归遍历寻找服装节点。
                // 原版动态装配的衣服通常没有明确的 TagDef（它们是运行时由 ApparelGraphicRecord 注入的），
                // 必须借助 workerClass.Name 包含 "Apparel" 才能可靠捕捉。
                HideApparelLikeNodesRecursive(tree.rootNode);
            }

            // hiddenPaths（NodePath 精确定位）
            var hiddenPaths = skinDef.hiddenPaths;
            var fallbackTagsFromMissedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (hiddenPaths != null && hiddenPaths.Count > 0)
            {
                foreach (string nodePath in hiddenPaths)
                {
                    if (string.IsNullOrEmpty(nodePath)) continue;
                    var targetNode = FindNodeByPath(tree, nodePath, warnOnFail: false);
                    if (targetNode != null)
                        HideNode(targetNode);
                    else
                    {
                        string fallbackTag = ExtractTerminalTagFromNodePath(nodePath);
                        if (!string.IsNullOrEmpty(fallbackTag))
                            fallbackTagsFromMissedPaths.Add(fallbackTag);
                    }
                }
            }

            foreach (string nodePath in CollectRenderFixHiddenPaths(tree?.pawn))
            {
                if (tree == null || string.IsNullOrWhiteSpace(nodePath))
                {
                    continue;
                }

                PawnRenderNode? targetNode = FindNodeByPath(tree, nodePath, warnOnFail: false);
                if (targetNode != null)
                {
                    HideNode(targetNode);
                }
            }

            // hiddenTags（向后兼容）+ 路径失配回退
#pragma warning disable CS0618
            if (canUseTag)
            {
                var tagsToHide = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (skinDef.hiddenTags != null)
                    foreach (var t in skinDef.hiddenTags)
                        if (!string.IsNullOrEmpty(t))
                            tagsToHide.Add(t);

                foreach (var t in fallbackTagsFromMissedPaths)
                    tagsToHide.Add(t);

                foreach (var t in tagsToHide)
                    HideNodeByTagName(nodesByTag!, t);
            }
#pragma warning restore CS0618
        }

        private static void ProcessRenderFixOnlyHiding(PawnRenderTree tree, Pawn pawn)
        {
            foreach (string nodePath in CollectRenderFixHiddenPaths(pawn))
            {
                if (string.IsNullOrWhiteSpace(nodePath))
                {
                    continue;
                }

                PawnRenderNode? targetNode = FindNodeByPath(tree, nodePath, warnOnFail: false);
                if (targetNode != null)
                {
                    HideNode(targetNode);
                }
            }
        }
    }
}
