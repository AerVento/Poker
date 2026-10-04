# 礼物

礼物是可消耗的普通物品，`Gifts.json` 让它可以在牌局中“送出”给其他玩家。服务器负责校验、扣库存或扣款、广播；每个客户端收到广播后，用礼物包里的 Lua 脚本播放同一段特效。新增礼物的流程见 [poker-gifts](../../.agents/skills/poker-gifts/SKILL.md)。

## 礼物清单与配置

有哪些礼物、名称与介绍、价格和菜单顺序都以配置为准，文档不逐一列出：`Configs/Items/Gifts.json` 决定哪些物品是礼物及菜单顺序，`Configs/Items/Items.json` 提供名称、介绍与图标，`Configs/Shop/Shop.json` 决定是否出售及价格，获取途径另见 [筹码抽奖](SlotMachine.md#奖池配置)、成就与每日任务配置。各礼物的时长由其 Lua 模块计算（见“模块形态”），单款礼物的动画与美术设计记录放在 `ArtSources/Gifts/<礼物>/README.md`。

- 礼物列表在 `Configs/Items/Gifts.json`：数组顺序就是菜单顺序，每项写物品 ID 和特效地址 `gifts/<物品 ID>`。礼物必须是 `Configs/Items/Items.json` 中的可消耗物品，见 [货币、商店与物品](Economy.md)。服务器只按物品 ID 绑定“送出”方式，不加载特效；修改后重启服务器生效。
- 不在 `Shop.json` 中可购买的礼物（如抽奖限定礼物）没有购买入口：只能送出已有库存，库存不足时不会自动补买，没有库存时不出现在礼物菜单中。
- 礼物在仓库里照常显示，标注“牌局中送出”，不能在仓库直接使用。
- 每个礼物一个远程包，入口是 `Assets/RemoteContent/gifts/<礼物大驼峰>.asset`（`GiftEffectSO`）：引用入口 Lua 模块、所依赖的 Lua 包（`_dependencies`，用到共享模块时为共享 Lua 包 `lua/shared`），以及“资源名 → 图片 / 音效 / UI prefab”的映射。共享模块在独立的共享 Lua 包里，礼物包不含也不引用它们（见 [Lua 规范](../Standards/LuaConvention.md)）。图片在 `Assets/Art/Gifts/<礼物>/`，只属于该礼物；音效在 `Assets/Audio/SFX/Gifts/`；菜单与商城图标在 `Assets/Sprites/Items/Gifts/`，进入公共图标包。生成原图、提示词与音效生成脚本放在 `ArtSources/Gifts/`、`AudioSources/Gifts/`。
- 只改脚本、美术、音效或礼物配置时，重新打包远程内容、重启服务器即可，不需要发布新页面。新增宿主能力（新的程序化图形或弹型、关节网格、屏幕特效、染色等接口）属于页面代码，需要随新页面发布，单独更新远程礼物包不足以让旧页面支持。

## 使用规则（服务器）

- 只能在已开始的牌局中使用。发送者身份来自已认证的连接；目标是稳定的座位号，服务器拒绝不存在、已离开或是自己的座位。
- 送礼是统一消耗请求 `ItemConsumeRequest` 的“送出”方式（`ItemConsumeMethod.Give`），每条命令带物品 ID、数量和目标座位。
- 服刑中（刑期达到 `Contracts.json` 最低服刑名片门槛，见 [名片](Namecards.md) 中的恶魔名片）禁止送礼，服务器在同一事务中读取契约库存并拒绝。
- 一次请求最多 32 条命令、200 个礼物。`ConsumeItems` 在一个 SQLite 事务中处理整批：先用库存，不足的按商城单价扣美金（礼物须在 `Shop.json` 中可购买）；按命令顺序执行到付不起为止（最后一条可以只执行一部分）。
- 请求带 `operationId`，重试返回原结果，不会重复扣除或重复广播。
- 首次成功提交后，服务器向房间成员发送 `GiftPlaybackBatchMessage`（发送者座位、目标座位、数量、统一开始时间）。

代码：`Storage/SqliteAccountRepository.Items.cs`（`ConsumeItems`）、`NetworkRoom/NetworkRoom.Items.cs`（消耗请求）、`NetworkRoom/NetworkRoom.Gifts.cs`（座位校验与播放广播）。

## 客户端交互

- `GamePanel` 上的 `GiftInteractionController` 负责菜单和播放，礼物列表来自服务器下发的物品目录，库存与可购买状态来自统一的库存快照。点击其他玩家的名片打开礼物菜单，点击菜单外关闭。
- 菜单按 `Gifts.json` 顺序只列出两类礼物：玩家有库存的，或商城当前出售的（`Shop.json` 中 `listed` 与 `purchasable` 都为 `true`）；其余不显示。付不起不会隐藏商城礼物。库存变化后列表随库存快照刷新，选中的礼物仍在列表中时保持选中。规则在 `GiftInteractionController.VisibleGifts`。
- 有库存时显示数量；无库存时显示单价，付得起为白色，付不起为红色并禁用。服刑期间图标加灰色遮罩并禁用。
- 礼物商城与收藏不提供下载遮罩或下载按钮。牌局菜单中，未下载礼物的小图标覆盖半透明黑色遮罩和下载标记；标记本身不执行操作，也不穿透触发投掷。
- 点击投掷后先下载礼物包及其依赖的共享 Lua 包（只下载缺少或不兼容的部分，见 [远程资源](../Operations/RemoteArtwork.md#包依赖与版本)），期间显示简约进度条并继续接受投掷；礼物包下载、加载且 Lua 模块通过校验后，遮罩消失，才向服务器发送请求。0.2 秒内的连续点击及下载等待期间的点击保留数量，连续同目标同礼物合并为命令，仍受每批 32 条／200 个限制。已下载的旧版本礼物直接使用，同时在后台更新（见 [远程资源](../Operations/RemoteArtwork.md)）。
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
| 程序化图形 | `RottenEggAccent`（圆片、液滴、烟雾、环）、`GunfireAccent`（弹孔、枪焰）、`ChampagneGlassGraphic`（杯体与液面）、`SpellBulletGraphic`（蝴蝶、飞刀、星弹、光束、灵珠） |
| 脚本 | `Assets/Scripts/UI/Gifts/Lua/`；共享模块在 `Shared/`（如 `GiftMotion`），打成共享 Lua 包 `lua/shared`，放置与版本规则见 [Lua 规范](../Standards/LuaConvention.md) |

### 模块形态

入口模块返回一个表：

| 字段 | 说明 |
| --- | --- |
| `apiVersion` | 脚本接口版本，当前为 1。高于页面支持的版本时该礼物不可用。 |
| `duration` | 完整时长（秒，大于 0、小于 10；常规礼物约 5 秒内，演出型限定礼物可到 6～9 秒），由模块里的节奏参数算出，必须等于时间线终点（误差 0.05 秒内）。 |
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
| `g.node`、`g.image`、`g.shape`、`g.instantiate`、`g.find` | 创建空节点、图片、程序化图形（`disc` `drop` `wisp` `ring` `hole` `flash` `glass` `butterfly` `knife` `star` `beam` `cannon` `orb`），实例化包内 UI prefab，查找子节点 |
| `g.set(node, { ... }, channel)` | 初始状态：`position` `rotation` `scale` `alpha`（可指定通道）、`size` `pivot` `color` `visible` `sprite` `preserveAspect` `fill` `clockOffset` |
| `g.joint(node, reach, direction)` | 为简单 Image 启用肩部网格变形；`reach` 为过渡长度，`direction` 为手臂延伸的 X 方向（±1）。旋转通道控制手臂，袖根保持贴合；此接口需要对应版本的客户端。 |
| `g.curve(node, at, duration, p0, p1, p2, p3, ease)` | 单个 Tween 的三次贝塞尔轨迹，支持预览回退。 |
| `g.concurrentScale(node, key, at, duration, growth, limit)` | 同一 Canvas 内、相同 key 的有效时间段每多一个播放实例，节点纵向缩放增加 growth，上限 limit；占用缩放通道 7，同一实例的多个节点只计一次，停止后自动移除。 |
| `g.sharedState(key)` | 显式取得跨礼物定义共享的 Lua 状态表；普通 `require` 缓存仍按定义隔离。应在实际播放回调中访问，不在构建或预览时修改。 |
| `g.onStop(fn)` / `g.onAudioStop(fn)` | 在 build 中登记清理回调。结束、停止、禁用、销毁或播放错误会调用一次；onAudioStop 还覆盖预览的音效开关关闭。拖动预览不调用。 |
| `g.musicStart(resource, volume, loop)` | 在实际播放回调中建立独立音乐句柄，遵循音乐音量与静音设置；失败返回 0。 |
| `g.musicAlive(handle)` / `g.musicRetain(handle)` | 查询句柄有效性；将当前播放登记为句柄使用者，成功返回 true。 |
| `g.musicFade(handle, volume, seconds, stop)` | 调整当前播放已持有的句柄音量，可在渐变结束时停止；新的渐变取消旧渐变，保留播放进度。 |
| `g.move` `g.rotate` `g.scale` `g.fade` `g.resize` `g.fill` | `(node, 开始时间, 时长, 起点, 终点, 缓动, 通道)`，返回 Tween 句柄 |
| `g.tint(node, at, duration, fromRGB, toRGB, ease)` | 渐变 RGB，保留节点的独立透明度通道。 |
| `g.screenEffect(parent, material)` | 创建屏幕快照特效，材质来自礼物包；画面捕获包含 uGUI，实例销毁时释放 RenderTexture 与材质。 |
| `g.screenParameter(node, parameter, at, duration, from, to, ease)` | 驱动屏幕特效的 `radius`（径向扩张）或 `strength`（染灰／恢复），范围 0～1，支持预览回退。 |
| `g.loop(tween, 次数, yoyo)` | 有限循环 |
| `g.show`、`g.hide`、`g.swap` | 某时刻显隐、换图；预览拖动时可正反向重建 |
| `g.sound(时刻, 资源名, 音量)` | 按当前音量与静音设置播放；预览定位时不发声 |
| `g.callback(时刻, 函数)` | 阶段回调，只在正向播放时调用，仅在确需分支时使用 |

- 礼物音乐与音效分开控制；预览拖动不触发音乐。音乐句柄由 `GiftMusicPlayback` 持有对应音频包并驱动 AudioSource，Lua 决定播放、共享、循环与停止规则。静音只影响输出，播放进度继续。
- 结束时先调用 Lua 清理，再解除宿主对音频句柄的持有。最后一位使用者退出后，只允许已安排的有限时长停止渐变继续；否则立即清理，避免 Lua 回调报错后留下循环音乐。停止音频并清空 clip 后才释放资源。共享句柄不依赖首个特效的存活。公共 `GameAudioManager` 只提供通用的临时静音接口。
- 东方符卡通过 `Shared/TouhouMusic.lua.txt` 加入 `touhou.music` 状态：同曲增加 Lua 引用计数并续播，异曲跳过且不排队；最后一个引用结束后淡出，淡出期间同曲可取消停止并继续。片段循环、音量和淡出时长由共享 Lua 模块决定。
- 通道：每个节点有 8 个通道，0 号是绝对基准值，1～7 号对位置、旋转相加，对缩放、透明度相乘。同一属性有多段同时变化时（如人物的入场、蓄力、甩出）写入不同通道，不拆嵌套节点。
- 缓动使用 DOTween 名称（如 `Linear`、`InOutSine`、`OutBack`）；原来的 SmoothStep 用 `InOutSine`。所有 Tween 显式给出起点和终点。
- 脚本中的随机量（如落点、弹孔位置）在各客户端独立随机，不同步，不能用来表达需要一致的结果。

### 安全与预算

- 脚本拿到的只有 Lua 函数和数字句柄，接触不到任何 C# 对象。沙箱不提供 `CS`、`io`、`os`、`debug`、`load`、`pcall`、`coroutine`、`string.rep` 和 `__gc`；`require` 只解析本礼物的入口模块和所依赖 Lua 包中的模块。
- 每次模块加载、`build` 和阶段回调都有指令计数预算（200 万条），死循环会被中断，该特效结束并清理，同一页面中该礼物不再重复报错。每个特效最多 160 个节点、800 个 Tween，每类清理回调最多 16 个；客户端最多 64 个共享状态槽、16 个音乐句柄，单次音量渐变最长 10 秒。
- 报错只结束当前特效，后续队列继续；已获服务器确认的消费不因本地播放失败重发或退款。

`GiftScriptHost` 的公开成员、`GiftScriptRuntime` 的引导代码和脚本接口版本都计入内容格式版本（见 [远程资源](../Operations/RemoteArtwork.md)）。修改它们后需执行 `XLua/Generate Code`（生成代码在 `Assets/XLua/Gen/`，构建前会检查是否过期），并与新页面一起发布。

## 预览

打开 `Assets/Scenes/GiftPreview.unity` 进入 Play 模式，或执行菜单 `Poker/Gifts/Play Gift Preview`。预览不连接服务器、不消耗库存：左侧选择礼物，点击目标名片或“播放”；支持“连送 × 5”、停止、循环、音效开关，拖动进度条可查看静止画面。列表读取 `Configs/Items/Gifts.json`；预览界面是 `Assets/Prefabs/Gifts/GiftPreview.prefab`。

Editor 预览默认直接读取磁盘上的 `.lua.txt`（包括所依赖的共享模块），保存后重新播放即生效。勾选预览控制器 Inspector 的 `Use Remote Resources` 可验证真实加载路径，需先为 Windows 构建远程内容并运行本地资源服务器。
