# 牌面

牌面决定玩家自己看到的扑克牌样式（手牌、公共牌、结算展示），只影响本地显示。新增或调整牌面的流程见 [poker-cosmetics](../../.agents/skills/poker-cosmetics/SKILL.md)。

## 当前款式

| 名称 | ID | 获取 | 美术 |
| --- | --- | --- | --- |
| 经典 | `default` | 默认拥有，初始装备 | 本地 `Sprites/CardFaces/Classic/card-list-2d.png` 图集 |
| 简约 | `minimal` | 默认拥有 | 远程；来源见下方说明 |
| 猫猫宫廷 | `cat-court` | 商城，默认 $50,000 | 远程 |
| 宝石宫廷 | `jewel-court` | 商城，默认 $50,000 | 远程；设计与导出流程见 `ArtSources/CardFaces/JewelCourt/` |

经典和简约由服务器默认提供所有权，不需要发放；对它们改价或下架会被配置校验拒绝。

简约款基于 Bad Hand Studio 的 [Simplified Playing Cards](https://badhandstudio.itch.io/simplified-playing-cards)，**按非商业用途授权使用**。原始 SVG、授权声明和导出脚本在 `ArtSources/CardFaces/Minimal/`。

## 数据结构

| 内容 | 位置 |
| --- | --- |
| 商品配置 | `Configs/Shop/CardFaces.json`：`Id`、`Name`、`Description`、远程地址 `Cards`（`cardfaces/<ID>`，经典款不填）、预览名 `Preview`、`Price`、`Listed`；顺序即商城顺序，经典款 `default` 在第一位 |
| 本地默认外观 `ShopLocalArtSO` | `Assets/Resources/SO/ShopLocalArt.asset`：经典牌组、经典预览和统一的显示 prefab |
| 牌面数据 `PokerCardsSO`（52 张正面 + 牌背） | 经典：`Assets/Sprites/CardFaces/Classic/ClassicCards.asset`；其他：`Assets/RemoteContent/cardfaces/<ID转大驼峰>.asset` |
| 牌面图片 | `Assets/Sprites/CardFaces/<主题>/`，如 `spade-a.png`、`club-10.png`、`back.png` |
| 预览图 | `Assets/Sprites/CardFaces/Previews/<预览名>.png`，384×256，包含黑桃 A 和牌背；经典预览留在本地，其余打进公共远程包 `previews/cardfaces` |
| 显示组件 `CardFaceView` | 脚本 `Assets/Scripts/UI/Shared/CardFaceView.cs`，prefab `Assets/Prefabs/UI/Shared/CardFaceView.prefab` |
| 当前装备 `CardFaceAppearance` | `Assets/Scripts/UI/Shared/CardFaceAppearance.cs` |

- 所有牌面共用同一个 `CardFaceView.prefab`，新增主题不需要新 prefab。组件提供 `SetCards(PokerCardsSO)` 与 `Display(Poker, bool showFront)`；装饰图关闭 Raycast Target，不含规则或网络组件。
- `PokerElement` 在远程牌面下载完成前使用经典牌面。

## 美术要求

52 张正面 + 1 张牌背，不含大小王；统一竖版 17:24，显示基准 204×288，原图建议至少 408×576。同一套的尺寸、透明边距和圆角位置一致，缩小到 102×144 时点数与花色仍清晰。导入为 Sprite（Single、Full Rect），带透明通道，不使用有损压缩；远程牌面的导入尺寸与压缩由 `Poker/Remote Artwork/Optimize Texture Imports` 统一设置。

## 商城与装备

- 商城与仓库的“牌面”页只显示小预览图，浏览与购买不下载整套牌面；预览包下载前预览位置为空。美术更新后执行 `Poker/Card Faces/Generate Previews`，从真实黑桃 A 与牌背重新生成预览，再执行 `Poker/Remote Artwork/Prepare Catalogs and Groups`。
- 未下载的预览上覆盖半透明黑色遮罩和下载图标按钮；点击后替换为进度条，成功后遮罩消失，失败后可再次点击重试。
- 未下载时可以购买，但已拥有款式的装备按钮为灰色“未下载”；下载完成后才允许装备。购买后不会自动装备；装备需要离开房间且没有进行中的手牌。
- 下载状态来自浏览器本地记录（见 [远程资源](../Operations/RemoteArtwork.md)），不等同于驻留内存；关闭界面或释放内存不影响已下载状态。已下载的旧版本仍算已下载，新版本在后台更新。
- 改价、上下架、改名只改 `Configs/Shop/CardFaces.json` 并重启服务器。已售款式下架时设 `Listed: false`，保留条目与远程数据。经典与简约两款必须免费上架。
- 所有权存在 `account_card_faces`，不进入普通仓库；统一物品 ID 为 `cardface-<ID>`，可由服务器发放（见 [货币、商店与物品](Economy.md)）。
- 客户端缺少已装备款式的美术时显示经典，不修改服务器上的装备记录。
- 校验：`Poker/Card Faces/Validate Catalog`。代码：`Assets/Scripts/Network/Shop/CardFaceShop.cs`、`Storage/SqliteAccountRepository.CardFaces.cs`、`NetworkRoom/NetworkRoom.CardFaces.cs`。
