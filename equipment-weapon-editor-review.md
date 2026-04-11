# 装备/武器编辑器代码 Review
- 日期: 2026-04-08
- 概述: CharacterStudio 装备/武器编辑器子系统全面代码审查
- 状态: 已完成
- 总体结论: 有条件通过

## 评审范围

# 装备/武器编辑器代码 Review

## 审查范围

本次 Review 覆盖 CharacterStudio 项目中装备（Equipment）和武器（Weapon）编辑器的完整子系统，包括：

### 数据模型层 (Core)
- `CharacterEquipmentDef.cs` — 装备定义数据模型（700行）
- `WeaponRenderConfig.cs` — 武器渲染覆写配置（179行）
- `DefModExtension_EquipmentRender.cs` — 装备渲染 Def 扩展（234行）

### UI 编辑器层 (UI)
- `Dialog_SkinEditor.Equipment.cs` — 装备列表面板与操作逻辑（1309行）
- `Dialog_SkinEditor.Weapon.cs` — 武器渲染编辑面板（225行）
- `Dialog_SkinEditor.Properties.Equipment.cs` — 装备属性编辑面板（948行）
- `Dialog_ApparelBrowser.cs` — 服装选择浏览器（139行）

### 渲染层 (Rendering)
- `Patch_WeaponRender.cs` — 武器渲染 Harmony 补丁（180行）
- `PawnRenderNodeWorker_WeaponCarryVisual.cs` — 武器携带视觉层（170行）

### 导出层 (Exporter)
- `ModExportXmlWriter.cs` — 装备 ThingDef / RecipeDef / Bundle XML 导出

## 评审摘要

- 当前状态: 已完成
- 已审模块: Core/CharacterEquipmentDef.cs, Core/WeaponRenderConfig.cs, Core/DefModExtension_EquipmentRender.cs, UI/Dialog_SkinEditor.Equipment.cs, UI/Dialog_SkinEditor.Weapon.cs, UI/Dialog_SkinEditor.Properties.Equipment.cs, UI/Dialog_ApparelBrowser.cs, Rendering/Patch_WeaponRender.cs, Rendering/PawnRenderNodeWorker_WeaponCarryVisual.cs, Exporter/ModExportXmlWriter.cs
- 当前进度: 已记录 3 个里程碑；最新：M3
- 里程碑总数: 3
- 已完成里程碑: 3
- 问题总数: 15
- 问题严重级别分布: 高 2 / 中 8 / 低 5
- 最新结论: ## 总体评价 装备/武器编辑器子系统**功能完整度很高**，覆盖了从编辑器内创建装备、预览渲染、导入导出 XML、测试生成、正式 Mod 导出（ThingDef + RecipeDef + Bundle）、武器携带视觉系统到运行时渲染补丁的完整链路。架构上对旧版存档的兼容迁移（MigrateLegacyVisualIfNeeded）和 Undo/Redo 的集成做得比较用心。 但存在三类系统性问题需要重点关注： ### 1. 数据重复与「上帝对象」（核心可维护性风险） CharacterEquipmentRenderData、DefModExtension_EquipmentRender、EquipmentTriggeredAnimationOverride 三个类之间存在大量字段镜像复制，加上 CharacterEquipmentDef 本身承载了标识、渲染、服装、制作、交易、飞行器、技能等 7+ 个领域职责。新增任何一个渲染或动画字段需要同步修改 3 个类定义 + 2 个映射方法 + 2 个 Clone 方法，遗漏风险极高。 ### 2. 国际化不完整（用户体验阻碍） 整个项目大部分 UI 标签已经走了 `.Translate()` 体系，但 DrawTriggeredAnimationOverrideSection 中约 20 个标签和 RecipeDef 导出的 label/jobString 仍为硬编码字符串。这对非中文/非英文用户构成明显障碍。 ### 3. 编辑器 Undo 一致性不足 Weapon 面板和 Equipment 属性面板中 Slider 变更的 Undo 支持存在缺陷，两种变更提交模式（MutateWithUndo vs 手动 CaptureSnapshot + MarkDirty）混用降低了代码可预测性。 ### 亮点 - **XML 导入/导出健壮**：支持多种 XML 格式（PawnSkinDef 嵌套、独立 equipments、正式 ThingDef）的自动识别和解析 - **飞行翼预设系统**：AddAircraftWingAnimationPreset 是一个精心设计的快捷工具 - **方向渲染覆写**：支持 South/EastWest/North 独立动画参数覆写，功能丰富 - **运行时测试生成**：可在编辑器中直接创建运行时 ThingDef 并在地图上生成测试物品
- 下一步建议: 按优先级修复 2 个 HIGH 级问题（硬编码标签国际化、导出硬编码中文），然后着手提取 TriggeredAnimationParams 公共类消除三处重复。
- 总体结论: 有条件通过

## 评审发现

### CharacterEquipmentRenderData 与 DefModExtension_EquipmentRender 存在大量字段重复

- ID: F-DATA-01
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  CharacterEquipmentRenderData（编辑器内部模型）和 DefModExtension_EquipmentRender（Def 运行时扩展）拥有 40+ 个近乎完全一致的字段（offset/scale/rotation/triggered* 系列）。FromEquipment() 和 ToPawnLayerConfig() 中需逐字段手工映射。任何新字段添加都必须同步修改两处定义 + 两处映射，极易遗漏。
- 建议:

  考虑将共享的渲染字段提取到一个公共基类或接口 IEquipmentRenderSpec，让 CharacterEquipmentRenderData 和 DefModExtension_EquipmentRender 共享字段定义，或使用反射/源码生成器自动映射。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs`
  - `CharacterStudio/Source/CharacterStudio/Core/DefModExtension_EquipmentRender.cs`

### EquipmentTriggeredAnimationOverride 与 CharacterEquipmentRenderData 的 triggered* 字段完全重复

- ID: F-DATA-02
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  EquipmentTriggeredAnimationOverride 类包含 20+ 个与 CharacterEquipmentRenderData 中 triggered* 前缀字段完全一致的字段，以及各自独立的 Clone()/EnsureDefaults() 方法。修改动画字段时需要同步三处：RenderData 主体、Override 类、DefModExtension。
- 建议:

  将 triggered 动画参数提取为独立的 TriggeredAnimationParams 类，在 CharacterEquipmentRenderData 和 EquipmentTriggeredAnimationOverride 中内嵌使用。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs`

### CharacterEquipmentDef 单类承载过多职责

- ID: F-DATA-03
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  CharacterEquipmentDef 一个类同时包含：基础标识（defName/label）、渲染数据（renderData）、Apparel 定义属性（bodyPartGroups/apparelLayers/apparelTags）、制作配方（recipeDefName/recipeWorkAmount/recipeIngredients）、交易属性（allowTrading/marketValue/tradeTags）、飞行器配置（flyerThingDefName/flyerClassName/flyerFlightSpeed）、技能绑定（abilityDefNames）和遗留兼容字段。单文件 700 行，实质是一个「上帝对象」。
- 建议:

  按职责拆分为组合对象：EquipmentIdentity（标识）、EquipmentApparelSpec（服装定义）、EquipmentCraftingSpec（制作配方）、EquipmentTradeSpec（交易属性）、EquipmentFlyerSpec（飞行器）等，以 CharacterEquipmentDef 作为聚合根。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs`

### EnsureDefaults 中默认值与常量定义不一致

- ID: F-DATA-04
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  CharacterEquipmentDef 定义了 DefaultSlotTag = "Apparel"、DefaultParentThingDefName = "ApparelMakeableBase" 等常量，但 EnsureDefaults() 方法中的部分回退值使用了硬编码字符串 "Apparel"、"ApparelMakeableBase" 而非引用常量。虽然值一致，但不便维护。
- 建议:

  统一使用类常量代替硬编码字符串。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs`

### DrawTriggeredAnimationOverrideSection 中大量硬编码英文标签

- ID: F-UI-01
- 严重级别: 高
- 分类: 可访问性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  DrawTriggeredAnimationOverrideSection 方法中存在约 20 处硬编码英文标签："Enabled"、"Use Triggered Local Animation"、"Trigger Ability DefName"、"Animation Group Key"、"Role"、"Deploy Angle"、"Return Angle"、"Deploy Ticks"、"Hold Ticks"、"Return Ticks"、"Pivot X/Y"、"Use VFX Visibility"、"Visible During Deploy/Hold/Return"、"Visible Outside Cycle"。以及方向覆写的标题 "South Override"、"East/West Override"、"North Override" 也是硬编码。这打破了其他模块统一使用 `.Translate()` 的国际化约定。
- 建议:

  为所有硬编码标签创建 CS_Studio_Equip_TriggeredOverride_* 系列翻译键，替换硬编码字符串。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs`

### DrawPathFieldWithBrowser 局部函数在两个文件中完全重复

- ID: F-UI-02
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  Dialog_SkinEditor.Weapon.cs（行31-63）和 Dialog_SkinEditor.Properties.Equipment.cs（行50-82）各定义了一个功能完全相同的 DrawPathFieldWithBrowser 局部函数，两者的签名、实现和布局逻辑完全一致，形成了代码重复。
- 建议:

  将 DrawPathFieldWithBrowser 提取到 UIHelper 作为静态方法，消除重复。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Weapon.cs`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs`

### Properties.Equipment 的 viewRect 高度硬编码为 1500

- ID: F-UI-03
- 严重级别: 中
- 分类: CSS
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  DrawEquipmentProperties 中 viewRect 的高度硬编码为 1500（行38），但实际渲染内容因折叠状态不同差异很大。展开所有折叠面板（基础+能力+定义+视觉+变换+触发动画+3个方向覆写）时可能超过 1500，折叠时则浪费大量空白。对比 Weapon 面板用了动态计算 `viewRect.height = Mathf.Max(y + 10f, contentRect.height - 4f)`。
- 建议:

  改为动态高度，在所有属性绘制完成后根据实际 y 值更新 viewRect.height。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs`

### Equipment 面板按钮只用单字符标签 (+/A/C/T/-) 语义不清

- ID: F-UI-04
- 严重级别: 低
- 分类: 可访问性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  DrawEquipmentPanel 的 8 个操作按钮使用 "+"、"A"、"✈"、"-"、"C"、"T"、"↓"、"↑" 等单字符标签。虽然有 Tooltip，但初次使用时用户无法快速理解含义，特别是 "A"（能力）、"C"（复制）、"T"（测试生成）等字母缩写不直觉。
- 建议:

  建议至少使用两字符的缩写或语义图标（如有 Texture 图标资源），或在按钮宽度允许时使用短文本如"新增"、"复制"等。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs`

### Weapon 面板 Slider 直接修改引用字段但变更检测在帧末

- ID: F-UI-05
- 严重级别: 中
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  DrawWeaponPanel 中，UIHelper.DrawPropertySlider 直接通过 ref 修改 carry.drawOrder、carry.scale、carry.offset 等字段（行142-153），然后在帧末通过 AreCarryVisualConfigsEqual 与快照比较来触发 FinalizeMutatedEditorState。但 Slider 拖动时每帧都在修改值，而 CaptureUndoSnapshot 只在开头拍了一次快照 carrySnapshot。这意味着：1）拖动 Slider 过程中没有产生正确的 undo 记录；2）如果用户在同一帧内修改多个 Slider 也只会检测到一次变更。
- 建议:

  给 Slider 变更也包裹 MutateWithUndo，或者采用 "开始拖动时拍快照、结束拖动时提交" 的模式来正确支持 Undo。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Weapon.cs`

### Dialog_ApparelBrowser 使用 ToLower() 而非 OrdinalIgnoreCase 进行搜索过滤

- ID: F-UI-06
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  FilterApparel 中使用 .ToLower().Contains(searchLower) 模式进行大小写不敏感搜索（行47-51），这对非英语区域（如土耳其语 I/İ）有潜在兼容问题，且每次搜索为每个条目创建小写副本字符串。
- 建议:

  使用 IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 代替 ToLower().Contains()。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_ApparelBrowser.cs`

### Properties.Equipment 中 CaptureUndoSnapshot + MarkEquipmentDirty 模式不一致

- ID: F-UI-07
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  DrawEquipmentProperties 方法中存在两种变更提交模式混用：1）部分属性使用 MutateWithUndo(() => {...}) 一步完成快照+变更+刷新；2）部分属性使用 CaptureUndoSnapshot(); 直接修改字段; MarkEquipmentDirty() 三步手动模式（如 VisualBase/VisualTransform/TriggeredAnimation 整个区域）。混用两种模式使代码约定不统一，增加理解和维护成本。
- 建议:

  统一为一种模式。推荐所有变更使用 MutateEquipmentWithUndo 封装。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs`

### Equipment 面板 runtimeTestEquipmentDefs 静态字典无清理机制

- ID: F-UI-08
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  runtimeTestEquipmentDefs 是一个 static readonly Dictionary，用于缓存测试生成的运行时 ThingDef。但没有任何清理/淘汰机制。在长时间使用编辑器反复测试生成时，这些 ThingDef 会持续累积在 DefDatabase 和缓存字典中，可能造成内存泄漏和 DefDatabase 膨胀。
- 建议:

  添加清理机制：1）编辑器关闭时调用清理；2）或限制缓存大小实行 LRU 淘汰。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs`

### Patch_WeaponRender.OffsetFor_Postfix 对所有 PawnRenderNodeWorker 子类生效

- ID: F-RENDER-01
- 严重级别: 中
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M3
- 说明:

  Patch_WeaponRender 将 Postfix 补丁应用到基类 PawnRenderNodeWorker.OffsetFor。由于 RimWorld 的渲染树每帧对每个 Pawn 的每个渲染节点都会调用 OffsetFor/ScaleFor，这意味着即使大多数节点不是武器节点，也都会进入 Postfix 逻辑执行 IsWeaponNode 检查。在大型殖民地（100+ Pawn）场景下可能成为性能热点。ApplyFlightStateOffset 也在此 Postfix 中执行，进一步增加了非武器节点的额外开销。
- 建议:

  1) 考虑使用 TargetMethod 更精确地定位武器相关的 Worker 子类；2) 将 IsWeaponNode 的 tagDef 字符串检查结果缓存到节点实例上（Dictionary<PawnRenderNode, bool>），避免每帧重复字符串操作。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Rendering/Patch_WeaponRender.cs`

### WeaponCarryVisual 的 graphicCache 无大小限制且全局共享

- ID: F-RENDER-02
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M3
- 说明:

  PawnRenderNodeWorker_WeaponCarryVisual 中的 graphicCache 和 isMultiCache 均为 static 全局字典，无大小限制。若用户在编辑器中频繁切换不同贴图路径、shader 和颜色组合，缓存会持续增长。CanCacheGraphic 会清除加载失败的条目但不清除过时条目。
- 建议:

  加入容量上限（如 128 条）或定期清理策略。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_WeaponCarryVisual.cs`

### RecipeDef 导出中 label/jobString 使用硬编码中文

- ID: F-EXPORT-01
- 严重级别: 高
- 分类: 可访问性
- 跟踪状态: 开放
- 相关里程碑: M3
- 说明:

  ModExportXmlWriter.GenerateEquipmentRecipeDefXml 中 label 和 jobString 直接使用硬编码中文字符串：`$"制作{equipment.GetDisplayLabel()}"` 和 `$"正在制作{equipment.GetDisplayLabel()}"`（行1003-1004）。导出的 RecipeDef XML 对非中文用户完全不可读。这是一个跨模块问题，但直接影响装备编辑器的导出质量。
- 建议:

  1) 使用可翻译的模板字符串或让用户在编辑器中自定义 label/jobString；2) 至少回退到英文默认值 `Make {label}` / `Making {label}`。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Exporter/ModExportXmlWriter.cs`

## 评审里程碑

### M1 · 数据模型层审查

- 状态: 已完成
- 记录时间: 2026-04-08T21:45:36.247Z
- 已审模块: Core/CharacterEquipmentDef.cs, Core/WeaponRenderConfig.cs, Core/DefModExtension_EquipmentRender.cs
- 摘要:

  审查 CharacterEquipmentDef、CharacterEquipmentRenderData、EquipmentTriggeredAnimationOverride、WeaponRenderConfig、WeaponCarryVisualConfig、DefModExtension_EquipmentRender 等数据模型的设计质量、字段组织和数据一致性。
- 结论:

  审查 CharacterEquipmentDef、CharacterEquipmentRenderData、EquipmentTriggeredAnimationOverride、WeaponRenderConfig、WeaponCarryVisualConfig、DefModExtension_EquipmentRender 等数据模型的设计质量、字段组织和数据一致性。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs`
  - `CharacterStudio/Source/CharacterStudio/Core/WeaponRenderConfig.cs`
  - `CharacterStudio/Source/CharacterStudio/Core/DefModExtension_EquipmentRender.cs`
- 问题:
  - [中] 可维护性: CharacterEquipmentRenderData 与 DefModExtension_EquipmentRender 存在大量字段重复
  - [中] 可维护性: EquipmentTriggeredAnimationOverride 与 CharacterEquipmentRenderData 的 triggered* 字段完全重复
  - [中] 可维护性: CharacterEquipmentDef 单类承载过多职责
  - [低] 可维护性: EnsureDefaults 中默认值与常量定义不一致

### M2 · UI 编辑器层审查

- 状态: 已完成
- 记录时间: 2026-04-08T21:49:03.144Z
- 已审模块: UI/Dialog_SkinEditor.Equipment.cs, UI/Dialog_SkinEditor.Weapon.cs, UI/Dialog_SkinEditor.Properties.Equipment.cs, UI/Dialog_ApparelBrowser.cs
- 摘要:

  审查 Dialog_SkinEditor.Equipment.cs、Dialog_SkinEditor.Weapon.cs、Dialog_SkinEditor.Properties.Equipment.cs、Dialog_ApparelBrowser.cs 的代码质量、国际化、代码重复和用户体验问题。
- 结论:

  审查 Dialog_SkinEditor.Equipment.cs、Dialog_SkinEditor.Weapon.cs、Dialog_SkinEditor.Properties.Equipment.cs、Dialog_ApparelBrowser.cs 的代码质量、国际化、代码重复和用户体验问题。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Weapon.cs`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_ApparelBrowser.cs`
- 问题:
  - [高] 可访问性: DrawTriggeredAnimationOverrideSection 中大量硬编码英文标签
  - [中] 可维护性: DrawPathFieldWithBrowser 局部函数在两个文件中完全重复
  - [中] CSS: Properties.Equipment 的 viewRect 高度硬编码为 1500
  - [低] 可访问性: Equipment 面板按钮只用单字符标签 (+/A/C/T/-) 语义不清
  - [中] JavaScript: Weapon 面板 Slider 直接修改引用字段但变更检测在帧末
  - [低] 性能: Dialog_ApparelBrowser 使用 ToLower() 而非 OrdinalIgnoreCase 进行搜索过滤
  - [中] 可维护性: Properties.Equipment 中 CaptureUndoSnapshot + MarkEquipmentDirty 模式不一致
  - [低] 性能: Equipment 面板 runtimeTestEquipmentDefs 静态字典无清理机制

### M3 · 渲染层与导出层审查

- 状态: 已完成
- 记录时间: 2026-04-08T21:49:36.096Z
- 已审模块: Rendering/Patch_WeaponRender.cs, Rendering/PawnRenderNodeWorker_WeaponCarryVisual.cs, Exporter/ModExportXmlWriter.cs
- 摘要:

  审查 Patch_WeaponRender、PawnRenderNodeWorker_WeaponCarryVisual 和 ModExportXmlWriter 的实现质量、安全性和可维护性。
- 结论:

  审查 Patch_WeaponRender、PawnRenderNodeWorker_WeaponCarryVisual 和 ModExportXmlWriter 的实现质量、安全性和可维护性。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Rendering/Patch_WeaponRender.cs`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_WeaponCarryVisual.cs`
  - `CharacterStudio/Source/CharacterStudio/Exporter/ModExportXmlWriter.cs:917-1050`
- 问题:
  - [中] 性能: Patch_WeaponRender.OffsetFor_Postfix 对所有 PawnRenderNodeWorker 子类生效
  - [低] 性能: WeaponCarryVisual 的 graphicCache 无大小限制且全局共享
  - [高] 可访问性: RecipeDef 导出中 label/jobString 使用硬编码中文

## 最终结论

## 总体评价

装备/武器编辑器子系统**功能完整度很高**，覆盖了从编辑器内创建装备、预览渲染、导入导出 XML、测试生成、正式 Mod 导出（ThingDef + RecipeDef + Bundle）、武器携带视觉系统到运行时渲染补丁的完整链路。架构上对旧版存档的兼容迁移（MigrateLegacyVisualIfNeeded）和 Undo/Redo 的集成做得比较用心。

但存在三类系统性问题需要重点关注：

### 1. 数据重复与「上帝对象」（核心可维护性风险）
CharacterEquipmentRenderData、DefModExtension_EquipmentRender、EquipmentTriggeredAnimationOverride 三个类之间存在大量字段镜像复制，加上 CharacterEquipmentDef 本身承载了标识、渲染、服装、制作、交易、飞行器、技能等 7+ 个领域职责。新增任何一个渲染或动画字段需要同步修改 3 个类定义 + 2 个映射方法 + 2 个 Clone 方法，遗漏风险极高。

### 2. 国际化不完整（用户体验阻碍）
整个项目大部分 UI 标签已经走了 `.Translate()` 体系，但 DrawTriggeredAnimationOverrideSection 中约 20 个标签和 RecipeDef 导出的 label/jobString 仍为硬编码字符串。这对非中文/非英文用户构成明显障碍。

### 3. 编辑器 Undo 一致性不足
Weapon 面板和 Equipment 属性面板中 Slider 变更的 Undo 支持存在缺陷，两种变更提交模式（MutateWithUndo vs 手动 CaptureSnapshot + MarkDirty）混用降低了代码可预测性。

### 亮点
- **XML 导入/导出健壮**：支持多种 XML 格式（PawnSkinDef 嵌套、独立 equipments、正式 ThingDef）的自动识别和解析
- **飞行翼预设系统**：AddAircraftWingAnimationPreset 是一个精心设计的快捷工具
- **方向渲染覆写**：支持 South/EastWest/North 独立动画参数覆写，功能丰富
- **运行时测试生成**：可在编辑器中直接创建运行时 ThingDef 并在地图上生成测试物品

## 评审快照

```json
{
  "formatVersion": 4,
  "kind": "limcode.review",
  "reviewRunId": "review-mnqksn6v-btiw5j",
  "createdAt": "2026-04-08T00:00:00.000Z",
  "updatedAt": "2026-04-08T21:50:35.100Z",
  "finalizedAt": "2026-04-08T21:50:35.100Z",
  "status": "completed",
  "overallDecision": "conditionally_accepted",
  "header": {
    "title": "装备/武器编辑器代码 Review",
    "date": "2026-04-08",
    "overview": "CharacterStudio 装备/武器编辑器子系统全面代码审查"
  },
  "scope": {
    "markdown": "# 装备/武器编辑器代码 Review\n\n## 审查范围\n\n本次 Review 覆盖 CharacterStudio 项目中装备（Equipment）和武器（Weapon）编辑器的完整子系统，包括：\n\n### 数据模型层 (Core)\n- `CharacterEquipmentDef.cs` — 装备定义数据模型（700行）\n- `WeaponRenderConfig.cs` — 武器渲染覆写配置（179行）\n- `DefModExtension_EquipmentRender.cs` — 装备渲染 Def 扩展（234行）\n\n### UI 编辑器层 (UI)\n- `Dialog_SkinEditor.Equipment.cs` — 装备列表面板与操作逻辑（1309行）\n- `Dialog_SkinEditor.Weapon.cs` — 武器渲染编辑面板（225行）\n- `Dialog_SkinEditor.Properties.Equipment.cs` — 装备属性编辑面板（948行）\n- `Dialog_ApparelBrowser.cs` — 服装选择浏览器（139行）\n\n### 渲染层 (Rendering)\n- `Patch_WeaponRender.cs` — 武器渲染 Harmony 补丁（180行）\n- `PawnRenderNodeWorker_WeaponCarryVisual.cs` — 武器携带视觉层（170行）\n\n### 导出层 (Exporter)\n- `ModExportXmlWriter.cs` — 装备 ThingDef / RecipeDef / Bundle XML 导出"
  },
  "summary": {
    "latestConclusion": "## 总体评价\n\n装备/武器编辑器子系统**功能完整度很高**，覆盖了从编辑器内创建装备、预览渲染、导入导出 XML、测试生成、正式 Mod 导出（ThingDef + RecipeDef + Bundle）、武器携带视觉系统到运行时渲染补丁的完整链路。架构上对旧版存档的兼容迁移（MigrateLegacyVisualIfNeeded）和 Undo/Redo 的集成做得比较用心。\n\n但存在三类系统性问题需要重点关注：\n\n### 1. 数据重复与「上帝对象」（核心可维护性风险）\nCharacterEquipmentRenderData、DefModExtension_EquipmentRender、EquipmentTriggeredAnimationOverride 三个类之间存在大量字段镜像复制，加上 CharacterEquipmentDef 本身承载了标识、渲染、服装、制作、交易、飞行器、技能等 7+ 个领域职责。新增任何一个渲染或动画字段需要同步修改 3 个类定义 + 2 个映射方法 + 2 个 Clone 方法，遗漏风险极高。\n\n### 2. 国际化不完整（用户体验阻碍）\n整个项目大部分 UI 标签已经走了 `.Translate()` 体系，但 DrawTriggeredAnimationOverrideSection 中约 20 个标签和 RecipeDef 导出的 label/jobString 仍为硬编码字符串。这对非中文/非英文用户构成明显障碍。\n\n### 3. 编辑器 Undo 一致性不足\nWeapon 面板和 Equipment 属性面板中 Slider 变更的 Undo 支持存在缺陷，两种变更提交模式（MutateWithUndo vs 手动 CaptureSnapshot + MarkDirty）混用降低了代码可预测性。\n\n### 亮点\n- **XML 导入/导出健壮**：支持多种 XML 格式（PawnSkinDef 嵌套、独立 equipments、正式 ThingDef）的自动识别和解析\n- **飞行翼预设系统**：AddAircraftWingAnimationPreset 是一个精心设计的快捷工具\n- **方向渲染覆写**：支持 South/EastWest/North 独立动画参数覆写，功能丰富\n- **运行时测试生成**：可在编辑器中直接创建运行时 ThingDef 并在地图上生成测试物品",
    "recommendedNextAction": "按优先级修复 2 个 HIGH 级问题（硬编码标签国际化、导出硬编码中文），然后着手提取 TriggeredAnimationParams 公共类消除三处重复。",
    "reviewedModules": [
      "Core/CharacterEquipmentDef.cs",
      "Core/WeaponRenderConfig.cs",
      "Core/DefModExtension_EquipmentRender.cs",
      "UI/Dialog_SkinEditor.Equipment.cs",
      "UI/Dialog_SkinEditor.Weapon.cs",
      "UI/Dialog_SkinEditor.Properties.Equipment.cs",
      "UI/Dialog_ApparelBrowser.cs",
      "Rendering/Patch_WeaponRender.cs",
      "Rendering/PawnRenderNodeWorker_WeaponCarryVisual.cs",
      "Exporter/ModExportXmlWriter.cs"
    ]
  },
  "stats": {
    "totalMilestones": 3,
    "completedMilestones": 3,
    "totalFindings": 15,
    "severity": {
      "high": 2,
      "medium": 8,
      "low": 5
    }
  },
  "milestones": [
    {
      "id": "M1",
      "title": "数据模型层审查",
      "status": "completed",
      "recordedAt": "2026-04-08T21:45:36.247Z",
      "summaryMarkdown": "审查 CharacterEquipmentDef、CharacterEquipmentRenderData、EquipmentTriggeredAnimationOverride、WeaponRenderConfig、WeaponCarryVisualConfig、DefModExtension_EquipmentRender 等数据模型的设计质量、字段组织和数据一致性。",
      "conclusionMarkdown": "审查 CharacterEquipmentDef、CharacterEquipmentRenderData、EquipmentTriggeredAnimationOverride、WeaponRenderConfig、WeaponCarryVisualConfig、DefModExtension_EquipmentRender 等数据模型的设计质量、字段组织和数据一致性。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/WeaponRenderConfig.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/DefModExtension_EquipmentRender.cs"
        }
      ],
      "reviewedModules": [
        "Core/CharacterEquipmentDef.cs",
        "Core/WeaponRenderConfig.cs",
        "Core/DefModExtension_EquipmentRender.cs"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "F-DATA-01",
        "F-DATA-02",
        "F-DATA-03",
        "F-DATA-04"
      ]
    },
    {
      "id": "M2",
      "title": "UI 编辑器层审查",
      "status": "completed",
      "recordedAt": "2026-04-08T21:49:03.144Z",
      "summaryMarkdown": "审查 Dialog_SkinEditor.Equipment.cs、Dialog_SkinEditor.Weapon.cs、Dialog_SkinEditor.Properties.Equipment.cs、Dialog_ApparelBrowser.cs 的代码质量、国际化、代码重复和用户体验问题。",
      "conclusionMarkdown": "审查 Dialog_SkinEditor.Equipment.cs、Dialog_SkinEditor.Weapon.cs、Dialog_SkinEditor.Properties.Equipment.cs、Dialog_ApparelBrowser.cs 的代码质量、国际化、代码重复和用户体验问题。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Weapon.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_ApparelBrowser.cs"
        }
      ],
      "reviewedModules": [
        "UI/Dialog_SkinEditor.Equipment.cs",
        "UI/Dialog_SkinEditor.Weapon.cs",
        "UI/Dialog_SkinEditor.Properties.Equipment.cs",
        "UI/Dialog_ApparelBrowser.cs"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "F-UI-01",
        "F-UI-02",
        "F-UI-03",
        "F-UI-04",
        "F-UI-05",
        "F-UI-06",
        "F-UI-07",
        "F-UI-08"
      ]
    },
    {
      "id": "M3",
      "title": "渲染层与导出层审查",
      "status": "completed",
      "recordedAt": "2026-04-08T21:49:36.096Z",
      "summaryMarkdown": "审查 Patch_WeaponRender、PawnRenderNodeWorker_WeaponCarryVisual 和 ModExportXmlWriter 的实现质量、安全性和可维护性。",
      "conclusionMarkdown": "审查 Patch_WeaponRender、PawnRenderNodeWorker_WeaponCarryVisual 和 ModExportXmlWriter 的实现质量、安全性和可维护性。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/Patch_WeaponRender.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_WeaponCarryVisual.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Exporter/ModExportXmlWriter.cs",
          "lineStart": 917,
          "lineEnd": 1050
        }
      ],
      "reviewedModules": [
        "Rendering/Patch_WeaponRender.cs",
        "Rendering/PawnRenderNodeWorker_WeaponCarryVisual.cs",
        "Exporter/ModExportXmlWriter.cs"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "F-RENDER-01",
        "F-RENDER-02",
        "F-EXPORT-01"
      ]
    }
  ],
  "findings": [
    {
      "id": "F-DATA-01",
      "severity": "medium",
      "category": "maintainability",
      "title": "CharacterEquipmentRenderData 与 DefModExtension_EquipmentRender 存在大量字段重复",
      "descriptionMarkdown": "CharacterEquipmentRenderData（编辑器内部模型）和 DefModExtension_EquipmentRender（Def 运行时扩展）拥有 40+ 个近乎完全一致的字段（offset/scale/rotation/triggered* 系列）。FromEquipment() 和 ToPawnLayerConfig() 中需逐字段手工映射。任何新字段添加都必须同步修改两处定义 + 两处映射，极易遗漏。",
      "recommendationMarkdown": "考虑将共享的渲染字段提取到一个公共基类或接口 IEquipmentRenderSpec，让 CharacterEquipmentRenderData 和 DefModExtension_EquipmentRender 共享字段定义，或使用反射/源码生成器自动映射。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/DefModExtension_EquipmentRender.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-DATA-02",
      "severity": "medium",
      "category": "maintainability",
      "title": "EquipmentTriggeredAnimationOverride 与 CharacterEquipmentRenderData 的 triggered* 字段完全重复",
      "descriptionMarkdown": "EquipmentTriggeredAnimationOverride 类包含 20+ 个与 CharacterEquipmentRenderData 中 triggered* 前缀字段完全一致的字段，以及各自独立的 Clone()/EnsureDefaults() 方法。修改动画字段时需要同步三处：RenderData 主体、Override 类、DefModExtension。",
      "recommendationMarkdown": "将 triggered 动画参数提取为独立的 TriggeredAnimationParams 类，在 CharacterEquipmentRenderData 和 EquipmentTriggeredAnimationOverride 中内嵌使用。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-DATA-03",
      "severity": "medium",
      "category": "maintainability",
      "title": "CharacterEquipmentDef 单类承载过多职责",
      "descriptionMarkdown": "CharacterEquipmentDef 一个类同时包含：基础标识（defName/label）、渲染数据（renderData）、Apparel 定义属性（bodyPartGroups/apparelLayers/apparelTags）、制作配方（recipeDefName/recipeWorkAmount/recipeIngredients）、交易属性（allowTrading/marketValue/tradeTags）、飞行器配置（flyerThingDefName/flyerClassName/flyerFlightSpeed）、技能绑定（abilityDefNames）和遗留兼容字段。单文件 700 行，实质是一个「上帝对象」。",
      "recommendationMarkdown": "按职责拆分为组合对象：EquipmentIdentity（标识）、EquipmentApparelSpec（服装定义）、EquipmentCraftingSpec（制作配方）、EquipmentTradeSpec（交易属性）、EquipmentFlyerSpec（飞行器）等，以 CharacterEquipmentDef 作为聚合根。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-DATA-04",
      "severity": "low",
      "category": "maintainability",
      "title": "EnsureDefaults 中默认值与常量定义不一致",
      "descriptionMarkdown": "CharacterEquipmentDef 定义了 DefaultSlotTag = \"Apparel\"、DefaultParentThingDefName = \"ApparelMakeableBase\" 等常量，但 EnsureDefaults() 方法中的部分回退值使用了硬编码字符串 \"Apparel\"、\"ApparelMakeableBase\" 而非引用常量。虽然值一致，但不便维护。",
      "recommendationMarkdown": "统一使用类常量代替硬编码字符串。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Core/CharacterEquipmentDef.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-01",
      "severity": "high",
      "category": "accessibility",
      "title": "DrawTriggeredAnimationOverrideSection 中大量硬编码英文标签",
      "descriptionMarkdown": "DrawTriggeredAnimationOverrideSection 方法中存在约 20 处硬编码英文标签：\"Enabled\"、\"Use Triggered Local Animation\"、\"Trigger Ability DefName\"、\"Animation Group Key\"、\"Role\"、\"Deploy Angle\"、\"Return Angle\"、\"Deploy Ticks\"、\"Hold Ticks\"、\"Return Ticks\"、\"Pivot X/Y\"、\"Use VFX Visibility\"、\"Visible During Deploy/Hold/Return\"、\"Visible Outside Cycle\"。以及方向覆写的标题 \"South Override\"、\"East/West Override\"、\"North Override\" 也是硬编码。这打破了其他模块统一使用 `.Translate()` 的国际化约定。",
      "recommendationMarkdown": "为所有硬编码标签创建 CS_Studio_Equip_TriggeredOverride_* 系列翻译键，替换硬编码字符串。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-02",
      "severity": "medium",
      "category": "maintainability",
      "title": "DrawPathFieldWithBrowser 局部函数在两个文件中完全重复",
      "descriptionMarkdown": "Dialog_SkinEditor.Weapon.cs（行31-63）和 Dialog_SkinEditor.Properties.Equipment.cs（行50-82）各定义了一个功能完全相同的 DrawPathFieldWithBrowser 局部函数，两者的签名、实现和布局逻辑完全一致，形成了代码重复。",
      "recommendationMarkdown": "将 DrawPathFieldWithBrowser 提取到 UIHelper 作为静态方法，消除重复。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Weapon.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-03",
      "severity": "medium",
      "category": "css",
      "title": "Properties.Equipment 的 viewRect 高度硬编码为 1500",
      "descriptionMarkdown": "DrawEquipmentProperties 中 viewRect 的高度硬编码为 1500（行38），但实际渲染内容因折叠状态不同差异很大。展开所有折叠面板（基础+能力+定义+视觉+变换+触发动画+3个方向覆写）时可能超过 1500，折叠时则浪费大量空白。对比 Weapon 面板用了动态计算 `viewRect.height = Mathf.Max(y + 10f, contentRect.height - 4f)`。",
      "recommendationMarkdown": "改为动态高度，在所有属性绘制完成后根据实际 y 值更新 viewRect.height。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-04",
      "severity": "low",
      "category": "accessibility",
      "title": "Equipment 面板按钮只用单字符标签 (+/A/C/T/-) 语义不清",
      "descriptionMarkdown": "DrawEquipmentPanel 的 8 个操作按钮使用 \"+\"、\"A\"、\"✈\"、\"-\"、\"C\"、\"T\"、\"↓\"、\"↑\" 等单字符标签。虽然有 Tooltip，但初次使用时用户无法快速理解含义，特别是 \"A\"（能力）、\"C\"（复制）、\"T\"（测试生成）等字母缩写不直觉。",
      "recommendationMarkdown": "建议至少使用两字符的缩写或语义图标（如有 Texture 图标资源），或在按钮宽度允许时使用短文本如\"新增\"、\"复制\"等。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-05",
      "severity": "medium",
      "category": "javascript",
      "title": "Weapon 面板 Slider 直接修改引用字段但变更检测在帧末",
      "descriptionMarkdown": "DrawWeaponPanel 中，UIHelper.DrawPropertySlider 直接通过 ref 修改 carry.drawOrder、carry.scale、carry.offset 等字段（行142-153），然后在帧末通过 AreCarryVisualConfigsEqual 与快照比较来触发 FinalizeMutatedEditorState。但 Slider 拖动时每帧都在修改值，而 CaptureUndoSnapshot 只在开头拍了一次快照 carrySnapshot。这意味着：1）拖动 Slider 过程中没有产生正确的 undo 记录；2）如果用户在同一帧内修改多个 Slider 也只会检测到一次变更。",
      "recommendationMarkdown": "给 Slider 变更也包裹 MutateWithUndo，或者采用 \"开始拖动时拍快照、结束拖动时提交\" 的模式来正确支持 Undo。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Weapon.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-06",
      "severity": "low",
      "category": "performance",
      "title": "Dialog_ApparelBrowser 使用 ToLower() 而非 OrdinalIgnoreCase 进行搜索过滤",
      "descriptionMarkdown": "FilterApparel 中使用 .ToLower().Contains(searchLower) 模式进行大小写不敏感搜索（行47-51），这对非英语区域（如土耳其语 I/İ）有潜在兼容问题，且每次搜索为每个条目创建小写副本字符串。",
      "recommendationMarkdown": "使用 IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 代替 ToLower().Contains()。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_ApparelBrowser.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-07",
      "severity": "medium",
      "category": "maintainability",
      "title": "Properties.Equipment 中 CaptureUndoSnapshot + MarkEquipmentDirty 模式不一致",
      "descriptionMarkdown": "DrawEquipmentProperties 方法中存在两种变更提交模式混用：1）部分属性使用 MutateWithUndo(() => {...}) 一步完成快照+变更+刷新；2）部分属性使用 CaptureUndoSnapshot(); 直接修改字段; MarkEquipmentDirty() 三步手动模式（如 VisualBase/VisualTransform/TriggeredAnimation 整个区域）。混用两种模式使代码约定不统一，增加理解和维护成本。",
      "recommendationMarkdown": "统一为一种模式。推荐所有变更使用 MutateEquipmentWithUndo 封装。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Equipment.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-UI-08",
      "severity": "low",
      "category": "performance",
      "title": "Equipment 面板 runtimeTestEquipmentDefs 静态字典无清理机制",
      "descriptionMarkdown": "runtimeTestEquipmentDefs 是一个 static readonly Dictionary，用于缓存测试生成的运行时 ThingDef。但没有任何清理/淘汰机制。在长时间使用编辑器反复测试生成时，这些 ThingDef 会持续累积在 DefDatabase 和缓存字典中，可能造成内存泄漏和 DefDatabase 膨胀。",
      "recommendationMarkdown": "添加清理机制：1）编辑器关闭时调用清理；2）或限制缓存大小实行 LRU 淘汰。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-RENDER-01",
      "severity": "medium",
      "category": "performance",
      "title": "Patch_WeaponRender.OffsetFor_Postfix 对所有 PawnRenderNodeWorker 子类生效",
      "descriptionMarkdown": "Patch_WeaponRender 将 Postfix 补丁应用到基类 PawnRenderNodeWorker.OffsetFor。由于 RimWorld 的渲染树每帧对每个 Pawn 的每个渲染节点都会调用 OffsetFor/ScaleFor，这意味着即使大多数节点不是武器节点，也都会进入 Postfix 逻辑执行 IsWeaponNode 检查。在大型殖民地（100+ Pawn）场景下可能成为性能热点。ApplyFlightStateOffset 也在此 Postfix 中执行，进一步增加了非武器节点的额外开销。",
      "recommendationMarkdown": "1) 考虑使用 TargetMethod 更精确地定位武器相关的 Worker 子类；2) 将 IsWeaponNode 的 tagDef 字符串检查结果缓存到节点实例上（Dictionary<PawnRenderNode, bool>），避免每帧重复字符串操作。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/Patch_WeaponRender.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M3"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-RENDER-02",
      "severity": "low",
      "category": "performance",
      "title": "WeaponCarryVisual 的 graphicCache 无大小限制且全局共享",
      "descriptionMarkdown": "PawnRenderNodeWorker_WeaponCarryVisual 中的 graphicCache 和 isMultiCache 均为 static 全局字典，无大小限制。若用户在编辑器中频繁切换不同贴图路径、shader 和颜色组合，缓存会持续增长。CanCacheGraphic 会清除加载失败的条目但不清除过时条目。",
      "recommendationMarkdown": "加入容量上限（如 128 条）或定期清理策略。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_WeaponCarryVisual.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M3"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-EXPORT-01",
      "severity": "high",
      "category": "accessibility",
      "title": "RecipeDef 导出中 label/jobString 使用硬编码中文",
      "descriptionMarkdown": "ModExportXmlWriter.GenerateEquipmentRecipeDefXml 中 label 和 jobString 直接使用硬编码中文字符串：`$\"制作{equipment.GetDisplayLabel()}\"` 和 `$\"正在制作{equipment.GetDisplayLabel()}\"`（行1003-1004）。导出的 RecipeDef XML 对非中文用户完全不可读。这是一个跨模块问题，但直接影响装备编辑器的导出质量。",
      "recommendationMarkdown": "1) 使用可翻译的模板字符串或让用户在编辑器中自定义 label/jobString；2) 至少回退到英文默认值 `Make {label}` / `Making {label}`。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Exporter/ModExportXmlWriter.cs"
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
    "bodyHash": "sha256:250ba54bd9ad44e5d6e3d71c33f344d0486c3c3f99e2ebcd6be1efeee69d1157",
    "generatedAt": "2026-04-08T21:50:35.100Z",
    "locale": "zh-CN"
  }
}
```
