# 设置与牌桌音效（2026-09-06）

- 返回图标：`Assets/Sprites/UITheme/BackButton-v1.png`；齿轮图标：`Assets/Sprites/UITheme/SettingsButton-v1.png`。使用内置 image_gen 生成，原始 PNG 的透明通道保留，Unity 以最大 256 像素 Sprite 导入。
- 五个原有面板的返回按钮替换为方形箭头；GamePanel 和 RoomListPanel 的设置入口使用齿轮。TitlePanel 保留主菜单中的“设置”文字入口。
- `OptionPanel` 供标题和大厅使用；`InGameOptionPanel` 供 GamePanel 使用。共同继承 `SettingsPanel<T>`，复用显示、音量与本地保存。玩家看到的标题均为“设置”。
- 局内 prefab 不包含登出按钮，基类的登出方法也会检查是否允许登出。2026-09-06 本轮调整后，离开、转旁观、继续旁观／取消预约、下一手参战按钮位于 GamePanel 左上角，由 GamePanel 序列化持有并绑定／解绑。局内设置页不再承载这些按钮，开关设置不会解除牌桌按钮绑定。请求状态继续以服务端为准。
- UIManager 注册两个独立面板。局内设置覆盖牌桌但保持牌桌启用和网络同步；返回恢复输入，断线／离房会清理设置及返回历史。
- `PlayerOperation.TurnId` 由 GameManager 在成功执行操作时赋值，通过已有 ClientGameInfo JSON 投影同步；未修改 Mirror 的二进制消息字段或自定义序列化器。新客户端和服务端应一起发布。
- `PokerOperationAudioState` 按牌局和操作编号消费公开操作；旁观者可听见，初次进入／断线恢复快照不回放历史，同一操作只播一次。本机按钮保持即时音效，服务器回显会去重；服务端自动操作仍会播放。
- 集成验收额外记录每个客户端收到的远端操作与实际音效请求的匹配数（`client-*.audio.json`）。无图形客户端验证的是音效调用，音色仍采用此前已确认的素材。

## 图标生成提示词

### 返回
Use case: stylized-concept. Asset type: production Unity poker game UI button sprite, one square button only. Create a refined square back navigation icon button matching an understated luxury poker game: deep pine green / near black teal satin enamel face (#17312d), slim muted champagne gold bevel border, softly rounded corners (small radius), a single large simple ivory-gold LEFT ARROW glyph centered with a thick clean shaft and two diagonal arrowhead strokes, readily legible at 56 pixels. Straight-on orthographic flat game UI asset, slight material shading only, no perspective, no ornate casino decoration, no text, letters, numbers or watermark. Full button silhouette fills 94% of square canvas, symmetric 3% transparent margin, genuinely transparent alpha background outside button, NO checkerboard drawn. Clean premium tactile satin, restrained, no glow. Deliver just this one square button on transparent background.

### 设置
Use case: stylized-concept. Asset type: production Unity poker game UI button sprite, one square button only. Create a refined square SETTINGS GEAR icon button matching an understated luxury poker game: deep pine green / near black teal satin enamel face (#17312d), slim muted champagne gold bevel border, softly rounded corners (small radius), a single large simple ivory-gold EIGHT TOOTH COGWHEEL silhouette centered with circular hole, symmetric thick clean geometric strokes, readily legible at 56 pixels. Straight-on orthographic flat game UI asset, slight material shading only, no perspective, no ornate casino decoration, no text, letters, numbers or watermark. Full button silhouette fills 94% of square canvas, symmetric 3% transparent margin, genuinely transparent alpha background outside button, NO checkerboard drawn. Clean premium tactile satin, restrained, no glow. Deliver just this one square button on transparent background.

## 此前版本验证记录
- Unity 2022.3.52f1c1，全量 EditMode 304/304 通过，作业 `0f87f11b6ffb4c1283ad30113a0ae858`。
- Game.unity Play Mode 检查局内设置布局；实际 GraphicRaycaster 命中齿轮，触发 pointerClick 后打开 InGameOptionPanel。返回恢复牌桌，局内没有登出节点。
- Windows 一服三端 `spectators-22ad2c7f` 通过三手旁观／参战／离房流程，远端操作与音效请求匹配；`review-bust-0dfb4846` 通过全下、结果保留与断线返回。
- `departures-16bdeb92` 通过预约、取消、恢复、两手牌局和离房。此前脚本只等待 5 秒，小于现有 10 秒结果展示期，误报失败；改为 15 秒后全部原有断言通过，未改业务规则。
- Linux Dedicated Server 在 WSL 启动、WebSocket 101 握手、SQLite 完整性检查及正常退出通过。
- 用户要求停止测试后，中止正在运行的 Linux 一服三端检查，进程退出并恢复登录偏好；未进行浏览器测试。
- 正式发布包位于 `Build/Release/2026-09-06/`，构建结果见该目录的发布记录。


## 发布构建

- 非 Development：WebGL `build-78a8052647` 成功，0 error / 4 warnings；Linux x86_64 Dedicated Server `build-5f7c313222` 成功，0 error / 5 warnings。
- 发布包和校验位于 `Build/Release/2026-09-06/`。用户要求停止测试后仅完成构建与打包。

## Linux 部署命名

- Linux 发行包固定采用旧版的 `PokerServer.x86_64`、`PokerServer_Data/` 和同级 `UnityPlayer.so`，保持现有 systemd 服务的可执行文件命名。
- Unity 构建时优先把输出路径设为 `Linux/PokerServer.x86_64`。打包脚本也会将唯一的 `.x86_64` 文件及其对应 `_Data` 目录映射为上述部署名称，并保留可执行权限，排除运行日志与调试目录。
- 仅重打 Linux 包：`python Tools/Release/package_release.py Build/Release/2026-09-06 --linux-only`；WebGL 压缩包保持不变，更新统一校验清单。
- 2026-09-06 修正仅重打现有 Linux 构建，二进制内容未改动，没有追加测试。原始输出和旧版 `Build/Release/Linux` 保留。

## WebGL 部署缓存

- 线上部署曾出现 `mainThreadEM_ASM` 中读取 `apply` 失败。HTTP 校验确认当前四个核心文件与原始构建一致，但 JS 缓存期为 4 小时，WASM 未由 Cloudflare 缓存；新开的浏览器正常显示标题页。证据支持旧脚本与新 WASM 混用。
- 发布脚本将 ZIP 内的 `Build/` 改为 `Build/<内容哈希>/` 并同步页面引用，所有构建文件字节保持不变。每次内容变化会生成新的版本目录，归档附带 `WEBGL-MANIFEST.json`。
- 仅重打 WebGL：`python Tools/Release/package_release.py Build/Release/2026-09-06 --webgl-only`。先部署资源目录，最后更新入口页面；入口使用 `Cache-Control: no-cache`，版本目录可长期缓存。
- 保留线上客户端网络地址与导航配置。本次没有修改服务器配置、Unity 设置或重新编译游戏，没有追加多人测试。

本轮每手结算、排行榜、金额／按钮及观战修复的实际结果见 [2026-09-06 验证记录](HandSettlementValidation.md)。
