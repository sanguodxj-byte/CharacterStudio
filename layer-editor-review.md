# CharacterStudio 图层编辑器代码审查
- 日期: 2026-04-08
- 概述: 对 CharacterStudio 图层编辑器 UI、数据模型、渲染管线进行全面代码审查
- 状态: 进行中
- 总体结论: 待定

## 评审范围

# CharacterStudio 图层编辑器代码审查

## 审查范围

- **UI 层**: `Dialog_SkinEditor` 全部 partial 文件（约 20 个）
- **数据模型**: `PawnLayerConfig`, `RenderNodeSnapshot`, `PawnSkinDef`
- **渲染管线**: `PawnRenderNodeWorker_CustomLayer`, `PawnRenderNode_Custom`
- **内省系统**: `RenderTreeParser`
- **辅助设施**: `EditorHistory`, `SkinEditorSession`, `UIHelper`

## 审查方法

逐文件阅读全部源码，结合上下游调用关系分析架构合理性、可维护性、性能和健壮性。

## 评审摘要

- 当前状态: 进行中
- 已审模块: Dialog_SkinEditor (全部 partial 文件), PawnLayerConfig, PawnRenderNodeWorker_CustomLayer, PawnRenderNode_Custom, RenderTreeParser, RenderNodeSnapshot, EditorHistory, SkinEditorSession, SkinEditorPreviewRefresher, UIHelper
- 当前进度: 已记录 1 个里程碑；最新：M1
- 里程碑总数: 1
- 已完成里程碑: 1
- 问题总数: 14
- 问题严重级别分布: 高 1 / 中 6 / 低 7
- 最新结论: 图层编辑器整体架构成熟度高，partial class 拆分策略合理，功能覆盖完整。发现 14 个问题点，其中 1 个高优先级、6 个中优先级、7 个低优先级。
- 下一步建议: 优先处理 F01（Worker 文件拆分）和 F07（MountLayer undo 缺失），然后系统性解决 F03（硬编码中文本地化）
- 总体结论: 待定

## 评审发现

### PawnRenderNodeWorker_CustomLayer 单文件体量过大

- ID: F01
- 严重级别: 高
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  PawnRenderNodeWorker_CustomLayer.cs 达到 3871 行，承载了图层渲染、纹理变体解析、所有动画类型计算、面部程序化变形（眼/瞳/眉/嘴/眼皮上下）、情感覆层语义路由、触发式装备动画、替换眼/嘴贴图回退链等大量职责。这是整个图层系统中最明显的技术债，维护成本极高，新开发者理解门槛极高。
- 建议:

  建议按职责拆分为多个 partial 文件或独立类：
  1. `PawnRenderNodeWorker_CustomLayer.Animation.cs` — Twitch/Swing/IdleSway/Breathe/Spin/Brownian 动画
  2. `PawnRenderNodeWorker_CustomLayer.Face.cs` — 面部程序化变形逻辑
  3. `PawnRenderNodeWorker_CustomLayer.Variant.cs` — 纹理变体/后缀解析
  4. `PawnRenderNodeWorker_CustomLayer.Overlay.cs` — 情感覆层语义路由
  5. `PawnRenderNodeWorker_CustomLayer.Equipment.cs` — 触发式装备动画
  保持 Worker 主类仅处理 CanDrawNow/OffsetFor/ScaleFor/RotationFor/GetGraphic 的调度，每个子方法调用对应的拆分模块。
- 证据:
  - `Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs`

### 属性面板滚动视图高度硬编码

- ID: F02
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  DrawSelectedLayerProperties 中 viewRect 高度硬编码为 1600，DrawNodeProperties 中硬编码为 820。当属性增加时容易出现内容截断或过大的空白区域。
- 建议:

  改为先在不可见 pass 中测量实际内容高度，然后用测量结果设置 viewRect.height，或使用累加式布局（类似 Listing_Standard 的模式），在绘制完成后回写高度。
- 证据:
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Layers.cs:54`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs:30`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Layers.cs`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs`

### 节点属性面板存在未本地化的硬编码中文

- ID: F03
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  Dialog_SkinEditor.Properties.Nodes.cs 中 "图层修改补丁" (L99)、"从补丁中显示节点" / "加入补丁隐藏节点" (L102)、"DrawOrder Offset" (L109)、"清除" (L140) 等字符串未走翻译键。Dialog_SkinEditor.Layout.cs 状态栏中也有 "图层修改工作流：隐藏节点…" (L335) 等硬编码中文。Dialog_SkinEditor.Properties.Panel.cs 的 "图层修改补丁属性" (L11) 同样未本地化。
- 建议:

  提取为 CS_Studio_Patch_* 系列翻译键，确保英文和中文 Keyed 文件同步添加对应条目。
- 证据:
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs:99-102`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs:335`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Panel.cs:11`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Panel.cs`

### PawnLayerConfig 字段过度膨胀

- ID: F04
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  PawnLayerConfig 类从 L100 到 L429 共约 330 行，拥有 60+ 个公有字段。涵盖基础变换、颜色、变体、动画（Twitch/Swing/IdleSway/Breathe/Spin/Brownian）、触发式装备动画（20+字段）等多种不相关关注点。Clone() 方法因此需要手动复制 80+ 个字段，新增字段极易遗漏。
- 建议:

  考虑将相关字段组合为嵌套配置结构：
  - `LayerTransformConfig` (offset/scale/rotation 相关)
  - `LayerAnimationConfig` (animType/freq/amp/brownian* 相关)
  - `LayerTriggeredAnimationConfig` (triggered* 相关)
  这样 Clone 可以调用子对象的 Clone，不再逐字段复制。
- 证据:
  - `Source/CharacterStudio/Core/PawnLayerConfig.cs`

### Undo/Redo 快照的 GC 压力

- ID: F05
- 严重级别: 中
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  EditorHistory 使用 CharacterDesignDocument.Clone() 进行完整深拷贝。每次调用 MutateWithUndo 都会先 SyncWorkingDocumentFromWorkingSkin（触发 RebuildNodeRules 遍历所有 layers 和 hiddenPaths），然后对整个 Document 做深拷贝。在滑块拖动等高频场景下会产生大量中间对象。undoMutationDepth 嵌套虽然可以防止重复快照，但频繁的单次 slider 调整仍会为每次微调创建完整快照。
- 建议:

  1. 对 slider 类控件使用 "延迟提交" 模式：拖动期间不创建 undo 快照，仅在 MouseUp 时提交一次。
  2. 或引入 undo 合并（coalescing）：连续同类操作在短时间窗口内合并为一个快照。
  3. 考虑增量 undo（记录 Action/Inverse-Action pair）替代全量快照，至少对高频属性修改场景。
- 证据:
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.SelectionUndo.cs`
  - `Source/CharacterStudio/UI/EditorHistory.cs`

### EditorHistory Stack 无固定容量，TrimUndoDepth 重建效率低

- ID: F06
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  TrimUndoDepth 在超过 maxDepth 时将整个 Stack 转为数组再重建，此操作 O(n)。虽然历史深度默认 100 且触发频率低，但使用 LinkedList 或自定义环形缓冲区可完全避免此开销。
- 建议:

  用固定大小的 CircularBuffer<Snapshot> 替代 Stack，自动丢弃最早的快照。
- 证据:
  - `Source/CharacterStudio/UI/EditorHistory.cs:143-157`
  - `Source/CharacterStudio/UI/EditorHistory.cs`

### MountLayer 操作缺少 Undo 保护

- ID: F07
- 严重级别: 中
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  在 DrawNodeProperties 的节点属性面板中（Properties.Nodes.cs L161-177），MountLayer 按钮直接修改 workingSkin.layers 和 workingDocument.nodeRules，设置 isDirty 并刷新预览，但 **未** 使用 MutateWithUndo 包裹。而在右键菜单（LayerTree.Nodes.cs L156-176）中同一操作则正确使用了 MutateWithUndo。这导致通过属性面板挂载图层后无法撤销。
- 建议:

  将 Properties.Nodes.cs L163-176 的操作包裹在 MutateWithUndo(() => { ... }) 中，与右键菜单保持一致。
- 证据:
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs:161-177`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs:156-176`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs`

### 图层面板工具栏按钮计算不精确

- ID: F08
- 严重级别: 低
- 分类: CSS
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  DrawLayerPanel 中 7 个按钮的宽度用 (rect.width - Margin * 8) / 7 计算，但实际排列使用 Margin * 1..7 递增间距，最后一个按钮的 x 坐标为 rect.x + Margin * 7 + btnWidth * 6，可能超出面板或留下间隙。这种手动 Rect 计算方式难以维护，在面板宽度变化时容易错位。
- 建议:

  封装一个 DrawToolbarRow 辅助方法，接受按钮数组并自动等分/对齐，消除魔数计算。
- 证据:
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Panel.cs:38-75`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Panel.cs`

### DrawNodeRow 递归深度无保护

- ID: F09
- 严重级别: 低
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  DrawNodeRow 递归遍历渲染树节点。虽然实际 RimWorld 渲染树深度很少超过 10 层，但缺少最大深度保护。如果某个 mod 错误地创建了循环引用或极深的树结构，可能导致 StackOverflowException。
- 建议:

  添加 maxDepth 参数（建议 50），超过时停止递归并显示截断提示。
- 证据:
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs:18-122`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs`

### SkinEditorSession 状态提取不完整

- ID: F10
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  SkinEditorSession 注释表明 "First extraction slice" 仅包含 IsDirty、SelectedLayerIndex、SelectedLayerIndices，"Additional editor state should be migrated here incrementally"。但 Dialog_SkinEditor 主类仍保有大量应属 session 的状态（如 selectedEquipmentIndex、selectedNodePath、selectedBaseSlotType、previewRotation 等）。Session 对象未真正承担其设计目标。
- 建议:

  继续按计划迁移：将 selectedEquipmentIndex、selectedNodePath、selectedBaseSlotType、previewRotation、previewZoom、currentTab 等迁入 SkinEditorSession，使 Dialog_SkinEditor 只持有 session 引用而非直接持有状态字段。
- 证据:
  - `Source/CharacterStudio/UI/SkinEditorSession.cs`

### PawnRenderNode_Custom 动画状态字段过多

- ID: F11
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  PawnRenderNode_Custom 类有 20+ 个动画状态字段（Twitch/Spin/Brownian/程序化面部/外部动画等），这些运行时可变状态与节点的配置/生命周期混合在一起。字段命名一致性好，但体量大。
- 建议:

  可考虑将动画状态封装为 AnimationState 子结构（或 sealed class），挂在 customNode.animState 上，保持节点类本身精简。
- 证据:
  - `Source/CharacterStudio/Rendering/PawnRenderNode_Custom.cs`

### DoWindowContents 异常时直接关闭窗口

- ID: F12
- 严重级别: 中
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  Dialog_SkinEditor.Layout.cs 的 DoWindowContents 在 catch 块中调用 Close()。这意味着任何子面板的瞬态异常（如贴图加载失败、空引用）都会导致整个编辑器关闭，用户丢失未保存工作。
- 建议:

  改为记录异常并显示错误状态消息，仅在连续异常超过阈值（如 3 帧内连续 3 次异常）时才关闭。或引入 error boundary 模式：跳过本帧的异常面板绘制，下帧重试。
- 证据:
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs:110-115`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs`

### textureExistsCache / frameSequenceCountCache 无失效机制

- ID: F13
- 严重级别: 低
- 分类: 性能
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  PawnRenderNodeWorker_CustomLayer 中的 textureExistsCache 和 frameSequenceCountCache 是静态字典，仅通过 ClearCache() 清除，运行期间会无限增长。如果用户在编辑器中频繁切换纹理路径，缓存条目会持续积累。
- 建议:

  添加 LRU 或定期清理策略（如每 5 分钟或地图切换时清除），或限制缓存条目上限。
- 证据:
  - `Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs`

### TryResolveBaseSlotType 匹配过于宽泛

- ID: F14
- 严重级别: 低
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: M1
- 说明:

  TryResolveBaseSlotType 使用 ContainsAny 在 tag/label/texPath 中搜索 "body"/"head"/"hair"/"beard" 子串。这种模糊匹配可能将 "HeadgearExtended" 误判为 Head，"Hairpin" 误判为 Hair。
- 建议:

  改用精确的 tag 匹配（PawnRenderNodeTagDef.defName 直接比较），仅在 tag 无法判定时才回退到子串匹配。
- 证据:
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Helpers.cs:72-84`
  - `Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Helpers.cs`

## 评审里程碑

### M1 · 图层编辑器全模块审查完成

- 状态: 已完成
- 记录时间: 2026-04-08T21:17:16.965Z
- 已审模块: Dialog_SkinEditor (全部 partial 文件), PawnLayerConfig, PawnRenderNodeWorker_CustomLayer, PawnRenderNode_Custom, RenderTreeParser, RenderNodeSnapshot, EditorHistory, SkinEditorSession, SkinEditorPreviewRefresher, UIHelper
- 摘要:

  完成了对图层编辑器 UI 架构、数据模型、渲染管线、Undo/Redo 系统、内省系统的全面审查。整体架构成熟度高，partial class 拆分合理，功能完备度很高。主要发现集中在：渲染工作器单文件体量过大（3871 行）、属性面板滚动视图高度硬编码、节点属性面板中存在未本地化硬编码中文、Undo 快照克隆可能产生 GC 压力、PawnLayerConfig 字段数量膨胀等方面。
- 结论:

  图层编辑器整体架构成熟度高，partial class 拆分策略合理，功能覆盖完整。发现 14 个问题点，其中 1 个高优先级、6 个中优先级、7 个低优先级。
- 下一步建议:

  优先处理 F01（Worker 文件拆分）和 F07（MountLayer undo 缺失），然后系统性解决 F03（硬编码中文本地化）
- 问题:
  - [高] 可维护性: PawnRenderNodeWorker_CustomLayer 单文件体量过大
  - [中] 可维护性: 属性面板滚动视图高度硬编码
  - [中] 可维护性: 节点属性面板存在未本地化的硬编码中文
  - [中] 可维护性: PawnLayerConfig 字段过度膨胀
  - [中] 性能: Undo/Redo 快照的 GC 压力
  - [低] 性能: EditorHistory Stack 无固定容量，TrimUndoDepth 重建效率低
  - [中] JavaScript: MountLayer 操作缺少 Undo 保护
  - [低] CSS: 图层面板工具栏按钮计算不精确
  - [低] JavaScript: DrawNodeRow 递归深度无保护
  - [低] 可维护性: SkinEditorSession 状态提取不完整
  - [低] 可维护性: PawnRenderNode_Custom 动画状态字段过多
  - [中] JavaScript: DoWindowContents 异常时直接关闭窗口
  - [低] 性能: textureExistsCache / frameSequenceCountCache 无失效机制
  - [低] JavaScript: TryResolveBaseSlotType 匹配过于宽泛

## 最终结论

图层编辑器整体架构成熟度高，partial class 拆分策略合理，功能覆盖完整。发现 14 个问题点，其中 1 个高优先级、6 个中优先级、7 个低优先级。

## 评审快照

```json
{
  "formatVersion": 4,
  "kind": "limcode.review",
  "reviewRunId": "review-mnqjjm0k-d0a8pm",
  "createdAt": "2026-04-08T00:00:00.000Z",
  "updatedAt": "2026-04-08T21:17:16.965Z",
  "finalizedAt": null,
  "status": "in_progress",
  "overallDecision": null,
  "header": {
    "title": "CharacterStudio 图层编辑器代码审查",
    "date": "2026-04-08",
    "overview": "对 CharacterStudio 图层编辑器 UI、数据模型、渲染管线进行全面代码审查"
  },
  "scope": {
    "markdown": "# CharacterStudio 图层编辑器代码审查\n\n## 审查范围\n\n- **UI 层**: `Dialog_SkinEditor` 全部 partial 文件（约 20 个）\n- **数据模型**: `PawnLayerConfig`, `RenderNodeSnapshot`, `PawnSkinDef`\n- **渲染管线**: `PawnRenderNodeWorker_CustomLayer`, `PawnRenderNode_Custom`\n- **内省系统**: `RenderTreeParser`\n- **辅助设施**: `EditorHistory`, `SkinEditorSession`, `UIHelper`\n\n## 审查方法\n\n逐文件阅读全部源码，结合上下游调用关系分析架构合理性、可维护性、性能和健壮性。"
  },
  "summary": {
    "latestConclusion": "图层编辑器整体架构成熟度高，partial class 拆分策略合理，功能覆盖完整。发现 14 个问题点，其中 1 个高优先级、6 个中优先级、7 个低优先级。",
    "recommendedNextAction": "优先处理 F01（Worker 文件拆分）和 F07（MountLayer undo 缺失），然后系统性解决 F03（硬编码中文本地化）",
    "reviewedModules": [
      "Dialog_SkinEditor (全部 partial 文件)",
      "PawnLayerConfig",
      "PawnRenderNodeWorker_CustomLayer",
      "PawnRenderNode_Custom",
      "RenderTreeParser",
      "RenderNodeSnapshot",
      "EditorHistory",
      "SkinEditorSession",
      "SkinEditorPreviewRefresher",
      "UIHelper"
    ]
  },
  "stats": {
    "totalMilestones": 1,
    "completedMilestones": 1,
    "totalFindings": 14,
    "severity": {
      "high": 1,
      "medium": 6,
      "low": 7
    }
  },
  "milestones": [
    {
      "id": "M1",
      "title": "图层编辑器全模块审查完成",
      "status": "completed",
      "recordedAt": "2026-04-08T21:17:16.965Z",
      "summaryMarkdown": "完成了对图层编辑器 UI 架构、数据模型、渲染管线、Undo/Redo 系统、内省系统的全面审查。整体架构成熟度高，partial class 拆分合理，功能完备度很高。主要发现集中在：渲染工作器单文件体量过大（3871 行）、属性面板滚动视图高度硬编码、节点属性面板中存在未本地化硬编码中文、Undo 快照克隆可能产生 GC 压力、PawnLayerConfig 字段数量膨胀等方面。",
      "conclusionMarkdown": "图层编辑器整体架构成熟度高，partial class 拆分策略合理，功能覆盖完整。发现 14 个问题点，其中 1 个高优先级、6 个中优先级、7 个低优先级。",
      "evidence": [],
      "reviewedModules": [
        "Dialog_SkinEditor (全部 partial 文件)",
        "PawnLayerConfig",
        "PawnRenderNodeWorker_CustomLayer",
        "PawnRenderNode_Custom",
        "RenderTreeParser",
        "RenderNodeSnapshot",
        "EditorHistory",
        "SkinEditorSession",
        "SkinEditorPreviewRefresher",
        "UIHelper"
      ],
      "recommendedNextAction": "优先处理 F01（Worker 文件拆分）和 F07（MountLayer undo 缺失），然后系统性解决 F03（硬编码中文本地化）",
      "findingIds": [
        "F01",
        "F02",
        "F03",
        "F04",
        "F05",
        "F06",
        "F07",
        "F08",
        "F09",
        "F10",
        "F11",
        "F12",
        "F13",
        "F14"
      ]
    }
  ],
  "findings": [
    {
      "id": "F01",
      "severity": "high",
      "category": "maintainability",
      "title": "PawnRenderNodeWorker_CustomLayer 单文件体量过大",
      "descriptionMarkdown": "PawnRenderNodeWorker_CustomLayer.cs 达到 3871 行，承载了图层渲染、纹理变体解析、所有动画类型计算、面部程序化变形（眼/瞳/眉/嘴/眼皮上下）、情感覆层语义路由、触发式装备动画、替换眼/嘴贴图回退链等大量职责。这是整个图层系统中最明显的技术债，维护成本极高，新开发者理解门槛极高。",
      "recommendationMarkdown": "建议按职责拆分为多个 partial 文件或独立类：\n1. `PawnRenderNodeWorker_CustomLayer.Animation.cs` — Twitch/Swing/IdleSway/Breathe/Spin/Brownian 动画\n2. `PawnRenderNodeWorker_CustomLayer.Face.cs` — 面部程序化变形逻辑\n3. `PawnRenderNodeWorker_CustomLayer.Variant.cs` — 纹理变体/后缀解析\n4. `PawnRenderNodeWorker_CustomLayer.Overlay.cs` — 情感覆层语义路由\n5. `PawnRenderNodeWorker_CustomLayer.Equipment.cs` — 触发式装备动画\n保持 Worker 主类仅处理 CanDrawNow/OffsetFor/ScaleFor/RotationFor/GetGraphic 的调度，每个子方法调用对应的拆分模块。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F02",
      "severity": "medium",
      "category": "maintainability",
      "title": "属性面板滚动视图高度硬编码",
      "descriptionMarkdown": "DrawSelectedLayerProperties 中 viewRect 高度硬编码为 1600，DrawNodeProperties 中硬编码为 820。当属性增加时容易出现内容截断或过大的空白区域。",
      "recommendationMarkdown": "改为先在不可见 pass 中测量实际内容高度，然后用测量结果设置 viewRect.height，或使用累加式布局（类似 Listing_Standard 的模式），在绘制完成后回写高度。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Layers.cs",
          "lineStart": 54,
          "lineEnd": 54
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs",
          "lineStart": 30,
          "lineEnd": 30
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Layers.cs"
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F03",
      "severity": "medium",
      "category": "maintainability",
      "title": "节点属性面板存在未本地化的硬编码中文",
      "descriptionMarkdown": "Dialog_SkinEditor.Properties.Nodes.cs 中 \"图层修改补丁\" (L99)、\"从补丁中显示节点\" / \"加入补丁隐藏节点\" (L102)、\"DrawOrder Offset\" (L109)、\"清除\" (L140) 等字符串未走翻译键。Dialog_SkinEditor.Layout.cs 状态栏中也有 \"图层修改工作流：隐藏节点…\" (L335) 等硬编码中文。Dialog_SkinEditor.Properties.Panel.cs 的 \"图层修改补丁属性\" (L11) 同样未本地化。",
      "recommendationMarkdown": "提取为 CS_Studio_Patch_* 系列翻译键，确保英文和中文 Keyed 文件同步添加对应条目。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs",
          "lineStart": 99,
          "lineEnd": 102
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs",
          "lineStart": 335,
          "lineEnd": 335
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Panel.cs",
          "lineStart": 11,
          "lineEnd": 11
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs"
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs"
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Panel.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F04",
      "severity": "medium",
      "category": "maintainability",
      "title": "PawnLayerConfig 字段过度膨胀",
      "descriptionMarkdown": "PawnLayerConfig 类从 L100 到 L429 共约 330 行，拥有 60+ 个公有字段。涵盖基础变换、颜色、变体、动画（Twitch/Swing/IdleSway/Breathe/Spin/Brownian）、触发式装备动画（20+字段）等多种不相关关注点。Clone() 方法因此需要手动复制 80+ 个字段，新增字段极易遗漏。",
      "recommendationMarkdown": "考虑将相关字段组合为嵌套配置结构：\n- `LayerTransformConfig` (offset/scale/rotation 相关)\n- `LayerAnimationConfig` (animType/freq/amp/brownian* 相关)\n- `LayerTriggeredAnimationConfig` (triggered* 相关)\n这样 Clone 可以调用子对象的 Clone，不再逐字段复制。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Core/PawnLayerConfig.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F05",
      "severity": "medium",
      "category": "performance",
      "title": "Undo/Redo 快照的 GC 压力",
      "descriptionMarkdown": "EditorHistory 使用 CharacterDesignDocument.Clone() 进行完整深拷贝。每次调用 MutateWithUndo 都会先 SyncWorkingDocumentFromWorkingSkin（触发 RebuildNodeRules 遍历所有 layers 和 hiddenPaths），然后对整个 Document 做深拷贝。在滑块拖动等高频场景下会产生大量中间对象。undoMutationDepth 嵌套虽然可以防止重复快照，但频繁的单次 slider 调整仍会为每次微调创建完整快照。",
      "recommendationMarkdown": "1. 对 slider 类控件使用 \"延迟提交\" 模式：拖动期间不创建 undo 快照，仅在 MouseUp 时提交一次。\n2. 或引入 undo 合并（coalescing）：连续同类操作在短时间窗口内合并为一个快照。\n3. 考虑增量 undo（记录 Action/Inverse-Action pair）替代全量快照，至少对高频属性修改场景。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.SelectionUndo.cs"
        },
        {
          "path": "Source/CharacterStudio/UI/EditorHistory.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F06",
      "severity": "low",
      "category": "performance",
      "title": "EditorHistory Stack 无固定容量，TrimUndoDepth 重建效率低",
      "descriptionMarkdown": "TrimUndoDepth 在超过 maxDepth 时将整个 Stack 转为数组再重建，此操作 O(n)。虽然历史深度默认 100 且触发频率低，但使用 LinkedList 或自定义环形缓冲区可完全避免此开销。",
      "recommendationMarkdown": "用固定大小的 CircularBuffer<Snapshot> 替代 Stack，自动丢弃最早的快照。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/EditorHistory.cs",
          "lineStart": 143,
          "lineEnd": 157
        },
        {
          "path": "Source/CharacterStudio/UI/EditorHistory.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F07",
      "severity": "medium",
      "category": "javascript",
      "title": "MountLayer 操作缺少 Undo 保护",
      "descriptionMarkdown": "在 DrawNodeProperties 的节点属性面板中（Properties.Nodes.cs L161-177），MountLayer 按钮直接修改 workingSkin.layers 和 workingDocument.nodeRules，设置 isDirty 并刷新预览，但 **未** 使用 MutateWithUndo 包裹。而在右键菜单（LayerTree.Nodes.cs L156-176）中同一操作则正确使用了 MutateWithUndo。这导致通过属性面板挂载图层后无法撤销。",
      "recommendationMarkdown": "将 Properties.Nodes.cs L163-176 的操作包裹在 MutateWithUndo(() => { ... }) 中，与右键菜单保持一致。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs",
          "lineStart": 161,
          "lineEnd": 177
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs",
          "lineStart": 156,
          "lineEnd": 176
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.Nodes.cs"
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F08",
      "severity": "low",
      "category": "css",
      "title": "图层面板工具栏按钮计算不精确",
      "descriptionMarkdown": "DrawLayerPanel 中 7 个按钮的宽度用 (rect.width - Margin * 8) / 7 计算，但实际排列使用 Margin * 1..7 递增间距，最后一个按钮的 x 坐标为 rect.x + Margin * 7 + btnWidth * 6，可能超出面板或留下间隙。这种手动 Rect 计算方式难以维护，在面板宽度变化时容易错位。",
      "recommendationMarkdown": "封装一个 DrawToolbarRow 辅助方法，接受按钮数组并自动等分/对齐，消除魔数计算。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Panel.cs",
          "lineStart": 38,
          "lineEnd": 75
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Panel.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F09",
      "severity": "low",
      "category": "javascript",
      "title": "DrawNodeRow 递归深度无保护",
      "descriptionMarkdown": "DrawNodeRow 递归遍历渲染树节点。虽然实际 RimWorld 渲染树深度很少超过 10 层，但缺少最大深度保护。如果某个 mod 错误地创建了循环引用或极深的树结构，可能导致 StackOverflowException。",
      "recommendationMarkdown": "添加 maxDepth 参数（建议 50），超过时停止递归并显示截断提示。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs",
          "lineStart": 18,
          "lineEnd": 122
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F10",
      "severity": "low",
      "category": "maintainability",
      "title": "SkinEditorSession 状态提取不完整",
      "descriptionMarkdown": "SkinEditorSession 注释表明 \"First extraction slice\" 仅包含 IsDirty、SelectedLayerIndex、SelectedLayerIndices，\"Additional editor state should be migrated here incrementally\"。但 Dialog_SkinEditor 主类仍保有大量应属 session 的状态（如 selectedEquipmentIndex、selectedNodePath、selectedBaseSlotType、previewRotation 等）。Session 对象未真正承担其设计目标。",
      "recommendationMarkdown": "继续按计划迁移：将 selectedEquipmentIndex、selectedNodePath、selectedBaseSlotType、previewRotation、previewZoom、currentTab 等迁入 SkinEditorSession，使 Dialog_SkinEditor 只持有 session 引用而非直接持有状态字段。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/SkinEditorSession.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F11",
      "severity": "low",
      "category": "maintainability",
      "title": "PawnRenderNode_Custom 动画状态字段过多",
      "descriptionMarkdown": "PawnRenderNode_Custom 类有 20+ 个动画状态字段（Twitch/Spin/Brownian/程序化面部/外部动画等），这些运行时可变状态与节点的配置/生命周期混合在一起。字段命名一致性好，但体量大。",
      "recommendationMarkdown": "可考虑将动画状态封装为 AnimationState 子结构（或 sealed class），挂在 customNode.animState 上，保持节点类本身精简。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Rendering/PawnRenderNode_Custom.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F12",
      "severity": "medium",
      "category": "javascript",
      "title": "DoWindowContents 异常时直接关闭窗口",
      "descriptionMarkdown": "Dialog_SkinEditor.Layout.cs 的 DoWindowContents 在 catch 块中调用 Close()。这意味着任何子面板的瞬态异常（如贴图加载失败、空引用）都会导致整个编辑器关闭，用户丢失未保存工作。",
      "recommendationMarkdown": "改为记录异常并显示错误状态消息，仅在连续异常超过阈值（如 3 帧内连续 3 次异常）时才关闭。或引入 error boundary 模式：跳过本帧的异常面板绘制，下帧重试。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs",
          "lineStart": 110,
          "lineEnd": 115
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F13",
      "severity": "low",
      "category": "performance",
      "title": "textureExistsCache / frameSequenceCountCache 无失效机制",
      "descriptionMarkdown": "PawnRenderNodeWorker_CustomLayer 中的 textureExistsCache 和 frameSequenceCountCache 是静态字典，仅通过 ClearCache() 清除，运行期间会无限增长。如果用户在编辑器中频繁切换纹理路径，缓存条目会持续积累。",
      "recommendationMarkdown": "添加 LRU 或定期清理策略（如每 5 分钟或地图切换时清除），或限制缓存条目上限。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F14",
      "severity": "low",
      "category": "javascript",
      "title": "TryResolveBaseSlotType 匹配过于宽泛",
      "descriptionMarkdown": "TryResolveBaseSlotType 使用 ContainsAny 在 tag/label/texPath 中搜索 \"body\"/\"head\"/\"hair\"/\"beard\" 子串。这种模糊匹配可能将 \"HeadgearExtended\" 误判为 Head，\"Hairpin\" 误判为 Hair。",
      "recommendationMarkdown": "改用精确的 tag 匹配（PawnRenderNodeTagDef.defName 直接比较），仅在 tag 无法判定时才回退到子串匹配。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Helpers.cs",
          "lineStart": 72,
          "lineEnd": 84
        },
        {
          "path": "Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Helpers.cs"
        }
      ],
      "relatedMilestoneIds": [
        "M1"
      ],
      "trackingStatus": "open"
    }
  ],
  "render": {
    "rendererVersion": 4,
    "bodyHash": "sha256:d8c48ffee9162bad094f2e0ac428c889075f06ff47bf3f7c0882cbf63a003d76",
    "generatedAt": "2026-04-08T21:17:16.965Z",
    "locale": "zh-CN"
  }
}
```
