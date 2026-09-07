# 新增、上架与替换名片

名片是静态外观商品。普通新增款式只需导入图片、创建商品 ScriptableObject（SO）并加入目录，不需要增加面板、按钮或修改网络协议。商城每页自动显示 4 款；默认款始终免费拥有。

## 1. 准备图片

为同一款准备两张独立构图的 PNG，拖入 `Assets/Sprites/Namecards/`：宽版 8:3，最大 2048×768；紧凑版 4:3，推荐 1024×768，源图上限 2048×1536，Unity 导入后最大 1024×768。当前生成的紧凑源图为 1448×1086，由 Unity 等比缩小。图片需要完整、不透明且低对比的主题背景；亮色边框、实体装饰与明显光晕限制在四周 64 px 内，文字安全区四周内缩 96 px；宽版按 2048×768、紧凑版按 1024×768 标准画布换算。不要把内部做成纯黑或透明洞，也不要把姓名、金额和状态图标画进图片。详细规范与各款背景方向见 [NamecardArtStandard.md](NamecardArtStandard.md)。

Inspector 设置：Texture Type = Sprite (2D and UI)、Sprite Mode = Single、Mesh Type = Full Rect、Max Size = 1024、关闭 Generate Mip Maps、Wrap Mode = Clamp、Non Power of 2 = None，然后 Apply。透明图打开 Alpha Is Transparency。请保留 Unity 自动生成的 `.meta`，通过 Unity Project 窗口移动资源。

## 2. 创建商品 SO

在 `Assets/Resources/SO/Shop/Products/` 右键，选择 Create → Poker → Shop → Namecard Product。也可以复制现有普通商品，再修改：

| 字段 | 用法 |
| --- | --- |
| 商品 ID | 永久且唯一的小写英文、数字、下划线或短横线，例如 `thunder`。不要使用 `default`，售出后不能修改或复用。 |
| 名片名称 | 商城显示名称，推荐 2～6 个汉字。 |
| 商品介绍 | 一句简短介绍，推荐 12～24 个汉字。 |
| 宽版图片（8:3） | 拖入宽版 Sprite，供宽格子与商城预览使用。 |
| 紧凑图片（4:3） | 拖入同主题 4:3 Sprite，供较高的格子使用；不要另建商品 ID。 |
| 价格（账户筹码） | 使用现有账户余额支付，由服务器最终核定。 |
| 默认款 | 新商品不要勾选。只有 `default.asset` 勾选，价格固定为 0。 |
| 上架 | 新商品勾选；下架时取消，保留 SO 和目录引用。已拥有者仍可装备。 |

## 3. 加入目录与预览

打开 `Assets/Resources/SO/Shop/NamecardCatalog.asset`，将商品拖入商品数组。数组顺序就是商城顺序；保持“经典”在第一个位置。无需手工创建页码，第 13 款会自动出现在第 4 页。

先选择商品 SO，分别检查宽版和 4:3 两个预览，用 Inspector 的黄线检查 64 px 边框上限、青线检查 96 px 文字安全区；执行 Poker → Shop → Validate Namecard Artwork 检查尺寸与导入配置。打开 `Assets/Scenes/Game.unity`，执行 Poker → UI → Preview Namecard Shop，检查图片、名称和介绍。此菜单使用临时示例账户，不会扣除正式账户余额。退出 Play Mode 后示例购买状态消失。

## 4. 导出并发布配置

执行 Poker → Shop → Export Server Catalog。工具会检查重复 ID、默认款、缺失图片等问题，并从 SO 生成 `Assets/Resources/SO/Shop/NamecardServerCatalog.json`。该文件只包含服务器需要的商品 ID、价格和上架状态，没有图片依赖；不要手工改它。

正常 Unity Build 会自动校验并重新导出。修改名称、介绍、价格、图片或上架状态后，重新构建并配套更新客户端和服务器；当前不支持远程热更新图片。服务器启动时固定读取自己的目录，客户端不能指定扣款价格。价格发生变化时，旧购买确认会被拒绝，刷新商城后重新确认。

## 5. 上线后的修改规则

- 改价、改名称、改介绍或换同款图片：编辑原 SO，保持 ID 不变，重新导出和构建。已购库存不会丢失，也不会补扣差价。
- 下架：取消“上架”，保留 SO、图片和目录引用。未购买者不再看到它，拥有者仍可在同一商城页面装备。
- 新系列或不同款式：创建新 SO 和新 ID。不要覆盖另一款已售商品的 ID。
- 默认款：保留唯一 `default`。新老账户均天然拥有；玩家可随时免费换回。
- 单个比例缺图时回退该款另一个比例，仍保持原图比例。未知 ID 或两张图都缺失时显示经典；库存不受影响。

## 6. 功能与验证

商城从房间列表进入，离开房间后才能购买和装备。购买是永久的，每款最多扣费一次；购买完成后按钮变为“装备”，不会自动装备。装备后进入等待房间和牌桌，其他参赛玩家与观战玩家都能看到对应外观。等待页保留 8:3 比例，常规宽度双列、窄宽度单列，人数多时滚动。准备状态、操作高亮、倒计时、弃牌变暗和断线图标继续独立显示。

发布前使用独立测试账户检查：新账户默认拥有经典；余额不足不能购买；购买扣款与库存同时保存；重复点击不会重复扣款；重登仍有库存和装备；换回默认款；两端在等待房间和牌桌看到相同外观；第 4／5 款的分页边界。购买降低排行榜余额，买到 1,000 或以下仍按现有规则判断当天“东山再起”。

数据库升级由服务器自动执行。上线前按既有账户库运维流程备份；意外存储错误沿用停服策略，已提交购买保留，未提交购买整体回滚。

## 7. 按名片 ID 获取图片的代码接口

`NamecardProductSO.Artwork` 保留原来的宽图字段；`Artwork4x3` 是紧凑图。`NamecardCatalogSO.GetProduct(id)` 获取商品，`GetArtwork(id, width / height)` 按目标格子的比例获取更适合的图片，例如：

```csharp
var catalog = Resources.Load<NamecardCatalogSO>("SO/Shop/NamecardCatalog");
Sprite wide = catalog.GetArtwork("galaxy", 8f / 3f);
Sprite compact = catalog.GetArtwork("galaxy", 4f / 3f);
```

现有玩家组件继续调用 `SetNamecard(id)`，无需判断具体主题或人数。`NamecardAppearance` 按自己的美术槽实际尺寸选图，并在布局改变后重新选择；`Image.preserveAspect` 保留比例。其 Content 引用应指向现有 `NamecardContent`，姓名、余额和状态图标会一起放进图片的安全区。外观组件只改变内部安全区，不改变玩家根节点尺寸、列数或字体。为适应安全区，接入时曾将 8 / 16 人档金额自动缩小下限补调为 14 / 12（最终数值以各 prefab 的后续布局调整为准）；默认字号和各档其他排布由牌桌布局任务维护。

当前 Player4 / Player16 使用宽版，Player8 使用 4:3；房间等待页与商城使用宽版。判定依据是实际格子形状，而非固定人数或硬编码名片 ID。以后修改格子尺寸仍可自动选择；未配置 4:3 时不会把宽图拉伸。

本次仅增加资源与客户端选图接口，服务器商品配置、价格、库存与装备协议不需要改动。以后只替换图片可仅更新客户端资源；新增 ID、改价或改上架状态仍需按第 4 节同步发布服务器目录。
