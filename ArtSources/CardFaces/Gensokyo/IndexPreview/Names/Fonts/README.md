# 角色名称候选字体

2026-10-05 从官方项目下载。霞鹜文楷 Medium 已用于全部52张牌面的角色名拼接；其他字体仅用于比较。字体未安装到系统，也未作为运行时字库导入 Unity；角色名已烘焙在 PNG 中。所有字体按原文件保存，没有修改字库。许可证随字体保留。

| 字体 | 下载来源 | 许可 |
| --- | --- | --- |
| 朱雀仿宋 v0.212（预览版） | https://github.com/TrionesType/zhuque/releases/tag/v0.212 | SIL OFL 1.1，`Zhuque/LICENSE.txt` |
| 霞鹜文楷 Medium v1.522 | https://github.com/lxgw/LxgwWenKai/releases/tag/v1.522 | SIL OFL 1.1，`lxgw-OFL.txt` |
| Ma Shan Zheng Regular | https://github.com/google/fonts/tree/main/ofl/mashanzheng | SIL OFL 1.1，`ma-shan-zheng-OFL.txt` |

字库 cmap 检查以 DeckGeneration.json 中的全部角色名为准：霞鹜文楷无缺字；朱雀仿宋缺「鵺」；Ma Shan Zheng 缺「椛、洩、·、燐、塚、鵺、咲」。毛笔楷书示例的间隔点使用浏览器后备字形；若选用该方案，正式导出前须明确统一缺字素材，不能依赖静默字体回退。

正式名称和预览均为 36px、字距 2px、颜色 #75644B、顶部文字条 y=66 高 44，使用真实字体加载后生成透明文字图片，再叠加到牌面。字体比较见 `../Online/font-comparison.png` 和 `../Online/fonts.json`；正式图层与完整清单见 `../Final/`。
