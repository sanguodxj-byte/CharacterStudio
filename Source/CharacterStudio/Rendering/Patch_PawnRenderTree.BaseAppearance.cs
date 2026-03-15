using System;
using System.Collections.Generic;
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
                    var field = GetGraphicsField();
                    if (field != null)
                    {
                        var val = field.GetValue(targetNode);
                        if (val is List<Graphic> list)
                        {
                            list.Clear();
                            list.Add(overrideGraphic);
                        }
                        else
                        {
                            field.SetValue(targetNode, new Graphic[] { overrideGraphic });
                        }
                        primaryGraphicField?.SetValue(targetNode, overrideGraphic);
                    }

                    // 应用偏移
                    targetNode.debugOffset = slot.offset;
                    if (slot.offsetEast != Vector3.zero)
                        RuntimeAssetLoader.RegisterNodeOffsetEast(targetNode.GetHashCode(), slot.offsetEast);
                    if (slot.offsetNorth != Vector3.zero)
                        RuntimeAssetLoader.RegisterNodeOffsetNorth(targetNode.GetHashCode(), slot.offsetNorth);

                    // 从隐藏名单释放（避免被 ProcessVanillaHiding 误隐藏）
                    GetHiddenSet(tree).Remove(targetNode);

                    if (Prefs.DevMode)
                        Log.Message($"[CharacterStudio] 已覆写基础槽位 {slot.slotType} -> {targetNode.Props?.tagDef?.defName ?? "(无tag)"}, tex={slot.texPath}, drawSize={overrideGraphic.drawSize}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 覆写基础槽位 {slot.slotType} 时出错: {ex.Message}");
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

            // drawSize 优先级：
            // 1. slot.scale 被用户明确修改（不等于 (1,1)）→ 使用用户设置的缩放
            // 2. 否则使用原节点的 drawSize，保持原版渲染尺寸不变（避免头部变大/脱离身体）
            Vector2 drawSize = (slot.scale.x > 0.001f && slot.scale != Vector2.one)
                ? slot.scale
                : (originDrawSize.x > 0.001f ? originDrawSize : Vector2.one);

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
    }
}
