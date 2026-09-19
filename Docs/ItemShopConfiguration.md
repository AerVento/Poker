# 物品商店配置

统一入口为 `Assets/Resources/SO/Shop/Items/ItemShopCatalog.asset`。在 Inspector 的 Products 中设置物品引用、默认价格（美金）、是否上架和库存上限。名称、介绍、图标及类型仍来自 `Assets/Resources/SO/Shop/Items/Products/` 下对应的 `ItemDefinitionSO`。列表顺序决定默认商品顺序。

`ShopPanel.prefab` 中的 `ItemBrowserView` 只引用这个目录，不再维护 `_definitions` 数组。服务端也从同一 SO 创建默认商品配置，再应用可选的外部覆盖；最终价格与购买资格仍以服务器同步结果为准。新增物品不再修改 UI prefab 或另一份默认 JSON。

选中 SO 后点击“导出默认配置…”，可以从当前 SO 生成一份完整的覆盖模板。服务端固定从 `Configs/Shop/Items.json` 读取可选覆盖；路径由通用 `ConfigManager` 管理，不再跟随网络配置文件的位置。

外部文件按 ID 部分覆盖，例如：

```json
[
  { "Id": "rotten-egg", "Price": 80 },
  { "Id": "placeholder-gift", "Listed": false }
]
```

- 文件不存在时完全使用 SO 默认值，不自动生成覆盖文件，以免后续编辑 SO 的默认值被旧文件遮盖。
- 未列出的物品和未写出的字段沿用 SO 默认值；空数组表示不覆盖任何内容。
- 可覆盖 `Price`、`Listed`、`MaxQuantity`。导出的 `Kind` 用于校验，必须与 SO 的物品类型一致。
- 不能通过 JSON 添加 SO 中不存在的物品；未知 ID 会被忽略并写入警告。重复 ID、未知字段和无效值仍会阻止配置加载。
- 覆盖仅作用于服务端运行时副本，不修改工程 SO。配置变更在重新启动服务端后生效。
- 契约现有规则保持不变：免费、消耗品、无库存上限。其他普通物品可使用目录配置的价格与库存上限；非消耗品的库存上限为 1。新增特殊使用效果仍需注册对应业务逻辑。

`GiftCatalogSO.asset` 负责礼物类型与播放 prefab；`ItemShopCatalog.asset` 负责商品默认报价。增加礼物时在两者中引用同一个物品定义即可。当前目录包含三种契约、臭鸡蛋和占位礼物。

## 名片商品槽位

`NamecardsView/Product1`～`Product4` 是 `ShopItemCard.prefab` 的四个嵌套实例，全部由 `ShopPanel._cards` 引用，用于每页四款名片的显示。编辑器内预置槽位，运行时分页只更新数据，不重复生成。模板和实例均在使用，保留它们。

## 验证范围

按用户要求仅修改工程并检查编译及资源引用；未运行自动化、Play Mode 或联机测试。现有测试及诊断脚本已同步改为使用新的目录结构。
