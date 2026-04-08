# CharacterStudio 技能编辑器模块 Code Review
- 日期: 2026-04-08
- 概述: 对 CharacterStudio 技能编辑器（Abilities + UI）的全面代码审查，涵盖架构设计、代码质量、性能、可维护性等维度。
- 状态: 进行中
- 总体结论: 待定

## 评审范围

# CharacterStudio 技能编辑器模块 Code Review

## 审查范围

- `Source/CharacterStudio/Abilities/` 目录下全部 12 个文件
- `Source/CharacterStudio/UI/Dialog_AbilityEditor*.cs` 系列 14 个 partial class 文件
- `Source/CharacterStudio/UI/AbilityEditorExtensionRegistry.cs`
- `Defs/AbilityExampleDefs/`

## 总体评价

技能编辑器模块实现了一套 **非常完备的模块化技能系统**，从编辑器 UI 到运行时执行链路、从视觉特效到热键系统，功能覆盖面广且设计目标清晰。代码有良好的注释和文档意识。但由于功能持续堆叠，部分区域出现了明显的 **技术债务**，需要有针对性地治理。

## 评审摘要

- 当前状态: 进行中
- 已审模块: Abilities/ModularAbilityDef.cs, Abilities/CompAbilityEffect_Modular.cs, Abilities/EffectWorkers.cs, Abilities/VisualEffectWorker.cs, Abilities/AbilityAreaUtility.cs, Abilities/AbilityGrantUtility.cs, Abilities/AbilityHotkeyRuntimeComponent.cs, Abilities/AbilityLoadoutRuntimeUtility.cs, Abilities/AbilityTimeStopRuntimeController.cs, Abilities/AbilityVanillaFlightUtility.cs, UI/Dialog_AbilityEditor.cs, UI/Dialog_AbilityEditor.Labels.cs, UI/AbilityEditorExtensionRegistry.cs
- 当前进度: 已记录 2 个里程碑；最新：M2
- 里程碑总数: 2
- 已完成里程碑: 2
- 问题总数: 12
- 问题严重级别分布: 高 2 / 中 6 / 低 4
- 最新结论: 审查了运行时性能热点、内存分配模式、并发安全性、工厂模式效率、序列化健壮性等方面的问题。同时覆盖了编辑器 UI 层的 Dialog_AbilityEditor partial class 体系和扩展注册表设计。
- 下一步建议: 待定
- 总体结论: 待定

## 评审发现

### AbilityRuntimeComponentConfig 上帝对象

- ID: F-ARCH-01
- 严重级别: 高
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  AbilityRuntimeComponentConfig 类包含超过 80 个公共字段（第216-326行），所有 30 种运行时组件类型的参数都平铺在同一个类中。这导致：1) 大量无关字段对每个具体类型而言是噪音；2) Validate() 方法已膨胀到近 200 行的 switch 语句；3) NormalizeForSave() 同样需要逐一处理 80+ 字段；4) 新增运行时组件类型需要修改多处代码。这是典型的 God Object 反模式。
- 建议:

  引入继承体系或 composition 模式：创建 AbilityRuntimeComponentConfigBase 基类，为每种组件类型定义专用子配置类。或使用 Dictionary<string, object> 存储类型特定参数 + 按类型的 IComponentValidator / IComponentNormalizer 接口。短期可先将参数按功能分组为嵌套结构体。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/ModularAbilityDef.cs:216-611`
  - `CharacterStudio/Source/CharacterStudio/Abilities/ModularAbilityDef.cs`

### 热键槽位 switch/case 重复代码泛滥

- ID: F-ARCH-02
- 严重级别: 高
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  整个技能系统中，对 Q/W/E/R/T/A/S/D/F/Z/X/C/V 13 个热键槽位的处理通过大量几乎完全相同的 switch/case 语句实现。仅在 CompAbilityEffect_Modular.cs 中就有 ApplyHotkeyOverride（13分支）、ApplyFollowupCooldownGate（13分支）、GetSlotCooldownUntilTick（13分支）、SetSlotCooldownUntilTick（13分支）四处。在 AbilityHotkeyRuntimeComponent.cs 中还有 GetSlotCooldownUntil、SetSlotCooldown、GetSlotOverrideState、ClearSlotOverrideState 等多处。AbilityLoadoutRuntimeUtility.cs 中同样如此。保守估计这些重复 switch 代码超过 1000 行。
- 建议:

  将 CompPawnSkin 中的 13 个独立字段（qCooldownUntilTick, wCooldownUntilTick, ...）重构为 Dictionary<string, SlotState> 或数组索引结构。一个 SlotState 结构体包含 cooldownUntilTick、overrideAbilityDefName、overrideExpireTick 等字段。所有 switch 都可简化为一次字典/数组查找。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs:467-531`
  - `CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs:1263-1327`
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs:154-195`
  - `CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs`
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs`

### AbilityAreaUtility 与 AbilityHotkeyRuntimeComponent 之间存在大量重复实现

- ID: F-ARCH-03
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  AbilityHotkeyRuntimeComponent 中的 BuildAbilityAreaCells、BuildIrregularPatternCells、GetFacingDirection、GetRight 等方法与 AbilityAreaUtility 中的同名方法实现完全相同。虽然注释中提到了 '统一' 的意图，但 AbilityHotkeyRuntimeComponent 中仍保留了完整的私有副本（第1148-1267行），违反 DRY 原则。
- 建议:

  删除 AbilityHotkeyRuntimeComponent 中的重复方法，改为直接调用 AbilityAreaUtility 的公共静态方法。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs:1148-1267`
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityAreaUtility.cs:61-257`
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs`
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityAreaUtility.cs`

### VFX 位置解析逻辑在 CompAbilityEffect_Modular 与 VisualEffectWorker 之间重复

- ID: F-ARCH-04
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  CompAbilityEffect_Modular 中有 ResolveVfxPosition/ResolveVfxPositions/TryResolveFacingBasis/ResolveFacingMode/ResolveCastDirectionCell 等完整的 VFX 位置解析管线（约200行）。VisualEffectWorker 基类中同样有一套 ResolvePosition/ResolvePositions/TryResolveFacingBasis/ResolveFacingMode/ResolveCastDirectionCell。两者逻辑高度相似但又有微妙差异（如 sourceOverride 参数的支持），使得行为一致性难以保证。
- 建议:

  提取一个共享的 AbilityVfxPositionResolver 静态工具类，统一位置解析逻辑。CompAbilityEffect_Modular 和 VisualEffectWorker 都调用该工具类。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs:1817-1964`
  - `CharacterStudio/Source/CharacterStudio/Abilities/VisualEffectWorker.cs:20-148`
  - `CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs`
  - `CharacterStudio/Source/CharacterStudio/Abilities/VisualEffectWorker.cs`

### EffectWorkerFactory 每次调用创建新实例

- ID: F-PERF-01
- 严重级别: 中
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  EffectWorkerFactory.GetWorker() 每次调用都通过 Activator.CreateInstance() 创建新的 EffectWorker 实例。而 EffectWorker 子类全部是无状态的（Apply 方法不依赖任何实例字段），完全可以使用单例。特别是在范围技能的情境下，一次施放可能对数十个格子调用多次 GetWorker，产生不必要的 GC 压力。VisualEffectWorkerFactory 同理。
- 建议:

  将 workerTypes Dictionary<AbilityEffectType, Type> 改为 Dictionary<AbilityEffectType, EffectWorker>，预创建并缓存单例。由于 EffectWorker 无状态，单例完全安全。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/EffectWorkers.cs:333-361`
  - `CharacterStudio/Source/CharacterStudio/Abilities/EffectWorkers.cs`

### FindThingById 全地图线性扫描

- ID: F-PERF-02
- 严重级别: 中
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  CompAbilityEffect_Modular.FindThingById() 方法通过遍历 map.listerThings.AllThings 全地图所有物体来查找指定 ThingID。该方法在 TickProjectileInterceptorShield 和 TickAttachedShieldVisual 中每 tick 都可能被调用。在大地图上 AllThings 可达数千个元素，这是 O(n) 的线性扫描。
- 建议:

  将 Thing 引用直接存储在 CompPawnSkin 中（而非 ThingID 字符串），或使用 Map 级别的 ID 索引。另外要在保存/加载时通过 Scribe 重新解析引用。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs:825-841`
  - `CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs`

### UpdateRStackingStates 全图扫描所有 Pawn

- ID: F-PERF-03
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  UpdateRStackingStates() 每30 tick 会遍历所有地图的所有已生成 Pawn 来检测 R 叠层状态。在多地图大数量 Pawn 场景下可能产生不必要的性能开销。虽然已有 30 tick 节流保护，但扫描本身依赖 .Where/.SelectMany/.ToList() 产生 LINQ 临时分配。
- 建议:

  可以维护一个有活跃 rStacking 状态的 Pawn 集合，仅在技能触发时注册/注销，避免全图扫描。同时将 LINQ 链替换为 foreach + 预分配 buffer。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs:590-676`
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs`

### AbilityGrantUtility 双重拷贝浪费

- ID: F-QUAL-01
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  AbilityGrantUtility.CacheRuntimeAbilitySource() 在每次缓存时调用 CopyAbilityDefForRuntimeCache 创建深拷贝，而 GetRuntimeAbilitySourceDef() 在读取时又创建一次深拷贝。即每次获取都会触发完整的 Clone 链，包括 effects/visualEffects/runtimeComponents 列表的全量拷贝。
- 建议:

  考虑将缓存的副本标记为 immutable（只读语义），不在读取时再次拷贝。或者仅在 fingerprint 变化时更新缓存。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityGrantUtility.cs:169-177`
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityGrantUtility.cs:313-316`
  - `CharacterStudio/Source/CharacterStudio/Abilities/AbilityGrantUtility.cs`

### NormalizeForSave 中 trigger switch 无实际作用

- ID: F-QUAL-02
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  AbilityVisualEffectConfig.NormalizeForSave() 中的 trigger switch 表达式（第917-926行）将每个枚举值映射到自身，实际上没有任何转换效果，仅有 default 分支有意义。这段代码是死代码，徒增认知负担。
- 建议:

  简化为：`if (!Enum.IsDefined(typeof(AbilityVisualEffectTrigger), trigger)) trigger = AbilityVisualEffectTrigger.OnTargetApply;`
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/ModularAbilityDef.cs:917-926`
  - `CharacterStudio/Source/CharacterStudio/Abilities/ModularAbilityDef.cs`

### NormalizeRuntimeTrigger 空方法

- ID: F-QUAL-03
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  CompAbilityEffect_Modular.NormalizeRuntimeTrigger() 方法直接返回输入参数，没有任何实际处理。虽然可能是为将来扩展留的占位，但当前增加了不必要的方法调用开销和认知干扰。
- 建议:

  如无近期扩展计划，建议删除该方法，直接使用 vfx.trigger。如有计划，应添加 TODO 注释说明。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs:1528-1531`
  - `CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs`

### CopyHotkeyConfig 不完整

- ID: F-QUAL-04
- 严重级别: 中
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  Dialog_AbilityEditor.cs 中的 CopyHotkeyConfig 方法仅复制了 enabled/Q/W/E/R 四个槽位，但实际上 SkinAbilityHotkeyConfig 还有 T/A/S/D/F/Z/X/C/V 等 9 个槽位的字段。这意味着在编辑器中保存/拷贝热键配置时，这 9 个槽位的绑定会被静默丢失。这是一个功能性 bug。
- 建议:

  补全 CopyHotkeyConfig 方法中所有槽位的复制，或更好地使用 Clone() 方法加字段覆写的方式。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_AbilityEditor.cs:316-323`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_AbilityEditor.cs`

### SanitizeHotkeyConfigAgainstAbilities 不完整

- ID: F-QUAL-05
- 严重级别: 中
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  与 CopyHotkeyConfig 同理，SanitizeHotkeyConfigAgainstAbilities 方法仅清理 Q/W/E/R 四个槽位。T/A/S/D/F/Z/X/C/V 槽位即使引用了已删除的技能也不会被清理。
- 建议:

  扩展该方法覆盖所有 13 个槽位。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_AbilityEditor.cs:325-346`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_AbilityEditor.cs`

## 评审里程碑

### M1 · 架构与数据模型审查

- 状态: 已完成
- 记录时间: 2026-04-08T21:34:52.740Z
- 已审模块: Abilities/ModularAbilityDef.cs, Abilities/CompAbilityEffect_Modular.cs, Abilities/EffectWorkers.cs, Abilities/VisualEffectWorker.cs, Abilities/AbilityAreaUtility.cs, Abilities/AbilityGrantUtility.cs, Abilities/AbilityHotkeyRuntimeComponent.cs, Abilities/AbilityLoadoutRuntimeUtility.cs, Abilities/AbilityTimeStopRuntimeController.cs, Abilities/AbilityVanillaFlightUtility.cs
- 摘要:

  审查了 ModularAbilityDef、AbilityRuntimeComponentConfig、AbilityVisualEffectConfig、AbilityEffectConfig 等核心数据模型，以及 CompAbilityEffect_Modular 运行时执行链路、EffectWorker/VisualEffectWorker 工厂体系、AbilityGrantUtility 技能授予系统、AbilityHotkeyRuntimeComponent 热键系统、AbilityTimeStopRuntimeController 时停系统的整体架构。
- 结论:

  审查了 ModularAbilityDef、AbilityRuntimeComponentConfig、AbilityVisualEffectConfig、AbilityEffectConfig 等核心数据模型，以及 CompAbilityEffect_Modular 运行时执行链路、EffectWorker/VisualEffectWorker 工厂体系、AbilityGrantUtility 技能授予系统、AbilityHotkeyRuntimeComponent 热键系统、AbilityTimeStopRuntimeController 时停系统的整体架构。
- 问题:
  - [高] 可维护性: AbilityRuntimeComponentConfig 上帝对象
  - [高] 可维护性: 热键槽位 switch/case 重复代码泛滥
  - [中] 可维护性: AbilityAreaUtility 与 AbilityHotkeyRuntimeComponent 之间存在大量重复实现
  - [中] 可维护性: VFX 位置解析逻辑在 CompAbilityEffect_Modular 与 VisualEffectWorker 之间重复

### M2 · 性能、运行时安全与代码质量审查

- 状态: 已完成
- 记录时间: 2026-04-08T21:36:43.982Z
- 已审模块: UI/Dialog_AbilityEditor.cs, UI/Dialog_AbilityEditor.Labels.cs, UI/AbilityEditorExtensionRegistry.cs
- 摘要:

  审查了运行时性能热点、内存分配模式、并发安全性、工厂模式效率、序列化健壮性等方面的问题。同时覆盖了编辑器 UI 层的 Dialog_AbilityEditor partial class 体系和扩展注册表设计。
- 结论:

  审查了运行时性能热点、内存分配模式、并发安全性、工厂模式效率、序列化健壮性等方面的问题。同时覆盖了编辑器 UI 层的 Dialog_AbilityEditor partial class 体系和扩展注册表设计。
- 问题:
  - [中] 性能: EffectWorkerFactory 每次调用创建新实例
  - [中] 性能: FindThingById 全地图线性扫描
  - [低] 性能: UpdateRStackingStates 全图扫描所有 Pawn
  - [低] 性能: AbilityGrantUtility 双重拷贝浪费
  - [低] 可维护性: NormalizeForSave 中 trigger switch 无实际作用
  - [低] 可维护性: NormalizeRuntimeTrigger 空方法
  - [中] JavaScript: CopyHotkeyConfig 不完整
  - [中] JavaScript: SanitizeHotkeyConfigAgainstAbilities 不完整

## 最终结论

审查了运行时性能热点、内存分配模式、并发安全性、工厂模式效率、序列化健壮性等方面的问题。同时覆盖了编辑器 UI 层的 Dialog_AbilityEditor partial class 体系和扩展注册表设计。

## 评审快照

```json
{
  "formatVersion": 4,
  "kind": "limcode.review",
  "reviewRunId": "review-mnqkbxgt-8gu1c6",
  "createdAt": "2026-04-08T00:00:00.000Z",
  "updatedAt": "2026-04-08T21:36:43.982Z",
  "finalizedAt": null,
  "status": "in_progress",
  "overallDecision": null,
  "header": {
    "title": "CharacterStudio 技能编辑器模块 Code Review",
    "date": "2026-04-08",
    "overview": "对 CharacterStudio 技能编辑器（Abilities + UI）的全面代码审查，涵盖架构设计、代码质量、性能、可维护性等维度。"
  },
  "scope": {
    "markdown": "# CharacterStudio 技能编辑器模块 Code Review\n\n## 审查范围\n\n- `Source/CharacterStudio/Abilities/` 目录下全部 12 个文件\n- `Source/CharacterStudio/UI/Dialog_AbilityEditor*.cs` 系列 14 个 partial class 文件\n- `Source/CharacterStudio/UI/AbilityEditorExtensionRegistry.cs`\n- `Defs/AbilityExampleDefs/`\n\n## 总体评价\n\n技能编辑器模块实现了一套 **非常完备的模块化技能系统**，从编辑器 UI 到运行时执行链路、从视觉特效到热键系统，功能覆盖面广且设计目标清晰。代码有良好的注释和文档意识。但由于功能持续堆叠，部分区域出现了明显的 **技术债务**，需要有针对性地治理。"
  },
  "summary": {
    "latestConclusion": "审查了运行时性能热点、内存分配模式、并发安全性、工厂模式效率、序列化健壮性等方面的问题。同时覆盖了编辑器 UI 层的 Dialog_AbilityEditor partial class 体系和扩展注册表设计。",
    "recommendedNextAction": null,
    "reviewedModules": [
      "Abilities/ModularAbilityDef.cs",
      "Abilities/CompAbilityEffect_Modular.cs",
      "Abilities/EffectWorkers.cs",
      "Abilities/VisualEffectWorker.cs",
      "Abilities/AbilityAreaUtility.cs",
      "Abilities/AbilityGrantUtility.cs",
      "Abilities/AbilityHotkeyRuntimeComponent.cs",
      "Abilities/AbilityLoadoutRuntimeUtility.cs",
      "Abilities/AbilityTimeStopRuntimeController.cs",
      "Abilities/AbilityVanillaFlightUtility.cs",
      "UI/Dialog_AbilityEditor.cs",
      "UI/Dialog_AbilityEditor.Labels.cs",
      "UI/AbilityEditorExtensionRegistry.cs"
    ]
  },
  "stats": {
    "totalMilestones": 2,
    "completedMilestones": 2,
    "totalFindings": 12,
    "severity": {
      "high": 2,
      "medium": 6,
      "low": 4
    }
  },
  "milestones": [
    {
      "id": "M1",
      "title": "架构与数据模型审查",
      "status": "completed",
      "recordedAt": "2026-04-08T21:34:52.740Z",
      "summaryMarkdown": "审查了 ModularAbilityDef、AbilityRuntimeComponentConfig、AbilityVisualEffectConfig、AbilityEffectConfig 等核心数据模型，以及 CompAbilityEffect_Modular 运行时执行链路、EffectWorker/VisualEffectWorker 工厂体系、AbilityGrantUtility 技能授予系统、AbilityHotkeyRuntimeComponent 热键系统、AbilityTimeStopRuntimeController 时停系统的整体架构。",
      "conclusionMarkdown": "审查了 ModularAbilityDef、AbilityRuntimeComponentConfig、AbilityVisualEffectConfig、AbilityEffectConfig 等核心数据模型，以及 CompAbilityEffect_Modular 运行时执行链路、EffectWorker/VisualEffectWorker 工厂体系、AbilityGrantUtility 技能授予系统、AbilityHotkeyRuntimeComponent 热键系统、AbilityTimeStopRuntimeController 时停系统的整体架构。",
      "evidence": [],
      "reviewedModules": [
        "Abilities/ModularAbilityDef.cs",
        "Abilities/CompAbilityEffect_Modular.cs",
        "Abilities/EffectWorkers.cs",
        "Abilities/VisualEffectWorker.cs",
        "Abilities/AbilityAreaUtility.cs",
        "Abilities/AbilityGrantUtility.cs",
        "Abilities/AbilityHotkeyRuntimeComponent.cs",
        "Abilities/AbilityLoadoutRuntimeUtility.cs",
        "Abilities/AbilityTimeStopRuntimeController.cs",
        "Abilities/AbilityVanillaFlightUtility.cs"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "F-ARCH-01",
        "F-ARCH-02",
        "F-ARCH-03",
        "F-ARCH-04"
      ]
    },
    {
      "id": "M2",
      "title": "性能、运行时安全与代码质量审查",
      "status": "completed",
      "recordedAt": "2026-04-08T21:36:43.982Z",
      "summaryMarkdown": "审查了运行时性能热点、内存分配模式、并发安全性、工厂模式效率、序列化健壮性等方面的问题。同时覆盖了编辑器 UI 层的 Dialog_AbilityEditor partial class 体系和扩展注册表设计。",
      "conclusionMarkdown": "审查了运行时性能热点、内存分配模式、并发安全性、工厂模式效率、序列化健壮性等方面的问题。同时覆盖了编辑器 UI 层的 Dialog_AbilityEditor partial class 体系和扩展注册表设计。",
      "evidence": [],
      "reviewedModules": [
        "UI/Dialog_AbilityEditor.cs",
        "UI/Dialog_AbilityEditor.Labels.cs",
        "UI/AbilityEditorExtensionRegistry.cs"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "F-PERF-01",
        "F-PERF-02",
        "F-PERF-03",
        "F-QUAL-01",
        "F-QUAL-02",
        "F-QUAL-03",
        "F-QUAL-04",
        "F-QUAL-05"
      ]
    }
  ],
  "findings": [
    {
      "id": "F-ARCH-01",
      "severity": "high",
      "category": "maintainability",
      "title": "AbilityRuntimeComponentConfig 上帝对象",
      "descriptionMarkdown": "AbilityRuntimeComponentConfig 类包含超过 80 个公共字段（第216-326行），所有 30 种运行时组件类型的参数都平铺在同一个类中。这导致：1) 大量无关字段对每个具体类型而言是噪音；2) Validate() 方法已膨胀到近 200 行的 switch 语句；3) NormalizeForSave() 同样需要逐一处理 80+ 字段；4) 新增运行时组件类型需要修改多处代码。这是典型的 God Object 反模式。",
      "recommendationMarkdown": "引入继承体系或 composition 模式：创建 AbilityRuntimeComponentConfigBase 基类，为每种组件类型定义专用子配置类。或使用 Dictionary<string, object> 存储类型特定参数 + 按类型的 IComponentValidator / IComponentNormalizer 接口。短期可先将参数按功能分组为嵌套结构体。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/ModularAbilityDef.cs",
          "lineStart": 216,
          "lineEnd": 611
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/ModularAbilityDef.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ARCH-02",
      "severity": "high",
      "category": "maintainability",
      "title": "热键槽位 switch/case 重复代码泛滥",
      "descriptionMarkdown": "整个技能系统中，对 Q/W/E/R/T/A/S/D/F/Z/X/C/V 13 个热键槽位的处理通过大量几乎完全相同的 switch/case 语句实现。仅在 CompAbilityEffect_Modular.cs 中就有 ApplyHotkeyOverride（13分支）、ApplyFollowupCooldownGate（13分支）、GetSlotCooldownUntilTick（13分支）、SetSlotCooldownUntilTick（13分支）四处。在 AbilityHotkeyRuntimeComponent.cs 中还有 GetSlotCooldownUntil、SetSlotCooldown、GetSlotOverrideState、ClearSlotOverrideState 等多处。AbilityLoadoutRuntimeUtility.cs 中同样如此。保守估计这些重复 switch 代码超过 1000 行。",
      "recommendationMarkdown": "将 CompPawnSkin 中的 13 个独立字段（qCooldownUntilTick, wCooldownUntilTick, ...）重构为 Dictionary<string, SlotState> 或数组索引结构。一个 SlotState 结构体包含 cooldownUntilTick、overrideAbilityDefName、overrideExpireTick 等字段。所有 switch 都可简化为一次字典/数组查找。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs",
          "lineStart": 467,
          "lineEnd": 531
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs",
          "lineStart": 1263,
          "lineEnd": 1327
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs",
          "lineStart": 154,
          "lineEnd": 195
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ARCH-03",
      "severity": "medium",
      "category": "maintainability",
      "title": "AbilityAreaUtility 与 AbilityHotkeyRuntimeComponent 之间存在大量重复实现",
      "descriptionMarkdown": "AbilityHotkeyRuntimeComponent 中的 BuildAbilityAreaCells、BuildIrregularPatternCells、GetFacingDirection、GetRight 等方法与 AbilityAreaUtility 中的同名方法实现完全相同。虽然注释中提到了 '统一' 的意图，但 AbilityHotkeyRuntimeComponent 中仍保留了完整的私有副本（第1148-1267行），违反 DRY 原则。",
      "recommendationMarkdown": "删除 AbilityHotkeyRuntimeComponent 中的重复方法，改为直接调用 AbilityAreaUtility 的公共静态方法。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs",
          "lineStart": 1148,
          "lineEnd": 1267
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityAreaUtility.cs",
          "lineStart": 61,
          "lineEnd": 257
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityAreaUtility.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ARCH-04",
      "severity": "medium",
      "category": "maintainability",
      "title": "VFX 位置解析逻辑在 CompAbilityEffect_Modular 与 VisualEffectWorker 之间重复",
      "descriptionMarkdown": "CompAbilityEffect_Modular 中有 ResolveVfxPosition/ResolveVfxPositions/TryResolveFacingBasis/ResolveFacingMode/ResolveCastDirectionCell 等完整的 VFX 位置解析管线（约200行）。VisualEffectWorker 基类中同样有一套 ResolvePosition/ResolvePositions/TryResolveFacingBasis/ResolveFacingMode/ResolveCastDirectionCell。两者逻辑高度相似但又有微妙差异（如 sourceOverride 参数的支持），使得行为一致性难以保证。",
      "recommendationMarkdown": "提取一个共享的 AbilityVfxPositionResolver 静态工具类，统一位置解析逻辑。CompAbilityEffect_Modular 和 VisualEffectWorker 都调用该工具类。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs",
          "lineStart": 1817,
          "lineEnd": 1964
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/VisualEffectWorker.cs",
          "lineStart": 20,
          "lineEnd": 148
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/VisualEffectWorker.cs"
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
      "title": "EffectWorkerFactory 每次调用创建新实例",
      "descriptionMarkdown": "EffectWorkerFactory.GetWorker() 每次调用都通过 Activator.CreateInstance() 创建新的 EffectWorker 实例。而 EffectWorker 子类全部是无状态的（Apply 方法不依赖任何实例字段），完全可以使用单例。特别是在范围技能的情境下，一次施放可能对数十个格子调用多次 GetWorker，产生不必要的 GC 压力。VisualEffectWorkerFactory 同理。",
      "recommendationMarkdown": "将 workerTypes Dictionary<AbilityEffectType, Type> 改为 Dictionary<AbilityEffectType, EffectWorker>，预创建并缓存单例。由于 EffectWorker 无状态，单例完全安全。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/EffectWorkers.cs",
          "lineStart": 333,
          "lineEnd": 361
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/EffectWorkers.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-PERF-02",
      "severity": "medium",
      "category": "performance",
      "title": "FindThingById 全地图线性扫描",
      "descriptionMarkdown": "CompAbilityEffect_Modular.FindThingById() 方法通过遍历 map.listerThings.AllThings 全地图所有物体来查找指定 ThingID。该方法在 TickProjectileInterceptorShield 和 TickAttachedShieldVisual 中每 tick 都可能被调用。在大地图上 AllThings 可达数千个元素，这是 O(n) 的线性扫描。",
      "recommendationMarkdown": "将 Thing 引用直接存储在 CompPawnSkin 中（而非 ThingID 字符串），或使用 Map 级别的 ID 索引。另外要在保存/加载时通过 Scribe 重新解析引用。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs",
          "lineStart": 825,
          "lineEnd": 841
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-PERF-03",
      "severity": "low",
      "category": "performance",
      "title": "UpdateRStackingStates 全图扫描所有 Pawn",
      "descriptionMarkdown": "UpdateRStackingStates() 每30 tick 会遍历所有地图的所有已生成 Pawn 来检测 R 叠层状态。在多地图大数量 Pawn 场景下可能产生不必要的性能开销。虽然已有 30 tick 节流保护，但扫描本身依赖 .Where/.SelectMany/.ToList() 产生 LINQ 临时分配。",
      "recommendationMarkdown": "可以维护一个有活跃 rStacking 状态的 Pawn 集合，仅在技能触发时注册/注销，避免全图扫描。同时将 LINQ 链替换为 foreach + 预分配 buffer。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs",
          "lineStart": 590,
          "lineEnd": 676
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityHotkeyRuntimeComponent.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-QUAL-01",
      "severity": "low",
      "category": "performance",
      "title": "AbilityGrantUtility 双重拷贝浪费",
      "descriptionMarkdown": "AbilityGrantUtility.CacheRuntimeAbilitySource() 在每次缓存时调用 CopyAbilityDefForRuntimeCache 创建深拷贝，而 GetRuntimeAbilitySourceDef() 在读取时又创建一次深拷贝。即每次获取都会触发完整的 Clone 链，包括 effects/visualEffects/runtimeComponents 列表的全量拷贝。",
      "recommendationMarkdown": "考虑将缓存的副本标记为 immutable（只读语义），不在读取时再次拷贝。或者仅在 fingerprint 变化时更新缓存。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityGrantUtility.cs",
          "lineStart": 169,
          "lineEnd": 177
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityGrantUtility.cs",
          "lineStart": 313,
          "lineEnd": 316
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/AbilityGrantUtility.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-QUAL-02",
      "severity": "low",
      "category": "maintainability",
      "title": "NormalizeForSave 中 trigger switch 无实际作用",
      "descriptionMarkdown": "AbilityVisualEffectConfig.NormalizeForSave() 中的 trigger switch 表达式（第917-926行）将每个枚举值映射到自身，实际上没有任何转换效果，仅有 default 分支有意义。这段代码是死代码，徒增认知负担。",
      "recommendationMarkdown": "简化为：`if (!Enum.IsDefined(typeof(AbilityVisualEffectTrigger), trigger)) trigger = AbilityVisualEffectTrigger.OnTargetApply;`",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/ModularAbilityDef.cs",
          "lineStart": 917,
          "lineEnd": 926
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/ModularAbilityDef.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-QUAL-03",
      "severity": "low",
      "category": "maintainability",
      "title": "NormalizeRuntimeTrigger 空方法",
      "descriptionMarkdown": "CompAbilityEffect_Modular.NormalizeRuntimeTrigger() 方法直接返回输入参数，没有任何实际处理。虽然可能是为将来扩展留的占位，但当前增加了不必要的方法调用开销和认知干扰。",
      "recommendationMarkdown": "如无近期扩展计划，建议删除该方法，直接使用 vfx.trigger。如有计划，应添加 TODO 注释说明。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs",
          "lineStart": 1528,
          "lineEnd": 1531
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Abilities/CompAbilityEffect_Modular.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-QUAL-04",
      "severity": "medium",
      "category": "javascript",
      "title": "CopyHotkeyConfig 不完整",
      "descriptionMarkdown": "Dialog_AbilityEditor.cs 中的 CopyHotkeyConfig 方法仅复制了 enabled/Q/W/E/R 四个槽位，但实际上 SkinAbilityHotkeyConfig 还有 T/A/S/D/F/Z/X/C/V 等 9 个槽位的字段。这意味着在编辑器中保存/拷贝热键配置时，这 9 个槽位的绑定会被静默丢失。这是一个功能性 bug。",
      "recommendationMarkdown": "补全 CopyHotkeyConfig 方法中所有槽位的复制，或更好地使用 Clone() 方法加字段覆写的方式。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_AbilityEditor.cs",
          "lineStart": 316,
          "lineEnd": 323
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_AbilityEditor.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-QUAL-05",
      "severity": "medium",
      "category": "javascript",
      "title": "SanitizeHotkeyConfigAgainstAbilities 不完整",
      "descriptionMarkdown": "与 CopyHotkeyConfig 同理，SanitizeHotkeyConfigAgainstAbilities 方法仅清理 Q/W/E/R 四个槽位。T/A/S/D/F/Z/X/C/V 槽位即使引用了已删除的技能也不会被清理。",
      "recommendationMarkdown": "扩展该方法覆盖所有 13 个槽位。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_AbilityEditor.cs",
          "lineStart": 325,
          "lineEnd": 346
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_AbilityEditor.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    }
  ],
  "render": {
    "rendererVersion": 4,
    "bodyHash": "sha256:a02498a66cb6066460e8894f26f914be2d5eee0e19154eca11a7fef43e24b930",
    "generatedAt": "2026-04-08T21:36:43.982Z",
    "locale": "zh-CN"
  }
}
```
