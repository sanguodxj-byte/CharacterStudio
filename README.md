# Character Studio

<p align="center">
  <img src="https://img.shields.io/badge/RimWorld-1.6-blue" alt="RimWorld Version">
  <img src="https://img.shields.io/badge/Dependency-Harmony-orange" alt="Dependency">
  <img src="https://img.shields.io/badge/Type-RimWorld%20Mod-green" alt="Type">
</p>

**Character Studio** 是一个面向 RimWorld 的角色外观编辑模组。它把外观编辑、图层调整、表情预览、装备与技能相关内容整理到一套游戏内工具里，适合想做角色包、外观包，或者想给自己的殖民者做一套完整形象的玩家与作者。

## 这模组能做什么

- **直接在游戏里打开编辑器**：主界面底部有 `Character Studio` 按钮，点开就能进编辑器。
- **可以从殖民者面板快速操作外观**：选中玩家殖民者后，可以直接进入编辑、套用已有外观，或清除当前外观。
- **三栏实时编辑**：左侧切换与管理编辑内容，中间实时预览，右侧调整当前选中项目的参数；改完马上能看到效果。
- **不只改贴图，还能改结构**：除了普通图层，还能隐藏原版头、发、身、服装节点，做覆盖式外观。
- **支持完整面部配置**：表情、眼睛朝向、嘴型、眼睑、眉毛、情绪叠层都能做，并且能在预览里直接演示。
- **还能继续扩展武器、装备、属性、技能内容**：编辑器里有独立页签或菜单处理武器显示、装备定义、属性画像和技能内容。
- **做好后可以直接导出**：能导出成偏外观分享的 **Cosmetic Pack**，也能导出为更完整的角色包，并按需要附带 PawnKind、召唤物物品和技能内容。

## 适合谁

- 想给自己档里的角色做专属外观的玩家
- 想做立绘感角色包、人物包、皮肤包的作者
- 想把角色外观、装备表现、技能表现打成独立模组分享的创作者

## 主要亮点

### 1. 从“捏外观”到“打包分享”是一条完整流程

你不需要先在别处整理大量定义文件再回来导入。Character Studio 本身就带编辑、保存、套用、导出这一整条链路。做完的皮肤可以先存成项目皮肤，后续再继续修改；确认满意后，再通过导出界面打包成可单独加载的模组。

### 2. 不只是换贴图，而是真正的图层式角色编辑

编辑器支持基础外观、图层、面部、属性、武器、装备六个主要页签。你可以给节点挂自定义贴图、调整偏移缩放旋转、改变颜色来源，也可以隐藏原版渲染节点，做出更彻底的角色替换效果。

### 3. 预览不是静态截图，而是实时检查工具

预览区支持旋转、缩放、表情预设、自动播放流程，还能直接测试眼神、嘴型和情绪叠层。想进一步检查节点结构和渲染层级，也可以打开渲染树检查器查看对应信息。

### 4. 导入入口很实用

你可以从当前地图上的 Pawn 开始，也可以从可用种族模板或项目里已保存的皮肤继续做；另外还专门提供了“图层修改工作流”，更适合做节点隐藏和层级修正这类微调工作。

### 5. 导出时就考虑了分享场景

导出界面不只是简单吐文件。它会区分不同导出模式，支持选择是否复制贴图、是否导出 Gene，以及是否附带 PawnKind、召唤物物品和技能内容，同时还会要求确认素材版权，避免把来源不清的资源直接打包出去。

## 你在游戏里大概会这样用

1. 进入游戏后，点击底部主按钮 **Character Studio**。
2. 新建一个皮肤工程，或者从地图 Pawn、种族、项目皮肤导入。
3. 在编辑器里处理基础外观、图层、面部、武器、装备等内容。
4. 用中间预览区实时检查最终效果。
5. 需要的话，直接把当前外观应用到目标 Pawn。
6. 保存到项目皮肤目录，或者导出成一个独立 RimWorld 模组。

## 已确认的功能范围

以下内容都能在当前代码里找到明确实现：

- 主按钮入口与编辑器启动
- 殖民者面板中的外观快捷入口
- 项目皮肤保存/加载（`Config/CharacterStudio/Skins`）
- 从地图 Pawn、种族、项目皮肤导入
- 图层编辑、节点隐藏、基础外观槽位
- 面部表情与预览流程控制
- 武器显示覆写
- 装备数据与导出 XML
- 技能编辑器与热键映射
- 渲染树检查器
- 导出 Cosmetic Pack / Full Unit

## 安装

### 依赖

- Harmony

### 支持版本

- RimWorld **1.6**

### 安装方式

1. 将模组放入 `RimWorld/Mods/`
2. 在启动器中启用 `Character Studio`
3. 确保 Harmony 已启用并排在可正常加载的位置

## 对分享作者比较重要的几点

- 皮肤工程会保存到配置目录，可以作为项目资产反复迭代。
- 皮肤可设置目标种族，也可以配置为某些目标种族的默认皮肤。
- 导出时可以按“纯外观包”或更完整的角色包来组织内容。
- 导出前有素材权利确认步骤，说明这个工具默认面向可发布、可分发的创作场景。

## 给扩展模组作者的接口说明

Character Studio 现在已经开放了几条面向外部模组的稳定入口，适合做联动、运行时扩展或自定义编辑器增强。

### 1. 通过 `CharacterStudioAPI` 读写运行时状态

你可以直接使用 `CharacterStudio.Core.CharacterStudioAPI`：

- 查询 Pawn 是否已被 Character Studio 接管
- 获取当前生效皮肤与技能 Loadout
- 注册或替换运行时 `PawnSkinDef`
- 主动授予 / 撤销技能

### 2. 订阅全局事件

目前已开放这些全局事件：

- `CharacterStudioAPI.SkinChangedGlobal`
- `CharacterStudioAPI.AbilitiesGrantedGlobal`
- `CharacterStudioAPI.AbilitiesRevokedGlobal`
- `CharacterStudioAPI.RuntimeSkinRegisteredGlobal`

适合做“皮肤应用后联动特效”“技能授予后同步自定义状态”“运行时皮肤注册后建立额外索引”这类扩展。

### 3. 为每个图层追加外部动画

现在可以通过 `CharacterStudioAPI.RegisterLayerAnimationProvider(...)` 给每个 `PawnLayerConfig` 追加程序化动画。你返回的是**增量**，会叠加在 Character Studio 自带图层动画之后，而不是替换原逻辑。

如果你只想处理部分图层，推荐使用带 matcher 的重载，让 provider 只在命中的图层上执行，避免每个 provider 对所有图层全量运行。

示例：让名为 `Tail_Main` 的图层持续摆动，让 `Ear_*` 图层在预览和游戏里带一点呼吸感缩放。

```csharp
using CharacterStudio.Core;
using UnityEngine;
using Verse;

[StaticConstructorOnStartup]
public static class MyCharacterStudioBridge
{
    static MyCharacterStudioBridge()
    {
        CharacterStudioAPI.RegisterLayerAnimationProvider(
            "my-mod.layer-anim",
            context =>
                context.layer != null
                && ((context.layer.layerName ?? string.Empty) == "Tail_Main"
                    || (context.layer.layerName ?? string.Empty).StartsWith("Ear_")),
            AnimateLayer);
    }

    private static CharacterStudioLayerAnimationResult AnimateLayer(CharacterStudioLayerAnimationContext context)
    {
        if (context.layer == null || context.pawn == null)
        {
            return CharacterStudioLayerAnimationResult.Identity();
        }

        string layerName = context.layer.layerName ?? string.Empty;
        float time = context.currentTick / 60f;

        if (layerName == "Tail_Main")
        {
            float angle = Mathf.Sin(time * 2.2f) * 9f;
            Vector3 offset = new Vector3(Mathf.Sin(time * 2.2f) * 0.01f, 0f, 0f);
            return new CharacterStudioLayerAnimationResult(angle, offset, Vector3.one);
        }

        if (layerName.StartsWith("Ear_"))
        {
            float pulse = 1f + Mathf.Sin(time * 1.4f) * 0.03f;
            return new CharacterStudioLayerAnimationResult(0f, Vector3.zero, new Vector3(pulse, 1f, pulse));
        }

        return CharacterStudioLayerAnimationResult.Identity();
    }
}
```

`CharacterStudioLayerAnimationContext` 里当前会提供这些信息：

- `pawn`：当前正在渲染的 Pawn
- `layer`：当前图层配置（`PawnLayerConfig`）
- `facing`：当前朝向
- `currentTick`：当前渲染 Tick
- `isPreview`：是否处于非 Playing 的预览环境

因此你可以按 `layer.layerName`、`layer.role`、`anchorTag`、`pawn.Faction` 或自定义组件状态来决定是否追加动画。

## 一句话总结

如果你想要的是“在 RimWorld 里直接把角色外观做出来，还能顺手打包成能分享的模组”，那 Character Studio 做的正是这件事。
