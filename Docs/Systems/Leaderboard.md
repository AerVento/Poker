# 排行榜

排行榜从大厅进入，包含“富豪榜”和“服役榜”两个页签，默认显示富豪榜。两个页签共用同一次服务器查询，切换不再请求；刷新或切换后列表回到顶部。

## 规则

| 榜单 | 排序依据 | 相同时 |
| --- | --- | --- |
| 富豪榜 | 总资产（美金 + 筹码）降序 | 注册时间早者在前，再按账户 ID 的二进制顺序 |
| 服役榜 | 服刑年数降序：恶魔契约 × 1 + 大恶魔契约 × 10 + 撒旦契约 × 100 | 同上 |

- 两榜各显示前 10 名，另有一行固定显示本人的真实名次，即使不在前 10 名。
- 包含离线账户与零契约账户。年数按数据库当前库存计算，还款消耗契约后年数随之减少。
- 查询在一个只读 SQLite 事务中完成（`Storage/SqliteAccountRepository.Leaderboard.cs`）。客户端只收到公开名字、名次、资产或三种契约数量、名片 ID、服刑年数和“本人”标记。

## 显示

- 富豪榜使用玩家装备的名片外观；服刑年数达到 100 / 500 / 1000 时替换为对应的恶魔名片，姓名显示为红字黑描边（见 [名片](Namecards.md)）。
- 服役榜使用独立的 `ServiceLeaderboardRow` prefab 和地狱契约背景 `Assets/Sprites/Leaderboard/infernal-archive.png`（生成提示词在 `ArtSources/`）。三种契约图标只出现在表头，行内只显示数量。
- 列表为纵向 ScrollRect，本人行不随列表滚动。

代码：`Assets/Scripts/Network/Leaderboard/Leaderboard.cs`、`NetworkRoom/NetworkRoom.Leaderboard.cs`，界面 `Assets/Scripts/UI/LeaderboardPanel/`。
