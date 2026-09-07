# 每手结算、排行榜及牌桌调整验证记录

日期：2026-09-06。Unity：2022.3.52f1c1，通过 Unity MCP 操作当前 Poker 项目。商店只是下一阶段计划，未包含在本轮已实现功能中。

## 实现范围

- 新手的盲注、下注、跟注、All-in 仅变动服务端内存；所有参与者（包括输家、弃牌、断线及零净变化者）在手末原子结算。
- schema 4 区分新旧记账模式：旧扣款手牌仅退款一次，新未结算手牌只作废，已提交结算保留。故障继续停服，不增加在线重试队列。
- 大厅排行榜：已结算账户余额前 10、离线账户、同余额稳定排序、固定本人行、手动刷新、身份与请求校验。
- 删除 300 秒业务空闲踢人，保留传输心跳、真实失联检测、思考超时与生命周期保护。
- 离开／观战及取消、下一手参战控件移到 GamePanel 左上角；设置面板解除对这些按钮的所有权。
- 东山再起包含 1,000 边界；GamePanel 及其结果／确认金额统一为小于 1M 显示完整数字，从 1M 起除以 1,000 使用 K。
- 修复观战者结算结果被清空公开底牌的问题；仅显示已公开摊牌，弃牌与未摊牌直接获胜不泄露底牌。

## EditMode

| 验证 | 结果 | Unity MCP 作业 |
| --- | --- | --- |
| 全部自有 Editor 测试，含新增结算／迁移／排行榜／金额用例 | 337/337，通过，0 跳过 | `a630f1efb5fe44b3a1e11f455be96928` |
| 最后牌桌布局及金额字号修正后，UIInteractionTest 和 UINavigationTest | 72/72，通过，0 跳过 | `a9ecca2f7bd84efb8b69bc9b35c73833` |

新增测试实际发现并修复了一处升级风险：当前 SQLite 驱动执行一组 DDL 时，后续语句出错可能未向 `ExecuteNonQuery` 传递异常。迁移现逐条执行，共享同一事务，冲突时回滚字段和 schema 版本。其他故障注入覆盖账本、参与者摘要、完成记录写入失败，确保不发生部分结算。

## 一服三端实际程序验证

全部使用本轮 Windows 开发构建、独立测试数据库、正式网络处理器和 UI 按钮。运行器备份并恢复了测试前的已保存登录偏好；未使用实际玩家数据库。

| 场景与日志目录（项目相对路径） | 实际结果 |
| --- | --- |
| `Logs/HandSettlement/leaderboard-c9a170ab` | 一服三端各完成两手，另外预置 11 个离线账户；前 10、榜外本人名次、已结算余额、刷新、返回及结算后再次查询通过。最终总额／账本 5,530,000，活动手牌 0。观战端公开牌及牌型显示通过。 |
| `Logs/HandSettlement/idle-e4054524` | 三个已认证客户端在标题页保持 310 秒，不发送业务请求，仅维持底层连接；均保持在线，随后排行榜请求及刷新成功。 |
| `Logs/HandSettlement/spectators-0207a6d0` | 三手连续旁观、满桌申请拒绝、离开重入、申请下一手参战、参赛者转旁观、回等待房间与大厅通过。两个不同观战者的公开摊牌 UI 检查通过。最终 30,000，活动手牌 0。 |
| `Logs/HandSettlement/departures-b7b83082` | 预约／取消、预约后断线重连、恢复预约、等待玩家离开、两手结算及离房通过。最终 30,000，活动手牌 0。 |
| `Logs/HandSettlement/tasks-137682be` | 每日任务 UI、领取／重复领取、重登、房间内拒绝领取，加上三手旁观流程通过。最终 7,700，账本一致。精确 1,000 边界及先补给 0→1,000 后领取补助由 EditMode 用例覆盖。 |
| `Logs/HandSettlement/integration-9dc47564`（storage-allin） | 第一手成功结算，第二手全下结算注入数据库写入失败；服务端退出码 1，三个客户端收到原因并回标题页，未广播失败手的成功结果。重启后活动手牌 1→0，余额仍 30,000，账本条数仍 5；再次初始化不退款、不重复入账。 |
| `Logs/HandSettlement/integration-88eaca06`（storage-normal） | 普通结算故障场景同样通过停服通知、事务回滚、重启作废和重复初始化检查；总额 30,000，账本条数不增加。 |

## Unity 场景与视觉

- 在 `Assets/Scenes/Game.unity` Play Mode 检查 1920×1080 和 1366×768。排行榜长名称、最大 uint 余额、十名列表和固定本人行可读。
- 使用实际 EventSystem 射线和滚轮事件滚动排行榜，第 10 名可到达；本人行固定在 ScrollView 下方，1920 分辨率下其上下边界保持 y=144／220。
- 检查牌桌普通参赛、旁观、预约及结果查看状态。视觉检查发现附加控件重叠后，修正左上角分区并为状态文字和玩家列表保留固定空间。玩家金额取消换行，极大数值允许更小自动字号，K 后缀保持同一行。
- 测试离开、参战、继续旁观／取消预约和设置按钮的射线命中；打开／关闭局内设置 3 次，按钮不随设置解绑。正式交互状态另由 72 项 UI 回归覆盖。
- Game 场景与修改／新增的七个相关 prefab 检查：Missing Script 0、Missing Reference 0；结束 Play Mode 后场景不脏。最后 Console error 为 0。
- 截图位于 `Logs/HandSettlement/Visual/`，例如 `leaderboard-1920-1.png`、`leaderboard-scrolled.png`、`leaderboard-1366.png`、`game-1366-final.png`、`game-max-review-1366-final.png` 和 `game-max-review-1920-final.png`。截图使用本地构造的显示数据；真实网络与余额验证见上一节。

## 构建和平台边界

| 目标 | 构建结果 | 作业 |
| --- | --- | --- |
| Windows x64 开发构建 | 成功，0 error，4 warning | `build-c90885cda3` |
| Linux x64 Dedicated Server 开发构建 | 成功，0 error，7 warning | `build-cd2cf9d285` |
| WebGL 开发构建 | 成功，0 error，2 warning | `build-744082bb3e` |

Linux 构建还在 WSL Ubuntu 实际启动，隔离临时数据目录，WebSocket 返回 HTTP 101，SQLite integrity_check 为 ok，进程结束码 0；记录 `Logs/HandSettlement/linux-smoke/result.json`。没有在 Linux 上重复整套三客户端牌局，也未进行 WebGL 浏览器内联机。

上述构建包含本轮最终 C# 代码。之后只调整了 GamePanel／Player prefab 的布局和金额文字换行／最小字号，并在 Editor Play Mode 与 72 项 UI 测试复核；没有为这两处最终布局调整再次打包。本轮交付为工作区项目修改，`Build/HandSettlement` 是本地验证产物，不是发布包。

## 工作区检查

- 已恢复原 WebGL 构建目标与 Full HD Game View，退出 Play Mode，恢复自动资源刷新。
- 修改 C# 保持原 UTF-8／GB18030 编码和 CRLF。新资源具备 `.meta`。Unity 自动带来的既有 TMP 默认序列化字段改写已清除，只保留需要的属性与引用变化；重新导入后确认金额不换行及字号设置有效。
- 本轮代码／prefab／文档的 `git diff --check` 通过。任务开始时已有的 TMP、WebGL 模板、包配置和其他用户文件逐一哈希比较，未改变；没有手工更新 Unity 或依赖。
- 未提交或发布，`Logs`、测试库、构建目录和生成工程文件不作为交付源文件。
