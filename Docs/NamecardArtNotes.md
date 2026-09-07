# 名片商品美术记录

最新进展：11 款横向名片已更新为细边框与不透明主题背景，源 PNG 位于 `Assets/Sprites/Namecards/*-v2.png`，商品 SO 已引用新图。尺寸与留白标准见 [NamecardArtStandard.md](NamecardArtStandard.md)。下面的 v1／v2 章节保留历史生成记录。

日期：2026-09-06。按照用户“先生成几个商品美术让我看看效果”的要求，本次仅制作供选择风格的静态名片底图，不代表商店或装备功能已实现。商店直接展示少量商品，不提供筛选功能，规则详见 [商店计划](NamecardShopPlan.md)。

## 素材与预览

使用内置 `image_gen.imagegen` 分别生成 4 款，每款一次独立调用；未使用 CLI 或替代绘图程序。四张 PNG 均为 **2048×768、8:3、RGBA**，适用于横向玩家名片的初步视觉评审。原图已复制到项目，保留原始像素与透明通道，未覆盖既有资源。

| 名称 | 文件 | 视觉方向 |
| --- | --- | --- |
| 星河 | [galaxy-v1.png](NamecardPreviews/galaxy-v1.png) | 蓝紫星云、银色边缘、局部星光，右侧星旋作为识别点。 |
| 赤焰 | [ember-v1.png](NamecardPreviews/ember-v1.png) | 黑曜石与熔岩裂纹，橙红火焰集中在边缘。 |
| 霓虹 | [neon-v1.png](NamecardPreviews/neon-v1.png) | 青蓝到紫红的灯带，深色科技面板与几何边角。 |
| 鎏金 | [gilded-v1.png](NamecardPreviews/gilded-v1.png) | 墨绿底色、金属卷纹与金粉，更接近当前牌桌的绿色与金色。 |

### 星河

![星河名片底图](NamecardPreviews/galaxy-v1.png)

### 赤焰

![赤焰名片底图](NamecardPreviews/ember-v1.png)

### 霓虹

![霓虹名片底图](NamecardPreviews/neon-v1.png)

### 鎏金

![鎏金名片底图](NamecardPreviews/gilded-v1.png)

## 生成提示词与原图记录

完整实际提示词保存在 [prompts-v1.json](NamecardPreviews/prompts-v1.json)，包含共同构图约束与每款主题描述。来源路径、实际尺寸、色彩模式、透明度范围与 SHA-256 保存在 [manifest-v1.json](NamecardPreviews/manifest-v1.json)。

共同要求：横向圆角名片、正视平面、深色低对比中心、装饰集中在边缘和右侧；为左上姓名、左下筹码、右上倒计时和下方状态徽记留白；不生成任何文字、价格、金额、水印或状态图标。提示词请求了 1536×576，工具实际交付 2048×768，比例一致；原样保存实际输出。

参考依据是用户提供的牌桌玩家列表截图，只用于理解比例与信息布局；本次没有编辑截图，也没有把截图中的姓名或金额烘焙进素材。

## 已检查与接入前待验证

- 已逐张查看生成结果：4 款主题可区分，主体为单张横向名片，信息区域保持深色，无烘焙文字、金额或水印。
- 已校验 4 张 PNG 均可解码，尺寸一致，并核对项目副本与原图 SHA-256 完全一致。RGBA 透明通道被原样保留；图像包含半透明边缘和略有透明度的内部，正式接入需要检查在牌桌底色上的合成效果。
- 当前只是静态图。发光是图片中的视觉表现，不是已经实现的流光或粒子动画。是否增加轻微 Unity 动效仍由后续方案确定。
- 当前素材在 `Docs` 下，未修改 UI prefab 或运行时代码，因此本次没有执行 Unity 编译、联机或商店功能测试。
- 正式接入前需在约 280×104 的实际玩家行及窄窗口下检查最长姓名、999999／1000K 金额、倒计时与 D/SB/BB 徽记；这一步尚未完成，不能只凭大图认定小尺寸可读性已通过。
- 外观底图必须与当前行动者、准备、弃牌、断线的状态层分开。尤其鎏金的装饰边缘可能接近现有行动金边，需要额外醒目的行动状态标记，并验证切换状态不会覆盖名片。
- 各款圆角、外圈留白和装饰宽度有少量差异，正式接入需统一 Sprite 显示边界或遮罩，避免各行尺寸跳动。按实际比例处理，不直接九宫格拉伸星旋、灯带或金饰。商品预览与实际装备使用同一素材和显示规则。

当前主题为首批设计提案，商品价格、最终款式与动效尚未定稿。

## 扩展美术与 UI（2026-09-06）

用户新增每页 4 款分页、商品名称／介绍、商品 SO 配置，以及默认款天然拥有的要求。已完成商店 UI 原型，详见 [UI 与配置说明](NamecardShopUI.md)。原有 4 款加下列 7 款美术，再加复用现有 PlayerIdle.png 的免费“经典”，共 12 款、3 页。

| 名称 | 美术方向 | 原图 |
| --- | --- | --- |
| 极霜 | 冰晶凝住锋芒，极光掠过桌沿。 | [frost-v1.png](NamecardPreviews/frost-v1.png) |
| 幽海 | 潜入无声深海，让幽光替你发声。 | [abyss-v1.png](NamecardPreviews/abyss-v1.png) |
| 龙珀 | 青玉藏龙，云纹之间自有威仪。 | [jade-v1.png](NamecardPreviews/jade-v1.png) |
| 绯樱 | 一席夜樱，把锋芒藏进温柔。 | [sakura-v1.png](NamecardPreviews/sakura-v1.png) |
| 时轮 | 黄铜齿轮转动，掌握自己的节奏。 | [clockwork-v1.png](NamecardPreviews/clockwork-v1.png) |
| 蚀日 | 日冕划破暗夜，沉默亦有引力。 | [eclipse-v1.png](NamecardPreviews/eclipse-v1.png) |
| 幻晶 | 万色折光，让每次亮相都不同。 | [prism-v1.png](NamecardPreviews/prism-v1.png) |

七款均通过内置 image_gen.imagegen 独立生成，每款一次；完整实际提示词见 [prompts-v2.json](NamecardPreviews/prompts-v2.json)，原图来源、尺寸、色彩模式及 SHA-256 见 [manifest-v2.json](NamecardPreviews/manifest-v2.json)。首批提示词记录也已补齐为实际 4 条，未使用 CLI。

极霜、幽海、龙珀、绯樱为 RGBA；时轮、蚀日、幻晶原图为 RGB。对后三款又用 Image 各做一次透明背景修订，但输出仍为带棋盘背景的 RGB，并非真实透明图片，因此没有选用修订图，也未将它们导入项目。编辑提示词保留在 [prompts-alpha-v2.json](NamecardPreviews/prompts-alpha-v2.json)。没有用脚本伪造或宣称修订图拥有 Alpha。

选定原图已原样复制到 Assets/Sprites/Namecards/，全部副本与原图哈希一致。Unity 使用统一的圆角 Mask 衔接各款边界，保留原图画面；遮罩不是给 RGB 图片增加 Alpha。预览姓名／余额向内缩进，避开高亮边饰。

原图统一为 2048×768。Unity Sprite 导入设置为最长边 1024、关闭 mipmap、Clamp、FullRect 与高质量压缩；源 PNG 不缩放、不重绘，透明通道原样保留。正式 GamePanel／RoomPanel 的小尺寸玩家行与状态叠层仍待真实装备同步阶段验证。

已逐张查看 7 款生成结果，未包含文字、金额或水印，均为单款名片；并在商店三页检查预览。全部都是静态图，图中的发光不代表已经实现动画。默认款复用项目原有素材，不重新生成。


## 细边框与主题背景修订（2026-09-07）

用户要求新名片替换绿色底板，统一边框上限与文字留白；随后明确内部需要与主题相符的背景，不能只剩纯黑或透明中心。因此 11 款均重新生成：保持 2048 × 768，收紧边框，加入星云、熔岩、极光、云龙、樱枝等低对比背景。

- 最终横图：`Assets/Sprites/Namecards/{id}-v2.png`，11 张均为 2048 × 768 RGB PNG，内部完全不透明。Unity 导入后为 1024 × 384 Sprite。
- 完整图片为生成工具原样输出；已逐张查看并核对源图与工程副本 SHA-256，没有用脚本补画、拉伸或伪造 Alpha。
- 生成使用内置 `image_gen.imagegen`，未使用 CLI。边框中间稿提示词见 [prompts-v3-thin-frame-studies.json](NamecardPreviews/prompts-v3-thin-frame-studies.json)，最终背景与两款角部修订提示词见 [prompts-v4-themed-backgrounds.json](NamecardPreviews/prompts-v4-themed-backgrounds.json)。
- 最终尺寸、色彩模式、资源路径和 SHA-256 见 [manifest-v4.json](NamecardPreviews/manifest-v4.json)。v1 原图保留，商品 ID、价格、默认款和库存不变。
- 边框上限按 64 px、文字内缩按 96 px 制作；该约束针对明亮实体装饰，暗色背景可延伸到整个内容区。照片式高光、大片白光和密集亮点不适合作为文字底图。
- 用户确认另一个任务正在修改牌桌 UI，本任务后续专注美术与资源导入，不再操作其牌桌布局。这里的美术目检不能替代最终格子上的 UI 可读性验收；本轮未构建。

用户另要求同主题 4:3 专用构图，首张以星河制作专用样例：生成原图为 1448 × 1086，Unity Sprite 为 1024 × 768，保持与横图相同的边框材质与星云风格。其文件与导入说明见美术标准中的 4:3 小节，不能用宽图拉伸代替。

## 2026-09-07：4:3 全套与 SO 接入

补齐星河以外 10 款 4:3，共 11 款；每张以同款最终宽图和星河 4:3 作为参考，重新构图，保持薄边框和低对比主题背景。10 张本轮生成图全部一次得到精确 1448×1086、RGB、不透明的源图；不做外部图像裁剪、拉伸或后处理，由 Unity 等比导入为 1024×768。

每个商品 SO 新增 `_artwork4x3` 引用，保留 `_artwork` 宽图。名片目录提供 ID + 目标比例选图；玩家现有 SetNamecard 接口沿用。共享外观组件保持图片比例，并把内容安全区放到实际图片内部；三个玩家档位和房间补 Content 引用，保留另一项布局工作的根节点配置；8 / 16 人档金额自动缩小下限补调为 14 / 12，以适应实际图片安全区。

提示词和文件哈希见 NamecardPreviews/prompts-v6-4x3-collection.json、manifest-v6-4x3-collection.json；图册见 NamecardGallery.md。测试与实际 UI 结果记入 NamecardShopValidation.md。
