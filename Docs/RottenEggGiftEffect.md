# 臭鸡蛋礼物与牌局接入

## 在 Unity 中直接查看

使用 Unity `2022.3.52f1c1` 打开 `Assets/Scenes/RottenEggGiftDemo.unity` 并进入 Play Mode，或执行菜单 **Poker > Gifts > Play Rotten Egg Demo**。

演示场景可以点击玩家名片后投掷一次，也可以点击「连投 × 5」查看节流后的重叠效果。演示不连接服务器、不扣除库存。

正式牌局不需要手工挂组件。`GamePanel.prefab` 已挂载 `GiftInteractionController`，并通过 Inspector 引用礼物菜单，特效 prefab 由 `GiftCatalogSO.asset` 统一配置；菜单 prefab 自己持有主题底板、中文字体和礼物图标。进入已经开始的牌局后：

1. 点击其他玩家名片打开礼物菜单。
2. 点击菜单外关闭。
3. 连续点击「臭鸡蛋」会在 0.2 秒窗口内合并成一个请求。
4. 服务端成功扣除整批库存后向房间成员转播；所有客户端按队列播放。

臭鸡蛋当前商城单价为 50，可在 `Assets/Resources/SO/Shop/ItemShopCatalog.asset` 调整。商城中的消耗品购买会显示数字输入框，支持一次购买 100 个；服务器在一个事务中按输入数量扣款并增加库存。

## 便捷接口

通常只需使用名片菜单。其他牌局 UI 也可以把一次点击交给同一个 0.2 秒批处理器：

```csharp
GiftInteractionController.EnsureActive()?.QueueGift(
    GiftItemIds.RottenEgg,
    targetSeatIndex);
```

返回 `true` 表示这次点击已加入待发送批次。客户端只做库存预留和表现排队；服务端仍会重新验证身份、房间阶段、目标座位与真实库存。

需要绕过 UI 编写自动化客户端时，可以直接发送一批命令：

```csharp
var operationId = networkRoom.RequestGiftUseBatch(new[]
{
    new GiftUseCommandMessage
    {
        ItemId = GiftItemIds.RottenEgg,
        TargetSeatIndex = 4,
        Quantity = 5,
    },
});
```

正式业务代码优先调用 `QueueGift`，这样能共享捕获窗口、本地库存预留与反馈。

若要在其他界面直接播放美术资源，请序列化引用 `Assets/Prefabs/Gifts/RottenEggGiftEffect.prefab`，实例化后调用：

```csharp
effect.Play(receiverNamecardRectTransform);
```

`Stop()` 会立即停止，`PreviewAt(target, seconds)` 可固定动画时间用于美术检查。

## 服务端规则

- `ItemUsePolicy` 使用 `AllowedLocations` 决定物品可在房间外、等待房间或已开始房间使用。
- 三种契约只允许在房间外使用；臭鸡蛋属于 `ItemUseKind.Gift`，只允许在已开始的房间使用。
- 发送者身份从已认证连接推导，客户端请求不携带发送者。
- 目标使用稳定的 `SeatIndex`，服务端拒绝不存在、已经离开或发送者自己的座位。
- 一批请求最多 32 条合并命令、200 个实际礼物。正常连点远低于这个协议防滥用边界。
- `ConsumeGiftBatch` 在一个 SQLite 事务中校验并扣除整批物品；优先消耗库存，不足部分按服务器单价扣美金；按命令顺序保留可支付的前缀（允许最后一条只执行部分数量），遇到无法支付的礼物后停止。整批只有一个数据库事务。
- `operationId` 写入幂等收据。相同请求重试会返回成功，但不会再次扣除或再次转播。
- 礼物事务统一写入库存、幂等收据和实际成功数量的域事件；自动购买时同时扣美金、增加账户版本并记录美金流水，不扣筹码。

服务端在首次成功提交后发送 `GiftPlaybackBatchMessage`，其中包含服务端推导的发送者座位、目标座位、数量和统一的 `StartAtNetworkTime`。房间内客户端收到相同事件后开始表现。

## 播放节流

客户端没有对象池。每个礼物都会实例化一个独立特效对象，并在动画结束后销毁。

播放间隔由特效自身完整时长计算：

```text
播放间隔 = 完整动画时长 / 10
```

因此同一种特效连续播放时，屏幕上最多约有 10 个仍在播放。臭鸡蛋当前完整时长约 3.69 秒，间隔约 0.369 秒；若以后把完整动画改为 2 秒，间隔会自动变为 0.2 秒。队列不因显示并发数而丢弃已经被服务端接受的礼物。

## 动画时间线

- 0.00–0.32 秒：屏幕中心弹出 Q 版臭鸡蛋。
- 0.32–1.04 秒：沿抛物线旋转飞向目标名片。
- 1.04–3.04 秒：命中飞溅，碎蛋和蛋液在名片上滑落约 2 秒。
- 3.04–3.69 秒：整体渐隐。

完整鸡蛋、碎裂组、光环和拖尾当前使用 `0.5` 的宽高缩放，即原画面面积的四分之一。时间、缩放、抛物线高度与滑落距离可在 `Assets/Prefabs/Gifts/RottenEggGiftEffect.prefab` 的 Inspector 中调整。表现使用 `unscaledDeltaTime`，支持 Overlay Canvas 和配置了 `worldCamera` 的 Screen Space Camera Canvas。

## 资源

- `Assets/Art/Gifts/RottenEgg.png`：完整鸡蛋。
- `Assets/Art/Gifts/RottenEggSplat.png`：破裂蛋液与蛋壳。
- `Assets/Resources/SO/Shop/Items/Products/RottenEgg.asset`：商城和收藏定义。
- `Assets/Prefabs/Gifts/RottenEggGiftEffect.prefab`：由 `GamePanel` 序列化引用的特效 prefab。
- `Assets/Prefabs/UI/Panels/GamePanel/GiftMenuView.prefab`：牌局内礼物菜单和特效播放层。
- `Assets/Prefabs/UI/Shared/QuantityInputField.prefab`：商城批量购买复用的数字输入框。
- `Assets/Scenes/RottenEggGiftDemo.unity`：无需联网的播放场景。
- 美术生成提示词见 `Docs/RottenEggArtPrompts.md`。

## 库存不足时直接赠送

礼物面板有库存时显示数量；无库存时显示美金单价，可支付时为白色，不可支付时为红色且按钮不可用。连续点击只合并相邻的同类、同目标礼物，保持点击顺序。客户端保留待发送金额，最终播放数量和扣款以服务端结果为准。

红名／强制名片惩罚期间（服刑年数 ≥ 100）禁止送礼。客户端保留打开礼物菜单的入口，图标显示灰色遮罩并禁用；服务端在礼物批次的同一事务内读取契约库存并拒绝请求。低于惩罚门槛的小额借款不影响送礼。
