# 幻夜双影：双比例名片美术

后续状态：用户已确认接入下个版本商城，商品 `twilight-duo`，售价 50,000 筹码。配置与 Unity 验证见 发布记录。以下保留最初美术交付时的记录。

2026-09-08。使用用户提供的两张插画，通过内置 image_gen 分别编辑；名称仅用于本套美术文件识别，尚未创建商品 ID 或上架。

## 设计依据

核对了 `Docs/Standards/NamecardArt.md`、`Docs/Systems/Namecards.md`、`Assets/Scripts/UI/Namecards/NamecardLayout.cs` 和 `Assets/Scripts/Game/Editor/NamecardArtValidation.cs`。

保留双人背靠背、长发、蓝紫洞窟与花海。将白发、肤色、光球和金色花朵压成低对比暮色，去除中央强光与亮粒子；搭配银紫细线和小型花晶角饰。两版独立构图，未将横版裁切或压扁为紧凑版。背景保持不透明且有完整场景，没有烘焙姓名、筹码或状态图标。

制作目标：宽版按 2048×768，紧凑版按 1024×768 换算，亮边饰限制在外侧 64 px，文字区四周内缩 96 px。内部人物作为低对比背景保留。边饰与文字可读性仍须用 Unity 参考线和真实 UI 验证，生成提示词本身不构成验收通过。

## 文件

| 用途 | 文件 | 已核验源尺寸 | 预期 Unity 导入尺寸 |
| --- | --- | --- | --- |
| 宽版 | [twilight-duo-wide-v1.png](../../Assets/Sprites/Namecards/twilight-duo-wide.png) | 2048×768，精确 8:3 | 1024×384 |
| 紧凑版 | [twilight-duo-4x3-v1.png](../../Assets/Sprites/Namecards/twilight-duo-4x3.png) | 1448×1086，精确 4:3 | 1024×768 |

![宽版](../../Assets/Sprites/Namecards/twilight-duo-wide.png)

![紧凑版](../../Assets/Sprites/Namecards/twilight-duo-4x3.png)

[完整实际提示词](twilight-duo-prompts-v1.json)。紧凑版提示词请求 1024×768，工具实际返回 1448×1086，仍符合仓库紧凑源图上限 2048×1536；原样保留，不进行像素重采样。

## 验证与接入状态

- 已逐张查看生成图，核对主题、构图、低对比处理和无文字设计。
- 使用 System.Drawing 成功解码两张 PNG，均为 Format24bppRgb，完整不透明。
- 复制前后 SHA-256 一致，宽版为 `C438F554A0B119DBF521138352EDD85FF059E52A778BBD24E29A5D27DBDEECE0`，紧凑版为 `02C4405CEFD3CA185FD6353DE3BD878135D1A4B031B486B59A57C26D8B7177FC`。
- 本次仅新增 Docs 下两张图片、本说明及提示词；没有修改现有图片、Assets、SO、prefab、商城配置或运行时代码。
- 未执行 Unity 编译、商品图片校验菜单、实际尺寸 UI 叠加或联机测试；当前为美术交付稿，不代表已完成游戏内验收。
- 接入时按既有文档设置 Sprite / Single / Full Rect、Max Size 1024、关闭 Mip Maps、Clamp、NPOT None，保留 Unity 生成的 .meta；分别在宽版和 Player8 紧凑槽验证姓名、金额与状态。无需因图片调整现有文字布局。
