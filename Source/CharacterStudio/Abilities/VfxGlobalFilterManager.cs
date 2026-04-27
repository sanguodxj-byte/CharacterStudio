// ─────────────────────────────────────────────
// 全局滤镜管理器
// 管理技能 VFX 的全屏滤镜效果（灰度、着色、怀旧等）
// 混合方案：优先通过 GUI Overlay 渲染颜色效果，饱和度效果仍尝试通过 CameraColor
// ─────────────────────────────────────────────

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 全局滤镜效果管理器。
    /// 管理 VFX 配置中的 globalFilterMode 字段驱动的全屏后处理效果。
    /// 在 1.6 中使用 GUI Overlay 确保效果 100% 可见。
    /// </summary>
    internal static class VfxGlobalFilterManager
    {
        // ── 滤镜模式常量 ──
        public const string ModeGrayscale = "Grayscale";
        public const string ModeSepia = "Sepia";
        public const string ModeTint = "Tint";
        public const string ModeNegative = "Negative";
        public const string ModeDesaturate = "Desaturate";
        public const string ModeBlackAndWhite = "BlackAndWhite";

        // ── 活跃滤镜实例 ──
        private static readonly List<ActiveGlobalFilter> activeFilters = new List<ActiveGlobalFilter>();

        // ── 反射缓存（保留作为辅助） ──
        private static readonly System.Reflection.PropertyInfo? FindCameraColorProperty =
            AccessTools.Property(typeof(Find), "CameraColor");
        private static readonly Dictionary<Type, System.Reflection.FieldInfo?> SaturationFieldCache =
            new Dictionary<Type, System.Reflection.FieldInfo?>();

        private static float originalSaturation = 1f;
        private static bool originalValuesCaptured;
        private static int lastProcessedTick = -1;

        // ── 当前帧应用的状态 ──
        private static Color currentOverlayColor = Color.clear;
        private static float currentSaturationTarget = 1f;
        private static bool isAnyFilterActive = false;

        public static bool IsValidFilterMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return false;
            return mode == ModeGrayscale
                || mode == ModeSepia
                || mode == ModeTint
                || mode == ModeNegative
                || mode == ModeDesaturate
                || mode == ModeBlackAndWhite;
        }

        public static void Activate(AbilityVisualEffectConfig vfx, Pawn caster)
        {
            if (!IsValidFilterMode(vfx.globalFilterMode)) return;
            if (caster?.Map == null) return;

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int transitionTicks = Math.Max(1, vfx.globalFilterTransitionTicks);
            int durationTicks = Math.Max(1, vfx.displayDurationTicks);

            var filter = new ActiveGlobalFilter
            {
                mode = vfx.globalFilterMode,
                tintColor = vfx.shaderTintColor,
                intensity = vfx.shaderIntensity,
                startTick = nowTick,
                transitionInEndTick = nowTick + transitionTicks,
                holdEndTick = nowTick + transitionTicks + durationTicks,
                transitionOutEndTick = nowTick + transitionTicks + durationTicks + transitionTicks,
                casterThingId = caster.thingIDNumber,
                mapId = caster.Map.uniqueID
            };

            activeFilters.Add(filter);
        }

        /// <summary>
        /// 每帧逻辑更新 - 由 PawnSkinBootstrapComponent.GameComponentTick 调用。
        /// </summary>
        public static void Tick()
        {
            if (activeFilters.Count == 0)
            {
                if (isAnyFilterActive) ClearAndRestore();
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? -1;
            if (nowTick < 0) return;

            if (lastProcessedTick == nowTick) return;
            lastProcessedTick = nowTick;

            activeFilters.RemoveAll(f => f == null || nowTick > f.transitionOutEndTick);

            if (activeFilters.Count == 0)
            {
                ClearAndRestore();
                return;
            }

            ActiveGlobalFilter? best = ResolveBestFilterForCurrentMap(nowTick);
            if (best == null)
            {
                ClearAndRestore();
                return;
            }

            UpdateFilterState(best, nowTick);
        }

        /// <summary>
        /// 每帧渲染 - 需要在 UIRoot.OnGUI 或类似位置调用以显示滤镜。
        /// </summary>
        public static void OnGUI()
        {
            if (!isAnyFilterActive) return;

            // 绘制全屏遮罩（如果有颜色叠加）
            if (currentOverlayColor.a > 0.001f)
            {
                GUI.color = currentOverlayColor;
                Widgets.DrawAtlas(new Rect(0, 0, (float)Verse.UI.screenWidth, (float)Verse.UI.screenHeight), BaseContent.WhiteTex);
                GUI.color = Color.white;
            }
        }

        private static void UpdateFilterState(ActiveGlobalFilter filter, int nowTick)
        {
            float weight = ComputeWeight(filter, nowTick);
            if (weight < 0.001f)
            {
                ClearAndRestore();
                return;
            }

            isAnyFilterActive = true;
            float effectiveIntensity = filter.intensity * weight;

            switch (filter.mode)
            {
                case ModeGrayscale:
                    currentSaturationTarget = Mathf.Lerp(1f, 0f, weight);
                    // 添加轻微的灰色叠加以确保效果可见
                    currentOverlayColor = new Color(0.5f, 0.5f, 0.5f, 0.15f * weight);
                    break;
                case ModeDesaturate:
                    currentSaturationTarget = Mathf.Lerp(1f, 1f - filter.intensity, weight);
                    // 添加轻微的灰色叠加
                    currentOverlayColor = new Color(0.5f, 0.5f, 0.5f, 0.1f * effectiveIntensity);
                    break;
                case ModeSepia:
                    currentSaturationTarget = Mathf.Lerp(1f, 0.2f, weight);
                    currentOverlayColor = new Color(0.35f, 0.2f, 0.05f, 0.25f * effectiveIntensity);
                    break;
                case ModeTint:
                    currentSaturationTarget = Mathf.Lerp(1f, 0.7f, weight);
                    currentOverlayColor = new Color(filter.tintColor.r, filter.tintColor.g, filter.tintColor.b, 0.35f * effectiveIntensity);
                    break;
                case ModeNegative:
                    currentSaturationTarget = Mathf.Lerp(1f, 0.05f, weight);
                    currentOverlayColor = new Color(1, 1, 1, 0.5f * effectiveIntensity); // 模拟强曝光反色
                    break;
                case ModeBlackAndWhite:
                    currentSaturationTarget = Mathf.Lerp(1f, 0f, weight);
                    // 纯黑白：饱和度完全归零 + 深色半透明覆盖增强对比度
                    currentOverlayColor = new Color(0.08f, 0.08f, 0.08f, 0.35f * weight);
                    break;
            }

            // 尝试应用原版饱和度修改（如果可用）
            ApplySaturationToCamera(currentSaturationTarget);
        }

        private static void ClearAndRestore()
        {
            isAnyFilterActive = false;
            currentOverlayColor = Color.clear;
            currentSaturationTarget = 1f;
            ApplySaturationToCamera(1f);
        }

        private static void ApplySaturationToCamera(float targetSat)
        {
            try
            {
                object? camColor = FindCameraColorProperty?.GetValue(null, null);
                if (camColor == null) return;

                System.Reflection.FieldInfo? satField = GetSaturationField(camColor.GetType());
                if (satField == null) return;

                if (!originalValuesCaptured)
                {
                    originalSaturation = (float)(satField.GetValue(camColor) ?? 1f);
                    originalValuesCaptured = true;
                }

                satField.SetValue(camColor, Mathf.Lerp(originalSaturation, targetSat, 1f));
                
                // 强制更新参数
                System.Reflection.MethodInfo? updateMethod = AccessTools.Method(camColor.GetType(), "UpdateParameters");
                updateMethod?.Invoke(camColor, null);
                
                Behaviour? b = camColor as Behaviour;
                if (b != null && !b.enabled && targetSat < 0.99f) b.enabled = true;
            }
            catch { /* 忽略 1.6 可能的反射失败 */ }
        }

        private static ActiveGlobalFilter? ResolveBestFilterForCurrentMap(int nowTick)
        {
            Map? currentMap = Find.CurrentMap;
            if (currentMap == null) return null;

            ActiveGlobalFilter? best = null;
            float maxW = -1f;

            for (int i = 0; i < activeFilters.Count; i++)
            {
                if (activeFilters[i].mapId != currentMap.uniqueID) continue;
                float w = ComputeWeight(activeFilters[i], nowTick);
                if (w > maxW) { maxW = w; best = activeFilters[i]; }
            }
            return best;
        }

        private static float ComputeWeight(ActiveGlobalFilter filter, int nowTick)
        {
            if (nowTick < filter.startTick) return 0f;
            if (nowTick <= filter.transitionInEndTick) return Mathf.Clamp01((float)(nowTick - filter.startTick) / Math.Max(1, filter.transitionInEndTick - filter.startTick));
            if (nowTick <= filter.holdEndTick) return 1f;
            if (nowTick <= filter.transitionOutEndTick) return Mathf.Clamp01(1f - (float)(nowTick - filter.holdEndTick) / Math.Max(1, filter.transitionOutEndTick - filter.holdEndTick));
            return 0f;
        }

        private static System.Reflection.FieldInfo? GetSaturationField(Type type)
        {
            if (SaturationFieldCache.TryGetValue(type, out var f)) return f;
            return SaturationFieldCache[type] = AccessTools.Field(type, "saturation");
        }

        public static void ClearAll() { activeFilters.Clear(); ClearAndRestore(); }

        private class ActiveGlobalFilter
        {
            public string mode = "";
            public Color tintColor = Color.white;
            public float intensity = 1f;
            public int startTick, transitionInEndTick, holdEndTick, transitionOutEndTick;
            public int casterThingId, mapId;
        }
    }
}
