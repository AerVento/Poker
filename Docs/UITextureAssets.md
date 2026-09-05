# UI 生成素材接入记录

2026-09-05，在既有墨绿、香槟金配色上增加背景、绒面和主按钮材质。本次只修改 UI 资源与预制体，没有新增玩法或运行时脚本。

## 素材

使用内置 `image_gen.imagegen`，三次独立生成。工具未暴露具体模型版本选择，完整提示词保存在 [UITextureGeneration.json](UITextureGeneration.json)。原始生成结果原样复制进项目，没有通过脚本重绘或修改位图。

| 资源 | 用途 |
| --- | --- |
| `Assets/Sprites/UITheme/AmbientBackdrop-v2.png` | 标题、登录、设置、大厅、房间、牌局共用背景；细纹理、弧形暗面和边缘金线。 |
| `Assets/Sprites/UITheme/FeltSurface-v2.png` | 标题装饰牌桌与牌局桌面；圆角遮罩内按比例覆盖，避免拉伸绒面纹理。 |
| `Assets/Sprites/UITheme/ButtonSatin-v2.png` | 9 个主按钮共用的中性拉丝材质，由原有 ColorBlock 着色为金色。 |

素材 `.meta` 均由 Unity 生成。背景与绒面导入上限 2048，按钮上限 512；关闭 mipmap、CPU Read/Write，Clamp + Bilinear。当前编辑器实际纹理格式为 RGBA32，三张合计约 12.4 MiB；源文件共约 5.9 MiB。旧版纯色素材保留。

## 预制体接入

通过 Unity Prefab Stage 修改并保存 9 个预制体：TitlePanel、LoginPanel、OptionPanel、RoomListPanel、RoomPanel、GamePanel、MsgBox、RoundResultMsgBox、GameResultMsgBox。

标题绒面透明度为 0.48，牌局为 0.42；牌局背景额外压暗，保证牌面、筹码和操作优先。按钮保留原有圆角形状和业务回调，`ThemeButtonTexture` 作为 `Selectable.targetGraphic` 接收原有悬停、选中、按下及禁用颜色。所有新增装饰图片关闭 Raycast Target，并忽略布局计算。

## 验证

- Unity 2022.3.52f1c1 EditMode：106 / 106 通过，0 失败，任务 `abb109372d0e4d8d8865220fcaec317f`。
- Play Mode 检查按钮射线命中、悬停/按下/禁用颜色，以及禁用按钮不触发点击。
- 在 `Game.unity` 中使用独立测试账户库，以编辑器 server + client 和一个既有 Windows 测试客户端完成注册登录、建房、加入准备、开局和两手牌。编辑器通过 UI 射线实际点击了 4 次操作按钮；客户端确认下注、河牌与局间倒计时。成功报告：`Logs/UITextureRefresh/live-smoke-v2/client-1.pass`。
- 首轮人工操作因停留截图而超时弃牌，测试客户端未观察到河牌，未作为通过结果。第二轮使用连续 UI 点击完成上述验证。
- 调用现有登出路径后，验证连接关闭并回到标题页。
- 查看 1920×1080、1600×1000 的最终牌局截图，检查圆角裁切、牌面和操作区。预览数据仅存在于 Play Mode，未保存到场景。
- 扫描 21 个 UI / Manager 预制体：无 Missing Script 或损坏的序列化引用；`Game.unity` 场景检查无问题。
- 联机测试清理时曾出现 SimpleWeb 的 `Failed to kick 1 because id not found` 日志；未修改第三方传输层。最后一次 Play Mode UI 检查 Console 为 0 error。
- 本次未重新打包 Windows / WebGL / Linux。外部客户端复用已有测试构建，新纹理的显示与点击在当前 Unity 编辑器中验证。

测试后恢复原有登录偏好、Game View 尺寸，停止辅助客户端，退出 Play Mode / Prefab Mode。`Game.unity` 和 `ProjectSettings.asset` 与本轮开始前逐字节一致。

最终截图位于 `Logs/UITextureRefresh/title-final.png`、`game-final.png`、`game-16x10.png`。
