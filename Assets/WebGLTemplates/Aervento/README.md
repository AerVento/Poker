# Aervento WebGL 导航模板

适用于 Unity **2022.3.52f1c1**。基于该版本自带 Default 模板的加载流程与 TemplateData 图片，添加与游戏代码独立的静态导航栏。导航样式参考 https://www.aervento.top/：浅蓝背景、白字、桌面横排与移动端折叠菜单。

## 在 Unity 中启用

1. 将本文件所在的完整 `Aervento` 文件夹及 `.meta` 复制到目标项目的 `Assets/WebGLTemplates/` 下。
2. 在 **Project Settings → Player → WebGL → Resolution and Presentation → WebGL Template** 选择 **Aervento**。
3. 编辑 `navigation.json`，然后按原流程构建 WebGL。发布完整构建目录，包括导航文件与 TemplateData。

无需添加 C# 脚本、组件或修改场景。游戏名称、宽高和构建文件名来自 Unity 模板变量，没有写死 Poker 路径。迁移到其他 Unity 大版本时，应对照那个版本的默认模板核对加载参数。

## 修改导航

```json
{
    "items": [
        { "label": "主页", "url": "https://www.aervento.top/", "newTab": true },
        { "label": "关于我", "url": "https://www.aervento.top/about/", "newTab": false }
    ]
}
```

- 数组顺序就是导航顺序，增加或删除对象即可增删链接。
- `label`：必填，非空文字，以纯文本显示。
- `url`：必填，支持 HTTP/HTTPS 完整 URL 或相对路径。相对路径以游戏页面地址为基准：`./help.html` 指向游戏目录，`/about/` 指向当前域名根目录。跨域跳转请使用完整 URL。
- `newTab`：可选布尔值，省略时为 `true`（新标签页）。设为 `false` 在当前页打开，会离开正在运行的游戏。不要把布尔值写成字符串 `"false"`。
- `{"items": []}` 可隐藏导航。

导航使用页面旁的 `./navigation.json`，在页面打开/刷新时请求一次，5 秒超时。单项配置有误时跳过该项并输出浏览器控制台提示；配置文件缺失、超时、JSON 错误或没有有效项目时隐藏导航，Unity 仍独立加载。

## 上线后更新配置

可单独替换部署目录中与 `index.html` 同级的 `navigation.json`，不需要重新编译游戏。已打开的页面在下次刷新时更新，不会轮询或自动改变正在显示的导航。

客户端请求使用 `cache: "no-store"`。同时在静态服务器/CDN 上为 **各游戏路径下的 navigation.json** 设置响应头 `Cache-Control: no-store`，并确保 CDN 不覆盖这个规则；若旧文件已被缓存，先清除旧缓存。这个响应头需要在部署环境配置，模板本身无法设置服务器响应头。游戏的 Build 资源继续沿用原有缓存及压缩配置。

同步修改项目里的源配置，避免下一次完整发布覆盖服务器上的修改。每个游戏默认维护自己的配置，样式和脚本可整套复制复用。

## 文件职责

| 文件 | 职责 |
| --- | --- |
| `navigation.json` | 导航名称、地址、顺序及打开方式 |
| `navigation.js` | 配置加载、校验、链接创建及菜单操作 |
| `navigation.css` | 仅导航组件的样式，选择器带 `aervento-navigation` 前缀 |
| `index.html` | 导航容器及当前 Unity 版本的游戏加载流程 |
| `TemplateData/` | Unity 加载图片和游戏容器布局 |

导航占据自己的布局空间，移动端展开时将游戏往下推。游戏容器根据剩余空间缩小，保留构建宽高比且不超过构建默认宽度；游戏内 UI 适配仍由 Unity 负责。没有 Ghost 运行时、搜索服务、前端框架或外部字体依赖。

## 验证方法

模板源 `index.html` 含 Unity 宏，需构建后通过 HTTP/HTTPS 打开（例如 Unity 的 Build And Run），不能直接双击源码或将 `file://` 当作运行环境。

验证桌面导航、窄屏菜单、Tab/Enter/Escape、链接目标、窗口缩放及全屏。修改部署配置后刷新，应看到更新；临时移走配置或写入无效 JSON 后刷新，游戏应继续启动。

参考：[Unity 2022.3 WebGL Templates](https://docs.unity3d.com/2022.3/Documentation/Manual/webgl-templates.html)。
