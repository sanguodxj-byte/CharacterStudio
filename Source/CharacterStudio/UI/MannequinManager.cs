using System;
using System.Linq;
using RimWorld;
using CharacterStudio.Core;
using CharacterStudio.Rendering;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 人偶管理器
    /// 用于在编辑器中预览角色外观
    /// </summary>
    public class MannequinManager
    {
        // ─────────────────────────────────────────────
        // 状态
        // ─────────────────────────────────────────────
        private Pawn? mannequinPawn;
        
        /// <summary>
        /// 获取当前人偶 Pawn
        /// </summary>
        public Pawn? CurrentPawn => mannequinPawn;
        private ThingDef? currentRace;
        private bool isInitialized = false;
        private RenderTexture? previewTexture;

        // 缓存的渲染
        private Rot4 cachedRotation;
        private bool needsRefresh = true;

        // 热加载控制
        private float lastRefreshTime = 0f;
        private const float RefreshInterval = 1f; // 每秒检查一次

        // 文件修改时间追踪（用于精确热加载检测）
        private System.Collections.Generic.Dictionary<string, System.DateTime> fileModificationTimes =
            new System.Collections.Generic.Dictionary<string, System.DateTime>();

        // 表情模拟 (模拟 RenderTreeDef)
        public string currentExpression = "Neutral";
        public int currentVariant = 0;

        // ─────────────────────────────────────────────
        // 公共方法
        // ─────────────────────────────────────────────

        /// <summary>
        /// 初始化人偶
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            try
            {
                // 默认使用人类
                currentRace = ThingDefOf.Human;
                CreateMannequin();
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 初始化失败: {ex}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            DestroyMannequin();

            if (previewTexture != null)
            {
                previewTexture.Release();
                UnityEngine.Object.Destroy(previewTexture);
                previewTexture = null;
            }

            isInitialized = false;
        }

        /// <summary>
        /// 设置种族
        /// </summary>
        public void SetRace(ThingDef raceDef)
        {
            if (raceDef == null || currentRace == raceDef) return;

            currentRace = raceDef;
            DestroyMannequin();
            CreateMannequin();
            needsRefresh = true;
        }

        /// <summary>
        /// 应用皮肤定义
        /// </summary>
        public void ApplySkin(PawnSkinDef? skinDef)
        {
            if (mannequinPawn == null) return;

            try
            {
                // 获取或添加皮肤组件
                var skinComp = mannequinPawn.GetComp<CompPawnSkin>();
                if (skinComp == null)
                {
                    // 动态添加组件
                    skinComp = new CompPawnSkin();
                    skinComp.parent = mannequinPawn;
                    mannequinPawn.AllComps.Add(skinComp);
                    skinComp.Initialize(new CompProperties_PawnSkin());
                }

                // 应用皮肤
                skinComp.ActiveSkin = skinDef;
                
                // 刷新隐藏节点缓存（处理 hiddenPaths 变更）
                Patch_PawnRenderTree.RefreshHiddenNodes(mannequinPawn);
                
                // 刷新渲染 (强制 PortraitsCache 更新)
                mannequinPawn.Drawer.renderer.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(mannequinPawn);
                needsRefresh = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用皮肤失败: {ex}");
            }
        }

        /// <summary>
        /// 检查热加载
        /// </summary>
        public void Update()
        {
            if (Time.realtimeSinceStartup - lastRefreshTime > RefreshInterval)
            {
                lastRefreshTime = Time.realtimeSinceStartup;
                CheckForTextureChanges();
            }
        }

        private void CheckForTextureChanges()
        {
            if (mannequinPawn == null) return;
            var skinComp = mannequinPawn.GetComp<CompPawnSkin>();
            if (skinComp?.ActiveSkin == null) return;

            bool anyChanged = false;
            foreach (var layer in skinComp.ActiveSkin.layers)
            {
                if (string.IsNullOrEmpty(layer.texPath)) continue;
                
                // 检查文件是否存在
                if (!System.IO.File.Exists(layer.texPath)) continue;

                try
                {
                    // 获取文件的最后修改时间
                    var currentModTime = System.IO.File.GetLastWriteTime(layer.texPath);
                    
                    // 检查是否记录过此文件
                    if (fileModificationTimes.TryGetValue(layer.texPath, out var lastModTime))
                    {
                        // 只有当文件真正被修改时才更新
                        if (currentModTime > lastModTime)
                        {
                            // 文件已修改，重新加载纹理
                            RuntimeAssetLoader.RemoveFromCache(layer.texPath);
                            RuntimeAssetLoader.LoadTextureRaw(layer.texPath, true);
                            fileModificationTimes[layer.texPath] = currentModTime;
                            anyChanged = true;
                        }
                    }
                    else
                    {
                        // 首次记录此文件
                        fileModificationTimes[layer.texPath] = currentModTime;
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 检查文件修改时间失败: {layer.texPath}, {ex.Message}");
                }
            }

            if (anyChanged)
            {
                mannequinPawn.Drawer.renderer.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(mannequinPawn);
                needsRefresh = true;
                Log.Message("[CharacterStudio] 检测到纹理变化，已刷新预览");
            }
        }

        /// <summary>
        /// 绘制预览
        /// </summary>
        public void DrawPreview(Rect rect, Rot4 rotation, float zoom)
        {
            if (mannequinPawn == null)
            {
                DrawPlaceholder(rect, "人偶未初始化");
                return;
            }

            try
            {
                // 确保刷新
                if (needsRefresh || cachedRotation != rotation)
                {
                    PortraitsCache.SetDirty(mannequinPawn);
                    cachedRotation = rotation;
                    needsRefresh = false;
                }

                // 使用 RimWorld 的肖像渲染
                DrawPawnPortrait(rect, rotation, zoom);
            }
            catch (Exception ex)
            {
                DrawPlaceholder(rect, $"渲染错误: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // 私有方法
        // ─────────────────────────────────────────────

        private void CreateMannequin()
        {
            if (currentRace == null) return;

            try
            {
                // 查找与当前种族匹配的 PawnKindDef
                // 对于 HAR 种族，需要找到其对应的 PawnKindDef 而非使用 Colonist
                PawnKindDef kindDef = FindPawnKindForRace(currentRace);
                
                // 创建生成请求
                var request = new PawnGenerationRequest(
                    kind: kindDef,
                    faction: null,
                    context: PawnGenerationContext.NonPlayer,
                    tile: null,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: false,
                    colonistRelationChanceFactor: 0f,
                    forceAddFreeWarmLayerIfNeeded: false,
                    allowGay: true,
                    allowPregnant: false,
                    allowFood: false,
                    allowAddictions: false,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: false,
                    worldPawnFactionDoesntMatter: false,
                    biocodeWeaponChance: 0f,
                    biocodeApparelChance: 0f,
                    extraPawnForExtraRelationChance: null,
                    relationWithExtraPawnChanceFactor: 0f,
                    validatorPreGear: null,
                    validatorPostGear: null,
                    forcedTraits: null,
                    prohibitedTraits: null,
                    minChanceToRedressWorldPawn: null,
                    fixedBiologicalAge: null,
                    fixedChronologicalAge: null,
                    fixedGender: null,
                    fixedLastName: null,
                    fixedBirthName: null,
                    fixedTitle: null
                );

                // 生成 Pawn
                mannequinPawn = PawnGenerator.GeneratePawn(request);

                if (mannequinPawn != null)
                {
                    // 确保渲染器初始化
                    mannequinPawn.Drawer.renderer.SetAllGraphicsDirty();
                    
                    // 确保有 CompPawnSkin
                    if (mannequinPawn.GetComp<CompPawnSkin>() == null)
                    {
                        var skinComp = new CompPawnSkin();
                        skinComp.parent = mannequinPawn;
                        mannequinPawn.AllComps.Add(skinComp);
                        skinComp.Initialize(new CompProperties_PawnSkin());
                    }

                    Log.Message("[CharacterStudio] 人偶创建成功");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 创建人偶失败: {ex}");
            }
        }

        private void DestroyMannequin()
        {
            if (mannequinPawn != null)
            {
                mannequinPawn.Destroy();
                mannequinPawn = null;
            }
        }

        private void DrawPawnPortrait(Rect rect, Rot4 rotation, float zoom)
        {
            if (mannequinPawn == null) return;

            try
            {
                // 计算基础尺寸 (256x256 足够预览使用)
                float baseSize = 256f;
                Vector2 portraitSize = new Vector2(baseSize, baseSize);

                // 获取肖像
                // 注意：zoom 参数在 PortraitsCache.Get 中控制相机的 OrthographicSize
                // 较大的 zoom 值 (例如 1.5) 会放大 Pawn
                var portrait = PortraitsCache.Get(
                    mannequinPawn,
                    portraitSize,
                    rotation,
                    new Vector3(0f, 0f, 0.15f), // 稍微调整摄像机 Z 轴偏移
                    zoom // 传递 zoom 参数给 PortraitsCache
                );

                if (portrait != null)
                {
                    // 计算绘制区域，保持纵横比
                    float drawSize = Mathf.Min(rect.width, rect.height);
                    
                    // 居中绘制
                    float x = rect.x + (rect.width - drawSize) / 2;
                    float y = rect.y + (rect.height - drawSize) / 2;
                    Rect drawRect = new Rect(x, y, drawSize, drawSize);

                    // 使用 ScaleMode.ScaleToFit 确保正确缩放
                    GUI.DrawTexture(drawRect, portrait, ScaleMode.ScaleToFit);
                }
            }
            catch (Exception ex)
            {
                DrawPlaceholder(rect, $"肖像错误: {ex.Message}");
            }
        }

        private void DrawPlaceholder(Rect rect, string message)
        {
            // 绘制占位背景
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

            // 绘制文本
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.gray;
            Widgets.Label(rect, message);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 查找与指定种族匹配的 PawnKindDef
        /// </summary>
        private PawnKindDef FindPawnKindForRace(ThingDef raceDef)
        {
            // 如果是人类，使用 Colonist
            if (raceDef == ThingDefOf.Human)
            {
                return PawnKindDefOf.Colonist;
            }

            // 对于其他种族（如 HAR），尝试查找匹配的 PawnKindDef
            // 优先查找没有派系绑定的 PawnKindDef（更适合用作人偶）
            var matchingKind = DefDatabase<PawnKindDef>.AllDefs
                .FirstOrDefault(k => k.race == raceDef && k.defaultFactionDef == null);
            
            if (matchingKind != null)
            {
                return matchingKind;
            }

            // 其次查找任何使用此种族的 PawnKindDef
            matchingKind = DefDatabase<PawnKindDef>.AllDefs
                .FirstOrDefault(k => k.race == raceDef);

            if (matchingKind != null)
            {
                return matchingKind;
            }

            // 如果找不到，回退到 Colonist（可能会导致问题，但至少不会崩溃）
            Log.Warning($"[CharacterStudio] 未找到种族 {raceDef.defName} 对应的 PawnKindDef，回退到 Colonist");
            return PawnKindDefOf.Colonist;
        }

        /// <summary>
        /// 获取可用的种族列表
        /// </summary>
        public static ThingDef[] GetAvailableRaces()
        {
            return DefDatabase<ThingDef>.AllDefs
                .Where(d => d.race != null && d.race.Humanlike)
                .OrderBy(d => d.label)
                .ToArray();
        }
    }
}