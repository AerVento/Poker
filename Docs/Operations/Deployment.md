# 部署与配置

## 配置目录

运行时配置都在仓库和构建产物根目录的 `Configs/`，由 `Framework.Configs.ConfigManager` 解析路径（不依赖进程工作目录）：

```text
Configs/
├─ Network/     client-network.json、server-network.json（完整配置）
├─ Items/       Items.json、Namecards.json、CardFaces.json、Gifts.json、Contracts.json（物品与功能目录）
├─ Shop/        Shop.json（商城出售的物品、价格与上架）
├─ Accounts/    StarterGrants.json（新账号初始发放）
├─ SlotMachine/ Lottery.json（抽奖费用、保底、档位与奖池权重）
└─ DailyTasks/  TaskDefinitions.json、FixedTasks.json、RandomTasks.json（完整配置）
```

- `Items/`、`Shop/`、`Accounts/` 是全部物品与商品的唯一来源。服务器启动时把它们与每日任务、抽奖配置一起校验，一次列出所有问题，有任何错误时拒绝启动，见 [货币、商店与物品](../Systems/Economy.md)、[礼物](../Systems/Gifts.md)、[名片](../Systems/Namecards.md)、[牌面](../Systems/CardFaces.md)。每日任务三表见 [每日任务](../Systems/DailyTasks.md)。抽奖配置见 [筹码抽奖](../Systems/SlotMachine.md)。
- 构建会把配置复制到产物的 `Configs/`（产物中已存在的文件不会被覆盖）：Windows 构建可作客户端或临时服务器，带两份网络配置和物品、商城、初始发放、每日任务、抽奖配置；Linux Dedicated Server 带 `server-network.json` 和物品、商城、初始发放、每日任务、抽奖配置，不带 `client-network.json`；WebGL 只发布公开的 `Configs/Network/client-network.json`。
- 所有配置只在启动时读取，修改后重启对应的客户端或服务器。

## Unity 配置编辑器

通过 `Poker/配置编辑器` 打开工具。窗口自动扫描项目根目录 `Configs/` 下所有 JSON，左侧以可折叠文件树显示，支持文件名搜索和读取端筛选。文件名前的绿色 `[客户端]` 与红色 `[服务端]` 表示谁直接读取源文件，不表示网络下发的数据范围。`Network/client-network.json` 属于客户端，其余当前配置属于服务端。新增文件会自动出现；读取端来自数据类型的 `[Config(Usage = ...)]`。没有对应类型或未声明读取端的文件标为“未分类”。已登记但尚未创建的文件也会列出，可显式创建默认配置，不覆盖已有文件。

表单按 C# 类型的 Newtonsoft 序列化成员生成，遵循 `JsonProperty` 字段名和 `JsonIgnore`，显示字段上的 `Header`、`Tooltip`、`Space`；枚举显示选项。对象显示为 Inspector 风格的折叠分组；数组可添加、复制、移除和上下移动元素，长数组每页显示 25 项。已知元素类型的“添加”调用无参构造函数，使用字段初始化值及嵌套默认值；“复制”单独保留原元素内容。必填字段始终显示且没有移除按钮；进入表单时，缺失或为 null 的必填字段补入草稿并提示保存，不自动写回磁盘。可选字段缺失时显示“未设置”和“添加”，一次生成可编辑的默认值（字符串为空串，对象调用构造函数）；存在时显示 ×，移除后恢复未设置状态。未登记类型的 JSON 仍可通用编辑并选择新字段／元素类型。JSON 视图带独立滚动区域，支持鼠标滚轮和滚动条，用于直接编辑原文、修改字段结构或修复格式错误。表单与 JSON 共用草稿，支持 Unity 撤销／重做；切换文件或关闭窗口时会提示未保存的修改。

引用字段可标记 `[ItemId]` 或 `[TaskId]`（定义在 `Game` 命名空间）。点击字段打开搜索选择框，可按 ID 或名称搜索，点击条目填入 ID，也可使用自行输入的 ID。物品候选来自磁盘上的 `Items/Items.json`，任务候选来自 `DailyTasks/TaskDefinitions.json`；源文件保存后自动刷新，未保存的其他窗口草稿不作为候选来源。已填写但不存在的 ID 显示红字，不限制输入、不回退旧值；业务校验仍由原校验器完成。源文件无法读取时显示简短提示并清空候选列表。定义新物品／新任务自身的 `Id` 不使用这些特性。

保存先检查 JSON 格式与重复字段，并检测磁盘文件是否在外部改动；冲突时保留草稿并拒绝覆盖，可先复制 JSON 再重新读取。保存保留数字精度、字段顺序、未知字段、原文件换行和 UTF-8 BOM。工具不修改配置部署规则，不会自动发布或重启程序。

窗口顶部的“校验全部配置”检查磁盘上的所有 JSON 格式、客户端与服务端网络配置（含启用 SSL 时的证书），以及服务端物品、商城、初始发放、每日任务和抽奖业务规则。有未保存修改时先提示保存；校验成功或失败均通过弹窗反馈，成功不向 Console 打印信息。没有对应类型的 JSON 仅做格式校验；有类型的配置同时检查字段、类型和必填项。

编辑器代码位于 `Assets/Scripts/UI/ConfigurationEditor/Editor/`，不进入客户端或服务器构建。

## 配置类型与加载

配置数据类型在 `Assets/Scripts/Game/Configuration/ConfigurationData.cs`，通过 `[Config(Path = "相对路径.json", Usage = ConfigUsage.Client/Server/Both)]` 声明文件及读取端。路径相对于 `Configs/`，类型需可实例化；新增元素和创建默认文件依赖无参构造函数，默认值写在字段初始化或构造函数中。根节点是数组时，配置类型继承 `List<T>`，文件就是 JSON 数组。字段命名、必填与可选的规则见 [配置文件规范](../Standards/Configuration.md)：`ConfigManager` 把成员名转换为小写加短横线（`MaxQuantity` → `max-quantity`），报错也使用文件中的字段名。

- `ConfigManager.Load<T>()` 负责读取并反序列化为数据对象；指定文件用 `LoadAtPath<T>()`，下载到的 JSON 用 `Deserialize<T>()`。它检查重复字段、未知字段、类型不匹配、数值溢出与 `JsonProperty.Required`，不判断端口范围、物品 ID 是否存在或任务奖励是否合法。可选字段缺失时保留构造默认值，明确写入的 `0`、`false` 和数组内容按文件读取。
- 业务层在加载之后显式校验：网络配置通过 `ConfigurationValidation`，物品与商城通过 `GameCatalog.GetErrors()`，任务通过 `TaskCatalog.Build()`，奖励和抽奖通过各自规则。启动入口与 Editor 全量校验共用这些规则；新增业务规则应放在所属业务层并接入全量校验。
- Editor 使用相同序列化契约识别成员，但以 JSON 草稿保存，保留未知字段和原数字文本；不会用对象重新序列化整份文件来丢弃额外数据。业务校验失败可以修复后重试，不妨碍保存中间草稿。
- 每日任务的奖励写在 `reward` 字段，规则参数使用 `type` + `params` 的位置参数格式，`type` 在 C# 中声明为枚举、在 JSON 中保存枚举名。Editor 为各位置参数、条件与子目标提供类型信息，任务工厂按同样的字段检查读取这些参数并校验规则。
- 构建时 `ConfigurationLinker` 为注册类型及成员生成保留声明，供 IL2CPP 裁剪使用；声明文件位于 `Library/Configuration/`。

## 客户端网络配置

`Configs/Network/client-network.json`：

```json
{
  "server-host": "127.0.0.1",
  "server-port": 7777,
  "use-ssl": false,
  "connect-timeout-seconds": 10,
  "resource-base-url": "https://www.aervento.top/games/poker/Content/v1"
}
```

| 字段 | 说明 |
| --- | --- |
| `server-host` | 域名、IPv4 或 IPv6，不含协议、端口和路径。 |
| `server-port` | 客户端连接的外部端口（1～65535），可与服务器监听端口不同（反向代理）。 |
| `use-ssl` | `true` 用 WSS，`false` 用 WS。HTTPS 页面上的 WebGL 必须用 WSS。 |
| `connect-timeout-seconds` | 建立连接的超时（1～300 秒），不影响牌局倒计时。 |
| `resource-base-url` | 远程内容的根地址（不含平台名）；写 `""` 时 WebGL 使用页面目录下的 `Content/v1`。Editor 选择远端内容来源时也读这里，见 [远程资源](RemoteArtwork.md)。 |

- Windows 默认读取可执行文件旁的该文件，可用 `-pokerClientConfig <路径>` 指定。
- WebGL 相对于当前页面 URL 读取（支持部署在子目录），带随机查询参数避免缓存；Web 服务器应对该文件返回 `Cache-Control: no-store`，并且不能被单页应用的回退规则改写成 `index.html`。读取失败时显示“暂时无法连接服务”，可以重试。
- 这是公开文件，不能包含任何密码、令牌或证书。发布新版游戏时保留线上已有的这份文件。

## 服务器网络配置

`Configs/Network/server-network.json`（不会进入 WebGL 包）：

```json
{
  "listen-port": 7777,
  "use-ssl": false,
  "path": "",
  "password": "",
  "max-connections": 100
}
```

| 字段 | 说明 |
| --- | --- |
| `listen-port` | 监听端口。 |
| `use-ssl` | 是否由游戏进程提供 TLS（固定 TLS 1.2）。 |
| `path` / `password` | 含私钥的 PFX/P12 证书的绝对路径与密码。两个字段始终要写；`use-ssl` 为 `false` 时写 `""`。 |
| `max-connections` | 服务器总连接数上限（含大厅与旁观），不是单桌人数。 |

- 启动时校验证书存在、可解密、含私钥且在有效期内。网络、物品、商城、初始发放、每日任务或抽奖配置缺失、格式错误、证书不可用或监听失败时，日志写明出错的文件、条目和字段，然后以退出码 1 退出，不会退回非加密监听。失败原因同时直接写到 stderr 并立即刷新，systemd 下用 `journalctl -u <服务名> -n 50` 查看。
- 默认读取可执行文件旁的 `Configs/Network/server-network.json`。用 `-pokerServerConfig <路径>` 改读其他位置（如服务器私有目录）时，该文件必须存在；证书与密码不要提交到仓库。
- 提供 WSS 的两种方式：游戏进程直接加载证书（`"use-ssl": true`）；或由反向代理终止 TLS 并转发 WebSocket（客户端 `"server-port": 443`、`"use-ssl": true`，服务器 `"use-ssl": false` 监听内网端口）。

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
4. 客户端与服务器使用同一版本的网络协议，需一起发布，不能与旧客户端混用。当前需要数据库 schema 17：首次启动时自动把旧的名片、牌面所有权表并入 `account_items`、改写装备记录并删除旧表（schema 15），并把已分配的每日任务改为只保存任务 ID（schema 16），新增抽奖保底计数表 `slot_pity`（schema 17）。**升级前必须备份数据库**，此迁移不可逆。服务器的 `Configs/Items/`、`Configs/Shop/`、`Configs/Accounts/` 随 Linux 构建复制（已存在的文件不会被覆盖），线上修改物品或商品时直接编辑服务器上的这些文件；从旧版升级时需要换成新的目录结构，旧的 `Configs/Shop/Items.json` 等四个文件不再读取。所有配置文件（包括 `Configs/Network/` 和 `Configs/DailyTasks/`）都改用小写加短横线的字段名，旧格式会被启动校验拒绝，服务器上的配置必须与程序一起替换；WebGL 页面旁的 `client-network.json` 也要同时换成新格式。

## 数据库

- SQLite schema 由服务器启动时自动升级；升级前按运维流程备份。
- 存储异常时服务器会停服，见 [房间与牌桌](../Systems/TableAndRooms.md) 的“存储故障”。
