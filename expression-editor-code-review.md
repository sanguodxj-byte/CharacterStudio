# Expression Editor Code Review
- 日期: 2026-04-08
- 概述: 对 CharacterStudio 表情编辑器全部相关代码的架构、质量、可维护性进行全面审查
- 状态: 已完成
- 总体结论: 有条件通过

## 评审范围

# 表情编辑器代码审查

## 审查范围

- `Dialog_SkinEditor.Face.cs` — 表情面板 UI 绘制主入口（2608 行）
- `Dialog_SkinEditor.Face.AutoImport.cs` — 自动导入/扫描/识别逻辑（2127 行）
- `Dialog_FaceMovementSettings.cs` — 运动参数微调对话框（89 行）
- `PawnRenderNodeWorker_FaceComponent.cs` — 面部贴图渲染 Worker（333 行）
- `PawnRenderNodeWorker_EyeDirection.cs` — 眼睛方向覆盖层 Worker（191 行）
- `PawnFaceConfig.cs` — 核心数据模型
- `EyeDirectionConfig.cs` — 眼睛方向配置模型
- `RuntimeFace/FaceRuntimeModels.cs` — 运行时面部渲染模型

## 审查结论摘要

表情编辑器整体功能完整度很高，支持整脸换图和分层动态两种工作流，涵盖了眼/嘴/眉/瞳孔/情绪覆盖层等丰富的面部部件管理。自动导入系统的命名规则识别能力很强。但代码存在**文件膨胀严重、大量重复模式、国际化不彻底、运动参数编辑器缺少分组抽象**等问题。

---

## 评审摘要

- 当前状态: 已完成
- 已审模块: UI/Dialog_SkinEditor.Face.cs, UI/Dialog_SkinEditor.Face.AutoImport.cs, UI/Dialog_FaceMovementSettings.cs, Rendering/PawnRenderNodeWorker_FaceComponent.cs, Rendering/PawnRenderNodeWorker_EyeDirection.cs, UI/Dialog_SkinEditor.Face.cs (i18n), UI/Dialog_SkinEditor.Face.AutoImport.cs (logic), Rendering/PawnRenderNodeWorker_EyeDirection.cs (logic), Core/PawnFaceConfig.cs, Core/EyeDirectionConfig.cs
- 当前进度: 已记录 2 个里程碑；最新：M2
- 里程碑总数: 2
- 已完成里程碑: 2
- 问题总数: 11
- 问题严重级别分布: 高 2 / 中 5 / 低 4
- 最新结论: ## 总体评价 表情编辑器是 CharacterStudio 中功能最丰富、复杂度最高的模块之一。它成功实现了： - **双工作流架构**（FullFaceSwap / LayeredDynamic），能满足从简单整脸换图到精细分层表情的全谱需求 - **强大的自动导入系统**，支持约 50+ 种文件名模式的智能识别，包括方向变体、左右侧独立配置、表情语义推断 - **完善的运动微调系统**，覆盖眼/瞳孔/眉毛/嘴巴/情绪覆盖层 6 大类共 170+ 个可调参数 - **有效的 Undo 集成**，通过 MutateWithUndo 统一包裹所有变更操作 - **合理的缓存策略**，渲染 Worker 中对 Graphic 对象和 Multi 探测结果进行了适当缓存 主要问题集中在**可维护性**层面： 1. 两个核心文件（Face.cs + AutoImport.cs）合计约 4700 行，职责混合严重，需要进一步拆分 2. 运动参数编辑器采用逐行手写绑定（170+ 行 DrawFloatProperty），缺少数据驱动的元信息抽象 3. 国际化覆盖不完整，至少有 21+ 处硬编码中文字符串未走翻译系统 4. 两个渲染 Worker 之间存在大量复制粘贴的缓存基础设施代码 5. 存在少量死代码和不可达分支 这些问题不影响当前功能正确性，但会显著增加后续维护和扩展的成本。建议按优先级分批处理：P0 修复逻辑 bug（PERF-001 路径判断不一致、LOGIC-002 不可达代码），P1 完成 i18n 覆盖，P2 进行架构拆分与重复代码消除。
- 下一步建议: 按优先级处理：1) 修复 PERF-001 路径判断不一致（潜在运行时 bug）和 LOGIC-002 死代码；2) 完成 I18N-001 的全量国际化覆盖；3) 将 Face.cs 拆分为 5+ 个 partial class 文件以降低单文件认知负担；4) 考虑为运动参数编辑器引入数据驱动的元信息机制。
- 总体结论: 有条件通过

## 评审发现

### Face.cs 文件过度膨胀

- ID: ARCH-001
- 严重级别: 高
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  Dialog_SkinEditor.Face.cs 单文件达 2608 行，混合了: 1) 面板布局绘制 2) 文件名解析逻辑 3) 语义映射管理 4) 运动参数编辑器 5) 眼睛方向UI 6) 缓存管理 7) 数值工具函数。这些职责应该分拆为独立的 partial class 文件。
- 建议:

  建议至少分拆为: Face.Layout.cs（面板布局）、Face.SemanticMapping.cs（扫描与语义映射）、Face.MotionEditor.cs（运动参数编辑）、Face.EyeDirection.cs（眼睛方向UI）、Face.Helpers.cs（工具函数）
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs`

### AutoImport.cs 同样过大

- ID: ARCH-002
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  Dialog_SkinEditor.Face.AutoImport.cs 达 2127 行，包含完整的文件名解析引擎、方向变体解析、自动填充策略、图层同步等，已超过单一职责合理范围。AutoPopulateLayeredFacePartsFromDirectory 方法本身就超过 400 行。
- 建议:

  将文件名解析引擎（TryParseLayeredFaceFileName 等）提取为独立的静态工具类 FaceAutoImportParser；将图层同步逻辑提取为 FaceLayerSynchronizer。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs:317-735`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs`

### 两个渲染Worker间存在大量重复缓存代码

- ID: ARCH-003
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  PawnRenderNodeWorker_FaceComponent 和 PawnRenderNodeWorker_EyeDirection 各自独立维护了 graphicCache、isMultiCache、BuildGraphic、GetOrBuildGraphic、GetOrDetectIsMulti、CanCacheGraphic 等几乎完全相同的缓存基础设施代码。两者的 BuildGraphic 方法略有差异（判断外部路径的条件不同），但核心逻辑一致。
- 建议:

  提取公共基类 FaceRenderNodeWorkerBase 或静态辅助类 FaceGraphicCacheHelper，统一管理 Graphic 缓存逻辑。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs:249-321`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs:113-179`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs`

### 大量 UI 字符串硬编码中文

- ID: I18N-001
- 严重级别: 高
- 分类: 可访问性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  Face.cs 中存在至少 21 处硬编码中文字符串，涉及:
  - 第146行: "未选择 overlay" / "已选择 N 个 overlay"
  - 第448-451行: "注视方向附加偏移"、"正面左眼 X 偏移"、"正面右眼 X 偏移"、"侧面朝向 X 偏移"
  - 第505/578/1527行: "请先设置 layered source root。"
  - 第511/584/1533行: "目录不存在：..."
  - 第531/606/1556行: "扫描 Eye/Mouth/overlay 纹理失败：..."
  - 第1654行: "左侧"、"右侧"
  - 第2239-2242行: 运动微调对话框中重复的硬编码中文标签

  AutoImport.cs 中也有大量硬编码中文（MessageBox 内容、摘要报告等），但这些属于开发者工具提示，影响相对较小。

  所有这些都应使用 .Translate() 走国际化流程。
- 建议:

  1. 为所有硬编码字符串创建对应的 Keyed 翻译键
  2. 特别是运动参数编辑器中的 DrawPupilOffsetCoreSection 方法里与 DrawFaceTuningSections 中存在两处相同的硬编码中文标签，应统一到翻译系统
  3. Face.AutoImport.cs 中的 MessageBox 提示也应国际化
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:146`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:448-451`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:1654-1655`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs`

### 运动参数编辑器缺少数据驱动抽象（200+ 行重复调用）

- ID: UI-001
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  DrawFaceTuningSections 方法中，LidMotion 段有 58 个 DrawFloatProperty 调用、MouthMotion 段有 31 个、BrowMotion 段 13 个、EmotionOverlayMotion 段 17 个、EyeMotion 段 22 个、PupilMotion 段 26 个 —— 共计约 170+ 个几乎相同模式的手动绑定调用。每增加一个运动参数都需要在 UI 代码中手动添加一行。

  GetFaceSectionItemCount 方法（第2296-2309行）使用硬编码数字返回各段的项目数，与实际 DrawFloatProperty 调用数必须手动保持同步，极易出错。
- 建议:

  1. 定义运动参数元数据列表（字段名、显示键、范围、精度），用反射或数据结构自动驱动 UI 绘制
  2. 或者至少将 GetFaceSectionItemCount 改为基于实际字段数量的反射计算，避免魔法数字同步问题
  3. 考虑使用属性注解 [MotionParam("key", min, max, "format")] 来声明参数元信息
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:2311-2478`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:2296-2309`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs`

### 语义行绘制代码高度重复

- ID: UI-002
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  DrawScannedEyeSemanticRow、DrawScannedMouthSemanticRow、DrawScannedOverlaySemanticRow 三个方法结构几乎一致：都是绘制行背景 → 设置字体 → 截断文字 → 绘制按钮 → 添加 Tooltip。三个方法各约30-50行，共约120行可合并代码。同样，EnsureScannedEyeCandidates、EnsureScannedMouthCandidates、EnsureScannedOverlayCandidates 也是结构完全一致的缓存加载逻辑，只有解析函数不同。
- 建议:

  提取通用的 DrawScannedCandidateRow<T> 泛型方法和 EnsureScannedCandidates<T> 模板方法，通过委托/策略参数区分不同部件类型的解析逻辑。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:610-680`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:710-780`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:981-1029`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs`

### PupilOffsetCoreSection 中的参数范围与微调对话框不一致

- ID: UI-003
- 严重级别: 中
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  DrawPupilOffsetCoreSection（第442-457行）中 frontLeftEyeOffsetX 等参数的 slider 范围是 [-0.03, 0.03]，而 DrawFaceTuningSections 中 PupilMotion 段（第2239-2242行）对完全相同的字段使用了 [-0.01, 0.01] 的范围。两处绑定同一个数据源，但给用户提供了不同的可调范围，这会造成混淆。
- 建议:

  统一两处相同字段的 slider 范围定义，最好提取为常量或从参数元数据中读取。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:449-451`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:2240-2242`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs`

### TryParseReplacementEyeCandidate default 分支总返回 true

- ID: LOGIC-001
- 严重级别: 低
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  第914行: `default: return Enum.TryParse(semanticToken, true, out expression) || true;` — 这个表达式的 `|| true` 意味着无论 Enum.TryParse 是否成功，default 分支始终返回 true。如果意图是 "尝试解析为枚举，失败则仍然接受为候选"，逻辑是正确的但 expression 此时可能残留为 Neutral（因 TryParse 失败时 out 参数为默认值），应加注释说明。TryParseReplacementMouthCandidate（第956行）有同样的模式。
- 建议:

  如果意图是始终接受未知语义 token，建议添加注释说明设计意图；或者改为显式 `expression = ExpressionType.Neutral; return true;` 以清晰化。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:914`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:956`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs`

### TryParseViewFacingToken 末尾存在不可达的重复代码

- ID: LOGIC-002
- 严重级别: 低
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  AutoImport.cs 第1931-1958行的 TryParseViewFacingToken 方法中，第1948-1953行已经处理了 "south" 的情况（以及 "back"、"front"），但第1955-1958行又重复检查了一次 "south"。这段代码不可达。
- 建议:

  删除第1955-1958行的重复 south 检查。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs:1931-1959`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs`

### EyeDirection Worker 外部路径判断策略不一致

- ID: PERF-001
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  FaceComponent 的 BuildGraphic 使用 `Path.IsPathRooted(path)` 判断外部路径，而 EyeDirection 的 BuildGraphic 使用 `Path.IsPathRooted(path) || path.EndsWith(".png", ...)` 判断。后者会导致类似 `Textures/MyMod/face.png` 这样的游戏内路径也被当作外部文件处理，可能引发 Graphic_Runtime 初始化失败。
- 建议:

  统一两个 Worker 的外部路径判断逻辑，推荐仅使用 Path.IsPathRooted。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs:278`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs:144`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs`

### DrawLayeredPartRow 中存在已失效的 isOverlay 分支

- ID: UI-004
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M2
- 说明:

  DrawLayeredPartRow 方法（第1658-1862行）在第1682-1685行已经对 isOverlay 类型执行了 DrawCompactOverlayPartRow 并 return，但后续的逻辑中（第1700-1704行、第1721-1723行等）仍然包含大量 `if (isOverlay)` 的分支判断。这些分支在当前控制流下永远不会被执行，是历史遗留的死代码。
- 建议:

  清理 DrawLayeredPartRow 中第1684行 return 之后所有的 isOverlay 分支代码，简化方法逻辑。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:1682-1705`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs`

## 评审里程碑

### M1 · 架构与代码组织审查

- 状态: 已完成
- 记录时间: 2026-04-08T22:07:51.636Z
- 已审模块: UI/Dialog_SkinEditor.Face.cs, UI/Dialog_SkinEditor.Face.AutoImport.cs, UI/Dialog_FaceMovementSettings.cs, Rendering/PawnRenderNodeWorker_FaceComponent.cs, Rendering/PawnRenderNodeWorker_EyeDirection.cs
- 摘要:

  对表情编辑器整体架构、文件组织、类职责划分进行审查。表情编辑器功能模块分布在两个 partial class 文件中（Face.cs 2608行 + Face.AutoImport.cs 2127行 = 共约4735行），加上独立对话框、2个渲染Worker、数据模型层和运行时编译层。
- 结论:

  对表情编辑器整体架构、文件组织、类职责划分进行审查。表情编辑器功能模块分布在两个 partial class 文件中（Face.cs 2608行 + Face.AutoImport.cs 2127行 = 共约4735行），加上独立对话框、2个渲染Worker、数据模型层和运行时编译层。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:1-2608`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs:1-2127`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_FaceMovementSettings.cs`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs`
- 问题:
  - [高] 可维护性: Face.cs 文件过度膨胀
  - [中] 可维护性: AutoImport.cs 同样过大
  - [中] 可维护性: 两个渲染Worker间存在大量重复缓存代码

### M2 · 国际化 (i18n)、代码质量与 UI 模式审查

- 状态: 已完成
- 记录时间: 2026-04-08T22:09:22.593Z
- 已审模块: UI/Dialog_SkinEditor.Face.cs (i18n), UI/Dialog_SkinEditor.Face.AutoImport.cs (logic), Rendering/PawnRenderNodeWorker_EyeDirection.cs (logic)
- 摘要:

  对表情编辑器中的国际化合规性、UI 绘制模式重复度、代码逻辑质量进行深度审查。发现大量硬编码中文字符串、重复的 UI 行绘制模式、运动参数编辑器缺少数据驱动抽象、以及若干值得关注的逻辑细节问题。
- 结论:

  对表情编辑器中的国际化合规性、UI 绘制模式重复度、代码逻辑质量进行深度审查。发现大量硬编码中文字符串、重复的 UI 行绘制模式、运动参数编辑器缺少数据驱动抽象、以及若干值得关注的逻辑细节问题。
- 证据:
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:146`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:448-456`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:505-531`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:1654-1655`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:2196-2267`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:2296-2309`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:2481-2535`
  - `CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs:105-127`
  - `CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs:139-158`
- 问题:
  - [高] 可访问性: 大量 UI 字符串硬编码中文
  - [中] 可维护性: 运动参数编辑器缺少数据驱动抽象（200+ 行重复调用）
  - [中] 可维护性: 语义行绘制代码高度重复
  - [中] JavaScript: PupilOffsetCoreSection 中的参数范围与微调对话框不一致
  - [低] JavaScript: TryParseReplacementEyeCandidate default 分支总返回 true
  - [低] JavaScript: TryParseViewFacingToken 末尾存在不可达的重复代码
  - [低] 性能: EyeDirection Worker 外部路径判断策略不一致
  - [低] 可维护性: DrawLayeredPartRow 中存在已失效的 isOverlay 分支

## 最终结论

## 总体评价

表情编辑器是 CharacterStudio 中功能最丰富、复杂度最高的模块之一。它成功实现了：
- **双工作流架构**（FullFaceSwap / LayeredDynamic），能满足从简单整脸换图到精细分层表情的全谱需求
- **强大的自动导入系统**，支持约 50+ 种文件名模式的智能识别，包括方向变体、左右侧独立配置、表情语义推断
- **完善的运动微调系统**，覆盖眼/瞳孔/眉毛/嘴巴/情绪覆盖层 6 大类共 170+ 个可调参数
- **有效的 Undo 集成**，通过 MutateWithUndo 统一包裹所有变更操作
- **合理的缓存策略**，渲染 Worker 中对 Graphic 对象和 Multi 探测结果进行了适当缓存

主要问题集中在**可维护性**层面：
1. 两个核心文件（Face.cs + AutoImport.cs）合计约 4700 行，职责混合严重，需要进一步拆分
2. 运动参数编辑器采用逐行手写绑定（170+ 行 DrawFloatProperty），缺少数据驱动的元信息抽象
3. 国际化覆盖不完整，至少有 21+ 处硬编码中文字符串未走翻译系统
4. 两个渲染 Worker 之间存在大量复制粘贴的缓存基础设施代码
5. 存在少量死代码和不可达分支

这些问题不影响当前功能正确性，但会显著增加后续维护和扩展的成本。建议按优先级分批处理：P0 修复逻辑 bug（PERF-001 路径判断不一致、LOGIC-002 不可达代码），P1 完成 i18n 覆盖，P2 进行架构拆分与重复代码消除。

## 评审快照

```json
{
  "formatVersion": 4,
  "kind": "limcode.review",
  "reviewRunId": "review-mnqljrv6-7fpn05",
  "createdAt": "2026-04-08T00:00:00.000Z",
  "updatedAt": "2026-04-08T22:10:28.714Z",
  "finalizedAt": "2026-04-08T22:10:28.714Z",
  "status": "completed",
  "overallDecision": "conditionally_accepted",
  "header": {
    "title": "Expression Editor Code Review",
    "date": "2026-04-08",
    "overview": "对 CharacterStudio 表情编辑器全部相关代码的架构、质量、可维护性进行全面审查"
  },
  "scope": {
    "markdown": "# 表情编辑器代码审查\n\n## 审查范围\n\n- `Dialog_SkinEditor.Face.cs` — 表情面板 UI 绘制主入口（2608 行）\n- `Dialog_SkinEditor.Face.AutoImport.cs` — 自动导入/扫描/识别逻辑（2127 行）\n- `Dialog_FaceMovementSettings.cs` — 运动参数微调对话框（89 行）\n- `PawnRenderNodeWorker_FaceComponent.cs` — 面部贴图渲染 Worker（333 行）\n- `PawnRenderNodeWorker_EyeDirection.cs` — 眼睛方向覆盖层 Worker（191 行）\n- `PawnFaceConfig.cs` — 核心数据模型\n- `EyeDirectionConfig.cs` — 眼睛方向配置模型\n- `RuntimeFace/FaceRuntimeModels.cs` — 运行时面部渲染模型\n\n## 审查结论摘要\n\n表情编辑器整体功能完整度很高，支持整脸换图和分层动态两种工作流，涵盖了眼/嘴/眉/瞳孔/情绪覆盖层等丰富的面部部件管理。自动导入系统的命名规则识别能力很强。但代码存在**文件膨胀严重、大量重复模式、国际化不彻底、运动参数编辑器缺少分组抽象**等问题。\n\n---"
  },
  "summary": {
    "latestConclusion": "## 总体评价\n\n表情编辑器是 CharacterStudio 中功能最丰富、复杂度最高的模块之一。它成功实现了：\n- **双工作流架构**（FullFaceSwap / LayeredDynamic），能满足从简单整脸换图到精细分层表情的全谱需求\n- **强大的自动导入系统**，支持约 50+ 种文件名模式的智能识别，包括方向变体、左右侧独立配置、表情语义推断\n- **完善的运动微调系统**，覆盖眼/瞳孔/眉毛/嘴巴/情绪覆盖层 6 大类共 170+ 个可调参数\n- **有效的 Undo 集成**，通过 MutateWithUndo 统一包裹所有变更操作\n- **合理的缓存策略**，渲染 Worker 中对 Graphic 对象和 Multi 探测结果进行了适当缓存\n\n主要问题集中在**可维护性**层面：\n1. 两个核心文件（Face.cs + AutoImport.cs）合计约 4700 行，职责混合严重，需要进一步拆分\n2. 运动参数编辑器采用逐行手写绑定（170+ 行 DrawFloatProperty），缺少数据驱动的元信息抽象\n3. 国际化覆盖不完整，至少有 21+ 处硬编码中文字符串未走翻译系统\n4. 两个渲染 Worker 之间存在大量复制粘贴的缓存基础设施代码\n5. 存在少量死代码和不可达分支\n\n这些问题不影响当前功能正确性，但会显著增加后续维护和扩展的成本。建议按优先级分批处理：P0 修复逻辑 bug（PERF-001 路径判断不一致、LOGIC-002 不可达代码），P1 完成 i18n 覆盖，P2 进行架构拆分与重复代码消除。",
    "recommendedNextAction": "按优先级处理：1) 修复 PERF-001 路径判断不一致（潜在运行时 bug）和 LOGIC-002 死代码；2) 完成 I18N-001 的全量国际化覆盖；3) 将 Face.cs 拆分为 5+ 个 partial class 文件以降低单文件认知负担；4) 考虑为运动参数编辑器引入数据驱动的元信息机制。",
    "reviewedModules": [
      "UI/Dialog_SkinEditor.Face.cs",
      "UI/Dialog_SkinEditor.Face.AutoImport.cs",
      "UI/Dialog_FaceMovementSettings.cs",
      "Rendering/PawnRenderNodeWorker_FaceComponent.cs",
      "Rendering/PawnRenderNodeWorker_EyeDirection.cs",
      "UI/Dialog_SkinEditor.Face.cs (i18n)",
      "UI/Dialog_SkinEditor.Face.AutoImport.cs (logic)",
      "Rendering/PawnRenderNodeWorker_EyeDirection.cs (logic)",
      "Core/PawnFaceConfig.cs",
      "Core/EyeDirectionConfig.cs"
    ]
  },
  "stats": {
    "totalMilestones": 2,
    "completedMilestones": 2,
    "totalFindings": 11,
    "severity": {
      "high": 2,
      "medium": 5,
      "low": 4
    }
  },
  "milestones": [
    {
      "id": "M1",
      "title": "架构与代码组织审查",
      "status": "completed",
      "recordedAt": "2026-04-08T22:07:51.636Z",
      "summaryMarkdown": "对表情编辑器整体架构、文件组织、类职责划分进行审查。表情编辑器功能模块分布在两个 partial class 文件中（Face.cs 2608行 + Face.AutoImport.cs 2127行 = 共约4735行），加上独立对话框、2个渲染Worker、数据模型层和运行时编译层。",
      "conclusionMarkdown": "对表情编辑器整体架构、文件组织、类职责划分进行审查。表情编辑器功能模块分布在两个 partial class 文件中（Face.cs 2608行 + Face.AutoImport.cs 2127行 = 共约4735行），加上独立对话框、2个渲染Worker、数据模型层和运行时编译层。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 1,
          "lineEnd": 2608
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs",
          "lineStart": 1,
          "lineEnd": 2127
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_FaceMovementSettings.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs"
        }
      ],
      "reviewedModules": [
        "UI/Dialog_SkinEditor.Face.cs",
        "UI/Dialog_SkinEditor.Face.AutoImport.cs",
        "UI/Dialog_FaceMovementSettings.cs",
        "Rendering/PawnRenderNodeWorker_FaceComponent.cs",
        "Rendering/PawnRenderNodeWorker_EyeDirection.cs"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "ARCH-001",
        "ARCH-002",
        "ARCH-003"
      ]
    },
    {
      "id": "M2",
      "title": "国际化 (i18n)、代码质量与 UI 模式审查",
      "status": "completed",
      "recordedAt": "2026-04-08T22:09:22.593Z",
      "summaryMarkdown": "对表情编辑器中的国际化合规性、UI 绘制模式重复度、代码逻辑质量进行深度审查。发现大量硬编码中文字符串、重复的 UI 行绘制模式、运动参数编辑器缺少数据驱动抽象、以及若干值得关注的逻辑细节问题。",
      "conclusionMarkdown": "对表情编辑器中的国际化合规性、UI 绘制模式重复度、代码逻辑质量进行深度审查。发现大量硬编码中文字符串、重复的 UI 行绘制模式、运动参数编辑器缺少数据驱动抽象、以及若干值得关注的逻辑细节问题。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 146,
          "lineEnd": 146
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 448,
          "lineEnd": 456
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 505,
          "lineEnd": 531
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 1654,
          "lineEnd": 1655
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 2196,
          "lineEnd": 2267
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 2296,
          "lineEnd": 2309
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 2481,
          "lineEnd": 2535
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs",
          "lineStart": 105,
          "lineEnd": 127
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs",
          "lineStart": 139,
          "lineEnd": 158
        }
      ],
      "reviewedModules": [
        "UI/Dialog_SkinEditor.Face.cs (i18n)",
        "UI/Dialog_SkinEditor.Face.AutoImport.cs (logic)",
        "Rendering/PawnRenderNodeWorker_EyeDirection.cs (logic)"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "I18N-001",
        "UI-001",
        "UI-002",
        "UI-003",
        "LOGIC-001",
        "LOGIC-002",
        "PERF-001",
        "UI-004"
      ]
    }
  ],
  "findings": [
    {
      "id": "ARCH-001",
      "severity": "high",
      "category": "maintainability",
      "title": "Face.cs 文件过度膨胀",
      "descriptionMarkdown": "Dialog_SkinEditor.Face.cs 单文件达 2608 行，混合了: 1) 面板布局绘制 2) 文件名解析逻辑 3) 语义映射管理 4) 运动参数编辑器 5) 眼睛方向UI 6) 缓存管理 7) 数值工具函数。这些职责应该分拆为独立的 partial class 文件。",
      "recommendationMarkdown": "建议至少分拆为: Face.Layout.cs（面板布局）、Face.SemanticMapping.cs（扫描与语义映射）、Face.MotionEditor.cs（运动参数编辑）、Face.EyeDirection.cs（眼睛方向UI）、Face.Helpers.cs（工具函数）",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "ARCH-002",
      "severity": "medium",
      "category": "maintainability",
      "title": "AutoImport.cs 同样过大",
      "descriptionMarkdown": "Dialog_SkinEditor.Face.AutoImport.cs 达 2127 行，包含完整的文件名解析引擎、方向变体解析、自动填充策略、图层同步等，已超过单一职责合理范围。AutoPopulateLayeredFacePartsFromDirectory 方法本身就超过 400 行。",
      "recommendationMarkdown": "将文件名解析引擎（TryParseLayeredFaceFileName 等）提取为独立的静态工具类 FaceAutoImportParser；将图层同步逻辑提取为 FaceLayerSynchronizer。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs",
          "lineStart": 317,
          "lineEnd": 735
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "ARCH-003",
      "severity": "medium",
      "category": "maintainability",
      "title": "两个渲染Worker间存在大量重复缓存代码",
      "descriptionMarkdown": "PawnRenderNodeWorker_FaceComponent 和 PawnRenderNodeWorker_EyeDirection 各自独立维护了 graphicCache、isMultiCache、BuildGraphic、GetOrBuildGraphic、GetOrDetectIsMulti、CanCacheGraphic 等几乎完全相同的缓存基础设施代码。两者的 BuildGraphic 方法略有差异（判断外部路径的条件不同），但核心逻辑一致。",
      "recommendationMarkdown": "提取公共基类 FaceRenderNodeWorkerBase 或静态辅助类 FaceGraphicCacheHelper，统一管理 Graphic 缓存逻辑。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs",
          "lineStart": 249,
          "lineEnd": 321
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs",
          "lineStart": 113,
          "lineEnd": 179
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "I18N-001",
      "severity": "high",
      "category": "accessibility",
      "title": "大量 UI 字符串硬编码中文",
      "descriptionMarkdown": "Face.cs 中存在至少 21 处硬编码中文字符串，涉及:\n- 第146行: \"未选择 overlay\" / \"已选择 N 个 overlay\"\n- 第448-451行: \"注视方向附加偏移\"、\"正面左眼 X 偏移\"、\"正面右眼 X 偏移\"、\"侧面朝向 X 偏移\"\n- 第505/578/1527行: \"请先设置 layered source root。\"\n- 第511/584/1533行: \"目录不存在：...\"\n- 第531/606/1556行: \"扫描 Eye/Mouth/overlay 纹理失败：...\"\n- 第1654行: \"左侧\"、\"右侧\"\n- 第2239-2242行: 运动微调对话框中重复的硬编码中文标签\n\nAutoImport.cs 中也有大量硬编码中文（MessageBox 内容、摘要报告等），但这些属于开发者工具提示，影响相对较小。\n\n所有这些都应使用 .Translate() 走国际化流程。",
      "recommendationMarkdown": "1. 为所有硬编码字符串创建对应的 Keyed 翻译键\n2. 特别是运动参数编辑器中的 DrawPupilOffsetCoreSection 方法里与 DrawFaceTuningSections 中存在两处相同的硬编码中文标签，应统一到翻译系统\n3. Face.AutoImport.cs 中的 MessageBox 提示也应国际化",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 146,
          "lineEnd": 146
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 448,
          "lineEnd": 451
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 1654,
          "lineEnd": 1655
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "UI-001",
      "severity": "medium",
      "category": "maintainability",
      "title": "运动参数编辑器缺少数据驱动抽象（200+ 行重复调用）",
      "descriptionMarkdown": "DrawFaceTuningSections 方法中，LidMotion 段有 58 个 DrawFloatProperty 调用、MouthMotion 段有 31 个、BrowMotion 段 13 个、EmotionOverlayMotion 段 17 个、EyeMotion 段 22 个、PupilMotion 段 26 个 —— 共计约 170+ 个几乎相同模式的手动绑定调用。每增加一个运动参数都需要在 UI 代码中手动添加一行。\n\nGetFaceSectionItemCount 方法（第2296-2309行）使用硬编码数字返回各段的项目数，与实际 DrawFloatProperty 调用数必须手动保持同步，极易出错。",
      "recommendationMarkdown": "1. 定义运动参数元数据列表（字段名、显示键、范围、精度），用反射或数据结构自动驱动 UI 绘制\n2. 或者至少将 GetFaceSectionItemCount 改为基于实际字段数量的反射计算，避免魔法数字同步问题\n3. 考虑使用属性注解 [MotionParam(\"key\", min, max, \"format\")] 来声明参数元信息",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 2311,
          "lineEnd": 2478
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 2296,
          "lineEnd": 2309
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "UI-002",
      "severity": "medium",
      "category": "maintainability",
      "title": "语义行绘制代码高度重复",
      "descriptionMarkdown": "DrawScannedEyeSemanticRow、DrawScannedMouthSemanticRow、DrawScannedOverlaySemanticRow 三个方法结构几乎一致：都是绘制行背景 → 设置字体 → 截断文字 → 绘制按钮 → 添加 Tooltip。三个方法各约30-50行，共约120行可合并代码。同样，EnsureScannedEyeCandidates、EnsureScannedMouthCandidates、EnsureScannedOverlayCandidates 也是结构完全一致的缓存加载逻辑，只有解析函数不同。",
      "recommendationMarkdown": "提取通用的 DrawScannedCandidateRow<T> 泛型方法和 EnsureScannedCandidates<T> 模板方法，通过委托/策略参数区分不同部件类型的解析逻辑。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 610,
          "lineEnd": 680
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 710,
          "lineEnd": 780
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 981,
          "lineEnd": 1029
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "UI-003",
      "severity": "medium",
      "category": "javascript",
      "title": "PupilOffsetCoreSection 中的参数范围与微调对话框不一致",
      "descriptionMarkdown": "DrawPupilOffsetCoreSection（第442-457行）中 frontLeftEyeOffsetX 等参数的 slider 范围是 [-0.03, 0.03]，而 DrawFaceTuningSections 中 PupilMotion 段（第2239-2242行）对完全相同的字段使用了 [-0.01, 0.01] 的范围。两处绑定同一个数据源，但给用户提供了不同的可调范围，这会造成混淆。",
      "recommendationMarkdown": "统一两处相同字段的 slider 范围定义，最好提取为常量或从参数元数据中读取。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 449,
          "lineEnd": 451
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 2240,
          "lineEnd": 2242
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "LOGIC-001",
      "severity": "low",
      "category": "javascript",
      "title": "TryParseReplacementEyeCandidate default 分支总返回 true",
      "descriptionMarkdown": "第914行: `default: return Enum.TryParse(semanticToken, true, out expression) || true;` — 这个表达式的 `|| true` 意味着无论 Enum.TryParse 是否成功，default 分支始终返回 true。如果意图是 \"尝试解析为枚举，失败则仍然接受为候选\"，逻辑是正确的但 expression 此时可能残留为 Neutral（因 TryParse 失败时 out 参数为默认值），应加注释说明。TryParseReplacementMouthCandidate（第956行）有同样的模式。",
      "recommendationMarkdown": "如果意图是始终接受未知语义 token，建议添加注释说明设计意图；或者改为显式 `expression = ExpressionType.Neutral; return true;` 以清晰化。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 914,
          "lineEnd": 914
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 956,
          "lineEnd": 956
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "LOGIC-002",
      "severity": "low",
      "category": "javascript",
      "title": "TryParseViewFacingToken 末尾存在不可达的重复代码",
      "descriptionMarkdown": "AutoImport.cs 第1931-1958行的 TryParseViewFacingToken 方法中，第1948-1953行已经处理了 \"south\" 的情况（以及 \"back\"、\"front\"），但第1955-1958行又重复检查了一次 \"south\"。这段代码不可达。",
      "recommendationMarkdown": "删除第1955-1958行的重复 south 检查。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs",
          "lineStart": 1931,
          "lineEnd": 1959
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.AutoImport.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "PERF-001",
      "severity": "low",
      "category": "performance",
      "title": "EyeDirection Worker 外部路径判断策略不一致",
      "descriptionMarkdown": "FaceComponent 的 BuildGraphic 使用 `Path.IsPathRooted(path)` 判断外部路径，而 EyeDirection 的 BuildGraphic 使用 `Path.IsPathRooted(path) || path.EndsWith(\".png\", ...)` 判断。后者会导致类似 `Textures/MyMod/face.png` 这样的游戏内路径也被当作外部文件处理，可能引发 Graphic_Runtime 初始化失败。",
      "recommendationMarkdown": "统一两个 Worker 的外部路径判断逻辑，推荐仅使用 Path.IsPathRooted。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs",
          "lineStart": 278,
          "lineEnd": 278
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs",
          "lineStart": 144,
          "lineEnd": 144
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_FaceComponent.cs"
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/Rendering/PawnRenderNodeWorker_EyeDirection.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M2"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "UI-004",
      "severity": "low",
      "category": "maintainability",
      "title": "DrawLayeredPartRow 中存在已失效的 isOverlay 分支",
      "descriptionMarkdown": "DrawLayeredPartRow 方法（第1658-1862行）在第1682-1685行已经对 isOverlay 类型执行了 DrawCompactOverlayPartRow 并 return，但后续的逻辑中（第1700-1704行、第1721-1723行等）仍然包含大量 `if (isOverlay)` 的分支判断。这些分支在当前控制流下永远不会被执行，是历史遗留的死代码。",
      "recommendationMarkdown": "清理 DrawLayeredPartRow 中第1684行 return 之后所有的 isOverlay 分支代码，简化方法逻辑。",
      "evidence": [
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs",
          "lineStart": 1682,
          "lineEnd": 1705
        },
        {
          "path": "CharacterStudio/Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs"
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
    "bodyHash": "sha256:bf03ac251dea0160fd8b303a239af39476d9712bff21f19bd76dcd64cfb4eb0d",
    "generatedAt": "2026-04-08T22:10:28.714Z",
    "locale": "zh-CN"
  }
}
```
