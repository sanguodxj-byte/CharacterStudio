using System;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Rendering
{
    public static partial class Patch_PawnRenderTree
    {
        // ─────────────────────────────────────────────
        // BaseAppearance 运行时辅助
        // 当前策略：
        //   - 运行时显示走 BaseAppearanceUtility.BuildSyntheticLayers(skinDef)
        //   - 注入为 PawnRenderNode_Custom
        //   - 原版 Body/Head/Hair/Beard 节点通过隐藏逻辑退场
        // 不再直接补丁/改写原版节点的 GraphicFor/graphics cache，
        // 以避免与原版重建、表情系统、实际 Pawn 状态刷新互相打架。
        // ─────────────────────────────────────────────

        private static bool HasEnabledBaseSlot(PawnSkinDef skinDef, BaseAppearanceSlotType slotType)
        {
            if (skinDef?.baseAppearance == null) return false;
            var slot = skinDef.baseAppearance.GetSlot(slotType);
            return slot != null && slot.enabled && !string.IsNullOrWhiteSpace(slot.texPath);
        }

        private static void ApplyBaseAppearanceOverrides(PawnRenderTree tree, Pawn pawn, PawnSkinDef skinDef)
        {
            // 兼容保留：旧调用点已迁移到 synthetic-layer 注入。
            // 这里保持 no-op，避免历史代码引用时报错。
        }

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
                    if (val is System.Collections.Generic.List<Graphic> list && list.Count > 0 && list[0] != null)
                        return list[0].drawSize;
                    if (val is Graphic[] arr && arr.Length > 0 && arr[0] != null)
                        return arr[0].drawSize;
                }

                if (node.Props?.drawSize is Vector2 propsSize && propsSize.x > 0.001f)
                    return propsSize;
            }
            catch
            {
            }

            return Vector2.one;
        }

        private static Graphic? BuildSlotGraphic(BaseAppearanceSlotConfig slot, Pawn? pawn, Vector2 originDrawSize)
        {
            if (string.IsNullOrEmpty(slot.texPath)) return null;

            Shader shader = ShaderDatabase.LoadShader(slot.shaderDefName ?? "Cutout");
            Color resolvedColor    = ResolveSlotColor(slot.colorSource,    slot.customColor,    pawn);
            Color resolvedColorTwo = ResolveSlotColor(slot.colorTwoSource, slot.customColorTwo, pawn);
            Vector2 drawSize = originDrawSize.x > 0.001f ? originDrawSize : Vector2.one;

            bool isAbsolutePath = System.IO.Path.IsPathRooted(slot.texPath);
            if (isAbsolutePath)
            {
                var req = new GraphicRequest(
                    typeof(Graphic_Runtime), slot.texPath, shader,
                    drawSize, resolvedColor, resolvedColorTwo,
                    null, 0, null, null);
                var gr = new Graphic_Runtime();
                gr.Init(req);
                return gr;
            }

            Type graphicType = (slot.graphicClass != null && slot.graphicClass != typeof(Graphic_Runtime))
                ? slot.graphicClass
                : typeof(Graphic_Multi);

            return GraphicDatabase.Get(
                graphicType, slot.texPath, shader,
                drawSize, resolvedColor, resolvedColorTwo);
        }

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

        private static void ApplyBaseAppearanceRuntimePatches(HarmonyLib.Harmony harmony)
        {
            // synthetic-layer 方案下不再需要运行时 patch 原版节点 GraphicFor。
        }

        private static void UnpatchBaseAppearanceRuntimePatches(HarmonyLib.Harmony harmony)
        {
            // synthetic-layer 方案下不再需要运行时 patch 原版节点 GraphicFor。
        }
    }
}
