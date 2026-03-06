# CharacterStudio 渲染/编辑器重构方案

## 目标

将 CharacterStudio 从“渲染树补丁驱动的高级编辑器”重构为“面向玩家的可视化角色纹理编辑器”，使玩家无需编写 C# 或 XML 即可完成：

- 基础身体/头部纹理替换
- 头发、胡须、面部组件调整
- 外部纹理导入
- 可视化缩放、偏移、旋转、颜色、Shader 调整
- 追加装饰图层
- 保留高级节点树调试能力，但不作为主工作流入口

---

## 当前问题

### 1. `Head/Body` 被视为结构性父节点
当前导入逻辑在 [`ProcessSnapshotNode()`](Source/CharacterStudio/Core/VanillaImportUtility.cs:109) 中会跳过结构性父节点，避免把原版 `Head/Body` 作为普通图层导入。

优点：
- 避免重复渲染
- 避免普通用户误操作基础骨架节点

缺点：
- 编辑器中看得到固定头/身体效果，但不能像普通图层一样直接替换与缩放
- 用户心智不一致，容易感觉“有图但不能改”

### 2. 当前隐藏原版节点方案复杂且脆弱
当前逻辑主要依赖：
- [`CanDrawNow_Prefix()`](Source/CharacterStudio/Rendering/Patch_PawnRenderTree.cs:234)
- [`ProcessVanillaHiding()`](Source/CharacterStudio/Rendering/Patch_PawnRenderTree.cs:453)
- [`HideNode()`](Source/CharacterStudio/Rendering/Patch_PawnRenderTree.cs:815)

问题在于：
- 依赖原版渲染树遍历时机
- 依赖 `hiddenPaths` / `hiddenTags`
- 对 HAR / 异种结构兼容成本高
- 对普通用户来说不可理解

### 3. 节点树工具承担了过多“主编辑功能”
当前 [`Dialog_SkinEditor.cs`](Source/CharacterStudio/UI/Dialog_SkinEditor.cs) 同时承担：
- 普通图层编辑
- 节点树浏览
- 节点隐藏
- 节点挂载
- 高级路径调试

这让普通工作流和高级调试工作流混在一起。

---

## 重构原则

1. **基础部件与附加图层分离**
2. **普通玩家优先，不要求理解节点树**
3. **保留高级功能，但降级为专业模式/调试模式**
4. **尽量复用现有自定义图层渲染能力**
5. **分阶段实施，避免一次性推倒重来**

---

## 新架构总览

### A. 基础槽位系统（Base Slots）
将以下部件定义为“基础槽位”：

- Body
- Head
- Hair
- Beard
- Eyes
- Brow
- Mouth
- Nose
- Ear

这些部件不再作为普通 [`PawnLayerConfig`](Source/CharacterStudio/Core/PawnLayerConfig.cs:11) 图层处理，而是作为独立配置项存在。

#### 每个基础槽位建议支持字段

- `enabled`
- `slotType`
- `texPath`
- `maskTexPath`
- `shaderDefName`
- `colorSource`
- `customColor`
- `colorTwoSource`
- `customColorTwo`
- `scale`
- `offset`
- `offsetEast`
- `offsetNorth`
- `rotation`
- `flipHorizontal`
- `drawOrderOffset`
- `graphicClass`

### B. 附加图层系统（Overlay Layers）
普通 [`PawnLayerConfig`](Source/CharacterStudio/Core/PawnLayerConfig.cs:11) 继续保留，负责：

- 饰品
- 额外耳朵/尾巴
- 表情叠层
- 光效
- 特效纹理
- 额外纹身或装饰件

该部分继续走现有自定义节点注入路线：
- [`PawnRenderNodeWorker_CustomLayer.GetGraphic()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs:490)
- [`PawnRenderNodeWorker_CustomLayer.ScaleFor()`](Source/CharacterStudio/Rendering/PawnRenderNodeWorker_CustomLayer.cs:131)
- [`CreateNodeProperties()`](Source/CharacterStudio/Rendering/Patch_PawnRenderTree.cs:1408)

### C. 高级节点工具（Advanced Node Tools）
节点树 UI 继续保留，但职责变为：

- 锚点选择
- 调试渲染树结构
- 高级隐藏/挂接
- HAR / 异种兼容诊断

不再作为普通用户修改 Body/Head 的主要入口。

---

## 数据结构建议

### 1. 在 `PawnSkinDef` 中新增基础槽位集合
建议在 [`PawnSkinDef`](Source/CharacterStudio/Core/PawnSkinDef.cs:34) 中新增：

```csharp
public BaseAppearanceConfig baseAppearance = new BaseAppearanceConfig();
```

### 2. 新增基础槽位配置类型
建议新增文件：

- `Source/CharacterStudio/Core/BaseAppearanceConfig.cs`
- `Source/CharacterStudio/Core/BaseAppearanceSlotConfig.cs`

建议结构：

```csharp
public enum BaseAppearanceSlotType
{
    Body,
    Head,
    Hair,
    Beard,
    Eyes,
    Brow,
    Mouth,
    Nose,
    Ear
}

public class BaseAppearanceSlotConfig
{
    public bool enabled = false;
    public BaseAppearanceSlotType slotType;
    public string texPath = "";
    public string maskTexPath = "";
    public string shaderDefName = "Cutout";
    public LayerColorSource colorSource = LayerColorSource.Fixed;
    public Color customColor = Color.white;
    public LayerColorSource colorTwoSource = LayerColorSource.Fixed;
    public Color customColorTwo = Color.white;
    public Vector2 scale = Vector2.one;
    public Vector3 offset = Vector3.zero;
    public Vector3 offsetEast = Vector3.zero;
    public Vector3 offsetNorth = Vector3.zero;
    public float rotation = 0f;
    public bool flipHorizontal = false;
    public float drawOrderOffset = 0f;
    public Type? graphicClass;
}
```

---

## 渲染层改造建议

## 原则

基础槽位不再走“隐藏原版父节点 + 自定义子层替代”的主路径，而是：

- 保留原版节点结构
- 在原版对应节点上做图形来源覆写
- 只在必要时做隐藏或材质抑制

### 推荐实现路线

#### 方案 1：节点级 Graphic 覆写（推荐）
在 [`Patch_PawnRenderTree.cs`](Source/CharacterStudio/Rendering/Patch_PawnRenderTree.cs) 中增加“基础槽位命中逻辑”：

- 识别当前节点是否为 `Body/Head/Hair/...`
- 若皮肤定义中该槽位启用覆写，则优先使用外部配置图形
- 原版节点继续参与原树流程，但其 `Graphic/Material` 来自槽位配置

#### 方案 2：低层材质替换（次选）
若节点级 Graphic 覆写困难，则在材质/图形获取链路中做优先替换。

但该方案调试成本更高，可读性更差，不建议作为首选。

---

## 编辑器改造建议

## 新 UI 分区

在 [`Dialog_SkinEditor.cs`](Source/CharacterStudio/UI/Dialog_SkinEditor.cs) 中将编辑器拆为三块：

### 1. 基础部件面板
新增“基础部件”区域，供普通用户编辑：

- 身体
- 头部
- 头发
- 胡须
- 面部组件

每个部件提供：
- 纹理选择
- 外部文件选择
- 缩放
- 偏移
- 旋转
- 颜色来源
- Shader
- 启用/禁用

### 2. 图层面板
保留当前图层系统：
- 新建图层
- 删除图层
- 批量选中
- 动画
- 锚点设置

### 3. 高级节点面板
节点树改为高级模式：
- 隐藏节点
- 复制路径
- 节点挂载
- 诊断结构

---

## 导入逻辑改造建议

当前 [`VanillaImportUtility.ProcessSnapshotNode()`](Source/CharacterStudio/Core/VanillaImportUtility.cs:109) 已正确跳过结构性父节点。

下一步不应该把这些节点重新当作普通图层导入，而应该：

- 将 `Body/Head/Hair/...` 信息写入 `baseAppearance`
- 将附加节点继续写入 `layers`

### 新导入策略

#### 基础部件
导入到：
- `baseAppearance.body`
- `baseAppearance.head`
- `baseAppearance.hair`
- ...

#### 装饰与补层
导入到：
- [`PawnSkinDef.layers`](Source/CharacterStudio/Core/PawnSkinDef.cs:64)

这样导入后，用户直接在编辑器中看到：
- 基础身体/头部已填充
- 附加层仍保留为普通图层

---

## 保存与导出改造建议

需要同步改造：
- [`SkinSaver.cs`](Source/CharacterStudio/Exporter/SkinSaver.cs)
- [`ModBuilder.cs`](Source/CharacterStudio/Exporter/ModBuilder.cs)
- [`XmlExporter.cs`](Source/CharacterStudio/Exporter/XmlExporter.cs)

建议新增 XML 段：

```xml
<baseAppearance>
  <slots>
    <li>
      <slotType>Body</slotType>
      <enabled>true</enabled>
      <texPath>...</texPath>
      <scale>(1,1)</scale>
      ...
    </li>
  </slots>
</baseAppearance>
```

需要保证：
- 老皮肤文件可正常读取
- 没有基础槽位时默认回退原版表现

---

## 分阶段实施计划

## Phase 1：数据模型接入
目标：只加数据，不改主渲染逻辑。

工作项：
- 新增 `BaseAppearanceConfig`
- 新增 `BaseAppearanceSlotConfig`
- 扩展 [`PawnSkinDef`](Source/CharacterStudio/Core/PawnSkinDef.cs:34) Clone 与默认值
- 扩展保存/导出

验收标准：
- 基础槽位数据能保存、加载、导出
- 老数据不受影响

## Phase 2：编辑器基础面板
目标：在 UI 中可编辑基础槽位。

工作项：
- 在 [`Dialog_SkinEditor.cs`](Source/CharacterStudio/UI/Dialog_SkinEditor.cs) 添加基础部件区域
- 复用现有纹理选择、颜色、缩放、偏移控件
- 保持图层面板不变

验收标准：
- 用户无需理解节点树即可编辑 Body/Head

## Phase 3：渲染覆写接入
目标：让基础槽位真正生效。

工作项：
- 在 [`Patch_PawnRenderTree.cs`](Source/CharacterStudio/Rendering/Patch_PawnRenderTree.cs) 中接入基础槽位图形覆写
- 尽量不依赖 `HideNode()` 主路径
- 为 Body/Head/Hair 做第一批实现

验收标准：
- 外部纹理替换可见
- 缩放、偏移、颜色可见
- 不影响普通附加图层

## Phase 4：导入映射升级
目标：将地图角色的基础节点自动导入到基础槽位。

工作项：
- 改造 [`VanillaImportUtility.cs`](Source/CharacterStudio/Core/VanillaImportUtility.cs)
- 从结构性父节点提取基础槽位信息
- 其余节点继续走图层导入

验收标准：
- 导入一个现有 Pawn 后，基础槽位自动填充

## Phase 5：高级模式收口
目标：让节点树只服务高级需求。

工作项：
- 对 UI 做分级
- 将 `hiddenPaths` / `hiddenTags` 标记为高级功能
- 默认主流程不暴露节点路径概念

验收标准：
- 普通用户在不接触节点树的前提下即可完成主要外观编辑

---

## 推送建议

建议后续提交按两条主线组织：

### 提交 A：技能组件化与编辑器扩展
保留当前已稳定完成的能力系统提交。

### 提交 B：渲染编辑器重构起步
从基础槽位数据模型开始，不与现有技能提交混在一起。

---

## 结论

最合理、最易实施的方向不是继续强化“所有东西都作为渲染树节点处理”，而是：

- **基础部件走槽位覆写**
- **附加装饰走普通图层**
- **节点树保留为高级工具**

这条路线最符合 CharacterStudio 的产品目标：

> 让 RimWorld 角色纹理编辑真正可视化、可操作、无代码化。