# Poker 音频试听包 v1

整理日期：2026-09-06。包含 9 个短音效和 3 首 BGM 候选，尚未接入游戏播放逻辑。

用浏览器打开 [Listen.html](Listen.html) 可以逐项播放。也可以直接在 Unity 的 `Assets/Audio` 中选中音频并使用 Inspector 的播放按钮。

## 短音效

可以先听 [01–09 连续试听](SFX_Preview_01-09.wav)，顺序如下，每项之间留有 0.85 秒间隔。

| 编号 | 事件 | 文件 | 时长 | 设计方向 |
| --- | --- | --- | --- | --- |
| 01 | 轮到自己 | [SFX_YourTurn_v1.wav](../../Assets/Audio/SFX/SFX_YourTurn_v1.wav) | 0.62 秒 | 柔和双音提示 |
| 02 | 过牌 | [SFX_Check_v1.wav](../../Assets/Audio/SFX/SFX_Check_v1.wav) | 0.35 秒 | 合成的两下轻敲桌面 |
| 03 | 下注 | [SFX_Bet_v1.wav](../../Assets/Audio/SFX/SFX_Bet_v1.wav) | 0.46 秒 | 小叠筹码落桌 |
| 04 | 跟注 | [SFX_Call_v1.wav](../../Assets/Audio/SFX/SFX_Call_v1.wav) | 0.42 秒 | 轻量筹码移动 |
| 05 | 加注 | [SFX_Raise_v1.wav](../../Assets/Audio/SFX/SFX_Raise_v1.wav) | 0.62 秒 | 较密集的筹码叠放 |
| 06 | 全下 | [SFX_AllIn_v1.wav](../../Assets/Audio/SFX/SFX_AllIn_v1.wav) | 0.95 秒 | 筹码推进，搭配低音重音 |
| 07 | 弃牌 | [SFX_Fold_v1.wav](../../Assets/Audio/SFX/SFX_Fold_v1.wav) | 0.55 秒 | 两张牌滑过桌面 |
| 08 | 胜利 | [SFX_Win_v1.wav](../../Assets/Audio/SFX/SFX_Win_v1.wav) | 1.65 秒 | 上行音符与收筹码声 |
| 09 | 失败 | [SFX_Lose_v1.wav](../../Assets/Audio/SFX/SFX_Lose_v1.wav) | 1.05 秒 | 柔和下行音符 |

短音效统一为 44.1 kHz、单声道、16-bit PCM WAV，做过起止淡化和峰值控制。素材中的牌、筹码声来自 Kenney Casino Audio 1.1（CC0）。提示音、敲桌音和低音重音由本地脚本合成，没有调用收费音频服务。

## BGM 候选

| 编号 | 用途 | 文件 | 原曲 / 作者 | 时长 |
| --- | --- | --- | --- | --- |
| 10 | 大厅 | [BGM_Lobby_v1.mp3](../../Assets/Audio/Music/BGM_Lobby_v1.mp3) | Lobby Time / Kevin MacLeod | 3:13 |
| 11 | 牌局 | [BGM_Game_v1.mp3](../../Assets/Audio/Music/BGM_Game_v1.mp3) | Covert Affair / Kevin MacLeod | 3:14 |
| 12 | 全下 | [BGM_AllIn_v1.mp3](../../Assets/Audio/Music/BGM_AllIn_v1.mp3) | Crypto / Kevin MacLeod | 3:24 |

大厅候选偏轻松爵士；牌局候选有慢速贝斯、电钢琴与小号；全下候选偏悬念、打击乐和合成器氛围。这些是根据作者的曲目信息作出的选曲方向，实际是否适合牌桌请以试听为准。

BGM 是官网下载的整曲 MP3，仅做重命名，没有裁剪或重新编码，也尚未制作无缝循环。试听页的循环开关会直接重复整曲，曲尾可能有停顿。先确定风格，再制作循环和游戏内混音。

三首 BGM 使用 CC BY 4.0，可免费使用，但正式发布游戏时需要展示署名。已准备 [完整署名文本](../../Assets/Audio/CREDITS.txt)，接入时可放入游戏的音频致谢页面。短音效的 Kenney 原始素材为 CC0，无强制署名要求。

## 反馈方式

直接按编号或文件名给建议即可，例如：“01 再柔和一点”“06 更有力度”“11 旋律太明显”“12 希望紧张一些”。

## 来源与可复现记录

- [Kenney Casino Audio](https://kenney.nl/assets/casino-audio)：原始 ZIP 和原授权文本位于 `AudioSources/Kenney`。
- [Lobby Time](https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1600054)
- [Covert Affair](https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100795)
- [Crypto](https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1600013)
- [Incompetech 授权说明](https://incompetech.com/music/royalty-free/licenses/)
- [manifest.json](manifest.json)：文件用途、实际时长、SHA-256、音效采样来源和连续试听时间点。
- `AudioSources/Incompetech/sources.json`：官网下载地址、曲目编号和原文件校验值。
- `Tools/Audio/prepare_audio_review.py`：音效制作脚本，需要 Python、numpy 和 FFmpeg；运行方式为 `python Tools/Audio/prepare_audio_review.py --ffmpeg <FFmpeg可执行文件路径>`。

已对文件进行解码、音效峰值和有效音频检查；这些检查不能替代人工试听。当前游戏集成与验证见 [音频系统说明](../Audio.md)。
