using System;
using System.Reflection;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace CharacterStudio.Introspection
{
    public static partial class RenderTreeParser
    {
        // ─────────────────────────────────────────────
        // HAR 调试日志
        // 所有调试输出仅在 Prefs.DevMode 下触发
        // ─────────────────────────────────────────────

        private static void DebugLog(string message)
        {
            if (IsVerboseDebugEnabled)
                Log.Message(message);
        }

        private static bool ShouldInspectHarDebugNode(string label, string tagDef)
            => ContainsHarDebugKeyword(tagDef) || ContainsHarDebugKeyword(label);

        private static bool ContainsHarDebugKeyword(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("ear",  StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("tail", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>输出节点自身字段及属性的调试信息</summary>
        private static void EmitHarDebugInfo(
            PawnRenderNode gameNode,
            string label,
            string tagDef,
            PawnRenderNodeProperties? props,
            Color nodeColor)
        {
            string nodeTypeName = gameNode.GetType().Name;
            Log.Message($"[CS.HAR.Debug] ====== 节点 '{label}' ({nodeTypeName}) ======");
            Log.Message($"[CS.HAR.Debug] 节点类型完整名: {gameNode.GetType().FullName}");

            // 节点字段
            var allNodeFields = gameNode.GetType().GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Log.Message($"[CS.HAR.Debug] 节点字段数量: {allNodeFields.Length}");
            foreach (var field in allNodeFields)
            {
                try
                {
                    string fn = field.Name.ToLower();
                    if (fn.Contains("offset") || fn.Contains("color") || fn.Contains("scale")
                        || fn.Contains("position") || fn.Contains("loc") || fn.Contains("pos")
                        || fn.Contains("size") || fn.Contains("draw") || fn.Contains("mesh")
                        || fn.Contains("parent") || fn.Contains("anchor"))
                    {
                        Log.Message($"[CS.HAR.Debug]   节点.{field.Name} ({field.FieldType.Name}): {field.GetValue(gameNode)}");
                    }
                }
                catch { }
            }

            // 父节点
            try
            {
                var parentField = AccessTools.Field(typeof(PawnRenderNode), "parent");
                if (parentField != null)
                {
                    var parentNode = parentField.GetValue(gameNode) as PawnRenderNode;
                    if (parentNode != null)
                    {
                        Log.Message($"[CS.HAR.Debug]   父节点类型: {parentNode.GetType().Name}");
                        Log.Message($"[CS.HAR.Debug]   父节点label: {parentNode}");
                        Log.Message($"[CS.HAR.Debug]   父节点debugScale: {parentNode.debugScale}");
                        Log.Message($"[CS.HAR.Debug]   父节点debugOffset: {parentNode.debugOffset}");
                    }
                }
            }
            catch { }

            // 节点属性
            var allProps = gameNode.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var p in allProps)
            {
                try
                {
                    if (!p.CanRead) continue;
                    string pn = p.Name.ToLower();
                    if (pn.Contains("offset") || pn.Contains("color") || pn.Contains("scale")
                        || pn.Contains("position") || pn.Contains("loc") || pn.Contains("pos")
                        || pn.Contains("size") || pn.Contains("draw"))
                    {
                        Log.Message($"[CS.HAR.Debug]   节点属性.{p.Name} ({p.PropertyType.Name}): {p.GetValue(gameNode)}");
                    }
                }
                catch { }
            }
        }

        /// <summary>输出 props 偏移的调试信息</summary>
        private static void EmitHarDebugPropsOffsets(
            string label, string tagDef,
            PawnRenderNodeProperties? props,
            Vector3 propsOffset, Vector3 propsOffsetEast, Vector3 propsOffsetNorth)
        {
            Log.Message($"[CS.HAR.Debug] 节点 '{label}' Tag={tagDef}");
            Log.Message($"[CS.HAR.Debug]   - props类型: {props?.GetType().FullName ?? "null"}");
            Log.Message($"[CS.HAR.Debug]   - props.offset: {propsOffset}");
            Log.Message($"[CS.HAR.Debug]   - props.offsetEast: {propsOffsetEast}");
            Log.Message($"[CS.HAR.Debug]   - props.offsetNorth: {propsOffsetNorth}");

            if (props == null) return;
            var propsType = props.GetType();
            foreach (var field in propsType.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try
                {
                    string fn = field.Name.ToLower();
                    if (fn.Contains("offset") || fn.Contains("color") || fn.Contains("scale")
                        || fn.Contains("size") || fn.Contains("draw"))
                    {
                        Log.Message($"[CS.HAR.Debug]   - props.{field.Name}: {field.GetValue(props)}");
                    }
                }
                catch { }
            }
        }

        /// <summary>输出 Worker 返回的偏移调试信息</summary>
        private static void EmitHarDebugWorker(PawnRenderNodeWorker worker, Vector3 southOffset)
        {
            Log.Message($"[CS.HAR.Debug]   - Worker类型: {worker.GetType().FullName}");
            Log.Message($"[CS.HAR.Debug]   - Worker.OffsetFor(South): {southOffset}");
        }

        /// <summary>输出最终捕获数据的调试信息</summary>
        private static void EmitHarDebugFinal(Vector3 offset, Vector3 scale, Color color)
        {
            Log.Message($"[CS.HAR.Debug]   - 最终 runtimeOffset: {offset}");
            Log.Message($"[CS.HAR.Debug]   - 最终 runtimeScale: {scale}");
            Log.Message($"[CS.HAR.Debug]   - 最终 runtimeColor: {color}");
        }
    }
}
