# CharacterStudio 技能编辑器方案（模块化组装 / XML 导出 / Hediff 门控）

## 1. 目标

- 参考 Ars Nouveau（载体+形态+效果）与 Noita（法术模块串联）思路，做可视化“拼技能”。
- 编辑器内使用 `ModularAbilityDef` 作为中间模型，运行时执行沿用 `CompAbilityEffect_Modular` + `EffectWorkerFactory`。
- 最终落地为可加载的 `AbilityDef.xml`，并可绑定到指定 `HediffDef`：
  - 角色拥有 Hediff 时可用技能；
  - Hediff 移除后技能自动失效/移除。

---

## 2. 现有基础（可复用）

### 2.1 数据层

- `ModularAbilityDef`
  - 已含：基础属性（冷却、预热、次数、图标）、载体（Self/Touch/Target/Projectile/Area）、效果列表。
- `AbilityEffectConfig`
  - 已含：`amount`、`duration`、`chance`、`hediffDef`、`summonKind` 等。
- `ModularAbilityDefExtensions.Validate()`
  - 已有基础校验与效果校验。

### 2.2 执行层

- `CompAbilityEffect_Modular.Apply(...)`
  - 已支持遍历 `effects` 并分发给 `EffectWorkerFactory`。
- `EffectWorkerFactory`
  - 已支持注册与扩展新效果执行器。

### 2.3 UI 层

- `Dialog_AbilityEditor`
  - 已有“技能列表 + 属性面板 + 效果面板”雏形。

---

## 3. 架构设计（编辑器 -> 编译器 -> 运行时）

## 3.1 核心分层

1. **Editor Model（编辑态）**
   - 使用 `ModularAbilityDef` 作为主模型。
   - 扩展“门控配置”（见 3.3）。

2. **Compiler（导出态）**
   - `AbilityXmlExporter`：把 `ModularAbilityDef` 转成标准 `AbilityDef` XML。
   - `HediffBindingXmlExporter`：输出 Hediff 绑定定义（可独立 XML 文件）。

3. **Runtime（运行态）**
   - `CompAbilityEffect_Modular`：执行模块化效果。
   - `CompAbilityEffect_HediffGate`（新增）：施放前再做 Hediff 门控。
   - `HediffComp_GrantEditedAbility`（新增）：按 Hediff 生命周期授予/撤销技能。

---

## 3.2 “载体 + 效果”组装模型

### 载体（Carrier）

- X 轴：`Self` / `Touch` / `Target` / `Projectile` / `Area`
- 负责目标传递与命中形态（射程、半径、投射物）。

### 效果（Effect）

- Y 轴：`Damage`、`Heal`、`Buff/Debuff`、`Summon`、`Teleport`、`Control`、`Terraform`
- 通过列表顺序形成“串联”语义（Noita 风格）：前置效果可影响后续状态。

### 约束规则（建议补充）

- `Projectile` 载体必须有 `projectileDef`（当前是 Warning，建议升级 Error）。
- `Area` 载体必须 `radius > 0`。
- `Buff/Debuff` 必须有 `hediffDef`。
- 伤害/治疗 `amount > 0`。
- 效果链长度上限（例如 16）防止性能异常。

---

## 3.3 Hediff 门控模型（关键）

新增门控配置对象：

- `AbilityGateConfig`
  - `requiredHediffDef`：必需 Hediff。
  - `consumeOnCast`：施放时是否消耗层数/移除 Hediff。
  - `requiredSeverityMin`：最低严重度门槛（可选）。

挂载位置：

- 放在 `ModularAbilityDef` 中（编辑态字段），导出时写入 Ability 的 `modExtensions`。

示例（导出到 AbilityDef 的扩展段）：

```xml
<modExtensions>
  <li Class="CharacterStudio.Abilities.DefModExtension_AbilityGate">
    <requiredHediffDef>CS_ManaChannel</requiredHediffDef>
    <consumeOnCast>false</consumeOnCast>
    <requiredSeverityMin>0.2</requiredSeverityMin>
  </li>
</modExtensions>
```

---

## 4. XML 导出规范

## 4.1 输出文件建议

- `Defs/AbilityDefs/CS_EditedAbilities.xml`
- `Defs/HediffDefs/CS_EditedAbilityBindings.xml`

## 4.2 AbilityDef 导出映射

### 字段映射

- `defName` -> `<defName>`
- `label` -> `<label>`
- `description` -> `<description>`
- `cooldownTicks` -> `<cooldownTicksRange><min/max>`（或定值）
- `warmupTicks` -> `<warmupTime>`（按 RimWorld 版本映射）
- `range` -> `<verbProperties><range>`
- `radius` -> `<verbProperties><radius>`（Area/Projectile 时）
- `iconPath` -> `<iconPath>`

### 效果映射

采用 Ability comp 方式：

```xml
<comps>
  <li Class="CharacterStudio.Abilities.CompProperties_AbilityModular">
    <abilityDefName>CS_Ability_FireNova</abilityDefName>
  </li>
  <li Class="CharacterStudio.Abilities.CompProperties_AbilityHediffGate">
    <requiredHediffDef>CS_ManaChannel</requiredHediffDef>
  </li>
</comps>
```

> 说明：`CompProperties_AbilityModular` 当前已有，建议加一个字段 `abilityDefName` 或直接内嵌序列化后的 effects（二选一）。

## 4.3 完整 Ability XML 示例

```xml
<Defs>
  <AbilityDef>
    <defName>CS_Ability_FireNova</defName>
    <label>烈焰新星</label>
    <description>向目标点释放爆裂火环并附带灼烧。</description>
    <iconPath>UI/Abilities/FireNova</iconPath>
    <cooldownTicksRange>
      <min>600</min>
      <max>600</max>
    </cooldownTicksRange>
    <verbProperties>
      <range>24</range>
      <radius>3.5</radius>
      <targetParams>
        <canTargetPawns>true</canTargetPawns>
        <canTargetLocations>true</canTargetLocations>
      </targetParams>
    </verbProperties>
    <comps>
      <li Class="CharacterStudio.Abilities.CompProperties_AbilityModular">
        <modularDefName>CS_Modular_FireNova</modularDefName>
      </li>
      <li Class="CharacterStudio.Abilities.CompProperties_AbilityHediffGate">
        <requiredHediffDef>CS_ManaChannel</requiredHediffDef>
        <requiredSeverityMin>0.2</requiredSeverityMin>
      </li>
    </comps>
  </AbilityDef>
</Defs>
```

---

## 5. Hediff 挂载与权限门控流程

## 5.1 数据定义

新增 `HediffCompProperties_GrantEditedAbility`：

- `abilityDef`：授予的 AbilityDef。
- `removeOnHediffRemoved`：移除 Hediff 时是否撤销能力。
- `refreshIntervalTicks`：兜底同步间隔（防异常状态）。

Hediff XML 示例：

```xml
<Defs>
  <HediffDef>
    <defName>CS_ManaChannel</defName>
    <label>魔力通道</label>
    <hediffClass>HediffWithComps</hediffClass>
    <comps>
      <li Class="CharacterStudio.Abilities.HediffCompProperties_GrantEditedAbility">
        <abilityDef>CS_Ability_FireNova</abilityDef>
        <removeOnHediffRemoved>true</removeOnHediffRemoved>
        <refreshIntervalTicks>120</refreshIntervalTicks>
      </li>
    </comps>
  </HediffDef>
</Defs>
```

## 5.2 运行时时序

1. Pawn 获得 `CS_ManaChannel`。
2. `HediffComp_GrantEditedAbility.CompPostPostAdd` 触发，向 Pawn 授予 `CS_Ability_FireNova`。
3. 玩家点击技能时，`CompAbilityEffect_HediffGate` 先检查 Hediff 是否存在/严重度是否达标。
4. 检查通过才进入 `CompAbilityEffect_Modular.Apply` 执行效果链。
5. Hediff 被移除时，`CompPostPostRemoved` 撤销能力。

> 双重保险：**授予时门控 + 施放时门控**，避免缓存/同步延迟导致越权施法。

---

## 6. 编辑器交互设计（Dialog_AbilityEditor）

## 6.1 左侧：技能资产列表

- 新建 / 复制 / 删除 / 重命名。
- 状态图标：✅通过校验、⚠警告、❌错误。

## 6.2 中间：基础属性 + 载体

- 基础：名称、描述、图标、冷却、预热、次数。
- 载体：类型、射程、半径、投射物。
- 门控：required Hediff、最低 severity、是否施放消耗。

## 6.3 右侧：效果链（可排序）

- 添加效果、删除效果、拖拽排序。
- 每个效果显示参数模板（按 `AbilityEffectType` 动态表单）。
- 支持“测试施放（Dev）”按钮用于快速迭代。

## 6.4 底栏：验证 + 导出

- `Validate`：调用 `ModularAbilityDefExtensions.Validate` + 扩展校验。
- `Export XML`：输出 Ability 与 Hediff 绑定 XML。
- `Apply to Pawn (Debug)`：临时注入能力做联调。

---

## 7. 建议新增类型清单

- `CharacterStudio.Abilities.AbilityGateConfig`
- `CharacterStudio.Abilities.DefModExtension_AbilityGate`
- `CharacterStudio.Abilities.CompProperties_AbilityHediffGate`
- `CharacterStudio.Abilities.CompAbilityEffect_HediffGate`
- `CharacterStudio.Abilities.HediffCompProperties_GrantEditedAbility`
- `CharacterStudio.Abilities.HediffComp_GrantEditedAbility`
- `CharacterStudio.Exporter.AbilityXmlExporter`
- `CharacterStudio.Exporter.HediffBindingXmlExporter`

---

## 8. 与现有代码的衔接建议

- `Dialog_AbilityEditor` 继续作为主入口，不改窗口结构，仅补字段与导出按钮。
- `CompAbilityEffect_Modular` 保持“纯执行器”定位；门控逻辑不混入，交给独立 Gate comp。
- `EffectWorkerFactory` 维持开放注册，后续可加粒子/声音/连锁触发类型。
- `ModularAbilityDefExtensions.Validate` 增补载体-效果联合规则与性能阈值检查。

---

## 9. 最小可行版本（MVP）落地顺序

1. 实现 `AbilityGateConfig` + 编辑器门控字段。
2. 实现 `AbilityXmlExporter`，先导出 AbilityDef（含 Modular comp + Gate comp）。
3. 实现 `HediffComp_GrantEditedAbility`，完成“有 Hediff 才有技能”。
4. 实现 `CompAbilityEffect_HediffGate`，完成施放前校验。
5. 增加导出预览窗口（显示最终 XML 文本，便于调试）。

---

## 10. 零代码可用性设计（重点增强）

为满足“完全不懂代码/XML 也能做复杂技能”，编辑器需采用**引导式创作**而不是“参数堆砌”。

### 10.1 双模式编辑

- **新手模式（默认）**
  - 仅展示“技能意图”选项：
    - 我想做：单体伤害 / 范围爆发 / 持续治疗 / 召唤 / 控场。
  - 系统自动生成推荐 Carrier + Effects 组合。
  - 只暴露 3~5 个关键滑条：强度、范围、冷却、持续、命中稳定性。
- **专家模式**
  - 展开完整效果链与高级参数（chance、severity、条件触发等）。

### 10.2 模板库 + 一键变体

- 内置模板分类：
  - 输出类、生存类、控制类、召唤类、机制类（位移/地形）。
- 每个模板提供“变体按钮”：
  - 火球 -> 冰球（替换伤害类型与附加 Debuff）
  - 治疗光环 -> 战吼增益（改目标与持续）
- 模板均带说明卡片：用途、推荐职业、强度评级、性能评级。

### 10.3 可视化技能流程（节点图）

- 用“流程图节点”表示技能链：
  - 起点（载体）-> 命中阶段 -> 效果1 -> 效果2 -> 结束。
- 节点支持拖拽排序、复制分支、禁用测试。
- 每个节点悬浮提示“自然语言描述”：
  - 例如：`对半径 3.5 格敌人造成 25 点燃烧伤害，并附加灼烧 6 秒`。

### 10.4 实时预览与安全防错

- **实时文本预览**：把当前配置翻译成人话，不显示 XML。
- **平衡性仪表盘**：DPS、控制时长、召唤总量、预计影响单元数。
- **风险提示分级**：
  - 绿色（可用）
  - 黄色（偏强或有冲突）
  - 红色（无效配置，禁止导出）
- **自动修复按钮**：
  - “一键修复错误”自动补齐缺失字段（如 `projectileDef`、`hediffDef`）。

### 10.5 向导式发布流程（4 步）

1. 选择玩法意图（模板或空白）
2. 调整技能手感（范围/节奏/特效）
3. 绑定使用条件（Hediff 门控）
4. 预览并导出（自动生成 Ability + Hediff XML）

每步结束提供“通过/失败”状态，不通过时给明确修复建议。

### 10.6 特殊角色创作体验（你的场景）

- 提供“角色套装”概念：
  - 一个角色可保存 `外观 + 技能组 + Hediff门票` 为同一预设。
- 提供“角色主题词驱动”：
  - 输入关键词（如：血术师、风暴猎手），自动推荐 3 套技能方案。
- 支持导出“角色包”：
  - 包含该角色专属 AbilityDefs、HediffDefs、图标与说明文档。

### 10.7 新手保护策略

- 参数上限动态约束（防止爆表）。
- 冲突互斥规则（例如同一链路禁用互斥控制）。
- 回退历史（撤销/重做 + 自动存档）。
- 导出前模拟检查（空目标/越界目标/无 Hediff 情况）。

---

## 11. 结果

按本方案，用户即使不懂任何代码与 XML，也能通过 UI 完成复杂技能设计、校验、测试与导出；并通过 Hediff 挂载实现“拥有条件才可使用”的严格门控，满足特殊角色创作的易用性与可控性要求。