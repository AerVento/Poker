# 名片

名片是玩家座位的外观：替换经典绿色底板，姓名、余额、倒计时和状态标识由 UI 绘制在图片的安全区内。名片只改变外观，不影响规则。新增或调整名片的流程见 [poker-cosmetics](../../.agents/skills/poker-cosmetics/SKILL.md)，美术要求见 [名片美术标准](../Standards/NamecardArt.md)。

## 商品配置

`Configs/Shop/Namecards.json`：数组顺序就是商城顺序，默认款 `default` 必须在第一位。修改后重启服务器生效（总体规则见 [货币、商店与物品](Economy.md)）。

| 字段 | 说明 |
| --- | --- |
| `Id` | 永久唯一，小写英文、数字、`_`、`-`，1～48 位。售出或发放后不能修改。 |
| `Name`、`Description` | 名称建议 2～6 个汉字，介绍一句话。只写主题，不写获取方式。 |
| `Artwork` | 远程地址 `namecards/<商品ID>`，指向 `Assets/RemoteContent/namecards/` 中的 `NamecardArtworkSO`（宽版 8:3 + 紧凑 4:3 两张图）。默认款不填，使用本地经典底板。 |
| `Price` | 美金，服务器最终核定。默认款免费。 |
| `Listed` | `false` 时未拥有者看不到，已拥有者仍可装备（默认 `true`）。下架不要删除条目或改 ID。 |
| `Purchasable` | `false` 时仍然展示，按钮显示“不可购买”；拥有者照常可以装备（默认 `true`）。 |
| `Reward` | `true` 时禁止购买，只能通过成就奖励获得。 |

- 服务器只出售 `Listed && Purchasable && !Reward` 的款式，“不可购买”的款式不会被当作免费商品出售。
- 改名、改介绍、改价、上下架只改配置；换图重新打包远程内容，已下载的玩家回到大厅时更新。

## 当前特殊款式

| 款式 | ID | 获取方式 |
| --- | --- | --- |
| 同花顺 | `straight-flush` | 成就“同花打不打得过俘虏豪斯？”奖励（`namecard-straight-flush`） |
| 皇家同花顺 | `royal-straight-flush` | 成就“皇家同花顺”奖励 |
| 狱契之印 / 深狱领主 / 撒旦敕印 | `devil-service` / `archdevil-service` / `satan-service` | 不可购买，由服刑年数强制显示（见下） |

## 购买与装备

- 商城从大厅进入，名片页每页 4 款。购买是永久的，每款最多扣费一次；购买后不会自动装备。
- 装备需要离开房间且没有进行中的手牌。装备后，等待房间、牌桌上的其他玩家和旁观者都能看到。
- 名片所有权存在 `account_namecards`，不会出现在道具仓库里。统一物品 ID 为 `namecard-<ID>`，可由服务器发放（见 [货币、商店与物品](Economy.md)）。
- 代码：`Assets/Scripts/Network/Shop/NamecardShop.cs`、`Storage/SqliteAccountRepository.Shop.cs`、`NetworkRoom/NetworkRoom.Shop.cs`；界面 `Assets/Scripts/UI/ShopPanel/`。

## 显示

- `NamecardAppearance`（`Assets/Scripts/UI/Namecards/`）根据美术槽的实际宽高比，选择更接近的宽版或紧凑版并等比显示；缺少某个比例时使用另一张，两张都缺或 ID 未知时显示经典。目前 Player8 使用 4:3，Player4、Player16、等待房间和商城使用宽版。
- 远程美术按需下载，下载期间显示经典外观，失败时保持经典；已下载的旧版本先显示，新版本在后台下载后替换（见 [远程资源](../Operations/RemoteArtwork.md)）。
- 名片 ID 通过 `RoomInfo.Namecards` 公开给同房间玩家。

## 恶魔名片（服刑外观）

- 服刑年数 = 恶魔契约数 × 1 + 大恶魔契约数 × 10 + 撒旦契约数 × 100，按服务器当前库存计算（`ulong`）。
- 100 ≤ 年数 < 500 显示狱契之印，500 ≤ 年数 < 1000 显示深狱领主，年数 ≥ 1000 显示撒旦敕印。
- 达到门槛时，客户端（`InfernalNamecard.Resolve`）在等待房间、牌桌、旁观列表和富豪榜上强制显示对应名片，姓名改为鲜红色 `#FF1818` 加黑色描边。数据库中的装备记录不变，还款使年数低于门槛后恢复原外观。
- 服刑期间禁止送礼（见 [礼物](Gifts.md)）。
- 同房间玩家的年数通过 `RoomInfo.ServiceYears` 公开，排行榜条目也带有年数；不会公开其他库存。
