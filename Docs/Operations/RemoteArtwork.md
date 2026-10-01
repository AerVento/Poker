# 远程资源

名片、非经典牌面、礼物特效（Lua 脚本与美术）、全部物品图标和牌面小预览都是远程内容：从资源网站按需下载，不在 WebGL 页面里。发布平台只有 WebGL；Windows 构建只用于 Editor 开发验证。

## 范围

- Addressables 固定为 1.21.21，Unity 保持 2022.3.52f1c1。
- 每个商品一个 LZ4 包：17 款非默认名片、3 套非经典牌面、4 种礼物；另有两个公共包：物品图标 `icons/items`、牌面预览 `previews/cardfaces`。
- 本地保留：默认名片、经典牌面与其预览、礼物菜单的默认图标、牌面显示 prefab（`Assets/Resources/SO/ShopLocalArt.asset`），以及商品之外的界面。商城和收藏在图标下载前显示卡片自带的宝箱线条占位。商品的名称、介绍和地址来自服务器下发的配置（见 [货币、商店与物品](../Systems/Economy.md)）。
- 远程入口在 `Assets/RemoteContent/`：`namecards/`、`cardfaces/`、`gifts/`、`icons/Items.asset`、`previews/Cardfaces.asset`。地址保持商品 ID 原样（如 `cardfaces/jewel-court`），文件名为大驼峰，由 `RemoteAssets.SourcePath(address)` 换算。

## 内容版本

- 网站上只有一个内容目录 `Content/v1/<平台>/`。每次构建都完整重建，目录中只留当前目录文件引用的包。
- 构建在同一目录生成 `content-version.txt`：

```text
version=20261001T095827Z
format=e91b808248d6fc01
catalog=catalog_v1.json
```

  `version` 是 UTC 构建时间，就是内容版本；`format` 是内容格式版本；`catalog` 是远程目录文件名。客户端只认资源网站上的这份文件，不经过游戏服务器。
- 不额外校验目录摘要，依赖受控 HTTPS 地址和目录中记录的每个包的 CRC（各组启用 `UseAssetBundleCrc`）。

### 内容格式版本

页面中的 C# 代码负责读取内容包里的序列化数据和 Lua 脚本。格式版本是以下内容的哈希，由 `Game.Editor.ContentFormatVersion` 自动计算，不手动维护：

- 远程内容组中资源实际引用的全部脚本类型，以及它们的序列化字段（递归到可序列化的嵌套类型）；
- 标记 `[ContentScriptApi]` 的类型的公开成员与常量（礼物宿主接口、Lua 引导代码）；
- Unity 版本。

构建 WebGL 页面时把格式版本写入页面；构建内容时写入 `content-version.txt`。两处用同一个函数，代码不变时结果相同。页面更新只要没改到这些类型和接口，已下载的内容继续可用。构建 WebGL 内容时若格式与上一次页面构建不同，Console 会提示这份内容必须与新页面一起部署。

## 浏览器中的三种状态

客户端在 `localStorage`（键 `poker.content.records`）里为每个地址记录当前使用的版本：包地址、CRC 等加载参数、包内资源键，以及下载时的格式版本。包本身由 Unity WebGL 的缓存保存在浏览器 Cache Storage 与 IndexedDB `UnityCache` 中，不写入 `persistentDataPath`。

| 状态 | 判定 | 名片／牌面／图标／预览 | 礼物 |
| --- | --- | --- | --- |
| 缺失 | 没有记录，或记录的格式版本与页面不同 | 显示默认外观并开始下载，完成后替换 | 送出：投掷后下载，加载成功才发请求；接收：下载完才播放 |
| 失效 | 有记录，但与当前目录中的版本不同 | 先显示旧版本，后台下载新版本后替换 | 直接用旧版本，后台下载新版本 |
| 生效 | 记录与当前目录一致 | 正常显示 | 正常播放 |

- 每个地址在浏览器中最多保留一份可用版本。格式版本不同的记录按缺失处理，并删除旧包。
- 失效资源按记录直接加载旧包，不经当前目录。旧包加载失败（浏览器已清理、网站已删除）时清除记录，按缺失下载当前版本。
- 替换顺序：新包只下载到浏览器缓存、不加载；完成后通知使用方释放旧版本，旧版本卸载后从浏览器缓存删除，记录改为新版本，下一次获取加载新包。名片和牌面在替换间隙短暂显示默认外观；正在播放的礼物持有旧版本直到结束，期间新的获取等待替换完成。旧版本 30 秒内仍未被释放时放弃本次替换，下次回到大厅再试。
- 新旧包的内部名相同（`m_UniqueBundleIds` 关闭），因此同一时刻只能加载一个版本。
- 下载失败：缺失的保持默认外观，礼物按 [礼物](../Systems/Gifts.md) 的重试规则处理；失效的继续使用旧版本，下次回到大厅再试。
- 页面的 `productVersion`（`PlayerSettings.bundleVersion`，当前 `1.0`）一旦改变，Unity 会在页面启动时清空整个浏览器缓存。不要随意修改；清空后已有记录会按需重新下载。

## 启动与回到大厅

- 页面启动：读取 `content-version.txt` 和其中指定的目录，均绕过缓存；运行时只使用这份远程目录，页面内置的目录不参与查找。读取失败时有限重试，期间名片、牌面显示默认外观，礼物不可用；网站内容的格式版本与页面不同时同样不采用。不预下载任何内容。
- 每次进入大厅：先检查页面版本（见下）；页面未过期时再读一次 `content-version.txt`。版本相同不处理；格式不同不采用；版本不同则换用新目录，并在后台依次下载已下载过且发生变化的资源。从未下载的资源不在大厅主动下载。检查与下载都不打断玩家。

## 页面更新

- 构建 WebGL 页面时自动生成页面版本（UTC 构建时间），写入页面本身，并在输出目录生成 `page-version.txt`。
- 登录前（手动登录、保存的登录、断线后重新登录）和每次进入大厅读取 `page-version.txt`。与页面自身版本不同时弹出提示，只有“刷新”按钮；刷新前不能登录。读取失败时不阻止游戏。
- 刷新会在地址上加 `?pokerPage=<网站上的页面版本>`。若刷新后仍是旧页面（新页面未上传或被缓存），不再重复拦截，只在浏览器 Console 写警告，避免玩家被困在提示里；之后网站版本再变化时照常提示。
- 新页面与重新构建的服务端同时部署；部署时在线玩家会断线，登录前的检查保证旧页面不会连接新服务器。

## 远程内容管理器

`Game.Content.RemoteAssets` 统一负责内容版本与目录、本地记录、状态判定、共享下载（只下载不加载、真实进度、有限重试）、替换和引用计数租约。名片外观、牌面、礼物、物品图标、牌面预览和下载状态视图都只通过它取资源。

| 代码 | 职责 |
| --- | --- |
| `RemoteAssets.cs` | 初始化、获取租约、按记录加载、释放与闲置清理（最多暂留两个闲置资源，15 秒后释放；被替换的版本立即卸载） |
| `RemoteAssets.Download.cs` | 状态与进度查询、下载、后台更新队列、替换、大厅更新检查 `CheckForUpdatesAsync` |
| `RemoteContentRules.cs` | 不依赖 Unity 的规则：记录、版本文件、状态判定 |
| `RemoteContentCatalog.cs` | 解析远程目录，记录与加载位置的相互转换 |
| `RemoteBundleStore.cs`、`RemoteContent.jslib` | WebGL：通过 Unity 自己的 `cachedFetch` 只下载、删除单个缓存条目、读写 `localStorage`；Editor：`Caching` 与 `PlayerPrefs` |
| `RemoteAssetBinding<T>` | 视图持有资源；新版本就绪时释放旧版本、显示默认外观并重新获取；失败最多三轮，间隔 30 秒 |
| `ProductArt` | 物品图标与牌面预览两个公共包，整个会话各持有一份 |
| `Game.PageVersion`、`UI/MsgBox/PageUpdatePrompt.cs` | 页面版本检查与刷新提示 |

`RemoteContent.jslib` 依赖 Unity WebGL 加载器的内部实现（`Module.cachedFetch`、`Module.UnityCache`），升级 Unity 时需要重新验证下载、命中缓存和删除条目。

## 构建

Unity 菜单：

1. `Poker/Remote Artwork/Prepare Catalogs and Groups`：按 `Configs/Shop` 为每个商品注册分组，重建物品图标集和牌面预览集，并运行校验。
2. `Poker/Remote Artwork/Optimize Texture Imports`：应用远程美术的导入压缩设置，保留源图。
3. `Poker/Remote Artwork/Validate First Build Dependencies`：检查每个远程入口独立成组、配置中的地址都已准备、图标和预览齐全、页面不引用远程美术（界面主题图和契约图标除外）、礼物脚本可加载且礼物包之间不共享美术、本地经典牌组完整。
4. 切换到目标平台，执行 `Poker/Remote Artwork/Build for Active Platform`。输出 `Build/RemoteContent/Content/v1/<平台>/`：包、目录、`content-version.txt` 和文件清单 `files.txt`；旧包会被删除。
5. 可选：WebGL 平台下执行 `Poker/Remote Artwork/Build WebGL Deployment Example`，输出配套的页面到 `Build/RemoteContent/WebGLGame`，配置为读取旁边的 `../Content/v1`。

页面构建不会重新构建内容（`BuildAddressablesWithPlayerBuild` 关闭），但要求当前平台的内容至少构建过一次。页面构建前还会检查商品配置和 xLua 生成代码是否过期。

## 发布

- 只改内容（脚本、美术、音效）：构建内容后按顺序上传——先上传所有 `.bundle`，再上传目录文件，最后上传 `content-version.txt`；上传完成后删除网站上不在 `files.txt` 中的旧包。正在下载旧包的页面会按下载失败处理，回到大厅时转到新版本。
- 只改商品配置：编辑服务器上的 `Configs/Shop/*.json` 并重启服务器；新商品引用的远程内容要先按上一条发布。
- 改了页面代码：页面、服务端和（格式变化时）内容一起部署，内容仍按上述顺序上传。

网站结构示例：

```text
https://example.com/poker/
  index.html
  page-version.txt
  Build/                    # WebGL 页面
  Configs/Network/client-network.json
  StreamingAssets/aa/       # 构建生成，完整保留
  Content/v1/WebGL/
    content-version.txt
    catalog_v1.json
    catalog_v1.hash
    *.bundle
    files.txt
```

`client-network.json` 的 `resourceBaseUrl` 为空时使用页面所在目录下的 `Content/v1`；也可以写完整地址（如 `https://assets.example.com/poker/v1`，不含末尾平台名），跨域时资源服务器需返回 CORS 头。目录中 `https://poker-content.invalid` 是构建占位地址，运行时替换为配置地址。正式 HTTPS 页面必须使用 HTTPS 资源地址。

### 缓存头

页面模板（`Assets/WebGLTemplates/Aervento/index.html`）让 Unity 对 `.bundle` 使用不可变缓存，对 `.json` 与 `.txt` 不写入浏览器缓存库，其余构建文件每次重新验证。网站应配合：

```nginx
location /Content/ {
    root /var/www/poker;
    add_header Access-Control-Allow-Origin "*" always;
    add_header Access-Control-Expose-Headers "Content-Length, ETag, Last-Modified" always;
    add_header Cache-Control "no-cache" always;
    try_files $uri =404;
}

location ~ ^/Content/.*\.bundle$ {
    root /var/www/poker;
    default_type application/octet-stream;
    add_header Access-Control-Allow-Origin "*" always;
    add_header Access-Control-Expose-Headers "Content-Length, ETag, Last-Modified" always;
    add_header Cache-Control "public, max-age=31536000, immutable" always;
    try_files $uri =404;
}
```

`content-version.txt`、`page-version.txt`、`client-network.json` 使用 `no-cache` 或 `no-store`。不存在的文件必须返回 404，不能改写成首页；未生成 gzip/Brotli 文件时不要手工添加 `Content-Encoding`。

## 本地验证

安装 Python 后，在项目根目录运行：

```powershell
python Tools/serve_remote_content.py
```

这会提供 `http://127.0.0.1:18080/Content/v1/`，只绑定本机。Editor/Windows 中空 `resourceBaseUrl` 默认使用 `http://localhost:18080/Content/v1`；Editor Play Mode 总是读取 `StandaloneWindows64` 内容，需要先在 Windows 平台构建内容。浏览器测试时把 WebGL 页面放在 `Build/RemoteContent` 下并通过同一服务器打开。

浏览器开发者工具中可以检查：Network 中 `.bundle` 的首次请求与缓存命中、`localStorage` 的 `poker.content.records`，以及 Console 中 `[UnityCache]` 的下载、命中和删除日志。
