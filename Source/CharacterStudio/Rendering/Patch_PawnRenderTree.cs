using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using CharacterStudio.Core;
using UnityEngine;
using Verse;
using System.Linq;

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
                    Log.Message("[CharacterStudio] PawnRenderNodeWorker.CanDrawNow 补丁已应用");
                }

                // 补丁 PawnRenderNode.GraphicFor 方法 (用于隐藏 Mesh 但允许子节点绘制)
                // 显式指定参数以确保找到正确的方法
                var graphicMethod = AccessTools.Method(typeof(PawnRenderNode), "GraphicFor", new Type[] { typeof(Pawn) });
                var graphicPrefix = AccessTools.Method(typeof(Patch_PawnRenderTree), nameof(GraphicFor_Prefix));

                if (graphicMethod != null && graphicPrefix != null)
                {
                    harmony.Patch(graphicMethod, prefix: new HarmonyMethod(graphicPrefix));
                    Log.Message("[CharacterStudio] PawnRenderNode.GraphicFor 补丁已应用");
                }
                else
                {
                    Log.Error("[CharacterStudio] 无法找到 PawnRenderNode.GraphicFor(Pawn) 方法，隐藏修复可能失效");
                }

                // 关键修复：额外补丁 PawnRenderNode_Head.GraphicFor 方法
                // 因为 PawnRenderNode_Head 完全重写了 GraphicFor，基类补丁不会被调用
                var headGraphicMethod = AccessTools.Method(typeof(PawnRenderNode_Head), "GraphicFor", new Type[] { typeof(Pawn) });
                if (headGraphicMethod != null && graphicPrefix != null)
                {
                    harmony.Patch(headGraphicMethod, prefix: new HarmonyMethod(graphicPrefix));
                    Log.Message("[CharacterStudio] PawnRenderNode_Head.GraphicFor 补丁已应用");
                }
                else
                {
                    Log.Warning("[CharacterStudio] 无法找到 PawnRenderNode_Head.GraphicFor(Pawn) 方法");
                }

                // 同样补丁 PawnRenderNode_Body.GraphicFor（如果存在）
                var bodyType = AccessTools.TypeByName("Verse.PawnRenderNode_Body");
                if (bodyType != null)
                {
                    var bodyGraphicMethod = AccessTools.Method(bodyType, "GraphicFor", new Type[] { typeof(Pawn) });
                    if (bodyGraphicMethod != null && graphicPrefix != null)
                    {
                        harmony.Patch(bodyGraphicMethod, prefix: new HarmonyMethod(graphicPrefix));
                        Log.Message("[CharacterStudio] PawnRenderNode_Body.GraphicFor 补丁已应用");
                    }
                }

                // 补丁 PawnRenderNode_Hair.GraphicFor（如果存在）
                var hairType = AccessTools.TypeByName("Verse.PawnRenderNode_Hair");
                if (hairType != null)
                {
                    var hairGraphicMethod = AccessTools.Method(hairType, "GraphicFor", new Type[] { typeof(Pawn) });
                    if (hairGraphicMethod != null && graphicPrefix != null)
                    {
                        harmony.Patch(hairGraphicMethod, prefix: new HarmonyMethod(graphicPrefix));
                        Log.Message("[CharacterStudio] PawnRenderNode_Hair.GraphicFor 补丁已应用");
                    }
                }

                // HAR 兼容性：补丁所有 PawnRenderNode 的派生类的 GraphicFor 方法
                // 这确保了任何模组添加的自定义渲染节点类型也能被正确隐藏
                PatchAllDerivedGraphicForMethods(harmony, graphicPrefix);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用 PawnRenderTree 补丁时出错: {ex}");
            }
        }

        /// <summary>
        /// HAR 兼容性：补丁所有 PawnRenderNode 派生类的 GraphicFor 方法
        /// 这确保了外星人种族等模组的自定义节点类型也能被正确隐藏
        /// </summary>
        private static void PatchAllDerivedGraphicForMethods(Harmony harmony, MethodInfo graphicPrefix)
        {
            if (graphicPrefix == null) return;
            
            try
            {
                // 获取所有已加载的程序集
                var pawnRenderNodeType = typeof(PawnRenderNode);
                var pawnType = typeof(Pawn);
                
                // 已经补丁过的类型（避免重复）
                var patchedTypes = new HashSet<Type>
                {
                    typeof(PawnRenderNode),
                    typeof(PawnRenderNode_Head)
                };
                
                // 遍历所有程序集查找 PawnRenderNode 的派生类
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            // 跳过已补丁的类型
                            if (patchedTypes.Contains(type)) continue;
                            
                            // 检查是否是 PawnRenderNode 的派生类
                            if (!pawnRenderNodeType.IsAssignableFrom(type)) continue;
                            if (type.IsAbstract) continue;
                            
                            // 查找该类型自己声明的 GraphicFor 方法（不是继承的）
                            var graphicMethod = type.GetMethod("GraphicFor",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                                null, new Type[] { pawnType }, null);
                            
                            if (graphicMethod != null)
                            {
                                try
                                {
                                    harmony.Patch(graphicMethod, prefix: new HarmonyMethod(graphicPrefix));
                                    patchedTypes.Add(type);
                                    Log.Message($"[CharacterStudio] {type.FullName}.GraphicFor 补丁已应用 (派生类自动发现)");
                                }
                                catch (Exception patchEx)
                                {
                                    Log.Warning($"[CharacterStudio] 无法补丁 {type.FullName}.GraphicFor: {patchEx.Message}");
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 某些程序集可能无法枚举类型，忽略
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 枚举派生类时出错: {ex.Message}");
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

                var canDrawMethod = AccessTools.Method(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.CanDrawNow));
                if (canDrawMethod != null)
                {
                    harmonyInstance.Unpatch(canDrawMethod, HarmonyPatchType.Prefix, harmonyInstance.Id);
                }

                var graphicMethod = AccessTools.Method(typeof(PawnRenderNode), "GraphicFor");
                if (graphicMethod != null)
                {
                    harmonyInstance.Unpatch(graphicMethod, HarmonyPatchType.Prefix, harmonyInstance.Id);
                }

                // 移除 PawnRenderNode_Head.GraphicFor 补丁
                var headGraphicMethod = AccessTools.Method(typeof(PawnRenderNode_Head), "GraphicFor", new Type[] { typeof(Pawn) });
                if (headGraphicMethod != null)
                {
                    harmonyInstance.Unpatch(headGraphicMethod, HarmonyPatchType.Prefix, harmonyInstance.Id);
                }

                // 移除 PawnRenderNode_Body.GraphicFor 补丁
                var bodyType = AccessTools.TypeByName("Verse.PawnRenderNode_Body");
                if (bodyType != null)
                {
                    var bodyGraphicMethod = AccessTools.Method(bodyType, "GraphicFor", new Type[] { typeof(Pawn) });
                    if (bodyGraphicMethod != null)
                    {
                        harmonyInstance.Unpatch(bodyGraphicMethod, HarmonyPatchType.Prefix, harmonyInstance.Id);
                    }
                }

                // 移除 PawnRenderNode_Hair.GraphicFor 补丁
                var hairType = AccessTools.TypeByName("Verse.PawnRenderNode_Hair");
                if (hairType != null)
                {
                    var hairGraphicMethod = AccessTools.Method(hairType, "GraphicFor", new Type[] { typeof(Pawn) });
                    if (hairGraphicMethod != null)
                    {
                        harmonyInstance.Unpatch(hairGraphicMethod, HarmonyPatchType.Prefix, harmonyInstance.Id);
                    }
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
            if (node != null && hiddenNodes.Contains(node))
            {
                // 关键修复：如果被隐藏的节点有自定义图层作为子节点，必须允许 Draw 被调用
                // 否则子节点（我们的自定义图层）将无法绘制
                if (HasCustomChildren(node))
                {
                     // 允许执行，但在 GraphicFor 补丁中我们会返回空图形以隐藏自身
                     return true;
                }

                __result = false;
                return false; // 跳过原方法，完全隐藏
            }
            return true;
        }

        // 缓存一个空的 Graphic 实例
        private static Graphic_Empty? emptyGraphic;
        private static Graphic_Empty EmptyGraphic
        {
            get
            {
                if (emptyGraphic == null)
                {
                    emptyGraphic = new Graphic_Empty();
                    // 注意：不调用 Init()，因为 Graphic_Empty 已经重写了 MatAt() 直接返回 BaseContent.ClearMat
                    // 调用 Init() 会尝试加载纹理，导致 "Could not load Texture2D" 红字错误
                }
                return emptyGraphic;
            }
        }

        /// <summary>
        /// GraphicFor 前缀补丁
        /// 如果节点在隐藏列表中但有自定义子节点，返回空图形以隐藏自身
        /// </summary>
        private static bool GraphicFor_Prefix(PawnRenderNode __instance, ref Graphic? __result)
        {
            if (__instance != null && hiddenNodes.Contains(__instance))
            {
                // 如果这是被隐藏的节点，不能返回 null，因为 RenderNode.Draw 中如果 graphic 为 null 会直接返回
                // 从而跳过子节点的绘制。
                // 所以我们必须返回一个"透明/空"的 Graphic，让 Draw 方法继续执行并绘制子节点。
                __result = EmptyGraphic;
                return false; // 跳过原方法
            }
            return true;
        }

        /// <summary>
        /// 空图形类，用于隐藏节点但不中断绘制流程
        /// </summary>
        public class Graphic_Empty : Graphic_Single
        {
            public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation) { }
            public override void Print(SectionLayer layer, Thing thing, float extraRotation) { }
            public override Material MatAt(Rot4 rot, Thing thing = null) { return BaseContent.ClearMat; }
        }

        /// <summary>
        /// 检查节点是否有自定义子节点
        /// </summary>
        private static bool HasCustomChildren(PawnRenderNode node)
        {
            if (node.children == null) return false;
            for (int i = 0; i < node.children.Length; i++)
            {
                if (node.children[i] is PawnRenderNode_Custom) return true;
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

                // 优先级1：检查基因皮肤（生物科技集成）
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
                    // 优先级2：检查 CompPawnSkin 组件
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

                // 处理隐藏原版元素（非叠加模式时）
                if (!overlayMode)
                {
                    ProcessVanillaHiding(__instance, skinDef, hideVanillaHead, hideVanillaHair);
                }

                // 注入自定义图层
                InjectCustomLayers(__instance, pawn, skinDef);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 处理渲染树时出错: {ex}");
            }
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
        private static void ProcessVanillaHiding(PawnRenderTree tree, PawnSkinDef skinDef, bool hideHead, bool hideHair)
        {
            // 使用反射获取 nodesByTag 字典
            var nodesByTagField = AccessTools.Field(typeof(PawnRenderTree), "nodesByTag");
            if (nodesByTagField == null) return;

            var nodesByTag = nodesByTagField.GetValue(tree) as Dictionary<PawnRenderNodeTagDef, PawnRenderNode>;
            if (nodesByTag == null) return;

            // 隐藏头部
            if (hideHead || skinDef.hideVanillaHead)
            {
                HideNodeByTagName(nodesByTag, "Head");
            }

            // 隐藏头发
            if (hideHair || skinDef.hideVanillaHair)
            {
                HideNodeByTagName(nodesByTag, "Hair");
            }

            // 隐藏身体
            if (skinDef.hideVanillaBody)
            {
                HideNodeByTagName(nodesByTag, "Body");
            }

            // 处理自定义 hiddenTags 列表（向后兼容）
            #pragma warning disable CS0618 // 忽略过时警告
            if (skinDef.hiddenTags != null && skinDef.hiddenTags.Count > 0)
            {
                foreach (var tagName in skinDef.hiddenTags)
                {
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        HideNodeByTagName(nodesByTag, tagName);
                    }
                }
            }
            #pragma warning restore CS0618

            // 阶段4: 处理新版 hiddenPaths 列表（NodePath 精确定位）
            if (skinDef.hiddenPaths != null && skinDef.hiddenPaths.Count > 0)
            {
                foreach (var nodePath in skinDef.hiddenPaths)
                {
                    if (!string.IsNullOrEmpty(nodePath))
                    {
                        var targetNode = FindNodeByPath(tree, nodePath);
                        if (targetNode != null)
                        {
                            HideNode(targetNode);
                        }
                        else
                        {
                            Log.Warning($"[CharacterStudio] 无法找到路径对应的节点: {nodePath}");
                        }
                    }
                }
            }
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

                // 从第二段开始匹配子节点
                for (int i = 1; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    var (tagName, index) = ParsePathSegment(segment);

                    // 在当前节点的子节点中查找
                    var matchedChild = FindChildByTagAndIndex(currentNode, tagName, index);
                    if (matchedChild == null)
                    {
                        if (warnOnFail) Log.Warning($"[CharacterStudio] 在路径 '{path}' 中找不到段 '{segment}'");
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
        /// 隐藏单个节点（用于路径匹配后的隐藏）
        /// </summary>
        private static void HideNode(PawnRenderNode node)
        {
            if (node == null || hiddenNodes.Contains(node))
                return;

            hiddenNodes.Add(node);
            // 改为使用 CanDrawNow 补丁控制可见性，不再修改 debugScale
            // 这样可以避免破坏子节点的矩阵计算
            HideChildNodes(node);
        }

        // 存储隐藏节点的状态，避免与调试工具冲突
        private static readonly HashSet<PawnRenderNode> hiddenNodes = new HashSet<PawnRenderNode>();

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
                        if (!hiddenNodes.Contains(node))
                        {
                            hiddenNodes.Add(node);
                            // 同时隐藏子节点
                            HideChildNodes(node);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[CharacterStudio] 隐藏节点 {tagName} 时出错: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 递归隐藏子节点
        /// 注意：跳过 PawnRenderNode_Custom 类型的节点，因为这些是自定义图层
        /// 自定义图层虽然作为原版节点的子节点（用于定位），但不应该随原版节点一起隐藏
        /// </summary>
        private static void HideChildNodes(PawnRenderNode node)
        {
            if (node.children == null) return;
            
            foreach (var child in node.children)
            {
                if (child == null) continue;
                
                // 跳过自定义图层节点 - 它们不应该随父节点一起隐藏
                if (child is PawnRenderNode_Custom)
                {
                    continue;
                }
                
                if (!hiddenNodes.Contains(child))
                {
                    hiddenNodes.Add(child);
                    HideChildNodes(child);
                }
            }
        }

        /// <summary>
        /// 清除隐藏状态（用于皮肤切换时恢复）
        /// </summary>
        public static void ClearHiddenNodes()
        {
            hiddenNodes.Clear();
        }

        /// <summary>
        /// 刷新指定 Pawn 的隐藏节点（在皮肤变更时调用）
        /// </summary>
        public static void RefreshHiddenNodes(Pawn pawn)
        {
            if (pawn?.Drawer?.renderer?.renderTree == null) return;
            
            var tree = pawn.Drawer.renderer.renderTree;
            
            // 清除旧的隐藏节点
            hiddenNodes.Clear();
            
            // 移除所有旧的自定义图层节点，确保状态纯净
            // 这对于编辑器预览至关重要，因为图层列表可能发生了变化
            RemoveAllCustomNodes(tree);
            
            // 获取皮肤定义
            var skinComp = pawn.GetComp<Core.CompPawnSkin>();
            var skinDef = skinComp?.ActiveSkin;
            
            if (skinDef != null)
            {
                // 重新处理隐藏逻辑，使用皮肤定义中的设置
                ProcessVanillaHiding(tree, skinDef, skinDef.hideVanillaHead, skinDef.hideVanillaHair);
                
                // 强制注入当前的所有自定义图层
                InjectCustomLayers(tree, pawn, skinDef);
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
                // 清除旧的隐藏节点
                hiddenNodes.Clear();

                // 使用反射强制标记渲染树需要重建
                var rendererField = AccessTools.Field(typeof(PawnRenderer), "renderTree");
                if (rendererField != null)
                {
                    var tree = rendererField.GetValue(pawn.Drawer.renderer) as PawnRenderTree;
                    if (tree != null)
                    {
                        // 尝试调用 SetDirty 方法
                        var setDirtyMethod = AccessTools.Method(typeof(PawnRenderTree), "SetDirty");
                        if (setDirtyMethod != null)
                        {
                            setDirtyMethod.Invoke(tree, null);
                        }
                        
                        // 同时尝试重新设置 setupComplete 为 false 以强制重新初始化
                        var setupCompleteField = AccessTools.Field(typeof(PawnRenderTree), "setupComplete");
                        if (setupCompleteField != null)
                        {
                            setupCompleteField.SetValue(tree, false);
                        }
                    }
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
                InjectCustomLayers(tree, pawn, skinDef);
                Log.Message($"[CharacterStudio] 在皮肤变更时注入了 {skinDef.layers.Count} 个自定义图层");
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
        private static void InjectCustomLayers(PawnRenderTree tree, Pawn pawn, PawnSkinDef skinDef)
        {
            if (skinDef.layers == null || skinDef.layers.Count == 0) return;

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
        }

        /// <summary>
        /// 重新初始化节点祖先字典
        /// 通过反射调用游戏的 InitializeAncestors 方法，确保所有节点都被正确注册
        /// </summary>
        private static void ReinitializeNodeAncestors(PawnRenderTree tree)
        {
            try
            {
                // 获取 nodeAncestors 字段
                var nodeAncestorsField = AccessTools.Field(typeof(PawnRenderTree), "nodeAncestors");
                if (nodeAncestorsField == null)
                {
                    Log.Warning("[CharacterStudio] 无法找到 nodeAncestors 字段");
                    return;
                }

                // 清空现有字典
                var nodeAncestors = nodeAncestorsField.GetValue(tree) as Dictionary<PawnRenderNode, List<PawnRenderNode>>;
                if (nodeAncestors == null)
                {
                    // 如果字典不存在，创建一个新的
                    nodeAncestors = new Dictionary<PawnRenderNode, List<PawnRenderNode>>();
                    nodeAncestorsField.SetValue(tree, nodeAncestors);
                }
                else
                {
                    nodeAncestors.Clear();
                }

                // 尝试调用私有的 InitializeAncestors 方法
                var initMethod = AccessTools.Method(typeof(PawnRenderTree), "InitializeAncestors");
                if (initMethod != null)
                {
                    initMethod.Invoke(tree, null);
                }
                else
                {
                    // 如果找不到方法，手动遍历树并注册所有节点
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
                    Log.Message($"[CharacterStudio] CreateNodeProperties: {layer.layerName} → colorType=Hair, offset={layer.offset}, offsetNorth={layer.offsetNorth}");
                    break;
                case LayerColorType.Skin:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Skin;
                    Log.Message($"[CharacterStudio] CreateNodeProperties: {layer.layerName} → colorType=Skin, offset={layer.offset}, offsetNorth={layer.offsetNorth}");
                    break;
                case LayerColorType.White:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Custom;
                    props.color = UnityEngine.Color.white;
                    Log.Message($"[CharacterStudio] CreateNodeProperties: {layer.layerName} → colorType=White, offset={layer.offset}, offsetNorth={layer.offsetNorth}");
                    break;
                default:
                    props.colorType = PawnRenderNodeProperties.AttachmentColorType.Custom;
                    props.color = layer.customColor;
                    Log.Message($"[CharacterStudio] CreateNodeProperties: {layer.layerName} → colorType=Custom({layer.customColor}), offset={layer.offset}, offsetNorth={layer.offsetNorth}");
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
