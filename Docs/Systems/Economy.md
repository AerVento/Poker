# 货币、商店与物品

## 两种货币

| 货币 | 用途 |
| --- | --- |
| 筹码 | 只用于牌桌：下注、奖池、结算、入座门槛（一个大盲） |
| 美金 | 牌桌以外的一切：商店、礼物、契约、每日任务与成就奖励 |

- 账户上的 `Dollars` 和 `Chips` 均为 `uint`；总资产 `TotalAssets = Dollars + Chips`（`ulong`），用于排行榜和各类“资产门槛”判断。
- 新账户的初始资金发放为美金。数据库在 schema 13 时把旧版单一余额一次性转换为美金。
- 界面上两者都用 `$`，以不同图标区分。

## 通用余额组件

`Assets/Prefabs/UI/Shared/BalanceView.prefab` 统一使用货币图标与 `$1,234` 格式；图标区分美金和筹码，不额外显示“美金余额”标题。大厅、房间列表、房间、牌局、商城、兑换及抽奖页面复用该组件。金额尚未取得时显示 `—`，大额金额自动缩小字号，保留完整数值。

`BalanceView` 可订阅 `GameSettings.WalletChanged` 自动显示登录账户余额；牌局、商城和兑换也可使用 `Present` 展示页面所接收的权威快照。账户登录、退出及货币变化后统一通知，`SetWallet` 在两种金额都更新后仅通知一次。

## 兑换

- 美金与筹码 1:1 双向兑换，入口在大厅和牌桌。
- 本手仍在参战且未弃牌时不能兑换（包括已全下）；弃牌、旁观、两手之间和等待房间时可以兑换。可兑换的筹码 = 账户筹码 − 本手已投入的筹码。
- 手牌结算按“账户当前筹码 + 奖金 + 退款 − 投入”入账，因此弃牌后的兑换会被保留。
- 付款时美金不足：服务器返回 `ConversionRequired` 和精确差额；玩家确认后，客户端带着授权的差额重新提交，兑换与付款在同一个事务中完成。差额在确认期间变化时返回 `QuoteChanged`，需要重新确认。

代码：`Assets/Scripts/Network/Accounts/Wallet.cs`、`Storage/SqliteAccountRepository.Wallet.cs`，网络处理 `NetworkRoom/NetworkRoom.Wallet.cs`，界面 `UI/ChipExchangePanel/`。

## 物品

游戏里所有可拥有的东西都是**物品**：普通物品、礼物、契约、名片、牌面。物品表只回答“有哪些物品”；名片、牌面、礼物、契约等功能目录引用物品，赋予它们额外的角色与美术；商城表决定哪些物品可以购买。

### 配置

全部以 JSON 为唯一来源，直接编辑文件、重启服务器生效，不需要重新构建页面或服务端。服务器启动时读取并校验所有文件，**一次列出全部问题**（文件、条目、字段），任何错误都会阻止启动；登录成功前，服务器把物品与商城目录（`GameCatalogMessage`）发给客户端，商城、仓库、礼物菜单、名片、牌面和奖励名称都以它为准。

| 文件 | 作用 | 字段 |
| --- | --- | --- |
| `Configs/Items/Items.json` | 系统里有哪些物品 | `id`、`name`、`description`、`icon`、`consumable`、`max-quantity` |
| `Configs/Items/Namecards.json` | 哪些物品是名片，美术在哪 | `item-id`、`artwork` |
| `Configs/Items/CardFaces.json` | 哪些物品是牌面，美术在哪 | `item-id`、`cards`、`preview` |
| `Configs/Items/Gifts.json` | 哪些物品可以在牌桌上送出，播放什么特效；数组顺序就是礼物菜单顺序 | `item-id`、`effect` |
| `Configs/Items/Contracts.json` | 恶魔契约的签约、赠款、还款与刑期，以及服刑名片 | 见下文“恶魔契约” |
| `Configs/Shop/Shop.json` | 商城可以卖哪些物品；数组顺序就是商城顺序 | `item-id`、`price`、`listed`、`purchasable` |
| `Configs/Accounts/StarterGrants.json` | 新账号创建时发放的美金、筹码和物品 | 奖励条目数组（见下文“统一奖励”） |

字段名与必填规则见 [配置文件规范](../Standards/Configuration.md)。表中每个对象都必须写全所列字段：缺字段、多字段或类型不对都会在启动校验中报错。空值也要显式写出，如默认名片的 `"artwork": ""`、经典牌面的 `"cards": ""`、没有介绍时的 `"description": ""`。

`Items.json` 字段：

| 字段 | 说明 |
| --- | --- |
| `id` | 永久唯一：小写英文、数字、`_`、`-`，1～64 位。名片以 `namecard-`、牌面以 `cardface-` 开头，新增时保持这个前缀。 |
| `name`、`description` | 名称（不能为空）与介绍。名片、牌面的名称与介绍也写在这里。 |
| `icon` | 图标名，即 `Assets/Sprites/Items/` 下的文件名（Sprite Mode 必须为 Single）。所有物品都必须有图标，名片、牌面图标规范见 [UI 规范](../Standards/UIConvention.md)。所有图标打进公共远程包 `icons/items`，下载前显示占位图形。 |
| `consumable` | 是否可消耗。只有可消耗物品能被消耗。 |
| `max-quantity` | 持有上限；不可消耗物品必须为 1，可消耗物品不限量时写 `4294967295`。 |

`Shop.json` 字段：在表里才有资格出现在商城。`"listed": false` 下架（条目保留，改回 `true` 即重新上架）；`"purchasable": false` 在商城显示但不能购买（如成就奖励的名片）；正常出售时两者都写 `true`。不在表里的物品只能通过奖励或发放获得。已拥有的物品总在仓库、名片页和牌面页显示，不受商城配置影响。

校验规则：

- ID 合法且唯一，名称与图标非空；不可消耗物品的 `max-quantity` 必须为 1。
- 每个物品最多承担一种角色（名片、牌面、礼物、契约之一）。名片、牌面必须不可消耗、以对应前缀开头，带前缀的物品也必须出现在对应目录；礼物和契约必须可消耗。
- `Namecards.json` 必须包含 `namecard-default`、`CardFaces.json` 必须包含 `cardface-default`，二者使用页面内的本地美术，不填远程地址；其余名片、牌面必须填写远程地址，牌面必须填写预览名。
- 商城、功能目录、服刑名片和初始发放引用的物品都必须存在；初始发放的物品数量不能超过持有上限。
- 菜单 `Poker/Validate Server Configuration` 在 Editor 中执行与服务器相同的校验（还包括每日任务与筹码抽奖），构建前也会自动执行。
- 玩家可能拥有的物品不能从 `Items.json` 删除；不再出售时在 `Shop.json` 写 `"listed": false`。服务器启动时若发现库存中有物品表里没有的 ID，写入警告日志。

代码：`Assets/Scripts/Game/Items/GameCatalog.cs`（数据与校验）、`Assets/Scripts/Network/Config/CatalogConfiguration.cs`（读取）、`Assets/Scripts/Network/Config/ServerConfiguration.cs`（启动时统一校验所有服务器配置）、`Assets/Scripts/Network/Shop/ItemRules.cs`（服务器规则）、`Assets/Scripts/Network/Shop/ClientCatalog.cs`（客户端目录）。

### 拥有、装备与默认物品

- 所有物品（包括名片、牌面）都保存在同一张表 `account_items`，一行一个物品和数量；“玩家有没有某物品”只查这一张表。
- 名片、牌面可以装备，装备时只需拥有该物品；装备值保存物品 ID，空值表示该栏位的默认款。牌局中（包括已加入房间）不能更换外观。
- 默认名片与经典、简约牌面不再是代码里写死的“天生拥有”，而是由 `StarterGrants.json` 在新账号创建时发放。修改初始发放只影响之后创建的账号；要给老账号补发，使用服务器发放或奖励。

### 购买

- 服务器校验报价与目录版本、上架与可购买状态、持有上限和购买条件，扣款、入库、记账和回执在一个事务中提交。每个请求带操作 ID，同一 ID 重试返回原结果；同一 ID 改变内容会被拒绝。
- 没有购买效果的可消耗物品可以一次购买多个，其他物品每次 1 个；持有上限为 1 的物品已拥有时不能再买。
- 购买没有房间限制。

### 消耗

只有 `consumable` 为 `true` 的物品能被消耗。消耗方式由功能目录绑定，客户端统一发送 `ItemConsumeRequest`：“消耗物品 X，方式 M，附带 M 需要的参数”。

| 方式 | 绑定目录 | 参数 | 规则 |
| --- | --- | --- | --- |
| 使用（`Use`） | `Contracts.json` | 无，每次 1 个 | 仓库里只有绑定了“使用”的物品才显示“使用”按钮；契约的使用就是还款，任何地点都可以使用 |
| 送出（`Give`） | `Gifts.json` | 目标座位、数量；一次请求可合并多条 | 只能在已开始的牌局中送给其他在座玩家；库存不足时按商城价补买；服刑期间不能送礼，见 [礼物](Gifts.md) |

- 可消耗但没有绑定任何方式的物品仍可拥有，只是暂时没有地方消耗。礼物在仓库里照常显示，标注“牌局中送出”。
- 新增消耗方式时，在 `ItemConsumeMethod` 中追加取值，由新的功能目录绑定物品，并在 `SqliteAccountRepository.ConsumeItems` 中实现该方式的规则；不要在网络处理器或存储代码里按物品 ID 分支。

### 恶魔契约

`Contracts.json` 由两部分组成：

```json
{
  "contracts": [
    { "item-id": "devil-contract", "sign-below-assets": 1000, "payout": 1000, "repayment": 1100, "service-years": 1 }
  ],
  "service-namecards": [
    { "min-service-years": 100, "namecard-item-id": "namecard-devil-service" }
  ]
}
```

| 字段 | 说明 |
| --- | --- |
| `sign-below-assets` | 总资产低于此值才能签约（购买） |
| `payout` | 签约时获得的美金，也是每份契约计入净资产成就的欠款 |
| `repayment` | 使用一份契约（还款）支付的美金 |
| `service-years` | 每份持有的契约计入的刑期年数 |
| `min-service-years`、`namecard-item-id` | 刑期达到门槛后公开显示的服刑名片；达到最低门槛即为“服刑中”，不能自选名片、不能送礼 |

当前配置：

| 物品 | ID | 签约条件 | 签约获得 | 还款 | 刑期 |
| --- | --- | --- | ---: | ---: | --- |
| 恶魔契约 | `devil-contract` | 总资产 < $1,000 | $1,000 | $1,100 | 1 年 |
| 大恶魔契约 | `archdevil-contract` | 总资产 < $10,000 | $10,000 | $11,000 | 10 年 |
| 撒旦契约 | `satan-contract` | 总资产 < $100,000 | $100,000 | $110,000 | 100 年 |

契约在 `Shop.json` 中价格为 0，可以多次签约。持有契约计为“刑期”，会改变公开名片外观（见 [名片](Namecards.md)）并计入排行榜的服役榜；服役榜的三列依次显示 `Contracts.json` 前三份契约的持有数。

## 服务器发放

- 可信的服务端代码通过 `NetworkRoom.GrantItem(accountId, itemId, operationId, quantity)` 发放物品：不扣款、不自动装备，离线玩家也能收到。`operationId` 用 `Guid.ToString("N")`，重试必须复用。

| 情况 | 结果 |
| --- | --- |
| 首次成功 | `Success`，`IsNewlyApplied = true` |
| 同一操作重试 | 返回保存的原结果 |
| 同一操作 ID 但物品或数量不同 | `InvalidRequest` |
| 已拥有的、上限为 1 的物品 | `AlreadyOwned` |
| 未注册 / 账户不存在 / 已达到可堆叠物品上限 | `UnknownItem` / `AccountNotFound` / `QuantityLimit` |

- 未满额物品按剩余容量发放。
- 发放不会触发购买条件和购买效果。例如直接发放契约不会附带赠款。
- 发放接口自己开启事务，不要在另一个已打开的 SQLite 写事务里调用。成就、任务和抽奖在各自事务中调用共同的 `ApplyReward`，不能嵌套调用另起事务的发放接口。

## 商城与仓库界面

商城与仓库共用 `ShopPanel`，以 `ShopPageMode` 区分，页签为“道具 / 名片 / 牌面”，三个页签共用同一份库存快照和同一条请求通道，同一时间只处理一个购买、装备或使用请求。道具页显示名片、牌面以外的物品，每页 6 格；仓库只显示持有数量大于零的物品，目录中缺少定义的旧库存显示 ID 和数量。所有操作都需要确认，请求期间禁止重复提交，结果以服务器快照为准。代码在 `Assets/Scripts/UI/ShopPanel/`。

## 统一奖励

`Game/Economy/Reward.cs` 定义公共 `Game.Reward` 和 `RewardEntry`，成就、每日任务、抽奖和初始发放的配置、回执、网络响应和展示都使用它。配置文件中奖励写成条目数组，每条包含 `kind`（`Dollars` / `Chips` / `Item`）和正整数 `quantity`；只有物品填写 `item-id`，即 `Items.json` 中的物品 ID（名片、牌面也是如此，如 `namecard-galaxy`）。同一份奖励中的同类条目会合并；一份奖励最多 100 种条目，抽奖奖项最多 8 种以限制十连抽回执大小。

```json
[
  { "kind": "Dollars", "quantity": 1000 },
  { "kind": "Item", "item-id": "cardface-gensokyo", "quantity": 1 }
]
```

- 物品上限来自当前 `Items.json` 的 `max-quantity`（已完成奖励的内容快照不冻结持有上限），名片、牌面固定为 1。只发到上限：已有 98、上限 100、奖励 5 时发 2；满额时发 0，其他奖励继续。超出的部分不保留、不补偿、不重抽；任务和成就照常标记已领取。抽奖在抽取前就排除会超出上限的奖励，见 [持有上限与已拥有奖励](SlotMachine.md#持有上限与已拥有奖励)。
- 奖励发放与 `Shop.json` 的 `listed`、`purchasable` 无关。两个开关只控制商城可见性与购买，服务端购买必须同时满足两者。
- `SqliteAccountRepository.Rewards.cs` 负责共同入账：校验、上限裁剪、物品入库、钱包流水和事实事件。调用方将扣费、领取状态、操作回执一起提交，并负责重复操作去重。不能由客户端指定获奖内容。
- 未知物品或货币溢出仍拒绝整份奖励，已开始写入的抽奖整批回滚。筹码奖励不能修改进行中牌局的筹码快照，须等该手结束后领取；美金与物品奖励不受此限制。
- 成功回执和奖励弹窗只包含实际发放内容。抽奖保留每一抽的独立结果；满额导致没有新物品时显示已达上限。
- 数据库中的奖励以 `{"Entries": [...]}` 保存。旧成就的 `RewardChips/RewardItems` 和抽奖单条奖励回执保持可读；旧任务回执的 `Chip/Items` 在数据库升级到第 16 版时改写为新格式。历史 Chip 金额按美金处理。
- 此协议不能与旧客户端混用：同时部署服务端、客户端及 `Configs/Items`、`Configs/Shop`、`Configs/Accounts`。旧格式的配置（大驼峰字段名、带 `Entries` 的奖励）会被配置校验拒绝。
