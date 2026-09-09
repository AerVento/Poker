# 臭鸡蛋礼物特效

## 直接播放

在 Unity 2022.3.52f1c1 中打开 `Assets/Scenes/RottenEggGiftDemo.unity`，按 Play。
也可以使用菜单 **Poker > Gifts > Play Rotten Egg Demo**。

演示默认循环播放。点击任意玩家名片打开礼物菜单，点击「使用 · 预览」投掷，点击菜单外关闭。「再扔一次」立即重播，「循环播放」可以关闭自动重播。
这是独立美术演示，不连接服务器，不购买或扣除物品，不改变正式构建场景。

## 动画

- 0.00–0.32 秒：屏幕中心弹出 Q 版臭鸡蛋，带弹性缩放。
- 0.32–1.04 秒：沿抛物线旋转飞向目标名片，带绿色拖尾。
- 1.04–3.04 秒：命中飞溅，碎蛋与蛋液向下滑落，臭气上升。
- 3.04–3.69 秒：整体平滑渐隐；隐藏视觉节点，供下一次复用。

时间、蛋大小、抛物线高度和滑落距离可在 prefab Inspector 调节。

## 接入牌局

将 `Assets/Prefabs/Gifts/RottenEggGiftEffect.prefab` 实例化到 UI Canvas 下，RectTransform 拉伸铺满画布。
收到服务端确认的礼物使用事件后调用：

```csharp
effect.Play(receiverNamecardRectTransform);
```

`Play` 会重新开始当前实例的动画；需要同时多份礼物时使用多个实例。
`Stop()` 立即收起；目标被销毁、隐藏或特效组件停用时自动停止。
`PreviewAt(target, seconds)` 可固定动画时间用于美术检查，再调用 `Play` 恢复播放。

使用 unscaledDeltaTime，牌局暂停时仍可播放。每帧将目标名片转换为特效 Canvas 坐标，支持 Overlay 和配置了 worldCamera 的 Screen Space Camera Canvas。特效图层不接收射线，不修改目标名片的位置或缩放。
推荐放在根 Canvas 的独立、未裁剪容器中；显示层排序为 30000。正式接入时由服务端校验礼物数量、消耗和收发双方身份，客户端只播放表现。

## 美术来源

两张透明 PNG 使用内置 imagegen 生成，原始 alpha 保留；Unity 导入为 1024 上限、无 mipmap 的 Sprite。臭气、拖尾、飞溅和流淌由 uGUI 网格辅助表现。

- `Assets/Art/Gifts/RottenEgg.png`：完整鸡蛋。
- `Assets/Art/Gifts/RottenEggSplat.png`：破裂蛋液与蛋壳。
- 完整生成提示词见 `Docs/RottenEggArtPrompts.md`。

后续的正式礼物菜单、库存消耗、商城批量购买和网络广播属于业务接入，不在本次特效演示中。

## 本次验证

2026-09-09，在 Unity 2022.3.52f1c1 中完成脚本编译及演示场景 Play Mode 验证：

- 检查弹出、抛物线飞行、命中滑落、渐隐四阶段，检查 2 秒滑落距离及渐隐中点/终点透明度。
- 三个名片的命中位置误差小于 1 屏幕像素；目标移动后贴图继续跟随。
- 菜单外实际 EventSystem 射线及 PointerClick 分发可关闭菜单；所有特效 Graphic 不接收射线。
- 重播重置计时，空目标、隐藏目标、停用组件均安全停止。
- 1920×1080 截图检查；辅助图形有有效渲染网格；Missing Script 为 0，最终 Console error 为 0。
- PNG 角落 alpha 为 0；新 C# 均为 UTF-8 / CRLF，资源都有对应 `.meta`。

本次未运行牌型 NUnit、联网 server/client 流程或平台构建；本次没有改动牌型、网络协议、商城交易或正式牌局 UI。截图保留于本地 `Logs/GiftPreview`，不纳入版本控制。
