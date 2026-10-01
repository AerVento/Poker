# 同花顺与皇家同花顺奖励名片

两款商品加入 `Assets/Resources/SO/NamecardCatalog.asset`，商城上架、不可购买。商城只显示主题介绍，不提示获取途径；未拥有时按钮为「不可购买」，拥有后可免费装备。所有名片统一遵循此规则，`CanPurchase` 只限制购买。

| 名片 ID | 主题 | 现有成就 | 奖励 |
| --- | --- | --- | --- |
| `straight-flush` | 翡翠缎面、银色细框、五枚黑桃 | `showdown_straight_flush`，牌型类，最终摊牌时最佳牌型为同花顺 | $20,000 + `namecard-straight-flush` ×1 |
| `royal-straight-flush` | 紫绒暗纹、金色细框、冠冕与紫晶 | `royal_straight_flush`，特殊类，弃牌前曾组成同花色 A/K/Q/J/10 | $50,000 + `namecard-royal-straight-flush` ×1 |

保持现有判定和领奖时机：达成时保存奖励快照，玩家在成就面板领取时将筹码、名片库存及领取状态原子写入。皇家同花顺在最终摊牌时也满足普通同花顺成就，因此首次同时满足时两项奖励均可领取。已拥有名片不会阻塞筹码奖励；重复领奖不重复发放；不自动装备。

名片写入 `account_namecards`，不会进入普通背包 `account_items`。服务端初始化通过统一 `ItemIdentityRegistry` 解析名片物品 ID。保持现有网络协议、数据库版本和外部服务器目录格式。

## 美术交付

使用内置 image_gen 分别生成四张图片。宽版 2048×768；独立构图的紧凑源图 1448×1086，Unity 分别等比导入至 1024×384 和 1024×768。四张图片使用 Sprite/Single/Full Rect、Max Size 1024、无 Mipmap、Clamp、NPOT None；`.meta` 由 Unity 生成。

- `Assets/Sprites/Namecards/straight-flush-wide.png`
- `Assets/Sprites/Namecards/straight-flush-4x3.png`
- `Assets/Sprites/Namecards/royal-straight-flush-wide.png`
- `Assets/Sprites/Namecards/royal-straight-flush-4x3.png`

完整提示词、定向修改与文件清单见 [reward-namecards-v1.json](reward-namecards-v1.json)。同花顺宽版经过一次局部修改，将顶部饰件收回 64 px 边框限制内。每款两种比例均接入同一商品 SO。

## 配置与发布

商品 SO 新增「成就奖励」标记，用于禁售及奖励名片展示。它不影响已拥有者的装备资格。商品描述为纯主题文案。

服务端默认目录直接由 `NamecardCatalog.asset` 创建。SO 中上架但不可购买的商品会得到 `Listed: false`：商城可展示奖励来源，服务端拒绝购买，已拥有者仍能装备。外部 `Configs/Shop/Namecards.json` 仅用于按 ID 调价或上下架，无需复制完整目录；新 ID 随新版 SO 进入默认目录。

已有成就完成记录保留原奖励快照，不自动追补或修改历史领奖；本次配置适用于更新后新达成的成就。

## 验证入口

- EditMode：`Game.Tests.RewardNamecardTest`，覆盖实际 SQLite 发放、幂等、已拥有、失败回滚、购买拒绝、装备与重开数据库、奖励配置和两种图片比例，以及普通不可购买名片的装备入口。
- 联机：`-pokerIntegrationScenario reward-namecards`，一服三端，首手注入皇家同花顺；第三人中途入桌。验证真实下注、摊牌、两项成就、越过 UI 的购买请求被拒绝、非达成者无奖励、两次领奖、装备及重连。

## 实际验证（2026-09-09）

- Unity 2022.3.52f1c1 中 119 项 EditMode 测试通过：RewardNamecard、Achievement、NamecardShop、NamecardArtworkVariant、NamecardCatalogLoader、NamecardItemId、InfernalNamecard。
- Windows Development Build 成功（0 error，4 warning），输出为本地忽略目录 Build/RewardNamecards。未执行 Linux server 或 WebGL 构建。
- reward-83f83b1d 一服三端全部通过，两手完整牌局；最后账户总额和账本总额均为 100,000，活动手牌 0，对账一致。独立账号与数据库隔离，进程退出后恢复了原有登录偏好。
- Game.unity Play 模式检查未拥有/已拥有/已装备状态、长姓名、极大余额和实际 Player8 的 4:3 构图（236×177），同时显示 D/SB；两张紧凑图均由比例选择逻辑正确选中。名片 SO 和图片引用完整，美术导入校验无问题。
- 编辑器保留原有 SocketException 连接拒绝日志，本次没有新增脚本编译错误；独立客户端无异常日志。无图形服务器记录 URP shader 不受支持信息，不影响联机测试。
- 商城实景；拥有后与紧凑版实景。
