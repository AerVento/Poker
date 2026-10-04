# 宝石宫廷牌面设计与接入

## 视觉规则

使用确认的纹饰底板 v2：白色圆角、细纸纹、浅色压纹卷草、香槟金交织边线及植物装饰，比例 17:24。左上同时保留大点数与普通花色；黑桃／梅花近黑，红桃／方片红色。右下不放第二组角标。

A～10 无论点数多少，只在右下显示一件花色珠宝。黑桃为蓝宝石，红桃为红宝石，方片为橙红宝石，梅花为祖母绿；有切面、金属镶座、爪镶与碎钻包边。

J/Q/K 只在右下显示单个人物半身像，采用深色轮廓、简化明暗的卡通画风，穿华丽贵族礼服。以经典牌相应人物的身份、发型、冠帽和持物为参考，属于风格化改编。人物胸前首饰属于插画装饰，左上花色承担识别功能。

## 组合与接入

珠宝、人物、底板由 image_gen 生成，完整原图、提示词和透明元素保存在本目录。点数与左上花色由代码布局，避免模型书写错误。保留共用元素，使用 `export.py` 组合导出 52 张成品，不逐张生成。

正式输出使用同一 v2 底板，单张 816×1152、RGBA，统一由 `compose.py` 与 `export.py` 拼装。此前四张 10 的整图生图替换已撤销；全部牌面重新使用独立素材，禁止单张绕过拼装流程。

统一布局（像素坐标，左上为原点）：

- 点数共用 x=66～354、y=70～310 的 288×240 区域，水平与垂直居中。整组字形等比缩放到约 40,000 平方像素的外接矩形面积（像素取整误差小于 1%）；10 按两个数字的整体计算，不再扩大区域。字形保持原比例，因此宽、高随字符形状变化，不拉伸字体。
- 左上普通花色固定在 x=144～276、y=338～470 内，同花色全部点数的位置、尺寸完全相同。
- 右下所有珠宝与 J/Q/K 人物的可见主体高度统一为 420，顶部 y=648、底部 y=1068、右侧 x=744。保持素材比例，宽度随造型变化，最大区域为 x=314～744。以 alpha > 128 测量主体并裁去外围空白，避免人物底部低透明度残留影响缩放。
- 同花色 A～10 复用同一件珠宝，右下区域逐像素一致；装饰底板与透明圆角全套共用。

正式预览为 `ArtSources/CardFaces/JewelCourt/Previews/Released52-v2.png`，重点对照图为 `Previews/UnifiedLayout.png`。运行同目录 `validate_export.py` 检查全部成品的底板、透明度、点数区域与面积、插画实际高度、花色位置、珠宝一致性、哈希和 GUID。

52 张 PNG 位于 `Assets/Sprites/CardFaces/JewelCourt/`，通过商品的 `Cards Address` 按需加载 `Assets/RemoteContent/cardfaces/JewelCourt.asset`，使用统一 `CardFaceView.prefab`。牌背已替换为专属 `back.png`（白金卷草和多色宝石徽章）；牌面数据结构见 [牌面](../../../Docs/Systems/CardFaces.md)。Sprite 使用 Single、Full Rect、100 PPU、Clamp、Bilinear、无 mipmap；当前导入尺寸与压缩配置见 [远程美术资源](../../../Docs/Operations/RemoteArtwork.md)；所有装饰关闭 Raycast Target。

远程数据为 `Assets/RemoteContent/cardfaces/JewelCourt.asset`，物品 ID `cardface-jewel-court`，名称「宝石宫廷」。物品、牌面目录与售价分别配置在 `Configs/Items/Items.json`、`Configs/Items/CardFaces.json` 和 `Configs/Shop/Shop.json`（售价以该文件为准），见 [牌面](../../../Docs/Systems/CardFaces.md)。没有向现有账户发放或自动装备商品。
