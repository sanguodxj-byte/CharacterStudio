# Special Characters - 特殊角色模组

## 简介 / Introduction

这是一个环世界（RimWorld）模组，添加独特的可招募特殊角色到游戏中。每个角色都有独特的背景故事、能力和外观。

This is a RimWorld mod that adds unique recruitable special characters to the game. Each character has unique backstory, abilities, and appearance.

## 功能特点 / Features

- 🎭 **独特角色** - 精心设计的特殊角色，每人都有独特的个性
- 📖 **背景故事** - 深度的背景故事与角色设定
- ⚔️ **专属能力** - 角色特有的技能和能力
- 🎨 **高质量贴图** - 定制的角色立绘和图标
- 🔧 **易于扩展** - 结构化的设计便于添加新角色

## 依赖 / Requirements

- RimWorld 1.5 或 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

## 安装 / Installation

1. 确保已安装 Harmony
2. 将 `SpecialCharacters` 文件夹放入 RimWorld 的 `Mods` 目录
3. 在游戏模组列表中启用此模组

### 模组加载顺序

此模组应加载在以下模组之后：
- Harmony
- 核心游戏 (Core)
- DLC 扩展（如已安装）

## 文件夹结构 / Folder Structure

```
SpecialCharacters/
├── About/
│   ├── About.xml          # 模组元数据
│   └── Preview.png        # 模组预览图（可选）
├── Defs/
│   ├── PawnKindDefs/      # 角色类型定义
│   ├── ThingDef/          # 角色实体定义
│   ├── BackstoryDefs/     # 背景故事定义
│   ├── TraitDefs/         # 特质定义
│   └── AbilityDefs/       # 能力定义
├── Languages/
│   ├── ChineseSimplified/ # 简体中文
│   │   └── Keyed/         # 翻译字符串
│   └── English/           # 英文
│       └── Keyed/         # 翻译字符串
├── Textures/
│   └── Characters/        # 角色贴图
└── README.md              # 本文档
```

## 创建新角色 / Creating New Characters

### 步骤 1: 定义角色实体

在 `Defs/ThingDef/` 中创建角色定义文件：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
    <ThingDef ParentName="BasePawn">
        <defName>SpecialCharacter_Example</defName>
        <label>Example Character</label>
        <!-- 更多属性... -->
    </ThingDef>
</Defs>
```

### 步骤 2: 定义角色类型

在 `Defs/PawnKindDefs/` 中创建角色类型：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
    <PawnKindDef ParentName="BaseColonist">
        <defName>SpecialCharacterKind_Example</defName>
        <label>example character</label>
        <!-- 更多属性... -->
    </PawnKindDef>
</Defs>
```

### 步骤 3: 添加背景故事

在 `Defs/BackstoryDefs/` 中创建背景故事：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
    <BackstoryDef>
        <defName>ExampleBackstory_Child</defName>
        <slot>Childhood</slot>
        <title>Example Child</title>
        <titleShort>Example</titleShort>
        <baseDescription>[PAWN_nameDef] grew up...</baseDescription>
        <!-- 技能影响... -->
    </BackstoryDef>
</Defs>
```

### 步骤 4: 添加翻译

在 `Languages/ChineseSimplified/Keyed/` 和 `Languages/English/Keyed/` 中添加翻译文件。

## 角色设计指南 / Character Design Guidelines

### 背景故事设计

1. **童年背景** - 影响基础技能和性格形成
2. **成年背景** - 决定专业技能和生活经历
3. **特质组合** - 合理搭配正面和负面特质

### 能力平衡

- 每个角色应有明显的专长领域
- 避免过于强大的"完美角色"
- 考虑角色在殖民地的定位和作用

### 视觉设计

- 贴图尺寸建议：128x128 或 256x256
- 使用 PNG 格式，支持透明背景
- 保持与原版画风一致

## 版本历史 / Changelog

### v1.0.0
- 初始版本
- 添加基础框架结构

## 许可证 / License

MIT License - 可自由使用、修改和分发

## 贡献 / Contributing

欢迎提交 Issue 和 Pull Request！

## 联系方式 / Contact

- GitHub: [Your Repository Link]
- Steam Workshop: [Your Workshop Link]