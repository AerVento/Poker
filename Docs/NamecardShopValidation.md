# 名片商城与装备验证记录

日期：2026-09-06，Unity 2022.3.52f1c1。本文记录真实实现，原 UI 预览记录见 `NamecardShopUI.md`。

## 已实现

- 大厅「名片商城」，12 款（含经典默认款），每页 4 款，共 3 页；无筛选功能。
- 服务器报价、购买确认、永久库存、手动装备、免费换回默认、下架保留库存与装备入口。
- schema 5：账户装备字段、账户名片表、幂等请求结果表；购买扣款、ShopPurchase 账本、库存、结果在同一事务提交。
- 同账户同商品只收费一次，重试沿用操作 ID；旧装备请求不会覆盖后续换装。意外数据库异常沿用停服策略。
- 服务器拒绝未认证／被替换连接、过期或频繁请求、房间及活动手牌内的消费与装备。
- 私有库存仅返回本人；公开 RoomInfo.Namecards 随房间及牌局投影同步。牌桌和等待房间使用实际商品图片，旁观者同样可见。公开字典清除已离开的无关成员，保留仍在手牌中的断线成员。
- 等待房间按名片比例显示双列卡片并滚动；宽度不足时单列。牌桌独立叠加行动边框，保留准备、倒计时、弃牌变暗、断线及盲注标记。
- SO 作者工作流与自动构建导出，详见 `AddingNamecards.md`。

## 自动化测试

| 范围 | 结果 | Unity MCP 作业 |
| --- | --- | --- |
| 全部自有 EditMode，包含首批 15 项商店测试 | 352/352，0 跳过 | fb39be70973d406c94e1d643dff7a2b7 |
| 最后修正后的 18 项商店测试与 72 项 UI 回归 | 90/90，0 跳过 | a6188d8e9e3d469e8053195add69bd61 |

覆盖默认拥有、精确余额用尽、余额不足、不自动装备、重复购买、操作 ID 按账户隔离、旧装备重试、重开数据库、下架、价格变化、房间／手牌占用、排行榜降低、消费后 1,000 边界领取补助、schema 4 升级及公开序列化。身份测试直接调用正式服务端处理器，拒绝未认证、顶号旧连接、重复序号、频率超限和房间内请求。

分别在库存、账本、请求结果插入时使用 SQLite trigger 注入异常，确认余额、库存、版本和结果不会部分提交，GuardedAccountRepository 进入故障状态；装备结果写入失败也回滚原装备。最初两个失败来自测试夹具（一人开手、尝试在旧 SQLite 中删除列），修正为两人开手及真实构造 schema 4 后通过。

## 一服三端

`Logs/NamecardShop/shop-4fa31b33` 使用新 Windows 开发构建、独立测试数据库与正式网络／UI 处理器。三个账户各从 10,000 购买 6,000 的星河，确认取消不扣款，购买后变为装备但不自动穿戴，再切换星河／经典。客户端 0、1 装备星河，客户端 2 保持经典。

随后完成三手：建房、加入等待房间、准备、下注、阶段推进、旁观／参战切换、公开摊牌、离开重入与返回大厅。三端逐次核对公开外观 ID、实际 Image Sprite 和行动边框。旁观端同样通过。最终账户总额与账本均 12,000，手牌内部净额 0、活动手牌 0，无对账差异。

该运行产生四份 `.pass`，并保存各端 `shop-*-purchase.checked` 和 `shop-public-*.checked`。运行器恢复测试前的登录偏好，没有使用真实玩家数据库。

## 视觉与工程检查

实际进入 Game.unity Play Mode，检查等待房间的双列／最后一行滚动、牌桌多款装备与最大余额文字、大厅入口，以及 1920×1080、1366×768。视觉截图为本地构造显示数据，真实消费与联机结论来自上面的独立程序测试。

截图位于 `Docs/NamecardShopUI/`：`waiting-equipped-1920-1.png`、`waiting-equipped-1366.png`、`table-equipped-1366.png`、`lobby-shop-entry.png`。初次等待房间预览遗漏本人身份产生一次测试显示异常，修正预览数据后正常；不计为正式联机失败。

## 构建与限制

最终 Windows 构建成功（build-d0c6960894，0 error），最终一服三端 shop-b7dabdce 再次完成全部三手，并验证离房重开商城的库存和装备。Linux Dedicated Server 构建成功（build-bb69709087，0 error）；WSL 启动读取商品目录并建好名片表，WebSocket HTTP 101、SQLite integrity_check=ok、正常退出码 0。用户随后要求暂停后续构建，因此没有进行本功能的 WebGL 构建或浏览器联机验证。

本功能为静态名片外观，没有动画或远程热更新。客户端与服务器须配套更新，不能让旧客户端凭空显示新图片。EditMode 验证了重新打开数据库后的持久化；未对正式玩家数据库进行升级或消费。

## 最终 UI 截图

用户要求仅查看界面，后续不再构建。Unity Play Mode 1920×1080：shop-final-page1.png、shop-final-page3.png、waiting-final.png、table-final.png，均位于 Docs/NamecardShopUI，使用明确的示例账户及牌局数据。

## 美术修订与 4:3 样例（2026-09-07）

- 11 款宽版 PNG 逐张目检并核验为 2048 × 768、RGB、不透明主题背景；工程副本与选用生成原图 SHA-256 相同。Unity 导入的 11 张纹理均为 1024 × 384，艺术资源尺寸、Sprite、Full Rect、平台上限、Mip Maps 与 Clamp 检查通过。
- 星河 4:3 样例原图为 1448 × 1086，拒绝了先前错误输出的 1254 × 1254 正方形稿。最终 Unity Sprite／Texture 实测为 1024 × 768。原 PNG 未经脚本改画或拉伸。
- 用户确认牌桌 UI 有另一个并行任务，本任务随后只继续美术、图片引用与规范文件，不再修改其玩家数量布局或进行运行时 UI 截图。本轮未构建。
- 转为只做美术之前曾运行 80 项 UI／导航／名片测试（任务 ID c3c26d1f30c141349ed5f0374ee6e9b1），其中 11 项 GamePanel 测试发生 NullReferenceException。当时工作区正在更新玩家 prefab 和布局；一项失败用例随后单独调用通过，未完成全组复跑与归因，因此不能将该次运行记为通过。UI 最终验收应由正在修改布局的任务完成。
- 本节验收结论仅针对美术文件与导入设置；最终格子上的文字、D／SB／BB 和准备等状态叠加效果仍需要配合当前 UI 布局验证。

## 2026-09-07：11 款 4:3 资源与 SO 接口

- 内置 Image 生图补齐 10 款，加上星河共 11 款精确 4:3：源 PNG 均为 1448×1086、RGB、不透明，Unity 实际纹理均为 1024×768；源文件未经外部图像后处理。宽版 1024×384 引用保留。
- 每个商品 `_artwork4x3` 指向本款紧凑 Sprite；通过 `NamecardCatalogSO.GetArtwork(id, targetAspect)` 解析，现有 `SetNamecard(id)` 接入 `NamecardAppearance` 自动选图。未知 ID、默认款、缺少单个比例、未测量格子均有回退。
- 共享外观组件使用 Image.preserveAspect；Content 以实际图片边界计算安全区。GamePanel 三档、旧版 Player 和 RoomPanel 的 5 个外观组件均已补 Content 引用，并逐项核查 artwork/background/outline/catalog/content 无空引用。
- Unity 2022.3.52f1c1 EditMode：NamecardArtworkVariantTest、NamecardAppearanceTest、NamecardShopTest、GamePanelLayoutTest、UIInteractionTest、UINavigationTest 共 **129/129 通过**，作业 `371f53d85b024e29a5c8ca95e1382326`。覆盖全部 11 个 ID 的双图绑定、独立图片、1024×768 导入规范、缺图回退、三个 prefab 接口、格子缩放后重新选图、安全区和高亮状态。
- Game.unity 的本地 Play Mode 展示确认：8 人档实际使用各款 `*-4x3-v1`，4 / 16 人档使用宽图；已目检完整主题背景和细边框。大额筹码在紧凑安全区内曾触发省略，随后补调金额字体自动缩小下限，并实测 8 人档 `$4294967.3K` 的 TMP `isTextTruncated=false`。
- 本次验证期间，另一项牌桌布局任务继续修改三档比例和倒计时/操作区域；本目录的 `namecards-*` 截图是资源接入过程截图，不作为该布局任务最终验收图。当前布局的最终截图与验证请看 `Docs/GamePanelLayouts.md`。本轮没有覆盖其后续排版修改，也没有宣称最后一版排版全流程通过。
- 最后只读资源核查：22 张 Sprite 尺寸和 ID 引用正确，5 个 prefab 的外观字段引用齐全；11 张紧凑 PNG 哈希和 meta 一致；新增/修改的本轮 7 份 C# 保持 UTF-8、CRLF。Console error 查询为 0。
- 本轮未构建，未重跑真实服务器连接、入房、开局、结算与断线流程；外观资源和选图接口没有修改服务器协议、余额或商品价格。

使用说明见 `AddingNamecards.md`；双图预览见 `NamecardGallery.md`；精确生成记录见 `NamecardPreviews/prompts-v6-4x3-collection.json`、`manifest-v6-4x3-collection.json`。
