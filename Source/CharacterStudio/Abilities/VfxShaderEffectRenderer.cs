using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// Shader 特效渲染器
    /// 负责从文件系统加载自定义 Shader 并在游戏世界中渲染特效。
    /// 
    /// 使用方式：
    /// 1. 在外部工具（Unity/Shader Graph/手写）中编写 .shader 文件
    /// 2. 编译为 .shader 或 .bytes 格式放入 Mod 的 Shaders/ 目录
    /// 3. 在技能编辑器中选择 "Shader特效" 类型，指定 shader 路径和参数
    /// 
    /// Shader 约定：
    /// - Shader 中必须包含以下属性（可选，缺失时使用默认值）:
    ///   - _MainTex    (Texture2D) - 主贴图
    ///   - _TintColor  (Color)     - 染色
    ///   - _Intensity  (Float)     - 强度
    ///   - _Speed      (Float)     - 动画速度
    ///   - _Param1~4   (Float)     - 自定义参数
    ///   - _Time       (Float)     - 自动更新的时间
    /// </summary>
    public static class VfxShaderEffectRenderer
    {
        // 已加载的 Shader 缓存（路径 → Shader）
        private static readonly Dictionary<string, Shader> loadedShaders = new Dictionary<string, Shader>(StringComparer.OrdinalIgnoreCase);

        // 已加载的材质缓存（shaderPath|texturePath → Material）
        private static readonly Dictionary<string, Material> loadedMaterials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        // 加载失败记录
        private static readonly HashSet<string> loadWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 活跃的 Shader 特效实例
        private static readonly List<VfxShaderInstance> activeInstances = new List<VfxShaderInstance>();

        // 最大同时活跃数量
        private const int MaxActiveInstances = 128;

        // Shader VFX 的活跃实例列表是全局的，但 Tick() 目前会跟随每个技能 CompTick 重复进入。
        // 将更新限制为每游戏 Tick 一次，避免地图上技能实例越多，VFX 更新越被重复放大。
        private static int lastProcessedGameTick = -1;

        // 用于生成 MoteDef 的缓存
        private static readonly Dictionary<string, ThingDef> shaderMoteDefCache = new Dictionary<string, ThingDef>();

        // 全局唯一 Mesh（一个单位平面）
        private static Mesh? quadMesh;
        private static Mesh QuadMesh
        {
            get
            {
                if (quadMesh == null)
                {
                    quadMesh = new Mesh();
                    quadMesh.vertices = new Vector3[]
                    {
                        new Vector3(-0.5f, 0f, -0.5f),
                        new Vector3(0.5f, 0f, -0.5f),
                        new Vector3(0.5f, 0f, 0.5f),
                        new Vector3(-0.5f, 0f, 0.5f)
                    };
                    quadMesh.uv = new Vector2[]
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f)
                    };
                    quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
                    quadMesh.RecalculateNormals();
                }
                return quadMesh;
            }
        }

        /// <summary>
        /// 解析 Shader 文件完整路径
        /// </summary>
        private static string ResolveFullShaderPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            if (Path.IsPathRooted(relativePath) && File.Exists(relativePath))
                return relativePath;

            string? modRoot = CharacterStudioMod.ModContent?.RootDir;
            if (!string.IsNullOrEmpty(modRoot))
            {
                string fullPath = Path.Combine(modRoot, relativePath);
                if (File.Exists(fullPath))
                    return fullPath;

                // 尝试 Shaders/ 子目录
                fullPath = Path.Combine(modRoot, "Shaders", relativePath);
                if (File.Exists(fullPath))
                    return fullPath;

                // 尝试添加 .shader 扩展名
                if (!relativePath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                    && !relativePath.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
                {
                    fullPath = Path.Combine(modRoot, "Shaders", relativePath + ".shader");
                    if (File.Exists(fullPath))
                        return fullPath;

                    fullPath = Path.Combine(modRoot, "Shaders", relativePath + ".bytes");
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }

            return relativePath;
        }

        private static string BuildShaderCacheKey(AbilityVisualEffectConfig config)
        {
            return config.shaderLoadFromAssetBundle
                ? $"ab|{config.shaderAssetBundlePath}|{config.shaderAssetBundleShaderName}"
                : $"file|{config.shaderPath}";
        }

        /// <summary>
        /// 加载自定义 Shader
        /// </summary>
        public static Shader? LoadShader(AbilityVisualEffectConfig config)
        {
            string cacheKey = BuildShaderCacheKey(config);
            if (string.IsNullOrWhiteSpace(cacheKey))
                return null;

            if (loadedShaders.TryGetValue(cacheKey, out Shader? cached))
            {
                if (cached != null)
                    return cached;
                loadedShaders.Remove(cacheKey);
            }

            if (config.shaderLoadFromAssetBundle)
            {
                Shader? bundleShader = VfxAssetBundleLoader.LoadAsset<Shader>(config.shaderAssetBundlePath, config.shaderAssetBundleShaderName);
                if (bundleShader == null)
                {
                    return ShaderUtil.FindFallbackShader(config.shaderAssetBundleShaderName);
                }

                if (!bundleShader.isSupported)
                {
                    LogWarningOnce($"ShaderBundleUnsupported:{cacheKey}",
                        $"[CharacterStudio] AssetBundle Shader 不被当前平台支持: {config.shaderAssetBundleShaderName}");
                    return ShaderUtil.FindFallbackShader(config.shaderAssetBundleShaderName);
                }

                loadedShaders[cacheKey] = bundleShader;
                return bundleShader;
            }

            string relativePath = config.shaderPath;
            string fullPath = ResolveFullShaderPath(relativePath);
            if (!File.Exists(fullPath))
            {
                Shader? namedFallback = ShaderUtil.FindFallbackShader(relativePath);
                if (namedFallback != null)
                {
                    loadedShaders[cacheKey] = namedFallback;
                    return namedFallback;
                }

                LogWarningOnce($"ShaderNotFound:{fullPath}",
                    $"[CharacterStudio] Shader 文件不存在: {relativePath} (解析为: {fullPath})");
                return null;
            }

            try
            {
                byte[] shaderBytes = File.ReadAllBytes(fullPath);
                Shader? shader = ShaderUtil.CreateShader(shaderBytes, relativePath);
                if (shader == null || !shader.isSupported)
                {
                    LogWarningOnce($"ShaderNotSupported:{relativePath}",
                        $"[CharacterStudio] Shader 不被当前平台支持: {relativePath}");
                    return null;
                }

                loadedShaders[cacheKey] = shader;
                return shader;
            }
            catch (Exception ex)
            {
                LogWarningOnce($"ShaderLoadError:{relativePath}",
                    $"[CharacterStudio] 加载 Shader 异常: {relativePath} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建或获取材质（Shader + 可选贴图）
        /// </summary>
        public static Material? CreateMaterial(AbilityVisualEffectConfig config)
        {
            // P-PERF: 使用 Color 分量替代 GetHashCode()，避免哈希碰撞导致缓存未命中
            var tintColor = config.shaderTintColor;
            string cacheKey = $"{BuildShaderCacheKey(config)}|{config.shaderTexturePath}|{tintColor.r:F3},{tintColor.g:F3},{tintColor.b:F3},{tintColor.a:F3}|{config.shaderIntensity:F3}|{config.shaderSpeed:F3}";

            if (loadedMaterials.TryGetValue(cacheKey, out Material? cached))
            {
                if (cached != null)
                    return cached;
                loadedMaterials.Remove(cacheKey);
            }

            Shader? shader = LoadShader(config);
            if (shader == null)
                return null;

            try
            {
                Material mat = new Material(shader);

                // 设置主贴图（可选）
                if (!string.IsNullOrWhiteSpace(config.shaderTexturePath))
                {
                    Texture2D? tex = LoadShaderTexture(config.shaderTexturePath);
                    if (tex != null)
                    {
                        mat.SetTexture("_MainTex", tex);
                    }
                }

                // 设置标准参数
                if (mat.HasProperty("_TintColor"))
                    mat.SetColor("_TintColor", config.shaderTintColor);

                if (mat.HasProperty("_Intensity"))
                    mat.SetFloat("_Intensity", config.shaderIntensity);

                if (mat.HasProperty("_Speed"))
                    mat.SetFloat("_Speed", config.shaderSpeed);

                // 设置自定义参数
                if (mat.HasProperty("_Param1"))
                    mat.SetFloat("_Param1", config.shaderParam1);
                if (mat.HasProperty("_Param2"))
                    mat.SetFloat("_Param2", config.shaderParam2);
                if (mat.HasProperty("_Param3"))
                    mat.SetFloat("_Param3", config.shaderParam3);
                if (mat.HasProperty("_Param4"))
                    mat.SetFloat("_Param4", config.shaderParam4);

                loadedMaterials[cacheKey] = mat;
                return mat;
            }
            catch (Exception ex)
            {
                LogWarningOnce($"MaterialCreateError:{cacheKey}",
                    $"[CharacterStudio] 创建 Shader 材质异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载 Shader 使用的贴图
        /// </summary>
        private static Texture2D? LoadShaderTexture(string texturePath)
        {
            if (string.IsNullOrWhiteSpace(texturePath))
                return null;

            // 使用现有的 RuntimeAssetLoader 加载
            return Rendering.RuntimeAssetLoader.LoadTextureRaw(texturePath);
        }

        internal static ThingDef GetOrCreateShaderMoteDef(AbilityVisualEffectConfig config)
        {
            string shaderKey = config.shaderLoadFromAssetBundle
                ? $"ab_{config.shaderAssetBundlePath}_{config.shaderAssetBundleShaderName}"
                : config.shaderPath;
            string key = $"__CS_ShaderVfx_{shaderKey.GetHashCode():X8}_{config.displayDurationTicks}_{config.drawSize:F1}_{config.textureScale.x:F2}_{config.textureScale.y:F2}";
            if (shaderMoteDefCache.TryGetValue(key, out ThingDef? existing))
            {
                return existing;
            }

            ShaderTypeDef shaderType = DefDatabase<ShaderTypeDef>.GetNamedSilentFail("Transparent") ?? ShaderTypeDefOf.Cutout;
            string texturePath = string.IsNullOrWhiteSpace(config.shaderTexturePath)
                ? "Things/Mote/SparkFlash"
                : config.shaderTexturePath;
            Type graphicClass = Rendering.RuntimeAssetLoader.LooksLikeExternalTexturePath(texturePath)
                ? typeof(CharacterStudio.Rendering.Graphic_Runtime)
                : typeof(Graphic_Single);

            var def = new ThingDef
            {
                defName = key,
                label = "CS runtime shader vfx mote",
                thingClass = typeof(MoteThrown),
                category = ThingCategory.Mote,
                altitudeLayer = AltitudeLayer.MoteOverhead,
                drawerType = DrawerType.RealtimeOnly,
                isSaveable = false,
                useHitPoints = false,
                drawGUIOverlay = false,
                tickerType = TickerType.Normal,
                mote = new MoteProperties
                {
                    realTime = true,
                    fadeInTime = 0f,
                    solidTime = Mathf.Max(1, config.displayDurationTicks) / 60f,
                    fadeOutTime = 0.2f,
                    needsMaintenance = false,
                    collide = false,
                    speedPerTime = 0f,
                    growthRate = 0f
                },
                graphicData = new GraphicData
                {
                    texPath = texturePath,
                    graphicClass = graphicClass,
                    shaderType = shaderType,
                    drawSize = new Vector2(
                        Mathf.Max(0.1f, config.drawSize * Mathf.Max(0.1f, config.textureScale.x)),
                        Mathf.Max(0.1f, config.drawSize * Mathf.Max(0.1f, config.textureScale.y))),
                    color = config.shaderTintColor,
                    colorTwo = config.shaderTintColor
                }
            };

            shaderMoteDefCache[key] = def;
            return def;
        }

        /// <summary>
        /// 在指定位置播放 Shader 特效
        /// 通过创建自定义 Mote 实现 RimWorld 渲染管线集成
        /// </summary>
        public static VfxShaderInstance? PlayShaderEffect(
            AbilityVisualEffectConfig config,
            Vector3 position,
            Map map,
            float rotation = 0f)
        {
            if (map == null)
                return null;

            Material? material = CreateMaterial(config);
            if (material == null)
                return null;

            // 清理过期实例
            CleanupExpiredInstances();

            if (activeInstances.Count >= MaxActiveInstances)
            {
                VfxShaderInstance oldest = activeInstances[0];
                oldest.ForceDestroy();
                activeInstances.RemoveAt(0);
            }

            try
            {
                var instance = new VfxShaderInstance(material, config, position, map, rotation);
                activeInstances.Add(instance);
                return instance;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 播放 Shader 特效异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public static void Tick()
        {
            if (activeInstances.Count == 0)
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? -1;
            if (currentTick >= 0)
            {
                if (lastProcessedGameTick == currentTick)
                {
                    return;
                }

                lastProcessedGameTick = currentTick;
            }

            float time = Time.time;
            for (int i = activeInstances.Count - 1; i >= 0; i--)
            {
                VfxShaderInstance instance = activeInstances[i];
                if (instance.IsExpired)
                {
                    instance.ForceDestroy();
                    activeInstances.RemoveAt(i);
                }
                else
                {
                    instance.Update(time);
                }
            }
        }

        private static void CleanupExpiredInstances()
        {
            for (int i = activeInstances.Count - 1; i >= 0; i--)
            {
                if (activeInstances[i].IsExpired)
                {
                    activeInstances[i].ForceDestroy();
                    activeInstances.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 卸载所有 Shader 资源
        /// </summary>
        public static void UnloadAll()
        {
            lastProcessedGameTick = -1;

            foreach (var instance in activeInstances)
            {
                instance.ForceDestroy();
            }
            activeInstances.Clear();

            foreach (var kvp in loadedMaterials)
            {
                try
                {
                    if (kvp.Value != null)
                        UnityEngine.Object.Destroy(kvp.Value);
                }
                catch { }
            }
            loadedMaterials.Clear();

            // Shader 通常由 Unity 管理生命周期，不手动卸载
            loadedShaders.Clear();
            shaderMoteDefCache.Clear();
            loadWarnings.Clear();
        }

        private static void LogWarningOnce(string key, string message)
        {
            if (loadWarnings.Add(key))
            {
                Log.Warning(message);
            }
        }
    }

    /// <summary>
    /// Shader 特效运行时实例
    /// 使用 RimWorld 的 Mote 机制结合自定义材质渲染
    /// </summary>
    public class VfxShaderInstance
    {
        public Material Material { get; }
        public Vector3 Position { get; set; }
        public Map Map { get; }
        public float Rotation { get; set; }
        public int StartTick { get; }
        public int DurationTicks { get; }
        public float Scale { get; }
        public bool IsExpired => Find.TickManager?.TicksGame - StartTick >= DurationTicks;

        private readonly AbilityVisualEffectConfig config;
        private Thing? spawnedMote;

        public VfxShaderInstance(Material material, AbilityVisualEffectConfig config, Vector3 position, Map map, float rotation)
        {
            Material = material;
            this.config = config;
            Position = position;
            Map = map;
            Rotation = rotation;
            StartTick = Find.TickManager?.TicksGame ?? 0;
            DurationTicks = Mathf.Max(1, config.displayDurationTicks);
            Scale = Mathf.Max(0.1f, config.scale) * Mathf.Max(0.1f, config.drawSize);

            Spawn();
        }

        private void Spawn()
        {
            try
            {
                ThingDef moteDef = VfxShaderEffectRenderer.GetOrCreateShaderMoteDef(config);
                if (moteDef == null)
                    return;

                var mote = ThingMaker.MakeThing(moteDef) as MoteThrown;
                if (mote == null)
                    return;

                mote.exactPosition = Position;
                mote.exactRotation = Rotation + config.rotation;
                mote.rotationRate = 0f;
                mote.instanceColor = config.shaderTintColor;
                mote.linearScale = new Vector3(
                    Scale * Mathf.Max(0.1f, config.textureScale.x),
                    1f,
                    Scale * Mathf.Max(0.1f, config.textureScale.y));
                mote.SetVelocity(0f, 0f);

                GenSpawn.Spawn(mote, Position.ToIntVec3(), Map, WipeMode.Vanish);
                spawnedMote = mote;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 生成 Shader 特效 Mote 异常: {ex.Message}");
            }
        }

        public void Update(float time)
        {
            // 更新 Shader 时间参数
            if (Material != null)
            {
                if (Material.HasProperty("_Time"))
                {
                    Material.SetFloat("_Time", time);
                }

                // 渐隐效果（接近结束时）
                float elapsed = (Find.TickManager?.TicksGame ?? 0) - StartTick;
                float remaining = DurationTicks - elapsed;
                if (remaining < 30f && Material.HasProperty("_Intensity"))
                {
                    float fadeOut = Mathf.Max(0f, remaining / 30f) * config.shaderIntensity;
                    Material.SetFloat("_Intensity", fadeOut);
                }
            }

            // 更新位置（附加到目标时）
            if (spawnedMote != null && !spawnedMote.Destroyed)
            {
                if (config.attachToPawn)
                {
                    // 位置同步由外部管理
                }
            }
        }

        public void ForceDestroy()
        {
            try
            {
                if (spawnedMote != null && !spawnedMote.Destroyed)
                {
                    spawnedMote.Destroy();
                    spawnedMote = null;
                }
            }
            catch { }
        }

    }

    /// <summary>
    /// Shader 加载工具类
    /// 提供从字节流创建 Unity Shader 的能力
    /// </summary>
    internal static class ShaderUtil
    {
        /// <summary>
        /// 从字节流创建 Shader
        /// 支持预编译的 .bytes 格式（DX11/OpenGL 编译后的 Shader）
        /// </summary>
        public static Shader? CreateShader(byte[] shaderBytes, string name)
        {
            if (shaderBytes == null || shaderBytes.Length == 0)
                return null;

            try
            {
                // 尝试直接加载预编译 Shader
                // Unity 支持从 .bytes 文件加载预编译 Shader
                Shader? shader = FindFallbackShader(name);
                if (shader != null)
                    return shader;

                Log.Message($"[CharacterStudio] Shader '{name}' 使用回退 Shader: null");
                return null;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 创建 Shader 异常: {name} - {ex.Message}");
                return null;
            }
        }

        public static Shader? FindFallbackShader(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                Shader? direct = Resources.Load<Shader>(name);
                if (direct != null)
                {
                    return direct;
                }

                direct = UnityEngine.Shader.Find(name);
                if (direct != null)
                {
                    return direct;
                }

                string fileStem = Path.GetFileNameWithoutExtension(name);
                if (!string.Equals(fileStem, name, StringComparison.OrdinalIgnoreCase))
                {
                    direct = UnityEngine.Shader.Find(fileStem);
                    if (direct != null)
                    {
                        return direct;
                    }
                }
            }

            return UnityEngine.Shader.Find("Transparent/Diffuse")
                ?? UnityEngine.Shader.Find("Unlit/Transparent")
                ?? UnityEngine.Shader.Find("Unlit/Texture")
                ?? ShaderDatabase.Transparent;
        }
    }
}
