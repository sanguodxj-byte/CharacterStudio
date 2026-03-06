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
    /// PawnRenderTree 补丁
    /// 用于在渲染树构建时注入自定义图层
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_PawnRenderTree
    {
        private static Harmony? harmony;

        private static readonly object patchStateLock = new object();
        private static readonly HashSet<MethodInfo> patchedCanDrawNowMethods = new HashSet<MethodInfo>();

        // 反射字段缓存：只在首次调用时查找一次
        private static FieldInfo? graphicsField;
        private static readonly string[] knownGraphicsFieldNames = { "graphics", "_graphics", "cachedGraphics", "graphicCache" };

        // P2: 全局反射缓存 —— 避免每次调用时重新查找
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
            if (graphicsField != null)
            {
                return graphicsField;
            }

            foreach (var name in knownGraphicsFieldNames)
            {
                graphicsField = AccessTools.Field(typeof(PawnRenderNode), name);
                if (graphicsField != null)
                {
                    return graphicsField;
                }
            }

            foreach (var field in typeof(PawnRenderNode).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.FieldType == typeof(Graphic[]))
                {
                    graphicsField = field;
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[CharacterStudio] 自动探测到 Graphics 缓存字段: {field.Name}");
                    }
                    return graphicsField;
                }
            }

            Log.Warning("[CharacterStudio] 未找到 PawnRenderNode.Graphics[] 缓存字段，将回退到 CanDrawNow 方案（节点可能无法完全隐藏）");
            return null;
        }

        static Patch_PawnRenderTree()
        {
            // Harmony 在 ModEntryPoint 中初始化
        }

        /// <summary>
        /// 应用补丁
        /// </summary>
        public static void Apply(Harmony harmonyInstance)
        {
            harmony = harmonyInstance;

            try
            {
                // 补丁 TrySetupGraphIfNeeded 方法
                var originalMethod = AccessTools.Method(typeof(PawnRenderTree), "TrySetupGraphIfNeeded");
                var postfixMethod = AccessTools.Method(typeof(Patch_PawnRenderTree), nameof(TrySetupGraphIfNeeded_Postfix));

                if (originalMethod != null && postfixMethod != null)
                {
                    harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                    Log.Message("[CharacterStudio] PawnRenderTree.TrySetupGraphIfNeeded 补丁已应用");
                }
                else
                {
                    Log.Warning("[CharacterStudio] 无法找到 PawnRenderTree.TrySetupGraphIfNeeded 方法");
                }

                // 补丁 PawnRenderNodeWorker.CanDrawNow 方法 (用于隐藏节点)
                var canDrawMethod = AccessTools.Method(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow));
                var canDrawPrefix = AccessTools.Method(typeof(Patch_PawnRenderTree), nameof(CanDrawNow_Prefix));

                if (canDrawMethod != null && canDrawPrefix != null)
                {
                    harmony.Patch(canDrawMethod, prefix: new HarmonyMethod(canDrawPrefix));
                    lock (patchStateLock)
                    {
                        patchedCanDrawNowMethods.Add(canDrawMethod);
                    }
                    Log.Message("[CharacterStudio] PawnRenderNodeWorker.CanDrawNow 补丁已应用");
                }

                // 注意：不再补丁 GraphicFor。实际渲染走 worker.GetGraphic -> node.Graphics[] 缓存。
                // 隐藏策略改为在 HideNode 时清空 Graphics[]，保证不提交 Mesh 且不中断子节点遍历。

                // HAR 兼容性：补丁所有 PawnRenderNodeWorker 的派生类的 CanDrawNow 方法
                if (canDrawPrefix != null)
                {
                    PatchAllDerivedCanDrawNowMethods(harmony, canDrawPrefix);
                }
                else
                {
                    Log.Warning("[CharacterStudio] CanDrawNow_Prefix 未找到，跳过派生类 CanDrawNow 补丁");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用 PawnRenderTree 补丁时出错: {ex}");
            }
        }

        /// <summary>
        /// HAR 兼容性：补丁所有 PawnRenderNodeWorker 派生类的 CanDrawNow 方法
        /// </summary>
        private static void PatchAllDerivedCanDrawNowMethods(Harmony harmony, MethodInfo canDrawPrefix)
        {
            if (canDrawPrefix == null) return;
            
            try
            {
                var workerType = typeof(PawnRenderNodeWorker);
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
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                            
                            if (method != null)
                            {
                                try
                                {
                                    harmony.Patch(method, prefix: new HarmonyMethod(canDrawPrefix));
                                    patchedTypes.Add(type);
                                    lock (patchStateLock)
                                    {
                                        patchedCanDrawNowMethods.Add(method);
                                    }
                                    if (Prefs.DevMode)
                                    {
                                        Log.Message($"[CharacterStudio] {type.FullName}.CanDrawNow 补丁已应用");
                                    }
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

        /// <summary>
        /// 卸载补丁
        /// </summary>
        public static void Unpatch(Harmony harmonyInstance)
        {
            try
            {
                var originalMethod = AccessTools.Method(typeof(PawnRenderTree), "TrySetupGraphIfNeeded");
                if (originalMethod != null)
                {
                    harmonyInstance.Unpatch(originalMethod, HarmonyPatchType.Postfix, harmonyInstance.Id);
                }

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
                
                // 清理所有隐藏节点状态
                ClearHiddenNodes();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 移除 PawnRenderTree 补丁时出错: {ex}");
            }
        }

        /// <summary>
        /// CanDrawNow 前缀补丁
        /// 如果节点在隐藏列表中，则禁止绘制
        /// </summary>
        private static bool CanDrawNow_Prefix(PawnRenderNode node, ref bool __result)
        {
            if (node?.tree == null)
                return true;

            // 自定义节点自身始终放行，避免“自己拦截自己”导致不绘制。
            if (node is PawnRenderNode_Custom)
                return true;

            var hidden = GetHiddenSet(node.tree);
            if (hidden.Contains(node))
            {
                // 若该节点任意深度存在自定义后代，必须允许 Draw 流程继续，
                // 否则孙层/更深层的自定义图层会被整棵分支短路。
                if (HasCustomDescendant(node))
                {
                    // 允许执行，自身贴图由 HideNode 时清空 Graphics[] 缓存实现隐藏
                    return true;
                }

                __result = false;
                return false; // 跳过原方法，完全隐藏
            }
            return true;
        }

        /// <summary>
        /// 检查节点任意深度是否存在自定义后代节点
        /// </summary>
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

        /// <summary>
        /// TrySetupGraphIfNeeded 后缀补丁
        /// 在渲染树构建完成后注入自定义图层
        /// </summary>
        private static void TrySetupGraphIfNeeded_Postfix(PawnRenderTree __instance)
        {
            try
            {
                var pawn = __instance.pawn;
                if (pawn == null) return;

                PawnSkinDef? skinDef = null;
                bool hideVanillaHead = false;
                bool hideVanillaHair = false;
                bool overlayMode = false;

                var geneSkin = TryGetGeneSkin(pawn);
                if (geneSkin.skinDef != null)
                {
                    skinDef = geneSkin.skinDef;
                    hideVanillaHead = geneSkin.hideHead;
                    hideVanillaHair = geneSkin.hideHair;
                    overlayMode = geneSkin.overlayMode;
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
                            overlayMode = false;
                        }
                    }
                }

                if (skinDef == null) return;

                // P1: 如果已经注入了自定义节点，跳过重复注入
                bool hasCustomNodes = false;
                CheckForCustomNodes(__instance.rootNode, ref hasCustomNodes);
                if (hasCustomNodes) return;

                bool tagHidingAvailable = true;
                if (!overlayMode)
                {
                    ProcessVanillaHiding(__instance, skinDef, hideVanillaHead, hideVanillaHair, out tagHidingAvailable);
                }

                bool anyNodesInjected = InjectCustomLayers(__instance, pawn, skinDef);

                if (!overlayMode && anyNodesInjected)
                {
                    try
                    {
                        if (__instance?.rootNode != null && __instance.pawn != null && !__instance.pawn.Dead)
                        {
                            if (!tagHidingAvailable && IsGraphicsReadyForVanillaNodes(__instance))
                            {
                                HideVanillaNodesByImportedTexPaths(__instance, skinDef);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[CharacterStudio] 兜底隐藏跳过: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 处理渲染树时出错: {ex}");
            }
        }

        /// <summary>
        /// 检查是否可使用 nodesByTag 索引（部分模组/版本可能不可用）
        /// </summary>
        private static bool HasNodesByTagIndex(PawnRenderTree tree)
        {
            // P2: 使用缓存的反射字段
            var nodesByTag = nodesByTagField_Cached?.GetValue(tree) as Dictionary<PawnRenderNodeTagDef, PawnRenderNode>;
            return nodesByTag != null;
        }

        /// <summary>
        /// 检查“原版节点”图形是否已就绪（过滤占位贴图/未初始化节点）
        /// 注意：排除 PawnRenderNode_Custom，避免注入后被误判为就绪。
        /// </summary>
        private static bool IsGraphicsReadyForVanillaNodes(PawnRenderTree tree)
        {
            if (tree?.rootNode == null)
                return false;

            return IsNodeGraphicsReadyRecursive(tree.rootNode);
        }

        private static bool IsNodeGraphicsReadyRecursive(PawnRenderNode node)
        {
            if (node == null)
                return true;

            if (!(node is PawnRenderNode_Custom))
            {
                try
                {
                    var g = node.PrimaryGraphic;
                    if (g == null) return false;

                    string path = g.path ?? string.Empty;
                    // 已知占位路径：Blank / 烟雾占位
                    if (path.Equals("Miho/Blank", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("/Blank", StringComparison.OrdinalIgnoreCase) ||
                        path.Equals("Things/Mote/Smoke", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }

            if (node.children == null)
                return true;

            foreach (var child in node.children)
            {
                if (child == null) continue;
                if (!IsNodeGraphicsReadyRecursive(child)) return false;
            }

            return true;
        }

        /// <summary>
        /// 尝试从基因获取皮肤
        /// </summary>
        private static (PawnSkinDef? skinDef, bool hideHead, bool hideHair, bool overlayMode) TryGetGeneSkin(Pawn pawn)
        {
            if (pawn.genes == null)
                return (null, false, false, false);

            DefModExtension_SkinLink? bestLink = null;
            int bestPriority = int.MinValue;

            foreach (var gene in pawn.genes.GenesListForReading)
            {
                var ext = gene.def.GetModExtension<DefModExtension_SkinLink>();
                if (ext != null && ext.priority > bestPriority)
                {
                    var skinDef = ext.GetSkinDef();
                    if (skinDef != null)
                    {
                        bestLink = ext;
                        bestPriority = ext.priority;
                    }
                }
            }

            if (bestLink != null)
            {
                return (bestLink.GetSkinDef(), bestLink.hideVanillaHead, bestLink.hideVanillaHair, bestLink.overlayMode);
            }

            return (null, false, false, false);
        }

        /// <summary>
        /// 处理隐藏原版元素
        /// 阶段4: 同时支持 hiddenTags（旧版）和 hiddenPaths（新版 NodePath）
        /// </summary>
        private static void ProcessVanillaHiding(PawnRenderTree tree, PawnSkinDef skinDef, bool hideHead, bool hideHair, out bool tagHidingAvailable)
        {
            // P2: 使用缓存的反射字段
            var nodesByTag = nodesByTagField_Cached?.GetValue(tree) as Dictionary<PawnRenderNodeTagDef, PawnRenderNode>;

            tagHidingAvailable = nodesByTag != null;
            bool canUseTagHiding = tagHidingAvailable;

            // 隐藏头部
            if (canUseTagHiding && (hideHead || skinDef.hideVanillaHead))
            {
                HideNodeByTagName(nodesByTag!, "Head");
            }

            // 隐藏头发
            if (canUseTagHiding && (hideHair || skinDef.hideVanillaHair))
            {
                HideNodeByTagName(nodesByTag!, "Hair");
            }

            // 隐藏身体
            if (skinDef.hideVanillaBody)
            {
                if (canUseTagHiding)
                {
                    // 1) 先按标准 Body 标签隐藏（原版/部分模组）
                    HideNodeByTagName(nodesByTag!, "Body");

                    // 2) 再走一次兼容性回退（HAR/异种等非标准标签）
                    HideVanillaBodyFallback(tree, nodesByTag!);
                }
                else
                {
                    // 没有 tag 索引时，至少保留递归 Body-like 回退
                    HideBodyLikeNodesRecursive(tree.rootNode);
                }
            }

            // 阶段4: 处理新版 hiddenPaths 列表（NodePath 精确定位）
            bool hasHiddenPath = skinDef.hiddenPaths != null && skinDef.hiddenPaths.Count > 0;
            bool hiddenByPath = false;
            var fallbackTagsFromMissedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (hasHiddenPath)
            {
                foreach (var nodePath in skinDef.hiddenPaths)
                {
                    if (string.IsNullOrEmpty(nodePath))
                        continue;

                    var targetNode = FindNodeByPath(tree, nodePath, warnOnFail: false);
                    if (targetNode != null)
                    {
                        HideNode(targetNode);
                        hiddenByPath = true;
                    }
                    else
                    {
                        // 路径失配时，提取末段 tag 做“段级回退”，用于补漏不同种族/模组的节点结构差异。
                        string fallbackTag = ExtractTerminalTagFromNodePath(nodePath);
                        if (!string.IsNullOrEmpty(fallbackTag))
                        {
                            fallbackTagsFromMissedPaths.Add(fallbackTag);
                        }
                    }
                }
            }

            // 处理自定义 hiddenTags 列表（向后兼容） + 路径失配段级回退
            #pragma warning disable CS0618 // 忽略过时警告
            if (canUseTagHiding)
            {
                var tagsToHide = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 旧行为：仅当未配置 hiddenPaths 或 hiddenPaths 全失配时，启用 hiddenTags 全量回退
                if (skinDef.hiddenTags != null && skinDef.hiddenTags.Count > 0 && (!hasHiddenPath || !hiddenByPath))
                {
                    foreach (var tagName in skinDef.hiddenTags)
                    {
                        if (!string.IsNullOrEmpty(tagName))
                        {
                            tagsToHide.Add(tagName);
                        }
                    }
                }

                // 新行为：对 hiddenPaths 中“失配项”做段级 tag 回退（即使部分路径已命中也补漏）
                foreach (var fallbackTag in fallbackTagsFromMissedPaths)
                {
                    tagsToHide.Add(fallbackTag);
                }

                foreach (var tagName in tagsToHide)
                {
                    HideNodeByTagName(nodesByTag!, tagName);
                }
            }
            #pragma warning restore CS0618

        }

        /// <summary>
        /// 按导入图层 texPath 隐藏原版节点（跨路径兜底）
        /// </summary>
        private static void HideVanillaNodesByImportedTexPaths(PawnRenderTree tree, PawnSkinDef skinDef)
        {
            if (tree?.rootNode == null || skinDef?.layers == null || skinDef.layers.Count == 0)
                return;

            var importedTexPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in skinDef.layers)
            {
                if (layer == null || string.IsNullOrEmpty(layer.texPath))
                    continue;

                // 过滤动态/未知贴图，避免误隐藏
                if (layer.texPath == "Dynamic/Unknown" || layer.texPath == "Unknown" || layer.texPath.StartsWith("Dynamic/", StringComparison.OrdinalIgnoreCase))
                    continue;

                importedTexPaths.Add(NormalizeTexPath(layer.texPath));
            }

            if (importedTexPaths.Count == 0)
                return;

            HideVanillaNodesByTexPathRecursive(tree.rootNode, tree.pawn, importedTexPaths);
        }

        /// <summary>
        /// 递归按“真实渲染纹理路径”隐藏匹配节点
        /// </summary>
        private static void HideVanillaNodesByTexPathRecursive(PawnRenderNode node, Pawn? pawn, HashSet<string> importedTexPaths)
        {
            if (node == null)
                return;

            // 自定义节点不参与原版去重隐藏
            if (!(node is PawnRenderNode_Custom))
            {
                string nodeTexPath = ResolveNodeTexturePathForHide(node, pawn);
                if (!string.IsNullOrEmpty(nodeTexPath) && importedTexPaths.Contains(nodeTexPath))
                {
                    HideNode(node);
                }
            }

            if (node.children == null) return;
            foreach (var child in node.children)
            {
                if (child == null) continue;
                HideVanillaNodesByTexPathRecursive(child, pawn, importedTexPaths);
            }
        }

        /// <summary>
        /// 获取节点用于隐藏匹配的真实纹理路径
        /// 优先级：GraphicFor -> PrimaryGraphic -> Graphics -> Props.texPath
        /// </summary>
        private static string ResolveNodeTexturePathForHide(PawnRenderNode node, Pawn? pawn)
        {
            // 1) 动态 GraphicFor（HAR/异种常见）
            if (pawn != null)
            {
                try
                {
                    var g = node.GraphicFor(pawn);
                    if (g != null && !string.IsNullOrEmpty(g.path))
                        return NormalizeTexPath(g.path);
                }
                catch
                {
                    // 忽略单节点异常，继续回退
                }
            }

            // 2) PrimaryGraphic
            try
            {
                var primary = node.PrimaryGraphic;
                if (primary != null && !string.IsNullOrEmpty(primary.path))
                    return NormalizeTexPath(primary.path);
            }
            catch { }

            // 3) Graphics 列表
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

            // 4) 静态 Props 路径
            return NormalizeTexPath(node.Props?.texPath ?? string.Empty);
        }

        private static string NormalizeTexPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim();
        }

        /// <summary>
        /// 从 NodePath 提取末段 tag（如 Root/Body:0/Head:0 -> Head）
        /// </summary>
        private static string ExtractTerminalTagFromNodePath(string nodePath)
        {
            if (string.IsNullOrEmpty(nodePath))
                return string.Empty;

            var segments = nodePath.Split('/');
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(segments[i]))
                    continue;

                var (tagName, _) = ParsePathSegment(segments[i]);
                if (!string.IsNullOrEmpty(tagName) && !tagName.Equals("Root", StringComparison.OrdinalIgnoreCase))
                {
                    return tagName;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 阶段4: 通过 NodePath 路径查找节点
        /// 路径格式: "Root/Body:0/Head:0" 或 "Root/Body/Head:0"
        /// </summary>
        private static PawnRenderNode? FindNodeByPath(PawnRenderTree tree, string path, bool warnOnFail = true)
        {
            if (string.IsNullOrEmpty(path) || tree.rootNode == null)
                return null;

            try
            {
                // 分割路径
                var segments = path.Split('/');
                if (segments.Length == 0)
                    return null;

                // 第一段应该是 "Root" 或 "Root:0"（兼容两种格式）
                var (firstTag, _) = ParsePathSegment(segments[0]);
                if (firstTag != "Root")
                {
                    if (warnOnFail) Log.Warning($"[CharacterStudio] NodePath 必须以 'Root' 开头: {path}");
                    return null;
                }

                // 从根节点开始遍历
                PawnRenderNode currentNode = tree.rootNode;

                // 从第二段开始匹配子节点（严格模式：tag + index）
                for (int i = 1; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    var (tagName, index) = ParsePathSegment(segment);

                    var matchedChild = FindChildByTagAndIndex(currentNode, tagName, index);
                    if (matchedChild == null)
                    {
                        // 回退：只按 tag 匹配（忽略 index），提高跨模组稳定性
                        matchedChild = FindChildByTagFirst(currentNode, tagName);
                        if (matchedChild == null)
                        {
                            if (warnOnFail) Log.Warning($"[CharacterStudio] 在路径 '{path}' 中找不到段 '{segment}'");
                            return null;
                        }
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

        /// <summary>
        /// 解析路径段，提取 tagName 和 index
        /// 格式: "TagName:0" 或 "TagName"（默认索引为0）
        /// </summary>
        private static (string tagName, int index) ParsePathSegment(string segment)
        {
            int colonIndex = segment.LastIndexOf(':');
            if (colonIndex > 0 && colonIndex < segment.Length - 1)
            {
                string tagPart = segment.Substring(0, colonIndex);
                string indexPart = segment.Substring(colonIndex + 1);
                if (int.TryParse(indexPart, out int index))
                {
                    return (tagPart, index);
                }
            }
            // 没有索引后缀，默认为0
            return (segment, 0);
        }

        /// <summary>
        /// 在子节点中查找匹配 tag 和 index 的节点
        /// </summary>
        private static PawnRenderNode? FindChildByTagAndIndex(PawnRenderNode parent, string tagName, int targetIndex)
        {
            if (parent.children == null)
                return null;

            int currentIndex = 0;
            foreach (var child in parent.children)
            {
                if (child == null) continue;

                string childTag = child.Props?.tagDef?.defName ?? "Untagged";
                if (childTag == tagName)
                {
                    if (currentIndex == targetIndex)
                    {
                        return child;
                    }
                    currentIndex++;
                }
            }

            return null;
        }

        /// <summary>
        /// 在子节点中按 tag 查找第一个匹配节点（忽略 index）
        /// </summary>
        private static PawnRenderNode? FindChildByTagFirst(PawnRenderNode parent, string tagName)
        {
            if (parent.children == null)
                return null;

            foreach (var child in parent.children)
            {
                if (child == null) continue;
                string childTag = child.Props?.tagDef?.defName ?? "Untagged";
                if (childTag == tagName)
                {
                    return child;
                }
            }

            return null;
        }

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

            // 直接清空 Graphics[] 缓存，避免渲染提交 Mesh
            ClearNodeGraphicsCache(node);
            HideChildNodes(node, hidden);
        }

        // 存储隐藏节点状态（按渲染树分组），避免跨 Pawn 引用污染与失效引用
        private static ConditionalWeakTable<PawnRenderTree, HashSet<PawnRenderNode>> hiddenNodesByTree =
            new ConditionalWeakTable<PawnRenderTree, HashSet<PawnRenderNode>>();

        // P3: 移除 trackedHiddenTrees（强引用阻止 GC 回收已卸载地图的渲染树）

        // 保存被清空节点的原始 Graphics[] 数组，供恢复时使用
        private static ConditionalWeakTable<PawnRenderNode, Graphic[]> savedGraphicsByNode =
            new ConditionalWeakTable<PawnRenderNode, Graphic[]>();

        private static HashSet<PawnRenderNode> GetHiddenSet(PawnRenderTree tree)
        {
            return hiddenNodesByTree.GetOrCreateValue(tree);
        }

        /// <summary>
        /// 通过标签名隐藏节点
        /// 使用自定义标记而非 debugEnabled，避免与调试工具冲突
        /// </summary>
        private static void HideNodeByTagName(Dictionary<PawnRenderNodeTagDef, PawnRenderNode> nodesByTag, string tagName)
        {
            foreach (var kvp in nodesByTag)
            {
                if (kvp.Key.defName == tagName && kvp.Value != null)
                {
                    var node = kvp.Value;
                    
                    try
                    {
                        HideNode(node);
                    }
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
                var existing = field.GetValue(node) as Graphic[];
                if (existing != null && existing.Length > 0)
                {
                    savedGraphicsByNode.Remove(node);
                    savedGraphicsByNode.Add(node, existing);
                    field.SetValue(node, Array.Empty<Graphic>());
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
                    field.SetValue(node, saved);
                    savedGraphicsByNode.Remove(node);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 恢复图形缓存出错 ({node}): {ex.Message}");
            }
        }

        /// <summary>
        /// 递归隐藏子节点
        /// 注意：跳过 PawnRenderNode_Custom 类型的节点，因为这些是自定义图层
        /// 自定义图层虽然作为原版节点的子节点（用于定位），但不应该随原版节点一起隐藏
        /// </summary>
        private static void HideChildNodes(PawnRenderNode node, HashSet<PawnRenderNode> hidden)
        {
            if (node.children == null) return;

            foreach (var child in node.children)
            {
                if (child == null) continue;

                if (child is PawnRenderNode_Custom)
                {
                    continue;
                }

                if (hidden.Add(child))
                {
                    ClearNodeGraphicsCache(child);
                    HideChildNodes(child, hidden);
                }
            }
        }

        /// <summary>
        /// hideVanillaBody 兼容性回退：处理非标准 Body 标签的种族/模组
        /// </summary>
        private static void HideVanillaBodyFallback(PawnRenderTree tree, Dictionary<PawnRenderNodeTagDef, PawnRenderNode> nodesByTag)
        {
            if (tree?.rootNode == null)
                return;

            // 先尝试一组常见标签名
            string[] commonBodyTags = { "Torso", "Core", "BodyBase", "NakedBody", "AlienBody", "Body" };
            foreach (var tag in commonBodyTags)
            {
                HideNodeByTagName(nodesByTag, tag);
            }

            // 再递归按启发式隐藏：标签/调试名包含 body/torso，且不是服装类节点
            HideBodyLikeNodesRecursive(tree.rootNode);
        }

        /// <summary>
        /// 递归隐藏 Body-like 节点（跳过服装与自定义节点）
        /// </summary>
        private static void HideBodyLikeNodesRecursive(PawnRenderNode node)
        {
            if (node == null) return;

            if (!(node is PawnRenderNode_Custom) && IsBodyLikeNode(node))
            {
                HideNode(node);
            }

            if (node.children == null) return;
            foreach (var child in node.children)
            {
                if (child == null) continue;
                HideBodyLikeNodesRecursive(child);
            }
        }

        /// <summary>
        /// 判断节点是否属于“身体主体”类别
        /// </summary>
        private static bool IsBodyLikeNode(PawnRenderNode node)
        {
            var props = node.Props;
            string tag = props?.tagDef?.defName ?? string.Empty;
            string workerName = props?.workerClass?.Name ?? string.Empty;
            string label = node.ToString() ?? string.Empty;

            // 过滤服装/装备相关节点
            if (tag.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                tag.IndexOf("Headgear", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workerName.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                workerName.IndexOf("Headgear", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            // 命中 body/torso/core 等关键词
            return tag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tag.IndexOf("Torso", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tag.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   label.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   label.IndexOf("Torso", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 清除隐藏状态（用于皮肤切换时恢复）
        /// </summary>
        public static void ClearHiddenNodes()
        {
            // P3: 不再依赖 trackedHiddenTrees 遍历，直接重建 ConditionalWeakTable
            hiddenNodesByTree = new ConditionalWeakTable<PawnRenderTree, HashSet<PawnRenderNode>>();
            savedGraphicsByNode = new ConditionalWeakTable<PawnRenderNode, Graphic[]>();
        }

        // P3: RestoreAllHiddenNodeGraphics 已不再使用（去除 trackedHiddenTrees 后无法遍历）
        // 单棵树的恢复通过 RestoreAndRemoveHiddenForTree 实现

        private static void RestoreAndRemoveHiddenForTree(PawnRenderTree tree)
        {
            if (tree == null)
                return;

            if (hiddenNodesByTree.TryGetValue(tree, out var oldHidden))
            {
                foreach (var oldNode in oldHidden)
                {
                    if (oldNode != null)
                    {
                        RestoreNodeGraphicsCache(oldNode);
                    }
                }
            }

            hiddenNodesByTree.Remove(tree);
            // P3: 不再操作 trackedHiddenTrees
        }


        /// <summary>
        /// 刷新指定 Pawn 的隐藏节点（在皮肤变更时调用）
        /// </summary>
        public static void RefreshHiddenNodes(Pawn pawn)
        {
            if (pawn?.Drawer?.renderer?.renderTree == null) return;
            
            var tree = pawn.Drawer.renderer.renderTree;
            
            // 先恢复上一轮被清空的 Graphics[] 缓存
            RestoreAndRemoveHiddenForTree(tree);

            // 移除所有旧的自定义图层节点，确保状态纯净
            // 这对于编辑器预览至关重要，因为图层列表可能发生了变化
            RemoveAllCustomNodes(tree);
            
            // 获取皮肤定义
            var skinComp = pawn.GetComp<Core.CompPawnSkin>();
            var skinDef = skinComp?.ActiveSkin;
            
            if (skinDef != null)
            {
                // 重新处理隐藏逻辑，使用皮肤定义中的设置
                ProcessVanillaHiding(tree, skinDef, skinDef.hideVanillaHead, skinDef.hideVanillaHair, out bool tagHidingAvailable);

                // 强制注入当前的所有自定义图层
                bool anyNodesInjected = InjectCustomLayers(tree, pawn, skinDef);

                // 与 Postfix 保持一致：仅在缺失 tag 索引时触发 texPath 兜底隐藏。
                if (anyNodesInjected && !tagHidingAvailable && IsGraphicsReadyForVanillaNodes(tree))
                {
                    HideVanillaNodesByImportedTexPaths(tree, skinDef);
                }
            }
            
            // 标记树为脏，触发重绘和排序更新
            try
            {
                tree.SetDirty();
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 刷新渲染树状态时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 移除所有自定义节点
        /// </summary>
        private static void RemoveAllCustomNodes(PawnRenderTree tree)
        {
            if (tree?.rootNode == null) return;
            RemoveCustomNodesRecursive(tree.rootNode);
        }

        /// <summary>
        /// 递归移除自定义节点
        /// </summary>
        private static void RemoveCustomNodesRecursive(PawnRenderNode node)
        {
            if (node.children == null || node.children.Length == 0) return;

            // 检查是否有子节点是自定义节点
            bool hasCustomChild = false;
            foreach (var child in node.children)
            {
                if (child is PawnRenderNode_Custom)
                {
                    hasCustomChild = true;
                    RuntimeAssetLoader.UnregisterNodeOffsets(child.GetHashCode());
                    // 不需要 break，我们还需要递归处理其他节点
                }
                else
                {
                    // 递归处理非自定义节点的子节点
                    RemoveCustomNodesRecursive(child);
                }
            }

            if (hasCustomChild)
            {
                // 重建 children 数组，排除 PawnRenderNode_Custom
                var newChildren = new List<PawnRenderNode>();
                foreach (var child in node.children)
                {
                    if (!(child is PawnRenderNode_Custom))
                    {
                        newChildren.Add(child);
                    }
                }
                node.children = newChildren.ToArray();
            }
        }

        /// <summary>
        /// 强制重建 Pawn 的渲染树（用于皮肤变更后完全刷新）
        /// </summary>
        public static void ForceRebuildRenderTree(Pawn pawn)
        {
            if (pawn?.Drawer?.renderer == null) return;

            try
            {
                // P2: 使用缓存的反射字段
                var tree = renderTreeField_Cached?.GetValue(pawn.Drawer.renderer) as PawnRenderTree;
                if (tree != null)
                {
                    // 不再调用 RestoreAndRemoveHiddenForTree：
                    // 调用方（ApplySkinToPawn/ClearSkinFromPawn）已由 RefreshHiddenNodes
                    // 处理了完整的 恢复→清理→隐藏→注入 流程。
                    // 如果在此恢复隐藏，P1 守卫会跳过 Postfix，导致隐藏丢失。

                    setDirtyMethod_Cached?.Invoke(tree, null);
                    setupCompleteField_Cached?.SetValue(tree, false);
                }

                // 设置图形为脏以触发重新渲染
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 强制重建渲染树时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查并注入自定义图层（如果尚未注入）
        /// </summary>
        private static void InjectCustomLayersIfNeeded(PawnRenderTree tree, Pawn pawn, PawnSkinDef skinDef)
        {
            if (skinDef?.layers == null || skinDef.layers.Count == 0) return;

            // 检查是否已经注入了自定义图层
            bool hasCustomNodes = false;
            CheckForCustomNodes(tree.rootNode, ref hasCustomNodes);

            if (!hasCustomNodes)
            {
                // 尚未注入，执行注入
                var injected = InjectCustomLayers(tree, pawn, skinDef);
                if (injected)
                {
                    Log.Message($"[CharacterStudio] 在皮肤变更时注入了 {skinDef.layers.Count} 个自定义图层");
                }
            }
        }

        /// <summary>
        /// 递归检查是否存在自定义节点
        /// </summary>
        private static void CheckForCustomNodes(PawnRenderNode? node, ref bool found)
        {
            if (node == null || found) return;

            if (node is PawnRenderNode_Custom)
            {
                found = true;
                return;
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    CheckForCustomNodes(child, ref found);
                    if (found) return;
                }
            }
        }

        /// <summary>
        /// 注入自定义图层
        /// 阶段4: 支持 anchorPath（NodePath）优先于 anchorTag
        /// </summary>
        private static bool InjectCustomLayers(PawnRenderTree tree, Pawn pawn, PawnSkinDef skinDef)
        {
            if (skinDef.layers == null || skinDef.layers.Count == 0) return false;

            bool anyNodesInjected = false;

            foreach (var layer in skinDef.layers)
            {
                if (!layer.visible) continue;
                if (string.IsNullOrEmpty(layer.texPath)) continue;
                // 防御性检查：过滤无效的纹理路径
                if (layer.texPath == "Dynamic/Unknown" || layer.texPath == "Error" || layer.texPath == "No Graphic (Logic Only)") continue;
                // 额外检查：过滤包含 Unknown 或 Dynamic 关键字的路径
                if (layer.texPath.Contains("Unknown") || layer.texPath.StartsWith("Dynamic/")) continue;

                try
                {
                    // 创建渲染节点属性
                    var props = CreateNodeProperties(layer);

                    // 阶段4: 优先使用 anchorPath（NodePath 精确定位）
                    PawnRenderNode? parentNode = null;
                    
                    if (!string.IsNullOrEmpty(layer.anchorPath))
                    {
                        parentNode = FindNodeByPath(tree, layer.anchorPath);
                        if (parentNode == null)
                        {
                            Log.Warning($"[CharacterStudio] 无法通过路径找到锚点节点: {layer.anchorPath}，回退到 anchorTag");
                        }
                    }
                    
                    // 回退到 anchorTag
                    if (parentNode == null)
                    {
                        parentNode = FindParentNode(tree, layer.anchorTag);
                    }
                    
                    // 智能回退：如果仍然是 Root (说明 anchorTag 也没找到，或者是 Root 本身)
                    // 且 anchorTag 不是 "Root"，尝试基于名称的回退
                    if (parentNode == tree.rootNode && layer.anchorTag != "Root")
                    {
                        if (layer.anchorTag.Contains("Head") || layer.anchorTag.Contains("Eye") || layer.anchorTag.Contains("Mouth") || layer.anchorTag.Contains("Face"))
                        {
                            var headNode = FindParentNode(tree, "Head");
                            if (headNode != tree.rootNode)
                            {
                                parentNode = headNode;
                                Log.Warning($"[CharacterStudio] 锚点 {layer.anchorTag} 未找到，智能回退到 Head");
                            }
                        }
                        else if (layer.anchorTag.Contains("Body") || layer.anchorTag.Contains("Apparel"))
                        {
                            var bodyNode = FindParentNode(tree, "Body");
                            if (bodyNode != tree.rootNode)
                            {
                                parentNode = bodyNode;
                                Log.Warning($"[CharacterStudio] 锚点 {layer.anchorTag} 未找到，智能回退到 Body");
                            }
                        }
                    }
                    
                    if (parentNode == null)
                    {
                        Log.Warning($"[CharacterStudio] 无法找到锚点节点: anchorPath={layer.anchorPath}, anchorTag={layer.anchorTag}");
                        continue;
                    }

                    // 关键修改：不再在这里自动隐藏锚点节点
                    // 隐藏节点的逻辑应该由 skinDef.hiddenPaths 控制
                    // 这样可以避免导入后的图层自己被隐藏
                    // 如果用户需要隐藏原版节点，应在导入时添加到 hiddenPaths

                    // 创建渲染节点
                    var node = (PawnRenderNode)Activator.CreateInstance(
                        props.nodeClass,
                        new object[] { pawn, props, tree }
                    );

                    // 注入配置到自定义节点
                    if (node is PawnRenderNode_Custom customNode)
                    {
                        customNode.config = layer;
                    }

                    // 添加为子节点
                    if (node != null)
                    {
                        // 直接使用 layer.scale —— 该值在捕获阶段（RenderTreeParser.ScaleFor）
                        // 已经包含了节点的 props.drawSize 缩放。
                        // 不再叠乘 anchorScale，避免二次放大。
                        node.Props.drawSize = layer.scale;

                        node.parent = parentNode;
                        AddChildToNode(parentNode, node);
                        anyNodesInjected = true;
                        
                        // 应用调试偏移
                        // 注意：offset 通过 debugOffset 应用（基类 OffsetFor 会使用）
                        node.debugOffset = layer.offset;
                        // 关键修复：不设置 debugScale，因为 props.drawSize 已经包含了缩放
                        // 如果同时设置两者，缩放会被应用两次（drawSize * debugScale）
                        // node.debugScale 保持默认值 1
                        
                        // 注册侧视图偏移量到 RuntimeAssetLoader
                        if (layer.offsetEast != Vector3.zero)
                        {
                            RuntimeAssetLoader.RegisterNodeOffsetEast(node.GetHashCode(), layer.offsetEast);
                        }
                        // 注册北方向偏移量到 RuntimeAssetLoader
                        if (layer.offsetNorth != Vector3.zero)
                        {
                            RuntimeAssetLoader.RegisterNodeOffsetNorth(node.GetHashCode(), layer.offsetNorth);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] 注入图层 {layer.layerName} 时出错: {ex}");
                }
            }

            // 关键修复：在所有节点注入完成后，重新初始化整个祖先字典
            // 这样可以确保所有节点（包括新注入的）都正确注册到 nodeAncestors 中
            if (anyNodesInjected)
            {
                ReinitializeNodeAncestors(tree);
            }

            return anyNodesInjected;
        }

        /// <summary>
        /// 重新初始化节点祖先字典
        /// 通过反射调用游戏的 InitializeAncestors 方法，确保所有节点都被正确注册
        /// </summary>
        private static void ReinitializeNodeAncestors(PawnRenderTree tree)
        {
            try
            {
                // P2: 使用缓存的反射字段
                if (nodeAncestorsField_Cached == null)
                {
                    Log.Warning("[CharacterStudio] 无法找到 nodeAncestors 字段");
                    return;
                }

                var nodeAncestors = nodeAncestorsField_Cached.GetValue(tree) as Dictionary<PawnRenderNode, List<PawnRenderNode>>;
                if (nodeAncestors == null)
                {
                    nodeAncestors = new Dictionary<PawnRenderNode, List<PawnRenderNode>>();
                    nodeAncestorsField_Cached.SetValue(tree, nodeAncestors);
                }
                else
                {
                    nodeAncestors.Clear();
                }

                if (initAncestorsMethod_Cached != null)
                {
                    initAncestorsMethod_Cached.Invoke(tree, null);
                }
                else
                {
                    ManuallyInitializeAncestors(tree.rootNode, nodeAncestors, new List<PawnRenderNode>());
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 重新初始化节点祖先时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 手动初始化节点祖先字典（作为反射调用的后备方案）
        /// </summary>
        private static void ManuallyInitializeAncestors(PawnRenderNode? node, Dictionary<PawnRenderNode, List<PawnRenderNode>> nodeAncestors, List<PawnRenderNode> currentPath)
        {
            if (node == null) return;

            // 添加当前节点到路径
            var pathWithCurrent = new List<PawnRenderNode>(currentPath) { node };
            
            // 注册当前节点的祖先链
            nodeAncestors[node] = new List<PawnRenderNode>(pathWithCurrent);

            // 递归处理子节点
            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    ManuallyInitializeAncestors(child, nodeAncestors, pathWithCurrent);
                }
            }
        }

        /// <summary>
        /// 创建节点属性
        /// </summary>
        private static PawnRenderNodeProperties CreateNodeProperties(PawnLayerConfig layer)
        {
            var props = new PawnRenderNodeProperties
            {
                texPath = layer.texPath,
                workerClass = layer.workerClass ?? typeof(PawnRenderNodeWorker_CustomLayer),
                nodeClass = typeof(PawnRenderNode_Custom),
                baseLayer = layer.drawOrder,
                flipGraphic = layer.flipHorizontal,
                rotDrawMode = layer.rotDrawMode,
                drawSize = layer.scale,
                debugLabel = layer.layerName
            };

            // 设置颜色类型
            switch (layer.colorType)
            {
                case LayerColorType.Hair:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Hair;
                    break;
                case LayerColorType.Skin:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Skin;
                    break;
                case LayerColorType.White:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Custom;
                    props.color = UnityEngine.Color.white;
                    break;
                default:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Custom;
                    props.color = layer.customColor;
                    break;
            }

            return props;
        }

        /// <summary>
        /// 查找父节点
        /// </summary>
        private static PawnRenderNode? FindParentNode(PawnRenderTree tree, string anchorTag)
        {
            if (string.IsNullOrEmpty(anchorTag))
            {
                return tree.rootNode;
            }

            // 使用反射获取 nodesByTag 字典
            var nodesByTagField = AccessTools.Field(typeof(PawnRenderTree), "nodesByTag");
            if (nodesByTagField == null) return tree.rootNode;

            var nodesByTag = nodesByTagField.GetValue(tree) as Dictionary<PawnRenderNodeTagDef, PawnRenderNode>;
            if (nodesByTag == null) return tree.rootNode;

            // 查找匹配的标签
            foreach (var kvp in nodesByTag)
            {
                if (kvp.Key.defName == anchorTag)
                {
                    return kvp.Value;
                }
            }

            // 未找到，返回根节点
            return tree.rootNode;
        }

        /// <summary>
        /// 添加子节点
        /// </summary>
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
    }
}
