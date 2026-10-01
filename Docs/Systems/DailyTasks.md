# 每日任务

每日任务由服务器按配置分配、计进度和发奖。客户端只显示服务器快照，并提交领取、删除、刷新请求。

## 代码位置

| 部分 | 位置 |
| --- | --- |
| 任务类型、奖励、配置解析（`TaskFactory`） | `Assets/Scripts/Game/Tasks/` |
| 牌局事实（牌型、结算）`HandFacts` | `Assets/Scripts/Game/Tasks/HandFacts.cs` |
| 分配、进度、领奖、快照 `ConfiguredTaskService` | `Assets/Scripts/Network/Tasks/` |
| 结果枚举、快照结构、业务日 `TaskPeriod` | `Assets/Scripts/Network/Tasks/DailyTaskTypes.cs` |
| 网络处理 | `Assets/Scripts/Network/NetworkRoom/NetworkRoom.DailyTasks.cs`、`NetworkRoom.ConfiguredTasks.cs` |
| 存储 | `Assets/Scripts/Network/Accounts/Storage/SqliteAccountRepository.Tasks.cs` |
| 面板 | `Assets/Scripts/UI/DailyTaskPanel/` |
| 配置编辑器 | Unity 菜单 `Poker/每日任务/配置编辑器` |

## 配置

服务器读取三份 JSON，默认目录为仓库或构建产物旁的 `Configs/DailyTasks/`。可用启动参数 `--task-config <目录>` 或环境变量 `POKER_TASK_CONFIG_DIR` 指定其他目录，命令行优先。三份文件的 `schemaVersion` 必须为 `2`，`revision` 必须相同。

| 文件 | 内容 |
| --- | --- |
| `TaskDefinitions.json` | 全部任务定义：`id`、`name`、`type`、`params`、`maxCompletions`（每个实例最多完成几轮） |
| `FixedTasks.json` | 固定任务：`taskId`、`enabled`、`sortOrder` |
| `RandomTasks.json` | 随机池容量 `capacity`，以及每项的 `taskId`、`enabled`、`weight`、`validityDays` |

- 同一任务不能同时出现在固定和随机表中。引用不存在、参数不合法、未知类型或道具、重复 ID 等任何错误都会使服务器启动失败，不会退回硬编码配置。
- 构建时会校验三表并复制到产物的 `Configs/DailyTasks/`。修改后需重启服务器；新配置只影响之后分配的任务，已分配的实例保留当时的定义快照。
- 推荐用配置编辑器修改：它按字段编辑、预览目标文字，并用与服务器相同的校验器“校验并保存三表”。

### 奖励

`params` 的第一项是每一轮的奖励，可以写成整数，也可以写成 `{"chip": 1000, "items": [{"itemId": "rotten-egg", "count": 1}]}`。字段名 `chip` 是历史名称，**实际发放的是美金**。道具必须存在于服务器物品目录，并受该道具的持有上限约束。

### 任务类型

| type | params | 何时计数 |
| --- | --- | --- |
| `DirectClaim` | `[奖励]` | 领取成功即完成一轮 |
| `DirectClaimWhenLesserThan` | `[奖励, 阈值]` | 领取时总资产严格小于阈值 |
| `DirectClaimWhenAtMost` | `[奖励, 阈值]` | 领取时总资产不高于阈值 |
| `PlayHands` | `[奖励, 手数]` | 每正常结算一手 |
| `FormPattern` | `[奖励, 牌型, 手数]` | 该手出现指定牌型时立即计数 |
| `WinHands` | `[奖励, 手数]` | 正常结算且赢得任一奖池（含平分） |
| `WinWithPattern` | `[奖励, 牌型, 手数]` | 获胜且本手曾出现指定牌型 |
| `CountHands` | `[奖励, 手数, 条件数组]` | 同一手同时满足全部条件 |
| `AllObjectives` | `[奖励, 子目标数组]` | 各子目标分别累计，全部达标完成一轮 |

- `CountHands` 的条件：`{"type": "PatternObserved", "params": ["FullHouse"]}`、`{"type": "Won", "params": []}`、`{"type": "HandSettled", "params": []}`。
- `AllObjectives` 的子目标：`{"id": "play", "type": "CountDistinctHands", "params": [手数, 条件数组]}`。
- 牌型：`Pair`、`TwoPair`、`ThreeOfAKind`、`Straight`、`Flush`、`FullHouse`、`FourOfAKind`、`StraightFlush`（不含高牌）。
- 直接领取类任务放进随机池时，`validityDays` 必须为 1。
- 新增任务类型需要在 `TaskFactory` 中显式注册，并同步更新配置编辑器。

## 分配与生命周期

- 业务日以 UTC+8 的 00:00 为界，与服务器所在时区无关。
- **固定任务**：每个业务日为所有账户生成一份新实例，不占随机名额。当天没领完的直接领取次数不会累积到第二天。
- **随机任务**：当天第一次登录（或在线跨过零点）时，按权重不放回地抽取任务，补满空余名额。打开面板不会触发分配。同一天释放出的名额当天不再补。
- 每次向空名额分配随机任务都附带一次免费刷新：刷新会换成另一项任务，进度清零，有效期重新计算；领过任意一轮奖励后不能再刷新。
- 随机任务可以删除：删除前自动领取当前可领的奖励，放弃剩余进度。删除释放的名额要到下一个业务日才补。
- 到期后不再增加进度。已完成但未领取的轮次会保留，任务标为过期（不占名额），领完后移除。
- 多轮任务必须先领取当前轮的奖励，才开始累计下一轮；等待领取期间的新进度不计入。同一手牌不能为同一个任务贡献多轮。

## 进度来源

牌局只发布事实，不知道任务的存在：翻牌、转牌、河牌公开后发布本阶段可组成的牌型；结算成功后发布“已结算、是否获胜、本手出现过的牌型”。任务服务收到事实后，只对当时有效、处于累计中的实例计数。牌局取消不会撤回已计入的进度。事实与进度分别在各自的 SQLite 事务中持久化，重复投递按事件 ID 去重。

## 领取

- 客户端提交实例 ID 和轮次序号，服务器据此做到幂等：重试不会领到下一轮。
- 登录后在任何页面都可以领取，包括牌局中；奖励为美金，不影响正在进行的一手的筹码。
- 带余额条件的任务每领一轮都重新检查条件。
- 美金入账、道具发放、领取记录和任务状态在同一个事务中提交；余额或道具达到上限时整笔失败。

## 界面

每日任务面板可从大厅、等待房间和牌桌打开，一次显示一张任务卡，可拖动、滚轮或左右按钮翻页。卡片显示目标、各子目标进度、总轮次、奖励和截止时间；随机任务右上角在可刷新时显示刷新图标，否则显示删除图标。列表按每页 40 项分页读取，并以快照版本号防止混用新旧数据。
