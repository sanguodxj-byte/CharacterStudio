using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    public static partial class Patch_PawnRenderTree
    {
        // ─────────────────────────────────────────────
        // BaseAppearance 覆写
        // 将槽位贴图直接注入对应的原版渲染节点 Graphics[] 缓存
        // ─────────────────────────────────────────────

        private static readonly Type[] BaseAppearanceNodeTypes =
        {
            typeof(PawnRenderNode_Body),
            typeof(PawnRenderNode_Head),
            typeof(PawnRenderNode_Hair),
            typeof(PawnRenderNode_Beard)
        };

        private static readonly HashSet<MethodBase> patchedBaseAppearanceMethods = new HashSet<MethodBase>();

        // 仅用于当前 Head 基础槽位问题排查：
        // - 默认开启，方便定位实际角色是否命中基础槽位拦截
        // - 日志量远小于 RenderTreeParser 的全节点日志
        private static bool EnableHeadBaseSlotDiagnostics => true;

        private static void HeadDiag(string message)
        {
            if (EnableHeadBaseSlotDiagnostics)
                Log.Message("[CharacterStudio][HeadDiag] " + message);
        }

        private static void ApplyBaseAppearanceRuntimePatches(Harmony harmony)
        {
            try
            {
                PatchBaseAppearanceGraphicFor(harmony, typeof(PawnRenderNode));
                PatchBaseAppearanceGraphicFor(harmony, typeof(PawnRenderNode_Body));
                PatchBaseAppearanceGraphicFor(harmony, typeof(PawnRenderNode_Head));
                PatchBaseAppearanceGraphicFor(harmony, typeof(PawnRenderNode_Hair));
                PatchBaseAppearanceGraphicFor(harmony, typeof(PawnRenderNode_Beard));
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用 BaseAppearance 运行时补丁失败: {ex}");
            }
        }

        private static bool BaseAppearanceGraphicFor_Prefix(PawnRenderNode __instance, Pawn pawn, ref Graphic __result)
        {
            var slot = TryGetBaseAppearanceSlotForNode(__instance, pawn);

            if (slot == null)
            {
                if (EnableHeadBaseSlotDiagnostics && IsHeadLikeNode(__instance))
                {
                    string tag = __instance?.Props?.tagDef?.defName ?? "(null)";
                    string worker = __instance?.Props?.workerClass?.Name ?? "(null)";
                    HeadDiag($"跳过基础槽位拦截: nodeType={__instance?.GetType().FullName ?? "null"}, tag={tag}, worker={worker}, pawn={pawn?.LabelShort ?? "null"}");
                }
                return true;
            }

            Vector2 originDrawSize = GetNodeOriginalDrawSize(__instance);
            __result = BuildSlotGraphic(slot, pawn, originDrawSize)!;

            if (EnableHeadBaseSlotDiagnostics && slot.slotType == BaseAppearanceSlotType.Head)
            {
                string tag = __instance?.Props?.tagDef?.defName ?? "(null)";
                string worker = __instance?.Props?.workerClass?.Name ?? "(null)";
                HeadDiag($"命中基础槽位拦截: nodeType={__instance?.GetType().FullName ?? "null"}, tag={tag}, worker={worker}, tex={slot.texPath}, pawn={pawn?.LabelShort ?? "null"}");
            }

            return false;
        }

        private static void UnpatchBaseAppearanceRuntimePatches(Harmony harmony)
        {
            lock (patchStateLock)
            {
                foreach (var method in patchedBaseAppearanceMethods)
                {
                    if (method != null)
                    {
                        harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id);
                    }
                }
                patchedBaseAppearanceMethods.Clear();
            }
        }

        private static void PatchBaseAppearanceGraphicFor(Harmony harmony, Type nodeType)
        {
            var method = AccessTools.DeclaredMethod(nodeType, nameof(PawnRenderNode.GraphicFor), new[] { typeof(Pawn) });
            if (method == null)
            {
                return;
            }

            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Patch_PawnRenderTree), nameof(BaseAppearanceGraphicFor_Prefix)));
            lock (patchStateLock)
            {
                patchedBaseAppearanceMethods.Add(method);
            }
        }

        private static void ApplyBaseAppearanceOverrides(PawnRenderTree tree, Pawn pawn, PawnSkinDef skinDef)
        {
            if (skinDef?.baseAppearance == null) return;

            var nodesByTag = nodesByTagField_Cached?.GetValue(tree) as Dictionary<PawnRenderNodeTagDef, PawnRenderNode>;
            if (nodesByTag == null) return;

            foreach (var slot in skinDef.baseAppearance.EnabledSlots())
            {
                var targetNode = FindNodeForBaseSlot(tree, nodesByTag, slot.slotType);
                if (targetNode == null)
                {
                    if (Prefs.DevMode)
                        Log.Warning($"[CharacterStudio] 无法找到基础槽位 {slot.slotType} 对应的渲染节点");
                    continue;
                }

                try
                {
                    // 读取节点原始 drawSize，保持渲染尺寸不变
                    Vector2 originDrawSize = GetNodeOriginalDrawSize(targetNode);
                    var overrideGraphic = BuildSlotGraphic(slot, pawn, originDrawSize);
                    if (overrideGraphic == null) continue;

                    // 注入 Graphics[] 缓存
                    ReplaceNodeGraphicsCache(targetNode, overrideGraphic);

                    // 应用偏移
                    targetNode.debugOffset = slot.offset;
                    if (slot.offsetEast != Vector3.zero)
                    {
                        RuntimeAssetLoader.RegisterNodeOffsetEast(targetNode.GetHashCode(), slot.offsetEast);
                    }
                    else
                    {
                        RuntimeAssetLoader.RegisterNodeOffsetEast(targetNode.GetHashCode(), Vector3.zero);
                    }
                    if (slot.offsetNorth != Vector3.zero)
                    {
                        RuntimeAssetLoader.RegisterNodeOffsetNorth(targetNode.GetHashCode(), slot.offsetNorth);
                    }
                    else
                    {
                        RuntimeAssetLoader.RegisterNodeOffsetNorth(targetNode.GetHashCode(), Vector3.zero);
                    }

                    if (Prefs.DevMode)
                        Log.Message($"[CharacterStudio] 已覆写基础槽位 {slot.slotType} -> {targetNode.Props?.tagDef?.defName ?? "(无tag)"}, tex={slot.texPath}, drawSize={overrideGraphic.drawSize}");

                    if (EnableHeadBaseSlotDiagnostics && slot.slotType == BaseAppearanceSlotType.Head)
                    {
                        HeadDiag($"ApplyBaseAppearanceOverrides 已处理 Head: nodeType={targetNode.GetType().FullName}, tag={targetNode.Props?.tagDef?.defName ?? "(null)"}, tex={slot.texPath}, drawSize={overrideGraphic.drawSize}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 覆写基础槽位 {slot.slotType} 时出错: {ex.Message}");
                }
            }

            foreach (BaseAppearanceSlotType slotType in Enum.GetValues(typeof(BaseAppearanceSlotType)))
            {
                var slot = skinDef.baseAppearance.GetSlot(slotType);
                var targetNode = FindNodeForBaseSlot(tree, nodesByTag, slotType);
                if (targetNode == null)
                {
                    continue;
                }

                if (slot != null && slot.enabled && !string.IsNullOrWhiteSpace(slot.texPath))
                {
                    // BaseAppearance 现在通过 GraphicFor 前缀稳定接管图形来源，
                    // 其余可直接通过原版节点调试字段稳定叠加。
                    targetNode.debugOffset = slot.offset;
                    targetNode.debugScale = slot.scale.x > 0.001f ? slot.scale.x : 1f;
                    targetNode.debugAngleOffset = slot.rotation;
                    targetNode.debugLayerOffset = slot.drawOrderOffset;
                    targetNode.debugFlip = slot.flipHorizontal;
                    targetNode.requestRecache = false;
                    // 此处只需确保节点不会被隐藏。
                    GetHiddenSet(tree).Remove(targetNode);
                }
                else
                {
                    // 槽位被禁用或清空时，移除附加调试/偏移状态，恢复原版节点行为。
                    targetNode.debugOffset = Vector3.zero;
                    targetNode.requestRecache = false;
                    targetNode.debugScale = 1f;
                    targetNode.debugAngleOffset = 0f;
                    targetNode.debugLayerOffset = 0f;
                    targetNode.debugFlip = false;
                    RuntimeAssetLoader.UnregisterNodeOffsets(targetNode.GetHashCode());

                    ClearNodeGraphicsCacheDirect(targetNode);
                }
            }
        }

        /// <summary>
        /// 读取节点当前 Graphic 的原始 drawSize（覆写前保存）
        /// </summary>
        private static Vector2 GetNodeOriginalDrawSize(PawnRenderNode node)
        {
            try
            {
                var primary = node.PrimaryGraphic;
                if (primary != null && primary.drawSize.x > 0.001f)
                    return primary.drawSize;

                var field = GetGraphicsField();
                if (field != null)
                {
                    var val = field.GetValue(node);
                    if (val is List<Graphic> list && list.Count > 0 && list[0] != null)
                        return list[0].drawSize;
                    if (val is Graphic[] arr && arr.Length > 0 && arr[0] != null)
                        return arr[0].drawSize;
                }

                // 回退到 Props.drawSize
                if (node.Props?.drawSize is Vector2 propsSize && propsSize.x > 0.001f)
                    return propsSize;
            }
            catch { }
            return Vector2.one;
        }

        /// <summary>
        /// 根据槽位配置构建 Graphic 对象
        /// originDrawSize：从目标节点读取的原始 drawSize，用于保持渲染尺寸不变
        /// 绝对路径 -> Graphic_Runtime（不走 ContentFinder）
        /// 游戏内相对路径 -> GraphicDatabase（自动探测 Multi/Single）
        /// </summary>
        private static Graphic? BuildSlotGraphic(BaseAppearanceSlotConfig slot, Pawn? pawn, Vector2 originDrawSize)
        {
            if (string.IsNullOrEmpty(slot.texPath)) return null;

            Shader shader = ShaderDatabase.LoadShader(slot.shaderDefName ?? "Cutout");
            Color resolvedColor    = ResolveSlotColor(slot.colorSource,    slot.customColor,    pawn);
            Color resolvedColorTwo = ResolveSlotColor(slot.colorTwoSource, slot.customColorTwo, pawn);

            // BaseAppearance 的用户缩放走节点 debugScale，而不是 Graphic.drawSize。
            // 否则会把“变换参数”混入“贴图本体尺寸”，在重建/重算时容易出现
            // 头部恢复原纹理、缩放异常或无法再次替换等副作用。
            // 这里始终保持原节点 drawSize 稳定，仅替换贴图来源本身。
            Vector2 drawSize = originDrawSize.x > 0.001f ? originDrawSize : Vector2.one;

            bool isAbsolutePath = System.IO.Path.IsPathRooted(slot.texPath);

            if (isAbsolutePath)
            {
                // 外部文件：直接 new Graphic_Runtime + Init，绕过 GraphicDatabase/ContentFinder
                var req = new GraphicRequest(
                    typeof(Graphic_Runtime), slot.texPath, shader,
                    drawSize, resolvedColor, resolvedColorTwo,
                    null, 0, null, null);
                var gr = new Graphic_Runtime();
                gr.Init(req);
                return gr;
            }
            else
            {
                // 游戏内路径：
                // graphicClass 有明确指定时优先使用；
                // 否则默认 Graphic_Multi（原版 Body/Head/Hair 全部是 Multi），
                // 不在此处调用 ContentFinder（可能在非主线程触发资源加载报错）
                Type graphicType;
                if (slot.graphicClass != null && slot.graphicClass != typeof(Graphic_Runtime))
                    graphicType = slot.graphicClass;
                else
                    graphicType = typeof(Graphic_Multi); // Body/Head/Hair 均为 Multi

                return GraphicDatabase.Get(
                    graphicType, slot.texPath, shader,
                    drawSize, resolvedColor, resolvedColorTwo);
            }
        }

        /// <summary>根据颜色源解析实际颜色</summary>
        private static Color ResolveSlotColor(LayerColorSource source, Color fallback, Pawn? pawn)
        {
            if (pawn?.story == null) return fallback;
            return source switch
            {
                LayerColorSource.PawnHair => pawn.story.HairColor,
                LayerColorSource.PawnSkin => pawn.story.SkinColor,
                LayerColorSource.White    => Color.white,
                _                         => fallback
            };
        }

        private static BaseAppearanceSlotConfig? TryGetBaseAppearanceSlotForNode(PawnRenderNode? node, Pawn? pawn)
        {
            if (node == null || pawn == null)
            {
                return null;
            }

            if (node is PawnRenderNode_Custom)
            {
                return null;
            }

            var skinDef = pawn.GetComp<CompPawnSkin>()?.ActiveSkin;
            if (skinDef?.baseAppearance == null)
            {
                if (EnableHeadBaseSlotDiagnostics && IsHeadLikeNode(node))
                {
                    HeadDiag($"无 ActiveSkin 或 baseAppearance: nodeType={node.GetType().FullName}, pawn={pawn.LabelShort}");
                }
                return null;
            }

            if (EnableHeadBaseSlotDiagnostics && IsHeadLikeNode(node))
            {
                HeadDiag($"检查基础槽位: nodeType={node.GetType().FullName}, faceEnabled={skinDef.faceConfig?.enabled == true}, hideHead={skinDef.hideVanillaHead}, texHead={skinDef.baseAppearance.GetSlot(BaseAppearanceSlotType.Head)?.texPath ?? ""}");
            }

            BaseAppearanceSlotType? slotType = node switch
            {
                PawnRenderNode_Body => BaseAppearanceSlotType.Body,
                PawnRenderNode_Head => BaseAppearanceSlotType.Head,
                PawnRenderNode_Hair => BaseAppearanceSlotType.Hair,
                PawnRenderNode_Beard => BaseAppearanceSlotType.Beard,
                _ => null
            };

            if (slotType == null)
            {
                string tag = node.Props?.tagDef?.defName ?? string.Empty;
                string worker = node.Props?.workerClass?.Name ?? string.Empty;
                string texPath = node.Props?.texPath ?? string.Empty;

                if (ContainsAny(tag, worker, texPath, "body"))
                {
                    slotType = BaseAppearanceSlotType.Body;
                }
                else if (ContainsAny(tag, worker, texPath, "head"))
                {
                    slotType = BaseAppearanceSlotType.Head;
                }
                else if (ContainsAny(tag, worker, texPath, "hair"))
                {
                    slotType = BaseAppearanceSlotType.Hair;
                }
                else if (ContainsAny(tag, worker, texPath, "beard"))
                {
                    slotType = BaseAppearanceSlotType.Beard;
                }
            }

            if (slotType == null)
            {
                return null;
            }

            var slot = skinDef.baseAppearance.GetSlot(slotType.Value);
            if (slot == null || !slot.enabled || string.IsNullOrWhiteSpace(slot.texPath))
            {
                return null;
            }

            return slot;
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

        private static bool IsHeadLikeNode(PawnRenderNode? node)
        {
            if (node == null)
                return false;

            string typeName = node.GetType().FullName ?? string.Empty;
            string tag = node.Props?.tagDef?.defName ?? string.Empty;
            string worker = node.Props?.workerClass?.Name ?? string.Empty;
            string texPath = node.Props?.texPath ?? string.Empty;

            return ContainsAny(typeName, tag, worker, "head")
                || ContainsAny(tag, worker, texPath, "head");
        }
    }
}
