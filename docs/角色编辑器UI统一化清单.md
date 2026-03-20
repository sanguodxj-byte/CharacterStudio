# 角色编辑器 UI 统一化清单

## 基准面板
- 以 [`DrawLayerPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Panel.cs:15)、[`DrawFacePanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:99)、[`DrawEquipmentPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs:19)、[`DrawWeaponPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Weapon.cs:10) 采用的“深色面板 + 细边框 + 软色标题条 + 标题底部强调线 + Tiny 标题字重 + 内层滚动容器”的结构为统一基准。
- 顶部工具条与状态栏的按钮基准参考 [`DrawTopMenu()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs:159) 与 [`DrawStatusBar()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs:290)。

## 已扫描到的未统一项

### 1. BaseAppearance 左侧面板仍使用旧式 `Widgets.DrawMenuSection`
- 位置：[`DrawBaseAppearancePanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.BaseAppearance.cs:14)
- 现状：直接用 `Widgets.DrawMenuSection(rect)` + `GameFont.Medium` 标题，没有统一的标题条、边框、高亮底线、工具区容器。
- 与基准差异：明显落后于 [`DrawLayerPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Panel.cs:15) / [`DrawFacePanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:99) / [`DrawWeaponPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Weapon.cs:10) 的统一卡片风格。
- 统一建议：改为统一面板壳层，标题区改为软底色标题条；列表滚动区域改为独立内层框；行选中边框改用统一边框色。

### 2. Attributes 左侧面板仍完全沿用旧属性页样式
- 位置：[`DrawAttributesPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs:22)
- 现状：整体仍是 `Widgets.DrawMenuSection(rect)` + `rect.ContractedBy(Margin)` 的传统属性布局，没有统一标题条、内容容器、信息卡片、工具按钮风格。
- 与基准差异：相比 [`DrawFacePanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:99) 与 [`DrawEquipmentPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs:19) 缺少层次感，视觉上更像旧版调试面板。
- 统一建议：补统一标题头；将 LLM 区块与基础属性区块包装为统一 section / banner / content box；生成按钮区切换为统一工具按钮视觉语言。

### 3. Preview 面板仍是旧式 `DrawMenuSection` + 原生 `ButtonText` 控件
- 位置：[`DrawPreviewPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs:281)
- 现状：外层使用 `Widgets.DrawMenuSection(rect)`，标题是 `GameFont.Medium` 普通文本；旋转、缩放、重置、Auto、速度、通道覆写按钮基本都直接使用 `Widgets.ButtonText(...)`。
- 与基准差异：缺少像 [`DrawTopMenu()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs:159) 那样的统一按钮填充、悬停描边、底部强调线，也没有像 [`DrawFaceToolbarButton()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:50) 那样的工具条按钮风格。
- 统一建议：
  - 外层壳统一为卡片式面板；
  - 标题栏统一为软色标题条；
  - 旋转/缩放/自动播放/覆盖控制按钮统一抽成预览工具按钮样式；
  - 预览画布区域与提示层使用单独内框，减少旧式窗口感。

### 4. 属性面板顶层仍是旧式标题 + 原生 +/- 折叠按钮
- 位置：[`DrawPropertiesPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.cs:22)
- 现状：整体用 `Widgets.DrawMenuSection(rect)`，标题仍是传统 `GameFont.Medium`，展开/折叠按钮直接用 `Widgets.ButtonText(...)`。
- 与基准差异：与 [`DrawTopMenu()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs:159) 或 [`DrawLayerPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Panel.cs:15) 的按钮风格不一致；缺少统一标题头与强化容器。
- 统一建议：
  - 顶部标题区改成统一标题条；
  - 全部展开/折叠按钮改成统一小型工具按钮；
  - 右侧滚动内容改为内层柔和背景框。

### 5. BaseAppearance 与 Attributes 没有与其他页一致的“标题区 + 工具区 + 内容区”三段式结构
- 位置：[`DrawBaseAppearancePanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.BaseAppearance.cs:14)、[`DrawAttributesPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs:22)
- 现状：结构更接近旧式单列属性页，没有中间缓冲和视觉分层。
- 与基准差异：相比 [`DrawEquipmentPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs:19) 与 [`DrawFacePanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:99)，信息密度大但缺乏组织层次。
- 统一建议：统一拆成：标题栏、可选工具栏/摘要栏、滚动内容区。

### 6. Preview 的控制按钮体系没有复用现有统一按钮视觉语言
- 位置：[`DrawPreviewPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs:281)
- 现状：同一面板内混用了 `◀` / `▶` / `-` / `+` / `↺` / `Auto` / 状态按钮，但视觉样式全部是系统默认 `ButtonText`。
- 与基准差异：图层页局部按钮在 [`DrawLayerPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Panel.cs:42) 已采用统一 hover/border/accent 语言；面部页在 [`DrawFaceToolbarButton()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:50) 也已经统一。
- 统一建议：复用同一套 toolbar button 绘制逻辑，避免编辑器内部出现三套按钮语义。

### 7. Preview 覆盖控制按钮与顶部菜单按钮的可视层级不一致
- 位置：[`DrawPreviewOverrideButton()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs:266)、[`DrawTopMenu()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Layout.cs:159)
- 现状：覆盖控制是普通 `ButtonText` 组合；顶部菜单已经具有 accent、hover、边框层次。
- 与基准差异：同属高频交互按钮，但预览区域视觉权重偏低，状态感不足。
- 统一建议：让预览覆写按钮至少具备选中态/悬停态/强调态。

### 8. Attributes 中 LLM 区域仍是裸 `Label + TextArea + ButtonText`
- 位置：[`DrawAttributesPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs:59)
- 现状：缺少统一信息卡和 section box，按钮也不是统一风格。
- 与基准差异：与 [`DrawFaceInfoBanner()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs:79) 这种信息提示卡相比，信息承载方式明显更粗糙。
- 统一建议：引入信息 banner、输入区外框、按钮条统一样式。

## 优先统一顺序
1. [`DrawPreviewPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs:281)
2. [`DrawBaseAppearancePanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.BaseAppearance.cs:14)
3. [`DrawAttributesPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Attributes.cs:22)
4. [`DrawPropertiesPanel()`](Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.cs:22)

## 本轮统一目标
- 先统一外层壳、标题栏、工具按钮、滚动内容容器。
- 不改变核心交互逻辑与字段行为。
- 不重做文案与布局语义，只收敛视觉语言。
