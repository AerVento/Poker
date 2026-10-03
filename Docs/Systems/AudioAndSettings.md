# 音频与设置

## 音频资源

| 类别 | 文件 | 说明 |
| --- | --- | --- |
| 音乐 | `Assets/Audio/Music/bgm-lobby.ogg`、`bgm-game.ogg`、`bgm-all-in.ogg` | 大厅/标题/等待房间、普通牌局、本手有人全下；按整数小节剪成无缝循环 |
| 牌桌音效 | `Assets/Audio/SFX/sfx-*.wav` | 轮到你、过牌、下注、跟注、加注、全下、弃牌、胜利、失败 |
| 礼物音效 | `Assets/Audio/SFX/Gifts/gift-*.wav` | 见 [礼物](Gifts.md) |
| 抽奖声音 | `Assets/Audio/SFX/SlotMachine/`、`Assets/Audio/Music/bgm-slot-spin.wav`、`bgm-slot-lounge.wav` | 原创本地合成，资源由老虎机 prefab 引用；见 [筹码抽奖](SlotMachine.md) |

- `Assets/Resources/SO/GameAudioLibrary.asset` 集中引用游戏实际使用的音频，替换音频时改这里。
- 原始素材与生成脚本不在 `Assets`：音乐原曲在 `AudioSources/Incompetech/`（剪辑区间与参数见 `loop_edits.json`，用 `Tools/Audio/build_music_loops.py` 重建）；牌桌音效的采样映射见 `AudioSources/sfx_manifest.json`（`Tools/Audio/prepare_audio_review.py` 生成）。
- 授权：三首音乐为 Kevin MacLeod（incompetech）的 CC BY 4.0 作品，必须保留署名；Kenney 牌桌音效为 CC0；礼物音效为本地合成或用户提供的录音。完整来源与署名在 `Assets/Audio/CREDITS.txt`，设置页的“音频致谢”也会显示。

## 播放规则

- `GameAudioManager` 只在客户端创建，跨界面常驻，只有一个音乐源和一个音效源（音效可叠加）。服务器进程不会创建它。
- 音乐由 `GameAudioManager` 集中调度，支持循环曲目和独占的单次曲目。切换时旧曲 0.2 秒淡出、新曲 0.4 秒淡入，任何时刻最多一首；单次曲目在尾部淡出，完整播放结束后再恢复循环曲目。牌局胜负音效播放时短暂压低音乐。
- 全下音乐由服务器同步的 `IsAllIn` 触发，持续到下一手开始；新一手开局时若盲注已导致全下，直接播放全下音乐。
- “轮到你”提醒由 `PokerAudioState` 根据 `RoomId / HandId / TurnId` 判断；旁观、已弃牌、已全下、等待下一手的玩家没有提醒。
- 本机操作在实际发出请求时立即播放音效（取消确认不播放）；其他玩家的操作按服务器确认的 `PlayerOperation.TurnId` 播放，并与本机回显去重。刚进入或恢复房间时不回放历史操作。耗尽筹码的下注或跟注使用全下音效。
- 结果面板出现时，本地玩家本手收益大于 0（含平分、边池）播放胜利，否则播放失败；旁观者不播放。每手只播放一次。
- 所有过渡使用不受 `timeScale` 影响的时间。
- 进入抽奖页后，`GameAudioManager` 的同一音乐源循环播放专属背景音乐；开始滚动时通过淡出淡入切换至滚动专属音乐，中奖时先独占播放该档位的庆祝音乐，整首结束后恢复抽奖页 BGM；未中奖也先独占播放失败旋律，播完后再恢复页面音乐。离开页面时恢复原场景曲目。页面、滚动、三档庆祝音乐和失败旋律互斥，统一遵循音乐音量。拉杆、逐个停轮和礼炮声音由页面音效源播放，遵循音效音量，并在切页时停止。

### 临时音乐接口

- `SetMusicOverride(owner, clip)`：当前页面请求循环曲目，同时取消该页面之前的单次音乐。
- `PlayMusicOnce(owner, clip, resumeClip)`：淡出当前曲目后播放一次，实际开始播放时记录音频时钟，结束后自动恢复指定循环曲目；调用方不需要自行计时或恢复 BGM。
- `ClearMusicOverride(owner)`：仅释放匹配持有者的请求；页面销毁、新请求与切页都会取消旧的单次播放及恢复计划。
- 单次音乐静音期间仍按音频时钟结束。中途取消静音从当前进度继续，已经结束的庆祝音乐不再补播；静音时不会启动音源。

## 设置面板

- 标题页和大厅打开 `OptionPanel`，牌桌右上角齿轮打开 `InGameOptionPanel`（牌局继续进行，不含登出）。两者都继承 `SettingsPanel<T>`，共享音量与快捷键配置。代码在 `Assets/Scripts/UI/OptionPanel/`。
- 音乐、音效各有 0～100% 滑杆（默认 35% / 70%）和独立的扬声器静音图标按钮。静音时滑杆和百分比归零，取消后恢复原音量；静音期间拖动滑杆会按新音量恢复该通道。音乐为 0 时停止并禁止启动 BGM；音效为 0 时立即停止并拦截牌局与礼物声音，恢复后不补播错过的音效。
- 两个面板复用 `Assets/Prefabs/UI/Panels/OptionPanel/AudioMuteButton.prefab`，图标为 `Assets/Sprites/UITheme/audio-on-button.png` 与 `audio-muted-button.png`。来源与提示词见 [图标资源记录](../../ArtSources/UITheme/audio-buttons.json)。
- 已登录时显示只读的用户名和“登出”按钮；从标题页打开时不显示登出。

## 快捷键

| 操作 | 默认按键 | 可修改 |
| --- | --- | --- |
| 返回 | Esc | 否 |
| 确认 | Enter | 是 |
| 下一个 / 上一个输入框 | Tab / Shift+Tab | 是 |
| 弃牌 | F | 是 |
| 过牌 / 跟注 | C | 是 |
| 聚焦加注金额 | R | 是 |
| 全下 | Shift+A | 是 |

- 定义在 `Assets/Scripts/Game/Input/PokerShortcutBindings.cs`，由 `PanelShortcutHandler` 分发，只有最上层面板响应。
- 输入框编辑、改键过程中以及有其他弹窗时，不触发牌局快捷键。按钮上的小字提示随按键更新。设置中可以恢复默认。

## 本地保存

设置只保存在本机 PlayerPrefs，与账号和服务器无关：

| 键 | 内容 |
| --- | --- |
| `Poker.Audio.MusicVolume`、`Poker.Audio.SoundVolume` | 音量（静音前的值） |
| `Poker.Audio.MusicMuted`、`Poker.Audio.SoundMuted` | 静音状态 |
| `Poker.Shortcuts.Path.v1.*`、`Poker.Shortcuts.Shift.v1.*` | 自定义快捷键 |
| `Account_UserName`、`Account_SessionToken` | 保存的登录信息（`LoginCredentialStore`） |
| `Screen_Width`、`Screen_Height`、`Screen_Fullscreen` | Windows 窗口尺寸 |

音量在拖动后 0.5 秒保存，关闭设置、失焦、暂停和退出时也会保存。
