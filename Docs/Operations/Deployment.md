# 部署与配置

## 配置目录

运行时配置都在仓库和构建产物根目录的 `Configs/`，由 `Framework.Configs.ConfigManager` 解析路径（不依赖进程工作目录）：

```text
Configs/
├─ Network/     client-network.json、server-network.json（完整配置）
├─ Shop/        Items.json、Gifts.json、Namecards.json、CardFaces.json（完整商品配置）
└─ DailyTasks/  TaskDefinitions.json、FixedTasks.json、RandomTasks.json（完整配置）
```

- 商店四个文件是全部商品的唯一来源，服务器启动时校验，错误时拒绝启动，见 [货币、商店与物品](../Systems/Economy.md)、[礼物](../Systems/Gifts.md)、[名片](../Systems/Namecards.md)、[牌面](../Systems/CardFaces.md)。每日任务三表见 [每日任务](../Systems/DailyTasks.md)。
- 构建会把配置复制到产物的 `Configs/`（产物中已存在的文件不会被覆盖）：Windows 构建可作客户端或临时服务器，带两份网络配置和商店、每日任务配置；Linux Dedicated Server 带 `server-network.json` 和商店、每日任务配置，不带 `client-network.json`；WebGL 只发布公开的 `Configs/Network/client-network.json`。
- 所有配置只在启动时读取，修改后重启对应的客户端或服务器。

## 客户端网络配置

`Configs/Network/client-network.json`：

```json
{
  "serverHost": "127.0.0.1",
  "serverPort": 7777,
  "useSsl": false,
  "connectTimeoutSeconds": 10,
  "resourceBaseUrl": ""
}
```

| 字段 | 说明 |
| --- | --- |
| `serverHost` | 域名、IPv4 或 IPv6，不含协议、端口和路径。 |
| `serverPort` | 客户端连接的外部端口（1～65535），可与服务器监听端口不同（反向代理）。 |
| `useSsl` | 必填布尔值：`true` 用 WSS，`false` 用 WS。HTTPS 页面上的 WebGL 必须用 WSS。 |
| `connectTimeoutSeconds` | 建立连接的超时（1～300 秒），不影响牌局倒计时。 |
| `resourceBaseUrl` | 可选，远程内容的根地址，见 [远程资源](RemoteArtwork.md)。 |

- Windows 默认读取可执行文件旁的该文件，可用 `-pokerClientConfig <路径>` 指定。
- WebGL 相对于当前页面 URL 读取（支持部署在子目录），带随机查询参数避免缓存；Web 服务器应对该文件返回 `Cache-Control: no-store`，并且不能被单页应用的回退规则改写成 `index.html`。读取失败时显示“暂时无法连接服务”，可以重试。
- 这是公开文件，不能包含任何密码、令牌或证书。发布新版游戏时保留线上已有的这份文件。

## 服务器网络配置

`Configs/Network/server-network.json`（不会进入 WebGL 包）：

```json
{
  "listenPort": 7777,
  "useSsl": false,
  "path": "",
  "password": "",
  "maxConnections": 100
}
```

| 字段 | 说明 |
| --- | --- |
| `listenPort` | 监听端口。 |
| `useSsl` | 是否由游戏进程提供 TLS（固定 TLS 1.2）。 |
| `path` / `password` | 含私钥的 PFX/P12 证书的绝对路径与密码，`useSsl` 为 `true` 时必填。 |
| `maxConnections` | 服务器总连接数上限（含大厅与旁观），不是单桌人数。 |

- 启动时校验证书存在、可解密、含私钥且在有效期内。网络或商店配置缺失、格式错误、证书不可用或监听失败时，日志写明出错的文件、条目和字段，然后以退出码 1 退出，不会退回非加密监听。
- 默认读取可执行文件旁的 `Configs/Network/server-network.json`。用 `-pokerServerConfig <路径>` 改读其他位置（如服务器私有目录）时，该文件必须存在；证书与密码不要提交到仓库。
- 提供 WSS 的两种方式：游戏进程直接加载证书（`useSsl: true`）；或由反向代理终止 TLS 并转发 WebSocket（客户端 `serverPort: 443`、`useSsl: true`，服务器 `useSsl: false` 监听内网端口）。

## 启动方式

| 场景 | 方式 |
| --- | --- |
| Editor 调试 | 打开 `Game.unity` 进入 Play，自动启动 Host（服务器 + 本地客户端）。读取两份网络配置，但在内存中强制客户端连 `127.0.0.1`、关闭 SSL，不改文件。 |
| Linux 服务器构建 | 默认以服务器启动：`./PokerServer.x86_64 -batchmode -nographics -logFile -`，可加 `-pokerServerConfig <路径>` |
| Windows 临时服务器 | `Poker.exe -batchmode -nographics -pokerServer -logFile server.log` |
| Windows 客户端 | 直接运行 `Poker.exe`，可加 `-pokerClientConfig <路径>` |

每日任务配置目录可用 `--task-config <目录>` 或环境变量 `POKER_TASK_CONFIG_DIR` 指定。账户数据库 `PokerAccounts.db` 位于服务器的 `Application.persistentDataPath`（Linux 上为 `$XDG_CONFIG_HOME/unity3d/AerVento/Poker/`），服务端文件日志写在工作目录下的 `logs/`。不要使用 Mirror 自带的调试 HUD 启动网络（运行时已禁用）。

### Linux systemd

程序与配置放在 `/opt/poker`，数据库和日志放在独立的状态目录，替换程序时可以整体清空 `/opt/poker`：

```ini
[Service]
User=poker
Group=poker
StateDirectory=poker
WorkingDirectory=/var/lib/poker
Environment=XDG_CONFIG_HOME=/var/lib/poker
ExecStart=/opt/poker/PokerServer.x86_64 -batchmode -nographics -logFile -
Restart=on-failure
RestartPreventExitStatus=1
```

退出码 1 表示配置错误，`RestartPreventExitStatus=1` 让 systemd 不再反复重启，修正配置后手动启动。

## 发布打包

1. 在 Unity 中构建 WebGL（模板 `Aervento`）和 Linux 服务器（StandaloneLinux64 / Server），只包含 `Game.unity`，放在同一个发布目录的 `WebGL/` 与 `Linux/` 下。
2. 运行 `python Tools/Release/package_release.py <发布目录>`（可选 `--webgl-only` / `--linux-only`），生成 WebGL 归档、Linux 归档和 SHA-256 清单，并排除 `DoNotShip` 调试输出。
   - Linux 包固定使用 `PokerServer.x86_64`、`PokerServer_Data/` 和同级 `UnityPlayer.so`，与现有 systemd 服务保持一致；构建输出名不同时，脚本会自动映射。部署时程序文件与 `Configs/` 整体替换，不能只换其中一部分。
   - 归档内文件权限统一为 `644`（目录、可执行文件和 `.so` 为 `755`），解压后 Web 服务器和服务进程用户都能读取。
   - WebGL 包把 `Build/` 放进按内容哈希命名的子目录并同步页面引用，附带 `WEBGL-MANIFEST.json`，避免浏览器把旧脚本和新 WASM 混用。部署时先上传版本目录，最后更新入口页面；入口页面使用 `Cache-Control: no-cache`，版本目录可以长期缓存。
   - 包内的 `Configs/Network/client-network.json` 是开发默认值，解压时不要覆盖线上那份。
3. 远程内容需要单独构建和上传，见 [远程资源](RemoteArtwork.md)。WebGL 构建输出中的 `page-version.txt` 与 `index.html` 一起上传，并使用 `Cache-Control: no-cache`；旧页面据此提示玩家刷新。
4. 客户端与服务器使用同一版本的网络协议，需一起发布。服务器的 `Configs/Shop/` 随 Linux 构建复制（已存在的文件不会被覆盖），线上修改商品时直接编辑服务器上的这四个文件。

## 数据库

- SQLite schema 由服务器启动时自动升级；升级前按运维流程备份。
- 存储异常时服务器会停服，见 [房间与牌桌](../Systems/TableAndRooms.md) 的“存储故障”。
