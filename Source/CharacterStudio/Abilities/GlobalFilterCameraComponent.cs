// ─────────────────────────────────────────────
// 全局滤镜摄像机后处理组件
// 通过 OnRenderImage 实现全屏滤镜效果。
//
// 三级渲染路径（自动降级）：
//   1. Shader 路径   — Graphics.Blit + 自定义 Shader Material（最佳质量）
//                      需要 Shader "CharacterStudio/GlobalFilter" 从 AssetBundle 预加载
//   2. CPU 路径      — 降采样 → 逐像素颜色变换 → 上采样（无需任何外部工具）
//   3. GUI 叠加层    — VfxGlobalFilterManager.OnGUI() 半透明矩形（近似效果）
//
// 滤镜参数由 VfxGlobalFilterManager 通过静态字段写入。
// ─────────────────────────────────────────────

using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    internal enum GlobalFilterMode
    {
        None,
        Shader,
        Cpu
    }

    /// <summary>
    /// 附加到主摄像机的后处理 MonoBehaviour。
    /// </summary>
    internal class GlobalFilterCameraComponent : MonoBehaviour
    {
        // ── 滤镜参数（由 VfxGlobalFilterManager 每帧写入）──
        public static float Saturation = 1f;
        public static float Brightness = 1f;
        public static float Contrast = 1f;
        public static Color TintColor = new Color(1f, 1f, 1f, 0f);
        public static float Invert;
        public static bool FilterActive;
        public static GlobalFilterMode ActiveMode = GlobalFilterMode.None;

        // ── Shader 资源 ──
        private Material? filterMaterial;

        // ── CPU 处理缓存 ──
        private RenderTexture? downscaleRT;
        private Texture2D? processedTex;
        private Color32[]? pixelBuffer;

        // ── 静态实例 ──
        private static GlobalFilterCameraComponent? instance;

        // ── 摄像机查找 ──
        private static readonly System.Reflection.PropertyInfo? FindCameraProperty =
            AccessTools.Property(typeof(Find), "Camera");

        private const string ShaderName = "CharacterStudio/GlobalFilter";
        private const string BundlePath = "Effects/cs_global_filter";

        /// <summary>
        /// Shader 是否已成功加载。
        /// </summary>
        public static bool IsShaderAvailable => instance?.filterMaterial != null;

        /// <summary>
        /// 摄像机组件是否已创建成功。
        /// </summary>
        public static bool IsCreated => instance != null;

        /// <summary>
        /// 查找主摄像机（RimWorld 兼容，多重回退）。
        /// </summary>
        private static Camera? FindMainCamera()
        {
            // 1. 标准 Unity Camera.main（需要 "MainCamera" tag）
            Camera? cam = Camera.main;
            if (cam != null) return cam;

            // 2. RimWorld: Find.Camera 属性（反射访问，最可靠）
            cam = FindCameraProperty?.GetValue(null, null) as Camera;
            if (cam != null) return cam;

            // 3. 遍历场景中所有启用的摄像机
            Camera[] allCameras = Camera.allCameras;
            if (allCameras.Length > 0)
            {
                // 优先选择 tag 为 "MainCamera" 或名称包含 "Camera" 的
                foreach (Camera c in allCameras)
                {
                    if (c.CompareTag("MainCamera")) return c;
                }
                foreach (Camera c in allCameras)
                {
                    if (c.name.Contains("Camera")) return c;
                }
                return allCameras[0]; // 最终回退
            }

            return null;
        }

        /// <summary>
        /// 确保摄像机组件已创建并附加到主摄像机。
        /// </summary>
        public static void EnsureCreated()
        {
            if (instance != null) return;

            Camera? cam = FindMainCamera();
            if (cam == null)
            {
                Log.Warning("[CharacterStudio] 全局滤镜：无法找到主摄像机，将使用 GUI 叠加层回退。");
                return;
            }

            instance = cam.gameObject.AddComponent<GlobalFilterCameraComponent>();
            instance.enabled = true;
            instance.TryLoadShader();

            ActiveMode = instance.filterMaterial != null
                ? GlobalFilterMode.Shader
                : GlobalFilterMode.Cpu;

            Log.Message($"[CharacterStudio] 全局滤镜：摄像机组件已附加到 '{cam.name}'，模式 = {ActiveMode}。");
        }

        /// <summary>
        /// 周期性重试着色器加载（AssetBundle 延迟放置场景）。
        /// </summary>
        public static void TryLoadShaderOnce()
        {
            if (instance == null) return;
            if (instance.filterMaterial != null) return;
            instance.TryLoadShader();
            if (instance.filterMaterial != null)
            {
                ActiveMode = GlobalFilterMode.Shader;
            }
        }

        // ─────────────────────────────────────────────
        // Shader 加载
        // ─────────────────────────────────────────────

        private void TryLoadShader()
        {
            Shader? shader = null;

            try
            {
                shader = VfxAssetBundleLoader.LoadAsset<Shader>(BundlePath, ShaderName);
                if (shader != null)
                {
                    Log.Message($"[CharacterStudio] 已从 AssetBundle 加载全局滤镜 Shader: {BundlePath}");
                }
            }
            catch { /* 忽略 */ }

            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
                if (shader != null)
                {
                    Log.Message($"[CharacterStudio] 已通过 Shader.Find 找到全局滤镜 Shader: {ShaderName}");
                }
            }

            if (shader == null || !shader.isSupported) return;

            filterMaterial = new Material(shader)
            {
                name = "CS_GlobalFilter_Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        // ─────────────────────────────────────────────
        // OnRenderImage — 渲染管线后处理入口
        // ─────────────────────────────────────────────

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!FilterActive)
            {
                Graphics.Blit(source, destination);
                return;
            }

            switch (ActiveMode)
            {
                case GlobalFilterMode.Shader:
                    ApplyViaShader(source, destination);
                    break;
                case GlobalFilterMode.Cpu:
                    ApplyViaCpu(source, destination);
                    break;
                default:
                    Graphics.Blit(source, destination);
                    break;
            }
        }

        // ─────────────────────────────────────────────
        // Shader 路径
        // ─────────────────────────────────────────────

        private void ApplyViaShader(RenderTexture source, RenderTexture destination)
        {
            if (filterMaterial == null)
            {
                // Shader 意外丢失，降级到 CPU
                ActiveMode = GlobalFilterMode.Cpu;
                ApplyViaCpu(source, destination);
                return;
            }

            if (filterMaterial.HasProperty("_Saturation"))
                filterMaterial.SetFloat("_Saturation", Saturation);
            if (filterMaterial.HasProperty("_Brightness"))
                filterMaterial.SetFloat("_Brightness", Brightness);
            if (filterMaterial.HasProperty("_Contrast"))
                filterMaterial.SetFloat("_Contrast", Contrast);
            if (filterMaterial.HasProperty("_TintColor"))
                filterMaterial.SetColor("_TintColor", TintColor);
            if (filterMaterial.HasProperty("_Invert"))
                filterMaterial.SetFloat("_Invert", Invert);

            Graphics.Blit(source, destination, filterMaterial);
        }

        // ─────────────────────────────────────────────
        // CPU 路径：降采样 → 逐像素处理 → 上采样
        // ─────────────────────────────────────────────

        /// <summary>
        /// CPU 处理的目标最大像素数。值越小性能越好，但画质越低。
        /// ~130K = 480×270 @1080p，适合全屏滤镜（色彩偏移/灰度等对精度不敏感的效果）。
        /// </summary>
        private const int MaxCpuPixels = 130_000;

        /// <summary>
        /// 施法者排除区域（UV 空间，由 VfxGlobalFilterManager 每帧写入）。
        /// 为零 Rect 表示不排除任何区域。
        /// </summary>
        public static Rect ExcludeUV = Rect.zero;

        private void ApplyViaCpu(RenderTexture source, RenderTexture destination)
        {
            int w = source.width;
            int h = source.height;

            // 计算降采样因子，目标不超过 MaxCpuPixels
            int scaleDiv = 1;
            while ((w / scaleDiv) * (h / scaleDiv) > MaxCpuPixels) scaleDiv++;

            int dw = w / scaleDiv;
            int dh = h / scaleDiv;

            // 确保降采样 RT 存在且尺寸匹配
            if (downscaleRT == null || downscaleRT.width != dw || downscaleRT.height != dh)
            {
                ReleaseCpuResources();
                downscaleRT = new RenderTexture(dw, dh, 0, RenderTextureFormat.ARGB32)
                {
                    name = "CS_FilterDownscale",
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                downscaleRT.Create();
            }

            // 降采样
            Graphics.Blit(source, downscaleRT);

            // 确保处理纹理存在且尺寸匹配
            if (processedTex == null || processedTex.width != dw || processedTex.height != dh)
            {
                if (processedTex != null) Destroy(processedTex);
                processedTex = new Texture2D(dw, dh, TextureFormat.RGBA32, false)
                {
                    name = "CS_FilterProcessed",
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                pixelBuffer = null; // 强制重新分配
            }

            // 分配或复用像素缓冲区
            int pixelCount = dw * dh;
            if (pixelBuffer == null || pixelBuffer.Length != pixelCount)
            {
                pixelBuffer = new Color32[pixelCount];
            }

            // 从 GPU 读取像素
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = downscaleRT;
            processedTex.ReadPixels(new Rect(0, 0, dw, dh), 0, 0);
            RenderTexture.active = previousActive;

            // 获取像素数组
            Color32[] pixels = processedTex.GetPixels32();

            ApplyFilterCpu(pixels, pixelBuffer, pixelCount, dw, dh,
                Saturation, Brightness, Contrast, TintColor, Invert, ExcludeUV);
            processedTex.SetPixels32(pixelBuffer);
            processedTex.Apply(false);

            // 上采样到目标
            Graphics.Blit(processedTex, destination);
        }

        /// <summary>
        /// 在 CPU 上逐像素应用滤镜变换，同时排除施法者区域（ExcludeUV）。
        /// 处理逻辑与 CS_GlobalFilter.shader 完全一致。
        /// </summary>
        private static void ApplyFilterCpu(
            Color32[] source, Color32[] dest, int count,
            int texW, int texH,
            float saturation, float brightness, float contrast,
            Color tintColor, float invert,
            Rect excludeUV)
        {
            float tintR = tintColor.r;
            float tintG = tintColor.g;
            float tintB = tintColor.b;
            float tintA = tintColor.a;
            bool hasExclude = excludeUV.width > 0.001f && excludeUV.height > 0.001f;
            float invW = 1f / texW;
            float invH = 1f / texH;

            for (int i = 0; i < count; i++)
            {
                Color32 c = source[i];

                // 施法者区域跳过滤镜处理
                if (hasExclude)
                {
                    int px = i % texW;
                    int py = i / texW;
                    float u = (px + 0.5f) * invW;
                    float v = (py + 0.5f) * invH;
                    if (excludeUV.Contains(new Vector2(u, v)))
                    {
                        dest[i] = c;
                        continue;
                    }
                }

                // 转为浮点范围 [0, 1]
                float r = c.r * 0.003921569f; // 1/255
                float g = c.g * 0.003921569f;
                float b = c.b * 0.003921569f;

                // 1. 饱和度：基于亮度权重的灰度混合
                float gray = 0.299f * r + 0.587f * g + 0.114f * b;
                r = gray + (r - gray) * saturation;
                g = gray + (g - gray) * saturation;
                b = gray + (b - gray) * saturation;

                // 2. 亮度
                r *= brightness;
                g *= brightness;
                b *= brightness;

                // 3. 对比度（以 0.5 为中心）
                r = (r - 0.5f) * contrast + 0.5f;
                g = (g - 0.5f) * contrast + 0.5f;
                b = (b - 0.5f) * contrast + 0.5f;

                // 4. 色调叠加
                if (tintA > 0.001f)
                {
                    r = r + (r * tintR - r) * tintA;
                    g = g + (g * tintG - g) * tintA;
                    b = b + (b * tintB - b) * tintA;
                }

                // 5. 反色
                if (invert > 0.001f)
                {
                    r = r + (1f - r - r) * invert;
                    g = g + (1f - g - g) * invert;
                    b = b + (1f - b - b) * invert;
                }

                // 钳制并写回
                dest[i] = new Color32(
                    ClampToByte(r),
                    ClampToByte(g),
                    ClampToByte(b),
                    c.a);
            }
        }

        private static byte ClampToByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 1f) return 255;
            return (byte)(v * 255f + 0.5f);
        }

        // ─────────────────────────────────────────────
        // 生命周期
        // ─────────────────────────────────────────────

        private void ReleaseCpuResources()
        {
            if (downscaleRT != null) { downscaleRT.Release(); Destroy(downscaleRT); downscaleRT = null; }
            if (processedTex != null) { Destroy(processedTex); processedTex = null; }
            pixelBuffer = null;
        }

        private void OnDestroy()
        {
            if (filterMaterial != null) { Destroy(filterMaterial); filterMaterial = null; }
            ReleaseCpuResources();

            if (instance == this) instance = null;
        }

        private void OnDisable()
        {
            FilterActive = false;
            ActiveMode = GlobalFilterMode.None;
        }
    }
}
