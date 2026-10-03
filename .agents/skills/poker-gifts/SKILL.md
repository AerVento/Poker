---
name: poker-gifts
description: 在 Poker Unity 项目中新增或调整牌局礼物，包括物品与礼物配置、Lua 特效脚本、美术与音效、远程礼物包，以及礼物预览与验证。用于“加一个礼物”“调整某个礼物动画/音效/价格”等请求；不用于名片、牌面或其他道具。
---

# Poker 礼物

礼物是可消耗的普通物品，`Gifts.json` 让它可以在牌局中“送出”：服务端负责库存、扣款和广播，客户端用礼物包里的 Lua 脚本播放特效。先读根目录与 `Assets/Scripts/UI/AGENTS.md`、`Assets/Scripts/Network/AGENTS.md`，检查工作区已有改动。

- [礼物](../../../Docs/Systems/Gifts.md)：配置、服务器规则、客户端交互、Lua 脚本接口与预览。
- [货币、商店与物品](../../../Docs/Systems/Economy.md)：`Configs/Items` 物品与功能目录、`Configs/Shop` 商城配置、消耗方式。
- [远程资源](../../../Docs/Operations/RemoteArtwork.md)：打包、内容版本与发布顺序；配置与发布步骤见 [poker-dynamic-content](../poker-dynamic-content/SKILL.md)。
- 涉及界面、按钮或文案时完整阅读 [UI 规范](../../../Docs/Standards/UIConvention.md)；放置资源时使用 [poker-assets](../poker-assets/SKILL.md)。

## 各类文件的位置

| 内容 | 位置 |
| --- | --- |
| 物品定义（ID、名称、介绍、图标名、可消耗） | `Configs/Items/Items.json` |
| 礼物列表（菜单顺序、特效地址） | `Configs/Items/Gifts.json` |
| 价格与上架 | `Configs/Shop/Shop.json` |
| 入口 Lua 模块 | `Assets/Scripts/UI/Gifts/Lua/<礼物大驼峰>.lua.txt` |
| 共享 Lua 模块 | `Assets/Scripts/UI/Gifts/Lua/Shared/` |
| 礼物包入口 `GiftEffectSO` | `Assets/RemoteContent/gifts/<礼物大驼峰>.asset`，地址 `gifts/<物品 ID>` |
| 运行时图片、音效 | `Assets/Art/Gifts/`、`Assets/Audio/SFX/Gifts/` |
| 菜单与商城图标 | `Assets/Sprites/Items/Gifts/<图标名>.png`，打进公共图标包 `icons/items` |
| 生成原图与提示词、音频源参数 | `ArtSources/Gifts/`、`AudioSources/Gifts/`、`Tools/Audio/build_gift_sfx.py` |
| 程序化图形、宿主接口 | `Assets/Scripts/UI/Gifts/`（改动需要发布新页面） |

## 新增或调整一个礼物

1. 确认稳定物品 ID（小写英文、数字、`-`）、名称、介绍、价格（美金）和总时长（小于 5 秒）。
2. 在 `Items.json` 加入物品（`"consumable": true`、`"max-quantity": 4294967295`，`icon` 为图标文件名），在 `Gifts.json` 按菜单顺序加入 `{ "item-id", "effect": "gifts/<ID>" }`，在 `Shop.json` 加入 `{ "item-id", "price", "listed": true, "purchasable": true }`（缺货时按此价自动补买）。字段规则见 `Docs/Standards/Configuration.md`。
3. 编写 Lua 入口模块：返回 `{ apiVersion, duration, resources, build }`。节奏参数写在模块里，`duration` 由参数算出且等于时间线终点；`resources` 列出用到的资源名；`build(g)` 只通过 `g` 搭节点、写初始状态和插入 Tween（接口见 [礼物](../../../Docs/Systems/Gifts.md)）。所有 Tween 显式给出起点和终点；同一属性多段同时变化时用不同通道。可 `require` 共享模块。
4. 在 `GiftEffectSO` 上引用入口模块、用到的共享模块，并为每个资源名填写图片、音效或 UI prefab。
5. 执行 `Poker/Remote Artwork/Prepare Catalogs and Groups`：注册礼物包、重建图标集并运行校验（资源名存在且唯一、模块可加载、礼物包之间不共享美术）。
6. 用 `Poker/Gifts/Play Gift Preview` 检查。Editor 预览直接读取磁盘上的 `.lua.txt`，保存后重新播放即生效。
7. 只改 Lua、美术或配置时，重新打包远程内容并重启服务器即可；改了 `GiftScriptHost`、程序化图形或序列化字段时，执行 `XLua/Generate Code`，并与新页面一起发布（构建会提示内容格式变化）。

不要为某个礼物单独写一次性的配置脚本；如确实临时需要，用完删除。

## 验证

- 动画效果交给用户在预览中观看确认，说明要看的时间点、位置、音效、拖动与连送；自己完成编译、校验和 EditMode 测试。
- 在 `Game.unity` 检查礼物菜单的库存、缺货付费发送与价格显示。
- 多人牌局中的赠送、扣款与广播需要一个服务端和两个客户端验证；未做时明确说明。
- 涉及加载时同时验证首次下载后播放、缓存复用、连送保留次数、失败有限重试、断线/离房取消和目标身份变化。礼物商城／收藏不提供下载按钮。
