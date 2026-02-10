# CharacterStudio: HAR (外星人模组) 完整支持方案设计

## 1. 问题背景
Humanoid Alien Races (HAR) 模组通过 `AlienPartGenerator` 动态生成 Pawn 的身体部件（BodyAddons）。这些部件具有复杂的渲染逻辑，包括：
- **双通道颜色**：使用 `_Color` 和 `_ColorTwo`（配合 Mask 纹理）。
- **动态缩放**：通过 `drawSize` 或 `scaleWithPawnDrawsize` 调整大小。
- **动态偏移**：根据朝向（南/东/北）应用不同的偏移量。
- **Shader 变体**：使用 `CutoutComplex` 等支持 Mask 的着色器。
- **颜色源绑定**：颜色可能绑定到发色、肤色或特定的 HAR 颜色通道。

目前的 CharacterStudio 在导入这些部件时，存在信息丢失（如缩放、Mask 颜色）或绑定失效（颜色变为静态）的问题。

## 2. 设计目标
1.  **完整捕获**：准确提取 HAR 节点的纹理、Shader、双通道颜色、缩放和偏移。
2.  **动态绑定**：支持将图层颜色源绑定到 Pawn 属性（如发色/肤色），而非仅存储静态颜色。
3.  **所见即所得**：确保编辑器预览与游戏内渲染完全一致。
4.  **高度可配置**：允许用户在编辑器中手动调整 Shader、颜色源和 Mask。

## 3. 数据模型升级 (PawnLayerConfig)

我们需要扩展 `PawnLayerConfig` 以支持更丰富的渲染属性。

```csharp
public enum LayerColorSource
{
    Fixed,              // 使用自定义固定颜色
    PawnHair,           // 绑定到 Pawn 的发色 (story.HairColor)
    PawnSkin,           // 绑定到 Pawn 的肤色 (story.SkinColor)
    PawnApparelPrimary, // 绑定到服装主色
    PawnApparelSecondary // 绑定到服装副色
}

public class PawnLayerConfig
{
    // ... 现有字段 ...

    // --- 颜色系统升级 ---
    public LayerColorSource colorSource = LayerColorSource.Fixed;
    public Color customColor = Color.white; // 当 source 为 Fixed 时使用

    public LayerColorSource colorTwoSource = LayerColorSource.Fixed; // Mask 通道颜色源
    public Color customColorTwo = Color.white; // 当 source 为 Fixed 时使用

    // --- Shader 系统 ---
    public string shaderDefName = "Cutout"; // 默认为 Cutout，HAR 常用 CutoutComplex

    // --- 高级纹理 ---
    public string maskTexPath = ""; // Mask 纹理路径 (如果与主纹理分离)
}
```

## 4. 智能解析器 (RenderTreeParser & ImportUtility)

### 4.1 增强捕获逻辑
在 `RenderTreeParser` 中，增加对 HAR 特性的探测：
*   **Shader 探测**：从 `Graphic.Shader` 获取 Shader 名称。如果是 `CutoutComplex`，自动标记需要双通道颜色。
*   **Mask 探测**：检查是否存在 `_m` 后缀的纹理文件，或从 `Graphic_Multi` 中提取 Mask 路径。
*   **缩放探测**：同时记录 `Worker.ScaleFor` (运行时动态缩放) 和 `Graphic.drawSize` (静态基础缩放)，在导入时智能合并。

### 4.2 智能颜色源推断
在导入 (`VanillaImportUtility`) 时，不再仅存储颜色值，而是推断其来源：
1.  **Tag 优先**：如果节点 Tag 包含 "Hair"，默认 `colorSource = PawnHair`。
2.  **值比对**：
    *   如果 `runtimeColor` ≈ `pawn.story.HairColor`，推断为 `PawnHair`。
    *   如果 `runtimeColor` ≈ `pawn.story.SkinColor`，推断为 `PawnSkin`。
3.  **HAR 反射 (可选)**：尝试通过反射读取 `BodyAddon.colorChannel` 配置，直接映射到对应的源。

## 5. 渲染管线适配 (PawnRenderNodeWorker_CustomLayer)

更新渲染工作器以支持动态属性：

```csharp
public override MaterialPropertyBlock GetMaterialPropertyBlock(...)
{
    // 1. 获取主颜色
    Color colorOne = GetColorFromSource(config.colorSource, pawn, config.customColor);
    
    // 2. 获取副颜色 (Mask)
    Color colorTwo = GetColorFromSource(config.colorTwoSource, pawn, config.customColorTwo);

    // 3. 应用到属性块
    matPropBlock.SetColor(ShaderPropertyIDs.Color, colorOne);
    matPropBlock.SetColor(ShaderPropertyIDs.ColorTwo, colorTwo); // 确保 Shader 支持
    
    return matPropBlock;
}

private Color GetColorFromSource(LayerColorSource source, Pawn pawn, Color fixedColor)
{
    switch (source)
    {
        case LayerColorSource.PawnHair: return pawn.story.HairColor;
        case LayerColorSource.PawnSkin: return pawn.story.SkinColor;
        // ... 其他源
        default: return fixedColor;
    }
}
```

同时，`GetGraphic` 需要根据 `config.shaderDefName` 加载正确的 Shader，确保 `_ColorTwo` 能够生效。

## 6. 编辑器交互设计

在 `Dialog_SkinEditor` 中：
*   **颜色编辑器**：将单一的颜色选择器改为“源选择器 + 颜色/预览块”。
    *   下拉菜单选择 `Fixed/Hair/Skin`。
    *   如果是 `Fixed`，显示颜色拾取器。
    *   如果是 `Hair/Skin`，显示当前 Pawn 的对应颜色预览（只读）。
*   **Shader 选择**：提供下拉菜单选择 Shader（Cutout / CutoutComplex / Transparent）。
*   **Mask 支持**：如果选择了支持 Mask 的 Shader，显示 `ColorTwo` 配置区。

## 7. 实施计划

1.  **Phase 1 (基础架构)**: 修改 `PawnLayerConfig`，引入 `ColorSource` 和 `Shader` 字段。
2.  **Phase 2 (渲染适配)**: 更新 `PawnRenderNodeWorker` 支持动态颜色源和 Shader 切换。
3.  **Phase 3 (导入增强)**: 升级 `VanillaImportUtility`，实现智能颜色源推断和 Shader 识别。
4.  **Phase 4 (UI 更新)**: 重构编辑器属性面板，提供更直观的颜色源绑定和 Shader 配置。

---
**针对 Miho 种族的特别说明**：
Miho 的头发颜色由基因控制，这意味着在 RimWorld 逻辑中它就是 `Pawn.story.HairColor`。通过上述的 **Phase 3 (导入增强)**，系统将自动识别 Miho 头发节点的颜色源为 `PawnHair`，从而实现完美兼容：当你在游戏中改变 Miho 的发色基因时，CS 皮肤上的头发也会随之变色。