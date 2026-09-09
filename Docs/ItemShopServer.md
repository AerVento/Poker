# 物品商店与背包服务端

## 2026-09-09 更新

购买条件与购买效果继续分离；新增 ItemUseRegistry / ItemUsePolicy，将使用条件与扣款效果分别注册。契约使用由服务端原子扣余额、扣库存、记账和保存回执。ItemOffer 协议增加 CanUse / UseCost，客户端与服务端须使用同一版本。

实现计划见 [UIPolishAndContractRepaymentPlan.md](UIPolishAndContractRepaymentPlan.md)。

2026-09-07。基于已有 `ItemBrowserView` / `ShopPanel` 接口接入真实账户服务。当前产品范围是不可消耗物品；消耗品只预留接口，使用请求不会扣库存或执行效果。

## 已接通

- `NetworkRoom.Items.cs` 注册已认证的 Mirror 读取、购买、使用请求。账户只取当前连接对应的有效会话，不接受客户端提供账户 ID。库存仅返回本人，不加入房间、牌局或其他玩家的公开投影。
- 服务端校验请求序号、频率、操作 ID、物品 ID、数量、目录版本和报价。实际价格、上架状态、类型和数量上限来自服务端目录。每次购买数量固定为 1；不可消耗物品最多持有 1 个。
- 房间内或活动手牌未结束时不能购买或使用；读取仍可返回库存，并标记不可操作。库存保留已下架、已从目录移除的物品。
- SQLite schema 6 新增 `account_items` 和 `item_requests`。购买扣款、账户版本、库存、`ItemPurchase` 筹码账本及操作回执在同一个事务提交；任一步失败全部回滚，并沿用 `GuardedAccountRepository` 的存储故障处理。
- 操作 ID 按账户隔离，相同操作与载荷重试返回原结果，不再次扣款。复用同一个 ID 改变载荷会被拒绝；目录变更后重试已成功的原请求仍返回原结果。
- `ShopPanel.ItemsOnline.cs` 打开面板时读取服务器快照，接收购买/使用事件，按同一操作 ID 最多尝试 3 次。每次发送使用新请求序号；超时后保留未确认状态并禁用操作，重开面板读取实际库存。断线/关闭清理会话，旧面板结果通过账户、请求 ID 与操作 ID 校验被丢弃。
- 物品和名片余额共用现有账户筹码。名片回复刷新物品余额；物品回复刷新公共余额与名片按钮。永久物品购买后显示「已收藏」，收藏页不可消耗。

## 配置正式物品

正式服务端目录是 `Assets/Resources/SO/Shop/ItemServerCatalog.json`，当前包含三种免费永久契约。它们按 ID 分别注册 `BalanceCondition(Equal, 0)` 和 `RewardChipsEffect(1000/10000/100000)`。上架不会自动发放库存。目录随构建打包，在服务端启动时读取和验证；更新后需重新构建并重启服务端。

添加新商品还需要在组合根显式注册条件/效果（普通商品可为空），并更新契约专属校验范围；目录格式示例：

```json
[
  {
    "Id": "your-item-id",
    "Kind": 1,
    "Price": 300,
    "Listed": true,
    "MaxQuantity": 1
  }
]
```

`Kind` 为 `ItemKind` 的数值：0 消耗品、1 不可消耗品。ID 使用小写字母、数字、下划线或连字符，最多 48 个字符；发布后保持不变。不可消耗品 `MaxQuantity` 必须为 1。目录最多 500 项，不能有重复 ID；空目录合法。所有字段均使用上例大小写。

客户端通过 `Create > Poker > Items > Item Definition` 创建 `ItemDefinitionSO`，设置相同 ID、名称、说明、图片和不可消耗类型，并在 `ShopPanel` prefab 的 `ItemsView._definitions` 注册。价格由服务器提供；客户端定义不决定交易规则。缺少客户端定义的商品不会显示在商城，旧库存仍显示 ID 和数量。

将 `Listed` 改为 false 可下架，已购买库存不变。修改 ID 会被视为另一个物品。三种契约沿用原 prefab 引用、ID 与 GUID，仅将资产类别修正为不可消耗。

## 消耗品扩展点

协议已包含 `Use`；`IItemRepository.ChangeItem` 已校验所有权、物品类型、房间/手牌限制和幂等性。目前永久物品返回 `NotConsumable`，持有的消耗品返回 `EffectUnavailable`，数量和余额均不变。

确定使用效果后，在 `SqliteAccountRepository.Items.cs` 的 `ValidateItemChange` 和事务成功分支中实现条件、效果及扣除。效果与库存扣除须同事务提交；不能仅把拒绝结果改为成功，因为当前成功分支专用于购买。客户端只能请求使用，不决定效果、奖励或消耗结果。

## 协议与升级

`ItemShopMessages.cs` 使用公开字段和基础类型数组，由 Mirror Weaver 生成序列化；`Serialization.cs` 的既有房间 JSON 不需要修改。请求不包含账户身份，回复不包含完整 `PlayerAccount`、密码或服务端手牌状态。新增协议需客户端与服务器配套构建。

启动账户存储时自动从 schema 5 升至 6，保留已有账户、余额、名片与账本；不会给已有账户写入物品。低版本数据库升级仍按原有迁移顺序执行。

## 通用商店阶段验证（历史记录）

- Unity `2022.3.52f1c1` 脚本编译和 Mirror Weaver 通过。全部自有 EditMode **447/447 通过，0 失败、0 跳过**，作业 `3259a4f10d874cf7ab0ad3828e148e22`，包含新增 16 项物品测试及已有 UI、账户、名片、牌局回归。故障回滚测试会主动产生预期故障日志。
- 最终 Windows 开发构建 `build-dcf223451a` 成功，0 error、2 warning；产物位于忽略提交的 `Build/ItemShop/`。
- 一服三端 `Logs/ItemShop/items-46ba5bae` 产生四份 `.pass`。三端实际点击名片和物品 UI，验证取消不扣款、提交后等待权威结果、购买一次、永久物品禁用重复购买/使用、跨页签余额同步、离房重开仍持有；每端均有 `items-*-purchase.checked` 和 `items-*-reopen.checked`。
- 同次运行完成三手建房、加入、准备、下注、推进、结算、旁观/参战转换、离开重入、离房返回与客户端断开。测试账户先各购买 6,000 的名片，再各购买 300 的测试物品；最终账户总额与账本均为 **11,100**，活动手牌 0，账户/账本差异 0。
- 第一轮 `items-725402f5` 在旧名片测试环节失败：测试脚本仍假定售价 6,000，正式目录已为 50,000。随后仅对 `items` 场景设置独立内存名片报价，并让测试等待两个库存请求完成，再构建重跑通过；正式目录未改价。
- 测试启动脚本恢复了运行前的登录偏好。没有对正式玩家数据库执行升级、购买或发放操作。本轮没有构建 Linux/WebGL，也没有做浏览器联机或新的视觉截图验收。
- 新增脚本均有 `.meta`，使用 UTF-8 / CRLF；原 `NetworkRoom.cs` 保持 GBK / CRLF。

可运行 `Game.Tests.ItemShopTest` 复验新库存、迁移、交易、回滚、协议与身份校验；集成运行器新增 `-pokerIntegrationScenario items`，复用原一服三端名片/牌局流程，并在内存注入独立测试商品。该场景仅使用 `PokerIntegration-<runId>.db` 测试数据库，不修改正式目录或玩家库存。


## 契约购买扩展

schema 8 为 `item_requests` 增加可空 `outcome_json`，兼容旧回执。余额校验、基础扣款、库存、`ItemPurchaseReward` 账本和原始成交明细同事务提交；相同操作重试优先读取回执，不重复发款。服务端使用不可变 `ItemPurchaseRuntime`，报价版本涵盖商品、独立条件和有序效果的描述及参数。

`ItemShopService` 在首次提交后向 `ItemPurchaseEvents` 发布按 ID 订阅的只读通知，订阅者异常不会触发存储停服；原子奖励由购买效果执行。新回复增加条件、资格、拒绝原因、实际奖励与是否首次执行，需要客户端和服务端配套更新构建。

UI 从服务端条件描述格式化购买要求，不硬编码契约 ID。余额同步后先禁用旧资格并重新读取，写操作核对期间不覆盖其操作 ID。新版测试与一服多端记录见 `ContractShopPurchaseTask.md`。
