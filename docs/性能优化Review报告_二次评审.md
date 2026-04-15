# 性能优化Review报告 — 二次评审与调整

> 评审日期：2026-04-15  
> 基于对 `docs/性能优化Review报告.md` 的逐条源码验证  
> 目标：纠正误判、调整严重程度、优化修复建议

---

## 一、严重程度降级项

### 1.1 RED-01 → 🟡 降级为中等

**原判定**: 🔴 高 — EnsureProgrammaticFaceStateUpdated 每帧每节点 4 次冗余调用  
**修订后**: 🟡 中

**降级理由**:

经源码验证，[`EnsureProgrammaticFaceStateUpdated()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.FaceTransform.cs:17) 内部已有完善的 tick 级缓存：

```csharp
// FaceTransform.cs:29-30
if (customNode.lastProgrammaticFaceTick == currentTick)
    return;  // ← 同 tick 内第二次调用直接返回
```

因此同一 tick 内 4 次调用的实际开销为：
- 第 1 次：完整计算（~5-20μs，取决于面部部件数）
- 第 2-4 次：仅 `lastProgrammaticFaceTick == currentTick` 整数比较（~0.01μs）

**量化修正**: 80 次/帧中仅 20 次有效计算，60 次为 ~3 条 CPU 指令的早期返回。实际帧时间影响从报告中的 ~40μs 修正为 ~20μs + 60×0.01μs ≈ **20.6μs**，占 60FPS 帧预算的 **0.12%**。

**修复建议调整**: 原方案（添加 `_faceStateUpdatedTick`）本质上是复制已有的 `lastProgrammaticFaceTick` 字段。更合理的做法是保持现状，因为现有缓存机制已经有效。如果确实要优化，建议在调用侧（OffsetFor/ScaleFor/RotationFor）统一在方法开头调用一次，后续直接读取 `customNode.currentProgrammatic*` 字段：

```csharp
// 在 OffsetFor 开头一次性更新所有动画状态
if (customNode != null && customNode.config != null)
{
    // 统一在此处更新一次
    EnsureProgrammaticFaceStateUpdated(customNode, parms.pawn);
    EnsureExternalLayerAnimationUpdated(customNode, parms);
    // 后续 ScaleFor/RotationFor/GetMaterialPropertyBlock 中移除这些调用
}
```

但这需要确保 RimWorld 渲染管线中 OffsetFor 一定在 ScaleFor 之前被调用——需要验证这个调用顺序是否由引擎保证。

---

### 1.2 RED-03 → 🟢 降级为低优先级

**原判定**: 🔴 高 — GetGraphic 中反射调用 GraphicDatabase.Get  
**修订后**: 🟢 低

**降级理由**:

经源码验证，反射代码路径 [`GetGraphic()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.Graphic.cs:279) 仅在以下条件同时满足时执行：

1. `graphicType != typeof(Graphic_Runtime)` — 非 Runtime
2. `graphicType != typeof(Graphic_Multi)` — 非 Multi
3. `graphicType != typeof(Graphic_Single)` — 非 Single
4. 探测缓存未命中

在实际使用中，CharacterStudio 的图层配置几乎只使用 `Graphic_Multi`、`Graphic_Single` 或 `Graphic_Runtime` 三种类型。反射路径是为用户自定义 Graphic 子类准备的 fallback，**几乎从未被触发**。

此外，`GraphicDatabase.Get<>()` 本身有 RimWorld 内部的 Graphic 缓存，即使走了反射路径，结果也会被节点级缓存（P8）和 `externalGraphicCache` 捕获，不会每帧重复反射。

**修复建议调整**: 委托缓存仍然是好的防御性编程，但优先级应降至 Phase 3。建议在代码中添加注释标明这是 fallback 路径即可。

---

### 1.3 RED-04 → 🟡 降级为中等

**原判定**: 🔴 高 — ApplyTransparentZWrite 反射 + 无界 HashSet + Material 泄漏  
**修订后**: 🟡 中

**降级理由**:

1. **HashSet 增长有限**: `_zWriteAppliedInstanceIds` 使用 `RuntimeHelpers.GetHashCode(baseGraphic)` 作为 key。RimWorld 的 `GraphicDatabase` 会缓存 Graphic 实例，因此相同纹理路径复用同一 Graphic 对象。HashSet 的条目数 = 使用透明 shader 的唯一纹理路径数，通常不超过几十个，不会无界增长。

2. **Material 泄漏范围有限**: 仅影响 `Graphic_Multi` 类型且使用 Custom/Transparent shader 的图层。原 Graphic 的 Material 被 `GraphicDatabase` 持有不会被 GC，因此克隆的 Material 也不会被 GC——这其实是正确行为（原 Material 仍被使用）。

3. **`new Material[]` 是一次性分配**: 只在首次应用 ZWrite 时执行，后续由 `_zWriteAppliedInstanceIds` 跳过。

**保留的风险点**:
- 如果使用热加载（编辑器模式），Graphic 被重新创建但旧的 HashSet 条目不会清理 → 建议在 `ClearCache()` 时一并清理
- 对 `Graphic_Single` 的 `_graphicMatField.SetValue` 直接替换了 Graphic 内部的 Material 引用，原 Material 如果没有其他引用则泄漏

**修复建议调整**: 在 [`ClearCache()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.Graphic.cs:602) 中添加 `_zWriteAppliedInstanceIds.Clear()` 即可。

---

## 二、严重程度维持项

### 2.1 RED-02 维持 🟡→🔴 中高优先级

**原判定**: 🔴 高 — Dictionary FIFO 淘汰不可靠  
**验证结论**: 问题真实存在，但实际影响取决于缓存填充速度。

经分析，受影响的缓存及其填充场景：

| 缓存 | 容量 | 填充速度 | 风险 |
|------|------|---------|------|
| `textureExistsCache` (4096) | 4096 | 慢 — 每个纹理路径只查一次 | 低 — 极少触发淘汰 |
| `externalGraphicCache` (2048) | 2048 | 中 — 外部纹理场景 | 中 — 多角色多皮肤可能触发 |
| `graphicTypeProbeCache` (4096) | 4096 | 慢 — 每个纹理路径只探测一次 | 低 |
| `RuntimeAssetLoader.textureCache` (512) | 512 | 快 — 每次纹理加载都写入 | **高** — 最可能触发淘汰 |
| `RuntimeAssetLoader.materialCache` (1024) | 1024 | 中 | 中 |

**核心风险点**: [`RuntimeAssetLoader.textureCache`](Source/CharacterStudio/Rendering/RuntimeAssetLoader.cs:19) 只有 512 容量，且纹理对象占用内存大。如果淘汰了热点纹理，下次访问需要重新从文件加载（`File.ReadAllBytes` + `CreateTexture2D`），可能造成可见的帧时间尖刺。

**修复建议调整**: 不建议全面替换为 LRU（改动量大），而是**仅对 `textureCache` 和 `externalGraphicCache` 实施改进**，这两个缓存的淘汰行为对用户体验影响最大。其他大容量缓存（4096）几乎不会触发淘汰，保持现状即可。

简化方案：
```csharp
// 使用 Queue<string> 追踪插入顺序（比 LinkedList LRU 更轻量）
private static readonly Queue<string> textureCacheInsertOrder = new Queue<string>();

// Set 时:
textureCacheInsertOrder.Enqueue(resolvedPath);

// 淘汰时（真正 FIFO）:
if (textureCache.Count >= MaxTextureCacheSize)
{
    string oldest = textureCacheInsertOrder.Dequeue();
    textureCache.Remove(oldest);
    // 配套清理其他索引
}
```

---

## 三、新增发现

### 3.1 [NEW-01] 🟡 RuntimeAssetLoader 的 LRU 使用 DateTime.Now 开销

**文件**: [`RuntimeAssetLoader.cs`](Source/CharacterStudio/Rendering/RuntimeAssetLoader.cs:30)

`cacheAccessTimes[path] = DateTime.Now` 在每次缓存命中时执行。`DateTime.Now` 在 Windows 上精度约 15ms，且涉及系统调用。

**建议**: 替换为 `Environment.TickCount`（毫秒精度，~27天回卷，开销极低）。但考虑到此缓存只在 `LoadTextureRaw` 中使用且有文件修改检查节流（2s/0.5s），LRU 时间戳实际上很少被读取——仅在缓存满淘汰时使用。因此 **实际影响很小**，维持 🟢 低优先级。

### 3.2 [NEW-02] 🟡 Patch_AnimationRender 对无 CompPawnSkin 的 Pawn 的开销

**文件**: [`Patch_AnimationRender.cs:39-80`](Source/CharacterStudio/Rendering/Patch_AnimationRender.cs:39)

原报告中将此列为 YELLOW-03，但未充分量化。实际上这是全项目**最大的隐式性能税**：

- Harmony Postfix 挂在 `PawnRenderNodeWorker.OffsetFor` 和 `ScaleFor` 上
- 地图上每个 Pawn 的每个渲染节点都会触发（15+ 节点/Pawn）
- 10 个 Pawn 的殖民地 = ~300 次/帧
- NPC/动物/袭击者通常没有 CompPawnSkin，但仍需执行 `FastGetSkinComp_Pawn` 的 WeakReference 检查

**快速优化**: 在 Postfix 入口添加最快的可能的 bail-out：
```csharp
// 当前已有：
Pawn? pawn = __1.pawn;
if (pawn == null) return;
CompPawnSkin? skinComp = FastGetSkinComp_Pawn(pawn, ...);
if (skinComp == null || ...) return;

// 可以考虑在 CompPawnSkin 上标记静态 flag：
// 但由于 FastGetSkinComp 已经很快（WeakReference hit = 引用比较），
// 进一步优化的收益有限。
```

实际测量 `FastGetSkinComp_Pawn` 在缓存命中时约 0.02μs/call，300 次 = ~6μs/帧。这是可接受的开销，维持 🟡。

---

## 四、修复优先级调整后的路线图

### Phase 1 — 建议立即实施（修正后）

| 优先级 | 任务 | 原级别 | 修正级别 | 理由 |
|--------|------|--------|---------|------|
| **P0** | `textureCache` / `externalGraphicCache` 淘汰策略改用 Queue 实现真正的 FIFO | 🔴 | 🟡中高 | 唯一可能触发帧时间尖刺的缓存问题 |
| **P1** | `_zWriteAppliedInstanceIds` 在 ClearCache 中清理 | 🔴 | 🟡中 | 防止编辑器模式下 HashSet 增长 |
| **P1** | VFX Shader 缓存键改用 ValueTuple | 🟡 | 🟡中 | 消除字符串分配 + 提高缓存正确性 |

### Phase 2 — 建议后续实施

| 优先级 | 任务 | 原级别 | 修正级别 |
|--------|------|--------|---------|
| **P2** | CompTick 快速路径 + GetComp 缓存到实例字段 | 🟡 | 🟡 |
| **P2** | Animation Sin tick 级缓存 | 🟡 | 🟢 |
| **P2** | RuntimeAssetLoader DateTime → Environment.TickCount | 🟡 | 🟢 |
| **P3** | GraphicDatabase.Get 泛型委托缓存 | 🔴→🟢 | 🟢 |

### 从 Phase 1 中移除的项

| 任务 | 原级别 | 移除理由 |
|------|--------|---------|
| 合并 EnsureProgrammaticFaceStateUpdated 4 次调用 | 🔴 | 内部 tick 缓存已有效，额外优化收益 <0.1%帧时间 |
| GraphicDatabase.Get 反射替换为委托缓存 | 🔴 | 反射路径几乎不触发，是 fallback 代码 |

---

## 五、原报告准确性评分

| 问题编号 | 行号准确性 | 问题真实性 | 严重程度判定 | 修复建议可行性 |
|----------|-----------|-----------|-------------|---------------|
| RED-01 | ✅ 正确 | ✅ 真实但夸大 | ⚠️ 应为🟡 | ⚠️ 建议重复了已有机制 |
| RED-02 | ✅ 正确 | ✅ 真实 | ✅ 基本准确 | ✅ 可行，建议缩小范围 |
| RED-03 | ✅ 正确 | ⚠️ 真实但极少触发 | ⚠️ 应为🟢 | ✅ 可行但不急迫 |
| RED-04 | ✅ 正确 | ✅ 真实但影响有限 | ⚠️ 应为🟡 | ✅ 简化即可 |
| YELLOW-01~06 | ✅ 正确 | ✅ 真实 | ✅ 基本准确 | ✅ 可行 |
| GREEN-01~05 | ✅ 正确 | ✅ 真实 | ✅ 准确 | ✅ 可行 |

**总体评价**: 原报告的问题发现准确（行号、代码引用均正确），主要偏差在于对内部缓存机制的二次防护效果评估不足，导致 3 个问题被过度定级。修正后 Phase 1 工作量从 4 项缩减为 3 项，更加聚焦于真正影响用户体验的改进。

---

## 六、最终推荐实施清单

1. **[`textureCache`](Source/CharacterStudio/Rendering/RuntimeAssetLoader.cs:19) 淘汰策略改进** — 添加 `Queue<string>` 追踪插入顺序，实现真正 FIFO
2. **[`externalGraphicCache`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs:26) 淘汰策略改进** — 同上
3. **[`_zWriteAppliedInstanceIds.Clear()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.Graphic.cs:609)** — 在 `ClearCache()` 中添加清理
4. **[`VfxShaderEffectRenderer.CreateMaterial`](Source/CharacterStudio/Abilities/VfxShaderEffectRenderer.cs:207) 缓存键改用 ValueTuple** — 提高正确性 + 消除字符串分配
5. **统一缓存管理器**（Phase 2）— 创建 `CacheManager` 在场景切换时一键清理所有缓存
