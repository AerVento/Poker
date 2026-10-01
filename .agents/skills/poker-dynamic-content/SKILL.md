---
name: poker-dynamic-content
description: 在 Poker 项目中不重新构建页面地更新商品与远程内容：编辑 Configs/Shop 的物品、礼物、名片、牌面配置，打包并按顺序发布远程内容，判断何时必须发布新 WebGL 页面，以及排查内容版本、格式版本和页面刷新提示。用于“上架/下架/改价/改名”“加一个物品”“发布新内容”“玩家看不到新内容”等请求；具体美术与脚本制作分别用 poker-cosmetics、poker-gifts。
---

# Poker 动态商品与内容发布

商品列表在 `Configs/Shop/`，美术和礼物脚本是远程内容，两者都能在不重新构建页面的情况下更新。先读根目录 `AGENTS.md`，检查工作区已有改动。

- [货币、商店与物品](../../../Docs/Systems/Economy.md)：四个配置文件的字段与校验规则。
- [远程资源](../../../Docs/Operations/RemoteArtwork.md)：内容版本、格式版本、缺失／失效／生效、页面更新、构建与上传顺序。
- 新款名片或牌面的美术：[poker-cosmetics](../poker-cosmetics/SKILL.md)；新礼物或改礼物动画：[poker-gifts](../poker-gifts/SKILL.md)。

## 先判断改动属于哪一类

| 改动 | 需要做的 |
| --- | --- |
| 改价、上下架、改名称或介绍、调整顺序 | 只改 `Configs/Shop/*.json`，重启服务器 |
| 新增无专属行为的物品、礼物、名片、牌面 | 准备远程内容并发布，再加配置、重启服务器 |
| 换图、换音效、改礼物 Lua 节奏或效果 | 只发布远程内容；已下载的玩家回到大厅时在后台更新 |
| 改了礼物宿主接口、程序化图形、远程内容用到的脚本类型或序列化字段 | 内容格式变化：执行 `XLua/Generate Code`（宿主接口变化时），页面、服务端和内容一起发布 |
| 契约等带购买或还款规则的物品、服刑名片、成就奖励逻辑 | 需要改代码，页面与服务端一起发布 |

## 修改配置

1. 编辑 `Items.json`、`Gifts.json`、`Namecards.json` 或 `CardFaces.json`。数组顺序就是商城或礼物菜单顺序；ID 永久不变。
2. 玩家可能拥有的商品不能删除，只能写 `"Listed": false`。名片、牌面第一项必须是 `default`；三种契约和三款服刑名片必须存在。
3. 在 Unity 执行 `Poker/Shop/Validate Catalogs`（与服务器启动时的校验相同），通过后把文件放到服务器的 `Configs/Shop/` 并重启服务器。配置有误时服务器会拒绝启动，日志写明文件、条目和字段。
4. 新配置引用的远程地址（`Artwork`、`Cards`、`Effect`）和图标、预览名必须已经发布到资源网站，否则玩家只看到默认外观。

## 发布远程内容

1. 执行 `Poker/Remote Artwork/Prepare Catalogs and Groups`：按配置注册分组、重建物品图标集与牌面预览集并校验。
2. 切到 WebGL 平台，执行 `Poker/Remote Artwork/Build for Active Platform`。Console 若提示内容格式与上次页面构建不同，这份内容只能与新页面一起部署。
3. 上传 `Build/RemoteContent/Content/v1/WebGL/`：先所有 `.bundle`，再 `catalog_v1.*`，最后 `content-version.txt`；全部完成后删除网站上不在 `files.txt` 中的旧包。
4. 需要改配置时，内容上传完成后再改配置、重启服务器。

## 发布新页面

页面、服务端与（格式变化时的）内容一起部署：先上传内容，再部署服务端与页面（`page-version.txt` 与 `index.html` 一起上传，使用 `no-cache`）。在线的旧页面回到大厅或登录前会提示刷新；只改 `page-version.txt` 而不换页面会让玩家在第一次刷新后看到 Console 警告，不会被反复拦截。

## 排查

- 玩家看不到新内容：确认 `content-version.txt` 已更新且格式与页面一致；玩家需要回到大厅才会检查。
- 浏览器 Console：`[UnityCache]` 日志显示下载、命中和删除；`localStorage` 的 `poker.content.records` 是每个地址当前的版本。
- 不要修改 `PlayerSettings.bundleVersion`，否则浏览器会清空所有已下载内容。

## 验证

- 配置：`Poker/Shop/Validate Catalogs`、EditMode 测试，并在 `Game.unity` 登录后检查商城、收藏和礼物菜单。
- 内容：本地运行 `python Tools/serve_remote_content.py`，用 WebGL 页面验证首次下载、刷新后命中缓存、回到大厅后的更新替换。
- 动画效果交给用户观看确认；如实说明未做的上传、多人与浏览器验证。
