// ─────────────────────────────────────────────
// 全局滤镜管理器
// 管理技能 VFX 的全屏滤镜效果（灰度、着色、怀旧等）
// 通过 Find.CameraColor 控制 saturation/color/temperature
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
    /// 利用 RimWorld 的 CameraColor 组件控制 saturation/color temperature 等参数。
    /// </summary>
    internal static class VfxGlobalFilterManager
    {
        // ── 滤镜模式常量 ──
        public const string ModeGrayscale = "Grayscale";
        public const string ModeSepia = "Sepia";
        public const string ModeTint = "Tint";
        public const string ModeNegative = "Negative";
        public const string ModeDesaturate = "Desaturate";

        // ── 活跃滤镜实例 ──
        private static readonly List<ActiveGlobalFilter> activeFilters = new List<ActiveGlobalFilter>();

        // ── 反射缓存 ──
        private static readonly System.Reflection.PropertyInfo? FindCameraColorProperty =
            AccessTools.Property(typeof(Find), "CameraColor");
        private static readonly System.Reflection.PropertyInfo? FindCameraProperty =
            AccessTools.Property(typeof(Find), "Camera");
        private static readonly Dictionary<Type, System.Reflection.FieldInfo?> SaturationFieldCache =
            new Dictionary<Type, System.Reflection.FieldInfo?>();

        // ── 原始值缓存（只在第一个滤镜激活时保存） ──
        private static float originalSaturation = 1f;
        private static Color originalColor = Color.white;
        private static bool originalEnabled;
        private static bool originalValuesCaptured;
        private static bool createdCameraColorForFilter;

        private static int lastProcessedTick = -1;

        /// <summary>
        /// 判断给定模式字符串是否为有效的全局滤镜。
        /// </summary>
        public static bool IsValidFilterMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return false;
            return mode == ModeGrayscale
                || mode == ModeSepia
                || mode == ModeTint
                || mode == ModeNegative
                || mode == ModeDesaturate;
        }

        /// <summary>
        /// 激活一个全局滤镜。由 AbilityVfxPlayer 在触发带 globalFilterMode 的 VFX 时调用。
        /// </summary>
        public static void Activate(AbilityVisualEffectConfig vfx, Pawn caster)
        {
            if (!IsValidFilterMode(vfx.globalFilterMode)) return;
            if (caster?.Map == null) return;

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int transitionTicks = Math.Max(1, vfx.globalFilterTransitionTicks);
            int durationTicks = Math.Max(1, vfx.displayDurationTicks);

            activeFilters.Add(new ActiveGlobalFilter
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
            });
        }

        /// <summary>
        /// 每帧更新——由 CompAbilityEffect_Modular.CompTick 调用。
        /// </summary>
        public static void Tick()
        {
            if (activeFilters.Count == 0) return;

            int nowTick = Find.TickManager?.TicksGame ?? -1;
            if (nowTick < 0) return;

            // 去重：每 tick 只处理一次
            if (lastProcessedTick == nowTick) return;
            lastProcessedTick = nowTick;

            // 清理过期滤镜
            activeFilters.RemoveAll(f => f == null || nowTick > f.transitionOutEndTick);

            if (activeFilters.Count == 0)
            {
                RestoreOriginalValues();
                return;
            }

            // 计算当前地图上最优先的滤镜
            ActiveGlobalFilter? best = ResolveBestFilterForCurrentMap(nowTick);
            if (best == null)
            {
                RestoreOriginalValues();
                return;
            }

            // 计算过渡进度
            float weight = ComputeWeight(best, nowTick);
            if (weight < 0.001f)
            {
                RestoreOriginalValues();
                return;
            }

            ApplyFilter(best, weight, nowTick);
        }

        /// <summary>
        /// 清除所有活跃滤镜（地图切换/存档载入时）。
        /// </summary>
        public static void ClearAll()
        {
            activeFilters.Clear();
            RestoreOriginalValues();
            lastProcessedTick = -1;
        }

        // ─────────────────────────────────────────────
        // 内部实现
        // ─────────────────────────────────────────────

        private static ActiveGlobalFilter? ResolveBestFilterForCurrentMap(int nowTick)
        {
            Map? currentMap = Find.CurrentMap;
            if (currentMap == null) return null;

            int mapId = currentMap.uniqueID;
            ActiveGlobalFilter? best = null;
            float bestWeight = 0f;

            for (int i = 0; i < activeFilters.Count; i++)
            {
                ActiveGlobalFilter f = activeFilters[i];
                if (f.mapId != mapId) continue;

                float w = ComputeWeight(f, nowTick);
                if (w > bestWeight)
                {
                    bestWeight = w;
                    best = f;
                }
            }

            return best;
        }

        private static float ComputeWeight(ActiveGlobalFilter filter, int nowTick)
        {
            if (nowTick < filter.startTick)
                return 0f;

            // Transition in
            if (nowTick <= filter.transitionInEndTick)
            {
                int range = filter.transitionInEndTick - filter.startTick;
                if (range <= 0) return 1f;
                return Mathf.Clamp01((float)(nowTick - filter.startTick) / range);
            }

            // Hold
            if (nowTick <= filter.holdEndTick)
                return 1f;

            // Transition out
            if (nowTick <= filter.transitionOutEndTick)
            {
                int range = filter.transitionOutEndTick - filter.holdEndTick;
                if (range <= 0) return 0f;
                return Mathf.Clamp01(1f - (float)(nowTick - filter.holdEndTick) / range);
            }

            return 0f;
        }

        private static void ApplyFilter(ActiveGlobalFilter filter, float weight, int nowTick)
        {
            if (!TryGetOrCreateCameraColor(out object? cameraColorObj, out Behaviour? cameraColor))
                return;

            System.Reflection.FieldInfo? satField = GetSaturationField(cameraColorObj!.GetType());
            if (satField == null) return;

            // 首次激活时保存原始值
            if (!originalValuesCaptured)
            {
                originalSaturation = (float)(satField.GetValue(cameraColorObj) ?? 1f);
                originalColor = GUI.color;
                originalEnabled = cameraColor!.enabled;
                originalValuesCaptured = true;
            }

            float effectiveWeight = Mathf.Clamp01(weight);

            switch (filter.mode)
            {
                case ModeGrayscale:
                case ModeDesaturate:
                    {
                        float targetSat = filter.mode == ModeGrayscale ? 0f : Mathf.Max(0f, 1f - filter.intensity);
                        float sat = Mathf.Lerp(originalSaturation, targetSat, effectiveWeight);
                        satField.SetValue(cameraColorObj, sat);
                    }
                    break;

                case ModeSepia:
                    {
                        // 降低饱和度 + 偏暖色调
                        float sat = Mathf.Lerp(originalSaturation, 0.2f, effectiveWeight);
                        satField.SetValue(cameraColorObj, sat);
                    }
                    break;

                case ModeTint:
                    {
                        // 通过降低饱和度模拟着色效果
                        float sat = Mathf.Lerp(originalSaturation, Mathf.Max(0f, 1f - filter.intensity * 0.5f), effectiveWeight);
                        satField.SetValue(cameraColorObj, sat);
                    }
                    break;

                case ModeNegative:
                    {
                        // 极度降低饱和度 + 反色感
                        float sat = Mathf.Lerp(originalSaturation, 0.05f, effectiveWeight);
                        satField.SetValue(cameraColorObj, sat);
                    }
                    break;
            }

            InvokeMethodIfExists(cameraColorObj, "UpdateParameters");

            if (!cameraColor!.enabled)
            {
                cameraColor.enabled = true;
            }
        }

        private static void RestoreOriginalValues()
        {
            if (!originalValuesCaptured) return;

            if (!TryGetOrCreateCameraColor(out object? cameraColorObj, out Behaviour? cameraColor))
            {
                originalValuesCaptured = false;
                return;
            }

            System.Reflection.FieldInfo? satField = GetSaturationField(cameraColorObj!.GetType());
            if (satField != null)
            {
                satField.SetValue(cameraColorObj, originalSaturation);
            }

            InvokeMethodIfExists(cameraColorObj, "UpdateParameters");

            if (cameraColor != null && createdCameraColorForFilter && !originalEnabled)
            {
                UnityEngine.Object.Destroy(cameraColor);
                createdCameraColorForFilter = false;
            }
            else if (cameraColor != null)
            {
                cameraColor.enabled = originalEnabled;
            }

            originalValuesCaptured = false;
        }

        // ─────────────────────────────────────────────
        // 反射工具（与时停复用相同模式）
        // ─────────────────────────────────────────────

        private static bool TryGetOrCreateCameraColor(out object? cameraColorObject, out Behaviour? cameraColor)
        {
            cameraColorObject = FindCameraColorProperty?.GetValue(null, null);
            cameraColor = cameraColorObject as Behaviour;
            if (cameraColorObject != null && cameraColor != null)
            {
                return true;
            }

            Camera? cameraComponent = FindCameraProperty?.GetValue(null, null) as Camera;
            Type? colorType = FindCameraColorProperty?.PropertyType;
            if (cameraComponent == null || colorType == null)
            {
                return false;
            }

            cameraColor = cameraComponent.GetComponent(colorType) as Behaviour;
            if (cameraColor == null)
            {
                cameraColor = cameraComponent.gameObject.AddComponent(colorType) as Behaviour;
                createdCameraColorForFilter = cameraColor != null;
            }

            cameraColorObject = cameraColor;
            if (cameraColorObject == null)
            {
                return false;
            }

            InvokeMethodIfExists(cameraColorObject, "CheckResources");
            InvokeMethodIfExists(cameraColorObject, "UpdateTextures");
            InvokeMethodIfExists(cameraColorObject, "UpdateParameters");
            return true;
        }

        private static System.Reflection.FieldInfo? GetSaturationField(Type cameraColorType)
        {
            if (SaturationFieldCache.TryGetValue(cameraColorType, out System.Reflection.FieldInfo? cached))
            {
                return cached;
            }

            System.Reflection.FieldInfo? field = AccessTools.Field(cameraColorType, "saturation");
            SaturationFieldCache[cameraColorType] = field;
            return field;
        }

        private static void InvokeMethodIfExists(object target, string methodName)
        {
            System.Reflection.MethodInfo? method = AccessTools.Method(target.GetType(), methodName);
            method?.Invoke(target, null);
        }

        // ── 活跃滤镜实例 ──
        private class ActiveGlobalFilter
        {
            public string mode = string.Empty;
            public Color tintColor = Color.white;
            public float intensity = 1f;
            public int startTick;
            public int transitionInEndTick;
            public int holdEndTick;
            public int transitionOutEndTick;
            public int casterThingId;
            public int mapId;
        }
    }
}
