# Framework 目录说明

本目录是与本游戏无关、可在其他项目复用的通用设施：单例、UI 管理、事件、状态机、计时器、输入、日志、序列化与配置读写。命名空间为 `Framework.*`。

## 约束

- 不依赖 `Game`、`Game.Network`、`Game.UI` 等游戏代码。新代码不要增加这类依赖；现有 `BasePanel` 引用 `Game.UI.PanelShortcutHandler` 是已知例外，不要照此扩展。
- 本目录的代码即使当前没有调用方也保留，不要因为“未被使用”而删除或提议删除。
- 只放通用能力。与扑克、账户、商城等业务相关的逻辑放到 `Game/`、`Network/` 或 `UI/`。
- 修改公共 API 时检查全项目调用方；保留兼容入口（如 `UIManager.ShowPanel`）除非用户要求移除。
