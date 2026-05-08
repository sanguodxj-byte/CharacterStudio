using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Rendering;
using UnityEngine;
using RimWorld;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// AssetBundle 特效资源加载器（VFX 专用层）
    /// 负责特效预制体加载、播放和生命周期管理。
    /// 通用 AssetBundle 加载已委托给 <see cref="AssetBundleManager"/>。
    /// 
    /// 使用方式：
    /// 1. 在 Unity 编辑器中制作特效，导出为 AssetBundle（.ab 文件）
    /// 2. 将 .ab 文件放置到 Mod 的 Effects/ 目录下
    /// 3. 在技能编辑器中选择 "AssetBundle特效" 类型，指定路径和特效名称
    /// 
    /// 约定：
    /// - AB 包路径相对于 Mod 根目录，如 "Effects/fire_burst"
    /// - 特效名称为 AB 包内资源的完整名称
    /// </summary>
    public static class VfxAssetBundleLoader
    {
        // 已加载的 GameObject 缓存（bundlePath|effectName → GameObject）
        private static readonly Dictionary<string, GameObject> loadedPrefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        // 加载失败的记录（避免重复警告）
        private static readonly HashSet<string> loadFailureWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 运行时活跃的特效实例（用于生命周期管理）
        private static readonly List<VfxAssetBundleInstance> activeInstances = new List<VfxAssetBundleInstance>();

        // 最大同时活跃特效数
        private const int MaxActiveInstances = 64;

        // CompAbilityEffect_Modular.CompTick() 可能在同一游戏 Tick 内被多个技能实例重复调用。
        // VFX 生命周期更新是全局状态，限制为每游戏 Tick 仅处理一次，避免按技能实例数重复遍历活跃特效。
        private static int lastProcessedGameTick = -1;

        /// <summary>
        /// 加载 AssetBundle 并提取指定名称的 GameObject Prefab
        /// </summary>
        /// <param name="relativeBundlePath">AB包相对路径</param>
        /// <param name="effectName">包内特效资源名称</param>
        /// <returns>加载的 GameObject，失败返回 null</returns>
        public static GameObject? LoadEffectPrefab(string relativeBundlePath, string effectName)
        {
            if (string.IsNullOrWhiteSpace(relativeBundlePath) || string.IsNullOrWhiteSpace(effectName))
                return null;

            string cacheKey = $"{relativeBundlePath}|{effectName}";

            // 检查预制体缓存
            if (loadedPrefabs.TryGetValue(cacheKey, out GameObject? cachedPrefab))
            {
                if (cachedPrefab != null)
                    return cachedPrefab;
                loadedPrefabs.Remove(cacheKey);
            }

            // 委托给 AssetBundleManager 加载
            GameObject? prefab = AssetBundleManager.LoadAsset<GameObject>(relativeBundlePath, effectName);
            if (prefab == null)
            {
                LogWarningOnce($"EffectNotFound:{cacheKey}",
                    $"[CharacterStudio] AssetBundle '{relativeBundlePath}' 中未找到特效资源: {effectName}");
                return null;
            }

            loadedPrefabs[cacheKey] = prefab;
            return prefab;
        }

        public static string[]? EnumerateBundleAssets(string relativeBundlePath)
        {
            return AssetBundleManager.EnumerateAssets(relativeBundlePath);
        }

        public static string[]? EnumerateEffectNames(string relativeBundlePath)
        {
            string[]? assetNames = AssetBundleManager.EnumerateAssets(relativeBundlePath);
            if (assetNames == null)
                return null;

            return assetNames
                .Where(static name => name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Select(static name => Path.GetFileNameWithoutExtension(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[]? EnumerateTextureNames(string relativeBundlePath)
        {
            string[]? assetNames = AssetBundleManager.EnumerateAssets(relativeBundlePath);
            if (assetNames == null)
                return null;

            return assetNames
                .Where(static name => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".psd", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".tif", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
                .Select(static name => Path.GetFileNameWithoutExtension(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static Texture2D? LoadTexture(string relativeBundlePath, string textureName)
        {
            return AssetBundleManager.LoadTexture(relativeBundlePath, textureName);
        }

        public static T? LoadAsset<T>(string relativeBundlePath, string assetName) where T : UnityEngine.Object
        {
            return AssetBundleManager.LoadAsset<T>(relativeBundlePath, assetName);
        }

        /// <summary>
        /// 在指定位置播放 AB 包特效
        /// </summary>
        /// <param name="config">特效配置</param>
        /// <param name="position">世界坐标位置</param>
        /// <param name="map">所在地图</param>
        /// <returns>特效实例，失败返回 null</returns>
        public static VfxAssetBundleInstance? PlayEffect(AbilityVisualEffectConfig config, Vector3 position, Map map)
        {
            if (map == null)
                return null;

            GameObject? prefab = null;
            if (config.bundleRenderStrategy != VfxBundleRenderStrategy.CustomMote)
            {
                prefab = LoadEffectPrefab(config.assetBundlePath, config.assetBundleEffectName);
            }

            if (config.bundleRenderStrategy != VfxBundleRenderStrategy.CustomMote && prefab == null)
                return null;

            // 限制同时活跃数量
            CleanupExpiredInstances();
            if (activeInstances.Count >= MaxActiveInstances)
            {
                // 强制销毁最早的实例
                VfxAssetBundleInstance oldest = activeInstances[0];
                oldest.ForceDestroy();
                activeInstances.RemoveAt(0);
            }

            try
            {
                // 由于 RimWorld 使用自定义渲染管线，我们不能直接 Instantiate Unity GameObject
                // 而是创建一个包装器 Thing 来管理特效生命周期
                var instance = new VfxAssetBundleInstance(prefab, config, position, map);
                activeInstances.Add(instance);
                return instance;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 播放 AB 特效异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 每帧更新活跃特效
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

            for (int i = activeInstances.Count - 1; i >= 0; i--)
            {
                VfxAssetBundleInstance instance = activeInstances[i];
                if (instance.IsExpired)
                {
                    instance.ForceDestroy();
                    activeInstances.RemoveAt(i);
                }
                else
                {
                    instance.Update();
                }
            }
        }

        /// <summary>
        /// 清理已过期的特效实例
        /// </summary>
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
        /// 卸载所有 AssetBundle（场景切换/游戏退出时调用）
        /// </summary>
        public static void UnloadAll()
        {
            lastProcessedGameTick = -1;

            foreach (var instance in activeInstances)
            {
                instance.ForceDestroy();
            }
            activeInstances.Clear();

            // 委托通用卸载到 AssetBundleManager
            AssetBundleManager.UnloadAll();

            loadedPrefabs.Clear();
            VfxAssetBundleInstance.ClearCachedDefs();
            loadFailureWarnings.Clear();
        }

        private static void LogWarningOnce(string key, string message)
        {
            if (loadFailureWarnings.Add(key))
            {
                Log.Warning(message);
            }
        }
    }

    /// <summary>
    /// AB包特效运行时实例
    /// 由于 RimWorld 的 2D 俯视渲染管线限制，
    /// 实际的 AB 包预制体渲染通过 Mote 机制桥接。
    /// 如果 AB 包内包含粒子系统，会尝试在屏幕空间模拟渲染。
    /// </summary>
    public class VfxAssetBundleInstance
    {
        private static readonly Dictionary<string, ThingDef> customTextureMoteDefs = new Dictionary<string, ThingDef>(StringComparer.OrdinalIgnoreCase);

        public GameObject? RuntimeObject { get; private set; }
        public Vector3 Position { get; }
        public Map Map { get; }
        public int StartTick { get; }
        public int DurationTicks { get; }
        public float Scale { get; }
        public bool IsExpired => Find.TickManager?.TicksGame - StartTick >= DurationTicks;

        private readonly AbilityVisualEffectConfig config;
        private Thing? spawnedMote;

        public VfxAssetBundleInstance(GameObject? prefab, AbilityVisualEffectConfig config, Vector3 position, Map map)
        {
            this.config = config;
            Position = position;
            Map = map;
            StartTick = Find.TickManager?.TicksGame ?? 0;
            DurationTicks = Mathf.Max(1, config.displayDurationTicks);
            Scale = Mathf.Max(0.1f, config.assetBundleEffectScale);

            TryCreateRuntimeEffect(prefab);
        }

        private void TryCreateRuntimeEffect(GameObject? prefab)
        {
            try
            {
                if (config.bundleRenderStrategy == VfxBundleRenderStrategy.CustomMote)
                {
                    TryCreateCustomMoteEffect();
                    return;
                }

                if (prefab == null)
                {
                    return;
                }

                // 尝试从 AB 预制体提取粒子系统组件用于渲染
                // RimWorld 环境下直接 Instantiate 可能不显示，
                // 因此我们采用桥接策略：创建一个 Mote 作为视觉锚点，
                // 然后附加 AB 中的粒子系统
                var particleSystem = prefab.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    // 创建包装 Mote 作为位置锚点
                    spawnedMote = CreateBridgeMote();
                    if (spawnedMote != null)
                    {
                        // 在 Mote 位置实例化粒子特效
                        RuntimeObject = UnityEngine.Object.Instantiate(prefab);
                        RuntimeObject.transform.position = Position;
                        RuntimeObject.transform.localScale = Vector3.one * Scale;

                        var ps = RuntimeObject.GetComponent<ParticleSystem>();
                        if (ps != null)
                        {
                            var main = ps.main;
                            main.simulationSpace = ParticleSystemSimulationSpace.World;
                            var lifetime = Mathf.Max(0.1f, DurationTicks / 60f);
                            main.duration = lifetime;
                            main.startLifetime = lifetime;
                            ps.Play();
                        }
                    }
                }
                else
                {
                    // 非 ParticleSystem 类型，尝试直接实例化
                    spawnedMote = CreateBridgeMote();
                    RuntimeObject = UnityEngine.Object.Instantiate(prefab);
                    RuntimeObject.transform.position = Position;
                    RuntimeObject.transform.localScale = Vector3.one * Scale;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 创建 AB 特效运行时实例异常: {ex.Message}");
            }
        }

        private void TryCreateCustomMoteEffect()
        {
            if (string.IsNullOrWhiteSpace(config.assetBundlePath) || string.IsNullOrWhiteSpace(config.assetBundleTextureName))
            {
                return;
            }

            Texture2D? bundleTexture = AssetBundleManager.LoadTexture(config.assetBundlePath, config.assetBundleTextureName);
            if (bundleTexture == null)
            {
                return;
            }

            ThingDef customMoteDef = GetOrCreateCustomTextureMoteDef(bundleTexture, config.assetBundleTextureName, Mathf.Max(0.1f, Scale), DurationTicks);
            MoteThrown? mote = ThingMaker.MakeThing(customMoteDef) as MoteThrown;
            if (mote == null)
            {
                return;
            }

            mote.exactPosition = Position;
            mote.exactRotation = config.rotation;
            mote.rotationRate = 0f;
            mote.instanceColor = Color.white;
            mote.linearScale = new Vector3(Scale, 1f, Scale);
            mote.SetVelocity(0f, 0f);
            GenSpawn.Spawn(mote, Position.ToIntVec3(), Map, WipeMode.Vanish);
            spawnedMote = mote;
        }

        private Thing? CreateBridgeMote()
        {
            try
            {
                // 创建一个简单的 Mote 作为位置锚点和生命周期管理器
                ThingDef moteDef = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_AirPuff");
                if (moteDef == null)
                    return null;

                var mote = ThingMaker.MakeThing(moteDef) as Mote;
                if (mote == null)
                    return null;

                mote.exactPosition = Position;
                mote.linearScale = Vector3.one * Scale;
                GenSpawn.Spawn(mote, Position.ToIntVec3(), Map, WipeMode.Vanish);
                return mote;
            }
            catch
            {
                return null;
            }
        }

        private static ThingDef GetOrCreateCustomTextureMoteDef(Texture2D texture, string textureName, float drawSize, int displayDurationTicks)
        {
            string key = $"{texture.GetInstanceID()}|{drawSize:F3}|{displayDurationTicks}";
            if (customTextureMoteDefs.TryGetValue(key, out ThingDef cachedDef))
            {
                return cachedDef;
            }

            string runtimeTexturePath = WriteTextureToRuntimeCache(texture, textureName);

            var def = new ThingDef
            {
                defName = $"CS_RuntimeBundleTextureVfx_{Math.Abs(key.GetHashCode()):X8}",
                label = "runtime bundle texture vfx mote",
                thingClass = typeof(MoteThrown),
                category = ThingCategory.Mote,
                altitudeLayer = AltitudeLayer.MoteOverhead,
                drawerType = DrawerType.RealtimeOnly,
                useHitPoints = false,
                drawGUIOverlay = false,
                tickerType = TickerType.Normal,
                mote = new MoteProperties
                {
                    realTime = true,
                    fadeInTime = 0f,
                    solidTime = Mathf.Max(1, displayDurationTicks) / 60f,
                    fadeOutTime = 0.2f,
                    needsMaintenance = false,
                    collide = false,
                    speedPerTime = 0f,
                    growthRate = 0f
                },
                graphicData = new GraphicData
                {
                    texPath = runtimeTexturePath,
                    graphicClass = typeof(Graphic_Runtime),
                    shaderType = ShaderTypeDefOf.Transparent,
                    drawSize = new Vector2(drawSize, drawSize),
                    color = Color.white,
                    colorTwo = Color.white
                }
            };

            customTextureMoteDefs[key] = def;
            return def;
        }

        private static string WriteTextureToRuntimeCache(Texture2D texture, string textureName)
        {
            string safeName = string.IsNullOrWhiteSpace(textureName)
                ? $"bundle_tex_{Math.Abs(texture.GetInstanceID())}"
                : string.Concat(textureName.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = $"bundle_tex_{Math.Abs(texture.GetInstanceID())}";
            }

            string cacheDir = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "RuntimeVfxCache");
            Directory.CreateDirectory(cacheDir);

            string fullPath = Path.Combine(cacheDir, $"{safeName}_{Math.Abs(texture.GetInstanceID()):X8}.png");
            if (!File.Exists(fullPath))
            {
                byte[] pngBytes = texture.EncodeToPNG();
                File.WriteAllBytes(fullPath, pngBytes);
            }

            return fullPath;
        }

        public void Update()
        {
            if (RuntimeObject == null)
                return;

            try
            {
                // 更新位置（如果需要附加到目标）
                if (config.attachToPawn)
                {
                    // 跟随逻辑在 CompAbilityEffect_Modular 中处理
                }

                // 同步位置
                RuntimeObject.transform.position = Position;
            }
            catch { /* 忽略更新异常 */ }
        }

        public void ForceDestroy()
        {
            try
            {
                if (RuntimeObject != null)
                {
                    UnityEngine.Object.Destroy(RuntimeObject);
                    RuntimeObject = null;
                }

                if (spawnedMote != null && !spawnedMote.Destroyed)
                {
                    spawnedMote.Destroy();
                    spawnedMote = null;
                }
            }
            catch { /* 忽略销毁异常 */ }
        }

        public static void ClearCachedDefs()
        {
            customTextureMoteDefs.Clear();
        }
    }
}
