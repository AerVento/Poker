# Poker UI Prefab Convention

本文记录仓库现有 uGUI 面板的实际约定。新增或修改 UI prefab 时应遵循本文件，并使用 Unity Editor（可通过 Unity MCP）完成资源和序列化引用修改。

## 1. 目录与命名

- 可独立显示的面板放在 `Assets/Prefabs/UI/Panels`。
- 结构较小的单文件面板直接使用 `Panels/<PanelName>.prefab`，例如 `TitlePanel.prefab`、`MsgBox.prefab`。
- 带专用列表项或嵌套元素的面板使用 `Panels/<PanelName>/` 子目录，例如 `GamePanel/GamePanel.prefab` 和 `RoomPanel/RoomPanel.prefab`。
- 面板 prefab 根对象、脚本类型和 prefab 文件名保持一致，例如根对象 `RoomPanel` 挂载 `Game.UI.RoomPanel`。
- 可复用列表项使用职责名称，例如 `Player`、`RoomItem`、`RoundResultListItem`；不要把面板脚本挂在列表项根对象上。

## 2. 层级结构

- 面板根对象使用 `RectTransform`，通常同时具有 `Image` 和对应面板脚本。
- 常用直接子节点命名为 `Title`、`Status`、`Buttons`、`Settings`、`PlayerList` 等，名称表达职责而不是视觉坐标。
- 多按钮区域使用名为 `Buttons` 的容器和 `HorizontalLayoutGroup` 或 `VerticalLayoutGroup`。
- 滚动列表沿用 `ScrollRect -> Viewport -> Content/Players` 结构；内容节点使用布局组件和 `ContentSizeFitter`。
- 输入框和按钮沿用 TextMeshPro/uGUI 组合：`TMP_InputField`、`TextMeshProUGUI`、`Button`。
- 已有可复用视觉元素优先作为 nested prefab 使用，例如牌局中的 `Poker.prefab`，不要复制其内部 YAML。

## 3. 面板脚本与生命周期

- 单实例页面继承 `SingletonPanel<T>`，默认以类型名作为 identifier，例如 `TitlePanel`、`RoomPanel`、`GamePanel`。
- 允许同时存在多个实例的系统弹窗继承 `BasePanel` 并显式使用唯一 identifier，例如 `MsgBox`。
- Inspector 引用使用私有字段和 `[SerializeField]`，字段按 Buttons、Labels、Inputs 等职责分组。
- 在 `OnEnable`/`OnDisable` 中成对注册和移除按钮、输入框及网络事件监听；不要依赖 prefab 中持久化的 `Button.onClick` 业务回调。
- `Show`、`Hide`、`DestroyPanel` 由 `UIManager` 统一管理。系统级阻塞或确认弹窗放在 `UIManager.Layer.System`。
- 动态列表项由所属面板持有 prefab 引用，在运行时实例化到布局容器中。

## 4. UIManager 注册

- 所有可由 `UIManager.CreatePanel/ShowPanel` 创建的面板 prefab 都必须注册到 `Assets/Prefabs/Manager/UIManager.prefab` 的 `_panels` 数组。
- 每种面板脚本类型只能注册一个 prefab；重复类型会在运行时产生警告。
- 新增面板时同时确认 `Canvas.prefab` 的 `Bot`、`Mid`、`Top`、`System` 四层仍存在，并选择符合用途的层级。
- 仅修改现有面板的子节点或序列化引用时，不新增 `_panels` 项。

## 5. 编辑与验证流程

1. 修改脚本后等待 Unity 编译完成并检查 Console error。
2. 通过 Unity Editor 打开目标 prefab stage，再新增节点、组件或设置序列化引用。
3. 保存并关闭 prefab stage，确认 prefab 没有未保存状态。
4. 新资源必须同时生成 `.meta`；移动和删除资源通过 Unity Editor 完成以保留 GUID。
5. 修改 `UIManager.prefab`、面板 prefab 或场景后，在 `Game.unity` 中检查 Missing Script、Missing Reference 和 Console error。
6. 对登录、房间、牌局等流程 UI，至少在 Play Mode 覆盖显示、关闭、断线重建和重复打开。

## 6. 本次断线恢复 UI 决定

- 断线和返回房间选择复用现有 `MsgBox`，因为它已经支持确认、取消、系统层显示和独立 identifier。
- 不为单一确认流程创建新的面板 prefab，也不修改 `UIManager._panels`。
- 确认与取消按钮文字由 `MsgBox` 运行时属性设置，以便表达“重新登录”和“离开房间”，同时保持现有 prefab 结构不变。

## 7. 账户入口与设置页

- `TitlePanel` 只负责初始入口，保留“开始游戏”按钮，不承载账户输入、注册或设置逻辑。
- `LoginPanel` 是独立单实例面板，统一承载用户名、密码、登录、注册、返回和认证状态；注册成功直接进入已登录状态。
- 大厅中的“设置”按钮负责打开 `OptionPanel`；设置页返回大厅，不再返回标题页。
- `OptionPanel` 只读显示不可修改的账户用户名，不使用 `TMP_InputField` 模拟文本，并提供撤销当前持久会话的“登出”按钮。
- 认证成功前不得打开 `RoomListPanel`；网络连接成功与账户认证成功是两个独立状态。
