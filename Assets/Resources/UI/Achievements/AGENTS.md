# 成就图标美术规范（Agent 必读）

本文件适用于本目录内所有成就图标。新增或替换图标前，先阅读仓库根目录 AGENTS.md，再查看本文件和下列实际图片。除非用户明确要求更换整套风格，后续图标应保持为同一套徽章。

## 视觉基准

- 首选参考：`Cards-v1.png`，用于圆框比例、墨绿色背景、金属与象牙白材质。
- 辅助参考：`Champion-v1.png`（金属主体）、`Wealth-v1.png`（筹码／财富主题）、`FoodChain-v1.png`（叙事符号）。
- `Default.png` 为默认奖杯，`FullHouse.png` 为三加二的葫芦符号，已统一到同一风格。旧版紫底、细金线、扁平几何图标已弃用，不作为参考。
- 生成时必须实际查看并提供至少一张上述成品图作为风格参考。仅凭文字提示词容易产生另一套边框、光照或材质。若参考图编辑反复产出画死的棋盘格背景，可用下方统一提示词重新生成，再与上述成品逐项比对；必须检查实际 alpha 通道，不能只看预览。

## 统一画法

| 元素 | 规范 |
| --- | --- |
| 轮廓 | 正视、居中的单枚圆形徽章，完整圆框不裁切；方形画布四周约留 5% 透明空间，圆直径约占 90%。 |
| 外框 | 有厚度的暖金色拉丝金属圈，简洁倒角和内缘；比例、亮度参照 Cards-v1，不叠加额外花边或桂冠。 |
| 底面 | 深墨绿／祖母绿珐琅，中心略亮、边缘较暗，轻微细腻纹理；近似色域 #032D23 至 #08694E。 |
| 主体 | 暖金色浮雕，扑克牌可用象牙白；近似主色 #D5A543、受光 #FFE4A1、象牙白 #F4EBD3。颜色是方向，以成品参考为准。 |
| 光照 | 左上方柔和主光，金属边缘克制的高光，右下轻微接触阴影；浅浮雕立体感，不能改成纯色剪影、像素画或写实场景照片。 |
| 构图 | 单一、清晰的主题，主体在内圈留足呼吸空间，视觉重量与已有图标相近；优先大形状，避免细线和碎小装饰。 |
| 透明 | 圆框外必须是真实 alpha 透明；不能把棋盘格、黑底或白底画进图片，也不能带大面积外发光。 |
| 内容 | 默认无文字、字母、数字、水印、奖励金额或成就名称。可用牌花等图形表达；葫芦采用清晰的三加二符号。 |

以实际约 92×92 的 UI 显示尺寸判断可读性，同时与现有图标并排查看。不要只在生成原图放大后判断质量。裁切、圆框粗细、主体比例、绿底色相和金属高光都应一致。

## 主题与复用

同系列成就优先复用：累计盈利用 Wealth，单手盈利与皇家主题用 Champion，常规牌型用 Cards，葫芦用 FullHouse，大鱼吃小鱼用 FoodChain，缺省用 Default。只有现有符号不能表达新主题时才增加图标。

未解锁时的变暗由 UI 实时处理，不要把灰色、锁标、进度或领取状态烘焙到图片中。需要新主题时只改变中央符号，保持圆框、背景、材质与光照。

## 可复用生成提示词

使用内置 image_gen；把 Cards-v1.png 作为风格参考图，将下文 SUBJECT 替换成主题。若在改已有图标，也提供旧图并明确其仅用于主体含义参考。

```text
Create one achievement icon for a Unity poker game.
Use the supplied Cards-v1 image as the approved style and badge geometry reference.
Match its centered circular badge, thick warm brushed-gold beveled rim,
deep emerald enamel face, top-left lighting, gentle three-dimensional relief,
ivory accents and contrast. Keep the complete rim within a square canvas,
with about 5% transparent margin on every side.
SUBJECT: [one concise emblem description].
Keep bold, simple shapes readable at 92 pixels and comfortable space inside the rim.
Change only the central emblem; preserve the reference palette, materials,
framing, light direction and visual weight.
Real transparent alpha outside the circle. No checkerboard pattern, purple fill,
flat vector or pixel-art treatment, words, letters, numbers, watermark,
extra badges, elaborate ornament, excessive glow or cropped edges.
```

不要为了省事用程序重新绘制旧式纯色几何奖杯／纸牌。生成图需要目视检查；若主体数目有含义（例如三加二），确认数量与遮挡正确。

## 文件与 Unity 导入

- 格式：正方形 PNG，RGBA，保留生成原图（通常不低于 1024×1024）；通过导入配置控制运行时尺寸，不必重采样原图。
- Unity：Texture Type = Sprite (2D and UI)，Sprite Mode = Single，Max Size = 256，Alpha Is Transparency = 开，Generate Mip Maps = 关，Compression = None，Filter = Bilinear，Wrap = Clamp，Pivot = Center，Pixels Per Unit = 100。
- 新主题使用明确的英文文件名，例如 NewTheme-v1.png，并生成和保留对应 .meta。
- 用户要求替换已有图标时，覆盖其 PNG 内容但保留文件路径和 .meta GUID；尤其不要改动 Default、FullHouse 的路径，否则会影响 Resources 加载和 prefab 引用。
- 图标必须保存到当前目录，不能只留在生成工具的输出目录。完整提示词、参考图和来源记录在 `Docs/AchievementIconGeneration.json`（仓库根目录下）。
- UI 构建工具只检查默认图标是否存在，不负责生成美术；资源丢失应从版本库恢复或按本规范重新生成。
- 新资源和本目录的文档都需对应 .meta。不要为换图无故重建面板、场景或其他资源。
