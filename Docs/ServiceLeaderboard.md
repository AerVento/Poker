# 富豪榜与服役榜

## 2026-09-09 更新

服刑榜背景改为根 Canvas 下等比覆盖全屏，允许裁切边缘以适应非 16:9。年数继续由当前契约库存计算，因此还款消耗契约会减少对应的服刑年数。

实现计划见 [UIPolishAndContractRepaymentPlan.md](UIPolishAndContractRepaymentPlan.md)。

- 标题页进入排行榜时默认显示富豪榜；两个页签共享同一次服务器查询，切换即时生效，不额外触发请求限流。
- 两榜沿用前 10 名与固定本人行，列表使用 Vertical Layout Group、ScrollRect、Viewport 裁剪和竖向滚动条。每位玩家占一行；刷新、切换页签后回到顶部。
- 服役年数 = `devil-contract` 数量 × 1 + `archdevil-contract` 数量 × 10 + `satan-contract` 数量 × 100。以数据库当前库存为准，不计算消费历史或其他物品。
- 包含离线及零契约账户。年数降序；相同时按注册时间升序、账户 ID 的二进制顺序升序。本人即使不在前 10 名，也显示真实名次与总年数。
- 服役查询在原排行榜的只读 SQLite 事务内完成，无 schema 修改。客户端仅接收公开名字、名次、三种契约数量和本人标记；年数用 `ulong` 计算，支持三个 `uint.MaxValue` 库存合计 476,741,369,745 年。
- `LeaderboardSnapshot` 增加服役榜投影，使用 Mirror Weaver 自动序列化；`Serialization.cs` 的自定义 JSON 读写仅涉及房间类型，无须新增手写序列化。发布时客户端与服务端须使用同版协议重新构建。

## 视觉资源

服役榜使用独立 `ServiceLeaderboardRow` prefab 和地狱契约背景，配合现有三种契约图标、猩红排名印记、金色年数。富豪榜沿用名片外观；服役达到 100/500/1000 年时，由客户端覆盖为对应恶魔名片。两榜姓名均使用鲜红色与黑色描边，具体规则见 InfernalNamecards.md。

三种契约图标仅在固定表头出现，作为数量列标题；列表及本人行只显示对应数量，不带“×”前缀。页面不展示每种契约的年数换算说明，具体信息沿用物品描述。

三张契约图标仅调整 TextureImporter：启用 mipmap 和 Trilinear，消除缩为小图标时的高频锯齿；原始图片及 GUID 不变。

背景：`Assets/Sprites/Leaderboard/infernal-archive-v1.png`。使用内置 image_gen 生成，原始输出保持不变并复制到项目中；Unity 导入为无 mipmap 的 Sprite。

最终生成提示词：

```text
Use case: stylized-concept. Asset type: a production 16:9 full-screen background illustration for a Chinese multiplayer poker game's hell-contract service-years leaderboard. Create a new background, not a UI mockup. Style: polished hand-painted cartoon fantasy RPG item illustration, bold black-red devil horns, sharp molten-gold metal trim, crimson infernal flames, dark charred parchment and obsidian, inspired by demon contract scrolls with black iron chains, ruby wax seals, red tridents and gilded pointed corners. Composition: landscape 1920x1080 or equivalent 16:9. Crucial: central 80% width from x=10% to 90%, and y=12% to 95%, must remain extremely dark burgundy/black, calm, low detail and empty to host actual UI leaderboard rows and text. All bright flames, chains, gilded devil faces, horns and red trident silhouettes stay in narrow left and right margins and far corners. Stylish symmetrical gate to an infernal archive; understated gold trident emblem near top center only. Warm ember glow at outer bottom corners. No writing, no letters, no numbers, no table rows, no buttons, no watermark. The actual text will be rendered separately in Unity. Rich crisp stylized rendering matching game inventory icons, not realistic horror, no gore.
```

## 验证记录（2026-09-07～08）

- Unity 2022.3.52f1c1 编译并运行 Game 场景；排行榜 panel、两种 row prefab 无 Missing Script / Missing Reference，退出预览后未保存场景改动。
- 共享 EditMode 作业 `42e9998df16d4744927803a441dcda2b` 完成 567 项，新增 4 项 ServiceLeaderboard、已有排行榜与导航测试通过。本次作业存在 3 项其他功能测试失败，不能视为全套通过；相关任务另行修正及补测。
- 最终共享 EditMode 作业 `01900dae7436414496ad3645d1dc402c`：567/567 通过，0 失败、0 跳过，包含全部新增服役榜测试及排行榜、导航回归。
- 1920×1080 和 1366×768 视觉检查：两个主题、长玩家名、省略显示、三个最大 uint 数量、476,741,369,745 年总额和固定本人行均可读。截图位于 `Logs/ServiceLeaderboard/Visual/`，使用本地示例数据，不代表真实玩家库存。
- 实际 EventSystem 射线命中 Viewport，滚轮将位置从 1 移至约 0.628；内容高度 1214、可视高度 536。滚到底部时第 10 行完整可见，本人行不随列表滚动。切换恢复顶部，关闭重开默认富豪榜。
- 复用最终 Windows 构建 `build-ece9d259d2`（0 error、4 warning）完成一服三端 `leaderboard-8635657e`。3 个客户端均完成建房/入房、准备、下注、阶段推进、两手结算和离房；6 份榜单报告覆盖结算前后、离线服役顺序、0 库存本人、名片外观、即时切换、手动刷新及滚动。四进程均通过，余额与账本总额 5,530,000 一致，活动手牌为 0。日志：`Logs/ServiceLeaderboard/leaderboard-8635657e/`；测试使用隔离数据库，结束后恢复原登录偏好。
- 本轮未单独执行 Linux server 和 WebGL 构建；平台启动逻辑未修改。

### 2026-09-08：表头与数量显示精简

仅调整两个 UI 脚本与排行榜/服役行 prefab，无统计或协议变更。Unity 编译无 CS error，prefab 无缺失引用；1920×1080、1366×768 实际预览确认只出现 3 个表头图标、本人行与列表数字对齐、最大 uint 数量可读，换算说明及行内图标已移除，滚轮仍可到达第 10 名。`UINavigationTest` 作业 `77846f377267416b8af0c7b72c876813`：54/54 通过。本次展示精简未重新构建或重跑多人网络测试。最新预览：`Logs/ServiceLeaderboard/Visual/service-column-headers.png`。
