## TODO LIST

<!-- LIMCODE_TODO_LIST_START -->
- [ ] 阶段一 review：验证数据结构、编辑器配置和旧 FlightState 语义隔离  `#phase-1-review`
- [ ] 阶段一：新增真实飞行 runtime component 类型、配置字段与编辑器面板支持  `#phase-1-runtime-schema`
- [ ] 阶段二：新增 PawnFlyer Def、启动 Utility、基础 CharacterStudioPawnFlyer，实现最小原版飞行闭环  `#phase-2-flyer-core`
- [ ] 阶段二 review：验证角色是否真正进入原版 PawnFlyer 状态并正确落地/清理  `#phase-2-review`
- [ ] 阶段三：新增仅真实飞行中可用的 followup 组件与技能可用性判定  `#phase-3-followup-skill`
- [ ] 阶段三 review：验证 followup 技能窗口、状态约束和异常回滚  `#phase-3-review`
- [ ] 阶段四：新增落地范围伤害/特效组件，完成真实飞行落地爆发技能  `#phase-4-landing-burst`
- [ ] 阶段四 review：验证落地 AOE、特效、声音与目标筛选稳定性  `#phase-4-review`
- [ ] 阶段五：实现高速升空离屏/高速降落到目标格的表现增强 flyer  `#phase-5-offscreen-dive`
- [ ] 阶段五 review：验证屏外转场表现、时序、HAR 兼容与整体稳定性  `#phase-5-review`
- [ ] 阶段六：补充容错、清理状态残留、添加必要日志与回归检查点  `#phase-6-hardening`
- [ ] 阶段六 review：进行最终回归审查，确认功能完备后结束  `#phase-6-review`
<!-- LIMCODE_TODO_LIST_END -->

# CharacterStudio 真实 PawnFlyer 组件实施计划

## 约束与目标

- **不修改现有 `FlightState` 语义**，其继续作为 CharacterStudio 自定义飞行姿态/演出状态存在。
- 新增一套**真实原版 PawnFlyer 链路**，确保 Pawn 真正进入 RimWorld 1.6 的 `PawnFlyer` 体系。
- 最终支持一个**仅真实飞行中可用**的技能：高速升空、离屏、再高速降落到目标格，并触发落地范围伤害/特效。
- 每一阶段完成后均进行一次 review，再进入下一阶段。

---

## 阶段一：Runtime Schema 与编辑器配置接入

### 目标
新增与真实飞行相关的 runtime component 类型与配置字段，并接入技能编辑器，使数据层与 UI 层具备配置能力。

### 计划内容
1. 在 `AbilityRuntimeComponentType` 中新增：
   - `VanillaPawnFlyer`
   - `FlightOnlyFollowup`
   - `FlightLandingBurst`
2. 在 `AbilityRuntimeComponentConfig` 中新增字段：
   - `flyerThingDefName`
   - `flyerWarmupTicks`
   - `launchFromCasterPosition`
   - `requireValidTargetCell`
   - `storeTargetForFollowup`
   - `enableFlightOnlyWindow`
   - `flightOnlyWindowTicks`
   - `flightOnlyAbilityDefName`
   - `hideCasterDuringTakeoff`
   - `autoExpireFlightMarkerOnLanding`
   - `requiredFlightSourceAbilityDefName`
   - `requireReservedTargetCell`
   - `consumeFlightStateOnCast`
   - `onlyUseDuringFlightWindow`
   - `landingBurstRadius`
   - `landingBurstDamage`
   - `landingBurstDamageDef`
   - `landingEffecterDefName`
   - `landingSoundDefName`
   - `affectBuildings`
   - `affectCells`
   - `knockbackTargets`
   - `knockbackDistance`
3. 补充 `NormalizeForSave()` 与 `Validate()`。
4. 在 `Dialog_AbilityEditor` runtime panel 中加入上述组件的基础编辑 UI。
5. 保证旧配置与 `FlightState` 完全不受影响。

### 阶段一 Review 标准
- 新组件可在编辑器中创建、保存、再次载入。
- 旧技能配置不报错，旧 `FlightState` 行为未改变。
- 新字段校验合理，不会写出非法配置。

---

## 阶段二：真实 PawnFlyer 最小闭环

### 目标
让技能可真正把 Pawn 送入原版 `PawnFlyer` 状态，并在飞行结束时安全落地。

### 计划内容
1. 在 `CompPawnSkin` 中新增真实飞行独立状态字段：
   - `isInVanillaFlight`
   - `vanillaFlightStartTick`
   - `vanillaFlightExpireTick`
   - `vanillaFlightSourceAbilityDefName`
   - `vanillaFlightFollowupAbilityDefName`
   - `vanillaFlightReservedTargetCell`
   - `vanillaFlightHasReservedTargetCell`
   - `vanillaFlightFollowupWindowEndTick`
   - `vanillaFlightPendingLandingBurst`
2. 新增 `Defs/ThingDefs_Misc/CharacterStudio_PawnFlyers.xml`：
   - `CS_PawnJumper_Default`
3. 新增 C# 类：
   - `CharacterStudioPawnFlyer_Base`
   - `AbilityVanillaFlightUtility`
4. 在 `CompAbilityEffect_Modular` 中接入 `VanillaPawnFlyer` 组件执行逻辑：
   - 解析 target / cell
   - 生成/启动原版 flyer
   - 写入 `CompPawnSkin` 状态
5. 实现 landing cleanup：
   - 落地清理飞行标记
   - 中断/失败路径也清理

### 阶段二 Review 标准
- 使用技能后，Pawn 真正进入原版 `PawnFlyer` 体系。
- 能通过 `SpawnedParentOrMe is PawnFlyer` 之类语义被识别。
- 落地后状态无残留。
- 基础 HAR / 自定义种族不会立即崩溃或卡死。

---

## 阶段三：飞行中限定后续技能

### 目标
实现一个只有在真实飞行状态中才可用的 followup 技能窗口。

### 计划内容
1. 新增 `FlightOnlyFollowup` 组件执行与判定逻辑。
2. 新增 `AbilityFlightFollowupUtility`：
   - 判断是否处于真实飞行中
   - 判断来源技能是否匹配
   - 判断是否仍在 followup window 内
3. 将 followup 技能的可见性/可用性接入：
   - 技能栏
   - 热键链路
   - 施法前校验
4. 允许起飞技能配置：
   - followup ability def
   - followup 窗口时长
   - 是否消耗飞行状态

### 阶段三 Review 标准
- 非飞行时 followup 技能不可用。
- 飞行中符合窗口时可用。
- 飞行结束/中断后自动失效。
- 技能链切换稳定，不影响普通技能。

---

## 阶段四：落地范围伤害与特效

### 目标
在真实飞行技能落地时触发范围伤害、特效、声音与可选击退。

### 计划内容
1. 新增 `FlightLandingBurst` 组件逻辑。
2. 在 landing callback 中：
   - 读取落地点
   - 构建范围目标集合
   - 应用伤害
   - 播放 effecter / sound
   - 可选击退
3. 目标筛选需支持：
   - 仅 Pawn
   - 可选建筑
   - 同阵营过滤
4. 接入现有伤害 / VFX 工具层，避免重复造轮子。

### 阶段四 Review 标准
- 落地时稳定触发 AOE。
- 伤害与视觉特效时序正确。
- 不会重复多次触发。
- 目标筛选符合配置预期。

---

## 阶段五：离屏升空与高速降落表现增强

### 目标
实现你提出的“高速升空飞出屏幕外，再高速降落至目标格”的高级表现。

### 计划内容
1. 新增 `CS_PawnJumper_OffscreenDive` Def。
2. 新增 `CharacterStudioPawnFlyer_OffscreenDive`：
   - 自定义 progress / height / draw 表现
   - 让起飞阶段快速上升并在视觉上离屏
   - 终段高速俯冲至目标格
3. 必要时增加：
   - 起飞残影 effecter
   - 离屏期间主体隐藏/极高高度处理
   - 降落轨迹 effecter
4. 与 `FlightOnlyFollowup` 联动，实现：
   - 起飞技能后可在飞行中施放“降落打击”技能

### 阶段五 Review 标准
- 离屏表现明确、时序连贯。
- 降落位置准确。
- 落地爆发仍稳定触发。
- 不因特殊视角/HAR 附件导致明显异常。

---

## 阶段六：加固与最终回归

### 目标
补足稳定性、容错和调试能力，完成最终可交付版本。

### 计划内容
1. 统一处理以下路径的状态清理：
   - 起飞失败
   - 目标非法
   - 落地成功
   - 技能取消
   - Pawn 死亡/离图
2. 增加必要日志：
   - flyer launch
   - landing callback
   - followup window open/close
   - state cleanup
3. 进行回归检查：
   - 普通技能不受影响
   - 旧 `FlightState` 不受影响
   - 技能编辑器保存/加载正常
   - 真实飞行技能链稳定
4. 如有必要，增加小型调试开关或 dev-only 提示。

### 阶段六 Review 标准
- 状态清理完整，无明显残留。
- 旧功能无回归。
- 真实飞行技能可以稳定重复使用。
- 方案达成“完备后停止”。

---

## 推荐实施顺序

1. 阶段一：先把 schema 和编辑器铺平。
2. 阶段二：做最小原版 flyer 闭环。
3. 阶段三：做飞行中限定后续技能。
4. 阶段四：落地爆发。
5. 阶段五：离屏升空/高速降落表现。
6. 阶段六：加固与最终回归。

---

## 关键实现原则

- **不要修改现有 `FlightState` 语义**。
- **真实飞行与自定义飞行姿态并存**。
- **飞行启动、飞行状态、落地伤害、后续技能窗口分层实现**。
- **每阶段做完必须 review，再进入下一阶段。**
