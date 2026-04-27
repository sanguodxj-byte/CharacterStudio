// ─────────────────────────────────────────────
// 全局滤镜管理器
// 管理技能 VFX 的全屏滤镜效果（灰度、着色、怀旧、反色、黑白等）
//
// 三级渲染路径（自动选择最佳可用路径）：
//   1. Shader  — OnRenderImage + 自定义 Shader Material（需 AssetBundle）
//   2. CPU     — 降采样 → 逐像素颜色变换 → 上采样（无需外部工具，默认启用）
//   3. GUI 叠加层 — 全屏半透明矩形（最终回退，功能受限）
// ─────────────────────────────────────────────

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
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

        // ── 当前帧状态 ──
        private static Color currentOverlayColor = Color.clear;
        private static bool isAnyFilterActive;
        private static bool shaderAvailable;
        private static bool cpuAvailable;
        private static bool systemChecked;
        private static int lastProcessedTick = -1;

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

            EnsureSystemReady();

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
        /// 每帧逻辑更新。由 PawnSkinBootstrapComponent.GameComponentTick 调用。
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

            EnsureSystemReady();

            ActiveGlobalFilter? best = ResolveBestFilterForCurrentMap(nowTick);
            if (best == null)
            {
                ClearAndRestore();
                return;
            }

            UpdateFilterState(best, nowTick);
        }

        /// <summary>
        /// 每帧渲染回调（仅在 GUI 叠加层回退路径时绘制）。
        /// 由 Patch_UIRoot_AbilityHotkeys 在 UIRootOnGUI 中调用。
        /// </summary>
        public static void OnGUI()
        {
            // 有 Shader 或 CPU 后处理时，不需要 GUI 叠加层
            if (!isAnyFilterActive || shaderAvailable || cpuAvailable) return;

            if (currentOverlayColor.a > 0.001f)
            {
                Color prevColor = GUI.color;
                GUI.color = currentOverlayColor;
                Widgets.DrawAtlas(
                    new Rect(0, 0, Verse.UI.screenWidth, Verse.UI.screenHeight),
                    BaseContent.WhiteTex);
                GUI.color = prevColor;
            }
        }

        // ─────────────────────────────────────────────
        // 系统初始化
        // ─────────────────────────────────────────────

        private static void EnsureSystemReady()
        {
            if (systemChecked) return;

            GlobalFilterCameraComponent.EnsureCreated();

            shaderAvailable = GlobalFilterCameraComponent.IsShaderAvailable;

            // CPU 路径始终可用（只要摄像机组件创建成功）
            cpuAvailable = GlobalFilterCameraComponent.IsCreated;

            systemChecked = true;

            if (shaderAvailable)
            {
                Log.Message("[CharacterStudio] 全局滤镜：使用 Shader 后处理路径。");
            }
            else if (cpuAvailable)
            {
                Log.Message("[CharacterStudio] 全局滤镜：使用 CPU 像素处理后处理路径（无需外部依赖）。");
            }
        }

        /// <summary>
        /// 周期性重新检查 Shader 是否变为可用（用户放置 AssetBundle）。
        /// </summary>
        private static void TryUpgradeToShader()
        {
            if (shaderAvailable) return;
            if ((lastProcessedTick % 120) != 0) return;

            GlobalFilterCameraComponent.TryLoadShaderOnce();
            shaderAvailable = GlobalFilterCameraComponent.IsShaderAvailable;
            if (shaderAvailable)
            {
                Log.Message("[CharacterStudio] 全局滤镜已升级到 Shader 后处理路径。");
            }
        }

        // ─────────────────────────────────────────────
        // 滤镜状态更新
        // ─────────────────────────────────────────────

        private static void UpdateFilterState(ActiveGlobalFilter filter, int nowTick)
        {
            float weight = ComputeWeight(filter, nowTick);
            if (weight < 0.001f)
            {
                ClearAndRestore();
                return;
            }

            isAnyFilterActive = true;
            TryUpgradeToShader();
            float effectiveIntensity = filter.intensity * weight;

            // 计算施法者屏幕排除区域（CPU 路径下跳过该区域的滤镜处理）
            UpdateCasterExcludeRect(filter);

            if (shaderAvailable || cpuAvailable)
            {
                ApplyViaPostProcess(filter, weight, effectiveIntensity);
            }
            else
            {
                ApplyViaOverlay(filter, weight, effectiveIntensity);
            }
        }

        /// <summary>
        /// Shader / CPU 后处理路径：通过 GlobalFilterCameraComponent.OnRenderImage 渲染。
        /// </summary>
        private static void ApplyViaPostProcess(ActiveGlobalFilter filter, float weight, float effectiveIntensity)
        {
            GlobalFilterCameraComponent.FilterActive = true;
            GlobalFilterCameraComponent.Saturation = 1f;
            GlobalFilterCameraComponent.Brightness = 1f;
            GlobalFilterCameraComponent.Contrast = 1f;
            GlobalFilterCameraComponent.TintColor = new Color(1f, 1f, 1f, 0f);
            GlobalFilterCameraComponent.Invert = 0f;

            // 设置模式（Shader 可用时优先）
            GlobalFilterCameraComponent.ActiveMode = shaderAvailable
                ? GlobalFilterMode.Shader
                : GlobalFilterMode.Cpu;

            switch (filter.mode)
            {
                case ModeGrayscale:
                    GlobalFilterCameraComponent.Saturation = Mathf.Lerp(1f, 0f, weight);
                    break;

                case ModeDesaturate:
                    GlobalFilterCameraComponent.Saturation = Mathf.Lerp(1f, 1f - filter.intensity, weight);
                    break;

                case ModeSepia:
                    GlobalFilterCameraComponent.Saturation = Mathf.Lerp(1f, 0f, weight);
                    GlobalFilterCameraComponent.TintColor = new Color(1f, 0.85f, 0.65f,
                        Mathf.Lerp(0f, 0.5f, effectiveIntensity));
                    break;

                case ModeTint:
                    GlobalFilterCameraComponent.Saturation = Mathf.Lerp(1f, 0.4f, weight * 0.3f);
                    GlobalFilterCameraComponent.TintColor = new Color(
                        filter.tintColor.r, filter.tintColor.g, filter.tintColor.b,
                        Mathf.Lerp(0f, 0.4f, effectiveIntensity));
                    break;

                case ModeNegative:
                    GlobalFilterCameraComponent.Invert = Mathf.Lerp(0f, 1f, weight);
                    GlobalFilterCameraComponent.Brightness = Mathf.Lerp(1f, 0.9f, weight);
                    GlobalFilterCameraComponent.Contrast = Mathf.Lerp(1f, 1.1f, weight);
                    break;

                case ModeBlackAndWhite:
                    GlobalFilterCameraComponent.Saturation = Mathf.Lerp(1f, 0f, weight);
                    GlobalFilterCameraComponent.Contrast = Mathf.Lerp(1f, 1.4f, weight);
                    GlobalFilterCameraComponent.Brightness = Mathf.Lerp(1f, 0.9f, weight);
                    break;
            }

            currentOverlayColor = Color.clear;
        }

        /// <summary>
        /// GUI 叠加层回退（仅在摄像机组件不可用时使用）。
        /// </summary>
        private static void ApplyViaOverlay(ActiveGlobalFilter filter, float weight, float effectiveIntensity)
        {
            GlobalFilterCameraComponent.FilterActive = false;

            switch (filter.mode)
            {
                case ModeGrayscale:
                    currentOverlayColor = new Color(0.5f, 0.5f, 0.5f, 0.45f * weight);
                    break;
                case ModeDesaturate:
                    currentOverlayColor = new Color(0.5f, 0.5f, 0.5f, 0.35f * effectiveIntensity);
                    break;
                case ModeSepia:
                    currentOverlayColor = new Color(0.6f, 0.42f, 0.2f, 0.4f * effectiveIntensity);
                    break;
                case ModeTint:
                    currentOverlayColor = new Color(
                        filter.tintColor.r, filter.tintColor.g, filter.tintColor.b,
                        0.45f * effectiveIntensity);
                    break;
                case ModeNegative:
                    currentOverlayColor = new Color(1f, 1f, 1f, 0.65f * effectiveIntensity);
                    break;
                case ModeBlackAndWhite:
                    currentOverlayColor = new Color(0.15f, 0.15f, 0.15f, 0.55f * weight);
                    break;
            }
        }

        // ─────────────────────────────────────────────
        // 清除
        // ─────────────────────────────────────────────

        private static void ClearAndRestore()
        {
            isAnyFilterActive = false;
            currentOverlayColor = Color.clear;

            GlobalFilterCameraComponent.FilterActive = false;
            GlobalFilterCameraComponent.ActiveMode = GlobalFilterMode.None;
            GlobalFilterCameraComponent.Saturation = 1f;
            GlobalFilterCameraComponent.Brightness = 1f;
            GlobalFilterCameraComponent.Contrast = 1f;
            GlobalFilterCameraComponent.TintColor = new Color(1f, 1f, 1f, 0f);
            GlobalFilterCameraComponent.Invert = 0f;
            GlobalFilterCameraComponent.ExcludeUV = Rect.zero;
        }

        // ─────────────────────────────────────────────
        // 施法者屏幕区域排除
        // ─────────────────────────────────────────────

        private static readonly System.Reflection.PropertyInfo? FindCameraProp =
            AccessTools.Property(typeof(Find), "Camera");

        private static Camera? FindCamera()
        {
            Camera? cam = Camera.main;
            if (cam != null) return cam;
            cam = FindCameraProp?.GetValue(null, null) as Camera;
            if (cam != null) return cam;
            Camera[] all = Camera.allCameras;
            if (all.Length > 0) return all[0];
            return null;
        }

        private static void UpdateCasterExcludeRect(ActiveGlobalFilter filter)
        {
            GlobalFilterCameraComponent.ExcludeUV = Rect.zero;

            // Shader 路径暂不支持像素级排除（需要模板缓冲），仅 CPU 路径生效
            if (shaderAvailable) return;

            Camera? cam = FindCamera();
            if (cam == null) return;

            Map? map = FindMapById(filter.mapId);
            if (map == null) return;

            Pawn? caster = FindPawnById(map, filter.casterThingId);
            if (caster == null || !caster.Spawned) return;

            Vector3 worldPos = caster.DrawPos;
            // 施法者世界空间包围盒（单位：格，足够覆盖角色全身 + 武器/特效）
            float worldRadiusH = 1.0f;
            float worldRadiusV = 2.0f;

            Vector3 screenCenter = cam.WorldToScreenPoint(worldPos);
            Vector3 screenRight = cam.WorldToScreenPoint(worldPos + new Vector3(worldRadiusH, 0, 0));
            Vector3 screenTop = cam.WorldToScreenPoint(worldPos + new Vector3(0, 0, worldRadiusV));

            float pixelW = Mathf.Max(24f, Mathf.Abs(screenRight.x - screenCenter.x) * 2f);
            float pixelH = Mathf.Max(48f, Mathf.Abs(screenTop.y - screenCenter.y) * 2f);

            float uCenter = screenCenter.x / (float)Screen.width;
            float vCenter = screenCenter.y / (float)Screen.height;
            float uHalf = (pixelW * 0.5f) / (float)Screen.width;
            float vHalf = (pixelH * 0.5f) / (float)Screen.height;

            GlobalFilterCameraComponent.ExcludeUV = new Rect(
                Mathf.Clamp01(uCenter - uHalf),
                Mathf.Clamp01(vCenter - vHalf),
                Mathf.Clamp01(uCenter + uHalf) - Mathf.Clamp01(uCenter - uHalf),
                Mathf.Clamp01(vCenter + vHalf) - Mathf.Clamp01(vCenter - vHalf));
        }

        private static Map? FindMapById(int mapId)
        {
            if (Current.Game?.Maps == null) return null;
            for (int i = 0; i < Current.Game.Maps.Count; i++)
            {
                if (Current.Game.Maps[i]?.uniqueID == mapId)
                    return Current.Game.Maps[i];
            }
            return null;
        }

        private static Pawn? FindPawnById(Map map, int thingId)
        {
            if (map?.mapPawns?.AllPawnsSpawned == null) return null;
            for (int i = 0; i < map.mapPawns.AllPawnsSpawned.Count; i++)
            {
                if (map.mapPawns.AllPawnsSpawned[i]?.thingIDNumber == thingId)
                    return map.mapPawns.AllPawnsSpawned[i];
            }
            return null;
        }

        // ─────────────────────────────────────────────
        // 滤镜权重计算
        // ─────────────────────────────────────────────

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
            if (nowTick <= filter.transitionInEndTick)
                return Mathf.Clamp01((float)(nowTick - filter.startTick) / Math.Max(1, filter.transitionInEndTick - filter.startTick));
            if (nowTick <= filter.holdEndTick) return 1f;
            if (nowTick <= filter.transitionOutEndTick)
                return Mathf.Clamp01(1f - (float)(nowTick - filter.holdEndTick) / Math.Max(1, filter.transitionOutEndTick - filter.holdEndTick));
            return 0f;
        }

        public static void ClearAll()
        {
            activeFilters.Clear();
            ClearAndRestore();
        }

        // ─────────────────────────────────────────────
        // 滤镜实例数据
        // ─────────────────────────────────────────────

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
