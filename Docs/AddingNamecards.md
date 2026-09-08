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
| 可购买 | 默认勾选。取消后，上架商品仍显示，按钮灰显“不可购买”，不提供购买或手动装备入口；仍显示配置价格，说明写入商品介绍。 |

## 3. 加入目录与预览

打开 `Assets/Resources/SO/Shop/NamecardCatalog.asset`，将商品拖入商品数组。数组顺序就是商城顺序；保持“经典”在第一个位置。无需手工创建页码，第 13 款会自动出现在第 4 页。

先选择商品 SO，分别检查宽版和 4:3 两个预览，用 Inspector 的黄线检查 64 px 边框上限、青线检查 96 px 文字安全区；执行 Poker → Shop → Validate Namecard Artwork 检查尺寸与导入配置。打开 `Assets/Scenes/Game.unity`，执行 Poker → UI → Preview Namecard Shop，检查图片、名称和介绍。此菜单使用临时示例账户，不会扣除正式账户余额。退出 Play Mode 后示例购买状态消失。

## 4. 导出并发布配置

执行 Poker → Shop → Export Server Catalog。工具会检查重复 ID、默认款、缺失图片等问题，并从 SO 生成 `Assets/Resources/SO/Shop/NamecardServerCatalog.json`。该文件只包含服务器需要的商品 ID、价格和上架状态，没有图片依赖；不要手工改它。

正常 Unity Build 会自动校验并重新导出。这份 Resources JSON 是首次部署的默认目录；服务端实际使用外部 `NamecardServerCatalog.json`，启动时读取一次，后续使用内存快照。

首次启动新版服务端时，如果外部文件不存在，会从内置 JSON 生成。默认路径是服务端可执行文件旁的 `Configs/NamecardServerCatalog.json`；Editor 启动服务端时位于项目的 `Configs/`。如果使用 `-pokerServerConfig /etc/poker/server-network.json`，商城文件也放在 `/etc/poker/NamecardServerCatalog.json`，始终与指定的网络配置文件同目录，不受进程工作目录影响。首次生成时该目录需要可写；文件已存在时只需可读。

部署后调整现有名片的价格或上下架：

1. 编辑外部 `NamecardServerCatalog.json`，找到对应 `Id`，修改 `Price` 或 `Listed`。例如 `{"Id":"galaxy","Price":6000,"Listed":false}` 表示价格 6,000、已下架。完整文件仍然是商品对象数组，保留其他商品。
2. 保存文件并重启服务端。无需重新构建服务端或客户端；当前不支持运行中自动重载。客户端重新连接并打开商城后会接收服务端的新价格与上架状态。
3. 查看启动日志 `Namecard server catalog loaded:`，确认绝对路径、商品数和 revision。服务端最终按这份目录校验并扣款；旧目录版本的购买请求会被拒绝。

所有商品必须明确填写大小写准确的 `Id`、`Price` 和 `Listed`，不能有未知或重复字段。`Price` 必须是 0～4,294,967,295 的整数，`Listed` 必须是 JSON 布尔值。首项必须保留 `{"Id":"default","Price":0,"Listed":true}`，商品 ID 必须有效且唯一。JSON 格式、字段或目录校验失败，以及读取或首次写入失败，都会使启动失败并报告文件路径；不会悄悄回退到内置价格，也不会覆盖错误文件。

重新启动或更新程序不会覆盖已有外部商城文件，发布时也应保留该文件。内置 SO/JSON 更新不会自动合并到外部文件。新增商品需要发布对应的客户端图片与 SO、更新服务端版本，并在已有外部 JSON 中加入新商品；仅更换现有商品的图片、名称或介绍只需更新客户端。当前不支持远程热更新图片。

## 5. 上线后的修改规则

- 改价：修改服务端外部 JSON 的 `Price` 并重启。可同步维护原 SO 的默认价格，供以后的全新部署使用；已有部署以外部 JSON 为准。已购库存不会丢失，也不会补扣差价。
- 改名称、改介绍或换同款图片：编辑原 SO，保持 ID 不变，重新构建客户端。
- 下架：将服务端外部 JSON 中的 `Listed` 改成 `false` 并重启；重新上架改为 `true`。保留商品条目、SO、图片和目录引用，不要通过删除或改 ID 来下架。未购买者不再看到它，拥有者仍可在同一商城页面装备。
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

只替换图片可仅更新客户端资源；新增 ID 按第 4 节发布并补充外部服务器目录，改价或改上架状态直接修改外部 JSON 并重启服务端。库存与装备协议保持不变。

## 8. 通过物品 ID 发放名片

名片在统一物品身份中使用 `namecard-<原ID>`，例如 `namecard-galaxy`。通过 `NamecardItemId.ToItemId` / `ToNamecardId` 及对应的 `Try` 方法双向转换；JSON、SO 和原装备接口继续使用 `galaxy`。服务端可调用 `NetworkRoom.GrantItem(accountId, itemId, operationId)` 发放，已拥有不重复发放，不扣款、不自动装备，普通背包不展示名片。接口约定、回执与版本 9 迁移见 [NamecardItemIdsAndGrants.md](NamecardItemIdsAndGrants.md)。
