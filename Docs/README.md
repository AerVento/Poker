# Poker 文档

描述项目当前的行为、配置和规范。代码结构与全局约束见根目录 [AGENTS.md](../AGENTS.md)，反复执行的操作流程见 `.agents/skills/`。

## Systems：各系统如何运作

| 文档 | 内容 |
| --- | --- |
| [TableAndRooms](Systems/TableAndRooms.md) | 房间、参战与旁观、一手的流程、超时与断线、结算后流程、牌桌布局、存储故障停服 |
| [Economy](Systems/Economy.md) | 美金与筹码、兑换、物品与功能目录（`Configs/Items`）、商城（`Configs/Shop`）、初始发放、购买与消耗、恶魔契约、服务器发放 |
| [Namecards](Systems/Namecards.md) | 名片配置与获取方式、购买与装备、显示规则、恶魔名片 |
| [CardFaces](Systems/CardFaces.md) | 牌面获取方式、数据结构、美术要求、商城与装备 |
| [Gifts](Systems/Gifts.md) | 礼物配置、服务器规则、客户端交互、Lua 特效接口与沙箱、预览场景 |
| [DailyTasks](Systems/DailyTasks.md) | 每日任务配置、任务类型、分配与生命周期、领取 |
| [Achievements](Systems/Achievements.md) | 成就定义、事件来源、保存与领奖、客户端缓存、判定口径 |
| [Leaderboard](Systems/Leaderboard.md) | 富豪榜与服役榜 |
| [AudioAndSettings](Systems/AudioAndSettings.md) | 音频资源与播放规则、设置面板、快捷键、本地保存 |
| [SlotMachine](Systems/SlotMachine.md) | 筹码抽奖、服务端加权奖池与扣费发奖、通用奖励物品框 |

## Operations：部署与运维

| 文档 | 内容 |
| --- | --- |
| [Deployment](Operations/Deployment.md) | 配置目录、网络配置、启动方式、发布打包、数据库 |
| [RemoteArtwork](Operations/RemoteArtwork.md) | 远程内容：内容与格式版本、包依赖与版本、缺失／失效／生效、页面内更新与刷新提示、构建发布、本地验证 |

## Standards：规范

| 文档 | 内容 |
| --- | --- |
| [UIConvention](Standards/UIConvention.md) | UI 工作的强制规范与验收要求 |
| [NamecardArt](Standards/NamecardArt.md) | 名片美术尺寸、边框与文字安全区 |
| [Configuration](Standards/Configuration.md) | `Configs/` 配置文件的字段命名、必填与可选、结构和校验 |
| [LuaConvention](Standards/LuaConvention.md) | 远程更新的 Lua：入口与共享模块的放置、依赖声明、版本号与锁文件 |

## Proposals：未实施的方案

| 文档 | 内容 |
| --- | --- |
| [AchievementLua](Proposals/AchievementLua.md) | 用 Lua 定义成就、支持热更新的设计方案 |

美术生成记录（提示词、旧版原图、图集）不在这里，见 `ArtSources/` 各子目录。
