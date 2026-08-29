# AGENTS.md

本文件适用于整个仓库。修改任何文件前，先阅读本文件以及目标目录下可能存在的更深层 `AGENTS.md`。

## 项目概览

- 这是一个基于 Unity 的多人德州扑克项目，产品名为 `Poker`。
- Unity 编辑器版本固定为 `2022.3.52f1c1`；除非任务明确要求，不要升级编辑器、包版本或序列化格式。
- 渲染管线为 URP 14，输入系统为 Unity Input System。
- 联机层使用内嵌的 Mirror `93.0.0`；异步代码使用内嵌的 UniTask `2.5.10`。
- 客户端界面使用 uGUI、TextMeshPro；JSON 主要使用 `System.Text.Json`，相关 NuGet DLL 位于 `Assets/Packages`。
- 当前自有代码未拆分 asmdef：运行时代码编译进 `Assembly-CSharp`，`Editor` 目录下的测试编译进 `Assembly-CSharp-Editor`。

## 目录与职责

- `Assets/Scripts/Game`：扑克牌、牌型比较、游戏设置和客户端启动入口。
  - `Poker.cs`、`PokerHand.cs`、`PokerPattern.cs`、`PokerUtility.cs` 是纯牌型领域逻辑。
  - `GameController.cs` 是持久化的客户端/服务器启动入口。
- `Assets/Scripts/Network`：服务端房间、玩家、牌局状态以及 Mirror 消息收发。
  - `NetworkRoom.cs` 继承 `Mirror.NetworkManager`，注册消息处理器并连接网络层、游戏层和 UI。
  - `RoomManager` 管理大厅和房间；`PlayerManager` 管理玩家名与连接的映射。
  - `GameManager` 是服务端牌局规则的权威实现；`ServerGameInfo` 保存完整状态。
  - `ClientGameInfo` 是按玩家裁剪后的同步视图，不得泄漏其他玩家底牌或仅服务端可见的状态。
  - `Messages` 下的类型是网络协议。更改字段、类型或序列化方式时，必须同步检查发送端、接收端和 `Serialization.cs`。
- `Assets/Scripts/UI`：标题、房间列表、房间和牌局面板。
- `Assets/Scripts/Framework`：单例、UI 管理、事件、状态机、计时器、输入、日志和序列化等通用设施。
- `Assets/Scripts/SO` 与 `Assets/Resources/SO`：扑克牌图片等 ScriptableObject 数据。
- `Assets/Prefabs/Manager`：`GameController`、`NetworkRoomManager`、`SingletonSOManager`、`UIManager` 的入口 prefab。
- `Assets/Scenes/Game.unity`：唯一启用的构建场景，包含上述管理器 prefab。
- `Assets/Scenes/TestScene.unity`：未加入正式构建的手动测试场景。
- `Assets/Scripts/Game/Editor/PokerTest.cs`：NUnit EditMode 牌型测试。
- `Assets/Scripts/Tests`：由 `MonoBehaviour` 驱动的手动/场景测试脚本，不属于自动化 NUnit 套件。
- `Assets/Mirror`、`Assets/Plugins/UniTask`、`Assets/OArielG`、`Assets/PlayingCards`、`Assets/TextMesh Pro`：第三方或导入资源。除非任务明确针对依赖本身，否则不要修改。
- `Library`、`Temp`、`obj`、`Logs`、`Build`、`UserSettings` 以及生成的 `*.csproj`/`*.sln`：本地或生成内容，不要手工编辑或提交。

## 运行时约束

- `GameController` 在 Windows Editor/Standalone 和 WebGL 中启动客户端 UI；Linux Standalone 分支直接启动 Mirror server。更改启动逻辑时必须检查所有相关编译条件。
- 服务器必须保持权威：房间状态、轮次推进、下注校验、奖池/边池分配、超时操作和胜负结算应在 `GameManager`/服务端处理器中完成。
- 客户端提交的是请求，不能让客户端直接决定筹码、操作顺序、牌面或结算结果。
- `GameManager` 通过 `OnGameInfoChanged`、`OnGameStageEnded`、`OnRoomRoundEnded`、`OnRoomGameOver` 驱动同步和流程推进。新增提前返回或异步等待时，检查事件是否仍然恰好触发一次。
- 对 `Check`、`Bet`、`Call`、`AllIn`、`Fold` 的修改要覆盖：当前操作玩家、最小加注、筹码不足、all-in、fold、断线、阶段结束以及边池。
- Mirror handler 当前使用 `requireAuthentication: false`。不要把“已有连接”当作已认证身份；任何玩家名或房间归属相关操作都要在服务端验证。
- Unity 对象只能在主线程访问。需要等待帧或秒数时沿用 UniTask，并明确处理对象销毁、断线和场景退出后的生命周期。
- UI prefab 在 `UIManager` 的 `_panels` 中注册，通过 `ShowPanel`/`HidePanel`/`DestroyPanel` 管理。新增面板时同时更新 prefab 注册和序列化引用。
- `SingletonPanel<T>` 默认以类型名作为唯一标识；只有确实需要多实例时才使用显式 identifier。

## 代码风格

- 使用 C# 9 与现有 Unity API 兼容的语法；不要依赖更高版本语言或运行时特性。
- 延续现有的四空格缩进和 Allman 大括号风格。
- 类型、方法、属性和事件使用 `PascalCase`；私有字段使用 `_camelCase`；局部变量和参数使用 `camelCase`。
- Inspector 引用使用私有字段加 `[SerializeField]`，不要仅为序列化而暴露 public 字段。
- 命名空间按职责沿用 `Game`、`Game.Network`、`Game.UI` 和 `Framework.*`；新文件不要使用 `Assets.Scripts.*` 命名空间。
- 领域逻辑优先保持为不依赖 `MonoBehaviour` 的普通 C# 类型，便于 EditMode 测试。
- 避免无关的全文件格式化、using 重排或命名清理；此项目包含大量 Unity YAML 与 prefab 引用，小范围改动更容易审查。
- 新增、移动或删除 `Assets` 下资源时必须同步对应 `.meta` 文件，以保留 GUID。优先通过 Unity Editor 移动场景、prefab 和资源。
- 仓库中的 C# 文件统一使用 CRLF，但有 16 个旧文件使用非 UTF-8 中文编码，包括 `GameController.cs`、`NetworkRoom.cs`、`RoomInfo.cs` 以及部分 Framework/UI 文件。编辑既有文件时先检测并保留原编码；不要借功能改动批量转码。新文件使用 UTF-8 和 CRLF。

## 依赖与资源修改

- Unity Package Manager 依赖记录在 `Packages/manifest.json` 和 `Packages/packages-lock.json`，二者应保持一致。
- NuGetForUnity 配置位于 `Assets/NuGet.config` 与 `Assets/packages.config`，还原结果位于 `Assets/Packages`。不要用普通 `dotnet restore` 随意覆盖这些 Unity 插件 DLL。
- 不要手工修改 Unity 生成的 `.csproj`/`.sln`；需要刷新时让 Unity 重新生成。
- 场景、prefab、材质和 ScriptableObject 是 Unity YAML。修改序列化字段名时使用 `[FormerlySerializedAs]` 或在 Editor 中迁移，避免静默丢失 Inspector 引用。
- 修改 `PokerCardsSO`、面板 prefab 或 Manager prefab 后，打开 `Game.unity` 验证引用没有变成 Missing。

## 验证流程

验证范围应与改动风险匹配，至少完成能运行的最小集合。

1. 使用 Unity `2022.3.52f1c1` 打开项目，等待脚本编译完成，确认 Console 没有新增 error。
2. 修改牌型或比较逻辑时，运行 Test Runner 的 EditMode 测试 `Game.Tests.PokerTest`。
3. 修改 UI、prefab 或生命周期时，运行 `Assets/Scenes/Game.unity`，覆盖连接、进入房间、开始牌局和断线返回标题页。
4. 修改服务端规则或网络协议时，至少以一个 server 和两个 client 验证建房、入房、准备、下注、阶段推进、结算与断线；不能只验证单机 UI。
5. 修改平台启动或构建配置时，分别检查目标客户端平台和 Linux server 构建。

在已安装匹配版本 Unity 的机器上，可用以下 PowerShell 命令运行 EditMode 测试；将 `$unityEditor` 替换为实际 `Unity.exe` 路径：

```powershell
& $unityEditor -batchmode -nographics -projectPath (Get-Location).Path -runTests -testPlatform EditMode -testFilter "Game.Tests.PokerTest" -testResults "Logs/EditModeResults.xml" -logFile "Logs/EditModeTests.log"
```

Unity 未安装或版本不匹配时，不要声称测试已通过。`dotnet build Poker.sln` 不能替代 Unity 的编译、Mirror Weaver 和 Test Runner。

## 完成前检查

- 查看 `git status --short`，只保留与任务相关的修改；工作区已有的用户改动不得覆盖或回滚。
- 确认没有提交 `Library`、`Logs`、构建产物、IDE 文件或重新生成的工程文件。
- 确认新资源有 `.meta`，删除资源时其 `.meta` 也一并删除。
- 对网络状态新增/删除字段后，确认 server state、client projection、消息体、序列化和 UI 消费端全部一致。
- 对 serialized field、prefab 或场景做过修改时，在 Unity 中检查 Missing Script、Missing Reference 和 Console error。
- 最终说明实际执行过的测试，以及由于环境限制未执行的验证。
