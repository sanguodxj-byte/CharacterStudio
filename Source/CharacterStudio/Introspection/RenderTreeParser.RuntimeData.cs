using System;
using Verse;
using RimWorld;
using UnityEngine;
using HarmonyLib;

namespace CharacterStudio.Introspection
{
    public static partial class RenderTreeParser
    {
        // ─────────────────────────────────────────────
        // 运行时变换捕获
        // 负责捕获节点的偏移、缩放、旋转及 HAR colorChannel
        // ─────────────────────────────────────────────

        /// <summary>
        /// 捕获节点的运行时变换数据（偏移/缩放/旋转/绘制层级）
        /// 优先使用 Worker API，回退到 props 字段，再回退到 GetTransform
        /// </summary>
        private static void CaptureRuntimeTransform(
            PawnRenderNode gameNode,
            Pawn pawn,
            PawnRenderNodeProperties? props,
            string label,
            string tagDef,
            bool shouldDebug,
            out Vector3 nodeOffset,
            out float   nodeScale,
            out float   nodeDrawOrder,
            out Vector3 runtimeOffset,
            out Vector3 runtimeOffsetEast,
            out Vector3 runtimeOffsetNorth,
            out Vector3 runtimeScale,
            out float   runtimeRotation,
            out bool    runtimeDataValid)
        {
            nodeOffset        = Vector3.zero;
            nodeScale         = 1f;
            nodeDrawOrder     = props?.baseLayer ?? 0f;
            runtimeOffset     = Vector3.zero;
            runtimeOffsetEast = Vector3.zero;
            runtimeOffsetNorth= Vector3.zero;
            runtimeScale      = Vector3.one;
            runtimeRotation   = 0f;
            runtimeDataValid  = false;

            // 先读取 props 中的静态偏移作为回退基础值
            ReadPropsOffsets(props, out Vector3 propsOffset, out Vector3 propsOffsetEast, out Vector3 propsOffsetNorth);

            if (shouldDebug)
                EmitHarDebugPropsOffsets(label, tagDef, props, propsOffset, propsOffsetEast, propsOffsetNorth);

            try
            {
                if (!IsMainThread())
                {
                    // 非主线程：只使用 props 静态值
                    nodeOffset        = propsOffset;
                    runtimeOffset     = propsOffset;
                    runtimeOffsetEast = propsOffsetEast;
                    runtimeOffsetNorth= propsOffsetNorth;
                    nodeDrawOrder     = props?.baseLayer ?? 0f;
                    runtimeScale      = Vector3.one;
                    runtimeDataValid  = false;
                    return;
                }

                var worker = gameNode.Worker;
                if (worker != null)
                    CaptureViaWorker(gameNode, pawn, worker, props,
                        propsOffset, propsOffsetEast, propsOffsetNorth,
                        shouldDebug,
                        out nodeOffset, out nodeScale, out nodeDrawOrder,
                        out runtimeOffset, out runtimeOffsetEast, out runtimeOffsetNorth,
                        out runtimeRotation, out runtimeDataValid);
                else
                    CaptureViaGetTransform(gameNode, pawn, props,
                        propsOffset, propsOffsetEast, propsOffsetNorth,
                        out nodeOffset, out nodeScale, out nodeDrawOrder,
                        out runtimeOffset, out runtimeOffsetEast, out runtimeOffsetNorth,
                        out runtimeRotation, out runtimeDataValid);
            }
            catch
            {
                // 回退到 debug 值
                nodeOffset       = gameNode.debugOffset;
                nodeScale        = gameNode.debugScale;
                nodeDrawOrder    = props?.baseLayer ?? 0f;
                runtimeDataValid = false;
            }
        }

        private static void CaptureViaWorker(
            PawnRenderNode gameNode, Pawn pawn, PawnRenderNodeWorker worker,
            PawnRenderNodeProperties? props,
            Vector3 propsOffset, Vector3 propsOffsetEast, Vector3 propsOffsetNorth,
            bool shouldDebug,
            out Vector3 nodeOffset, out float nodeScale, out float nodeDrawOrder,
            out Vector3 runtimeOffset, out Vector3 runtimeOffsetEast, out Vector3 runtimeOffsetNorth,
            out float runtimeRotation, out bool runtimeDataValid)
        {
            nodeOffset        = Vector3.zero;
            nodeScale         = 1f;
            nodeDrawOrder     = 0f;
            runtimeOffset     = Vector3.zero;
            runtimeOffsetEast = Vector3.zero;
            runtimeOffsetNorth= Vector3.zero;
            runtimeRotation   = 0f;
            runtimeDataValid  = false;

            var southParms = MakeParms(pawn, Rot4.South);
            var eastParms  = MakeParms(pawn, Rot4.East);
            var northParms = MakeParms(pawn, Rot4.North);

            Vector3 southOffset = worker.OffsetFor(gameNode, southParms, out _);
            if (shouldDebug) EmitHarDebugWorker(worker, southOffset);

            if (southOffset == Vector3.zero && propsOffset != Vector3.zero)
                southOffset = propsOffset;

            if (southOffset != Vector3.zero)
            {
                nodeOffset    = southOffset;
                runtimeOffset = southOffset;
            }

            Vector3 eastOffset = worker.OffsetFor(gameNode, eastParms, out _);
            Vector3 eastDelta  = eastOffset - southOffset;
            if (eastDelta == Vector3.zero && propsOffsetEast != Vector3.zero)
                eastDelta = propsOffsetEast;
            if (eastDelta != Vector3.zero)
                runtimeOffsetEast = eastDelta;

            Vector3 northOffset = worker.OffsetFor(gameNode, northParms, out _);
            Vector3 northDelta  = northOffset - southOffset;
            if (northDelta == Vector3.zero && propsOffsetNorth != Vector3.zero)
                northDelta = propsOffsetNorth;
            if (northDelta != Vector3.zero)
                runtimeOffsetNorth = northDelta;

            Vector3 workerScale = worker.ScaleFor(gameNode, southParms);
            float   avgScale    = (workerScale.x + workerScale.z) / 2f;
            if (avgScale != 1f) nodeScale = avgScale;

            runtimeRotation = worker.RotationFor(gameNode, southParms).eulerAngles.y;
            nodeDrawOrder   = worker.LayerFor(gameNode, southParms);
            runtimeDataValid= true;
        }

        private static void CaptureViaGetTransform(
            PawnRenderNode gameNode, Pawn pawn,
            PawnRenderNodeProperties? props,
            Vector3 propsOffset, Vector3 propsOffsetEast, Vector3 propsOffsetNorth,
            out Vector3 nodeOffset, out float nodeScale, out float nodeDrawOrder,
            out Vector3 runtimeOffset, out Vector3 runtimeOffsetEast, out Vector3 runtimeOffsetNorth,
            out float runtimeRotation, out bool runtimeDataValid)
        {
            nodeOffset        = Vector3.zero;
            nodeScale         = 1f;
            nodeDrawOrder     = props?.baseLayer ?? 0f;
            runtimeOffset     = Vector3.zero;
            runtimeOffsetEast = Vector3.zero;
            runtimeOffsetNorth= Vector3.zero;
            runtimeRotation   = 0f;
            runtimeDataValid  = false;

            var southParms = MakeParms(pawn, Rot4.South);
            gameNode.GetTransform(southParms, out Vector3 transformOffset, out _, out Quaternion rotation, out Vector3 transformScale);

            if (transformOffset == Vector3.zero && propsOffset != Vector3.zero)
                transformOffset = propsOffset;

            if (transformOffset != Vector3.zero)
            {
                nodeOffset    = transformOffset;
                runtimeOffset = transformOffset;
            }

            if (propsOffsetEast  != Vector3.zero) runtimeOffsetEast  = propsOffsetEast;
            if (propsOffsetNorth != Vector3.zero) runtimeOffsetNorth = propsOffsetNorth;

            float avgScale = (transformScale.x + transformScale.z) / 2f;
            if (avgScale != 1f) nodeScale = avgScale;

            runtimeRotation  = rotation.eulerAngles.y;
            runtimeDataValid = true;
        }

        // ─────────────────────────────────────────────
        // HAR colorChannel 捕获
        // ─────────────────────────────────────────────

        private static string CaptureHarColorChannel(PawnRenderNode gameNode, PawnRenderNodeProperties? props)
        {
            try
            {
                // 1. 从 BodyAddon 获取（HAR 标准方式）
                var addonField = AccessTools.Field(gameNode.GetType(), "addon");
                if (addonField != null)
                {
                    object? addon = addonField.GetValue(gameNode);
                    if (addon != null)
                    {
                        var channelField = AccessTools.Field(addon.GetType(), "colorChannel");
                        if (channelField != null)
                        {
                            object? val = channelField.GetValue(addon);
                            if (val != null) return val.ToString() ?? "";
                        }
                    }
                }

                // 2. 从 Props 直接获取（某些变体）
                if (props != null)
                {
                    var propsChannelField = AccessTools.Field(props.GetType(), "colorChannel");
                    if (propsChannelField != null)
                    {
                        object? val = propsChannelField.GetValue(props);
                        if (val != null) return val.ToString() ?? "";
                    }
                }
            }
            catch { }
            return "";
        }

        // ─────────────────────────────────────────────
        // Props 偏移读取辅助
        // ─────────────────────────────────────────────

        private static void ReadPropsOffsets(
            PawnRenderNodeProperties? props,
            out Vector3 offset,
            out Vector3 offsetEast,
            out Vector3 offsetNorth)
        {
            offset      = Vector3.zero;
            offsetEast  = Vector3.zero;
            offsetNorth = Vector3.zero;

            if (props == null) return;
            try
            {
                offset      = ReadVector3Field(props, "offset");
                offsetEast  = ReadVector3Field(props, "offsetEast");
                offsetNorth = ReadVector3Field(props, "offsetNorth");
            }
            catch { }
        }

        private static Vector3 ReadVector3Field(object target, string fieldName)
        {
            var field = AccessTools.Field(target.GetType(), fieldName);
            if (field != null) return (Vector3)field.GetValue(target);
            var prop = AccessTools.Property(target.GetType(), fieldName);
            if (prop != null) return (Vector3)prop.GetValue(target);
            return Vector3.zero;
        }

        private static PawnDrawParms MakeParms(Pawn pawn, Rot4 facing)
            => new PawnDrawParms { pawn = pawn, facing = facing };
    }
}
