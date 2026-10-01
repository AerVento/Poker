# Network 目录说明

本目录是服务端权威逻辑与 Mirror 通信。命名空间统一为 `Game.Network`，`Accounts`、`Shop`、`Gifts`、`Leaderboard` 下的既有类型沿用 `Game.Network.Accounts`，`Tasks`、`Achievements` 下的沿用各自已有命名空间；子目录不再新建命名空间。

## 子目录

| 目录 | 内容 |
| --- | --- |
| `NetworkRoom/` | `NetworkRoom`（继承 `NetworkManager`）及其按功能拆分的 partial 文件，负责注册处理器、广播和客户端回调；`ClientSnapshotCache` 等客户端缓存 |
| `Rooms/` | 房间与座位（`RoomManager`、`RoomInfo`）、在线会话（`PlayerManager`）、离座与参战（`RoomDepartureService`、`TableParticipationService`） |
| `Holdem/` | 服务端牌局 `GameManager` 及其 partial、完整状态 `ServerGameInfo`、按玩家裁剪的 `ClientGameInfo`、结算结果与操作类型 |
| `Messages/` | 网络协议消息，以及复杂字段的读写方法 `Serialization.cs` |
| `Config/` | 网络配置，以及 `Configs/Shop` 商品配置的读取与校验（`ShopConfiguration`） |
| `Accounts/` | 登录（`AccountAuthenticator`、`AccountService`、`PasswordHasher`）、账户与钱包模型、仓储接口 |
| `Accounts/Storage/` | `SqliteAccountRepository` 与 `GuardedAccountRepository`，按功能拆成 partial 文件 |
| `Shop/` | 道具、名片、牌面商店，购买与使用策略，物品发放；客户端商品目录 `ClientShopCatalog` |
| `Gifts/`、`Leaderboard/`、`Tasks/`、`Achievements/` | 对应功能的服务端逻辑与仓储接口 |

纯规则（`HoldemRules`、`PotCalculator`、牌型比较）放在 `Game/Poker`，不要放进本目录。

## 约束

- **身份**：业务处理器用 `RegisterServerHandler<T>(..., requireAuthentication: true)` 注册，玩家名通过 `TryGetAuthenticatedPlayer(conn, ...)` 或 `PlayerManager` 的会话取得。消息里携带的玩家名、座位号只能用来和会话核对，不能直接信任。
- **协议**：消息字段是复杂类型（类、嵌套结构、集合）时，直接声明为该类型字段，并在 `Messages/Serialization.cs` 中添加对应的 `WriteXxx/ReadXxx` 方法。不要再用 `RawData` 字符串加属性的写法。改动字段后按 [poker-network-messages](../../../.agents/skills/poker-network-messages/SKILL.md) 检查两端。
- **信息泄漏**：发给客户端的牌局数据只能来自 `ClientGameInfo`，不得包含其他玩家底牌、牌堆或仅服务端可见的状态。
- **连接状态**：为每个连接保存的字典（请求序号、缓存等）必须在 `NetworkRoom.OnServerDisconnect` 中清理，停服时在 `OnStopServer` 中清空。
- **存储**：业务代码通过 `GuardedAccountRepository` 访问存储，意外的存储异常会触发停服保护。新增持久化功能时，在 `Accounts/Storage/` 为两个仓储各加一个同名 partial 文件，并在对应功能目录定义仓储接口。
- **广播**：一次广播中所有接收者共用的数据（如名片、服役年数）只查询一次，不要在每个接收者的循环里查数据库。
- **日志**：只记录低频的生命周期事件（开房、入离房、断线、超时自动操作、错误）。不要按每次操作、每次同步写日志，也不要输出完整牌局状态。
- **异步流程**：`await` 之后先确认服务器、房间和牌局仍是等待前的那一个（参照 `_serverLifecycleVersion` 与 `HandId` 的检查），再继续推进。
- **错误码**：新增结果类型用 `enum`，不要再新增魔法数字返回码。
