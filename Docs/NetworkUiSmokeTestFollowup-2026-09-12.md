# 联网 UI 修复与复测 · 2026-09-12

本记录接续 [首轮测试](NetworkUiSmokeTestReport-2026-09-11.md)，按用户确认修复大厅、Prefab 和连接弹窗，并更新仍有意义的 UI 自动化测试。结算继续显示从奖池领取的金额，不改为净收益。

## 已完成修复

- 大厅显示用户名、美金和筹码，兑换及领取奖励后更新；用户名使用省略，金额保留完整显示区域。
- 标题主按钮移至左侧，设置改用统一齿轮图标；设置页清除遮罩的直角底色，滑条使用现有圆角素材；通用弹窗和结算弹窗采用统一金色关闭图标。相应 Editor 迁移工具同步更新。
- 连接弹窗在底层连接建立时关闭，不再等待账户认证成功。Mirror 的 `OnClientConnect` 在认证通过后才触发；原逻辑把连接提示绑到这个回调，导致尚需输入凭据时弹窗挡住登录。Host 还存在连接回调早于 `OnStartClient` 的顺序，新增已连接检查防止随后重新创建弹窗。没有将底层连接当作账户已认证。
- 修复 Editor Host 登出后无法再次登录：保留本地服务端，通过已配置的 localhost transport 重新连接客户端。
- 清理旧账户入口、旧登录字段、旧任务奖励限制和旧文案断言；保留遮挡、导航、服务端结果、隐藏底牌和任务卡滚动等有效检查，新增连接已建立但未认证的弹窗回归。

## 实测结果

- Unity 2022.3.52f1c1：UIInteractionTest、UINavigationTest、TaskCardScrollTest 合计 **95/95 通过，0 失败、0 跳过**。任务 ID：`6510cc0d82f74f0f807c1009431d32f4`（最终布局修改后复跑）。
- 当前源码 Windows Development 构建成功，0 error、5 个已有 warning，输出 `Build/UiFollowup20260912/Poker.exe`。
- Editor `Game.unity`：观察到 `connected=true`、`authenticated=false`、`serverActive=true` 时仅有标题页，没有连接弹窗。错误密码仍可修改并重试；正确登录进入大厅。登出后再次登录成功，服务端保持运行，客户端连接类型从本地 Host 连接变为正常 transport 连接。
- 独立服务端 + 两客户端：登录／注册、兑换各 1000 筹码、建房、入房、准备、开局、加注到 20、跟注、逐街过牌至结算及自动下一手均完成。第一手 A 1020、B 980，奖池领取分别显示 +$40、$0，账目一致。
- 设置覆盖时另一端可继续操作，关闭设置后显示最新状态；上手回看可关闭；离房弹窗的 × 只关闭弹窗，底层控件不会穿透。
- B 预约离开后弃牌，返回大厅；A 断线时只保留标题及恢复弹窗，选择离开房间后标题可以操作。
- B 领取每日奖励后，美金 9000 → 10000，大厅同步更新，筹码保持 979。
- 已查看 1920×1080、1366×768、1600×1000 的关键界面截图；不代表每个页面及所有内容状态均完成三尺寸矩阵。

## 用户指出的布局问题与追加复测

先前功能测试漏掉了 LoginPanel 输入框高度和 GamePanel 操作区比例问题，不能将原先的功能通过当作视觉验收。本轮追加修正：

- LoginPanel 单行输入固定为参考尺寸 64 高，标签宽 160；取消表单行的 flexibleHeight。底板使用原生 VerticalLayoutGroup / ContentSizeFitter 随登录两行、注册三行内容确定高度，保留独立错误提示行，不按屏幕方向重排，也没有新增运行时自定义脚本。
- GamePanel 扩展底板、内收操作区域，底牌从 98×147 调至 124×186。加注输入框恢复正常比例，Slider 手柄区域固定在竖直中心，避免继承滑块高度后被拉长。玩家列表上方留出状态区，预约离开文字不再压到名片上。
- 更新现有 Editor 迁移方法，重复运行时仍保留这些布局参数；Prefab 通过 Unity 的隔离加载／保存修改，避免再弹出已知会阻塞 MCP 的 Prefab Stage 保存窗口。
- 新构建 `Build/UiLayout20260912/Poker.exe` 用于追加双客户端实测。1366×768 和 1600×1000 查看等待／行动状态，实际点击最低加注、加注、滑块中点（金额更新至 513）、跟注和逐街过牌，双方再次收到结算。手牌 `6e8afb5dc5354df8a44c8fb5bc942971`；证据 `C/screen-13.png`、`D/screen-13.png`、`C/state-22.json`、`D/state-23.json`。
- 最终登录底板收紧后另建 `Build/UiLayoutFinal20260912/Poker.exe`，0 error、0 warning。已查看登录／注册切换和空输入错误提示，证据 `E/screen-6.png`、`E/screen-7.png`、`E/state-8.json`。

## 测试证据与边界

本地日志、截图和每次操作 JSON 位于 `Logs/UiFollowup/`。关键证据：`Editor/state-1.json`（已连接未认证）、`Editor2/state-15.json`（Editor 再登录）、`A/screen-28.png`（结算）、`A/screen-31.png`（1366 回看）、`A/screen-34.png`（断线后标题）、`B/screen-43.png`（大厅余额）。

Standalone 使用临时驱动在控件中心执行 EventSystem Raycast 和 pointer 事件，输入通过 TMP 提交；没有绕过按钮直接调用业务方法。不等同于操作系统键鼠或移动真机触控测试。曾在结算弹窗出现的瞬间点击底层离房按钮，被驱动的 raycast 检查阻止；这是测试时序碰撞，未发生游戏点击穿透。B 日志有 SimpleWeb 消息队列未完全处理的 warning。

使用独立测试账户及 `UiFollowup-20260912.db`，未使用正式账户数据库。全清单的长用户名、最大余额、满列表、全部商城／任务／成就、复杂边池、移动真机和真实软键盘不在本轮完整覆盖范围。

## Prefab 自动 Modified 简查

用户反馈确认了 Prefab 保存提示会阻塞 MCP。此前只读调查发现布局组继承 ExecuteAlways，尺寸变化回调也可能触发布局写入；尚未定位到唯一脚本和具体被改字段，因此不能断言某一个脚本就是根因。按要求只调查说明，未另行修改这部分机制。

## 关闭图标来源

使用内置 imagegen 编辑现有 BackButton-v1，保留墨绿底板、金边和透明背景，只将箭头替换为金色 X。正式资源：`Assets/Sprites/UITheme/CloseButton-v1.png`，配套 `.meta` 已由 Unity 导入。

生成提示：Edit target is the provided Poker UI back button. Change ONLY the central gold left arrow to a centered gold X close symbol with two thick rounded diagonal strokes, matching the original beveled metal thickness, champagne gold material and lighting. Preserve the exact rounded square dark emerald satin button, gold border, perspective, and margins. No text. Actual transparent alpha outside the button. This is a matching production Unity UI sprite.

## 收尾清理

已通过 Unity AssetDatabase 删除临时 UiSmokeDriver.cs 及其 .meta，源码仅保留于 Logs/UiFollowup。独立 Poker 测试服务器和 8772 网页服务器已停止，Unity 进程 POKER_UI_SMOKE 已清空，Editor 中本轮测试账户凭据已清除；Standalone PlayerPrefs 已在测试进程退出后恢复。没有删除项目正式回归测试或用户原有场景测试脚本。

清理后脚本编译结束，45 个 UI / Manager Prefab 未发现 Missing Script 或断裂对象引用。测试日志中的 Injected settlement failure / Injected hand start write failure 来自故障注入用例。另有 SWT-WebSocketServer 的 Failed to kick 1 because id not found 日志，尚未单独定位其来源与实际影响。

新增连续超时缩短限时及服务器截止时间同步后的针对性 EditMode 回归为 80/80 通过（任务 b8caf9bc429749f0b22313209d110400）。该功能尚未进行新版双客户端和 WebGL 实测；此前构建不能代表包含该功能的最终版本。
