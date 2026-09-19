# UI 目录整理与双平台构建（2026-09-09）

## 目录整理

遵循 [UIConvention.md](UIConvention.md)，将原本散落在 UI 根目录的 31 个脚本、14 个预制体归入功能目录，共移动 45 个资源。

| 功能目录 | 内容 |
| --- | --- |
| AchievementPanel | 成就页面、横幅、成就行 |
| DailyTaskPanel | 每日任务面板、卡片、配套滚动组件 |
| LeaderboardPanel | 排行榜与普通／服刑排行行 |
| ShopPanel | 商城、仓库、商品卡片、所有面板 partial 文件 |
| OptionPanel | 菜单设置、游戏内设置、共享设置基类 |
| TitlePanel、LoginPanel、MsgBox | 各自面板 |
| RoomListPanel、RoomPanel | 面板主脚本归入既有列表项目录 |
| Namecards、Shared | 通用名片组件、金额格式化工具 |

脚本基路径为 `Assets/Scripts/UI/`；面板预制体基路径为 `Assets/Prefabs/UI/Panels/`。GamePanel、Gifts 既有目录保持原样，Canvas/EventSystem 与名片展示资源保留原位置。

通过 Unity `AssetDatabase.MoveAsset` 移动，逐一核对 45 个资源的 SHA-256 与 `.meta` 内容均未改变。未修改类型、命名空间、序列化字段或资源 GUID。同步调整 8 个编辑器／测试文件的资源路径以及相关文档链接。

## 验证

- Unity：2022.3.52f1c1。
- 全部 `Game.Tests` EditMode：647 通过，0 失败，0 跳过；作业 `be183c8aa2734e2f83e4780f6ad2c2e6`。
- 37 个 UI／Manager prefab：无 Missing Script 或损坏对象引用。
- `Assets/Scenes/Game.unity`：Missing Script 0，损坏 prefab 0。

## 构建

输出目录：`Build/Release/2026-09-09/`。仅包含正式构建场景 `Assets/Scenes/Game.unity`；非 Development Build。

- Linux：StandaloneLinux64 / Server，输出 `Linux/PokerServer.x86_64`。
- WebGL：项目既有 Aervento 模板，输出 `WebGL/`。
- 保留 `runInBackground: 1` 及现有网络配置。客户端和服务端配置分别位于 `Configs/Network/client-network.json` 与 `Configs/Network/server-network.json`。
- 使用 `Tools/Release/package_release.py` 生成版本化 WebGL 资源归档、Linux 发布归档及 SHA-256 清单，排除 DoNotShip 调试输出。

Linux 构建成功：作业 `build-64fcc653cf`，189.97 秒，84.61 MiB，0 错误、2 警告。

使用正式 Linux Server 产物在 WSL Ubuntu 中启动，配合已有的同版本协议 Windows 验证客户端完成：

- `linux-task-ingame-5ae6f5bf`：一服三端，两手牌，弃牌及结算阶段领奖、下一手筹码生效、重复领取拒绝；四份成功报告，账户／账本总额均 32,000，未结算牌局 0。
- `linux-achievements-143ab099`：一服三端，两手牌，成就进度、展示、奖励及重复领取检查；四份成功报告，账户／账本总额均 31,000，未结算牌局 0。
- 使用独立测试数据库和网络配置，结束后关闭相关进程并恢复原有 Windows 登录偏好。

WebGL 构建成功：作业 `build-c134c096bd`，657.86 秒，119.30 MiB，0 错误、3 警告。实际 BuildOptions 不含 Development，包含 IL2CPP、脚本优化与 StripDebugSymbols。

构建警告均为既有未使用成员：`GameManager.OnRoomGameOver`、`PokerIntegrationRunner._shopRoomChecked`，WebGL 额外报告服务端使用的 `AccountAuthenticator._initialChips`。未因本次目录整理新增编译错误。

## 发布包与运行检查

| 归档 | 字节数 | SHA-256 |
| --- | ---: | --- |
| Poker-WebGL.zip | 62,383,848 | `e0c094f4180f61d959195626c776afeb02f2ccb88d35f09a9a0b1734239ac342` |
| Poker-Linux-Server.tar.gz | 36,209,679 | `e76a94a1bcf1b47f1cd687b020534c15d6f36b439dafc798a27a337e9c52347c` |

- WebGL 版本目录：`Build/47f21505dd74ab82`。ZIP CRC、每项 Build 资源哈希、HTML 版本路径、两个归档的 SHA-256 均通过。
- Linux 归档包含 `PokerServer.x86_64`、`UnityPlayer.so`、数据目录及服务端配置；可执行文件权限正确。两个归档均排除 DoNotShip 调试产物。
- 正式 Linux 程序在独立临时数据目录启动，WebSocket 返回 HTTP 101，SQLite `PRAGMA integrity_check` 为 `ok`，成就与任务表齐全。
- 从 WebGL ZIP 解压后的目录通过本机 HTTP 提供服务，仅测试 HTTP 响应覆盖连接地址，未改写归档内的部署配置。浏览器实际加载 WASM、注册隔离测试账户、登录大厅、返回标题、打开设置／成就／每日任务并取得服务器数据。测试过程中浏览器无错误日志，仅 Unity 自带的持久化自动同步弃用提示。
- 停止 Linux 服务后，WebGL 正确回到标题并显示断线弹窗；测试 Linux 进程退出码 0。测试 HTTP 服务与浏览器标签页随后关闭。
- Unity 恢复为 Windows64 / Player，停留在 `Game.unity` 非播放状态，供其他任务继续使用。

本轮未向外部服务器部署，也未再次进行移动真机或公网 HTTPS 验证。打包内保留现有 `127.0.0.1:7777` 客户端默认配置，实际部署应沿用目标环境的地址与 TLS 配置。客户端和服务端需配套更新。
