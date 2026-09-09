# 牌面：美术接入、商店与奖励

当前内置「经典牌面」ID 为 `default`、「简约款」ID 为 `minimal`，所有新老账户默认拥有这两款，无需购买；初始装备仍为经典款，已有装备保持不变。默认所有权由服务端直接提供，无需逐个玩家补发或迁移数据库。商城和个人仓库的「牌面」页共用购买、正反面预览和装备界面。UI 沿用名片商城，每页固定 2×2、最多 4 款；不足 4 款时从左上角依次填充，其余留空。装备随账号保存，只影响该玩家自己看到的手牌、公共牌与结算牌面，不同步给其他玩家。

## 简约款来源

采用 Bad Hand Studio 的 [Simplified Playing Cards](https://badhandstudio.itch.io/simplified-playing-cards)，原始 SVG、作者免费使用声明和可复现导出脚本保存在 `ArtSources/CardFaces/Minimal`。本项目按非商业用途使用。已调整为左上点数、右下花色，沿用原包红色几何牌背，导出 52 张正面与 1 张牌背。

`minimal` 与 `default` 均显示“免费默认款”；服务端启动时会为旧版外部目录补上缺少的 `minimal` 内置条目，因此无需手动给已有玩家发奖。修改内置款的价格或下架会被配置校验拒绝。统一物品 ID 为 `cardface-minimal`，对已有玩家发放返回 `AlreadyOwned`。

按用户要求，本次新增美术后暂未运行测试、联机回归或构建。

## 新增一套牌面

1. 准备 **52 张正面 + 1 张牌背**，四种花色、2～A 齐全，不含大小王。统一竖版 **17:24** 比例；当前显示基准为 **204×288**，建议原图至少 **408×576**。同一套所有图的尺寸、透明边距、圆角位置一致；数字和花色缩小到 102×144 时仍应清晰。Unity 导入类型为 Sprite (2D and UI)，透明通道启用，避免有损压缩造成文字毛边。
2. 复制 `Assets/Resources/SO/PokerCardsSO.asset` 到自有美术目录，逐项替换 52 张 `Front` 和 `Back`；不要覆盖经典牌面或修改第三方资源。可使用独立 PNG，也可使用已正确切片的图集。
3. 复制 `Assets/Prefabs/UI/Panels/GamePanel/ClassicCardFace.prefab`。根节点保持 `RectTransform`，挂 `CardFaceView`，绑定 `_image` 和新建的 `_cards`。根节点按父槽位拉伸，内部保持 17:24，不修改外部布局。装饰放在子节点内；所有美术 Graphic 关闭 Raycast Target。**不要放入 `PokerElement`、NetworkIdentity 或游戏规则组件**。更复杂的视觉可继承 `CardFaceView`，实现 `Display(Poker, bool)`；`false` 时必须只展示牌背。
4. 在 Unity 的 `Create > Poker > Card Faces > Product` 创建商品 SO，填写稳定 ID、名称、说明和预制体，加入 `Assets/Resources/SO/CardFaces/CardFaceCatalog.asset` 的 `_products`。ID 使用 1～48 位小写字母、数字、`_`、`-`；售出或发放后不改 ID。
5. 在默认 `Assets/Resources/SO/Shop/CardFaceServerCatalog.json` 添加报价：`{"Id":"new-style","Price":1000,"Listed":true}`。部署后实际服务端读取服务器网络配置同目录的 `CardFaceServerCatalog.json`；已有外部文件不会被默认文件覆盖，需更新外部文件并重启服务器。客户端 SO 不决定价格或所有权。
6. 执行 `Poker > Card Faces > Validate Catalog`，再在 `Game.unity` 检查牌面页的黑桃 A 正面/牌背预览、实际手牌、公共牌和结算展示。已售牌面下架时保留 SO、预制体和服务端条目，将 `Listed` 改为 `false`；拥有者仍能装备。

## 用物品 ID 发放奖励

与名片共用可信服务端入口和幂等回执；映射为 `new-style ↔ cardface-new-style`，经典款为 `cardface-default`。所有权写入 `account_card_faces`，不加入普通物品背包，不扣筹码，也不自动装备。

```csharp
string itemId = CardFaceItemId.ToItemId("new-style");
// 首次发放前保存此业务操作 ID；重试必须复用，不能每次生成新 ID。
string operationId = Guid.NewGuid().ToString("N");
var result = networkRoom.GrantItem(accountId, itemId, operationId, quantity: 1);
```

全部服务端目录条目（包括下架款）自动进入 `ItemIdentities`，无需再登记一份物品配置。牌面为永久收藏，最大数量 1；已拥有返回 `AlreadyOwned`，相同请求重试返回原结果且不重复发放。`CardFaceItemId.ToCardFaceId()` 可反向转换。普通物品禁止占用 `cardface-` 前缀。

此入口与现有名片奖励一样供服务端代码调用，支持离线玩家；不能从客户端自行发奖，也不要在另一个 SQLite 写事务中嵌套调用。它不自动扩展每日任务 JSON 中的普通道具奖励语法。玩家重新打开牌面页后读取最新库存。

## 兼容与存储

数据库由 v10 迁移至 v11，新增装备列、牌面所有权和购买回执表，保留现有账户/名片/物品数据。购买、扣款、库存和回执同事务提交；奖励发放复用 `item_grant_receipts`。新增登录与牌面商城协议，客户端和服务端需同步更新。客户端缺少已装备款的美术时回退经典款，不改变服务器装备记录。
