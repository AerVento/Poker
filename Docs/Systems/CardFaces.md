# 牌面

牌面决定玩家自己看到的扑克牌样式（手牌、公共牌、结算展示），只影响本地显示。新增或调整牌面的流程见 [poker-cosmetics](../../.agents/skills/poker-cosmetics/SKILL.md)。

## 款式与获取

有哪些款式以 `Configs/Items/CardFaces.json` 与 `Items.json` 为准，是否出售看 `Configs/Shop/Shop.json`；获取途径可以是新账号初始发放（`Configs/Accounts/StarterGrants.json`）、商城、抽奖或其他奖励配置，文档不逐款列出。

- `cardface-default`（经典）必须存在，使用页面内的本地美术，未装备其他款时显示；其余款式都是远程内容。
- 单套牌面的设计、生成与导出记录放在 `ArtSources/CardFaces/<主题>/`。
- 使用第三方授权素材的款式，授权声明随源文件放在对应目录，发布前确认授权范围。目前简约款基于 Bad Hand Studio 的 [Simplified Playing Cards](https://badhandstudio.itch.io/simplified-playing-cards)，**仅限非商业用途**，原始 SVG 与授权声明在 `ArtSources/CardFaces/Minimal/`。

## 数据结构

| 内容 | 位置 |
| --- | --- |
| 物品 | `Configs/Items/Items.json`：`id`（`cardface-<牌面ID>`）、`name`、`description`、`icon`；牌面不可消耗 |
| 牌面目录 | `Configs/Items/CardFaces.json`：`item-id`、远程地址 `cards`（`cardfaces/<牌面ID>`，经典款写 `""`）、预览名 `preview`；顺序即牌面页顺序 |
| 出售条件 | `Configs/Shop/Shop.json`：`price`、`listed`、`purchasable`；不在表里的牌面只能通过奖励获得 |
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

每套牌面还必须同时制作一张 `Assets/Sprites/Items/CardFaces/cardface-<商品ID>.png`，经典、默认拥有和奖励专属款也适用。物品图标统一为三张扇形扑克牌，前一张显示主题的简化正面，后两张显示牌背；与商城小预览分别交付。具体风格、1024×1024 透明 PNG、导入、生成记录和小尺寸验收要求见 [UI 规范第 8.4 节](../Standards/UIConvention.md)。主题或牌背核心元素变化时同步更新图标。

## 商城与装备

- 商城与仓库的“牌面”页只显示小预览图，浏览与购买不下载整套牌面；预览包下载前预览位置为空。美术更新后执行 `Poker/Card Faces/Generate Previews`，从真实黑桃 A 与牌背重新生成预览，再执行 `Poker/Remote Artwork/Prepare Catalogs and Groups`。
- 未下载的预览上覆盖半透明黑色遮罩和下载图标按钮；点击后替换为进度条，成功后遮罩消失，失败后可再次点击重试。
- 未下载时可以购买，但已拥有款式的装备按钮为灰色“未下载”；下载完成后才允许装备。购买后不会自动装备；装备需要离开房间且没有进行中的手牌。
- 下载状态来自浏览器本地记录（见 [远程资源](../Operations/RemoteArtwork.md)），不等同于驻留内存；关闭界面或释放内存不影响已下载状态。已下载的旧版本仍算已下载，新版本在后台更新。
- 改名只改 `Items.json`，改价、上下架只改 `Shop.json`，然后重启服务器。`listed` 控制未拥有者是否可见，`purchasable` 控制购买；只有两者均为 `true` 时服务端接受购买。已售款式下架时设 `"listed": false`，保留条目与远程数据，拥有者仍可装备。
- 牌面与其他物品一样存在 `account_items`，显示在牌面页而不是道具页；可由服务器发放（见 [货币、商店与物品](Economy.md)）。装备记录保存物品 ID，空值表示经典款。
- 客户端缺少已装备款式的美术时显示经典，不修改服务器上的装备记录。
- 校验：`Poker/Card Faces/Validate Catalog`。代码：购买、装备走统一的物品请求（`NetworkRoom/NetworkRoom.Items.cs`、`Storage/SqliteAccountRepository.Items.cs`）。
