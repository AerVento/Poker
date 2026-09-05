# UI 面板层级管理与跳转修复

更新：2026-09-05。本轮已按用户确认的语义实施。Push 不隐藏已有面板；Pop 可以关闭最上层面板，也可以移除指定的中间面板。

## 已修复的问题

| 原问题 | 当前行为 |
| --- | --- |
| 设置页按是否登录猜测返回目标 | 进入设置只 Push，返回只 Pop 自身；从标题进入返回标题，从房间列表进入返回列表。 |
| 断线保留账户资料后，设置返回可能进入失效大厅 | 断线、登出立即清空面板层级并建立标题页；保存的账户资料不再参与设置返回判断。 |
| 旧游戏结果框 Hide 会打开标题，却没有离开服务器房间 | 结果框只关闭自身，BasePanel 同步更新层级；服务器房间状态决定等待、牌桌和大厅之间的切换。 |
| 任意等待房间同步可能打开 RoomPanel | 验证在线认证、房间成员、当前已进入的房间或正在等待的入房请求；普通同步更新已有房间内容，不重复提升它的层级。 |

入房、建房和恢复房间使用同一组客户端请求状态，等待响应期间拒绝重复进入请求。成功开局响应本身没有房间编号，因此不再仅凭这个响应打开牌桌，而由携带匹配房间信息的 GameInfoSync 完成切换。离开成功会移除牌桌及其提示，回到大厅；等待人数不足时，剩余参战者与旁观者仍按原规则分别返回等待房间与大厅。

## UIManager 接口

```csharp
var ui = UIManager.Instance;
ui.PushPanel<OptionPanel>();          // 打开设置；不隐藏原面板
ui.PopPanel();                        // 关闭最上层
ui.PopPanel<OptionPanel>();           // 按类型关闭指定单例面板
ui.PopPanel("RoomRecovery");          // 按 identifier 关闭指定面板
ui.ResetPanels<TitlePanel>();         // 清理失效层级并建立新的根界面
```

- 同一实例不会重复记录；再次 Push 会把它移到所在渲染层的最前面。
- 保留 Bottom / Middle / Top / System 渲染层，System 提示不会被后续打开的普通面板盖住。
- 中间项移除后，其他项保持相对顺序。例如 A、B、C 移除 B 后成为 A、C，C 仍然打开。
- 底层面板保持 active，前景面板独占指针和键盘选择输入，避免点击穿透。关闭前景后恢复底层输入和有效焦点。
- Pop 与 Clear 不执行确认、取消等按钮回调，因而断线清理不会误发操作请求。
- BasePanel 的直接 Show、Hide 和销毁也会通知管理器，避免层级保留已关闭对象。
- 保留 ShowPanel / HidePanel 兼容入口，内部委托给 Push / Pop；项目内调用已迁移到新接口。
- 断线清理复用普通面板实例，不再销毁所有单例并等待下一帧重新打开标题，避免延迟清理覆盖较新的页面。

实现：[UIManager](D:/GitHubRepos/Poker/Assets/Scripts/Framework/UI/UIManager.cs)、[BasePanel](D:/GitHubRepos/Poker/Assets/Scripts/Framework/UI/BasePanel.cs)、[网络界面流程](D:/GitHubRepos/Poker/Assets/Scripts/Network/NetworkRoom.cs)。

## 实际验证

- Unity 2022.3.52f1c1：新增导航用例 18 项，全量 NUnit EditMode **193/193 通过**，作业 `7d7151074c2a4f74ae1e8da8592b2884`。
- 新用例覆盖叠加显示与输入隔离、中间项移除、重复 Push、渲染层、直接 Hide、销毁、空 Pop、两种设置入口、移除来源、旧结算框关闭、重置、断线清理和无效房间消息。
- Game.unity PlayMode 中调用正式按钮事件，复核标题 / 大厅进入设置与返回、中间项移除。记录 `Logs/UINavigation/runtime-navigation.json`。随后用实际指针 Raycast + IPointerClickHandler 验证返回按钮命中与返回、断线后设置返回标题、单例同帧销毁重建后仍有效，记录 `Logs/UINavigation/runtime-navigation-pointer.json`；1920×1080 截图 `Logs/UINavigation/settings-stacked-final.png`。
- 初版 Windows 构建的一服三端 spectators：`Logs/UINavigation/integration-369ffeaf`，三手完成；departures：`Logs/UINavigation/integration-5a12e930`，两手、实际重新登录与恢复房间均通过；两次服务端总筹码均为 30,000 且账本一致。
- UI 实现与按钮检查版本 Windows 构建 `build-c57c13234a` 成功，22.94 秒，0 错误。该次构建器报告原有未使用 OnRoomGameOver 事件警告，以及编辑器后处理代码尚未编译的提示；编译检查、玩家运行和下列联机验证均实际执行。
- 加强按钮检查后，最终版本 spectators：`Logs/UINavigation/integration-86b47506`，三手完成；departures：`Logs/UINavigation/integration-e8c1fbca`，两手与重连完成。测试会检查 Button.IsInteractable（含 CanvasGroup），需要时先通过正式确认按钮关闭结算框，再操作牌桌。
- 第一轮导航测试暴露了 Unity 对象空值判断问题（缺少 CanvasGroup 时不能使用普通 ?? 判断），已修复，之后 193 项全部通过。最初同一工具调用内进行指针命中探针时尚未等到画布完成一帧刷新，且模拟断线未设置曾连接状态；修正探针后实际指针命中和断线返回验证通过，没有把初次探针结果计作通过。

这些验证运行于本机 Windows；未执行 Linux 原生运行、WebGL 浏览器运行或公网弱网压力测试。面板没有引入新的业务功能，网络协议字段保持本轮开始时的版本。

停服场景第一轮 `Logs/UINavigation/storage-11bd7829` 失败：旧断言要求 GamePanel 单例被销毁，与新的普通面板缓存策略冲突。断言已改为要求牌桌不可见且不在层级中，并继续检查断线、已完成手数、标题显示和明确的存储错误提示；不会把缓存实例存在误报成仍停留在牌桌。

最新 Windows 构建 `build-b64182dc52` 成功，27.31 秒，0 错误，仅有原有未使用 OnRoomGameOver 事件警告；最新构建不再报告后处理尚未编译提示。输出为 `Build/UINavigation/Poker.exe`。此构建相对前一版只调整停服测试对缓存面板的断言。

停服回归最终通过：`Logs/UINavigation/storage-dc976f56`。一服三端先完成一手，再在第二手全下结算时注入 SQLite 写入失败；三个客户端均回到标题并显示明确存储原因，牌桌不可见且已移出层级，服务器退出码为 1。重启核对进程通过，总余额恢复为 30,000，无重复退款。
