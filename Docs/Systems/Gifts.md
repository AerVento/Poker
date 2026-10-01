# 礼物

礼物是牌局中对其他玩家使用的消耗品。服务器负责校验、扣库存或扣款、广播；每个客户端收到广播后，用礼物包里的 Lua 脚本播放同一段特效。新增礼物的流程见 [poker-gifts](../../.agents/skills/poker-gifts/SKILL.md)。

## 当前礼物

| 礼物 | 物品 ID | 默认价格 | 脚本 | 完整时长 |
| --- | --- | ---: | --- | --- |
| 臭鸡蛋 | `rotten-egg` | $50 | `RottenEgg.lua.txt` | 3.69 秒 |
| 倒香槟 | `champagne` | $100 | `Champagne.lua.txt` | 4.1 秒 |
| 枪击 | `gunfire` | $100 | `Gunfire.lua.txt` | 3.3 秒 |
| 瑞士银行本票 | `swiss-bank-note` | $100 | `SwissBankNote.lua.txt` | 4.9 秒 |

- 礼物列表在 `Configs/Shop/Gifts.json`：数组顺序就是菜单顺序，每项写物品 ID 和特效地址 `gifts/<物品 ID>`。礼物必须是 `Items.json` 中的消耗品（名称、介绍、图标、价格都在那里，见 [货币、商店与物品](Economy.md)）。服务器只按物品 ID 注册礼物用途，不加载特效；修改后重启服务器生效。
- 每个礼物一个远程包，入口是 `Assets/RemoteContent/gifts/<礼物大驼峰>.asset`（`GiftEffectSO`）：引用入口 Lua 模块、用到的共享模块，以及“资源名 → 图片 / 音效 / UI prefab”的映射。图片在 `Assets/Art/Gifts/`，音效在 `Assets/Audio/SFX/Gifts/`；菜单与商城图标在 `Assets/Sprites/Items/Gifts/`，进入公共图标包。生成原图与提示词在 `ArtSources/Gifts/`，音效由 `Tools/Audio/build_gift_sfx.py` 等脚本重建（参数在 `AudioSources/Gifts/`）。
- 只改脚本、美术、音效或礼物配置时，重新打包远程内容、重启服务器即可，不需要发布新页面。“瑞士银行本票”的传统黑桃 A 与红色花纹牌背在 `Assets/Art/Gifts/SwissBankNote/`，只属于该礼物。

## 使用规则（服务器）

- 只能在已开始的牌局中使用。发送者身份来自已认证的连接；目标是稳定的座位号，服务器拒绝不存在、已离开或是自己的座位。
- 服刑年数 ≥ 100（见 [名片](Namecards.md) 中的恶魔名片）时禁止送礼，服务器在同一事务中读取契约库存并拒绝。
- 一次请求最多 32 条命令、200 个礼物。`ConsumeGiftBatch` 在一个 SQLite 事务中处理整批：先用库存，不足的按服务器单价扣美金；按命令顺序执行到付不起为止（最后一条可以只执行一部分）。
- 请求带 `operationId`，重试返回原结果，不会重复扣除或重复广播。
- 首次成功提交后，服务器向房间成员发送 `GiftPlaybackBatchMessage`（发送者座位、目标座位、数量、统一开始时间）。

代码：`Assets/Scripts/Network/Gifts/GiftUse.cs`、`Storage/SqliteAccountRepository.Gifts.cs`、`NetworkRoom/NetworkRoom.Gifts.cs`。

## 客户端交互

- `GamePanel` 上的 `GiftInteractionController` 负责菜单和播放，礼物列表来自服务器下发的商品目录。点击其他玩家的名片打开礼物菜单，点击菜单外关闭。
- 菜单有库存时显示数量；无库存时显示单价，付得起为白色，付不起为红色并禁用。服刑期间图标加灰色遮罩并禁用。
- 礼物商城与收藏不提供下载遮罩或下载按钮。牌局菜单中，未下载礼物的小图标覆盖半透明黑色遮罩和下载标记；标记本身不执行操作，也不穿透触发投掷。
- 点击投掷后先下载礼物包，期间显示简约进度条并继续接受投掷；礼物包下载、加载且 Lua 模块通过校验后，遮罩消失，才向服务器发送请求。0.2 秒内的连续点击及下载等待期间的点击保留数量，连续同目标同礼物合并为命令，仍受每批 32 条／200 个限制。已下载的旧版本礼物直接使用，同时在后台更新（见 [远程资源](../Operations/RemoteArtwork.md)）。
- 下载期间客户端只做本地库存和金额预留。加载失败有限重试后取消该礼物待发送内容并允许重试；断线、离房、失去送礼资格或目标玩家离开／换人时取消未提交请求。关闭礼物菜单不取消已经投掷的内容。最终数量和扣款以服务器结果为准。
- 其他牌局 UI 也可以调用 `GiftInteractionController.EnsureActive()?.QueueGift(itemId, targetSeatIndex)` 共用这套批处理。
- 收到广播后按顺序后台加载对应礼物包，下载期间不显示进度或弹窗，不阻塞牌局。就绪后从头播放，音效也从播放开始计时；不同客户端首次播放可能有延迟差异。
- 加载失败最多尝试三轮，间隔 30 秒。三轮仍失败时跳过当前队列中该礼物的待播内容，继续处理其他礼物。不重复提交送礼请求，也不重复扣款。
- 断线、离开或禁用牌局界面会取消等待并清空队列；下载完成后重新检查房间、双方玩家身份和座位状态。特效实例销毁前持有资源，正在播放的礼物不会因新版本替换而中断。
- 播放不使用对象池：每个礼物创建一个 `LuaGiftEffect`，结束后销毁。同一种特效的播放间隔 = 完整时长 / 10，因此同时最多约 10 个。

## Lua 特效

特效由 xLua 执行、DOTween 驱动。脚本只描述“何时、哪个节点、从什么值到什么值”；逐帧赋值都在 C# 中完成。

| 层 | 代码 |
| --- | --- |
| 播放组件（适配 `GiftEffect`，持有根 Sequence、节点、音源） | `Assets/Scripts/UI/Gifts/LuaGiftEffect.cs` |
| 宿主接口（节点、资源、坐标、Tween、音效） | `GiftScriptHost.cs`、`GiftNode.cs` |
| Lua 环境、沙箱、执行预算与脚本接口外观 | `GiftScriptRuntime.cs` |
| 程序化图形 | `RottenEggAccent`（圆片、液滴、烟雾、环）、`GunfireAccent`（弹孔、枪焰）、`ChampagneGlassGraphic`（杯体与液面） |
| 脚本 | `Assets/Scripts/UI/Gifts/Lua/`，共享模块在 `Shared/`（如 `GiftMotion`） |

### 模块形态

入口模块返回一个表：

| 字段 | 说明 |
| --- | --- |
| `apiVersion` | 脚本接口版本，当前为 1。高于页面支持的版本时该礼物不可用。 |
| `duration` | 完整时长（秒，小于 5），由模块里的节奏参数算出，必须等于时间线终点（误差 0.05 秒内）。 |
| `resources` | 用到的资源名列表；加载时检查都存在。 |
| `build(g)` | 用接口 `g` 搭建节点、设初始状态、插入 Tween 和音效。只在每次播放开始时调用一次。 |

节奏和几何参数写在模块里，不放在 prefab Inspector。模块缓存只共享函数和不可变参数，每次播放的状态都在 `build` 内部。

### 接口 `g`

节点用整数句柄表示，`g.layer` 是特效层（排序在牌局 UI 之上）。坐标都在特效根节点的本地空间，名片位置只在 `build` 时读取一次；名片随后隐藏或离开时 C# 停止播放。

| 函数 | 说明 |
| --- | --- |
| `g.sender(ox, oy)`、`g.target(ox, oy)` | 赠送者／目标名片中心偏移（以名片尺寸为单位）处的坐标 |
| `g.senderSize()`、`g.targetSize()`、`g.bounds()`、`g.screen(u, v)` | 名片尺寸、特效根节点范围、屏幕比例位置 |
| `g.random()` | `[0, 1)` 随机数；正式播放不固定种子，预览固定种子 |
| `g.node`、`g.image`、`g.shape`、`g.instantiate`、`g.find` | 创建空节点、图片、程序化图形（`disc` `drop` `wisp` `ring` `hole` `flash` `glass`），实例化包内 UI prefab，查找子节点 |
| `g.set(node, { ... }, channel)` | 初始状态：`position` `rotation` `scale` `alpha`（可指定通道）、`size` `pivot` `color` `visible` `sprite` `preserveAspect` `fill` `clockOffset` |
| `g.move` `g.rotate` `g.scale` `g.fade` `g.resize` `g.fill` | `(node, 开始时间, 时长, 起点, 终点, 缓动, 通道)`，返回 Tween 句柄 |
| `g.loop(tween, 次数, yoyo)` | 有限循环 |
| `g.show`、`g.hide`、`g.swap` | 某时刻显隐、换图；预览拖动时可正反向重建 |
| `g.sound(时刻, 资源名, 音量)` | 按当前音量与静音设置播放；预览定位时不发声 |
| `g.callback(时刻, 函数)` | 阶段回调，只在正向播放时调用，仅在确需分支时使用 |

- 通道：每个节点有 8 个通道，0 号是绝对基准值，1～7 号对位置、旋转相加，对缩放、透明度相乘。同一属性有多段同时变化时（如本票人物的入场、蓄力、甩出）写入不同通道，不拆嵌套节点。
- 缓动使用 DOTween 名称（如 `Linear`、`InOutSine`、`OutBack`）；原来的 SmoothStep 用 `InOutSine`。所有 Tween 显式给出起点和终点。
- 枪击的弹孔在目标名片内完全随机，各客户端独立随机。

### 安全与预算

- 脚本拿到的只有 Lua 函数和数字句柄，接触不到任何 C# 对象。沙箱不提供 `CS`、`io`、`os`、`debug`、`load`、`pcall`、`coroutine`、`string.rep` 和 `__gc`；`require` 只解析本礼物包内的模块。
- 每次模块加载、`build` 和阶段回调都有指令计数预算（200 万条），死循环会被中断，该特效结束并清理，同一页面中该礼物不再重复报错。每个特效最多 160 个节点、800 个 Tween。
- 报错只结束当前特效，后续队列继续；已获服务器确认的消费不因本地播放失败重发或退款。

`GiftScriptHost` 的公开成员、`GiftScriptRuntime` 的引导代码和脚本接口版本都计入内容格式版本（见 [远程资源](../Operations/RemoteArtwork.md)）。修改它们后需执行 `XLua/Generate Code`（生成代码在 `Assets/XLua/Gen/`，构建前会检查是否过期），并与新页面一起发布。

## 预览

打开 `Assets/Scenes/GiftPreview.unity` 进入 Play 模式，或执行菜单 `Poker/Gifts/Play Gift Preview`。预览不连接服务器、不消耗库存：左侧选择礼物，点击目标名片或“播放”；支持“连送 × 5”、停止、循环、音效开关，拖动进度条可查看静止画面。列表读取 `Configs/Shop/Gifts.json`；预览界面是 `Assets/Prefabs/Gifts/GiftPreview.prefab`。

Editor 预览默认直接读取磁盘上的 `.lua.txt`，保存后重新播放即生效。勾选预览控制器 Inspector 的 `Use Remote Resources` 可验证真实加载路径，需先为 Windows 构建远程内容并运行本地资源服务器。
