# 新版本本地验证（2026-09-09）

使用 Unity 2022.3.52f1c1，对当前工作区执行测试。结果：675 项 EditMode 全部通过，0 失败、0 跳过；本轮离线界面检查未发现阻断问题。

按用户要求跳过联机验证；臭鸡蛋不属于本版本，本轮未测试、未打开其演示场景。未执行平台构建或发布。

## 自动化回归

- 筛选：`Game.Tests`，EditMode；作业 `cb84b37f125a4adba5515e1330b2f3d3`。
- Unity 报告用例运行时间 28.91 秒。
- 原始逐项结果：`Logs/LocalVersionValidation-20260909/EditModeResults.json`（本地忽略目录）。
- 本次实际重新执行测试，没有沿用其他文档中的历史通过记录。

| 新功能或重点范围 | 测试类 | 通过数 |
| --- | --- | ---: |
| 成就判定、持久化、领奖及界面 | AchievementTest、AchievementUITest | 22 |
| 默认牌面、购买装备、持久化、奖励及隐藏牌 | CardFaceTest | 6 |
| 同花顺／皇家同花顺奖励名片 | RewardNamecardTest | 10 |
| 契约签约、还款、失败回滚及重复请求 | ContractPurchaseTest | 43 |
| 配置任务和每日任务 | ConfiguredTaskTest、DailyTaskTest | 80 |
| 商城、收藏、物品发放 | ItemShopTest、ItemCollectionTest、ItemGrantTest | 38 |
| 结算与排行榜 | HandSettlementAndLeaderboardTest | 40 |
| UI 交互和导航 | UIInteractionTest、UINavigationTest | 111 |
| 音频逻辑与资源 | AudioTest | 34 |
| 其余牌型、规则、账户、名片、布局等回归 | 其余 Game.Tests | 291 |
| 合计 | | 675 |

这些用例中的服务端规则、SQLite 临时库及序列化检查均在本地执行，不代表完成了真实服务器／客户端联调。

## 本轮 Play Mode 检查

在 `Game.unity` 中进行 1920×1080 检查。为保持离线，临时禁用 GameController 自动启动，保留真实 UIManager、面板与资源，通过内存样例呈现界面；没有保存场景改动。检查期间 NetworkClient.active 和 NetworkServer.active 均为 false。

- 标题页正常显示，成就入口中心的 EventSystem 射线命中正确按钮。
- 使用当前真实目录中的 18 个成就显示列表；通过 EventSystem 射线和 PointerClick 分发切换分类，数量为财富 6、牌型 8、特殊 4、通用 0、全部 18。
- 成就列表可滚动到底部，最后一行完整可见。内容高度 2938、视口高度 594，底部 normalizedPosition 为 0。
- 成就横幅顺序显示两个条目，每个重复入队一次后仍总共确认 2 次；底层导航保持 TitlePanel，横幅不拦截射线和交互。
- 商城展示经典与简约两套牌面、各自正面和牌背、免费默认款及装备状态。离线样例的操作按钮禁用符合预期；未执行在线购买／装备按钮闭环。
- 实际分发点击切换商城到收藏，再切换名片、牌面页签，模式和页签状态正确。
- 在真实 PokerElement 上逐一切换两套牌面并遍历全部扑克牌：每套 52 个独立正面资源；隐藏时保持所选牌背，翻开后匹配对应正面，共 208 次运行时断言通过。此计数不叠加到 675 项 NUnit 数量中。
- 结算样例输入 10 位玩家、其中 1 位弃牌，实际显示 9 人；每排 4 人，末行单人居中，可滚动到底部。公共牌和底牌均使用简约牌面，牌型文字正常显示。
- Play Mode 结束前 Console error／warning 均为 0。开始检查前 Console 中已有一条连接拒绝日志；本轮离线检查没有新增网络连接错误。

截图保存在 `Logs/LocalVersionValidation-20260909/`：`title.png`、`achievements-1.png`、`achievements-scrolled.png`、`cardfaces.png`、`result-minimal.png`、`result-bottom.png`。成就及商城画面使用测试展示数据，并非已登录账户。

## 资源与恢复

- Game 场景校验：0 问题、0 Missing Script、0 broken prefab。
- 正式场景依赖的 34 个自有预制体、2724 个组件检查：无丢失脚本或非空引用断链。该项不把合法的可选空字段当作错误。
- 启用的构建场景只有 Game；Game 的预制体依赖中没有礼物预制体。此项是依赖检查，不是构建产物检查。
- 已停止 Play Mode，恢复 GameController.enabled=true；当前仍为 Game.unity，场景未变脏，无脚本正在编译。
- 保存本报告前，git status 与测试前快照完全一致；本轮未改业务代码、prefab 或场景，保留所有已有工作区改动。

本轮结论仅覆盖上述本地自动化与离线界面范围。真实登录、同步、在线交易／领奖闭环、多端牌局，以及 Linux／WebGL／Windows 构建均未在本轮验证。
