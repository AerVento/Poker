# 幻夜双影：下个版本商城配置

## 商品

- 商品 ID：`twilight-duo`；统一物品 ID：`namecard-twilight-duo`。
- 名称：幻夜双影。
- 介绍：花海映双影，与你共赴蓝紫幻夜。
- 售价：50,000 账户筹码（用户已确认）；上架；非默认款。
- 商品 SO：`Assets/Resources/SO/Shop/Products/twilight-duo.asset`。
- 已追加至 `Assets/Resources/SO/Shop/NamecardCatalog.asset` 末尾，第 13 款，商城第 4 页；原商品顺序保持。
- 图片：`Assets/Sprites/Namecards/twilight-duo-wide-v1.png`、`twilight-duo-4x3-v1.png`，原样复制自已确认的美术稿；Unity 创建并保留全部 `.meta`。
- Unity 导入：Sprite / Single / Full Rect、Max Size 1024、无 Mip Maps、Clamp、NPOT None。实际纹理宽版 1024×384，紧凑版 1024×768。

## 下个版本发布

已通过现有 `Poker/Shop/Export Server Catalog` 导出内置 `Assets/Resources/SO/Shop/NamecardServerCatalog.json`，共 13 款。本次未操作线上服务端或账户。

发布包含新图片、SO 与目录的客户端，并按既有流程部署配套服务端。已有部署必须在其实际使用的外部 `NamecardServerCatalog.json` 数组末尾追加下面的商品对象（注意前一个对象后的逗号），保留其他商品和线上价格：

```json
{"Id":"twilight-duo","Price":50000,"Listed":true}
```

外部文件默认在服务端旁的 `Configs/`；指定 `-pokerServerConfig` 时，与该网络配置文件同目录。重启服务端读取新目录，检查启动日志商品数和 revision。不要用内置目录整份覆盖已有外部配置。首次部署且无外部文件时，会从新版内置目录生成。

## 实际验证

在 Unity 2022.3.52f1c1、`Assets/Scenes/Game.unity` 中完成：

- 图片导入与 `Validate Namecard Artwork` 全目录检查通过。
- 校验新商品价格、上架/默认状态、13 个目录引用、两种比例图片解析及实际纹理尺寸。
- 执行商城预览并进入第 4 页，检查唯一可见商品为幻夜双影且引用正确；已目检名称、介绍、购买按钮、50,000 售价及姓名/筹码叠加。
- 临时实例化现有 Player4、Player8、Player16 prefab，调用 `NamecardAppearance.Present`，断言分别选择宽版、紧凑版、宽版；临时对象已清理。
- 已退出预览 Play Mode，未保存场景改动。
- Console 未发现新增脚本编译错误；进入本任务前已有本地连接拒绝的 `SocketException`，因此不声称 Console 全无 error。
- 本次仅配置与资源变化，未运行 NUnit、真实账户购买、server + 两个 client 联机流程或发布构建。上述预览不替代上线前的购买、装备、重连和状态组合验收。

![商城第 4 页](twilight-duo-shop-v1.png)
