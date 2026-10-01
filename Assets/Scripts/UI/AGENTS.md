# UI 目录说明

开始任何 UI 工作前，完整阅读 [项目 UI 规范](../../../Docs/Standards/UIConvention.md)，它是界面工作的验收标准。本文件只说明目录归属。命名空间以 `Game.UI` 为主，既有的 `Game.UI.*` 子命名空间保持不变，新文件不再新建子命名空间。

## 子目录

- 每个面板一个目录，脚本与 `Assets/Prefabs/UI/Panels/<同名目录>/` 下的 prefab 对应：`TitlePanel`、`LoginPanel`、`LobbyPanel`、`RoomListPanel`、`RoomPanel`、`GamePanel`、`ShopPanel`（商城与仓库）、`OptionPanel`（游戏内外设置）、`AchievementPanel`（页面、横幅与列表项）、`DailyTaskPanel`、`LeaderboardPanel`、`ChipExchangePanel`、`MsgBox`。
- 面板的 partial 文件、列表项和专用组件放在该面板目录。
- `Shared/`：被两个以上功能使用的控件与表现组件，例如 `CardFaceView`、`CardFaceAppearance`、`RollingBalance`、`SwitchControl`。对应 prefab 放在 `Assets/Prefabs/UI/Shared/`。
- `Namecards/`：名片外观组件。
- `Gifts/`：礼物菜单、交互与特效脚本，prefab 在 `Assets/Prefabs/Gifts/`。

一个组件从单个面板扩展到多个功能使用时，把脚本和 prefab 一起移到 `Shared/`（连同 `.meta`），不要跨面板目录互相引用。

## 约束

- 不要把界面实现放进 `Game/`、`Network/` 或场景临时脚本；通用 UI 管理设施在 `Framework/UI/`。
- 面板布局用 prefab 维护，不要在运行时用 `new GameObject` 拼整块界面。
- Editor 工具放在所属面板目录的 `Editor/` 子目录，只保留长期使用的工具；一次性迁移脚本用完即删。
