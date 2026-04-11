# CharacterStudio 运行时性能评估报告

> 评估范围：通过该模组创建/应用皮肤的角色在游戏内运行时的性能消耗  
> 评估日期：2026-04-09

---

## 一、总体评价

CharacterStudio 在运行时的性能消耗总体处于 **中等偏高** 水平。对于少量角色（1-5个），影响可忽略；但随着使用皮肤的角色数量增加至 10+ 或使用了面部动画系统（LayeredDynamic），性能影响会显著放大。

**核心瓶颈不在单次操作的计算量，而在于调用频率的乘法效应**：
- 每个带皮肤的 Pawn × 每个自定义渲染节点 × 每帧渲染 × 多次 TryGetComp 查找

---

## 二、热路径分级分析

### 🔴 高频热路径（每帧×每节点，最高优先级）

#### H1: `TryGetComp<CompPawnSkin>` 重复查找 — 51 处调用

**位置**: `PawnRenderNodeWorker_CustomLayer.cs` 及其 partial 文件  
**频率**: 每帧 × 每个自定义渲染节点 × 单次渲染调用链中多次  
**问题**:
- `TryGetComp<T>` 在 RimWorld 内部是 O(N) 线性搜索 comps 列表
- 单个 Pawn 的一次渲染帧内，同一个节点的 `CanDrawNow`→`OffsetFor`→`ScaleFor`→`RotationFor`→`GetMaterialPropertyBlock`→`GetGraphic` 调用链中，多个方法各自独立调用 `TryGetComp`
- 一个典型皮肤有 5-15 个自定义图层节点，启用面部系统后可达 20-30 个
- **每帧每 Pawn 可产生 50-200+ 次 `TryGetComp` 调用**

**影响**: ⚠️ 中高。单次调用 ~0.1μs，但累计效应明显，是本模组最大的系统性开销源。

**优化建议**: 在 `PawnRenderNode_Custom` 上缓存 `CompPawnSkin` 引用，或在 Worker 方法间通过参数传递。

---

#### H2: `EnsureProgrammaticFaceStateUpdated` 多次调用

**位置**: `PawnRenderNodeWorker_CustomLayer.cs` 第 90, 162, 303, 350 行  
**频率**: 每帧 × 每个面部图层节点 × 4次（CanDrawNow + OffsetFor + GetMaterialPropertyBlock + RotationFor）  
**问题**:
- 该方法在单个节点的同一帧渲染中被 4 个不同方法分别调用
- 虽然内部应有 dirty flag 保护，但方法调用本身的开销（参数传递、null 检查链）仍然累积

**影响**: ⚠️ 中。依赖内部 dirty flag 优化，实际计算不重复，但调用开销存在。

---

#### H3: `MatchesDirectionalFacing` 字符串比较

**位置**: `PawnRenderNodeWorker_CustomLayer.cs` 第 102-138 行  
**频率**: 每帧 × 每个自定义节点（CanDrawNow 中调用）  
**问题**:
- 使用 `string.Equals(..., StringComparison.OrdinalIgnoreCase)` 做方向匹配
- 对 6 种方向值做串行比较，最坏情况需要 6 次字符串比较
- `config.directionalFacing?.Trim()` 每帧创建新字符串

**影响**: ⚠️ 低-中。字符串比较本身很快，但 `Trim()` 每帧分配新字符串是不必要的。

**优化建议**: 在 config 加载时预处理为枚举值，运行时用整数比较。

---

### 🟡 中频热路径（每帧 × 每 Pawn 或 每 tick × 每 Pawn）

#### M1: `GetFinalizedMaterial_Postfix` — 全局材质 Postfix

**位置**: `Patch_PawnRenderTree.cs` 第 235-257 行  
**频率**: 每帧 × **所有 Pawn 的所有渲染节点**（不仅限于有皮肤的 Pawn）  
**问题**:
- 这是 Harmony Postfix，挂在 `PawnRenderNodeWorker.GetFinalizedMaterial` 上
- **对所有 Pawn 的所有节点都会触发**，即使 Pawn 没有安装皮肤
- 每次调用检查 `hiddenNodesByTree` 字典查找
- 有 `node is PawnRenderNode_Custom` 的早期退出（很高效）

**影响**: ⚠️ 中。字典查找 O(1) 但调用频率极高（可能 1000+/帧）。短路逻辑设计合理。

---

#### M2: `StatWorker.GetValue` Postfix — 属性修正

**位置**: `Patch_CharacterAttributeBuffStat.cs` 第 38-52 行  
**频率**: 每次 **任何 Pawn 的任何 Stat 计算**  
**问题**:
- **反射调用** `StatWorkerStatField.GetValue(__instance)` 在每次 stat 计算时执行
- 即使 Pawn 没有任何 CS 属性 Buff，也要执行完整路径：反射取 stat → GetComp → 获取 profile → 遍历 entries
- `GetActiveEntries` 使用 `yield return` 迭代器，每次调用分配 IEnumerator
- Stat 计算在 RimWorld 中极其频繁（移动、工作、战斗、UI 信息面板等）

**影响**: ⚠️⚠️ 高。这可能是 **整个模组在运行时最大的单一性能开销**，因为：
1. 触发频率最高（远超渲染帧，stat 计算在游戏逻辑和 UI 中无处不在）
2. 反射调用每次 ~1μs
3. 对无皮肤 Pawn 也完全执行（没有早期退出）

**优化建议**: 
- 添加 Pawn 级早期退出：`if (!pawn.HasComp<CompPawnSkin>()) return;`（但 HasComp 本身也是 O(N)）
- 缓存 `StatWorkerStatField.GetValue` 结果（可用 ConditionalWeakTable 或 per-frame 缓存）
- 将 `GetActiveEntries` 从 yield return 改为直接返回 list/span

---

#### M3: `CompPawnSkin.CompTick` — 每 tick 面部状态

**位置**: `CompPawnSkin.cs` 第 728-742 行  
**频率**: 每 tick × 每个有皮肤的 spawned Pawn  
**调用链**:
```
CompTick()
├── TickForcedMove(pawn)          // 通常 noop
├── FlushDeferredRefresh(this)     // 通常 noop
├── TickAbilityFaceOverride()      // 快速检查
└── FaceRuntimeTickCoordinator.Tick()
    ├── EnsureFaceRuntimeStateUpdated()
    ├── AdvanceAnimTick()
    ├── ClearExpiredShock()         // 整数比较
    ├── UpdateExpressionState()    // 每 30 tick（IsHashIntervalTick）
    ├── UpdateAnimatedExpressionFrame()
    ├── UpdateBlinkLogic()
    ├── UpdateEyeAnimationVariant()
    ├── UpdateEyeDirectionState()  // 每 15 tick
    └── UpdateGlobalFaceDriveState()
```

**影响**: ✅ 低-中。设计合理，重计算已做了 tick 间隔节流（30/15 tick）。每 tick 的基础开销约 ~1-2μs/Pawn。

---

#### M4: `TrySetupGraphIfNeeded_Postfix` — 渲染树注入

**位置**: `Patch_PawnRenderTree.cs` 第 263-353 行  
**频率**: 仅在渲染树初始化/重建时触发（非每帧）  
**问题**:
- `CheckForCustomNodes` 递归遍历整棵渲染树检查是否已注入
- `TryGetGeneSkin` 遍历 Pawn 的所有基因
- `RenderFixPatchRegistry.GetApplicablePatches(pawn).Any()` — 可能涉及 LINQ

**影响**: ✅ 低。触发频率低（仅在树重建时），单次开销可接受。

---

### 🟢 低频路径（事件驱动 / 间隔触发）

#### L1: `CharacterRuntimeTriggerComponent.GameComponentTick` — 每 60 tick

**位置**: `CharacterRuntimeTriggerComponent.cs` 第 50-59, 102-141 行  
**频率**: 每 60 game tick  
**问题**:
- 使用 LINQ (`.Where().OrderBy().ThenBy()`) 创建排序的 trigger 枚举
- 每次评估分配临时迭代器
- trigger 数量通常很少（0-5），所以实际开销极小
- `BuildMapScopedKey` 每次调用 `$"{triggerDefName}|{mapId}"` 字符串拼接

**影响**: ✅ 极低。60 tick 间隔 + trigger 数量少，LINQ 开销可忽略。

---

#### L2: `AbilityHotkeyRuntimeComponent.GameComponentUpdate` — 每 Unity 帧

**位置**: `AbilityHotkeyRuntimeComponent.cs` 第 76-100 行  
**频率**: 每 Unity 渲染帧  
**问题**: 仅做 Input.GetKeyDown 检测，极其轻量。

**影响**: ✅ 极低。

---

#### L3: `PawnSkinBootstrapComponent` — 仅游戏初始化

**位置**: `PawnSkinBootstrapComponent.cs`  
**频率**: LoadedGame / StartedNewGame / FinalizeInit  
**设计**: 世界签名去重 + Pawn ID 集合防重复处理

**影响**: ✅ 极低。一次性开销，优化已做到位。

---

#### L4: `Patch_AbilityRuntimeTick` — 每个 Ability tick

**位置**: `Patch_AbilityRuntimeTick.cs` 第 18-27 行  
**频率**: 每 tick × 每个 Ability  
**问题**: `CompOfType<CompAbilityEffect_Modular>()` 对每个 Ability 调用，大多数返回 null。

**影响**: ✅ 低。`CompOfType` 是简单列表遍历，Modular comp 通常只在 CS 技能上存在。

---

## 三、定量估算（10 个带皮肤的 Pawn，每个 10 个自定义图层）

| 路径 | 频率 | 单次开销 | 每帧/tick 总计 |
|------|------|---------|---------------|
| H1: TryGetComp 查找 | ~1000/帧 | ~0.1μs | ~100μs/帧 |
| M2: StatWorker Postfix | ~500-2000/tick | ~1-2μs | ~500-4000μs/tick |
| H2: EnsureProgrammaticFaceState | ~400/帧 | ~0.05μs | ~20μs/帧 |
| M1: GetFinalizedMaterial Postfix | ~200-500/帧 | ~0.1μs | ~20-50μs/帧 |
| H3: MatchesDirectionalFacing | ~100/帧 | ~0.2μs | ~20μs/帧 |
| M3: CompPawnSkin.CompTick | 10/tick | ~2μs | ~20μs/tick |
| 其他低频 | — | — | <5μs |
| **渲染帧合计** | | | **~160-190μs/帧** |
| **逻辑 tick 合计** | | | **~520-4020μs/tick** |

> 注: 60FPS = 16.6ms/帧预算，1× 速度 = ~16.6ms/tick  
> 渲染开销约占帧预算 1-1.2%（10 Pawn），可接受  
> StatWorker 补丁是主要风险点，在高 stat 计算场景可占 tick 预算的 3-25%

---

## 四、严重性排序与优化建议

### 🔴 P0 — StatWorker Postfix 反射 + 无差别拦截
**文件**: `Patch_CharacterAttributeBuffStat.cs`  
**问题**: 对所有 Pawn 的所有 stat 计算都执行反射 + 完整属性遍历  
**建议**:
1. 添加 Pawn 级快速跳过：维护一个 `HashSet<int>` 记录有活跃属性 buff 的 PawnID
2. 缓存 `StatDef` 引用避免每次反射
3. 将 `GetActiveEntries` 的 yield return 改为直接 list 访问

### 🟡 P1 — TryGetComp 重复查找
**文件**: `PawnRenderNodeWorker_CustomLayer*.cs`  
**问题**: 51 处 TryGetComp 调用，同一帧同一 Pawn 反复查找  
**建议**:
1. 在 `PawnRenderNode_Custom` 上缓存 `CompPawnSkin?` 引用（弱引用或按需更新）
2. 或在 Worker 层引入 per-frame cached comp 模式

### 🟢 P2 — MatchesDirectionalFacing 字符串比较
**文件**: `PawnRenderNodeWorker_CustomLayer.cs`  
**问题**: `Trim()` 每帧分配，6 路字符串比较  
**建议**: config 加载时预计算为枚举值

### 🟢 P3 — RuntimeTrigger LINQ
**文件**: `CharacterRuntimeTriggerComponent.cs`  
**问题**: .Where().OrderBy().ThenBy() 每 60 tick 分配迭代器  
**建议**: 预排序 trigger 列表，运行时直接遍历

---

## 五、与同类模组的对比评估

| 维度 | CharacterStudio | 典型外观模组 | 评价 |
|------|----------------|-------------|------|
| 每 Pawn Tick 开销 | ~2μs (面部动画) | 0 | 略高但已做节流 |
| 每帧渲染开销 | ~16-19μs/Pawn | ~2-5μs/Pawn | 较高，主要因 TryGetComp |
| 全局 Harmony Patch | 2个高频 Postfix | 通常 0-1 个 | 中等 |
| Stat 拦截 | 全局 Postfix + 反射 | 通常无 | **显著偏高** |
| 内存占用 | 512 纹理 + 1024 材质缓存 | ~100 纹理 | 偏高但合理 |
| 初始化开销 | Bootstrap 签名去重 | 简单遍历 | 优秀 |

---

## 六、结论

1. **StatWorker Postfix 是最大的性能风险点**，因为它对游戏中所有 Pawn 的所有 stat 计算都无差别拦截，且使用反射。这远超渲染层面的开销。

2. **渲染层面的开销可控**，面部动画 tick 已有合理的间隔节流，渲染 Worker 的主要问题是 `TryGetComp` 的重复查找。

3. **对于少量角色（1-5个），总体性能影响在可接受范围内**。10+ 个皮肤角色 + 高 stat 计算场景可能产生可感知的性能下降。

4. **已完成的 P0-P8 优化有效解决了纹理加载和缓存管理的问题**，不会再出现纹理丢失/重加载循环。

5. **推荐优先修复 StatWorker Postfix（P0）和 TryGetComp 重复查找（P1）**，预计可减少 30-50% 的运行时 CPU 开销。
