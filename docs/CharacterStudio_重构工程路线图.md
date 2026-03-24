# CharacterStudio 重构工程路线图

## 文档目的

本文档基于当前 CharacterStudio 项目结构与代码审查结果，给出一份**按优先级排序的中期重构工程方案**。  
目标不是一次性“大重写”，而是在**不破坏现有功能可用性**的前提下，逐步把项目从“强功能模组”收束为“可持续迭代的平台型模组”。

本文档聚焦以下几个问题：

- 当前系统最主要的复杂度来源是什么
- 为什么这些问题会阻碍后续开发
- 应该按什么顺序重构
- 每一阶段应该产出什么
- 如何控制风险、避免重构把项目拖垮

---

## 一、当前项目状态概述

CharacterStudio 已经不是一个简单的外观替换模组，而是一个具备以下能力的复杂系统：

- 角色外观/图层编辑
- 面部表情与动态状态驱动
- 自定义 PawnRenderTree 注入与隐藏
- 装备视觉与技能联动
- 编辑器预览系统
- 导入/导出能力
- 运行时皮肤挂载
- 向“文档 -> 编译产物 -> 应用执行”架构演进
- 局部接入 AI / LLM 辅助生成

这说明项目的核心问题已经不再是“功能不够”，而是：

1. 状态越来越多  
2. 运行时耦合越来越重  
3. UI 与运行时边界不够清晰  
4. 编译链路还处于过渡态  
5. 渲染补丁层需要更强的稳定性和治理能力  

因此，后续工作的重点应从**继续横向堆功能**转向**纵向治理结构**。

---

## 二、重构总原则

在进入具体阶段前，先明确重构时必须遵守的工程原则。

### 1. 不做全量推倒重来
CharacterStudio 已经具备大量有效资产：

- 领域模型
- 编辑器 UI
- 运行时渲染链路
- 资源加载与缓存
- 配套文档与使用路径

因此不应进行“从零重写式重构”。  
正确方式是：**围绕高风险节点做分层抽离和边界收束**。

### 2. 每一阶段必须可独立落地
每一轮改动都应满足：

- 可以编译
- 可以运行
- 不要求后续阶段全部完成后才能见效
- 即使中途暂停，当前产物仍有价值

### 3. 先收束状态，再收束结构
项目当前的主要问题不是“文件多”，而是“共享可变状态太多”。  
因此优先级上应先做：

- 运行时状态拆分
- 编辑器状态拆分
- 编译边界固定

再考虑更大规模的模块拆分。

### 4. 先建立边界，再建立规范
如果没有稳定边界，规范很难落地。  
因此先做：

- 运行时边界
- 编辑态边界
- 编译出口
- 渲染补丁入口

之后再谈统一验证、测试、命名、规则表化等。

### 5. 所有重构都要可验证
每一阶段至少要定义：

- 完成标准
- 兼容性检查点
- 回归验证点

---

## 三、重构优先级总览

### P0：建立重构护栏与度量基础
**目标：先让后续重构可控。**

### P1：拆分 `CompPawnSkin` 运行时总控
**目标：解决当前最明显的职责堆积问题。**

### P2：让 `CharacterDesignDocument` 成为真正的编辑源模型
**目标：建立“编辑文档 -> 编译产物”的真实边界。**

### P3：拆分 `Dialog_SkinEditor` 的状态与工作流
**目标：降低编辑器共享状态复杂度。**

### P4：建立统一验证与编译出口
**目标：让保存、应用、导入、导出都走统一校验链。**

### P5：降低字符串驱动逻辑的脆弱性
**目标：减少魔法字符串与隐式协议带来的长期风险。**

### P6：补测试与回归验证体系
**目标：让后续迭代速度建立在稳定基础上。**

### P7：渲染补丁层治理与兼容性收束
**目标：让 PawnRenderTree 补丁体系更稳、更易诊断。**

---

# 四、分阶段重构方案

---

## P0：建立重构护栏与度量基础

### 优先级
最高，且必须先做。

### 背景问题
如果直接进入结构拆分，很容易出现以下情况：

- 拆了一半无法确认行为是否变了
- 运行时看起来没报错，但实际预览、导出、应用已悄悄回归
- 改动越来越大，最后无法稳定合并

### 本阶段目标
建立一套“重构前后能对照”的护栏。

### 具体任务

#### 1. 明确关键行为清单
至少整理出以下关键路径：

- 新建皮肤
- 从 Pawn 导入
- 从 Race 导入
- 应用到 Pawn
- 导出为 Mod
- 图层预览拖拽
- 面部表情预览切换
- 装备与技能绑定编译
- Pawn 运行时自动应用默认皮肤
- PawnRenderTree 自定义图层注入
- 隐藏节点规则生效
- 运行时表情状态切换

#### 2. 建立最小回归检查表
在 docs 中维护一份“手工回归清单”，每次重构后可逐项确认。

建议文件：
- `docs/CharacterStudio_重构回归清单.md`

#### 3. 给关键入口补统一日志前缀
至少覆盖：

- 文档编译
- 皮肤应用
- 渲染树注入
- 导入
- 导出
- 运行时默认皮肤挂载

### 产出物
- 重构回归清单
- 关键路径列表
- 统一日志前缀规范

### 完成标准
- 任意一轮重构后，开发者能在 10~15 分钟内完成核心回归验证
- 出现问题时，日志能定位到“哪一段链路坏了”

---

## P1：拆分 `CompPawnSkin` 运行时总控

### 优先级
最高业务优先级。

### 当前问题
`CompPawnSkin` 同时承担了以下职责：

- 皮肤绑定与切换
- Xenotype 同步
- 表情状态机
- 动画帧推进
- 眨眼逻辑
- 眼球方向推断
- 预览覆盖状态
- 技能热键运行时状态
- 连段/CD/二段技能状态
- 渲染刷新触发
- 面部运行时缓存协调

这会导致：

- 文件持续膨胀
- 修改表情逻辑时可能影响技能逻辑
- 修改预览逻辑时可能污染运行时
- 运行时问题难以定位
- 后续功能接入门槛越来越高

### 重构目标
把 `CompPawnSkin` 从“全能总控器”收缩为“运行时协调器”。

### 建议拆分结构

#### 1. `SkinAttachmentState`
负责：

- activeSkin
- activeSkinDefName
- activeSkinFromDefaultRaceBinding
- Set/Clear/Load/PostExposeData 相关基础状态

#### 2. `FaceExpressionController`
负责：

- curExpression
- previewExpressionOverride
- blinkTimer
- expressionAnimTick
- UpdateExpressionState
- UpdateAnimatedExpressionFrame
- UpdateBlinkLogic
- GetEffectiveExpression

#### 3. `EyeDirectionController`
负责：

- curEyeDirection
- previewEyeDirectionOverride
- UpdateEyeDirectionState
- 方向映射工具方法

#### 4. `FacePreviewOverrideState`
负责：

- mouth/lid/brow/emotion 的 preview override
- 通道状态解析与对外接口

#### 5. `AbilityHotkeyRuntimeState`
负责：

- q/w/e/r cooldown
- q combo
- r 二段状态
- 施法窗口状态

#### 6. `FaceRuntimeCoordinator`
负责：

- faceRuntimeState
- faceRuntimeCompiledData
- MarkFaceRuntimeDirty
- EnsureFaceRuntimeStateUpdated
- EnsureFaceRuntimeStateReadyForPreview

### 推荐落地方式
不要一次性彻底拆完。  
建议三步走：

#### 第一步：先抽内部私有类/服务类
仍由 `CompPawnSkin` 持有实例，但逻辑迁移出去。

#### 第二步：收敛字段访问
减少外部代码直接碰 `CompPawnSkin` 内部字段。

#### 第三步：统一公共接口
只保留少量稳定方法，例如：

- `GetEffectiveExpression()`
- `RequestRenderRefresh()`
- `SetPreviewXxx(...)`
- `SetActiveSkinWithSource(...)`

### 产出物
- 更薄的 `CompPawnSkin`
- 若干运行时子控制器类
- 更清晰的运行时职责图

### 完成标准
- `CompPawnSkin` 不再包含大段表情、眼球、技能状态实现细节
- 各子模块职责能一句话解释清楚
- 运行时功能保持不变

---

## P2：让 `CharacterDesignDocument` 成为真正的编辑源模型

### 优先级
非常高，是中期架构转向的关键。

### 当前问题
当前 `CharacterDesignDocument` 仍然直接持有 `runtimeSkin`，并且通过：

- `SyncMetadataFromRuntimeSkin()`

将运行时对象反向覆盖文档元数据。

这意味着：

- 文档层不是唯一真相源
- UI 本质上仍在直接编辑运行时皮肤
- 编译器更像“后处理器”而不是“编译器”
- 后续做版本迁移、差量编辑、规则增强会越来越困难

### 重构目标
建立稳定的数据流：

**编辑文档 -> 编译器 -> 运行时皮肤 / 应用计划**

而不是：

**运行时皮肤 + 少量文档壳 -> 再补编译**

### 目标数据分层

#### 编辑态
`CharacterDesignDocument`
- 设计元信息
- 图层定义
- 面部配置定义
- 装备定义
- 技能定义
- 节点规则
- 编辑器附加元数据

#### 运行态
`PawnSkinDef`
- 可直接给运行时系统消费
- 无编辑器临时状态
- 无 UI 缓冲字段
- 已做完规范化和补全

### 建议实施步骤

#### 第一步：停止“文档元数据从 runtimeSkin 反向同步”
把 `title/description/author/version` 等文档字段变成真正的编辑字段。

#### 第二步：逐步把 `workingSkin` 的编辑入口迁移到 `workingDocument`
UI 编辑优先写文档字段，而不是直接写 runtimeSkin。

#### 第三步：让 `CharacterDesignCompiler` 从文档完整生成 `PawnSkinDef`
而不是以 clone 为核心。

#### 第四步：明确哪些字段只存在于编辑态
例如：

- sourceSkinDefName
- importedSources
- lastSavedFilePath
- 编辑器选择/布局相关信息（如未来需要持久化）

### 产出物
- 更明确的设计文档模型
- 更纯粹的运行时模型
- 可持续增强的编译边界

### 完成标准
- UI 不再把 `workingSkin` 作为主要编辑对象
- `CharacterDesignCompiler` 成为唯一运行时产物生成入口
- 文档可独立保存、迁移、升级

---

## P3：拆分 `Dialog_SkinEditor` 的状态与工作流

### 优先级
高。

### 当前问题
虽然 `Dialog_SkinEditor` 已拆成多个 partial 文件，但本质问题仍在：

- 大量共享字段集中在主类
- 所有 tab 共用一个大状态容器
- 预览、选择、导入、撤销、面部编辑互相影响
- partial 解决的是“文件长度”，不是“状态耦合”

### 重构目标
把编辑器从“巨型窗口类”变为“窗口 + 状态对象 + 工作流服务”的组合。

### 建议拆分方向

#### 1. `SkinEditorSession`
负责全局编辑会话：

- 当前文档
- 当前目标 Pawn
- dirty 状态
- 当前 tab
- 状态栏消息

#### 2. `SkinEditorSelectionState`
负责：

- selectedLayerIndex
- selectedLayerIndices
- selectedEquipmentIndex
- selectedNodePath
- selectedBaseSlotType

#### 3. `SkinEditorPreviewState`
负责：

- previewRotation
- previewZoom
- 各类 preview override 开关和值
- autoplay 状态
- reference ghost 状态

#### 4. `SkinEditorScrollState`
负责所有 scroll position

#### 5. `SkinEditorWorkflowService`
负责：

- 新建
- 导入
- 导出
- 应用到 Pawn
- 打开设置
- 打开 LLM 设置
- 保存流程

#### 6. `SkinEditorUndoService`
对接 `EditorHistory`，统一处理快照捕获与恢复

### 实施建议
先做“状态类抽离”，后做“方法类抽离”。

因为当前最主要的痛点不是方法放在哪个文件，而是共享状态无边界。

### 产出物
- 更轻的 `Dialog_SkinEditor`
- 更清晰的 UI 状态分层
- 更易插入新工作流

### 完成标准
- 新增一个 tab 或工作流时，不需要理解整个编辑器全局状态
- 各 partial 访问的字段显著减少
- 撤销、预览、选择状态不再互相缠绕

---

## P4：建立统一验证与编译出口

### 优先级
高。

### 当前问题
当前保存、应用、导入、导出链路中，规范化和校验逻辑分散在多个位置。  
这会导致：

- 某些路径修正了数据，另一些路径没修正
- 导入后能预览，但导出后坏掉
- 应用到 Pawn 没问题，但保存后再读出现异常
- 规则边界不清晰

### 重构目标
建立统一的“从文档到运行时”的处理出口。

### 建议结构

#### 1. `CharacterDesignValidator`
校验编辑文档是否完整、可编译。

#### 2. `CharacterDesignCompiler`
负责文档 -> 运行时皮肤。

#### 3. `PawnSkinRuntimeValidator`
负责运行时皮肤最终规范化。

#### 4. `CharacterApplicationPlanBuilder`
负责从文档或编译产物生成应用计划。

### 建议统一链路

#### 保存
`Document -> Validate -> Persist`

#### 应用到 Pawn
`Document -> Validate -> Compile -> RuntimeValidate -> BuildPlan -> Execute`

#### 导出
`Document -> Validate -> Compile -> RuntimeValidate -> Export`

#### 导入
`Import -> Normalize -> ConvertToDocument -> Validate`

### 额外建议
在编译结果对象中引入：
- warnings
- errors
- normalized field report

这样 UI 可以提示“已自动修正哪些问题”。

### 产出物
- 清晰的编译/验证总入口
- 各链路复用同一套核心逻辑
- 错误更容易定位

### 完成标准
- 不同入口的行为一致
- “为什么这里能过、那里不能过”的问题明显减少

---

## P5：降低字符串驱动逻辑的脆弱性

### 优先级
中高。

### 当前问题
当前系统大量依赖字符串协议：

- `job.defName` 包含关系
- 节点路径字符串
- `anchorTag`
- `overlayId`
- `defName`
- 贴图路径后缀规则
- 表情/方向/通道命名约定

这类设计在原型和快速迭代阶段很高效，但随着项目扩大，问题会逐步放大：

- 拼写错误静默失败
- 重构难以自动发现影响面
- 兼容性问题难定位
- 新人接手成本高

### 重构目标
逐步把“隐式字符串协议”替换成“显式规则与封装”。

### 建议措施

#### 1. Job -> Expression 抽成规则表
替代长链式 `if / else if`。

建议结构：
- 规则列表
- 每条规则含匹配条件和输出表情
- 支持调试输出“本次命中了哪条规则”

#### 2. 节点路径封装为值对象
例如：
- `RenderNodePath`
- `AnchorTarget`

#### 3. overlayId、anchorTag 建立统一规范入口
不要让各处自己拼接、猜测、归一化。

#### 4. 贴图变体解析独立为规则服务
尤其是：
- expression suffix
- directional suffix
- layered face channel suffix
- paired part 侧向变体

### 产出物
- 更稳定的规则层
- 更少的字符串散落逻辑
- 更强的调试能力

### 完成标准
- 关键规则不再依赖大量散落的 `string` 判断
- 出问题时可以明确看到“哪条规则没匹配上”

---

## P6：补测试与回归验证体系

### 优先级
中高，但要尽早开始，不必等前面全部完成。

### 当前问题
越复杂的系统，越不能靠“肉眼点点看”保证稳定。  
CharacterStudio 当前最需要保护的是以下几条核心链路：

- 文档编译
- 运行时挂载
- 图层规则编译
- 装备技能联动
- 导入导出往返一致性
- 表情/眼球状态解析

### 建议测试清单

#### 1. 编译测试
- 文档为空时是否能生成最小合法 `PawnSkinDef`
- 含 hide rule 时是否正确落入 `hiddenPaths`
- 含 attach rule 时是否正确生成 layer
- equipment ability binding 是否正确编译出最终 abilities

#### 2. 判重与规范化测试
- 相同 anchor 不同 offset 的 layer 不应被错误去重
- base appearance 缺省项是否自动补齐
- 非法数值是否被 runtime validator 修正

#### 3. 导入导出 round-trip 测试
- 导出后重新导入，关键字段保持一致
- face config 不丢失
- equipment 配置不丢失
- ability hotkey 关联不丢失

#### 4. 表情逻辑测试
- 死亡/睡眠/倒地优先级正确
- mood/rest fallback 正确
- preview override 能覆盖运行时表达式

#### 5. 渲染相关的最小验证测试
如果不能做完整自动测试，也应至少建立日志断言或 debug 检查点。

### 产出物
- 一组最小但高价值的测试
- 一份长期可维护的回归清单

### 完成标准
- 关键编译逻辑改动后可以快速发现回归
- 重构不再完全依赖手工直觉

---

## P7：渲染补丁层治理与兼容性收束

### 优先级
中后期，但非常重要。

### 当前问题
`Patch_PawnRenderTree` 体系是 CharacterStudio 的核心能力来源，同时也是兼容性与维护风险最高的部分。

风险主要来自：

- RimWorld 版本变动
- 其他模组也 patch 相同方法
- 图形 cache 状态不一致
- 注入/隐藏/基础外观覆写链路过长
- 一旦出问题，症状常表现为“没画出来”，很难快速定位

### 重构目标
让渲染补丁从“可用但复杂”逐步变成“可诊断、可分层、可降级”。

### 建议措施

#### 1. 把 patch 注册改为独立异常隔离
不要所有 patch 放一个 try/catch。

#### 2. 给渲染补丁链路加阶段化日志
例如：

- RenderTree Setup
- Hidden Node Process
- BaseAppearance Override
- Custom Layer Injection
- Layered Face Injection
- Weapon Visual Injection

#### 3. 建立“失败降级策略”
例如某个注入阶段失败时：

- 是否仍允许基础皮肤显示
- 是否允许只关闭 layered face
- 是否允许跳过 weapon visual 注入

#### 4. 明确 cache 生命周期
重点治理：
- Graphic cache
- Material cache
- runtime asset cache
- hidden node cache
- node offset registry

#### 5. 抽渲染诊断窗口或 debug dump
至少能回答：
- 当前 Pawn 的 active skin 是什么
- 当前注入了哪些 layer
- 哪些 node 被隐藏了
- 哪个变体路径最终命中
- 为什么某层 `CanDrawNow = false`

### 产出物
- 更可调试的渲染链路
- 更强的模组兼容治理能力
- 更低的维护成本

### 完成标准
- 渲染问题可以在较短时间内定位到具体阶段
- 某个局部渲染功能失效时，不会导致整个系统不可用

---

# 五、建议的实施里程碑

---

## 里程碑 M1：运行时收束
对应优先级：
- P0
- P1

### 目标
先把最危险的运行时复杂度压下去。

### 交付内容
- 回归清单
- `CompPawnSkin` 拆分第一轮
- 统一运行时日志前缀
- 不改变外部功能表现

---

## 里程碑 M2：编辑态/编译态分层
对应优先级：
- P2
- P4

### 目标
建立文档为中心的数据流。

### 交付内容
- `CharacterDesignDocument` 成为编辑源
- `CharacterDesignCompiler` 成为唯一编译出口
- `PawnSkinRuntimeValidator` 固定接在编译出口
- 保存/应用/导出共用统一链路

---

## 里程碑 M3：编辑器状态治理
对应优先级：
- P3

### 目标
降低大窗口类失控风险。

### 交付内容
- `Dialog_SkinEditor` 状态对象化
- 预览、选择、工作流、撤销分层
- 新增功能时不必触碰全局状态

---

## 里程碑 M4：规则系统与稳定性增强
对应优先级：
- P5
- P6
- P7

### 目标
让项目具备长期演进能力。

### 交付内容
- 规则表化
- 最小测试集
- 渲染诊断能力
- 更稳的 patch 生命周期治理

---

# 六、不建议现在做的事

为避免重构范围失控，以下事项不建议当前立即推进。

## 1. 不要先全面重写 UI
UI 表层虽然复杂，但真正的痛点是状态与边界，不是绘制代码本身。

## 2. 不要先全面替换渲染方案
PawnRenderTree patch 虽复杂，但已是项目能力核心，贸然整体替换风险极高。

## 3. 不要先做“所有规则数据驱动化”
规则表化是方向，但应先处理最脆弱、最频繁修改的部分，例如 Job -> Expression。

## 4. 不要把测试目标定得过大
先建立最小高价值测试集，比试图一口气覆盖整个项目更现实。

---

# 七、建议新增的工程文档清单

建议在 `docs/` 下逐步补齐以下文档：

## 1. `CharacterStudio_重构回归清单.md`
记录每轮重构后的手工检查项。

## 2. `CharacterStudio_运行时架构图.md`
梳理：
- CompPawnSkin
- Face runtime
- RenderTree patch
- Asset loader
- Ability runtime state

## 3. `CharacterStudio_编辑态与编译态边界说明.md`
明确：
- 文档层
- 编译层
- 运行时层
- 应用执行层

## 4. `CharacterStudio_渲染补丁诊断手册.md`
用于排查：
- 没有绘制
- 图层错位
- 贴图未命中
- 节点未隐藏
- 局部功能与其他模组冲突

## 5. `CharacterStudio_规则系统演进计划.md`
记录哪些字符串协议要逐步规则化、类型化。

---

# 八、推荐的首轮执行顺序

如果从下一轮开发开始正式落地，推荐顺序如下：

### 第 1 周
- 建立重构回归清单
- 梳理关键日志点
- 拆 `CompPawnSkin` 第一批子职责（先 internal service）

### 第 2 周
- 清理 `CompPawnSkin` 对外暴露面
- 把 preview override 与 ability runtime state 分离

### 第 3 周
- 停止文档元数据从 runtimeSkin 反向覆盖
- 让部分 UI 编辑行为直接写入 `CharacterDesignDocument`

### 第 4 周
- 建立统一编译出口
- 保存/应用/导出接入一致链路

### 第 5 周
- 给 `Dialog_SkinEditor` 抽 session / selection / preview state
- 整理撤销与预览更新路径

### 第 6 周及以后
- 推进规则表化
- 补最小测试
- 做渲染诊断与 patch 分层治理

---

# 九、最终结论

CharacterStudio 当前最需要的不是“再加多少功能”，而是：

- 把运行时总控拆开
- 把编辑态与运行态彻底分层
- 把大型窗口状态治理起来
- 把编译与验证出口固定下来
- 把字符串协议逐步规则化
- 把关键链路测试补上

只要按本文档的优先级顺序推进，项目就可以从：

**功能很强但复杂度快速上升的模组**

逐步演进为：

**结构清晰、扩展稳定、可长期维护的角色创作平台型模组**

---

## 附录：一句话版执行策略

> 先护栏，后拆运行时；先确立文档边界，再收束编辑器状态；最后补规则、补测试、补渲染治理。