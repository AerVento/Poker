# 每日任务系统实施说明

## 2026-09-09：任务槽位、免费刷新与完成清理

以下规则替代后文旧版随机任务删除及历史奖励保留说明：

- 每次系统向空槽位分配随机任务时赠送一次免费刷新；跨日保留任务不恢复次数。刷新排除原任务，允许抽到其他槽位已有的任务；进度归零，有效期从新分配时间按 UTC+8 业务日计算。没有替代候选时保留原任务和次数。
- 领取过任何一轮奖励即失去刷新资格。可刷新时右上角只显示刷新图标，否则显示 X；固定任务无此操作。刷新/删除会提示放弃剩余进度，并沿用当前可领奖励自动领取；全部完成且奖励领完时删除免确认。
- 每天分配随机任务前清理全部完成的任务，待领奖励尽力自动领取。领奖受阻则保留并标记 `OutDated`，不占槽位、不参与进度累计；玩家领取后立即删除。已到期的未完成任务也只保留其已达标的待领奖励。
- 同日删除、完成或领取不会补空位；只有用免费刷新替换任务会立即分配。刷新后的任务不能再次刷新；奖励、替换和次数消耗同事务保存，重复请求不会再换一张。
- 老任务首次加载时分配稳定槽位；已领过奖励的旧任务不能刷新，未领过的旧任务获得一次刷新。任务文档持久化 `SlotId`、`RefreshUsed`、`OutDated`；快照新增 `CanRefresh`、`SlotId`、`OutDated`，删除请求的 `Refresh` 区分替换，响应的 `Refreshed` 标记操作类型。Mirror 自动序列化并有往返测试，客户端/服务端需一起更新。


2026-09-08。实现说明及验证结果。

## 配置入口

Unity 菜单 **Poker → 每日任务 → 配置编辑器**。窗口分为总任务列表、固定任务、随机任务三个页面，编辑后“校验并保存三表”。默认保存到仓库 `Config/DailyTasks`，服务器重启后新分配任务使用新版本。

服务器读取以下三个文件，三者必须都是 `schemaVersion: 2`，`revision` 相同：

- `TaskDefinitions.json`：全部任务定义，包含 ID、名称、任务类、参数、每实例可完成次数。
- `FixedTasks.json`：每天刷新的固定任务引用、启用状态和显示顺序。
- `RandomTasks.json`：随机任务总名额，以及每项引用的启用状态、相对权重、有效业务日数。

可通过 `--task-config <绝对或相对目录>` 或环境变量 `POKER_TASK_CONFIG_DIR` 指定服务端目录，命令行优先。Windows/Linux 构建会把默认三表复制到可执行文件旁 `Config/DailyTasks`。构建前与服务器启动时都会校验。修改配置后先校验再重启；不支持运行中热更新。

缺文件、未知类或参数、未知任务引用、重复 ID、同时进入固定和随机池、无效权重/期限/次数、未知奖励道具等均报错，服务器不会偷偷使用硬编码配置。

当前默认内容：每日奖励每轮 1000 筹码、每日最多 3 轮；东山再起余额不高于 1000 时领取 2000、每日 1 轮；随机名额 2，候选为两对十手（1500、1 轮、权重 60）和葫芦三次（每轮 500、3 轮、权重 40），均有效 3 个业务日。

## 完整任务类与参数

参数中的 `reward` 可以写一个无符号筹码整数，也可写 `{"chip":1000,"items":[{"itemId":"道具稳定ID","count":1}]}`；每一轮发同一份奖励。普通道具 ID 必须存在于服务端物品目录。名片外观发放属于独立发放 API，本期任务奖励没有自动接入外观名片目录。

| type | params | 计数时机 |
| --- | --- | --- |
| `DirectClaim` | `[reward]` | 领取成功才完成一轮 |
| `DirectClaimWhenLesserThan` | `[reward,threshold]` | 领取时余额严格小于阈值 |
| `DirectClaimWhenAtMost` | `[reward,threshold]` | 领取时余额不高于阈值 |
| `PlayHands` | `[reward,target]` | 正常结算，不要求获胜 |
| `FormPattern` | `[reward,pattern,target]` | 牌型事实到达时立即计数 |
| `WinHands` | `[reward,target]` | 正常结算且赢任意奖池或平分 |
| `WinWithPattern` | `[reward,pattern,target]` | 正常获胜，使用本手曾形成的牌型 |
| `CountHands` | `[reward,target,conditions]` | 同一手满足所有 AND 条件 |
| `AllObjectives` | `[reward,objectives]` | 各子目标分别累计，全达标完成一轮 |

支持牌型：`Pair`、`TwoPair`、`ThreeOfAKind`、`Straight`、`Flush`、`FullHouse`、`FourOfAKind`、`StraightFlush`。按已确认的数量门槛包含，不开放高牌。

例如“玩 10 手且每手曾组成两对”：

```json
{"id":"two_pair_10","name":"两对十手","type":"FormPattern","params":[1500,"TwoPair",10],"maxCompletions":1}
```

例如“曾组成葫芦并最终获胜，共 2 手，每实例可完成 3 轮”：

```json
{"id":"full_house_wins","name":"葫芦获胜","type":"WinWithPattern","params":[1000,"FullHouse",2],"maxCompletions":3}
```

例如“同一手先后曾形成葫芦和四条”：

```json
{"id":"two_patterns","name":"葫芦到四条","type":"CountHands","params":[1000,1,[{"type":"PatternObserved","params":["FullHouse"]},{"type":"PatternObserved","params":["FourOfAKind"]}]],"maxCompletions":1}
```

例如“正常完成 10 手；另外曾组成一次葫芦”，两个子目标分别累计：

```json
{"id":"play_and_pattern","name":"十手与葫芦","type":"AllObjectives","params":[1000,[{"id":"play","type":"CountDistinctHands","params":[10,[{"type":"HandSettled","params":[]}]]},{"id":"pattern","type":"CountDistinctHands","params":[1,[{"type":"PatternObserved","params":["FullHouse"]}]]}]],"maxCompletions":1}
```

编辑器会显示命名字段、目标文字预览和实际 JSON，无需记住 params 的位置。配置中不使用反射加载任意类型；新增类型需要显式加入 `TaskFactory`。

## OOP 与职责

`Game.Facts` 保存服务端牌桌事实和牌型结构识别。`GameManager.Facts.cs` 在翻牌前结束、公共牌完整揭示、成功结算时发布事实。该模块没有任务 ID、任务配置或玩家任务状态。

`Game.Tasks` 是普通 C# 领域层，`GameTask` 抽象与上述九种具体任务类负责资格和目标规则，`IHandCondition` 与 `ObjectivePlan` 负责组合；奖励值对象 `Reward` 支持受检相加并合并同 ID 道具。定义由工厂在加载时从 JSON 编译成强类型对象，不在每次领奖时解释 object 参数。

`ConfiguredTaskService` 管理任务实例、固定/随机分配、有效期、轮次、幂等与快照投影。`GetReward()` 仅描述奖励，实际 `Claim()` 在服务端事务中发奖，避免 UI 或普通任务对象直接修改资产。

`ITaskRepository` / `TaskAccountTransaction` 隔离存储。SQLite 中任务文档、领奖回执、筹码账本及道具更新在同一事务中提交。牌桌结算与获胜 outbox 同事务；任务消费在独立事务中记录 inbox 与进度。异常取消不会删除已记入的事实或任务贡献。

## 生命周期与界面

- UTC+8 业务日。固定任务每天为所有已有账户刷新；注册/登录补齐当日初始化。随机任务首次登录补空位，持续在线跨日也执行一次；面板打开不生成任务。
- 固定不占随机名额。随机任务只有全部轮次达标或到期才释放名额；同一天释放不立刻补发。
- 普通累计任务只有领取当前轮奖励后才进入下一轮，等待领取时忽略新通知。同一手不能在后续轮次再次贡献。
- 到期时看消费时间与当前任务池；未完成的轮次终止，已完成待领奖轮次永久保留。直接领取任务必须在当天领取，随机池中的直接领取任务也限制为 1 业务日。
- 实例持有定义、版本、奖励及道具上限快照。配置修改不会改变旧实例。旧版福利账本可迁移第一轮的已领取状态，避免升级日重复发第一份。
- 面板沿用现有 prefab 卡片皮肤与字体，在运行时构造水平 `ScrollRect` 和动态卡片。每个累计子目标有进度条，另有总完成次数进度条；历史待领取实例与新任务可同时显示。
- 网络查询每页最多 40 张卡片，用文档 revision 防止混合不同版本页面；发生变化会重新读取。每次领奖提交实例 ID 与轮次序号，重试第一轮不能误领下一轮。

本次扩展了 Mirror 消息字段，客户端和服务器需要同步更新。保留旧枚举和调用入口是为了内部兼容及账本迁移，不表示旧版客户端二进制协议仍可兼容。

## 验证记录

Unity 2022.3.52f1c1 脚本编译通过。新增 `ConfiguredTaskTest`、`FormablePatternTest`，并更新多人诊断任务场景以覆盖配置的全部直接领取轮次。

共享工作区最终全量 EditMode：**567/567 通过，0 失败、0 跳过**，Unity Test Runner job `01900dae7436414496ad3645d1dc402c`。包含任务领域、真实牌桌通知、SQLite 回滚/重启、配置校验、奖励、分页与网络序列化，以及既有牌型、牌桌、账户和 UI 回归。测试执行由共享工作区的契约任务协调占用 Unity 后完成。

最终任务卡片清理旧编号后，UI 导航专项再次 **54/54 通过**，job `07eb10fb01a54664b4751724c68a2a71`。

最终 Windows Player 构建成功：`Build/DailyTasks/Poker.exe`，job `build-170bf8a096`，0 error。构建附带完整三表。

Windows 独立进程任务场景：**1 server + 3 client 全通过**，日志 `Logs/DailyTasks/tasks-d062d050`。三端分别到账后余额 3900、5900、3900；覆盖从标题进入、注册/登录、三轮领取、双击只发一次请求、重复轮次拒绝、刷新、重登持久化、返回标题、进入牌桌后拒绝领取。三个账户完成三手牌及旁观/回桌/离房流程；最终资产与账本均为 **13700**，无活动手牌、无账本差异。测试结束已恢复原登录偏好。

UI 实际渲染检查：1920×1080 与 1366×768 均无标题/描述/进度重叠；横向拖拽可从子目标区域传递至任务列表；八个子目标能滚动至最后三项，总完成次数和领取按钮保持可见。截图位于 `Logs/DailyTasks/panel-final-1920.png`、`panel-final-1366.png`。配置编辑器实际打开并完成三表内存往返校验（4 定义、2 固定、2 随机、每日奖励 3 轮）。

Linux Dedicated Server 构建成功：`Build/DailyTasksLinux/Poker.x86_64`，job `build-a23ab6ee0d`，0 error。使用实际 Linux server 编译条件，包含任务配置文件。已确认编辑器恢复为 Windows Player 目标、编译空闲，临时预览对象已清理。

Linux server（WSL Ubuntu）+ 3 个 Windows clients 跨平台任务场景也全部通过，日志 `Logs/DailyTasks/linux-tasks-9142bf9e`。同样完成三轮领取、三手牌和进出桌流程；最终资产与账本均为 **13700**、无活动手牌、无账本差异。四个进程已退出，原登录偏好已恢复。无图形模式有 Unity 的不支持 shader 提示，未出现影响任务/牌局流程的异常。

## 2026-09-08：删除随机任务与单卡片吸附

- 每页只呈现一张完整任务卡片；拖拽松手后吸附到卡片中心，短距离明确滑动也可翻页。支持滚轮、左右按钮和页码；删除后优先保留当前任务的位置，最后一张删除后回到相邻卡片。
- 只有随机任务显示“删除”。二次确认说明奖励自动领取、放弃剩余进度/轮次和下一业务日补空位；取消不发请求。
- 删除时服务端重新检查账户归属、连接、房间和活动手牌。只根据事务内最新状态判断是否可领奖；可领奖时自动领取当前一轮，再删除整个任务实例。多轮直接领取任务也只自动领取一次。
- 过期但保留待领奖励的累计任务也支持上述操作。条件不满足的直接领取任务和未完成任务直接删除，不生成奖励。
- 筹码、道具、领奖回执、删除记录和任务移除共用一次 SQLite 事务。余额/道具上限或存储失败时不删除任务，不发生部分到账。重复删除返回 AlreadyDeleted，领奖回执仍能拦截旧领取请求。
- 删除释放随机池名额，但不更改当天随机刷新标记。当天刷新面板、重登或重启服务器均不会补发；下一业务日按既有首次上线/持续在线跨日规则补空位。之后收到的事实只影响仍持有的任务。
- 删除协议新增 DailyTaskDeleteRequest/Response，快照新增 CanDelete；服务端与客户端须同时更新。持久化只扩展 task_accounts JSON，不修改数据库 schema。

### 本次专项验证

- Unity 2022.3.52f1c1 相关 EditMode **126/126** 通过，job `78683923a58841da853b4cce65c73e35`；覆盖 ConfiguredTaskTest、DailyTaskTest、TaskCardScrollTest、UINavigationTest。
- Windows 构建成功，job `build-90f61cc5d2`，0 error。独立进程 **1 server + 3 clients 全通过**，日志 `Logs/DailyTasks/tasks-d329daba`。三端均完成删除/取消、单卡片宽度、同日不补发、领取、重登、三手牌、旁观/回桌/离房；资产/账本 13700，无活动手牌，无账本差异。登录偏好已恢复。
- 实际面板以预览数据渲染并检查 1920×1080、1366×768；截图 `Logs/DailyTasks/panel-single-card-1920.png`、`panel-single-card-1366.png`。临时对象已清理，Game.unity 未变脏。
- 遵照用户要求，等待另一 Agent 完成名片修改并明确释放 Unity 后，才执行以下全量回归与发布构建。

### 合并工作区后的全量验证与发布

- 最终全量 EditMode **598/598** 通过，0失败/跳过，job `c05312cbabc84ba2ad4e557dadbd967e`。
- 独立进程1 server+3 clients：`tasks-acb811ea`、`contracts-1e6710fb`、`leaderboard-4dccc7d7`、`shop-287b881f`、`items-6bdb154e`五组全通过，均位于 `Logs/DailyTasks/`。任务删除、服役排行榜、恶魔名片、商店/背包及牌局/旁观/离房均完成验证，资产账本一致。
- 修复了历史商店诊断的三页固定断言及测试价格初始化缺失；不改变生产商品价格。
- Linux x86_64 Dedicated Server 构建成功，job `build-3c532db035`，0error；该实际发布产物与三个Windows客户端的任务回归通过，`Logs/DailyTasks/linux-tasks-ef2e4eee`，账本13700、无活动手牌。
- WebGL 构建成功，job `build-9a035f8ed1`，0error。此次没有追加浏览器多人实测；客户端协议和UI联机验证使用Windows客户端。
- 发布包位于 `Build/Release/2026-09-08/`：`Poker-WebGL.zip`、`Poker-Linux-Server.tar.gz`。ZIP CRC、资源版本目录与文件哈希、默认网络配置、Linux可执行权限与产物一致性、无测试数据库均已校验。测试、构建、归档校验与SHA256报告均同目录。
