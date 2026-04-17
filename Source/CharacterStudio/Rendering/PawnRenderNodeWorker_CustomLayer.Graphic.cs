using System;
using RimWorld;
using UnityEngine;
using Verse;
using CharacterStudio.Core;
using CharacterStudio.Abilities;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CharacterStudio.Rendering
{
    /// <summary>
    /// 自定义图层渲染工作器 — Graphic 获取、Shader 解析、材质属性、透明 ZWrite 修正、纹理缓存
    /// </summary>
    public partial class PawnRenderNodeWorker_CustomLayer : PawnRenderNodeWorker
    {
        /// <summary>
        /// 检查纹理是否存在
        /// P-PERF: 淘汰策略改为 O(1) 移除最旧条目（FIFO），避免分配数组。
        /// </summary>
        private bool TextureExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (textureExistsCache.TryGetValue(path, out bool cachedExists))
                return cachedExists;

            bool exists;
            if (System.IO.Path.IsPathRooted(path) || path.StartsWith("/"))
            {
                exists = RuntimeAssetLoader.ExternalTextureExists(path, out _);
            }
            else if (!RuntimeAssetLoader.IsMainThread())
            {
                exists = true;
            }
            else if (ContentFinder<Texture2D>.Get(path, false) != null)
            {
                exists = true;
            }
            else
            {
                exists = ContentFinder<Texture2D>.Get(path + "_north", false) != null;
            }

            // P-PERF: Queue-based FIFO 淘汰，确保可靠的插入顺序淘汰
            if (textureExistsCache.Count >= MaxTextureExistsCacheSize && textureExistsEvictionQueue.Count > 0)
            {
                textureExistsCache.Remove(textureExistsEvictionQueue.Dequeue());
            }

            textureExistsEvictionQueue.Enqueue(path);
            textureExistsCache[path] = exists;
            return exists;
        }

        private static void LogMissingExternalTextureWarningOnce(string path)
        {
            string key = path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (missingExternalTextureWarnings)
            {
                if (!missingExternalTextureWarnings.Add(key))
                {
                    return;
                }
            }

            Log.Warning($"[CharacterStudio] 外部纹理缺失，已回退透明占位: {path}");
        }

        /// <summary>
        /// 获取图形
        /// </summary>
        protected override Graphic? GetGraphic(PawnRenderNode node, PawnDrawParms parms)
        {
            bool hasCustomConfig = node is PawnRenderNode_Custom customNodeWithConfig && customNodeWithConfig.config != null;
            if (string.IsNullOrEmpty(node.Props?.texPath) && !hasCustomConfig)
                return null;

            // P-PERF: 设置线程本地 CompPawnSkin 缓存，供 Variant/FaceTexture 复用
            _lastTextureResolveCachedSkinComp = (node is PawnRenderNode_Custom cn) ? cn.GetCachedSkinComp() : parms.pawn?.TryGetComp<CompPawnSkin>();

            // P8: 节点级图形缓存命中检查
            if (node is PawnRenderNode_Custom cacheNode && cacheNode.config != null)
            {
                int currentTick = (Current.ProgramState == ProgramState.Playing)
                    ? (Find.TickManager?.TicksGame ?? 0)
                    : (int)(Time.realtimeSinceStartup * 60f);
                int facingInt = parms.facing.AsInt;

                if (cacheNode._cachedGraphicHasResult
                    && cacheNode._cachedGraphicTick == currentTick
                    && cacheNode._cachedGraphicFacing == facingInt)
                {
                    return cacheNode._cachedGraphicResult;
                }
            }

            string texPath = node.Props?.texPath ?? "";
            string resolvedTexPath = texPath;
            bool matchedVariant = false;
            bool attemptedVariant = false;

            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                PawnLayerConfig config = customNode.config;

                if (ShouldSuppressEmotionOverlayRendering(customNode, parms.pawn))
                    return null;

                string? layeredBasePath = ResolveLayeredFacePartBasePath(customNode, parms.pawn, parms.facing);
                if (customNode.layeredFacePartType.HasValue
                    && parms.facing == Rot4.North
                    && string.IsNullOrWhiteSpace(layeredBasePath))
                {
                    return null;
                }

                string? configuredPath = ResolveConfiguredTexPath(
                    config,
                    parms.pawn,
                    parms.facing,
                    out matchedVariant,
                    out attemptedVariant,
                    layeredBasePath);

                if (string.IsNullOrEmpty(configuredPath))
                    return null;

                resolvedTexPath = configuredPath!;

                if (!UsesUnifiedVariantLogic(config))
                {
                    resolvedTexPath = ResolveExpressionVariant(resolvedTexPath, parms.pawn, customNode.GetCachedSkinComp());
                }

                if (attemptedVariant && !matchedVariant && config.hideWhenMissingVariant)
                    return null;
            }
            else
            {
                string directionalTexPath = ResolveDirectionalVariant(texPath, parms.facing);
                resolvedTexPath = ResolveExpressionVariant(directionalTexPath, parms.pawn);
            }

            Shader shader = ResolveShader(node, parms.pawn!);

            Graphic? resultGraphic = null;

            bool looksLikeExternal = resolvedTexPath.Contains(":")
                || resolvedTexPath.StartsWith("/")
                || System.IO.Path.IsPathRooted(resolvedTexPath);

            if (looksLikeExternal)
            {
                bool externalExists = RuntimeAssetLoader.ExternalTextureExists(resolvedTexPath, out string resolvedExternalPath);
                if (!externalExists)
                {
                    LogMissingExternalTextureWarningOnce(resolvedTexPath);
                }

                var props = node.Props;

                var req = new GraphicRequest(
                    typeof(Graphic_Runtime),
                    externalExists ? resolvedExternalPath : resolvedTexPath,
                    shader,
                    Vector2.one,
                    props?.color ?? Color.white,
                    Color.white, null, 0, null, null
                );

                string cachePath = externalExists ? resolvedExternalPath : resolvedTexPath;
                string cacheKey = BuildExternalGraphicCacheKey(cachePath, shader, props?.color ?? Color.white);
                if (externalGraphicCache.TryGetValue(cacheKey, out Graphic cachedGraphic))
                {
                    resultGraphic = cachedGraphic;
                }
                else
                {
                    // P-PERF: Queue-based FIFO 淘汰，确保可靠的插入顺序淘汰
                    if (externalGraphicCache.Count >= MaxExternalGraphicCacheSize && externalGraphicEvictionQueue.Count > 0)
                    {
                        string evictedKey = externalGraphicEvictionQueue.Dequeue();
                        if (externalGraphicCache.TryGetValue(evictedKey, out Graphic evictedGraphic))
                        {
                            externalGraphicCache.Remove(evictedKey);
                            if (evictedGraphic is Graphic_Runtime runtimeEvicted)
                            {
                                GraphicRuntimePool.Return(runtimeEvicted);
                            }
                        }
                    }

                    var graphic = GraphicRuntimePool.Get();
                    graphic.Init(req);
                    if (graphic.IsInitializedSuccessfully)
                    {
                        externalGraphicEvictionQueue.Enqueue(cacheKey);
                        externalGraphicCache[cacheKey] = graphic;
                        resultGraphic = graphic;
                    }
                    else
                    {
                        resultGraphic = graphic;
                    }
                }
            }
            else if (node is PawnRenderNode_Custom customNode2)
            {
                var config = customNode2.config;
                if (config == null)
                {
                    resultGraphic = base.GetGraphic(node, parms);
                }
                else
                {
                    Type? graphicType = config.graphicClass;

                    if (graphicType != typeof(Graphic_Runtime))
                    {
                        if (graphicType == null || (graphicType != typeof(Graphic_Multi) && graphicType != typeof(Graphic_Single)))
                        {
                            if (graphicTypeProbeCache.TryGetValue(resolvedTexPath, out var cachedGraphicType))
                            {
                                graphicType = cachedGraphicType;
                            }
                            else if (!RuntimeAssetLoader.IsMainThread())
                            {
                                graphicType = typeof(Graphic_Multi);
                            }
                            else if (ContentFinder<Texture2D>.Get(resolvedTexPath + "_north", false) != null)
                            {
                                graphicType = typeof(Graphic_Multi);
                                // P-PERF: Queue-based FIFO 淘汰，确保可靠的插入顺序淘汰
                                if (graphicTypeProbeCache.Count >= MaxGraphicTypeProbeCacheSize && graphicTypeProbeEvictionQueue.Count > 0)
                                {
                                    graphicTypeProbeCache.Remove(graphicTypeProbeEvictionQueue.Dequeue());
                                }
                                graphicTypeProbeEvictionQueue.Enqueue(resolvedTexPath);
                                graphicTypeProbeCache[resolvedTexPath] = graphicType;
                            }
                            else if (ContentFinder<Texture2D>.Get(resolvedTexPath, false) != null)
                            {
                                graphicType = typeof(Graphic_Single);
                                if (graphicTypeProbeCache.Count >= MaxGraphicTypeProbeCacheSize && graphicTypeProbeEvictionQueue.Count > 0)
                                {
                                    graphicTypeProbeCache.Remove(graphicTypeProbeEvictionQueue.Dequeue());
                                }
                                graphicTypeProbeEvictionQueue.Enqueue(resolvedTexPath);
                                graphicTypeProbeCache[resolvedTexPath] = graphicType;
                            }

                            if (graphicType == null) graphicType = typeof(Graphic_Multi);
                        }

                        Vector2 drawSize = Vector2.one;
                        Color color = GetColorFromSource(config.colorSource, parms.pawn, config.customColor);
                        Color colorTwo = GetColorFromSource(config.colorTwoSource, parms.pawn, config.customColorTwo);

                        if (graphicType == typeof(Graphic_Multi))
                        {
                            resultGraphic = GraphicDatabase.Get<Graphic_Multi>(resolvedTexPath, shader, drawSize, color, colorTwo);
                        }
                        else if (graphicType == typeof(Graphic_Single))
                        {
                            resultGraphic = GraphicDatabase.Get<Graphic_Single>(resolvedTexPath, shader, drawSize, color);
                        }
                        else
                        {
                            // P-PERF: 缓存泛型 MethodInfo，避免每帧 MakeGenericMethod + new object[] 分配
                            if (!_cachedGraphicDbMethods.TryGetValue(graphicType!, out System.Reflection.MethodInfo? cachedGeneric))
                            {
                                cachedGeneric = _graphicDbGetTwoColors?.MakeGenericMethod(graphicType!);
                                _cachedGraphicDbMethods[graphicType!] = cachedGeneric;
                            }

                            if (cachedGeneric != null)
                            {
                                resultGraphic = (Graphic)cachedGeneric.Invoke(null, new object[] {
                                    resolvedTexPath,
                                    shader,
                                    drawSize,
                                    color,
                                    colorTwo
                                });
                            }
                            else
                            {
                                Log.Warning($"[CharacterStudio] 无法找到匹配的 GraphicDatabase.Get 方法用于类型 {graphicType}");
                            }
                        }
                    }
                }
            }

            if (resultGraphic == null && resolvedTexPath != texPath)
            {
                var props = node.Props;
                resultGraphic = GraphicDatabase.Get<Graphic_Single>(
                    resolvedTexPath,
                    shader,
                    Vector2.one,
                    props?.color ?? Color.white
                );
            }

            if (resultGraphic == null)
            {
                resultGraphic = base.GetGraphic(node, parms);
            }

            // 透明 shader 深度写入修正
            if (resultGraphic != null && node is PawnRenderNode_Custom zNode && zNode.config != null)
            {
                string? shaderDef = zNode.config.shaderDefName;
                if (shaderDef == "Custom" || IsAlphaBlendShader(shaderDef))
                {
                    resultGraphic = ApplyTransparentZWrite(resultGraphic);
                }
            }

            // P8: 写入节点级图形缓存
            if (node is PawnRenderNode_Custom cacheWriteNode && cacheWriteNode.config != null)
            {
                int writeTick = (Current.ProgramState == ProgramState.Playing)
                    ? (Find.TickManager?.TicksGame ?? 0)
                    : (int)(Time.realtimeSinceStartup * 60f);
                cacheWriteNode._cachedGraphicResult = resultGraphic;
                cacheWriteNode._cachedGraphicTick = writeTick;
                cacheWriteNode._cachedGraphicFacing = parms.facing.AsInt;
                cacheWriteNode._cachedGraphicHasResult = true;
            }

            return resultGraphic;
        }

        // P-PERF: 缓存反射 FieldInfo，避免每次调用都做反射查找
        private static readonly System.Reflection.FieldInfo? _graphicMatField =
            typeof(Graphic).GetField("mat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo? _graphicMultiMatsField =
            typeof(Graphic_Multi).GetField("mats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // P-PERF: 记录已应用 ZWrite 的 Graphic 实例 ID，避免对同一 Graphic 重复创建 Material
        private static readonly HashSet<int> _zWriteAppliedInstanceIds = new HashSet<int>();

        // P-PERF: 全局材质池，用于在不同 Graphic 间共享开启了 ZWrite 的材质，Promote Batching
        private static readonly Dictionary<int, Material> _sharedZWriteMaterials = new Dictionary<int, Material>();

        /// <summary>
        /// 对透明/Custom 图形的材质强制开启 ZWrite，保持原始 renderQueue 不变。
        /// 透明图层仍在 3000+ 队列绘制（alpha blending 正常），ZWrite=On 确保深度缓冲正确遮挡地面阴影。
        /// </summary>
        private Graphic ApplyTransparentZWrite(Graphic baseGraphic)
        {
            try
            {
                // P-PERF: 已应用过则直接跳过（Graphic 非 UnityObject，用 GetHashCode）
                int instanceId = RuntimeHelpers.GetHashCode(baseGraphic);
                if (_zWriteAppliedInstanceIds.Contains(instanceId))
                    return baseGraphic;

                if (baseGraphic is Graphic_Single single)
                {
                    Material? mat = single.MatSingle;
                    if (mat != null)
                    {
                        if (mat.GetInt("_ZWrite") == 1)
                        {
                            _zWriteAppliedInstanceIds.Add(instanceId);
                        }
                        else
                        {
                            Material zWriteMat = GetSharedZWriteMaterial(mat);
                            _graphicMatField?.SetValue(single, zWriteMat);
                            _zWriteAppliedInstanceIds.Add(instanceId);
                        }
                    }
                }
                else if (baseGraphic is Graphic_Multi multi)
                {
                    if (_graphicMultiMatsField?.GetValue(multi) is Material[] existingMats)
                    {
                        bool alreadyZWrite = true;
                        for (int i = 0; i < existingMats.Length; i++)
                        {
                            if (existingMats[i] != null && existingMats[i].GetInt("_ZWrite") == 0)
                            {
                                alreadyZWrite = false;
                                break;
                            }
                        }

                        if (alreadyZWrite)
                        {
                            _zWriteAppliedInstanceIds.Add(instanceId);
                        }
                        else
                        {
                            Material[] newMats = new Material[existingMats.Length];
                            for (int i = 0; i < existingMats.Length; i++)
                            {
                                if (existingMats[i] != null)
                                {
                                    newMats[i] = GetSharedZWriteMaterial(existingMats[i]);
                                }
                            }
                            _graphicMultiMatsField.SetValue(multi, newMats);
                            _zWriteAppliedInstanceIds.Add(instanceId);
                        }
                    }
                }
                return baseGraphic;
            }
            catch (Exception ex)
            {
                Log.WarningOnce($"[CharacterStudio] Failed to apply ZWrite to graphic: {ex.Message}", baseGraphic.path?.GetHashCode() ?? 0);
                return baseGraphic;
            }
        }

        private Material GetSharedZWriteMaterial(Material baseMat)
        {
            // P-PERF: 基于原始材质 ID 获取共享的 ZWrite 版本，极大提升合批概率
            int baseMatId = baseMat.GetInstanceID();
            if (_sharedZWriteMaterials.TryGetValue(baseMatId, out Material shared))
                return shared;

            Material zWriteMat = new Material(baseMat);
            zWriteMat.SetInt("_ZWrite", 1);
            // 保持原始 renderQueue，仅开启 ZWrite
            _sharedZWriteMaterials[baseMatId] = zWriteMat;
            return zWriteMat;
        }

        // P-PERF: 缓存 LoadShader / Shader.Find 结果，避免每帧重复查找
        private static Shader? _cachedTransparentPostLight;
        private static Shader? _cachedItemTransparent;
        private static readonly Dictionary<string, Shader?> customShaderCache = new Dictionary<string, Shader?>(StringComparer.Ordinal);

        // P-PERF: 缓存 GraphicDatabase.Get 的泛型 MethodInfo，避免每帧 MakeGenericMethod + new object[] 分配
        private static readonly Dictionary<Type, System.Reflection.MethodInfo?> _cachedGraphicDbMethods
            = new Dictionary<Type, System.Reflection.MethodInfo?>();
        private static readonly System.Reflection.MethodInfo _graphicDbGetTwoColors
            = typeof(GraphicDatabase).GetMethod("Get", new Type[] { typeof(string), typeof(Shader), typeof(Vector2), typeof(Color), typeof(Color) })!;
        private static readonly System.Reflection.MethodInfo _graphicDbGetOneColor
            = typeof(GraphicDatabase).GetMethod("Get", new Type[] { typeof(string), typeof(Shader), typeof(Vector2), typeof(Color) })!;

        // P-PERF: 复用 StringBuilder 避免每次 BuildExternalGraphicCacheKey 分配
        [ThreadStatic]
        private static System.Text.StringBuilder? _cacheKeyBuilder;

        private static string BuildExternalGraphicCacheKey(string path, Shader shader, Color color)
        {
            // P-PERF: 使用预分配 StringBuilder，避免 string.Concat 的多次中间字符串分配
            var sb = _cacheKeyBuilder ??= new System.Text.StringBuilder(256);
            sb.Length = 0;
            sb.Append(path);
            sb.Append('|');
            sb.Append(shader?.name ?? string.Empty);
            sb.Append('|');
            sb.Append(color.r.ToString("F3"));
            sb.Append(',');
            sb.Append(color.g.ToString("F3"));
            sb.Append(',');
            sb.Append(color.b.ToString("F3"));
            sb.Append(',');
            sb.Append(color.a.ToString("F3"));
            return sb.ToString();
        }

        public static void ClearExternalGraphicCache()
        {
            foreach (var graphic in externalGraphicCache.Values)
            {
                if (graphic is Graphic_Runtime runtime)
                {
                    GraphicRuntimePool.Return(runtime);
                }
            }
            externalGraphicCache.Clear();
            externalGraphicEvictionQueue.Clear();
        }

        /// <summary>
        /// 解析着色器
        /// P-PERF: 缓存 LoadShader / Shader.Find 结果，避免每帧重复 Unity 资源查找
        /// </summary>
        private Shader ResolveShader(PawnRenderNode node, Pawn pawn)
        {
            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                string shaderName = customNode.config.shaderDefName;
                if (!string.IsNullOrEmpty(shaderName))
                {
                    switch (shaderName)
                    {
                        case "Cutout": return ShaderDatabase.Cutout;
                        case "CutoutComplex": return ShaderDatabase.CutoutComplex;
                        case "Transparent":
                        case "TransparentZWrite":
                            return ShaderDatabase.Transparent;
                        case "TransparentPostLight":
                            return _cachedTransparentPostLight ??= ShaderDatabase.LoadShader("TransparentPostLight");
                        case "ItemTransparent":
                            return _cachedItemTransparent ??= ShaderDatabase.LoadShader("ItemTransparent");
                        case "MetaOverlay": return ShaderDatabase.MetaOverlay;
                        case "Custom":
                        {
                            string customPath = customNode.config.customShaderPath;
                            if (!string.IsNullOrWhiteSpace(customPath))
                            {
                                if (!customShaderCache.TryGetValue(customPath, out Shader? customShader))
                                {
                                    customShader = Shader.Find(customPath);
                                    customShaderCache[customPath] = customShader;
                                    if (customShader == null)
                                        Log.WarningOnce($"[CharacterStudio] Custom shader not found: {customPath}, falling back to CutoutComplex", customPath.GetHashCode());
                                }
                                if (customShader != null)
                                    return customShader;
                            }
                            return ShaderDatabase.CutoutComplex;
                        }
                    }
                }
            }
            return node.ShaderFor(pawn) ?? ShaderDatabase.Cutout;
        }

        private static bool IsAlphaBlendShader(string? shaderDefName)
        {
            return shaderDefName == "Transparent"
                || shaderDefName == "TransparentPostLight"
                || shaderDefName == "TransparentZWrite"
                || shaderDefName == "ItemTransparent";
        }

        private static float AlphaToCutoff(float alpha)
        {
            return Mathf.Lerp(0.01f, 0.5f, Mathf.Clamp01(alpha));
        }

        /// <summary>
        /// 获取指定源的颜色
        /// </summary>
        private Color GetColorFromSource(LayerColorSource source, Pawn? pawn, Color fixedColor)
        {
            switch (source)
            {
                case LayerColorSource.PawnHair:
                    return pawn?.story?.HairColor ?? Color.white;
                case LayerColorSource.PawnSkin:
                    return pawn?.story?.SkinColor ?? Color.white;
                case LayerColorSource.PawnApparelPrimary:
                    {
                        var worn = pawn?.apparel?.WornApparel;
                        if (worn != null && worn.Count > 0)
                            return worn[0].DrawColor;
                        return Color.white;
                    }
                case LayerColorSource.PawnApparelSecondary:
                    return Color.white;
                case LayerColorSource.Fixed:
                default:
                    return fixedColor;
            }
        }

        /// <summary>
        /// 覆盖 GetMaterialPropertyBlock 以应用自定义颜色
        /// </summary>
        public override MaterialPropertyBlock GetMaterialPropertyBlock(
            PawnRenderNode node,
            Material material,
            PawnDrawParms parms)
        {
            MaterialPropertyBlock matPropBlock = node.MatPropBlock;

            if (node is PawnRenderNode_Custom customNode && customNode.config != null)
            {
                var config = customNode.config;
                var pawn = parms.pawn;

                EnsureProgrammaticFaceStateUpdated(customNode, pawn);
                float programmaticAlpha = customNode.layeredFacePartType.HasValue
                    ? Mathf.Clamp01(customNode.currentProgrammaticAlpha)
                    : 1f;

                Color colorOne = GetColorFromSource(config.colorSource, pawn, config.customColor);
                Color colorTwo = GetColorFromSource(config.colorTwoSource, pawn, config.customColorTwo);

                float globalAlpha = Mathf.Clamp01(config.alpha) * programmaticAlpha;

                if (parms.Statue)
                {
                    Color statueColor = parms.statueColor ?? Color.white;
                    statueColor.a *= globalAlpha;
                    matPropBlock.SetColor(ShaderPropertyIDs.Color, statueColor);
                }
                else
                {
                    Color finalColor = parms.tint * colorOne;
                    finalColor.a *= globalAlpha;

                    Color finalColorTwo = colorTwo;
                    finalColorTwo.a *= globalAlpha;

                    matPropBlock.SetColor(ShaderPropertyIDs.Color, finalColor);
                    matPropBlock.SetColor(CachedColorTwoID, finalColorTwo);
                }

                matPropBlock.SetVector(CachedMainTexSTID, new Vector4(1f, 1f, 0f, 0f));
            }
            else
            {
                base.GetMaterialPropertyBlock(node, material, parms);
            }

            return matPropBlock;
        }

        public static void ClearCache()
        {
            ClearExternalGraphicCache();
            textureExistsCache.Clear();
            textureExistsEvictionQueue.Clear();
            frameSequenceCountCache.Clear();
            graphicTypeProbeCache.Clear();
            graphicTypeProbeEvictionQueue.Clear();
            // P-PERF: 清理 ZWrite 已处理记录，防止 HashSet 无界增长
            _zWriteAppliedInstanceIds.Clear();
            // P-PERF: 清理全局材质池
            _sharedZWriteMaterials.Clear();
        }
    }
}