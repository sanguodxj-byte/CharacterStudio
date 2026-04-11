# CharacterStudio Exporter Code Review
- 日期: 2026-04-08
- 概述: 对 CharacterStudio 导出器模块 (Exporter/) 的全面代码审查与优化建议
- 状态: 已完成
- 总体结论: 有条件通过

## 评审范围

# CharacterStudio Exporter 模块代码审查

## 审查范围
- `Exporter/ExportAssetUtility.cs` — 资产枚举与来源识别
- `Exporter/ModBuilder.cs` — 核心导出构建器
- `Exporter/ModExportXmlWriter.cs` — XML 片段序列化
- `Exporter/SkinSaver.cs` — 皮肤定义持久化
- `Exporter/XmlExporter.cs` — PawnRenderNodeDef 导出

## 审查目标
1. 代码质量与可维护性
2. 重复代码识别
3. 健壮性与错误处理
4. 性能考量
5. 架构设计合理性

## 评审摘要

- 当前状态: 已完成
- 已审模块: Exporter/ExportAssetUtility.cs, Exporter/ModBuilder.cs, Exporter/ModExportXmlWriter.cs, Exporter/SkinSaver.cs, Exporter/XmlExporter.cs
- 当前进度: 已记录 2 个里程碑；最新：ms-robustness
- 里程碑总数: 2
- 已完成里程碑: 2
- 问题总数: 9
- 问题严重级别分布: 高 1 / 中 5 / 低 3
- 最新结论: ## 总体评价 CharacterStudio 的导出器模块在功能完整性上表现出色，涵盖了皮肤定义保存、完整 mod 包构建、基因/PawnKind/装备/技能/触发器等多种 Def 类型的 XML 导出，Pipeline 流程清晰、导出失败时有清理机制、资产权利确认机制到位。代码整体可用，但存在一个需要优先解决的架构级问题和多个中等优先级的改进项。 ### 优点 1. **导出 Pipeline 设计良好** — ModBuilder.Export → CloneConfig → NormalizeModuleSelection → ExecuteExportPipeline 流程清晰，有 try-catch 和 CleanupFailedExport 回滚 2. **资产溯源能力强** — ExportAssetUtility 能区分 VanillaContent/ExternalMod/LocalFile，为版权合规提供了基础 3. **模块化导出选项丰富** — 支持 CosmeticPack/FullUnit 两种模式，包含精细的模块开关（SkinDef/GeneDef/PawnKind/Abilities 等） 4. **SkinSaver 的故障分类** — 针对磁盘满、权限不足等 IO 错误提供了专门的分类和本地化错误消息 Key 5. **装备-技能-Flyer 绑定逻辑** — PrepareEquipmentExportBindings 能自动关联装备与技能的 FlyerThingDef，减少手动配置 ### 需要改进的关键问题 1. **[高] SkinSaver 与 ModExportXmlWriter 之间约 15 个方法存在大规模代码重复**，且重复之间存在微妙差异（字段遗漏、序列化策略不同），是最大的维护风险 2. **[中] ModBuilder 有约 12 个死代码方法**需要清理 3. **[中] CloneExportConfig 对 Abilities 的浅拷贝**可能导致导出操作污染原始配置 4. **[中] SkinSaver 原子写入实现有缺陷**，在 Copy 失败时可能损坏原文件 5. **[中] 纹理扩展名列表不一致** — 两处定义不同的扩展名集合 6. **[中] RecipeDef 硬编码中文** — 导出的 mod 在英文环境下显示中文 ### 优化路线建议（优先级排序） 1. **P0**: 统一序列化路径 — SkinSaver 改为调用 ModExportXmlWriter 的方法，消除重复 2. **P1**: 修复 CloneExportConfig 的 Abilities 浅拷贝问题 3. **P1**: 改进 SkinSaver 原子写入为 File.Replace 模式 4. **P2**: 清理 ModBuilder 中的死代码方法和未使用局部变量 5. **P2**: 统一纹理扩展名常量、提取格式化工具类 6. **P3**: RecipeDef 国际化处理
- 下一步建议: 优先处理 F-ARCH-01（SkinSaver 与 ModExportXmlWriter 代码重复），将 SkinSaver 的 XML 序列化逻辑统一委托给 ModExportXmlWriter，消除约 600 行重复代码。
- 总体结论: 有条件通过

## 评审发现

### SkinSaver 与 ModExportXmlWriter 之间存在大规模代码重复

- ID: F-ARCH-01
- 严重级别: 高
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: ms-architecture
- 说明:

  SkinSaver.cs 和 ModExportXmlWriter.cs 各自独立实现了几乎相同的 XML 序列化逻辑。以下方法在两个文件中存在近乎完全一致的重复实现：

  - `GenerateEquipmentsXml` (SkinSaver L195-246 vs ModExportXmlWriter L78-138)
  - `GenerateEquipmentStatEntriesXml` (SkinSaver L248-270 vs ModExportXmlWriter L140-162)
  - `GenerateEquipmentCostEntriesXml` (SkinSaver L272-294 vs ModExportXmlWriter L164-186)
  - `GenerateEquipmentRenderDataXml` (SkinSaver L296-324 vs ModExportXmlWriter L188-246)
  - `GenerateAbilitiesXml` / `GenerateSkinAbilitiesXml` (SkinSaver L326-360 vs ModExportXmlWriter L561-598)
  - `GenerateEffectsXml` / `GenerateSkinAbilityEffectsXml` (SkinSaver L362-392 vs ModExportXmlWriter L600-633)
  - `GenerateRuntimeComponentsXml` (SkinSaver L394-483 vs ModExportXmlWriter L635-750)
  - `GenerateAbilityHotkeysXml` (SkinSaver L485-496 vs ModExportXmlWriter L752-766)
  - `GenerateAttributesXml` (SkinSaver L529-558 vs ModExportXmlWriter L850-879)
  - `GenerateBaseAppearanceXml` (SkinSaver L590-628 vs ModExportXmlWriter L18-59)
  - `GenerateFaceConfigXml` (SkinSaver L698-820 vs ModExportXmlWriter L416-535)
  - `GenerateEyeDirectionConfigXml` (SkinSaver L870-890 vs ModExportXmlWriter L1516-1671)
  - `GenerateWeaponRenderConfigXml` (SkinSaver L899-919 vs ModExportXmlWriter L1673-1689)
  - `GenerateWeaponCarryVisualXml` (SkinSaver L921-938 vs ModExportXmlWriter L1691-1708)
  - `DirectXmlToInnerElements` (SkinSaver L892-897 vs ModExportXmlWriter L554-559)

  更严重的是，这些重复实现之间存在微妙差异：
  1. SkinSaver 中 `GenerateEquipmentRenderDataXml` 不输出 triggered animation 相关字段，而 ModExportXmlWriter 版本更完整
  2. SkinSaver 中 `GenerateRuntimeComponentsXml` 缺少 PawnFlyer 相关字段（flyerThingDefName 等），而 ModExportXmlWriter 版本包含这些
  3. SkinSaver 中的 `GenerateEyeDirectionConfigXml` 使用 DirectXmlSaver 序列化 lidMotion/eyeMotion/pupilMotion，而 ModExportXmlWriter 版本手动逐字段序列化每个参数
  4. SkinSaver 中 `GenerateVisualEffectsXml` 输出的字段比 ModExportXmlWriter 的 `GenerateAbilityVisualEffectsXml` 多很多（包含 textureSource, customTexturePath, displayDurationTicks, linkedExpression, sound 相关, drawSize 等）

  这意味着修改任何序列化逻辑都需要在两处同步修改，极易引发不一致 Bug。
- 建议:

  将 SkinSaver 的序列化逻辑统一委托给 ModExportXmlWriter，SkinSaver 只负责 'Save to file' 的 IO 层面工作。对于 SkinSaver 特有的字段（如 statModifiers），可在 ModExportXmlWriter 中补充对应方法。
- 证据:
  - `Source/CharacterStudio/Exporter/SkinSaver.cs`
  - `Source/CharacterStudio/Exporter/ModExportXmlWriter.cs`

### ModBuilder 中残留无用的委托包装方法

- ID: F-ARCH-02
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: ms-architecture
- 说明:

  ModBuilder 中有多个方法只是简单包装了对 ModExportXmlWriter 的调用，没有任何额外逻辑。这些方法是从 ModBuilder 职责拆分到 ModExportXmlWriter 后遗留的历史产物：

  - `GenerateBaseAppearanceXml` (L819-822) → 直接调用 ModExportXmlWriter.GenerateBaseAppearanceXml
  - `GenerateLayersXml` (L824-827) → 直接调用 ModExportXmlWriter.GenerateLayersXml
  - `GenerateStringListXml` (L829-832) → 直接调用 ModExportXmlWriter.GenerateStringListXml
  - `GenerateFaceConfigXml` (L834-837) → 直接调用 ModExportXmlWriter.GenerateFaceConfigXml
  - `GenerateTargetRacesXml` (L839-842) → 直接调用 ModExportXmlWriter.GenerateTargetRacesXml
  - `GenerateSkinAbilitiesXml` (L844-847) → 直接调用 ModExportXmlWriter.GenerateSkinAbilitiesXml
  - `GenerateSkinAbilityEffectsXml` (L849-852) → 直接调用
  - `GenerateRuntimeComponentsXml` (L854-857) → 直接调用
  - `GenerateAbilityHotkeysXml` (L859-862) → 直接调用
  - `CreatePawnKindDefElement` (L1004-1007) → 直接调用
  - `GenerateAbilitiesXml` (L1022-1025) → 直接调用
  - `GenerateEffectsXml` (L1027-1030) → 直接调用

  这些方法现在都是 private 且未在 ModBuilder 内部使用（SkinDef 的生成已交给 ModExportXmlWriter.CreateSkinDefDocument），属于死代码。
- 建议:

  删除这些无用的委托包装方法，减少认知负担。如果需要在 ModBuilder 中保留某些引用，直接调用 ModExportXmlWriter 的静态方法即可。
- 证据:
  - `Source/CharacterStudio/Exporter/ModBuilder.cs:819-862`
  - `Source/CharacterStudio/Exporter/ModBuilder.cs`

### XmlExporter 与其他序列化路径职责重叠

- ID: F-ARCH-03
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: ms-architecture
- 说明:

  XmlExporter.cs 提供了一个将 PawnSkinDef 导出为 PawnRenderNodeDef 的功能。该文件内部又重新实现了 Vector3/Vector2/Color 格式化方法 (ToVec3, ToVec2, ToColor) 和 SanitizeDefName 方法，与 ModExportXmlWriter 的 FormatVector3/FormatVector2/FormatColor 及 ModBuilder 的 SanitizeFileName 功能相同但实现独立。

  另外 XmlExporter 的 Vector2 格式化使用 F3 精度，而 ModExportXmlWriter 使用 F2 精度，可能导致同一数据在不同导出路径下精度不一致。
- 建议:

  将通用格式化方法统一抽取到一个共享的工具类（如 XmlFormatUtility），所有导出路径共享。

### ModBuilder.GenerateGeneDefXml 存在未使用的局部变量

- ID: F-ROB-01
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: ms-robustness
- 说明:

  ModBuilder.cs L868-884 中 `GenerateGeneDefXml` 方法声明了 `geneDefName`, `skinDefName`, `iconPath` 三个局部变量，但实际没有任何地方使用它们（文档生成已委托给 `ModExportXmlWriter.CreateGeneDefDocument(config, safeName)`）。这些是重构后的遗留代码。
- 建议:

  删除 L873-877 的三个未使用局部变量 `geneDefName`, `skinDefName`, `iconPath`。
- 证据:
  - `Source/CharacterStudio/Exporter/ModBuilder.cs:868-884`
  - `Source/CharacterStudio/Exporter/ModBuilder.cs`

### ModBuilder.GenerateUnitDefXml 存在未使用的局部变量

- ID: F-ROB-02
- 严重级别: 低
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: ms-robustness
- 说明:

  ModBuilder.cs L891-924 中 `GenerateUnitDefXml` 方法声明了 `pawnKindName`, `thingDefName`, `skinDefName` 三个局部变量，但同样只将 `safeName` 传给了 `ModExportXmlWriter.CreateUnitDefDocument(config, safeName)`。这些变量也是重构遗留。
- 建议:

  删除 L896-898 的三个未使用局部变量。
- 证据:
  - `Source/CharacterStudio/Exporter/ModBuilder.cs:891-924`
  - `Source/CharacterStudio/Exporter/ModBuilder.cs`

### 纹理搜索扩展名列表不一致

- ID: F-ROB-03
- 严重级别: 中
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: ms-robustness
- 说明:

  纹理文件搜索时使用的扩展名列表在两个位置不一致：

  1. `ExportAssetUtility.DetectAssetSource` (L74): `[".png", ".PNG", ".jpg", ".JPG", ".jpeg", ".JPEG"]` — 6 项
  2. `ModBuilder.FindSourceTexture` (L708): `[".png", ".PNG", ".jpg", ".JPG", ".jpeg"]` — 5 项，缺少 `.JPEG`

  虽然在 Windows 上文件系统不区分大小写所以影响不大，但在 Linux 服务器上（如果使用 Mono 或 .NET Core 运行时）会导致无法匹配 `.JPEG` 后缀的纹理文件。

  建议统一为一个常量数组并使用大小写不敏感的文件存在检查。
- 建议:

  定义一个静态只读常量 `private static readonly string[] TextureExtensions = { ".png", ".jpg", ".jpeg" };`，然后搜索时统一进行大小写不敏感匹配。
- 证据:
  - `Source/CharacterStudio/Exporter/ExportAssetUtility.cs:74`
  - `Source/CharacterStudio/Exporter/ModBuilder.cs:708`
  - `Source/CharacterStudio/Exporter/ExportAssetUtility.cs`
  - `Source/CharacterStudio/Exporter/ModBuilder.cs`

### SkinSaver 的原子写入逻辑有缺陷

- ID: F-ROB-04
- 严重级别: 中
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: ms-robustness
- 说明:

  SkinSaver.SaveSkinDef (L49-65) 尝试实现原子写入：先写入 `.tmp` 文件，然后替换原文件。但实现有问题：

  ```csharp
  if (File.Exists(filePath))
  {
      File.Copy(tempFilePath, filePath, true);  // 覆盖原文件
      File.Delete(tempFilePath);                // 删除临时文件
  }
  else
  {
      File.Move(tempFilePath, filePath);         // 直接重命名
  }
  ```

  当目标文件已存在时，使用 `File.Copy + File.Delete` 而不是 `File.Move` 的原因是 Windows 上 `File.Move` 不支持覆盖。但这种方式不是原子的——如果在 `File.Copy` 之后、`File.Delete` 之前崩溃，会留下 `.tmp` 文件。

  更关键的是，如果 `File.Copy` 过程中发生 IO 错误（比如磁盘满），原文件可能已被部分覆盖导致数据损坏。更安全的做法是先备份原文件。
- 建议:

  改用 `File.Replace(sourceFileName, destinationFileName, backupFileName)` API，它在 Windows 上提供更好的原子性保证，会自动创建备份。或者使用 'write to temp → rename old to backup → rename temp to target → delete backup' 模式。
- 证据:
  - `Source/CharacterStudio/Exporter/SkinSaver.cs:49-65`
  - `Source/CharacterStudio/Exporter/SkinSaver.cs`

### CloneExportConfig 浅拷贝 Abilities 列表存在副作用风险

- ID: F-ROB-05
- 严重级别: 中
- 分类: JavaScript
- 跟踪状态: 开放
- 相关里程碑: ms-robustness
- 说明:

  ModBuilder.CloneExportConfig (L138-170) 中对 `config.Abilities` 使用直接赋值（浅拷贝）而非深拷贝：
  ```csharp
  Abilities = config.Abilities, // 浅拷贝，指向同一个 List
  ```

  而在后续的 `PrepareEquipmentExportBindings` 中会修改 `config.Abilities`（添加新元素、修改 runtimeComponents 的字段）。虽然 SkinDef 做了深拷贝 (`config.SkinDef!.Clone()`)，但 Abilities 列表仍然共享，导致 `PrepareEquipmentExportBindings` 的修改会影响原始传入的配置对象。

  同样 `SourceTexturePaths` 也是浅拷贝。
- 建议:

  对 Abilities 也进行深拷贝：`Abilities = config.Abilities?.Select(a => a?.Clone()).Where(a => a != null).ToList() ?? new List<ModularAbilityDef>()`，或者至少创建一个新的 List 副本 `new List<ModularAbilityDef>(config.Abilities)`。
- 证据:
  - `Source/CharacterStudio/Exporter/ModBuilder.cs:138-170`
  - `Source/CharacterStudio/Exporter/ModBuilder.cs`

### RecipeDef 硬编码中文字符串

- ID: F-ROB-06
- 严重级别: 中
- 分类: 可维护性
- 跟踪状态: 开放
- 相关里程碑: ms-robustness
- 说明:

  ModExportXmlWriter.GenerateEquipmentRecipeDefXml (L1003-1004) 中硬编码了中文字符串：
  ```csharp
  new XElement("label", $"制作{equipment.GetDisplayLabel()}"),
  new XElement("jobString", $"正在制作{equipment.GetDisplayLabel()}"),
  ```

  这会导致：
  1. 导出的 mod 在非中文环境下显示中文 label/jobString
  2. 国际化支持缺失

  作为 mod 导出工具，输出的 XML 应该使用英文或支持多语言。
- 建议:

  改为英文默认值（如 `"Make {label}"` / `"Making {label}"`），或者生成 Keys 本地化文件使之支持 i18n。
- 证据:
  - `Source/CharacterStudio/Exporter/ModExportXmlWriter.cs:1003-1004`
  - `Source/CharacterStudio/Exporter/ModExportXmlWriter.cs`

## 评审里程碑

### ms-architecture · 架构设计与职责划分审查

- 状态: 已完成
- 记录时间: 2026-04-08T22:14:51.729Z
- 已审模块: Exporter/ExportAssetUtility.cs, Exporter/ModBuilder.cs, Exporter/ModExportXmlWriter.cs, Exporter/SkinSaver.cs, Exporter/XmlExporter.cs
- 摘要:

  对导出器模块的整体架构、文件职责划分、模块间耦合关系进行了全面审查。模块整体遵循了 Pipeline 模式，职责分层基本清晰，但存在严重的代码重复问题和一些架构缺陷。
- 结论:

  对导出器模块的整体架构、文件职责划分、模块间耦合关系进行了全面审查。模块整体遵循了 Pipeline 模式，职责分层基本清晰，但存在严重的代码重复问题和一些架构缺陷。
- 问题:
  - [高] 可维护性: SkinSaver 与 ModExportXmlWriter 之间存在大规模代码重复
  - [中] 可维护性: ModBuilder 中残留无用的委托包装方法
  - [低] 可维护性: XmlExporter 与其他序列化路径职责重叠

### ms-robustness · 健壮性、错误处理与潜在Bug审查

- 状态: 已完成
- 记录时间: 2026-04-08T22:15:50.913Z
- 摘要:

  审查了导出器模块中的输入校验、错误处理、边界条件、文件 IO 安全性等方面。整体错误处理框架基本到位（Pipeline 级别的 try-catch + cleanup），但细节层面存在多个可改进点。
- 结论:

  审查了导出器模块中的输入校验、错误处理、边界条件、文件 IO 安全性等方面。整体错误处理框架基本到位（Pipeline 级别的 try-catch + cleanup），但细节层面存在多个可改进点。
- 问题:
  - [低] 可维护性: ModBuilder.GenerateGeneDefXml 存在未使用的局部变量
  - [低] 可维护性: ModBuilder.GenerateUnitDefXml 存在未使用的局部变量
  - [中] JavaScript: 纹理搜索扩展名列表不一致
  - [中] JavaScript: SkinSaver 的原子写入逻辑有缺陷
  - [中] JavaScript: CloneExportConfig 浅拷贝 Abilities 列表存在副作用风险
  - [中] 可维护性: RecipeDef 硬编码中文字符串

## 最终结论

## 总体评价

CharacterStudio 的导出器模块在功能完整性上表现出色，涵盖了皮肤定义保存、完整 mod 包构建、基因/PawnKind/装备/技能/触发器等多种 Def 类型的 XML 导出，Pipeline 流程清晰、导出失败时有清理机制、资产权利确认机制到位。代码整体可用，但存在一个需要优先解决的架构级问题和多个中等优先级的改进项。

### 优点
1. **导出 Pipeline 设计良好** — ModBuilder.Export → CloneConfig → NormalizeModuleSelection → ExecuteExportPipeline 流程清晰，有 try-catch 和 CleanupFailedExport 回滚
2. **资产溯源能力强** — ExportAssetUtility 能区分 VanillaContent/ExternalMod/LocalFile，为版权合规提供了基础
3. **模块化导出选项丰富** — 支持 CosmeticPack/FullUnit 两种模式，包含精细的模块开关（SkinDef/GeneDef/PawnKind/Abilities 等）
4. **SkinSaver 的故障分类** — 针对磁盘满、权限不足等 IO 错误提供了专门的分类和本地化错误消息 Key
5. **装备-技能-Flyer 绑定逻辑** — PrepareEquipmentExportBindings 能自动关联装备与技能的 FlyerThingDef，减少手动配置

### 需要改进的关键问题
1. **[高] SkinSaver 与 ModExportXmlWriter 之间约 15 个方法存在大规模代码重复**，且重复之间存在微妙差异（字段遗漏、序列化策略不同），是最大的维护风险
2. **[中] ModBuilder 有约 12 个死代码方法**需要清理
3. **[中] CloneExportConfig 对 Abilities 的浅拷贝**可能导致导出操作污染原始配置
4. **[中] SkinSaver 原子写入实现有缺陷**，在 Copy 失败时可能损坏原文件
5. **[中] 纹理扩展名列表不一致** — 两处定义不同的扩展名集合
6. **[中] RecipeDef 硬编码中文** — 导出的 mod 在英文环境下显示中文

### 优化路线建议（优先级排序）
1. **P0**: 统一序列化路径 — SkinSaver 改为调用 ModExportXmlWriter 的方法，消除重复
2. **P1**: 修复 CloneExportConfig 的 Abilities 浅拷贝问题
3. **P1**: 改进 SkinSaver 原子写入为 File.Replace 模式
4. **P2**: 清理 ModBuilder 中的死代码方法和未使用局部变量
5. **P2**: 统一纹理扩展名常量、提取格式化工具类
6. **P3**: RecipeDef 国际化处理

## 评审快照

```json
{
  "formatVersion": 4,
  "kind": "limcode.review",
  "reviewRunId": "review-mnqlumjx-8od6lh",
  "createdAt": "2026-04-08T00:00:00.000Z",
  "updatedAt": "2026-04-08T22:16:32.248Z",
  "finalizedAt": "2026-04-08T22:16:32.248Z",
  "status": "completed",
  "overallDecision": "conditionally_accepted",
  "header": {
    "title": "CharacterStudio Exporter Code Review",
    "date": "2026-04-08",
    "overview": "对 CharacterStudio 导出器模块 (Exporter/) 的全面代码审查与优化建议"
  },
  "scope": {
    "markdown": "# CharacterStudio Exporter 模块代码审查\n\n## 审查范围\n- `Exporter/ExportAssetUtility.cs` — 资产枚举与来源识别\n- `Exporter/ModBuilder.cs` — 核心导出构建器\n- `Exporter/ModExportXmlWriter.cs` — XML 片段序列化\n- `Exporter/SkinSaver.cs` — 皮肤定义持久化\n- `Exporter/XmlExporter.cs` — PawnRenderNodeDef 导出\n\n## 审查目标\n1. 代码质量与可维护性\n2. 重复代码识别\n3. 健壮性与错误处理\n4. 性能考量\n5. 架构设计合理性"
  },
  "summary": {
    "latestConclusion": "## 总体评价\n\nCharacterStudio 的导出器模块在功能完整性上表现出色，涵盖了皮肤定义保存、完整 mod 包构建、基因/PawnKind/装备/技能/触发器等多种 Def 类型的 XML 导出，Pipeline 流程清晰、导出失败时有清理机制、资产权利确认机制到位。代码整体可用，但存在一个需要优先解决的架构级问题和多个中等优先级的改进项。\n\n### 优点\n1. **导出 Pipeline 设计良好** — ModBuilder.Export → CloneConfig → NormalizeModuleSelection → ExecuteExportPipeline 流程清晰，有 try-catch 和 CleanupFailedExport 回滚\n2. **资产溯源能力强** — ExportAssetUtility 能区分 VanillaContent/ExternalMod/LocalFile，为版权合规提供了基础\n3. **模块化导出选项丰富** — 支持 CosmeticPack/FullUnit 两种模式，包含精细的模块开关（SkinDef/GeneDef/PawnKind/Abilities 等）\n4. **SkinSaver 的故障分类** — 针对磁盘满、权限不足等 IO 错误提供了专门的分类和本地化错误消息 Key\n5. **装备-技能-Flyer 绑定逻辑** — PrepareEquipmentExportBindings 能自动关联装备与技能的 FlyerThingDef，减少手动配置\n\n### 需要改进的关键问题\n1. **[高] SkinSaver 与 ModExportXmlWriter 之间约 15 个方法存在大规模代码重复**，且重复之间存在微妙差异（字段遗漏、序列化策略不同），是最大的维护风险\n2. **[中] ModBuilder 有约 12 个死代码方法**需要清理\n3. **[中] CloneExportConfig 对 Abilities 的浅拷贝**可能导致导出操作污染原始配置\n4. **[中] SkinSaver 原子写入实现有缺陷**，在 Copy 失败时可能损坏原文件\n5. **[中] 纹理扩展名列表不一致** — 两处定义不同的扩展名集合\n6. **[中] RecipeDef 硬编码中文** — 导出的 mod 在英文环境下显示中文\n\n### 优化路线建议（优先级排序）\n1. **P0**: 统一序列化路径 — SkinSaver 改为调用 ModExportXmlWriter 的方法，消除重复\n2. **P1**: 修复 CloneExportConfig 的 Abilities 浅拷贝问题\n3. **P1**: 改进 SkinSaver 原子写入为 File.Replace 模式\n4. **P2**: 清理 ModBuilder 中的死代码方法和未使用局部变量\n5. **P2**: 统一纹理扩展名常量、提取格式化工具类\n6. **P3**: RecipeDef 国际化处理",
    "recommendedNextAction": "优先处理 F-ARCH-01（SkinSaver 与 ModExportXmlWriter 代码重复），将 SkinSaver 的 XML 序列化逻辑统一委托给 ModExportXmlWriter，消除约 600 行重复代码。",
    "reviewedModules": [
      "Exporter/ExportAssetUtility.cs",
      "Exporter/ModBuilder.cs",
      "Exporter/ModExportXmlWriter.cs",
      "Exporter/SkinSaver.cs",
      "Exporter/XmlExporter.cs"
    ]
  },
  "stats": {
    "totalMilestones": 2,
    "completedMilestones": 2,
    "totalFindings": 9,
    "severity": {
      "high": 1,
      "medium": 5,
      "low": 3
    }
  },
  "milestones": [
    {
      "id": "ms-architecture",
      "title": "架构设计与职责划分审查",
      "status": "completed",
      "recordedAt": "2026-04-08T22:14:51.729Z",
      "summaryMarkdown": "对导出器模块的整体架构、文件职责划分、模块间耦合关系进行了全面审查。模块整体遵循了 Pipeline 模式，职责分层基本清晰，但存在严重的代码重复问题和一些架构缺陷。",
      "conclusionMarkdown": "对导出器模块的整体架构、文件职责划分、模块间耦合关系进行了全面审查。模块整体遵循了 Pipeline 模式，职责分层基本清晰，但存在严重的代码重复问题和一些架构缺陷。",
      "evidence": [],
      "reviewedModules": [
        "Exporter/ExportAssetUtility.cs",
        "Exporter/ModBuilder.cs",
        "Exporter/ModExportXmlWriter.cs",
        "Exporter/SkinSaver.cs",
        "Exporter/XmlExporter.cs"
      ],
      "recommendedNextAction": null,
      "findingIds": [
        "F-ARCH-01",
        "F-ARCH-02",
        "F-ARCH-03"
      ]
    },
    {
      "id": "ms-robustness",
      "title": "健壮性、错误处理与潜在Bug审查",
      "status": "completed",
      "recordedAt": "2026-04-08T22:15:50.913Z",
      "summaryMarkdown": "审查了导出器模块中的输入校验、错误处理、边界条件、文件 IO 安全性等方面。整体错误处理框架基本到位（Pipeline 级别的 try-catch + cleanup），但细节层面存在多个可改进点。",
      "conclusionMarkdown": "审查了导出器模块中的输入校验、错误处理、边界条件、文件 IO 安全性等方面。整体错误处理框架基本到位（Pipeline 级别的 try-catch + cleanup），但细节层面存在多个可改进点。",
      "evidence": [],
      "reviewedModules": [],
      "recommendedNextAction": null,
      "findingIds": [
        "F-ROB-01",
        "F-ROB-02",
        "F-ROB-03",
        "F-ROB-04",
        "F-ROB-05",
        "F-ROB-06"
      ]
    }
  ],
  "findings": [
    {
      "id": "F-ARCH-01",
      "severity": "high",
      "category": "maintainability",
      "title": "SkinSaver 与 ModExportXmlWriter 之间存在大规模代码重复",
      "descriptionMarkdown": "SkinSaver.cs 和 ModExportXmlWriter.cs 各自独立实现了几乎相同的 XML 序列化逻辑。以下方法在两个文件中存在近乎完全一致的重复实现：\n\n- `GenerateEquipmentsXml` (SkinSaver L195-246 vs ModExportXmlWriter L78-138)\n- `GenerateEquipmentStatEntriesXml` (SkinSaver L248-270 vs ModExportXmlWriter L140-162)\n- `GenerateEquipmentCostEntriesXml` (SkinSaver L272-294 vs ModExportXmlWriter L164-186)\n- `GenerateEquipmentRenderDataXml` (SkinSaver L296-324 vs ModExportXmlWriter L188-246)\n- `GenerateAbilitiesXml` / `GenerateSkinAbilitiesXml` (SkinSaver L326-360 vs ModExportXmlWriter L561-598)\n- `GenerateEffectsXml` / `GenerateSkinAbilityEffectsXml` (SkinSaver L362-392 vs ModExportXmlWriter L600-633)\n- `GenerateRuntimeComponentsXml` (SkinSaver L394-483 vs ModExportXmlWriter L635-750)\n- `GenerateAbilityHotkeysXml` (SkinSaver L485-496 vs ModExportXmlWriter L752-766)\n- `GenerateAttributesXml` (SkinSaver L529-558 vs ModExportXmlWriter L850-879)\n- `GenerateBaseAppearanceXml` (SkinSaver L590-628 vs ModExportXmlWriter L18-59)\n- `GenerateFaceConfigXml` (SkinSaver L698-820 vs ModExportXmlWriter L416-535)\n- `GenerateEyeDirectionConfigXml` (SkinSaver L870-890 vs ModExportXmlWriter L1516-1671)\n- `GenerateWeaponRenderConfigXml` (SkinSaver L899-919 vs ModExportXmlWriter L1673-1689)\n- `GenerateWeaponCarryVisualXml` (SkinSaver L921-938 vs ModExportXmlWriter L1691-1708)\n- `DirectXmlToInnerElements` (SkinSaver L892-897 vs ModExportXmlWriter L554-559)\n\n更严重的是，这些重复实现之间存在微妙差异：\n1. SkinSaver 中 `GenerateEquipmentRenderDataXml` 不输出 triggered animation 相关字段，而 ModExportXmlWriter 版本更完整\n2. SkinSaver 中 `GenerateRuntimeComponentsXml` 缺少 PawnFlyer 相关字段（flyerThingDefName 等），而 ModExportXmlWriter 版本包含这些\n3. SkinSaver 中的 `GenerateEyeDirectionConfigXml` 使用 DirectXmlSaver 序列化 lidMotion/eyeMotion/pupilMotion，而 ModExportXmlWriter 版本手动逐字段序列化每个参数\n4. SkinSaver 中 `GenerateVisualEffectsXml` 输出的字段比 ModExportXmlWriter 的 `GenerateAbilityVisualEffectsXml` 多很多（包含 textureSource, customTexturePath, displayDurationTicks, linkedExpression, sound 相关, drawSize 等）\n\n这意味着修改任何序列化逻辑都需要在两处同步修改，极易引发不一致 Bug。",
      "recommendationMarkdown": "将 SkinSaver 的序列化逻辑统一委托给 ModExportXmlWriter，SkinSaver 只负责 'Save to file' 的 IO 层面工作。对于 SkinSaver 特有的字段（如 statModifiers），可在 ModExportXmlWriter 中补充对应方法。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Exporter/SkinSaver.cs"
        },
        {
          "path": "Source/CharacterStudio/Exporter/ModExportXmlWriter.cs"
        }
      ],
      "relatedMilestoneIds": [
        "ms-architecture"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ARCH-02",
      "severity": "medium",
      "category": "maintainability",
      "title": "ModBuilder 中残留无用的委托包装方法",
      "descriptionMarkdown": "ModBuilder 中有多个方法只是简单包装了对 ModExportXmlWriter 的调用，没有任何额外逻辑。这些方法是从 ModBuilder 职责拆分到 ModExportXmlWriter 后遗留的历史产物：\n\n- `GenerateBaseAppearanceXml` (L819-822) → 直接调用 ModExportXmlWriter.GenerateBaseAppearanceXml\n- `GenerateLayersXml` (L824-827) → 直接调用 ModExportXmlWriter.GenerateLayersXml\n- `GenerateStringListXml` (L829-832) → 直接调用 ModExportXmlWriter.GenerateStringListXml\n- `GenerateFaceConfigXml` (L834-837) → 直接调用 ModExportXmlWriter.GenerateFaceConfigXml\n- `GenerateTargetRacesXml` (L839-842) → 直接调用 ModExportXmlWriter.GenerateTargetRacesXml\n- `GenerateSkinAbilitiesXml` (L844-847) → 直接调用 ModExportXmlWriter.GenerateSkinAbilitiesXml\n- `GenerateSkinAbilityEffectsXml` (L849-852) → 直接调用\n- `GenerateRuntimeComponentsXml` (L854-857) → 直接调用\n- `GenerateAbilityHotkeysXml` (L859-862) → 直接调用\n- `CreatePawnKindDefElement` (L1004-1007) → 直接调用\n- `GenerateAbilitiesXml` (L1022-1025) → 直接调用\n- `GenerateEffectsXml` (L1027-1030) → 直接调用\n\n这些方法现在都是 private 且未在 ModBuilder 内部使用（SkinDef 的生成已交给 ModExportXmlWriter.CreateSkinDefDocument），属于死代码。",
      "recommendationMarkdown": "删除这些无用的委托包装方法，减少认知负担。如果需要在 ModBuilder 中保留某些引用，直接调用 ModExportXmlWriter 的静态方法即可。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs",
          "lineStart": 819,
          "lineEnd": 862
        },
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs"
        }
      ],
      "relatedMilestoneIds": [
        "ms-architecture"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ARCH-03",
      "severity": "low",
      "category": "maintainability",
      "title": "XmlExporter 与其他序列化路径职责重叠",
      "descriptionMarkdown": "XmlExporter.cs 提供了一个将 PawnSkinDef 导出为 PawnRenderNodeDef 的功能。该文件内部又重新实现了 Vector3/Vector2/Color 格式化方法 (ToVec3, ToVec2, ToColor) 和 SanitizeDefName 方法，与 ModExportXmlWriter 的 FormatVector3/FormatVector2/FormatColor 及 ModBuilder 的 SanitizeFileName 功能相同但实现独立。\n\n另外 XmlExporter 的 Vector2 格式化使用 F3 精度，而 ModExportXmlWriter 使用 F2 精度，可能导致同一数据在不同导出路径下精度不一致。",
      "recommendationMarkdown": "将通用格式化方法统一抽取到一个共享的工具类（如 XmlFormatUtility），所有导出路径共享。",
      "evidence": [],
      "relatedMilestoneIds": [
        "ms-architecture"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ROB-01",
      "severity": "low",
      "category": "maintainability",
      "title": "ModBuilder.GenerateGeneDefXml 存在未使用的局部变量",
      "descriptionMarkdown": "ModBuilder.cs L868-884 中 `GenerateGeneDefXml` 方法声明了 `geneDefName`, `skinDefName`, `iconPath` 三个局部变量，但实际没有任何地方使用它们（文档生成已委托给 `ModExportXmlWriter.CreateGeneDefDocument(config, safeName)`）。这些是重构后的遗留代码。",
      "recommendationMarkdown": "删除 L873-877 的三个未使用局部变量 `geneDefName`, `skinDefName`, `iconPath`。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs",
          "lineStart": 868,
          "lineEnd": 884
        },
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs"
        }
      ],
      "relatedMilestoneIds": [
        "ms-robustness"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ROB-02",
      "severity": "low",
      "category": "maintainability",
      "title": "ModBuilder.GenerateUnitDefXml 存在未使用的局部变量",
      "descriptionMarkdown": "ModBuilder.cs L891-924 中 `GenerateUnitDefXml` 方法声明了 `pawnKindName`, `thingDefName`, `skinDefName` 三个局部变量，但同样只将 `safeName` 传给了 `ModExportXmlWriter.CreateUnitDefDocument(config, safeName)`。这些变量也是重构遗留。",
      "recommendationMarkdown": "删除 L896-898 的三个未使用局部变量。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs",
          "lineStart": 891,
          "lineEnd": 924
        },
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs"
        }
      ],
      "relatedMilestoneIds": [
        "ms-robustness"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ROB-03",
      "severity": "medium",
      "category": "javascript",
      "title": "纹理搜索扩展名列表不一致",
      "descriptionMarkdown": "纹理文件搜索时使用的扩展名列表在两个位置不一致：\n\n1. `ExportAssetUtility.DetectAssetSource` (L74): `[\".png\", \".PNG\", \".jpg\", \".JPG\", \".jpeg\", \".JPEG\"]` — 6 项\n2. `ModBuilder.FindSourceTexture` (L708): `[\".png\", \".PNG\", \".jpg\", \".JPG\", \".jpeg\"]` — 5 项，缺少 `.JPEG`\n\n虽然在 Windows 上文件系统不区分大小写所以影响不大，但在 Linux 服务器上（如果使用 Mono 或 .NET Core 运行时）会导致无法匹配 `.JPEG` 后缀的纹理文件。\n\n建议统一为一个常量数组并使用大小写不敏感的文件存在检查。",
      "recommendationMarkdown": "定义一个静态只读常量 `private static readonly string[] TextureExtensions = { \".png\", \".jpg\", \".jpeg\" };`，然后搜索时统一进行大小写不敏感匹配。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Exporter/ExportAssetUtility.cs",
          "lineStart": 74,
          "lineEnd": 74
        },
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs",
          "lineStart": 708,
          "lineEnd": 708
        },
        {
          "path": "Source/CharacterStudio/Exporter/ExportAssetUtility.cs"
        },
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs"
        }
      ],
      "relatedMilestoneIds": [
        "ms-robustness"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ROB-04",
      "severity": "medium",
      "category": "javascript",
      "title": "SkinSaver 的原子写入逻辑有缺陷",
      "descriptionMarkdown": "SkinSaver.SaveSkinDef (L49-65) 尝试实现原子写入：先写入 `.tmp` 文件，然后替换原文件。但实现有问题：\n\n```csharp\nif (File.Exists(filePath))\n{\n    File.Copy(tempFilePath, filePath, true);  // 覆盖原文件\n    File.Delete(tempFilePath);                // 删除临时文件\n}\nelse\n{\n    File.Move(tempFilePath, filePath);         // 直接重命名\n}\n```\n\n当目标文件已存在时，使用 `File.Copy + File.Delete` 而不是 `File.Move` 的原因是 Windows 上 `File.Move` 不支持覆盖。但这种方式不是原子的——如果在 `File.Copy` 之后、`File.Delete` 之前崩溃，会留下 `.tmp` 文件。\n\n更关键的是，如果 `File.Copy` 过程中发生 IO 错误（比如磁盘满），原文件可能已被部分覆盖导致数据损坏。更安全的做法是先备份原文件。",
      "recommendationMarkdown": "改用 `File.Replace(sourceFileName, destinationFileName, backupFileName)` API，它在 Windows 上提供更好的原子性保证，会自动创建备份。或者使用 'write to temp → rename old to backup → rename temp to target → delete backup' 模式。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Exporter/SkinSaver.cs",
          "lineStart": 49,
          "lineEnd": 65
        },
        {
          "path": "Source/CharacterStudio/Exporter/SkinSaver.cs"
        }
      ],
      "relatedMilestoneIds": [
        "ms-robustness"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ROB-05",
      "severity": "medium",
      "category": "javascript",
      "title": "CloneExportConfig 浅拷贝 Abilities 列表存在副作用风险",
      "descriptionMarkdown": "ModBuilder.CloneExportConfig (L138-170) 中对 `config.Abilities` 使用直接赋值（浅拷贝）而非深拷贝：\n```csharp\nAbilities = config.Abilities, // 浅拷贝，指向同一个 List\n```\n\n而在后续的 `PrepareEquipmentExportBindings` 中会修改 `config.Abilities`（添加新元素、修改 runtimeComponents 的字段）。虽然 SkinDef 做了深拷贝 (`config.SkinDef!.Clone()`)，但 Abilities 列表仍然共享，导致 `PrepareEquipmentExportBindings` 的修改会影响原始传入的配置对象。\n\n同样 `SourceTexturePaths` 也是浅拷贝。",
      "recommendationMarkdown": "对 Abilities 也进行深拷贝：`Abilities = config.Abilities?.Select(a => a?.Clone()).Where(a => a != null).ToList() ?? new List<ModularAbilityDef>()`，或者至少创建一个新的 List 副本 `new List<ModularAbilityDef>(config.Abilities)`。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs",
          "lineStart": 138,
          "lineEnd": 170
        },
        {
          "path": "Source/CharacterStudio/Exporter/ModBuilder.cs"
        }
      ],
      "relatedMilestoneIds": [
        "ms-robustness"
      ],
      "trackingStatus": "open"
    },
    {
      "id": "F-ROB-06",
      "severity": "medium",
      "category": "maintainability",
      "title": "RecipeDef 硬编码中文字符串",
      "descriptionMarkdown": "ModExportXmlWriter.GenerateEquipmentRecipeDefXml (L1003-1004) 中硬编码了中文字符串：\n```csharp\nnew XElement(\"label\", $\"制作{equipment.GetDisplayLabel()}\"),\nnew XElement(\"jobString\", $\"正在制作{equipment.GetDisplayLabel()}\"),\n```\n\n这会导致：\n1. 导出的 mod 在非中文环境下显示中文 label/jobString\n2. 国际化支持缺失\n\n作为 mod 导出工具，输出的 XML 应该使用英文或支持多语言。",
      "recommendationMarkdown": "改为英文默认值（如 `\"Make {label}\"` / `\"Making {label}\"`），或者生成 Keys 本地化文件使之支持 i18n。",
      "evidence": [
        {
          "path": "Source/CharacterStudio/Exporter/ModExportXmlWriter.cs",
          "lineStart": 1003,
          "lineEnd": 1004
        },
        {
          "path": "Source/CharacterStudio/Exporter/ModExportXmlWriter.cs"
        }
      ],
      "relatedMilestoneIds": [
        "ms-robustness"
      ],
      "trackingStatus": "open"
    }
  ],
  "render": {
    "rendererVersion": 4,
    "bodyHash": "sha256:f07185dcafeca2ad3286f15d21afb08559db3d9c3a5e97a3dfd2ee8e00f26820",
    "generatedAt": "2026-04-08T22:16:32.248Z",
    "locale": "zh-CN"
  }
}
```
