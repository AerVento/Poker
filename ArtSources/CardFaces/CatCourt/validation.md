# 验证记录

日期：2026-09-09。Unity：2022.3.52f1c1。场景：Assets/Scenes/Game.unity。

## 已通过

- 内置 image_gen 生成 52 张正面与 1 张牌背；逐张检查点数、角标花色、中央猫咪数量与人物造型。
- 文件检查：53 张 PNG，全部 816×1152、RGBA，透明通道遮罩完全一致，53 份纹理 .meta 齐全。
- contact-sheet.png 以每张 102×144 显示整套牌面，数字、花色与 J/Q/K 可辨识。
- Unity `Game.Editor.CardFaceEditor.Validate()` 通过，客户端目录中包括 default、minimal、cat-court。
- `validate.cs.txt` 在编辑模式和 Game.unity 运行模式中分别执行通过：52 张正面对应 52 个唯一 Sprite，导入尺寸正确、无损未压缩、无 mipmap、开启透明通道。
- 通过真实 PokerElement 逐张切换 52 次正面及 52 次背面；隐藏状态只显示本套 Back，图像保持比例且不拦截射线。
- Game.unity 检查 Missing Script 数量为 0。本套商品、预制体、图像组件、牌面 SO、52 张正面、牌背的必需引用均有效。
- 导入及编辑模式检查完成时 Console 无 error/warning。
- 运行检查后销毁临时牌槽、恢复原牌面选择并退出 Play 模式；未保存测试场景改动。

## 未完成范围

运行场景时遇到已有连接故障：`[SWT:Exception] SocketException: 由于目标计算机积极拒绝，无法连接。` 因此没有完成连接、进房、开局、结算与断线返回的完整联机流程。该连接异常在任务开始时也存在。

本次未修改牌型规则和协议，未运行牌型 NUnit 套件、客户端构建或 Linux server 构建；以上代码检查通过不代表联机回归通过。

商品配置为 `cat-court / Price: 0 / Listed: false`，其中价格为未定价占位值；没有执行发布、购买、账号发放或线上配置变更。
