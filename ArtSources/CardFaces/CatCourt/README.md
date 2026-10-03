# 猫猫宫廷 / Cat Court

本项目的原创 AI 生成牌面套装，使用内置 `image_gen` 生成。设计依据为用户确认的红桃 5、黑桃 J 预览，以及 [牌面](../../../Docs/Systems/CardFaces.md) 的接入规范。

## 美术规则

- 52 张正面：Heart、Diamond、Club、Spade 各 A、2～10、J、Q、K，不含大小王。
- A～10 中央始终只有一个猫头，轮廓分别保留心形、菱形、三叶梅花、黑桃尖顶与底柄；不按点数重复猫头。
- 角标使用正确的点数和普通花色符号；右下角旋转 180°。
- J 持戟、Q 持花、K 持剑。每张人物牌只有一只宫廷猫咪。
- 一张全套共用的墨绿金色双猫牌背。
- 导出为 816×1152、17:24、RGBA PNG；统一 40 px 圆角透明遮罩，无外部留白。
- Unity 使用 Single Sprite、Full Rect、中心 pivot、100 PPU、Clamp、Bilinear、无 mipmap、无损未压缩导入，最大尺寸 2048。

## 资源

- `Assets/Sprites/CardFaces/CatCourt/`：52 张正面及 `back.png`，各有独立 `.meta`。
- `Assets/Resources/SO/CardFaces/CatCourtCards.asset`：52 个花色／点数映射与牌背。
- `Assets/Prefabs/UI/Panels/GamePanel/CatCourtCardFace.prefab`：沿用 CardFaceView，适配父槽位，关闭图像 Raycast Target。
- `Assets/Resources/SO/CardFaces/CatCourt.asset`：商品，稳定 ID `cat-court`，统一奖励物品 ID `cardface-cat-court`。
- `generation.json`：完整提示词、生成记录与源文件位置。
- `Originals/`：原始生成 PNG，导出不依赖 Codex 缓存。
- `contact-sheet.png`：每张牌按 102×144 展示的全套核对图。

## 再次导出及接入

在仓库根目录运行 `./ArtSources/CardFaces/CatCourt/export.ps1`。该脚本只缩放整张生成图并统一透明圆角，不重绘猫咪、点数、花色或边框。`Originals` 是导出时优先读取的母图目录。更换生成图时应同步更新该目录和 generation.json。

然后在匹配版本 Unity 2022.3.52f1c1 中，通过 Unity MCP `execute_code` 执行 `install.cs.txt` 方法体；再执行 `validate.cs.txt` 核对全套映射及正反面显示。两个文件位于 Assets 之外，不参与游戏编译。安装仅写入本套资产并向现有客户端目录追加引用。

运行 `./ArtSources/CardFaces/CatCourt/preview.ps1` 可重新生成全套核对图。

## 上架

物品 ID `cardface-cat-court`，名称与介绍在 `Configs/Items/Items.json`，远程地址在 `Configs/Items/CardFaces.json`，售价 $50,000 在 `Configs/Shop/Shop.json`，详见 [牌面](../../../Docs/Systems/CardFaces.md)。

本次资产制作不向账号发放物品，不修改已装备牌面，不修改线上服务器配置。
