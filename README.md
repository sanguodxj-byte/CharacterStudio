# CharacterStudio

<p align="center">
  <img src="https://img.shields.io/badge/RimWorld-1.5%20%7C%201.6-blue" alt="RimWorld Version">
  <img src="https://img.shields.io/badge/License-MIT-green" alt="License">
  <img src="https://img.shields.io/badge/Language-C%23-purple" alt="Language">
</p>

**CharacterStudio** 是一个强大的 RimWorld 模组开发框架，提供角色皮肤编辑器、渲染树可视化、自定义图层系统等功能，帮助模组作者创建复杂的角色外观自定义系统。

## ✨ 主要功能

### 🎨 皮肤编辑器 (Skin Editor)
- **可视化编辑界面**：直观的 UI 设计，支持实时预览
- **图层管理**：添加、删除、重排自定义渲染图层
- **隐藏节点控制**：通过路径或标签隐藏原版渲染节点
- **导入/导出**：支持从现有 Pawn 导入外观设置

### 🌳 渲染树检查器 (Render Tree Inspector)
- **树形结构可视化**：完整展示 Pawn 的渲染节点层级
- **节点详情查看**：显示每个节点的类型、路径、标签等信息
- **调试支持**：快速定位渲染问题

### 🖼️ 自定义图层系统
- **运行时资源加载**：从外部路径加载 PNG/JPG 图像
- **多种混合模式**：支持不同的图层叠加效果
- **面部组件支持**：专门针对面部的自定义渲染节点

### 📦 模组导出器 (Mod Exporter)
- **一键导出**：将皮肤配置导出为独立模组
- **自动生成 Defs**：自动创建必要的 XML 定义文件
- **资源打包**：自动复制和组织纹理资源

## 🏗️ 技术架构

### 核心组件

```
CharacterStudio/
├── Core/                    # 核心定义和数据结构
│   ├── PawnSkinDef.cs       # 皮肤定义类
│   ├── PawnLayerConfig.cs   # 图层配置
│   ├── PawnFaceConfig.cs    # 面部配置
│   └── CompPawnSkin.cs      # 皮肤组件
├── Rendering/               # 渲染系统
│   ├── Patch_PawnRenderTree.cs        # 渲染树 Harmony 补丁
│   ├── PawnRenderNode_Custom.cs       # 自定义渲染节点
│   ├── PawnRenderNodeWorker_*.cs      # 节点工作器
│   ├── RuntimeAssetLoader.cs          # 运行时资源加载
│   └── Graphic_Runtime.cs             # 运行时图形类
├── Introspection/           # 渲染树解析
│   ├── RenderTreeParser.cs            # 渲染树解析器
│   └── RenderNodeSnapshot.cs          # 节点快照
├── UI/                      # 用户界面
│   ├── Dialog_SkinEditor.cs           # 皮肤编辑器窗口
│   ├── Dialog_RenderTreeInspector.cs  # 渲染树检查器
│   ├── Dialog_ExportMod.cs            # 模组导出对话框
│   ├── MannequinManager.cs            # 预览人偶管理
│   └── UIHelper.cs                    # UI 辅助工具
└── Exporter/                # 导出系统
    └── ModBuilder.cs                  # 模组构建器
```

### 关键技术实现

#### 1. 渲染节点隐藏机制

通过 Harmony 补丁拦截 `PawnRenderNode.GraphicFor()` 方法：

```csharp
// Patch_PawnRenderTree.cs
[HarmonyPrefix]
public static bool GraphicFor_Prefix(PawnRenderNode __instance, ref Graphic __result)
{
    if (hiddenNodes.Contains(__instance))
    {
        __result = Graphic_Empty.Instance;
        return false; // 跳过原方法
    }
    return true;
}
```

**重要**：由于 `PawnRenderNode_Head`、`PawnRenderNode_Body`、`PawnRenderNode_Hair` 等派生类完全重写了 `GraphicFor()` 方法，必须对每个派生类单独应用补丁。

#### 2. 运行时资源加载

支持从外部路径加载纹理：

```csharp
// RuntimeAssetLoader.cs
public static Texture2D LoadTextureFromFile(string filePath)
{
    byte[] data = File.ReadAllBytes(filePath);
    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
    tex.LoadImage(data);
    tex.filterMode = FilterMode.Point;
    return tex;
}
```

#### 3. 渲染树解析

递归遍历 `PawnRenderTree` 生成节点快照：

```csharp
// RenderTreeParser.cs
public static List<RenderNodeSnapshot> ParseTree(Pawn pawn)
{
    var tree = pawn.Drawer?.renderer?.renderTree;
    if (tree?.rootNode == null) return new List<RenderNodeSnapshot>();
    return ParseNodeRecursive(tree.rootNode, "", 0);
}
```

#### 4. 预览人偶系统

`MannequinManager` 创建独立的预览 Pawn，应用皮肤后刷新隐藏节点：

```csharp
// MannequinManager.cs
public void ApplySkin(PawnSkinDef skin)
{
    currentSkin = skin;
    Patch_PawnRenderTree.RefreshHiddenNodes(previewPawn, skin);
    previewPawn.Drawer?.renderer?.SetDirty();
}
```

### 数据结构

#### PawnSkinDef
```csharp
public class PawnSkinDef : Def
{
    public List<PawnLayerConfig> layers;      // 自定义图层列表
    public List<string> hiddenPaths;          // 按路径隐藏的节点
    public List<string> hiddenTags;           // 按标签隐藏的节点
    public PawnFaceConfig faceConfig;         // 面部配置
}
```

#### PawnLayerConfig
```csharp
public class PawnLayerConfig
{
    public string texturePath;                // 纹理路径
    public string parentNodePath;             // 父节点路径
    public Vector2 offset;                    // 偏移量
    public float scale = 1f;                  // 缩放比例
    public Color color = Color.white;         // 颜色
    public int drawOrder;                     // 绘制顺序
}
```

## 📥 安装

1. 下载最新 Release 或克隆仓库
2. 将 `CharacterStudio` 文件夹复制到 `RimWorld/Mods/` 目录
3. 在游戏中启用模组

## 🔧 开发

### 环境要求
- .NET Framework 4.7.2
- RimWorld 1.5 或 1.6
- 0Harmony 库

### 编译

使用 PowerShell 脚本：
```powershell
.\deploy.ps1
```

或使用 .NET CLI：
```bash
cd Source/CharacterStudio
dotnet build -c Release
```

### 部署

脚本会自动将编译后的 DLL 和资源文件复制到 RimWorld Mods 目录。

## 📖 使用指南

### 创建自定义皮肤

1. 在游戏中选择一个殖民者
2. 打开 CharacterStudio 皮肤编辑器
3. 使用"导入"功能获取当前外观
4. 添加自定义图层或隐藏原版节点
5. 点击"导出为模组"生成独立模组

### 调试渲染问题

1. 打开渲染树检查器
2. 查看完整的节点层级结构
3. 使用眼睛图标切换节点可见性
4. 检查节点路径和标签

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

MIT License

## 🙏 致谢

- RimWorld 开发团队
- Harmony 库作者
- 所有贡献者和测试者