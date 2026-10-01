---
name: poker-cosmetics
description: 在 Poker Unity 项目中新增、替换、改价、上下架名片或牌面，包括美术导入、远程资源（Addressables）数据、Configs/Shop 商品配置、奖励发放和发布检查。用于“加一款名片”“做一套新牌面”“把某款下架/改价”等请求；不用于礼物、普通道具或界面改版。
---

# Poker 名片与牌面

名片和牌面都是纯外观商品：新增款式只需要美术、远程数据和 `Configs/Shop` 中的一条配置，不需要新面板、新按钮、新协议或重新构建页面。先读根目录与相关目录的 `AGENTS.md`，检查工作区已有改动，再按下列资料执行，不要另建一套流程。

- [名片](../../../Docs/Systems/Namecards.md)：配置字段、上下架、显示规则、恶魔名片。
- [名片美术标准](../../../Docs/Standards/NamecardArt.md)：构图、边框与文字安全区。
- [牌面](../../../Docs/Systems/CardFaces.md)：52 张正面加牌背的规格、数据结构、商城与装备。
- [远程资源](../../../Docs/Operations/RemoteArtwork.md)：Addressables 分组、构建菜单、内容版本与部署。
- 放置和命名资源时使用 [poker-assets](../poker-assets/SKILL.md)；改配置与发布内容的步骤见 [poker-dynamic-content](../poker-dynamic-content/SKILL.md)。

## 各类文件的位置

| 内容 | 位置 |
| --- | --- |
| 名片图片（宽版 8:3、紧凑 4:3 各一张） | `Assets/Sprites/Namecards/<商品ID>-wide.png`、`<商品ID>-4x3.png` |
| 牌面图片（52 张正面 + 牌背） | `Assets/Sprites/CardFaces/<主题>/`，如 `spade-a.png`、`club-10.png`、`back.png` |
| 名片远程数据 `NamecardArtworkSO` | `Assets/RemoteContent/namecards/<商品ID转大驼峰>.asset`，如 `RoyalStraightFlush.asset` |
| 牌面远程数据 `PokerCardsSO` | `Assets/RemoteContent/cardfaces/<商品ID转大驼峰>.asset`，如 `JewelCourt.asset` |
| 牌面小预览 | `Assets/Sprites/CardFaces/Previews/<预览名>.png`，打进公共预览包 `previews/cardfaces` |
| 商品配置（名称、介绍、地址、价格、上架） | `Configs/Shop/Namecards.json`、`Configs/Shop/CardFaces.json`，数组顺序即商城顺序 |
| 本地默认外观（默认名片、经典牌面与预览） | `Assets/Resources/SO/ShopLocalArt.asset` |
| 生成原图、提示词、导出脚本 | `ArtSources/`（名片的历次提示词、旧版与图集在 `ArtSources/Namecards/`） |

## 新增一款

1. 确认商品 ID：小写英文、数字、`_`、`-`，永久不变，不能复用已售 ID，不用 `default`。
2. 导入图片并按文档设置 Sprite 导入参数；创建远程数据 SO 并引用图片（名片的 Inspector 显示边框与安全区参考线）。
3. 在配置末尾或合适位置加入一条：名片 `{ "Id", "Name", "Description", "Artwork": "namecards/<ID>", "Price" }`，牌面 `{ "Id", "Name", "Description", "Cards": "cardfaces/<ID>", "Preview": "<ID>", "Price" }`。价格单位是美金。
4. 牌面执行 `Poker/Card Faces/Generate Previews` 生成小预览。
5. 执行 `Poker/Remote Artwork/Prepare Catalogs and Groups` 建立分组并重建预览集，再运行 `Poker/Shop/Validate Namecard Artwork` 或 `Poker/Card Faces/Validate Catalog`，以及 `Poker/Shop/Validate Catalogs`。
6. 打包远程内容并上传，然后重启服务器（顺序见 `RemoteArtwork.md`）。
7. 作为成就奖励时，名片配置写 `"Reward": true`，奖励物品 ID 用 `namecard-<ID>` / `cardface-<ID>`；成就本身仍需代码改动并重新构建。

## 修改已上线商品

- 改价、上下架：只改 `Configs/Shop/*.json` 的对应条目并重启服务器；玩家可能拥有的款式只能设 `"Listed": false`，不能删除条目或改 ID。
- 换图：编辑远程数据，保持 ID 不变，重新打包远程内容即可；已下载的玩家回到大厅时自动更新。
- 改名、改介绍：只改配置并重启服务器。

## 验证

- 在 `Game.unity` 打开商城和仓库，检查名片加载占位、牌面预览与下载遮罩、购买和下载前禁用装备、成功显示、分页边界与装备后的等待房间和牌桌外观。
- 远程资源的真实下载按 `RemoteArtwork.md` 的本地验证步骤进行。
- 如实说明未完成的构建、下载和多人验证。
