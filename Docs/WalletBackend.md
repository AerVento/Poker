# 双货币后端（schema 13）

## 金额与迁移

- `PlayerAccount.Dollars` / `Chips` 分别为 uint 美金和筹码；`TotalAssets` 使用 ulong 求和。
- 升级在单一 SQLite 事务内恢复旧活动手，再把全部旧 Chips 转成 Dollars，Chips 归零，并推进 schema 版本。重复初始化不会再兑换。
- 旧 chip_ledger 不改写，追加迁移扣减腿；新 dollar_ledger 保存迁移入账与后续美金流水。旧成就/任务 Reward.Chip、RewardChips 等持久化字段保留，未领奖金额现在兑现为美金。
- 注册沿用现有初始金额配置参数，但发放美金。ItemPurchaseOutcome 新记录使用 FormatVersion 2（USD），旧版本 1 仍可读取。

## 兑换、支付与结算

数据库筹码采用手末净额结算：手中 `account.Chips` 仍包含已投入筹码。服务端在钱包和购买入口同步 GM 权威 `RoundBet`、`IsFold` 到参与者钱包许可，客户端不能提交这些值。

- 未弃牌的参战玩家禁止双向兑换，包括 all-in；弃牌、旁观、手间允许兑换。
- 可兑换筹码为账户筹码减去本手已投入部分。兑换与流水、请求凭据在同一事务内提交。
- 手末按 `account.Chips + award + refund - contribution` 结算。`HandSettlementBalance.Chips` 保持本手独立结果，`AccountChips` 返回包含弃牌后兑换的实际账户筹码。
- 商店/任务/成就/契约收支均为美金。购买及美金还款可在房间内执行；装备外观仍需离房，单独通过 CanEquip 投影。
- 不足美金时服务端返回 ConversionRequired 和 RequiredConversion。确认请求携带 AuthorizeChipConversion=true、AuthorizedShortfall=精确差额；差额变化则 QuoteChanged。兑换和购买/还款原子提交，成功操作按账户及 operationId 防重。
- 零资产契约与贫困救济判断 TotalAssets，包含本手未结算投入，不通过饱和 uint 截断比较。

## 接口

`IWalletRepository`: ReadWallet、ExchangeWallet、SetHandWalletAccess、ApplyDollarMutation。

`IWalletPaymentRepository`: 带精确差额授权的 ChangeNamecard / ChangeCardFace / ChangeItem；旧接口保留为无兑换授权入口。

客户端 NetworkRoom: CanOpenWallet、RequestWallet()、ExchangeWallet(bool dollarsToChips, uint amount, string operationId=null)、WalletReceived 事件。

WalletResponse: RequestId、AccountId、IsExchange、OperationId、Result、HasSnapshot、Dollars、Chips、AvailableChips、CanExchange。

可信服务端发奖：NetworkRoom.GrantDollars(accountId, operationId, amount)，没有面向客户端的同名授权请求。

## 集成交接与验证

父代理负责 AccountAuthenticator、GameSettings/GameController 状态和客户端双币消费；牌局代理负责 Wallet handlers 注册、GM RefreshWalletBalance、AccountBalanceSync、准备计数广播及断线字典清理；UI 代理负责兑换与短缺确认界面。

本子任务仅检查源码、金额路径、diff 空白和新文件 CRLF/.meta 配套，没有运行 Unity、测试、构建、资源导入或 prefab 操作。Unity 编译与简单 Play 检查交由主代理统一执行。

## 本子任务修改的路径

以下仅列本子任务实际写入文件；已有其他 dirty 改动均保留。

- `Assets/Scripts/Network/Accounts/AccountRepository.cs`
- `Assets/Scripts/Network/Accounts/AccountService.cs`
- `Assets/Scripts/Network/Accounts/DailyTaskService.cs`
- `Assets/Scripts/Network/Accounts/ItemPurchasePolicy.cs`
- `Assets/Scripts/Network/Accounts/ItemPurchaseRuntime.cs`
- `Assets/Scripts/Network/Accounts/ItemShop.cs`
- `Assets/Scripts/Network/Accounts/ItemShopService.cs`
- `Assets/Scripts/Network/Accounts/Leaderboard.cs`
- `Assets/Scripts/Network/Accounts/NamecardShop.cs`
- `Assets/Scripts/Network/Accounts/PlayerAccount.cs`
- `Assets/Scripts/Network/Accounts/SqliteAccountRepository.Achievements.cs`
- `Assets/Scripts/Network/Accounts/SqliteAccountRepository.CardFaces.cs`
- `Assets/Scripts/Network/Accounts/SqliteAccountRepository.Gifts.cs`
- `Assets/Scripts/Network/Accounts/SqliteAccountRepository.PlayerEvents.cs`
- `Assets/Scripts/Network/Accounts/SqliteAccountRepository.Items.cs`
- `Assets/Scripts/Network/Accounts/SqliteAccountRepository.Shop.cs`
- `Assets/Scripts/Network/Accounts/SqliteAccountRepository.Tasks.cs`
- `Assets/Scripts/Network/Accounts/SqliteAccountRepository.cs`
- `Assets/Scripts/Network/Accounts/TaskRepository.cs`
- `Assets/Scripts/Network/Accounts/Wallet.cs`
- `Assets/Scripts/Network/Accounts/WalletPayments.cs`
- `Assets/Scripts/Network/Accounts/SqliteAccountRepository.Wallet.cs`
- `Assets/Scripts/Network/Accounts/GuardedAccountRepository.Wallet.cs`
- `Assets/Scripts/Network/NetworkRoom.Shop.cs`
- `Assets/Scripts/Network/NetworkRoom.CardFaces.cs`
- `Assets/Scripts/Network/NetworkRoom.Items.cs`
- `Assets/Scripts/Network/NetworkRoom.DailyTasks.cs`
- `Assets/Scripts/Network/NetworkRoom.ItemGrants.cs`
- `Assets/Scripts/Network/NetworkRoom.Wallet.cs`
- `Assets/Scripts/Network/Messages/AccountAuthenticationMessages.cs`
- `Assets/Scripts/Network/Messages/NamecardShopMessages.cs`
- `Assets/Scripts/Network/Messages/CardFaceShopMessages.cs`
- `Assets/Scripts/Network/Messages/ItemShopMessages.cs`
- `Assets/Scripts/Network/Messages/WalletMessages.cs`
- `Assets/Scripts/Network/Tasks/ConfiguredTaskService.cs`
- `Assets/Scripts/Game/Tasks/GameTask.cs`

新建的 Wallet.cs、WalletPayments.cs、SqliteAccountRepository.Wallet.cs、GuardedAccountRepository.Wallet.cs、NetworkRoom.Wallet.cs、WalletMessages.cs 均新增同名 `.meta`。
- `Docs/WalletBackend.md`（本说明）
