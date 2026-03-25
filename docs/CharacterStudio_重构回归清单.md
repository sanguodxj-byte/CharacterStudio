# CharacterStudio 重构回归清单

## 文档目的

本文档用于支撑 CharacterStudio 的分步重构工作。  
原则是：

- 每完成一步重构，都至少执行一次 review
- 每次 review，都基于同一套核心检查项
- 优先验证高价值路径，而不是临时凭感觉测试

本文档对应重构路线图中的 **P0：建立重构护栏与度量基础**。

---

# 一、使用方式

每完成一个重构步骤后，按以下顺序执行：

1. 记录本轮改动范围
2. 勾选需要验证的功能路径
3. 执行最小回归测试
4. 记录结果
5. 输出本轮 review 结论：
   - 是否通过
   - 风险点在哪里
   - 下一步建议做什么

---

# 二、本轮重构记录模板

## 重构步骤名称
- 

## 目标
- 

## 涉及文件
- 

## 预期不变行为
- 

## 允许变化行为
- 

## 本轮 review 结论
- [ ] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

## review 摘要
- 

---

# 二点五、已完成步骤 Review 记录

## Step 01：ModEntryPoint 补丁注册隔离

### 重构步骤名称
- 将 `ModEntryPoint.ApplyPatches()` 从“单个 try/catch 包裹全部补丁”改为“每个补丁独立隔离注册”

### 目标
- 避免单个补丁注册失败时阻断后续补丁
- 提高初始化阶段的容错性
- 为后续渲染/运行时补丁治理提供更清晰的日志边界

### 涉及文件
- `Source/CharacterStudio/ModEntryPoint.cs`

### 预期不变行为
- Harmony 仍正常初始化
- PawnRenderTree / Gizmo / WeaponRender / RaceLabel 等补丁仍按原顺序尝试注册
- 正常情况下模组初始化行为不变

### 允许变化行为
- 初始化日志会从“统一成功/失败”变为“每个补丁单独记录”
- 某个补丁失败时，其余补丁仍继续尝试注册

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj"`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 2 warning**
- 当前警告位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告为既有空值警告，与本次 `ModEntryPoint` 重构无直接关系
- 说明本次改动未破坏编译链路，且入口初始化结构更稳健

### 影响范围评估
- 影响范围集中在模组初始化阶段
- 不直接改变运行时渲染、编辑器逻辑、文档编译逻辑
- 属于低风险结构性改进

### 当前风险观察
- 尚未做游戏内运行验证，因此还不能确认每个补丁的运行时副作用完全不变
- 但从编译结果与代码行为看，本轮改动可视为安全

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 进入 `CompPawnSkin` 第一轮小步拆分
- 优先选择**预览覆盖状态**或**技能热键运行时状态**这类局部职责做抽离
- 保持“小步重构 + 编译验证 + review 记录”的节奏

---

## Step 02：CompPawnSkin 预览覆盖状态抽离（第一轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中分散的预览覆盖字段收敛为内部状态对象 `FacePreviewOverrideState`

### 目标
- 降低 `CompPawnSkin` 顶层字段数量
- 先从低风险的预览覆盖职责开始拆分
- 为后续继续拆分表情状态、眼球方向状态、技能运行时状态建立模式

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有类：`FacePreviewOverrideState`
- 收敛以下预览覆盖状态：
  - `PreviewExpression`
  - `PreviewEyeDirection`
  - `PreviewMouthState`
  - `PreviewLidState`
  - `PreviewBrowState`
  - `PreviewEmotionOverlayState`
- 将以下方法改为通过状态对象读写：
  - `SetPreviewExpressionOverride`
  - `SetPreviewEyeDirection`
  - `SetPreviewMouthState`
  - `SetPreviewLidState`
  - `SetPreviewBrowState`
  - `SetPreviewEmotionOverlayState`
  - `ClearPreviewChannelOverrides`
  - `GetEffectiveExpression`
  - `GetEffectiveMouthState`
  - `GetEffectiveLidState`
  - `GetEffectiveBrowState`
  - `GetEffectiveEmotionOverlayState`
  - `CurEyeDirection`

### 预期不变行为
- 预览覆盖 API 对外行为不变
- Blink 与表情覆盖逻辑不变
- EyeDirection 预览覆盖逻辑不变
- Mouth / Lid / Brow / Emotion 通道覆盖逻辑不变
- 相关渲染刷新时机保持不变

### 允许变化行为
- `CompPawnSkin` 内部状态组织方式变化
- 字段访问从“直接字段”变为“状态对象属性”
- 同值重复设置时不再产生冗余刷新请求

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 2 warning**
- 当前警告仍位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告与本次 `CompPawnSkin` 预览覆盖状态抽离无直接关系
- 说明本轮改动未破坏编译链路，且职责收拢方向正确

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的编辑器预览覆盖逻辑
- 不直接改变 `CompPawnSkin` 的皮肤绑定、Xenotype 同步、技能 CD、R 二段机制等其他职责
- 属于低风险的“内部状态收束型”重构

### 当前风险观察
- 当前只是“类内聚合”，还不属于真正独立模块
- `FacePreviewOverrideState` 仍然作为嵌套类存在于 `CompPawnSkin` 内部
- 尚未做游戏内手工验证，因此还不能完全确认预览 UI 的刷新表现与之前百分百一致
- 但从代码路径与编译结果判断，可作为安全的第一轮职责拆分

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先处理以下两条路线之一：
  1. 抽离 `AbilityHotkeyRuntimeState`
  2. 抽离表情/眨眼状态控制逻辑
- 若继续保持低风险节奏，建议先抽离 **技能热键运行时状态**，因为它与预览覆盖逻辑边界更清晰
- 保持“改一小步 -> build -> review 记录”节奏，不要跨太多职责同时修改

---

## Step 03：CompPawnSkin 技能热键运行时状态抽离（第二轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中 Q/W/E/R 热键状态、R 二段机制状态、施法窗口状态收敛为内部状态对象 `AbilityHotkeyRuntimeState`

### 目标
- 进一步减少 `CompPawnSkin` 顶层状态噪音
- 将技能热键相关运行时状态从主类字段堆叠中抽离
- 在不破坏外部调用的前提下，为后续继续拆分运行时逻辑建立更稳定模式

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有类：`AbilityHotkeyRuntimeState`
- 将以下状态收敛到内部对象：
  - `qHotkeyModeIndex`
  - `qComboWindowEndTick`
  - `qCooldownUntilTick`
  - `wCooldownUntilTick`
  - `eCooldownUntilTick`
  - `rCooldownUntilTick`
  - `rStackingEnabled`
  - `rStackCount`
  - `rSecondStageReady`
  - `rSecondStageExecuteTick`
  - `rSecondStageHasTarget`
  - `rSecondStageTargetCell`
  - `weaponCarryCastingUntilTick`
- 保留原有对外成员名，但改为代理属性，确保以下外部调用点不需要同步改动：
  - `AbilityHotkeyRuntimeComponent.cs`
  - `CompAbilityEffect_Modular.cs`
  - 其他直接访问 `CompPawnSkin` 热键状态的代码
- 将存档逻辑收敛到：
  - `AbilityHotkeyRuntimeState.ExposeData()`
- 将运行时归一化逻辑收敛到：
  - `AbilityHotkeyRuntimeState.Normalize()`

### 预期不变行为
- 外部对 `CompPawnSkin.qHotkeyModeIndex` 等成员的读写方式不变
- Q/W/E/R 技能 CD 行为不变
- R 堆叠与二段机制行为不变
- 武器施法窗口行为不变
- 读档/存档字段名保持不变

### 允许变化行为
- `CompPawnSkin` 内部实现从“公开字段堆叠”变为“状态对象 + 代理属性”
- 归一化与序列化逻辑位置发生内聚化调整
- 内部实现更适合后续继续抽离真正的运行时能力模块

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 2 warning**
- 当前警告仍位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告与本次 `CompPawnSkin` 技能热键运行时状态抽离无直接关系
- 说明本轮改动未破坏编译链路，且通过代理属性保住了对外兼容性

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的技能热键/CD/R 二段机制内部状态组织
- 对外 API 形式保持不变，因此对 `AbilityHotkeyRuntimeComponent` 和 `CompAbilityEffect_Modular` 的影响被压缩到最低
- 属于低到中低风险的“内部状态聚合 + 向后兼容”重构

### 当前风险观察
- 当前仍然只是类内聚合，`AbilityHotkeyRuntimeState` 仍是嵌套类，不是独立服务
- 虽然代理属性保住了兼容性，但 `CompPawnSkin` 仍然承担大量行为逻辑，尚未真正降为协调器
- 尚未进行游戏内手工验证，因此不能完全确认读档恢复、R 二段目标缓存、施法窗口视觉在真实运行时 100% 与之前一致
- 但从编译结果与改动方式判断，这一步可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 下一轮继续拆 `CompPawnSkin`，优先考虑：
  1. 表情/眨眼状态控制逻辑
  2. 眼球方向状态控制逻辑
- 若保持低风险节奏，建议优先处理 **表情/眨眼状态控制逻辑**，因为它与当前已抽离的预览覆盖状态天然相关
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 04：CompPawnSkin 表情/眨眼状态控制抽离（第三轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中当前表情、眨眼计时、表情动画 Tick 收敛为内部状态对象 `FaceExpressionRuntimeState`

### 目标
- 进一步减少 `CompPawnSkin` 顶层运行时状态字段
- 将表情/眨眼这一组天然内聚的状态统一封装
- 为后续继续拆分眼球方向和更多面部运行时逻辑建立模式

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有类：`FaceExpressionRuntimeState`
- 将以下状态收敛到内部对象：
  - `curExpression`（通过代理属性保留原成员名）
  - `blinkTimer`
  - `expressionAnimTick`
- 新增内部行为封装：
  - `AdvanceAnimTick()`
  - `ConsumeBlinkTick()`
  - `StartBlink(int durationTicks)`
  - `ClearBlink()`
  - `IsBlinkActive`
- 将以下逻辑改为通过状态对象访问：
  - `CompTick()`
  - `UpdateAnimatedExpressionFrame()`
  - `UpdateBlinkLogic()`
  - `SetPreviewExpressionOverride()`
  - `GetEffectiveExpression()`
  - `GetExpressionAnimTick()`

### 预期不变行为
- 运行时表情推断逻辑不变
- Blink 触发概率与持续时长不变
- 动画表情帧推进逻辑不变
- 预览表达式覆盖时仍会压制 Blink
- 对外 `curExpression` 访问方式保持可用

### 允许变化行为
- `CompPawnSkin` 内部实现从“零散字段”变为“状态对象 + 局部行为封装”
- Blink 清理与动画 Tick 推进从主类细节转为状态对象方法
- 后续继续抽离面部运行时控制逻辑的可操作性更强

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 2 warning**
- 当前警告仍位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告与本次 `CompPawnSkin` 表情/眨眼状态控制抽离无直接关系
- 说明本轮改动未破坏编译链路，且表情运行时状态已开始从主类行为中内聚

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的表情运行时状态与 Blink/动画 Tick 控制逻辑
- 对外只有 `curExpression` 仍作为兼容入口暴露，其他状态已收敛为内部实现
- 属于低到中低风险的“面部运行时状态内聚”重构

### 当前风险观察
- 当前仍为类内聚合，`FaceExpressionRuntimeState` 还是嵌套类，未形成独立运行时服务
- `curExpression` 仍作为公开成员存在，说明主类尚未完全摆脱状态承载角色
- 尚未做游戏内手工验证，因此不能完全确认 Blink 时序、动画边界刷新、预览覆盖解除后的恢复时机在真实运行时 100% 与之前一致
- 但从编译结果与改动方式判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 眼球方向状态控制逻辑
  2. 进一步抽离面部运行时更新流程（expression / eye / channel 的协调层）
- 若继续保持低风险节奏，建议优先处理 **眼球方向状态控制逻辑**，因为它同样主要内聚在 `CompPawnSkin` 内部，且与本轮面部状态拆分边界清晰
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 05：CompPawnSkin 眼球方向状态控制抽离（第四轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中运行时眼球方向状态收敛为内部状态对象 `EyeDirectionRuntimeState`

### 目标
- 继续减少 `CompPawnSkin` 顶层状态字段
- 将眼球方向这一独立运行时状态从主类中收束出来
- 保持 `CurEyeDirection` 预览覆盖与渲染消费链路不变，为后续抽离面部运行时协调层铺路

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有类：`EyeDirectionRuntimeState`
- 将运行时方向状态 `curEyeDirection` 收敛到内部对象：
  - `currentEyeDirection`
- 保留原有 `curEyeDirection` 成员名，但改为代理属性：
  - `get => eyeDirectionState.currentEyeDirection`
  - `set => eyeDirectionState.currentEyeDirection = value`
- 将 `UpdateEyeDirectionState()` 改为先计算 `resolvedDirection`，再通过：
  - `eyeDirectionState.SetDirection(resolvedDirection)`
  控制状态写入与冗余刷新
- 保持以下消费链路不变：
  - `CurEyeDirection`
  - `GetChannelStateSuffix(LayerRole.Eye)`
  - `EnsureFaceRuntimeStateUpdated()`
  - `EnsureFaceRuntimeStateReadyForPreview()`

### 预期不变行为
- 编辑器和渲染侧通过 `CurEyeDirection` 读取当前眼球方向的方式不变
- 预览覆盖仍优先于自动推断
- 死亡 / 倒地 / 睡眠 / 有目标 / 无目标时的方向推断规则不变
- 方向未变化时不应产生额外渲染刷新

### 允许变化行为
- `CompPawnSkin` 内部实现从“直接字段”变为“状态对象 + 代理属性”
- `UpdateEyeDirectionState()` 从“边算边写”变为“先求值后统一提交”
- 未来更容易继续抽离面部运行时状态协调逻辑

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 2 warning**
- 当前警告仍位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告与本次 `CompPawnSkin` 眼球方向状态控制抽离无直接关系
- 说明本轮改动未破坏编译链路，且眼球方向运行时状态已开始从主类字段中内聚

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的眼球方向运行时状态与方向更新逻辑
- 外部 UI / 渲染代码继续通过 `CurEyeDirection` 消费，不需要同步修改
- 属于低风险的“运行时状态内聚 + 刷新时机收束”重构

### 当前风险观察
- 当前仍为类内聚合，`EyeDirectionRuntimeState` 还是嵌套类，未形成独立运行时服务
- `curEyeDirection` 仍保留为公开兼容成员，说明主类尚未完全摆脱状态承载角色
- 尚未做游戏内手工验证，因此不能完全确认目标追踪、预览覆盖切换、方向变化刷新时机在真实运行时 100% 与之前一致
- 但从编译结果与改动方式判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 面部运行时更新流程协调层（expression / eye / channel 汇总）
  2. 评估 `curExpression` / `curEyeDirection` 等兼容入口的进一步收口方式
- 若继续保持低风险节奏，建议优先处理 **面部运行时更新流程协调层**，因为表达式、眼球方向、通道状态已经分别开始内聚，当前最自然的下一步是收束它们的协同更新边界
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 06：CompPawnSkin 面部运行时更新流程协调层抽离（第五轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中“有效面部状态汇总 + 变更比对 + 运行时写回”的协调流程收敛为独立的快照与辅助方法

### 目标
- 缩减 `EnsureFaceRuntimeStateUpdated()` 与 `EnsureFaceRuntimeStateReadyForPreview()` 中重复的状态拼装逻辑
- 将 expression / eye / mouth / lid / brow / emotion 的汇总边界显式化
- 为后续继续抽离面部运行时协调层或独立服务提供更稳定的过渡结构

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有类型：`EffectiveFaceStateSnapshot`
- 新增内部辅助方法：
  - `BuildEffectiveFaceStateSnapshot()`
  - `HasEffectiveFaceStateChanged(FaceRuntimeState runtimeState, EffectiveFaceStateSnapshot snapshot)`
  - `ApplyEffectiveFaceState(FaceRuntimeState runtimeState, EffectiveFaceStateSnapshot snapshot)`
- 将 `EnsureFaceRuntimeStateUpdated()` 中原本分散的：
  - 有效状态计算
  - 与 `FaceRuntimeState` 的逐项比对
  - 当前有效状态写回
  收敛为“构建快照 -> 判断差异 -> 统一写回”
- 将 `EnsureFaceRuntimeStateReadyForPreview()` 中原本重复的多字段写回逻辑改为复用同一套快照写回流程

### 预期不变行为
- `FaceRuntimeState` 中 expression / eye / mouth / lid / brow / emotion 的最终值不变
- `expressionDirty` 的判定语义不变
- 预览准备流程仍会把当前有效面部状态同步到 `FaceRuntimeState`
- 运行时渲染消费链路不变

### 允许变化行为
- `CompPawnSkin` 内部实现从“多个方法内部分别拼装状态”变为“统一快照 + 辅助方法”
- 面部运行时状态更新流程的边界更明确，可读性更高
- 后续可以更自然地继续收口为独立协调对象

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 2 warning**
- 当前警告仍位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告与本次 `CompPawnSkin` 面部运行时更新流程协调层抽离无直接关系
- 说明本轮改动未破坏编译链路，且面部运行时状态汇总边界已开始从具体更新流程中显式化

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的面部运行时状态同步流程
- 未改动对外消费接口，主要影响 `FaceRuntimeState` 的内部更新组织方式
- 属于低风险的“流程收束 / 重复逻辑归并”重构

### 当前风险观察
- 当前仍然只是类内重组，`EffectiveFaceStateSnapshot` 仍是 `CompPawnSkin` 内部辅助类型，尚未形成独立协调服务
- `BuildEffectiveFaceStateSnapshot()` 仍依赖主类上的多个 `GetEffective*` 方法，说明职责虽然更清晰，但边界尚未完全脱离主类
- 尚未做游戏内手工验证，因此不能完全确认 portrait/world 切换、预览同步、状态脏标记时机在真实运行时 100% 与之前一致
- 但从编译结果与改动方式判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 评估 `curExpression` / `curEyeDirection` 等兼容入口的进一步收口方式
  2. 继续抽离面部运行时协调层为独立私有对象或服务
- 若继续保持低风险节奏，建议优先处理 **兼容入口收口评估**，先确认外部直接访问点，再决定是否能把公开状态进一步压缩到只保留必要 API
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 07：CompPawnSkin 兼容入口收口（第六轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中仅供内部使用的运行时状态入口 `curExpression` / `curEyeDirection` 从公开兼容成员收口为私有实现细节

### 目标
- 基于外部引用搜索结果，继续压缩 `CompPawnSkin` 的原始状态暴露面
- 明确外部消费应通过 `GetEffectiveExpression()`、`CurEyeDirection` 等稳定 API，而不是直接访问底层运行时状态
- 进一步推动 `CompPawnSkin` 从“状态堆叠容器”向“受控运行时协调器”收束

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`
- `Source/CharacterStudio/UI/Dialog_AbilityEditor.Preview.cs`

### 本轮改动点
- 先执行外部引用搜索，确认：
  - `curExpression`
  - `curEyeDirection`
  没有真实的外部代码级成员访问，只有类内使用与注释命中
- 将以下成员从 `public` 改为 `private`：
  - `curExpression`
  - `curEyeDirection`
- 保持以下外部消费入口不变：
  - `GetEffectiveExpression()`
  - `CurEyeDirection`
  - 其他基于 `GetEffective*` 的状态读取 API
- 在本轮 build review 过程中，发现并修复一个阻塞编译的问题：
  - `Dialog_AbilityEditor.Preview.cs(118,31)`
  - `ability.warmupTicks` 为 `float`，预览时间轴事件 `tick` 需要 `int`
  - 通过显式转换修复：
    - `int warmupTicks = Math.Max(0, (int)ability.warmupTicks);`

### 预期不变行为
- 外部模块继续通过 `CurEyeDirection`、`GetEffectiveExpression()` 等 API 获取面部运行时状态
- `CompPawnSkin` 的表情、眨眼、眼球方向推断与预览覆盖行为不变
- Ability 预览面板的 warmup 事件仍按 warmup 时长落点到时间轴
- 编译链路恢复为可通过状态

### 允许变化行为
- `CompPawnSkin` 原始运行时状态入口不再允许外部直接访问
- Ability 预览中 `warmupTicks` 会显式截断为整数 Tick，而不是依赖隐式转换
- 代码对“稳定 API”与“内部状态入口”的边界更清晰

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 初次 build 时暴露出一个编译阻塞：
  - `Source/CharacterStudio/UI/Dialog_AbilityEditor.Preview.cs(118,31)`
  - 错误：`CS0266`，`float` 不能隐式转换为 `int`
- 在对预览时间轴 warmup tick 做显式转换修复后，重新构建通过，结果为 **0 error / 2 warning**
- 当前警告仍位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告与本次 `CompPawnSkin` 兼容入口收口无直接关系
- 说明本轮改动未破坏编译链路，且 `CompPawnSkin` 的原始状态暴露面已进一步收窄

### 影响范围评估
- 影响范围主要集中在 `CompPawnSkin` 的内部状态可见性边界
- 对外实际消费路径未改动，因此渲染/UI 侧的读取方式基本不受影响
- 额外影响到 `Dialog_AbilityEditor` 预览时间轴的 warmup tick 类型处理
- 属于低风险的“可见性收口 + 编译阻塞修复”重构

### 当前风险观察
- 虽然 `curExpression` / `curEyeDirection` 已收口，但 `CompPawnSkin` 仍然掌握大量状态计算与同步逻辑，尚未完全降为窄接口协调器
- `Dialog_AbilityEditor.Preview` 当前对 `float warmupTicks` 采用截断语义；若后续希望时间轴更贴近真实表现，可能要评估是否改为四舍五入
- 尚未做游戏内手工验证，因此不能完全确认 Ability 预览 warmup 显示与面部状态切换时机在真实运行时 100% 与预期一致
- 但从引用搜索、构建结果与修改规模判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将面部运行时协调辅助方法进一步收敛为独立私有对象或静态协同器
  2. 清理剩余依赖“原始状态”命名的注释与文档描述，统一改为稳定 API 语义
- 若继续保持低风险节奏，建议优先处理 **面部运行时协调辅助方法的继续收束**，因为当前 `EffectiveFaceStateSnapshot` 与相关 helper 已经形成明显边界，适合进一步内聚
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 08：CompPawnSkin 面部运行时同步协同器抽离（第七轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中面部运行时同步辅助逻辑收敛为内部静态协同器 `FaceRuntimeSyncCoordinator`

### 目标
- 进一步压缩 `EnsureFaceRuntimeStateUpdated()` 与 `EnsureFaceRuntimeStateReadyForPreview()` 的流程噪音
- 把 track/lod 更新判定、脏标记清理、有效状态写回等协同逻辑从主类中抽出
- 推进 `CompPawnSkin` 向“状态拥有者 + 高层流程编排器”继续收束

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有静态类：`FaceRuntimeSyncCoordinator`
- 将以下辅助逻辑收敛到协同器内部：
  - `UpdateTrackAndLodIfNeeded(...)`
  - `SyncEffectiveState(...)`
  - `PreparePreviewState(...)`
  - `ResetDirtyFlags(...)`
  - `ShouldUpdateTrackAndLod(...)`
  - `HasEffectiveFaceStateChanged(...)`
  - `ApplyEffectiveFaceState(...)`
- 删除 `CompPawnSkin` 顶层散落的同类静态 helper
- 将 `EnsureFaceRuntimeStateUpdated()` 改为：
  - 通过 `FaceRuntimeSyncCoordinator.UpdateTrackAndLodIfNeeded(...)` 处理 track/lod 更新与 dirty flag 清理
  - 通过 `FaceRuntimeSyncCoordinator.SyncEffectiveState(...)` 处理有效状态同步
- 将 `EnsureFaceRuntimeStateReadyForPreview()` 改为复用：
  - `FaceRuntimeSyncCoordinator.ResetDirtyFlags(...)`
  - `FaceRuntimeSyncCoordinator.PreparePreviewState(...)`
- 保留 `BuildEffectiveFaceStateSnapshot()` 作为主类内部的有效状态组装入口

### 预期不变行为
- track/lod 更新时间判定逻辑不变
- `trackDirty / lodDirty / compiledDataDirty` 的清理时机不变
- `expressionDirty` 的判定与有效面部状态写回语义不变
- 预览准备流程仍先更新 `FaceRuntimeState`，再同步当前有效状态
- 渲染刷新触发条件不变

### 允许变化行为
- 面部运行时同步逻辑从“主类内散落 helper + 局部内联流程”变为“内部协同器统一处理”
- `CompPawnSkin` 的两个核心同步方法更偏向高层编排，而不再承担全部细节
- 后续若继续拆分为独立文件或服务，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 2 warning**
- 当前警告仍位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告与本次 `CompPawnSkin` 面部运行时同步协同器抽离无直接关系
- 说明本轮改动未破坏编译链路，且面部运行时同步逻辑的组织边界已进一步清晰化

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的面部运行时同步流程
- 对外消费 API 未变化，主要影响内部同步与脏标记管理的组织方式
- 属于低风险的“内部协同逻辑收束”重构

### 当前风险观察
- `FaceRuntimeSyncCoordinator` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立的运行时服务
- `BuildEffectiveFaceStateSnapshot()` 仍位于主类中，并依赖多个 `GetEffective*` 方法，说明“状态求值”与“状态同步”尚未完全分层
- 尚未做游戏内手工验证，因此不能完全确认 portrait/world 切换、预览进入/退出、脏标记驱动刷新时机在真实运行时 100% 与之前一致
- 但从编译结果与改动方式判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `ResolveMouthState / ResolveLidState / ResolveBrowState / ResolveEmotionOverlayState` 收敛为独立的通道状态解析器
  2. 清理剩余依赖旧字段语义的注释与文档描述，统一改为稳定 API 语义
- 若继续保持低风险节奏，建议优先处理 **通道状态解析器收束**，因为这几组方法天然是纯函数边界，改动局部、风险较低，且能继续降低 `CompPawnSkin` 的职责密度
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 09：CompPawnSkin 通道状态解析器抽离（第八轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中 `ResolveMouthState / ResolveLidState / ResolveBrowState / ResolveEmotionOverlayState` 四组纯函数解析逻辑收敛为内部静态解析器 `FaceChannelStateResolver`

### 目标
- 继续降低 `CompPawnSkin` 主类的职责密度
- 将 mouth / lid / brow / emotion 的表达式到通道状态映射边界显式化
- 为后续继续收束状态求值逻辑或清理旧语义注释提供更稳定的纯函数边界

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有静态类：`FaceChannelStateResolver`
- 将以下纯函数解析逻辑迁移到解析器内部：
  - `ResolveMouthState(ExpressionType expression)`
  - `ResolveLidState(ExpressionType expression)`
  - `ResolveBrowState(ExpressionType expression)`
  - `ResolveEmotionOverlayState(ExpressionType expression)`
- 保留 `CompPawnSkin` 顶层同名方法，但改为薄代理：
  - `=> FaceChannelStateResolver.ResolveMouthState(expression)`
  - `=> FaceChannelStateResolver.ResolveLidState(expression)`
  - `=> FaceChannelStateResolver.ResolveBrowState(expression)`
  - `=> FaceChannelStateResolver.ResolveEmotionOverlayState(expression)`
- 保持 `GetEffectiveMouthState / GetEffectiveLidState / GetEffectiveBrowState / GetEffectiveEmotionOverlayState` 的外部消费路径不变

### 预期不变行为
- 不同 `ExpressionType` 对应的 mouth / lid / brow / emotion 映射结果不变
- 预览覆盖优先级不变
- `GetChannelStateSuffix(...)` 的输出语义不变
- 渲染树中的 channel variant 路径命中逻辑不变

### 允许变化行为
- `CompPawnSkin` 内部从“主类直接承载多组纯函数”变为“解析器统一承载 + 主类薄代理”
- 通道状态映射的维护位置更集中
- 后续若继续拆分状态求值层，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 2 warning**
- 当前警告仍位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告与本次 `CompPawnSkin` 通道状态解析器抽离无直接关系
- 说明本轮改动未破坏编译链路，且通道状态映射逻辑已从主类流程中进一步收束为纯函数边界

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的通道状态求值组织方式
- 对外消费 API 未变化，主要影响 expression 到 channel state 的内部映射承载位置
- 属于低风险的“纯函数解析边界收束”重构

### 当前风险观察
- `FaceChannelStateResolver` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- `BuildEffectiveFaceStateSnapshot()` 与多个 `GetEffective*` 方法仍位于主类中，说明“状态求值入口”与“状态解析实现”虽然更清晰，但尚未完全分层
- 尚未做游戏内手工验证，因此不能完全确认面部通道切换、预览覆盖解除、channel variant 路径选择在真实运行时 100% 与之前一致
- 但从编译结果与改动方式判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 在完成本轮文档落档后，再规划下一轮小步重构，优先考虑：
  1. 继续收束 `CompPawnSkin` 中剩余的状态求值 helper / 组装入口
  2. 清理仍依赖旧字段语义的注释与文档描述，统一改为稳定 API 语义
- 若继续保持低风险节奏，建议优先处理 **状态求值入口的继续收束或注释语义统一**，避免在未完成 review 记录前直接进入更大范围代码改动
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 10：CompPawnSkin 有效面部状态求值器收束（第九轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中有效面部状态求值入口收敛为内部静态求值器 `EffectiveFaceStateEvaluator`

### 目标
- 继续降低 `CompPawnSkin` 主类中“有效状态求值”相关逻辑的分散度
- 将 expression / eye / mouth / lid / brow / emotion 的最终求值入口统一到单一边界
- 为后续继续拆分表情推断逻辑或眼球方向推断逻辑建立更清晰的上层求值结构

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`
- `Source/CharacterStudio/UI/Dialog_AbilityEditor.cs`

### 本轮改动点
- 新增内部私有静态类：`EffectiveFaceStateEvaluator`
- 将以下有效状态求值入口统一收敛到求值器内部：
  - `BuildEffectiveFaceStateSnapshot()`
  - `GetEffectiveExpression()`
  - `CurEyeDirection`
  - `GetEffectiveMouthState()`
  - `GetEffectiveLidState()`
  - `GetEffectiveBrowState()`
  - `GetEffectiveEmotionOverlayState()`
- 在求值器内部统一处理：
  - 预览覆盖优先级
  - Blink 对有效表情的覆盖
  - 表情到 mouth / lid / brow / emotion 通道状态的映射调用
- 保持 `CompPawnSkin` 顶层对外 API 不变，但改为薄代理或统一经由求值器取值
- 在本轮 build review 过程中，发现并修复一个阻塞编译的问题：
  - `Dialog_AbilityEditor.cs(468,34)`
  - `Dialog_AbilityEditor.cs(475,32)`
  - `selectedAbility.cooldownTicks / warmupTicks` 为 `float`，原代码使用了 `int` 本地变量与整型边界参数
  - 通过改为 `float` 基线变量、`Math.Abs(... ) > 0.001f` 比较、以及 `0f / 100000f` 浮点边界修复

### 预期不变行为
- `GetEffectiveExpression()` 的预览覆盖与 Blink 优先级不变
- `CurEyeDirection` 对外读取语义不变
- mouth / lid / brow / emotion 的最终求值结果不变
- `BuildEffectiveFaceStateSnapshot()` 输出的有效状态语义不变
- 技能编辑器中 cooldown / warmup 的编辑行为保持可用

### 允许变化行为
- `CompPawnSkin` 内部从“多个方法各自求值”变为“统一求值器汇总”
- 有效状态求值路径更集中，主类更偏向高层编排
- `Dialog_AbilityEditor` 中 cooldown / warmup 数值比较与边界参数显式采用浮点语义

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 初次 build 时暴露出两个编译阻塞：
  - `Source/CharacterStudio/UI/Dialog_AbilityEditor.cs(468,34)`
  - `Source/CharacterStudio/UI/Dialog_AbilityEditor.cs(475,32)`
  - 错误：`CS0266`，`float` 不能隐式转换为 `int`
- 对 `Dialog_AbilityEditor` 做最小类型修复后，重新构建通过，结果为 **0 error / 2 warning**
- 当前警告仍位于：
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(259,33)`
  - `Source/CharacterStudio/Abilities/EffectWorkers.cs(280,37)`
- 这两个警告与本次 `CompPawnSkin` 有效面部状态求值器收束无直接关系
- 说明本轮改动未破坏编译链路，且有效面部状态的上层求值边界已进一步集中

### 影响范围评估
- 影响范围主要集中在 `CompPawnSkin` 的有效状态求值组织方式
- 对外消费 API 未变化，主要影响 expression / eye / channel state 的内部求值入口
- 附带影响到 `Dialog_AbilityEditor` 中 cooldown / warmup 字段的编辑类型一致性
- 属于低风险的“求值入口收束 + 编译阻塞修复”重构

### 当前风险观察
- `EffectiveFaceStateEvaluator` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- 求值器仍直接依赖 `previewOverrides`、`faceExpressionState`、`curExpression`、`curEyeDirection` 等主类内部状态，说明“求值入口”虽然统一，但尚未完全脱离宿主对象
- `UpdateExpressionState()` 与 `UpdateEyeDirectionState()` 的具体推断逻辑仍留在主类中，后续仍有继续抽离空间
- 尚未做游戏内手工验证，因此不能完全确认表情覆盖切换、Blink 过渡、眼球方向预览恢复、技能编辑器 cooldown/warmup 编辑体验在真实运行时 100% 与之前一致
- 但从构建结果与修改规模判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `UpdateExpressionState()` 中的大块 Job / MentalState / Need 推断逻辑收敛为独立的表情状态解析器
  2. 评估 `UpdateEyeDirectionState()` 中目标方向推断与映射逻辑的进一步收束边界
- 若继续保持低风险节奏，建议优先处理 **表情状态推断逻辑抽离**，因为它目前仍是 `CompPawnSkin` 中体量较大、规则密集且边界相对清晰的一段逻辑
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 11：CompPawnSkin 表情状态推断解析器抽离（第十轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中 `UpdateExpressionState()` 内的大块表情推断规则收敛为内部静态解析器 `FaceExpressionStateResolver`

### 目标
- 继续降低 `CompPawnSkin` 主类中表情推断逻辑的体积与分支噪音
- 将死亡 / 倒地 / 睡眠 / Job / MentalState / Needs 的表情判定边界显式化
- 为后续继续拆分眼球方向推断逻辑或将面部运行时求值进一步模块化提供更清晰的规则层

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有静态类：`FaceExpressionStateResolver`
- 将以下表情推断规则从 `UpdateExpressionState()` 主流程中迁移到解析器内部：
  - 死亡 / 倒地 / 睡眠 判定
  - Job 驱动表情判定
  - MentalState 驱动表情判定
  - rest / mood 驱动的 needs fallback 判定
- 在解析器内部进一步拆分为：
  - `ResolveExpression(Pawn pawn)`
  - `ResolveJobExpression(Pawn pawn)`
  - `ResolveMentalStateExpression(Pawn pawn)`
  - `ResolveNeedsExpression(Pawn pawn)`
- 将 `UpdateExpressionState()` 收束为：
  - 读取旧值
  - 调用 `FaceExpressionStateResolver.ResolveExpression(pawn)`
  - 在表情发生变化时请求渲染刷新

### 预期不变行为
- 不同 Pawn 状态下的表情推断结果不变
- Job / MentalState / Needs 的优先级顺序不变
- 表情变化时的渲染刷新时机不变
- 外部仍通过既有 `GetEffectiveExpression()` 与相关面部状态 API 获取结果

### 允许变化行为
- `CompPawnSkin` 内部从“主类直接承载大量 if/else 推断”变为“主类高层编排 + 独立规则解析器”
- 表情判定规则的维护位置更集中
- 后续继续拆出独立文件级解析器的迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 warning / 0 error**
- 本次 build 输出中未再出现此前持续存在的 `EffectWorkers.cs` 两个 `CS8600` 警告
- 说明本轮改动未破坏编译链路，且 `CompPawnSkin` 中最密集的一段表情推断规则已进一步从主类主流程中收束出来
- 从编译结果看，当前工程处于比前几轮更干净的状态

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的运行时表情推断组织方式
- 对外消费 API 未变化，主要影响内部规则判定的承载位置
- 属于低风险的“规则解析层抽离”重构

### 当前风险观察
- `FaceExpressionStateResolver` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- 解析器虽然已经承载规则，但仍依赖 RimWorld 运行时对象 `Pawn`、`JobDefOf`、`RestUtility` 等环境上下文，说明它是“规则收束”而非“纯函数无依赖模块”
- `UpdateEyeDirectionState()` 仍保留在主类中，面部运行时推断逻辑尚未完全对称拆分
- 尚未做游戏内手工验证，因此不能完全确认 Job 切换、精神状态切换、rest/mood fallback 触发表情的时机在真实运行时 100% 与之前一致
- 但从构建结果与修改规模判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `UpdateEyeDirectionState()` 中目标方向推断与映射逻辑收敛为独立解析器
  2. 评估 `GetJobTargetCell()` / `MapDeltaToEyeDirection()` / `MapRotationToEyeDirection()` 的协作边界，形成完整的眼球方向规则层
- 若继续保持低风险节奏，建议优先处理 **眼球方向推断解析器抽离**，这样可以与本轮表情状态解析器形成对称结构
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 12：CompPawnSkin 眼球方向推断解析器抽离（第十一轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中 `UpdateEyeDirectionState()` 的目标方向推断与映射逻辑收敛为内部静态解析器 `EyeDirectionStateResolver`

### 目标
- 继续降低 `CompPawnSkin` 主类中眼球方向推断逻辑的分支噪音
- 将目标单元获取、相对方向映射、默认朝向回退等规则边界显式化
- 与上一轮 `FaceExpressionStateResolver` 形成对称的“面部规则解析层”结构

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有静态类：`EyeDirectionStateResolver`
- 将以下逻辑从 `UpdateEyeDirectionState()` 主流程中迁移到解析器内部：
  - 死亡 / 倒地 / 睡眠时返回 `EyeDirection.Center`
  - 当前 Job 目标单元获取
  - 目标相对方向到 `EyeDirection` 的映射
  - 无目标时按朝向回退到默认方向
- 在解析器内部进一步拆分为：
  - `ResolveDirection(Pawn pawn)`
  - `GetJobTargetCell(Pawn pawn)`
  - `MapDeltaToEyeDirection(IntVec3 delta, Rot4 rot)`
  - `MapRotationToEyeDirection(Rot4 rot)`
- 将 `UpdateEyeDirectionState()` 收束为：
  - 校验前置条件
  - 调用 `EyeDirectionStateResolver.ResolveDirection(pawn)`
  - 在方向发生变化时请求渲染刷新

### 预期不变行为
- 死亡 / 倒地 / 睡眠时眼球方向仍回到 `Center`
- 有 Job 目标时仍按相对方向推断 Left / Right / Up / Down / Center
- 无目标时仍使用既有默认回退逻辑
- 方向变化时的渲染刷新时机不变
- 外部仍通过 `CurEyeDirection` 与相关面部状态 API 获取结果

### 允许变化行为
- `CompPawnSkin` 内部从“主类直接承载方向推断细节”变为“主类高层编排 + 独立规则解析器”
- 眼球方向规则维护位置更集中
- 后续继续拆分为更独立的文件级规则模块时迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 warning / 0 error**
- 本轮改动未引入新的编译问题，且工程继续保持零警告状态
- 说明眼球方向推断规则已从 `CompPawnSkin` 主流程中进一步收束出来，结构与上一轮表情状态解析器已形成对称模式

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的运行时眼球方向推断组织方式
- 对外消费 API 未变化，主要影响内部规则判定与映射逻辑的承载位置
- 属于低风险的“规则解析层抽离”重构

### 当前风险观察
- `EyeDirectionStateResolver` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- 解析器仍依赖 RimWorld 运行时对象 `Pawn`、`Rot4`、Job target 等环境上下文，说明它是“规则收束”而非完全独立的纯函数模块
- `UpdateExpressionState()` 与 `UpdateEyeDirectionState()` 虽然都已简化，但 `CompTick()` 仍承担较多面部运行时调度责任
- 尚未做游戏内手工验证，因此不能完全确认目标切换、朝向变化、预览覆盖解除后的眼神恢复时机在真实运行时 100% 与之前一致
- 但从构建结果与修改规模判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `CompTick()` 中面部运行时轮询与调度逻辑收敛为独立的 tick 协调器
  2. 评估 `EnsureFaceRuntimeStateUpdated()`、`UpdateAnimatedExpressionFrame()`、`UpdateBlinkLogic()`、`UpdateExpressionState()`、`UpdateEyeDirectionState()` 的调度边界，形成统一的面部运行时调度层
- 若继续保持低风险节奏，建议优先处理 **面部运行时 tick 协调器抽离**，因为表达式规则层与眼球方向规则层已经具备，当前最自然的下一步是收束它们的调度主流程
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 13：CompPawnSkin 面部运行时 Tick 协调器抽离（第十二轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中 `CompTick()` 的面部运行时轮询与调度流程收敛为内部静态协调器 `FaceRuntimeTickCoordinator`

### 目标
- 继续降低 `CompPawnSkin` 主类中逐 Tick 运行时调度代码的体积与流程噪音
- 将表情更新、动画帧推进、Blink 逻辑、眼球方向更新的调度顺序显式化
- 推进 `CompPawnSkin` 从“直接承载调度细节”向“高层生命周期入口 + 协调器编排”继续收束

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有静态类：`FaceRuntimeTickCoordinator`
- 在协调器内部统一收敛以下逐 Tick 调度顺序：
  - `EnsureFaceRuntimeStateUpdated()`
  - `faceExpressionState.AdvanceAnimTick()`
  - 按 `pawn.IsHashIntervalTick(30)` 判定是否执行 `UpdateExpressionState()`
  - `UpdateAnimatedExpressionFrame()`
  - `UpdateBlinkLogic()`
  - 按眼球方向配置启用状态与 `pawn.IsHashIntervalTick(15)` 判定是否执行 `UpdateEyeDirectionState()`
- 将 `CompTick()` 收束为：
  - 基础前置校验
  - `needsRefresh` 刷新处理
  - 在 face runtime 启用时调用 `FaceRuntimeTickCoordinator.Tick(this, Pawn!)`

### 预期不变行为
- `CompTick()` 中面部运行时更新顺序不变
- 表情状态仍按 30 Tick 节奏更新
- 眼球方向仍按配置启用状态与 15 Tick 节奏更新
- 动画帧推进、Blink 逻辑、渲染刷新时机保持不变
- 对外运行时表现与既有 Face API 保持兼容

### 允许变化行为
- `CompPawnSkin` 内部从“主类内联调度流程”变为“主类高层入口 + Tick 协调器”
- 逐 Tick 调度规则的维护位置更集中
- 后续继续拆分刷新策略、前置条件守卫或独立运行时服务时迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 warning / 0 error**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮改动未引入新的编译问题，工程继续保持零警告状态
- 说明面部运行时逐 Tick 调度主流程已经从 `CompPawnSkin` 主类中进一步收束出来

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的面部运行时调度组织方式
- 对外消费 API 未变化，主要影响逐 Tick 生命周期的内部编排位置
- 属于低风险的“运行时调度层抽离”重构

### 当前风险观察
- `FaceRuntimeTickCoordinator` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- 协调器仍直接调用宿主对象上的 `UpdateExpressionState()`、`UpdateBlinkLogic()`、`UpdateEyeDirectionState()` 等方法，说明当前仍是“调度边界收束”而非完全独立的运行时服务
- `CompTick()` 虽然已变薄，但 `needsRefresh` 处理和 face runtime 启用前置条件仍分散在主类多个方法中，后续仍有继续收束空间
- 尚未做游戏内手工验证，因此不能完全确认长时间运行下的动画刷新节奏、Blink 周期、眼球方向切换时机在真实运行时 100% 与之前一致
- 但从构建结果与修改规模判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 face runtime 启用判定与前置条件检查收敛为统一守卫/能力判定入口
  2. 评估 `needsRefresh`、`RequestRenderRefresh()` 与面部运行时状态脏标记之间的协作边界，形成更清晰的刷新协调层
- 若继续保持低风险节奏，建议优先处理 **face runtime 前置条件与启用守卫收束**，因为 `CompTick()` 调度层已经成型，当前最自然的下一步是进一步压缩主类中重复出现的启用判定与入口防卫代码
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 14：CompPawnSkin Face Runtime 启用守卫收束（第十三轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中重复出现的 face runtime 启用判定与 eye direction 前置条件检查收敛为内部静态守卫 `FaceRuntimeActivationGuard`

### 目标
- 继续降低 `CompPawnSkin` 主类中重复的启用判定与空值防卫噪音
- 将 face runtime 是否启用、是否可处理 eye direction 的前置条件边界显式化
- 为后续继续收束刷新协调层或生命周期守卫逻辑建立统一入口

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有静态类：`FaceRuntimeActivationGuard`
- 在守卫内部统一收敛以下判定入口：
  - `IsFaceRuntimeEnabled(CompPawnSkin owner)`
  - `CanProcessFaceRuntime(CompPawnSkin owner, Pawn? pawn)`
  - `IsEyeDirectionEnabled(CompPawnSkin owner)`
  - `CanProcessEyeDirection(CompPawnSkin owner, Pawn? pawn)`
- 将以下方法中的重复前置条件判断改为统一经由守卫执行：
  - `EnsureFaceRuntimeStateUpdated()`
  - `CompTick()`
  - `UpdateExpressionState()`
  - `UpdateEyeDirectionState()`
  - `EnsureFaceRuntimeStateReadyForPreview()`
- 将 `FaceRuntimeTickCoordinator.ShouldUpdateEyeDirection(...)` 改为复用：
  - `FaceRuntimeActivationGuard.IsEyeDirectionEnabled(owner)`
- 将 `CompTick()` 中的 `Pawn` 读取收束为局部变量，减少重复属性访问

### 预期不变行为
- face runtime 启用时机不变
- eye direction 启用判定与刷新节奏不变
- 表情状态更新、眼球方向更新、预览准备流程的对外行为不变
- `CompTick()` 的运行时调度顺序与刷新时机不变

### 允许变化行为
- `CompPawnSkin` 内部从“多处重复判定”变为“统一守卫入口 + 高层调用”
- face runtime 前置条件维护位置更集中
- 后续继续拆分刷新守卫、运行时生命周期边界时迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 warning / 0 error**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮改动未引入新的编译问题，工程继续保持零警告状态
- 说明 face runtime 启用守卫与前置条件检查已经从主类分散判断进一步收束为统一边界

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的 face runtime 启用判定与前置条件组织方式
- 对外消费 API 未变化，主要影响内部守卫与生命周期入口的组织位置
- 属于低风险的“前置条件守卫收束”重构

### 当前风险观察
- `FaceRuntimeActivationGuard` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- 守卫只统一了启用判定与空值检查，`needsRefresh`、`RequestRenderRefresh()`、运行时脏标记之间的刷新协作边界仍然分散
- 尚未做游戏内手工验证，因此不能完全确认预览态切换、运行态 tick 调度、眼球方向启停在真实运行时 100% 与之前一致
- 但从构建结果与修改规模判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `needsRefresh`、`RequestRenderRefresh()` 与 face runtime 脏标记之间的协作关系收敛为统一刷新协调层
  2. 评估 `ActiveSkin`、`SetActiveSkinSilent()`、`MarkFaceRuntimeDirty()`、`ClearSkin()` 等入口中的刷新职责边界
- 若继续保持低风险节奏，建议优先处理 **刷新协调层收束**，因为启用守卫已经统一，当前最自然的下一步是压缩主类中仍然分散的刷新触发与脏标记协作逻辑
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 15：CompPawnSkin 刷新协调层收束（第十四轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中与 face runtime 脏标记、即时刷新、延迟刷新相关的分散逻辑收敛为内部静态协调器 `FaceRuntimeRefreshCoordinator`

### 目标
- 继续降低 `CompPawnSkin` 主类中刷新触发与 `needsRefresh` 协作逻辑的重复度
- 将“标记 face runtime dirty”“是否立即刷新”“是否延迟到 Tick 刷新”的边界显式化
- 为后续继续梳理皮肤生命周期入口与渲染刷新职责建立统一刷新编排入口

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有静态类：`FaceRuntimeRefreshCoordinator`
- 在协调器内部统一收敛以下刷新入口：
  - `Apply(CompPawnSkin owner, bool immediateRefresh, bool deferredRefresh)`
  - `FlushDeferredRefresh(CompPawnSkin owner)`
- 将以下入口中的“MarkFaceRuntimeDirty + needsRefresh + RequestRenderRefresh”组合逻辑改为统一经由协调器执行：
  - `ActiveSkin` setter
  - `SetActiveSkinSilent()`
  - `ClearSkin()`
  - `TryApplyDefaultRaceSkinIfNeeded()`
- 将 `CompTick()` 中的延迟刷新执行改为复用：
  - `FaceRuntimeRefreshCoordinator.FlushDeferredRefresh(this)`
- 保持 `MarkFaceRuntimeDirty()` 与 `RequestRenderRefresh()` 作为底层能力，不改变对外 API

### 预期不变行为
- 应用皮肤、静默设置皮肤、清除皮肤、默认种族皮肤注入时的 face runtime dirty 语义不变
- 即时刷新与延迟刷新时机不变
- `CompTick()` 中的 deferred refresh 刷新行为不变
- face runtime 生命周期与对外渲染表现保持兼容

### 允许变化行为
- `CompPawnSkin` 内部从“多处散落的刷新组合逻辑”变为“统一刷新协调入口 + 高层调用”
- 刷新职责维护位置更集中
- 后续继续拆分皮肤生命周期刷新策略时迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 warning / 0 error**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮改动未引入新的编译问题，工程继续保持零警告状态
- 说明 face runtime 的脏标记与刷新触发协作已经从主类分散组合进一步收束为统一边界

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的刷新触发组织方式与皮肤生命周期入口中的刷新协作逻辑
- 对外消费 API 未变化，主要影响内部刷新编排位置
- 属于低风险的“刷新协调层收束”重构

### 当前风险观察
- `FaceRuntimeRefreshCoordinator` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- 当前只统一了部分皮肤生命周期入口，`PostSpawnSetup()`、预览覆盖相关 API、动画/Blink 驱动刷新等路径仍然直接调用 `RequestRenderRefresh()`
- 尚未做游戏内手工验证，因此不能完全确认皮肤切换、默认皮肤注入、清除皮肤与延迟刷新在真实运行时 100% 与之前一致
- 但从构建结果与修改规模判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `PostSpawnSetup()`、`TryApplyDefaultRaceSkinIfNeeded()`、`PostExposeData()` 等皮肤生命周期入口中的恢复/应用逻辑收敛为统一皮肤恢复协调层
  2. 评估预览覆盖 API 与运行时动画/Blink 刷新路径是否也应逐步接入统一刷新协调入口
- 若继续保持低风险节奏，建议优先处理 **皮肤生命周期恢复入口收束**，因为刷新协调层已经建立，当前最自然的下一步是进一步压缩皮肤恢复、默认皮肤注入与读档恢复这几条入口中的重复职责
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 16：CompPawnSkin 皮肤生命周期恢复入口收束（第十五轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中 `PostSpawnSetup()`、`TryApplyDefaultRaceSkinIfNeeded()`、`PostExposeData()` 的皮肤恢复/默认皮肤注入逻辑收敛为内部静态协调器 `SkinLifecycleRecoveryCoordinator`

### 目标
- 继续降低 `CompPawnSkin` 主类中皮肤生命周期恢复逻辑的重复度
- 将“出生后恢复皮肤”“读档后恢复皮肤”“默认种族皮肤注入”三条入口的协作边界显式化
- 为后续继续梳理皮肤绑定来源、默认皮肤策略与刷新职责建立统一生命周期恢复入口

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 新增内部私有静态类：`SkinLifecycleRecoveryCoordinator`
- 在协调器内部统一收敛以下生命周期恢复入口：
  - `RestoreAfterSpawn(CompPawnSkin owner)`
  - `RestoreAfterLoad(CompPawnSkin owner)`
  - `TryApplyDefaultRaceSkin(CompPawnSkin owner)`
- 将以下主类入口中的恢复逻辑改为统一经由协调器执行：
  - `PostSpawnSetup(bool respawningAfterLoad)`
  - `TryApplyDefaultRaceSkinIfNeeded()`
  - `PostExposeData()`
- 保持以下既有行为语义不变：
  - `activeSkinDefName` 恢复时先尝试 `DefDatabase<PawnSkinDef>.GetNamedSilentFail(...)`
  - 读档恢复失败时回退到 `PawnSkinDefRegistry.TryGet(...)`
  - 默认种族皮肤仍通过 `Clone()` 注入，并保留 `activeSkinFromDefaultRaceBinding = true`
  - 默认种族皮肤注入后仍通过 `FaceRuntimeRefreshCoordinator.Apply(...)` 触发脏标记与延迟刷新

### 预期不变行为
- 出生后如存在 `activeSkinDefName`，仍会优先恢复对应皮肤
- 无活动皮肤时，默认种族皮肤仍按既有条件自动注入
- `PostLoadInit` 阶段的皮肤恢复顺序与回退语义不变
- 皮肤恢复完成后的渲染刷新行为保持兼容
- `activeSkinFromDefaultRaceBinding` 的来源标记语义不变

### 允许变化行为
- `CompPawnSkin` 内部从“多处分散的生命周期恢复逻辑”变为“统一恢复协调入口 + 高层调用”
- 皮肤生命周期恢复职责的维护位置更集中
- 后续若继续拆分默认皮肤策略或恢复/应用规则，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 warning / 0 error**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮改动未引入新的编译问题，工程继续保持零警告状态
- 说明皮肤恢复、默认皮肤注入与读档恢复三条生命周期入口已经从主类分散逻辑进一步收束为统一边界

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的皮肤生命周期恢复组织方式
- 对外消费 API 未变化，主要影响内部恢复流程与默认皮肤注入逻辑的承载位置
- 属于低风险的“生命周期恢复协调层收束”重构

### 当前风险观察
- `SkinLifecycleRecoveryCoordinator` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- 当前只统一了出生、默认皮肤注入、读档恢复三条主路径，`ActiveSkin` setter、`ClearSkin()`、外部运行时应用入口之间的生命周期协作仍分散
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、默认种族皮肤自动绑定、存档恢复后的渲染刷新在真实运行时 100% 与之前一致
- 但从构建结果与修改规模判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `ActiveSkin` setter、`SetActiveSkinSilent()`、`SetActiveSkinWithSource()`、`ClearSkin()` 等入口中的皮肤应用来源与状态切换语义收敛为统一皮肤应用协调层
  2. 评估预览覆盖 API 与动画/Blink 刷新路径是否继续接入统一刷新与生命周期边界
- 若继续保持低风险节奏，建议优先处理 **皮肤应用入口与来源语义收束**，因为生命周期恢复路径已经统一，当前最自然的下一步是继续压缩皮肤设置/清除/来源标记这些仍分散的入口职责
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 17：CompPawnSkin 皮肤应用入口与来源语义收束（第十六轮）

### 重构步骤名称
- 将 `CompPawnSkin` 中 `ActiveSkin` setter、`SetActiveSkinSilent()`、`SetActiveSkinSource()`、`SetActiveSkinWithSource()`、`ClearSkin()` 的皮肤应用与来源标记逻辑收敛为内部静态协调器 `SkinApplicationCoordinator`

### 目标
- 继续降低 `CompPawnSkin` 主类中皮肤设置/清除/来源标记逻辑的分散度
- 将“手动应用皮肤”“静默设置皮肤”“来源标记更新”“清除皮肤”这些入口的状态切换语义显式化
- 为后续继续统一默认种族皮肤注入、生命周期恢复与手动应用逻辑建立更稳定的皮肤应用边界

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 先通过引用搜索确认写入边界：
  - 外部大量代码主要只读消费 `ActiveSkin`
  - 写入入口主要仍集中在 `CompPawnSkin` 自身和 `PawnSkinRuntimeUtility`
- 新增内部私有静态类：`SkinApplicationCoordinator`
- 在协调器内部统一收敛以下入口：
  - `ApplyManual(CompPawnSkin owner, PawnSkinDef? skin)`
  - `SetWithSourceSilently(CompPawnSkin owner, PawnSkinDef? skin, bool fromDefaultRaceBinding)`
  - `UpdateSource(CompPawnSkin owner, bool fromDefaultRaceBinding)`
  - `Clear(CompPawnSkin owner)`
  - 私有辅助：`Apply(...)`
  - 私有辅助：`SetSkin(...)`
- 将以下主类入口改为统一经由协调器执行：
  - `ActiveSkin` setter
  - `SetActiveSkinSilent()`
  - `SetActiveSkinSource()`
  - `SetActiveSkinWithSource()`
  - `ClearSkin()`
- 将 `ActiveSkin` setter 的写入条件收束为：
  - `activeSkin != value || activeSkinFromDefaultRaceBinding`
  - 这样当当前皮肤来自默认种族绑定时，即使赋相同皮肤，也能通过一次手动应用把来源语义切换为“非默认绑定”
- 保持手动应用时仍执行：
  - `SyncXenotype(...)`
  - `FaceRuntimeRefreshCoordinator.Apply(..., immediateRefresh: true, deferredRefresh: true)`

### 预期不变行为
- 外部读取 `ActiveSkin`、`HasActiveSkin`、`ActiveSkinFromDefaultRaceBinding` 的方式不变
- `PawnSkinRuntimeUtility` 仍可通过 `SetActiveSkinWithSource()` 进行静默应用
- `ClearSkin()` 后的清空、脏标记与刷新语义保持兼容
- 手动应用皮肤时 Xenotype 同步与刷新行为不变
- 来源标记仍可区分“默认种族绑定”与“手动应用”

### 允许变化行为
- `CompPawnSkin` 内部从“多处散落的应用/来源切换逻辑”变为“统一皮肤应用协调入口 + 高层调用”
- 当当前皮肤来自默认种族绑定时，对同一皮肤执行手动赋值会显式转换来源语义为手动应用
- 皮肤应用职责的维护位置更集中，后续协调默认皮肤注入与恢复入口的迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 warning / 0 error**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮改动未引入新的编译问题，工程继续保持零警告状态
- 说明皮肤应用、静默设置、来源切换与清除入口已经从主类分散逻辑进一步收束为统一边界

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的皮肤应用组织方式与来源标记切换语义
- 对外只读消费 API 未变化，主要影响内部写入入口的承载位置
- 属于低风险的“皮肤应用协调层收束”重构

### 当前风险观察
- `SkinApplicationCoordinator` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- `SkinLifecycleRecoveryCoordinator.TryApplyDefaultRaceSkin(...)` 仍直接写入 `activeSkin` 等字段，尚未完全复用 `SkinApplicationCoordinator`
- `ActiveSkin` setter 现在允许“默认绑定的同皮肤再次赋值”转化为手动来源语义，这一行为虽然是设计上更清晰的来源收束，但仍建议后续做游戏内手工验证
- 尚未做游戏内手工验证，因此不能完全确认默认皮肤转手动皮肤、静默应用后来源标记、清除皮肤后的运行时恢复在真实运行时 100% 与之前一致
- 但从构建结果与修改规模判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `SkinLifecycleRecoveryCoordinator` 中默认种族皮肤注入与恢复路径进一步改为复用 `SkinApplicationCoordinator`，统一“恢复/应用/来源标记”的协作边界
  2. 评估预览覆盖 API、Blink/动画刷新路径是否继续接入统一刷新与生命周期边界
- 若继续保持低风险节奏，建议优先处理 **生命周期恢复与皮肤应用协调器协作边界统一**，因为当前已经形成“恢复协调器 + 应用协调器”双层结构，下一步最自然的是进一步减少它们对底层字段的直接写入
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 18：CompPawnSkin 生命周期恢复与皮肤应用协调器协作边界统一（第十七轮）

### 重构步骤名称
- 将 `SkinLifecycleRecoveryCoordinator` 中恢复后的皮肤写回、默认种族皮肤注入逻辑进一步改为复用 `SkinApplicationCoordinator`

### 目标
- 继续降低 `CompPawnSkin` 中不同协调器对底层字段的直接写入次数
- 将“恢复已解析皮肤”“注入默认绑定皮肤”这两类写入行为进一步收敛到统一皮肤应用边界
- 推进 `CompPawnSkin` 从“多个协调器各自写字段”向“恢复协调器负责流程、应用协调器负责写入”的分层结构继续收束

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinApplicationCoordinator` 中新增：
  - `RestoreResolved(CompPawnSkin owner, PawnSkinDef? skin, bool fromDefaultRaceBinding)`
  - `ApplyDefaultBound(CompPawnSkin owner, PawnSkinDef? skin)`
- 将 `SkinLifecycleRecoveryCoordinator.RestoreAfterSpawn(...)` 中的恢复后写回逻辑改为：
  - 先解析 `restoredSkin`
  - 再统一调用 `SkinApplicationCoordinator.RestoreResolved(...)`
- 将 `SkinLifecycleRecoveryCoordinator.RestoreAfterLoad(...)` 中的恢复后写回逻辑改为：
  - 先解析 `restoredSkin`
  - 再统一调用 `SkinApplicationCoordinator.RestoreResolved(...)`
  - 之后保持 `activeSkinDefName = restoredSkin.defName` 的既有归一化行为
- 将 `SkinLifecycleRecoveryCoordinator.TryApplyDefaultRaceSkin(...)` 中的默认种族皮肤注入改为：
  - `SkinApplicationCoordinator.ApplyDefaultBound(owner, defaultSkin.Clone())`
- 保持恢复流程与默认皮肤注入的高层控制权仍留在 `SkinLifecycleRecoveryCoordinator`，但把底层字段写入与来源标记设置继续收敛到应用协调器

### 预期不变行为
- 出生后恢复皮肤的顺序与语义不变
- 读档后恢复皮肤时仍先尝试 `DefDatabase<PawnSkinDef>.GetNamedSilentFail(...)`，失败后回退到 `PawnSkinDefRegistry.TryGet(...)`
- 默认种族皮肤仍以 `Clone()` 形式注入，且继续标记为默认绑定来源
- 默认种族皮肤注入后仍会触发 face runtime dirty 与延迟刷新
- `activeSkinDefName` 在恢复成功后仍会归一化为实际 `defName`

### 允许变化行为
- `CompPawnSkin` 内部从“恢复协调器直接写字段”进一步变为“恢复协调器负责流程，应用协调器负责统一写入”
- 协调器之间的职责边界更清晰
- 后续继续统一手动应用、默认应用、恢复应用三类路径时迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮改动未引入编译错误
- build 暴露出的 4 个 warning 位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 属于渲染树注入模块，不属于本轮 `CompPawnSkin` 协调器协作边界统一的直接改动范围
- 按当前执行规则，**不属于本轮修改模块的 warning 直接忽略**，因此本轮仍视为通过

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的生命周期恢复路径与皮肤应用协调器之间的内部协作方式
- 对外消费 API 未变化，主要影响内部恢复后写回与默认皮肤注入的承载位置
- 属于低风险的“协调器协作边界统一”重构

### 当前风险观察
- `SkinLifecycleRecoveryCoordinator` 与 `SkinApplicationCoordinator` 仍然都是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- build 新暴露的 4 个 warning 位于 `Patch_PawnRenderTree.Injection.cs`，不属于本轮修改模块，当前按规则忽略
- 本轮真正需要关注的风险仍集中在 `CompPawnSkin` 内部：恢复路径、默认皮肤注入与来源标记协作是否在运行时与既有语义完全一致
- 尚未做游戏内手工验证，因此不能完全确认默认皮肤恢复、读档恢复与来源标记协作在真实运行时 100% 与之前一致
- 但从构建结果与修改规模判断，本轮可视为通过并继续推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `SkinLifecycleRecoveryCoordinator` 中皮肤解析与恢复顺序继续收敛为共享 helper，进一步减少 `RestoreAfterSpawn()` / `RestoreAfterLoad()` 的重复逻辑
  2. 评估预览覆盖 API 与运行时刷新触发路径是否继续接入更统一的协同边界
- 若继续保持低风险节奏，建议优先处理 **生命周期恢复中的皮肤解析 helper 收束**，因为当前恢复协调器与应用协调器的写入边界已经统一，下一步最自然的是继续压缩恢复流程本身的重复解析逻辑
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 19：CompPawnSkin 生命周期恢复中的皮肤解析 helper 收束（第十八轮）

### 重构步骤名称
- 将 `SkinLifecycleRecoveryCoordinator` 中保存皮肤解析与恢复写回流程收敛为共享 helper，并保留 Spawn / Load 路径的既有语义差异

### 目标
- 进一步减少 `RestoreAfterSpawn()` / `RestoreAfterLoad()` 中重复的皮肤解析与恢复写回逻辑
- 将“是否允许 registry fallback”“是否归一化 `activeSkinDefName`”这两类语义差异显式参数化
- 保持恢复协调器负责流程编排，避免共享 helper 误抹平生命周期差异

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinLifecycleRecoveryCoordinator` 中新增共享 helper：
  - `TryRestoreSavedSkin(CompPawnSkin owner, bool allowRegistryFallback, bool normalizeDefName)`
  - `ResolveSavedSkin(string? skinDefName, bool allowRegistryFallback)`
- 将 `RestoreAfterSpawn(...)` 改为：
  - 仅在 `activeSkin == null` 时调用 `TryRestoreSavedSkin(...)`
  - 明确使用 `allowRegistryFallback: false`
  - 明确使用 `normalizeDefName: false`
- 将 `RestoreAfterLoad(...)` 改为：
  - 统一调用 `TryRestoreSavedSkin(...)`
  - 明确使用 `allowRegistryFallback: true`
  - 明确使用 `normalizeDefName: true`
- 在本轮中额外修正了一次 helper 抽取后的语义漂移：
  - 初版共享 helper 会对 Spawn 恢复路径也执行 `activeSkinDefName = restoredSkin.defName`
  - 已回调为仅在 Load 恢复路径归一化 `activeSkinDefName`
- 保持以下行为继续不变：
  - Spawn 恢复只走 `DefDatabase<PawnSkinDef>.GetNamedSilentFail(...)`
  - Load 恢复在 `DefDatabase` 失败后才回退到 `PawnSkinDefRegistry.TryGet(...)`
  - 恢复成功后的底层写回仍复用 `SkinApplicationCoordinator.RestoreResolved(...)`

### 预期不变行为
- `PostSpawnSetup()` 阶段的皮肤恢复顺序不变
- Spawn 恢复不新增 registry fallback
- `PostLoadInit` 阶段仍允许 registry fallback 恢复已保存皮肤
- 只有 Load 恢复路径继续归一化 `activeSkinDefName`
- 恢复成功后的来源标记与后续刷新行为保持兼容

### 允许变化行为
- `SkinLifecycleRecoveryCoordinator` 内部从“两条入口各自展开解析逻辑”变为“共享 helper + 显式差异参数”
- Spawn / Load 的语义差异从隐式分散代码转为显式参数化
- 后续继续整理恢复流程时，更容易避免误合并不同生命周期语义

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明共享 helper 抽取与语义回调修正未破坏编译链路，且 Spawn / Load 的恢复差异已被更显式地固化下来

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的皮肤生命周期恢复流程组织方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部保存皮肤解析与恢复写回的承载位置
- 属于低风险的“重复恢复逻辑归并 + 生命周期语义显式化”重构

### 当前风险观察
- `SkinLifecycleRecoveryCoordinator` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- 本轮在共享 helper 抽取过程中，曾短暂把 `activeSkinDefName` 归一化扩散到 Spawn 恢复路径；虽然已在本轮内修正，但说明恢复语义差异仍需持续谨慎维护
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、读档 fallback 恢复与来源标记协作在真实运行时 100% 与之前一致
- 但从修正后的代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `SkinLifecycleRecoveryCoordinator` 中“尝试恢复 -> 默认皮肤回退 -> 最终刷新”这段 spawn 生命周期编排继续收敛为更明确的辅助入口，进一步压缩 `RestoreAfterSpawn()` 的流程噪音
  2. 评估 `RequestRenderRefresh()` 的直接调用点是否可以在生命周期恢复路径中进一步统一
- 若继续保持低风险节奏，建议优先处理 **spawn 恢复编排 helper 收束**，因为保存皮肤解析差异已经参数化，下一步最自然的是继续压缩恢复主流程本身的条件编排
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 20：CompPawnSkin Spawn 恢复编排 helper 收束（第十九轮）

### 重构步骤名称
- 将 `RestoreAfterSpawn()` 中“尝试恢复保存皮肤 -> 默认皮肤回退”的主流程收敛为辅助入口 `EnsureSkinAvailableAfterSpawn(...)`

### 目标
- 进一步压缩 `RestoreAfterSpawn()` 中的条件编排噪音
- 将 Spawn 生命周期中的“保证最终可用皮肤”职责显式化
- 保持刷新触发仍由高层入口掌控，避免把可用性恢复与最终刷新混在同一层

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinLifecycleRecoveryCoordinator` 中新增辅助入口：
  - `EnsureSkinAvailableAfterSpawn(CompPawnSkin owner)`
- 将 `RestoreAfterSpawn(...)` 收束为：
  - 先调用 `EnsureSkinAvailableAfterSpawn(owner)`
  - 再仅在 `activeSkin != null` 时执行 `RequestRenderRefresh()`
- 将原本展开在 `RestoreAfterSpawn(...)` 中的逻辑迁移到 helper 内部：
  - 若已有 `activeSkin`，直接返回
  - 否则执行 `TryRestoreSavedSkin(owner, allowRegistryFallback: false, normalizeDefName: false)`
  - 若仍无活动皮肤，再执行 `TryApplyDefaultRaceSkin(owner)`
- 保持以下既有语义继续不变：
  - Spawn 恢复仍不允许 registry fallback
  - Spawn 恢复仍不归一化 `activeSkinDefName`
  - 最终刷新仍由 `RestoreAfterSpawn(...)` 统一决定

### 预期不变行为
- `PostSpawnSetup()` 阶段的恢复顺序不变
- Spawn 恢复仍优先尝试保存皮肤，再回退到默认种族皮肤
- Spawn 路径仍不引入 registry fallback
- Spawn 路径仍不引入 `activeSkinDefName` 归一化
- 恢复成功后仍会执行渲染刷新

### 允许变化行为
- `SkinLifecycleRecoveryCoordinator` 内部从“主入口直接展开完整条件分支”变为“高层入口 + Spawn 可用性 helper”
- Spawn 生命周期恢复编排的维护位置更集中
- 后续继续整理默认皮肤回退或刷新策略时，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明 Spawn 恢复编排 helper 抽取未破坏编译链路，且恢复主流程已进一步从入口方法中收束出来

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的 Spawn 生命周期恢复组织方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部恢复编排的承载位置
- 属于低风险的“Spawn 恢复主流程收束”重构

### 当前风险观察
- `SkinLifecycleRecoveryCoordinator` 仍然是 `CompPawnSkin` 内部嵌套静态类，尚未成为独立文件级组件
- 当前只是把 Spawn 可用性恢复从主入口中抽出，`RequestRenderRefresh()` 仍由 `RestoreAfterSpawn(...)` 直接调用，说明生命周期恢复与最终刷新边界尚未完全统一
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、默认种族皮肤回退与最终刷新协作在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `TryApplyDefaultRaceSkin()` 中默认皮肤解析与前置条件检查继续收敛为共享 helper，进一步压缩 Spawn 恢复回退路径中的局部分支
  2. 评估 `RestoreAfterSpawn()` 末尾 `RequestRenderRefresh()` 与默认皮肤延迟刷新之间是否存在进一步统一空间
- 若继续保持低风险节奏，建议优先处理 **默认种族皮肤解析 helper 收束**，因为 Spawn 主流程已经收束完成，下一步最自然的是继续压缩默认皮肤回退分支本身的条件噪音
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 21：CompPawnSkin 默认种族皮肤解析 helper 收束（第二十轮）

### 重构步骤名称
- 将 `TryApplyDefaultRaceSkin()` 中默认种族皮肤的前置条件检查与 clone 解析收敛为辅助入口 `ResolveDefaultRaceSkinToApply(...)`

### 目标
- 进一步压缩默认种族皮肤回退路径中的局部分支噪音
- 将“当前 Pawn 是否允许默认皮肤注入”“是否存在可应用默认皮肤”“是否需要 clone”的判断集中到单一 helper
- 保持默认皮肤应用动作继续由高层入口掌控，避免解析与应用职责混杂

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinLifecycleRecoveryCoordinator` 中新增辅助入口：
  - `ResolveDefaultRaceSkinToApply(CompPawnSkin owner)`
- 将 `TryApplyDefaultRaceSkin(...)` 收束为：
  - 先调用 `ResolveDefaultRaceSkinToApply(owner)`
  - 若结果为 `null` 直接返回
  - 若结果存在，则统一调用 `SkinApplicationCoordinator.ApplyDefaultBound(owner, defaultSkin)`
- 将原本展开在 `TryApplyDefaultRaceSkin(...)` 中的逻辑迁移到 helper 内部：
  - 校验 `Pawn` 与 `pawn.def` 非空
  - 校验 `pawn.RaceProps.Humanlike`
  - 校验当前 `owner.activeSkin == null`
  - 通过 `PawnSkinDefRegistry.GetDefaultSkinForRace(pawn.def)` 解析默认种族皮肤
  - 返回 `defaultSkin?.Clone()` 供应用层消费
- 保持默认皮肤的实际注入动作继续由：
  - `SkinApplicationCoordinator.ApplyDefaultBound(...)`
  负责

### 预期不变行为
- 只有 Humanlike Pawn 且当前无活动皮肤时，才可能注入默认种族皮肤
- 默认种族皮肤仍由 `PawnSkinDefRegistry.GetDefaultSkinForRace(...)` 解析
- 默认种族皮肤在应用前仍会执行 `Clone()`，避免共享实例污染
- 默认皮肤应用后仍会保留默认绑定来源语义
- face runtime dirty 与延迟刷新语义保持兼容

### 允许变化行为
- 默认种族皮肤回退从“入口方法内联前置检查 + clone”变为“解析 helper + 应用入口”
- 默认皮肤回退分支的维护位置更集中
- 后续继续统一 Spawn 恢复与刷新边界时，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明默认种族皮肤解析 helper 抽取未破坏编译链路，且默认皮肤回退分支已进一步从入口方法中收束出来

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的默认种族皮肤回退组织方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部默认皮肤解析与应用前准备的承载位置
- 属于低风险的“默认皮肤解析分支收束”重构

### 当前风险观察
- `ResolveDefaultRaceSkinToApply(...)` 仍然是 `SkinLifecycleRecoveryCoordinator` 内部私有 helper，尚未成为独立文件级组件
- 当前只是把默认皮肤解析与 clone 前准备从入口中抽出，`TryApplyDefaultRaceSkin(...)` 仍负责实际应用，`RestoreAfterSpawn(...)` 仍负责最终刷新，说明生命周期恢复与刷新边界尚未完全统一
- 尚未做游戏内手工验证，因此不能完全确认默认种族皮肤回退、默认来源标记与延迟刷新协作在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 评估 `RestoreAfterSpawn()` 末尾 `RequestRenderRefresh()` 与 `ApplyDefaultBound(...)` 的延迟刷新语义是否可以继续收敛为更统一的 Spawn 刷新决策 helper
  2. 继续压缩 `SkinLifecycleRecoveryCoordinator` 中 `RestoreAfterSpawn()` / `TryApplyDefaultRaceSkin()` 的高层编排，使“可用性恢复”和“最终刷新”边界更清晰
- 若继续保持低风险节奏，建议优先处理 **Spawn 最终刷新决策收束**，因为默认皮肤解析分支已经收束完成，下一步最自然的是继续统一恢复成功后的刷新触发策略
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 22：CompPawnSkin Spawn 最终刷新决策收束（第二十一轮）

### 重构步骤名称
- 将 `RestoreAfterSpawn()` 末尾的刷新判断与 Spawn 可用性恢复 helper 的返回契约统一为 `TryEnsureSkinAvailableAfterSpawn(...)`

### 目标
- 继续压缩 Spawn 生命周期恢复路径中“可用性恢复”和“最终刷新”之间的隐式耦合
- 将“恢复后是否已有可用皮肤”从入口末尾字段判断改为 helper 显式返回值
- 保持最终刷新仍由高层入口控制，避免刷新决策散落在恢复分支内部

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 将 `EnsureSkinAvailableAfterSpawn(...)` 调整为：
  - `TryEnsureSkinAvailableAfterSpawn(CompPawnSkin owner)`
- 将该 helper 的职责从“仅执行恢复流程”改为“执行恢复流程并返回是否已获得可用皮肤”
- 将 `RestoreAfterSpawn(...)` 收束为：
  - `if (TryEnsureSkinAvailableAfterSpawn(owner)) owner.RequestRenderRefresh();`
- 在 `TryEnsureSkinAvailableAfterSpawn(...)` 内部显式固化以下返回契约：
  - 若进入时已有 `activeSkin`，直接返回 `true`
  - 否则先执行 `TryRestoreSavedSkin(owner, allowRegistryFallback: false, normalizeDefName: false)`
  - 若仍无活动皮肤，再执行 `TryApplyDefaultRaceSkin(owner)`
  - 最终统一返回 `owner.activeSkin != null`
- 保持 Spawn 恢复顺序、默认皮肤回退顺序与刷新触发时机继续不变

### 预期不变行为
- `PostSpawnSetup()` 阶段仍先尝试恢复已保存皮肤，再回退到默认种族皮肤
- Spawn 路径仍不允许 registry fallback
- Spawn 路径仍不归一化 `activeSkinDefName`
- 只有恢复后存在可用皮肤时，才会执行最终 `RequestRenderRefresh()`
- 默认绑定皮肤的来源标记、face runtime dirty 与延迟刷新语义保持兼容

### 允许变化行为
- Spawn 最终刷新决策从“入口末尾读取字段”变为“helper 显式返回恢复结果”
- `SkinLifecycleRecoveryCoordinator` 中恢复成功判定与刷新触发边界更清晰
- 后续若继续统一 Spawn / Load 恢复后的刷新契约，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明 Spawn 最终刷新决策的返回契约收束未破坏编译链路，且恢复成功与刷新触发之间的关系已经更显式地固化下来

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的 Spawn 生命周期恢复与最终刷新决策组织方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部恢复成功判定的承载方式
- 属于低风险的“恢复结果显式化 + 刷新决策收束”重构

### 当前风险观察
- `TryEnsureSkinAvailableAfterSpawn(...)` 仍然是 `SkinLifecycleRecoveryCoordinator` 内部私有 helper，尚未成为独立文件级组件
- 当前只是将 Spawn 最终刷新条件显式化，`ApplyDefaultBound(...)` 内部的延迟刷新语义与 `RestoreAfterSpawn(...)` 的即时刷新语义仍并存，说明生命周期恢复刷新策略尚未完全统一
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、默认种族皮肤回退与最终刷新协作在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `RestoreAfterSpawn()` / `RestoreAfterLoad()` 成功恢复后的刷新触发语义继续收敛为更统一的生命周期恢复刷新 helper
  2. 评估 `ApplyDefaultBound(...)` 的延迟刷新与 Spawn 路径即时刷新之间是否可以形成更清晰的分层边界
- 若继续保持低风险节奏，建议优先处理 **生命周期恢复刷新策略 helper 收束**，因为 Spawn 恢复成功判定已经显式化，下一步最自然的是继续统一恢复结果与刷新触发之间的契约
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 23：CompPawnSkin 生命周期恢复刷新策略 helper 收束（第二十二轮）

### 重构步骤名称
- 将 `RestoreAfterSpawn()` / `RestoreAfterLoad()` 成功恢复后的刷新触发语义继续收敛为共享 helper `ApplyRecoveryRefresh(...)`

### 目标
- 继续压缩 `SkinLifecycleRecoveryCoordinator` 中“恢复成功判定”与“最终刷新触发”之间的重复连接逻辑
- 将 Spawn / Load 两条生命周期入口在“恢复结果 -> 是否立即刷新”这一层的协作边界显式化
- 保持既有运行时语义不变：
  - Spawn 恢复成功后仍立即刷新
  - Load 恢复成功后仍不立即刷新

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.PreviewLifecycle.cs`

### 本轮改动点
- 在 `SkinLifecycleRecoveryCoordinator` 中新增共享 helper：
  - `ApplyRecoveryRefresh(CompPawnSkin owner, bool hasRecoveredSkin, bool immediateRefresh)`
- 将 `RestoreAfterSpawn(...)` 收束为：
  - 先调用 `TryEnsureSkinAvailableAfterSpawn(owner)` 获取恢复结果
  - 再统一通过 `ApplyRecoveryRefresh(owner, hasRecoveredSkin, immediateRefresh: true)` 处理最终刷新
- 将 `RestoreAfterLoad(...)` 收束为：
  - 先调用 `TryRestoreSavedSkin(owner, allowRegistryFallback: true, normalizeDefName: true)` 获取恢复结果
  - 再统一通过 `ApplyRecoveryRefresh(owner, hasRecoveredSkin, immediateRefresh: false)` 保持“读档恢复后不立即刷新”的既有语义
- 保持以下行为继续不变：
  - Spawn 路径仍不允许 registry fallback
  - Spawn 路径仍不归一化 `activeSkinDefName`
  - Load 路径仍允许 registry fallback，且恢复成功后继续归一化 `activeSkinDefName`
- 在本轮 build review 过程中，暴露出一个独立的编译阻塞：
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.PreviewLifecycle.cs(49,20)`
  - 错误：`CS0103`，当前上下文中不存在名称 `ThingDefOf`
- 对该阻塞做最小修复：
  - 将 `ThingDefOf.Human` 改为 `DefDatabase<ThingDef>.GetNamed("Human")`
  - 仅修复默认预览种族回退解析，不扩大 UI 模块改动范围

### 预期不变行为
- Spawn 生命周期中仍先尝试恢复保存皮肤，再回退到默认种族皮肤
- Spawn 恢复成功后仍立即执行渲染刷新
- Load 恢复成功后仍不新增即时刷新
- 默认种族皮肤的来源标记、face runtime dirty 与延迟刷新语义保持兼容
- 编辑器预览重置时，默认种族回退仍指向 `Human`

### 允许变化行为
- 生命周期恢复刷新策略从“入口方法各自决定刷新”变为“共享 helper + 显式参数化的立即刷新语义”
- `SkinLifecycleRecoveryCoordinator` 中恢复结果与刷新触发之间的边界更集中
- `Dialog_SkinEditor.PreviewLifecycle` 中默认预览种族回退不再依赖当前编译环境下不可见的 `ThingDefOf` 符号

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 初次 build 时暴露出一个编译阻塞：
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.PreviewLifecycle.cs(49,20)`
  - 错误：`CS0103`，当前上下文中不存在名称 `ThingDefOf`
- 对默认预览种族回退做最小修复后，重新构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` / `Dialog_SkinEditor.PreviewLifecycle` 直接目标模块，按当前执行规则忽略
- 说明生命周期恢复刷新 helper 收束与最小 UI 编译修复均未破坏编译链路

### 影响范围评估
- 影响范围主要集中在 `CompPawnSkin` 的生命周期恢复刷新策略组织方式
- 次要影响到 `Dialog_SkinEditor` 预览重置时的默认种族回退解析
- 对外消费 API 未变化，主要影响内部恢复结果与刷新触发的承载位置
- 属于低风险的“恢复刷新契约收束 + 构建阻塞修复”重构

### 当前风险观察
- `ApplyRecoveryRefresh(...)` 仍然采用 `immediateRefresh` 布尔参数表达 Spawn / Load 的语义差异，说明刷新策略虽然更集中，但尚未完全转化为更具语义化的生命周期策略入口
- `Dialog_SkinEditor.PreviewLifecycle` 当前通过 `DefDatabase<ThingDef>.GetNamed("Human")` 做默认种族回退，依赖 `Human` def 在运行环境中可用；在 RimWorld 主环境下这是低风险假设，但仍建议后续在编辑器实际打开路径中顺手验证
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、读档恢复、默认预览种族回退与刷新协作在真实运行时 100% 与之前一致
- 但从代码结构、修复范围与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `RestoreAfterLoad()` 的恢复结果继续显式化为与 Spawn 路径更对称的 helper，进一步压缩 Load 生命周期入口的流程噪音
  2. 评估 `ApplyRecoveryRefresh(...)` 中 `immediateRefresh` 布尔参数是否可以继续收敛为更具语义的生命周期刷新策略入口
- 若继续保持低风险节奏，建议优先处理 **Load 恢复结果 helper 对称收束**，因为 Spawn 路径已经具备显式 bool 契约，下一步最自然的是让 Load 路径也具备同级别的恢复结果表达边界
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 24：CompPawnSkin Load 恢复结果 helper 对称收束（第二十三轮）

### 重构步骤名称
- 将 `RestoreAfterLoad()` 中内联的恢复结果计算收敛为辅助入口 `TryRestoreSkinAfterLoad(...)`

### 目标
- 让 Load 生命周期路径与 Spawn 路径一样具备显式的恢复结果 helper 边界
- 进一步压缩 `RestoreAfterLoad()` 的流程噪音
- 保持读档恢复成功后仍不立即刷新，避免引入语义漂移

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinLifecycleRecoveryCoordinator` 中新增辅助入口：
  - `TryRestoreSkinAfterLoad(CompPawnSkin owner)`
- 将 `RestoreAfterLoad(...)` 收束为：
  - 先调用 `TryRestoreSkinAfterLoad(owner)` 获取恢复结果
  - 再统一通过 `ApplyRecoveryRefresh(owner, hasRecoveredSkin, immediateRefresh: false)` 保持既有刷新语义
- 将原本展开在 `RestoreAfterLoad(...)` 中的读档恢复逻辑迁移到 helper 内部：
  - `TryRestoreSavedSkin(owner, allowRegistryFallback: true, normalizeDefName: true)`
- 保持以下既有行为继续不变：
  - Load 路径仍允许 registry fallback
  - Load 路径恢复成功后仍归一化 `activeSkinDefName`
  - Load 路径恢复成功后仍不新增即时刷新

### 预期不变行为
- `PostExposeData()` 的 `PostLoadInit` 阶段仍会尝试恢复 `activeSkinDefName`
- 读档恢复时仍先走 `DefDatabase<PawnSkinDef>.GetNamedSilentFail(...)`，失败后再回退到 `PawnSkinDefRegistry.TryGet(...)`
- 恢复成功后仍会归一化 `activeSkinDefName`
- 恢复成功后仍不立即调用 `RequestRenderRefresh()`
- 来源标记、face runtime dirty 与后续延迟刷新语义保持兼容

### 允许变化行为
- `SkinLifecycleRecoveryCoordinator` 内部从“Load 入口内联恢复结果计算”变为“Load helper + 高层入口”
- Spawn / Load 在“恢复结果计算”这一层的结构更对称
- 后续继续整理生命周期刷新策略时，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明 Load 恢复结果 helper 的对称收束未破坏编译链路，且生命周期恢复路径的结构对称性进一步增强

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的 Load 生命周期恢复组织方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部读档恢复结果的承载位置
- 属于低风险的“Load 恢复结果显式化 + 结构对称收束”重构

### 当前风险观察
- `TryRestoreSkinAfterLoad(...)` 目前仍然只是对 `TryRestoreSavedSkin(...)` 的一层薄封装，说明 Load 路径的结构虽然更对称，但读档恢复语义尚未形成更独立的策略边界
- `ApplyRecoveryRefresh(...)` 仍然通过 `immediateRefresh` 布尔参数表达 Spawn / Load 的差异，说明刷新策略的语义仍可继续提升
- 尚未做游戏内手工验证，因此不能完全确认读档恢复、来源标记协作与延迟刷新时机在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `ApplyRecoveryRefresh(...)` 中 `immediateRefresh` 布尔参数继续收敛为更具语义的 Spawn / Load 生命周期刷新入口
  2. 评估 `TryEnsureSkinAvailableAfterSpawn(...)` 与 `TryRestoreSkinAfterLoad(...)` 是否还能进一步统一成更清晰的“恢复结果策略层”
- 若继续保持低风险节奏，建议优先处理 **生命周期刷新语义入口收束**，因为 Spawn / Load 的恢复结果 helper 已经成型，下一步最自然的是继续消除 `immediateRefresh` 这种偏实现细节的布尔参数
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 25：CompPawnSkin 生命周期刷新语义入口收束（第二十四轮）

### 重构步骤名称
- 将 `ApplyRecoveryRefresh(...)` 的布尔刷新参数收束为具名的 Spawn / Load 生命周期刷新入口

### 目标
- 去除 `SkinLifecycleRecoveryCoordinator` 中 `immediateRefresh` 这种偏实现细节的布尔参数
- 让 Spawn / Load 两条生命周期路径的刷新语义通过具名 helper 显式表达
- 保持既有运行时语义不变：
  - Spawn 恢复成功后仍立即刷新
  - Load 恢复成功后仍不立即刷新

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 删除共享布尔参数入口：
  - `ApplyRecoveryRefresh(CompPawnSkin owner, bool hasRecoveredSkin, bool immediateRefresh)`
- 新增具名生命周期刷新入口：
  - `ApplySpawnRecoveryRefresh(CompPawnSkin owner, bool hasRecoveredSkin)`
  - `ApplyLoadRecoveryRefresh(bool hasRecoveredSkin)`
- 将 `RestoreAfterSpawn(...)` 收束为：
  - 先调用 `TryEnsureSkinAvailableAfterSpawn(owner)` 获取恢复结果
  - 再统一通过 `ApplySpawnRecoveryRefresh(owner, hasRecoveredSkin)` 处理 Spawn 恢复后的立即刷新
- 将 `RestoreAfterLoad(...)` 收束为：
  - 先调用 `TryRestoreSkinAfterLoad(owner)` 获取恢复结果
  - 再统一通过 `ApplyLoadRecoveryRefresh(hasRecoveredSkin)` 显式保留“读档恢复后不立即刷新”的语义
- 在 `ApplyLoadRecoveryRefresh(...)` 中保留空操作注释：
  - `// 保持既有语义：读档恢复后不立即刷新。`
- 保持以下既有行为继续不变：
  - Spawn 路径恢复成功时仍调用 `RequestRenderRefresh()`
  - Load 路径恢复成功后仍不触发立即刷新
  - Spawn / Load 的恢复顺序、fallback 规则与 `activeSkinDefName` 归一化语义不变

### 预期不变行为
- `PostSpawnSetup()` 阶段仍先尝试恢复保存皮肤，再回退到默认种族皮肤
- Spawn 恢复成功后仍立即执行渲染刷新
- `PostLoadInit` 阶段仍会尝试恢复 `activeSkinDefName`
- Load 恢复成功后仍不立即调用 `RequestRenderRefresh()`
- 默认种族皮肤来源标记、face runtime dirty 与延迟刷新语义保持兼容

### 允许变化行为
- 生命周期恢复刷新策略从“一个 helper + 布尔参数”变为“两个具名生命周期刷新入口”
- Spawn / Load 的刷新语义边界更显式，代码阅读时不再需要反向推断 `immediateRefresh: true/false`
- 后续若继续抽象生命周期策略层，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明生命周期刷新语义入口收束未破坏编译链路，且 Spawn / Load 的刷新语义表达已经比布尔参数形式更明确

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的生命周期恢复刷新策略组织方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部刷新入口的承载方式
- 属于低风险的“刷新语义具名化 + 生命周期入口收束”重构

### 当前风险观察
- `ApplyLoadRecoveryRefresh(...)` 当前仍然是“显式空操作 + 注释”的语义占位，说明 Load 恢复后的刷新策略虽然更清晰，但尚未沉淀为更完整的策略对象或结果模型
- `TryEnsureSkinAvailableAfterSpawn(...)` 与 `TryRestoreSkinAfterLoad(...)` 仍分别代表两种恢复路径，说明恢复策略层与刷新策略层目前只是部分解耦
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、读档恢复与延迟刷新时机在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 评估 `TryEnsureSkinAvailableAfterSpawn(...)` 与 `TryRestoreSkinAfterLoad(...)` 是否可以继续统一为更清晰的恢复结果策略层
  2. 评估 `ApplyLoadRecoveryRefresh(...)` 的空操作语义是否值得保留为更明确的“no-op policy”表达，或与恢复结果模型进一步合并
- 若继续保持低风险节奏，建议优先处理 **恢复结果策略层收束**，因为 Spawn / Load 的刷新入口已经具名化，下一步最自然的是继续统一恢复结果本身的承载边界
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 26：CompPawnSkin 恢复结果策略层收束（第二十五轮）

### 重构步骤名称
- 将 `TryEnsureSkinAvailableAfterSpawn(...)` 与 `TryRestoreSkinAfterLoad(...)` 的共享恢复分支收敛为参数化 helper `TryRecoverSkin(...)`

### 目标
- 继续统一 Spawn / Load 两条恢复路径在“恢复结果”这一层的表达方式
- 将 `allowRegistryFallback`、`normalizeDefName`、`allowDefaultRaceFallback`、`treatExistingActiveSkinAsSuccess` 这四类生命周期差异显式参数化
- 保持 Spawn / Load 的既有行为不变，避免策略收束时误抹平语义边界

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinLifecycleRecoveryCoordinator` 中新增共享 helper：
  - `TryRecoverSkin(CompPawnSkin owner, bool allowRegistryFallback, bool normalizeDefName, bool allowDefaultRaceFallback, bool treatExistingActiveSkinAsSuccess)`
- 将 `TryRestoreSkinAfterLoad(...)` 收束为：
  - `TryRecoverSkin(owner, allowRegistryFallback: true, normalizeDefName: true, allowDefaultRaceFallback: false, treatExistingActiveSkinAsSuccess: false)`
- 将 `TryEnsureSkinAvailableAfterSpawn(...)` 收束为：
  - `TryRecoverSkin(owner, allowRegistryFallback: false, normalizeDefName: false, allowDefaultRaceFallback: true, treatExistingActiveSkinAsSuccess: true)`
- 在 `TryRecoverSkin(...)` 中统一固化以下流程：
  - 若 `treatExistingActiveSkinAsSuccess` 且当前已有 `activeSkin`，直接返回 `true`
  - 否则执行 `TryRestoreSavedSkin(owner, allowRegistryFallback, normalizeDefName)`
  - 若仍未恢复成功且允许默认种族回退，则执行 `TryApplyDefaultRaceSkin(owner)`
  - 最终统一返回是否已有可用皮肤
- 保持 `ApplySpawnRecoveryRefresh(...)` / `ApplyLoadRecoveryRefresh(...)` 两条具名刷新入口继续不变

### 预期不变行为
- Spawn 路径中，若当前已有 `activeSkin`，仍视为恢复成功
- Spawn 路径仍不允许 registry fallback
- Spawn 路径仍不归一化 `activeSkinDefName`
- Spawn 路径在保存皮肤恢复失败后仍允许默认种族皮肤回退
- Spawn 恢复成功后仍立即执行渲染刷新
- Load 路径仍允许 registry fallback
- Load 路径恢复成功后仍归一化 `activeSkinDefName`
- Load 路径仍不执行默认种族皮肤回退
- Load 恢复成功后仍不立即调用 `RequestRenderRefresh()`

### 允许变化行为
- 恢复结果策略从“两条 helper 各自展开条件分支”变为“共享策略 helper + 显式参数差异”
- Spawn / Load 的恢复差异从分散逻辑转为集中表达
- 后续若继续抽象恢复策略模型，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明恢复结果策略 helper 收束未破坏编译链路，且 Spawn / Load 的生命周期差异已经更显式地集中到单一边界

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的生命周期恢复结果组织方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部恢复策略的承载位置
- 属于低风险的“恢复结果策略收束 + 生命周期差异显式化”重构

### 当前风险观察
- `TryRecoverSkin(...)` 当前仍然采用 4 个布尔参数表达策略差异，说明恢复结果边界虽然已经集中，但参数语义仍偏实现细节
- Load 路径显式设置 `treatExistingActiveSkinAsSuccess: false`，后续若读档阶段出现新的预加载状态语义，仍需谨慎维护这条差异
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、读档恢复、默认种族皮肤回退与刷新协作在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `TryRecoverSkin(...)` 的 4 个布尔参数继续收敛为更具语义的恢复策略对象或具名策略入口
  2. 评估 Spawn / Load 两条恢复路径是否可以继续共用更明确的结果模型，而不仅仅返回 `bool`
- 若继续保持低风险节奏，建议优先处理 **恢复策略参数语义收束**，因为当前最大噪音已经从流程重复转移为布尔参数表达本身
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 27：CompPawnSkin 恢复策略参数语义收束（第二十六轮）

### 重构步骤名称
- 将 `TryRecoverSkin(...)` 的 4 个布尔参数收敛为具名策略对象 `SkinRecoveryPolicy`

### 目标
- 去除 `SkinLifecycleRecoveryCoordinator` 中 `allowRegistryFallback`、`normalizeDefName`、`allowDefaultRaceFallback`、`treatExistingActiveSkinAsSuccess` 这组偏实现细节的布尔参数噪音
- 让 Spawn / Load 两条恢复路径通过具名策略对象显式表达生命周期差异
- 保持既有恢复顺序与刷新语义不变，避免策略语义提升时引入行为漂移

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`
- `Source/CharacterStudio/UI/Dialog_AbilityEditor.Panels.cs`

### 本轮改动点
- 在 `SkinLifecycleRecoveryCoordinator` 中新增内部私有 `readonly struct`：
  - `SkinRecoveryPolicy`
- 在该策略对象中集中承载以下差异字段：
  - `allowRegistryFallback`
  - `normalizeDefName`
  - `allowDefaultRaceFallback`
  - `treatExistingActiveSkinAsSuccess`
- 新增具名静态策略：
  - `LoadRecoveryPolicy`
  - `SpawnRecoveryPolicy`
- 将 `TryRestoreSkinAfterLoad(...)` 收束为：
  - `TryRecoverSkin(owner, LoadRecoveryPolicy)`
- 将 `TryEnsureSkinAvailableAfterSpawn(...)` 收束为：
  - `TryRecoverSkin(owner, SpawnRecoveryPolicy)`
- 将 `TryRecoverSkin(...)` 的签名从 4 个布尔参数改为：
  - `TryRecoverSkin(CompPawnSkin owner, SkinRecoveryPolicy policy)`
- 保持 `ApplySpawnRecoveryRefresh(...)` / `ApplyLoadRecoveryRefresh(...)` 两条具名刷新入口继续不变
- 在本轮 build review 过程中，暴露出一个独立的编译阻塞：
  - `Source/CharacterStudio/UI/Dialog_AbilityEditor.Panels.cs(1705,1)` 等
  - 错误：`CS1519 / CS1002 / CS1022 / CS8803` 等大量 parser error
  - 根因：一段新加方法残留了补丁式前缀 `+`
- 对该阻塞做最小修复：
  - 仅移除 `GetRuntimeComponentTypeMenuLabel(...)` / `GetRuntimeComponentTypeDescription(...)` 这一段方法前的非法 `+` 文本污染
  - 不改变该段方法逻辑

### 预期不变行为
- Spawn 路径仍把“已有 `activeSkin`”视为恢复成功
- Spawn 路径仍不允许 registry fallback
- Spawn 路径仍不归一化 `activeSkinDefName`
- Spawn 路径在保存皮肤恢复失败后仍允许默认种族皮肤回退
- Spawn 恢复成功后仍立即执行渲染刷新
- Load 路径仍允许 registry fallback
- Load 路径恢复成功后仍归一化 `activeSkinDefName`
- Load 路径仍不执行默认种族皮肤回退
- Load 恢复成功后仍不立即调用 `RequestRenderRefresh()`
- Ability 编辑器运行时组件菜单文案逻辑不变

### 允许变化行为
- 恢复策略从“共享 helper + 4 个布尔参数”变为“共享 helper + 具名策略对象”
- Spawn / Load 生命周期差异在代码层的表达更集中、更可读
- `Dialog_AbilityEditor.Panels` 中编译阻塞段落的非法文本污染被清理，但方法行为保持不变

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 初次 build 时暴露出一个独立的编译阻塞：
  - `Source/CharacterStudio/UI/Dialog_AbilityEditor.Panels.cs(1705,1)` 等
  - 错误：`CS1519 / CS1002 / CS1022 / CS8803` 等大量 parser error
  - 根因：一段新加方法残留了补丁式前缀 `+`
- 对该段文本污染做最小修复后，重新构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` / `Dialog_AbilityEditor.Panels` 目标语义范围，其中 `Patch_PawnRenderTree.Injection.cs` 也不属于本轮修改模块，按当前执行规则忽略
- 说明恢复策略对象收束与最小 UI 编译修复均未破坏编译链路

### 影响范围评估
- 影响范围主要集中在 `CompPawnSkin` 的生命周期恢复策略表达方式
- 次要影响到 `Dialog_AbilityEditor` 运行时组件菜单文案方法所在段落的文本完整性
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部策略参数的承载方式
- 属于低风险的“恢复策略语义具名化 + 构建阻塞修复”重构

### 当前风险观察
- `SkinRecoveryPolicy` 当前仍然是 `CompPawnSkin` 内部私有 `readonly struct`，尚未成为更独立的文件级策略类型
- `TryRecoverSkin(...)` 目前仍只返回 `bool`，虽然参数语义已经显式化，但恢复结果本身仍未区分“已有皮肤”“恢复保存皮肤”“默认种族回退”等来源类型
- `Dialog_AbilityEditor.Panels.cs` 的编译阻塞根因属于文本污染而非业务逻辑，但也说明该文件后续修改时需要继续注意避免补丁残留
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、读档恢复、默认种族皮肤回退与 Ability 编辑器运行时组件菜单在真实运行时 100% 与之前一致
- 但从代码结构、修复范围与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `TryRecoverSkin(...)` 的 `bool` 返回值继续收敛为更明确的恢复结果模型，例如区分“已有皮肤 / 恢复保存皮肤 / 默认种族回退 / 恢复失败”
  2. 评估 Spawn / Load 两条恢复路径是否可以继续共用更具语义的刷新/结果组合边界，而不仅仅由“策略对象 + bool 结果”拼装
- 若继续保持低风险节奏，建议优先处理 **恢复结果模型语义收束**，因为当前参数语义已经清晰，下一步最自然的是继续提升恢复结果本身的表达能力
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 28：CompPawnSkin 恢复结果模型语义收束（第二十七轮）

### 重构步骤名称
- 将 `TryRecoverSkin(...)` 的 `bool` 返回值收敛为具名恢复结果枚举 `SkinRecoveryResult`

### 目标
- 去除 `SkinLifecycleRecoveryCoordinator` 中恢复结果只能表达“成功/失败”的过度压缩语义
- 让 Spawn / Load 两条恢复路径在内部能够区分“已有皮肤”“恢复保存皮肤”“默认种族回退”“恢复失败”这几类结果来源
- 保持对外刷新行为与生命周期语义不变，避免结果模型升级时引入行为漂移

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinLifecycleRecoveryCoordinator` 中新增内部私有枚举：
  - `SkinRecoveryResult`
- 在该结果模型中显式区分以下恢复来源：
  - `NotRecovered`
  - `ExistingActiveSkin`
  - `RestoredSavedSkin`
  - `AppliedDefaultRaceSkin`
- 将以下入口的返回值从 `bool` 改为：
  - `TryRestoreSkinAfterLoad(CompPawnSkin owner)` → `SkinRecoveryResult`
  - `TryEnsureSkinAvailableAfterSpawn(CompPawnSkin owner)` → `SkinRecoveryResult`
  - `TryRecoverSkin(CompPawnSkin owner, SkinRecoveryPolicy policy)` → `SkinRecoveryResult`
- 将 `RestoreAfterSpawn(...)` 收束为：
  - 先获取 `SkinRecoveryResult recoveryResult`
  - 再通过 `ApplySpawnRecoveryRefresh(owner, recoveryResult)` 处理刷新
- 将 `RestoreAfterLoad(...)` 收束为：
  - 先获取 `SkinRecoveryResult recoveryResult`
  - 再通过 `ApplyLoadRecoveryRefresh(recoveryResult)` 保持既有 no-op 刷新语义
- 新增辅助判断：
  - `HasRecoveredSkin(SkinRecoveryResult recoveryResult)`
- 在 `TryRecoverSkin(...)` 中统一固化以下结果分支：
  - 若 Spawn 路径允许把现有 `activeSkin` 视为成功，则返回 `ExistingActiveSkin`
  - 若 `TryRestoreSavedSkin(...)` 成功，则返回 `RestoredSavedSkin`
  - 若允许默认种族回退，且回退后已有 `activeSkin`，则返回 `AppliedDefaultRaceSkin`
  - 否则返回 `NotRecovered`

### 预期不变行为
- Spawn 路径仍把“已有 `activeSkin`”视为恢复成功
- Spawn 路径仍不允许 registry fallback
- Spawn 路径仍不归一化 `activeSkinDefName`
- Spawn 路径在保存皮肤恢复失败后仍允许默认种族皮肤回退
- Spawn 恢复成功后仍立即执行渲染刷新
- Load 路径仍允许 registry fallback
- Load 路径恢复成功后仍归一化 `activeSkinDefName`
- Load 路径仍不执行默认种族皮肤回退
- Load 恢复成功后仍不立即调用 `RequestRenderRefresh()`

### 允许变化行为
- 恢复结果从“共享 helper + bool”变为“共享 helper + 具名结果枚举”
- Spawn / Load 生命周期内部可以显式区分恢复来源，而不是只保留成功/失败语义
- 刷新入口从读取 `bool` 改为读取 `SkinRecoveryResult`，但外部行为保持不变

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明恢复结果模型语义收束未破坏编译链路，且恢复来源语义已经比 `bool` 更明确

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的生命周期恢复结果表达方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部恢复结果的承载方式
- 属于低风险的“恢复结果具名化 + 生命周期来源显式化”重构

### 当前风险观察
- `ApplySpawnRecoveryRefresh(...)` / `ApplyLoadRecoveryRefresh(...)` 当前仍然通过 `HasRecoveredSkin(...)` 折叠结果模型，说明刷新策略尚未真正利用“已有皮肤 / 恢复保存皮肤 / 默认回退”这些更细粒度结果
- `AppliedDefaultRaceSkin` 结果目前仍通过 `TryApplyDefaultRaceSkin(...)` 后检查 `owner.activeSkin != null` 推断，尚未形成更直接的默认回退结果契约
- `SkinRecoveryResult` 当前仍然是 `CompPawnSkin` 内部私有枚举，尚未成为更独立的文件级结果类型
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、读档恢复、默认种族皮肤回退与刷新协作在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 让 `ApplySpawnRecoveryRefresh(...)` / `ApplyLoadRecoveryRefresh(...)` 更显式地消费 `SkinRecoveryResult`，评估是否需要把“恢复来源”和“刷新策略”进一步组合成更清晰的生命周期恢复结果契约
  2. 评估 `TryApplyDefaultRaceSkin(...)` 是否可以返回更明确的默认回退结果，而不是由 `owner.activeSkin != null` 反推 `AppliedDefaultRaceSkin`
- 若继续保持低风险节奏，建议优先处理 **默认种族回退结果契约收束**，因为当前结果模型已经建立，下一步最自然的是减少 `AppliedDefaultRaceSkin` 对外部状态反推的依赖
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 29：CompPawnSkin 默认种族回退结果契约收束（第二十八轮）

### 重构步骤名称
- 将 `TryApplyDefaultRaceSkin(...)` 的成功语义从“外部通过 `owner.activeSkin != null` 反推”收束为“方法自身显式返回 `bool`”

### 目标
- 去除 `SkinLifecycleRecoveryCoordinator` 中 `AppliedDefaultRaceSkin` 对外部状态反推的隐式依赖
- 让默认种族皮肤回退是否成功应用由 `TryApplyDefaultRaceSkin(...)` 自身直接表达
- 保持 Spawn / Load 恢复顺序与刷新语义不变，避免在结果契约收束时引入行为漂移

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 将 `TryApplyDefaultRaceSkin(CompPawnSkin owner)` 的签名从 `void` 改为 `bool`
- 在该入口中显式固化以下返回契约：
  - 若 `ResolveDefaultRaceSkinToApply(owner)` 返回 `null`，则返回 `false`
  - 若成功解析默认种族皮肤，则调用 `SkinApplicationCoordinator.ApplyDefaultBound(owner, defaultSkin)` 后返回 `true`
- 将 `TryRecoverSkin(...)` 中默认种族回退分支从：
  - 先调用 `TryApplyDefaultRaceSkin(owner)`
  - 再通过 `owner.activeSkin != null` 反推是否应返回 `AppliedDefaultRaceSkin`
  收束为：
  - `if (policy.allowDefaultRaceFallback && TryApplyDefaultRaceSkin(owner)) return SkinRecoveryResult.AppliedDefaultRaceSkin;`
- 保持 `ResolveDefaultRaceSkinToApply(...)`、`SkinApplicationCoordinator.ApplyDefaultBound(...)` 与现有恢复策略对象继续不变

### 预期不变行为
- Spawn 路径在保存皮肤恢复失败后仍允许默认种族皮肤回退
- Load 路径仍不执行默认种族皮肤回退
- 默认种族皮肤仍只在 Humanlike Pawn 且当前无活动皮肤时应用
- 默认皮肤应用前仍通过 `ResolveDefaultRaceSkinToApply(...)` 获取 clone 后实例
- 默认皮肤应用后的来源标记、face runtime dirty 与延迟刷新语义保持兼容

### 允许变化行为
- `AppliedDefaultRaceSkin` 的判定从“外部字段反推”变为“默认回退入口显式返回值”
- 默认种族回退结果契约更集中、更可读
- 后续若继续扩展恢复结果模型，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明默认种族回退结果契约收束未破坏编译链路，且 `AppliedDefaultRaceSkin` 已不再依赖 `owner.activeSkin != null` 的外部反推

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的默认种族皮肤回退结果表达方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部默认回退成功判定的承载方式
- 属于低风险的“默认回退结果显式化 + 结果契约收束”重构

### 当前风险观察
- `ApplySpawnRecoveryRefresh(...)` / `ApplyLoadRecoveryRefresh(...)` 目前仍通过 `HasRecoveredSkin(...)` 折叠结果模型，说明刷新策略尚未真正按 `ExistingActiveSkin / RestoredSavedSkin / AppliedDefaultRaceSkin` 这些来源做更显式消费
- `TryApplyDefaultRaceSkin(...)` 虽已返回 `bool`，但默认回退结果仍只区分成功/失败，尚未上升为更细粒度的默认回退结果类型
- `SkinRecoveryResult` 与默认回退契约目前仍是 `CompPawnSkin` 内部私有结构，尚未成为更独立的文件级结果模型
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、默认种族皮肤回退与刷新协作在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 让 `ApplySpawnRecoveryRefresh(...)` / `ApplyLoadRecoveryRefresh(...)` 更显式地消费 `SkinRecoveryResult`，减少对 `HasRecoveredSkin(...)` 这种折叠 helper 的依赖
  2. 评估是否需要把“恢复来源”和“刷新策略”进一步组合成更清晰的生命周期恢复结果契约
- 若继续保持低风险节奏，建议优先处理 **恢复结果刷新消费语义收束**，因为默认种族回退结果契约已经显式化，下一步最自然的是让刷新入口也更直接地消费结果模型
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 30：CompPawnSkin 恢复结果刷新消费语义收束（第二十九轮）

### 重构步骤名称
- 让 `ApplySpawnRecoveryRefresh(...)` / `ApplyLoadRecoveryRefresh(...)` 直接显式消费 `SkinRecoveryResult`

### 目标
- 去除 `SkinLifecycleRecoveryCoordinator` 中对 `HasRecoveredSkin(...)` 折叠 helper 的依赖
- 让 Spawn / Load 两条生命周期刷新入口直接表达对 `SkinRecoveryResult` 的消费语义
- 保持既有刷新行为不变，避免在刷新消费收束时引入行为漂移

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 将 `ApplySpawnRecoveryRefresh(...)` 从：
  - 通过 `HasRecoveredSkin(recoveryResult)` 判断是否刷新
  收束为：
  - 直接 `switch` 消费 `SkinRecoveryResult`
  - `ExistingActiveSkin / RestoredSavedSkin / AppliedDefaultRaceSkin` 时执行 `RequestRenderRefresh()`
  - `NotRecovered` 时直接返回
- 将 `ApplyLoadRecoveryRefresh(...)` 从：
  - 通过 `HasRecoveredSkin(recoveryResult)` 判断是否进入 no-op 逻辑
  收束为：
  - 直接 `switch` 消费 `SkinRecoveryResult`
  - 对三类恢复成功结果显式保留“读档恢复后不立即刷新”的既有 no-op 语义
  - `NotRecovered` 时直接返回
- 删除已不再需要的辅助方法：
  - `HasRecoveredSkin(SkinRecoveryResult recoveryResult)`

### 预期不变行为
- Spawn 路径在 `ExistingActiveSkin`、`RestoredSavedSkin`、`AppliedDefaultRaceSkin` 时仍立即执行渲染刷新
- Load 路径在恢复成功时仍不立即调用 `RequestRenderRefresh()`
- `NotRecovered` 时两条路径仍不触发额外刷新
- `SkinRecoveryResult` 的来源判定语义保持不变

### 允许变化行为
- 刷新入口从“先把结果折叠为 `bool` 再决策”变为“直接按结果模型决策”
- Spawn / Load 的刷新消费边界更显式，阅读代码时不再需要反向推断 `HasRecoveredSkin(...)` 的折叠语义
- 后续若继续组合“恢复来源 + 刷新策略”，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 error / 4 warning**
- `CharacterStudio` 已成功输出到 `1.6\Assemblies\CharacterStudio.dll`
- 本轮 build 中的 4 个 warning 仍位于 `Patch_PawnRenderTree.Injection.cs`：
  - `Patch_PawnRenderTree.Injection.cs(440,86)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(468,71)`：`CS8604`
  - `Patch_PawnRenderTree.Injection.cs(542,13)`：`CS1717`
  - `Patch_PawnRenderTree.Injection.cs(544,13)`：`CS1717`
- 这些 warning 不属于本轮修改的 `CompPawnSkin` 模块，按当前执行规则忽略
- 说明恢复结果刷新消费语义收束未破坏编译链路，且 Spawn / Load 刷新入口已经开始直接消费 `SkinRecoveryResult`

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的生命周期恢复刷新决策表达方式
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部刷新入口对恢复结果模型的消费方式
- 属于低风险的“刷新消费显式化 + 结果模型直连”重构

### 当前风险观察
- `ApplySpawnRecoveryRefresh(...)` / `ApplyLoadRecoveryRefresh(...)` 虽已直接消费 `SkinRecoveryResult`，但当前仍存在重复的 case 列表，说明 Spawn / Load 刷新策略虽然更显式，但尚未沉淀为更统一的生命周期结果契约
- Load 路径当前仍对三类恢复成功结果统一采取 no-op，说明“恢复来源”和“刷新策略”仍是分层但未组合的两个概念
- 尚未做游戏内手工验证，因此不能完全确认出生后恢复、读档恢复与刷新协作在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 评估是否把“恢复来源”和“刷新策略”进一步组合成更清晰的生命周期恢复结果契约
  2. 评估 Spawn / Load 刷新入口中的重复 case 是否适合继续收敛为更具语义的策略 helper 或结果对象
- 若继续保持低风险节奏，建议优先处理 **生命周期恢复结果契约收束**，因为刷新入口已经开始直接消费结果模型，下一步最自然的是进一步统一“恢复来源”和“刷新策略”的协作边界
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 31：CompPawnSkin 生命周期恢复结果契约收束（第三十轮）

### 重构步骤名称
- 将“恢复来源”和“刷新策略”组合为更明确的生命周期恢复结果对象 `SkinRecoveryOutcome`

### 目标
- 继续减少 `SkinLifecycleRecoveryCoordinator` 中“恢复来源”和“刷新策略”分散表达的噪音
- 让 Spawn / Load 生命周期入口不再直接消费裸 `SkinRecoveryResult`，而是消费包含刷新指令的结果对象
- 保持 `TryRecoverSkin(...)` 的核心恢复流程不变，仅收束结果消费边界，避免扩大改动范围

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinLifecycleRecoveryCoordinator` 中新增恢复结果刷新指令枚举：
  - `SkinRecoveryRefreshDirective`
  - `None`
  - `ImmediateRefresh`
- 在 `SkinLifecycleRecoveryCoordinator` 中新增结果对象：
  - `SkinRecoveryOutcome`
  - 集中承载：
    - `SkinRecoveryResult result`
    - `SkinRecoveryRefreshDirective refreshDirective`
- 将以下入口的返回值从 `SkinRecoveryResult` 改为：
  - `TryRestoreSkinAfterLoad(CompPawnSkin owner)` → `SkinRecoveryOutcome`
  - `TryEnsureSkinAvailableAfterSpawn(CompPawnSkin owner)` → `SkinRecoveryOutcome`
- 新增结果映射入口：
  - `CreateSpawnRecoveryOutcome(SkinRecoveryResult recoveryResult)`
  - `CreateLoadRecoveryOutcome(SkinRecoveryResult recoveryResult)`
- 新增统一消费入口：
  - `ApplyRecoveryOutcome(CompPawnSkin owner, SkinRecoveryOutcome recoveryOutcome)`
- 将 `RestoreAfterSpawn(...)` / `RestoreAfterLoad(...)` 收束为：
  - 先获取 `SkinRecoveryOutcome`
  - 再统一通过 `ApplyRecoveryOutcome(...)` 消费刷新指令
- 保持以下行为继续不变：
  - Spawn 恢复成功结果映射为 `ImmediateRefresh`
  - Load 恢复结果统一映射为 `None`
  - `TryRecoverSkin(...)` 仍继续只负责恢复来源判定，不直接承载刷新行为

### 预期不变行为
- Spawn 路径恢复成功后仍立即执行渲染刷新
- Load 路径恢复成功后仍不立即调用 `RequestRenderRefresh()`
- `SkinRecoveryResult` 的来源判定语义继续保持：
  - `ExistingActiveSkin`
  - `RestoredSavedSkin`
  - `AppliedDefaultRaceSkin`
  - `NotRecovered`
- 默认种族皮肤来源标记、face runtime dirty 与延迟刷新语义保持兼容

### 允许变化行为
- 生命周期入口从“直接消费恢复来源结果”变为“消费恢复结果对象 + 刷新指令”
- Spawn / Load 的刷新策略表达更集中、更显式
- 后续若继续扩展恢复结果契约，迁移成本更低

### 本轮 review 方式
- 原计划执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮代码改动已完成，目标是把恢复来源与刷新策略组合为更清晰的结果契约
- 原始阶段的 build 曾被 `Dialog_AbilityEditor` 并行拆分中的重复定义错误污染，因此当时未形成有效回归结论
- 在 `Dialog_AbilityEditor` 拆分完成后，重新执行统一 build 验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`
- 补充验证结果为 **0 warning / 0 error**
- 说明 Step 31 的 `CompPawnSkin` 生命周期恢复结果契约收束已经通过正式编译回归验证

### 影响范围评估
- 代码改动范围仍集中在 `CompPawnSkin` 的生命周期恢复结果消费边界
- 当前已确认外部并行拆分不再阻断本轮模块的编译回归
- 本轮状态可正式视为“代码完成 + build 验证通过”

### 当前风险观察
- 当前已获得一次可采信的独立 build 结果
- `Dialog_AbilityEditor` 的并行拆分错误不再影响本轮回归结论
- 剩余风险主要在于尚未做游戏内手工验证，而非编译链路

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 将 Step 32 ~ Step 33 一并回填为正式通过结论
- 如继续推进 `CompPawnSkin` 子线，可进入后续小步收束或转向其他生命周期/刷新边界
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 32：CompPawnSkin 恢复策略刷新指令内聚（第三十一轮）

### 重构步骤名称
- 将“恢复成功后的刷新指令”从 Spawn / Load outcome 映射入口收束到 `SkinRecoveryPolicy.successRefreshDirective`

### 目标
- 继续减少 `SkinLifecycleRecoveryCoordinator` 中 Spawn / Load 两套 outcome 映射 helper 的重复噪音
- 让恢复策略对象同时承载“恢复差异”和“成功后的刷新指令”
- 保持 `NotRecovered` 始终映射为 `SkinRecoveryRefreshDirective.None`，避免策略提升时引入恢复失败语义漂移

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinRecoveryPolicy` 中新增字段：
  - `successRefreshDirective`
- 将具名策略显式扩展为：
  - `LoadRecoveryPolicy(..., successRefreshDirective: SkinRecoveryRefreshDirective.None)`
  - `SpawnRecoveryPolicy(..., successRefreshDirective: SkinRecoveryRefreshDirective.ImmediateRefresh)`
- 删除原本按生命周期分别构造 outcome 的两套 helper：
  - `CreateSpawnRecoveryOutcome(...)`
  - `CreateLoadRecoveryOutcome(...)`
- 新增共享 outcome 工厂：
  - `CreateRecoveryOutcome(SkinRecoveryResult recoveryResult, SkinRecoveryRefreshDirective recoveredRefreshDirective)`
- 让 outcome 映射入口开始统一经由共享工厂构造，而不是分别由 Spawn / Load 路径各自展开 case 分支

### 预期不变行为
- Spawn 路径恢复成功后仍立即执行渲染刷新
- Load 路径恢复成功后仍不立即调用 `RequestRenderRefresh()`
- `NotRecovered` 时仍不触发额外即时刷新
- `SkinRecoveryResult` 的来源判定语义保持不变：
  - `ExistingActiveSkin`
  - `RestoredSavedSkin`
  - `AppliedDefaultRaceSkin`
  - `NotRecovered`

### 允许变化行为
- 生命周期恢复策略从“策略对象 + 生命周期专属 outcome 工厂”变为“策略对象 + 共享 outcome 工厂”
- Spawn / Load 的刷新指令差异在代码层的表达更集中
- 后续若继续收束恢复结果对象生成路径，迁移成本更低

### 本轮 review 方式
- 当前按并行拆分约束执行代码 review：
  - 暂不执行可采信 build
  - 继续回避 `Dialog_AbilityEditor*` 并行拆分文件

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- `CompPawnSkin` 中“恢复策略 -> 刷新指令”的表达边界已进一步集中到 `SkinRecoveryPolicy`
- 初始阶段由于 `Dialog_AbilityEditor` 并行文件拆分，Step 32 只记录为 review-only
- 在 `Dialog_AbilityEditor` 拆分完成后，重新执行统一 build 验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`
- 补充验证结果为 **0 warning / 0 error**
- 说明 Step 32 的刷新指令策略内聚未破坏编译链路，现可回填为正式通过结论

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的生命周期恢复策略与 outcome 映射边界
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部刷新指令的承载方式
- 属于低风险的“刷新指令策略内聚”重构

### 当前风险观察
- 当前已获得一次可采信的独立 build 结果
- 当前剩余风险主要在于尚未做游戏内手工验证，而非编译链路
- `TryRecoverSkin(...)` 在本轮后的结构仍有继续收束空间，但不影响本轮验证通过

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续收束 `CompPawnSkin` 中恢复结果对象的生成路径
- 优先让 `TryRecoverSkin(...)` 直接返回 `SkinRecoveryOutcome`，进一步减少“先产出来源结果、再包装 outcome”的中间层
- 当前可将 Step 32 视为正式 build 通过后的稳定基线

---

## Step 33：CompPawnSkin 恢复结果对象生成路径直连（第三十二轮）

### 重构步骤名称
- 让 `TryRecoverSkin(...)` 直接返回 `SkinRecoveryOutcome`，把恢复来源与刷新指令的组合收束到单一恢复入口

### 目标
- 继续减少 `SkinLifecycleRecoveryCoordinator` 中“先产出 `SkinRecoveryResult`，再包装为 `SkinRecoveryOutcome`”的中间层噪音
- 让 Spawn / Load 两条路径通过策略对象直接获得最终 outcome
- 保持恢复顺序、默认种族回退语义与刷新行为不变，避免在返回契约升级时引入行为漂移

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 将以下入口的返回值进一步收束为 `SkinRecoveryOutcome`：
  - `TryRecoverSkin(CompPawnSkin owner, SkinRecoveryPolicy policy)`
- 将 `TryRestoreSkinAfterLoad(...)` / `TryEnsureSkinAvailableAfterSpawn(...)` 收束为对：
  - `TryRecoverSkin(owner, LoadRecoveryPolicy)`
  - `TryRecoverSkin(owner, SpawnRecoveryPolicy)`
  的薄封装
- 在 `TryRecoverSkin(...)` 内部直接完成以下 outcome 生成：
  - `ExistingActiveSkin`
  - `RestoredSavedSkin`
  - `AppliedDefaultRaceSkin`
  - `NotRecovered`
- 让 `CreateRecoveryOutcome(...)` 成为统一的结果对象构造入口，Spawn / Load 生命周期只再负责选择策略，不再负责二次包装结果

### 预期不变行为
- Spawn 路径仍把“已有 `activeSkin`”视为恢复成功
- Spawn 路径仍不允许 registry fallback，且保存皮肤恢复失败后仍允许默认种族皮肤回退
- Spawn 恢复成功后仍立即执行渲染刷新
- Load 路径仍允许 registry fallback，恢复成功后仍归一化 `activeSkinDefName`
- Load 路径恢复成功后仍不立即调用 `RequestRenderRefresh()`
- 默认种族皮肤来源标记、face runtime dirty 与延迟刷新语义保持兼容

### 允许变化行为
- 生命周期恢复结果从“策略对象 -> 来源结果 -> outcome”变为“策略对象 -> outcome”
- `SkinLifecycleRecoveryCoordinator` 中恢复入口更偏向高层编排，结果对象生成路径更短
- 后续若继续扩展恢复结果契约，迁移成本更低

### 本轮 review 方式
- 当前按并行拆分约束执行代码 review：
  - 暂不执行可采信 build
  - 不处理 `Dialog_AbilityEditor*` 并行拆分冲突

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- `TryRecoverSkin(...)` 已直接返回 `SkinRecoveryOutcome`，恢复来源与刷新指令的组合边界进一步下沉到单一恢复入口
- `TryRestoreSkinAfterLoad(...)` / `TryEnsureSkinAvailableAfterSpawn(...)` 已进一步瘦身为策略选择层，`RestoreAfterSpawn(...)` / `RestoreAfterLoad(...)` 继续只负责高层消费 outcome
- 初始阶段由于 `Dialog_AbilityEditor` 并行文件拆分，Step 33 只形成 review-only 结论
- 在 `Dialog_AbilityEditor` 拆分完成后，重新执行统一 build 验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`
- 补充验证结果为 **0 warning / 0 error**
- 说明 Step 33 的结果对象生成路径直连未破坏编译链路，现可回填为正式通过结论

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的生命周期恢复结果对象生成路径
- 对外消费 API 未变化，主要影响 `SkinLifecycleRecoveryCoordinator` 内部恢复结果契约的承载位置
- 属于低风险的“结果对象生成路径直连”重构

### 当前风险观察
- 当前已获得一次可采信的独立 build 结果
- `SkinRecoveryOutcome.result` 当前仍主要承载结构化语义，`ApplyRecoveryOutcome(...)` 依旧只消费 `refreshDirective`，这是后续优化点而非当前构建风险
- 剩余风险主要在于尚未做游戏内手工验证，而非编译链路

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 当前可将 Step 31 ~ Step 33 统一视为已通过正式 build 回归验证
- 下一轮可继续评估 `SkinRecoveryOutcome` 在消费层的利用价值，或转向 `CompPawnSkin` 其他未收束的生命周期/刷新边界
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 34：CompPawnSkin 皮肤应用指令契约收束（第三十三轮）

### 重构步骤名称
- 将 `SkinApplicationCoordinator` 中皮肤应用/静默写入/默认绑定/清除逻辑收敛为具名指令对象 `SkinApplicationDirective`

### 目标
- 去除 `SkinApplicationCoordinator.Apply(...)` 中 `fromDefaultRaceBinding / syncXenotype / immediateRefresh / deferredRefresh` 这组偏实现细节的参数噪音
- 让“手动应用”“默认绑定应用”“静默写入”“恢复写回”“清除皮肤”这几类入口通过具名指令显式表达差异
- 保持现有皮肤应用顺序、来源标记与刷新行为不变，避免在契约收束时引入行为漂移

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinApplicationCoordinator` 中新增内部私有枚举：
  - `SkinApplicationRefreshDirective`
  - `None`
  - `MarkDirtyOnly`
  - `DeferredRefresh`
  - `ImmediateRefresh`
  - `ImmediateAndDeferredRefresh`
- 在 `SkinApplicationCoordinator` 中新增内部私有只读结构：
  - `SkinApplicationDirective`
  - 集中承载：
    - `fromDefaultRaceBinding`
    - `syncXenotype`
    - `refreshDirective`
- 新增具名静态指令：
  - `ManualApplicationDirective`
  - `DefaultBoundApplicationDirective`
  - `ClearApplicationDirective`
- 将以下入口改为通过具名指令驱动：
  - `ApplyManual(...)`
  - `SetWithSourceSilently(...)`
  - `RestoreResolved(...)`
  - `ApplyDefaultBound(...)`
  - `Clear(...)`
- 新增局部指令工厂：
  - `CreateSilentApplicationDirective(...)`
  - `CreateRestoreApplicationDirective(...)`
- 将原本带多布尔参数的：
  - `Apply(CompPawnSkin owner, PawnSkinDef? skin, bool fromDefaultRaceBinding, bool syncXenotype, bool immediateRefresh, bool deferredRefresh)`
  收束为：
  - `Apply(CompPawnSkin owner, PawnSkinDef? skin, SkinApplicationDirective directive)`
- 将刷新路径统一收敛到：
  - `ApplyRefreshDirective(...)`

### 预期不变行为
- `ActiveSkin` 手动赋值时仍会同步 Xenotype，并触发即时刷新 + 延迟刷新
- `SetActiveSkinSilent(...)` 与 `SetActiveSkinWithSource(...)` 仍保持“仅写入 + 标记 dirty，不立即刷新”的既有语义
- `RestoreResolved(...)` 仍保持“仅恢复写回，不附带刷新”的既有语义
- 默认种族皮肤应用仍保持默认绑定来源语义，并只触发延迟刷新
- `ClearSkin()` 仍会清空当前皮肤，并执行即时刷新
- `activeSkin / activeSkinDefName / activeSkinFromDefaultRaceBinding` 的写入语义保持兼容

### 允许变化行为
- 皮肤应用路径从“共享方法 + 多布尔参数”变为“共享方法 + 具名指令对象”
- 刷新策略从分散的布尔组合变为集中枚举表达
- 后续若继续扩展皮肤应用结果模型或刷新协作边界，迁移成本更低

### 本轮 review 方式
- 执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [x] 通过
- [ ] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 构建通过，结果为 **0 warning / 0 error**
- `CharacterStudio` 已成功输出到：
  - `D:\rim mod\CharacterStudio\1.6\Assemblies\CharacterStudio.dll`
- 本轮改动未触及技能编辑器模块，符合“跳过技能编辑器，转向其他模块重构”的当前执行方向
- 说明 `CompPawnSkin` 中皮肤应用入口的指令契约收束未破坏编译链路，且工程当前保持零警告状态

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的皮肤写入入口与刷新协作表达方式
- 对外消费 API 未变化，主要影响 `SkinApplicationCoordinator` 内部契约的承载方式
- 属于低风险的“应用指令具名化 + 刷新语义集中表达”重构

### 当前风险观察
- `SkinApplicationDirective` 与 `SkinApplicationRefreshDirective` 当前仍然是 `CompPawnSkin` 内部私有结构，尚未成为更独立的文件级契约类型
- `RestoreResolved(...)` 当前通过 `SkinApplicationRefreshDirective.None` 显式表达“只写入不刷新”，`SetWithSourceSilently(...)` 通过 `MarkDirtyOnly` 表达“写入并标记 dirty”；这两者的差异已经更清晰，但仍建议后续结合运行时应用链路继续验证
- 尚未做游戏内手工验证，因此不能完全确认默认绑定转手动应用、静默写入、恢复写回与即时/延迟刷新协作在真实运行时 100% 与之前一致
- 但从代码结构与构建结果判断，本轮可以视为安全推进

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续拆 `CompPawnSkin`，优先考虑：
  1. 将 `SkinLifecycleRecoveryCoordinator` 与 `SkinApplicationCoordinator` 的结果/指令边界继续收束为更对称的生命周期写入契约
  2. 评估 `RestoreResolved(...)`、`SetWithSourceSilently(...)`、`ApplyDefaultBound(...)` 三类写入路径是否还能进一步统一“写入结果 + 刷新策略”的表达层
- 若继续保持低风险节奏，建议优先处理 **皮肤写入结果契约收束**，因为当前恢复侧已有 `SkinRecoveryOutcome`，应用侧也已有 `SkinApplicationDirective`，下一步最自然的是继续提升两者之间的协作对称性
- 继续坚持“改一小步 -> build -> review 记录”节奏

---

## Step 35：CompPawnSkin 皮肤写入结果契约收束（第三十四轮）

### 重构步骤名称
- 将 `SkinApplicationCoordinator` 中皮肤写入是否真正发生变化的语义收敛为具名结果对象 `SkinApplicationWriteResult`

### 目标
- 去除 `Apply(...)` 对“写入后总是继续同步与刷新”的隐式假设
- 让 `SetSkin(...)` 显式返回“皮肤定义是否变化 / 来源标记是否变化”的写入结果
- 避免无实际变化时仍继续执行 `SyncXenotype(...)` 与刷新指令
- 让应用侧契约与恢复侧 `SkinRecoveryOutcome` 保持更对称的结果表达

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 在 `SkinApplicationCoordinator` 中新增内部私有只读结构：
  - `SkinApplicationWriteResult`
  - 集中承载：
    - `skinChanged`
    - `sourceChanged`
    - `HasChanged`
- 将 `SetSkin(...)` 从 `void` 收束为返回 `SkinApplicationWriteResult`
- 在 `SetSkin(...)` 中显式区分：
  - `activeSkin / activeSkinDefName` 是否变化
  - `activeSkinFromDefaultRaceBinding` 是否变化
- 将 `Apply(...)` 收束为：
  - 先调用 `SetSkin(...)`
  - 若 `!writeResult.HasChanged` 则直接返回
  - 仅在写入确实发生变化时才继续：
    - `SyncXenotype(skin)`
    - `ApplyRefreshDirective(...)`
- 保持现有指令对象与刷新指令体系继续不变：
  - `SkinApplicationDirective`
  - `SkinApplicationRefreshDirective`

### 预期不变行为
- 手动应用新皮肤时仍会同步 Xenotype，并执行既有的即时/延迟刷新策略
- 默认绑定应用、恢复写回、静默写入、清除皮肤的来源标记与刷新语义保持兼容
- `activeSkin / activeSkinDefName / activeSkinFromDefaultRaceBinding` 的最终写入结果保持不变

### 允许变化行为
- 当皮肤对象、defName、来源标记都未发生变化时，不再继续执行多余的 `SyncXenotype(...)` 与刷新指令
- 应用侧从“写入后无条件继续副作用”变为“写入结果驱动后续副作用”
- 后续若继续扩展皮肤应用结果模型，迁移成本更低

### 本轮 review 方式
- 尝试执行构建验证：
  - `dotnet build "..\CharacterStudio\Source\CharacterStudio\CharacterStudio.csproj" --no-restore -nologo -v minimal`

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 已尝试执行 build 验证，但当前构建被以下非本轮修改模块错误阻断：
  - `Source/CharacterStudio/UI/Dialog_AbilityEditor.Panels.Vfx.cs(77,47)`：`CS0206`
- 错误内容为：
  - 非引用返回属性或索引器不能用作 `out` 或 `ref` 值
- 该错误位于技能编辑器 VFX 面板文件，不属于本轮修改的 `CompPawnSkin` 范围
- 按当前执行规则：
  - `跳过技能编辑器部分，进行其他模块重构`
  - `遇到非修改部分报错则跳过该步的 build 环节`
  因此本轮记为“有风险但可继续”
- 本轮 `CompPawnSkin` 改动本身只收束了皮肤写入结果契约，未扩展到技能编辑器模块

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的皮肤写入结果表达方式与后续副作用触发时机
- 对外消费 API 未变化，主要影响 `SkinApplicationCoordinator` 内部“写入结果 -> Xenotype 同步 / 刷新指令”的承载方式
- 属于低风险的“写入结果显式化 + 无变化短路”重构

### 当前风险观察
- 当前未获得一次仅针对本轮 `CompPawnSkin` 改动的可采信 build 结果，因为统一构建被技能编辑器模块的独立错误阻断
- `SkinApplicationWriteResult` 当前仍然是 `CompPawnSkin` 内部私有结构，尚未成为更独立的文件级结果类型
- 尚未做游戏内手工验证，因此不能完全确认默认绑定转手动应用、静默写入、恢复写回与无变化短路在真实运行时 100% 与之前一致
- 但从改动边界与失败来源判断，本轮风险主要来自外部模块编译阻断，而非当前修改本身

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 在继续 `CompPawnSkin` 收束前，优先等待或绕过技能编辑器模块当前的 `Dialog_AbilityEditor.Panels.Vfx.cs` 编译阻断
- 若后续获得可采信构建窗口，优先补做一次统一 build，验证 Step 35 在完整工程上的编译回归
- 在 `CompPawnSkin` 子线内部，可继续评估 `SkinApplicationWriteResult` 与 `SkinRecoveryOutcome` 是否还能进一步形成更对称的生命周期/应用结果契约
- 继续坚持“改一小步 -> review 记录；遇到非当前模块阻断则按规则跳过 build”的节奏

---

## Step 36：CompPawnSkin 静默写入结果首层上浮（第三十五轮）

### 重构步骤名称
- 将 `SkinApplicationCoordinator.SetWithSourceSilently(...)` 与 `CompPawnSkin.SetActiveSkinWithSource(...)` 的返回值从“无结果”收束为“是否真实发生写入变化”的首层契约

### 目标
- 让 runtime apply 调用链首次能感知“这次静默写入是否真的改动了皮肤或来源标记”
- 为后续把无变化短路能力继续上浮到 `PawnSkinRuntimeUtility` 做准备
- 保持现有手动应用、默认绑定应用、恢复写回的写入语义不变

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 将 `SkinApplicationCoordinator.Apply(...)` 的返回契约从 `void` 提升为 `SkinApplicationWriteResult`
- 将 `SkinApplicationCoordinator.SetWithSourceSilently(...)` 从 `void` 改为返回 `bool`
  - 返回 `Apply(...).HasChanged`
- 将 `CompPawnSkin.SetActiveSkinWithSource(...)` 从 `void` 改为返回 `bool`
- 保持 `SetSkin(...)` 内部对：
  - `skinChanged`
  - `sourceChanged`
  的区分逻辑继续不变

### 预期不变行为
- 静默写入仍不直接触发 runtime apply 路径中的立即重建
- 手动应用、默认绑定应用、恢复写回的刷新/来源标记语义不变
- `activeSkin / activeSkinDefName / activeSkinFromDefaultRaceBinding` 的最终写入结果不变

### 允许变化行为
- 静默写入链路开始向外暴露“是否发生真实变化”的结果
- 无变化时，调用方可以开始具备短路冗余副作用的能力
- 当前仍只是首层上浮，runtime 调用方尚未消费该返回值

### 本轮 review 方式
- 按当前用户指令执行 review-only：
  - 暂停 build
  - 仅做调用链与副作用路径检查

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮未执行 build，原因是用户明确要求：
  - `继续进行，到40步，暂停build`
- 代码改动范围仅限 `CompPawnSkin` 内部静默写入结果首层上浮
- 当前返回值尚未被 runtime apply 侧消费，因此行为面仍接近原实现，风险较低
- 该步的主要价值是为后续无变化短路提供稳定契约起点

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的静默写入调用链返回契约
- 对外运行时 API 语义未扩展到 UI/渲染侧，主要影响内部协作边界
- 属于低风险的“结果首层上浮”重构

### 当前风险观察
- 当前未获得构建回归结果，仍属于 review-only 结论
- `bool` 只能表达“是否有变化”，尚不能区分“皮肤变化”和“仅来源变化”
- 后续若要精细短路副作用，仍需继续细化结果模型

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续把 runtime apply 链路接到这个返回值上
- 优先在 `PawnSkinRuntimeUtility.ApplySkinToPawn(...)` 中消除“无变化仍重建渲染树/重复授能”的路径
- 保持暂停 build、先做 review-only 的推进方式

---

## Step 37：PawnSkinRuntimeUtility 无变化短路（第三十六轮）

### 重构步骤名称
- 让 `PawnSkinRuntimeUtility.ApplySkinToPawn(...)` 在静默写入无变化时直接短路返回

### 目标
- 避免 runtime apply 路径在“皮肤与来源均未变化”时仍继续执行缓存清理、渲染树重建和技能授予
- 将 Step 36 上浮出来的“是否发生变化”结果真正接入外层运行时入口
- 保持 `ApplySkinToPawn(...)` 的外部 `bool` 成功返回语义不变

### 涉及文件
- `Source/CharacterStudio/Core/PawnSkinRuntimeUtility.cs`

### 本轮改动点
- 在 `ApplySkinToPawn(...)` 中接收：
  - `bool hasSkinStateChanged = comp.SetActiveSkinWithSource(preparedSkin, fromDefaultRaceBinding);`
- 新增无变化短路：
  - `if (!hasSkinStateChanged) return true;`
- 保持对外调用方仍通过 `bool` 判断“应用入口是否成功完成”

### 预期不变行为
- 皮肤真实变化时，仍会继续执行：
  - `ClearCache()`
  - `RefreshHiddenNodes(...)`
  - `ForceRebuildRenderTree(...)`
  - `GrantSkinAbilitiesToPawn(...)`
- 调用方仍只感知 `ApplySkinToPawn(...)` 是否成功执行，不需要同步修改

### 允许变化行为
- 当皮肤与来源均未变化时，不再继续执行重渲染与授能副作用
- runtime apply 路径的冗余副作用开始被显式短路
- 返回 `true` 现在更偏向“入口成功执行完毕”，而不再隐含“必然发生了状态变化”

### 本轮 review 方式
- 按当前用户指令执行 review-only：
  - 暂停 build
  - 仅做 runtime 调用链与副作用路径检查

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮未执行 build，原因是用户明确要求暂停 build
- 代码变更仅影响 `PawnSkinRuntimeUtility` 的 runtime apply 主路径
- 对外 `bool` 返回值保持不变，因此外部调用点无需同步修改
- 本轮已经消除“完全无变化仍重建渲染树”的第一层冗余副作用

### 影响范围评估
- 影响范围集中在 `PawnSkinRuntimeUtility.ApplySkinToPawn(...)` 的无变化短路路径
- 不改变 UI、编辑器或生命周期恢复路径的调用方式
- 属于低风险的“运行时副作用短路”重构

### 当前风险观察
- 仍未获得构建回归结果，当前结论基于代码 review
- 当前短路仍把“皮肤变化”和“来源变化”合并看作“有变化”，后续仍可继续细化
- 如果调用方未来需要区分来源变化与皮肤变化，当前 `bool` 结果仍不够表达

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 将静默写入结果继续细化为结构化结果，而不是 `bool`
- 优先把 `skinChanged` 与 `sourceChanged` 从 `CompPawnSkin` 内部继续上浮出来
- 为“仅来源变化时也避免渲染/授能副作用”做准备

---

## Step 38：CompPawnSkin 皮肤/来源写入结果细粒度上浮（第三十七轮）

### 重构步骤名称
- 将静默写入链路的返回契约从 `bool` 升级为结构化结果 `SkinApplicationWriteResult`

### 目标
- 让 runtime apply 调用方能够区分：
  - `skinChanged`
  - `sourceChanged`
- 继续提升应用侧结果表达能力，使其与恢复侧 `SkinRecoveryOutcome` 更对称
- 为下一步实现“仅来源变化时也不触发渲染/授能副作用”提供直接条件

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 将 `SkinApplicationWriteResult` 从 `SkinApplicationCoordinator` 内部私有结构提升为 `CompPawnSkin` 内部可复用结构
- 将 `SkinApplicationCoordinator.SetWithSourceSilently(...)` 从返回 `bool` 改为返回 `SkinApplicationWriteResult`
- 将 `CompPawnSkin.SetActiveSkinWithSource(...)` 从返回 `bool` 改为返回 `SkinApplicationWriteResult`
- 保持 `HasChanged` 聚合属性继续存在，兼容内部已有使用模式

### 预期不变行为
- 静默写入本身的写字段语义不变
- `skinChanged / sourceChanged` 的判定标准不变
- runtime apply 侧仍未改变最终 outward API，仅增强可消费的内部结果细节

### 允许变化行为
- 应用侧结果开始具备“皮肤变化 / 来源变化”细粒度区分能力
- 调用方不再只能通过 `HasChanged` 获得折叠语义
- 后续副作用短路条件可以按真实皮肤变化精细化

### 本轮 review 方式
- 按当前用户指令执行 review-only：
  - 暂停 build
  - 仅做内部契约与调用链检查

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮未执行 build，原因是用户明确要求暂停 build
- 代码改动只扩大了内部结果契约的表达力，没有直接扩展对外行为面
- `SkinApplicationWriteResult` 现在可以直接承载 `skinChanged / sourceChanged`，为后续 runtime 侧精细短路提供条件
- 该步本质上是契约增强，风险集中在调用方后续接入是否一致

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的静默写入返回契约
- 不影响恢复路径与手动应用路径的对外使用方式
- 属于低风险的“结果模型细粒度上浮”重构

### 当前风险观察
- 当前仍未执行 build
- `SkinApplicationWriteResult` 虽已提升到外层，但仍是 `CompPawnSkin` 内部结构，尚未成为更独立的文件级契约
- 后续调用方若继续只消费 `HasChanged`，则这一步的细粒度价值还未完全兑现

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 让 `PawnSkinRuntimeUtility.ApplySkinToPawn(...)` 改为直接消费 `writeResult.skinChanged`
- 在“仅来源标记变化”时避免渲染树重建、缓存清理与技能授予
- 保持外部 `ApplySkinToPawn(...)` 返回签名不变

---

## Step 39：PawnSkinRuntimeUtility 来源变化副作用短路（第三十八轮）

### 重构步骤名称
- 让 `PawnSkinRuntimeUtility.ApplySkinToPawn(...)` 直接按 `writeResult.skinChanged` 决定是否执行 runtime 副作用

### 目标
- 进一步消除“仅来源标记变化时”仍执行缓存清理、渲染树重建和技能授予的冗余副作用
- 真正把 Step 38 提供的细粒度结果模型接入 runtime apply 主路径
- 保持调用方继续通过 `bool` 获得成功/失败语义

### 涉及文件
- `Source/CharacterStudio/Core/PawnSkinRuntimeUtility.cs`

### 本轮改动点
- 将 `ApplySkinToPawn(...)` 中的接收逻辑改为：
  - `var writeResult = comp.SetActiveSkinWithSource(preparedSkin, fromDefaultRaceBinding);`
- 将短路条件从：
  - `if (!hasSkinStateChanged) return true;`
  收束为：
  - `if (!writeResult.skinChanged) return true;`
- 由此将“仅来源变化”与“完全无变化”统一纳入不做 runtime 重副作用的分支

### 预期不变行为
- 当皮肤定义真实变化时，仍继续执行：
  - `ClearCache()`
  - `RefreshHiddenNodes(...)`
  - `ForceRebuildRenderTree(...)`
  - `GrantSkinAbilitiesToPawn(...)`
- `ApplySkinToPawn(...)` 的 outward 返回签名与调用方语义不变

### 允许变化行为
- 仅来源标记变化时，不再触发渲染树重建、缓存清理与技能授予
- runtime apply 路径的副作用短路更加贴近真实皮肤变化，而不是折叠语义
- 运行时链路的无效工作量进一步下降

### 本轮 review 方式
- 按当前用户指令执行 review-only：
  - 暂停 build
  - 仅做 runtime 副作用链路检查

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮未执行 build，原因是用户明确要求暂停 build
- `ApplySkinToPawn(...)` 现在直接按 `skinChanged` 决定是否执行重副作用路径
- 这意味着“来源标记变了，但皮肤没变”的情况将不再产生不必要的渲染与授能操作
- 该步继续维持对外 API 稳定，主要收益集中在内部副作用精细化短路

### 影响范围评估
- 影响范围集中在 `PawnSkinRuntimeUtility` 的 runtime apply 副作用触发条件
- 不影响 UI/编辑器入口，不影响恢复策略与皮肤写入本身的字段语义
- 属于低风险的“source-only 副作用短路”重构

### 当前风险观察
- 仍未获得构建回归结果，当前结论来自代码 review
- `ApplySkinToPawn(...)` 当前把 `sourceChanged && !skinChanged` 视作无需重渲染；这一点与当前优化目标一致，但仍建议后续在实际运行时补验证
- 若未来出现依赖来源标记变化触发额外副作用的路径，需要再单独评估

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 收口 `CompPawnSkin` 中已经失去调用方的旧 helper API
- 优先清理 `SetActiveSkinSilent(...)`、`SetActiveSkinSource(...)` 及其底层未使用包装
- 继续缩小 `CompPawnSkin` 的内部 API 表面

---

## Step 40：CompPawnSkin 未使用皮肤应用 helper API 收口（第三十九轮）

### 重构步骤名称
- 删除 `CompPawnSkin` 与 `SkinApplicationCoordinator` 中已无调用方的旧 helper API，保留 `SetActiveSkinWithSource(...)` 作为当前 runtime apply 入口

### 目标
- 缩小 `CompPawnSkin` 的内部 API 表面
- 去除已经被新调用链替代、且无任何外部调用方的中间 helper
- 让当前真实使用的 runtime apply 路径更加聚焦和清晰

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 基于调用搜索，确认以下 helper 已无外部调用方：
  - `SetActiveSkinSilent(...)`
  - `SetActiveSkinSource(...)`
  - `SkinApplicationCoordinator.UpdateSource(...)`
- 删除上述未使用 helper
- 保留当前真实使用中的：
  - `SetActiveSkinWithSource(...)`
  作为 runtime apply 的统一静默写入入口
- 不改变 `ActiveSkin`、`ClearSkin()`、恢复路径与默认绑定路径的既有行为

### 预期不变行为
- `PawnSkinRuntimeUtility` 仍通过 `SetActiveSkinWithSource(...)` 执行 runtime 静默写入
- 手动应用、恢复写回、默认绑定应用、清除皮肤的既有行为不变
- `activeSkin / activeSkinDefName / activeSkinFromDefaultRaceBinding` 的写入语义不变

### 允许变化行为
- `CompPawnSkin` 的内部 helper 表面缩小
- 旧的中间包装入口不再保留，后续维护时不再需要判断多条已失效写入路径
- 当前 runtime apply 入口更集中，后续继续收束更容易

### 本轮 review 方式
- 按当前用户指令执行 review-only：
  - 暂停 build
  - 仅做调用搜索与 API 表面收口 review

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮未执行 build，原因是用户明确要求暂停 build
- 先通过调用搜索确认 `SetActiveSkinSilent(...)`、`SetActiveSkinSource(...)` 与 `UpdateSource(...)` 均无调用方，再执行删除
- 本轮改动没有引入新的行为路径，只是收口已失效的内部 helper API
- 至此已按要求连续推进到 Step 40，并保持 build 暂停、review-only 的执行方式

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 内部未使用 helper 的可见面与维护成本
- 对外消费 API 未变化，主要收益是内部路径收敛与死代码清理
- 属于低风险的“内部 API 表面收口”重构

### 当前风险观察
- 仍未获得构建回归结果，当前结论来自代码 review 与调用搜索
- 当前主要风险不在功能路径，而在未做 build 验证与运行时验证
- 从调用分布看，这批被删 helper 已无真实消费方，风险整体可控

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 当前已按用户要求推进到 Step 40，并暂停 build
- 后续若恢复推进，建议优先选择两条路线之一：
  1. 补一次可采信 build，验证 Step 36 ~ Step 40 的完整编译回归
  2. 继续沿 `CompPawnSkin` / `PawnSkinRuntimeUtility` 的结果契约和清除路径做小步收束
- 继续坚持“改一小步 -> review 记录；按用户要求暂缓 build”的节奏

---

## Step 41：CompPawnSkin 清除路径结果契约首层上浮（第四十轮）

### 重构步骤名称
- 将 `SkinApplicationCoordinator.Clear(...)` 与 `CompPawnSkin.ClearSkinWithResult()` 收敛为可返回 `SkinApplicationWriteResult` 的 clear-path 写入契约

### 目标
- 让 clear-path 与 apply-path 一样具备“本次写入是否真实改变皮肤/来源标记”的结果表达
- 为 `PawnSkinRuntimeUtility.ClearSkinFromPawn(...)` 提供直接消费 `skinChanged` 的基础
- 保持原有清除皮肤字段写入、刷新与来源标记语义不变

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 将 `SkinApplicationCoordinator.Clear(CompPawnSkin owner)` 从 `void` 改为返回：
  - `SkinApplicationWriteResult`
- 新增：
  - `internal SkinApplicationWriteResult ClearSkinWithResult()`
- 将 clear-path 的底层写入统一经由：
  - `SkinApplicationCoordinator.Clear(this)`
- 保持 `SkinApplicationWriteResult` 继续承载：
  - `skinChanged`
  - `sourceChanged`
  - `HasChanged`

### 预期不变行为
- 清除皮肤时仍会清空：
  - `activeSkin`
  - `activeSkinDefName`
  - `activeSkinFromDefaultRaceBinding`
- clear-path 的即时刷新语义保持兼容
- 外部调用方的成功/失败判断方式暂不改变

### 允许变化行为
- clear-path 首次开始向外暴露结构化写入结果
- 后续调用方可以基于 `skinChanged` / `sourceChanged` 做副作用短路
- 当前仍只是结果首层上浮，外层 runtime 入口尚未完全消费全部细粒度结果

### 本轮 review 方式
- 按当前用户指令执行 review-only：
  - 暂停 build
  - 仅做 clear-path 写入契约与调用链检查

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮未执行 build，原因是当前阶段继续遵循暂停 build 的执行节奏
- 改动范围仅限 `CompPawnSkin` 内部 clear-path 结果契约上浮
- 当前主要收益是让 clear-path 也具备与 apply-path 对称的结果表达能力
- 该步本质上是契约增强，后续价值取决于 runtime clear 调用方是否继续接入该结果

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` 的 clear-path 内部协作边界
- 不改变编辑器、UI 与外部调用入口的 outward API
- 属于低风险的“clear-path 结果首层上浮”重构

### 当前风险观察
- 当前未获得构建回归结果，仍属于 review-only 结论
- `ClearSkinWithResult()` 新增后，旧 clear API 与新结果 API 在短期内并存
- 后续若不继续清理 API 表面，clear-path 入口会暂时存在双轨表达

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续把 `PawnSkinRuntimeUtility.ClearSkinFromPawn(...)` 接到 `ClearSkinWithResult()` 上
- 优先让 runtime clear 路径在“无真实皮肤变化”时直接短路重副作用
- 保持暂停 build、先做 review-only 的推进方式

---

## Step 42：PawnSkinRuntimeUtility 清除路径无变化短路（第四十一轮）

### 重构步骤名称
- 让 `PawnSkinRuntimeUtility.ClearSkinFromPawn(...)` 在 clear-path 无真实皮肤变化时直接短路返回

### 目标
- 避免 runtime clear 路径在“当前已经没有皮肤”时仍继续执行渲染树重建和技能撤销
- 将 Step 41 上浮出来的 clear 写入结果真正接入 runtime 清理入口
- 保持 `ClearSkinFromPawn(...)` 的 outward `bool` 成功返回语义不变

### 涉及文件
- `Source/CharacterStudio/Core/PawnSkinRuntimeUtility.cs`

### 本轮改动点
- 在 `ClearSkinFromPawn(...)` 中接收：
  - `var writeResult = comp.ClearSkinWithResult();`
- 新增无变化短路：
  - `if (!writeResult.skinChanged) return true;`
- 保持外部调用方仍通过 `bool` 判断“清除入口是否成功执行”

### 预期不变行为
- 当皮肤真实发生清除时，仍继续执行：
  - `RefreshHiddenNodes(...)`
  - `ForceRebuildRenderTree(...)`
  - `RevokeAllCSAbilitiesFromPawn(...)`
- 外部调用方不需要同步修改

### 允许变化行为
- 当当前已经没有皮肤时，不再继续执行 runtime clear 的重副作用
- clear-path 开始像 apply-path 一样具备 no-op short-circuit 语义
- 返回 `true` 现在更明确表示“入口执行成功”，而非“必然发生了状态变化”

### 本轮 review 方式
- 按当前用户指令执行 review-only：
  - 暂停 build
  - 仅做 runtime clear 调用链与副作用路径检查

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮未执行 build，原因是当前阶段继续遵循暂停 build 的执行节奏
- `ClearSkinFromPawn(...)` 现在直接按 `writeResult.skinChanged` 决定是否进入重副作用路径
- 这意味着“已经无皮肤，再次 clear”时将不再重复 rebuild render tree 或 revoke abilities
- 该步继续维持对外 API 稳定，主要收益集中在内部副作用短路

### 影响范围评估
- 影响范围集中在 `PawnSkinRuntimeUtility` 的 runtime clear 副作用触发条件
- 不影响 UI/编辑器入口，不影响恢复策略与皮肤写入字段语义
- 属于低风险的“clear-path no-op 短路”重构

### 当前风险观察
- 当前仍未获得构建回归结果，结论基于代码 review
- 当前短路仅按 `skinChanged` 判定，不进一步区分 `sourceChanged`
- 若未来 clear-path 需要对来源标记变化触发额外行为，仍需单独评估

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续检查 clear/apply 两条 runtime 路径在缓存失效层面的对称性
- 优先确认 clear-path 是否也需要执行面部缓存清理
- 保持暂停 build、以 review-only 方式继续推进

---

## Step 43：PawnSkinRuntimeUtility 清除路径缓存失效对称补强（第四十二轮）

### 重构步骤名称
- 为 `PawnSkinRuntimeUtility.ClearSkinFromPawn(...)` 补上与 apply-path 对称的表情缓存清理

### 目标
- 避免 clear-path 在真实清除皮肤后继续复用旧的面部/眼球方向缓存
- 让 runtime clear 与 runtime apply 在 cache invalidation 层面更对称
- 保持 clear-path 的 outward API 与重副作用顺序不变

### 涉及文件
- `Source/CharacterStudio/Core/PawnSkinRuntimeUtility.cs`

### 本轮改动点
- 在 `ClearSkinFromPawn(...)` 的真实皮肤变化分支中新增：
  - `CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent.ClearCache();`
  - `CharacterStudio.Rendering.PawnRenderNodeWorker_EyeDirection.ClearCache();`
- 保持上述缓存清理仍位于：
  - `writeResult.skinChanged` 通过之后
  - render tree rebuild 之前

### 预期不变行为
- 当皮肤真实发生清除时，仍继续执行 render tree rebuild 与能力撤销
- `ClearSkinFromPawn(...)` 的 outward `bool` 语义不变
- apply-path 与 clear-path 的主流程顺序保持兼容

### 允许变化行为
- clear-path 在真实清除皮肤后会主动清理面部/眼球方向缓存
- 避免继续沿用旧皮肤缓存造成的贴图残留
- runtime clear 与 runtime apply 的缓存层行为更加对称

### 本轮 review 方式
- 按当前用户指令执行 review-only：
  - 暂停 build
  - 仅做 cache invalidation 对称性检查

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮未执行 build，原因是当前阶段继续遵循暂停 build 的执行节奏
- 通过搜索确认 `FaceComponent` 与 `EyeDirection` 都存在 `ClearCache()` 入口后，补齐 clear-path 缓存失效
- 该步主要补强 clear/apply 对称性，不改变 outward 调用方式
- 当前收益集中在减少 clear 后继续复用旧贴图缓存的风险

### 影响范围评估
- 影响范围集中在 `PawnSkinRuntimeUtility.ClearSkinFromPawn(...)` 的 clear-path 缓存失效层
- 不影响编辑器、恢复路径或皮肤字段写入语义
- 属于低风险的“clear/apply 缓存对称性补强”重构

### 当前风险观察
- 当前仍未获得构建回归结果，结论基于代码 review 与符号搜索
- 当前缓存清理只在 `skinChanged == true` 时执行，意味着 source-only clear 不会额外清缓存
- 这一点与当前“按真实皮肤变化驱动重副作用”的目标一致，但仍建议后续在运行时补验证

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续检查 clear-path API 表面是否还能低风险收口
- 若 `CompPawnSkin.ClearSkin()` 已无调用方，可进一步删除旧包装入口
- 保持暂停 build、继续按 review-only 节奏推进

---

## Step 44：CompPawnSkin 未使用 ClearSkin 包装入口收口（第四十三轮）

### 重构步骤名称
- 删除 `CompPawnSkin` 中已无调用方的 `ClearSkin()` 包装层，仅保留 `ClearSkinWithResult()` 作为 clear-path 真实入口

### 目标
- 缩小 `CompPawnSkin` 在 clear-path 上的内部 API 表面
- 去除已经被结构化结果入口替代、且无任何直接调用方的旧包装方法
- 让 clear-path 的真实消费边界更加聚焦和清晰

### 涉及文件
- `Source/CharacterStudio/Core/CompPawnSkin.cs`

### 本轮改动点
- 基于调用搜索确认：
  - `CompPawnSkin.ClearSkin()` 已无直接调用方
  - 当前唯一真实调用点为：
    - `var writeResult = comp.ClearSkinWithResult();`
- 删除未使用的：
  - `public void ClearSkin()`
- 保留当前真实使用中的：
  - `internal SkinApplicationWriteResult ClearSkinWithResult()`
  作为 runtime clear 的统一结构化入口

### 预期不变行为
- `PawnSkinRuntimeUtility` 仍通过 `ClearSkinWithResult()` 执行 runtime clear
- clear-path 的字段写入、缓存清理、render tree rebuild 与能力撤销语义不变
- 外部 UI 调用 `PawnSkinRuntimeUtility.ClearSkinFromPawn(...)` 的方式不变

### 允许变化行为
- `CompPawnSkin` 的 clear-path API 表面缩小
- 旧的无结果包装入口不再保留，后续维护时无需再在双轨 clear API 之间做判断
- 当前 clear-path 结果入口更集中，后续继续收束更容易

### 本轮 review 方式
- 按当前用户指令执行 review-only：
  - 暂停 build
  - 仅做调用搜索与 API 表面收口 review

### 本轮 review 结果
- [ ] 通过
- [x] 有风险但可继续
- [ ] 不通过，需要回滚或修复

### review 摘要
- 本轮未执行 build，原因是当前阶段继续遵循暂停 build 的执行节奏
- 先通过调用搜索确认 `CompPawnSkin.ClearSkin()` 已无直接消费方，再执行删除
- 删除后再次搜索 `\.(ClearSkinWithResult|ClearSkin)\s*\(`，结果仅剩：
  - `Source/CharacterStudio/Core/PawnSkinRuntimeUtility.cs`
  - `var writeResult = comp.ClearSkinWithResult();`
- 本轮改动没有引入新的行为路径，只是继续收口 clear-path 已失效的旧包装 API

### 影响范围评估
- 影响范围集中在 `CompPawnSkin` clear-path 内部 API 的可见面与维护成本
- 对外消费 API 未变化，主要收益是调用边界收敛与旧包装清理
- 属于低风险的“clear-path API 表面收口”重构

### 当前风险观察
- 当前仍未获得构建回归结果，结论来自代码 review 与调用搜索
- 当前主要风险不在功能路径，而在未做 build 验证与运行时验证
- 从调用分布看，这个被删包装层已无真实消费方，风险整体可控

### 是否建议进入下一步
- [x] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

### 下一步建议
- 继续沿 `CompPawnSkin` / `PawnSkinRuntimeUtility` 的结果契约与副作用对称性子线推进
- 优先评估 `ApplySkinToPawn(...)` / `ClearSkinFromPawn(...)` 是否还能共用更统一的 runtime 副作用协同边界
- 继续坚持“改一小步 -> review 记录；按当前节奏暂缓 build”的方式

---

# 三、核心回归路径清单

> 说明：不是每轮都要全量回归。  
> 每次只勾选与改动相关的部分，但以下项目构成 CharacterStudio 的核心行为基线。

---

## A. 编辑器基础打开与初始化

### A1. 打开主编辑器窗口
- [ ] 能正常打开 `Dialog_SkinEditor`
- [ ] 无明显红字/异常日志
- [ ] 初始界面布局正常
- [ ] 默认 tab 可正常显示

### A2. 打开已有皮肤进入编辑器
- [ ] 能从已有 `PawnSkinDef` 打开编辑器
- [ ] 标题/描述/基础信息正常显示
- [ ] 图层列表能正常加载
- [ ] 不出现空引用或选择状态异常

### A3. 从 Pawn 打开编辑器
- [ ] 能从目标 Pawn 打开编辑器
- [ ] `targetPawn` 正确绑定
- [ ] 预览种族/目标种族信息正确
- [ ] Pawn 当前皮肤能正确载入到编辑器

---

## B. 图层与基础外观编辑

### B1. 新建皮肤
- [ ] 能正常创建新皮肤
- [ ] 初始 `defName / label / description` 正常
- [ ] 默认数据结构完整，不报错

### B2. 图层增删改
- [ ] 能新增图层
- [ ] 能删除图层
- [ ] 能上下移动图层
- [ ] 图层选中状态正常
- [ ] 多选状态正常
- [ ] 删除后选择索引未错乱

### B3. 图层属性编辑
- [ ] `texPath` 可编辑
- [ ] `anchorPath / anchorTag` 可编辑
- [ ] drawOrder 修改后界面与预览正常
- [ ] offset / scale / rotation 修改可生效
- [ ] shader / color / 可见性编辑正常

### B4. Base Appearance 编辑
- [ ] 各基础外观槽位可显示
- [ ] 槽位启用/禁用正常
- [ ] 槽位贴图可替换
- [ ] 槽位偏移/缩放/旋转正常
- [ ] BaseAppearance 改动后预览正常

---

## C. 面部与动态状态

### C1. Face 面板打开
- [ ] Face 页签可正常打开
- [ ] 表情配置能显示
- [ ] 分层面部部件配置能显示
- [ ] 无空引用报错

### C2. 表情预览
- [ ] 手动切换表情可生效
- [ ] Blink 预览正常
- [ ] Happy / Sad / Angry / Neutral 至少可切换
- [ ] 预览覆盖开关生效
- [ ] 关闭覆盖后恢复自动状态

### C3. 通道状态预览
- [ ] Mouth 覆盖可用
- [ ] Lid 覆盖可用
- [ ] Brow 覆盖可用
- [ ] Emotion 覆盖可用
- [ ] 清除覆盖后状态恢复正常

### C4. Eye Direction 预览
- [ ] 眼球方向覆盖可用
- [ ] Left / Right / Up / Down / Center 正常切换
- [ ] 关闭覆盖后恢复自动逻辑

### C5. 自动播放预览
- [ ] Auto Play 可启动
- [ ] 表情按预期轮换
- [ ] 停止后状态稳定
- [ ] 不出现明显卡顿或异常刷新

---

## D. 预览与人台系统

### D1. 人台初始化
- [ ] `MannequinManager` 能正常初始化
- [ ] 预览 Pawn 正常显示
- [ ] 切换 race 时无异常

### D2. 预览交互
- [ ] 旋转预览正常
- [ ] 缩放预览正常
- [ ] 点击选中图层正常
- [ ] 拖拽调整位置正常
- [ ] 参考虚影功能正常

### D3. 图层预览应用
- [ ] 修改图层后预览立即刷新
- [ ] 选中高亮正常
- [ ] 武器预览叠加正常
- [ ] Base slot 预览拖拽正常

---

## E. 导入链路

### E1. 从 Pawn 导入
- [ ] 能正常选择地图 Pawn
- [ ] 导入后图层数量合理
- [ ] BaseAppearance 可正确合并/替换
- [ ] 导入完成提示正常

### E2. 从 Race 导入
- [ ] 能正常选择 race
- [ ] 导入后预览 race 正确
- [ ] 图层/基础外观数据结构完整

### E3. 从项目皮肤导入
- [ ] 能从已有 `PawnSkinDef` 导入
- [ ] sourceSkinDefName 逻辑正常
- [ ] 不出现重复叠层或错误覆盖

---

## F. 保存、应用、导出链路

### F1. 保存皮肤
- [ ] 点击保存无异常
- [ ] 保存后的 skin 能再次正常读取
- [ ] 保存后关键字段未丢失

### F2. 应用到 Pawn
- [ ] 能成功应用到目标 Pawn
- [ ] Pawn 外观即时刷新
- [ ] 不出现异常红字
- [ ] 已应用皮肤在运行时可保持稳定

### F3. 导出 Mod
- [ ] 导出窗口可正常打开
- [ ] 引用资源分析正常
- [ ] 导出过程不报错
- [ ] 导出结果包含必要资源与 XML

---

## G. 运行时链路

### G1. PawnSkin 运行时挂载
- [ ] `CompPawnSkin` 能正常工作
- [ ] activeSkin 能正确恢复/加载
- [ ] 默认种族皮肤能按预期应用
- [ ] 清除皮肤后状态正常

### G2. 表情运行时更新
- [ ] 存活 Pawn 有正常基础表情
- [ ] 睡眠时表情切换正常
- [ ] 倒地/死亡时表情切换正常
- [ ] 心情/休息值 fallback 逻辑正常

### G3. 眼球方向运行时更新
- [ ] 有目标时眼神变化合理
- [ ] 无目标时无异常跳变
- [ ] 睡眠/死亡时回到 Center

### G4. 技能/施法状态
- [ ] 技能热键相关状态未损坏
- [ ] 冷却状态正常
- [ ] 施法窗口状态正常
- [ ] 武器持握视觉状态正常

---

## H. 渲染树与注入链路

### H1. RenderTree 正常建立
- [ ] 不因 patch 导致 Pawn 消失
- [ ] 自定义图层能正常注入
- [ ] 原版节点仍能正常绘制

### H2. Hide 规则
- [ ] `hiddenPaths` 生效
- [ ] 指定节点能被正确隐藏
- [ ] 不应误隐藏无关节点

### H3. Attach 规则
- [ ] attach 规则生成的 layer 能正常挂载
- [ ] anchorPath / anchorTag 能正确命中
- [ ] drawOrder 生效

### H4. Layered Face 注入
- [ ] 分层面部部件能注入
- [ ] overlay 顺序正确
- [ ] paired part 方向逻辑正常
- [ ] channel variant 路径能正确命中

### H5. 武器视觉注入
- [ ] Weapon carry visual 可显示
- [ ] offset / scale 生效
- [ ] 状态切换不报错

---

## I. 装备与技能编辑器

### I1. Equipment 编辑
- [ ] 能新增装备
- [ ] 能删除装备
- [ ] 能复制装备
- [ ] linked thing / shader / visual 配置正常

### I2. Ability 编辑
- [ ] `Dialog_AbilityEditor` 能正常打开
- [ ] 技能列表可显示
- [ ] 新建技能正常
- [ ] 导入 XML 正常
- [ ] 导出 XML 正常

### I3. 装备-技能绑定编译
- [ ] equipment 的 `abilityDefNames` 能正确参与编译
- [ ] hotkey 引用技能不会丢失
- [ ] 最终运行时 ability 列表符合预期

---

## J. Undo / Redo / 选择状态

### J1. Undo / Redo
- [ ] 撤销可用
- [ ] 重做可用
- [ ] 连续撤销不会破坏选择状态
- [ ] 图层修改/删除后撤销正常

### J2. 多选状态
- [ ] 多选图层后批量修改正常
- [ ] 删除多选图层后索引未错乱
- [ ] 切换 tab 不会破坏多选状态

### J3. 节点/图层/槽位选择
- [ ] 节点选择正常
- [ ] 图层选择正常
- [ ] Base slot 选择正常
- [ ] Equipment 选择正常
- [ ] 不同选择类型切换不会互相污染

---

# 四、与本轮改动强相关的专项检查

> 每轮重构时，把最相关的专项检查复制到本节。

## 本轮专项检查项
- [ ] 

## 本轮风险观察
- 

## 是否建议进入下一步
- [ ] 建议继续
- [ ] 先补修复
- [ ] 需要回滚

---

# 五、review 输出模板

## 本轮 review 结论
- 重构步骤：
- 结果：
- 影响范围：
- 主要风险：
- 是否建议继续下一步：

## 建议写法示例
- 本轮完成了 `CompPawnSkin` 中预览覆盖状态的抽离。
- 核心运行时行为未发现明显回归。
- 当前风险主要集中在 Face 预览刷新时序与 Undo 快照同步。
- 建议进入下一步，但下一步前先补一次针对 Face 预览链路的专项检查。

---

# 六、建议的执行节奏

## 第一阶段（护栏建设）
优先执行：
- A
- B
- C
- F
- G
- H

## 第二阶段（运行时拆分）
重点关注：
- G
- H
- C
- D

## 第三阶段（编辑态/编译态分层）
重点关注：
- A
- B
- E
- F
- I

## 第四阶段（编辑器状态治理）
重点关注：
- A
- B
- C
- D
- J

---

# 七、当前默认结论模板

在每一步重构完成后，默认按以下结构给出 review：

1. 本步改动了什么
2. 为什么这样改
3. 影响了哪些模块
4. 当前观察到的风险
5. 是否可以进入下一步

这份清单将作为后续每一步重构 review 的统一基线。