# 网络部署配置

客户端和服务端在启动联网前读取各自的 JSON。配置解析或证书校验失败不会退回 Inspector 中的地址、端口或加密设置。配置只在启动时加载，成功后登录和断线重连复用同一份配置。

## 客户端

源码默认文件：`Assets/StreamingAssets/client-network.json`。仓库提供可直接连接本机测试服务器的配置：

```json
{
  "serverHost": "127.0.0.1",
  "serverPort": 7777,
  "useSsl": false,
  "connectTimeoutSeconds": 10
}
```

| 字段 | 说明 |
| --- | --- |
| `serverHost` | 域名、IPv4 或 IPv6 地址，不包含协议、端口、路径、用户名或密码。 |
| `serverPort` | 外部连接端口，整数 1～65535。与服务器监听端口分开管理，支持代理转发。 |
| `useSsl` | 必须明确填写布尔值。`true` 使用 WSS，`false` 使用 WS。HTTPS WebGL 页面必须使用 WSS。 |
| `connectTimeoutSeconds` | 建立连接的最长等待时间，整数 1～300 秒。启动后台连接、手动登录及重连均适用；不控制牌局操作倒计时或认证响应时间。 |

Windows 默认读取 `Poker_Data/StreamingAssets/client-network.json`。可以用 `-pokerClientConfig "绝对或相对文件路径"` 指定本机文件；显式相对路径以启动进程的工作目录为基准。

WebGL 自动读取网站游戏目录下的 `StreamingAssets/client-network.json`，由 `Application.streamingAssetsPath` 确定位置，支持游戏部署在网站子目录。玩家正常启动时无需操作配置；读取结束后才允许登录。下载单次超时 10 秒，网络或 HTTP 失败会自动再试一次，间隔 0.5 秒；这段启动下载时间不能从尚未读取的 JSON 中获取。读取或校验最终失败时，显示“暂时无法连接服务”，提供“重试”和“稍后”；稍后再点击开始游戏也会重试。

配置为公开文件，不得填写任何账户密码、会话令牌、证书私钥或服务端凭据。直接修改线上 JSON 后，新打开或刷新的页面使用新配置；已经运行的页面保留原配置，当前牌局不会被切换服务器。

### Web 服务器缓存与发布

配置请求附带每次不同的查询参数，避免与 Unity 构建缓存绑定。Web 服务器还应对这个文件返回 `Cache-Control: no-store`，CDN 或 Service Worker 如有额外缓存规则，也应排除它。以游戏部署在 `/poker/` 的 Nginx 为例，在现有站点中加入：

```nginx
location = /poker/StreamingAssets/client-network.json {
    add_header Cache-Control "no-store" always;
    try_files $uri =404;
}
```

沿用该站点现有的 `root`。此文件应返回 JSON，不能被单页站点的 fallback 规则改写成 `index.html`。配置与游戏页面同源部署，读取时无需额外的跨域配置。

上传前校验 JSON，先写临时文件再在同一文件系统中替换正式文件，避免读到半份内容。发布新版游戏时保留线上 `StreamingAssets/client-network.json`，不要用构建中的本地测试配置覆盖它。Unity 构建输出自身仍使用源码中的默认客户端 JSON。

## 服务端

源码默认文件：`Configs/server-network.json`，在 `Assets` 之外，不会打进 WebGL 资源。

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
| `listenPort` | 监听 TCP 端口，整数 1～65535。继续使用 SimpleWebTransport 默认监听地址，不配置域名。防火墙和公网端口映射由部署环境设置。 |
| `useSsl` | 是否由游戏进程提供 TLS。必须明确填写布尔值；开启时固定使用 TLS 1.2，并在监听前校验证书。 |
| `path` | 包含私钥的 PFX/P12 证书的绝对路径。SSL 开启时必填；关闭时可以留空或省略。Windows 路径可使用正斜杠。 |
| `password` | 证书密码，无密码时填空字符串或省略。SSL 关闭时忽略。 |
| `maxConnections` | 游戏服务器总连接上限，正整数；包括大厅、旁观和各房间连接，不等于单桌参战人数上限。 |

`path` 与 `password` 是现有 Mirror 证书加载器的字段名。加载器直接读取这份服务端 JSON，无需第三份证书配置。启动时检查文件存在、密码可解密、包含私钥且处于证书有效期；浏览器信任链与域名匹配仍应由部署使用的证书满足。日志不会打印配置原文或证书密码。

服务端默认读取可执行文件旁的 `Configs/server-network.json`，不依赖进程工作目录。Editor 对应项目根目录。Windows/Linux 构建完成后自动复制默认服务端配置；输出目录已经存在该文件时保留原文件，避免覆盖本机部署参数。实际生产配置、证书及密码应放在服务器私有目录，通过启动参数指定，不随网页发布或提交到仓库。

Linux Standalone / Dedicated Server 默认启动服务端；Windows 默认启动客户端，增加 `-pokerServer` 可以运行临时服务端。两者均支持 `-pokerServerConfig "文件路径"`。不要通过 Mirror 的旧调试 HUD 启动联网：该 HUD 在运行时关闭，防止绕过配置；Mirror 的 headless 自动启动也统一交给 GameController。

### 本地测试

保持两份默认 JSON 不变，构建 Windows Player 后运行：

```powershell
# 一个服务器进程
.\Poker.exe -batchmode -nographics -pokerServer -logFile server.log

# 分别启动多个客户端进程
.\Poker.exe -logFile client-1.log
.\Poker.exe -logFile client-2.log
```

也可让 Windows 服务器进程配合 Unity Editor Play 模式客户端测试。测试其他端口时，同时修改服务端监听端口和客户端连接端口，或分别传入自定义 JSON：

```powershell
.\Poker.exe -batchmode -nographics -pokerServer -pokerServerConfig "D:/PokerTest/server-network.json"
.\Poker.exe -pokerClientConfig "D:/PokerTest/client-network.json"
```

### 直接提供 WSS

客户端：`serverHost` 为游戏域名，`serverPort` 为 7777，`useSsl` 为 true。

服务端：`listenPort` 为 7777，`useSsl` 为 true，`path` 为 `/srv/poker/certificates/server.pfx`，填写相应 `password`。

```bash
./Poker.x86_64 -batchmode -nographics \
  -pokerServerConfig /srv/poker/private/server-network.json \
  -logFile /srv/poker/logs/server.log
```

修改服务端 JSON 或证书后重启服务。配置缺失、JSON 错误、参数无效、证书不可用或监听失败时记录原因并以退出码 1 退出；Editor 中停止 Play 模式。不会自动改为非加密监听。

### 由反向代理提供 WSS

客户端连接代理公网端口，例如 `serverPort: 443`、`useSsl: true`；游戏服务器监听内部端口，例如 `listenPort: 7777`、`useSsl: false`。代理负责证书和 WebSocket 转发。内部游戏端口的访问范围由网络与防火墙配置控制。

## 验证记录

2026-09-05，Unity 2022.3.52f1c1。用户调整验证范围后只执行配置读取与启动冒烟，不再运行全量测试。

| 检查 | 实际结果 |
| --- | --- |
| Windows Player 构建 | 最新作业 `build-3fd197b8f0` 成功，0 error、1 个既有 OnRoomGameOver 未使用警告。 |
| Linux Dedicated Server 构建 | 作业 `build-0269870ca6` 成功，0 error、1 个相同警告；此构建在最后补充启动日志同步输出到 Player.log 之前完成。 |
| Windows 原生客户端配置 | 自定义 JSON 指定 17893 端口，在不同工作目录启动；配置加载成功，测试服务收到 `Host: 127.0.0.1:17893` 并完成普通 WebSocket 握手。 |
| Windows 服务端启动失败 | 缺失配置、无效端口、缺失 SSL 证书三种情况都在初始化数据库之前以退出码 1 结束，日志说明原因，未打印测试密码。 |
| Linux 服务端配置 | WSL Ubuntu 下以默认服务端模式启动，从外部 JSON 读取 17894 端口和总连接上限 7，完成 WebSocket 握手；数据库与日志位于隔离目录。 |
| Linux SSL 启动 | 外部 JSON 开启 SSL，读取临时 PFX 路径和密码，在 17897 端口完成加密 WebSocket 握手。探针只信任这张测试证书并验证 localhost 域名，没有修改系统证书信任。 |
| HTTP 配置下载 | UnityWebRequest 请求子目录配置，首个响应 503 后自动重试成功；两次查询参数不同，读取到指定端口 17895、SSL false 和超时 3 秒。该探针在 Editor 中运行。 |
| Game.unity 错误与重试 UI | 错误配置时停留标题页、客户端未启动、显示重试框；恢复 JSON 后点击实际确认按钮事件，提示关闭、配置就绪、连接地址和端口正确。运行时旧 HUD 已关闭。 |

证据位于忽略目录 `Logs/NetworkConfiguration/` 的 `startup-smoke`、`http-smoke`、`linux-startup-smoke`、`linux-tls-startup-smoke`。

用户调整范围之前，新增 25 项配置 EditMode 测试及既有套件共 **218/218** 通过，作业 `62ce28dadcf3433dbe3f5f5e3ffaf785`；一服三端使用外部 JSON 的 17891 端口完成 spectators 场景，记录为 `integration-f7c308e2`。这些是配置功能阶段的验证；每日任务功能完成后，已重新执行 257 项全量 EditMode 和 Windows / Linux 一服三端回归，详见 [每日任务验证](D:/GitHubRepos/Poker/Docs/DailyTasks.md)。

本轮未执行 WebGL 构建、实际浏览器 HTTPS/WSS 端到端验证或真实公网证书部署验证。原定下一功能完成后的自动化与多人流程回归现已完成；上述浏览器和公网验证仍未执行。
