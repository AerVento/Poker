# 名片

名片是玩家座位的外观：替换经典绿色底板，姓名、余额、倒计时和状态标识由 UI 绘制在图片的安全区内。名片只改变外观，不影响规则。新增或调整名片的流程见 [poker-cosmetics](../../.agents/skills/poker-cosmetics/SKILL.md)，美术要求见 [名片美术标准](../Standards/NamecardArt.md)。

## 配置

名片是物品，配置分三处（总体规则见 [货币、商店与物品](Economy.md)），修改后重启服务器生效：

| 文件 | 内容 |
| --- | --- |
| `Configs/Items/Items.json` | 物品本身：`id`（`namecard-<名片ID>`，售出或发放后不能修改）、`name`（建议 2～6 个汉字）、`description`（一句话，只写主题，不写获取方式）、`icon`。名片不可消耗。 |
| `Configs/Items/Namecards.json` | 让物品成为名片：`item-id` 与远程地址 `artwork`（`namecards/<名片ID>`，指向 `Assets/RemoteContent/namecards/` 中的 `NamecardArtworkSO`，宽版 8:3 + 紧凑 4:3 两张图）。数组顺序就是名片页顺序。默认款 `namecard-default` 写 `"artwork": ""`，使用本地经典底板。 |
| `Configs/Shop/Shop.json` | 出售条件：`price`、`listed`（`false` 时未拥有者看不到）、`purchasable`（`false` 时展示但按钮显示“不可购买”）。不在表里的名片只能通过奖励获得。 |

- 服务器只出售 `listed` 与 `purchasable` 都为 `true` 的款式，“不可购买”的款式不会被当作免费商品出售。已拥有的名片不受商城配置影响，总能装备。
- 改名、改介绍、改价、上下架只改配置；换图重新打包远程内容，已下载的玩家回到大厅时更新。

## 获取方式

有哪些款式以 `Configs/Items/Namecards.json` 与 `Items.json` 为准，文档不逐款列出。名片的获取方式由配置组合决定：

| 获取方式 | 配置 |
| --- | --- |
| 新账号初始发放 | `Configs/Accounts/StarterGrants.json`；`namecard-default`（经典）必须存在，使用页面内本地美术，装备记录为空时显示 |
| 商城购买 | `Shop.json` 中 `listed`、`purchasable` 都为 `true` |
| 成就奖励 | 成就定义中的奖励条目（见 [成就](Achievements.md)）；通常在 `Shop.json` 写 `"listed": true`、`"purchasable": false`，商城展示但不出售 |
| 抽奖 | `Configs/SlotMachine/Lottery.json` 的奖励条目；不在 `Shop.json` 中的名片只能从奖励获得（抽奖限定） |
| 服刑外观 | `Contracts.json` 的 `service-namecards`，由服刑年数强制显示，不需要拥有（见下文“恶魔名片”） |

## 购买与装备

- 商城从大厅进入，名片页每页 4 款。购买是永久的，每款最多扣费一次；购买后不会自动装备。
- 装备需要离开房间且没有进行中的手牌。装备后，等待房间、牌桌上的其他玩家和旁观者都能看到。
- 名片与其他物品一样存在 `account_items`，显示在名片页而不是道具页；可由服务器发放（见 [货币、商店与物品](Economy.md)）。装备记录保存物品 ID，空值表示经典款。
- 代码：购买、装备走统一的物品请求（`NetworkRoom/NetworkRoom.Items.cs`、`Storage/SqliteAccountRepository.Items.cs`）；房间内的名片公开在 `NetworkRoom/NetworkRoom.Namecards.cs`；界面 `Assets/Scripts/UI/ShopPanel/`。

## 显示

- `NamecardAppearance`（`Assets/Scripts/UI/Namecards/`）根据美术槽的实际宽高比，选择更接近的宽版或紧凑版并等比显示；缺少某个比例时使用另一张，两张都缺或 ID 未知时显示经典。目前 Player8 使用 4:3，Player4、Player16、等待房间和商城使用宽版。
- 登录成功取得私有库存快照和物品目录后，`EquippedArtwork` 主动下载并加载当前实际显示的名片（包含服刑外观），更换装备或断线时释放。其他玩家及浏览页面中的名片仍按需下载；加载未完成或失败时显示经典外观。已下载的旧版本先使用，新版本在后台下载后替换（见 [远程资源](../Operations/RemoteArtwork.md)）。
- 装备的名片物品 ID 通过 `RoomInfo.Namecards` 公开给同房间玩家。

## 恶魔名片（服刑外观）

- 服刑年数 = Σ 每种契约的持有数 × 该契约的 `service-years`，按服务器当前库存计算（`ulong`）；规则都在 `Configs/Items/Contracts.json`（见 [货币、商店与物品](Economy.md)）。
- `service-namecards` 按年数门槛决定显示哪款名片：取不超过当前年数的最高门槛；最低门槛就是“服刑中”的判定线。
- 达到门槛时，客户端（`GameCatalog.ResolveNamecard`）在等待房间、牌桌、旁观列表和富豪榜上强制显示对应名片，姓名改为鲜红色 `#FF1818` 加黑色描边。数据库中的装备记录不变，还款使年数低于门槛后恢复原外观。
- 服刑期间禁止送礼（见 [礼物](Gifts.md)）。
- 同房间玩家的年数通过 `RoomInfo.ServiceYears` 公开，排行榜条目也带有年数；不会公开其他库存。
