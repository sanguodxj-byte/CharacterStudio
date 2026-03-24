using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using CharacterStudio.Core;
using UnityEngine;
using Verse;
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
        private static readonly HashSet<MethodInfo> patchedCanDrawNowMethods = new HashSet<MethodInfo>();

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
                else
                {
                    Log.Warning("[CharacterStudio] 无法找到 PawnRenderTree.TrySetupGraphIfNeeded 方法");
                }

                var canDrawMethod = AccessTools.Method(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow));
                var canDrawPrefix = AccessTools.Method(typeof(Patch_PawnRenderTree), nameof(CanDrawNow_Prefix));

                if (canDrawMethod != null && canDrawPrefix != null)
                {
                    harmony.Patch(canDrawMethod, prefix: new HarmonyMethod(canDrawPrefix));
                    lock (patchStateLock) { patchedCanDrawNowMethods.Add(canDrawMethod); }
                    Log.Message("[CharacterStudio] PawnRenderNodeWorker.CanDrawNow 补丁已应用");
                }

                if (canDrawPrefix != null)
                    PatchAllDerivedCanDrawNowMethods(harmony, canDrawPrefix);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用补丁时出错: {ex}");
            }
        }

        private static void PatchAllDerivedCanDrawNowMethods(Harmony harmony, MethodInfo canDrawPrefix)
        {
            if (canDrawPrefix == null) return;
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

                            var method = type.GetMethod("CanDrawNow",
                                BindingFlags.Instance | BindingFlags.Public |
                                BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                            if (method != null)
                            {
                                try
                                {
                                    harmony.Patch(method, prefix: new HarmonyMethod(canDrawPrefix));
                                    patchedTypes.Add(type);
                                    lock (patchStateLock) { patchedCanDrawNowMethods.Add(method); }
                                    if (Prefs.DevMode)
                                        Log.Message($"[CharacterStudio] {type.FullName}.CanDrawNow 补丁已应用");
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning($"[CharacterStudio] 无法补丁 {type.FullName}.CanDrawNow: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 补丁派生 CanDrawNow 时出错: {ex.Message}");
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
                    methodsToUnpatch = new List<MethodInfo>(patchedCanDrawNowMethods);
                    patchedCanDrawNowMethods.Clear();
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
        // Harmony 前缀：CanDrawNow
        // ─────────────────────────────────────────────

        private static bool CanDrawNow_Prefix(PawnRenderNode __0, ref bool __result)
        {
            if (__0?.tree == null) return true;
            if (__0 is PawnRenderNode_Custom) return true;

            var hidden = GetHiddenSet(__0.tree);
            if (hidden.Contains(__0))
            {
                if (HasCustomDescendant(__0)) return true;
                __result = false;
                return false;
            }
            return true;
        }

        private static bool HasCustomDescendant(PawnRenderNode node)
        {
            if (node?.children == null) return false;
            foreach (var child in node.children)
            {
                if (child == null) continue;
                if (child is PawnRenderNode_Custom) return true;
                if (HasCustomDescendant(child)) return true;
            }
            return false;
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

                if (skinDef == null) return;

                // 已注入则跳过（防止重复注入）
                bool hasCustomNodes = false;
                CheckForCustomNodes(__instance.rootNode, ref hasCustomNodes);
                if (hasCustomNodes) return;

                bool expectsInjectedOverrides = HasPotentialInjectedOverrides(pawn, skinDef);

                if (!overlayMode)
                    ProcessVanillaHiding(__instance, skinDef, hideVanillaHead, hideVanillaHair, out _);

                bool anyNodesInjected = InjectCustomLayers(__instance, pawn, skinDef);
                bool anyLayeredFaceInjected = InjectLayeredFaceLayers(__instance, pawn, skinDef);

                // 注入眼睛方向覆盖层（与 RefreshHiddenNodes 保持一致）
                InjectEyeDirectionLayer(__instance, pawn, skinDef);

                if (expectsInjectedOverrides && !anyNodesInjected && !anyLayeredFaceInjected)
                {
                    RestoreAndRemoveHiddenForTree(__instance);
                    Log.Warning($"[CharacterStudio] 图层注入失败，已恢复原始渲染节点以避免角色部件被隐藏: {pawn.LabelShortCap}");
                }
                else if (!overlayMode && (anyNodesInjected || anyLayeredFaceInjected))
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
                var ext = gene.def.GetModExtension<DefModExtension_SkinLink>();
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
                else
                {
                    HideApparelLikeNodesRecursive(tree.rootNode);
                }
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

            // hiddenTags（向后兼容）+ 路径失配回退
#pragma warning disable CS0618
            if (canUseTag)
            {
                var tagsToHide = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (skinDef.hiddenTags != null)
                    foreach (var t in skinDef.hiddenTags)
                        if (!string.IsNullOrEmpty(t) &&
                            t.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) < 0 &&
                            t.IndexOf("Headgear", StringComparison.OrdinalIgnoreCase) < 0)
                            tagsToHide.Add(t);

                foreach (var t in fallbackTagsFromMissedPaths)
                    if (t.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) < 0 &&
                        t.IndexOf("Headgear", StringComparison.OrdinalIgnoreCase) < 0)
                        tagsToHide.Add(t);

                foreach (var t in tagsToHide)
                    HideNodeByTagName(nodesByTag!, t);
            }
#pragma warning restore CS0618
        }
    }
}