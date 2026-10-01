---
name: poker-network-messages
description: 在 Poker Unity 项目中新增或修改 Mirror 网络消息，包括消息定义、复杂字段的序列化、服务端与客户端处理器注册、身份校验、断线清理和联机验证。用于新增请求/响应、给现有消息加减字段、调整同步数据等请求。
---

# Poker 网络消息

服务端是权威方：客户端只发请求，服务端校验后回复结果或广播状态。先读根目录和 [Network/AGENTS.md](../../../Assets/Scripts/Network/AGENTS.md)，检查工作区已有改动。

## 定义消息

- 在 `Assets/Scripts/Network/Messages/` 定义 `public struct Xxx : NetworkMessage`，命名空间 `Game.Network`。同一功能的请求与响应放在一个 `<功能>Messages.cs` 文件。
- 只有 public 字段会被传输，属性不会。简单类型（数值、`string`、`bool`、枚举）直接写字段。
- 复杂类型（类、嵌套结构、集合）也直接声明为字段，并在 `Messages/Serialization.cs` 添加一对 `WriteXxx(this NetworkWriter, Xxx)` / `ReadXxx(this NetworkReader)`。Mirror 会自动使用它们，业务代码读取字段时不会重复解析。不要再用 `RawData` 字符串加属性的写法。
- 结果用枚举（`enum XxxResult : byte`），不要用魔法数字。
- 请求带 `RequestId`；响应回传 `RequestId` 和 `AccountId`，客户端据此丢弃过期响应、防止重复提交。牌局相关请求再带 `RoomId`、`HandId`、`TurnId` 等状态标识。

## 服务端处理

- 在 `NetworkRoom/NetworkRoom.<功能>.cs` 的 `Register<功能>ServerHandlers()` 里用 `RegisterServerHandler<T>(..., requireAuthentication: true)` 注册，并确认该方法在 `OnStartServer` 中被调用。
- 玩家身份用 `TryGetAuthenticatedPlayer(conn, ...)` 或 `PlayerManager` 的会话取得。消息里的玩家名、座位号、价格等只能用来核对，不能直接采用。
- 需要按连接保存的状态（请求序号、缓存），在 `OnServerDisconnect` 中移除，在 `OnStopServer` 中清空。
- 广播时，所有接收者共用的数据只查询一次；发给具体玩家的牌局数据只能来自 `ClientGameInfo` 的裁剪视图。
- 不要为每次请求或每次同步写日志。

## 客户端处理

- 在同一 partial 文件的 `Register<功能>ClientHandlers()` 中用 `NetworkClient.RegisterHandler` 注册，并确认在 `OnStartClient` 中被调用。
- 处理响应前核对当前账户、房间和请求序号，界面可能已经关闭或切换；面板用 `Xxx.Instance?.` 访问。
- 界面只展示服务端返回的结果，不能先显示成功再忽略失败。

## 修改现有消息

改字段或类型时，同步检查：服务端状态、`ClientGameInfo` 等裁剪视图、消息体、`Serialization.cs`、发送端、接收端和 UI 使用处。客户端与服务端必须一起更新，发布说明中写明不兼容旧版本。

## 验证

- Unity 编译通过且 Mirror Weaver 无报错（Console 中没有 `Weaver` 错误）。
- 在 `Assets/Tests/Editor/` 为纯逻辑部分写临时测试。
- 以一个服务端、两个客户端覆盖正常请求、拒绝、重复提交、断线重连和过期响应；未做时明确说明。
