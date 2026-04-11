# 属性编辑器代码审查
- 日期: 2026-04-08
- 概述: CharacterStudio 属性编辑器模块（Attributes + Stat Buff + UI）的全面代码审查
- 状态: 已完成
- 总体结论: 有条件通过

## 评审范围

# CharacterStudio 属性编辑器模块代码审查

## 审查范围

本次审查覆盖 CharacterStudio 属性编辑器的完整链路：

| 模块 | 文件 | 职责 |
|------|------|------|
| 数据模型 | `Attributes/CharacterStatModifierModels.cs` | Stat Modifier 数据结构、序列化、目录 |
| 数据模型 | `AI/LlmGenerationModels.cs` → `CharacterAttributeProfile` | 角色画像数据 |
| 业务服务 | `Attributes/CharacterAttributeBuffService.cs` | Buff 同步、修正计算、说明文本 |
| Def 定义 | `Attributes/CharacterAttributeBuffDef.cs` | 空壳 HediffDef 子类 |
| Harmony 补丁 | `Patches/Patch_CharacterAttributeBuffStat.cs` | StatWorker Postfix 注入 |
| 编辑器 UI | `UI/Dialog_AttributeBuffEditor.cs` | 独立属性增益编辑窗口 |
| 面板集成 | `UI/Dialog_SkinEditor.Attributes.cs` | 皮肤编辑器属性面板 |
| XML Def | `Defs/HediffDefs/CharacterStudio_AttributeBuff.xml` | Hediff 定义 |
| 本地化 | `Languages/*/Keyed/CS_Keys_Attributes.xml` | i18n 键值 |
| 运行时集成 | `Core/PawnSkinRuntimeUtility.cs` | 皮肤应用/清除时同步 Buff |

## 评审摘要

- 当前状态: 已完成
- 已审模块: Attributes/CharacterStatModifierModels.cs, AI/LlmGenerationModels.cs, Attributes/CharacterAttributeBuffDef.cs, Core/PawnSkinDef.cs, Attributes/CharacterAttributeBuffService.cs, Patches/Patch_CharacterAttributeBuffStat.cs, Defs/HediffDefs/CharacterStudio_AttributeBuff.xml, UI/Dialog_AttributeBuffEditor.cs, UI/Dialog_SkinEditor.Attributes.cs, Languages/English/Keyed/CS_Keys_Attributes.xml, Languages/ChineseSimplified/Keyed/CS_Keys_Attributes.xml, Core/PawnSkinRuntimeUtility.cs, Languages/*/Keyed/CS_Keys_Attributes.xml
- 当前进度: 已记录 3 个里程碑；最新：M3
- 里程碑总数: 3
- 已完成里程碑: 3
- 问题总数: 9
- 问题严重级别分布: 高 1 / 中 3 / 低 5
- 最新结论: ## 总体评价 CharacterStudio 属性编辑器模块整体质量**良好**，架构设计清晰，代码风格统一。该模块成功实现了一个完整的"编辑器 → 数据模型 → 运行时 Buff → Harmony 注入"链路，让用户能够无代码地为角色附加自定义属性增益。 ### 核心亮点 - **三层分离架构**：数据模型、业务服务、UI 层职责分明，耦合度低 - **安全的运行时注入**：通过 Hediff + Harmony Postfix 实现属性修正，不修改原版种族基础值 - **完善的编辑体验**：搜索过滤、分类标签、模式切换、实时预览一应俱全 - **LLM 集成**：异步生成 + Undo 支持 + 错误处理的设计成熟 ### 需要修复的问题（按优先级） 1. **🔴 高 · F-I18N-01**：3 处硬编码中文字符串会导致英语用户看到乱码 — **应立即修复** 2. **🟡 中 · F-PERF-01**：StatWorker Postfix 缺少快速路径，大殖民地下有性能风险 3. **🟡 中 · F-ARCH-01**：CharacterAttributeProfile 放置在 AI 模块中，核心模型位置不当 4. **🟡 中 · F-UI-01**：滚动区域高度硬编码 1560f，内容变化时可能出问题 5. **🟢 低 · 其他 5 项**：迭代器分配、反射脆弱性、DefDatabase 查询、序列化不一致、扩展性限制 ### 量化统计 - 审查文件：11 个 - 发现问题：9 个（高 1 / 中 3 / 低 5） - 模块健康度评估：**B+**（功能完整、设计合理，但有若干 i18n 和性能债务需要清理）
- 下一步建议: 立即修复 F-I18N-01（硬编码中文字符串），然后排期处理 F-PERF-01（StatWorker Postfix 快速路径优化）和 F-ARCH-01（CharacterAttributeProfile 迁移）。
- 总体结论: 有条件通过

## 评审发现

### CharacterAttributeProfile 类放置位置不当

- ID: F-ARCH-01
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  CharacterAttributeProfile 是角色属性画像的核心数据类，被 PawnSkinDef、CharacterDefinition、SkinSaver、ModExportXmlWriter 等多个核心模块引用，但它定义在 `AI/LlmGenerationModels.cs` 中。这意味着核心数据模型对 AI 模块产生了反向依赖，违反了关注点分离原则。
- 建议:

  将 CharacterAttributeProfile 移动到 `Core/` 或 `Attributes/` 目录下独立文件中。AI 模块应引用核心模块的类型，而非反过来。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/AI/LlmGenerationModels.cs:81-130`
  - `CharacterStudio/Source/CharacterStudio/Core/PawnSkinDef.cs:165`
  - `CharacterStudio/Source/CharacterStudio/AI/LlmGenerationModels.cs`
  - `CharacterStudio/Source/CharacterStudio/Core/PawnSkinDef.cs`

### CharacterAttributeProfile 未实现 IExposable

- ID: F-ARCH-02
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  CharacterStatModifierProfile 和 CharacterStatModifierEntry 都正确实现了 IExposable，可以通过 RimWorld 的 Scribe 系统直接序列化。但同为核心模型的 CharacterAttributeProfile 却没有实现此接口，依赖外部的 XML 生成逻辑。这导致序列化风格不一致。
- 建议:

  为 CharacterAttributeProfile 实现 IExposable 接口，使其可以统一通过 Scribe 系统序列化，保持项目内序列化风格一致。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/AI/LlmGenerationModels.cs:81-130`
  - `CharacterStudio/Source/CharacterStudio/AI/LlmGenerationModels.cs`

### CommonStatDefNames 硬编码不可扩展

- ID: F-ARCH-03
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  CharacterStatModifierCatalog.CommonStatDefNames 是一个硬编码的 readonly string[] 数组（30 个预设 StatDef），无法由用户配置或其他 Mod 通过注册机制扩展。虽然当前有 'Add Any Stat' 按钮作为回退，但用户的常用列表无法定制。
- 建议:

  考虑增加一个注册入口（如 static 方法 RegisterCommonStat），或从 XML 配置文件加载常用属性列表，使其可扩展。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Attributes/CharacterStatModifierModels.cs:95-129`
  - `CharacterStudio/Source/CharacterStudio/Attributes/CharacterStatModifierModels.cs`

### StatWorker Postfix 无条件执行开销

- ID: F-PERF-01
- 严重级别: 中
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  Patch_CharacterAttributeBuffStat.Postfix 在每次 StatWorker.GetValue() 调用时都会触发，即使目标 Pawn 没有任何 CS Buff。它需要调用 GetActiveEntries()，后者会获取 CompPawnSkin、访问 ActiveSkin、遍历 entries 列表。在大型殖民地中，这可能导致明显的性能开销。
- 建议:

  在 Postfix 中增加快速路径判断：先检查 Pawn 是否拥有 CompPawnSkin 且其 ActiveSkin 不为 null，如果没有则直接返回。考虑维护一个 HashSet<Pawn> 缓存标记哪些 Pawn 当前有活跃 Buff。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Patches/Patch_CharacterAttributeBuffStat.cs:33-47`
  - `CharacterStudio/Source/CharacterStudio/Patches/Patch_CharacterAttributeBuffStat.cs`

### HasAnyActiveEntry 迭代器分配开销

- ID: F-PERF-02
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  HasAnyActiveEntry 方法通过 foreach 遍历 GetActiveEntries()（一个 yield 迭代器）然后立即返回 true。这会导致每次调用都产生一个 IEnumerator 对象的堆分配。在编辑器内影响不大，但如果 SyncAttributeBuff 被频繁调用则会累积 GC 压力。
- 建议:

  将 HasAnyActiveEntry 改为直接访问 profile.entries 列表进行判断，避免 yield 迭代器分配。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Attributes/CharacterAttributeBuffService.cs:139-147`
  - `CharacterStudio/Source/CharacterStudio/Attributes/CharacterAttributeBuffService.cs`

### StatWorker 反射字段访问存在版本脱节风险

- ID: F-MAINT-01
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  Patch_CharacterAttributeBuffStat 使用 AccessTools.Field(typeof(StatWorker), "stat") 反射获取私有字段。这在 RimWorld 大版本更新时可能因字段名称或类型变更而断裂。当前代码在 StatWorkerStatField == null 时会静默跳过，不会崩溃，但会导致功能静默失效。
- 建议:

  增加启动时的反射字段校验日志，当 StatWorkerStatField 为 null 时输出警告，以便在版本更新后快速定位问题。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Patches/Patch_CharacterAttributeBuffStat.cs:14`
  - `CharacterStudio/Source/CharacterStudio/Patches/Patch_CharacterAttributeBuffStat.cs`

### UI 中存在硬编码中文字符串

- ID: F-I18N-01
- 严重级别: 高
- 分类: 可访问性
- 跟踪状态: 开放
- 相关里程碑: M3
- 说明:

  属性编辑器 UI 中有 3 处硬编码中文字符串未使用翻译键：
  1. `Dialog_AttributeBuffEditor.cs:149` — `"值"` 应使用 `"CS_AttrBuff_Value".Translate()`
  2. `Dialog_SkinEditor.Attributes.cs:163` — `"已配置 {0} 项属性增益，启用 {1} 项"` 缺少翻译键
  3. `Dialog_SkinEditor.Attributes.cs:168` — `"打开属性增益编辑器"` / `"管理属性增益 Buff"` 缺少翻译键

  这会导致英语用户看到无法理解的中文界面文本。
- 建议:

  将这些字符串替换为对应的 Translate() 调用，并在两个语言的 CS_Keys_Attributes.xml 中添加缺少的键值。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_AttributeBuffEditor.cs:149`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs:163-168`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_AttributeBuffEditor.cs`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs`

### 滚动区域高度硬编码

- ID: F-UI-01
- 严重级别: 中
- 分类: CSS
- 跟踪状态: 开放
- 相关里程碑: M3
- 说明:

  DrawAttributesPanel 中 viewRect 高度硬编码为 1560f。当内容增减（如 LLM 区域变化、未来添加新 section）时，这个固定值可能导致过长的空白或内容被截断。
- 建议:

  将 viewRect 高度改为动态计算：先用临时变量累加各 section 的实际高度，然后用累加值作为 viewRect 的总高度。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs:34`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs`

### 列表行内重复 DefDatabase 查询

- ID: F-UI-02
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M3
- 说明:

  DrawEntryList 中每一行都调用 `DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName)` 获取 StatDef 的显示名称。由于编辑器窗口每帧重绘，这些查询会被频繁重复执行。虽然 DefDatabase 内部有 Dictionary 缓存，但仍有不必要的开销。
- 建议:

  在条目创建/更新时缓存 StatDef 引用到 Entry 对象上（使用 [NonSerialized] transient 字段），避免每帧重复查询。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_AttributeBuffEditor.cs:134`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_AttributeBuffEditor.cs`

## 评审里程碑

### M1 · 架构设计与数据模型审查

- 状态: 已完成
- 记录时间: 2026-04-08T22:25:32.189Z
- 已审模块: Attributes/CharacterStatModifierModels.cs, AI/LlmGenerationModels.cs, Attributes/CharacterAttributeBuffDef.cs, Core/PawnSkinDef.cs
- 摘要:

  审查了属性编辑器的整体架构设计、数据模型层、序列化机制以及模块间职责划分。

  **优点**：
  - 清晰的三层分离：数据模型 (`CharacterStatModifierModels`) → 业务服务 (`CharacterAttributeBuffService`) → UI (`Dialog_AttributeBuffEditor`)
  - `CharacterStatModifierEntry` 实现了 `IExposable`，序列化逻辑简洁正确
  - `CharacterStatModifierProfile` 的 `ActiveEntries` 使用 yield 惰性求值，只在真正需要时过滤
  - 双数据模型设计合理：`CharacterAttributeProfile`（画像/描述性数据）与 `CharacterStatModifierProfile`（运行时 Buff 数据）分工明确
  - Clone 方法实现完整、安全地进行深拷贝

  **问题**：
  - `CharacterAttributeProfile` 类定义在 `AI/LlmGenerationModels.cs` 中，放置位置不合理——它是核心数据模型，却寄宿在 AI 模块中
  - `CharacterAttributeProfile` 未实现 `IExposable`，依赖外部 XML 手动序列化；与同级别的 `CharacterStatModifierProfile` 实现不一致
  - `CharacterStatModifierCatalog.CommonStatDefNames` 是硬编码的 string 数组，无法由用户或其他 Mod 扩展
- 结论:

  审查了属性编辑器的整体架构设计、数据模型层、序列化机制以及模块间职责划分。 **优点**： - 清晰的三层分离：数据模型 (`CharacterStatModifierModels`) → 业务服务 (`CharacterAttributeBuffService`) → UI (`Dialog_AttributeBuffEditor`) - `CharacterStatModifierEntry` 实现了 `IExposable`，序列化逻辑简洁正确 - `CharacterStatModifierProfile` 的 `ActiveEntries` 使用 yield 惰性求值，只在真正需要时过滤 - 双数据模型设计合理：`CharacterAttributeProfile`（画像/描述性数据）与 `CharacterStatModifierProfile`（运行时 Buff 数据）分工明确 - Clone 方法实现完整、安全地进行深拷贝 **问题**： - `CharacterAttributeProfile` 类定义在 `AI/LlmGenerationModels.cs` 中，放置位置不合理——它是核心数据模型，却寄宿在 AI 模块中 - `CharacterAttributeProfile` 未实现 `IExposable`，依赖外部 XML 手动序列化；与同级别的 `CharacterStatModifierProfile` 实现不一致 - `CharacterStatModifierCatalog.CommonStatDefNames` 是硬编码的 string 数组，无法由用户或其他 Mod 扩展
- 问题:
  - [中] 可维护性: CharacterAttributeProfile 类放置位置不当
  - [低] 可维护性: CharacterAttributeProfile 未实现 IExposable
  - [低] 可维护性: CommonStatDefNames 硬编码不可扩展

### M2 · 运行时 Buff 服务与 Harmony 补丁审查

- 状态: 已完成
- 记录时间: 2026-04-08T22:26:56.915Z
- 已审模块: Attributes/CharacterAttributeBuffService.cs, Patches/Patch_CharacterAttributeBuffStat.cs, Attributes/CharacterAttributeBuffDef.cs, Defs/HediffDefs/CharacterStudio_AttributeBuff.xml
- 摘要:

  审查了 Buff 服务层（`CharacterAttributeBuffService`）、Harmony 补丁（`Patch_CharacterAttributeBuffStat`）以及 XML Hediff 定义。

  **优点**：
  - `SyncAttributeBuff` 逻辑简洁：有 entry 则确保 Hediff 存在，无 entry 则移除，幂等且安全
  - `ApplyModifiers` 正确区分 Offset（加法）和 Factor（乘法）两种修正模式
  - 使用 `Math.Max(0f, 1f + entry.value)` 防止 Factor 模式将结果翻转为负数
  - `BuildExplanation` 提供友好的 stat 说明文本，可直接嵌入原版 stat tooltip
  - Harmony 补丁通过 `AccessTools.Field` 获取 StatWorker.stat 字段，避免了直接修改原版类
  - Postfix 模式（非 Prefix）确保在原版计算完成后叠加修正，降低冲突风险
  - `GetNamedSilentFail` 的使用避免了 Def 缺失时的硬崩溃

  **问题**：
  - `HasAnyActiveEntry` 使用 foreach + 立即 return 模式遍历 yield 迭代器，虽然逻辑正确但存在迭代器分配开销，在 `SyncAttributeBuff` 被频繁调用时可能成为 GC 压力来源
  - `Patch_CharacterAttributeBuffStat.Postfix` 会在每次 `StatWorker.GetValue()` 调用时触发，即使 Pawn 没有任何 CS Buff，仍需遍历到 `GetActiveEntries` 返回空才能退出
  - `CharacterAttributeBuffDef` 是完全空的 HediffDef 子类，仅起到类型标记作用；其存在的必要性值得商榷
  - `StatWorkerStatField` 使用反射获取 private 字段，在 RimWorld 大版本更新时存在断裂风险
- 结论:

  审查了 Buff 服务层（`CharacterAttributeBuffService`）、Harmony 补丁（`Patch_CharacterAttributeBuffStat`）以及 XML Hediff 定义。 **优点**： - `SyncAttributeBuff` 逻辑简洁：有 entry 则确保 Hediff 存在，无 entry 则移除，幂等且安全 - `ApplyModifiers` 正确区分 Offset（加法）和 Factor（乘法）两种修正模式 - 使用 `Math.Max(0f, 1f + entry.value)` 防止 Factor 模式将结果翻转为负数 - `BuildExplanation` 提供友好的 stat 说明文本，可直接嵌入原版 stat tooltip - Harmony 补丁通过 `AccessTools.Field` 获取 StatWorker.stat 字段，避免了直接修改原版类 - Postfix 模式（非 Prefix）确保在原版计算完成后叠加修正，降低冲突风险 - `GetNamedSilentFail` 的使用避免了 Def 缺失时的硬崩溃 **问题**： - `HasAnyActiveEntry` 使用 foreach + 立即 return 模式遍历 yield 迭代器，虽然逻辑正确但存在迭代器分配开销，在 `SyncAttributeBuff` 被频繁调用时可能成为 GC 压力来源 - `Patch_CharacterAttributeBuffStat.Postfix` 会在每次 `StatWorker.GetValue()` 调用时触发，即使 Pawn 没有任何 CS Buff，仍需遍历到 `GetActiveEntries` 返回空才能退出 - `CharacterAttributeBuffDef` 是完全空的 HediffDef 子类，仅起到类型标记作用；其存在的必要性值得商榷 - `StatWorkerStatField` 使用反射获取 private 字段，在 RimWorld 大版本更新时存在断裂风险
- 问题:
  - [中] 性能: StatWorker Postfix 无条件执行开销
  - [低] 性能: HasAnyActiveEntry 迭代器分配开销
  - [低] 可维护性: StatWorker 反射字段访问存在版本脱节风险

### M3 · 编辑器 UI 与国际化审查

- 状态: 已完成
- 记录时间: 2026-04-08T22:28:03.406Z
- 已审模块: UI/Dialog_AttributeBuffEditor.cs, UI/Dialog_SkinEditor.Attributes.cs, Languages/English/Keyed/CS_Keys_Attributes.xml, Languages/ChineseSimplified/Keyed/CS_Keys_Attributes.xml
- 摘要:

  审查了属性增益编辑器 UI（`Dialog_AttributeBuffEditor`）、皮肤编辑器属性面板（`Dialog_SkinEditor.Attributes.cs`）以及国际化合规性。

  **优点**：
  - `Dialog_AttributeBuffEditor` 是独立的 Window 子类，可以从多个上下文打开，解耦良好
  - UI 使用了统一的 `UIHelper` 风格组件（`DrawToolbarButton`、`DrawSelectionButton`、`DrawDangerButton`、`DrawContentCard`、`DrawAlternatingRowBackground`），视觉一致性好
  - 搜索功能覆盖了 stat label、defName 和 category，搜索体验友好
  - 条目数量和启用数量的摘要显示给予用户清晰的状态感知
  - checkbox 的 isDirty 追踪实现细致，每个变更点都正确标记
  - `ToggleModifierMode` 简洁优雅——单击切换 Offset/Factor，无需下拉菜单
  - LLM 生成区域的异步状态管理（generating/pendingResult/pendingError）设计合理
  - `MutateWithUndo` 包裹 LLM 结果应用，确保可撤销

  **问题**：
  - **硬编码中文字符串**（高严重度）：
    - `Dialog_AttributeBuffEditor.cs:149` — `"值"` 应使用 `"CS_AttrBuff_Value".Translate()`
    - `Dialog_SkinEditor.Attributes.cs:163` — `"已配置 {0} 项属性增益，启用 {1} 项"` 应使用翻译键
    - `Dialog_SkinEditor.Attributes.cs:168` — `"打开属性增益编辑器"` 和 `"管理属性增益 Buff"` 应使用翻译键
  - `DrawAttributesPanel` 中 `viewRect` 高度硬编码为 `1560f`，当内容增减时可能导致过长的空白或内容被截断
  - `DrawEntryList` 中每一行调用 `DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName)` 进行 DefDatabase 查询，在列表滚动时每帧重复查询，存在效率问题
  - 删除条目时使用 `RemoveAt(i) + break` 模式，虽然逻辑正确但意味着每帧只能删除一项——如果用户快速操作可能感觉响应迟钝
  - 数值编辑使用 `TextFieldNumeric` 但范围为 -100 到 100，对某些 stat（如 CarryingCapacity）可能过于限制
- 结论:

  审查了属性增益编辑器 UI（`Dialog_AttributeBuffEditor`）、皮肤编辑器属性面板（`Dialog_SkinEditor.Attributes.cs`）以及国际化合规性。 **优点**： - `Dialog_AttributeBuffEditor` 是独立的 Window 子类，可以从多个上下文打开，解耦良好 - UI 使用了统一的 `UIHelper` 风格组件（`DrawToolbarButton`、`DrawSelectionButton`、`DrawDangerButton`、`DrawContentCard`、`DrawAlternatingRowBackground`），视觉一致性好 - 搜索功能覆盖了 stat label、defName 和 category，搜索体验友好 - 条目数量和启用数量的摘要显示给予用户清晰的状态感知 - checkbox 的 isDirty 追踪实现细致，每个变更点都正确标记 - `ToggleModifierMode` 简洁优雅——单击切换 Offset/Factor，无需下拉菜单 - LLM 生成区域的异步状态管理（generating/pendingResult/pendingError）设计合理 - `MutateWithUndo` 包裹 LLM 结果应用，确保可撤销 **问题**： - **硬编码中文字符串**（高严重度）： - `Dialog_AttributeBuffEditor.cs:149` — `"值"` 应使用 `"CS_AttrBuff_Value".Translate()` - `Dialog_SkinEditor.Attributes.cs:163` — `"已配置 {0} 项属性增益，启用 {1} 项"` 应使用翻译键 - `Dialog_SkinEditor.Attributes.cs:168` — `"打开属性增益编辑器"` 和 `"管理属性增益 Buff"` 应使用翻译键 - `DrawAttributesPanel` 中 `viewRect` 高度硬编码为 `1560f`，当内容增减时可能导致过长的空白或内容被截断 - `DrawEntryList` 中每一行调用 `DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName)` 进行 DefDatabase 查询，在列表滚动时每帧重复查询，存在效率问题 - 删除条目时使用 `RemoveAt(i) + break` 模式，虽然逻辑正确但意味着每帧只能删除一项——如果用户快速操作可能感觉响应迟钝 - 数值编辑使用 `TextFieldNumeric` 但范围为 -100 到 100，对某些 stat（如 CarryingCapacity）可能过于限制
- 问题:
  - [高] 可访问性: UI 中存在硬编码中文字符串
  - [中] CSS: 滚动区域高度硬编码
  - [低] 性能: 列表行内重复 DefDatabase 查询

## 最终结论

## 总体评价

CharacterStudio 属性编辑器模块整体质量**良好**，架构设计清晰，代码风格统一。该模块成功实现了一个完整的"编辑器 → 数据模型 → 运行时 Buff → Harmony 注入"链路，让用户能够无代码地为角色附加自定义属性增益。

### 核心亮点
- **三层分离架构**：数据模型、业务服务、UI 层职责分明，耦合度低
- **安全的运行时注入**：通过 Hediff + Harmony Postfix 实现属性修正，不修改原版种族基础值
- **完善的编辑体验**：搜索过滤、分类标签、模式切换、实时预览一应俱全
- **LLM 集成**：异步生成 + Undo 支持 + 错误处理的设计成熟

### 需要修复的问题（按优先级）
1. **🔴 高 · F-I18N-01**：3 处硬编码中文字符串会导致英语用户看到乱码 — **应立即修复**
2. **🟡 中 · F-PERF-01**：StatWorker Postfix 缺少快速路径，大殖民地下有性能风险
3. **🟡 中 · F-ARCH-01**：CharacterAttributeProfile 放置在 AI 模块中，核心模型位置不当
4. **🟡 中 · F-UI-01**：滚动区域高度硬编码 1560f，内容变化时可能出问题
5. **🟢 低 · 其他 5 项**：迭代器分配、反射脆弱性、DefDatabase 查询、序列化不一致、扩展性限制

### 量化统计
- 审查文件：11 个
- 发现问题：9 个（高 1 / 中 3 / 低 5）
- 模块健康度评估：**B+**（功能完整、设计合理，但有若干 i18n 和性能债务需要清理）

## 评审快照

```json
{
  "formatVersion": 4,
  "kind": "limcode.review",
  "reviewRunId": "review-mnqm82ft-8eenix",
  "createdAt": "2026-04-08T00:00:00.000Z",
  "updatedAt": "2026-04-08T22:28:30.782Z",
  "finalizedAt": "2026-04-08T22:28:30.782Z",
  "status": "completed",
  "overallDecision": "conditionally_accepted",
  "header": {
    "title": "属性编辑器代码审查",
    "date": "2026-04-08",
    "overview": "CharacterStudio 属性编辑器模块（Attributes + Stat Buff + UI）的全面代码审查"
  },
  "scope": {
    "markdown": "# CharacterStudio 属性编辑器模块代码审查\n\n## 审查范围\n\n本次审查覆盖 CharacterStudio 属性编辑器的完整链路：\n\n| 模块 | 文件 | 职责 |\n|------|------|------|\n| 数据模型 | `Attributes/CharacterStatModifierModels.cs` | Stat Modifier 数据结构、序列化、目录 |\n| 数据模型 | `AI/LlmGenerationModels.cs` → `CharacterAttributeProfile` | 角色画像数据 |\n| 业务服务 | `Attributes/CharacterAttributeBuffService.cs` | Buff 同步、修正计算、说明文本 |\n| Def 定义 | `Attributes/CharacterAttributeBuffDef.cs` | 空壳 HediffDef 子类 |\n| Harmony 补丁 | `Patches/Patch_CharacterAttributeBuffStat.cs` | StatWorker Postfix 注入 |\n| 编辑器 UI | `UI/Dialog_AttributeBuffEditor.cs` | 独立属性增益编辑窗口 |\n| 面板集成 | `UI/Dialog_SkinEditor.Attributes.cs` | 皮肤编辑器属性面板 |\n| XML Def | `Defs/HediffDefs/CharacterStudio_AttributeBuff.xml` | Hediff 定义 |\n| 本地化 | `Languages/*/Keyed/CS_Keys_Attributes.xml` | i18n 键值 |\n| 运行时集成 | `Core/PawnSkinRuntimeUtility.cs` | 皮肤应用/清除时同步 Buff |"
  },
  "summary": {
    "latestConclusion": "## 总体评价\n\nCharacterStudio 属性编辑器模块整体质量**良好**，架构设计清晰，代码风格统一。该模块成功实现了一个完整的\"编辑器 → 数据模型 → 运行时 Buff → Harmony 注入\"链路，让用户能够无代码地为角色附加自定义属性增益。\n\n### 核心亮点\n- **三层分离架构**：数据模型、业务服务、UI 层职责分明，耦合度低\n- **安全的运行时注入**：通过 Hediff + Harmony Postfix 实现属性修正，不修改原版种族基础值\n- **完善的编辑体验**：搜索过滤、分类标签、模式切换、实时预览一应俱全\n- **LLM 集成**：异步生成 + Undo 支持 + 错误处理的设计成熟\n\n### 需要修复的问题（按优先级）\n1. **🔴 高 · F-I18N-01**：3 处硬编码中文字符串会导致英语用户看到乱码 — **应立即修复**\n2. **🟡 中 · F-PERF-01**：StatWorker Postfix 缺少快速路径，大殖民地下有性能风险\n3. **🟡 中 · F-ARCH-01**：CharacterAttributeProfile 放置在 AI 模块中，核心模型位置不当\n4. **🟡 中 · F-UI-01**：滚动区域高度硬编码 1560f，内容变化时可能出问题\n5. **🟢 低 · 其他 5 项**：迭代器分配、反射脆弱性、DefDatabase 查询、序列化不一致、扩展性限制\n\n### 量化统计\n- 审查文件：11 个\n- 发现问题：9 个（高 1 / 中 3 / 低 5）\n- 模块健康度评估：**B+**（功能完整、设计合理，但有若干 i18n 和性能债务需要清理）",
    "recommendedNextAction": "立即修复 F-I18N-01（硬编码中文字符串），然后排期处理 F-PERF-01（StatWorker Postfix 快速路径优化）和 F-ARCH-01（CharacterAttributeProfile 迁移）。",
    "reviewedModules": [
      "Attributes/CharacterStatModifierModels.cs",
      "AI/LlmGenerationModels.cs",
      "Attributes/CharacterAttributeBuffDef.cs",
      "Core/PawnSkinDef.cs",
      "Attributes/CharacterAttributeBuffService.cs",
      "Patches/Patch_CharacterAttributeBuffStat.cs",
      "Defs/HediffDefs/CharacterStudio_AttributeBuff.xml",
      "UI/Dialog_AttributeBuffEditor.cs",
      "UI/Dialog_SkinEditor.Attributes.cs",
      "Languages/English/Keyed/CS_Keys_Attributes.xml",
      "Languages/ChineseSimplified/Keyed/CS_Keys_Attributes.xml",
      "Core/PawnSkinRuntimeUtility.cs",
      "Languages/*/Keyed/CS_Keys_Attributes.xml"
    ]
  },
  "stats": {
    "totalMilestones": 3,
    "completedMilestones": 3,
    "totalFindings": 9,
    "severity": {
      "high": 1,
      "medium": 3,
      "low": 5
    }
  },
  "milestones": [
    {
      "id": "M1",
      "title": "架构设计与数据模型审查",
      "status": "completed",
      "recordedAt": "2026-04-08T22:25:32.189Z",
      "summaryMarkdown": "审查了属性编辑器的整体架构设计、数据模型层、序列化机制以及模块间职责划分。\n\n**优点**：\n- 清晰的三层分离：数据模型 (`CharacterStatModifierModels`) → 业务服务 (`CharacterAttributeBuffService`) → UI (`Dialog_AttributeBuffEditor`)\n- `CharacterStatModifierEntry` 实现了 `IExposable`，序列化逻辑简洁正确\n- `CharacterStatModifierProfile` 的 `ActiveEntries` 使用 yield 惰性求值，只在真正需要时过滤\n- 双数据模型设计合理：`CharacterAttributeProfile`（画像/描述性数据）与 `CharacterStatModifierProfile`（运行时 Buff 数据）分工明确\n- Clone 方法实现完整、安全地进行深拷贝\n\n**问题**：\n- `CharacterAttributeProfile` 类定义在 `AI/LlmGenerationModels.cs` 中，放置位置不合理——它是核心数据模型，却寄宿在 AI 模块中\n- `CharacterAttributeProfile` 未实现 `IExposable`，依赖外部 XML 手动序列化；与同级别的 `CharacterStatModifierProfile` 实现不一致\n- `CharacterStatModifierCatalog.CommonStatDefNames` 是硬编码的 string 数组，无法由用户或其他 Mod 扩展",
      "conclusionMarkdown": "审查了属性编辑器的整体架构设计、数据模型层、序列化机制以及模块间职责划分。 **优点**： - 清晰的三层分离：数据模型 (`CharacterStatModifierModels`) → 业务服务 (`CharacterAttributeBuffService`) → UI (`Dialog_AttributeBuffEditor`) - `CharacterStatModifierEntry` 实现了 `IExposable`，序列化逻辑简洁正确 - `CharacterStatModifierProfile` 的 `ActiveEntries` 使用 yield 惰性求值，只在真正需要时过滤 - 双数据模型设计合理：`CharacterAttributeProfile`（画像/描述性数据）与 `CharacterStatModifierProfile`（运行时 Buff 数据）分工明确 - Clone 方法实现完整、安全地进行深拷贝 **问题**： - `CharacterAttributeProfile` 类定义在 `AI/LlmGenerationModels.cs` 中，放置位置不合理——它是核心数据模型，却寄宿在 AI 模块中 - `CharacterAttributeProfile` 未实现 `IExposable`，依赖外部 XML 手动序列化；与同级别的 `CharacterStatModifierProfile` 实现不一致 - `CharacterStatModifierCatalog.CommonStatDefNames` 是硬编码的 string 数组，无法由用户或其他 Mod 扩展",
      "evidence": [],
      "reviewedModules": [
        "Attributes/CharacterStatModifierModels.cs",
        "AI/LlmGenerationModels.cs",
        "Attributes/CharacterAttributeBuffDef.cs",
        "Core/PawnSkinDef.cs"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "F-ARCH-01",
        "F-ARCH-02",
        "F-ARCH-03"
      ]
    },
    {
      "id": "M2",
      "title": "运行时 Buff 服务与 Harmony 补丁审查",
      "status": "completed",
      "recordedAt": "2026-04-08T22:26:56.915Z",
      "summaryMarkdown": "审查了 Buff 服务层（`CharacterAttributeBuffService`）、Harmony 补丁（`Patch_CharacterAttributeBuffStat`）以及 XML Hediff 定义。\n\n**优点**：\n- `SyncAttributeBuff` 逻辑简洁：有 entry 则确保 Hediff 存在，无 entry 则移除，幂等且安全\n- `ApplyModifiers` 正确区分 Offset（加法）和 Factor（乘法）两种修正模式\n- 使用 `Math.Max(0f, 1f + entry.value)` 防止 Factor 模式将结果翻转为负数\n- `BuildExplanation` 提供友好的 stat 说明文本，可直接嵌入原版 stat tooltip\n- Harmony 补丁通过 `AccessTools.Field` 获取 StatWorker.stat 字段，避免了直接修改原版类\n- Postfix 模式（非 Prefix）确保在原版计算完成后叠加修正，降低冲突风险\n- `GetNamedSilentFail` 的使用避免了 Def 缺失时的硬崩溃\n\n**问题**：\n- `HasAnyActiveEntry` 使用 foreach + 立即 return 模式遍历 yield 迭代器，虽然逻辑正确但存在迭代器分配开销，在 `SyncAttributeBuff` 被频繁调用时可能成为 GC 压力来源\n- `Patch_CharacterAttributeBuffStat.Postfix` 会在每次 `StatWorker.GetValue()` 调用时触发，即使 Pawn 没有任何 CS Buff，仍需遍历到 `GetActiveEntries` 返回空才能退出\n- `CharacterAttributeBuffDef` 是完全空的 HediffDef 子类，仅起到类型标记作用；其存在的必要性值得商榷\n- `StatWorkerStatField` 使用反射获取 private 字段，在 RimWorld 大版本更新时存在断裂风险",
      "conclusionMarkdown": "审查了 Buff 服务层（`CharacterAttributeBuffService`）、Harmony 补丁（`Patch_CharacterAttributeBuffStat`）以及 XML Hediff 定义。 **优点**： - `SyncAttributeBuff` 逻辑简洁：有 entry 则确保 Hediff 存在，无 entry 则移除，幂等且安全 - `ApplyModifiers` 正确区分 Offset（加法）和 Factor（乘法）两种修正模式 - 使用 `Math.Max(0f, 1f + entry.value)` 防止 Factor 模式将结果翻转为负数 - `BuildExplanation` 提供友好的 stat 说明文本，可直接嵌入原版 stat tooltip - Harmony 补丁通过 `AccessTools.Field` 获取 StatWorker.stat 字段，避免了直接修改原版类 - Postfix 模式（非 Prefix）确保在原版计算完成后叠加修正，降低冲突风险 - `GetNamedSilentFail` 的使用避免了 Def 缺失时的硬崩溃 **问题**： - `HasAnyActiveEntry` 使用 foreach + 立即 return 模式遍历 yield 迭代器，虽然逻辑正确但存在迭代器分配开销，在 `SyncAttributeBuff` 被频繁调用时可能成为 GC 压力来源 - `Patch_CharacterAttributeBuffStat.Postfix` 会在每次 `StatWorker.GetValue()` 调用时触发，即使 Pawn 没有任何 CS Buff，仍需遍历到 `GetActiveEntries` 返回空才能退出 - `CharacterAttributeBuffDef` 是完全空的 HediffDef 子类，仅起到类型标记作用；其存在的必要性值得商榷 - `StatWorkerStatField` 使用反射获取 private 字段，在 RimWorld 大版本更新时存在断裂风险",
      "evidence": [],
      "reviewedModules": [
        "Attributes/CharacterAttributeBuffService.cs",
        "Patches/Patch_CharacterAttributeBuffStat.cs",
        "Attributes/CharacterAttributeBuffDef.cs",
        "Defs/HediffDefs/CharacterStudio_AttributeBuff.xml"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "F-PERF-01",
        "F-PERF-02",
        "F-MAINT-01"
      ]
    },
    {
      "id": "M3",
      "title": "编辑器 UI 与国际化审查",
      "status": "completed",
      "recordedAt": "2026-04-08T22:28:03.406Z",
      "summaryMarkdown": "审查了属性增益编辑器 UI（`Dialog_AttributeBuffEditor`）、皮肤编辑器属性面板（`Dialog_SkinEditor.Attributes.cs`）以及国际化合规性。\n\n**优点**：\n- `Dialog_AttributeBuffEditor` 是独立的 Window 子类，可以从多个上下文打开，解耦良好\n- UI 使用了统一的 `UIHelper` 风格组件（`DrawToolbarButton`、`DrawSelectionButton`、`DrawDangerButton`、`DrawContentCard`、`DrawAlternatingRowBackground`），视觉一致性好\n- 搜索功能覆盖了 stat label、defName 和 category，搜索体验友好\n- 条目数量和启用数量的摘要显示给予用户清晰的状态感知\n- checkbox 的 isDirty 追踪实现细致，每个变更点都正确标记\n- `ToggleModifierMode` 简洁优雅——单击切换 Offset/Factor，无需下拉菜单\n- LLM 生成区域的异步状态管理（generating/pendingResult/pendingError）设计合理\n- `MutateWithUndo` 包裹 LLM 结果应用，确保可撤销\n\n**问题**：\n- **硬编码中文字符串**（高严重度）：\n  - `Dialog_AttributeBuffEditor.cs:149` — `\"值\"` 应使用 `\"CS_AttrBuff_Value\".Translate()`\n  - `Dialog_SkinEditor.Attributes.cs:163` — `\"已配置 {0} 项属性增益，启用 {1} 项\"` 应使用翻译键\n  - `Dialog_SkinEditor.Attributes.cs:168` — `\"打开属性增益编辑器\"` 和 `\"管理属性增益 Buff\"` 应使用翻译键\n- `DrawAttributesPanel` 中 `viewRect` 高度硬编码为 `1560f`，当内容增减时可能导致过长的空白或内容被截断\n- `DrawEntryList` 中每一行调用 `DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName)` 进行 DefDatabase 查询，在列表滚动时每帧重复查询，存在效率问题\n- 删除条目时使用 `RemoveAt(i) + break` 模式，虽然逻辑正确但意味着每帧只能删除一项——如果用户快速操作可能感觉响应迟钝\n- 数值编辑使用 `TextFieldNumeric` 但范围为 -100 到 100，对某些 stat（如 CarryingCapacity）可能过于限制",
      "conclusionMarkdown": "审查了属性增益编辑器 UI（`Dialog_AttributeBuffEditor`）、皮肤编辑器属性面板（`Dialog_SkinEditor.Attributes.cs`）以及国际化合规性。 **优点**： - `Dialog_AttributeBuffEditor` 是独立的 Window 子类，可以从多个上下文打开，解耦良好 - UI 使用了统一的 `UIHelper` 风格组件（`DrawToolbarButton`、`DrawSelectionButton`、`DrawDangerButton`、`DrawContentCard`、`DrawAlternatingRowBackground`），视觉一致性好 - 搜索功能覆盖了 stat label、defName 和 category，搜索体验友好 - 条目数量和启用数量的摘要显示给予用户清晰的状态感知 - checkbox 的 isDirty 追踪实现细致，每个变更点都正确标记 - `ToggleModifierMode` 简洁优雅——单击切换 Offset/Factor，无需下拉菜单 - LLM 生成区域的异步状态管理（generating/pendingResult/pendingError）设计合理 - `MutateWithUndo` 包裹 LLM 结果应用，确保可撤销 **问题**： - **硬编码中文字符串**（高严重度）： - `Dialog_AttributeBuffEditor.cs:149` — `\"值\"` 应使用 `\"CS_AttrBuff_Value\".Translate()` - `Dialog_SkinEditor.Attributes.cs:163` — `\"已配置 {0} 项属性增益，启用 {1} 项\"` 应使用翻译键 - `Dialog_SkinEditor.Attributes.cs:168` — `\"打开属性增益编辑器\"` 和 `\"管理属性增益 Buff\"` 应使用翻译键 - `DrawAttributesPanel` 中 `viewRect` 高度硬编码为 `1560f`，当内容增减时可能导致过长的空白或内容被截断 - `DrawEntryList` 中每一行调用 `DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName)` 进行 DefDatabase 查询，在列表滚动时每帧重复查询，存在效率问题 - 删除条目时使用 `RemoveAt(i) + break` 模式，虽然逻辑正确但意味着每帧只能删除一项——如果用户快速操作可能感觉响应迟钝 - 数值编辑使用 `TextFieldNumeric` 但范围为 -100 到 100，对某些 stat（如 CarryingCapacity）可能过于限制",
      "evidence": [],
      "reviewedModules": [
        "UI/Dialog_AttributeBuffEditor.cs",
        "UI/Dialog_SkinEditor.Attributes.cs",
        "Languages/English/Keyed/CS_Keys_Attributes.xml",
        "Languages/ChineseSimplified/Keyed/CS_Keys_Attributes.xml"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "F-I18N-01",
        "F-UI-01",
        "F-UI-02"
      ]
    }
  ],
  "findings": [
    {
      "id": "F-ARCH-01",
      "severity": "medium",
      "category": "maintainability",
      "title": "CharacterAttributeProfile 类放置位置不当",
      "descriptionMarkdown": "CharacterAttributeProfile 是角色属性画像的核心数据类，被 PawnSkinDef、CharacterDefinition、SkinSaver、ModExportXmlWriter 等多个核心模块引用，但它定义在 `AI/LlmGenerationModels.cs` 中。这意味着核心数据模型对 AI 模块产生了反向依赖，违反了关注点分离原则。",
      "recommendationMarkdown": "将 CharacterAttributeProfile 移动到 `Core/` 或 `Attributes/` 目录下独立文件中。AI 模块应引用核心模块的类型，而非反过来。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/AI/LlmGenerationModels.cs",
          "lineStart": 81,
          "lineEnd": 130
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/PawnSkinDef.cs",
          "lineStart": 165,
          "lineEnd": 165
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/AI/LlmGenerationModels.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/PawnSkinDef.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ARCH-02",
      "severity": "low",
      "category": "maintainability",
      "title": "CharacterAttributeProfile 未实现 IExposable",
      "descriptionMarkdown": "CharacterStatModifierProfile 和 CharacterStatModifierEntry 都正确实现了 IExposable，可以通过 RimWorld 的 Scribe 系统直接序列化。但同为核心模型的 CharacterAttributeProfile 却没有实现此接口，依赖外部的 XML 生成逻辑。这导致序列化风格不一致。",
      "recommendationMarkdown": "为 CharacterAttributeProfile 实现 IExposable 接口，使其可以统一通过 Scribe 系统序列化，保持项目内序列化风格一致。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/AI/LlmGenerationModels.cs",
          "lineStart": 81,
          "lineEnd": 130
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/AI/LlmGenerationModels.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ARCH-03",
      "severity": "low",
      "category": "maintainability",
      "title": "CommonStatDefNames 硬编码不可扩展",
      "descriptionMarkdown": "CharacterStatModifierCatalog.CommonStatDefNames 是一个硬编码的 readonly string[] 数组（30 个预设 StatDef），无法由用户配置或其他 Mod 通过注册机制扩展。虽然当前有 'Add Any Stat' 按钮作为回退，但用户的常用列表无法定制。",
      "recommendationMarkdown": "考虑增加一个注册入口（如 static 方法 RegisterCommonStat），或从 XML 配置文件加载常用属性列表，使其可扩展。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Attributes/CharacterStatModifierModels.cs",
          "lineStart": 95,
          "lineEnd": 129
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Attributes/CharacterStatModifierModels.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-PERF-01",
      "severity": "medium",
      "category": "performance",
      "title": "StatWorker Postfix 无条件执行开销",
      "descriptionMarkdown": "Patch_CharacterAttributeBuffStat.Postfix 在每次 StatWorker.GetValue() 调用时都会触发，即使目标 Pawn 没有任何 CS Buff。它需要调用 GetActiveEntries()，后者会获取 CompPawnSkin、访问 ActiveSkin、遍历 entries 列表。在大型殖民地中，这可能导致明显的性能开销。",
      "recommendationMarkdown": "在 Postfix 中增加快速路径判断：先检查 Pawn 是否拥有 CompPawnSkin 且其 ActiveSkin 不为 null，如果没有则直接返回。考虑维护一个 HashSet<Pawn> 缓存标记哪些 Pawn 当前有活跃 Buff。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Patches/Patch_CharacterAttributeBuffStat.cs",
          "lineStart": 33,
          "lineEnd": 47
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Patches/Patch_CharacterAttributeBuffStat.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-PERF-02",
      "severity": "low",
      "category": "performance",
      "title": "HasAnyActiveEntry 迭代器分配开销",
      "descriptionMarkdown": "HasAnyActiveEntry 方法通过 foreach 遍历 GetActiveEntries()（一个 yield 迭代器）然后立即返回 true。这会导致每次调用都产生一个 IEnumerator 对象的堆分配。在编辑器内影响不大，但如果 SyncAttributeBuff 被频繁调用则会累积 GC 压力。",
      "recommendationMarkdown": "将 HasAnyActiveEntry 改为直接访问 profile.entries 列表进行判断，避免 yield 迭代器分配。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Attributes/CharacterAttributeBuffService.cs",
          "lineStart": 139,
          "lineEnd": 147
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Attributes/CharacterAttributeBuffService.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-MAINT-01",
      "severity": "low",
      "category": "maintainability",
      "title": "StatWorker 反射字段访问存在版本脱节风险",
      "descriptionMarkdown": "Patch_CharacterAttributeBuffStat 使用 AccessTools.Field(typeof(StatWorker), \"stat\") 反射获取私有字段。这在 RimWorld 大版本更新时可能因字段名称或类型变更而断裂。当前代码在 StatWorkerStatField == null 时会静默跳过，不会崩溃，但会导致功能静默失效。",
      "recommendationMarkdown": "增加启动时的反射字段校验日志，当 StatWorkerStatField 为 null 时输出警告，以便在版本更新后快速定位问题。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Patches/Patch_CharacterAttributeBuffStat.cs",
          "lineStart": 14,
          "lineEnd": 14
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Patches/Patch_CharacterAttributeBuffStat.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-I18N-01",
      "severity": "high",
      "category": "accessibility",
      "title": "UI 中存在硬编码中文字符串",
      "descriptionMarkdown": "属性编辑器 UI 中有 3 处硬编码中文字符串未使用翻译键：\n1. `Dialog_AttributeBuffEditor.cs:149` — `\"值\"` 应使用 `\"CS_AttrBuff_Value\".Translate()`\n2. `Dialog_SkinEditor.Attributes.cs:163` — `\"已配置 {0} 项属性增益，启用 {1} 项\"` 缺少翻译键\n3. `Dialog_SkinEditor.Attributes.cs:168` — `\"打开属性增益编辑器\"` / `\"管理属性增益 Buff\"` 缺少翻译键\n\n这会导致英语用户看到无法理解的中文界面文本。",
      "recommendationMarkdown": "将这些字符串替换为对应的 Translate() 调用，并在两个语言的 CS_Keys_Attributes.xml 中添加缺少的键值。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_AttributeBuffEditor.cs",
          "lineStart": 149,
          "lineEnd": 149
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs",
          "lineStart": 163,
          "lineEnd": 168
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_AttributeBuffEditor.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M3"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-01",
      "severity": "medium",
      "category": "css",
      "title": "滚动区域高度硬编码",
      "descriptionMarkdown": "DrawAttributesPanel 中 viewRect 高度硬编码为 1560f。当内容增减（如 LLM 区域变化、未来添加新 section）时，这个固定值可能导致过长的空白或内容被截断。",
      "recommendationMarkdown": "将 viewRect 高度改为动态计算：先用临时变量累加各 section 的实际高度，然后用累加值作为 viewRect 的总高度。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs",
          "lineStart": 34,
          "lineEnd": 34
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M3"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-02",
      "severity": "low",
      "category": "performance",
      "title": "列表行内重复 DefDatabase 查询",
      "descriptionMarkdown": "DrawEntryList 中每一行都调用 `DefDatabase<StatDef>.GetNamedSilentFail(entry.statDefName)` 获取 StatDef 的显示名称。由于编辑器窗口每帧重绘，这些查询会被频繁重复执行。虽然 DefDatabase 内部有 Dictionary 缓存，但仍有不必要的开销。",
      "recommendationMarkdown": "在条目创建/更新时缓存 StatDef 引用到 Entry 对象上（使用 [NonSerialized] transient 字段），避免每帧重复查询。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_AttributeBuffEditor.cs",
          "lineStart": 134,
          "lineEnd": 134
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_AttributeBuffEditor.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M3"
      ],
      "trackingStatus": "open"
    }
  ],
  "render": {
    "rendererVersion": 4,
    "bodyHash": "sha256:d60c1c0270191404d5f77702b8ee9a23451e405adaec283c43d704f3ceb769df",
    "generatedAt": "2026-04-08T22:28:30.782Z",
    "locale": "zh-CN"
  }
}
```
