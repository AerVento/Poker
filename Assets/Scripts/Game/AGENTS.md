# Game 目录说明

本目录放不依赖网络收发的游戏逻辑和客户端启动入口。命名空间以 `Game` 为主；`Achievements`、`Content`、`Events`、`Input`、`Tasks` 沿用各自已有的 `Game.*` 命名空间，新增子目录不新建命名空间。

## 子目录

| 目录 | 内容 |
| --- | --- |
| `App/` | 常驻启动入口 `GameController`（含 Editor 本地主机）、客户端账户会话 `GameSettings`、登录凭据存储 |
| `Poker/` | 纯牌型与德州规则：扑克牌、牌型评估与比较、座位与盲注规则 `HoldemRules`、边池计算 `PotCalculator` |
| `Items/` | 物品与商城目录 `GameCatalog`：物品表、名片／牌面／礼物／契约等功能目录、商城报价，及其校验与契约规则（刑期、欠款、服刑名片） |
| `Economy/` | 货币、库存快照与物品请求类型、购买条件、统一奖励 |
| `Achievements/` | 成就定义、判定规则与实例，流程见 [poker-achievements](../../../.agents/skills/poker-achievements/SKILL.md) |
| `Tasks/` | 可配置每日任务的规则、奖励与配置解析，以及任务使用的牌局事实 `HandFacts` |
| `Events/` | 玩家事件模型与事件来源注册 |
| `Audio/` | 音频库、音频管理与牌局音效状态 |
| `Content/` | 远程内容管理器 `RemoteAssets`：内容版本与目录、浏览器本地记录、缺失／失效／生效判定、包依赖与版本、批量下载与替换、引用计数与绑定；纯规则在 `RemoteContentRules.cs`、`RemoteContentPackages.cs`、`RemoteContentPlanner.cs` |
| `Input/` | 牌局快捷键绑定 |
| `Editor/` | 长期使用的 Editor 工具：构建钩子、配置编辑器、Inspector 扩展与校验菜单 |

## 约束

- `Poker/` 与规则类代码保持为普通 C# 类型，不依赖 `MonoBehaviour`、Mirror 或 UI，方便在 `Assets/Tests/Editor/` 中直接测试。
- 需要网络收发、会话或存储的逻辑放到 `Network/` 对应的功能目录，本目录只保留可在两端复用的模型与规则。
- 界面代码放到 `UI/`，不要在本目录创建面板或控件。
- `Editor/` 不放一次性迁移脚本和测试，规则见根目录 `AGENTS.md`。
