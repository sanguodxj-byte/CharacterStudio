# CharacterStudio 全项目 UI 审查报告

## 审查目标

本次审查针对 CharacterStudio 当前全部主要编辑器与辅助对话框 UI，重点核查以下问题：

- 是否存在未翻译文本、纯英文可见键名、硬编码英文标签
- 是否存在文本显示不完整、宽度不足、容易裁切的问题
- 是否存在按钮过小、过窄、仅图标且难以点击的问题
- 是否存在交互层级混乱、视觉语言不统一、可用性较差的问题
- 是否满足“易用、美观、统一、可本地化”的基本要求

## 审查范围

本轮重点检查了以下 UI 与公共绘制组件：

- [`Dialog_SkinEditor.Properties.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.cs)
- [`Dialog_SkinEditor.Preview.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs)
- [`Dialog_SkinEditor.Face.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs)
- [`Dialog_SkinEditor.LayerTree.CustomLayers.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.CustomLayers.cs)
- [`Dialog_SkinEditor.LayerTree.Nodes.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs)
- [`Dialog_SkinEditor.Equipment.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs)
- [`Dialog_SkinEditor.BaseAppearance.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.BaseAppearance.cs)
- [`Dialog_SkinSettings.cs`](../Source/CharacterStudio/UI/Dialog_SkinSettings.cs)
- [`Dialog_RenderTreeInspector.cs`](../Source/CharacterStudio/UI/Dialog_RenderTreeInspector.cs)
- [`Dialog_FileBrowser.cs`](../Source/CharacterStudio/UI/Dialog_FileBrowser.cs)
- [`Dialog_ExportMod.cs`](../Source/CharacterStudio/UI/Dialog_ExportMod.cs)
- [`Dialog_PerformanceStats.cs`](../Source/CharacterStudio/UI/Dialog_PerformanceStats.cs)
- [`UIHelper.cs`](../Source/CharacterStudio/UI/UIHelper.cs)
- [`Languages/ChineseSimplified/Keyed`](../Languages/ChineseSimplified/Keyed)

---

## 总体结论

当前项目 UI **尚未达到“无未翻译/无纯英文键/无文字截断/无难点按钮/具备统一美观可用性”的目标标准**。

主要问题集中在四类：

1. **大量硬编码英文字符串直接进入 UI**，未走 `.Translate()` 流程
2. **部分面板继续使用旧式原生 `Widgets.ButtonText(...)` 与窄按钮**，易造成点击困难与风格不统一
3. **多个属性区使用固定宽度布局**，在中文文案扩展后容易产生裁切/拥挤/阅读困难
4. **翻译资源存在漏项、混杂项、坏数据项**，局部已影响最终显示质量

建议优先级：

- **P0：清除所有可见硬编码英文/乱码翻译/明显不可达按钮**
- **P1：统一高频工具按钮与属性行布局，修复裁切风险**
- **P2：提升辅助窗口、检查器、文件浏览器、导出器等次级界面的视觉一致性**

---

## 一、未翻译 / 纯英文可见文本问题

### 1.1 属性面板存在大量直接显示的英文标签

文件：[`Dialog_SkinEditor.Properties.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.cs)

该文件是当前未翻译文本最密集的区域之一。已发现的典型问题包括：

- `South Scale X`
- `South Scale Y`
- `South Rotation`
- `Preview Scale X`
- `Preview Scale Y`
- `Preview Rotation`
- `East Transform`
- `North Transform`
- `Scale Multiplier X`
- `Scale Multiplier Y`
- `Rotation Offset`
- `FinalDrawOrder`
- `ParentName`
- `ThingDef / Apparel`
- `World TexPath`
- `Worn TexPath`
- `Apparel Mask`
- `useWornGraphicMask`
- `ThingCategories`
- `BodyPartGroups`
- `ApparelLayers`
- `ApparelTags`
- `Triggered Animation`
- `Enable Triggered Animation`
- `Trigger Ability DefName`
- `Triggered Role`
- `Deploy Angle`
- `Return Angle`
- `Deploy Ticks`
- `Hold Ticks`
- `Return Ticks`
- `Pivot X`
- `Pivot Y`
- `Effect Layer Visibility By Cycle`

影响：

- 中文用户会直接看到英文内部字段名
- 破坏整个编辑器的语言一致性
- 不利于后续维护与翻译归档

判定：**P0**

建议：

- 所有对外显示字段名统一改为翻译 key
- 对内部名、枚举名、defName 类字段，区分“显示名”和“技术名”两个层次
- 避免直接暴露源码字段名给最终用户

---

### 1.2 预览面板仍有大量英文按钮与英文状态词

文件：[`Dialog_SkinEditor.Preview.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs)

已发现的可见英文/混合文本包括：

- `Flow`
- `▶ Flow`
- `Playlist`
- `Auto`
- `Combat`
- `Panic`
- `Tired`
- `Depressed`
- `Romance`
- `Downed`
- `Dead`
- `Eye`
- `Mouth`
- `Lid`
- `Brow`
- `Emotion`

问题说明：

- 这些按钮属于高频交互入口，不能保留英文裸字
- `Flow`、`Playlist` 这类运行态文本出现在 tooltip 和按钮态上，用户会频繁看到
- 通道按钮全部是短英文，虽然技术人员可理解，但不符合中文界面目标

影响：

- 直接破坏预览面板的本地化完整度
- 用户无法从视觉上区分“功能按钮”、“调试按钮”、“技术缩写”

判定：**P0**

建议：

- 为预览流程、预设、通道覆写按钮补齐完整翻译 key
- 建议显示为中文短词，例如“自动”“战斗”“惊慌”“疲惫”“抑郁”“恋爱”“倒地”“死亡”等
- 通道按钮建议改为“眼向”“嘴型”“眼睑”“眉形”“情绪”等更自然中文

---

### 1.3 渲染树检查器存在裸英文提示与字段标签

文件：[`Dialog_RenderTreeInspector.cs`](../Source/CharacterStudio/UI/Dialog_RenderTreeInspector.cs)

已发现问题：

- `No render tree data available.`
- `Select a node to view details.`
- `Unknown Node`
- `Type`
- `Graphic`
- `Color`
- `Visible`
- `Tag`
- `None`

影响：

- 检查器属于技术窗口，但仍属于项目正式 UI
- 当前英文化程度过高，且与主编辑器中文界面割裂

判定：**P0**

建议：

- 检查器全部可见文本接入翻译系统
- 技术术语保留英文原值时，应通过“中文标签 + 技术值”的方式呈现
- 例如“节点类型: xxx”“贴图路径: xxx”

---

### 1.4 公共 UI 组件仍内置英文与非翻译态文本

文件：[`UIHelper.cs`](../Source/CharacterStudio/UI/UIHelper.cs)

已发现问题：

- 颜色选择器通道名：`R`、`G`、`B`、`A`
- 开关状态文字：`On` / `Off`
- 空值占位：`(无)` 为直写字面量，未走翻译 key

说明：

- `R/G/B/A` 虽可视为通行缩写，但当前目标是统一、本地化、无裸英文键；至少应评估是否保留，或改为可翻译短标签
- `On/Off` 已属于明显用户态文本，不应硬编码
- 公共组件中的硬编码会扩散到整个工程

判定：**P0**

建议：

- `On/Off` 立即改为翻译 key
- 空值占位统一抽为公共翻译项
- `R/G/B/A` 可按需求决定保留英文缩写还是改为“红/绿/蓝/透明”，但必须统一策略

---

### 1.5 面部编辑页存在未走 key 的直接中文文案

文件：[`Dialog_SkinEditor.Face.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs)

已发现问题：

- `重新导入`
- `表情`
- `眼球朝向`
- `上眼睑下移量`

说明：

- 这些虽然不是英文，但仍属于硬编码直写文本
- 与全项目统一翻译资源管理目标冲突
- 长期来看会导致多语言不可维护

判定：**P1**

建议：

- 所有可见字符串统一 key 化
- 面部工作流相关术语集中归档到 [`CS_Keys_Face.xml`](../Languages/ChineseSimplified/Keyed/CS_Keys_Face.xml)

---

### 1.6 性能统计窗口存在直写中文且翻译资源已出现乱码项

文件：[`Dialog_PerformanceStats.cs`](../Source/CharacterStudio/UI/Dialog_PerformanceStats.cs)

已发现问题：

- 按钮文本 `重置统计` 为直写

翻译资源问题：

- [`CS_Keys_Common.xml`](../Languages/ChineseSimplified/Keyed/CS_Keys_Common.xml) 中 `CS_Studio_OpenPerformanceStats` 当前值出现乱码：`鎵撳紑鎬ц兘缁熻`

影响：

- 如果主入口调用该 key，最终界面会直接显示乱码
- 属于高优先级质量问题

判定：**P0**

建议：

- 立即修复乱码翻译值
- 性能统计窗口所有可见文字统一转为 key

---

## 二、文本裁切 / 宽度不足 / 可读性风险

### 2.1 属性行广泛使用固定宽度标签与输入控件，长中文容易拥挤

文件：[`UIHelper.cs`](../Source/CharacterStudio/UI/UIHelper.cs)

相关实现特征：

- `LabelWidth = 100f`
- 多处属性行高度固定为 `28f`
- 滑块输入框固定宽度 `64f`
- 带按钮字段通常使用非常窄的末端按钮宽度

风险：

- 中文标签一旦扩展，控件区会被压缩
- 属性名较长时，即使 `actualLabelWidth` 有自适应，整行仍容易出现输入框过窄
- 多列复合行在窄面板下很容易变得拥挤

判定：**P1**

建议：

- 对长字段面板采用分段式布局，不强行单行塞入
- 高密度属性行应允许双行或更高行高
- 对高频长文案使用 tooltip 兜底，但不能依赖 tooltip 解决主体可读性

---

### 2.2 预览面板顶部预设按钮宽度固定，中文化后极易裁切

文件：[`Dialog_SkinEditor.Preview.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs)

当前问题：

- 预设按钮宽度大量写死，例如 `56f`、`64f`、`72f`、`78f`、`82f`
- 现阶段英文短词刚好能放下，一旦改为中文或更自然文案，将立刻面临宽度重算
- 自动播放、速度、预设、覆写按钮在同一顶部区域并排堆叠，空间紧张

影响：

- 一旦推进完整汉化，按钮将出现字被截断、按钮互相挤压、视觉密度过高

判定：**P1**

建议：

- 按钮宽度改为基于 `Text.CalcSize(...)` 动态计算
- 预设区改为自动换行或分组式布局
- 工具按钮与状态按钮分两层，不要全部混在同一条工具带

---

### 2.3 文件浏览器路径栏与文件项宽度不足，长路径显示体验较差

文件：[`Dialog_FileBrowser.cs`](../Source/CharacterStudio/UI/Dialog_FileBrowser.cs)

问题：

- 当前路径直接完整绘制在小高度路径框内
- 文件项名直接整行 `Widgets.Label(...)`，超长文件名与深目录路径可读性差
- 搜索与浏览区之间缺少层级提示

影响：

- 用户在贴图目录很深时不易确认所在位置
- 长路径只能依赖 tooltip，不利于连续浏览

判定：**P2**

建议：

- 路径栏采用裁切头部、保留尾部的重要路径显示策略
- 文件项增加 hover 高亮与目录/文件更明确的视觉区分
- 可增加“当前目录”说明与路径复制功能

---

### 2.4 导出器说明、状态、确认区可能出现文本堆叠感

文件：[`Dialog_ExportMod.cs`](../Source/CharacterStudio/UI/Dialog_ExportMod.cs)

问题：

- 说明文本、复选项、倒计时、输出路径都堆在同一纵向滚动区中
- `Description` 文本区、模块摘要、确认区连续排列，视觉分组虽有 section，但仍偏密集
- 底部状态消息直接画在主区域上方，容易与滚动区域形成视觉竞争

判定：**P2**

建议：

- 确认区使用更明显的警示卡样式
- 状态区固定在底部按钮上方的独立容器中
- 对长段说明文本增加更舒适留白

---

## 三、按钮可点击性 / 命中面积 / 误触风险

### 3.1 多处删除按钮仅使用 `×` 且尺寸过小

典型位置：

- [`Dialog_SkinEditor.Properties.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.cs)
- [`Dialog_SkinEditor.Face.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs)
- [`Dialog_SkinEditor.LayerTree.CustomLayers.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.CustomLayers.cs)
- [`Dialog_SkinEditor.Equipment.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs)

已发现的典型尺寸：

- `20x20`
- `22x20`
- `26x20`
- `30x20`

问题：

- 仅显示 `×`，缺少文字语义与确认层级
- 命中面积太小，尤其在高 DPI、窗口缩放、连续编辑时容易误点或点不到
- 与附近文本控件、选择按钮间距过小

影响：

- 删除类操作风险高，不应使用过小点击区域
- 不满足“无无法点到按钮”的要求

判定：**P0**

建议：

- 所有删除/清除按钮最小点击区域建议不低于 `28x24`
- 重要删除使用“清除”“删除”短文案或带 tooltip 的标准危险按钮
- 对紧凑行可保留图标，但外层点击矩形必须扩展

---

### 3.2 图层树与基础槽位可见性按钮仅靠符号表达，易误触且不统一

典型位置：

- [`Dialog_SkinEditor.LayerTree.Nodes.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.Nodes.cs)
- [`Dialog_SkinEditor.LayerTree.CustomLayers.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.LayerTree.CustomLayers.cs)
- [`Dialog_SkinEditor.BaseAppearance.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.BaseAppearance.cs)
- [`Dialog_SkinEditor.Equipment.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Equipment.cs)

当前图标/符号包括：

- `▼` / `▶`
- `◉` / `◯`
- 眼睛图标或状态图标
- `×`

问题：

- 同类行为在不同面板中使用不同符号和样式
- 部分按钮尺寸仅 `20~22` 像素级别
- 仅凭图形表达，不利于新用户理解

判定：**P1**

建议：

- 统一“显隐”“展开/折叠”“删除/清除”按钮语义和颜色
- 高风险操作补 tooltip
- 常驻小图标按钮统一使用公共绘制函数，确保 hitbox 一致

---

### 3.3 面部编辑页中 `…` 与 `×` 微型按钮过窄

文件：[`Dialog_SkinEditor.Face.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs)

已发现问题：

- 资源选择按钮 `…`
- 清空按钮 `×`
- 部分按钮尺寸约 `22~26` 宽、`20` 高

影响：

- 这是高频贴图编辑区域，微型按钮会直接影响效率
- 鼠标稍有偏移就可能无法点击，或点到临近控件

判定：**P0**

建议：

- 将“选择”“清除”按钮至少提升到统一小工具按钮尺寸
- 若空间不足，可拆成下一行，而不是继续压缩

---

### 3.4 预览面板控制按钮虽然视觉已优化，但信息承载过密

文件：[`Dialog_SkinEditor.Preview.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs)

问题：

- 旋转、缩放、重置、Flow、速度、预设、覆写控制高度集中在上部
- 当后续翻译成中文后，按钮更宽，点击区域虽然尚可，但密度会快速升高
- 当前还存在符号按钮 `◀`、`▶`、`-`、`+`、`↺`

判定：**P1**

建议：

- 保留符号按钮可以，但应补 tooltip 与更稳定的分组
- 流程按钮、速度按钮、预设按钮建议分区
- 若面板宽度不足，自动换行优先于继续缩小按钮

---

## 四、易用性与美观度问题

### 4.1 项目内仍并存多套按钮视觉语言

已观察到的按钮来源：

- 原生 [`Widgets.ButtonText(...)`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs)
- 自定义 [`DrawPreviewToolbarButton(...)`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs)
- 公共 [`DrawSelectionButton(...)`](../Source/CharacterStudio/UI/UIHelper.cs)
- 面部页自定义 [`DrawFaceToolbarButton(...)`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Face.cs)

问题：

- 同属编辑器内高频按钮，却在边框、悬停、颜色、字体、强调线方面不一致
- 用户切换不同页面时，无法建立稳定的操作预期

判定：**P1**

建议：

- 收敛为 3 类按钮即可：主按钮、次按钮、危险按钮
- 工具条按钮与属性选择按钮尽量共用公共绘制逻辑
- 预览页、面部页、属性页不要再各自维护一套独立样式

---

### 4.2 技术字段、业务字段、用户描述字段混排，阅读负担大

主要区域：[`Dialog_SkinEditor.Properties.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.cs)

问题：

- 技术字段如 `DefName`、`TexPath`、`ParentName`、`Tag` 与用户可调参数混在一起
- 没有明显区分“普通用户常用项”和“高级技术项”
- 新手进入后会快速被技术术语淹没

判定：**P1**

建议：

- 属性面板增加“基础 / 高级 / 调试”分层
- 默认优先展示用户常调项
- 技术字段折叠收纳，避免首屏信息过载

---

### 4.3 检查器、文件浏览器、导出器等辅助窗口视觉统一度不足

涉及文件：

- [`Dialog_RenderTreeInspector.cs`](../Source/CharacterStudio/UI/Dialog_RenderTreeInspector.cs)
- [`Dialog_FileBrowser.cs`](../Source/CharacterStudio/UI/Dialog_FileBrowser.cs)
- [`Dialog_ExportMod.cs`](../Source/CharacterStudio/UI/Dialog_ExportMod.cs)

问题：

- 主编辑器部分面板已经建立了较好的深色卡片视觉语言
- 但辅助窗口仍大量保留传统 RimWorld 原生窗口感
- 标题、工具区、状态区、列表区样式不统一

判定：**P2**

建议：

- 延续 [`docs/角色编辑器UI统一化清单.md`](./角色编辑器UI统一化清单.md) 中的统一方向
- 先统一标题条、内层容器、按钮边框、hover 样式，再逐步统一布局

---

## 五、翻译资源层面问题

### 5.1 中文 key 资源仍未覆盖全部实际显示文本

目录：[`Languages/ChineseSimplified/Keyed`](../Languages/ChineseSimplified/Keyed)

问题：

- 许多 UI 已有完整 key，但代码侧仍未调用
- 另一些区域根本没有对应 key，导致继续硬编码
- 少数术语在不同文件中翻译策略不一致

判定：**P1**

建议：

- 代码审查时增加规则：禁止新增可见硬编码字符串
- 所有新增 UI 文案必须同步补齐中英文 key
- 建立术语表，统一 `DefName`、`ThingDef`、`Shader`、`Tag` 等技术词的显示策略

---

### 5.2 存在乱码/异常翻译值，需立即修复

已确认问题：

- [`CS_Studio_OpenPerformanceStats`](../Languages/ChineseSimplified/Keyed/CS_Keys_Common.xml) 显示值乱码

判定：**P0**

建议：

- 立即修复坏数据
- 对全部中文翻译 XML 进行一次编码与内容完整性巡检

---

## 六、问题优先级汇总

### P0（必须优先处理）

1. 清理 [`Dialog_SkinEditor.Properties.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Properties.cs) 中全部可见英文标签
2. 清理 [`Dialog_SkinEditor.Preview.cs`](../Source/CharacterStudio/UI/Dialog_SkinEditor.Preview.cs) 中全部可见英文按钮与状态文本
3. 清理 [`Dialog_RenderTreeInspector.cs`](../Source/CharacterStudio/UI/Dialog_RenderTreeInspector.cs) 中全部可见英文
4. 修复 [`UIHelper.cs`](../Source/CharacterStudio/UI/UIHelper.cs) 中 `On/Off` 等公共硬编码文本
5. 修复 [`CS_Keys_Common.xml`](../Languages/ChineseSimplified/Keyed/CS_Keys_Common.xml) 中性能统计入口乱码
6. 扩大所有删除/清除类微型按钮的点击热区，尤其是 `×`、`…` 等 20 像素级按钮

### P1（应尽快处理）

1. 面部页与其他页面所有直写中文改为 key
2. 统一工具按钮视觉语言与命中面积
3. 调整属性页长标签/长控件布局，减少裁切风险
4. 将技术字段与普通调节项分层显示
5. 为预览面板按钮宽度与换行策略做中文化预留

### P2（中期体验优化）

1. 统一文件浏览器、导出器、检查器等辅助窗口外观
2. 优化长路径、长列表、状态区的视觉组织
3. 提升辅助窗口的信息密度与留白平衡

---

## 七、推荐整改顺序

### 第一阶段：先过“可用与可本地化”底线

- 修复全部可见英文裸字
- 修复乱码 key
- 修复所有过小删除按钮 / 清空按钮 / 资源选择按钮
- 确保所有用户可见文本都可翻译

### 第二阶段：统一高频编辑体验

- 统一属性页、预览页、面部页、图层树按钮体系
- 统一 hover / active / danger 状态表达
- 统一常用操作按钮最小尺寸

### 第三阶段：完善审美与信息架构

- 对复杂属性页做基础/高级分层
- 对预览页做工具区与状态区解耦
- 对辅助窗口做统一卡片化改造

---

## 八、验收标准建议

整改后建议以以下标准验收：

1. 任意主流程 UI 不再出现未翻译英文裸字
2. 任意窗口不再出现乱码文本
3. 删除、清空、选择资源等常用按钮均能稳定点击
4. 中文文本在常见分辨率下不出现明显截断
5. 同类按钮在不同页面具有一致视觉语言
6. 普通用户可在不理解内部字段名的前提下完成主要编辑操作

---

## 九、结论

当前项目 UI 已具备一定基础风格，但**本地化完整度、按钮可点击性、布局弹性、视觉一致性**仍存在明显缺口。

其中最需要优先处理的是：

- 可见英文文本清理
- 翻译资源坏数据修复
- 微型按钮点击热区扩展
- 高频按钮体系统一

在这些问题完成前，项目 UI 仍不适合宣称达到“无未翻译、无文字显示不全、无点不到按钮、保证易用与美观”的目标状态。