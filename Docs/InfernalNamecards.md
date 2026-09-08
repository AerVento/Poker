# 恶魔服役名片

## 当前交付

三款已加入现有名片商城，沿用通用 ShopItemCard 与原面板布局，没有专属分区、额外标签或新增面板。当前目录共 16 款，三款位于第 4 页，与幻夜双影同页。

| 名称 | 名片 ID | 触发条件 | 商品 SO |
| --- | --- | --- | --- |
| 狱契之印 | devil-service | 地狱服役满 100 年 | Assets/Resources/SO/Shop/Products/devil-service.asset |
| 深狱领主 | archdevil-service | 地狱服役满 500 年 | Assets/Resources/SO/Shop/Products/archdevil-service.asset |
| 撒旦敕印 | satan-service | 地狱服役满 1000 年 | Assets/Resources/SO/Shop/Products/satan-service.asset |

六张图片位于 `Assets/Sprites/Namecards/{id}-wide-v1.png` 和 `{id}-4x3-v1.png`，原样复制自已认可预览。Unity 导入为 Sprite / Single / Full Rect，Max Size 1024、无 Mip Maps、Clamp、NPOT None；运行时宽版 1024×384、紧凑版 1024×768。

三份独立展示卡片预制体位于 `Assets/Prefabs/UI/Namecards/{id}.prefab`，复用现有 ShopItemCard 的结构并保存对应图片、名称、介绍及禁用按钮。它们是独立查看用的展示预制体；商城分页仍复用原有四张通用卡片并读取商品 SO，后续修改 SO 不会自动更新这些独立展示预制体。

## 配置语义

- **上架**：保持既有含义。普通商品下架后，未拥有者看不到，已拥有者仍保留库存和装备入口。
- **可购买**：新增 `_canPurchase`，默认为 true，旧普通商品无需迁移。三款取消勾选后继续按上架配置展示，按钮禁用并显示“不可购买”，仍显示配置价格。三款当前配置价格为 0；用户尚未指定其他标价。当前该配置同时禁用此卡片的手动操作入口，即使库存中有它也不会从此按钮发出装备请求。
- 三款都是上架、不可购买、非默认款。商品介绍使用文学化文案，阈值与客户端强制显示规则见下文。
- 狱契之印：“服役百年，狱火将契约烙入灵魂。”
- 深狱领主：“服役五百年，锁链铸成深狱的权印。”
- 撒旦敕印：“服役千年，万狱之火俯首于不灭的王冠。”
- 服务器目录导出沿用现有协议，将 `Listed` 写为 `IsListed && CanPurchase`，三款在内置服务器目录中均为 false，因此不会被当作售价 0 的免费商品出售。客户端可单独展示本地“上架且不可购买”的商品，即使没有服务器报价。没有更改服务器购买处理器或网络消息格式。
- 本次未修改任何外部线上目录。已有部署的目录不会被内置 JSON 自动覆盖；后续发布不要把这三款的外部 `Listed` 配成 true。

## 客户端强制显示规则

以现有服役榜口径为准：恶魔契约数量 × 1 + 大恶魔契约数量 × 10 + 撒旦契约数量 × 100；读取服务器当前库存，使用 ulong。当前项目没有死亡后的实际计时系统，本次未新增死亡状态或计时器。

100≤年数<500 为狱契之印，500≤年数<1000 为深狱领主，年数≥1000 为撒旦敕印；达到多个阈值时采用最高档，包含等于。

- 客户端 InfernalNamecard.Resolve 统一选择显示名片，房间座位、房间旁观者、牌局玩家及财富榜（含本人固定行）均接入。商品展示仍展示所选商品，便于浏览。
- 达到阈值的姓名使用鲜红色 #FF1818 与黑色 SDF 描边（宽度 0.2），服役榜姓名也应用。每个姓名独立克隆材质，保留共享字体材质；列表复用为普通玩家时恢复原色、渐变和材质，销毁时释放临时材质。
- 数据库 EquippedNamecardId 和原有库存不变；更换实际装备不影响强制外观。这里实现客户端显示覆盖，没有新增服务端装备锁或授予商品所有权。库存折算年数若被服务器调整到阈值以下，会恢复实际装备的外观。
- 房间新增 ServiceYears（玩家名 → 公开年数），批量读取当前成员的年数，与原 Namecards 一起清理离房数据；断线且仍参加本手的玩家保留最后外观。ClientGameInfo 沿用 RoomInfo 投影。
- 财富榜新增 LeaderboardEntry.ServiceYears，前十名与榜外本人均在原只读事务内读取；不依赖服役榜前十名单，因此离线、两榜排名不同的玩家也正确。
- RoomInfo 沿用 Serialization.cs 的 JSON 序列化，排行榜由 Mirror Weaver 生成字段读写；没有发出无关物品库存、账号凭据或私有手牌。客户端和服务端需要一同重新构建发布。

## 验证

- Unity 2022.3.52f1c1，Game.unity；脚本已成功编译，名片全目录纹理配置检查通过。
- EditMode：NamecardArtworkVariantTest、NamecardAppearanceTest、NamecardShopTest，共 47 项通过，0 失败。
- 在 Unity 中临时实例化现有商城及三份展示预制体，验证在线无报价仍显示、拥有/未拥有时按钮均不可购买、强制恢复按钮 interactable 也不会触发回调、直接选择商品不会发出购买或装备请求、宽/紧凑图解析正确。临时对象已清理。
- 验证其余 13 款保留可购买默认值，普通下架款保留“未拥有隐藏，已拥有可见”的语义。
- 商城使用本次预览示例账户，不扣真实余额。预览不替代真实 server + 两个 client 的完整购买、重连或房间流程验收；本次没有执行这些流程。
- 进入任务前 Console 已有本地连接拒绝 SocketException；不将其计作本次新错误，也不声称全局历史 Console 无 error。

![现有商城第 4 页](NamecardPreviews/infernal-shop-v1.png)

### 客户端覆盖验证（2026-09-08）

- Unity 2022.3.52f1c1 成功编译；专项 EditMode 作业 9b4e831fd49e4958abec65d051c1ac18：100/100 通过。包含 InfernalNamecardTest、NamecardAppearanceTest、NamecardArtworkVariantTest、NamecardShopTest、HandSettlementAndLeaderboardTest。
- 覆盖 99/100/499/500/999/1000 边界、最大 ulong、实际玩家/排行榜 prefab 的图片覆盖和姓名样式、共享材质不变、复用还原和材质释放、离线与榜外本人、房间旁观者及公开网络数据往返。
- Game.unity 实际 PlayMode 使用现有财富榜面板渲染 100/500/1000 年和 99 年对照行；预览使用临时示例数据，退出后不保存场景，不改变真实账号。截图 infernal-name-style-v1.png。
- 独立进程诊断 leaderboard 已更新为检验三档恶魔名片与红字黑描边；contracts 场景增加房间、旁观、牌局实际外观及年数检查。多人回归结果由随后统一回归记录补充。

![客户端强制外观示例](NamecardPreviews/infernal-name-style-v1.png)
### 独立进程契约回归（2026-09-08）

Windows 新版构建 build-3dea3e17d1 成功，0 error。运行 Logs/DailyTasks/contracts-1e6710fb：1 个服务器与 3 个客户端全部通过。三个客户端完成 3 手牌，覆盖旁观、满桌拒绝、离房重入、回桌参战、离房；infernal-room-0/1 与 infernal-game-0/1/2 检查确认公开年数及实际恶魔名片、红字黑描边生效。账户余额与账本合计均为 111,000，活动手牌、账本差异均为 0。该场景覆盖契约折算 1/10/100 年，排行榜三档网络回归另行记录。
### 独立进程排行榜回归（2026-09-08）

Logs/DailyTasks/leaderboard-4dccc7d7：1 个服务器与 3 个客户端全部通过，完成建房、入房、准备、下注、阶段推进、两手结算及离房。6 份 leaderboard 检查记录覆盖结算前后，验证离线玩家在 100/500/1000 年各档强制覆盖原 galaxy 名片、姓名鲜红色及黑色描边、两榜切换、本人固定行、刷新与滚动。账户余额与账本合计均为 5,530,000，活动手牌及账本差异均为 0。

本轮全量 EditMode 作业 4ecb28eabad44a25ac62e7db45c95a00：598/598 通过，0 失败、0 跳过。名片专项及多人回归完成；后续 WebGL / DS 构建由统一发布任务继续处理。