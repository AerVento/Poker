# 简约款 / Minimal

原资源：Bad Hand Studio / CaptainLaptop — Simplified Playing Cards
来源：https://badhandstudio.itch.io/simplified-playing-cards
下载日期：2026-09-09。项目按非商业用途使用。

作者页面的许可原文："Free to use but donations always thankful."
此说明只记录作者的原声明，不将资源重新标记为 CC0 或其他标准许可。

## 下载文件

- `SimplifiedPlayingCards.svg`：原始完整 52 张矢量图；保持原样。
- `CardBacks.svg`：原始牌背图；保持原样。

## 本项目调整

保留原有 Arial Bold 字形和四种花色造型，将点数放到左上角、花色放到右下角；白底、红黑双色。导出统一 408×576 透明圆角 PNG，牌背使用原包第三款红色几何线条图。没有使用 AI 生成牌面。

`export.py` 从原始 SVG 读取点数和花色，使用 Windows 自带 Arial Bold 将字形转为轮廓后导出；字体文件不随项目分发。安装工具依赖 `resvg-py==0.5.0`、`fonttools` 后，在仓库根目录运行 `python ArtSources/CardFaces/Minimal/export.py` 即可重新导出。勿改变已经导入 Unity 的 PNG 文件名或删除其 `.meta`。

资源 ID：`minimal`；统一物品 ID：`cardface-minimal`；界面名：简约款。所有新老玩家默认拥有，初始装备仍为经典款。

## 原始文件 SHA-256

- `CardBacks.svg`: `eebca5041e4a06ac4e087556dbd8d243b377934aa612fce053f3a598d18fbf0b`
- `SimplifiedPlayingCards.svg`: `79937bc925532af240cf6283147b0d2eb6eabdfe6be70e389f54bba4bea9b94e`
