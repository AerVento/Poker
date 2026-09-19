# 牌面与礼物配置

## 牌面

`Assets/Resources/SO/Shop/CardFaces/CardFaceCatalog.asset` 的 `View Prefab` 统一引用 `Assets/Prefabs/UI/Panels/GamePanel/CardFaceView.prefab`。各商品的 `Cards` 引用对应 `PokerCardsSO`，并保存默认价格和上架状态。新增牌面只需添加正背面数据和商品，并注册目录。

独立使用时，实例化通用 `CardFaceView`，调用 `SetCards(cards)` 设置牌面，再调用 `Display(poker, showFront)` 设置点数、花色及正背面。更换数据会保留当前显示的牌与正背面状态。牌局和商店预览均使用此组件；牌局切换外观不会重新创建显示对象。

宝石宫廷牌背为 `Assets/Sprites/CardFaces/JewelCourt/Back.png`，已配置到 `JewelCourtCards.asset`。生成原图与完整提示词保存在 `ArtSources/CardFaces/JewelCourt/Back-v1.*`，使用内置 image_gen 生成。源图为 RGB，通用卡牌 prefab 使用项目圆角遮罩裁切外侧像素。

## 礼物

统一目录是 `Assets/Resources/SO/GiftCatalogSO.asset`，列表顺序就是菜单顺序。每项引用物品定义（稳定 ID、名称、图标、介绍）和继承 `GiftEffect` 的播放 prefab；`Duration` 为非缩放秒数，`Play` 接收目标玩家的 RectTransform。播放结束和面板关闭后的销毁由控制器负责。

新增礼物时，将物品定义及效果 prefab 加入此目录，并在 `ItemShopCatalog.asset` 中添加同一个物品定义及默认报价。服务端根据目录注册礼物用途；价格、上架状态、库存扣除和缺货购买仍由服务端决定。外部 `Configs/Shop/Items.json` 可按 ID 覆盖默认报价，完整用法见 [物品商店配置](ItemShopConfiguration.md)。

菜单展示所有有效配置项，左右箭头到边界禁用。选中的礼物仅在本次运行期间保留，关闭、重新打开或重建面板后继续使用；不写入本地存储。有库存时显示数量，缺货时显示服务端价格并支持付费发送，连续发送不同礼物时按各自报价合计预留余额。

目录包含臭鸡蛋和 `placeholder-gift`（占位礼物）。后者价格为 0、无动画或声音，仍经过正常的服务端发送流程，便于手动检查切页与再次打开。测试结束可从礼物目录和物品商店 SO 同时移除对应项；未增加编译条件。

## 本次验证范围

按用户要求仅修改工程，不执行 Play Mode、自动化测试、联机测试或多分辨率交互验收；这些由用户进行。资源通过 Unity Editor API 迁移与保存，检查脚本编译及配置引用。

静态检查确认四套牌面数据和两个礼物效果引用齐全，修改的 prefab 未发现 Missing Script 或断开的序列化引用。未发现 C# 编译错误。Console 保留已有 Mirror 异常日志；切换 prefab stage 时另出现 UnityEditor.UIElements.InspectorElement 内部空引用，调用栈不含项目脚本，未将 Console 标记为无错误。
