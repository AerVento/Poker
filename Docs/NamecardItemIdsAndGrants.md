# 名片物品 ID 与服务端发放

名片接入统一物品身份注册表，但保留现有名片商城、购买与装备请求。名片所有权仍存储在 `account_namecards`，普通物品仍在 `account_items`；系统发放与原名片购买检查同一份所有权，不迁移旧库存，也不把名片显示到普通背包或物品商城。

## ID 双向转换

`Game.NamecardItemId` 提供不依赖 Unity 对象的静态 API：

```csharp
string itemId = NamecardItemId.ToItemId("galaxy");       // namecard-galaxy
string namecardId = NamecardItemId.ToNamecardId(itemId); // galaxy

bool validItemId = NamecardItemId.TryToItemId("galaxy", out string convertedItemId);
bool validNamecardId = NamecardItemId.TryToNamecardId("namecard-galaxy", out string convertedNamecardId);
```

- 原名片 ID、外部 JSON、SO、装备字段及网络消息仍使用 `galaxy`；统一发放接口使用 `namecard-galaxy`。
- 转换区分大小写。原 ID 允许 1～48 个小写英文字母、数字、下划线、连字符；添加 `namecard-` 后最多 57 个字符。该扩展 ID 用于服务端注册和发放，不传入原来限长 48 的商城消息。
- `To...` 遇到非法输入抛出 `ArgumentException`；`TryTo...` 返回 `false`，输出为 `null`。
- 默认名片为 `default ↔ namecard-default`。账户装备字段中的空字符串是原有“默认外观”表示法，不作为转换 API 的输入。
- 转换只检查格式，不证明商品存在。使用 `NetworkRoom.ItemIdentities.TryGet(itemId, out var identity)` 检查当前注册状态。

## 身份注册

服务端加载两份商城目录后调用 `InitializeItemGrants()`。`ItemIdentityRegistry` 复制普通物品身份和全部名片身份，包括下架商品；名片映射自动从名片目录生成，不需要手工维护另一份 SO 或价格配置。

名片身份为 `Kind=NonConsumable`、`MaxQuantity=1`、`IsEquippable=true`、`ShowInInventory=false`，`NamecardId` 指向原 ID。普通物品保持原 ID，并保留其数量上限。普通物品不能占用 `namecard-` 前缀，冲突时启动失败。

身份注册表只用于类型识别和所有权分派，不作为普通商城报价或背包快照。名片价格和上下架仍由外部 `NamecardServerCatalog.json` 控制，修改后重启生效，详见 [AddingNamecards.md](AddingNamecards.md)。

## 发放 API

服务端运行期间，在 Unity 主线程通过 `NetworkRoom` 调用：

```csharp
string itemId = NamecardItemId.ToItemId("galaxy");
// 首次发放前生成并保存，重试时必须复用同一个 operationId。
string operationId = Guid.NewGuid().ToString("N");
ItemGrantExecution execution = networkRoom.GrantItem(accountId, itemId, operationId);

if (execution.Result == ItemGrantResult.Success && execution.IsNewlyApplied)
{
    // 本次调用首次完成发放。可在此发送非持久化提示。
}
```

`accountId` 是账户 ID，不是用户名或连接 ID。`operationId` 必须是 `Guid.ToString("N")` 格式，按收件账户隔离。`quantity` 默认为 1，名片和其他永久物品最多持有 1 个；普通消耗品遵守目录数量上限。

发放入口只供可信服务端代码调用，没有新增可让客户端自行发奖的 Mirror 消息。离线、房间内玩家也可以接收库存发放，不改变当前装备、房间或筹码。服务器尚未启动、已发生存储故障或注册表不可用时返回 `Unavailable`。

底层接口是 `IItemGrantRepository.GrantItem(accountId, itemId, operationId, quantity, registry)`，由 SQLite 实现并经过 `GuardedAccountRepository`。它自行开启并提交事务，应在其他业务事务之外调用；本次没有把它嵌入现有每日任务奖励事务。需要让任务领取与名片奖励同事务时，应单独接入该任务事务，不能在已经打开的 SQLite 写事务内调用本接口。

## 行为与结果

| 情况 | 返回与行为 |
| --- | --- |
| 首次成功发放 | `Success`，`IsNewlyApplied=true` |
| 相同账户、操作 ID、物品 ID、数量重试 | 返回持久化的原结果，`IsNewlyApplied=false`；重启后仍有效 |
| 复用操作 ID，但改变物品或数量 | `InvalidRequest` |
| 已拥有的永久物品或默认名片 | `AlreadyOwned`，不叠加、不自动装备 |
| 格式正确但未注册的 ID | `UnknownItem` |
| 玩家账户不存在 | `AccountNotFound` |
| 数量超出上限 | `QuantityLimit` |
| 非法 ID、操作 ID 或零数量 | `InvalidRequest` |

下架只限制购买，不阻止服务端发放。格式正确、账户存在的业务结果会记录回执；若同一次发放曾返回 `UnknownItem` 或 `QuantityLimit`，后续改变配置不会改变该操作的历史结果，要发起新的业务发放应使用新的操作 ID。已成功的发放即使后来从注册目录移除，也能按原请求重试并返回原结果。

名片发放写入原 `account_namecards`，因此原购买请求会返回已拥有、原装备请求可正常装备。玩家下次打开名片商城或刷新现有快照后看到结果，本次不增加主动推送消息。请保留已售/已发名片在目录中的条目，以便后续装备。

普通物品发放仅增加 `account_items`，不执行购买条件、购买效果或购买事件。例如直接发放 `devil-contract` 只增加契约所有权，不奖励筹码。所有发放均不扣款、不写筹码账本，也不写购买回执。

## 数据库升级与验证

数据库结构从版本 8 升至 9，新增 `item_grant_receipts`，主键为 `(account_id, operation_id)`，保存物品 ID、请求数量、结果与时间。所有权、账户版本和回执在一个事务提交，失败整体回滚；存储异常沿用停服保护。复用 `account_namecards.purchased_at_utc` 保存名片首次取得时间，新的发放回执用于区分系统发放与历史购买。

没有迁移或删除旧名片、普通物品、任务、订单或装备数据。此次数据库修改在契约、每日任务、排行榜任务交接后串行完成，保留版本 7/8 的迁移与任务结算 outbox。

新增 `Game.Tests.NamecardItemIdTest` 和 `Game.Tests.ItemGrantTest`，覆盖双向转换、长度边界、命名冲突、购买/发放互认、背包隔离、默认/下架名片、持久化重试、数量限制、回滚、存储保护与版本 8 升级。本轮按用户要求仅做静态检查，未编译、未运行这些测试或一服多端验证。
