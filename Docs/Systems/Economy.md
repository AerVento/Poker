# 货币、商店与物品

## 两种货币

| 货币 | 用途 |
| --- | --- |
| 筹码 | 只用于牌桌：下注、奖池、结算、入座门槛（一个大盲） |
| 美金 | 牌桌以外的一切：商店、礼物、契约、每日任务与成就奖励 |

- 账户上的 `Dollars` 和 `Chips` 均为 `uint`；总资产 `TotalAssets = Dollars + Chips`（`ulong`），用于排行榜和各类“资产门槛”判断。
- 新账户的初始资金发放为美金。数据库在 schema 13 时把旧版单一余额一次性转换为美金。
- 界面上两者都用 `$`，以不同图标区分。

## 兑换

- 美金与筹码 1:1 双向兑换，入口在大厅和牌桌。
- 本手仍在参战且未弃牌时不能兑换（包括已全下）；弃牌、旁观、两手之间和等待房间时可以兑换。可兑换的筹码 = 账户筹码 − 本手已投入的筹码。
- 手牌结算按“账户当前筹码 + 奖金 + 退款 − 投入”入账，因此弃牌后的兑换会被保留。
- 付款时美金不足：服务器返回 `ConversionRequired` 和精确差额；玩家确认后，客户端带着授权的差额重新提交，兑换与付款在同一个事务中完成。差额在确认期间变化时返回 `QuoteChanged`，需要重新确认。

代码：`Assets/Scripts/Network/Accounts/Wallet.cs`、`WalletPayments.cs`、`Storage/SqliteAccountRepository.Wallet.cs`，网络处理 `NetworkRoom/NetworkRoom.Wallet.cs`，界面 `UI/ChipExchangePanel/`。

## 道具商店

### 配置

物品、礼物、名片、牌面四类商品都以 `Configs/Shop/` 中的 JSON 为唯一来源，直接编辑文件，重启服务器生效，不需要重新构建页面或服务端（有专属行为的商品除外，见下）。服务器启动时读取并校验，任何错误都会阻止启动；登录成功前，服务器把完整商品目录（`ShopCatalogMessage`）发给客户端，商城、收藏、礼物菜单、名片、牌面和奖励名称都以它为准。

`Items.json` 是物品定义数组，顺序就是商城顺序：

| 字段 | 说明 |
| --- | --- |
| `Id` | 永久唯一：小写英文、数字、`_`、`-`，1～48 位；不能使用 `namecard-`、`cardface-` 前缀。 |
| `Name`、`Description` | 名称（必填）与介绍。 |
| `Icon` | 图标名，即 `Assets/Sprites/Items/` 下的文件名（Sprite Mode 必须为 Single）；所有图标打进公共远程包 `icons/items`，下载前商城显示占位线条，礼物菜单显示默认图标。 |
| `Kind` | `Consumable`（消耗品）或 `NonConsumable`（永久收藏）。 |
| `Price`、`Listed` | 美金价格；`Listed: false` 下架（默认 `true`）。 |
| `MaxQuantity` | 可选持有上限；消耗品默认不限，永久收藏固定为 1。 |

- 校验内容：ID 合法且唯一、名称与图标非空、礼物引用的物品存在且为消耗品、名片和牌面以 `default` 开头、远程地址非空、代码引用的商品（三种契约、三款服刑名片）存在。`Poker/Shop/Validate Catalogs` 在 Editor 中执行同样的校验，构建前也会自动执行。
- 玩家可能拥有的商品不能从配置中删除，只能 `Listed: false`；服务器启动时若发现库存中有配置里没有的 ID，写入警告日志。
- 新增无专属行为的物品、礼物、名片、牌面只需配置和远程内容；契约等带购买与还款规则的物品、服刑名片、成就奖励的发放逻辑仍需修改代码并重新构建。

代码：`Assets/Scripts/Network/Config/ShopConfiguration.cs`（读取与校验）、`Assets/Scripts/Game/Economy/ShopDefinitions.cs`（数据与规则）、`Assets/Scripts/Network/Shop/ClientShopCatalog.cs`（客户端目录）。

### 购买与使用

- 购买：服务器校验报价与目录版本、上架状态、持有上限和购买条件，扣款、入库、记账和回执在一个事务中提交。每个请求带操作 ID，同一 ID 重试返回原结果；同一 ID 改变内容会被拒绝。礼物类消耗品可以一次购买多个，其他物品每次 1 个。
- 购买没有房间限制。
- 使用：只有消耗品可以使用，每个物品按注册的“使用策略”限定使用地点、条件和效果。契约还款可在任何地方使用；礼物只能在进行中的牌桌上使用（见 [礼物](Gifts.md)）。
- 物品的特殊规则按 ID 注册在 `Assets/Scripts/Network/Shop/ItemPurchaseRuntime.cs`（`ContractPurchaseRegistration`）：
  - 购买条件（`IPurchaseCondition`，如资产比较）与购买效果（`IPurchaseEffect`，如赠款）相互独立，由商品组合。
  - 使用策略（`ItemUsePolicy`）定义条件、效果与允许的地点。
  - 普通商品注册空策略；新增特殊效果必须在此注册，不能在网络处理器或存储代码里按 ID 分支。

### 恶魔契约

| 物品 | ID | 购买条件 | 购买时获得 | 还款（使用一份） | 刑期 |
| --- | --- | --- | ---: | ---: | --- |
| 恶魔契约 | `devil-contract` | 总资产 < $1,000 | $1,000 | $1,100 | 1 年 |
| 大恶魔契约 | `archdevil-contract` | 总资产 < $10,000 | $10,000 | $11,000 | 10 年 |
| 撒旦契约 | `satan-contract` | 总资产 < $100,000 | $100,000 | $110,000 | 100 年 |

契约是免费消耗品，可以多次签约。持有契约计为“刑期”，会改变公开名片外观（见 [名片](Namecards.md)）并计入排行榜的服役榜；净资产类成就会扣除契约欠款。

## 统一物品 ID 与服务器发放

- 普通物品使用自身 ID；名片和牌面映射为 `namecard-<名片ID>`、`cardface-<牌面ID>`（`Game.NamecardItemId`、`Game.CardFaceItemId` 提供双向转换）。普通物品不能占用这两个前缀，冲突时启动失败。
- 服务器加载各商店目录后建立 `ItemIdentityRegistry`，包括已下架的商品。
- 可信的服务端代码通过 `NetworkRoom.GrantItem(accountId, itemId, operationId, quantity)` 发放物品：不扣款、不自动装备，离线玩家也能收到。`operationId` 用 `Guid.ToString("N")`，重试必须复用。

| 情况 | 结果 |
| --- | --- |
| 首次成功 | `Success`，`IsNewlyApplied = true` |
| 同一操作重试 | 返回保存的原结果 |
| 同一操作 ID 但物品或数量不同 | `InvalidRequest` |
| 已拥有的永久物品 | `AlreadyOwned` |
| 未注册 / 账户不存在 / 超过上限 | `UnknownItem` / `AccountNotFound` / `QuantityLimit` |

- 发放不会触发购买条件和购买效果。例如直接发放契约不会附带赠款。
- 发放接口自己开启事务，不要在另一个已打开的 SQLite 写事务里调用。成就和任务奖励中的物品走各自的领奖事务。

## 商城与仓库界面

商城与仓库共用 `ShopPanel`，以 `ShopPageMode` 区分，页签为“道具 / 名片 / 牌面”。道具页每页 6 格；仓库只显示持有数量大于零的物品，目录中缺少定义的旧库存显示 ID 和数量。所有操作都需要确认，请求期间禁止重复提交，结果以服务器快照为准。代码在 `Assets/Scripts/UI/ShopPanel/`。
