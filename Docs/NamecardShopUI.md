# 名片商店 UI 与商品配置

更新：2026-09-06。已接入真实购买、永久库存、装备与房间／牌桌外观。登录后在房间列表点击「名片商城」。新增款式的逐步操作见 [新增名片操作指南](AddingNamecards.md)，实现与验证见 [商店计划](NamecardShopPlan.md)。

## 在 Unity 中查看

打开 Assets/Scenes/Game.unity，选择 **Poker → UI → Preview Namecard Shop**。编辑器会进入 Play Mode 并打开商店。预览余额、库存和装备只保存在 Editor 演示代码的局部变量中，不影响真实账户或排行榜；停止 Play Mode 后丢弃。

可测试翻页、购买确认／取消、购买后变成“装备”、自由切换以及切回默认款。购买不会自动装备。示例账户初始余额为 500,000，已收藏星河、霓虹，当前装备默认经典款。底部明确标注“界面预览”。

商店沿用墨绿与金色风格，两行两列，每页 4 款；每款显示图片、名称、介绍、价格／拥有状态与按钮，无筛选或搜索。姓名和金额由 UI 叠加，未烘焙到素材中。

## 配置商品

目录位于 **Assets/Resources/SO/Shop/Namecards/NamecardCatalog.asset**，每款商品位于 **Assets/Resources/SO/Shop/Namecards/Products/**。中文 Inspector 支持：

- 商品 ID：关联库存的稳定标识，售出后不要更改。
- 名片名称、商品介绍：分别显示在卡片底部；介绍建议一句短文案。
- 名片图片：可直接替换 Sprite 引用，建议保持 8:3 横向比例。
- 价格（账户筹码）：普通商品可编辑，当前数值为初始可调整配置；线上显示服务器报价。
- 默认款：玩家天然拥有、不需解锁，有效价格恒为 0；目录中保留唯一默认款并放在第一位。

目录的上架数组决定展示顺序，拖动引用即可排序；添加普通商品后页数自动计算。下架请取消商品 SO 的「上架」，保留目录引用和美术，已拥有者仍可装备。导出／构建会拒绝空引用或重复 ID。SO 保存商品配置，不保存玩家余额、库存或当前装备。

新建商品使用 Project 右键 **Create → Poker → Shop → Namecard Product**，配置后将资源拖入目录。运行期间改动配置后，重新打开预览即可刷新。

## 当前目录与介绍

| 页 | 名称 | 介绍 | 初始价格 |
| --- | --- | --- | ---: |
| 1 | 经典 | 熟悉的墨绿，属于每一位入座者。 | 免费 |
| 1 | 星河 | 把璀璨星河，留在你的座位。 | 6,000 |
| 1 | 赤焰 | 以黑曜为甲，让余烬为你加冕。 | 12,000 |
| 1 | 霓虹 | 穿过霓虹夜色，留下你的光轨。 | 18,000 |
| 2 | 鎏金 | 墨绿藏锋，金色自有分量。 | 30,000 |
| 2 | 极霜 | 冰晶凝住锋芒，极光掠过桌沿。 | 16,000 |
| 2 | 幽海 | 潜入无声深海，让幽光替你发声。 | 20,000 |
| 2 | 龙珀 | 青玉藏龙，云纹之间自有威仪。 | 28,000 |
| 3 | 绯樱 | 一席夜樱，把锋芒藏进温柔。 | 14,000 |
| 3 | 时轮 | 黄铜齿轮转动，掌握自己的节奏。 | 24,000 |
| 3 | 蚀日 | 日冕划破暗夜，沉默亦有引力。 | 36,000 |
| 3 | 幻晶 | 万色折光，让每次亮相都不同。 | 32,000 |

## 组件与资产

- NamecardProductSO、NamecardCatalogSO：商品及目录配置。
- ShopPanel：分页、账户视图、确认窗口与购买／装备请求事件。
- ShopItemCard：名片图片、介绍和按钮状态。
- NamecardShopEditor：中文 Inspector 和独立 Editor 预览入口；演示代码不进入客户端／服务器构建。
- ShopPanel.prefab、ShopItemCard.prefab：面板与可复用卡片，已在 UIManager 注册。每页复用固定 4 个卡片实例。
- 图片在 Assets/Sprites/Namecards/；完整提示词和原图在 [美术记录](NamecardArtNotes.md)。

正式商城由 BeginOnline 显式接入服务器；服务端认证、报价版本和原子交易已实现。房间与牌桌使用 NamecardAppearance，在美术上方独立保留准备／行动边框、文字与状态图标。等待房间使用 NamecardRoomGrid 保持图片比例。

## UI 原型阶段验证（历史记录）

- Unity 2022.3.52f1c1 编译无新增 error。
- UIInteractionTest 与 UINavigationTest：72/72 通过。Test Runner job：629d7d32259744a7a3b5e9cfc8e3edf9。
- Play Mode 预览：四卡布局、首尾页边界、末页空位、购买确认与取消、购买后装备按钮、购买不自动装备、切回默认及预览不修改真实余额均通过。
- 金额上限与较长用户名已检查，余额不溢出；玩家名称允许省略。
- 以上为 UI 原型阶段的记录；当前真实购买／库存的一服三端、数据库及平台验证见 [最新验证记录](NamecardShopValidation.md)。


## 实际界面截图与最终检查

[第一页](NamecardShopUI/page-1-final.png) · [第二页](NamecardShopUI/page-2-final.png) · [第三页](NamecardShopUI/page-3-final.png) · [1366×768](NamecardShopUI/page-3-1366.png)

![商店第二页](NamecardShopUI/page-2-final.png)

- 三页 1920×1080 以及第三页 1366×768 均已实际查看；较窄窗口没有文本溢出。
- 用临时 SO 副本改动名称、介绍、价格、图片和目录数量，确认界面随配置更新；未修改保存的配置资产。
- 返回关闭缓存面板；重开回到第一页并清空旧账户视图，符合现有 UIManager 的缓存行为。
- 两个新增 prefab 和 UIManager 检查结果：Missing Script／Reference 为 0，ShopPanel 恰好注册一次。12 款商品均有图片、名称、描述，唯一默认款位于首位且免费。
- Game.unity 保持未修改，完成后退出 Play Mode，并恢复原先 1920×1080 Game View 设置。72 项测试之后仅调整了名片遮罩与文字内边距，调整后已做上述实际界面检查。
