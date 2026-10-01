# 恶魔服役名片：三档美术预览

日期：2026-09-08。本轮在用户认可的「狱契之印」基础上，完成两款更高等级名片的宽版和独立紧凑版。使用内置 image_gen，原图直接复制到本目录，未裁切、拉伸或后处理。

## 等级递进

| 档位 | 暂定名称 | 识别元素 | 红色与火焰 |
| --- | --- | --- | --- |
| 恶魔 | 狱契之印 | 双角、恶魔暗纹、铁链 | 暗褐红契纸，零星火苗 |
| 大恶魔 | 深狱领主 | 四角、四角恶魔纹章、赤红链条反光 | 深绯红契纸，两侧连续火舌与局部底焰 |
| 撒旦 | 撒旦敕印 | 卷角、三尖王冠、三叉戟王印 | 鲜猩红契纸，四边环绕赤焰与橙金焰芯 |

火焰目前是 PNG 中的静态表现，没有新增运行时动画或粒子。

## 宽版对比

第一档：狱契之印（此前已认可）。

![狱契之印](../../Assets/Sprites/Namecards/devil-service-wide.png)

第二档：深狱领主。

![深狱领主](../../Assets/Sprites/Namecards/archdevil-service-wide.png)

第三档：撒旦敕印。

![撒旦敕印](../../Assets/Sprites/Namecards/satan-service-wide.png)

## 紧凑版

第二档：深狱领主。

![深狱领主紧凑版](../../Assets/Sprites/Namecards/archdevil-service-4x3.png)

第三档：撒旦敕印。

![撒旦敕印紧凑版](../../Assets/Sprites/Namecards/satan-service-4x3.png)

## 交付与验证

- 四张新增 PNG 已逐张目检，均可用 System.Drawing 解码，RGB 全不透明，无烘焙玩家姓名、金额或状态文字。
- 两张宽版均为 2048×768，精确 8:3；两张紧凑版均为 1448×1086，精确 4:3，按各自宽版重新构图。
- 已核对四张项目副本与生成源图的 SHA-256 一致。
- [实际生成及修订提示词、源文件记录](infernal-higher-tiers-prompts-v1.json)。
- 本轮为美术预览，强调用户要求的红色和火焰递进。边饰已收敛，但部分火舌、角饰仍可能超出标准 64 px 装饰带，尤其撒旦宽版两侧和四角；这些文件尚未通过生产用安全区验收。接入时应结合 Inspector 参考线继续局部收边，保留火焰覆盖率和鲜红强度，并叠加实际 UI 验证。
- 尚未接入 Assets、商品 SO、服役阈值、服务器投影或统一名片覆盖逻辑。未运行 Unity 编译、EditMode、房间/牌桌/排行榜实机测试；运行时小尺寸、长姓名与状态可读性尚待验证。

| 文件 | SHA-256 |
| --- | --- |
| archdevil-service-wide-v1.png | `3595CCDFF6A420EECD588E65046514181E6E16A02634C7E7DA90826F9E25E33B` |
| archdevil-service-4x3-v1.png | `737EA5F953FB63166C29462204E20612BD9003F2C8A3D4F08A2693327E8F9EFE` |
| satan-service-wide-v1.png | `191AA30642791E29A12291A769F98EC5B5E987E3E9F40E362C1771E462515063` |
| satan-service-4x3-v1.png | `E27A9EECCA0F8200B5D9D69453AC10938E69EF5865411841BD5A195D5A074FB0` |
