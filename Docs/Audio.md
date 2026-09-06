# 游戏音频

更新：2026-09-06。

## 资源与循环

短音效位于 `Assets/Audio/SFX`，保持试听通过的 v1 版本，共 9 个：行动提醒、过牌、下注、跟注、加注、全下、弃牌、胜利、失败。

游戏使用 `Assets/Audio/Music` 中带 `_Loop_v1` 的 Ogg Vorbis 文件：

| 场景 | 原曲 | 取段 | 循环长度 |
| --- | --- | --- | --- |
| 大厅、标题、房间等待 | Lobby Time | 约 0:51–1:51，32 小节 | 60 秒 |
| 普通牌局 | Covert Affair | 约 0:34–1:31，16 小节 | 56.47 秒 |
| 本手有人全下 | Crypto | 约 0:21–1:09，16 小节 | 48 秒 |

循环片段按节拍、持续能量和接缝音色相似程度筛选，保留整数小节，在循环尾部混入起点前的短音频，统一伴奏响度并保留峰值余量。原始 MP3 保存在 `AudioSources/Incompetech`；完整的起止时间、处理参数和校验值见该目录的 `loop_edits.json`。`Tools/Audio/build_music_loops.py` 可以重建循环资源。

`Assets/Resources/SO/GameAudioLibrary.asset` 集中引用 12 个实际使用的音频，便于以后替换。旧的试听页和原曲候选仍保留，但没有被音频库引用。

## 播放行为

`GameAudioManager` 在客户端打开界面时创建，跨界面保存，仅包含一个循环音乐 AudioSource 和一个支持 PlayOneShot 叠加的音效 AudioSource。服务器流程不会通过客户端入口创建音频管理器。

- 音乐切换先在 0.2 秒内淡出并停止旧曲，再用同一个 AudioSource 播放新曲并在 0.4 秒内淡入。因此任何时刻最多播放一首 BGM。
- 胜负音效短暂降低音乐音量，音效结束后恢复。打开设置、重复同步和刷新结果不会重启音乐。
- `PokerAudioState` 根据 `RoomId / HandId / TurnId` 识别本地玩家的新行动机会；旁观、弃牌、全下、等待下一手、离开或断线中的玩家没有行动提醒。
- 本机操作在 `OperationArea` 实际发送请求后立即播放，确认框取消时不播放。其他玩家的操作通过服务端确认的 `PlayerOperation.TurnId` 播放，本机回显和重复快照去重；首次进入／恢复不会回放历史。下注和加注分别选音，下注、跟注耗尽筹码时使用全下音效。
- 全下 BGM 由服务端同步的 `IsAllIn` 触发，本手一旦出现便持续到下一手，包括后续下注街和结果查看期。新手同步重置为普通牌局音乐；如果新手盲注已经造成全下，则直接使用全下音乐。
- 结果弹窗显示时，以本地玩家在服务端 `RoundResult.PlayerResults` 的 `ChipEarned > 0` 判断胜利。分到主池、边池或平分奖池均算胜利；没有本手参战结果的旁观者不播放胜负音。每手结算仅播放一次。
- 返回大厅、房间等待或断线返回标题时恢复大厅音乐，清除本手提醒状态。

所有音乐过渡使用 Update 和不受 timeScale 影响的时间，无额外异步任务或延迟回调遗留。

## 设置与授权

设置页新增音乐音量和音效音量两个 0–100% 滑杆，0% 即静音，默认分别为 35% 和 70%。调整立即影响相应 AudioSource。牌局右上角的齿轮打开专用 `InGameOptionPanel`，当前牌局继续进行；局内不含登出，离房和参战安排也在此面板。标题和大厅打开 `OptionPanel`，两者共享音量及本地保存。

本地 PlayerPrefs 键为 `Poker.Audio.MusicVolume` 和 `Poker.Audio.SoundVolume`。拖动后 0.5 秒保存；关闭设置、应用失焦、暂停和退出时也会保存未落盘的更改。设置与账号、房间和网络同步无关。

三首音乐采用 CC BY 4.0。设置页的“音频致谢”展示作者、曲名、许可和剪辑说明，`Assets/Audio/CREDITS.txt` 保存完整来源和署名。Kenney 原始牌桌音效采用 CC0；本地合成音和具体采样映射见 `AudioSources/sfx_manifest.json`。

## 验证

- Unity 2022.3.52f1c1 编译通过，Console 没有新增脚本错误。
- 针对 `AudioTest`、`UIInteractionTest`、`UINavigationTest` 的 96 项 EditMode 测试全部通过，作业 `49867e4443ad4ba88bcb4f62fef4cb64`。
- 在 `Game.unity` Play 模式确认大厅音乐实际播放、单个音乐源、音效源独立播放、设置滑杆数值即时更新。
- 运行时调整为音乐 24% / 音效 55%，保存并销毁、重建管理器后读回同样的值；随后恢复默认 35% / 70%。
- 运行时确认全下音乐对应循环资源、音乐播放源数量为 1、RoundOver 保持全下音乐、新 HandId 请求恢复普通牌局音乐。

后续的全桌音效、设置改版、多人验收与发布构建见 [SettingsAndAudio.md](SettingsAndAudio.md)。音频自动解码和峰值检查不能代替实际设备试听。
