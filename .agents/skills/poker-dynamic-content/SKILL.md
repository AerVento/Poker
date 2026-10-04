---
name: poker-dynamic-content
description: 在 Poker 项目中不重新构建页面地更新商品与远程内容：编辑 Configs/Shop 的物品、礼物、名片、牌面配置，打包并按顺序发布远程内容，判断何时必须发布新 WebGL 页面，以及排查内容版本、格式版本和页面刷新提示。用于“上架/下架/改价/改名”“加一个物品”“发布新内容”“玩家看不到新内容”等请求；具体美术与脚本制作分别用 poker-cosmetics、poker-gifts。
---

# Poker 动态商品与内容发布

商品列表在 `Configs/Shop/`，美术和礼物脚本是远程内容，两者都能在不重新构建页面的情况下更新。先读根目录 `AGENTS.md`，检查工作区已有改动。

- [货币、商店与物品](../../../Docs/Systems/Economy.md)：物品表、功能目录、商城、初始发放各文件的字段与校验规则。
- [远程资源](../../../Docs/Operations/RemoteArtwork.md)：内容版本、格式版本、缺失／失效／生效、页面更新、构建与上传顺序。
- 新款名片或牌面的美术：[poker-cosmetics](../poker-cosmetics/SKILL.md)；新礼物或改礼物动画：[poker-gifts](../poker-gifts/SKILL.md)。

## 先判断改动属于哪一类

| 改动 | 需要做的 |
| --- | --- |
| 改价、上下架、调整商城顺序 | 只改 `Configs/Shop/Shop.json`，重启服务器 |
| 改名称或介绍 | 只改 `Configs/Items/Items.json`，重启服务器 |
| 新增普通物品、礼物、名片、牌面，或调整契约数值、服刑名片门槛 | 需要远程内容时先发布，再改 `Configs/Items/` 与 `Configs/Shop/`、重启服务器 |
| 修改新账号初始发放 | 只改 `Configs/Accounts/StarterGrants.json`，重启服务器；只影响之后创建的账号 |
| 换图、换音效、改礼物 Lua 节奏或效果 | 只发布远程内容；已下载的玩家回到大厅时在后台更新 |
| 改共享 Lua 模块 | 按 [Lua 规范](../../../Docs/Standards/LuaConvention.md) 修改 `Shared.asset` 的版本号（升主版本时同步修改依赖它的礼物），只发布远程内容；只有共享 Lua 包需要重新下载 |
| 改了礼物宿主接口、程序化图形、远程内容用到的脚本类型或序列化字段 | 内容格式变化：执行 `XLua/Generate Code`（宿主接口变化时），页面、服务端和内容一起发布 |
| 新的消耗方式、新的功能目录、成就奖励逻辑 | 需要改代码，页面与服务端一起发布 |

## 修改配置

1. 物品本身（名称、介绍、图标、可消耗、上限）在 `Configs/Items/Items.json`；名片、牌面、礼物、契约的角色在 `Configs/Items/` 下对应目录；出售在 `Configs/Shop/Shop.json`。`Shop.json` 顺序就是商城顺序，`Gifts.json` 顺序就是礼物菜单顺序；ID 永久不变，名片、牌面 ID 保留 `namecard-`、`cardface-` 前缀。字段名用小写加短横线，每个对象都写全该文件的所有字段，空值写 `""`，见 `Docs/Standards/Configuration.md`；字段表见 `Docs/Systems/Economy.md`。
2. 玩家可能拥有的物品不能从 `Items.json` 删除；停售只在 `Shop.json` 写 `"listed": false`。`namecard-default`、`cardface-default` 必须存在。
3. 在 Unity 执行 `Poker/Validate Server Configuration`（与服务器启动时的校验相同，一次列出全部问题），通过后把文件放到服务器的 `Configs/Items/`、`Configs/Shop/`、`Configs/Accounts/` 并重启服务器。配置有误时服务器会拒绝启动，日志和 stderr 写明文件、条目和字段。
4. 新配置引用的远程地址（`artwork`、`cards`、`effect`）和图标、预览名必须已经发布到资源网站，否则玩家只看到默认外观。

## 发布远程内容

1. 执行 `Poker/Remote Artwork/Prepare Catalogs and Groups`：按配置注册分组、收集共享 Lua 包、重建物品图标集与牌面预览集并校验（含包依赖与锁文件）。
2. 切到 WebGL 平台，执行 `Poker/Remote Artwork/Build for Active Platform`。Console 若提示内容格式与上次页面构建不同，这份内容只能与新页面一起部署。构建更新的 `Assets/RemoteContent/packages-lock.json` 与内容改动一起提交。
3. 上传 `Build/RemoteContent/Content/v1/WebGL/`：先所有 `.bundle`，再 `catalog_v1.*` 与 `content-packages.json`，最后 `content-version.txt`；全部完成后删除网站上不在 `files.txt` 中的旧包。
4. 需要改配置时，内容上传完成后再改配置、重启服务器。

## 发布新页面

页面、服务端与（格式变化时的）内容一起部署：先上传内容，再部署服务端与页面（`page-version.txt` 与 `index.html` 一起上传，使用 `no-cache`）。在线的旧页面回到大厅或登录前会提示刷新；只改 `page-version.txt` 而不换页面会让玩家在第一次刷新后看到 Console 警告，不会被反复拦截。

## 排查

- 玩家看不到新内容：确认 `content-version.txt` 已更新且格式与页面一致；玩家需要回到大厅才会检查。
- 浏览器 Console：`[UnityCache]` 日志显示下载、命中和删除；`localStorage` 的 `poker.content.records` 是每个地址当前的版本。
- 不要修改 `PlayerSettings.bundleVersion`，否则浏览器会清空所有已下载内容。

## 验证

- 配置：`Poker/Validate Server Configuration`、EditMode 测试，并在 `Game.unity` 登录后检查商城、收藏和礼物菜单。
- 内容：Editor Play Mode 默认直接读取 `Assets/RemoteContent`（`Poker/Remote Artwork/Editor Content Source/Local Assets`），用于检查美术与脚本；验证已上传的内容时切到 `Remote Server`，它读取 `client-network.json` 的 `resource-base-url`。浏览器缓存与更新替换用 `python Tools/serve_remote_content.py` 和 WebGL 页面验证首次下载、刷新后命中缓存、回到大厅后的更新替换。
- 动画效果交给用户观看确认；如实说明未做的上传、多人与浏览器验证。
