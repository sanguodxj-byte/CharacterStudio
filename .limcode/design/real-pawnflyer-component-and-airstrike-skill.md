# CharacterStudio 真实 PawnFlyer 组件与空中降落技能方案

## 背景与目标

当前 CharacterStudio 已有 `AbilityRuntimeComponentType.FlightState`，但其实现仅写入 `CompPawnSkin` 的自定义状态字段：

- `flightStateStartTick`
- `flightStateExpireTick`
- `flightStateHeightFactor`
- `suppressCombatActionsDuringFlightState`

该机制属于“自定义飞行姿态/演出状态”，**并不会让 Pawn 进入 RimWorld 1.6 原版 `PawnFlyer` 体系**。

本方案目标是：

1. 新增一个**全新的真实飞行组件**，不复用也不篡改现有 `FlightState` 语义。
2. 让角色能够真正进入原版 `PawnFlyer` 飞行状态。
3. 在此基础上支持一个“只有真实飞行中才能使用”的二段技能：
   - 高速升空
   - 飞出屏幕外
   - 再高速降落到另一个地图格
   - 落地产生范围伤害特效

该方案优先追求：

- 与 RimWorld 1.6 原版机制一致
- 与其他 mod 的 flyer 识别逻辑兼容
- 与现有 CharacterStudio 运行时组件体系尽量低耦合
- 对 HAR / 自定义渲染体系风险可控

---

## 总体思路

新增一套**独立于 `FlightState` 的原版飞行链路**：

`Ability Runtime Component -> VanillaFlight Launch Utility -> PawnJumper ThingDef -> Custom PawnFlyer -> Landing Callback / Followup Skill`

### 核心原则

- `FlightState` 保留：继续作为自定义飞行姿态/演出状态，不改语义。
- 新组件独立命名，例如：
  - `AbilityRuntimeComponentType.VanillaPawnFlyer`
  - `AbilityRuntimeComponentType.FlightOnlyFollowup`
  - `AbilityRuntimeComponentType.FlightLandingBurst`
- 真实飞行技能只走原版 `PawnFlyer`。
- 屏外转场与高速降落也应建立在真实飞行链路之上，而不是伪装渲染。

---

## 现有系统复用点

### 可复用

1. `ModularAbilityDef` / `AbilityRuntimeComponentConfig`
   - 可扩展新的 runtime component 类型与字段。
2. `CompAbilityEffect_Modular`
   - 可作为 runtime component 执行调度器。
3. `CompPawnSkin`
   - 可作为“记录当前是否处于真实飞行中”的轻量状态存储位置。
4. `AbilityLoadoutRuntimeUtility`
   - 可用于真实飞行中技能可见性/可用性判断。
5. 现有特效系统（VFX / Runtime components / Damage effects）
   - 可复用做落地爆发范围伤害。

### 不应复用

1. `FlightState`
   - 不能直接升级为原版飞行，否则会破坏旧语义和已有配置。
2. 仅通过渲染高度模拟的逻辑
   - 不能作为真实飞行判定依据。

---

## 新增组件设计

## 1. 真正飞行组件：`VanillaPawnFlyer`

### 新枚举项

在 `AbilityRuntimeComponentType` 中新增：

- `VanillaPawnFlyer`：启动原版真实飞行
- `FlightOnlyFollowup`：仅在真实飞行中可施放的后续技能
- `FlightLandingBurst`：飞行落地时触发范围伤害/特效

### `VanillaPawnFlyer` 配置字段建议

新增到 `AbilityRuntimeComponentConfig`：

- `string flyerThingDefName`
  - 对应 PawnJumper 的 ThingDef 名。
- `int flyerWarmupTicks`
  - 起飞前准备时间（可选）
- `bool launchFromCasterPosition = true`
- `bool requireValidTargetCell = true`
- `bool storeTargetForFollowup = true`
- `bool enableFlightOnlyWindow = false`
- `int flightOnlyWindowTicks = 180`
- `string flightOnlyAbilityDefName = ""`
- `bool hideCasterDuringTakeoff = true`
- `bool autoExpireFlightMarkerOnLanding = true`

### 运行时行为

当组件触发时：

1. 校验当前 caster、target、map
2. 查找 `flyerThingDefName`
3. 创建并启动原版 `PawnFlyer`
4. 在 `CompPawnSkin` 中记录：
   - `isInVanillaFlight = true`
   - `vanillaFlightSourceAbilityDefName`
   - `vanillaFlightStartTick`
   - `vanillaFlightExpireTick`（估算或事件驱动）
   - `vanillaFlightReservedTargetCell`
   - `vanillaFlightFollowupAbilityDefName`
5. 如果启用 followup，则在飞行期间开放后续技能窗口

---

## 2. 仅飞行中可用组件：`FlightOnlyFollowup`

### 用途

用于限制某个技能：

- 只有在 `CompPawnSkin.isInVanillaFlight == true` 时才可用
- 且可进一步要求：
  - 必须来自指定飞行技能
  - 必须在指定窗口内

### 配置字段建议

- `string requiredFlightSourceAbilityDefName = ""`
- `bool requireReservedTargetCell = false`
- `bool consumeFlightStateOnCast = false`
- `bool onlyUseDuringFlightWindow = true`

### 使用方式

例如“空中二段打击技能”：
- 只有当角色已经通过 `VanillaPawnFlyer` 升空后，技能栏才允许释放。

---

## 3. 落地爆发组件：`FlightLandingBurst`

### 用途

用于支持：

- 高速降落到目标格
- 落地后对半径范围内敌人造成伤害
- 播放 effecter / mote / 声音

### 配置字段建议

- `float landingBurstRadius = 3f`
- `float landingBurstDamage = 30f`
- `DamageDef landingBurstDamageDef`
- `string landingEffecterDefName = ""`
- `string landingSoundDefName = ""`
- `bool affectBuildings = false`
- `bool affectCells = true`
- `bool knockbackTargets = false`
- `float knockbackDistance = 1.5f`

### 触发方式

- 可绑定在 followup skill 的命中逻辑中
- 或绑定在 custom flyer landing callback 中

---

## 新增运行时宿主状态（CompPawnSkin）

为避免把真实飞行状态和当前 `FlightState` 混用，在 `CompPawnSkin` 中新增独立字段：

- `bool isInVanillaFlight`
- `int vanillaFlightStartTick`
- `int vanillaFlightExpireTick`
- `string vanillaFlightSourceAbilityDefName`
- `string vanillaFlightFollowupAbilityDefName`
- `IntVec3 vanillaFlightReservedTargetCell`
- `bool vanillaFlightHasReservedTargetCell`
- `int vanillaFlightFollowupWindowEndTick`
- `bool vanillaFlightPendingLandingBurst`

### 原则

- 这些字段只用于原版 flyer 体系
- 不与现有 `flightState*` 共用

---

## 新增 Def 层设计

## 1. 新增 PawnFlyer ThingDefs

建议文件：

- `Defs/ThingDefs_Misc/CharacterStudio_PawnFlyers.xml`

定义基础模板，例如：

- `CS_PawnJumper_Default`
- `CS_PawnJumper_FastRise`
- `CS_PawnJumper_DiveStrike`
- `CS_PawnJumper_OffscreenDash`

每个 Def：

- `thingClass` 指向自定义 `CharacterStudioPawnFlyer_*`
- 配置 `<pawnFlyer>`
  - `flightDurationMin`
  - `flightSpeed`
  - `heightFactor`
  - 可选 `progressCurve`

### 设计建议

不要一开始做太多，第一阶段只做：

- `CS_PawnJumper_Default`
- `CS_PawnJumper_OffscreenDive`

---

## 2. 自定义 PawnFlyer 子类

建议新增：

- `CharacterStudioPawnFlyer_Base`
- `CharacterStudioPawnFlyer_OffscreenDive`

### Base

负责：
- 接收起飞 Pawn / 目标 Cell
- 在飞行中记录状态
- 在落地时回调 Utility

### OffscreenDive

用于实现：
- 高速升空
- 飞出屏幕外（表现层）
- 再从高空快速落下

注意：
- “飞出屏幕外”本质是表现需求
- 原版 flyer 不一定天然支持这个视觉，需要在 custom flyer draw / progress / effecter 层处理

---

## 新增 Utility 层

建议新增：

- `AbilityVanillaFlightUtility`
- `AbilityFlightFollowupUtility`

### `AbilityVanillaFlightUtility`
职责：
- 启动原版 flyer
- 绑定 caster / target / ability context
- 记录 `CompPawnSkin` 状态
- 在失败时回滚状态

### `AbilityFlightFollowupUtility`
职责：
- 判断后续技能是否只有飞行中可用
- 在 landing 或 cast 完成时清理状态
- 负责飞行中二段技能窗口控制

---

## 高速升空飞出屏幕外再降落技能：可行性评估

## 目标描述

你提出的技能表现为：

1. 角色高速升空
2. 飞出屏幕外
3. 再高速降落到另一个地图格
4. 落地播放范围伤害特效

## 是否可实现

### 结论：
**可以实现，但推荐拆成“真实飞行 + 表现增强 + 落地爆发”三层。**

### 第一层：真实飞行
使用 `PawnFlyer` 保证进入原版飞行状态。

### 第二层：屏外表现
通过 custom flyer 的：
- progress 曲线
- 高度曲线
- 自定义 draw / overlay / effecter
来表现“快速升空离屏”。

这不一定意味着角色真的从地图边界移动到屏幕外，而是视觉上通过：
- 高度抬升
- alpha / trail / effecter
- 起飞后短时不显示主体
来表现。

### 第三层：落地爆发
落地时：
- 在目标格中心生成 effecter
- 对半径内 Pawn / 建筑应用伤害
- 可选击退

---

## 推荐技能结构

### 技能 1：起飞技能
- 类型：`VanillaPawnFlyer`
- 作用：进入真实飞行状态
- 可选：开放 followup 技能窗口

### 技能 2：空中二段技能（仅飞行中可用）
- 类型：`FlightOnlyFollowup`
- 作用：指定降落点
- 触发：使用 `CS_PawnJumper_OffscreenDive`
- 落地：触发 `FlightLandingBurst`

这样你就能实现：

- 第一下：升空离场
- 第二下：从屏外高速俯冲到目标格
- 落地 AOE

---

## 高稳定性实现建议

## 第一阶段（必须）

先做一个最小稳定闭环：

1. 新增 `VanillaPawnFlyer` runtime component
2. 新增 `CS_PawnJumper_Default` Def
3. 新增 `CharacterStudioPawnFlyer_Base`
4. 施法后真正进入原版 flyer
5. 用 `CompPawnSkin.isInVanillaFlight` 标记飞行中
6. 新增 `FlightOnlyFollowup` 判定组件

这一阶段先不追求“离屏”华丽表现，先保证：

- 进入 flyer
- 落地
- followup 可控
- 状态不乱

## 第二阶段（增强）

再做：

1. `CS_PawnJumper_OffscreenDive`
2. 自定义 flyer 进度曲线 / 高度曲线
3. 飞出屏幕外表现
4. 落地 effecter / aoe / sound

## 第三阶段（复杂能力）

再补：

- HAR 兼容专项测试
- 多段飞行
- 飞行中装备/面部/武器联动
- 飞行中技能栏切换

---

## 稳定性风险与规避

### 风险 1：飞行状态残留
规避：
- 所有 launch / landing / interrupt 路径都统一清理 `isInVanillaFlight`

### 风险 2：followup 技能窗口不同步
规避：
- `FlightOnlyFollowup` 只读取 `CompPawnSkin` 的独立字段
- 不依赖推导状态

### 风险 3：HAR 下渲染异常
规避：
- 第一阶段不要绑定复杂装备/面部动画
- 先只测试是否能稳定进入/退出 flyer

### 风险 4：技能效果和 flyer 生命周期耦合太深
规避：
- 飞行启动逻辑与伤害逻辑分层
- 落地爆发使用独立 `FlightLandingBurst`

---

## 与现有 `FlightState` 的关系

### 保留
`FlightState` 继续作为：
- 自定义演出飞行姿态
- 非原版 flyer 的轻量效果状态

### 新增
真实飞行新增：
- `VanillaPawnFlyer`
- `FlightOnlyFollowup`
- `FlightLandingBurst`

### 原则
禁止把 `FlightState` 硬改成 PawnFlyer，否则：
- 旧数据语义会变化
- 编辑器配置会混乱
- 与现有自定义动画逻辑耦合过深

---

## 最终建议

### 推荐落地路线

1. **新增真实飞行组件，不改现有 FlightState**
2. **先做最小原版 flyer 闭环**
3. **再做“飞行中才能施放”的 followup 技能**
4. **最后补高速离屏 + 高速降落 + AOE 表现**

### 对你的需求结论

你要的：

- 新的真实飞行组件：**可以，应该新增而不是改 FlightState**
- 真实飞行中才能使用的技能：**可以**
- 高速升空离屏、再高速降落到目标格并播放范围伤害特效：**可以实现，建议作为第二阶段表现增强能力**

---

## 建议下一步

下一步应进入计划阶段，拆分为：

1. 新增 runtime component 类型与字段
2. 新增 PawnFlyer Def
3. 新增 launch utility 与 base flyer class
4. 接入技能编辑器 runtime panel
5. 新增 followup-only 判定组件
6. 新增落地爆发组件
7. 最后再加离屏表现型 flyer
