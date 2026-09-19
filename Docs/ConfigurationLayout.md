# 配置目录

项目运行时配置统一归档在仓库和构建产物根目录的 `Configs/`。代码通过 `Framework.Configs.ConfigManager` 解析默认路径和执行文本读写，不依赖进程工作目录。

```text
Configs/
├─ Network/
│  ├─ client-network.json
│  └─ server-network.json
├─ Shop/
│  ├─ Items.json
│  ├─ Namecards.json
│  └─ CardFaces.json
└─ DailyTasks/
   ├─ TaskDefinitions.json
   ├─ FixedTasks.json
   └─ RandomTasks.json
```

- `Network/` 和 `DailyTasks/` 保存完整配置。默认读写都在 `Configs/` 下；既有显式启动参数仍可指定部署环境中的其他路径。
- `Shop/` 保存可选的运营覆盖。三套完整默认目录来自对应 SO；JSON 只写需要调整的 ID 和字段。文件不存在或为 `[]` 时完全使用 SO，未知 ID 忽略并记录警告，重复 ID、未知字段和无效值会阻止加载。
- Windows/Linux 构建把三类配置复制到产物的 `Configs/`。WebGL 只发布公开的 `Configs/Network/client-network.json`，不会包含服务端或商城运营配置。
- 配置在启动时读取，修改后需要重启对应客户端或服务端。

商城 SO 统一归档在 `Assets/Resources/SO/Shop/`：

```text
Shop/
├─ Items/
│  ├─ ItemShopCatalog.asset
│  └─ Products/
├─ Namecards/
│  ├─ NamecardCatalog.asset
│  └─ Products/
└─ CardFaces/
   ├─ CardFaceCatalog.asset
   ├─ *Cards.asset
   └─ 各牌面商品 SO
```

图片继续按职责放在 `Assets/Sprites/Items/`、`Assets/Sprites/Namecards/` 和 `Assets/Sprites/CardFaces/`，不与 SO 或通用 UI 主题素材混放。
