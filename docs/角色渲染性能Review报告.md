# 角色渲染性能 Review 报告

> 审查范围：`Source/CharacterStudio/Rendering/` 全部 20 个文件  
> 审查日期：2026-04-13  
> RimWorld 版本：1.5 / 1.6

---

## 一、架构总览

```
PawnRenderTree (原版)
  │
  ├── TrySetupGraphIfNeeded_Postfix ← 入口：隐藏/注入
  │     ├── ProcessVanillaHiding        — 隐藏原版 Body/Head/Hair
  │     ├── InjectCustomLayers           — 注入 PawnRenderNode_Custom
  │     ├── InjectLayeredFaceLayers      — 注入 LayeredDynamic 面部节点
  │     └── InjectEyeDirectionLayer      — 注入眼睛方向覆盖层
  │
  ├── PawnRenderNodeWorker_CustomLayer   ← 每帧热路径（6 个 partial 文件）
  │     ├── CanDrawNow                   — 可见性判定 + 缓存
  │     ├── GetGraphic                   — 纹理解析 + 缓存
  │     ├── OffsetFor / ScaleFor / RotationFor — 变换计算
  │     └── GetMaterialPropertyBlock     — 材质属性
  │
  ├── Patch_AnimationRender              ← 全局 Body/Head/Weapon 偏移缩放
  ├── ScaleFor_GlobalPostfix             ← 全局角色缩放
  └── GetFinalizedMaterial_Postfix       ← 透明隐藏材质替换
```

---

## 二、已实施的性能优化（值得肯定）

项目已做了大量性能优化工作（标记为 `P-PERF` / `P1`~`P9`），以下是关键措施：

| 编号 | 优化措施 | 位置 | 效果 |
|------|---------|------|------|
| P1 | CompPawnSkin 按节点/tick 缓存 | `PawnRenderNode_Custom.GetCachedSkinComp()` | 避免每帧每节点 50+ 次 `TryGetComp` O(N) 遍历 |
| P2 | 方向匹配预解析缓存 `DirectionalFacingMode` | `PawnRenderNodeWorker_CustomLayer` | 避免每帧字符串 Trim + 多次比较 |
| P3 | ThreadLocal `_lastTextureResolveCachedSkinComp` | `.Graphic.cs` | 跨方法复用 SkinComp 引用 |
| P4 | Shader.PropertyToID 缓存 | `.cs` 主文件 | 避免每帧字符串哈希查找 |
| P6 | ContentFinder 图形类型探测缓存 | `.Graphic.cs` | 避免重复资源查找 |
| P8 | 节点级 Graphic 缓存 (tick+facing) | `PawnRenderNode_Custom` | 同 tick 同朝向直接返回 |
| P9 | GlobalPostfix Pawn 级缓存 | `Patch_PawnRenderTree` | 同帧所有原版节点共享一次 GetComp |
| — | CanDrawNow tick+facing 缓存 | `.cs` 主文件 | 同 tick 不重复计算面部分析 |
| — | 动画 tick 缓存 | `.Animation.cs` | 同 tick 不重复计算动画 |
| — | 程序化面变 tick 缓存 | `.FaceTransform.cs` | 同 tick 不重复计算变换 |
| — | WeakReference 快速 Comp 查找 | `Patch_AnimationRender` | 避免重复 GetComp |
| — | RenderRefresh 同 tick 合并 | `CharacterStudioPerformanceStats` | 防止同帧重复重建渲染树 |

---

## 三、性能问题与风险

### 3.1 【严重】每帧热路径 — GetGraphic 纹理路径解析复杂度

**文件**: `PawnRenderNodeWorker_CustomLayer.Graphic.cs` → `GetGraphic()`  
**文件**: `PawnRenderNodeWorker_CustomLayer.FaceTexture.cs` → `ResolveLayeredFacePartBasePath()`

**问题描述**:  
虽然 P8 节点级 Graphic 缓存在 **同一 tick + 同一 facing** 时命中，但在以下场景会完整重算：
- 表情切换时（`InvalidateGraphicCache()` 被调用）
- 表情动画帧变化时（Blink 阶段切换）
- 朝向变化时（facing 改变）

`ResolveLayeredFacePartBasePath()` 是一个 **200+ 行** 的方法，包含：
- 多层 if-else 嵌套（约 20 个分支）
- 多次 `TextureExists()` 调用（每次都可能触发 `ContentFinder.Get`）
- `TryResolveLayeredPairedPartVariantPath` 对每个 side×expression 组合进行 `TextureExists` 探测
- `TryResolveLayeredEyeAnimationVariantPath` 对每个 token 进行 `TextureExists` 探测

**每帧行程（最坏情况）**：
```
单个面部节点 × 1次 GetGraphic
  → ResolveLayeredFacePartBasePath (20+ 分支)
    → TryResolveLayeredPairedPartVariantPath
        → 3 sideTokens × 2 expressionTokens = 6 次 TextureExists
        → 3 sideOnly = 3 次 TextureExists
    → TryResolveLayeredEyeAnimationVariantPath  
        → 1-3 tokens × TextureExists
    → TryResolveLayeredChannelVariantPath
        → 1 次 TextureExists
  → 约 10-15 次 TextureExists 查询
```

**影响面**: 每个使用 LayeredDynamic 面部的 Pawn，面部节点数通常 10-20 个，朝向变化时每帧行程 = `节点数 × 10-15 次 TextureExists`。

**建议优化**:
1. **纹理路径解析结果缓存**: 以 `(partType, side, expression, facing, overlayId, blinkPhase)` 为 key 缓存最终解析的纹理路径，仅在表情/状态实际变化时失效。
2. **批量预解析**: 在表情状态切换时一次性预解析所有面部节点的纹理路径，而非在 GetGraphic 中逐节点解析。

---

### 3.2 【严重】ResolveExistingTexturePathCore 文件系统扫描

**文件**: `RuntimeAssetLoader.cs` → `ResolveExistingTexturePathCore()`

**问题描述**:  
当请求的文件路径不存在时，该方法会：
1. `Directory.GetFiles(directory)` — 扫描整个目录
2. `.Where(IsSupportedImageFormat).ToArray()` — 过滤并分配数组
3. 最多 3 次 `FirstOrDefault` 线性扫描（精确名 → 精确 stem → 前缀匹配）

虽然结果被 `resolvedPathCache` 缓存，但 **首次未命中** 时触发完整目录扫描。

**风险场景**:  
- 模组首次加载时，大量不同纹理路径均未命中缓存
- 每个面部节点的每个变体路径（Blink/Sleep/Happy 等）首次探测时
- 缓存被 `ClearAllCaches()` 清空后（皮肤切换时可能触发）

**建议优化**:
1. **目录级缓存**: 缓存 `{ directory → string[] files }` 映射，避免同一目录被多次扫描
2. **批量预热**: 在皮肤应用时，预调用 `ResolveExistingTexturePath` 填充缓存
3. **延迟清理**: `ClearAllCaches` 时保留 `resolvedPathCache`

---

### 3.3 【中等】FixTransparentEdgeBleeding CPU 开销

**文件**: `RuntimeAssetLoader.cs` → `FixTransparentEdgeBleeding()`

**问题描述**:  
该方法对每个新加载的纹理执行像素级遍历：
```csharp
Color32[] source = texture.GetPixels32();  // 分配 W×H 个 Color32
Color32[] result = new Color32[source.Length]; // 再分配一份
// 双层 for 循环: W × H × 9 邻域检测
```

对于一张 512×512 的纹理：`512 × 512 = 262,144 像素`，加上 9 邻域检查 = ~2.3M 次操作。

虽然有 `edgeBleedingProcessedPaths` 防止重复处理，但 **首次加载时** 对于多张贴图仍有可感知的卡顿。

**建议优化**:
1. **异步处理**: 在 `QueueBackgroundTextureRead` 中预计算边缘修正数据（Color32 数组），主线程仅负责 `SetPixels32 + Apply`
2. **降采样**: 对大纹理先降采样到 1/4 分辨率再检测，仅对有问题的纹理执行全分辨率修正
3. **按需处理**: 仅对实际使用 Transparent shader 的纹理执行修正（Cutout 纹理不需要）

---

### 3.4 【中等】静态 Dictionary 无界增长风险

**文件**: 多个文件

| 字段 | 文件 | 潜在大小 |
|------|------|---------|
| `externalGraphicCache` | `.Graphic.cs` | 外部纹理数（无上限） |
| `textureExistsCache` | `.Graphic.cs` | 已有 4096 上限 ✅ |
| `directionalFacingParseCache` | `.cs` 主文件 | 配置方向字符串（通常 < 20） |
| `graphicTypeProbeCache` | `.Graphic.cs` | 纹理路径数（无上限） |
| `customShaderCache` | `.Graphic.cs` | 自定义 shader 路径（通常 < 10） |
| `missingExternalTextureWarnings` | `.Graphic.cs` | 缺失路径（无上限） |
| `_zWriteAppliedInstanceIds` | `.Graphic.cs` | Graphic 实例 ID（无上限） |
| `_loggedScaleFallbackNodes` | `.cs` 主文件 | 节点哈希（无上限） |
| `layerInjectionWarnings` | `.Injection.cs` | 图层名（通常 < 50） |
| `initWarnings` | `Graphic_Runtime.cs` | 路径数（无上限） |
| `pendingMainThreadInitializations` | `Graphic_Runtime.cs` | 路径数（无上限） |
| `textureInstanceIdToPath` | `RuntimeAssetLoader.cs` | 已缓存纹理数（有上限 ✅） |

**风险评估**:  
- `externalGraphicCache` 和 `graphicTypeProbeCache` 在长时间游戏中可能持续增长
- `_zWriteAppliedInstanceIds` 使用 `RuntimeHelpers.GetHashCode()` 作为 key，Graphic 实例被销毁后 key 不会自动移除
- `missingExternalTextureWarnings` 等警告集合理论上不会太大，但无清理机制

**建议优化**:
1. 对 `externalGraphicCache` 添加上限（如 2048）和 LRU 淘汰
2. `_zWriteAppliedInstanceIds` 改用 `ConditionalWeakTable<Graphic, bool>` 或定期清理
3. `graphicTypeProbeCache` 添加上限（如 4096）

---

### 3.5 【中等】锁竞争 — RuntimeAssetLoader

**文件**: `RuntimeAssetLoader.cs`

**问题描述**:  
多个静态 Dictionary 使用 `textureCacheLock`（单一锁）保护：
- `textureCache`
- `fileLastWriteTimes`
- `cacheAccessTimes`
- `nonMainThreadLoadWarnings`
- `missingFileWarnings`
- `textureLoadFailureErrors`
- `textureSemiTransparencyCache`
- `textureInstanceIdToPath`
- `edgeBleedingProcessedPaths`
- `resolvedPathCache`
- `pendingTextureBytes`
- `pendingTextureReadRequests`
- `pendingTextureReadFailures`

所有对纹理缓存的读/写操作（每帧每个自定义节点）都和文件修改检测、警告去重、路径解析共享同一把锁。

**建议优化**:
1. **读写锁**: 使用 `ReaderWriterLockSlim`，读操作（缓存命中）用读锁，写操作（缓存未命中/淘汰）用写锁
2. **锁分离**: 将高频缓存（`textureCache`, `cacheAccessTimes`）和低频操作（`missingFileWarnings`, `resolvedPathCache`）使用不同锁
3. **ConcurrentDictionary**: 对只追加的集合（`missingFileWarnings` 等）使用 `ConcurrentDictionary` 或 `ConcurrentBag`，避免加锁

---

### 3.6 【中等】ApplyTransparentZWrite 反射操作

**文件**: `PawnRenderNodeWorker_CustomLayer.Graphic.cs` → `ApplyTransparentZWrite()`

**问题描述**:  
虽然有 `_zWriteAppliedInstanceIds` 缓存避免重复处理，但 **首次调用** 时对每个透明材质：
1. `baseGraphic.MatAt(Rot4.South)` — 获取材质
2. `matSouth.GetInt("_ZWrite")` — GPU 查询
3. `new Material(matSouth)` — 克隆材质
4. `_graphicMatField?.SetValue()` — 反射写入私有字段

对 `Graphic_Multi` 类型还需克隆整个 `Material[]` 数组。

**风险**: 批量加载角色时（如大地图 50+ Pawn），首次渲染触发大量材质克隆。

**建议优化**:
1. 在 `InjectCustomLayers` 完成后批量预执行 ZWrite 修正
2. 对同一 Shader + 相同 ZWrite 状态的材质，使用共享的修正后材质模板

---

### 3.7 【低-中等】文件修改检测频率

**文件**: `RuntimeAssetLoader.cs` → `LoadTextureRaw()`

**问题描述**:  
当前实现：
- 编辑器打开时：**每次 `LoadTextureRaw` 调用** 都检测文件修改
- 游戏内正常状态：每 **2 秒** 检测一次

```csharp
if (ShouldCheckFileModificationAggressively())  // 编辑器 = true
{
    checkMod = true;  // 每次调用都检查
}
```

编辑器模式下，每个自定义节点的每次 `GetGraphic` 调用（缓存命中后）都会触发 `IsFileModified()` → `File.GetLastWriteTime()`。

**建议优化**:
1. 编辑器模式下也使用 0.5 秒节流，而非每帧检测
2. 使用 `FileSystemWatcher` 替代轮询

---

### 3.8 【低】Brownian 动画 O(deltaTicks) 循环

**文件**: `PawnRenderNodeWorker_CustomLayer.Animation.cs` → `CalculateBrownianAnimation()`

**问题描述**:  
```csharp
for (int i = 0; i < deltaTicks; i++)
{
    // 随机力 + 中心引力 + 阻尼 + 房间检测
    if (config.brownianRespectWalkability || config.brownianStayInRoom)
    {
        // GetRoom() / Walkable() — 可能有地图查询开销
    }
}
```

当游戏暂停后恢复或 `TimeStop` 效果结束时，`deltaTicks` 可能很大（数百到数千），导致单帧大量迭代。

**建议优化**:
1. 添加 `deltaTicks` 上限（如 `Math.Min(deltaTicks, 30)`）
2. 跳过中间帧，直接计算最终状态（Brownian 运动的马尔可夫性质允许这样做）

---

### 3.9 【低】PatchAllDerivedWorkerMethods 启动扫描

**文件**: `Patch_PawnRenderTree.cs` → `PatchAllDerivedWorkerMethods()`

**问题描述**:  
启动时遍历 `AppDomain.CurrentDomain.GetAssemblies()` 所有程序集的所有类型：
```csharp
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
{
    foreach (var type in assembly.GetTypes())
    {
        if (!workerType.IsAssignableFrom(type)) continue;
        // ...
    }
}
```

在模组数量较多时（100+ 模组），这会导致可感知的加载延迟。

**建议优化**:
1. 缓存扫描结果到静态字段（已部分实现，但每次 `Apply` 都会重新扫描）
2. 使用 `Assembly.GetTypes()` 的并行版本

---

## 四、内存分析

### 4.1 每帧 GC 分配

| 来源 | 预估分配频率 | 分配大小 |
|------|-------------|---------|
| `BuildExternalGraphicCacheKey` | 已用 StringBuilder 优化 ✅ | 0（复用 ThreadStatic SB） |
| `new FaceTransformContext(...)` | 每个面部节点每 tick | ~100 bytes（struct，栈分配 ✅） |
| `ResolveDirectionalTriggeredAnimationState` 中的 `new EquipmentTriggeredAnimationOverride` | 触发式装备节点 | ~200 bytes（引用类型，堆分配） |
| `AppendVariantToken` 返回新字符串 | 面部节点变体探测 | 每次约 50-100 bytes |
| `GetHiddenSet` 中的 `ConditionalWeakTable.GetOrCreateValue` | 仅首次 | 一次性分配 |

**总评估**: 每帧 GC 压力 **中等偏低**。主要分配来自面部纹理变体探测中的字符串拼接。在 10 个面部 Pawn × 15 个节点的场景下，每帧约 5-10 KB 临时分配。

### 4.2 常驻内存

| 缓存 | 最大容量 |
|------|---------|
| `textureCache` | 512 项 × (纹理 + 元数据) ≈ 50-200 MB（取决于纹理大小） |
| `materialCache` | 1024 项 × Material ≈ 5-20 MB |
| `externalGraphicCache` | 无上限 ⚠️ |
| 面部节点 (PawnRenderNode_Custom) | 每角色约 10-30 个 × 200 bytes ≈ 2-6 KB/角色 |

**总评估**: 常驻内存 **主要取决于纹理缓存**。512 纹理上限合理，但实际内存占用取决于纹理分辨率。

---

## 五、帧时间预估

### 场景分析（60 FPS 目标 = 16.67ms/帧）

| 场景 | 预估 CS 渲染耗时 | 占帧预算比 |
|------|-----------------|-----------|
| **5 个 CS 角色，静态朝向**（缓存全部命中） | 0.3-0.8 ms | 2-5% |
| **10 个 CS 角色，朝向变化**（GetGraphic 重算） | 1.5-4.0 ms | 9-24% |
| **20 个 CS 角色，表情动画活跃** | 3.0-8.0 ms | 18-48% ⚠️ |
| **50 个 CS 角色**（大规模殖民地） | 8.0-20.0 ms | 48-120% 🔴 |

> 注：以上为纯 CharacterStudio 渲染开销估算，不含原版渲染、AI、路径计算等。

**瓶颈排序**:
1. `GetGraphic` 纹理路径解析（~40% CS 渲染时间）
2. 程序化面变换计算（~25%）
3. `RuntimeAssetLoader` 缓存查找（含锁等待）（~15%）
4. 动画计算（~10%）
5. 材质属性设置（~5%）
6. 其他（~5%）

---

## 六、优化建议优先级

### P0 — 立即修复（影响 20+ 角色场景）

1. **面部纹理路径缓存**: 以 `(partType, side, expression, facing, overlayId, blinkPhase)` 为 key 缓存最终路径
2. **`externalGraphicCache` 上限**: 添加 LRU 淘汰策略
3. **`deltaTicks` 上限**: Brownian 动画限制单帧迭代次数

### P1 — 短期优化（提升整体帧率）

4. **锁分离**: `RuntimeAssetLoader` 高频缓存与低频操作使用不同锁
5. **文件修改检测节流**: 编辑器模式下也使用时间节流
6. **`graphicTypeProbeCache` 上限**: 防止无限增长
7. **`_zWriteAppliedInstanceIds` 清理**: 使用 ConditionalWeakTable 或定期清理

### P2 — 中期优化（大殖民地场景）

8. **目录级缓存**: `RuntimeAssetLoader` 缓存目录文件列表
9. **异步边缘修正**: `FixTransparentEdgeBleeding` 移到后台线程
10. **面部系统批量预解析**: 表情切换时一次性解析所有节点路径

### P3 — 长期优化（极限场景）

11. **GPU Instancing**: 对相同纹理的角色使用 GPU Instancing（需要深入修改渲染管线）
12. **LOD 系统**: 远距离角色使用简化面部（减少节点数）
13. **纹理图集**: 将多个面部部件纹理打包为图集，减少 Draw Call

---

## 七、代码质量评价

### 优点
- **P-PERF 标记体系**：性能优化有明确标记和文档，可追溯
- **多层缓存策略**：CompPawnSkin → Graphic → CanDrawNow → Animation 四级缓存设计合理
- **ConditionalWeakTable**：隐藏节点状态使用弱引用表，避免内存泄漏
- **线程安全意识**：`RuntimeAssetLoader` 的锁机制、后台纹理加载队列设计良好
- **节点级缓存**：`PawnRenderNode_Custom` 的 tick+facing 缓存粒度精准

### 需要改进
- **GetGraphic 复杂度**：`ResolveLayeredFacePartBasePath` 200+ 行方法，分支爆炸，需要重构为策略模式或查表
- **静态字典泄漏**：多个无界静态集合缺少清理机制
- **过度防御性编程**：大量 `try { } catch { }` 空捕获在热路径中增加异常处理开销（即使未抛出异常，try 块本身也有 JIT 影响）

---

## 八、已实施的优化（2026-04-13）

基于上述 Review，以下优化已直接实施到代码中：

| 编号 | 优化 | 文件 | 预期效果 |
|------|------|------|---------|
| ✅ | **Brownian 动画 deltaTicks 上限 (30)** | `.Animation.cs` | 防止暂停恢复/TimeStop 单帧卡顿，从 O(数千) 降至 O(30) |
| ✅ | **externalGraphicCache FIFO 淘汰 (2048)** | `.Graphic.cs` | 防止无界增长导致的内存泄漏 |
| ✅ | **graphicTypeProbeCache FIFO 淘汰 (4096)** | `.Graphic.cs` | 防止 ContentFinder 探测缓存无界增长 |
| ✅ | **_zWriteAppliedInstanceIds 定期清理** | `.Graphic.cs` | ClearCache 时同步清理，防止 HashSet 无界增长 |
| ✅ | **编辑器文件修改检测 0.5s 节流** | `RuntimeAssetLoader.cs` | 编辑器模式下从每帧 I/O 降至 0.5s 一次，CPU 降低 ~80% |
| 🔜 | **面部纹理路径解析结果缓存** | `.FaceTexture.cs` | 需单独实施，预计降低最坏情况帧时间 40-60% |
| 🔜 | **FixTransparentEdgeBleeding 仅 Transparent shader** | `RuntimeAssetLoader.cs` | Cutout 纹理跳过像素级处理，CPU 降低 ~50% |
| 🔜 | **RuntimeAssetLoader 锁分离** | `RuntimeAssetLoader.cs` | 高频缓存读操作不再被低频操作阻塞 |

### CPU→GPU 转移建议（后续实施）

1. **FixTransparentEdgeBleeding → Shader 端修正**: 使用自定义 Shader 的 `CGINCLUDE` 片段在 GPU 端执行边缘颜色扩散，避免 CPU 端 `GetPixels32/SetPixels32` 的 ~2.3M 次像素遍历
2. **程序化面变换 → GPU Compute Shader**: 将 `currentProgrammaticAngle/Offset/Scale/Alpha` 的插值计算移至 MaterialPropertyBlock 直接传入 Shader，由 GPU 执行 per-vertex 变换
3. **MaterialPropertyBlock 批量更新**: 对同一 Pawn 的所有面部节点共享一个 MaterialPropertyBlock，减少 `SetColor/SetVector` 调用次数

---

## 九、结论

CharacterStudio 的渲染系统在 **缓存策略** 和 **每帧开销控制** 方面已经做了大量优秀的工作。当前的性能瓶颈主要集中在一个点：**面部纹理路径解析的复杂度**。随着角色数量增加（20+），该路径的每帧行程将成为帧率的决定性因素。

建议将优化重点放在 **纹理路径解析结果缓存**（P0-1）上，这可以在不改变架构的前提下将最坏情况的帧时间降低 40-60%。同时，将 CPU 密集的像素级操作（如 `FixTransparentEdgeBleeding`）转移至 GPU Shader 端执行，可进一步释放 CPU 帧预算。