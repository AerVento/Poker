---
name: poker-assets
description: 在 Poker Unity 项目中决定新文件放在哪里、如何命名，以及如何安全地移动、重命名、删除资源并保留 GUID 引用。用于新增美术、音频、prefab、SO、脚本、文档，或整理、清理、迁移任何文件的请求。
---

# Poker 资源放置与清理

先读根目录 `AGENTS.md` 的目录地图和目标目录的 `AGENTS.md`。UI 资源另按 [UI 规范](../../../Docs/Standards/UIConvention.md) 第 1 节归档。

## 放在哪里

| 内容 | 位置 |
| --- | --- |
| 运行时脚本 | `Assets/Scripts/<Game/Network/UI/Framework>/<功能>/` |
| 长期使用的 Editor 工具 | 所属功能目录的 `Editor/` 子目录 |
| 测试 | 只放 `Assets/Tests/Editor/`（临时目录，会被清空） |
| 面板 prefab | `Assets/Prefabs/UI/Panels/<面板>/`；跨功能控件放 `Assets/Prefabs/UI/Shared/` |
| 礼物 prefab | `Assets/Prefabs/Gifts/` |
| UI 主题背景、边框、导航图标 | `Assets/Sprites/UITheme/` |
| 其他 UI 图标 | `Assets/Sprites/UI/` |
| 名片、牌面、物品、排行榜图片 | `Assets/Sprites/Namecards/`、`CardFaces/<主题>/`、`Items/<分类>/`、`Leaderboard/` |
| 礼物图片 | `Assets/Art/Gifts/` |
| 成就图标（按路径加载） | `Assets/Resources/UI/Achievements/` |
| 音乐与音效 | `Assets/Audio/Music/`、`Assets/Audio/SFX/<分类>/` |
| 运行时 SO（音频库、本地默认外观） | `Assets/Resources/SO/` 根目录；物品与商品定义在 `Configs/Items/*.json`、`Configs/Shop/Shop.json` |
| 远程资源入口 | `Assets/RemoteContent/<namecards/cardfaces/gifts>/`、`icons/Items.asset`、`previews/Cardfaces.asset`；物品图标在 `Assets/Sprites/Items/<分类>/`（文件名即配置中的图标名） |
| 礼物 Lua 脚本 | `Assets/Scripts/UI/Gifts/Lua/<礼物>.lua.txt`，共享模块在 `Shared/`（进入共享 Lua 包，见 [Lua 规范](../../../Docs/Standards/LuaConvention.md)） |
| 生成原图、提示词、导出脚本、原始音频 | 仓库根目录 `ArtSources/`、`AudioSources/`，不进 `Assets` |
| 设计、规范、说明文档 | `Docs/<Systems/Operations/Standards/Proposals>/`，规则见 `Docs/AGENTS.md`；测试报告与截图放 `Logs/` |
| 部署配置 | `Configs/` |

- `Resources` 目录下的所有内容都会打进游戏包：只放运行时确实要按路径加载的资源，不放文档、源图或示例。
- 不在 `Assets` 根目录或任意目录散放文件；不要另建 `New`、`Temp`、`Old`、`v2` 之类的平行目录。
- 第三方包导入后只保留实际使用的部分。只用到其中一两张图时，把图（连同 `.meta`）移到项目自己的目录，再删除整个包。

## 命名

按文件性质决定写法，不按所在目录：

| 类型 | 写法 | 例子 |
| --- | --- | --- |
| 导入的原始美术与音频：`png`、`jpg`、`jpeg`、`wav`、`ogg`、`mp3`、`ttf`、`ttc` | 小写短横线 | `champagne-bottle.png`、`abyss-wide.png`、`club-10.png`、`msyh.ttc` |
| 音频 | 小写短横线，保留类型前缀 `bgm-`、`sfx-`、`gift-` | `bgm-game.ogg`、`sfx-all-in.wav`、`gift-egg-splat.wav` |
| 脚本、prefab、SO、场景、材质、TMP 字体资产等 Unity 序列化资产 | 大驼峰 | `GamePanel.prefab`、`ShopLocalArt.asset`、`RoyalStraightFlush.asset`、`MsyhSdf.asset` |
| 目录 | 保持大驼峰，不跟随文件规则 | `Sprites/UITheme/`、`Sprites/CardFaces/JewelCourt/` |

- 以商品 ID 命名的 SO 按 ID 转成大驼峰：`jewel-court` → `JewelCourt.asset`。商品 ID、物品 ID 和 Addressables 地址本身不改（如 `cardfaces/jewel-court`），远程数据文件路径统一用 `RemoteAssets.SourcePath(address)` 换算。
- 文件名不带版本号。`Assets` 里只放当前在用的版本；旧版、迭代稿和生成原图放到 `ArtSources/` 对应目录，那里可以保留 `-vN`。替换美术时直接覆盖原文件（保留 `.meta` 和 GUID），不要新建 `-v2` 文件。
- 图集中切出的子图名称不受文件规则约束，已有子图名不要改，代码可能按名称查找。
- 不适用的范围：第三方目录（`Mirror`、`Plugins`、`TextMesh Pro`、`Packages`、`NuGet`）、`Assets/WebGLTemplates`（网页按文件名引用）、Addressables 工具生成的分组文件、`Configs/` 下代码按固定名称读取的 JSON。`Docs/` 的文档用英文大驼峰、不带日期或版本号（见 `Docs/AGENTS.md`）。
- 改名后同步修改按名称或路径引用它的代码与脚本：`Resources.Load` 路径（如成就 `IconPath`）、`AssetDatabase.LoadAssetAtPath` 路径、`Tools/` 和 `ArtSources/` 中的生成与导出脚本、文档中的路径。
- 仅改大小写的重命名要用 `git mv` 记录，否则 Windows 上的 git 不会识别这次改名。

## 移动与重命名

- 优先在 Unity Project 窗口中操作。用文件系统操作时，资源和它的 `.meta` 必须在同一步一起移动或改名，GUID 才会保留，prefab、场景和 SO 的引用才不会断。
- 移动后搜索并更新按字符串引用的路径：`Resources.Load` 路径常量（如各 SO 的 `ResourcePath`）、`AssetDatabase.LoadAssetAtPath` 中的路径、Addressables 地址，以及 `Docs/`、Skill 和 `AGENTS.md` 中的路径。
- 移动脚本不改命名空间和类型名。
- 编辑现有 C# 文件时保留原编码（少数旧文件是 GBK）和 CRLF。

## 删除

1. 用 GUID 确认没有引用：取 `.meta` 中的 `guid`，在 `Assets/`、`ProjectSettings/`、`Packages/` 的 `.unity`、`.prefab`、`.asset`、`.mat`、`.controller`、`.json` 等 YAML/文本文件中搜索。
2. 再确认没有按路径或名称加载：搜索 `Resources.Load`、`AssetDatabase.LoadAssetAtPath`、Addressables 地址和资源文件名。
3. 资源与 `.meta` 一起删除；目录删空后连同目录的 `.meta` 删除。
4. 删除场景时同步移除 `ProjectSettings/EditorBuildSettings.asset` 中的条目。
5. `Assets/Scripts/Framework` 中的代码即使无人使用也不删除。
6. 删除后等待 Unity 重新导入，在 `Game.unity` 中检查 Missing Script、Missing Reference 和 Console error。
