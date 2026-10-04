# 成就

成就完全在服务器判定。业务模块在自己的事务里写入“事实事件”，成就服务读取事件、更新进度并保存奖励快照；客户端只收到展示所需的目录与状态。新增成就的流程见 [poker-achievements](../../.agents/skills/poker-achievements/SKILL.md)。

## 代码位置

| 部分 | 位置 |
| --- | --- |
| 成就基类、上下文、事件路由 | `Assets/Scripts/Game/Achievements/` |
| 具体成就（按类别分目录） | `Assets/Scripts/Game/Achievements/Instances/{General,Hand,Special,Wealth}/` |
| 可复用的规则基类（牌型、盈利、净资产、刑期、换装） | `Assets/Scripts/Game/Achievements/Rules/` |
| 事件模型与来源注册 | `Assets/Scripts/Game/Events/` |
| 进度、领奖、目录投影 `AchievementService`；客户端缓存 | `Assets/Scripts/Network/Achievements/` |
| 网络处理 | `Assets/Scripts/Network/NetworkRoom/NetworkRoom.Achievements.cs`、`NetworkRoom.AchievementCache.cs` |
| 面板、列表行、横幅 | `Assets/Scripts/UI/AchievementPanel/` |
| 图标 | `Assets/Resources/UI/Achievements/`，美术规范见 [图标规范](../../.agents/skills/poker-achievements/references/icon-style.md) |

## 定义一个成就

在 `Instances/<类别>/` 下新增一个 public 类，使用无参构造。运行时自动发现所有成就类，`AchievementLinker` 在构建时保留它们（IL2CPP 裁剪），无需改注册表、网络消息或 prefab。

| 字段 | 说明 |
| --- | --- |
| `Id` | 永久身份：小写字母、数字、`_`、`-`，最长 64。发布后不能修改。 |
| `Name` / `Description` | 名称与简短描述，用于列表和解锁横幅。 |
| `Category` | `AchievementCategories` 中的通用、特殊、牌型、财富；返回新名称会自动多一个页签。 |
| `IconPath` | Resources 路径，不带扩展名，如 `UI/Achievements/cards`。缺失时使用 `default`。 |
| `SortOrder`、`Target` | 列表顺序与目标进度（默认 1）。 |
| `Reward` | 公共 `Game.Reward`，包含 `Entries`（美金、筹码、物品）。`new Reward(1000, items)` 是美金加物品的便捷构造。见 [统一奖励](Economy.md#统一奖励)。 |
| `Hidden` | 为 `true` 时未解锁前只显示遮蔽信息。 |

判定写法二选一：

- **牌局成就**：继承 `HandAchievement`，覆写 `Evaluate(AchievementContext, accountId, progress)`。上下文是本手截至当前时点的不可变快照：参赛者（底牌、最佳牌型、投入、弃牌/all-in/断线、奖金与退款）、各街快照 `Streets`、有效行动 `Actions`、结算奖池 `Pots`（主池/边池的争夺者与赢家）以及是否摊牌 `Showdown`。
- **其他成就**：继承 `Achievement`，实现一个或多个 `IAchievementHandler<TEvent>`。

定义实例被所有玩家共享，不能在类字段里保存玩家状态；分步状态写进 `progress.Data`。达到 `Target` 后自动完成，之后不再判断。

## 事件来源

| 来源 / 类型 | 事件 | 内容 |
| --- | --- | --- |
| `table/stage` | `HandStageRecordedEvent` | 翻牌前、翻牌、转牌、河牌和结算时的完整快照 |
| `table/action` | `HandActionRecordedEvent` | 一次有效行动：行动前筹码、本次投入、操作类型、是否断线自动操作 |
| `table/revealed` | `HandCardsRevealedEvent` | 对手全部弃牌后获胜者主动亮牌（只用于补充牌型类成就） |
| `shop/purchased` | `ItemPurchasedEvent` | 购买物品、名片或牌面 |
| `inventory/used`、`inventory/changed` | `ItemUsedEvent`、`InventoryChangedEvent` | 使用物品（含礼物）、库存变化后的快照 |
| `account/assets` | `AccountAssetsChangedEvent` | 美金、筹码与契约库存快照 |
| `collection/equipped` | `EquipmentChangedEvent` | 名片或牌面的实际装备变化 |

- 事件只在业务成功的同一个 SQLite 事务中写入（通过 `TransactionEventWriter`）；失败、重复请求和“重选同一款式”不产生事件。
- 事件 ID 由来源、账号和业务操作 ID 组成，重试必须沿用同一操作 ID。成就服务在另一个事务里消费事件并记录每个账号的消费回执，因此重启后可以继续处理、不会重复计数。
- 新增事件类型：在 `Game/Events` 加不可变的事件类，在对应来源的 `Register` 中注册稳定的来源 ID、类型 ID 和版本；已发布事件的结构变化要加新版本并保留旧版本的读取。
- 新成就只处理加入之后的事件，不回溯历史。

## 保存与领奖

- 进度按 `AccountId + AchievementId` 永久保存，不重置。完成时保存奖励快照，之后修改奖励不影响已获得的待领奖励。
- 登录后在任何页面都能领取（包括牌局中），当前成就货币奖励进入美金余额。奖励、流水和领取状态原子提交；货币溢出时整笔失败并保留待领状态。物品只发剩余容量，满额物品跳过，其余照常发放；若配置筹码奖励，进行中的牌局内暂不可领取该奖励。
- 领取成功后通过通用 `RewardPopupPanel` 展示美金与物品，物品框默认显示图标与数量，点击查看名称和描述；单次和批量领取共用此组件，见 [通用奖励展示](SlotMachine.md#通用奖励展示)。
- 面板每个页签底部的“一键领取”在一个事务里领取该类别全部可领奖励，“全部”页签覆盖所有类别；服务器重新筛选，不信任客户端传来的 ID。

## 客户端展示与缓存

- 列表顺序：已完成未领取 → 已完成已领取 → 普通未完成 → 隐藏未完成，同组按目录顺序。
- 列表右侧奖励逐项复用 `RewardItemView`，显示图标与数量，点击查看名称和说明。奖励区固定容纳两格，超过两份时横向滚动，并显示滚动条，不因奖励数量撑高列表项。未解锁的隐藏成就仍显示 `??`，不展示奖励详情。
- 未解锁的隐藏成就只发送类别和遮蔽内容（“隐藏成就”“继续游戏以解锁”、`hidden` 图标、`?? / ??`）；解锁后随状态附带真实定义。
- 解锁横幅按顺序排队，显示在不拦截输入的系统层；断线前没有确认的横幅会在重新登录后补发。
- 目录与玩家状态分开传输、分开缓存（在 `NetworkRoom` 内存中，不写磁盘）：
  - 目录首次打开面板时分页获取，同一服务器内一直复用。目录版本号是全部定义内容的 SHA-256，服务器定义变化后客户端会重新下载。
  - 状态在登录时获取一次。普通进度变化只推送一个“已过期”标记，下次打开面板或手动刷新时才重新请求；解锁通知则立即刷新；领奖回包直接合入该项状态。
  - 分页每页最多 32 项；换账号或断线时清空账号状态，旧请求的响应会被丢弃。

## 判定口径

以下是容易误判的规则，修改相关成就前先读：

- **盈利**：净盈利 = 奖池奖金 + 未跟注退款 − 本手总投入。累计盈利只累加正数；单手盈利记录历史最高值。成就奖励等非牌局收入不算盈利。
- **净资产**：美金 + 筹码 − 契约欠款（每份按 `Contracts.json` 中该契约的 `payout` 计，不是还款额 `repayment`）。未完成前进度随当前资产变化，完成后不撤销。
- **刑期**：按当时持有的契约乘以各自的 `service-years`（`Contracts.json`）计算，进度取观察到的最高值，还款不撤销。
- **换装**：以服务器保存的装备实际变化为准，换到默认款（保存为空值，事件里记作 `default`）或重选同款不算；刑期导致的强制外观不算换装。
- **摊牌牌型**：要求最终摊牌且未弃牌，不要求获胜；只认最佳五张牌的准确类别，不向下连带。对手全弃牌后主动亮牌，只补充牌型类成就，不计入胜利、盈利等。
- **行动类**（如“AK不梭是棒槌”“一掷成灰”）：只认玩家主动的有效行动，盲注耗尽和断线自动操作不算；行动成就一经达成，之后牌局中止也不撤销。
- **奖池比较类**（“葫芦之上”“大鱼吃小鱼”“这也能行？”“史上最强vs现代最强”“虚张声势”）：只在同一个实际获胜奖池内比较有效争夺者，不能跨边池拼凑；只拿到退款不算赢池。
- **牌型等级差**（诈唬类“高出至少两级”）按牌型类别比较，不比较点数或踢脚。
- **皇家同花顺**：在翻牌、转牌、河牌公开时立即判断，当时未弃牌即可，不要求摊牌。
