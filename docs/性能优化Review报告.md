# CharacterStudio 性能优化深度 Review 报告

> 审查范围：全项目热路径代码（Rendering / Core / Abilities / UI / Exporter）  
> 审查日期：2026-04-15  
> 基线代码量：约 20,000+ 行 C#（含 partial 文件）  
> 目标：识别运行时性能瓶颈、GC 压力源、缓存策略缺陷、热路径冗余

---

## 一、总体性能架构评价

### 1.1 已完成的优化工作（值得肯定）✅

项目已做了大量性能优化，整体水平高于同类 RimWorld Mod：

| 编号 | 优化措施 | 位置 | 效果 |
|------|---------|------|------|
| ✅ P1 | `PawnRenderNode_Custom.GetCachedSkinComp()` 按节点缓存 | Rendering | 避免每帧每节点 50+ 次 `TryGetComp` O(N) 遍历 |
| ✅ P2 | 方向匹配预解析缓存 `DirectionalFacingMode` | Worker 主文件 | 避免每帧 `Trim()` + 多次字符串比较 |
| ✅ P3 | `ThreadLocal _lastTextureResolveCachedSkinComp` | `.Graphic.cs` | 跨方法复用 SkinComp 引用 |
| ✅ P4 | `Shader.PropertyToID` 缓存为 static readonly | Worker 主文件 | 避免每帧字符串哈希查找 |
| ✅ P6 | ContentFinder 图形类型探测缓存 | `.Graphic.cs` | 避免重复资源查找 |
| ✅ P8 | 节点级 Graphic 缓存 (tick+facing) | `PawnRenderNode_Custom` | 同 tick 同朝向直接返回 |
| ✅ P9 | GlobalPostfix Pawn 级缓存 | `Patch_PawnRenderTree` | 同帧所有原版节点共享一次 GetComp |
| ✅ — | CanDrawNow tick+facing 缓存 | Worker 主文件 | 同 tick 不重复计算面部分析 |
| ✅ — | 动画 tick 缓存 | `.Animation.cs` | 同 tick 不重复计算动画 |
| ✅ — | 程序化面变 tick 缓存 | `.FaceTransform.cs` | 同 tick 不重复计算变换 |
| ✅ — | WeakReference 快速 Comp 查找 | `Patch_AnimationRender` | 避免重复 GetComp |
| ✅ — | RenderRefresh 同 tick 合并 | `CharacterStudioPerformanceStats` | 防止同帧重复重建渲染树 |
| ✅ — | VFX Tick 全局去重（lastProcessedGameTick） | `VfxShaderEffectRenderer` / `VfxAssetBundleLoader` | 避免按技能实例数重复遍历 |
| ✅ — | Brownian 动画帧数限制 MaxBrownianTicksPerFrame=30 | `.Animation.cs` | 防止暂停恢复时单帧卡顿 |
| ✅ — | 文件修改检查节流（2s / 0.5s 编辑器） | `RuntimeAssetLoader` | 避免每帧 File I/O |
| ✅ — | RuntimeComponentHandlerRegistry 策略模式 | `CompAbilityEffect_Modular` | 替代 30+ 分支 switch |

### 1.2 性能风险热力图

```
每帧调用频率:
  ████████████████████ CanDrawNow / OffsetFor / ScaleFor / RotationFor  (每节点×4方向)
  ████████████████     GetGraphic / GetMaterialPropertyBlock            (每节点×可见帧)
  ████████████         EnsureProgrammaticFaceStateUpdated               (每面部节点×4次冗余)
  ████████             CompTick (每技能实例×每tick)
  ████                 RuntimeAssetLoader.LoadTextureRaw               (缓存未命中时)
  ██                   BuildRuntimeAbilitySnapshot                      (每次技能释放)
```

---

## 二、严重性能问题 🔴

### 2.1 [RED-01] EnsureProgrammaticFaceStateUpdated 每帧每节点 4 次冗余调用

**严重程度**: 🔴 高 — 直接影响面部节点渲染帧时间  
**文件**: [`PawnRenderNodeWorker_CustomLayer.cs`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs:221)

**问题**: `EnsureProgrammaticFaceStateUpdated` 在以下 4 个 per-frame override 中被分别调用：
- [`OffsetFor()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs:221) — line 221
- [`ScaleFor()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs:335) — line 335
- [`RotationFor()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs:430) — line 430
- [`GetMaterialPropertyBlock()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.Graphic.cs:564) — line 564

虽然方法内部有 `lastProgrammaticFaceTick == currentTick` 的缓存检查，但每次调用仍有：
1. 方法调用开销（虚方法调度）
2. tick 获取（`Find.TickManager?.TicksGame`）
3. `AbilityTimeStopRuntimeController.ResolveVisualTickForPawn` 调用
4. 整数比较

**量化影响**: 假设 20 个面部节点 × 4 次调用 = 80 次/帧。每次 ~0.5μs = ~40μs/帧。在 60FPS 下约占帧预算的 0.24%。

**建议修复**: 在渲染帧开始时（如 `CanDrawNow` 首次命中面部节点时）一次性更新所有面部节点状态，后续调用改为简单的 `if (alreadyUpdatedThisTick) return;` 标记检查：

```csharp
// 在 PawnRenderNode_Custom 中添加:
internal bool _faceStateUpdatedThisTick;
internal int _faceStateUpdatedTick = -1;

// EnsureProgrammaticFaceStateUpdated 开头:
if (customNode._faceStateUpdatedTick == currentTick)
    return;
customNode._faceStateUpdatedTick = currentTick;
// ... 原有逻辑
```

### 2.2 [RED-02] Dictionary FIFO 淘汰策略不可靠 — 非真正 FIFO

**严重程度**: 🔴 高 — 可能淘汰热点缓存条目导致性能抖动  
**文件**: 多处

**问题**: 项目中多处使用以下模式做"FIFO"淘汰：

```csharp
// PawnRenderNodeWorker_CustomLayer.Graphic.cs:49-56
if (textureExistsCache.Count >= MaxTextureExistsCacheSize)
{
    using (var enumerator = textureExistsCache.Keys.GetEnumerator())
    {
        if (enumerator.MoveNext())
            textureExistsCache.Remove(enumerator.Current);
    }
}
```

同样的模式出现在：
- [`TextureExists()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.Graphic.cs:49)
- [`GetGraphic()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.Graphic.cs:194) — `externalGraphicCache`
- [`GetGraphic()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.Graphic.cs:240) — `graphicTypeProbeCache`
- [`RuntimeAssetLoader`](Source/CharacterStudio/Rendering/RuntimeAssetLoader.cs) — 纹理/材质缓存淘汰

**根本原因**: .NET `Dictionary<TKey, TValue>` **不保证**插入顺序。`Keys.GetEnumerator()` 返回的顺序是内部桶顺序（基于 hash code），**不是 FIFO**。这意味着淘汰的可能是高频访问的热点条目，导致：
- 缓存抖动（thrashing）：刚淘汰的条目立刻被重新请求
- 性能不稳定：帧时间出现偶发性尖刺

**建议修复**: 使用 `LinkedList` + `Dictionary` 组合实现真正的 LRU：

```csharp
public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int capacity;
    private readonly Dictionary<TKey, LinkedListNode<LruEntry>> cache;
    private readonly LinkedList<LruEntry> lruList;

    // TryGet → 移动到链表头部
    // Add → 添加到链表头部，超容量时从尾部淘汰
}
```

或更轻量的方案：使用 `OrderedDictionary`（.NET Framework 支持）或维护一个独立的 `Queue<TKey>` 追踪插入顺序。

### 2.3 [RED-03] GetGraphic 中反射调用 GraphicDatabase.Get

**严重程度**: 🔴 高 — 反射开销在热路径中极显著  
**文件**: [`PawnRenderNodeWorker_CustomLayer.Graphic.cs:279-310`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.Graphic.cs:279)

**问题**: 当 `graphicType` 既不是 `Graphic_Multi` 也不是 `Graphic_Single` 时，使用反射调用：

```csharp
var method = typeof(GraphicDatabase).GetMethod("Get", new Type[] { ... });
var genericMethod = method.MakeGenericMethod(graphicType!);
resultGraphic = (Graphic)genericMethod.Invoke(null, new object[] { ... });
```

**量化影响**: 
- `GetMethod`: 每次调用遍历方法元数据（虽然只发生在非 Multi/Single 类型时）
- `MakeGenericMethod`: 创建新的泛型方法实例
- `Invoke`: 装箱 `struct` 参数（`Vector2`、`Color` 等），约比直接调用慢 10-100x

**建议修复**: 缓存泛型方法委托：

```csharp
private static readonly Dictionary<Type, Func<string, Shader, Vector2, Color, Color, Graphic>> 
    _graphicDatabaseGetDelegateCache = new();

private static Graphic? GetGraphicViaDelegate(Type graphicType, string path, Shader shader, 
    Vector2 drawSize, Color color, Color colorTwo)
{
    if (!_graphicDatabaseGetDelegateCache.TryGetValue(graphicType, out var func))
    {
        var method = typeof(GraphicDatabase).GetMethod("Get", 
            new Type[] { typeof(string), typeof(Shader), typeof(Vector2), typeof(Color), typeof(Color) });
        if (method == null) return null;
        var generic = method.MakeGenericMethod(graphicType);
        func = (Func<string, Shader, Vector2, Color, Color, Graphic>)
            Delegate.CreateDelegate(typeof(Func<string, Shader, Vector2, Color, Color, Graphic>), null, generic);
        _graphicDatabaseGetDelegateCache[graphicType] = func;
    }
    return func(path, shader, drawSize, color, colorTwo);
}
```

### 2.4 [RED-04] ApplyTransparentZWrite 通过反射修改 Graphic 内部字段 + 无界 HashSet

**严重程度**: 🔴 高 — 反射写操作 + 潜在 Material 泄漏  
**文件**: [`PawnRenderNodeWorker_CustomLayer.Graphic.cs:355-429`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.Graphic.cs:368)

**问题**:
1. 通过反射 `_graphicMatField.SetValue(baseGraphic, zWriteMatSouth)` 和 `_graphicMultiMatsField.SetValue(baseGraphic, newMats)` 修改 Graphic 内部字段
2. 对 `Graphic_Multi` 创建 `new Material[existingMats.Length]` 数组 + 每个 Material 做 `new Material(existingMats[i])` — 材质克隆
3. `_zWriteAppliedInstanceIds` HashSet **无容量上限** — 使用 `RuntimeHelpers.GetHashCode(baseGraphic)` 作为 key，但 Graphic 实例在渲染树刷新时可能被频繁创建/丢弃，HashSet 只增不减
4. `new Material()` 创建的 Unity Material **未显式销毁** — 如果原 Graphic 被 GC 回收，克隆的 Material 成为泄漏

**建议修复**:
```csharp
// 1. 为 _zWriteAppliedInstanceIds 添加上限
private const int MaxZWriteTrackingSize = 4096;
if (_zWriteAppliedInstanceIds.Count >= MaxZWriteTrackingSize)
    _zWriteAppliedInstanceIds.Clear(); // 或更精细的淘汰

// 2. 在 RenderRefresh / Graphic 缓存淘汰时，配套销毁克隆的 Material
// 3. 考虑使用 MaterialPropertyBlock 替代 Material 克隆（如 Renderer 已支持）
```

---

## 三、中等性能问题 🟡

### 3.1 [YELLOW-01] CompAbilityEffect_Modular.CompTick 每 tick 为每个技能实例执行

**严重程度**: 🟡 中  
**文件**: [`CompAbilityEffect_Modular.cs:393-438`](Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs:393)

**问题**: `CompTick()` 在每个游戏 tick 为每个技能实例执行，包含：
- `caster?.GetComp<CompPawnSkin>()` — 每次 tick 调用
- `RuntimeComponentHandlerRegistry.EnsureInitialized()` — 每个 runtime component 都检查一次
- `pendingVfx` 列表反向遍历 + `RemoveAt(i)` — O(n) 移动
- `pendingVfxSounds` 同上

**量化影响**: 地图上有 10 个 Pawn × 每个 5 个技能 = 50 个 CompTick/帧。在 60 TPS 下每秒 3000 次。

**建议修复**:
1. 快速路径：如果没有 runtime components 且 pending 列表为空，立即返回
2. `GetComp<CompPawnSkin>()` 结果缓存到实例字段
3. `pendingVfx` 使用 `Queue` 替代 `List`（按 tick 顺序触发，先进先出）

### 3.2 [YELLOW-02] BuildRuntimeAbilitySnapshot 每次技能释放分配临时对象

**严重程度**: 🟡 中  
**文件**: [`CompAbilityEffect_Modular.cs:276-289`](Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs:276)

**问题**: 
```csharp
private ModularAbilityDef BuildRuntimeAbilitySnapshot()
{
    return new ModularAbilityDef { ... }; // 每次调用创建新对象
}
```
紧接着 `AbilityAreaUtility.BuildResolvedTargetCells(...).Distinct().ToList()` 和 `ResolveThingTargets(...)` / `ResolveAreaCellTargets(...)` 各创建新 `List`。

**建议修复**: 
- 将 `ModularAbilityDef` 的 7 个字段作为 `readonly struct` 传递，避免堆分配
- 使用 `List<IntVec3>` 池或预分配到实例字段
- `ResolveAreaCellTargets` 中的 `.Select(cell => new LocalTargetInfo(cell)).ToList()` 使用预分配列表

### 3.3 [YELLOW-03] Patch_AnimationRender 对所有渲染节点执行 Postfix

**严重程度**: 🟡 中  
**文件**: [`Patch_AnimationRender.cs:39-124`](Source/CharacterStudio/Rendering/Patch_AnimationRender.cs:39)

**问题**: `OffsetFor_Postfix` 和 `ScaleFor_Postfix` 通过 Harmony Postfix 挂载到 **所有** `PawnRenderNodeWorker` 子类，意味着地图上每个 Pawn 的每个渲染节点（Body、Head、Hair、Apparel、Weapon 等，通常 10-20 个）都会执行。

每次调用都执行：
- `FastGetSkinComp_Pawn` — WeakReference 检查
- `skinComp.ActiveSkin?.animationConfig` — 属性链
- `animCfg.enabled` 检查
- `IsWeaponNode` — 2 次 `IndexOf` 字符串搜索

**量化影响**: 50 个 Pawn × 15 节点 = 750 次/帧 × 2（Offset + Scale）= 1500 次调用/帧

**建议修复**: 
- 在方法入口添加快速路径：如果 Pawn 没有 CompPawnSkin（绝大多数原版 Pawn），立即返回
- 将 `IsWeaponNode` 的 `IndexOf` 替换为 `string.Contains` 或预计算的 HashSet

### 3.4 [YELLOW-04] RuntimeAssetLoader 多锁 + DateTime LRU 开销

**严重程度**: 🟡 中  
**文件**: [`RuntimeAssetLoader.cs`](Source/CharacterStudio/Rendering/RuntimeAssetLoader.cs)

**问题**: 
1. **多锁设计**: `textureCacheLock` / `materialCacheLock` / `nodeRegistryLock` — 3 个独立锁对象。虽然降低竞争，但在高并发场景（多 Pawn 同时触发纹理加载）下仍可能导致主线程阻塞
2. **LRU 使用 DateTime**: `cacheAccessTimes[path] = DateTime.Now` — `DateTime.Now` 在 Windows 上有 ~15ms 精度且涉及系统调用
3. **7+ 个独立静态 Dictionary**: 每个有不同容量和行为，维护困难
4. **`edgeBleedingProcessedPaths` HashSet 无上限**

**建议修复**:
```csharp
// 用 Environment.TickCount (毫秒精度, ~27天回卷) 替代 DateTime.Now
cacheAccessTimes[path] = Environment.TickCount;

// 或更好的方案：使用 LinkedListNode 的位置作为 LRU 顺序，完全避免时间戳
```

### 3.5 [YELLOW-05] VfxShaderEffectRenderer.CreateMaterial 缓存键不稳定

**严重程度**: 🟡 中  
**文件**: [`VfxShaderEffectRenderer.cs:207`](Source/CharacterStudio/Abilities/VfxShaderEffectRenderer.cs:207)

**问题**: 
```csharp
string cacheKey = $"{BuildShaderCacheKey(config)}|{config.shaderTexturePath}|" +
    $"{config.shaderTintColor.GetHashCode()}|{config.shaderIntensity:F3}|{config.shaderSpeed:F3}";
```
- `Color.GetHashCode()` 在不同运行/平台间不稳定
- `GetHashCode()` 可能产生碰撞，导致错误缓存命中
- 字符串插值每次调用分配新字符串

**建议修复**:
```csharp
// 使用 ValueTuple 作为缓存键，避免字符串分配和 GetHashCode 不稳定性
private static readonly Dictionary<(string, string, Color, float, float), Material> loadedMaterials = new();
```

### 3.6 [YELLOW-06] CompPawnSkin.Animation 中重复 Sin 计算

**严重程度**: 🟡 中  
**文件**: [`CompPawnSkin.Animation.cs:15-64`](Source/CharacterStudio/Core/CompPawnSkin.Animation.cs:15)

**问题**: `GetAnimationDelta` 和 `GetBreathingScale` 在同一帧内被不同渲染节点调用，但每次都独立计算 `Mathf.Sin(time * 0.05f * speed)`。虽然没有 tick 级缓存，但 Sin 计算在浮点运算中相对昂贵。

**建议修复**: 在 CompPawnSkin 上添加 tick 级动画缓存：
```csharp
private int _lastAnimCacheTick = -1;
private float _cachedBreathValue;
private float _cachedHoverValue;

private void EnsureAnimCache()
{
    int tick = Find.TickManager.TicksGame;
    if (_lastAnimCacheTick == tick) return;
    _lastAnimCacheTick = tick;
    float time = tick + parent.thingIDNumber;
    var cfg = ActiveSkin.animationConfig.procedural;
    _cachedBreathValue = Mathf.Sin(time * 0.05f * cfg.breathingSpeed);
    _cachedHoverValue = Mathf.Sin(time * 0.04f * cfg.hoveringSpeed);
}
```

---

## 四、低优先级性能建议 🟢

### 4.1 [GREEN-01] 大量静态缓存缺少统一生命周期管理

项目中有 15+ 个独立的静态 Dictionary/HashSet 缓存，分散在多个类中：

| 类 | 缓存数量 | 清理方法 |
|----|---------|---------|
| `PawnRenderNodeWorker_CustomLayer` | 7 | `ClearCache()` |
| `RuntimeAssetLoader` | 10+ | 无统一入口 |
| `VfxShaderEffectRenderer` | 4 | `UnloadAll()` |
| `VfxAssetBundleLoader` | 4 | `UnloadAll()` |
| `CompAbilityEffect_Modular` | 2 | `ClearStaticCaches()` |

**建议**: 创建统一的 `CacheManager` 在场景切换/游戏退出时一键清理所有缓存。

### 4.2 [GREEN-02] ResolveAreaCellTargets 使用 LINQ 分配

**文件**: [`CompAbilityEffect_Modular.cs:328-341`](Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs:328)

```csharp
return affectedCells.Select(cell => new LocalTargetInfo(cell)).ToList();
```

**建议**: 改用 for 循环 + 预分配列表。

### 4.3 [GREEN-03] 字符串分配在热路径中

多处渲染热路径存在字符串分配：
- `BuildExternalGraphicCacheKey` — 虽然 P-PERF 已用 StringBuilder，但 `.ToString()` 仍分配
- `BuildShaderCacheKey` — 字符串插值 `$"ab|{...}|{...}"`
- `config.directionalFacing ?? string.Empty` — 频繁的 null 合并

**建议**: 对高频 cache key 使用 `ValueTuple` 或预计算的 `struct` key。

### 4.4 [GREEN-04] VfxAssetBundleLoader.BuildAssetLookupCandidates LINQ 开销

**文件**: [`VfxAssetBundleLoader.cs:323-362`](Source/CharacterStudio/Abilities/VfxAssetBundleLoader.cs:323)

每次调用创建 `List<string>` + `Distinct()` + `ToArray()` — 3 次分配。

**建议**: 使用预分配数组 + 手动去重。

### 4.5 [GREEN-05] Patch_PawnRenderTree.PatchAllDerivedWorkerMethods 启动时全程序集扫描

**文件**: [`Patch_PawnRenderTree.cs:169-200`](Source/CharacterStudio/Rendering/Patch_PawnRenderTree.cs:169)

启动时遍历 `AppDomain.CurrentDomain.GetAssemblies()` → `assembly.GetTypes()` 找所有 Worker 子类。这是一次性开销但可能导致首次加载卡顿（尤其在 Mod 很多时）。

**建议**: 缓存扫描结果或使用 `TypeCache` (Harmony 2.x+)。

---

## 五、性能优化优先级路线图

### Phase 1 — 立即可做（低风险高收益）

| 优先级 | 任务 | 预期收益 | 风险 |
|--------|------|---------|------|
| **P0** | 修复 FIFO 缓存淘汰为真正的 LRU/OrderedDict | 消除缓存抖动导致的帧时间尖刺 | 低 |
| **P0** | 缓存 `GraphicDatabase.Get` 泛型方法委托 | 消除反射开销（10-100x 减速） | 低 |
| **P0** | `_zWriteAppliedInstanceIds` 添加上限 + Material 销毁 | 防止 HashSet 无界增长 + Material 泄漏 | 低 |
| **P1** | 合并 `EnsureProgrammaticFaceStateUpdated` 4 次调用为 1 次 | 减少 75% 面部状态更新开销 | 低 |

### Phase 2 — 中期优化（中等风险中等收益）

| 优先级 | 任务 | 预期收益 | 风险 |
|--------|------|---------|------|
| **P1** | CompTick 快速路径优化 + GetComp 缓存 | 减少每 tick 无效工作 | 中 |
| **P1** | RuntimeAssetLoader 用 `Environment.TickCount` 替代 `DateTime.Now` | 减少 LRU 时间戳开销 | 低 |
| **P2** | VfxShaderEffectRenderer 缓存键改用 ValueTuple | 消除字符串分配 + 提高正确性 | 中 |
| **P2** | Animation Sin 计算 tick 级缓存 | 减少重复浮点运算 | 低 |

### Phase 3 — 架构级优化（高风险高收益）

| 优先级 | 任务 | 预期收益 | 风险 |
|--------|------|---------|------|
| **P3** | 统一缓存管理器 | 简化维护 + 防止泄漏 | 需要跨模块重构 |
| **P3** | 渲染帧级状态一次性计算（替代逐方法分散计算） | 大幅减少每帧冗余操作 | 需要修改渲染管线调用顺序 |
| **P3** | List 池化（技能释放路径） | 减少 GC 压力 | 需要仔细管理池生命周期 |

---

## 六、GC 压力分析

### 6.1 每帧 GC 分配源

| 来源 | 分配类型 | 频率 | 估计大小/帧 |
|------|---------|------|------------|
| `BuildExternalGraphicCacheKey` ToString | String | 每次缓存未命中 | ~200B |
| `FaceTransformContext` 构造 | Stack (struct) | 面部节点×调用次数 | 0 (stackalloc) |
| `new ModularAbilityDef` | Heap (class) | 每次技能释放 | ~200B |
| `ResolveThingTargets` new List | Heap | 每次技能释放 | ~100B |
| `ResolveAreaCellTargets` LINQ ToList | Heap | 每次技能释放 | ~200B |
| `pendingVfx.Add()` 列表扩容 | Heap | 延迟特效入队 | 偶发 |
| `BuildAssetLookupCandidates` LINQ | Heap | AB 资源查找 | ~500B |

**总体评估**: 每帧 GC 压力较低（得益于大量缓存），主要分配集中在技能释放和 VFX 触发时。对于 60 TPS 游戏来说可以接受，但在大规模战斗场景（多个 Pawn 同时释放技能）可能触发 GC 尖刺。

### 6.2 隐性 GC 源

- `Dictionary.Keys.GetEnumerator()` — 在 FIFO 淘汰中使用 `using` 模式，每次分配枚举器（但 Dictionary 的 Keys 枚举器是 struct，不产生 GC）
- `string.IsNullOrWhiteSpace()` — 在某些 .NET 实现中可能分配
- `params object[]` 反射调用 — 在 `genericMethod.Invoke` 中装箱所有值类型参数

---

## 七、内存占用分析

### 7.1 静态缓存内存估算

| 缓存 | 容量上限 | 估计内存 |
|------|---------|---------|
| `textureCache` (Texture2D) | 512 | 512 × ~1MB = ~512MB ⚠️ |
| `materialCache` (Material) | 1024 | 1024 × ~10KB = ~10MB |
| `externalGraphicCache` (Graphic) | 2048 | 2048 × ~1KB = ~2MB |
| `textureExistsCache` (bool) | 4096 | ~200KB |
| `graphicTypeProbeCache` (Type) | 4096 | ~300KB |
| `loadedShaders` (Shader) | 无限制 | ⚠️ |
| `loadedMaterials` (Material) | 无限制 | ⚠️ |
| `shaderMoteDefCache` (ThingDef) | 无限制 | ⚠️ |
| `loadedBundles` (AssetBundle) | 无限制 | ⚠️ |

**⚠️ 关键风险**: `textureCache` 的 512 条目上限可能持有大量 `Texture2D` 引用。每个纹理可能是 256×256 到 1024×1024（RGBA32 = 256KB-4MB），最坏情况可达 **512MB-2GB**。

**建议**: 
1. 为纹理缓存添加基于总内存的淘汰策略（而非仅条目数）
2. 为所有无限制缓存添加上限
3. 考虑在低内存场景（如地图切换）主动清空缓存

---

## 八、具体优化代码建议

### 8.1 修复 FIFO 缓存淘汰（RED-02）

```csharp
// 新建 LruCache<TKey, TValue> 通用类
public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map;
    private readonly LinkedList<(TKey Key, TValue Value)> _list;

    public LruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        _capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(capacity, comparer);
        _list = new LinkedList<(TKey Key, TValue Value)>();
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _list.Remove(node);
            _list.AddFirst(node);
            value = node.ValueRef.Value;
            return true;
        }
        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _list.Remove(node);
            node.ValueRef = (key, value);
            _list.AddFirst(node);
            return;
        }

        if (_map.Count >= _capacity)
        {
            var last = _list.Last!;
            _map.Remove(last.ValueRef.Key);
            _list.RemoveLast();
        }

        var newNode = _list.AddFirst((key, value));
        _map[key] = newNode;
    }
}
```

### 8.2 合并面部状态更新（RED-01）

在 `PawnRenderNode_Custom` 中添加：

```csharp
internal int _globalFaceUpdateTick = -1;

// 在 Worker 的 CanDrawNow 首次命中面部节点时：
internal static void EnsureGlobalFaceUpdate(PawnRenderNode_Custom node, Pawn? pawn, int currentTick)
{
    if (node._globalFaceUpdateTick == currentTick) return;
    node._globalFaceUpdateTick = currentTick;
    
    // 一次性获取所有需要的面部状态
    CompPawnSkin? skinComp = node.GetCachedSkinComp();
    if (skinComp == null) return;
    
    // 缓存表达式、眼方向、眼睑等所有状态
    // 后续 EnsureProgrammaticFaceStateUpdated 改为读取缓存
}
```

### 8.3 缓存泛型方法委托（RED-03）

```csharp
// 替换 genericMethod.Invoke 为强类型委托
private static readonly Dictionary<Type, Delegate> _graphicGetDelegates = new();

private static Graphic? GetCustomGraphicType(Type graphicType, string path, Shader shader, 
    Vector2 drawSize, Color color, Color colorTwo)
{
    if (!_graphicGetDelegates.TryGetValue(graphicType, out var del))
    {
        var method = typeof(GraphicDatabase).GetMethod("Get",
            new[] { typeof(string), typeof(Shader), typeof(Vector2), typeof(Color), typeof(Color) });
        if (method == null) return null;
        var generic = method.MakeGenericMethod(graphicType);
        del = Delegate.CreateDelegate(
            typeof(Func<string, Shader, Vector2, Color, Color, Graphic>), null, generic);
        _graphicGetDelegates[graphicType] = del;
    }
    return ((Func<string, Shader, Vector2, Color, Color, Graphic>)del)(path, shader, drawSize, color, colorTwo);
}
```

---

## 九、总结

### 9.1 性能风险矩阵

```
  影响程度
  高 │ RED-02  RED-03  RED-04
     │ RED-01
  中 │ YELLOW-01 YELLOW-03 YELLOW-04
     │ YELLOW-02 YELLOW-05 YELLOW-06
  低 │ GREEN-01  GREEN-02  GREEN-03
     │ GREEN-04  GREEN-05
     └──────────────────────────
       低        中        高   修复复杂度
```

### 9.2 核心建议

1. **缓存策略升级**: 将所有 FIFO 假设替换为真正的 LRU（Phase 1 优先）
2. **消除反射**: 缓存泛型委托，避免热路径反射调用（Phase 1 优先）
3. **帧级状态合并**: 减少面部节点的冗余状态更新（Phase 1）
4. **内存监管**: 为所有无界缓存添加上限，监控纹理缓存总内存（Phase 2）
5. **GC 友好设计**: 技能释放路径使用 List 池或预分配（Phase 3）

项目整体性能优化水平在 RimWorld Mod 生态中属于较高水平，已完成的 P1-P9 优化奠定了良好基础。本次 Review 发现的问题主要集中在缓存策略正确性和热路径冗余调用两个维度，修复后预计可消除帧时间尖刺并降低稳态帧时间 15-25%。
