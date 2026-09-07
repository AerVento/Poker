# 名片 UI 实机检查与首版调整

Unity 2022.3.52f1c1，2026-09-07。截图来自 Game.unity 的实际 Play Mode；画面使用本地构造的玩家状态，以便逐一展示全部美术，不代表线上账户。网络验证另行记录。

## 三档牌局

| 档位 | 独立预制体 | 名片尺寸（1920×1080 参考画布） | 排列 |
| --- | --- | --- | --- |
| 1–4 人 | Player4 | 480×180，8:3 | 1 列 |
| 5–8 人 | Player8 | 236×177，4:3 | 2 列 |
| 9–16 人 | Player16 | 236×88.5，8:3 | 2 列 |

经典、星河、赤焰、霓虹、鎏金、极霜、幽海、龙珀、绯樱、时轮、蚀日、幻晶，共 12 款 × 3 档均已逐一查看。8 人档采用各主题独立的 4:3 美术，4/16 人档采用 8:3 美术。

- 检查画面未发现姓名、筹码、行动、位置标记与装饰边框互相遮挡。
- 行动底条由绿色改成半透明深色，行动文字提亮，减轻与各主题的色彩冲突。
- 倒计时与本轮行动仍共用状态区域，互斥显示；16 人档仍保留两行信息。
- 长昵称自动缩小后省略；极端金额保留现有 K 格式并缩小字号。1366×768 的 16 人极端金额虽然不溢出，但字体较小，见压力截图。
- 本轮未修改名片图片、商城售价、商品身份或装备流程。

### 4 人：12 款分三组

![4 人：经典、星河、赤焰、霓虹](NamecardUIReview/game-4-styles-0.png)
![4 人：鎏金、极霜、幽海、龙珀](NamecardUIReview/game-4-styles-4.png)
![4 人：绯樱、时轮、蚀日、幻晶](NamecardUIReview/game-4-styles-8.png)

### 8 人：两组覆盖全部 12 款

![8 人：前八款](NamecardUIReview/game-8-styles-0.png)
![8 人：后四款及前四款](NamecardUIReview/game-8-styles-8.png)

### 16 人：12 款及四张重复款

![16 人全部款式](NamecardUIReview/game-16-styles-0.png)

## 房间

- 固定 8:3，参考画布实际格子约 341.53×128.08，三列、12 像素间距。
- 姓名在上方，房主与已准备标记在下方。用小型文字角标替换房主图标。
- 去掉列表大块底色，名片直接排列在房间背景上；状态说明与玩家区左对齐。
- 12 人可一屏完整显示。16 人仍使用滚动，末位可到达；牌桌设置与开始按钮固定在右侧。
- NamecardRoomGrid 继续响应可用宽度，卡片宽高比不随列数变化。滚轮灵敏度由 0.1 调至 32，使用实际滚轮事件确认可到达最后一位。

![房间 12 款](NamecardUIReview/room-12.png)
![1366×768 房间 16 人](NamecardUIReview/room-16-1366.png)
![房间滚动到底部](NamecardUIReview/room-16-bottom-1366.png)

## 排行榜

- 两列五行显示前十名，按从左到右、从上到下排序。
- 每张姓名名片为 256×96，严格 8:3；金额在右侧独立展示。
- 本人排名固定在列表下方，可展示榜外名次；刷新与返回保留。刷新冷却从收到响应后计算，避免网络时延使按钮提前可用而被服务端限流。
- LeaderboardEntry 新增 NamecardId，服务端从 player_accounts.equipped_namecard 读取装备 ID。空 ID 沿用默认名片；不改变数据库结构。
- 离线玩家的装备也会随排行榜同步，客户端通过 NamecardAppearance 呈现。

![排行榜](NamecardUIReview/leaderboard.png)
![排行榜长昵称与最大 uint 余额，1366×768](NamecardUIReview/leaderboard-stress-1366.png)

## 验证

- 相关 EditMode 测试 166/166 通过：GamePanelLayoutTest、UIInteractionTest、UINavigationTest、NamecardAppearanceTest、NamecardArtworkVariantTest、NamecardShopTest、HandSettlementAndLeaderboardTest。
- 最终作业：aa2236685bcd415e8cb06aaf486da05d。
- 新增验证覆盖：离线前十及榜外本人装备 ID、装备更新后刷新、Mirror 消息序列化往返。修正已有美术测试对状态区旧层级的访问。
- 场景与窗口：Game.unity，1920×1080、1366×768。正常昵称及长昵称、最大 uint 金额、弃牌与断线、房主及准备状态。
- 相关八个 prefab 检查 Missing Script / Missing Reference 为 0，Game.unity 场景无未保存改动。
- Windows x64 开发构建成功，0 error、2 warning，作业 build-8e4decc500；仅作本地验证，未发布。
- 本地一服三端：Logs/NamecardUIReview/namecards-d934b763，server.pass 与三个 client.pass 全部通过。每端完成两手，覆盖连接认证、建房入房、准备、下注推进、离房；前十离线账户装备的星河名片经服务端查询、Mirror 传输、UI 呈现及手动刷新后均保持 8:3，榜外本人名次与余额正确。
- 服务端最终余额与账本总额均为 5,530,000，活动手牌为 0。测试使用独立数据库，测试前的登录偏好已恢复。
- 网络测试首次发现刷新按钮在服务端冷却结束前可能恢复可用；修正为收到响应后再计时，本节记录的是修复后的完整重测结果。
- 全部结束后 Console error 为 0，退出 Play Mode；恢复测试前的 Linux Server 构建目标及 Full HD Game View。没有重测 Linux/WebGL 构建，也未单独重测意外断线后返回标题页。

![16 人长昵称与最大金额压力检查](NamecardUIReview/game-16-stress-1366.png)