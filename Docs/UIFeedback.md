# UI 提示与确认

更新：2026-09-05。本轮仅实施用户选择的 1、2、5、6、9、10。

## 已实现

1. 全下确认：点击全下、跟注耗尽筹码、下注到全部可用总额，均先显示具体剩余筹码，确认后才发送原操作。默认焦点为取消。
2. 免费过牌时弃牌确认：当前无需跟注即可过牌时，弃牌前询问；需要付出筹码的普通弃牌不增加确认步骤。
3. 确认框不暂停服务器计时。手牌、行动轮次、下注状态或行动资格变化，以及牌桌关闭时，关闭旧确认；旧回调不得提交到后续轮次。
4. 下一手无法继续时，服务器在 RoomInfoSync / PlayerLeaveRoomResponse 中携带 RoomTransitionReason。人数不足与其他停止原因区分，参战玩家看到返回房间等待的说明，旁观者看到返回房间列表的说明。普通主动离开不显示人数不足。
5. 本次客户端启动首次进入旁观时，说明主动申请下一手、继续旁观、离开及参战名额限制。再次同步、离开再进入不重复。若已经显示筹码不足选择，则优先使用该提示，避免叠加。正式参战、离开房间或断线时清理旁观说明。
6. 房间参数与下注金额修正时，在表单旁解释原因，不弹出遮挡操作的确认框。下注反馈在行动变化时清除，房间参数反馈在有效编辑或重新打开页面时清除。
7. 断线提示的“重新登录”按钮会打开登录页，优先使用当前窗口断开前的会话恢复同一账号，缺少当前会话时才读取已保存登录；没有凭据时留在登录页输入。普通连接失败改为易懂的文字，原始错误只保留在日志中；错误与断线统一由断线流程显示提示。存储故障仍使用优先的停服原因。

MsgBox 继续复用现有预制体，仅新增可选的取消优先焦点。GamePanel 与 RoomPanel 预制体新增输入说明文字引用，没有新增面板或纹理。

离开和人数不足清理旧游戏结果框时改为销毁它，避免调用旧 GameResultMsgBox.Hide 所附带的返回标题行为。该旧类自身的跳转设计仍在单独的审查建议中。

## 协议与验证范围

RoomTransitionReason 使用 Mirror 自动生成的消息序列化；原 RoomInfo JSON 保持不变，Serialization.cs 中原有 RoomInfo/RoomList 扩展无需修改。新旧网络消息布局有变化，客户端和服务器应使用同一版本。

新增 18 项 NUnit 展开用例：全下金额边界、下注与房间输入反馈、返回目的地、旁观说明、取消默认焦点及两类消息经 Mirror 序列化往返。

- Unity 2022.3.52f1c1 EditMode：175/175 通过，作业 `ce77a43f086941ce9b23559e1942c404`。
- 首轮 Windows 严格构建 `build-5ad68c9e24`：0 error，1 个原有未使用事件警告。
- 一服三端 spectators 场景 `Logs/UIFeedback/integration-e8e1f4d1/` 通过。包括三手正常游戏、旁观隐私、主动申请/离开、转旁观及人数不足。三个客户端的 transition.checked 确认目的地提示，intro.checked 验证首次说明；confirmation-lifecycle.checked 验证四条风险操作路径的取消、旧回调与轮次变化关闭。
- 首轮 departures 场景发现同机多个客户端共用 PlayerPrefs，重新登录可能使用其他窗口最后保存的令牌。已将重新登录改为优先使用当前窗口会话，并增加登录身份一致性检查；复跑 `Logs/UIFeedback/integration-eee1e61d/` 一服三端全部通过，relogin-button.checked 和 pending-reconnected 确认正式按钮恢复同一账号、房间和原离开预约。

跳转问题与后续方案见 [UI 跳转审查](D:/GitHubRepos/Poker/Docs/UINavigationReview.md)。

- 最终 Windows 严格构建 `build-b766a454a8`：0 error，1 个原有未使用事件警告。

- 最终会话修复后的全下停服回归 `Logs/UIFeedback/storage-b683183d/` 一服三端通过：正式确认按钮后完成全下，故障发生时三个客户端均显示专用停服原因，服务器退出码 1，恢复后筹码总额 30,000，未重复退款。
- Game.unity 在 1920×1080 下检查了实际预制体的全下确认、人数不足、旁观说明、房间与下注反馈。截图逐张查看，修正范围符号缺字并将下注说明对齐至按钮下方；最终输入截图为 `room-input-feedback-final.png` 与 `bet-feedback-final.png`，位于 `Logs/UIFeedback/`。取消按钮 EventSystem 射线命中正确，默认焦点为取消。视觉预览使用模拟牌桌数据。
- 注入两次连接异常后，界面仅出现一个易懂的连接失败提示，原始诊断文字没有进入 UI；结果见 `Logs/UIFeedback/connection-error-ui.json`。
- 运行时跳转复核见上述审查文档。本轮未执行 Linux / WebGL 构建或公网延迟压力测试。联机测试通过正式 UI 按钮事件驱动，未使用操作系统鼠标自动化。

- 最后两项视觉修正后，UIInteractionTest 再跑 40/40 通过（`dbadc520ae64486d905993bb31e9793a`）；最终 Windows 严格构建 `build-cc4ded4dae` 成功，0 error、1 个原有未使用事件警告。Game.unity 验证无 Missing Script 或断裂 prefab，Console 无 C# 编译错误。

后续导航重构已完成，当前 Push / Pop 行为和新的验证记录见 [UI 面板层级管理](D:/GitHubRepos/Poker/Docs/UINavigationReview.md)。本文件保留提示框实现阶段的历史记录。
