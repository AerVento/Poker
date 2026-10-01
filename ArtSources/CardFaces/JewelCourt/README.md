# 宝石宫廷 / Jewel Court

已将确认的纹饰底板 v2 与共用元素组合为 52 张正面并接入 Unity。白底、左上大点数和普通花色、右下精细珠宝或卡通贵族；右下没有第二组角标。商品 ID 为 `jewel-court`，售价 $50,000，默认上架。牌背复用经典款。

## 资源

- `Sources/`：image_gen 生成的珠宝、人物原始图集及 `Base-v2.png` 纹饰底板；完整提示词见 `generation.json`、`generation-v2.json`。
- `Elements/`：4 个透明珠宝、12 个透明人物、13 个点数字形、4 个普通花色字形及底板。字形分别来自本机 Georgia Bold、Segoe UI Symbol，未分发字体文件。
- `layout.json`：共用布局的导出记录；布局输入位于 `compose.py` 的 `LAYOUT`。
- `Previews/OrnateBase-v2.png`：确认用的四张组合预览。
- `Previews/Released52-v2.png`：正式 52 张牌面的 102×144 核对图。
- `export-validation.json`：正式输出的尺寸、布局边界和每张图片的 SHA256。
- `Assets/Sprites/CardFaces/JewelCourt/`：正式 52 张 PNG，816×1152、RGBA、17:24，圆角外透明。
- `Assets/Resources/SO/CardFaces/JewelCourtCards.asset`：完整花色／点数映射与经典牌背引用。
- `Assets/Prefabs/UI/Panels/GamePanel/JewelCourtCardFace.prefab`：复用 `CardFaceView`，保持比例、关闭装饰 Raycast Target。
- `Assets/Resources/SO/CardFaces/JewelCourt.asset`：商品，已加入 `CardFaceCatalog.asset`。

源素材可继续复用；此次通过代码组合导出 52 张成品，没有逐张请求模型生成。Unity 采用现有单图牌面组件。

## 再次导出和安装

2026-09-30 统一拼装：全部 52 张共用底板、点数区域和普通花色位置；点数整组外接矩形面积约 40,000 平方像素，居中于 288×240 区域。所有珠宝与人物按可见主体统一高度 420，贴齐右侧 x=744、底部 y=1068，并保持宽高比。人物底部低透明度残留不再参与尺寸计算。`EditedTens/` 与 `ten-refinement.json` 仅为已停用的历史生图稿，导出流程不再引用。

本次实际导出 52 张；`validate_export.py` 检查通过：主体实际高度全部 420，点数面积 39,820～40,112 平方像素，同花色 A～10 右下逐像素相同，底板、透明圆角、花色位置一致，52 个 GUID／哈希唯一。Unity 2022.3.52f1c1 中真实 `CardFaceView` 的 52 组正反面切换、图片引用、保持比例和 Raycast 检查通过；`Game.unity` 无 Missing Script，编译已结束，最终 Console error 为 0。查看了三种分辨率截图（`Logs/JewelCourtUnified-1920x1080.png`、`1366x768`、`1440x1080`）。完整连接、入房、开局、断线流程未验证；未重建或发布远程美术包。

依赖 Python 3.10+、Pillow、numpy。在仓库根目录运行：

```powershell
python ArtSources/CardFaces/JewelCourt/export.py
python ArtSources/CardFaces/JewelCourt/validate_export.py
```

目标目录须先通过 Unity Editor 创建。`export.py` 读取现有透明元素与 `Sources/Base-v2.png`，导出正式牌面与核对图。`compose.py` 用于重新分离原始图集和调整字形布局，其默认预览仍使用早期素白底板；重建元素后须再次执行 `export.py` 才会得到正式 v2 输出。Windows 字体路径可通过该脚本的 `--font`、`--suit-font` 参数指定。

在 Unity 2022.3.52f1c1 中重新安装时，先复制 `ClassicCardFace.prefab` 为上述 JewelCourt prefab（已有则直接复用），打开 JewelCourt prefab stage，再通过 Unity MCP `execute_code` 执行 `install.cs.txt` 方法体。脚本设置 Sprite 导入参数、52 个映射、商品和目录引用，并保存关闭 prefab stage。所有导入资源保留配套 `.meta`。

`validate.cs.txt` 检查映射、导入参数和全部正反面；`validate-commerce.cs.txt` 使用隔离数据库检查两款牌面的购买与装备；`validate-shop-preview.cs.txt` 渲染真实商店卡片组件并检查价格和按钮回调。它们位于 Assets 外，不参与游戏编译。设计规则见 [Design.md](Design.md)。

## 上架配置

默认 `Assets/Resources/SO/Shop/CardFaceServerCatalog.json` 与本地外部 `Configs/CardFaceServerCatalog.json` 均已将 `jewel-court`、`cat-court` 设为 `Price: 50000`、`Listed: true`。部署到已有服务器时须同步其外部目录文件并重启服务器；客户端商品 SO 不决定成交价或所有权。
