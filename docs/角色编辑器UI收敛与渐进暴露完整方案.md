# CharacterStudio 角色编辑器 UI 收敛与渐进暴露完整方案

> 基于当前源码结构整理。  
> 本方案的目标不是把所有底层字段直接铺到主界面，而是在 **有限 UI 空间** 内，建立一套可持续扩展的 **渐进暴露（Progressive Disclosure）** 方案，让普通创作者能顺畅完成大部分工作，同时让高级用户仍可到达底层能力。  
> 本文作为后续逐步优化的主方案文档，后续实现应以源码为准。

---

## 1. 背景与问题定义

根据当前已读取的源码，CharacterStudio 的底层能力明显强于主界面暴露能力，主要体现在以下三类：

### 1.1 图层系统底层能力强于当前属性面板
`PawnLayerConfig` 与运行时渲染层已经支持大量高级能力，例如：

- `anchorPath`
- `scaleEastMultiplier`
- `scaleNorthMultiplier`
- `rotationEastOffset`
- `rotationNorthOffset`
- `graphicClass`
- `shaderDefName`
- `colorSource`
- `colorTwoSource`
- `maskTexPath`
- `visible`
- `rotDrawMode`
- `weaponCarryVisual`
- `useTriggeredEquipmentAnimation`
- 一整套表达过滤、方向后缀、眨眼、帧序列、眼球 UV 偏移、动画控制等

但当前主属性面板主要暴露的是“可创作所需的第一层字段”，并没有完整接出这些高级能力。

### 1.2 面部系统已经形成独立子系统，但 UI 仍偏收敛入口
`PawnFaceConfig` 与运行时面部系统支持：

- `FullFaceSwap`
- `LayeredDynamic`
- 分层部件
- overlay
- expression path
- eye direction
- frame animation
- 分方向贴图
- 左右部件
- overlay order
- fallback

但当前 Face 页更偏向于：

- 自动导入
- 常见部件快速配置
- 少量 anchor 校正
- 少量眼睛方向控制

这对于普通创作流是好事，但也意味着 **高级能力需要新的承载方式**。

### 1.3 导出链路对高级资源的闭环不完整
从 `ModBuilder` 来看，导出已覆盖不少纹理 remap，但对于更复杂的高级面部资源，闭环仍不够完整。例如潜在缺口包括：

- `ExpressionFrame.frames[*].texPath`
- `faceConfig.layeredParts[*].texPath`
- `texPathSouth`
- `texPathEast`
- `texPathNorth`

这类问题本质不是“再加几个字段”就能解决，而是需要 **导出前校验器 + 补齐导出 remap 流程**。

---

## 2. 本方案解决什么，不解决什么

## 2.1 解决目标

本方案解决以下问题：

1. 当前 UI 放不下更多字段的问题
2. 如何把未暴露的高级能力分类管理的问题
3. 如何避免主面板变成“渲染底层调试器”的问题
4. 如何让高级能力可达，但不打断主创作流
5. 如何让导入、编辑、预览、导出形成更稳定闭环

## 2.2 暂不解决的内容

本方案暂不以“完整新物种生成器”为目标，不做以下扩张：

- 不把 CharacterStudio 直接变成完整 RaceDef 编辑器
- 不把所有玩法系统参数塞进角色编辑器
- 不以“所有底层能力都直接暴露”为原则
- 不在第一阶段引入巨量新页签或超大右侧表单

---

## 3. 核心设计原则

后续所有 UI 优化都应遵守以下原则。

### 3.1 主流程优先
优先保证 80% 用户最常见的角色创作流程：

- 选模板 / 导入资源
- 选图层 / 调整位置
- 预览角色
- 调整表情 / 面部部件
- 配置基础属性
- 导出可用结果

### 3.2 渐进暴露，而非一次性全部展开
能力分层暴露：

- 主面板：高频核心能力
- Advanced 折叠区：中频能力
- 子编辑器：低频但复杂能力
- 导入器 / 预设 / 校验器：不适合手填的能力

### 3.3 暴露“对象入口”，而不是暴露“散乱字段”
例如：

- 不要平铺 10 个触发动画字段
- 而是显示一行摘要：`触发动画：已配置 2 条规则`
- 然后提供按钮：`编辑触发动画...`

### 3.4 复杂系统必须有摘要层
凡是复杂能力，都必须先有“摘要信息”，例如：

- 材质：`双通道着色 / 遮罩已启用`
- 方向覆盖：`South/East/North 已配置`
- 变体规则：`按表情+方向切换`
- 表达过滤：`显示 4 / 隐藏 2`

### 3.5 能自动导入的，不要求手填
面部系统和复杂图层结构应该优先通过：

- 命名约定
- 自动识别
- 预设模板
- 导入报告

建立内容，而不是要求用户手工逐字段创建。

### 3.6 能校验的，不要求用户理解底层 remap 细节
导出前应该告诉用户：

- 哪些资源没被 remap
- 哪些方向贴图没打包
- 哪些 frame path 有风险
- 哪些 layered face 项可能导出后失效

而不是把所有 remap 逻辑暴露成设置项。

---

## 4. 总体分层策略

把编辑器能力划分成四层：

| 层级 | 定义 | UI 形式 | 示例 |
|---|---|---|---|
| L0 主面板 | 高频、单值、立即可见效果 | 直接控件 | texPath / offset / scale / rotation |
| L1 Advanced | 中频、仍属于单个对象属性 | 折叠组 | anchorPath / shader / directional scale |
| L2 子编辑器 | 低频但成组且复杂 | 弹窗 / 子窗口 | 触发动画 / 面部资源编辑 / 材质设置 |
| L3 自动流程 | 不适合手填，适合导入与校验 | 导入器 / 预设 / 导出验证 | layered face 结构生成 / remap 检查 |

---

## 5. 目标 UI 架构

在不重做整体主窗口的前提下，建议保持当前大结构：

- 左侧：树 / 对象列表 / 部件列表
- 中央：预览与上下文状态
- 右侧：属性检查器

但右侧属性检查器的组织方式需要调整为四区结构：

### 5.1 右侧属性面板四区结构

1. **基础字段区（Basic）**
   - 当前对象的核心编辑字段
   - 默认始终展开

2. **上下文辅助区（Context）**
   - 当前对象摘要
   - 当前对象状态
   - 当前对象关联规则摘要

3. **高级字段区（Advanced）**
   - 默认折叠
   - 放置中频但重要字段

4. **专项工具区（Tools）**
   - 只放按钮和摘要
   - 按下后进入二级编辑器

### 5.2 不再做的事情
不再尝试：

- 在单个右侧面板中直接铺开所有高级字段
- 在主页面同时出现“材质”“动画”“方向覆盖”“frame 序列”“表达过滤”“触发规则”等全部明细
- 把角色编辑器做成一个需要不停滚动的大表单

---

## 6. 图层系统完整优化方案

---

## 6.1 图层主面板（P0 必须保留）

这些字段必须保留在第一层，因为它们是最核心、最常改、最能立刻看到效果的：

### 基础标识与资源
- `layerName`
- `texPath`
- `anchorTag`

### 基础变换
- `offset`
- `offsetEast`
- `offsetNorth`（可考虑折叠为“方向偏移”子组）
- `scale`
- `rotation`
- `drawOrder`
- `flipHorizontal`

### 颜色入口
- 常用颜色来源选择
- `customColor`
- `customColorTwo`（仅在需要时出现）

### 常见逻辑
- `role`
- `variantLogic`
- `variantBaseName`

### 常用后缀逻辑
- `useDirectionalSuffix`
- `useExpressionSuffix`
- `useEyeDirectionSuffix`
- `useBlinkSuffix`
- `useFrameSequence`
- `hideWhenMissingVariant`

### 基础动画
- `animationType`
- `animFrequency`
- `animAmplitude`

### 设计目标
主面板必须保证用户在不打开任何高级窗口的情况下，也能完成：

- 添加一个图层
- 指定资源
- 把图层放到正确位置
- 设置颜色
- 让图层跟随表情/方向/眨眼变化
- 做简单动态效果

---

## 6.2 图层 Advanced 折叠区（P1）

以下字段适合放进 `Advanced` 折叠组，默认收起：

### 锚点与方向微调
- `anchorPath`
- `scaleEastMultiplier`
- `scaleNorthMultiplier`
- `rotationEastOffset`
- `rotationNorthOffset`

### 渲染与材质
- `visible`
- `shaderDefName`
- `rotDrawMode`

### 高级颜色/遮罩
- 完整 `colorSource`
- `colorTwoSource`
- `maskTexPath`

### 眼球 UV 与表达筛选
- `eyeRenderMode`
- `eyeUvMoveRange`
- `visibleExpressions`
- `hiddenExpressions`

### 设计目标
Advanced 区的原则是：

- 默认不干扰普通用户
- 进阶用户不需要翻页就能找到
- 所有 Advanced 字段仍属于“单对象属性”，没有跨多规则的复杂关系

---

## 6.3 图层专项子编辑器（P2）

以下能力不应该继续内联在主属性区，应改为“摘要 + 编辑按钮”。

### A. 触发动画编辑器
应独立成 `Dialog_LayerTriggeredAnimationEditor` 或同类窗口。

#### 涉及能力
- `useTriggeredEquipmentAnimation`
- 触发条件
- 武器/装备触发绑定
- 触发时的 offset / rotation / speed / amplitude / phase
- 多条件情况

#### 主面板展示方式
- 摘要：`触发动画：未启用 / 已配置 1 条 / 已配置 3 条`
- 按钮：`编辑触发动画...`

#### 原因
这不是一个字段，而是一整套系统。

---

### B. 材质与着色编辑器
应独立成 `Dialog_LayerMaterialSettings`。

#### 涉及能力
- `shaderDefName`
- `colorSource`
- `colorTwoSource`
- `maskTexPath`

#### 主面板展示方式
- 摘要：`材质：Cutout / 双通道 / 遮罩已设置`
- 按钮：`编辑材质与颜色...`

#### 原因
这些字段之间强耦合，单独放在主面板只会造成认知断裂。

---

### C. 方向覆盖与序列编辑器
应独立成 `Dialog_LayerVariantEditor`。

#### 涉及能力
- `variantLogic`
- 各类 suffix 组合
- 帧序列
- 缺失变体处理
- 方向资源校验

#### 主面板展示方式
- 摘要：`变体：方向 + 表情 / 帧序列 4 帧`
- 按钮：`编辑变体规则...`

#### 原因
这部分本质上是“资源解析策略编辑器”，不适合散字段化。

---

### D. 表达过滤编辑器
应独立成一个轻量多选窗口。

#### 涉及能力
- `visibleExpressions`
- `hiddenExpressions`

#### 主面板展示方式
- 摘要：`表达过滤：显示 4 / 隐藏 2`
- 按钮：`编辑表达过滤...`

#### 原因
多选内容不适合在主面板里用长文本或大量复选框表示。

---

## 6.4 图层系统摘要行设计

为解决主 UI 空间不足，建议图层属性区引入以下摘要行：

- `资源状态：主纹理已设置 / 缺失方向变体`
- `颜色策略：PawnSkin / Custom / DualColor`
- `材质状态：CutoutComplex + Mask`
- `变体状态：方向 + 表情 + 眨眼`
- `动画状态：IdleSway / 频率 0.25 / 幅度 0.08`
- `触发状态：绑定近战武器`
- `可见性状态：仅 Angry / Happy 显示`

这些摘要行的作用是：

- 让用户不用打开子编辑器也能理解当前配置
- 减少对“大量明细控件”的依赖
- 支持快速扫描多个图层对象

---

## 7. 面部系统完整优化方案

---

## 7.1 Face 主页面定位

Face 页应明确定位为：

> “面部系统的主工作流入口与预览校对页”，  
> 而不是“把所有面部底层字段全部展开的总表单”。

### Face 主页面保留内容
- `workflowMode`
- 自动导入入口
- 当前导入结果摘要
- preview expression
- preview eye direction
- Base / Eye / Pupil / UpperLid / LowerLid / Brow / Mouth / Overlay 这些常用 part 的启用、路径、浏览、清空、基础 anchor 校正

### 目标
用户在主 Face 页应能完成：

- 自动导入一套面部资源
- 快速调整核心部件
- 修正简单错位
- 测试不同表情和眼睛方向
- 判断是否需要进入高级编辑器

---

## 7.2 Face Advanced 折叠区（P1）

主页面可增加 `Advanced` 折叠区，但只放摘要和少量中频字段：

- 当前 overlay 数量
- 当前左右拆分部件数量
- 当前方向覆盖数量
- eyeDirection 参数摘要
- fallback 状态摘要
- 当前 workflow 额外能力提示

这一区不建议继续展开成大量逐项字段，而应更多承担“状态与入口”作用。

---

## 7.3 面部资源编辑器（P2）

应新增 `Dialog_FaceAssetEditor`，作为面部系统的核心高级编辑器。

### 该窗口负责的内容
1. 查看所有 face part 的完整结构
2. 编辑 layered parts
3. 编辑 direction override
4. 管理 overlay 分组
5. 管理左右部件
6. 管理特殊部件与 fallback

### 主页面展示方式
- 摘要：`面部资源：12 个部件 / 3 个 overlay / 2 个方向覆盖`
- 按钮：`打开面部资源编辑器...`

### 原因
这些内容本质是“面部资源拓扑结构”，不是普通属性项。

---

## 7.4 FullFace 高级表达映射器（P2）
当前 FullFace 更偏自动导入和摘要，这个方向没有问题，但为了支持高级作者，应增加：

`Dialog_FullFaceExpressionMapper`

### 负责内容
- 手工映射每种 expression
- 检查缺失 expression
- 指定 fallback
- 预览 expression 切换效果

### 主页面展示方式
- 摘要：`表情映射：已覆盖 8 / 缺失 2`
- 按钮：`编辑表达映射...`

### 原因
逐表情映射是低频高复杂任务，不应占据主页面空间。

---

## 7.5 Frame 动画编辑器（P2）
应新增 `Dialog_FaceFrameSequenceEditor`。

### 负责内容
- `ExpressionFrame.frames[*]`
- 帧顺序
- 帧时长
- 帧资源路径
- 动画预览
- 缺失帧检查

### 主页面展示方式
- 摘要：`Frame 动画：3 组 / 总 14 帧`
- 按钮：`编辑 Frame 动画...`

### 原因
这是一个典型“时间序列编辑问题”，不能靠普通属性栏处理。

---

## 7.6 Overlay 管理器（P2）
overlay 应从普通部件列表中抽象出来，进入单独 overlay 管理器。

### 负责内容
- overlay group
- overlay id
- overlay order
- 开关与层叠关系
- 部分条件显示规则

### 主页面展示方式
- 摘要：`Overlay：blush, tears, shadow`
- 按钮：`管理 Overlay...`

### 原因
overlay 本质是分层合成结构，不是普通贴图路径。

---

## 7.7 左右部件与方向覆盖创建器（P2）
为了解决复杂面部结构的“创建”问题，而不是只解决“编辑”问题，应增加结构型工具：

- 创建左右部件
- 从单张资源拆分规则生成 part
- 快速生成 south/east/north 结构
- 从命名规则批量补齐

这类功能应属于“工具按钮 + 子窗口”，而不是字段暴露。

---

## 8. 导入工作流优化方案

当前源码已经体现出较强的自动导入倾向，这个方向应该继续加强。

## 8.1 导入应成为高级结构的主要入口
尤其对于：

- layered face
- 左右部件
- overlay
- 方向贴图
- 表情命名
- 眼睛方向资源

原则上都应该优先通过自动导入建立初始结构。

## 8.2 导入后要给出结构报告
自动导入后，应展示一份摘要报告，而不是只完成数据写入：

- 检测到的 face part 数量
- 缺失的命名项
- 自动创建的 overlay 组
- 自动推断的 eyeDirection 配置
- 未能识别的资源列表
- 可能冲突的命名

## 8.3 导入后的推荐动作
自动导入完成后，给出建议入口：

- `打开面部资源编辑器`
- `查看未识别资源`
- `补齐缺失表达`
- `检查导出兼容性`

这样能把“自动导入”和“高级修正”自然串起来。

---

## 9. 导出闭环与导出前验证器方案

当前导出问题的核心不是 UI 暴露不够，而是闭环不完整。因此应优先增加导出前验证器。

## 9.1 导出前验证器职责
在导出前统一检查：

### 图层资源
- `texPath`
- `maskTexPath`
- variant 相关路径
- frame sequence 相关路径

### 面部资源
- `expressions[*].texPath`
- `ExpressionFrame.frames[*].texPath`
- `layeredParts[*].texPath`
- `texPathSouth`
- `texPathEast`
- `texPathNorth`
- eyeDirection 方向资源

### 资源 remap 完整性
- 是否属于导出目录
- 是否会在导出后失效
- 是否引用到未经 remap 的源路径

### 逻辑一致性
- 启用了 frame sequence 但资源数量为 0
- 配了 overlay 但 overlay id 缺失
- 使用方向覆盖但缺少关键方向贴图
- 表情过滤引用了不存在的 expression

## 9.2 导出前验证器的 UI 形式
建议在导出前弹出 `Export Validation Report`：

### 结果等级
- 通过
- 警告
- 错误

### 结果分类
- 缺失资源
- 未 remap 资源
- 风险配置
- 自动修复建议

### 可用动作
- 忽略警告继续导出
- 定位到对应图层/面部项
- 自动修正常见路径问题
- 打开相关高级编辑器

## 9.3 导出验证器的价值
它能替代很多原本想通过字段暴露解决的问题，因为：

- 用户不需要理解底层 remap 机制
- 用户只需要知道“这里会坏”
- 系统告诉用户去哪修、怎么修

---

## 10. 其他页签的收敛建议

---

## 10.1 BaseAppearance
保持相对简洁，不建议继续膨胀成“种族编辑器”。

可增强的方向：

- 增加摘要卡片
- 更清晰地展示 hide vanilla 状态
- 提供和图层/面部的关联说明

但不建议引入太多高级底层字段。

---

## 10.2 Attributes
当前 Attributes 更偏：

- 角色画像
- 年龄
- backstory
- stat modifier
- LLM prompt / generate

这个定位基本合理。

优化方向主要是：

- 把数值修改器编辑体验做清晰
- 增加摘要与校验
- 不要把它扩张成完整种族身份配置页

---

## 10.3 Equipment / Weapon
对于装备与武器相关显示逻辑，建议遵循和图层同样的原则：

- 主页面只保留高频可见字段
- 把复杂联动配置移到子编辑器
- 把 runtime 相关结构通过摘要暴露，而不是平铺字段

---

## 11. 具体实现阶段划分

后续建议严格按阶段实施，不要一次性大改。

---

## 阶段 0：建立基线（准备阶段）

### 目标
先整理现有 UI 结构，确认哪些区域最拥挤、哪些字段最少使用。

### 任务
1. 梳理当前属性面板结构
2. 标记现有字段分类：
   - 高频
   - 中频
   - 低频
   - 专家级
3. 梳理现有按钮入口与弹窗能力
4. 列出当前可直接复用的通用 UI 组件

### 产出
- 一份 UI 字段分类表
- 一份页面拥挤点清单
- 一份可复用对话框能力清单

---

## 阶段 1：主属性区瘦身（第一轮优化）

### 目标
在不改变主工作流的情况下，降低右侧面板的信息密度。

### 任务
1. 引入 `Basic / Advanced / Tools` 三段结构
2. 把 Advanced 字段从主区移出
3. 增加摘要行
4. 统一摘要样式
5. 统一“编辑...”按钮样式

### 预期收益
- 主界面立刻变清爽
- 不需要新增大量复杂对话框就能先减压
- 对现有用户影响最小

---

## 阶段 2：复杂能力对话框化（第二轮优化）

### 目标
把低频复杂能力从主界面剥离出去。

### 任务
1. 新增图层材质编辑器
2. 新增图层触发动画编辑器
3. 新增图层变体规则编辑器
4. 新增面部资源编辑器
5. 新增 FullFace 表情映射器
6. 新增 Frame 动画编辑器
7. 新增 Overlay 管理器

### 预期收益
- 主面板压力进一步下降
- 高级能力可达性大幅提升
- 底层能力不再“藏着但难用”

---

## 阶段 3：导入与导出闭环（第三轮优化）

### 目标
提升高级工作流的可用性和稳定性。

### 任务
1. 增强导入报告
2. 增加导入后的推荐操作
3. 新增导出前验证器
4. 补齐高级面部资源的 remap 检查
5. 增加错误定位跳转

### 预期收益
- 高级资源不再“编辑器里能看，导出后坏掉”
- 用户更敢使用 layered face / frame / overlay

---

## 阶段 4：专家模式与长期能力（第四轮优化）

### 目标
给极少数高级创作者保留更深层通道，但不污染主工作流。

### 任务
1. 增加可选“专家模式”
2. 暴露更底层的 runtime / graphicClass 配置入口
3. 增加更强的资源检查与诊断工具
4. 视需要增加新物种脚手架导出能力

### 预期收益
- 高阶扩展性足够
- 普通用户界面不会被专家选项淹没

---

## 12. 优先级规划（P0 / P1 / P2 / P3）

## P0：必须优先做
这些是最先应该实施的：

1. 属性面板分区：Basic / Advanced / Tools
2. 图层属性摘要行
3. Face 页摘要入口
4. 导出前验证器方案设计
5. 自动导入后的结构报告

## P1：第二批做
1. 图层材质编辑器
2. 图层变体规则编辑器
3. 图层触发动画编辑器
4. 面部资源编辑器

## P2：第三批做
1. FullFace 表情映射器
2. Frame 动画编辑器
3. Overlay 管理器
4. 左右部件 / 方向覆盖结构工具

## P3：长期观察
1. 专家模式
2. 更底层 graphicClass/runtime 能力
3. 新物种脚手架导出

---

## 13. 预计涉及文件（实施期参考）

以下为后续实施最可能涉及的源码文件，当前仅作为实施定位参考。

### 主属性区与布局
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Layers.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Panel.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Shared.cs`

### Face 页
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs`

### 导入导出
- `Source/CharacterStudio/UI/Dialog_SkinEditor.ImportExecution.cs`
- `Source/CharacterStudio/UI/Dialog_SkinEditor.ImportApply.cs`
- `Source/CharacterStudio/UI/Dialog_ExportMod.cs`
- `Source/CharacterStudio/Exporter/ModBuilder.cs`

### 运行时与数据模型
- `Source/CharacterStudio/Core/PawnLayerConfig.cs`
- `Source/CharacterStudio/Core/PawnFaceConfig.cs`
- `Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs`

---

## 14. 验收标准

在逐阶段实施后，至少应达到以下标准。

### 14.1 对普通创作者
- 不看高级面板也能完成 80% 创作流
- 主界面滚动长度明显下降
- 图层与面部主页面可读性提高
- 不会被不理解的底层字段打断

### 14.2 对高级创作者
- 原本藏在底层的高级能力可被找到
- 复杂能力有明确入口
- 复杂能力配置后有摘要反馈
- 高级面部资源导出更稳定

### 14.3 对维护者
- 主属性区职责更清晰
- 复杂系统逻辑分离到子编辑器
- 后续新增高级能力时，不需要继续污染主面板
- 导出问题更容易定位

---

## 15. 风险与约束

### 风险 1：一口气新增太多弹窗
若没有统一规范，可能从“主页面过满”变成“弹窗过多”。

**控制方式：**
- 所有弹窗必须先有摘要入口
- 只为“成组复杂系统”创建弹窗
- 能用折叠组解决的，不立即做弹窗

### 风险 2：主页面与高级编辑器重复配置
如果职责不清，会出现同一字段在两个地方都能改，造成混乱。

**控制方式：**
- 主页面只保留高频字段
- 高级编辑器负责成组系统
- 同一能力只能有一个主编辑入口

### 风险 3：导入器与高级手改互相覆盖
自动导入后再手工高级编辑，若结构不清晰，可能导致覆盖问题。

**控制方式：**
- 导入后记录摘要
- 标记自动生成项与手工覆盖项
- 高级编辑器明确显示“手工覆盖状态”

---

## 16. 推荐的第一轮落地路径

如果接下来开始真正实现，建议按照以下顺序做，而不是同时开工：

### 第一步
主属性区瘦身：

- 增加 `Advanced` 折叠区
- 增加摘要行
- 增加统一“编辑...”按钮区

### 第二步
优先做 2 个高价值子编辑器：

- 图层材质编辑器
- 面部资源编辑器

### 第三步
补导入/导出闭环：

- 自动导入报告
- 导出前验证器

### 第四步
再做更深的复杂系统：

- 触发动画编辑器
- Frame 动画编辑器
- Overlay 管理器

---

## 17. 最终结论

当前 CharacterStudio 的问题不是“能力不够”，而是：

> **底层能力已经开始接近专业级，但 UI 仍停留在主流程编辑器形态。**

因此正确方向不是继续给主面板加字段，而是建立如下结构：

1. **主面板保留高频创作字段**
2. **Advanced 折叠承接中频能力**
3. **复杂系统改为摘要 + 子编辑器**
4. **复杂资源依赖自动导入**
5. **导出正确性依赖导出前验证器**

这套方案能同时满足两类用户：

- 普通用户：不被复杂字段压垮
- 高级用户：能真正用到底层能力

并且它为后续继续优化留出了清晰路线，而不会把编辑器推向不可维护的大表单结构。

---

## 18. 后续优化工作单（供下一步实施使用）

### 立即可执行的第一批工作
- [ ] 梳理当前属性面板字段并标注 P0/P1/P2
- [ ] 为 Layers 属性区增加 `Advanced` 折叠组
- [ ] 为 Layers 属性区设计统一摘要行组件
- [ ] 为 Face 页增加结构摘要与高级入口
- [ ] 设计导出前验证器的数据结构与报告格式

### 第二批工作
- [ ] 新建图层材质编辑器
- [ ] 新建图层变体规则编辑器
- [ ] 新建图层触发动画编辑器
- [ ] 新建面部资源编辑器

### 第三批工作
- [ ] 新建 FullFace 表情映射器
- [ ] 新建 Frame 动画编辑器
- [ ] 新建 Overlay 管理器
- [ ] 增强自动导入报告
- [ ] 补齐导出 remap 校验

### 长期工作
- [ ] 评估专家模式是否必要
- [ ] 评估是否需要新物种脚手架导出
- [ ] 评估与其他编辑器能力的边界划分