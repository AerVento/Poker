# 成就事件扩展说明

## 结构

`Game.Events` 中的业务事件是不可变、仅服务端使用的事实。业务模块通过来源适配器写事件，成就通过 `IAchievementHandler<TEvent>` 声明订阅。来源和成就 UI 类别互不关联。现有 18 个成就使用 `HandAchievement` 兼容层，ID、进度、奖励快照、领取记录及客户端消息均保持兼容；账户操作与牌局扩展新增 11 个，另有 2 个诈唬主题成就和 1 个隐藏成就，目前共 32 个。

| 来源 | 稳定类型 ID / v1 | 数据 |
| --- | --- | --- |
| table | stage | 现有 AchievementContext，完整手牌阶段快照 |
| table | action | 行动账号、底牌、阶段、有效行动、剩余筹码、行动前筹码和本次投入；v2 写入新增金额，v1 保留读取 |
| table | revealed | 主动亮牌账号、手牌 ID、实际已翻公共牌和两张底牌；v1 |
| shop | purchased | 商品类别 item／namecard／card-face、ID、数量、实际支付筹码 |
| inventory | used | 物品 ID、数量、实际支付筹码、目标座位（无目标为 -1） |
| inventory | changed | 操作来源与变更后的物品库存快照 |
| collection | equipped | 槽位、前后装备及当时名片／牌面装备快照 |

`PlayerEvent` 提供 EventId、SourceId、OccurredAt、AccountIds。多玩家牌桌事件每位参与者有独立消费回执；账户操作只投递给操作账号。泛型处理器仍接收 accountId，从而兼容多人快照与账号隔离。

## 事务与重试

购买、使用、装备、礼物批次消耗、系统赠送、每日任务及成就物品奖励，在原业务 SQLite 事务内通过 `TransactionEventWriter` 插入事件，记录成功后一起提交。读操作、失败操作、重复操作回执不产生新事实。装备前后归一化到 default 后相同则不产生 equipped 事件。

`IPlayerEventWriter.Append` 不提交事务。不要在 UI、网络成功响应或原先 best-effort 的 ItemPurchaseEvents 回调里写需要可靠保存的成就事件；那些通知仍可用于提交后的非关键反馈。物品取得与实际购买的来源必须区分。

事件 ID 由来源、业务类别、账号、持久化操作 ID 和事件序号组合，礼物批次按命令下标区分。同一次操作重试必须沿用其 operationId。实际物品变更和事件原子提交，后台服务读取尚未消费的事件，在另一个事务内更新成就进度并标记该账号回执。中途失败回滚，可以重启重试。

牌桌有效行动由 GameManager 的现有 AdvanceAfterAction 入口记录，在增加 TurnId 和推进阶段之前发布；不新增客户端权限。阶段快照沿用原入口，最终结算快照仍和手牌净结算处于同一事务。行动属于已接受的即时事实，牌局后来中止不撤销已解锁的行动成就。

`action/v2` 的 `StackBefore` 是本次有效行动执行前可用筹码，`CommittedChips` 是本次行动实际新增投入，不是本轮累计 `StreetBet`。旧 `action/v1` 解码时两者为零，因此不会误触依赖精确投入的新成就。「一掷成灰」在最终 `stage` 结算事件中把明确 AllIn 的 `CommittedChips` 减去该玩家最终未跟注退款，并检查其未赢得任何实际奖池。

主动亮牌只在服务端确认“其他玩家全部弃牌获胜”、仍处于该手结果窗口且尚未公开时发布。`TableEventSource.Revealed` 生成 `handId:reveal:accountId`，数据库唯一事件 ID 和账户消费回执共同保证幂等。该事件只路由给明确订阅 `HandCardsRevealedEvent` 的牌型展示成就；每日任务如接入同一事实，也只能推进组成牌型类任务，不能伪造 `Settled`、胜利或盈利事实。

礼物 used 事件表示服务端已接受并消耗该命令，不声称客户端动画已经命中。库存变更只包含物品库存，化妆品所有权仍由现有独立表管理。将来若需要“获得名片”事件，应新增对应事实，不能把它误报成主动装备。

## 增加成就

```csharp
public sealed class ExampleAchievement : Achievement,
    IAchievementHandler<ItemPurchasedEvent>,
    IAchievementHandler<ItemUsedEvent>
{
    // 与现有定义一样填写 Id、Name、Description、Category、IconPath、Reward。
    public void Evaluate(ItemPurchasedEvent value, string accountId, AchievementProgress progress)
    {
        // 仅更新进度／progress.Data，不修改共享定义实例，不自行领奖或调用业务服务。
    }

    public void Evaluate(ItemUsedEvent value, string accountId, AchievementProgress progress)
    {
        // 可以读取前面事件存入的分步进度，形成跨来源成就。
    }
}
```

以上是结构示意，补齐元数据后才能编译。启动时 `AchievementEventRouter` 扫描具体泛型接口并建立按事件类型分发的索引，一个事件只交给订阅该类型的定义。处理器使用事件发生时的事实，避免重放时读取最新装备或库存。进度去重、封顶、完成及奖励快照由 AchievementService 负责。

现有牌局成就继承 `HandAchievement` 并覆写原 Evaluate(AchievementContext, accountId, progress)。旧扩展直接继承 Achievement 并覆写此方法仍有兼容路由，新增扩展应使用显式的 HandAchievement 或类型化接口。

## 增加事件来源或事件类型

1. 在 `Assets/Scripts/Game/Events` 添加不可变 DTO，继承 PlayerEvent 或单账号的 AccountEvent。来源 ID 和事件类型 ID 使用稳定字符串，不使用客户端可指定的 CLR 类型名。
2. 来源适配器实现 IPlayerEventSource，在 Register 中调用 `registry.Register<具体事件>(sourceId, typeId, version)`。新来源在 `PlayerEventRegistry.CreateDefault` 的组合列表加入一次；现有来源新增类型只修改其注册方法。
3. 在业务成功路径将事件追加到同一个业务事务。已有事务范围内不能调用会另开事务的 PublishPlayerEvent；后者适用于牌桌等现有即时发布入口。
4. 成就实现对应的 IAchievementHandler<T>，无需修改路由、领奖、横幅或网络协议。新增 Assets 文件配套 .meta，并遵循图标目录中的美术规范。
5. 变更已发布事件结构时增加版本，并保留旧版本解码器；注册器支持每个稳定键的多版本读取，默认以该 DTO 注册的最高版本写入。语义变化应使用新 DTO 或显式转换解码器，不能假定旧 JSON 缺失字段就等于合法默认值。

注册器的编解码委托显式使用具体 DTO，避免任意类型反序列化和动态生成泛型分发器。AchievementLinker 保留发现的成就定义、事件 DTO、公共基类及牌桌嵌套快照，供 IL2CPP 裁剪使用。

## 数据兼容与验证范围

数据库 schema 12 在原 achievement_events 表追加 source_id、event_type、event_version；旧行默认 table／stage／1，payload 保留原 AchievementContext JSON，event_id、sequence 与所有消费回执不改写。历史 hand_id 列保留用于兼容查询，非牌桌事件写空字符串，不伪造手牌。旧的 PublishAchievementEvent 和 PendingAchievementEvent.Context 保留适配接口。

已消费事件不会自动重新判定，已有未消费事件继续按原顺序消费。新成就从新事件开始推进，不扫描旧账本回填；此前遗留未消费事实会正常补处理。遇到未知类型／版本或数据身份冲突会报错并保留未消费状态，不静默丢弃。

目前回执仍属于成就消费者。将来每日任务也消费同一事件表时，需要独立的消费者游标／回执，不得复用成就的 consumed 标记。本次未将每日任务判定一并迁移。

按用户要求，本次不运行自动化、联机或 UI 操作测试，也不执行构建。仅检查编译、定义／资源元数据和代码改动；迁移执行、重启重放、真实操作与平台构建尚未验证。
