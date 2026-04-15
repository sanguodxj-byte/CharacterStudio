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
        public static readonly Vector3 PreviewCameraOffset = new Vector3(0f, 0.22f, 0.15f);
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

        public bool showClothes = true;
        public bool showHeadgear = true;

        // 手动装备的服装
        private System.Collections.Generic.List<ThingDef> manualApparelDefs = new System.Collections.Generic.List<ThingDef>();

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
            // 清空文件修改时间缓存：新人偶的贴图路径集合与旧人偶不同，
            // 旧路径残留会导致字典无限增长（内存泄漏）。
            fileModificationTimes.Clear();
            CreateMannequin();
            needsRefresh = true;
        }

        /// <summary>
        /// 强制重建人偶（包含同种族时），用于新建皮肤时清除旧外观残留。
        /// </summary>
        public void ForceReset(ThingDef? raceDef = null)
        {
            currentRace = raceDef ?? ThingDefOf.Human;
            DestroyMannequin();
            fileModificationTimes.Clear();
            CreateMannequin();
            needsRefresh = true;
        }

        /// <summary>
        /// 从源 Pawn 复制外观特征到人偶
        /// </summary>
        public void CopyAppearanceFrom(Pawn source)
        {
            if (mannequinPawn == null || source == null) return;

            try
            {
                // 1. 同步 StoryTracker (发型, 发色, 肤色, 体型, 头型)
                if (mannequinPawn.story != null && source.story != null)
                {
                    mannequinPawn.story.hairDef = source.story.hairDef;
                    mannequinPawn.story.HairColor = source.story.HairColor;
                    mannequinPawn.story.bodyType = source.story.bodyType;
                    mannequinPawn.story.headType = source.story.headType;
                    
                    // 强制同步肤色
                    mannequinPawn.story.skinColorOverride = source.story.SkinColor;
                    
                    // 同步年龄 (影响身形和纹理)
                    mannequinPawn.ageTracker.AgeBiologicalTicks = source.ageTracker.AgeBiologicalTicks;
                    mannequinPawn.ageTracker.AgeChronologicalTicks = source.ageTracker.AgeChronologicalTicks;
                }
                
                // 2. 同步 StyleTracker (胡须, 纹身)
                if (mannequinPawn.style != null && source.style != null)
                {
                    mannequinPawn.style.beardDef = source.style.beardDef;
                    mannequinPawn.style.FaceTattoo = source.style.FaceTattoo;
                    mannequinPawn.style.BodyTattoo = source.style.BodyTattoo;
                }

                // 3. 同步 Genes (如果存在)
                if (mannequinPawn.genes != null && source.genes != null)
                {
                    // 简单同步异种类型
                    if (mannequinPawn.genes.Xenotype != source.genes.Xenotype)
                    {
                        mannequinPawn.genes.SetXenotype(source.genes.Xenotype);
                    }
                }

                // 4. 刷新渲染
                mannequinPawn.Drawer.renderer.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(mannequinPawn);
                needsRefresh = true;
                
                Log.Message($"[CharacterStudio] 已从 {source.LabelShort} 同步外观到预览人偶");
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 同步外观失败: {ex}");
            }
        }

        /// <summary>
        /// 应用皮肤定义
        /// </summary>
        public void ApplySkin(PawnSkinDef? skinDef)
        {
            if (mannequinPawn == null) return;

            try
            {
                // ApplySkinToPawn 内部已通过 ForceRebuildRenderTree → SetAllGraphicsDirty
                // 完成一次完整刷新，此处不再重复调用，避免同帧三重刷新。
                if (!PawnSkinRuntimeUtility.ApplySkinToPawn(mannequinPawn, skinDef))
                    return;

                needsRefresh = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用皮肤失败: {ex}");
            }
        }

        public void ApplyPlan(CharacterStudio.Design.CharacterApplicationPlan? plan)
        {
            if (mannequinPawn == null || plan == null)
            {
                return;
            }

            try
            {
                // 预览始终复用同一个 mannequin Pawn。
                // 若不先清空上一轮 runtime skin，则删除图层/清空部件后，
                // 旧的自定义节点、overlay、装备预览节点仍可能残留在当前渲染树上。
                PawnSkinRuntimeUtility.ClearSkinFromPawn(mannequinPawn);

                plan.targetPawn = mannequinPawn;
                if (!CharacterStudio.Design.CharacterApplicationExecutor.Execute(plan))
                {
                    return;
                }

                needsRefresh = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用计划到预览人偶失败: {ex}");
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
                DrawPlaceholder(rect, "CS_Studio_Mannequin_NotInitialized".Translate());
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
                DrawPlaceholder(rect, "CS_Studio_Mannequin_RenderError".Translate(ex.Message));
            }
        }

        /// <summary>
        /// 装备指定服装
        /// </summary>
        public void EquipApparel(ThingDef apparelDef)
        {
            if (mannequinPawn == null || apparelDef == null || !apparelDef.IsApparel) return;

            if (!manualApparelDefs.Contains(apparelDef))
                manualApparelDefs.Add(apparelDef);

            // 穿上新衣服
            try
            {
                Apparel apparel = (Apparel)ThingMaker.MakeThing(apparelDef);
                // Wear 会自动处理层级冲突并脱掉冲突的旧衣服
                mannequinPawn.apparel.Wear(apparel, false);
                
                // 同步 manualApparelDefs 以反映实际穿着情况（Wear 可能会导致其他衣服脱落）
                SyncManualApparelFromPawn();
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 装备服装失败: {apparelDef.defName}, {ex.Message}");
            }

            needsRefresh = true;
        }

        /// <summary>
        /// 脱掉所有手动装备的服装
        /// </summary>
        public void ClearApparel()
        {
            if (mannequinPawn == null) return;

            var worn = mannequinPawn.apparel.WornApparel.ToList();
            foreach (var a in worn)
            {
                mannequinPawn.apparel.Remove(a);
            }
            manualApparelDefs.Clear();
            needsRefresh = true;
        }

        private void SyncManualApparelFromPawn()
        {
            if (mannequinPawn == null) return;
            manualApparelDefs = mannequinPawn.apparel.WornApparel.Select(a => a.def).ToList();
        }

        private void ReapplyManualApparel()
        {
            if (mannequinPawn == null || manualApparelDefs.Count == 0) return;

            var toEquip = manualApparelDefs.ToList();
            // 先清空当前所有衣服（包含随机生成的）
            var current = mannequinPawn.apparel.WornApparel.ToList();
            foreach (var a in current) mannequinPawn.apparel.Remove(a);

            foreach (var def in toEquip)
            {
                try
                {
                    Apparel apparel = (Apparel)ThingMaker.MakeThing(def);
                    mannequinPawn.apparel.Wear(apparel, false);
                }
                catch { }
            }
            
            // 再次同步，防止因种族限制等原因导致的穿着失败
            SyncManualApparelFromPawn();
        }

        /// <summary>
        /// 获取所有原版服装
        /// </summary>
        public static ThingDef[] GetAvailableApparel()
        {
            return DefDatabase<ThingDef>.AllDefs
                .Where(d => d.IsApparel)
                .OrderBy(d => d.label)
                .ToArray();
        }

        // ─────────────────────────────────────────────
        // 私有方法
        // ─────────────────────────────────────────────

        private void CreateMannequin()
        {
            if (currentRace == null) return;

            try
            {
                mannequinPawn = TryGenerateMannequinPawn(currentRace) ?? TryGenerateMannequinPawn(ThingDefOf.Human);

                if (mannequinPawn != null)
                {
                    // 应用手动选择的服装（覆盖随机生成的）
                    if (manualApparelDefs.Count > 0)
                    {
                        ReapplyManualApparel();
                    }
                    else
                    {
                        // 如果没有手动设置过，则清空随机生成的衣服（保持纯净预览）
                        var randomApparel = mannequinPawn.apparel.WornApparel.ToList();
                        foreach (var a in randomApparel) mannequinPawn.apparel.Remove(a);
                    }

                    // 确保渲染器初始化
                    mannequinPawn.Drawer.renderer.SetAllGraphicsDirty();
                    
                    Log.Message("[CharacterStudio] 人偶创建成功");
                }
                else
                {
                    Log.Warning($"[CharacterStudio] 人偶创建失败，已跳过预览初始化: {currentRace.defName}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 创建人偶失败，预览已降级为空状态: {ex.Message}");
                mannequinPawn = null;
            }
        }

        private Pawn? TryGenerateMannequinPawn(ThingDef? raceDef)
        {
            if (raceDef == null)
            {
                return null;
            }

            try
            {
                PawnKindDef kindDef = FindPawnKindForRace(raceDef);
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

                Pawn? pawn = PawnGenerator.GeneratePawn(request);
                if (pawn == null)
                {
                    Log.Warning($"[CharacterStudio] 生成预览人偶返回空值，已跳过: {raceDef.defName}");
                }

                return pawn;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] 生成预览人偶失败，已尝试回退: race={raceDef.defName}, {ex.Message}");
                return null;
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
                float effectiveZoom = zoom;
                var skinComp = mannequinPawn.GetComp<CompPawnSkin>();
                PawnSkinDef? activeSkin = skinComp?.ActiveSkin;

                var portrait = PortraitsCache.Get(
                    mannequinPawn,
                    portraitSize,
                    rotation,
                    PreviewCameraOffset, // 以头部区域为预览中心，而非默认身体中心
                    effectiveZoom, // 传递整体缩放后的 zoom 给 PortraitsCache
                    showHeadgear,
                    showClothes
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
                DrawPlaceholder(rect, "CS_Studio_Mannequin_PortraitError".Translate(ex.Message));
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

            // 对于其他种族，尝试查找匹配的 PawnKindDef
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
        public static ThingDef[] GetAvailableRaces(bool includeHumanlike = true, bool includeAnimals = false, bool includeMechanoids = false)
        {
            return DefDatabase<ThingDef>.AllDefs
                .Where(d => d.race != null)
                .Where(d =>
                    (includeHumanlike && d.race.Humanlike)
                    || (includeAnimals && d.race.Animal)
                    || (includeMechanoids && d.race.IsMechanoid))
                .OrderBy(d => d.label)
                .ToArray();
        }
    }
}
