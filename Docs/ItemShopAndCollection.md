# 物品商城与收藏面板

物品列表现由 `ItemShopCatalog.asset` 统一维护，`ItemBrowserView` 仅引用该 SO。配置及迁移说明见 [物品商店配置](ItemShopConfiguration.md)。

## 2026-09-09 更新

商城与仓库导航改为购物车／箱子图标。三种契约为可消耗品，使用时扣款并减少库存；旧的不可消耗契约说明已由此变更替代。特殊购买与使用条件仅保留在物品详细介绍里。

实现计划见 [UIPolishAndContractRepaymentPlan.md](UIPolishAndContractRepaymentPlan.md)。

2026-09-07。UI、导航、真实服务端交易和三种契约已接通。三种契约均免费且不可消耗，仅余额为零时可购买，购买后立即获得奖励；使用效果仍仅预留接口。

## 入口与交互

- 标题页「商城」打开物品商店；「收藏」打开已拥有物品。两个入口每次打开均默认选中物品页。
- 商城页签为「物品商店 / 名片商店」，收藏页签为「物品 / 名片」。名片页共用原有商品、库存、购买确认、装备和分页逻辑，保留未拥有名片的购买入口。
- 两个入口复用已注册的 `ShopPanel` prefab，通过 `ShopPageMode` 区分；不会额外叠加同类面板或重复创建名片请求会话。标题入口沿用登录、房间恢复优先及房间内禁止打开的规则。
- 物品视图每页 6 格，点击查看说明、类型及持有数量。商城展示有效上架报价，收藏展示数量大于零的库存；下架物品仍可收藏，缺少本地定义的旧库存显示 ID 和数量并禁用使用。
- 消耗品每次使用 1 个；不可消耗品显示「永久收藏」。购买和使用均需确认；取消、切页、切页签或刷新数据会废弃旧确认。请求未完成时禁用再次提交。
- 返回标题后清理物品视图会话；重新打开由数据提供方重新读取。两种商品的余额显示互相同步。
- 无正式物品时显示空状态，不把测试物品写入玩家库存。未绑定处理器时，购买和使用按钮不可执行。

## 数据与服务接入

`ItemDefinitionSO` 是可在 Inspector 编辑的物品定义，包含稳定 ID、名称、说明、图标与 `ItemKind`（消耗品 / 不可消耗品）。在 `ShopPanel` prefab 的 `ItemsView._catalog` 注册引用。通过 `Create > Poker > Items > Item Definition` 创建定义。

### 契约物品配置

以下资产位于 `Assets/Resources/SO/Shop/Items/Products/`，已按下表顺序注册到共用面板，并关联 `Assets/Sprites/Items/Contracts/` 中对应的透明图标。

| 名称 | 稳定 ID | 配置文件 | 类别 | 设定 |
| --- | --- | --- | --- | --- |
| 恶魔契约 | `devil-contract` | `DevilContract.asset` | 不可消耗品 | 余额为 $0 时购买，获得 $1,000；死后服役一年 |
| 大恶魔契约 | `archdevil-contract` | `ArchdevilContract.asset` | 不可消耗品 | 余额为 $0 时购买，获得 $10,000；死后服役十年 |
| 撒旦契约 | `satan-contract` | `SatanContract.asset` | 不可消耗品 | 余额为 $0 时购买，获得 $100,000；死后服役一百年 |

三者均为永久收藏，每账户每种最多购买一次。服务端在同一事务中校验余额恰好为 $0、增加库存并分别赠送 $1,000 / $10,000 / $100,000；不能在背包使用或消耗。零余额条件与奖励效果独立注册，详见 `ContractShopPurchaseTask.md`。ID 不随展示名变化，客户端定义不决定奖励金额或购买资格。

注册定义不会自动上架或发放库存。商城仍需服务提供同 ID 的 `ItemOffer`；收藏仍需同 ID 且数量大于零的 `ItemStack`。未绑定真实操作处理器时，购买与使用保持禁用。

配置验证：三项 ID 唯一、图标与详情一致、共用 prefab 引用完整且无 Missing Script。Unity 2022.3.52f1c1 的 `Game.unity` Play Mode 中，通过临时内存快照验证商城展示三件、收藏只展示拥有的一件、详情图标正确及名片/物品页签切换；测试报价不保存到资产或账户。`ItemCollectionTest` 与 `UINavigationTest` 共 62 项 EditMode 测试通过（作业 `f4a722a2df724e6e9a6486723872b051`），Console 无 error。本次配置变更未重跑一服多端牌局流程。

`ItemInventory.cs` 内的普通 C# 类型不依赖 Unity 对象：

- `ItemStack`：物品 ID、数量。
- `ItemOffer`：物品 ID、单价、上架状态；与定义分开，便于后续服务端报价。
- `ItemInventorySnapshot`：只读库存、报价、余额和允许操作状态。复制输入集合，并拒绝重复 ID。
- `ItemActionRequest`：购买或使用、物品 ID、数量 1、报价及操作 ID。

`ShopPanel.ItemsOnline` 在打开界面后，向 `ShopPanel.ItemsView.SetSnapshot` 提供账户数据，通过 `BindActions` 接收请求。在 Unity 主线程调用 `CompleteAction(operationId, snapshot, message)` 返回结果；只有匹配当前请求的结果会被接受。失败时也要返回核对后的快照和提示；连接失败、超时核对和按同一个操作 ID 重试由服务负责。

UI 不扣款、不增加库存、不减少数量，也不执行游戏效果。真实服务会验证身份、价格、持有量、物品类别、使用条件与幂等性，成功后返回权威快照。名片服务返回的账户余额通过 `SetBalance` 更新物品页；物品快照通过 `SnapshotChanged` 更新公共余额和名片按钮。

## 初始 UI 阶段验证（历史记录）

- Unity 2022.3.52f1c1 编译通过。
- `ItemCollectionTest`、`UINavigationTest`、`NamecardShopTest` 共 80 项 EditMode 测试通过，0 失败、0 跳过。最终作业：`546a16f31bc94eef9db272746f9fe7a5`。
- 在 `Game.unity` 的 Play Mode 使用仅存在内存中的测试物品，实际调用按钮验证：购买取消、确认、重复提交拦截、收到结果前库存不变、购买后数量刷新、切页签关闭确认、拒绝旧确认和旧结果、使用消耗品、不可消耗品禁用、返回标题、重开复位。
- 1920×1080 下查看标题页、物品空状态、已拥有物品详情及共用名片页。截图在忽略提交的 `Logs/CollectionTitle.png`、`Logs/CollectionEmpty.png`、`Logs/CollectionPopulated.png` 等文件中；已拥有物品截图是临时 UI 测试数据。
- 两个修改过的 prefab 无 Missing Script，相关新增序列化引用完整；`Game.unity` 场景校验无问题，最终 Console 无 error。
- 本轮没有重新构建或运行一服多端完整建房、下注、结算、断线流程；没有验证尚未接入的真实物品购买、持久化和使用效果。登录路由及断线返回覆盖来自上述 EditMode 导航测试。


契约实现后的最新验证记录见 `ContractShopPurchaseTask.md`。余额改变会使旧资格失效并触发刷新，条件不满足时按钮禁用，详情与确认框展示免费价格、条件和奖励。
