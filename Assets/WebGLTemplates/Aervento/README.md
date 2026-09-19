# Aervento WebGL 模板

适用于 Unity **2022.3.52f1c1**。基于该版本自带 Default 模板的加载流程与 TemplateData 图片，提供占满浏览器可用区域的 Unity WebGL 画布。

## 在 Unity 中启用

1. 将本文件所在的完整 `Aervento` 文件夹及 `.meta` 复制到目标项目的 `Assets/WebGLTemplates/` 下。
2. 在 **Project Settings → Player → WebGL → Resolution and Presentation → WebGL Template** 选择 **Aervento**。
3. 按原流程构建 WebGL。发布完整构建目录，包括 `TemplateData`。

无需添加 C# 脚本、组件或修改场景。游戏名称、宽高和构建文件名来自 Unity 模板变量，没有写死 Poker 路径。迁移到其他 Unity 大版本时，应对照那个版本的默认模板核对加载参数。

## 文件职责

| 文件 | 职责 |
| --- | --- |
| `index.html` | 当前 Unity 版本的游戏加载流程 |
| `TemplateData/` | Unity 加载图片和游戏容器布局 |

页面不显示网站导航、Unity 页脚、产品名或模板全屏按钮。网页占满浏览器可用区域，游戏画布保持构建时的设计宽高比并居中等比显示。横屏空间允许时 Fit Height，竖屏 Fit Width；宽高比不匹配时留边，不裁切内容。适配完全由 WebTemplate 完成，Unity UI 不按方向重排。页面不会调用 Fullscreen API，也不会强制旋转设备；软键盘改变可用视口时更新画布尺寸。

`index.html` 对 `TemplateData/style.css` 使用版本查询参数。修改布局 CSS 时同步更新此参数，避免 HTML 已更新但浏览器/CDN 仍返回旧样式。发布时仍需上传完整的模板文件；仅刷新页面或给页面地址加参数，不会改变 CSS 自身的缓存地址。

## 验证方法

模板源 `index.html` 含 Unity 宏，需构建后通过 HTTP/HTTPS 打开（例如 Unity 的 Build And Run），不能直接双击源码或将 `file://` 当作运行环境。

验证桌面和移动端横竖屏的画布尺寸、窗口缩放、刘海安全区和软键盘输入。横屏确认按高度适配、竖屏确认按宽度适配，页面无滚动条且不显示导航和 Unity 页脚。模板源码需通过 HTTP/HTTPS 构建后验证，不能直接双击源码。

参考：[Unity 2022.3 WebGL Templates](https://docs.unity3d.com/2022.3/Documentation/Manual/webgl-templates.html)。
