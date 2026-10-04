# AGENTS.md

本文件适用于整个仓库。修改任何文件前，先阅读本文件以及目标目录下可能存在的更深层 `AGENTS.md`。本文件写全局约束和目录地图；各目录的细则写在该目录的 `AGENTS.md`；反复执行的操作流程写成 Skill。

## 项目级 Skills

- 新增或调整成就：[poker-achievements](.agents/skills/poker-achievements/SKILL.md)。
- 新增、替换、上下架名片或牌面：[poker-cosmetics](.agents/skills/poker-cosmetics/SKILL.md)。
- 新增或调整礼物：[poker-gifts](.agents/skills/poker-gifts/SKILL.md)。
- 改物品或商城配置（上下架、改价、新增物品／礼物／名片／牌面／契约）或发布远程内容、新页面：[poker-dynamic-content](.agents/skills/poker-dynamic-content/SKILL.md)。
- 新增或修改网络消息：[poker-network-messages](.agents/skills/poker-network-messages/SKILL.md)。
- 新增、移动、重命名或清理任何资源与文件：[poker-assets](.agents/skills/poker-assets/SKILL.md)。

Skill 中的具体需求以当前用户要求为准。

## UI 工作强制规范

- 编写或修改任何随远程内容更新的 Lua（礼物入口与共享模块）前，阅读 [Lua 规范](Docs/Standards/LuaConvention.md)：共享逻辑放进共享 Lua 包，修改后按规则升版本号。
- 凡涉及界面设计、布局、UI 文案、图标、按钮、交互、UI 脚本、prefab、UI 美术资源或生成／修改 UI 的 Editor 工具，开始设计或修改前必须完整阅读并遵守 [项目 UI 规范](Docs/Standards/UIConvention.md)。不能只在修改 prefab 时才阅读。
- UI 规范是本项目界面工作的验收要求，不是可选建议。必须按功能目录归档资源，沿用统一美术风格和圆角组件，复用统一图标按钮，保持玩家文案简洁，并完成规范中的视觉与交互检查。
- 禁止以实现方便为理由用文字按钮替代已有约定图标，或用冗长说明文字补救不直观的交互。名称、介绍、价格、规则、重置时间等必要信息必须保留。
- 旧界面或历史实施记录不构成违反规范的先例。新增与本次修改的 UI 必须符合规范；用户明确要求改变规范时同步更新文档。完成前按规范逐项自查，并如实说明未验证项。

## 项目概览

- 这是一个基于 Unity 的多人德州扑克项目，产品名为 `Poker`。
- Unity 编辑器版本固定为 `2022.3.52f1c1`；除非任务明确要求，不要升级编辑器、包版本或序列化格式。
- 渲染管线为 URP 14，输入系统为 Unity Input System。
- 联机层使用内嵌的 Mirror `93.0.0`；异步代码使用内嵌的 UniTask `2.5.10`。
- 客户端界面使用 uGUI、TextMeshPro。网络协议和存储使用 `System.Text.Json`（NuGet DLL 位于 `Assets/Packages`）；`Framework` 的配置读写使用 UPM 包提供的 Newtonsoft.Json，不要再另装一份。
- 服务端账户数据存储在 SQLite（`Microsoft.Data.Sqlite`，NuGetForUnity 安装）。
- 名片、非经典牌面、礼物特效、物品图标与牌面预览是 Addressables 远程内容，按需下载并在页面内更新，见 [远程资源](Docs/Operations/RemoteArtwork.md)。物品以 `Configs/Items/`（物品表与名片、牌面、礼物、契约等功能目录）、商城以 `Configs/Shop/Shop.json`、新账号初始发放以 `Configs/Accounts/StarterGrants.json` 为唯一来源，服务器启动时统一校验并把目录下发给客户端，见 [货币、商店与物品](Docs/Systems/Economy.md)。
- 礼物特效由 Lua 编排：xLua v2.1.16（`Assets/XLua/`，Lua 5.3；WebGL 时 Lua C 源码随页面以 C 语言编译）执行，DOTween 1.3.030（`Assets/Plugins/Demigiant/DOTween/`，官网免费版）驱动，见 [礼物](Docs/Systems/Gifts.md)。xLua 生成代码在 `Assets/XLua/Gen/`，修改礼物宿主接口后执行 `XLua/Generate Code`。
- 当前自有代码未拆分 asmdef：运行时代码编译进 `Assembly-CSharp`，`Editor` 目录下的代码编译进 `Assembly-CSharp-Editor`。

## 目录地图

脚本目录（进入前先读该目录的 `AGENTS.md`）：

- `Assets/Scripts/Game`：客户端启动、牌型规则、经济、成就、任务、音频等不依赖网络的逻辑，见 [Game/AGENTS.md](Assets/Scripts/Game/AGENTS.md)。
- `Assets/Scripts/Network`：Mirror 入口、房间、牌局、协议、账户存储和各服务端功能，见 [Network/AGENTS.md](Assets/Scripts/Network/AGENTS.md)。
- `Assets/Scripts/UI`：按面板分目录的界面脚本，见 [UI/AGENTS.md](Assets/Scripts/UI/AGENTS.md)。
- `Assets/Scripts/Framework`：与本游戏无关的通用设施，见 [Framework/AGENTS.md](Assets/Scripts/Framework/AGENTS.md)。
- `Assets/Scripts/SO`：ScriptableObject 类型定义。

资源目录：

- `Assets/Resources/SO`：运行时按路径加载的 SO 数据（`GameAudioLibrary`、本地默认外观 `ShopLocalArt`）。物品与商品不用 SO 定义，见 `Configs/Items/`、`Configs/Shop/`；SO 只承载需要引用 Unity 资源的美术与特效。`Resources` 下只放运行时要加载的资源，不放文档。
- `Assets/Resources/UI/Achievements`：成就图标，按 `IconPath` 路径加载。
- `Assets/RemoteContent`：Addressables 远程资源入口（名片、牌面、礼物包、共享 Lua 包、物品图标集、牌面预览集）与包版本锁文件 `packages-lock.json`。礼物 Lua 源码在 `Assets/Scripts/UI/Gifts/Lua/`，共享模块在其 `Shared/`。
- `Assets/Prefabs`：`Manager/` 为入口管理器 prefab，`UI/` 为 Canvas、面板与共享控件，`Gifts/` 为礼物特效和礼物菜单。
- `Assets/Sprites`、`Assets/Art`、`Assets/Audio`、`Assets/Font`：导入后的美术、音频和字体。生成源图、原始音频、导出脚本放在仓库根目录的 `ArtSources/`、`AudioSources/`，不要放进 `Assets`。
- `Assets/Scenes/Game.unity`：唯一启用的构建场景。`GiftPreview.unity` 是礼物预览工具场景。
- `Assets/Tests`：唯一允许放测试的目录，规则见下文“测试代码”。

仓库根目录：

- `Configs/`：部署时读取的网络、商城和每日任务配置，见 [配置目录](Docs/Operations/Deployment.md)。
- `Docs/`：当前行为、配置与规范的说明，按 `Systems/`、`Operations/`、`Standards/`、`Proposals/` 分类，索引见 [Docs/README.md](Docs/README.md)，写作规则见 [Docs/AGENTS.md](Docs/AGENTS.md)。修改代码改变了文档描述的行为时，同一任务内更新文档。
- `ArtSources/`、`AudioSources/`：美术与音频的生成原图、提示词、旧版素材和导出脚本。`Tools/`：音频、发布和远程资源的辅助脚本。
- `Assets/Mirror`、`Assets/Plugins/UniTask`、`Assets/Plugins/Demigiant`、`Assets/XLua`（`Gen/` 除外）、`Assets/TextMesh Pro`、`Assets/Packages`、`Assets/NuGet`：第三方或导入资源。除非任务明确针对依赖本身，否则不要修改。`Assets/XLua/Plugins/WebGL/xlua_webgl.c` 是本项目替换上游 `xlua_webgl.cpp` 的编译入口。
- `Library`、`Temp`、`obj`、`Logs`、`Build`、`UserSettings` 以及生成的 `*.csproj`/`*.sln`：本地或生成内容，不要手工编辑或提交。

## 测试代码

- 测试只能放在 `Assets/Tests/`。EditMode（NUnit）测试放在 `Assets/Tests/Editor/`，命名空间用 `Game.Tests`。不要在 `Assets/Scripts/` 下新增测试、测试场景、测试专用组件或集成测试脚本。
- `Assets/Tests/` 是临时目录，会被定期整体清空。测试只用来验证当次改动，不要假设以前写的测试还在。
- 会被游戏、工具或多个任务复用的逻辑（包括 Editor 工具、校验器、数据构造辅助）不得放在 `Assets/Tests/`，应放到所属功能目录。
- 不要为了方便测试在运行时代码里增加测试钩子或测试专用入口。
- 测试产生的报告、截图和日志写到 `Logs/`，不要写进 `Docs/`。

## 一次性脚本

- 只执行一次的迁移、批量生成或修补 prefab 的 Editor 脚本，用完后连同 `.meta` 删除，不要留在仓库里。
- 需要长期保留的 Editor 工具只限构建钩子、配置编辑器、Inspector 扩展、校验菜单和预览工具，并放在所属功能的 `Editor/` 子目录。

## 运行时约束

- `GameController` 在 Windows Editor/Standalone 和 WebGL 中启动客户端 UI；以服务端进程启动时（`NetworkConfigurationLoader.IsServerProcess`，Linux 服务端构建）直接启动 Mirror server。更改启动逻辑时必须检查所有相关编译条件。
- 服务器必须保持权威：房间状态、轮次推进、下注校验、奖池/边池分配、超时操作和胜负结算应在 `GameManager`/服务端处理器中完成。
- 客户端提交的是请求，不能让客户端直接决定筹码、操作顺序、牌面或结算结果。
- `GameManager` 通过 `OnGameInfoChanged`、`OnGameStageEnded`、`OnRoomRoundEnded` 等事件驱动同步和流程推进。新增提前返回或异步等待时，检查事件是否仍然恰好触发一次。
- 对 `Check`、`Bet`、`Call`、`AllIn`、`Fold` 的修改要覆盖：当前操作玩家、最小加注、筹码不足、all-in、fold、断线、阶段结束以及边池。
- 登录由 `AccountAuthenticator` 完成，业务消息处理器都以 `requireAuthentication: true` 注册。玩家身份只能来自服务端的连接会话，不能信任消息里携带的玩家名。网络细则见 [Network/AGENTS.md](Assets/Scripts/Network/AGENTS.md)。
- Unity 对象只能在主线程访问。需要等待帧或秒数时沿用 UniTask，并明确处理对象销毁、断线和场景退出后的生命周期。
- UI prefab 在 `UIManager` 的 `_panels` 中注册，通过 `PushPanel`/`PopPanel`/`ResetPanels`/`DestroyPanel` 管理。新增面板时同时更新 prefab 注册和序列化引用。
- `SingletonPanel<T>` 默认以类型名作为唯一标识；只有确实需要多实例时才使用显式 identifier。

## 代码风格

- 使用 C# 9 与现有 Unity API 兼容的语法；不要依赖更高版本语言或运行时特性。
- 延续现有的四空格缩进和 Allman 大括号风格。
- 类型、方法、属性和事件使用 `PascalCase`；私有字段使用 `_camelCase`；局部变量和参数使用 `camelCase`。
- Inspector 引用使用私有字段加 `[SerializeField]`，不要仅为序列化而暴露 public 字段。
- 命名空间使用较大的分组，不跟随子目录变化：`Game`、`Game.Network`、`Game.UI`、`Framework.*`，以及已有的 `Game.Network.Accounts`、`Game.Network.Tasks` 等。新文件沿用同目录已有文件的命名空间，不要为新子目录新建命名空间，也不要使用 `Assets.Scripts.*`。
- 领域逻辑优先保持为不依赖 `MonoBehaviour` 的普通 C# 类型，便于 EditMode 测试。
- 避免无关的全文件格式化、using 重排或命名清理；此项目包含大量 Unity YAML 与 prefab 引用，小范围改动更容易审查。
- 新增、移动或删除 `Assets` 下资源时必须同步对应 `.meta` 文件，以保留 GUID。具体做法见 [poker-assets](.agents/skills/poker-assets/SKILL.md)。
- 仓库中的 C# 文件统一使用 CRLF。少数旧文件使用 GBK 中文编码（如 `NetworkRoom.cs`、`GameController.cs`、`RoomInfo.cs` 及部分 Framework/UI 文件）。编辑既有文件时先检测并保留原编码；不要借功能改动批量转码。新文件使用 UTF-8 和 CRLF。

## 依赖与资源修改

- Unity Package Manager 依赖记录在 `Packages/manifest.json` 和 `Packages/packages-lock.json`，二者应保持一致。
- NuGetForUnity 配置位于 `Assets/NuGet.config` 与 `Assets/packages.config`，还原结果位于 `Assets/Packages`。不要用普通 `dotnet restore` 随意覆盖这些 Unity 插件 DLL；UPM 已提供的库不要再通过 NuGet 安装第二份。
- 不要手工修改 Unity 生成的 `.csproj`/`.sln`；需要刷新时让 Unity 重新生成。
- 场景、prefab、材质和 ScriptableObject 是 Unity YAML。修改序列化字段名时使用 `[FormerlySerializedAs]` 或在 Editor 中迁移，避免静默丢失 Inspector 引用。
- 修改牌面数据、面板 prefab 或 Manager prefab 后，打开 `Game.unity` 验证引用没有变成 Missing。

## 验证流程

验证范围应与改动风险匹配，至少完成能运行的最小集合。

1. 使用 Unity `2022.3.52f1c1` 打开项目，等待脚本编译完成，确认 Console 没有新增 error。
2. 修改牌型、比较或其他纯逻辑时，按“测试代码”一节在 `Assets/Tests/Editor/` 编写针对本次改动的 EditMode 测试并运行。
3. 修改 UI、prefab 或生命周期时，运行 `Assets/Scenes/Game.unity`，覆盖连接、进入房间、开始牌局和断线返回标题页。
4. 修改服务端规则或网络协议时，至少以一个 server 和两个 client 验证建房、入房、准备、下注、阶段推进、结算与断线；不能只验证单机 UI。
5. 修改平台启动或构建配置时，分别检查目标客户端平台和 Linux server 构建。

在已安装匹配版本 Unity 的机器上，可用以下 PowerShell 命令运行 EditMode 测试；将 `$unityEditor` 替换为实际 `Unity.exe` 路径（编辑器正在打开该工程时无法使用命令行）：

```powershell
& $unityEditor -batchmode -nographics -projectPath (Get-Location).Path -runTests -testPlatform EditMode -testFilter "Game.Tests" -testResults "Logs/EditModeResults.xml" -logFile "Logs/EditModeTests.log"
```

Unity 未安装或版本不匹配时，不要声称测试已通过。`dotnet build Poker.sln` 不能替代 Unity 的编译、Mirror Weaver 和 Test Runner。

## 完成前检查

- 查看 `git status --short`，只保留与任务相关的修改；工作区已有的用户改动不得覆盖或回滚。
- 确认没有提交 `Library`、`Logs`、构建产物、IDE 文件或重新生成的工程文件。
- 确认新资源有 `.meta`，删除资源时其 `.meta` 也一并删除。
- 对网络状态新增/删除字段后，确认 server state、client projection、消息体、序列化和 UI 消费端全部一致。
- 对 serialized field、prefab 或场景做过修改时，在 Unity 中检查 Missing Script、Missing Reference 和 Console error。
- 最终说明实际执行过的测试，以及由于环境限制未执行的验证。
